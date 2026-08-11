using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 冒险界面（MainNavTab.Adventure）。
///
/// 布局（720×1280 设计分辨率，竖屏）：
///   ─ TopBar（ResourceBar 复用，高 120）
///   ─ LeftSidebar（副本类型列表，宽 170）
///   ─ ContentArea（右侧章节地图+地图顶部标题栏，剩余宽度）
///   ─ DetailPanel（选中关卡详情，含敌人、掉落、体力、难度、开始/扫荡）
///   ─ BottomNav（底部五入口复用，高 150）
///
/// 资源条和底部五入口均通过 TownSharedChrome 复用，本页不自建。
/// 资源图片（地图背景、副本图标、关卡节点等）留白格占位，你去替换 Sprite。
/// </summary>
public class AdventureUI : MonoBehaviour, ITownPage
{
    public static AdventureUI Instance { get; private set; }
    public MainNavTab Tab => MainNavTab.Adventure;

    // ── 公开引用（预制体序列化后可在 Inspector 拖资源）──
    [Header("左侧副本类型按钮（顺序：主线/精英/迷宫/每日/活动）")]
    public Button[] modeButtons = new Button[5];
    public Image[]  modeButtonIcons = new Image[5];
    public Text[]   modeButtonLabels = new Text[5];

    [Header("地图区")]
    public Text    chapterTitle;       // "第一章  边境小镇"
    public Button  prevChapterBtn;
    public Button  nextChapterBtn;
    public Image   mapBg;             // 替换：章节地图背景图
    public Transform stageNodeContainer; // 关卡节点父节点
    public Text    progressLabel;     // "24/24"
    public Image   progressFill;
    public Button  chapterRewardBtn;  // 章节奖励
    public Button  adventureLogBtn;   // 冒险日志

    [Header("关卡详情面板")]
    public Text    stageNameLabel;    // "1-3  哥布林营地"
    public Text    stageDescLabel;
    public Transform enemyIconContainer;
    public Transform dropIconContainer;
    public Text    staminaCostLabel;
    public Text    remainChancesLabel;
    public Button  addChancesBtn;

    [Header("难度按钮（普通/困难/噩梦/地狱）")]
    public Button[] difficultyButtons = new Button[4];
    public Text[]   difficultyLabels  = new Text[4];

    [Header("底部操作")]
    public Button startBtn;
    public Button sweepBtn;

    // ── 内部状态 ──
    static readonly string[] ModeNames  = { "主线冒险", "精英挑战", "迷宫探索", "每日副本", "活动副本" };
    static readonly string[] DiffNames  = { "普通", "困难", "噩梦", "地狱" };
    static readonly Color    ColNormal  = new Color(0.30f, 0.55f, 0.22f, 1f);
    static readonly Color    ColHard    = new Color(0.28f, 0.42f, 0.65f, 1f);
    static readonly Color    ColNight   = new Color(0.45f, 0.22f, 0.62f, 1f);
    static readonly Color    ColHell    = new Color(0.65f, 0.18f, 0.18f, 1f);

    int _selectedMode = 0;
    int _selectedDiff = 0;
    int _selectedStage = 0;      // 关卡节点索引
    List<Button> _stageNodeBtns = new List<Button>();

    const float TOP_H    = 120f;
    const float BOT_H    = 150f;
    const float LEFT_W   = 170f;
    const float DETAIL_H = 340f;

    bool _built;
    bool _preloaded;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ────────────────────────────────────────────────────
    // ITownPage
    // ────────────────────────────────────────────────────

    public void PreloadOnce()
    {
        if (_preloaded) return;

        if (!_built) Build();

        EnsureVisibleTransform();
        ConfigCanvas();
        GameFonts.ApplyToHierarchy(transform);
        WireClicks();

        _preloaded = true;
        gameObject.SetActive(false);
    }

    public void ShowPage()
    {
        if (!_preloaded) PreloadOnce();
        EnsureVisibleTransform();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        Transform hall = GuildHallUI.Instance != null
            ? GuildHallUI.Instance.transform
            : transform.root;
        TownSharedChrome.RaiseSharedChrome(hall);

        RefreshModeHighlight();
        RefreshDetailPanel();
    }

