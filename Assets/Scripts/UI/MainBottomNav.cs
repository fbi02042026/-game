using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主界面底部导航（公会/角色/冒险/酒馆/日志）。
/// 选中态切换只改 Image.enabled，避免 SetActive 触发大 Canvas 重建卡顿。
/// </summary>
public class MainBottomNav : MonoBehaviour
{
    public static MainBottomNav Instance { get; private set; }

    [Header("按钮（可空，Awake 按节点名自动找）")]
    public Button guildButton;
    public Button characterButton;
    public Button adventureButton;
    public Button tavernButton;
    public Button logButton;

    [Header("选中高亮（可空，自动找各按钮下「选中」）")]
    public GameObject guildSelected;
    public GameObject characterSelected;
    public GameObject adventureSelected;
    public GameObject tavernSelected;
    public GameObject logSelected;

    [Header("默认路由")]
    public bool adventureLoadsBattle = true;
    public bool guildReturnsToTown = true;

    public event Action<MainNavTab> OnTabSelected;
    public event Func<MainNavTab, bool> OnTabClickOverride;

    MainNavTab _current = MainNavTab.Guild;
    GameObject[] _selected;
    Image[] _selectedImages;
    bool _wired;
    bool _loadingBattle;

    public MainNavTab Current => _current;

    void Awake()
    {
        Instance = this;
        AutoBind();
        WireClicks();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        SetSelected(_current, notify: false);
    }

    public void Initialize(MainNavTab tab)
    {
        AutoBind();
        WireClicks();
        SetSelected(tab, notify: false);
    }

    public void SetSelected(MainNavTab tab, bool notify = true)
    {
        _current = tab;
        if (_selected == null) AutoBind();
        if (_selected == null) return;

        for (int i = 0; i < _selected.Length; i++)
        {
            bool on = i == (int)tab;
            ApplySelectedVisual(i, on);
        }

        if (notify)
            OnTabSelected?.Invoke(tab);
    }

    void ApplySelectedVisual(int index, bool on)
    {
        if (_selected == null || index < 0 || index >= _selected.Length) return;
        var go = _selected[index];
        if (go == null) return;

        // 优先只开关 Image，避免 SetActive 造成整页卡顿
        Image img = null;
        if (_selectedImages != null && index < _selectedImages.Length)
            img = _selectedImages[index];
        if (img == null)
            img = go.GetComponent<Image>();

        if (img != null)
        {
            if (!go.activeSelf) go.SetActive(true);
            img.enabled = on;
            return;
        }

        if (go.activeSelf != on)
            go.SetActive(on);
    }

    void WireClicks()
    {
        if (_wired) return;
        AutoBind();
        Bind(guildButton, MainNavTab.Guild);
        Bind(characterButton, MainNavTab.Character);
        Bind(adventureButton, MainNavTab.Adventure);
        Bind(tavernButton, MainNavTab.Tavern);
        Bind(logButton, MainNavTab.Log);
        _wired = true;
    }

    void Bind(Button btn, MainNavTab tab)
    {
        if (btn == null) return;
        // 清掉旧监听，防止重复绑定导致越点越卡
        btn.onClick = new Button.ButtonClickedEvent();
        MainNavTab captured = tab;
        btn.onClick.AddListener(() => HandleClick(captured));
    }

    void HandleClick(MainNavTab tab)
    {
        if (_loadingBattle) return;

        SetSelected(tab, notify: true);

        if (OnTabClickOverride != null)
        {
            bool handled = false;
            foreach (Delegate d in OnTabClickOverride.GetInvocationList())
            {
                if (d is Func<MainNavTab, bool> fn && fn(tab))
                    handled = true;
            }
            if (handled) return;
        }

        RouteDefault(tab);
    }

    void RouteDefault(MainNavTab tab)
    {
        switch (tab)
        {
            case MainNavTab.Guild:
                if (guildReturnsToTown)
                {
                    string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                    if (scene != GameSceneManager.TOWN_SCENE && scene != GameSceneManager.BOOT_SCENE)
                        GameSceneManager.Instance?.GoMainHub();
                }
                break;
            case MainNavTab.Character:
            case MainNavTab.Log:
                Debug.Log($"[MainBottomNav] {tab}（待实现）");
                break;
            case MainNavTab.Tavern:
                // 由 TownHubController.OnTabClickOverride 处理；无 Hub 时仅日志
                if (TownHubController.Instance == null)
                    Debug.Log("[MainBottomNav] 酒馆（TownHub 未就绪）");
                break;
            case MainNavTab.Adventure:
                TryEnterAdventure();
                break;
        }
    }

    void TryEnterAdventure()
    {
        if (!adventureLoadsBattle) return;
        if (_loadingBattle) return;

        if (!StaminaSystem.TrySpendForAdventure())
        {
            SetSelected(MainNavTab.Guild, notify: false);
            return;
        }

        _loadingBattle = true;
        GameSceneManager.Instance?.EnterAdventure();
    }

    void AutoBind()
    {
        Transform searchRoot = transform;
        Transform inner = FindDirectChild(transform, "BottomNav");
        if (inner != null) searchRoot = inner;

        if (guildButton == null) guildButton = FindButton(searchRoot, "NavGuild");
        if (characterButton == null) characterButton = FindButton(searchRoot, "NavCharacter");
        if (adventureButton == null) adventureButton = FindButton(searchRoot, "NavAdventure");
        if (tavernButton == null) tavernButton = FindButton(searchRoot, "NavTavern");
        if (logButton == null) logButton = FindButton(searchRoot, "NavLog");

        if (guildSelected == null) guildSelected = FindSelected(guildButton);
        if (characterSelected == null) characterSelected = FindSelected(characterButton);
        if (adventureSelected == null) adventureSelected = FindSelected(adventureButton);
        if (tavernSelected == null) tavernSelected = FindSelected(tavernButton);
        if (logSelected == null) logSelected = FindSelected(logButton);

        _selected = new[] { guildSelected, characterSelected, adventureSelected, tavernSelected, logSelected };
        _selectedImages = new Image[_selected.Length];
        for (int i = 0; i < _selected.Length; i++)
            _selectedImages[i] = _selected[i] != null ? _selected[i].GetComponent<Image>() : null;
    }

    static Button FindButton(Transform root, string name)
    {
        Transform t = FindDeepChild(root, name);
        return t != null ? t.GetComponent<Button>() : null;
    }

    static GameObject FindSelected(Button btn)
    {
        if (btn == null) return null;
        Transform sel = FindDeepChild(btn.transform, "选中");
        return sel != null ? sel.gameObject : null;
    }

    static Transform FindDirectChild(Transform parent, string name)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform c = parent.GetChild(i);
            if (c.name == name) return c;
        }
        return null;
    }

    static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var r = FindDeepChild(parent.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }
}
