using NUnit.Framework;

/// <summary>
/// EndlessRiftRules 纯逻辑单测（EditMode，传 SaveData 直测；SaveSystem.Instance 为空时全部 ?. 短路安全）。
/// 锚定拍板口径：
///   - 每日 2 次机会 × 15 体力（11-10 口径：3 → 2，打通一层扣 1 次）；本地 0 点刷新（10-28 UTC→本地）
///   - 保存点间隔 10 层、起始层 = 保存点 + 1（不重打保存层）
///   - 每周可主动重置 1 次（11-02 拍板，周一 0 点刷新），重置不动生涯最高
///   - 重打旧层不发首通：NoteBestStage/MarkCleared 双轨（本轮进度 vs 生涯最高）
/// </summary>
public class EndlessRiftRulesTests
{
    [Test]
    public void Constants_Pinned()
    {
        Assert.AreEqual(2, GameConfig.ENDLESS_DAILY_TRIES, "每日 2 次机会（11-10 口径：每打通一层消耗 1 次）");
        Assert.AreEqual(10, GameConfig.ENDLESS_SAVE_INTERVAL, "保存点每 10 层");
        Assert.AreEqual(15, StaminaSystem.ENDLESS_RIFT_COST, "进本 15 体力与每日 2 次配套");
    }

    [Test]
    public void EnsureDay_Null_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => EndlessRiftRules.EnsureDay(null));
    }

    [Test]
    public void EnsureDay_StaleDayKey_ResetsTries()
    {
        var data = new SaveData { endlessRiftDayKey = "19990101", endlessRiftTriesToday = 3 };
        EndlessRiftRules.EnsureDay(data);
        Assert.AreEqual(DayKeyUtil.TodayKey(), data.endlessRiftDayKey, "日键翻到本地今天");
        Assert.AreEqual(0, data.endlessRiftTriesToday, "跨天清零");
    }

    [Test]
    public void EnsureDay_SameDay_KeepsTries()
    {
        var data = new SaveData { endlessRiftDayKey = DayKeyUtil.TodayKey(), endlessRiftTriesToday = 2 };
        EndlessRiftRules.EnsureDay(data);
        Assert.AreEqual(2, data.endlessRiftTriesToday, "同日不清零");
    }

    [Test]
    public void TriesLeft_Null_ReturnsFull()
    {
        Assert.AreEqual(2, EndlessRiftRules.TriesLeft(null), "null 存档不炸，按满次数报");
    }

    [Test]
    public void TriesLeft_FreshSave_TwoPerDay()
    {
        Assert.AreEqual(2, EndlessRiftRules.TriesLeft(new SaveData()));
    }

    [Test]
    public void TriesLeft_UsedAndOverused()
    {
        var data = new SaveData();
        data.endlessRiftDayKey = DayKeyUtil.TodayKey();
        data.endlessRiftTriesToday = 1;
        Assert.AreEqual(1, EndlessRiftRules.TriesLeft(data));
        data.endlessRiftTriesToday = 5;   // 异常超领
        Assert.AreEqual(0, EndlessRiftRules.TriesLeft(data), "超领钳 0 不出负数");
    }

    // ---------- 保存点 / 层数 ----------

    [Test]
    public void CheckpointOf_FloorToInterval()
    {
        Assert.AreEqual(0, EndlessRiftRules.CheckpointOf(0));
        Assert.AreEqual(0, EndlessRiftRules.CheckpointOf(9), "9 层未到保存点 → 0");
        Assert.AreEqual(10, EndlessRiftRules.CheckpointOf(10));
        Assert.AreEqual(10, EndlessRiftRules.CheckpointOf(12), "本轮通 12 层 → 出生点保存层 10（不重打保存层口径）");
        Assert.AreEqual(40, EndlessRiftRules.CheckpointOf(47));
        Assert.AreEqual(0, EndlessRiftRules.CheckpointOf(-3), "负数钳 0");
    }

    [Test]
    public void BestStage_NullSafe()
    {
        Assert.AreEqual(0, EndlessRiftRules.BestStage(null));
        var data = new SaveData { endlessRiftBestStage = 47 };
        Assert.AreEqual(47, EndlessRiftRules.BestStage(data));
        data.endlessRiftBestStage = -5;
        Assert.AreEqual(0, EndlessRiftRules.BestStage(data), "异常负值钳 0");
    }

    // ---------- 每周重置（11-02 拍板：每周 1 次，周一 0 点刷新） ----------

    [Test]
    public void CanResetThisWeek()
    {
        Assert.IsFalse(EndlessRiftRules.CanResetThisWeek(null), "null 存档不给重置（fail-closed）");
        var data = new SaveData();   // 周键空 → 本周没重置过
        Assert.IsTrue(EndlessRiftRules.CanResetThisWeek(data));
        data.endlessRiftResetWeekKey = DayKeyUtil.WeekKey();
        Assert.IsFalse(EndlessRiftRules.CanResetThisWeek(data), "本周已重置过 → 不给");
        Assert.GreaterOrEqual(EndlessRiftRules.DaysUntilReset(data), 1, "已重置 → 至少等 1 天");
        Assert.LessOrEqual(EndlessRiftRules.DaysUntilReset(data), 7, "最多等 7 天（到周一）");
    }

    [Test]
    public void DaysUntilReset_CanResetNow_IsZero()
    {
        var data = new SaveData();   // 本周未重置
        Assert.AreEqual(0, EndlessRiftRules.DaysUntilReset(data), "现在就能重置 → 0");
    }
}
