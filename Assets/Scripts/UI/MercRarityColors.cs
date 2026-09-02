using UnityEngine;

/// <summary>佣兵稀有度颜色（图鉴 / 战斗头顶名牌等共用）。</summary>
public static class MercRarityColors
{
    public static Color Get(MercRosterDefs.MercRarity rarity)
    {
        switch (rarity)
        {
            case MercRosterDefs.MercRarity.Rare:
                return new Color(0.55f, 0.78f, 1f, 1f);
            case MercRosterDefs.MercRarity.Legendary:
                return new Color(1f, 0.82f, 0.28f, 1f);
            default:
                return Color.white;
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
