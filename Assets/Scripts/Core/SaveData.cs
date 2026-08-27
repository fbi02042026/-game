using System;
using System.Collections.Generic;

/// <summary>JsonUtility 可序列化的 string→int 条目。</summary>
[Serializable]
public class StringIntEntry
{
    public string id;
    public int value;
}

/// <summary>JsonUtility 可序列化的 string id 条目。</summary>
[Serializable]
public class StringIdEntry
{
    public string id;
}

/// <summary>JsonUtility 可序列化的 int id 条目。</summary>
[Serializable]
public class IntIdEntry
{
    public int id;
}

[Serializable]
public class SaveData
{
    // === 货币 / 资源（统一上限见 ResourceWallet，体力有特殊上限）===
    public long totalGold = 0;
    public int talentPoints = 0;
    public int diamond = 0;
    public int enchantStones = 0;   // 附魔石
    public int decomposeMats = 0;   // 分解材料
    /// <summary>体力（特殊上限 GameConfig.STAMINA_MAX）</summary>
    public int stamina = 100;
    /// <summary>体力上次结算 Unix 秒（用于回复）</summary>
    public long lastStaminaUtc = 0;
    /// <summary>广告奖励日键 yyyyMMdd（UTC）</summary>
    public string adRewardDayKey = "";
    /// <summary>当日已领体力广告次数</summary>
    public int adStaminaClaimCount = 0;
    /// <summary>当日已领金币广告次数</summary>
    public int adGoldClaimCount = 0;
    /// <summary>邮件箱（资源溢出等）</summary>
    public List<MailEntry> mailInbox = new List<MailEntry>();

    // === 天赋（List 持久化；运行时用 Dictionary）===
    public List<StringIntEntry> talentEntries = new List<StringIntEntry>();
    [NonSerialized] public Dictionary<string, int> talents = new Dictionary<string, int>();

    // === 传说武器 ===
    public List<StringIdEntry> unlockedLegendaryWeaponEntries = new List<StringIdEntry>();
    [NonSerialized] public HashSet<string> unlockedLegendaryWeapons = new HashSet<string>();

    // === 遗产装备 ===
    public List<EquipmentData> legacyEquipPool = new List<EquipmentData>();

    // === 城镇等级 ===
    public TownLevel townLevel = new TownLevel();

    /// <summary>公会等级（影响高级佣兵解锁等）</summary>
    public int guildLevel = 1;

    // === 佣兵 ===
    public List<MercenaryData> permanentMercs = new List<MercenaryData>();
    /// <summary>今日已招募次数（按日刷新键见 dailyMercRecruitDayKey）</summary>
    public int dailyMercRecruitUsed = 0;
    public string dailyMercRecruitDayKey = "";

    // === 玩家基础属性 ===
    public int playerStrength = 0;     // 额外力量（天赋/遗产加成）
    public int playerIntelligence = 0; // 额外智力
    public int playerAgility = 0;      // 额外敏捷
    public int playerVitality = 0;     // 额外体质

    // === 成就系统 ===
    public List<StringIntEntry> achievementProgressEntries = new List<StringIntEntry>();
    [NonSerialized] public Dictionary<string, int> achievementProgress = new Dictionary<string, int>();
    public List<StringIdEntry> completedAchievementEntries = new List<StringIdEntry>();
    [NonSerialized] public HashSet<string> completedAchievements = new HashSet<string>();
    public int totalAchievementPoints = 0;
    public List<IntIdEntry> claimedMilestoneEntries = new List<IntIdEntry>();
    [NonSerialized] public HashSet<int> claimedMilestoneIds = new HashSet<int>();

    /// <summary>战前选择的玩家技能 id（PlayerSkillDefs）</summary>
    public string selectedPlayerSkillId = "heal_spring";

    public bool openingIntroPlayed;
    public bool tutorialIntroDone;
    public bool tutorialBattleCleared;
    public bool tutorialOutroPending;
    public bool tutorialDone;
    public bool chapter1IntroDone;
    public bool chapter1ChoiceDone;
    public List<NpcBondEntry> npcBonds = new List<NpcBondEntry>();
    public List<StoryChoiceEntry> storyChoices = new List<StoryChoiceEntry>();

