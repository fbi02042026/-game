using UnityEngine;

/// <summary>
/// 关卡掉落稀有度表 + 稀有度基础属性系数（对齐《装备系统核心规则》）。
/// </summary>
public static class EquipDropRules
{
    public static Rarity RollRarity(StageType stageType, Rarity maxRarity)
    {
        float r = Random.value;
        Rarity rolled;
        switch (stageType)
        {
            case StageType.Boss:
                // 普通30 / 稀有45 / 史诗20 / 传奇5 → 映射到工程枚举
                if (r < 0.05f) rolled = Rarity.Legendary;
                else if (r < 0.25f) rolled = Rarity.Epic;
                else if (r < 0.70f) rolled = Rarity.Rare;
                else if (r < 0.85f) rolled = Rarity.Uncommon;
                else rolled = Rarity.Common;
                break;
            case StageType.Elite:
                if (r < 0.01f) rolled = Rarity.Legendary;
                else if (r < 0.10f) rolled = Rarity.Epic;
                else if (r < 0.50f) rolled = Rarity.Rare;
                else if (r < 0.75f) rolled = Rarity.Uncommon;
                else rolled = Rarity.Common;
                break;
            default: // Normal / 其它 → 裂缝宝箱偏高一档用 Elite 近似；普通关
                if (r < 0.02f) rolled = Rarity.Epic;
                else if (r < 0.30f) rolled = Rarity.Rare;
                else if (r < 0.55f) rolled = Rarity.Uncommon;
                else rolled = Rarity.Common;
                break;
        }

        if (rolled > maxRarity) rolled = maxRarity;
        if (rolled < Rarity.Common) rolled = Rarity.Common;
        return rolled;
    }

    /// <summary>稀有度基础属性系数（取设计区间中值）。</summary>
    public static float RarityBaseMul(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Legendary: return 1.65f;
            case Rarity.Epic: return 1.32f;
            case Rarity.Rare: return 1.15f;
            case Rarity.Uncommon: return 1.08f;
            default: return 1f;
        }
    }
}
