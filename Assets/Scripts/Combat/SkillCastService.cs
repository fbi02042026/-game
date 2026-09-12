using UnityEngine;

/// <summary>
/// 玩家 / 佣兵主动技施放。BattleManager 只接线与扣能量。
/// 佣兵分派走 <see cref="MercSkillExecutor"/>（表 Category/Target/Formula），无 SK### if。
/// 玩家战斗数走 player_skills Cook 表，不读 Ally SO 的伤害/治疗/Buff。
/// </summary>
public sealed class SkillCastService
{
    readonly BattleManager bm;

    public SkillCastService(BattleManager host)
    {
        bm = host;
    }

    Hero hero => bm.hero;
    System.Collections.Generic.List<UnitBase> allyUnits => bm.allyUnits;
    System.Collections.Generic.List<UnitBase> monsters => bm.monsters;
    BattleRunStats RunStats => bm.RunStats;
    float playerSkillEnergy { get => bm.playerSkillEnergy; set => bm.playerSkillEnergy = value; }

    void RecordAllyHeal(UnitBase healer, float amount) => bm.RecordAllyHeal(healer, amount);


    // ============================================================
    // 玩家技能释放（由头像点击触发）
    // ============================================================

    /// <summary>释放玩家技能（需要能量满）— 头像点击</summary>
    public bool TryUsePlayerSkill()
    {
        if (playerSkillEnergy < 0.99f) return false;
        if (hero == null || hero.isDead) return false;

        var skill = ResolvePlayerSkill();
        if (skill == null) return false;
        UnitBase healTarget = null;
        bool isHeal = IsHealSkill(skill);

        if (isHeal)
        {
            healTarget = FindPreferredHealTarget();
            ExecuteAllySkillFallback(hero, skill, healTarget);
        }
        else
        {
            bool ok = skill.skillType != SkillSystem.SkillType.Buff
                && SkillSystem.Instance != null
                && SkillSystem.Instance.UseSkill(skill, hero);
            if (!ok)
                ExecuteAllySkillFallback(hero, skill);
        }

        Vector3 vfxPos = healTarget != null ? healTarget.GetHitPosition() : hero.GetHitPosition();
        Transform vfxAttach = healTarget != null ? healTarget.transform : hero.transform;
        SkillRegistry.Instance?.PlaySkillVfx(skill.skillId, vfxPos, true, hero.GetVfxFacingDir(), vfxAttach);

        // #region agent log
        DebugAgentLog.Log("H5", "BattleManager.TryUsePlayerSkill", "skill_vfx_facing",
            $"{{\"facingDir\":{hero.facingDir},\"vfxDir\":{hero.GetVfxFacingDir()},\"scaleX\":{hero.transform.localScale.x:F3},\"skill\":\"{skill.skillId}\"}}");
        // #endregion

        playerSkillEnergy = 0f;
        BattleUI.Instance?.UpdateSkillEnergy(0, 0f);
        TutorialDirector.Instance?.NotifyPlayerSkillUsed();
        Debug.Log($"[BattleManager] 玩家技能释放: {skill.skillName} ({skill.skillId}) → {(healTarget != null ? healTarget.name : "default")}");
        return true;
    }


