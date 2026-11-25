/// <summary>
/// 【2026-10-31 主人拍板】功能开启顺序 · **唯一出口**。
///
/// 起因：B2 关卡层（篇 → 10 座大关 → 每座 5 小关）落地后，`HasClearedChapter(N)` 的含义
/// 从「打完 5 关」变成「打完 50 关」，所有还挂在这个判据上的功能门槛集体往后挪了一天多，
/// 出现「标签能点、进本被拦」的假开门状态。本文件把所有功能门槛收成一处，
/// 以后**改顺序只改这里**，业务代码不许再自己写 `HasClearedChapter(N)` 判功能开没开。
///
/// 口径（纯广告小游戏首日节奏，2026-10-31 主人确认）：
/// ┌ 引导结束 主线（恒开）+ 天赋 / 背包 + **佣兵队列**（D1 每日登录已送佣兵本体，得让他配）
/// │  ⚠ 竞技场不再「引导结束恒开」：2026-11-25 起 = 通关第 1 章第 5 关（见 ARENA_UNLOCK_SUB0）
/// ├ D1 通第 1 座大关（≈25 体力）→ 无限裂隙（标签 + 进本）、世界地图入口、支线岛派岛
/// ├ D1~2 通第 2 座大关（打到 3-1）→ **佣兵招募**（2026-11-01 主人拍板「三选一在第二大关完成后解锁」）
/// ├ D2 通第 1 章（50 小关）  → 金币副本、定向抽、**进阶/升星**（2026-11-08 主人拍板由 D3 提前）
/// ├ D3 通第 2 章（100 小关） → 徽记副本、佣兵抽池末格、**佣兵情报**、**佣兵羁绊 + 酒馆闲话**（2026-11-09 主人拍板由 D4~5 提前）
/// └ D4~5 通第 3 章           → 卷轴副本
///
/// 🔴 本文件只做**判定**，不改存档、不碰 UI、不写文案以外的副作用。
/// 🔴 门槛数值全部是常量，禁止在业务代码里散写数字。
/// </summary>
public static class FeatureUnlock
{
    // ────────────────────────────────────────────────────────────────
    // 门槛表（改顺序 = 改这里，全链生效）
    // ────────────────────────────────────────────────────────────────

    /// <summary>无限裂隙（标签 + 进本同一门槛）：第 1 篇第 1 座大关通关。</summary>
    public const int RIFT_GRAND_GATE = 1;

    /// <summary>世界地图入口：第 1 篇第 1 座大关通关（它是主线导航，不是玩法，跟着主线走）。</summary>
    public const int WORLD_MAP_GRAND_GATE = 1;

    /// <summary>支线岛派岛：通第 1 章（10 关全通）后才开放。2026-11-17 主人拍板后挪。</summary>
    public const int SIDE_ISLAND_GRAND_GATE = 10;

    /// <summary>金币副本：通第 1 章。</summary>
    public const int GOLD_DUNGEON_CHAPTER_GATE = 1;

    /// <summary>徽记副本：通第 2 章（2026-10-31 与金币错开，D2/D3 各开一个）。</summary>
    public const int BADGE_DUNGEON_CHAPTER_GATE = 2;

    /// <summary>卷轴副本：通第 3 章。</summary>
    public const int SCROLL_DUNGEON_CHAPTER_GATE = 3;

    /// <summary>礼物副本：通第 1 章（周六才开，开放日限制见 ActivityDungeonRules.IsOpenToday）。</summary>
    public const int GIFT_DUNGEON_CHAPTER_GATE = 1;

    /// <summary>竞技场（试炼场）入口：通关第 1 章第 5 关（2026-11-25 主人拍板「第 5 关吧」，由章末 1-10 提前）。
    /// 与佣兵招募（HasStageCleared(1,4)）同批开放；门禁判定从 ArenaEntryUI 收口到这里，功能门槛只留这一个出口。</summary>
    public const int ARENA_UNLOCK_SUB0 = 4;

