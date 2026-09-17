#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 佣兵养成素材（职业徽记 / 本命碎片底版）的 Art→Resources 同步 + 可视化预览。
///
/// 美术放图位置（不用改）：
///   Assets/Art/UI/Icons/徽章/    剑盾|狂战|游侠|法师|牧师|重武 + 普通|稀有|传奇 .png
///   Assets/Art/UI/Icons/人物碎片/ 普通碎片.png / 稀有碎片.png / 传奇碎片.png
/// 打包能 Load 到的位置：
///   Assets/Resources/Icons/JobBadge、Assets/Resources/Icons/MercFragmentBase
/// </summary>
public static class MercGrowArtTool
{
    const string ArtBadgeDir = "Assets/Art/UI/Icons/徽章";
    const string ArtFragmentDir = "Assets/Art/UI/Icons/人物碎片";
    const string ResBadgeDir = "Assets/Resources/Icons/JobBadge";
    const string ResFragmentDir = "Assets/Resources/Icons/MercFragmentBase";

    const string PreviewRootName = "MercGrowArtPreview";

    // ==================== 同步 ====================

    [MenuItem("Tools/UI/同步佣兵养成素材到 Resources")]
    public static void SyncAll()
    {
        EnsureFolder("Assets/Resources/Icons");
        int badge = SyncDir(ArtBadgeDir, ResBadgeDir);
        int frag = SyncDir(ArtFragmentDir, ResFragmentDir);
        AssetDatabase.Refresh();
        Debug.Log($"[MercGrowArtTool] 徽记={badge} 碎片底版={frag}");
        EditorUtility.DisplayDialog("佣兵养成素材",
            $"已同步到 Resources。\n职业徽记：{badge} 张\n碎片底版：{frag} 张\n\n" +
            "（Resources 那份才是打包后 Load 得到的，两处都要保留）", "OK");
    }

    static int SyncDir(string artDir, string resDir)
    {
        if (!Directory.Exists(artDir)) return 0;
        EnsureFolder(resDir);
        int n = 0;
        foreach (string file in Directory.GetFiles(artDir, "*.png"))
        {
            string fileName = Path.GetFileName(file);
            string dest = resDir + "/" + fileName;
            File.Copy(file, dest, true);
            AssetDatabase.ImportAsset(dest, ImportAssetOptions.ForceUpdate);
            EnsureSpriteImport(dest);
            n++;
        }
        return n;
    }

