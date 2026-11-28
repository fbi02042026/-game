/// <summary>
/// 【2026-10-31 立；2026-11-27 主人拍板门槛重排】功能开启顺序 · **唯一出口**。
///
/// 起因（历史）：B2 关卡层落地后 HasClearedChapter(N) 语义从「打完 5 关」变成「打完 50 关」，
/// 所有挂在它上面的功能门槛集体后移，出现「标签能点、进本被拦」的假开门状态。
/// 故把所有门槛收成一处，业务代码不许再自己写 HasClearedChapter(N) 判功能开没开。
///
/// 【2026-11-27 门槛重排 · 主人拍板（下午定稿）】关卡一律用**全局关号 1~80**
/// （路线 B：8 章 × 每章 10 关）。节奏按「一类一起给、每批≤3、批间隔≥2 关」，
/// 深度玩法跨章疏散，佣兵线留第一章。定稿节奏：
/// ├ 引导结束 → 佣兵队列
/// ├ 第 3 关 → 日常任务（宠物按剧情同关，不占本清单）
/// ├ 第 5 关 → 佣兵招募（单独一批）
/// ├ 第 6 关 → 支线岛（按剧情提示，不占本清单）
/// ├ 第 8 关 → 试炼场 / 进阶升星 / 徽记副本（升阶+徽记配对）
/// ├ 第 10 关 → 无限裂隙（世界地图、城镇入口均已隐藏，不报）
/// ├ 第 12 关 → 金币副本 / 卷轴副本 / 礼物副本（活动副本放一起）
/// ├ 第 14 关 → 佣兵情报 / 羁绊闲话（靠后）
/// └ 第 16 关 → 抽池第 4 格 / 换职业（账号级深度，最末）
///
/// 🔴 本文件只做**判定**，不改存档、不碰 UI、不写文案以外的副作用。
/// 🔴 提示文案与判定同源：改门槛常量时同步改下面的 LockHint / LockDesc 文案。
/// </summary>
public static class FeatureUnlock
{
    // ────────────────────────────────────────────────────────────────
    // 门槛常量（全局关号，改顺序 = 改这里，全链生效）
    // ────────────────────────────────────────────────────────────────

    /// <summary>佣兵招募：全局第 5 关（第 1 章节下标 0 基 4）。2026-11-27 主人拍板「招募放第 5 关」。</summary>
    public const int MERC_RECRUIT_CHAPTER = 1;
    public const int MERC_RECRUIT_SUB0 = 4;

    /// <summary>试炼场：全局第 8 关（第 1 章节下标 0 基 7）。从原第 5 关顺延，让位给招募。</summary>
    public const int ARENA_UNLOCK_CHAPTER = 1;
    public const int ARENA_UNLOCK_SUB0 = 7;

    /// <summary>早档：通关第 6 关（第 1 章节下标 0 基 5）—— 现只有支线岛（按剧情提示）。</summary>
    public const int EARLY_GATE_SUB0 = 5;

    /// <summary>中档：通关第 8 关（第 1 章节下标 0 基 7）—— 进阶升星 + 徽记副本（配对）。</summary>
    public const int MID_GATE_SUB0 = 7;

    /// <summary>活动副本档：全局第 12 关 = 第 2 章节下标 0 基 1 —— 金币 / 卷轴 / 礼物副本。</summary>
    public const int DUNGEON_CHAPTER = 2;
    public const int DUNGEON_SUB0 = 1;

    /// <summary>佣兵社交档：全局第 14 关 = 第 2 章节下标 0 基 3 —— 佣兵情报 / 羁绊闲话（靠后）。</summary>
    public const int MERC_SOCIAL_CHAPTER = 2;
    public const int MERC_SOCIAL_SUB0 = 3;

    /// <summary>账号级深度档：全局第 16 关 = 第 2 章节下标 0 基 5 —— 抽池第 4 格 / 换职业。</summary>
    public const int DEEP_CHAPTER = 2;
    public const int DEEP_SUB0 = 5;

    /// <summary>主档：第一章通关 = 全局第 10 关（第 1 章 10 关全清）—— 无限裂隙。2026-11-27 主人拍板。</summary>
    public const int MAIN_GATE_GRAND = 1;

    // 旧常量保留兼容（外部若有引用不断链）；判定一律走上面各档，不再读这些值。
    public const int RIFT_GRAND_GATE = MAIN_GATE_GRAND;
    public const int WORLD_MAP_GRAND_GATE = MAIN_GATE_GRAND;
    public const int SIDE_ISLAND_GRAND_GATE = MAIN_GATE_GRAND;
    public const int GOLD_DUNGEON_CHAPTER_GATE = 1;
    public const int BADGE_DUNGEON_CHAPTER_GATE = 1;
    public const int SCROLL_DUNGEON_CHAPTER_GATE = 1;
    public const int GIFT_DUNGEON_CHAPTER_GATE = 1;
    public const int MERC_RECRUIT_GRAND_GATE = 1;         // 退役兼容常量
    public const int MERC_INTEL_CHAPTER_GATE = 1;         // 退役兼容常量，判定见 MercIntelOpen
    public const int MERC_ASCEND_CHAPTER_GATE = 1;        // 退役兼容常量，判定见 MercAscendOpen
    public const int MERC_BOND_CHAPTER_GATE = 1;          // 退役兼容常量，判定见 MercBondOpen

