using UnityEngine;

/// <summary>
/// 「换个佣兵上阵」三步箭头提示（2026-11-16 主人需求）：
/// ① 回到主界面（Guild 页）→ 气泡指底部「酒馆」按钮，告诉玩家可以去换人上阵；
/// ③ 玩家进了酒馆页 → 气泡指「佣兵队列」入口卡，告诉玩家点进去换人；
/// ② 玩家打开佣兵队列 → 气泡指那张还没上阵的佣兵卡，告诉玩家点「替换」。
/// 三条各只在自己那一页弹一次（主界面 / 酒馆页 / 队列弹窗），派发在 TownHubController。
///
/// 视觉全复用现成件（与 MercStarReadyHint 同口径）：底框 = BattleManager.DraftHintBubble
/// （「像素风空白对话框指针」，自带尖角）；载体 = TutorialHintUI 软引导
/// （SetBannerSpriteOnce + Show），不叠手指、不挡点击、到点自收。零新增 UI / 图片 / 预制体。
///
/// 触发不绑「获得那一瞬」，只认状态：队列已满 + 已获得里有人比队列里最弱的更强。
/// 队列没满时新佣兵会被 SaveData.EnsurePoolFilled 直接补进队列，这里自然不成立
/// （塔克 H003 稀有 对 老盾 H001 普通 正好命中）。
///
/// 三条各只弹一次，标记存 SaveData（随云存档一致、清档归零）；
/// 只有气泡真的 Show 出来才写标记（照 MercStarReadyHint / SkillOrderGuide 口径）。
/// </summary>
public static class MercSwapHint
{
    /// <summary>① 主界面指酒馆的文案（主人原话口径）。</summary>
    public const string TavernText = "可以去酒馆换佣兵上阵";
    /// <summary>① 气泡停留时长（秒）。</summary>
    const float TavernHoldSeconds = 5f;
    /// <summary>② 气泡停留时长（秒）：要够玩家看清卡、点开详情、再点「替换」。</summary>
    const float CardHoldSeconds = 6f;
    /// <summary>③ 酒馆页指「佣兵队列」入口卡的文案。</summary>
    public const string QueueText = "点这里进佣兵队列\n换人上阵";
    /// <summary>③ 气泡停留时长（秒）。</summary>
    const float QueueHoldSeconds = 5f;
    /// <summary>② 文案（2026-11-16 主人拍板缩短，配合 250 宽气泡）。</summary>
    const string CardText = "点这张卡\n替换上场";
    /// <summary>④ 佣兵详情弹窗里指「替换」钮的文案。</summary>
    public const string ReplaceText = "点这里替换上阵";
    /// <summary>④ 气泡停留时长。</summary>
    const float ReplaceHoldSeconds = 6f;
    /// <summary>
    /// 【2026-11-25 主人拍板「框体不能出屏所以歪了：框可出屏、字不要出屏也行」】
    /// ②③④ 三条各自缩窄 + BubbleAllowOffscreenKeepTextIn=true：
    /// 气泡本体允许贴边/出屏，尖角严格对齐目标中心；文字由 textShiftX 补偿留在屏内。
    /// 旧口径（400×192 + 夹取 ±152）会把右上/右下目标夹偏 → 主人报「歪」。
    /// ① 指底栏酒馆、常态在屏内，一个字不动；其余引导默认 400×192 也不动。
    /// ② 250 宽沿用 2026-11-16 拍板；高度 120→138：两行字不再顶到上边框。
    /// </summary>
    const float CardBubbleWidth = 250f;
    const float CardBubbleHeight = 138f;
    /// <summary>③ 酒馆页指「佣兵队列」入口卡（x≈+165）：340 宽夹取上限 182，目标不再被夹。</summary>
    const float QueueBubbleWidth = 340f;
    const float QueueBubbleHeight = 144f;
    /// <summary>④ 详情弹窗指右下「替换」钮：单行 7 字≈182 宽 + 左右 PadX 共 80 → 280 宽够且不空。</summary>
    const float ReplaceBubbleWidth = 280f;
    const float ReplaceBubbleHeight = 130f;
    /// <summary>
    /// 队列界面是 TownPopup(900)、获得佣兵详情弹窗是 925：
    /// 气泡默认 600 会被这些弹窗整个盖住，必须一次性抬到它们之上（Hide 自动还原默认层级）。
    /// </summary>
    const int SortOverPopup = GameConfig.UiSort.TownPopup + 40;

