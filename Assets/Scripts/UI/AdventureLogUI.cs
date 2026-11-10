using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 冒险日志：以 Resources/Prefabs/Town/AdventureLogUI 预制体为准。
/// 只绑定节点、改显隐/文本；不重建层级、不覆盖预制体资源。
/// </summary>
public class AdventureLogUI : MonoBehaviour, ITownPage
{
    public static AdventureLogUI Instance { get; private set; }
    public MainNavTab Tab => MainNavTab.Log;

    public bool IsPageVisible =>
        gameObject.activeInHierarchy && _root != null && _root.activeSelf;

    public string CurrentTabName =>
        (int)_tab >= 0 && (int)_tab < TabNames.Length ? TabNames[(int)_tab] : "";

    public string BodyText => _probeBody ?? "";

    enum LogTab
    {
        MainStory = 0,
        SideStory = 1,
        Monster = 2,
        Merc = 3,
        Achievement = 4,
        World = 5
    }

    struct LogRow
    {
        public string Title;
        public string Progress;
        public string Detail;
        // 2026-10-31 主人报「点任务条后当前目标只显示进行中，没写怎么做什么的」：
        // 行新增「点击后当前目标槽该显示什么」，点击卡片时不再拿进度字凑数。
        public string Objective;
        public bool Locked;
        public string AchId;
        /// <summary>世界页条目在 AdventureLogCatalog.World 的下标；-1 = 非世界行（2026-11-01 主人拍板：世界页点行改走怪物信息弹窗）。</summary>
        public int WorldIndex;
        /// <summary>主线/支线任务行：主线=任务表 id（quest.csv），支线=岛 id 数字串；null = 非任务行。2026-11-01 主人拍板「不能有两套主线支线」：与主界面任务栏同源，点行弹详情。</summary>
        public string QuestId;
        /// <summary>QuestId 非空时生效：true = 主线任务，false = 支线岛。</summary>
        public bool QuestMain;
        /// <summary>【2026-11-07 主人拍板】成就行：已领取 → 整条压暗并沉到列表底部。</summary>
        public bool Claimed;
        /// <summary>【2026-11-07 主人拍板】成就行：可领取 → 右上角挂红点。</summary>
        public bool Claimable;
    }

    static readonly string[] TabNames =
    {
        "主线", "支线", "怪物", "佣兵", "成就", "世界"
    };

    GameObject _root;
    Text _activeTitle;
    Text _activeDesc;
    Text _activeObj;
    Text _activeProg;
    // 2026-10-22 主人拍板：Paper 顶部的「进行中」小标头改为显示当前页签名（主线/支线/成就…）
    Text _ongoingHeader;
    Transform _listContent;
    GameObject _claim;
    // 领取按钮置灰（2026-10-20 主人要求）：无可领成就时按钮变灰不可点。
    // 缓存「领取」文字原色，禁用时乘灰、恢复时原样还原；红点子节点不在此列（只处理 Text）。
    readonly Dictionary<int, Color> _claimTextBaseColors = new Dictionary<int, Color>();
    static readonly Color ClaimGrayMultiplier = new Color(0.45f, 0.45f, 0.45f, 0.9f);
    GameObject _rowTemplate;
    string _probeBody;
    readonly List<GameObject> _tabSelectGems = new List<GameObject>();
    readonly List<GameObject> _spawnedRows = new List<GameObject>();
    LogTab _tab = LogTab.MainStory;
    bool _preloaded;
    bool _bound;
    ScrollRect _scroll;
    Image _tabIllustration;
    GameObject _paper;
    GameObject _activeCard;
    AdventureLogCodexPanel _codex;
    AdventureLogPhase3Panel _phase3;
    bool _logChromeOpen;

    const float FrameTopReserveWithBar = 120f;
    // 2026-10-22 主人拍板：日志全屏打开也按状态栏/刘海预留 100px（原 8px 会把标题横幅怼进状态栏）
    const float FrameTopReserveLogOpen = 100f;
    const float FrameBottomReserve = 150f;
    const float FrameSidePad = 16f;
    // 列表首行章节头向上溢出约 52px，Content 顶部至少留这么多，否则被 Viewport 裁
    const int ListTopPadding = 56;

    public void PreloadOnce()
    {
        if (_preloaded) return;
        BindPrefab();
        HidePage();
        _preloaded = true;
    }

    public void ShowPage()
    {
        BindPrefab();
        TownSaveAlign.AlignAll();
        AdventureLogAchievements.EvaluateAll();
        AdventureLogMileageShop.EnsureWeek();
        if (_root != null) _root.SetActive(true);
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        var hall = GetComponentInParent<GuildHallUI>();
        if (hall != null)
            TownSharedChrome.RaiseSharedChrome(hall.transform);
        SetLogChrome(true);
        TownPageDim.Ensure(transform);
        // 只在编辑器失效：运行时清缓存会让底栏下次点击重新 Resources.Load 图集，造成一次卡顿
#if UNITY_EDITOR
        MainBottomNav.InvalidateNavBgCache();
#endif
        MainBottomNav.Instance?.SetSelected(MainNavTab.Log, notify: false);
        EnsureFrameClearsChrome();
        EnsureCloseButtonPosition();
        EnsureSidebarLayout();
        _phase3 = AdventureLogPhase3Panel.Ensure(this);
        RedDot.RefreshCommon();
        BindSidebarTabIcons();
        SelectTab(LogTab.MainStory, force: true);
    }

    /// <summary>碎片/商店操作后刷新列表（不关弹层）。</summary>
    public void RefreshAfterPhase3()
    {
        if (!IsCodexTab(_tab))
            RefreshBody();
        RedDot.RefreshCommon();
    }

    public void HidePage()
    {
        AchievementMilestoneUI.Hide();
        if (_root != null) _root.SetActive(false);
        gameObject.SetActive(false);
        SetLogChrome(false);
        EnsureFrameClearsChrome();
        EnsureCloseButtonPosition();
    }

