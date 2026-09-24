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
    Image _infoBar;     // 底部整条（领完佣兵后压暗）

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
        public Text markText;   // ★ 45°「已领」斜字（claimed 状态才显示）
        public Button btn;
        public int day;
    }

    public static void Show() => Ensure().Open();

    public static DailyLoginUI Ensure()
    {
        if (Instance != null) return Instance;

        // 完整弹窗预制体优先（2026-09-23）：自带 Canvas + 全部节点，组件直接挂实例根上
        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab != null && prefab.GetComponent<Canvas>() != null)
        {
            var go = UnityEngine.Object.Instantiate(prefab);
            go.name = "DailyLoginUI";
            DontDestroyOnLoad(go);
            var ui = go.GetComponent<DailyLoginUI>();
            if (ui == null) ui = go.AddComponent<DailyLoginUI>();
            ui.Build();
            return ui;
        }

        // 兜底：老路径，代码从零生成（预制体缺失/还是旧皮肤层版时走这里）
        var fb = new GameObject("DailyLoginUI", typeof(RectTransform));
        DontDestroyOnLoad(fb);
        var ui2 = fb.AddComponent<DailyLoginUI>();
        ui2.Build();
        return ui2;
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
        // 完整弹窗预制体：画布与节点都在预制体上，直接按名字绑定（2026-09-23）
        if (TryBindFullPrefab()) return;

        // ---- 兜底：代码从零生成 ----
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
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

    /// <summary>
    /// 完整弹窗预制体绑定（2026-09-23）：预制体自带 Canvas + 全部节点（见 Tools/每日登录/生成器），
    /// 这里只按名字抓引用，不再创建节点。任何必需节点缺失都返回 false 走代码生成兜底。
    /// 节点名即契约：Cell_N / DayTag/Label / IconHolder(+Fallback) / Reward / X2 / Rare / Mark
    ///               CycleBar(Tag/Name/ClaimBtn+Label) / StreakBar(Tag/Streak_天数) / InfoBar(Cap/Days/MercIcon/MercText)
    /// </summary>
    bool TryBindFullPrefab()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) return false;                 // 不是完整弹窗预制体
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownPopup);

        // 先全部检查完再写入成员，避免绑一半失败后回退造成节点重复
        var bound = new List<Cell>();
        for (int i = 1; i <= DailyLoginDefs.Starter.Length; i++)
        {
            var t = FindChild(transform, "Cell_" + i);
            if (t == null)
            {
                Debug.LogError("[DailyLoginUI] 预制体缺格子 Cell_" + i + "，回退代码生成");
                return false;
            }
            var bg = t.GetComponent<Image>();
            if (bg == null) return false;

            var c = new Cell { go = t.gameObject, bg = bg, day = i };

            var tag = FindChild(t, "DayTag");
            c.dayText = tag != null ? tag.GetComponentInChildren<Text>() : null;

            var icon = FindChild(t, "IconHolder");
            if (icon != null)
            {
                c.icon = icon.GetComponent<Image>();
                c.iconFallback = icon.GetComponentInChildren<Text>();
            }

            var reward = FindChild(t, "Reward");
            c.rewardText = reward != null ? reward.GetComponent<Text>() : null;

            var x2 = FindChild(t, "X2");
            c.x2Badge = x2 != null ? x2.gameObject : null;
            var rare = FindChild(t, "Rare");
            c.rareBadge = rare != null ? rare.gameObject : null;
            var mark = FindChild(t, "Mark");
            c.markText = mark != null ? mark.GetComponent<Text>() : null;

            c.btn = t.GetComponent<Button>();
            if (c.btn == null) c.btn = t.gameObject.AddComponent<Button>();
            c.btn.transition = Selectable.Transition.None;
            // 用 cell.day 而不是绑死的 i：轮回后 cell.day 会在 Refresh 里换成累计天数（9、10…）
            c.btn.onClick.AddListener(() => OnClickStarter(c.day));
            bound.Add(c);
        }

        // 2026-09-23 主人决定：每日循环条 / 连击加成条不再在界面显示（连击改为双倍奖励）。
        // 所以这三条都按「可选」处理——缺了就跳过，BindBars/Refresh/Layout 内部都有判空。
        BindBars();
        _starterCells.AddRange(bound);
        _root = gameObject;                               // Layout/Refresh/Hide 全按根节点找名字
        return true;
    }

    /// <summary>绑定三条横带的子节点引用（名字同兜底路径，Layout() 直接复用）。</summary>
    void BindBars()
    {
        var cycle = FindChild(transform, "CycleBar");
        if (cycle != null)
        {
            var nameT = FindChild(cycle, "Name");
            _cycleName = nameT != null ? nameT.GetComponent<Text>() : null;
            var btnT = FindChild(cycle, "ClaimBtn");
            if (btnT != null)
            {
                _cycleBtn = btnT.GetComponent<Button>();
                if (_cycleBtn == null) _cycleBtn = btnT.gameObject.AddComponent<Button>();
                _cycleBtn.onClick.AddListener(OnClickCycle);
                _cycleState = btnT.GetComponentInChildren<Text>();
            }
        }

        var streak = FindChild(transform, "StreakBar");
        if (streak != null)
        {
            var tagT = FindChild(streak, "Tag");
            _streakTag = tagT != null ? tagT.GetComponent<Text>() : null;
            for (int i = 0; i < DailyLoginDefs.Streak.Length && i < _streakBtns.Length; i++)
            {
                var s = DailyLoginDefs.Streak[i];
                var bt = FindChild(streak, "Streak_" + s.days);
                if (bt == null) continue;                 // 改了 Streak 天数后重跑生成器即可
                _streakBtns[i] = bt.GetComponent<Button>();
                if (_streakBtns[i] == null) _streakBtns[i] = bt.gameObject.AddComponent<Button>();
                int streakDays = s.days;
                _streakBtns[i].onClick.AddListener(() => OnClickStreak(streakDays));
                _streakLabels[i] = bt.GetComponentInChildren<Text>();
            }
        }

        var info = FindChild(transform, "InfoBar");
        if (info != null)
        {
            _infoBar = info.GetComponent<Image>();
            var daysT = FindChild(info, "Days");
            _infoDays = daysT != null ? daysT.GetComponent<Text>() : null;
            var mercT = FindChild(info, "MercText");
            _infoMerc = mercT != null ? mercT.GetComponent<Text>() : null;
            var iconT = FindChild(info, "MercIcon");
            _infoMercIcon = iconT != null ? iconT.GetComponent<Image>() : null;
        }
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

        // ★ 45° 已领斜字（兜底路径同样要有，绑定路径由预制体提供）
        var markGo = new GameObject("Mark", typeof(RectTransform));
        markGo.transform.SetParent(bg.transform, false);
        markGo.layer = 5;
        var mrt = markGo.GetComponent<RectTransform>();
        mrt.anchorMin = mrt.anchorMax = new Vector2(0.5f, 0.5f);
        mrt.pivot = new Vector2(0.5f, 0.5f);
        mrt.anchoredPosition = Vector2.zero;
        mrt.sizeDelta = new Vector2(220f, 44f);
        markGo.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        c.markText = CreateTxt(markGo.transform, "Label", "已 领", 34, TextAnchor.MiddleCenter);
        Stretch(c.markText.rectTransform);
        c.markText.color = Color.white;
        var markOl = markGo.AddComponent<Outline>();
        markOl.effectColor = new Color(0f, 0f, 0f, 0.85f);
        markOl.effectDistance = new Vector2(2f, -2f);
        markGo.SetActive(false);

        c.btn = bg.gameObject.AddComponent<Button>();
        c.btn.transition = Selectable.Transition.None;
        // 同上：点的时候读 cell.day（Refresh 会把它换成累计天数）
        c.btn.onClick.AddListener(() => OnClickStarter(c.day));
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
        _infoBar = bar;
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
        // 2026-09-23 轮回（主人要求）：第 1 轮 1~8 天，第 2 轮 9~16 天，第 3 轮 17~24 天……
        // 奖励仍按 Starter[i] 循环取；已领记录按累计天数存，所以不需要重置存档。
        int lastDay = DailyLoginDefs.Starter.Length;
        int round = lastDay > 0 ? (Mathf.Max(1, days) - 1) / lastDay : 0;
        for (int i = 0; i < _starterCells.Count && i < lastDay; i++)
        {
            var cell = _starterCells[i];
            int day = round * lastDay + i + 1;                      // ★ 轮回后的真实累计天数
            var r = DailyLoginSystem.EffectiveStarterReward(day);   // 轮回后佣兵日自动换碎片，显示=实发
            cell.day = day;                        // 点击领取用的就是这个

            bool dbl = DailyLoginDefs.IsStarterDouble(day);
            bool isLast = i == lastDay - 1;        // 本轮最后一格（佣兵）
            bool claimed = DailyLoginSystem.IsStarterClaimed(day);
            bool reached = days >= day;

            cell.dayText.text = "第 " + day + " 天";   // 已领/未解锁状态用底色与文字颜色区分
            cell.rewardText.text = r.DisplayName;
            if (cell.x2Badge != null) cell.x2Badge.SetActive(dbl);
            if (cell.rareBadge != null) cell.rareBadge.SetActive(isLast && !claimed);

            // 图标
            var sp = LoadRewardIcon(r);
            SetIcon(cell, sp, r.name);

            // ★ 已领斜字：只有 claimed 状态亮出来
            if (cell.markText != null) cell.markText.gameObject.SetActive(claimed);

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

            // ★ 2026-09-23 主人要求：当天可领的那一格套光圈，点它就能领（其余按已领/未到区分）
            bool isToday = reached && !claimed && day == days;
            ApplyGlow(cell.bg, isToday);

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
        // 2026-09-23 主人要求：领完佣兵后这条压暗 + 显示「已领」
        int mercDay = round * lastDay + lastDay;          // 本轮的佣兵日（8 / 16 / 24 …）
        // 佣兵只能解锁一次，拿到过就永久显示「已领」并压暗（不是只按本轮判定）
        bool mercGot = DailyLoginSystem.IsStarterMercOwned()
                       || DailyLoginSystem.IsStarterClaimed(mercDay);

        if (_infoDays != null)
            _infoDays.text = mercGot ? "已领" : days + " 天";

        var mercReward = DailyLoginDefs.Starter[lastDay - 1];
        if (_infoMerc != null)
        {
            string name = mercReward.name.Replace("\n", "·");
            _infoMerc.text = mercGot
                ? "已领"
                : "第 " + mercDay + " 天可领取\n限定角色：" + name;
        }
        // 整条压暗
        if (_infoBar != null)
            _infoBar.color = mercGot ? new Color(0.16f, 0.15f, 0.16f, 1f) : Color.white;
        if (_infoDays != null) _infoDays.color = mercGot ? new Color(0.55f, 0.55f, 0.58f, 1f) : ColText;
        if (_infoMerc != null) _infoMerc.color = mercGot ? new Color(0.55f, 0.55f, 0.58f, 1f) : ColText;
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

    /// <summary>
    /// 给「当天可领」的格子套一圈金色光圈（用 Outline 描边实现，沿贴图轮廓走）。
    /// 美术后面要换成真正的光圈图时：在 Cell 下加一个 Glow 子节点（Image），这里改成开关它的 active 即可。
    /// </summary>
    static void ApplyGlow(Image bg, bool on)
    {
        var old = bg.GetComponent<Outline>();
        if (old != null) UnityEngine.Object.Destroy(old);
        if (!on) return;
        var outline = bg.gameObject.AddComponent<Outline>();
        outline.effectColor = ColGold;
        outline.effectDistance = new Vector2(5f, -5f);   // 比原来粗，远看像一圈光
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
