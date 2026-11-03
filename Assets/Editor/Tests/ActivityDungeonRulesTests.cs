using NUnit.Framework;

/// <summary>
/// ActivityDungeonRules + FeatureUnlock（活动副本）纯逻辑单测（EditMode）。
/// 锚定拍板口径：
///   - 每日次数三本各自独立计数，都是 2 次（2026-10-24 拍板）
///   - 体力：金币/徽记/礼物 10、卷轴 15（2026-10-24 拍板；礼物 10-26 未单独给价按同价）
///   - 章节门槛：金币=第1章、徽记=第2章、卷轴=第3章、礼物=第1章（FeatureUnlock 唯一出口）
///   - 战斗场景章：金=1 森林 / 徽=2 亡灵 / 卷=6 洞穴 / 礼=6 暂同洞穴（2026-10-24 拍板）
///   - TEST_OPEN_ALL=true（2026-10-31 拍板：测试期三本全开、难度全开、显示层不旁路）
///     → 翻 false 恢复开放日/章节门槛/难度逐级解锁后，IsOpenToday/IsDiffUnlocked 用例会红，
///       到时按正式口径改期望值，不许删测试绕过。
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
        Assert.AreEqual(true, ActivityDungeonRules.TEST_OPEN_ALL, "测试总闸当前=开（10-31 拍板，打包前改回）");
    }

    [Test]
    public void EntryStaminaCost_ScrollFifteen_OthersTen()
    {
        Assert.AreEqual(15, ActivityDungeonRules.EntryStaminaCost(ActivityDungeonKind.Scroll));
        Assert.AreEqual(10, ActivityDungeonRules.EntryStaminaCost(ActivityDungeonKind.Gold));
        Assert.AreEqual(10, ActivityDungeonRules.EntryStaminaCost(ActivityDungeonKind.Badge));
        Assert.AreEqual(10, ActivityDungeonRules.EntryStaminaCost(ActivityDungeonKind.Gift), "礼物本未单独给价，按金币同价（10-26 口径）");
    }

    // ---------- 测试总闸旁路（翻 false 后这两条按正式口径重写） ----------

    [Test]
    public void IsOpenToday_TestGateOpen_AllKinds()
    {
        Assert.IsTrue(ActivityDungeonRules.IsOpenToday(ActivityDungeonKind.Gold));
        Assert.IsTrue(ActivityDungeonRules.IsOpenToday(ActivityDungeonKind.Badge));
        Assert.IsTrue(ActivityDungeonRules.IsOpenToday(ActivityDungeonKind.Scroll));
        Assert.IsTrue(ActivityDungeonRules.IsOpenToday(ActivityDungeonKind.Gift));
    }

    [Test]
    public void IsDiffUnlocked_TestGateOpen_AllDifficulties()
    {
        var data = new SaveData();   // activityDungeonClearedDiff 默认 -1（未通任何难度）
        Assert.IsTrue(ActivityDungeonRules.IsDiffUnlocked(data, 0), "总闸开：初级开");
        Assert.IsTrue(ActivityDungeonRules.IsDiffUnlocked(data, 1), "总闸开：中级不看进度");
        Assert.IsTrue(ActivityDungeonRules.IsDiffUnlocked(data, 2), "总闸开：高级不看进度");
        Assert.IsTrue(ActivityDungeonRules.IsDiffUnlocked(null, 2), "null 存档不炸（总闸开恒真）");
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

    // ---------- 章节门槛（FeatureUnlock 唯一出口） ----------

    [Test]
    public void GateChapter_DelegatesToFeatureUnlock_Pinned()
    {
        Assert.AreEqual(1, ActivityDungeonRules.GateChapter(ActivityDungeonKind.Gold), "金币=第1章（D2）");
        Assert.AreEqual(2, ActivityDungeonRules.GateChapter(ActivityDungeonKind.Badge), "徽记=第2章（与金币错开）");
        Assert.AreEqual(3, ActivityDungeonRules.GateChapter(ActivityDungeonKind.Scroll), "卷轴=第3章");
        Assert.AreEqual(1, ActivityDungeonRules.GateChapter(ActivityDungeonKind.Gift), "礼物=第1章");
    }

    [Test]
    public void DungeonOpen_ByClearedChapters()
    {
        var data = new SaveData();
        data.clearedChapterIds.Add(1);
        Assert.IsTrue(FeatureUnlock.DungeonOpen(data, ActivityDungeonKind.Gold), "通了第1章 → 金币本开");
        Assert.IsFalse(FeatureUnlock.DungeonOpen(data, ActivityDungeonKind.Badge), "没通第2章 → 徽记本关");
        Assert.IsFalse(FeatureUnlock.DungeonOpen(data, ActivityDungeonKind.Scroll), "没通第3章 → 卷轴本关");
        Assert.IsFalse(FeatureUnlock.DungeonOpen(null, ActivityDungeonKind.Gold), "null 存档不炸且判未解锁（fail-closed）");
        Assert.IsFalse(FeatureUnlock.AnyDungeonOpen(null));
    }

    [Test]
    public void AnyDungeonOpen_AnyOneUnlocked()
    {
        var data = new SaveData();
        Assert.IsFalse(FeatureUnlock.AnyDungeonOpen(data), "全没通 → 标签页不可切");
        data.clearedChapterIds.Add(2);
        Assert.IsTrue(FeatureUnlock.AnyDungeonOpen(data), "只通第2章（徽记）→ 标签页可切");
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
