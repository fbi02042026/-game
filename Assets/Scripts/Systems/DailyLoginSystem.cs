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
    const string KEY_ACCUM = "acc";

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

    /// <summary>累计登录限定佣兵是否已领取（flag acc=1）。</summary>
    public static bool IsAccumClaimed() => Flag(KEY_ACCUM) > 0;

    /// <summary>是否已累计登录达到领取限定佣兵的天数（默认 8 天，由 DailyLoginDefs.AccumTargetDays 决定）。</summary>
    public static bool IsAccumReached() => LoginDays >= DailyLoginDefs.AccumTargetDays;

    /// <summary>
    /// 从界面底部信息条领取“累计登录限定佣兵”。
    /// 佣兵类在 GrantOne 内部已处理“已解锁就不重复写”，重复点安全；达到天数但没点之前 HasClaimable 会亮起。
    /// </summary>
    public static bool TryClaimAccum(out string msg)
    {
        msg = "";
        var d = Data;
        if (d == null) { msg = "存档未就绪"; return false; }
        if (IsAccumClaimed()) { msg = "已领取"; return false; }
        if (LoginDays < DailyLoginDefs.AccumTargetDays)
        {
            int remain = DailyLoginDefs.AccumTargetDays - LoginDays;
            msg = $"累计登录 {LoginDays} 天，还需 {remain} 天";
            return false;
        }
        var r = DailyLoginDefs.AccumReward;
        Grant(r);
        SetFlag(KEY_ACCUM, 1);
        SaveSystem.Instance.Save();
        msg = GrantedMercHireId != null ? CongratsMsg(GrantedMercHireId)
            : "已领取：" + r.DisplayName.Replace("\n", "、");
        return true;
    }

    /// <summary>
    /// 每次进游戏（城镇就绪后）调用一次：累加登录天数。
    /// 同一天重复调用不会重复累加。
    /// </summary>
    public static void OnEnterGame()
    {
        var d = Data;
        if (d == null) return;

        string today = ShopDefs.TodayKey();
        if (d.loginLastDay == today)
        {
            // 【2026-10-27 修·主人报「每日登录不能领取」】自愈死锁：
            // 实测存档出现「loginLastDay=今天 但 loginFlagEntries 全空」的脏状态
            //（清档+引导重测后出现，成因未定案）——OnEnterGame 在这里永远早退，
            // LoginDays 恒 0 → 8 日格连第 1 天都不可领，玩家永远领不了。
            // 天数标记比 loginLastDay 更真：标记空了就补回当天基数并留痕，绝不吞已有累计。
            if (LoginDays < 1)
            {
                SetFlag(KEY_DAYS, 1);
                if (StreakDays < 1) SetFlag(KEY_STREAK, 1);
                SaveSystem.Instance.Save();
                Debug.LogWarning("[DailyLogin] 检测到登录天数标记丢失（loginLastDay=今天但 loginFlags 为空），已自愈为第 1 天");
            }
            return;
        }

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

    /// <summary>还有没领的东西吗（只算界面实际能领的：新手 8 日格 + 累计大奖）。</summary>
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
        // 【2026-10-20 修·主人报「领完红点不灭」】
        // 2026-09-23 主人拍板：每日循环条 / 连击加成条已从弹窗 UI 移除（连击改双倍日），
        // 完整预制体 DailyLoginFull 没有 CycleBar/StreakBar 节点，循环奖与连击档实际无从领取；
        // 这两项不能再计入「有可领」，否则主界面每日登录红点永远不灭（HasClaimable 恒真）。
        // 累计大奖走底部 InfoBar 点击领取，入口还在，保留判定。
        if (IsAccumReached() && !IsAccumClaimed()) return true;
        return false;
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
        // 【2026-10-20】记下基础奖励（Doubled 前）：UI「获得奖励」弹窗要用它——
        // 若等 Grant 解锁塔克后再取 EffectiveStarterReward，会二次折算成碎片，与实发本体不符。
        LastStarterBaseReward = r;
        // 2026-09-22：X2 双倍日——角标之外**实发也翻倍**（佣兵类不翻，见 Doubled）
        bool dbl = DailyLoginDefs.IsStarterDouble(day);
        if (dbl) r = DailyLoginDefs.Doubled(r);
        Grant(r);
        SetFlag(StarterKeyPrefix + day, 1);
        SaveSystem.Instance.Save();
        // 2026-10-20 主人拍板：实发佣兵本体时统一报喜（与三选一抽中报喜同款）；佣兵类不参与 X2 翻倍
        msg = GrantedMercHireId != null
            ? CongratsMsg(GrantedMercHireId)
            : "已领取：" + r.DisplayName.Replace("\n", "、") + (dbl ? "（X2 双倍日，数量已翻倍）" : "");
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
        msg = GrantedMercHireId != null ? CongratsMsg(GrantedMercHireId)
            : "已领取：" + r.DisplayName.Replace("\n", "、");
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
        msg = GrantedMercHireId != null ? CongratsMsg(GrantedMercHireId)
            : "已领取：" + r.DisplayName.Replace("\n", "、");
        return true;
    }

    static void Grant(DailyLoginDefs.Reward r)
    {
        GrantedMercHireId = GrantOne(r.grant, r.type, r.amount, r.hireId);
        if (r.HasSecond)
        {
            string second = GrantOne(r.grant2, r.type2, r.amount2, r.hireId2);
            if (string.IsNullOrEmpty(GrantedMercHireId)) GrantedMercHireId = second;
        }
    }

    /// <summary>Grant() 每次实发佣兵 hireId 的暂存（非佣兵奖 = null），供 LastGrantedMercHireId 对外读。</summary>
    static string GrantedMercHireId;

    /// <summary>本次 Grant 实发**佣兵本体**（真解锁）时的 hireId；碎片 / 已拥有空发 / 非佣兵奖 = null。
    /// UI 读取它决定要不要播「获得佣兵」演出（MercGainCinematicUI）。</summary>
    public static string LastGrantedMercHireId => GrantedMercHireId;

    /// <summary>
    /// 最近一次 TryClaimStarter 的**基础奖励**（Doubled 前）。UI 弹「获得奖励」弹窗用这个，
    /// 不许再调 EffectiveStarterReward——领取动作本身可能解锁佣兵（D1 塔克），重取会二次折算失真。
    /// grant == None 表示本次进程还没领取过。
    /// </summary>
    public static DailyLoginDefs.Reward LastStarterBaseReward;

    /// <summary>
    /// 佣兵本体报喜文案（2026-10-20 主人拍板：登录领取佣兵要报喜，与三选一抽中报喜统一）。
    /// 结构与 RunDraftDirector 同款：稀有度名 + 真名（只写真名不带花名）+ 名字按稀有度上色（RarityPalette 真源）。
    /// </summary>
    static string CongratsMsg(string hireId)
    {
        bool hasRoster = MercRosterDefs.TryGetByHireId(hireId, out var rd);
        string fullName = hasRoster && !string.IsNullOrEmpty(rd.Name) ? rd.Name : hireId;
        string coloredName = "<color=#" + ColorUtility.ToHtmlStringRGB(
            RarityPalette.Get(hasRoster ? rd.Rarity : MercRosterDefs.MercRarity.Common))
            + ">" + fullName + "</color>";
        string rarityName = hasRoster
            ? SkillRarityUtil.DisplayName(SkillRarityUtil.FromMerc(rd.Rarity))
            : "";
        return string.IsNullOrEmpty(rarityName)
            ? $"恭喜获得佣兵·{coloredName}，实力大增！"
            : $"恭喜获得{rarityName}佣兵·{coloredName}，实力大增！";
    }

    /// <summary>返回本次真解锁的佣兵 hireId（无则 null）。</summary>
    static string GrantOne(DailyLoginDefs.Grant grant, ResourceWallet.ResourceType type, int amount, string hireId)
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
                // 照 MailSystem 先例：解锁佣兵后自动补全抽奖池（主人拍板「池子最少要补全」）
                d.EnsurePoolFilled();
                Debug.Log($"[DailyLogin] 解锁佣兵 {hireId}");
                return hireId;
            }

            case DailyLoginDefs.Grant.MercFragment:
                // 【2026-10-22 施工单 T5/T6】碎片改走统一发碎片入口（背包优先→邮件），不再进 mercGrowItems
                MercGrowInventory.GrantFragments(hireId, amount, "daily-login");
                break;

            case DailyLoginDefs.Grant.Badge:
            {
                // 【2026-10-23 主人拍板 D3】升星职业徽记：由 hireId 反查职业+档位（塔克=剑盾·稀有→中级徽记），
                // 走 MercGrowInventory.Add（badge: 口径，进 mercGrowItems，与关卡掉落同仓同读）。
                if (string.IsNullOrEmpty(hireId)) break;
                if (!MercRosterDefs.TryGetByHireId(hireId, out var bdef))
                {
                    Debug.LogError($"[DailyLogin] 发徽记失败：花名册查不到 {hireId}，本次徽记未发放");
                    break;
                }
                string bJob = MercGrowSprites.JobKey(bdef.JobName);
                if (string.IsNullOrEmpty(bJob))
                {
                    Debug.LogError($"[DailyLogin] 发徽记失败：{bdef.Name} 职业名「{bdef.JobName}」识别不出徽记大类，本次徽记未发放");
                    break;
                }
                string badgeId = MercGrowInventory.BadgeId(bJob, MercStarDefs.BadgeTierOf(bdef.Rarity));
                MercGrowInventory.Add(badgeId, amount);
                Debug.Log($"[DailyLogin] 发徽记 {badgeId} ×{amount}（{bdef.Name}）");
                break;
            }

            case DailyLoginDefs.Grant.LegendaryFragment:
                // 【2026-10-22 主人拍板】「通用传说碎片 legendfrag」取消 —— 没有这个道具。
                // 表里若仍配 legendfrag（不该发生，CSV 已改钻石）fail closed 报出来，不发不吞。
                Debug.LogError($"[DailyLogin] 登录奖励配了已取消的 legendfrag（amount={amount}）——请改 daily_login.csv，本格未发放任何碎片");
                break;
        }
        return null;
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
