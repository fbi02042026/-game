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

    // === 玩家技能解锁（2026-09-15）===
    // 只记录「非章节途径」解锁的技能 id：天赋树 / 商店购买 / 成就奖励。
    // 章节解锁是动态判定（读 clearedChapterIds），不写进来，避免通关回退后状态不一致。
    public List<StringIdEntry> unlockedSkillEntries = new List<StringIdEntry>();
    [NonSerialized] public HashSet<string> unlockedSkills = new HashSet<string>();

    // === 商店每日限购（2026-09-15）===
    // 记「商品 id → 当天已买次数」，配合 shopPurchaseDay 跨天清零。
    // 没有限购的话，体力药水和金币袋会被无限搬空，通胀只是时间问题。
    public List<StringIntEntry> shopPurchaseEntries = new List<StringIntEntry>();
    [NonSerialized] public Dictionary<string, int> shopPurchases = new Dictionary<string, int>();
    /// <summary>yyyyMMdd，与今天不同则清空 shopPurchases。</summary>
    public string shopPurchaseDay = "";

    // === 技能碎片（2026-09-15）===
    // 「技能 id → 该技能碎片数」。史诗/传说技能靠攒碎片合成；
    // 抽卡抽到「已解锁」的技能也转成这里的碎片，让抽卡在后期仍有意义。
    public List<StringIntEntry> skillFragmentEntries = new List<StringIntEntry>();
    [NonSerialized] public Dictionary<string, int> skillFragments = new Dictionary<string, int>();

    // === 登录奖励（2026-09-15）===
    // 用一张 StringInt 表存所有标记，避免为几个 int 反复加字段：
    //   "days"   = 累计登录自然日数
    //   "cycle"  = 每日循环已领到第几个
    //   "s{N}"   = 新手第 N 天已领（1）
    public List<StringIntEntry> loginFlagEntries = new List<StringIntEntry>();
    [NonSerialized] public Dictionary<string, int> loginFlags = new Dictionary<string, int>();
    /// <summary>上次登录的 yyyyMMdd。</summary>
    public string loginLastDay = "";
    /// <summary>上次领取每日循环奖励的 yyyyMMdd。</summary>
    public string loginCycleDay = "";

    // === 遗产装备 ===
    public List<EquipmentData> legacyEquipPool = new List<EquipmentData>();

    // === 城镇等级 ===
    public TownLevel townLevel = new TownLevel();

    /// <summary>公会等级（影响高级佣兵解锁等）</summary>
    public int guildLevel = 1;

    // === 佣兵 ===
    /// <summary>历史兼容 / 教程写入；出战以 hiredMercs 为准。图鉴走 seenMerc。</summary>
    public List<MercenaryData> permanentMercs = new List<MercenaryData>();
    /// <summary>本局临时雇佣（下本结束清空）</summary>
    public List<MercenaryData> hiredMercs = new List<MercenaryData>();
    /// <summary>上一趟出战佣兵 hireId（酒馆刷出互动句）。</summary>
    public List<string> lastRunMercHireIds = new List<string>();
    /// <summary>今日已招募次数（按日刷新键见 dailyMercRecruitDayKey）</summary>
    public int dailyMercRecruitUsed = 0;
    public string dailyMercRecruitDayKey = "";
    /// <summary>酒馆三选一候选日键（本地日历日，0 点刷新）</summary>
    public string mercOfferDayKey = "";
    /// <summary>上次手动/自动刷新 Unix 秒（UTC）</summary>
    public long mercOfferRefreshUtc = 0;
    /// <summary>日切后需重抽候选</summary>
    public bool mercOfferDirty = true;

    // === 玩家基础属性 ===
    /// <summary>玩家冒险者显示名（引导签名后设定）</summary>
    public string playerDisplayName = "";
    public bool playerNameChosen;
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

    /// <summary>进战所选玩家职业（PlayerJobId）</summary>
    public int selectedPlayerJobId = 0;

    /// <summary>隐藏经验等级（不对玩家展示；驱动掉落权重）</summary>
    public int hiddenLevel = 1;
    /// <summary>当前隐藏等级内已积累经验</summary>
    public int hiddenExp = 0;

    /// <summary>佣兵主动技释放：0=手动，1=自动（强制自动，UI 已隐藏）</summary>
    public int mercSkillCastMode = 1;

    /// <summary>佣兵技能 ID 规范版本。&lt; 202609 为旧号：SK004=狂怒 / SK011=治愈 / SK012=自愈。</summary>
    public int mercSkillCanonVersion = 0;

    public bool openingIntroPlayed;
    public bool tutorialIntroDone;
    public bool tutorialBattleCleared;
    public bool tutorialOutroPending;
    public bool tutorialDone;
    public bool chapter1IntroDone;
    public bool chapter1ChoiceDone;
    public List<NpcBondEntry> npcBonds = new List<NpcBondEntry>();
    public List<StoryChoiceEntry> storyChoices = new List<StoryChoiceEntry>();

    // —— 叙事 V2.0 ——
    /// <summary>第 7 章转折点：0=未选，1=A，2=B，3=C。同一周目不可更改。</summary>
    public int endingChoice = 0;
    public List<StringIdEntry> seenClueEntries = new List<StringIdEntry>();
    [NonSerialized] public HashSet<string> seenClueIds = new HashSet<string>();
    public List<StringIdEntry> seenEndingEntries = new List<StringIdEntry>();
    [NonSerialized] public HashSet<string> seenEndingIds = new HashSet<string>();
    /// <summary>酒馆靠窗座位的累计查看次数，第 5 次发放线索页 C26。</summary>
    public int tavernSeatViewCount = 0;

    // === 章节进度 ===
    public int maxUnlockedChapter = 1; // 最大解锁章节（兼容字段；权威来源是 ChapterRouteTable.AvailableChapters）
    /// <summary>章节通关次数列表（用于渐进式怪物解锁）</summary>
    public List<ChapterClearCountEntry> chapterClearCounts = new List<ChapterClearCountEntry>();
    /// <summary>已通关章节集合（权威；章节分叉后 maxUnlockedChapter 的减法不再成立，一律读这里）</summary>
    public List<IntIdEntry> clearedChapterEntries = new List<IntIdEntry>();
    [NonSerialized] public HashSet<int> clearedChapterIds = new HashSet<int>();
    /// <summary>
    /// 是否在困难（或更高）难度下通关过第 8 章。2026-09-17：**噩梦难度的唯一解锁条件**。
    /// 由 <see cref="MarkChapterCleared(int, int)"/> 在通关第 8 章且难度 &gt;= 1 时置 true。
    /// 旧存档默认 false —— 已通关的玩家重打一次困难第 8 章即可开启噩梦。
    /// </summary>
    public bool hardCleared = false;
    /// <summary>
    /// 本周目在分叉点（第 2/3/4 章）选定的路线；0 = 未选。
    /// 通关第 8 章后归零，允许下一周目换一条支线。
    /// </summary>
    public int chosenBranch = 0;

    // === 冒险日志图鉴 ===
    public List<StringIdEntry> seenMonsterEntries = new List<StringIdEntry>();
    public List<StringIdEntry> viewedMonsterEntries = new List<StringIdEntry>();
    public List<StringIdEntry> seenMercEntries = new List<StringIdEntry>();
    public List<StringIdEntry> viewedMercEntries = new List<StringIdEntry>();
    /// <summary>已在酒馆解锁的佣兵（HireId）—— 只有解锁的才会进战斗内「佣兵三选一」随机池</summary>
    public List<StringIdEntry> unlockedMercEntries = new List<StringIdEntry>();
    [NonSerialized] public HashSet<string> seenMonsterIds = new HashSet<string>();
    [NonSerialized] public HashSet<string> viewedMonsterIds = new HashSet<string>();
    [NonSerialized] public HashSet<string> seenMercIds = new HashSet<string>();
    [NonSerialized] public HashSet<string> viewedMercIds = new HashSet<string>();
    [NonSerialized] public HashSet<string> unlockedMercIds = new HashSet<string>();
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

    // === 佣兵养成道具（2026-09-18 新增：徽记 + 本命碎片）===
    // id 口径：徽记 "badge:{职业key}:{档位}"（如 badge:剑盾:传奇）；本命碎片 "frag:{hireId}"（如 frag:H001）
    public List<StringIntEntry> mercGrowItemEntries = new List<StringIntEntry>();
    [NonSerialized] public Dictionary<string, int> mercGrowItems = new Dictionary<string, int>();

    // === 时间戳 ===
    public long lastSaveTime = 0;

    /// <summary>JsonUtility 反序列化后调用：List → 运行时 Dictionary/HashSet。</summary>
    public void SyncRuntimeFromLists()
    {
        talentEntries ??= new List<StringIntEntry>();
        unlockedLegendaryWeaponEntries ??= new List<StringIdEntry>();
        unlockedSkillEntries ??= new List<StringIdEntry>();
        shopPurchaseEntries ??= new List<StringIntEntry>();
        skillFragmentEntries ??= new List<StringIntEntry>();
        loginFlagEntries ??= new List<StringIntEntry>();
        achievementProgressEntries ??= new List<StringIntEntry>();
        completedAchievementEntries ??= new List<StringIdEntry>();
        claimedMilestoneEntries ??= new List<IntIdEntry>();
        mailInbox ??= new List<MailEntry>();
        legacyEquipPool ??= new List<EquipmentData>();
        permanentMercs ??= new List<MercenaryData>();
        hiredMercs ??= new List<MercenaryData>();
        lastRunMercHireIds ??= new List<string>();
        npcBonds ??= new List<NpcBondEntry>();
        storyChoices ??= new List<StoryChoiceEntry>();
        chapterClearCounts ??= new List<ChapterClearCountEntry>();
        clearedChapterEntries ??= new List<IntIdEntry>();
        seenMonsterEntries ??= new List<StringIdEntry>();
        viewedMonsterEntries ??= new List<StringIdEntry>();
        seenMercEntries ??= new List<StringIdEntry>();
        viewedMercEntries ??= new List<StringIdEntry>();
        unlockedMercEntries ??= new List<StringIdEntry>();
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
        mercGrowItemEntries ??= new List<StringIntEntry>();
        townLevel ??= new TownLevel();
        if (hiddenLevel <= 0) hiddenLevel = 1;
        if (hiddenExp < 0) hiddenExp = 0;

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

        unlockedSkills = new HashSet<string>();
        for (int i = 0; i < unlockedSkillEntries.Count; i++)
        {
            var e = unlockedSkillEntries[i];
            if (e == null || string.IsNullOrEmpty(e.id)) continue;
            unlockedSkills.Add(e.id);
        }

        shopPurchases = new Dictionary<string, int>();
        string today = ShopDefs.TodayKey();
        if (!string.IsNullOrEmpty(shopPurchaseDay) && shopPurchaseDay != today)
        {
            // 跨天：限购清零（不写回 List，下次 SyncListsFromRuntime 会同步）
            shopPurchaseDay = today;
        }
        else
        {
            for (int i = 0; i < shopPurchaseEntries.Count; i++)
            {
                var e = shopPurchaseEntries[i];
                if (e == null || string.IsNullOrEmpty(e.id)) continue;
                shopPurchases[e.id] = e.value;
            }
        }
        if (string.IsNullOrEmpty(shopPurchaseDay)) shopPurchaseDay = today;

        skillFragments = new Dictionary<string, int>();
        for (int i = 0; i < skillFragmentEntries.Count; i++)
        {
            var e = skillFragmentEntries[i];
            if (e == null || string.IsNullOrEmpty(e.id)) continue;
            skillFragments[e.id] = e.value;
        }

        loginFlags = new Dictionary<string, int>();
        for (int i = 0; i < loginFlagEntries.Count; i++)
        {
            var e = loginFlagEntries[i];
            if (e == null || string.IsNullOrEmpty(e.id)) continue;
            loginFlags[e.id] = e.value;
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
        unlockedMercIds = ToIdSet(unlockedMercEntries);
        claimedCodexRewardIds = ToIdSet(claimedCodexRewardEntries);
        defeatedMonsterIds = ToIdSet(defeatedMonsterEntries);
        unlockedWorldIds = ToIdSet(unlockedWorldEntries);
        completedMainIds = ToIdSet(completedMainEntries);
        completedSideIds = ToIdSet(completedSideEntries);
        seenClueIds = ToIdSet(seenClueEntries);
        seenEndingIds = ToIdSet(seenEndingEntries);

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
        mercGrowItems = new Dictionary<string, int>();
        if (mercGrowItemEntries != null)
        {
            for (int i = 0; i < mercGrowItemEntries.Count; i++)
            {
                var e = mercGrowItemEntries[i];
                if (e == null || string.IsNullOrEmpty(e.id)) continue;
                mercGrowItems[e.id] = e.value;
            }
        }

        logAchProgress = new Dictionary<string, int>();
        for (int i = 0; i < logAchProgressEntries.Count; i++)
        {
            var e = logAchProgressEntries[i];
            if (e == null || string.IsNullOrEmpty(e.id)) continue;
            logAchProgress[e.id] = e.value;
        }

        MigrateClearedChapters();
        if (unlockedMercIds.Count == 0)
            SeedDefaultUnlockedMercs();
    }

    /// <summary>
    /// 老档迁移：把 chapterClearCounts（count&gt;0）补进权威的 clearedChapterIds。
    /// 新档本来就是空集合，走同一入口无副作用。
    /// </summary>
    void MigrateClearedChapters()
    {
        clearedChapterIds = new HashSet<int>();
        for (int i = 0; i < clearedChapterEntries.Count; i++)
        {
            var e = clearedChapterEntries[i];
            if (e != null && e.id > 0) clearedChapterIds.Add(e.id);
        }
        if (clearedChapterIds.Count > 0) return;

        for (int i = 0; i < chapterClearCounts.Count; i++)
        {
            var e = chapterClearCounts[i];
            if (e != null && e.chapter > 0 && e.clearCount > 0)
                clearedChapterIds.Add(e.chapter);
        }
        if (clearedChapterIds.Count > 0)
            clearedChapterEntries = FromIntIdSet(clearedChapterIds);
    }

    /// <summary>新档/空集合兜底：默认解锁 3 名基础佣兵，保证第一局就有佣兵卡可抽。</summary>
    void SeedDefaultUnlockedMercs()
    {
        unlockedMercIds ??= new HashSet<string>();
        foreach (string id in DefaultUnlockedMercIds)
            unlockedMercIds.Add(id);
        unlockedMercEntries = FromIdSet(unlockedMercIds);
    }

    /// <summary>默认解锁：H001 马库斯（剑盾/坦）· H005 米娅（游侠/远程）· H019 艾琳（法师/法系）</summary>
    public static readonly string[] DefaultUnlockedMercIds = { "H001", "H005", "H019" };

    /// <summary>该佣兵是否已在酒馆解锁（进战斗三选一池的必要条件）。</summary>
    public bool IsMercUnlocked(string hireId)
    {
        if (string.IsNullOrEmpty(hireId)) return false;
        if (unlockedMercIds == null) unlockedMercIds = new HashSet<string>();
        return unlockedMercIds.Contains(hireId);
    }

    /// <summary>解锁佣兵；返回是否本次新解锁。</summary>
    public bool UnlockMerc(string hireId)
    {
        if (string.IsNullOrEmpty(hireId)) return false;
        if (unlockedMercIds == null) unlockedMercIds = new HashSet<string>();
        if (!unlockedMercIds.Add(hireId)) return false;
        unlockedMercEntries = FromIdSet(unlockedMercIds);
        return true;
    }

    /// <summary>标记章节通关；通关第 8 章时把 chosenBranch 归零，允许下一周目换支线。</summary>
    /// <param name="difficulty">本次通关所处的难度（0 普通 / 1 困难 / 2 噩梦）；传 -1 表示未知，不参与 hardCleared 判定。</param>
    public void MarkChapterCleared(int chapter, int difficulty = -1)
    {
        if (chapter <= 0) return;
        if (clearedChapterIds == null) clearedChapterIds = new HashSet<int>();
        if (clearedChapterIds.Add(chapter))
            clearedChapterEntries = FromIntIdSet(clearedChapterIds);
        if (chapter >= 8) chosenBranch = 0;
        // 噩梦门槛：在困难及以上难度通关第 8 章
        if (chapter >= 8 && difficulty >= 1) hardCleared = true;
    }

    public bool HasClearedChapter(int chapter)
    {
        return clearedChapterIds != null && chapter > 0 && clearedChapterIds.Contains(chapter);
    }

    /// <summary>已通关章节数（困难/噩梦难度解锁门槛用；分叉后不能用 maxUnlockedChapter-1 推算）。</summary>
    public int ClearedChapterCount()
    {
        return clearedChapterIds != null ? clearedChapterIds.Count : 0;
    }

    static List<IntIdEntry> FromIntIdSet(HashSet<int> set)
    {
        var list = new List<IntIdEntry>(set != null ? set.Count : 0);
        if (set == null) return list;
        foreach (int id in set)
            list.Add(new IntIdEntry { id = id });
        return list;
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
        unlockedSkills ??= new HashSet<string>();
        shopPurchases ??= new Dictionary<string, int>();
        achievementProgress ??= new Dictionary<string, int>();
        completedAchievements ??= new HashSet<string>();
        claimedMilestoneIds ??= new HashSet<int>();
        seenMonsterIds ??= new HashSet<string>();
        viewedMonsterIds ??= new HashSet<string>();
        seenMercIds ??= new HashSet<string>();
        viewedMercIds ??= new HashSet<string>();
        unlockedMercIds ??= new HashSet<string>();
        clearedChapterIds ??= new HashSet<int>();
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

        unlockedSkillEntries = new List<StringIdEntry>(unlockedSkills.Count);
        foreach (string id in unlockedSkills)
            unlockedSkillEntries.Add(new StringIdEntry { id = id });

        shopPurchaseEntries = new List<StringIntEntry>(shopPurchases.Count);
        foreach (var kv in shopPurchases)
            shopPurchaseEntries.Add(new StringIntEntry { id = kv.Key, value = kv.Value });

        skillFragmentEntries = new List<StringIntEntry>(skillFragments.Count);
        foreach (var kv in skillFragments)
            skillFragmentEntries.Add(new StringIntEntry { id = kv.Key, value = kv.Value });

        loginFlagEntries = new List<StringIntEntry>(loginFlags.Count);
        foreach (var kv in loginFlags)
            loginFlagEntries.Add(new StringIntEntry { id = kv.Key, value = kv.Value });

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
        unlockedMercEntries = FromIdSet(unlockedMercIds);
        clearedChapterEntries = FromIntIdSet(clearedChapterIds);
        claimedCodexRewardEntries = FromIdSet(claimedCodexRewardIds);
        defeatedMonsterEntries = FromIdSet(defeatedMonsterIds);
        unlockedWorldEntries = FromIdSet(unlockedWorldIds);
        completedMainEntries = FromIdSet(completedMainIds);
        completedSideEntries = FromIdSet(completedSideIds);
        seenClueEntries = FromIdSet(seenClueIds);
        seenEndingEntries = FromIdSet(seenEndingIds);
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

        mercGrowItemEntries = new List<StringIntEntry>(mercGrowItems.Count);
        foreach (var kv in mercGrowItems)
            mercGrowItemEntries.Add(new StringIntEntry { id = kv.Key, value = kv.Value });
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
    /// <summary>外号 / 称号</summary>
    public string nickname;
    /// <summary>花名册 HireId（H001…）</summary>
    public string hireId;
    /// <summary>实例唯一 ID，名册可有多条同 mercId</summary>
    public string uid;
    public int favorLevel;
    public int level;
    /// <summary>星级 1～5</summary>
    public int star = 1;
    /// <summary>佩戴主动技能（SK 系列；普通佣兵可为空）</summary>
    public string skillId;
    /// <summary>佩戴被动技能（SK 系列；稀有佣兵可为空）</summary>
    public string passiveSkillId;
}
