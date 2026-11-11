using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// ArenaRules 纯逻辑单测（EditMode，传 SaveData 直测，不依赖 SaveSystem/场景）。
/// 覆盖拍板口径：
///   - 每日 5 次免费 + 每看 1 次广告 +3、广告每天 2 次封顶（2026-10-31）
///   - 可挑战窗口 = 上方 3 名、下方 0 名（2026-10-31「能挑战的改成上面3个」）
///   - 名次按积分重排（2026-10-30 20:00）、同分玩家排在同分机器人之后（2026-10-31）
///   - 积分新口径：赢 WinBasePoints 固定值（与连胜场次无关，2026-11-11 拍板）；输 LosePointsDelta；积分永不负（2026-10-30）
///   - 机器人无 0 分、垫底保底 10 分（2026-10-31「0 分太多」）；DistVersion 当前值由专门用例断言
///   - 胜局升名 1~3 名、前 10 名 1 名（RankGainMax / TopTenRank）
///   - 阀门战力系数：新手/连输保底/低分保底 ×0.35、连赢熔断 ×1.9（2026-10-30 晚 + 10-31 重校准）
///   - 赛季奖励档与预制体面板同值（2026-10-29 深夜）
/// </summary>
public class ArenaRulesTests
{
    /// <summary>造一份已初始化（机器人+换位链）的存档。</summary>
    static SaveData NewSave()
    {
        var data = new SaveData();
        ArenaRules.EnsureDay(data);   // 首进：RegenerateBots + InitLadder + 新手必赢 3 场
        return data;
    }

    /// <summary>
    /// 登记一条预期警告：赢局分支走 ResourceWallet.Add 发金币，
    /// EditMode 里没有存档（SaveSystem.Instance == null），Add 按项目「不兜底、要让我看见」的
    /// 口径打 [ResourceWallet] SaveData 为空 后早退——这是测试环境特性，不是 bug。
    /// 登记后本用例内若出现【别的原因】产生的其他警告，Test Runner 仍会正常标黄。
    /// </summary>
    static void ExpectWalletNoSaveWarning()
    {
        LogAssert.Expect(LogType.Warning, "[ResourceWallet] SaveData 为空，无法发放 Gold");
    }

    // ---------- 次数 ----------

    [Test]
    public void TriesLeft_Fresh_IsDailyTries()
    {
        var data = NewSave();
        Assert.AreEqual(ArenaRules.DailyTries, ArenaRules.TriesLeft(data), "新一天应为 5 次免费挑战");
    }

    [Test]
    public void TriesLeft_AdBonus_AddsTwoPerView()
    {
        var data = NewSave();
        data.arenaTriesToday = ArenaRules.DailyTries;   // 免费用完
        Assert.AreEqual(0, ArenaRules.TriesLeft(data));
        data.arenaAdViewsToday = 1;                     // 看 1 次广告 +2（2026-11-11 拍板）
        Assert.AreEqual(ArenaRules.AdBonusTries, ArenaRules.TriesLeft(data));
        data.arenaAdViewsToday = ArenaRules.AdViewCapPerDay;   // 5 次 = +10（2026-11-11 拍板）
        Assert.AreEqual(ArenaRules.AdViewCapPerDay * ArenaRules.AdBonusTries, ArenaRules.TriesLeft(data));
    }

    [Test]
    public void TriesLeft_NeverNegative()
    {
        var data = NewSave();
        data.arenaTriesToday = 99;   // 异常超用
        Assert.AreEqual(0, ArenaRules.TriesLeft(data), "剩余次数应钳到 0");
    }

    // ---------- 可挑战窗口 ----------

