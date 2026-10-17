using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 进裂隙前的体力消耗确认弹窗（2026-10-17 主人要求）：
/// 选完职业点「进入裂隙」→ 先弹「消耗体力 N」再真进。
///
/// <para>与体力资源挂钩：当前体力 / 上限、可否进入全部实时读 <see cref="StaminaSystem"/>；
/// 体力不足时确认按钮置灰禁用，并显示「MM:SS 后回复 1 点」（与自动恢复同一时钟，
/// 玩家停在弹窗上等恢复，够了按钮自动点亮）。</para>
///
/// <para>纯运行时白模（参考 EvacuateConfirmPopupUI.BuildHierarchy 风格），不依赖预制体；
/// 排版定死在这一个方法里，主人要调外观直接改这里。</para>
/// </summary>
public class StaminaConfirmPopupUI : MonoBehaviour
{
    public static StaminaConfirmPopupUI Instance { get; private set; }

    public GameObject root;
    public Text costText;
    public Text staminaText;
    public Text regenText;
    public Button confirmButton;
    public Button cancelButton;
    public Button dimButton;

    /// <summary>确认=true 进裂隙 / 取消或点空白=false 留在原界面</summary>
    Action<bool> _onDone;
    Image _confirmImg;
    float _refreshTimer;
    /// <summary>本次弹窗的体力消耗（主线 10 / 无限裂隙 20；Open 时按进本标记决定）</summary>
    int _cost = StaminaSystem.ADVENTURE_COST;

    const float RefreshInterval = 0.25f;   // 倒计时刷新频率（秒）
    // 可进入：与职业选择「进入裂隙」按钮同一紫系；不足：置灰
    static readonly Color ConfirmOn = new Color(0.45f, 0.22f, 0.55f, 1f);
    static readonly Color ConfirmOff = new Color(0.42f, 0.42f, 0.46f, 1f);

    public static void Show(Action<bool> onDone) => Ensure().Open(onDone);

    /// <summary>
    /// 消耗数与本次进本模式自动对齐：无限裂隙（AdventureUI.PendingEndlessRift）20 体力，其余 10。
    /// 不改调用方签名 —— 职业选择界面无须知道模式细节。
    /// </summary>
    static int CostForThisEnter()
        => AdventureUI.PendingEndlessRift ? StaminaSystem.ENDLESS_RIFT_COST : StaminaSystem.ADVENTURE_COST;

