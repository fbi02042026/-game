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
    const float MoveSpeed = 3.2f;

    float _damage = 1f;
    UnitBase _caster;
    float _hitRadius = 2.5f;
    int _maxTargets = int.MaxValue;
    float _remainDist;
    Vector2 _dir = Vector2.right;
    readonly HashSet<UnitBase> _hit = new HashSet<UnitBase>();
    float _scanTimer;

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
        ex._remainDist = skill.aoeRadius > 0f ? skill.aoeRadius : 7f;

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
        Vector2 step = _dir * (MoveSpeed * dt);
        transform.position += (Vector3)step;
        _remainDist -= step.magnitude;

        _scanTimer -= dt;
        if (_scanTimer <= 0f)
        {
            _scanTimer = ScanInterval;
            Scan();
        }

        if (_remainDist <= 0f || _hit.Count >= _maxTargets)
            Destroy(gameObject);
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
