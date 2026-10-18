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
public partial class DailyLoginUI : MonoBehaviour
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
    bool _fitCaptured;          // 是否已记录 Bg_Full 基准（原始 sizeDelta）
    Vector2 _fitBaseSize;       // 原始 sizeDelta（等比缩放基准 / 一键回退用）

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
        public GameObject selectedNode;   // ★ 当天可领的天（reached&&!claimed）才显示的「选中」图片节点
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
}