    // === 章节进度 ===
    public int maxUnlockedChapter = 1; // 最大解锁章节
    /// <summary>章节通关次数列表（用于渐进式怪物解锁）</summary>
    public List<ChapterClearCountEntry> chapterClearCounts = new List<ChapterClearCountEntry>();

    // === 冒险日志图鉴 ===
    public List<StringIdEntry> seenMonsterEntries = new List<StringIdEntry>();
    public List<StringIdEntry> viewedMonsterEntries = new List<StringIdEntry>();
    public List<StringIdEntry> seenMercEntries = new List<StringIdEntry>();
    public List<StringIdEntry> viewedMercEntries = new List<StringIdEntry>();
    [NonSerialized] public HashSet<string> seenMonsterIds = new HashSet<string>();
    [NonSerialized] public HashSet<string> viewedMonsterIds = new HashSet<string>();
    [NonSerialized] public HashSet<string> seenMercIds = new HashSet<string>();
    [NonSerialized] public HashSet<string> viewedMercIds = new HashSet<string>();
    public List<StringIdEntry> claimedCodexRewardEntries = new List<StringIdEntry>();
    [NonSerialized] public HashSet<string> claimedCodexRewardIds = new HashSet<string>();

    /// <summary>已击败过的怪物图鉴 id（完整描述档）</summary>
    public List<StringIdEntry> defeatedMonsterEntries = new List<StringIdEntry>();
    [NonSerialized] public HashSet<string> defeatedMonsterIds = new HashSet<string>();

    // === 冒险日志里程（收集驱动，与战斗成就点数分离）===
    public int logMileagePoints = 0;
    public List<IntIdEntry> claimedLogMileageLevelEntries = new List<IntIdEntry>();
    [NonSerialized] public HashSet<int> claimedLogMileageLevels = new HashSet<int>();
    public List<StringIdEntry> logMileageGrantEntries = new List<StringIdEntry>();
    [NonSerialized] public HashSet<string> logMileageGrantedKeys = new HashSet<string>();
    /// <summary>里程称号等占位（Lv6）</summary>
    public string logMileageTitleId = "";

    public List<StringIdEntry> unlockedWorldEntries = new List<StringIdEntry>();
    public List<StringIdEntry> completedMainEntries = new List<StringIdEntry>();
    public List<StringIdEntry> completedSideEntries = new List<StringIdEntry>();
    [NonSerialized] public HashSet<string> unlockedWorldIds = new HashSet<string>();
    [NonSerialized] public HashSet<string> completedMainIds = new HashSet<string>();
    [NonSerialized] public HashSet<string> completedSideIds = new HashSet<string>();

    /// <summary>日志碎片数量（二期合成）</summary>
    public List<StringIntEntry> logFragmentEntries = new List<StringIntEntry>();
    [NonSerialized] public Dictionary<string, int> logFragments = new Dictionary<string, int>();

    // === 日志成就 A001–A015 ===
    public List<StringIdEntry> completedLogAchEntries = new List<StringIdEntry>();
    public List<StringIdEntry> claimedLogAchEntries = new List<StringIdEntry>();
    public List<StringIntEntry> logAchProgressEntries = new List<StringIntEntry>();
    [NonSerialized] public HashSet<string> completedLogAchIds = new HashSet<string>();
    [NonSerialized] public HashSet<string> claimedLogAchIds = new HashSet<string>();
    [NonSerialized] public Dictionary<string, int> logAchProgress = new Dictionary<string, int>();
    public List<StringIdEntry> unlockedTitleEntries = new List<StringIdEntry>();
    public List<StringIdEntry> unlockedFrameEntries = new List<StringIdEntry>();
    public List<StringIdEntry> unlockedSkinEntries = new List<StringIdEntry>();
    [NonSerialized] public HashSet<string> unlockedTitleIds = new HashSet<string>();
    [NonSerialized] public HashSet<string> unlockedFrameIds = new HashSet<string>();
    [NonSerialized] public HashSet<string> unlockedSkinIds = new HashSet<string>();
    public int backpackExtraSlots = 0;
    public int staminaBonusMax = 0;
    /// <summary>第一章已通关最高难度：-1 未通关，0 普通，1 困难，2 噩梦</summary>
    public int ch1BestClearDifficulty = -1;

