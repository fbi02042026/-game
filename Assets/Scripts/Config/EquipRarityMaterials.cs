using UnityEngine;

/// <summary>
/// 装备/武器稀有度材质：普通不挂；稀有 Armor_xiyou；传奇 Armor_chuanqi。
/// 资源路径：Resources/Materials/Armor_xiyou、Armor_chuanqi。
/// </summary>
public static class EquipRarityMaterials
{
    const string RarePath = "Materials/Armor_xiyou";
    const string LegendaryPath = "Materials/Armor_chuanqi";

    static Material _rare;
    static Material _legendary;
    static Material _defaultSprite;
    static bool _defaultSpriteResolved;

    public static Material Get(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Rare:
                return LoadRare();
            case Rarity.Legendary:
                return LoadLegendary();
            default:
                return null;
        }
    }

    public static Material DefaultSpriteMaterial()
    {
        if (_defaultSpriteResolved) return _defaultSprite;
        _defaultSpriteResolved = true;
        _defaultSprite = Resources.GetBuiltinResource<Material>("Sprites-Default.mat");
        return _defaultSprite;
    }

    public static void Apply(SpriteRenderer sr, Rarity rarity)
    {
        if (sr == null) return;
        var mat = Get(rarity);
        if (mat != null)
            sr.sharedMaterial = mat;
        else
        {
            var def = DefaultSpriteMaterial();
            if (def != null) sr.sharedMaterial = def;
        }
    }

    public static void Apply(UnityEngine.UI.Image img, Rarity rarity)
    {
        if (img == null) return;
        var mat = Get(rarity);
        img.material = mat;
    }

    static Material LoadRare()
    {
        if (_rare == null)
            _rare = Resources.Load<Material>(RarePath);
        return _rare;
    }

    static Material LoadLegendary()
    {
        if (_legendary == null)
            _legendary = Resources.Load<Material>(LegendaryPath);
        return _legendary;
    }
}
