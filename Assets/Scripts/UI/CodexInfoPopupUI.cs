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
    /// <summary>可选「解锁」按钮：预制体里没有时运行时补一个，节点名固定 UnlockButton。</summary>
    public Button unlockButton;
    public Text unlockButtonLabel;

    Action _onUnlock;

    public bool IsOpen => root != null && root.activeSelf;

    public static void Show(string title, string meta, string desc, string lore, Sprite portraitSprite)
    {
        Ensure().Open(title, meta, desc, lore, portraitSprite, null, null);
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
        Instance = this;
        BindRefs();
        Wire();
        ApplyProjectArt();
        if (root != null) root.SetActive(false);
    }

    /// <summary>
    /// 用项目里的现成素材给程序搭的壳“换皮”（2026-09-18 用户要求：程序生成的先在项目里找素材拼）。
    /// 走 UiKeyedBackgrounds 运行时替换，找不到素材时保持原样，不动预制体文件。
    /// 面板 = Frames/内容底（羊皮纸），头像框/关闭钮 = Frames/图层 1，解锁钮 = Frames/字底；
    /// 纸底上白字看不清，正文统一改深棕。
    /// </summary>
    void ApplyProjectArt()
    {
        if (panel != null && UiKeyedBackgrounds.ApplyLogFrame(panel, "内容底", preserveAspect: false))
        {
            panel.type = Image.Type.Simple;
            var dark = new Color(0.24f, 0.15f, 0.08f, 1f);
            if (titleText != null) titleText.color = dark;
            if (metaText != null) metaText.color = new Color(0.35f, 0.24f, 0.14f, 1f);
            if (descText != null) descText.color = dark;
            if (loreText != null) loreText.color = new Color(0.32f, 0.21f, 0.12f, 1f);
        }

        var frameImg = FindDeep(root != null ? root.transform : transform, "PortraitFrame")?.GetComponent<Image>();
        if (frameImg != null)
            UiKeyedBackgrounds.ApplyLogFrame(frameImg, "图层 1", preserveAspect: false);

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
        string unlockText, Action onUnlock)
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
        if (titleText != null) titleText.text = title ?? "";
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
        }
        if (root != null)
        {
            root.SetActive(true);
            transform.SetAsLastSibling();
        }
        GameFonts.ApplyToHierarchy(transform);
    }

    public void Close()
    {
        if (root != null) root.SetActive(false);
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
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var f = FindDeep(root.GetChild(i), name);
            if (f != null) return f;
        }
        return null;
    }
}