    [Test]
    public void IsChallengeable_OnlyTopThreeWithinWindow()
    {
        var data = NewSave();
        data.arenaPlayerPos = 50;   // 0 基链位，玩家排名 51
        Assert.IsTrue(ArenaRules.IsChallengeable(data, 49), "上方第 1 名应可挑战");
        Assert.IsTrue(ArenaRules.IsChallengeable(data, 48), "上方第 2 名应可挑战");
        Assert.IsTrue(ArenaRules.IsChallengeable(data, 47), "上方第 3 名应可挑战");
        Assert.IsFalse(ArenaRules.IsChallengeable(data, 50), "自己所在行不可挑战");
        Assert.IsFalse(ArenaRules.IsChallengeable(data, 46), "超出 3 名窗口不可挑战");
        Assert.IsFalse(ArenaRules.IsChallengeable(data, 51), "下方窗口已收为 0（2026-10-31 拍板）");
    }

    // ---------- 名次与换位链 ----------

    [Test]
    public void PlayerRank_And_RankOfIndex_Consistent()
    {
        var data = NewSave();
        data.arenaPlayerPos = 50;
        Assert.AreEqual(51, ArenaRules.PlayerRank(data));
        Assert.AreEqual(50, ArenaRules.RankOfIndex(data, 49), "玩家上方相邻机器人排名 = 下标+1");
        Assert.AreEqual(52, ArenaRules.RankOfIndex(data, 50), "跨过玩家位后排名 = 下标+2");
    }

    // ---------- 积分落账 ----------

    [Test]
    public void ApplyBattleOutcome_Win_FirstBattle_Base30()
    {
        var data = NewSave();
        int rowIdx = data.arenaPlayerPos - 1;   // 上方第 1 名（合法目标）
        ExpectWalletNoSaveWarning();            // 赢局发金币，EditMode 无存档 → 预期警告 1 条
        ArenaRules.ApplyBattleOutcome(data, rowIdx, won: true, out int pts, out _);
        Assert.AreEqual(ArenaRules.WinBasePoints, pts, "首胜无连赢加成 = 基础分");
        Assert.AreEqual(ArenaRules.WinBasePoints, data.arenaPoints);
        Assert.AreEqual(1, data.arenaWinStreak);
        Assert.AreEqual(ArenaRules.NewbieWinsPerSeason - 1, data.arenaFreshWinsLeft, "新手必赢应消耗 1 场");
    }

    [Test]
    public void ApplyBattleOutcome_Win_NoStreakBonus()
    {
        // 2026-11-11 主人拍板「连胜不应该有积分加成」：本场前连赢多少场，胜利得分都等于 WinBasePoints。
        var data = NewSave();
        data.arenaBotRefreshAt = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();   // 抑制刷新，隔离胜局重排
        data.arenaWinStreak = 2;
        int rowIdx = data.arenaPlayerPos - 1;
        ExpectWalletNoSaveWarning();            // 第 1 次赢局
        ArenaRules.ApplyBattleOutcome(data, rowIdx, won: true, out int pts, out _);
        Assert.AreEqual(ArenaRules.WinBasePoints, pts, "本场前连赢 2 场也只得基础分");
        // 本场前连赢 5 场：同样只得基础分，不再有封顶加成
        data.arenaWinStreak = 5;
        ExpectWalletNoSaveWarning();            // 第 2 次赢局
        ArenaRules.ApplyBattleOutcome(data, data.arenaPlayerPos - 1, won: true, out pts, out _);
        Assert.AreEqual(ArenaRules.WinBasePoints, pts, "连胜场次高也不加分");
    }

    [Test]
    public void ApplyBattleOutcome_Lose_Minus10_FlooredAtZero()
    {
        var data = NewSave();
        data.arenaWinStreak = 3;    // 连赢链应被清零
        data.arenaPoints = 5;       // 低于 10 → 只扣到 0
        int rowIdx = data.arenaPlayerPos - 1;
        ArenaRules.ApplyBattleOutcome(data, rowIdx, won: false, out int pts, out _);
        Assert.AreEqual(ArenaRules.LosePointsDelta, pts, "败局 = -10（2026-10-30 20:00 拍板）");
        Assert.AreEqual(0, data.arenaPoints, "积分永不扣成负数");
        Assert.AreEqual(0, data.arenaWinStreak, "输一把连赢清零");
        Assert.AreEqual(1, data.arenaLossStreak);
        Assert.AreEqual(ArenaRules.LowScorePityWins, data.arenaLowPityLeft, "积分 ≤30 应武装低分保底 3 场");
    }

