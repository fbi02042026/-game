/// <summary>
/// 属性归属。AttrSystem 用它决定是否套职业表 / 是否叠力量→攻击，不再问 Hero.Instance。
/// </summary>
public enum AttrOwnerKind
{
    Unspecified = 0,
    Player = 1,
    Merc = 2,
    Monster = 3
}
