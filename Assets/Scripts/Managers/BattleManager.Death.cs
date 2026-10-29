using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 战斗编排器 partial —— 2026-10-17 由 BattleManager.cs 纯位移拆出（手法 B，无逻辑改动）
public partial class BattleManager
{
    #region 死亡与撤离

    /// <summary>
    /// 【2026-10-24 主人拍板「复活先接上，用钻石复活」】本关是否已用过复活（每关一次，
    /// 关卡开始时随 _battleStartTime 一起重置）。等待复活选择期间本局处于「冻场待抉择」状态。
    /// </summary>
    bool _reviveUsedThisStage;

    /// <summary>复活选择窗是否开着（2026-10-25 广告版守卫）：广告是异步回调，
    /// 玩家可能先点【返回】收窗、之后回调才回来 —— 回调凭本标志判定战斗还在待抉择，防诈尸复活。</summary>
    bool _reviveChoiceOpen;

    /// <summary>复活广告拉起中（防连点重复拉起两次广告）。</summary>
    bool _reviveAdPending;

    /// <summary>是否提供复活选择：非引导局、本关没用过、主角在场、今日复活额度未用完（ReviveQuota，ADS_ENABLED=false 期间恒有）。
    /// 【2026-10-29 主人拍板】改走 <see cref="IBattleMode.OffersRevive"/>（与 UsesBattleResultScreen 解耦）——
    /// 无限裂隙通关不播结果屏，但死亡同样弹复活窗（含看广告档）；正式关/副本行为不变。
    /// 引导局照旧「阵亡重进」。</summary>
    bool CanOfferRevive => Mode != null && Mode.OffersRevive
        && !Rules.Active && !SkipLegacyOnEvacuate
        && hero != null && !_reviveUsedThisStage
        && ReviveQuota.CanOfferToday();

    public void OnHeroDead()
    {
        // 【2026-10-24 主人拍板】正式关阵亡先弹「失败 + 复活/返回」选择界面：冻场待抉择，
        // 点【复活】按当日档位（广告/50钻/100钻）原地半血复活继续打，点【返回】才走原收尾（遗产结算）。
        // 【2026-10-29 改版】无限裂隙**也要**这道门（Mode.OffersRevive=true，与结算屏解耦）；引导局照旧跳过。
        if (CanOfferRevive)
        {
            OfferHeroRevive();
            return;
        }
        FinalizeHeroDeath();
    }

    /// <summary>
    /// 复活选择窗：停出兵 + 冻场（与天雷演出/冻场同机制），弹带双钮的失败界面。
    /// 此时<b>不收局</b>（不清佣兵、不 EndRunLoadout、不记死亡埋点）—— 玩家复活后战斗原样继续。
    /// 【2026-10-25 14:47 主人拍板】复活档位 = 每日 2 广告 + 50 钻 + 100 钻，之后只能返回
    /// （CanOfferRevive 已挡 Capped，不会走到这里）；ADS_ENABLED=false 期间维持 10 钻现状。
    /// </summary>
    void OfferHeroRevive()
    {
        _reviveUsedThisStage = true;
        isInBattle = false;      // 停出兵/推进（与原死亡口径一致）
        UnitsCanAct = false;     // 冻场：怪/佣兵/协程全停，复活后还原
        _reviveChoiceOpen = true;
        var kind = ReviveQuota.ResolveKind(out int price);
        // 广告档按钮文案换「看广告复活」（钻石 icon 为主人摆的不动，观感差异已报主人）；
        // 钻石档沿用「{价格}复活」格式。
        string label = kind == ReviveClaimKind.Ad ? "看广告复活" : null;
        BattleResultScreen.ShowFailureChoice(
            revivePrice: price,
            onRevive: TryReviveHero,
            onGiveUp: FinalizeHeroDeath,
            reviveLabel: label);
    }

