using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 登录奖励系统（2026-09-15 新增）。
///
/// 两条线并行：
///   · 新手 7 日：按**累计**登录自然日解锁，断签不重置（惩罚断签只会劝退休闲玩家）
///   · 每日循环：每天领 1 个，7 天一轮，无限循环
///
/// 只管发奖与状态，不管 UI。UI 调 <see cref="HasClaimable"/> 决定要不要弹。
/// </summary>
public static class DailyLoginSystem
{
    const string KEY_DAYS = "days";
    const string KEY_CYCLE = "cycle";
    const string StarterKeyPrefix = "s";

    static SaveData Data => SaveSystem.Instance?.Data;

    static int Flag(string key)
    {
        var d = Data;
        if (d == null || d.loginFlags == null) return 0;
        d.loginFlags.TryGetValue(key, out int v);
        return v;
    }

    static void SetFlag(string key, int v)
    {
        var d = Data;
        if (d == null) return;
        d.loginFlags ??= new Dictionary<string, int>();
        d.loginFlags[key] = v;
    }

    /// <summary>累计登录自然日数（1 起）。</summary>
    public static int LoginDays => Mathf.Max(0, Flag(KEY_DAYS));

    /// <summary>每日循环已领到第几个（0 起，0 = 下一个领第 1 个）。</summary>
    public static int CycleIndex => Mathf.Clamp(Flag(KEY_CYCLE), 0, DailyLoginDefs.CycleLength - 1);

    public static bool IsStarterClaimed(int day) => Flag(StarterKeyPrefix + day) > 0;
    public static bool CycleClaimedToday => Data != null && Data.loginCycleDay == ShopDefs.TodayKey();

    /// <summary>
    /// 每次进游戏（城镇就绪后）调用一次：累加登录天数。
    /// 同一天重复调用不会重复累加。
    /// </summary>
    public static void OnEnterGame()
    {
        var d = Data;
        if (d == null) return;

        string today = ShopDefs.TodayKey();
        if (d.loginLastDay == today) return;

        d.loginLastDay = today;
        SetFlag(KEY_DAYS, LoginDays + 1);
        SaveSystem.Instance.Save();
    }

    /// <summary>还有没领的东西吗（新手或每日循环）。</summary>
    public static bool HasClaimable()
    {
        if (Data == null) return false;
        for (int i = 0; i < DailyLoginDefs.Starter.Length; i++)
        {
            int day = i + 1;
            if (LoginDays >= day && !IsStarterClaimed(day)) return true;
        }
        return !CycleClaimedToday;
    }

    public static bool TryClaimStarter(int day, out string msg)
    {
        msg = "";
        var d = Data;
        if (d == null) { msg = "存档未就绪"; return false; }
        if (day < 1 || day > DailyLoginDefs.Starter.Length) { msg = "没有这一天的奖励"; return false; }
        if (LoginDays < day) { msg = $"第 {LoginDays} 天，还没到第 {day} 天"; return false; }
        if (IsStarterClaimed(day)) { msg = "已领取"; return false; }

        var r = DailyLoginDefs.Starter[day - 1];
        Grant(r);
        SetFlag(StarterKeyPrefix + day, 1);
        SaveSystem.Instance.Save();
        msg = "已领取：" + r.name;
        return true;
    }

    public static bool TryClaimCycle(out string msg)
    {
        msg = "";
        var d = Data;
        if (d == null) { msg = "存档未就绪"; return false; }
        if (CycleClaimedToday) { msg = "今天已领取"; return false; }

        int idx = CycleIndex;
        var r = DailyLoginDefs.Cycle[idx];
        Grant(r);

        d.loginCycleDay = ShopDefs.TodayKey();
        SetFlag(KEY_CYCLE, (idx + 1) % DailyLoginDefs.CycleLength);
        SaveSystem.Instance.Save();
        msg = "已领取：" + r.name;
        return true;
    }

    static void Grant(DailyLoginDefs.Reward r)
    {
        switch (r.grant)
        {
            case DailyLoginDefs.Grant.Resource:
                ResourceWallet.Add(r.type, r.amount, save: false, notify: false);
                break;
            case DailyLoginDefs.Grant.EpicFragment:
            {
                string id = PickRandomSkill(SkillRarity.Epic);
                if (!string.IsNullOrEmpty(id)) SkillFragmentSystem.Add(id, r.amount, save: false);
                break;
            }
            case DailyLoginDefs.Grant.RandomFragment:
            {
                string id = PickRandomSkill(null);
                if (!string.IsNullOrEmpty(id)) SkillFragmentSystem.Add(id, r.amount, save: false);
                break;
            }
        }
    }

    /// <summary>挑一个还没解锁的技能发碎片；全解锁了就随便挑一个（碎片仍有价值——攒着换下一轮）。</summary>
    static string PickRandomSkill(SkillRarity? rarity)
    {
        var data = Data;
        var all = PlayerSkillDefs.All;
        if (data == null || all == null) return null;

        bool melee = PlayerJobDefs.IsSelectedMelee();
        var pool = new List<string>();
        var fallback = new List<string>();
        for (int i = 0; i < all.Length; i++)
        {
            var def = all[i];
            if (def == null || string.IsNullOrEmpty(def.id)) continue;
            if (!SkillDraftMeta.InDraftPool(def.id)) continue;
            if (def.meleeOnly && !melee) continue;
            if (rarity.HasValue && SkillDraftMeta.Rarity(def.id) != rarity.Value) continue;
            fallback.Add(def.id);
            if (!PlayerSkillDefs.IsUnlocked(def, data)) pool.Add(def.id);
        }
        var src = pool.Count > 0 ? pool : fallback;
        if (src.Count == 0) return null;
        return src[Random.Range(0, src.Count)];
    }
}
