using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

/// <summary>
/// 章节关卡管理器：每章一个大关，每大关 <see cref="GameConfig.STAGES_PER_GRAND"/> 小关（2026-10-23 批 B1 起）。
///
/// 现行规则（对齐 StageRoller，勿按旧注释排商人/诅咒）：
/// - 第 1 小关是起点，第 5 小关占位 Boss
/// - 中间关类型由 StageRoller 按位置定死：普通 / 精英 / Boss
/// - 关卡图按顺序一关一关解锁，一次只开一把锁（2026-10-19 拍板），不做分支/跳关选路
/// - 商人 / 附魔 / 诅咒未开放，不要在此复活预排逻辑
/// ⚠️ 「章」身份（currentChapter 1–8）批 B1 未动，仍走 ChapterRouteTable / SaveData 章级存档；
///    大关全局编号（1–126）属批 B2，见 Docs/关卡结构重构_篇大关小关_待办任务清单.md。
/// </summary>
public class ChapterManager : Singleton<ChapterManager>
{
    public int currentChapter = 1;
    public int currentStageIndex = 0;

    /// <summary>
    /// 【2026-10-31 B2 落地】当前打的是本篇第几座大关（1 起，1..ActProgressionDefs.GrandCountOf(篇)）。
    /// currentStageIndex 仍是大关内 0~4 的小关下标；两者合成篇内小关下标 <see cref="CurrentSubIndex0"/>，
    /// 存档 clearedStages 的 key 用的就是篇内下标（老档 0~4 正好是第一座大关）。
    /// </summary>
    public int currentGrand = 1;

    /// <summary>当前大关内小关下标 → 篇内小关下标（0 起）。存档 / 宝箱列 / 首通判定唯一主键。</summary>
    public int CurrentSubIndex0 => ActProgressionDefs.Sub0Of(currentGrand, currentStageIndex);

    /// <summary>
    /// 当前章的所有关卡节点
    /// </summary>
    public List<StageData> stageMap = new List<StageData>();

    /// <summary>
    /// 当前打完一关后可以选的下一关列表
    /// </summary>
    public List<StageData> availableNextStages = new List<StageData>();

    public event Action<StageData> OnStageSelected;
    public event Action OnChapterComplete;
    public event Action<List<StageData>> OnBranchReady; // 分支选择就绪

    /// <summary>
    /// 本章的关卡抽取状态。关卡类型不再开局排死，而是每关打完由 StageRoller 现抽，
    /// 抽的结果交给 NextStageRouletteUI 滚动展示。
    /// </summary>
    public StageRoller.ChapterRollState RollState { get; private set; } = new StageRoller.ChapterRollState();

    protected override void Awake()
    {
        base.Awake();
    }

    #region 章节生成

    /// <summary>
    /// 开始新章节，生成分支关卡图
    /// </summary>
    public void StartChapter(int chapterNum)
    {
        currentChapter = chapterNum;
        currentStageIndex = 0;
        // 【2026-10-31 B2 落地】进本时按存档大关进度落位（打完一座大关回城再进，从这里续上下一座）。
        var saveData = SaveSystem.Instance?.Data;
        currentGrand = saveData != null ? saveData.CurrentGrand(chapterNum) : 1;
        stageMap.Clear();
        availableNextStages.Clear();
        RollState.Reset();

        ChapterBranchTable.Reload();
        StageRollerWeightsTable.Reload();

        for (int i = 0; i < GameConfig.STAGES_PER_GRAND; i++)
        {
            stageMap.Add(new StageData { stageIndex = i, nextStages = new List<int>() });
        }

        AssignStageTypes();
        GenerateBranches();

        availableNextStages.Clear();
        availableNextStages.Add(stageMap[0]);

        Debug.Log($"[ChapterManager] 第{chapterNum}章关卡图生成完成，共{stageMap.Count}关");
    }

    /// <summary>金币副本：单场战斗，通关拿固定金，不推进主线章节。</summary>
    public void StartGoldDungeon(int chapterNum)
    {
        // 【2026-10-24 主人拍板「金币用第一章森林的战斗场景」】场景/怪池章 = 固定题材章（SceneChapter 单一出口），
        // 入参章号不再决定场景；金币奖励按难度固定 300/500/800（GetGoldDungeonClearGold 不依赖章节，三本产出同理）。
        currentChapter = ActivityDungeonRules.SceneChapter(ActivityDungeonKind.Gold);
        currentStageIndex = 0;
        stageMap.Clear();
        availableNextStages.Clear();
        var stage = new StageData
        {
            stageIndex = 0,
            type = StageType.Elite,
            nextStages = new List<int>()
        };
        stageMap.Add(stage);
        availableNextStages.Add(stage);
        Debug.Log($"[ChapterManager] 金币副本 场景=第{currentChapter}章(森林) 单场（入参章号 {chapterNum} 不再决定场景）");
    }

