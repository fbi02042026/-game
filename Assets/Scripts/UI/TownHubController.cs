using UnityEngine;

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
        if (tab == MainNavTab.Tavern)
        {
            TutorialDirector.Instance?.NotifyTownTab(tab);
            _adventure?.HidePage();
            _character?.HidePage();
            _log?.HidePage();
            _tavern?.ShowPage();
            GameBgm.Play(GameBgm.Track.Tavern);
        }
        else if (tab == MainNavTab.Adventure)
        {
            if (TutorialDirector.Instance != null && TutorialDirector.Instance.TryEnterTutorialBattleFromNav())
                return;

            _tavern?.HidePage();
            _character?.HidePage();
            _log?.HidePage();
            _adventure?.ShowPage();
            GameBgm.Play(GameBgm.Track.Town);
        }
        else if (tab == MainNavTab.Character)
        {
            TutorialDirector.Instance?.NotifyTownTab(tab);
            _tavern?.HidePage();
            _adventure?.HidePage();
            _log?.HidePage();
            _character?.ShowPage();
            GameBgm.Play(GameBgm.Track.Town);
        }
        else if (tab == MainNavTab.Log)
        {
            _tavern?.HidePage();
            _adventure?.HidePage();
            _character?.HidePage();
            EnsureLogPreloaded();
            _log?.ShowPage();
            GameBgm.Play(GameBgm.Track.Town);
        }
        else
        {
            _tavern?.HidePage();
            _adventure?.HidePage();
            _character?.HidePage();
            _log?.HidePage();
            ShowGuildOnly();
            GameBgm.Play(GameBgm.Track.Town);
        }
    }

    void ShowGuildOnly()
    {
        TavernUI.SetGuildHallOverlayMode(false);
        TownSharedChrome.RaiseSharedChrome(transform);
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
