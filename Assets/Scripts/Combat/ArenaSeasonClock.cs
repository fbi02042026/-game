using System;
using UnityEngine;

/// <summary>
/// 竞技场赛季时钟（2026-11-11 主人拍板：通用 N 秒周期；正式定 1 周赛季，周一 0 点北京时间结算）。
/// · 纯时间推导，不落存档位：冻结态每次按当前时钟实时算，关游戏 / 重进都绕不过去；
/// · 赛季周期 = <see cref="SeasonPeriodSeconds"/>（正式 604800 = 7 天；临时验证可改 300 = 5 分钟）；
/// · 冻结窗口 = 赛季最后 <see cref="FreezeSeconds"/> 秒（周周期 = 最后 1 小时冻结）；
/// · 新赛季 0 秒点解冻并重置（积分清零 / 机器人重建 / 按最终名次发奖，结算体仍走 ArenaRules.EnsureSeason）；
/// · 倒计时提示 &amp; 文案随周期缩放：短周期显示秒/分钟、长周期显示天。
/// </summary>
public static class ArenaSeasonClock
{
    // ───────────────────────── 集中常量（改周期只改这一个） ─────────────────────────
    /// <summary>赛季周期（秒）。2026-11-11 主人拍板正式定 604800 = 7 天（1 周赛季）；临时验证可改 300 = 5 分钟。</summary>
    public const int SeasonPeriodSeconds = 604800;

    /// <summary>每个赛季最后多少秒冻结榜单（不能挑战、名次不动）。
    /// 周周期 = 3600（最后 1 小时，2026-11-11 随周赛季一起定）；临时验证可改 30 秒。</summary>
    public const int FreezeSeconds = 3600;

    /// <summary>倒计时提示提前多久开始弹（秒）。默认周期的 1/5；5 分钟周期 = 最后 1 分钟开始提示。
    /// 长周期（月/周）可单独覆盖；这里用比例保持短周期测试体验。</summary>
    public static int CountdownPromptSeconds => Mathf.Max(60, SeasonPeriodSeconds / 5);

    /// <summary>一句话规则（可直接上界面：排行奖励面板 / 结算倒计时提示框）。周期变化时自动选合适的单位。</summary>
    public static string RuleLine
    {
        get
        {
            if (SeasonPeriodSeconds <= 3600)   // ≤ 1 小时：5分钟测试档
                return $"每 {SeasonPeriodSeconds} 秒一个赛季，赛季最后 {FreezeSeconds} 秒冻结排行榜。";
            if (SeasonPeriodSeconds <= 86400)   // ≤ 1 天：半天/小时档
                return $"每 {SeasonPeriodSeconds / 60} 分钟一个赛季，最后 {FreezeSeconds / 60} 分钟冻结。";
            if (SeasonPeriodSeconds <= 2592000) // ≤ 1 月：周档
                return $"每 {SeasonPeriodSeconds / 86400} 天一个赛季，最后 {FreezeSeconds / 3600} 小时冻结。";
            return $"每 {SeasonPeriodSeconds / 2592000} 个月一个赛季，最后一天冻结。";
        }
    }

    // ───────────────────────── 赛季键 & 时间推导 ─────────────────────────

    static long NowUnix(long nowUnix) => nowUnix > 0 ? nowUnix : DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>当前赛季起始 Unix 秒（= 本周一 0 点北京时间）。316800 = 1970-01-05 周一 0 点北京时间的 unix 秒：unix 纪元本身落在周四，直接除周期会得「周四早 8 点」边界——减偏移再除才对齐周一 0 点（2026-11-11 主人拍板，与周常任务/周轮换武器同节奏）。</summary>
    public static long SeasonStartUnix(long nowUnix = 0)
    {
        long now = NowUnix(nowUnix) - 316800;   // 周一 0 点（北京时间）对齐偏移
        return (now / SeasonPeriodSeconds) * SeasonPeriodSeconds + 316800;
    }

    /// <summary>下一个赛季起始 Unix 秒。</summary>
    public static long NextSeasonStartUnix(long nowUnix = 0)
        => SeasonStartUnix(nowUnix) + SeasonPeriodSeconds;