    /// <summary>
    /// 徽记副本（2026-10-23 主人拍板「活动副本一拆二」）：单场 Elite 战斗，通关只掉随机职业徽记
    /// （见 BattleManager.Settlement → MercGrowInventory.GrantBadgeDungeonDrops），不推进主线章节。
    /// 结构照抄 StartGoldDungeon。
    /// </summary>
    public void StartBadgeDungeon(int chapterNum)
    {
        // 【2026-10-24 主人拍板「徽记用亡灵的战斗场景」】场景/怪池章 = 固定题材章（第 2 章 幽冥墓园）。
        currentChapter = ActivityDungeonRules.SceneChapter(ActivityDungeonKind.Badge);
        currentStageIndex = 0;
        stageMap.Clear();
        availableNextStages.Clear();
        var stage = new StageData
        {
            stageIndex = 0,
            type = StageType.Elite,
            nextStages = new List<int>()
        };
        stageMap.Add(stage);
        availableNextStages.Add(stage);
        Debug.Log($"[ChapterManager] 徽记副本 场景=第{currentChapter}章(亡灵) 单场（入参章号 {chapterNum} 不再决定场景）");
    }

    /// <summary>
    /// 卷轴副本（2026-10-24 主人拍板「第三章通关开启 · 每周五 · 每次15体力 · 每日2次」）：
    /// 单场 Elite 战斗，通关只掉招募卷轴（见 BattleManager.Settlement → MercGrowInventory.GrantScrollDungeonDrops），
    /// 不推进主线章节。结构照抄 StartBadgeDungeon。
    /// </summary>
    public void StartScrollDungeon(int chapterNum)
    {
        // 【2026-10-24 主人拍板「卷轴用洞穴的战斗场景」】场景/怪池章 = 固定题材章（第 6 章 巨岩深窟）。
        currentChapter = ActivityDungeonRules.SceneChapter(ActivityDungeonKind.Scroll);
        currentStageIndex = 0;
        stageMap.Clear();
        availableNextStages.Clear();
        var stage = new StageData
        {
            stageIndex = 0,
            type = StageType.Elite,
            nextStages = new List<int>()
        };
        stageMap.Add(stage);
        availableNextStages.Add(stage);
        Debug.Log($"[ChapterManager] 卷轴副本 场景=第{currentChapter}章(洞穴) 单场（入参章号 {chapterNum} 不再决定场景）");
    }

    /// <summary>
    /// 礼物副本（2026-10-26 主人拍板「新加一个掉落礼物的活动副本周六开启」）：
    /// 单场 Elite 战斗，通关只掉好感礼物（见 BattleManager.Settlement → MercGrowInventory.GrantGiftDungeonDrops），
    /// 不推进主线章节。结构照抄 StartScrollDungeon。
    /// </summary>
    public void StartGiftDungeon(int chapterNum)
    {
        // 场景/怪池章 = 固定题材章（暂用第 6 章 巨岩深窟，与卷轴同款；要换改 ActivityDungeonRules.SceneChapter 的 Gift 行）
        currentChapter = ActivityDungeonRules.SceneChapter(ActivityDungeonKind.Gift);
        currentStageIndex = 0;
        stageMap.Clear();
        availableNextStages.Clear();
        var stage = new StageData
        {
            stageIndex = 0,
            type = StageType.Elite,
            nextStages = new List<int>()
        };
        stageMap.Add(stage);
        availableNextStages.Add(stage);
        Debug.Log($"[ChapterManager] 礼物副本 场景=第{currentChapter}章 单场（入参章号 {chapterNum} 不再决定场景）");
    }

