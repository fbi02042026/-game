using System;
using UnityEngine;
using UnityEngine.UI;

// 冒险界面 partial —— 无限裂隙「进度」三件事（2026-11-02 主人拍板）
//
// ① 进本确认：主人原话「无限点击进入关卡弹出提示返回上回保存点继续 问玩家是否继续」
//    → AskEndlessStartChoice：只问「要不要从保存点继续」。是 = 从保存点 +1 层接着打（构筑/装备接回）；
//    否 = 本次不进本（不是「从头开始」——从头开始改由右下角的重置徽章负责，两条路不再抢一个弹窗）。
// ② 重置徽章：主人原话「我把重置按钮放到无限关卡右边的地方…放到无限关卡右下角 不跟随关卡移动」
//    → 用主人给的「魔法重置徽章」图，钉在 无限/关卡 容器右下角外侧；点击弹确认窗，重置后从第 1 层起。
//    每周 1 次（周一 0 点刷新，DayKeyUtil.WeekKey）。
//    【2026-11-03 主人拍板「就按原来世界地图的位置往上100」→ 同日晚澄清】徽章**仍在无限页**，
//    只是位置参考主线那颗世界地图按钮的 spot 再往上抬 100（主人原话「不是让你放到主线标签
//    只是告诉你位置跟主线那个位置类似 只是往上了100 还是在无限标签」）。
//    → 挂「无限」节点下（跟随无限页签显隐），每次刷新按世界地图按钮的 rect 中心换算到无限页
//    局部坐标再 +100；世界地图入口 2026-11-01 起整体隐藏，主线/无限两页签同域，位置正好空着。
//    找不到世界地图按钮（预制体被改）才回退锚「关卡」容器右下角的老挂点。
// ③ 一键直达（2026-11-02 晚）：主人原话「是不是可以加一个一键到历史最高出生点 中间的抽奖全部自动抽」
//    → 生涯最高出生点比本轮保存点更远时，① 的副按钮变成「直达第 N 层」，二次确认后
//    覆盖起始层 + 补偿中间各层的免费抽（ConfirmEndlessJump）。
//
// 奖励口径（主人二选一后定）：重打**生涯已通关**的层只发首通金币的 30%、天赋石不发
//   （GameConfig.ENDLESS_REPEAT_GOLD_RATE，判定见 EndlessRiftRules.NoteBestStage）。
//
// 为什么按钮要运行时建树：预制体里没有这颗钮（主人只给了图），铁律 3 不允许改预制体。
// 位置**锚在「关卡」容器右下角**（anchor 1,0 + 固定外扩距离）——容器本身不会被适配/重排移动，
// 只有容器里的层数牌会动，所以徽章天然「不跟随关卡移动」。
public partial class AdventureUI
{
    /// <summary>重置徽章图（主人 2026-11-02 给的「魔法重置徽章」，131×131 透明底）。</summary>
    const string EndlessResetSpritePath = "UI/Art/Adventure/重置徽章";
    /// <summary>徽章显示边长（原图 131 见方，缩到 96 与层数牌（125 宽）同一量级）。</summary>
    const float EndlessResetBadgeSize = 96f;
    /// <summary>徽章中心距「关卡」容器右下角的外扩距离（【2026-11-03 起】仅回退路径用——
    /// 正常路径徽章已改按世界地图按钮位置对位 + 上抬 100，见 <see cref="PositionEndlessResetBadge"/>）。
    /// 算得开：容器半宽 50 + 48 = 98 &gt; 层数牌右缘 75.5 → 徽章（半边长 48）左缘落在 50+48+48−48 = 98，
    /// 离牌面还有 22 的缝，不会压住「第N层」的牌。</summary>
    const float EndlessResetBadgeMargin = 48f;

    Button _endlessResetBtn;
    Text _endlessResetLbl;
    Image _endlessResetIcon;

