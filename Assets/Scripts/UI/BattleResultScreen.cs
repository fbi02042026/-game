using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗结果界面（预制体版，2026-10-24 主人拍板「接下代码还有自适应」）：
/// 战斗结束弹出「胜利 / 失败」大画面 + 特效，淡入弹出 → 停留 → 淡出 → 回调后续流程；
/// 期间<b>点击任意处可跳过</b>（主人拍板：「弹出界面后点击任意地方关闭界面并返回到冒险界面」——
/// 跳过的是演出，后续流程照旧：普通关胜利→回冒险页；阵亡→遗产结算）。
///
/// <para>预制体：<c>Resources/Prefabs/Battle/战斗结果</c>（主人手工搭：根节点自带
/// ScreenSpaceCamera Canvas + GraphicRaycaster（铁律 10）+ CanvasScaler 720×1280；
/// Root → Panel(620×560) → 胜利/失败(各 600×462，图 + Button + 世界空间粒子子树)；
/// Root → Dim 全屏遮罩。两个胜负节点由本类按胜负切显隐）。</para>
///
/// <para>【2026-10-25 主人拍板 · 胜利流程改版】主人在胜利节点里手摆了奖励区：
/// 胜利 → Panel(带 CanvasGroup，内含 装饰 / ConfirmBtn / CellTemplate[Icon+Label])。
/// 新时间轴：先弹出胜利节点（中部）→ 上移到预制体拍板位（≈250，读预制体值不写死）
/// → 奖励 Panel 的 CanvasGroup 从 0 淡到 1（拍板：默认是 0，prefab 里是 1 由运行时压 0）
/// → 等玩家点【确定】或点任意处跳过 → onDone（既有链：结算 → 回冒险界面）。
/// 宝箱奖励已在结算链自动入账，界面只负责展示（「打完自动领取宝箱后的奖励」，无宝箱点击步）。</para>
///
/// <para>【2026-10-26 主人拍板 · 分享钮改版】奖励区分享钮整体居中收紧在面板内（旧版 x=-373
/// 在面板外整只出屏）；钮图换主人给的 <c>Art/UI/分享/分享按钮</c>（路径加载）；钮下加一行
/// 「分享获得100钻 每日一次」（钻数直引 SHARE_REWARD_DIAMOND）；奖励面板加宽 500→620。
/// 动效检查：unscaled 帧步长封顶（切后台/分享面板返回首帧跳变）。</para>
///
/// <para>【2026-10-29 主人拍板 · 分享钮 v4（预制体手摆版）】主人在预制体里手摆好分享区：
/// 奖励 Panel 下「分享按钮」（Image+Button+子Text「分享」，图主人已拖好）与确定钮并排
///（确定左 x=-118 / 分享右 x=129），胜利节点下另摆「分享」说明文字（「分享获得100钻石，每日一次」）。
/// 代码改绑预制体节点，不再运行时克隆/建提示行（v3 克隆方案整段废弃）。
/// 排版口径：可分享（非引导局 且 今日未领）＝主人摆的排版原样；分享过后（今日已领，含本次点分享
/// 领奖回来，由 ShareService.ShareRewardGranted 事件驱动）＝隐藏分享按钮与说明、确定钮 x 归中。
/// 引导局沿用「没有分享按钮」口径（2026-10-27 拍板）。</para>
///
/// <para>【2026-10-29 主人拍板 · 分享钮 v3 + 动效节奏】分享钮换主人指定的
/// <c>Art/UI/分享/分享按钮1</c>（普通蓝底金边按钮图 247×68，无自带文字 → 克隆来的 Text 保留写
/// 「分享」），尺寸照抄确定钮（同 sizeDelta × 0.6 缩放 = 差不多大）；与【确定】并排成居中双钮行
/// （分享左、确定右，各移 SharePairOffsetX，y 不动）——不再占奖励行下方居中位，10-26 的奖励行
/// +46 上抬随之作废、回模板基线。提示行改摆按钮行上方（装饰条与按钮行之间）。
/// 胜利时间轴加停顿：弹出胜利图 → 停 0.7s → 上移 → 奖励 Panel 淡入（主人原话「先出来胜利图片
/// 有个停顿在上移出现物品框」）。奖励为空的根因在 BattleManager：GoldGained 只在局末快照才填，
/// 普通关通关链路在 Show 前提前拷一次（见 FinishStageAfterPortalReached）。</para>
///
/// <para>接入（统一门，2026-10-24 主人拍板「正式关卡/支线/活动副本都走」；引导撤离
/// 2026-10-28 主人拍板也走）：<br/>
/// · 胜利：<see cref="BattleManager.FinishStageAfterPortalReached"/> 里 <c>Mode.UsesBattleResultScreen</c> 门；<br/>
/// · 失败：<see cref="BattleManager.OnHeroDead"/> 同一道门。<br/>
/// · 引导撤离/引导通关收尾：<see cref="BattleManager.TriggerTutorialEvacuateWithSettlement"/>
///   直呼 Show(victory)（引导局分享钮不建，见 SetupRewardPanel 分流）。<br/>
/// 无限裂隙（每层通关即有累计奖励）直接走自己的结算续层链；引导局阵亡重进战斗不播；
/// 终章追查关不播（通关接揭底剧情，保持节奏）。</para>
///
/// <para>铁律合规：29/30 + AGENTS §2.3 —— 预制体与图都走路径加载，零 guid 依赖
/// （预制体 meta 与图的 meta 都是团结 56 字符标识，不扫不修）；大图按路径<b>主动设图</b>
/// （预制体 m_Sprite 引用断，这是需求不是兜底）；32 —— 不写任何 sizeDelta /
/// anchoredPosition.x / Image.type，动效只动 localScale / CanvasGroup.alpha / 纵向 y（上移拍板位），
/// 用完即毁。</para>
/// </summary>
public static class BattleResultScreen
{
    /// <summary>预制体路径 —— 与预制体实际位置必须同值，改一边忘另一边 = 运行时加载不到。</summary>
    public const string PrefabPath = "Prefabs/Battle/战斗结果";

    /// <summary>当前活动的失败选择窗（2026-10-25 广告版新增）：广告异步回调成功后
    /// ForceCloseChoice 凭它收窗；窗销毁时由 UI 侧清空。非选择模式（胜利/失败直出）不占位。</summary>
    internal static BattleResultScreenUI _activeChoice;

    /// <summary>
    /// 弹出战斗结果界面。onDone 在界面销毁后回调（后续流程如回城/结算放这里）。
    /// 【2026-10-25 主人拍板】胜利新流程见类头注释：奖励格数据源 = stats（本局 BattleRunStats），
    /// 调用方（FinishStageAfterPortalReached）传 RunStats；失败路（Death 遗产结算）传 null 不上格。
    /// 非战斗场景 / 预制体缺失 → 不弹，直接走 onDone（缺真值不卡流程，铁律 14）。
    /// 【2026-10-29 竞技场】battleSceneOnly=false 允许城镇场景调用（竞技场结算没有战斗场景）；
    /// 默认 true，原战斗流程调用方零改动。
    /// 【2026-10-30 竞技场真战斗】extraCells = 调用方组装好的追加奖励格（默认 null = 原行为零改动）：
    /// 竞技场胜利传 [积分格]（图标 +「+N」），拼在 stats 格之后展示；失败路不传，界面零改动（失败只有图）。
    /// 【2026-10-31 主人报「失败我没点自己就消失回城了」】holdUntilTap = 失败界面不自动收，
    /// 停 1.2s 后改为等玩家点击任意处（点一下才淡出走 onDone）——竞技场失败专用
    /// （普通关失败停留自动收的老口径不变，调用方不传 = 零改动）。
    /// 【2026-11-12 主人拍板「战斗结果界面加下一关」】onNextStage = 界面销毁后直进下一关回调；
    /// onNextStageGuard = 点【下一关】时的守卫（false = 拦下不收窗，守卫侧弹体力不足购买弹窗）；
    /// onRewardsShown = 奖励 Panel 淡入完成后回调（主人拍板「奖励展示完再弹升级窗」）。
    /// 三参全默认 null，原调用方零改动；只有主线 Normal 同章有下一关才传（章末/其它模式不传）。
    /// </summary>
    public static void Show(bool victory, BattleRunStats stats, Action onDone, bool battleSceneOnly = true,
        List<SettlementRewardCell> extraCells = null, bool holdUntilTap = false,
        Action onNextStage = null, Func<bool> onNextStageGuard = null, Action onRewardsShown = null,
        string unlockText = null)
    {
        if (!Application.isPlaying || (battleSceneOnly && !GameSceneGate.IsBattle))
        {
            onDone?.Invoke();
            return;
        }

        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[BattleResultScreen] 找不到预制体 Resources/{PrefabPath}，本局不播结果界面");
            onDone?.Invoke();
            return;
        }

        // 【2026-10-24 编译修复】Object 因 using System + using UnityEngine 双引用产生歧义（CS0104），
        // 显式限定 UnityEngine.Object；本类非 MonoBehaviour，不能裸调 Instantiate。
        var go = UnityEngine.Object.Instantiate(prefab);
        var ui = go.GetComponent<BattleResultScreenUI>() ?? go.AddComponent<BattleResultScreenUI>();
        ui.Play(victory, stats, onDone, extraCells, holdUntilTap, onNextStage, onNextStageGuard, onRewardsShown,
            unlockText);
    }

    /// <summary>
    /// 失败选择界面（2026-10-24 主人拍板「失败界面加复活和返回，复活先扣钻石」）：
    /// 弹出「失败」画面后<b>不自动收</b>，等玩家二选一 ——
    /// 【复活（X钻）】→ onRevive() 扣钻复活；返回 false（钻石不足，toast 已提示）则界面停留可再点；
    /// 【返回】→ onGiveUp()（死亡收尾 → 遗产结算）。
    /// 选择模式下 Dim 与失败大图不接「点击任意处跳过」（防误触放弃）；胜利界面照旧走 <see cref="Show"/>。
    /// </summary>
    /// <param name="useAdIcon">
    /// true = 确认钮贴广告图（广告档）。【2026-11-02】原来靠 <paramref name="reviveLabel"/> 非空来判定，
    /// 只要有自定义文案就贴广告图 —— 改成显式传参，避免以后加文案档位时误贴。
    /// </param>
    public static void ShowFailureChoice(int revivePrice, Func<bool> onRevive, Action onGiveUp,
        string reviveLabel = null, bool useAdIcon = false)
    {
        if (!Application.isPlaying || !GameSceneGate.IsBattle)
        {
            onGiveUp?.Invoke();
            return;
        }
        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[BattleResultScreen] 找不到预制体 Resources/{PrefabPath}，跳过复活选择直接死亡收尾");
            onGiveUp?.Invoke();
            return;
        }
        var go = UnityEngine.Object.Instantiate(prefab);
        var ui = go.GetComponent<BattleResultScreenUI>() ?? go.AddComponent<BattleResultScreenUI>();
        ui.PlayChoice(revivePrice, onRevive, onGiveUp, reviveLabel, useAdIcon);
    }

    /// <summary>
    /// 竞技场失败界面（2026-10-31 主人拍板「竞技场没有复活 胜利有返回和分享 失败只有返回」）：
    /// 失败图 + 主人手摆的【取消】钮（文本「返回」）一个出口；【确定】（复活钮）整颗隐藏，
    /// 不接 Dim / 败图「点击任意处跳过」（旧 holdUntilTap 点任意处回城口径作废，只有点返回才走）。
    /// onReturn 在界面销毁后回调（toast 扣分 + 回城链放这里）。
    /// </summary>
    public static void ShowArenaFailure(Action onReturn)
    {
        if (!Application.isPlaying || !GameSceneGate.IsBattle)
        {
            onReturn?.Invoke();
            return;
        }
        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[BattleResultScreen] 找不到预制体 Resources/{PrefabPath}，竞技场失败界面不播直接收尾");
            onReturn?.Invoke();
            return;
        }
        var go = UnityEngine.Object.Instantiate(prefab);
        var ui = go.GetComponent<BattleResultScreenUI>() ?? go.AddComponent<BattleResultScreenUI>();
        ui.PlayArenaFailure(onReturn);
    }

    /// <summary>
    /// 外部强制收窗（复活成功分支）：广告是异步回调，RewardedAdBridge 核验通过后由
    /// BattleManager 调用 —— 等效「玩家点了复活钮成功」的收尾（淡出、不触发 onGiveUp）。
    /// 当前无活动选择窗时静默跳过（不给战斗流程兜底，只是没有窗可收）。
    /// </summary>
    public static void ForceCloseChoice()
    {
        if (_activeChoice != null)
            _activeChoice.ForceCloseReviveChosen();
    }
}

