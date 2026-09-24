using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 登录奖励系统（2026-09-18 第二版：改走佣兵线）。
///
/// 三条线并行：
///   · 新手 8 日：按**累计**登录自然日解锁，断签不重置（惩罚断签只会劝退休闲玩家）
///   · 每日循环：每天领 1 个，7 天一轮，无限循环
///   · 连击加成：按**连续**登录天数解锁，断签清零（唯一制造回归压力的机制，
///               但已领过的档位不回退——清零的是天数不是领奖记录）
///   · X2 双倍日：DailyLoginDefs.StarterDoubleDays（2026-09-22 参考全屏版新增，实发翻倍）
///
/// 只管发奖与状态，不管 UI。UI 调 <see cref="HasClaimable"/> 决定要不要弹。
/// </summary>
public static class DailyLoginSystem
{
    const string KEY_DAYS = "days";
    const string KEY_CYCLE = "cycle";
    const string KEY_STREAK = "streak";
    const string StarterKeyPrefix = "s";
    const string StreakKeyPrefix = "k";

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

    /// <summary>
    /// 新手 8 日的限定佣兵（塔克）是否已经拿到过。
    /// 2026-09-23：轮回后每轮的佣兵日都还是「塔克」，但只可能解锁一次——
    /// UI 用它把底部那条压暗并显示「已领」（主人要求：领完佣兵后就一直暗着）。
    /// </summary>
    public static bool IsStarterMercOwned()
    {
        var d = Data;
        var id = DailyLoginDefs.STARTER_MERC_ID;
        return d != null && !string.IsNullOrEmpty(id) && d.IsMercUnlocked(id);
    }
    public static bool CycleClaimedToday => Data != null && Data.loginCycleDay == ShopDefs.TodayKey();

    /// <summary>
    /// 连续登录天数（断签清零）。这是唯一制造"今天必须来"压力的机制——
    /// 新手 7 日与每日循环都刻意不惩罚断签，压力全压在这里。
    /// </summary>
    public static int StreakDays => Mathf.Max(0, Flag(KEY_STREAK));

    public static bool IsStreakClaimed(int days) => Flag(StreakKeyPrefix + days) > 0;

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

        // 连击：昨天登录过就 +1，否则重新计数。**只清"连续天数"，不动已领记录**。
        SetFlag(KEY_STREAK, IsYesterday(d.loginLastDay) ? StreakDays + 1 : 1);

