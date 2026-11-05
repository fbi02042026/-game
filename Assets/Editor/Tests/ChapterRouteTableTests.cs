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

    /// <summary>把某篇 clearedStages 填成 [fromSub0..toSub0] 并刷新两套集合（真源写入的唯一入口）。</summary>
    static void FillStages(SaveData d, int chapter, int fromSub0, int toSub0)
    {
        for (int s = fromSub0; s <= toSub0; s++) d.clearedStages.Add(chapter + "_" + s);
        d.RefreshChapterTruth(chapter);
    }

    [Test]
    public void CurrentChapterToAdvance()
    {
        Assert.AreEqual(1, ChapterRouteTable.CurrentChapterToAdvance(null), "null 存档 → 从第1章开始");

        var fresh = new SaveData();
        Assert.AreEqual(1, ChapterRouteTable.CurrentChapterToAdvance(fresh), "新档 → 第1章");

        // 【2026-11-05】真源口径：只看 clearedStages，不看集合标签。
        var mid = new SaveData();
        FillStages(mid, 1, 0, ActProgressionDefs.StageCountOf(1) - 1);   // 第1篇真通关
        FillStages(mid, 2, 0, 4);                                        // 第2篇第1座大关
        Assert.AreEqual(2, ChapterRouteTable.CurrentChapterToAdvance(mid),
            "第1篇真通关 + 第2篇已开打 → 停第2篇（不回跳第1篇）");

        var all = new SaveData();
        for (int ch = 1; ch <= 8; ch++)
            FillStages(all, ch, 0, ActProgressionDefs.StageCountOf(ch) - 1);
        Assert.AreEqual(8, ChapterRouteTable.CurrentChapterToAdvance(all), "全通关 → 第8章（NG+ 重打入口）");
    }

    // ---------- 2026-11-05 回归：真源 / 两套集合 ----------

    [Test]
    public void Regression_A_OneGrandOnly_Chapter2StaysLocked()
    {
        var d = new SaveData();
        FillStages(d, 1, 0, ActProgressionDefs.SubsPerGrandOf(1) - 1);   // 只通第 1 座大关
        Assert.IsFalse(d.HasTrulyClearedChapter(1), "未覆盖全篇小关 → 不算真通关");
        Assert.IsFalse(d.IsChapterUnlocked(1), "真通关才是解锁来源 → 第1篇还不该标已解锁");
        Assert.IsFalse(ChapterRouteTable.CanEnter(2, d), "第 2 篇必须仍锁（误标来源已删除）");
        Assert.AreEqual(1, ChapterRouteTable.CurrentChapterToAdvance(d));
    }

    [Test]
    public void Regression_B_FullChapter_UnlocksNext()
    {
        var d = new SaveData();
        FillStages(d, 1, 0, ActProgressionDefs.StageCountOf(1) - 1);
        Assert.IsTrue(d.HasTrulyClearedChapter(1), "整篇小关全清 → 真通关");
        Assert.IsTrue(d.IsChapterUnlocked(1));
        Assert.IsTrue(ChapterRouteTable.CanEnter(2, d), "真通关 → 第 2 篇解锁");
        Assert.IsFalse(ChapterRouteTable.CanEnter(3, d), "线性按序：第 3 篇仍锁");
        Assert.AreEqual(2, ChapterRouteTable.CurrentChapterToAdvance(d), "整篇通完 → 落到第 2 篇");
    }

    [Test]
    public void Regression_C_After11_1_NextIs11_2_NormalHardNightmare()
    {
        // 普通(0) / 困难(1) / 噩梦(2) 三组同口径：都要保证「打完 11-1 后落位仍在第 2 篇，下一关 = 11-2」。
        foreach (int diff in new[] { 0, 1, 2 })
        {
            var d = new SaveData();
            int normalStages = 0, normalUnlocked = 0;
            if (diff > 0)
            {
                GrantDiffGlobalGate(d, diff);   // 该难度全局门槛凑够（困难=1 章、噩梦=3 章；按普通真通关）
                normalStages = d.clearedStages.Count;
                normalUnlocked = d.clearedChapterIds.Count;
            }

            // 第 1 篇该难度整篇通关（解锁第 2 篇）
            if (diff == 0) FillStages(d, 1, 0, ActProgressionDefs.StageCountOf(1) - 1);
            else FillStagesAtDiff(d, diff, 1, 0, ActProgressionDefs.StageCountOf(1) - 1);

            // 只打完 11-1
            if (diff == 0) FillStages(d, 2, 0, 0);
            else FillStagesAtDiff(d, diff, 2, 0, 0);

            int subs = ActProgressionDefs.SubsPerGrandOf(2);

            Assert.AreEqual(2, ChapterRouteTable.CurrentChapterToAdvanceAtDiff(diff, d),
                $"难度{diff}：回城落位 = 第 2 篇（不是第 1 篇 5-4）");
            // 只通 1 节 ≠ 通 1 座大关（旧用例这里写成 1，与实现相反，必挂）。
            Assert.AreEqual(0, d.DerivedGrandProgressFromClearedStagesAtDiff(2, diff),
                $"难度{diff}：只通 11-1，第 2 篇已通大关数必须是 0");
            Assert.IsTrue(d.HasStageClearedAtDiff(2, 0, diff), $"难度{diff}：11-1 已记账");
            Assert.IsFalse(d.HasStageClearedAtDiff(2, 1, diff),
                $"难度{diff}：11-2 未通 → 下一关开打 {ActProgressionDefs.GlobalStageLabel(2, 1)}");

            // 补齐第 1 座大关剩余小节 → 已通大关数 = 1，下一关跨到大关 2 的首节。
            if (diff == 0) FillStages(d, 2, 1, subs - 1);
            else FillStagesAtDiff(d, diff, 2, 1, subs - 1);

            Assert.AreEqual(1, d.DerivedGrandProgressFromClearedStagesAtDiff(2, diff),
                $"难度{diff}：第 2 篇第 1 座大关全清 → 已通大关数 1");
            Assert.AreEqual(2, ChapterRouteTable.CurrentChapterToAdvanceAtDiff(diff, d),
                $"难度{diff}：第 1 座大关全清后仍在本篇");
            Assert.IsFalse(d.HasStageClearedAtDiff(2, subs, diff),
                $"难度{diff}：下一关开打 {ActProgressionDefs.GlobalStageLabel(2, subs)}（第 2 座大关首节尚未通关）");

            // 难度隔离：难度局不得写普通真源，也不得串到另一档难度。
            if (diff > 0)
            {
                int other = diff == 1 ? 2 : 1;
                Assert.AreEqual(normalStages, d.clearedStages.Count, $"难度{diff}：普通 clearedStages 不新增");
                Assert.AreEqual(normalUnlocked, d.clearedChapterIds.Count, $"难度{diff}：不得新增普通解锁");
                Assert.IsFalse(d.HasStageClearedAtDiff(2, 0, other), $"难度{diff}：不得串到难度{other}");
            }
        }
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
        FillStages(one, 1, 0, ActProgressionDefs.StageCountOf(1) - 1);   // 第 1 篇**真通关**
        Assert.AreEqual(1, one.TrulyClearedChapterCount());
        Assert.IsTrue(ChapterRouteTable.CanEnterAtDiff(1, 1, one), "困难：真通关 1 章 → 第1章开");
        Assert.IsFalse(ChapterRouteTable.CanEnterAtDiff(1, 2, one), "噩梦：真通关 1 章 < 3 → 锁");
    }

    [Test]
    public void CanEnterAtDiff_ChapterByChapter()
    {
        var data = new SaveData();
        FillStages(data, 1, 0, ActProgressionDefs.StageCountOf(1) - 1);   // 真通关 1 章 → 困难全局门槛过
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

    // ---------- 2026-11-05 回归：难度隔离（普通 / 困难 / 噩梦各一组） ----------

    /// <summary>把某难度某篇的小关真源填成 [fromSub0..toSub0]（难度专属，绝不写普通 clearedStages）。</summary>
    static void FillStagesAtDiff(SaveData d, int difficulty, int chapter, int fromSub0, int toSub0)
    {
        d.clearedStageDiffKeys ??= new HashSet<string>();
        for (int s = fromSub0; s <= toSub0; s++)
            d.clearedStageDiffKeys.Add(SaveData.DiffStageKey(difficulty, chapter, s));
        d.RefreshChapterTruthAtDiff(chapter, difficulty);
    }

    /// <summary>
    /// 给该难度凑够「全局门槛」：按 2026-11-05 新口径 = 普通**真通关**篇数（<see cref="SaveData.TrulyClearedChapterCount"/>）。
    /// 用 FillStages 真实推进前 need 篇，保证 setup 状态与现实一致（不再伪造 clearedChapterIds）。
    /// 调用方在调用后自行记录普通基线，再断言「难度写入不得改变普通任何字段」。
    /// </summary>
    static void GrantDiffGlobalGate(SaveData d, int difficulty)
    {
        int need = difficulty == 1 ? GameConfig.DIFF_HARD_NEED_CLEARS : GameConfig.DIFF_NIGHTMARE_NEED_CLEARS;
        for (int ch = 1; ch <= need; ch++)
            FillStages(d, ch, 0, ActProgressionDefs.StageCountOf(ch) - 1);
    }

    [Test]
    public void Regression_A_Diff_OneGrandOnly_NextChapterStaysLocked()
    {
        foreach (int diff in new[] { 1, 2 })
        {
            var d = new SaveData();
            GrantDiffGlobalGate(d, diff);   // 该难度的全局门槛凑够（困难=1 章、噩梦=3 章）
            // setup 基线：门槛是按普通真通关凑的，难度写入不得再改动普通任何字段
            int normalStages = d.clearedStages.Count;
            int normalUnlocked = d.clearedChapterIds.Count;
            int normalTruly = d.trulyClearedChapterIds.Count;

            FillStagesAtDiff(d, diff, 1, 0, ActProgressionDefs.SubsPerGrandOf(1) - 1);   // 只通第 1 座大关

            Assert.IsFalse(d.HasTrulyClearedChapterAtDiff(1, diff), $"难度{diff}：只通 1 座大关 ≠ 整篇通关");
            Assert.IsFalse(ChapterRouteTable.CanEnterAtDiff(2, diff, d), $"难度{diff}：第 2 篇必须仍锁");
            Assert.AreEqual(1, ChapterRouteTable.CurrentChapterToAdvanceAtDiff(diff, d), $"难度{diff}：应推进仍在第 1 篇");
            Assert.AreEqual(normalStages, d.clearedStages.Count, $"难度{diff}：不得写普通 clearedStages（跨难度污染）");
            Assert.AreEqual(normalUnlocked, d.clearedChapterIds.Count, $"难度{diff}：不得新增普通解锁");
            Assert.AreEqual(normalTruly, d.trulyClearedChapterIds.Count, $"难度{diff}：不得新增普通真通关");
        }
    }

    [Test]
    public void Regression_B_Diff_FullChapter_UnlocksNext()
    {
        foreach (int diff in new[] { 1, 2 })
        {
            var d = new SaveData();
            GrantDiffGlobalGate(d, diff);
            int normalStages = d.clearedStages.Count;
            int normalUnlocked = d.clearedChapterIds.Count;
            int normalTruly = d.trulyClearedChapterIds.Count;

            FillStagesAtDiff(d, diff, 1, 0, ActProgressionDefs.StageCountOf(1) - 1);   // 整篇通关

            Assert.IsTrue(d.HasTrulyClearedChapterAtDiff(1, diff), $"难度{diff}：整篇小关全清 → 真通关");
            Assert.IsTrue(ChapterRouteTable.CanEnterAtDiff(2, diff, d), $"难度{diff}：第 2 篇解锁");
            Assert.IsFalse(ChapterRouteTable.CanEnterAtDiff(3, diff, d), $"难度{diff}：线性按序，第 3 篇仍锁");
            Assert.AreEqual(2, ChapterRouteTable.CurrentChapterToAdvanceAtDiff(diff, d), $"难度{diff}：落位到第 2 篇");
            // 难度通关不得改动普通任何字段（基线 = 门槛凑够后的普通状态）
            Assert.AreEqual(normalStages, d.clearedStages.Count, $"难度{diff}：普通 clearedStages 一行不动");
            Assert.AreEqual(normalUnlocked, d.clearedChapterIds.Count, $"难度{diff}：不得新增普通解锁");
            Assert.AreEqual(normalTruly, d.trulyClearedChapterIds.Count, $"难度{diff}：不得新增普通真通关");
        }
    }

    [Test]
    public void Regression_E_DifficultiesIsolated()
    {
        var d = new SaveData();
        for (int ch = 1; ch <= 3; ch++)   // 让困难/噩梦门槛都过（新口径 = 普通真通关 3 篇）
            FillStages(d, ch, 0, ActProgressionDefs.StageCountOf(ch) - 1);
        int normalStages = d.clearedStages.Count;
        int normalUnlocked = d.clearedChapterIds.Count;

        FillStagesAtDiff(d, 1, 1, 0, ActProgressionDefs.StageCountOf(1) - 1);   // 困难第 1 篇通

        Assert.IsTrue(d.HasTrulyClearedChapterAtDiff(1, 1), "困难真通关");
        Assert.IsFalse(d.HasTrulyClearedChapterAtDiff(1, 2), "困难通关 ≠ 噩梦通关（互不串味）");
        Assert.IsTrue(d.HasStageClearedAtDiff(1, 0, 1), "困难 1-1 已通");
        Assert.IsFalse(d.HasStageClearedAtDiff(1, 0, 2), "噩梦 1-1 仍空白");
        Assert.AreEqual(normalStages, d.clearedStages.Count, "普通真源不被难度写入污染");
        Assert.AreEqual(normalUnlocked, d.clearedChapterIds.Count, "普通解锁集合不被难度写入污染");

        // 普通通关同样不替难度解锁
        FillStages(d, 1, 0, ActProgressionDefs.StageCountOf(1) - 1);   // 普通第 1 篇真通关
        Assert.IsTrue(d.HasTrulyClearedChapter(1), "普通真通关");
        Assert.IsFalse(d.HasStageClearedAtDiff(1, 0, 2), "普通通关 ≠ 噩梦通关");
        Assert.AreEqual(1, ChapterRouteTable.CurrentChapterToAdvanceAtDiff(2, d),
            "普通进度不替噩梦落位（噩梦仍停第 1 篇）");
        Assert.AreEqual(2, ChapterRouteTable.CurrentChapterToAdvanceAtDiff(1, d),
            "困难只认困难自己的真源：第 1 篇困难已通 → 第 2 篇");
    }

    /// <summary>
    /// (e) 难度局只写难度真源 clearedStageDiffKeys —— 普通任何字段一行不动。
    /// 剧情线索 / 图鉴 / 世界地图的解锁写入在 ChapterManager.OnStageComplete 里由 `runDiff &lt;= 0` 守卫（仅普通），
    /// 本单测覆盖存档侧可断言的部分。
    /// </summary>
    [Test]
    public void Regression_E2_DiffClearWritesOnlyDiffFields()
    {
        var d = new SaveData();
        int countsBefore = d.chapterClearCounts.Count;
        int maxUnlockedBefore = d.maxUnlockedChapter;

        // 难度局三个写入点：首通发石（写难度真源）、章通关记账、整篇通关记账
        d.GrantTalentForStageFirstClear(1, 0, 1);
        d.GrantTalentForStageFirstClear(1, 1, 2);
        d.MarkChapterClearedAtDiff(1, 1);

        Assert.AreEqual(0, d.clearedStages.Count, "普通关卡真源一行不动");
        Assert.AreEqual(0, d.clearedChapterIds.Count, "不解锁普通章节");
        Assert.AreEqual(0, d.trulyClearedChapterIds.Count, "不写普通真通关集合");
        Assert.AreEqual(countsBefore, d.chapterClearCounts.Count, "不累加普通 chapterClearCounts");
        Assert.AreEqual(maxUnlockedBefore, d.maxUnlockedChapter, "不抬 maxUnlockedChapter");
        Assert.AreEqual(0, d.GrandClearedCount(1), "普通大关进度不前进");
        Assert.IsFalse(d.HasTrulyClearedChapter(1), "普通不算通关");
        Assert.IsFalse(ChapterRouteTable.CanEnter(2, d), "难度局不解锁普通下一篇");

        // 难度侧自身生效
        Assert.IsTrue(d.HasStageClearedAtDiff(1, 0, 1), "困难 1-1 已记");
        Assert.IsTrue(d.HasStageClearedAtDiff(1, 1, 2), "噩梦 1-2 已记");
        Assert.IsTrue(d.HasClearedChapterAtDiff(1, 1), "困难第 1 篇已记通关");
    }

    /// <summary>
    /// (f) 难度奖励倍率：GameConfig.GetDifficultyGoldMul = 1 / 1.8 / 3.5，通关金随难度放大。
    /// 「难度通关不弹普通首通奖励通知」依赖 BattleManager 运行时，单测无法覆盖；
    /// 由 BattleManager.Settlement.RecordStageRewardNoticeIfFirstClear 的 `if (BattleDifficulty &gt; 0) return;` 保证。
    /// </summary>
    [Test]
    public void Regression_F_DiffRewardMultiplier()
    {
        Assert.AreEqual(1f, GameConfig.GetDifficultyGoldMul(0), 0.0001f, "普通倍率");
        Assert.AreEqual(1.8f, GameConfig.GetDifficultyGoldMul(1), 0.0001f, "困难倍率");
        Assert.AreEqual(3.5f, GameConfig.GetDifficultyGoldMul(2), 0.0001f, "噩梦倍率");

        int g0 = BattleQuestConfig.GetClearGold(1, StageType.Normal, 0);
        int g1 = BattleQuestConfig.GetClearGold(1, StageType.Normal, 1);
        int g2 = BattleQuestConfig.GetClearGold(1, StageType.Normal, 2);
        Assert.Greater(g1, g0, "困难通关金高于普通");
        Assert.Greater(g2, g1, "噩梦通关金高于困难");
    }

    /// <summary>
    /// (g) 切难度来回：切到困难 → 切回普通，落位必须回到**普通真源**篇。
    /// UI 层（AdventureUI.Interaction.OnSelectDiff）切换时把 _selectedChapter 重置为
    /// CurrentChapterToAdvanceAtDiff(新难度)，这里直接锁该函数的两个分支。
    /// 构造：普通 = 第 1 篇全清 + 第 2 篇只打 11-1（真源=2）；困难 = 第 1、2 篇全清（真源=3）。
    /// </summary>
    [Test]
    public void Regression_G_SwitchDiffBackToNormal_LandsOnNormalTruth()
    {
        var d = new SaveData();
        FillStages(d, 1, 0, ActProgressionDefs.StageCountOf(1) - 1);
        FillStages(d, 2, 0, 0);

        FillStagesAtDiff(d, 1, 1, 0, ActProgressionDefs.StageCountOf(1) - 1);
        FillStagesAtDiff(d, 1, 2, 0, ActProgressionDefs.StageCountOf(2) - 1);

        Assert.AreEqual(2, ChapterRouteTable.CurrentChapterToAdvanceAtDiff(0, d), "普通真源 = 第 2 篇");
        Assert.AreEqual(3, ChapterRouteTable.CurrentChapterToAdvanceAtDiff(1, d), "困难真源 = 第 3 篇");

        // 等价于 UI：在困难下选中第 3 篇 → 切回普通 → 重置为普通真源（不是停在 3）
        Assert.AreEqual(2, ChapterRouteTable.CurrentChapterToAdvanceAtDiff(0, d),
            "切回普通落第 2 篇，不残留难度模式选中的第 3 篇");
    }

    /// <summary>
    /// (h) 难度进度落盘：难度通关 → SyncListsFromRuntime → 重启（只带序列化列表重建）→ 难度进度仍在。
    /// 评审必修项：clearedStageDiffEntries 漏镜像会导致难度进度只活在运行时 HashSet 里。
    /// </summary>
    [Test]
    public void Regression_H_DiffProgressPersistsThroughSync()
    {
        var d = new SaveData();
        d.GrantTalentForStageFirstClear(1, 0, 1);
        d.MarkChapterClearedAtDiff(1, 1);
        d.SyncListsFromRuntime();

        Assert.AreEqual(1, d.clearedStageDiffEntries.Count,
            "难度关卡真源必须镜像到 clearedStageDiffEntries（否则存档/重启后丢失）");
        Assert.IsTrue(d.clearedStageDiffEntries.Exists(e => e.id == SaveData.DiffStageKey(1, 1, 0)),
            "镜像内容 = 难度_篇_小关 键");
        Assert.AreEqual(1, d.clearedChapterDiffEntries.Count, "难度章通关记录同样要镜像");

        // 模拟重启：新对象只带序列化列表，走 SyncRuntimeFromLists 重建运行时集合
        var reborn = new SaveData();
        reborn.clearedStageDiffEntries = new List<StringIdEntry>(d.clearedStageDiffEntries);
        reborn.clearedChapterDiffEntries = new List<StringIdEntry>(d.clearedChapterDiffEntries);
        reborn.SyncRuntimeFromLists();

        Assert.IsTrue(reborn.HasStageClearedAtDiff(1, 0, 1), "重启后难度关卡进度仍在");
        Assert.IsTrue(reborn.HasClearedChapterAtDiff(1, 1), "重启后难度章通关记录仍在");
    }

    /// <summary>
    /// (i) 难度模式的可进集合必须按该难度过滤：普通只推进到第 2 篇、困难已推进到第 3 篇时，
    /// 普通集合里没有第 3 篇，冒险页难度模式必须用 AvailableChaptersAtDiff，否则会被普通集合挤回普通篇。
    /// setup 用**真实推进**（普通整篇通关第 1 篇 + 困难通第 1、2 篇），不用 GrantDiffGlobalGate，
    /// 避免伪造「普通只有第 1 篇」这种现实中不成立的状态（普通集合本来就允许 1、4 这种非连续）。
    /// </summary>
    [Test]
    public void Regression_I_AvailableChaptersAtDiff_ScopedToDiff()
    {
        var d = new SaveData();
        FillStages(d, 1, 0, ActProgressionDefs.StageCountOf(1) - 1);                    // 普通第 1 篇真通关
        FillStagesAtDiff(d, 1, 1, 0, ActProgressionDefs.StageCountOf(1) - 1);           // 困难第 1 篇真通关
        FillStagesAtDiff(d, 1, 2, 0, ActProgressionDefs.StageCountOf(2) - 1);           // 困难第 2 篇真通关

        // setup 自证：普通只解锁到第 1 篇（第 3 篇仍锁），困难已推进到第 3 篇
        Assert.AreEqual(1, d.ClearedChapterCount(), "setup：普通已解锁 1 篇");
        Assert.IsFalse(ChapterRouteTable.CanEnter(3, d), "setup：普通第 3 篇仍锁");
        Assert.IsTrue(d.HasClearedChapterAtDiff(2, 1), "setup：困难第 2 篇已通");

        CollectionAssert.AreEqual(new[] { 1, 2 }, ChapterRouteTable.AvailableChapters(d),
            "普通可选 = 1、2（不含只在困难解锁的第 3 篇）");
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, ChapterRouteTable.AvailableChaptersAtDiff(1, d),
            "困难可选 = 1、2、3（按困难自己的进度解锁）");
        CollectionAssert.AreEqual(new int[0], ChapterRouteTable.AvailableChaptersAtDiff(2, d),
            "噩梦全局门槛（3 章）未到 → 空集合");
    }

    /// <summary>
    /// 【2026-11-05 必修·回归】难度全局门槛必须按「真通关」计数，不能用「已解锁」镜像：
    /// 老档误标 3 篇已解锁但从未整篇打穿 → 噩梦不得提前开门。
    /// </summary>
    [Test]
    public void Regression_J_DiffGateUsesTrulyCleared_NotUnlocked()
    {
        var d = new SaveData();
        d.clearedChapterIds.Add(1);
        d.clearedChapterIds.Add(2);
        d.clearedChapterIds.Add(3);   // 只增不减的已解锁镜像（可能来自老档误标）

        Assert.AreEqual(3, d.ClearedChapterCount(), "setup：已解锁 3 篇");
        Assert.AreEqual(0, d.TrulyClearedChapterCount(), "setup：真通关 0 篇");
        Assert.IsFalse(ChapterRouteTable.CanEnterAtDiff(1, 2, d), "已解锁镜像不算门槛 → 噩梦必须仍锁");

        FillStages(d, 1, 0, ActProgressionDefs.StageCountOf(1) - 1);   // 真通关 1 篇
        Assert.AreEqual(1, d.TrulyClearedChapterCount());
        Assert.IsFalse(ChapterRouteTable.CanEnterAtDiff(1, 2, d), "真通关 1 篇 < 噩梦门槛 3 → 仍锁");
        Assert.IsTrue(ChapterRouteTable.CanEnterAtDiff(1, 1, d), "真通关 1 篇 ≥ 困难门槛 1 → 开");
    }

    /// <summary>
    /// 【2026-11-05 必修·回归】老档把第 1 篇误标成「已解锁」后打 11-1：
    /// 落位必须是第 2 篇，且第 1 篇**不得**因此被重新确认为真通关。
    /// </summary>
    [Test]
    public void Regression_K_OldMislabel_11_1_LandsChapter2_NotRetrulyCleared()
    {
        var d = new SaveData();
        d.clearedChapterIds.Add(1);   // 老档错误标记：「第 1 篇已解锁/已通」
        Assert.IsFalse(d.HasTrulyClearedChapter(1), "误标只是解锁镜像，不是真通关");

        FillStages(d, 2, 0, 0);       // 打完 11-1
        Assert.AreEqual(2, ChapterRouteTable.CurrentChapterToAdvance(d),
            "打完 11-1 必须落在第 2 篇（不得回跳）");

        d.MarkChapterCleared(2);      // 章节推进记账
        Assert.IsFalse(d.HasTrulyClearedChapter(1), "第 1 篇误标不得被重新标成真通关");
        Assert.IsTrue(d.IsChapterUnlocked(2), "第 2 篇已解锁");
    }

    /// <summary>
    /// 【2026-11-05 必修·回归】chapterClearCounts 残留（旧口径的通关次数）**不得**用来补解锁：
    /// 解锁真源只能是 clearedStages / clearedChapterIds 的整篇判定。
    /// </summary>
    [Test]
    public void Regression_L_ChapterClearCountsResidue_NoUnlock()
    {
        var d = new SaveData();
        d.chapterClearCounts.Add(new ChapterClearCountEntry { chapter = 1, clearCount = 5 });

        Assert.AreEqual(0, d.ClearedChapterCount(), "残留次数不得补解锁");
        Assert.IsFalse(ChapterRouteTable.CanEnter(2, d), "第 2 篇仍锁");
        Assert.AreEqual(1, ChapterRouteTable.CurrentChapterToAdvance(d), "落位仍在第 1 篇");
    }

    /// <summary>
    /// 【2026-11-05 必修·回归】难度局不写普通主线内容的唯一口径：
    /// · NormalOnlyContentAllowed(0)=true，1/2=false（剧情 / 线索 / 日志 / 普通成就全部走这一出口）；
    /// · 成就难度分档 ID 必须可区分（普通原 ID 不变，困难 #d1、噩梦 #d2）。
    /// </summary>
    [Test]
    public void Regression_M_DifficultyContent_ScopedByDifficulty()
    {
        Assert.IsTrue(BattleManager.NormalOnlyContentAllowed(0), "普通局允许写主线内容");
        Assert.IsFalse(BattleManager.NormalOnlyContentAllowed(1), "困难局不写普通主线内容（剧情/线索/日志）");
        Assert.IsFalse(BattleManager.NormalOnlyContentAllowed(2), "噩梦局不写普通主线内容（剧情/线索/日志）");

        Assert.AreEqual("clear_ch3", AchievementSystem.DiffAchievementId("clear_ch3", 0), "普通成就 ID 原样不变");
        Assert.AreEqual("clear_ch3#d1", AchievementSystem.DiffAchievementId("clear_ch3", 1), "困难独立 ID");
        Assert.AreEqual("clear_ch3#d2", AchievementSystem.DiffAchievementId("clear_ch3", 2), "噩梦独立 ID");
        Assert.AreNotEqual(AchievementSystem.DiffAchievementId("clear_ch3", 1),
            AchievementSystem.DiffAchievementId("clear_ch3", 2), "困难/噩梦互不覆盖");
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
        FillStages(data, 1, 0, ActProgressionDefs.SubsPerGrandOf(1) - 1);   // 过第 1 大关门槛（真源 clearedStages）
        data.branchRoll1 = 2;            // 派了岛 2 还没打完
        ChapterRouteTable.EnsureForkRoll(data);
        Assert.AreEqual(2, data.branchRoll1, "当前岛没打完 → 保留不重掷");
        Assert.AreEqual(0, data.branchRoll2, "也不提前派第二个");
    }
}