    /// <summary>
    /// 羁绊副本（2026-10-27 主人拍板「照礼物副本框架」）：好感满佣兵专属单场 Elite 战斗，
    /// 难度固定（≈5~7 章强度，见 MercFavorDefs.BondDungeonChapter / BondDungeonDifficulty），不通关掉落、不推进主线。
    /// 结构照抄 StartGiftDungeon；场景章 = 固定高章（难度来源之一），入参章号同样不再决定场景。
    /// </summary>
    public void StartBondDungeon(int chapterNum)
    {
        currentChapter = MercFavorDefs.BondDungeonChapter;
        currentStageIndex = 0;
        stageMap.Clear();
        availableNextStages.Clear();
        var stage = new StageData
        {
            stageIndex = 0,
            type = StageType.Elite,
            nextStages = new List<int>()
        };
        stageMap.Add(stage);
        availableNextStages.Add(stage);
        Debug.Log($"[ChapterManager] 羁绊副本 场景=第{currentChapter}章 单场（入参章号 {chapterNum} 不再决定场景）");
    }

    /// <summary>
    /// 竞技场挑战（2026-10-30 主人拍板「竞技场真战斗」）：单场 Elite 战斗，敌方 = 机器人佣兵镜像小队
    /// （生成走 BattleManager 开战分流 → ArenaEnemySpawner），不推进主线。
    /// 结构照抄 StartGoldDungeon；场景章 = 1~8 章随机（主人拍板「战斗地图随机章节，每场地图不同」；
    /// 主线共 8 章，见 AdventureUI.chapterBackgrounds[8]），入参章号同样不决定场景。
    /// </summary>
    public void StartArena(int chapterNum)
    {
        // 【2026-10-30 主人拍板「战斗地图随机章节」】场景/怪池章 = 1~8 章随机（含 8，每场一掷）
        currentChapter = UnityEngine.Random.Range(1, 9);
        currentStageIndex = 0;
        stageMap.Clear();
        availableNextStages.Clear();
        var stage = new StageData
        {
            stageIndex = 0,
            type = StageType.Elite,
            nextStages = new List<int>()
        };
        stageMap.Add(stage);
        availableNextStages.Add(stage);
        Debug.Log($"[ChapterManager] 竞技场挑战 场景=第{currentChapter}章(随机) 单场（入参章号 {chapterNum} 不决定场景）");
    }

    /// <summary>
    /// 支线岛（2026-10-23 合并方案）：单场 Boss 战。currentChapter = 岛的背景篇（怪池/背景资产同源复用），
    /// stageIndex=0 的 Boss 关（WavePlanner 的 Boss 取池按 STAGES_PER_GRAND-1，与 stageIndex 无关，真 Boss 行必入池）。
    /// 结构照抄 StartBadgeDungeon；通关结算走 SideIslandBattleMode.SettleStageClear（不进 OnStageComplete 主线链）。
    /// </summary>
    public void StartSideIsland(int islandId)
    {
        var isl = SideIslandDefs.Get(islandId);
        if (isl == null)
        {
            int fixedId = Mathf.Clamp(islandId, 1, SideIslandDefs.Count);
            Debug.LogError($"[ChapterManager] StartSideIsland：islandId={islandId} 非法，钳到岛{fixedId}（定义域 1..{SideIslandDefs.Count}）");
            isl = SideIslandDefs.Get(fixedId);
        }
        currentChapter = Mathf.Clamp(isl.act, 1, 8);
        currentStageIndex = 0;
        stageMap.Clear();
        availableNextStages.Clear();
        var stage = new StageData
        {
            stageIndex = 0,
            type = StageType.Boss,
            nextStages = new List<int>()
        };
        stageMap.Add(stage);
        availableNextStages.Add(stage);
        Debug.Log($"[ChapterManager] 支线岛「{isl.name}」（岛{isl.id}）背景第{isl.act}篇 单 Boss 关");
    }

    /// <summary>
    /// 终章追查关（2026-10-23 主人拍板「最后一关后剧情二选一，选完再出几关进完整剧情」）：
    /// 单场 Boss 战（关号 1..2 见 FinaleDefs）。currentChapter = 关的背景篇（act=8 占位，怪池/背景同源复用），
    /// stageIndex=0 的 Boss 关——结构照抄 StartSideIsland；结算走 FinaleBattleMode.SettleStageClear
    /// （不进 OnStageComplete 主线链，标记+关后剧情都在自己的链里）。
    /// </summary>
    public void StartFinaleStage(int stageId)
    {
        var st = FinaleDefs.Get(stageId);
        if (st == null)
        {
            int fixedId = Mathf.Clamp(stageId, 1, FinaleDefs.Count);
            Debug.LogError($"[ChapterManager] StartFinaleStage：stageId={stageId} 非法，钳到关{fixedId}（定义域 1..{FinaleDefs.Count}）");
            st = FinaleDefs.Get(fixedId);
        }
        currentChapter = Mathf.Clamp(st.act, 1, 8);
        currentStageIndex = 0;
        stageMap.Clear();
        availableNextStages.Clear();
        var stage = new StageData
        {
            stageIndex = 0,
            type = StageType.Boss,
            nextStages = new List<int>()
        };
        stageMap.Add(stage);
        availableNextStages.Add(stage);
        Debug.Log($"[ChapterManager] 终章追查「{st.name}」（关{st.id}）背景第{st.act}篇 单 Boss 关");
    }

