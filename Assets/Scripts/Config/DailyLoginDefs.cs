using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 登录奖励（2026-09-18 第二版：改走佣兵线）。
///
/// 设计原则：**登录奖励是钩子，不是施舍**。它必须做到三件事——
///   1. 前 7 天让玩家拿到一个**新的构筑维度**：第 4 天直接送稀有佣兵（塔克·重盾），
///      而不是送一堆会被通胀吃掉的金币。纯给金币，玩家第二天就忘了。
///   2. 每日循环给**该佣兵的本命碎片**，让"每天回来"在长线上看得见进度。
///   3. 峰值/谷值比 ≥ 3:1：谷值日给当天能花掉的（体力），峰值日给要攒的（碎片/钻石）。
///
/// 新手 8 日按**累计登录自然日**计算，断签不重置（惩罚断签会劝退休闲玩家）；
/// 「今天必须来」的压力交给连击加成——连击断签清零，但**已领过的档位不回退**。
///
/// ⚠ 2026-09-18 口径变更：登录奖励**不再发技能残卷**（用户定：走佣兵线）。
///    技能残卷仍存在于商店直购与抽卡重复转化，本表不涉及。
///
/// 钻石：当前版本**不做内购**，钻石是纯免费货币，唯一出口是抽卡（单抽 150 / 十连 1350）。
/// 免费产出 ≈ 240~340 钻/月（Cycle 周峰值 60 + 连击 14·30 天档位），即首月约 3 次单抽。
/// 曾短暂去掉过钻石、后又按用户要求恢复，改这里之前先确认 Store 的抽卡价是否同步。
///
/// ⚠ 数值锚点（改数值前先看这里，来源是 EconomyDefs / ShopDefs 的实际定价）：
///    1 体力 ≈ 13.3 金（商店 30 点 / 400 金）　　1 天赋石 = 100 金（GOLD_PER_TALENT_POINT）
///    1 强化石 = 120 金（商店 5 个 / 600 金）　　1 分解材料 = 30 金（商店 10 个 / 300 金）
///    1 次冒险 = 10 体力 ≈ 240~690 金　　　　　　1 天自然体力 24 点 ≈ 570~1650 金
///    日价值预算：新手期 ≈ 1 天自然产出；第 8 天起 0.2~0.3 天。低于 10% 无感，高于 40% 通胀。
/// </summary>
public static class DailyLoginDefs
{
    /// <summary>
    /// 新手期送的稀有佣兵：塔克·重盾（H003，剑盾卫士，HP140/攻10/防20，解锁价 1800）。
    /// 选它的理由：稀有档里最硬的前排（防 20 为稀有最高），坦克定位不挑流派，
    /// 中后期第 6/7 章的数值墙依然靠得住；且 InInitialPool=false ，玩家自己拿不到，
    /// 登录送才有"专属"价值。换人只改这里（后续碎片会跟着走）。
    /// 落点：主人口径第 4 天给佣兵（daily_login.csv 的 starter_4），D8 起给其碎片。
    /// </summary>
    public const string STARTER_MERC_ID = "H003";

    /// <summary>
    /// 累计登录领限定佣兵：所需累计天数。表 daily_login.csv（accum 段）有数据时被覆盖取第一个 accum 行的 day；
    /// 缺表 / 空表保持 8（与主人“累计 8 天”口径一致）。
    /// </summary>
    public static int AccumTargetDays = 8;

    /// <summary>
    /// 累计登录领限定佣兵的奖励。表有 accum 行时取该行翻译结果；缺表兜底 = 送塔克·重盾（H003）。
    /// 名字不带稀有度前缀——主人要求名字只写名字（“稀有佣兵·塔克·重盾”太长）。
    /// </summary>
    public static Reward AccumReward = MercGrant("塔克·重盾", STARTER_MERC_ID);

