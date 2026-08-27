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
        _phase3 = AdventureLogPhase3Panel.Ensure(this);
        RedDot.RefreshCommon();
        SelectTab(_tab, force: true);
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
        if (_root != null) _root.SetActive(false);
        gameObject.SetActive(false);
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

        _claim = FindTransform("ClaimAch")?.gameObject;
        _paper = transform.Find("Root/Frame/Paper")?.gameObject;
        _activeCard = FindTransform("ActiveCard")?.gameObject;
        var frame = transform.Find("Root/Frame");
        if (frame != null)
            _codex = new AdventureLogCodexPanel(frame);
        PrepareDoneTemplate();
        WireTabs();
        WireButtons();
        BindRewardRedDots();
        BindTabIllustration();
        RedDot.RefreshCommon();
        _bound = true;
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
        if (_tabIllustration == null) return;
        string key = CurrentTabName;
        if (string.IsNullOrEmpty(key)) return;
        if (!UiKeyedBackgrounds.ApplyLogTabIllust(_tabIllustration, key))
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
            if (_paper != null) _paper.SetActive(true);
        }
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
        const float TopReserve = 120f;
        const float BottomReserve = 150f;
        const float SidePad = 16f;
        var frame = transform.Find("Root/Frame") as RectTransform;
        if (frame == null) return;
        frame.anchorMin = Vector2.zero;
        frame.anchorMax = Vector2.one;
        frame.pivot = new Vector2(0.5f, 0.5f);
        frame.anchoredPosition = Vector2.zero;
        frame.sizeDelta = Vector2.zero;
        frame.offsetMin = new Vector2(SidePad, BottomReserve);
        frame.offsetMax = new Vector2(-SidePad, -TopReserve);
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
                    AchievementMilestoneUI.Show();
                else
                    UIManager.Instance?.ShowToast("暂无可领成就奖励");
                RefreshBody();
                RedDot.RefreshCommon();
            });
            var claimLabel = _claim.GetComponentInChildren<Text>(true);
            if (claimLabel != null)
                claimLabel.text = "领取成就奖励";
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
                on ? e.Desc + "\n" + e.Lore : (e.LaterChapter ? "在后续裂缝层中可解锁。" : "解锁条件：" + e.Unlock),
                !on);
        }
        title = hasCurrent ? current.Name : "怪物图鉴";
        desc = hasCurrent ? current.Desc + "\n" + current.Lore : "本版描述偏趣闻与冒险者口耳相传，不代表公会官方立场。";
        objective = hasCurrent ? current.Place : "在裂缝中遭遇并记录";
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
        for (int i = 0; i < _spawnedRows.Count; i++)
        {
            if (_spawnedRows[i] != null)
                Destroy(_spawnedRows[i]);
        }
        _spawnedRows.Clear();

        if (_rowTemplate == null || _listContent == null) return;

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
