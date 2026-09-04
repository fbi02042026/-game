using System;
using UnityEngine;

/// <summary>
/// 顶栏金币/体力加号：激励视频接口（本版本为本地模拟发放）。
/// </summary>
public static class ResourceAdRewards
{
    public const int StaminaPerAd = 30;
    public const int StaminaAdsPerDay = 5;
    public const int GoldPerAd = 200;
    public const int GoldAdsPerDay = 10;

    static string TodayKey() => DateTime.UtcNow.ToString("yyyyMMdd");

    public static void EnsureDay(SaveData data)
    {
        if (data == null) return;
        string today = TodayKey();
        if (data.adRewardDayKey == today) return;
        data.adRewardDayKey = today;
        data.adStaminaClaimCount = 0;
        data.adGoldClaimCount = 0;
    }

    public static void TryClaimStamina()
    {
        if (SpotlightBuild.Enabled)
        {
            UIManager.Instance?.ShowToast(SpotlightBuild.AdDisabledToast);
            return;
        }
        var data = SaveSystem.Instance?.Data;
        if (data == null)
        {
            UIManager.Instance?.ShowToast("存档未就绪");
            return;
        }
        EnsureDay(data);
        if (data.adStaminaClaimCount >= StaminaAdsPerDay)
        {
            UIManager.Instance?.ShowToast($"今日体力激励已满（{StaminaAdsPerDay}次）");
            return;
        }
        if (StaminaSystem.IsFull)
        {
            UIManager.Instance?.ShowToast("体力已满");
            return;
        }

        RewardedAdBridge.ShowRewarded("stamina_plus", ok =>
        {
            if (!ok)
            {
                UIManager.Instance?.ShowToast("激励未完成");
                return;
            }
            EnsureDay(data);
            if (data.adStaminaClaimCount >= StaminaAdsPerDay) return;
            data.adStaminaClaimCount++;
            var r = ResourceWallet.Add(ResourceWallet.ResourceType.Stamina, StaminaPerAd, save: true, notify: true);
            UIManager.Instance?.ShowToast(r.added > 0
                ? $"体力 +{r.added}（激励模拟 · 今日 {data.adStaminaClaimCount}/{StaminaAdsPerDay}）"
                : "体力已满，溢出已进邮件");
        });
    }

    public static void TryClaimGold()
    {
        if (SpotlightBuild.Enabled)
        {
            UIManager.Instance?.ShowToast(SpotlightBuild.AdDisabledToast);
            return;
        }
        var data = SaveSystem.Instance?.Data;
        if (data == null)
        {
            UIManager.Instance?.ShowToast("存档未就绪");
            return;
        }
        EnsureDay(data);
        if (data.adGoldClaimCount >= GoldAdsPerDay)
        {
            UIManager.Instance?.ShowToast($"今日金币激励已满（{GoldAdsPerDay}次）");
            return;
        }

        RewardedAdBridge.ShowRewarded("gold_plus", ok =>
        {
            if (!ok)
            {
                UIManager.Instance?.ShowToast("激励未完成");
                return;
            }
            EnsureDay(data);
            if (data.adGoldClaimCount >= GoldAdsPerDay) return;
            data.adGoldClaimCount++;
            var r = ResourceWallet.Add(ResourceWallet.ResourceType.Gold, GoldPerAd, save: true, notify: true);
            UIManager.Instance?.ShowToast(r.added > 0
                ? $"金币 +{r.added}（激励模拟 · 今日 {data.adGoldClaimCount}/{GoldAdsPerDay}）"
                : "金币已达上限，溢出已进邮件");
        });
    }
}

