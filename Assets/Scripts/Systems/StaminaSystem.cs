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

    // ── 体力药水（2026-10-18 主人拍板：药水改道具入背包，使用时才恢复；超出上限先确认、多余作废）──

    /// <summary>单瓶恢复量（2026-10-18 主人拍板：30 → 20；商店 shop_stamina_30 同口径）。</summary>
    public const int POTION_RESTORE = 20;

    /// <summary>背包里的体力药水数量。</summary>
    public static int PotionCount => SaveSystem.Instance?.Data?.staminaPotions ?? 0;

    /// <summary>使用一瓶药水会浪费多少点（不真正消耗）；不超上限返回 0。给确认弹窗预检用。</summary>
    public static int GetPotionWaste()
    {
        long max = ResourceWallet.GetMax(ResourceWallet.ResourceType.Stamina);
        long cur = Current;   // 先 Tick 回复再算
        long waste = cur + POTION_RESTORE - max;
        return waste > 0 ? (int)waste : 0;
    }

    /// <summary>从背包消耗一瓶体力药水：恢复量按上限截断，超出部分作废（调用方负责先弹确认）。</summary>
    public static bool UsePotion(out string msg)
    {
        msg = null;
        var data = SaveSystem.Instance?.Data;
        if (data == null) { msg = "存档未就绪"; return false; }
        if (data.staminaPotions <= 0) { msg = "没有体力药水"; return false; }

        Tick(save: true);
        long max = ResourceWallet.GetMax(ResourceWallet.ResourceType.Stamina);
        long cur = data.stamina;
        long add = Math.Min(POTION_RESTORE, Math.Max(0L, max - cur));
        data.stamina = (int)Math.Min(int.MaxValue, cur + add);
        data.staminaPotions -= 1;
        if (data.stamina >= Max)
            data.lastStaminaUtc = Now();   // 回满即重置恢复时钟（与 Tick 同一口径）
        SaveSystem.Instance.Save();
        GuildHallUI.RefreshAllHudStatic();
        msg = add >= POTION_RESTORE ? $"体力 +{add}" : $"体力 +{add}，{POTION_RESTORE - add} 点超出上限浪费";
        return true;
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
