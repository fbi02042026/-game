using System;
using UnityEngine;

/// <summary>
/// 顶栏金币/体力加号：每日补给位。
/// 【2026-10-25 14:47 主人拍板：商业广告版阶梯】ADS_ENABLED=true 时每日 6 档 =
/// 前 2 次免费 → 第 3、4 次看广告（RewardedAdBridge，placement gold_plus / stamina_plus）
/// → 第 5 次 50 钻、第 6 次 100 钻；之后封顶。
/// 【过渡期】GameConfig.ADS_ENABLED=false（TapADN SDK 真机验收前）广告档折叠，
/// 维持 2026-10-25 13:5x 口径 = 前 2 次免费 + 50/100 钻、每日 4 次封顶，与线上包行为一致。
/// 聚光灯（2026-09-21 广告停用铁律）已随计划结束作废（2026-10-25 主人拍板），广告路径恢复。
/// </summary>
public static class ResourceAdRewards
{
    /// <summary>体力补给一次的数量（2026-10-17 主人拍板：30→20）。</summary>
    public const int StaminaPerAd = 20;
    /// <summary>每日免费体力补给次数。</summary>
    public const int StaminaFreePerDay = 2;
    /// <summary>金币补给一次的数量（2026-10-17 主人拍板：40→60）。</summary>
    public const int GoldPerAd = 60;
    /// <summary>每日免费金币补给次数。</summary>
    public const int GoldFreePerDay = 2;

    /// <summary>广告版每日广告档次数（金币/体力同规则，2026-10-25 主人拍板）。</summary>
    public const int AdTiersPerDay = 2;
    /// <summary>广告版第 5 次补给的钻石价（原第 3 次价沿用，2026-10-25 主人拍板「一次50 一个100」）。</summary>
    public const int Tier3DiamondPrice = 50;
    /// <summary>广告版第 6 次及以后补给的钻石价（原第 4 次价沿用）。</summary>
    public const int Tier4DiamondPrice = 100;

    /// <summary>补给档位（唯一出口 ResolveClaimKind 返回）。</summary>
    public enum ClaimKind { Free, Ad, Diamond, Capped }

    /// <summary>
    /// 按当日已领次数解析下一档（唯一出口，金币/体力共用）。
    /// ADS_ENABLED=true：2 免费 → 2 广告 → 50 钻 → 100 钻 → 封顶（每日 6 次）；
    /// ADS_ENABLED=false：2 免费 → 50 钻 → 100 钻 → 封顶（每日 4 次，过渡期口径）。
    /// </summary>
    public static ClaimKind ResolveClaimKind(int claimedToday, int freePerDay, out int diamondPrice)
    {
        diamondPrice = 0;
        if (GameConfig.ADS_ENABLED)
        {
            switch (claimedToday)
            {
                case 0:
                case 1:
                    return ClaimKind.Free;
                case 2:
                case 3:
                    return ClaimKind.Ad;
                case 4:
                    diamondPrice = Tier3DiamondPrice;
                    return ClaimKind.Diamond;
                case 5:
                    diamondPrice = Tier4DiamondPrice;
                    return ClaimKind.Diamond;
                default:
                    return ClaimKind.Capped;
            }
        }
        // 过渡期（广告未接入）：现状 4 档
        if (claimedToday < freePerDay)
            return ClaimKind.Free;
        if (claimedToday == freePerDay)
        {
            diamondPrice = Tier3DiamondPrice;
            return ClaimKind.Diamond;
        }
        if (claimedToday == freePerDay + 1)
        {
            diamondPrice = Tier4DiamondPrice;
            return ClaimKind.Diamond;
        }
        return ClaimKind.Capped;
    }

    /// <summary>每日封顶次数（toast 文案用，随总开关切口径）。</summary>
    public static int DailyClaimCap(int freePerDay)
        => GameConfig.ADS_ENABLED ? freePerDay + AdTiersPerDay * 2 : freePerDay + 2;

    static string TodayKey() => DateTime.UtcNow.ToString("yyyyMMdd");

