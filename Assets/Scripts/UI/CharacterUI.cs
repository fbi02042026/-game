using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 角色页（MainNavTab.Character）。参考 Art/UI/Character/character_reference.png。
/// - 无左侧装备栏
/// - 右上：天赋 / 技能；左侧独立按钮也可打开技能选择
/// - 背包格子按角色页预制体显示（4×3，共 12 格）
/// - 不自建资源条/底栏，Show 时 RaiseSharedChrome，底部预留下方 150
/// </summary>
public partial class CharacterUI : MonoBehaviour, ITownPage
{
    public static CharacterUI Instance { get; private set; }
    public MainNavTab Tab => MainNavTab.Character;

    const float TopReserve = 120f;
    const float BottomReserve = 150f;

    [Header("入口")]
    public Button talentButton;
    public Button skillButton;
    public Button leftSkillButton; // 2026-09-19 起按主人要求移除显示：原左下角「装备技能」入口，技能改随机后失效
    public Transform leftButtonRoot; // 主人在预制体里把 LeftSkillButton 改名为 LeftButton；该组现在内含「背包」按钮，禁止整体隐藏
    Button backpackEntryButton;     // 「背包」入口：优先取预制体 Content/Stage/LeftButton/背包，缺失时才运行时生成
    Button boardEntryButton;        // 运行时创建的「看板」入口，挂在天赋按钮同父节点下方（背包之后）

    [Header("展示")]
    public Image portraitImage;
    public Button flipPortraitButton;
    public Text titleText;
    public Text titleSubText;
    public Image headerAvatar;
    public Image carriedSkillIcon;

    [Header("基础属性")]
    public Text attrHpText;
    public Text attrAtkText;
    public Text attrMagicAtkText;   // 2026-09-29 魔攻（面板新增节点，找不到则不显示）
    public Text attrDefText;
    public Text attrMagicDefText;   // 2026-09-29 魔防（面板新增节点，找不到则不显示）
    public Text attrSpdText;
    public Text attrCritText;
    public Text attrResistText;

    [Header("背包")]
    public Text bagCapacityText;
    public TownBackpackGrid backpackGrid;
    public Button bagExpandButton;
    /// <summary>2026-10-24 主人拍板：角色页背包改名「随身仓库」，与战斗内「作战背包」区分，避免两个背包混为一谈。
    /// 真源 = 预制体 Content/BackpackPanel 下的 Text 节点 BackpackTitle；运行时覆盖其文案，不改预制体文件。</summary>
    public Text bagTitleText;
    /// <summary>
    /// 界面内的背包面板 Content/BackpackPanel（主人在预制体里摆好的，不是弹窗）。
    /// 「背包」按钮切换它的显隐；AutoBind 里赋值。
    /// </summary>
    public Transform bagPanelRoot;
    /// <summary>
    /// 兜底开关：主人在编辑器里展开编辑后忘了把 Content/BackpackPanel、SkillSelectUI 关掉
    /// （预制体里 activeSelf 是 1），初始化时强制收起这两个面板。
    /// 置 false 可一键回退（完全不碰二者）。
    /// </summary>
    public bool collapsePanelsOnStart = true;
    /// <summary>角色页背包网格显示规格覆盖：`每行 8 格 × 4 行，下 2 行上锁`。关掉则回退 4 列默认规格。</summary>
    public bool useWideBackpackGrid = true;
    /// <summary>点击 BackpackPanel 外任意处收起背包面板。</summary>
    public bool tapOutsideToCloseBag = true;
    /// <summary>背包翻页按钮（预制体优先：BackpackPanel/PrevPageButton、NextPageButton）。</summary>
    public Button bagPrevPageButton;
    /// <summary>背包翻页按钮（预制体优先：BackpackPanel/NextPageButton）。</summary>
    public Button bagNextPageButton;
    /// <summary>背包右上角「整理」按钮（预制体 BackpackPanel 下的 SortButton，此前从未绑过）。
    /// 2026-10-24 主人拍板：点它切换排序——整理态 = 同类聚拢 + 稀有度高的在前。</summary>
    public Button bagSortButton;

    // ── 体力药水条目（2026-10-18 主人拍板：药水入背包可使用；运行时建在 BackpackPanel 底部空档，不碰预制体）──
    GameObject _potionRow;
    Text _potionCountLabel;

    [Header("弹层")]
    public SkillSelectUI skillSelect;

    bool _built;
    bool _preloaded;
    bool _wired;
    bool _portraitFlipped;
    bool _bagEventsWired;
    int _bagPage;
    /// <summary>2026-10-24 主人拍板：随身仓库排序开关。false=默认获得顺序；true=整理态（同类聚拢+稀有度高的在前）。</summary>
    bool _bagSortedByRarity;
    GameObject _bagTapLayer;
    /// <summary>背包窗体自适应（瘦屏下只做防溢出 / 防裁切）。关掉则完整还原主人手摆的原始值。</summary>
    public bool enableBackpackFit = true;
    bool _bagFitCaptured;
    BackpackFitBase _bagFitBase;

    /// <summary>背包自适应用的原始值快照（主人手摆的矩形 + 手调的 GridLayoutGroup 参数），还原时原样套回。</summary>
    private struct BackpackFitBase
    {
        public Vector2 panelAnchorMin, panelAnchorMax, panelPivot, panelSizeDelta, panelPos;
        public Vector2 gridAnchorMin, gridAnchorMax, gridPivot, gridSizeDelta, gridPos;
        public Vector2 cellSize, spacing;
        public int padL, padR, padT, padB;
        public bool hasPadding;
    }

