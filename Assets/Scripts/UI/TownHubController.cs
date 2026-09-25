using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Town 内页签切换：所有功能页进 Town 时预加载，点击只做 Show/Hide。
/// 挂在 GuildHallUI 根上，共用 MainBottomNav + TopBar。
/// </summary>
public class TownHubController : MonoBehaviour
{
    public static TownHubController Instance { get; private set; }

    /// <summary>从战斗撤离等回城后，进 Town 自动打开冒险页。</summary>
    public static bool PendingOpenAdventure;

    static GameObject _tavernPrefabCache;
    static GameObject _adventurePrefabCache;

    AdventureLogUI _log;
    TavernUI _tavern;
    AdventureUI _adventure;
    CharacterUI _character;
    MainBottomNav _nav;
    bool _wired;
    bool _pagesPreloaded;
    /// <summary>已经显示过一次的页：这些页再切只是 Show/Hide，不会卡，不用遮挡。</summary>
    readonly System.Collections.Generic.HashSet<MainNavTab> _shownOnce =
        new System.Collections.Generic.HashSet<MainNavTab>();
    /// <summary>首次切页正在协程里等下一帧，期间再点直接忽略（避免叠加两次构建）。</summary>
    bool _firstSwitchBusy;
    /// <summary>等待下一帧执行的切页（连点时取消上一个，只保留最后一次）。</summary>
    Coroutine _pendingSwitch;
    Image _switchVeil;
    MainNavTab _current = MainNavTab.Guild;
    bool _wasLandladyBanned;
    TavernNavBanHud _tavernBanHud;

    void Awake()
    {
        Instance = this;
    }

    // 禁入状态存在 PlayerPrefs 里，倒计时只精确到秒，不必每帧去读
    const float BanPollInterval = 0.25f;
    float _banPollTimer;

    void Update()
    {
        _banPollTimer -= Time.unscaledDeltaTime;
        if (_banPollTimer > 0f) return;
        _banPollTimer = BanPollInterval;

        bool banned = TavernLandladyTease.IsBanned;
        if (_wasLandladyBanned && !banned && _tavern != null)
            _tavern.MarkWelcomeBack();
        _wasLandladyBanned = banned;
        RefreshTavernNavBannedVisual(banned);
    }

    void RefreshTavernNavBannedVisual(bool banned)
    {
        EnsureNavBound();
        if (_nav == null || _nav.tavernButton == null) return;
        if (_tavernBanHud == null)
            _tavernBanHud = TavernNavBanHud.EnsureOn(_nav.tavernButton);
        _tavernBanHud?.Refresh(banned, TavernLandladyTease.BanRemainingSeconds);
    }

    void Start()
    {
        TownSaveAlign.AlignAll();
        EnsureWired();
        if (!_pagesPreloaded)
            PreloadAllPages();
        ConsumePendingAdventure();
        StartCoroutine(CoWarmupPages());
    }

    /// <summary>
    /// 进镇预热（2026-09-18 用户要求：加载过程中把要提前加载的都加载掉）：
    /// 亮 Loading 界面 → ① StartupPreloader 预加载常用资源 → ② 把常点页面逐帧
    /// 「点亮一遍再藏回去」完成首次构建 → 收起 Loading。
    /// 之后点任何入口都是纯 Show/Hide。若玩家已经在用某页（回城自动开冒险）则放弃预热。
    /// </summary>
    System.Collections.IEnumerator CoWarmupPages()
    {
        var order = new[] { MainNavTab.Character, MainNavTab.Adventure, MainNavTab.Tavern, MainNavTab.Log };

        // 玩家已在用某页（回城自动开冒险）→ 不遮、不预热
        foreach (var tab in order)
        {
            var mb0 = PageOf(tab) as MonoBehaviour;
            if (mb0 != null && mb0.gameObject.activeSelf) yield break;
        }

        var loading = EnsureWarmupLoading();

        // ① 常用资源预加载（字体 / 底图 / 插图 / 图标 / 城镇页 prefab）
        if (loading != null)
        {
            var lv = loading;
            yield return StartCoroutine(StartupPreloader.Run((v, tip) =>
            {
                if (lv != null)
                {
                    lv.SetProgress(0.05f + v * 0.35f);
                    if (!string.IsNullOrEmpty(tip)) lv.SetTip(tip);
                }
            }));
        }
        else
        {
            yield return StartCoroutine(StartupPreloader.Run(null));
        }

        // ② 常点页面逐帧构建一次（首建成本提前摊掉）
        for (int i = 0; i < order.Length; i++)
        {
            var tab = order[i];
            var page = PageOf(tab);
            var mb = page as MonoBehaviour;
            if (page == null || mb == null) continue;
            if (mb.gameObject.activeSelf) continue;
            if (loading != null)
            {
                loading.SetTip(LoadingFlavorText.Next());
                loading.SetProgress(0.4f + 0.55f * ((i + 1) / (float)order.Length));
            }
            page.ShowPage();
            _shownOnce.Add(tab);
            yield return null;
            page.HidePage();
        }

        if (loading != null)
        {
            loading.SetTip(LoadingFlavorText.Next());
            loading.SetProgress(1f);
            yield return null;
            Destroy(loading.gameObject);
        }
        ShowGuildOnly(); // 恢复大厅底图状态（清掉预热期间页面设置的 overlay 模式）
    }

