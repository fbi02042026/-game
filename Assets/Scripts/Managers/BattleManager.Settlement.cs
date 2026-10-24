using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 战斗编排器 partial —— 2026-10-17 由 BattleManager.cs 纯位移拆出（手法 B，无逻辑改动）
public partial class BattleManager
{
    #region 通关结算
    public void OnStageClear()
    {
        // 兼容旧调用：直接走完整结算选关（无宝箱时）
        Planner?.ClearStageEventState(); // 事件层：清场，避免移速/金币倍率泄漏到下一关
        FinishStageAfterPortalReached();
    }

    void EnsureRewardDirector()
    {
        if (StageClearRewardDirector.Instance != null) return;
        var go = new GameObject("StageClearRewardDirector");
        DontDestroyOnLoad(go);
        go.AddComponent<StageClearRewardDirector>();
    }

    void TryGrantStageQuestGoldIfQuestComplete()
    {
        if (_stageQuestGoldGranted || StageQuestClearGold <= 0) return;
        int goal = GetStageMonsterGoal();
        int defeated = Mathf.Clamp(_defeatedMonsterKills, 0, goal);
        if (defeated >= goal)
            TryGrantStageQuestGold();
    }

    /// <summary>
    /// 「本局获得金币」进账唯一出口（只加不减，抽奖花不到它）：
    /// 任务金 / 宝箱 / 装备折金 / 按关结算金……全部记进 <see cref="BattleRunStats.GoldEarned"/>，
    /// 顶部 HUD 显示的就是它，死亡 / 撤离 / 通关经 <see cref="PersistBattleGold"/> 按增量写回城镇
    /// —— 顶部看到多少，落袋就是多少。
    /// 【2026-10-18 主人拍板「顶部的是玩家本局获得的金币」】不再进 <c>currentGold</c>：
    /// 那是抽奖币池（启动金 / 通关补贴 / 抽中金币，见 <see cref="AddRunGold"/>），带不出本局。
    /// </summary>
    public void GrantRunGold(long amount)
    {
        if (amount <= 0) return;
        RunStats.GoldEarned += amount;
        BattleUI.Instance?.UpdateGold(RunEarnedGold);
    }

    void TryGrantStageQuestGold()
    {
        if (_stageQuestGoldGranted || StageQuestClearGold <= 0) return;
        int grant = Mathf.RoundToInt(StageQuestClearGold * GoldGainMul);
        if (grant <= 0) return;
        _stageQuestGoldGranted = true;
        GrantRunGold(grant);   // 2026-10-19 主人拍板：现在没有任务功能，「获得任务奖励」toast 不弹（钱照进「本局获得金币」账）
        // #region agent log
        DebugAgentLog.Log("H9", "BattleManager.TryGrantStageQuestGold", "quest_gold_granted",
            $"{{\"grant\":{grant},\"currentGold\":{currentGold}}}");
        // #endregion
    }

    /// <summary>清怪后：宝箱 → 掉落 → 三选一 → chuansongmen</summary>
    void StartStageClearRewardSequence()
    {
        if (_rewardSequenceStarted || _stageCleared) return;
        if (CountAliveMonsters() > 0) return;
        Planner?.GrantStageEventRewards(); // 事件层：发放精英小队累计的强化石
        TryGrantStageQuestGold();
        GrantStageClues();
        GrantStageGrowDrops();
        if (ShouldPlayChapter1Ending())
        {
            _rewardSequenceStarted = true;
            StartCoroutine(CoChapter1EndingThenRewards());
            return;
        }
        if (ShouldPlayChapterStory())
        {
            _rewardSequenceStarted = true;
            StartCoroutine(CoChapterStoryThenRewards());
            return;
        }
        BeginRewardSequence();
    }

    /// <summary>叙事 V2.0：按关卡进度发放冒险日志线索页（幂等，重复发放无副作用）。</summary>
    void GrantStageClues()
    {
        if (currentStage == null) return;
        int idx = ChapterManager.Instance != null ? ChapterManager.Instance.currentStageIndex : 0;
        StoryClue.OnStageCleared(CurrentChapter, idx);
        if (currentStage.type == StageType.Elite)
            StoryClue.OnEliteFirstKill(CurrentChapter);
    }

    /// <summary>
    /// 2026-09-18：关卡掉落徽记（stage_drop.csv，见 StageDropTable / MercGrowInventory）。
    /// Boss 关且该章通关次数 ≤1 视为「本章首次通关」，额外给 §7.4 的保底（+2 自选徽记）。
    /// 用 ≤1 而不是 ==0：IncrementChapterClearCount 的时序若在清怪前，==0 会漏掉保底。
    /// </summary>
    void GrantStageGrowDrops()
    {
        if (currentStage == null) return;
        // —— 徽记副本（2026-10-23 拍板「活动副本一拆二」）：通关只发随机职业徽记，走自己的发放链 ——
        // 顺序口径：徽记副本发徽记 → 金币本禁掉落 → 正常主线掉落，三路各写一行不合并。
        if (IsBadgeDungeon)
        {
            MercGrowInventory.GrantBadgeDungeonDrops(BattleDifficulty);   // 返回 0（存档未就绪）时函数内已不弹 Toast
            return;
        }
        // —— 金币本禁掉落（2026-10-23 拍板「金币本只掉金币」）：stage_drop 徽记/碎片/道具/Boss 卷轴
        //    全在同一方法里，一个 early return 全禁；金币走 GetGoldDungeonClearGold 固定金币，与此无关 ——
        if (IsGoldDungeon) return;
        // —— 支线岛（2026-10-23 合并方案）：不发 stage_drop 徽记/碎片/卷轴 ——
        //    岛产出 = 固定金币 + 天赋石（岛结算链 SideIslandBattleMode.SettleIslandRewards）
        //    + 装备（宝箱链 DropsEquipment=true），三路各走各的
        if (IsSideIsland) return;
        string stageType = currentStage.type == StageType.Boss ? "Boss"
                         : currentStage.type == StageType.Elite ? "Elite" : "Normal";
        bool firstClear = currentStage.type == StageType.Boss
            && (ChapterManager.Instance == null || ChapterManager.Instance.GetChapterClearCount(CurrentChapter) <= 1);
        // 2026-09-26 主人拍板：按本关物理/魔法击杀占比推导主导类型，透传给掉落分池（平局=不限制）。
        string dropBias = null;
        if (_magicKills > _physicalKills) dropBias = "magic";
        else if (_physicalKills > _magicKills) dropBias = "physical";
        int got = MercGrowInventory.GrantStageDrops(CurrentChapter, stageType, firstClear, dropBias);
        if (got > 0)
            GlobalToastUI.Show("获得养成掉落 ×" + got, true);   // 2026-10-06 主人拍板：物品/掉落获得，force 弹出

        // —— Boss 掉落卷轴（2026-10-17 主人拍板）：普通卷轴几率掉，高级招募卷轴几率很低 ——
        if (currentStage.type == StageType.Boss)
        {
            var data = SaveSystem.Instance?.Data;
            if (data != null)
            {
                string scrollMsg = null;
                if (Random.value < GameConfig.BOSS_SCROLL_DROP_RATE)
                {
                    data.mercScrollCommon++;
                    scrollMsg = "普通卷轴×1";
                }
                if (Random.value < GameConfig.BOSS_ADV_SCROLL_DROP_RATE)
                {
                    data.mercScrollLegendary++;
                    scrollMsg = scrollMsg == null ? "高级招募卷轴×1" : scrollMsg + "、高级招募卷轴×1";
                }
                if (scrollMsg != null)
                {
                    GlobalToastUI.Show("Boss 掉落 " + scrollMsg, true);
                    SaveSystem.Instance?.Save();
                }
            }
        }
    }

    bool ShouldPlayChapter1Ending()
    {
        if (IsEndlessRift) return false;   // 无限裂隙不播剧情（2026-10-18 主人拍板）：Boss 记录点层直接发奖励
        if (Rules.SkipChapter1Ending) return false;
        if (!StoryProgress.TutorialDone || StoryProgress.Chapter1ChoiceDone) return false;
        if (CurrentChapter > 1) return false;
        return currentStage != null && currentStage.type == StageType.Boss;
    }

    IEnumerator CoChapter1EndingThenRewards()
    {
        bool done = false;
        // 【2026-10-19 防卡死】播剧情前把抽奖面板强制收起（幂等）：协程被中途杀掉时面板会停留展开态，
        // 4 个按钮正好压在剧情文本上把点击全吃掉（timeScale=0 → 全场定格）。三处剧情入口同理。
        BattleEntryDraftPanel.Hide();
        // 【2026-10-19 防卡死】与战前/战后剧情入口同款：剧情启动若有异常，绝不能让本协程死掉——
        // 下面 300 秒保险和 BeginRewardSequence 都在这条协程里，协程一死奖励流程永远不来。
        bool playing = false;
        try
        {
            Chapter1Story.PlayEnding(() => done = true);
            playing = true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[BattleManager] 第 1 章结局剧情播放异常，直接放行奖励流程: " + e.Message);
            // 剧情可能已开播（timeScale 已被压 0、对话窗停在半句）：先强制收掉播放态再放行，
            // 否则奖励流程虽然跑了，全场仍定格在 timeScale=0。
            StoryDirector.Ensure().StopPlaying();
            var dlg = DialogueUI.Instance;
            if (dlg != null) dlg.Hide();
            playing = false;
        }
        if (!playing)
        {
            _rewardSequenceStarted = false;
            BeginRewardSequence();
            yield break;
        }
        float storyWatchdog = 0f;
        while (!done)
        {
            storyWatchdog += Time.unscaledDeltaTime;
            if (storyWatchdog >= 300f)
            {
                Debug.LogWarning("[BattleManager] 第 1 章结局剧情回调 5 分钟未完成，强制放行奖励流程");
                break;
            }
            yield return null;
        }
        _rewardSequenceStarted = false;
        BeginRewardSequence();
    }

    /// <summary>叙事 V2.0：第 2–8 章 Boss 后的关键对话节点（含第 7 章转折点与第 8 章结局）。</summary>
    bool ShouldPlayChapterStory()
    {
        if (IsEndlessRift) return false;   // 无限裂隙不播剧情（2026-10-18 主人拍板）：记录点层只弹「撤离/继续」双选
        if (IsFinale) return false;        // 终章追查关不播战后章节剧情（2026-10-23）：act=8+Boss 型会误触 N8 重播，关后剧情走 FinaleBattleMode 自己的链
        if (Rules.SkipChapter1Ending) return false;
        if (CurrentChapter < 2) return false;
        if (!StoryProgress.TutorialDone || !StoryProgress.Chapter1ChoiceDone) return false;
        if (currentStage == null || currentStage.type != StageType.Boss) return false;
        // 第 7 章的转折点是不可逆的：做过了就不再重播
        if (CurrentChapter == 7 && ChapterStoryBeats.EndingChoiceDone) return false;
        return true;
    }

    IEnumerator CoChapterStoryThenRewards()
    {
        bool done = false;
        // 【2026-10-19 防卡死】播剧情前把抽奖面板强制收起（幂等）：这是第 2 章「剧情被 4 个抽奖按钮
        // 盖住点不动 → 全场定格」的治本补丁之一（层级 1500 是另一条腿）。协程被杀时面板会停留展开态。
        BattleEntryDraftPanel.Hide();
        // 【2026-10-19 防卡死】与战前剧情入口同款：剧情启动若有异常，绝不能让本协程死掉——
        // 下面 300 秒保险和 BeginRewardSequence 都在这条协程里，协程一死奖励流程永远不来，
        // 玩家看到的就是「打死 Boss 出完剧情回到战斗场景，没宝箱没传送门卡死」。
        bool playing = false;
        try
        {
            playing = ChapterStoryBeats.TryPlayPostBoss(CurrentChapter, () => done = true);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[BattleManager] 战后剧情播放异常，直接放行奖励流程: " + e.Message);
            // 剧情可能已开播（timeScale 已被压 0、对话窗停在半句）：先强制收掉播放态再放行，
            // 否则奖励流程虽然跑了，全场仍定格在 timeScale=0。
            StoryDirector.Ensure().StopPlaying();
            var dlg = DialogueUI.Instance;
            if (dlg != null) dlg.Hide();
            playing = false;
        }
        if (!playing)
        {
            _rewardSequenceStarted = false;
            BeginRewardSequence();
            yield break;
        }
        float storyWatchdog = 0f;
        while (!done)
        {
            storyWatchdog += Time.unscaledDeltaTime;
            if (storyWatchdog >= 300f)
            {
                Debug.LogWarning("[BattleManager] 战后剧情回调 5 分钟未完成，强制放行奖励流程");
                break;
            }
            yield return null;
        }
        _rewardSequenceStarted = false;
        BeginRewardSequence();
    }

    /// <summary>
    /// 两段式（2026-09-19 定调）：进关后、首波怪物出现前播战前剧情，与战后 TryPlayPostBoss 对称。
    /// 只在每章第一关（currentStageIndex == 0）播；玩家点「跳过」时 StoryDirector 直接走 onDone 放行。
    /// </summary>
    bool ShouldPlayPreBattleStory()
    {
        if (IsEndlessRift) return false;   // 无限裂隙不播剧情（2026-10-18 主人拍板）：进层直接开抽奖
        if (IsFinale) return false;        // 终章追查关不播战前章节剧情（2026-10-23）：act=8+首关会误触第 8 章战前剧情重播
        if (Rules.SkipChapter1Ending) return false;
        if (Rules.UseTutorialSplash) return false;   // 新手教学关有自己的引导节奏
        if (Rules.SkipFirstWaveAuto) return false;   // 教程关刷怪归 TutorialDirector
        if (CurrentChapter < 1 || CurrentChapter > 8) return false;
        if (!StoryProgress.TutorialDone || !StoryProgress.Chapter1ChoiceDone) return false;
        // 【2026-10-19 主人拍板】战前剧情看过即不重播：每章播过一次（开播即记）后进关直接开打，
        // 治「每次进都卡剧情、玩不了只能退出重来、体力白扣」。
        if (StoryProgress.PreStorySeen(CurrentChapter)) return false;
        var cm = ChapterManager.Instance;
        if (cm == null || cm.currentStageIndex != 0) return false;
        return true;
    }