    public void HidePage()
    {
        gameObject.SetActive(false);
    }

    // ────────────────────────────────────────────────────
    // 构建 UI 树
    // ────────────────────────────────────────────────────

    void Build()
    {
        // ── 根 RectTransform ──
        var rt = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
        Stretch(rt);

        // ── 可选：半透明遮罩覆盖主大厅 ──
        var overlay = CreateImg(transform, "Overlay", new Color(0, 0, 0, 0.45f));
        Stretch(overlay.rectTransform);

        // ── 主体：左侧副本列表 + 右侧内容 ──
        // 内容区上下留出 TopBar 和 BottomNav 的空间
        float contentTop = -TOP_H;
        float contentBot = BOT_H;

        BuildLeftSidebar(contentTop, contentBot);
        BuildRightContent(contentTop, contentBot);

        _built = true;
    }

    // ── 左侧副本类型竖列 ──
    void BuildLeftSidebar(float top, float bot)
    {
        var go = new GameObject("LeftSidebar", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot     = new Vector2(0, 0.5f);
        rt.offsetMin = new Vector2(0, bot);
        rt.offsetMax = new Vector2(LEFT_W, top);

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.10f, 0.07f, 0.03f, 0.95f);

        float btnH = 100f;
        float gap   = 8f;
        float startY = -top - 20f;   // 从顶部偏移开始

        string[] iconPlaceholders = { "⚔", "★", "◎", "✦", "☆" };

        for (int i = 0; i < 5; i++)
        {
            int idx = i;
            var btn = CreateSidebarButton(go.transform,
                $"ModeBtn_{i}", ModeNames[i], iconPlaceholders[i],
                startY + i * (btnH + gap), btnH);

            if (modeButtons.Length > i) modeButtons[i] = btn;
            var label = btn.GetComponentInChildren<Text>();
            if (modeButtonLabels.Length > i) modeButtonLabels[i] = label;
        }
    }

