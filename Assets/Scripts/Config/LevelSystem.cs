/// <summary>
/// 等级系统：升级增加基础属性，提升四大基础属性
/// </summary>
public static class LevelSystem
{
    public static int GetExpForLevel(int level) => 50 + level * 20;

    public static void OnLevelUp(Hero hero)
    {
        hero.level++;
        hero.currentExp = 0;

        // 升级增加四大基础属性
        hero.attr.Strength += 1;
        hero.attr.Intelligence += 1;
        hero.attr.Agility += 1;
        hero.attr.Vitality += 1;

        // 同时增加战斗属性
        hero.attr.AddAttr(AttrType.Attack, 3, false);
        hero.attr.AddAttr(AttrType.MaxHp, 15, false);

        hero.currentHp = hero.attr.GetAttr(AttrType.MaxHp);
    }
}