    /// <summary>
    /// 复活扣费唯一入口（2026-10-25 14:47 主人拍板）：按 ReviveQuota 档位分流。
    /// 返回 false = 本次点击未立即复活（钻石不足 / 广告拉起中 / 广告档异步等待），界面停留。
    /// 广告档：RewardedAdBridge.ShowRewarded("revive") 异步回调，核验通过（IsVerified）才
    /// ForceCloseChoice + DoReviveHero；回调竞态由 _reviveChoiceOpen / _reviveAdPending 双守卫挡住。
    /// </summary>
    bool TryReviveHero()
    {
        var kind = ReviveQuota.ResolveKind(out int price);
        switch (kind)
        {
            case ReviveClaimKind.Ad:
                // 【2026-10-28 远程急停】维护中直接提示，界面停留（可点【返回】走死亡收尾），不消耗档位
                if (AdKillSwitch.AdsOff)
                {
                    UIManager.Instance?.ShowToast("广告维护中，请稍后再试");
                    return false;
                }
                if (_reviveAdPending) return false;   // 广告已在拉起，忽略重复点击
                _reviveAdPending = true;
                UIManager.Instance?.ShowToast("广告加载中…");   // 【2026-10-28】拉起到展示有等待期，先给反馈防狂点
                RewardedAdBridge.ShowRewarded("revive", ok =>
                {
                    _reviveAdPending = false;
                    if (!ok)
                    {
                        UIManager.Instance?.ShowToast("激励未完成，复活未生效");
                        return;
                    }
                    if (!_reviveChoiceOpen) return;   // 玩家已点【返回】/界面已收，不许诈尸复活
                    _reviveChoiceOpen = false;
                    ReviveQuota.RegisterClaimed();
                    BattleResultScreen.ForceCloseChoice();
                    DoReviveHero();
                });
                return false;   // 异步：窗暂不收（模拟桥同步回调时上面已收，走到这里也无害）

            case ReviveClaimKind.Diamond:
                // 【2026-10-28 主人规则重申「只要扣钻石的地方都要有二次确认」】
                // 钻石复活不再「点复活钮直接扣」：先弹 DiamondSpendGate 通用确认门
                // （余额不足 Toast 缺口不弹窗；充足弹双按钮确认框，正文带花费行），
                // 玩家点「确认」才扣钻复活。本分支恒 return false = 选择窗停留（与广告档异步同型），
                // 确认回调里走 ForceCloseChoice 收窗。
                DiamondSpendGate.Request("战斗复活", "用钻石原地复活，继续本关。", price, () =>
                {
                    if (!_reviveChoiceOpen) return;   // 确认期间玩家已点【返回】/界面已收，不许诈尸复活
                    if (!ResourceWallet.TrySpendDiamond(price, "战斗复活", save: true, notify: false))
                    {
                        UIManager.Instance?.ShowToast("钻石不足，复活未生效");
                        return;
                    }
                    _reviveChoiceOpen = false;
                    ReviveQuota.RegisterClaimed();
                    BattleResultScreen.ForceCloseChoice();
                    DoReviveHero();
                });
                return false;

            default:   // Capped：CanOfferRevive 已挡，理论上到不了；fail-closed 当放弃处理
                Debug.LogError("[BattleManager] 复活档位异常（Capped 却弹了选择窗），按放弃处理");
                _reviveChoiceOpen = false;
                return false;
        }
    }

    /// <summary>复活本体：半血原地起立 + 解冻 + 恢复战斗。血量比例走 GameConfig.REVIVE_HP_RATIO。</summary>
    void DoReviveHero()
    {
        if (hero == null)
        {
            Debug.LogError("[BattleManager] 复活失败：hero 为空，按放弃处理走死亡收尾");
            FinalizeHeroDeath();
            return;
        }
        if (!hero.Revive(GameConfig.REVIVE_HP_RATIO))
            Debug.LogWarning("[BattleManager] 主角未处于死亡态却收到复活指令（重复点击？），已忽略扣费后状态");
        UnitsCanAct = true;
        isInBattle = true;
        BattleUI.Instance?.ForceRefreshLiveBars();
    }