    // === 日志碎片合成 / 里程商店（三期）===
    public List<StringIdEntry> craftedFragmentRecipeEntries = new List<StringIdEntry>();
    [NonSerialized] public HashSet<string> craftedFragmentRecipes = new HashSet<string>();
    public string mileageShopWeekKey = "";
    public List<StringIntEntry> mileageShopBuyEntries = new List<StringIntEntry>();
    [NonSerialized] public Dictionary<string, int> mileageShopBought = new Dictionary<string, int>();
    public int mercScrollCommon = 0;
    public int mercScrollRare = 0;
    public int mercScrollLegendary = 0;

    // === 时间戳 ===
    public long lastSaveTime = 0;

    /// <summary>JsonUtility 反序列化后调用：List → 运行时 Dictionary/HashSet。</summary>
    public void SyncRuntimeFromLists()
    {
        talentEntries ??= new List<StringIntEntry>();
        unlockedLegendaryWeaponEntries ??= new List<StringIdEntry>();
        achievementProgressEntries ??= new List<StringIntEntry>();
        completedAchievementEntries ??= new List<StringIdEntry>();
        claimedMilestoneEntries ??= new List<IntIdEntry>();
        mailInbox ??= new List<MailEntry>();
        legacyEquipPool ??= new List<EquipmentData>();
        permanentMercs ??= new List<MercenaryData>();
        npcBonds ??= new List<NpcBondEntry>();
        storyChoices ??= new List<StoryChoiceEntry>();
        chapterClearCounts ??= new List<ChapterClearCountEntry>();
        seenMonsterEntries ??= new List<StringIdEntry>();
        viewedMonsterEntries ??= new List<StringIdEntry>();
        seenMercEntries ??= new List<StringIdEntry>();
        viewedMercEntries ??= new List<StringIdEntry>();
        claimedCodexRewardEntries ??= new List<StringIdEntry>();
        defeatedMonsterEntries ??= new List<StringIdEntry>();
        claimedLogMileageLevelEntries ??= new List<IntIdEntry>();
        logMileageGrantEntries ??= new List<StringIdEntry>();
        unlockedWorldEntries ??= new List<StringIdEntry>();
        completedMainEntries ??= new List<StringIdEntry>();
        completedSideEntries ??= new List<StringIdEntry>();
        logFragmentEntries ??= new List<StringIntEntry>();
        completedLogAchEntries ??= new List<StringIdEntry>();
        claimedLogAchEntries ??= new List<StringIdEntry>();
        logAchProgressEntries ??= new List<StringIntEntry>();
        unlockedTitleEntries ??= new List<StringIdEntry>();
        unlockedFrameEntries ??= new List<StringIdEntry>();
        unlockedSkinEntries ??= new List<StringIdEntry>();
        craftedFragmentRecipeEntries ??= new List<StringIdEntry>();
        mileageShopBuyEntries ??= new List<StringIntEntry>();
        townLevel ??= new TownLevel();

        talents = new Dictionary<string, int>();
        for (int i = 0; i < talentEntries.Count; i++)
        {
            var e = talentEntries[i];
            if (e == null || string.IsNullOrEmpty(e.id)) continue;
            talents[e.id] = e.value;
        }

        unlockedLegendaryWeapons = new HashSet<string>();
        for (int i = 0; i < unlockedLegendaryWeaponEntries.Count; i++)
        {
            var e = unlockedLegendaryWeaponEntries[i];
            if (e == null || string.IsNullOrEmpty(e.id)) continue;
            unlockedLegendaryWeapons.Add(e.id);
        }

        achievementProgress = new Dictionary<string, int>();
        for (int i = 0; i < achievementProgressEntries.Count; i++)
        {
            var e = achievementProgressEntries[i];
            if (e == null || string.IsNullOrEmpty(e.id)) continue;
            achievementProgress[e.id] = e.value;
        }

        completedAchievements = new HashSet<string>();
        for (int i = 0; i < completedAchievementEntries.Count; i++)
        {
            var e = completedAchievementEntries[i];
            if (e == null || string.IsNullOrEmpty(e.id)) continue;
            completedAchievements.Add(e.id);
        }

        claimedMilestoneIds = new HashSet<int>();
        for (int i = 0; i < claimedMilestoneEntries.Count; i++)
        {
            var e = claimedMilestoneEntries[i];
            if (e == null) continue;
            claimedMilestoneIds.Add(e.id);
        }

        seenMonsterIds = ToIdSet(seenMonsterEntries);
        viewedMonsterIds = ToIdSet(viewedMonsterEntries);
        seenMercIds = ToIdSet(seenMercEntries);
        viewedMercIds = ToIdSet(viewedMercEntries);
        claimedCodexRewardIds = ToIdSet(claimedCodexRewardEntries);
        defeatedMonsterIds = ToIdSet(defeatedMonsterEntries);
        unlockedWorldIds = ToIdSet(unlockedWorldEntries);
        completedMainIds = ToIdSet(completedMainEntries);
        completedSideIds = ToIdSet(completedSideEntries);

        claimedLogMileageLevels = new HashSet<int>();
        for (int i = 0; i < claimedLogMileageLevelEntries.Count; i++)
        {
            var e = claimedLogMileageLevelEntries[i];
            if (e == null) continue;
            claimedLogMileageLevels.Add(e.id);
        }
        logMileageGrantedKeys = ToIdSet(logMileageGrantEntries);

        logFragments = new Dictionary<string, int>();
        for (int i = 0; i < logFragmentEntries.Count; i++)
        {
            var e = logFragmentEntries[i];
            if (e == null || string.IsNullOrEmpty(e.id)) continue;
            logFragments[e.id] = e.value;
        }

        completedLogAchIds = ToIdSet(completedLogAchEntries);
        claimedLogAchIds = ToIdSet(claimedLogAchEntries);
        unlockedTitleIds = ToIdSet(unlockedTitleEntries);
        unlockedFrameIds = ToIdSet(unlockedFrameEntries);
        unlockedSkinIds = ToIdSet(unlockedSkinEntries);
        craftedFragmentRecipes = ToIdSet(craftedFragmentRecipeEntries);
        mileageShopBought = new Dictionary<string, int>();
        for (int i = 0; i < mileageShopBuyEntries.Count; i++)
        {
            var e = mileageShopBuyEntries[i];
            if (e == null || string.IsNullOrEmpty(e.id)) continue;
            mileageShopBought[e.id] = e.value;
        }
        logAchProgress = new Dictionary<string, int>();
        for (int i = 0; i < logAchProgressEntries.Count; i++)
        {
            var e = logAchProgressEntries[i];
            if (e == null || string.IsNullOrEmpty(e.id)) continue;
            logAchProgress[e.id] = e.value;
        }
    }