    /// <summary>
    /// 屏上是否开着「会盖住外层气泡」的弹窗（奖励窗 / 队列 / 登录 / 招募 / 获得演出）。
    /// 【2026-11-16 主人报「提示跑到展示界面/队列界面上」】①③ 是主界面/酒馆页的外层气泡，
    /// 弹窗开着时它们的目标节点被盖住、气泡会浮到弹窗上 —— 这时不弹、也不写标记，
    /// 等玩家关窗回到对应页再轮询弹。
    /// ⚠ 不用泛化 Canvas 扫描：MercDrawPoolUI 的 Canvas(sortingOrder=900) 挂在 DDOL 根 gameObject 上、
    /// 关窗只隐藏子 _root，根 Canvas 恒 active —— 泛化扫会恒判为「有弹窗」让 ①③ 永远不弹。
    /// 改用各弹窗自己的可见标志（IsOpen / activeInHierarchy / PopupVisible），可靠且零误判。
    /// ② 不调这里：它本就指队列弹窗内的卡（overPopup 抬层），只管 ①③。
    /// </summary>
    public static bool PopupOnScreen()
    {
        if (MercGainCinematicUI.IsShowing) return true;
        if (DailyLoginUI.PopupVisible) return true;
        if (RewardPopupUI.Instance != null && RewardPopupUI.Instance.gameObject.activeInHierarchy) return true;
        if (MercDrawPoolUI.Instance != null && MercDrawPoolUI.Instance.IsOpen) return true;
        var recruit = MercenaryRecruitPopupUI.Instance;
        if (recruit != null && recruit.IsOpen) return true;
        // 【2026-11-16 主人报「提示跑到邮件界面上」】邮件列表/详情是 DDOL 弹窗，
        // 盖在 Guild 页上时外层气泡①会浮到邮件窗上。两级都纳入占屏判定：
        // 开邮件窗 → DismissOuterOnPopup 收气泡并回滚标记，关窗回主界面再轮询弹。
        if (MailListUI.Instance != null && MailListUI.Instance.gameObject.activeInHierarchy) return true;
        if (MailDetailUI.Instance != null && MailDetailUI.Instance.gameObject.activeInHierarchy) return true;
        return false;
    }

    /// <summary>
    /// 有弹窗占屏时收掉正挂着的气泡，防「气泡浮在弹窗上」/「计时跑完标记写掉提示被吞」。
    /// rollback=false（队列/详情开窗，玩家已完成这一步）：只 Hide、标记保持 true，关窗后不再重弹；
    /// rollback=true （被奖励窗等意外打断）：Hide 并回滚标记，关窗回原页重新弹。
    /// 只认本类四条文案，不碰其它引导。
    /// </summary>
    public static void DismissOuterOnPopup(bool rollback)
    {
        var hint = TutorialHintUI.Instance;
        if (hint == null || !hint.IsVisible) return;
        string cur = hint.CurrentHint;
        bool isTavern = cur == TavernText;
        bool isQueue = cur == QueueText;
        bool isReplace = cur == ReplaceText;
        bool isCard = cur == CardText;
        // rollback=true 是 TownHubController 的占屏轮询：只收外层气泡①③（会浮到弹窗上）。
        // ②④ 本就指弹窗内的控件、理应在弹窗里显示，不能被占屏轮询误收（否则④弹出瞬间即被收+回滚）。
        if (rollback)
        {
            if (!isTavern && !isQueue) return;
        }
        else
        {
            // rollback=false 是明确开窗动作：队列开窗收③、详情开窗收②；也认④以防重复派发时先收旧的。
            if (!isTavern && !isQueue && !isReplace && !isCard) return;
        }
        if (rollback)
        {
            var saveSys = SaveSystem.Instance;
            var d = saveSys != null ? saveSys.Data : null;
            if (d != null)
            {
                if (isTavern) d.mercSwapHintTavernShown = false;
                else if (isQueue) d.mercSwapHintQueueShown = false;
                else if (isReplace) d.mercSwapHintReplaceShown = false;
                // isCard 永不回滚：玩家点了卡，第②条使命已完成
                saveSys.Save();
            }
        }
        hint.Hide();
        Debug.Log("[MercSwapHint] 弹窗占屏，气泡「" + cur + "」已收起（rollback=" + rollback + "）");
    }
    /// <summary>
    /// 【2026-11-16】详情弹窗关闭时收掉第④条「替换」气泡（替换/确认/暗带关窗都要收）：
    /// 气泡挂在弹窗的替换钮上，弹窗一关目标消失，气泡却会挂到 6s 自动收，期间浮在队列界面上。
    /// 只认 ReplaceText，不碰其它引导。
    /// </summary>
    public static void DismissReplaceIfVisible()
    {
        var hint = TutorialHintUI.Instance;
        if (hint == null || !hint.IsVisible) return;
        if (hint.CurrentHint != ReplaceText) return;
        hint.Hide();
        Debug.Log("[MercSwapHint] ④ 气泡随详情弹窗关闭已收起");
    }

