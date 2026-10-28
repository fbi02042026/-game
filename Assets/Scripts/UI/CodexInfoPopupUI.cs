using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 通用信息弹窗：图鉴点怪物/佣兵详情。
/// 预制体：Resources/Prefabs/Town/CodexInfoPopup
/// 节点按名绑定；不写回覆盖用户手做布局。
/// </summary>
public class CodexInfoPopupUI : MonoBehaviour
{
    public const string PrefabPath = "Prefabs/Town/CodexInfoPopup";

    public static CodexInfoPopupUI Instance { get; private set; }

    public GameObject root;
    public Image panel;
    public Image portrait;
    public Text titleText;
    public Text metaText;
    public Text descText;
    public Text loreText;
    public Button closeButton;
    public Button dimButton;
    /// <summary>top 节点下的小图标：图鉴入口按怪物/佣兵换 guaiwuicon/yongbingicon。</summary>
    public Image topIcon;
    /// <summary>top 节点下的头条文字（预制体文案「怪物信息」）：图鉴入口按怪物/佣兵换成「怪物图鉴/佣兵图鉴」（2026-10-20）。</summary>
    public Text topTitleText;
    /// <summary>可选「解锁」按钮：预制体里没有时运行时补一个，节点名固定 UnlockButton。</summary>
    public Button unlockButton;
    public Text unlockButtonLabel;

    Action _onUnlock;
    int _baseDescSize;   // Desc/Lore 基准字号（首次打开时记录，降档排版后用于恢复）
    int _baseLoreSize;
    /// <summary>可选大图（佣兵全身立绘）：非空时点弹窗头像放大查看，点任意处关闭（2026-10-20）。</summary>
    Sprite _zoomSprite;
    RectTransform _zoomRoot;
    Image _zoomImg;

    public bool IsOpen => root != null && root.activeSelf;

    public static void Show(string title, string meta, string desc, string lore, Sprite portraitSprite, bool? codexKind = null)
    {
        Ensure().Open(title, meta, desc, lore, portraitSprite, null, null, codexKind: codexKind);
    }

    /// <summary>
    /// 带大图的重载（2026-10-20 用户拍板）：zoomSprite 非空时点弹窗头像可放大查看，
    /// 深色遮罩铺满、点任意处关闭；传 null 则头像不可点。
    /// </summary>
    public static void Show(string title, string meta, string desc, string lore, Sprite portraitSprite, Sprite zoomSprite, bool? codexKind = null)
    {
        Ensure().Open(title, meta, desc, lore, portraitSprite, null, null, zoomSprite, codexKind);
    }

    /// <summary>
    /// 带解锁按钮的重载（2026-09-18 用户拍板 Q2）。
    /// unlockText 非空且 onUnlock 非空才显示按钮；不显示时节点 SetActive(false)。
    /// </summary>
    public static void Show(string title, string meta, string desc, string lore, Sprite portraitSprite,
        string unlockText, Action onUnlock)
    {
        Ensure().Open(title, meta, desc, lore, portraitSprite, unlockText, onUnlock);
    }

    public static void HideActive()
    {
        if (Instance != null) Instance.Close();
    }