    IEnumerator CoPreBattleStoryThenFirstWave()
    {
        bool done = false;
        bool playing = false;
        // 【2026-10-19 防卡死】进关先把本章剧情立绘/背景同步读进缓存（几张图，毫秒级）：
        // 播到一半才首次加载会卡一下；缺图时 StoryPortraits/StoryBackgrounds 自带占位图降级 + 告警，不会黑屏中断。
        ChapterStoryBeats.WarmupChapterAssets(CurrentChapter);
        // 【2026-10-19 防卡死】剧情开播前把抽奖面板强制收起（幂等）：Prewarm 失败时面板会停在
        // 预制体展开态（4 个黑金按钮），剧情一播就被盖住、点击全被吃 → 全场定格（第 2 章事故根因）。
        BattleEntryDraftPanel.Hide();
        // 剧情播放定义在别处，任何异常都不能把战斗卡在“无首波”状态：捕住异常并直接放行。
        try
        {
            if (ShouldPlayPreBattleStory())
            {
                playing = ChapterStoryBeats.TryPlayPreBattle(CurrentChapter, () => done = true);
                // 开播即标记「看过」（不是播完）：中途退出也算，之后进关不再重播同一段开场
                if (playing) StoryProgress.MarkPreStorySeen(CurrentChapter);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[BattleManager] 战前剧情播放异常，直接放行首波: " + e.Message);
            playing = false;
        }

        if (!playing)
        {
            // 没有剧情：直接进抽奖。抽奖期间保持 _preBattleStoryPlaying=true，
            // 否则 CoFirstWaveHardFallback 会在 2.2s 后硬刷怪，玩家还在选卡就被打。
            //
            // 【2026-10-08 主人拍板】引导局**开局不抽奖** —— 先让玩家用初始武器打完第一波，
            // 清场后由 TutorialDirector 弹第一抽（BattleRoutine 里 1c 那一拍）。
            // 这样「抽到装备换上 → 下一波明显变强」才看得出来；正式关照常进关抽奖。
            _preBattleStoryPlaying = true;
            if (!IsTutorialRun) yield return CoStageEntryDraft();
            _preBattleStoryPlaying = false;
            ScheduleFirstWaveSpawn();
            yield break;
        }

        _preBattleStoryPlaying = true;
        float storyWatchdog = 0f;
        while (!done)
        {
            storyWatchdog += Time.unscaledDeltaTime;
            if (storyWatchdog >= 300f)
            {
                Debug.LogWarning("[BattleManager] 战前剧情回调 5 分钟未完成，强制放行首波流程");
                break;
            }
            yield return null;
        }
        // 剧情结束（含跳过）后进抽奖，期间继续压住硬刷兜底
        // 同上：引导局跳过进关抽奖，第一抽留给第一波清场之后。
        if (!IsTutorialRun) yield return CoStageEntryDraft();
        _preBattleStoryPlaying = false;
        // 抽完才放首波
        ScheduleFirstWaveSpawn();
    }

    /// <summary>
    /// 进关抽奖（2026-09-29 第二版）：面板在开战前**常驻**，四个按钮（随机 / 佣兵 / 装备 / 技能）。
    ///
    /// · 点四个按钮之一 = **买**：扣抽奖币，立刻出 1 张结果并生效、弹提示，**不弹三选一**。
    ///   玩家买的是「结果」不是「选择」，所以一秒都不等（抽奖就图个快）。
    /// · 「再来一次」触发 = **送**：免费老虎机走个形式 → 弹该类型的三选一，玩家在这里才做选择。
    /// · 只要抽奖币够就能一直抽；点「开始战斗」或超时才关面板开打。
    ///
    /// 🔴 战斗一开始必须完全隐藏抽奖面板 —— 战斗画面里只留战斗，不留任何抽奖入口。
    /// 这里在循环结束后统一调 <see cref="BattleEntryDraftPanel.Hide"/> 兜底，不管从哪条路退出。
    ///
    /// 引导关（Rules.GuideEntryDraft）开局会圈住「随机抽奖」按钮强制引导一次，抽完收起提示，
    /// 之后按 TutorialDirector 的既有节拍一步一步走。
    /// 由 <see cref="CoPreBattleStoryThenFirstWave"/> 在「剧情播完之后、首波之前」调用。
    /// </summary>

    /// <summary>
    /// 最近一抽「到底获得了什么」的文案（<see cref="CoInstantPick"/> 发奖成功后写入）。
    ///
    /// <para>为什么要单独存一份：<c>CoInstantPick</c> 是协程、拿不到返回值，
    /// 而 <see cref="WaitForContinue"/> 的硬引导文案必须<b>带上这件东西的名字</b> ——
    /// 只弹一句「奖励到手了」玩家根本不知道自己拿到了啥（2026-10-06 主人反馈）。</para>
    /// <para>为什么不能只靠 Toast：Toast 会被硬引导遮罩盖住 / 一闪而过，等于没提示。</para>
    /// </summary>
    string _lastDrawRewardMsg;

    /// <summary>
    /// 【2026-10-18 主人拍板】无限裂隙进场的「免费抽 N 次」提示，每局只弹一次。
    /// 续层不走这段（免费额度是每局一次，续层不再送），StartNewRunInternal 里清零。
    /// </summary>
    bool _endlessEntryHintShown;

    /// <summary>
    /// 【2026-10-06 主人拍板】抽完奖励<b>不许自动继续</b> —— 引导局 / 正式关一律等玩家点「继续」。
    ///
    /// <para>· <paramref name="lockDraw"/> = true：这一拍不能再抽（引导局一次一抽、中途抽一次一抽）。<br/>
    /// · <paramref name="guideText"/> = null：不弹硬引导（正式关玩家自己知道点「继续」）。<br/>
    /// · 面板上跑 <c>BattleEntryDraftPanel.ContinueCountdownSec</c> 秒倒计时，
    ///   归零自动当作玩家点了继续 —— 只做「挂着不动」的兜底，不是第二条流程。</para>
    /// </summary>
    /// <summary>
    /// 「等玩家点继续」那句引导的<b>前半段</b> —— 必须是「这一抽拿到了什么」，
    /// 不能只是「奖励到手了」（2026-10-06 主人反馈）。
    /// <para>拿不到记录说明发奖链路没走到，按铁律 14 打 Error 报出来，不静默糊一句。</para>
    /// </summary>
    string RewardTip()
    {
        if (string.IsNullOrEmpty(_lastDrawRewardMsg))
        {
            Debug.LogError("[BattleManager] 要弹「奖励到手」却没记下获得了什么 —— 玩家看不到奖励内容");
            return "奖励到手了";
        }
        return _lastDrawRewardMsg;
    }

    IEnumerator WaitForContinue(bool lockDraw, string guideText, System.Func<bool> isDone)
    {
        var panel = BattleEntryDraftPanel.Instance;
        if (panel == null) yield break;      // 面板都没了就没得等，直接放玩家走（绝不卡死）

        if (lockDraw) panel.SetDrawLocked(true);
        if (!string.IsNullOrEmpty(guideText))
        {
            // 【2026-10-13 修「引导三拍中途抽，点继续卡死」】
            // 引导局倒计时被冻住、只靠玩家点「继续」推进；若硬遮罩与「继续」按钮存在坐标偏移，
            // 按钮会被暗带盖住、点击被吞 → 永久卡（只能干等 300s 兜底）。
            // 引导局的「继续」引导改用<b>软引导</b>（不挡点击）：手指照常指向「继续」，
            // 但按钮始终可直接点，杜绝卡死。正式关仍走硬遮罩 + 倒计时自动继续（不受影响）。
            if (IsTutorialRun)
                TutorialHintUI.Ensure().Show(guideText, panel.ContinueButtonRect, -1f);
            else
                TutorialHintUI.Ensure().ShowHard(guideText, panel.ContinueButtonRect);
        }
        // 【2026-10-06 主人二次拍板】这里<b>不再起表</b> —— 倒计时早在面板弹出（Begin）那一刻就开走了，
        // 抽奖/结算期间被冻住，到这里只是<b>解冻接着走剩下的秒数</b>。
        // 原写法在这里 StartContinueCountdown() = 抽完又把 30s 补满，玩家看到的正是
        // 「怎么还是抽完才开始计时」。
        //
        // 【2026-10-08 主人拍板】引导局**全程不催**：倒计时保持冻住，玩家想看多久看多久，
        // 只有他自己点「继续」才往下走。正式关照常解冻、接着走剩下的秒数（30s 总预算是正式关的玩法）。
        if (!IsTutorialRun)
            panel.SetCountdownPaused(false);

        // 300s 只是异常兜底（倒计时本身会先归零走 DoContinue）
        // 【2026-10-18】局已收尾（撤离/死亡）→ 当场退场，不再等（面板收起后 choice 永远不会变）
        float guard = 0f;
        while (!isDone() && guard < 300f && !_runClosed)
        {
            guard += Time.unscaledDeltaTime;
            yield return null;
        }
        TutorialHintUI.Instance?.Hide();
    }

    // ===== 关卡商店（2026-10-21 主人需求：临时界面，后续正式化再出美术/预制体）=====
    // 时机 = 每关进关抽奖之前（引导关不弹）。商品：重生护符 50% 占一格，其余出随机武器/装备。
    // 货币 = 战斗内抽奖币池 currentGold（玩家口中的「抽奖卷」，扣款走 SpendBattleGold 唯一出口）。
    // 要调价格 / 概率，改下面三个常量即可。
    const int StageShopCharmPrice = 100;
    const int StageShopEquipPrice = 80;
    const float StageShopCharmChance = 0.5f;

    static readonly EquipSlotType[] AllEquipSlots =
    {
        EquipSlotType.Head, EquipSlotType.Chest, EquipSlotType.Hands, EquipSlotType.Feet,
        EquipSlotType.Cape, EquipSlotType.MainHand, EquipSlotType.OffHand
    };

    IEnumerator CoStageShop()
    {
        if (IsTutorialRun) yield break;   // 引导关不打扰（与进关抽奖同口径）
        var items = BuildStageShopItems();
        if (items == null || items.Count == 0) yield break;

        bool closed = false;
        StageShopPopupUI.Show(items, (int)currentGold, () => closed = true);   // currentGold 是 long（BattleManager.State.cs:10），币池规模远小于 int 上限，此处显式收窄

        // 防卡死口径与进关抽奖一致：300s 上限；撤离/局收尾（_runClosed）随时放行
        float guard = 0f;
        while (!closed && guard < 300f && !_runClosed)
        {
            guard += Time.unscaledDeltaTime;
            yield return null;
        }
        if (!closed) StageShopPopupUI.Hide();   // 超时/退场强收（Close 内会触发 closed 回调）
    }

    List<StageShopPopupUI.ShopItem> BuildStageShopItems()
    {
        var list = new List<StageShopPopupUI.ShopItem>();

        // ① 重生护符：50% 概率上架，占一格
        bool charm = Random.value < StageShopCharmChance;
        if (charm)
        {
            list.Add(new StageShopPopupUI.ShopItem
            {
                title = "重生护符",
                info = "佣兵阵亡时，点击其变灰的头像消耗 1 枚原地满血复活。",
                icon = ItemDefs.LoadIcon("item_revive_charm"),
                price = StageShopCharmPrice,
                rarity = null,
                onBuy = () =>
                {
                    var d = SaveSystem.Instance?.Data;
                    if (d == null) return false;
                    if (!SpendBattleGold(StageShopCharmPrice)) return false;
                    d.reviveCharms++;
                    SaveSystem.Instance?.Save();                 // 买了就落盘，别白嫖
                    BattleUI.Instance?.UpdateCharacterSlots();   // 已有阵亡槽的话，护符闪烁立刻补上
                    return true;
                }
            });
        }

        // ② 随机武器/装备：占满剩余格子，稀有度保底 ≥ 玩家当前已穿最高一档（「比玩家当前的装备好」）
        int wantCount = 3 - (charm ? 1 : 0);
        var equips = RollStageShopEquips(wantCount);
        foreach (var eq in equips)
        {
            var captured = eq;
            list.Add(new StageShopPopupUI.ShopItem
            {
                title = EquipUiText.EquipTitleWithHand(captured) ?? "装备",
                info = "买下立即穿上，替换下来的旧件自动折成强化石。",
                icon = ResolveStageShopEquipIcon(captured),
                price = StageShopEquipPrice,
                rarity = captured.rarity,
                onBuy = () =>
                {
                    var bag = GridBackpackSystem.Instance;
                    if (bag == null) return false;
                    if (!SpendBattleGold(StageShopEquipPrice)) return false;
                    if (!bag.TryEquipDirect(captured))
                    {
                        // 穿不上退币，不能白扣
                        currentGold += StageShopEquipPrice;
                        BattleEntryDraftPanel.NotifyPoolGoldChanged(currentGold);
                        UIManager.Instance?.ShowToast("这件穿不上，已退款。");
                        return false;
                    }
                    return true;
                }
            });
        }
        return list;
    }

    /// <summary>
    /// 生成商店装备：多 roll 几件候选，优先稀有度<b>高于玩家当前已穿最高档</b>的，
    /// 部位尽量不重复；凑不齐才放宽同槽补位。
    /// </summary>
    List<EquipInstance> RollStageShopEquips(int count)
    {
        var result = new List<EquipInstance>();
        if (count <= 0) return result;

        Rarity bestWorn = Rarity.Common;
        var bag = GridBackpackSystem.Instance;
        if (bag != null)
        {
            for (int i = 0; i < AllEquipSlots.Length; i++)
            {
                var w = bag.GetEquippedInSlot(AllEquipSlots[i]);
                if (w != null && w.rarity > bestWorn) bestWorn = w.rarity;
            }
        }

        PlayerJobId job = PlayerJobDefs.GetSelected();
        int chapter = ChapterManager.Instance != null ? ChapterManager.Instance.currentChapter : 1;
        var candidates = new List<EquipInstance>();
        for (int i = 0; i < 10 && candidates.Count < count + 4; i++)
        {
            var eq = RiftEquipGenerator.GenerateOne(StageType.Normal, job, chapter: chapter);
            if (eq != null) candidates.Add(eq);
        }

        // 稀有度降序，先挑「比当前好」的、部位不重复的
        candidates.Sort((a, b) => b.rarity.CompareTo(a.rarity));
        var usedSlots = new HashSet<EquipSlotType>();
        foreach (var c in candidates)
        {
            if (result.Count >= count) break;
            if (usedSlots.Contains(c.slotType)) continue;
            usedSlots.Add(c.slotType);
            result.Add(c);
        }
        foreach (var c in candidates)   // 不够就放宽同槽补齐
        {
            if (result.Count >= count) break;
            if (!result.Contains(c)) result.Add(c);
        }
        return result;
    }

    static Sprite ResolveStageShopEquipIcon(EquipInstance eq)
    {
        if (eq == null) return null;
        EquipIcons.Resolve(eq);
        return eq.icon;
    }

    IEnumerator CoStageEntryDraft()
    {
        // 【2026-10-21 主人拍板：关卡商店暂时注释，等想好再开。】
        // 重新开启时口径已改：**不是每关都弹，是随机出现**（掷概率，概率主人定）——
        // 实现（CoStageShop / BuildStageShopItems / RollStageShopEquips / StageShopPopupUI）都在，取消下面注释即可接入。
        //yield return CoStageShop();

        // ⚠ 启动抽奖币**不在这里补**（2026-10-05 主人拍板：「开始金币是每局的，不是每关的」）。
        // 原来挂在这里 = 每关进关都补一次，成了「每关地板」，通关产出的取舍就失效了。
        // 现在挪到 RunDraftDirector.OnRunStart，只有开新局（resumed=false）那一关才补。
        var cats = SlotMachineSystem.AvailableCategories();
        if (cats.Count == 0)
        {
            // 2026-09-30：不静默跳过 —— 池子全空就等于面板压根不出现，控制台必须能看到原因
            Debug.LogError("[BattleManager] 抽奖池全空（技能 / 佣兵 / 装备都取不到内容），进关抽奖不弹出");
            yield break;
        }

        // 【2026-10-05 主人拍板「不要摇杆了」→ 整条链路停用，先注释不删】
        // BattleJoystick.Instance?.SetVisible(false);   // 抽奖期间收起摇杆
        // 【2026-10-10 主人拍板】价跟**本局已抽数**走（2026-10-23 起 40 起、每抽 +10），不再跟关卡走 ——
        // 与 CoMidBattleDraft 同一出口（SlotMachineSystem.DrawPrice），别再写第二套算法。
        // ⚠ 价必须**每轮重算**，见循环内（2026-10-11 修「连抽价格不涨」）。

        // 2026-10-05 主人拍板：金币本开局免费抽 N 次（额度写在模式上）。进关清零，一关一算。
        // ⚠ 这行已于 2026-10-07 上移到 CoStartBattle 的「引导 / 正式」分叉之前（唯一入口，见那里）；
        //    引导关不再进 CoStageEntryDraft，额度仍要清，别在这里再加一次。

        // 引导局：这一拍只抽一次就开打（唯一判定见 OneDrawPerBeat）
        bool onceOnly = OneDrawPerBeat;
        // 引导局这一拍「抽了但没生效」的重试次数（见下方 break 处说明）
        int beatRetry = 0;
        // 引导硬提示只弹一次（重试那一圈不再重复弹遮罩）
        bool guideHintShown = false;

        // 【2026-10-18 主人拍板】无限裂隙进场引导：告诉玩家本次进场免费抽几次（每局只提示一次）。
        // 抽奖面板弹出时 Toast 一句，玩家低头就能对上面板上「免费 ×N」的按钮。
        if (IsEndlessRift && !_endlessEntryHintShown)
        {
            _endlessEntryHintShown = true;
            int freeTotal = SlotMachineSystem.TotalFreeDraws();
            if (freeTotal > 0)
                GlobalToastUI.Show($"本次进场免费抽 {freeTotal} 次，抽完再开打！", true);
        }

        while (true)
        {
            DraftCategory? picked = null;
            int choice = 0;
            // 【2026-10-11 修】价必须**每轮重算**：每抽一次本局抽数 +1，下一抽贵 10（2026-10-23 拍板；
            // 原 10-10 口径是 +10% 复利，主人原话「每抽一次奖增加下一次抽奖花费的金币 每次+10%」）。
            // 旧写法把 price / focusPrice 算在 while 外面，面板连抽 N 次都还是同一个价 ——
            // 主人反馈「抽了一次奖后 花费没有增长」就是这个。
            int price = SlotMachineSystem.DrawPrice();
            int focusPrice = SlotMachineSystem.DrawFocusPrice();
            // 还有免费额度 → 随机抽 0 币；【2026-10-19 主人拍板】定向不吃免费，面板永远显示原价
            int freeLeft = SlotMachineSystem.FreeDrawsLeft();
            int showPrice = freeLeft > 0 ? 0 : price;
            int showFocus = focusPrice;
            // 2026-09-30 主人改版：抽奖按钮长在战斗 HUD 底部的 BackpackPanel 里
            //（BackpackPanel 收起 600 / 展开 750），不再弹居中面板 SlotOfferUI。
            BattleEntryDraftPanel.Show(showPrice, showFocus, cats, currentGold,
                c => { picked = c; choice = 1; },
                () => choice = 2, freeLeft);

            // 【防卡死兜底】面板构建失败（缺节点 / 缺图）时不会展开 —— 玩家看到的就是
            // 「画面冻住、卡牌没加载出来」，只能退游戏白扣体力（2026-10-18 玩家反馈）。
            // Begin 内 SetExpanded 是同步置位，健康路径当帧就开；3 秒还没开就不再冻场干等 300 秒，
            // 报错 + 提示后直接开战（与 Show 自身「拿不到面板就直接开战」同一口径）。
            float openGuard = 0f;
            while (!BattleEntryDraftPanel.IsLotteryOpen && openGuard < 3f)
            {
                openGuard += Time.unscaledDeltaTime;
                yield return null;
            }
            if (!BattleEntryDraftPanel.IsLotteryOpen)
            {
                Debug.LogError("[BattleManager] 进关抽奖面板 3 秒内没弹出来（构建失败），跳过抽奖直接开战，不再冻场");
                UIManager.Instance?.ShowToast("抽奖面板没加载出来，已跳过这次抽奖");
                break;
            }

            // 同上（CoMidBattleDraft 那份）：引导局不催，面板一弹出就冻住倒计时。
            // 现在引导局已跳过进关抽奖，这行是给「以后引导又走这条路」兜的同一道保险。
            if (IsTutorialRun)
                BattleEntryDraftPanel.Instance?.SetCountdownPaused(true);

            // 引导关：开局先教玩家抽奖，圈住「随机抽奖」按钮
            if (onceOnly && !guideHintShown)
            {
                guideHintShown = true;
                var rect = BattleEntryDraftPanel.Instance != null
                    ? BattleEntryDraftPanel.Instance.NormalButtonRect : null;
                // 【2026-10-08 主人拍板】原文案「先抽一次奖，开局白拿一个强化。」作废 ——
                //   主人原话「引导文字不要说开局白拿一个抽奖」：引导现在也不再从开局白送，
                //   第一抽是**打完第一波才给**的（见 TutorialDirector 1c 那一拍）。
                //   文案只说「抽一次」，不提白拿、不剧透类别。
                TutorialHintUI.Ensure().ShowHard("先抽一次奖，看看能开出什么。", rect);
            }

            // 面板常驻，给足看构筑的时间；不点就一直不开战（上限 5 分钟防卡死）
            // 【2026-10-18 主人报「撤离结算下面还能点抽奖」】面板常驻期间玩家可从暂停菜单撤离，
            // isInBattle 此时还是 false（首波没刷，不能当退场判据）—— 用 _runClosed 当场退场：
            // 不再等满 300s，也不再走后面的扣币/出货/开战流程。
        float offerGuard = 0f;
        while (choice == 0 && offerGuard < 300f && !_runClosed)
        {
            offerGuard += Time.unscaledDeltaTime;
            yield return null;
        }
        if (choice != 1) break;

        // 【2026-10-06 主人二次拍板】玩家一按抽奖就<b>冻结</b>倒计时：接下来是扣币 / 老虎机 /
        // 三选一 / 换装备弹窗这一串结算，这段时间不该催玩家点「继续」。
        // ⚠ 冻的是<b>剩余秒数</b>，不重新起表 —— 结算完接着走剩下的，绝不给它偷偷补满。
        if (BattleEntryDraftPanel.Instance != null)
            BattleEntryDraftPanel.Instance.SetCountdownPaused(true);

        // 每一轮都重算一次可抽类型：上一抽可能把技能槽 / 佣兵位占满，
        // 用旧列表会随机到一个已经发不出去的类型（2026-10-05）。
        cats = SlotMachineSystem.AvailableCategories();

            // null = 点的是随机；有值 = 点的是定向
            var target = picked.HasValue ? picked.Value : SlotMachineSystem.ResolveCategory(cats);
            // 定向价按类型各自的币种算（佣兵 = 佣兵币价，装备 / 技能 = 抽奖币价）。
            // 【2026-10-19 主人拍板】免费次数只惠及随机抽（null）；定向永远按价扣币，
            // 也不消耗免费额度（cost>0 走 SpendBattleGold，免费额度只在 cost==0 时才被吃）。
            int cost = !picked.HasValue && freeLeft > 0 ? 0
                     : (picked.HasValue ? SlotMachineSystem.FocusPrice(picked.Value) : price);
            // 只在这里「查余额」；真正的扣款挪进 CoInstantPick，等结果确认能生效了才扣
            // （2026-10-05：原来先扣后出结果，槽位/背包满时会白扣钱）
            // 免费抽 cost = 0，余额检查自然通过，也不会扣币。币种口径见 CanAffordDraw。
            if (!CanAffordDraw(picked, cost, out string shortBy))
            {
                UIManager.Instance?.ShowToast(shortBy + "不足");
                // 【2026-10-20 修·主人报「抽奖金币不足 直接就开始了 不让选择了」】
                // 旧写法这里 break = 没收面板直接开战，玩家连「继续」都点不着。
                // 改成回面板重来一轮：按钮按真实余额置灰（红字「还差 N」），
                // 玩家自己点「继续」才开打。这不是成功抽取的那条 10-12 固定预算链路
                //（没扣币、定序没动），面板重开补一次倒计时是失败重试，不算违反「不补满」。
                continue;
            }

            // 【2026-10-12 主人拍板】「再来一次」= **抽奖结果之一**，不再是抽完之后的额外赠送。
            // 主人原话：「为什么抽中了技能还出现了再抽一次　再抽一次应该也算是抽中的东西」。
            // 于是这一抽**先掷**「再来一次」：
            //   · 命中 → 本次结果就是它：不出佣兵 / 技能 / 装备，**不扣币、不推进抽数**，
            //     只送一次免费抽，回面板让玩家用这次免费额度再点（按钮金额当场变「免费」）；
            //   · 未命中 → 才真的出类型奖品（往下走 CoInstantPick）。
            // ⚠ 引导局不参与（那一拍只抽一次），判定沿用同一处 onceOnly，别在别处加第二个。
            // ⚠ 旧的「抽完再掷、额外再送一次免费抽」（2026-10-11 口径）已删除，不留开关。
            // ⚠【2026-10-19 主人拍板】「再来一次」只惠及随机抽（picked 为 null）；
            //   三个定向（佣兵/装备/技能）不吃 reroll，点了直接出类型奖品、正常扣费。
            if (!onceOnly && !picked.HasValue && SlotMachineSystem.RollReroll())
            {
                SlotMachineSystem.GrantBonusFreeDraw();
                var rerollPanel = BattleEntryDraftPanel.Instance;
                if (rerollPanel != null)
                {
                    rerollPanel.SetCountdownPaused(true);
                    // 按钮金额**当场**变成免费（不等面板下一轮重开），主人要求的就是这个瞬间的反馈
                    rerollPanel.RefreshFreeLeft(currentGold, SlotMachineSystem.FreeDrawsLeft());
                }
                UIManager.Instance?.ShowToast("再来一次！这一抽免费");
                // 【2026-10-06 主人拍板】左侧滑入 → 慢慢滑行 1 秒 → 快速向右滑走的大字横幅
                SlotMachineAgainBanner.Show();
                // 等横幅播完（0.30 滑入 + 1.00 滑行 + 0.22 滑出 ≈ 1.52s）再回面板
                yield return new WaitForSecondsRealtime(1.55f);
                if (rerollPanel != null) rerollPanel.SetCountdownPaused(false);
                continue;   // 这一抽的结果就是「再来一次」—— 回面板，让玩家点这次免费的
            }

            // 买了就直接给结果，不走三选一。
            // 「这一抽到底生效没有」唯一真源 = 保底定序有没有往前走（生效才 NoteDraw）。
            int drawsBefore = SlotMachineSystem.DrawIndex;
            yield return CoInstantPick(target, cost);
            bool landed = SlotMachineSystem.DrawIndex > drawsBefore;

            // 引导局：这一拍**只抽一次**就开打 —— 不进「再来一次」、也不再回到面板。
            // 面板常驻连抽是正式关的玩法；引导这里若让玩家一口气把三次抽完，
            // 后面「打完两波抽佣兵 / 再两波抽技能」两拍就吃不到保底定序了（序号是本局累计的）。
            //
            // ⚠ 唯一例外：这一抽**没生效**（池空 / 技能槽满 / 佣兵位满 / 玩家不肯换装备）时定序不推进，
            //   放它过去 = 引导三拍会缺一件（缺的往往是最后那件本命技能，引导直接断在半路）。
            //   → 让它再点一次；最多重试 1 次，池子真出不了卡时不把玩家卡在面板里。
            // ⚠ 「只抽一次」的判定只用 OneDrawPerBeat 一处，别在别处加第二个。
            // 【2026-10-06 主人拍板】引导局：只有「这一抽没生效」才放它再点一次（最多重试 1 次，
            // 否则引导三拍会缺一件）；抽到了就往下走，进「等玩家点继续」。
            if (onceOnly && !landed && ++beatRetry <= 1)
                continue;

            if (onceOnly)
            {
                // 引导局 = 一次一抽：锁住抽奖按钮（只剩「继续」能点），引导指向「继续」，
                // 等玩家点继续 / 倒计时归零。
                yield return WaitForContinue(true, $"{RewardTip()}　点「继续」开打。", () => choice == 2);
                break;
            }

            // 正式关：**连抽仍然是玩法** —— 抽完回到面板，玩家想再抽就再抽；
            // 不想抽、或者币不够（按钮会自动置灰）时，点「继续」就继续战斗。
            // ⚠ 不是「每抽一次就得点一次继续」：这里不锁按钮、不 break。
            //
            // ⚠【2026-10-12 纠正一处「记反了」的归因，主人当天又报了一次】
            // 旧注释写的是「2026-10-11 主人拍板：抽一次重置一次计时」，但它**自己引用的主人原话**
            // 是「时间是固定的 不是抽一次重置一次计时」—— 引文和结论正好相反，是当时的误记，
            // 而且主人 2026-10-12 明确又强调了一遍（原话「不是说过倒计时不要抽一次重置一次吗」）。
            // 按归因纪律纠正：主人从头到尾要的都是 **总预算固定 = 面板弹出时那 30s**。
            // → 这一抽结算完只**解冻**（接着走剩下的秒数），**绝不补满重来**。
            //   与 2026-10-06 那份口径一致，不存在「以 10-11 为准」这回事，别再改回 StartContinueCountdown。
            var entryPanel = BattleEntryDraftPanel.Instance;
            if (entryPanel != null) entryPanel.SetCountdownPaused(false);

            // ⚠【2026-10-12 已删】原来这里还有一段「抽完再掷 RollReroll → 额外再送一次免费抽」。
            //   主人报「抽中了技能还出现了再抽一次」→ 判定挪到这一抽**开始之前**（见本循环上方），
            //   「再来一次」从此是抽奖结果之一，不再与类型奖品叠加。别把这段加回来。
        }

        // 退出循环 = 要开战了：收起 BackpackPanel、隐藏抽奖按钮与「继续」，引导提示也关掉
        BattleEntryDraftPanel.Hide();
        TutorialHintUI.Instance?.Hide();
    }

    /// <summary>
    /// 抽奖「查余额」的<b>唯一出口</b>（只看不扣；真扣款在 <see cref="CoInstantPick"/> 里、
    /// 确认结果能生效之后才做）。【2026-10-18 双钱包拆分】抽奖只扣<b>抽奖币池</b> currentGold
    ///（启动金 / 通关补贴 / 抽中金币），花不到「本局获得金币」那本账。
    /// </summary>
    /// <param name="shortBy">不够时回传币种显示名（「抽奖币」），给 toast 用。</param>
    bool CanAffordDraw(DraftCategory? picked, int cost, out string shortBy)
    {
        shortBy = "抽奖币";
        return cost <= 0 || currentGold >= cost;
    }

    /// <summary>
    /// 抽奖扣抽奖币的<b>唯一出口</b>：直接扣战斗内抽奖币池 <c>currentGold</c>
    ///（启动金 / 通关补贴 / 抽中金币，带不出本局；【2026-10-18 双钱包拆分】花不到「本局获得金币」）。
    /// 扣完立刻刷抽奖面板旁的池子计数；余额不足返回 false（调用方已先用 <see cref="CanAffordDraw"/> 拦过，这里是保险）。
    /// </summary>
    public bool SpendBattleGold(int cost)
    {
        if (cost <= 0) return true;
        if (currentGold < cost)
        {
            Debug.LogError($"[BattleManager] 抽奖扣金币失败：需 {cost}，现有 {currentGold}（不应走到这里，前置 CanAffordDraw 漏拦）");
            return false;
        }
        currentGold -= cost;
        BattleEntryDraftPanel.NotifyPoolGoldChanged(currentGold);
        return true;
    }

    /// <summary>
    /// 抽奖币池进钱的<b>唯一出口</b>（2026-10-10 主人拍板「进战斗只带启动金」配套）：
    /// 启动金 / 每关通关补贴 / 抽中的金币，三种都走这里进 <c>currentGold</c>。
    /// </summary>
    /// <param name="countsAsEarned">
    /// false = <b>局内循环资源</b>（启动金 / 通关补贴 / 抽中金币）：抬高 <c>_goldAtRunStart</c>，
    /// 带不出本局（跟「战斗内的都是局内的」同口径，也不会被白刷）；
    /// true = 本局赚的钱（目前无调用方）：记进 <see cref="BattleRunStats.GoldEarned"/>，结算写回。
    /// </param>
    public void AddRunGold(int n, bool countsAsEarned)
    {
        if (n <= 0) return;
        currentGold += n;
        if (!countsAsEarned) _goldAtRunStart += n;
        else { RunStats.GoldEarned += n; BattleUI.Instance?.UpdateGold(RunEarnedGold); }
        BattleEntryDraftPanel.NotifyPoolGoldChanged(currentGold);   // 【2026-10-18 双钱包拆分】池子计数：抽奖面板旁 + 顶栏 DraftDisplay 两处同步
    }

    /// <summary>
    /// 点了抽奖按钮：不弹三选一，直接从该类型里抽 1 张、立刻生效，并把结果提示给玩家。
    /// 玩家买的是结果，不是选择 —— 选择留给免费的老虎机三选一。
    ///
    /// <para><b>扣钱与发奖的顺序（2026-10-05 修的漏洞）</b>：原来是「外层先扣钱 → 这里出结果」，
    /// 于是「池子空 / 卡无效 / 技能槽满 / 佣兵位满 / 背包满」这些情况都会
    /// <b>钱扣了、东西没拿到</b>。现在改成：<b>先出结果并确认真的生效，生效了才扣钱</b>；
    /// 拿不到东西就一分不扣、保底定序也不推进（不算玩家抽过这一抽）。</para>
    ///
    /// <param name="cost">这一抽的价格（币）。由 <see cref="BattleManager"/> 的编排层算好传进来，
    /// 本方法不再自己算价，避免两处价格对不上。</param>
    /// </summary>
    IEnumerator CoInstantPick(DraftCategory cat, int cost)
    {
        var dir = RunDraftDirector.Instance ?? RunDraftDirector.Ensure(this);
        if (dir == null)
        {
            Debug.LogError("[BattleManager] RunDraftDirector 缺失，抽奖奖励无法生效（未扣币）");
            yield break;
        }

        // 【2026-10-07 主人拍板】佣兵定向「一次一枚币，有几率失败」：
        // 花一次钱开一次口，未必说得动对方 —— 失败 = 钱照花、人没来（设计好的赌，不是 bug）。
        // ⚠ 引导局的保底佣兵（塔克）绝不许失败，否则引导会断在半路 → 引导局整段跳过；
        // ⚠ 免费额度（金币本）那几次 cost = 0，也不参与失败判定。
        // 【2026-10-10 主人拍板】扣的币统一为战斗内金币（SpendBattleGold 唯一出口）。
        if (cat == DraftCategory.Merc && cost > 0 && !IsTutorialRun)
        {
            float rate = SlotMachineDefs.MERC_FOCUS_SUCCESS_RATE;
            if (Random.value >= rate)
            {
                SpendBattleGold(cost);                        // 失败也要把这笔钱花掉
                Debug.Log($"[BattleManager] 佣兵定向招募失败（成功率 {rate:P0}），扣 {cost} 金币");
                UIManager.Instance?.ShowToast("招募失败：这一回没能说动对方。", true);
                yield break;
            }
        }

        // ① 保底：引导三拍的保底内容**只有这一处**编排，不许散成两处。
        DraftCard card = default;
        // 【2026-10-11 主人拍板】技能类不再保底：原来的「本局第一次抽到技能 = 白送职业初始技能」
        // （SlotMachineSystem.BuildGuaranteedSkillCard）已整条删除，技能一律走下面的随机池。
        // 2026-10-07 主人拍板：引导第 2 抽（佣兵）= 直接招募塔克本人入队（H003 剑盾卫士，稀有），
        // 碎片那条口径已作废（TutorialRules.MercDraftGivesFragment 整条删除）。
        if (cat == DraftCategory.Merc)
            card = SlotMachineSystem.BuildGuaranteedMercCard();
        // 2026-10-06 主人澄清：第一抽是「随机拿到一件新装备」，不是保底木盾；
        // 木制圆盾（equip_shield_1）只是**起步装备**换成它（player_job_base_stats P001 副手），
        // 跟抽奖是两码事 —— 装备保底那条已撤回，装备类一律走随机池。

        // ② 其余走正常卡池随机：按权重从整池里抽 1 张（概率与公示弹窗一致）
            if (!card.IsValid)
            {
                // 这一抽出不出「抽奖币」方向唯一出口 = SlotMachineSystem.GoldAllowed()：
                // 【2026-10-19 主人拍板】金币升格为第 4 个方向（独立权重 5，不再随佣兵联动），不再混在三类池子里；
                // 引导定序窗口内不出（主人拍板引导三拍必须是 装备→佣兵→技能）；
                // 保底佣兵窗口内不出（保底必须真的给佣兵）；本局抽中满 3 次后不再出；正式关第 1 抽也不出。
                card = DraftPool.PickOne(cat, SlotMachineSystem.GoldAllowed());
            }

        if (!card.IsValid)
        {
            // fail closed：出不了卡就不许扣钱，报出来让主人看见
            Debug.LogError($"[BattleManager] 抽奖池出不了卡（{cat}），本次未扣币、未计入保底定序");
            UIManager.Instance?.ShowToast("这一抽没能出结果，未扣币");
            yield break;
        }

        // ③ 先确认这张卡现在真能生效（槽位 / 星级 / 背包位 / 玩家肯不肯换装备），生效了才扣钱
        // 【2026-10-11 主人拍板】抽了技能/佣兵要立刻涨战力：与免费老虎机同口径，
        // 发奖前后各取一次真值算差（见下方 CoPickFromCategory 的既有写法），别漏这一拍。
        int powerBefore = RunLoadout.TotalPower();
        bool ok;
        string msg;
        if (card.Kind == DraftCardKind.Equip)
        {
            // 装备要走「要不要替换」的确认弹窗（2026-10-05 主人拍板），是异步的
            ok = false;
            msg = null;
            yield return dir.CoApplyEquipCard(card, (o, m) => { ok = o; msg = m; });
        }
        else
        {
            ok = dir.TryApplyCard(card, out msg);
        }

        if (!ok)
        {
            Debug.LogError($"[BattleManager] 抽奖结果无法生效（{cat}）：{msg}　本次未扣币、未计入保底定序");
            UIManager.Instance?.ShowToast($"{msg}（未扣币）");
            yield break;
        }

        // 免费抽（金币本开局送的那几次 / 「再来一次」送的那次）：不扣币，只消耗免费额度。
        // ⚠ 抽数序号照常推进 —— 引导局靠它一格一格走「装备→佣兵→技能」，
        //   不推进的话引导三拍会全落在同一类上，等于白教（正式关是纯随机，序号只作统计）。
        // 【2026-10-10 主人拍板】付费抽统一扣战斗内金币（价格按抽数递增：2026-10-23 起 40 起、每抽 +10）。
        // 【2026-10-11 主人拍板】例外：「再来一次」那一抽**不推进抽数** ——
        //   主人原话「这次抽不用涨费用」，否则奖励反而把下一抽的价格顶上去，玩家点不动。
        bool bonusFree = false;
        if (cost > 0)
            SpendBattleGold(cost);
        else if (SlotMachineSystem.ConsumeBonusFreeDraw())
            bonusFree = true;
        else
            SlotMachineSystem.ConsumeFreeDraw();

        if (!bonusFree)
            SlotMachineSystem.NoteDraw();      // 本局抽数 +1（跨关不清零）
        // 【2026-10-24 主人拍板】保底稀有记账：生效才算（含金币本免费额度抽），取消替换（ok=false 分支）绝不计数 —— 防刷。
        if (cat == DraftCategory.Equip)
            SlotMachineSystem.NoteEquipDrawResult(card);
        RunLoadout.Save();
        RunDraftDirector.RefreshSkillPower();
        // 【2026-10-11 主人拍板】战力涨了 → 头顶飘「战力 +N」+ 三档称赞；没涨就只刷数字。
        // 与 CoPickFromCategory（免费老虎机）同一口径，别两边写法分叉。
        int powerDelta = RunLoadout.TotalPower() - powerBefore;
        if (powerDelta > 0) PlayerPowerHud.NotifyGain(powerDelta);
        else PlayerPowerHud.NotifyPowerChanged();
        // 2026-10-05：抽完必须重建技能栏，否则新技能不上 HUD（连带「拖动改顺序」的软引导也没机会弹）。
        RunSkillBarUI.Refresh();
        // 【2026-10-10 主人拍板】抽到技能要**立即**看到底部技槽图标（主人反馈「抽了技能后没有立即显示图标」）：
        // 左栏 Rebuild 不刷底部 4 槽，逐帧 tick 又要等下一帧比对 —— 这里主动刷一次，抽完当场亮出来。
        BattleUI.Instance?.UpdateRunSkillSlots();
        BattleUI.Instance?.UpdateMercSkillSlots();
        // 【2026-10-10 主人反馈】抽到佣兵/技能后**头像栏**也要立即亮（UpdateCharacterSlots 内部自带
        // ShowMercHud 推导，抽到第一个佣兵时整条头像栏都是靠它挂出来的）。
        BattleUI.Instance?.UpdateCharacterSlots();

        string got = !string.IsNullOrEmpty(msg) ? msg
                   : !string.IsNullOrEmpty(card.Title) ? card.Title
                   : "已生效";
        // 【2026-10-06 主人反馈「获得的东西怎么不弹出提示，只弹奖励到手了」】
        // 存一份给后面的「等玩家点继续」硬引导用 —— 引导文案里会带这份内容。
        // 【2026-10-06 主人二次拍板：物品变动都要弹，不管对话/遮罩在不在】→ Toast 走 force=true，
        // 不再被 AnyBubbleShowing 吞掉；引导文案那份保留，等于屏幕浮字 + 引导框双保险。
        // ⚠ 这行赋值不动：RewardTip()（「等玩家点继续」硬引导文案）还读它。
        _lastDrawRewardMsg = $"【{SlotMachineSystem.CategoryName(cat)}】{got}";
        // 【2026-10-13 抽奖优化 #1】Toast 与开奖卡顺序对调（修「文字先剧透」）：
        // 真东西（装备/技能/佣兵）先弹卡，Toast 延后到开奖卡演出结束的**唯一通知**再弹
        // （DraftPrizePopupUI 的 onFinished，与停留时长常量同源，不写死等待秒数）；
        // 金币安慰奖维持 Toast 先弹（卡的价值低，无所谓剧透）。
        if (card.Kind == DraftCardKind.Gold)
        {
            UIManager.Instance?.ShowToast(_lastDrawRewardMsg, true);
            // 【2026-10-12 主人拍板】补「开奖」的仪式感：Toast 一闪就没了，玩家对抽奖没概念
            // （主人原话「现在没有抽中奖的喜悦 玩家对抽奖这个没有概念 怎么优化一下」）。
            ShowDrawPrizeCard(card, cat, powerDelta);
        }
        else
        {
            // 【2026-10-14 主人拍板】佣兵/技能**不再弹开奖卡**（主人原话「最开始那个不要了 只要我做的这个」）
            //   —— 开奖卡只留给金币安慰奖。
            // 【2026-10-15 主人拍板】获得佣兵弹窗（huodeyongbing）**不再随抽中默认弹出** ——
            //   入口移到战斗头像栏：点佣兵头像才弹（BattleUI.OnMercAvatarClicked → ShowFromEntry），
            //   抽中当场只流入队横幅（MercJoinBannerUI 照旧报喜）+ Toast；引导局在二抽后教这一步。
            bool rewardPopupClosed = !(card.Kind == DraftCardKind.SkillNew || card.Kind == DraftCardKind.SkillUp);
            // 闭包捕获本地副本：别让下一抽改写了 _lastDrawRewardMsg 字段后 Toast 内容串台
            string toastMsg = _lastDrawRewardMsg;
            if (card.Kind == DraftCardKind.MercRecruit)
                UIManager.Instance?.ShowToast(toastMsg, true);
            else if (card.Kind == DraftCardKind.SkillNew || card.Kind == DraftCardKind.SkillUp)
                StartCoroutine(SkillObtainPopupUI.CoShow(card, () =>
                {
                    UIManager.Instance?.ShowToast(toastMsg, true);
                    rewardPopupClosed = true;
                }));
            else if (card.Kind != DraftCardKind.Equip && DraftRewardDetailUI.NeedsDetail(card))
            {
                // 佣兵升级/升星拍暂无新预制体：维持「开奖卡 → 详情弹窗」原样（主人没点名改）。
                ShowDrawPrizeCard(card, cat, powerDelta, () =>
                    StartCoroutine(DraftRewardDetailUI.CoShow(card, () => UIManager.Instance?.ShowToast(toastMsg, true))));
            }
            else
                UIManager.Instance?.ShowToast(toastMsg, true);

            // 等获得弹窗关闭（玩家点「确认」；CoShow 内部 while(!root.activeSelf) 等到关才结束，
            // 异常路径 Root 缺失也会当场回调 onClosed，不会死等）。超时只是异常兜底，放行不卡死。
            float popupGuard = 0f;
            while (!rewardPopupClosed && popupGuard < 300f)
            {
                popupGuard += Time.unscaledDeltaTime;
                yield return null;
            }
            if (popupGuard >= 300f)
                Debug.LogWarning("[BattleManager] 等获得弹窗关闭超时（300s），放行继续流程");
        }
    }

    /// <summary>
    /// 【2026-10-12 主人拍板】抽中生效后弹一张开奖卡（演出细节见 <see cref="DraftPrizePopupUI"/>）。
    /// 这是开奖演出的<b>唯一出口</b>：文案 / 配色 / 稀有度档次都在这里定，别在别处再弹一份。
    /// ⚠ 稀有度配色只能问 <see cref="RarityPalette"/>，不许在本文件里抄色值。
    /// </summary>
    /// <param name="powerDelta">【2026-10-13 #2·裁定1】发奖后<b>实际生效</b>的战力增量（L2947 那个差），
    /// 不用 <see cref="DraftCard.PowerDelta"/> 预估值 —— 与头顶飘字同源，两屏不打架。</param>
    /// <param name="onCardFinished">【2026-10-13 #1】开奖卡演出结束的唯一通知；非空 = Toast 等它再弹（真东西）。</param>
    void ShowDrawPrizeCard(DraftCard card, DraftCategory cat, int powerDelta, System.Action onCardFinished = null)
    {
        string title = card.Title;
        string kind = SlotMachineSystem.CategoryName(cat);
        Color tint = RarityPalette.Get(card.Rarity);
        int tier = SkillRarityTier(card.Rarity);
        Sprite icon = null;
        // 【2026-10-13 #2】第 3 行两件套：品质词（色 = 卡面 tint）+ 特例文案（note）
        string rarityLabel = null;
        string note = null;

        // 【2026-10-13 #4】金币安慰奖分支<b>提到最前</b>：安慰奖是按类别混进各类池子的，
        // card.Kind==Gold 时 cat 仍可能是技能/佣兵 —— 旧写法排在 cat 分支后面，
        // 定向技能/佣兵抽到金币会被错标成「获得技能 / 佣兵升级」。
        if (card.Kind == DraftCardKind.Gold)
        {
            // 「折扣」语义：金色卡 + 抽奖币图 + 第 1 行「获得抽奖币」+ 无光晕/无全屏闪/停 0.60s（tier=普通档）。
            // 2026-10-18 主人拍板：战斗内的钱统一叫「抽奖币」（icon_draft_coin），金币是城镇那笔，不混说。
            // 特判金色 tint 不变（RarityPalette.Gold），不走 RarityPalette.Get(card.Rarity)（否则白灰 = 视觉最差档）。
            kind = "获得抽奖币";
            title = "抽奖币 +" + card.Amount;
            tint = RarityPalette.Gold;
            icon = CommonIconSprites.Load("icon_draft_coin");
            // 第 3 行只显示「回血 N」—— 直接用卡池给的 Desc（CollectGoldCard 已改成这句，不另写一份）
            note = card.Desc;
        }
        else if (card.Kind == DraftCardKind.Equip && card.Equip != null)
        {
            // 装备的稀有度在实例上（Rarity 枚举），不用卡片的 SkillRarity
            tint = RarityPalette.Get(card.Equip.rarity);
            tier = EquipRarityTier(card.Equip.rarity);
            icon = card.Equip.icon ?? card.Equip.template?.icon;
            kind = "获得装备";
            // 品质词：装备先过 DraftPool.MapRarity（唯一映射，不写第二份），词色跟卡框同色
            rarityLabel = SkillRarityUtil.DisplayName(DraftPool.MapRarity(card.Equip.rarity));
            // 【2026-10-13 #2·裁定1】实际增量为 0 = 玩家留着旧的、新件已折成分解材料 → 不显示「战力 +0」
            if (powerDelta <= 0) note = "已折成分解材料";
        }
        else if (cat == DraftCategory.Skill)
        {
            icon = BattleUI.LoadRunSkillIcon(card.Id);
            // 槽位参照系（2026-10-13 #2）：槽没满 → 「获得技能 n/4」；槽满走「技能升星」分支，不显示 4/4
            kind = card.Kind == DraftCardKind.SkillUp || RunLoadout.IsSkillFull
                 ? "技能升星"
                 : $"获得技能 {RunLoadout.SkillIds().Count}/{RunLoadout.MaxSkillSlots}";
            rarityLabel = SkillRarityUtil.DisplayName(card.Rarity);
        }
        else if (cat == DraftCategory.Merc)
        {
            // 佣兵头像要按 AssetId 找，这里不另外加载 —— 没图就只显示名字（不兜底造图）
            // 槽位参照系（2026-10-13 #2）：「招募佣兵 n/2」，n = 入队后计数（与 CategoryHint 同一口径）
            kind = card.Kind == DraftCardKind.MercRecruit
                 ? $"招募佣兵 {RunLoadout.Mercs().Count}/{RunLoadout.MaxRunMercs}"
                 : (card.Kind == DraftCardKind.MercStarUp ? "佣兵升星" : "佣兵升级");
            rarityLabel = SkillRarityUtil.DisplayName(card.Rarity);
        }

        if (string.IsNullOrEmpty(title)) title = kind;
        DraftPrizePopupUI.Show(BattleUI.Instance != null ? BattleUI.Instance.transform : null,
                               title, kind, tint, icon, tier, powerDelta, rarityLabel, note, onCardFinished);
    }

    static int SkillRarityTier(SkillRarity r)
    {
        switch (r)
        {
            case SkillRarity.Legendary: return DraftPrizePopupUI.TierLegend;
            case SkillRarity.Epic: return DraftPrizePopupUI.TierEpic;
            case SkillRarity.Rare: return DraftPrizePopupUI.TierRare;
            default: return DraftPrizePopupUI.TierCommon;
        }
    }

    static int EquipRarityTier(Rarity r)
    {
        switch (r)
        {
            case Rarity.Legendary: return DraftPrizePopupUI.TierLegend;
            case Rarity.Epic: return DraftPrizePopupUI.TierEpic;
            case Rarity.Rare: return DraftPrizePopupUI.TierRare;
            case Rarity.Uncommon: return DraftPrizePopupUI.TierRare;   // 绿档与蓝档同档演出
            default: return DraftPrizePopupUI.TierCommon;
        }
    }

    /// <summary>
    /// 战斗中再开一次进关抽奖 —— **正式入口，不是引导特例**。
    /// <para>2026-10-05 主人拍板：抽奖是主玩法（正式关也抽），所以「打完两波抽一次」这种节拍
    /// 走这里，不要在 TutorialDirector 里另写一套弹面板的代码。</para>
    /// 行为：冻住单位 + 收摇杆 → 弹进关抽奖面板 → 抽一次 → 收面板 → 恢复原来能不能动。
    ///
    /// <para><b>本方法天然「一次一抽」</b>（没有连抽循环）—— 引导的第 ②③ 拍走它，
    /// 正好对上主人「引导每次只能抽一次」的要求，不需要再另加限制开关。</para>
    /// </summary>
    /// <param name="guideText">给 null = 不强引导；给了就用硬遮罩圈住「随机抽奖」按钮。</param>
    public IEnumerator CoMidBattleDraft(string guideText = null)
    {
        var cats = SlotMachineSystem.AvailableCategories();
        if (cats.Count == 0)
        {
            // 与开局抽奖同口径：池子全空不静默跳过，控制台必须能看到原因
            Debug.LogError("[BattleManager] 抽奖池全空，中途抽奖不弹出");
            yield break;
        }

        bool canActBefore = UnitsCanAct;
        // ⚠ 冻住单位是「有借有还」：协程若在等待玩家时被外部 StopCoroutine（撤离 / 死亡 / 切场景），
        // finally 仍会执行 —— 否则 UnitsCanAct 会永远停在 false，整场战斗谁都动不了（2026-10-05）。
        try
        {
            UnitsCanAct = false;
            var hero = Hero.Instance;
            if (hero != null && hero.rb != null) hero.rb.velocity = Vector2.zero;
            // 【2026-10-05 主人拍板「不要摇杆了」→ 整条链路停用，先注释不删】
            // BattleJoystick.Instance?.SetVisible(false);

            int price = SlotMachineSystem.DrawPrice();
            int focusPrice = SlotMachineSystem.DrawFocusPrice();

            DraftCategory? picked = null;
            int choice = 0;
            int freeLeft = SlotMachineSystem.FreeDrawsLeft();
            // 【2026-10-10 主人拍板】面板余额显示战斗内金币（抽奖真扣的就是它，不再是另一套抽奖币）
            // 【2026-10-19 主人拍板】定向不吃免费：第二个参数永远传原价
            BattleEntryDraftPanel.Show(freeLeft > 0 ? 0 : price, focusPrice,
                cats, currentGold,
                c => { picked = c; choice = 1; },
                () => choice = 2, freeLeft);

            // 【防卡死兜底】面板构建失败（缺节点 / 缺图）时不会展开 —— 玩家看到的就是
            // 「画面冻住、卡牌没加载出来」，只能退游戏白扣体力（2026-10-18 玩家反馈）。
            // 3 秒还没开就不再冻场干等 300 秒：报错 + 提示，跳过这一拍
            //（yield break 走 finally：收面板 + 还原 UnitsCanAct，战斗恢复）。
            float openGuard = 0f;
            while (!BattleEntryDraftPanel.IsLotteryOpen && openGuard < 3f)
            {
                openGuard += Time.unscaledDeltaTime;
                yield return null;
            }
            if (!BattleEntryDraftPanel.IsLotteryOpen)
            {
                Debug.LogError("[BattleManager] 中途抽奖面板 3 秒内没弹出来（构建失败），跳过本拍抽奖，不再冻场");
                UIManager.Instance?.ShowToast("抽奖面板没加载出来，已跳过这次抽奖");
                yield break;
            }

            // 【2026-10-08 主人拍板】引导局**不催**：面板一弹出就把倒计时冻住（引导三拍都走这里），
            // 玩家看清楚自己抽到了什么再点「继续」。正式关照常跑那 30s 总预算。
            // 后面 WaitForContinue 里也有同一道判定，保证整段引导都不解冻。
            if (IsTutorialRun)
                BattleEntryDraftPanel.Instance?.SetCountdownPaused(true);

            if (!string.IsNullOrEmpty(guideText) && BattleEntryDraftPanel.Instance != null)
                TutorialHintUI.Ensure().ShowHard(guideText, BattleEntryDraftPanel.Instance.NormalButtonRect);

            float guard = 0f;
            while (choice == 0 && guard < 300f)
            {
                guard += Time.unscaledDeltaTime;
                yield return null;
            }
            TutorialHintUI.Instance?.Hide();

            // 【2026-10-06 主人拍板】引导三拍（装备→佣兵→技能）靠「本局累计抽数」定位序，
            // 玩家这一拍直接点「继续」（或倒计时走完）不抽 → 定序**不走格**，下一拍就重复出同一类
            //（主人报的正是「第二次没抽，第三次抽就抽出佣兵了」）。
            // → 引导局这一拍替他补抽一次，把定序推回正轨；**正式关一律不帮忙**
            //（主人原话「正式关就不要帮忙抽了」——正式关等权随机，本就没有定序可错）。
            if (choice == 2 && OneDrawPerBeat)
            {
                var autoTarget = SlotMachineSystem.ResolveCategory(cats);
                int autoCost = freeLeft > 0 ? 0 : price;
                if (currentGold < autoCost)
                {
                    // fail closed：不静默跳过，报出来让主人看见「引导三拍的钱不够了」
                    Debug.LogError($"[BattleManager] 引导补抽失败：局内金币不足" +
                                   $"（需 {autoCost}，现有 {currentGold}），本拍定序不会推进");
                }
                else
                {
                    int autoBefore = SlotMachineSystem.DrawIndex;
                    yield return CoInstantPick(autoTarget, autoCost);
                    Debug.Log($"[BattleManager] 引导补抽：玩家这一拍没抽，已自动抽一次（{autoTarget}）" +
                              $"　抽数 {autoBefore} → {SlotMachineSystem.DrawIndex}");
                }
            }

            if (choice == 1)
            {
                // null = 点的是随机；有值 = 点的是定向
                var target = picked.HasValue ? picked.Value : SlotMachineSystem.ResolveCategory(cats);
                // 【2026-10-19 主人拍板】免费次数只惠及随机抽（null）；定向永远按价扣币、不消耗免费额度
                int cost = !picked.HasValue && freeLeft > 0 ? 0
                         : (picked.HasValue ? SlotMachineSystem.FocusPrice(picked.Value) : price);
                // 与开局抽奖同口径：这里只查余额（按各自币种），真扣款等 CoInstantPick 确认结果能生效了再做
                if (!CanAffordDraw(picked, cost, out string shortBy))
                    UIManager.Instance?.ShowToast(shortBy + "不足");
                else
                {
                    yield return CoInstantPick(target, cost);
                    // 【2026-10-06 主人拍板】抽完奖励不许自动继续（引导 ②③ 拍 + 正式关中途抽都算）：
                    // 锁抽 + 面板倒计时，等玩家点「继续」才收面板、恢复战斗。
                    choice = 0;
                    yield return WaitForContinue(true,
                        !string.IsNullOrEmpty(guideText) ? $"{RewardTip()}　点「继续」继续战斗。" : null,
                        () => choice == 2);
                    // 【2026-10-08 主人拍板】点完「继续」立刻弹一句「继续前进」——
                    // 面板收起、战斗恢复的那一刻，给玩家一个明确的「接着往前走」的信号。
                    // 只走引导（guideText 非空）；正式关不打扰。想改停留秒数就改这个 2.2f。
                    if (!string.IsNullOrEmpty(guideText))
                        TutorialHintUI.Ensure().Show("继续前进", null, 2.2f);
                }
            }
        }
        finally
        {
            BattleEntryDraftPanel.Hide();
            // 【2026-10-05 主人拍板「不要摇杆了」→ 整条链路停用，先注释不删】
            // BattleJoystick.Instance?.SetVisible(true);
            UnitsCanAct = canActBefore;
        }
    }

    /// <summary>
    /// 「再来一次」的免费奖励：随机一类 → 老虎机走个形式 → 弹**该类型的三选一**。
    /// 不扣钱；玩家在这里才做选择，所以演出可以慢一点（JACKPOT_REVEAL_SEC）。
    /// </summary>
    IEnumerator CoJackpot(List<DraftCategory> cats)
    {
        // 【2026-10-19 主人拍板】免费老虎机排除「抽奖币」方向 —— 白送的奖励还抽回币没有意义，
        // 与「第 1 抽不安慰」同一精神；allowGold=false 时 RollCategory 只在三类里掷。
        var cat = SlotMachineSystem.RollCategory(cats, false);

        bool rolled = false;
        SlotMachineUI.Show(cats, cat, 0, () => rolled = true);
        float rollGuard = 0f;
        while (!rolled && rollGuard < 20f)
        {
            rollGuard += Time.unscaledDeltaTime;
            yield return null;
        }
        if (!rolled)
        {
            Debug.LogWarning("[BattleManager] 老虎机没有回调，跳过本次免费奖励");
            yield break;
        }

        yield return CoPickFromCategory(cat, 0);
    }

    /// <summary>老虎机停下那一类的三选一：复用现成的 LevelUpDraftUI.Show。</summary>
    /// <param name="retry">重选次数。选中的卡没生效时允许再选一次，但最多 <c>MaxFreeRetry</c> 次，防止死循环。</param>
    IEnumerator CoPickFromCategory(DraftCategory cat, int retry)
    {
        var cards = DraftPool.BuildCards(cat);
        // 免费奖励出不了卡就安静跳过（不扣钱，没什么可退的），但要报出来
        if (cards == null || cards.Count == 0)
        {
            Debug.LogWarning($"[BattleManager] 免费老虎机出不了卡（{cat}），本次奖励作废");
            yield break;
        }

        var dir = RunDraftDirector.Instance ?? RunDraftDirector.Ensure(this);
        if (dir == null)
        {
            Debug.LogError("[BattleManager] RunDraftDirector 缺失，抽奖奖励无法生效");
            yield break;
        }

        bool picked = false;
        bool landed = false;
        // 2026-10-05：装备卡要弹「换不换」确认窗，是异步的，不能在回调里直接结算 ——
        // 这里只记下玩家选了哪张，真正的生效挪到等待循环之后（那里才能 yield）。
        DraftCard chosen = default;
        LevelUpDraftUI.Show(cards, "抽奖结果 · 选一个", card =>
        {
            if (card.IsValid) chosen = card;
            picked = true;
        }, manageFreeze: false);

        float guard = 0f;
        while (!picked && guard < 180f)
        {
            guard += Time.unscaledDeltaTime;
            yield return null;
        }

        if (picked && chosen.IsValid)
        {
            string msg = null;
            // 【2026-10-06 主人拍板】战力涨幅：发奖前后各取一次真值，差多少就在玩家头顶飘多少。
            // 不用 RunLoadout.ConsumeLastDelta（那是「相对上次调用」的累计，会跨次叠加）。
            int powerBefore = RunLoadout.TotalPower();
            if (chosen.Kind == DraftCardKind.Equip)
                yield return dir.CoApplyEquipCard(chosen, (o, m) => { landed = o; msg = m; });
            else
                // 与「买了就出结果」同口径：分不清成功失败就会把「槽位已满」当成获奖弹给玩家
                landed = dir.TryApplyCard(chosen, out msg);

            // 【2026-10-13 主人拍板】免费抽同口径：详情弹窗（点任意位置关）在前，Toast 延后，防剧透
            if (landed && DraftRewardDetailUI.NeedsDetail(chosen))
                yield return DraftRewardDetailUI.CoShow(chosen);
            if (!string.IsNullOrEmpty(msg)) UIManager.Instance?.ShowToast(msg);
            if (landed)
            {
                RunLoadout.Save();
                RunDraftDirector.RefreshSkillPower();
                // 2026-10-05：免费这一抽也可能给技能，技能栏必须重建，否则新技能不上 HUD
                RunSkillBarUI.Refresh();
                // 【2026-10-06 主人拍板】战力涨了 → 头顶飘「战力 +N」+ 三档称赞；没涨就只刷数字。
                int delta = RunLoadout.TotalPower() - powerBefore;
                if (delta > 0) PlayerPowerHud.NotifyGain(delta);
                else PlayerPowerHud.NotifyPowerChanged();
            }
        }

        // 选了但没生效（槽位/背包满）：让玩家能再选一张，别白白吞掉这次免费奖励。
        // 最多重选 MaxFreeRetry 次 —— 三张全发不出去时不能一直弹下去（会死循环）。
        if (picked && !landed)
        {
            if (retry >= MaxFreeRetry)
            {
                Debug.LogWarning($"[BattleManager] 免费老虎机（{cat}）连续 {retry} 次选中的卡都没生效，放弃本次奖励");
                yield break;
            }
            Debug.LogWarning($"[BattleManager] 免费老虎机选中的卡未生效（{cat}），重选第 {retry + 1} 次");
            yield return CoPickFromCategory(cat, retry + 1);
        }
    }

    /// <summary>免费老虎机「选中的卡没生效」时最多重选几次。0 = 不重选。</summary>
    const int MaxFreeRetry = 1;

    void BeginRewardSequence()
    {
        if (_rewardSequenceStarted || _stageCleared) return;
        _rewardSequenceStarted = true;
        isInBattle = false;
        UnitsCanAct = false;

        FocusMarkSystem.Ensure()?.ResetForBattle();
        BattleUI.Instance?.EnsureBattleControls();
        BattleUI.Instance?.EnsureRunSkillSlotsRepaired();
        // 【2026-10-05 主人拍板「不要摇杆了」→ 整条链路停用，先注释不删】
        // BattleJoystick.Instance?.SetVisible(false);

        if (currentStage == null)
        {
            Debug.LogError("[BattleManager] 结算时 currentStage 为空");
            FinishStageAfterPortalReached();
            return;
        }

        // 2026-09-29：通关发抽奖币（与金币奖励完全分开，只用于下关进关抽奖）
        // 2026-10-05：发不发由**模式**说了算（IBattleMode.GrantsRunCoins）。
        //   金币本打完就结算回城、币随局清零，再发一笔就是条「+100 然后归零」的假收益 → 不发。
        if (Mode == null)
            Debug.LogError("[BattleManager] BeginRewardSequence：Mode 为空，无法判断本模式发不发抽奖币（本次未发）");
        else if (Mode.GrantsRunCoins)
            // 【2026-10-22 主人拍板：通关补贴停发，抽奖币只来自击杀（10/25/50）+ 启动金】
            // 主线正式关不再发通关补贴；GrantStageCoins 函数体保留不删（下线≠删除）。
            // 启动金（RunDraftDirector.OnRunStart）不动。
            // 误伤检查：金币本 GrantsRunCoins=false 本就不发；无限裂隙 GrantsRunCoins=true、
            // IsFormalMainRun=false → 照发，不受影响。
            if (!IsFormalMainRun)
                SlotMachineSystem.GrantStageCoins(currentStage.type);

        // 2026-10-05 主人拍板「局外在结算界面才发」：这里**不再**即时加金币、不再飘字。
        // 只把本关额度记进待发池，等结算（PersistBattleGold → GrantPendingStageGold）一次性发。
        // 口径不变：打完一章拿满，中途撤离 / 阵亡只拿已通关那几关的部分（结算侧就是这个算法）。
        int stageGold = StageGoldDefs.CurrentStageGold();
        if (stageGold > 0 && !IsSideIsland)   // 支线岛（2026-10-23 合并方案）不发主线关卡金——岛金=goldReward，走岛结算链
            _runStageGoldPending += stageGold;
        _runStagesCleared++; // 撤离补偿计数：本局已通关关数（2026-10-17）

        bool isBoss = currentStage.type == StageType.Boss;
        if (isBoss)
            BattleUI.Instance?.UpdateStageProgress(currentStage.stageIndex, atEndFlag: true);
        else
            // 【2026-10-21 主人拍板】进度条改按当前关波次推进：本关已清完全部波 → 满进度
            //（Boss 关保留原 atEndFlag:true，marker 停最右旗子，语义兼容）
            BattleUI.Instance?.UpdateWaveProgress(_waves != null ? _waves.Count : 0, _waves != null ? _waves.Count : 0);

        int bonusStar = 0;
        int equipCount = GameConfig.EQUIP_CHOOSE_COUNT;
        int ch = ChapterManager.Instance != null ? ChapterManager.Instance.currentChapter : 1;
        HiddenLevelSystem.AddStageClearExp(ch);
        int bonusGold = 0;
        // 支线岛（2026-10-23 合并方案）不发关卡任务金——岛产出走自己的结算链，宝箱演出只呈现装备与折金
        if (!_stageQuestGoldGranted && !IsSideIsland)
        {
            bonusGold = StageQuestClearGold > 0
                ? StageQuestClearGold
                : BattleQuestConfig.GetClearGold(ch, currentStage.type, BattleDifficulty);
            bonusGold = Mathf.RoundToInt(bonusGold * GoldGainMul);
        }

        // 问「这个模式掉不掉装备」，不再问「是不是金币本」——
        // 以后「武器副本」这类活动就算走活动入口，只要声明 DropsEquipment = true 就会掉装。
        if (!DropsEquipment)
            equipCount = 0;
        else if (isBoss)
        {
            bonusStar = 2;
            equipCount += 1;
        }
        else if (currentStage.type == StageType.Elite)
            bonusStar = 1;

        // 多出的奖励件直接折金，三选一只展示 3 张
        int blacksmithLevel = TownSystem.Instance != null ? TownSystem.Instance.GetBuildingLevel(BuildingType.Blacksmith) : 1;
        List<EquipInstance> rewards = (DropsEquipment && ConfigManager.Instance != null)
            ? ConfigManager.Instance.GetRandomEquipInstances(equipCount, blacksmithLevel, bonusStar, currentStage.type, GameConfig.RIFT_DROP_ATTR_BONUS, false, ch)
            : new List<EquipInstance>();

        if (rewards != null && rewards.Count > 3)
        {
            for (int i = 3; i < rewards.Count; i++)
                bonusGold += GameConfig.EquipScrapGold(rewards[i].rarity, rewards[i].star);
            rewards.RemoveRange(3, rewards.Count - 3);
        }

        EnsureRewardDirector();
        // 【2026-10-22 主人拍板】正式关（主线）去宝箱：宝箱演出链（StageClearRewardDirector.Begin →
        // 开箱/飞金币/折金 → 开门）对主线正式关停用，代码保留不删（下线≠删除）。
        // 直接开传送门 → 保留「传送门 → FinishStageAfterPortalReached」全链：PersistBattleGold（金币写回）、
        // ChapterManager.OnStageComplete（章节推进/首通记录）都挂在链上，进传送门后由
        // SettleNormalStageClear 弹结算（双钮：下一关/返回）。
        // 金币本 / 无限裂隙自己的结算链不动：照旧走宝箱 → 传送门 → 各自 SettleStageClear。
        //
        // 【2026-10-22 主人拍板 v4.1】再进一步：去传送门。末波清完**不开传送门**，直接调
        // FinishStageAfterPortalReached()。该函数的账务链完整可复用（已逐行确认，无门直调安全）：
        //   ResetPortalGateState（无门时 _portalGate=null、_portalParty 空 → 循环/Close 全空转，安全）
        //   → TickFallenMercsStagePassed（阵亡佣兵养血）→ Planner.NotifyStageResult（压力阀）
        //   → PersistBattleGold（金币写回）→ ChapterManager.OnStageComplete（章节推进）
        //   → Analytics.StageEnd / AdventureLogFragments 掉落 → Mode.SettleStageClear
        //   → SettleNormalStageClear 弹双钮结算（下一关/返回）。
        //   _tutorialPortalPending 分支不适用正式关：该标志只在引导局 BeginTutorialPortalExit
        //   （BattleManager.Death.cs 引导链）置位，正式关恒 false，不会误入引导撤离分支。
        //   无「走入门」演出前置可卡：进门行走检测 TickPortalWalkIn 只在 _portalActive=true 时跑，
        //   不开门 → _portalActive=false → 不触发、不卡、不空指针。
        // OpenClearPortalDirect / 宝箱链 / 传送门行走链全部保留退役不删（下线≠删除）。
        if (IsFormalMainRun)
        {
            Debug.Log("[BattleManager] 正式关去宝箱去传送门（v4.1）：直接进通关结算");
            FinishStageAfterPortalReached();
            return;
        }
        StageClearRewardDirector.Instance.Begin(rewards, bonusGold, currentStage.type);
        Debug.Log($"[BattleManager] 开始宝箱结算 type={currentStage.type} bonusGold={bonusGold} equips={rewards?.Count ?? 0}");
    }

    /// <summary>
    /// 【2026-10-22 主人拍板】正式关去宝箱后直接开门：与 StageClearRewardDirector 的开门段同款
    ///（PortalGate.Open + NotifyChuanSongMenOpened），门开不出来也不锁死玩家 —— 报错后直接走通关结算。
    /// 【2026-10-22 主人拍板 v4.1】退役：正式关不再开传送门（BeginRewardSequence 直调
    /// FinishStageAfterPortalReached）。本函数与宝箱链/传送门行走链保留不删（下线≠删除）。
    /// </summary>
    void OpenClearPortalDirect()
    {
        var gate = PortalGate.Open(BattleUI.Instance != null ? BattleUI.Instance.transform : null);
        if (gate != null)
        {
            NotifyChuanSongMenOpened(gate);
            return;
        }
        Debug.LogError("[BattleManager] 传送门没开出来（PortalGate.Open 返回空，详见其上一条报错），直接走通关结算");
        FinishStageAfterPortalReached();
    }

    /// <summary>
    /// 门开了（<see cref="PortalGate"/> 已挂到 BattleUI 下）：把要进门的队伍记下来，全员朝右走进门。
    /// <para>【2026-10-11 主人拍板】门是 UI 节点但代表世界位置：进门判定用 <c>PortalGate.WorldEnterX</c>，
    /// 玩家先走、佣兵隔一个身位跟着进，每人进门各播一次传送特效（见 <see cref="TickPortalWalkIn"/>）。</para>
    /// </summary>
    public void NotifyChuanSongMenOpened(PortalGate gate)
    {
        _portalGate = gate;
        _chuanSongMen = null;          // 场景旧门不再参与流程
        _portalActive = gate != null;
        _heroEnteredPortal = false;
        _heroEnteredAt = 0f;
        _portalOpenedAt = Time.unscaledTime;
        _portalEntered.Clear();
        _portalParty.Clear();

        if (_portalActive)
        {
            if (hero != null) _portalParty.Add(hero);
            var mercs = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetActiveMercs() : null;
            if (mercs != null)
            {
                for (int i = 0; i < mercs.Count; i++)
                    if (mercs[i] != null) _portalParty.Add(mercs[i]);
            }
            float gateX = gate.WorldEnterX;
            if (!float.IsNaN(gateX)) ExtendCameraMaxX(gateX + 3f);
        }

        // 让英雄/佣兵继续向右走向传送门
        UnitsCanAct = true;
        isInBattle = true; // Update 里检测走近传送门需要跑
        // 【2026-10-05 主人拍板「不要摇杆了」→ 整条链路停用，先注释不删】
        // BattleJoystick.Instance?.SetVisible(true);
        if (hero != null)
        {
            hero.Face(1);
            if (hero.rb != null)
                hero.rb.velocity = new Vector2(Mathf.Max(0.4f, hero.attr != null ? hero.attr.GetAttr(AttrType.MoveSpeed) : 1.2f), 0f);
        }
    }

    /// <summary>
    /// 门开着的时候每帧看队伍走到哪了：谁跨过门中心 → 播一次传送特效 + 藏起来；
    /// 玩家进了之后再等佣兵（最多 <see cref="PortalMercWaitTimeout"/> 秒）→ 全员进完走通关结算（黑屏 → 关卡界面）。
    /// </summary>
    void TickPortalWalkIn()
    {
        float gateX = _portalGate.WorldEnterX;
        if (float.IsNaN(gateX))
        {
            // 门挂不到 Canvas / 拿不到世界坐标时不能把玩家锁在门口：3s 后报错并直接结算。
            if (Time.unscaledTime - _portalOpenedAt >= 3f)
            {
                Debug.LogError("[BattleManager] 传送门的世界坐标一直算不出来（门没挂上 Canvas？），3s 后直接走通关结算");
                FinishStageAfterPortalReached();
            }
            return;
        }

        for (int i = 0; i < _portalParty.Count; i++)
        {
            var u = _portalParty[i];
            if (u == null || u.isDead || _portalEntered.Contains(u)) continue;
            if (UnitBase.GetCombatX(u) < gateX - PortalGate.EnterTolerance) continue;

            _portalEntered.Add(u);
            _portalGate.PlayEnterEffect(u.transform.position);
            PortalGate.SetUnitVisible(u, false);
            if (u == hero)
            {
                _heroEnteredPortal = true;
                _heroEnteredAt = Time.unscaledTime;
            }
            Debug.Log($"[BattleManager] {u.name} 进门 x={UnitBase.GetCombatX(u):F2} 门X={gateX:F2}");
        }

        // 玩家阵亡 / 不在场时不可能进门 —— 别把流程卡死，按「已进」起算收尾计时。
        if (!_heroEnteredPortal && (hero == null || hero.isDead))
        {
            _heroEnteredPortal = true;
            _heroEnteredAt = Time.unscaledTime;
        }

        if (!_heroEnteredPortal) return;

        bool allIn = true;
        for (int i = 0; i < _portalParty.Count; i++)
        {
            var u = _portalParty[i];
            if (u == null || u.isDead || u == hero) continue;
            if (!_portalEntered.Contains(u)) { allIn = false; break; }
        }

        if (allIn || Time.unscaledTime - _heroEnteredAt >= PortalMercWaitTimeout)
            FinishStageAfterPortalReached();
    }

    /// <summary>
    /// 新一局 / 新一关 / 通关结算：把传送门整体收掉，并把进过门的单位**重新显示出来**
    /// （进门时只关了渲染，逻辑还在 —— 不还原的话下一关玩家和佣兵就是隐形的）。
    /// </summary>
    void ResetPortalGateState()
    {
        for (int i = 0; i < _portalParty.Count; i++)
        {
            var u = _portalParty[i];
            if (u != null) PortalGate.SetUnitVisible(u, true);
        }
        if (_portalGate != null)
        {
            _portalGate.Close();
            _portalGate = null;
        }
        _chuanSongMen = null;
        _portalParty.Clear();
        _portalEntered.Clear();
        _heroEnteredPortal = false;
        _heroEnteredAt = 0f;
        _portalOpenedAt = 0f;
    }

    // 【2026-10-11】旧 PlayPortalEnterVfxOnce 已删：进门特效的唯一出口是 PortalGate.PlayEnterEffect
    // （玩家 / 佣兵各播一次，原来那个「只播一次」的写法和主人「佣兵进也要播」的要求冲突）。

    /// <summary>走进 chuansongmen 后：写档 → 结算 → 选关/回城</summary>
    public void FinishStageAfterPortalReached()
    {
        if (_stageCleared) return;
        _stageCleared = true;
        // 【2026-10-15 主人拍板】引导局传送门出口：全员进门 → 引导撤离结算（BattleSettlement → 回城），
        // 不进下面的正常关清场链（不发关卡奖励 / 不推进章节 / 不演选关）。
        if (_tutorialPortalPending)
        {
            _tutorialPortalPending = false;
            ResetPortalGateState();
            isInBattle = false;
            UnitsCanAct = false;
            if (hero != null && hero.rb != null) hero.rb.velocity = Vector2.zero;
            ClearAllMonsters();
            TriggerTutorialEvacuateWithSettlement();
            return;
        }
        // 门收掉 + 进过门的单位重新显示（不还原的话下一关玩家/佣兵是隐形的）
        ResetPortalGateState();
        // 2026-10-06 主人拍板：真正通关一关 → 阵亡过的佣兵养回 30% 血（防 20% 血进场的死亡螺旋）。
        // 挂在「通关」而不是「加载下一关」上：反复重打同一关不会白刷恢复。
        RunLoadout.TickFallenMercsStagePassed();
        // V3.0 压力阀：把本关通关血量喂给下一关（连续轻松通关 → 下一关多一波）
        float clearMaxHp = hero != null && hero.attr != null ? hero.attr.GetAttr(AttrType.MaxHp) : 0f;
        Planner?.NotifyStageResult(false, clearMaxHp > 0f ? hero.currentHp / clearMaxHp : 1f);
        isInBattle = false;
        UnitsCanAct = false;

        if (hero != null && hero.rb != null) hero.rb.velocity = Vector2.zero;

        // 每关通关只写回「本局获得金币」，抽奖币池按现值结转下一关（不重置回基线）——
        // 否则本关抽奖花销被退回，下一关的钱凭空变多（2026-10-20 主人反馈修复）
        PersistBattleGold(resetPool: false);
        // 冒险页「获得奖励」弹窗：主线小关首次通关记一条本关实际掉落。
        // 必须夹在 PersistBattleGold（关卡金先进 GoldEarned）与 OnStageComplete（写 clearedStages）之间，
        // 否则首通判定/金币差值都会少一笔（见 RecordStageRewardNoticeIfFirstClear）。
        RecordStageRewardNoticeIfFirstClear();
        ChapterManager.Instance?.OnStageComplete();

        int ch = ChapterManager.Instance != null ? ChapterManager.Instance.currentChapter : CurrentChapter;
        Analytics.StageEnd(ch, currentStage != null ? currentStage.stageIndex : 0, true,
            (double)(Time.unscaledTime - _battleStartTime)); // 埋点：关卡通关（胜利）
        bool bossStage = currentStage != null && currentStage.type == StageType.Boss;
        AdventureLogFragments.TryDropOnStageClear(ch, bossStage);

        // 通关结算交给模式：核心不需要知道一共有几种玩法、各自发什么、之后去哪。
        if (Mode == null)
        {
            Debug.LogError("[BattleManager] 通关结算失败：Mode 为空（本局不是从 StartNewRun 开始的？）");
            return;
        }
        // 【2026-10-24 主人拍板】正式关卡/支线岛/活动副本（金币/徽记）先播「战斗结果」界面再进各自结算；
        // 无限裂隙不播（每层通关即有累计奖励，直接走 SettleEndlessStageClear 续层链）。
        // 界面播完（或点击跳过）才走模式结算；引导局在进本函数前已分流，永远到不了这里。
        if (Mode.UsesBattleResultScreen)
            BattleResultScreen.Show(true, () => Mode.SettleStageClear(this));
        else
            Mode.SettleStageClear(this);
    }

    /// <summary>
    /// 主线冒险的通关结算：【2026-10-22 主人拍板「打一关回一次」】普通关清完直接回冒险页（不结束本局）；
    /// 章末 Boss 仍弹胜利结算 → 回城 / 直进下一章。金币本这类模式有自己的结算，不进这里。
    /// </summary>
    public void SettleNormalStageClear()
    {
        bool isBoss = currentStage != null && currentStage.type == StageType.Boss;
        var cm = ChapterManager.Instance;
        if (isBoss)
        {
            // 章节解锁（MarkChapterCleared）已在 ChapterManager 关卡落账时完成，
            // 回城后世界地图传送门 / 冒险页第二章即开放。
            // 【2026-10-22 主人拍板】章末 Boss 也弹双钮结算：【下一关】= 下一章第 1 关
            //（StartChapter(nextCh) 重建关卡图后走原 GoChapterGate 过场+直进）；
            // 跨章候选口径：OnStageComplete 章末分支不生成下一章关卡图（availableNextStages 仍是旧候选），
            // 下一章可用性以存档解锁章数（ChapterRouteTable.AvailableChapters）为准；
            // 不可用（最后一章/未解锁）→【下一关】隐藏，只留【返回】（onNextStage=null 单确认钮口径）。
            int nextChapter = (cm != null ? cm.currentChapter : 1) + 1;
            if (HasNextChapter(nextChapter))
            {
                ShowVictorySettlementThen(
                    EndRunAndReturnToTown,
                    () =>
                    {
                        var cmNext = ChapterManager.Instance;
                        if (cmNext == null)
                        {
                            Debug.LogError("[BattleManager] 进下一章失败：ChapterManager 为空，回城收尾");
                            EndRunAndReturnToTown();
                            return;
                        }
                        cmNext.StartChapter(nextChapter);
                        GoChapterGate(nextChapter, cmNext.availableNextStages);
                    },
                    "下一关");
            }
            else
                ShowVictorySettlementThen(EndRunAndReturnToTown);
            return;
        }

        // 【2026-10-11 主人拍板 · 三】普通关通关**不再弹「本局结算面板」**。
        // 主人原话：「怎么打完一关就结算了，我跟你说的打完一关进传送门，然后弹老虎机怎么没有了」。
        // （历史归因注释保留如上）
        //
        // 【2026-10-22 主人拍板「打一关回一次」】上面 10-22 双钮结算（下一关/返回）口径作废：
        // 普通关清完不再弹战斗内结算，直接回冒险页 —— 构筑/局内金币池随 RunLoadout 保留
        // （ReturnToTownKeepRun，不结束本局），下次进本从下一关续打；
        // 本关掉落由冒险页自动弹「获得奖励」（AdventureRewardNotice，与结算同账）。
        // 历史归因注释保留：10-11 主人拍板「打完一关进传送门弹老虎机」→ 10-22 早间「普通关双钮结算」→ 本次「直接回」。
        // 【2026-10-24 主人拍板】胜利结果界面已上移到 FinishStageAfterPortalReached 统一门
        // （所有适用模式一次到位：正式关含 Boss / 支线岛 / 活动副本；无限裂隙不播），这里直接回冒险页。
        ReturnToTownKeepRun();
    }

    /// <summary>
    /// 【2026-10-22 新增】主线小关**首次**通关 → 往 AdventureRewardNotice 记一条本关实际掉落
    /// （玩家回到城镇打开冒险页时自动弹「获得奖励」，AdventureUI.ShowPage 消费）。
    /// 金币本 / 无限裂隙 / 引导局不进队列（各有自己的结算链）。
    /// </summary>
    void RecordStageRewardNoticeIfFirstClear()
    {
        if (Mode == null || Mode.Id != BattleModeId.Normal) return;
        if (currentStage == null || ChapterManager.Instance == null) return;
        var data = SaveSystem.Instance?.Data;
        string key = ChapterManager.Instance.currentChapter + "_" + currentStage.stageIndex;
        bool firstClear = data?.clearedStages == null || !data.clearedStages.Contains(key);
        if (!firstClear) return;

        var notice = new AdventureRewardNotice.StageClear
        {
            chapter = ChapterManager.Instance.currentChapter,
            stageIndex = currentStage.stageIndex,
            gold = (RunStats != null ? RunStats.GoldEarned : 0) - _stageGoldEarnedAtStart,
            talent = (RunStats != null ? RunStats.TalentGained : 0) - _stageTalentAtStart,
            enchant = (RunStats != null ? RunStats.EnchantStoneDelta : 0) - _stageEnchantAtStart,
            mats = (RunStats != null ? RunStats.DecomposeMatDelta : 0) - _stageMatsAtStart,
        };
        if (RunStats != null && RunStats.EquipGains != null)
            for (int i = _stageEquipCountAtStart; i < RunStats.EquipGains.Count; i++)
                if (RunStats.EquipGains[i] != null)
                    notice.equips.Add(RunStats.EquipGains[i]);
        AdventureRewardNotice.Push(notice);
    }

    /// <summary>
    /// 【2026-10-22 主人拍板】下一章是否可进（章末结算【下一关】钮的显示条件）。
    /// 以 ChapterRouteTable.AvailableChapters（存档解锁区域）为准；打完 Boss 时
    /// OnStageComplete 已先 MarkChapterCleared/解锁，时序有保证。
    /// </summary>
    static bool HasNextChapter(int nextChapter)
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return false;
        var avail = ChapterRouteTable.AvailableChapters(data);
        return avail != null && avail.Contains(nextChapter);
    }