/// <summary>结果界面驱动（协程宿主：摆节点 + 接线 + 时间轴；结束自毁）。</summary>
public class BattleResultScreenUI : MonoBehaviour
{
    // 【2026-10-24 编译修复】以下常量原写在 BattleResultScreen（私有），唯一使用方是本类
    // → CS0103（InDur/HoldDur/OutDur/SkipOutDur/ArtDir 找不到）。按「同一语义只留一个出口」
    // 搬到唯一消费方，不改任何数值。    /// <summary>胜利/失败大图资源目录（按路径设图）。</summary>
    const string ArtDir = "UI/Art/battle/战斗结果/";
    /// <summary>淡入/弹出时长（秒）。</summary>
    const float InDur = 0.28f;
    /// <summary>停留时长（秒）。</summary>
    const float HoldDur = 1.2f;
    /// <summary>正常淡出时长（秒）。</summary>
    const float OutDur = 0.3f;
    /// <summary>点击跳过的淡出时长（秒）——跳过要利落，比正常淡出短。</summary>
    const float SkipOutDur = 0.12f;
    /// <summary>【2026-10-25 主人拍板】胜利节点从弹出到上移到位的时长（秒）。</summary>
    const float MoveUpDur = 0.35f;
    /// <summary>【2026-10-29 主人拍板「先出来胜利图片有个停顿再上移」】胜利图弹出后的停顿（秒）。
    /// 【2026-10-31 主人拍板「1秒后再出现奖励框」】0.7 → 1.0。</summary>
    const float VictoryHoldDur = 1.0f;
    /// <summary>【2026-10-31 主人拍板「从小放大再回原来大小的动效 可以快点」】段一弹出曲线参数：
    /// 缩放 0.5 → 峰值 → 回 1，两段线性各占一段时长，全程仍在 InDur(0.28s) 内跑完 —— 节奏快、过冲明显。</summary>
    const float VictoryPopPeak = 1.15f;
    /// <summary>放大段占整段的比例（前 60% 从 0.5 冲到峰值，后 40% 缩回 1）。</summary>
    const float VictoryPopUpFrac = 0.6f;
    /// <summary>【2026-10-25 主人拍板】奖励 Panel 淡入时长（秒）。</summary>
    const float RewardInDur = 0.3f;
    /// <summary>奖励格横向间距（运行时克隆格用，先例 = BattleSettlementUI 网格 spacing 10）。</summary>
    const float RewardCellGap = 10f;

    // —— 2026-10-26 加宽常量保留；v3 分享钮布局常量（SharePairOffsetX / ShareTipLocalY /
    //    ShareTipSize / ShareBtnArt）随 2026-10-29 v4「预制体手摆版」整段废弃删除 ——
    //    分享区摆位全以预制体为准（铁律 9），代码只切显隐与确定钮 x（拍板内容）。
    /// <summary>奖励面板加宽后的宽（预制体 500 → 620，与外层 Panel 同宽；只放宽高度不动）。
    /// 面板子节点全是中心锚，加宽以中心对称、不改主人摆的双钮相对位（10-29 v4 分享钮版仍保留）。</summary>
    const float RewardPanelWidth = 620f;

    /// <summary>【2026-10-31 主人拍板「返回/确定/分享按钮再放大30%」】结果界面四颗按钮（确定/分享/失败确定/失败返回）
    /// 统一 localScale ×1.3（只动缩放不碰 sizeDelta/摆位，铁律 32；与进阶钮呼吸动效同通道）。</summary>
    const float ResultBtnScaleMul = 1.3f;

    /// <summary>【2026-10-31 主人拍板「战斗结果界面的确定和分享按钮缩小30%，战斗失败的按钮先不动」】
    /// 胜利区两颗（ConfirmBtn 确定 / 分享按钮）在 ×1.3 基础上再 ×0.7 = 0.91；失败区（确定/取消）仍按
    /// <see cref="ResultBtnScaleMul"/> 不动。注：胜利钮基数 363×123@0.6=217.8 宽，失败钮 100×33@1.4=140 宽，
    /// 两组来源图不同、本就不等大（预制体口径），这里只按拍板量缩，不擅自统一尺寸。</summary>
    const float VictoryBtnScaleMul = ResultBtnScaleMul * 0.7f;

    /// <summary>把一颗结果按钮按倍率缩放（在 authored localScale 基础上乘，不写死 1）。
    /// 默认倍率 = 失败区口径 <see cref="ResultBtnScaleMul"/>；胜利区两颗传 <see cref="VictoryBtnScaleMul"/>。</summary>
    static void EnlargeResultButton(Transform btnT, float mul = ResultBtnScaleMul)
    {
        if (btnT == null) return;
        btnT.localScale = btnT.localScale * mul;
    }

    // —— 分享奖励行（2026-10-31 主人拍板「在分享按钮下面加上一行字 +100钻石的图标」）——
    // 尺寸是行内排版口径（不是预制体摆位）：行宽/行高/与按钮的纵向间隙/图标边长。
    // 【2026-10-31 晚 主人拍板「不要字 钻石要图标 字号和图标放大20%」】文字只留「+100」，
    // 钻石含义由图标表达；行宽/行高/图标/字号整体 ×1.2。
    const float ShareRewardRowW = 204f;
    const float ShareRewardRowH = 41f;
    const float ShareRewardRowGap = 6f;      // 行顶边离分享按钮底边的空隙
    const float ShareRewardIconSize = 31f;
    const float ShareRewardIconGap = 4f;     // 图标与文字的横向间隙
    const int ShareRewardFontSize = 26;      // 原 22 × 1.2
    /// <summary>钻石图标走 CommonIconSprites 统一出口（与每日登录/资源条同一张图）。</summary>
    const string ShareRewardIconName = "icon_diamond_blue";

    RectTransform _panel;
    CanvasGroup _cg;
    Action _onDone;
    bool _skipped;
    bool _finished;

    // —— 胜利新流程（2026-10-25 主人拍板）专用状态 ——
    Transform _victoryNode;
    RectTransform _victoryRt;
    CanvasGroup _rewardCg;
    bool _confirmed;

    // —— 【下一关】（2026-11-12 主人拍板「战斗结果界面加下一关」）专用状态 ——
    /// <summary>true = 玩家点了【下一关】（守卫放行 → 段四放行 → 段五淡出 → _onNextStage 直进下一关）。</summary>
    bool _nextStageChosen;
    /// <summary>本次胜利界面是否显示【下一关】钮（onNextStage 非 null 才 true；复用「分享按钮」节点）。</summary>
    bool _nextStageActive;
    /// <summary>点击守卫（体力够=扣体力 true；不足=弹 StaminaConfirmPopup false 拦下留在本界面）。null = 不设守卫。</summary>
    Func<bool> _onNextStageGuard;
    /// <summary>界面销毁后的直进回调（BattleManager.ContinueToNextStageInPlace：黑底字幕直进下一关，不回城）。</summary>
    Action _onNextStage;
    /// <summary>奖励 Panel 淡入完成回调（LevelUpPopup.FlushPending，主人拍板「奖励展示完再弹升级窗」）。</summary>
    Action _onRewardsShown;

    // —— 分享钮 v4（2026-10-29 主人拍板，预制体手摆版）专用状态 ——
    /// <summary>
    /// 【2026-11-02 主人拍板「去掉分享的按钮和功能」】分享入口总开关。
    /// false = 胜利界面不显示「分享按钮」/「分享」说明/「+100钻石」行，确定钮<b>恒居中</b>，
    /// 且不接点击（功能整体下线）；<see cref="ShareService"/> / <see cref="SharePanelUI"/> 文件保留不删，
    /// 改 true 即恢复（恢复时同步看 GameConfig.SHARE_REWARD_DIAMOND 的文案口径）。
    /// 下线原因：微信小游戏运营规范 2.5.1 禁止利益诱导分享（详见 Docs/《分享功能可发性体检》_2026-11-02.md）。
    /// </summary>
    const bool ShareFeatureEnabled = false;

    Transform _shareBtnT;      // 奖励 Panel 下「分享按钮」（主人手摆：Image+Button+子Text，图已拖好）
    Transform _shareTipT;      // 胜利节点下「分享」说明文字（主人手摆，跟随分享钮显隐）
    Transform _shareRewardRow; // 奖励 Panel 下动态建的「钻石图标 +100钻石」行（分享钮正下方，跟随显隐）
    RectTransform _confirmRt;  // 确定钮（分享下线后恒居中；y/scale/尺寸一律不动）
    float _confirmBaseX;       // 确定钮预制体原 x（= 主人摆的可分享位；打开时读值，不写死）
    bool _shareEntryReported;  // 分享入口曝光埋点只报一次（可分享态才算曝光）

    /// <summary>【2026-11-27 主人拍板】本关新开启的功能清单（多行；null = 不建框）。</summary>
    string _unlockText;
    Transform _unlockBannerT;  // 动态建的金框黄字框（确定钮下方），销毁随界面
    Transform _rewardPanelT;   // 胜利节点下的奖励 Panel（建新功能框时当父节点，同 BuildShareRewardRow 口径）

    // —— 失败选择模式（复活/返回）专用状态 ——
    bool _choiceMode;
    bool _chosen;
    bool _choseGiveUp;
    Func<bool> _onRevive;
    Action _onGiveUp;
    /// <summary>复活钮上方「共 N 钻」余额提示的字号/间距/宽度。
    /// 【2026-11-28 主人「我说的一共有多少钻石要放到按钮上面的text 不要放到按钮里面」】
    /// 提示是**按钮外**独立的一条 Text（父级 = 复活钮，所以跟着它 1.3 倍缩放 → 17 号实际约 22）。</summary>
    const int ReviveHaveFontSize = 17;
    const float ReviveHaveGapY = 8f;        // 文字底边离按钮顶边的距离（本地单位，父级缩放后约 10px）
    const float ReviveHaveLabelW = 220f;    // 够放「共 9999999 钻」，横排不换行
    /// <summary>余额提示色：浅蓝，与钻石 icon 同色系；描边走 PurchasePriceStyle 统一口径。</summary>
    static readonly Color ReviveHaveColor = new Color(0.75f, 0.88f, 1f, 1f);

    // —— 失败停留等点击（2026-10-31 主人报「失败我没点自己就消失回城了」，竞技场失败专用）——
    bool _holdUntilTap;

    // —— 竞技场失败·仅返回（2026-10-31 主人拍板「失败只有返回」）——
    Action _arenaReturn;
    bool _arenaReturned;

