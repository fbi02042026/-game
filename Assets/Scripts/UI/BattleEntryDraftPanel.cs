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
public partial class BattleEntryDraftPanel : MonoBehaviour
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
    /// 场景 map 的两态参数：
    /// 默认（面板收起）在 <b>250</b>；面板展开后在 <b>350</b>。
    /// 缩放两态恒为 <b>1.4</b>（2026-10-27 主人拍板「改成放大到1.4倍」：
    /// map 不再按 1.13→1 两态缩放，收起/展开都按 1.4 倍显示，只动位置不动缩放）。
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
    const float MapCollapsedScale = 1.4f;
    const float MapExpandedScale = 1.4f;

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

    // ── 5 秒无操作软引导（2026-10-18 主人拍板）────────────────────────────
    // 正式战斗中：面板弹出后玩家 5 秒没任何动作 → 手指指抽奖按钮；
    // 【2026-10-18 二次拍板】抽完后又间隔 5 秒无操作 → 再指引点「继续」（每次抽奖都重走 5 秒）。
    // 引导局（IsTutorialRun）不走这套，新手引导还是原来的脚本化硬引导。
    const float IdleGuideSec = 5f;
    Coroutine _idleGuideCo;
    bool _hasDrawnThisPanel;    // 本次抽奖环节玩家有没有抽过（决定手指指抽奖按钮还是指「继续」）
    bool _idleGuideShown;       // 引导手指当前是否已亮出

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
        /// <summary>【2026-10-13 #7】币不够时价格数字变红，缓存主人调好的原色，够了再恢复（不永久覆盖美术）。</summary>
        public Color? CostColorDefault;
        /// <summary>【2026-10-18 主人拍板】「免费」两字要居中：缓存预制体原对齐，非免费时恢复，不永久覆盖美术。</summary>
        public TextAnchor? CostAlignDefault;
        /// <summary>【2026-10-13 #7】价格下方的「还差 N」红字（仅币不够时显示）。</summary>
        public Text ShortfallText;
    }

    /// <summary>币种图标缓存：别每次 Refresh 都 Resources.Load。</summary>
    static readonly Dictionary<ResourceWallet.ResourceType, Sprite> _currencyIcons =
        new Dictionary<ResourceWallet.ResourceType, Sprite>();

    /// <summary>
    /// 抽奖按钮上显示哪种币的图标 —— <b>单一出口</b>。
    /// 主人原话：「在抽奖的按钮左侧加个货币的图标 金币就加金币的 佣兵就加佣兵的，
    /// 目前只有佣兵是佣兵币 其他的都是金币 等其他三个抽奖按钮开放了再说」。
    /// 哪天其余按钮要换币，只改这一个函数，别在调用处各写一遍。
    /// <para>【2026-10-10 主人拍板】抽奖统一花金币：随机 / 装备 / 技能 / **佣兵定向**都显示<b>金币</b>图标，
    /// 因为真扣的就是局内金币 <c>BattleManager.currentGold</c>（不再有佣兵币 / 抽奖币）。
    /// 与 <c>SlotMachineSystem.FocusCurrency</c> 必须同值 —— 显示与扣费不许是两套。</para>
    /// </summary>
    static ResourceWallet.ResourceType CurrencyOf(DraftCategory? cat)
    {
        // 【2026-10-10 主人拍板】抽奖统一花金币（佣兵定向也不再花佣兵币）——
        // 与 `SlotMachineSystem.FocusCurrency` 必须同值，否则玩家看着金币图标、扣的却是别的币。
        return ResourceWallet.ResourceType.Gold;
    }

    /// <summary>币种 → 图标资源路径（单一出口，别处不许再抄一遍路径）。</summary>
    static string CurrencyIconPath(ResourceWallet.ResourceType t)
    {
        switch (t)
        {
            case ResourceWallet.ResourceType.MercGold: return "UI/Icons/Common/icon_yongbinggold";
            // 主人 2026-10-17 放入的抽奖货币新图（Resources/Art/ 下，路径与 Art 源不同）
            case ResourceWallet.ResourceType.Gold: return "UI/Art/Icons/Common/icon_draft_coin";
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
    /// <summary>概率公示按钮（美术图，运行时建，见 BuildOddsButton）。</summary>
    Button _oddsBtn;
    /// <summary>【2026-10-13 #9】连抽计数（每次开面板清零，每点一次抽奖 +1）。</summary>
    int _comboCount;
    /// <summary>【2026-10-13 #9】「已连抽 ×N」提示（≥2 显示），挂在抽奖容器上方。</summary>
    Text _comboText;
    Coroutine _comboFlashCo;
    // 【2026-10-21 主人拍板「抽奖数都在顶部」】原 _poolChipText（按钮上方抽奖币计数）已随
    // BuildPoolChip 整段拆除，余额只走顶栏 DraftDisplay 抽奖币条（BattleUI.UpdateDraftCoin）。

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
    /// 【2026-10-18 主人拍板】30 → 60：自动开始前的倒计时放宽到 1 分钟，
    /// 给玩家足够的看构筑 / 考虑时间，到点才自动开打。
    /// </summary>
    public const float ContinueCountdownSec = 60f;

    /// <summary>
    /// 倒计时数字字号。【2026-10-13 #7】120 → 56：不再压屏幕正中，把视觉焦点还给抽奖区。
    /// 要大小只改这一个数。
    /// </summary>
    const int CountdownFontSize = 56;

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

}
