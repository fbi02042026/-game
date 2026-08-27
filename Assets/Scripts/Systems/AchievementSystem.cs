using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 成就系统：所有成就进度永久保存，多局累计
/// 成就类型：击杀、副本通关、装备收集、遗产收集、生存
/// 完成后给奖励+成就点数，点数兑换里程奖励
/// </summary>
public class AchievementSystem : Singleton<AchievementSystem>
{
    /// <summary>
    /// 成就进度更新事件 (achievementId, currentProgress, targetProgress)
    /// </summary>
    public event Action<string, int, int> OnProgressUpdated;

    /// <summary>
    /// 成就完成事件 (achievementId, reward)
    /// </summary>
    public event Action<string, AchievementReward> OnAchievementCompleted;

    /// <summary>
    /// 里程奖励领取事件 (milestoneId)
    /// </summary>
    public event Action<int> OnMilestoneClaimed;

    /// <summary>
    /// 成就定义表（只读配置，运行时不变）
    /// </summary>
    private Dictionary<string, AchievementDef> _achievementDefs;

    /// <summary>
    /// 里程奖励定义表
    /// </summary>
    private List<MilestoneDef> _milestoneDefs;

    protected override void Awake()
    {
        base.Awake();
        InitAchievementDefs();
        InitMilestoneDefs();
    }

    #region 成就定义

    void InitAchievementDefs()
    {
        _achievementDefs = new Dictionary<string, AchievementDef>
        {
            // === 击杀成就 ===
            { "kill_ch1_boss_1", new AchievementDef("kill_ch1_boss_1", "首次击杀", "击杀第1章BOSS 1次", AchievementType.Kill, 1, new AchievementReward(100, 0, 10)) },
            { "kill_ch1_boss_5", new AchievementDef("kill_ch1_boss_5", "熟练猎手", "击杀第1章BOSS 5次", AchievementType.Kill, 5, new AchievementReward(300, 0, 20)) },
            { "kill_ch1_boss_10", new AchievementDef("kill_ch1_boss_10", "章节主宰", "击杀第1章BOSS 10次", AchievementType.Kill, 10, new AchievementReward(500, 0, 30)) },
            { "kill_ch1_boss_20", new AchievementDef("kill_ch1_boss_20", "终极猎杀", "击杀第1章BOSS 20次", AchievementType.Kill, 20, new AchievementReward(1000, 1, 50)) },

            { "kill_total_100", new AchievementDef("kill_total_100", "百人斩", "累计击杀100只怪物", AchievementType.Kill, 100, new AchievementReward(200, 0, 15)) },
            { "kill_total_500", new AchievementDef("kill_total_500", "千人斩", "累计击杀500只怪物", AchievementType.Kill, 500, new AchievementReward(500, 0, 30)) },
            { "kill_total_1000", new AchievementDef("kill_total_1000", "万人斩", "累计击杀1000只怪物", AchievementType.Kill, 1000, new AchievementReward(1000, 2, 50)) },

            // === 副本成就 ===
            { "clear_ch1", new AchievementDef("clear_ch1", "初出茅庐", "通关第1章", AchievementType.Dungeon, 1, new AchievementReward(200, 1, 20)) },
            { "clear_ch3", new AchievementDef("clear_ch3", "渐入佳境", "通关第3章", AchievementType.Dungeon, 1, new AchievementReward(500, 0, 30)) },
            { "clear_ch5", new AchievementDef("clear_ch5", "冒险大师", "通关第5章", AchievementType.Dungeon, 1, new AchievementReward(1000, 2, 50)) },
            { "clear_ch8", new AchievementDef("clear_ch8", "传奇冒险家", "通关第8章", AchievementType.Dungeon, 1, new AchievementReward(2000, 5, 100)) },

            // === 装备收集 ===
            { "equip_first_orange", new AchievementDef("equip_first_orange", "传说之始", "首次获得橙色品质武器", AchievementType.EquipCollect, 1, new AchievementReward(500, 2, 50)) },
            { "equip_collect_10", new AchievementDef("equip_collect_10", "装备爱好者", "累计收集10件武器", AchievementType.EquipCollect, 10, new AchievementReward(200, 0, 15)) },
            { "equip_collect_20", new AchievementDef("equip_collect_20", "武器大师", "累计收集20件武器", AchievementType.EquipCollect, 20, new AchievementReward(500, 0, 30)) },
            { "equip_collect_50", new AchievementDef("equip_collect_50", "军械库", "累计收集50件武器", AchievementType.EquipCollect, 50, new AchievementReward(1000, 3, 60)) },

            // === 遗产收集 ===
            { "legacy_1", new AchievementDef("legacy_1", "薪火相传", "带回1件遗产", AchievementType.Legacy, 1, new AchievementReward(100, 0, 10)) },
            { "legacy_5", new AchievementDef("legacy_5", "积累传承", "带回5件遗产", AchievementType.Legacy, 5, new AchievementReward(300, 0, 20)) },
            { "legacy_10", new AchievementDef("legacy_10", "遗产收藏家", "带回10件遗产", AchievementType.Legacy, 10, new AchievementReward(500, 1, 30)) },
            { "legacy_20", new AchievementDef("legacy_20", "不朽传承", "带回20件遗产", AchievementType.Legacy, 20, new AchievementReward(1000, 2, 50)) },

            // === 生存成就 ===
            { "survive_stage5", new AchievementDef("survive_stage5", "深入险境", "单局到达第5关", AchievementType.Survive, 1, new AchievementReward(100, 0, 10)) },
            { "survive_stage8", new AchievementDef("survive_stage8", "极限求生", "单局到达第8关", AchievementType.Survive, 1, new AchievementReward(300, 0, 20)) },
            { "survive_clear", new AchievementDef("survive_clear", "完美通关", "单局通关一章", AchievementType.Survive, 1, new AchievementReward(500, 1, 30)) },
        };
    }

