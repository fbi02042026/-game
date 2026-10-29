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
    /// </summary>
    public static void Show(bool victory, BattleRunStats stats, Action onDone)
    {
        if (!Application.isPlaying || !GameSceneGate.IsBattle)
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
        ui.Play(victory, stats, onDone);
    }

    /// <summary>
    /// 失败选择界面（2026-10-24 主人拍板「失败界面加复活和返回，复活先扣钻石」）：
    /// 弹出「失败」画面后<b>不自动收</b>，等玩家二选一 ——
    /// 【复活（X钻）】→ onRevive() 扣钻复活；返回 false（钻石不足，toast 已提示）则界面停留可再点；
    /// 【返回】→ onGiveUp()（死亡收尾 → 遗产结算）。
    /// 选择模式下 Dim 与失败大图不接「点击任意处跳过」（防误触放弃）；胜利界面照旧走 <see cref="Show"/>。
    /// </summary>
    public static void ShowFailureChoice(int revivePrice, Func<bool> onRevive, Action onGiveUp, string reviveLabel = null)
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
        ui.PlayChoice(revivePrice, onRevive, onGiveUp, reviveLabel);
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
    /// <summary>【2026-10-29 主人拍板「先出来胜利图片有个停顿再上移」】胜利图弹出后的停顿（秒）。</summary>
    const float VictoryHoldDur = 0.7f;
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

    // —— 分享钮 v4（2026-10-29 主人拍板，预制体手摆版）专用状态 ——
    Transform _shareBtnT;      // 奖励 Panel 下「分享按钮」（主人手摆：Image+Button+子Text，图已拖好）
    Transform _shareTipT;      // 胜利节点下「分享」说明文字（主人手摆，跟随分享钮显隐）
    RectTransform _confirmRt;  // 确定钮（分享过后切 x 居中用；y/scale/尺寸一律不动）
    float _confirmBaseX;       // 确定钮预制体原 x（= 主人摆的可分享位；打开时读值，不写死）
    bool _shareEntryReported;  // 分享入口曝光埋点只报一次（可分享态才算曝光）

    // —— 失败选择模式（复活/返回）专用状态 ——
    bool _choiceMode;
    bool _chosen;
    bool _choseGiveUp;
    Func<bool> _onRevive;
    Action _onGiveUp;

    /// <summary>按胜负摆节点 + 自适应 + 接线 + 开播。</summary>
    public void Play(bool victory, BattleRunStats stats, Action onDone)
    {
        _onDone = onDone;

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
        BindSprite(showNode, victory);
        showNode.gameObject.SetActive(true);
        hideNode.gameObject.SetActive(false);   // 另一个节点的粒子随失活停止（预制体两节点默认全 active）

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
            SetupRewardPanel(showNode, stats);
            // 【2026-10-29 v4】订阅分享发奖事件：本次点分享领完回来 → 切「分享过后」排版（OnDestroy 退订）
            ShareService.ShareRewardGranted += OnShareRewardGranted;
            StartCoroutine(CoPlayVictory());
        }
        else
        {
            StartCoroutine(CoPlay());   // 失败非选择模式照旧：停留后自动收
        }
    }

    /// <summary>
    /// 摆主人手摆的奖励区（胜利 → Panel[CanvasGroup] → 装饰 / ConfirmBtn / CellTemplate）：
    /// CG 默认压 0（拍板「默认是 0」，prefab 里是 1 由运行时压，prefab 零改动）；
    /// 克隆 CellTemplate 填本局奖励（奖励已在结算链自动入账，界面只展示 —— 「打完自动领取宝箱后的奖励」）；
    /// ConfirmBtn 接【确定】= 确认收下 → 走 onDone 回冒险界面。缺节点/缺图 LogError 不静默（铁律 14）。
    /// </summary>
    void SetupRewardPanel(Transform victoryNode, BattleRunStats stats)
    {
        var rewardPanel = victoryNode.Find("Panel");
        if (rewardPanel == null)
        {
            Debug.LogError("[BattleResultScreen] 胜利节点下缺奖励 Panel（预制体结构被改过？），奖励区不显示");
            return;
        }
        _rewardCg = rewardPanel.GetComponent<CanvasGroup>();
        if (_rewardCg == null)
            _rewardCg = rewardPanel.gameObject.AddComponent<CanvasGroup>();
        _rewardCg.alpha = 0f;   // 【2026-10-25 主人拍板】默认 0，上移到位后淡到 1

        // 【2026-10-26 主人拍板「界面做下适配可以再宽点」】奖励面板加宽 500→620（与外层 Panel 同宽，
        // 只放宽高度不动）：装得下 5 格奖励行（5×109+4×10=585），也给居中分享钮 + 提示行留内宽
        var rewardRt = (RectTransform)rewardPanel;
        rewardRt.sizeDelta = new Vector2(RewardPanelWidth, rewardRt.sizeDelta.y);

        BuildRewardCells(rewardPanel, stats);

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
        if (lbl != null) lbl.text = "确定";

        // 【2026-10-29 主人拍板 v4（预制体手摆版）】分享钮/说明改绑主人手摆节点，不再运行时克隆：
        // 「分享按钮」（奖励 Panel 下）接线 OnShareClicked；「分享」说明（胜利节点下）跟随显隐。
        // 排版 = 可分享用主人摆的原样；分享过后（今日已领）隐藏分享钮+说明、确定钮 x 归中。
        // 【2026-10-27 主人口径「引导没有分享按钮」】引导局两节点隐藏、确定钮居中。
        _confirmRt = confirmT as RectTransform;
        _confirmBaseX = _confirmRt != null ? _confirmRt.anchoredPosition.x : 0f;   // 读预制体值不写死
        BindShareNodes(victoryNode, rewardPanel);
        bool isTutorial = BattleManager.Instance != null && BattleManager.Instance.IsTutorialRun;
        RefreshShareLayout(isTutorial);
    }

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
                btn.onClick.AddListener(OnShareClicked);
            }
        }
        if (_shareTipT == null)
        {
            Debug.LogError("[BattleResultScreen] 胜利节点下缺「分享」说明节点（预制体结构被改过？），分享提示不显示");
        }
        else
        {
            // 说明文案动态写（预制体里的字只是主人示例）：格式照主人示例「分享获得100钻石，每日一次」
            var tipText = _shareTipT.GetComponent<Text>();
            if (tipText != null)
                tipText.text = $"分享获得{GameConfig.SHARE_REWARD_DIAMOND}钻石，每日一次";
        }
    }

    /// <summary>
    /// 【2026-10-29 主人拍板 v4】按「可分享 / 分享过后」切排版：
    /// · 可分享（非引导局 且 今日未领）＝主人摆的排版原样：分享按钮 + 说明显示，确定钮在预制体位（分享右）；
    /// · 分享过后（今日已领）或引导局＝隐藏分享按钮与说明，确定钮 x 归 0 居中（y/scale/尺寸不动）。
    /// 「今日已领」判定走 <see cref="ShareService.IsRewardClaimedToday"/> 唯一出口；本次点分享领奖回来由
    /// <see cref="ShareService.ShareRewardGranted"/> 事件驱动再刷（实例订阅，OnDestroy 退订）。
    /// 动 x 是拍板内容（「分享过后隐藏分享按钮 确定按钮居中」）；其余布局项照旧不碰（铁律 32）。
    /// </summary>
    void RefreshShareLayout(bool isTutorial)
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        bool shareable = !isTutorial && !ShareService.IsRewardClaimedToday(data);

        if (_shareBtnT != null) _shareBtnT.gameObject.SetActive(shareable);
        if (_shareTipT != null) _shareTipT.gameObject.SetActive(shareable);
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
    /// 切「分享过后」排版（隐藏分享钮、确定居中）。结果界面不关。</summary>
    void OnShareClicked()
    {
        SharePanelUI.Show();
    }

    /// <summary>
    /// 克隆 CellTemplate 填本局奖励格（数据口径同 BattleSettlementUI.BuildRewardCells：
    /// 金币/天赋石/附魔石/强化材料/装备，图标走 CommonIconSprites 唯一出口）。
    /// 克隆格摆位：横向一排、以模板位置为基准居中，间距/尺寸全读模板值（铁律 32，不碰预制体 rect）；
    /// 模板本身 SetActive(false) 只当母版。没奖励就不上格（奖励 Panel 照常淡入，留 装饰+确定）。
    /// </summary>
    void BuildRewardCells(Transform rewardPanel, BattleRunStats stats)
    {
        var tmplT = rewardPanel.Find("CellTemplate");
        if (tmplT == null)
        {
            Debug.LogError("[BattleResultScreen] 奖励 Panel 下缺 CellTemplate，奖励格不上（不静默）");
            return;
        }
        var cells = stats != null ? BattleSettlementUI.BuildRewardCells(stats) : new List<SettlementRewardCell>();
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
                label.text = data.count > 1 ? $"{data.label}×{data.count}" : data.label;

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

        // 段一：淡入 + 胜利节点缩放弹出（EaseOutBack 过冲，「砸出来」的手感）
        float t = 0f;
        while (t < InDur && !_skipped)
        {
            t += Dt();
            float k = Mathf.Clamp01(t / InDur);
            _cg.alpha = k;
            _victoryNode.localScale = baseScale * Mathf.Lerp(0.5f, 1f, BackOut(k));
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
        _onDone?.Invoke();
    }

    /// <summary>
    /// 失败选择模式：摆失败节点（不接跳过）+ 接主人手摆的预制钮 + 开播等抉择。
    /// 【2026-10-25 主人拍板「战斗失败按我摆的按钮」】双钮 = 失败节点下的「确定」（右，钻石 icon +
    /// 价格文本，语义=复活）/「取消」（左，文本「返回」，语义=放弃）——改用预制钮，
    /// 2026-10-25 早前的运行时建钮方案（BuildChoiceButton）整段删除。预制体里两钮只有 Image
    /// 没挂 Button → 运行时补 Button（transition None 不改观感，先例 = SpeechBubbleTalker
    /// EnsureBoardPokeButton），只接线与同步价格文本，摆位/图标/字体一律以预制体为准（铁律 32）。
    /// </summary>
    public void PlayChoice(int revivePrice, Func<bool> onRevive, Action onGiveUp, string reviveLabel = null)
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
        BindSprite(showNode, victory: false);
        showNode.gameObject.SetActive(true);
        hideNode.gameObject.SetActive(false);

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
        // 价格文本同步真源（主人手摆格式「20复活」→ 用传入的 revivePrice，防预制体占位价过期）；
        // 广告档（2026-10-25 主人拍板）传 reviveLabel=「看广告复活」直接替换整段文案；
        // 【2026-10-29 主人拍板】广告档把确认钮上主人摆的钻石 icon **换成广告图**（AdIconUI：
        // 只换 sprite 不动位置/尺寸，preserveAspect 防变形，文字不挪 —— 不挡字），
        // 结掉 10-25 报过的「广告档下钻石 icon 观感不匹配」遗留；钻石档保持钻石 icon 原样。
        var priceLbl = confirmT.GetComponentInChildren<Text>(true);
        if (priceLbl != null)
            priceLbl.text = reviveLabel ?? $"{revivePrice}复活";
        if (reviveLabel != null && !AdIconUI.Apply(confirmT))
            Debug.LogError("[BattleResultScreen] 复活钮广告图贴不上（见上一条 Error），广告档维持文字态");

        _cg = gameObject.AddComponent<CanvasGroup>();
        _cg.alpha = 0f;
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

    void OnDestroy()
    {
        ShareService.ShareRewardGranted -= OnShareRewardGranted;   // 实例销毁必退订（防悬空事件）
        if (BattleResultScreen._activeChoice == this)
            BattleResultScreen._activeChoice = null;
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

    /// <summary>点击跳过：只打断「未结束」的等待段，随后走缩短淡出。已结束后点击无效。</summary>
    public void Skip()
    {
        if (_finished) return;
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

        // 段二：停留（点击可跳过）
        t = 0f;
        while (t < HoldDur && !_skipped)
        {
            t += Dt();
            yield return null;
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
