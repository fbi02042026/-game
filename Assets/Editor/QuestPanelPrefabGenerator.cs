#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.IO;

/// <summary>
/// 任务面板预制体生成器（2026-09-26 新增）。
///
/// 菜单：Tools/UI/生成任务面板预制体
/// 一键生成 / 增量更新 Assets/Resources/Prefabs/UI/QuestPanel.prefab。
///
/// 美术来源：Assets/Art/UI/任务/ 下主人新放的 5 张图，已语义重命名进 Resources/UI/Quest/：
///   图层 1 → QuestPanel.png  （主底框：金边深蓝、顶部中槽、左羽毛笔/右旗帜）
///   图层 3 → QuestTabOn.png  （Tab 选中态：亮蓝金边胶囊）
///   图层 4 → QuestTabOff.png （Tab 未选中态：暗灰蓝金边胶囊）
///   图层 5 → QuestRow.png    （任务条：左侧暗底 + 右侧自带蓝色小胶囊=前往按钮位）
///   图层 6 → QuestClose.png  （关闭按钮：红金胶囊）
///
/// 节点层级（与 Assets/Scripts/UI/QuestPanelUI.cs 读取的节点名**必须完全一致**）：
///   QuestPanel (root, Canvas + QuestPanelUI)
///   ├─ Dim            全屏半透明黑（兼点击关闭）
///   ├─ Panel          图层1 底框（Image，Sliced 九宫格）
///   │  ├─ Title       「任务」文本（摆在图层1 顶部中槽内）
///   │  ├─ TabMain     图层3/4 底图 + 文字「主线」 + Button
///   │  ├─ TabDaily    图层3/4 底图 + 文字「每日」 + Button
///   │  ├─ Header      「进行中 0/10」金色小标题
///   │  ├─ ScrollView  ScrollRect + RectMask2D（滑动裁剪）
///   │  │  └─ Content  VerticalLayoutGroup + ContentSizeFitter(Vertical=PreferredSize)
///   │  │     └─ RowTemplate  单条任务条（图层5 底图；含 StatusIcon/Title/Sub/GoBtn），生成后 SetActive(false)
///   │  └─ CloseBtn    图层6 底图 + 文字「关闭」 + Button
///
/// 增量更新：若预制体已存在，用 PrefabUtility.LoadPrefabContents 原地修改（按节点名复用已有节点、
/// 只刷新我们关心的属性），绝不清空重建——主人手调过的位置/尺寸会被保留。
/// </summary>
public class QuestPanelPrefabGenerator
{
    const string PREFAB_DIR = "Assets/Resources/Prefabs/UI";
    const string PREFAB_PATH = "Assets/Resources/Prefabs/UI/QuestPanel.prefab";
    const string ART_DIR = "Assets/Resources/UI/Quest/";   // 注意：Resources 下去掉 Resources 前缀

    // ===== 尺寸常量（720×1280 竖屏参考分辨率，MatchHeight）=====
    const float PANEL_W = 662f;     // 屏宽 720 × 92%
    const float PANEL_H = 1100f;    // 屏高 1280 × 86%
    const float ROW_H = 96f;        // 单条任务行高
    const float ROW_GAP = 8f;       // 行间距
    const int SORT_ORDER = 900;     // GameConfig.UiSort.TownPopup，与每日登录界面同级

    [MenuItem("Tools/UI/生成任务面板预制体")]
    public static void Generate()
    {
        EnsureFolders();

        bool existed = File.Exists(PREFAB_PATH);
        // 已存在 → 原地加载增量更新；不存在 → 全新建
        GameObject root = existed
            ? PrefabUtility.LoadPrefabContents(PREFAB_PATH)
            : new GameObject("QuestPanel");

        Build(root);

        // 写回预制体（两种来源都走 SaveAsPrefabAsset）
        PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);

