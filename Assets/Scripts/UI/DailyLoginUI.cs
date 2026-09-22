using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 登录奖励弹窗（2026-09-22 第三版：全屏版，参考主人给的角色卡风参考图重做）。
///
/// 版式（720×1280 竖屏逻辑坐标，逻辑宽度 576~720 随屏浮动，全部相对排版）：
///   · 全屏深红底 + 顶部标题「每日登录 / 领取丰厚奖励」+ 右上角 X 关闭
///   · 标题右侧提示卡：「连续登录，好礼不断！更有稀有角色相赠！」
///   · 8 天奖励格 2 行 × 4 列：
///       - 每格：第 N 天标签 / 奖励图标（找不到图用色块+首字）/ 奖励名 ×数量
///       - **X2 角标只有双倍日才显示**（DailyLoginDefs.StarterDoubleDays），领取实发翻倍
///       - 第 8 天（限定佣兵）金框高亮 + 「限定」角标
///       - 已领取压暗；未解锁压暗；今天可领金框
///   · 每日循环一行（今日奖励 + 领取按钮）
///   · 连击档一行（连 3 / 7 / 14 / 30，可点领取）
///   · 底部信息条：累计登录 n / 8 天 + 第 8 天限定佣兵预告
///
/// 预制体接口：若 Resources/Prefabs/UI/DailyLoginFull 存在则实例化它当 Root
/// （美术后续做精修预制体直接放这个路径，代码只在找不到时用临时样式兜底）。
/// ⚠ 排版铁律沿用第二版：画布 Match Height，逻辑高度恒 1280、宽度 576~720 浮动，
///    **禁止写死 1040 这类绝对宽度**，格子尺寸一律运行时按实际宽度算。
/// </summary>
public class DailyLoginUI : MonoBehaviour
{
    public static DailyLoginUI Instance { get; private set; }

    const string PrefabPath = "Prefabs/UI/DailyLoginFull";

    // ---- 配色（参考图：红金主题）----
    static readonly Color ColBgTop = new Color(0.30f, 0.07f, 0.08f, 1f);
    static readonly Color ColBgBottom = new Color(0.14f, 0.04f, 0.05f, 1f);
    static readonly Color ColGold = new Color(1f, 0.84f, 0.42f, 1f);
    static readonly Color ColGoldDeep = new Color(0.72f, 0.50f, 0.14f, 1f);
    static readonly Color ColCell = new Color(0.09f, 0.14f, 0.25f, 1f);
    static readonly Color ColCellClaimed = new Color(0.10f, 0.11f, 0.13f, 1f);
    static readonly Color ColCellLocked = new Color(0.12f, 0.11f, 0.14f, 1f);
    static readonly Color ColBlue = new Color(0.16f, 0.38f, 0.86f, 1f);
    static readonly Color ColRed = new Color(0.62f, 0.10f, 0.10f, 1f);
    static readonly Color ColText = new Color(0.94f, 0.92f, 0.88f, 1f);

    // ---- 纵向布局（y 为相对屏幕中心的偏移，屏幕逻辑高 1280）----
    const float Y_TITLE = 548f;
    const float Y_SUB = 478f;
    const float Y_TIP = 400f;
    const float Y_ROW1 = 225f;
    const float Y_ROW2 = -45f;
    const float CELL_H = 255f;
    const float ROW_GAP = 20f;
    const float Y_CYCLE = -300f;
    const float Y_STREAK = -382f;
    const float Y_INFO = -515f;

    readonly List<Cell> _starterCells = new List<Cell>();

    GameObject _root;
    Text _cycleName;
    Text _cycleState;
    Button _cycleBtn;
    Text _streakTag;
    readonly Button[] _streakBtns = new Button[4];
    readonly Text[] _streakLabels = new Text[4];
    Text _infoDays;
    Text _infoMerc;
    Image _infoMercIcon;

    class Cell
    {
        public GameObject go;
        public Image bg;
        public Text dayText;
        public Image icon;
        public Text iconFallback;
        public Text rewardText;
        public GameObject x2Badge;
        public GameObject rareBadge;
        public Button btn;
        public int day;
    }