    public enum Grant
    {
        None = 0,              // 不发（用作"第二份奖励"的空位哨兵）
        Resource = 1,          // 直接发资源（type + amount）
        Merc = 2,              // 直接解锁佣兵（hireId）
        MercFragment = 3,      // 指定佣兵的本命碎片（hireId + amount）
        LegendaryFragment = 4, // 随机传说佣兵本命碎片（amount）
    }

    public struct Reward
    {
        public string name;
        public Grant grant;
        public ResourceWallet.ResourceType type;
        public int amount;
        public string hireId;

        // 第二份奖励（grant2 == None 表示没有）。用于"钻石 + 碎片"这类组合峰值。
        public string name2;
        public Grant grant2;
        public ResourceWallet.ResourceType type2;
        public int amount2;
        public string hireId2;

        public bool HasSecond => grant2 != Grant.None && !string.IsNullOrEmpty(name2);

        /// <summary>UI 显示用：两份都有时换行拼。</summary>
        public string DisplayName => HasSecond ? name + "\n" + name2 : name;
    }

    static Reward Res(string name, ResourceWallet.ResourceType type, int amount) =>
        new Reward { name = name, grant = Grant.Resource, type = type, amount = amount };

    static Reward MercGrant(string name, string hireId) =>
        new Reward { name = name, grant = Grant.Merc, hireId = hireId };

    static Reward Frag(string name, string hireId, int amount) =>
        new Reward { name = name, grant = Grant.MercFragment, hireId = hireId, amount = amount };

    static Reward LegendFrag(string name, int amount) =>
        new Reward { name = name, grant = Grant.LegendaryFragment, amount = amount };

    /// <summary>把两份奖励合成一格（峰值日专用）。</summary>
    static Reward Pack(Reward a, Reward b) => new Reward
    {
        name = a.name, grant = a.grant, type = a.type, amount = a.amount, hireId = a.hireId,
        name2 = b.name, grant2 = b.grant, type2 = b.type, amount2 = b.amount, hireId2 = b.hireId
    };

    /// <summary>
    /// 新手 8 日【硬编码兜底】：表 daily_login.csv（starter 段）有数据时被整段覆盖。
    /// 兜底数值与"改动前"一致：D4=塔克碎片×10、D8=稀有佣兵塔克·重盾。
    /// 表里的口径（主人要求）：**D4=稀有佣兵塔克（H003）、D8 起改碎片×10**，其余相同。
    /// —— 即表优先、缺表 / 空表 / 该行缺失 回退硬编码，行为与改动前完全一致（"缺表回退，不让登录奖励开不起来"）。
    /// 节奏目标：塔克（稀有）满级要 **75 本命碎片**，D4 给佣兵、D8 起每轮碎片日补碎片，
    /// 叠加「个位 3/6/9 双倍」→ 30 天全勤 ≈ 满级。体力给上限一半（50）；强化石/天赋石已停用。
    /// 老存档已领标记按天记，兼容。注意：本数组是兜底，运行时真正生效看表（见 EnsureTableApplied）。
    /// </summary>
    public static Reward[] Starter = new Reward[]
    {
        Res("金币 ×500",         ResourceWallet.ResourceType.Gold,         500), // D1
        Res("体力 ×50",          ResourceWallet.ResourceType.Stamina,       50), // D2（体力上限一半）
        Res("钻石 ×30",          ResourceWallet.ResourceType.Diamond,       30), // D3
        Frag("塔克碎片 ×10",     STARTER_MERC_ID,                           10), // D4 兜底=碎片（新口径：格子只给碎片，佣兵改 accum 底部领取）
        Res("金币 ×800",         ResourceWallet.ResourceType.Gold,         800), // D5
        Res("体力 ×50",          ResourceWallet.ResourceType.Stamina,       50), // D6
        Res("钻石 ×50",          ResourceWallet.ResourceType.Diamond,       50), // D7
        Frag("塔克碎片 ×15", STARTER_MERC_ID, 15),                             // D8 兜底=碎片（新口径：格子只给碎片，佣兵改由 accum 累计8天底部领取，见 TryClaimAccum）
    };

