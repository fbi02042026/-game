#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 酒馆预制体：带 Canvas（Screen Space - Camera）的内容骨架。
/// 不含资源条/底栏。已存在时必须确认才覆盖。无 InitializeOnLoad。
/// </summary>
public static class TavernPrefabGenerator
{
    static readonly Color C_BG = new Color(0.18f, 0.10f, 0.07f, 1f);
    static readonly Color C_SCENE = new Color(0.35f, 0.22f, 0.14f, 0.65f);
    static readonly Color C_PANEL = new Color(0.10f, 0.07f, 0.05f, 0.88f);
    static readonly Color C_BORDER = new Color(0.72f, 0.52f, 0.28f, 0.75f);
    static readonly Color C_TITLE = new Color(1f, 0.93f, 0.78f, 1f);
    static readonly Color C_DESC = new Color(0.86f, 0.80f, 0.70f, 0.92f);

    const string PrefabPath = "Assets/Resources/Prefabs/Town/TavernUI.prefab";

    [MenuItem("Tools/_归档/UI/重新生成酒馆骨架（会确认覆盖）")]
    public static void GenerateMenu()
    {
        if (System.IO.File.Exists(PrefabPath))
        {
            if (!EditorUtility.DisplayDialog(
                    "确认覆盖酒馆预制体？",
                    "将重建带 Canvas 的酒馆骨架（场景槽 + 四入口），不含资源条/底栏。\n你的美术改动可能丢失。\n\n" + PrefabPath,
                    "覆盖重建", "取消"))
                return;
        }
        GenerateInternal();
        EditorUtility.DisplayDialog("完成",
            "已生成酒馆骨架（含 Canvas / Scaler / Raycaster）。\n运行时绑 Camera.main。\n" + PrefabPath, "OK");
    }

    /// <summary>批处理入口：Tuanjie.exe -executeMethod TavernPrefabGenerator.GenerateBatch</summary>
    public static void GenerateBatch()
    {
        GenerateInternal();
        Debug.Log("[TavernPrefabGenerator] Batch OK: " + PrefabPath);
    }

    static void GenerateInternal()
    {
        EnsureFolders();

        var root = new GameObject("TavernUI", typeof(RectTransform));
        var rt = root.GetComponent<RectTransform>();
        Stretch(rt);

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.sortingOrder = 10;
        UICanvasSetup.Apply(canvas, null);
        // 与公会大厅一致：Match Height
        var scaler = root.GetComponent<CanvasScaler>();
        if (scaler != null)
            scaler.matchWidthOrHeight = GameConfig.UI_MATCH;

        var tavern = root.AddComponent<TavernUI>();

        var bg = CreateImage(root.transform, "TavernBackground", C_BG);
        Stretch(bg.rectTransform);

        var scene = CreateImage(root.transform, "TavernScene", C_SCENE);
        Stretch(scene.rectTransform);
        scene.rectTransform.offsetMin = new Vector2(0f, 150f);
        scene.rectTransform.offsetMax = new Vector2(0f, -120f);
        var sceneHint = CreateText(scene.transform, "SceneHint", "【酒馆场景插画槽】", 22, new Color(1f, 1f, 1f, 0.35f), false);
        Stretch(sceneHint.rectTransform);

        var grid = CreateRect(root.transform, "FeatureGrid");
        SetAnchored(grid, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 360f), new Vector2(660f, 380f));

        tavern.recruitButton = CreateFeatureCard(grid.transform, "Recruit", "佣兵招募", "招募新佣兵加入队伍",
            new Vector2(-165f, 90f), new Color(0.58f, 0.44f, 0.28f));
        tavern.trustButton = CreateFeatureCard(grid.transform, "Trust", "信任交流", "提升信任解锁故事与事件",
            new Vector2(165f, 90f), new Color(0.68f, 0.32f, 0.36f));
        tavern.questButton = CreateFeatureCard(grid.transform, "Quest", "酒馆任务", "完成委托获取丰厚奖励",
            new Vector2(-165f, -100f), new Color(0.38f, 0.46f, 0.58f));
        tavern.intelButton = CreateFeatureCard(grid.transform, "Intel", "佣兵情报", "查看佣兵资料与背景故事",
            new Vector2(165f, -100f), new Color(0.34f, 0.40f, 0.52f));

        tavern.bottomNavReserve = 150f;
        tavern.topBarReserve = 120f;

        GameFonts.ApplyToHierarchy(root.transform);
        root.SetActive(false);
        root.layer = 5; // UI

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TavernPrefabGenerator] 已生成: " + PrefabPath);
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs")) AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs/Town")) AssetDatabase.CreateFolder("Assets/Resources/Prefabs", "Town");
    }

    static Button CreateFeatureCard(Transform parent, string name, string title, string desc, Vector2 pos, Color accent)
    {
        var go = CreateRect(parent, name);
        SetAnchored(go, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(310f, 158f));

        CreateImage(go.transform, "Border", C_BORDER);
        Stretch(go.transform.Find("Border").GetComponent<RectTransform>());

        var panel = CreateImage(go.transform, "CardBg", C_PANEL);
        Stretch(panel.rectTransform);
        panel.rectTransform.offsetMin = new Vector2(4f, 4f);
        panel.rectTransform.offsetMax = new Vector2(-4f, -4f);

        var icon = CreateImage(go.transform, "Icon", accent);
        SetAnchored(icon.gameObject, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(54f, 6f), new Vector2(68f, 68f));

        var titleT = CreateText(go.transform, "Title", title, 26, C_TITLE, false);
        titleT.fontStyle = FontStyle.Bold;
        titleT.alignment = TextAnchor.MiddleLeft;
        var titleRt = titleT.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.offsetMin = new Vector2(104f, -52f);
        titleRt.offsetMax = new Vector2(-14f, -12f);

        var descT = CreateText(go.transform, "Desc", desc, 16, C_DESC, false);
        descT.alignment = TextAnchor.UpperLeft;
        descT.horizontalOverflow = HorizontalWrapMode.Wrap;
        var descRt = descT.rectTransform;
        descRt.anchorMin = new Vector2(0f, 0f);
        descRt.anchorMax = new Vector2(1f, 1f);
        descRt.offsetMin = new Vector2(104f, 16f);
        descRt.offsetMax = new Vector2(-14f, -56f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = panel;
        return btn;
    }

    static GameObject CreateRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = 5;
        return go;
    }

    static Image CreateImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        go.layer = 5;
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    static Text CreateText(Transform parent, string name, string content, int size, Color color, bool number)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        go.layer = 5;
        var t = go.GetComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.font = number ? GameFonts.GetNumber() : GameFonts.GetChinese();
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
        return t;
    }

    static void SetAnchored(GameObject go, Vector2 amin, Vector2 amax, Vector2 pos, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = amin;
        rt.anchorMax = amax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
#endif