    /// <summary>
    /// 跨天清零。<c>adStaminaClaimCount</c> / <c>adGoldClaimCount</c> 统计「当日补给已领次数」
    /// （免费/广告/钻石三档共用计数，字段名沿用不改，避免旧存档失效）。
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
        if (ResourceWallet.TrySpendDiamond(price, "广告位钻石补给", save: true, notify: false))
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
        if (StaminaSystem.IsFull)
        {
            UIManager.Instance?.ShowToast("体力已满");
            return;
        }
        int cap = DailyClaimCap(StaminaFreePerDay);
        var kind = ResolveClaimKind(data.adStaminaClaimCount, StaminaFreePerDay, out int price);
        switch (kind)
        {
            case ClaimKind.Capped:
                UIManager.Instance?.ShowToast($"今日体力补给已达上限（{cap}次）");
                return;

            case ClaimKind.Ad:
                // 广告档（2026-10-25 主人拍板）：看广告领体力，走 RewardedAdBridge 单一出口；
                // 异步回调 —— 窗口期可能跨天/达上限，回调内重新 EnsureDay + 上限校验，绝不超发。
                RewardedAdBridge.ShowRewarded("stamina_plus", ok =>
                {
                    if (!ok)
                    {
                        UIManager.Instance?.ShowToast("激励未完成，体力未发放");
                        return;
                    }
                    EnsureDay(data);
                    if (data.adStaminaClaimCount >= DailyClaimCap(StaminaFreePerDay)) return;
                    data.adStaminaClaimCount++;
                    Analytics.AdSlotClick("stamina", true); // 埋点：广告档发奖成功（原先被 case 提前 return 挡住，从未上报）
                    GrantStamina(data, $"体力 +{StaminaPerAd}（激励 · 今日 {data.adStaminaClaimCount}/{cap}）");
                });
                return;

            case ClaimKind.Free:
                data.adStaminaClaimCount++;
                GrantStamina(data, $"今日免费体力补给 {data.adStaminaClaimCount}/{StaminaFreePerDay}（体力 +{{added}}）");
                break;

            case ClaimKind.Diamond:
                if (!TryPayDiamond(price, "体力补给")) return;
                data.adStaminaClaimCount++;
                GrantStamina(data, $"钻石开启：体力 +{{added}}（今日 {data.adStaminaClaimCount}/{cap}）");
                break;
        }
        Analytics.AdSlotClick("stamina", true); // 埋点：体力补给位点击（成功发奖）
    }

    /// <summary>体力入账唯一出口（added>0 弹飘字；文案里 {added} 占位替换实际数量）。</summary>
    static void GrantStamina(SaveData data, string toastTemplate)
    {
        var r = ResourceWallet.Add(ResourceWallet.ResourceType.Stamina, StaminaPerAd, save: true, notify: false);
        if (r.added > 0)
            GuildHallUI.FloatStaminaGain((int)r.added);   // 体力加号上方弹「体力+XX」飘字（2026-10-20 主人要求）
        UIManager.Instance?.ShowToast(r.added > 0
            ? toastTemplate.Replace("{added}", r.added.ToString())
            : "体力已满，溢出已进邮件");
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
        int cap = DailyClaimCap(GoldFreePerDay);
        var kind = ResolveClaimKind(data.adGoldClaimCount, GoldFreePerDay, out int price);
        switch (kind)
        {
            case ClaimKind.Capped:
                UIManager.Instance?.ShowToast($"今日金币补给已达上限（{cap}次）");
                return;

            case ClaimKind.Ad:
                RewardedAdBridge.ShowRewarded("gold_plus", ok =>
                {
                    if (!ok)
                    {
                        UIManager.Instance?.ShowToast("激励未完成，金币未发放");
                        return;
                    }
                    EnsureDay(data);
                    if (data.adGoldClaimCount >= DailyClaimCap(GoldFreePerDay)) return;
                    data.adGoldClaimCount++;
                    Analytics.AdSlotClick("gold", true); // 埋点：广告档发奖成功（原先被 case 提前 return 挡住，从未上报）
                    GrantGold(data, $"金币 +{GoldPerAd}（激励 · 今日 {data.adGoldClaimCount}/{cap}）");
                });
                return;

            case ClaimKind.Free:
                data.adGoldClaimCount++;
                GrantGold(data, $"今日免费金币补给 {data.adGoldClaimCount}/{GoldFreePerDay}（金币 +{{added}}）");
                break;

            case ClaimKind.Diamond:
                if (!TryPayDiamond(price, "金币补给")) return;
                data.adGoldClaimCount++;
                GrantGold(data, $"钻石开启：金币 +{{added}}（今日 {data.adGoldClaimCount}/{cap}）");
                break;
        }
        Analytics.AdSlotClick("gold", true); // 埋点：金币补给位点击（成功发奖）
    }

    /// <summary>金币入账唯一出口（added>0 弹 toast；文案里 {added} 占位替换实际数量）。</summary>
    static void GrantGold(SaveData data, string toastTemplate)
    {
        var r = ResourceWallet.Add(ResourceWallet.ResourceType.Gold, GoldPerAd, save: true, notify: false);
        UIManager.Instance?.ShowToast(r.added > 0
            ? toastTemplate.Replace("{added}", r.added.ToString())
            : "金币已达上限，溢出已进邮件");
    }
}