    /// <summary>
    /// 轮回后 D8 的「实际奖励」：塔克已解锁就自动换成碎片 ×10。
    /// 修掉旧坑：Grant.Merc 对已解锁佣兵是空发（GrantOne 直接 break），
    /// 不换的话第 16/24/32 天点了格子什么都拿不到却变灰。
    /// UI 显示与实际发放都必须走这一个入口。
    ///
    /// 节日覆盖：仅当 day == 今天累计登录天数（即"今天"那一格）且节日表命中时，
    /// 才整条替换为节日奖励——**只覆盖 Starter 新手格**，Cycle / Streak 不动（范围不扩大）。
    /// 命中后直接返回表的翻译结果（整条覆盖）；原有 mercOwned→碎片 的兜底对节日行不强制套用，
    /// 若节日给了佣兵且已解锁，GrantOne 会安全 no-op（不会崩，只是当天无奖励，主人配节日数据时自决）。
    /// </summary>
    public static Reward EffectiveStarterReward(int day, bool mercOwned)
    {
        // 节日覆盖：仅"今天"这一格生效（只覆盖 Starter 新手格）
        if (day == DailyLoginSystem.LoginDays && DailyLoginTable.TryGetSpecialForToday(out var sp))
            return Translate(sp);

        var r = Starter[(day - 1) % Starter.Length];
        if (r.grant == Grant.Merc && mercOwned)
            r = Frag("塔克碎片 ×10", STARTER_MERC_ID, 10);
        return r;
    }

    /// <summary>
    /// X2 双倍日（累计登录第 N 天）：UI 显示「X2」角标，领取时奖励**实发翻倍**。
    /// 佣兵类（Grant.Merc）不参与翻倍，所以佣兵日天然不在其中。
    ///
    /// ⚠ 2026-09-25 规则修正：原「个位 3/6/9」是十进制尾数口径——
    ///   界面是预制体固定 **8 格**（Cell_1~8），而 8 与 10 的最小公倍数是 40，
    ///   即双倍日与格子的对应关系要 40 天才循环一次，玩家根本记不住哪天双倍。
    ///   改为**按轮次取模**：每轮 8 格里固定第 3 格、第 6 格双倍，每 8 天固定 2 天，
    ///   永远落在界面第 3 格和第 6 格（X2 角标玩家一眼能对上），可预期、会惦记。
    ///   改规则就改 IsStarterDouble 这一个方法（UI 角标与实际发放都走它）。
    /// </summary>
    public static readonly int[] StarterDoubleDays = { 3, 6 };  // 仅作文档用：每轮第 3、6 格双倍（与 IsStarterDouble 同步）

    public static bool IsStarterDouble(int day)
    {
        // 按轮次取模：每 8 格一轮，固定第 3、第 6 格双倍（永远落在界面第 3、6 格，对应 X2 角标）。
        int slot = ((day - 1) % 8) + 1;
        return slot == 3 || slot == 6;
    }

    /// <summary>把一份奖励的数量翻倍（佣兵类不动，资源/碎片类数量 ×2）。</summary>
    public static Reward Doubled(Reward r)
    {
        var c = r;
        if (c.grant != Grant.Merc) c.amount *= 2;
        if (c.HasSecond && c.grant2 != Grant.Merc) c.amount2 *= 2;
        return c;
    }

