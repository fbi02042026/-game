using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 锻造 / 附魔关简易弹窗。恢复关用 RestStagePopupUI（回 50% 血）。
/// 预制体：
/// · Prefabs/Battle/ForgeStagePopup
/// · Prefabs/Battle/EnchantStagePopup
/// 缺失时用代码搭，结构一致方便你换图。
/// </summary>
public class CraftStagePopupUI : MonoBehaviour
{
    public enum Kind { Forge, Enchant }

    public const string ForgePrefabPath = "Prefabs/Battle/ForgeStagePopup";
    public const string EnchantPrefabPath = "Prefabs/Battle/EnchantStagePopup";

    public static CraftStagePopupUI Instance { get; private set; }

    public GameObject root;
    public Text titleText;
    public Text flavorText;
    public Button continueButton;
    public Text continueLabel;
    public Button closeButton;

    Kind _kind;
    Action _onContinue;
    float _prevTimeScale = 1f;

    public static void ShowForge(Action onContinue) => Ensure(Kind.Forge).Open(Kind.Forge, onContinue);
    public static void ShowEnchant(Action onContinue) => Ensure(Kind.Enchant).Open(Kind.Enchant, onContinue);

    static CraftStagePopupUI Ensure(Kind kind)
    {
        // 锻造/附魔各可有独立实例；没有就共用一个临时壳
        if (Instance != null && Instance.gameObject != null)
            return Instance;

        string path = kind == Kind.Forge ? ForgePrefabPath : EnchantPrefabPath;
        var prefab = Resources.Load<GameObject>(path);
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab);
            go.name = kind == Kind.Forge ? "ForgeStagePopup" : "EnchantStagePopup";
        }
        else
        {
            go = new GameObject(kind == Kind.Forge ? "ForgeStagePopup" : "EnchantStagePopup",
                typeof(RectTransform));
            BuildHierarchy(go, kind);
        }
        DontDestroyOnLoad(go);
        var ui = go.GetComponent<CraftStagePopupUI>();
        if (ui == null) ui = go.AddComponent<CraftStagePopupUI>();
        return ui;
    }

    void Awake()
    {
        Instance = this;
        BindRefs();
        if (root != null) root.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void BindRefs()
    {
        if (root == null)
        {
            var t = transform.Find("Root");
            root = t != null ? t.gameObject : gameObject;
        }
        if (titleText == null)
        {
            var t = root.transform.Find("Panel/Title") ?? FindDeep(root.transform, "Title");
            if (t != null) titleText = t.GetComponent<Text>();
        }
        if (flavorText == null)
        {
            var t = FindDeep(root.transform, "FlavorText");
            if (t != null) flavorText = t.GetComponent<Text>();
        }
        if (continueButton == null)
        {
            var t = FindDeep(root.transform, "ContinueButton");
            if (t != null) continueButton = t.GetComponent<Button>();
        }
        if (continueLabel == null && continueButton != null)
            continueLabel = continueButton.GetComponentInChildren<Text>();
        if (closeButton == null)
        {
            var t = FindDeep(root.transform, "CloseButton");
            if (t != null) closeButton = t.GetComponent<Button>();
        }
    }

    void Open(Kind kind, Action onContinue)
    {
        BindRefs();
        _kind = kind;
        _onContinue = onContinue;
        EnsureCanvas();
        _prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        if (titleText != null)
            titleText.text = kind == Kind.Forge ? "锻造关卡" : "附魔关卡";
        if (flavorText != null)
            flavorText.text = kind == Kind.Forge
                ? "铁匠铺的炉火正旺，强化材料已备好。"
                : "附魔台闪着微光，可为装备附加词条。";
        if (continueLabel != null)
            continueLabel.text = "继续冒险";

        // 锻造关：发强化材料 + 尝试升星一件
        if (kind == Kind.Forge)
        {
            var bm = BattleManager.Instance;
            int stageIdx = bm != null && bm.currentStage != null ? bm.currentStage.stageIndex : 0;
            int chapter = bm != null ? bm.CurrentChapter : 1;
            int mats = StageRoller.RestMaterialReward(stageIdx, chapter);
            ResourceWallet.Add(ResourceWallet.ResourceType.DecomposeMat, mats, save: true, notify: true);
            UIManager.Instance?.ShowToast($"获得强化材料 ×{mats}");
            if (CraftStageApply.TryForgeUpgrade(out string forgeMsg))
                UIManager.Instance?.ShowToast(forgeMsg);
            else if (!string.IsNullOrEmpty(forgeMsg))
                UIManager.Instance?.ShowToast(forgeMsg);
        }
        else if (kind == Kind.Enchant)
        {
            if (CraftStageApply.TryEnchantRandom(out string enchMsg))
                UIManager.Instance?.ShowToast(enchMsg);
            else if (!string.IsNullOrEmpty(enchMsg))
                UIManager.Instance?.ShowToast(enchMsg);
        }

        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(OnContinue);
        }
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(OnContinue);
        }

        if (root != null) root.SetActive(true);
        transform.SetAsLastSibling();
        GameFonts.ApplyToHierarchy(transform);
    }

    void OnContinue()
    {
        Time.timeScale = _prevTimeScale > 0.01f ? _prevTimeScale : 1f;
        if (root != null) root.SetActive(false);
        var cb = _onContinue;
        _onContinue = null;
        cb?.Invoke();
    }

    void EnsureCanvas()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        UICanvasSetup.Apply(canvas);
        canvas.sortingOrder = 920;
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
    }

    public static void BuildHierarchy(GameObject host, Kind kind)
    {
        if (host.GetComponent<RectTransform>() == null)
            host.AddComponent<RectTransform>();
        for (int i = host.transform.childCount - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(host.transform.GetChild(i).gameObject);

        var rootGo = new GameObject("Root", typeof(RectTransform));
        rootGo.transform.SetParent(host.transform, false);
        Stretch(rootGo.GetComponent<RectTransform>());

        var dim = NewImage(rootGo.transform, "Dim", new Color(0f, 0f, 0f, 0.7f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        Color panelC = kind == Kind.Forge
            ? new Color(0.22f, 0.16f, 0.1f, 0.98f)
            : new Color(0.12f, 0.18f, 0.26f, 0.98f);
        var panel = NewImage(rootGo.transform, "Panel", panelC);
        var prt = panel.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(560f, 520f);

        var title = NewText(prt, "Title", kind == Kind.Forge ? "锻造关卡" : "附魔关卡",
            38, TextAnchor.MiddleCenter);
        var trt = title.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -48f);
        trt.sizeDelta = new Vector2(400f, 50f);

        var illu = NewImage(prt, "Illustration", new Color(0.3f, 0.3f, 0.35f, 0.6f));
        var irt = illu.rectTransform;
        irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 1f);
        irt.pivot = new Vector2(0.5f, 1f);
        irt.anchoredPosition = new Vector2(0f, -110f);
        irt.sizeDelta = new Vector2(480f, 220f);
        illu.preserveAspect = true;

        var flavor = NewText(prt, "FlavorText", "", 24, TextAnchor.MiddleCenter);
        var frt = flavor.rectTransform;
        frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0.5f);
        frt.anchoredPosition = new Vector2(0f, -40f);
        frt.sizeDelta = new Vector2(480f, 60f);

        var closeImg = NewImage(prt, "CloseButton", new Color(0.45f, 0.22f, 0.2f, 1f));
        closeImg.raycastTarget = true;
        var crt = closeImg.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(1f, 1f);
        crt.sizeDelta = new Vector2(56f, 56f);
        crt.anchoredPosition = new Vector2(-10f, -10f);
        var closeBtn = closeImg.gameObject.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        var cx = NewText(crt, "Label", "×", 32, TextAnchor.MiddleCenter);
        Stretch(cx.rectTransform);

        var contImg = NewImage(prt, "ContinueButton", new Color(0.22f, 0.5f, 0.32f, 1f));
        contImg.raycastTarget = true;
        var contRt = contImg.rectTransform;
        contRt.anchorMin = contRt.anchorMax = new Vector2(0.5f, 0f);
        contRt.pivot = new Vector2(0.5f, 0f);
        contRt.sizeDelta = new Vector2(360f, 76f);
        contRt.anchoredPosition = new Vector2(0f, 40f);
        var contBtn = contImg.gameObject.AddComponent<Button>();
        contBtn.targetGraphic = contImg;
        var cl = NewText(contRt, "Label", "继续冒险", 30, TextAnchor.MiddleCenter);
        Stretch(cl.rectTransform);

        var ui = host.GetComponent<CraftStagePopupUI>() ?? host.AddComponent<CraftStagePopupUI>();
        ui.root = rootGo;
        ui.titleText = title;
        ui.flavorText = flavor;
        ui.continueButton = contBtn;
        ui.continueLabel = cl;
        ui.closeButton = closeBtn;
        GameFonts.ApplyToHierarchy(host.transform);
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

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Image NewImage(Transform parent, string name, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    static Text NewText(Transform parent, string name, string content, int size, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = GameFonts.GetChinese();
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }
}
