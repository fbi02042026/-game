using System;
using NUnit.Framework;

/// <summary>
/// ActivityDungeonRules + FeatureUnlock（活动副本）纯逻辑单测（EditMode）。
/// 锚定拍板口径：
///   - 每日次数三本各自独立计数，都是 2 次（2026-10-24 拍板）
///   - 体力：金币/徽记/礼物 10、卷轴 15（2026-10-24 拍板；礼物 10-26 未单独给价按同价）
///   - 关卡门槛（2026-11-27 定稿·17:45 提交 eada4b8a，覆盖当日 16:25 的 6/10 旧版）：按**全局关号 1~80** 分两档——
///     徽记 = 通关第 8 关（随进阶升星同批）；金币 / 卷轴 / 礼物 = 通关第 12 关（活动副本放一起）。FeatureUnlock 唯一出口。
///   - 战斗场景章：金=1 森林 / 徽=2 亡灵 / 卷=6 洞穴 / 礼=6 暂同洞穴（2026-10-24 拍板；与解锁门槛无关）
///   - TEST_OPEN_ALL=false（2026-11-07 主人拍板：正式包恢复开放日 + 难度逐级解锁）
///     → 回测试期翻 true 时，下面两条正式口径用例要按测试口径改回期望值，不许删测试绕过。
/// </summary>
public class ActivityDungeonRulesTests
{
    // ---------- 常量锚定 ----------

    [Test]
    public void Constants_Pinned()
    {
        Assert.AreEqual(2, ActivityDungeonRules.DAILY_TRIES, "每日每本 2 次（10-24 拍板）");
        Assert.AreEqual(10, ActivityDungeonRules.ENTRY_STAMINA_COST, "金币/徽记进本 10 体力");
        Assert.AreEqual(15, ActivityDungeonRules.SCROLL_ENTRY_STAMINA_COST, "卷轴进本 15 体力");
        Assert.AreEqual(false, ActivityDungeonRules.TEST_OPEN_ALL, "正式包测试总闸=关（11-07 拍板改回）");
    }

    [Test]
    public void EntryStaminaCost_ScrollFifteen_OthersTen()
    {
        Assert.AreEqual(15, ActivityDungeonRules.EntryStaminaCost(ActivityDungeonKind.Scroll));
        Assert.AreEqual(10, ActivityDungeonRules.EntryStaminaCost(ActivityDungeonKind.Gold));
        Assert.AreEqual(10, ActivityDungeonRules.EntryStaminaCost(ActivityDungeonKind.Badge));
        Assert.AreEqual(10, ActivityDungeonRules.EntryStaminaCost(ActivityDungeonKind.Gift), "礼物本未单独给价，按金币同价（10-26 口径）");
    }

    // ---------- 正式口径：按开放日 + 难度逐级解锁 ----------

    [Test]
    public void IsOpenToday_FormalGate_ByWeekday()
    {
        var d = DateTime.Now.DayOfWeek;
        Assert.AreEqual(d == DayOfWeek.Monday || d == DayOfWeek.Wednesday || d == DayOfWeek.Saturday,
            ActivityDungeonRules.IsOpenToday(ActivityDungeonKind.Gold), "金币本=周一/三/六");
        Assert.AreEqual(d == DayOfWeek.Tuesday || d == DayOfWeek.Thursday || d == DayOfWeek.Sunday,
            ActivityDungeonRules.IsOpenToday(ActivityDungeonKind.Badge), "徽记本=周二/四/日");
        Assert.AreEqual(d == DayOfWeek.Friday,
            ActivityDungeonRules.IsOpenToday(ActivityDungeonKind.Scroll), "卷轴本=周五");
        Assert.AreEqual(d == DayOfWeek.Saturday,
            ActivityDungeonRules.IsOpenToday(ActivityDungeonKind.Gift), "礼物本=周六");
    }

