using System;
using UnityEngine;

/// <summary>离线金币统一公式（农场等级）。</summary>
public static class OfflineGoldCalc
{
    public static long FromSeconds(long offlineSeconds, int farmLevel)
    {
        if (offlineSeconds <= 0) return 0;
        int maxOfflineHours = Mathf.Max(GameConfig.MAX_OFFLINE_HOURS, 8 + Mathf.Max(0, farmLevel) * 2);
        double effectiveMinutes = Math.Min(offlineSeconds / 60.0, maxOfflineHours * 60.0);
        int goldPerMinute = 10 + Mathf.Max(0, farmLevel) * 10;
        return (long)(effectiveMinutes * goldPerMinute);
    }

    public static long FromDuration(TimeSpan duration, int farmLevel)
        => FromSeconds((long)duration.TotalSeconds, farmLevel);
}