    void InitMilestoneDefs()
    {
        _milestoneDefs = new List<MilestoneDef>
        {
            new MilestoneDef(1, 50, new AchievementReward(200, 1, 0), "里程 I"),
            new MilestoneDef(2, 100, new AchievementReward(500, 2, 0), "里程 II"),
            new MilestoneDef(3, 200, new AchievementReward(1000, 3, 0), "里程 III"),
            new MilestoneDef(4, 500, new AchievementReward(2000, 5, 0), "里程 IV"),
            new MilestoneDef(5, 1000, new AchievementReward(5000, 10, 0), "里程 V"),
        };
    }

    #endregion

    #region 进度更新

    /// <summary>
    /// 更新成就进度（自动累加）
    /// </summary>
    public void AddProgress(string achievementId, int add = 1)
    {
        if (!_achievementDefs.TryGetValue(achievementId, out var def))
        {
            Debug.LogWarning($"[AchievementSystem] 成就ID不存在: {achievementId}");
            return;
        }

        var data = SaveSystem.Instance.Data;

        // 已完成的不重复累计（除了可以进阶的成就）
        if (data.completedAchievements.Contains(achievementId) && !def.canProgressAfterComplete)
            return;

        // 获取当前进度
        if (!data.achievementProgress.TryGetValue(achievementId, out int current))
            current = 0;

        int newProgress = current + add;
        data.achievementProgress[achievementId] = newProgress;

        OnProgressUpdated?.Invoke(achievementId, newProgress, def.targetProgress);

        // 检查是否完成
        if (newProgress >= def.targetProgress && !data.completedAchievements.Contains(achievementId))
        {
            CompleteAchievement(achievementId, def);
        }

        SaveSystem.Instance.Save();
    }