        if (existed) PrefabUtility.UnloadPrefabContents(root);
        else UnityEngine.Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[QuestPanelPrefabGenerator] 任务面板预制体已生成：" + PREFAB_PATH
                  + (existed ? "（增量更新）" : "（首次创建）"));
        EditorUtility.DisplayDialog("生成完成",
            "任务面板预制体已生成！\n\n路径：" + PREFAB_PATH
            + "\n\n可直接在团结里打开预览；节点名已与 QuestPanelUI 对齐，运行时无需手改 prefab。", "确定");
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs"))
            AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");
        if (!AssetDatabase.IsValidFolder(PREFAB_DIR))
            AssetDatabase.CreateFolder("Assets/Resources/Prefabs", "UI");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/UI"))
            AssetDatabase.CreateFolder("Assets/Resources", "UI");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/UI/Quest"))
            AssetDatabase.CreateFolder("Assets/Resources/UI", "Quest");
    }

    // ============================================================
    // 建树（增量：节点存在则复用，只刷新属性）
    // ============================================================

    static void Build(GameObject root)
    {
        // ---- 根 Canvas + QuestPanelUI 组件 ----
        Canvas canvas = root.GetComponent<Canvas>();
        if (canvas == null) canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.overrideSorting = true;
        canvas.sortingOrder = SORT_ORDER;
        if (root.GetComponent<GraphicRaycaster>() == null)
            root.AddComponent<GraphicRaycaster>();
        if (root.GetComponent<QuestPanelUI>() == null)
            root.AddComponent<QuestPanelUI>();
        var rootRt = root.transform as RectTransform;
        Stretch(rootRt);

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.referenceResolution = new Vector2(720f, 1280f);
        scaler.matchWidthOrHeight = 1f;

        // ---- Dim：全屏半透明黑，兼点击关闭 ----
        GameObject dim = EnsureChild(root.transform, "Dim");
        Image dimImg = dim.GetComponent<Image>() ?? dim.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.72f);
        dimImg.raycastTarget = true;
        Stretch(dim.GetComponent<RectTransform>());
        if (dim.GetComponent<Button>() == null) dim.AddComponent<Button>();

        // ---- Panel：图层1 底框 ----
        GameObject panel = EnsureChild(root.transform, "Panel");
        Image panelImg = panel.GetComponent<Image>() ?? panel.AddComponent<Image>();
        panelImg.sprite = LoadSprite("QuestPanel");
        panelImg.type = Image.Type.Sliced;
        panelImg.raycastTarget = true;
        SetAnchored(panel.GetComponent<RectTransform>(),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(PANEL_W, PANEL_H));

        // ---- Title：「任务」 ----
        GameObject title = EnsureChild(panel.transform, "Title");
        Text titleTxt = title.GetComponent<Text>() ?? title.AddComponent<Text>();
        titleTxt.text = "任务";
        titleTxt.fontSize = 40;
        titleTxt.color = new Color(1f, 0.92f, 0.80f);
        titleTxt.alignment = TextAnchor.MiddleCenter;
        titleTxt.raycastTarget = false;
        SetAnchored(title.GetComponent<RectTransform>(),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 480f), new Vector2(PANEL_W - 80f, 64f));

        // ---- TabMain / TabDaily ----
        BuildTab(panel, "TabMain", "主线", new Vector2(-120f, 400f));
        BuildTab(panel, "TabDaily", "每日", new Vector2(120f, 400f));

        // ---- Header：金色小标题 ----
        GameObject header = EnsureChild(panel.transform, "Header");
        Text headerTxt = header.GetComponent<Text>() ?? header.AddComponent<Text>();
        headerTxt.text = "进行中 0/10";
        headerTxt.fontSize = 22;
        headerTxt.color = new Color(1f, 0.86f, 0.5f);
        headerTxt.alignment = TextAnchor.MiddleLeft;
        headerTxt.raycastTarget = false;
        SetAnchored(header.GetComponent<RectTransform>(),
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(-PANEL_W / 2f + 24f, 330f), new Vector2(PANEL_W - 48f, 32f));

        // ---- ScrollView：ScrollRect + RectMask2D（滑动裁剪）----
        GameObject scroll = EnsureChild(panel.transform, "ScrollView");
        ScrollRect sr = scroll.GetComponent<ScrollRect>() ?? scroll.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Elastic;
        sr.inertia = true;
        if (scroll.GetComponent<RectMask2D>() == null) scroll.AddComponent<RectMask2D>();
        var scrollRt = scroll.GetComponent<RectTransform>();
        // 留出顶部（标题/Tab/Header）与底部（关闭按钮）空间
        scrollRt.anchorMin = new Vector2(0f, 0f);
        scrollRt.anchorMax = new Vector2(1f, 1f);
        scrollRt.offsetMin = new Vector2(20f, 90f);
        scrollRt.offsetMax = new Vector2(-20f, -150f);
        scrollRt.anchoredPosition = Vector2.zero;
        scrollRt.sizeDelta = Vector2.zero;

        // ---- Content：VerticalLayoutGroup + ContentSizeFitter ----
        GameObject content = EnsureChild(scroll.transform, "Content");
        VerticalLayoutGroup vlg = content.GetComponent<VerticalLayoutGroup>() ?? content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = ROW_GAP;
        vlg.padding = new RectOffset(8, 8, 8, 8);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        ContentSizeFitter csf = content.GetComponent<ContentSizeFitter>() ?? content.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = Vector2.zero;
        sr.content = contentRt;

        // ---- RowTemplate：单条任务行（SetActive(false) 当模板）----
        GameObject rowTpl = EnsureChild(content.transform, "RowTemplate");
        Image rowImg = rowTpl.GetComponent<Image>() ?? rowTpl.AddComponent<Image>();
        rowImg.sprite = LoadSprite("QuestRow");
        rowImg.type = Image.Type.Sliced;
        rowImg.color = Color.white;
        rowImg.raycastTarget = true;
        LayoutElement le = rowTpl.GetComponent<LayoutElement>() ?? rowTpl.AddComponent<LayoutElement>();
        le.preferredHeight = ROW_H;
        le.flexibleHeight = 0f;
        var rowRt = rowTpl.GetComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0f, 1f);
        rowRt.anchorMax = new Vector2(1f, 1f);
        rowRt.pivot = new Vector2(0.5f, 1f);
        rowRt.anchoredPosition = Vector2.zero;
        rowRt.sizeDelta = Vector2.zero;

        BuildRowChildren(rowTpl);

        rowTpl.SetActive(false);

        // ---- CloseBtn：图层6 底图 + 「关闭」----
        GameObject close = EnsureChild(panel.transform, "CloseBtn");
        Image closeImg = close.GetComponent<Image>() ?? close.AddComponent<Image>();
        closeImg.sprite = LoadSprite("QuestClose");
        closeImg.type = Image.Type.Sliced;
        closeImg.raycastTarget = true;
        SetAnchored(close.GetComponent<RectTransform>(),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -480f), new Vector2(220f, 56f));
        if (close.GetComponent<Button>() == null) close.AddComponent<Button>();
        GameObject closeLabelGo = EnsureChild(close.transform, "Label");
        Text closeTxt = closeLabelGo.GetComponent<Text>();
        if (closeTxt == null) closeTxt = closeLabelGo.AddComponent<Text>();
        closeTxt.text = "关闭";
        closeTxt.fontSize = 24;
        closeTxt.color = Color.white;
        closeTxt.alignment = TextAnchor.MiddleCenter;
        closeTxt.raycastTarget = false;
        Stretch(closeTxt.rectTransform);

        // 字体预览（运行时 Open 还会再刷一次）
        GameFonts.ApplyToHierarchy(root.transform);
    }

    /// <summary>一个 Tab：胶囊底图（图层3/4）+ 文字 + Button。</summary>
    static void BuildTab(GameObject parent, string name, string label, Vector2 pos)
    {
        GameObject tab = EnsureChild(parent.transform, name);
        Image img = tab.GetComponent<Image>() ?? tab.AddComponent<Image>();
        img.sprite = LoadSprite("QuestTabOff");
        img.type = Image.Type.Sliced;
        img.raycastTarget = true;
        SetAnchored(tab.GetComponent<RectTransform>(),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(200f, 56f));
        if (tab.GetComponent<Button>() == null) tab.AddComponent<Button>();

        GameObject labelGo = EnsureChild(tab.transform, "Label");
        Text txt = labelGo.GetComponent<Text>() ?? labelGo.AddComponent<Text>();
        txt.text = label;
        txt.fontSize = 26;
        txt.color = new Color(0.55f, 0.55f, 0.58f);
        txt.alignment = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;
        Stretch(labelGo.GetComponent<RectTransform>());
    }

    /// <summary>任务行内部四件套：StatusIcon / Title / Sub / GoBtn。</summary>
    static void BuildRowChildren(GameObject row)
    {
        // 状态图标（▶ 进行中 / ○ 未完成，代码画字）
        GameObject status = EnsureChild(row.transform, "StatusIcon");
        Text statusTxt = status.GetComponent<Text>() ?? status.AddComponent<Text>();
        statusTxt.text = "○";
        statusTxt.fontSize = 30;
        statusTxt.color = new Color(1f, 0.86f, 0.5f);
        statusTxt.alignment = TextAnchor.MiddleCenter;
        statusTxt.raycastTarget = false;
        SetAnchors(status.GetComponent<RectTransform>(),
            0f, 0.5f, 0f, 0.5f, 24f, -20f, 64f, 20f);

        // 标题（白/亮色）
        GameObject title = EnsureChild(row.transform, "Title");
        Text titleTxt = title.GetComponent<Text>() ?? title.AddComponent<Text>();
        titleTxt.text = "任务标题";
        titleTxt.fontSize = 24;
        titleTxt.color = new Color(0.95f, 0.92f, 0.86f);
        titleTxt.alignment = TextAnchor.MiddleLeft;
        titleTxt.raycastTarget = false;
        SetAnchors(title.GetComponent<RectTransform>(),
            0f, 1f, 1f, 1f, 76f, -48f, -170f, -12f);

        // 副标题（灰绿色小字：条件或奖励）
        GameObject sub = EnsureChild(row.transform, "Sub");
        Text subTxt = sub.GetComponent<Text>() ?? sub.AddComponent<Text>();
        subTxt.text = "任务描述 / 奖励：天赋石 ×1";
        subTxt.fontSize = 18;
        subTxt.color = new Color(0.62f, 0.74f, 0.66f);
        subTxt.alignment = TextAnchor.MiddleLeft;
        subTxt.raycastTarget = false;
        SetAnchors(sub.GetComponent<RectTransform>(),
            0f, 0f, 1f, 0f, 76f, 8f, -170f, 38f);

        // 「前往」按钮：落在图层5 自带的蓝色胶囊位上（按钮本体透明，靠行底图显示蓝色胶囊）
        GameObject go = EnsureChild(row.transform, "GoBtn");
        Image goImg = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        goImg.color = new Color(1f, 1f, 1f, 0f);   // 透明，但保留射线接收（落到蓝色胶囊位）
        goImg.raycastTarget = true;
        if (go.GetComponent<Button>() == null) go.AddComponent<Button>();
        SetAnchors(go.GetComponent<RectTransform>(),
            1f, 0.5f, 1f, 0.5f, -170f, -24f, -20f, 24f);
        GameObject goLabel = EnsureChild(go.transform, "Label");
        Text goTxt = goLabel.GetComponent<Text>() ?? goLabel.AddComponent<Text>();
        goTxt.text = "前往";
        goTxt.fontSize = 22;
        goTxt.color = Color.white;
        goTxt.alignment = TextAnchor.MiddleCenter;
        goTxt.raycastTarget = false;
        Stretch(goLabel.GetComponent<RectTransform>());
    }

    // ============================================================
    // 小工具
    // ============================================================

    static Sprite LoadSprite(string fileNameWithoutExt)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(ART_DIR + fileNameWithoutExt + ".png");
    }

    static GameObject EnsureChild(Transform parent, string name)
    {
        if (parent == null) return null;
        var found = parent.Find(name);
        if (found != null) return found.gameObject;
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        return g;
    }

    static void SetAnchored(RectTransform rt, Vector2 min, Vector2 max, Vector2 pos, Vector2 size)
    {
        if (rt == null) return;
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    static void SetAnchors(RectTransform rt, float minX, float minY, float maxX, float maxY,
        float left, float bottom, float right, float top)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(minX, minY);
        rt.anchorMax = new Vector2(maxX, maxY);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(right, top);
    }

    static void Stretch(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }
}
#endif
