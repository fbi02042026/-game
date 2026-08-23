using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 成就里程碑展示与领取（代码 UI）。
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
        var sys = AchievementSystem.Instance;
        var data = SaveSystem.Instance?.Data;
        var sb = new StringBuilder();
        int pts = data != null ? data.totalAchievementPoints : 0;
        sb.AppendLine($"成就点数：{pts}");
        sb.AppendLine();
        if (sys == null)
        {
            sb.Append("成就系统未就绪");
        }
        else
        {
            for (int id = 1; id <= 5; id++)
            {
                bool can = sys.CanClaimMilestone(id);
                bool claimed = data != null && data.claimedMilestoneIds != null && data.claimedMilestoneIds.Contains(id);
                string state = claimed ? "已领取" : (can ? "可领取" : "未达成");
                sb.AppendLine($"里程 {id}：{state}");
            }
        }
        if (_body != null) _body.text = sb.ToString();
    }

    void OnClaimAll()
    {
        var sys = AchievementSystem.Instance;
        if (sys == null) return;
        int n = 0;
        for (int id = 1; id <= 5; id++)
        {
            if (sys.ClaimMilestone(id)) n++;
        }
        UIManager.Instance?.ShowToast(n > 0 ? $"领取 {n} 个里程奖励" : "暂无可领里程");
        RefreshBody();
        SaveSystem.Instance?.Save();
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
        prt.sizeDelta = new Vector2(480f, 420f);
        panel.GetComponent<Image>().color = new Color(0.12f, 0.1f, 0.14f, 0.97f);

        var title = CreateText(panel.transform, "成就里程碑", 28);
        var trt = title.rectTransform;
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -16f);
        trt.sizeDelta = new Vector2(-24f, 36f);

        _body = CreateText(panel.transform, "", 20);
        var brt = _body.rectTransform;
        brt.anchorMin = new Vector2(0.08f, 0.28f);
        brt.anchorMax = new Vector2(0.92f, 0.82f);
        brt.offsetMin = brt.offsetMax = Vector2.zero;
        _body.alignment = TextAnchor.UpperLeft;

        var claim = new GameObject("Claim", typeof(RectTransform), typeof(Image), typeof(Button));
        claim.transform.SetParent(panel.transform, false);
        var crt = claim.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0f);
        crt.pivot = new Vector2(0.5f, 0f);
        crt.anchoredPosition = new Vector2(-90f, 24f);
        crt.sizeDelta = new Vector2(160f, 48f);
        claim.GetComponent<Image>().color = new Color(0.25f, 0.55f, 0.35f, 1f);
        CreateText(claim.transform, "领取", 22);
        claim.GetComponent<Button>().onClick.AddListener(OnClaimAll);

        var close = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
        close.transform.SetParent(panel.transform, false);
        var clrt = close.GetComponent<RectTransform>();
        clrt.anchorMin = clrt.anchorMax = new Vector2(0.5f, 0f);
        clrt.pivot = new Vector2(0.5f, 0f);
        clrt.anchoredPosition = new Vector2(90f, 24f);
        clrt.sizeDelta = new Vector2(160f, 48f);
        close.GetComponent<Image>().color = new Color(0.4f, 0.35f, 0.35f, 1f);
        CreateText(close.transform, "关闭", 22);
        close.GetComponent<Button>().onClick.AddListener(Close);
    }

    void Close()
    {
        if (_root != null) _root.SetActive(false);
    }

    static Text CreateText(Transform parent, string content, int size)
    {
        var go = new GameObject("T", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Stretch(go.GetComponent<RectTransform>());
        var t = go.GetComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
