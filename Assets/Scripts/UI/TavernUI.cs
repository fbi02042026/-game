using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 酒馆功能页：只做场景 + 2×2 功能入口。
/// 资源条、底部五入口一律复用主界面节点（TownSharedChrome），禁止自建。
/// 字体：中文 fusion-pixel，数字 PixelFont（经 GameFonts.ApplyToHierarchy）。
/// </summary>
public class TavernUI : MonoBehaviour, ITownPage
{
    public static TavernUI Instance { get; private set; }

    public MainNavTab Tab => MainNavTab.Tavern;

    [Header("功能入口")]
    public Button recruitButton;
    public Button trustButton;
    public Button questButton;
    public Button intelButton;

    [Header("布局")]
    public float bottomNavReserve = 150f;
    public float topBarReserve = 120f;

    [Header("立绘动态（可选，留空则自动找）")]
    public RectTransform portraitRoot;

    bool _built;
    bool _preloaded;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>进 Town 时调用一次：建 UI、绑字体、Canvas 配置，然后隐藏待命</summary>
    public void PreloadOnce()
    {
        if (_preloaded) return;

        if (transform.Find("FeatureGrid") != null)
        {
            AutoBind();
            _built = true;
        }
        else if (!_built)
            BuildContentOnly();

        StripLocalChrome();
        EnsureVisibleTransform();
        ConfigureHostCanvasOnce();
        GameFonts.ApplyToHierarchy(transform);
        EnsurePortraitMotion();
        WireClicks();

        _preloaded = true;
        gameObject.SetActive(false);
    }

    /// <summary>轻量显示：禁止 Resources.Load / Instantiate / 全树 ApplyToHierarchy</summary>
    public void ShowPage()
    {
        if (!_preloaded) PreloadOnce();
        EnsureVisibleTransform();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        SetGuildHallOverlayMode(true);

        Transform hall = GuildHallUI.Instance != null ? GuildHallUI.Instance.transform : transform.root;
        TownSharedChrome.RaiseSharedChrome(hall);
        TownSaveAlign.AlignAll();
    }

    /// <summary>轻量隐藏</summary>
    public void HidePage()
    {
        if (!gameObject.activeSelf) return;
        gameObject.SetActive(false);
        SetGuildHallOverlayMode(false);
    }

    public void Show() => ShowPage();
    public void Hide() => HidePage();

    /// <summary>预制体/编辑器里 scale 可能被存成 0，导致「酒馆空白」</summary>
    void EnsureVisibleTransform()
    {
        if (transform.localScale.sqrMagnitude < 0.0001f)
            transform.localScale = Vector3.one;
    }

    /// <summary>
    /// 嵌在公会下时去掉独立 Canvas，走父 Canvas；保留 scale=1。
    /// </summary>
    bool _canvasConfigured;