    [Test]
    public void ApplyBattleOutcome_WinFive_GainWithinCap_AndRankMatchesPoints()
    {
        var data = NewSave();
        data.arenaBotRefreshAt = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();   // 抑制刷新，隔离胜局重排
        for (int n = 0; n < 5; n++)
        {
            int rowIdx = data.arenaPlayerPos - 1;   // 上方第 1 名（合法目标）
            ExpectWalletNoSaveWarning();
            ArenaRules.ApplyBattleOutcome(data, rowIdx, won: true, out _, out _);
            Assert.That(ArenaRules.LastBattleAnim, Is.InRange(1, ArenaRules.RankGainMax),
                $"第 {n + 1} 场胜局升名位数应在 1..{ArenaRules.RankGainMax}");
            int atOrAbove = 0;
            foreach (var b in data.arenaBots)
                if (b != null && b.points >= data.arenaPoints) atOrAbove++;
            Assert.AreEqual(atOrAbove, data.arenaPlayerPos,
                $"第 {n + 1} 场后积分序与 arenaPlayerPos 应一致");
        }
    }

    [Test]
    public void ApplyBattleOutcome_Win_TopTenRank_GainAlwaysOne()
    {
        var data = NewSave();
        // 构造与 arenaPlayerPos 一致的积分序：前 5 个机器人高于玩家分，其余低于（避免测到一个真实存档不会出现的失序状态）
        for (int i = 0; i < data.arenaBots.Count; i++)
            data.arenaBots[i].points = i < 5 ? 1000 : 10;
        data.arenaPoints = 500;
        data.arenaPlayerPos = 5;                                   // 玩家排名 6（前 10 名内）
        data.arenaBotRefreshAt = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        int rowIdx = data.arenaPlayerPos - 1;
        ExpectWalletNoSaveWarning();
        ArenaRules.ApplyBattleOutcome(data, rowIdx, won: true, out _, out _);
        Assert.AreEqual(1, ArenaRules.LastBattleAnim, "前 10 名内胜局升名位数恒为 1");
        int atOrAbove = 0;
        foreach (var b in data.arenaBots)
            if (b != null && b.points >= data.arenaPoints) atOrAbove++;
        Assert.AreEqual(atOrAbove, data.arenaPlayerPos, "前 10 名场景积分序仍一致");
    }

    [Test]
    public void ApplyBattleOutcome_Win_BotsRiseByPts_AndSumUp()
    {
        var data = NewSave();
        data.arenaBotRefreshAt = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();   // 抑制刷新，隔离单场抬升
        long sumBefore = 0;
        foreach (var b in data.arenaBots) if (b != null) sumBefore += b.points;
        int topBefore = data.arenaBots[0].points;   // 榜首机器人不会落入胜局下调区间
        int rowIdx = data.arenaPlayerPos - 1;
        ExpectWalletNoSaveWarning();
        ArenaRules.ApplyBattleOutcome(data, rowIdx, won: true, out int pts, out _);
        long sumAfter = 0;
        foreach (var b in data.arenaBots) if (b != null) sumAfter += b.points;
        Assert.Greater(sumAfter, sumBefore, "胜局后机器人分数整体上升");
        Assert.AreEqual(pts, data.arenaBots[0].points - topBefore,
            "单个机器人单场增量应等于 pts");
    }

    [Test]
    public void RefreshBotScoresIfDue_OldTimestamp_KeepsRankConsistent_NoBounce()
    {
        var data = NewSave();
        int rowIdx = data.arenaPlayerPos - 1;
        ExpectWalletNoSaveWarning();
        ArenaRules.ApplyBattleOutcome(data, rowIdx, won: true, out _, out _);
        int posBefore = data.arenaPlayerPos;

        data.arenaBotRefreshAt = 1;   // 很旧的时间戳，强制触发刷新
        ArenaRules.RefreshBotScoresIfDue(data);

        int atOrAbove = 0;
        foreach (var b in data.arenaBots)
            if (b != null && b.points >= data.arenaPoints) atOrAbove++;
        Assert.AreEqual(atOrAbove, data.arenaPlayerPos, "刷新后积分序与 arenaPlayerPos 仍一致");
        Assert.AreEqual(posBefore, data.arenaPlayerPos, "刷新后名次不回跳");
    }

