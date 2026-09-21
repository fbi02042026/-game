using System;
using UnityEngine;

/// <summary>
/// 顶栏金币/体力加号：原为激励视频广告入口，现改为**纯钻石消耗位**。
/// 2026-09-19 主人拍板：聚光灯上线前不接任何广告，所有「看广告」位改成钻石消耗位。
/// 2026-09-21 主人要求：广告相关**全部停用**（聚光灯参赛包不得出现广告），
/// 广告分支（RewardedAdBridge）与 GameConfig.ADS_ENABLED_BEFORE_SPOTLIGHT 一起注释掉。
/// 现在这里只有一条路径：每日前 N 次免费，超出扣钻石。恢复广告时把注释解开即可。
/// </summary>
public static class ResourceAdRewards
{
    public const int StaminaPerAd = 30;
    public const int StaminaAdsPerDay = 5;
    /// <summary>每日免费体力补给次数（2026-09-20 钻石经济调整：前 N 次不扣钻，日总上限 StaminaAdsPerDay 不变）。</summary>
    public const int StaminaFreePerDay = 2;
    public const int GoldPerAd = 40;
    public const int GoldAdsPerDay = 10;
    /// <summary>每日免费金币补给次数（2026-09-20 钻石经济调整：前 N 次不扣钻，日总上限 GoldAdsPerDay 不变）。</summary>
    public const int GoldFreePerDay = 2;

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

        // 2026-09-21：广告路径整体停用（聚光灯参赛包不得含广告），下面不再有「看广告」分支。
        // 需要恢复时：解注释下面 5 行 + GameConfig.ADS_ENABLED_BEFORE_SPOTLIGHT + 文件末尾的 ClaimStaminaByAd。
        // if (GameConfig.ADS_ENABLED_BEFORE_SPOTLIGHT)
        // {
        //     ClaimStaminaByAd(data);
        //     return;
        // }

        // 2026-09-20 钻石经济调整：每日前 StaminaFreePerDay 次免费，超出才扣钻（日总上限 StaminaAdsPerDay 不变）
        bool freeStamina = data.adStaminaClaimCount < StaminaFreePerDay;
        if (!freeStamina && !TryPayDiamond(StaminaDiamondPrice, "体力补给")) return;

        data.adStaminaClaimCount++;
        var r = ResourceWallet.Add(ResourceWallet.ResourceType.Stamina, StaminaPerAd, save: true, notify: false);
        UIManager.Instance?.ShowToast(r.added > 0
            ? (freeStamina
                ? $"今日免费体力补给 {data.adStaminaClaimCount}/{StaminaFreePerDay}（体力 +{r.added}）"
                : $"钻石开启：体力 +{r.added}（今日 {data.adStaminaClaimCount}/{StaminaAdsPerDay}）")
            : "体力已满，溢出已进邮件");
        Analytics.AdSlotClick("stamina", true); // 埋点：体力补给位点击（成功发奖）
    }

    /* ===== 2026-09-21 广告路径整体停用（聚光灯参赛包不得含广告）=====
       恢复步骤：解注释本段 + GameConfig.ADS_ENABLED_BEFORE_SPOTLIGHT + TryClaimStamina 里的广告分支。
       注意 RewardedAdBridge.cs 文件本身仍保留（未删），只是不再被任何业务代码调用。

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
    ===== 广告路径停用结束 ===== */

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

        // 2026-09-21：广告路径整体停用（聚光灯参赛包不得含广告），下面不再有「看广告」分支。
        // 需要恢复时：解注释下面 5 行 + GameConfig.ADS_ENABLED_BEFORE_SPOTLIGHT + 文件末尾的 ClaimGoldByAd。
        // if (GameConfig.ADS_ENABLED_BEFORE_SPOTLIGHT)
        // {
        //     ClaimGoldByAd(data);
        //     return;
        // }

        // 2026-09-20 钻石经济调整：每日前 GoldFreePerDay 次免费，超出才扣钻（日总上限 GoldAdsPerDay 不变）
        bool freeGold = data.adGoldClaimCount < GoldFreePerDay;
        if (!freeGold && !TryPayDiamond(GoldDiamondPrice, "金币补给")) return;

        data.adGoldClaimCount++;
        var r = ResourceWallet.Add(ResourceWallet.ResourceType.Gold, GoldPerAd, save: true, notify: false);
        UIManager.Instance?.ShowToast(r.added > 0
            ? (freeGold
                ? $"今日免费金币补给 {data.adGoldClaimCount}/{GoldFreePerDay}（金币 +{r.added}）"
                : $"钻石开启：金币 +{r.added}（今日 {data.adGoldClaimCount}/{GoldAdsPerDay}）")
            : "金币已达上限，溢出已进邮件");
        Analytics.AdSlotClick("gold", true); // 埋点：金币补给位点击（成功发奖）
    }

    /* ===== 2026-09-21 广告路径整体停用 =====
       恢复步骤：解注释本段 + GameConfig.ADS_ENABLED_BEFORE_SPOTLIGHT + TryClaimGold 里的广告分支。

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
    ===== 广告路径停用结束 ===== */

}