    /// <summary>当前赛季键（存 SaveData.arenaMonthKey 里，string 类型）。用赛季起始 unix 秒做键，天然周期对齐。</summary>
    public static string SeasonKey(long nowUnix = 0) => SeasonStartUnix(nowUnix).ToString();

    /// <summary>冻结开始 Unix 秒（= 下赛季起点 − FreezeSeconds）。</summary>
    public static long FreezeStartUnix(long nowUnix = 0)
        => NextSeasonStartUnix(nowUnix) - FreezeSeconds;

    // ───────────────────────── 状态查询 ─────────────────────────

    /// <summary>是否处于结算冻结期：冻结期内不能挑战、榜单不再变动。</summary>
    public static bool IsFrozen(long nowUnix = 0)
    {
        long now = NowUnix(nowUnix);
        return now >= FreezeStartUnix(now) && now < NextSeasonStartUnix(now);
    }

    /// <summary>距结算冻结开始还剩多少秒（0 = 已进入冻结期；负数同理 0）。</summary>
    public static long SecondsUntilSettle(long nowUnix = 0)
    {
        long now = NowUnix(nowUnix);
        long freeze = FreezeStartUnix(now);
        if (now >= freeze) return 0;
        return freeze - now;
    }

    /// <summary>跨赛季判断：baseline 是否在当前赛季之前（离线时间被上一个赛季的结算消化了，不再二次扣）。</summary>
    public static bool CrossSeason(long baselineUnix, long nowUnix = 0)
    {
        if (baselineUnix <= 0) return false;
        return baselineUnix < SeasonStartUnix(nowUnix);
    }

    // ───────────────────────── 文案 & 提示 ─────────────────────────

    /// <summary>倒计时信息框 headline（简短版）。</summary>
    public static string CountdownHeadline(long secondsLeft)
    {
        if (secondsLeft <= 0) return "本赛季结算中";
        if (SeasonPeriodSeconds <= 3600)
            return $"距结算还剩 {secondsLeft} 秒";
        if (SeasonPeriodSeconds <= 86400)
            return $"距结算还剩 {secondsLeft / 60} 分钟";
        if (SeasonPeriodSeconds <= 2592000)
            return $"距结算还剩 {secondsLeft / 3600} 小时";
        int days = Mathf.CeilToInt((float)secondsLeft / 86400f);
        return $"距结算还剩 {days} 天";
    }

    /// <summary>倒计时信息框正文（只放规则与冻结说明，headline 由调用方给）。</summary>
    public static string CountdownText(long secondsLeft) => secondsLeft <= 0
        ? $"排行榜已冻结，下赛季开启后按最终名次发放奖励并清分开榜。\n\n{RuleLine}"
        : $"{RuleLine}\n\n结算冻结期内无法挑战、名次不再变动，下赛季开启后自动重开。";

    /// <summary>今天/本次周期是否该弹倒计时提示框：距结算 ≤ CountdownPromptSeconds 且没弹过。</summary>
    public static bool ShouldPrompt(SaveData data, long nowUnix = 0)
    {
        if (data == null) return false;
        if (SecondsUntilSettle(nowUnix) > CountdownPromptSeconds) return false;
        // 5 分钟短周期 → 用"当前赛季起始秒"做去重键（一个赛季只弹一次）；长周期保留"按天去重"
        if (SeasonPeriodSeconds <= 86400)
            return data.arenaSeasonHintDayKey != SeasonKey(nowUnix);
        return data.arenaSeasonHintDayKey != DayKeyUtil.TodayKey();
    }

    /// <summary>记下已弹过（短周期按赛季键、长周期按天键，自动适配）。</summary>
    public static void MarkPrompted(SaveData data, long nowUnix = 0)
    {
        if (data == null) return;
        data.arenaSeasonHintDayKey = SeasonPeriodSeconds <= 86400
            ? SeasonKey(nowUnix)
            : DayKeyUtil.TodayKey();
        SaveSystem.Instance?.Save();
    }
}
