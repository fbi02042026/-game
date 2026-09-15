using System.Collections.Generic;

/// <summary>
/// 玩家技能的「局内抽卡元数据」：稀有度 / 流派 / 职业亲和。
/// 刻意不放进 player_skills CSV，避免 Cook 流程与旧存档兼容负担；
/// 表内显式登记已知技能，未登记的走启发式兜底（保证加技能不会漏抽卡权重）。
/// </summary>
public static class SkillDraftMeta
{
    public struct Meta
    {
        public SkillRarity Rarity;
        public SynergyTag Tag;
        /// <summary>亲近职业（PlayerJobId 的整数值）；-1 = 通用，任何职业都能吃满亲和加成。</summary>
        public int JobAffinity;
        /// <summary>是否进入局内抽卡池。</summary>
        public bool InDraftPool;
    }

    /// <summary>职业亲和加成：伤害 ×1.2，冷却 ×0.85。</summary>
    public const float AffinityDamageMul = 1.2f;
    public const float AffinityCooldownMul = 0.85f;

    static readonly Dictionary<string, Meta> Table = new Dictionary<string, Meta>
    {
        // —— 原始 6 技能 ——
        { "heal_spring",     M(SkillRarity.Common,    SynergyTag.Guard,   (int)PlayerJobId.Priest) },
        { "holy_barrier",    M(SkillRarity.Rare,      SynergyTag.Guard,   (int)PlayerJobId.SwordShield) },
        { "battle_surge",    M(SkillRarity.Rare,      SynergyTag.Combo,   (int)PlayerJobId.Berserker) },
        { "gale_stance",     M(SkillRarity.Rare,      SynergyTag.Combo,   (int)PlayerJobId.Heavy) },
        { "deadly_focus",    M(SkillRarity.Common,    SynergyTag.Combo,   (int)PlayerJobId.Ranger) },
        { "thunder_verdict", M(SkillRarity.Epic,      SynergyTag.Thunder, (int)PlayerJobId.Mage) },

        // —— 局内扩充池（VFX 走 attackKit 共用套，无 allyConfigId 依赖）——
        { "flame_burst",     M(SkillRarity.Epic,      SynergyTag.Fire,    (int)PlayerJobId.Mage) },
        { "thunder_chain",   M(SkillRarity.Rare,      SynergyTag.Thunder, (int)PlayerJobId.Mage) },
        { "wolf_volley",     M(SkillRarity.Rare,      SynergyTag.Summon,  (int)PlayerJobId.Ranger) },
        { "war_banner",      M(SkillRarity.Rare,      SynergyTag.Combo,   (int)PlayerJobId.Berserker) },
        { "iron_wall",       M(SkillRarity.Common,    SynergyTag.Guard,   (int)PlayerJobId.SwordShield) },
        { "frost_nova",      M(SkillRarity.Legendary, SynergyTag.Thunder, (int)PlayerJobId.Mage) },
        { "blood_harvest",   M(SkillRarity.Legendary, SynergyTag.Fire,    (int)PlayerJobId.Berserker) },

        // —— 2026-09-15 第二轮扩充（13 → 24）——
        // 治疗线：牧师原来只有 1 个技能，补短 CD 续航 + 长 CD 大治疗
        { "swift_mend",      M(SkillRarity.Common,    SynergyTag.Guard,   (int)PlayerJobId.Priest) },
        { "sacred_revival",  M(SkillRarity.Epic,      SynergyTag.Guard,   (int)PlayerJobId.Priest) },
        // 守护线：补短 CD 常驻减伤 + 传说级长减伤
        { "stone_skin",      M(SkillRarity.Common,    SynergyTag.Guard,   (int)PlayerJobId.SwordShield) },
        { "aegis_oath",      M(SkillRarity.Legendary, SynergyTag.Guard,   (int)PlayerJobId.SwordShield) },
        // 重甲近战线：原来只有 gale_stance 一个
        { "bull_rush",       M(SkillRarity.Common,    SynergyTag.Combo,   (int)PlayerJobId.Heavy) },
        { "quake_slam",      M(SkillRarity.Rare,      SynergyTag.Combo,   (int)PlayerJobId.Heavy) },
        // 游侠线：暴击增益 + 召唤流补到 3 个
        { "hawk_eye",        M(SkillRarity.Common,    SynergyTag.Combo,   (int)PlayerJobId.Ranger) },
        { "arrow_storm",     M(SkillRarity.Epic,      SynergyTag.Summon,  (int)PlayerJobId.Ranger) },
        { "spirit_wolf",     M(SkillRarity.Common,    SynergyTag.Summon,  (int)PlayerJobId.Ranger) },
        // 剑盾输出 + 火系高伤
        { "blade_storm",     M(SkillRarity.Rare,      SynergyTag.Combo,   (int)PlayerJobId.SwordShield) },
        { "arcane_flame",    M(SkillRarity.Epic,      SynergyTag.Fire,    (int)PlayerJobId.Mage) },
    };

