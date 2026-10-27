using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 战斗编排器：本局状态、胜负/结算、能量与开场过场。
/// 波次规划/出怪 → <see cref="WavePlanner"/>；技能施放 → <see cref="SkillCastService"/>。
/// </summary>
public partial class BattleManager : Singleton<BattleManager>, ICombatBoundSingleton
{
    #region 场景引用与单位
    [Header("场景引用")]
    public Hero hero;
    public Transform spawnPoint;
    public Transform endPoint;
    public Transform[] monsterSpawnPoints;
    /// <summary>用户摆好的 unit 节点：所有人物/怪物挂在其下，位置即草地站立线</summary>
    public Transform unitRoot;

    public List<UnitBase> allyUnits = new List<UnitBase>();
    public List<UnitBase> monsters = new List<UnitBase>();
    public StageData currentStage;
    #endregion
    #region 生命周期与开局
    protected override void Awake()
    {
        base.Awake();
    }

    public void StartNewRun()
    {
        StartNewRunInternal();
    }

    /// <summary>
    /// 定本局的玩法模式。<b>这是全项目唯一决定「这是哪一种玩法」的地方。</b>
    /// 以后加无限关卡 / 世界BOSS / PVP，只在这里多加一条返回，其余代码一行不动。
    /// </summary>
    IBattleMode ResolveMode()
    {
        // 引导优先：它由剧情触发，跟玩家在冒险界面选哪个按钮无关。
        // 注意要在 StoryProgress.ConsumeTutorialBattleFlag() 之前判。
        if (StoryProgress.ShouldStartTutorialBattle()) return BattleModes.Tutorial;

        // 支线岛（2026-10-23 合并方案）：冒险页支线岛点击进本，判在徽记/金币本之前（意图明确优先）。
        if (AdventureUI.PendingSideIsland > 0) return BattleModes.SideIsland;

        // 终章追查关（2026-10-23 主人拍板「最后一关后剧情二选一」）：冒险页追查入口进本，判在活动副本之前。
        if (AdventureUI.PendingFinaleStage > 0) return BattleModes.Finale;

        // 徽记副本（2026-10-23 拍板「活动副本一拆二」）：入口 = AdventureUI.EnterBadgeDungeon()（本期无按钮）。
        // 判在金币本之前：两标志理论上同真时，后置的徽记副本意图优先。
        if (AdventureUI.PendingBadgeDungeon) return BattleModes.BadgeDungeon;

        // 卷轴副本（2026-10-24 主人拍板「第三章通关开启 · 每周五 · 15体力 · 2次」）：
        // 入口 = 冒险页活动列表「卷轴」横条，判在金币本之前（同徽记口径）。
        if (AdventureUI.PendingScrollDungeon) return BattleModes.ScrollDungeon;

        // 礼物副本（2026-10-26 主人拍板「掉落礼物的活动副本周六开启」）：
        // 入口 = 冒险页活动列表「礼物」横条，判在金币本之前（同徽记/卷轴口径）。
        if (AdventureUI.PendingGiftDungeon) return BattleModes.GiftDungeon;

        // 羁绊副本（2026-10-27 主人拍板「照礼物副本框架」）：入口 = 好感界面「羁绊地图」按钮，判在金币本之前。
        if (AdventureUI.PendingBondDungeon) return BattleModes.BondDungeon;

        // 活动副本（冒险界面下标 4）。这是一个「类」，不是一种玩法：
        // 现在只开了金币本；以后佣兵副本 / 武器副本 / 技能副本上了，
        // 在这里按「当前开放的是哪一个活动」返回对应模式即可。
        if (AdventureUI.PendingGoldDungeon) return BattleModes.GoldDungeon;

        // 无限裂隙（冒险界面下标 2）
        if (AdventureUI.PendingEndlessRift) return BattleModes.Endless;

        return BattleModes.Normal;
    }

