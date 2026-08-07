/// <summary>
/// 属性类型枚举，后期加新属性直接在这里加，不用改其他底层逻辑
/// </summary>
public enum AttrType
{
    // === 四大基础属性（玩家基础属性，影响派生属性） ===
    Strength,       // 力量：影响物理攻击力
    Intelligence,   // 智力：影响魔法攻击力
    Agility,        // 敏捷：影响攻速和暴击率
    Vitality,       // 体质：影响生命值和防御

    // === 战斗属性 ===
    MaxHp,
    Attack,
    AttackSpeed,
    CritRate,
    MoveSpeed,
    AttackRange,
    Defense,        // 防御力

    // === 扩展属性 ===
    LifeSteal,
    Dodge,
    FireDamage,
    IceDamage,
    ExpBonus,
    GoldBonus,
    CooldownReduce,
    MagicPower,     // 魔法强度
    PhyPower        // 物理强度
}