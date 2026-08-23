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

    /// <summary>无模板名时的占位中文名（按 id 稳定哈希，避免每帧乱跳）。</summary>
    public static string TempName(string equipId)
    {
        if (string.IsNullOrEmpty(equipId)) return Generic[0];
        string id = equipId.ToLowerInvariant();
        string[] pool = Generic;
        if (id.Contains("sword") || id.Contains("blade") || id.Contains("剑"))
            pool = Sword;
        else if (id.Contains("axe") || id.Contains("斧"))
            pool = Axe;
        else if (id.Contains("armor") || id.Contains("chest") || id.Contains("helm") || id.Contains("甲"))
            pool = Armor;
        int h = 0;
        for (int i = 0; i < equipId.Length; i++)
            h = unchecked(h * 31 + equipId[i]);
        if (h < 0) h = -h;
        return pool[h % pool.Length];
    }
}
