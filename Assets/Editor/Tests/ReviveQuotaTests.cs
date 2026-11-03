using NUnit.Framework;
using System;

/// <summary>
/// ReviveQuota 纯逻辑单测（EditMode）。
/// 当前工程 GameConfig.ADS_ENABLED = false（过渡期，TapADN 真机验收前），
/// 本批用例锚定过渡期口径：复活恒为钻石档（10 钻）、无每日上限。
/// 🔴 未来把 ADS_ENABLED 翻 true 时：本文件会红——按 2026-10-25 拍板口径把
///    阶梯用例补上（0/1=Ad，2=50 钻，3=100 钻，≥4=Capped），不要删测试绕过去。
/// </summary>
public class ReviveQuotaTests
{
    [Test]
    public void ResolveKind_TransitionPeriod_AlwaysDiamondAtRevivePrice()
    {
        int price;
        var kind = ReviveQuota.ResolveKind(out price);
        Assert.AreEqual(ReviveClaimKind.Diamond, kind, "ADS_ENABLED=false 过渡期应恒为钻石复活");
        Assert.AreEqual(GameConfig.AD_SLOT_REVIVE_DIAMOND, price, "过渡期钻石价应取 AD_SLOT_REVIVE_DIAMOND");
    }

    [Test]
    public void CanOfferToday_TransitionPeriod_AlwaysTrue()
    {
        Assert.IsTrue(ReviveQuota.CanOfferToday(), "ADS_ENABLED=false 期间不应拦截复活");
    }

    [Test]
    public void EnsureDay_NullSaveData_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => ReviveQuota.EnsureDay(null));
    }

    [Test]
    public void EnsureDay_FreshSaveData_SetsLocalTodayKeyAndZeroCount()
    {
        var data = new SaveData(); // 公开字段有默认值，可直接 new
        ReviveQuota.EnsureDay(data);
        Assert.AreEqual(DateTime.Now.ToString("yyyyMMdd"), data.reviveDayKey,
            "日键应为本地日 yyyyMMdd（2026-10-28 由 UTC 改本地拍板）");
        Assert.AreEqual(0, data.reviveClaimCount, "新一天计数应清零");
    }

    [Test]
    public void EnsureDay_SameKey_KeepsClaimCount()
    {
        var data = new SaveData();
        data.reviveDayKey = DateTime.Now.ToString("yyyyMMdd");
        data.reviveClaimCount = 3;
        ReviveQuota.EnsureDay(data);
        Assert.AreEqual(3, data.reviveClaimCount, "同日重复 EnsureDay 不应清零计数");
    }

    [Test]
    public void DailyReviveCap_Pinned()
    {
        Assert.AreEqual(4, ReviveQuota.DailyReviveCap, "每日复活总档位（2026-10-25 拍板：2 广告 + 50 钻 + 100 钻）");
    }

    [Test]
    public void DiamondTierPrices_Pinned()
    {
        Assert.AreEqual(50, ReviveQuota.Tier3DiamondPrice, "第 3 次复活钻石价应取 ResourceAdRewards.Tier3DiamondPrice");
        Assert.AreEqual(100, ReviveQuota.Tier4DiamondPrice, "第 4 次复活钻石价应取 ResourceAdRewards.Tier4DiamondPrice");
    }
}
