using System;
using UnityEngine;

/// <summary>
/// 体力恢复：不满时按秒回复，主界面显示回满倒计时。
/// </summary>
public static class StaminaSystem
{
    /// <summary>回复 1 点所需秒数（可调）</summary>
    public const int REGEN_SECONDS_PER_POINT = 60;
    public const int ADVENTURE_COST = 10;

    public static int Current
    {
        get
        {
            Tick();
            return SaveSystem.Instance?.Data?.stamina ?? 0;
        }
    }

    public static int Max => GameConfig.STAMINA_MAX;

    public static bool IsFull
    {
        get
        {
            Tick();
            return Current >= Max;
        }
    }

    /// <summary>回满还需秒数；已满为 0</summary>
    public static int SecondsToFull
    {
        get
        {
            Tick();
            var data = SaveSystem.Instance?.Data;
            if (data == null) return 0;
            int missing = Max - data.stamina;
            if (missing <= 0) return 0;
            long now = Now();
            long elapsed = Math.Max(0, now - data.lastStaminaUtc);
            int into = (int)(elapsed % REGEN_SECONDS_PER_POINT);
            int remainCurrent = REGEN_SECONDS_PER_POINT - into;
            if (remainCurrent >= REGEN_SECONDS_PER_POINT) remainCurrent = REGEN_SECONDS_PER_POINT;
            return (missing - 1) * REGEN_SECONDS_PER_POINT + remainCurrent;
        }
    }

    public static string FormatCountdown(int totalSeconds)
    {
        if (totalSeconds <= 0) return "";
        int h = totalSeconds / 3600;
        int m = (totalSeconds % 3600) / 60;
        int s = totalSeconds % 60;
        if (h > 0) return $"{h:D2}:{m:D2}:{s:D2}";
        return $"{m:D2}:{s:D2}";
    }

    public static void Tick(bool save = false)
    {
        var sys = SaveSystem.Instance;
        var data = sys?.Data;
        if (data == null) return;

        if (data.lastStaminaUtc <= 0)
            data.lastStaminaUtc = Now();

        if (data.stamina >= Max)
        {
            data.stamina = Max;
            data.lastStaminaUtc = Now();
            return;
        }

        long now = Now();
        long elapsed = now - data.lastStaminaUtc;
        if (elapsed < REGEN_SECONDS_PER_POINT) return;

        int gain = (int)(elapsed / REGEN_SECONDS_PER_POINT);
        if (gain <= 0) return;

        int before = data.stamina;
        data.stamina = Mathf.Min(Max, data.stamina + gain);
        data.lastStaminaUtc += (long)gain * REGEN_SECONDS_PER_POINT;
        if (data.stamina >= Max)
            data.lastStaminaUtc = now;

        if (save && data.stamina != before)
            sys.Save();
    }

    public static bool TrySpendForAdventure()
    {
        Tick(save: true);
        if (!ResourceWallet.TrySpend(ResourceWallet.ResourceType.Stamina, ADVENTURE_COST, save: true, notify: true))
            return false;
        // 开始不满回复计时
        var data = SaveSystem.Instance?.Data;
        if (data != null && data.stamina < Max && data.lastStaminaUtc <= 0)
            data.lastStaminaUtc = Now();
        SaveSystem.Instance?.Save();
        GuildHallUI.RefreshAllHudStatic();
        return true;
    }

    static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
