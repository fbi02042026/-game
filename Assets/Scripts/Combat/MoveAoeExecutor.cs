using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 移动判定 AOE（2026-11-05 主人拍板「风龙卷是移动的 所有范围也是移动的」，
/// 改造点见 Docs/《战斗趣味升级_pixelfx特效与打击反馈》_待办任务清单.md）：
/// 锚点绕怪群质心做圆周运动，判定盒随锚点走，周期性重叠检测；
/// 2026-11-14 改为可重复碰触：每只怪有独立内置 CD，每次扫描只结算 aoeMaxTargets 个最近目标；
/// 走完弧长 / 战斗结束 / 施法者销毁 → 自毁（VFX 子物体随之销毁）。
/// 表驱动入口：SkillType.MoveAoe（player_skills / merc_skills 均可），由 SkillSystem.UseSkill 分支进入。
/// 风龙卷与佣兵 SK021 烈焰龙卷共用本组件，轨迹 / 速度 / 碰触口径同步生效。
/// </summary>
public class MoveAoeExecutor : MonoBehaviour
{
    const float ScanInterval = 0.1f;
    // 2026-11-14：1.0 → 0.7（慢 30%）。3.5 弧长 / 0.7 = 5s 判定段，+2s 余韵 = 7s < prefab 8s 保险丝。
    const float MoveSpeed = 0.7f;
    // 弧长累计上限（原「直线漂移距离」）：绕圈也按走过的弧长收口，位置不再单向累加。
    const float MaxTravelDist = 3.5f;
    // 判定结束后的「余韵」（2026-11-08 主人追加「触发完了也可以再转一会消失」）：
    // 不再移动、不再结算伤害，只让 loop 特效再转一整轮才收。
    const float LingerDuration = 2f;
    // 圆周半径：取施法者到怪群质心的距离，钳在 0.8~1.5 —— 龙卷始终贴着怪群绕，不会飘出镜头。
    const float OrbitRadiusMin = 0.8f;
    const float OrbitRadiusMax = 1.5f;
    // 同一只怪两次受击的最小间隔（可重复碰触的内置 CD）。
    const float HitCooldown = 0.8f;
    // 每次扫描最多结算的敌人数上限；表 aoeMaxTargets 为 0 = 不限的技能不受此约束。
    const int MaxHitTargets = 6;

    float _damage = 1f;
    UnitBase _caster;
    float _hitRadius = 2.5f;
    int _maxTargets = int.MaxValue;
    float _remainDist;
    float _planeZ;
    Vector2 _orbitCenter;
    float _orbitRadius;
    float _orbitAngle;
    readonly Dictionary<UnitBase, float> _nextHitAt = new Dictionary<UnitBase, float>();
    readonly List<UnitBase> _candidates = new List<UnitBase>();
    float _scanTimer;
    /// <summary>≥0 = 已进入余韵（判定结束、只播特效）；-1 = 仍在漂移判定。</summary>
    float _lingerTimer = -1f;

    /// <summary>生成移动判定锚点。damage 由 SkillSystem.CalculateDamage 先算好（含攻击基底/星级）。</summary>
    public static void Spawn(SkillSystem.ActiveSkill skill, UnitBase caster, float damage)
    {
        if (skill == null || caster == null) return;
        var bm = BattleManager.Instance;
        if (bm == null) return;

        var go = new GameObject($"MoveAoe_{skill.skillId}");

        var ex = go.AddComponent<MoveAoeExecutor>();
        ex._caster = caster;
        ex._damage = Mathf.Max(1f, damage);
        // 判定盒半径：表半径的缩小档，钳在 2~3.5（视觉放大 30% 不改判定盒）
        ex._hitRadius = Mathf.Clamp(skill.aoeRadius > 0f ? skill.aoeRadius * 0.4f : 2.5f, 2f, 3.5f);
        if (skill.aoeMaxTargets > 0)
            ex._maxTargets = Mathf.Min(skill.aoeMaxTargets, MaxHitTargets);
        ex._remainDist = Mathf.Min(skill.aoeRadius > 0f ? skill.aoeRadius : 7f, MaxTravelDist);

        // 圆心 = 屏内怪群质心；半径取施法者到质心的距离（钳 0.8~1.5）。
        // 圆周起点就是生成点 → 龙卷直接出现在怪群里，第一帧不跳变，也不会一路飘出镜头。
        Vector3 origin = caster.GetHitPosition();
        Vector2 self = origin;
        ex._planeZ = origin.z;
        Vector2 sum = Vector2.zero;
        int count = 0;
        var enemyList = caster.isAlly ? bm.monsters : bm.allyUnits;
        if (enemyList != null)
        {
            for (int i = 0; i < enemyList.Count; i++)
            {
                var e = enemyList[i];
                if (e == null || e.isDead) continue;
                if (!GameConfig.IsInCombatViewport(e)) continue;
                sum += (Vector2)e.transform.position;
                count++;
            }
        }
        ex._orbitCenter = count > 0 ? sum / count : self;
        Vector2 off = self - ex._orbitCenter;
        ex._orbitRadius = Mathf.Clamp(off.magnitude, OrbitRadiusMin, OrbitRadiusMax);
        ex._orbitAngle = Mathf.Atan2(off.y, off.x);
        Vector2 start = ex._orbitCenter
            + new Vector2(Mathf.Cos(ex._orbitAngle), Mathf.Sin(ex._orbitAngle)) * ex._orbitRadius;
        go.transform.position = new Vector3(start.x, start.y, ex._planeZ);

        // VFX 挂在锚点上随行（prefab 根挂 PixelFxSheetPlayer；龙卷类 sheet 自带 loop）
        var fxPrefab = SkillRegistry.Instance != null
            ? SkillRegistry.Instance.GetSkillVfxPrefab(skill.skillId) : null;
        if (fxPrefab != null)
        {
            var fx = Object.Instantiate(fxPrefab, go.transform);
            fx.transform.localPosition = Vector3.zero;
            fx.transform.localScale = Vector3.one;
        }
        else
        {
            // 不兜底：判定照走、演出缺席，让主人看见缺了哪个
            Debug.LogError($"[MoveAoe] 技能特效缺失 skillId={skill.skillId}（VFX/Skills 下无此 prefab）");
        }
    }

