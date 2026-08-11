using UnityEngine;

/// <summary>
/// Town 内页签切换：所有功能页进 Town 时预加载，点击只做 Show/Hide。
/// 挂在 GuildHallUI 根上，共用 MainBottomNav + TopBar。
/// </summary>
public class TownHubController : MonoBehaviour
{
    public static TownHubController Instance { get; private set; }

    static GameObject _tavernPrefabCache;

    TavernUI _tavern;
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
        if (tab == MainNavTab.Guild || tab == MainNavTab.Tavern)
        {
            SwitchTab(tab);
            return true;
        }
        if (tab == MainNavTab.Character || tab == MainNavTab.Log)
        {
            UIManager.Instance?.ShowToast(tab == MainNavTab.Character ? "角色（待实现）" : "冒险日志（待实现）");
            if (_current == MainNavTab.Tavern)
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
            _tavern?.ShowPage();
        }
        else
        {
            _tavern?.HidePage();
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