    /// <summary>预热用的 Loading 实例（自己持有，不抢 BattleLoadingOverlay 的单例，避免被场景流程提前 Hide）。</summary>
    LoadingUI EnsureWarmupLoading()
    {
        var prefab = Resources.Load<GameObject>(LoadingUI.ResourcePath);
        if (prefab == null) return null;
        var go = Instantiate(prefab, transform, false);
        go.name = "WarmupLoading";
        var ui = go.GetComponent<LoadingUI>() ?? go.AddComponent<LoadingUI>();
        ui.PrepareCanvas();
        ui.SetTip(LoadingFlavorText.Next());
        ui.SetProgress(0.05f);
        go.transform.SetAsLastSibling();
        return ui;
    }

    /// <summary>回城后打开冒险页（撤离等场景用）。可在 Bootstrap 完成后再调一次。</summary>
    public static void ConsumePendingAdventure()
    {
        if (!PendingOpenAdventure) return;
        PendingOpenAdventure = false;
        if (Instance == null) return;
        Instance.OpenAdventure();
    }

    public void OpenAdventure()
    {
        TownSaveAlign.AlignAll();
        EnsureWired();
        if (!_pagesPreloaded) PreloadAllPages();
        // 撤离回城时不要再被教程拦截进战斗
        _nav?.SetSelected(MainNavTab.Adventure, notify: false);
        _tavern?.HidePage();
        _character?.HidePage();
        _log?.HidePage();
        _adventure?.ShowPage();
        _current = MainNavTab.Adventure;
        GameBgm.Play(GameBgm.Track.Town);
    }

    void OnDestroy()
    {
        if (_nav != null)
            _nav.OnTabSelected -= OnTabSelected;
        if (Instance == this) Instance = null;
    }

    public static TownHubController EnsureOn(GameObject hallRoot)
    {
        if (hallRoot == null) return null;
        var hub = hallRoot.GetComponent<TownHubController>();
        if (hub == null) hub = hallRoot.AddComponent<TownHubController>();
        hub.EnsureWired();
        return hub;
    }

    /// <summary>Town 启动时调用：实例化并 Preload 全部功能页（隐藏）</summary>
    public void PreloadAllPages()
    {
        if (_pagesPreloaded) return;
        EnsureNavBound();
        EnsureTavernPreloaded();
        EnsureAdventurePreloaded();
        EnsureCharacterPreloaded();
        EnsureLogPreloaded();
        _pagesPreloaded = true;
        ShowGuildOnly();
        if (_nav != null)
            _nav.SetSelected(MainNavTab.Guild, notify: false);
        _current = MainNavTab.Guild;
        _shownOnce.Add(MainNavTab.Guild);   // 公会页是进 Town 的默认页，不算「首次点击」
        _wired = true;
    }

    public void EnsureWired()
    {
        EnsureNavBound();
        if (!_pagesPreloaded)
            return;
        if (!_wired)
        {
            ShowGuildOnly();
            if (_nav != null)
                _nav.SetSelected(MainNavTab.Guild, notify: false);
            _wired = true;
        }
    }

    bool OnTabOverride(MainNavTab tab)
    {
        if (tab == MainNavTab.Guild || tab == MainNavTab.Tavern || tab == MainNavTab.Adventure
            || tab == MainNavTab.Character || tab == MainNavTab.Log)
        {
            SwitchTab(tab);
            return true;
        }
        return false;
    }

    void OnTabSelected(MainNavTab tab) { }

    /// <summary>轻量切页：无 Instantiate / Resources.Load</summary>
    public void SwitchTab(MainNavTab tab)
    {
        if (!_pagesPreloaded)
            PreloadAllPages();

        // 每一页第一次显示时要同步构建整页内容（冒险日志最重：要对齐存档 + 建整个列表），
        // 同步做会卡一帧、点击像没反应。这里先亮一层全屏罩、拖到下一帧才真切，
        // 让点击立刻有反馈，构建开销藏在罩后面。第二次以后是纯 Show/Hide，直接走。
        if (!_firstSwitchBusy && !_shownOnce.Contains(tab))
        {
            _firstSwitchBusy = true;
            StartCoroutine(CoFirstSwitch(tab));
            return;
        }

        // 第二次以后：底栏高亮已在 MainBottomNav.HandleClick 里当帧生效，
        // 这里把整段切页推到下一帧，避免点击这一帧就承担整页重建（点了像卡住）。
        if (_pendingSwitch != null) StopCoroutine(_pendingSwitch);
        _pendingSwitch = StartCoroutine(CoSwitchNextFrame(tab));
    }

