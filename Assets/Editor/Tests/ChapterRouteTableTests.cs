using NUnit.Framework;
using System.Collections.Generic;

/// <summary>
/// ChapterRouteTable 纯逻辑单测（EditMode）。
/// 数据真源 = Assets/Resources/Data/Tables/chapter_route.bytes（EditMode 可加载）；
/// 2026-10-23 合并方案后主线为**线性 1→8**，reqCleared=前一章；表缺失走硬编码兜底，行为一致。
/// 锚定拍板口径：
///   - 第 1 章永远开放；每章解锁 = 通关前一章（reqCleared 由表给）
///   - 难度逐章解锁（10-24 拍板）：困难全局门槛=通 1 章、噩梦=通 3 章，之后上一章同难度通了才开下一章
///   - CurrentChapterToAdvance：可选集合里第一个未通关的；全通关 → 第 8 章（NG+ 重打入口）；纯查询不掷点
///   - 支线岛派定门槛=通关第 1 大关（10-29 拍板改回）；当前岛没打完/两条已派满 → 幂等不重掷
///     🔴 真正掷点路径会调 SaveSystem.Instance.Save()，EditMode 无实例会 NRE——只测返回前的短路分支
/// </summary>
public class ChapterRouteTableTests
{
    // ---------- 路线：线性 1→8 ----------

    [Test]
    public void NextChapters_LinearOneToEight()
    {
        CollectionAssert.AreEqual(new[] { 2 }, ChapterRouteTable.NextChapters(1), "合并方案后第1章下一章只有2（分叉退役）");
        CollectionAssert.AreEqual(new[] { 3 }, ChapterRouteTable.NextChapters(2));
        CollectionAssert.AreEqual(new[] { 8 }, ChapterRouteTable.NextChapters(7));
        Assert.AreEqual(0, ChapterRouteTable.NextChapters(8).Count, "第8章是终章，无下一章");
        Assert.AreEqual(0, ChapterRouteTable.NextChapters(0).Count, "非法章号 → 空（fail-closed）");
        Assert.AreEqual(0, ChapterRouteTable.NextChapters(9).Count);
    }

    [Test]
    public void DefaultNext_LastChapterIsMinusOne()
    {
        Assert.AreEqual(2, ChapterRouteTable.DefaultNext(1));
        Assert.AreEqual(8, ChapterRouteTable.DefaultNext(7));
        Assert.AreEqual(-1, ChapterRouteTable.DefaultNext(8), "终章返回 -1（终局）");
        Assert.AreEqual(-1, ChapterRouteTable.DefaultNext(0), "非法章号 → -1");
    }

    [Test]
    public void NextChapter_IgnoresSaveData_LinearEra()
    {
        // 2026-10-23 合并方案后 NextChapter 等价 DefaultNext，data 参数仅为兼容保留
        var data = new SaveData();
        data.clearedChapterIds.Add(5);
        Assert.AreEqual(2, ChapterRouteTable.NextChapter(1, data));
    }

    // ---------- CanEnter / AvailableChapters ----------

    [Test]
    public void CanEnter_Chapter1AlwaysOpen()
    {
        Assert.IsTrue(ChapterRouteTable.CanEnter(1, null));
        Assert.IsTrue(ChapterRouteTable.CanEnter(1, new SaveData()));
    }

    [Test]
    public void CanEnter_OutOfBounds_FailClosed()
    {
        Assert.IsFalse(ChapterRouteTable.CanEnter(0, new SaveData()));
        Assert.IsFalse(ChapterRouteTable.CanEnter(9, new SaveData()));
        Assert.IsFalse(ChapterRouteTable.CanEnter(-1, null));
    }

    [Test]
    public void CanEnter_SequentialUnlock()
    {
        var data = new SaveData();
        Assert.IsFalse(ChapterRouteTable.CanEnter(2, data), "没通第1章 → 第2章锁");
        data.clearedChapterIds.Add(1);
        Assert.IsTrue(ChapterRouteTable.CanEnter(2, data), "通第1章 → 第2章开");
        Assert.IsFalse(ChapterRouteTable.CanEnter(3, data), "通第1章 ≠ 开第3章（线性按序，reqCleared=2）");
        data.clearedChapterIds.Add(2);
        Assert.IsTrue(ChapterRouteTable.CanEnter(3, data));
    }

    [Test]
    public void CanEnter_NullData_OnlyChapter1()
    {
        CollectionAssert.AreEqual(new[] { 1 }, ChapterRouteTable.AvailableChapters(null), "null 存档 → 只有第1章可进");
    }