    // ────────────────────────────────────────────────────────────────
    // 判定（业务代码只调这些）
    // ────────────────────────────────────────────────────────────────

    /// <summary>早档：通关第 6 关。</summary>
    static bool EarlyGate(SaveData data)
        => data != null && data.HasStageCleared(1, EARLY_GATE_SUB0);

    /// <summary>中档：通关第 8 关（进阶升星 + 徽记副本）。</summary>
    static bool MidGate(SaveData data)
        => data != null && data.HasStageCleared(1, MID_GATE_SUB0);

    /// <summary>活动副本档：通关第 12 关（金币 / 卷轴 / 礼物）。</summary>
    static bool DungeonGate(SaveData data)
        => data != null && data.HasStageCleared(DUNGEON_CHAPTER, DUNGEON_SUB0);

    /// <summary>佣兵社交档：通关第 14 关（情报 / 羁绊）。</summary>
    static bool MercSocialGate(SaveData data)
        => data != null && data.HasStageCleared(MERC_SOCIAL_CHAPTER, MERC_SOCIAL_SUB0);

    // 注：账号级深度档（第 16 关 = 抽池第 4 格 / 换职业）的真源分别在
    // SaveData.DrawPoolSlots 与 TalentSystem.CanChangeJobByProgress 各自判 HasStageCleared(DEEP_CHAPTER, DEEP_SUB0)，
    // 本文件只提供常量与「新开启」文案，不放判定（避免 SaveData 反向依赖本类）。

    /// <summary>主档：第一章通关（第 10 关全清）。真源 = clearedStages 实时推导，不读缓存。</summary>
    static bool MainGate(SaveData data)
        => GrandCleared(data, MAIN_GATE_GRAND);

    /// <summary>无限裂隙是否开放（标签可切 + 可进本，两者必须同源，否则就是假开门）。</summary>
    public static bool RiftOpen(SaveData data) => MainGate(data);

    /// <summary>竞技场（试炼场）：第 8 关（第 5 关让位给招募）。</summary>
    public static bool ArenaOpen(SaveData data)
        => data != null && data.HasStageCleared(ARENA_UNLOCK_CHAPTER, ARENA_UNLOCK_SUB0);

    /// <summary>世界地图入口是否显示（入口已整体隐藏，判定保留供其它调用，不列进新开启文案）。</summary>
    public static bool WorldMapOpen(SaveData data) => MainGate(data);

    /// <summary>支线岛是否可派岛：通关第 6 关（按剧情提示触发）。</summary>
    public static bool SideIslandOpen(SaveData data) => EarlyGate(data);

    /// <summary>某类活动副本所属章节（兼容旧调用；无实际调用点，保持旧值不破坏）。</summary>
    public static int DungeonGateChapter(ActivityDungeonKind kind) => 1;

    /// <summary>某类活动副本是否已解锁（只判关卡门槛，不管开放日）。
    /// 徽记 = 第 8 关（随升阶），金币/卷轴/礼物 = 第 12 关（活动副本放一起）。</summary>
    public static bool DungeonOpen(SaveData data, ActivityDungeonKind kind)
    {
        switch (kind)
        {
            case ActivityDungeonKind.Badge:
                return MidGate(data);
            case ActivityDungeonKind.Gold:
            case ActivityDungeonKind.Scroll:
            case ActivityDungeonKind.Gift:
            default:
                return DungeonGate(data);
        }
    }

    /// <summary>活动副本标签页是否可切：任一副本已解锁即可（否则切进去是一片空白横条）。</summary>
    public static bool AnyDungeonOpen(SaveData data)
        => DungeonOpen(data, ActivityDungeonKind.Gold)
        || DungeonOpen(data, ActivityDungeonKind.Badge)
        || DungeonOpen(data, ActivityDungeonKind.Scroll)
        || DungeonOpen(data, ActivityDungeonKind.Gift);

    // ── 佣兵功能（判定）────────────────────────────────────────────

    /// <summary>佣兵队列是否开放：新手引导结束即开（D1 登录已送佣兵，先能排队）。</summary>
    public static bool MercQueueOpen(SaveData data) => data != null && data.tutorialDone;

    /// <summary>佣兵招募（三选一）：第 5 关（2026-11-27 主人拍板，单独一批）。</summary>
    public static bool MercRecruitOpen(SaveData data)
        => data != null && data.HasStageCleared(MERC_RECRUIT_CHAPTER, MERC_RECRUIT_SUB0);

