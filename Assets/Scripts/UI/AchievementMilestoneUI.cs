using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 日志里程等级展示与领取弹窗。对齐设计 Lv1–6。
///
/// <para>【2026-10-17 主人拍板】预制体化：优先实例化 <see cref="PrefabResourcePath"/>
/// （由 Tools/UI/生成日志里程弹窗预制体 菜单生成，外观全归预制体，脚本只填数据）；
/// 预制体缺失时回退运行时代码搭建（结构 / 坐标 / 配色与生成器完全一致，仅作过渡）。</para>
///
/// <para>节点命名两套路径统一：Root / Dim / Panel / Title / Close / Body / Claim，
/// 绑定与事件挂接共用同一套 Find 逻辑。</para>
/// </summary>
public class AchievementMilestoneUI : MonoBehaviour
{
    public const string PrefabResourcePath = "Prefabs/UI/AchievementMilestoneUI";

    public static AchievementMilestoneUI Instance { get; private set; }

    GameObject _root;
    Text _body;
    Button _dimBtn;
    Button _closeBtn;
    Button _claimBtn;
    bool _fromPrefab;      // 走了预制体实例化路径（字体已由生成器指定，运行时不再整树套字体）
    bool _popupApplied;    // 画布参数只套一次，之后每次 Open 只 RefreshPopup（防切换场景后弹窗变大）
    bool _wired;           // 按钮事件只挂一次（预制体实例跨多次开关复用）

    public static void Show()
    {
        Ensure().Open();
    }

    public static void Hide()
    {
        if (Instance != null)
            Instance.Close();
    }

    static AchievementMilestoneUI Ensure()
    {
        if (Instance != null) return Instance;

        // 预制体优先
        var prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab != null)
        {
            var go = Object.Instantiate(prefab);
            DontDestroyOnLoad(go);
            return go.GetComponent<AchievementMilestoneUI>() ?? go.AddComponent<AchievementMilestoneUI>();
        }

        var fb = new GameObject("AchievementMilestoneUI");
        DontDestroyOnLoad(fb);
        return fb.AddComponent<AchievementMilestoneUI>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Open()
    {
        EnsureHierarchy();
        BindRefs();
        Wire();
        var canvas = GetComponent<Canvas>();
        if (canvas != null)
            UICanvasSetup.RefreshPopup(canvas, GameConfig.UiSort.BattleMilestone);
        RefreshBody();
        if (_root != null)
        {
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
        }
        if (!_fromPrefab)
            GameFonts.ApplyToHierarchy(transform);
    }

    /// <summary>决定界面来源：预制体已有 Root 就直接用；没有才走代码白模。</summary>
    void EnsureHierarchy()
    {
        if (_root != null) return;

        var rootTf = transform.Find("Root");
        if (rootTf != null)
        {
            _root = rootTf.gameObject;
            _fromPrefab = true;
            var canvas = GetComponent<Canvas>();
            if (canvas != null && !_popupApplied)
            {
                UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.BattleMilestone);
                _popupApplied = true;
            }
            return;
        }