    /// <summary>
    /// 【2026-11-16 主人补拍板】点开任意佣兵详情（含不带替换钮的已入队详情）时收掉第②条指卡气泡：
    /// 玩家已点开详情=第②条使命完成，标记不回滚；不收会浮到详情弹窗上（气泡抬层 940 > 详情 925）。
    /// 只认 CardText，其它三条引导零影响；与 DismissReplaceIfVisible 同口径。
    /// </summary>
    public static void DismissCardBubble()
    {
        var hint = TutorialHintUI.Instance;
        if (hint == null || !hint.IsVisible) return;
        if (hint.CurrentHint != CardText) return;
        hint.Hide();
        Debug.Log("[MercSwapHint] ② 气泡随佣兵详情打开已收起");
    }

    // ============================================================
    // ① 回主界面 → 指酒馆
    // ============================================================

    /// <summary>城镇轮询调用（TownHubController，与 MercStarReadyHint 同节流）。true = 本次真的弹出来了。
    /// 【2026-11-16 主人要求】只在主界面（Guild 页）弹，其它页由 TownHubController 不派发到这里。</summary>
    public static bool TryShowTavernIcon(RectTransform tavernIcon)
    {
        var saveSys = SaveSystem.Instance;
        var data = saveSys != null ? saveSys.Data : null;
        if (data == null || data.mercSwapHintTavernShown) return false;
        // 底栏收起/切页时这次不弹、也不记档，等下次轮询
        if (tavernIcon == null || !tavernIcon.gameObject.activeInHierarchy) return false;
        // 首次强制引导还占着屏幕时让位（与 TownIdleNudge 同口径）：引导收完自然还会轮询到这里
        var guide = FirstRunGuide.Instance;
        if (guide != null && (!guide.IsFinished || guide.IsOverlayVisible)) return false;
        // 【2026-11-16】弹窗占屏时不弹（奖励窗/队列/登录/设置……），关窗回原页再轮询弹
        if (PopupOnScreen()) return false;
        // 【2026-11-16 主人需求】「获得新佣兵要等到回主界面再弹」：获得佣兵的全屏展示界面还开着、
        // 或者酒馆招募弹窗 / 佣兵队列弹窗还没关，都不算回到主界面 —— 这次不弹、也不记档，等关掉后轮询再来。
        // （演出层 11500 会把 600 的气泡整个盖住，5 秒照跑完、标记照写，等于提示被吞掉。）
        if (MercGainCinematicUI.IsShowing) return false;
        var recruit = MercenaryRecruitPopupUI.Instance;
        if (recruit != null && recruit.IsOpen) return false;
        var pool = MercDrawPoolUI.Instance;
        if (pool != null && pool.IsOpen) return false;
        if (!PickSwapTarget(data, out _, out _)) return false;
        if (!TryShow(TavernText, tavernIcon, TavernHoldSeconds, overPopup: false)) return false;

        data.mercSwapHintTavernShown = true;
        saveSys.Save();
        Debug.Log("[MercSwapHint] ① 回主界面提示「" + TavernText + "」已弹出（酒馆图标上方箭头气泡）");
        return true;
    }

