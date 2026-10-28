using System;
using UnityEngine;

/// <summary>
/// 系统分享 + 每日分享奖励（2026-10-25 主人拍板：做分享、不做邀请码、不加验证、每日一次发 100 钻）。
///
/// 流程：点分享钮 → #share_click → 唤起 Android 系统分享面板（纯文本）→
/// 唤起成功记 #share_launch → 当日首次再发 100 钻（局外账，ResourceWallet.Add 唯一出口）并记 #share_reward。
///
/// 【口径】
/// 1. #share_launch = 分享面板唤起成功，**不是**「用户真的发出去了」（Android Intent 拿不到该回调，
///    主人拍板不加验证，按唤起成功即发奖）。
/// 2. 奖励日键 = SaveData.shareRewardDayKey（本地日 yyyyMMdd，跨天可再领，模式同 ResourceAdRewards.EnsureDay；
///    2026-10-28 由 UTC 改本地日，与每日登录/商店 0 点刷新同口径）。
/// 3. 非安卓真机（Editor/自测）：视为唤起成功（Debug.Log 注明测试口径），让奖励链路在 Editor 可验。
/// 4. 钻石入账失败（added<=0）只 LogError 不回滚日键 —— 防当天反复重试刷报错，次日自动恢复。
/// </summary>
public static class ShareService
{
    /// <summary>分享文案（纯文本带游戏名；主人未给落地页，不带链接）。</summary>
    public const string ShareText = "我正在玩《像素冒险：裂隙之刃》，割草爆装超解压，快来一起冒险吧！";

    /// <summary>今日分享奖励是否已领（唯一判定出口；SharePanelUI 已领态共用，同语义单一出口）。
    /// 【2026-10-28 升级兼容】旧 UTC 日键在本地 0~8 点 == 本地今天 → 视同已领，防止升级当天多领 100 钻。</summary>
    public static bool IsRewardClaimedToday(SaveData data)
        => data != null && (data.shareRewardDayKey == DateTime.Now.ToString("yyyyMMdd")
                            || ShopDefs.LegacyUtcDayKeyStillToday(data.shareRewardDayKey));

    /// <summary>分享入口唯一出口：埋点 → 唤起系统分享面板 → 当日首次发奖。sceneTag = 埋点 #scene。</summary>
    public static void TryShareAndReward(string sceneTag)
    {
        GameAnalytics.ReportShareClick("battle_report", sceneTag);

        if (!LaunchSystemShare(ShareText))
            return;   // 唤起失败已 LogError，不发奖、不记 launch

        GameAnalytics.ReportShareLaunch("battle_report");

        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null)
        {
            Debug.LogError("[ShareService] SaveData 为空，分享奖励落不了账（不发不吞）");
            return;
        }

        if (IsRewardClaimedToday(data))
        {
            UIManager.Instance?.ShowToast("今日分享奖励已领过，明天再来吧");
            return;
        }

        data.shareRewardDayKey = DateTime.Now.ToString("yyyyMMdd");
        var r = ResourceWallet.Add(ResourceWallet.ResourceType.Diamond, GameConfig.SHARE_REWARD_DIAMOND,
                                   save: true, notify: false);
        if (r.added <= 0)
        {
            Debug.LogError("[ShareService] 分享奖励入账 0（钻石账户异常？），日键已写防止当天反复重试");
            return;
        }
        UIManager.Instance?.ShowToast($"分享成功：钻石 +{r.added}");
        GameAnalytics.ReportShareReward("diamond", (int)r.added);
    }

    /// <summary>
    /// 唤起 Android 系统分享面板（ACTION_SEND + createChooser，纯文本）。
    /// 团结沿用 com.unity3d.player.UnityPlayer 取 currentActivity；真机唤不起时优先查这里（验收项）。
    /// </summary>
    static bool LaunchSystemShare(string text)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var intent = new AndroidJavaObject("android.content.Intent"))
            {
                intent.Call<AndroidJavaObject>("setAction", "android.intent.action.SEND");
                intent.Call<AndroidJavaObject>("setType", "text/plain");
                intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.TEXT", text);
                using (var intentClass = new AndroidJavaClass("android.content.Intent"))
                using (var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "分享"))
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    activity.Call<AndroidJavaObject>("startActivity", chooser);
                }
            }
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[ShareService] 唤起系统分享面板失败：{e.Message}");
            return false;
        }
#else
        Debug.Log("[ShareService] 非安卓真机（Editor 测试口径）：视为分享面板唤起成功");
        return true;
#endif
    }
}
