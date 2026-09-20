using System;
using UnityEngine;

/// <summary>离线金币统一公式（农场等级）。</summary>
/// <remarks>
/// 2026-09-20 起本功能「暂停」：进城镇自动结算/弹窗入口已停（见 TownSceneBootstrap.TryClaimTownOfflineReward），
/// 城镇主动入口与「钻石翻倍」也已移除（GuildHallUI / OfflineRewardPopup）。
/// 本公式类保留，待主人重新设计离线收益后再接回，请勿删除。
/// </remarks>
public static class OfflineGoldCalc
{
    public static long FromSeconds(long offlineSeconds, int farmLevel)
    {
        if (offlineSeconds <= 0) return 0;
        int maxOfflineHours = Mathf.Max(GameConfig.MAX_OFFLINE_HOURS, 8 + Mathf.Max(0, farmLevel) * 2);
        double effectiveMinutes = Math.Min(offlineSeconds / 60.0, maxOfflineHours * 60.0);
        int goldPerMinute = 1 + Mathf.Max(0, farmLevel) * 1;
        return (long)(effectiveMinutes * goldPerMinute);
    }

    public static long FromDuration(TimeSpan duration, int farmLevel)
        => FromSeconds((long)duration.TotalSeconds, farmLevel);
}