    /// <summary>
    /// 设置成就进度（覆盖）
    /// </summary>
    public void SetProgress(string achievementId, int value)
    {
        if (!_achievementDefs.TryGetValue(achievementId, out var def))
            return;

        var data = SaveSystem.Instance.Data;
        data.achievementProgress[achievementId] = value;

        OnProgressUpdated?.Invoke(achievementId, value, def.targetProgress);

        if (value >= def.targetProgress && !data.completedAchievements.Contains(achievementId))
        {
            CompleteAchievement(achievementId, def);
        }

        SaveSystem.Instance.Save();
    }

    /// <summary>
    /// 直接完成成就（不检查进度）
    /// </summary>
    public void ForceComplete(string achievementId)
    {
        if (!_achievementDefs.TryGetValue(achievementId, out var def))
            return;

        var data = SaveSystem.Instance.Data;
        data.achievementProgress[achievementId] = def.targetProgress;

        if (!data.completedAchievements.Contains(achievementId))
        {
            CompleteAchievement(achievementId, def);
        }
    }

    void CompleteAchievement(string achievementId, AchievementDef def)
    {
        var data = SaveSystem.Instance.Data;
        data.completedAchievements.Add(achievementId);
        data.totalAchievementPoints += def.reward.achievementPoints;

        // 发放奖励（走上限；溢出进邮件）
        if (def.reward.gold > 0)
            ResourceWallet.Add(ResourceWallet.ResourceType.Gold, def.reward.gold, save: false, notify: true);
        if (def.reward.diamond > 0)
            ResourceWallet.Add(ResourceWallet.ResourceType.Diamond, def.reward.diamond, save: false, notify: true);
        if (def.reward.talentPoints > 0)
            ResourceWallet.Add(ResourceWallet.ResourceType.TalentPoint, def.reward.talentPoints, save: false, notify: true);
        SaveSystem.Instance.Save();

        // 折算进日志里程（防重）；战斗成就点数仍保留供旧查询
        AdventureLogMileage.GrantAchievement(achievementId, def.reward.achievementPoints);

        OnAchievementCompleted?.Invoke(achievementId, def.reward);
        RedDot.RefreshCommon();

        Debug.Log($"[AchievementSystem] 成就完成: {def.name} (+{def.reward.achievementPoints}点)");
    }

    #endregion

    #region 查询接口

    /// <summary>
    /// 获取成就当前进度
    /// </summary>
    public int GetProgress(string achievementId)
    {
        var data = SaveSystem.Instance.Data;
        return data.achievementProgress.TryGetValue(achievementId, out int val) ? val : 0;
    }

    /// <summary>
    /// 是否已完成
    /// </summary>
    public bool IsCompleted(string achievementId)
    {
        return SaveSystem.Instance.Data.completedAchievements.Contains(achievementId);
    }

    /// <summary>
    /// 获取成就定义
    /// </summary>
    public AchievementDef GetDef(string achievementId)
    {
        _achievementDefs.TryGetValue(achievementId, out var def);
        return def;
    }

    /// <summary>
    /// 获取所有成就定义
    /// </summary>
    public Dictionary<string, AchievementDef> GetAllDefs() => _achievementDefs;

    /// <summary>
    /// 获取总成就点数
    /// </summary>
    public int GetTotalPoints() => SaveSystem.Instance.Data.totalAchievementPoints;

    #endregion

    #region 里程奖励

    /// <summary>
    /// 获取所有里程奖励定义
    /// </summary>
    public List<MilestoneDef> GetAllMilestones() => _milestoneDefs;

    /// <summary>是否可领日志里程等级（id 对应 Lv，兼容旧调用）。</summary>
    public bool CanClaimMilestone(int milestoneId) => AdventureLogMileage.CanClaimLevel(milestoneId);

    /// <summary>领取日志里程等级奖励。</summary>
    public bool ClaimMilestone(int milestoneId)
    {
        if (!AdventureLogMileage.ClaimLevel(milestoneId)) return false;
        OnMilestoneClaimed?.Invoke(milestoneId);
        return true;
    }