    void ConfigureHostCanvasOnce()
    {
        if (_canvasConfigured) return;
        EnsureVisibleTransform();
        GuildHallUI hall = GetComponentInParent<GuildHallUI>();
        bool nestedUnderHall = hall != null && hall.gameObject != gameObject;

        if (nestedUnderHall)
        {
            var raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster != null) Destroy(raycaster);
            var scaler = GetComponent<CanvasScaler>();
            if (scaler != null) Destroy(scaler);
            var own = GetComponent<Canvas>();
            if (own != null) Destroy(own);
            _canvasConfigured = true;
            return;
        }

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();
        canvas.enabled = true;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 5;
        UICanvasSetup.Apply(canvas, Camera.main);
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
        _canvasConfigured = true;
    }

    static readonly string[] GuildHideWhenTavern =
    {
        "HallScene", "Background", "LeftBar", "RightBar",
        "MailButton", "NoticeButton", "ActivityButton",
        "RankButton", "ShopButton", "SettingsButton",
        "TitleBadge"
    };

    static Transform[] _guildHideCache;
    static Button[] _guildHotspotCache;

    public static void SetGuildHallOverlayMode(bool tavernOpen)
    {
        var hall = GuildHallUI.Instance;
        if (hall == null) return;

        EnsureGuildHideCache(hall);
        bool show = !tavernOpen;

        if (_guildHideCache != null)
        {
            for (int i = 0; i < _guildHideCache.Length; i++)
            {
                Transform t = _guildHideCache[i];
                if (t == null) continue;
                if (t.gameObject.activeSelf != show)
                    t.gameObject.SetActive(show);
            }
        }

        SetTopBarResourceOnly(!show);

        if (_guildHotspotCache != null)
        {
            for (int i = 0; i < _guildHotspotCache.Length; i++)
            {
                Button b = _guildHotspotCache[i];
                if (b == null) continue;
                if (b.gameObject.activeSelf != show)
                    b.gameObject.SetActive(show);
            }
        }
    }

    static void EnsureGuildHideCache(GuildHallUI hall)
    {
        if (_guildHideCache != null) return;

        var nodes = new System.Collections.Generic.List<Transform>();
        for (int i = 0; i < GuildHideWhenTavern.Length; i++)
        {
            Transform t = FindDeep(hall.transform, GuildHideWhenTavern[i]);
            if (t != null) nodes.Add(t);
        }
        _guildHideCache = nodes.ToArray();

        _guildHotspotCache = new[]
        {
            hall.noticeBoardButton,
            hall.licenseHallButton,
            hall.armoryButton,
            hall.receptionistButton
        };
    }

    /// <summary>功能页只留金币/体力资源条，隐藏公会名等主界面顶栏装饰。</summary>
    static void SetTopBarResourceOnly(bool resourceOnly)
    {
        var hall = GuildHallUI.Instance;
        if (hall == null) return;
        Transform top = TownSharedChrome.FindDeep(hall.transform, "TopBar");
        if (top == null) return;
        for (int i = 0; i < top.childCount; i++)
        {
            Transform c = top.GetChild(i);
            if (c == null) continue;
            if (c.name == "GoldPanel" || c.name == "体力Panel") continue;
            if (c.name.IndexOf("Stamina", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (c.name == "TopBarBg") continue;
            bool on = !resourceOnly;
            if (c.gameObject.activeSelf != on)
                c.gameObject.SetActive(on);
        }
    }

    public static void ClearGuildHideCache()
    {
        _guildHideCache = null;
        _guildHotspotCache = null;
    }

    /// <summary>兼容旧调用：资源条在主界面，无需酒馆再刷</summary>
    public void RefreshGold()
    {
        // 故意不回调 RefreshAllHudStatic，避免与 GuildHallUI 互相递归
    }

    void StripLocalChrome()
    {
        // 历史运行时/错误预制体可能自带 TopBar，统一移除
        DestroyChildNamed(transform, "TopBar");
        DestroyChildNamed(transform, "SharedResourceBar");

        Transform localNav = transform.Find("BottomNav");
        if (localNav != null && localNav.IsChildOf(transform)
            && localNav.GetComponentInParent<GuildHallUI>() == null)
            Destroy(localNav.gameObject);
    }

    static void DestroyChildNamed(Transform root, string name)
    {
        Transform t = root.Find(name);
        if (t != null) Destroy(t.gameObject);
    }

    void WireClicks()
    {
        if (recruitButton != null)
        {
            recruitButton.onClick.RemoveAllListeners();
            recruitButton.onClick.AddListener(TavernRosterPanel.Show);
        }
        // 信任/任务/情报未做：隐藏，避免空入口
        if (trustButton != null) trustButton.gameObject.SetActive(false);
        if (questButton != null) questButton.gameObject.SetActive(false);
        if (intelButton != null) intelButton.gameObject.SetActive(false);
    }

    static void Wire(Button btn, string toast)
    {
        if (btn == null) return;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => UIManager.Instance?.ShowToast(toast));
    }

    void AutoBind()
    {
        if (recruitButton == null) recruitButton = FindButton("Recruit");
        if (trustButton == null) trustButton = FindButton("Trust");
        if (questButton == null) questButton = FindButton("Quest");
        if (intelButton == null) intelButton = FindButton("Intel");
    }

    Button FindButton(string name)
    {
        var t = FindDeep(transform, name);
        return t != null ? t.GetComponent<Button>() : null;
    }

    /// <summary>只搭建酒馆内容区，不含资源条/底栏</summary>
    public void BuildIfNeeded() => BuildContentOnly();

    void BuildContentOnly()
    {
        if (transform.Find("FeatureGrid") != null)
        {
            AutoBind();
            _built = true;
            return;
        }

        var rt = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
        Stretch(rt);

        var bg = CreateImage(transform, "TavernBackground", new Color(0.22f, 0.12f, 0.08f, 1f));
        Stretch(bg.rectTransform);

        var scene = CreateImage(transform, "TavernScene", new Color(0.45f, 0.28f, 0.18f, 0.55f));
        Stretch(scene.rectTransform);
        scene.rectTransform.offsetMin = new Vector2(0f, bottomNavReserve);
        scene.rectTransform.offsetMax = new Vector2(0f, -topBarReserve);
        var hint = CreateText(scene.transform, "SceneHint", "【酒馆场景插画槽】", 22, new Color(1f, 1f, 1f, 0.35f), number: false);
        Stretch(hint.rectTransform);

        var grid = CreateRect(transform, "FeatureGrid");
        SetAnchored(grid, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, bottomNavReserve + 210f), new Vector2(640f, 360f));

        recruitButton = CreateFeatureCard(grid.transform, "Recruit", "佣兵招募", "招募新佣兵加入队伍",
            new Vector2(-160f, 85f), new Color(0.55f, 0.42f, 0.28f));
        trustButton = CreateFeatureCard(grid.transform, "Trust", "信任交流", "提升信任解锁故事与事件",
            new Vector2(160f, 85f), new Color(0.65f, 0.32f, 0.35f));
        questButton = CreateFeatureCard(grid.transform, "Quest", "酒馆任务", "完成委托获取丰厚奖励",
            new Vector2(-160f, -95f), new Color(0.4f, 0.45f, 0.55f));
        intelButton = CreateFeatureCard(grid.transform, "Intel", "佣兵情报", "查看佣兵资料与背景故事",
            new Vector2(160f, -95f), new Color(0.35f, 0.4f, 0.5f));

        _built = true;
        GameFonts.ApplyToHierarchy(transform);
    }

    Button CreateFeatureCard(Transform parent, string name, string title, string desc, Vector2 pos, Color accent)
    {
        var go = CreateRect(parent, name);
        SetAnchored(go, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(300f, 150f));

        var panel = CreateImage(go.transform, "CardBg", new Color(0.08f, 0.06f, 0.05f, 0.78f));
        Stretch(panel.rectTransform);

        var border = CreateImage(go.transform, "Border", new Color(accent.r, accent.g, accent.b, 0.55f));
        Stretch(border.rectTransform);
        border.rectTransform.offsetMin = new Vector2(2f, 2f);
        border.rectTransform.offsetMax = new Vector2(-2f, -2f);

        var inner = CreateImage(go.transform, "Inner", new Color(0.1f, 0.07f, 0.05f, 0.92f));
        Stretch(inner.rectTransform);
        inner.rectTransform.offsetMin = new Vector2(5f, 5f);
        inner.rectTransform.offsetMax = new Vector2(-5f, -5f);

        var icon = CreateImage(go.transform, "Icon", accent);
        SetAnchored(icon.gameObject, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(52f, 8f), new Vector2(64f, 64f));

        var titleT = CreateText(go.transform, "Title", title, 26, new Color(1f, 0.92f, 0.75f), number: false);
        titleT.fontStyle = FontStyle.Bold;
        titleT.alignment = TextAnchor.MiddleLeft;
        var titleRt = titleT.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.offsetMin = new Vector2(100f, -48f);
        titleRt.offsetMax = new Vector2(-16f, -10f);

        var descT = CreateText(go.transform, "Desc", desc, 16, new Color(0.85f, 0.8f, 0.7f, 0.9f), number: false);
        var descRt = descT.rectTransform;
        descRt.anchorMin = new Vector2(0f, 0f);
        descRt.anchorMax = new Vector2(1f, 1f);
        descRt.offsetMin = new Vector2(100f, 14f);
        descRt.offsetMax = new Vector2(-16f, -52f);
        descT.alignment = TextAnchor.UpperLeft;
        descT.horizontalOverflow = HorizontalWrapMode.Wrap;
        descT.verticalOverflow = VerticalWrapMode.Truncate;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = panel;
        return btn;
    }

    static GameObject CreateRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static Image CreateImage(Transform parent, string name, Color color)
    {
        var go = CreateRect(parent, name);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = true;
        return img;
    }

    static Text CreateText(Transform parent, string name, string content, int size, Color color, bool number)
    {
        var go = CreateRect(parent, name);
        var t = go.AddComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.font = number ? GameFonts.GetNumber() : GameFonts.GetChinese();
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void SetAnchored(GameObject go, Vector2 amin, Vector2 amax, Vector2 pos, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = amin;
        rt.anchorMax = amax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    void EnsurePortraitMotion()
    {
        RectTransform target = portraitRoot != null ? portraitRoot : ResolvePortraitRoot();
        if (target == null) return;

        if (portraitRoot == null)
            portraitRoot = target;

        if (target.GetComponent<PortraitIdleMotion>() == null)
            target.gameObject.AddComponent<PortraitIdleMotion>();
    }

    RectTransform ResolvePortraitRoot()
    {
        string[] names = { "人物", "Portrait", "Character", "TavernPortrait", "立绘" };
        for (int i = 0; i < names.Length; i++)
        {
            Transform t = FindDeep(transform, names[i]);
            if (t != null)
                return t as RectTransform ?? t.GetComponent<RectTransform>();
        }

        Transform scene = FindDeep(transform, "TavernScene");
        if (scene == null) return null;

        var images = scene.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image img = images[i];
            if (img == null || img.sprite == null) continue;
            string n = img.gameObject.name;
            if (n == "SceneHint" || n == "bg" || n == "TavernBackground") continue;
            return img.rectTransform;
        }

        return null;
    }

    static Transform FindDeep(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var r = FindDeep(parent.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }
}
