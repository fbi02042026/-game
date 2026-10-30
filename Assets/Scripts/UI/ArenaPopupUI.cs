using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 竞技场弹窗 · 换位制（2026-10-29 主人拍板改版）。
/// 展示层走预制体 Assets/Resources/Prefabs/Town/ArenaPopupUI.prefab
///（菜单 Tools/竞技场/生成竞技场弹窗预制体 一次性生成；主人手改后【禁止重跑生成器】）。
/// 运行时只按节点名绑定（UiFind.Deep）+ 克隆榜单行 + 填数据/接事件；
/// 主人改排版/图标/按钮色都会保留。行底色例外：换位时玩家行高亮写 _rowMine，
/// 非玩家行恢复隔行色（想改色改本文件常量）。
/// 节点名契约见 ArenaPopupPrefabBuilder.cs，改名前先对齐两边。
/// 字号 24/48（fusion-pixel 12px 点阵整倍数）；数据与规则全部在 ArenaRules（真源 SaveData.arena*）。
/// </summary>
public class ArenaPopupUI : MonoBehaviour
{
    public static ArenaPopupUI Instance;

    public const string PrefabPath = "Prefabs/Town/ArenaPopupUI";

    // ── 2026-10-29 深夜主人手摆进预制体的三个节点（节点名契约，改名要两边对齐）──
    /// <summary>「对战记录」入口按钮（中文节点名；不再运行时克隆 RewardBtn，克隆会和主人手摆的那颗撞成两颗）。</summary>
    const string RecordBtnName = "对战记录Btn";
    /// <summary>排行奖励面板（沿用通用弹窗皮名；自带 Root/Dim 黑底 + 5 档奖励行 + 确定钮）。</summary>
    const string RewardPanelName = "EvacuateConfirmPopup";
    /// <summary>对战记录面板（主人已整块搭进主预制体，不再 Resources.Load 独立预制体）。</summary>
    const string RecordsNodeName = "ArenaBattleRecords";

    const int RowTotal = ArenaRules.BotCount + 1;   // 100 机器人 + 玩家 = 101 行
    const float RowStep = 64f;
    const float RowH = 56f;
    /// <summary>视口实际高（= 预制体 List 高，2026-10-30 框体 1000→1080 时 550→630；ScrollToPlayer 居中用）。</summary>
    const float ViewportH = 630f;
    const float ContentH = RowTotal * RowStep + 8f;

    // ── 榜单行控件（行号 0=榜首 … 100=末位；玩家行下标=arenaPlayerPos）──
    readonly GameObject[] _rowRoots = new GameObject[RowTotal];
    readonly Text[] _rowRank = new Text[RowTotal];
    readonly Image[] _rowHead = new Image[RowTotal];
    readonly Text[] _rowName = new Text[RowTotal];
    readonly Text[] _rowPts = new Text[RowTotal];
    readonly GameObject[] _rowFight = new GameObject[RowTotal];   // 「对战」按钮（可挑战行显示）
    readonly GameObject[] _rowDash = new GameObject[RowTotal];    // 「—」（不可挑战行显示）
    readonly bool[] _rowIsMine = new bool[RowTotal];
    readonly Outline[] _rowOutline = new Outline[RowTotal];   // 2026-10-30 主人拍板：滑动列表「我的那一条」加描边（运行时 Outline 组件，不用材质球）

    ScrollRect _scroll;
    RectTransform _content;
    GameObject _rowTemplate;
    Text _triesText;      // 顶部「对战次数 N/M」
    Text _adBtnText;
    Text _noticeText;
    bool _bindFailed;

    // ── 前三领奖台（Podium1/2/3 卡内同名节点，绑定从各自卡根搜）──
    readonly Image[] _podRoot = new Image[3];    // 卡根（换名次色 tint）
    readonly Text[] _podRank = new Text[3];
    readonly Image[] _podHead = new Image[3];
    readonly Text[] _podName = new Text[3];
    readonly Text[] _podPts = new Text[3];

    // ── 我的排名条 ──
    Text _myRankText;
    Image _myHead;
    Text _myName;
    Text _myPtsText;

    // 【2026-10-29 主人拍板】选职业/结果子面板节点已从预制体删除——
    // 选职业改调独立界面 PlayerJobSelectUI，结算改调战斗结果界面 BattleResultScreen（对齐现役三场景 UI）。
    // （2026-10-30「竞技场真战斗」：结果界面由战斗结算链播，本类不再自己弹；_chosenJob 字段随掷骰链一并删除。）

    int _pendingRowIdx = -1;

    // ── 机器人/我的档案详情（运行时建树，不进 prefab 契约；点榜单行打开）──
    GameObject _detailRoot;
    Transform _detailRows;
    Text _dTitle;
    Text _dPower;
    Text _dPoints;

    // ── 对战记录（2026-10-29 晚主人拍板新增；深夜起面板节点已搭进主预制体，代码只绑定+填行）──
    GameObject _recordsRoot;
    RectTransform _recordContent;

    // ── 排行奖励面板（2026-10-29 深夜主人拍板：节点已搭进主预制体，自带 Dim 黑底；代码只管开关）──
    GameObject _rewardRoot;

    // ── 竞技商店（2026-10-30 积分版：主人手摆 Top/竞技商店Btn 开关「竞技商店」页；
    // 页内 RecordsBox（689×739）+ 底框/积分icon(余额)/RTitle/RCloseBtn/商品（示例单元）。
    // 商品单元按需求收敛为「图标+积分价格」：克隆排格 4×2，横条/商品名(1)/购买 三件多余节点克隆里隐藏，
    // 示例单元本体恒隐藏（同 RowTemplate 口径）。排版不改 prefab，全部运行时摆（铁律 3）。──
    const string ShopBtnName = "竞技商店Btn";
    const string ShopPageName = "竞技商店";
    GameObject _shopRoot;
    RectTransform _shopBox;          // 页内 RecordsBox（货架格子容器）
    GameObject _shopUnitTemplate;    // 「商品」示例单元（克隆源，恒隐藏）
    Text _shopBalanceText;           // 积分icon 下的余额数字
    Text _shopBannerText;            // 赛季末清零横幅（运行时建，最后 3 天显示）
    readonly List<GameObject> _shopUnits = new List<GameObject>();   // 8 个克隆单元（下标对齐 ArenaShopGoods.AllGoods）

    // 行底色（运行时在克隆与换位时写；预制体里摆的同款色只是初始观感）
    readonly Color _panelColor = new Color(0.13f, 0.12f, 0.18f, 0.97f);   // 详情弹窗盒子底
    readonly Color _btnSlate = new Color(0.35f, 0.36f, 0.45f, 1f);        // 详情弹窗关闭钮
    readonly Color _rowColorA = new Color(0.21f, 0.20f, 0.28f, 0.95f);
    readonly Color _rowColorB = new Color(0.25f, 0.24f, 0.33f, 0.95f);
    readonly Color _rowMine = new Color(0.42f, 0.32f, 0.13f, 0.97f);
    readonly Color _rowOutlineGold = new Color(1f, 0.80f, 0.35f, 1f);   // 玩家行描边色（金色）
    readonly Color _txtSub = new Color(0.75f, 0.75f, 0.80f, 1f);
    readonly Color _txtGold = new Color(1f, 0.80f, 0.35f, 1f);

    // ───────────────────────── 打开 / 生命周期 ─────────────────────────

    public static void Show()
    {
        var ui = Ensure();
        if (ui != null) ui.Open();
    }