    // ============================================================
    // ② 佣兵队列 → 指没上阵的那张卡
    // ============================================================

    /// <summary>佣兵队列开窗后一帧调用（MercDrawPoolUI.Open）。卡不在屏上就这次不弹，下次开窗再试。</summary>
    public static bool TryShowRosterCard(MercDrawPoolUI pool)
    {
        var saveSys = SaveSystem.Instance;
        var data = saveSys != null ? saveSys.Data : null;
        if (data == null || data.mercSwapHintCardShown) return false;
        if (pool == null || !pool.IsOpen) return false;
        if (!PickSwapTarget(data, out string inHireId, out _)) return false;

        var cardRect = pool.GetOwnedCardRect(inHireId);
        if (cardRect == null) return false;

        string text = CardText;
        var layout = new TutorialHintUI.HintLayout
        {
            BubbleWidth = CardBubbleWidth,
            BubbleHeight = CardBubbleHeight,
            BubbleAllowOffscreenKeepTextIn = true,
        };
        if (!TryShow(text, cardRect, CardHoldSeconds, overPopup: true, layout)) return false;

        data.mercSwapHintCardShown = true;
        saveSys.Save();
        Debug.Log("[MercSwapHint] ② 队列提示「" + text + "」已弹出（指 " + inHireId + " 卡）");
        return true;
    }

    // ============================================================
    // ③ 酒馆页 → 指「佣兵队列」入口卡
    // ============================================================

    /// <summary>城镇轮询调用（TownHubController，只在酒馆页派发）。true = 本次真的弹出来了。</summary>
    public static bool TryShowTavernQueue(RectTransform queueCard)
    {
        var saveSys = SaveSystem.Instance;
        var data = saveSys != null ? saveSys.Data : null;
        if (data == null || data.mercSwapHintQueueShown) return false;
        // 酒馆页还没建好 / 不在前台时这次不弹、也不记档，等下一次轮询
        if (queueCard == null || !queueCard.gameObject.activeInHierarchy) return false;
        // 首次强制引导还占着屏幕时让位（与 ① 同口径）
        var guide = FirstRunGuide.Instance;
        if (guide != null && (!guide.IsFinished || guide.IsOverlayVisible)) return false;
        // 【2026-11-16】弹窗占屏时不弹，关窗回酒馆页再轮询弹
        if (PopupOnScreen()) return false;
        // 获得佣兵的展示界面 / 招募弹窗还开着：等关掉、真回到酒馆页再弹
        if (MercGainCinematicUI.IsShowing) return false;
        var recruit = MercenaryRecruitPopupUI.Instance;
        if (recruit != null && recruit.IsOpen) return false;
        if (!PickSwapTarget(data, out _, out _)) return false;
        var layout = new TutorialHintUI.HintLayout
        {
            BubbleWidth = QueueBubbleWidth,
            BubbleHeight = QueueBubbleHeight,
            BubbleAllowOffscreenKeepTextIn = true,
        };
        if (!TryShow(QueueText, queueCard, QueueHoldSeconds, overPopup: false, layout)) return false;

        data.mercSwapHintQueueShown = true;
        saveSys.Save();
        Debug.Log("[MercSwapHint] ③ 酒馆页提示「" + QueueText + "」已弹出（佣兵队列入口卡上方箭头气泡）");
        return true;
    }

    // ============================================================
    // ④ 佣兵详情弹窗 → 指「替换」钮
    // ============================================================

