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
    /// <summary>【2026-10-25 主人拍板】奖励 Panel 淡入时长（秒）。</summary>
    const float RewardInDur = 0.3f;
    /// <summary>奖励格横向间距（运行时克隆格用，先例 = BattleSettlementUI 网格 spacing 10）。</summary>
    const float RewardCellGap = 10f;

    // —— 以下为 2026-10-26 主人拍板「分享钮居中收紧 panel 内 + 界面再宽点」新增布局常量 ——
    // （主人明确要求改版奖励区排版，此处按拍板写尺寸；除这几项外仍不碰布局，铁律 32 其余口径不变）
    /// <summary>奖励面板加宽后的宽（预制体 500 → 620，与外层 Panel 同宽；只放宽高度不动）。</summary>
    const float RewardPanelWidth = 620f;
    /// <summary>奖励格行基线整体上抬量（模板 y=9 + 46 = 55，给居中分享钮腾位）。</summary>
    const float RewardRowLiftY = 46f;
    /// <summary>分享钮在奖励面板内的纵向位（奖励行与装饰条之间空档，居中 x=0）。</summary>
    const float ShareBtnLocalY = -45f;
    /// <summary>分享钮 sizeDelta（克隆体自带 0.6 缩放 → 可见 ≈150×76，匹配分享钮图 200:102 比例）。</summary>
    static readonly Vector2 ShareBtnSize = new Vector2(250f, 127f);
    /// <summary>分享提示行（分享钮下方）纵向位（装饰条与面板底边之间）。</summary>
    const float ShareTipLocalY = -130f;
    /// <summary>分享提示行 sizeDelta（单行字，fontSize 24）。</summary>
    static readonly Vector2 ShareTipSize = new Vector2(420f, 28f);
    /// <summary>分享钮图路径（Resources/Art/UI/分享/分享按钮.png；meta 是团结 56 字符标识 → 走路径加载）。</summary>
    const string ShareBtnArt = "UI/Art/分享/分享按钮";

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

        // 【2026-10-26 主人拍板】分享钮改版：居中收紧在奖励面板内 + 换主人给的分享钮图；
        // 下方一行「分享获得100钻 每日一次」提示。两件都挂奖励 Panel 下随 CanvasGroup 一起淡入。
        // 【2026-10-27 主人口径「引导没有分享按钮」】引导局两件都不建（提示行是分享钮的附属）。
        bool isTutorial = BattleManager.Instance != null && BattleManager.Instance.IsTutorialRun;
        if (!isTutorial)
        {
            BuildShareButton(rewardPanel, confirmT);
            BuildShareTip(rewardPanel);
        }
    }

    /// <summary>
    /// 【2026-10-26 主人拍板】胜利奖励区【分享】钮改版（克隆 ConfirmBtn 保 Button，预制体零改动）：
    /// ① 整体居中收紧在奖励面板内（x=0）——旧版横向左移摆 x=-373，在面板（半宽 250）之外、
    ///    720 宽画布上整只出屏，玩家根本看不见点不着，这就是本次要修的排版问题；
    /// ② 钮图换主人给的 <c>Art/UI/分享/分享按钮</c>（蓝底分享图标图；meta 是团结 56 字符标识
    ///    → 铁律 29/30 走路径加载，不碰 guid）；图自带图标不带字 → 删克隆来的文本子节点；
    /// ③ 尺寸按图比例 200:102 × 克隆自带 0.6 缩放 → sizeDelta 250×127 ≈ 可见 150×76。
    /// 点击 → SharePanelUI.Show()（发钻/日键/埋点在分享界面内完成），结果界面不关。
    /// 克隆成功即报 #share_entry_show（scene=victory）。
    /// </summary>
    void BuildShareButton(Transform rewardPanel, Transform confirmT)
    {
        if (confirmT == null) return;   // ConfirmBtn 缺失已在 SetupRewardPanel LogError
        var shareGo = Instantiate(confirmT.gameObject, rewardPanel);
        shareGo.name = "ShareBtn";

        // 摆位：居中收紧在面板内（奖励行已上抬，此位在行与装饰条之间空档，常量见文件头）
        var dst = (RectTransform)shareGo.transform;
        dst.anchoredPosition = new Vector2(0f, ShareBtnLocalY);
        dst.sizeDelta = ShareBtnSize;

        // 换主人给的分享钮图（路径加载）；图自带分享图标 → 克隆来的「分享」文本子节点删掉
        var img = shareGo.GetComponent<Image>();
        if (img == null)
            Debug.LogError("[BattleResultScreen] 分享钮克隆体上没有 Image（ConfirmBtn 结构被改过？），分享钮缺图");
        else
        {
            var sp = Resources.Load<Sprite>(ShareBtnArt);
            if (sp == null)
                Debug.LogError($"[BattleResultScreen] 找不到分享钮图 Resources/{ShareBtnArt}，分享钮缺图（不静默）");
            else
            {
                img.sprite = sp;
                img.preserveAspect = true;
            }
        }
        var oldLbl = shareGo.GetComponentInChildren<Text>(true);
        if (oldLbl != null) Destroy(oldLbl.gameObject);

        var btn = shareGo.GetComponent<Button>();
        if (btn == null)
        {
            Debug.LogError("[BattleResultScreen] 分享钮克隆体上没有 Button（ConfirmBtn 结构被改过？），分享钮不可点");
            return;
        }
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnShareClicked);

        GameAnalytics.ReportShareEntryShow("victory");   // 埋点：分享入口曝光
    }

    /// <summary>
    /// 【2026-10-26 主人拍板】分享钮下方一行提示「分享获得100钻 每日一次」：
    /// 钻数直引 <see cref="GameConfig.SHARE_REWARD_DIAMOND"/>（先例 = 佣兵概率公示直引权重常量，
    /// 改常量文案自动跟）。运行时建 Text（预制体零改动），挂奖励 Panel 下随 CanvasGroup 一起淡入；
    /// raycastTarget=false 不挡「点任意处跳过」的射线。
    /// </summary>
    void BuildShareTip(Transform rewardPanel)
    {
        var tipGo = new GameObject("ShareTip", typeof(RectTransform), typeof(Text));
        var rt = (RectTransform)tipGo.transform;
        rt.SetParent(rewardPanel, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);   // 与面板内其它子节点同口径：中心锚点（面板子节点全是 0.5,0.5）
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        rt.anchoredPosition = new Vector2(0f, ShareTipLocalY);
        rt.sizeDelta = ShareTipSize;
        var t = tipGo.GetComponent<Text>();
        t.text = $"分享获得{GameConfig.SHARE_REWARD_DIAMOND}钻 每日一次";
        t.fontSize = 24;
        t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
        GameFonts.ApplyToHierarchy(tipGo.transform);
    }

    /// <summary>【分享】钮点击：【2026-10-25 主人拍板 v2】改为打开通用分享界面（SharePanelUI），
    /// 不再直弹系统分享面板；发钻/埋点在分享界面内完成。结果界面不关。</summary>
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
        // 行基线整体上抬（2026-10-26 主人拍板：给居中的分享钮腾位，模板 y=9 → 55）；间距/尺寸仍读模板值
        Vector2 rowCenter = new Vector2(tmplRt.anchoredPosition.x - total * 0.5f + cellW * 0.5f,
                                        tmplRt.anchoredPosition.y + RewardRowLiftY);

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
        // 广告档（2026-10-25 主人拍板）传 reviveLabel=「看广告复活」直接替换整段文案
        // （确认钮上的钻石 icon 是主人摆的，广告档下观感不匹配，已报主人定夺）；
        // 钻石 icon 为主人摆的，不动。
        var priceLbl = confirmT.GetComponentInChildren<Text>(true);
        if (priceLbl != null)
            priceLbl.text = reviveLabel ?? $"{revivePrice}复活";

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
