/// <summary>
/// 【2026-10-31 立；2026-11-27 主人拍板门槛重排】功能开启顺序 · **唯一出口**。
///
/// 起因（历史）：B2 关卡层落地后 HasClearedChapter(N) 语义从「打完 5 关」变成「打完 50 关」，
/// 所有挂在它上面的功能门槛集体后移，出现「标签能点、进本被拦」的假开门状态。
/// 故把所有门槛收成一处，业务代码不许再自己写 HasClearedChapter(N) 判功能开没开。
///
/// 【2026-11-27 门槛重排 · 主人拍板（下午再调三档）】关卡一律用**全局关号 1~80**
/// （路线 B：8 章 × 每章 10 关），不再写「1-1 / 1-5 / 第 X 章第 Y 关 / 第 N 座大关」。
/// 节奏按「一类一起给，学一次就懂」分档，全部压进第一章：
/// ├ 前期：佣兵队列(引导结束)、日常·周常(第 3 关)、宠物(第 3 关)、试炼场/佣兵招募/换职业(第 5 关)
/// ├ 第 6 关 → 支线岛 / 金币副本 / 徽记副本（都是新玩法入口，一次学会看地图）
/// ├ 第 8 关 → 进阶·升星 / 佣兵情报 / 羁绊+酒馆闲话（都是佣兵养成，都在酒馆/角色页）
/// └ 第一章通关（第 10 关）→ 无限裂隙 / 世界地图 / 礼物副本 / 卷轴副本 / 抽池第 4 格（章末大礼包）
///
/// 🔴 本文件只做**判定**，不改存档、不碰 UI、不写文案以外的副作用。
/// 🔴 提示文案与判定同源：改门槛常量时同步改下面的 LockHint / LockDesc 文案。
/// </summary>
public static class FeatureUnlock
{
    // ────────────────────────────────────────────────────────────────
    // 门槛常量（全局关号，改顺序 = 改这里，全链生效）
    // ────────────────────────────────────────────────────────────────

    /// <summary>早档：通关第 6 关 = 第 1 章节 6（章内节下标 0 基 5）—— 支线岛 / 金币副本 / 徽记副本。
    /// 2026-11-27 主人拍板（从第 8 关提前，跟进阶类错开）。</summary>
    public const int EARLY_GATE_SUB0 = 5;

    /// <summary>中档：通关第 8 关 = 第 1 章节 8（章内节下标 0 基 7）—— 进阶升星 / 佣兵情报 / 羁绊闲话。
    /// 2026-11-27 主人拍板：佣兵养成类单独一档，不与玩法入口混发。</summary>
    public const int MID_GATE_SUB0 = 7;

    /// <summary>主档：第一章通关 = 全局第 10 关（第 1 章 10 关全清）。2026-11-27 主人拍板。</summary>
    public const int MAIN_GATE_GRAND = 1;

    // 旧常量保留兼容（外部若有引用不断链）；判定一律走 EARLY_GATE / MAIN_GATE，不再读这些值。
    public const int RIFT_GRAND_GATE = MAIN_GATE_GRAND;
    public const int WORLD_MAP_GRAND_GATE = MAIN_GATE_GRAND;
    public const int SIDE_ISLAND_GRAND_GATE = MAIN_GATE_GRAND;
    public const int GOLD_DUNGEON_CHAPTER_GATE = 1;
    public const int BADGE_DUNGEON_CHAPTER_GATE = 1;
    public const int SCROLL_DUNGEON_CHAPTER_GATE = 1;
    public const int GIFT_DUNGEON_CHAPTER_GATE = 1;
    public const int ARENA_UNLOCK_SUB0 = 4;               // 第 5 关（前期档，不动）
    public const int MERC_RECRUIT_GRAND_GATE = 1;         // 退役兼容常量
    public const int MERC_INTEL_CHAPTER_GATE = 1;         // 判定已改第 8 关，见 MercIntelOpen
    public const int MERC_ASCEND_CHAPTER_GATE = 1;        // 判定已改第 8 关，见 MercAscendOpen
    public const int MERC_BOND_CHAPTER_GATE = 1;          // 判定已改第 8 关，见 MercBondOpen

    // ────────────────────────────────────────────────────────────────
    // 判定（业务代码只调这些）
    // ────────────────────────────────────────────────────────────────

    /// <summary>早档：通关第 6 关。</summary>
    static bool EarlyGate(SaveData data)
        => data != null && data.HasStageCleared(1, EARLY_GATE_SUB0);

    /// <summary>中档：通关第 8 关（佣兵养成类）。</summary>
    static bool MidGate(SaveData data)
        => data != null && data.HasStageCleared(1, MID_GATE_SUB0);

    /// <summary>主档：第一章通关（第 10 关全清）。真源 = clearedStages 实时推导，不读缓存。</summary>
    static bool MainGate(SaveData data)
        => GrandCleared(data, MAIN_GATE_GRAND);

    /// <summary>无限裂隙是否开放（标签可切 + 可进本，两者必须同源，否则就是假开门）。</summary>
    public static bool RiftOpen(SaveData data) => MainGate(data);

