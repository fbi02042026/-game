using System;

/// <summary>
/// 全项目的「本地日键」口径（唯一出口）。
///
/// 由来：这两个方法原先挂在 <c>ShopDefs</c> 上（因为最早只有商店用「每日限购」需要日键）。
/// 2026-10-31 主人拍板删掉商店功能，但日键已经被每日登录 / 复活次数 / 分享奖励 / 无限裂隙 /
/// 四副本 / 好评弹窗 / 任务等 30+ 处共用 —— 直接随 ShopDefs 删会全线断链，
/// 故原样搬到这里，语义不变、调用方只换类名。
///
/// ⚠ 以后凡是要「按本地自然日刷新」的东西，一律用 <see cref="TodayKey"/>，
/// 不要再各自写 <c>DateTime.Now.ToString("yyyyMMdd")</c>。
/// </summary>
public static class DayKeyUtil
{
    /// <summary>本地时间日键（yyyyMMdd），0 点刷新。</summary>
    public static string TodayKey() => DateTime.Now.ToString("yyyyMMdd");

    /// <summary>
    /// 【2026-10-28 升级兼容·日键 UTC→本地切换】旧 UTC 日键是否仍代表「本地今天」的已领/已用记录。
    /// 背景：2026-10-28 把 广告补给/复活/分享/裂隙/四副本/好评弹窗 的日键从 UTC 统一切到本地日（0 点刷新）。
    /// UTC+8 下，本地 0:00~7:59 期间 UTC 日期仍是「昨天」——玩家在这个窗口装上新包时，旧档里的 UTC 键
    /// 恰好等于「本地的今天」，若按「键≠今日」直接清零，会让玩家当天多领一轮次数/奖励（主人拍板：不能清零加次数）。
    /// 命中本判定时，调用方应把 dayKey <b>平移写成本地今日并保留计数</b>（或视同"今日已领/已弹"），
    /// 不清零。平移写入后键 == 本地今日，后续调用天然短路，幂等。
    /// 中国无夏令时，Hour &lt; 8 ⟺ UTC 日期 = 本地日期 − 1，判定恒成立。
    /// </summary>
    public static bool LegacyUtcDayKeyStillToday(string legacyKey)
        => !string.IsNullOrEmpty(legacyKey)
           && DateTime.Now.Hour < 8
           && legacyKey == DateTime.UtcNow.ToString("yyyyMMdd");
}
