using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主界面底部导航（公会/角色/冒险/酒馆/日志）。
/// 选中态：NavBg 换紫色底图，并以底部为原点放大 1.2 倍；默认态：NavBg 深色底图、scale=1。
/// 「选中」子节点保持隐藏，避免叠两层同色框。
/// </summary>
public class MainBottomNav : MonoBehaviour
{
    const string NavBgDefaultPath = "UI/Town/Nav/nav_bg_default";
    const string NavBgSelectedPath = "UI/Town/Nav/nav_bg_selected";
    const float SelectedScale = 1.2f;

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
    Image[] _navBgImages;
    Button[] _buttons;
    RectTransform[] _buttonRts;
    bool[] _pivotReady;
    static Sprite _navBgDefault;
    static Sprite _navBgSelected;
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

    void OnEnable()
    {
        SetSelected(_current, notify: false);
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
        EnsureNavBgSprites();

        if (_navBgImages != null && index >= 0 && index < _navBgImages.Length)
        {
            Image bg = _navBgImages[index];
            if (bg != null)
            {
                Sprite sp = on ? _navBgSelected : _navBgDefault;
                if (sp == null && !on)
                {
                    EnsureNavBgSprites();
                    sp = _navBgDefault;
                }
                if (sp != null)
                {
                    bg.sprite = sp;
                    bg.color = Color.white;
                    bg.enabled = true;
                }
            }
        }

        ApplySelectedScale(index, on);

        if (_selected == null || index < 0 || index >= _selected.Length) return;
        var go = _selected[index];
        if (go == null) return;

        // 预制体里的「选中」层与 NavBg 选中图重复，统一隐藏，只靠 NavBg 换图区分状态
        Image overlay = null;
        if (_selectedImages != null && index < _selectedImages.Length)
            overlay = _selectedImages[index];
        if (overlay == null)
            overlay = go.GetComponent<Image>();

        if (overlay != null)
        {
            overlay.enabled = false;
            return;
        }

        if (go.activeSelf)
            go.SetActive(false);
    }

    /// <summary>选中：底部中心为原点放大；未选中恢复 1。</summary>
    void ApplySelectedScale(int index, bool on)
    {
        if (_buttonRts == null || index < 0 || index >= _buttonRts.Length) return;
        RectTransform rt = _buttonRts[index];
        if (rt == null) return;

        EnsureBottomPivot(index, rt);
        float s = on ? SelectedScale : 1f;
        rt.localScale = new Vector3(s, s, 1f);

        // 同步按压缩放基准，避免松手后回到 1 冲掉选中放大
        var press = rt.GetComponent<UiButtonPressFeedback>();
        if (press != null)
            press.SyncBaseScale(rt.localScale);
    }

    /// <summary>把 pivot 改到底边中心，并补偿位置，避免视觉跳动。</summary>
    void EnsureBottomPivot(int index, RectTransform rt)
    {
        if (_pivotReady != null && index < _pivotReady.Length && _pivotReady[index])
            return;

        Vector2 want = new Vector2(0.5f, 0f);
        if ((rt.pivot - want).sqrMagnitude > 0.0001f)
        {
            Vector2 size = rt.rect.size;
            Vector2 deltaPivot = want - rt.pivot;
            Vector2 delta = new Vector2(deltaPivot.x * size.x, deltaPivot.y * size.y);
            rt.pivot = want;
            rt.anchoredPosition += delta;
        }

        if (_pivotReady != null && index < _pivotReady.Length)
            _pivotReady[index] = true;
    }

    static void EnsureNavBgSprites()
    {
        if (_navBgDefault == null)
            _navBgDefault = LoadNavSprite(NavBgDefaultPath);
        if (_navBgSelected == null)
            _navBgSelected = LoadNavSprite(NavBgSelectedPath);
    }

    static Sprite LoadNavSprite(string path)
    {
        var sp = Resources.Load<Sprite>(path);
        if (sp != null) return sp;
        var tex = Resources.Load<Texture2D>(path);
        if (tex == null) return null;
        return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>资源导入变更后强制重载 Nav 底图。</summary>
    public static void InvalidateNavBgCache()
    {
        _navBgDefault = null;
        _navBgSelected = null;
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
        _navBgImages = new Image[_selected.Length];
        _buttons = new[] { guildButton, characterButton, adventureButton, tavernButton, logButton };
        _buttonRts = new RectTransform[_buttons.Length];
        _pivotReady = new bool[_buttons.Length];
        for (int i = 0; i < _selected.Length; i++)
        {
            _selectedImages[i] = _selected[i] != null ? _selected[i].GetComponent<Image>() : null;
            _navBgImages[i] = FindNavBg(_buttons[i]);
            _buttonRts[i] = _buttons[i] != null ? _buttons[i].transform as RectTransform : null;
        }

        EnsureNavBgSprites();
        if (_navBgSelected == null && _navBgImages.Length > 0 && _navBgImages[0] != null)
            _navBgSelected = _navBgImages[0].sprite;
        if (_navBgDefault == null)
        {
            for (int i = 0; i < _navBgImages.Length; i++)
            {
                if (_navBgImages[i] == null || _navBgImages[i].sprite == null) continue;
                if (_navBgSelected != null && _navBgImages[i].sprite == _navBgSelected) continue;
                _navBgDefault = _navBgImages[i].sprite;
                break;
            }
        }
    }

    static Image FindNavBg(Button btn)
    {
        if (btn == null) return null;
        Transform bg = FindDeepChild(btn.transform, "NavBg");
        return bg != null ? bg.GetComponent<Image>() : null;
    }

    static Button FindButton(Transform root, string name)
    {
        Transform t = FindDeepChild(root, name);
        return t != null ? t.GetComponent<Button>() : null;
    }

    static GameObject FindSelected(Button btn)
    {
        if (btn == null) return null;
        Transform sel = FindDeepChild(btn.transform, "选中")
                        ?? FindDeepChild(btn.transform, "Selected")
                        ?? FindDeepChild(btn.transform, "Select");
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
