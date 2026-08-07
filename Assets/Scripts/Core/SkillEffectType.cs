/// <summary>
/// 战斗特效约定（两层）：
/// 1) 共用套装 AttackVfxKit —— 普攻怎么播（我方/敌方分观感）
/// 2) 独立技能 —— 特殊技逻辑 + 可选专属特效
///
/// 共用套：
///   MeleeSlash  近战刀光
///   Bow         弓箭（飞行）
///   Orb         法球（飞行）
///   Heal        加血特效（可选）；治疗数字走 DamageTextSystem
///
/// 暴击：不用特效套，用飘字（颜色/大小/速度）区分。
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
/// 技能/特效命名与推断
/// 技能配置 id：
///   Ally：ally_heal / ally_atk_speed / ally_crit_up / ally_thunder / ally_shield（玩家+佣兵共用）
///   Monster：mon_slam_multi / mon_magic_burst
/// 特效套装：按 AttackVfxKit，不按职业拆文件夹
/// </summary>
public static class SkillNaming
{
    public static AttackVfxKit KitFromAttackType(WeaponAttackType attackType, float attackRange = 1.5f)
    {
        if (attackType == WeaponAttackType.Magic)
            return AttackVfxKit.Orb;
        // 物理但射程明显偏远 → 当弓箭飞弹
        if (attackType == WeaponAttackType.Physical && attackRange >= GameConfig.RangeBow - 0.05f)
            return AttackVfxKit.Bow;
        if (attackType == WeaponAttackType.Hybrid)
            return AttackVfxKit.Orb; // 混合默认飞法球；近战混合可由单位自行指定 Melee
        return AttackVfxKit.MeleeSlash;
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
}
