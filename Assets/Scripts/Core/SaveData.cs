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
    /// <summary>
    /// 初始金币：2026-10-05 主人要求「初始给的金币数量也设计一下」。
    /// 定 500 —— 进城就能点几次天赋（天赋第一次消费门槛远低于此），不至于开局一分钱干瞪眼；
    /// 又不至于多到跳过前期积累。要改只改这一处（新档唯一真源），老档从磁盘读不受影响。
    /// </summary>
    public long totalGold = 500;
    /// <summary>
    /// 抽奖币（2026-09-29 新增）：只用于进关抽奖，与 totalGold 完全分开。
    /// 战斗通关产出，抽奖消耗；城镇升级 / 商店 / 天赋仍旧只花 totalGold。
    /// </summary>
    public int slotCoins = 0;
    /// <summary>
    /// 佣兵币（2026-10-07 主人拍板新增）：只用于「佣兵」这一类**定向抽奖**的支付币种。
    /// 主人原话「定向招募改用特殊的金币 也可以叫佣兵币」，与 totalGold / slotCoins 三边不通用。
    /// 图标：<c>Assets/Art/UI/Icons/Common/icon_yongbinggold.png</c>
    /// （Resources 副本 <c>Resources/UI/Icons/Common/icon_yongbinggold.png</c>，按铁律 4 同名复制）。
    /// 币种归属的唯一出口 = <c>SlotMachineSystem.FocusCurrency</c>。
    /// </summary>
    public int mercGold = 0;
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

    // === 无限裂隙（2026-10-17 主人拍板）===
    /// <summary>无限裂隙每日次数日键 yyyyMMdd（UTC），跨天清零 triesToday</summary>
    public string endlessRiftDayKey = "";
    /// <summary>今日已用进入次数（每天 2 次）</summary>
    public int endlessRiftTriesToday = 0;
    /// <summary>已首通的最高层（首通奖励一次性真源；记录点 = 本值 / 5 * 5，只进不退）</summary>
    public int endlessRiftClearedStage = 0;

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
    /// <summary>上次自动弹出每日登录面板的 yyyyMMdd；用于「一天最多自动弹一次」，避免回主界面反复弹。</summary>
    public string loginPopupShownDay = "";

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
    /// <summary>
    /// 酒馆三选一当前批次（2026-10-19 主人拍板：招募候选只认 0 点和手动刷新，App 重启也不许换批）。
    /// 首掷/重掷后写回，冷启动弹窗先读这里恢复同一批。
    /// </summary>
    public List<MercenaryData> mercOffers = new List<MercenaryData>();

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
    /// <summary>
    /// 战前剧情「看过即不重播」位标记（2026-10-19 主人拍板）：bit0=第1章 … bit7=第8章。
    /// 每章战前剧情开播过一次就置位，之后进关不再重播（治「每次进都卡剧情」）。
    /// </summary>
    public int preStorySeenMask = 0;
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
    /// <summary>
    /// 【2026-10-20 主人拍板】佣兵获得顺序（HireId，只追加不删）：列表下标即获得先后（越早越靠前），
    /// 给酒馆雇佣队列 / 已拥有列表的「稀有度 → 获得时间」双重排序当第二排序键。
    /// 旧档没有记录的佣兵查不到键，排序时按花名册表序垫底，不影响老档。
    /// </summary>
    public List<StringIdEntry> mercUnlockOrder = new List<StringIdEntry>();
    /// <summary>【2026-10-13 主人拍板】佣兵抽奖池（HireId）：战斗内招募只从池子里的出。格子制，见 <see cref="DrawPoolMaxSlots"/>。</summary>
    public List<StringIdEntry> mercDrawPoolEntries = new List<StringIdEntry>();
    /// <summary>初期开放的抽奖池格数（总容量 5，见 <see cref="DrawPoolMaxSlots"/>；开放进度见 <see cref="DrawPoolSlots"/>）。</summary>
    public const int DrawPoolDefaultSlots = 3;
    /// <summary>抽奖池总容量（UI 画 5 格，后期逐格开放）。</summary>
    public const int DrawPoolMaxSlots = 5;
    [NonSerialized] public HashSet<string> seenMonsterIds = new HashSet<string>();
    [NonSerialized] public HashSet<string> viewedMonsterIds = new HashSet<string>();
    [NonSerialized] public HashSet<string> seenMercIds = new HashSet<string>();
    [NonSerialized] public HashSet<string> viewedMercIds = new HashSet<string>();
    [NonSerialized] public HashSet<string> unlockedMercIds = new HashSet<string>();
    [NonSerialized] public HashSet<string> mercDrawPool = new HashSet<string>();
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

    // === 天赋石里程碑发放（2026-09-19）===
    // 已首次通关的关卡 key（格式 chapter_stageIndex，如 3_7）。重复通关不再发石。
    // 用 List<StringIdEntry> 做可序列化镜像（JsonUtility 不支持 HashSet）。
    [NonSerialized] public HashSet<string> clearedStages = new HashSet<string>();
    public List<StringIdEntry> clearedStageEntries = new List<StringIdEntry>();

    // === 主线任务进度（2026-09-20 迁移：从 PlayerPrefs mq_v1_* 迁入云存档）===
    // 已完成集合：id = questId（如 C1_Q2）
    public List<StringIdEntry> mainQuestDoneEntries = new List<StringIdEntry>();
    [NonSerialized] public HashSet<string> mainQuestDone = new HashSet<string>();
    // 计数：TalkNpc/WatchStory 完成时记 = need（一次性达成）
    public List<StringIntEntry> mainQuestCntEntries = new List<StringIntEntry>();
    [NonSerialized] public Dictionary<string, int> mainQuestCnt = new Dictionary<string, int>();
    // 基线：RecruitMerc/UpgradeTalent 接任务时的快照（雇佣数 / 天赋石），progress = 当前 - 基线
    public List<StringIntEntry> mainQuestBaseEntries = new List<StringIntEntry>();
    [NonSerialized] public Dictionary<string, int> mainQuestBase = new Dictionary<string, int>();

    /// <summary>洗点次数（天赋系统只读，本文件只负责加字段）。</summary>
    public int respecCount = 0;
    /// <summary>背包已解锁行数，默认 3（与 GameConfig 默认值保持一致）。</summary>
    public int backpackRows = 3;

    // === 新手引导进度（2026-09-21 从 PlayerPrefs 迁入云存档）===
    /// <summary>首次软引导游标：下一个要跑的步骤下标（= 已完成步骤数）；7 = FirstRunGuide.GuideStep.Done，已走完。</summary>
    public int guideStep = 0;
    /// <summary>首次引导是否已全部完成（旧版本只写这个标记，用于老存档补齐游标）。</summary>
    public bool guideDone = false;

    // === 每日任务「通关 3 关」保底天赋石（2026-09-19）===
    /// <summary>当日累计通关计数所绑定的日期键（yyyyMMdd）。</summary>
    public string dailyStageClearDayKey = "";
    public int dailyStageClearCount = 0;
    /// <summary>已领取「通关 3 关」5 石奖励的日期键（跨天后可再领）。</summary>
    public string dailyClearTaskClaimDayKey = "";
    /// <summary>每日任务阈值：当日通关 3 关。</summary>
    public const int DAILY_CLEAR_TASK_NEED = 3;
    /// <summary>每日任务奖励：5 天赋石。</summary>
    public const int DAILY_CLEAR_TASK_STONES = 5;

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

    // === 体力药水（2026-10-18 主人拍板：药水改道具入背包，背包里手动使用，使用时超出上限会提示浪费）===
    public int staminaPotions = 0;

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
        clearedStageEntries ??= new List<StringIdEntry>();
        mainQuestDoneEntries ??= new List<StringIdEntry>();
        mainQuestCntEntries ??= new List<StringIntEntry>();
        mainQuestBaseEntries ??= new List<StringIntEntry>();
        townLevel ??= new TownLevel();
        if (hiddenLevel <= 0) hiddenLevel = 1;
        if (hiddenExp < 0) hiddenExp = 0;
        if (backpackRows < 1) backpackRows = 3;

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
        mercDrawPool = ToIdSet(mercDrawPoolEntries);
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
        clearedStages = ToIdSet(clearedStageEntries);

        mainQuestDone = ToIdSet(mainQuestDoneEntries);

        mainQuestCnt = new Dictionary<string, int>();
        for (int i = 0; i < mainQuestCntEntries.Count; i++)
        {
            var e = mainQuestCntEntries[i];
            if (e == null || string.IsNullOrEmpty(e.id)) continue;
            mainQuestCnt[e.id] = e.value;
        }

        mainQuestBase = new Dictionary<string, int>();
        for (int i = 0; i < mainQuestBaseEntries.Count; i++)
        {
            var e = mainQuestBaseEntries[i];
            if (e == null || string.IsNullOrEmpty(e.id)) continue;
            mainQuestBase[e.id] = e.value;
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
        MigrateMercScrolls();
        if (unlockedMercIds.Count == 0)
            SeedDefaultUnlockedMercs();
        // 【2026-10-13 主人拍板】塔克补发 + 抽奖池播种：老档第一次进新版也走（幂等，配过池子的不打扰）
        SeedDrawPoolIfEmpty();
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

    /// <summary>
    /// 卷轴体系改版迁移（2026-10-17 主人拍板）：稀有用「普通卷轴」、传奇用「高级招募卷轴」招募后，
    /// 旧「稀有招募卷」（mercScrollRare）失去用途——一次性折算进普通卷轴并清零（幂等：清零后不再触发）。
    /// </summary>
    void MigrateMercScrolls()
    {
        if (mercScrollRare > 0)
        {
            mercScrollCommon += mercScrollRare;
            mercScrollRare = 0;
        }
    }

    /// <summary>新档/空集合兜底：默认解锁基础佣兵，保证第一局就有佣兵卡可抽。</summary>
    void SeedDefaultUnlockedMercs()
    {
        unlockedMercIds ??= new HashSet<string>();
        foreach (string id in DefaultUnlockedMercIds)
        {
            unlockedMercIds.Add(id);
            // 【2026-10-20 主人拍板】默认名单也记获得顺序（幂等），初始三白按名单序排最前
            RecordMercUnlockOrder(id);
        }
        unlockedMercEntries = FromIdSet(unlockedMercIds);
        // 【2026-10-13 主人拍板】抽奖池同步播种：默认名单进池
        // （2026-10-20 塔克移出默认名单后初期 3 白占 3 格，第 4 格由 D1 领塔克后 EnsurePoolFilled 补上）
        SeedDrawPoolIfEmpty();
    }

    /// <summary>
    /// 【2026-10-13 主人拍板】抽奖池播种（幂等，新档 / 老档第一次进新版都走）：
    /// 池子不满开放格数就走 <see cref="EnsurePoolFilled"/> 自动补满
    /// （主人拍板「池子最少要补全 会自动补全」，玩家点开酒馆时池子已经是满的）。
    /// 【2026-10-20 二次拍板】塔克不再在此无条件补进解锁——改由每日登录 D1 真发本体，
    /// GrantOne 解锁佣兵后自动 EnsurePoolFilled 补进池子（照 MailSystem 先例）。
    /// </summary>
    void SeedDrawPoolIfEmpty()
    {
        mercDrawPool ??= new HashSet<string>();
        EnsurePoolFilled();
    }

    /// <summary>
    /// 【2026-10-13 主人拍板】池子自动补全：不足开放格数时，从未上架的已解锁佣兵按
    /// 稀有度（传说 → 稀有 → 普通）+ 花名册表序补满；候选耗尽（已获得全在池里）才留空格。
    /// 下架（<see cref="RemoveFromDrawPool"/>）后系统立即调它 —— 玩家点开酒馆看到的池子永远是满的。
    /// </summary>
    public bool EnsurePoolFilled()
    {
        if (mercDrawPool == null) mercDrawPool = new HashSet<string>();
        if (unlockedMercIds == null) return false;
        int slots = DrawPoolSlots;
        bool changed = false;
        IReadOnlyList<MercRosterDefs.Def> all = MercRosterDefs.All;
        // 稀有度从高到低三段扫（不猜枚举数值，显式按档位过）
        for (int pass = 0; pass < 3 && mercDrawPool.Count < slots; pass++)
        {
            var tier = pass == 0 ? MercRosterDefs.MercRarity.Legendary
                : pass == 1 ? MercRosterDefs.MercRarity.Rare
                : MercRosterDefs.MercRarity.Common;
            for (int i = 0; i < all.Count && mercDrawPool.Count < slots; i++)
            {
                var d = all[i];
                if (d.Rarity != tier) continue;
                if (!unlockedMercIds.Contains(d.HireId)) continue;
                if (mercDrawPool.Add(d.HireId)) changed = true;
            }
        }
        if (changed) mercDrawPoolEntries = FromIdSet(mercDrawPool);
        return changed;
    }

    /// <summary>
    /// 【2026-10-13 主人拍板】上架进池（酒馆点已获得佣兵）。池满返回 false（UI 走替换弹窗流）。
    /// 返回是否变化。
    /// </summary>
    public bool AddToDrawPool(string hireId)
    {
        if (string.IsNullOrEmpty(hireId)) return false;
        if (unlockedMercIds == null || !unlockedMercIds.Contains(hireId)) return false;
        if (mercDrawPool == null) mercDrawPool = new HashSet<string>();
        if (mercDrawPool.Contains(hireId)) return false;
        if (mercDrawPool.Count >= DrawPoolSlots) return false;
        mercDrawPool.Add(hireId);
        mercDrawPoolEntries = FromIdSet(mercDrawPool);
        return true;
    }

    /// <summary>
    /// 【2026-10-13 主人拍板】从池里移除并立即自动补全（池格右上角红「−」的下架流）。
    /// 候选耗尽（已获得全在池里）才留空格。返回是否变化。
    /// </summary>
    public bool RemoveFromDrawPool(string hireId)
    {
        if (string.IsNullOrEmpty(hireId)) return false;
        if (mercDrawPool == null || !mercDrawPool.Remove(hireId)) return false;
        EnsurePoolFilled();
        mercDrawPoolEntries = FromIdSet(mercDrawPool);
        return true;
    }

    /// <summary>
    /// 【2026-10-13 主人拍板】池满时点已获得佣兵 → 弹替换弹窗 → 换入换出（一次成对操作）。
    /// 两者都须已解锁、被换的须在池里。返回是否变化。
    /// </summary>
    public bool SwapInDrawPool(string inHireId, string outHireId)
    {
        if (string.IsNullOrEmpty(inHireId) || string.IsNullOrEmpty(outHireId)) return false;
        if (inHireId == outHireId) return false;
        if (unlockedMercIds == null || !unlockedMercIds.Contains(inHireId)) return false;
        if (mercDrawPool == null || !mercDrawPool.Contains(outHireId)) return false;
        if (!mercDrawPool.Remove(outHireId)) return false;
        mercDrawPool.Add(inHireId);
        mercDrawPoolEntries = FromIdSet(mercDrawPool);
        return true;
    }

    /// <summary>
    /// 默认解锁（2026-10-13 主人拍板「初期三个白色佣兵：一个牧师一个剑盾一个游侠」）：
    /// H011 路加（牧师）· H001 马库斯（剑盾）· H005 米娅（游侠）。
    /// 【2026-10-20 二次拍板】塔克（H003）移出默认名单——改由每日登录 D1 真发佣兵本体，
    /// 领取时报喜「恭喜获得稀有佣兵·塔克，实力大增！」（与三选一抽中报喜统一）。
    /// </summary>
    public static readonly string[] DefaultUnlockedMercIds = { "H011", "H001", "H005" };

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
        // 【2026-10-20 主人拍板】记获得顺序（只追加不删，下标即获得先后），供队列/列表双重排序
        RecordMercUnlockOrder(hireId);
        return true;
    }

    /// <summary>获得顺序追加一条（幂等：同一佣兵只记首次；旧档/默认名单兜底播种共用）。</summary>
    void RecordMercUnlockOrder(string hireId)
    {
        mercUnlockOrder ??= new List<StringIdEntry>();
        for (int i = 0; i < mercUnlockOrder.Count; i++)
            if (mercUnlockOrder[i] != null && mercUnlockOrder[i].id == hireId) return;
        mercUnlockOrder.Add(new StringIdEntry { id = hireId });
    }

    /// <summary>
    /// 【2026-10-13 主人拍板】该佣兵是否在抽奖池里。池子是**格子制**（酒馆 6 格、初期开放 4 格）：
    /// 格子里有谁才抽谁；**空池 = 本局抽不到新佣兵**（升级 / 升星卡照常出，不会弹「没能出结果」）。
    /// </summary>
    public bool IsMercInDrawPool(string hireId)
    {
        if (string.IsNullOrEmpty(hireId)) return false;
        if (mercDrawPool == null) mercDrawPool = new HashSet<string>();
        return mercDrawPool.Contains(hireId);
    }

    /// <summary>
    /// 【2026-10-13 主人拍板】整池替换抽奖池（酒馆「佣兵抽奖池」弹窗确定时调用）。
    /// 只保留已解锁的 hireId（下架的不该变成暗池）；超出开放格数按花名册表序截断（UI 层已拦，这里防御）；
    /// 返回是否真的变了（没变就不重写 entries）。
    /// </summary>
    public bool SetMercDrawPool(HashSet<string> ids)
    {
        var next = ids != null ? new HashSet<string>(ids) : new HashSet<string>();
        if (unlockedMercIds != null)
            next.RemoveWhere(id => !unlockedMercIds.Contains(id));
        int slots = DrawPoolSlots;
        if (next.Count > slots)
        {
            // HashSet 无序，截断必须走花名册表序，否则随机砍配置
            var keep = new HashSet<string>();
            IReadOnlyList<MercRosterDefs.Def> all = MercRosterDefs.All;
            for (int i = 0; i < all.Count && keep.Count < slots; i++)
                if (next.Contains(all[i].HireId)) keep.Add(all[i].HireId);
            next = keep;
        }
        if (mercDrawPool == null) mercDrawPool = new HashSet<string>();
        bool same = mercDrawPool.Count == next.Count;
        if (same)
        {
            foreach (string id in next)
            {
                if (!mercDrawPool.Contains(id)) { same = false; break; }
            }
        }
        if (same) return false;
        mercDrawPool = next;
        mercDrawPoolEntries = FromIdSet(mercDrawPool);
        return true;
    }

    /// <summary>
    /// 抽奖池当前开放格数（2026-10-13 主人拍板「剩下的格子当玩家通关第一章后面的章节开放」）：
    /// 初期 4 格；**通关第 2 章开第 5 格、通关第 3 章开第 6 格**（条件要改就动这里，一行一处）。
    /// </summary>
    public int DrawPoolSlots
    {
        get
        {
            int s = DrawPoolDefaultSlots;
            if (clearedChapterIds != null)
            {
                if (clearedChapterIds.Contains(2)) s++;
                if (clearedChapterIds.Contains(3)) s++;
            }
            // 本文件是纯数据层，不依赖 UnityEngine，故用 System.Math.Min 而非 Mathf.Min
            s = System.Math.Min(s, DrawPoolMaxSlots);
            return s;
        }
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

    /// <summary>标记某关首次通关；返回本次应发的天赋石（按章首次通关石）。重复通关返回 0。</summary>
    public int GrantTalentForStageFirstClear(int chapter, int stageIndex)
    {
        clearedStages ??= new HashSet<string>();
        string key = chapter + "_" + stageIndex;
        if (!clearedStages.Add(key)) return 0;     // 重复通关不再发石
        clearedStageEntries = FromIdSet(clearedStages);
        int ch = chapter < 1 ? 1 : (chapter > 8 ? 8 : chapter);
        return TalentDefs.TalentStoneRewards.FirstClearStone(ch);
    }

    /// <summary>某章已首次通关的关卡数（冒险日志里程碑进度用）。</summary>
    public int ClearedStageCountInChapter(int chapter)
    {
        if (clearedStages == null || clearedStages.Count == 0) return 0;
        string prefix = chapter + "_";
        int n = 0;
        foreach (var k in clearedStages)
            if (!string.IsNullOrEmpty(k) && k.StartsWith(prefix)) n++;
        return n;
    }

    /// <summary>每日任务「通关 3 关」：当日累计通关达阈值且未领过则发 5 天赋石。返回发放数（0 或 5）。</summary>
    public int TryDailyClearTaskStones()
    {
        string today = ShopDefs.TodayKey();
        if (string.IsNullOrEmpty(today)) return 0;
        if (dailyStageClearDayKey != today)
        {
            dailyStageClearDayKey = today;
            dailyStageClearCount = 0;
        }
        dailyStageClearCount++;
        if (dailyStageClearCount >= DAILY_CLEAR_TASK_NEED && dailyClearTaskClaimDayKey != today)
        {
            dailyClearTaskClaimDayKey = today;
            return DAILY_CLEAR_TASK_STONES;
        }
        return 0;
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
        mercDrawPool ??= new HashSet<string>();
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
        mainQuestDone ??= new HashSet<string>();
        mainQuestCnt ??= new Dictionary<string, int>();
        mainQuestBase ??= new Dictionary<string, int>();

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
        mercDrawPoolEntries = FromIdSet(mercDrawPool);
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

        clearedStageEntries = FromIdSet(clearedStages);

        mainQuestDoneEntries = FromIdSet(mainQuestDone);

        mainQuestCntEntries = new List<StringIntEntry>(mainQuestCnt.Count);
        foreach (var kv in mainQuestCnt)
            mainQuestCntEntries.Add(new StringIntEntry { id = kv.Key, value = kv.Value });

        mainQuestBaseEntries = new List<StringIntEntry>(mainQuestBase.Count);
        foreach (var kv in mainQuestBase)
            mainQuestBaseEntries.Add(new StringIntEntry { id = kv.Key, value = kv.Value });

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