    /// <summary>
    /// 无限裂隙每层通关结算（由 EndlessBattleMode.SettleStageClear 调，2026-10-17 主人拍板）：
    ///   · 宝箱金币每层都发（可重复小额）；
    ///   · 首通奖励（金币+天赋石）只在该层是本次首通时发，BOSS 层 ×3；
    ///   · 2026-10-19 主人拍板：不再弹「撤离结算 / 继续深入」双选 —— 每层打完都是
    ///     「宝箱 → 传送门 → 自动进下一层」，离开只认两条路：死了，或右上角设置里主动撤离。
    /// </summary>
    public void SettleEndlessStageClear()
    {
        int stage = Mathf.Max(1, EndlessRiftStage);
        bool isBoss = GameConfig.IsEndlessBossStage(stage);

        // —— 宝箱掉落：每次通关都发 ——
        int chestGold = Mathf.RoundToInt(GameConfig.EndlessChestGold(stage) * GoldGainMul);
        if (chestGold > 0)
        {
            GrantRunGold(chestGold);
            GlobalToastUI.Show($"宝箱开启，金币 +{chestGold}", true);
        }

        // —— 每 10 层固定宝箱加发普通卷轴（2026-10-17 主人拍板）——
        if (stage % GameConfig.ENDLESS_CHEST_SCROLL_INTERVAL == 0)
        {
            var data = SaveSystem.Instance?.Data;
            if (data != null)
            {
                data.mercScrollCommon++;
                GlobalToastUI.Show($"第 {stage} 层固定宝箱：普通卷轴×1", true);
                SaveSystem.Instance?.Save();
            }
        }

        // —— 首通奖励：只在真正推进进度（本次首通）时发 ——
        if (EndlessRiftRules.MarkCleared(stage))
        {
            int mul = isBoss ? GameConfig.ENDLESS_BOSS_FIRSTCLEAR_MUL : 1;
            int gold = Mathf.RoundToInt(GameConfig.EndlessFirstClearGold(stage) * mul * GoldGainMul);
            int talent = GameConfig.EndlessFirstClearTalent(stage) * mul;
            if (gold > 0)
            {
                GrantRunGold(gold);
            }
            if (talent > 0)
                ResourceWallet.Add(ResourceWallet.ResourceType.TalentPoint, talent, save: false, notify: true);
            GlobalToastUI.Show(isBoss
                ? $"首通第 {stage} 层记录点！金币 +{gold}，天赋石 +{talent}"
                : $"首通第 {stage} 层：金币 +{gold}，天赋石 +{talent}", true);
            SaveSystem.Instance?.Save();
        }

        // —— 直接续层（2026-10-19：BOSS 层也自动进下一层，无双选）——
        AdvanceEndlessRiftStage();
    }

