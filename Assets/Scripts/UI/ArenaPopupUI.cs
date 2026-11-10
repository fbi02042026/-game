using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 竞技场弹窗 · 换位制（2026-10-29 主人拍板改版）。
/// 展示层走预制体 Assets/Resources/Prefabs/Town/ArenaPopupUI.prefab
///（菜单 Tools/竞技场/生成竞技场弹窗预制体 一次性生成；主人手改后【禁止重跑生成器】）。
/// 运行时只按节点名绑定（UiFind.Deep）+ 克隆榜单行 + 填数据/接事件；
/// 主人改排版/图标/按钮色都会保留。条的图/色/九宫格一律以预制体为准
///（2026-11-01 主人拍板「直接用预制体里做好的九宫格选中条，不要改里面条的数值」）：
/// 玩家行只把 sprite 引用换成 MyBar 上主人摆的选中条（同为 Sliced，随框拉伸不变形），
/// 其余行用模板原条；Image 的颜色/类型/几何代码一概不碰，适配靠整窗缩放（Canvas 统一管）。
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
    /// <summary>视口实际高（= 预制体 List 高，2026-10-30 框体 1000→1080 时 550→630；ScrollToPlayer 居中用）。</summary>
    const float ViewportH = 630f;
    // 行高/步进 2026-10-30 起运行时从预制体 RowTemplate 实测（主人拍板「运行时以预制体几何为准」）：
    // 原 RowH=56/RowStep=64 写死，把主人 prefab 里调的行几何压扁——即「预制体看的效果运行时不是这样」。
    // 步进 = 行高 + 8（沿用原 56/64 的 8px 行间距节奏）；字段默认值仅作模板缺失时的兜底。
    float _rowH = 56f;
    float _rowStep = 64f;
    float _contentH;

    // ── 榜单行控件（行号 0=榜首 … 100=末位；玩家行下标=arenaPlayerPos）──
    readonly GameObject[] _rowRoots = new GameObject[RowTotal];
    readonly Text[] _rowRank = new Text[RowTotal];
    readonly Image[] _rowHead = new Image[RowTotal];
    readonly Text[] _rowName = new Text[RowTotal];
    readonly Text[] _rowPts = new Text[RowTotal];
    readonly GameObject[] _rowFight = new GameObject[RowTotal];   // 「对战」按钮（可挑战行显示）
    readonly GameObject[] _rowDash = new GameObject[RowTotal];    // 「—」（不可挑战行显示）
    Sprite _rowBarSprite;   // 普通行条（_0009_条1，无外框；按路径加载）
    Sprite _rowSelSprite;   // 玩家行选中条（_0009_条1选中，金色外框；按路径加载）

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

    // ── 机器人档案详情弹窗已整条删除（2026-10-31 主人拍板：点榜单行 / 点领奖台卡都不弹信息）──
    // 原 _detailRoot/_dTitle/_dPower/_dPoints 字段与 ShowDetailForRow/ShowDetailForRank/
    // ShowBotDetail/BuildBotDetail/FillLoadout/ClearDetailRows/SectionName 全部移除。

    // ── 对战记录（2026-10-29 晚主人拍板新增；深夜起面板节点已搭进主预制体，代码只绑定+填行）──
    GameObject _recordsRoot;
    RectTransform _recordContent;

    // ── 排行奖励面板（2026-10-29 深夜主人拍板：节点已搭进主预制体，自带 Dim 黑底；代码只管开关）──
    GameObject _rewardRoot;

    // ── 竞技商店（2026-10-30 积分版；2026-10-31 主人拍板改版：「把所有商品按这个模式加上 做成上下滑动列表
    // 注意商品图框按稀有度换 点击图标弹出信息框」）──
    // 页内 RecordsBox(689×739) + 人/底框/RTitle/积分icon(余额)/RCloseBtn + 商品（主人手摆的行卡模板）。
    // 商品单元 = 行卡版式（横条行底 + 商品图框>icon + 商品名 + 商品名 (1)价格 + 购买钮），排版全以预制体为准，
    // 代码只设图/填字/接线，不摆位（铁律 3）。货架 = 运行时搭的纵向 ScrollRect（ShopList>ShopViewport>ShopContent），
    // 8 件 = ArenaShopGoods.AllGoods 单列往下排，首行位置/行高以模板实测几何为准；模板恒隐藏（同 RowTemplate 口径）。
    // 图框一律按稀有度设（MercHireSession.LoadTavernCardFrame 酒馆池格三档框，Sliced；
    // ⚠️ 首版误用 LoadRarityFrame——那是三选一高卡插画底 219×789，preserveAspect 塞进 70×70 方框
    // 被压成 19×70 细条（2026-10-31 主人报「图标框不对 比例也不对」，照佣兵池列表卡同款修法换框））；
    // 点图标（图框）或点「购买」钮
    // → ArenaShopInfoPopupUI 商品信息弹窗（按「获得技能」弹窗同款皮，带价格+现有积分）。
    // （2026-10-31 主人拍板：赛季末清零横幅撤掉，不再挂「速花」文案。）──
    const string ShopBtnName = "竞技商店Btn";
    const string ShopPageName = "竞技商店";
    GameObject _shopRoot;
    RectTransform _shopBox;          // 页内 RecordsBox（货架格子容器）
    ScrollRect _shopScroll;          // 货架纵向滚动（每次打开回顶部）
    GameObject _shopUnitTemplate;    // 「商品」示例单元（克隆源，恒隐藏）
    Text _shopBalanceText;           // 积分icon 下的余额数字
    readonly List<GameObject> _shopUnits = new List<GameObject>();   // 克隆单元（显示顺序，与 _shopGoods 同下标）
    readonly List<ArenaShopGoods.Good> _shopGoods = new List<ArenaShopGoods.Good>();   // 显示顺序的商品（=AllGoods 顺序，2026-10-31 起纵向列表）

    // 行条图/色全以预制体为准（2026-11-01 主人拍板），代码不再持有行底色
    readonly Color _txtSub = new Color(0.75f, 0.75f, 0.80f, 1f);
    readonly Color _txtGold = new Color(1f, 0.80f, 0.35f, 1f);

    // ───────────────────────── 打开 / 生命周期 ─────────────────────────

    public static void Show()
    {
        var ui = Ensure();
        if (ui != null) ui.Open();
    }

    /// <summary>【2026-10-31 主人拍板「从竞技场返回的时候回到竞技场界面 不要一下回到主界面」】
    /// 竞技场战斗（胜/负两条结算路都置位）回城后自动重开本弹窗。
    /// 置位点 = BattleModes.ArenaBattleMode.SettleStageClear（胜）/ BattleManager.Death 竞技场失败分支（负）；
    /// 消费点 = TownHubController.Start（先例 = TownHubController.PendingOpenAdventure / TapTapRatePopup.ConsumePendingPrompt）。</summary>
    public static bool PendingOpenAfterBattle;

    /// <summary>回城消费口：置位则重开竞技场弹窗（只消费一次）。</summary>
    public static void ConsumePendingOpen()
    {
        if (!PendingOpenAfterBattle) return;
        PendingOpenAfterBattle = false;
        Show();
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
        ArenaRules.EnsurePeriods(data);   // 跨天清次数+机器人漂移换位；跨周赛季结算（懒触发；2026-11-03 测试用改一周一结）
        ArenaRules.SettlePendingQuitLoss();   // 中途退出判负补账（2026-10-31 主人拍板「直接算输」）：有未落账挑战先补 -10 再刷榜
        ArenaRules.RefreshBotScoresIfDue(data);   // 游玩中到点刷机器人分（2026-10-30 20:10 主人拍板「一个时间刷一下，不要不变」）
        string notice = ArenaRules.TakeSeasonNotice(data);

        var canvas = GetComponent<Canvas>();
        if (canvas != null) UICanvasSetup.RefreshPopup(canvas, GameConfig.UiSort.TownPopup);

        gameObject.SetActive(true);
        FitBackdrop();   // 背景等比铺满：父级实际矩形此刻才算得出（2026-10-30 主人拍板）
        Refresh();
        ScrollToPlayer();
        ResetRowTransient();    // 清换位动效残留（见方法注释，2026-11-01 主人报「滑动看到一条机器人的条显示有问题」）
        GlobalToastUI.Hide();   // 战败「积分 -10」toast 会活着飘进榜单压住一行（2026-11-01 主人截图）：开榜即收，掉分由名次动效表达
        TryPlayShiftAnim();   // 战斗结算名次动效（2026-10-30 主人拍板：胜=放大上移砸落 / 败=隐藏下移出现）
        if (_noticeText != null)
        {
            _noticeText.gameObject.SetActive(!string.IsNullOrEmpty(notice));
            if (!string.IsNullOrEmpty(notice)) _noticeText.text = notice;
        }
    }

    void Close()
    {
        if (_recordsRoot != null) _recordsRoot.SetActive(false);
        if (_rewardRoot != null) _rewardRoot.SetActive(false);
        if (_shopRoot != null) _shopRoot.SetActive(false);   // 商店页一并收起（关弹窗不留残页）
        ArenaShopInfoPopupUI.CloseIfOpen();   // 商品信息弹窗是 DDOL 独立实例，主弹窗关了顺带收掉
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
        var topT = UiFind.Deep(transform, "Top");
        if (topT != null)
        {
            // 2026-11-01 主人拍板「跟主界面一样避开刘海」：主人手摆的 Top 排挂主界面同款
            // SafeAreaFitter.EnsureTopOffset（Offset 模式只下移 anchoredPosition、不碰锚点/布局；
            // 幂等可重复调，DDOL 常驻弹窗组件自己跟随安全区变化）。
            SafeAreaFitter.EnsureTopOffset(topT as RectTransform);
            return;   // 有 Top 就一个节点都不碰（位置/父级/层级全以预制体为准，铁律 9）
        }
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
        // 【2026-11-01】广告过渡期（ADS_ENABLED=false）按钮折叠不显示（与结算双倍/佣兵活动同口径）；
        // 微信激励视频接入后自动恢复显示
        var adBtn = UiFind.Deep(transform, "AdBonusBtn");
        if (adBtn != null)
        {
            if (!GameConfig.ADS_ENABLED)
            {
                adBtn.gameObject.SetActive(false);
            }
            else
            {
                var lt = UiFind.Deep(adBtn, "Label");
                if (lt != null) _adBtnText = lt.GetComponent<Text>();
                var btn = adBtn.GetComponent<Button>();
                if (btn == null) btn = adBtn.gameObject.AddComponent<Button>();
                btn.onClick.AddListener(ArenaRules.RequestAdBonus);
            }
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
            // 2026-10-31 主人拍板「点前三名的卡也不用弹信息」：领奖台点卡弹档案已收掉（原 cardBtn+ShowDetailForRank 整块删除）。
            // 领奖台卡点击零反应；机器人档案弹窗整条链（ShowBotDetail/BuildBotDetail/FillLoadout）同批删除。
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
        BuildRecords();       // 对战记录弹窗：绑定主预制体里已有的 ArenaBattleRecords 节点
        BuildRewardPanel();   // 排行奖励面板：绑定主预制体里已有的 EvacuateConfirmPopup 节点（自带 Dim 黑底）
        BuildShop();          // 竞技商店页：绑定主人手摆的「竞技商店」节点 + 克隆商品单元排格（不进 prefab）
        MoveTopBar();         // 顶部一排：对战次数/对战记录/排行奖励移出弹窗（2026-10-30 主人拍板）

        ArenaRules.Changed += Refresh;
    }

    void BuildRows()
    {
        // 行几何以预制体为准（2026-10-30 主人拍板）：行高/行宽取模板实际值，不再写死覆盖
        var tmplRt = _rowTemplate.GetComponent<RectTransform>();
        if (tmplRt != null) _rowH = tmplRt.sizeDelta.y;
        _rowStep = _rowH + 8f;
        // 2026-11-10 主人报「最下面列表显示不全」：Viewport 下移 47 压进底框装饰带，
        // content 底部原只留 12px，滚到底时第 101 名下半被切；多留 56px 把最后一行抬出装饰区（不动 prefab）。
        _contentH = RowTotal * _rowStep + 8f + 56f;
        _content.sizeDelta = new Vector2(tmplRt != null ? tmplRt.sizeDelta.x : 640f, _contentH);   // BotCount 变化时运行时同步
        if (_rowTemplate.activeSelf) _rowTemplate.SetActive(false);   // 模板恒隐藏（防手滑）

        // 行条全按路径加载（2026-11-01 主人报「玩家该有外框 其他机器人没有 现在反了」）：
        // 根因＝行条走 prefab 旧 hex32 引用（对战美术是 56 字符 guid，这两个 guid 磁盘上无 meta 拥有，
        // 引擎兼容映射不可靠导致外框条/普通条发反）；改同 ApplyArt 的 Resources.Load 口径，图定死不靠映射。
        // 普通行 = _0009_条1（无外框），玩家行 = _0009_条1选中（金色外框）；边框照 meta spriteBorder（Sliced 随框拉伸不变形）。
        _rowBarSprite = LoadArt("_0009_条1", new Vector4(68f, 38f, 79f, 36f));
        _rowSelSprite = LoadArt("_0009_条1选中", new Vector4(73f, 45f, 70f, 45f));
        if (_rowBarSprite == null || _rowSelSprite == null)
            Debug.LogError("[Arena] 行条图缺失：Resources/UI/Art/对战/_0009_条1(-选中).png（玩家行选中条切不了，报主人）");

        for (int i = 0; i < RowTotal; i++)
        {
            var row = Object.Instantiate(_rowTemplate, _content);
            row.name = "Row_" + i;
            row.SetActive(true);
            var rt = row.GetComponent<RectTransform>();
            if (rt != null)
            {
                // 只写纵向排队 y（必要介入）；锚点/宽高/x 一律跟随预制体模板，不写死（铁律 20）
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -(i * _rowStep + 4f));
            }
            _rowRoots[i] = row;

            var rankT = UiFind.Deep(row.transform, "RankText");
            _rowRank[i] = rankT != null ? rankT.GetComponent<Text>() : null;
            var headT = UiFind.Deep(row.transform, "HeadImg");
            _rowHead[i] = headT != null ? headT.GetComponent<Image>() : null;
            // 2026-10-31 主人报「图还是变形了 跟没改一样」：行头像 Image 从未设 preserveAspect，
            // 非正方形头像被硬拉满 HeadImg 框 → 变形。绑定时设一次（只动 Image 展示位，不改框尺寸）。
            if (_rowHead[i] != null) _rowHead[i].preserveAspect = true;
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

            // 2026-10-31 主人拍板「点其他玩家那个条无反应就行」：行点击弹档案已收掉（原 rowBtn+ShowDetailForRow 整块删除）。
            // 点行不再有任何反应；进对战只走行上「对战」钮 → 选职业 → 进入挑战（OnChallengeClicked，单一出口）。
            // 同日晚「点前三名的卡也不用弹信息」→ 领奖台入口也收，机器人档案弹窗整条链已删（见字段区与档案弹窗区拍板记录）。
            // 2026-11-01 主人拍板「还原和别人一样的条 + 代码做一个选中框」→ 当日晚改口「直接用预制体里
            // 做好的九宫格选中条」：代码金框整块废弃，玩家行只换 sprite 引用（见 Refresh），别的不碰。
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

    /// <summary>同上，但 9-slice 边框按四边给（x=左 y=下 z=右 w=上，与 meta spriteBorder 同序）。缺图 LogError 不兜底。</summary>
    static Sprite LoadArt(string file, Vector4 border)
    {
        if (ArtCache.TryGetValue(file, out var hit) && hit != null) return hit;
        var tex = Resources.Load<Texture2D>(ArtDir + file);
        if (tex == null)
        {
            Debug.LogError($"[Arena] 对战美术缺失：Resources/{ArtDir}{file}（确认 png 与 .meta 都在）");
            return null;
        }
        var sp = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, border);
        ArtCache[file] = sp;
        return sp;
    }

    /// <summary>玩家当前头像（跟随头像选择框）；缺图 LogError 不兜底（2026-11-03 主人要求「缺图不要静默空白」）。
    /// where = 出错位置标签，三处复用同一张头像时便于定位。</summary>
    static Sprite PlayerHeadSprite(string where)
    {
        string hireId = AvatarDefs.HireIdOf(AvatarDefs.CurrentSlot());
        var sp = MercPortraitSprites.GetHead(hireId);
        if (sp == null)
            Debug.LogError($"[Arena] 玩家头像加载失败：{hireId}（{where}，检查 Resources/UI/Icons/MercHead/{hireId}.png 与 .meta；头像留空不兜底）");
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
    /// 映射：Panel=底框(9-slice)、MyBar 不设图（预制体主人摆的选中条为准，2026-11-01）、TitleImg=标题横幅（烤字，TitleText 已隐藏）、TriesCapsule=对战次数胶囊、
    /// Podium1/2/3=第1/第2/第三卡（名次已烤图，RankNum 已隐藏，运行时 tint 改白）、
    /// FightBtn=对战按钮（烤字，Label 已清空）、PtsIcon 系列=积分.png（主人 2026-10-29 新放）。
    /// </summary>
    void ApplyArt()
    {
        var panelImg = UiFind.Deep(transform, "Panel")?.GetComponent<Image>();
        if (panelImg != null) panelImg.sprite = LoadArt("_0000_底框", 40f);
        // MyBar 不再由代码设图（2026-11-01 主人拍板）：主人已在预制体里摆好「选中条」，
        // 旧版这里强设 _0000_底框 把选中条盖掉（面板大框图塞进 620×103 横条）→ 一运行就变形。
        // 预制体有值→代码不写，图全信 prefab。

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
            _myHead.sprite = PlayerHeadSprite("弹窗我的头像");   // 跟随玩家当前头像（2026-11-03 主人拍板「竞技场不替换节点，头像根据玩家换的头像来换」）

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
                    _podHead[i].sprite = PlayerHeadSprite("领奖台头像");   // 跟随玩家当前头像（2026-11-03 主人拍板「竞技场不替换节点，头像根据玩家换的头像来换」）
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

            // 玩家行 = 选中条（_0009_条1选中，金色外框）、其余行 = 普通条（_0009_条1）；两支按路径加载（见 BuildRows），
            // 只换 sprite 引用，Image 的 Sliced/颜色/几何一概不动，随框拉伸不变形。
            if (_rowRoots[i] != null)
            {
                var img = _rowRoots[i].GetComponent<Image>();
                if (img != null && _rowBarSprite != null && _rowSelSprite != null)
                    img.sprite = isPlayer ? _rowSelSprite : _rowBarSprite;
            }

            if (_rowRank[i] != null) _rowRank[i].text = (i + 1).ToString();
            if (isPlayer)
            {
                if (_rowName[i] != null) _rowName[i].text = myName;
                if (_rowPts[i] != null) _rowPts[i].text = data.arenaPoints.ToString();
                if (_rowHead[i] != null)
                    _rowHead[i].sprite = PlayerHeadSprite("对战行头像");   // 跟随玩家当前头像（2026-11-03 主人拍板「竞技场不替换节点，头像根据玩家换的头像来换」）
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

        RefreshShop();   // 商店页余额/价格随 Changed 同步（积分变化/购买后即时刷新）
    }

    /// <summary>把玩家行滚到视口中间。</summary>
    void ScrollToPlayer()
    {
        if (_scroll == null) return;
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null) return;
        int pos = Mathf.Clamp(data.arenaPlayerPos, 0, RowTotal - 1);
        float offset = pos * _rowStep + _rowH * 0.5f - ViewportH * 0.5f;   // 玩家行中心对齐视口中心
        float max = Mathf.Max(1f, _contentH - ViewportH);
        _scroll.verticalNormalizedPosition = Mathf.Clamp01(1f - offset / max);
    }

    // ───────────────────── 战斗结算名次动效（2026-10-30 主人拍板） ─────────────────────
    // 胜局升名：玩家条放大 1.5 倍 → 从旧名次位上移到新位 → 砸落归位（约 2.2s）；
    // 败局掉名（-10 分按积分重排）：条在旧位淡出隐藏 → 隐身下移到新名次位 → 出现 → 落定轻震（约 2.1s）。
    // 数据来源 ArenaRules.LastBattleAnim（结算落账时写、Open 时消费清零）；纯 RT 动画，不动 prefab/排版。

    /// <summary>胜利条放大倍数（主人拍板 1.5 倍）。</summary>
    const float ShiftScaleBig = 1.5f;

    /// <summary>
    /// 清换位动效残留（幂等，每次 Open 调）：行是 DDOL 常驻对象，换位动效（CoWinShift/CoFailShift）
    /// 若被中途关弹窗打断（SetActive(false) 停协程），半透明 CanvasGroup / 缩放 / 错位 y / 滚动锁
    /// 会原样带到下一次打开——主人 2026-11-01 报「滑动看到一条机器人的条显示有问题」即此。
    /// 在 TryPlayShiftAnim 前复位：alpha=1、scale=1、归位 y、解锁滚动；动效自己再从旧位起步不受影响。
    /// </summary>
    void ResetRowTransient()
    {
        for (int i = 0; i < RowTotal; i++)
        {
            var row = _rowRoots[i];
            if (row == null) continue;
            var cg = row.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f;
            row.transform.localScale = Vector3.one;
            var rt = row.GetComponent<RectTransform>();
            if (rt != null) rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -(i * _rowStep + 4f));   // 同 BuildRows/动效 home 公式
        }
        if (_scroll != null) _scroll.enabled = true;
    }

    /// <summary>Open 时消费结算标记并启动名次动效（没打过分/名次没动直接跳过——动效是锦上添花，不报错）。</summary>
    void TryPlayShiftAnim()
    {
        int anim = ArenaRules.LastBattleAnim;
        ArenaRules.LastBattleAnim = 0;
        if (anim == 0 || _scroll == null) return;
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null) return;
        int pos = Mathf.Clamp(data.arenaPlayerPos, 0, RowTotal - 1);
        var rt = _rowRoots[pos] != null ? _rowRoots[pos].GetComponent<RectTransform>() : null;
        if (rt == null) return;
        StartCoroutine(anim > 0 ? CoWinShift(rt, pos, anim) : CoFailShift(rt, pos, -anim));
    }

    /// <summary>胜局升名：放大1.5 → 上移到新名次位 → 砸落归位。条恒 SetAsLastSibling 压列表最上层，途经/压住邻行即「飞过」，无需藏行。</summary>
    IEnumerator CoWinShift(RectTransform rt, int newPos, int upRows)
    {
        float baseY = -(newPos * _rowStep + 4f);
        float fromY = -((newPos + upRows) * _rowStep + 4f);   // 旧名次位（下方）
        float x = rt.anchoredPosition.x;

        rt.SetAsLastSibling();
        if (_scroll != null) _scroll.enabled = false;        // 动效期间锁滚动防打架
        rt.anchoredPosition = new Vector2(x, fromY);

        yield return Tween(0.35f, k => rt.localScale = Vector3.one * (1f + (ShiftScaleBig - 1f) * EaseOutCubic(k)));       // 放大 1.5
        yield return Tween(1.55f, k => rt.anchoredPosition = new Vector2(x, Mathf.Lerp(fromY, baseY, EaseInOutCubic(k)))); // 上移到新位
        yield return Tween(0.22f, k => rt.localScale = Vector3.one * Mathf.Lerp(ShiftScaleBig, 1f, EaseInCubic(k)));       // 砸落骤缩
        yield return Tween(0.12f, k => rt.localScale = new Vector3(1f + 0.06f * (1f - k), 1f - 0.08f * (1f - k), 1f));     // 落地震一下

        rt.localScale = Vector3.one;
        rt.anchoredPosition = new Vector2(x, baseY);
        EndShift();
    }

    /// <summary>败局掉名：旧位淡出隐藏 → 隐身下移到新名次位 → 出现 → 落定轻震（掉名幅度大时起点在屏外，隐身滑行不受影响）。</summary>
    IEnumerator CoFailShift(RectTransform rt, int newPos, int downRows)
    {
        float baseY = -(newPos * _rowStep + 4f);
        float fromY = -((newPos - downRows) * _rowStep + 4f);   // 旧名次位（上方）
        float x = rt.anchoredPosition.x;
        var cg = rt.GetComponent<CanvasGroup>();
        if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();

        rt.SetAsLastSibling();
        if (_scroll != null) _scroll.enabled = false;
        rt.anchoredPosition = new Vector2(x, fromY);

        yield return Tween(0.45f, k => cg.alpha = 1f - k);                                                                  // 旧位淡出隐藏
        yield return Tween(0.8f, k => rt.anchoredPosition = new Vector2(x, Mathf.Lerp(fromY, baseY, EaseInCubic(k))));      // 隐身下移到新位
        yield return Tween(0.5f, k => cg.alpha = k);                                                                        // 到位后出现
        yield return Tween(0.35f, k => { float s = 1f + 0.06f * (1f - k); rt.localScale = new Vector3(s, 2f - s, 1f); });   // 落定轻震

        rt.localScale = Vector3.one;
        rt.anchoredPosition = new Vector2(x, baseY);
        cg.alpha = 1f;
        EndShift();
    }

    /// <summary>动效收尾：解锁滚动 + 玩家行重新居中（行显隐/底图由既有 Refresh 链管，动效不碰）。</summary>
    void EndShift()
    {
        if (_scroll != null) _scroll.enabled = true;
        ScrollToPlayer();
    }

    /// <summary>轻量补间：每帧回传 0~1 进度（unscaled 防暂停影响），结束必回传 1。</summary>
    static IEnumerator Tween(float dur, System.Action<float> apply)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            apply(Mathf.Clamp01(t / dur));
            yield return null;
        }
        apply(1f);
    }

    static float EaseOutCubic(float k) => 1f - Mathf.Pow(1f - k, 3f);
    static float EaseInCubic(float k) => k * k * k;
    static float EaseInOutCubic(float k) => k < 0.5f ? 4f * k * k * k : 1f - Mathf.Pow(-2f * k + 2f, 3f) / 2f;

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
    // 竞技商店(全屏页) > RecordsBox(689×739) > 人 / 底框 / RTitle / 积分icon>Text(余额) / RCloseBtn / 商品(示例单元)
    // 货架 8 件 = ArenaShopGoods.AllGoods（1 弓 + 3 佣兵 + 1 金币包 + 3 徽记包），运行时纵向滚动列表单列排。

    // 货架滚动区（RecordsBox 中心锚坐标）：顶部让开余额行（积分icon 底 ≈ y137），底部到框底 y-345。
    const float ShopShelfTopY = 137f;
    const float ShopShelfBottomY = -315f;   // 2026-11-01 主人报「滑动列表超出下面的框」：底边 -345 上收 30
    /// <summary>行间距 = 行高 + 12（沿用榜单 8px 行距的呼吸感，行卡更高取 12）。</summary>
    const float ShopRowGap = 12f;

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

        // 关闭钮：页内搜索（对战记录面板也有同名 RCloseBtn，不能全树搜）。
        // 2026-10-31 主人报「点关闭无反应」：静态核对（prefab 有 Button interactable=1、无遮挡、Editor.log 无报错）
        // 未找到断链根因 → 按主弹窗 CloseBtn 同款口径（2026-10-30 同症状已修）补 AddComponent 兜底 + CloseShop 落日志，待真机复验。
        var closeT = UiFind.Deep(pageT, "RCloseBtn");
        var closeBtn = closeT != null ? closeT.GetComponent<Button>() : null;
        if (closeBtn == null && closeT != null) closeBtn = closeT.gameObject.AddComponent<Button>();
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

        BuildShopUnits();
        _shopRoot.SetActive(false);   // 运行时接线建树完成即藏（红线：Build 末尾 SetActive(false)）
    }

    void BuildShopUnits()
    {
        if (_shopUnitTemplate != null || _shopBox == null) return;
        var unitT = UiFind.Deep(_shopBox, "商品") as RectTransform;
        if (unitT == null)
        {
            Debug.LogError("[Arena] 商店页缺节点：商品（示例单元，克隆源）");
            _bindFailed = true;
            return;
        }
        _shopUnitTemplate = unitT.gameObject;
        _shopUnitTemplate.SetActive(false);   // 模板恒隐藏（防手滑；同 RowTemplate 口径）

        // ── 运行时搭纵向滚动列表（预制体里只有模板单元，滚动结构代码建；同 BuildRecords 口径）──
        // ShopList(ScrollRect) > ShopViewport(RectMask2D + 透明 Image 接收拖拽) > ShopContent(顶中锚)
        float shelfH = ShopShelfTopY - ShopShelfBottomY;
        var listRt = NewUINode(_shopBox, "ShopList", typeof(ScrollRect));
        listRt.anchorMin = listRt.anchorMax = new Vector2(0.5f, 0.5f);
        listRt.pivot = new Vector2(0.5f, 0.5f);
        listRt.sizeDelta = new Vector2(_shopBox.sizeDelta.x, shelfH);
        listRt.anchoredPosition = new Vector2(0f, (ShopShelfTopY + ShopShelfBottomY) * 0.5f);

        var viewportRt = NewUINode(listRt, "ShopViewport", typeof(RectMask2D), typeof(Image));
        Stretch(viewportRt);
        viewportRt.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);   // 透明，只做拖拽 raycast（ScrollRect 标准摆法）
        var contentRt = NewUINode(viewportRt, "ShopContent");

        var scroll = listRt.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 15f;
        scroll.viewport = viewportRt;
        scroll.content = contentRt;
        _shopScroll = scroll;

        // 行几何以预制体模板实测为准（榜单 RowTemplate 同口径）：行高取横条高（缺则取单元高），步进 = 行高 + 间隙
        var stripT = UiFind.Deep(_shopUnitTemplate.transform, "横条") as RectTransform;
        float rowH = stripT != null ? stripT.sizeDelta.y : unitT.sizeDelta.y;
        float rowStep = rowH + ShopRowGap;
        float firstOffset = ShopShelfTopY - unitT.anchoredPosition.y;   // 模板摆在哪，首行就在哪（距视口顶）

        // 货架显示顺序按稀有度排列（2026-11-01 主人拍板「让商品按稀有度排列」）：
        // 传奇 → 稀有 → 普通；同档保持 AllGoods 定义顺序（分桶追加，稳定不乱序）。
        // 只动货架显示顺序，AllGoods 定义与购买链一概不动。
        var goods = new List<ArenaShopGoods.Good>(ArenaShopGoods.AllGoods.Length);
        for (int r = (int)MercRosterDefs.MercRarity.Legendary; r >= (int)MercRosterDefs.MercRarity.Common; r--)
            foreach (var g in ArenaShopGoods.AllGoods)
                if ((int)g.frameRarity == r) goods.Add(g);
        contentRt.anchorMin = contentRt.anchorMax = contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(listRt.sizeDelta.x, firstOffset + (goods.Count - 1) * rowStep + rowH * 0.5f + 20f);

        for (int i = 0; i < goods.Count; i++)
        {
            var good = goods[i];
            var row = Object.Instantiate(_shopUnitTemplate, contentRt);
            row.name = "ShopUnit_" + good.id;
            row.SetActive(true);
            var rt = (RectTransform)row.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);   // 保持模板中心锚语义：anchoredPosition 即行中心
            rt.anchoredPosition = new Vector2(unitT.anchoredPosition.x, -(firstOffset + i * rowStep));
            rt.localScale = Vector3.one;
            _shopUnits.Add(row);
            _shopGoods.Add(good);

            // 图框按稀有度设图（2026-10-31 主人拍板「商品图框按稀有度换」）——只设图，不动主人摆的位置尺寸。
            // 【2026-10-31 修「图标框不对 比例也不对」】首版误用 LoadRarityFrame（三选一高卡插画底
            // 219×789），preserveAspect 塞进 70×70 方框被压成 19×70 细条。照佣兵池列表卡/获得佣兵弹窗
            // 同款修法：换 LoadTavernCardFrame 酒馆池格三档框（传奇金/稀有蓝/普通银）+ Sliced，
            // 九宫边框厚度不随格子尺寸变形，正好包住 60×60 图标。
            var frameT = UiFind.Deep(row.transform, "商品图框");
            var frameImg = frameT != null ? frameT.GetComponent<Image>() : null;
            if (frameImg != null)
            {
                var frameSp = MercHireSession.LoadTavernCardFrame(good.frameRarity);
                if (frameSp == null)
                    Debug.LogError($"[Arena] 稀有度底框缺失：酒馆头像框（{good.frameRarity}，商品 {good.id}，底框留空可见）");
                else
                {
                    // 【2026-11-01 主人拍板「要原始的大小 然后缩放 不要改宽高」】Sliced 把框拉到格子
                    // 尺寸会变形：改 Simple + SetNativeSize 用贴图原始尺寸，再按主人摆的框**等比**缩放
                    // 包进去（min 比例），宽高一概不写、只动 localScale。
                    var frameRt = frameImg.rectTransform;
                    Vector2 frameBox = frameRt.sizeDelta;   // 主人摆的目标框，只当缩放基准用
                    frameImg.sprite = frameSp;
                    frameImg.type = Image.Type.Simple;
                    frameImg.preserveAspect = false;
                    frameImg.SetNativeSize();
                    float fs = Mathf.Min(
                        frameSp.rect.width > 0.5f ? frameBox.x / frameSp.rect.width : 1f,
                        frameSp.rect.height > 0.5f ? frameBox.y / frameSp.rect.height : 1f);
                    frameRt.localScale = new Vector3(fs, fs, 1f);
                }
            }
            else
                Debug.LogError("[Arena] 商品单元缺节点/组件：商品图框（无 Image，稀有度底框挂不上）");

            // 点图标（图框）弹信息框（2026-10-31 主人拍板「点击图标弹出信息框」；点 icon 子节点会冒泡到图框按钮）
            if (frameT != null)
            {
                var iconBtn = frameT.GetComponent<Button>();
                if (iconBtn == null) iconBtn = frameT.gameObject.AddComponent<Button>();
                var capturedIcon = good;
                iconBtn.onClick.AddListener(() => OnShopUnitClicked(capturedIcon));
            }

            // 图标（按货架类型取图，缺图不兜底）
            var iconImg = UiFind.Deep(row.transform, "icon")?.GetComponent<Image>();
            if (iconImg != null)
            {
                var sp = ShopIconFor(good);
                if (sp == null)
                    Debug.LogError($"[Arena] 商品图标缺失：{good.id}（不兜底，格内留灰块可见）");
                else
                {
                    // 【2026-11-01 主人拍板「其他的也是」】与图框同口径：原始大小 + 等比缩放，不写宽高
                    var iconRt = iconImg.rectTransform;
                    Vector2 iconBox = iconRt.sizeDelta;
                    iconImg.sprite = sp;
                    iconImg.preserveAspect = false;
                    iconImg.SetNativeSize();
                    float isc = Mathf.Min(
                        sp.rect.width > 0.5f ? iconBox.x / sp.rect.width : 1f,
                        sp.rect.height > 0.5f ? iconBox.y / sp.rect.height : 1f);
                    iconRt.localScale = new Vector3(isc, isc, 1f);
                }
            }

            // 名字：建格写一次（DisplayName；价格随 RefreshShop 动态刷「商品名 (1)」）
            var nameText = UiFind.Deep(row.transform, "商品名")?.GetComponent<Text>();
            if (nameText != null) nameText.text = good.DisplayName;
            else Debug.LogError("[Arena] 商品单元缺文本节点：商品名（名字不显示）");

            // 购买钮（prefab 无 Button，运行时补；点它 → 商品信息弹窗，与点图标同一条出口）
            var buyT = UiFind.Deep(row.transform, "购买");
            if (buyT != null)
            {
                var buyBtn = buyT.GetComponent<Button>();
                if (buyBtn == null) buyBtn = buyT.gameObject.AddComponent<Button>();
                var captured = good;
                buyBtn.onClick.AddListener(() => OnShopUnitClicked(captured));
                // 【2026-11-10 主人报「按钮太大了」】撤销 11-01 的 ×1.2 放大，回到预制体原始尺寸。
                // 统一口径：所有界面的购买按钮都不额外放大，不逐个报。
                buyT.localScale = Vector3.one;
            }
            else
                Debug.LogError("[Arena] 商品单元缺节点：购买（购买钮不显示）");

            // 【2026-11-01 主人报「点条的中间部分弹窗竟然关闭了」】横条行底原来不接点击，
            // 点中间落空冒泡到页根 Button（点空白关页）→ 整页被关。改为横条整条可点 →
            // 打开商品信息框（与点图框/购买钮同一条出口 OnShopUnitClicked）。
            var rowStrip = UiFind.Deep(row.transform, "横条");
            if (rowStrip != null)
            {
                var stripImg = rowStrip.GetComponent<Image>();
                if (stripImg != null) stripImg.raycastTarget = true;
                var stripBtn = rowStrip.GetComponent<Button>();
                if (stripBtn == null) stripBtn = rowStrip.gameObject.AddComponent<Button>();
                var capturedRow = good;
                stripBtn.onClick.AddListener(() => OnShopUnitClicked(capturedRow));
            }
            else
                Debug.LogError("[Arena] 商品单元缺节点：横条（行底整条点击接不上，点中间仍会关页）");
        }
    }

    /// <summary>运行时建 UI 节点（带 RectTransform；extra 组件随建，SetParent(false) 保持本地值）。</summary>
    static RectTransform NewUINode(Transform parent, string name, params System.Type[] extra)
    {
        var types = new System.Type[extra.Length + 1];
        types[0] = typeof(RectTransform);
        for (int i = 0; i < extra.Length; i++) types[i + 1] = extra[i];
        var go = new GameObject(name, types);
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
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

    /// <summary>点「购买」→ 商品信息弹窗（2026-10-31 主人拍板「按照技能那个信息弹窗来」：
    /// 走 ArenaShopInfoPopupUI（同「获得技能」弹窗皮），弹窗带 价格行 + 现有积分（主人要求
    /// 「弹窗上要加个价格」「也要玩家的积分总量」）。置灰两档（键仍可点，点击 toast 不入账）：
    /// ① 积分不足；② 佣兵满星（判定与 TryBuy 拦截同源）。确认回调里做购买（判定/扣账仍在这边）。</summary>
    void OnShopUnitClicked(ArenaShopGoods.Good good)
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null) return;
        int price = ArenaShopGoods.PriceOf(data, good);
        bool afford = data.arenaPoints >= price;
        bool fullStar = good.kind == ArenaShopGoods.GoodKind.Merc
            && ArenaShopGoods.IsOwned(data, good)
            && MercStarSystem.OutStarOf(good.hireId) >= MercStarDefs.MaxOutStar;
        // 弹窗正文：效果/规则 + 周限进度（价格已独立成价格行，正文不再重复写价）
        string body = good.BuildConfirmBody(data);
        if (good.weeklyLimit > 0)
            body += $"\n本周已购 {ArenaShopGoods.WeekBuyCount(data, good.id)}/{good.weeklyLimit} 次";
        ArenaShopInfoPopupUI.Show("商品信息", good.DisplayName,
            $"价格 {price} 积分 · 现有 {data.arenaPoints}", body,
            ShopIconFor(good), !afford || fullStar, () =>
        {
            // 复验日志（照 2026-10-31「点关闭无反应」落日志先例）：确认回调有没有进来、走了哪条分支
            var d2 = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
            if (d2 == null) { Debug.LogWarning("[Arena] 确认购买回调：存档未就绪，忽略"); return; }
            int pay = ArenaShopGoods.PriceOf(d2, good);
            if (d2.arenaPoints < pay)
            {
                Debug.Log($"[Arena] 确认购买 {good.id}：积分不足（现有 {d2.arenaPoints} < 应付 {pay}）");
                // force=true：购买反馈必须让玩家看到，不被对话/气泡压制吞掉（同 GlobalToastUI 物品变动口径）
                GlobalToastUI.Show("积分不足", true);
                return;
            }
            ArenaShopGoods.TryBuy(good.id, out string msg);   // 满星拦截在 TryBuy 内（toast 已有，不扣分）
            Debug.Log($"[Arena] 确认购买 {good.id}：TryBuy={(!string.IsNullOrEmpty(msg) ? msg : "成功")}");
            if (!string.IsNullOrEmpty(msg)) GlobalToastUI.Show(msg, true);
            RefreshShop();
        });
    }

    void ShowShop()
    {
        if (_shopRoot == null) return;
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        ArenaRules.EnsurePeriods(data);   // 幂等：跨天/跨月对齐（顺带注册逐风长弓模板）
        // 【2026-10-31 修「滑动列表时整个弹窗缩小一下」】页根自带 Button（点空白关页），
        // UICanvasSetup 会给它自动挂按压反馈（缩到 0.94）：在货架列表上按下/拖动时，
        // 按下事件沿层级冒泡到页根 Selectable → 整页缩一下再弹回。页根只当点空白关页用，
        // 不该有视觉反馈 → 关掉（Open→RefreshPopup 已先挂好组件，此处必定 suppress 得上）。
        UiButtonPressFeedback.Suppress(_shopRoot);
        _shopRoot.SetActive(true);
        _shopRoot.transform.SetAsLastSibling();
        if (_shopScroll != null) _shopScroll.verticalNormalizedPosition = 1f;   // 每次打开回顶部
        RefreshShop();
    }

    void CloseShop()
    {
        Debug.Log("[Arena] 商店页关闭");   // 2026-10-31 主人报「点关闭无反应」：落一行日志，复验时看这条有没有打出来
        if (_shopRoot != null) _shopRoot.SetActive(false);
    }

    /// <summary>商店页刷新：余额（订阅 Changed 即时刷新）+ 各格价格/买断变灰。
    /// （2026-10-31 主人拍板：赛季末清零横幅撤掉，这里不再刷横幅。）</summary>
    void RefreshShop()
    {
        if (_shopRoot == null) return;
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null) return;
        ArenaShopGoods.EnsureWeek(data);

        if (_shopBalanceText != null) _shopBalanceText.text = data.arenaPoints.ToString();

        for (int i = 0; i < _shopUnits.Count && i < _shopGoods.Count; i++)
        {
            var good = _shopGoods[i];
            var unit = _shopUnits[i];
            var priceT = UiFind.Deep(unit.transform, "商品名 (1)")?.GetComponent<Text>();
            if (priceT == null) continue;
            bool owned = ArenaShopGoods.IsOwned(data, good);
            bool soldOut = owned && good.repurchasePrice <= 0;   // 买断（弓）→ 变灰「已拥有」
            int price = ArenaShopGoods.PriceOf(data, good);
            priceT.text = soldOut ? "已拥有" : price.ToString();
            priceT.color = soldOut ? new Color(0.6f, 0.6f, 0.6f, 1f) : PurchasePriceStyle.NormalColor;
            PurchasePriceStyle.Apply(priceT);   // 2026-11-08：商店价格统一暖金 + 描边（已拥有灰不变）

            // 【2026-11-10 主人拍板「不能购买时应该是灰的，所有界面里都是，统一化」】
            // 判据与 OnShopUnitClicked / ArenaShopGoods.TryBuy 同源：买断 / 周限用完 / 满星 / 积分不足。
            bool weekFull = good.weeklyLimit > 0
                            && ArenaShopGoods.WeekBuyCount(data, good.id) >= good.weeklyLimit;
            bool fullStar = good.kind == ArenaShopGoods.GoodKind.Merc && owned
                            && !string.IsNullOrEmpty(good.hireId)
                            && MercStarSystem.OutStarOf(good.hireId) >= MercStarDefs.MaxOutStar;
            bool canBuy = !soldOut && !weekFull && !fullStar && data.arenaPoints >= price;
            var buyBtn = UiFind.Deep(unit.transform, "购买")?.GetComponent<Button>();
            if (buyBtn != null) PurchasePriceStyle.ApplyBuyable(buyBtn, canBuy);
            else Debug.LogError($"[Arena] 商品 {good.id} 缺「购买」按钮，不能购买时无法置灰");
        }
    }
    // ───────────────────────── 档案详情弹窗（已整条删除，2026-10-31 主人拍板） ─────────────────────────
    // 拍板记录：早「点其他玩家那个条无反应就行」→ ShowDetailForRow + 行 Button 删除；
    // 晚「点前三名的卡也不用弹信息」→ ShowDetailForRank + 领奖台卡 Button 删除；
    // 两个入口都收掉后机器人档案弹窗零调用 → ShowBotDetail / CloseDetail / BuildBotDetail /
    // FillLoadout / ClearDetailRows / SectionName 与 _detailRoot 等字段全部删除（铁律 10 不留死代码）。
    // 注意：ArenaRules.BuildBotLoadout / GetBotPower / RankOfIndex 仍在（数据侧，不在本文件职责内）。

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
        // 2026-11-08 主人报「这个底框的横条右边比例不对」「可以参考排行榜的横条」：
        // 原 _0008_条2 只有 83×33、meta 切片 28/10/32/12，代码却按均匀 10f 建 9-slice，
        // 右端装饰被划进拉伸区，再拉到 prefab 行的 488.1×60 → 右端比例变形。改排行榜行同款
        // _0009_条1（488×106，切片照 meta 68/38/79/36），与 BuildRows 同参数同缓存。
        var rowStripSp = LoadArt("_0009_条1", new Vector4(68f, 38f, 79f, 36f));
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
        // 同 ShowShop：页根「点空白关页」Button 不做按压反馈，防在记录列表上按下/拖动时整页缩一下
        UiButtonPressFeedback.Suppress(_recordsRoot);
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

        for (int i = 0; i < n; i++)
        {
            var rec = list[i];
            if (rec == null) continue;

            var row = Object.Instantiate(rowTmpl, _recordContent).GetComponent<RectTransform>();
            row.name = "Rec_" + i;
            row.anchoredPosition = new Vector2(0f, -(i * RecRowStep + 4f));
            row.gameObject.SetActive(true);

            // 2026-11-08 主人「对战记录不要横条了 只要字就行了」：行底条整块停用——不再 LoadArt 铺条，
            // 直接关掉行自身的 Image（prefab 里这颗 Image 还带着旧 sprite 引用，只不设图会露出底图，必须关 enabled）。
            // 子节点的字 / 头像 / 胜败章一律不动，不改预制体、不改任何尺寸与坐标。
            var rowImg = row.GetComponent<Image>();
            if (rowImg != null) rowImg.enabled = false;
            else Debug.LogError("[Arena] 记录行缺 Image（行底条关不掉，报主人）");

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
