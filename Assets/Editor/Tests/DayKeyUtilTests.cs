using NUnit.Framework;
using System;

/// <summary>
/// DayKeyUtil 纯逻辑单测（EditMode）。全项目 30+ 处「按本地自然日/周刷新」的唯一出口：
///   - TodayKey = 本地 yyyyMMdd（2026-10-28 由 UTC 切本地，0 点刷新）
///   - LegacyUtcDayKeyStillToday = UTC→本地切换的升级兼容判定（本地 0~8 点旧 UTC 键视同今天，不清零加次数）
///   - WeekKey = 周一 0 点刷新的绝对周序（不做 ISO 跨年回绕）
/// </summary>
public class DayKeyUtilTests
{
    [Test]
    public void TodayKey_IsEightDigitsLocalDate()
    {
        string key = DayKeyUtil.TodayKey();
        Assert.AreEqual(8, key.Length, "日键应为 yyyyMMdd 8 位");
        long parsed;
        Assert.IsTrue(long.TryParse(key, out parsed), "日键应可解析为数字");
        Assert.AreEqual(DateTime.Now.ToString("yyyyMMdd"), key, "日键应为本地日期（非 UTC）");
    }

    [Test]
    public void LegacyKey_NullOrEmpty_ReturnsFalse()
    {
        Assert.IsFalse(DayKeyUtil.LegacyUtcDayKeyStillToday(null));
        Assert.IsFalse(DayKeyUtil.LegacyUtcDayKeyStillToday(""));
    }

    [Test]
    public void LegacyKey_OldDate_ReturnsFalse()
    {
        // 一个肯定不是今天的键：1999-01-01（无论 UTC 还是本地都不会等于今天）
        Assert.IsFalse(DayKeyUtil.LegacyUtcDayKeyStillToday("19990101"));
    }

    [Test]
    public void LegacyKey_UtcToday_MatchesHourWindow()
    {
        // UTC 今天的键：仅当本地时间在 0~8 点（UTC 日期 = 本地日期-1）时视同「本地今天」。
        // 用与实现同源的时间条件断言一致性——白天跑恒 false，凌晨跑恒 true，两种情况都算对。
        string utcToday = DateTime.UtcNow.ToString("yyyyMMdd");
        bool expected = DateTime.Now.Hour < 8;
        Assert.AreEqual(expected, DayKeyUtil.LegacyUtcDayKeyStillToday(utcToday),
            "UTC 今日键的判定应与「本地 Hour<8」窗口一致（2026-10-28 升级兼容拍板）");
    }

    [Test]
    public void WeekKey_IsYearWIndex_MondayAnchored()
    {
        string key = DayKeyUtil.WeekKey();
        StringAssert.IsMatch(@"^\d{4}W\d+$", key, "周键格式应为 {年份}W{绝对周序}");

        // 用同一套口径复算周序做回归锚：周一锚定 1970-01-05，绝对序号不回绕
        var now = DateTime.Now;
        int dowFromMonday = ((int)now.DayOfWeek + 6) % 7;
        var monday = now.Date.AddDays(-dowFromMonday);
        int expectedIndex = (int)((monday - new DateTime(1970, 1, 5)).TotalDays / 7);
        StringAssert.EndsWith("W" + expectedIndex, key, "周序应锚定本周周一（2026-11-03 赛季改自然周口径）");
        StringAssert.StartsWith(now.Year.ToString(), key, "周键前缀应为当前年份");
    }
}
