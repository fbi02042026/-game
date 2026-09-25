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
    static readonly Color ColBlue = new Color(0.16f, 0.38f, 0.86f, 1f);
    static readonly Color ColRed = new Color(0.62f, 0.10f, 0.10f, 1f);
    static readonly Color ColText = new Color(0.94f, 0.92f, 0.88f, 1f);

    /// <summary>B5 开关：格子底框压暗总开关。false 时格子全部还原到预制体原色（base），不染不灰。</summary>
    public static bool EnableCellDim = true;
    /// <summary>B11: 已领格子「整体压暗」的倍率（base × k，只压暗不染色；与底框/天数同档，0.45 够暗又不糊）。</summary>
    const float DIM_CLAIMED_K = 0.45f;
    /// <summary>
    /// C1 开关：碎片格「奖励名按稀有度染色」总开关（主人要求：碎片的名字要跟着稀有度改变颜色）。
    /// false = 所有奖励名统一用预制体原色（base），稀有度色完全不介入，一键回退。
    /// </summary>
    public static bool EnableFragmentNameRarityColor = true;
    /// <summary>B4 开关：底部信息条压暗总开关。false 时整条还原到首次记录的原色（base），不压暗。</summary>
    public static bool EnableInfoDim = true;
    /// <summary>2026-09-25 开关：底部信息条「可领取」时的发光材质球 + effect 节点特效总开关。false 时材质设为 null、effect 关闭（完整还原）。</summary>
    public static bool EnableInfoGlow = true;

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

    // ---- 竖屏背景等比缩放适配（主人要求：搬进界面自身，不通用）----
    /// <summary>背景原始基准尺寸（来自主人预制体 Bg_Full 的 size：750×1125），等比缩放恒保此比例。</summary>
    const float DAILY_LOGIN_BG_BASE_W = 750f;
    const float DAILY_LOGIN_BG_BASE_H = 1125f;
    /// <summary>竖屏背景适配总开关：false 时还原 Bg_Full 到预制体原始 sizeDelta（一键回退）。</summary>
    public static bool EnableDailyLoginFit = true;
    bool _fitApplying;          // 防递归守卫
    bool _fitCaptured;          // 是否已记录 Bg_Full 基准（原始 sizeDelta + 中心偏移）
    Vector2 _fitBaseSize;       // 原始 sizeDelta（一键回退用）
    Vector2 _fitBasePos;        // 首次记录的 Bg_Full「中心偏移」（换成中心锚点后相对父级中心的偏移，恒定，不随分辨率变）

    GameObject _root;
    bool _boundFull;     // 是否走「完整预制体」路径（节点位置以美术预制体为准，Layout 不重排）
    Text _cycleName;
    Text _cycleState;
    Button _cycleBtn;
    Text _streakTag;
    readonly Button[] _streakBtns = new Button[4];
    readonly Text[] _streakLabels = new Text[4];
    Text _infoDays;
    Image _infoMercIcon;   // B1: 底部佣兵大图（400×200），代码永不动其 sprite，只在压暗时改 color
    // B2: InfoBar 下所有 "MercText*" 文本（MercText / MercText (1) / MercText (2)），代码只改 color 绝不改 text
    readonly List<Text> _infoMercTexts = new List<Text>();
    // B4: 整条压暗（CaptureBase 模式：首次 Refresh 记下每个 Graphic 的原色，之后从 base 重算，绝不写死 Color.white）
    readonly List<Graphic> _infoGraphics = new List<Graphic>();
    readonly List<Color> _infoBaseColors = new List<Color>();
    readonly List<Color> _infoMercTextBaseColors = new List<Color>();
    bool _infoDimCaptured;
    // 2026-09-25：底部信息条「可领取」时挂的发光材质球（取自主人预制体 InfoBar 自身 Image.material，首次捕获）。普通态 = null（用默认 UI 材质）。
    Material _infoGlowMat;
    // B10: 底部「第N天可领取」进度行。预制体原文写死成"第4天可领取"，一天没领也显示第4天（主人反馈）。
    // 只按**预制体原文**认一次并缓存 index——改写成"已领取"之后，按内容就再也认不出来了。
    int _infoProgressIdx = -1;
    bool _infoProgressResolved;

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
        // B5: 绑定时刻记录的预制体原色（底框 / 天数文字），压暗一律从 base 重算，绝不写死颜色覆盖美术底图
        public Color baseBgColor;
        public Color baseDayColor;
        // B11: 其余「已领要一起暗下去」的元素原色（奖励文字 / 图标 / X2 / 限定 / 已领斜字 / 碎片卡三张图）
        public Color baseRewardColor;
        public Color baseIconColor;
        public Color baseX2Color;
        public Color baseRareColor;
        public Color baseMarkColor;
        public Color baseFragClipColor;
        public Color baseFragPortraitColor;
        public Color baseFragFrameColor;
        // C1: 本格「奖励名原色」的覆盖值。hasRarityTint=true 时，压暗/还原一律以 rarityTint 为基准，
        //     不再用 baseRewardColor。每帧 Refresh 里重算（只有本格是碎片才置 true），
        //     **绝不回写 baseRewardColor** —— 那是预制体原始色，改了就会出现「今天染色、明天变不回原色」的漂移。
        public bool hasRarityTint;
        public Color rarityTint;
        // B11: 角标子树里的附属 Graphic（如 X2 角标内的 "X2" 文字）——主人反馈「字没暗」，子文字也要一起暗
        public readonly List<Graphic> dimExtras = new List<Graphic>();
        public readonly List<Color> dimExtraBases = new List<Color>();
        // B7: 碎片卡（含佣兵头像）挂在 IconHolder 下，只建一次；Refresh 只切显隐 + Apply
        public MercGrowUI.FragmentCard fragCard;
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
            // 递归深度查找：Cell_1~8 在主人预制体里包在 CellsPanel 下，直接子级查找会失败，必须递归
            var t = FindChildDeep(transform, "Cell_" + i);
            if (t == null)
            {
                Debug.LogError("[DailyLoginUI] 预制体缺格子 Cell_" + i + "，回退代码生成");
                return false;
            }
            var bg = t.GetComponent<Image>();
            if (bg == null) return false;

            var c = new Cell { go = t.gameObject, bg = bg, day = i };
            // B5: 记录预制体底框原色（压暗一律从 base 重算，绝不写死颜色去覆盖美术底图配色）
            c.baseBgColor = bg != null ? bg.color : Color.white;

            var tag = FindChildDeep(t, "DayTag");
            c.dayText = tag != null ? tag.GetComponentInChildren<Text>() : null;
            // B5: 记录预制体天数文字原色（同样从 base 重算，避免换掉美术色）
            c.baseDayColor = c.dayText != null ? c.dayText.color : Color.white;

            var icon = FindChildDeep(t, "IconHolder");
            if (icon != null)
            {
                c.icon = icon.GetComponent<Image>();
                c.iconFallback = icon.GetComponentInChildren<Text>();
                // B7: 碎片卡（含佣兵头像）只建一次挂到 IconHolder 下并缓存；Refresh 只切显隐 + Apply（严禁每次重建）
                var irt = icon.GetComponent<RectTransform>();
                float fragSize = irt != null ? irt.sizeDelta.x : 70f;   // IconHolder 预制体 70×70，取其宽
                // 创建时先给默认稀有档占位，真正的 rarity/头像在 Refresh 里按奖励 Apply；头像卡默认隐藏
                var card = MercGrowUI.CreateFragmentCard(icon, "FragmentCard", MercRosterDefs.MercRarity.Rare, "", fragSize);
                c.fragCard = card;
                if (card.Root != null) card.Root.SetActive(false);
            }

            var reward = FindChildDeep(t, "Reward");
            c.rewardText = reward != null ? reward.GetComponent<Text>() : null;

            var x2 = FindChildDeep(t, "X2");
            c.x2Badge = x2 != null ? x2.gameObject : null;
            var rare = FindChildDeep(t, "Rare");
            c.rareBadge = rare != null ? rare.gameObject : null;
            var mark = FindChildDeep(t, "Mark");
            c.markText = mark != null ? mark.GetComponent<Text>() : null;

            c.btn = t.GetComponent<Button>();
            if (c.btn == null) c.btn = t.gameObject.AddComponent<Button>();
            c.btn.transition = Selectable.Transition.None;
            // 用 cell.day 而不是绑死的 i：轮回后 cell.day 会在 Refresh 里换成累计天数（9、10…）
            c.btn.onClick.AddListener(() => OnClickStarter(c.day));
            // B11: 全部组件绑完后统一记原色（CaptureBase），之后压暗一律从 base 重算
            CaptureCellBase(c);
            bound.Add(c);
        }

        // 2026-09-23 主人决定：每日循环条 / 连击加成条不再在界面显示（连击改为双倍奖励）。
        // 所以这三条都按「可选」处理——缺了就跳过，BindBars/Refresh/Layout 内部都有判空。
        BindBars();

        // 绑定预制体上的关闭按钮（主人反馈：点了没反应——代码生成路径才建按钮，
        // 走预制体路径时那颗按钮没有监听）。铁律：以预制体那颗按钮为准，绝不新建；
        // 防重复绑定先 RemoveAllListeners，再挂 Hide。找不到则 no-op 不报错。
        var closeT = FindChildDeep(transform, "CloseButton");
        if (closeT != null)
        {
            var closeBtn = closeT.GetComponent<Button>();
            if (closeBtn == null) closeBtn = closeT.gameObject.AddComponent<Button>();
            closeBtn.transition = Selectable.Transition.None;     // 与格子按钮风格一致（见上 c.btn）
            closeBtn.onClick.RemoveAllListeners();
            closeBtn.onClick.AddListener(Hide);
        }

        // 2026-09-25 主人要求：第 8 天底框改普通框——只有「限定英雄天」才用特殊框（详见 NormalizeCellFrames）
        NormalizeCellFrames(bound);

        _starterCells.AddRange(bound);
        _root = gameObject;                               // Layout/Refresh/Hide 全按根节点找名字
        _boundFull = true;                                // 完整预制体路径：节点位置以美术预制体为准
        return true;
    }

    // 【主人口径 2026-09-25】默认所有天都用普通框；只有主人点名要金框（限定英雄天）的天才填进这里。
    // 碎片天（mercfrag）也是普通框。将来主人说「第 N 天改金色框」，把 N 加进这个数组即可，代码其它地方不用动。
    private static readonly int[] GOLD_FRAME_DAYS = { };

    /// <summary>
    /// 2026-09-25 主人要求：默认所有天都是普通框，包括佣兵碎片天（mercfrag）。
    /// 金框（限定英雄天）不靠代码自动判定，而是**等主人点名**——只认 GOLD_FRAME_DAYS 白名单（当前为空 → 8 天全普通框）。
    ///
    /// 普通框 sprite 不从 Resources 加载（怕加载错图）：直接取**某个确定该用普通框的格子**的底图
    /// （主人预制体里摆好的原图，天然与美术资源一致）。只写 sprite，
    /// 绝不碰 sizeDelta / anchoredPosition / 层级顺序。预制体缺图（兜底路径）则整段 no-op。
    /// </summary>
    static void NormalizeCellFrames(List<Cell> cells)
    {
        if (cells == null || cells.Count == 0) return;

        // 1) 取普通框样本：第一个「非金框」的格子（不写死 Cell_1）
        Sprite normal = null;
        for (int i = 0; i < cells.Count; i++)
        {
            if (IsHeroDay(i + 1)) continue;
            var sample = cells[i]?.bg;
            if (sample == null) continue;
            normal = sample.sprite;
            break;
        }
        if (normal == null) return;   // 没有任何普通框底图（兜底路径）→ 不动

        // 2) 非金框的天一律换成普通框（只赋 sprite）
        for (int i = 0; i < cells.Count; i++)
        {
            if (IsHeroDay(i + 1)) continue;            // 金框天保留预制体的特殊框
            var bg = cells[i]?.bg;
            if (bg == null || bg.sprite == normal) continue;
            bg.sprite = normal;
        }
    }

    /// <summary>
    /// 该天（1 基天数）是否用金框（限定英雄天）：只认显式白名单 GOLD_FRAME_DAYS。
    /// 默认全 false（普通框）；主人点名某天要金框时，把天数加进 GOLD_FRAME_DAYS 即可。
    /// </summary>
    static bool IsHeroDay(int day)
    {
        return System.Array.IndexOf(GOLD_FRAME_DAYS, day) >= 0;
    }

    /// <summary>绑定三条横带的子节点引用（名字同兜底路径，Layout() 直接复用）。</summary>
    void BindBars()
    {
        var cycle = FindChildDeep(transform, "CycleBar");
        if (cycle != null)
        {
            var nameT = FindChildDeep(cycle, "Name");
            _cycleName = nameT != null ? nameT.GetComponent<Text>() : null;
            var btnT = FindChildDeep(cycle, "ClaimBtn");
            if (btnT != null)
            {
                _cycleBtn = btnT.GetComponent<Button>();
                if (_cycleBtn == null) _cycleBtn = btnT.gameObject.AddComponent<Button>();
                _cycleBtn.onClick.AddListener(OnClickCycle);
                _cycleState = btnT.GetComponentInChildren<Text>();
            }
        }

        var streak = FindChildDeep(transform, "StreakBar");
        if (streak != null)
        {
            var tagT = FindChildDeep(streak, "Tag");
            _streakTag = tagT != null ? tagT.GetComponent<Text>() : null;
            for (int i = 0; i < DailyLoginDefs.Streak.Length && i < _streakBtns.Length; i++)
            {
                var s = DailyLoginDefs.Streak[i];
                var bt = FindChildDeep(streak, "Streak_" + s.days);
                if (bt == null) continue;                 // 改了 Streak 天数后重跑生成器即可
                _streakBtns[i] = bt.GetComponent<Button>();
                if (_streakBtns[i] == null) _streakBtns[i] = bt.gameObject.AddComponent<Button>();
                int streakDays = s.days;
                _streakBtns[i].onClick.AddListener(() => OnClickStreak(streakDays));
                _streakLabels[i] = bt.GetComponentInChildren<Text>();
            }
        }

        var info = FindChildDeep(transform, "InfoBar");
        if (info != null)
        {
            var daysT = FindChildDeep(info, "Days");
            _infoDays = daysT != null ? daysT.GetComponent<Text>() : null;
            // B2: 收集 InfoBar 下所有 "MercText*" 文本（主人摆好的三行文案），代码只改 color 绝不改 text
            _infoMercTexts.Clear();
            CollectMercTexts(info, _infoMercTexts);
            var iconT = FindChildDeep(info, "MercIcon");
            _infoMercIcon = iconT != null ? iconT.GetComponent<Image>() : null;

            // B4: 底部信息条 = 累计大奖领取入口（与 CloseButton 同风格：None 过渡，先 RemoveAllListeners 再挂监听；用预制体已有节点不新建）
            var infoBtn = info.GetComponent<Button>();
            if (infoBtn == null) infoBtn = info.gameObject.AddComponent<Button>();
            infoBtn.transition = Selectable.Transition.None;
            infoBtn.onClick.RemoveAllListeners();
            infoBtn.onClick.AddListener(OnClickAccum);
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
        // B11: 兜底路径同样记原色，保证两条路径压暗行为一致
        CaptureCellBase(c);
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
        // B2: 兜底路径也按 "MercText*" 命名，让 CollectMercTexts 能收集到（代码只改 color 绝不改 text）
        var mercTxt = CreateTxt(bar.transform, "MercText", "", 16, TextAnchor.MiddleLeft);
        SetRect(mercTxt.rectTransform, 0f, 0.5f, 396f, 0f, 420f, 80f);
        mercTxt.color = ColText;
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
        // 竖屏背景适配：背景只做整体等比缩放（cover），其余节点保持预制体原样。
        // 此 UI 是 DontDestroyOnLoad 单例 Canvas，UICanvasSetup 覆盖不到，必须显式调。
        // 仅更瘦屏生效，标准 9:16 直接 return 不动任何东西（内部已判）。
        ApplyFit();
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
        // 完整预制体路径：全部节点位置由美术预制体摆好，代码严禁反向"纠正"排版，直接跳过整段重排。
        if (_boundFull) return;
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

    /// <summary>
    /// 递归深度查找：从 parent 的每个直接子节点出发沿整棵子树 DFS，
    /// 返回第一个名字匹配的节点；找不到返回 null。
    /// 与 FindChild 只查直接子级不同，本方法能穿透 CellsPanel 这类中间容器，
    /// 因此 Cell_1~8 即便包在 CellsPanel 下也能被找到（主人预制体结构）。
    /// </summary>
    static Transform FindChildDeep(Transform parent, string name)
    {
        if (parent == null || string.IsNullOrEmpty(name)) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c != null && c.name == name) return c;
        }
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c == null) continue;
            var found = FindChildDeep(c, name);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// 在 InfoBar 下按名字（**忽略大小写**）查找 effect / glow 子节点，任意深度 DFS。找到返回该节点，找不到返回 null。
    /// 主人新加的节点叫小写 "effect"、且可能包在深层；这里不限于直接子级、也不大小写敏感，
    /// 因此既兼容旧的 "Effect"/"Glow"，也能命中小写的 "effect"。找不到就返回 null（调用方 no-op，绝不新建节点）。
    /// </summary>
    static Transform FindInfoEffect(Transform infoBar)
    {
        if (infoBar == null) return null;
        for (int i = 0; i < infoBar.childCount; i++)
        {
            var c = infoBar.GetChild(i);
            if (c == null) continue;
            if (c.name.Equals("effect", System.StringComparison.OrdinalIgnoreCase)
                || c.name.Equals("glow", System.StringComparison.OrdinalIgnoreCase))
                return c;
            var deep = FindInfoEffect(c);   // 任意深度递归
            if (deep != null) return deep;
        }
        return null;
    }

    /// <summary>
    /// B2: 递归收集 root 下所有名字以 "MercText" 开头的 Text（主人摆好的多行文案：MercText / MercText (1) / MercText (2)）。
    /// 收集后代码只改它们的 color，绝不改 text（文案以预制体为准）。
    /// </summary>
    static void CollectMercTexts(Transform root, List<Text> outList)
    {
        if (root == null) return;
        for (int i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            if (c == null) continue;
            if (c.name.StartsWith("MercText") && c.GetComponent<Text>() != null)
                outList.Add(c.GetComponent<Text>());
            CollectMercTexts(c, outList);
        }
    }

    /// <summary>
    /// 在 Starter 新手奖励数组里找「第一个佣兵日」的索引（0 基）。返回 -1 表示没有佣兵日。
    /// UI 的「限定」角标与底部佣兵预告都从数据里取，不再硬编码最后一格——
    /// 主人改表（如把佣兵从 D8 挪到 D4）后 UI 自动跟随，不会再现 D8 变碎片就头像空的问题。
    /// </summary>
    static int FindStarterMercSlot()
    {
        var list = DailyLoginDefs.Starter;
        for (int i = 0; i < list.Length; i++)
            if (list[i].grant == DailyLoginDefs.Grant.Merc) return i;
        return -1;
    }

    void Refresh()
    {
        int days = DailyLoginSystem.LoginDays;

        // ---- 8 天格 ----
        // 2026-09-23 轮回（主人要求）：第 1 轮 1~8 天，第 2 轮 9~16 天，第 3 轮 17~24 天……
        // 奖励仍按 Starter[i] 循环取；已领记录按累计天数存，所以不需要重置存档。
        int lastDay = DailyLoginDefs.Starter.Length;
        int round = lastDay > 0 ? (Mathf.Max(1, days) - 1) / lastDay : 0;
        // 佣兵日不再硬编码：B8 改为按每格奖励 r.grant == Grant.Merc 判定「限定」角标（FindStarterMercSlot 保留备用）
        for (int i = 0; i < _starterCells.Count && i < lastDay; i++)
        {
            var cell = _starterCells[i];
            // C1: 每帧先清染色标记，再由本格奖励重新判定（稀有度色每格独立，绝不跨天累积）
            cell.hasRarityTint = false;
            int day = round * lastDay + i + 1;                      // ★ 轮回后的真实累计天数
            var r = DailyLoginSystem.EffectiveStarterReward(day);   // 轮回后佣兵日自动换碎片，显示=实发
            cell.day = day;                        // 点击领取用的就是这个

            bool dbl = DailyLoginDefs.IsStarterDouble(day);
            bool claimed = DailyLoginSystem.IsStarterClaimed(day);
            bool reached = days >= day;
            bool canPress = reached && !claimed;

            // 2026-09-25 主人反馈「不能领取的天点一下就变亮」：真凶是 UiButtonPressFeedback.OnPointerUp。
            // 它只有 OnPointerDown 判了 interactable，OnPointerUp / OnPointerExit / OnDisable 都无条件 Restore()，
            // 而 Restore 写回的是 Awake 时抓的旧 base 色（亮色），会把下面压暗的颜色顶掉；
            // 不可领的格子又不会触发 onClick → 没有 Refresh 纠正，于是就一直亮着。
            // 所以：不可领的天直接把该组件摘掉（摘掉时 OnDisable→Restore 会写一次旧色，必须放在本格写色之前）。
            SyncPressFeedback(cell, canPress);

            cell.dayText.text = "第 " + day + " 天";   // 已领/未解锁状态用底色与文字颜色区分
            // B6: 奖励名去掉稀有度前缀（"稀有佣兵·" 等），并把 \n 换成 ·
            cell.rewardText.text = StripRarity(r.DisplayName);
            if (cell.x2Badge != null) cell.x2Badge.SetActive(dbl);
            // B8: 「限定」标识位置以预制体为准，代码只控 active；判定改为该格奖励是 Merc 才显示（佣兵日后自动换碎片则不显示）
            if (cell.rareBadge != null) cell.rareBadge.SetActive(r.grant == DailyLoginDefs.Grant.Merc && !claimed);

            // B7: 碎片格带佣兵头像（复用 MercGrowUI.FragmentCard），其余类型走原 SetIcon
            bool isFrag = r.grant == DailyLoginDefs.Grant.MercFragment
                       || r.grant == DailyLoginDefs.Grant.LegendaryFragment;
            // C1: 碎片格奖励名按稀有度染色（主人要求）。只给本格记一个「原色覆盖值」，
            //     不碰 baseRewardColor（预制体原色），所以关开关 / 非碎片格 / 换轮回后都能干净还原。
            if (isFrag && EnableFragmentNameRarityColor)
            {
                var rc = RarityPalette.Get(RarityOfGrant(r.grant, r.hireId));
                // 只换 RGB、保留预制体文字的 alpha —— 以主人预制体为准，代码只兜底
                cell.rarityTint = new Color(rc.r, rc.g, rc.b, cell.baseRewardColor.a);
                cell.hasRarityTint = true;
            }
            if (isFrag && cell.fragCard != null)
            {
                // 底图图标本身隐藏，只显示带头像的碎片卡（绝不让底图图盖住头像）
                if (cell.icon != null) { cell.icon.enabled = false; cell.icon.color = new Color(1f, 1f, 1f, 0f); }
                if (cell.iconFallback != null) cell.iconFallback.gameObject.SetActive(false);
                // 普通/稀有佣兵碎片按 hireId 查档位；传说碎片固定 Legendary；查不到回退 Rare
                // C1: 与奖励名染色共用同一份取稀有度逻辑（RarityOfGrant），别写两份
                MercRosterDefs.MercRarity fr = RarityOfGrant(r.grant, r.hireId);
                cell.fragCard.Root.SetActive(true);
                cell.fragCard.Apply(fr, r.hireId);
            }
            else
            {
                if (cell.fragCard != null && cell.fragCard.Root != null) cell.fragCard.Root.SetActive(false);
                if (cell.icon != null) cell.icon.enabled = true;
                var sp = LoadRewardIcon(r);
                SetIcon(cell, sp, r.name);
            }

            // ★ 已领斜字：只有 claimed 状态亮出来
            if (cell.markText != null) cell.markText.gameObject.SetActive(claimed);

            // B5: 格子底框只灰度、绝不染色（CaptureBase：以预制体 base 原色为基准做灰度暗化，绝不写死深蓝覆盖美术底图）
            Color bgBase = cell.baseBgColor;
            Color dayBase = cell.baseDayColor;
            if (claimed)
            {
                // 已领：灰度暗化（保持灰不偏色）；关开关则还原 base
                float t = EnableCellDim ? 0.65f : 0f;
                cell.bg.color = Color.Lerp(bgBase, new Color(0.35f, 0.35f, 0.35f, bgBase.a), t);
                cell.dayText.color = Color.Lerp(dayBase, new Color(0.35f, 0.35f, 0.35f, dayBase.a), t);
                // B11: 底框/天数之外的一切（奖励名、图标、X2、限定、已领斜字、碎片卡）一起压暗，
                //      修掉「领取过的只有底和天数暗了，字还亮着」（主人 2026-09-25 反馈）
                ApplyCellDim(cell, EnableCellDim ? DIM_CLAIMED_K : 1f);
            }
            else if (reached)
            {
                // 今天可领：原样（金圈由下方 ApplyGlow 处理）；关开关则强行还原 base
                if (EnableCellDim) { cell.bg.color = bgBase; cell.dayText.color = dayBase; }
                ApplyCellDim(cell, 1f);
            }
            else
            {
                // 未解锁：暗化（灰度，保持灰不偏色）；关开关则还原 base
                float t = EnableCellDim ? 0.45f : 0f;
                cell.bg.color = Color.Lerp(bgBase, new Color(0.30f, 0.30f, 0.30f, bgBase.a), t);
                cell.dayText.color = Color.Lerp(dayBase, new Color(0.30f, 0.30f, 0.30f, dayBase.a), t);
                // 未解锁格主人没提，保持原样（底框/天数照旧暗，其余还原 base），要一起暗就把这里改成 DIM_CLAIMED_K
                ApplyCellDim(cell, 1f);
            }

            // ★ 2026-09-23 主人要求：当天可领的那一格套光圈，点它就能领（其余按已领/未到区分）
            bool isToday = reached && !claimed && day == days;
            ApplyGlow(cell.bg, isToday);

            cell.btn.interactable = canPress;
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

        // ---- 底部信息条：累计大奖领取入口（B4）----
        // 累计大奖三态：已领 / 可领 / 未达天数；只改颜色、不改文字、不改位置（文案以预制体为准）
        bool accumGot = DailyLoginSystem.IsAccumClaimed();
        bool accumRdy = DailyLoginSystem.IsAccumReached() && !accumGot;
        // B3: 左侧天数改回主人格式（预制体原文 "1 / 8"）
        if (_infoDays != null)
            _infoDays.text = days + " / " + DailyLoginDefs.AccumTargetDays;
        // B10: 底部进度行跟着累计天数走（只改那一行，其余 MercText 保持预制体原文）
        ApplyInfoProgressText(days);
        // B4: 整条压暗（CaptureBase 模式）+ 特效挂点，绝不碰文字/位置
        ApplyInfoDim(accumGot, accumRdy);
    }

    /// <summary>
    /// B4: 首次调用时记录 InfoBar 下每个 Graphic 的「原色」（CaptureBase 模式）。
    /// 之后压暗都从 base 重算，绝不写死 Color.white——否则会把主人美术配色整个换掉。
    /// MercIcon 单独用 _infoMercIcon 字段管理（B1 永不动其 sprite）；MercText 系列走 _infoMercTexts。
    /// </summary>
    void CaptureInfoBase()
    {
        if (_infoDimCaptured) return;
        var info = FindChildDeep(transform, "InfoBar");
        if (info == null) return;
        var gs = info.GetComponentsInChildren<Graphic>(true);
        _infoGraphics.Clear();
        _infoBaseColors.Clear();
        _infoMercTextBaseColors.Clear();
        foreach (var g in gs)
        {
            // B1: 佣兵大图用字段单独管理；B2: MercText 系列单独走列表。两者都不进整条压暗集合
            if (g.gameObject.name == "MercIcon") continue;
            if (g.gameObject.name.StartsWith("MercText")) continue;
            _infoGraphics.Add(g);
            _infoBaseColors.Add(g.color);
        }
        // 单独纳入 _infoMercIcon（B1 永不动其 sprite，只随整条压暗改 color）
        if (_infoMercIcon != null) { _infoGraphics.Add(_infoMercIcon); _infoBaseColors.Add(_infoMercIcon.color); }
        // MercText 系列：只记录原色，压暗时只改 color 绝不改 text
        foreach (var t in _infoMercTexts) _infoMercTextBaseColors.Add(t.color);
        _infoDimCaptured = true;
    }

    /// <summary>B4: 一键还原到首次记录的原色（EnableInfoDim=false 时调用）。</summary>
    void RestoreInfoBase()
    {
        if (!_infoDimCaptured) return;
        for (int i = 0; i < _infoGraphics.Count; i++) _infoGraphics[i].color = _infoBaseColors[i];
        for (int i = 0; i < _infoMercTexts.Count; i++) _infoMercTexts[i].color = _infoMercTextBaseColors[i];
    }

    /// <summary>
    /// B4: 底部信息条压暗 + 特效挂点。只改颜色、不改文字、不改位置（文案以预制体为准）。
    ///   accumGot   已领：整条压暗 ×0.45 灰
    ///   accumRdy   可领：保持原色（原样）
    ///   未达天数        ：压暗 ×0.55 灰
    /// 特效挂点：在 InfoBar 下运行时找名为 Effect / Glow 的子节点，找到才 SetActive(accumRdy)，找不到 no-op（绝不新建）。
    /// </summary>
    void ApplyInfoDim(bool accumGot, bool accumRdy)
    {
        CaptureInfoBase();

        // 2026-09-25 主人要求：底部信息条「可领取」时——① 给 InfoBar 的 Image 挂上发光材质球；② 打开 effect 节点。
        // 跟压暗是两件事，故放在压暗开关之前；压暗关掉时这两样仍按 accumRdy 生效。
        var infoGlow = FindChildDeep(transform, "InfoBar");
        var infoImg = infoGlow != null ? infoGlow.GetComponent<Image>() : null;
        if (infoImg != null)
        {
            // 首次捕获：记下主人预制体里那颗发光材质球（只抓一次，之后都从缓存取，避免把写进去的值又当 base）
            if (_infoGlowMat == null && infoImg.material != null) _infoGlowMat = infoImg.material;
            // 可领取 + 开关开 → 发光材质球；否则还原成没有材质球（默认 UI 材质）
            infoImg.material = (EnableInfoGlow && accumRdy) ? _infoGlowMat : null;
        }
        // effect / glow 子节点：任意深度 + 忽略大小写（主人加的叫小写 effect），找不到 no-op、绝不新建
        var fxGo = FindInfoEffect(infoGlow);
        if (fxGo != null) fxGo.gameObject.SetActive(EnableInfoGlow && accumRdy);   // Transform 没有 SetActive，要走 .gameObject

        if (!EnableInfoDim) { RestoreInfoBase(); return; }

        float k = accumGot ? 0.45f : (accumRdy ? 1f : 0.55f);
        for (int i = 0; i < _infoGraphics.Count; i++)
        {
            var baseC = _infoBaseColors[i];
            // 整条压暗：base × k（保持 alpha，避免整条变透明）
            _infoGraphics[i].color = new Color(baseC.r * k, baseC.g * k, baseC.b * k, baseC.a);
        }
        for (int i = 0; i < _infoMercTexts.Count; i++)
        {
            var baseC = _infoMercTextBaseColors[i];
            _infoMercTexts[i].color = new Color(baseC.r * k, baseC.g * k, baseC.b * k, baseC.a);
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

    /// <summary>
    /// 2026-09-25：格子按压反馈同步（主人要求：不可领的天点了不能有任何视觉反馈）。
    ///   on = true  → 可领取：确保挂上 UiButtonPressFeedback，按压缩放/变暗反馈照常。
    ///   on = false → 未达成 / 已领取：摘掉该组件，点击不缩放、不变亮、不变色。
    ///
    /// 为什么是摘组件而不是设 suppress：UiButtonPressFeedback.OnPointerUp 没判 interactable 也没判 suppress，
    /// 无条件 Restore() 会把 Awake 时抓的旧「亮色」base 写回底图，顶掉本文件压暗后的颜色（详见 Refresh 里的注释）。
    ///
    /// ⚠ 调用时机：Destroy 会触发 OnDisable → Restore（写一次旧色），
    ///   所以本方法必须在**本格写颜色之前**调用，写完色才不会被旧色冲掉。
    /// </summary>
    static void SyncPressFeedback(Cell c, bool on)
    {
        if (c?.go == null) return;
        var pf = c.go.GetComponent<UiButtonPressFeedback>();
        if (on)
        {
            if (pf == null) c.go.AddComponent<UiButtonPressFeedback>();   // Awake 抓当前（正常）色为 base
        }
        else if (pf != null)
        {
            UnityEngine.Object.Destroy(pf);
        }
    }

    /// <summary>
    /// B11: 记录格子内所有可压暗元素的**当前色**作为 base（CaptureBase 模式，与 baseBgColor/baseDayColor 同款）。
    /// 绑定 / 创建完成后调用一次；之后压暗一律从 base 重算，绝不写死颜色去覆盖美术配色。
    /// </summary>
    static void CaptureCellBase(Cell c)
    {
        if (c == null) return;
        c.baseBgColor = c.bg != null ? c.bg.color : Color.white;
        c.baseDayColor = c.dayText != null ? c.dayText.color : Color.white;
        c.baseRewardColor = c.rewardText != null ? c.rewardText.color : Color.white;
        c.baseIconColor = c.icon != null ? c.icon.color : Color.white;
        c.baseMarkColor = c.markText != null ? c.markText.color : Color.white;

        var x2Img = c.x2Badge != null ? c.x2Badge.GetComponent<Image>() : null;
        c.baseX2Color = x2Img != null ? x2Img.color : Color.white;
        var rareImg = c.rareBadge != null ? c.rareBadge.GetComponent<Image>() : null;
        c.baseRareColor = rareImg != null ? rareImg.color : Color.white;

        // 角标里的附属文字（如 X2 角标内的 "X2"）：主人反馈「字没暗」，所以连子级 Graphic 一起记
        c.dimExtras.Clear();
        c.dimExtraBases.Clear();
        CollectDimExtras(c.x2Badge, c.dimExtras, c.dimExtraBases);
        CollectDimExtras(c.rareBadge, c.dimExtras, c.dimExtraBases);

        var fc = c.fragCard;
        c.baseFragClipColor = fc != null && fc.Clip != null ? fc.Clip.color : Color.white;
        c.baseFragPortraitColor = fc != null && fc.Portrait != null ? fc.Portrait.color : Color.white;
        c.baseFragFrameColor = fc != null && fc.Frame != null ? fc.Frame.color : Color.white;
    }

    /// <summary>B11: 收集角标子树里的附属 Graphic（角标自身那张 Image 走 baseX2Color / baseRareColor，不重复收）。</summary>
    static void CollectDimExtras(GameObject root, List<Graphic> gs, List<Color> bases)
    {
        if (root == null) return;
        var self = root.GetComponent<Graphic>();
        var all = root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var g = all[i];
            if (g == null || g == self) continue;
            gs.Add(g);
            bases.Add(g.color);
        }
    }

    /// <summary>
    /// B11: 格子内「底框 / 天数之外」的元素统一压暗或还原。k = 1 还原到 base，k &lt; 1 为 base × k（保持 alpha，避免变透明）。
    /// 只改 color：**绝不改 sprite、绝不改 active、绝不改位置**（碎片卡显示什么图由 Refresh 的 Apply 决定）。
    /// </summary>
    static void ApplyCellDim(Cell c, float k)
    {
        if (c == null) return;
        // C1: 碎片格用稀有度色当「原色」，其余格用预制体原色；k=1 即还原到该原色
        if (c.rewardText != null) c.rewardText.color = MulColor(RewardBase(c), k);
        if (c.markText != null) c.markText.color = MulColor(c.baseMarkColor, k);

        // 图标：没 sprite 时 SetIcon 已把它调成透明（避免露白块），这里必须保持透明，绝不能把白块还原出来
        if (c.icon != null && c.icon.enabled)
            c.icon.color = c.icon.sprite != null
                ? MulColor(c.baseIconColor, k)
                : new Color(c.baseIconColor.r, c.baseIconColor.g, c.baseIconColor.b, 0f);

        var x2Img = c.x2Badge != null ? c.x2Badge.GetComponent<Image>() : null;
        if (x2Img != null) x2Img.color = MulColor(c.baseX2Color, k);
        var rareImg = c.rareBadge != null ? c.rareBadge.GetComponent<Image>() : null;
        if (rareImg != null) rareImg.color = MulColor(c.baseRareColor, k);
        for (int i = 0; i < c.dimExtras.Count; i++)
            c.dimExtras[i].color = MulColor(c.dimExtraBases[i], k);

        // 碎片卡：只压暗 Clip / Portrait / Frame 三张 Image 的 color，绝不换 sprite
        var fc = c.fragCard;
        if (fc != null)
        {
            if (fc.Clip != null) fc.Clip.color = MulColor(c.baseFragClipColor, k);
            if (fc.Portrait != null) fc.Portrait.color = MulColor(c.baseFragPortraitColor, k);
            if (fc.Frame != null) fc.Frame.color = MulColor(c.baseFragFrameColor, k);
        }
    }

    /// <summary>B11: base × k，保持 alpha（压暗时绝不让元素变透明）。</summary>
    static Color MulColor(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

    /// <summary>
    /// C1: 本格奖励名的「原色」——碎片格是稀有度色（rarityTint），其余格是预制体原色（baseRewardColor）。
    /// 只读取值，绝不回写 baseRewardColor：保证任何一帧都从 base / 稀有度色重算，不会累积漂移。
    /// </summary>
    static Color RewardBase(Cell c) => c != null && c.hasRarityTint ? c.rarityTint : c.baseRewardColor;

    /// <summary>
    /// B10: 认出底部信息条里的「进度文案」那一行（预制体原文 "第4天可领取"）。
    /// 铁律：只认「含『可领取』且不含『限定角色』」的那一行——"限定角色：战士" 是主人摆好的文案，绝不碰。
    /// 只认一次并缓存 index：改写成"已领取"之后，按内容就再也认不出来了。
    /// </summary>
    void ResolveInfoProgressText()
    {
        if (_infoProgressResolved) return;
        _infoProgressResolved = true;
        for (int i = 0; i < _infoMercTexts.Count; i++)
        {
            var t = _infoMercTexts[i];
            if (t == null) continue;
            string s2 = t.text ?? "";
            if (s2.Contains("限定角色")) continue;    // 主人特意摆的角色说明行，跳过
            if (!s2.Contains("可领取")) continue;     // 只认进度行
            _infoProgressIdx = i;
            return;
        }
    }

    /// <summary>
    /// B10: 底部「第N天可领取」跟着累计天数走（主人反馈：一天没领，最下面却写着"第4天"）。
    /// 只改 ResolveInfoProgressText 认出来的那一行，其余 MercText（含「限定角色」行）一律保持预制体原文。
    ///   未达 AccumTargetDays → "还差 N 天可领取" / 已达未领 → "可领取" / 已领 → "已领取"
    /// </summary>
    void ApplyInfoProgressText(int days)
    {
        ResolveInfoProgressText();
        if (_infoProgressIdx < 0 || _infoProgressIdx >= _infoMercTexts.Count) return;
        var t = _infoMercTexts[_infoProgressIdx];
        if (t == null) return;

        string txt;
        if (DailyLoginSystem.IsAccumClaimed()) txt = "已领取";
        else if (days >= DailyLoginDefs.AccumTargetDays) txt = "可领取";
        else txt = "还差 " + (DailyLoginDefs.AccumTargetDays - days) + " 天可领取";

        if (t.text != txt) t.text = txt;
    }

    /// <summary>
    /// B12: 把一份奖励（含第二份）塞进通用「获得奖励」弹窗。主人反馈：领完没有任何弹窗。
    /// 图标复用已有的 LoadRewardIcon；文案去稀有度前缀（详见 MakeRewardEntry：碎片类已改走主人碎片预制体）。
    /// factor = X2 双倍日的实发倍率，用来把文案里的数量同步翻倍（否则弹窗写 ×500、实际到账 ×1000）。
    /// </summary>
    static void ShowRewardPopup(DailyLoginDefs.Reward r, int factor)
    {
        var items = new List<RewardPopupUI.Entry>
        {
            MakeRewardEntry(r, factor)
        };
        if (r.HasSecond)
        {
            var r2 = new DailyLoginDefs.Reward
            {
                name = r.name2, grant = r.grant2, type = r.type2, amount = r.amount2, hireId = r.hireId2
            };
            items.Add(MakeRewardEntry(r2, factor));
        }
        RewardPopupUI.Show(items);
    }

    /// <summary>
    /// C2: 构造一条弹窗条目。碎片（MercFragment / LegendaryFragment）走主人的碎片预制体
    /// （RewardPopupUI.Entry.Fragment，渲染成 yongbingsuipian 卡片而不是一张底版图），
    /// 其余奖励保持老构造（图标 + 文案）。ScaleAmountInName 的 X2 翻倍逻辑照旧。
    /// </summary>
    static RewardPopupUI.Entry MakeRewardEntry(DailyLoginDefs.Reward r, int factor)
    {
        string label = ScaleAmountInName(StripRarity(r.name), factor);
        bool isFrag = r.grant == DailyLoginDefs.Grant.MercFragment
                   || r.grant == DailyLoginDefs.Grant.LegendaryFragment;
        if (isFrag)
            return RewardPopupUI.Entry.Fragment(RarityOfGrant(r.grant, r.hireId), r.hireId, label);
        return new RewardPopupUI.Entry(LoadRewardIcon(r), label);
    }

    /// <summary>
    /// B12: 把奖励名里「×」后的数字按 factor 翻倍。X2 双倍日实发翻倍（DailyLoginDefs.Doubled）只改 amount 不改 name，
    /// 弹窗文案必须自己同步，否则会出现「弹窗写 ×500、实际到账 ×1000」。名字里没有 ×数字 时原样返回。
    /// </summary>
    static string ScaleAmountInName(string name, int factor)
    {
        if (factor <= 1 || string.IsNullOrEmpty(name)) return name;
        int k = name.IndexOf('×');
        if (k < 0) return name;
        int start = k + 1, end = start;
        while (end < name.Length && char.IsDigit(name[end])) end++;
        if (end == start) return name;
        int v;
        if (!int.TryParse(name.Substring(start, end - start), out v)) return name;
        return name.Substring(0, start) + (v * factor) + name.Substring(end);
    }

    static void SetIcon(Cell cell, Sprite sp, string rewardName)
    {
        if (cell?.icon == null) return;
        var img = cell.icon.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = sp;
            img.preserveAspect = true;
            // 白框兜底：sprite 为空时把 Image 调透明，避免露出纯白块（找不到图就只显首字兜底）。
            img.color = sp != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        }
        if (cell.iconFallback != null)
        {
            cell.iconFallback.gameObject.SetActive(sp == null);
            if (sp == null && !string.IsNullOrEmpty(rewardName))
                cell.iconFallback.text = rewardName.Substring(0, 1);
        }
    }

    /// <summary>
    /// B6: 奖励名去稀有度前缀。主人反馈：奖励名不该带「稀有佣兵·」「传说·」这类前缀。
    /// 同时把多行奖励里的 \n 换成 ·，便于单行紧凑显示（以预制体文案为准，这里只清前缀）。
    /// </summary>
    static string StripRarity(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        // 前缀列表（覆盖 普通/稀有/史诗/传说 × 佣兵/通用 的常见写法）
        string[] prefixes = {
            "稀有佣兵·", "传说佣兵·", "史诗佣兵·", "普通佣兵·",
            "稀有·", "传说·", "史诗·", "普通·"
        };
        foreach (var p in prefixes)
            if (name.StartsWith(p))
                return name.Substring(p.Length).Replace("\n", "·");
        return name.Replace("\n", "·");
    }

    /// <summary>
    /// B7: 按 hireId 查佣兵档位；查不到（如配置缺失）回退 Rare，避免碎片卡用错底色或空引用。
    /// </summary>
    static MercRosterDefs.MercRarity LookupRarity(string hireId)
    {
        if (!string.IsNullOrEmpty(hireId) && MercRosterDefs.TryGetByHireId(hireId, out var def))
            return def.Rarity;
        return MercRosterDefs.MercRarity.Rare;
    }

    /// <summary>
    /// C1/C2 共用：按 grant 类型取稀有度。
    /// MercFragment → 用 hireId 查档位（查不到回退 Rare，见 LookupRarity）；LegendaryFragment → 固定 Legendary。
    /// 抽成一份是有意的：格子染色（C1）与弹窗条目（C2）必须同源，否则改一处漏一处、两边颜色对不上。
    /// </summary>
    static MercRosterDefs.MercRarity RarityOfGrant(DailyLoginDefs.Grant g, string hireId)
    {
        if (g == DailyLoginDefs.Grant.LegendaryFragment) return MercRosterDefs.MercRarity.Legendary;
        return LookupRarity(hireId);
    }

    /// <summary>奖励图标缓存：按奖励指纹复用，避免每次 Refresh 重复加载（底层 CommonIconSprites / MercGrowSprites 各自也有缓存，这里是界面层兜底）。</summary>
    static readonly Dictionary<string, Sprite> _rewardIconCache = new Dictionary<string, Sprite>();

    /// <summary>按奖励类型加载图标（佣兵头像 / 资源 / 碎片底版）。结果按奖励指纹缓存。</summary>
    static Sprite LoadRewardIcon(DailyLoginDefs.Reward r)
    {
        if (r.grant == DailyLoginDefs.Grant.None) return null;

        string key = RewardIconKey(r);
        if (key != null)
        {
            Sprite hit;
            if (_rewardIconCache.TryGetValue(key, out hit)) return hit;
        }

        Sprite sp;
        switch (r.grant)
        {
            case DailyLoginDefs.Grant.Merc:
                sp = MercPortraitSprites.GetHead(r.hireId);
                break;
            case DailyLoginDefs.Grant.Resource:
                sp = LoadResourceIcon(r.type);
                break;
            case DailyLoginDefs.Grant.MercFragment:
                // 塔克·重盾是稀有档，碎片底版取稀有；不叠佣兵头像（不改预制体 / 不新增节点，SetIcon 已支持范围足够）。
                sp = MercGrowSprites.LoadFragmentBase(MercRosterDefs.MercRarity.Rare);
                break;
            case DailyLoginDefs.Grant.LegendaryFragment:
                sp = MercGrowSprites.LoadFragmentBase(MercRosterDefs.MercRarity.Legendary);
                break;
            default:
                sp = null;
                break;
        }

        if (key != null) _rewardIconCache[key] = sp;
        return sp;
    }

    /// <summary>资源类奖励图标统一走 Common 目录（金币/钻石/天赋石/强化石/分解材料）；体力沿用原路径。</summary>
    static Sprite LoadResourceIcon(ResourceWallet.ResourceType type)
    {
        switch (type)
        {
            case ResourceWallet.ResourceType.Stamina:
                return Resources.Load<Sprite>("UI/Icons/Stamina");
            case ResourceWallet.ResourceType.Gold:
                return CommonIconSprites.Load("icon_gold");
            case ResourceWallet.ResourceType.Diamond:
                return CommonIconSprites.Load("icon_diamond_blue");
            case ResourceWallet.ResourceType.TalentPoint:
                return CommonIconSprites.Load("icon_talent_stone");
            case ResourceWallet.ResourceType.EnchantStone:
                return CommonIconSprites.Load("icon_enchant_stone");
            case ResourceWallet.ResourceType.DecomposeMat:
                return CommonIconSprites.Load("icon_decompose_mat");
            default:
                return null;
        }
    }

    /// <summary>构造奖励图标缓存 key（同奖励指纹相同即复用）；无法归类返回 null 不缓存。</summary>
    static string RewardIconKey(DailyLoginDefs.Reward r)
    {
        switch (r.grant)
        {
            case DailyLoginDefs.Grant.Merc:
                return "Merc_" + r.hireId;
            case DailyLoginDefs.Grant.MercFragment:
                // B7: 碎片卡现已带头像，不同佣兵不能共用缓存，key 必须含 hireId 与档位
                return "Frag_" + r.hireId + "_" + LookupRarity(r.hireId).ToString();
            case DailyLoginDefs.Grant.LegendaryFragment:
                return "Frag_" + r.hireId + "_" + MercRosterDefs.MercRarity.Legendary.ToString();
            case DailyLoginDefs.Grant.Resource:
                return "Res_" + r.type.ToString();
            default:
                return null;
        }
    }

    // ============================================================
    // 点击
    // ============================================================

    void OnClickStarter(int day)
    {
        string msg;
        bool ok = DailyLoginSystem.TryClaimStarter(day, out msg);
        GlobalToastUI.Show(msg);
        if (ok)
        {
            Debug.Log($"[DailyLogin] {msg}");
            // B12: 领完弹「获得奖励」弹窗（主人反馈：领了没弹窗，不知道拿到啥）。
            // 必须按**实发**构造：TryClaimStarter 内部对 X2 双倍日做了 Doubled，这里同样复算一遍
            // （不改 DailyLoginSystem 接口，UI 侧自算），保证弹窗数量 = 实际到账数量。
            var r = DailyLoginSystem.EffectiveStarterReward(day);
            bool dbl = DailyLoginDefs.IsStarterDouble(day);
            ShowRewardPopup(r, dbl ? 2 : 1);
        }
        Refresh();
    }

    void OnClickCycle()
    {
        string msg;
        // 领取**前**先取序号：TryClaimCycle 成功后会把 CycleIndex 往前推一格，之后再取就取到下一条了
        int idx = DailyLoginSystem.CycleIndex;
        bool ok = DailyLoginSystem.TryClaimCycle(out msg);
        GlobalToastUI.Show(msg);
        if (ok)
        {
            Debug.Log($"[DailyLogin] {msg}");
            ShowRewardPopup(DailyLoginDefs.Cycle[idx], 1);
        }
        Refresh();
    }

    void OnClickStreak(int days)
    {
        string msg;
        var def = DailyLoginDefs.StreakFor(days);
        bool ok = DailyLoginSystem.TryClaimStreak(days, out msg);
        GlobalToastUI.Show(msg);
        if (ok)
        {
            Debug.Log($"[DailyLogin] {msg}");
            if (def != null) ShowRewardPopup(def.Value.reward, 1);
        }
        Refresh();
    }

    /// <summary>
    /// B4: 底部信息条「累计大奖」领取入口（与 OnClickStreak 写法一致）。
    /// 走预制体已有 InfoBar 节点的 Button，不新建；领取结果弹 Toast 后刷新整页。
    /// </summary>
    void OnClickAccum()
    {
        string msg;
        bool ok = DailyLoginSystem.TryClaimAccum(out msg);
        GlobalToastUI.Show(msg);
        if (ok)
        {
            Debug.Log($"[DailyLogin] {msg}");
            // B12: 累计大奖同样弹「获得奖励」弹窗（主人反馈：领了没弹窗）
            ShowRewardPopup(DailyLoginDefs.AccumReward, 1);
        }
        Refresh();
    }

    // ============================================================
    // 竖屏背景等比缩放适配（主人要求：搬进界面自身，不通用）
    // ============================================================

    /// <summary>是否更瘦屏：逻辑 UI 高 > 设计高 + 1 时为真（9:16 标准屏逻辑高恒 1280，必须保持原样）。</summary>
    static bool IsThinnerScreen()
    {
        float uiLogicalH = DesignAspectLetterbox.ResolveUiMatch() < 0.5f
            ? GameConfig.DESIGN_WIDTH / Mathf.Max(1e-3f, Screen.width / (float)Screen.height)
            : GameConfig.DESIGN_HEIGHT;
        return uiLogicalH > GameConfig.DESIGN_HEIGHT + 1f;
    }

    /// <summary>
    /// 每日登录弹窗竖屏适配（主人铁律：只动背景 Bg_Full，其余节点一律保持预制体原样）。
    ///   - **Bg_Full 走中心缩放**（2026-09-25 主人要求：别再以左下角缩放）：
    ///       锚点 / pivot 一律设 (0.5,0.5)，anchoredPosition 是「视觉中心相对父级中心的偏移」，恒定不动；
    ///       放大只改 sizeDelta（等比），中心不动 → 整张底图以中心为基准整体放大。
    ///   - 只在「更瘦屏」(IsThinnerScreen) 放大，且**只放大不缩小**：标准 9:16 保持预制体原 sizeDelta 原样；
    ///       倍数按「预制体实际渲染尺寸 = sizeDelta × localScale」算 cover，
    ///       避免与美术自带的 1.16 缩放叠加导致底图放过头（旧代码就是这么放飞的）。
    ///   - **Title 不再做任何分辨率补偿**（2026-09-25 主人反馈：就它随分辨率上下跑，还挡住下面窗体）。
    ///       它跟 CellsPanel / InfoBar 一样只按预制体自己的锚点站位，本方法一行都不碰它；
    ///       也不再给它引入任何新的分辨率相关偏移。
    ///   - 幂等：首次记录 Bg_Full 原始 sizeDelta 与中心偏移，重复调用都从基准重算；
    ///       EnableDailyLoginFit=false / 标准屏 → 倍率恒 1（回到预制体原样）。
    /// 铁律：运行时改 rect，绝不碰 .prefab。找不到节点、或父级还没量出尺寸则 no-op 不报错。
    /// </summary>
    void ApplyFit()
    {
        if (_fitApplying) return;
        _fitApplying = true;
        try
        {
            var bg = FindChildDeep(transform, "Bg_Full") as RectTransform;
            if (bg == null) return;
            var parent = bg.parent as RectTransform;
            if (parent == null) return;
            if (parent.rect.width <= 0f || parent.rect.height <= 0f) return;   // 尺寸还没量好，下次 Open 再算

            // ① 首次记录基准：原始 sizeDelta + 视觉中心「相对父级中心的偏移」（之后恒定，不随分辨率变）
            if (!_fitCaptured)
            {
                _fitBaseSize = bg.sizeDelta;
                if (_fitBaseSize.x <= 0f || _fitBaseSize.y <= 0f)
                    _fitBaseSize = new Vector2(DAILY_LOGIN_BG_BASE_W, DAILY_LOGIN_BG_BASE_H);

                // 当前视觉中心（父级本地坐标）：锚点参考点 + anchoredPosition，再补 pivot → 中心 的差
                Vector2 aMin = new Vector2(
                    Mathf.Lerp(parent.rect.xMin, parent.rect.xMax, bg.anchorMin.x),
                    Mathf.Lerp(parent.rect.yMin, parent.rect.yMax, bg.anchorMin.y));
                Vector2 aMax = new Vector2(
                    Mathf.Lerp(parent.rect.xMin, parent.rect.xMax, bg.anchorMax.x),
                    Mathf.Lerp(parent.rect.yMin, parent.rect.yMax, bg.anchorMax.y));
                Vector2 pivotPos = new Vector2(Mathf.Lerp(aMin.x, aMax.x, bg.pivot.x),
                                              Mathf.Lerp(aMin.y, aMax.y, bg.pivot.y)) + bg.anchoredPosition;
                Vector2 center = pivotPos + (new Vector2(0.5f, 0.5f) - bg.pivot) * bg.rect.size;
                _fitBasePos = center - (parent.rect.min + parent.rect.max) * 0.5f;
                _fitCaptured = true;
            }

            // ② 中心锚点：缩放时中心不动（不再从左下角长出来）
            bg.anchorMin = new Vector2(0.5f, 0.5f);
            bg.anchorMax = new Vector2(0.5f, 0.5f);
            bg.pivot = new Vector2(0.5f, 0.5f);
            bg.anchoredPosition = _fitBasePos;

            // ③ 整体等比放大到盖住整屏（只放大、不缩小）
            float k = 1f;
            if (EnableDailyLoginFit && IsThinnerScreen())
            {
                // 以「实际渲染尺寸」为基准（把预制体自带的 localScale 算进去），否则会和美术缩放叠加放过头
                float renderedW = Mathf.Max(1f, _fitBaseSize.x * bg.localScale.x);
                float renderedH = Mathf.Max(1f, _fitBaseSize.y * bg.localScale.y);
                float screenW = GameConfig.DESIGN_WIDTH;
                float screenH = ResolveLogicalScreenHeight();
                k = Mathf.Max(1f, Mathf.Max(screenW / renderedW, screenH / renderedH));
            }
            bg.sizeDelta = new Vector2(_fitBaseSize.x * k, _fitBaseSize.y * k);
        }
        finally
        {
            _fitApplying = false;
        }
    }

    /// <summary>屏幕逻辑尺寸（与项目一致口径）：宽恒为 DESIGN_WIDTH，高按 IsThinnerScreen 同算法解析。</summary>
    static float ResolveLogicalScreenHeight()
    {
        float match = DesignAspectLetterbox.ResolveUiMatch();
        if (match < 0.5f)
            return GameConfig.DESIGN_WIDTH / Mathf.Max(1e-3f, Screen.width / (float)Screen.height);
        return GameConfig.DESIGN_HEIGHT;
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
