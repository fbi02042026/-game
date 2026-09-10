using UnityEngine;

/// <summary>佣兵稀有度颜色（图鉴 / 战斗头顶名牌等共用）。</summary>
public static class MercRarityColors
{
    public static Color Get(MercRosterDefs.MercRarity rarity)
    {
        switch (rarity)
        {
            case MercRosterDefs.MercRarity.Rare:
                return new Color(0.35f, 0.65f, 1f, 1f); // 蓝
            case MercRosterDefs.MercRarity.Legendary:
                return new Color(1f, 0.84f, 0.2f, 1f); // 金
            default:
                return Color.white; // 普通白
        }
    }

    public static Color GetOutline() => new Color(0.1f, 0.1f, 0.1f, 1f);

    public static MercRosterDefs.MercRarity ResolveMercRarity(string mercIdOrAssetId)
    {
        if (MercRosterDefs.TryGetByAssetId(mercIdOrAssetId, out var def))
            return def.Rarity;
        if (MercRosterDefs.TryGetByHireId(mercIdOrAssetId, out def))
            return def.Rarity;
        return MercRosterDefs.MercRarity.Common;
    }
}