    // ---------- 阀门战力系数 ----------

    [Test]
    public void ValveMultiplier_NewbieWin_EasesDown()
    {
        var data = NewSave();
        data.arenaFreshWinsLeft = 1;
        data.arenaPityTarget = 4;
        data.arenaWinCapTarget = 5;
        Assert.AreEqual(ArenaRules.ValveEasePowerMul, ArenaRules.GetValvePowerMultiplier(data),
            "新手必赢期间敌方战力应放水 ×0.35");
    }

    [Test]
    public void ValveMultiplier_LossStreakReachesTarget_EasesDown()
    {
        var data = NewSave();
        data.arenaFreshWinsLeft = 0;
        data.arenaLossStreak = 3;
        data.arenaPityTarget = 3;   // 连输达标
        data.arenaWinCapTarget = 5;
        Assert.AreEqual(ArenaRules.ValveEasePowerMul, ArenaRules.GetValvePowerMultiplier(data),
            "连输保底应放水");
    }

    [Test]
    public void ValveMultiplier_WinStreakReachesTarget_HardensUp()
    {
        var data = NewSave();
        data.arenaFreshWinsLeft = 0;
        data.arenaLossStreak = 0;
        data.arenaPityTarget = 4;
        data.arenaWinStreak = 3;
        data.arenaWinCapTarget = 3;   // 连赢达标
        Assert.AreEqual(ArenaRules.ValveHardPowerMul, ArenaRules.GetValvePowerMultiplier(data),
            "连赢熔断应加压 ×1.9");
    }

    [Test]
    public void ValveMultiplier_NormalState_IsOne()
    {
        var data = NewSave();
        data.arenaFreshWinsLeft = 0;
        data.arenaLossStreak = 0;
        data.arenaLowPityLeft = 0;
        data.arenaPityTarget = 4;
        data.arenaWinStreak = 1;
        data.arenaWinCapTarget = 5;
        Assert.AreEqual(1f, ArenaRules.GetValvePowerMultiplier(data), 0.0001f);
    }

    // ---------- 机器人分布 ----------

    [Test]
    public void EnsureDay_Fresh_Generates100Bots_NoZeroScore_UniqueNames()
    {
        var data = NewSave();
        Assert.AreEqual(ArenaRules.BotCount, data.arenaBots.Count, "应生成 100 个机器人");
        var names = new HashSet<string>();
        foreach (var b in data.arenaBots)
        {
            Assert.IsNotNull(b);
            Assert.GreaterOrEqual(b.points, ArenaRules.BotMinScore, "机器人不应有低于保底 10 分的（2026-10-31「0 分太多」拍板）");
            Assert.IsTrue(names.Add(b.name), "机器人名字不应重复（2026-10-29 拍板）");
        }
        Assert.AreEqual(ArenaRules.NewbieWinsPerSeason, data.arenaFreshWinsLeft, "首进应送新手必赢 3 场");
    }

    [Test]
    public void PowerForRank_Endpoints_And_Monotonic()
    {
        Assert.AreEqual((int)ArenaRules.PowerTop, ArenaRules.PowerForRank(1), "榜首战力 = 天花板");
        Assert.AreEqual((int)ArenaRules.PowerBottom, ArenaRules.PowerForRank(ArenaRules.BotCount + 1), "垫底战力 = 保底值");
        for (int rank = 1; rank < ArenaRules.BotCount + 1; rank++)
            Assert.GreaterOrEqual(ArenaRules.PowerForRank(rank), ArenaRules.PowerForRank(rank + 1),
                $"战力曲线应单调不增（rank {rank}→{rank + 1} 出现逆增）");
    }