    // ────────────────────────────────────────────────────────────────
    // 佣兵功能门槛（2026-10-31 主人拍板「佣兵的那些功能也要区分一下，不要一下都开，
    // 按照玩家需求的先后顺序来」）
    //
    // 排序依据 = **玩家在佣兵上的需求递进**，不是功能复杂度：
    //   ① 先决定带谁   → 佣兵队列（**新手引导一结束就开**：D1 每日登录直接送「塔克·重盾」本体，
    //                     玩家手里已经有佣兵了，第一件想做的事就是配置下场带谁）
    //   ② 再永久招人   → 招募（有人之后才谈"养谁"，且招募要花金币/招募卷，不适合第一天就丢给新手）
    //   ③ 再想变强     → 进阶/升星（抽到重复碎片才用得上）+ 情报（每日答题给增益，顺手给个每天来的理由）
    //   ④ 再谈感情     → 羁绊 + 闲话（主力固定下来，好感才有意义）
    // 一天只开一样，D1~D4 每天进酒馆都有新东西。
    // 【2026-10-31 主人二次拍板】队列必须比招募**先**开：招募一开队列也得同时可用，
    // 所以队列排 ①、招募排 ②，绝不允许「招募开了、队列还锁着」。
    // 【2026-10-31 主人三次拍板】队列门槛不是推图，而是 **tutorialDone** ——
    // 依据：`DailyLoginDefs` 的 D1（starter_1）直接发稀有佣兵「塔克·重盾」本体，
    // 玩家过完引导就已有佣兵，队列再锁着等于让他有兵不能编。
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 佣兵招募（三选一）：【路线 B】改为通第 1 章开启（原「通第 1 篇第 2 座大关 / 3-1 解锁」口径作废）。必然晚于队列（队列 = 引导结束即开）。
    /// 路线 B 起招募判定 / 文案不再引用本常量，保留仅为兼容。
    /// </summary>
    public const int MERC_RECRUIT_GRAND_GATE = 2;

    /// <summary>佣兵情报（每日答题）：通第 2 章（D3）。</summary>
    public const int MERC_INTEL_CHAPTER_GATE = 2;

    /// <summary>进阶 / 升星：通第 1 章（D2）。【2026-11-08 主人拍板】由第 2 章提前到第 1 章；
    /// 与佣兵情报 / 徽记副本不再同批。只改本常量，卡片「升阶」钮、详情「进阶」钮、锁提示文案同源自动跟随。</summary>
    public const int MERC_ASCEND_CHAPTER_GATE = 1;

    /// <summary>佣兵羁绊（好感 / 送礼 / 羁绊地图）+ 酒馆闲话：通第 2 章（D3）。【2026-11-09 主人拍板】由第 3 章提前到第 2 章；只改本常量，锁提示 / 卡面副标题同源自动跟随。</summary>
    public const int MERC_BOND_CHAPTER_GATE = 2;


    // ────────────────────────────────────────────────────────────────
    // 判定（业务代码只调这些）
    // ────────────────────────────────────────────────────────────────

    /// <summary>无限裂隙是否开放（标签可切 + 可进本，两者必须同源，否则就是假开门）。</summary>
    public static bool RiftOpen(SaveData data) => GrandCleared(data, RIFT_GRAND_GATE);

    /// <summary>竞技场（试炼场）是否开放：入口显隐、冒险页 toast、日常任务门槛共用这一个判断。</summary>
    public static bool ArenaOpen(SaveData data)
        => data != null && data.HasStageCleared(1, ARENA_UNLOCK_SUB0);

    /// <summary>竞技场未开放提示。</summary>
    public static string ArenaLockHint() => "通关第 1 章第 5 关后开启试炼场";

    /// <summary>世界地图入口是否显示。</summary>
    public static bool WorldMapOpen(SaveData data) => GrandCleared(data, WORLD_MAP_GRAND_GATE);

    /// <summary>支线岛是否可派岛。</summary>
    public static bool SideIslandOpen(SaveData data) => GrandCleared(data, SIDE_ISLAND_GRAND_GATE);

    /// <summary>某类活动副本的章节门槛。</summary>
    public static int DungeonGateChapter(ActivityDungeonKind kind)
    {
        switch (kind)
        {
            case ActivityDungeonKind.Gold:   return GOLD_DUNGEON_CHAPTER_GATE;
            case ActivityDungeonKind.Badge:  return BADGE_DUNGEON_CHAPTER_GATE;
            case ActivityDungeonKind.Scroll: return SCROLL_DUNGEON_CHAPTER_GATE;
            case ActivityDungeonKind.Gift:   return GIFT_DUNGEON_CHAPTER_GATE;
            default:                         return GOLD_DUNGEON_CHAPTER_GATE;
        }
    }

    /// <summary>某类活动副本是否已解锁（只判章节门槛，不管开放日）。</summary>
    public static bool DungeonOpen(SaveData data, ActivityDungeonKind kind)
        => data != null && data.HasClearedChapter(DungeonGateChapter(kind));

    /// <summary>活动副本标签页是否可切：任一副本已解锁即可（否则切进去是一片空白横条）。</summary>
    public static bool AnyDungeonOpen(SaveData data)
        => DungeonOpen(data, ActivityDungeonKind.Gold)
        || DungeonOpen(data, ActivityDungeonKind.Badge)
        || DungeonOpen(data, ActivityDungeonKind.Scroll)
        || DungeonOpen(data, ActivityDungeonKind.Gift);

    // ── 佣兵功能（判定）────────────────────────────────────────────

    /// <summary>
    /// 佣兵队列是否开放：**新手引导结束即开**（比招募早）。
    /// 依据：D1 每日登录直接送「塔克·重盾」本体，玩家过完引导手里就有佣兵，需要马上去配置下场带谁。
    /// </summary>
    public static bool MercQueueOpen(SaveData data) => data != null && data.tutorialDone;