    /// <summary>
    /// 无限裂隙（2026-10-17 主人拍板）：按绝对层数建**单层**关。
    /// 背景章 = 每 5 层轮换现有 8 章资源（怪物文件夹同源，自动跟切）；
    /// 层类型（2026-10-19 主人拍板）：5 的倍数 = BOSS 层（10 的倍数是大 BOSS），
    /// 精英层每 10 层随机 2~4 个（GameConfig.IsEndlessEliteStage，越深越多），其余普通层。
    /// </summary>
    public void StartEndlessRift(int riftStage)
    {
        int stage = Mathf.Max(1, riftStage);
        currentChapter = GameConfig.EndlessChapterForStage(stage);
        currentStageIndex = 0;
        stageMap.Clear();
        availableNextStages.Clear();
        var type = GameConfig.IsEndlessBossStage(stage) ? StageType.Boss
                 : GameConfig.IsEndlessEliteStage(stage) ? StageType.Elite
                 : StageType.Normal;
        var sd = new StageData
        {
            stageIndex = 0,
            type = type,
            nextStages = new List<int>()
        };
        stageMap.Add(sd);
        availableNextStages.Add(sd);
        Debug.Log($"[ChapterManager] 无限裂隙 第{stage}层（背景章{currentChapter}，类型{type}）");
    }

    /// <summary>
    /// 分配关卡类型占位：第1关普通、末关（第5小关）Boss；中间类型由 StageRoller 按位置定死。
    /// 【2026-10-23 批 B1】每局从「每章 10 关」改为「每大关 5 小关」（STAGES_PER_GRAND）。
    /// （旧版预排商人/诅咒等已被轮盘规则取代，避免双轨不一致。）
    /// </summary>
    void AssignStageTypes()
    {
        for (int i = 0; i < stageMap.Count; i++)
            stageMap[i].type = StageType.Normal;

        stageMap[0].type = StageType.Normal;
        stageMap[GameConfig.STAGES_PER_GRAND - 1].type = StageType.Boss;

        string log = "[ChapterManager] 关卡占位: ";
        for (int i = 0; i < stageMap.Count; i++)
            log += $"{i + 1}:{stageMap[i].type.ToString()[0]} ";
        Debug.Log(log);
    }

    /// <summary>
    /// 生成关卡连接。
    /// 【2026-10-19 主人拍板】关卡图按顺序一关一关解锁，一次只开一把锁，
    /// 每关类型由老虎机现抽 —— 不做分支/跳关选路。
    /// 旧的 ChapterBranchTable 主边 / 固定跳关 / 随机跳关整段停用：
    /// nextStages 永远只有 i+1 一条 → availableNextStages 恒为单候选
    /// → BattleStageMapUI 只走单路流程，分支选路 UI（StartBranchFlow）不再触发。
    /// </summary>
    void GenerateBranches()
    {
        for (int i = 0; i < stageMap.Count; i++)
            stageMap[i].nextStages.Clear();

        // 每关的下一关固定为 i+1（线性顺序）
        for (int i = 0; i < GameConfig.STAGES_PER_GRAND - 1; i++)
            stageMap[i].nextStages.Add(i + 1);

        LogBranchInfo();
    }

    void ApplyRandomSkipBranches()
    {
        int branchCountMin = 1;
        int branchCountMax = 2;
        int poolFrom = 1;
        int poolTo = 5;
        int skipDistance = 2;

        if (ChapterBranchTable.TryGetRules(currentChapter, out var rules))
        {
            branchCountMin = rules.branchCountMin;
            branchCountMax = rules.branchCountMax;
            poolFrom = rules.branchPoolFrom;
            poolTo = rules.branchPoolTo;
            skipDistance = rules.skipDistance;
        }

        int branchCount = UnityEngine.Random.Range(branchCountMin, branchCountMax + 1);
        var branchPool = new List<int>();
        for (int i = poolFrom; i <= poolTo; i++) branchPool.Add(i);

        for (int i = branchPool.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            int temp = branchPool[i];
            branchPool[i] = branchPool[j];
            branchPool[j] = temp;
        }

        for (int i = 0; i < branchCount && i < branchPool.Count; i++)
        {
            int nodeIdx = branchPool[i];
            int skipTarget = nodeIdx + skipDistance;
            if (skipTarget <= GameConfig.STAGES_PER_GRAND - 2
                && nodeIdx >= 0 && nodeIdx < stageMap.Count
                && !stageMap[nodeIdx].nextStages.Contains(skipTarget))
                stageMap[nodeIdx].nextStages.Add(skipTarget);
        }
    }

