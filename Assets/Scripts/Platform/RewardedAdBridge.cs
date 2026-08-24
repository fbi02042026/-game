using System;
using UnityEngine;

/// <summary>
/// 激励视频广告桥：正式微信 SDK 接入前用本地模拟成功。
/// </summary>
public static class RewardedAdBridge
{
    /// <summary>是否已接入真实广告 SDK（微信小游戏）。</summary>
    public static bool HasRealSdk { get; set; }

    /// <summary>
    /// 请求播放激励视频；完成后回调 success。
    /// </summary>
    public static void ShowRewarded(string placement, Action<bool> onComplete)
    {
        if (HasRealSdk)
        {
            // TODO: 微信 wx.createRewardedVideoAd(placement)
            Debug.Log($"[RewardedAdBridge] 真实 SDK 未绑定 placement={placement}，回退模拟");
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[RewardedAdBridge] 模拟激励视频成功: {placement}");
        onComplete?.Invoke(true);
#else
        // 定版前：无 SDK 时仍允许刷新，避免卡死；上架前改为 false 或接微信
        Debug.Log($"[RewardedAdBridge] 模拟激励视频: {placement}");
        onComplete?.Invoke(true);
#endif
    }
}