    [Test]
    public void IsDiffUnlocked_FormalGate_Progressive()
    {
        var data = new SaveData();   // activityDungeonClearedDiff 默认 -1（未通任何难度）
        Assert.IsTrue(ActivityDungeonRules.IsDiffUnlocked(data, 0), "初级恒开");
        Assert.IsFalse(ActivityDungeonRules.IsDiffUnlocked(data, 1), "没通初级 → 中级锁");
        Assert.IsFalse(ActivityDungeonRules.IsDiffUnlocked(data, 2), "没通中级 → 高级锁");
        Assert.IsFalse(ActivityDungeonRules.IsDiffUnlocked(null, 2), "null 存档不炸且 fail-closed");
        data.activityDungeonClearedDiff = 0;
        Assert.IsTrue(ActivityDungeonRules.IsDiffUnlocked(data, 1), "通了初级 → 中级开");
        Assert.IsFalse(ActivityDungeonRules.IsDiffUnlocked(data, 2), "还没通中级 → 高级仍锁");
    }

    // ---------- 场景章 / 名称 / 提示（单一出口） ----------

    [Test]
    public void SceneChapter_Pinned()
    {
        Assert.AreEqual(1, ActivityDungeonRules.SceneChapter(ActivityDungeonKind.Gold), "金币=第1章 暮影森林");
        Assert.AreEqual(2, ActivityDungeonRules.SceneChapter(ActivityDungeonKind.Badge), "徽记=第2章 幽冥墓园");
        Assert.AreEqual(6, ActivityDungeonRules.SceneChapter(ActivityDungeonKind.Scroll), "卷轴=第6章 巨岩深窟");
        Assert.AreEqual(6, ActivityDungeonRules.SceneChapter(ActivityDungeonKind.Gift), "礼物暂同洞穴（无专属怪图回落口径）");
    }

    [Test]
    public void DungeonName_SingleExit()
    {
        Assert.AreEqual("金币副本", ActivityDungeonRules.DungeonName(ActivityDungeonKind.Gold));
        Assert.AreEqual("徽记副本", ActivityDungeonRules.DungeonName(ActivityDungeonKind.Badge));
        Assert.AreEqual("卷轴副本", ActivityDungeonRules.DungeonName(ActivityDungeonKind.Scroll));
        Assert.AreEqual("礼物副本", ActivityDungeonRules.DungeonName(ActivityDungeonKind.Gift));
        Assert.AreEqual("活动副本", ActivityDungeonRules.DungeonName((ActivityDungeonKind)999), "未知种类 fail-closed 占位");
    }

    [Test]
    public void OpenDayHint_SingleExit()
    {
        StringAssert.Contains("一、三、六", ActivityDungeonRules.OpenDayHint(ActivityDungeonKind.Gold));
        StringAssert.Contains("二、四、日", ActivityDungeonRules.OpenDayHint(ActivityDungeonKind.Badge));
        StringAssert.Contains("周五", ActivityDungeonRules.OpenDayHint(ActivityDungeonKind.Scroll));
        StringAssert.Contains("周六", ActivityDungeonRules.OpenDayHint(ActivityDungeonKind.Gift));
    }

    // ---------- 关卡门槛（FeatureUnlock 唯一出口） ----------

    [Test]
    public void GateChapter_DelegatesToFeatureUnlock_Pinned()
    {
        // 兼容旧调用：GateChapter 恒返回第 1 章（FeatureUnlock.DungeonGateChapter 唯一出口），
        // 真正的区分在 FeatureUnlock.DungeonOpen（徽记第 8 关 / 金币·卷轴·礼物第 12 关两档）。
        Assert.AreEqual(1, ActivityDungeonRules.GateChapter(ActivityDungeonKind.Gold), "金币=兼容值第1章（实际第12关解锁）");
        Assert.AreEqual(1, ActivityDungeonRules.GateChapter(ActivityDungeonKind.Badge), "徽记=兼容值第1章（实际第8关解锁）");
        Assert.AreEqual(1, ActivityDungeonRules.GateChapter(ActivityDungeonKind.Scroll), "卷轴=兼容值第1章（实际第12关解锁）");
        Assert.AreEqual(1, ActivityDungeonRules.GateChapter(ActivityDungeonKind.Gift), "礼物=兼容值第1章（实际第12关解锁）");
    }