    /// <summary>
    /// 每日循环（7 天一轮，可无限循环）。【硬编码兜底，表 daily_login.csv（cycle 段）优先覆盖】
    /// D3 给塔克碎片——把"送出去的佣兵"变成长期养成目标，玩家会为了升星回来。
    /// D7 是周峰值：钻石 + 传说碎片，两者都是要攒的。
    /// </summary>
    public static Reward[] Cycle = new Reward[]
    {
        Res("体力 ×15",           ResourceWallet.ResourceType.Stamina,       15), // D1
        Res("金币 ×250",          ResourceWallet.ResourceType.Gold,         250), // D2
        Frag("塔克碎片 ×3",       STARTER_MERC_ID,                            3), // D3
        Res("天赋石 ×3",          ResourceWallet.ResourceType.TalentPoint,    3), // D4 每日登录保底 3 石
        Res("强化石 ×2",          ResourceWallet.ResourceType.EnchantStone,   2), // D5
        Res("分解材料 ×8",        ResourceWallet.ResourceType.DecomposeMat,   8), // D6
        Pack(Res("钻石 ×60",      ResourceWallet.ResourceType.Diamond,       60),
             LegendFrag("传说碎片 ×2", 2)),                                      // D7 周峰值
    };

    public struct StreakReward
    {
        /// <summary>连续登录满几天可领。</summary>
        public int days;
        public Reward reward;
    }

    /// <summary>
    /// 连击加成：**连续**登录满 N 天才能领，断签清零（这是唯一制造回归压力的机制）。【硬编码兜底，表 daily_login.csv（streak 段）优先覆盖】
    /// 已领过的档位不会因为断签而回退——清零的是"连续天数"，不是"领奖记录"。
    /// 梯度按 3 / 7 / 14 / 30 天：3 天给能立刻用的，30 天给最重的长线资源。
    /// </summary>
    public static StreakReward[] Streak = new StreakReward[]
    {
        new StreakReward { days = 3,  reward = Res("体力 ×10", ResourceWallet.ResourceType.Stamina, 10) },
        new StreakReward { days = 7,  reward = Frag("塔克碎片 ×5", STARTER_MERC_ID, 5) },
        new StreakReward { days = 14, reward = Pack(Res("钻石 ×30", ResourceWallet.ResourceType.Diamond, 30),
                                                    LegendFrag("传说碎片 ×3", 3)) },
        new StreakReward { days = 30, reward = Pack(Res("钻石 ×100", ResourceWallet.ResourceType.Diamond, 100),
                                                    LegendFrag("传说碎片 ×15", 15)) },
    };

    public static int CycleLength => Cycle.Length;

    public static StreakReward? StreakFor(int days)
    {
        for (int i = 0; i < Streak.Length; i++)
            if (Streak[i].days == days) return Streak[i];
        return null;
    }

    // ============================================================
    // 配表优先 + 硬编码兜底
    // ============================================================

    /// <summary>
    /// 首次访问本类（静态构造）与显式 EnsureLoaded 都会走到这里：
    /// 把 daily_login.csv 翻译进 Starter / Cycle / Streak；表里没有（缺表 / 空表 / 该行缺失）
    /// 就保持上面的硬编码兜底，行为与改动前完全一致。
    /// </summary>
    static DailyLoginDefs()
    {
        try { EnsureTableApplied(); }
        catch (System.Exception e)
        {
            // 保险丝：配表应用失败绝不能让整个类加载崩（否则登录奖励全开不起来）。
            // 静默回退到硬编码数组即可。
            Debug.LogError("[DailyLoginDefs] 配表应用失败，回退硬编码：" + e.Message);
        }
    }

    /// <summary>显式确保配表已应用（与静态构造二选一，幂等）。</summary>
    public static void EnsureLoaded() => EnsureTableApplied();

