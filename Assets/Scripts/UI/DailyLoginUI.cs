using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 登录奖励弹窗（2026-09-18 第二版：重排版 + 新增连击区）。
/// 纯代码构建，不依赖预制体；样式沿用 TavernUnlockUI / ShopUI 的一套路子。
///
/// ⚠ 排版硬约束（改之前先看）：
///   画布是 720×1280 竖版 + Match Height（见 UICanvasSetup），
///   所以**逻辑高度恒为 1280，逻辑宽度随屏幕在 576~720 之间浮动**（越瘦的屏越窄）。
///   上一版把面板写成死宽 1040、卡片 138×7 横排，在 720 屏上左右各溢出 160、在 576 屏上更严重。
///   本版一律改成：面板按**比例拉伸**，格子宽度**运行时按面板实际宽度算**（见 <see cref="Layout"/>）。
///   任何新控件都必须走相对宽度，禁止再写死 1040 / 450 这类绝对值。
/// </summary>
public class DailyLoginUI : MonoBehaviour
{
    public static DailyLoginUI Instance { get; private set; }

    // ---- 排版常量（y 是相对面板中心的偏移，面板高 = 屏幕逻辑高 × 0.88）----
    const float PANEL_H_RATIO = 0.88f;
    const float PANEL_W_RATIO = 0.92f;
    const float PAD_X = 20f;
    const float GAP = 10f;
    const int STARTER_COLS = 4;
    const float CELL_H = 168f;
    const float STREAK_H = 112f;
    const float TODAY_H = 140f;

    const float Y_TITLE = 500f;
    const float Y_SUB = 450f;
    const float Y_STREAK = 368f;
    const float Y_STARTER_LABEL = 276f;
    const float Y_STARTER_ROW1 = 178f;
    const float Y_STARTER_ROW2 = -10f;
    const float Y_CYCLE_LABEL = -130f;
    // 用户 2026-09-18 定：**不显示下一轮**，只给"今天能领的那一个"，点整卡直接领。
    // 所以没有 7 格进度预览，今日卡下面也就少了一行，Y 整体上移。
    const float Y_TODAY = -250f;
    const float Y_CLOSE = -400f;

    GameObject _root;
    RectTransform _panelRt;
    Text _subText;
    readonly List<Cell> _starterCells = new List<Cell>();
    readonly List<Cell> _streakCells = new List<Cell>();
    Cell _todayCell;

    class Cell
    {
        public GameObject go;
        public Image bg;
        public Text dayText;
        public Text rewardText;
        public Button btn;
        public Text btnLabel;
        public int day;        // 新手 1~7；连击 3/7/14/30；今日卡 0
        public bool isStreak;
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

    // ============================================================
    // 构建
    // ============================================================

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
        _panelRt = panel.rectTransform;
        StretchRatio(_panelRt, (1f - PANEL_W_RATIO) / 2f, (1f - PANEL_H_RATIO) / 2f);

        var title = CreateTxt(panel.transform, "Title", "每日登录", 40, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, 0.5f, 0.5f, 0f, Y_TITLE, 520f, 52f);

        _subText = CreateTxt(panel.transform, "Sub", "", 20, TextAnchor.MiddleCenter);
        SetRect(_subText.rectTransform, 0.5f, 0.5f, 0f, Y_SUB, 980f, 30f);
        _subText.color = new Color(0.72f, 0.78f, 0.9f);

        // ---- 连击加成：4 档节点，横向均分 ----
        for (int i = 0; i < DailyLoginDefs.Streak.Length; i++)
        {
            var cell = CreateCell(panel.transform, "Streak_" + DailyLoginDefs.Streak[i].days, 0f, Y_STREAK);
            cell.isStreak = true;
            cell.day = DailyLoginDefs.Streak[i].days;
            _streakCells.Add(cell);
        }

        var starterLabel = CreateTxt(panel.transform, "StarterLabel", "新手七日 · 按累计登录天数，断签不重置",
            24, TextAnchor.MiddleLeft);
        SetRect(starterLabel.rectTransform, 0.5f, 0.5f, 0f, Y_STARTER_LABEL, 900f, 32f);
        starterLabel.color = new Color(1f, 0.86f, 0.5f);

        for (int i = 0; i < DailyLoginDefs.Starter.Length; i++)
        {
            var cell = CreateCell(panel.transform, "Starter_" + (i + 1), 0f,
                i < STARTER_COLS ? Y_STARTER_ROW1 : Y_STARTER_ROW2);
            cell.day = i + 1;
            _starterCells.Add(cell);
        }

        var cycleLabel = CreateTxt(panel.transform, "CycleLabel", "每日循环 · 7 天一轮，今天",
            24, TextAnchor.MiddleLeft);
        SetRect(cycleLabel.rectTransform, 0.5f, 0.5f, 0f, Y_CYCLE_LABEL, 900f, 32f);
        cycleLabel.color = new Color(1f, 0.86f, 0.5f);