    Button CreateSidebarButton(Transform parent, string name, string label,
        string iconChar, float yOffset, float h)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0, -yOffset);
        rt.sizeDelta = new Vector2(0, h);

        var img = go.GetComponent<Image>();
        img.color = new Color(0.18f, 0.13f, 0.06f, 1f);
        var btn = go.GetComponent<Button>();

        // icon 占位
        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Text));
        iconGo.transform.SetParent(go.transform, false);
        var iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0.5f, 0.62f);
        iconRt.anchorMax = new Vector2(0.5f, 0.62f);
        iconRt.sizeDelta = new Vector2(52, 52);
        iconRt.anchoredPosition = Vector2.zero;
        var iconTxt = iconGo.GetComponent<Text>();
        iconTxt.text      = iconChar;
        iconTxt.fontSize  = 28;
        iconTxt.alignment = TextAnchor.MiddleCenter;
        iconTxt.color     = Color.white;

        // label
        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelGo.transform.SetParent(go.transform, false);
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0, 0);
        labelRt.anchorMax = new Vector2(1, 0.45f);
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;
        var txt = labelGo.GetComponent<Text>();
        txt.text      = label;
        txt.fontSize  = 18;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color     = new Color(0.9f, 0.82f, 0.65f);
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow   = VerticalWrapMode.Overflow;

        return btn;
    }

    // ── 右侧内容区（地图 + 详情）──
    void BuildRightContent(float top, float bot)
    {
        var go = new GameObject("RightContent", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(LEFT_W, bot);
        rt.offsetMax = new Vector2(0, top);

        // ── 地图背景区 ──
        float mapH = 1280f - TOP_H - BOT_H - DETAIL_H - 16f;
        var mapRoot = new GameObject("MapRoot", typeof(RectTransform));
        mapRoot.transform.SetParent(go.transform, false);
        var mapRt = mapRoot.GetComponent<RectTransform>();
        mapRt.anchorMin = new Vector2(0, 1);
        mapRt.anchorMax = new Vector2(1, 1);
        mapRt.pivot     = new Vector2(0.5f, 1f);
        mapRt.anchoredPosition = Vector2.zero;
        mapRt.sizeDelta = new Vector2(0, mapH);

        // 地图背景图（占位：深棕色框，替换成 Sprite）
        var mapBgImg = CreateImg(mapRoot.transform, "MapBg",
            new Color(0.18f, 0.26f, 0.14f, 1f));
        Stretch(mapBgImg.rectTransform);
        mapBg = mapBgImg;

        // 章节标题栏
        BuildMapTitleBar(mapRoot.transform, mapH);

        // 节点容器
        var nodeContainer = new GameObject("StageNodes", typeof(RectTransform));
        nodeContainer.transform.SetParent(mapRoot.transform, false);
        var ncRt = nodeContainer.GetComponent<RectTransform>();
        ncRt.anchorMin = new Vector2(0, 0);
        ncRt.anchorMax = new Vector2(1, 1);
        ncRt.offsetMin = new Vector2(0, 50f);
        ncRt.offsetMax = new Vector2(0, -54f);
        stageNodeContainer = nodeContainer.transform;

        // 生成示例节点（1-1 到 1-7 + BOSS，布局参考截图）
        BuildStageNodes(nodeContainer.transform);

        // 底部进度条 + 按钮行
        BuildMapBottomBar(mapRoot.transform);

        // ── 关卡详情面板 ──
        BuildDetailPanel(go.transform, mapH);
    }

    void BuildMapTitleBar(Transform parent, float mapH)
    {
        var bar = new GameObject("TitleBar", typeof(RectTransform));
        bar.transform.SetParent(parent, false);
        var rt = bar.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0, 54f);

        var barBg = bar.AddComponent<Image>();
        barBg.color = new Color(0.10f, 0.06f, 0.02f, 0.88f);

        // ← 按钮
        prevChapterBtn = CreateIconBtn(bar.transform, "PrevBtn", "◀", new Vector2(30, -27), 44f);

        // 章节名
        var titleGo = new GameObject("ChapterTitle", typeof(RectTransform), typeof(Text));
        titleGo.transform.SetParent(bar.transform, false);
        var tRt = titleGo.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0.1f, 0);
        tRt.anchorMax = new Vector2(0.9f, 1);
        tRt.offsetMin = tRt.offsetMax = Vector2.zero;
        chapterTitle = titleGo.GetComponent<Text>();
        chapterTitle.text      = "第一章  边境小镇";
        chapterTitle.fontSize  = 26;
        chapterTitle.alignment = TextAnchor.MiddleCenter;
        chapterTitle.color     = new Color(1f, 0.92f, 0.7f);

        // → 按钮
        nextChapterBtn = CreateIconBtn(bar.transform, "NextBtn", "▶", new Vector2(-30, -27), 44f);
    }

    Button CreateIconBtn(Transform parent, string name, string icon,
        Vector2 anchoredPos, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        bool left = anchoredPos.x > 0;
        rt.anchorMin = new Vector2(left ? 0 : 1, 0.5f);
        rt.anchorMax = new Vector2(left ? 0 : 1, 0.5f);
        rt.pivot     = new Vector2(left ? 0 : 1, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(size, size);
        go.GetComponent<Image>().color = new Color(0.25f, 0.18f, 0.08f, 0.9f);
        var btn = go.GetComponent<Button>();

        var txtGo = new GameObject("Lbl", typeof(RectTransform), typeof(Text));
        txtGo.transform.SetParent(go.transform, false);
        Stretch(txtGo.GetComponent<RectTransform>());
        var t = txtGo.GetComponent<Text>();
        t.text = icon; t.fontSize = 22;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        return btn;
    }

    // 生成关卡节点（位置参考截图：散点布局，7个普通+1个Boss）
    void BuildStageNodes(Transform container)
    {
        // 相对于容器中心的偏移（归一化：容器宽550×高约480，可自由调整）
        // 参考截图顺序：1-1左上，1-2中上，1-3右上，BOSS中，1-4右中，1-5右下，1-6右下，1-6左，1-7左下
        var positions = new Vector2[]
        {
            new Vector2(-180,  140),  // 1-1
            new Vector2( -40,  175),  // 1-2
            new Vector2(  90,  185),  // 1-3（高亮）
            new Vector2(   0,   50),  // BOSS
            new Vector2( 160,   80),  // 1-4
            new Vector2( 145,  -40),  // 1-5
            new Vector2(  50, -130),  // 1-6 右
            new Vector2(-130, -130),  // 1-6 左（截图有两个1-6，一个应为1-7的占位）
            new Vector2(-175,  -45),  // 1-7
        };
        string[] labels = { "1-1","1-2","1-3","BOSS","1-4","1-5","1-6","1-6","1-7" };
        bool[]   isBoss = { false,false,false,true,false,false,false,false,false };
        int[]    stars  = { 3,3,4,4,3,3,3,3,3 };

        _stageNodeBtns.Clear();
        for (int i = 0; i < positions.Length; i++)
        {
            int idx = i;
            var node = BuildStageNode(container, labels[i], positions[i],
                stars[i], isBoss[i], i == 2);
            _stageNodeBtns.Add(node);
            node.onClick.AddListener(() => OnSelectStage(idx));
        }
        _selectedStage = 2; // 默认选 1-3
    }

    Button BuildStageNode(Transform parent, string label, Vector2 pos,
        int starCount, bool isBoss, bool selected)
    {
        float size = isBoss ? 80f : 64f;
        var go = new GameObject("Node_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(size, size);

        Color bg = isBoss
            ? new Color(0.55f, 0.10f, 0.08f, 1f)
            : selected
                ? new Color(0.80f, 0.65f, 0.15f, 1f)
                : new Color(0.22f, 0.36f, 0.22f, 1f);
        go.GetComponent<Image>().color = bg;
        var btn = go.GetComponent<Button>();

        // 关卡编号文字
        var numGo = new GameObject("Num", typeof(RectTransform), typeof(Text));
        numGo.transform.SetParent(go.transform, false);
        var nRt = numGo.GetComponent<RectTransform>();
        nRt.anchorMin = new Vector2(0, 0.35f);
        nRt.anchorMax = new Vector2(1, 1);
        nRt.offsetMin = nRt.offsetMax = Vector2.zero;
        var numTxt = numGo.GetComponent<Text>();
        numTxt.text      = label;
        numTxt.fontSize  = isBoss ? 16 : 18;
        numTxt.alignment = TextAnchor.MiddleCenter;
        numTxt.color     = Color.white;
        numTxt.horizontalOverflow = HorizontalWrapMode.Overflow;

        // 星星行（最多4颗，放在节点下方）
        if (starCount > 0 && !isBoss)
        {
            var starBar = new GameObject("Stars", typeof(RectTransform), typeof(Text));
            starBar.transform.SetParent(go.transform, false);
            var sRt = starBar.GetComponent<RectTransform>();
            sRt.anchorMin = new Vector2(0, 0);
            sRt.anchorMax = new Vector2(1, 0.38f);
            sRt.offsetMin = sRt.offsetMax = Vector2.zero;
            var sTxt = starBar.GetComponent<Text>();
            sTxt.text      = new string('★', starCount);
            sTxt.fontSize  = 12;
            sTxt.alignment = TextAnchor.MiddleCenter;
            sTxt.color     = new Color(1f, 0.85f, 0.2f);
        }

        return btn;
    }

    void BuildMapBottomBar(Transform parent)
    {
        var bar = new GameObject("MapBottomBar", typeof(RectTransform));
        bar.transform.SetParent(parent, false);
        var rt = bar.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot     = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0, 52f);

        var barBg = bar.AddComponent<Image>();
        barBg.color = new Color(0.08f, 0.06f, 0.02f, 0.85f);

        // ★ 进度 "24/24"
        progressLabel = CreateTextGO(bar.transform, "Progress", "24/24",
            20, TextAnchor.MiddleLeft, new Vector2(10, 0), new Vector2(80, 36));
        progressLabel.color = new Color(1f, 0.85f, 0.2f);

        // 进度条
        var fillBg = CreateImg(bar.transform, "ProgressBarBg",
            new Color(0.15f, 0.12f, 0.05f, 1f));
        var fBgRt = fillBg.rectTransform;
        fBgRt.anchorMin = new Vector2(0, 0.5f);
        fBgRt.anchorMax = new Vector2(0, 0.5f);
        fBgRt.pivot     = new Vector2(0, 0.5f);
        fBgRt.anchoredPosition = new Vector2(96, 0);
        fBgRt.sizeDelta = new Vector2(160, 14);

        var fillImg = CreateImg(fillBg.transform, "Fill",
            new Color(0.9f, 0.75f, 0.2f, 1f));
        Stretch(fillImg.rectTransform);
        progressFill = fillImg;

        // 章节奖励 / 冒险日志按钮
        chapterRewardBtn = CreateSmallBtn(bar.transform, "ChapterReward", "章节奖励",
            new Vector2(-140, 0));
        adventureLogBtn  = CreateSmallBtn(bar.transform, "AdventureLog",  "冒险日志",
            new Vector2(-30,  0));
    }

    Button CreateSmallBtn(Transform parent, string name, string label, Vector2 anchoredPos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 0.5f);
        rt.anchorMax = new Vector2(1, 0.5f);
        rt.pivot     = new Vector2(1, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(100, 40);
        go.GetComponent<Image>().color = new Color(0.28f, 0.22f, 0.10f, 1f);

        var t = CreateTextGO(go.transform, "Lbl", label, 17, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
        t.color = new Color(0.9f, 0.82f, 0.6f);
        var tRt = t.rectTransform;
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = tRt.offsetMax = Vector2.zero;

        return go.GetComponent<Button>();
    }

    // ── 关卡详情面板 ──
    void BuildDetailPanel(Transform parent, float mapH)
    {
        var panel = new GameObject("DetailPanel", typeof(RectTransform));
        panel.transform.SetParent(parent, false);
        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot     = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0, DETAIL_H);

        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0.18f, 0.13f, 0.06f, 0.98f);

        // ── 上半：关卡名 + 描述 + 敌人图标 ──
        float y = -14f;

        stageNameLabel = CreateTextGO(panel.transform, "StageName",
            "1-3  哥布林营地", 24, TextAnchor.MiddleLeft,
            new Vector2(16, y - 12), new Vector2(400, 32));
        stageNameLabel.color = new Color(1f, 0.92f, 0.7f);

        stageDescLabel = CreateTextGO(panel.transform, "StageDesc",
            "哥布林们在山洞前搭建了营地，\n小心他们的埋伏和投掷的石块。",
            18, TextAnchor.UpperLeft,
            new Vector2(16, y - 52), new Vector2(280, 56));
        stageDescLabel.color = new Color(0.85f, 0.78f, 0.65f);

        // 敌人图标行（占位：4个）
        var enemyCont = new GameObject("EnemyIcons", typeof(RectTransform));
        enemyCont.transform.SetParent(panel.transform, false);
        var eRt = enemyCont.GetComponent<RectTransform>();
        eRt.anchorMin = new Vector2(0, 1);
        eRt.anchorMax = new Vector2(0, 1);
        eRt.pivot     = new Vector2(0, 1);
        eRt.anchoredPosition = new Vector2(16, y - 112);
        eRt.sizeDelta = new Vector2(220, 48);
        enemyIconContainer = enemyCont.transform;
        for (int i = 0; i < 4; i++) BuildIconSlot(enemyCont.transform, i, 52f);

        // 可能掉落标题
        var dropTitle = CreateTextGO(panel.transform, "DropTitle",
            "可能掉落", 18, TextAnchor.MiddleLeft,
            new Vector2(310, y - 14), new Vector2(100, 28));
        dropTitle.color = new Color(0.85f, 0.78f, 0.65f);

        // 掉落图标（占位：4个）
        var dropCont = new GameObject("DropIcons", typeof(RectTransform));
        dropCont.transform.SetParent(panel.transform, false);
        var dRt = dropCont.GetComponent<RectTransform>();
        dRt.anchorMin = new Vector2(1, 1);
        dRt.anchorMax = new Vector2(1, 1);
        dRt.pivot     = new Vector2(1, 1);
        dRt.anchoredPosition = new Vector2(-16, y - 44);
        dRt.sizeDelta = new Vector2(220, 48);
        dropIconContainer = dropCont.transform;
        for (int i = 0; i < 4; i++) BuildIconSlot(dropCont.transform, i, 52f);

        // ── 中：体力 / 次数 ──
        float midY = y - 172;
        staminaCostLabel = CreateTextGO(panel.transform, "StaminaCost",
            "⚡ 10", 20, TextAnchor.MiddleLeft,
            new Vector2(16, midY), new Vector2(150, 30));
        staminaCostLabel.color = new Color(0.9f, 0.85f, 0.3f);

        remainChancesLabel = CreateTextGO(panel.transform, "Chances",
            "3/3", 20, TextAnchor.MiddleLeft,
            new Vector2(230, midY), new Vector2(80, 30));
        remainChancesLabel.color = Color.white;

        addChancesBtn = CreateSmallBtn2(panel.transform, "AddChances", "+",
            new Vector2(310, midY - 14), new Vector2(36, 30));

        // ── 难度选择行 ──
        float diffY = midY - 46;
        Color[] diffCols = { ColNormal, ColHard, ColNight, ColHell };
        float[] diffX    = { 0, 1f/4, 2f/4, 3f/4 };
        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            var dBtn = BuildDiffBtn(panel.transform, DiffNames[i], diffCols[i], i, diffY);
            difficultyButtons[i] = dBtn;
            var lbls = dBtn.GetComponentsInChildren<Text>(true);
            if (lbls.Length > 0) difficultyLabels[i] = lbls[0];
            dBtn.onClick.AddListener(() => OnSelectDiff(idx));
        }

        // ── 底部：开始冒险 + 扫荡 ──
        float btnY = -DETAIL_H + 60f;
        startBtn = BuildWideBtn(panel.transform, "StartBtn", "⚔  开始冒险",
            new Vector2(0, btnY + 24), new Vector2(-240, 56), ColNormal);
        sweepBtn = BuildWideBtn(panel.transform, "SweepBtn", "▶▶  扫荡",
            new Vector2(0, btnY + 24), new Vector2(110, 56), ColHard);
    }

    void BuildIconSlot(Transform parent, int idx, float size)
    {
        var go = new GameObject($"Icon{idx}", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
        rt.pivot     = new Vector2(0, 0.5f);
        rt.anchoredPosition = new Vector2(idx * (size + 6), 0);
        rt.sizeDelta = new Vector2(size, size);
        go.GetComponent<Image>().color = new Color(0.28f, 0.22f, 0.10f, 1f);
    }

    Button BuildDiffBtn(Transform parent, string label, Color col, int idx, float y)
    {
        var go = new GameObject("Diff_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(idx / 4f,          0);
        rt.anchorMax = new Vector2((idx + 1) / 4f,    0);
        rt.pivot     = new Vector2(0.5f, 0);
        rt.anchoredPosition = new Vector2(0, -y);
        rt.sizeDelta = new Vector2(-8, 42);
        go.GetComponent<Image>().color = col;

        var t = CreateTextGO(go.transform, "Lbl", label, 20, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.zero);
        t.color = Color.white;
        var tRt = t.rectTransform;
        tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
        tRt.offsetMin = tRt.offsetMax = Vector2.zero;

        return go.GetComponent<Button>();
    }

    Button BuildWideBtn(Transform parent, string name, string label,
        Vector2 anchoredPos, Vector2 sizeDelta, Color col)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        if (name == "StartBtn")
        {
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0.6f, 0);
            rt.pivot     = new Vector2(0, 0);
            rt.anchoredPosition = new Vector2(8, 12);
            rt.sizeDelta = new Vector2(-16, 56);
        }
        else
        {
            rt.anchorMin = new Vector2(0.6f, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot     = new Vector2(0, 0);
            rt.anchoredPosition = new Vector2(4, 12);
            rt.sizeDelta = new Vector2(-12, 56);
        }
        go.GetComponent<Image>().color = col;

        var t = CreateTextGO(go.transform, "Lbl", label, 24, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.zero);
        t.color = Color.white;
        var tRt = t.rectTransform;
        tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
        tRt.offsetMin = tRt.offsetMax = Vector2.zero;

        return go.GetComponent<Button>();
    }

    Button CreateSmallBtn2(Transform parent, string name, string label,
        Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot     = new Vector2(0, 1);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        go.GetComponent<Image>().color = new Color(0.85f, 0.65f, 0.15f, 1f);

        var t = CreateTextGO(go.transform, "Lbl", label, 22, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.zero);
        t.color = Color.white;
        var tRt = t.rectTransform;
        tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
        tRt.offsetMin = tRt.offsetMax = Vector2.zero;
        return go.GetComponent<Button>();
    }

    // ────────────────────────────────────────────────────
    // 交互逻辑
    // ────────────────────────────────────────────────────

    void WireClicks()
    {
        for (int i = 0; i < modeButtons.Length; i++)
        {
            if (modeButtons[i] == null) continue;
            int idx = i;
            modeButtons[i].onClick.AddListener(() => OnSelectMode(idx));
        }
        prevChapterBtn?.onClick.AddListener(OnPrevChapter);
        nextChapterBtn?.onClick.AddListener(OnNextChapter);
        startBtn?.onClick.AddListener(OnStartBattle);
        sweepBtn?.onClick.AddListener(OnSweep);
        chapterRewardBtn?.onClick.AddListener(OnChapterReward);
        adventureLogBtn?.onClick.AddListener(OnAdventureLog);
        addChancesBtn?.onClick.AddListener(OnAddChances);
    }

    void OnSelectMode(int idx)
    {
        _selectedMode = idx;
        RefreshModeHighlight();
        if (idx != 0)
            UIManager.Instance?.ShowToast($"{ModeNames[idx]}（即将开放）");
    }

    void OnSelectStage(int idx)
    {
        _selectedStage = idx;
        RefreshDetailPanel();
    }

    void OnSelectDiff(int idx)
    {
        _selectedDiff = idx;
        RefreshDiffHighlight();
        if (idx > 0)
            UIManager.Instance?.ShowToast($"{DiffNames[idx]}（即将开放）");
    }

    void OnPrevChapter() => UIManager.Instance?.ShowToast("已是第一章");
    void OnNextChapter() => UIManager.Instance?.ShowToast("后续章节开发中");
    void OnChapterReward() => UIManager.Instance?.ShowToast("章节奖励（即将开放）");
    void OnAdventureLog()  => UIManager.Instance?.ShowToast("冒险日志（即将开放）");
    void OnAddChances()    => UIManager.Instance?.ShowToast("购买次数（即将开放）");

    void OnStartBattle()
    {
        if (GameSceneManager.Instance == null) return;
        HidePage();
        GameSceneManager.Instance.LoadBattleScene();
    }

    void OnSweep() => UIManager.Instance?.ShowToast("扫荡（即将开放）");

    // ────────────────────────────────────────────────────
    // 刷新状态
    // ────────────────────────────────────────────────────

    void RefreshModeHighlight()
    {
        for (int i = 0; i < modeButtons.Length; i++)
        {
            if (modeButtons[i] == null) continue;
            var img = modeButtons[i].GetComponent<Image>();
            if (img != null)
                img.color = i == _selectedMode
                    ? new Color(0.45f, 0.32f, 0.10f, 1f)
                    : new Color(0.18f, 0.13f, 0.06f, 1f);
        }
    }

    void RefreshDiffHighlight()
    {
        Color[] cols = { ColNormal, ColHard, ColNight, ColHell };
        for (int i = 0; i < difficultyButtons.Length; i++)
        {
            if (difficultyButtons[i] == null) continue;
            var img = difficultyButtons[i].GetComponent<Image>();
            if (img == null) continue;
            Color c = cols[i];
            img.color = i == _selectedDiff ? c : new Color(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f, 1f);
        }
    }

    void RefreshDetailPanel()
    {
        RefreshDiffHighlight();
        // TODO：从 ChapterManager/StageData 读真实数据
        // 当前只做 UI 占位演示
    }

    // ────────────────────────────────────────────────────
    // 工具方法
    // ────────────────────────────────────────────────────

    void EnsureVisibleTransform()
    {
        var rt = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
        if (rt.localScale == Vector3.zero) rt.localScale = Vector3.one;
    }

    void ConfigCanvas()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) return;
        UICanvasSetup.Apply(canvas);
    }

    static Image CreateImg(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    static void Stretch(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    Text CreateTextGO(Transform parent, string name, string content,
        int size, TextAnchor align, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot     = new Vector2(0, 1);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        var t = go.GetComponent<Text>();
        t.text      = content;
        t.fontSize  = size;
        t.alignment = align;
        t.color     = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow   = VerticalWrapMode.Overflow;
        return t;
    }
}