    /// <summary>佣兵主动技施放入口（自动/手动共用）</summary>
    /// <remarks>
    /// 分派只看 merc_skills 的 Category / TargetType / Formula（<see cref="MercSkillExecutor"/>）。
    /// 禁止再加 <c>if (skillId == "SKxxx")</c>。
    /// </remarks>
    public bool TryCastMercActiveSkill(Mercenary merc, string skillId, bool manual)
    {
        if (merc == null || merc.isDead || string.IsNullOrEmpty(skillId)) return false;
        if (MercSkillTable.IsPassive(skillId)) return false;

        var skill = ResolveSkill(skillId);
        if (skill == null) return false;

        UnitBase healTarget = null;
        var kind = MercSkillExecutor.Resolve(skillId);
        if (kind == MercSkillExecutor.Kind.HealSingle || kind == MercSkillExecutor.Kind.HealTeam)
        {
            healTarget = FindPreferredHealTarget();
            // 全员接近满血且无人眩晕待救时，不空放治疗
            var stunMerc = healTarget as Mercenary;
            if ((stunMerc == null || !stunMerc.TutorialStunned) && !AllyBelowHpRatio(0.92f))
                return false;
            float atk = merc.attr != null ? merc.attr.GetAttr(AttrType.Attack) : 0f;
            var cfg = SkillRegistry.Instance?.Get(skillId);
            float mul = MercSkillExecutor.HealAtkMul(cfg);
            if (kind == MercSkillExecutor.Kind.HealTeam)
                ApplyHealToTeam(atk * mul, merc);
            else
                ApplyHealToUnit(healTarget ?? merc, atk * mul, merc);
            if (merc.PassiveRunner != null)
                merc.PassiveRunner.OnOwnerHealed(atk * mul);
        }
        else if (kind == MercSkillExecutor.Kind.SelfDefBuff)
        {
            if (merc.PassiveRunner != null)
                merc.PassiveRunner.ApplySelfDefBuff(MercSkillExecutor.SelfDefDuration(skillId));
        }
        else if (kind == MercSkillExecutor.Kind.TeamShield)
        {
            MercSkillExecutor.TeamShieldParams(skillId, out float ratio, out float duration);
            ApplyTeamShieldBuff(ratio, duration);
        }
        else if (kind == MercSkillExecutor.Kind.TeamHealAtk)
        {
            float atk = merc.attr != null ? merc.attr.GetAttr(AttrType.Attack) : 0f;
            ApplyHealToTeam(atk * MercSkillExecutor.TeamHealAtkMul(skillId), merc);
        }
        else if (kind == MercSkillExecutor.Kind.FearThenFallback)
        {
            UnitBase t = merc.FindNearestEnemy();
            if (t != null && merc.PassiveRunner != null)
                merc.PassiveRunner.ApplyFearDebuff(t, MercSkillExecutor.FearDuration(skillId));
            ExecuteAllySkillFallback(merc, skill);
        }
        else if (skill.skillType == SkillSystem.SkillType.Buff)
            ExecuteAllySkillFallback(merc, skill);
        else if (!(SkillSystem.Instance != null && SkillSystem.Instance.UseSkill(skill, merc)))
            ExecuteAllySkillFallback(merc, skill);

        Vector3 vfxFrom = merc.GetFirePosition();
        Vector3 vfxTo = healTarget != null
            ? healTarget.GetHitPosition()
            : (FindMercSkillVfxTarget(merc)?.GetHitPosition() ?? merc.GetHitPosition());
        Transform vfxAttach = healTarget != null ? healTarget.transform : FindMercSkillVfxTarget(merc)?.transform;
        int face = merc.GetVfxFacingDir();
        if (vfxAttach != null)
        {
            float dx = vfxTo.x - vfxFrom.x;
            if (Mathf.Abs(dx) > 0.05f) face = dx > 0f ? 1 : -1;
        }
        SkillRegistry.Instance?.PlaySkillVfx(
            skill.skillId, vfxFrom, vfxTo, true, face, vfxAttach, merc.GetBasicAttackVfxKit());

        Debug.Log($"[BattleManager] 佣兵技能释放: {merc.mercId} → {skill.skillName} ({skill.skillId}) manual={manual}");
        return true;
    }


    static UnitBase FindMercSkillVfxTarget(Mercenary merc)
    {
        if (merc == null) return null;
        var locked = merc.CurrentTarget;
        if (locked != null && !locked.isDead && locked.isAlly != merc.isAlly)
            return locked;
        return merc.FindNearestEnemy();
    }


    void ApplyTeamShieldBuff(float ratio, float duration)
    {
        var mercs = MercenaryManager.Instance?.GetActiveMercs();
        if (mercs != null)
        {
            for (int i = 0; i < mercs.Count; i++)
            {
                if (mercs[i]?.PassiveRunner != null)
                    mercs[i].PassiveRunner.ApplyTeamShieldFromActive(ratio, duration);
            }
        }
    }