    static ArenaPopupUI Ensure()
    {
        if (Instance != null) return Instance;
        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[Arena] 竞技场弹窗预制体缺失：Resources/{PrefabPath}（菜单 Tools/竞技场/生成竞技场弹窗预制体 生成）");
            return null;
        }
        var go = Object.Instantiate(prefab);
        DontDestroyOnLoad(go);
        go.SetActive(false);
        var ui = go.GetComponent<ArenaPopupUI>();
        if (ui == null)
        {
            Debug.LogError($"[Arena] 预制体根上没有 ArenaPopupUI 组件：{PrefabPath}");
            Object.Destroy(go);
            return null;
        }
        ui.Bind();
        return ui;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        ArenaRules.Changed -= Refresh;
    }

    void Open()
    {
        if (_bindFailed) return;
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        ArenaRules.EnsurePeriods(data);   // 跨天清次数+机器人漂移换位；跨月赛季结算（懒触发）
        string notice = ArenaRules.TakeSeasonNotice(data);

        var canvas = GetComponent<Canvas>();
        if (canvas != null) UICanvasSetup.RefreshPopup(canvas, GameConfig.UiSort.TownPopup);

        gameObject.SetActive(true);
        FitBackdrop();   // 背景等比铺满：父级实际矩形此刻才算得出（2026-10-30 主人拍板）
        Refresh();
        ScrollToPlayer();
        if (_noticeText != null)
        {
            _noticeText.gameObject.SetActive(!string.IsNullOrEmpty(notice));
            if (!string.IsNullOrEmpty(notice)) _noticeText.text = notice;
        }
    }

    void Close()
    {
        if (_detailRoot != null) _detailRoot.SetActive(false);
        if (_recordsRoot != null) _recordsRoot.SetActive(false);
        if (_rewardRoot != null) _rewardRoot.SetActive(false);
        if (_shopRoot != null) _shopRoot.SetActive(false);   // 商店页一并收起（关弹窗不留残页）
        _pendingRowIdx = -1;
        gameObject.SetActive(false);
    }

    // ── 顶部一排（2026-10-30 主人拍板「对战次数/对战记录/排行奖励移出弹窗放到顶部一排」）──
    // 五个节点（TriesText 是 Panel 下的独立兄弟节点，必须跟胶囊一起挪）从 Panel reparent 到 Root
    //（Root 全屏 stretch 且与 Panel 同中心 → x 保持原值即保持主人原横排），纵位统一 TopBarY：
    // Panel 顶边 +570、标题横幅底 +516 → 一排按钮顶 +508，整排悬在金框外、标题之下，互不重叠。
    // ⚠ 预制体不动（先例：BuildRewardPanel 的 ApplyFillScreen 运行时改锚）——主人改 prefab 里这几颗的
    // 图/字号照常生效，位置与父级以这里的运行时摆位为准；要固进 prefab 等验收后再说。
    // 已把 "ArenaPopupUI" 加进 UiLayoutStretch.IndividuallyFitted（2026-10-30 主人反馈小分辨率
    // 金框挡住顶部一排）：ApplyVerticalSplit/EnsureFitBackdrop 整块跳过本弹窗，五节点恒为中心锚
    // y=450，与居中的 Panel/标题相对位置任何分辨率恒定（逻辑高恒 >=1280，金框顶 570 永在按钮顶
    // 508 之上 62px，挡不住）。
    /// <summary>顶部一排纵位（Root 本地、中心锚；Panel 顶 570 / 标题底 516 → 450 不撞任何元素）。</summary>
    const float TopBarY = 450f;

    /// <summary>移到顶部一排的节点（x 顺序即主人原横排：次数胶囊、次数数字、广告+2、对战记录、排行奖励）。</summary>
    static readonly string[] TopBarNodes = { "TriesCapsule", "TriesText", "AdBonusBtn", RecordBtnName, "RewardBtn" };

    void MoveTopBar()
    {
        // 【2026-10-30 主人已在预制体手摆 Top 节点：五节点归入其下（Top 贴顶锚 0.5,1 / pivot 顶中、
        // 位置主人自调），金框改高 1140→1000】代码完全让位——有 Top 就一个节点都不碰
        //（位置/父级/层级全以预制体为准，铁律 9）。自适应已实测闭环：Top 距屏顶恒定，金框顶距顶
        // 最矮情形（逻辑高 1280）=140 > 按钮底 133，任何分辨率都不遮挡。
        // 下面的运行时挪只剩「预制体还没有 Top」的兜底职责（正常永远走不到）。
        if (UiFind.Deep(transform, "Top") != null) return;   // 全 prefab 唯一（2026-10-30 实测），不会误命中
        var root = UiFind.Deep(transform, "Root");
        if (root == null)
        {
            Debug.LogError("[Arena] 预制体缺节点：Root（顶部一排挪不动，留在弹窗内）");
            return;
        }
        var panel = UiFind.Deep(root, "Panel");   // 主面板（Root 子树内第一个 Panel；奖励面板的 Panel 在根下另一子树）
        // 插入点 = Panel 之后，且不低于主 Dim（背景必须在顶部一排之下）；记录/奖励面板在更后，打开时照常盖住顶部
        int insert = panel != null ? panel.GetSiblingIndex() + 1 : root.childCount;
        var dimT = UiFind.Deep(root, "Dim");
        if (dimT != null) insert = Mathf.Max(insert, dimT.GetSiblingIndex() + 1);
        for (int i = 0; i < TopBarNodes.Length; i++)
        {
            var t = UiFind.Deep(root, TopBarNodes[i]);
            if (t == null)
            {
                Debug.LogError($"[Arena] 预制体缺节点：{TopBarNodes[i]}（顶部一排少一件，留在原位）");
                continue;
            }
            t.SetParent(root, false);   // 保持本地值（Panel 与 Root 同中心，x 语义不变）
            var rt = (RectTransform)t;
            var p = rt.anchoredPosition;
            rt.anchoredPosition = new Vector2(p.x, TopBarY);   // 只改 y；x 保持主人原横排
            t.SetSiblingIndex(insert++);
        }
    }

    // ───────────────────────── 绑定（按节点名，契约见 ArenaPopupPrefabBuilder.cs） ─────────────────────────

    T Bind<T>(string name) where T : Component => Bind<T>(transform, name);

    /// <summary>searchRoot 从指定节点往下搜（Result/JobSelect 内部绑定用它，避开榜单行模板里的同名节点）。</summary>
    T Bind<T>(Transform searchRoot, string name) where T : Component
    {
        var t = UiFind.Deep(searchRoot, name);
        if (t == null)
        {
            Debug.LogError($"[Arena] 预制体缺节点：{name}（节点契约见 ArenaPopupPrefabBuilder.cs，改名要两边对齐）");
            _bindFailed = true;
            return null;
        }
        return t.GetComponent<T>();
    }

    void Bind()
    {
        _scroll = Bind<ScrollRect>("List");
        if (_scroll == null) return;
        var viewport = UiFind.Deep(_scroll.transform, "Viewport");
        _content = UiFind.Deep(_scroll.transform, "Content") as RectTransform;
        _rowTemplate = UiFind.Deep(_scroll.transform, "RowTemplate")?.gameObject;
        if (viewport == null || _content == null || _rowTemplate == null)
        {
            Debug.LogError("[Arena] 预制体缺 List/Viewport/Content/RowTemplate（契约见 ArenaPopupPrefabBuilder.cs）");
            _bindFailed = true;
            return;
        }
        // 滚动参数运行时兜一遍（幂等；主人手滑清了引用也不至于滚不动）
        _scroll.horizontal = false;
        _scroll.movementType = ScrollRect.MovementType.Clamped;
        _scroll.scrollSensitivity = 15f;
        _scroll.viewport = viewport as RectTransform;
        _scroll.content = _content;

        _triesText = Bind<Text>("TriesText");
        _noticeText = Bind<Text>("NoticeText");
        if (_noticeText != null) _noticeText.gameObject.SetActive(false);

        // 顶部：看广告 +2 按钮
        var adBtn = UiFind.Deep(transform, "AdBonusBtn");
        if (adBtn != null)
        {
            var lt = UiFind.Deep(adBtn, "Label");
            if (lt != null) _adBtnText = lt.GetComponent<Text>();
            var btn = adBtn.GetComponent<Button>();
            if (btn == null) btn = adBtn.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(ArenaRules.RequestAdBonus);
        }
        else { _bindFailed = true; Debug.LogError("[Arena] 预制体缺节点：AdBonusBtn"); }

        // 顶部：排行榜奖励（2026-10-29 深夜主人拍板：改开预制体里手摆的「排行奖励」面板，
        // 不再走通用 ShowNotice——5 档奖励行已烤在预制体里，面板自带 Dim 黑底）
        var rewardBtn = UiFind.Deep(transform, "RewardBtn");
        if (rewardBtn != null)
        {
            var btn = rewardBtn.GetComponent<Button>();
            if (btn == null) btn = rewardBtn.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(ShowRewardInfo);
        }
        else { _bindFailed = true; Debug.LogError("[Arena] 预制体缺节点：RewardBtn"); }

        // 顶部：「对战记录」按钮（主人已手摆，中文节点名）——只补 Button 与监听，
        // 排版/图一律以预制体为准（不再运行时克隆 RewardBtn：克隆会和主人手摆的那颗撞成两颗）
        var recordBtn = UiFind.Deep(transform, RecordBtnName);
        if (recordBtn != null)
        {
            var btn = recordBtn.GetComponent<Button>();
            if (btn == null) btn = recordBtn.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(ShowRecords);
        }
        else { _bindFailed = true; Debug.LogError("[Arena] 预制体缺节点：" + RecordBtnName + "（对战记录按钮）"); }

        // 竞技商店入口（2026-10-30 主人手摆 Top/竞技商店Btn）：开关「竞技商店」页
        var shopBtnT = UiFind.Deep(transform, ShopBtnName);
        if (shopBtnT != null)
        {
            var shopBtn = shopBtnT.GetComponent<Button>();
            if (shopBtn == null) shopBtn = shopBtnT.gameObject.AddComponent<Button>();
            shopBtn.onClick.AddListener(ShowShop);
        }
        else { _bindFailed = true; Debug.LogError("[Arena] 预制体缺节点：" + ShopBtnName + "（竞技商店按钮）"); }

        // 主弹窗右上 ×（预制体 CloseBtn）→ Close()。2026-10-30 主人报「点击关闭弹窗无反应」：
        // Close() 一直都在（收子面板 + 藏整弹窗），但这颗钮从来没绑过事件——补上（缺节点按铁律报错）。
        var mainCloseT = UiFind.Deep(transform, "CloseBtn");
        if (mainCloseT != null)
        {
            var mainCloseBtn = mainCloseT.GetComponent<Button>();
            if (mainCloseBtn == null) mainCloseBtn = mainCloseT.gameObject.AddComponent<Button>();
            mainCloseBtn.onClick.AddListener(Close);
        }
        else { _bindFailed = true; Debug.LogError("[Arena] 预制体缺节点：CloseBtn（主弹窗关闭钮）"); }

        // 前三领奖台（卡内同名子节点从各自卡根搜；点卡 → 详情）
        for (int r = 0; r < 3; r++)
        {
            var cardT = UiFind.Deep(transform, "Podium" + (r + 1));
            if (cardT == null)
            {
                Debug.LogError($"[Arena] 预制体缺节点：Podium{r + 1}");
                _bindFailed = true;
                continue;
            }
            _podRoot[r] = cardT.GetComponent<Image>();
            _podRank[r] = UiFind.Deep(cardT, "RankNum")?.GetComponent<Text>();
            _podHead[r] = UiFind.Deep(cardT, "HeadImg")?.GetComponent<Image>();
            _podName[r] = UiFind.Deep(cardT, "NameText")?.GetComponent<Text>();
            _podPts[r] = UiFind.Deep(cardT, "PtsText")?.GetComponent<Text>();
            var cardBtn = cardT.GetComponent<Button>();
            if (cardBtn == null) cardBtn = cardT.gameObject.AddComponent<Button>();
            if (_podRoot[r] != null) cardBtn.targetGraphic = _podRoot[r];
            int rr = r;   // 领奖台显示的是排名 r+1 的实体；详情下标运行时算
            cardBtn.onClick.AddListener(() => ShowDetailForRank(rr + 1));
        }

        // 我的排名条
        _myRankText = Bind<Text>("MyRankText");
        _myHead = Bind<Image>("MyHead");
        _myName = Bind<Text>("MyName");
        _myPtsText = Bind<Text>("MyPtsText");

        // 2026-10-29 主人已把对战美术换进预制体；ApplyArt 保留作同值保险（路径加载图 = 预制体同款，
        // 防换机/prefab 引用悬空时运行时没图），须在 BuildRows 前设好行模板图
        ApplyArt();
        BuildRows();
        BuildBotDetail();   // 详情弹窗运行时建树（点榜单行打开；不进 prefab 契约，主人改预制体不受影响）
        BuildRecords();       // 对战记录弹窗：绑定主预制体里已有的 ArenaBattleRecords 节点
        BuildRewardPanel();   // 排行奖励面板：绑定主预制体里已有的 EvacuateConfirmPopup 节点（自带 Dim 黑底）
        BuildShop();          // 竞技商店页：绑定主人手摆的「竞技商店」节点 + 克隆商品单元排格（不进 prefab）
        MoveTopBar();         // 顶部一排：对战次数/对战记录/排行奖励移出弹窗（2026-10-30 主人拍板）

        ArenaRules.Changed += Refresh;
    }

    void BuildRows()
    {
        _content.sizeDelta = new Vector2(640f, ContentH);   // BotCount 变化时运行时同步
        if (_rowTemplate.activeSelf) _rowTemplate.SetActive(false);   // 模板恒隐藏（防手滑）

        for (int i = 0; i < RowTotal; i++)
        {
            var row = Object.Instantiate(_rowTemplate, _content);
            row.name = "Row_" + i;
            row.SetActive(true);
            var rt = row.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -(i * RowStep + 4f));
                rt.sizeDelta = new Vector2(640f, RowH);
            }
            var rowImg = row.GetComponent<Image>();
            if (rowImg != null) rowImg.color = i % 2 == 0 ? _rowColorA : _rowColorB;
            _rowRoots[i] = row;

            var rankT = UiFind.Deep(row.transform, "RankText");
            _rowRank[i] = rankT != null ? rankT.GetComponent<Text>() : null;
            var headT = UiFind.Deep(row.transform, "HeadImg");
            _rowHead[i] = headT != null ? headT.GetComponent<Image>() : null;
            var nameT = UiFind.Deep(row.transform, "NameText");
            _rowName[i] = nameT != null ? nameT.GetComponent<Text>() : null;
            var ptsT = UiFind.Deep(row.transform, "PtsText");
            _rowPts[i] = ptsT != null ? ptsT.GetComponent<Text>() : null;
            var dashT = UiFind.Deep(row.transform, "DashText");
            _rowDash[i] = dashT != null ? dashT.gameObject : null;

            // 名次徽章（2026-10-29 主人拍板）：1~3 金银铜章（数字烤图，不写字）；4~10 蓝章 _0013_数字（章上写数字）；
            // 11~100 不用底图直接写数字。Row_i 名次恒为 i+1，克隆时设一次（数字文本 Refresh 里照常填）。
            var badgeT = UiFind.Deep(row.transform, "RankBadge");
            var badgeImg = badgeT != null ? badgeT.GetComponent<Image>() : null;
            int rowRank = i + 1;
            bool writeNum = rowRank >= 4;   // 1~3 数字已烤图，字不显示（防双重数字）
            if (_rowRank[i] != null)
            {
                _rowRank[i].gameObject.SetActive(writeNum);
                if (writeNum) _rowRank[i].color = rowRank <= 10 ? Color.white : _txtSub;   // 章上白字 / 无章灰字
            }
            if (badgeImg != null)
            {
                if (rowRank <= 3)
                {
                    badgeImg.enabled = true;
                    badgeImg.sprite = rowRank == 1 ? LoadArt("_0010_1")
                                    : rowRank == 2 ? LoadArt("_0011_2")
                                    : LoadArt("_0012_3");
                }
                else if (rowRank <= 10)
                {
                    badgeImg.enabled = true;
                    badgeImg.sprite = LoadArt("_0013_数字");
                }
                else
                {
                    badgeImg.enabled = false;   // 11+ 无底图（sprite 置空白模不渲染，直接关 Image）
                }
            }

            var fight = UiFind.Deep(row.transform, "FightBtn");
            if (fight == null)
            {
                Debug.LogError("[Arena] 行模板缺节点：FightBtn");
                _bindFailed = true;
                continue;
            }
            _rowFight[i] = fight.gameObject;
            var btn = fight.GetComponent<Button>();
            if (btn == null) btn = fight.gameObject.AddComponent<Button>();
            int idx = i;
            btn.onClick.AddListener(() => OnChallengeClicked(idx));

            // 行点击 → 机器人/我的档案详情（运行时补 Button，不改预制体；拖动不受影响——事件冒泡给 ScrollRect）
            if (rowImg != null) rowImg.raycastTarget = true;
            var rowBtn = row.GetComponent<Button>();
            if (rowBtn == null) rowBtn = row.AddComponent<Button>();
            rowBtn.targetGraphic = rowImg;
            rowBtn.onClick.AddListener(() => ShowDetailForRow(idx));
            // 2026-10-30 主人拍板：玩家行描边（金色 Outline，随 Refresh 里玩家行开关启停）
            var ol = row.AddComponent<Outline>();
            ol.effectColor = _rowOutlineGold;
            ol.effectDistance = new Vector2(3f, 3f);
            ol.enabled = false;
            _rowOutline[i] = ol;
        }
    }

    /// <summary>名次徽章色：1 金 / 2 银 / 3 铜 / 其余灰白（取 RarityPalette 语义，别处不私抄）。</summary>
    static Color RankColor(int rank)
    {
        if (rank == 1) return RarityPalette.Legendary;
        if (rank == 2) return RarityPalette.Common;
        if (rank == 3) return new Color(0.85f, 0.55f, 0.30f, 1f);   // 铜（Palette 无铜档，此处唯一）
        return RarityPalette.Common;
    }

    // ───────────────────────── 对战美术（2026-10-29 换图批次） ─────────────────────────
    // 主人把整套排行榜美术放在 Resources/UI/Art/对战/；这些图是团结签发的 56 字符 guid，
    // 进不了 prefab 的 sprite 引用（拖图不落盘），按项目口径走 Resources.Load 路径加载设图。
    // prefab 里只摆布局（TitleImg/TriesCapsule/RankBadge 节点的位置尺寸），图全部在这里给。

    const string ArtDir = "UI/Art/对战/";
    /// <summary>弹窗背景图（Dim 层，2026-10-30 主人拍板用「梦幻浮空城堡像素舞台」）：等比缩放铺满 + 降亮度。</summary>
    const string BackdropArtFile = "梦幻浮空城堡像素舞台";
    /// <summary>背景亮度系数（1=原图；主人 2026-10-30「亮度可以降点」）。</summary>
    const float BackdropBrightness = 0.72f;
    static readonly Dictionary<string, Sprite> ArtCache = new Dictionary<string, Sprite>();

    /// <summary>整图精灵（可选 9-slice 边框，单位像素）。缺图 LogError 不兜底。</summary>
    static Sprite LoadArt(string file, float border = 0f)
    {
        if (ArtCache.TryGetValue(file, out var hit) && hit != null) return hit;
        var tex = Resources.Load<Texture2D>(ArtDir + file);
        if (tex == null)
        {
            Debug.LogError($"[Arena] 对战美术缺失：Resources/{ArtDir}{file}（确认 png 与 .meta 都在）");
            return null;
        }
        var sp = border > 0f
            ? Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border))
            : Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        ArtCache[file] = sp;
        return sp;
    }

    /// <summary>图标精灵（Sprite 导入类型直取）。缺图 LogError 不兜底。</summary>
    static Sprite LoadIcon(string file)
    {
        if (ArtCache.TryGetValue(file, out var hit) && hit != null) return hit;
        var sp = Resources.Load<Sprite>(ArtDir + file);
        if (sp == null) Debug.LogError($"[Arena] 对战图标缺失：Resources/{ArtDir}{file}");
        else ArtCache[file] = sp;
        return sp;
    }

    /// <summary>
    /// 按示意图把对战美术铺到预制体节点上（Bind 时一次；只换图不改布局——布局在 prefab 里，主人可手调）。
    /// 映射：Panel/MyBar=底框(9-slice)、TitleImg=标题横幅（烤字，TitleText 已隐藏）、TriesCapsule=对战次数胶囊、
    /// Podium1/2/3=第1/第2/第三卡（名次已烤图，RankNum 已隐藏，运行时 tint 改白）、
    /// FightBtn=对战按钮（烤字，Label 已清空）、PtsIcon 系列=积分.png（主人 2026-10-29 新放）。
    /// </summary>
    void ApplyArt()
    {
        var panelImg = UiFind.Deep(transform, "Panel")?.GetComponent<Image>();
        if (panelImg != null) panelImg.sprite = LoadArt("_0000_底框", 40f);
        var myBarImg = UiFind.Deep(transform, "MyBar")?.GetComponent<Image>();
        if (myBarImg != null) myBarImg.sprite = LoadArt("_0000_底框", 24f);

        var titleImg = UiFind.Deep(transform, "TitleImg")?.GetComponent<Image>();
        if (titleImg != null) titleImg.sprite = LoadArt("_0007_标题");
        else Debug.LogError("[Arena] 预制体缺节点：TitleImg（标题横幅，换图批次新增）");
        var triesCap = UiFind.Deep(transform, "TriesCapsule")?.GetComponent<Image>();
        if (triesCap != null) triesCap.sprite = LoadArt("_0006_对战次数");
        else Debug.LogError("[Arena] 预制体缺节点：TriesCapsule（对战次数胶囊，换图批次新增）");

        var podArt = new[] { "_0003_第1", "_0001_第2", "_0002_第三" };
        for (int i = 0; i < 3; i++)
        {
            if (_podRoot[i] != null) _podRoot[i].sprite = LoadArt(podArt[i]);
            var podT = UiFind.Deep(transform, "Podium" + (i + 1));
            var icon = podT != null ? UiFind.Deep(podT, "PtsIcon")?.GetComponent<Image>() : null;
            if (icon != null) icon.sprite = LoadIcon("积分");
        }

        if (_rowTemplate != null)
        {
            var fightImg = UiFind.Deep(_rowTemplate.transform, "FightBtn")?.GetComponent<Image>();
            if (fightImg != null) fightImg.sprite = LoadArt("_0014_对战按钮");
            var tplIcon = UiFind.Deep(_rowTemplate.transform, "PtsIcon")?.GetComponent<Image>();
            if (tplIcon != null) tplIcon.sprite = LoadIcon("积分");
        }
        var myIcon = UiFind.Deep(transform, "MyPtsIcon")?.GetComponent<Image>();
        if (myIcon != null) myIcon.sprite = LoadIcon("积分");

        // 弹窗背景（Dim，层级在 Panel 后面）：prefab 引用已死链（对战美术 56 字符重签）→ 按路径加载
        var dimImg = UiFind.Deep(transform, "Dim")?.GetComponent<Image>();
        if (dimImg != null)
        {
            var bgTex = Resources.Load<Texture2D>(ArtDir + BackdropArtFile);
            if (bgTex == null)
                Debug.LogError($"[Arena] 弹窗背景图缺失：Resources/{ArtDir}{BackdropArtFile}");
            else
            {
                dimImg.sprite = Sprite.Create(bgTex, new Rect(0f, 0f, bgTex.width, bgTex.height), new Vector2(0.5f, 0.5f), 100f);
                dimImg.color = new Color(BackdropBrightness, BackdropBrightness, BackdropBrightness, 1f);   // 亮度降点
            }
        }
        else Debug.LogError("[Arena] 预制体缺节点：Dim（弹窗背景层）");

        // 两颗入口按钮 prefab 引用同样死链，素材目录里有现成图 → 路径补上（预制体已 preserveAspect，不变形）
        var recBtnImg = UiFind.Deep(transform, RecordBtnName)?.GetComponent<Image>();
        if (recBtnImg != null) recBtnImg.sprite = LoadArt("_0004_对战记录");
        var rewardBtnImg = UiFind.Deep(transform, "RewardBtn")?.GetComponent<Image>();
        if (rewardBtnImg != null) rewardBtnImg.sprite = LoadArt("_0005_排行奖励");
    }

    /// <summary>
    /// 背景等比缩放铺满（cover）：按父级实际矩形取 max 缩放系数——只等比、绝不拉伸变形，
    /// 多出屏幕的部分溢出裁掉。每次打开时重算（画布实际尺寸此时才有效，也兼容不同机型长宽比）。
    /// </summary>
    void FitBackdrop()
    {
        var dim = UiFind.Deep(transform, "Dim");
        var img = dim != null ? dim.GetComponent<Image>() : null;
        if (img == null || img.sprite == null) return;
        var parent = img.rectTransform.parent as RectTransform;
        if (parent == null) return;
        Canvas.ForceUpdateCanvases();   // 布局先算好，父级 rect 才是实际值
        float pw = parent.rect.width, ph = parent.rect.height;
        if (pw <= 0f || ph <= 0f) return;
        float k = Mathf.Max(pw / img.sprite.rect.width, ph / img.sprite.rect.height);
        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.sizeDelta = new Vector2(img.sprite.rect.width * k, img.sprite.rect.height * k);
    }

    // ───────────────────────── 交互 / 刷新 ─────────────────────────

    void OnChallengeClicked(int rowIdx)
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        // 【2026-10-30 排查日志】主人报「挑战无反应」三次未定位 → 链路每一步都出声（铁律 12：错误要让我看见）
        Debug.Log($"[Arena] 点「对战」row={rowIdx} 我方位次={data?.arenaPlayerPos} 可挑战={(data != null && ArenaRules.IsChallengeable(data, rowIdx))} 剩次数={(data != null ? ArenaRules.TriesLeft(data) : -1)}");
        if (data == null) return;
        if (!ArenaRules.IsChallengeable(data, rowIdx)) return;
        if (ArenaRules.TriesLeft(data) <= 0)
        {
            GlobalToastUI.Show($"今天的挑战次数用完了（每天 {ArenaRules.DailyTries} 次，看广告还能加）", true);
            return;
        }
        _pendingRowIdx = rowIdx;
        // 2026-10-29 主人拍板：选职业直接调独立界面（prefab 里的 JobSelect 节点已删）。
        // PlayerJobSelectUI 在回调前会 PlayerJobDefs.SetSelected(选中职业)，回调里凭 GetSelected() 取回。
        // 【2026-10-30 主人拍板】按钮文案=「挑战」；StaminaFree=对战不查体力（对战走次数）——
        // 不传的话体力<5 时会被冒险体力闸门拦下，表现为「点击无反应」（主人实测踩坑）。
        PlayerJobSelectUI.Show(OnArenaJobPicked, new PlayerJobSelectUI.Options
        {
            EnterLabel = "进入挑战",
            StaminaFree = true,
        });
    }

    /// <summary>PlayerJobSelectUI「进入」回调：取回刚选的职业走竞技场挑战流程。</summary>
    void OnArenaJobPicked()
    {
        OnJobPicked(PlayerJobDefs.GetSelected());
    }

    void OnJobPicked(PlayerJobId job)
    {
        int rowIdx = _pendingRowIdx;
        _pendingRowIdx = -1;
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null || rowIdx < 0)
        {
            Debug.LogError($"[Arena] 挑战回调被拦：data={(data == null ? "空" : "有")} rowIdx={rowIdx} 职业={job}（异常，报主人）");
            return;
        }
        if (!ArenaRules.IsChallengeable(data, rowIdx))
        {
            Debug.LogError($"[Arena] 挑战回调被拦：row={rowIdx} 不可挑战（我方位次 {data.arenaPlayerPos}）——点按钮时明明可挑战，异常，报主人");
            return;
        }
        if (!ArenaRules.TryConsumeChallenge()) return;   // 次数不足自带 toast，可见

        // 【2026-10-30 主人拍板「竞技场真战斗」】不再掷骰直出结果（原 ResolveChallenge/ShowResult 链已删）：
        // 快照对手上下文 → 立进本旗标 → 关弹窗（DDOL 弹窗不许带进战斗场景）→ 进战斗场景。
        // 胜负由真战斗决定，结算走战斗侧（胜利=FinishStageAfterPortalReached 统一门 / 失败=死亡链 →
        // ArenaRules.ApplyBattleOutcome），原面板 + 积分格 / 失败只有图 + 扣分 toast。
        ArenaRules.PrepareChallenge(data, rowIdx);
        // 进本标记照 AdventureUI.EnterAfterJobSelect「先全清再立标记」口径（ResetPendingFlags 在前，防被清掉）
        AdventureUI.ResetPendingFlags();
        AdventureUI.PendingArenaRowIdx = rowIdx;
        Close();
        GameSceneManager.Instance?.LoadBattleScene();
    }

    void Refresh()
    {
        if (_bindFailed) return;
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null) return;
        ArenaRules.EnsurePeriods(data);   // 幂等；保证换位链合法（老档迁移也在这里触发）

        int pos = Mathf.Clamp(data.arenaPlayerPos, 0, RowTotal - 1);
        int left = ArenaRules.TriesLeft(data);
        // 2026-10-30 主人拍板：对战次数不要字，只显示剩余数字（「对战次数」已烤在胶囊图上）；看广告 +2 累加、跨天清零回 10（口径不变）
        string myName = StoryProgress.GetPlayerName();   // 2026-10-30 主人拍板：榜单/领奖台一律用玩家起的名字（StoryProgress.GetPlayerName 唯一出口），不再写死「我」

        if (_triesText != null) _triesText.text = left.ToString();   // 只显示剩余次数（2026-10-30 主人拍板）
        if (_adBtnText != null)
            _adBtnText.text = data.arenaAdViewsToday >= ArenaRules.AdViewCapPerDay ? "已满" : $"+{ArenaRules.AdBonusTries}";

        // 我的排名条
        if (_myRankText != null) _myRankText.text = $"我的排名 第{pos + 1}名";
        if (_myName != null) _myName.text = myName;
        if (_myPtsText != null) _myPtsText.text = data.arenaPoints.ToString();   // 显示赛季积分（原误填战力估计，2026-10-30 主人拍板）
        if (_myHead != null)
        {
            var sp = MercPortraitSprites.GetHead("player");
            _myHead.sprite = sp;
            if (sp == null) Debug.LogError("[Arena] 玩家头像加载失败：player");
        }

        // 前三领奖台：排名 r 的实体 =（r-1 == pos ? 玩家 : 机器人[r-1]）；2026-10-30 修复：旧判断 idx<pos 把玩家刷满前三
        var cardColors = new[] { Color.white, Color.white, Color.white };   // 卡图自带金银铜（2026-10-29 换图后不再 tint）
        for (int r = 1; r <= 3; r++)
        {
            int idx = r - 1;
            bool isMe = idx == pos;   // 2026-10-30 修复：只有玩家自己那一名的卡显示玩家（旧判断 idx<pos 导致前三名全是「我」）
            var bot = !isMe && idx < data.arenaBots.Count ? data.arenaBots[idx] : null;
            int i = r - 1;
            if (_podRank[i] != null)
            {
                _podRank[i].text = r.ToString();
                _podRank[i].color = RankColor(r);
            }
            if (isMe)
            {
                if (_podName[i] != null) _podName[i].text = myName;
                if (_podPts[i] != null) _podPts[i].text = data.arenaPoints.ToString();
                if (_podHead[i] != null)
                {
                    var sp = MercPortraitSprites.GetHead("player");
                    _podHead[i].sprite = sp;
                    if (sp == null) Debug.LogError("[Arena] 玩家头像加载失败：player");
                }
            }
            else if (bot != null)
            {
                if (_podName[i] != null) _podName[i].text = bot.name;
                if (_podPts[i] != null) _podPts[i].text = bot.points.ToString();   // 显示赛季积分（原误填战力，2026-10-30 主人拍板）
                if (_podHead[i] != null)
                {
                    var sp = MercPortraitSprites.GetHead("H" + bot.avatar.ToString("D3"));
                    _podHead[i].sprite = sp;
                    if (sp == null) Debug.LogError("[Arena] 机器人头像加载失败：H" + bot.avatar.ToString("D3"));
                }
            }
            if (_podRoot[i] != null) _podRoot[i].color = cardColors[i];
        }

        for (int i = 0; i < RowTotal; i++)
        {
            bool isPlayer = i == pos;
            int listIdx = i < pos ? i : i - 1;   // 玩家行没有对应机器人
            var bot = (!isPlayer && listIdx >= 0 && listIdx < data.arenaBots.Count) ? data.arenaBots[listIdx] : null;
            bool on = isPlayer || bot != null;
            if (_rowRoots[i] != null) _rowRoots[i].SetActive(on);
            if (!on) continue;

            // 行底色：只在「是不是玩家行」切换时写，主人改的隔行色平时不被覆盖
            if (isPlayer != _rowIsMine[i])
            {
                _rowIsMine[i] = isPlayer;
                if (_rowRoots[i] != null)
                {
                    var img = _rowRoots[i].GetComponent<Image>();
                    if (img != null) img.color = isPlayer ? _rowMine : (i % 2 == 0 ? _rowColorA : _rowColorB);
                    if (_rowOutline[i] != null) _rowOutline[i].enabled = isPlayer;   // 描边只挂在玩家那一条（2026-10-30 主人拍板）
                }
            }

            if (_rowRank[i] != null) _rowRank[i].text = (i + 1).ToString();
            if (isPlayer)
            {
                if (_rowName[i] != null) _rowName[i].text = myName;
                if (_rowPts[i] != null) _rowPts[i].text = data.arenaPoints.ToString();
                if (_rowHead[i] != null)
                {
                    var sp = MercPortraitSprites.GetHead("player");
                    _rowHead[i].sprite = sp;
                    if (sp == null) Debug.LogError("[Arena] 玩家头像加载失败：player");
                }
            }
            else
            {
                if (_rowName[i] != null) _rowName[i].text = bot.name;
                if (_rowPts[i] != null) _rowPts[i].text = bot.points.ToString();   // 显示赛季积分（原误填战力，2026-10-30 主人拍板）
                if (_rowHead[i] != null)
                {
                    var sp = MercPortraitSprites.GetHead("H" + bot.avatar.ToString("D3"));
                    _rowHead[i].sprite = sp;
                    if (sp == null) Debug.LogError("[Arena] 机器人头像加载失败：H" + bot.avatar.ToString("D3"));
                }
            }

            // 「对战」按钮 vs 「—」互斥（参考图口径：可挑战亮按钮，其余灰杠）
            bool canFight = !isPlayer && ArenaRules.IsChallengeable(data, i);
            if (_rowFight[i] != null) _rowFight[i].SetActive(canFight);
            if (_rowDash[i] != null) _rowDash[i].SetActive(!canFight);
        }

        RefreshShop();   // 商店页余额/价格/清零横幅随 Changed 同步（积分变化/购买后即时刷新）
    }

    /// <summary>把玩家行滚到视口中间。</summary>
    void ScrollToPlayer()
    {
        if (_scroll == null) return;
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null) return;
        int pos = Mathf.Clamp(data.arenaPlayerPos, 0, RowTotal - 1);
        float offset = pos * RowStep + RowH * 0.5f - ViewportH * 0.5f;   // 玩家行中心对齐视口中心
        float max = Mathf.Max(1f, ContentH - ViewportH);
        _scroll.verticalNormalizedPosition = Mathf.Clamp01(1f - offset / max);
    }

    /// <summary>排行奖励面板（2026-10-29 深夜主人拍板：面板节点已搭进主预制体，节点名沿用通用弹窗皮
    /// EvacuateConfirmPopup——自带 Root/Dim 黑底(0.6) + 5 档奖励行 + 确定钮）。
    /// 档位数值真源在 ArenaRules（已按预制体档位同值更新）；面板文案/排版一律以预制体为准，代码只管开关。</summary>
    void ShowRewardInfo()
    {
        if (_rewardRoot == null) return;   // 缺节点在 BuildRewardPanel 已 LogError，这里不重复也不兜底
        _rewardRoot.SetActive(true);
    }

    /// <summary>关闭排行奖励面板（确定钮 / 点黑底空白都走这里）。</summary>
    void CloseReward()
    {
        if (_rewardRoot != null) _rewardRoot.SetActive(false);
    }

    // ───────────────────────── 竞技商店（2026-10-30 积分版） ─────────────────────────
    // 页面结构（主人手摆，名字/层级可改、美术排版保持原样）：
    // 竞技商店(全屏页) > RecordsBox(689×739) > 底框 / RTitle / 积分icon>Text(余额) / RCloseBtn / 商品(示例单元)
    // 商品单元收敛为「图标+积分价格」：克隆时隐藏 横条 / 商品名 (1) / 购买 三件多余节点（需求①）。
    // 货架 8 件 = ArenaShopGoods.AllGoods（1 弓 + 3 佣兵 + 1 金币包 + 3 徽记包），4 列 × 2 行克隆排格。

    /// <summary>格子布局：4 列 × 2 行（RecordsBox 689 宽内 165 步进不溢出，行位避开顶部标题区/余额行）。</summary>
    const int ShopCols = 4;
    const float ShopStepX = 165f;
    const float ShopStepY = 140f;
    const float ShopGridY0 = 40f;    // 首行中心 y（RecordsBox 中心锚；第二行 -100）

    void BuildShop()
    {
        if (_shopRoot != null) return;
        var pageT = UiFind.Deep(transform, ShopPageName);
        if (pageT == null)
        {
            Debug.LogError("[Arena] 预制体缺节点：" + ShopPageName + "（竞技商店页）");
            _bindFailed = true;
            return;
        }
        _shopRoot = pageT.gameObject;

        // 页根自带 Button（点空白关页，同对战记录面板 Dim 口径）——只在实例上接线，不改预制体
        var pageBtn = pageT.GetComponent<Button>();
        if (pageBtn != null) pageBtn.onClick.AddListener(CloseShop);

        var boxT = UiFind.Deep(pageT, "RecordsBox");
        if (boxT == null)
        {
            Debug.LogError("[Arena] 商店页缺节点：RecordsBox（货架容器）");
            _bindFailed = true;
            return;
        }
        _shopBox = (RectTransform)boxT;

        // 关闭钮：页内搜索（对战记录面板也有同名 RCloseBtn，不能全树搜）
        var closeT = UiFind.Deep(pageT, "RCloseBtn");
        var closeBtn = closeT != null ? closeT.GetComponent<Button>() : null;
        if (closeBtn != null) closeBtn.onClick.AddListener(CloseShop);
        else { Debug.LogError("[Arena] 商店页缺节点：RCloseBtn（或无 Button）"); _bindFailed = true; }

        // 余额：积分icon 下的数字文本（订阅 ArenaRules.Changed → Refresh → RefreshShop 即时刷新）
        var balT = UiFind.Deep(pageT, "积分icon");
        _shopBalanceText = balT != null ? balT.GetComponentInChildren<Text>(true) : null;
        if (_shopBalanceText == null)
        {
            Debug.LogError("[Arena] 商店页缺节点：积分icon/Text（积分余额显示）");
            _bindFailed = true;
        }

        BuildShopBanner();
        BuildShopUnits();
        _shopRoot.SetActive(false);   // 运行时接线建树完成即藏（红线：Build 末尾 SetActive(false)）
    }

    /// <summary>赛季末清零横幅（§四.5：最后 3 天挂一句文案；运行时建树，不动 prefab）。</summary>
    void BuildShopBanner()
    {
        var t = UiFactory.NewTextColored(_shopBox, "SeasonClearBanner", "", 24, TextAnchor.MiddleCenter, _txtGold);
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -196f);
        rt.sizeDelta = new Vector2(560f, 34f);
        t.gameObject.SetActive(false);
        _shopBannerText = t;
    }

    void BuildShopUnits()
    {
        if (_shopUnitTemplate != null || _shopBox == null) return;
        var unitT = UiFind.Deep(_shopBox, "商品");
        if (unitT == null)
        {
            Debug.LogError("[Arena] 商店页缺节点：商品（示例单元，克隆源）");
            _bindFailed = true;
            return;
        }
        _shopUnitTemplate = unitT.gameObject;
        _shopUnitTemplate.SetActive(false);   // 模板恒隐藏（防手滑；同 RowTemplate 口径）

        for (int i = 0; i < ArenaShopGoods.AllGoods.Length; i++)
        {
            var good = ArenaShopGoods.AllGoods[i];
            var row = Object.Instantiate(_shopUnitTemplate, _shopBox);
            row.name = "ShopUnit_" + good.id;
            row.SetActive(true);
            var rt = (RectTransform)row.transform;
            int col = i % ShopCols;
            int r = i / ShopCols;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(
                ((col - (ShopCols - 1) * 0.5f) * ShopStepX),
                ShopGridY0 - r * ShopStepY);
            rt.localScale = Vector3.one;
            _shopUnits.Add(row);

            // 需求①：货架只放「图标 + 积分价格」——示例单元里多余的 横条/商品名 (1)/购买 隐藏
            HideChild(row.transform, "横条");
            HideChild(row.transform, "商品名 (1)");
            HideChild(row.transform, "购买");

            // 需求②（2026-10-30 16:55 主人拍板「已经有的就按有的效果来」）：prefab 已给图 → 原样用不覆盖；
            // 现状未给（空 Image）→ 按说明来：佣兵用 MercHireSession.LoadRarityFrame（头像框档），
            // 其余（弓/金币/徽记）按稀有度选同套底框。上一轮用的 LoadTavernCardFrame（204×248 卡框）弃用。
            var frameImg = UiFind.Deep(row.transform, "商品图框")?.GetComponent<Image>();
            if (frameImg != null && frameImg.sprite == null)   // 有图不覆盖（prefab 摆的优先）
            {
                var frameSp = MercHireSession.LoadRarityFrame(good.frameRarity);
                if (frameSp == null)
                    Debug.LogError($"[Arena] 稀有度底框缺失：UI/Recruit/frame_*（{good.frameRarity}，商品 {good.id}，底框留空可见）");
                else
                {
                    frameImg.sprite = frameSp;
                    frameImg.preserveAspect = true;
                }
            }

            // 图标内容：弓=模板图标 / 佣兵=头像 / 金币=icon_gold / 徽记=职业徽记（缺图 LogError 不兜底）
            var iconImg = UiFind.Deep(row.transform, "icon")?.GetComponent<Image>();
            if (iconImg != null)
            {
                var sp = ShopIconFor(good);
                if (sp == null)
                    Debug.LogError($"[Arena] 商品图标缺失：{good.id}（不兜底，格内留灰块可见）");
                else
                {
                    iconImg.sprite = sp;
                    iconImg.preserveAspect = true;
                }
            }

            // 点单元 → 购买/取消确认框（走 StaminaConfirmPopup 通用弹窗，禁手搓第二套）
            var btn = row.GetComponent<Button>();
            if (btn == null) btn = row.AddComponent<Button>();
            var captured = good;
            btn.onClick.AddListener(() => OnShopUnitClicked(captured));
        }
    }

    static void HideChild(Transform root, string name)
    {
        var t = UiFind.Deep(root, name);
        if (t != null) t.gameObject.SetActive(false);
    }

    /// <summary>商品图标按货架类型取图（缺图返回 null 由调用方 LogError）。</summary>
    static Sprite ShopIconFor(ArenaShopGoods.Good good)
    {
        switch (good.kind)
        {
            case ArenaShopGoods.GoodKind.Bow:
                return ArenaShopGoods.BowIcon();
            case ArenaShopGoods.GoodKind.Merc:
            {
                var sp = MercPortraitSprites.GetHead(good.hireId);
                if (sp == null) Debug.LogError("[Arena] 佣兵头像加载失败：" + good.hireId);
                return sp;
            }
            case ArenaShopGoods.GoodKind.Gold:
            {
                var sp = CommonIconSprites.Load("icon_gold");
                if (sp == null) Debug.LogError("[Arena] 金币图标缺失：CommonIconSprites/icon_gold");
                return sp;
            }
            case ArenaShopGoods.GoodKind.Badge:
            {
                var sp = MercGrowSprites.LoadJobBadge(good.badgeJobKey, good.frameRarity);
                if (sp == null) Debug.LogError($"[Arena] 职业徽记加载失败：{good.badgeJobKey}/{good.badgeTier}");
                return sp;
            }
            default:
                return null;
        }
    }

    /// <summary>点商品 → 确认弹窗（标题=商品名；正文=价格/效果/已拥有说明）。置灰两档（键仍可点，点击 toast 不入账）：
    /// ① 积分不足 → toast「积分不足」；② 佣兵满星（2026-10-30 16:45 拍板「佣兵满星购买按钮变灰」）
    /// → toast「已满星，先拿碎片去升星吧」（TryBuy 既有拦截，不扣分）。</summary>
    void OnShopUnitClicked(ArenaShopGoods.Good good)
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null) return;
        int price = ArenaShopGoods.PriceOf(data, good);
        bool afford = data.arenaPoints >= price;
        // 需求⑥：佣兵满星 → 购买键同积分不足样式置灰（判定口径与 TryBuy 拦截同源：OutStarOf ≥ MaxOutStar）
        bool fullStar = good.kind == ArenaShopGoods.GoodKind.Merc
            && ArenaShopGoods.IsOwned(data, good)
            && MercStarSystem.OutStarOf(good.hireId) >= MercStarDefs.MaxOutStar;
        // 弹窗正文追加周限进度（周限商品），让玩家知道还剩几次
        string body = good.BuildConfirmBody(data);
        if (good.weeklyLimit > 0)
            body += $"\n本周已购 {ArenaShopGoods.WeekBuyCount(data, good.id)}/{good.weeklyLimit} 次";
        StaminaConfirmPopup.Show(good.DisplayName, body, "购买", () =>
        {
            var d2 = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
            if (d2 == null) return;
            if (d2.arenaPoints < ArenaShopGoods.PriceOf(d2, good))
            {
                GlobalToastUI.Show("积分不足");   // 置灰键被点：只提示不入账（余额没动）
                return;
            }
            ArenaShopGoods.TryBuy(good.id, out string msg);   // 满星拦截在 TryBuy 内（toast 已有，不扣分）
            if (!string.IsNullOrEmpty(msg)) GlobalToastUI.Show(msg, true);
            RefreshShop();
        });
        if (!afford || fullStar) StaminaConfirmPopup.SetOkGray(true);
    }

    void ShowShop()
    {
        if (_shopRoot == null) return;
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        ArenaRules.EnsurePeriods(data);   // 幂等：跨天/跨月对齐（顺带注册逐风长弓模板）
        _shopRoot.SetActive(true);
        _shopRoot.transform.SetAsLastSibling();
        RefreshShop();
    }

    void CloseShop()
    {
        if (_shopRoot != null) _shopRoot.SetActive(false);
    }

    /// <summary>商店页刷新：余额（订阅 Changed 即时刷新）+ 清零横幅 + 各格价格/买断变灰。</summary>
    void RefreshShop()
    {
        if (_shopRoot == null) return;
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null) return;
        ArenaShopGoods.EnsureWeek(data);

        if (_shopBalanceText != null) _shopBalanceText.text = data.arenaPoints.ToString();

        // 赛季末清零横幅：最后 3 天挂「本赛季积分 X 日后清零」（§四.5）
        if (_shopBannerText != null)
        {
            int left = ArenaShopGoods.DaysBeforeSeasonClear();
            bool show = left <= 3;
            _shopBannerText.gameObject.SetActive(show);
            if (show)
                _shopBannerText.text = left <= 0
                    ? "赛季最后一天！积分明天清零，速花！"
                    : $"本赛季积分 {left} 日后清零，速花！";
        }

        for (int i = 0; i < _shopUnits.Count && i < ArenaShopGoods.AllGoods.Length; i++)
        {
            var good = ArenaShopGoods.AllGoods[i];
            var unit = _shopUnits[i];
            var priceT = UiFind.Deep(unit.transform, "商品名")?.GetComponent<Text>();
            if (priceT == null) continue;
            bool owned = ArenaShopGoods.IsOwned(data, good);
            bool soldOut = owned && good.repurchasePrice <= 0;   // 买断（弓）→ 变灰「已拥有」
            priceT.text = soldOut ? "已拥有" : ArenaShopGoods.PriceOf(data, good).ToString();
            priceT.color = soldOut ? new Color(0.6f, 0.6f, 0.6f, 1f) : Color.white;
        }
    }

    /// <summary>领奖台点卡 → 详情：排名 rank 的实体（rank-1 &lt; 玩家位 = 玩家自己，否则机器人）。</summary>
    void ShowDetailForRank(int rank)
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null) return;
        int pos = Mathf.Clamp(data.arenaPlayerPos, 0, ArenaRules.BotCount);
        int idx = rank - 1;
        if (idx < pos) { ShowMyDetail(data, pos); return; }
        if (idx < data.arenaBots.Count) ShowBotDetail(data, data.arenaBots[idx], idx);
    }

    // ───────────────────────── 档案详情弹窗（运行时建树，不进 prefab 契约） ─────────────────────────

    /// <summary>点榜单行：玩家行弹我的档案（战力估计+积分），机器人行弹机器人档案（战力+佣兵/技能/装备）。</summary>
    void ShowDetailForRow(int rowIdx)
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null) return;
        int pos = Mathf.Clamp(data.arenaPlayerPos, 0, ArenaRules.BotCount);
        if (rowIdx == pos) { ShowMyDetail(data, pos); return; }
        int listIdx = rowIdx < pos ? rowIdx : rowIdx - 1;
        if (listIdx < 0 || listIdx >= data.arenaBots.Count) return;
        ShowBotDetail(data, data.arenaBots[listIdx], listIdx);
    }

    void ShowBotDetail(SaveData data, ArenaBotEntry bot, int listIdx)
    {
        if (_detailRoot == null || bot == null) return;
        int rank = ArenaRules.RankOfIndex(data, listIdx);
        if (_dTitle != null) _dTitle.text = $"{bot.name} · 第{rank}名";
        if (_dPower != null) _dPower.text = $"战力 {ArenaRules.GetBotPower(data, listIdx)}";
        if (_dPoints != null) _dPoints.text = $"赛季积分 {bot.points}";
        FillLoadout(ArenaRules.BuildBotLoadout(bot));
        _detailRoot.SetActive(true);
    }

    void ShowMyDetail(SaveData data, int pos)
    {
        if (_detailRoot == null) return;
        if (_dTitle != null) _dTitle.text = $"{StoryProgress.GetPlayerName()} · 第{pos + 1}名";   // 2026-10-30 主人拍板：用玩家起的名字
        if (_dPower != null) _dPower.text = $"战力 {ArenaRules.GetPlayerPower(data)}";
        if (_dPoints != null) _dPoints.text = $"赛季积分 {data.arenaPoints}";
        // 玩家没有假阵容；给一条估计机制说明（主人口径「战力根据输赢情况调整」要让玩家看见）
        ClearDetailRows();
        var tip = UiFactory.NewTextColored(_detailRows, "MyTip", "你的战力随输赢浮动：\n赢一把 +5% · 输一把 -4% —— 打赢强敌涨得快",
            24, TextAnchor.MiddleLeft, _txtSub);
        SetRT(tip.rectTransform, V2(0.5f, 1f), V2(0.5f, 1f), V2(0f, 1f), V2(-244f, -8f), V2(488f, 80f));
        _detailRoot.SetActive(true);
    }

    void CloseDetail()
    {
        if (_detailRoot != null) _detailRoot.SetActive(false);
    }

    /// <summary>详情弹窗建树（一次性；UiFactory 真源建 Image/Text，字体 GameFonts）。</summary>
    void BuildBotDetail()
    {
        if (_detailRoot != null) return;

        var dim = UiFactory.NewImage(transform, "BotDetail", new Color(0f, 0f, 0f, 0.66f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;   // 挡住下层点击
        _detailRoot = dim.gameObject;
        var dimBtn = dim.gameObject.AddComponent<Button>();
        dimBtn.targetGraphic = dim;
        dimBtn.onClick.AddListener(CloseDetail);   // 点空白关闭

        var box = UiFactory.NewImage(dim.transform, "DetailBox", _panelColor);
        SetRT(box.rectTransform, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0f, 0f), V2(560f, 860f));

        var close = UiFactory.NewImage(box.transform, "DCloseBtn", _btnSlate);
        SetRT(close.rectTransform, V2(0.5f, 1f), V2(0.5f, 1f), V2(0.5f, 0.5f), V2(240f, -36f), V2(56f, 56f));
        close.raycastTarget = true;
        var closeBtn = close.gameObject.AddComponent<Button>();
        closeBtn.targetGraphic = close;
        closeBtn.onClick.AddListener(CloseDetail);
        var closeLabel = UiFactory.NewText(close.transform, "Label", "X", 24, TextAnchor.MiddleCenter);
        Stretch(closeLabel.rectTransform);

        _dTitle = UiFactory.NewText(box.transform, "DTitle", "", 24, TextAnchor.MiddleCenter);
        SetRT(_dTitle.rectTransform, V2(0.5f, 1f), V2(0.5f, 1f), V2(0.5f, 0.5f), V2(0f, -70f), V2(460f, 40f));

        _dPower = UiFactory.NewText(box.transform, "DPower", "", 48, TextAnchor.MiddleCenter);
        _dPower.color = _txtGold;
        SetRT(_dPower.rectTransform, V2(0.5f, 1f), V2(0.5f, 1f), V2(0.5f, 0.5f), V2(0f, -135f), V2(460f, 70f));

        _dPoints = UiFactory.NewText(box.transform, "DPoints", "", 24, TextAnchor.MiddleCenter);
        _dPoints.color = _txtSub;
        SetRT(_dPoints.rectTransform, V2(0.5f, 1f), V2(0.5f, 1f), V2(0.5f, 0.5f), V2(0f, -185f), V2(460f, 34f));

        var rows = new GameObject("DetailRows", typeof(RectTransform));
        rows.transform.SetParent(box.transform, false);
        // 标题区（名字/战力/积分）占到 -200，行容器从 -210 起往下，避免叠印
        SetRT(rows.GetComponent<RectTransform>(), V2(0.5f, 1f), V2(0.5f, 1f), V2(0.5f, 1f), V2(0f, -210f), V2(560f, 640f));
        _detailRows = rows.transform;

        _detailRoot.SetActive(false);   // 运行时建树弹层，建完即藏
    }

    /// <summary>渲染阵容档案：按 section 分段（0 佣兵带头像 / 1 技能 / 2 装备），颜色走 RarityPalette。
    /// ⚠ LoadoutEntry 嵌套在 ArenaRules 类里，外部引用必须带前缀（裸名字会 CS0246）。</summary>
    void FillLoadout(List<ArenaRules.LoadoutEntry> entries)
    {
        ClearDetailRows();
        if (entries == null || entries.Count == 0) return;

        float y = -8f;
        int lastSection = -1;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.section != lastSection)
            {
                lastSection = e.section;
                var secT = UiFactory.NewText(_detailRows, "Sec" + i, SectionName(e.section), 24, TextAnchor.MiddleLeft);
                secT.color = _txtGold;
                SetRT(secT.rectTransform, V2(0.5f, 1f), V2(0.5f, 1f), V2(0f, 1f), V2(-244f, y), V2(480f, 34f));
                y -= 42f;
            }
            if (e.iconId > 0)
            {
                var img = UiFactory.NewImage(_detailRows, "Merc" + i, Color.white);
                img.preserveAspect = true;
                SetRT(img.rectTransform, V2(0.5f, 1f), V2(0.5f, 1f), V2(0f, 1f), V2(-214f, y - 24f), V2(44f, 44f));
                var sp = MercPortraitSprites.GetHead("H" + e.iconId.ToString("D3"));
                if (sp == null) Debug.LogError("[Arena] 详情佣兵头像加载失败：H" + e.iconId.ToString("D3"));
                else img.sprite = sp;

                var t = UiFactory.NewTextColored(_detailRows, "MercT" + i, e.label, 24, TextAnchor.MiddleLeft, e.color);
                SetRT(t.rectTransform, V2(0.5f, 1f), V2(0.5f, 1f), V2(0f, 1f), V2(-184f, y - 24f), V2(380f, 32f));
                y -= 48f;
            }
            else
            {
                var t = UiFactory.NewTextColored(_detailRows, "Item" + i, e.label, 24, TextAnchor.MiddleLeft, e.color);
                SetRT(t.rectTransform, V2(0.5f, 1f), V2(0.5f, 1f), V2(0f, 1f), V2(-244f, y - 15f), V2(480f, 30f));
                y -= 30f;
            }
        }
    }

    /// <summary>清空详情行容器（重建前调用）。</summary>
    void ClearDetailRows()
    {
        if (_detailRows == null) return;
        for (int i = _detailRows.childCount - 1; i >= 0; i--)
            Destroy(_detailRows.GetChild(i).gameObject);
    }

    static string SectionName(int section)
        => section == 0 ? "佣兵阵容" : section == 1 ? "技能" : "装备";

    // ───────────────────────── 对战记录（2026-10-29 晚主人拍板新增） ─────────────────────────
    // 只记 对手头像/名字/胜败/分值/时间（数据真源 SaveData.arenaBattleRecords，写入在 ArenaRules.ApplyBattleOutcome）。
    // 2026-10-29 深夜主人拍板：入口按钮、记录弹窗、排行奖励面板三样都手摆进主预制体，代码只接线+填行。

    /// <summary>行间距步进（行高/行宽以预制体 RecRowTemplate 为准，代码只按步进摆行）。</summary>
    const float RecRowStep = 72f;

    /// <summary>胜/败语义色（截图口径：胜=绿、败=红；分值同色）。</summary>
    readonly Color _recWin = new Color(0.24f, 0.72f, 0.38f, 1f);
    readonly Color _recLose = new Color(0.86f, 0.32f, 0.32f, 1f);

    /// <summary>
    /// 记录弹窗（2026-10-29 深夜主人拍板：面板已整块搭进主预制体，节点名 ArenaBattleRecords）。
    /// ⚠ 不再 Resources.Load 独立预制体——主预制体里已自带一份，再实例化会同时存在两个记录弹窗。
    /// 结构/排版以预制体为准；底框图是 56 字符 guid 美术进不了 prefab 引用 → 代码按路径设（同 ApplyArt 口径）。
    /// </summary>
    void BuildRecords()
    {
        if (_recordsRoot != null) return;
        var root = UiFind.Deep(transform, RecordsNodeName);
        if (root == null)
        {
            Debug.LogError("[Arena] 预制体缺节点：" + RecordsNodeName + "（对战记录面板）");
            _bindFailed = true;
            return;
        }
        _recordsRoot = root.gameObject;

        var dimBtn = root.GetComponent<Button>();
        if (dimBtn != null) dimBtn.onClick.AddListener(CloseRecords);   // 点暗罩空白关闭

        var box = UiFind.Deep(root.transform, "RecordsBox")?.GetComponent<Image>();
        if (box != null)
        {
            var boxSp = LoadArt("_0000_底框", 40f);   // 与主面板同款底框（9-slice）
            if (boxSp != null)
            {
                box.sprite = boxSp;
                box.type = Image.Type.Sliced;
            }
        }
        else Debug.LogError("[Arena] 记录弹窗预制体缺节点：RecordsBox");

        var close = UiFind.Deep(root.transform, "RCloseBtn");
        var closeBtn = close != null ? close.GetComponent<Button>() : null;
        if (closeBtn != null) closeBtn.onClick.AddListener(CloseRecords);
        else Debug.LogError("[Arena] 记录弹窗预制体缺节点：RCloseBtn（或无 Button）");

        var viewport = UiFind.Deep(root.transform, "RecViewport");
        if (viewport != null && viewport.GetComponent<RectMask2D>() == null)
            viewport.gameObject.AddComponent<RectMask2D>();   // 裁剪遮罩运行时补挂（幂等）
        var scroll = viewport != null ? viewport.GetComponent<ScrollRect>() : null;
        _recordContent = UiFind.Deep(root.transform, "RecContent") as RectTransform;
        if (scroll != null && _recordContent != null)
        {
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 15f;
            scroll.viewport = viewport as RectTransform;
            scroll.content = _recordContent;
        }
        else Debug.LogError("[Arena] 记录弹窗预制体缺 RecViewport/ScrollRect/RecContent");

        _recordsRoot.SetActive(false);   // 建完即藏
    }

    /// <summary>
    /// 排行奖励面板（2026-10-29 深夜主人拍板：节点已搭进主预制体，节点名沿用通用弹窗皮 EvacuateConfirmPopup）。
    /// 面板自带 Root/Dim 黑底(0.6) + Title「排行奖励」+ 5 档奖励行（RowTemplate1~5）+ 确定钮；
    /// 档位数值真源在 ArenaRules（已按预制体档位同值更新），代码只管接线与开关，不动预制体排版与文案。
    /// </summary>
    void BuildRewardPanel()
    {
        var root = UiFind.Deep(transform, RewardPanelName);
        if (root == null)
        {
            Debug.LogError("[Arena] 预制体缺节点：" + RewardPanelName + "（排行奖励面板）");
            _bindFailed = true;
            return;
        }
        _rewardRoot = root.gameObject;

        // 【2026-10-30 主人报「黑底没生效」· 根因与修法】
        // 这颗节点拖进主预制体后是 anchor(0.5,0.5) + sizeDelta(0,0) → 它自己就是 0×0；
        // 它下面 Root / Dim 都是 stretch(0,0~1,1)，而 stretch 是相对父级算的，父是 0×0
        // → 整条链算出来 0 像素，Dim 的 (0,0,0,0.6) 配得再对也看不见——改颜色是治不好的。
        // 运行时四边贴父级即可（唯一出口 UiLayoutStretch.ApplyFillScreen，改锚点不动 .prefab）。
        UiLayoutStretch.ApplyFillScreen(root as RectTransform);

        // 奖励面板皮与 5 行行条的 prefab 精灵引用已死链（对战美术 56 字符重签）→ 照 BuildRecords 口径路径补图
        var rPanelImg = UiFind.Deep(root.transform, "Panel")?.GetComponent<Image>();
        if (rPanelImg != null)
        {
            var panelSp = LoadArt("_0000_底框", 40f);
            if (panelSp != null) { rPanelImg.sprite = panelSp; rPanelImg.type = Image.Type.Sliced; }
        }
        var rowStripSp = LoadArt("_0008_条2", 10f);
        for (int ri = 1; ri <= 5; ri++)
        {
            var rowImg = UiFind.Deep(root.transform, "RowTemplate" + ri)?.GetComponent<Image>();
            if (rowImg != null && rowStripSp != null) { rowImg.sprite = rowStripSp; rowImg.type = Image.Type.Sliced; }
        }

        // 第 1 名行：数值走 ArenaRules（与预制体档位同值），末尾补当月冠军武器名——
        // 武器每月轮换，写死在预制体没意义（主人 2026-10-30 拍板「可以加个武器名」）。
        var row1 = UiFind.Deep(root, "RowTemplate1");
        var row1Txt = row1 != null ? UiFind.Deep(row1, "NameText")?.GetComponent<Text>() : null;
        if (row1Txt != null)
            row1Txt.text = $"金币×{ArenaRules.SeasonGoldRank1} + 钻石×{ArenaRules.SeasonDiamondRank1} + "
                           + ArenaSeasonWeapons.WeaponName(ArenaSeasonWeapons.CurrentSeasonWeaponId());
        else
            Debug.LogError("[Arena] 排行奖励面板缺节点：RowTemplate1/NameText（冠军武器名没地方写）");

        // 确定钮（面板沿用弹窗皮的 EvacuateButton 节点名）
        var ok = UiFind.Deep(root, "EvacuateButton");
        var okBtn = ok != null ? ok.GetComponent<Button>() : null;
        if (okBtn != null) okBtn.onClick.AddListener(CloseReward);
        else Debug.LogError("[Arena] 排行奖励面板缺节点：EvacuateButton（确定钮，或无 Button）");

        // 黑底（Dim）：主人要的「黑底」就是它——预制体里已是 0,0,0,0.6 全屏遮罩；点空白关闭
        var dim = UiFind.Deep(root, "Dim");
        if (dim == null)
        {
            Debug.LogError("[Arena] 排行奖励面板缺节点：Dim（黑底遮罩）");
        }
        else
        {
            var dimBtn = dim.GetComponent<Button>();
            if (dimBtn == null) dimBtn = dim.gameObject.AddComponent<Button>();   // 只在实例上补，不改预制体
            var dimImg = dim.GetComponent<Image>();
            if (dimImg != null) dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(CloseReward);
        }

        _rewardRoot.SetActive(false);   // 建完即藏
    }

    /// <summary>打开记录弹窗并按存档重建行（新→旧；行 = 预制体 RecRowTemplate 克隆，排版以预制体为准）。</summary>
    void ShowRecords()
    {
        if (_recordsRoot == null || _recordContent == null) return;
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        var list = data?.arenaBattleRecords;

        for (int i = _recordContent.childCount - 1; i >= 0; i--)
        {
            var child = _recordContent.GetChild(i);
            if (child.name == "RecRowTemplate") continue;   // 模板不删（克隆源）
            Destroy(child.gameObject);
        }

        var rowTmpl = UiFind.Deep(_recordContent, "RecRowTemplate");
        if (rowTmpl == null)
        {
            Debug.LogError("[Arena] 记录弹窗预制体缺节点：RecRowTemplate（记录行不出）");
            _recordsRoot.SetActive(true);
            return;
        }
        // 模板恒隐藏：主预制体里这行是主人手摆的「示例」（对手名字 / 胜 / +10 / 2026-10-29 21:00），
        // 编辑器里开着是为了看排版，运行时它只是克隆源——不隐藏会永远多出一条假记录
        // （同 BuildRows 里 RowTemplate 的口径）。
        rowTmpl.gameObject.SetActive(false);
        var rowRt = (RectTransform)rowTmpl;
        float rowW = rowRt.sizeDelta.x;

        int n = list != null ? list.Count : 0;
        _recordContent.sizeDelta = new Vector2(rowW, Mathf.Max(1, n) * RecRowStep);
        if (n == 0)
        {
            var empty = UiFactory.NewTextColored(_recordContent, "Empty", "还没有对战记录，先去打一场吧！", 24,
                TextAnchor.MiddleCenter, _txtSub);
            SetRT(empty.rectTransform, V2(0.5f, 1f), V2(0.5f, 1f), V2(0.5f, 1f), V2(0f, -100f), V2(rowW, 40f));
            _recordsRoot.SetActive(true);
            return;
        }

        var rowStrip = LoadArt("_0008_条2", 10f);   // 深蓝圆角条（对战美术包）；条1 是素材拼图大图不可用作行底
        for (int i = 0; i < n; i++)
        {
            var rec = list[i];
            if (rec == null) continue;

            var row = Object.Instantiate(rowTmpl, _recordContent).GetComponent<RectTransform>();
            row.name = "Rec_" + i;
            row.anchoredPosition = new Vector2(0f, -(i * RecRowStep + 4f));
            row.gameObject.SetActive(true);

            var rowImg = row.GetComponent<Image>();
            if (rowImg != null && rowStrip != null)
            {
                rowImg.sprite = rowStrip;
                rowImg.type = Image.Type.Sliced;   // 克隆图运行时设 sprite，切片吃 9-slice
            }

            var head = UiFind.Deep(row, "Head")?.GetComponent<Image>();
            if (head != null)
            {
                var headSp = MercPortraitSprites.GetHead("H" + rec.avatar.ToString("D3"));
                if (headSp == null) Debug.LogError("[Arena] 记录头像加载失败：H" + rec.avatar.ToString("D3"));
                else head.sprite = headSp;
            }
            var name = UiFind.Deep(row, "Name")?.GetComponent<Text>();
            if (name != null) name.text = rec.name;

            var badge = UiFind.Deep(row, "Badge")?.GetComponent<Image>();
            if (badge != null) badge.color = rec.won ? _recWin : _recLose;
            var badgeText = badge != null ? badge.GetComponentInChildren<Text>(true) : null;
            if (badgeText != null) badgeText.text = rec.won ? "胜" : "败";

            var score = UiFind.Deep(row, "Pts")?.GetComponent<Text>();
            if (score != null)
            {
                score.text = rec.points >= 0 ? "+" + rec.points : rec.points.ToString();
                score.color = rec.won ? _recWin : _recLose;   // 带符号分值，胜绿/败红
            }

            var timeT = UiFind.Deep(row, "Time")?.GetComponent<Text>();
            if (timeT != null)
                timeT.text = System.DateTimeOffset.FromUnixTimeSeconds(rec.time).LocalDateTime.ToString("yyyy-MM-dd HH:mm");
        }

        _recordsRoot.SetActive(true);
    }

    void CloseRecords()
    {
        if (_recordsRoot != null) _recordsRoot.SetActive(false);
    }

    // ───────────────────────── 建树小工具（详情弹窗运行时建树用） ─────────────────────────

    static Vector2 V2(float x, float y) => new Vector2(x, y);

    static void SetRT(RectTransform rt, Vector2 amin, Vector2 amax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = amin;
        rt.anchorMax = amax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
