/// <summary>
/// 看广告送佣兵活动纯数据定义（2026-10-31 立项，施工单
/// 《Docs/看广告送佣兵_艾拉活动_待办任务清单_2026-10-31.md》第一节）。
///
/// 一条累计轴四个里程碑，断签不清零（主人拍板）：
///   满 3 天 → 艾拉本体（H015，稀有档；已拥有则折算碎片，仿每日登录 mercOwned→折算口径）
///   满 5 天 → 艾拉碎片 ×10（够升 1 星）
///   满 7 天 → 艾拉碎片 ×20 + 中级徽记 ×10（够升 2 星）
///   满 10 天 → 艾拉碎片 ×40 + 中级徽记 ×20（够升 3 星）
/// 计 1 天的口径（2026-10-31 主人拍板，与活动面板设计稿副标题一致）：
///   **每天看满 AdsPerDay(3) 个成功激励视频才计 1 天**，当天看满即停，跨天重新计数。
/// 碎片合计 70 = MercStarDefs.FullStarFragTotal(Rare)，零溢出零缺口；礼包不发金币（主人原话
/// 「金币让玩家自己去获取 也算一种动力」）。
/// 徽记档位/职业不写死：运行时从花名册 H015 行反查（游侠 × MercStarDefs.BadgeTierOf），
/// 以后改稀有度/换佣兵只动 EventMercId。
/// </summary>
public static class AdMercEventDefs
{
    /// <summary>活动目标佣兵：H015 艾拉·风羽（2026-10-31 主人钦定，女弓手）。</summary>
    public const string EventMercId = "H015";

    /// <summary>里程碑数（4 个礼盒）。</summary>
    public const int MilestoneCount = 4;

    /// <summary>各里程碑所需累计看广告天数（本地日历日，一天最多 +1）。</summary>
    public static readonly int[] MilestoneDays = { 3, 5, 7, 10 };

    /// <summary>最后一档天数（达到即停止拉广告——「没有功能的不要累计」）。</summary>
    public static int MaxDay => MilestoneDays[MilestoneCount - 1];

    /// <summary>每天需看满多少个成功广告才计 1 天（2026-10-31 主人拍板「每天看够 3 个广告」）。</summary>
    public const int AdsPerDay = 3;

    // ---- 里程碑奖励数量（对齐 MercStarDefs 稀有档升星消耗 10/20/40 + 中级徽记 0/10/20）----
    public const int FragStage1 = 10;   // 满 5 天：碎片 ×10（1 星）
    public const int FragStage2 = 20;   // 满 7 天：碎片 ×20（2 星）
    public const int FragStage3 = 40;   // 满 10 天：碎片 ×40（3 星）
    public const int BadgeStage2 = 10;  // 满 7 天：中级徽记 ×10
    public const int BadgeStage3 = 20;  // 满 10 天：中级徽记 ×20

    /// <summary>激励视频点位名（RewardedAdBridge.ShowRewarded 用，埋点/急停日志可认）。</summary>
    public const string AdPlacement = "merc_event";

    /// <summary>
    /// 主人手调的进度条锚点（2026-10-31）：累计天数到达各里程碑时 ProgressFill.fillAmount 的值。
    /// 中间天数在相邻锚点间线性插值（实现见 AdMercEventSystem.DayToFillAmount）。
    /// </summary>
    public static readonly float[] MilestoneFillAmounts = { 0.15f, 0.40f, 0.65f, 0.95f };

    /// <summary>本体里程碑时玩家已拥有艾拉 → 折算碎片数（仿每日登录 mercOwned→折算，2026-10-31 口径）。</summary>
    public const int OwnedFallbackFrag = 10;
}