    /// <summary>
    /// 无限裂隙续层：过场（黑屏 + 本层映射章背景图，正好呈现换景）后重建下一层并进战。
    /// 不打关卡图（石墩）——无限裂隙的关间流程就是「打完一层 → 宝箱 → 下一层」。
    /// </summary>
    void AdvanceEndlessRiftStage()
    {
        EndlessRiftStage++;
        var cm = ChapterManager.Instance;
        if (cm == null)
        {
            Debug.LogError("[BattleManager] 无限裂隙续层失败：ChapterManager 为空，回城收尾");
            EndRunAndReturnToTown();
            return;
        }
        int nextStage = EndlessRiftStage;
        StartCoroutine(CoChapterGateLoading(GameConfig.EndlessChapterForStage(nextStage), () =>
        {
            cm.StartEndlessRift(nextStage);
            if (cm.stageMap != null && cm.stageMap.Count > 0)
                cm.SelectStage(cm.stageMap[0]);
        }));
    }

    /// <summary>
    /// 关间：过场（黑屏 + 本章背景图，换景演出，保留）→ 直接进下一关。
    /// 【2026-10-21 主人拍板】关间流程极简化：打完一关 → 开宝箱 → 直接进下一关，全程 0 点击；
    /// 石墩关卡图（BattleStageMapUI）+ 老虎机（NextStageRouletteUI）+ 恢复关（RestStagePopupUI 流程）
    /// 三件套全部停用（停用不删除）：ShowStageSelectUI 本体、BattleManager.Stats.cs 四个死方法调用点、
    /// BattleStageMapUI / NextStageRouletteUI 代码一律原样保留，旧「关卡图 → 老虎机 → 点石墩」流程 dormant。
    /// 推进口径：availableNextStages 为单路线性恒单候选（FinishStageAfterPortalReached 里
    /// OnStageComplete 已先更新为下一关候选，时序有保证）→ 取第一个 SelectStage 进关
    /// （内部调 BattleManager.LoadStage，见 ChapterManager.SelectStage）；传参 stages 为空时也用它兜底。
    /// 候选空/null → 沿用 UIManager.ShowStageSelectUI 的既有守卫口径（UIManager.cs）：
    /// LogWarning + ClearAllMercs + ReturnToTown。
    /// </summary>
    void GoChapterGate(int chapter, System.Collections.Generic.List<StageData> stages)
    {
        StartCoroutine(CoChapterGateLoading(chapter, () =>
        {
            var cm = ChapterManager.Instance;
            var candidates = cm != null ? cm.availableNextStages : null;
            var next = (stages != null && stages.Count > 0) ? stages[0]
                     : (candidates != null && candidates.Count > 0) ? candidates[0]
                     : null;
            if (next != null)
            {
                // 【2026-10-21 主人拍板】直接进下一关（0 点击）：SelectStage 内部调 BattleManager.LoadStage
                cm.SelectStage(next);
            }
            else
            {
                // 沿用 UIManager.ShowStageSelectUI 的既有守卫口径
                Debug.LogWarning("[BattleManager] 没有下一关可选，回城");
                MercenaryManager.Instance?.ClearAllMercs();
                GameSceneManager.Instance?.ReturnToTown();
            }
        }));
    }

