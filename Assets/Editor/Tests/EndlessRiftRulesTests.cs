using NUnit.Framework;

/// <summary>
/// EndlessRiftRules 纯逻辑单测（EditMode，传 SaveData 直测；SaveSystem.Instance 为空时全部 ?. 短路安全）。
/// 锚定拍板口径：
///   - 每日 2 次机会 × 15 体力（11-14 口径：进本扣 1 次、通关不扣）；本地 0 点刷新（10-28 UTC→本地）
///   - 保存点间隔 10 层、起始层 = 保存点 + 1（不重打保存层）
///   - 每周可主动重置 1 次（11-02 拍板，周一 0 点刷新），重置不动生涯最高
///   - 重打旧层不发首通：NoteBestStage/MarkCleared 双轨（本轮进度 vs 生涯最高）
/// </summary>
public class EndlessRiftRulesTests
{
    [Test]
    public void Constants_Pinned()
    {
        Assert.AreEqual(2, GameConfig.ENDLESS_DAILY_TRIES, "每日 2 次机会（11-14 口径：进本扣 1 次、打到死）");
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

    // ---------- 2026-11-15 拍板：每层固定 3 抽，不再按记录点 / 推进层数递减 ----------

    [Test]
    public void EndlessFreeDraws_ThreeEveryStage()
    {
        Assert.AreEqual(3, SlotMachineDefs.EndlessFreeDrawsForRun(0, 1), "第 1 层新开局也是 3 次");
        Assert.AreEqual(3, SlotMachineDefs.EndlessFreeDrawsForRun(0, 11), "记录点续局第一层 3 次");
        Assert.AreEqual(3, SlotMachineDefs.EndlessFreeDrawsForRun(0, 101), "深层起始层同样 3 次");
        for (int adv = 1; adv <= 30; adv++)
            Assert.AreEqual(3, SlotMachineDefs.EndlessFreeDrawsForRun(adv, 11), "本次进本后续层恒 3 抽");
    }

    [Test]
    public void EndlessChestStages_OnlyMultiplesOfFive()
    {
        for (int s = 1; s <= 40; s++)
            Assert.AreEqual(s % 5 == 0, GameConfig.IsEndlessChestStage(s), "第 " + s + " 层");
    }

    // ---------- 2026-11-15 拍板：每 20 层换章 / 词条怪比例前少后多 / 小怪属性随层数涨 ----------

    [Test]
    public void EndlessChapter_ChangesEvery20Stages()
    {
        Assert.AreEqual(1, GameConfig.EndlessChapterForStage(1), "第 1 层森林");
        Assert.AreEqual(1, GameConfig.EndlessChapterForStage(20), "第 20 层仍在森林");
        Assert.AreEqual(2, GameConfig.EndlessChapterForStage(21), "第 21 层换墓园");
        Assert.AreEqual(2, GameConfig.EndlessChapterForStage(40), "第 40 层仍在墓园");
        Assert.AreEqual(3, GameConfig.EndlessChapterForStage(41), "第 41 层换雨林");
        Assert.AreEqual(8, GameConfig.EndlessChapterForStage(160), "8 章走完最后一章");
        Assert.AreEqual(1, GameConfig.EndlessChapterForStage(161), "161 层回到第 1 章循环");
    }

    [Test]
    public void EndlessAffixRatio_And_MobStat_RiseWithStage()
    {
        Assert.AreEqual(0.05f, GameConfig.EndlessAffixRatio(1), 0.0001f, "第 1 层词条怪 5%");
        Assert.Less(GameConfig.EndlessAffixRatio(20), GameConfig.EndlessAffixRatio(40), "越往后词条怪占比越高");
        Assert.AreEqual(GameConfig.ENDLESS_AFFIX_RATIO_CAP, GameConfig.EndlessAffixRatio(60), 0.0001f, "深层封顶");
        Assert.AreEqual(1f, GameConfig.EndlessMobStatMul(1), 0.0001f, "第 1 层小怪属性不额外加");
        Assert.Less(GameConfig.EndlessMobStatMul(20), GameConfig.EndlessMobStatMul(40), "小怪属性随层数涨");
        Assert.AreEqual(GameConfig.ENDLESS_MOB_STAT_CAP, GameConfig.EndlessMobStatMul(80), 0.0001f, "50 层起封顶");
    }

    [Test]
    public void EndlessStageTypes_FixedPattern()
    {
        Assert.IsFalse(GameConfig.IsEndlessBossStage(2), "第 2 层普通");
        Assert.IsFalse(GameConfig.IsEndlessEliteStage(2), "第 2 层不再是精英（11-14 拍板）");
        Assert.IsFalse(GameConfig.IsEndlessBossStage(5), "第 5 层由小 BOSS 改判精英");
        Assert.IsTrue(GameConfig.IsEndlessEliteStage(5), "第 5 层 = 精英");
        Assert.IsTrue(GameConfig.IsEndlessBossStage(10), "第 10 层 = BOSS");
        Assert.IsFalse(GameConfig.IsEndlessEliteStage(10), "BOSS 层不当精英");
        Assert.IsTrue(GameConfig.IsEndlessEliteStage(15), "第 15 层 = 精英");
        Assert.IsTrue(GameConfig.IsEndlessBossStage(20), "第 20 层 = BOSS");
        for (int s = 1; s <= 40; s++)
        {
            if (s % 10 == 0 || s % 5 == 0) continue;
            Assert.IsFalse(GameConfig.IsEndlessEliteStage(s), $"第 {s} 层应为普通");
            Assert.IsFalse(GameConfig.IsEndlessBossStage(s), $"第 {s} 层应为普通");
        }
    }

    // ---------- 2026-11-14 拍板：奖励表驱动（endless_reward.csv）+ 徽记轮换 + BOSS 层不给金币 ----------

    [Test]
    public void EndlessRewardMul_ThreeTiers()
    {
        // 表驱动（endless_reward.csv firstGoldMul 列）：普通 ×1 / 精英 ×2 / BOSS 层 0（不给金币）
        Assert.AreEqual(1, GameConfig.EndlessRewardMul(2), "普通层首通金币 ×1");
        Assert.AreEqual(2, GameConfig.EndlessRewardMul(5), "精英层首通金币 ×2");
        Assert.AreEqual(2, GameConfig.EndlessRewardMul(15), "精英层首通金币 ×2");
        Assert.AreEqual(0, GameConfig.EndlessRewardMul(10), "BOSS 层不发金币（钻石+天赋石+抽奖卷替代）");
        Assert.AreEqual(0, GameConfig.EndlessRewardMul(20), "BOSS 层不发金币");
        Assert.AreEqual(0.3f, GameConfig.EndlessRepeatGoldMul(2), "普通层复刷 30%");
        Assert.AreEqual(0f, GameConfig.EndlessRepeatGoldMul(20), "BOSS 层复刷同样不发金币");
    }

    [Test]
    public void EndlessRewardTalentMul_OnlyBossTier()
    {
        // 2026-11-15 口径：普通层天赋石 1→0，天赋石只归 10 层 BOSS 档
        Assert.AreEqual(0, GameConfig.EndlessRewardTalentMul(2), "普通层不发天赋石（只归 10 层档）");
        Assert.AreEqual(0, GameConfig.EndlessRewardTalentMul(5), "精英层不发天赋石（归 10 层档）");
        Assert.AreEqual(0, GameConfig.EndlessRewardTalentMul(15), "精英层不发天赋石");
        Assert.AreEqual(3, GameConfig.EndlessRewardTalentMul(10), "BOSS 层天赋石 ×3");
        Assert.AreEqual(3, GameConfig.EndlessRewardTalentMul(30), "BOSS 层天赋石 ×3");
    }

    [Test]
    public void EndlessEliteBadge_RotationAndTiers()
    {
        // 表驱动（badgeJob/badgeTier 列）：每 10 层换职业按主人顺序轮换，每轮 6 个精英层升一档
        GameConfig.EndlessEliteBadge(5, out var job, out var tier);
        Assert.AreEqual("剑盾", job); Assert.AreEqual("普通", tier);
        GameConfig.EndlessEliteBadge(15, out job, out tier);
        Assert.AreEqual("游侠", job); Assert.AreEqual("普通", tier);   // 主人说的「弓手」= 系统「游侠」
        GameConfig.EndlessEliteBadge(25, out job, out tier);
        Assert.AreEqual("法师", job);
        GameConfig.EndlessEliteBadge(35, out job, out tier);
        Assert.AreEqual("牧师", job);
        GameConfig.EndlessEliteBadge(45, out job, out tier);
        Assert.AreEqual("重武", job);
        GameConfig.EndlessEliteBadge(55, out job, out tier);
        Assert.AreEqual("狂战", job); Assert.AreEqual("普通", tier);
        GameConfig.EndlessEliteBadge(65, out job, out tier);
        Assert.AreEqual("剑盾", job); Assert.AreEqual("稀有", tier);   // 轮完一遍升中级
        GameConfig.EndlessEliteBadge(125, out job, out tier);
        Assert.AreEqual("剑盾", job); Assert.AreEqual("传奇", tier);   // 再一轮升高级
        GameConfig.EndlessEliteBadge(155, out job, out tier);
        // 表尾外推：表最后一条徽记行是 145（法师/传奇），155 差 1 个 5 层档 → 轮换续推 1 位 = 牧师，档位封顶传奇
        Assert.AreEqual("牧师", job); Assert.AreEqual("传奇", tier);
        Assert.AreEqual(5, GameConfig.EndlessEliteBadgeCount(5), "打一次给 5 个");
    }

    [Test]
    public void EndlessBossDiamond_And_Scroll_FixedTwoPlusAdvanced()
    {
        Assert.AreEqual(10, GameConfig.EndlessBossDiamond(10), "10 层钻石 10");
        Assert.AreEqual(15, GameConfig.EndlessBossDiamond(20), "20 层钻石 15（每 10 层 +5）");
        Assert.AreEqual(20, GameConfig.EndlessBossDiamond(30), "30 层钻石 20");
        Assert.AreEqual(25, GameConfig.EndlessBossDiamond(40), "40 层钻石 25");
        // 【2026-11-15 主人口径「每10层掉2个」】普通卷恒 2，不再随档递增
        Assert.AreEqual(2, GameConfig.EndlessBossScrollCount(10), "10 层普通卷 2");
        Assert.AreEqual(2, GameConfig.EndlessBossScrollCount(20), "20 层普通卷 2");
        Assert.AreEqual(2, GameConfig.EndlessBossScrollCount(50), "50 层普通卷仍 2");
        Assert.AreEqual(2, GameConfig.EndlessBossScrollCount(150), "表尾 150 层普通卷仍 2");
        // 【2026-11-15 主人口径「50层以后再加1个高级的」】
        Assert.AreEqual(0, GameConfig.EndlessBossAdvancedScrollCount(10), "10 层不发高级卷");
        Assert.AreEqual(0, GameConfig.EndlessBossAdvancedScrollCount(40), "40 层不发高级卷");
        Assert.AreEqual(1, GameConfig.EndlessBossAdvancedScrollCount(50), "50 层起每 10 层加 1 个高级卷");
        Assert.AreEqual(1, GameConfig.EndlessBossAdvancedScrollCount(100), "100 层高级卷 1");
        Assert.AreEqual(1, GameConfig.EndlessBossAdvancedScrollCount(150), "表尾 150 层高级卷 1");
        Assert.AreEqual(0, GameConfig.EndlessBossDiamond(15), "精英层不发钻石");
        Assert.AreEqual(0, GameConfig.EndlessBossScrollCount(5), "精英层不发招募卷");
        Assert.AreEqual(0, GameConfig.EndlessBossAdvancedScrollCount(5), "精英层不发高级卷");
    }

    [Test]
    public void EndlessChestGold_TableDriven()
    {
        // 表驱动（chestGold 列）：-1 = 公式 6+2×层；BOSS 行配 0 = 不发
        Assert.AreEqual(8, GameConfig.EndlessChestGold(1), "普通层宝箱走公式 6+2×层");
        Assert.AreEqual(36, GameConfig.EndlessChestGold(15), "精英层宝箱走公式");
        Assert.AreEqual(0, GameConfig.EndlessChestGold(10), "BOSS 层不发宝箱金币");
        Assert.AreEqual(0, GameConfig.EndlessChestGold(50), "BOSS 层不发宝箱金币");
    }

    // ---------- 2026-11-14 拍板：10 层一节的上升锯齿 ----------

    [Test]
    public void EndlessDifficulty_TenStageRisingSawtooth()
    {
        // 节内逐层涨（第 6 关过渡位除外）
        for (int s = 1; s < 5; s++)
            Assert.Greater(GameConfig.EndlessDifficultyMul(s + 1), GameConfig.EndlessDifficultyMul(s),
                $"第 {s}→{s + 1} 层应逐层涨");
        for (int s = 6; s < 10; s++)
            Assert.Greater(GameConfig.EndlessDifficultyMul(s + 1), GameConfig.EndlessDifficultyMul(s),
                $"第 {s}→{s + 1} 层应逐层涨");

        // 第 10 层是本节高潮：精英层（第 5 层）不许盖过它
        Assert.Greater(GameConfig.EndlessDifficultyMul(10), GameConfig.EndlessDifficultyMul(5),
            "第 10 层高潮 > 第 5 层精英尖峰");
        Assert.Greater(GameConfig.EndlessDifficultyMul(20), GameConfig.EndlessDifficultyMul(15),
            "第 20 层高潮 > 第 15 层精英尖峰");

        // 换节回落一点，但整体仍是升的曲线（节首逐节抬高、峰值逐节抬高）
        Assert.Less(GameConfig.EndlessDifficultyMul(11), GameConfig.EndlessDifficultyMul(10), "第 11 层回落");
        Assert.Greater(GameConfig.EndlessDifficultyMul(11), GameConfig.EndlessDifficultyMul(1), "第 11 层仍高于第 1 层");
        Assert.Greater(GameConfig.EndlessDifficultyMul(20), GameConfig.EndlessDifficultyMul(10), "第 20 层峰值 > 第 10 层");
        Assert.Less(GameConfig.EndlessDifficultyMul(21), GameConfig.EndlessDifficultyMul(20), "第 21 层回落");
        Assert.Greater(GameConfig.EndlessDifficultyMul(30), GameConfig.EndlessDifficultyMul(20), "第 30 层峰值 > 第 20 层");

        // 每逢第 6 关降一档做过渡（第 6 / 16 层）
        Assert.Less(GameConfig.EndlessDifficultyMul(6), GameConfig.EndlessDifficultyMul(5), "第 6 关过渡降");
        Assert.Less(GameConfig.EndlessDifficultyMul(6), GameConfig.EndlessDifficultyMul(7), "第 6 关后继续爬升");
        Assert.Less(GameConfig.EndlessDifficultyMul(16), GameConfig.EndlessDifficultyMul(15), "第 16 关过渡降");
        Assert.Less(GameConfig.EndlessDifficultyMul(16), GameConfig.EndlessDifficultyMul(17), "第 16 关后继续爬升");
    }
}
