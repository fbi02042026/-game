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
///   - 积分新口径：赢 30 基础 + 连赢加成 10/场封顶 30；输 -10；积分永不负（2026-10-30）
///   - 机器人无 0 分、垫底保底 10 分（2026-10-31「0 分太多」）；DistVersion=7
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
    public void TriesLeft_AdBonus_AddsThreePerView()
    {
        var data = NewSave();
        data.arenaTriesToday = ArenaRules.DailyTries;   // 免费用完
        Assert.AreEqual(0, ArenaRules.TriesLeft(data));
        data.arenaAdViewsToday = 1;                     // 看 1 次广告 +3
        Assert.AreEqual(ArenaRules.AdBonusTries, ArenaRules.TriesLeft(data));
        data.arenaAdViewsToday = ArenaRules.AdViewCapPerDay;   // 2 次 = +6
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
        Assert.AreEqual(ArenaRules.WinBasePoints, pts, "首胜无连赢加成 = 基础 30");
        Assert.AreEqual(ArenaRules.WinBasePoints, data.arenaPoints);
        Assert.AreEqual(1, data.arenaWinStreak);
        Assert.AreEqual(ArenaRules.NewbieWinsPerSeason - 1, data.arenaFreshWinsLeft, "新手必赢应消耗 1 场");
    }

    [Test]
    public void ApplyBattleOutcome_Win_StreakBonus_StepAndCap()
    {
        var data = NewSave();
        // 连赢 2 场后第 3 胜：加成 = min(30, 2×10) = 20 → 共 50
        data.arenaWinStreak = 2;
        int rowIdx = data.arenaPlayerPos - 1;
        ExpectWalletNoSaveWarning();            // 第 1 次赢局
        ArenaRules.ApplyBattleOutcome(data, rowIdx, won: true, out int pts, out _);
        Assert.AreEqual(ArenaRules.WinBasePoints + 20, pts, "本场前连赢 2 场 → +20 加成");
        // 连赢 5 场后再赢：加成封顶 30 → 共 60（本场前连赢 5 ≥ 3）
        data.arenaWinStreak = 5;
        ExpectWalletNoSaveWarning();            // 第 2 次赢局
        ArenaRules.ApplyBattleOutcome(data, data.arenaPlayerPos - 1, won: true, out pts, out _);
        Assert.AreEqual(ArenaRules.WinBasePoints + ArenaRules.StreakBonusCap, pts, "连赢加成应封顶 +30");
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
    public void TopTargetFor_ScalesWithBattles_CapsAtSeasonTop()
    {
        var fresh = new SaveData();
        Assert.AreEqual(ArenaRules.StartTopPoints, ArenaRules.TopTargetFor(fresh), "0 场时榜首天花板 = 200（榜单贴近玩家）");
        var veteran = new SaveData { arenaBattlesTotal = 100 };
        Assert.AreEqual(ArenaRules.SeasonTopScore, ArenaRules.TopTargetFor(veteran), "足够场次后封顶 1500");
    }

    // ---------- 商店扣积分 ----------

    [Test]
    public void TrySpendPoints_Guards()
    {
        var data = NewSave();
        data.arenaPoints = 100;
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
        // 与预制体「排行奖励」面板 5 档同值（2026-10-29 深夜拍板）。改档位必须两边一起动。
        Assert.AreEqual(3000, ArenaRules.SeasonGoldRank1);
        Assert.AreEqual(300, ArenaRules.SeasonDiamondRank1);
        Assert.AreEqual(1600, ArenaRules.SeasonGoldRank2);
        Assert.AreEqual(100, ArenaRules.SeasonDiamondRank2);
        Assert.AreEqual(1200, ArenaRules.SeasonGoldRank3);
        Assert.AreEqual(80, ArenaRules.SeasonDiamondRank3);
        Assert.AreEqual(1000, ArenaRules.SeasonGoldRank4_10);
        Assert.AreEqual(50, ArenaRules.SeasonDiamondRank4_10);
        Assert.AreEqual(600, ArenaRules.SeasonGoldRank11_50);
        Assert.AreEqual(0, ArenaRules.SeasonDiamondRank11_50);
        Assert.AreEqual(50, ArenaRules.SeasonRewardTopRank);
    }
}