    static HashSet<string> ToIdSet(List<StringIdEntry> list)
    {
        var set = new HashSet<string>();
        if (list == null) return set;
        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i];
            if (e == null || string.IsNullOrEmpty(e.id)) continue;
            set.Add(e.id);
        }
        return set;
    }

    /// <summary>写入 JSON 前调用：运行时 Dictionary/HashSet → List。</summary>
    public void SyncListsFromRuntime()
    {
        talents ??= new Dictionary<string, int>();
        unlockedLegendaryWeapons ??= new HashSet<string>();
        achievementProgress ??= new Dictionary<string, int>();
        completedAchievements ??= new HashSet<string>();
        claimedMilestoneIds ??= new HashSet<int>();
        seenMonsterIds ??= new HashSet<string>();
        viewedMonsterIds ??= new HashSet<string>();
        seenMercIds ??= new HashSet<string>();
        viewedMercIds ??= new HashSet<string>();
        claimedCodexRewardIds ??= new HashSet<string>();
        defeatedMonsterIds ??= new HashSet<string>();
        claimedLogMileageLevels ??= new HashSet<int>();
        logMileageGrantedKeys ??= new HashSet<string>();
        unlockedWorldIds ??= new HashSet<string>();
        completedMainIds ??= new HashSet<string>();
        completedSideIds ??= new HashSet<string>();
        logFragments ??= new Dictionary<string, int>();
        completedLogAchIds ??= new HashSet<string>();
        claimedLogAchIds ??= new HashSet<string>();
        logAchProgress ??= new Dictionary<string, int>();
        unlockedTitleIds ??= new HashSet<string>();
        unlockedFrameIds ??= new HashSet<string>();
        unlockedSkinIds ??= new HashSet<string>();
        craftedFragmentRecipes ??= new HashSet<string>();
        mileageShopBought ??= new Dictionary<string, int>();

        talentEntries = new List<StringIntEntry>(talents.Count);
        foreach (var kv in talents)
            talentEntries.Add(new StringIntEntry { id = kv.Key, value = kv.Value });

        unlockedLegendaryWeaponEntries = new List<StringIdEntry>(unlockedLegendaryWeapons.Count);
        foreach (string id in unlockedLegendaryWeapons)
            unlockedLegendaryWeaponEntries.Add(new StringIdEntry { id = id });

        achievementProgressEntries = new List<StringIntEntry>(achievementProgress.Count);
        foreach (var kv in achievementProgress)
            achievementProgressEntries.Add(new StringIntEntry { id = kv.Key, value = kv.Value });

        completedAchievementEntries = new List<StringIdEntry>(completedAchievements.Count);
        foreach (string id in completedAchievements)
            completedAchievementEntries.Add(new StringIdEntry { id = id });

        claimedMilestoneEntries = new List<IntIdEntry>(claimedMilestoneIds.Count);
        foreach (int id in claimedMilestoneIds)
            claimedMilestoneEntries.Add(new IntIdEntry { id = id });

        seenMonsterEntries = FromIdSet(seenMonsterIds);
        viewedMonsterEntries = FromIdSet(viewedMonsterIds);
        seenMercEntries = FromIdSet(seenMercIds);
        viewedMercEntries = FromIdSet(viewedMercIds);
        claimedCodexRewardEntries = FromIdSet(claimedCodexRewardIds);
        defeatedMonsterEntries = FromIdSet(defeatedMonsterIds);
        unlockedWorldEntries = FromIdSet(unlockedWorldIds);
        completedMainEntries = FromIdSet(completedMainIds);
        completedSideEntries = FromIdSet(completedSideIds);
        logMileageGrantEntries = FromIdSet(logMileageGrantedKeys);

        claimedLogMileageLevelEntries = new List<IntIdEntry>(claimedLogMileageLevels.Count);
        foreach (int id in claimedLogMileageLevels)
            claimedLogMileageLevelEntries.Add(new IntIdEntry { id = id });

        logFragmentEntries = new List<StringIntEntry>(logFragments.Count);
        foreach (var kv in logFragments)
            logFragmentEntries.Add(new StringIntEntry { id = kv.Key, value = kv.Value });

        completedLogAchEntries = FromIdSet(completedLogAchIds);
        claimedLogAchEntries = FromIdSet(claimedLogAchIds);
        unlockedTitleEntries = FromIdSet(unlockedTitleIds);
        unlockedFrameEntries = FromIdSet(unlockedFrameIds);
        unlockedSkinEntries = FromIdSet(unlockedSkinIds);
        craftedFragmentRecipeEntries = FromIdSet(craftedFragmentRecipes);
        mileageShopBuyEntries = new List<StringIntEntry>(mileageShopBought.Count);
        foreach (var kv in mileageShopBought)
            mileageShopBuyEntries.Add(new StringIntEntry { id = kv.Key, value = kv.Value });
        logAchProgressEntries = new List<StringIntEntry>(logAchProgress.Count);
        foreach (var kv in logAchProgress)
            logAchProgressEntries.Add(new StringIntEntry { id = kv.Key, value = kv.Value });
    }

    static List<StringIdEntry> FromIdSet(HashSet<string> set)
    {
        var list = new List<StringIdEntry>(set != null ? set.Count : 0);
        if (set == null) return list;
        foreach (string id in set)
            list.Add(new StringIdEntry { id = id });
        return list;
    }
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
    /// <summary>形象/预制体模板 ID（如 gongshou101），可重复招募同一模板</summary>
    public string mercId;
    /// <summary>展示姓名（同形象可不同名）</summary>
    public string displayName;
    /// <summary>实例唯一 ID，名册可有多条同 mercId</summary>
    public string uid;
    public int favorLevel;
    public int level;
    /// <summary>星级 1～5</summary>
    public int star = 1;
    /// <summary>佩戴技能（Ally 技能 id，如 ally_heal）</summary>
    public string skillId;
}
