using UnityEngine;

/// <summary>
/// Town 内页签切换：所有功能页进 Town 时预加载，点击只做 Show/Hide。
/// 挂在 GuildHallUI 根上，共用 MainBottomNav + TopBar。
/// </summary>
public class TownHubController : MonoBehaviour
{
    public static TownHubController Instance { get; private set; }

    static GameObject _tavernPrefabCache;
    static GameObject _adventurePrefabCache;

    TavernUI _tavern;
    AdventureUI _adventure;
    CharacterUI _character;
    MainBottomNav _nav;
    bool _wired;
    bool _pagesPreloaded;
    MainNavTab _current = MainNavTab.Guild;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        EnsureWired();
        if (!_pagesPreloaded)
            PreloadAllPages();
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
        if (tab == MainNavTab.Guild || tab == MainNavTab.Tavern || tab == MainNavTab.Adventure || tab == MainNavTab.Character)
        {
            SwitchTab(tab);
            return true;
        }
        if (tab == MainNavTab.Log)
        {
            UIManager.Instance?.ShowToast("冒险日志（待实现）");
            if (_current == MainNavTab.Tavern || _current == MainNavTab.Adventure || _current == MainNavTab.Character)
                SwitchTab(MainNavTab.Guild);
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

        _current = tab;
        if (tab == MainNavTab.Tavern)
        {
            _adventure?.HidePage();
            _character?.HidePage();
            _tavern?.ShowPage();
        }
        else if (tab == MainNavTab.Adventure)
        {
            _tavern?.HidePage();
            _character?.HidePage();
            _adventure?.ShowPage();
        }
        else if (tab == MainNavTab.Character)
        {
            _tavern?.HidePage();
            _adventure?.HidePage();
            _character?.ShowPage();
        }
        else
        {
            _tavern?.HidePage();
            _adventure?.HidePage();
            _character?.HidePage();
            ShowGuildOnly();
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

    public void OpenCharacter()
    {
        EnsureWired();
        if (!_pagesPreloaded) PreloadAllPages();
        _nav?.SetSelected(MainNavTab.Character, notify: true);
        SwitchTab(MainNavTab.Character);
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