    void LogBranchInfo()
    {
        var branchNodes = new List<int>();
        for (int n = 0; n < stageMap.Count; n++)
        {
            if (stageMap[n].nextStages.Count > 1)
                branchNodes.Add(n);
        }
        if (branchNodes.Count == 0) return;

        string log = "[ChapterManager] 分支点: ";
        foreach (var n in branchNodes)
        {
            log += $"第{n + 1}关→[{string.Join(",", stageMap[n].nextStages.Select(x => (x + 1).ToString()))}] ";
        }
        Debug.Log(log);
    }

    #endregion

    #region 关卡选择

    /// <summary>
    /// 选择下一关
    /// </summary>
    public void SelectStage(StageData stage)
    {
        currentStageIndex = stage.stageIndex;
        BattleManager.Instance.LoadStage(stage);
        OnStageSelected?.Invoke(stage);
    }

    /// <summary>
    /// 恢复关专用（2026-10-20 主人拍板）：恢复关不进战斗，只把进度指针推进到该关，
    /// 让 OnStageComplete 按「刚通关恢复关」记账（RecordCleared / 下一关候选池）。
    /// 与 SelectStage 的唯一区别：绝不调 BattleManager.LoadStage。
    /// </summary>
    public void SelectRestStage(StageData stage)
    {
        if (stage == null) return;
        currentStageIndex = stage.stageIndex;
        OnStageSelected?.Invoke(stage);
    }

