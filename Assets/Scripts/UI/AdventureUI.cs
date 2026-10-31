using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 冒险界面（MainNavTab.Adventure）。
///
/// 布局（720×1280 设计分辨率，竖屏）：
///   ─ TopBar（ResourceBar 复用，高 120）
///   ─ LeftSidebar（副本类型列表，宽 170）
///   ─ ContentArea（右侧章节地图+地图顶部标题栏，剩余宽度）
///   ─ DetailPanel（选中关卡详情，含敌人、掉落、体力、难度、开始/扫荡）
///   ─ BottomNav（底部五入口复用，高 150）
///
/// 资源条和底部五入口均通过 TownSharedChrome 复用，本页不自建。
/// 资源图片（地图背景、副本图标、关卡节点等）留白格占位，你去替换 Sprite。
/// </summary>
public partial class AdventureUI : MonoBehaviour, ITownPage, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public static AdventureUI Instance { get; private set; }
    public MainNavTab Tab => MainNavTab.Adventure;

    /// <summary>点「开始冒险」后开战用的章节；0 表示用存档最新解锁章</summary>
    public static int PendingBattleChapter;
    public static int PendingBattleDifficulty;
    public static bool PendingGoldDungeon;
    /// <summary>
    /// 徽记副本进本标记（2026-10-23 主人拍板「活动副本一拆二」）。
    /// 由 TryEnterBattle 按活动列表选中态重设（徽记横条选中 = true）；BattleManager.StartNewRunInternal 读完即清（照 PendingGoldDungeon 口径）。
    /// </summary>
    public static bool PendingBadgeDungeon;
    /// <summary>
    /// 卷轴副本进本标记（2026-10-24 主人拍板「第三章通关开启 · 每周五 · 15体力 · 2次」）。
    /// 由 TryEnterBattle 按活动列表选中态重设（卷轴横条选中 = true）；BattleManager.StartNewRunInternal 读完即清（照徽记本口径）。
    /// </summary>
    public static bool PendingScrollDungeon;
    /// <summary>
    /// 礼物副本进本标记（2026-10-26 主人拍板「掉落礼物的活动副本周六开启」）。
    /// 由 TryEnterBattle 按活动列表选中态重设（礼物横条选中 = true）；BattleManager.StartNewRunInternal 读完即清（照卷轴本口径）。
    /// </summary>
    public static bool PendingGiftDungeon;

    /// <summary>
    /// 羁绊副本进本标记（2026-10-27 主人拍板「照礼物副本框架」）：好感满佣兵专属关。
    /// 由好感界面「羁绊地图」按钮（MercFavorUI.OnBondClicked）置 true；BattleManager.StartNewRunInternal 捕获进 BondMercHireId 后清零（照礼物本口径）。
    /// </summary>
    public static bool PendingBondDungeon;

    /// <summary>
    /// 羁绊副本对应的佣兵 hireId（与 PendingBondDungeon 同时由好感界面按钮设置；读后即清，照 SideIslandId 口径）。
    /// </summary>
    public static string PendingBondHireId;

    /// <summary>
    /// 支线岛进本标记（2026-10-23 合并方案）：值 = 岛 id（1..8，0=非岛局）。
    /// 由支线岛点击（BranchIslands）设置；BattleManager.StartNewRunInternal 捕获进 SideIslandId 后清零（照金币本口径）。
    /// </summary>
    public static int PendingSideIsland;

    /// <summary>
    /// 终章追查关进本标记（2026-10-23 主人拍板「最后一关后剧情二选一」）：值 = 追查关号（1..2，0=非追查局）。
    /// 由冒险页追查入口设置；BattleManager.StartNewRunInternal 捕获进 FinaleStageId 后清零（照支线岛口径）。
    /// </summary>
    public static int PendingFinaleStage;

    /// <summary>
    /// 徽记副本公开入口：冒险页切到「活动副本」并选中徽记横条后进本
    /// （2026-10-23 拍板「活动副本一拆二」，开放日/每日次数检查在 <see cref="TryEnterBattle"/>）。
    /// </summary>
    public void EnterBadgeDungeon()
    {
        _selectedMode = 4;
        _selectedActivityKind = ActivityDungeonKind.Badge;
        TryEnterBattle();
    }
    /// <summary>
    /// 无限裂隙进本标记（2026-10-17）。TryEnterBattle 每次按所选模式**重设**（非裂隙恒 false，防上一次残留污染）；
    /// BattleManager.StartNewRunInternal 读完即清。体力确认弹窗也读它区分消耗 5/10（2026-10-22 主人拍板）。
    /// </summary>
    public static bool PendingEndlessRift;

    /// <summary>
    /// 竞技场挑战进本标记（2026-10-30 主人拍板「竞技场真战斗」）：值 = 榜单行号（0 基，= 机器人列表下标，-1=非竞技场局）。
    /// 由竞技场弹窗「对战」选职业确认（ArenaPopupUI.OnJobPicked，照「先 ResetPendingFlags 全清再立标记」口径）设置；
    /// BattleManager.StartNewRunInternal 捕获进 ArenaChallengeRowIdx 后清回 -1（照 SideIslandId「捕获+读后即清」口径）。
    /// </summary>
    public static int PendingArenaRowIdx = -1;

    /// <summary>
    /// 进本标记统一重设（2026-10-28）：所有「进本路径」在设置自己的标记前先调本方法全清，
    /// 避免漏清导致旧标记污染下一局（如礼物本不打清 → 好感页点羁绊本被误判为礼物本）。
    /// 新增副本类型时，只需在对应路径里设自己的标记，不必再记着补清 N 处。
    /// 注意：本方法只负责清零，不含「进本优先级」读判定；PendingEndlessRift 由调用方按 endless 语义自行重立。
    /// </summary>
    public static void ResetPendingFlags()
    {
        PendingBattleChapter = 0;
        PendingBattleDifficulty = 0;
        PendingGoldDungeon = false;
        PendingBadgeDungeon = false;
        PendingScrollDungeon = false;
        PendingGiftDungeon = false;
        PendingBondDungeon = false;
        PendingBondHireId = null;
        PendingSideIsland = 0;
        PendingFinaleStage = 0;
        PendingArenaRowIdx = -1;
        PendingEndlessRift = false;
    }

    [Header("可替换资源")]
    public Sprite mapBackgroundSprite;
    public Sprite[] chapterBackgrounds = new Sprite[8]; // 可选覆盖 MapBg；空则只切换 StageNodes/Node_1..8
    public Sprite[] modeButtonSpriteIcons = new Sprite[5];
    public Sprite[] enemySprites = new Sprite[4];
    public Sprite[] dropSprites = new Sprite[4];

    // ── 公开引用（预制体序列化后可在 Inspector 拖资源）──
    [Header("左侧副本类型按钮（顺序：主线/精英/迷宫/每日/活动）")]
    public Button[] modeButtons = new Button[5];
    public Image[]  modeButtonIcons = new Image[5];
    public Text[]   modeButtonLabels = new Text[5];

    [Header("地图区")]
    public Text    chapterTitle;       // "第一章  边境小镇"
    public Button  prevChapterBtn;
    public Button  nextChapterBtn;
    public Image   mapBg;             // 替换：章节地图背景图
    public Transform stageNodeContainer; // 关卡节点父节点
    public Text    progressLabel;     // "24/24"
    public Image   progressFill;
    public Button  chapterRewardBtn;  // 章节奖励
    public Button  adventureLogBtn;   // 冒险日志

    [Header("关卡详情面板")]
    public Text    stageNameLabel;    // "1-3  哥布林营地"
    public Text    stageDescLabel;
    public Text    dropTitleLabel;    // "可能掉落"（字号对齐「敌人信息」）
    public Transform enemyIconContainer;
    public Transform dropIconContainer;
    public Text    staminaCostLabel;
    public Text    remainChancesLabel;
    public Button  addChancesBtn;

    [Header("难度按钮（普通/困难/噩梦）")]
    public Button[] difficultyButtons = new Button[3];
    public Text[]   difficultyLabels  = new Text[3];

    [Header("底部操作")]
    public Button startBtn;
    public Button sweepBtn;

    // ── 内部状态 ──
    static readonly string[] ModeNames  = { "主线冒险", "每日副本", "无限裂隙", "BOSS挑战", "活动副本" };
    // 2026-09-17：删除「地狱」——它与噩梦共用同一数值分支，是空壳。现在只有 普通 / 困难 / 噩梦 三档。
    // 2026-10-06：难度名一份拷贝都不留，全部改走 GameConfig.GetDifficultyName（战斗内 HUD 也读它）。
    static readonly Color    ColNormal  = new Color(0.30f, 0.55f, 0.22f, 1f);
    static readonly Color    ColHard    = new Color(0.28f, 0.42f, 0.65f, 1f);
    static readonly Color    ColNight   = new Color(0.45f, 0.22f, 0.62f, 1f);

    // ──【2026-10-23 主人新布局】节点名（prefab 实测，别动名字）──
    // RightContent 下三个页面节点：点左侧标签 → 显示对应节点；背景从根下 StageNodes 找。
    //   主线   → StageNodes/Node_1..8（每章一张整图）
    //   无限   → StageNodes/无限裂隙
    //   活动副本 → StageNodes/金币副本（金币/徽记两横条共用此背景，prefab 未摆徽记背景节点）
    const string MainNodeName      = "主线";
    const string EndlessNodeName   = "无限";
    const string ActivityNodeName  = "活动副本";
    const string ActivityBgNodeName = "金币副本";   // StageNodes 下的背景节点名（RiftMapNodeName 同口径）
    /// <summary>
    /// 活动副本背景图（2026-10-23 主人：「活动副本标签没有适配，背景应该用活动这个图片」）。
    /// prefab 里「金币副本」背景节点沿用了 Node_8 的雪山图（未适配）；且「活动.jpg」的 .meta
    /// 是 56 字符 guid（§2.3 口径：不修 guid，运行时用的图一律路径加载）→ 按路径设图，不动 prefab 排版。
    /// </summary>
    const string ActivityBgSpritePath = "UI/Art/Adventure/StageIcons/Big/活动";
    /// <summary>活动列表横条：金币副本（RightContent/活动副本/金币）。</summary>
    const string ActivityGoldRowName = "金币";
    /// <summary>活动列表横条：徽记副本（RightContent/活动副本/徽记）。</summary>
    const string ActivityBadgeRowName = "徽记";
    /// <summary>活动列表横条：卷轴副本（RightContent/活动副本/卷轴；2026-10-24 主人拍板接入）。</summary>
    const string ActivityScrollRowName = "卷轴";
    /// <summary>活动列表横条：礼物副本（RightContent/活动副本/礼物；2026-10-26 主人拍板「掉落礼物的活动副本周六开启」）。
    /// prefab 没摆第四条 → 运行时克隆卷轴条生成（照 AdventureUI.Finale 追查条的克隆先例），不改 prefab。</summary>
    const string ActivityGiftRowName = "礼物";
    /// <summary>没轮到（今天不开放）横条 bg 的压灰系数（2026-10-24 主人拍板「灰显」；选中效果改走 prefab「选中」节点）。</summary>
    const float ActivityRowGrayFactor = 0.45f;

    int _selectedMode = 0;
    int _selectedDiff = 0;
    int _selectedChapter = 1;
    /// <summary>活动副本选中态（2026-10-23 拆二 + 2026-10-24 加卷轴）：金币/徽记/卷轴三选一，与 ActivityDungeonKind 同源。</summary>
    ActivityDungeonKind _selectedActivityKind = ActivityDungeonKind.Gold;
    /// <summary>活动横条按钮是否已接线（金币/徽记/卷轴三横条，prefab 只摆了外观没挂 Button）。</summary>
    bool _activityRowsWired;
    /// <summary>横条底图原色缓存（选中高亮 = 改 bg 颜色，恢复要回原色；key = bg Image）。</summary>
    readonly Dictionary<Image, Color> _activityRowBaseColors = new Dictionary<Image, Color>();
    /// <summary>StageNodes 下「金币副本」背景节点的 Image（活动模式背景，prefab 自带）。</summary>
    Image _activityBgImg;
    /// <summary>活动副本背景图缓存（按路径加载，只 Load 一次）。</summary>
    Sprite _activityBgSprite;
    /// <summary>当前可进的章节集合（升序）——箭头在它里面循环，取代旧的 1..maxUnlocked 连续段</summary>
    readonly List<int> _availableChapters = new List<int>();
    List<Button> _stageNodeBtns = new List<Button>();
    readonly List<MonsterConfig> _previewMonsters = new List<MonsterConfig>();
    Text _bossTag;
    GameObject _tipRoot;
    Text _tipTitle;
    Text _tipBody;
    Text _floatToast;
    RectTransform _floatToastRt;
    Coroutine _floatToastCo;
    Sprite _goldDropSprite;
    Sprite _draftCoinDropSprite;
    Sprite _talentDropSprite;
    Sprite _scrollCommonSprite;
    Sprite _scrollRareSprite;
    Sprite _scrollLegendarySprite;
    Sprite _badgeDropSprite;
    Sprite _staminaIconSprite;
    Image _staminaCostIcon;
    Text _staminaCostText;   // 【2026-10-24 主人新布局】消耗值独立在 StaminaCost/Cost 子节点（预制体默认 "X10"）

    enum PreviewDropKind
    {
        Gold,
        DraftCoin,
        TalentStone,
        ScrollCommon,
        ScrollRare,
        ScrollLegendary,
        Badge,   // 徽记副本掉落预览（2026-10-23 活动副本一拆二）
        Gift     // 礼物副本掉落预览（2026-10-26 主人拍板「掉落礼物的活动副本周六开启」）
    }

    readonly List<PreviewDropKind> _previewDrops = new List<PreviewDropKind>(4);

    const float TOP_H    = 120f;
    const float BOT_H    = 150f;
    const float LEFT_W   = 170f;
    const float DETAIL_H = 340f;

    // ──【2026-10-11 主人拍板】关卡详情文字 ──
    // ① 主人「这个关卡的信息的字再大一号」→ 章节名 25 → 27。
    // ② 介绍框宽度【2026-10-19 主人改排版】起**跟随预制体**（信息框边界已拉大），
    //    代码不再运行时改宽（原 280 → 340 的强制逻辑已删）。
    //    字号 21：22 号在旧 344 宽下只能 15 字/行 → 溢出（实测推演，别再往上加）。
    // ③ 主人「可能掉落 4 个字的字号跟敌人信息字号一致」→ 预制体 DropTitle 18 → 25（= 节点 direnxinxi 字号）。
    // Build（代码建树）与 AutoBind（预制体绑定）两条路径都用这些常量，避免两套口径。
    const int DetailNameFontSize = 27;
    const int DetailDescFontSize = 21;
    const int DetailSubTitleFontSize = 25;

    // ──【2026-10-22 主人拍板】左侧模式标签（主线冒险 / 无限裂隙）字号 ──
    // 预制体现值 25 偏小 → 30。只改字号，按钮宽高/位置以预制体为准（铁律 32）；
    // Build（代码建树）与 AutoBind（预制体绑定）两条路径共用（见 ApplyModeLabelStyle）。
    const int ModeLabelFontSize = 30;

    bool _built;
    bool _preloaded;

    /// <summary>
    /// 【2026-10-28 主人报「一进还是出现会长说第七小队的剧情」】
    /// 进镇预热（TownHubController.CoWarmupPages）会对本页「点亮一遍再藏回」，
    /// 那一遍不算玩家进冒险页 → 本页所有玩家可见弹窗（支线岛情报指引 / 上一局意外退出询问）
    /// 都不该在 Loading 后面开播。
    /// 预热方置位本标志，ShowPage 消费一次并跳过（不写已播标记、不清中断快照，
    /// 不消耗这一岛的播报机会），等玩家自己点进冒险页时照常弹。
    /// 先例 = 酒馆 TavernUI.SuppressEggOnce（同一个坑：预热点亮被当成真实进店）。
    /// </summary>
    public bool SuppressGuideOnce;

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

    // ────────────────────────────────────────────────────
    // ITownPage
    // ────────────────────────────────────────────────────

    public void PreloadOnce()
    {
        if (_preloaded) return;

        // 预制体已有完整树时只绑定，不再重建
        if (transform.Find("LeftSidebar") != null || transform.Find("RightContent") != null)
        {
            AutoBindFromHierarchy();
            _built = true;
        }
        else if (!_built)
            Build();

        EnsureVisibleTransform();
        ConfigureHostCanvasOnce();
        GameFonts.ApplyToHierarchy(transform);
        WireClicks();

        _preloaded = true;
        gameObject.SetActive(false);
    }

    /// <summary>编辑器生成预制体时调用：建树并套字体，不隐藏、不绑点击</summary>
    public void BuildHierarchyForPrefab()
    {
        if (!_built) Build();
        EnsureVisibleTransform();
        GameFonts.ApplyToHierarchy(transform);
    }

    public void ShowPage()
    {
        if (!_preloaded) PreloadOnce();
        EnsureVisibleTransform();
        gameObject.SetActive(true);
        ClearStaleClaimAnim();   // 页面隐藏会杀领取退场协程：进页清标志，刷新自己收尾
        transform.SetAsLastSibling();
        TavernUI.SetGuildHallOverlayMode(true);
        TownPageDim.Ensure(transform);
        SoftenMapBgOverHall();

        Transform hall = GuildHallUI.Instance != null
            ? GuildHallUI.Instance.transform
            : transform.root;
        TownSharedChrome.RaiseSharedChrome(hall);
        // 【2026-10-28 主人重申「冒险界面只有两个资源：一个钻石一个体力」】冒险页顶条只显示钻石+体力，
        // 金币/天赋石隐藏（10-27 白天版曾实现、当晚被误删后一直四格，现按主人原口径恢复）。
        // 并排占上排两槽的排布见 ResourceStrip.ApplyRightAlign 的 N=2 分支。
        ResourceStrip.SetMode(ResourceStrip.Mode.DiamondStamina);
        EnsureStandaloneChrome(hall);

        // 越界（<1 或 >8）时落到「应推进的章节」：可选里第一个未通关，全通关回最后一章。
        // 仍在 1~8 范围内的有效选择（含已通关可重打的章）原样保留，由 RefreshAvailableChapters 兜底修正。
        if (_selectedChapter < 1 || _selectedChapter > 8)
            _selectedChapter = ChapterRouteTable.CurrentChapterToAdvance(SaveSystem.Instance?.Data);
        HideUnfinishedEntries();
        RefreshAll();
        // 【2026-10-24 主人拍板】进页不再自动弹「获得奖励」：改为玩家点击宝箱时按关取用
        // AdventureRewardNotice.Take 弹窗（见 AdventureUI.ChestNodes.cs OnClickChestClaim）。

        // 竖屏专用适配：更瘦屏时把 RightContent 居右、MapRoot 右溢出归零、DetailPanel 宽对齐下沉到底。
        // 已迁移到本界面自身（ApplyAdventureFit），标准 9:16 屏或开关关闭时该方法内部直接回退、不改任何东西。
        ApplyAdventureFit();

        // 【2026-10-23 主人要求】进页左侧标签逐颗弹入（见 AdventureUI.Anim.cs）
        PlayLeftTabsPopIn();

        // 【2026-10-28 主人报「怎么一进还是出现会长说第七小队的剧情」】进镇预热协程
        // （TownHubController.CoWarmupPages）会对冒险页「点亮一遍再藏回」，那一遍也走本方法 →
        // 剧情/询问在 Loading 后面就开播了，玩家进主界面第一眼撞上。
        // 预热点亮 ≠ 玩家进冒险页 → 本次整块跳过，且**不写任何标记**
        // （不消耗掉这一岛的播报机会、不清中断局快照），等玩家自己点进冒险页时照常播。
        // 先例 = 酒馆 SuppressEggOnce（同一坑）。
        bool warmupOnly = SuppressGuideOnce;
        SuppressGuideOnce = false;

        if (!warmupOnly)
        {
            // 【2026-10-28 主人拍板】会长支线岛情报指引：不在主界面主动弹（相关剧情以后一律不主动出来），
            // 只在玩家进冒险界面时播（每岛每次洗牌轮一次，播过记档）。有待续局的局让路给续局询问，
            // 本次不播、标记不写，下次进冒险页再播。
            if (!RunLoadout.HasInterruptedRun())
            {
                SideIslandGuide.TryShowForCurrent(SaveSystem.Instance?.Data);
            }

            // 【2026-10-22 主人原话「进了冒险界面就弹出上一局意外退出 是否继续 然后两个按钮确定和取消」】
            // 上一场战斗中途断掉的局：进冒险页就问一次。确定 = 继续上一局；取消 = 重新开始（丢局）。
            TryPromptInterruptedRun();
        }
    }

    /// <summary>
    /// 「上一局意外退出」询问（弹窗本体在 <see cref="RunResumeChoiceUI"/>）。
    /// 只在「有可续局 && 上一场战斗是中途断的（midBattle 标记还在）」时弹；答过一次标记即消，不再重复弹。
    /// </summary>
    void TryPromptInterruptedRun()
    {
        if (!RunLoadout.HasInterruptedRun()) return;
        RunResumeChoiceUI.Show(RunLoadout.PeekResumable()?.chapter ?? 1,
            onConfirm: () =>
            {
                // 确定 = 继续上一局：清掉中断标记，并把「被打断那关」的关号 + 局内金币池
                // 从中断快照接回构筑（见 RunLoadout.ConfirmResumeInterruptedRun），
                // 之后进本照旧静默续局，从被打断的那一关重打、不重发启动金。
                // 快照消费完毕一并清：金币已接回构筑，留着会被下次启动结算重复入账。
                RunLoadout.ConfirmResumeInterruptedRun(BattleStateSaver.PeekInterrupted());
                BattleStateSaver.ClearInterruptedSave();
            },
            onCancel: () =>
            {
                // 取消 = 重新开始：丢掉上一局（弹窗文案已写明构筑与金币不保留），快照一并清，
                // 否则下次启动会被 SettleInterruptedRun 按「撤离失败」再结算一遍。
                RunLoadout.Clear();
                BattleStateSaver.ClearInterruptedSave();
            });
    }


    /// <summary>
    /// 世界地图选完区域后的回调（2026-10-24 主人拍板改选章制）：
    /// 点「进入」后世界地图关闭，冒险页切到该章——背景换该章图、关卡列变 X-1（从第 1 关打起），
    /// **不再直接开战**，玩家自己点「开始冒险」。世界地图从此就是选章入口。
    /// </summary>
    public void OnWorldMapEnter(int chapter)
    {
        if (chapter < 1 || chapter > 8) return;
        if (!ChapterRouteTable.CanEnter(chapter, SaveSystem.Instance?.Data))
        {
            Toast("该区域尚未解锁");
            return;
        }
        // 难度逐章把关（2026-10-24 主人拍板）：该章在当前难度未开启 → 提示，不切章
        if (!ChapterRouteTable.CanEnterAtDiff(chapter, _selectedDiff, SaveSystem.Instance?.Data))
        {
            Toast(ChapterRouteTable.DiffChapterLockHint);
            return;
        }
        _selectedChapter = chapter;
        _selectedMode = 0;
        RefreshAll();
        PlayChapterFadeIn();
        // 提示落点：该章下一关（没通过 = 从 1 关打起；整章通关重打 = 第 1 关）
        // 【2026-10-29 修「1-1 未通 1-2 已标领取后」】下一关 = 第一个未通关（精确查单关），不再用最大号+1 推断
        int chapterTotal = ChapterSubTotal(chapter, SaveSystem.Instance?.Data);
        int nextStage = Mathf.Clamp(FirstUnclearedStage(chapter, chapterTotal, SaveSystem.Instance?.Data), 1, chapterTotal);
        Toast($"已前往 {GameConfig.GetChapterMapName(chapter)} {chapter}-{nextStage}");
    }

    public void HidePage()
    {
        HideTip();
        HideFloatToast();
        if (!gameObject.activeSelf) return;
        // 【2026-10-28】离开冒险页还原主界面四格（金币/天赋石恢复）——进冒险页时收成钻石+体力两格。
        // 放在 activeSelf 判断之后：本页已隐藏时不再回写模式，避免盖掉别的页当前的资源条模式。
        ResourceStrip.SetMode(ResourceStrip.Mode.Main);
        gameObject.SetActive(false);
        TavernUI.SetGuildHallOverlayMode(false);
    }

}