    void SetLogChrome(bool logOpen)
    {
        if (_logChromeOpen == logOpen) return;
        _logChromeOpen = logOpen;

        var hall = GuildHallUI.Instance;
        if (hall == null) return;

        TavernUI.SetGuildHallOverlayMode(logOpen);

        Transform top = TownSharedChrome.FindDeep(hall.transform, "TopBar");
        if (top != null && top.gameObject.activeSelf != !logOpen)
            top.gameObject.SetActive(!logOpen);
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void BindPrefab()
    {
        if (_bound && _root != null) return;

        _root = transform.Find("Root")?.gameObject;
        if (_root == null)
        {
            Debug.LogError("[AdventureLogUI] 找不到预制体 Root，请使用 Resources/Prefabs/Town/AdventureLogUI");
            return;
        }

        ConfigureHostCanvasOnce();

        _activeTitle = FindText("ActiveTitle");
        _activeDesc = FindText("ActiveDesc");
        var art = FindTransform("Art");
        _activeObj = art != null
            ? art.Find("Objective")?.GetComponent<Text>()
            : FindText("Objective");
        _activeProg = art != null
            ? art.Find("Progress")?.GetComponent<Text>()
            : FindText("Progress");

        _scroll = FindTransform("Scroll")?.GetComponent<ScrollRect>();
        _listContent = transform.Find("Root/Frame/Paper/Scroll/Viewport/Content");
        if (_listContent == null && _scroll != null)
            _listContent = _scroll.content;

        _paper = transform.Find("Root/Frame/Paper")?.gameObject;
        // 2026-10-31 实锤：OngoingHeader 实际挂在 Paper/bg 下，旧路径 Paper/OngoingHeader
        // 一直 Find 落空，顶部木牌永远显示预制体默认「进行中」、不随页签变。先查 bg/ 再兜旧路径。
        _ongoingHeader = _paper != null
            ? (_paper.transform.Find("bg/OngoingHeader")?.GetComponent<Text>()
               ?? _paper.transform.Find("OngoingHeader")?.GetComponent<Text>())
            : null;
        BindAchievementClaimButton();
        _activeCard = FindTransform("ActiveCard")?.gameObject;
        var frame = transform.Find("Root/Frame");
        if (frame != null)
            _codex = new AdventureLogCodexPanel(frame);
        HidePaper1Template();
        PrepareDoneTemplate();
        ApplyReadableFontSizes();
        EnsureLogChromeUsable();
        WireTabs();
        BindSidebarTabIcons();
        WireButtons();
        BindRewardRedDots();
        BindTabIllustration();
        // 2026-11-07 主人拍板：冒险日志其它页面用到同款「标签底」横条底的也一起 70% 不透明
        UiKeyedBackgrounds.ApplyLogBarAlphaToTree(transform);
        RedDot.RefreshCommon();
        _bound = true;
    }

    /// <summary>
    /// 成就领取：用用户手做的 Paper1/ClaimAch（小按钮「领取」）。
    /// Paper/ClaimAch 是旧大按钮，成就页隐藏，避免跟预制体不一致。
    /// </summary>
    void BindAchievementClaimButton()
    {
        var paper = transform.Find("Root/Frame/Paper");
        var legacy = paper != null ? paper.Find("ClaimAch") : null;
        var hand = transform.Find("Root/Frame/Paper1/ClaimAch");

        if (hand != null && paper != null)
        {
            if (legacy != null)
                legacy.gameObject.SetActive(false);
            // 挂到 Paper 下，成就页关掉 Paper1 时按钮仍在；布局保持手做 Rect
            if (hand.parent != paper)
                hand.SetParent(paper, false);
            _claim = hand.gameObject;
            _claim.SetActive(false);
            return;
        }

        _claim = legacy != null ? legacy.gameObject : FindTransform("ClaimAch")?.gameObject;
        if (_claim != null)
            _claim.SetActive(false);
    }

    /// <summary>
    /// 右上插图：ActiveCard/mask/Image。统一走 UiKeyedBackgrounds。
    /// </summary>
    void BindTabIllustration()
    {
        var t = transform.Find("Root/Frame/Paper/ActiveCard/mask/Image");
        if (t == null) t = FindTransform("mask")?.Find("Image");
        _tabIllustration = t != null ? t.GetComponent<Image>() : null;
    }

    void ApplyTabIllustration()
    {
        if (_tabIllustration == null)
            BindTabIllustration();
        if (_tabIllustration == null) return;
        if (!IsCodexTab(_tab) && _activeCard != null && !_activeCard.activeSelf)
            _activeCard.SetActive(true);
        string key = CurrentTabName;
        if (string.IsNullOrEmpty(key)) return;
        if (UiKeyedBackgrounds.ApplyLogTabIllust(_tabIllustration, key))
        {
            _tabIllustration.enabled = true;
            _tabIllustration.color = Color.white;
            // 2026-10-31 晚 主人重摆预制体并拍板「一切以预制体为准」：这里只负责按页签换图，
            // 不再动插图 RectTransform（旧版按 mask 等比缩放/拉满卡宽的适配已删），
            // 尺寸、锚点、preserveAspect 全按主人在预制体里摆的来。
        }
        else
            Debug.LogWarning($"[AdventureLogUI] 缺少标签插图 {key}");
    }

    // 2026-10-31 晚：插图宽度适配整套（LayoutTabIllustration/LayoutIllustCaption/补算协程/
    // OnRectTransformDimensionsChange）已删 —— 主人重摆预制体后拍板「以预制体为准」，
    // 运行时不再重算插图与字幕几何，见 ApplyTabIllustration 内注释。

    bool IsCodexTab(LogTab tab) => tab == LogTab.Monster || tab == LogTab.Merc;

    // 2026-10-31 晚：以下两套运行时几何修正已删（主人在预制体里重摆了版面并拍板
    // 「一切以预制体为准」，运行时不得再覆盖）：
    // ① EnsureCaptionFollowsArt/ReparentCaptionUnderMask（10-22 字随图走的改挂）——
    //    ActiveTitle/ActiveDesc 留在预制体原挂点，主人已按比例锚好；
    // ② EnsurePaperTopLayout（10-24 页签/插图贴顶）——bg、mask 位置全按预制体。
    // ✅ 同晚主人授权已把预制体里 Paper/bg 与 ActiveCard/mask 的 y 锚点改成 1（锚纸顶，
    // 位置按 1280 高画布视觉等价换算：bg y=-49.876、mask y=+27.947），瘦屏机不再下坠，
    // 本文件运行时不再做任何几何补偿。

    void ApplyModeVisibility()
    {
        bool codex = IsCodexTab(_tab);
        if (_paper != null)
        {
            // 任务形态：Paper 列表；图鉴形态：隐藏列表区，保留 Paper 也可整隐
            var scroll = _paper.transform.Find("Scroll");
            // 同一实锤：隐藏也要走 bg/ 路径，否则图鉴页顶牌藏不掉。
            var ongoing = _paper.transform.Find("bg/OngoingHeader")
                          ?? _paper.transform.Find("OngoingHeader");
            if (scroll != null) scroll.gameObject.SetActive(!codex);
            if (ongoing != null) ongoing.gameObject.SetActive(!codex);
            if (_activeCard != null) _activeCard.SetActive(!codex);
        }
        if (codex)
        {
            _codex?.Ensure();
            if (_tab == LogTab.Monster)
                _codex?.ShowMonsters();
            else
                _codex?.ShowMercs();
        }
        else
        {
            _codex?.Hide();
            HidePaper1Template();
            if (_paper != null) _paper.SetActive(true);
        }
    }

    /// <summary>预制体 Paper1/怪物模板默认激活，非图鉴 Tab 必须整页关掉。</summary>
    void HidePaper1Template()
    {
        var paper1 = transform.Find("Root/Frame/Paper1");
        if (paper1 != null)
            paper1.gameObject.SetActive(false);
        var cell = paper1 != null
            ? paper1.Find("怪物") ?? paper1.Find("MonsterCell")
            : null;
        if (cell != null)
            cell.gameObject.SetActive(false);
    }

    void OnCodexSelect(string id, string title, string detail)
    {
        _probeBody = title + "\n" + detail;
    }

    void ConfigureHostCanvasOnce()
    {
        // 与角色/冒险/酒馆一致：嵌在大厅下走父 Canvas，避免独立 Canvas(sorting40)
        // 盖住底栏后底部留黑边，看起来整页「偏下」。不是摄像机问题。
        EnsureHostRect();
        TownPageCanvas.Configure(gameObject, 5, stripCanvasWhenNested: true);
        EnsureFrameClearsChrome();
    }

    /// <summary>预制体根曾存成 scale0 / 锚点 0,0 零尺寸，运行时先拉满父节点。</summary>
    void EnsureHostRect()
    {
        var rt = transform as RectTransform;
        if (rt == null) return;
        if (rt.localScale.sqrMagnitude < 0.0001f)
            rt.localScale = Vector3.one;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// Frame 按顶栏/底栏像素预留上移，与 CharacterUI(120/150) 对齐。
    /// 不改手做预制体资源，仅运行时纠正。
    /// </summary>
    void EnsureFrameClearsChrome()
    {
        float topReserve = _logChromeOpen ? FrameTopReserveLogOpen : FrameTopReserveWithBar;
        var frame = transform.Find("Root/Frame") as RectTransform;
        if (frame == null) return;
        frame.anchorMin = Vector2.zero;
        frame.anchorMax = Vector2.one;
        frame.pivot = new Vector2(0.5f, 0.5f);
        frame.anchoredPosition = Vector2.zero;
        frame.sizeDelta = Vector2.zero;
        frame.offsetMin = new Vector2(FrameSidePad, FrameBottomReserve);
        frame.offsetMax = new Vector2(-FrameSidePad, -topReserve);
    }

    /// <summary>
    /// 关闭钮在预制体里按顶栏留 120px 定位；日志全屏打开时顶距改为 100px（2026-10-22 安全区
    /// 预留，原 8px 会与状态栏/微信胶囊重叠），需把钮拉回外框右上角并避开胶囊。
    /// </summary>
    void EnsureCloseButtonPosition()
    {
        var close = transform.Find("Root/CloseButton") as RectTransform;
        if (close == null) return;
        close.anchorMin = new Vector2(1f, 1f);
        close.anchorMax = new Vector2(1f, 1f);
        close.pivot = new Vector2(1f, 1f);
        float topInset = _logChromeOpen ? FrameTopReserveLogOpen + 10f : 81f;
        close.anchoredPosition = new Vector2(-14f, -topInset);
        close.SetAsLastSibling();
    }

    void PrepareDoneTemplate()
    {
        if (_rowTemplate != null) return;
        var done = FindTransform("已完成");
        if (done == null) return;

        if (_listContent != null && done.parent != _listContent)
            done.SetParent(_listContent, false);

        var le = done.GetComponent<LayoutElement>();
        if (le == null) le = done.gameObject.AddComponent<LayoutElement>();
        float h = Mathf.Max(72f, ((RectTransform)done).rect.height);
        if (h < 8f) h = 88f;
        le.minHeight = h;
        le.preferredHeight = h;
        le.flexibleWidth = 1f;

        // 首行章节头（di 牌 + DoneHeader 文案，scale 1.5）挂在行顶并向上溢出约 52px，
        // 而 Content 顶部 padding 只有 6：列表归顶时章节头上半截被 Viewport Mask 裁掉。
        // 运行时给 Content 补足表头留白，不改预制体文件。
        if (_listContent != null)
        {
            var vlg = _listContent.GetComponent<VerticalLayoutGroup>();
            if (vlg != null && vlg.padding.top < ListTopPadding)
                vlg.padding = new RectOffset(
                    vlg.padding.left, vlg.padding.right, ListTopPadding, vlg.padding.bottom);
        }

        _rowTemplate = done.gameObject;
        _rowTemplate.SetActive(false);

        // 2026-10-20 主人拍板：标题 Objective 的运行时改写已删，行内排版以预制体为准。
        // 仅保留 Progress 状态字兜底：预制体状态框按"0/1"这类短字设计，
        // 支线/成就页右侧放的是长解锁条件（十几个字），窄框放不下会换行挤成一团，
        // 这里把框收进行内右端加宽到 264 并开 BestFit，只动这一项。
        var prog = done.Find("Progress");
        if (prog != null)
        {
            var prt = (RectTransform)prog;
            prt.anchorMin = new Vector2(1f, 0.5f);
            prt.anchorMax = new Vector2(1f, 0.5f);
            prt.pivot = new Vector2(1f, 0.5f);
            prt.anchoredPosition = new Vector2(-16f, -10f);
            prt.sizeDelta = new Vector2(264f, 44f);
            var pt = prog.GetComponent<Text>();
            if (pt != null)
            {
                pt.alignment = TextAnchor.MiddleRight;
                pt.horizontalOverflow = HorizontalWrapMode.Wrap;
                pt.resizeTextForBestFit = true;
                pt.resizeTextMinSize = 16;
                int baseSize = Mathf.RoundToInt(pt.fontSize);
                pt.resizeTextMaxSize = Mathf.Clamp(baseSize, 20, 64);
            }
        }
    }

    /// <summary>
    /// 冒险日志可点性修复（不改预制体）：
    /// Paper / Paper1 是全屏拉伸容器，自身 Image 参与射线且渲染序压在 Sidebar 之上，
    /// 点击命中后沿父链找不到 Button 就被吞掉 —— 左侧标签永远点不到。关掉容器自身
    /// 的 raycast（子级滚动区/按钮/格子都是后代，不受影响）；横幅 bg 与标题 Text 同理。
    /// // 2026-10-23 主人拍板：标题字号/框以预制体为准，删除运行时 BestFit 与定框改写
    /// </summary>
    void EnsureLogChromeUsable()
    {
        DisableOwnRaycast(_paper);
        DisableOwnRaycast(transform.Find("Root/Frame/Paper1")?.gameObject);

        var biaotou = transform.Find("Root/Frame/biaotou");
        if (biaotou == null)
        {
            Debug.LogError("[AdventureLogUI] 找不到 Root/Frame/biaotou，横幅射线修复跳过");
            return;
        }

        var bgTr = biaotou.Find("bg");
        var bgImg = bgTr != null ? bgTr.GetComponent<Image>() : null;
        if (bgImg != null) bgImg.raycastTarget = false;

        var t = FindBannerTitle(biaotou);
        if (t == null)
        {
            Debug.LogError("[AdventureLogUI] Root/Frame/biaotou 下找不到 Title (1) 或任何 Text，标题射线修复跳过");
            return;
        }
        t.raycastTarget = false;
    }

    // 2026-10-23 主人拍板：横幅标题查找统一出口（优先 Title (1)，退化取 biaotou 下第一个 Text），射线修复与文案切换共用
    static Text FindBannerTitle(Transform biaotou)
    {
        var titleTr = biaotou.Find("Title (1)");
        if (titleTr == null)
        {
            // 名字对不上时退化为取 biaotou 下的第一个 Text
            for (int i = 0; i < biaotou.childCount && titleTr == null; i++)
            {
                var c = biaotou.GetChild(i);
                if (c.GetComponent<Text>() != null) titleTr = c;
            }
        }
        return titleTr != null ? titleTr.GetComponent<Text>() : null;
    }

    static void DisableOwnRaycast(GameObject go)
    {
        if (go == null) return;
        var img = go.GetComponent<Image>();
        if (img != null) img.raycastTarget = false;
    }

    /// <summary>
    /// 仅纠正标题横幅(biaotou)：Frame 被运行时拉满后，预制体按旧 Frame 尺寸摆的横幅
    /// 会跟着 Frame 中心上漂，这里按 2026-10-25 主人拍板钉成预制体观感——骑跨 Frame 左上角。
    /// 2026-10-16 主人拍板：左侧标签按预制体排版走 —— Tabs 容器与 Tab0~5 的
    /// 锚点/位置/尺寸一律不再被代码改写（9-19 引入、10-15 调整的运行时重排已整段移除）。
    /// </summary>
    void EnsureSidebarLayout()
    {
        var frame = transform.Find("Root/Frame") as RectTransform;
        if (frame == null) return;

        // 标题横幅：恢复预制体观感——骑跨 Frame 左上角（2026-10-25 主人拍板）。
        // 10-16 旧钉位取「框内左上内缩 10px」，运行时横幅整个挪进框内挂在边框下沿，
        // 与预制体骑跨框角（上探约 49 / 垂下约 54）不符，且该差异与分辨率无关。
        // 几何从预制体实测值反推：bg 中心 y = headerH/2 − 54（垂下 54，其余上探框顶）。
        // 2026-10-25 二次拍板：左侧绶带贴齐屏幕左边缘——Frame 左缘被 FrameSidePad 内缩，
        // x 取 bg半宽 − FrameSidePad，任何分辨率下绶带左端都顶到画布 x=0（原框左内缩 30 已废）。
        const float HeaderInsideDrop = 54f; // 横幅垂进框顶以内的部分
        var biaotou = frame.Find("biaotou") as RectTransform;
        if (biaotou == null)
        {
            Debug.LogError("[AdventureLogUI] 找不到 Root/Frame/biaotou，横幅钉位跳过");
            return;
        }
        var bgTr = biaotou.Find("bg") as RectTransform;
        if (bgTr == null)
        {
            Debug.LogError("[AdventureLogUI] 找不到 Root/Frame/biaotou/bg，横幅高度无法实测，钉位跳过");
            return;
        }
        float headerH = bgTr.rect.height;
        biaotou.anchorMin = biaotou.anchorMax = new Vector2(0f, 1f);
        biaotou.pivot = new Vector2(0.5f, 0.5f);
        biaotou.anchoredPosition = new Vector2(bgTr.rect.width * 0.5f - FrameSidePad, headerH * 0.5f - HeaderInsideDrop);
        biaotou.SetAsLastSibling(); // 横幅最上，但已不与标签重叠
    }

    /// <summary>
    /// 2026-10-23 主人拍板：标头常显、文案随页切换（替代 2026-10-16 图鉴页隐藏横幅的旧案）。
    /// 怪物/佣兵图鉴页顶部也要有横幅标头，所以 biaotou 不再因页隐藏；
    /// 只改标题 Text.text（怪物图鉴/佣兵图鉴/冒险日志），字号/颜色/RectTransform 一律不碰。
    /// </summary>
    void ApplyBannerVisibility()
    {
        var frame = transform.Find("Root/Frame");
        var biaotou = frame != null ? frame.Find("biaotou") : null;
        if (biaotou == null)
        {
            Debug.LogError("[AdventureLogUI] 找不到 Root/Frame/biaotou，横幅文案切换跳过");
            return;
        }
        biaotou.gameObject.SetActive(true); // 常显：所有页（含怪物/佣兵图鉴）都保留标头

        var t = FindBannerTitle(biaotou);
        if (t == null)
        {
            Debug.LogError("[AdventureLogUI] Root/Frame/biaotou 下找不到 Title (1) 或任何 Text，横幅文案无法切换");
            return;
        }
        switch (_tab)
        {
            case LogTab.Monster: t.text = "怪物图鉴"; break;
            case LogTab.Merc: t.text = "佣兵图鉴"; break;
            default: t.text = "冒险日志"; break;
        }
    }

    void WireTabs()
    {
        _tabSelectGems.Clear();
        var tabs = transform.Find("Root/Frame/Sidebar/Tabs");
        if (tabs == null) return;

        for (int i = 0; i < TabNames.Length; i++)
        {
            var tab = tabs.Find("Tab" + i);
            if (tab == null) continue;
            var gem = tab.Find("Gem");
            _tabSelectGems.Add(gem != null ? gem.gameObject : null);
            var btn = tab.GetComponent<Button>();
            if (btn == null) btn = tab.gameObject.AddComponent<Button>();
            int idx = i;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => SelectTab((LogTab)idx));
        }
    }

    void BindSidebarTabIcons()
    {
        ApplySidebarTabIcon(2, "怪物");
        ApplySidebarTabIcon(3, "佣兵");
    }

    void ApplySidebarTabIcon(int tabIndex, string tabKey)
    {
        var tab = transform.Find($"Root/Frame/Sidebar/Tabs/Tab{tabIndex}");
        if (tab == null) return;
        var icon = tab.Find("icon")?.GetComponent<Image>()
                   ?? tab.Find("Icon")?.GetComponent<Image>();
        if (icon == null)
        {
            for (int i = 0; i < tab.childCount; i++)
            {
                var img = tab.GetChild(i).GetComponent<Image>();
                if (img != null && img.gameObject.name.IndexOf("icon", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    icon = img;
                    break;
                }
            }
        }
        if (icon == null) return;
        var sp = UiKeyedBackgrounds.LogTabSidebarIcon(tabKey);
        if (sp == null)
            sp = UiKeyedBackgrounds.LogTabIllust(tabKey);
        if (sp == null) return;
        icon.sprite = sp;
        icon.enabled = true;
        icon.color = Color.white;
        icon.preserveAspect = true;
        var rt = icon.rectTransform;
        if (rt != null)
            rt.sizeDelta = new Vector2(44f, 45f);
    }

    void WireButtons()
    {
        var claim = _claim != null ? _claim.GetComponent<Button>() : null;
        if (claim != null)
        {
            claim.onClick.RemoveAllListeners();
            claim.onClick.AddListener(() =>
            {
                int n = AdventureLogAchievements.ClaimAll(out string got);
                if (n > 0)
                    // 2026-10-31 主人报「只说了领取了4个成就奖励，没说是什么」：改成汇报领到了什么
                    UIManager.Instance?.ShowToast(string.IsNullOrEmpty(got) ? $"领取 {n} 个成就奖励" : got);
                else if (AdventureLogMileage.HasUnclaimedLevel())
                    UIManager.Instance?.ShowToast("成就已领完，日志里程奖励请在世界/成就页查看");
                else
                    UIManager.Instance?.ShowToast("暂无可领成就奖励");
                RefreshBody();
                RedDot.RefreshCommon();
            });
            // 按钮图与文案以预制体为准，禁止改 Text / 放大
        }

        var close = transform.Find("Root/CloseButton")?.GetComponent<Button>();
        if (close != null)
        {
            close.onClick.RemoveAllListeners();
            close.onClick.AddListener(() =>
            {
                HidePage();
                TownHubController.Instance?.OpenGuild();
            });
        }

        // 侧栏 Emblem 徽记 → 里程商店入口（2026-10-20 主人拍板，图/排版以预制体为准）
        var emblem = transform.Find("Root/Frame/Sidebar/Emblem");
        if (emblem != null)
        {
            var ebtn = emblem.GetComponent<Button>();
            if (ebtn == null) ebtn = emblem.gameObject.AddComponent<Button>();
            ebtn.transition = Selectable.Transition.None;
            ebtn.onClick.RemoveAllListeners();
            ebtn.onClick.AddListener(OpenMileageShop);
        }
        else
            Debug.LogError("[AdventureLogUI] 预制体缺 Root/Frame/Sidebar/Emblem，里程商店入口接不上");
    }

    void OpenMileageShop()
    {
        _phase3 = AdventureLogPhase3Panel.Ensure(this);
        _phase3?.OpenShopPanel();
    }

    /// <summary>
    /// 领取按钮置灰：没有可领成就时 Button.interactable=false（走预制体自带
    /// ColorTint 的 DisabledColor 半透明灰），「领取」文字同步乘灰；
    /// 有可领时恢复 interactable 并还原文字原色。只动颜色，不改图/文案/字号。
    /// </summary>
    void UpdateClaimButtonState()
    {
        if (_claim == null) return;
        bool has = AdventureLogAchievements.HasUnclaimed();
        // 按钮红点与置灰同口径同出口：只看成就可领，不再被里程/碎片宽口径撑亮（2026-10-20 主人报）
        RedDot.Set(RedDot.AchClaim, has);
        var btn = _claim.GetComponent<Button>();
        if (btn != null) btn.interactable = has;
        for (int i = 0; i < _claim.transform.childCount; i++)
        {
            var txt = _claim.transform.GetChild(i).GetComponent<Text>();
            if (txt == null) continue;   // 红点等 Image 子节点不动
            int id = txt.GetInstanceID();
            if (!_claimTextBaseColors.TryGetValue(id, out var baseColor))
            {
                baseColor = txt.color;   // 首次见到时记原色（此时必为预制体原样）
                _claimTextBaseColors[id] = baseColor;
            }
            txt.color = has ? baseColor : baseColor * ClaimGrayMultiplier;
        }
    }

    void BindRewardRedDots()
    {
        var achTab = transform.Find("Root/Frame/Sidebar/Tabs/Tab4");
        if (achTab != null)
            RedDot.Bind(achTab, RedDot.Achievement);
        var monTab = transform.Find("Root/Frame/Sidebar/Tabs/Tab2");
        if (monTab != null)
            RedDot.Bind(monTab, RedDot.LogMonster);
        var mercTab = transform.Find("Root/Frame/Sidebar/Tabs/Tab3");
        if (mercTab != null)
            RedDot.Bind(mercTab, RedDot.LogMerc);
        // 2026-10-31：里程等级 / 碎片合成挂世界页 Tab，成就 Tab 只留「成就可领」
        var worldTab = transform.Find("Root/Frame/Sidebar/Tabs/Tab5");
        if (worldTab != null)
            RedDot.Bind(worldTab, RedDot.LogWorld);
        if (_claim != null)
            RedDot.Bind(_claim.transform, RedDot.AchClaim, new Vector2(-16f, -16f));
    }

    void SelectTab(LogTab tab, bool force = false)
    {
        if (!force && _tab == tab)
        {
            if (IsCodexTab(_tab)) ApplyModeVisibility();
            else RefreshBody();
            return;
        }

        _tab = tab;
        for (int i = 0; i < _tabSelectGems.Count; i++)
        {
            if (_tabSelectGems[i] != null)
                _tabSelectGems[i].SetActive(i == (int)_tab);
        }

        if (_claim != null)
            _claim.SetActive(_tab == LogTab.Achievement);

        ApplyTabIllustration();
        ApplyModeVisibility();
        ApplyBannerVisibility();
        _phase3 = AdventureLogPhase3Panel.Ensure(this);
        _phase3?.SetVisibleForTab(_tab == LogTab.World || _tab == LogTab.Achievement);
        if (!IsCodexTab(_tab))
            RefreshBody();
        else
            _probeBody = CurrentTabName;
    }

    void RefreshBody()
    {
        string title, desc, objective, progress;
        var rows = new List<LogRow>();
        switch (_tab)
        {
            case LogTab.MainStory: FillMain(out title, out desc, out objective, out progress, rows); break;
            case LogTab.SideStory: FillSide(out title, out desc, out objective, out progress, rows); break;
            case LogTab.Monster: FillMonsters(out title, out desc, out objective, out progress, rows); break;
            case LogTab.Merc: FillMerc(out title, out desc, out objective, out progress, rows); break;
            case LogTab.Achievement: FillAchievement(out title, out desc, out objective, out progress, rows); break;
            default: FillWorld(out title, out desc, out objective, out progress, rows); break;
        }

        // 顶部插图只做装饰，不写任务名与简介；非图鉴页标题/描述置空，条目详情走弹窗。
        bool deco = !IsCodexTab(_tab);
        if (_activeTitle != null) _activeTitle.text = deco ? "" : title;
        if (_activeDesc != null) _activeDesc.text = deco ? "" : desc;
        if (_activeObj != null) _activeObj.text = objective;
        if (_activeProg != null) _activeProg.text = progress;

        // 2026-10-22 主人拍板：Paper 顶部小标头（预制体写死「进行中」）改为显示当前页签名；
        // 图鉴页(怪物/佣兵)此节点已被 ApplyModeVisibility 隐藏，不受影响。
        if (_ongoingHeader != null) _ongoingHeader.text = CurrentTabName;

        RebuildRows(rows);

        var sb = new StringBuilder();
        sb.AppendLine(title);
        sb.AppendLine(desc);
        sb.AppendLine(objective);
        sb.AppendLine(progress);
        for (int i = 0; i < rows.Count; i++)
            sb.AppendLine(rows[i].Title + " " + rows[i].Progress);
        _probeBody = sb.ToString();

        if (_scroll != null)
            _scroll.verticalNormalizedPosition = 1f;

        // 成就领取按钮灰/亮随可领状态走（按钮只在成就页显示，其他页顺手刷新无害）
        UpdateClaimButtonState();
    }

    void FillMain(out string title, out string desc, out string objective, out string progress, List<LogRow> rows)
    {
        // 2026-11-01 主人拍板「不能有两套主线」：主线页与主界面任务栏同一数据源 ——
        // 任务表 MainQuestDefs + MainQuestSystem 状态/进度，列出全部主线任务（含已完成）；
        // 原剧情摘要记录（AdventureLogCatalog.Main）不再混进任务页。
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        int curChapter = MainQuestSystem.CurrentChapter();
        var current = MainQuestSystem.CurrentView();

        var chapters = new List<int>();
        for (int i = 0; i < MainQuestDefs.All.Length; i++)
        {
            int ch = MainQuestDefs.All[i].chapter;
            if (!chapters.Contains(ch)) chapters.Add(ch);
        }
        chapters.Sort();

        int doneCount = 0, totalCount = 0;
        var doneRows = new List<LogRow>();
        for (int c = 0; c < chapters.Count; c++)
        {
            int ch = chapters[c];
            var views = MainQuestSystem.ChapterViews(ch);
            for (int i = 0; i < views.Count; i++)
            {
                var v = views[i];
                totalCount++;
                if (v.done) doneCount++;
                bool reachable = ch <= curChapter;   // 未推到的章节行整体锁定（先做完前面的）
                string how = MainQuestDefs.AutoDesc(v.def);
                string name = string.IsNullOrEmpty(v.def.title) ? how : v.def.title;
                bool claimed = v.done && QuestRewardClaim.IsClaimedMain(v.def.id);
                bool claimable = v.done && !claimed;
                string status = claimed ? "已领取"
                    : v.done ? "已完成"
                    : !reachable ? "未开启"
                    : v.need > 1 ? v.progress + "/" + v.need
                    : "进行中";
                AddRow(claimed ? doneRows : rows, name, status,
                    "第" + ch + "章 · " + how,
                    !reachable,
                    v.done ? "已完成：" + how : how,
                    questId: v.def.id, questMain: true,
                    claimed: claimed, claimable: claimable);
            }
        }
        rows.AddRange(doneRows);

        title = current != null
            ? (string.IsNullOrEmpty(current.def.title) ? MainQuestDefs.AutoDesc(current.def) : current.def.title)
            : "主线任务";
        // 2026-11-01 主人拍板：顶部副标题不要「第X章主线」，就一行「主线简介」，字少点
        desc = "主线简介";
        objective = current != null ? MainQuestDefs.AutoDesc(current.def) : "推进下一章后开启新任务";
        progress = current != null && current.need > 1 ? current.progress + "/" + current.need
            : doneCount + "/" + totalCount;
    }

    void FillSide(out string title, out string desc, out string objective, out string progress, List<LogRow> rows)
    {
        // 2026-11-01 主人拍板「不能有两套支线」：支线页与任务栏同源 —— 支线岛
        // （SideIslandDefs 岛表 + SaveData.branchRoll1/2 派定 + clearedSideIslandIds 通关态，
        // 当前岛直接走 QuestBarData.CurrentSideIsland 同一出口），不再列剧情目录。
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        var currentSide = QuestBarData.CurrentSideIsland();
        int total = SideIslandDefs.Count;
        int clearedCount = 0;
        var doneRows = new List<LogRow>();
        for (int id = 1; id <= total; id++)
        {
            var isl = SideIslandDefs.Get(id);
            if (isl == null)
            {
                Debug.LogError("[AdventureLogUI] SideIslandDefs 缺岛 id=" + id);
                continue;
            }
            bool cleared = SideIslandDefs.IsCleared(id, data);
            if (cleared) clearedCount++;
            bool current = currentSide != null && currentSide.Value.islandId == id;
            // 2026-11-01 主人报「待派定什么玩意」：状态词改直白——没轮到的岛就是「未开启」
            bool claimed = cleared && QuestRewardClaim.IsClaimedSide(id);
            bool claimable = cleared && !claimed;
            string status = claimed ? "已领取"
                : cleared ? "已通关"
                : current ? "可挑战"
                : "未开启";
            AddRow(claimed ? doneRows : rows, isl.name, status,
                "奖励 金币\u00d7" + isl.goldReward + "＋天赋石\u00d7" + isl.talentReward,
                !cleared && !current,
                cleared ? "已通关「" + isl.name + "」" : "通关「" + isl.name + "」",
                questId: id.ToString(), questMain: false,
                claimed: claimed, claimable: claimable);
        }
        rows.AddRange(doneRows);

        title = currentSide != null ? currentSide.Value.name : "支线岛";
        // 2026-11-01 主人拍板：副标题一行短话，别写「暂无待打支线」这类绕口文案
        desc = "支线简介";
        objective = currentSide != null
            ? "通关「" + currentSide.Value.name + "」（体力" + SideIslandDefs.SIDE_ISLAND_STAMINA_COST + "）"
            : "通关对应篇章后开启";
        progress = clearedCount + "/" + total;
    }

    void FillMonsters(out string title, out string desc, out string objective, out string progress, List<LogRow> rows)
    {
        var list = AdventureLogCatalog.Monsters;
        int unlocked = 0;
        AdventureLogCatalog.MonsterEntry current = default;
        bool hasCurrent = false;
        for (int i = 0; i < list.Length; i++)
        {
            var e = list[i];
            bool on = AdventureLogCatalog.MonsterUnlocked(e);
            if (on)
            {
                unlocked++;
                if (!hasCurrent) { current = e; hasCurrent = true; }
            }
            string tag = e.Kind == "首领" ? "【Boss】" : e.Kind;
            string status = on ? tag : (e.LaterChapter ? "后续层" : e.Unlock);
            if (e.Kind == "首领" && !on)
                status = status + " 【Boss】";
            AddRow(rows, on ? e.Name : "？？？",
                status,
                on ? e.Desc + "\n" + e.Lore : (e.LaterChapter ? "在后续裂隙层中可解锁。" : "解锁条件：" + e.Unlock),
                !on,
                on ? e.Place : "尚未解锁");
        }
        title = hasCurrent ? current.Name : "怪物图鉴";
        desc = hasCurrent ? current.Desc + "\n" + current.Lore : "本版描述偏趣闻与冒险者口耳相传，不代表公会官方立场。";
        objective = hasCurrent ? current.Place : "在裂隙中遭遇并记录";
        progress = unlocked + "/" + list.Length;
    }

    void FillMerc(out string title, out string desc, out string objective, out string progress, List<LogRow> rows)
    {
        var list = AdventureLogCatalog.Mercs;
        int unlocked = 0;
        AdventureLogCatalog.MercEntry current = default;
        bool hasCurrent = false;
        for (int i = 0; i < list.Length; i++)
        {
            var e = list[i];
            bool on = AdventureLogCatalog.MercUnlocked(e);
            if (on)
            {
                unlocked++;
                if (!hasCurrent) { current = e; hasCurrent = true; }
            }
            string label = e.Name;
            if (!string.IsNullOrEmpty(e.Nickname))
                label = e.Name + " · " + e.Nickname;
            AddRow(rows, on ? label : "？？？",
                on ? e.Role : e.Unlock,
                on ? e.Desc + "\n" + e.Lore : "解锁条件：" + e.Unlock,
                !on,
                on ? e.Place : "尚未解锁");
        }
        string curTitle = "佣兵与角色";
        if (hasCurrent)
        {
            curTitle = current.Name;
            if (!string.IsNullOrEmpty(current.Nickname))
                curTitle = current.Name + " · " + current.Nickname;
        }
        title = curTitle;
        desc = hasCurrent ? current.Desc + "\n" + current.Lore : "剧情角色随主线解锁；酒馆招募后记入图鉴。";
        objective = hasCurrent ? current.Place : "在酒馆完成招募";
        progress = unlocked + "/" + list.Length;
    }

    void FillAchievement(out string title, out string desc, out string objective, out string progress, List<LogRow> rows)
    {
        AdventureLogAchievements.EvaluateAll();
        var list = AdventureLogCatalog.Achievements;
        // 【2026-11-07 主人拍板】已领取的条沉到列表最下面：先按原表顺序分两批，末尾合并（稳定，不打乱其余顺序）。
        var claimedRows = new List<LogRow>();
        int unlocked = 0;
        int claimable = 0;
        AdventureLogCatalog.AchEntry current = default;
        bool hasCurrent = false;
        for (int i = 0; i < list.Length; i++)
        {
            var e = list[i];
            bool done = AdventureLogAchievements.IsCompleted(e.Id) || AdventureLogCatalog.AchUnlocked(e);
            bool claimed = AdventureLogAchievements.IsClaimed(e.Id);
            bool canClaim = AdventureLogAchievements.CanClaim(e.Id);
            if (done) unlocked++;
            if (canClaim) claimable++;
            if (done && !claimed) { current = e; hasCurrent = true; }
            else if (done && !hasCurrent) { current = e; hasCurrent = true; }

            string prog;
            if (claimed) prog = "已领取";
            else if (canClaim) prog = "可领取";
            else if (done) prog = "已完成";
            else prog = AdventureLogAchievements.FormatProgress(e.Id);

            // 2026-10-31 主人报「成就没写是什么、怎么做什么的」：行标题不再用？？？遮蔽，
            // 未完成成就直接显示名字；点击卡片 = 描述(分类+描述) / 目标(达成条件) / 进度(含计数)。
            string detail = done
                ? e.Desc + "\n奖励：" + AdventureLogAchievements.GetReward(e.Id).Label
                : e.Category + "　" + e.Desc;
            // 【2026-11-07 主人拍板】可领取的条右上角挂红点；已领取的条整条压暗，并排到最后面。
            AddRow(claimed ? claimedRows : rows, e.Name, prog, detail, !done, e.Unlock, e.Id,
                claimed: claimed, claimable: canClaim);
        }
        rows.AddRange(claimedRows);
        title = hasCurrent ? current.Name : "成就";
        desc = hasCurrent
            ? current.Desc
            : "成长、战斗、收集、养成、探索。达成后请在此领取奖励。";
        // 2026-10-31 校对：原先这里显示 AdventureLogMileage.FormatStatusLine()（里程状态，
        // 属世界页口径，放成就页错位）。2026-11-07 起改为下方「成就达成 x/y」。
        // 2026-11-07 主人拍板：当前目标改「成就达成 x/y」，进度并到同一行，
        // 可领重复数字不再拼在进度槽；只动这一处，其它页签进度不变。
        objective = "成就达成 " + unlocked + "/" + list.Length;
        progress = "";
    }

    void FillWorld(out string title, out string desc, out string objective, out string progress, List<LogRow> rows)
    {
        var list = AdventureLogCatalog.World;
        int unlocked = 0;
        AdventureLogCatalog.WorldEntry current = default;
        bool hasCurrent = false;
        for (int i = 0; i < list.Length; i++)
        {
            var e = list[i];
            bool on = AdventureLogCatalog.WorldUnlocked(e);
            if (on)
            {
                unlocked++;
                if (!hasCurrent) { current = e; hasCurrent = true; }
            }
            // 2026-11-01 主人报「点条后信息不对（组织/组织）」：行目标槽原先也放 Category，
            // 与右侧状态字同词重复。改为显示解锁/来源条件（如「通关第一章后解锁」），
            // 右侧状态字保持分类（世界观/组织/地点/物品）。
            AddRow(rows, on ? e.Name : "？？？",
                on ? e.Category : e.Unlock,
                on ? e.Desc + "\n" + e.Flavor : "解锁条件：" + e.Unlock,
                !on,
                on ? e.Unlock : "尚未解锁", null, i);
        }
        title = hasCurrent ? current.Name : "世界图鉴";
        desc = hasCurrent ? current.Desc + "\n" + current.Flavor : "世界观、地点、组织、物品与传说。";
        // 2026-11-01 主人报「信息还是不对」：
        // ① 「当前目标」原先放当前条目的分类（组织/世界观），与目标槽语义不符 ——
        //    改为第一个未解锁条目的达成条件（与主线/支线页同口径），集齐后「世界记录已集齐」。
        // ② 进度槽原先拼碎片清单换行文本（森林残页0…），窄框装不下挤成一团 —— 只留 x/y；
        //    碎片/里程在世界页签的 Phase3 面板里已有展示。
        objective = NextGoal(list, AdventureLogCatalog.WorldUnlocked, e => e.Unlock)
                    ?? "世界记录已集齐";
        progress = unlocked + "/" + list.Length;
    }

    static void AddRow(List<LogRow> rows, string title, string progress, string detail, bool locked,
        string objective, string achId = null, int worldIndex = -1,
        string questId = null, bool questMain = false, bool claimed = false, bool claimable = false)
    {
        rows.Add(new LogRow { Title = title, Progress = progress, Detail = detail,
            Objective = objective, Locked = locked, AchId = achId, WorldIndex = worldIndex,
            QuestId = questId, QuestMain = questMain, Claimed = claimed, Claimable = claimable });
    }

    /// <summary>第一个未达成条目的目标文案；全部达成返回 null。</summary>
    static string NextGoal<T>(T[] list, Func<T, bool> reached, Func<T, string> goal)
    {
        for (int i = 0; i < list.Length; i++)
            if (!reached(list[i]))
                return goal(list[i]);
        return null;
    }

    // 2026-10-31 主人报「这些字有点小」：只调字号不动框（字号属允许项）。
    // 2026-11-01 主人再报「字还是有点小」：整体再上一档 —— 图上标题 22→26、
    // 图上描述 20→24、当前目标正文 22→24、当前目标小标 16→18、行任务名 22→24、
    // 行状态字 18→20（20 为「首次击败森之守护者」这类长条件折行的上限，不再加）。
    void ApplyReadableFontSizes()
    {
        var paperT = transform.Find("Root/Frame/Paper");
        if (paperT == null) return;
        SetFontSize(paperT.Find("ActiveCard/ActiveTitle"), 26);
        SetFontSize(paperT.Find("ActiveCard/ActiveDesc"), 24);
        SetFontSize(paperT.Find("ActiveCard/Art/Objective"), 24);

        // 「当前目标」小节标：节点名是 Text (Legacy) 且预制体文案写死，按文案找更稳。
        var card = paperT.Find("ActiveCard");
        if (card != null)
        {
            var texts = card.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
                if (texts[i] != null && texts[i].text == "当前目标")
                    texts[i].fontSize = 18;
        }

        // 行模板（已完成）：任务名。从列表 Content 下找，避免撞上
        // Paper1 里隐藏的同名模板（PrepareDoneTemplate 已把可见那份挂到 Content）。
        var done = _listContent != null ? _listContent.Find("已完成") : null;
        if (done != null)
        {
            SetFontSize(done.Find("Objective"), 24);
            var prog = done.Find("Progress")?.GetComponent<Text>();
            if (prog != null && !prog.resizeTextForBestFit) prog.fontSize = 20;
        }
    }

    static void SetFontSize(Transform t, int size)
    {
        if (t == null) return;
        var txt = t.GetComponent<Text>() ?? t.GetComponentInChildren<Text>(true);
        if (txt != null) txt.fontSize = size;
        else
            Debug.LogError("[AdventureLogUI] 调字号找不到 Text 节点：" + t.name);
    }

    void RebuildRows(List<LogRow> rows)
    {
        if (_rowTemplate == null || _listContent == null) return;

        // 行数一致时复用已有行，只更新文本与点击回调。
        // 之前每次切页都整批 Destroy + Instantiate（几十行），是底栏切页卡顿的大头之一。
        if (_spawnedRows.Count == rows.Count && rows.Count > 0)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                var exist = _spawnedRows[i];
                if (exist == null) continue;
                if (!exist.activeSelf) exist.SetActive(true);
                ApplyRow(exist.transform, rows[i]);
                WireRowClick(exist, rows[i]);
            }
            return;
        }

        for (int i = 0; i < _spawnedRows.Count; i++)
        {
            if (_spawnedRows[i] != null)
                Destroy(_spawnedRows[i]);
        }
        _spawnedRows.Clear();

        for (int i = 0; i < rows.Count; i++)
        {
            var go = Instantiate(_rowTemplate, _listContent, false);
            go.name = "DoneRow" + i;
            go.SetActive(true);
            ApplyRow(go.transform, rows[i]);
            WireRowClick(go, rows[i]);
            _spawnedRows.Add(go);
        }
    }