    /// <summary>佣兵情报（每日答题）：第 14 关（靠后，与羁绊同批）。</summary>
    public static bool MercIntelOpen(SaveData data) => MercSocialGate(data);

    /// <summary>进阶 / 升星：第 8 关（与徽记副本同批）。</summary>
    public static bool MercAscendOpen(SaveData data) => MidGate(data);

    /// <summary>佣兵羁绊 / 酒馆闲话：第 14 关（靠后，与情报同批；闲话是输入，羁绊是出口）。</summary>
    public static bool MercBondOpen(SaveData data) => MercSocialGate(data);

    // ────────────────────────────────────────────────────────────────
    // 提示文案（与判定同源，不许各写一份；全局关号 1~80）
    // ────────────────────────────────────────────────────────────────

    public static string RiftLockHint() => "通关第 10 关后开启无限裂隙";
    public static string ArenaLockHint() => "通关第 8 关后开启试炼场";
    /// <summary>活动标签页锁定提示：标签页判据是 AnyDungeonOpen，最早开的是徽记副本（第 8 关随升阶），
    /// 故这里写第 8 关，不是金币/卷轴/礼物的第 12 关。单个副本的下标仍走 <see cref="DungeonLockHint"/>。</summary>
    public static string ActivityTabLockHint() => "通关第 8 关后开启活动副本";

    public static string DungeonLockHint(ActivityDungeonKind kind)
        => kind == ActivityDungeonKind.Badge
           ? "通关第 8 关后开启" + ActivityDungeonRules.DungeonName(kind)
           : "通关第 12 关后开启" + ActivityDungeonRules.DungeonName(kind);

    public static string MercRecruitLockHint() => "通关第 5 关后开启佣兵招募";
    public static string MercQueueLockHint() => "完成新手引导后开启佣兵队列";
    public static string MercIntelLockHint() => "通关第 14 关后开启佣兵情报";
    public static string MercAscendLockHint() => "通关第 8 关后开启佣兵进阶";
    public static string MercBondLockHint() => "通关第 14 关后开启佣兵羁绊";

    public static string MercRecruitLockDesc() => "通关第 5 关后开启";
    public static string MercQueueLockDesc() => "完成新手引导开启";
    public static string MercIntelLockDesc() => "通关第 14 关开启";
    public static string MercBondLockDesc() => "通关第 14 关开启";

    // ────────────────────────────────────────────────────────────────
    // 通关结算「本关新开启」清单（2026-11-27 主人拍板：结算界面下方加一条框写开启的功能）
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 本关（普通难度首通）**新开启**的功能清单，多行文本；没有解锁任何东西 → null。
    /// 【2026-11-27 主人拍板】与上面门槛常量同源：改门槛时这里一起改，不许各写一份。
    /// 门槛跨到第二章，故本章普通难度都判（不再只 chapter==1）；难度局不解锁功能。
    /// 🔴 隐藏入口一律不报：世界地图（冒险页图标整体隐藏）、城镇入口 SLG（主界面入口 EntryEnabled=false）——
    ///    报了玩家也点不进去。支线岛/宠物按剧情提示，不列本清单。
    /// 宠物指路气泡在第 3 关同关弹，BattleResultScreen 已把气泡锚到本框下方错开。
    /// </summary>
    public static string NewlyUnlockedText(int chapter, int stageIndex0, int difficulty)
    {
        if (difficulty > 0) return null;
        if (chapter == 1)
        {
            switch (stageIndex0)
            {
                case 2:                         // 全局第 3 关
                    return "日常任务";
                case MERC_RECRUIT_SUB0:         // 全局第 5 关
                    return "佣兵招募";
                case MID_GATE_SUB0:             // 全局第 8 关
                    return "试炼场 · 进阶升星 · 徽记副本";
                case 9:                         // 全局第 10 关 = 第一章通关
                    return "无限裂隙";
                default:
                    return null;
            }
        }
        if (chapter == 2)
        {
            switch (stageIndex0)
            {
                case DUNGEON_SUB0:              // 全局第 12 关
                    return "金币副本 · 卷轴副本 · 礼物副本";
                case MERC_SOCIAL_SUB0:          // 全局第 14 关
                    return "佣兵情报 · 羁绊闲话";
                case DEEP_SUB0:                 // 全局第 16 关
                    return "抽池第 4 格 · 换职业";
                default:
                    return null;
            }
        }
        return null;
    }


    // ────────────────────────────────────────────────────────────────
    // 内部
    // ────────────────────────────────────────────────────────────────

    /// <summary>第 1 篇已通关大关数是否 ≥ 门槛。路线 B 每章 1 座大关 = 10 关，
    /// 故 GrandCleared(data,1) = 第一章通关 = 全局第 10 关。
    /// 🔴 一律从 clearedStages 实时推导，不读 grandProgress 缓存（缓存可能脏导致提前解锁）。</summary>
    static bool GrandCleared(SaveData data, int grandGate)
        => data != null && data.DerivedGrandProgressFromClearedStages(1) >= grandGate;
}
