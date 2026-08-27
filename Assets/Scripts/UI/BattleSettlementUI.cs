using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗结算界面（GDD §11.3）：撤离/死亡统一展示击杀、伤害、金币等本局成果。
/// 预制体：Resources/Prefabs/Battle/BattleSettlement
/// </summary>
public class BattleSettlementUI : MonoBehaviour
{
    public const string PrefabPath = "Prefabs/Battle/BattleSettlement";

    public static BattleSettlementUI Instance { get; private set; }

    public GameObject root;
    public Text titleText;
    public Text subtitleText;
    public Text statsText;
    public Text rewardsText;
    public Button confirmButton;
    public Text confirmLabel;
    public Image panel;

    Action _onConfirm;
    BattleRunStats _stats;

    public static void Show(BattleRunStats stats, Action onConfirm)
    {
        Ensure().Open(stats, onConfirm);
    }

    public static BattleSettlementUI Ensure()
    {
        if (Instance != null) return Instance;
        var prefab = Resources.Load<GameObject>(PrefabPath);
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab);
            go.name = "BattleSettlement";
        }
        else
        {
            Debug.LogWarning($"[BattleSettlement] 未找到 {PrefabPath}，临时代码搭壳");
            go = new GameObject("BattleSettlement", typeof(RectTransform));
            BuildHierarchy(go);
        }
        DontDestroyOnLoad(go);
        return go.GetComponent<BattleSettlementUI>() ?? go.AddComponent<BattleSettlementUI>();
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
        if (titleText == null)
            titleText = FindDeep(root.transform, "Title")?.GetComponent<Text>();
        if (subtitleText == null)
            subtitleText = FindDeep(root.transform, "Subtitle")?.GetComponent<Text>();
        if (statsText == null)
            statsText = FindDeep(root.transform, "Stats")?.GetComponent<Text>();
        if (rewardsText == null)
            rewardsText = FindDeep(root.transform, "Rewards")?.GetComponent<Text>();
        if (confirmButton == null)
            confirmButton = FindDeep(root.transform, "ConfirmButton")?.GetComponent<Button>();
        if (confirmLabel == null && confirmButton != null)
            confirmLabel = confirmButton.GetComponentInChildren<Text>(true);
    }

    void Wire()
    {
        if (confirmButton == null) return;
        confirmButton.onClick.RemoveAllListeners();
        confirmButton.onClick.AddListener(OnConfirm);
    }

    void Open(BattleRunStats stats, Action onConfirm)
    {
        BindRefs();
        Wire();
        _stats = stats ?? new BattleRunStats();
        _onConfirm = onConfirm;
        Time.timeScale = 0f;

        bool death = _stats.IsDeath;
        if (titleText != null)
        {
            titleText.text = death ? "你阵亡了" : "撤离成功";
            titleText.color = death
                ? new Color(0.85f, 0.28f, 0.28f)
                : new Color(0.35f, 0.78f, 0.45f);
        }
        if (subtitleText != null)
            subtitleText.text = string.IsNullOrEmpty(_stats.StageTitle)
                ? (death ? "本次冒险结束" : "本局成果已带回")
                : _stats.StageTitle + (death ? " · 阵亡" : " · 撤离");

        if (statsText != null)
            statsText.text = BuildStatsBlock(_stats);
        if (rewardsText != null)
            rewardsText.text = BuildRewardsBlock(_stats);

        if (confirmLabel != null)
            confirmLabel.text = "返回城镇";

        if (root != null)
        {
            root.SetActive(true);
            transform.SetAsLastSibling();
        }
        GameFonts.ApplyToHierarchy(transform);
    }

    static string BuildStatsBlock(BattleRunStats s)
    {
        var sb = new StringBuilder();
        sb.AppendLine("【战斗统计】");
        sb.AppendLine($"击杀数量　　{s.KillCount}");
        sb.AppendLine($"精英击杀　　{s.EliteKillCount}");
        sb.AppendLine($"首领击杀　　{s.BossKillCount}");
        sb.AppendLine($"造成伤害　　{Mathf.RoundToInt(s.DamageDealt)}");
        sb.AppendLine($"首领伤害　　{Mathf.RoundToInt(s.BossDamageDealt)}");
        sb.AppendLine($"受到伤害　　{Mathf.RoundToInt(s.DamageTaken)}");
        int sec = Mathf.Max(0, Mathf.RoundToInt(s.BattleTimeSec));
        sb.Append($"战斗时间　　{sec / 60:D2}:{sec % 60:D2}");
        return sb.ToString();
    }

    static string BuildRewardsBlock(BattleRunStats s)
    {
        var sb = new StringBuilder();
        sb.AppendLine("【本局获得】");
        if (s.IsDeath)
            sb.AppendLine("金币　　　　0（阵亡清零）");
        else
            sb.AppendLine($"金币　　　　+{s.GoldGained}");
        sb.AppendLine($"天赋石　　　+{s.TalentGained}");
        sb.AppendLine($"装备　　　　{s.EquipCount} 件（已带回）");
        if (s.EnchantStoneDelta > 0)
            sb.AppendLine($"附魔石　　　+{s.EnchantStoneDelta}");
        if (s.DecomposeMatDelta > 0)
            sb.AppendLine($"强化材料　　+{s.DecomposeMatDelta}");
        return sb.ToString();
    }

    void OnConfirm()
    {
        if (root != null) root.SetActive(false);
        Time.timeScale = 1f;
        var cb = _onConfirm;
        _onConfirm = null;
        cb?.Invoke();
    }

    public static void BuildHierarchy(GameObject host)
    {
        var canvas = host.GetComponent<Canvas>() ?? host.AddComponent<Canvas>();
        UICanvasSetup.Apply(canvas);
        canvas.sortingOrder = 990;
        if (host.GetComponent<GraphicRaycaster>() == null)
            host.AddComponent<GraphicRaycaster>();
        if (host.GetComponent<BattleSettlementUI>() == null)
            host.AddComponent<BattleSettlementUI>();

        var root = Mk(host.transform, "Root");
        Stretch(root);

        var dim = Mk(root, "Dim");
        Stretch(dim);
        dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

        var panel = Mk(root, "Panel");
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(600f, 900f);
        panel.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.1f, 0.14f, 0.98f);

        Label(panel, "Title", "撤离成功", 36, new Vector2(0f, -40f), new Vector2(540f, 48f));
        Label(panel, "Subtitle", "本局成果", 22, new Vector2(0f, -92f), new Vector2(540f, 36f));
        Label(panel, "Stats", "统计", 22, new Vector2(0f, -140f), new Vector2(520f, 320f), TextAnchor.UpperLeft);
        Label(panel, "Rewards", "获得", 22, new Vector2(0f, -480f), new Vector2(520f, 220f), TextAnchor.UpperLeft);

        var btn = Mk(panel, "ConfirmButton");
        btn.anchorMin = btn.anchorMax = new Vector2(0.5f, 0f);
        btn.pivot = new Vector2(0.5f, 0f);
        btn.anchoredPosition = new Vector2(0f, 36f);
        btn.sizeDelta = new Vector2(280f, 56f);
        btn.gameObject.AddComponent<Image>().color = new Color(0.32f, 0.48f, 0.28f, 1f);
        btn.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
        Label(btn, "Label", "返回城镇", 26, Vector2.zero, new Vector2(280f, 56f), TextAnchor.MiddleCenter, true);

        GameFonts.ApplyToHierarchy(host.transform);
    }

    static RectTransform Mk(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static void Label(Transform parent, string name, string text, int size, Vector2 pos, Vector2 sizeDelta,
        TextAnchor align = TextAnchor.MiddleCenter, bool stretch = false)
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
        t.color = Color.white;
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.text = text;
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
