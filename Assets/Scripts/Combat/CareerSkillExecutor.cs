using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 职业自带技能的演出与伤害（2026-11-13 主人拍板）。
/// 数值全部读 player_skills.csv 合成的 ActiveSkill（damageMultiplier / aoeRadius /
/// aoeMaxTargets / projectileCount / cooldown），本类不写第二份数值。
/// 伤害统一走 TakeDamage(source=Hero)，末尾 ApplyHitKnockback 复用已有击退协程与 AI 避让；
/// 借新增的 knockbackDistanceOverride 把普通怪推得更远（精英/Boss 仍按 11-05 口径免退）。
/// 特效全部复用项目现成共用套（刀光 / 弓 / 法球飞行体），不新增任何资源与预制体。
/// </summary>
public static class CareerSkillExecutor
{
    public const string ChargeSkillId = "career_charge";
    public const string ScatterSkillId = "career_scatter";
    public const string OrbSkillId = "career_orb";

    public static bool HasVisibleEnemy()
    {
        var bm = BattleManager.Instance;
        if (bm == null || bm.monsters == null) return false;
        for (int i = 0; i < bm.monsters.Count; i++)
        {
            var m = bm.monsters[i];
            if (m == null || m.isDead) continue;
            if (!GameConfig.IsInCombatViewport(m)) continue;
            return true;
        }
        return false;
    }

    public static bool Cast(MonoBehaviour runner, PlayerJobId job, string skillId)
    {
        var hero = Hero.Instance;
        if (runner == null || hero == null || hero.isDead) return false;
        var reg = SkillRegistry.Instance;
        var skill = reg != null ? reg.GetActiveSkill(skillId) : null;
        if (skill == null)
        {
            Debug.LogError($"[CareerSkill] 取不到职业技能配置，拒绝释放: {skillId}");
            return false;
        }
        if (BattleVFXSystem.Instance == null)
        {
            Debug.LogError("[CareerSkill] BattleVFXSystem 为空，拒绝释放职业技能");
            return false;
        }

        UnitBase target = PickTarget(hero);
        if (target == null) return false;

        SkillCastNotifyController.PushPlayerSkill(skillId, skill.skillName);

        switch (job)
        {
            case PlayerJobId.SwordShield:
                runner.StartCoroutine(CoCharge(hero, skill, target));
                break;
            case PlayerJobId.Ranger:
                runner.StartCoroutine(CoScatter(hero, skill, target));
                break;
            case PlayerJobId.Mage:
                runner.StartCoroutine(CoOrb(hero, skill, target));
                break;
            default:
                Debug.LogError($"[CareerSkill] 未支持的职业: {job}");
                return false;
        }
        return true;
    }

    static UnitBase PickTarget(Hero hero)
    {
        var bm = BattleManager.Instance;
        if (bm == null || bm.monsters == null) return null;
        UnitBase best = null;
        float bestD = float.MaxValue;
        Vector2 self = hero.transform.position;
        for (int i = 0; i < bm.monsters.Count; i++)
        {
            var m = bm.monsters[i];
            if (m == null || m.isDead) continue;
            if (!GameConfig.IsInCombatViewport(m)) continue;
            float d = Vector2.Distance(self, m.transform.position);
            if (d < bestD) { bestD = d; best = m; }
        }
        return best;
    }

