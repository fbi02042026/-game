using System;
using UnityEngine;

/// <summary>
/// 体力恢复：不满时按秒回复，主界面显示回满倒计时。
/// </summary>
public static class StaminaSystem
{
    /// <summary>回复 1 点所需秒数（可调）。
    /// 2026-10-17 主人拍板：60 → 300（5 分钟 1 点），全局生效，主线同步变慢（主人已知情）。</summary>
    public const int REGEN_SECONDS_PER_POINT = 300;
    public const int ADVENTURE_COST = 10;
    /// <summary>无限裂隙单次进入消耗（2026-10-17 主人拍板：每天 2 次 × 20 体力）</summary>
    public const int ENDLESS_RIFT_COST = 20;

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

    /// <summary>距下一次回复 1 点的秒数；已满为 0（与 Tick 同一恢复时钟）</summary>
    public static int SecondsToNextPoint
    {
        get
        {
            Tick();
            var data = SaveSystem.Instance?.Data;
            if (data == null) return 0;
            if (data.stamina >= Max) return 0;
            long now = Now();
            long elapsed = Math.Max(0, now - data.lastStaminaUtc);
            int into = (int)(elapsed % REGEN_SECONDS_PER_POINT);
            int remain = REGEN_SECONDS_PER_POINT - into;
            if (remain >= REGEN_SECONDS_PER_POINT) remain = REGEN_SECONDS_PER_POINT;
            return remain;
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
        return TrySpend(ADVENTURE_COST);
    }

    /// <summary>无限裂隙进本扣体力（每天 2 次 × 20 体力，2026-10-17 主人拍板）</summary>
    public static bool TrySpendForEndlessRift()
    {
        return TrySpend(ENDLESS_RIFT_COST);
    }

    /// <summary>按指定数额扣体力（主线 10 / 无限裂隙 20 共用一条链路）</summary>
    static bool TrySpend(int cost)
    {
        Tick(save: true);
        if (!ResourceWallet.TrySpend(ResourceWallet.ResourceType.Stamina, cost, save: true, notify: true))
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
