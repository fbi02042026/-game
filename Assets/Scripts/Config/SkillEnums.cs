using UnityEngine;

/// <summary>
/// 技能稀有度（局内抽卡权重用）。
/// 注意：与装备全局 <c>Rarity</c>、佣兵 <see cref="MercRosterDefs.MercRarity"/> 取值不同，勿混用、勿互转。
/// </summary>
public enum SkillRarity
{
    Common = 0,
    Rare = 1,
    Epic = 2,
    Legendary = 3
}

/// <summary>
/// 流派标签（Phase 2「流派势能」累积用；Phase 1 仅用于展示与职业亲和提示）。
/// </summary>
public enum SynergyTag
{
    None = 0,
    Fire = 1,      // 烈焰
    Thunder = 2,   // 雷霆
    Summon = 3,    // 召唤
    Combo = 4,     // 连击
    Guard = 5      // 守护
}

public static class SkillRarityUtil
{
    /// <summary>抽卡权重（越大越常见）。</summary>
    public static int Weight(SkillRarity r)
    {
        switch (r)
        {
            case SkillRarity.Legendary: return 1;
            case SkillRarity.Epic: return 3;
            case SkillRarity.Rare: return 6;
            default: return 10;
        }
    }

    /// <summary>该稀有度技能可升到的最高星级。</summary>
    public static int StarCap(SkillRarity r)
    {
        switch (r)
        {
            case SkillRarity.Legendary: return 5;
            case SkillRarity.Epic: return 4;
            default: return 3;
        }
    }

    public static string DisplayName(SkillRarity r)
    {
        switch (r)
        {
            case SkillRarity.Legendary: return "传说";
            case SkillRarity.Epic: return "史诗";
            case SkillRarity.Rare: return "稀有";
            default: return "普通";
        }
    }

    /// <summary>稀有度色调（深色底上可读）。</summary>
    public static Color Tint(SkillRarity r)
    {
        switch (r)
        {
            case SkillRarity.Legendary: return new Color(1f, 0.72f, 0.20f);
            case SkillRarity.Epic: return new Color(0.72f, 0.42f, 0.95f);
            case SkillRarity.Rare: return new Color(0.35f, 0.68f, 0.98f);
            default: return new Color(0.80f, 0.82f, 0.86f);
        }
    }
}

public static class SynergyTagUtil
{
    public static string DisplayName(SynergyTag t)
    {
        switch (t)
        {
            case SynergyTag.Fire: return "烈焰";
            case SynergyTag.Thunder: return "雷霆";
            case SynergyTag.Summon: return "召唤";
            case SynergyTag.Combo: return "连击";
            case SynergyTag.Guard: return "守护";
            default: return "";
        }
    }

    public static Color Tint(SynergyTag t)
    {
        switch (t)
        {
            case SynergyTag.Fire: return new Color(0.98f, 0.45f, 0.24f);
            case SynergyTag.Thunder: return new Color(0.66f, 0.48f, 0.98f);
            case SynergyTag.Summon: return new Color(0.40f, 0.82f, 0.52f);
            case SynergyTag.Combo: return new Color(0.98f, 0.78f, 0.28f);
            case SynergyTag.Guard: return new Color(0.42f, 0.72f, 0.94f);
            default: return new Color(0.75f, 0.75f, 0.78f);
        }
    }
}

/// <summary>局内抽卡的卡牌类型。</summary>
public enum DraftCardKind
{
    SkillNew = 0,      // 获得新技能
    SkillUp = 1,       // 已有技能升星
    MercRecruit = 2,   // 招募佣兵
    PowerUp = 3,       // 直接强化（攻击 / 生命 / 攻速）
    Equip = 4,         // 本局装备（撤离不带出）
    MercLevelUp = 5,   // 已有佣兵升级（佣兵不自动升级，只能抽卡升）
    MercStarUp = 6     // 已有佣兵升星（★+1，同时等级 +1）
}

/// <summary>
/// 升级抽卡的第一层「方向标签」。玩家先定方向、再看该方向的三选一，
/// 让「搭配」是主动选择而不是纯运气；只有 1 个方向可选时自动跳过本层。
/// </summary>
public enum DraftCategory
{
    Skill = 0,   // 技能：新技能 / 升星
    Merc = 1,    // 佣兵：招募 / 升级 / 升星
    Equip = 2    // 装备：本局掉落三选一（不带出）
}

public static class DraftCategoryUtil
{
    public static string DisplayName(DraftCategory c)
    {
        switch (c)
        {
            case DraftCategory.Merc: return "佣兵";
            case DraftCategory.Equip: return "装备";
            default: return "技能";
        }
    }

    /// <summary>标签上的大字图标（无美术资源时的占位）。</summary>
    public static string Glyph(DraftCategory c)
    {
        switch (c)
        {
            case DraftCategory.Merc: return "兵";
            case DraftCategory.Equip: return "装";
            default: return "技";
        }
    }

    public static Color Tint(DraftCategory c)
    {
        switch (c)
        {
            case DraftCategory.Merc: return new Color(0.42f, 0.72f, 0.94f);
            case DraftCategory.Equip: return new Color(0.98f, 0.72f, 0.30f);
            default: return new Color(0.72f, 0.52f, 0.98f);
        }
    }
}
