using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 进关抽奖面板（2026-09-30 主人改版，替代原 SlotOfferUI 居中面板）。
///
/// <b>放哪</b>：战斗 HUD 底部的 <c>BackpackPanel</c> 里，不再单独弹居中遮罩面板。
/// <b>怎么出来</b>：整套展开/收起是<b>一张两态真值表</b>（2026-10-04 主人拉坐标拍板），
/// 见下面的 HeightCollapsed / ContentCollapsedY / DraftCollapsedPosY / MapCollapsedPosY 常量组。
/// 每个节点的两个值都由主人在团结里量过给死，代码只做线性插值，<b>不做任何推算</b>。
/// <remarks>
/// ⚠ 上一版是「从预制体读藏身位 + 按屏幕坐标反算展示位」，主人改完预制体后读到的已是<b>展开态的值</b>，
/// 算出来的展示位反而比当前位置更靠下，抽奖按钮永远冒不出来 —— 这就是主人报的那个 bug。
/// 现在两个值都写死在这里，与预制体当前摆的是哪一态无关。
/// </remarks>
///
/// <b>节拍</b>：
/// · 抽奖阶段自动展开到 750，露出 4 个抽奖按钮（普通/佣兵/装备/技能）+「继续」；
/// · 点「继续」→ 收回 600，隐藏抽奖按钮与「继续」，开战；
/// · 战斗中「背包」按钮保留，玩家点它可以再展开看装备（此时没有抽奖按钮）。
///
/// <b>职责边界</b>：本类只管这一块的展开/收起与按钮交互；
/// 抽奖业务与流程编排仍在 <c>BattleManager.CoStageEntryDraft</c>（与旧 SlotOfferUI 的回调契约一致）。
///
/// <b>预制体</b>：主人已摆好节点骨架、icon 图、name/金额 的位置与字体。
/// 代码只做三件事：给节点补 Button 让它能点、改 name/金额 的 text、控 600↔750 的高度。
/// 项目约定「UI 运行时补节点，别改预制体」—— 不往 .prefab 里落任何东西。
/// </summary>
public class BattleEntryDraftPanel : MonoBehaviour
{
    public static BattleEntryDraftPanel Instance { get; private set; }

    // ============================================================
    // 2026-10-04 主人拍板：BackpackPanel 收起 / 展开两态真值
    // 主人在团结里手改节点后逐个量的，代码一律按这两个值插值，不再推算。
    // ============================================================
    const float HeightCollapsed = 600f;
    const float HeightExpanded = 750f;

    /// <summary>
    /// 背包<b>内容</b>：展开时整体上移 80。
    /// 真源是 zhuangshi（收起 0 / 展开 80，主人给死）。
    /// ⚠ 内容是<b>整体</b>移动：zhuangshi 与同层的网格背包 GridContainer 必须走<b>同一个位移量</b>，
    /// 不许各动各的（2026-10-05 主人拍板：「整体移动 zhuangshi 这个节点，不要分开移动」）。
    /// zhuangshi 底下的 CharacterBar / SkillBar / zhuangbei / BtnBackpack 是它的子节点，
    /// 动父节点就会跟着走 —— 代码<b>绝不逐个去挪这些子节点</b>。
    /// </summary>
    const float ContentCollapsedY = 0f;
    const float ContentExpandedY = 80f;

    /// <summary>
    /// 抽奖容器 DraftRoot 的两态 Y —— <b>主人在团结 Inspector 里看到的「Pos Y」= anchoredPosition</b>。
    /// <para>预制体实测（只读核对，未改 prefab）：
    /// DraftRoot 锚点 <c>(0.5, 0.5)</c>、pivot <c>(0.5, 0.5)</c>、Pos Y <c>-400</c>；
    /// 父级 BackpackPanel 锚点 <c>(0.5, 0)</c>、pivot <c>(0.5, 0)</c> → 面板 rect 就是 <c>0~H</c>。
    /// 于是「Pos Y」与「离面板底边多少」的换算只有一条：<c>本地Y = 0.5 × 面板高 + PosY</c>。</para>
    /// <para>· 收起 <c>-400</c>：面板 600 → 本地 <c>-100</c>，整块压在面板底边下方，看不见；</para>
    /// <para>· 展开 <c>-250</c>：面板 750 → 本地 <c>125</c>，四个按钮（图标 140×190）落在
    ///   <c>30~220</c>，正好是内容上移后让出来的那条带（zhuangbei 在 224）。</para>
    /// <remarks>
    /// ⚠ 2026-10-05 踩坑（主人报「一个也没改」的那次）：上一版把 -400/-250 当成
    /// 「离面板底边的本地坐标」直接用，等于把按钮永远按在面板外面（图标落在 -295~-105）。
    /// 主人给的是 Inspector 的 Pos Y，换算时必须带上锚点 0.5 那半截面板高。
    /// ⚠ 又：竖屏适配（<c>UiLayoutStretch</c>）会在运行时把面板/DraftRoot 重锚成贴底
    /// （实测 amin.y 从 0.5 变 0），同一个 Pos Y 的含义就变了 —— 所以代码先算出「本地Y」，
    /// 再按<b>当时的锚点</b>反算回 Pos Y（见 <c>SetPivotLocalY</c>）：
    /// 锚点 0.5 时算出来的 Pos Y 恰好就是主人给的 -400 / -250，一一对得上，可作自检。
    /// </remarks>
    /// </summary>
    const float DraftAnchorY = 0.5f;
    const float DraftCollapsedPosY = -400f;
    const float DraftExpandedPosY = -250f;

    /// <summary>
    /// 场景 map 的两态参数 —— <b>2026-10-05 主人给的确定数值</b>：
    /// 默认（面板收起）在 <b>250</b>、scale <b>1.13</b>；面板展开后在 <b>350</b>、scale <b>1</b>。
    ///
    /// <remarks>
    /// <b>250 / 350 是「中心 Y」这一把尺子</b>（父级本地坐标，原点在父级中心），也就是
    /// Inspector 里的 <b>Pos Y</b>（点锚点）与 <c>anchoredPosition.y</c>（拉伸锚点）——
    /// 两种锚点下 <c>anchoredPosition.y</c> 的含义<b>完全一致</b>：
    /// <para>· 点锚点（预制体里就是这样：<c>anchor (0.5,0.5) / pivot (0.5,0.5) / Pos Y 361</c>）：
    ///   Pos Y = pivot 相对「父级中心」的偏移；</para>
    /// <para>· 竖向拉伸锚点（<c>UiLayoutStretch.ApplyBattleMapWidth</c> 第 286-290 行运行时把它改成
    ///   <c>(0,0)~(1,1)</c> 加上下 inset）：<c>anchoredPosition.y = (offsetMin.y + offsetMax.y) / 2</c>，
    ///   同样是中心相对父级中心的偏移。</para>
    /// 所以两态就按这一个数写，<b>不换算、不补偿</b>。
    ///
    /// ⚠⚠ <b>为什么不能再「抓当前值当收起态基准 + 抬 100」</b>（2026-10-05 主人报「怎么一开始就上去了」的根因）：
    /// 适配是在 <c>BattleUI.Start</c> 里跑的，那一刻 <c>BackpackPanel</c> 还是<b>预制体的展开态 750</b>
    /// （主人手调过，见 <c>Build()</c> 注释），于是适配算出来的 map 落点是「贴着 750 的面板顶」——
    /// <b>它本身就是展开态的值</b>。旧代码把这个值当成收起态基准，k=0 原样写回 → 一开局 map 就顶在上面，
    /// 再展开还多抬 100，就更离谱。
    /// 现在两态都写<b>主人给的绝对值</b>，与「适配当时面板是 600 还是 750」彻底无关。
    ///
    /// ⚠ 上一次踩的坑（别再犯）：把 250 当成「离父级底边多少」去换算成 <c>0.5×父高 + 250 ≈ 890</c>，
    /// 那把尺子完全不同，会把 map 顶飞。这里的数<b>就是</b> Pos Y / 中心 Y，直接写。
    /// </remarks>
    /// </summary>
    const float MapCollapsedPosY = 250f;
    const float MapExpandedPosY = 350f;
    const float MapCollapsedScale = 1.13f;
    const float MapExpandedScale = 1f;

    const float AnimSec = 0.25f;

    // BackpackPanel 下的节点名（2026-09-30 主人授权：节点名统一英文，方便 grep 与脚本批量处理）
    const string NodePanel = "BackpackPanel";
    /// <summary>背包内容容器（头像/技能/装备都在它下面），展开时整体上移。</summary>
    const string NodeContent = "zhuangshi";
    /// <summary>网格背包。与 zhuangshi 平级（同挂 BackpackPanel），必须跟着一起上移。</summary>
    const string NodeGrid = "GridContainer";
    /// <summary>场景。不在 BackpackPanel 下，是它的兄弟节点。</summary>
    const string NodeMap = "map";
    const string NodeDraftRoot = "DraftRoot";
    const string NodeContinue = "BtnContinue";
    const string NodeBackpack = "BtnBackpack";
    /// <summary>按钮下的价格 Text 节点。</summary>
    const string NodeCost = "Cost";