    [Test]
    public void AvailableChapters_Ascending()
    {
        var data = new SaveData();
        data.clearedChapterIds.Add(1);
        data.clearedChapterIds.Add(2);
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, ChapterRouteTable.AvailableChapters(data), "升序，冒险页箭头在此集合内循环");
    }

    // ---------- 当前应推进的章 ----------

    [Test]
    public void CurrentChapterToAdvance()
    {
        Assert.AreEqual(1, ChapterRouteTable.CurrentChapterToAdvance(null), "null 存档 → 从第1章开始");

        var fresh = new SaveData();
        Assert.AreEqual(1, ChapterRouteTable.CurrentChapterToAdvance(fresh), "新档 → 第1章");

        var mid = new SaveData();
        mid.clearedChapterIds.Add(1);
        mid.clearedChapterIds.Add(2);
        mid.clearedChapterIds.Add(3);
        Assert.AreEqual(4, ChapterRouteTable.CurrentChapterToAdvance(mid), "可选集合里第一个未通关的章");

        var all = new SaveData();
        for (int ch = 1; ch <= 8; ch++) all.clearedChapterIds.Add(ch);
        Assert.AreEqual(8, ChapterRouteTable.CurrentChapterToAdvance(all), "全通关 → 第8章（NG+ 重打入口）");
    }

    // ---------- 难度逐章解锁（10-24 拍板） ----------

    [Test]
    public void CanEnterAtDiff_NormalDiff_DelegatesToCanEnter()
    {
        var data = new SaveData();
        Assert.IsFalse(ChapterRouteTable.CanEnterAtDiff(2, 0, data), "普通难度仍按前一章通关解锁");
        data.clearedChapterIds.Add(1);
        Assert.IsTrue(ChapterRouteTable.CanEnterAtDiff(2, 0, data));
    }

    [Test]
    public void CanEnterAtDiff_GlobalGates_Pinned()
    {
        Assert.AreEqual(1, GameConfig.DIFF_HARD_NEED_CLEARS, "困难门槛=通关 1 章");
        Assert.AreEqual(3, GameConfig.DIFF_NIGHTMARE_NEED_CLEARS, "噩梦门槛=通关 3 章");

        var fresh = new SaveData();
        Assert.IsFalse(ChapterRouteTable.CanEnterAtDiff(1, 1, fresh), "困难：0 章 → 第1章也锁");
        Assert.IsFalse(ChapterRouteTable.CanEnterAtDiff(1, 2, fresh), "噩梦：0 章 → 第1章也锁");

        var one = new SaveData();
        one.clearedChapterIds.Add(1);
        Assert.IsTrue(ChapterRouteTable.CanEnterAtDiff(1, 1, one), "困难：通 1 章 → 第1章开");
        Assert.IsFalse(ChapterRouteTable.CanEnterAtDiff(1, 2, one), "噩梦：通 1 章 < 3 → 锁");
    }

    [Test]
    public void CanEnterAtDiff_ChapterByChapter()
    {
        var data = new SaveData();
        data.clearedChapterIds.Add(1);   // 全局门槛过（困难）
        data.clearedChapterDiffKeys.Add("1_1");   // 第1章困难已通
        Assert.IsTrue(ChapterRouteTable.CanEnterAtDiff(1, 1, data));
        Assert.IsTrue(ChapterRouteTable.CanEnterAtDiff(2, 1, data), "上一章困难通了 → 第2章困难开");
        Assert.IsFalse(ChapterRouteTable.CanEnterAtDiff(3, 1, data), "第2章困难没通 → 第3章困难锁");
    }

    [Test]
    public void CanEnterAtDiff_UnknownDiff_FailClosed()
    {
        var data = new SaveData();
        data.clearedChapterIds.Add(1);
        data.clearedChapterDiffKeys.Add("1_1");
        Assert.IsFalse(ChapterRouteTable.CanEnterAtDiff(2, 99, data), "未知难度 → false（fail-closed）");
    }

    [Test]
    public void DiffChapterLockHint_SingleExit()
    {
        Assert.AreEqual("请把前面的关卡通关后开启", ChapterRouteTable.DiffChapterLockHint, "主人原话口径，冒险页/世界地图共用");
    }

    // ---------- 支线岛派定：幂等短路分支（不触 SaveSystem） ----------

    [Test]
    public void EnsureForkRoll_Null_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => ChapterRouteTable.EnsureForkRoll(null));
    }

    [Test]
    public void EnsureForkRoll_GateNotOpen_NoRoll()
    {
        var data = new SaveData();   // 没通第 1 大关 → 派岛门槛没到（10-29 拍板改回）
        ChapterRouteTable.EnsureForkRoll(data);
        Assert.AreEqual(0, data.branchRoll1, "门槛没到不派岛");
        Assert.AreEqual(0, data.branchRoll2);
    }

    [Test]
    public void EnsureForkRoll_TwoSlotsFull_Idempotent()
    {
        var data = new SaveData();
        data.branchRoll1 = 3;
        data.branchRoll2 = 5;   // 两条制已派满
        ChapterRouteTable.EnsureForkRoll(data);
        Assert.AreEqual(3, data.branchRoll1, "已派满 → 幂等不重掷");
        Assert.AreEqual(5, data.branchRoll2);
    }

    [Test]
    public void EnsureForkRoll_CurrentIslandUncleared_KeepsAssignment()
    {
        var data = new SaveData();
        data.clearedChapterIds.Add(1);   // 过第 1 大关门槛
        data.branchRoll1 = 2;            // 派了岛 2 还没打完
        ChapterRouteTable.EnsureForkRoll(data);
        Assert.AreEqual(2, data.branchRoll1, "当前岛没打完 → 保留不重掷");
        Assert.AreEqual(0, data.branchRoll2, "也不提前派第二个");
    }
}
