using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

/// <summary>
/// 章节关卡管理器：每章最多10关，分支地图，特殊关卡唯一
///
/// 分支地图规则：
/// - 第1关是起点，第10关固定BOSS
/// - 有的关卡分2支（可以跳到下一个或下下个）
/// - 分支后2-3关内必汇合回主路
/// - 特殊关卡（商人/附魔/诅咒/休息）每章1-2个，随机分配，不重复
/// - 特殊关卡只在第2-8关之间
/// </summary>
public class ChapterManager : Singleton<ChapterManager>
{
    public int currentChapter = 1;
    public int currentStageIndex = 0;

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
        stageMap.Clear();
        availableNextStages.Clear();
        RollState.Reset();

        ChapterBranchTable.Reload();
        StageRollerWeightsTable.Reload();

        for (int i = 0; i < GameConfig.STAGES_PER_CHAPTER; i++)
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
        currentChapter = Mathf.Clamp(chapterNum, 1, 8);
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
        Debug.Log($"[ChapterManager] 金币副本 第{currentChapter}章 单场");
    }

    /// <summary>
    /// 分配关卡类型占位：第1关普通、末关 Boss；中间类型由 StageRoller 每关现抽覆盖。
    /// （旧版预排商人/诅咒等已被轮盘规则取代，避免双轨不一致。）
    /// </summary>
    void AssignStageTypes()
    {
        for (int i = 0; i < stageMap.Count; i++)
            stageMap[i].type = StageType.Normal;

        stageMap[0].type = StageType.Normal;
        stageMap[GameConfig.STAGES_PER_CHAPTER - 1].type = StageType.Boss;

        string log = "[ChapterManager] 关卡占位: ";
        for (int i = 0; i < stageMap.Count; i++)
            log += $"{i + 1}:{stageMap[i].type.ToString()[0]} ";
        Debug.Log(log);
    }

    /// <summary>
    /// 生成分支连接：一分二，有的分有的合
    /// </summary>
    void GenerateBranches()
    {
        for (int i = 0; i < stageMap.Count; i++)
            stageMap[i].nextStages.Clear();

        ChapterBranchTable.EnsureLoaded();
        var mainEdges = ChapterBranchTable.GetMainEdges(currentChapter);
        if (mainEdges.Count > 0)
        {
            for (int i = 0; i < mainEdges.Count; i++)
            {
                var e = mainEdges[i];
                if (e.fromIndex >= 0 && e.fromIndex < stageMap.Count
                    && e.toIndex >= 0 && e.toIndex < stageMap.Count
                    && !stageMap[e.fromIndex].nextStages.Contains(e.toIndex))
                    stageMap[e.fromIndex].nextStages.Add(e.toIndex);
            }
        }
        else
        {
            for (int i = 0; i < GameConfig.STAGES_PER_CHAPTER - 1; i++)
                stageMap[i].nextStages.Add(i + 1);
        }

        var fixedSkips = ChapterBranchTable.GetFixedSkipEdges(currentChapter);
        for (int i = 0; i < fixedSkips.Count; i++)
        {
            var e = fixedSkips[i];
            if (e.fromIndex >= 0 && e.fromIndex < stageMap.Count
                && e.toIndex >= 0 && e.toIndex < stageMap.Count
                && !stageMap[e.fromIndex].nextStages.Contains(e.toIndex))
                stageMap[e.fromIndex].nextStages.Add(e.toIndex);
        }

        ApplyRandomSkipBranches();

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
            if (skipTarget <= GameConfig.STAGES_PER_CHAPTER - 2
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
    /// 当前关卡通关，更新可选下一关
    /// </summary>
    public void OnStageComplete()
    {
        if (BattleManager.Instance != null && BattleManager.Instance.IsGoldDungeon)
            return;

        // 先把刚打完的关卡记进抽取状态，下一关的概率要用到
        StageData cleared = GetCurrentStage();
        if (cleared != null) RollState.RecordCleared(cleared.type);

        // 首领关可能提前出现在倒数三关里：打完首领这一章就结束
        bool bossCleared = cleared != null && cleared.type == StageType.Boss;
        if (bossCleared || currentStageIndex >= GameConfig.STAGES_PER_CHAPTER - 1)
        {
            // === 章节通关逻辑 ===

            // 1. 增加通关次数（用于渐进式怪物解锁）
            IncrementChapterClearCount(currentChapter);

            // 2. 解锁下一章
            var data = SaveSystem.Instance?.Data;
            if (data != null)
            {
                int nextChapter = currentChapter + 1;
                if (nextChapter > data.maxUnlockedChapter && nextChapter <= 8)
                {
                    data.maxUnlockedChapter = nextChapter;
                    Debug.Log($"[ChapterManager] 解锁第{nextChapter}章！");
                }
                SaveSystem.Instance.Save();
            }

            // 3. 触发事件
            OnChapterComplete?.Invoke();
            AchievementSystem.Instance?.OnChapterClear(currentChapter);
            if (currentChapter >= 1)
            {
                AdventureCodex.CompleteMain("C1F");
                AdventureCodex.UnlockWorld("W004");
                AdventureCodex.UnlockWorld("W002");
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
        next.type = StageRoller.Roll(RollState, next.stageIndex, GameConfig.STAGES_PER_CHAPTER);
        Debug.Log($"[ChapterManager] 抽到下一关 第{next.stageIndex + 1}关 = {next.type}"
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
        return currentStageIndex >= GameConfig.STAGES_PER_CHAPTER - 1;
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