    /// <summary>
    /// 【2026-11-02 主人拍板】进裂隙前的「是否返回上回保存点继续」确认。
    /// <para>主人原话：「无限点击进入关卡弹出提示返回上回保存点继续 问玩家是否继续」——
    /// 这里只做「继续 / 不进本」二选，不再把「从头开始」塞进同一个弹窗
    /// （重置进度是独立入口：无限页右下角的重置徽章，每周 1 次）。</para>
    /// <para>【2026-11-02 晚·主人拍板「一键到历史最高出生点 中间的抽奖全部自动抽」】
    /// 当生涯最高出生点比本轮保存点更远时，副按钮改成「直达第 N 层」——
    /// 点它进二次确认，确认后起始层覆盖到最高出生点、中间层的免费抽一次性自动抽完
    /// （见 <see cref="ConfirmEndlessJump"/>）。</para>
    /// </summary>
    /// <param name="proceed">玩家确认继续（或本来就没有保存点可谈）后，继续原本的进本流程。</param>
    void AskEndlessStartChoice(Action proceed)
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null)
        {
            proceed();
            return;
        }

        int cleared = Mathf.Max(0, data.endlessRiftClearedStage);
        int checkpoint = EndlessRiftRules.CheckpointOf(cleared);
        int startStage = checkpoint + 1;
        // 生涯最高出生点 + 1：最高层向下取整到保存点间隔（生涯最高 47 → 出生点 40 → 从 41 层起）
        int bestStart = EndlessRiftRules.BestCheckpoint(data) + 1;
        bool canJump = bestStart > startStage;

        // 从没打过、刚重置过、且没有更远的生涯层 → 没有保存点可谈，直接进
        if (cleared <= 0 && !canJump)
        {
            proceed();
            return;
        }

        // 保存点 0 = 上一局没爬到第 10 层（每 10 层才存一次）→ 没有快照可接，只能从第 1 层重来。
        // 三种情形的文案必须分开写：说「保留构筑」却没有快照，玩家会以为系统吞了进度；
        // cleared = 0（刚重置过）时更不能写「上次止步第 0 层」。
        string body;
        if (cleared <= 0)
            body = "本轮进度已重置，本次将从第 1 层重新开始（上一局的构筑与装备已失效）。";
        else if (checkpoint > 0)
            body = $"上次止步第 {cleared} 层，保存点第 {checkpoint} 层。\n" +
                   $"是否返回上回保存点，从第 {startStage} 层继续？（保留保存点时的构筑与装备）\n" +
                   "想从第 1 层重来，请点页面右下角的重置徽章。";
        else
            body = $"上次止步第 {cleared} 层，还没到第 {GameConfig.ENDLESS_SAVE_INTERVAL} 层存档点。\n" +
                   "本次从第 1 层重新开始（上一局的构筑与装备已失效）。";

        // 跳层补偿的抽数只算一次，文案和实际发放用同一个值（不另算一遍，防两处口径漂移）
        int catchUp = canJump ? EndlessRiftRules.CatchUpDraws(startStage, bestStart) : 0;
        if (canJump)
            body += $"\n\n生涯最高出生点：第 {EndlessRiftRules.BestCheckpoint(data)} 层。" +
                    $"直达第 {bestStart} 层可跳过中间 {bestStart - startStage} 层，" +
                    $"这些层的免费抽 {catchUp} 次会一次性自动抽完。\n" +
                    "不想进入请点弹窗外空白处关闭。";

        // 弹窗期间先把进本标记放下：玩家点暗底/取消时不会把 true 带进下一次进本。
        // （EnterAfterJobSelect 开头会 ResetPendingFlags 再按 endless 重立，选了就自动补回）
        PendingEndlessRift = false;

        StaminaConfirmPopup.ShowChoice(
            title: "无限裂隙",
            body: body,
            okLabel: checkpoint > 0 ? $"继续第 {startStage} 层" : "从第 1 层开始",
            cancelLabel: canJump ? $"直达第 {bestStart} 层" : "暂不进入",
            onOk: () =>
            {
                // 继续 = 什么都不用改：StartStage() 天然 = 保存点 + 1，构筑由 EndlessRiftCheckpoint 接回。
                // ⚠ 必须顺手清掉「直达」的残留（上一次点直达因体力不足没进成时还会留着）——
                //   否则这里选了「继续保存点」，StartStage() 却仍返回覆盖后的深层，静默把玩家带偏。
                // ⚠ 同时收掉可能残留的直达预览（路径：点直达预览 → 二次确认点暗底全关 → 重弹后选继续）
                //   —— 不收的话层数牌还停在预览窗口，与「继续第 11 层」的选择对不上。
                EndlessRiftRules.ClearStartStageOverride();
                EndlessRiftRules.GrantCatchUpDraws(0);
                RestoreRiftPreview();
                proceed();
            },
            // 不进本：弹窗自己关掉，玩家可再点「开始冒险」（点暗底同样是这条出口）
            onCancel: canJump
                ? (Action)(() =>
                {
                    // 【2026-11-02 晚·主人拍板「选择重新打的时候岛快速滑动到最高的保存点」】
                    // 点「直达」的瞬间层数牌编号翻滚到目标窗口停住，二次确认期间玩家亲眼看到
                    // 自己要跳到哪；箭头全程指着底槽（目标窗口的出生点）不挪窝。
                    PreviewRiftSlideTo(bestStart);
                    ConfirmEndlessJump(startStage, bestStart, catchUp, proceed);
                })
                : null);
    }

    /// <summary>
    /// 【2026-11-02 主人拍板「一键到历史最高出生点 中间的抽奖全部自动抽」】直达二次确认。
    /// <para>必须把「跳过了什么、补了什么、没补什么」说全 —— 玩家要知道跳过的层不发首通奖励和宝箱金币，
    /// 否则跳完发现奖励少了会当成 bug 报上来。</para>
    /// <para>确认后：写起始层覆盖（本次进本生效一次）+ 登记补偿抽数，
    /// 然后照原路 proceed 走完体力/次数/进场景，不另开一条进本链路。</para>
    /// </summary>
    void ConfirmEndlessJump(int startStage, int bestStart, int catchUp, Action proceed)
    {
        int skipped = Mathf.Max(1, bestStart - startStage);
        string body =
            $"直接跳到生涯最高出生点：从第 {bestStart} 层开始。\n" +
            $"跳过第 {startStage}～{bestStart - 1} 层，共 {skipped} 层。\n" +
            $"这些层本该获得的免费抽 {catchUp} 次，进本第一层会一次性自动抽完。\n" +
            "跳过的层不补发首通奖励与宝箱金币（想要那些奖励就逐层打）。\n" +
            "本轮保存点不会因此后退。";

        StaminaConfirmPopup.ShowChoice(
            title: "直达最高出生点",
            body: body,
            okLabel: $"直达第 {bestStart} 层",
            cancelLabel: "返回",
            onOk: () =>
            {
                // 预览收尾（瞬间刷回真实窗口）：同帧内 proceed → TryConsumeEnter → HidePage，
                // 玩家看不到还原过程；若体力/次数不足弹窗拦截，页面也已回到真实状态不留预览残影。
                RestoreRiftPreview();
                EndlessRiftRules.SetStartStageOverride(bestStart);
                EndlessRiftRules.GrantCatchUpDraws(catchUp);
                Debug.Log($"[AdventureUI] 无限裂隙直达：起始层 {bestStart}，补偿免费抽 {catchUp} 次");
                proceed();
            },
            onCancel: () =>
            {
                // 返回上层选择：层数牌滑回真实起始层窗口（滑出去要仪式感、滑回来要快）
                RestoreRiftPreview();
                AskEndlessStartChoice(proceed);
            });   // 点暗底不走这里 —— RefreshRiftPage 的「弹窗已关 → 预览失效」对账兜底
    }

    /// <summary>建/取重置徽章，并按当前状态刷文案与可点状态。每次刷新详情面板调一次（幂等）。</summary>
    void RefreshEndlessResetButton(bool endless)
    {
        var data = SaveSystem.Instance?.Data;
        // 【2026-11-08 主人拍板】新号也常驻显示重置徽章，灰态不可点；切到主线页仍由 endless=false 关掉。
        bool worthShowing = endless && data != null;

        if (_endlessResetBtn == null)
        {
            if (!worthShowing) return;
            if (!BuildEndlessResetButton()) return;
        }
        if (_endlessResetBtn.gameObject.activeSelf != worthShowing)
            _endlessResetBtn.gameObject.SetActive(worthShowing);
        if (!worthShowing) return;

        // 每次刷新重摆一次：主线列被 FitMainColumnStretch 重排后，徽章仍跟得住世界地图位置
        PositionEndlessResetBadge();

        // 【2026-11-08 主人拍板】新号无进度 → 灰态不可点，不白烧周额度（data 非空已由 worthShowing 保证）。
        bool hasProgress = data.endlessRiftClearedStage > 0 || data.endlessRiftBestStage > 0;
        bool canReset = hasProgress && EndlessRiftRules.CanResetThisWeek(data);
        // 图标本身没字，下面挂一行小字说明「这是重置」，顺便承载「本周用过了」的冷却信息
        if (_endlessResetLbl != null)
            _endlessResetLbl.text = !hasProgress || canReset
                ? "重置"
                : $"{EndlessRiftRules.DaysUntilReset(data)}天后";

        // 不可用时压暗、不隐藏 —— 玩家要看得见「为什么现在点不了」
        if (_endlessResetIcon != null)
            _endlessResetIcon.color = canReset
                ? Color.white
                : new Color(0.45f, 0.45f, 0.45f, 1f);
        if (_endlessResetLbl != null)
            _endlessResetLbl.color = canReset
                ? new Color(0.98f, 0.88f, 0.62f)
                : new Color(0.58f, 0.55f, 0.50f);
        _endlessResetBtn.interactable = canReset;
    }

    /// <summary>无限页「关卡」容器（层数牌的家；徽章挂在它右下角外侧）。缺节点返回 null。</summary>
    RectTransform FindRiftStageContainer()
    {
        var right = transform.Find("RightContent");
        var endless = right != null ? right.Find(EndlessNodeName) : null;
        return endless != null ? endless.Find("关卡") as RectTransform : null;
    }

    /// <summary>
    /// 把徽章摆到「世界地图按钮的 spot」再往上抬 100（2026-11-03 主人拍板 + 同日晚澄清：
    /// 徽章仍在无限页，只是位置参考主线那颗世界地图按钮，不挪去主线标签）。
    /// <para>对位用世界地图按钮的 rect 中心换算成父节点（「无限」节点）局部坐标
    /// （TransformPoint/InverseTransformPoint，自动吃掉页签容器差异与 Canvas 缩放），
    /// 锚点固定 (0.5,0.5) 以中心对中心，再 +100 设计像素。
    /// 世界地图按钮在 主线 页签下、无限模式里整个 主线 是隐藏的，但 Transform 的层级矩阵
    /// 不受 activeSelf 影响，位置照常取得到。</para>
    /// </summary>
    void PositionEndlessResetBadge()
    {
        if (_endlessResetBtn == null) return;
        var wmRt = FindWorldMapBtnRt();
        if (wmRt == null) return;   // 找不到世界地图按钮（预制体被改）→ 保持建树时的回退挂点，不猜位置
        var parent = _endlessResetBtn.transform.parent as RectTransform;
        if (parent == null) return;

        Vector3 targetWorld = wmRt.TransformPoint(wmRt.rect.center);
        Vector3 local = parent.InverseTransformPoint(targetWorld);
        // 锚 (0.5,0.5) 的锚参考点 = rect 中心（相对 pivot 的局部坐标）；local 同样相对 pivot
        Vector2 anchorRef = new Vector2(
            Mathf.LerpUnclamped(parent.rect.xMin, parent.rect.xMax, 0.5f),
            Mathf.LerpUnclamped(parent.rect.yMin, parent.rect.yMax, 0.5f));
        var rt = (RectTransform)_endlessResetBtn.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = (Vector2)local - anchorRef + new Vector2(0f, 100f);
    }

    bool BuildEndlessResetButton()
    {
        // 2026-11-03 挂点：徽章**留在无限页**（挂「无限」节点，跟随无限页签显隐），只是位置参考
        // 主线那颗世界地图按钮的 spot + 100（见 PositionEndlessResetBadge）。
        // 「无限」节点缺失时依次回退 RightContent → 「关卡」容器，并把错误喊出来不静默。
        var right = transform.Find("RightContent") as RectTransform;
        var endlessNode = right != null ? right.Find(EndlessNodeName) as RectTransform : null;
        RectTransform home;
        bool fellBackToGuanka = false;
        if (endlessNode != null)
        {
            home = endlessNode;
        }
        else if (right != null)
        {
            home = right;
            Debug.LogWarning("[AdventureUI] 重置徽章：RightContent/无限 节点缺失，暂挂 RightContent（预制体结构被改过？）");
        }
        else
        {
            var guanka = FindRiftStageContainer();
            if (guanka == null)
            {
                Debug.LogError("[AdventureUI] 重置徽章建不出来：RightContent 与 RightContent/无限 节点都缺失（预制体结构被改过？）");
                return false;
            }
            home = guanka;
            fellBackToGuanka = true;
        }
        var spr = LoadChestSprite(EndlessResetSpritePath);
        if (spr == null)
            Debug.LogError($"[AdventureUI] 重置徽章图缺失：Resources/{EndlessResetSpritePath}（按钮会留白块，不静默不造图）");

        var go = new GameObject("EndlessResetBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(home, false);
        var rt = (RectTransform)go.transform;
        // 初始挂点几何：锚中心（PositionEndlessResetBadge 随后按世界地图位置覆盖）；
        // 回退路径（无 RightContent）时退化为锚「关卡」容器右下角 + 往外让一段的老挂法
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(EndlessResetBadgeSize, EndlessResetBadgeSize);
        if (fellBackToGuanka)
        {
            // 回退老挂法：锚「关卡」容器右下角 + 往外让一段（此路径 PositionEndlessResetBadge 找不到
            // 世界地图按钮时也不会动它，两条路互不干扰）
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(
                EndlessResetBadgeSize * 0.5f + EndlessResetBadgeMargin,
                -(EndlessResetBadgeSize * 0.5f + EndlessResetBadgeMargin));
        }

        var img = go.GetComponent<Image>();
        img.sprite = spr;
        img.preserveAspect = true;      // 131×131 原图等比缩到 96×96，别拉歪
        img.color = Color.white;
        img.raycastTarget = true;

        var lblGO = new GameObject("Lbl", typeof(RectTransform), typeof(Text));
        lblGO.transform.SetParent(go.transform, false);
        var lrt = (RectTransform)lblGO.transform;
        lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
        lrt.pivot = new Vector2(0.5f, 1f);   // 顶边贴徽章底缘往下长，不压徽章
        lrt.sizeDelta = new Vector2(180f, 24f);
        lrt.anchoredPosition = new Vector2(0f, -EndlessResetBadgeSize * 0.5f + 2f);
        var lbl = lblGO.GetComponent<Text>();
        lbl.font = GameFonts.GetChinese();
        lbl.fontSize = 18;
        lbl.alignment = TextAnchor.MiddleCenter;
        lbl.color = new Color(0.98f, 0.88f, 0.62f);
        lbl.horizontalOverflow = HorizontalWrapMode.Overflow;
        lbl.text = "重置";
        lbl.raycastTarget = false;   // 点击统一落到徽章底图上

        var btn = go.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;   // 观感由 RefreshEndlessResetButton 统一控制
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnEndlessResetClicked);

        _endlessResetBtn = btn;
        _endlessResetLbl = lbl;
        _endlessResetIcon = img;
        return true;
    }

    /// <summary>
    /// 点重置 → 二次确认（复用通用确认弹窗）。正文把后果说全，尤其「重打只发 30%、天赋石不重复发」——
    /// 玩家必须知道自己在放弃什么，否则重置完发现奖励变少会当成 bug 报上来。
    /// </summary>
    void OnEndlessResetClicked()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        if (!EndlessRiftRules.CanResetThisWeek(data))
        {
            GlobalToastUI.Show($"本周已重置过，{EndlessRiftRules.DaysUntilReset(data)} 天后再来", true);
            return;
        }
        // 本轮已经在第 1 层（没打过 / 刚重置过）→ 别白烧一次周额度
        if (data.endlessRiftClearedStage <= 0)
        {
            GlobalToastUI.Show("当前进度已在第 1 层，无需重置", true);
            return;
        }

        int checkpoint = EndlessRiftRules.CheckpointOf(data.endlessRiftClearedStage);
        string body =
            "重置后本轮进度归零，下次从第 1 层重新开始。\n" +
            (checkpoint > 0 ? $"（保存点第 {checkpoint} 层的构筑快照会一起作废）\n" : "") +
            "重打已通关的层只发 30% 金币，天赋石不重复发放。\n" +
            $"生涯最高层（{EndlessRiftRules.BestStage(data)} 层）与排行榜不受影响。\n" +
            "每周只能重置 1 次。";

        StaminaConfirmPopup.Show("重置裂隙进度", body, "重置", () =>
        {
            if (!EndlessRiftRules.TryResetProgress()) return;
            GlobalToastUI.Show("裂隙进度已重置，下次从第 1 层开始", true);
            RefreshAll();   // 层数牌/保存点文案/排行榜/徽章状态一起刷
        });
    }
}
