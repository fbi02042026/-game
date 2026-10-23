/// <summary>
/// 佣兵局外升星纯数据表（2026-10-22 主人拍板，施工单《佣兵升星与本命碎片入包》T3）。
///
/// 口径（全部定稿，改动只动这里）：
///   · 上限 MaxOutStar=3 星（获得时零星；以后再开更多星改这里留扩位）。
///   · 升星消耗（本命碎片，按稀有度）：
///       普通 10/30/50、稀有 10/20/40（2026-10-22 三次拍板，替代原 5/10/30）、传奇 2/3/5。
///   · 升星徽记消耗（该职业徽记，2026-10-23 主人拍板）：
///       普通 0/20/30（低级）、稀有 0/10/20（中级）、传奇 2/5/10（高级，传奇 1 星起就要）。
///   · 重复获得转化数：普通 ×10 / 稀有 ×5 / 传奇 ×1（替代 MercGrowInventory.DUP_FRAG_* 8/20/50）。
///   · 星级加成（外层乘区，只动攻防四条，血/移速不动）：
///       每星 物攻(Attack)/物防(Defense) +8%、魔攻(MagicAttack)/魔防(MagicDefense) +7%
///       （2026-10-22 三次拍板「先按8%加吧 魔按7%」）。
///   · 满星总消耗（自洽校验）：普通 9 次重复 ×10 = 90、稀有 14 ×5 = 70、传奇 10 ×1 = 10。
///   · 无城镇等级限制（不挂任何解锁门）。
/// </summary>
public static class MercStarDefs
{
    /// <summary>局外升星上限。2026-10-22 主人拍板「升星上限先 3 星」，以后开新档位改这里。</summary>
    public const int MaxOutStar = 3;

    /// <summary>每星物攻/物防加成（+8%，外层乘区）。</summary>
    public const float AtkPctPerStar = 0.08f;

    /// <summary>每星魔攻/魔防加成（+7%，外层乘区）。</summary>
    public const float MagicPctPerStar = 0.07f;

    /// <summary>满星碎片攒够后的招募降权系数（权重 ×0.2，2026-10-22 主人拍板建议值，重掷照旧）。</summary>
    public const float FullStarRecruitDownWeight = 0.2f;

    /// <summary>
    /// 从当前星升到 targetStar（1~3）所需的该佣兵本命碎片数。
    /// targetStar 越界返回 0（调用方视为不可升）。
    /// </summary>
    public static int StarUpCost(MercRosterDefs.MercRarity rarity, int targetStar)
    {
        switch (rarity)
        {
            case MercRosterDefs.MercRarity.Legendary:
                return targetStar == 1 ? 2 : targetStar == 2 ? 3 : targetStar == 3 ? 5 : 0;
            case MercRosterDefs.MercRarity.Rare:
                return targetStar == 1 ? 10 : targetStar == 2 ? 20 : targetStar == 3 ? 40 : 0;
            default:
                return targetStar == 1 ? 10 : targetStar == 2 ? 30 : targetStar == 3 ? 50 : 0;
        }
    }

    /// <summary>升星徽记档位：普通佣兵=低级("普通")、稀有=中级("稀有")、传奇=高级("传奇")。</summary>
    public static string BadgeTierOf(MercRosterDefs.MercRarity rarity)
    {
        switch (rarity)
        {
            case MercRosterDefs.MercRarity.Legendary: return "传奇";
            case MercRosterDefs.MercRarity.Rare: return "稀有";
            default: return "普通";
        }
    }

    /// <summary>
    /// 升到 targetStar（1~3）所需的**该职业**徽记数量；越界返回 0。
    /// 普通 0/20/30（低级）；稀有 0/10/20（中级）；传奇 2/5/10（高级）——2026-10-23 主人拍板。
    /// </summary>
    public static int BadgeCost(MercRosterDefs.MercRarity rarity, int targetStar)
    {
        switch (rarity)
        {
            case MercRosterDefs.MercRarity.Legendary:
                return targetStar == 1 ? 2 : targetStar == 2 ? 5 : targetStar == 3 ? 10 : 0;
            case MercRosterDefs.MercRarity.Rare:
                return targetStar == 1 ? 0 : targetStar == 2 ? 10 : targetStar == 3 ? 20 : 0;
            default:
                return targetStar == 1 ? 0 : targetStar == 2 ? 20 : targetStar == 3 ? 30 : 0;
        }
    }