    void StartNewRunInternal()
    {
        Debug.Log("[BattleManager] ===== StartNewRun 开始 =====");
        StopBattleSpawnCoroutines();
        // 新一局：清空「章内不重复」的关卡模式记录，重新洗牌（V3.0 起模式允许重复，此处仅清压力阀）
        StageModeTable.ResetRun();
        Planner?.ResetPressure();
        // 【2026-10-10 主人拍板】战斗钱包局内化：**城镇金币不带进战斗**，开局只有本局启动金。
        // 启动金由 RunDraftDirector.OnRunStart（非续关）走 SlotMachineSystem.GrantRunStarterGold 补 ——
        // 那里才读得到「初始资金」天赋等级。这里先归零，_goldAtRunStart 会随那笔一起抬起来，
        // 于是结算差额 = 本局净赚（抽奖花掉的钱也扣在里面），启动金既不会被白刷也不会被重复写回。
        currentGold = 0;
        _goldAtRunStart = 0;
        _runStageGoldPending = 0;
        _runStagesCleared = 0;
        _earnedPaidGold = 0;   // 【2026-10-18 双钱包拆分】已付水位同步归零
        _endlessEntryHintShown = false;   // 无限裂隙进场免费抽提示：每局重弹一次
        var data = SaveSystem.Instance?.Data;
        _enchantAtRunStart = data != null ? data.enchantStones : 0;
        _matsAtRunStart = data != null ? data.decomposeMats : 0;
        _stageQuestGoldGranted = false;
        _defeatedMonsterKills = 0;
        RunStats.Reset();
        // 「击败」计数新一局清零归位（右上角常驻计数隐藏）
        BattleSideHud.Instance?.ResetKillTotal();
        RunStats.Chapter = ChapterManager.Instance != null ? ChapterManager.Instance.currentChapter : 1;
        {
            var save = SaveSystem.Instance?.Data;
            string pn = save != null && !string.IsNullOrEmpty(save.playerDisplayName)
                ? save.playerDisplayName : "冒险者";
            RunStats.EnsureAlly(BattleRunStats.PlayerMvpKey, pn);
        }
        tempBuffs.Clear();
        _stageCleared = false;
        _portalActive = false;
        _rewardSequenceStarted = false;
        ResetPortalGateState();
        UnitsCanAct = false;
        AllowMonsterMapEnter = false;
        isAutoBattle = false;
        MonsterAttackStyleTable.Reload();
        // 纯冷却制：清能量换成「解除间隔 + 按槽位错峰上初始 CD」
        ResetPlayerSkillGcd();
        PrimePlayerSkillOpeningCooldowns();
        // 上一局/上一关残留的攻击/防御/暴击增益（定时增益层）必须清掉，否则会跨关带着走
        Hero.Instance?.attr?.ClearTimedBuffs();
        ResetMpForNewStage();
        MercenaryManager.Instance?.ClearAllMercs();
        allyUnits.RemoveAll(u => u == null || u is Mercenary);

        HidePortal();
        EnsureRewardDirector();
        StageClearRewardDirector.Instance?.CacheSceneRefs();
        StageClearRewardDirector.Instance?.HideClearProps();
        EnsurePortalAnimatorReady(endPoint);

        if (hero == null)
            hero = Hero.Instance != null ? Hero.Instance : FindObjectOfType<Hero>();
        if (hero == null)
        {
            Debug.LogError("[BattleManager] StartNewRun 失败：找不到 Hero，无法开战/刷怪");
            return;
        }

        if (!allyUnits.Contains(hero))
            allyUnits.Add(hero);

        try
        {
            hero.InitNewRun();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[BattleManager] hero.InitNewRun 异常（继续开战）: {e}");
        }

        // 玩法身份只在这里定一次；规则包由模式自带，不再单独判一次 IsTutorial。
        Mode = ResolveMode();
        Rules = Mode.Rules;
        TutorialPowerFantasy = false;
        _tutorialHpFromTable = false;
        if (Rules.Active)
        {
            StoryProgress.ResetTutorialRunInventoryIfNeeded();
            TutorialDirector.Instance?.ResetBattleTutorialRuntime();
            StoryProgress.ConsumeTutorialBattleFlag();
        }
        // 选职后一律发职业武器（含教程）；木剑兜底仅非教程
        if (BattleBagSystem.Instance != null)
        {
            PlayerJobDefs.ApplyForBattle();
            if (!Rules.SkipStarterWeaponFallback
                && BattleBagSystem.Instance.GetEquippedInLogicalSlot(EquipSlotType.MainHand) == null)
            {
                if (BattleBagSystem.Instance.EnsureStarterWeapon())
                    hero.RecalcAttr();
            }
        }
        // 【2026-10-05】SuppressStageClear / SkipLegacyOnEvacuate 已改成只读直通规则包，
        // 这里不再赋值（原先只在这一处设，换关只走 LoadStage 时会残留上一关的值）。
        runGoldGainMul = 1f;
        runMonsterAtkSpeedMul = 1f;
        ApplyChapter1RunModifiersFromSave();
        if (Rules.Active)
            Debug.Log("[BattleManager] 新手引导战斗：短关+强制撤离（规则包 TutorialRules）");

        // === 局内构筑（肉鸽影子）：技能 + 佣兵全部走本局 loadout，不读城镇酒馆存档 ===
        // 新手引导局：V6 起也开构筑（走内存模式），让玩家在引导里就学会三选一与技能顺序
        if (!Rules.Active || Rules.EnableRunDraft)
            RunDraftDirector.Ensure(this).OnRunStart();

        // loadout 生效时不再走城镇酒馆雇佣；否则保留旧路径（异常/特殊规则兜底）
        if (!RunLoadout.IsActive && !GameConfig.SOLO_PLAYER_BATTLE && !Rules.SkipMercPrecall)
        {
            EnsureTestMercenaries();
            SpawnMercenaries();
        }

        int targetChapter = AdventureUI.PendingBattleChapter;
        if (targetChapter < 1)
            targetChapter = SaveSystem.Instance?.Data?.maxUnlockedChapter ?? 1;
        if (targetChapter < 1) targetChapter = 1;
        BattleDifficulty = Mathf.Clamp(AdventureUI.PendingBattleDifficulty, 0, 2);
        // IsGoldDungeon 不再在这里写入：它由 Mode 派生（见属性定义）。
        // PendingGoldDungeon 只保留「读完即清」，避免下一局被上一局的静态标志污染。
        // 【2026-10-22 主人拍板「打一关回一次」】parked 局续/弃判定（必须在清 Pending* 标志之前）：
        // · 同章再进 → 续局：起始关 = RunLoadout.stageIndex（最后通关的小关）+ 1（见下方 BuildChapter 后跳关）
        // · 换章 / 进无限裂隙 / 进金币本 → parked 局作废（裂隙与金币本是独立进本，现状口径的延伸）
        var savedRun = RunLoadout.PeekResumable();
        bool resumeRun = false;
        if (savedRun != null && !StoryProgress.ShouldStartTutorialBattle())
        {
            if (AdventureUI.PendingEndlessRift
                || AdventureUI.PendingGoldDungeon
                || AdventureUI.PendingBadgeDungeon   // 徽记副本（2026-10-23）：独立进本，同金币本作废 parked 局
                || AdventureUI.PendingScrollDungeon  // 卷轴副本（2026-10-24）：独立进本，同金币本作废 parked 局
                || AdventureUI.PendingSideIsland > 0 // 支线岛（2026-10-23 合并方案）：独立进本，同金币本作废 parked 局
                || AdventureUI.PendingFinaleStage > 0 // 终章追查关（2026-10-23）：独立进本，同金币本作废 parked 局
                || AdventureUI.PendingBondDungeon    // 羁绊副本（2026-10-27）：独立进本，同金币本作废 parked 局
                || AdventureUI.PendingBattleChapter <= 0
                || AdventureUI.PendingBattleChapter != savedRun.chapter)
            {
                RunLoadout.Clear();
                savedRun = null;
                Debug.Log("[BattleManager] parked 局与本次进本不一致，已作废（打一关回一次口径）");
            }
            else
                resumeRun = true;
        }
        AdventureUI.PendingBattleChapter = 0;
        AdventureUI.PendingBattleDifficulty = 0;
        AdventureUI.PendingGoldDungeon = false;
        AdventureUI.PendingBadgeDungeon = false;   // 徽记副本（2026-10-23）：读完即清，照金币本口径
        AdventureUI.PendingScrollDungeon = false;  // 卷轴副本（2026-10-24）：读完即清，照徽记本口径
        // 支线岛（2026-10-23 合并方案）：捕获岛 id 供结算读（SideIslandId），Pending 读后即清（照金币本口径）
        SideIslandId = Mathf.Max(0, AdventureUI.PendingSideIsland);
        AdventureUI.PendingSideIsland = 0;
        // 终章追查关（2026-10-23）：捕获关号供结算读（FinaleStageId），Pending 读后即清（照支线岛口径）
        FinaleStageId = Mathf.Max(0, AdventureUI.PendingFinaleStage);
        AdventureUI.PendingFinaleStage = 0;
        // 羁绊副本（2026-10-27）：捕获佣兵 hireId 供结算标记 + 组队强制进队读（照支线岛「进本捕获」口径）
        BondMercHireId = AdventureUI.PendingBondHireId ?? "";
        AdventureUI.PendingBondDungeon = false;
        AdventureUI.PendingBondHireId = null;

        // 无限裂隙：起始层 = 记录点 + 1（读完即清，防污染）；普通章号参数对它无意义
        if (AdventureUI.PendingEndlessRift)
        {
            EndlessRiftStage = EndlessRiftRules.StartStage();
            AdventureUI.PendingEndlessRift = false;
            targetChapter = GameConfig.EndlessChapterForStage(EndlessRiftStage);
        }

        if (ChapterManager.Instance == null)
        {
            Debug.LogError("[BattleManager] ChapterManager 为空，用临时 Normal 关强行 LoadStage");
            LoadStage(new StageData { stageIndex = 0, type = StageType.Normal, nextStages = new List<int>() });
            return;
        }

        // 建关交给模式自己：核心不需要知道一共有几种玩法。
        Mode.BuildChapter(ChapterManager.Instance, targetChapter);

        // 【2026-10-24 主人拍板「只要打完就清」】续局跳关现在只剩一种来源：中断局弹窗点了「继续」
        // （被打断那关的关号由弹窗确认时从中断快照接回 stageIndex，见 RunLoadout.ConfirmResumeInterruptedRun）。
        // 普通关打完整局清档，不再存在 parked 局。
        if (resumeRun && savedRun != null
            && ChapterManager.Instance.stageMap != null && ChapterManager.Instance.stageMap.Count > 0)
        {
            int nextIdx = Mathf.Clamp(savedRun.stageIndex + 1, 0, ChapterManager.Instance.stageMap.Count - 1);
            var next = ChapterManager.Instance.stageMap[nextIdx];
            if (next != null)
            {
                // 类型走唯一出口（v4 按位置定死：0-based 4/9 → Boss、2/6 → 50% 精英），与正常推进同口径
                next.type = StageRoller.RollByPosition(next.stageIndex);
                ChapterManager.Instance.currentStageIndex = next.stageIndex;
                ChapterManager.Instance.availableNextStages.Clear();
                ChapterManager.Instance.availableNextStages.Add(next);
                Debug.Log($"[BattleManager] 中断局续打：从第 {nextIdx + 1} 关继续（第 {targetChapter} 章，类型 {next.type}）");
            }
        }
        // 【2026-10-24 主人拍板「只要打完就清」】无可续局的主线章：起始关改由存档通关进度决定 ——
        // 从「本章已通关最大关 + 1」打起（1-1 通了进本直接开 1-2）；整章已通关重打 → 从第 1 关。
        // 与冒险页「已前往 X-N」提示同口径，真源都是 clearedStages。只挂主线 Normal 章，
        // 支线岛 / 终章 / 活动副本 / 裂隙各有自己的建图与起始层，不掺和。
        else if (Mode != null && Mode.Id == BattleModeId.Normal
                 && ChapterManager.Instance.stageMap != null && ChapterManager.Instance.stageMap.Count > 0)
        {
            int stageCount = ChapterManager.Instance.stageMap.Count;
            int maxCleared = MaxClearedStageOneBased(targetChapter);   // 1 起；一关没通 = 0
            int startIdx = (maxCleared == 0 || maxCleared >= stageCount) ? 0 : maxCleared;
            var next = ChapterManager.Instance.stageMap[startIdx];
            if (next != null)
            {
                next.type = StageRoller.RollByPosition(next.stageIndex);
                ChapterManager.Instance.currentStageIndex = next.stageIndex;
                ChapterManager.Instance.availableNextStages.Clear();
                ChapterManager.Instance.availableNextStages.Add(next);
                Debug.Log($"[BattleManager] 打完即清：第 {targetChapter} 章从第 {startIdx + 1} 关开打（已通关到第 {maxCleared} 关）");
            }
        }

        StageData first = null;
        if (ChapterManager.Instance.availableNextStages != null && ChapterManager.Instance.availableNextStages.Count > 0)
            first = ChapterManager.Instance.availableNextStages[0];
        else if (ChapterManager.Instance.stageMap != null && ChapterManager.Instance.stageMap.Count > 0)
            first = ChapterManager.Instance.stageMap[0];

        if (first == null)
        {
            Debug.LogError("[BattleManager] 关卡图为空，创建临时 Normal 关");
            first = new StageData { stageIndex = 0, type = StageType.Normal, nextStages = new List<int>() };
        }

        Debug.Log($"[BattleManager] StartNewRun → LoadStage type={first.type} idx={first.stageIndex}");
        if (!Rules.SkipRiftEnteredAchievement)
            AdventureLogAchievements.OnRiftEntered();
        LoadStage(first);
    }