    /// <summary>
    /// 死亡收尾（原 OnHeroDead 主体，玩家点【返回】或不提供复活时走这里）。
    /// 从「进门即收局」拆出来：复活窗口期间这些一律不执行。
    /// </summary>
    void FinalizeHeroDeath()
    {
        _reviveChoiceOpen = false;  // 选择窗已收（放弃复活）——广告异步回调凭本标志不再生效
        UnitsCanAct = true;  // 还原冻场，后续结算/协程照旧
        Planner?.NotifyStageResult(true, 0f); // V3.0 压力阀：死过 → 下一关减一波
        isInBattle = false;
        _runClosed = true;   // 局已收尾：挂起的抽奖等待协程当场退场（见字段注释）
        Analytics.StageEnd(CurrentChapter, currentStage != null ? currentStage.stageIndex : 0, false,
            (double)(Time.unscaledTime - _battleStartTime)); // 埋点：关卡结束（阵亡/失败）
        MercenaryManager.Instance?.ClearAllMercs();
        AdventureLogAchievements.OnDied();
        EndRunLoadout();
        // 引导关死亡：不走遗产，也不标记教程通关 —— 重进引导战斗（2026-09-27 主人拍板）
        if (Rules.Active || SkipLegacyOnEvacuate)
        {
            RestartTutorialBattle();
            return;
        }
        // 【2026-10-24 主人拍板】正式关卡/支线岛/活动副本阵亡先播「失败」结果界面，
        // 淡出（或点击跳过）后再进遗产/撤离结算；无限裂隙不播（每层累计奖励走自己的结算链），直接进遗产结算。
        // 引导局在上面 Rules.Active 分支已重进战斗，永远到不了这里。
        if (Mode != null && Mode.UsesBattleResultScreen)
            BattleResultScreen.Show(false, null, () => TriggerLegacyFlow(isDeath: true));   // 失败无奖励格，stats 传 null
        else
            TriggerLegacyFlow(isDeath: true);
    }

    public void TriggerEvacuation()
    {
        TryGrantStageQuestGoldIfQuestComplete();
        isInBattle = false;
        _runClosed = true;   // 【2026-10-18 主人报「撤离结算下面还能点抽奖」】局已收尾：
                             // 抽奖面板常驻期间撤离，挂起的抽奖等待协程靠本标志当场退场
        Analytics.StageEnd(CurrentChapter, currentStage != null ? currentStage.stageIndex : 0, false,
            (double)(Time.unscaledTime - _battleStartTime)); // 埋点：关卡结束（主动撤离）
        ClearAllMonsters();
        MercenaryManager.Instance?.ClearAllMercs();
        // 主动撤离 = 本局结束：构筑作废，但金币/天赋石已入账
        EndRunLoadout();
        if (!Rules.SkipMercHireClearOnEvacuate)
            MercHireSession.ClearHired();
        AdventureLogAchievements.OnEvacuated(Rules.Active);
        // 撤离回城后打开冒险页（引导局另有收尾，不抢页签）
        if (!Rules.SkipPendingAdventureOnEvacuate)
            TownHubController.PendingOpenAdventure = true;
        // 教程收尾也走「战斗结果」界面（2026-10-28 主人拍板），关闭后再回城接剧情
        if (SkipLegacyOnEvacuate || Rules.Active)
        {
            TriggerTutorialEvacuateWithSettlement();
            return;
        }
        TriggerLegacyFlow(isDeath: false);
    }

