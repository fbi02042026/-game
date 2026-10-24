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
        public bool Locked;
        public string AchId;
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
    // 标题/描述改挂 mask（字随图走）只做一次
    bool _captionFollowsArt;
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
        _ongoingHeader = _paper != null
            ? _paper.transform.Find("OngoingHeader")?.GetComponent<Text>()
            : null;
        EnsureCaptionFollowsArt();
        EnsurePaperTopLayout();
        BindAchievementClaimButton();
        _activeCard = FindTransform("ActiveCard")?.gameObject;
        var frame = transform.Find("Root/Frame");
        if (frame != null)
            _codex = new AdventureLogCodexPanel(frame);
        HidePaper1Template();
        PrepareDoneTemplate();
        EnsureLogChromeUsable();
        WireTabs();
        BindSidebarTabIcons();
        WireButtons();
        BindRewardRedDots();
        BindTabIllustration();
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
            _tabIllustration.preserveAspect = true;
            _tabIllustration.SetNativeSize();
        }
        else
            Debug.LogWarning($"[AdventureLogUI] 缺少标签插图 {key}");
    }

    bool IsCodexTab(LogTab tab) => tab == LogTab.Monster || tab == LogTab.Merc;

    /// <summary>
    /// 2026-10-22 主人拍板「字随图走」：ActiveTitle/ActiveDesc 原本挂在 ActiveCard 的
    /// 比例锚点上，而 ActiveCard/mask 是定尺寸 440.6×274.7、插图 SetNativeSize 后也不随
    /// 屏幕伸缩 —— 瘦长屏上比例锚点的字整体上漂，与图错位。这里把两条字幕改挂到 mask 下、
    /// 按 mask 比例锚定（标题占上带 0.64~0.96，描述占中带 0.26~0.62），图走字跟着走。
    /// 仅运行时改挂，不动预制体文件；只处理正式 Paper，Paper1 模板整页隐藏不碰。
    /// </summary>
    void EnsureCaptionFollowsArt()
    {
        if (_captionFollowsArt) return;
        var card = transform.Find("Root/Frame/Paper/ActiveCard");
        var mask = card != null ? card.Find("mask") : null;
        if (mask == null)
        {
            Debug.LogError("[AdventureLogUI] 找不到 Root/Frame/Paper/ActiveCard/mask，标题/描述无法跟图走");
            return;
        }
        if (ReparentCaptionUnderMask(card.Find("ActiveTitle"), mask, 0.64f, 0.96f) |
            ReparentCaptionUnderMask(card.Find("ActiveDesc"), mask, 0.26f, 0.62f))
            _captionFollowsArt = true;
    }

    static bool ReparentCaptionUnderMask(Transform node, Transform mask, float yMin, float yMax)
    {
        if (node == null)
        {
            Debug.LogError("[AdventureLogUI] ActiveCard 下缺少 ActiveTitle/ActiveDesc，字幕跟图走失败");
            return false;
        }
        node.SetParent(mask, false);
        var rt = (RectTransform)node;
        rt.anchorMin = new Vector2(0.05f, yMin);
        rt.anchorMax = new Vector2(0.97f, yMax);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return true;
    }

    /// <summary>
    /// 2026-10-24 主人报「每个标签的页面上面都空了一截」：Paper 顶部的灰页签(Paper/bg，
    /// 内含 OngoingHeader 页签名)与插图(Paper/ActiveCard/mask)在预制体里按 1280 设计稿的
    /// 绝对偏移 + 中心锚点摆位（bg +408 / mask +72）。运行时 Frame 拉高后（逻辑高 1600、
    /// Paper 高约 1296）纸顶到纸中心距离变大，这两块整体从纸顶下坠 100~160px：插图顶距
    /// 纸顶约 210（设计稿约 120），页签坠进插图背后被盖住。这里把两块改挂纸顶锚点，恢复
    /// 设计稿的贴顶节奏：页签 bg 距纸顶 58，插图 mask 顶高出卡片顶 20（与设计稿一致）。
    /// 只动 y 锚点与偏移，不动尺寸/字号/图；字幕(ActiveTitle/ActiveDesc)已挂 mask 下按比例
    /// 锚定，图走字跟着走，不受影响。
    /// </summary>
    void EnsurePaperTopLayout()
    {
        var labelBg = transform.Find("Root/Frame/Paper/bg") as RectTransform;
        if (labelBg != null)
        {
            labelBg.anchorMin = new Vector2(0.5f, 1f);
            labelBg.anchorMax = new Vector2(0.5f, 1f);
            labelBg.pivot = new Vector2(0.5f, 1f);
            labelBg.anchoredPosition = new Vector2(labelBg.anchoredPosition.x, -58f);
        }
        else
            Debug.LogError("[AdventureLogUI] 找不到 Root/Frame/Paper/bg，页签贴顶修复跳过");

        var card = transform.Find("Root/Frame/Paper/ActiveCard") as RectTransform;
        var mask = card != null ? card.Find("mask") as RectTransform : null;
        if (mask == null)
        {
            Debug.LogError("[AdventureLogUI] 找不到 Root/Frame/Paper/ActiveCard/mask，插图贴顶修复跳过");
            return;
        }
        mask.anchorMin = new Vector2(0.5f, 1f);
        mask.anchorMax = new Vector2(0.5f, 1f);
        mask.pivot = new Vector2(0.5f, 1f);
        mask.anchoredPosition = new Vector2(mask.anchoredPosition.x, 20f);
    }

    void ApplyModeVisibility()
    {
        bool codex = IsCodexTab(_tab);
        if (_paper != null)
        {
            // 任务形态：Paper 列表；图鉴形态：隐藏列表区，保留 Paper 也可整隐
            var scroll = _paper.transform.Find("Scroll");
            var ongoing = _paper.transform.Find("OngoingHeader");
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
    /// 会跟着 Frame 中心上漂，这里钉回 Frame 左上角。
    /// 2026-10-16 主人拍板：左侧标签按预制体排版走 —— Tabs 容器与 Tab0~5 的
    /// 锚点/位置/尺寸一律不再被代码改写（9-19 引入、10-15 调整的运行时重排已整段移除）。
    /// </summary>
    void EnsureSidebarLayout()
    {
        var frame = transform.Find("Root/Frame") as RectTransform;
        if (frame == null) return;

        // 标题横幅：钉在 Frame 左上（biaotou 中心对 bg 中心）
        // 2026-10-23 主人拍板：横幅高度以预制体 bg 实测为准，不再写死 103
        float headerTopPad = 10f;
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
        biaotou.anchoredPosition = new Vector2(12f + bgTr.rect.width * 0.5f, -(headerTopPad + headerH * 0.5f));
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
                int n = AdventureLogAchievements.ClaimAll();
                if (n > 0)
                    UIManager.Instance?.ShowToast($"领取 {n} 个成就奖励");
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
        if (_claim != null)
            RedDot.Bind(_claim.transform, RedDot.AchClaim, new Vector2(-8f, -8f));
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

        if (_activeTitle != null) _activeTitle.text = title;
        if (_activeDesc != null) _activeDesc.text = desc;
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
        var list = AdventureLogCatalog.Main;
        int unlocked = 0;
        AdventureLogCatalog.StoryEntry current = default;
        bool hasCurrent = false;
        for (int i = 0; i < list.Length; i++)
        {
            var e = list[i];
            bool on = AdventureLogCatalog.MainUnlocked(e);
            if (on)
            {
                unlocked++;
                current = e;
                hasCurrent = true;
            }
            string extra = e.Id == "C1" && StoryProgress.Chapter1ChoiceDone
                ? "石碑选择：" + (StoryProgress.GetChoice(1) ?? "")
                : e.Extra;
            AddRow(rows, on ? e.Title : "？？？",
                on ? "已记录" : e.Unlock,
                on ? e.Summary + "\n" + extra : "解锁条件：" + e.Unlock,
                !on);
        }
        title = hasCurrent ? current.Title : "主线故事";
        desc = hasCurrent ? current.Summary : "完成引导后将在此记录章节摘要。";
        objective = hasCurrent ? current.Extra : "完成见习委托";
        progress = unlocked + "/" + list.Length;
    }

    void FillSide(out string title, out string desc, out string objective, out string progress, List<LogRow> rows)
    {
        var list = AdventureLogCatalog.Side;
        int unlocked = 0;
        AdventureLogCatalog.StoryEntry current = default;
        bool hasCurrent = false;
        for (int i = 0; i < list.Length; i++)
        {
            var e = list[i];
            bool on = AdventureLogCatalog.SideUnlocked(e);
            if (on)
            {
                unlocked++;
                current = e;
                hasCurrent = true;
            }
            AddRow(rows, on ? e.Title : "？？？",
                on ? "已完成" : e.Unlock,
                on ? e.Summary + "\n" + e.Extra : "解锁条件：" + e.Unlock,
                !on);
        }
        title = hasCurrent ? current.Title : "支线故事";
        desc = hasCurrent ? current.Summary : "支线通过佣兵参战、NPC 对话或关卡掉落触发。";
        objective = hasCurrent ? current.Extra : "继续冒险以解锁支线";
        progress = unlocked + "/" + list.Length;
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
                !on);
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
                !on);
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

            string detail = done
                ? e.Desc + "\n奖励：" + AdventureLogAchievements.GetReward(e.Id).Label
                : e.Category + "　解锁条件：" + e.Unlock;
            AddRow(rows, done ? e.Name : "？？？", prog, detail, !done, e.Id);
        }
        title = hasCurrent ? current.Name : "成就";
        desc = hasCurrent
            ? current.Desc
            : "成长、战斗、收集、养成、探索。达成后请在此领取奖励。";
        objective = AdventureLogMileage.FormatStatusLine();
        progress = unlocked + "/" + list.Length
                   + (claimable > 0 ? $"  可领×{claimable}" : "");
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
            AddRow(rows, on ? e.Name : "？？？",
                on ? e.Category : e.Unlock,
                on ? e.Desc + "\n" + e.Flavor : "解锁条件：" + e.Unlock,
                !on);
        }
        title = hasCurrent ? current.Name : "世界图鉴";
        desc = hasCurrent ? current.Desc + "\n" + current.Flavor : "世界观、地点、组织、物品与传说。";
        objective = hasCurrent ? current.Category : "到达对应节点后解锁";
        progress = unlocked + "/" + list.Length + "\n" + AdventureLogFragments.FormatInventory();
    }

    static void AddRow(List<LogRow> rows, string title, string progress, string detail, bool locked, string achId = null)
    {
        rows.Add(new LogRow { Title = title, Progress = progress, Detail = detail, Locked = locked, AchId = achId });
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
                ApplyRow(exist.transform, rows[i], showHeader: i == 0);
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
            ApplyRow(go.transform, rows[i], showHeader: i == 0);
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
            ApplyActiveCard(copy.Title, copy.Detail, copy.Progress, copy.Progress);
            if (!string.IsNullOrEmpty(copy.AchId) && AdventureLogAchievements.CanClaim(copy.AchId))
            {
                if (AdventureLogAchievements.Claim(copy.AchId))
                    RefreshBody();
            }
        });
    }

    void ApplyActiveCard(string title, string desc, string objective, string progress)
    {
        if (_activeTitle != null) _activeTitle.text = title ?? "";
        if (_activeDesc != null) _activeDesc.text = desc ?? "";
        if (_activeObj != null) _activeObj.text = objective ?? "";
        if (_activeProg != null) _activeProg.text = progress ?? "";
    }

    static void ApplyRow(Transform row, LogRow data, bool showHeader)
    {
        var header = row.Find("di");
        if (header != null) header.gameObject.SetActive(showHeader);
        var obj = row.Find("Objective")?.GetComponent<Text>();
        if (obj != null)
            obj.text = data.Title;
        var prog = row.Find("Progress")?.GetComponent<Text>();
        if (prog != null)
            prog.text = data.Progress ?? "";
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
