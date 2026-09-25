#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 生成引导话预制体（底板 + 文案 + 挖空遮罩 + 手指）。
/// 菜单：Tools/UI/生成引导话预制体（已存在则询问是否覆盖）。
/// </summary>
public static class TutorialHintPrefabGenerator
{
    const string PrefabAssetPath = "Assets/Resources/Prefabs/UI/TutorialHintUI.prefab";
    const string PrefabResourcesPath = "Prefabs/UI/TutorialHintUI";
    const string FontPath = "Assets/Resources/Fonts/fusion-pixel.ttf";
    const string BannerArtPath = "Assets/Art/UI/引导/引导底框.png";
    const string PointerArtPath = "Assets/Art/UI/引导/引导.png";

    [MenuItem("Tools/UI/生成引导话预制体")]
    public static void Generate()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAssetPath) != null)
        {
            if (!EditorUtility.DisplayDialog(
                    "引导话预制体",
                    "已存在 TutorialHintUI.prefab，是否覆盖？\n（你改过的 Sprite/布局会被盖掉）",
                    "覆盖", "取消"))
                return;
        }

        if (!GenerateInternal(showDialog: true))
            EditorUtility.DisplayDialog("失败", "引导话预制体生成失败，请检查字体与底框图。", "确定");
    }

    /// <summary>批处理：仅首次创建，不覆盖。</summary>
    public static void GenerateBatch()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAssetPath) != null)
        {
            Debug.Log("[TutorialHintPrefabGenerator] 已存在，跳过: " + PrefabAssetPath);
            return;
        }
        GenerateInternal(showDialog: false);
    }

    static bool GenerateInternal(bool showDialog)
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (font == null)
            font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/fusion-pixel.otf");
        if (font == null)
        {
            Debug.LogError("[TutorialHintPrefabGenerator] 未找到字体: " + FontPath);
            return false;
        }

        Sprite bannerSp = LoadSprite(BannerArtPath);
        Sprite pointerSp = LoadSprite(PointerArtPath);

        string dir = Path.GetDirectoryName(PrefabAssetPath)?.Replace('\\', '/');
        EnsureFolder(dir);

        var root = BuildHierarchy(font, bannerSp, pointerSp);
        try
        {
            PrefabUtility.SaveAsPrefabAsset(root, PrefabAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAssetPath) == null)
            {
                Debug.LogError("[TutorialHintPrefabGenerator] 写入失败: " + PrefabAssetPath);
                return false;
            }

            Debug.Log("[TutorialHintPrefabGenerator] 已生成: " + PrefabAssetPath);
            if (showDialog)
            {
                EditorUtility.DisplayDialog("完成",
                    "已生成：\n" + PrefabAssetPath +
                    "\n\n节点：Banner/HintText、Dim*、Hole、PointerHand\n" +
                    "底框：" + BannerArtPath,
                    "确定");
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAssetPath);
            }
            return true;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    static Sprite LoadSprite(string path)
    {
        var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sp != null) return sp;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null) return null;
        return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }

    static void EnsureFolder(string dir)
    {
        if (string.IsNullOrEmpty(dir) || Directory.Exists(dir)) return;
        string[] parts = dir.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }

    public static GameObject BuildHierarchy(Font font, Sprite bannerSp, Sprite pointerSp)
    {
        var root = new GameObject("TutorialHintUI", typeof(RectTransform));
        var canvas = root.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TutorialHint);
        root.AddComponent<GraphicRaycaster>();
        var cg = root.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;
        root.AddComponent<TutorialHintUI>();

        Color dim = new Color(0f, 0f, 0f, 0.38f);
        CreateImage(root.transform, "DimTop", dim, raycast: true).gameObject.SetActive(false);
        CreateImage(root.transform, "DimBottom", dim, raycast: true).gameObject.SetActive(false);
        CreateImage(root.transform, "DimLeft", dim, raycast: true).gameObject.SetActive(false);
        CreateImage(root.transform, "DimRight", dim, raycast: true).gameObject.SetActive(false);

        var holeGo = new GameObject("Hole", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        holeGo.transform.SetParent(root.transform, false);
        var holeImg = holeGo.GetComponent<Image>();
        holeImg.color = new Color(1f, 1f, 1f, 0f);
        holeImg.raycastTarget = true;
        var holeBtn = holeGo.GetComponent<Button>();
        holeBtn.transition = Selectable.Transition.None;
        holeGo.SetActive(false);

        var banner = CreateImage(root.transform, "Banner", Color.white, raycast: false);
        if (bannerSp != null)
        {
            banner.sprite = bannerSp;
            banner.type = Image.Type.Simple;
            banner.color = Color.white;
        }
        else
        {
            banner.color = new Color(0.08f, 0.1f, 0.16f, 0.88f);
        }
        var brt = banner.rectTransform;
        brt.anchorMin = new Vector2(0.02f, 0.58f);
        brt.anchorMax = new Vector2(0.98f, 0.58f);
        brt.pivot = new Vector2(0.5f, 0.5f);
        brt.anchoredPosition = Vector2.zero;
        brt.sizeDelta = new Vector2(0f, 100f);

        var hint = CreateText(banner.transform, "HintText", "引导提示文案", 26, TextAnchor.MiddleCenter, font);
        hint.color = new Color(0xFC / 255f, 0xFD / 255f, 0xBE / 255f, 1f);
        var lrt = hint.rectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.pivot = new Vector2(0.5f, 0.5f);
        lrt.offsetMin = new Vector2(28f, 18f);
        lrt.offsetMax = new Vector2(-28f, -10f); // Top=10
        hint.horizontalOverflow = HorizontalWrapMode.Wrap;
        hint.verticalOverflow = VerticalWrapMode.Overflow;

        var pointerGo = new GameObject("PointerHand", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        pointerGo.transform.SetParent(root.transform, false);
        var prt = pointerGo.GetComponent<RectTransform>();
        prt.pivot = new Vector2(0.5f, 1f);
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(96f, 96f);
        var pImg = pointerGo.GetComponent<Image>();
        pImg.raycastTarget = false;
        pImg.preserveAspect = true;
        if (pointerSp != null) pImg.sprite = pointerSp;
        pointerGo.SetActive(false);

        return root;
    }

    static Image CreateImage(Transform parent, string name, Color color, bool raycast)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    static Text CreateText(Transform parent, string name, string content, int size, TextAnchor align, Font font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.font = font;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }
}
#endif