    void WireRowClick(GameObject go, LogRow data)
    {
        var btn = go.GetComponent<Button>();
        if (btn == null) btn = go.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        LogRow copy = data;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() =>
        {
            // 2026-11-01 主人拍板：世界页点条目不再写顶部卡（顶部插图只做装饰），
            // 详情改走 CodexInfoPopupUI 怪物信息弹窗（分类/描述/传说/解锁条件写全）。
            if (copy.WorldIndex >= 0)
            {
                OpenWorldPopup(copy.WorldIndex);
                return;
            }
            // 2026-11-01 主人拍板：主线/支线任务行点击弹世界页同款详情弹窗，
            // 写清「达成方式」（怎么去完成）与进度；未解锁行补先推进前面进度的提示。
            if (!string.IsNullOrEmpty(copy.QuestId))
            {
                OpenQuestPopup(copy.QuestMain, copy.QuestId);
                return;
            }
            // 2026-10-31 主人报「点任务条后当前目标只显示进行中」：旧写法把进度字同时塞进
            // 目标槽和进度槽。改为行自带 Objective（是什么/怎么做的条件），进度槽照常显状态。
            // 【同日晚补充】主人报「长条件折行第二行只有一个字」：卡片进度槽是窄框（约 160px），
            // 未解锁行不能再把长解锁条件塞进去 —— 进度槽只放短状态，解锁条件留在描述字幕里。
            ApplyActiveCard(copy.Title, copy.Detail, copy.Objective,
                copy.Locked ? "" : copy.Progress);
            if (!string.IsNullOrEmpty(copy.AchId) && AdventureLogAchievements.CanClaim(copy.AchId))
            {
                if (AdventureLogAchievements.Claim(copy.AchId))
                    RefreshBody();
            }
        });

