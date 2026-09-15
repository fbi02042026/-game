using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 登录奖励弹窗（2026-09-15 新增）：新手 7 日 + 每日循环。
/// 纯代码构建，不依赖预制体；样式沿用 TavernUnlockUI / ShopUI 的一套路子。
/// </summary>
public class DailyLoginUI : MonoBehaviour
{
    public static DailyLoginUI Instance { get; private set; }

    GameObject _root;
    Text _subText;
    readonly List<Cell> _starterCells = new List<Cell>();
    Cell _cycleCell;

    class Cell
    {
        public GameObject go;
        public Image bg;
        public Text dayText;
        public Text rewardText;
        public Button btn;
        public Text btnLabel;
        public int day;      // 新手用 1~7；每日循环用 0
        public bool isCycle;
    }

    public static void Show()
    {
        Ensure().Open();
    }

    public static DailyLoginUI Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("DailyLoginUI", typeof(RectTransform));
        UnityEngine.Object.DontDestroyOnLoad(go);
        var ui = go.AddComponent<DailyLoginUI>();
        ui.Build();
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
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownPopup);

        _root = new GameObject("Root", typeof(RectTransform));
        _root.transform.SetParent(transform, false);
        Stretch(_root.GetComponent<RectTransform>());

        var dim = CreateImg(_root.transform, "Dim", new Color(0f, 0f, 0f, 0.72f));
        Stretch(dim.rectTransform);
        dim.gameObject.AddComponent<Button>().onClick.AddListener(Hide);

        var panel = CreateImg(_root.transform, "Panel", new Color(0.09f, 0.08f, 0.10f, 0.98f));
        SetRect(panel.rectTransform, 0.5f, 0.5f, 0f, 0f, 1040f, 900f);

        var title = CreateTxt(panel.transform, "Title", "每日登录", 40, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, 0.5f, 0.92f, 0f, 0f, 520f, 52f);

        _subText = CreateTxt(panel.transform, "Sub", "", 20, TextAnchor.MiddleCenter);
        SetRect(_subText.rectTransform, 0.5f, 0.865f, 0f, 0f, 980f, 28f);
        _subText.color = new Color(0.72f, 0.78f, 0.9f);

        var starterLabel = CreateTxt(panel.transform, "StarterLabel", "新手七日（按累计登录天数，断签不重置）", 24, TextAnchor.MiddleLeft);
        SetRect(starterLabel.rectTransform, 0f, 1f, 40f, -150f, 900f, 32f);
        starterLabel.rectTransform.pivot = new Vector2(0f, 1f);
        starterLabel.color = new Color(1f, 0.86f, 0.5f);

        // 新手 7 日：一行 7 张卡
        float startX = -450f;
        for (int i = 0; i < DailyLoginDefs.Starter.Length; i++)
        {
            var cell = CreateCell(panel.transform, "Starter_" + (i + 1),
                new Vector2(startX + i * 150f, 60f));
            cell.day = i + 1;
            _starterCells.Add(cell);
        }

        var cycleLabel = CreateTxt(panel.transform, "CycleLabel", "每日循环（7 天一轮）", 24, TextAnchor.MiddleLeft);
        SetRect(cycleLabel.rectTransform, 0f, 1f, 40f, -330f, 900f, 32f);
        cycleLabel.rectTransform.pivot = new Vector2(0f, 1f);
        cycleLabel.color = new Color(1f, 0.86f, 0.5f);

        _cycleCell = CreateCell(panel.transform, "Cycle", new Vector2(-450f, -130f));
        _cycleCell.isCycle = true;
        _cycleCell.dayText.text = "今日";

        var close = CreateBtn(panel.transform, "CloseButton", "关闭", new Vector2(460f, 400f), new Vector2(110f, 52f));
        close.onClick.AddListener(Hide);

        _root.SetActive(false);
    }

    Cell CreateCell(Transform parent, string name, Vector2 pos)
    {
        var bg = CreateImg(parent, name, new Color(0.17f, 0.15f, 0.20f, 1f));
        SetRect(bg.rectTransform, 0.5f, 0.5f, pos.x, pos.y, 138f, 200f);

        var c = new Cell { go = bg.gameObject, bg = bg };

        c.dayText = CreateTxt(bg.transform, "Day", "", 22, TextAnchor.MiddleCenter);
        SetRect(c.dayText.rectTransform, 0.5f, 0.86f, 0f, 0f, 124f, 28f);
        c.dayText.color = new Color(1f, 0.86f, 0.5f);

        c.rewardText = CreateTxt(bg.transform, "Reward", "", 17, TextAnchor.MiddleCenter);
        SetRect(c.rewardText.rectTransform, 0.5f, 0.55f, 0f, 0f, 124f, 70f);
        c.rewardText.color = new Color(0.86f, 0.88f, 0.94f);

        c.btn = CreateBtn(bg.transform, "ClaimBtn", "领取", new Vector2(0f, -62f), new Vector2(112f, 46f));
        c.btnLabel = c.btn.GetComponentInChildren<Text>();
        c.btn.onClick.AddListener(() => OnClickClaim(c));
        return c;
    }

    void Open()
    {
        if (_root == null) Build();
        Refresh();
        _root.SetActive(true);
        transform.SetAsLastSibling();
        GameFonts.ApplyToHierarchy(transform);
    }

    public void Hide()
    {
        if (_root != null) _root.SetActive(false);
    }

    void Refresh()
    {
        int days = DailyLoginSystem.LoginDays;
        if (_subText != null)
            _subText.text = $"累计登录 {days} 天";

        for (int i = 0; i < _starterCells.Count; i++)
        {
            var cell = _starterCells[i];
            var r = DailyLoginDefs.Starter[i];
            cell.dayText.text = "第 " + cell.day + " 天";
            cell.rewardText.text = r.name;

            bool claimed = DailyLoginSystem.IsStarterClaimed(cell.day);
            bool reached = days >= cell.day;
            SetCell(cell, claimed ? "已领取" : (reached ? "领取" : "未解锁"),
                reached && !claimed);
            cell.bg.color = claimed ? new Color(0.14f, 0.22f, 0.16f, 1f)
                                    : (reached ? new Color(0.24f, 0.19f, 0.12f, 1f)
                                               : new Color(0.13f, 0.13f, 0.15f, 1f));
        }

        int idx = DailyLoginSystem.CycleIndex;
        var cr = DailyLoginDefs.Cycle[idx];
        _cycleCell.rewardText.text = cr.name;
        bool todayDone = DailyLoginSystem.CycleClaimedToday;
        SetCell(_cycleCell, todayDone ? "已领取" : "领取", !todayDone);
        _cycleCell.bg.color = todayDone ? new Color(0.14f, 0.22f, 0.16f, 1f)
                                        : new Color(0.24f, 0.19f, 0.12f, 1f);
    }

    static void SetCell(Cell c, string label, bool interactable)
    {
        if (c.btn != null) c.btn.interactable = interactable;
        if (c.btnLabel != null)
        {
            c.btnLabel.text = label;
            c.btnLabel.color = interactable ? new Color(0.98f, 0.85f, 0.4f) : new Color(0.55f, 0.55f, 0.58f);
        }
    }

    void OnClickClaim(Cell c)
    {
        string msg;
        bool ok = c.isCycle
            ? DailyLoginSystem.TryClaimCycle(out msg)
            : DailyLoginSystem.TryClaimStarter(c.day, out msg);

        GlobalToastUI.Show(msg);
        if (ok) Debug.Log($"[DailyLogin] {msg}");
        Refresh();
    }

    // ============================================================
    // UI 小工具
    // ============================================================

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void SetRect(RectTransform rt, float ax, float ay, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    static Image CreateImg(Transform parent, string name, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = c;
        return img;
    }

    static Text CreateTxt(Transform parent, string name, string content, int size, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.raycastTarget = false;
        t.font = GameFonts.GetChinese();
        return t;
    }

    static Button CreateBtn(Transform parent, string name, string label, Vector2 pos, Vector2 size)
    {
        var img = CreateImg(parent, name, new Color(0.25f, 0.22f, 0.28f, 1f));
        SetRect(img.rectTransform, 0.5f, 0.5f, pos.x, pos.y, size.x, size.y);
        var btn = img.gameObject.AddComponent<Button>();
        var txt = CreateTxt(img.transform, "Label", label, 20, TextAnchor.MiddleCenter);
        Stretch(txt.rectTransform);
        return btn;
    }
}