    /// <summary>引导撤离：弹出结算 → 再回城（TownAfterBattle 剧情）。</summary>
    void TriggerTutorialEvacuateWithSettlement()
    {
        if (TutorialDirector.Instance != null)
            TutorialDirector.Instance.WaitingEvacuate = false;
        if (!_stageQuestGoldGranted && StageQuestClearGold > 0)
            TryGrantStageQuestGold();

        // 结算前先快照；局内装备仍清（裂缝口径）
        BattleBagSystem.Instance?.ClearRunEquipment();
        // 【2026-10-18 主人反馈「结算金币和局内获得的不一致」】引导通关金币（SlotMachineDefs.TUTORIAL_SETTLEMENT_GOLD
        // = 150）原来绕过账本直接塞城镇钱包，结算却把显示值硬抬到 150 —— 局内顶栏 / 结算 / 城镇实收三个数对不上。
        // 现改为：进账唯一出口 GrantRunGold 并入「本局获得金币」，再随 PersistBattleGold 一起写回，
        // 顶栏、结算、城镇三处同一个数。
        if (SlotMachineDefs.TUTORIAL_SETTLEMENT_GOLD > 0)
        {
            GrantRunGold(SlotMachineDefs.TUTORIAL_SETTLEMENT_GOLD);
            Debug.Log($"[BattleManager] 引导通关金币 +{SlotMachineDefs.TUTORIAL_SETTLEMENT_GOLD} 并入本局获得账");
        }
        PersistBattleGold();
        // 2026-09-19：天赋石不再由战斗内金币换算产出，改由冒险日志里程碑（首次/整章通关）发放。
        int talentGain = 0;
        StoryProgress.MarkTutorialBattleCleared();
        BattleStateSaver.Instance?.ClearBattleState();
        SaveSystem.Instance?.Save();

        FillSettlementSnapshot(isDeath: false, talentGain);
        AdventureLogAchievements.OnRunGoldPeak(RunStats.GoldEarned);   // 2026-10-18 双钱包拆分：峰值按「本局获得金币」记
        // 【2026-10-28 16:10 主人拍板「引导关结束也要战斗结果界面，不要战斗-结算」】
        // 引导撤离/引导通关收尾改播「战斗结果」界面（胜利大图 + 奖励格，奖励数据源 = RunStats 快照），
        // 点【确定】/点任意处 → 回城接剧情（链路原样）。结果界面引导局不建分享钮（BattleResultScreen 内已分流）。
        BattleResultScreen.Show(true, RunStats, () =>
        {
            GameSceneManager.Instance?.LoadTownScene();
        });
    }

    /// <summary>
    /// 【2026-10-15 主人拍板】引导结尾传送门出口：「所有打完一关后都出现传送门，进传送门的步骤」照旧 ——
    /// 玩家先走、佣兵隔一个身位跟着进，每人进门各播一次传送特效（见 <see cref="TickPortalWalkIn"/>）；
    /// 全员进完（或超时）经 <see cref="FinishStageAfterPortalReached"/> 的引导分支接撤离结算 → 回城。
    /// 门开不出来也不锁死玩家：LogError 后直接走撤离结算。
    /// </summary>
    public void BeginTutorialPortalExit()
    {
        if (_tutorialPortalPending) return;
        _tutorialPortalPending = true;
        var gate = PortalGate.Open(BattleUI.Instance != null ? BattleUI.Instance.transform : null);
        if (gate != null)
        {
            NotifyChuanSongMenOpened(gate);
            return;
        }
        Debug.LogError("[BattleManager] 引导结尾传送门没开出来（PortalGate.Open 返回空，详见其上一条报错），直接走撤离结算");
        _tutorialPortalPending = false;
        TriggerTutorialEvacuateWithSettlement();
    }

