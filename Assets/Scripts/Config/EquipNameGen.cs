using UnityEngine;

/// <summary>
/// 装备显示名兜底：模板已填中文名时优先用模板；否则按 id 稳定映射。
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

    public static string DisplayName(EquipTemplate tpl)
    {
        if (tpl == null) return Generic[0];
        if (!string.IsNullOrEmpty(tpl.equipName) && HasChinese(tpl.equipName))
            return tpl.equipName;
        return TempName(string.IsNullOrEmpty(tpl.templateId) ? tpl.equipName : tpl.templateId);
    }

    static bool HasChinese(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c >= 0x4e00 && c <= 0x9fff) return true;
        }
        return false;
    }

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

    /// <summary>无中文模板名时的兜底（按 id 稳定哈希）。</summary>
    public static string TempName(string equipId)
    {
        if (string.IsNullOrEmpty(equipId)) return Generic[0];
        string id = equipId.ToLowerInvariant();
        string[] pool = Generic;
        if (id.Contains("sword") || id.Contains("blade") || id.Contains("weapon") || id.Contains("剑"))
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