    // 按钮底色与文字颜色一律不动：icon 的图、name/金额 的字体和颜色都是主人在预制体里调好的，
    // 代码只负责改 text 和 interactable（置灰由 Button 的 disabledColor 作用在 icon 上）。

    /// <summary>
    /// 一个抽奖按钮：占位节点（主人摆好的 RectTransform）+ 运行时补的 Button。
    /// 名字与金额的两个 Text 是<b>主人在预制体里调好位置和字体的</b>，这里只改它们的 text，
    /// 绝不重建、绝不改字体颜色 —— 否则会把美术调好的东西冲掉。
    /// </summary>
    class DraftSlot
    {
        public RectTransform Root;
        public Button Btn;
        /// <summary>预制体里 btn/name 上的 Text：显示「普通 / 佣兵 / 装备 / 技能」。</summary>
        public Text NameText;
        /// <summary>预制体里 btn/name/金额 上的 Text：显示价格数字。</summary>
        public Text CostText;
        /// <summary>null = 普通（随机抽奖）；有值 = 该类型的定向抽奖。</summary>
        public DraftCategory? Cat;
        /// <summary>按钮左侧的货币图标（运行时建在 Cost 左外侧，见 BuildCoinIcon）。</summary>
        public Image CoinIcon;
    }

    /// <summary>币种图标缓存：别每次 Refresh 都 Resources.Load。</summary>
    static readonly Dictionary<ResourceWallet.ResourceType, Sprite> _currencyIcons =
        new Dictionary<ResourceWallet.ResourceType, Sprite>();

    /// <summary>
    /// 抽奖按钮上显示哪种币的图标 —— <b>单一出口</b>。
    /// 主人原话：「在抽奖的按钮左侧加个货币的图标 金币就加金币的 佣兵就加佣兵的，
    /// 目前只有佣兵是佣兵币 其他的都是金币 等其他三个抽奖按钮开放了再说」。
    /// 哪天其余按钮要换币，只改这一个函数，别在调用处各写一遍。
    /// <para>【2026-10-07 主人二次拍板】「<b>抽奖币默认金币</b>」—— 随机 / 装备 / 技能这几抽
    /// 真扣的是抽奖币 <c>SlotCoin</c>，但按钮上就显示<b>金币</b>图标（主人的口径，不是不一致）。
    /// 哪天要换成抽奖币自己的图，只改这一个函数。佣兵 = 佣兵币，与
    /// <c>SlotMachineSystem.FocusCurrency</c> 必须同值。</para>
    /// </summary>
    static ResourceWallet.ResourceType CurrencyOf(DraftCategory? cat)
    {
        return cat.HasValue && cat.Value == DraftCategory.Merc
            ? ResourceWallet.ResourceType.MercGold
            : ResourceWallet.ResourceType.Gold;
    }

    /// <summary>币种 → 图标资源路径（单一出口，别处不许再抄一遍路径）。</summary>
    static string CurrencyIconPath(ResourceWallet.ResourceType t)
    {
        switch (t)
        {
            case ResourceWallet.ResourceType.MercGold: return "UI/Icons/Common/icon_yongbinggold";
            case ResourceWallet.ResourceType.Gold: return "UI/Icons/Common/icon_gold";
            default: return null;
        }
    }

