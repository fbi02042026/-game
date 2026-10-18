using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 战斗编排器 partial —— 2026-10-17 由 BattleManager.cs 纯位移拆出（手法 B，无逻辑改动）
public partial class BattleManager
{
    #region 死亡与撤离
    public void OnHeroDead()
    {
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
        // 教程也走结算界面，关闭后再回城接剧情
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
        GridBackpackSystem.Instance?.ClearRunEquipment();
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
        BattleSettlementUI.Show(RunStats, () =>
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
        GridBackpackSystem.Instance?.ClearRunEquipment();
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
        GridBackpackSystem.Instance?.ClearRunEquipment();

        PersistBattleGold();
        // 金币保留 → 天赋石照发（死亡与撤离同规则）
        int talentGain = (int)(Mathf.Max(0, currentGold - _goldAtRunStart) / GameConfig.GOLD_PER_TALENT_POINT);
        if (talentGain > 0)
            ResourceWallet.Add(ResourceWallet.ResourceType.TalentPoint, talentGain, save: false, notify: false);

        // 【2026-10-17 主人拍板】撤离补偿（替代已停掉的精英事件强化石）：
        // 强化石现已不再掉落，撤离按本局已通关关数每关 +1 天赋石，中途撤离不空手。
        // 只在主动撤离（isDeath=false）发；死亡不发；引导局不走本方法（另有固定通关金币）。
        // 并入 talentGain，让结算界面「天赋石 +N」包含这笔；落盘由下方 Save() 统一做。
        if (!isDeath && _runStagesCleared > 0)
        {
            ResourceWallet.Add(ResourceWallet.ResourceType.TalentPoint, _runStagesCleared, save: false, notify: true);
            Debug.Log($"[BattleManager] 撤离补偿：本局已通关 {_runStagesCleared} 关 → 天赋石 +{_runStagesCleared}");
            talentGain += _runStagesCleared;
        }

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
        RunStats.EquipCount = GridBackpackSystem.Instance != null
            ? GridBackpackSystem.Instance.GetAllItemsForLegacy().Count
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

    #endregion
}