    /// <summary>买重复佣兵转本命碎片的数量（2026-10-22 拍板 10/5/1，替代旧 8/20/50）。</summary>
    public static int DupFragCount(MercRosterDefs.MercRarity rarity)
    {
        switch (rarity)
        {
            case MercRosterDefs.MercRarity.Legendary: return 1;
            case MercRosterDefs.MercRarity.Rare: return 5;
            default: return 10;
        }
    }

    // ============================ 升阶资金门槛（2026-10-22 新增）============================

    /// <summary>
    /// 升阶金币消耗（2026-10-22 主人拍板：升阶消耗玩家资金当门槛，传奇扣钻石、普通/稀有扣金币）。
    /// 以下为建议值待主人调：普通 300/600/1000、稀有 600/1200/2000（按目标星 1/2/3）；
    /// 传奇返回 0（走 DiamondCost）；targetStar 越界返回 0（调用方视为不可升）。
    /// </summary>
    public static int GoldCost(MercRosterDefs.MercRarity rarity, int targetStar)
    {
        switch (rarity)
        {
            case MercRosterDefs.MercRarity.Legendary:
                return 0;   // 传奇扣钻石，见 DiamondCost
            case MercRosterDefs.MercRarity.Rare:
                return targetStar == 1 ? 600 : targetStar == 2 ? 1200 : targetStar == 3 ? 2000 : 0;
            default:
                return targetStar == 1 ? 300 : targetStar == 2 ? 600 : targetStar == 3 ? 1000 : 0;
        }
    }

    /// <summary>
    /// 升阶钻石消耗（2026-10-22 主人拍板：传奇消耗钻石）。仅传奇非 0，
    /// 200/400/600（按目标星 1/2/3；2026-10-22 主人拍板定稿，替代原建议值 5/10/20）；
    /// 其余稀有度、越界一律返回 0。
    /// </summary>
    public static int DiamondCost(MercRosterDefs.MercRarity rarity, int targetStar)
    {
        if (rarity != MercRosterDefs.MercRarity.Legendary) return 0;
        return targetStar == 1 ? 200 : targetStar == 2 ? 400 : targetStar == 3 ? 600 : 0;
    }

    /// <summary>该稀有度从 0 星升满 3 星的总消耗（降权阈值：普通90/稀有70/传奇10）。</summary>
    public static int FullStarFragTotal(MercRosterDefs.MercRarity rarity)
    {
        int total = 0;
        for (int s = 1; s <= MaxOutStar; s++)
            total += StarUpCost(rarity, s);
        return total;
    }

    /// <summary>本命碎片道具 id 前缀（2026-10-22 施工单 T2：背包/邮件等处识别碎片道具用，唯一真源）。</summary>
    public const string FragmentIdPrefix = "item_frag_";

    /// <summary>该佣兵的本命碎片道具 id（item_defs.csv，T1 已落 22 行）。</summary>
    public static string FragmentItemId(string hireId)
    {
        if (string.IsNullOrEmpty(hireId)) return null;
        return FragmentIdPrefix + hireId;
    }

    /// <summary>
    /// 从碎片道具 id 反解 hireId（2026-10-22 施工单 T2：背包碎片格叠佣兵头像用）；
    /// 不是碎片道具 id 返回 null。
    /// </summary>
    public static string HireIdOfFragmentItem(string defId)
    {
        if (string.IsNullOrEmpty(defId) || !defId.StartsWith(FragmentIdPrefix)) return null;
        var hireId = defId.Substring(FragmentIdPrefix.Length);
        return string.IsNullOrEmpty(hireId) ? null : hireId;
    }
}