    /// <summary>造一个「全局已通到第 n 关（1 基，8 章 × 每章 10 关）」的存档：逐关清 "章_关0基"。</summary>
    static SaveData ClearedUpTo(int stageNo1Based)
    {
        var d = new SaveData();
        for (int i = 0; i < stageNo1Based; i++)
            d.clearedStages.Add((i / 10 + 1) + "_" + (i % 10));
        return d;
    }

    [Test]
    public void DungeonOpen_ByStageGate()
    {
        var none = new SaveData();
        Assert.IsFalse(FeatureUnlock.DungeonOpen(none, ActivityDungeonKind.Gold), "一关没通 → 金币本关");
        Assert.IsFalse(FeatureUnlock.DungeonOpen(none, ActivityDungeonKind.Badge), "一关没通 → 徽记本关");
        Assert.IsFalse(FeatureUnlock.DungeonOpen(none, ActivityDungeonKind.Scroll), "一关没通 → 卷轴本关");
        Assert.IsFalse(FeatureUnlock.DungeonOpen(none, ActivityDungeonKind.Gift), "一关没通 → 礼物本关");

        var s7 = ClearedUpTo(7);
        Assert.IsFalse(FeatureUnlock.DungeonOpen(s7, ActivityDungeonKind.Gold), "只到第7关 → 金币本仍关（差第12关）");
        Assert.IsFalse(FeatureUnlock.DungeonOpen(s7, ActivityDungeonKind.Badge), "只到第7关 → 徽记本仍关（差第8关）");

        var s8 = ClearedUpTo(8);
        Assert.IsTrue(FeatureUnlock.DungeonOpen(s8, ActivityDungeonKind.Badge), "通第8关 → 徽记本开（随升阶同批）");
        Assert.IsFalse(FeatureUnlock.DungeonOpen(s8, ActivityDungeonKind.Gold), "第8关只开徽记 → 金币本仍关");
        Assert.IsFalse(FeatureUnlock.DungeonOpen(s8, ActivityDungeonKind.Scroll), "第8关只开徽记 → 卷轴本仍关");
        Assert.IsFalse(FeatureUnlock.DungeonOpen(s8, ActivityDungeonKind.Gift), "第8关只开徽记 → 礼物本仍关");

        var s10 = ClearedUpTo(10);
        Assert.IsTrue(FeatureUnlock.DungeonOpen(s10, ActivityDungeonKind.Badge), "第一章通关 → 徽记本早该开了");
        Assert.IsFalse(FeatureUnlock.DungeonOpen(s10, ActivityDungeonKind.Gold), "未通第12关 → 金币本仍关");
        Assert.IsFalse(FeatureUnlock.DungeonOpen(s10, ActivityDungeonKind.Scroll), "未通第12关 → 卷轴本仍关");

        var s11 = ClearedUpTo(11);
        Assert.IsFalse(FeatureUnlock.DungeonOpen(s11, ActivityDungeonKind.Gold), "只到第11关（2_0）→ 金币本仍关（差第12关）");

        var s12 = ClearedUpTo(12);
        Assert.IsTrue(FeatureUnlock.DungeonOpen(s12, ActivityDungeonKind.Gold), "通第12关（2_1）→ 金币本开");
        Assert.IsTrue(FeatureUnlock.DungeonOpen(s12, ActivityDungeonKind.Scroll), "通第12关 → 卷轴本开");
        Assert.IsTrue(FeatureUnlock.DungeonOpen(s12, ActivityDungeonKind.Gift), "通第12关 → 礼物本开");

        Assert.IsFalse(FeatureUnlock.DungeonOpen(null, ActivityDungeonKind.Gold), "null 存档不炸且判未解锁（fail-closed）");
        Assert.IsFalse(FeatureUnlock.AnyDungeonOpen(null));
    }

