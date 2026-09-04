using UnityEngine;

/// <summary>
/// 聚光灯 / TapPlay 参赛包合规开关。
/// 打 Spotlight APK 时由构建脚本定义 <c>SPOTLIGHT_BUILD</c>。
/// </summary>
public static class SpotlightBuild
{
#if SPOTLIGHT_BUILD
    public const bool Enabled = true;
#else
    public const bool Enabled = false;
#endif

    public const string OfflineToast = "聚光灯版本为单机，暂不支持该功能";
    public const string AdDisabledToast = "聚光灯版本已关闭广告";

    /// <summary>Boot 时调用：强制关掉微信云 / 真广告标志。</summary>
    public static void ApplyRuntimeGuards()
    {
        if (!Enabled) return;
        CloudSaveBridge.UseWeChatCloud = false;
        RewardedAdBridge.HasRealSdk = false;
        Debug.Log("[SpotlightBuild] 聚光灯合规已生效：禁联网云存档 / 禁真广告");
    }
}
