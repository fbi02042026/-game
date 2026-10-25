using System;
using UnityEngine;

/// <summary>复活档位（唯一出口 ResolveKind 返回）。</summary>
public enum ReviveClaimKind
{
    /// <summary>看广告复活（ADS_ENABLED=true 且当日第 1、2 次）。</summary>
    Ad,
    /// <summary>钻石复活（diamondPrice 已带出）。</summary>
    Diamond,
    /// <summary>今日复活额度用完：不再提供复活，死亡只能返回。</summary>
    Capped,
}

/// <summary>
/// 战斗复活每日阶梯（2026-10-25 14:47 主人拍板）：
/// 每日前 2 次复活 = 看广告（placement "revive"），第 3 次 50 钻、第 4 次 100 钻，
/// 之后死亡只能【返回】（不弹复活选择）。
/// 过渡期 GameConfig.ADS_ENABLED=false（TapADN SDK 真机验收前）：维持 2026-10-24 口径 =
/// 每次复活 AD_SLOT_REVIVE_DIAMOND(10) 钻、无每日上限，与线上包行为一致。
/// 计数真源 = SaveData.reviveDayKey / reviveClaimCount（跨天清零，模式照 ResourceAdRewards.EnsureDay）。
/// </summary>
public static class ReviveQuota
{
    /// <summary>每日复活总次数（广告版 = 2 广告 + 2 钻石；2026-10-25 主人拍板）。</summary>
    public const int DailyReviveCap = 4;

    /// <summary>钻石价沿用补给位同一阶梯（单一真源，勿在此重复定数）。</summary>
    public static int Tier3DiamondPrice => ResourceAdRewards.Tier3DiamondPrice;
    public static int Tier4DiamondPrice => ResourceAdRewards.Tier4DiamondPrice;

    static string TodayKey() => DateTime.UtcNow.ToString("yyyyMMdd");

    /// <summary>跨天清零（幂等，读档后/判定前调用均可）。</summary>
    public static void EnsureDay(SaveData data)
    {
        if (data == null) return;
        string today = TodayKey();
        if (data.reviveDayKey == today) return;
        data.reviveDayKey = today;
        data.reviveClaimCount = 0;
    }

    /// <summary>今日是否还提供复活选择（ADS_ENABLED=false 恒 true = 现状；data 缺失不拦截复活）。</summary>
    public static bool CanOfferToday()
    {
        if (!GameConfig.ADS_ENABLED) return true;
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null)
        {
            Debug.LogError("[ReviveQuota] 存档未就绪，无法判定复活额度（放行，按钻石复活处理）");
            return true;
        }
        EnsureDay(data);
        return data.reviveClaimCount < DailyReviveCap;
    }

    /// <summary>
    /// 解析下一次复活档位（唯一出口）。
    /// ADS_ENABLED=false → 恒 Diamond(AD_SLOT_REVIVE_DIAMOND)，无每日上限（现状）；
    /// ADS_ENABLED=true  → 第 1、2 次 Ad，第 3 次 50 钻、第 4 次 100 钻，之后 Capped。
    /// </summary>
    public static ReviveClaimKind ResolveKind(out int diamondPrice)
    {
        if (!GameConfig.ADS_ENABLED)
        {
            diamondPrice = GameConfig.AD_SLOT_REVIVE_DIAMOND;
            return ReviveClaimKind.Diamond;
        }
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null)
        {
            Debug.LogError("[ReviveQuota] 存档未就绪，复活档位按钻石(10)兜不住 —— 按现状钻石复活处理");
            diamondPrice = GameConfig.AD_SLOT_REVIVE_DIAMOND;
            return ReviveClaimKind.Diamond;
        }
        EnsureDay(data);
        switch (data.reviveClaimCount)
        {
            case 0:
            case 1:
                diamondPrice = 0;
                return ReviveClaimKind.Ad;
            case 2:
                diamondPrice = Tier3DiamondPrice;
                return ReviveClaimKind.Diamond;
            case 3:
                diamondPrice = Tier4DiamondPrice;
                return ReviveClaimKind.Diamond;
            default:
                diamondPrice = 0;
                return ReviveClaimKind.Capped;
        }
    }

    /// <summary>复活成功后计数 +1（唯一记账出口；ADS_ENABLED=false 期间也照记，跨天自动清零）。</summary>
    public static void RegisterClaimed()
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null) return;
        EnsureDay(data);
        data.reviveClaimCount++;
        SaveSystem.Instance?.Save();
    }
}
