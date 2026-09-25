#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 生成健康忠告预制体：fusion-pixel 字体在 Inspector 预绑，避免运行时换字体闪屏。
/// </summary>
[InitializeOnLoad]
public static class HealthNoticePrefabGenerator
{
    const string PrefabAssetPath = "Assets/Resources/Prefabs/Boot/HealthNoticeUI.prefab";
    const string PrefabResourcesPath = "Prefabs/Boot/HealthNoticeUI";
    const string FontPath = "Assets/Resources/Fonts/fusion-pixel.ttf";

    static HealthNoticePrefabGenerator()
    {
        EditorApplication.delayCall += EnsurePrefabExists;
    }

    [MenuItem("Tools/UI/生成健康忠告预制体")]
    public static void Generate()
    {
        if (!GenerateInternal(showDialog: true))
            EditorUtility.DisplayDialog("失败", "健康忠告预制体生成失败，请检查字体路径。", "确定");
    }

    static void EnsurePrefabExists()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (PrefabIsLoadable()) return;
        GenerateInternal(showDialog: false);
    }

    static bool PrefabIsLoadable()
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAssetPath);
        if (asset == null) return false;
        return Resources.Load<GameObject>(PrefabResourcesPath) != null;
    }

    static bool GenerateInternal(bool showDialog)
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (font == null)
        {
            Debug.LogError("[HealthNoticePrefabGenerator] 未找到字体: " + FontPath);
            return false;
        }

        string dir = Path.GetDirectoryName(PrefabAssetPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var root = BuildHierarchy(font);
        try
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAssetPath);
            if (existing != null)
                PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabAssetPath, InteractionMode.UserAction);
            else
                PrefabUtility.SaveAsPrefabAsset(root, PrefabAssetPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!PrefabIsLoadable())
            {
                Debug.LogError("[HealthNoticePrefabGenerator] 预制体已写入但 Resources 仍无法加载: " + PrefabResourcesPath);
                return false;
            }

            if (showDialog)
                EditorUtility.DisplayDialog("完成", "已生成/更新：\n" + PrefabAssetPath, "确定");
            else
                Debug.Log("[HealthNoticePrefabGenerator] 已自动生成健康忠告预制体: " + PrefabAssetPath);
            return true;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    public static GameObject BuildHierarchy(Font font)
    {
        var root = new GameObject("HealthNoticeUI", typeof(RectTransform));

        var canvas = root.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.StoryDialogue);

        var ui = root.AddComponent<HealthNoticeUI>();

        var bg = CreateImage(root.transform, "Bg", Color.black);
        Stretch(bg);

        var title = CreateText(root.transform, "Title", "健康游戏忠告", 42, TextAnchor.MiddleCenter, font);
        title.color = new Color(1f, 0.92f, 0.55f, 1f);
        var titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0.08f, 0.62f);
        titleRt.anchorMax = new Vector2(0.92f, 0.76f);
        titleRt.offsetMin = titleRt.offsetMax = Vector2.zero;

        var body = CreateText(root.transform, "Body",
            "抵制不良游戏，拒绝盗版游戏。\n" +
            "注意自我保护，谨防受骗上当。\n" +
            "适度游戏益脑，沉迷游戏伤身。\n" +
            "合理安排时间，享受健康生活。",
            26, TextAnchor.MiddleCenter, font);
        body.lineSpacing = 1.45f;
        body.color = new Color(0.92f, 0.9f, 0.86f, 1f);
        var bodyRt = body.rectTransform;
        bodyRt.anchorMin = new Vector2(0.08f, 0.28f);
        bodyRt.anchorMax = new Vector2(0.92f, 0.58f);
        bodyRt.offsetMin = bodyRt.offsetMax = Vector2.zero;

        var so = new SerializedObject(ui);
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("bodyText").objectReferenceValue = body;
        so.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    static Image CreateImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
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
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static void Stretch(Component c)
    {
        var rt = c.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
#endif
