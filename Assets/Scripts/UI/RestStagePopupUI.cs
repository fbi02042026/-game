using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 恢复关弹窗：回复生命（不是体力），一次回复最大生命的 50%。
/// 预制体：Resources/Prefabs/Battle/RestStagePopup
/// 结构按仙泉图搭好，你只换 Backdrop / Panel / Illustration / 按钮图即可。
/// </summary>
public class RestStagePopupUI : MonoBehaviour
{
    public const string PrefabPath = "Prefabs/Battle/RestStagePopup";
    /// <summary>一次回复的生命比例</summary>
    public const float HealRatio = 0.5f;

    public static RestStagePopupUI Instance { get; private set; }

    [Header("按名字自动绑，也可 Inspector 覆盖")]
    public GameObject root;
    public Image panel;
    public Image illustration;
    public Text titleText;
    public Text flavorText;
    public Text statusText;
    public Text hpValueText;
    public Image statusIcon;
    public Button closeButton;
    public Button continueButton;
    public Text continueLabel;

    Action _onContinue;
    float _prevTimeScale = 1f;
    bool _healed;

    public static void Show(Action onContinue = null)
    {
        Ensure().Open(onContinue);
    }

    public static RestStagePopupUI Ensure()
    {
        if (Instance != null) return Instance;

        var prefab = Resources.Load<GameObject>(PrefabPath);
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab);
            go.name = "RestStagePopup";
        }
        else
        {
            Debug.LogWarning($"[RestStagePopup] 未找到预制体 {PrefabPath}，改用代码搭建");
            go = new GameObject("RestStagePopup", typeof(RectTransform));
            BuildHierarchy(go);
        }
        DontDestroyOnLoad(go);

        var ui = go.GetComponent<RestStagePopupUI>();
        if (ui == null) ui = go.AddComponent<RestStagePopupUI>();
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
        if (panel == null)
        {
            var t = root.transform.Find("Panel");
            if (t != null) panel = t.GetComponent<Image>();
        }
        if (illustration == null)
        {
            var t = FindDeep(root.transform, "Illustration");
            if (t != null) illustration = t.GetComponent<Image>();
        }
        if (titleText == null)
        {
            var t = FindDeep(root.transform, "Title");
            if (t != null) titleText = t.GetComponent<Text>();
        }
        if (flavorText == null)
        {
            var t = FindDeep(root.transform, "FlavorText");
            if (t != null) flavorText = t.GetComponent<Text>();
        }
        if (statusText == null)
        {
            var t = FindDeep(root.transform, "StatusText");
            if (t != null) statusText = t.GetComponent<Text>();
        }
        if (hpValueText == null)
        {
            var t = FindDeep(root.transform, "HpValue");
            if (t != null) hpValueText = t.GetComponent<Text>();
        }
        if (statusIcon == null)
        {
            var t = FindDeep(root.transform, "StatusIcon");
            if (t != null) statusIcon = t.GetComponent<Image>();
        }
        if (closeButton == null)
        {
            var t = FindDeep(root.transform, "CloseButton");
            if (t != null) closeButton = t.GetComponent<Button>();
        }
        if (continueButton == null)
        {
            var t = FindDeep(root.transform, "ContinueButton");
            if (t != null) continueButton = t.GetComponent<Button>();
        }
        if (continueLabel == null && continueButton != null)
        {
            var t = continueButton.transform.Find("Label");
            if (t != null) continueLabel = t.GetComponent<Text>();
        }
    }

    void Open(Action onContinue)
    {
        BindRefs();
        _onContinue = onContinue;
        _healed = false;

        EnsureCanvas();
        _prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        ApplyHeal();
        GrantRestMaterials();
        RefreshTexts();

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(OnContinue);
        }
        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(OnContinue);
        }

        if (root != null) root.SetActive(true);
        transform.SetAsLastSibling();
        GameFonts.ApplyToHierarchy(transform);
    }

    /// <summary>给英雄 + 所有存活佣兵各回 50% 最大生命。</summary>
    void ApplyHeal()
    {
        if (_healed) return;
        _healed = true;

        var bm = BattleManager.Instance;
        if (bm == null) return;

        HealUnit(bm.hero);
        if (bm.allyUnits != null)
        {
            for (int i = 0; i < bm.allyUnits.Count; i++)
                HealUnit(bm.allyUnits[i]);
        }
        BattleUI.Instance?.UpdateCharacterSlots();
    }

    void GrantRestMaterials()
    {
        var bm = BattleManager.Instance;
        int stageIdx = bm != null && bm.currentStage != null ? bm.currentStage.stageIndex : 0;
        int chapter = bm != null ? bm.CurrentChapter : 1;
        int mats = StageRoller.RestMaterialReward(stageIdx, chapter);
        if (mats <= 0) return;
        ResourceWallet.Add(ResourceWallet.ResourceType.DecomposeMat, mats, save: true, notify: true);
        UIManager.Instance?.ShowToast($"休息补给：强化材料 ×{mats}");
    }

    static void HealUnit(UnitBase u)
    {
        if (u == null || u.isDead || u.attr == null) return;
        float max = u.attr.GetAttr(AttrType.MaxHp);
        if (max <= 0.01f) return;
        float add = max * HealRatio;
        u.currentHp = Mathf.Min(max, u.currentHp + add);
    }

    void RefreshTexts()
    {
        var hero = BattleManager.Instance != null ? BattleManager.Instance.hero : null;
        float cur = 0f, max = 0f;
        if (hero != null && hero.attr != null)
        {
            max = hero.attr.GetAttr(AttrType.MaxHp);
            cur = hero.currentHp;
        }

        if (titleText != null) titleText.text = "生命恢复";
        if (flavorText != null) flavorText.text = "仙泉的力量恢复了你的生命！";
        if (statusText != null) statusText.text = "生命已恢复";
        if (hpValueText != null)
        {
            hpValueText.font = GameFonts.GetNumber();
            hpValueText.text = $"{Mathf.CeilToInt(cur)} / {Mathf.CeilToInt(max)}";
        }
        if (continueLabel != null) continueLabel.text = "继续冒险";
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
        EnsureEventSystem();
    }

    static void EnsureEventSystem()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null) return;
        var go = new GameObject("EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.EventSystems.StandaloneInputModule));
        go.hideFlags = HideFlags.DontSave;
    }

    // ===== 预制体结构（Editor / 运行时兜底共用）=====

    /// <summary>
    /// 节点名固定，方便换美术：
    /// Root/Dim、Panel、CloseButton、Title、TitleDecorL/R、
    /// Illustration、FlavorText、StatusRow(StatusIcon/StatusText/HpValue)、ContinueButton
    /// </summary>
    public static void BuildHierarchy(GameObject host)
    {
        if (host == null) return;
        var rootRt = host.GetComponent<RectTransform>();
        if (rootRt == null) rootRt = host.AddComponent<RectTransform>();

        for (int i = host.transform.childCount - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(host.transform.GetChild(i).gameObject);

        var rootGo = new GameObject("Root", typeof(RectTransform));
        rootGo.transform.SetParent(host.transform, false);
        Stretch(rootGo.GetComponent<RectTransform>());

        var dim = NewImage(rootGo.transform, "Dim", new Color(0f, 0f, 0f, 0.72f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        // 外框：深色木纹占位，你换成带边框的图
        var panelImg = NewImage(rootGo.transform, "Panel", new Color(0.18f, 0.14f, 0.12f, 0.98f));
        var prt = panelImg.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(620f, 920f);
        prt.anchoredPosition = Vector2.zero;

        // 内衬
        var inner = NewImage(prt, "Inner", new Color(0.10f, 0.12f, 0.18f, 1f));
        Stretch(inner.rectTransform);
        inner.rectTransform.offsetMin = new Vector2(18f, 18f);
        inner.rectTransform.offsetMax = new Vector2(-18f, -18f);

        // 关闭
        var closeImg = NewImage(prt, "CloseButton", new Color(0.45f, 0.22f, 0.2f, 1f));
        closeImg.raycastTarget = true;
        var crt = closeImg.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(1f, 1f);
        crt.sizeDelta = new Vector2(64f, 64f);
        crt.anchoredPosition = new Vector2(-12f, -12f);
        var closeBtn = closeImg.gameObject.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        var closeX = NewText(crt, "Label", "×", 36, TextAnchor.MiddleCenter);
        Stretch(closeX.rectTransform);

        // 标题（生命恢复，不是体力）
        var title = NewText(prt, "Title", "生命恢复", 40, TextAnchor.MiddleCenter);
        title.color = new Color(0.45f, 0.75f, 1f, 1f);
        var trt = title.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -48f);
        trt.sizeDelta = new Vector2(360f, 52f);

        var decorL = NewImage(prt, "TitleDecorL", new Color(0.35f, 0.65f, 0.95f, 0.9f));
        var dl = decorL.rectTransform;
        dl.anchorMin = dl.anchorMax = new Vector2(0.5f, 1f);
        dl.pivot = new Vector2(0.5f, 0.5f);
        dl.sizeDelta = new Vector2(28f, 28f);
        dl.anchoredPosition = new Vector2(-150f, -72f);

        var decorR = NewImage(prt, "TitleDecorR", new Color(0.35f, 0.65f, 0.95f, 0.9f));
        var dr = decorR.rectTransform;
        dr.anchorMin = dr.anchorMax = new Vector2(0.5f, 1f);
        dr.pivot = new Vector2(0.5f, 0.5f);
        dr.sizeDelta = new Vector2(28f, 28f);
        dr.anchoredPosition = new Vector2(150f, -72f);

        // 插画区：把仙泉图拖到 Illustration 的 Image.sprite
        var illuBg = NewImage(prt, "IllustrationFrame", new Color(0.08f, 0.1f, 0.14f, 1f));
        var ifr = illuBg.rectTransform;
        ifr.anchorMin = ifr.anchorMax = new Vector2(0.5f, 1f);
        ifr.pivot = new Vector2(0.5f, 1f);
        ifr.anchoredPosition = new Vector2(0f, -110f);
        ifr.sizeDelta = new Vector2(540f, 420f);

        var illu = NewImage(ifr, "Illustration", new Color(0.25f, 0.35f, 0.45f, 0.55f));
        Stretch(illu.rectTransform);
        illu.rectTransform.offsetMin = new Vector2(8f, 8f);
        illu.rectTransform.offsetMax = new Vector2(-8f, -8f);
        illu.preserveAspect = true;
        // 可选：Resources 里有图就自动挂上
        var sp = Resources.Load<Sprite>("Art/UI/RestStage/illustration");
        if (sp != null)
        {
            illu.sprite = sp;
            illu.color = Color.white;
        }

        // 文案
        var flavor = NewText(prt, "FlavorText", "仙泉的力量恢复了你的生命！", 26, TextAnchor.MiddleCenter);
        flavor.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        var frt = flavor.rectTransform;
        frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0.5f);
        frt.anchoredPosition = new Vector2(0f, -80f);
        frt.sizeDelta = new Vector2(520f, 40f);

        var statusRow = new GameObject("StatusRow", typeof(RectTransform));
        statusRow.transform.SetParent(prt, false);
        var srt = statusRow.GetComponent<RectTransform>();
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
        srt.sizeDelta = new Vector2(520f, 48f);
        srt.anchoredPosition = new Vector2(0f, -140f);

        var icon = NewImage(srt, "StatusIcon", new Color(0.35f, 0.85f, 0.45f, 1f));
        var irt = icon.rectTransform;
        irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
        irt.pivot = new Vector2(0f, 0.5f);
        irt.sizeDelta = new Vector2(40f, 40f);
        irt.anchoredPosition = new Vector2(40f, 0f);

        var status = NewText(srt, "StatusText", "生命已恢复", 26, TextAnchor.MiddleLeft);
        var stRt = status.rectTransform;
        stRt.anchorMin = stRt.anchorMax = new Vector2(0f, 0.5f);
        stRt.pivot = new Vector2(0f, 0.5f);
        stRt.sizeDelta = new Vector2(220f, 40f);
        stRt.anchoredPosition = new Vector2(96f, 0f);

        var hp = NewText(srt, "HpValue", "0 / 0", 28, TextAnchor.MiddleRight);
        hp.font = GameFonts.GetNumber();
        hp.color = new Color(1f, 0.92f, 0.45f, 1f);
        var hrt = hp.rectTransform;
        hrt.anchorMin = hrt.anchorMax = new Vector2(1f, 0.5f);
        hrt.pivot = new Vector2(1f, 0.5f);
        hrt.sizeDelta = new Vector2(200f, 40f);
        hrt.anchoredPosition = new Vector2(-40f, 0f);

        // 继续冒险
        var contImg = NewImage(prt, "ContinueButton", new Color(0.22f, 0.55f, 0.32f, 1f));
        contImg.raycastTarget = true;
        var contRt = contImg.rectTransform;
        contRt.anchorMin = contRt.anchorMax = new Vector2(0.5f, 0f);
        contRt.pivot = new Vector2(0.5f, 0f);
        contRt.sizeDelta = new Vector2(420f, 84f);
        contRt.anchoredPosition = new Vector2(0f, 48f);
        var contBtn = contImg.gameObject.AddComponent<Button>();
        contBtn.targetGraphic = contImg;
        var contLabel = NewText(contRt, "Label", "继续冒险", 32, TextAnchor.MiddleCenter);
        contLabel.color = new Color(1f, 0.95f, 0.75f, 1f);
        Stretch(contLabel.rectTransform);

        var ui = host.GetComponent<RestStagePopupUI>();
        if (ui == null) ui = host.AddComponent<RestStagePopupUI>();
        ui.root = rootGo;
        ui.panel = panelImg;
        ui.illustration = illu;
        ui.titleText = title;
        ui.flavorText = flavor;
        ui.statusText = status;
        ui.hpValueText = hp;
        ui.statusIcon = icon;
        ui.closeButton = closeBtn;
        ui.continueButton = contBtn;
        ui.continueLabel = contLabel;

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

    static Image NewImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
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
