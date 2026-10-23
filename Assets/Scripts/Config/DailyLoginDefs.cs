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
    /// 落点：【2026-10-20 主人拍板】第 1 天给佣兵（daily_login.csv 的 starter_1；
    /// 塔克已默认解锁 → 实际自动折算碎片×10），D4 起给碎片。
    /// </summary>
    public const string STARTER_MERC_ID = "H003";

    /// <summary>
    /// 累计登录领限定佣兵：所需累计天数。表 daily_login.csv（accum 段）有数据时被覆盖取第一个 accum 行的 day；
    /// 缺表 / 空表保持 8（与主人“累计 8 天”口径一致）。
    /// </summary>
    public static int AccumTargetDays = 8;

    /// <summary>
    /// 累计登录（默认第 8 天）底部那一格的奖励。表有 accum 行时取该行翻译结果。
    ///
    /// <para>【2026-10-22 主人拍板 T6】accum_8 的塔克碎片×15 取消（碎片只走统一发碎片入口、
    /// 且改按新手塔克节奏发），改「金币 ×500 + 天赋石 ×3」（与 daily_login.csv 同步）。</para>
    /// </summary>
    public static Reward AccumReward = Pack(
        Res("金币 ×500", ResourceWallet.ResourceType.Gold, 500),
        Res("天赋石 ×3", ResourceWallet.ResourceType.TalentPoint, 3));

    public enum Grant
    {
        None = 0,              // 不发（用作"第二份奖励"的空位哨兵）
        Resource = 1,          // 直接发资源（type + amount）
        Merc = 2,              // 直接解锁佣兵（hireId）
        MercFragment = 3,      // 指定佣兵的本命碎片（hireId + amount）
        LegendaryFragment = 4, // 随机传说佣兵本命碎片（amount）
        Badge = 5,             // 【2026-10-23 主人拍板 D3】升星职业徽记（hireId 反查职业+档位 + amount）
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

    static Reward MercGrant(string name, string hireId) =>
        new Reward { name = name, grant = Grant.Merc, hireId = hireId };

    static Reward Frag(string name, string hireId, int amount) =>
        new Reward { name = name, grant = Grant.MercFragment, hireId = hireId, amount = amount };

    /// <summary>升星职业徽记（2026-10-23 D3）：param/hireId 填佣兵，发放时反查职业+档位组 badge id。</summary>
    static Reward Badge(string name, string hireId, int amount) =>
        new Reward { name = name, grant = Grant.Badge, hireId = hireId, amount = amount };

    static Reward Res(string name, ResourceWallet.ResourceType type, int amount) =>
        new Reward { name = name, grant = Grant.Resource, type = type, amount = amount };

    /// <summary>把两份奖励合成一格（峰值日专用）。</summary>
    static Reward Pack(Reward a, Reward b) => new Reward
    {
        name = a.name, grant = a.grant, type = a.type, amount = a.amount, hireId = a.hireId,
        name2 = b.name, grant2 = b.grant, type2 = b.type, amount2 = b.amount, hireId2 = b.hireId
    };

    // 【2026-10-22 主人拍板】「通用传说碎片 legendfrag」取消——没有这个道具，LegendFrag 构造器删除；
    // cycle_7 / streak_14 / streak_30 的 grant2 改钻石（见下方数组与 daily_login.csv）。

    /// <summary>
    /// 新手 8 日【硬编码兜底】：表 daily_login.csv（starter 段）有数据时被整段覆盖。
    /// 【2026-10-22 主人拍板】碎片只放在第 2/4/6/8 天（1星×10 / 2星×20 / 3星×20+20，合计正好70、零溢出），
    /// 【2026-10-23 主人拍板】D1=塔克本体、D3=中级徽记×30（升2星10+升3星20 全包；双倍日已取消）、D5=金币、D7=钻石×200；登录奖励不再发体力。
    /// 塔克（H003）已移出默认解锁名单（SaveData.DefaultUnlockedMercIds，2026-10-20 二次拍板），
    /// D1 真发佣兵本体+报喜；碎片按 10/20/20/20 放第 2/4/6/8 天（对齐升星消耗 10/20/40：
    /// D2 后可升1星、D4 后升2星、D6+D8 后升3星——2026-10-23 主人重申口径）。
    /// —— 表优先、缺表 / 空表 / 该行缺失回退本数组（"缺表回退，不让登录奖励开不起来"）。
    /// 节奏目标：塔克（稀有）满星要 **70 本命碎片**，D2/D4 发 10/20（1星/2星），D6/D8 各发 20 凑齐 3星40，合计正好 70、零溢出。
    /// 体力给上限一半（50）；强化石已停用。
    /// 老存档已领标记按天记，兼容。注意：本数组是兜底，运行时真正生效看表（见 EnsureTableApplied）。
    /// </summary>
    public static Reward[] Starter = new Reward[]
    {
        MercGrant("塔克·重盾",   STARTER_MERC_ID),                                // D1 稀有佣兵本体（2026-10-23 主人重申：D1=塔克；塔克移出默认名单真发本体+报喜，领过才折碎片）
        Frag("塔克碎片 ×10",     STARTER_MERC_ID,                           10), // D2（2026-10-22 主人拍板：第2天给1星碎片×10）
        // 【2026-10-23 主人拍板】D3 发塔克升星徽记：塔克=剑盾·稀有 →「中级徽记」；双倍日已取消，直发 30 =
        // 升2星10 + 升3星20 全包（消耗见 MercStarDefs.BadgeCost）。
        Badge("中级徽记 ×30",    STARTER_MERC_ID,                           30), // D3（双倍日取消后直发30；原金币×120）
        Frag("塔克碎片 ×20",     STARTER_MERC_ID,                           20), // D4（2026-10-22 主人拍板：第4天给2星碎片×20）
        Res("金币 ×500",         ResourceWallet.ResourceType.Gold,          500), // D5（2026-10-23 主人拍板：800→500）
        Frag("塔克碎片 ×20",     STARTER_MERC_ID,                           20), // D6（2026-10-22 主人拍板：第6天给3星碎片×20，与D8合计40=第3星；累计D8满星70零溢出）
        Res("钻石 ×200",         ResourceWallet.ResourceType.Diamond,      200), // D7（2026-10-23 主人拍板：改钻石×200，原金币×200）
        Frag("塔克碎片 ×20",     STARTER_MERC_ID,                           20), // D8（2026-10-22 主人拍板：第8天给3星碎片×20，与D6合计40=第3星；累计70零溢出）
    };

    /// <summary>
    /// 唯一出口：界面显示与实际发放都走这里。
    /// 备注：自 2026-10-22 主人拍板，Starter 已无「佣兵本体」格（碎片直接放第2/4/6/8天），
    /// 故下面 mercOwned→碎片 的兜底分支对常规新手格不再触发（仅对节日表可能给佣兵的情形生效）。
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
    ///
    /// 【2026-10-23 主人拍板】**双倍日机制取消**——IsStarterDouble 一律返回 false：
    /// X2 角标（Refresh）与实发翻倍（TryClaimStarter）都走本方法，改这一处全局失效；
    /// D3 徽记改为直发 30（升2星10+升3星20 全包，不再靠翻倍凑数）。
    /// 历史口径（按轮次取模每轮第 3 格双倍，2026-09-25 定、2026-10-22 保留）作废。
    /// </summary>
    public static readonly int[] StarterDoubleDays = { };   // 【2026-10-23】双倍日取消，清空（仅作文档用）

    public static bool IsStarterDouble(int day)
    {
        // 【2026-10-23 主人拍板】登录奖励不再有双倍日，恒 false（day 参数保留为兼容签名）。
        return false;
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
    /// D7 是周峰值：【2026-10-20 钻石停发】原「钻石+传说碎片」→「金币+传说碎片」（1钻=4金折算）。
    /// </summary>
    public static Reward[] Cycle = new Reward[]
    {
        Res("金币 ×200",          ResourceWallet.ResourceType.Gold,         200), // D1（2026-10-23 主人拍板：不再发体力，原体力×15 按商店汇率折金币×200）
        Res("金币 ×250",          ResourceWallet.ResourceType.Gold,         250), // D2
        // 【2026-10-22 主人拍板 T6】塔克碎片×3 取消（碎片只走统一发碎片入口），改金币×240
        Res("金币 ×240",          ResourceWallet.ResourceType.Gold,         240), // D3（2026-10-22：原塔克碎片×3）
        Res("天赋石 ×3",          ResourceWallet.ResourceType.TalentPoint,    3), // D4 每日登录保底 3 石
        // 【2026-10-17 主人拍板】强化石暂无用途 → D5 原「强化石×2」按商店汇率（1石=120金）折成金币×240（表 daily_login 同步改）
        Res("金币 ×240",          ResourceWallet.ResourceType.Gold,          240), // D5
        // 【2026-10-18 主人拍板：材料产出全部改发抽奖币】D6 原「分解材料×8」按商店汇率（1材料=30金）折成金币×240（表 daily_login 同步改）
        Res("金币 ×240",          ResourceWallet.ResourceType.Gold,          240), // D6
        // 【2026-10-22 主人拍板 T6】D7 周峰值第二份：传说碎片×2 取消 → 钻石×15（钻石资源正式启用）
        Pack(Res("金币 ×240",     ResourceWallet.ResourceType.Gold,         240),  // D7 周峰值（2026-10-20 钻石停发：原钻石×60 折金币×240）
             Res("钻石 ×15",      ResourceWallet.ResourceType.Diamond,       15)),
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
        new StreakReward { days = 3,  reward = Res("金币 ×130", ResourceWallet.ResourceType.Gold, 130) }, // 2026-10-23 主人拍板：不再发体力，原体力×10 按商店汇率折金币×130
        // 【2026-10-22 主人拍板 T6】塔克碎片×5 取消 → 天赋石×5
        new StreakReward { days = 7,  reward = Res("天赋石 ×5", ResourceWallet.ResourceType.TalentPoint, 5) },
        // 【2026-10-22 主人拍板 T6】传说碎片取消 → 钻石（14 天 ×8 / 30 天 ×25）
        new StreakReward { days = 14, reward = Pack(Res("金币 ×120", ResourceWallet.ResourceType.Gold, 120),
                                                    Res("钻石 ×8",  ResourceWallet.ResourceType.Diamond, 8)) },
        new StreakReward { days = 30, reward = Pack(Res("金币 ×400", ResourceWallet.ResourceType.Gold, 400),
                                                    Res("钻石 ×25", ResourceWallet.ResourceType.Diamond, 25)) },
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

        // accum 段：累计登录底部那一格。取第一个 accum 行（按 day 升序）的 day 作为目标天数、
        // 该行翻译结果作为奖励；缺表 / 空表 / 无 accum 行则保持上面的硬编码兜底
        //（AccumTargetDays=8、AccumReward=塔克碎片×15 —— 佣兵本体已在第 4 天发过，这里不再重复送）。
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
        else if (r.grant == Grant.Merc || r.grant == Grant.MercFragment || r.grant == Grant.Badge) { r.hireId = row.param; }
        else { r.hireId = ""; }

        // 第二份奖励（组合峰值，如「钻石 + 传说碎片」）
        r.grant2 = ParseGrant(row.grant2);
        r.amount2 = row.amount2;
        r.name2 = row.name2;
        if (r.grant2 == Grant.Resource) { r.type2 = ParseResourceType(row.param2); r.hireId2 = ""; }
        else if (r.grant2 == Grant.Merc || r.grant2 == Grant.MercFragment || r.grant2 == Grant.Badge) { r.hireId2 = row.param2; }
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
            case "badge": return Grant.Badge;   // 2026-10-23 D3 升星徽记
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
