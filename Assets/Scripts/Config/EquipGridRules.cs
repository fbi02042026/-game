using UnityEngine;

/// <summary>
/// 按 EquipIcons 文件名推断槽位与默认占格（可在对照表覆盖）。
/// </summary>
public static class EquipGridRules
{
    public struct GridSpec
    {
        public EquipSlotType slot;
        public WeaponType weaponType;
        public WeaponHandSlot weaponHand;
        public WeaponAttackType attackType;
        public int width;
        public int height;
        public string displayName;
    }

    public static GridSpec Infer(string iconFileName)
    {
        var spec = new GridSpec
        {
            slot = EquipSlotType.Hands,
            weaponType = WeaponType.None,
            weaponHand = WeaponHandSlot.None,
            attackType = WeaponAttackType.Physical,
            width = 1,
            height = 1,
            displayName = iconFileName
        };
        if (string.IsNullOrEmpty(iconFileName)) return spec;

        string n = iconFileName;
        string lower = n.ToLowerInvariant();

        if (IsHelmet(n, lower))
        {
            spec.slot = EquipSlotType.Head;
            spec.width = 1;
            spec.height = 1;
            spec.displayName = TrimPrefix(n, "New_Helmet_", "Helmet_", "Normal_Helmet", "F_SR_Helmet");
            return spec;
        }

        if (IsChest(n, lower))
        {
            spec.slot = EquipSlotType.Chest;
            spec.width = 2;
            spec.height = 1;
            spec.displayName = TrimPrefix(n, "New_Armor_", "Armor_", "Normal_Armor");
            return spec;
        }

        if (IsPants(n, lower))
        {
            spec.slot = EquipSlotType.Feet;
            spec.width = 2;
            spec.height = 1;
            spec.displayName = TrimPrefix(n, "New_Pant_", "F_SR_Pants");
            return spec;
        }

        if (IsFeet(n, lower))
        {
            spec.slot = EquipSlotType.Feet;
            spec.width = 1;
            spec.height = 1;
            spec.displayName = TrimPrefix(n, "Foot_");
            return spec;
        }

        if (IsCape(n, lower))
        {
            spec.slot = EquipSlotType.Cape;
            spec.width = 1;
            spec.height = 1;
            spec.displayName = n;
            return spec;
        }

        if (IsHands(n, lower))
        {
            spec.slot = EquipSlotType.Hands;
            spec.width = 1;
            spec.height = 1;
            spec.displayName = TrimPrefix(n, "New_Cloth_", "Normal_Cloth", "Cloth_");
            return spec;
        }

        if (IsShield(n, lower))
        {
            spec.slot = EquipSlotType.OffHand;
            spec.weaponHand = WeaponHandSlot.OffHand;
            spec.width = 2;
            spec.height = 2;
            spec.displayName = TrimPrefix(n, "New_Shield_", "Shield_", "WoodShield", "SteelShield");
            return spec;
        }

        if (IsBow(n, lower))
        {
            spec.slot = EquipSlotType.MainHand;
            spec.weaponHand = WeaponHandSlot.MainHand;
            spec.weaponType = WeaponType.TwoHand;
            spec.width = 2;
            spec.height = 2;
            spec.displayName = TrimPrefix(n, "Bow_");
            return spec;
        }

        if (IsTwoHandWeapon(n, lower))
        {
            spec.slot = EquipSlotType.MainHand;
            spec.weaponHand = WeaponHandSlot.MainHand;
            spec.weaponType = WeaponType.TwoHand;
            spec.attackType = lower.Contains("spear") ? WeaponAttackType.Physical : spec.attackType;
            if (lower.Contains("staff") || lower.Contains("spear") || lower.Contains("hammer"))
                spec.attackType = lower.Contains("hammer") ? WeaponAttackType.Physical : spec.attackType;
            spec.width = 2;
            spec.height = 3;
            spec.displayName = n;
            return spec;
        }

        if (IsOneHandWeapon(n, lower))
        {
            spec.weaponType = WeaponType.OneHand;
            spec.weaponHand = WeaponLoadoutRules.InferHandFromIcon(n, spec.weaponType);
            spec.slot = spec.weaponHand == WeaponHandSlot.OffHand
                ? EquipSlotType.OffHand
                : EquipSlotType.MainHand;
            if (lower.Contains("staff") || lower.Contains("wand"))
                spec.attackType = WeaponAttackType.Magic;
            spec.width = 1;
            spec.height = 2;
            spec.displayName = TrimPrefix(n, "New_Weapon_", "New_weapon_", "Sword_", "Axe");
            return spec;
        }

        return spec;
    }

    public static string MakeTemplateId(string iconFileName)
    {
        if (string.IsNullOrEmpty(iconFileName)) return "equip_unknown";
        string safe = iconFileName.ToLowerInvariant()
            .Replace(" ", "_")
            .Replace("-", "_");
        return "equip_" + safe;
    }

    static bool IsHelmet(string n, string lower)
        => n.StartsWith("New_Helmet_") || n.StartsWith("Helmet_") || n.StartsWith("Normal_Helmet") || n == "F_SR_Helmet";

    static bool IsChest(string n, string lower)
        => n.StartsWith("New_Armor_") || n.StartsWith("Armor_") || n.StartsWith("Normal_Armor");

    static bool IsPants(string n, string lower)
        => n.StartsWith("New_Pant_") || n == "F_SR_Pants";

    static bool IsFeet(string n, string lower)
        => n.StartsWith("Foot_");

    static bool IsCape(string n, string lower)
        => (n.StartsWith("Cloth_") && !n.StartsWith("New_Cloth")) || n == "F_SR_Cloth";

    static bool IsHands(string n, string lower)
        => n.StartsWith("New_Cloth_") || n.StartsWith("Normal_Cloth");

    static bool IsShield(string n, string lower)
        => n.StartsWith("New_Shield_") || n.StartsWith("Shield_") || lower.Contains("shield");

    static bool IsBow(string n, string lower)
        => n.StartsWith("Bow_");

    static bool IsTwoHandWeapon(string n, string lower)
        => n.StartsWith("Spear_") || n == "Soon_Spear" || lower.Contains("axelong")
           || n == "F_SR_Hammer" || (n.StartsWith("New_Weapon_") && IsLikelyTwoHandNewWeapon(n));

    static bool IsOneHandWeapon(string n, string lower)
        => n.StartsWith("New_Weapon_") || n.StartsWith("New_weapon_") || n.StartsWith("Sword_")
           || n.StartsWith("Axe") || lower.StartsWith("axe");

    static bool IsLikelyTwoHandNewWeapon(string n)
    {
        // 法杖/长柄类编号（可按对照表改）
        if (n == "New_Weapon_06" || n == "New_Weapon_07" || n == "New_Weapon_18"
            || n == "New_Weapon_19" || n == "New_Weapon_20")
            return true;
        return false;
    }

    static string TrimPrefix(string name, params string[] prefixes)
    {
        for (int i = 0; i < prefixes.Length; i++)
        {
            if (name.StartsWith(prefixes[i]))
                return name.Substring(prefixes[i].Length);
        }
        return name;
    }
}