    /// <summary>
    /// 当前关卡通关，更新可选下一关
    /// </summary>
    public void OnStageComplete()
    {
        if (BattleManager.Instance != null && BattleManager.Instance.IsGoldDungeon)
            return;
        // 徽记副本（2026-10-23 拍板）：同金币本，通关不走主线首通/章节奖励（掉落全走自己的发放链）
        if (BattleManager.Instance != null && BattleManager.Instance.IsBadgeDungeon)
            return;
        // 卷轴副本（2026-10-24 拍板）：同徽记本，通关不走主线首通/章推进/派定（掉落全走自己的发放链）
        if (BattleManager.Instance != null && BattleManager.Instance.IsScrollDungeon)
            return;
        // 礼物副本（2026-10-26 拍板）：同卷轴本，通关不走主线首通/章推进/派定（掉落全走自己的发放链）
        if (BattleManager.Instance != null && BattleManager.Instance.IsGiftDungeon)
            return;
        // 支线岛（2026-10-23 合并方案）：同金币本，岛通关不走主线首通/章推进/派定——
        // 全在 SideIslandBattleMode.SettleStageClear 自己的链里（发奖 + MarkSideIslandCleared + 派第二岛）
        if (BattleManager.Instance != null && BattleManager.Instance.IsSideIsland)
            return;
        // 终章追查关（2026-10-23 主人拍板「最后一关后剧情二选一」）：同支线岛，通关不走主线
        // 首通/章推进/派定——标记与关后剧情全在 FinaleBattleMode.SettleStageClear 自己的链里
        if (BattleManager.Instance != null && BattleManager.Instance.IsFinale)
            return;
        // 无限裂隙：不发主线首通石/章通关奖励，进度与奖励全走自己的结算链（SettleEndlessStageClear）
        if (BattleManager.Instance != null && BattleManager.Instance.IsEndlessRift)
            return;
        // 竞技场（2026-10-31 补拦）：单场挑战不推进主线/首通/章节。此前漏拦 → 打赢走统一门进了这里，
        // 胜场被 TryDailyClearTaskExp 算进「今日通关 3 关」→ 满 3 场发 50 经验（DAILY_CLEAR_TASK_EXP）
        // → 升级弹窗（主人报「竞技场是不是也给经验了」，查实就是这条链）。
        if (BattleManager.Instance != null && BattleManager.Instance.IsArena)
            return;

        // 先把刚打完的关卡记进抽取状态，下一关的概率要用到
        StageData cleared = GetCurrentStage();
        if (cleared != null) RollState.RecordCleared(cleared.type);

        // === 天赋石里程碑发放（2026-09-19）===
        // 首次通关按章发石；重复通关 clearedStages 已含该关 key，不再发。
        // 每日任务「通关 3 关」保底石也在每次真实通关后累计。
        var milestoneData = SaveSystem.Instance?.Data;
        if (milestoneData != null)
        {
            // C.1 每关首通 +3 钻：必须在 GrantTalentForStageFirstClear 写入 clearedStages 之前判定，否则永远不是首次
            // 【2026-10-20 主人拍板】钻石不再对玩家展示与发放，发钻行注释保留，恢复时解开即可
            // 【2026-10-31 B2 落地】key 从「大关内小关 0~4」改成「篇内小关下标 0..N-1」，
            // 否则第二座大关的 1-1 会与第一座大关的 1-1 撞 key（宝箱列/首通全乱）。
            int sub0 = CurrentSubIndex0;
            string stageClearKey = currentChapter + "_" + sub0;
            bool isStageFirstClear = milestoneData.clearedStages == null || !milestoneData.clearedStages.Contains(stageClearKey);

            // 【2026-10-20 主人拍板】每关首通天赋石停发（天赋石改为精英/Boss 击杀概率掉落）。
            // GrantTalentForStageFirstClear 只保留首通记录（clearedStages，冒险日志里程碑进度要用），返回的石不再发放。
            milestoneData.GrantTalentForStageFirstClear(currentChapter, sub0);
            // int stageTalent = milestoneData.GrantTalentForStageFirstClear(currentChapter, currentStageIndex);
            // if (stageTalent > 0)
            //     ResourceWallet.Add(ResourceWallet.ResourceType.TalentPoint, stageTalent, save: false, notify: true);
            // if (isStageFirstClear)
            //     ResourceWallet.Add(ResourceWallet.ResourceType.Diamond, 3, save: true, notify: true);

            int dailyExp = milestoneData.TryDailyClearTaskExp();
            if (dailyExp > 0)
            {
                LevelSystem.AddExpForTown(dailyExp);   // 2026-10-30 主人拍板：每日通关任务改发经验（原 5 天赋石停发，天赋石只随关卡出）
                // C.3 每日通关任务 +5 钻：与天赋石同处发放，判重逻辑（dailyClearTaskClaimDayKey）不改
                // ResourceWallet.Add(ResourceWallet.ResourceType.Diamond, 5, save: true, notify: true);
            }

            // 里程碑/每日石已写入内存（save:false），此处统一落盘，避免未存档前退出丢失。
            SaveSystem.Instance?.Save();
        }

        // 首领关可能提前出现在倒数三关里：打完首领这一章就结束
        bool bossCleared = cleared != null && cleared.type == StageType.Boss;
        // 【2026-10-31 B2 落地】一篇 = GrandCountOf(篇) 座大关（10/10/14/16/18/18/20/20）。
        // 打完一座大关只算「大关通关」，整篇的大关全部打完才走章节解锁/剧情/成就。
        if (bossCleared || currentStageIndex >= GameConfig.STAGES_PER_GRAND - 1)
        {
            var gData = SaveSystem.Instance?.Data;

            // 1. 增加通关次数（用于渐进式怪物解锁）——按「大关」记，与旧「一章=一座大关」口径一致
            IncrementChapterClearCount(currentChapter);
            if (gData != null) gData.MarkGrandCleared(currentChapter, currentGrand);

            int grandTotal = ActProgressionDefs.GrandCountOf(currentChapter);
            bool chapterDone = gData == null || gData.GrandClearedCount(currentChapter) >= grandTotal;

            if (!chapterDone)
            {
                // 大关通关、整篇没完 → 进度落到下一座大关第 1 小关，回冒险页继续（进本时 StartChapter 会再落位）
                currentGrand = Mathf.Min(currentGrand + 1, grandTotal);
                currentStageIndex = 0;
                availableNextStages.Clear();
                if (stageMap.Count > 0) availableNextStages.Add(stageMap[0]);
                SaveSystem.Instance?.Save();
                Debug.Log($"[ChapterManager] 第{currentChapter}篇 第{currentGrand - 1}/{grandTotal} 座大关通关 → 下一座第{currentGrand}座");
                OnBranchReady?.Invoke(availableNextStages);
                AchievementSystem.Instance?.OnReachStage(0);
                return;
            }

            // === 章节（整篇）通关逻辑 ===

            // 2. 解锁下一章：走 ChapterRouteTable（不再是简单的 +1）
            var data = SaveSystem.Instance?.Data;
            if (data != null)
            {
                // 整章通关额外天赋石：仅首次通关该章时发放一次
                bool firstChapterClear = !data.HasClearedChapter(currentChapter);

                int clearDiff = BattleManager.Instance != null ? BattleManager.Instance.BattleDifficulty : -1;
                data.MarkChapterCleared(currentChapter, clearDiff);
                // 【2026-10-24 主人拍板「难度逐章解锁」】按难度另记一笔：上一章该难度没通，下一章该难度灰锁。
                // clearDiff=-1（存档未就绪等异常）不记，宁缺勿滥（fail closed 由查询端把关节制）。
                if (clearDiff >= 0) data.MarkChapterClearedAtDiff(currentChapter, clearDiff);

                // 【2026-10-29 主人拍板改回】派岛门槛在 EnsureForkRoll 内部判「通关第 1 章后」（覆盖 2026-10-26「引导完成即派」）。
                // 这里保留 currentChapter == 1 外层护栏：第 8 章通关时 branchRoll 刚归零，无条件调会把下周目支线提前掷掉。
                if (currentChapter == 1) ChapterRouteTable.EnsureForkRoll(data);

                // 【2026-10-20 主人拍板】整章首通额外天赋石停发（天赋石改为精英/Boss 击杀概率掉落）。
                // int chapterTalent = TalentDefs.TalentStoneRewards.ChapterClearStone(currentChapter);
                // if (firstChapterClear && chapterTalent > 0)
                //     ResourceWallet.Add(ResourceWallet.ResourceType.TalentPoint, chapterTalent, save: false, notify: true);
                // C.2 每章首通 +30 钻：firstChapterClear 在 MarkChapterCleared 之前判定，顺序正确
                // 【2026-10-20 主人拍板】钻石停发，注释保留
                // if (firstChapterClear)
                //     ResourceWallet.Add(ResourceWallet.ResourceType.Diamond, 30, save: false, notify: true);
                var avail = ChapterRouteTable.AvailableChapters(data);
                if (avail.Count > 0)
                {
                    int far = avail[avail.Count - 1];
                if (far > data.maxUnlockedChapter)
                {
                    data.maxUnlockedChapter = far;
                    Debug.Log($"[ChapterManager] 解锁新区域：{GameConfig.GetChapterMapName(far)}（内部第{far}章）");
                    Analytics.ChapterReach(far); // 埋点：章节解锁
                }
                }
                SaveSystem.Instance.Save();
            }

            // 3. 触发事件
            OnChapterComplete?.Invoke();
            AchievementSystem.Instance?.OnChapterClear(currentChapter);
            if (currentChapter >= 1)
            {
                // 叙事 V2.0：按本章发放线索页。旧代码无论第几章都标 C1F，是误标。
                StoryClue.OnChapterBossCleared(currentChapter);
                if (currentChapter == 1)
                {
                    AdventureCodex.CompleteMain("C1F");
                    AdventureCodex.UnlockWorld("W004");
                    AdventureCodex.UnlockWorld("W002");
                }
            }
            int diff = BattleManager.Instance != null ? BattleManager.Instance.BattleDifficulty : 0;
            bool perfect = BattleManager.Instance == null
                           || BattleManager.Instance.RunStats.DamageTaken <= 0.5f;
            AdventureLogAchievements.OnChapterCleared(currentChapter, diff, perfect);
            return;
        }

        // 获取下一层可选关卡
        availableNextStages.Clear();
        foreach (int nextIdx in stageMap[currentStageIndex].nextStages)
        {
            if (nextIdx >= 0 && nextIdx < stageMap.Count)
                availableNextStages.Add(stageMap[nextIdx]);
        }
        // 兜底：无连线时按顺序进下一关，避免石墩/轮盘无数据
        if (availableNextStages.Count == 0 && currentStageIndex + 1 < stageMap.Count)
            availableNextStages.Add(stageMap[currentStageIndex + 1]);

        if (availableNextStages.Count == 1)
            RollNextStageType(availableNextStages[0]);

        OnBranchReady?.Invoke(availableNextStages);

        // 通知成就系统
        AchievementSystem.Instance?.OnReachStage(currentStageIndex);
    }