        // ---- 今日可领：整卡可点，点哪都直接领 ----
        _todayCell = CreateCell(panel.transform, "Today", 0f, Y_TODAY);
        _todayCell.day = 0;
        _todayCell.dayText.text = "今日";
        var todayBgBtn = _todayCell.bg.gameObject.AddComponent<Button>();
        todayBgBtn.onClick.AddListener(() => OnClickClaim(_todayCell));

        var close = CreateBtn(panel.transform, "CloseButton", "关闭", new Vector2(0f, Y_CLOSE), new Vector2(220f, 56f));
        close.onClick.AddListener(Hide);

        _root.SetActive(false);
    }

    /// <summary>
    /// 通用卡片（新手格 / 连击格 / 今日卡共用）。
    /// 子控件一律用**锚点**排版：格子高度各不相同（168 / 112 / 124），
    /// 写死 y 偏移会让按钮和文字溢出格子底部，必须按格子自身高度自适应。
    /// </summary>
    Cell CreateCell(Transform parent, string name, float x, float y)
    {
        var bg = CreateImg(parent, name, new Color(0.17f, 0.15f, 0.20f, 1f));
        SetRect(bg.rectTransform, 0.5f, 0.5f, x, y, 120f, CELL_H);

        var c = new Cell { go = bg.gameObject, bg = bg };

        c.dayText = CreateTxt(bg.transform, "Day", "", 22, TextAnchor.MiddleCenter, true);
        AnchorTop(c.dayText.rectTransform, 30f, 6f);
        c.dayText.color = new Color(1f, 0.86f, 0.5f);

        c.rewardText = CreateTxt(bg.transform, "Reward", "", 16, TextAnchor.MiddleCenter, true);
        AnchorFill(c.rewardText.rectTransform, 38f, 54f);
        c.rewardText.color = new Color(0.86f, 0.88f, 0.94f);

        c.btn = CreateBtn(bg.transform, "ClaimBtn", "领取", Vector2.zero, new Vector2(100f, 42f));
        AnchorBottom(c.btn.GetComponent<RectTransform>(), 42f, 8f);
        c.btnLabel = c.btn.GetComponentInChildren<Text>();
        c.btn.onClick.AddListener(() => OnClickClaim(c));
        return c;
    }

    // ============================================================
    // 自适应排版
    // ============================================================

    /// <summary>
    /// 按面板**当前实际宽度**重算所有格子的位置与尺寸。
    /// 必须在 _root.SetActive(true) 之后调用，否则 rect 还是 0。
    /// </summary>
    void Layout()
    {
        if (_panelRt == null) return;
        float panelW = _panelRt.rect.width;
        if (panelW <= 0f) return;

        float half = panelW / 2f;
        float inner = panelW - PAD_X * 2f;
        float left = -half + PAD_X;

        // 新手：4 列 × 2 行（7 格，左对齐）
        float cellW = (inner - GAP * (STARTER_COLS - 1)) / STARTER_COLS;
        for (int i = 0; i < _starterCells.Count; i++)
        {
            int col = i % STARTER_COLS;
            float x = left + col * (cellW + GAP) + cellW / 2f;
            ApplyCellBox(_starterCells[i], x, cellW, CELL_H);
        }

        // 连击：4 档均分
        float streakW = (inner - GAP * (_streakCells.Count - 1)) / _streakCells.Count;
        for (int i = 0; i < _streakCells.Count; i++)
        {
            float x = left + i * (streakW + GAP) + streakW / 2f;
            ApplyCellBox(_streakCells[i], x, streakW, STREAK_H);
        }

        // 今日卡：整行
        if (_todayCell != null)
        {
            var rt = _todayCell.bg.rectTransform;
            rt.sizeDelta = new Vector2(inner, TODAY_H);
            rt.anchoredPosition = new Vector2(0f, Y_TODAY);
        }

        // 标题/副标题跟着收缩，避免窄屏上字被挤出面板
        var subRt = _subText != null ? _subText.rectTransform : null;
        if (subRt != null) subRt.sizeDelta = new Vector2(inner, 30f);
    }

    static void ApplyCellBox(Cell c, float x, float w, float h)
    {
        if (c == null || c.bg == null) return;
        c.bg.rectTransform.sizeDelta = new Vector2(w, h);
        c.bg.rectTransform.anchoredPosition = new Vector2(x, c.bg.rectTransform.anchoredPosition.y);
    }

    // ============================================================
    // 打开 / 刷新
    // ============================================================