    public static void Show() => Ensure().Open();

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

        // 预制体接口：美术做了精修预制体（Resources/Prefabs/UI/DailyLoginFull）就铺它当底，
        // 找不到才用临时色块兜底
        bool hasSkin = TryLoadSkin();
        BuildBackdrop(!hasSkin);
        BuildHeader();
        BuildGrid();
        BuildCycleLine();
        BuildStreakLine();
        BuildInfoBar();

        _root.SetActive(false);
    }

    /// <summary>美术预制体存在则实例化为最底层皮肤，返回是否有皮肤（有就跳过临时色块）。</summary>
    bool TryLoadSkin()
    {
        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null) return false;
        var skin = UnityEngine.Object.Instantiate(prefab, _root.transform, false);
        skin.name = "Skin";
        var srt = skin.GetComponent<RectTransform>();
        if (srt != null) Stretch(srt);
        skin.transform.SetSiblingIndex(0);
        return true;
    }

    void BuildBackdrop(bool useFallback)
    {
        if (!useFallback) return;   // 有美术皮肤时不铺色块，免得盖住图
        // 临时兜底：全屏双层底色（上红下深），中间用色带过渡
        var top = CreateImg(_root.transform, "BgTop", ColBgTop);
        AnchorFillRect(top.rectTransform, 0.62f, 1f);

        var bottom = CreateImg(_root.transform, "BgBottom", ColBgBottom);
        AnchorFillRect(bottom.rectTransform, 0f, 0.63f);

        var band = CreateImg(_root.transform, "BgBand", new Color(0.42f, 0.10f, 0.10f, 1f));
        AnchorFillRect(band.rectTransform, 0.58f, 0.66f);
    }

    void BuildHeader()
    {
        var title = CreateTxt(_root.transform, "Title", "每日登录", 56, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, 0.5f, 0.5f, -60f, Y_TITLE, 420f, 72f);
        title.color = ColGold;

        var sub = CreateImg(_root.transform, "SubBar", ColGoldDeep);
        SetRect(sub.rectTransform, 0.5f, 0.5f, -110f, Y_SUB, 300f, 46f);
        var subTxt = CreateTxt(sub.transform, "Label", "领取丰厚奖励", 24, TextAnchor.MiddleCenter);
        Stretch(subTxt.rectTransform);
        subTxt.color = new Color(0.16f, 0.06f, 0.04f, 1f);

        // 提示卡（标题右下；宽度按窄屏 576 留边：半宽 288，x=150 时最宽 270）
        var tip = CreateImg(_root.transform, "TipCard", new Color(0.10f, 0.05f, 0.06f, 0.86f));
        SetRect(tip.rectTransform, 0.5f, 0.5f, 150f, Y_TIP, 270f, 92f);
        var tipTxt = CreateTxt(tip.transform, "Label", "连续登录，好礼不断！\n更有稀有角色相赠！", 18, TextAnchor.MiddleLeft);
        var trt = tipTxt.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(14f, 8f);
        trt.offsetMax = new Vector2(-14f, -8f);
        tipTxt.color = ColText;

        // 右上角关闭：全屏弹窗只给 X，不给点空白关闭（防误触把登录奖励划走）
        var close = CreateImg(_root.transform, "CloseButton", ColGoldDeep);
        var crt = close.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(1f, 1f);
        crt.anchoredPosition = new Vector2(-28f, -28f);
        crt.sizeDelta = new Vector2(64f, 64f);
        var closeBtn = close.gameObject.AddComponent<Button>();
        closeBtn.onClick.AddListener(Hide);
        var closeTxt = CreateTxt(close.transform, "Label", "X", 30, TextAnchor.MiddleCenter);
        Stretch(closeTxt.rectTransform);
        closeTxt.color = new Color(0.98f, 0.94f, 0.86f, 1f);
    }

    void BuildGrid()
    {
        for (int i = 0; i < DailyLoginDefs.Starter.Length; i++)
        {
            int row = i / 4;
            float y = row == 0 ? Y_ROW1 : Y_ROW2;
            var cell = CreateCell(_root.transform, "Day_" + (i + 1), 0f, y);
            cell.day = i + 1;
            _starterCells.Add(cell);
        }
    }

    Cell CreateCell(Transform parent, string name, float x, float y)
    {
        var bg = CreateImg(parent, name, ColCell);
        SetRect(bg.rectTransform, 0.5f, 0.5f, x, y, 160f, CELL_H);
        var c = new Cell { go = bg.gameObject, bg = bg };

        // 第 N 天标签条
        var tag = CreateImg(bg.transform, "DayTag", new Color(0.13f, 0.22f, 0.40f, 1f));
        AnchorTopStrip(tag.rectTransform, 36f);
        c.dayText = CreateTxt(tag.transform, "Label", "", 22, TextAnchor.MiddleCenter);
        Stretch(c.dayText.rectTransform);
        c.dayText.color = ColGold;

        // 图标区：优先真图，找不到退化为色块 + 奖励名首字
        // （注意：这里只放 Image，**不能加 Button**——加了空按钮会把整卡点击吃掉不冒泡）
        var iconHolder = CreateImg(bg.transform, "IconHolder", new Color(1f, 1f, 1f, 0.06f));
        SetRect(iconHolder.rectTransform, 0.5f, 0.5f, 0f, 34f, 96f, 96f);
        c.icon = iconHolder;
        c.iconFallback = CreateTxt(iconHolder.transform, "Fallback", "", 40, TextAnchor.MiddleCenter);
        Stretch(c.iconFallback.rectTransform);
        c.iconFallback.color = new Color(0.9f, 0.85f, 0.7f, 0.9f);

        // 奖励名（两行）
        c.rewardText = CreateTxt(bg.transform, "Reward", "", 18, TextAnchor.MiddleCenter, true);
        SetRect(c.rewardText.rectTransform, 0.5f, 0f, 0f, 34f, 150f, 52f);
        c.rewardText.color = ColText;

        // X2 角标（只有双倍日显示）
        var x2 = CreateImg(bg.transform, "X2", ColBlue);
        var xrt = x2.rectTransform;
        xrt.anchorMin = xrt.anchorMax = new Vector2(1f, 1f);
        xrt.pivot = new Vector2(1f, 1f);
        xrt.anchoredPosition = new Vector2(-4f, -4f);
        xrt.sizeDelta = new Vector2(52f, 34f);
        var x2Txt = CreateTxt(x2.transform, "Label", "X2", 20, TextAnchor.MiddleCenter);
        Stretch(x2Txt.rectTransform);
        x2Txt.color = Color.white;
        c.x2Badge = x2.gameObject;

        // 限定角标（最后一日）
        var rare = CreateImg(bg.transform, "Rare", ColRed);
        var rrt = rare.rectTransform;
        rrt.anchorMin = rrt.anchorMax = new Vector2(1f, 1f);
        rrt.pivot = new Vector2(1f, 1f);
        rrt.anchoredPosition = new Vector2(-4f, -4f);
        rrt.sizeDelta = new Vector2(64f, 34f);
        var rareTxt = CreateTxt(rare.transform, "Label", "限定", 20, TextAnchor.MiddleCenter);
        Stretch(rareTxt.rectTransform);
        rareTxt.color = new Color(1f, 0.9f, 0.5f, 1f);
        c.rareBadge = rare.gameObject;

        c.btn = bg.gameObject.AddComponent<Button>();
        c.btn.transition = Selectable.Transition.None;
        int day = c.day;
        c.btn.onClick.AddListener(() => OnClickStarter(day));
        return c;
    }

    void BuildCycleLine()
    {
        var bar = CreateImg(_root.transform, "CycleBar", new Color(0.16f, 0.05f, 0.06f, 0.95f));
        var brt = bar.rectTransform;
        brt.anchorMin = new Vector2(0.03f, 0.5f);
        brt.anchorMax = new Vector2(0.97f, 0.5f);
        brt.offsetMin = Vector2.zero;
        brt.offsetMax = Vector2.zero;
        brt.anchoredPosition = new Vector2(0f, Y_CYCLE);
        brt.sizeDelta = new Vector2(0f, 76f);

        var label = CreateTxt(bar.transform, "Tag", "每日循环", 20, TextAnchor.MiddleLeft);
        SetRect(label.rectTransform, 0f, 0.5f, 16f, 0f, 120f, 40f);
        label.color = ColGold;

        _cycleName = CreateTxt(bar.transform, "Name", "", 20, TextAnchor.MiddleLeft);
        SetRect(_cycleName.rectTransform, 0f, 0.5f, 140f, 0f, 300f, 44f);
        _cycleName.color = ColText;

        var btnImg = CreateImg(bar.transform, "ClaimBtn", ColGoldDeep);
        // 左锚（Layout 运行时按条宽摆到贴右），别用右锚，否则 Layout 的 x 换算会翻车
        SetRect(btnImg.rectTransform, 0f, 0.5f, 0f, 0f, 140f, 52f);
        _cycleBtn = btnImg.gameObject.AddComponent<Button>();
        _cycleBtn.onClick.AddListener(OnClickCycle);
        var btnTxt = CreateTxt(btnImg.transform, "Label", "领取", 22, TextAnchor.MiddleCenter);
        Stretch(btnTxt.rectTransform);
        btnTxt.color = new Color(0.16f, 0.06f, 0.04f, 1f);
        _cycleState = btnTxt;
    }

    void BuildStreakLine()
    {
        var bar = CreateImg(_root.transform, "StreakBar", new Color(0.16f, 0.05f, 0.06f, 0.95f));
        var brt = bar.rectTransform;
        brt.anchorMin = new Vector2(0.03f, 0.5f);
        brt.anchorMax = new Vector2(0.97f, 0.5f);
        brt.offsetMin = Vector2.zero;
        brt.offsetMax = Vector2.zero;
        brt.anchoredPosition = new Vector2(0f, Y_STREAK);
        brt.sizeDelta = new Vector2(0f, 76f);

        _streakTag = CreateTxt(bar.transform, "Tag", "连击加成", 20, TextAnchor.MiddleLeft);
        SetRect(_streakTag.rectTransform, 0f, 0.5f, 16f, 0f, 150f, 40f);
        _streakTag.color = ColGold;

        for (int i = 0; i < DailyLoginDefs.Streak.Length && i < _streakBtns.Length; i++)
        {
            var s = DailyLoginDefs.Streak[i];
            var bImg = CreateImg(bar.transform, "Streak_" + s.days, new Color(0.22f, 0.18f, 0.12f, 1f));
            SetRect(bImg.rectTransform, 0f, 0.5f, 0f, 0f, 100f, 52f);
            _streakBtns[i] = bImg.gameObject.AddComponent<Button>();
            _streakLabels[i] = CreateTxt(bImg.transform, "Label", "", 15, TextAnchor.MiddleCenter);
            Stretch(_streakLabels[i].rectTransform);
            _streakLabels[i].color = ColText;
            int days = s.days;
            _streakBtns[i].onClick.AddListener(() => OnClickStreak(days));
        }
    }

    void BuildInfoBar()
    {
        var bar = CreateImg(_root.transform, "InfoBar", new Color(0.20f, 0.06f, 0.07f, 0.97f));
        var brt = bar.rectTransform;
        brt.anchorMin = new Vector2(0.03f, 0.5f);
        brt.anchorMax = new Vector2(0.97f, 0.5f);
        brt.offsetMin = Vector2.zero;
        brt.offsetMax = Vector2.zero;
        brt.anchoredPosition = new Vector2(0f, Y_INFO);
        brt.sizeDelta = new Vector2(0f, 118f);

        // 左：累计登录 n / 8 天
        var cap = CreateTxt(bar.transform, "Cap", "累计登录", 20, TextAnchor.MiddleLeft);
        SetRect(cap.rectTransform, 0f, 0.5f, 20f, 30f, 200f, 34f);
        cap.color = ColGold;
        _infoDays = CreateTxt(bar.transform, "Days", "", 40, TextAnchor.MiddleLeft);
        SetRect(_infoDays.rectTransform, 0f, 0.5f, 20f, -18f, 320f, 56f);
        _infoDays.color = ColText;

        // 右：第 8 天限定佣兵预告（头像 + 文案）
        var iconHolder = CreateImg(bar.transform, "MercIcon", new Color(1f, 1f, 1f, 0.08f));
        SetRect(iconHolder.rectTransform, 0f, 0.5f, 340f, 0f, 88f, 88f);
        _infoMercIcon = iconHolder;
        _infoMerc = CreateTxt(bar.transform, "MercText", "", 16, TextAnchor.MiddleLeft);
        SetRect(_infoMerc.rectTransform, 0f, 0.5f, 396f, 0f, 420f, 80f);
        _infoMerc.color = ColText;
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

    /// <summary>按面板实际宽度重排 8 格与三条横带（逻辑宽 576~720 浮动，必须运行时算）。</summary>
    void Layout()
    {
        var rt = transform as RectTransform;
        if (rt == null) return;
        float w = rt.rect.width;
        if (w <= 0f) return;

        float inner = w - 48f;
        float gap = 12f;
        float cellW = (inner - gap * 3f) / 4f;
        float left = -w / 2f + 24f;

        for (int i = 0; i < _starterCells.Count; i++)
        {
            var c = _starterCells[i];
            if (c?.bg == null) continue;
            int col = i % 4;
            float x = left + col * (cellW + gap) + cellW / 2f;
            int row = i / 4;
            c.bg.rectTransform.sizeDelta = new Vector2(cellW, CELL_H);
            c.bg.rectTransform.anchoredPosition = new Vector2(x, row == 0 ? Y_ROW1 : Y_ROW2);
        }

        LayoutBar("CycleBar", "Name", "ClaimBtn", 120f, 140f);
        LayoutBar("StreakBar", null, null, 160f, 0f);
        LayoutStreakButtons("StreakBar");
        LayoutInfoBar("InfoBar");
    }

    /// <summary>横带内部排版：左标签固定、中段文字弹性、右按钮贴边。</summary>
    void LayoutBar(string barName, string nameNode, string btnNode, float tagW, float btnW)
    {
        var bar = FindChild(_root.transform, barName) as RectTransform;
        if (bar == null) return;
        float wBar = bar.rect.width;
        if (wBar <= 0f) return;

        if (!string.IsNullOrEmpty(nameNode))
        {
            var nameT = FindChild(bar, nameNode) as RectTransform;
            if (nameT != null)
            {
                float x = tagW + 20f;
                float wName = wBar - x - btnW - 32f;
                nameT.anchoredPosition = new Vector2(x + wName * 0.5f, nameT.anchoredPosition.y);
                nameT.sizeDelta = new Vector2(Mathf.Max(120f, wName), nameT.sizeDelta.y);
            }
        }

        if (!string.IsNullOrEmpty(btnNode))
        {
            var btnT = FindChild(bar, btnNode) as RectTransform;
            if (btnT != null)
                btnT.anchoredPosition = new Vector2(wBar - btnW / 2f - 16f, btnT.anchoredPosition.y);
        }
    }

    void LayoutStreakButtons(string barName)
    {
        var bar = FindChild(_root.transform, barName) as RectTransform;
        if (bar == null) return;
        float wBar = bar.rect.width;
        if (wBar <= 0f) return;

        float tagW = 160f, edge = 16f, gap = 10f;
        float usable = wBar - tagW - edge * 2f - gap * 3f;
        float bw = usable / 4f;
        for (int i = 0; i < _streakBtns.Length; i++)
        {
            if (_streakBtns[i] == null) continue;
            var brt = _streakBtns[i].GetComponent<RectTransform>();
            float x = -wBar / 2f + tagW + edge + bw / 2f + i * (bw + gap);
            brt.anchoredPosition = new Vector2(x, 0f);
            brt.sizeDelta = new Vector2(bw, 52f);
        }
    }

    void LayoutInfoBar(string barName)
    {
        var bar = FindChild(_root.transform, barName) as RectTransform;
        if (bar == null) return;
        float wBar = bar.rect.width;
        if (wBar <= 0f) return;

        float iconX = wBar * 0.40f;
        var iconT = FindChild(bar, "MercIcon") as RectTransform;
        if (iconT != null) iconT.anchoredPosition = new Vector2(iconX, 0f);

        var textT = FindChild(bar, "MercText") as RectTransform;
        if (textT != null)
        {
            float x = iconX + 56f;
            float wTxt = wBar - x - 16f;
            textT.anchoredPosition = new Vector2(x + wTxt * 0.5f, 0f);
            textT.sizeDelta = new Vector2(Mathf.Max(160f, wTxt), textT.sizeDelta.y);
        }
    }

    static Transform FindChild(Transform parent, string name)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c != null && c.name == name) return c;
        }
        return null;
    }

    void Refresh()
    {
        int days = DailyLoginSystem.LoginDays;

        // ---- 8 天格 ----
        int lastDay = DailyLoginDefs.Starter.Length;
        for (int i = 0; i < _starterCells.Count && i < DailyLoginDefs.Starter.Length; i++)
        {
            var cell = _starterCells[i];
            var r = DailyLoginDefs.Starter[i];
            int day = i + 1;

            bool dbl = DailyLoginDefs.IsStarterDouble(day);
            bool isLast = day == lastDay;
            bool claimed = DailyLoginSystem.IsStarterClaimed(day);
            bool reached = days >= day;

            cell.dayText.text = "第 " + day + " 天";   // 已领/未解锁状态用底色与文字颜色区分
            cell.rewardText.text = r.DisplayName;
            if (cell.x2Badge != null) cell.x2Badge.SetActive(dbl);
            if (cell.rareBadge != null) cell.rareBadge.SetActive(isLast && !claimed);

            // 图标
            var sp = LoadRewardIcon(r);
            SetIcon(cell, sp, r.name);

            // 状态配色
            if (claimed)
            {
                cell.bg.color = ColCellClaimed;
                cell.dayText.color = new Color(0.6f, 0.6f, 0.6f, 1f);
            }
            else if (reached)
            {
                cell.bg.color = isLast ? new Color(0.30f, 0.16f, 0.04f, 1f) : ColCell;
                cell.dayText.color = ColGold;
            }
            else
            {
                cell.bg.color = ColCellLocked;
                cell.dayText.color = new Color(0.55f, 0.55f, 0.58f, 1f);
            }

            // 最后一日（限定佣兵）金框高亮：未领取时描一圈金色边
            ApplyGlow(cell.bg, isLast && !claimed && reached);

            cell.btn.interactable = reached && !claimed;
        }

        // ---- 每日循环 ----
        int idx = DailyLoginSystem.CycleIndex;
        bool cycleDone = DailyLoginSystem.CycleClaimedToday;
        var cr = DailyLoginDefs.Cycle[idx];
        if (_cycleName != null) _cycleName.text = "今日：" + cr.DisplayName.Replace("\n", "、");
        if (_cycleState != null)
        {
            _cycleState.text = cycleDone ? "已领取" : "领取";
            _cycleState.color = cycleDone ? new Color(0.55f, 0.55f, 0.58f, 1f) : new Color(0.16f, 0.06f, 0.04f, 1f);
        }
        if (_cycleBtn != null) _cycleBtn.interactable = !cycleDone;

        // ---- 连击档 ----
        int streak = DailyLoginSystem.StreakDays;
        if (_streakTag != null) _streakTag.text = $"连击 {streak} 天";
        for (int i = 0; i < DailyLoginDefs.Streak.Length && i < _streakBtns.Length; i++)
        {
            var s = DailyLoginDefs.Streak[i];
            bool sClaimed = DailyLoginSystem.IsStreakClaimed(s.days);
            bool sReached = streak >= s.days;
            if (_streakLabels[i] != null)
            {
                // 按钮窄（窄屏约 80px），文案保持两三个字，状态用颜色区分
                _streakLabels[i].text = sClaimed ? "已领" : $"连{s.days}天";
                _streakLabels[i].color = sReached && !sClaimed ? ColGold : new Color(0.62f, 0.60f, 0.58f, 1f);
            }
            if (_streakBtns[i] != null)
            {
                _streakBtns[i].interactable = sReached && !sClaimed;
                var img = _streakBtns[i].GetComponent<Image>();
                if (img != null)
                    img.color = sClaimed ? new Color(0.14f, 0.14f, 0.15f, 1f)
                              : sReached ? ColGoldDeep
                              : new Color(0.18f, 0.16f, 0.18f, 1f);
            }
        }

        // ---- 底部信息条 ----
        if (_infoDays != null)
            _infoDays.text = Mathf.Min(days, lastDay) + " / " + lastDay + " 天";

        var mercReward = DailyLoginDefs.Starter[lastDay - 1];
        if (_infoMerc != null)
        {
            string name = mercReward.name.Replace("\n", "·");
            bool got = DailyLoginSystem.IsStarterClaimed(lastDay);
            _infoMerc.text = got
                ? "第 " + lastDay + " 天限定佣兵已入队：" + name
                : "第 " + lastDay + " 天可领取\n限定角色：" + name;
        }
        if (_infoMercIcon != null && mercReward.grant == DailyLoginDefs.Grant.Merc)
        {
            var head = MercPortraitSprites.GetHead(mercReward.hireId);
            var img = _infoMercIcon.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = head;
                img.preserveAspect = true;
                img.color = head != null ? Color.white : new Color(1f, 1f, 1f, 0.08f);
            }
        }
    }

    static void ApplyGlow(Image bg, bool on)
    {
        var old = bg.GetComponent<Outline>();
        if (old != null) UnityEngine.Object.Destroy(old);
        if (!on) return;
        var outline = bg.gameObject.AddComponent<Outline>();
        outline.effectColor = ColGold;
        outline.effectDistance = new Vector2(3f, -3f);
    }

    static void SetIcon(Cell cell, Sprite sp, string rewardName)
    {
        if (cell?.icon == null) return;
        var img = cell.icon.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = sp;
            img.preserveAspect = true;
        }
        if (cell.iconFallback != null)
        {
            cell.iconFallback.gameObject.SetActive(sp == null);
            if (sp == null && !string.IsNullOrEmpty(rewardName))
                cell.iconFallback.text = rewardName.Substring(0, 1);
        }
    }

    static Sprite LoadRewardIcon(DailyLoginDefs.Reward r)
    {
        switch (r.grant)
        {
            case DailyLoginDefs.Grant.Merc:
                return MercPortraitSprites.GetHead(r.hireId);
            case DailyLoginDefs.Grant.Resource:
                switch (r.type)
                {
                    case ResourceWallet.ResourceType.Stamina:
                        return Resources.Load<Sprite>("UI/Icons/Stamina");
                    case ResourceWallet.ResourceType.Gold:
                        return Resources.Load<Sprite>("UI/Talent/金币");
                }
                break;
        }
        return null;
    }

    // ============================================================
    // 点击
    // ============================================================

    void OnClickStarter(int day)
    {
        string msg;
        bool ok = DailyLoginSystem.TryClaimStarter(day, out msg);
        GlobalToastUI.Show(msg);
        if (ok) Debug.Log($"[DailyLogin] {msg}");
        Refresh();
    }

    void OnClickCycle()
    {
        string msg;
        bool ok = DailyLoginSystem.TryClaimCycle(out msg);
        GlobalToastUI.Show(msg);
        if (ok) Debug.Log($"[DailyLogin] {msg}");
        Refresh();
    }

    void OnClickStreak(int days)
    {
        string msg;
        bool ok = DailyLoginSystem.TryClaimStreak(days, out msg);
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

    /// <summary>按屏幕竖向比例铺一条横带（minY/maxY 为 0~1）。</summary>
    static void AnchorFillRect(RectTransform rt, float minY, float maxY)
    {
        rt.anchorMin = new Vector2(0f, minY);
        rt.anchorMax = new Vector2(1f, maxY);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    /// <summary>贴父级顶部的通栏标签条。</summary>
    static void AnchorTopStrip(RectTransform rt, float h)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(0f, -h);
        rt.offsetMax = new Vector2(0f, 0f);
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
        t.verticalOverflow = wrapVertical ? VerticalWrapMode.Overflow : VerticalWrapMode.Truncate;
        t.raycastTarget = false;
        t.font = GameFonts.GetChinese();
        return t;
    }
}
