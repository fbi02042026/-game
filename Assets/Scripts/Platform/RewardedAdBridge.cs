using System;
using UnityEngine;

/// <summary>
/// 激励视频广告桥：玩法层<b>唯一出口</b>（复活/体力/金币加号都走这里，不直接碰具体 SDK）。
/// 【2026-10-25 T2】TapADN（聚合）SDK 已集成（Dirichlet.Mediation，实现见 TapAdBridge）：
/// HasRealSdk 默认 true → Android 真机走真广告；编辑器/其它平台回落本地模拟。
/// 注意 GameConfig.ADS_ENABLED=false 期间本类不会被任何业务调用（广告档折叠，线上行为不变）。
/// SpotlightBuild.ApplyRuntimeGuards 保留：聚光灯包（SPOTLIGHT_BUILD 宏）仍强制压回模拟路径。
/// </summary>
public static class RewardedAdBridge
{
    /// <summary>是否接入真实广告 SDK。【2026-10-25 主人拍板「聚光灯已结束，转商业广告版」】默认 true 走 TapAdBridge。</summary>
    public static bool HasRealSdk { get; set; } = true;

    /// <summary>
    /// 请求播放激励视频；完成后回调 success（true = 核验通过可发奖）。
    /// </summary>
    public static void ShowRewarded(string placement, Action<bool> onComplete)
    {
        if (SpotlightBuild.Enabled)
        {
            Debug.Log($"[RewardedAdBridge] Spotlight：拒绝广告 placement={placement}");
            onComplete?.Invoke(false);
            return;
        }

        if (HasRealSdk)
        {
            TapAdBridge.ShowRewarded(placement, onComplete);
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[RewardedAdBridge] 模拟激励视频成功: {placement}");
        onComplete?.Invoke(true);
#else
        Debug.Log($"[RewardedAdBridge] 模拟激励视频: {placement}");
        onComplete?.Invoke(true);
#endif
    }
}
