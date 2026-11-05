using UnityEngine;

/// <summary>
/// 装备材质口径（2026-10-06 主人拍板收敛后只剩两条路）。
///
/// <b>世界空间（SpriteRenderer）：装备不带描边</b> —— 一律 Sprites/Default，装备就是美术图本来的样子。
/// 原实现给 稀有 / 传奇 挂 <c>Armor_xiyou</c> / <c>Armor_chuanqi</c>：这两个材质用的是
/// 「Sprite Shaders Ultimate/Standard/Color/Outer Outline」着色器，唯一效果就是沿轮廓描一圈
/// 蓝（稀有 _OuterOutlineColor ≈ 0.46,0.53,1）/ 黄（传奇 ≈ 0.97,1,0.49）外描边
/// （该着色器只把 alpha&lt;1 的边缘像素往描边色拉，本体像素不动）。
/// 主人原话「装备不要带描边」→ 整条外层描边链路删除；两个 .mat 文件留着不动（美术资源不删）。
///
/// <b>UI（Image）：一律不挂材质</b>（null = UI 内置默认材质，透明通道才正常）。
/// 2026-09-27 的教训：Armor_xiyou 是不透明的世界空间材质，挂到 Image 上会把整块填成色块。
///
/// 稀有度在 UI 上只由 <see cref="EquipRarityRim"/> 的格子描边表达，别再往图标上挂东西。
/// </summary>
public static class EquipRarityMaterials
{
    static Material _defaultSprite;
    static bool _defaultSpriteResolved;

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

    /// <summary>世界空间装备：一律默认精灵材质（不带稀有度描边，见类注释）。</summary>
    public static void Apply(SpriteRenderer sr)
    {
        if (sr == null) return;
        var def = DefaultSpriteMaterial();
        if (def != null) sr.sharedMaterial = def;
    }

    /// <summary>UI 图标（无品质上下文）：一律不挂材质（null = UI 内置默认材质，透明通道正常）。</summary>
    public static void Apply(UnityEngine.UI.Image img)
    {
        if (img == null) return;
        img.material = null;
    }

    // ── UI 传奇（金色）装备流光（2026-11-05 主人指令：替换界面 + 装备界面里的图标）──
    // 10-06「UI 图标一律不挂材质」的例外：传奇品质装备图标挂 SSU Shine 流光材质
    // （Resources/Materials/Equip_shine_gold，与登录 LOGO logo01 / 按钮 btn19流光 同一着色器同族，
    //  UI 上已验证可用；09-27 色块事故的 Armor_xiyou 是外描边世界材质，与此不同族）。
    // 其余品质一律 null（内置默认材质）；品质框/描边仍走各自既有链路（LoadIconBg / EquipRarityRim），不混用。
    static Material _legendaryShine;
    static bool _legendaryShineResolved;
    static bool _legendaryShineMissingLogged;

    /// <summary>传奇流光材质；缺失按口径 LogError（不静默兜底），调用方退回默认材质。</summary>
    public static Material LegendaryShineMaterial()
    {
        if (_legendaryShineResolved) return _legendaryShine;
        _legendaryShineResolved = true;
        _legendaryShine = Resources.Load<Material>("Materials/Equip_shine_gold");
        if (_legendaryShine == null && !_legendaryShineMissingLogged)
        {
            _legendaryShineMissingLogged = true;
            Debug.LogError("[EquipRarityMaterials] 传奇流光材质缺失：Resources/Materials/Equip_shine_gold.mat 加载失败，图标暂用默认材质");
        }
        return _legendaryShine;
    }

    /// <summary>UI 图标（带品质）：传奇挂流光材质，其余一律内置默认材质（见上注释）。</summary>
    public static void Apply(UnityEngine.UI.Image img, Rarity? rarity)
    {
        if (img == null) return;
        img.material = rarity == Rarity.Legendary ? LegendaryShineMaterial() : null;
    }
}
