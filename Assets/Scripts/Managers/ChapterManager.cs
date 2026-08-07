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

        // 1. 创建10个节点
        for (int i = 0; i < GameConfig.STAGES_PER_CHAPTER; i++)
        {
            stageMap.Add(new StageData { stageIndex = i, nextStages = new List<int>() });
        }

        // 2. 分配关卡类型
        AssignStageTypes();

        // 3. 生成分支连接
        GenerateBranches();

        // 4. 初始可选第一关
        availableNextStages.Clear();
        availableNextStages.Add(stageMap[0]);

        Debug.Log($"[ChapterManager] 第{chapterNum}章关卡图生成完成，共{stageMap.Count}关");
    }

    /// <summary>
    /// 分配关卡类型：普通/精英/特殊/BOSS
    /// </summary>
    void AssignStageTypes()
    {
        // 第1关固定普通
        stageMap[0].type = StageType.Normal;

        // 第10关固定BOSS
        stageMap[GameConfig.STAGES_PER_CHAPTER - 1].type = StageType.Boss;

        // 可分配特殊关卡的索引池（第2-8关，index 1-7）
        List<int> specialPool = new List<int>();
        for (int i = 1; i <= 7; i++) specialPool.Add(i);

        // Fisher-Yates洗牌
        for (int i = specialPool.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            int temp = specialPool[i];
            specialPool[i] = specialPool[j];
            specialPool[j] = temp;
        }

        // 决定本章有几个特殊关卡（1-2个）
        int specialCount = UnityEngine.Random.Range(1, GameConfig.SPECIAL_STAGES_PER_CHAPTER + 1);

        // 特殊类型列表（不重复）
        List<StageType> specialTypes = new List<StageType>
        {
            StageType.Merchant,
            StageType.Enchant,
            StageType.Curse,
            StageType.Rest
        };

        // 随机打乱特殊类型
        for (int i = specialTypes.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            var temp = specialTypes[i];
            specialTypes[i] = specialTypes[j];
            specialTypes[j] = temp;
        }

        // 分配特殊关卡
        for (int i = 0; i < specialCount && i < specialPool.Count; i++)
        {
            int stageIdx = specialPool[i];
            stageMap[stageIdx].type = specialTypes[i];
        }

        // 剩余关卡按概率分配普通/精英
        for (int i = 1; i < GameConfig.STAGES_PER_CHAPTER - 1; i++)
        {
            if (stageMap[i].type != StageType.Normal) continue; // 已分配特殊关卡的跳过

            float roll = UnityEngine.Random.value;
            float eliteChance = 0.15f + i * 0.03f; // 越后面精英概率越高
            stageMap[i].type = roll < eliteChance ? StageType.Elite : StageType.Normal;
        }

        // 打印关卡图
        string log = "[ChapterManager] 关卡类型: ";
        for (int i = 0; i < stageMap.Count; i++)
        {
            log += $"{i + 1}:{stageMap[i].type.ToString()[0]} ";
        }
        Debug.Log(log);
    }

    /// <summary>
    /// 生成分支连接：一分二，有的分有的合
    /// </summary>
    void GenerateBranches()
    {
        // 基础连接：每个节点至少连到下一个
        for (int i = 0; i < GameConfig.STAGES_PER_CHAPTER - 1; i++)
        {
            stageMap[i].nextStages.Add(i + 1);
        }

        // 选1-2个分支点（index 1-5，确保分支后不会跳过BOSS）
        int branchCount = UnityEngine.Random.Range(1, 3);
        List<int> branchNodes = new List<int>();

        List<int> branchPool = new List<int>();
        for (int i = 1; i <= 5; i++) branchPool.Add(i);

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
            int skipTarget = nodeIdx + 2;

            // 确保不会连到BOSS之后，也不会跳过BOSS
            if (skipTarget <= GameConfig.STAGES_PER_CHAPTER - 2)
            {
                if (!stageMap[nodeIdx].nextStages.Contains(skipTarget))
                {
                    stageMap[nodeIdx].nextStages.Add(skipTarget);
                    branchNodes.Add(nodeIdx);
                }
            }
        }

        // 打印分支信息
        if (branchNodes.Count > 0)
        {
            string log = "[ChapterManager] 分支点: ";
            foreach (var n in branchNodes)
            {
                log += $"第{n + 1}关→[{string.Join(",", stageMap[n].nextStages.Select(x => (x + 1).ToString()))}] ";
            }
            Debug.Log(log);
        }
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
        if (currentStageIndex >= GameConfig.STAGES_PER_CHAPTER - 1)
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
            return;
        }

        // 获取下一层可选关卡
        availableNextStages.Clear();
        foreach (int nextIdx in stageMap[currentStageIndex].nextStages)
        {
            if (nextIdx >= 0 && nextIdx < stageMap.Count)
            {
                availableNextStages.Add(stageMap[nextIdx]);
            }
        }

        // 触发分支选择事件
        OnBranchReady?.Invoke(availableNextStages);

        // 通知成就系统
        AchievementSystem.Instance?.OnReachStage(currentStageIndex);
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