    [Test]
    public void TopTargetFor_ScalesWithBattles_NoHardCap()
    {
        // 新口径：top = StartTopPoints + 场次×TopPerBattle，随累计场次线性增长、不设硬封顶。
        var fresh = new SaveData { arenaBattlesTotal = 0, arenaPoints = 0 };
        Assert.AreEqual(ArenaRules.StartTopPoints,
            ArenaRules.TopTargetFor(fresh), "0 场 = 开局天花板");
        var veteran = new SaveData { arenaBattlesTotal = 100, arenaPoints = 0 };
        int expected = ArenaRules.StartTopPoints + 100 * ArenaRules.TopPerBattle;
        Assert.AreEqual(expected, ArenaRules.TopTargetFor(veteran), "按累计场次线性增长，不再封顶");
        Assert.Greater(expected, ArenaRules.CurveScoreReference, "足够场次后应超过 CurveScoreReference，证明无硬封顶");
    }

    // ---------- 商店扣积分 ----------

    [Test]
    public void TrySpendPoints_Guards()
    {
        var data = NewSave();
        data.arenaPoints = 100;
        data.arenaBots.Sort((a, b) => b.points.CompareTo(a.points));
        data.arenaPlayerPos = 0;
        foreach (var b in data.arenaBots)
            if (b != null && b.points >= data.arenaPoints) data.arenaPlayerPos++;
        Assert.IsFalse(ArenaRules.TrySpendPoints(data, 0), "0 扣费应拒绝");
        Assert.IsFalse(ArenaRules.TrySpendPoints(data, -5), "负数扣费应拒绝");
        Assert.IsFalse(ArenaRules.TrySpendPoints(data, 101), "余额不足应拒绝且不动账");
        Assert.AreEqual(100, data.arenaPoints, "失败的扣费不应改积分");
        Assert.IsTrue(ArenaRules.TrySpendPoints(data, 40));
        Assert.AreEqual(60, data.arenaPoints);
    }

    // ---------- 版本锚 ----------

    [Test]
    public void DistVersion_Pinned()
    {
        // 升版本 = 故意整榜重建（老档玩家按当前积分重新插位）。
        // 这里红掉说明有人动了 DistVersion——确认重建口径（玩家进度不丢）后再同步本用例。
        Assert.AreEqual(7, ArenaRules.DistVersion);
    }

    [Test]
    public void SeasonRewards_Pinned()
    {
        // 与预制体「排行奖励」面板 5 档同值（2026-10-29 深夜拍板；2026-11-11 周赛季调档：第 1 名 1000 金 + 100 钻主人定，其余按月档同比例取整）。改档位必须两边一起动。
        Assert.AreEqual(1000, ArenaRules.SeasonGoldRank1);
        Assert.AreEqual(100, ArenaRules.SeasonDiamondRank1);
        Assert.AreEqual(500, ArenaRules.SeasonGoldRank2);
        Assert.AreEqual(30, ArenaRules.SeasonDiamondRank2);
        Assert.AreEqual(400, ArenaRules.SeasonGoldRank3);
        Assert.AreEqual(20, ArenaRules.SeasonDiamondRank3);
        Assert.AreEqual(300, ArenaRules.SeasonGoldRank4_10);
        Assert.AreEqual(10, ArenaRules.SeasonDiamondRank4_10);
        Assert.AreEqual(200, ArenaRules.SeasonGoldRank11_50);
        Assert.AreEqual(0, ArenaRules.SeasonDiamondRank11_50);
        Assert.AreEqual(50, ArenaRules.SeasonRewardTopRank);
    }

    // ---------- 目标位次驱动重排：名次与积分序 ----------