    /// <summary>
    /// 取币种图标。缺图 → LogError + 返回 null（铁律 14：兜底 fail closed，
    /// 绝不静默拿别的图顶上，免得玩家看着金币图标其实扣的是佣兵币）。
    /// </summary>
    static Sprite GetCurrencyIcon(ResourceWallet.ResourceType t)
    {
        Sprite s;
        if (_currencyIcons.TryGetValue(t, out s)) return s;

        string path = CurrencyIconPath(t);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("[BattleEntryDraftPanel] 币种没配图标路径: " + t);
            _currencyIcons[t] = null;
            return null;
        }
        s = Resources.Load<Sprite>(path);
        if (s == null)
            Debug.LogError("[BattleEntryDraftPanel] 缺货币图标 Resources/" + path + ".png（把 Art 下同名图复制进 Resources）");
        _currencyIcons[t] = s;
        return s;
    }

    /// <summary>
    /// 在价格文本的<b>左外侧</b>建一个货币图标 —— 主人原话「在抽奖的按钮左侧加个货币的图标」。
    /// 做成 Cost 的子节点：Cost 的位置/字体是主人在预制体里调好的，这里一个字都不改，
    /// 图标挂上去跟着它走，也不会盖到 icon / name。
    /// </summary>
    void BuildCoinIcon(DraftSlot slot, Transform costNode)
    {
        var costRt = costNode as RectTransform;
        if (costRt == null) return;

        var go = new GameObject("CoinIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(costNode, false);

        var rt = go.GetComponent<RectTransform>();
        // 锚在 Cost 的左中，pivot 取右中 → 整个图标落在 Cost 左边外面。
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        float size = Mathf.Max(24f, costRt.rect.height * 0.9f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = new Vector2(-6f, 0f);

        var img = go.GetComponent<Image>();
        img.raycastTarget = false;                 // 别挡按钮点击
        img.preserveAspect = true;                 // 背景/底图绝不拉伸
        img.sprite = GetCurrencyIcon(CurrencyOf(slot.Cat));
        // 显隐的统一出口在 Refresh（免费抽要隐）；这里只按「有没有图」先定一次初始态。
        go.SetActive(img.sprite != null);
        slot.CoinIcon = img;
    }

    readonly List<DraftSlot> _slots = new List<DraftSlot>();
    /// <summary>跟着内容一起上移的节点（zhuangshi 之外的同伴，如网格背包 GridContainer）。</summary>
    readonly List<ContentFollower> _followers = new List<ContentFollower>();

    /// <summary>内容同伴：记下它没被动过时的 y，展开时按同一位移量一起走。</summary>
    class ContentFollower
    {
        public RectTransform Rt;
        public float BaseY;
    }

    RectTransform _panel;
    Transform _draftRoot;
    /// <summary>抽奖容器的 RectTransform —— 展示/收起时单独挪它。</summary>
    RectTransform _draftRt;
    /// <summary>背包内容容器 zhuangshi，展开时整体上移（真源）。</summary>
    RectTransform _contentRt;
    /// <summary>场景（BackpackPanel 的兄弟层），展开时按面板本地坐标上移并把缩放放大。</summary>
    RectTransform _mapRt;
    bool _mapBaseLogged;
    Coroutine _animCo;
    /// <summary>
    /// 【2026-10-06 主人拍板】入场归位协程：把适配摆在展开位（实测 361 / scale 1.0）的 map
    /// 平滑滑回收起位（250 / scale 1.13），避免「一进战斗 map 啪地跳一下」。
    /// 它在跑的时候 <c>LateUpdate</c> 让路，绝不与两态钳制同时写 map。
    /// </summary>
    Coroutine _settleCo;
    /// <summary>0 = 收起，1 = 展开。整套两态只有这一个状态出口。</summary>
    float _animK;
    Button _continueBtn;
    Button _backpackBtn;
    /// <summary>概率公示的「!」按钮（运行时建，见 BuildOddsButton）。</summary>
    Button _oddsBtn;

    // ===== 抽完后「等玩家点继续」的两件套（2026-10-06 主人拍板）=====

    /// <summary>
    /// 抽完之后「本拍不能再抽」的锁 —— 只影响按钮可用性（置灰），不动显隐、不动美术。
    /// 引导局抽完要锁（一次一抽）；正式关连抽是玩法，<b>不锁</b>。
    /// </summary>
    bool _drawLocked;

    /// <summary>上一次 <see cref="Refresh"/> 的参数：锁抽时要按同一组参数重刷按钮，不能另算一套。</summary>
    int _lastRandomPrice;
    int _lastFocusPrice;
    int _lastFreeLeft;
    long _lastCoins;
    List<DraftCategory> _lastCats;

    /// <summary>
    /// 【2026-10-06 二次拍板】「继续」倒计时总时长（秒）。
    /// 起表点<b>只有 <see cref="Begin"/> 一处</b> —— 面板一露头就开始走，
    /// 抽奖全程连续递减；<b>抽完不重置</b>（主人：「不是说过抽奖的时候就开始计时吗」）。
    /// </summary>
    public const float ContinueCountdownSec = 30f;

    /// <summary>
    /// 倒计时数字字号。【2026-10-06 主人校准】摆到 map 正中间后要「放大点」才显眼。
    /// 要大小只改这一个数。
    /// </summary>
    const int CountdownFontSize = 120;

    /// <summary>倒计时剩余秒数。&gt;0 表示在跑；0 表示没在跑。</summary>
    float _countdownLeft;
    /// <summary>
    /// 冻结开关：抽奖结算（老虎机 / 三选一 / 装备替换弹窗）期间为 true。
    /// <b>只冻结、不清零</b> —— 结算完接着走剩下的秒数，绝不给它偷偷补满。
    /// </summary>
    bool _countdownPaused;
    Text _countdownText;

    Action<DraftCategory?> _onPick;
    Action _onFight;
    bool _expanded;
    bool _built;
    /// <summary>抽奖阶段（此时抽奖按钮可用）；开战后为 false。</summary>
    bool _draftActive;
    /// <summary>
    /// 上一帧的 <c>BattleManager.UnitsCanAct</c> —— 开打自动收起背包做<b>边缘检测</b>要用，
    /// 只有 false→true 那一跳才触发，绝不每帧强制收起（2026-10-09 主人拍板）。
    /// </summary>
    bool _prevUnitsCanAct;

    /// <summary>「普通」（随机）按钮的 RectTransform —— 引导关要圈住它做高亮。</summary>
    public RectTransform NormalButtonRect =>
        _slots.Count > 0 && _slots[0].Root != null ? _slots[0].Root : null;

    /// <summary>
    /// 「继续」按钮的 RectTransform —— 抽完奖励后引导要圈住它（2026-10-06 主人拍板：
    /// 抽完不许自动继续，必须玩家点「继续」）。
    /// </summary>
    public RectTransform ContinueButtonRect =>
        _continueBtn != null ? _continueBtn.transform as RectTransform : null;

    /// <summary>
    /// 抽奖面板是否处于<b>展开</b>态 —— 遮罩 <c>zhezhao</c> 该不该压暗底部 HUD 的<b>唯一判据</b>。
    /// <c>BattleUI.TickBattleMask()</c> 每帧来问这一个值；本类自己一行都不碰 zhezhao，
    /// 不许再出现第二个地方改它的显隐（2026-10-05 主人拍板）。
    /// <para>收回动画跑完前（<c>_animK</c> 还没归 0）也算「开着」，
    /// 否则遮罩会在面板还在往下收的时候提前压回来，末帧闪一下。</para>
    /// </summary>
    public static bool IsLotteryOpen =>
        Instance != null && (Instance._expanded || Instance._animK > 0.001f);

    // ============================================================
    // 对外接口（与旧 SlotOfferUI 的签名保持一致，BattleManager 只需换类名）
    // ============================================================

    /// <summary>
    /// 展开面板并显示抽奖按钮。
    /// <paramref name="onPick"/>：null = 点了普通（随机），有值 = 点了该类型定向。
    /// <paramref name="onFight"/>：点了「继续」。
    /// <paramref name="freeLeft"/>：本关还剩几次**免费**抽（金币本开局送的那几次）。0 = 没有，正常按价扣币。
    /// </summary>
    public static void Show(int randomPrice, int focusPrice, List<DraftCategory> cats, long coins,
                            Action<DraftCategory?> onPick, Action onFight, int freeLeft = 0)
    {
        var ui = Ensure();
        if (ui == null)
        {
            // 拿不到面板就直接开战，绝不把玩家卡在抽奖阶段（与旧 SlotOfferUI 一致）
            onFight?.Invoke();
            return;
        }
        ui.Begin(randomPrice, focusPrice, cats, coins, onPick, onFight, freeLeft);
    }

    /// <summary>收起面板并隐藏抽奖按钮（开战/超时都走这里）。</summary>
    public static void Hide()
    {
        if (Instance == null) return;
        Instance.Finish();
    }

    static BattleEntryDraftPanel Ensure()
    {
        if (Instance != null) return Instance;
        var ui = BattleUI.Instance;
        if (ui == null)
        {
            Debug.LogError("[BattleEntryDraftPanel] BattleUI 不存在，抽奖面板无法挂载");
            return null;
        }
        var panel = FindDeep(ui.transform, NodePanel);
        if (panel == null)
        {
            Debug.LogError("[BattleEntryDraftPanel] 找不到节点 BackpackPanel");
            return null;
        }
        var go = new GameObject("BattleEntryDraftPanel", typeof(RectTransform));
        go.transform.SetParent(panel, false);
        var c = go.AddComponent<BattleEntryDraftPanel>();
        c._panel = panel as RectTransform;
        // 必须在任何一次 ApplyState 之前：此时 UI 适配已跑完、面板还没被本类动过，
        // 打出来的对照值才是「适配给的基准」。
        c._mapRt = c.ResolveMap();
        c.LogMapBaseOnce();
        // 【2026-10-06 主人拍板】组件一出生就把 map 往主人给的收起态（中心Y 250 / scale 1.13）落。
        // 原因：适配是按「那一刻面板多高」算的，而那一刻面板还停在预制体的展开态 750，
        // 算出来的 map 落点本身就是展开位 —— 这就是主人报「一上来就跑到上面」的根因。
        // 这里补一次落位：Build / Show 还没跑到的时候，map 就已经在往收起位走。
        // 【2026-10-06 主人二次拍板】不要瞬移 —— 走 SettleMapToCollapsed 平滑滑过去。
        c.SettleMapToCollapsed();
        return c;
    }

    /// <summary>
    /// 【2026-10-06 主人拍板 · 方案 A】战斗 UI 就绪后<b>立刻</b>把本组件建出来，不再等 <c>Show()</c>。
    /// <para>为什么非要这一步：<c>Ensure()</c> 原本只在 <c>Show()</c>（抽奖弹面板）里调，
    /// 于是「战斗一开始还没抽奖」的那段时间内 <b>本类实例根本不存在</b> →
    /// <c>LateUpdate</c> 不跑 → 没有任何人把 map 从适配给的展开位 361 拉回收起位 250
    /// → 主人看到的「map 一上来就在上面」。</para>
    /// <para>本方法<b>只建组件、不显示任何东西</b>（显隐仍归 <c>Begin/Finish</c>），
    /// 创建出口仍是 <c>Ensure()</c> 一个，这里只是提前调它。</para>
    /// </summary>
    public static void Prewarm()
    {
        if (BattleUI.Instance == null) return;
        // 【2026-10-09 主人拍板】这里必须补一次 Build()：Build 只在 Begin()（抽奖开始）里跑，
        // 于是「还没抽奖的那段时间」面板一直停在预制体存的展开态（750 / zhuangshi +80），
        // 且 BtnBackpack 的 onClick 还没绑 —— 就是主人报的「一上来背包是打开的」「点了关不掉」。
        // Build() 末尾的 SetDraftVisible(false) + ApplyState(0f) 正好把面板拍回收起态，
        // 只建不显示，与本方法「只建组件、不显示任何东西」的语义一致；它有 _built 守卫，重复调无害。
        var pre = Ensure();
        if (pre != null) pre.Build();
    }

    RectTransform ResolveMap()
    {
        var root = _panel != null ? _panel.parent : null;
        if (root == null) return null;
        var map = root.Find(NodeMap) as RectTransform;
        if (map == null)
            Debug.LogError("[BattleEntryDraftPanel] 找不到场景节点 " + NodeMap + "，展开时场景不动");
        return map;
    }

    /// <summary>
    /// map 的两态：<b>中心 Y = 250 → 350</b>（主人给的绝对值），<b>缩放 = 1.13 → 1</b>。
    ///
    /// <para>读位置恒用 <c>anchoredPosition.y</c> —— 点锚点与拉伸锚点下它都是「中心相对父级中心的偏移」，
    /// 是同一把尺子（见 <c>MapCollapsedPosY</c> 处注释）。</para>
    ///
    /// <para>写位置分两种锚点：<br/>
    /// · 竖向拉伸（<c>anchorMin.y != anchorMax.y</c>）：按差值推 <c>offsetMin/offsetMax</c> 整体平移，
    ///   绝不写 <c>anchoredPosition.y</c>（它是派生的，写了会被反算覆盖）；<br/>
    /// · 点锚点：直接写 <c>anchoredPosition.y</c>。</para>
    ///
    /// <para>每帧都按「目标 - 当前」算差值，所以适配/别处什么时候改了 map，下一帧都会被拉回主人给的两态，
    /// 不需要缓存基准，也不会有累积漂移。</para>
    /// </summary>
    void ApplyMapState(float k)
    {
        WriteMapTransform(
            Mathf.Lerp(MapCollapsedPosY, MapExpandedPosY, k),
            Mathf.Lerp(MapCollapsedScale, MapExpandedScale, k));
    }

    /// <summary>
    /// 写 map 的<b>唯一出口</b>：给一对绝对值（中心 Y + 缩放），由它按<b>当时的锚点</b>落笔。
    /// <para>· 点锚点：直接写 <c>anchoredPosition.y</c>；</para>
    /// <para>· 竖向拉伸锚点（<c>UiLayoutStretch.ApplyBattleMapWidth</c> 会把它改成 (0,0)~(1,1)）：
    ///   <c>anchoredPosition.y</c> 是派生的，写了会被反算覆盖 —— 必须按差值推
    ///   <c>offsetMin/offsetMax</c> 整体平移。</para>
    /// 每帧都按「目标 − 当前」算差值，所以别处什么时候改了 map，下一帧都会被拉回本类给的值，
    /// 不需要缓存基准，也不会有累积漂移。
    /// </summary>
    void WriteMapTransform(float targetY, float scale)
    {
        if (_mapRt == null) return;

        float d = targetY - _mapRt.anchoredPosition.y;
        if (Mathf.Abs(d) > 0.01f)
        {
            if (!Mathf.Approximately(_mapRt.anchorMin.y, _mapRt.anchorMax.y))
            {
                _mapRt.offsetMin = new Vector2(_mapRt.offsetMin.x, _mapRt.offsetMin.y + d);
                _mapRt.offsetMax = new Vector2(_mapRt.offsetMax.x, _mapRt.offsetMax.y + d);
            }
            else
            {
                _mapRt.anchoredPosition = new Vector2(_mapRt.anchoredPosition.x, targetY);
            }
        }
        _mapRt.localScale = Vector3.one * scale;
    }

    /// <summary>
    /// 【2026-10-06 主人拍板】map 落到收起态时<b>不许瞬移</b> —— 主人原话「往上移动或往下移动加个效果，不要突然出来」。
    /// <para>为什么需要它：竖屏适配把 map 摆在 <b>361 / scale 1.0</b>（展开位），
    /// 而主人定的收起态是 <b>250 / scale 1.13</b>。直接写目标值就会「啪」地跳一下。
    /// 这里从<b>当前实际位置</b>起算，用与展开/收起同一段时长（<c>AnimSec</c>）平滑滑回收起位。</para>
    /// <para>已经在收起位（差值小于半个像素）就不需要动，直接落位，不起协程。</para>
    /// </summary>
    void SettleMapToCollapsed()
    {
        if (_mapRt == null) return;

        bool atRest = Mathf.Abs(_mapRt.anchoredPosition.y - MapCollapsedPosY) < 0.5f
                      && Mathf.Abs(_mapRt.localScale.x - MapCollapsedScale) < 0.001f;
        if (atRest)
        {
            ApplyMapState(0f);
            return;
        }
        if (_settleCo != null) StopCoroutine(_settleCo);
        _settleCo = StartCoroutine(CoSettleMap());
    }

    IEnumerator CoSettleMap()
    {
        float fromY = _mapRt.anchoredPosition.y;
        float fromS = _mapRt.localScale.x;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / AnimSec;
            float e = Mathf.Clamp01(t);
            WriteMapTransform(Mathf.Lerp(fromY, MapCollapsedPosY, e),
                              Mathf.Lerp(fromS, MapCollapsedScale, e));
            yield return null;
        }
        _settleCo = null;
        ApplyMapState(0f);
    }

    /// <summary>
    /// 第一次碰 map 之前打一行<b>对照日志</b>：主人拍板的默认态是 <b>中心Y 250 / scale 1.13</b>。
    /// 这里报的是「适配刚跑完、本类还没动过」时的真实值，用来对账：
    /// 若 <c>PosY</c> 明显大于 250，就说明适配是拿展开态（面板 750）算的 —— 正是主人报
    /// 「怎么一开始就上去了」的那次；现在代码一律按 250/350 绝对值写，不再受它影响。
    /// <remarks>只报不改，<b>代码绝不私自补偿</b>。</remarks>
    /// </summary>
    void LogMapBaseOnce()
    {
        if (_mapBaseLogged || _mapRt == null) return;
        _mapBaseLogged = true;
        bool stretch = !Mathf.Approximately(_mapRt.anchorMin.y, _mapRt.anchorMax.y);
        Debug.Log($"[BattleEntryDraftPanel] map 对照：主人定 收起 中心Y={MapCollapsedPosY:F0}/scale {MapCollapsedScale}" +
                  $" 展开 中心Y={MapExpandedPosY:F0}/scale {MapExpandedScale}；" +
                  $"适配后实测 anchorMin.y={_mapRt.anchorMin.y:F2} anchorMax.y={_mapRt.anchorMax.y:F2}" +
                  $"{(stretch ? "（拉伸锚点）" : "（点锚点）")}" +
                  $" 中心Y(PosY)={_mapRt.anchoredPosition.y:F0}" +
                  $" 底={_mapRt.offsetMin.y:F0} 顶={-_mapRt.offsetMax.y:F0}" +
                  $" scale={_mapRt.localScale.x:F3}");
    }

    /// <summary>
    /// 【2026-10-06 主人拍板】map 的纵向位置与缩放<b>由本类独占</b>：静止时按当前进度 <c>_animK</c> 写回去。
    ///
    /// <para>为什么非要这一步：竖屏适配 <c>BattleViewportFit.Apply</c>（实际是
    /// <c>UiLayoutStretch.ApplyBattleMapWidth</c>）会把 map 重锚成「顶栏底 ~ 背包顶」的拉伸锚点，
    /// <b>它只认跑的那一刻面板多高</b>；那一刻面板常常还停在展开态 750（预制体就是这么摆的），
    /// 于是算出来的 map 落点本身就是展开位 —— 主人报「map 一上来就在展开位置、跑到上面了」的根因。
    /// 而这套适配<b>随时可能再跑一次</b>（<c>BattleUI.Start</c> 与
    /// <c>AutoGameInitializer</c> 都在调），跑完 map 就又飘去展开位，
    /// 要等下一次 <c>SetExpanded</c> 才被拉回来。</para>
    ///
    /// <para>所以静止态每帧按当前 k 写一次 —— 与本类同一个出口（<c>ApplyMapState</c>）、
    /// 同一组真值（250/1.13 ↔ 350/1），不另立一套；动画正在跑的那一帧交给动画协程，
    /// 绝不两个地方同时写。</para>
    /// </summary>
    void LateUpdate()
    {
        // 倒计时每帧走：与本类两态动画、入场归位互不干涉，各自独立。
        TickContinueCountdown();

        // 【2026-10-09 主人拍板】开打那一刻自动收起背包：只认 UnitsCanAct 的 false→true 跳变，
        // 且必须是玩家手动点开的（抽屉开着、不在抽奖阶段 _draftActive）。
        // ⚠ 绝不写成「战斗中每帧强制收起」——那会把主人 10-08 拍板保留的「战斗中点开背包查看」废掉。
        // 用 InstanceQuiet 静默取实例，系统未就绪时不刷 Error。
        // ⚠ 判据从 _expanded 换成 BattleUI.Instance.BagDrawerOpen：背包按钮现在开的是网格背包抽屉，
        // 不再是 600↔750 两态（_expanded 只反映抽奖展开）。
        var bm = BattleManager.InstanceQuiet;
        bool canAct = bm != null && bm.UnitsCanAct;
        if (_built && canAct && !_prevUnitsCanAct && !_draftActive
            && BattleUI.Instance != null && BattleUI.Instance.BagDrawerOpen)
            BattleUI.Instance?.CloseBagDrawer();
        // 每帧无条件更新，不放在上面的早退之后
        _prevUnitsCanAct = canAct;

        // 入场归位（_settleCo）在跑时也让路：同一时刻只有一处写 map。
        if (_mapRt == null || _animCo != null || _settleCo != null) return;
        ApplyMapState(_animK);
    }

    void Awake() { Instance = this; }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ============================================================
    // 内部
    // ============================================================

    void Begin(int randomPrice, int focusPrice, List<DraftCategory> cats, long coins,
               Action<DraftCategory?> onPick, Action onFight, int freeLeft = 0)
    {
        Build();
        // 【2026-10-08 主人拍板】玩家手动点开的「看背包」态：抽奖一开始先收回去，
        // 再由下面 SetExpanded(true) 从收起态平滑展开显示抽奖 —— 不让背包和抽奖叠在一起。
        // ⚠ 判据必须放在 _draftActive = true 之前：否则连抽中途回面板也会被当成「玩家开的」。
        if (_expanded && !_draftActive)
        {
            SetExpanded(false);
            ApplyState(0f);   // 立刻归位到收起态（不等动画走完），下面展开才有「收 → 展」的观感
        }
        // 【2026-10-06 主人二次拍板】这里是倒计时<b>唯一</b>的起表点：
        // 面板一露头（= 抽奖环节开始）就开始走 30s，**不是抽完才开始**；
        // 抽奖全程连续递减，抽完<b>不重置</b>（原来 CoStageEntryDraft 在抽完又起一次表，
        // 玩家看到的就是「数字跳回 30 重新开始」—— 主人报「怎么还是抽完才开始计时」的根因）。
        // 归零 = 玩家挂着没动 → 当作点了「继续」。
        // 连抽中途回面板只是<b>解冻</b>（SetCountdownPaused(false)），不补满 —— 总预算就是这 30s。
        _drawLocked = false;
        StartContinueCountdown();
        _onPick = onPick;
        _onFight = onFight;
        _draftActive = true;

        SetDraftVisible(true);
        SetExpanded(true);
        Refresh(randomPrice, focusPrice, cats, coins, freeLeft);
        LogState("Begin");
    }

    void Finish()
    {
        // 面板收了，倒计时也得停 —— 不然协程还挂在已经隐藏的面板上，归零会再触发一次「继续」
        StopContinueCountdown();
        _onPick = null;
        _onFight = null;
        _draftActive = false;
        SetDraftVisible(false);
        SetExpanded(false);
        // 面板收了，概率弹窗也得跟着收 —— 不然战斗画面里会留一个孤儿弹窗
        DraftOddsPopupUI.Hide();
    }

    /// <summary>
    /// 建按钮索引：4 个占位节点（主人摆好的 RectTransform，图在子节点 icon 上）
    /// 各补一个 Button，并把 name / 金额 两个 Text 的引用抓到手。
    /// 不新建 Image、不新建 Text —— 那些是主人在预制体里调好的美术。
    /// </summary>
    void Build()
    {
        if (_built) return;
        _built = true;

        if (_panel == null) return;
        _draftRoot = FindDeep(_panel, NodeDraftRoot);
        var content = FindDeep(_panel, NodeContent);
        if (content == null)
            Debug.LogError("[BattleEntryDraftPanel] 找不到内容节点 BackpackPanel/" + NodeContent);
        else
            _contentRt = content as RectTransform;

        // 网格背包与 zhuangshi 同层，记下它没被动过时的 y —— 展开时按<b>同一个位移量</b>跟着走。
        // 不这么做就变成「zhuangshi 动了、网格背包没动」，主人要的整体移动就散了。
        var grid = FindDeep(_panel, NodeGrid) as RectTransform;
        if (grid == null)
            Debug.LogError("[BattleEntryDraftPanel] 找不到网格背包节点 BackpackPanel/" + NodeGrid);
        else
            _followers.Add(new ContentFollower { Rt = grid, BaseY = grid.anchoredPosition.y });

        if (_draftRoot == null)
        {
            Debug.LogError("[BattleEntryDraftPanel] 找不到抽奖容器 BackpackPanel/" + NodeDraftRoot);
        }
        else
        {
            _draftRt = _draftRoot as RectTransform;
            // 顺序与预制体里主人摆的一致：普通 / 佣兵 / 装备 / 技能
            AddSlot("BtnNormal", null);
            AddSlot("BtnMerc", DraftCategory.Merc);
            AddSlot("BtnEquip", DraftCategory.Equip);
            AddSlot("BtnSkill", DraftCategory.Skill);

            // 概率公示的「!」（见 BuildOddsButton 注释）
            BuildOddsButton();
        }

        var cont = FindDeep(_panel, NodeContinue);
        if (cont == null)
        {
            Debug.LogError("[BattleEntryDraftPanel] 找不到「继续」按钮节点");
        }
        else
        {
            _continueBtn = EnsureClickable(cont);
            // 2026-10-04 主人拍板：「继续」按钮不要文字，只留美术图标。
            // 不管预制体自带还是别处补的 Text，一律关掉 —— 不留第二个文字入口。
            var contTexts = cont.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < contTexts.Length; i++)
                contTexts[i].gameObject.SetActive(false);
            if (_continueBtn != null) _continueBtn.onClick.AddListener(OnContinueClicked);
        }

        var bp = FindDeep(_panel, NodeBackpack);
        if (bp == null)
        {
            Debug.LogError("[BattleEntryDraftPanel] 找不到「背包」按钮节点");
        }
        else
        {
            // 背包是纯图标按钮，不加文字
            _backpackBtn = EnsureClickable(bp);
            if (_backpackBtn != null) _backpackBtn.onClick.AddListener(OnBackpackClicked);
        }

        // 预制体此刻存的是主人手调出来的<b>展开态</b>（面板 750 / zhuangshi 80 / DraftRoot -239）。
        // 先按真值表拍回收起态，之后 Begin→SetExpanded(true) 才能从 0 平滑动到 1；
        // 不拍这一步，第一次展开会先把面板从 750 抽回 600 再展开，看着就是「跳一下」。
        SetDraftVisible(false);
        ApplyState(0f);
    }

    /// <summary>
    /// 在一个占位节点上补出可点的抽奖按钮。
    /// 名字 / 金额两个 Text 走预制体里主人调好的，这里只拿到引用，不重建。
    /// </summary>
    void AddSlot(string nodeName, DraftCategory? cat)
    {
        var t = FindDeep(_draftRoot, nodeName);
        if (t == null)
        {
            Debug.LogError("[BattleEntryDraftPanel] 抽奖容器下找不到占位节点: " + nodeName);
            return;
        }

        var rt0 = t as RectTransform;

        // 【2026-10-07 主人拍板】定向招募 = 装备 / 佣兵 / 技能 三个按钮，**通过第一章后才开放**；
        // 没过第一章只留「普通」（随机）抽奖。
        // 平时用「隐藏」而不是「置灰」：置灰还会占位、玩家点了才知道不能抽，隐藏更干净。
        // 只在这里动显隐（单一出口），SetDraftVisible 管的是整个容器，不会把它们再打开。
        bool focusUnlocked = SaveSystem.Instance?.Data?.HasClearedChapter(1) == true;
        if (cat.HasValue)
        {
            t.gameObject.SetActive(focusUnlocked);
        }
        else if (!focusUnlocked && rt0 != null)
        {
            // 四个按钮原本摆在 x = -255 / -85 / 85 / 255。没过第一章只剩第一个时会偏在最左边
            // —— 把它挪到容器中间（x = 0）；解锁后<b>一个坐标都不动</b>，直接用主人摆好的四格。
            rt0.anchoredPosition = new Vector2(0f, rt0.anchoredPosition.y);
        }

        var slot = new DraftSlot { Root = t as RectTransform, Cat = cat };
        int idx = _slots.Count;

        slot.Btn = EnsureClickable(t);

        var nameNode = FindDeep(t, "name");
        if (nameNode != null) slot.NameText = nameNode.GetComponent<Text>();
        // 价格节点是按钮的<b>直接</b>子节点，跟 icon / name 平级 —— 不是在 name 下面。
        // 早期写成 FindDeep(nameNode, ...) 导致永远拿不到引用、价格数字不显示。
        var costNode = FindDeep(t, NodeCost);
        if (costNode != null) slot.CostText = costNode.GetComponent<Text>();

        // 按钮左侧的货币图标（2026-10-07 主人拍板）：建在 Cost 左外侧，不动主人调好的美术。
        if (costNode != null) BuildCoinIcon(slot, costNode);
        else Debug.LogError("[BattleEntryDraftPanel] 抽奖按钮 " + nodeName + " 找不到价格节点 " + NodeCost + "，货币图标没地方挂");

        if (slot.Btn != null)
        {
            int captured = idx;
            slot.Btn.onClick.AddListener(() => OnDraftClicked(captured));
        }
        _slots.Add(slot);
    }

    /// <summary>
    /// 概率公示的「!」按钮：贴在「普通抽奖」按钮右边，点了弹 <see cref="DraftOddsPopupUI"/>。
    ///
    /// 2026-10-05 主人拍板：「概率需要告诉玩家……可以再（抽）按钮旁边加个感叹号之类的，
    /// 点击弹出弹窗上面写的概率」「可以加到 DraftRoot 节点里」。预制体里没有这个节点
    ///（主人没摆，也不许我们改 prefab），所以运行时建一个挂到 <c>DraftRoot</c> 下 ——
    /// 跟抽奖按钮<b>同层</b>，抽奖按钮自己被置灰 / 缩放 / 换锚点都不会带着「!」一起跑。
    ///
    /// ⚠ 只做「一个圆底 + 一个 ! 字」，不碰主人在预制体里调好的任何美术。
    /// </summary>
    void BuildOddsButton()
    {
        if (_draftRt == null || _slots.Count == 0) return;
        var normal = _slots[0].Root;
        if (normal == null) return;

        var go = new GameObject("BtnOdds", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(_draftRt, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(58f, 58f);
        // 用世界角点换成容器本地坐标，抽奖按钮是什么锚点都不影响（直接读 anchoredPosition 会被锚点坑）。
        // 落点 = 抽奖按钮中心 + 半个按钮宽 + 38，跟抽奖按钮同高。
        var corners = new Vector3[4];
        normal.GetWorldCorners(corners);
        Vector2 center = _draftRt.InverseTransformPoint((corners[0] + corners[2]) * 0.5f);
        Vector2 right = _draftRt.InverseTransformPoint(corners[2]);
        float halfW = Mathf.Abs(right.x - center.x);
        rt.anchoredPosition = new Vector2(center.x + halfW + 38f, center.y);

        var img = go.GetComponent<Image>();
        img.color = new Color(0.14f, 0.15f, 0.20f, 0.92f);
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => DraftOddsPopupUI.Toggle());

        var txt = new GameObject("Txt", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        txt.transform.SetParent(go.transform, false);
        var trt = txt.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        var t = txt.GetComponent<Text>();
        t.text = "!";
        t.fontSize = 38;
        t.color = new Color(1f, 0.88f, 0.45f);
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
        var f = GameFonts.GetChinese();
        if (f != null) t.font = f;

        _oddsBtn = btn;
    }

    /// <summary>
    /// 给已有节点补 Button，让它能点。
    /// 优先用节点自己的 Image 做点击反馈；本体没有 Image 时（抽奖按钮就是这种，
    /// 图在子节点 icon 上）用 icon 的 Image —— <b>绝不在本体上硬加 Image</b>，
    /// 否则会多出一块白底盖住美术。
    /// 注意：不创建任何 Text，名字/金额都是主人在预制体里调好的。
    /// </summary>
    static Button EnsureClickable(Transform t)
    {
        var btn = t.GetComponent<Button>();
        if (btn == null) btn = t.gameObject.AddComponent<Button>();
        var g = t.GetComponent<Image>();
        if (g == null)
        {
            var icon = FindDeep(t, "icon");
            if (icon != null) g = icon.GetComponent<Image>();
        }
        if (g != null) btn.targetGraphic = g;
        return btn;
    }

    /// <summary>
    /// 刷新价格与可用状态：池子空或币不够 → 置灰，绝不自动扣钱。
    /// <para>⚠ <paramref name="focusPrice"/> <b>已不再使用</b>（2026-10-07）：定向价改由循环里按
    /// <c>slot.Cat</c> 各自的币种算（佣兵 = 佣兵币价，装备 / 技能 = 抽奖币价），
    /// 参数保留只是为了不动三个调用点的签名。<paramref name="coins"/> 只用于「普通」随机按钮。</para>
    /// </summary>
    void Refresh(int randomPrice, int focusPrice, List<DraftCategory> cats, long coins, int freeLeft = 0)
    {
        // 记下这一组参数：抽完锁按钮时要按同一组重刷，绝不另算一套（2026-10-06）
        _lastRandomPrice = randomPrice;
        _lastFocusPrice = focusPrice;
        _lastFreeLeft = freeLeft;
        _lastCoins = coins;
        _lastCats = cats;

        // 2026-10-05：金币本开局送的免费抽 —— 还有额度时按钮上写「免费 ×N」，不看余额也能点。
        bool free = freeLeft > 0;
        for (int i = 0; i < _slots.Count; i++)
        {
            var s = _slots[i];
            if (s.Btn == null) continue;
            bool isRandom = !s.Cat.HasValue;
            bool has = isRandom || (cats != null && cats.Contains(s.Cat.Value));
            // 价格与余额都按<b>各自币种</b>算（真源 SlotMachineSystem，单出口）：
            // 佣兵定向 = 佣兵币的价与余额；装备/技能定向 = 抽奖币的价与余额。
            int price = isRandom ? randomPrice : SlotMachineSystem.FocusPrice(s.Cat.Value);
            long wallet = isRandom
                ? coins
                : SlotMachineSystem.Balance(SlotMachineSystem.FocusCurrency(s.Cat.Value));
            // 2026-10-06：本拍已抽完（引导局一次一抽）→ 全部置灰，玩家只剩「继续」可点。
            bool on = !_drawLocked && has && (free || wallet >= price);
            // 置灰交给 Button.interactable —— 它会按 disabledColor 作用在 icon 上；
            // 不去改 name/金额 的文字颜色，那是主人调好的美术。
            s.Btn.interactable = on;
            if (s.NameText != null) s.NameText.text = Name(s.Cat);
            if (s.CostText != null)
                s.CostText.text = !has ? "-"
                               : free ? $"免费×{freeLeft}"
                               : price.ToString();
            // 免费抽不扣任何币 → 货币图标跟着一起隐（要改成「一直显示」只改这一处）。
            if (s.CoinIcon != null)
            {
                bool showIcon = has && !free && s.CoinIcon.sprite != null;
                if (s.CoinIcon.gameObject.activeSelf != showIcon)
                    s.CoinIcon.gameObject.SetActive(showIcon);
            }
        }
    }

    void OnDraftClicked(int idx)
    {
        if (!_draftActive || idx < 0 || idx >= _slots.Count) return;
        var cb = _onPick;
        var cat = _slots[idx].Cat;
        cb?.Invoke(cat);
    }

    void OnContinueClicked()
    {
        DoContinue();
    }

    /// <summary>
    /// 「继续」的<b>唯一出口</b> —— 玩家点按钮、倒计时归零，两条路都走这里。
    /// <para>2026-10-06 主人拍板：抽完奖励<b>不许自动继续</b>，必须有明确动作；
    /// 倒计时只是「玩家挂着不动」的兜底，不是第二条流程。</para>
    /// </summary>
    void DoContinue()
    {
        if (!_draftActive) return;
        StopContinueCountdown();
        // 【2026-10-06 主人拍板】点「继续」→ 本拍抽奖打上的「新」标记一次性清掉，角标跟着消失。
        ClearNewLootBadges();
        var cb = _onFight;
        cb?.Invoke();
    }

    /// <summary>
    /// 【2026-10-06 主人拍板】清「新」的<b>唯一出口</b>：玩家点「继续」、倒计时归零两条路都走
    /// <see cref="DoContinue"/> → 这里。别处一律不许清，免得「新」字提前消失（或永远不消失）。
    /// <para>顺序不能反：<b>先清标记，再刷 UI</b> —— 反了刷出来的还是带标记的那一版。</para>
    /// <para>没有标记就什么都不做（不无故刷 UI）。</para>
    /// </summary>
    void ClearNewLootBadges()
    {
        if (NewLootMarks.Count <= 0) return;
        NewLootMarks.ClearAll();
        var ui = BattleUI.Instance;
        if (ui == null) return;
        ui.UpdateRunSkillSlots();
        ui.UpdateMercSkillSlots();
        ui.UpdateEquipQuickSlots();
    }

    /// <summary>
    /// 抽完之后锁住抽奖按钮（<b>只置灰，不动显隐 / 不动美术</b>），这一拍玩家只剩「继续」可点。
    /// 正式关连抽本身就是玩法 → <b>不要调它</b>（主人 2026-10-06：正式关抽完也要点继续，但还能接着抽）。
    /// </summary>
    public void SetDrawLocked(bool locked)
    {
        _drawLocked = locked;
        if (!_draftActive) return;
        // 按上一次 Refresh 的同一组参数重刷，不另算一套
        Refresh(_lastRandomPrice, _lastFocusPrice, _lastCats, _lastCoins, _lastFreeLeft);
    }

    /// <summary>
    /// 抽完奖励后开「继续」倒计时：「继续」按钮上方显示剩余秒数，
    /// 归零自动走 <see cref="DoContinue"/>（防玩家挂着不动把战斗卡死）。
    /// <para>用 <c>WaitForSecondsRealtime</c>：抽奖阶段战斗是冻住的（timeScale 可能为 0），
    /// 普通 WaitForSeconds 会永远不走。</para>
    /// </summary>
    /// <summary>
    /// 【2026-10-06 二次拍板】倒计时<b>唯一</b>起表口 —— 只在 <see cref="Begin"/> 调用。
    ///
    /// <para>为什么不用协程：协程会随 GameObject 显隐被掐断/延后，主人在外面看到的就是
    /// 「计时好像没在走 / 抽完才开始跳」。现在改 <see cref="TickContinueCountdown"/> 每帧
    /// 按 <c>Time.unscaledDeltaTime</c> 递减 —— 抽奖阶段 timeScale 冻住也不受影响，
    /// 且一定是从面板出现那一刻起连续走。</para>
    /// </summary>
    public void StartContinueCountdown(float seconds = ContinueCountdownSec)
    {
        EnsureCountdownText();
        if (_countdownText == null) return;
        _countdownLeft = Mathf.Max(1f, seconds);
        _countdownPaused = false;
        _countdownText.gameObject.SetActive(true);
        _countdownText.text = Mathf.CeilToInt(_countdownLeft).ToString();
        Debug.Log($"[BattleEntryDraftPanel] 倒计时起表 {_countdownLeft:F0}s（面板出现即开始，抽奖全程连续，抽完不重置）");
    }

    /// <summary>
    /// 冻结 / 解冻倒计时（抽奖结算、三选一这类挡住玩家的流程）。
    /// <b>只暂停不清零</b> —— 解冻后接着走剩下的秒数，不偷偷补满。
    /// </summary>
    public void SetCountdownPaused(bool paused)
    {
        _countdownPaused = paused;
    }

    public void StopContinueCountdown()
    {
        _countdownLeft = 0f;
        _countdownPaused = false;
        if (_countdownText != null) _countdownText.gameObject.SetActive(false);
    }

    void TickContinueCountdown()
    {
        if (_countdownLeft <= 0f || _countdownPaused) return;
        _countdownLeft -= Time.unscaledDeltaTime;
        if (_countdownLeft <= 0f)
        {
            _countdownLeft = 0f;
            // 归零 = 玩家挂着不动 → 与手点「继续」同一个出口
            DoContinue();
            return;
        }
        if (_countdownText != null)
            _countdownText.text = Mathf.CeilToInt(_countdownLeft).ToString();
    }

    /// <summary>
    /// 倒计时数字：挂在<b>本组件自己的节点</b>下（不是美术的 BackpackPanel 子节点），
    /// 摆在「继续」按钮正上方 —— 不新建美术节点、不改预制体（铁律 3）。
    /// </summary>
    void EnsureCountdownText()
    {
        if (_countdownText != null) return;

        // 【2026-10-06 主人二次校准】倒计时挪到 <b>map 节点的正中间</b>并放大 ——
        // 之前挂「继续」按钮上方太小、不显眼；中间的大数字玩家一眼躲不开。
        //
        // ⚠ 宿主为什么不用「继续」按钮了：按钮会被 <c>SetDraftVisible(false)</c> 整块藏掉，
        // 且 600↔750 两态动画会带着它跑，位置判定反而在变。map 一直常驻在屏幕中间，
        // 挂它下面既稳定又醒目。
        var host = _mapRt;
        if (host == null)
        {
            Debug.LogError("[BattleEntryDraftPanel] 找不到 map 节点，倒计时无处可挂");
            return;
        }

        var go = new GameObject("ContinueCountdown", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(host, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(420f, 200f);
        rt.SetAsLastSibling();

        var t = go.GetComponent<Text>();
        t.text = "";
        t.fontSize = CountdownFontSize;
        t.color = new Color(1f, 0.88f, 0.45f);
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var ff = GameFonts.GetChinese();
        if (ff != null) t.font = ff;
        // 大字号更要描边：压在地图美术上才不会糊成一团
        var ol = go.AddComponent<Outline>();
        ol.effectColor = new Color(0f, 0f, 0f, 0.9f);
        ol.effectDistance = new Vector2(4f, -4f);

        _countdownText = t;
    }

    /// <summary>「背包」按钮：现在只切网格背包（BackpackPanel/GridContainer）的出现/收起。
    /// 【2026-10-09 主人拍板】背包按钮不再走 600↔750 两态 —— 那一套 SetExpanded 只留给抽奖展开用。</summary>
    void OnBackpackClicked()
    {
        // 【2026-10-09 主人拍板，先注释不删】原实现：SetExpanded(!_expanded);
        BattleUI.Instance?.ToggleBagDrawer();
    }

    /// <summary>抽奖按钮容器 + 「继续」按钮的显隐。开战后彻底隐藏，战斗画面里不留抽奖入口。</summary>
    void SetDraftVisible(bool on)
    {
        if (_draftRoot != null) _draftRoot.gameObject.SetActive(on);
        if (_continueBtn != null) _continueBtn.gameObject.SetActive(on);
    }

    /// <summary>
    /// 600 ↔ 750 整套两态切换 —— 本类唯一的展开/收起出口，只改 <c>_animK</c> 目标值，
    /// 所有节点的数值都由 ApplyState 按同一条进度插值写出来，不会出现某个节点漏掉不同步。
    /// </summary>
    public void SetExpanded(bool expanded)
    {
        _expanded = expanded;
        if (_panel == null) return;
        // ⚠ 遮罩 zhezhao 不在这里动 —— 它归 BattleUI.TickBattleMask() 独家管（每帧按 IsLotteryOpen 判）。
        // 两边都改过一次，结果就是遮罩把抽奖按钮/装备/背包一起压掉（2026-10-05 根因）。
        LogMapBaseOnce();
        float to = expanded ? 1f : 0f;
        if (_animCo != null) StopCoroutine(_animCo);
        _animCo = StartCoroutine(CoAnimState(to));
    }

    IEnumerator CoAnimState(float to)
    {
        float from = _animK;
        if (Mathf.Approximately(from, to))
        {
            ApplyState(to);
            yield break;
        }
        float t = 0f;
        while (t < AnimSec)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / AnimSec);
            // 整套两态只走<b>一条</b>进度：所有节点同一个 k，绝不出现谁快谁慢、谁先到位。
            ApplyState(Mathf.Lerp(from, to, EaseOut(p)));
            yield return null;
        }
        ApplyState(to);
        // 2026-10-05 主人报「zhuangshi 动对了，抽奖按钮没移到上面来」：
        // 数值层面 draftY 一直是按真值表写下去的（Begin 日志可证），所以只能落在
        // 「看不见」上 —— 要不没激活、要不落在屏幕外、要不动画结束后被别处拽回去。
        // 这里在动画跑完后再量一次真实屏幕位置，一次把三种可能分开。
        LogState(to >= 1f ? "展开完成" : "收回完成");
        if (to >= 1f) StartCoroutine(CoVerifyDraftOnScreen());
    }

    /// <summary>展开动画结束后 0.6s 再量一次，看抽奖容器有没有被别处拽回原位。</summary>
    IEnumerator CoVerifyDraftOnScreen()
    {
        yield return new WaitForSecondsRealtime(0.6f);
        LogState("展开后0.6s");
        LogDraftLocalRect();
    }

    /// <summary>
    /// 在<b>面板本地坐标</b>里量抽奖容器的落点。
    ///
    /// <para>⚠ 2026-10-05 教训：上一版量的是「世界/屏幕 Y」，但战斗画布不是 Overlay，
    /// 世界单位跟像素不是一个量纲（面板 750 量出来只有 6 个单位），数字根本没法比。
    /// 这里一律换算到面板本地——「离面板底边多少逻辑像素」，跟主人在团结里看到的是同一把尺子。</para>
    /// </summary>
    void LogDraftLocalRect()
    {
        if (_draftRt == null || _panel == null)
        {
            Debug.LogError("[BattleEntryDraftPanel] 抽奖容器/面板缺失，无法量落点");
            return;
        }
        // 先强制布局算完，否则量到的是上一帧的旧矩形
        LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);
        Rect pr = _panel.rect;

        // DraftRoot 中心换算到面板本地
        float centerY = _panel.InverseTransformPoint(_draftRt.position).y - pr.yMin;

        // 首个按钮的图标：上下边也换算到面板本地
        float iconLo = -1f, iconHi = -1f;
        bool hasIcon = false;
        var icon = _slots.Count > 0 && _slots[0].Root != null ? FindDeep(_slots[0].Root, "icon") : null;
        var ic = icon as RectTransform;
        if (ic != null)
        {
            var w = new Vector3[4];
            ic.GetWorldCorners(w);
            iconLo = Mathf.Min(_panel.InverseTransformPoint(w[0]).y, _panel.InverseTransformPoint(w[1]).y) - pr.yMin;
            iconHi = Mathf.Max(_panel.InverseTransformPoint(w[0]).y, _panel.InverseTransformPoint(w[1]).y) - pr.yMin;
            hasIcon = true;
        }

        // 展开时容器中心应落在「0.5×面板高 + 主人的 PosY」：面板 750、PosY -250 → 本地 125。
        float target = pr.height * DraftAnchorY + DraftExpandedPosY;
        bool landedRight = Mathf.Abs(centerY - target) < 12f;

        Debug.Log(        $"[BattleEntryDraftPanel] 落点实测 面板h={pr.height:F0}({pr.yMin:F0}~{pr.yMax:F0})" +
                  $" 面板锚点y={_panel.anchorMin.y:F2} | DraftRoot 实测中心={centerY:F0}" +
                  $" 目标={target:F0}（=0.5×{pr.height:F0}+({DraftExpandedPosY:F0})）" +
                  (landedRight ? " ✓正确" : " ✗不对") +
                  $"（本地坐标，离面板底边） 当前PosY={_draftRt.anchoredPosition.y:F0}" +
                  $" amin.y={_draftRt.anchorMin.y:F2} pivot.y={_draftRt.pivot.y:F2}" +
                  (hasIcon ? $" 图标 {iconLo:F0}~{iconHi:F0}" : "（图标缺失）") +
                  $" | 遮罩={(IsLotteryOpen ? "关" : "开(会压住)")}");

        // fail closed：抽奖按钮没落在面板里就是看不见，必须报出来，不许静默过去。
        float lo = hasIcon ? iconLo : centerY;
        float hi = hasIcon ? iconHi : centerY;
        if (hi <= 0f || lo >= pr.height)
        {
            Debug.LogError($"[BattleEntryDraftPanel] 抽奖按钮落在面板外面（看不见）：" +
                           $"图标 {lo:F0}~{hi:F0}，面板 0~{pr.height:F0}，" +
                           $"DraftRoot 中心={centerY:F0}（应在 {target:F0}），" +
                           $"amin.y={_draftRt.anchorMin.y:F2}。" +
                           $"落点=0.5×面板高+({DraftExpandedPosY:F0})，若仍越界说明面板/容器的锚点或高度" +
                           $"被别处改过 —— 报给主人，不要私自加补偿。");
        }
    }

    /// <summary>
    /// 按两态真值表插值写值。k=0 完全是收起态、k=1 完全是展开态。
    /// ⚠ 值全部来自上面那组主人拍板常量 / map 的运行时原值，不许在这里做任何推算。
    /// </summary>
    void ApplyState(float k)
    {
        _animK = k;
        float panelH = Mathf.Lerp(HeightCollapsed, HeightExpanded, k);
        if (_panel != null)
            _panel.sizeDelta = new Vector2(_panel.sizeDelta.x, panelH);

        // 内容整体上移：zhuangshi 是位移真源，GridContainer 加<b>同一个位移量</b>跟着走。
        float contentY = Mathf.Lerp(ContentCollapsedY, ContentExpandedY, k);
        SetY(_contentRt, contentY);
        float delta = contentY - ContentCollapsedY;
        for (int i = 0; i < _followers.Count; i++)
        {
            var f = _followers[i];
            if (f != null && f.Rt != null) SetY(f.Rt, f.BaseY + delta);
        }

        // 抽奖容器：主人给的是 Inspector 的 Pos Y（锚点 0.5 语义），
        // 先换算成「父级本地 Y = 0.5×面板高 + PosY」，再按<b>当时真实的锚点</b>反算回 Pos Y 写下去。
        float draftPosY = Mathf.Lerp(DraftCollapsedPosY, DraftExpandedPosY, k);
        SetPivotLocalY(_draftRt, panelH * DraftAnchorY + draftPosY, panelH);

        // map：中心 Y 250 → 350（主人给的绝对值），缩放 1.13 → 1。
        // 写的是「Pos Y / 中心 Y」这把尺子，不是「离父级底边多少」—— 换算错尺子会把 map 顶飞。
        ApplyMapState(k);

        // 【2026-10-06 主人拍板】map 上移时，<b>战斗场景里的玩家</b>（世界单位）要跟着一起上移：
        // 主人原话「map 上移时 玩家没有跟着移动上去」—— 背景上去了、人还在原地，看着像人掉下去了。
        ApplyWorldLift(k);
    }

    /// <summary>
    /// 【2026-10-06 主人拍板】map 上移 100 逻辑像素时，世界层要抬多少（世界单位）。
    /// <para>两把尺子不一样：map 走 UI 逻辑像素（250→350 = +100），世界层是 Unity 单位。
    /// 按「竖屏可视高 ≈ 1280 逻辑像素 = 2 × orthographicSize(5.4) ≈ 10.8 世界单位」折算，
    /// 100 像素 ≈ 0.85 世界单位。<b>主人在团结里看着差一点，只改这一个数。</b></para>
    /// </summary>
    const float WorldLiftY = 0.85f;

    /// <summary>
    /// 世界层跟随位移：只挪 <c>unitRoot</c> 这一层（玩家/佣兵都是它的子节点，会整体跟着走），
    /// 基准恒取 <c>UnitBase.GROUND_Y</c>（主人给的站立线），<b>不缓存</b>——
    /// 二次进战斗 unitRoot 重建也不会算错。
    /// </summary>
    void ApplyWorldLift(float k)
    {
        var bm = BattleManager.Instance;
        var root = bm != null ? bm.unitRoot : null;
        if (root == null) return;
        var p = root.position;
        p.y = UnitBase.GROUND_Y + Mathf.Lerp(0f, WorldLiftY, k);
        root.position = p;
    }

    static void SetY(RectTransform rt, float y)
    {
        if (rt == null) return;
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
    }

    /// <summary>
    /// 把节点 rt 的 <b>pivot</b> 摆到「<c>rt.parent</c> 的 rect 空间」的 <c>localY</c>（底边为 0）。
    /// <para>· 点锚点（<c>anchorMin.y == anchorMax.y</c>）：
    ///   <c>anchorRef = parent.rect.yMin + anchorMin.y × 父高</c>，pivot 本地 Y = <c>anchorRef + apos.y</c>
    ///   ⇒ <b><c>apos.y = localY - anchorRef</c></b>。
    ///   pivot 已经在 apos 的定义里了，<b>绝不能再加减 <c>pivot.y × 高</c></b> —— 2026-10-05 就是
    ///   多算了这一项，把 -250 写成 -200，抽奖按钮直接掉到面板外面（主人报「一个也没改」）。</para>
    /// <para>· 竖向拉伸锚点（<c>anchorMin.y != anchorMax.y</c>）：<c>anchoredPosition.y</c> 无效，
    ///   改成按差值整体推 <c>offsetMin/offsetMax</c>。</para>
    /// <remarks>⚠ 尺子恒为「节点自己的父级」。竖屏适配会把锚点重锚成贴底（实测 amin.y 0.5→0），
    /// 同一个 Pos Y 的含义就变了，所以只能先定本地落点、再按当时锚点反算。</remarks>
    /// </summary>
    static void SetPivotLocalY(RectTransform rt, float localY, float parentH = -1f)
    {
        if (rt == null) return;
        var parent = rt.parent as RectTransform;
        if (parent == null) return;

        if (!Mathf.Approximately(rt.anchorMin.y, rt.anchorMax.y))
        {
            // 拉伸锚点：整体平移差值（offsetMin/offsetMax 一起推，矩形才不会被拉变形）
            float d = localY - PivotLocalY(rt);
            rt.offsetMin = new Vector2(rt.offsetMin.x, rt.offsetMin.y + d);
            rt.offsetMax = new Vector2(rt.offsetMax.x, rt.offsetMax.y + d);
            return;
        }

        float h = parentH > 0f ? parentH : parent.rect.height;
        float anchorRef = parent.rect.yMin + rt.anchorMin.y * h;
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, localY - anchorRef);
    }

    /// <summary>rt 的 pivot 在「它自己父级 rect 空间」里的 Y（底边为 0），与 <see cref="SetPivotLocalY"/> 同一把尺子。</summary>
    static float PivotLocalY(RectTransform rt)
    {
        if (rt == null) return 0f;
        var parent = rt.parent as RectTransform;
        if (parent == null) return 0f;
        return parent.InverseTransformPoint(rt.position).y - parent.rect.yMin;
    }

    /// <summary>ease-out：起步快、末端平滑收住。</summary>
    static float EaseOut(float x)
    {
        float u = 1f - Mathf.Clamp01(x);
        return 1f - u * u;
    }

    static string Name(DraftCategory? c)
    {
        if (!c.HasValue) return "普通";
        switch (c.Value)
        {
            case DraftCategory.Merc: return "佣兵";
            case DraftCategory.Equip: return "装备";
            case DraftCategory.Skill: return "技能";
            default: return "普通";
        }
    }

    // ============================================================
    // 小工具
    // ============================================================

    static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        var direct = root.Find(name);
        if (direct != null) return direct;
        for (int i = 0; i < root.childCount; i++)
        {
            var hit = FindDeep(root.GetChild(i), name);
            if (hit != null) return hit;
        }
        return null;
    }

    /// <summary>
    /// 2026-10-04 主人拉的两态数值（改代码前先对着这组值核一遍）：
    ///   收起：面板 600 / zhuangshi 0 / DraftRoot PosY -400（本地 -100，藏在面板下）/ map 中心Y 250、scale 1.13
    ///   展开：面板 750 / zhuangshi 80 / DraftRoot PosY -250（本地 125）/ map 中心Y 350、scale 1
    ///   （map 的中心 Y = Inspector 的 Pos Y；拉伸锚点下看日志的 <c>mapPosY</c>，
    ///    <c>mapBottom / mapTop</c> 是离父级底/顶的距离，是另一把尺子，别拿来对 250/350）
    ///   （DraftRoot 的 -400 / -250 是 <b>Inspector 的 Pos Y</b>，锚点 0.5 —— 见常量处注释）
    /// 遮罩 zhezhao 不在本表内 —— 它归 BattleUI.TickBattleMask() 管（展开时由本类的
    /// <see cref="IsLotteryOpen"/> 通知它让开）。
    /// </summary>
    void LogState(string tag)
    {
        float draftLocal = _draftRt != null ? PivotLocalY(_draftRt) : -9999f;
        Debug.Log($"[BattleEntryDraftPanel] {tag} k={_animK:F2}" +
                  $" panelH={(_panel != null ? _panel.sizeDelta.y : -1f)}" +
                  $" contentY={(_contentRt != null ? _contentRt.anchoredPosition.y : -9999f)}" +
                  $" draftPosY={(_draftRt != null ? _draftRt.anchoredPosition.y : -9999f)}" +
                  $" draft本地Y={draftLocal:F0}" +
                  $" draftActive={(_draftRoot != null && _draftRoot.gameObject.activeSelf)}" +
                  $" mapBottom={(_mapRt != null ? _mapRt.offsetMin.y : -9999f)}" +
                  $" mapTop={(_mapRt != null ? -_mapRt.offsetMax.y : -9999f)}" +
                  $" mapPosY={(_mapRt != null ? _mapRt.anchoredPosition.y : -9999f)}" +
                  $" mapScale={(_mapRt != null ? _mapRt.localScale.x : -1f):F3}");
    }
}