    SkillSystem.ActiveSkill ResolvePlayerSkill()
    {
        string id = SkillRegistry.Instance != null
            ? SkillRegistry.Instance.GetPlayerSkillId()
            : null;
        return ResolveSkill(id);
    }


    SkillSystem.ActiveSkill ResolveMercSkill(Mercenary merc)
    {
        string id = SkillRegistry.Instance != null
            ? SkillRegistry.Instance.GetMercSkillId(merc != null ? merc.mercId : null,
                merc != null ? merc.equippedSkillId : null)
            : SkillRegistry.DefaultMercMeleeSkillId;
        return ResolveSkill(id);
    }


    // 旧签名保留注释，避免外部误调编译失败时再恢复：
    // SkillSystem.ActiveSkill ResolveMercSkill(string mercId) => ResolveSkill(
    //     SkillRegistry.Instance != null ? SkillRegistry.Instance.GetMercDefaultSkillId(mercId)
    //     : SkillRegistry.DefaultMercMeleeSkillId);

    public SkillSystem.ActiveSkill ResolveSkill(string skillId)
    {
        var fromReg = SkillRegistry.Instance?.GetActiveSkill(skillId);
        if (fromReg != null) return fromReg;
        Debug.LogError("[BattleManager] 技能配置缺失，拒绝释放: "
            + (string.IsNullOrEmpty(skillId) ? "(empty)" : skillId));
        return null;
    }


    void ExecuteAllySkillFallback(UnitBase caster, SkillSystem.ActiveSkill skill, UnitBase forcedHealTarget = null)
    {
        var cfg = SkillRegistry.Instance?.Get(skill.skillId);

        if (skill.skillType == SkillSystem.SkillType.Buff || IsHealSkill(skill))
        {
            float healBase = cfg != null && cfg.healBase > 0 ? cfg.healBase : skill.baseDamage;
            float pct = cfg != null ? cfg.healPercentOfMax : 0f;
            if (pct > 0f || healBase > 0 || IsHealSkill(skill))
            {
                UnitBase target = forcedHealTarget != null ? forcedHealTarget : FindPreferredHealTarget();
                if (target == null) target = caster;
                float maxHp = 0f;
                if (target.attr != null)
                    maxHp = target.attr.GetAttr(AttrType.MaxHp);
                float heal = pct > 0f ? maxHp * pct : healBase + caster.attr.GetAttr(AttrType.Attack) * 0.5f;
                if (pct <= 0f && IsHealSkill(skill))
                    heal = maxHp * 0.3f;
                ApplyHealToUnit(target, heal, caster);
                return;
            }

            if (cfg != null && cfg.buffValue > 0)
            {
                ApplyTeamBuff(cfg);
                return;
            }
        }

        float damage = caster.attr.GetAttr(AttrType.Attack) * Mathf.Max(1f, skill.damageMultiplier);
        int vfxDir = caster.GetVfxFacingDir();
        if (skill.skillType == SkillSystem.SkillType.SingleTarget)
        {
            UnitBase t = caster is Mercenary m ? m.FindNearestEnemy() : FindNearestMonster();
            if (t != null) t.TakeDamage(damage, false, false, true, vfxDir, caster);
        }
        else
        {
            for (int i = monsters.Count - 1; i >= 0; i--)
            {
                if (monsters[i] == null || monsters[i].isDead) continue;
                float dist = Mathf.Abs(monsters[i].transform.position.x - caster.transform.position.x);
                if (skill.aoeRadius > 0 && dist > skill.aoeRadius) continue;
                monsters[i].TakeDamage(damage, false, false, true, vfxDir, caster);
            }
        }
    }


    void ApplyHealToUnit(UnitBase unit, float heal, UnitBase healer = null)
    {
        if (unit == null || unit.isDead || unit.attr == null) return;
        float before = unit.currentHp;
        float maxHp = unit.attr.GetAttr(AttrType.MaxHp);
        unit.currentHp = Mathf.Min(maxHp, unit.currentHp + heal);
        int gained = Mathf.RoundToInt(unit.currentHp - before);
        if (gained > 0)
        {
            RunStats.HealingReceived += gained;
            if (healer != null)
                RecordAllyHeal(healer, gained);
            DamageTextSystem.Instance?.SpawnHealText(unit.GetHitPosition(), gained);
        }
    }


