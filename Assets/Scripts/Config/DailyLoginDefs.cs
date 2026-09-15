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

    /// <summary>
    /// 新手 7 日（按累计登录天数，断签不重置）。
    /// 2026-09-15 去零：整表 ÷10。两个例外在注释里写明——
    /// 它们是「钩子」，压到 ÷10 就没有感知了。
    /// </summary>
    public static readonly Reward[] Starter = new Reward[]
    {
        Res("金币 ×300",             ResourceWallet.ResourceType.Gold,          300), // D1
        Res("天赋石 ×1",             ResourceWallet.ResourceType.TalentPoint,     1), // D2
        Res("钻石 ×50",              ResourceWallet.ResourceType.Diamond,        50), // D3
        Res("体力 ×10",              ResourceWallet.ResourceType.Stamina,        10), // D4 例外：凑到「一次冒险」的整数，严格÷10 是 6
        Res("强化石 ×2",             ResourceWallet.ResourceType.EnchantStone,    2), // D5
        Res("分解材料 ×4",           ResourceWallet.ResourceType.DecomposeMat,    4), // D6
        new Reward { name = "史诗技能残卷 ×20", grant = Grant.EpicFragment, amount = 20 }, // D7 例外：80 片合成的 1/4，让玩家看见长线目标在动
    };

    /// <summary>每日循环（7 天一轮，可无限循环）。同样 ÷10。</summary>
    public static readonly Reward[] Cycle = new Reward[]
    {
        Res("金币 ×150",             ResourceWallet.ResourceType.Gold,          150),
        Res("体力 ×5",               ResourceWallet.ResourceType.Stamina,         5),
        Res("天赋石 ×1",             ResourceWallet.ResourceType.TalentPoint,     1),
        Res("强化石 ×2",             ResourceWallet.ResourceType.EnchantStone,    2),
        Res("钻石 ×5",               ResourceWallet.ResourceType.Diamond,         5),
        Res("分解材料 ×4",           ResourceWallet.ResourceType.DecomposeMat,    4),
        new Reward { name = "技能残卷 ×5", grant = Grant.RandomFragment, amount = 5 },
    };

    public static int CycleLength => Cycle.Length;
}