    /// <summary>
    /// 引导局阵亡收尾：重新进入引导战斗（重头走引导步骤）。
    /// 2026-09-27 主人拍板：旧实现是「MarkTutorialBattleCleared + 回城」，
    /// 等于把引导里的一次阵亡当成通关 —— 回主界面就直接加载引导后续剧情。
    /// 现在只重进战斗场景（TutorialBattleCleared 没写 → ShouldStartTutorialBattle 仍为 true，
    /// 重新加载后还是引导局），金币照常写回，通关奖励不再发。
    /// </summary>
    void RestartTutorialBattle()
    {
        if (TutorialDirector.Instance != null)
            TutorialDirector.Instance.WaitingEvacuate = false;
        BattleBagSystem.Instance?.ClearRunEquipment();
        // 【2026-10-18 双钱包拆分】currentGold 只是抽奖币池（带不出）；写回走 PersistBattleGold
        // 的「获得金币 − 已付水位」增量，不存在整额 Add 路径
        PersistBattleGold();
        BattleStateSaver.Instance?.ClearBattleState();
        SaveSystem.Instance?.Save();

        var gsm = GameSceneManager.Instance;
        if (gsm == null)
        {
            Debug.LogError("[BattleManager] 引导局阵亡后重进战斗失败：GameSceneManager 为空");
            return;
        }
        Debug.LogWarning("[BattleManager] 引导局阵亡：重新进入引导战斗");
        gsm.LoadBattleScene();
    }

    /// <summary>
    /// 死亡 / 撤离结算：构筑（装备/技能/佣兵）一律清空；
    /// 金币与材料/附魔石等资源一律保留 —— 死亡也保留本局赚到的金币，并按金币产出天赋石。
    /// </summary>
    void TriggerLegacyFlow(bool isDeath)
    {
        // 【2026-10-18 彩蛋批次】连败安慰计数：非引导局死亡 +1 / 主动撤离清零；
        // 连败≥3 回城后由前台小姐姐说句安慰（TownHubController.CoDefeatComfort 消费）
        if (isDeath) TavernEggState.RegisterDeath();
        else TavernEggState.RegisterEvacuate();

        // 裂缝口径：局内装备/武器退出清空，不带出城镇
        BattleBagSystem.Instance?.ClearRunEquipment();

        PersistBattleGold();
        // 金币保留 → 天赋石照发（死亡与撤离同规则）
        int talentGain = (int)(Mathf.Max(0, currentGold - _goldAtRunStart) / GameConfig.GOLD_PER_TALENT_POINT);
        if (talentGain > 0)
            ResourceWallet.Add(ResourceWallet.ResourceType.TalentPoint, talentGain, save: false, notify: false);

        // 【2026-10-20 主人拍板】撤离补偿（按已通关数每关 +1 天赋石）停发：天赋石改为精英/Boss 击杀概率掉落。
        // if (!isDeath && _runStagesCleared > 0)
        // {
        //     ResourceWallet.Add(ResourceWallet.ResourceType.TalentPoint, _runStagesCleared, save: false, notify: true);
        //     Debug.Log($"[BattleManager] 撤离补偿：本局已通关 {_runStagesCleared} 关 → 天赋石 +{_runStagesCleared}");
        //     talentGain += _runStagesCleared;
        // }

        SaveSystem.Instance?.Save();
        BattleStateSaver.Instance?.ClearBattleState();

        FillSettlementSnapshot(isDeath, talentGain);
        if (!isDeath)
            AdventureLogAchievements.OnRunGoldPeak(RunStats.GoldEarned);   // 2026-10-18 双钱包拆分：峰值按「本局获得金币」记
        TownHubController.PendingOpenAdventure = true;
        BattleSettlementUI.Show(RunStats, () =>
        {
            MercHireSession.ClearHired();
            GameSceneManager.Instance?.LoadTownScene();
        });
    }

