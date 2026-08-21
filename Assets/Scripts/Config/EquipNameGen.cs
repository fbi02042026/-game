using UnityEngine;

/// <summary>
/// 临时装备中文名（占位）。正式命名规则后续再接。
/// </summary>
public static class EquipNameGen
{
    static readonly string[] Sword =
    {
        "见习短剑", "旅人佩剑", "青锋短刃", "守夜钢剑", "林间直剑"
    };
    static readonly string[] Axe =
    {
        "伐木手斧", "裂岩短斧", "蛮力战斧"
    };
    static readonly string[] Armor =
    {
        "粗布胸甲", "皮制护心", "铁片甲衣"
    };
    static readonly string[] Generic =
    {
        "旧物", "拾荒之物", "无名装备", "旅途遗物"
    };

    public static string RandomWeaponName(EquipSlotType slot)
    {
        string[] pool = Generic;
        switch (slot)
        {
            case EquipSlotType.MainHand:
            case EquipSlotType.OffHand:
                pool = Sword;
                break;
            case EquipSlotType.Chest:
            case EquipSlotType.Head:
            case EquipSlotType.Hands:
            case EquipSlotType.Feet:
            case EquipSlotType.Cape:
                pool = Armor;
                break;
        }
        return pool[Random.Range(0, pool.Length)];
    }
}
