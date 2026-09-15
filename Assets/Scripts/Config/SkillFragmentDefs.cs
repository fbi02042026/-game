/// <summary>
/// 技能碎片规则（2026-09-15 新增）。
///
/// 为什么要有碎片：商店里「史诗 / 传说」技能如果也能直接买断，
/// 稀有度就只剩下颜色意义——玩家攒够钱就能一次性拿走顶级技能，
/// 抽卡池也失去存在价值。碎片把「顶级技能」从**一次性大额消费**
/// 变成**多次小额累积**，同时给抽卡一个后期仍有意义的出口（重复转碎片）。
///
/// 每个技能有自己的碎片（不共用），保证玩家能**定向**追求想要的技能；
/// 传说技能碎片极难凑齐，所以它仍然主要靠章节 / 成就白给——碎片只是加速。
/// </summary>
public static class SkillFragmentDefs
{
    /// <summary>碎片道具名。</summary>
    public const string ITEM_NAME = "技能残卷";

    /// <summary>合成一个技能需要的该技能碎片数。</summary>
    public static int CostToCraft(SkillRarity r)
    {
        switch (r)
        {
            case SkillRarity.Legendary: return 150;
            case SkillRarity.Epic: return 80;
            case SkillRarity.Rare: return 40;
            default: return 20;
        }
    }

    /// <summary>抽卡抽到「已解锁」技能时，转化出的碎片数。</summary>
    public static int ConvertFromDuplicate(SkillRarity r)
    {
        switch (r)
        {
            case SkillRarity.Legendary: return 60;
            case SkillRarity.Epic: return 30;
            case SkillRarity.Rare: return 15;
            default: return 8;
        }
    }

    /// <summary>商店直购 1 片碎片的金币单价。传说碎片不卖（只能靠抽卡重复与成就）。</summary>
    public static int GoldPerFragment(SkillRarity r)
    {
        switch (r)
        {
            case SkillRarity.Epic: return 250;
            case SkillRarity.Rare: return 120;
            default: return 60;
        }
    }

    /// <summary>该稀有度能否在商店直购碎片。</summary>
    public static bool CanBuyFragment(SkillRarity r) => r != SkillRarity.Legendary;

    public static string FragmentName(string skillId)
    {
        var def = PlayerSkillDefs.GetById(skillId);
        string name = def != null ? def.displayName : skillId;
        return name + ITEM_NAME;
    }
}
