using System;
using System.Collections;
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

    /// <summary>点「开始冒险」后开战用的章节；0 表示用存档最新解锁章</summary>
    public static int PendingBattleChapter;
    public static int PendingBattleDifficulty;
    public static bool PendingGoldDungeon;

    [Header("可替换资源")]
    public Sprite mapBackgroundSprite;
    public Sprite[] chapterBackgrounds = new Sprite[8]; // 可选覆盖 MapBg；空则只切换 StageNodes/Node_1..8
    public Sprite[] modeButtonSpriteIcons = new Sprite[5];
    public Sprite[] enemySprites = new Sprite[4];
    public Sprite[] dropSprites = new Sprite[4];

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
    static readonly string[] ModeNames  = { "主线冒险", "每日副本", "迷宫探索", "BOSS挑战", "活动副本" };
    static readonly string[] DiffNames  = { "普通", "困难", "噩梦", "地狱" };
    static readonly Color    ColNormal  = new Color(0.30f, 0.55f, 0.22f, 1f);
    static readonly Color    ColHard    = new Color(0.28f, 0.42f, 0.65f, 1f);
    static readonly Color    ColNight   = new Color(0.45f, 0.22f, 0.62f, 1f);
    static readonly Color    ColHell    = new Color(0.65f, 0.18f, 0.18f, 1f);

    int _selectedMode = 0;
    int _selectedDiff = 0;
    int _selectedChapter = 1;
    List<Button> _stageNodeBtns = new List<Button>();
    readonly List<MonsterConfig> _previewMonsters = new List<MonsterConfig>();
    Text _bossTag;
    GameObject _tipRoot;
    Text _tipTitle;
    Text _tipBody;
    Text _floatToast;
    RectTransform _floatToastRt;
    Coroutine _floatToastCo;
    Sprite _goldDropSprite;

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

        // 预制体已有完整树时只绑定，不再重建
        if (transform.Find("LeftSidebar") != null || transform.Find("RightContent") != null)
        {
            AutoBindFromHierarchy();
            _built = true;
        }
        else if (!_built)
            Build();

        EnsureVisibleTransform();
        ConfigureHostCanvasOnce();
        GameFonts.ApplyToHierarchy(transform);
        WireClicks();

        _preloaded = true;
        gameObject.SetActive(false);
    }

    /// <summary>编辑器生成预制体时调用：建树并套字体，不隐藏、不绑点击</summary>
    public void BuildHierarchyForPrefab()
    {
        if (!_built) Build();
        EnsureVisibleTransform();
        GameFonts.ApplyToHierarchy(transform);
    }

    public void ShowPage()
    {
        if (!_preloaded) PreloadOnce();
        EnsureVisibleTransform();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        TavernUI.SetGuildHallOverlayMode(true);

        Transform hall = GuildHallUI.Instance != null
            ? GuildHallUI.Instance.transform
            : transform.root;
        TownSharedChrome.RaiseSharedChrome(hall);
        EnsureStandaloneChrome(hall);

        int max = GetMaxUnlockedChapter();
        if (_selectedChapter < 1 || _selectedChapter > max)
            _selectedChapter = max;
        HideUnfinishedEntries();
        RefreshAll();
    }

    public void HidePage()
    {
        HideTip();
        HideFloatToast();
        if (!gameObject.activeSelf) return;
        gameObject.SetActive(false);
        TavernUI.SetGuildHallOverlayMode(false);
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

            // 可选：如果你在 Inspector 填了 modeButtonSpriteIcons，则替换 icon 字符为图片
            if (modeButtonSpriteIcons != null && modeButtonSpriteIcons.Length > i && modeButtonSpriteIcons[i] != null)
            {
                var iconImg = btn.transform.Find("Icon")?.GetComponent<Image>();
                if (iconImg != null)
                {
                    iconImg.sprite = modeButtonSpriteIcons[i];
                    iconImg.color = Color.white;
                    var iconTxt = btn.transform.Find("IconText")?.GetComponent<Text>();
                    if (iconTxt != null) iconTxt.gameObject.SetActive(false);
                }
            }
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

        // icon 占位（Image + Text 两套：你替换 Sprite 时只保留 Image）
        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(go.transform, false);
        var iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0.5f, 0.62f);
        iconRt.anchorMax = new Vector2(0.5f, 0.62f);
        iconRt.sizeDelta = new Vector2(52, 52);
        iconRt.anchoredPosition = Vector2.zero;
        iconGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);

        // 默认 icon 字符（你未填 sprite 时可见）
        var iconTxtGo = new GameObject("IconText", typeof(RectTransform), typeof(Text));
        iconTxtGo.transform.SetParent(go.transform, false);
        var iconTxtRt = iconTxtGo.GetComponent<RectTransform>();
        iconTxtRt.anchorMin = new Vector2(0.5f, 0.62f);
        iconTxtRt.anchorMax = new Vector2(0.5f, 0.62f);
        iconTxtRt.sizeDelta = new Vector2(52, 52);
        iconTxtRt.anchoredPosition = Vector2.zero;
        var iconTxt = iconTxtGo.GetComponent<Text>();
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
        if (mapBackgroundSprite != null)
        {
            mapBg.sprite = mapBackgroundSprite;
            mapBg.color = Color.white;
        }

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
            int chapter = Mathf.Clamp(idx + 1, 1, 8);
            node.onClick.AddListener(() => OnSelectChapter(chapter));
        }
        _selectedChapter = 1;
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
        // 可选：用你替换的敌人 Sprite 填充占位格
        if (enemySprites != null)
        {
            for (int i = 0; i < 4 && i < enemySprites.Length; i++)
            {
                var slot = enemyCont.transform.Find($"Icon{i}")?.GetComponent<Image>();
                if (slot != null && enemySprites[i] != null)
                {
                    slot.sprite = enemySprites[i];
                    slot.color = Color.white;
                }
            }
        }

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
        // 可选：用你替换的掉落 Sprite 填充占位格
        if (dropSprites != null)
        {
            for (int i = 0; i < 4 && i < dropSprites.Length; i++)
            {
                var slot = dropCont.transform.Find($"Icon{i}")?.GetComponent<Image>();
                if (slot != null && dropSprites[i] != null)
                {
                    slot.sprite = dropSprites[i];
                    slot.color = Color.white;
                }
            }
        }

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
        var img = go.GetComponent<Image>();
        img.color = new Color(0.28f, 0.22f, 0.10f, 1f);
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
            modeButtons[i].onClick.RemoveAllListeners();
            modeButtons[i].onClick.AddListener(() => OnSelectMode(idx));
        }
        for (int i = 0; i < difficultyButtons.Length; i++)
        {
            if (difficultyButtons[i] == null) continue;
            int idx = i;
            difficultyButtons[i].onClick.RemoveAllListeners();
            difficultyButtons[i].onClick.AddListener(() => OnSelectDiff(idx));
        }
        prevChapterBtn?.onClick.RemoveAllListeners();
        nextChapterBtn?.onClick.RemoveAllListeners();
        startBtn?.onClick.RemoveAllListeners();
        sweepBtn?.onClick.RemoveAllListeners();
        chapterRewardBtn?.onClick.RemoveAllListeners();
        adventureLogBtn?.onClick.RemoveAllListeners();
        addChancesBtn?.onClick.RemoveAllListeners();

        prevChapterBtn?.onClick.AddListener(OnPrevChapter);
        nextChapterBtn?.onClick.AddListener(OnNextChapter);
        startBtn?.onClick.AddListener(OnStartBattle);
        // 扫荡 / 章节奖励未做：隐藏入口
        if (sweepBtn != null) sweepBtn.gameObject.SetActive(false);
        if (chapterRewardBtn != null) chapterRewardBtn.gameObject.SetActive(false);
        adventureLogBtn?.onClick.AddListener(OnAdventureLog);
        addChancesBtn?.onClick.AddListener(OnAddChances);

        HideUnfinishedEntries();
        BindMapLayers();
        WireEnemyIconClicks();
        WireBoxReward();
    }

    /// <summary>隐藏尚未实现的玩法入口，避免误点「即将开放」。</summary>
    void HideUnfinishedEntries()
    {
        // 模式：仅保留主线(0)；活动副本及每日/迷宫/BOSS挑战均隐藏
        for (int i = 0; i < modeButtons.Length; i++)
        {
            if (modeButtons[i] == null) continue;
            modeButtons[i].gameObject.SetActive(i == 0);
        }
        _selectedMode = 0;
        // 难度：隐藏「地狱」(index 3)
        if (difficultyButtons != null && difficultyButtons.Length > 3 && difficultyButtons[3] != null)
            difficultyButtons[3].gameObject.SetActive(false);
        if (addChancesBtn != null)
            addChancesBtn.gameObject.SetActive(false);
    }

    /// <summary>StageNodes 下是整张章节地图（Node_1..8），不是关卡图标。关掉 Button 避免点按变色。</summary>
    void BindMapLayers()
    {
        _stageNodeBtns.Clear();
        if (stageNodeContainer == null) return;
        for (int i = 0; i < stageNodeContainer.childCount; i++)
        {
            var t = stageNodeContainer.GetChild(i);
            var btn = t.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.enabled = false;
                btn.transition = Selectable.Transition.None;
            }
            var img = t.GetComponent<Image>();
            if (img != null) img.raycastTarget = false;
        }
    }

    void WireEnemyIconClicks()
    {
        if (enemyIconContainer == null) return;
        for (int i = 0; i < enemyIconContainer.childCount; i++)
        {
            var slot = enemyIconContainer.GetChild(i);
            if (slot.name == "BOSS" || slot.name == "direnxinxi") continue;
            var btn = slot.GetComponent<Button>();
            if (btn == null) btn = slot.gameObject.AddComponent<Button>();
            int idx = ParseIconIndex(slot.name, i);
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnClickEnemyIcon(idx));
        }
    }

    void WireBoxReward()
    {
        Transform mapBottom = null;
        var right = transform.Find("RightContent");
        var mapRoot = right != null ? right.Find("MapRoot") : null;
        if (mapRoot != null) mapBottom = mapRoot.Find("MapBottomBar");
        if (mapBottom == null) return;
        var box = mapBottom.Find("boxbg") ?? mapBottom.Find("boxbg_kelingqu");
        if (box == null) return;
        var btn = box.GetComponent<Button>() ?? box.gameObject.AddComponent<Button>();
        if (box.GetComponent<Image>() != null) btn.targetGraphic = box.GetComponent<Image>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnChapterReward);
    }

    static int ParseIconIndex(string name, int fallback)
    {
        if (string.IsNullOrEmpty(name)) return fallback;
        for (int i = name.Length - 1; i >= 0; i--)
        {
            if (char.IsDigit(name[i]))
            {
                int end = i;
                while (i >= 0 && char.IsDigit(name[i])) i--;
                if (int.TryParse(name.Substring(i + 1, end - i), out int n))
                    return n;
                break;
            }
        }
        return fallback;
    }

    void OnSelectMode(int idx)
    {
        _selectedMode = idx;
        if (!IsDiffUnlocked(_selectedDiff))
            _selectedDiff = 0;
        HideTip();
        if (!IsModePlayable(idx))
            Toast($"{ModeLabel(idx)}即将开放");
        RefreshAll();
    }

    void OnSelectChapter(int chapter)
    {
        int max = GetMaxUnlockedChapter();
        if (chapter < 1 || chapter > 8) return;
        if (chapter > max)
        {
            Toast("通关上一章后开启");
            return;
        }
        _selectedChapter = chapter;
        RefreshAll();
    }

    void OnSelectDiff(int idx)
    {
        if (!IsModePlayable(_selectedMode))
        {
            Toast($"{ModeLabel(_selectedMode)}即将开放");
            return;
        }
        if (!IsDiffUnlocked(idx))
        {
            int need = idx >= 2 ? GameConfig.DIFF_NIGHTMARE_NEED_CLEARS : GameConfig.DIFF_HARD_NEED_CLEARS;
            Toast($"通关第{need}章后开启{DiffLabel(idx)}");
            return;
        }
        _selectedDiff = idx;
        RefreshDiffHighlight();
        RefreshDetailPanel();
    }

    void OnPrevChapter()
    {
        if (_selectedChapter <= 1)
        {
            Toast("已是第一章");
            return;
        }
        _selectedChapter--;
        RefreshAll();
    }

    void OnNextChapter()
    {
        int max = GetMaxUnlockedChapter();
        if (_selectedChapter >= max)
        {
            Toast(_selectedChapter >= 8 ? "已是最后一章" : "通关本章后开启下一章");
            return;
        }
        _selectedChapter++;
        RefreshAll();
    }

    void OnChapterReward() => Toast("章节奖励（即将开放）");
    void OnAdventureLog()
    {
        var hub = TownHubController.Instance;
        if (hub != null) hub.OpenAdventureLog();
        else Toast("请从底栏打开冒险日志");
    }
    void OnAddChances()    => Toast("主线不限次数");

    void OnStartBattle()
    {
        if (!IsModePlayable(_selectedMode))
        {
            Toast($"{ModeLabel(_selectedMode)}即将开放");
            return;
        }
        if (!IsDiffUnlocked(_selectedDiff))
        {
            int need = _selectedDiff >= 2 ? GameConfig.DIFF_NIGHTMARE_NEED_CLEARS : GameConfig.DIFF_HARD_NEED_CLEARS;
            Toast($"通关第{need}章后开启{DiffLabel(_selectedDiff)}");
            return;
        }

        if (StoryProgress.TutorialDone && !StoryProgress.Chapter1IntroDone && _selectedChapter <= 1)
        {
            Chapter1Story.PlayHallIntro(TryEnterBattle);
            return;
        }

        TryEnterBattle();
    }

    void TryEnterBattle()
    {
        // 丢弃半残战斗存档，避免进战无怪 / 脏 Prefs
        if (BattleStateSaver.Instance != null && BattleStateSaver.Instance.HasSavedBattle())
            BattleStateSaver.Instance.ClearBattleState();

        if (!StaminaSystem.TrySpendForAdventure())
        {
            Toast("体力不足");
            return;
        }

        PendingBattleChapter = _selectedChapter;
        PendingBattleDifficulty = _selectedDiff;
        PendingGoldDungeon = IsActivityMode(_selectedMode);
        ChapterManager.Instance?.SetChapter(_selectedChapter);
        HidePage();
        int legacyCount = SaveSystem.Instance?.Data?.legacyEquipPool?.Count ?? 0;
        if (legacyCount > 0)
            Toast($"遗产池 {legacyCount} 件 → 开战前三选一");
        else
            Toast("无遗产：开战前将提供基础装备三选一");
        GameSceneManager.Instance?.LoadBattleScene();
    }

    void OnSweep() => Toast("扫荡（即将开放）");

    void OnClickEnemyIcon(int idx)
    {
        if (idx < 0 || idx >= _previewMonsters.Count)
        {
            HideTip();
            return;
        }
        ShowMonsterTip(_previewMonsters[idx]);
    }

    // ────────────────────────────────────────────────────
    // 刷新
    // ────────────────────────────────────────────────────

    void RefreshAll()
    {
        HideTip();
        RefreshModeHighlight();
        RefreshDiffHighlight();
        RefreshChapterChrome();
        RefreshDetailPanel();
        RefreshContentLock();
    }

    void RefreshModeHighlight()
    {
        for (int i = 0; i < modeButtons.Length; i++)
        {
            if (modeButtons[i] == null) continue;
            SetChildSelected(modeButtons[i].transform, i == _selectedMode);
        }
    }

    void RefreshDiffHighlight()
    {
        bool playable = IsModePlayable(_selectedMode);
        for (int i = 0; i < difficultyButtons.Length; i++)
        {
            if (difficultyButtons[i] == null) continue;
            bool show = i < 3; // 只留普通/困难/噩梦
            difficultyButtons[i].gameObject.SetActive(show);
            if (!show) continue;
            bool unlocked = playable && IsDiffUnlocked(i);
            SetChildSelected(difficultyButtons[i].transform, playable && i == _selectedDiff && unlocked);
            SetGraphicDim(difficultyButtons[i].transform, !unlocked);
        }
    }

    static void SetChildSelected(Transform btn, bool on)
    {
        var sel = btn.Find("选中");
        if (sel == null) return;
        sel.gameObject.SetActive(on);
    }

    void RefreshChapterChrome()
    {
        if (chapterTitle != null)
            chapterTitle.text = GameConfig.GetChapterTitleText(_selectedChapter);

        if (prevChapterBtn != null)
            prevChapterBtn.interactable = _selectedChapter > 1;
        if (nextChapterBtn != null)
            nextChapterBtn.interactable = _selectedChapter < 8;

        // MapBg 是底框，不换图。章节地图在 StageNodes/Node_1..8，只显示当前章那一张。
        Sprite overrideBg = GetChapterBackground(_selectedChapter);
        if (overrideBg != null && mapBg != null)
            mapBg.sprite = overrideBg;

        RefreshMapLayers();
    }

    void RefreshMapLayers()
    {
        if (stageNodeContainer == null) return;
        for (int i = 0; i < stageNodeContainer.childCount; i++)
        {
            var child = stageNodeContainer.GetChild(i);
            int ch = ParseNodeChapter(child.name, i + 1);
            child.gameObject.SetActive(ch == _selectedChapter);
        }
    }

    static int ParseNodeChapter(string name, int fallback)
    {
        if (string.IsNullOrEmpty(name)) return fallback;
        int us = name.LastIndexOf('_');
        if (us >= 0 && us + 1 < name.Length && int.TryParse(name.Substring(us + 1), out int n) && n >= 1 && n <= 8)
            return n;
        if (int.TryParse(name, out n) && n >= 1 && n <= 8)
            return n;
        return fallback;
    }

        Sprite GetChapterBackground(int chapter)
        {
            int idx = chapter - 1;
            if (chapterBackgrounds != null && idx >= 0 && idx < chapterBackgrounds.Length && chapterBackgrounds[idx] != null)
                return chapterBackgrounds[idx];
            // 统一从 Resources/UI/Adventure 尝试加载 chapter_1 … chapter_8
            return UiKeyedBackgrounds.Load(UiKeyedBackgrounds.AdventurePages, "chapter_" + chapter)
                   ?? UiKeyedBackgrounds.Load(UiKeyedBackgrounds.AdventurePages, GameConfig.GetChapterMapName(chapter));
        }

    void RefreshDetailPanel()
    {
        bool main = _selectedMode == 0;
        bool activity = IsActivityMode(_selectedMode);
        bool playable = main || activity;
        string title = GameConfig.GetChapterTitleText(_selectedChapter);
        if (stageNameLabel != null)
        {
            if (activity) stageNameLabel.text = "金币副本";
            else if (main) stageNameLabel.text = title;
            else stageNameLabel.text = ModeLabel(_selectedMode);
        }
        if (stageDescLabel != null)
        {
            if (activity)
            {
                int gold = GameConfig.GetGoldDungeonClearGold(_selectedChapter, _selectedDiff);
                stageDescLabel.text = $"怪物只掉金币。通关获得 {gold} 金币。困难需通关第{GameConfig.DIFF_HARD_NEED_CLEARS}章，噩梦需通关第{GameConfig.DIFF_NIGHTMARE_NEED_CLEARS}章。";
            }
            else if (main)
                stageDescLabel.text = GetChapterIntro(_selectedChapter);
            else
                stageDescLabel.text = $"{ModeLabel(_selectedMode)}即将开放。";
        }

        if (staminaCostLabel != null)
        {
            string cost = StaminaSystem.ADVENTURE_COST.ToString();
            string t = staminaCostLabel.text;
            if (!string.IsNullOrEmpty(t) && t.IndexOf("体力", System.StringComparison.Ordinal) >= 0)
            {
                var sb = new System.Text.StringBuilder();
                bool replaced = false;
                for (int i = 0; i < t.Length; i++)
                {
                    if (char.IsDigit(t[i]))
                    {
                        if (!replaced) { sb.Append(cost); replaced = true; }
                        while (i + 1 < t.Length && char.IsDigit(t[i + 1])) i++;
                    }
                    else sb.Append(t[i]);
                }
                staminaCostLabel.text = replaced ? sb.ToString() : ("消耗体力  " + cost);
            }
            else
                staminaCostLabel.text = "消耗体力  " + cost;
        }

        if (remainChancesLabel != null)
            remainChancesLabel.text = playable ? "—" : "0";

        RefreshEnemyIcons();
        RefreshDropIcons();
    }

    void RefreshEnemyIcons()
    {
        _previewMonsters.Clear();
        if (enemyIconContainer == null) return;

        if (_bossTag == null)
        {
            var bt = enemyIconContainer.Find("BOSS");
            if (bt != null) _bossTag = bt.GetComponent<Text>();
        }

        var slots = new List<Transform>();
        for (int i = 0; i < enemyIconContainer.childCount; i++)
        {
            var c = enemyIconContainer.GetChild(i);
            if (c.name == "BOSS" || c.GetComponent<Text>() != null && c.name.IndexOf("Icon", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            slots.Add(c);
        }

        List<MonsterConfig> all = null;
        if (IsModePlayable(_selectedMode) && ConfigManager.Instance != null)
            all = ConfigManager.Instance.GetChapterPreviewMonsters(_selectedChapter);
        if (all == null) all = new List<MonsterConfig>();

        int showCount = Mathf.Min(slots.Count, all.Count);
        for (int i = 0; i < showCount; i++)
            _previewMonsters.Add(all[i]);

        Transform bossSlot = null;
        for (int i = 0; i < slots.Count; i++)
        {
            bool on = i < _previewMonsters.Count;
            slots[i].gameObject.SetActive(on);
            if (!on) continue;
            var cfg = _previewMonsters[i];
            var portrait = FindPortraitImage(slots[i]);
            Sprite sp = LoadMonsterSprite(cfg);
            if (portrait != null && sp != null)
            {
                portrait.sprite = sp;
                portrait.color = Color.white;
                portrait.preserveAspect = true;
            }
            if (cfg.isBoss) bossSlot = slots[i];
        }

        if (_bossTag != null)
        {
            bool hasBoss = bossSlot != null;
            _bossTag.gameObject.SetActive(hasBoss);
            if (hasBoss)
            {
                _bossTag.text = "BOSS";
                _bossTag.transform.SetParent(bossSlot, false);
                var rt = _bossTag.rectTransform;
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -2f);
                rt.sizeDelta = new Vector2(0f, 24f);
            }
        }
    }

    static Image FindPortraitImage(Transform slot)
    {
        Image[] imgs = slot.GetComponentsInChildren<Image>(true);
        if (imgs == null || imgs.Length == 0) return null;
        if (imgs.Length >= 2) return imgs[imgs.Length - 1];
        return imgs[0];
    }

    static Sprite LoadMonsterSprite(MonsterConfig cfg)
    {
        if (cfg == null) return null;
        int gameChapter = 1;
        int idCh = 0;
        if (!string.IsNullOrEmpty(cfg.id))
        {
            int us = cfg.id.IndexOf('_');
            if (us >= 0 && us + 1 < cfg.id.Length && int.TryParse(cfg.id.Substring(us + 1, 1), out int n))
                idCh = n;
        }
        // 怪物章号 → 游戏章
        for (int i = 1; i <= 8; i++)
        {
            if (GameConfig.GetMonsterChapter(i) == idCh) { gameChapter = i; break; }
        }
        int monsterChapter = idCh > 0 ? idCh : GameConfig.GetMonsterChapter(gameChapter);
        int spriteIndex = cfg.spriteIndex > 0 ? cfg.spriteIndex : 1;

        var loader = MonsterSpriteLoader.Instance;
        if (loader != null)
        {
            var sp = loader.LoadMonsterSprite(monsterChapter, spriteIndex - 1);
            if (sp != null) return sp;
        }

        string folder = null, prefix = null;
        switch (monsterChapter)
        {
            case 1: folder = "1 Undead"; prefix = "undead_1"; break;
            case 2: folder = "2 Jungle"; prefix = "jungle_2"; break;
            case 3: folder = "3 Sea"; prefix = "sea_3"; break;
            case 4: folder = "4 Forest"; prefix = "forest_4"; break;
            case 5: folder = "5 Field"; prefix = "field_5"; break;
            case 6: folder = "6 Cave"; prefix = "cave_6"; break;
            case 7: folder = "7 Devil"; prefix = "devil_7"; break;
            case 8: folder = "8 Ice"; prefix = "ice_8"; break;
        }
        if (folder == null) return null;
        string path = $"Config/MonsterSpriteRegistry/{folder}/{prefix}{spriteIndex:D2}";
        var sprite = Resources.Load<Sprite>(path);
        if (sprite != null) return sprite;
        var tex = Resources.Load<Texture2D>(path);
        if (tex == null) return null;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }

    void ShowMonsterTip(MonsterConfig cfg)
    {
        EnsureTip();
        if (_tipRoot == null || cfg == null) return;
        _tipRoot.SetActive(true);
        if (_tipTitle != null) _tipTitle.text = string.IsNullOrEmpty(cfg.monsterName) ? cfg.id : cfg.monsterName;
        int mch = GameConfig.GetMonsterChapter(_selectedChapter);
        var style = MonsterAttackStyleTable.Get(mch, cfg.spriteIndex);
        string styleName = style == MonsterAttackStyle.Ranged ? "远程" : "近战";
        if (_tipBody != null)
        {
            _tipBody.text = cfg.isBoss
                ? $"BOSS\n攻击 {cfg.baseAttack:0}\n生命 {cfg.baseHp:0}\n{styleName}"
                : $"攻击 {cfg.baseAttack:0}\n生命 {cfg.baseHp:0}\n{styleName}";
        }
    }

    void HideTip()
    {
        if (_tipRoot != null) _tipRoot.SetActive(false);
    }

    void EnsureTip()
    {
        if (_tipRoot != null) return;
        var host = transform.Find("DetailPanel") ?? transform;
        var go = new GameObject("MonsterTip", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(host, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.55f);
        rt.anchorMax = new Vector2(0.5f, 0.55f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(280f, 140f);
        rt.anchoredPosition = Vector2.zero;
        go.GetComponent<Image>().color = new Color(0.08f, 0.06f, 0.04f, 0.92f);

        var titleGo = new GameObject("TipTitle", typeof(RectTransform), typeof(Text));
        titleGo.transform.SetParent(go.transform, false);
        var tr = titleGo.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0, 1);
        tr.anchorMax = new Vector2(1, 1);
        tr.pivot = new Vector2(0.5f, 1);
        tr.anchoredPosition = new Vector2(0, -8);
        tr.sizeDelta = new Vector2(-16, 32);
        _tipTitle = titleGo.GetComponent<Text>();
        _tipTitle.fontSize = 22;
        _tipTitle.alignment = TextAnchor.MiddleCenter;
        _tipTitle.color = new Color(1f, 0.92f, 0.7f);
        _tipTitle.font = GameFonts.GetChinese();

        var bodyGo = new GameObject("TipBody", typeof(RectTransform), typeof(Text));
        bodyGo.transform.SetParent(go.transform, false);
        var br = bodyGo.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(0, 0);
        br.anchorMax = new Vector2(1, 1);
        br.offsetMin = new Vector2(12, 10);
        br.offsetMax = new Vector2(-12, -42);
        _tipBody = bodyGo.GetComponent<Text>();
        _tipBody.fontSize = 18;
        _tipBody.alignment = TextAnchor.UpperCenter;
        _tipBody.color = new Color(0.9f, 0.85f, 0.75f);
        _tipBody.font = GameFonts.GetChinese();

        var closeBtn = go.AddComponent<Button>();
        closeBtn.targetGraphic = go.GetComponent<Image>();
        closeBtn.onClick.AddListener(HideTip);

        _tipRoot = go;
        GameFonts.ApplyToHierarchy(go.transform);
        go.SetActive(false);
    }

    static int GetMaxUnlockedChapter()
    {
        int max = SaveSystem.Instance?.Data?.maxUnlockedChapter ?? 1;
        if (max < 1) max = 1;
        if (max > 8) max = 8;
        return max;
    }

    string ModeLabel(int i)
    {
        if (modeButtonLabels != null && i >= 0 && i < modeButtonLabels.Length && modeButtonLabels[i] != null
            && !string.IsNullOrEmpty(modeButtonLabels[i].text))
            return modeButtonLabels[i].text;
        if (i >= 0 && i < ModeNames.Length) return ModeNames[i];
        return "副本";
    }

    string DiffLabel(int i)
    {
        if (difficultyLabels != null && i >= 0 && i < difficultyLabels.Length && difficultyLabels[i] != null
            && !string.IsNullOrEmpty(difficultyLabels[i].text))
            return difficultyLabels[i].text;
        if (i >= 0 && i < DiffNames.Length) return DiffNames[i];
        return "难度";
    }

    static string GetChapterIntro(int chapter)
    {
        switch (chapter)
        {
            case 1: return "暮影森林外围，哥布林与野兽出没。击败章末首领即可开启下一章。";
            case 2: return "幽冥墓园中亡灵苏醒，小心成群的骷髅与法师。";
            case 3: return "翡翠秘境潮湿闷热，毒虫与密林伏兵环伺。";
            case 4: return "深蓝遗迹海域，潮水带来陌生的深海生物。";
            case 5: return "晨曦原野看似开阔，潜伏的猎手并不少。";
            case 6: return "巨岩深窟黑暗潮湿，洞穴生物成群结队。";
            case 7: return "赤焰炼狱热浪灼人，魔族精锐在此驻守。";
            case 8: return "永霜雪境天寒地冻，冰原霸主等待挑战者。";
            default: return "未知的冒险区域。";
        }
    }

    bool IsModePlayable(int mode) => mode == 0;

    bool IsActivityMode(int mode)
    {
        string label = ModeLabel(mode);
        if (!string.IsNullOrEmpty(label) && label.IndexOf("活动", StringComparison.Ordinal) >= 0)
            return true;
        return mode == 4;
    }

    static int GetClearedChapterCount()
    {
        return Mathf.Max(0, GetMaxUnlockedChapter() - 1);
    }

    static bool IsDiffUnlocked(int diff)
    {
        if (diff <= 0) return true;
        int cleared = GetClearedChapterCount();
        if (diff == 1) return cleared >= GameConfig.DIFF_HARD_NEED_CLEARS;
        if (diff == 2) return cleared >= GameConfig.DIFF_NIGHTMARE_NEED_CLEARS;
        return false;
    }

    void RefreshContentLock()
    {
        bool locked = !IsModePlayable(_selectedMode);
        ApplyCanvasLock(transform.Find("RightContent"), locked);
        var detail = transform.Find("DetailPanel");
        if (detail == null)
        {
            var right = transform.Find("RightContent");
            if (right != null) detail = right.Find("DetailPanel");
        }
        ApplyCanvasLock(detail, locked);
    }

    static void ApplyCanvasLock(Transform t, bool locked)
    {
        if (t == null) return;
        var cg = t.GetComponent<CanvasGroup>();
        if (cg == null) cg = t.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = locked ? 0.42f : 1f;
        cg.interactable = !locked;
        cg.blocksRaycasts = !locked;
    }

    static void SetGraphicDim(Transform root, bool dim)
    {
        if (root == null) return;
        var images = root.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] == null) continue;
            Color c = images[i].color;
            c.a = dim ? 0.4f : 1f;
            images[i].color = c;
        }
        var texts = root.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null) continue;
            Color c = texts[i].color;
            c.a = dim ? 0.45f : 1f;
            texts[i].color = c;
        }
    }

    void RefreshDropIcons()
    {
        if (dropIconContainer == null) return;

        var slots = new List<DropSlot>();
        for (int i = 0; i < 4; i++)
        {
            var kuang = dropIconContainer.Find($"Iconkuang{i}");
            var icon = dropIconContainer.Find($"Icon{i}");
            if (kuang == null && icon == null) continue;
            slots.Add(new DropSlot { kuang = kuang, icon = icon });
        }

        bool playable = IsModePlayable(_selectedMode);
        bool goldOnly = IsActivityMode(_selectedMode);
        int showCount = 0;
        if (playable)
            showCount = goldOnly ? 1 : Mathf.Min(slots.Count, 2);

        Sprite goldSp = LoadGoldDropSprite();
        for (int i = 0; i < slots.Count; i++)
        {
            bool on = i < showCount;
            if (slots[i].kuang != null) slots[i].kuang.gameObject.SetActive(on);
            if (slots[i].icon != null) slots[i].icon.gameObject.SetActive(on);
            if (!on || slots[i].icon == null) continue;
            var img = slots[i].icon.GetComponent<Image>();
            if (img == null) continue;
            if (i == 0 && goldSp != null)
            {
                img.sprite = goldSp;
                img.color = Color.white;
                img.preserveAspect = true;
            }
        }
    }

    struct DropSlot
    {
        public Transform kuang;
        public Transform icon;
    }

    Sprite LoadGoldDropSprite()
    {
        if (_goldDropSprite != null) return _goldDropSprite;
        Transform hall = GuildHallUI.Instance != null ? GuildHallUI.Instance.transform : null;
        if (hall != null)
        {
            Transform t = TownSharedChrome.FindDeep(hall, "GoldIcon")
                          ?? TownSharedChrome.FindDeep(hall, "金币");
            if (t != null)
            {
                var img = t.GetComponent<Image>();
                if (img != null && img.sprite != null)
                    _goldDropSprite = img.sprite;
            }
        }
        return _goldDropSprite;
    }

    void Toast(string msg)
    {
        Debug.Log("[AdventureUI] " + msg);
        ShowFloatToast(msg);
    }

    void ShowFloatToast(string msg)
    {
        EnsureFloatToast();
        if (_floatToast == null) return;
        if (_floatToastCo != null) StopCoroutine(_floatToastCo);
        _floatToastCo = StartCoroutine(CoFloatToast(msg));
    }

    void HideFloatToast()
    {
        if (_floatToastCo != null)
        {
            StopCoroutine(_floatToastCo);
            _floatToastCo = null;
        }
        if (_floatToast != null)
            _floatToast.gameObject.SetActive(false);
    }

    void EnsureFloatToast()
    {
        if (_floatToast != null) return;
        var go = new GameObject("FloatToast", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(transform, false);
        _floatToastRt = go.GetComponent<RectTransform>();
        _floatToastRt.anchorMin = new Vector2(0.5f, 0.5f);
        _floatToastRt.anchorMax = new Vector2(0.5f, 0.5f);
        _floatToastRt.pivot = new Vector2(0.5f, 0.5f);
        _floatToastRt.sizeDelta = new Vector2(560f, 80f);
        _floatToastRt.anchoredPosition = Vector2.zero;
        _floatToast = go.GetComponent<Text>();
        _floatToast.font = GameFonts.GetChinese();
        _floatToast.fontSize = 32;
        _floatToast.alignment = TextAnchor.MiddleCenter;
        _floatToast.color = new Color(1f, 0.94f, 0.72f, 1f);
        _floatToast.horizontalOverflow = HorizontalWrapMode.Overflow;
        _floatToast.raycastTarget = false;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        go.transform.SetAsLastSibling();
        go.SetActive(false);
    }

    IEnumerator CoFloatToast(string msg)
    {
        _floatToast.text = msg;
        _floatToast.gameObject.SetActive(true);
        _floatToast.transform.SetAsLastSibling();
        Color c = _floatToast.color;
        c.a = 1f;
        _floatToast.color = c;
        _floatToastRt.anchoredPosition = Vector2.zero;

        yield return new WaitForSeconds(2f);

        const float dur = 0.65f;
        float t = 0f;
        Vector2 start = Vector2.zero;
        Vector2 end = new Vector2(0f, 120f);
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            _floatToastRt.anchoredPosition = Vector2.Lerp(start, end, u);
            c.a = 1f - u;
            _floatToast.color = c;
            yield return null;
        }

        _floatToast.gameObject.SetActive(false);
        _floatToastCo = null;
    }

    // ────────────────────────────────────────────────────
    // 工具方法
    // ────────────────────────────────────────────────────

    /// <summary>从预制体节点回填公开引用（生成后改过树也能尽量绑上）</summary>
    void AutoBindFromHierarchy()
    {
        var left = transform.Find("LeftSidebar");
        if (left != null)
        {
            for (int i = 0; i < 5; i++)
            {
                var btnT = left.Find($"ModeBtn_{i}");
                if (btnT == null) continue;
                var btn = btnT.GetComponent<Button>();
                if (modeButtons.Length > i) modeButtons[i] = btn;
                var label = btnT.Find("Label")?.GetComponent<Text>();
                if (modeButtonLabels.Length > i) modeButtonLabels[i] = label;
                var iconImg = btnT.Find("Icon")?.GetComponent<Image>();
                if (modeButtonIcons.Length > i && iconImg != null) modeButtonIcons[i] = iconImg;
            }
        }

        var right = transform.Find("RightContent");
        var mapRoot = right != null ? right.Find("MapRoot") : transform.Find("MapRoot");
        if (mapRoot != null)
        {
            if (mapBg == null) mapBg = mapRoot.Find("MapBg")?.GetComponent<Image>();
            if (stageNodeContainer == null) stageNodeContainer = mapRoot.Find("StageNodes");
            var titleBar = mapRoot.Find("TitleBar");
            if (titleBar != null)
            {
                if (chapterTitle == null) chapterTitle = titleBar.Find("ChapterTitle")?.GetComponent<Text>();
                if (prevChapterBtn == null) prevChapterBtn = titleBar.Find("PrevBtn")?.GetComponent<Button>();
                if (nextChapterBtn == null) nextChapterBtn = titleBar.Find("NextBtn")?.GetComponent<Button>();
            }
        }

        var detail = transform.Find("DetailPanel");
        if (detail == null && right != null) detail = right.Find("DetailPanel");
        if (detail == null) return;
        stageNameLabel = detail.Find("StageName")?.GetComponent<Text>();
        stageDescLabel = detail.Find("StageDesc")?.GetComponent<Text>();
        enemyIconContainer = detail.Find("EnemyIcons");
        dropIconContainer = detail.Find("DropIcons");
        staminaCostLabel = detail.Find("StaminaCost")?.GetComponent<Text>();
        remainChancesLabel = detail.Find("Chances")?.GetComponent<Text>();
        addChancesBtn = detail.Find("AddChances")?.GetComponent<Button>();
        startBtn = detail.Find("StartBtn")?.GetComponent<Button>();
        sweepBtn = detail.Find("SweepBtn")?.GetComponent<Button>();
        for (int i = 0; i < 4; i++)
        {
            string[] names = { "Diff_普通", "Diff_困难", "Diff_噩梦", "Diff_地狱" };
            var d = detail.Find(names[i]);
            if (d == null) continue;
            if (difficultyButtons.Length > i) difficultyButtons[i] = d.GetComponent<Button>();
            if (difficultyLabels.Length > i) difficultyLabels[i] = d.Find("Lbl")?.GetComponent<Text>();
        }
    }

    void EnsureVisibleTransform()
    {
        var rt = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
        if (rt.localScale == Vector3.zero) rt.localScale = Vector3.one;
    }

    bool _canvasConfigured;

    void ConfigureHostCanvasOnce()
    {
        if (_canvasConfigured) return;
        EnsureVisibleTransform();
        TownPageCanvas.Configure(gameObject, 5, stripCanvasWhenNested: true);
        _canvasConfigured = true;
    }

    /// <summary>未挂在大厅下时，才在本页挂一份资源条+底栏（预制体/独立打开）。</summary>
    void EnsureStandaloneChrome(Transform hall)
    {
        var nested = GetComponentInParent<GuildHallUI>();
        if (nested != null && nested.gameObject != gameObject) return;
        TownSharedChrome.EnsureResourceBar(transform, hall);
        TownSharedChrome.EnsureBottomNav(transform, hall, MainNavTab.Adventure);
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
