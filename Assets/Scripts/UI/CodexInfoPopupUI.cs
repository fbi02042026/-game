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

    public bool IsOpen => root != null && root.activeSelf;

    public static void Show(string title, string meta, string desc, string lore, Sprite portraitSprite)
    {
        Ensure().Open(title, meta, desc, lore, portraitSprite);
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
        if (root != null) root.SetActive(false);
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

    void Open(string title, string meta, string desc, string lore, Sprite portraitSprite)
    {
        BindRefs();
        Wire();
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