    /// <summary>进关卡选择界面前那次 loading 过场停留多久（秒）。要调快慢只改这一个数。</summary>
    public const float ChapterGateLoadingSeconds = 1.2f;

    /// <summary>
    /// 「黑屏 + 本章背景图」的 loading 过场（2026-10-11 主人拍板）。
    /// 关卡之间不切场景，本来不会有 loading；主人要这个过场，所以在这里显式插一次。
    /// 复用的就是切场景那套 <see cref="LoadingUI"/>（同源同款，不另造一套过场），
    /// 背景图走 `Resources/UI/loading/beijing0N`。
    /// </summary>
    IEnumerator CoChapterGateLoading(int chapter, System.Action after)
    {
        BattleLoadingOverlay.Show(GameConfig.GetChapterMapName(chapter));
        BattleLoadingOverlay.SetChapterBackdrop(chapter);
        BattleLoadingOverlay.SetProgress(1f);
        yield return new WaitForSecondsRealtime(ChapterGateLoadingSeconds);
        BattleLoadingOverlay.Hide();
        after?.Invoke();
    }

    /// <summary>
    /// 本局构筑收尾：死亡 / 撤离 / 回城统一走这里。
    /// 引导局走内存模式：整包丢掉，不动玩家真正的本局存档；
    /// 金币与天赋石已按阶段写回城镇存档，掉落装备本就不带出。
    ///
    /// ⚠ **局内钱包的作废出口就在这里**（2026-10-05 主人拍板：「打完一章后清零，
    /// 不过玩家可以选择是否进行下一章，这时不清零」）。
    ///   · 回城（章末选「回城」）／阵亡／撤离 → 都走本方法 → **局内钱作废**（currentGold 里的
    ///     启动金 / 通关补贴 / 抽中金币都带不走，只有「本局真正赚到的」经 PersistBattleGold 写回）；
    ///   · 章末选「进下一章」→ <b>不走本方法</b>（`SettleNormalStageClear` 只切章，不收尾）→ **保留**。
    ///   别在别处再写第二份清零。下一局进关由 `SlotMachineSystem.GrantRunStarterGold` 重新补启动金。
    /// </summary>
    void EndRunLoadout()
    {
        if (Rules.Active)
        {
            if (Rules.EnableRunDraft)
            {
                RunLoadout.EndMemoryMode();
                RunSkillBarUI.Refresh();
            }
            return;
        }
        RunLoadout.Clear();
        RunSkillBarUI.Refresh();
    }