    /// <summary>
    /// 本章已通关的最大小关号（1 起；一关没通返回 0）。真源 clearedStages（"章_关0基"）。
    /// 与 AdventureUI.GetMaxClearedStage 同一口径（那边是 UI 私有，战斗侧自取一份，别为此动 UI）。
    /// </summary>
    static int MaxClearedStageOneBased(int chapter)
    {
        var cleared = SaveSystem.Instance?.Data?.clearedStages;
        if (cleared == null || cleared.Count == 0) return 0;
        int max = 0;
        string prefix = chapter + "_";
        foreach (var key in cleared)
        {
            if (string.IsNullOrEmpty(key) || !key.StartsWith(prefix, System.StringComparison.Ordinal)) continue;
            if (int.TryParse(key.Substring(prefix.Length), out int stage0)
                && stage0 >= 0 && stage0 < GameConfig.STAGES_PER_GRAND)
                max = Mathf.Max(max, stage0 + 1);
        }
        return max;
    }

    IEnumerator CoPreLevelThenLoad(StageData first)
    {
        // 遗产战前三选一已取消；保留方法避免外部误调编译失败
        LoadStage(first);
        yield break;
    }

    void EnsureTestMercenaries()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        if (data.townLevel == null) data.townLevel = new TownLevel();

        if (data.permanentMercs == null)
            data.permanentMercs = new List<MercenaryData>();

