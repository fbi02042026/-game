using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 日志里程等级展示与领取（代码 UI）。对齐设计 Lv1–6。
/// </summary>
public class AchievementMilestoneUI : MonoBehaviour
{
    public static AchievementMilestoneUI Instance { get; private set; }

    GameObject _root;
    Text _body;

    public static void Show()
    {
        Ensure().Open();
    }

    static AchievementMilestoneUI Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("AchievementMilestoneUI");
        DontDestroyOnLoad(go);
        return go.AddComponent<AchievementMilestoneUI>();
    }

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Open()
    {
        BuildIfNeeded();
        RefreshBody();
        if (_root != null)
        {
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
        }
        GameFonts.ApplyToHierarchy(transform);
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
    }

    void Close()
    {
        if (_root != null) _root.SetActive(false);
    }

    void BuildIfNeeded()
    {
        if (_root != null) return;
        var canvas = gameObject.GetComponent<Canvas>() ?? gameObject.AddComponent<Canvas>();
        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
        UICanvasSetup.Apply(canvas);
        canvas.sortingOrder = 970;

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
        dim.GetComponent<Button>().onClick.AddListener(Close);

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(_root.transform, false);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(520f, 560f);
        panel.GetComponent<Image>().color = new Color(0.12f, 0.1f, 0.14f, 0.97f);

        var title = CreateText(panel.transform, "日志里程", 28);
        var trt = title.rectTransform;
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -16f);
        trt.sizeDelta = new Vector2(-24f, 36f);

        _body = CreateText(panel.transform, "", 18);
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
        claim.GetComponent<Button>().onClick.AddListener(OnClaimAll);
        var claimTx = CreateText(claim.transform, "一键领取", 22);
        Stretch(claimTx.rectTransform);
        claimTx.alignment = TextAnchor.MiddleCenter;
    }

    static Text CreateText(Transform parent, string msg, int size)
    {
        var go = new GameObject("Text", typeof(RectTransform));
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
}