    /// <summary>
    /// 【2026-10-22 主人拍板「打一关回一次」新增】普通关通关直接回冒险页，但**不结束本局**：
    /// · 不清构筑 / 不 EndRunLoadout —— 下次进本 ResumeOrBegin 续上，从下一关继续（StartNewRunInternal 跳关）；
    /// · 局内金币池写进 RunLoadoutData.runGold 跨场景携带（局没结束，钱不算「带出本局」）；
    /// · 金币已逐关 PersistBattleGold 落盘，中断快照照常清（防重复入账）。
    /// 章末 Boss 不走这里：仍弹结算 → EndRunAndReturnToTown（整章完成才算出局）。
    /// </summary>
    public void ReturnToTownKeepRun()
    {
        if (RunLoadout.IsActive && RunLoadout.Data != null)
        {
            RunLoadout.Data.runGold = currentGold;
            RunLoadout.Save();
            // 【2026-10-22 主人原话「中途意外退出没打完 → 让玩家选继续/重新开始」】
            // 这一关是打完才回城的 = 干净收尾，清掉中断标记：下次进本不弹选择、照旧静默续局。
            RunLoadout.MarkReturnedClean();
        }
        BattleStateSaver.Instance?.ClearBattleState();
        GameSceneManager.Instance?.ReturnToTown();
    }