    System.Collections.IEnumerator CoSwitchNextFrame(MainNavTab tab)
    {
        yield return null;
        _pendingSwitch = null;
        SwitchTabNow(tab);
        _shownOnce.Add(tab);
    }

    System.Collections.IEnumerator CoFirstSwitch(MainNavTab tab)
    {
        var veil = EnsureSwitchVeil();
        if (veil != null)
        {
            veil.transform.SetAsLastSibling();
            veil.gameObject.SetActive(true);
        }
        yield return null;          // 先让罩渲染出来一帧，再承担构建开销
        SwitchTabNow(tab);
        _shownOnce.Add(tab);
        _firstSwitchBusy = false;
        if (veil != null)
            veil.gameObject.SetActive(false);
    }

    Image EnsureSwitchVeil()
    {
        if (_switchVeil != null) return _switchVeil;
        var go = new GameObject("SwitchVeil", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        _switchVeil = go.GetComponent<Image>();
        _switchVeil.color = new Color(0f, 0f, 0f, 0.35f);
        _switchVeil.raycastTarget = true;   // 顺手吃掉这期间的多余点击
        go.SetActive(false);
        return _switchVeil;
    }

    void SwitchTabNow(MainNavTab tab)
    {
        if (tab == MainNavTab.Tavern && TavernLandladyTease.IsBanned)
        {
            if (_tavernBanHud == null && _nav != null)
                _tavernBanHud = TavernNavBanHud.EnsureOn(_nav.tavernButton);
            _tavernBanHud?.ShowBannedBubble();
            // 停在当前页，底栏选中也还原
            if (_nav != null)
                _nav.SetSelected(_current, notify: false);
            return;
        }

        _current = tab;
        _nav?.SetSelected(tab, notify: false);

        // 目标页已在前台：只保持底栏高亮，不再重跑 ShowPage。
        // 之前每次点同一入口都会整页重建（背包/地图/日志列表全量重来），点一下卡一帧。
        var target = PageOf(tab);
        if (target != null && IsPageActive(target))
            return;

        // 已经隐藏的页不再跑 HidePage：Hide 里若销毁整页内容，重复调用纯属浪费。
        HideIfVisible(_tavern);
        HideIfVisible(_adventure);
        HideIfVisible(_character);
        HideIfVisible(_log);

        if (tab == MainNavTab.Tavern)
        {
            TutorialDirector.Instance?.NotifyTownTab(tab);
            _tavern?.ShowPage();
            GameBgm.Play(GameBgm.Track.Tavern);
        }
        else if (tab == MainNavTab.Adventure)
        {
            _adventure?.ShowPage();
            TutorialDirector.Instance?.NotifyAdventureOpened();
            GameBgm.Play(GameBgm.Track.Town);
        }
        else if (tab == MainNavTab.Character)
        {
            TutorialDirector.Instance?.NotifyTownTab(tab);
            _character?.ShowPage();
            GameBgm.Play(GameBgm.Track.Town);
        }
        else if (tab == MainNavTab.Log)
        {
            EnsureLogPreloaded();
            _log?.ShowPage();
            GameBgm.Play(GameBgm.Track.Town);
        }
        else
        {
            ShowGuildOnly();
            GameBgm.Play(GameBgm.Track.Town);
        }
    }

    ITownPage PageOf(MainNavTab tab)
    {
        if (tab == MainNavTab.Tavern) return _tavern;
        if (tab == MainNavTab.Adventure) return _adventure;
        if (tab == MainNavTab.Character) return _character;
        if (tab == MainNavTab.Log) return _log;
        return null;
    }

    static bool IsPageActive(ITownPage page)
    {
        var mb = page as MonoBehaviour;
        return mb != null && mb.gameObject.activeSelf;
    }

    static void HideIfVisible(ITownPage page)
    {
        if (page == null) return;
        if (!IsPageActive(page)) return;
        page.HidePage();
    }

    void ShowGuildOnly()
    {
        TavernUI.SetGuildHallOverlayMode(false);
        TownSharedChrome.RaiseSharedChrome(transform);

        // 显示公会页 = 底栏默认入口，必须把选中态拉回公会。
        // 预热协程会逐页 ShowPage，而 AdventureLogUI.ShowPage() 里会把底栏钉成 Log，
        // 预热结束回到公会页时若不还原，玩家一进主界面就看到「冒险日志」亮着。
        // notify:false：只刷视觉，不走 OnTabSelected / OnTabClickOverride，避免切页回环。
        EnsureNavBound();
        _nav?.SetSelected(MainNavTab.Guild, notify: false);
        _current = MainNavTab.Guild;
    }

    void EnsureNavBound()
    {
        if (_nav != null) return;
        _nav = GetComponentInChildren<MainBottomNav>(true);
        if (_nav == null) _nav = MainBottomNav.Instance;
        if (_nav == null) return;
        _nav.OnTabSelected -= OnTabSelected;
        _nav.OnTabSelected += OnTabSelected;
        _nav.OnTabClickOverride -= OnTabOverride;
        _nav.OnTabClickOverride += OnTabOverride;
    }

    void EnsureCharacterPreloaded()
    {
        if (_character != null) return;

        var prefab = Resources.Load<GameObject>("Prefabs/Town/CharacterUI");
        if (prefab != null)
        {
            var go = Instantiate(prefab, transform, false);
            go.name = "CharacterUI";
            _character = go.GetComponent<CharacterUI>();
            if (_character == null) _character = go.AddComponent<CharacterUI>();
        }
        else
        {
            var go = new GameObject("CharacterUI", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            Stretch(go);
            _character = go.AddComponent<CharacterUI>();
        }

        _character.PreloadOnce();
        _character.HidePage();
    }

    void EnsureLogPreloaded()
    {
        if (_log != null) return;

        var prefab = Resources.Load<GameObject>("Prefabs/Town/AdventureLogUI");
        if (prefab != null)
        {
            var go = Instantiate(prefab, transform, false);
            go.name = "AdventureLogUI";
            Stretch(go);
            _log = go.GetComponent<AdventureLogUI>();
            if (_log == null) _log = go.AddComponent<AdventureLogUI>();
        }
        else
        {
            var go = new GameObject("AdventureLogUI", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            Stretch(go);
            _log = go.AddComponent<AdventureLogUI>();
        }

        _log.PreloadOnce();
        _log.HidePage();
    }

    public void OpenCharacter()
    {
        EnsureWired();
        if (!_pagesPreloaded) PreloadAllPages();
        _nav?.SetSelected(MainNavTab.Character, notify: true);
        SwitchTab(MainNavTab.Character);
    }

    public void OpenAdventureLog()
    {
        EnsureWired();
        if (!_pagesPreloaded) PreloadAllPages();
        _nav?.SetSelected(MainNavTab.Log, notify: false);
        SwitchTab(MainNavTab.Log);
    }

    void EnsureAdventurePreloaded()
    {
        if (_adventure != null) return;

        var prefab = Resources.Load<GameObject>("Prefabs/Town/AdventureUI");
        if (prefab != null)
        {
            var go = Instantiate(prefab, transform, false);
            go.name = "AdventureUI";
            _adventure = go.GetComponent<AdventureUI>();
            if (_adventure == null) _adventure = go.AddComponent<AdventureUI>();
        }
        else
        {
            // 兜底：资源路径断链时仍可用运行时建树
            var go = new GameObject("AdventureUI", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            Stretch(go);
            _adventure = go.AddComponent<AdventureUI>();
        }

        _adventure.PreloadOnce();
        _adventure.HidePage();
    }

    void EnsureTavernPreloaded()
    {
        if (_tavern != null) return;

        if (_tavernPrefabCache == null)
            _tavernPrefabCache = Resources.Load<GameObject>("Prefabs/Town/TavernUI");

        if (_tavernPrefabCache != null)
        {
            var go = Instantiate(_tavernPrefabCache, transform, false);
            go.name = "TavernUI";
            _tavern = go.GetComponent<TavernUI>();
            if (_tavern == null) _tavern = go.AddComponent<TavernUI>();
        }
        else
        {
            var runtime = new GameObject("TavernUI", typeof(RectTransform));
            runtime.transform.SetParent(transform, false);
            Stretch(runtime);
            _tavern = runtime.AddComponent<TavernUI>();
        }

        _tavern.PreloadOnce();
        _tavern.HidePage();
    }

    static void Stretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    public void OpenTavern()
    {
        EnsureWired();
        if (!_pagesPreloaded) PreloadAllPages();
        _nav?.SetSelected(MainNavTab.Tavern, notify: true);
        SwitchTab(MainNavTab.Tavern);
    }

    public void OpenGuild()
    {
        EnsureWired();
        if (!_pagesPreloaded) PreloadAllPages();
        _nav?.SetSelected(MainNavTab.Guild, notify: true);
        SwitchTab(MainNavTab.Guild);
    }

    // 兼容旧调用
    void ApplyTab(MainNavTab tab) => SwitchTab(tab);
}