    [Test]
    public void AnyDungeonOpen_AnyOneUnlocked()
    {
        var data = new SaveData();
        Assert.IsFalse(FeatureUnlock.AnyDungeonOpen(data), "一关没通 → 标签页不可切");
        data = ClearedUpTo(7);
        Assert.IsFalse(FeatureUnlock.AnyDungeonOpen(data), "只到第7关 → 徽记/活动本都没开，标签页不可切");
        data = ClearedUpTo(8);
        Assert.IsTrue(FeatureUnlock.AnyDungeonOpen(data), "通第8关（徽记本开）→ 标签页可切");
    }

    // ---------- 每日次数（三本独立计数 + 日刷新） ----------

    [Test]
    public void TriesLeft_NullData_ReturnsFull()
    {
        Assert.AreEqual(2, ActivityDungeonRules.TriesLeft(null, ActivityDungeonKind.Gold), "null 存档不炸，按满次数报");
    }

    [Test]
    public void TriesLeft_FreshSave_FullPerKind()
    {
        var data = new SaveData();
        Assert.AreEqual(2, ActivityDungeonRules.TriesLeft(data, ActivityDungeonKind.Gold));
        Assert.AreEqual(2, ActivityDungeonRules.TriesLeft(data, ActivityDungeonKind.Badge));
        Assert.AreEqual(2, ActivityDungeonRules.TriesLeft(data, ActivityDungeonKind.Scroll));
        Assert.AreEqual(2, ActivityDungeonRules.TriesLeft(data, ActivityDungeonKind.Gift));
    }

    [Test]
    public void TriesLeft_IndependentCounters_PerKind()
    {
        var data = new SaveData();
        string today = DayKeyUtil.TodayKey();
        data.goldDungeonDayKey = today;
        data.goldDungeonTriesToday = 2;   // 金币本用满
        Assert.AreEqual(0, ActivityDungeonRules.TriesLeft(data, ActivityDungeonKind.Gold), "金币本用满 → 0");
        Assert.AreEqual(2, ActivityDungeonRules.TriesLeft(data, ActivityDungeonKind.Badge), "徽记本独立计数，不受金币影响");
        Assert.AreEqual(2, ActivityDungeonRules.TriesLeft(data, ActivityDungeonKind.Scroll), "卷轴本独立计数");
    }

    [Test]
    public void TriesLeft_Overused_ClampedToZero()
    {
        var data = new SaveData();
        data.scrollDungeonDayKey = DayKeyUtil.TodayKey();
        data.scrollDungeonTriesToday = 5;   // 异常超领
        Assert.AreEqual(0, ActivityDungeonRules.TriesLeft(data, ActivityDungeonKind.Scroll), "超领钳 0 不出负数");
    }

    [Test]
    public void TriesLeft_StaleDayKey_ResetsToFull()
    {
        var data = new SaveData();
        data.goldDungeonDayKey = "19990101";       // 陈旧日键
        data.goldDungeonTriesToday = 2;            // 昨天用满
        Assert.AreEqual(2, ActivityDungeonRules.TriesLeft(data, ActivityDungeonKind.Gold), "跨天 → 清零回满");
        Assert.AreEqual(DayKeyUtil.TodayKey(), data.goldDungeonDayKey, "日键已翻到今天");
        Assert.AreEqual(0, data.goldDungeonTriesToday, "今日已用次数清零");
    }

    [Test]
    public void TriesLeft_SameDay_KeepsCount()
    {
        var data = new SaveData();
        data.badgeDungeonDayKey = DayKeyUtil.TodayKey();
        data.badgeDungeonTriesToday = 1;
        Assert.AreEqual(1, ActivityDungeonRules.TriesLeft(data, ActivityDungeonKind.Badge), "同日重复查询不清零");
        Assert.AreEqual(1, data.badgeDungeonTriesToday, "已用次数保留");
    }
}