    /// <summary>是否有未领取的日志里程等级奖励。</summary>
    public bool HasUnclaimedMilestone() => AdventureLogMileage.HasUnclaimedLevel();

    /// <summary>是否已领取该里程等级。</summary>
    public bool IsMilestoneClaimed(int milestoneId) => AdventureLogMileage.IsLevelClaimed(milestoneId);

    #endregion

    #region 便捷触发方法

    /// <summary>
    /// 击杀怪物时调用
    /// </summary>
    public void OnKillMonster(int chapter, bool isBoss)
    {
        AddProgress("kill_total_100");
        AddProgress("kill_total_500");
        AddProgress("kill_total_1000");

        if (isBoss && chapter == 1)
        {
            AddProgress("kill_ch1_boss_1");
            AddProgress("kill_ch1_boss_5");
            AddProgress("kill_ch1_boss_10");
            AddProgress("kill_ch1_boss_20");
        }
    }

    /// <summary>
    /// 通关章节时调用
    /// </summary>
    public void OnChapterClear(int chapter)
    {
        if (chapter >= 1) AddProgress("clear_ch1");
        if (chapter >= 3) AddProgress("clear_ch3");
        if (chapter >= 5) AddProgress("clear_ch5");
        if (chapter >= 8) AddProgress("clear_ch8");

        AddProgress("survive_clear");
    }

    /// <summary>
    /// 到达某关时调用
    /// </summary>
    public void OnReachStage(int stageIndex)
    {
        if (stageIndex >= 4) AddProgress("survive_stage5"); // 第5关index=4
        if (stageIndex >= 7) AddProgress("survive_stage8"); // 第8关index=7
    }

    /// <summary>
    /// 获得装备时调用
    /// </summary>
    public void OnObtainEquip(Rarity rarity)
    {
        AddProgress("equip_collect_10");
        AddProgress("equip_collect_20");
        AddProgress("equip_collect_50");

        if (rarity == Rarity.Legendary)
        {
            AddProgress("equip_first_orange");
        }
    }

    /// <summary>
    /// 带回遗产时调用
    /// </summary>
    public void OnBringLegacy()
    {
        AddProgress("legacy_1");
        AddProgress("legacy_5");
        AddProgress("legacy_10");
        AddProgress("legacy_20");
    }

    #endregion
}

#region 数据定义

public enum AchievementType
{
    Kill,           // 击杀
    Dungeon,        // 副本通关
    EquipCollect,   // 装备收集
    Legacy,         // 遗产收集
    Survive         // 生存
}

/// <summary>
/// 成就定义
/// </summary>
public class AchievementDef
{
    public string id;
    public string name;
    public string desc;
    public AchievementType type;
    public int targetProgress;
    public AchievementReward reward;
    public bool canProgressAfterComplete; // 完成后是否继续累计（用于进阶型成就）

    public AchievementDef(string id, string name, string desc, AchievementType type, int target, AchievementReward reward, bool canProgress = false)
    {
        this.id = id;
        this.name = name;
        this.desc = desc;
        this.type = type;
        this.targetProgress = target;
        this.reward = reward;
        this.canProgressAfterComplete = canProgress;
    }
}

/// <summary>
/// 成就奖励
/// </summary>
public class AchievementReward
{
    public int gold;
    public int diamond;
    public int talentPoints;
    public int achievementPoints; // 成就点数（用于里程）

    public AchievementReward(int gold, int diamond, int points, int talent = 0)
    {
        this.gold = gold;
        this.diamond = diamond;
        this.achievementPoints = points;
        this.talentPoints = talent;
    }
}

/// <summary>
/// 里程奖励定义
/// </summary>
public class MilestoneDef
{
    public int id;
    public int requiredPoints; // 需要的成就点数
    public AchievementReward reward;
    public string name;

    public MilestoneDef(int id, int requiredPoints, AchievementReward reward, string name)
    {
        this.id = id;
        this.requiredPoints = requiredPoints;
        this.reward = reward;
        this.name = name;
    }
}

#endregion