    void Open()
    {
        if (_root == null) Build();
        _root.SetActive(true);
        Canvas.ForceUpdateCanvases();
        Layout();
        Refresh();
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
        int streak = DailyLoginSystem.StreakDays;
        if (_subText != null)
            _subText.text = $"累计登录 {days} 天　·　连续登录 {streak} 天";

        // ---- 新手 7 日 ----
        for (int i = 0; i < _starterCells.Count; i++)
        {
            var cell = _starterCells[i];
            var r = DailyLoginDefs.Starter[i];
            cell.dayText.text = "第 " + cell.day + " 天";
            cell.rewardText.text = r.DisplayName;

            bool claimed = DailyLoginSystem.IsStarterClaimed(cell.day);
            bool reached = days >= cell.day;
            SetCell(cell, claimed ? "已领取" : (reached ? "领取" : "未解锁"), reached && !claimed);
            cell.bg.color = claimed ? new Color(0.14f, 0.22f, 0.16f, 1f)
                                    : (reached ? new Color(0.24f, 0.19f, 0.12f, 1f)
                                               : new Color(0.13f, 0.13f, 0.15f, 1f));
        }

        // ---- 连击 4 档 ----
        for (int i = 0; i < _streakCells.Count; i++)
        {
            var cell = _streakCells[i];
            var s = DailyLoginDefs.Streak[i];
            cell.dayText.text = "连 " + s.days + " 天";
            cell.rewardText.text = s.reward.DisplayName;

            bool claimed = DailyLoginSystem.IsStreakClaimed(s.days);
            bool reached = streak >= s.days;
            SetCell(cell, claimed ? "已领" : (reached ? "领取" : "未达成"), reached && !claimed);
            cell.bg.color = claimed ? new Color(0.14f, 0.22f, 0.16f, 1f)
                                    : (reached ? new Color(0.30f, 0.20f, 0.10f, 1f)
                                               : new Color(0.13f, 0.13f, 0.15f, 1f));
        }

        // ---- 今日（不显示下一轮，只给今天这一个）----
        int idx = DailyLoginSystem.CycleIndex;
        bool todayDone = DailyLoginSystem.CycleClaimedToday;

        var cr = DailyLoginDefs.Cycle[idx];
        _todayCell.rewardText.text = cr.DisplayName;
        SetCell(_todayCell, todayDone ? "已领取" : "领取", !todayDone);
        _todayCell.bg.color = todayDone ? new Color(0.14f, 0.22f, 0.16f, 1f)
                                        : new Color(0.24f, 0.19f, 0.12f, 1f);
    }

    /// <summary>改按钮文案与可点状态。**不要在这里改 sizeDelta** —— 卡片用的是锚点排版，
    /// 一旦写 sizeDelta 就会覆盖 AnchorBottom 设好的高度。</summary>
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
        bool ok;
        if (c.isStreak)
            ok = DailyLoginSystem.TryClaimStreak(c.day, out msg);
        else if (c.day <= 0)
            ok = DailyLoginSystem.TryClaimCycle(out msg);
        else
            ok = DailyLoginSystem.TryClaimStarter(c.day, out msg);

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

    /// <summary>按 inset 比例拉伸：inset 0.04 → 左右各留 4%。</summary>
    static void StretchRatio(RectTransform rt, float insetX, float insetY)
    {
        rt.anchorMin = new Vector2(insetX, insetY);
        rt.anchorMax = new Vector2(1f - insetX, 1f - insetY);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    /// <summary>贴父级顶部、左右留 5%、固定高。</summary>
    static void AnchorTop(RectTransform rt, float h, float topPad)
    {
        rt.anchorMin = new Vector2(0.05f, 1f);
        rt.anchorMax = new Vector2(0.95f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(0f, -topPad - h);
        rt.offsetMax = new Vector2(0f, -topPad);
    }

    /// <summary>贴父级底部、左右留 5%、固定高。</summary>
    static void AnchorBottom(RectTransform rt, float h, float botPad)
    {
        rt.anchorMin = new Vector2(0.05f, 0f);
        rt.anchorMax = new Vector2(0.95f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.offsetMin = new Vector2(0f, botPad);
        rt.offsetMax = new Vector2(0f, botPad + h);
    }

    /// <summary>填充父级中间剩余区域（上下各留白），高度随格子自适应。</summary>
    static void AnchorFill(RectTransform rt, float topPad, float botPad)
    {
        rt.anchorMin = new Vector2(0.05f, 0f);
        rt.anchorMax = new Vector2(0.95f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(0f, botPad);
        rt.offsetMax = new Vector2(0f, -topPad);
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

    static Text CreateTxt(Transform parent, string name, string content, int size, TextAnchor align,
        bool wrapVertical = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        // 奖励名带 \n（如「钻石 ×60\n传说碎片 ×2」），必须允许纵向换行，否则第二行会被吃掉
        t.verticalOverflow = wrapVertical ? VerticalWrapMode.Overflow : VerticalWrapMode.Truncate;
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
