using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    // === 货币 ===
    public long totalGold = 0;
    public int talentPoints = 0;
    public int diamond = 0;
    public int enchantStones = 0;   // 附魔石
    public int decomposeMats = 0;   // 分解材料

    // === 天赋 ===
    public Dictionary<string, int> talents = new Dictionary<string, int>();

    // === 传说武器 ===
    public HashSet<string> unlockedLegendaryWeapons = new HashSet<string>();

    // === 遗产装备 ===
    public List<EquipmentData> legacyEquipPool = new List<EquipmentData>();

    // === 城镇等级 ===
    public TownLevel townLevel = new TownLevel();

    /// <summary>公会等级（影响高级佣兵解锁等）</summary>
    public int guildLevel = 1;

    // === 佣兵 ===
    public List<MercenaryData> permanentMercs = new List<MercenaryData>();

    // === 玩家基础属性 ===
    public int playerStrength = 0;     // 额外力量（天赋/遗产加成）
    public int playerIntelligence = 0; // 额外智力
    public int playerAgility = 0;      // 额外敏捷
    public int playerVitality = 0;     // 额外体质

    // === 成就系统 ===
    public Dictionary<string, int> achievementProgress = new Dictionary<string, int>(); // 成就ID → 当前进度值
    public HashSet<string> completedAchievements = new HashSet<string>(); // 已完成的成就ID（用于里程累计）
    public int totalAchievementPoints = 0; // 总成就点数
    public HashSet<int> claimedMilestoneIds = new HashSet<int>(); // 已领取的里程奖励ID

    // === 章节进度 ===
    public int maxUnlockedChapter = 1; // 最大解锁章节
    /// <summary>章节通关次数列表（用于渐进式怪物解锁）</summary>
    public List<ChapterClearCountEntry> chapterClearCounts = new List<ChapterClearCountEntry>();

    // === 时间戳 ===
    public long lastSaveTime = 0;
}

[Serializable]
public class ChapterClearCountEntry
{
    public int chapter;
    public int clearCount;
}

[Serializable]
public class TownLevel
{
    public int blacksmith = 1;
    public int tavern = 1;
    public int altar = 1;
    public int farm = 1;
}

[Serializable]
public class EquipmentData
{
    public string equipId;
    public int rarity;
    public int star;
    public int requireLevel;
    public List<AttrBonusData> attrBonus = new List<AttrBonusData>();
    public List<string> tags = new List<string>();
    public bool isLegacy;
}

[Serializable]
public class AttrBonusData
{
    public AttrType attrType;
    public float value;
    public bool isPercent;
}

[Serializable]
public class MercenaryData
{
    public string mercId;
    public int favorLevel;
    public int level;
}