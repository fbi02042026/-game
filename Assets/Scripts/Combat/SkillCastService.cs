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

    void RecordAllyHeal(UnitBase healer, float amount) => bm.RecordAllyHeal(healer, amount);


    // ============================================================
    // 玩家技能释放（由头像点击触发）
    // ============================================================

    /// <summary>
    /// 释放玩家技能。V6：技能能量改为<b>每槽一条</b>，所以要先找出「能量满且不在冷却」的槽，
    /// 再只释放那一个、只清那一条能量。
    /// </summary>
    public bool TryUsePlayerSkill()
    {
        return TryUsePlayerSkillSlot(bm.FindReadySkillSlot());
    }

    /// <summary>释放指定技能槽的技能（slot 来自 BattleManager.FindReadySkillSlot）。</summary>
    public bool TryUsePlayerSkillSlot(int slot)
    {
        if (slot < 0) return false;
        // 纯冷却制下不再看能量（PLAYER_SKILL_USE_ENERGY 置 true 可退回原行为）
        if (GameConfig.PLAYER_SKILL_USE_ENERGY && bm.GetPlayerSkillEnergy(slot) < 0.99f) return false;
        // 权威闸：早退闸只在 PlayerSkillPassive 里，任何别的调用方都必须在这里被挡住，
        // 保证任意两次玩家技能释放至少隔 GameConfig.PLAYER_SKILL_GCD 秒。
        if (!bm.IsPlayerSkillGcdReady) return false;
        if (hero == null || hero.isDead) return false;

        // 严格按槽取技能：取不到就本次不放（不放别的技能替补，避免清错能量）
        var skill = ResolvePlayerSkillAt(slot);
        if (skill == null) return false;
        UnitBase healTarget = null;
        bool isHeal = IsHealSkill(skill);
        bool isBuff = skill.skillType == SkillSystem.SkillType.Buff;

        if (isHeal)
        {
            healTarget = FindPreferredHealTarget();
            ExecuteAllySkillFallback(hero, skill, healTarget);
        }
        else
        {
            bool ok = !isBuff
                && SkillSystem.Instance != null
                && SkillSystem.Instance.UseSkill(skill, hero);
            if (!ok)
                ExecuteAllySkillFallback(hero, skill);
        }

        // Buff / 治疗类不走 SkillSystem.UseSkill，冷却在此补齐，避免随能量反复刷屏
        if ((isHeal || isBuff) && SkillSystem.Instance != null
            && !SkillSystem.Instance.IsOnCooldown(skill.skillId))
            SkillSystem.Instance.RegisterCooldown(skill.skillId, skill.cooldown);

        Vector3 vfxPos = healTarget != null ? healTarget.GetHitPosition() : hero.GetHitPosition();
        Transform vfxAttach = healTarget != null ? healTarget.transform : hero.transform;
        SkillRegistry.Instance?.PlaySkillVfx(skill.skillId, vfxPos, true, hero.GetVfxFacingDir(), vfxAttach);

        // #region agent log
        DebugAgentLog.Log("H5", "BattleManager.TryUsePlayerSkill", "skill_vfx_facing",
            $"{{\"facingDir\":{hero.facingDir},\"vfxDir\":{hero.GetVfxFacingDir()},\"scaleX\":{hero.transform.localScale.x:F3},\"skill\":\"{skill.skillId}\"}}");
        // #endregion

        // V6：只清这一个技能槽的能量，其它槽照保留（纯冷却制下这步不生效）
        if (GameConfig.PLAYER_SKILL_USE_ENERGY)
        {
            bm.SetPlayerSkillEnergy(slot, 0f);
            BattleUI.Instance?.UpdateSkillEnergy(0, bm.PlayerSkillEnergyPeak);
        }
        // 上膛：接下来 PLAYER_SKILL_GCD 秒内不再放下一个技能
        bm.ArmPlayerSkillGcd();
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
        // 眩晕/被控期间禁止施放主动技（含引导「原地眩晕」的小白：TutorialStunned）
        if (merc.IsStunned || merc.TutorialStunned) return false;
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


    /// <summary>
    /// 取指定技能槽的运行时技能（V6）。索引与 RunLoadout.SkillIds() 一一对应，
    /// 由 <see cref="RunDraftDirector"/> 重建时按序灌入 SkillSystem。
    /// </summary>
    SkillSystem.ActiveSkill ResolvePlayerSkillAt(int slot)
    {
        var sys = SkillSystem.Instance;
        if (sys == null || slot < 0) return null;
        var list = sys.GetPlayerSkills();
        if (list == null || slot >= list.Count) return null;
        return list[slot];
    }

    SkillSystem.ActiveSkill ResolvePlayerSkill()
    {
        // 局内构筑：优先取第一个不在冷却中的技能；全部冷却中则返回 null（本次不放，能量不消耗）
        var sys = SkillSystem.Instance;
        if (sys != null && sys.GetPlayerSkills().Count > 0)
            return sys.GetReadyPlayerSkill();

        // 回退：未灌入本局构筑时走旧的单技能路径
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
            // 优先用本局构筑算好的数值（含星级/流派/亲和），取不到才回退 SkillConfig 底稿。
            // 2026-09-15：治疗与增益改吃星级后，必须读 ActiveSkill，否则星级乘数无效。
            float healBase = skill.healBase > 0f
                ? skill.healBase
                : (cfg != null ? cfg.healBase : 0f);
            float pct = skill.healPercentOfMax > 0f
                ? skill.healPercentOfMax
                : (cfg != null ? cfg.healPercentOfMax : 0f);
            float atkMul = skill.healAtkMul > 0f
                ? skill.healAtkMul
                : (cfg != null ? cfg.healAtkMul : 0f);

            if (pct > 0f || healBase > 0f || atkMul > 0f || IsHealSkill(skill))
            {
                UnitBase target = forcedHealTarget != null ? forcedHealTarget : FindPreferredHealTarget();
                if (target == null) target = caster;
                float maxHp = 0f;
                if (target.attr != null)
                    maxHp = target.attr.GetAttr(AttrType.MaxHp);
                float casterAtk = caster.attr != null ? caster.attr.GetAttr(AttrType.Attack) : 0f;
                float heal = maxHp * pct + casterAtk * atkMul + healBase;
                // 三项全为 0 又被判定为治疗技（历史数据），给个保底 30% 最大生命
                if (heal <= 0f && IsHealSkill(skill))
                    heal = maxHp * 0.3f;
                ApplyHealToUnit(target, heal, caster);
                return;
            }

            if (cfg != null && cfg.buffValue > 0)
            {
                ApplyTeamBuff(skill, cfg);
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


    // 团队攻速 buff 的计时状态。attr.AddAttr 是永久写值，技能表里的 duration 从未被消费，
    // 导致 gale_stance 放一次 +35% 攻速永久生效（现存 bug）。攻速改走这里的到期时间：
    // 由 UnitBase.GetAttackCooldown 按倍率消耗，到期自动失效；残影也用同一标志判断「攻速技能是否生效中」。
    static float _teamAtkSpdBuffUntil = -1f;
    static float _teamAtkSpdMul = 1f;

    /// <summary>攻速增益技能是否处于生效期（残影与攻速计算共用的唯一判据）。</summary>
    public static bool IsTeamAttackSpeedBuffActive => Time.time < _teamAtkSpdBuffUntil;

    /// <summary>攻速增益倍率，过期返回 1（无增益）。</summary>
    public static float GetTeamAttackSpeedMul() => IsTeamAttackSpeedBuffActive ? _teamAtkSpdMul : 1f;

    void ApplyTeamBuff(SkillSystem.ActiveSkill skill, SkillConfig cfg)
    {
        // 数值以本局构筑为准（含星级/流派/亲和），SkillConfig 只作缺省兜底。
        float buffValue = skill != null && skill.buffValue > 0f ? skill.buffValue : (cfg != null ? cfg.buffValue : 0f);
        bool isPercent = skill != null ? skill.buffIsPercent : (cfg != null && cfg.buffIsPercent);
        float duration = skill != null && skill.duration > 0f ? skill.duration : (cfg != null ? cfg.duration : 0f);
        var buffAttr = skill != null ? skill.buffAttr : (cfg != null ? cfg.buffAttr : AttrType.Attack);

        // 攻速类增益不再走 AddAttr（无法回收），改为计时倍率，duration 才真正生效。
        if (buffAttr == AttrType.AttackSpeed && isPercent && duration > 0f)
        {
            _teamAtkSpdMul = 1f + Mathf.Max(0f, buffValue);
            _teamAtkSpdBuffUntil = Time.time + duration;
            GamePerf.Log($"[SkillCast] 团队攻速增益 {_teamAtkSpdMul:0.##}x，持续 {duration}s");
            return;
        }

        // 攻击 / 防御 / 暴击：走 AttrSystem 的定时增益层（到期自动失效、后放覆盖先放永不叠加）。
        // 以前这里用 attr.AddAttr 永久写值，表里 duration 完全没被消费 —— 纯冷却制下会无限叠加。
        if ((buffAttr == AttrType.Attack || buffAttr == AttrType.Defense
             || buffAttr == AttrType.CritRate) && duration > 0f)
        {
            void ApplyTimed(UnitBase u)
            {
                if (u == null || u.isDead || u.attr == null) return;
                u.attr.ApplyTimedBuff(buffAttr, buffValue, isPercent, duration);
            }
            ApplyTimed(hero);
            var team = MercenaryManager.Instance?.GetActiveMercs();
            if (team != null)
                foreach (var m in team) ApplyTimed(m);
            GamePerf.Log($"[SkillCast] 团队增益 {buffAttr} ×{(1f + buffValue):0.##}，持续 {duration}s");
            return;
        }

        void Apply(UnitBase u)
        {
            if (u == null || u.isDead || u.attr == null) return;
            u.attr.AddAttr(buffAttr, buffValue, isPercent);
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