        for (int i = data.permanentMercs.Count - 1; i >= 0; i--)
        {
            var m = data.permanentMercs[i];
            if (m == null || string.IsNullOrEmpty(m.mercId))
            {
                data.permanentMercs.RemoveAt(i);
                continue;
            }
            // 无效弓手预制体 id 纠正
            if (m.mercId.StartsWith("gongshou") && Resources.Load<GameObject>("Units/" + m.mercId) == null)
                m.mercId = "gongshou101";
            if (m.level < 1) m.level = 1;
            if (m.star < 1) m.star = 1;
            if (string.IsNullOrEmpty(m.displayName)) m.displayName = m.mercId;
            if (string.IsNullOrEmpty(m.uid)) m.uid = System.Guid.NewGuid().ToString("N");
            MercSkillMigrate.AlignMercenary(m);
        }

        // 软著版：佣兵由酒馆三选一反复招募，不再空档自动塞弓手。
        // 旧逻辑保留注释，便于日后调试：
        // if (data.permanentMercs.Count == 0)
        // {
        //     data.permanentMercs.Add(new MercenaryData {
        //         mercId = "gongshou101", displayName = "测试弓手",
        //         favorLevel = 1, level = 1, star = 1,
        //         skillId = SkillRegistry.DefaultMercRangedSkillId,
        //         uid = System.Guid.NewGuid().ToString("N")
        //     });
        // }