        BuildIfNeeded();   // 白模（内含 ApplyPopup）
    }

    void RefreshBody()
    {
        var sb = new StringBuilder();
        sb.AppendLine(AdventureLogMileage.FormatStatusLine());
        sb.AppendLine();
        for (int lv = 2; lv <= AdventureLogMileage.MaxLevel; lv++)
        {
            bool can = AdventureLogMileage.CanClaimLevel(lv);
            bool claimed = AdventureLogMileage.IsLevelClaimed(lv);
            bool reached = AdventureLogMileage.GetLevel() >= lv;
            string state = claimed ? "已领取" : (can ? "可领取" : (reached ? "可领？" : "未达成"));
            if (reached && !claimed && !can) state = "未达成";
            int need = AdventureLogMileage.LevelThresholds[lv - 1];
            sb.AppendLine($"Lv{lv}（{need}点）{AdventureLogMileage.FormatRewardPreview(lv)}");
            sb.AppendLine($"  → {state}");
            sb.AppendLine();
        }
        if (_body != null) _body.text = sb.ToString();
    }

    void OnClaimAll()
    {
        int n = AdventureLogMileage.ClaimAllAvailable();
        UIManager.Instance?.ShowToast(n > 0 ? $"领取 {n} 个日志里程奖励" : "暂无可领里程");
        RefreshBody();
        RedDot.RefreshCommon();
        if (n == 0 || !AdventureLogMileage.HasUnclaimedLevel())
            Close();
    }

    void Close()
    {
        if (_root != null) _root.SetActive(false);
    }

    void BindRefs()
    {
        if (_body != null && _claimBtn != null) return;
        var rootTf = _root != null ? _root.transform : transform;
        var panelTf = FindDeep(rootTf, "Panel") ?? rootTf;
        if (_body == null) _body = FindText(panelTf, "Body") ?? FindText(rootTf, "Body");
        if (_dimBtn == null) _dimBtn = FindDeep(rootTf, "Dim")?.GetComponent<Button>();
        if (_closeBtn == null) _closeBtn = FindDeep(rootTf, "Close")?.GetComponent<Button>();
        if (_claimBtn == null) _claimBtn = FindDeep(rootTf, "Claim")?.GetComponent<Button>();
    }

    void Wire()
    {
        if (_wired) return;
        if (_dimBtn != null)
        {
            _dimBtn.onClick.RemoveAllListeners();
            _dimBtn.onClick.AddListener(Close);
        }
        if (_closeBtn != null)
        {
            _closeBtn.onClick.RemoveAllListeners();
            _closeBtn.onClick.AddListener(Close);
        }
        if (_claimBtn != null)
        {
            _claimBtn.onClick.RemoveAllListeners();
            _claimBtn.onClick.AddListener(OnClaimAll);
        }
        _wired = true;
    }

    // ——— 代码白模（预制体缺失时的过渡路径，结构与生成器保持一致） ———

    void BuildIfNeeded()
    {
        var canvas = gameObject.GetComponent<Canvas>() ?? gameObject.AddComponent<Canvas>();
        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.BattleMilestone);
        _popupApplied = true;

        _root = new GameObject("Root", typeof(RectTransform));
        _root.transform.SetParent(transform, false);
        var rootRt = _root.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;

        var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image), typeof(Button));
        dim.transform.SetParent(_root.transform, false);
        Stretch(dim.GetComponent<RectTransform>());
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(_root.transform, false);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(520f, 560f);
        panel.GetComponent<Image>().color = new Color(0.12f, 0.1f, 0.14f, 0.97f);

        var title = CreateText(panel.transform, "Title", "日志里程", 28);
        var trt = title.rectTransform;
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -16f);
        trt.sizeDelta = new Vector2(-24f, 36f);

        var closeBtn = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
        closeBtn.transform.SetParent(panel.transform, false);
        var closeRt = closeBtn.GetComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(1f, 1f);
        closeRt.anchorMax = new Vector2(1f, 1f);
        closeRt.pivot = new Vector2(1f, 1f);
        closeRt.anchoredPosition = new Vector2(-12f, -12f);
        closeRt.sizeDelta = new Vector2(40f, 40f);
        closeBtn.GetComponent<Image>().color = new Color(0.35f, 0.25f, 0.18f, 1f);
        var closeTx = CreateText(closeBtn.transform, "Label", "×", 28);
        Stretch(closeTx.rectTransform);
        closeTx.alignment = TextAnchor.MiddleCenter;

        _body = CreateText(panel.transform, "Body", "", 18);
        _body.alignment = TextAnchor.UpperLeft;
        var brt = _body.rectTransform;
        brt.anchorMin = new Vector2(0f, 0f);
        brt.anchorMax = new Vector2(1f, 1f);
        brt.offsetMin = new Vector2(20f, 70f);
        brt.offsetMax = new Vector2(-20f, -60f);

        var claim = new GameObject("Claim", typeof(RectTransform), typeof(Image), typeof(Button));
        claim.transform.SetParent(panel.transform, false);
        var crt = claim.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0f);
        crt.anchorMax = new Vector2(0.5f, 0f);
        crt.pivot = new Vector2(0.5f, 0f);
        crt.anchoredPosition = new Vector2(0f, 16f);
        crt.sizeDelta = new Vector2(220f, 44f);
        claim.GetComponent<Image>().color = new Color(0.45f, 0.32f, 0.18f, 1f);
        var claimTx = CreateText(claim.transform, "Label", "一键领取", 22);
        Stretch(claimTx.rectTransform);
        claimTx.alignment = TextAnchor.MiddleCenter;
    }

    static Text CreateText(Transform parent, string name, string msg, int size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = GameFonts.GetChinese();
        t.fontSize = size;
        t.color = Color.white;
        t.text = msg;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
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