    /// <summary>竞技场（试炼场）是否开放：第 5 关（前期档，2026-11-27 未动）。</summary>
    public static bool ArenaOpen(SaveData data)
        => data != null && data.HasStageCleared(1, ARENA_UNLOCK_SUB0);

    /// <summary>世界地图入口是否显示。</summary>
    public static bool WorldMapOpen(SaveData data) => MainGate(data);

    /// <summary>支线岛是否可派岛：通关第 6 关。</summary>
    public static bool SideIslandOpen(SaveData data) => EarlyGate(data);

    /// <summary>某类活动副本所属章节（兼容旧调用；四种副本现全在第 1 章内解锁）。</summary>
    public static int DungeonGateChapter(ActivityDungeonKind kind) => 1;

    /// <summary>某类活动副本是否已解锁（只判章节门槛，不管开放日）。
    /// 金币/徽记 = 第 6 关，卷轴/礼物 = 第一章通关（第 10 关）。</summary>
    public static bool DungeonOpen(SaveData data, ActivityDungeonKind kind)
    {
        switch (kind)
        {
            case ActivityDungeonKind.Scroll:
            case ActivityDungeonKind.Gift:
                return MainGate(data);
            case ActivityDungeonKind.Gold:
            case ActivityDungeonKind.Badge:
            default:
                return EarlyGate(data);
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

    /// <summary>佣兵招募（三选一）：第 5 关（前期档，2026-11-27 未动）。</summary>
    public static bool MercRecruitOpen(SaveData data)
        => data != null && data.HasStageCleared(1, ARENA_UNLOCK_SUB0);

    /// <summary>佣兵情报（每日答题）：通关第 8 关。</summary>
    public static bool MercIntelOpen(SaveData data) => MidGate(data);

    /// <summary>进阶 / 升星：通关第 8 关。</summary>
    public static bool MercAscendOpen(SaveData data) => MidGate(data);

    /// <summary>佣兵羁绊 / 酒馆闲话：通关第 8 关（闲话是输入，羁绊是出口）。</summary>
    public static bool MercBondOpen(SaveData data) => MidGate(data);

    // ────────────────────────────────────────────────────────────────
    // 提示文案（与判定同源，不许各写一份；全局关号 1~80）
    // ────────────────────────────────────────────────────────────────

    public static string RiftLockHint() => "通关第 10 关后开启无限裂隙";
    public static string ArenaLockHint() => "通关第 5 关后开启试炼场";
    public static string ActivityTabLockHint() => "通关第 6 关后开启活动副本";

    public static string DungeonLockHint(ActivityDungeonKind kind)
        => (kind == ActivityDungeonKind.Scroll || kind == ActivityDungeonKind.Gift)
           ? "通关第 10 关后开启" + ActivityDungeonRules.DungeonName(kind)
           : "通关第 6 关后开启" + ActivityDungeonRules.DungeonName(kind);

    public static string MercRecruitLockHint() => "通关第 5 关后开启佣兵招募";
    public static string MercQueueLockHint() => "完成新手引导后开启佣兵队列";
    public static string MercIntelLockHint() => "通关第 8 关后开启佣兵情报";
    public static string MercAscendLockHint() => "通关第 8 关后开启佣兵进阶";
    public static string MercBondLockHint() => "通关第 8 关后开启佣兵羁绊";

    public static string MercRecruitLockDesc() => "通关第 5 关后开启";
    public static string MercQueueLockDesc() => "完成新手引导开启";
    public static string MercIntelLockDesc() => "通关第 8 关开启";
    public static string MercBondLockDesc() => "通关第 8 关开启";

    // ────────────────────────────────────────────────────────────────
    // 通关结算「本关新开启」清单（2026-11-27 主人拍板：结算界面下方加一条框写开启的功能）
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 本关（普通难度首通）**新开启**的功能清单，多行文本；没有解锁任何东西 → null。
    /// 【2026-11-27 主人拍板】与上面门槛常量同源：改门槛时这里一起改，不许各写一份。
    /// 只在第一章（门槛全压在第一章内）与普通难度生效 —— 难度局复用同一批关卡号但不再解锁功能。
    /// 第 3 关（sub0=2）也返回：日常/周常任务解锁。宠物指路气泡同关会弹，
    /// BattleResultScreen 已把气泡锚到本框下方错开（先建框、气泡贴框底），两框不撞。
    /// </summary>
    public static string NewlyUnlockedText(int chapter, int stageIndex0, int difficulty)
    {
        if (chapter != 1 || difficulty > 0) return null;
        switch (stageIndex0)
        {
            case 2:                      // 第 3 关
                return "日常任务 · 周常任务";
            case ARENA_UNLOCK_SUB0:      // 第 5 关
                return "试炼场 · 佣兵招募 · 换职业";
            case EARLY_GATE_SUB0:        // 第 6 关
                return "支线岛 · 金币副本 · 徽记副本";
            case MID_GATE_SUB0:          // 第 8 关
                return "进阶升星 · 佣兵情报 · 羁绊闲话";
            case 9:                      // 第 10 关 = 第一章通关
                return "无限裂隙 · 世界地图\n卷轴副本 · 礼物副本 · 抽池第 4 格";
            default:
                return null;
        }
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