    static StaminaConfirmPopupUI Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("StaminaConfirmPopupUI", typeof(RectTransform));
        DontDestroyOnLoad(go);
        BuildHierarchy(go);
        return go.GetComponent<StaminaConfirmPopupUI>() ?? go.AddComponent<StaminaConfirmPopupUI>();
    }

    void Awake()
    {
        Instance = this;
        BindRefs();
        Wire();
        // 白模路径 Awake 跑在 BuildHierarchy 建完层级之前（root 回落到宿主自身），
        // 此刻禁用宿主会把整个弹窗藏死 —— 只收真正的 Root 节点；白模修正由 Open 里的 BindRefs 完成
        if (root != null && root != gameObject)
            root.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    static void BuildHierarchy(GameObject host)
    {
        var canvas = host.GetComponent<Canvas>() ?? host.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.StaminaConfirm);
        if (host.GetComponent<GraphicRaycaster>() == null)
            host.AddComponent<GraphicRaycaster>();
        if (host.GetComponent<StaminaConfirmPopupUI>() == null)
            host.AddComponent<StaminaConfirmPopupUI>();

        var root = Mk(host.transform, "Root");
        Stretch(root);

        var dim = Mk(root, "Dim");
        Stretch(dim);
        dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        dim.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;

        var panel = Mk(root, "Panel");
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(520f, 420f);
        panel.gameObject.AddComponent<Image>().color = new Color(0.14f, 0.11f, 0.16f, 0.98f);

        Label(panel, "Title", "进入裂隙", 32, new Vector2(0f, -36f), new Vector2(460f, 44f));

        // 消耗行：初始值按当前进本模式取（主线 10 / 无限裂隙 20），Open 每次再刷
        Label(panel, "Cost", "消耗体力 " + CostForThisEnter(), 26,
            new Vector2(0f, -100f), new Vector2(460f, 40f),
            color: new Color(1f, 0.85f, 0.25f, 1f), bold: true);

        Label(panel, "Stamina", "", 24, new Vector2(0f, -148f), new Vector2(460f, 36f));
        Label(panel, "Regen", "", 20, new Vector2(0f, -192f), new Vector2(460f, 56f),
            color: new Color(0.95f, 0.72f, 0.45f, 1f));

        var cancel = Btn(panel, "CancelButton", new Vector2(0.30f, 0f), "取消",
            new Color(0.32f, 0.28f, 0.24f, 1f));
        var confirm = Btn(panel, "ConfirmButton", new Vector2(0.70f, 0f), "进入裂隙", ConfirmOn);

        // 挂引用（Awake 会先跑一次 BindRefs，这里补上 BuildHierarchy 刚建的节点）
        var ui = host.GetComponent<StaminaConfirmPopupUI>();
        ui.root = root.gameObject;
        ui.costText = FindText(panel, "Cost");
        ui.staminaText = FindText(panel, "Stamina");
        ui.regenText = FindText(panel, "Regen");
        ui.cancelButton = cancel.GetComponent<Button>();
        ui.confirmButton = confirm.GetComponent<Button>();
        ui.dimButton = dim.GetComponent<Button>();
        root.gameObject.SetActive(false);   // 建完先收好，Open 再放出来

        GameFonts.ApplyToHierarchy(host.transform);
    }

    void BindRefs()
    {
        if (root == null)
            root = transform.Find("Root")?.gameObject ?? gameObject;
        var panelTf = FindDeep(root.transform, "Panel");
        if (costText == null) costText = FindText(panelTf, "Cost");
        if (staminaText == null) staminaText = FindText(panelTf, "Stamina");
        if (regenText == null) regenText = FindText(panelTf, "Regen");
        if (confirmButton == null) confirmButton = FindDeep(root.transform, "ConfirmButton")?.GetComponent<Button>();
        if (cancelButton == null) cancelButton = FindDeep(root.transform, "CancelButton")?.GetComponent<Button>();
        if (dimButton == null) dimButton = FindDeep(root.transform, "Dim")?.GetComponent<Button>();
        if (_confirmImg == null && confirmButton != null) _confirmImg = confirmButton.GetComponent<Image>();
    }

    void Wire()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(() => Close(true));
        }
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(() => Close(false));
        }
        // 点空白＝取消（留在职业选择界面，与撤离弹窗同一口径）
        if (dimButton != null)
        {
            dimButton.onClick.RemoveAllListeners();
            dimButton.onClick.AddListener(() => Close(false));
        }
    }

    void Open(Action<bool> onDone)
    {
        BindRefs();
        Wire();
        _onDone = onDone;
        _refreshTimer = RefreshInterval;   // 立即刷一帧
        _cost = CostForThisEnter();
        if (costText != null)
            costText.text = "消耗体力 " + _cost;

        if (root != null)
        {
            root.SetActive(true);
            var canvas = GetComponent<Canvas>();
            if (canvas != null)
                UICanvasSetup.RefreshPopup(canvas, GameConfig.UiSort.StaminaConfirm);
            transform.SetAsLastSibling();
        }
        RefreshStamina();
    }

    /// <summary>
    /// 实时刷新体力与倒计时 —— 全部读 <see cref="StaminaSystem"/>（getter 内部会 Tick），
    /// 与主界面体力 HUD（GuildHallUI）同一真源，不会各说各话。
    /// </summary>
    void RefreshStamina()
    {
        int cur = StaminaSystem.Current;
        bool enough = cur >= _cost;

        if (staminaText != null)
            staminaText.text = $"当前体力 {cur} / {StaminaSystem.Max}";

        if (regenText != null)
        {
            if (StaminaSystem.IsFull)
            {
                if (regenText.gameObject.activeSelf) regenText.gameObject.SetActive(false);
            }
            else
            {
                if (!regenText.gameObject.activeSelf) regenText.gameObject.SetActive(true);
                regenText.text = enough
                    ? "回满 " + StaminaSystem.FormatCountdown(StaminaSystem.SecondsToFull)
                    : "体力不足，" + StaminaSystem.FormatCountdown(StaminaSystem.SecondsToNextPoint) + " 后回复 1 点";
            }
        }

        if (confirmButton != null)
        {
            confirmButton.interactable = enough;
            if (_confirmImg != null) _confirmImg.color = enough ? ConfirmOn : ConfirmOff;
        }
    }

    void Update()
    {
        if (root == null || !root.activeSelf) return;
        _refreshTimer += Time.unscaledDeltaTime;
        if (_refreshTimer < RefreshInterval) return;
        _refreshTimer = 0f;
        RefreshStamina();
    }

    void Close(bool confirmed)
    {
        if (root != null) root.SetActive(false);
        var cb = _onDone;
        _onDone = null;
        cb?.Invoke(confirmed);
    }

    // ——— 白模小工具（与 EvacuateConfirmPopupUI 同款） ———

    static RectTransform Mk(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static RectTransform Label(Transform parent, string name, string text, int size,
        Vector2 pos, Vector2 sizeDelta, TextAnchor align = TextAnchor.MiddleCenter,
        Color? color = null, bool bold = false, bool stretch = false)
    {
        var rt = Mk(parent, name);
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
        t.color = color ?? Color.white;
        t.alignment = align;
        t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.text = text;
        t.raycastTarget = false;
        return rt;
    }

    static RectTransform Btn(Transform parent, string name, Vector2 anchorX, string text, Color color)
    {
        var rt = Mk(parent, name);
        rt.anchorMin = new Vector2(anchorX.x, 0f);
        rt.anchorMax = new Vector2(anchorX.x, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 32f);
        rt.sizeDelta = new Vector2(180f, 60f);
        rt.gameObject.AddComponent<Image>().color = color;
        rt.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
        Label(rt, "Label", text, 24, Vector2.zero, new Vector2(180f, 60f), bold: true, stretch: true);
        return rt;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static Transform FindDeep(Transform rootTf, string name)
    {
        if (rootTf == null) return null;
        if (rootTf.name == name) return rootTf;
        for (int i = 0; i < rootTf.childCount; i++)
        {
            var f = FindDeep(rootTf.GetChild(i), name);
            if (f != null) return f;
        }
        return null;
    }

    static Text FindText(Transform rootTf, string name)
    {
        var t = FindDeep(rootTf, name);
        return t != null ? t.GetComponent<Text>() : null;
    }
}