    public static CodexInfoPopupUI Ensure()
    {
        if (Instance != null) return Instance;

        var prefab = Resources.Load<GameObject>(PrefabPath);
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab);
            go.name = "CodexInfoPopup";
        }
        else
        {
            Debug.LogWarning($"[CodexInfoPopup] 未找到 {PrefabPath}，临时代码搭壳");
            go = new GameObject("CodexInfoPopup", typeof(RectTransform));
            BuildHierarchy(go);
        }
        DontDestroyOnLoad(go);
        var ui = go.GetComponent<CodexInfoPopupUI>() ?? go.AddComponent<CodexInfoPopupUI>();
        return ui;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        BindRefs();
        Wire();
        ApplyProjectArt();
        if (root != null) root.SetActive(false);
    }

    /// <summary>
    /// 用项目里的现成素材给程序搭的壳“换皮”（2026-09-18 用户要求：程序生成的先在项目里找素材拼）。
    /// 走 UiKeyedBackgrounds 运行时替换，找不到素材时保持原样，不动预制体文件。
    /// 面板 = Frames/内容底（羊皮纸），关闭钮 = Frames/图层 1，解锁钮 = Frames/字底；
    /// 头像框保持预制体原样，不再换成「图层 1」（2026-10-20 用户要求：头像里去掉紫色板背景）。
    /// 纸底上白字看不清，正文统一改深棕。
    /// </summary>
    void ApplyProjectArt()
    {
        if (panel != null && UiKeyedBackgrounds.ApplyLogFrame(panel, "内容底", preserveAspect: false))
        {
            // 【2026-10-21 主人拍板】换图可以，图的显示方式不许代码改 —— 之前这里强制
            // panel.type = Simple，把主人做好的九宫格「内容底」压成整图拉伸（边框被拉粗、
            // 中间可写区被吃掉），正是文字骑到框上的根因。现在只换图，不动 type。
            var dark = new Color(0.24f, 0.15f, 0.08f, 1f);
            if (titleText != null) titleText.color = dark;
            if (metaText != null) metaText.color = new Color(0.35f, 0.24f, 0.14f, 1f);
            if (descText != null) descText.color = dark;
            if (loreText != null) loreText.color = new Color(0.32f, 0.21f, 0.12f, 1f);
        }

        var closeImg = closeButton != null ? closeButton.GetComponent<Image>() : null;
        if (closeImg != null)
            UiKeyedBackgrounds.ApplyLogFrame(closeImg, "图层 1", preserveAspect: false);

        if (unlockButton != null)
        {
            var ubImg = unlockButton.GetComponent<Image>();
            if (ubImg != null)
                UiKeyedBackgrounds.ApplyLogFrame(ubImg, "字底", preserveAspect: false);
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void BindRefs()
    {
        if (root == null)
            root = transform.Find("Root")?.gameObject ?? gameObject;
        if (panel == null)
            panel = FindDeep(root.transform, "Panel")?.GetComponent<Image>();
        if (portrait == null)
            portrait = FindDeep(root.transform, "Portrait")?.GetComponent<Image>();
        if (titleText == null)
            titleText = FindDeep(root.transform, "Title")?.GetComponent<Text>();
        if (metaText == null)
            metaText = FindDeep(root.transform, "Meta")?.GetComponent<Text>();
        if (descText == null)
            descText = FindDeep(root.transform, "Desc")?.GetComponent<Text>();
        if (loreText == null)
            loreText = FindDeep(root.transform, "Lore")?.GetComponent<Text>();
        if (closeButton == null)
            closeButton = FindDeep(root.transform, "CloseButton")?.GetComponent<Button>();
        if (dimButton == null)
            dimButton = FindDeep(root.transform, "Dim")?.GetComponent<Button>();
        if (topIcon == null)
            topIcon = FindDeep(root.transform, "icon")?.GetComponent<Image>();
        if (topTitleText == null)
            topTitleText = FindDeep(root.transform, "Name")?.GetComponent<Text>();
    }

    void Wire()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }
        if (dimButton != null)
        {
            dimButton.onClick.RemoveAllListeners();
            dimButton.onClick.AddListener(Close);
        }
    }

    /// <summary>
    /// 取解锁按钮：预制体里找 UnlockButton，找不到才运行时补建（不动预制体结构）。
    /// create=false 时只查找，不补建。
    /// </summary>
    Button ResolveUnlockButton(bool create)
    {
        if (unlockButton == null)
            unlockButton = FindDeep(root.transform, "UnlockButton")?.GetComponent<Button>();
        if (unlockButton == null && create)
            unlockButton = BuildUnlockButton();
        if (unlockButton != null && unlockButtonLabel == null)
            unlockButtonLabel = unlockButton.GetComponentInChildren<Text>();
        return unlockButton;
    }

    Button BuildUnlockButton()
    {
        var host = panel != null ? panel.transform : (root != null ? root.transform : transform);
        var rt = CreateUi(host, "UnlockButton", true);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 96f);   // 关闭按钮（y=28，高 52）上方，不压住它
        rt.sizeDelta = new Vector2(240f, 56f);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = new Color(0.28f, 0.46f, 0.30f, 1f);
        // 程序生成的按钮底也换成项目素材（字底横条），找不到素材时保留绿色兜底
        UiKeyedBackgrounds.ApplyLogFrame(img, "字底", preserveAspect: false);
        var btn = rt.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.ColorTint;
        unlockButtonLabel = AddLabel(rt, "Label", "解锁", 24, Vector2.zero,
            new Vector2(240f, 56f), TextAnchor.MiddleCenter, stretch: true);
        GameFonts.ApplyToHierarchy(rt);
        return btn;
    }

    void OnClickUnlock()
    {
        var cb = _onUnlock;
        if (cb == null) return;
        cb();
    }

    void Open(string title, string meta, string desc, string lore, Sprite portraitSprite,
        string unlockText, Action onUnlock, Sprite zoomSprite = null, bool? codexKind = null)
    {
        BindRefs();
        Wire();

        _onUnlock = onUnlock;
        bool showUnlock = !string.IsNullOrEmpty(unlockText) && onUnlock != null;
        var ub = ResolveUnlockButton(showUnlock);
        if (ub != null)
        {
            ub.gameObject.SetActive(showUnlock);
            if (showUnlock)
            {
                ub.onClick.RemoveAllListeners();
                ub.onClick.AddListener(OnClickUnlock);
                if (unlockButtonLabel != null) unlockButtonLabel.text = unlockText;
            }
        }
        if (titleText != null) titleText.text = title ?? "";   // 这里是怪物/佣兵名字，任何情况都不覆盖（2026-10-20 主人纠正：之前被错改成「怪物图鉴」）
        // 图鉴入口（2026-10-20 主人拍板）：按怪物/佣兵换 top/icon 图标，顶部头条（top/Name，预制体文案「怪物信息」）同步换成「怪物图鉴/佣兵图鉴」；
        // 其他调用方（酒馆解锁等）不传 codexKind，保持原样
        if (codexKind.HasValue)
        {
            if (topTitleText != null)
                topTitleText.text = codexKind.Value ? "佣兵图鉴" : "怪物图鉴";
            if (topIcon != null)
                topIcon.sprite = Resources.Load<Sprite>(codexKind.Value
                    ? "UI/Art/冒险日志/yongbingicon"
                    : "UI/Art/冒险日志/guaiwuicon");
        }
        if (metaText != null) metaText.text = meta ?? "";
        if (descText != null) descText.text = desc ?? "";
        if (loreText != null) loreText.text = lore ?? "";
        if (portrait != null)
        {
            portrait.sprite = portraitSprite;
            portrait.preserveAspect = true;
            portrait.color = portraitSprite != null ? Color.white : new Color(1f, 1f, 1f, 0.15f);
            if (portraitSprite != null)
                PortraitIdleMotion.EnsureOn(portrait.rectTransform, 0.18f);
            else
            {
                var idle = portrait.GetComponent<PortraitIdleMotion>();
                if (idle != null) idle.enabled = false;
            }

            // 点头像放大查看大图：有大图才可点（怪物弹窗不传大图，保持不可点）
            _zoomSprite = (zoomSprite != null && portraitSprite != null) ? zoomSprite : null;
            // 【2026-10-22 修】预制体 Portrait 的 Image 是 m_RaycastTarget: 0（CodexInfoPopup.prefab:383），
            // 射线收不到 → 挂上去的 Button 永远不响应，黑底立绘层弹不出来。只有可放大时才打开，
            // 怪物弹窗（无大图）保持预制体原样关闭。
            portrait.raycastTarget = _zoomSprite != null;
            var zoomBtn = portrait.GetComponent<Button>();
            if (zoomBtn == null && _zoomSprite != null)
            {
                zoomBtn = portrait.gameObject.AddComponent<Button>();
                zoomBtn.transition = Selectable.Transition.None;
            }
            if (zoomBtn != null)
            {
                zoomBtn.onClick.RemoveAllListeners();
                zoomBtn.onClick.AddListener(ShowZoom);
                zoomBtn.enabled = _zoomSprite != null;
            }
        }
        if (root != null)
        {
            root.SetActive(true);
            transform.SetAsLastSibling();
        }
        GameFonts.ApplyToHierarchy(transform);
        LayoutContent();
    }

    public void Close()
    {
        HideZoom();
        if (root != null) root.SetActive(false);
    }

    /// <summary>点任意处关闭大图（整层遮罩就是一个按钮）。</summary>
    public void HideZoom()
    {
        if (_zoomRoot != null && _zoomRoot.gameObject.activeSelf) _zoomRoot.gameObject.SetActive(false);
    }

    void ShowZoom()
    {
        if (_zoomSprite == null) return;
        EnsureZoomLayer();
        if (_zoomImg != null)
        {
            _zoomImg.sprite = _zoomSprite;
            _zoomImg.preserveAspect = true;
        }
        if (_zoomRoot != null)
        {
            _zoomRoot.gameObject.SetActive(true);
            _zoomRoot.SetAsLastSibling();
        }
    }

    /// <summary>
    /// 大图层：深色遮罩铺满 + 居中立绘（保持比例）。运行时按需补建一次，不动预制体；
    /// 立绘自身 raycastTarget 关掉，点哪里都算点遮罩 → 任意处关闭。
    /// </summary>
    void EnsureZoomLayer()
    {
        if (_zoomRoot != null) return;
        var host = root != null ? root.transform : transform;
        _zoomRoot = CreateUi(host, "ZoomLayer", true);
        Stretch(_zoomRoot);
        var maskImg = _zoomRoot.gameObject.AddComponent<Image>();
        maskImg.color = new Color(0f, 0f, 0f, 0.88f);
        var btn = _zoomRoot.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(HideZoom);

        var picRt = CreateUi(_zoomRoot, "ZoomPic", true);
        Stretch(picRt);
        picRt.offsetMin = new Vector2(60f, 80f);    // 四周留边，preserveAspect 自动居中适配
        picRt.offsetMax = new Vector2(-60f, -80f);
        _zoomImg = picRt.gameObject.AddComponent<Image>();
        _zoomImg.raycastTarget = false;
    }

    /// <summary>
    /// 【2026-10-19 排版优化】Desc/Lore 按实际文本高度流式排列。
    /// 【2026-10-21 主人拍板】图 / 字可以换，**字的宽度不许代码改**。
    /// 所以这里只写纵向：Desc 紧贴 Meta 底、Lore 紧跟 Desc 实际高度下方；
    /// Desc/Lore 的 Width、x、sizeDelta 全部以预制体为准，代码一笔都不碰
    /// （旧写法把 sizeDelta 读出来再原样写回，仍是代码在动预制体的值，且拉伸锚点下会算错宽度）。
    /// 文本长度超出可用区时才降字号，避免压到底部「关闭/解锁」按钮。
    /// </summary>
    void LayoutContent()
    {
        if (panel == null || descText == null || loreText == null) return;
        var panelRt = panel.transform as RectTransform;
        var descRt = descText.rectTransform;
        var loreRt = loreText.rectTransform;
        if (panelRt == null || descRt == null || loreRt == null) return;

        float loreGap = 16f;            // Desc 与 Lore 的间距（超长时自动压缩）
        const float metaGap = 6f;       // Meta 与 Desc 的间距

        // 顶部起点 = Meta 框底 + 间距（预制体原值 -360 与 Meta 框底 -366 有 6px 重叠）
        float top = -372f;
        var metaRt = metaText != null ? metaText.rectTransform : null;
        if (metaRt != null)
            top = metaRt.anchoredPosition.y - metaRt.sizeDelta.y - metaGap;

        // 底部红线：关闭按钮（y=28 + 高 52）上方留 12px；有解锁按钮时抬高红线
        float bottomLimit = -panelRt.rect.height + 92f;
        if (unlockButton != null && unlockButton.gameObject.activeSelf)
            bottomLimit = -panelRt.rect.height + 164f;

        // 首次记录基准字号；之后每次先恢复，避免上一次降档残留
        if (_baseDescSize <= 0)
        {
            _baseDescSize = descText.fontSize;
            _baseLoreSize = loreText.fontSize;
        }
        else
        {
            descText.fontSize = _baseDescSize;
            loreText.fontSize = _baseLoreSize;
        }

        // 文本高度按预制体给的宽度算（preferredHeight 自动取 rect 宽，代码不写宽度）
        float descH = descText.preferredHeight;
        float loreH = loreText.preferredHeight;

        float avail = top - bottomLimit;
        if (descH + loreGap + loreH > avail)
        {
            int[] ds = { 19, 17, 16 };
            int[] ls = { 18, 16, 15 };
            for (int i = 0; i < ds.Length; i++)
            {
                descText.fontSize = ds[i];
                loreText.fontSize = ls[i];
                descH = descText.preferredHeight;
                loreH = loreText.preferredHeight;
                if (descH + loreGap + loreH <= avail) break;
            }
            if (descH + loreGap + loreH > avail)
                loreGap = Mathf.Max(4f, avail - descH - loreH);
        }

        // 只动 y：宽度、x、sizeDelta 一律保留预制体原值
        descRt.anchoredPosition = new Vector2(descRt.anchoredPosition.x, top);
        loreRt.anchoredPosition = new Vector2(loreRt.anchoredPosition.x, top - descH - loreGap);
    }

    /// <summary>编辑器生成 / 运行时兜底共用。</summary>
    public static void BuildHierarchy(GameObject host)
    {
        var canvas = host.GetComponent<Canvas>() ?? host.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.BattleLegacyChoose);
        if (host.GetComponent<GraphicRaycaster>() == null)
            host.AddComponent<GraphicRaycaster>();
        if (host.GetComponent<CodexInfoPopupUI>() == null)
            host.AddComponent<CodexInfoPopupUI>();

        var root = CreateUi(host.transform, "Root", true);
        Stretch(root);

        var dim = CreateUi(root, "Dim", true);
        Stretch(dim);
        var dimImg = dim.gameObject.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.55f);
        dim.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;

        var panelRt = CreateUi(root, "Panel", true);
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(560f, 720f);
        var panelImg = panelRt.gameObject.AddComponent<Image>();
        panelImg.color = new Color(0.16f, 0.12f, 0.1f, 0.98f);

        var frame = CreateUi(panelRt, "PortraitFrame", true);
        frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 1f);
        frame.pivot = new Vector2(0.5f, 1f);
        frame.anchoredPosition = new Vector2(0f, -36f);
        frame.sizeDelta = new Vector2(220f, 220f);
        frame.gameObject.AddComponent<Image>().color = new Color(0.3f, 0.22f, 0.16f, 1f);

        var portraitRt = CreateUi(frame, "Portrait", true);
        Stretch(portraitRt);
        portraitRt.offsetMin = new Vector2(10f, 10f);
        portraitRt.offsetMax = new Vector2(-10f, -10f);
        var pImg = portraitRt.gameObject.AddComponent<Image>();
        pImg.color = new Color(1f, 1f, 1f, 0.2f);
        pImg.preserveAspect = true;

        AddLabel(panelRt, "Title", "名称", 32, new Vector2(0f, -280f), new Vector2(500f, 44f));
        AddLabel(panelRt, "Meta", "类型 · 地点", 20, new Vector2(0f, -330f), new Vector2(500f, 36f));
        AddLabel(panelRt, "Desc", "描述", 22, new Vector2(0f, -430f), new Vector2(500f, 140f), TextAnchor.UpperLeft);
        AddLabel(panelRt, "Lore", "趣闻", 20, new Vector2(0f, -560f), new Vector2(500f, 100f), TextAnchor.UpperLeft);

        var close = CreateUi(panelRt, "CloseButton", true);
        close.anchorMin = close.anchorMax = new Vector2(0.5f, 0f);
        close.pivot = new Vector2(0.5f, 0f);
        close.anchoredPosition = new Vector2(0f, 28f);
        close.sizeDelta = new Vector2(200f, 52f);
        close.gameObject.AddComponent<Image>().color = new Color(0.42f, 0.3f, 0.18f, 1f);
        close.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
        AddLabel(close, "Label", "关闭", 24, Vector2.zero, new Vector2(200f, 52f), TextAnchor.MiddleCenter, stretch: true);

        GameFonts.ApplyToHierarchy(host.transform);
    }

    static RectTransform CreateUi(Transform parent, string name, bool withRect)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static Text AddLabel(Transform parent, string name, string text, int size, Vector2 pos, Vector2 sizeDelta,
        TextAnchor align = TextAnchor.MiddleCenter, bool stretch = false)
    {
        var rt = CreateUi(parent, name, true);
        if (stretch)
        {
            Stretch(rt);
        }
        else
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
        }
        var t = rt.gameObject.AddComponent<Text>();
        t.font = GameFonts.GetChinese();
        t.fontSize = size;
        t.color = Color.white;
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.text = text;
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static Transform FindDeep(Transform root, string name)
    {
        // 2026-10-28 收敛：实现已统一到 UiFind/UiFactory 唯一真源，此处保留私有签名以免动调用点
        return UiFind.Deep(root, name);
    }
}