    static Meta M(SkillRarity r, SynergyTag tag, int jobAffinity)
    {
        return new Meta { Rarity = r, Tag = tag, JobAffinity = jobAffinity, InDraftPool = true };
    }

    /// <summary>未知 id 的兜底元数据（仍在抽卡池内，权重按稀有）。</summary>
    static readonly Meta FallbackMeta = new Meta
    {
        Rarity = SkillRarity.Rare,
        Tag = SynergyTag.None,
        JobAffinity = -1,
        InDraftPool = true
    };

    public static Meta Get(string skillId)
    {
        if (!string.IsNullOrEmpty(skillId) && Table.TryGetValue(skillId, out var m))
            return m;
        return FallbackMeta;
    }

    public static SkillRarity Rarity(string skillId) => Get(skillId).Rarity;
    public static SynergyTag Tag(string skillId) => Get(skillId).Tag;
    public static bool InDraftPool(string skillId) => Get(skillId).InDraftPool;

    /// <summary>当前所选职业是否吃满该技能的亲和加成。</summary>
    public static bool IsAffinity(string skillId, PlayerJobId job)
    {
        int affinity = Get(skillId).JobAffinity;
        return affinity < 0 || affinity == (int)job;
    }

    /// <summary>每星伤害增幅：线性 +18%/星（1 星 = 1.0）。
    /// 2026-09-15 由 +28% 下调：技能从能量制改纯 CD 制后释放频率大增，原曲线会滚雪球。</summary>
    public const float StarDamageStep = 0.18f;

    /// <summary>每星冷却缩减：线性 -6%/星（1 星 = 1.0，越高越短）。</summary>
    public const float StarCooldownStep = 0.06f;

    /// <summary>每星增益幅度：线性 +18%/星，与伤害同曲线（1 星 = 1.0）。
    /// 2026-09-15 起治疗量与增益幅度也吃星级，不再只涨冷却。</summary>
    public const float StarBuffStep = 0.18f;

    /// <summary>每星增益持续：线性 +6%/星（1 星 = 1.0），与冷却缩减对称，上限 2.0 倍。</summary>
    public const float StarDurationStep = 0.06f;

    /// <summary>星级 → 伤害倍率（1 星 = 1.0）。</summary>
    public static float StarDamageMul(int star)
    {
        int s = star < 1 ? 1 : star;
        return 1f + (s - 1) * StarDamageStep;
    }

    /// <summary>星级 → 冷却倍率（1 星 = 1.0，越高越短）。</summary>
    public static float StarCooldownMul(int star)
    {
        int s = star < 1 ? 1 : star;
        float mul = 1f - (s - 1) * StarCooldownStep;
        return mul < 0.6f ? 0.6f : mul;
    }

    /// <summary>星级 → 治疗量 / 增益幅度倍率（1 星 = 1.0）。与伤害同曲线。</summary>
    public static float StarBuffMul(int star)
    {
        int s = star < 1 ? 1 : star;
        return 1f + (s - 1) * StarBuffStep;
    }

    /// <summary>星级 → 增益持续秒数倍率（1 星 = 1.0，越高越久，封顶 2.0）。</summary>
    public static float StarDurationMul(int star)
    {
        int s = star < 1 ? 1 : star;
        float mul = 1f + (s - 1) * StarDurationStep;
        return mul > 2f ? 2f : mul;
    }
}