    static void EnsureTableApplied()
    {
        DailyLoginTable.EnsureLoaded();

        // 表缺 / 空表：保持硬编码兜底（行为与改动前完全一致）。逐段独立判断。
        if (DailyLoginTable.StarterCount > 0)
        {
            bool ok = true;
            var arr = new Reward[DailyLoginTable.StarterCount];
            for (int day = 1; day <= DailyLoginTable.StarterCount; day++)
            {
                if (DailyLoginTable.TryGetStarter(day, out var row)) arr[day - 1] = Translate(row);
                else { ok = false; break; }   // 该行缺失 → 整段回退硬编码
            }
            if (ok) Starter = arr;
        }

        if (DailyLoginTable.CycleCount > 0)
        {
            bool ok = true;
            var arr = new Reward[DailyLoginTable.CycleCount];
            for (int day = 1; day <= DailyLoginTable.CycleCount; day++)
            {
                if (DailyLoginTable.TryGetCycle(day, out var row)) arr[day - 1] = Translate(row);
                else { ok = false; break; }
            }
            if (ok) Cycle = arr;
        }

        if (DailyLoginTable.StreakCount > 0)
        {
            var src = DailyLoginTable.StreakRows;
            var arr = new StreakReward[src.Count];
            for (int i = 0; i < src.Count; i++)
                arr[i] = new StreakReward { days = src[i].day, reward = Translate(src[i]) };
            Streak = arr;
        }

        // accum 段：累计登录领限定佣兵。取第一个 accum 行（按 day 升序）的 day 作为目标天数、
        // 该行翻译结果作为奖励；缺表 / 空表 / 无 accum 行则保持上面的硬编码兜底（AccumTargetDays=8、AccumReward=送塔克）。
        if (DailyLoginTable.AccumCount > 0)
        {
            var first = DailyLoginTable.AccumRows[0];
            AccumTargetDays = first.day;
            AccumReward = Translate(first);
        }
    }

    /// <summary>把配表一行翻译成现有的 Reward 结构（DailyLoginTable.Row → Reward）。</summary>
    static Reward Translate(DailyLoginTable.Row row)
    {
        var r = new Reward
        {
            name = row.name,
            grant = ParseGrant(row.grant),
            amount = row.amount,
        };
        if (r.grant == Grant.Resource) { r.type = ParseResourceType(row.param); r.hireId = ""; }
        else if (r.grant == Grant.Merc || r.grant == Grant.MercFragment) { r.hireId = row.param; }
        else { r.hireId = ""; }

        // 第二份奖励（组合峰值，如「钻石 + 传说碎片」）
        r.grant2 = ParseGrant(row.grant2);
        r.amount2 = row.amount2;
        r.name2 = row.name2;
        if (r.grant2 == Grant.Resource) { r.type2 = ParseResourceType(row.param2); r.hireId2 = ""; }
        else if (r.grant2 == Grant.Merc || r.grant2 == Grant.MercFragment) { r.hireId2 = row.param2; }
        else { r.hireId2 = ""; }

        return r;
    }

    /// <summary>配表 grant 文本 → Grant 枚举。</summary>
    static Grant ParseGrant(string s)
    {
        switch (s.Trim().ToLowerInvariant())
        {
            case "resource": return Grant.Resource;
            case "merc": return Grant.Merc;
            case "mercfrag": return Grant.MercFragment;
            case "legendfrag": return Grant.LegendaryFragment;
            case "none":
            case "":
            default: return Grant.None;
        }
    }

    /// <summary>配表 param 资源名 → ResourceWallet.ResourceType。</summary>
    static ResourceWallet.ResourceType ParseResourceType(string s)
    {
        switch (s.Trim().ToLowerInvariant())
        {
            case "gold": return ResourceWallet.ResourceType.Gold;
            case "diamond": return ResourceWallet.ResourceType.Diamond;
            case "stamina": return ResourceWallet.ResourceType.Stamina;
            case "talent": return ResourceWallet.ResourceType.TalentPoint;
            case "enchant": return ResourceWallet.ResourceType.EnchantStone;
            case "decompose": return ResourceWallet.ResourceType.DecomposeMat;
            default:
                Debug.LogWarning("[DailyLoginDefs] 未知资源类型：" + s + "，按 Gold 处理");
                return ResourceWallet.ResourceType.Gold;
        }
    }
}