    void ApplyHealToTeam(float heal, UnitBase healer = null)
    {
        ApplyHealToUnit(hero, heal, healer);
        var mercs = MercenaryManager.Instance?.GetActiveMercs();
        if (mercs == null) return;
        foreach (var m in mercs)
            ApplyHealToUnit(m, heal * 0.8f, healer);
    }


    void ApplyTeamBuff(SkillConfig cfg)
    {
        void Apply(UnitBase u)
        {
            if (u == null || u.isDead || u.attr == null) return;
            u.attr.AddAttr(cfg.buffAttr, cfg.buffValue, cfg.buffIsPercent);
        }
        Apply(hero);
        var mercs = MercenaryManager.Instance?.GetActiveMercs();
        if (mercs != null)
            foreach (var m in mercs) Apply(m);
    }


    static bool IsHealSkill(SkillSystem.ActiveSkill skill)
    {
        if (skill == null) return false;
        if (skill.skillId != null && skill.skillId.IndexOf("heal", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        if (MercSkillTable.IsHealActiveId(skill.skillId))
            return true;
        var cfg = SkillRegistry.Instance?.Get(skill.skillId);
        return cfg != null && (cfg.healPercentOfMax > 0f || cfg.healBase > 0f);
    }


    /// <summary>引导中优先救眩晕牧师；否则血量比例最低的队友。</summary>
    UnitBase FindPreferredHealTarget()
    {
        if (allyUnits != null)
        {
            for (int i = 0; i < allyUnits.Count; i++)
            {
                if (allyUnits[i] is Mercenary merc && merc.TutorialStunned && !merc.isDead)
                    return merc;
            }
        }
        return FindLowestHpAlly() ?? hero;
    }


    bool AllyBelowHpRatio(float ratio)
    {
        bool Check(UnitBase u)
        {
            if (u == null || u.isDead || u.attr == null) return false;
            float maxHp = u.attr.GetAttr(AttrType.MaxHp);
            return maxHp > 1f && u.currentHp / maxHp < ratio;
        }
        if (Check(hero)) return true;
        if (allyUnits != null)
        {
            for (int i = 0; i < allyUnits.Count; i++)
                if (Check(allyUnits[i])) return true;
        }
        var mercs = MercenaryManager.Instance?.GetActiveMercs();
        if (mercs != null)
        {
            for (int i = 0; i < mercs.Count; i++)
                if (Check(mercs[i])) return true;
        }
        return false;
    }


    UnitBase FindLowestHpAlly()
    {
        UnitBase best = null;
        float worstRatio = 2f;

        void Consider(UnitBase u)
        {
            if (u == null || u.isDead || u.attr == null) return;
            float maxHp = u.attr.GetAttr(AttrType.MaxHp);
            if (maxHp <= 1f) return;
            float ratio = u.currentHp / maxHp;
            if (ratio < worstRatio)
            {
                worstRatio = ratio;
                best = u;
            }
        }

        Consider(hero);
        if (allyUnits != null)
        {
            for (int i = 0; i < allyUnits.Count; i++)
                Consider(allyUnits[i]);
        }
        var mercs = MercenaryManager.Instance?.GetActiveMercs();
        if (mercs != null)
        {
            for (int i = 0; i < mercs.Count; i++)
                Consider(mercs[i]);
        }
        return best;
    }


    UnitBase FindNearestMonster()
    {
        UnitBase nearest = null;
        float best = float.MaxValue;
        for (int i = 0; i < monsters.Count; i++)
        {
            if (monsters[i] == null || monsters[i].isDead) continue;
            float d = Mathf.Abs(monsters[i].transform.position.x - hero.transform.position.x);
            if (d < best) { best = d; nearest = monsters[i]; }
        }
        return nearest;
    }

}
