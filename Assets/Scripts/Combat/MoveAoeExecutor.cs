using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 移动判定 AOE（2026-11-05 主人拍板「风龙卷是移动的 所有范围也是移动的」，
/// 改造点见 Docs/《战斗趣味升级_pixelfx特效与打击反馈》_待办任务清单.md）：
/// 生成时锁定怪群方向，判定盒随锚点直线漂移，周期性重叠检测；
/// 每个目标整个持续期最多吃 1 次伤害（防反复碾怪刷伤），命中名单容量 = aoeMaxTargets；
/// 走完漂移距离 / 命满名单 / 战斗结束 / 施法者销毁 → 自毁（VFX 子物体随之销毁）。
/// 表驱动入口：SkillType.MoveAoe（player_skills / merc_skills 均可），由 SkillSystem.UseSkill 分支进入。
/// 降级口径：若要「半径内游走」不必改本组件——把漂移方向每 0.5s 随机化即可（当前按主人首选的直线漂移实现）。
/// </summary>
public class MoveAoeExecutor : MonoBehaviour
{
    const float ScanInterval = 0.1f;
    // 2026-11-08 主人拍板「龙卷时间太短还没看到就没了」：漂移 3.2 → 1.0 单位/秒，
    // 与下面钳进镜头的漂移距离配合，龙卷在屏内稳定停留约 3.5 秒（原 2.2s，且命满 4 只即秒销毁）。
    const float MoveSpeed = 1.0f;
    // 漂移距离上限：战斗镜头半屏宽 ≈ CAMERA_ORTHO_SIZE × 720/1280 ≈ 3.04，取 3.5 保证走完仍在屏内。
    // 原样吃表半径(风7/火8)会一路飘出镜头，而 Scan 对屏外怪直接 continue —— 后半程纯空跑还看不见。
    const float MaxTravelDist = 3.5f;
    // 判定结束后的「余韵」（2026-11-08 主人追加「触发完了也可以再转一会消失」）：
    // 不再移动、不再结算伤害，只让 loop 特效再转一整轮才收。2 秒 = sheet 60 帧 @30fps 的完整一轮；
    // 总展示 3.5+2=5.5s < 两个龙卷 prefab 的 autoDestroyAfter=8s 保险丝，不会中途被掐掉。
    const float LingerDuration = 2f;

    float _damage = 1f;
    UnitBase _caster;
    float _hitRadius = 2.5f;
    int _maxTargets = int.MaxValue;
    float _remainDist;
    Vector2 _dir = Vector2.right;
    readonly HashSet<UnitBase> _hit = new HashSet<UnitBase>();
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
        go.transform.position = caster.GetHitPosition();

        var ex = go.AddComponent<MoveAoeExecutor>();
        ex._caster = caster;
        ex._damage = Mathf.Max(1f, damage);
        // 判定盒半径：表半径的缩小档（漂移的是「路径」，不是一锤子大圆），钳在 2~3.5
        ex._hitRadius = Mathf.Clamp(skill.aoeRadius > 0f ? skill.aoeRadius * 0.4f : 2.5f, 2f, 3.5f);
        if (skill.aoeMaxTargets > 0) ex._maxTargets = skill.aoeMaxTargets;
        ex._remainDist = Mathf.Min(skill.aoeRadius > 0f ? skill.aoeRadius : 7f, MaxTravelDist);

        // 生成瞬间锁「身边怪最密的那只」的方向 = 直冲怪群（与 SkillSystem.PickAoeCenter 同思想，简化成方向）
        Vector2 self = caster.transform.position;
        Vector2 best = self;
        int bestCount = -1;
        var enemyList = caster.isAlly ? bm.monsters : bm.allyUnits;
        if (enemyList != null)
        {
            for (int i = 0; i < enemyList.Count; i++)
            {
                var e = enemyList[i];
                if (e == null || e.isDead) continue;
                if (!GameConfig.IsInCombatViewport(e)) continue;
                int n = 0;
                for (int j = 0; j < enemyList.Count; j++)
                {
                    var o = enemyList[j];
                    if (o == null || o.isDead) continue;
                    if (Vector2.Distance(e.transform.position, o.transform.position) <= 3f) n++;
                }
                if (n > bestCount)
                {
                    bestCount = n;
                    best = e.transform.position;
                }
            }
        }
        Vector2 d = best - self;
        ex._dir = d.sqrMagnitude > 0.0001f
            ? d.normalized
            : new Vector2(Mathf.Abs(caster.GetVfxFacingDir()) < 0.5f ? 1f : caster.GetVfxFacingDir(), 0f);

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

        Vector2 step = _dir * (MoveSpeed * dt);
        transform.position += (Vector3)step;
        _remainDist -= step.magnitude;

        _scanTimer -= dt;
        if (_scanTimer <= 0f)
        {
            _scanTimer = ScanInterval;
            Scan();
        }

        // 2026-11-08：命满名单不提前销毁、走完漂移也不立刻销毁 —— 进余韵再转一整轮。
        // 伤害上限由 _hit 名单 + Scan 的 _maxTargets 判断保证，与展示时长彻底解耦。
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
        for (int i = 0; i < enemyList.Count; i++)
        {
            var e = enemyList[i];
            if (e == null || e.isDead || _hit.Contains(e)) continue;
            if (!GameConfig.IsInCombatViewport(e)) continue;
            if (Vector2.Distance(center, e.transform.position) > _hitRadius) continue;
            if (_hit.Count >= _maxTargets) break;

            _hit.Add(e);   // 先记名单再结算：打死触发波次逻辑也不会二次进入
            float dmg = DamageFormula.ApplyCrit(_damage, _caster.attr, out bool isCrit);
            dmg = DamageFormula.ApplyAttackerSpecials(dmg, _caster, e);
            int vfxDir = e.transform.position.x >= center.x ? 1 : -1;
            e.TakeDamage(dmg, isCrit, false, true, vfxDir, _caster);
        }
    }
}