    /// <summary>按胜负摆节点 + 自适应 + 接线 + 开播。
    /// 【2026-10-30 竞技场】extraCells = 调用方组装好的追加奖励格（默认 null，原调用方零改动），透传给奖励区。
    /// 【2026-10-31】holdUntilTap = 失败界面停留后不自动收，等点击（见 <see cref="BattleResultScreen.Show"/>）。</summary>
    public void Play(bool victory, BattleRunStats stats, Action onDone, List<SettlementRewardCell> extraCells = null,
        bool holdUntilTap = false, Action onNextStage = null, Func<bool> onNextStageGuard = null,
        Action onRewardsShown = null, string unlockText = null)
    {
        _onDone = onDone;
        _holdUntilTap = holdUntilTap;
        // 【2026-11-27 主人拍板】本关新开启功能清单（多行文本，null = 不显示）；
        // 真源在 FeatureUnlock.NewlyUnlockedText，调用方算好传进来，本类只管画。
        _unlockText = unlockText;
        // 【2026-11-14 修误点】只有胜利演出需要「早期误点作废」；失败/复活选择/竞技场失败
        // 走 CoPlay/PlayChoice/PlayArenaFailure，_victoryFlow=false 让 Skip 保持原行为零改动。
        _victoryFlow = victory;
        _blockEarlySkip = victory;
        // 【2026-11-12 主人拍板「下一关」】guard 在点击瞬间（本界面存活期）先跑；onNextStage 在淡出销毁后
        // 跑（ContinueToNextStageInPlace）；onRewardsShown 在奖励淡入完成点跑（见 CoPlayVictory 段三末尾）。
        _onNextStage = onNextStage;
        _onNextStageGuard = onNextStageGuard;
        _onRewardsShown = onRewardsShown;
        _nextStageActive = onNextStage != null;

        // 统一适配入口（2026-10-24 主人拍板「接下自适应」）：一次修预制体三处硬伤 ——
        // ① worldCamera 为空（不绑相机整块不显示）；② 根节点 localScale=(0,0,0)（EnsureRootStretch 归一）；
        // ③ CanvasScaler 写死高对齐 720×1280（瘦长屏 1080×2400 逻辑宽只剩 576 < Panel 620 会切面板），
        //    按 DesignAspectLetterbox.ResolveUiMatch() 换宽对齐；④ override 层级 985（盖 HUD/技槽/抽奖面板）。
        UICanvasSetup.ApplyPopup(GetComponent<Canvas>(), GameConfig.UiSort.BattleResult);

        var rootT = transform.Find("Root");
        var panelT = rootT != null ? rootT.Find("Panel") : null;
        _panel = panelT as RectTransform;
        if (rootT == null || _panel == null)
        {
            Debug.LogError("[BattleResultScreen] 预制体缺 Root/Panel 节点（结构被改过？），本局不播结果界面");
            Destroy(gameObject);
            onDone?.Invoke();
            return;
        }

        // 胜利/失败是 Panel 的直属子节点；特效子树里还有一个叫「胜利」的粒子节点，深度查找会撞名
        var showNode = panelT.Find(victory ? "胜利" : "失败");
        var hideNode = panelT.Find(victory ? "失败" : "胜利");
        if (showNode == null || hideNode == null)
        {
            Debug.LogError("[BattleResultScreen] 预制体 Panel 下缺 胜利/失败 节点，本局不播结果界面");
            Destroy(gameObject);
            onDone?.Invoke();
            return;
        }
        ShiftPanelUp(_panel);   // 【2026-11-01 主人拍板】整体上移 40
        BindSprite(showNode, victory);
        showNode.gameObject.SetActive(true);
        hideNode.gameObject.SetActive(false);   // 另一个节点的粒子随失活停止（预制体两节点默认全 active）

        // 【2026-10-31 主人拍板】胜负曲接入：结果界面弹出即播对应 jingle（胜利/失败统一门，含竞技场）
        GameSting.PlayBattleResult(victory, this);

        // 点击任意处跳过：Dim 全屏钮 + 胜负图上的钮，同一个出口（2026-10-24 主人拍板）
        WireSkip(rootT.Find("Dim"));
        WireSkip(showNode);

        _cg = gameObject.AddComponent<CanvasGroup>();
        _cg.alpha = 0f;
        if (victory)
        {
            // 【2026-10-25 主人拍板】胜利新流程：弹出 → 上移 → 奖励 Panel 淡入 → 确认/点任意处 → 回冒险界面
            _victoryNode = showNode;
            _victoryRt = showNode as RectTransform;
            SetupRewardPanel(showNode, stats, extraCells);
            // 【2026-10-29 v4】订阅分享发奖事件：本次点分享领完回来 → 切「分享过后」排版（OnDestroy 退订）
            ShareService.ShareRewardGranted += OnShareRewardGranted;
            StartCoroutine(CoPlayVictory());
        }
        else
        {
            ShiftFailNodeUp(showNode as RectTransform);   // 【2026-10-31 主人拍板「战斗失败节点往上挪100」】
            // 【2026-10-31 主人拍板】失败界面的 确定/返回 钮放大 30%
            EnlargeResultButton(showNode.Find("确定"));
            EnlargeResultButton(showNode.Find("取消"));
            StartCoroutine(CoPlay());   // 失败非选择模式照旧：停留后自动收
        }
    }

    /// <summary>【2026-11-01 主人拍板「这种小的弹窗都整体往上移40」】结果界面 Panel 整体上移量（只动 y，
    /// x/尺寸不碰）。Panel 是 胜利/失败 两节点的父级，挪一处四种形态（胜利/失败直出/复活选择/竞技场失败）全生效；
    /// 失败节点此前单独拍板的 +100（<see cref="ShiftFailNodeUp"/>）照旧叠加，互不影响。</summary>
    const float PanelShiftUpY = 40f;

    /// <summary>失败节点整体上移 100（2026-10-31 主人拍板「战斗失败节点往上挪100 胜利节点不动」；
    /// 纵向 y 属必要介入口径，x/尺寸一律不碰）。三种失败显示（直出 / 复活选择 / 竞技场仅返回）共用。</summary>
    static void ShiftFailNodeUp(RectTransform failRt)
    {
        if (failRt == null) return;
        failRt.anchoredPosition = new Vector2(failRt.anchoredPosition.x, failRt.anchoredPosition.y + 100f);
    }

    /// <summary>【2026-11-01 主人拍板】结果界面 Panel 整体上移 <see cref="PanelShiftUpY"/>（只动 y）。
    /// 三个入口（Play / PlayChoice / PlayArenaFailure）各调一次，Panel 是实例化出来的、每次新建不存在叠加。</summary>
    static void ShiftPanelUp(RectTransform panelRt)
    {
        if (panelRt == null) return;
        panelRt.anchoredPosition = new Vector2(panelRt.anchoredPosition.x, panelRt.anchoredPosition.y + PanelShiftUpY);
    }

    /// <summary>
    /// 摆主人手摆的奖励区（胜利 → Panel[CanvasGroup] → 装饰 / ConfirmBtn / CellTemplate）：
    /// CG 默认压 0（拍板「默认是 0」，prefab 里是 1 由运行时压，prefab 零改动）；
    /// 克隆 CellTemplate 填本局奖励（奖励已在结算链自动入账，界面只展示 —— 「打完自动领取宝箱后的奖励」）；
    /// ConfirmBtn 接【确定】= 确认收下 → 走 onDone 回冒险界面。缺节点/缺图 LogError 不静默（铁律 14）。
    /// </summary>
    void SetupRewardPanel(Transform victoryNode, BattleRunStats stats, List<SettlementRewardCell> extraCells = null)
    {
        var rewardPanel = victoryNode.Find("Panel");
        if (rewardPanel == null)
        {
            Debug.LogError("[BattleResultScreen] 胜利节点下缺奖励 Panel（预制体结构被改过？），奖励区不显示");
            return;
        }
        _rewardPanelT = rewardPanel;
        _rewardCg = rewardPanel.GetComponent<CanvasGroup>();
        if (_rewardCg == null)
            _rewardCg = rewardPanel.gameObject.AddComponent<CanvasGroup>();
        _rewardCg.alpha = 0f;   // 【2026-10-25 主人拍板】默认 0，上移到位后淡到 1

        // 【2026-10-26 主人拍板「界面做下适配可以再宽点」】奖励面板加宽 500→620（与外层 Panel 同宽，
        // 只放宽高度不动）：装得下 5 格奖励行（5×109+4×10=585），也给居中分享钮 + 提示行留内宽
        var rewardRt = (RectTransform)rewardPanel;
        rewardRt.sizeDelta = new Vector2(RewardPanelWidth, rewardRt.sizeDelta.y);

        BuildRewardCells(rewardPanel, stats, extraCells);

        var confirmT = rewardPanel.Find("ConfirmBtn");
        var confirmBtn = confirmT != null ? confirmT.GetComponent<Button>() : null;
        if (confirmBtn == null)
        {
            Debug.LogError("[BattleResultScreen] 奖励 Panel 下缺 ConfirmBtn（预制体结构被改过？），只能点任意处跳过");
            return;
        }
        confirmBtn.onClick.RemoveAllListeners();
        confirmBtn.onClick.AddListener(OnConfirmClicked);
        var lbl = confirmBtn.GetComponentInChildren<Text>(true);
        // 【2026-10-31 主人拍板「胜利有返回和分享」】竞技场胜利的确认钮文案 = 返回（行为不变 = 收下结算回城）；
        // 普通关照旧「确定」。
        if (lbl != null)
            lbl.text = BattleManager.Instance != null && BattleManager.Instance.IsArena ? "返回" : "确定";

        // 【2026-10-29 主人拍板 v4（预制体手摆版）】分享钮/说明改绑主人手摆节点，不再运行时克隆：
        // 「分享按钮」（奖励 Panel 下）接线 OnShareClicked；「分享」说明（胜利节点下）跟随显隐。
        // 排版 = 可分享用主人摆的原样；分享过后（今日已领）隐藏分享钮+说明、确定钮 x 归中。
        // 【2026-10-27 主人口径「引导没有分享按钮」】引导局两节点隐藏、确定钮居中。
        _confirmRt = confirmT as RectTransform;
        _confirmBaseX = _confirmRt != null ? _confirmRt.anchoredPosition.x : 0f;   // 读预制体值不写死
        BindShareNodes(victoryNode, rewardPanel);
        // 【2026-11-12 主人拍板「新增下一关按钮 复用项目里的按钮 大小和其他按钮一致」】主线 Normal
        // 同章有下一关时，把主人手摆的「分享按钮」节点复用为【下一关】钮（分享 2026-11-02 已下线，
        // 节点闲置且本就与确定钮并排同尺寸 —— 零预制体改动、大小天然一致；图/摆位一律按预制体，铁律 9/32）：
        // 子 Text 改「下一关」、Button 接 OnNextStageClicked；确定钮回 _confirmBaseX 双钮并排基线
        //（RefreshShareLayout 的 _nextStageActive 前置分支负责显隐）。分享功能回归时删掉本段即可。
        if (_nextStageActive && _shareBtnT != null)
        {
            var nextBtn = _shareBtnT.GetComponent<Button>();
            if (nextBtn == null)
                Debug.LogError("[BattleResultScreen] 复用「分享按钮」作下一关钮：节点上没有 Button（预制体结构被改过？），下一关钮不可点");
            else
            {
                nextBtn.onClick.RemoveAllListeners();
                nextBtn.onClick.AddListener(OnNextStageClicked);
            }
            var nextLbl = _shareBtnT.GetComponentInChildren<Text>(true);
            if (nextLbl == null)
                Debug.LogError("[BattleResultScreen] 复用「分享按钮」作下一关钮：节点下缺 Text（预制体结构被改过？），文案不显示");
            else
                nextLbl.text = "下一关";
        }
        // 【2026-10-31 主人拍板】确定/分享钮放大 30%（BindShareNodes 之后才拿得到分享钮节点）
        // 【2026-10-31 二次拍板】胜利这两颗再缩小 30%（×1.3×0.7=0.91）；失败钮先不动
        EnlargeResultButton(confirmT, VictoryBtnScaleMul);
        EnlargeResultButton(_shareBtnT, VictoryBtnScaleMul);
        // 【2026-10-31 主人拍板「在分享按钮下面加上一行字 +100钻石的图标」】
        // 必须在 EnlargeResultButton 之后建：行 y 按按钮缩放后的实际底边算
        BuildShareRewardRow(rewardPanel);
        bool isTutorial = BattleManager.Instance != null && BattleManager.Instance.IsTutorialRun;
        RefreshShareLayout(isTutorial);
    }

