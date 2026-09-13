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
        // 不能用 Resources.GetBuiltinResource<Material>("Sprites-Default.mat")：
        // 该内置材质不在打包后的构建里，运行时会报 “Failed to find Sprites-Default.mat”，返回 null 导致材质丢失。
        // 改为按 Shader 现建材质，与 Monster.cs 的阴影材质走同一稳妥路径。
        var shader = Shader.Find("Sprites/Default");
        _defaultSprite = shader != null ? new Material(shader) : null;
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