    /// <summary>佣兵招募是否开放（【2026-11-18 主人拍板】由「通第 1 章」提前到「第一章第 5 关通关」，0 基关号 4）。
    /// 目的：让玩家在第一章中途就能招到新佣兵，制造换人/招新欲望。仍晚于队列（队列 = 引导结束即开）。</summary>
    public static bool MercRecruitOpen(SaveData data) => data != null && data.HasStageCleared(1, 4);

    /// <summary>佣兵情报是否开放。</summary>
    public static bool MercIntelOpen(SaveData data) => ChapterCleared(data, MERC_INTEL_CHAPTER_GATE);

    /// <summary>进阶 / 升星是否开放（卡片「升阶」钮、详情「进阶」钮都问这里）。</summary>
    public static bool MercAscendOpen(SaveData data) => ChapterCleared(data, MERC_ASCEND_CHAPTER_GATE);

    /// <summary>佣兵羁绊 / 酒馆闲话是否开放（两者同批：闲话是输入，羁绊是出口）。</summary>
    public static bool MercBondOpen(SaveData data) => ChapterCleared(data, MERC_BOND_CHAPTER_GATE);


    // ────────────────────────────────────────────────────────────────
    // 提示文案（与判定同源，不许各写一份）
    // ────────────────────────────────────────────────────────────────

    /// <summary>裂隙未开放提示。</summary>
    public static string RiftLockHint() => $"通关第 {RIFT_GRAND_GATE} 座大关后开启无限裂隙";

    /// <summary>活动副本标签未开放提示（按最早解锁的那本说）。</summary>
    public static string ActivityTabLockHint()
        => $"通关第 {GOLD_DUNGEON_CHAPTER_GATE} 章后开启活动副本";

    /// <summary>某类活动副本未解锁提示。</summary>
    public static string DungeonLockHint(ActivityDungeonKind kind)
        => $"通关第 {DungeonGateChapter(kind)} 章后开启{ActivityDungeonRules.DungeonName(kind)}";

    /// <summary>佣兵招募未开放提示。【路线 B】文案改「通关第 1 章后开启」口径（旧「X-X 大关-小关」格式作废）。</summary>
    public static string MercRecruitLockHint() => $"通关{MercRecruitGateStageNo()}后开启佣兵招募";

    /// <summary>佣兵队列未开放提示。</summary>
    public static string MercQueueLockHint() => "完成新手引导后开启佣兵队列";

    /// <summary>佣兵情报未开放提示。</summary>
    public static string MercIntelLockHint() => $"通关第 {MERC_INTEL_CHAPTER_GATE} 章后开启佣兵情报";

    /// <summary>进阶 / 升星未开放提示。</summary>
    public static string MercAscendLockHint() => $"通关第 {MERC_ASCEND_CHAPTER_GATE} 章后开启佣兵进阶";

    /// <summary>羁绊 / 闲话未开放提示。</summary>
    public static string MercBondLockHint() => $"通关第 {MERC_BOND_CHAPTER_GATE} 章后开启佣兵羁绊";

    /// <summary>卡面副标题用的短锁文案（酒馆功能卡 Desc 直接吃这个）。
    /// 【路线 B】佣兵招募改「通关第 1 章后开启」，不说「第 N 座大关」。</summary>
    public static string MercRecruitLockDesc() => $"通关{MercRecruitGateStageNo()}后开启";
    public static string MercQueueLockDesc() => "完成新手引导开启";
    public static string MercIntelLockDesc() => $"通关第 {MERC_INTEL_CHAPTER_GATE} 章开启";
    public static string MercBondLockDesc() => $"通关第 {MERC_BOND_CHAPTER_GATE} 章开启";


    // ────────────────────────────────────────────────────────────────
    // 内部
    // ────────────────────────────────────────────────────────────────

    /// <summary>第 1 篇已通关大关数是否 ≥ 门槛（裂隙 / 世界地图 / 支线岛共用）。
    /// 🔴 一律从 clearedStages 实时推导（DerivedGrandProgressFromClearedStages），不读 grandProgress 缓存——
    /// 缓存可能脏导致还没打完就提前解锁。同 ChapterManager.StartChapter / OnStageComplete 口径。</summary>
    static bool GrandCleared(SaveData data, int grandGate)
        => data != null && data.DerivedGrandProgressFromClearedStages(1) >= grandGate;

    /// <summary>第 chapter 章是否已通关（招募 / 情报 / 进阶 / 羁绊共用）。</summary>
    static bool ChapterCleared(SaveData data, int chapter)
        => data != null && data.HasClearedChapter(chapter);

    /// <summary>
    /// 佣兵招募门槛的文案（【2026-11-18 主人拍板】第一章第 5 关通关开启）。
    /// 文案唯一出口，锁提示 / 卡面副标题共用。
    /// </summary>
    static string MercRecruitGateStageNo() => "第 1 章第 5 关";
}