    /// <summary>
    /// 【2026-11-27 主人拍板「通关战斗结束界面下面上加一条框和几行黄字，把开启的功能写上」】
    /// 在【确定/下一关】按钮行下方挂一条金框黄字框，多行写本关新开启的功能（文案真源 =
    /// <see cref="FeatureUnlock.NewlyUnlockedText"/>，本类只负责画）。
    /// 底框用主人点名素材「角色_0002s_0010_图层-7」（深底金框九宫格，与概率公示/装备掉落弹窗同款，
    /// 2026-11-27 下午拍板）；黄字统一游戏「金黄字」口径（同「限定」角标），字号 24 同开奖卡第三行。
    /// 与 BuildShareRewardRow 同套路：运行时建树、不碰预制体（铁律 9/32 —— 预制体与已有节点零改动）。
    /// 尺寸走本段常量，改样式只动这里；不接 raycast，绝不挡「点任意处跳过」。
    /// </summary>
    void BuildUnlockBanner(Transform rewardPanel)
    {
        if (string.IsNullOrEmpty(_unlockText) || rewardPanel == null || _confirmRt == null) return;

        // 行数：正文按换行拆 + 顶部标题 1 行（文案里不折行，宽度按最长一行给足）
        int bodyLines = _unlockText.Split('\n').Length;
        float h = Mathf.Max(UnlockMinHeight, (bodyLines + 1) * UnlockLineH + UnlockPadY * 2f);

        var go = new GameObject("新功能开启", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(rewardPanel, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(UnlockBannerWidth, h);

        var bg = go.GetComponent<Image>();
        // 底框：项目标配深底金框（九宫格 spriteBorder 已在导入设置配好，Sliced 拉伸不变形）
        var frame = Resources.Load<Sprite>(UnlockFrameRes);
        if (frame != null)
        {
            bg.sprite = frame;
            bg.type = Image.Type.Sliced;
            bg.color = Color.white;
        }
        else
        {
            Debug.LogError("[BattleResultScreen] 新功能框底图缺失：Resources/" + UnlockFrameRes +
                           ".png（应按铁律4从 Assets/Art/UI/NavCharacter/ 同名挪入），退回纯色底");
            bg.color = UnlockBannerBg;   // 缺图兜底纯色，不静默
        }
        bg.raycastTarget = false;

        // 位置：确定钮实际底边往下（按钮已被 EnlargeResultButton 缩放过，按实际算，同 BuildShareRewardRow）
        // 【2026-11-28 主人拍板】整框再往下挪 100（逻辑像素），只改本框位置，不动按钮/其它 UI
        float btnHalfH = _confirmRt.rect.height * Mathf.Abs(_confirmRt.localScale.y) * 0.5f;
        float btnBottomY = _confirmRt.anchoredPosition.y - btnHalfH;
        rt.anchoredPosition = new Vector2(0f, btnBottomY - UnlockGapY - h * 0.5f - UnlockShiftDownY);

        var txtGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        txtGo.transform.SetParent(go.transform, false);
        var txtRt = (RectTransform)txtGo.transform;
        txtRt.anchorMin = new Vector2(0f, 0f);
        txtRt.anchorMax = new Vector2(1f, 1f);
        txtRt.offsetMin = new Vector2(UnlockPadX, UnlockPadY);
        txtRt.offsetMax = new Vector2(-UnlockPadX, -UnlockPadY);

        var txt = txtGo.GetComponent<Text>();
        txt.font = GameFonts.GetChinese();
        txt.fontSize = UnlockFontSize;
        txt.fontStyle = FontStyle.Normal;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = UnlockTextColor;
        txt.raycastTarget = false;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;   // 文案已按行写好，不靠自动折行
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.lineSpacing = 1.1f;
        // 【2026-11-28 主人拍板】标题用「新功能」（原「新功能开启」），下方仍是本关解锁项
        txt.text = "新功能\n" + _unlockText;

        _unlockBannerT = go.transform;
    }

    /// <summary>
    /// 【2026-11-28 主人拍板「失败了的话把新开功能那个框换成可提升战力的方式」】
    /// 失败界面下方挂同款金框：标题「提升战力」+ 按存档已开放系统挑出的 2~3 条提升途径
    /// （文案真源 = <see cref="FeatureUnlock.PowerUpAdviceItems"/>，本类只负责画），底部一颗「去提升」钮。
    /// 【2026-11-28 主人「分成4列 有几种就分几列」】清单由「逐条换行」改成「一行 N 列」：
    /// N = 当前已开放的途径数（1~4），列宽 = 安全区宽 / N，列间一条暗金细线；框高因此不再随条数变化。
    /// 「去提升」= 放弃复活（与【返回】同一条收尾）+ 回城后自动打开角色页（TownHubController.PendingOpenCharacter）。
    /// 与 BuildUnlockBanner 同套路：运行时建树、不碰预制体。
    /// </summary>
    void BuildPowerUpBanner(Transform parent)
    {
        if (parent == null) return;
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        var items = FeatureUnlock.PowerUpAdviceItems(data);
        if (items == null || items.Count == 0) return;
        int cols = Mathf.Clamp(items.Count, 1, PowerMaxCols);

        // 九宫格边宽（x=左 y=下 z=右 w=上，单位=UI 像素）：缺图时用素材实测值兜底。
        // 🔴 这个框的金边特别宽（左 66 / 右 83），内边距必须按 border 实算，写死小数值就会压边（主人截图那版）。
        var frame = Resources.Load<Sprite>(UnlockFrameRes);
        float bL = 66f, bB = 32f, bR = 83f, bT = 29f;
        if (frame != null) { bL = frame.border.x; bB = frame.border.y; bR = frame.border.z; bT = frame.border.w; }
        float padL = bL + PowerInnerPadX, padR = bR + PowerInnerPadX;
        float padT = bT + PowerInnerPadY, padB = bB + PowerInnerPadY;

        // 【2026-11-28 主人「分成4列 有几种就分几列」】清单只占一行：N 列横排，框高不再随条数变
        float h = padT + PowerTitleH + PowerTitleGap + PowerBodyLineH + PowerBtnGap + PowerBtnH + padB;

        var go = new GameObject("提升战力", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        // 【2026-11-28 主人「这个框再往下 移到最下面」】不再跟着按钮行排（框顶正好压在角色立绘上），
        // 直接钉在屏幕底部：父级改 Root（全屏、随安全区适配），底对齐 + 底边留 PowerBottomMarginY。
        // Root 的子级顺序是 Dim → Panel → 本框（SetAsLastSibling），所以画在失败大图之上。
        var rootRt = transform.Find("Root") as RectTransform;
        go.transform.SetParent(rootRt != null ? rootRt : parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(PowerBannerWidth, h);
        rt.anchoredPosition = new Vector2(0f, PowerBottomMarginY);
        rt.SetAsLastSibling();

        var bg = go.GetComponent<Image>();
        if (frame != null)
        {
            bg.sprite = frame;
            bg.type = Image.Type.Sliced;
            bg.color = Color.white;
        }
        else
        {
            Debug.LogError("[BattleResultScreen] 战力框底图缺失：Resources/" + UnlockFrameRes + ".png，退回纯色底");
            bg.color = UnlockBannerBg;
        }
        bg.raycastTarget = false;

        // 位置已在上面钉死（屏幕底部），不再按按钮行底边推算

        // 标题：单独一条，字号比正文大、颜色更亮，跟清单拉开层级
        var titleGo = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        titleGo.transform.SetParent(go.transform, false);
        var titleRt = (RectTransform)titleGo.transform;
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        // 一次写死：左右内边距 padL/padR；上边离框顶 padT，下边再往下 PowerTitleH
        titleRt.offsetMin = new Vector2(padL, -(padT + PowerTitleH));
        titleRt.offsetMax = new Vector2(-padR, -padT);

        var title = titleGo.GetComponent<Text>();
        title.font = GameFonts.GetChinese();
        title.fontSize = PowerTitleFontSize;
        title.fontStyle = FontStyle.Normal;
        title.alignment = TextAnchor.MiddleCenter;
        title.color = PowerTitleColor;
        title.raycastTarget = false;
        title.horizontalOverflow = HorizontalWrapMode.Overflow;
        title.verticalOverflow = VerticalWrapMode.Overflow;
        title.text = PowerBannerTitle;

        // 清单：一行 N 列（N = 当前已开放的途径数，最多 4），列间一条细分隔线
        var rowGo = new GameObject("Items", typeof(RectTransform));
        rowGo.transform.SetParent(go.transform, false);
        var rowRt = (RectTransform)rowGo.transform;
        rowRt.anchorMin = new Vector2(0f, 1f);
        rowRt.anchorMax = new Vector2(1f, 1f);
        rowRt.pivot = new Vector2(0.5f, 1f);
        rowRt.offsetMin = new Vector2(padL, -(padT + PowerTitleH + PowerTitleGap + PowerBodyLineH));
        rowRt.offsetMax = new Vector2(-padR, -(padT + PowerTitleH + PowerTitleGap));

        float innerW = PowerBannerWidth - padL - padR;
        float colW = innerW / cols;
        for (int i = 0; i < cols; i++)
        {
            var cellGo = new GameObject("Item" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            cellGo.transform.SetParent(rowGo.transform, false);
            var cellRt = (RectTransform)cellGo.transform;
            cellRt.anchorMin = cellRt.anchorMax = new Vector2(0.5f, 0.5f);
            cellRt.pivot = new Vector2(0.5f, 0.5f);
            cellRt.sizeDelta = new Vector2(colW, PowerBodyLineH);
            cellRt.anchoredPosition = new Vector2(-innerW * 0.5f + colW * (i + 0.5f), 0f);

            var cell = cellGo.GetComponent<Text>();
            cell.font = GameFonts.GetChinese();
            cell.fontSize = PowerColFontSize;
            cell.fontStyle = FontStyle.Normal;
            cell.alignment = TextAnchor.MiddleCenter;
            cell.color = PowerBodyColor;
            cell.raycastTarget = false;
            cell.horizontalOverflow = HorizontalWrapMode.Overflow;   // 每条都是短词组，不折行
            cell.verticalOverflow = VerticalWrapMode.Overflow;
            cell.text = items[i];

            // 列间分隔线（最后一列右侧不加）
            if (i < cols - 1)
            {
                var sepGo = new GameObject("Sep", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                sepGo.transform.SetParent(rowGo.transform, false);
                var sepRt = (RectTransform)sepGo.transform;
                sepRt.anchorMin = sepRt.anchorMax = new Vector2(0.5f, 0.5f);
                sepRt.pivot = new Vector2(0.5f, 0.5f);
                sepRt.sizeDelta = new Vector2(1f, PowerBodyLineH * 0.55f);
                sepRt.anchoredPosition = new Vector2(-innerW * 0.5f + colW * (i + 1), 0f);
                var sep = sepGo.GetComponent<Image>();
                sep.color = PowerSepColor;
                sep.raycastTarget = false;
            }
        }

        BuildPowerUpButton(go.transform, padB);
    }

    /// <summary>「去提升」钮：底图与预制体 确定/取消 同一张（绿底、深褐字），尺寸 150×50 并抬离金边。</summary>
    void BuildPowerUpButton(Transform parent, float bottomPad)
    {
        var btnGo = new GameObject("去提升", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(Button));
        btnGo.transform.SetParent(parent, false);
        var brt = (RectTransform)btnGo.transform;
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0f);
        brt.pivot = new Vector2(0.5f, 0f);
        brt.sizeDelta = new Vector2(PowerBtnW, PowerBtnH);
        brt.anchoredPosition = new Vector2(0f, bottomPad);

        var img = btnGo.GetComponent<Image>();
        var sp = Resources.Load<Sprite>(PowerBannerBtnRes);
        if (sp != null)
        {
            img.sprite = sp;
            img.type = Image.Type.Simple;
            img.color = Color.white;
        }
        else
        {
            Debug.LogError("[BattleResultScreen] 去提升钮底图缺失：Resources/" + PowerBannerBtnRes + ".png，退回纯色底");
            img.color = UnlockBannerBg;
        }
        img.raycastTarget = true;

        var btn = btnGo.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;   // 与 WireChoiceButton 同口径：不改观感
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnPowerUpClicked);

        var txtGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        txtGo.transform.SetParent(btnGo.transform, false);
        var trt = (RectTransform)txtGo.transform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        var t = txtGo.GetComponent<Text>();
        t.font = GameFonts.GetChinese();
        t.fontSize = PowerBtnFontSize;                    // 22 号：150×50 的钮上留得出呼吸
        t.fontStyle = FontStyle.Normal;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = PowerBannerBtnTextColor;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.text = PowerBannerBtnText;
    }

    /// <summary>
    /// 【2026-11-28 主人拍板】「去提升」：放弃复活（与【返回】同一条收尾 → 回城），
    /// 并置一次性旗让城镇页起来后直接打开角色界面（装备/佣兵/天赋都在那）。
    /// </summary>
    void OnPowerUpClicked()
    {
        if (_chosen) return;
        TownHubController.PendingOpenCharacter = true;
        OnGiveUpClicked();
    }

    // ── 新功能框样式（2026-11-27 下午拍板：素材底框 + 统一金黄字；改观感只动这一组）─────────
    /// <summary>底框素材：主人点名「角色_0002s_0010_图层-7」（与概率公示/装备掉落弹窗底框同款）。</summary>
    const string UnlockFrameRes = "UI/NavCharacter/角色_0002s_0010_图层-7";
    const float UnlockBannerWidth = 560f;   // 与奖励面板内宽（620 - 两侧留白）对齐，比按钮行宽一圈
    const float UnlockMinHeight = 180f;     // 【2026-11-28 主人拍板】框高加一点（原 100），字上下更松
    const float UnlockPadX = 44f;           // 左右留白避开金框装饰（原图边框宽 66/83px，等比缩放后约 40px）
    const float UnlockPadY = 28f;           // 【2026-11-28 主人拍板】上下留白 16→28，字不贴金边
    const float UnlockLineH = 56f;          // 单行高度（40 号字 + 行距；2026-11-28 改字号同步）
    const float UnlockGapY = 22f;           // 与按钮行底边的间距
    const float UnlockShiftDownY = 100f;    // 【2026-11-28 主人拍板】整框相对按钮行再往下挪 100 逻辑像素
    const int   UnlockFontSize = 40;        // 【2026-11-28 主人拍板】48 偏大，改 40 号左右；改字号须同步 UnlockLineH
    static readonly Color UnlockBannerBg   = new Color(0.10f, 0.10f, 0.13f, 0.94f);   // 缺图兜底纯色（同开奖卡底）
    static readonly Color UnlockTextColor  = new Color(1f, 0.9f, 0.5f, 1f);           // 金黄字（同「限定」角标口径）

    // ── 失败界面「提升战力」框（2026-11-28 主人拍板：失败时把「新功能」框换成可提升战力的方式）─────
    // 与胜利那条金框同一套底图/金黄字，只换文案 + 底部多一颗「去提升」钮（点了放弃复活回城并开角色页）。
    // 【2026-11-28 主人截图「这个排版就不能优化下吗」】内边距不再写死：底框素材九宫格边宽是 左66/下32/右83/上29，
    // 一律按 border 实算再加余量，标题与正文拆成两个 Text 做层级，按钮放大并抬离底边。
    // 【2026-11-28 18:42 主人「这个框小点」】整框等比收一圈：600×283 → 560×235，
    // 字号 36/24/26 → 30/20/22，纵向间距 26/32 → 18/22，内边距 10/12 → 8/8。
    // 收完 4 列仍放得下（内宽 395 ÷ 4 ≈ 99，4 字 ×20 号 = 80，留 19 的呼吸位）。
    const float PowerBannerWidth  = 560f;   // 原 600（与失败大图同宽）；收 40 让整框不那么压屏
    const float PowerInnerPadX    = 8f;     // 金边内侧再留的横向余量（实际 padX = border.x + 本值）
    const float PowerInnerPadY    = 8f;     // 同上，纵向
    const float PowerTitleH       = 38f;    // 标题行高（30 号字）
    /// <summary>标题与清单的间距。
    /// 【2026-11-28 主人「提升战力和按钮可以都空一行」】原 2 → 26；18:42「框小点」收到 18，仍是一整行空隙。</summary>
    const float PowerTitleGap     = 18f;
    const float PowerBodyLineH    = 30f;    // 清单行高（N 列横排，只有一行）
    const int   PowerTitleFontSize = 30;
    /// <summary>清单每列的字号：4 列时列宽约 99，4 字 × 20 号 = 80 刚好留出呼吸位。</summary>
    const int   PowerColFontSize  = 20;
    /// <summary>最多分几列（现在最多 4 条途径：天赋 / 升星 / 招募 / 宠物）。</summary>
    const int   PowerMaxCols      = 4;
    /// <summary>框底边离屏幕底边的距离。
    /// 【2026-11-28 主人「这个框再往下 移到最下面」】原 `PowerBannerGapY=18`（框顶贴按钮行底边）作废，
    /// 现在整框钉在屏幕底部。48 = 给刘海机底部小黑条留的安全量（本界面预制体没挂 SafeAreaFitter，
    /// 24 那种小值在真机会压到 home indicator 上）；要更贴底只改这一个常量。</summary>
    const float PowerBottomMarginY = 48f;
    const float PowerBtnW         = 150f;   // 【2026-11-28 18:42 框小点】170×56 → 150×50
    const float PowerBtnH         = 50f;    // 宽高比 3.0 ≈ 素材 100×33 的 3.03，拉伸不变形
    /// <summary>清单与按钮的纵向间距。
    /// 【2026-11-28 主人「提升战力和按钮可以都空一行」】原 12 → 32；18:42「框小点」收到 22。</summary>
    const float PowerBtnGap       = 22f;
    const int   PowerBtnFontSize  = 22;
    /// <summary>按钮底图：与失败界面的 确定/取消 同一张（绿底圆角，文字浅黄）。</summary>
    const string PowerBannerBtnRes = "UI/Art/Common/确定按钮";
    /// <summary>框内标题（固定一句，正文随存档状态变）。
    /// 【2026-11-28 主人「战力不足改成提升战力 不要否定 多肯定」】原「战力不足」带否定意味，改正向表述。</summary>
    const string PowerBannerTitle = "提升战力";
    /// <summary>按钮文字（2026-11-28 主人拍板：点了回城并直接打开角色界面）。</summary>
    const string PowerBannerBtnText = "去提升";
    /// <summary>标题色：比正文更亮一档，拉开层级。</summary>
    static readonly Color PowerTitleColor = new Color(1f, 0.95f, 0.72f, 1f);
    /// <summary>正文色：沿用金黄字口径。</summary>
    static readonly Color PowerBodyColor  = new Color(1f, 0.88f, 0.52f, 1f);
    /// <summary>列间分隔线色：暗金半透明，只做视觉分栏。</summary>
    static readonly Color PowerSepColor   = new Color(0.79f, 0.64f, 0.15f, 0.45f);
    /// <summary>按钮文字色。
    /// 【2026-11-28 主人「按钮上的字不太明显换个颜色」】原「复活」钮同款浅黄（1, 0.98, 0.54）
    /// 压在绿底上几乎糊成一片 → 改成深褐 0.16/0.10/0.03：绿底上对比最强，也贴合界面的木金配色。
    /// 同款绿底钮在别处（复活/返回）仍用浅黄，这里只改「提升战力」框这颗，不联动。</summary>
    static readonly Color PowerBannerBtnTextColor = new Color(0.16f, 0.10f, 0.03f, 1f);

    /// <summary>
    /// 【2026-10-29 主人拍板 v4（预制体手摆版）】绑主人预制体手摆的分享节点（缺了 LogError 不静默，铁律 14）：
    /// · 奖励 Panel 下「分享按钮」：主人已摆好 Image（图=分享按钮1，guid 引用已落盘）+ Button + 子Text「分享」，
    ///   代码只接线 <see cref="OnShareClicked"/> → SharePanelUI.Show()，图/字/摆位一律不动（铁律 9/32）；
    /// · 胜利节点下「分享」说明：主人预制体里的字只是示例（2026-10-29 14:18 主人拍板「那个说明文字你
    ///   可以修改我只是示例」）→ 代码动态写文案，钻数直引 <see cref="GameConfig.SHARE_REWARD_DIAMOND"/>
    ///   （先例 = 佣兵概率公示直引权重常量，改常量文案自动跟）；字号/颜色/摆位以预制体为准，代码只填字。
    /// 两节点都不克隆、不设图 —— 预制体本有图，不再开第二条加载路径（AGENTS §4 分界线）。
    /// </summary>
    void BindShareNodes(Transform victoryNode, Transform rewardPanel)
    {
        _shareBtnT = rewardPanel.Find("分享按钮");
        _shareTipT = victoryNode.Find("分享");
        if (_shareBtnT == null)
        {
            Debug.LogError("[BattleResultScreen] 奖励 Panel 下缺「分享按钮」节点（预制体结构被改过？），分享入口不显示");
        }
        else
        {
            var btn = _shareBtnT.GetComponent<Button>();
            if (btn == null)
                Debug.LogError("[BattleResultScreen] 「分享按钮」上没有 Button（预制体结构被改过？），分享钮不可点");
            else
            {
                btn.onClick.RemoveAllListeners();
                // 【2026-11-02 主人拍板「去掉分享的按钮和功能」】开关关着就不接线：节点由
                // RefreshShareLayout 整颗隐藏，这里连点击都不挂，防别的路径误触。
                if (ShareFeatureEnabled) btn.onClick.AddListener(OnShareClicked);
            }
        }
        if (_shareTipT == null)
        {
            Debug.LogError("[BattleResultScreen] 胜利节点下缺「分享」说明节点（预制体结构被改过？），分享提示不显示");
        }
        else
        {
            // 说明文案动态写（预制体里的字只是主人示例）：格式照主人示例「分享获得100钻石，每日一次」
            // 【2026-11-02 分享下线】开关关着不再写「分享得钻」文案（节点整颗隐藏，但文案也别留诱导字样）
            var tipText = _shareTipT.GetComponent<Text>();
            if (tipText != null && ShareFeatureEnabled)
                tipText.text = $"分享获得{GameConfig.SHARE_REWARD_DIAMOND}钻石，每日一次";
        }
    }

    /// <summary>
    /// 【2026-10-31 主人拍板「在分享按钮下面加上一行字 +100钻石的图标」】
    /// 在奖励 Panel 下、分享按钮<b>正下方</b>动态建一行「钻石图标 + 『+100钻石』」。
    /// · 摆位：以分享按钮为锚算 —— x 跟按钮一致，y = 按钮<b>实际</b>底边（rect.height × localScale，缩放改过也算得准）
    ///   − <see cref="ShareRewardRowGap"/> − 半行高；不写死绝对坐标（铁律 32 口径的延伸）。
    /// · 图标：<see cref="CommonIconSprites"/> 的 <c>icon_diamond_blue</c>（唯一出口，缺图由它报缺，不自造兜底）。
    /// · 文字：钻数直引 <see cref="GameConfig.SHARE_REWARD_DIAMOND"/>（改常量文案自动跟）；
    ///   字号/颜色对齐上方手摆的「分享获得100钻石，每日一次」说明（22 号、暖白；晚拍板 ×1.2 后为 26 号）。
    /// · 显隐不在这里管 —— 由 <see cref="RefreshShareLayout"/> 跟分享钮一起切（已领/引导局一并隐藏）。
    /// 上面那行说明保留不动（主人 2026-10-31 明确「保留，下面另加一行」）。
    /// </summary>
    void BuildShareRewardRow(Transform rewardPanel)
    {
        // 【2026-11-02 分享下线】不建这一行了 —— 建出来也是隐藏，白搭一次动态节点。
        if (!ShareFeatureEnabled) return;
        if (rewardPanel == null || _shareBtnT == null) return;
        var btnRt = _shareBtnT as RectTransform;
        if (btnRt == null) return;

        var rowGo = new GameObject("分享奖励", typeof(RectTransform));
        rowGo.transform.SetParent(rewardPanel, false);
        var rowRt = (RectTransform)rowGo.transform;
        rowRt.anchorMin = rowRt.anchorMax = new Vector2(0.5f, 0.5f);
        rowRt.pivot = new Vector2(0.5f, 0.5f);
        rowRt.sizeDelta = new Vector2(ShareRewardRowW, ShareRewardRowH);
        // 按钮实际半高 = sizeDelta.y × |localScale.y| / 2（按钮已被 EnlargeResultButton 缩放过，按实际算）
        float btnHalfH = btnRt.rect.height * Mathf.Abs(btnRt.localScale.y) * 0.5f;
        float btnBottomY = btnRt.anchoredPosition.y - btnHalfH;
        rowRt.anchoredPosition = new Vector2(btnRt.anchoredPosition.x,
                                             btnBottomY - ShareRewardRowGap - ShareRewardRowH * 0.5f);

        // 钻石图标（左）+ 文字（右）
        var iconGo = new GameObject("DiamondIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGo.transform.SetParent(rowGo.transform, false);
        var iconRt = (RectTransform)iconGo.transform;
        iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
        iconRt.pivot = new Vector2(0.5f, 0.5f);
        iconRt.sizeDelta = new Vector2(ShareRewardIconSize, ShareRewardIconSize);
        var iconImg = iconGo.GetComponent<Image>();
        iconImg.raycastTarget = false;    // 别挡点击
        iconImg.preserveAspect = true;    // 图标不拉伸
        iconImg.sprite = CommonIconSprites.Load(ShareRewardIconName);

        var txtGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        txtGo.transform.SetParent(rowGo.transform, false);
        var txtRt = (RectTransform)txtGo.transform;
        txtRt.anchorMin = txtRt.anchorMax = new Vector2(0.5f, 0.5f);
        txtRt.pivot = new Vector2(0.5f, 0.5f);
        txtRt.sizeDelta = new Vector2(ShareRewardRowW, ShareRewardRowH);
        var txt = txtGo.GetComponent<Text>();
        txt.font = GameFonts.GetChinese();
        txt.fontSize = ShareRewardFontSize;   // 【2026-10-31 晚 拍板】原 22 ×1.2
        txt.fontStyle = FontStyle.Normal;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = new Color(0.95f, 0.93f, 0.88f, 1f);   // 与上方手摆说明同色
        txt.raycastTarget = false;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.text = $"+{GameConfig.SHARE_REWARD_DIAMOND}";   // 【2026-10-31 晚 拍板】不要「钻石」二字，钻石含义由图标表达

        // 按文字实际渲染宽度把「图标 + 文字」整体水平居中（不靠估宽：换字号/改文案自动对齐）
        // 下限 60：字体首帧未就绪时 preferredWidth 可能给 0，防止图标和文字叠在一起
        float textW = Mathf.Max(txt.preferredWidth, 60f);
        float totalW = ShareRewardIconSize + ShareRewardIconGap + textW;
        txtRt.sizeDelta = new Vector2(textW, ShareRewardRowH);
        iconRt.anchoredPosition = new Vector2(-totalW * 0.5f + ShareRewardIconSize * 0.5f, 0f);
        txtRt.anchoredPosition = new Vector2(
            -totalW * 0.5f + ShareRewardIconSize + ShareRewardIconGap + textW * 0.5f, 0f);

        _shareRewardRow = rowGo.transform;
    }

    /// <summary>
    /// 【2026-11-02 主人拍板「去掉分享的按钮和功能」】分享整体下线：<see cref="ShareFeatureEnabled"/>=false 时
    /// 分享按钮/说明/+100钻石行<b>一律隐藏</b>，确定钮 x 归 0 居中（y/scale/尺寸不动）。
    /// 恢复分享只需把开关改 true —— 下面这套「可分享 / 分享过后」排版逻辑原样还在，不用重写。
    /// </summary>
    void RefreshShareLayout(bool isTutorial)
    {
        // 【2026-11-12 主人拍板「下一关」】下一关态前置分支：「分享按钮」节点已复用为【下一关】钮 →
        // 强制显示它、确定钮回双钮并排基线（_confirmBaseX）；分享说明/钻石行照旧隐藏。
        // 原 shareable 逻辑零改动（分享回归时删掉本分支即可）。
        if (_nextStageActive)
        {
            if (_shareBtnT != null) _shareBtnT.gameObject.SetActive(true);
            if (_shareTipT != null) _shareTipT.gameObject.SetActive(false);
            if (_shareRewardRow != null) _shareRewardRow.gameObject.SetActive(false);
            if (_confirmRt != null)
                _confirmRt.anchoredPosition = new Vector2(_confirmBaseX, _confirmRt.anchoredPosition.y);
            return;
        }
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        bool shareable = ShareFeatureEnabled && !isTutorial && !ShareService.IsRewardClaimedToday(data);

        if (_shareBtnT != null) _shareBtnT.gameObject.SetActive(shareable);
        if (_shareTipT != null) _shareTipT.gameObject.SetActive(shareable);
        // 【2026-10-31】分享钮下面的「钻石图标 +100钻石」行跟分享入口同生共死
        if (_shareRewardRow != null) _shareRewardRow.gameObject.SetActive(shareable);
        if (_confirmRt != null)
            _confirmRt.anchoredPosition = new Vector2(shareable ? _confirmBaseX : 0f, _confirmRt.anchoredPosition.y);

        if (shareable && !_shareEntryReported)
        {
            _shareEntryReported = true;
            GameAnalytics.ReportShareEntryShow("victory");   // 埋点：分享入口曝光（可分享态才算曝光）
        }
    }

    /// <summary>ShareService 当日发奖成功回调（本次点分享领完回来）：切「分享过后」排版。界面已收尾则忽略。</summary>
    void OnShareRewardGranted()
    {
        if (_finished) return;
        bool isTutorial = BattleManager.Instance != null && BattleManager.Instance.IsTutorialRun;
        RefreshShareLayout(isTutorial);
    }

    /// <summary>【分享】钮点击（v4 绑主人预制体手摆的「分享按钮」节点）：打开通用分享界面（SharePanelUI），
    /// 发钻/日键/埋点在分享界面内完成；发奖成功由 ShareService.ShareRewardGranted 事件驱动本界面
    /// 切「分享过后」排版（隐藏分享钮、确定居中）。结果界面不关。
    /// 【2026-11-02 分享下线】开关关着时这里直接 return —— 接线处已经不挂了，这道是防外部路径误调。</summary>
    void OnShareClicked()
    {
        if (!ShareFeatureEnabled) return;
        SharePanelUI.Show();
    }

    /// <summary>
    /// 克隆 CellTemplate 填本局奖励格（数据口径同 BattleSettlementUI.BuildRewardCells：
    /// 金币/天赋石/附魔石/强化材料/装备，图标走 CommonIconSprites 唯一出口）。
    /// 【2026-10-30 竞技场】extraCells（调用方组装好的追加格，如积分格）拼在 stats 格之后；
    /// 默认 null = 原行为零改动。克隆格摆位：横向一排、以模板位置为基准居中，间距/尺寸全读模板值
    /// （铁律 32，不碰预制体 rect）；模板本身 SetActive(false) 只当母版。没奖励就不上格（奖励 Panel 照常淡入，留 装饰+确定）。
    /// </summary>
    void BuildRewardCells(Transform rewardPanel, BattleRunStats stats, List<SettlementRewardCell> extraCells = null)
    {
        var tmplT = rewardPanel.Find("CellTemplate");
        if (tmplT == null)
        {
            Debug.LogError("[BattleResultScreen] 奖励 Panel 下缺 CellTemplate，奖励格不上（不静默）");
            return;
        }
        var cells = stats != null ? BattleSettlementUI.BuildRewardCells(stats) : new List<SettlementRewardCell>();
        if (extraCells != null)
            cells.AddRange(extraCells);   // 追加格（icon/label 调用方组装好，这里只负责克隆展示）
        if (cells.Count == 0)
        {
            tmplT.gameObject.SetActive(false);
            return;
        }

        var tmplRt = (RectTransform)tmplT;
        float cellW = tmplRt.sizeDelta.x;
        float total = cells.Count * cellW + (cells.Count - 1) * RewardCellGap;
        // 行基线 = 模板拍板位（10-29 分享钮挪去与确定并排后，10-26 的 +46 上抬作废，回模板基线）
        Vector2 rowCenter = new Vector2(tmplRt.anchoredPosition.x - total * 0.5f + cellW * 0.5f,
                                        tmplRt.anchoredPosition.y);

        for (int i = 0; i < cells.Count; i++)
        {
            var data = cells[i];
            var cell = Instantiate(tmplT.gameObject, rewardPanel);
            cell.name = "RewardCell";
            var rt = (RectTransform)cell.transform;
            rt.anchoredPosition = rowCenter + new Vector2(i * (cellW + RewardCellGap), 0f);
            cell.SetActive(true);

            var frame = cell.GetComponent<Image>();
            if (frame != null) frame.color = data.frameColor;

            var iconTf = cell.transform.Find("Icon");
            var icon = iconTf != null ? iconTf.GetComponent<Image>() : null;
            if (icon != null)
            {
                if (data.icon != null)
                {
                    icon.sprite = data.icon;
                    icon.color = Color.white;
                    icon.enabled = true;
                }
                else
                {
                    // 缺图标不静默留灰块让主人看见（不自己造图，同 BattleSettlementUI 口径）
                    Debug.LogError($"[BattleResultScreen] 奖励格「{data.label}」缺图标，留灰块（不静默不造图）");
                    icon.color = new Color(0.3f, 0.28f, 0.35f, 1f);
                }
                icon.preserveAspect = true;
            }

            var labelTf = cell.transform.Find("Label");
            var label = labelTf != null ? labelTf.GetComponent<Text>() : null;
            if (label != null)
            {
                label.text = data.count > 1 ? $"{data.label}×{data.count}" : data.label;
                // 【2026-10-31 晚 主人拍板「战斗胜利获得的道具下面的字放大30%」】在预制体字号基础上 ×1.3，不写死
                label.fontSize = Mathf.RoundToInt(label.fontSize * 1.3f);
            }

            GameFonts.ApplyToHierarchy(cell.transform);
        }
        tmplT.gameObject.SetActive(false);   // 模板只当克隆母版，不自己显示（防多一个假格子）
    }

    /// <summary>【确定】= 确认收下奖励 → 段四放行 → 淡出 → onDone（回冒险界面链照旧）。</summary>
    void OnConfirmClicked()
    {
        if (_finished) return;
        _confirmed = true;
    }

    /// <summary>【2026-11-12 主人拍板「下一关」】点击守卫先行：onNextStageGuard 返回 false = 拦下（界面不收，
    /// 守卫侧已弹「体力不足 / 购买体力」弹窗，玩家处理后可再点本钮）。守卫必须在本界面存活期执行 ——
    /// 不能等到淡出销毁后的 onNextStage（那时本界面已毁，弹窗没挂靠、玩家会卡在空场景）。
    /// 放行 → _nextStageChosen + 复用段四放行 → 段五淡出 → _onNextStage（直进下一关）。</summary>
    void OnNextStageClicked()
    {
        if (_finished) return;
        if (_onNextStageGuard != null && !_onNextStageGuard()) return;
        _nextStageChosen = true;
        _confirmed = true;
    }

    /// <summary>
    /// 【2026-10-25 主人拍板】胜利新时间轴：弹出胜利节点（中部）→ 上移到预制体拍板位（≈250，
    /// 读预制体值不写死）→ 奖励 Panel 淡入（0→1）→ 等点【确定】或点任意处跳过 → 淡出 → onDone
    /// （确认与跳过同一个出口，结算 → 回冒险界面链照旧）。全程 unscaled（战斗结束可能定格/慢动作）。
    /// </summary>
    IEnumerator CoPlayVictory()
    {
        Vector3 baseScale = _victoryNode.localScale;      // 读预制体基准值只做倍乘（铁律 2/32，不写死 1）
        Vector2 targetPos = _victoryRt.anchoredPosition;  // 拍板位读预制体（≈上移到250），代码不写死
        _victoryRt.anchoredPosition = new Vector2(targetPos.x, 0f);   // 从中部弹出（只动 y，纵向必要介入；x 不碰）

        // 段一：淡入 + 胜利图弹出（【2026-10-31 主人拍板】从小(0.5)放大到峰值 1.15 再回原大小(1)，
        // 两段线性、全程 InDur=0.28s 跑完 —— 快节奏，过冲比原 EaseOutBack 更看得出来）
        float t = 0f;
        while (t < InDur && !_skipped)
        {
            t += Dt();
            float k = Mathf.Clamp01(t / InDur);
            _cg.alpha = k;
            float s = k < VictoryPopUpFrac
                ? Mathf.Lerp(0.5f, VictoryPopPeak, k / VictoryPopUpFrac)
                : Mathf.Lerp(VictoryPopPeak, 1f, (k - VictoryPopUpFrac) / (1f - VictoryPopUpFrac));
            _victoryNode.localScale = baseScale * s;
            yield return null;
        }
        _cg.alpha = 1f;
        _victoryNode.localScale = baseScale;

        // 段一点五：停顿 —— 【2026-10-29 主人拍板「先出来胜利图片 有个停顿在上移出现物品框」】
        // 胜利图独自停留一拍，再开始上移 + 奖励 Panel 淡入（点任意处跳过仍有效）
        t = 0f;
        while (t < VictoryHoldDur && !_skipped)
        {
            t += Dt();
            yield return null;
        }

        // 段二：上移到拍板位
        if (targetPos.y > 1f)
        {
            t = 0f;
            while (t < MoveUpDur && !_skipped)
            {
                t += Dt();
                float k = Mathf.Clamp01(t / MoveUpDur);
                _victoryRt.anchoredPosition = new Vector2(targetPos.x, Mathf.Lerp(0f, targetPos.y, k));
                yield return null;
            }
            _victoryRt.anchoredPosition = targetPos;
        }

        // 段三：奖励 Panel 淡入（默认 0 → 1，拍板口径）
        if (_rewardCg != null)
        {
            t = 0f;
            while (t < RewardInDur && !_skipped)
            {
                t += Dt();
                _rewardCg.alpha = Mathf.Clamp01(t / RewardInDur);
                yield return null;
            }
            _rewardCg.alpha = 1f;
        }

        // 【2026-11-12 主人拍板「奖励展示完再弹升级窗」】奖励 Panel 淡入完成后回调
        //（FinishStageAfterPortalReached 传 LevelUpPopup.FlushPending）：升级窗(996)盖在本界面(985)上，
        // 点黑幕逐级看完才露出本界面继续点确定/下一关。
        // 【2026-11-14 修误点】奖励演完前的点任意处已在 Skip() 里作废，故此处 _skipped 恒为 false，
        // 升级窗一定在本界面上弹出并看完，不会再退化成「跳过→回城后弹到冒险界面」。
        if (!_skipped)
            _onRewardsShown?.Invoke();
        // 【2026-11-27 主人拍板】先在按钮行下方挂「新功能开启」金框黄字框（本关解锁了功能才建；
        // 【2026-11-28 主人拍板】第 3 关不再建本框（与宠物指路气泡重叠），真源在 FeatureUnlock.NewlyUnlockedText）。
        // 再弹宠物指路气泡：有解锁框时锚到框底，第 3 关无框则锚确定按钮，两框不撞。
        BuildUnlockBanner(_rewardPanelT);
        // 【2026-11-25 主人拍板】第一章第三关首通：结算界面下方贴一条「回角色界面开启宠物上阵」气泡。
        // 判定与文案真源在 PetFeatureGuide；本类只负责在奖励展示完这个时点把锚点交出去。
        // 【2026-11-27】有解锁框时锚到框底，没框时仍锚确定按钮（原口径）。
        PetFeatureGuide.TryShowSettlementGuide(_unlockBannerT != null ? (RectTransform)_unlockBannerT : _confirmRt);
        // 奖励演完 → 放开「点任意处跳过」（之后照旧可跳过收窗）；升级窗显示期间仍由 Skip() 挡住。
        _blockEarlySkip = false;

        // 段四：等【确定】或点击任意处跳过（不自动收 —— 奖励要看清）
        while (!_confirmed && !_skipped)
            yield return null;

        // 段五：淡出 → 销毁 → onDone（回冒险界面链照旧）。
        // 跳过走缩短版；从当前 alpha 归位再淡（中途跳过不闪帧，同旧 CoPlay 口径）
        _victoryNode.localScale = baseScale;
        float outDur = _skipped ? SkipOutDur : OutDur;
        float startAlpha = _cg.alpha;
        t = 0f;
        while (t < outDur)
        {
            t += Dt();
            float k = Mathf.Clamp01(t / outDur);
            _cg.alpha = Mathf.Lerp(startAlpha, 0f, k);
            yield return null;
        }
        _cg.alpha = 0f;
        _finished = true;
        Destroy(gameObject);
        // 【2026-11-12 主人拍板「下一关」】点了【下一关】走 _onNextStage（ContinueToNextStageInPlace：
        // 不回城，黑底字幕直进）；确定/跳过照旧 _onDone（回城链）。失败/选择/竞技场模式不设 _onNextStage。
        if (_nextStageChosen) _onNextStage?.Invoke();
        else _onDone?.Invoke();
    }

    /// <summary>
    /// 失败选择模式：摆失败节点（不接跳过）+ 接主人手摆的预制钮 + 开播等抉择。
    /// 【2026-10-25 主人拍板「战斗失败按我摆的按钮」】双钮 = 失败节点下的「确定」（右，钻石 icon +
    /// 价格文本，语义=复活）/「取消」（左，文本「返回」，语义=放弃）——改用预制钮，
    /// 2026-10-25 早前的运行时建钮方案（BuildChoiceButton）整段删除。预制体里两钮只有 Image
    /// 没挂 Button → 运行时补 Button（transition None 不改观感，先例 = SpeechBubbleTalker
    /// EnsureBoardPokeButton），只接线与同步价格文本，摆位/图标/字体一律以预制体为准（铁律 32）。
    /// </summary>
    public void PlayChoice(int revivePrice, Func<bool> onRevive, Action onGiveUp,
        string reviveLabel = null, bool useAdIcon = false)
    {
        _choiceMode = true;
        _onRevive = onRevive;
        _onGiveUp = onGiveUp;
        BattleResultScreen._activeChoice = this;   // 广告异步回调收窗用（ForceCloseChoice）

        UICanvasSetup.ApplyPopup(GetComponent<Canvas>(), GameConfig.UiSort.BattleResult);

        var rootT = transform.Find("Root");
        var panelT = rootT != null ? rootT.Find("Panel") : null;
        _panel = panelT as RectTransform;
        var showNode = panelT != null ? panelT.Find("失败") : null;
        var hideNode = panelT != null ? panelT.Find("胜利") : null;
        if (rootT == null || _panel == null || showNode == null || hideNode == null)
        {
            Debug.LogError("[BattleResultScreen] 预制体缺 Root/Panel/失败/胜利 节点，跳过复活选择直接死亡收尾");
            Destroy(gameObject);
            onGiveUp?.Invoke();
            return;
        }
        ShiftPanelUp(_panel);   // 【2026-11-01 主人拍板】整体上移 40
        BindSprite(showNode, victory: false);
        showNode.gameObject.SetActive(true);
        hideNode.gameObject.SetActive(false);
        ShiftFailNodeUp(showNode as RectTransform);   // 【2026-10-31 主人拍板「战斗失败节点往上挪100」】

        // 选择模式：Dim 与失败大图上的钮一律不接「点击任意处」（防误触放弃复活机会）
        var dimBtn = rootT.Find("Dim") != null ? rootT.Find("Dim").GetComponent<Button>() : null;
        dimBtn?.onClick.RemoveAllListeners();
        var imgBtn = showNode.GetComponent<Button>();
        imgBtn?.onClick.RemoveAllListeners();

        // 双钮接线 = 主人手摆的预制钮：确定（右）= 复活（钻石 icon + 价格文本，主人口径按预制体观感），
        // 取消（左）= 返回（文本主人已写好「返回」）。缺钮 = 结构被改过，LogError 后直接死亡收尾（不兜底）。
        var confirmT = showNode.Find("确定");
        var cancelT = showNode.Find("取消");
        if (confirmT == null || cancelT == null)
        {
            Debug.LogError("[BattleResultScreen] 失败节点下缺 确定/取消 预制钮（预制体结构被改过？），跳过复活选择直接死亡收尾");
            Destroy(gameObject);
            onGiveUp?.Invoke();
            return;
        }
        WireChoiceButton(confirmT.gameObject, OnReviveClicked);
        WireChoiceButton(cancelT.gameObject, OnGiveUpClicked);
        // 【2026-10-31 主人拍板】复活选择窗的 确定/返回 钮放大 30%（与失败直出同一口径）
        EnlargeResultButton(confirmT);
        EnlargeResultButton(cancelT);
        // 【2026-11-28 主人拍板「失败了的话把新开功能那个框换成可提升战力的方式」】
        // 在双钮下方挂「提升战力」金框（内容按存档已开放系统自动挑 + 一颗「去提升」钮）。
        // 【2026-11-28 主人「这个框再往下 移到最下面」】已改为钉在屏幕底部，不再依赖按钮位置。
        BuildPowerUpBanner(showNode);
        // 价格文本同步真源（主人手摆格式「20复活」→ 用传入的 revivePrice，防预制体占位价过期）；
        // 广告档（2026-10-25 主人拍板）传 reviveLabel=「看广告复活」直接替换整段文案；
        // 【2026-10-29 主人拍板】广告档把确认钮上主人摆的钻石 icon **换成广告图**（AdIconUI：
        // 只换 sprite 不动位置/尺寸，preserveAspect 防变形，文字不挪 —— 不挡字），
        // 结掉 10-25 报过的「广告档下钻石 icon 观感不匹配」遗留；钻石档保持钻石 icon 原样。
        var priceLbl = confirmT.GetComponentInChildren<Text>(true);
        if (priceLbl != null)
        {
            priceLbl.text = reviveLabel ?? $"{revivePrice}复活";
            PurchasePriceStyle.Apply(priceLbl);   // 2026-11-08：复活价格统一描边（文字色仍是预制体摆的）
        }
        // 【2026-11-28 主人「我说的一共有多少钻石要放到按钮上面的text 不要放到按钮里面」】
        // 余额提示独立成一条 Text，挂在复活钮正上方（父级 = 复活钮，跟着它的缩放/位置走）；
        // 按钮里只留「{价}复活」主价签。广告档没有钻石消耗 → 不显示余额。
        if (string.IsNullOrEmpty(reviveLabel))
            BuildReviveHaveLabel(confirmT);
        if (useAdIcon)
        {
            if (!AdIconUI.Apply(confirmT))
                Debug.LogError("[BattleResultScreen] 复活钮广告图贴不上（见上一条 Error），广告档维持文字态");
        }

        _cg = gameObject.AddComponent<CanvasGroup>();
        _cg.alpha = 0f;
        // 【2026-10-31 主人拍板】失败选择窗（复活/返回）同样先播失败曲（战斗 BGM 由 GameSting 淡出，
        // 复活成功回战斗时 OnDestroy 里恢复战斗曲）
        GameSting.PlayBattleResult(false, this);
        StartCoroutine(CoPlayChoice());
    }

    /// <summary>给主人手摆的预制钮补 Button 并接线（预制体里只有 Image；transition None 不改观感；
    /// 预制体若日后自己挂了 Button，原样复用只重绑回调）。</summary>
    static void WireChoiceButton(GameObject go, UnityEngine.Events.UnityAction action)
    {
        var btn = go.GetComponent<Button>() != null ? go.GetComponent<Button>() : go.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(action);
    }

    /// <summary>
    /// 【2026-11-28 主人「我说的一共有多少钻石要放到按钮上面的text 不要放到按钮里面」】
    /// 复活钮正上方挂一条余额提示「共 N 钻」：父级 = 复活钮（跟着 1.3 倍缩放与摆位走），
    /// 顶对齐 + 底边距按钮顶边 <see cref="ReviveHaveGapY"/>；按钮内只剩「{价}复活」主价签。
    /// 数值走 <see cref="ResourceWallet"/> 钻石真源，与扣费同源；raycastTarget=false 不挡点击。
    /// </summary>
    void BuildReviveHaveLabel(Transform confirmT)
    {
        if (confirmT == null) return;
        var old = confirmT.Find(ReviveHaveLabelName);
        if (old != null) Destroy(old.gameObject);   // 幂等：重复调用不叠字

        long have = 0;
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data != null) have = ResourceWallet.Get(data, ResourceWallet.ResourceType.Diamond);

        var go = new GameObject(ReviveHaveLabelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(confirmT, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);   // 按钮顶边中点为锚
        rt.pivot = new Vector2(0.5f, 0f);                       // 文字底边对齐锚点
        rt.sizeDelta = new Vector2(ReviveHaveLabelW, ReviveHaveFontSize + 8f);
        rt.anchoredPosition = new Vector2(0f, ReviveHaveGapY);

        var t = go.GetComponent<Text>();
        t.font = GameFonts.GetChinese();
        t.fontSize = ReviveHaveFontSize;
        t.fontStyle = FontStyle.Normal;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = ReviveHaveColor;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.text = "共" + have + "钻";
        PurchasePriceStyle.Apply(t);   // 压在大图上，统一描边保证可读
    }

    /// <summary>复活钮上方余额提示的节点名（重建时按名字清旧节点）。</summary>
    const string ReviveHaveLabelName = "钻石总数";

    /// <summary>
    /// 竞技场失败·仅返回（2026-10-31 主人拍板）：失败图 + 【取消】钮（文本主人已写好「返回」）一个出口。
    /// · 【确定】（复活钮）整颗隐藏 —— 竞技场没有复活（ArenaBattleMode.OffersRevive=false 同拍板）；
    /// · Dim / 失败大图一律不接「点击任意处跳过」（旧 holdUntilTap 点任意处回城口径作废）；
    /// · 缺【取消】钮 = 预制体结构被改过，LogError 后退回 Dim 点击收尾（出口必须留一个，不锁死玩家）。
    /// </summary>
    public void PlayArenaFailure(Action onReturn)
    {
        _arenaReturn = onReturn;

        UICanvasSetup.ApplyPopup(GetComponent<Canvas>(), GameConfig.UiSort.BattleResult);

        var rootT = transform.Find("Root");
        var panelT = rootT != null ? rootT.Find("Panel") : null;
        _panel = panelT as RectTransform;
        var showNode = panelT != null ? panelT.Find("失败") : null;
        var hideNode = panelT != null ? panelT.Find("胜利") : null;
        if (rootT == null || _panel == null || showNode == null || hideNode == null)
        {
            Debug.LogError("[BattleResultScreen] 预制体缺 Root/Panel/失败/胜利 节点，竞技场失败界面不播直接收尾");
            Destroy(gameObject);
            onReturn?.Invoke();
            return;
        }
        ShiftPanelUp(_panel);   // 【2026-11-01 主人拍板】整体上移 40
        BindSprite(showNode, victory: false);
        showNode.gameObject.SetActive(true);
        hideNode.gameObject.SetActive(false);
        ShiftFailNodeUp(showNode as RectTransform);   // 【2026-10-31 主人拍板「战斗失败节点往上挪100」】

        // 唯一出口 = 主人手摆的【取消】钮（文本「返回」）；【确定】（复活钮）整颗隐藏
        var confirmT = showNode.Find("确定");
        if (confirmT != null) confirmT.gameObject.SetActive(false);
        var cancelT = showNode.Find("取消");
        if (cancelT == null)
        {
            Debug.LogError("[BattleResultScreen] 失败节点下缺【取消】预制钮（预制体结构被改过？），退回 Dim 点击收尾");
            var dimT = rootT.Find("Dim");
            if (dimT == null)
            {
                Debug.LogError("[BattleResultScreen] Dim 也没有，竞技场失败界面无出口，直接收尾");
                Destroy(gameObject);
                onReturn?.Invoke();
                return;
            }
            WireChoiceButton(dimT.gameObject, OnArenaReturnClicked);
        }
        else
        {
            WireChoiceButton(cancelT.gameObject, OnArenaReturnClicked);
            EnlargeResultButton(cancelT);   // 【2026-10-31 主人拍板】竞技场失败【返回】钮放大 30%
            // 【2026-11-01 主人报「按钮歪了」】竞技场失败只剩【返回】一颗钮：预制体里 取消x=-124/确定x=+129
            // 是双钮对称摆位，【确定】隐藏后单钮留在左位不居中 → x 归 0（只动 x，y/缩放/尺寸不动；
            // 先例 = 胜利确定钮「分享过后 x 归中」同文件同手法）。复活选择窗双钮态不走此处，不受影响。
            if (cancelT is RectTransform cancelRt)
                cancelRt.anchoredPosition = new Vector2(0f, cancelRt.anchoredPosition.y);
            // Dim 不接跳过：失败只有「返回」一个出口（拍板口径）
            var dimBtn = rootT.Find("Dim") != null ? rootT.Find("Dim").GetComponent<Button>() : null;
            if (dimBtn != null) dimBtn.onClick.RemoveAllListeners();
            var imgBtn = showNode.GetComponent<Button>();
            if (imgBtn != null) imgBtn.onClick.RemoveAllListeners();
        }

        _cg = gameObject.AddComponent<CanvasGroup>();
        _cg.alpha = 0f;
        GameSting.PlayBattleResult(false, this);
        StartCoroutine(CoPlayArenaFailure());
    }

    void OnArenaReturnClicked()
    {
        if (_arenaReturned) return;
        _arenaReturned = true;
    }

    /// <summary>竞技场失败时间轴：淡入弹出 → 等【返回】 → 淡出 → onReturn（toast 扣分 + 回城 + 重开竞技场弹窗）。</summary>
    IEnumerator CoPlayArenaFailure()
    {
        Vector3 baseScale = _panel.localScale;   // 读预制体基准值只做倍乘（铁律 2/32，不写死 1）

        // 段一：淡入 + 弹出（与失败直出同一手感）
        float t = 0f;
        while (t < InDur)
        {
            t += Dt();
            float k = Mathf.Clamp01(t / InDur);
            _cg.alpha = k;
            _panel.localScale = baseScale * Mathf.Lerp(0.5f, 1f, BackOut(k));
            yield return null;
        }
        _cg.alpha = 1f;
        _panel.localScale = baseScale;

        // 段二：等玩家点【返回】（唯一出口，不自动收）
        while (!_arenaReturned)
            yield return null;

        // 段三：淡出 → 销毁 → onReturn
        t = 0f;
        while (t < OutDur)
        {
            t += Dt();
            _cg.alpha = 1f - Mathf.Clamp01(t / OutDur);
            yield return null;
        }
        _cg.alpha = 0f;
        _finished = true;
        Destroy(gameObject);
        _arenaReturn?.Invoke();
    }

    void OnReviveClicked()
    {
        if (_chosen) return;
        // 返回 false = 未立即复活（钻石不足 toast 已提示 / 广告档拉起中，异步回调成功走 ForceCloseChoice）
        // → 界面停留，可再点或点【返回】
        if (_onRevive == null || !_onRevive()) return;
        _chosen = true;
        _choseGiveUp = false;
    }

    void OnGiveUpClicked()
    {
        if (_chosen) return;
        _chosen = true;
        _choseGiveUp = true;
    }

    /// <summary>
    /// 广告核验通过后的强制收窗（2026-10-25 广告版）：等效「复活钮点击成功」——
    /// 置 _chosen 走正常淡出、不触发 onGiveUp（死亡收尾），战斗由 BattleManager.DoReviveHero 恢复。
    /// 仅选择模式且尚未选定时有意义；其余情况静默忽略（调用方有 _reviveChoiceOpen 前置守卫）。
    /// internal：唯一调用方 = 同文件 BattleResultScreen.ForceCloseChoice（广告回调收窗单一出口）。
    /// </summary>
    internal void ForceCloseReviveChosen()
    {
        if (!_choiceMode || _chosen) return;
        _chosen = true;
        _choseGiveUp = false;
    }

    /// <summary>结果界面创建即收掉还开着的战斗内怪物信息弹窗：它挂在 sort 1000 的 Canvas 上，
    /// 压过本界面 985，且全屏透明点击拦截层会吞掉「确定/下一关」的点击。三个 Show 都经过本组件。</summary>
    void Awake()
    {
        MonsterTipPanel.Hide();   // 幂等；不触发 onClose，未看过的怪下次进战斗照常提示
    }

    void OnDestroy()
    {
        // 【2026-11-25】结算指路气泡随结果界面一起收：TutorialHintUI 是 DDOL，不收会残留到城镇。
        PetFeatureGuide.DismissSettlementGuide();
        ShareService.ShareRewardGranted -= OnShareRewardGranted;   // 实例销毁必退订（防悬空事件）
        // 【2026-10-31 主人报修「失败音乐 2 秒后就没了」】失败曲循环开着，界面一关就停
        //（胜利曲一次性，StopDefeat 不动它；场景切换的既有 Stop 依旧兜底）
        GameSting.StopDefeat(this);
        if (BattleResultScreen._activeChoice == this)
            BattleResultScreen._activeChoice = null;
        // 【2026-10-31 主人拍板】复活成功回战斗：失败曲停掉 + 恢复战斗曲
        //（仅选择模式且选定复活时触发；返回/胜利/失败直出走场景切换自动停，不在这恢复）
        if (_choiceMode && _chosen && !_choseGiveUp)
            GameSting.StopAndResumeBattleBgm();
    }

    IEnumerator CoPlayChoice()
    {
        Vector3 baseScale = _panel.localScale;   // 读预制体基准值只做倍乘（铁律 2/32，不写死 1）

        // 段一：淡入 + 弹出（与正常演出同手感）
        float t = 0f;
        while (t < InDur)
        {
            t += Dt();
            float k = Mathf.Clamp01(t / InDur);
            _cg.alpha = k;
            _panel.localScale = baseScale * Mathf.Lerp(0.5f, 1f, BackOut(k));
            yield return null;
        }
        _cg.alpha = 1f;
        _panel.localScale = baseScale;

        // 段二：原地等玩家二选一（不自动收 —— 收了就没有复活机会了）
        while (!_chosen)
            yield return null;

        // 段三：淡出后按选择分流 —— 返回 → 死亡收尾；复活 → 战斗已在 DoReviveHero 恢复，无回调
        t = 0f;
        while (t < OutDur)
        {
            t += Dt();
            _cg.alpha = 1f - Mathf.Clamp01(t / OutDur);
            yield return null;
        }
        _cg.alpha = 0f;
        _finished = true;
        Destroy(gameObject);
        if (_choseGiveUp)
            _onGiveUp?.Invoke();
    }

    /// <summary>
    /// 按路径设大图（预制体 m_Sprite 引用断 + 图 meta 是团结 56 字符 guid → 铁律 29/30 走路径）。
    /// 取不到 → LogError 不静默（铁律 14）；界面照播（缺图看得见，不卡流程）。
    /// </summary>
    static void BindSprite(Transform node, bool victory)
    {
        var img = node.GetComponent<Image>();
        if (img == null)
        {
            Debug.LogError($"[BattleResultScreen] 节点 {node.name} 上没有 Image，设不了大图");
            return;
        }
        string art = ArtDir + (victory ? "胜利" : "失败");
        var sp = Resources.Load<Sprite>(art);
        if (sp == null)
        {
            Debug.LogError($"[BattleResultScreen] 找不到图 Resources/{art}，结果界面将缺图（不静默）");
            return;
        }
        img.sprite = sp;
    }

    void WireSkip(Transform node)
    {
        if (node == null) return;
        var btn = node.GetComponent<Button>();
        if (btn == null) return;
        btn.onClick.RemoveAllListeners();   // 预制体里本无持久回调，只清运行时残留
        btn.onClick.AddListener(Skip);
    }

    // —— 【2026-11-14 修「战斗结果刚弹出点抽奖 → 直接回冒险界面 + 升级窗跑到冒险界面」】——
    // 根因：结果界面第一帧就把全屏 Dim / 胜负大图接了 Skip，演出刚开始的任意一次点击都会置
    // _skipped → CoPlayVictory 各等待段立即结束 → onDone 直接回城；且段三末尾 onRewardsShown
    // 因 _skipped 不触发 → 升级窗留到 TownSceneBootstrap 回城才弹。
    // 修法（主人 2026-11-14 拍板「这一下误点作废」）：胜利界面奖励演完前不接收 Skip；
    // 升级窗显示期间也不接收（防点黑幕误触）。失败/复活选择/竞技场失败三条路径零改动。
    /// <summary>本次是否为胜利演出（只有胜利时间轴做上面两层加固）。</summary>
    bool _victoryFlow;
    /// <summary>胜利界面奖励 Panel 淡入完成前为 true：这期间点任意处不跳过。</summary>
    bool _blockEarlySkip;

    /// <summary>点击跳过：只打断「未结束」的等待段，随后走缩短淡出。已结束后点击无效。</summary>
    public void Skip()
    {
        if (_finished) return;
        if (_victoryFlow)
        {
            if (_blockEarlySkip) return;         // 结果刚弹出的误点：作废
            if (LevelUpPopup.IsShowing) return;  // 升级窗显示期间：不跳过
        }
        _skipped = true;
    }

    IEnumerator CoPlay()
    {
        Vector3 baseScale = _panel.localScale;   // 读预制体基准值只做倍乘（铁律 2/32，不写死 1）

        // 段一：淡入 + 面板缩放弹出（EaseOutBack 过冲）；全程 unscaled —— 战斗结束可能处于定格/慢动作
        float t = 0f;
        while (t < InDur && !_skipped)
        {
            t += Dt();
            float k = Mathf.Clamp01(t / InDur);
            _cg.alpha = k;
            _panel.localScale = baseScale * Mathf.Lerp(0.5f, 1f, BackOut(k));
            yield return null;
        }

        // 段二：停留（点击可跳过）。
        // 【2026-10-31 主人报「失败我没点自己就消失回城了」】holdUntilTap = 停留段过后不自动收，
        // 一直等到玩家点击任意处才走淡出（选择模式的「不自动收」口径；普通关失败老口径不变）。
        if (_holdUntilTap)
        {
            while (!_skipped)
                yield return null;
        }
        else
        {
            t = 0f;
            while (t < HoldDur && !_skipped)
            {
                t += Dt();
                yield return null;
            }
        }

        // 段三：淡出（跳过走缩短版；先从弹出中途归位再淡，避免跳过瞬间画面跳变）
        _cg.alpha = 1f;
        _panel.localScale = baseScale;
        float outDur = _skipped ? SkipOutDur : OutDur;
        t = 0f;
        while (t < outDur)
        {
            t += Dt();
            _cg.alpha = 1f - Mathf.Clamp01(t / outDur);
            yield return null;
        }
        _cg.alpha = 0f;

        _finished = true;
        Destroy(gameObject);   // 粒子随对象销毁消失（CanvasGroup 淡出管不了世界空间粒子，见任务文档验收 7）
        _onDone?.Invoke();
    }

    /// <summary>
    /// unscaled 帧步长封顶（动效检查发现项，2026-10-26）：本界面全程用 unscaled 时间，
    /// 而分享钮会拉起 Android 系统分享面板/玩家可能切后台——切回时的首帧 unscaledDeltaTime
    /// 可能是数秒级，不封顶会把正在播的淡入/弹出/淡出一步打到终点（动效跳变）。
    /// 0.05s = 正常帧步长上限，正常帧无感；等待段（段四等确认）不累计 t，不受影响。
    /// </summary>
    static float Dt() => Mathf.Min(Time.unscaledDeltaTime, 0.05f);

    /// <summary>回弹缓动（ease-out-back）：中段越过 1 再回来，「砸出来」的手感。</summary>
    static float BackOut(float u)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float p = u - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }
}