    void Update()
    {
        if (BattleManager.Instance == null)
        {
            Destroy(gameObject);
            return;
        }

        float dt = Time.deltaTime;

        // 余韵阶段：判定已结束，不移动、不 Scan（伤害结算已在判定阶段收口，绝不多打一次），
        // 只让挂在锚点上的 VFX 继续 loop 播完 LingerDuration 才销毁锚点。
        if (_lingerTimer >= 0f)
        {
            // 沿用原语义：施法者已销毁则判定作废，余韵一并作废，不留无主特效。
            if (_caster == null) { Destroy(gameObject); return; }
            _lingerTimer -= dt;
            if (_lingerTimer <= 0f) Destroy(gameObject);
            return;
        }

        // 圆周：角速度 = 线速度 / 半径；位置直接落在圆周上，不累加单向位移。
        _orbitAngle += (MoveSpeed / Mathf.Max(0.01f, _orbitRadius)) * dt;
        Vector2 radial = new Vector2(Mathf.Cos(_orbitAngle), Mathf.Sin(_orbitAngle));
        Vector2 p = _orbitCenter + radial * _orbitRadius;
        transform.position = new Vector3(p.x, p.y, _planeZ);
        _remainDist -= MoveSpeed * dt;

        _scanTimer -= dt;
        if (_scanTimer <= 0f)
        {
            _scanTimer = ScanInterval;
            Scan();
        }

        // 走完弧长后不立刻销毁 —— 进余韵再转一整轮；判定段完全由 _remainDist 收口，与命中数解耦。
        if (_remainDist <= 0f)
            _lingerTimer = LingerDuration;
    }

    void Scan()
    {
        if (_caster == null)
        {
            // 施法者已销毁（佣兵阵亡被回收）——本判定的伤害来源没了，按战斗口径作废自毁
            Destroy(gameObject);
            return;
        }
        var bm = BattleManager.Instance;
        if (bm == null) return;
        var enemyList = _caster.isAlly ? bm.monsters : bm.allyUnits;
        if (enemyList == null) return;

        Vector2 center = transform.position;
        float now = Time.time;
        _candidates.Clear();
        for (int i = 0; i < enemyList.Count; i++)
        {
            var e = enemyList[i];
            if (e == null || e.isDead) continue;
            if (!GameConfig.IsInCombatViewport(e)) continue;
            if (Vector2.Distance(center, e.transform.position) > _hitRadius) continue;
            if (_nextHitAt.TryGetValue(e, out float readyAt) && now < readyAt) continue;
            _candidates.Add(e);
        }
        if (_candidates.Count == 0) return;

        // 就近优先：每次扫描只结算 _maxTargets 只，内置 CD 让同一只怪可以反复被刮到。
        _candidates.Sort((a, b) => Vector2.Distance(center, a.transform.position)
            .CompareTo(Vector2.Distance(center, b.transform.position)));
        int n = Mathf.Min(_candidates.Count, _maxTargets);
        for (int i = 0; i < n; i++)
        {
            var e = _candidates[i];
            _nextHitAt[e] = now + HitCooldown;  // 先记 CD 再结算：打死触发波次逻辑也不会同帧二次进入
            float dmg = DamageFormula.ApplyCrit(_damage, _caster.attr, out bool isCrit);
            dmg = DamageFormula.ApplyAttackerSpecials(dmg, _caster, e);
            int vfxDir = e.transform.position.x >= center.x ? 1 : -1;
            e.TakeDamage(dmg, isCrit, false, true, vfxDir, _caster);
        }
    }
}