    /// <summary>
    /// 通关后「结束本局并回城」：清本局装备 → 清佣兵 → 清雇佣会话 → 结束构筑 → 回城。
    /// 这段原本在通关结算里被复制了 3 份，收成一处；以后改收尾流程只改这里。
    /// 注意：死亡（<see cref="OnHeroDead"/>）与主动撤离（<see cref="TriggerEvacuation"/>）
    /// 走的是另一套收尾（遗产 / 教程流程，且不回城），不共用本方法。
    /// </summary>
    public void EndRunAndReturnToTown()
    {
        _runClosed = true;   // 记录点「撤离结算」也收尾（与 TriggerEvacuation 同口径）
        GridBackpackSystem.Instance?.ClearRunEquipment();
        MercenaryManager.Instance?.ClearAllMercs();
        MercHireSession.ClearHired();
        EndRunLoadout();
        // 2026-10-09 主人拍板：通关回城也要清掉中断快照，否则通关落盘的金币会在崩溃恢复时被重复入账
        // （金币已在 FinishStageAfterPortalReached → PersistBattleGold 落盘一次，快照不清 = 下次启动 SettleInterruptedRun 再追加一次）
        BattleStateSaver.Instance?.ClearBattleState();
        GameSceneManager.Instance?.ReturnToTown();
    }

