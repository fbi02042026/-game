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
    PhyPower,       // 物理强度
        CritDamage,     // 暴击伤害倍率（1.5 = 150%）
        EliteDamage,     // 对精英/Boss 的伤害加成倍率（0.08 = +8%）
        // 2026-09-26 新增（追加在末尾，不改变既有枚举值，避免序列化错位）
        MagicAttack,     // 魔法攻击力：法师/牧师类与法球怪专用；未写入时回退 Attack
        MagicDefense,    // 魔法防御：魔法伤害扣这一项；未写入时回退 Defense × GameConfig.MAGIC_DEFENSE_FALLBACK_RATIO
        // 2026-09-26 主人拍板：落地「中毒」装备词缀（游侠武器专属，百分比制），供 PoisonDotRunner 消费
        Poison           // 中毒：装备词缀值（如 0.10 = 10%）；每跳伤害系数见 GameConfig.POISON_DPS_RATIO
}