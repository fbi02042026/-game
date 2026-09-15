using System.Collections.Generic;

/// <summary>
/// 登录奖励（2026-09-15 新增）。
///
/// 设计原则：**登录奖励是钩子，不是施舍**。它必须做到两件事——
///   1. 前 7 天给的东西要能让玩家**明显感觉到构筑在长**（技能 / 碎片 / 抽卡券），
///      纯给金币会被通胀吃掉，玩家第二天就忘了。
///   2. 每日循环的奖励要**小但稳定**，且第 7 天给一个"值得回来"的东西，
///      让"连续登录"这件事本身有节奏。
///
/// 新手 7 日按「累计登录自然日」计算，**断签不重置**——
/// 惩罚断签会让休闲玩家直接放弃，而我们要的是长期留存不是日活 KPI。
/// </summary>
public static class DailyLoginDefs
{
    public enum Grant
    {
        Resource = 0,      // 直接发资源（type + amount）
        EpicFragment = 1,  // 随机一个史诗技能 +amount 碎片
        RandomFragment = 2 // 随机一个任意技能 +amount 碎片
    }

    public struct Reward
    {
        public string name;
        public Grant grant;
        public ResourceWallet.ResourceType type;
        public int amount;
    }

    static Reward Res(string name, ResourceWallet.ResourceType type, int amount) =>
        new Reward { name = name, grant = Grant.Resource, type = type, amount = amount };

    /// <summary>新手 7 日（按累计登录天数，断签不重置）。</summary>
    public static readonly Reward[] Starter = new Reward[]
    {
        Res("金币 ×3000",            ResourceWallet.ResourceType.Gold,         3000), // D1
        Res("天赋石 ×5",             ResourceWallet.ResourceType.TalentPoint,    5), // D2
        Res("钻石 ×500",             ResourceWallet.ResourceType.Diamond,       500), // D3（≈一次单抽）
        Res("体力 ×60",              ResourceWallet.ResourceType.Stamina,        60), // D4
        Res("强化石 ×20",            ResourceWallet.ResourceType.EnchantStone,   20), // D5
        Res("分解材料 ×40",          ResourceWallet.ResourceType.DecomposeMat,   40), // D6
        new Reward { name = "史诗技能残卷 ×40", grant = Grant.EpicFragment, amount = 40 }, // D7
    };

    /// <summary>每日循环（7 天一轮，可无限循环）。</summary>
    public static readonly Reward[] Cycle = new Reward[]
    {
        Res("金币 ×1500",            ResourceWallet.ResourceType.Gold,         1500),
        Res("体力 ×20",              ResourceWallet.ResourceType.Stamina,        20),
        Res("天赋石 ×1",             ResourceWallet.ResourceType.TalentPoint,     1),
        Res("强化石 ×5",             ResourceWallet.ResourceType.EnchantStone,    5),
        Res("钻石 ×30",              ResourceWallet.ResourceType.Diamond,        30),
        Res("分解材料 ×20",          ResourceWallet.ResourceType.DecomposeMat,    20),
        new Reward { name = "技能残卷 ×10", grant = Grant.RandomFragment, amount = 10 },
    };

    public static int CycleLength => Cycle.Length;
}