    // —— 剑盾：高速横移突进，落地范围伤害 + 强击退 ——
    static IEnumerator CoCharge(Hero hero, SkillSystem.ActiveSkill skill, UnitBase target)
    {
        Vector3 start = hero.transform.position;
        float dir = Mathf.Sign(target != null ? target.transform.position.x - start.x : hero.GetVfxFacingDir());
        if (Mathf.Abs(dir) < 0.5f) dir = hero.GetVfxFacingDir() >= 0 ? 1f : -1f;
        float reach = target != null
            ? Mathf.Abs(target.transform.position.x - start.x) - GameConfig.CAREER_CHARGE_STOP_GAP
            : GameConfig.CAREER_CHARGE_DIST;
        float dist = Mathf.Clamp(reach, 0f, GameConfig.CAREER_CHARGE_DIST);
        // 只横移、不碰 Y：竖屏 lane 由 BattleLaneBounds 管，改 Y 会让单位脱离所在行。
        Vector3 end = start + new Vector3(dir * dist, 0f, 0f);

        Debug.Log($"[CareerSkill] 破阵冲锋 演出开始 dist={dist:0.00}");
        float dur = Mathf.Max(0.01f, GameConfig.CAREER_CHARGE_DURATION);
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            GameConfig.SetWorldPosition(hero.transform, Vector3.Lerp(start, end, Mathf.Clamp01(t / dur)));
            yield return null;
        }
        GameConfig.SetWorldPosition(hero.transform, end);
        DealArea(hero, skill, end, GameConfig.CAREER_SKILL_KNOCKBACK);
    }

    // —— 游侠：一次齐射多条独立箭道（结构照 Monster.CoFanVolley），每条飞到自己的终点再结算 ——
    static IEnumerator CoScatter(Hero hero, SkillSystem.ActiveSkill skill, UnitBase target)
    {
        float delay = GameConfig.RANGED_FIRE_RELEASE_DELAY_RANGER;
        if (delay > 0.001f) yield return new WaitForSeconds(delay);
        if (hero == null || hero.isDead || BattleVFXSystem.Instance == null) yield break;

        Vector3 fire = hero.GetFirePosition();
        int facing = hero.GetVfxFacingDir();
        Vector3 aim = target != null && !target.isDead
            ? target.GetHitPosition()
            : fire + Vector3.right * facing * GameConfig.CAREER_SCATTER_RANGE;
        Vector2 baseDir = new Vector2(aim.x - fire.x, aim.y - fire.y);
        if (baseDir.sqrMagnitude < 1e-6f) baseDir = new Vector2(facing, 0f);
        float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;
        float flyDist = Mathf.Min(baseDir.magnitude, GameConfig.CAREER_SCATTER_RANGE);

        int count = Mathf.Max(1, skill.projectileCount);
        Debug.Log($"[CareerSkill] 散射箭 演出开始 {count} 条箭道");
        var hitCount = new Dictionary<UnitBase, int>();
        float baseDamage = BuildDamage(hero, skill);

        for (int i = 0; i < count; i++)
        {
            float ang = (baseAngle + (i - (count - 1) * 0.5f) * GameConfig.CAREER_SCATTER_ANGLE_STEP) * Mathf.Deg2Rad;
            Vector2 d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            Vector3 end = fire + (Vector3)(d * flyDist);
            Vector3 lockedEnd = end;
            BattleVFXSystem.Instance.PlaySkillProjectile(
                VfxFaction.Ally, fire, end, facing, null, AttackVfxKit.Bow, null,
                1f, 1f,
                () => DealArrowHit(hero, lockedEnd, baseDamage, hitCount),
                caster: hero);
        }
    }

    static void DealArrowHit(Hero hero, Vector3 end, float baseDamage, Dictionary<UnitBase, int> hitCount)
    {
        var bm = BattleManager.Instance;
        if (bm == null || bm.monsters == null || hero == null) return;
        UnitBase best = null;
        float bestD = float.MaxValue;
        for (int i = 0; i < bm.monsters.Count; i++)
        {
            var m = bm.monsters[i];
            if (m == null || m.isDead) continue;
            if (!GameConfig.IsInCombatViewport(m)) continue;
            float d = Vector2.Distance(end, m.transform.position);
            if (d > GameConfig.CAREER_SCATTER_HIT_RADIUS || d >= bestD) continue;
            if (hitCount.TryGetValue(m, out int n) && n >= GameConfig.CAREER_SCATTER_MAX_HITS_PER_ENEMY) continue;
            bestD = d;
            best = m;
        }
        if (best == null) return;
        hitCount.TryGetValue(best, out int prev);
        hitCount[best] = prev + 1;
        HitOne(hero, best, baseDamage, end, GameConfig.CAREER_SKILL_KNOCKBACK);
    }

    // —— 法师：巨型法球慢速飞向目标，撞到后范围炸开 + 强击退 ——
    static IEnumerator CoOrb(Hero hero, SkillSystem.ActiveSkill skill, UnitBase target)
    {
        Vector3 fire = hero.GetFirePosition();
        int facing = hero.GetVfxFacingDir();
        Vector3 end = target != null && !target.isDead
            ? target.GetHitPosition()
            : fire + Vector3.right * facing * 8f;
        Vector3 lockedEnd = end;
        float scale = GameConfig.CAREER_ORB_SCALE_MUL;
        Debug.Log($"[CareerSkill] 大法球 演出开始 scale={scale:0.0}");
        BattleVFXSystem.Instance.PlaySkillProjectile(
            VfxFaction.Ally, fire, end, facing, target != null ? target.transform : null,
            AttackVfxKit.Orb, null, scale, GameConfig.CAREER_ORB_SPEED_MUL,
            () => DealArea(hero, skill, lockedEnd, GameConfig.CAREER_SKILL_KNOCKBACK),
            caster: hero, impactScaleMul: scale);
        yield break;
    }

    static float BuildDamage(Hero hero, SkillSystem.ActiveSkill skill)
    {
        return DamageFormula.BuildSkillBase(skill.baseDamage, skill.damageMultiplier,
            hero.attr, hero.IsMagicDamageDealer());
    }

    static void DealArea(Hero hero, SkillSystem.ActiveSkill skill, Vector3 center, float knockDist)
    {
        var bm = BattleManager.Instance;
        if (bm == null || bm.monsters == null || hero == null) return;
        var list = new List<UnitBase>();
        for (int i = 0; i < bm.monsters.Count; i++)
        {
            var m = bm.monsters[i];
            if (m == null || m.isDead) continue;
            if (!GameConfig.IsInCombatViewport(m)) continue;
            if (Vector2.Distance(center, m.transform.position) > skill.aoeRadius) continue;
            list.Add(m);
        }
        if (skill.aoeMaxTargets > 0 && list.Count > skill.aoeMaxTargets)
        {
            list.Sort((a, b) => Vector2.Distance(center, a.transform.position)
                .CompareTo(Vector2.Distance(center, b.transform.position)));
            list.RemoveRange(skill.aoeMaxTargets, list.Count - skill.aoeMaxTargets);
        }
        float baseDamage = BuildDamage(hero, skill);
        for (int i = 0; i < list.Count; i++)
            HitOne(hero, list[i], baseDamage, center, knockDist);
    }

    static void HitOne(Hero hero, UnitBase victim, float baseDamage, Vector3 center, float knockDist)
    {
        if (victim == null || victim.isDead) return;
        float dmg = DamageFormula.ApplyCrit(baseDamage, hero.attr, out bool isCrit);
        dmg = DamageFormula.ApplyAttackerSpecials(dmg, hero, victim);
        int dir = victim.transform.position.x >= center.x ? 1 : -1;
        victim.TakeDamage(dmg, isCrit, false, true, dir, hero, false, knockDist);
    }
}