    void FillSettlementSnapshot(bool isDeath, int talentGain, bool isVictory = false)
    {
        RunStats.IsDeath = isDeath;
        RunStats.IsVictory = isVictory && !isDeath;
        // 【2026-10-18 主人反馈「打完一关没有给金币」】结算「金币」改毛收入口径（GoldEarned）：
        // 任务金/宝箱/首通/装备折金/拆分币/通关金全记账，不再被抽奖花费抵成 0（净额口径废弃）。
        // 死亡也照常显示本局赚到的钱。
        RunStats.GoldGained = RunStats.GoldEarned;
        RunStats.TalentGained = talentGain;
        RunStats.EquipCount = BattleBagSystem.Instance != null
            ? BattleBagSystem.Instance.GetAllItemsForLegacy().Count
            : 0;
        var data = SaveSystem.Instance?.Data;
        if (data != null)
        {
            RunStats.EnchantStoneDelta = Mathf.Max(0, data.enchantStones - _enchantAtRunStart);
            RunStats.DecomposeMatDelta = Mathf.Max(0, data.decomposeMats - _matsAtRunStart);
            RunStats.Chapter = ChapterManager.Instance != null
                ? ChapterManager.Instance.currentChapter
                : CurrentChapter;
        }
        if (currentStage != null)
            RunStats.StageTitle = ChapterManager.Instance != null
                ? ChapterManager.Instance.GetCurrentStageDisplayName()
                : GameConfig.GetChapterMapName(RunStats.Chapter);
        else
            RunStats.StageTitle = GameConfig.GetChapterMapName(RunStats.Chapter);
        RunStats.ResolveMvp();

        // 2026-10-13：数据埋点（TapTap 数据分析）。全部取自上面已算好的结算快照，不额外采集。
        ReportRunEndAnalytics();
    }

    /// <summary>把结算快照转为一条 #run_end 埋点。失败不阻塞结算流程。</summary>
    void ReportRunEndAnalytics()
    {
        try
        {
            GameAnalytics.ReportRunEnd(
                isDeath: RunStats.IsDeath,
                isVictory: RunStats.IsVictory,
                chapter: RunStats.Chapter,
                stageId: currentStage != null ? currentStage.stageIndex : 0,
                goldGained: RunStats.GoldGained,
                talentGained: RunStats.TalentGained,
                equipCount: RunStats.EquipCount,
                stoneDelta: RunStats.EnchantStoneDelta,
                decomposeDelta: RunStats.DecomposeMatDelta,
                mvp: RunStats.MvpKey,
                durationSec: RunStats.BattleTimeSec);
            GameAnalytics.AddRunCount();
            if (RunStats.IsDeath) GameAnalytics.AddDeathCount();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[BattleManager] #run_end 埋点失败：{e.Message}");
        }
    }

    /// <summary>通关后弹结算，再执行后续选关/回城。</summary>
    public void ShowVictorySettlementThen(System.Action afterConfirm)
    {
        // 金币已在通关时 PersistBattleGold 写回；天赋石改由冒险日志里程碑发放，此处不再按金币补发。
        int talentGain = 0;
        FillSettlementSnapshot(isDeath: false, talentGain, isVictory: true);
        AdventureLogAchievements.OnRunGoldPeak(RunStats.GoldEarned);   // 2026-10-18 双钱包拆分：峰值按「本局获得金币」记
        BattleSettlementUI.Show(RunStats, afterConfirm);
    }

    /// <summary>
    /// 【2026-10-22 主人拍板】v4 通关结算双钮变体：确认钮 =【下一关】（onNextStage，如 GoChapterGate 直进），
    /// 返回钮（运行时克隆）= afterConfirm（回城 EndRunAndReturnToTown 原行为）。
    /// onNextStage 传 null（跨章候选空）→ 不克隆返回钮、确认钮文案回「确定」，等同单确认钮口径。
    /// 死亡/撤离路径仍走上面单参重载，行为完全不变。
    /// </summary>
    public void ShowVictorySettlementThen(System.Action afterConfirm, System.Action onNextStage, string confirmLabel = "下一关")
    {
        int talentGain = 0;
        FillSettlementSnapshot(isDeath: false, talentGain, isVictory: true);
        AdventureLogAchievements.OnRunGoldPeak(RunStats.GoldEarned);
        BattleSettlementUI.Show(RunStats, afterConfirm, onNextStage, confirmLabel);
    }

    #endregion
}