    /// <summary>徽记/碎片是 UI 方形图：Sprite + Single + nPOT=None + FullRect（Mask 裁剪需要）。</summary>
    static void EnsureSpriteImport(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;
        bool dirty = false;
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            dirty = true;
        }
        if (importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            dirty = true;
        }
        if (importer.npotScale != TextureImporterNPOTScale.None)
        {
            importer.npotScale = TextureImporterNPOTScale.None;
            dirty = true;
        }
        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            dirty = true;
        }
        if (!importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            dirty = true;
        }
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        if (settings.spriteMeshType != SpriteMeshType.FullRect)
        {
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            dirty = true;
        }
        if (dirty) importer.SaveAndReimport();
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    // ==================== 预览 ====================

    static readonly string[] JobKeys = { "剑盾", "狂战", "游侠", "法师", "牧师", "重武" };
    static readonly MercRosterDefs.MercRarity[] Tiers =
    {
        MercRosterDefs.MercRarity.Common,
        MercRosterDefs.MercRarity.Rare,
        MercRosterDefs.MercRarity.Legendary,
    };
    static readonly string[] SampleMercs = { "H001", "H006", "H004" };

    /// <summary>在当前场景摆一个预览板（ScreenSpaceOverlay，不碰相机，删掉即可）。</summary>
    [MenuItem("Tools/UI/预览佣兵养成素材（徽记/碎片底版）")]
    public static void Preview()
    {
        var old = GameObject.Find(PreviewRootName);
        if (old != null) Object.DestroyImmediate(old);

        var root = new GameObject(PreviewRootName, typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;   // 编辑模式安全，不动相机
        canvas.sortingOrder = 32000;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720f, 1280f);
        scaler.matchWidthOrHeight = 0.5f;

        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(720f, 1280f);

        var bg = root.AddComponent<Image>();
        bg.color = new Color(0.10f, 0.09f, 0.12f, 1f);
        bg.raycastTarget = false;

        int found = 0, missing = 0;

        Label(root.transform, "佣兵养成素材预览 · 徽记 18 / 碎片底版 3", 26, 540f, 700f, 46f);

        // --- 徽记：6 职业 × 3 档 ---
        float cell = 108f, gapX = 6f;
        float startX = -(JobKeys.Length - 1) * (cell + gapX) * 0.5f;
        for (int c = 0; c < JobKeys.Length; c++)
            Label(root.transform, JobKeys[c], 18, 470f, cell, 26f, startX + c * (cell + gapX));

        for (int r = 0; r < Tiers.Length; r++)
        {
            float y = 390f - r * (cell + 16f);
            Label(root.transform, TierKey(Tiers[r]), 18, y, cell, 26f, startX - (cell + gapX));
            for (int c = 0; c < JobKeys.Length; c++)
            {
                var sp = MercGrowSprites.LoadJobBadge(JobKeys[c], Tiers[r]);
                if (sp != null) found++; else missing++;
                var img = MercGrowUI.CreateBadge(root.transform, $"Badge_{r}_{c}", JobKeys[c], Tiers[r], cell);
                img.rectTransform.anchoredPosition = new Vector2(startX + c * (cell + gapX), y - 6f);
                MercGrowUI.SetBadgeCount(img, (r + 1) * (c + 1) * 3);
            }
        }

        // --- 碎片底版 ---
        Label(root.transform, "本命碎片底版（Art/UI/Icons/人物碎片）", 20, -70f, 700f, 30f);
        float fx = -150f;
        for (int i = 0; i < Tiers.Length; i++)
        {
            var sp = MercGrowSprites.LoadFragmentBase(Tiers[i]);
            if (sp != null) found++; else missing++;
            var go = StretchImage(root.transform, $"FragBase_{i}", sp);
            var grt = go.GetComponent<RectTransform>();
            grt.anchorMin = grt.anchorMax = new Vector2(0.5f, 0.5f);
            grt.sizeDelta = new Vector2(120f, 120f);
            grt.anchoredPosition = new Vector2(fx + i * 150f, -170f);
        }

        // --- 碎片卡：底版 + 头像（Mask 裁剪）---
        Label(root.transform, $"碎片卡 = 底版 + 头像（Mask 用底版 alpha，FrameOnTop={MercGrowUI.FrameOnTop}）",
            20, -280f, 700f, 30f);
        for (int i = 0; i < Tiers.Length; i++)
        {
            var card = MercGrowUI.CreateFragmentCard(root.transform, $"FragmentCard_{i}",
                Tiers[i], SampleMercs[i], 170f);
            card.Rect.anchoredPosition = new Vector2(-170f + i * 170f, -400f);
            Label(root.transform, SampleMercs[i], 18, -500f, 160f, 26f, -170f + i * 170f);
        }

        // --- 另一层序对照 ---
        bool keep = MercGrowUI.FrameOnTop;
        MercGrowUI.FrameOnTop = !keep;
        var alt = MercGrowUI.CreateFragmentCard(root.transform, "FragmentCard_Alt",
            MercRosterDefs.MercRarity.Legendary, "H004", 170f);
        alt.Rect.anchoredPosition = new Vector2(0f, -630f);
        MercGrowUI.FrameOnTop = keep;
        Label(root.transform, $"同一张卡、FrameOnTop={!keep}（对照）", 18, -730f, 700f, 26f);

        Label(root.transform, $"命中 {found} 张 / 缺 {missing} 张" +
            (missing > 0 ? "　→ 跑一次 Tools/UI/同步佣兵养成素材到 Resources" : "　素材齐"),
            22, -800f, 700f, 30f);

        Selection.activeGameObject = root;
        Debug.Log($"[MercGrowArtTool] 预览已生成：命中 {found}、缺 {missing}");
    }

    // ==================== 小工具 ====================

    static string TierKey(MercRosterDefs.MercRarity rarity) => MercGrowSprites.TierKey(rarity);

    static Text Label(Transform parent, string content, int size, float y, float w, float h, float x = 0f)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);

        var t = go.GetComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color(0.92f, 0.90f, 0.86f, 1f);
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.font = GameFonts.GetChinese();
        if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return t;
    }

    static GameObject StretchImage(Transform parent, string name, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = Color.white;
        img.raycastTarget = false;
        img.preserveAspect = true;
        return go;
    }
}
#endif