    [Test]
    public void TrySpendPoints_KeepsRank_AndPointsOrder()
    {
        var data = NewSave();
        data.arenaBotRefreshAt = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();   // 抑制刷新，隔离重排
        ExpectWalletNoSaveWarning();                       // 打过 1 场（赢局发金币，EditMode 无存档 → 预期警告）
        ArenaRules.ApplyBattleOutcome(data, data.arenaPlayerPos - 1, won: true, out _, out _);
        data.arenaPoints = 100;                           // 设够余额
        // 同步积分序，避免测到真实存档不会出现的失序状态（另两处测试同口径）
        data.arenaBots.Sort((a, b) => b.points.CompareTo(a.points));
        data.arenaPlayerPos = 0;
        foreach (var b in data.arenaBots)
            if (b != null && b.points >= data.arenaPoints) data.arenaPlayerPos++;
        int posBefore = data.arenaPlayerPos;

        Assert.IsTrue(ArenaRules.TrySpendPoints(data, 50), "余额充足应扣费成功");
        Assert.AreEqual(posBefore, data.arenaPlayerPos, "商店消费当场名次不变");
        int atOrAbove = 0;
        foreach (var b in data.arenaBots)
            if (b != null && b.points >= data.arenaPoints) atOrAbove++;
        Assert.AreEqual(atOrAbove, data.arenaPlayerPos, "扣费后积分序与 arenaPlayerPos 一致");

        data.arenaBotRefreshAt = 1;                       // 强制触发刷新
        ArenaRules.RefreshBotScoresIfDue(data);
        Assert.AreEqual(posBefore, data.arenaPlayerPos, "刷新后名次仍不变");
        atOrAbove = 0;
        foreach (var b in data.arenaBots)
            if (b != null && b.points >= data.arenaPoints) atOrAbove++;
        Assert.AreEqual(atOrAbove, data.arenaPlayerPos, "刷新后积分序与 arenaPlayerPos 仍一致");
    }

    [Test]
    public void DriftBots_KeepsRank()
    {
        var data = NewSave();
        int posBefore = data.arenaPlayerPos;
        data.arenaDayKey = "1970-01-01";   // 改成昨天（旧值）以触发跨天漂移
        ArenaRules.EnsureDay(data);
        Assert.AreEqual(posBefore, data.arenaPlayerPos, "跨天漂移后玩家名次不变");
        int atOrAbove = 0;
        foreach (var b in data.arenaBots)
            if (b != null && b.points >= data.arenaPoints) atOrAbove++;
        Assert.AreEqual(atOrAbove, data.arenaPlayerPos, "漂移后积分序与 arenaPlayerPos 一致");
    }

    [Test]
    public void ApplyBattleOutcome_Lose_DropsWithinCap()
    {
        var data = NewSave();
        data.arenaBotRefreshAt = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();   // 抑制刷新，隔离重排
        for (int n = 0; n < 3; n++)                       // 先赢 3 场把名次从垫底升上来
        {
            ExpectWalletNoSaveWarning();
            ArenaRules.ApplyBattleOutcome(data, data.arenaPlayerPos - 1, won: true, out _, out _);
        }
        // 构造与 arenaPlayerPos=50 一致的积分序：前 50 个机器人高于玩家分，其余低于
        for (int i = 0; i < data.arenaBots.Count; i++)
            data.arenaBots[i].points = i < 50 ? 1000 : 10;
        data.arenaPoints = 500;
        data.arenaPlayerPos = 50;

        for (int n = 0; n < 3; n++)                       // 连输 3 场
        {
            int rowIdx = data.arenaPlayerPos - 1;         // 上方第 1 名（合法窗口内）
            ArenaRules.ApplyBattleOutcome(data, rowIdx, won: false, out _, out _);   // 输局不发金币，不登记钱包警告
            Assert.That(ArenaRules.LastBattleAnim, Is.InRange(-ArenaRules.RankGainMax, -1),
                $"第 {n + 1} 场败局掉名位数应在 -{ArenaRules.RankGainMax}..-1");
            int atOrAbove = 0;
            foreach (var b in data.arenaBots)
                if (b != null && b.points >= data.arenaPoints) atOrAbove++;
            Assert.AreEqual(atOrAbove, data.arenaPlayerPos,
                $"第 {n + 1} 场败局后积分序与 arenaPlayerPos 一致");
        }
    }
}
