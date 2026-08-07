/// <summary>
/// 武器类型
/// </summary>
public enum WeaponType
{
    OneHand,    // 单手武器（可配盾牌）
    TwoHand,    // 双手武器（占主手+副手）
    Dual,       // 双持武器（副手可再装一把单手武器）
    None        // 不是武器
}

/// <summary>
/// 武器攻击属性
/// </summary>
public enum WeaponAttackType
{
    Physical,   // 物理攻击
    Magic,      // 魔法攻击
    Hybrid      // 混合攻击
}