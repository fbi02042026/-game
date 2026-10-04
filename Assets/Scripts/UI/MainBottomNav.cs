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

    /// <summary>运行时补出来的「安全区背景延伸」节点名（不进预制体）。</summary>
    const string SafeAreaBackdropName = "SafeAreaBackdrop";

    RectTransform _safeBackdrop;
    int _backdropW = -1;
    int _backdropH = -1;
    Rect _backdropSafe = new Rect(0f, 0f, -1f, -1f);
    float _backdropScale = -1f;
    Vector2 _backdropRootOffsetMin = new Vector2(float.NaN, float.NaN);

    /// <summary>
    /// 选中态兜底总开关（主人反馈「默认选中跑到了冒险日志」）。
    /// true：AutoBind / Initialize / SetSelected 三处都会先无条件清掉按钮下的「选中」层，并强制只有目标项高亮；
    /// false：完全恢复改动前的旧行为，方便一键回退对比。
    /// </summary>
    public static bool EnableNavSelectedGuard = true;

    /// <summary>缺图警告只打一次，避免每次 SetSelected 刷五条重复日志。</summary>
    static bool _navBgWarned;

    public MainNavTab Current => _current;

    void Awake()
    {
        Instance = this;
        AutoBind();
        WireClicks();

        // 2026-10-04 主人拍板：底栏根节点被 SafeAreaFitter(Shift) 整体上移后，栏底与屏幕底之间露出 Canvas 黑底
        // → 先补一条贴屏幕最底的延伸背景垫底，再挂 SafeAreaFitter（顺序不能反）。别再改回「只靠 BottomNavBG 往下溢出」。
        EnsureSafeAreaBackdrop();

        // P2-5 SafeArea：底部五入口根节点（本组件所在 GameObject）贴底，
        // 内缩底部安全区（挖孔/手势条），避免按钮被遮挡。左右不缩。
        // 运行时补组件，不碰预制体；GetComponent 守卫避免重复挂。
        if (GetComponent<SafeAreaFitter>() == null)
        {
            var navSafe = gameObject.AddComponent<SafeAreaFitter>();
            navSafe.edge = SafeAreaFitter.Edge.Bottom;
            navSafe.enabledFit = true;
        }
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

    /// <summary>
    /// 屏幕尺寸 / 安全区 / 画布缩放 / 根节点底边距四者任一变化才重算一次延伸背景。
    /// 为什么必须盯住根节点 offsetMin：背景条是根节点的子节点，根节点被 SafeAreaFitter 上移时它会跟着走，
    /// 只在屏幕变化时重算会漏掉「安全区先变、根节点后重排」的那一帧。平时每帧只做几次比较，不写 RectTransform。
    /// </summary>
    void LateUpdate()
    {
        var rootRt = transform as RectTransform;
        if (rootRt == null) return;

        int w = Screen.width;
        int h = Screen.height;
        Rect safe = Screen.safeArea;
        float sf = CanvasScaleFactor();
        Vector2 rootOffsetMin = rootRt.offsetMin;

        if (w == _backdropW && h == _backdropH && safe == _backdropSafe
            && Mathf.Approximately(sf, _backdropScale) && rootOffsetMin == _backdropRootOffsetMin)
            return;

        _backdropW = w;
        _backdropH = h;
        _backdropSafe = safe;
        _backdropScale = sf;
        _backdropRootOffsetMin = rootOffsetMin;
        EnsureSafeAreaBackdrop();
    }

    /// <summary>
    /// 在本节点下补（或复用）一条「安全区背景延伸」，盖住导航栏底与屏幕底之间的黑底。
    /// 幂等：同名子节点已存在就复用，只刷新矩形/颜色，绝不重复创建。
    /// </summary>
    void EnsureSafeAreaBackdrop()
    {
        var rootRt = transform as RectTransform;
        if (rootRt == null) return;

        RectTransform rt = _safeBackdrop;
        if (rt == null)
        {
            Transform exist = transform.Find(SafeAreaBackdropName);
            GameObject go = exist != null
                ? exist.gameObject
                : new GameObject(SafeAreaBackdropName, typeof(RectTransform), typeof(Image));
            if (exist == null) go.transform.SetParent(transform, false);

            rt = go.transform as RectTransform;
            var img0 = go.GetComponent<Image>();
            if (img0 == null) img0 = go.AddComponent<Image>();
            if (rt == null || img0 == null)
            {
                Debug.LogError("[MainBottomNav] SafeAreaBackdrop 创建失败，节点路径：" + NodePath(transform)
                               + "/" + SafeAreaBackdropName);
                return;
            }
        }

        var img = rt.GetComponent<Image>();
        if (img == null) img = rt.gameObject.AddComponent<Image>();
        _safeBackdrop = rt;

        // 垫在最底层：先画它，再画 BottomNavBG 和按钮，两层同色才接缝不露馅
        rt.SetAsFirstSibling();

        // 绝不挂 SafeAreaFitter：它必须永远贴屏幕最底，跟着根节点一起上移就白补了
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);

        // 视觉对齐 BottomNavBG：优先复制它的 sprite + color，取不到就用导航栏深色兜底（不静默放过）
        Transform bgNode = FindDeepChild(transform, "BottomNavBG");
        Image bgImg = bgNode != null ? bgNode.GetComponent<Image>() : null;
        if (bgImg != null)
        {
            img.sprite = bgImg.sprite;
            img.color = bgImg.color;
        }
        else
        {
            img.sprite = null;
            img.color = new Color(0.10f, 0.09f, 0.13f, 1f);
        }
        img.raycastTarget = false;   // 只是背景，不能吃掉按钮点击

        var parent = rootRt.parent as RectTransform;
        if (parent == null || parent.rect.width <= 0f || parent.rect.height <= 0f)
        {
            // 量不到父级矩形就没有换算基准：先收成 0 高度，绝不能凭空撑出一条色带
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return;
        }

        Vector2 pMin = parent.rect.min;
        Vector2 pMax = parent.rect.max;
        // 根节点四边在父级坐标里的实际位置（SafeAreaFitter 上移量已经算在 offsetMin.y 里，取当前值最准）
        float rootBottomY = Mathf.Lerp(pMin.y, pMax.y, rootRt.anchorMin.y) + rootRt.offsetMin.y;
        float rootLeftX = Mathf.Lerp(pMin.x, pMax.x, rootRt.anchorMin.x) + rootRt.offsetMin.x;
        float rootRightX = Mathf.Lerp(pMin.x, pMax.x, rootRt.anchorMax.x) + rootRt.offsetMax.x;

        // 2026-10-04 主人拍板：填的是「根节点底边 → 屏幕（父级）最底」之间的全部空隙，
        // 不管空的是 safeArea 还是预制体本来就有的原始底边间隙，一次性全吃掉，别再改回「只补 safeArea」。
        // 顶边贴根节点底边（节点自身坐标系里就是 0），底边贴父级最底 → 高度 = rootBottomY - pMin.y。
        // 夹到 >= 0：根节点向下溢出/数值异常时 rootBottomY <= pMin.y，不夹会算出反向矩形。
        float gap = Mathf.Max(0f, rootBottomY - pMin.y);
        rt.offsetMin = new Vector2(pMin.x - rootLeftX, -gap);
        rt.offsetMax = new Vector2(pMax.x - rootRightX, 0f);
    }

    /// <summary>屏幕像素 → 画布参考单位的换算系数（取最外层带 CanvasScaler 的 Canvas；取不到就 1，继续往下走）。</summary>
    float CanvasScaleFactor()
    {
        var canvases = GetComponentsInParent<Canvas>(true);
        for (int i = canvases.Length - 1; i >= 0; i--)
        {
            var c = canvases[i];
            if (c == null) continue;
            if (c.GetComponent<CanvasScaler>() != null && c.scaleFactor > 0f)
                return c.scaleFactor;
        }
        return 1f;
    }

    /// <summary>拼出节点全路径，给创建失败日志定位用。</summary>
    static string NodePath(Transform t)
    {
        if (t == null) return "(null)";
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    public void Initialize(MainNavTab tab)
    {
        AutoBind();
        // 双保险：绑定完先把预制体里默认亮着的「选中」层清掉，再决定谁该高亮
        if (EnableNavSelectedGuard) ForceCleanSelectedVisuals();
        WireClicks();
        SetSelected(tab, notify: false);
    }

    public void SetSelected(MainNavTab tab, bool notify = true)
    {
        _current = tab;
        // 先无条件清一遍「选中」层：即使下面 _selected 为 null 直接 return，也不会留下错误高亮
        if (EnableNavSelectedGuard) ForceCleanSelectedVisuals();
        if (_selected == null) AutoBind();
        if (_selected == null) return;

        // 只有 index == (int)tab 的按钮走选中态（NavBg 换 _navBgSelected + localScale=SelectedScale），
        // 其余一律强制普通态（NavBg 换 _navBgDefault + localScale=1），杜绝上一个高亮残留。
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
                else if (!_navBgWarned)
                {
                    // 缺图不能静默：不然界面上「看不出谁被选中」很难定位
                    _navBgWarned = true;
                    string state = on ? "选中态" : "普通态";
                    Debug.LogWarning("[MainBottomNav] NavBg 底图缺失（" + state + "，路径 "
                        + NavBgDefaultPath + " / " + NavBgSelectedPath + "），选中高亮可能看不出来。");
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

    /// <summary>
    /// 无条件清掉 5 个按钮子树里的「选中」层（幂等，可重复调用）。
    /// 为什么：预制体 MainBottomNav.prefab 里 5 个 Nav* 下的「选中」节点默认全是 activeSelf=true 且 Image 启用，
    /// 而 ApplySelectedVisual 只有被 SetSelected 遍历到才隐藏它；一旦 AutoBind 没绑上（_selected 元素为 null）
    /// 或 SetSelected 中途 return，就会留下一个亮着的「选中」框，看起来就是「默认选中项跑偏」。
    /// 这里不依赖 _selected 是否绑定成功，按按钮逐个 FindDeep 处理。
    /// </summary>
    public void ForceCleanSelectedVisuals()
    {
        Button[] btns = { guildButton, characterButton, adventureButton, tavernButton, logButton };
        int bound = 0;
        for (int i = 0; i < btns.Length; i++)
        {
            if (btns[i] == null) continue;
            bound++;
            CleanSelectedUnder(btns[i]);
        }

        // 还有按钮没绑上（AutoBind 未执行/部分失败）：退化成整棵底栏子树扫一遍同名节点。
        // 注意这里不能再调 AutoBind，否则与 AutoBind 末尾的清理互相调用会死递归。
        if (bound < btns.Length)
        {
            Transform searchRoot = FindDirectChild(transform, "BottomNav");
            CleanSelectedInSubtree(searchRoot != null ? searchRoot : transform);
        }
    }

    /// <summary>整棵子树里所有名为「选中」/「Selected」/「Select」的节点全部关掉。</summary>
    static void CleanSelectedInSubtree(Transform root)
    {
        if (root == null) return;
        if (root.name == "选中" || root.name == "Selected" || root.name == "Select")
        {
            Image img = root.GetComponent<Image>();
            if (img != null) img.enabled = false;
            if (root.gameObject.activeSelf) root.gameObject.SetActive(false);
        }
        for (int i = 0; i < root.childCount; i++)
            CleanSelectedInSubtree(root.GetChild(i));
    }

    /// <summary>单个按钮子树：有 Image 就 enabled=false，同时 SetActive(false)（预制体叫「选中」，兼容 Selected/Select）。</summary>
    static void CleanSelectedUnder(Button btn)
    {
        if (btn == null) return;
        Transform[] overlays =
        {
            FindDeepChild(btn.transform, "选中"),
            FindDeepChild(btn.transform, "Selected"),
            FindDeepChild(btn.transform, "Select"),
        };
        for (int i = 0; i < overlays.Length; i++)
        {
            Transform t = overlays[i];
            if (t == null) continue;
            Image img = t.GetComponent<Image>();
            if (img != null) img.enabled = false;
            if (t.gameObject.activeSelf) t.gameObject.SetActive(false);
        }
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

        // 绑完立刻清一遍：不管绑定成功与否，都不让预制体里默认亮着的「选中」层留在屏幕上
        if (EnableNavSelectedGuard) ForceCleanSelectedVisuals();
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
