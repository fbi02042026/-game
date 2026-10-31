using System;
using UnityEngine;

/// <summary>
/// 【2026-10-31 主人拍板「结算双倍可以加」】关卡结算金币翻倍广告位。
///
/// 定位：主线小关首次通关 → 回冒险页点宝箱领奖时的「获得奖励」弹窗上多一颗
/// 「看广告 · 金币×2」按钮。看完广告 = 把本关实际入账金币**再加一遍**（不是重算掉落，
/// 严格按弹窗上已经展示的那个数字发，避免两套口径）。
///
/// 规则（可调参数都在本文件顶部）：
/// · 每日 3 次（GDD 19.1 的「奖励翻倍 每日 3 次」口径）；
/// · 广告关闭 / 跳过 / 核验失败一律不发奖（只认 RewardedAdBridge 回 true）；
/// · 走 RewardedAdBridge 单一出口，placement = "settle_double"（禁在业务里散字符串）；
/// · 总开关 GameConfig.ADS_ENABLED=false 时整个按钮不出现（与线上包行为一致）。
/// </summary>
public static class SettleDoubleAd
{
    /// <summary>每日可看次数（2026-10-31 定：3 次/天）。</summary>
    public const int DailyLimit = 3;
    /// <summary>广告点位名（后台按 placement 分桶统计）。</summary>
    public const string Placement = "settle_double";

    static string TodayKey() => DateTime.Now.ToString("yyyyMMdd");

    /// <summary>今天还能看几次（0 = 用完）。</summary>
    public static int Remaining(SaveData data)
    {
        if (data == null) return 0;
        if (data.settleDoubleDayKey != TodayKey()) return DailyLimit;
        return Mathf.Max(0, DailyLimit - data.settleDoubleCount);
    }

    /// <summary>该不该出现这颗按钮（开关 + 次数 + 有金币可翻）。</summary>
    public static bool CanOffer(long gold)
        => GameConfig.ADS_ENABLED && gold > 0 && Remaining(SaveSystem.Instance?.Data) > 0;

    /// <summary>
    /// 请求看广告翻倍。<paramref name="gold"/> = 本关已入账金币（弹窗展示的那个数）。
    /// onResult(true) 只在发奖成功后回调；失败 / 次数用完 / 广告维护中都会先弹 toast 再回 false。
    /// </summary>
    public static void Request(long gold, Action<bool> onResult)
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null)
        {
            UIManager.Instance?.ShowToast("存档未就绪");
            onResult?.Invoke(false);
            return;
        }
        if (gold <= 0)
        {
            onResult?.Invoke(false);
            return;
        }
        if (Remaining(data) <= 0)
        {
            UIManager.Instance?.ShowToast($"今日结算双倍已达上限（{DailyLimit} 次）");
            onResult?.Invoke(false);
            return;
        }
        if (AdKillSwitch.AdsOff)
        {
            UIManager.Instance?.ShowToast("广告维护中，请稍后再试");
            onResult?.Invoke(false);
            return;
        }

        UIManager.Instance?.ShowToast("广告加载中…");
        RewardedAdBridge.ShowRewarded(Placement, ok =>
        {
            if (!ok)
            {
                UIManager.Instance?.ShowToast("激励未完成，金币未翻倍");
                onResult?.Invoke(false);
                return;
            }
            // 窗口期可能跨天：回调内重对当日计数，绝不超发
            if (data.settleDoubleDayKey != TodayKey())
            {
                data.settleDoubleDayKey = TodayKey();
                data.settleDoubleCount = 0;
            }
            if (data.settleDoubleCount >= DailyLimit)
            {
                onResult?.Invoke(false);
                return;
            }
            data.settleDoubleCount++;
            ResourceWallet.Add(ResourceWallet.ResourceType.Gold, gold, save: true, notify: false);
            UIManager.Instance?.ShowToast($"结算双倍：金币 +{gold}（今日 {data.settleDoubleCount}/{DailyLimit}）", true);
            Analytics.AdSlotClick("settle_double", true);
            onResult?.Invoke(true);
        });
    }
}