        d.loginLastDay = today;
        SetFlag(KEY_DAYS, LoginDays + 1);
        SaveSystem.Instance.Save();
    }

    /// <summary>传入的 yyyyMMdd 是不是昨天（本地时间口径，与 ShopDefs.TodayKey 一致）。</summary>
    static bool IsYesterday(string yyyyMMdd)
    {
        if (string.IsNullOrEmpty(yyyyMMdd)) return false;
        System.DateTime prev;
        if (!System.DateTime.TryParseExact(yyyyMMdd, "yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out prev))
            return false;
        return (System.DateTime.Now.Date - prev.Date).TotalDays == 1.0;
    }

    /// <summary>还有没领的东西吗（新手或每日循环）。</summary>
    public static bool HasClaimable()
    {
        if (Data == null) return false;
        // 2026-09-23 轮回：按当前轮次里的累计天数判定（第 9 天起是 9~16，第 17 天起是 17~24…）
        int len = DailyLoginDefs.Starter.Length;
        int round = len > 0 ? (Mathf.Max(1, LoginDays) - 1) / len : 0;
        for (int i = 0; i < len; i++)
        {
            int day = round * len + i + 1;
            if (LoginDays >= day && !IsStarterClaimed(day)) return true;
        }
        for (int i = 0; i < DailyLoginDefs.Streak.Length; i++)
        {
            int need = DailyLoginDefs.Streak[i].days;
            if (StreakDays >= need && !IsStreakClaimed(need)) return true;
        }
        return !CycleClaimedToday;
    }

    /// <summary>
    /// 第 day 天（累计天数）的**实际奖励**：塔克已解锁时，佣兵日自动换成碎片 ×10。
    /// UI 显示与 TryClaimStarter 发放都走这一个入口，保证「看到的 = 拿到的」。
    /// </summary>
    public static DailyLoginDefs.Reward EffectiveStarterReward(int day)
        => DailyLoginDefs.EffectiveStarterReward(day, IsStarterMercOwned());

    public static bool TryClaimStarter(int day, out string msg)
    {
        msg = "";
        var d = Data;
        if (d == null) { msg = "存档未就绪"; return false; }
        if (day < 1) { msg = "没有这一天的奖励"; return false; }
        if (LoginDays < day) { msg = $"第 {LoginDays} 天，还没到第 {day} 天"; return false; }
        if (IsStarterClaimed(day)) { msg = "已领取"; return false; }

        // 2026-09-23 轮回：day 是**累计天数**（可以 >8），奖励按 8 天一轮回取；
        // 佣兵已解锁的佣兵日自动换碎片（EffectiveStarterReward），不会再发空气。
        var r = EffectiveStarterReward(day);
        // 2026-09-22：X2 双倍日——角标之外**实发也翻倍**（佣兵类不翻，见 Doubled）
        bool dbl = DailyLoginDefs.IsStarterDouble(day);
        if (dbl) r = DailyLoginDefs.Doubled(r);
        Grant(r);
        SetFlag(StarterKeyPrefix + day, 1);
        SaveSystem.Instance.Save();
        msg = "已领取：" + r.DisplayName.Replace("\n", "、") + (dbl ? "（X2 双倍日，数量已翻倍）" : "");
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
        msg = "已领取：" + r.DisplayName.Replace("\n", "、");
        return true;
    }

    public static bool TryClaimStreak(int days, out string msg)
    {
        msg = "";
        var d = Data;
        if (d == null) { msg = "存档未就绪"; return false; }

        var def = DailyLoginDefs.StreakFor(days);
        if (def == null) { msg = "没有这一档连击奖励"; return false; }
        if (StreakDays < days) { msg = $"连续登录 {StreakDays} 天，还没到 {days} 天"; return false; }
        if (IsStreakClaimed(days)) { msg = "已领取"; return false; }

        var r = def.Value.reward;
        Grant(r);
        SetFlag(StreakKeyPrefix + days, 1);
        SaveSystem.Instance.Save();
        msg = "已领取：" + r.DisplayName.Replace("\n", "、");
        return true;
    }

    static void Grant(DailyLoginDefs.Reward r)
    {
        GrantOne(r.grant, r.type, r.amount, r.hireId);
        if (r.HasSecond)
            GrantOne(r.grant2, r.type2, r.amount2, r.hireId2);
    }

    static void GrantOne(DailyLoginDefs.Grant grant, ResourceWallet.ResourceType type, int amount, string hireId)
    {
        switch (grant)
        {
            case DailyLoginDefs.Grant.Resource:
                ResourceWallet.Add(type, amount, save: false, notify: false);
                break;

            case DailyLoginDefs.Grant.Merc:
            {
                var d = Data;
                if (d == null || string.IsNullOrEmpty(hireId)) break;
                if (d.IsMercUnlocked(hireId)) break; // 已解锁就不重复写，避免刷日志
                d.UnlockMerc(hireId);
                Debug.Log($"[DailyLogin] 解锁佣兵 {hireId}");
                break;
            }

            case DailyLoginDefs.Grant.MercFragment:
                MercGrowInventory.Add(MercGrowInventory.FragmentId(hireId), amount);
                break;

            case DailyLoginDefs.Grant.LegendaryFragment:
            {
                string id = PickRandomLegendaryMerc();
                if (!string.IsNullOrEmpty(id))
                    MercGrowInventory.Add(MercGrowInventory.FragmentId(id), amount);
                break;
            }
        }
    }

    /// <summary>
    /// 挑一个传说佣兵发本命碎片。**优先给已解锁的**——碎片是升星用的，
    /// 给没解锁的佣兵等于存着不能用；全没解锁就随机挑一个，玩家以后解锁了就能用。
    /// </summary>
    static string PickRandomLegendaryMerc()
    {
        var data = Data;
        var all = MercRosterDefs.All;
        if (data == null || all == null) return null;

        var pool = new List<string>();
        var fallback = new List<string>();
        for (int i = 0; i < all.Count; i++)
        {
            var def = all[i];
            if (def.Rarity != MercRosterDefs.MercRarity.Legendary) continue;
            if (string.IsNullOrEmpty(def.HireId)) continue;
            fallback.Add(def.HireId);
            if (data.IsMercUnlocked(def.HireId)) pool.Add(def.HireId);
        }
        var src = pool.Count > 0 ? pool : fallback;
        if (src.Count == 0) return null;
        return src[Random.Range(0, src.Count)];
    }

    /// <summary>挑一个还没解锁的技能发碎片；全解锁了就随便挑一个（碎片仍有价值——攒着换下一轮）。
    /// ⚠ 2026-09-18：登录奖励改走佣兵线，不再发技能残卷，此方法暂无调用点。
    /// 保留备用——将来若加"技能类"登录奖励可直接复用，删了要重写一遍筛选逻辑。</summary>
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
