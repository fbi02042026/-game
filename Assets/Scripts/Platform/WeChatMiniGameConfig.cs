using UnityEngine;

/// <summary>
/// 微信小游戏定版前配置：分辨率基准、分包占位、云/广告开关。
/// </summary>
public static class WeChatMiniGameConfig
{
    public const int DesignWidth = 720;
    public const int DesignHeight = 1280;

    /// <summary>主包建议上限（MB），超出需拆分包</summary>
    public const int MainPackageSoftLimitMb = 4;

    /// <summary>
    /// 微信激励视频广告位 id（流量主开通后在微信后台「流量主 → 广告位管理」复制填入）。
    /// 空字符串 = 未配置，WeChatAd 会直接拒绝拉起并回调 false（不发奖）。
    /// 【2026-11-02 P2-B】接入微信广告前先留空，开通流量主（个人主体累计独立访客 UV≥500）后补。
    /// </summary>
    public const string RewardedAdUnitId = ""; // TODO: 流量主开通后填入真实 adUnitId

    /// <summary>是否启用云存档上行（需微信 SDK）</summary>
    public static bool EnableCloudSave
    {
        get => CloudSaveBridge.UseWeChatCloud;
        set => CloudSaveBridge.UseWeChatCloud = value;
    }

    /// <summary>是否启用真实激励视频</summary>
    public static bool EnableRewardedAd
    {
        get => RewardedAdBridge.HasRealSdk;
        set => RewardedAdBridge.HasRealSdk = value;
    }

    /// <summary>启动时把全局 Canvas 基准对齐到 720×1280（幂等）。</summary>
    public static void EnsureDesignResolution()
    {
        // GameConfig 常量已是 720×1280；此处扫描场景 CanvasScaler 纠偏手写分辨率
        var scalers = UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.CanvasScaler>(true);
        for (int i = 0; i < scalers.Length; i++)
        {
            var s = scalers[i];
            if (s == null) continue;
            s.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(DesignWidth, DesignHeight);
            s.matchWidthOrHeight = GameConfig.UI_MATCH;
        }
    }
}