        if (data.townLevel.tavern < 1 && data.permanentMercs.Count > 0)
            data.townLevel.tavern = 1;
    }

    void SpawnMercenaries()
    {
        var mm = MercenaryManager.Instance;
        if (mm == null) return;

        var data = SaveSystem.Instance?.Data;
        Vector3 basePos = spawnPoint != null ? spawnPoint.position : hero.transform.position;
        basePos.y = UnitBase.GROUND_Y;
        basePos.z = 0f;

        int max = mm.GetMaxMercSlots();
        int spawned = 0;
        bool countedLaodun = false;
        var party = mm.GetActiveMercData();
        for (int i = 0; i < party.Count && spawned < max; i++)
        {
            var md = party[i];
            if (md == null || string.IsNullOrEmpty(md.mercId)) continue;
            if (!GameConfig.IsMercAvailable(md.mercId, data)) continue;

            Vector3 pos = basePos;
            if (hero != null)
                pos.x = UnitCrowd.GetMercDesiredCombatX(hero, null, spawned);
            else
                pos.x = basePos.x + MERC_FRONT_SPACING * (spawned + 1);

            var merc = mm.SpawnMercenary(md.mercId, pos, Mathf.Max(1, md.level));
            if (merc != null)
            {
                MercSkillMigrate.AlignMercenary(md);
                string active = SkillRegistry.Instance != null
                    ? SkillRegistry.Instance.GetMercSkillId(md)
                    : md.skillId;
                string passive = SkillRegistry.Instance != null
                    ? SkillRegistry.Instance.GetMercPassiveSkillId(md)
                    : md.passiveSkillId;
                merc.SetupBattleSkills(active, passive);
                merc.SetPartyIndex(spawned);
                merc.SetDisplayName(md.displayName);
                if (!string.IsNullOrEmpty(md.hireId))
                    merc.SetHireId(md.hireId);
                else
                {
                    string resolved = MercRosterDefs.ResolveHireId(md.mercId);
                    if (!string.IsNullOrEmpty(resolved))
                        merc.SetHireId(resolved);
                }
                if (!string.IsNullOrEmpty(md.displayName))
                    merc.gameObject.name = "Merc_" + md.displayName;
                allyUnits.Add(merc);
                merc.OnDead += OnMercenaryDead;
                RunStats.EnsureAlly(GetAllyMvpKey(merc), GetAllyMvpDisplayName(merc));
                spawned++;
                if (!Rules.SkipLaodunAchievement && !countedLaodun && md.mercId.StartsWith("dunbing"))
                {
                    AdventureLogAchievements.OnLaodunBattled();
                    countedLaodun = true;
                }
                // 【2026-10-24 主人拍板「钩子肯定需要」】N-1 佣兵参战计数：城镇酒馆旧路径的入场点
                //（常规肉鸽局走 RunDraftDirector.RecruitMerc，另有同款调用）。出口唯一见 OnMercBattled。
                AdventureLogAchievements.OnMercBattled(
                    !string.IsNullOrEmpty(md.hireId) ? md.hireId : MercRosterDefs.ResolveHireId(md.mercId));
            }
        }
        Debug.Log($"[BattleManager] 已生成佣兵 {spawned} 个 (tavern槽={max})");
    }

    /// <summary>重生护符复活血比例（2026-10-21 主人拍板「护符给 80 的血」）。</summary>
    const float ReviveCharmHpRatio = 0.8f;

    /// <summary>
    /// 【2026-10-17】阵亡佣兵下一关复活 —— 兑现 2026-10-06 拍板「阵亡下一关按 20% 血复活（每过一关养回 30%）」。
    /// <para>旧实现只有开新一局才 <see cref="SpawnMercenaries"/>，同一局内阵亡的佣兵永远回不来
    /// （玩家报「佣兵打着打着就消失了」）。进下一关时扫 <see cref="RunLoadout"/>：
    /// fallen=true 且场上没有实例的，按 <see cref="RunLoadout.MercReviveHpRatio"/> 的血量重新生成
    /// （血量 = 20% + 30%×已过关数，封顶 100%）。</para>
    /// <para>生成参数与 <see cref="RunDraftDirector.RecruitMerc"/> 对齐：星级 / 技能 / 名字 / hireId /
    /// OnDead 订阅一个不少，保证它再阵亡时走同一条死亡链路。</para>
    /// </summary>
    void ReviveFallenMercsForStage()
    {
        var mm = MercenaryManager.Instance;
        if (mm == null || !RunLoadout.IsActive) return;

        var onField = mm.GetActiveMercs();
        var mercs = RunLoadout.Mercs();
        for (int i = 0; i < mercs.Count; i++)
        {
            var e = mercs[i];
            if (e == null || !e.fallen) continue;
            string key = !string.IsNullOrEmpty(e.hireId) ? e.hireId : e.mercId;

            // 场上已有同 id 实例就不补（理论上 fallen=true 时实例已 Destroy，这里只防手抖双生成）
            bool alreadyOnField = false;
            for (int k = 0; k < onField.Count; k++)
            {
                var m = onField[k];
                if (m == null) continue;
                string mKey = !string.IsNullOrEmpty(m.hireId) ? m.hireId : m.mercId;
                if (mKey == key) { alreadyOnField = true; break; }
            }
            if (alreadyOnField) continue;

            var heroRef = hero != null ? hero : Hero.Instance;
            int slot = mm.GetActiveMercs().Count;
            Vector3 pos;
            if (heroRef != null)
                pos = new Vector3(UnitCrowd.GetMercDesiredCombatX(heroRef, null, slot), UnitBase.GROUND_Y,
                    unitRoot != null ? unitRoot.position.z : 0f);
            else
                pos = new Vector3(transform.position.x + MERC_FRONT_SPACING * (slot + 1), UnitBase.GROUND_Y, 0f);

            var merc = mm.SpawnMercenary(e.mercId, pos, Mathf.Max(1, e.level));
            if (merc == null)
            {
                Debug.LogError($"[BattleManager] 阵亡佣兵复活失败：预制体缺失 mercId={e.mercId}");
                continue;
            }
            merc.mercStar = Mathf.Clamp(e.star, 1, 5);
            // SpawnMercenary → Init 会重置满血，惩罚必须在这里补砍回去（与 RunDraftDirector 同口径）
            float reviveRatio = RunLoadout.MercReviveHpRatio(e);
            if (reviveRatio < 1f && merc.attr != null)
            {
                float maxHp = merc.attr.GetAttr(AttrType.MaxHp);
                merc.currentHp = Mathf.Max(1f, maxHp * reviveRatio);
            }
            merc.SetupBattleSkills(e.skillId, e.passiveSkillId);
            merc.SetPartyIndex(slot);
            merc.SetDisplayName(e.displayName);
            if (!string.IsNullOrEmpty(e.hireId))
                merc.SetHireId(e.hireId);
            if (!string.IsNullOrEmpty(e.displayName))
                merc.gameObject.name = "Merc_" + e.displayName;
            if (!allyUnits.Contains(merc))
                allyUnits.Add(merc);
            merc.OnDead += OnMercenaryDead;
            merc.Face(1);
            Debug.Log($"[BattleManager] 阵亡佣兵复活：{e.displayName}({e.mercId}) 血量 {reviveRatio * 100f:0.}%（fallenStages={e.fallenStages}）");
        }
    }

    /// <summary>
    /// 【2026-10-21 主人需求】重生护符复活：战斗中佣兵阵亡时，在佣兵信息框点「复活」消耗 1 枚重生护符，
    /// <b>原地按 80% 血复活</b>（主人拍板「护符给 80 的血」）。
    /// <para>与 <see cref="ReviveFallenMercsForStage"/>（下一关惩罚性 20% 起步复活）并存；
    /// spawn 参数照抄该方法（星级/技能/名字/OnDead 订阅一个不少），血量在满血生成后补砍到 80%。</para>
    /// <para>返回 false（没护符/状态不对）时 Toast 已提示、信息框留在原处（主人拍板的「复活/取消」双钮）。</para>
    /// </summary>
    public bool TryReviveMercWithCharm(int loadoutIndex)
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null || !RunLoadout.IsActive) return false;
        var mercs = RunLoadout.Mercs();
        if (mercs == null || loadoutIndex < 0 || loadoutIndex >= mercs.Count) return false;
        var e = mercs[loadoutIndex];
        if (e == null || !e.fallen) return false;

        var mm = MercenaryManager.Instance;
        if (mm == null) return false;

        string key = !string.IsNullOrEmpty(e.hireId) ? e.hireId : e.mercId;

        // 场上已有同 id 实例就不补（防连点双生成）
        var onField = mm.GetActiveMercs();
        for (int k = 0; k < onField.Count; k++)
        {
            var m = onField[k];
            if (m == null) continue;
            string mKey = !string.IsNullOrEmpty(m.hireId) ? m.hireId : m.mercId;
            if (mKey == key) return false;
        }

        if (data.reviveCharms <= 0)
        {
            // 【2026-10-21 主人拍板】没护符点复活 = 提示道具不足（信息框留在原处继续看）
            UIManager.Instance?.ShowToast("道具不足：没有重生护符（关卡商店有售）。");
            return false;
        }
        data.reviveCharms--;
        e.fallen = false;
        e.fallenStages = 0;

        // ---- spawn 段与 ReviveFallenMercsForStage 同款；血量按护符口径补砍到 80% ----
        var heroRef = hero != null ? hero : Hero.Instance;
        int slot = mm.GetActiveMercs().Count;
        Vector3 pos;
        if (heroRef != null)
            pos = new Vector3(UnitCrowd.GetMercDesiredCombatX(heroRef, null, slot), UnitBase.GROUND_Y,
                unitRoot != null ? unitRoot.position.z : 0f);
        else
            pos = new Vector3(transform.position.x + MERC_FRONT_SPACING * (slot + 1), UnitBase.GROUND_Y, 0f);

        var merc = mm.SpawnMercenary(e.mercId, pos, Mathf.Max(1, e.level));
        if (merc == null)
        {
            // 复活失败要把护符与 fallen 记账退回去，不能白扣
            data.reviveCharms++;
            e.fallen = true;
            e.fallenStages = 0;
            Debug.LogError($"[BattleManager] 重生护符复活失败：预制体缺失 mercId={e.mercId}（护符已退还）");
            UIManager.Instance?.ShowToast("复活失败（佣兵预制体缺失），护符已退还。");
            return false;
        }
        merc.mercStar = Mathf.Clamp(e.star, 1, 5);
        // SpawnMercenary → Init 会重置满血，护符复活按主人拍板补砍到 80%
        if (merc.attr != null && ReviveCharmHpRatio < 1f)
        {
            float maxHp = merc.attr.GetAttr(AttrType.MaxHp);
            merc.currentHp = Mathf.Max(1f, maxHp * ReviveCharmHpRatio);
        }
        merc.SetupBattleSkills(e.skillId, e.passiveSkillId);
        merc.SetPartyIndex(slot);
        merc.SetDisplayName(e.displayName);
        if (!string.IsNullOrEmpty(e.hireId))
            merc.SetHireId(e.hireId);
        if (!string.IsNullOrEmpty(e.displayName))
            merc.gameObject.name = "Merc_" + e.displayName;
        if (!allyUnits.Contains(merc))
            allyUnits.Add(merc);
        merc.OnDead += OnMercenaryDead;
        merc.Face(1);

        SaveSystem.Instance?.Save();   // 护符扣了就落盘，中途杀进程别白嫖
        UIManager.Instance?.ShowToast($"重生护符发动，「{e.displayName}」原地复活！");
        BattleUI.Instance?.UpdateCharacterSlots();   // 槽亮回来 + 护符图标自动收掉
        Debug.Log($"[BattleManager] 重生护符复活：{e.displayName}({e.mercId}) 血量 {ReviveCharmHpRatio * 100f:0.}%（剩余护符 {data.reviveCharms}）");
        return true;
    }

    #endregion
}