    /// <summary>角色页背包网格的显示列数（只改显示，GameConfig.BACKPACK_WIDTH 仍是 4）。</summary>
    const int WideBackpackColumns = 8;
    /// <summary>角色页背包网格显示的解锁行数：4 显示行里下 2 行上锁。</summary>
    const int WideBackpackUnlockedRows = 2;
    /// <summary>翻页按钮在 BackpackPanel 内的纵坐标（GridContainer 下方那条空档）。</summary>
    const float BagPageButtonY = -184f;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_bagEventsWired && BattleBagSystem.Instance != null)
        {
            BattleBagSystem.Instance.OnBackpackChanged -= OnBagOrCostumeChanged;
            BattleBagSystem.Instance.OnCostumeChanged -= OnBagOrCostumeChanged;
            _bagEventsWired = false;
        }
    }

    /// <summary>
    /// 收起主人在编辑器里「展开编辑忘了关」的两个节点（预制体里 activeSelf 目前是 1）：
    /// Content/BackpackPanel 与 SkillSelectUI。只在 PreloadOnce 里跑一次，运行时兜底纠正，不改预制体文件。
    /// 只收起，不动 OpenSkillSelect() 里的 skillSelect.Show()。
    /// </summary>
    void CollapseForgottenPanels()
    {
        if (!collapsePanelsOnStart) return;

        if (bagPanelRoot == null)
            bagPanelRoot = transform.Find("Content/BackpackPanel") ?? FindDeep(transform, "BackpackPanel");
        if (bagPanelRoot != null && bagPanelRoot.gameObject.activeSelf)
            bagPanelRoot.gameObject.SetActive(false);

        if (skillSelect == null)
            skillSelect = GetComponentInChildren<SkillSelectUI>(true);
        if (skillSelect != null)
        {
            if (skillSelect.gameObject.activeSelf)
                skillSelect.Hide();
        }
        else
        {
            var skillRoot = transform.Find("SkillSelectUI") ?? FindDeep(transform, "SkillSelectUI");
            if (skillRoot != null && skillRoot.gameObject.activeSelf)
                skillRoot.gameObject.SetActive(false);
        }
    }

    public void PreloadOnce()
    {
        if (_preloaded) return;

        if (transform.Find("Content") != null)
        {
            AutoBind();
            _built = true;
        }
        else if (!_built)
            BuildHierarchyForPrefab();

        CollapseForgottenPanels();
        StripLocalChrome();
        EnsureVisibleTransform();
        ConfigureHostCanvasOnce();
        if (backpackGrid != null)
        {
            backpackGrid.BindFromHierarchy(transform);
            // 预制体已有格子则只绑不重建，避免盖掉手摆布局
        }
        GameFonts.ApplyToHierarchy(transform);
        WireClicks();
        WireBagEvents();
        FixWrongArtBindings();
        _preloaded = true;
        gameObject.SetActive(false);
    }

    public void ShowPage()
    {
        if (!_preloaded) PreloadOnce();
        EnsureVisibleTransform();
        ApplyBgEnvelopeForThinScreen();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        SetGuildOverlay(true);
        TownPageDim.Ensure(transform);

        Transform hall = GuildHallUI.Instance != null ? GuildHallUI.Instance.transform : transform.root;
        TownSharedChrome.RaiseSharedChrome(hall);
        // 2026-10-13 主人拍板：「主界面的顶条跑到角色界面了」→ 本页不显示主界面那条顶栏
        //（金币 / 体力 / 邮件 / 设置）。底栏照旧保留 —— 本页要靠它切页（预制体已预留下方 150）。
        // ⚠ 只切显隐、不销毁；HidePage 还原，别的页 ShowPage 里的 RaiseSharedChrome 也会重新点亮。
        TownSharedChrome.SetTopBarVisible(hall, false);

        // 2026-10-24 主人拍板：本页顶部显示 金币/钻石/天赋石 资源条（克隆主界面 TopBar 的页私有条，
        // 见 CharacterUI.ResBar.cs；与上面隐藏「主界面那条顶栏」不冲突 —— 那条整宽带邮件/设置，本条只有三列货币）。
        EnsureResBar();

        // 页可能比背包系统更早预载，Show 时再补订一次
        WireBagEvents();
        RefreshAll();
        EnsureStageShadow();
        // SPUM 仅离屏换装；Portrait 用玩家立绘
        TownHeroCostumePreview.EnsureOn(this)?.EnsureOffscreenCostume();
        RefreshPlayerPortrait();
        TutorialHintUI.Ensure().Hide();
    }

    public void HidePage()
    {
        if (skillSelect != null) skillSelect.Hide();
        TownHeroCostumePreview.EnsureOn(this)?.Hide();
        if (!gameObject.activeSelf) return;
        gameObject.SetActive(false);
        // 顶栏交还（与 ShowPage 的 SetTopBarVisible(false) 成对）：下一页 ShowPage 也会点亮，
        // 这里还原是防「离开角色页后顶栏一直不见」的兜底路径。
        Transform hall = GuildHallUI.Instance != null ? GuildHallUI.Instance.transform : transform.root;
        TownSharedChrome.SetTopBarVisible(hall, true);
        SetGuildOverlay(false);
    }

    public void Show() => ShowPage();
    public void Hide() => HidePage();

    public void RefreshAll()
    {
        RefreshIdentity();
        RefreshPlayerPortrait();
        // 携带技能展示按主人 2026-09-19 要求移除（技能改随机，已装备技能入口失效），不再刷新
        RefreshAttrs();
        RefreshBag();
        RefreshResNumbers();   // 2026-10-24 主人拍板：页顶金币/钻石/天赋石资源条（CharacterUI.ResBar.cs）
        TownHeroCostumePreview.EnsureOn(this)?.EnsureOffscreenCostume();
    }
}
