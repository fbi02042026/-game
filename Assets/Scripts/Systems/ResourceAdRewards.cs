using System;
using UnityEngine;

/// <summary>
/// 顶栏金币/体力加号：原为激励视频广告入口，现改为钻石开启。
/// 2026-09-19 主人拍板：聚光灯上线前不接任何广告，所有「看广告」位改成钻石消耗位。
/// 总开关 <c>GameConfig.ADS_ENABLED_BEFORE_SPOTLIGHT</c>：false = 走钻石（当前）；true = 走广告。
/// 后期 SDK 就绪后把开关置 true 即可切回，UI 与发奖逻辑无需改动。
/// </summary>
public static class ResourceAdRewards
{
    public const int StaminaPerAd = 30;
    public const int StaminaAdsPerDay = 5;
    public const int GoldPerAd = 20;
    public const int GoldAdsPerDay = 10;

    /// <summary>体力补给一次的钻石定价。</summary>
    public static int StaminaDiamondPrice => GameConfig.AD_SLOT_STAMINA_DIAMOND;
    /// <summary>金币补给一次的钻石定价。</summary>
    public static int GoldDiamondPrice => GameConfig.AD_SLOT_GOLD_DIAMOND;

    static string TodayKey() => DateTime.UtcNow.ToString("yyyyMMdd");

    /// <summary>
    /// 跨天清零。<c>adStaminaClaimCount</c> / <c>adGoldClaimCount</c> 原本统计「当日看广告次数」，
    /// 在 GameConfig.ADS_ENABLED_BEFORE_SPOTLIGHT=false 期间改作「当日钻石开启次数」；
    /// 字段沿用不改名，避免旧存档失效。
    /// </summary>
    public static void EnsureDay(SaveData data)
    {
        if (data == null) return;
        string today = TodayKey();
        if (data.adRewardDayKey == today) return;
        data.adRewardDayKey = today;
        data.adStaminaClaimCount = 0;
        data.adGoldClaimCount = 0;
    }

    /// <summary>扣钻石；不足时提示「需多少 / 当前多少」并返回 false，绝不静默失败。</summary>
    static bool TryPayDiamond(int price, string slotTag)
    {
        if (ResourceWallet.TrySpend(ResourceWallet.ResourceType.Diamond, price, save: true, notify: false))
            return true;

        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        long own = ResourceWallet.Get(data, ResourceWallet.ResourceType.Diamond);
        UIManager.Instance?.ShowToast($"钻石不足：{slotTag}需 {price} 钻（当前 {own} 钻）");
        return false;
    }

    public static void TryClaimStamina()
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null)
        {
            UIManager.Instance?.ShowToast("存档未就绪");
            return;
        }
        EnsureDay(data);
        if (data.adStaminaClaimCount >= StaminaAdsPerDay)
        {
            UIManager.Instance?.ShowToast($"今日体力补给已达上限（{StaminaAdsPerDay}次）");
            return;
        }
        if (StaminaSystem.IsFull)
        {
            UIManager.Instance?.ShowToast("体力已满");
            return;
        }

        // TODO: 聚光灯上线后改为「看广告」；当前阶段（2026-09-19 主人拍板）统一走钻石
        if (GameConfig.ADS_ENABLED_BEFORE_SPOTLIGHT)
        {
            ClaimStaminaByAd(data);
            return;
        }

        if (!TryPayDiamond(StaminaDiamondPrice, "体力补给")) return;

        data.adStaminaClaimCount++;
        var r = ResourceWallet.Add(ResourceWallet.ResourceType.Stamina, StaminaPerAd, save: true, notify: false);
        UIManager.Instance?.ShowToast(r.added > 0
            ? $"钻石开启：体力 +{r.added}（今日 {data.adStaminaClaimCount}/{StaminaAdsPerDay}）"
            : "体力已满，溢出已进邮件");
    }

    /// <summary>广告路径（保留备用）：仅在 GameConfig.ADS_ENABLED_BEFORE_SPOTLIGHT=true 时调用。</summary>
    static void ClaimStaminaByAd(SaveData data)
    {
        if (SpotlightBuild.Enabled)
        {
            UIManager.Instance?.ShowToast(SpotlightBuild.AdDisabledToast);
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
            var r = ResourceWallet.Add(ResourceWallet.ResourceType.Stamina, StaminaPerAd, save: true, notify: false);
            UIManager.Instance?.ShowToast(r.added > 0
                ? $"体力 +{r.added}（激励 · 今日 {data.adStaminaClaimCount}/{StaminaAdsPerDay}）"
                : "体力已满，溢出已进邮件");
        });
    }

    public static void TryClaimGold()
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null)
        {
            UIManager.Instance?.ShowToast("存档未就绪");
            return;
        }
        EnsureDay(data);
        if (data.adGoldClaimCount >= GoldAdsPerDay)
        {
            UIManager.Instance?.ShowToast($"今日金币补给已达上限（{GoldAdsPerDay}次）");
            return;
        }

        // TODO: 聚光灯上线后改为「看广告」；当前阶段（2026-09-19 主人拍板）统一走钻石
        if (GameConfig.ADS_ENABLED_BEFORE_SPOTLIGHT)
        {
            ClaimGoldByAd(data);
            return;
        }

        if (!TryPayDiamond(GoldDiamondPrice, "金币补给")) return;

        data.adGoldClaimCount++;
        var r = ResourceWallet.Add(ResourceWallet.ResourceType.Gold, GoldPerAd, save: true, notify: false);
        UIManager.Instance?.ShowToast(r.added > 0
            ? $"钻石开启：金币 +{r.added}（今日 {data.adGoldClaimCount}/{GoldAdsPerDay}）"
            : "金币已达上限，溢出已进邮件");
    }

    /// <summary>广告路径（保留备用）：仅在 GameConfig.ADS_ENABLED_BEFORE_SPOTLIGHT=true 时调用。</summary>
    static void ClaimGoldByAd(SaveData data)
    {
        if (SpotlightBuild.Enabled)
        {
            UIManager.Instance?.ShowToast(SpotlightBuild.AdDisabledToast);
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
            var r = ResourceWallet.Add(ResourceWallet.ResourceType.Gold, GoldPerAd, save: true, notify: false);
            UIManager.Instance?.ShowToast(r.added > 0
                ? $"金币 +{r.added}（激励 · 今日 {data.adGoldClaimCount}/{GoldAdsPerDay}）"
                : "金币已达上限，溢出已进邮件");
        });
    }

}

