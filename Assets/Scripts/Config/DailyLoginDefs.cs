using System.Collections.Generic;

/// <summary>
/// 登录奖励（2026-09-18 第二版：改走佣兵线）。
///
/// 设计原则：**登录奖励是钩子，不是施舍**。它必须做到三件事——
///   1. 前 7 天让玩家拿到一个**新的构筑维度**：第 7 天直接送稀有佣兵，
///      而不是送一堆会被通胀吃掉的金币。纯给金币，玩家第二天就忘了。
///   2. 每日循环给**该佣兵的本命碎片**，让"每天回来"在长线上看得见进度。
///   3. 峰值/谷值比 ≥ 3:1：谷值日给当天能花掉的（体力），峰值日给要攒的（碎片/钻石）。
///
/// 新手 7 日按**累计登录自然日**计算，断签不重置（惩罚断签会劝退休闲玩家）；
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
    /// 第 7 天送的稀有佣兵：塔克·重盾（H003，剑盾卫士，HP140/攻10/防20，解锁价 1800）。
    /// 选它的理由：稀有档里最硬的前排（防 20 为稀有最高），坦克定位不挑流派，
    /// 中后期第 6/7 章的数值墙依然靠得住；且 InInitialPool=false ，玩家自己拿不到，
    /// 登录送才有"专属"价值。换人只改这里（后续碎片会跟着走）。
    /// </summary>
    public const string STARTER_MERC_ID = "H003";

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
    /// 新手 7 日（按累计登录天数，断签不重置）。
    /// 顺序刻意是「钱 → 体力 → 强化 → 天赋 → 钻石 → 材料 → 佣兵」：
    /// 前期给能立刻花掉的，最后一天给能改变构筑的。
    /// </summary>
    public static readonly Reward[] Starter = new Reward[]
    {
        Res("金币 ×500",         ResourceWallet.ResourceType.Gold,         500), // D1
        Res("体力 ×30",          ResourceWallet.ResourceType.Stamina,       30), // D2 = 3 次冒险
        Res("强化石 ×5",         ResourceWallet.ResourceType.EnchantStone,   5), // D3
        Res("天赋石 ×3",         ResourceWallet.ResourceType.TalentPoint,    3), // D4
        Res("钻石 ×50",          ResourceWallet.ResourceType.Diamond,       50), // D5
        Res("分解材料 ×10",      ResourceWallet.ResourceType.DecomposeMat,  10), // D6
        MercGrant("稀有佣兵\n塔克·重盾", STARTER_MERC_ID),                        // D7 峰值
    };

    /// <summary>
    /// 每日循环（7 天一轮，可无限循环）。
    /// D3 给塔克碎片——把"送出去的佣兵"变成长期养成目标，玩家会为了升星回来。
    /// D7 是周峰值：钻石 + 传说碎片，两者都是要攒的。
    /// </summary>
    public static readonly Reward[] Cycle = new Reward[]
    {
        Res("体力 ×15",           ResourceWallet.ResourceType.Stamina,       15), // D1
        Res("金币 ×250",          ResourceWallet.ResourceType.Gold,         250), // D2
        Frag("塔克碎片 ×3",       STARTER_MERC_ID,                            3), // D3
        Res("天赋石 ×1",          ResourceWallet.ResourceType.TalentPoint,    1), // D4
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
    /// 连击加成：**连续**登录满 N 天才能领，断签清零（这是唯一制造回归压力的机制）。
    /// 已领过的档位不会因为断签而回退——清零的是"连续天数"，不是"领奖记录"。
    /// 梯度按 3 / 7 / 14 / 30 天：3 天给能立刻用的，30 天给最重的长线资源。
    /// </summary>
    public static readonly StreakReward[] Streak = new StreakReward[]
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
}
