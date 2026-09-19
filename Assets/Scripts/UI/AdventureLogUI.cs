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
    Transform _listContent;
    GameObject _claim;
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
    const float FrameTopReserveLogOpen = 8f;
    const float FrameBottomReserve = 150f;
    const float FrameSidePad = 16f;

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
    /// 关闭钮在预制体里按顶栏留 120px 定位；日志全屏打开时顶距改为 8px，需把钮拉回外框右上角。
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

        _rowTemplate = done.gameObject;
        _rowTemplate.SetActive(false);

        // 「已完成/已记录」状态字兜底：预制体 Progress 框宽仅 8px 且锚出行右缘，
        // 文字靠溢出渲染，右半被 Scroll Viewport 的 Mask 裁掉。
        // 运行时把框收进行内右端并加 BestFit，不改预制体文件。
        var prog = done.Find("Progress");
        if (prog != null)
        {
            var prt = (RectTransform)prog;
            prt.anchorMin = new Vector2(1f, 0.5f);
            prt.anchorMax = new Vector2(1f, 0.5f);
            prt.pivot = new Vector2(1f, 0.5f);
            // y 略偏下（相对行中心 −10）：状态字在行内靠下时不容易被上边缘压住，整行都能露出来
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

        // 行标题同样兜底：预制体框是跨行全宽（还超出行左缘），长标题会横向溢出被 Mask 裁。
        // 左右分区：Objective 只占左段，右侧整段留给 Progress，互不覆盖。
        var obj = done.Find("Objective");
        if (obj != null)
        {
            var ort = (RectTransform)obj;
            ort.anchorMin = new Vector2(0f, 0.5f);
            ort.anchorMax = new Vector2(1f, 0.5f);
            ort.pivot = new Vector2(0.5f, 0.5f);
            ort.offsetMin = new Vector2(16f, 0f);
            ort.offsetMax = new Vector2(-300f, 0f);
            ort.anchoredPosition = Vector2.zero;
            var ot = obj.GetComponent<Text>();
            if (ot != null)
            {
                ot.alignment = TextAnchor.MiddleLeft;
                ot.horizontalOverflow = HorizontalWrapMode.Wrap;
            }
        }
    }

    /// <summary>
    /// 冒险日志可点性/标题自适应兜底（不改预制体）：
    /// 1) Paper / Paper1 是全屏拉伸容器，自身 Image 参与射线且渲染序压在 Sidebar 之上，
    ///    点击命中后沿父链找不到 Button 就被吞掉 —— 左侧标签永远点不到。关掉容器自身
    ///    的 raycast（子级滚动区/按钮/格子都是后代，不受影响）。
    /// 2) 标题「冒险日志」的文本框(约 150 宽)没跟随横幅 bg(291×103)，字号大时溢出错位。
    ///    文本框改为居中铺满横幅内缩区域，并开 BestFit 自适应字号。
    /// </summary>
    void EnsureLogChromeUsable()
    {
        DisableOwnRaycast(_paper);
        DisableOwnRaycast(transform.Find("Root/Frame/Paper1")?.gameObject);

        var biaotou = transform.Find("Root/Frame/biaotou");
        if (biaotou == null) return;

        var bgTr = biaotou.Find("bg");
        var bgImg = bgTr != null ? bgTr.GetComponent<Image>() : null;
        if (bgImg != null) bgImg.raycastTarget = false;

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
        if (titleTr == null) return;

        var t = titleTr.GetComponent<Text>();
        if (t != null) t.raycastTarget = false;

        var rt = (RectTransform)titleTr;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(260f, 78f); // 横幅 291×103 内缩留边

        if (t != null)
        {
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            int baseSize = Mathf.RoundToInt(t.fontSize);
            t.resizeTextMaxSize = Mathf.Clamp(baseSize, 20, 64);
            t.resizeTextMinSize = 18;
            if (t.resizeTextMinSize >= t.resizeTextMaxSize)
                t.resizeTextMinSize = Mathf.Max(10, t.resizeTextMaxSize / 2);
        }
    }

    static void DisableOwnRaycast(GameObject go)
    {
        if (go == null) return;
        var img = go.GetComponent<Image>();
        if (img != null) img.raycastTarget = false;
    }

    /// <summary>
    /// 侧栏布局兜底（不改预制体文件）：
    /// 预制体的标题横幅(biaotou)和 Tab 锚点是按旧 Frame 尺寸摆的，Frame 被运行时拉满后
    /// 横幅漂进标签区、把第一个「主线」标签顶出屏外。这里显式重排：
    /// 横幅钉在 Frame 左上角，Tab0~5 从横幅下方开始等距排到 Emblem 之上，任何分辨率都不重叠。
    /// </summary>
    void EnsureSidebarLayout()
    {
        var frame = transform.Find("Root/Frame") as RectTransform;
        var sidebar = frame != null ? frame.Find("Sidebar") as RectTransform : null;
        var tabs = sidebar != null ? sidebar.Find("Tabs") as RectTransform : null;
        if (frame == null || tabs == null) return;

        float frameH = frame.rect.height;
        if (frameH < 400f) return;

        // Tabs：占 Sidebar 左侧 300 宽、全高
        tabs.anchorMin = new Vector2(0f, 0f);
        tabs.anchorMax = new Vector2(0f, 1f);
        tabs.pivot = new Vector2(0f, 0.5f);
        tabs.offsetMin = new Vector2(0f, 0f);
        tabs.offsetMax = new Vector2(300f, 0f);

        // 标题横幅：钉在 Frame 左上（bg 291×103，biaotou 中心对 bg 中心）
        float headerH = 103f, headerTopPad = 10f;
        var biaotou = frame.Find("biaotou") as RectTransform;
        if (biaotou != null)
        {
            biaotou.anchorMin = biaotou.anchorMax = new Vector2(0f, 1f);
            biaotou.pivot = new Vector2(0.5f, 0.5f);
            biaotou.anchoredPosition = new Vector2(12f + 145.5f, -(headerTopPad + headerH * 0.5f));
            biaotou.SetAsLastSibling(); // 横幅最上，但已不与标签重叠
        }

        // 底部给徽章(Emblem)留白
        const float bottomReserve = 175f;
        float top = headerTopPad + headerH + 14f;
        float availH = frameH - top - bottomReserve;
        float minStep = 62f;
        float step = Mathf.Max(minStep, availH / TabNames.Length);

        for (int i = 0; i < TabNames.Length; i++)
        {
            var tab = tabs.Find("Tab" + i) as RectTransform;
            if (tab == null) continue;
            tab.anchorMin = tab.anchorMax = new Vector2(0.5f, 1f);
            tab.pivot = new Vector2(0.5f, 1f);
            const float h = 65f; // 预制体 Tab 统一 162×65
            tab.sizeDelta = new Vector2(162f, h);
            // 每个标签在自己的 step 槽位里垂直居中
            tab.anchoredPosition = new Vector2(150f, -(top + step * i + (step - h) * 0.5f));
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
            RedDot.Bind(_claim.transform, RedDot.Achievement, new Vector2(-8f, -8f));
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