    /// <summary>
    /// 把「本局已通关但还没发」的局外金币一次性发掉 —— <b>唯一的发放出口</b>。
    /// <para>2026-10-05 主人拍板「局外在结算界面才发」：通关时只累加 <see cref="_runStageGoldPending"/>，
    /// 到<b>结算这一刻</b>（死亡 / 撤离 / 通关回城三条收尾都要走 <see cref="PersistBattleGold"/>）才入账，
    /// 于是结算界面的「金币 +N」与 HUD 上真正多出来的钱是同一笔，不会出现「战斗中先涨、结算再涨一次」。</para>
    /// ⚠ 别在别处再写第二份发放 —— 多发一次就是刷金。
    /// </summary>
    public void GrantPendingStageGold()
    {
        if (_runStageGoldPending <= 0) return;
        long gold = _runStageGoldPending;
        _runStageGoldPending = 0;
        GrantRunGold(gold);   // 2026-10-18：走进账唯一出口（本局获得金币），结算「金币」栏同账
        Debug.Log($"[BattleManager] 结算发放通关奖励 金币 +{gold}（本局通过关卡累计）");
    }

    /// <summary>
    /// 【2026-10-22 主人拍板「打一关回一次」】续局还原局内金币池（RunDraftDirector.OnRunStart 续局分支调）。
    /// _goldAtRunStart 一并抬到还原水位：局末清账（resetPool）回到「续局时的池子」，不会把玩家抽剩的钱清成 0。
    /// </summary>
    public void RestoreRunGoldPool(long gold)
    {
        if (gold <= 0) return;
        currentGold = gold;
        _goldAtRunStart = gold;
        Debug.Log($"[BattleManager] 续局还原局内金币池 {gold}");
    }

    /// <summary>
    /// 「本局获得金币」写回城镇的<b>唯一出口</b>（每关通关 / 死亡 / 撤离 / 通关回城都会走）。
    /// <paramref name="resetPool"/>：是否把抽奖币池重置回基线 _goldAtRunStart。
    /// 只有<b>局末清账</b>（死亡 / 撤离 / 回城）才重置（「带不出本局」口径）；
    /// 每关通关传 false —— 否则基线只加不减（启动金 / 补贴入基线，抽奖花销不扣基线），
    /// 重置等于把本关花掉的抽奖币全额退回，下一关的钱凭空变多（2026-10-20 主人反馈修复）。
    /// 【2026-10-18 主人拍板「顶部的是玩家本局获得的金币」】双钱包拆分后的口径：
    ///   · 写回的是 <see cref="BattleRunStats.GoldEarned"/>（顶部 HUD 那个数）按 <c>_earnedPaidGold</c>
    ///     水位取增量 —— 逐关写回不重复入账，死亡结算时顶部看到多少城镇就多多少；
    ///   · <c>currentGold</c> 只是抽奖币池（启动金 / 通关补贴 / 抽中金币），**带不出本局**，
    ///     局末清账（resetPool=true）时重置回基线，通关进下一关则按现值结转。
    /// </summary>
    void PersistBattleGold(bool resetPool = true)
    {
        var save = SaveSystem.Instance?.Data;
        if (save == null) return;
        // 结算才发的按关金币：必须先入账（进 GoldEarned），否则这一关的钱写不回存档
        GrantPendingStageGold();
        long owed = RunStats.GoldEarned - _earnedPaidGold;
        if (owed > 0)
            ResourceWallet.Add(ResourceWallet.ResourceType.Gold, owed, save: true, notify: false);
        else
            SaveSystem.Instance.Save();   // 与原实现同款：没有增量也落一次盘，别丢其它待存改动
        _earnedPaidGold = RunStats.GoldEarned;
        // 抽奖币池重置回基线 —— 仅局末清账（resetPool=true）执行，「带不出本局」口径；
        // 每关通关（resetPool=false）按现值结转下一关，否则本关抽奖花销会被基线全额退回。
        if (resetPool)
            currentGold = _goldAtRunStart;
    }

    #endregion
}
