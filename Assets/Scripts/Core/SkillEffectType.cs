/// <summary>
/// 战斗特效约定（两层）：
/// 1) 共用套装 AttackVfxKit —— 普攻怎么播（我方/敌方分观感）
/// 2) 独立技能 —— 优先专属预制体；否则用 SkillConfig.attackKit 回退共用套
///
/// 加新技能必填其一：
///   A) Resources/VFX/Skills/{Ally|Monster|Merc}/{skillId}.prefab（或 SkillConfig.vfxPrefab）
///   B) SkillConfig.attackKit ≠ None（回退共用套：刀光/弓/法球/治疗）
///
/// 共用套路径：Resources/VFX/Shared/{Ally|Enemy}/{MeleeSlash|Bow|Orb|Heal}/vfx_*
/// 暴击：不用特效套，用飘字区分。
/// </summary>
public enum AttackVfxKit
{
    MeleeSlash,
    Bow,
    Orb,
    Heal,
    None
}

public enum VfxFaction
{
    Ally,
    Enemy
}

/// <summary>
/// 技能效果类型 - 特殊技/扩展用（独立技能可引用）
/// </summary>
public enum SkillEffectType
{
    Slash,
    Critical,
    MagicImpact,
    Fireball,
    IceImpact,
    Lightning,
    Heal,
    Shield,
    ExplosionBig,
    ExplosionSmall,
    LevelUp,
}

/// <summary>
/// 技能/特效命名与推断。
/// 技能配置 id：
///   Ally：ally_heal / ally_atk_up / ally_atk_speed / ally_crit_up / ally_thunder / ally_shield
///   Monster：mon_slam_multi / mon_magic_burst
/// </summary>
public static class SkillNaming
{
    /// <summary>
    /// 玩家主手武器 → 共用特效套。近战一律 MeleeSlash，弓 Bow，法杖 Orb。
    /// 仅用于普攻，不要拿来盖掉技能自己的 attackKit。
    /// </summary>
    public static AttackVfxKit KitFromWeaponKind(WeaponCombatTable.WeaponKind kind)
    {
        switch (kind)
        {
            case WeaponCombatTable.WeaponKind.Bow:
                return AttackVfxKit.Bow;
            case WeaponCombatTable.WeaponKind.Staff:
                return AttackVfxKit.Orb;
            default:
                return AttackVfxKit.MeleeSlash;
        }
    }

    public static AttackVfxKit KitFromAttackType(WeaponAttackType attackType, float attackRange = 1.5f)
    {
        if (attackType == WeaponAttackType.Magic)
            return AttackVfxKit.Orb;
        if (attackType == WeaponAttackType.Physical && attackRange >= GameConfig.RangeBow - 0.05f)
            return AttackVfxKit.Bow;
        if (attackType == WeaponAttackType.Hybrid)
            return AttackVfxKit.Orb;
        return AttackVfxKit.MeleeSlash;
    }

    /// <summary>
    /// 技能特效套装解析（唯一入口）。优先级：
    /// 1. Heal（按 attackKit / skillType / id）
    /// 2. SkillConfig.attackKit（非 None）
    /// 3. skillType=Projectile → Orb
    /// 4. id 关键字兜底（thunder/magic/orb→Orb，bow/arrow→Bow）
    /// 5. MeleeSlash
    /// 禁止：用玩家武器套盖掉技能配置。
    /// </summary>
    public static AttackVfxKit ResolveSkillVfxKit(SkillConfig cfg, string skillId = null)
    {
        string id = !string.IsNullOrEmpty(skillId) ? skillId
            : (cfg != null ? cfg.id : null);

        if (IsHealSkill(cfg, id))
            return AttackVfxKit.Heal;

        if (cfg != null && cfg.attackKit != AttackVfxKit.None)
            return cfg.attackKit;

        if (cfg != null && cfg.skillType == SkillSystem.SkillType.Projectile)
            return AttackVfxKit.Orb;

        if (!string.IsNullOrEmpty(id))
        {
            string low = id.ToLowerInvariant();
            if (low.Contains("thunder") || low.Contains("magic") || low.Contains("orb") || low.Contains("fire"))
                return AttackVfxKit.Orb;
            if (low.Contains("bow") || low.Contains("arrow"))
                return AttackVfxKit.Bow;
            if (low.Contains("heal") || low.Contains("shield") || low.Contains("buff"))
                return AttackVfxKit.Heal;
        }

        return AttackVfxKit.MeleeSlash;
    }

    public static bool IsHealSkill(SkillConfig cfg, string skillId = null)
    {
        if (cfg != null)
        {
            if (cfg.attackKit == AttackVfxKit.Heal) return true;
            if (cfg.skillType == SkillSystem.SkillType.Buff && (cfg.healBase > 0f || cfg.healPercentOfMax > 0f))
                return true;
        }
        string id = !string.IsNullOrEmpty(skillId) ? skillId : cfg?.id;
        return !string.IsNullOrEmpty(id)
            && id.IndexOf("heal", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>弹道技能用的飞行套：Bow / Orb；其它回退 Orb。</summary>
    public static AttackVfxKit ResolveProjectileKit(SkillConfig cfg, string skillId = null)
    {
        AttackVfxKit kit = ResolveSkillVfxKit(cfg, skillId);
        if (kit == AttackVfxKit.Bow || kit == AttackVfxKit.Orb)
            return kit;
        return AttackVfxKit.Orb;
    }

    public static SkillEffectType GetDefaultAttackEffect(WeaponAttackType attackType)
    {
        switch (attackType)
        {
            case WeaponAttackType.Magic: return SkillEffectType.MagicImpact;
            case WeaponAttackType.Hybrid: return SkillEffectType.Slash;
            default: return SkillEffectType.Slash;
        }
    }

    public static string GetKitName(AttackVfxKit kit)
    {
        switch (kit)
        {
            case AttackVfxKit.MeleeSlash: return "近战刀光";
            case AttackVfxKit.Bow: return "弓箭";
            case AttackVfxKit.Orb: return "法球";
            case AttackVfxKit.Heal: return "治疗";
            default: return kit.ToString();
        }
    }

    public static string SharedKitResourceHint(AttackVfxKit kit, VfxFaction faction)
    {
        string side = faction == VfxFaction.Enemy ? "Enemy" : "Ally";
        switch (kit)
        {
            case AttackVfxKit.MeleeSlash:
                return $"VFX/Shared/{side}/MeleeSlash/vfx_melee_hit";
            case AttackVfxKit.Bow:
                return $"VFX/Shared/{side}/Bow/vfx_bow_fly + vfx_bow_hit";
            case AttackVfxKit.Orb:
                return $"VFX/Shared/{side}/Orb/vfx_orb_fly + vfx_orb_hit";
            case AttackVfxKit.Heal:
                return $"VFX/Shared/{side}/Heal/vfx_heal";
            default:
                return $"VFX/Shared/{side}/…";
        }
    }
}