    /// <summary>
    /// 给下一关重新抽一次类型并写回 stageMap。
    /// 现在关卡类型由轮盘决定，AssignStageTypes 的预排只当兜底。
    /// </summary>
    public StageData RollNextStageType(StageData target = null)
    {
        StageData next = target ?? GetNextStage();
        if (next == null) return null;
        // 【2026-10-22 主人拍板】正式关类型按位置定死（v4 回合制）：0-based 4/9 → Boss、2/6 → 50% 精英、其余普通。
        // 【2026-10-23 批 B1】每局 10 关 → 每大关 5 小关：0-based 4 → Boss、2 → 50% 精英、其余普通。
        // 原权重抽签保留退役不删（恢复关闸已在）；引导局/金币本/裂隙不走本方法（金币本 StartGoldDungeon、
        // 裂隙 StartEndlessRift 各自建关，类型来源未波及）。
        // next.type = StageRoller.Roll(RollState, next.stageIndex, GameConfig.STAGES_PER_GRAND);
        next.type = StageRoller.RollByPosition(next.stageIndex);
        Debug.Log($"[ChapterManager] 定死下一关类型 第{next.stageIndex + 1}关 = {next.type}（v4 按位置，2026-10-23 批 B1 改 5 关制）"
                  + $"（恢复{RollState.restCount}/{StageRoller.MaxRestPerChapter} 战斗{RollState.combatStagesDone}）");
        return next;
    }

