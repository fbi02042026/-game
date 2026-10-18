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
    /// 无限裂隙进本标记（2026-10-17）。TryEnterBattle 每次按所选模式**重设**（非裂隙恒 false，防上一次残留污染）；
    /// BattleManager.StartNewRunInternal 读完即清。体力确认弹窗也读它区分消耗 10/20。
    /// </summary>
    public static bool PendingEndlessRift;

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

    int _selectedMode = 0;
    int _selectedDiff = 0;
    int _selectedChapter = 1;
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
    Sprite _staminaIconSprite;
    Image _staminaCostIcon;

    enum PreviewDropKind
    {
        Gold,
        DraftCoin,
        TalentStone,
        ScrollCommon,
        ScrollRare,
        ScrollLegendary
    }

    readonly List<PreviewDropKind> _previewDrops = new List<PreviewDropKind>(4);

    const float TOP_H    = 120f;
    const float BOT_H    = 150f;
    const float LEFT_W   = 170f;
    const float DETAIL_H = 340f;

    // ──【2026-10-11 主人拍板】关卡详情文字 ──
    // ① 主人「这个关卡的信息的字再大一号」→ 章节名 25 → 27。
    // ② 主人「敌人信息那右边不是还有空地吗 再往右拉伸一点」→ 介绍框宽 280 → 344（预制体 StageDesc 宽）；
    //    拉宽后每行可容 16 字 → 第 1 章那段 31 字正好 2 行、不再溢出压住下面的「敌人信息」。
    //    字号取 +1（20 → 21）：22 号在 344 宽下只能 15 字/行 → 又变 3 行溢出（实测推演，别再往上加）。
    // ③ 主人「可能掉落 4 个字的字号跟敌人信息字号一致」→ 预制体 DropTitle 18 → 25（= 节点 direnxinxi 字号）。
    // Build（代码建树）与 AutoBind（预制体绑定）两条路径都用这些常量，避免两套口径。
    const int DetailNameFontSize = 27;
    const int DetailDescFontSize = 21;
    const float DetailDescWidth = 340f;
    const int DetailSubTitleFontSize = 25;

    bool _built;
    bool _preloaded;

    void Awake()
    {
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
        transform.SetAsLastSibling();
        TavernUI.SetGuildHallOverlayMode(true);
        TownPageDim.Ensure(transform);
        SoftenMapBgOverHall();

        Transform hall = GuildHallUI.Instance != null
            ? GuildHallUI.Instance.transform
            : transform.root;
        TownSharedChrome.RaiseSharedChrome(hall);
        EnsureStandaloneChrome(hall);

        int max = GetMaxUnlockedChapter();
        if (_selectedChapter < 1 || _selectedChapter > max)
            _selectedChapter = max;
        HideUnfinishedEntries();
        RefreshAll();

        // 竖屏专用适配：更瘦屏时把 RightContent 居右、MapRoot 右溢出归零、DetailPanel 宽对齐下沉到底。
        // 已迁移到本界面自身（ApplyAdventureFit），标准 9:16 屏或开关关闭时该方法内部直接回退、不改任何东西。
        ApplyAdventureFit();
    }


    /// <summary>世界地图选完区域后的回调（传送门 WorldMapPortal 走这里开战）。</summary>
    public void OnWorldMapEnter(int chapter)
    {
        if (chapter < 1 || chapter > 8) return;
        if (!ChapterRouteTable.CanEnter(chapter, SaveSystem.Instance?.Data))
        {
            Toast("该区域尚未解锁");
            return;
        }
        if (SaveSystem.Instance != null && SaveSystem.Instance.Data != null
            && SaveSystem.Instance.Data.HasClearedChapter(chapter))
        {
            Toast("该区域已通关");
            return;
        }
        _selectedChapter = chapter;
        _selectedMode = 0;
        _selectedDiff = 0;
        OnStartBattle();
    }

    public void HidePage()
    {
        HideTip();
        HideFloatToast();
        if (!gameObject.activeSelf) return;
        gameObject.SetActive(false);
        TavernUI.SetGuildHallOverlayMode(false);
    }

}