    /// <summary>详情弹窗开窗后一帧调用（MercObtainPopupUI.ShowFromEntry）。
    /// 只在带「替换」钮的详情（未入队已获得佣兵）弹；标记独立，账号全局一次。</summary>
    public static bool TryShowReplaceButton(MercObtainPopupUI ui)
    {
        var saveSys = SaveSystem.Instance;
        var data = saveSys != null ? saveSys.Data : null;
        if (data == null || data.mercSwapHintReplaceShown) return false;
        if (ui == null || !ui.IsOpen) return false;
        var btnRect = ui.ReplaceButtonRect;
        if (btnRect == null) return false;   // 不带替换钮的详情（已入队/未获得）：不弹
        var layout = new TutorialHintUI.HintLayout
        {
            BubbleWidth = ReplaceBubbleWidth,
            BubbleHeight = ReplaceBubbleHeight,
            BubbleAllowOffscreenKeepTextIn = true,
        };
        if (!TryShow(ReplaceText, btnRect, ReplaceHoldSeconds, overPopup: true, layout)) return false;
        data.mercSwapHintReplaceShown = true;
        saveSys.Save();
        Debug.Log("[MercSwapHint] ④ 详情提示「" + ReplaceText + "」已弹出（指替换钮）");
        return true;
    }

    // ============================================================
    // 判定 / 展示
    // ============================================================

    /// <summary>
    /// 找出「该换上谁 / 该换下谁」。返回 false = 此刻不该提示：
    /// 队列没满（新佣兵会被自动补进队列，不需要换人）、已获得里没有更强的、或队列里查不到人。
    /// </summary>
    static bool PickSwapTarget(SaveData data, out string inHireId, out string outHireId)
    {
        inHireId = null;
        outHireId = null;
        if (data == null) return false;

        var queue = data.mercDrawPool;
        var unlocked = data.unlockedMercIds;
        if (queue == null || unlocked == null || queue.Count == 0) return false;
        if (queue.Count < data.DrawPoolSlots) return false;   // 还有空位：直接入队，不用换人

        // 队列里最弱的（稀有度最低；同档取队首）
        int weakestRank = int.MaxValue;
        for (int i = 0; i < queue.Count; i++)
        {
            if (!MercRosterDefs.TryGetByHireId(queue[i], out var d)) continue;
            int rank = (int)d.Rarity;
            if (rank < weakestRank)
            {
                weakestRank = rank;
                outHireId = queue[i];
            }
        }
        if (outHireId == null) return false;

        // 已获得、没上阵、可上阵的里面最强的（剧情 NPC 不进队列，与列表同口径）
        int bestRank = -1;
        foreach (string id in unlocked)
        {
            if (string.IsNullOrEmpty(id) || queue.Contains(id)) continue;
            if (!MercRosterDefs.TryGetByHireId(id, out var d)) continue;
            if (GameConfig.GetMercTier(d.AssetId) == MercTier.Npc) continue;
            int rank = (int)d.Rarity;
            if (rank > bestRank)
            {
                bestRank = rank;
                inHireId = id;
            }
        }
        if (inHireId == null || bestRank <= weakestRank)
        {
            inHireId = null;
            outHireId = null;
            return false;
        }
        return true;
    }

    /// <summary>统一展示出口：一次性气泡底框 + 一次性抬层，没真弹出来就把两个一次性设置退掉。
    /// layout 非空 = 本条单独覆盖排版（目前只有 ② 用来把气泡缩到 250 宽指卡）。</summary>
    static bool TryShow(string text, RectTransform target, float holdSeconds, bool overPopup,
        TutorialHintUI.HintLayout? layout = null)
    {
        // 别的引导正占着这条横幅：这次不弹、不记标记，等下次轮询
        var existing = TutorialHintUI.Instance;
        if (existing != null && existing.IsVisible) return false;

        var bubble = BattleManager.DraftHintBubble;
        if (bubble == null)
        {
            Debug.LogError("[MercSwapHint] 取不到底框 Sprite：UI/Art/Common/像素风空白对话框指针");
            return false;
        }

        var hint = TutorialHintUI.Ensure();
        if (hint == null) return false;
        hint.SetBannerSpriteOnce(bubble);
        if (overPopup) hint.SetSortOrderOnce(SortOverPopup);
        hint.Show(text, target, holdSeconds, false, layout);

        // Show 内部遇到战斗头顶台词会直接 return：只有真显示出来才算数
        if (!hint.IsVisible || hint.CurrentHint != text)
        {
            hint.SetBannerSpriteOnce(null);   // 没弹出就退掉一次性设置，防污染下一次引导
            hint.SetSortOrderOnce(0);
            return false;
        }
        return true;
    }
}