    /// <summary>玩家选定分支路线后，对该关抽类型。</summary>
    public StageData SelectBranchAndRoll(StageData stage)
    {
        if (stage == null) return null;
        return RollNextStageType(stage);
    }

    /// <summary>轮盘只给一条路：取分支里的第一个作为下一关</summary>
    public StageData GetNextStage()
    {
        if (availableNextStages != null && availableNextStages.Count > 0)
            return availableNextStages[0];
        int idx = currentStageIndex + 1;
        if (idx >= 0 && idx < stageMap.Count) return stageMap[idx];
        return null;
    }

    #endregion

    #region 通关追踪

    /// <summary>获取指定章节的通关次数</summary>
    public int GetChapterClearCount(int chapter)
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null || data.chapterClearCounts == null) return 0;
        var entry = data.chapterClearCounts.Find(e => e.chapter == chapter);
        return entry?.clearCount ?? 0;
    }

    /// <summary>增加指定章节的通关次数（章节通关时调用）</summary>
    public void IncrementChapterClearCount(int chapter)
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;

        var entry = data.chapterClearCounts.Find(e => e.chapter == chapter);
        if (entry != null)
            entry.clearCount++;
        else
            data.chapterClearCounts.Add(new ChapterClearCountEntry { chapter = chapter, clearCount = 1 });

        Debug.Log($"[ChapterManager] 第{chapter}章通关次数+1，当前={GetChapterClearCount(chapter)}");
    }

    #endregion

    #region 查询接口

    /// <summary>
    /// 设置当前章节（不生成关卡图，仅设置编号）
    /// </summary>
    public void SetChapter(int chapter)
    {
        currentChapter = chapter;
    }

    /// <summary>
    /// 获取当前关卡数据
    /// </summary>
    public StageData GetCurrentStage()
    {
        if (currentStageIndex >= 0 && currentStageIndex < stageMap.Count)
            return stageMap[currentStageIndex];
        return null;
    }

    /// <summary>
    /// 是否已通关当前章节
    /// </summary>
    public bool IsChapterCleared()
    {
        return currentStageIndex >= GameConfig.STAGES_PER_GRAND - 1;
    }

    /// <summary>
    /// 获取关卡显示名称 "1-3"
    /// </summary>
    public string GetStageDisplayName(int stageIndex)
    {
        return $"{currentChapter}-{stageIndex + 1}";
    }

    /// <summary>
    /// 获取当前关卡显示名称
    /// </summary>
    public string GetCurrentStageDisplayName()
    {
        return GetStageDisplayName(currentStageIndex);
    }

    #endregion
}

/// <summary>
/// 关卡数据
/// </summary>
public class StageData
{
    public int stageIndex;
    public StageType type;
    public List<int> nextStages;
    public List<EquipInstance> merchantGoodsInst; // 商人关商品实例
    public List<CurseBuff> curseOptions; // 诅咒关选项
}

/// <summary>
/// 诅咒关buff/debuff选项
/// </summary>
public class CurseBuff
{
    public string buffName;
    public AttrBonusData buff;
    public AttrBonusData debuff;
}
