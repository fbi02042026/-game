using NUnit.Framework;

/// <summary>
/// ResourceAdRewards 纯逻辑单测（EditMode，ResolveClaimKind 参数注入，不碰单例）。
/// 锚定拍板口径：
///   - 过渡期 ADS_ENABLED=false：每日 4 档 = 2 免费 → 50 钻 → 100 钻 → 封顶（与线上包一致）
///   - 广告版（翻 true 后本文件会红，按拍板补 6 档：2 免费 → 2 广告 → 50 → 100）
///   - 数值：体力 +20/次、金币 +60/次（2026-10-17 拍板）
///   - 钻石加号纯广告：每日 2 次 50 → 100 递进（2026-10-29 拍板）
///   - 扣钻必须二次确认（DiamondSpendGate，2026-10-28 规则）——结算路径归人工验收
/// </summary>
public class ResourceAdRewardsTests
{
    // ---------- 补给阶梯（过渡期 4 档，freePerDay=2） ----------

    [Test]
    public void ResolveClaimKind_TransitionPeriod_Ladder()
    {
        Assert.AreEqual(ResourceAdRewards.ClaimKind.Free, ResourceAdRewards.ResolveClaimKind(0, 2, out int p0));
        Assert.AreEqual(0, p0, "免费档不出钻石价");
        Assert.AreEqual(ResourceAdRewards.ClaimKind.Free, ResourceAdRewards.ResolveClaimKind(1, 2, out _));
        Assert.AreEqual(ResourceAdRewards.ClaimKind.Diamond, ResourceAdRewards.ResolveClaimKind(2, 2, out int p2));
        Assert.AreEqual(50, p2, "第 3 次 = 50 钻（Tier3DiamondPrice）");
        Assert.AreEqual(ResourceAdRewards.ClaimKind.Diamond, ResourceAdRewards.ResolveClaimKind(3, 2, out int p3));
        Assert.AreEqual(100, p3, "第 4 次 = 100 钻（Tier4DiamondPrice）");
        Assert.AreEqual(ResourceAdRewards.ClaimKind.Capped, ResourceAdRewards.ResolveClaimKind(4, 2, out int p4));
        Assert.AreEqual(0, p4, "封顶档不出钻石价");
        Assert.AreEqual(ResourceAdRewards.ClaimKind.Capped, ResourceAdRewards.ResolveClaimKind(99, 2, out _));
    }

    [Test]
    public void DailyClaimCap_TransitionPeriod_IsFour()
    {
        Assert.AreEqual(4, ResourceAdRewards.DailyClaimCap(2), "过渡期 = 免费2 + 钻石2 = 每日 4 次");
    }

    [Test]
    public void SupplyAmounts_Pinned()
    {
        Assert.AreEqual(20, ResourceAdRewards.StaminaPerAd, "体力补给 +20（2026-10-17 拍板 30→20）");
        Assert.AreEqual(60, ResourceAdRewards.GoldPerAd, "金币补给 +60（2026-10-17 拍板 40→60）");
        Assert.AreEqual(2, ResourceAdRewards.StaminaFreePerDay);
        Assert.AreEqual(2, ResourceAdRewards.GoldFreePerDay);
    }

    // ---------- 钻石加号（纯广告 2 档） ----------

    [Test]
    public void DiamondAdRewardOf_FiftyThenHundredThenZero()
    {
        Assert.AreEqual(50, ResourceAdRewards.DiamondAdRewardOf(0), "第 1 次 +50");
        Assert.AreEqual(100, ResourceAdRewards.DiamondAdRewardOf(1), "第 2 次 +100");
        Assert.AreEqual(0, ResourceAdRewards.DiamondAdRewardOf(2), "已封顶 → 0（fail-closed 不弹）");
        Assert.AreEqual(0, ResourceAdRewards.DiamondAdRewardOf(7));
    }

    [Test]
    public void DiamondAdsLeft_ClampsAtZero()
    {
        Assert.AreEqual(2, ResourceAdRewards.DiamondAdsLeft(null), "空存档不炸，按未使用过计（?? 0）");
        Assert.AreEqual(2, ResourceAdRewards.DiamondAdsLeft(new SaveData()));
        var used = new SaveData { adDiamondClaimCount = 5 };
        Assert.AreEqual(0, ResourceAdRewards.DiamondAdsLeft(used), "异常超领也应钳到 0");
    }

    // ---------- 跨天清零 ----------

    [Test]
    public void EnsureDay_FreshSave_ResetsAllCounts()
    {
        var data = new SaveData { adRewardDayKey = "19990101", adStaminaClaimCount = 3, adGoldClaimCount = 3, adDiamondClaimCount = 3 };
        ResourceAdRewards.EnsureDay(data);
        Assert.AreEqual(System.DateTime.Now.ToString("yyyyMMdd"), data.adRewardDayKey);
        Assert.AreEqual(0, data.adStaminaClaimCount);
        Assert.AreEqual(0, data.adGoldClaimCount);
        Assert.AreEqual(0, data.adDiamondClaimCount);
    }

    [Test]
    public void EnsureDay_SameDay_KeepsCounts()
    {
        string today = System.DateTime.Now.ToString("yyyyMMdd");
        var data = new SaveData { adRewardDayKey = today, adStaminaClaimCount = 2, adGoldClaimCount = 1, adDiamondClaimCount = 1 };
        ResourceAdRewards.EnsureDay(data);
        Assert.AreEqual(2, data.adStaminaClaimCount, "同日重复 EnsureDay 不清零");
        Assert.AreEqual(1, data.adGoldClaimCount);
    }

    [Test]
    public void EnsureDay_Null_DoesNotThrow()
    {
        NUnit.Framework.Assert.DoesNotThrow(() => ResourceAdRewards.EnsureDay(null));
    }
}
