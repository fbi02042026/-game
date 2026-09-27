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

    /// <summary>
    /// 2026-09-27 主人反馈：武器/装备图标「变红后显示有白色区域，感觉没有透明通道了」。
    /// 根因：Armor_xiyou / Armor_chuanqi 是 **SPUM 角色染色**材质（不透明、带描边、走世界空间），
    /// 挂到 UI Image 上不走 UI 的 alpha 混合 → 整块被不透明填充，贴图外的部分就成了白/红方块。
    /// 修法：**UI 图标一律不挂材质**（null = 用 UI 内置默认材质，透明通道正常）；
    /// 稀有度在 UI 上用颜色/边框表达，别再往 Image 上挂世界材质。
    /// 世界空间的 SpriteRenderer 染色仍走 <see cref="Apply(SpriteRenderer, Rarity)"/>，不受影响。
    /// </summary>
    public static void Apply(UnityEngine.UI.Image img, Rarity rarity)
    {
        if (img == null) return;
        img.material = null;
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