        var claimBtnTr = go.transform.Find("Progress/ClaimButton");
        if (claimBtnTr != null)
        {
            var claimBtn = claimBtnTr.GetComponent<Button>();
            if (claimBtn != null)
            {
                claimBtn.onClick.RemoveAllListeners();
                if (copy.Claimable && !string.IsNullOrEmpty(copy.QuestId))
                {
                    claimBtnTr.gameObject.SetActive(true);
                    claimBtn.onClick.AddListener(() =>
                    {
                        bool ok;
                        if (copy.QuestMain)
                        {
                            ok = QuestRewardClaim.ClaimMain(copy.QuestId);
                        }
                        else
                        {
                            if (!int.TryParse(copy.QuestId, out int islandId))
                            {
                                Debug.LogError("[AdventureLogUI] 支线任务 id 非数字，无法领奖：" + copy.QuestId);
                                return;
                            }
                            ok = QuestRewardClaim.ClaimSide(islandId);
                        }
                        if (ok)
                        {
                            RefreshBody();
                            RedDot.RefreshCommon();
                        }
                    });
                }
                else
                {
                    claimBtnTr.gameObject.SetActive(false);
                }
            }
        }
    }

    /// <summary>
    /// 2026-11-01 主人拍板：世界页点条目改弹 CodexInfoPopupUI（怪物信息弹窗同款），
    /// 分类进 meta，描述进 desc，传说 + 解锁条件进 lore；未解锁只给解锁条件。
    /// 【同日晚追加】主人拍板「不要图标」：topIconPath 传空串收起头部图标。
    /// </summary>
    void OpenWorldPopup(int index)
    {
        var list = AdventureLogCatalog.World;
        if (index < 0 || index >= list.Length) return;
        var e = list[index];
        bool on = AdventureLogCatalog.WorldUnlocked(e);
        string title = on ? e.Name : "？？？";
        string desc = on ? e.Desc : "尚未记录该条目。";
        string lore = on
            ? e.Flavor + "\n\n解锁条件：" + e.Unlock
            : "解锁条件：" + e.Unlock;
        CodexInfoPopupUI.Show(title, "分类：" + e.Category, desc, lore, null,
            codexKind: null, topTitleOverride: "世界图鉴", topIconPath: "");
    }

    /// <summary>奖励 icon 胶数据：金币/天赋石走 CommonIconSprites 统一出口；数值 ≤0 不出胶（2026-11-01 主人要求「奖励的字用图标代替」）。</summary>
    static CodexInfoPopupUI.RewardChip[] BuildRewardChips(int gold, int stones)
    {
        var list = new List<CodexInfoPopupUI.RewardChip>();
        if (gold > 0)
            list.Add(new CodexInfoPopupUI.RewardChip
            {
                Icon = CommonIconSprites.Load("icon_gold"),
                IconPath = "icon_gold",
                Label = "×" + gold
            });
        if (stones > 0)
            list.Add(new CodexInfoPopupUI.RewardChip
            {
                Icon = CommonIconSprites.Load("icon_talent_stone"),
                IconPath = "icon_talent_stone",
                Label = "×" + stones
            });
        return list.Count > 0 ? list.ToArray() : null;
    }

    /// <summary>
    /// 主线弹窗详情文案（2026-11-03 主人「详情太短不好看」）：
    /// 目标句沿用 <see cref="MainQuestDefs.AutoDesc"/>（任务表唯一真源，不复制机制判定），
    /// 后面补一句完成方式，让详情有 2 行、不再是孤零零 4 个字。
    /// </summary>
    static string QuestPopupDetail(MainQuestDef def, string objective)
    {
        string hint;
        switch (def.type)
        {
            case MainQuestType.ClearStage:
            case MainQuestType.ClearStages:
                hint = "击败关卡内全部敌人即可完成";
                break;
            case MainQuestType.TalkNpc:
                hint = "对话结束后自动完成";
                break;
            case MainQuestType.WatchStory:
                hint = "在剧情回顾中重看一遍即可完成";
                break;
            case MainQuestType.RecruitMerc:
                hint = "任意品质的佣兵都计入";
                break;
            case MainQuestType.UpgradeTalent:
                hint = "任意一次加点都计入";
                break;
            default:
                hint = "完成后在冒险日志领取奖励";
                break;
        }
        return string.IsNullOrEmpty(objective) ? hint : objective + "\n\n" + hint;
    }

    /// <summary>
    /// 2026-11-01 主人拍板「不能有两套主线支线」：主线/支线行数据已与任务栏同源，
    /// 点击弹 CodexInfoPopupUI（世界页同款）：达成方式/进度/奖励写全。
    /// 【同日晚主人要求】排版优化＋奖励的字用图标代替：奖励不再写「奖励：金币×N」文字行，
    /// 改弹窗内 icon×N 奖励条（desc 与说明之间）；lore 只留进度/入口/锁定提示。
    /// 未开启的主线补「先完成当前章节的任务」、未开启的支线补「通关对应篇章后开启」。
    /// </summary>
    void OpenQuestPopup(bool isMain, string questId)
    {
        if (isMain)
        {
            if (!MainQuestDefs.TryFind(questId, out var def)) return;
            bool done = MainQuestSystem.IsDone(def);
            string how = MainQuestDefs.AutoDesc(def);
            // 【2026-11-03 主人「详情太短不好看」】弹窗详情 = 目标句（AutoDesc 单一真源）+ 一句完成方式。
            // 只补弹窗文案，任务栏/日志行仍用 AutoDesc 原句，别处一个字不变。
            string detail = QuestPopupDetail(def, how);
            string name = string.IsNullOrEmpty(def.title) ? how : def.title;
            var views = MainQuestSystem.ChapterViews(def.chapter);
            MainQuestView v = null;
            for (int i = 0; i < views.Count; i++)
                if (views[i].def.id == questId) { v = views[i]; break; }
            string lore;
            if (done)
            {
                lore = QuestRewardClaim.IsClaimedMain(questId)
                    ? "已领取奖励"
                    : "已完成，可在列表右侧领取奖励";
            }
            else
            {
                // 奖励改图标条后，这里只留进度与未开启提示（有则各占一行）
                var lines = new List<string>();
                if (v != null && v.need > 1) lines.Add("进度 " + v.progress + "/" + v.need);
                if (def.chapter > MainQuestSystem.CurrentChapter()) lines.Add("先完成当前章节的任务");
                lore = string.Join("\n", lines);
            }
            CodexInfoPopupUI.Show(name, "", detail, lore, null,
                codexKind: null, topTitleOverride: "主线任务", topIconPath: "",
                rewards: BuildRewardChips(def.rewardGold, def.rewardStones), taskLayout: true);
        }
        else
        {
            int id;
            if (!int.TryParse(questId, out id)) return;
            var isl = SideIslandDefs.Get(id);
            if (isl == null)
            {
                Debug.LogError("[AdventureLogUI] SideIslandDefs 缺岛 id=" + id);
                return;
            }
            var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
            bool cleared = SideIslandDefs.IsCleared(id, data);
            var currentSide = QuestBarData.CurrentSideIsland();
            bool current = currentSide != null && currentSide.Value.islandId == id;
            string lore;
            if (cleared)
            {
                lore = QuestRewardClaim.IsClaimedSide(id)
                    ? "已领取奖励"
                    : "已通关，可在列表右侧领取奖励";
            }
            else
            {
                // 2026-11-01 主人拍板：短行直白——入口/锁定提示各占一行；奖励改图标条
                lore = "进入：冒险页 · 支线岛槽（体力" + SideIslandDefs.SIDE_ISLAND_STAMINA_COST + "）";
                if (!current)
                    lore += "\n通关对应篇章后开启";
            }
            // 【2026-11-03 主人「详情太短不好看」】支线详情加长：补上所属篇章，再补一句完成方式
            string sideDesc = "通关第" + isl.act + "篇的支线岛「" + isl.name + "」\n进入岛内打通即可完成";
            CodexInfoPopupUI.Show(isl.name, "", sideDesc, lore, null,
                codexKind: null, topTitleOverride: "支线任务", topIconPath: "",
                rewards: BuildRewardChips(isl.goldReward, isl.talentReward), taskLayout: true);
        }
    }

    void ApplyActiveCard(string title, string desc, string objective, string progress)
    {
        bool deco = !IsCodexTab(_tab);
        if (_activeTitle != null) _activeTitle.text = deco ? "" : (title ?? "");
        if (_activeDesc != null) _activeDesc.text = deco ? "" : (desc ?? "");
        if (_activeObj != null) _activeObj.text = objective ?? "";
        if (_activeProg != null) _activeProg.text = progress ?? "";
    }

    static void ApplyRow(Transform row, LogRow data)
    {
        var header = row.Find("di");
        // 2026-10-31 校对：Paper/bg/OngoingHeader 顶部木牌修好绑定后会正确显示页签名，
        // 行头这块小牌（预制体写死「已完成」残留）再显示页签名就重复出现两块「主线」；
        // 主人上午也报过它标错 —— 直接隐藏，页签名由顶部木牌统一承担。
        if (header != null) header.gameObject.SetActive(false);
        var obj = row.Find("Objective")?.GetComponent<Text>();
        if (obj != null)
            obj.text = data.Title;
        var prog = row.Find("Progress")?.GetComponent<Text>();
        if (prog != null)
            prog.text = data.Progress ?? "";
        ApplyRowState(row, data);
    }

    /// <summary>
    /// 【2026-11-07 主人拍板】成就行状态：可领取 → 右上角红点；已领取 → 整条压暗并排在列表最下面（排序在 FillAchievement）。
    /// 红点图走 RedDot.Sprite 唯一出口；压暗用 CanvasGroup（同 AdMercEventUI 口径，不改预制体底图 / 文字色）。
    /// 行是复用的（RebuildRows 行数一致时只刷数据），所以每次都按当前数据重设，切页 / 领取后不残留。
    /// </summary>
    static void ApplyRowState(Transform row, LogRow data)
    {
        // 主线/支线任务行：可领取时右侧「可领取」按钮铺满 Progress 节点，状态字让位；
        // 成就行红点逻辑保持原样，仅处理成就行。
        bool questRow = !string.IsNullOrEmpty(data.QuestId);
        bool showClaim = data.Claimable && questRow;
        var prog = row.Find("Progress");
        var progText = prog != null ? prog.GetComponent<Text>() : null;
        if (showClaim && prog != null)
        {
            var claimBtn = prog.Find("ClaimButton");
            if (claimBtn == null)
            {
                var go = new GameObject("ClaimButton", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(prog, false);
                var brt = (RectTransform)go.transform;
                brt.anchorMin = Vector2.zero;
                brt.anchorMax = Vector2.one;
                brt.offsetMin = Vector2.zero;
                brt.offsetMax = Vector2.zero;
                var bimg = go.GetComponent<Image>();
                bimg.color = new Color(1f, 0.84f, 0.3f, 0.5f);
                bimg.raycastTarget = true;
                var bbtn = go.GetComponent<Button>();
                bbtn.transition = Selectable.Transition.None;

                var label = new GameObject("Text", typeof(RectTransform), typeof(Text));
                label.transform.SetParent(go.transform, false);
                var lrt = (RectTransform)label.transform;
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.one;
                lrt.offsetMin = Vector2.zero;
                lrt.offsetMax = Vector2.zero;
                var lt = label.GetComponent<Text>();
                lt.font = GameFonts.GetChinese();
                lt.text = "可领取";
                lt.fontSize = 20;
                lt.alignment = TextAnchor.MiddleCenter;
                lt.raycastTarget = false;
                claimBtn = go.transform;
            }
            claimBtn.gameObject.SetActive(true);
            if (progText != null)
            {
                progText.enabled = false;
                progText.raycastTarget = false;
            }
        }
        else
        {
            var stale = prog != null ? prog.Find("ClaimButton") : null;
            if (stale != null) stale.gameObject.SetActive(false);
            if (progText != null)
            {
                progText.enabled = true;
                progText.raycastTarget = true;
            }
        }

        // 任务行的可领状态已由按钮消费，置 false 后下面原样的红点分支只作用于成就行，
        // 也不会给主线/支线行挂红点。
        if (questRow) data.Claimable = false;

        var red = row.Find("ClaimRedDot");
        if (red == null && data.Claimable)
        {
            var go = new GameObject("ClaimRedDot", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(row, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(-16f, -16f);
            rt.sizeDelta = new Vector2(RedDot.DefaultSize, RedDot.DefaultSize);
            var img = go.GetComponent<Image>();
            img.sprite = RedDot.Sprite;
            img.color = Color.white;
            img.raycastTarget = false;
            img.preserveAspect = true;   // 与 UIRedDot 同款，不拉伸红点图
            red = go.transform;
        }
        if (red != null) red.gameObject.SetActive(data.Claimable);

        var cg = row.GetComponent<CanvasGroup>();
        if (cg == null) cg = row.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = data.Claimed ? 0.55f : 1f;
    }

    Transform FindTransform(string name)
    {
        if (_root == null) return null;
        var direct = _root.transform.Find(name);
        if (direct != null) return direct;
        var all = _root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].name == name)
                return all[i];
        return null;
    }

    Text FindText(string name)
    {
        return FindTransform(name)?.GetComponent<Text>();
    }
}
