using UnityEngine;

/// <summary>
/// 唯一武器组规则：全局最多主手+副手各一件；入包即装备；替换旧件→强化石。
/// </summary>
public static class WeaponLoadoutRules
{
    public static bool IsLoadoutItem(EquipInstance equip)
    {
        if (equip == null) return false;
        if (equip.weaponType == WeaponType.TwoHand) return true;
        if (equip.weaponType != WeaponType.None) return true;
        return IsShield(equip);
    }

    public static bool IsShield(EquipInstance equip)
    {
        if (equip == null) return false;
        if (equip.weaponHand == WeaponHandSlot.OffHand && equip.weaponType == WeaponType.None)
            return true;
        if (equip.template != null)
        {
            string n = (equip.template.spumName ?? equip.template.iconFileName ?? "").ToLowerInvariant();
            if (n.Contains("shield")) return true;
        }
        return false;
    }

    public static bool IsOffHandWeapon(EquipInstance equip)
        => equip != null && equip.weaponType != WeaponType.None
           && equip.weaponType != WeaponType.TwoHand
           && equip.weaponHand == WeaponHandSlot.OffHand;

    public static bool IsMainHandWeapon(EquipInstance equip)
        => equip != null && equip.weaponType != WeaponType.None
           && equip.weaponType != WeaponType.TwoHand
           && (equip.weaponHand == WeaponHandSlot.MainHand || equip.weaponHand == WeaponHandSlot.None);

    public static bool ReplacesEntireLoadout(EquipInstance incoming)
        => incoming != null && incoming.weaponType == WeaponType.TwoHand;

    /// <summary>本次替换会清掉哪些装备槽（逻辑槽 MainHand/OffHand，再由 HandRig 映射到 SPUM）。</summary>
    public static void GetSlotsToReplace(EquipInstance incoming, out bool clearMain, out bool clearOff)
    {
        clearMain = false;
        clearOff = false;
        if (incoming == null) return;

        if (ReplacesEntireLoadout(incoming))
        {
            clearMain = true;
            clearOff = true;
            return;
        }

        if (incoming.weaponHand == WeaponHandSlot.OffHand || IsShield(incoming))
        {
            clearOff = true;
            return;
        }

        clearMain = true;
    }

    public static EquipSlotType ResolveLogicalSlot(EquipInstance equip)
    {
        if (equip == null) return EquipSlotType.MainHand;
        if (ReplacesEntireLoadout(equip)) return EquipSlotType.MainHand;
        if (equip.weaponHand == WeaponHandSlot.OffHand || IsShield(equip))
            return EquipSlotType.OffHand;
        return EquipSlotType.MainHand;
    }

    public static EquipSlotType ResolveWearSlot(EquipInstance equip, in HeroWeaponRig.HandRig rig)
    {
        if (!rig.IsValid)
            return ResolveLogicalSlot(equip);

        if (ReplacesEntireLoadout(equip))
            return rig.AttackSlot;

        if (equip.weaponHand == WeaponHandSlot.OffHand || IsShield(equip))
            return rig.SecondarySlot;

        return rig.AttackSlot;
    }

    public static WeaponHandSlot InferHandFromIcon(string iconFileName, WeaponType weaponType)
    {
        if (weaponType == WeaponType.None)
        {
            string lower = (iconFileName ?? "").ToLowerInvariant();
            if (lower.Contains("shield")) return WeaponHandSlot.OffHand;
            return WeaponHandSlot.None;
        }

        if (weaponType == WeaponType.TwoHand)
            return WeaponHandSlot.MainHand;

        string n = (iconFileName ?? "").ToLowerInvariant();
        // 副手剑/斧等（可按对照表扩展）
        if (n.Contains("offhand") || n.Contains("_oh_") || n.Contains("dual"))
            return WeaponHandSlot.OffHand;

        return WeaponHandSlot.MainHand;
    }

    public static int CalcDecomposeMats(EquipInstance equip)
    {
        if (equip == null) return 0;
        int baseVal = (int)equip.rarity * (1 + Mathf.Max(0, equip.star));
        return Mathf.Max(1, baseVal);
    }

    public static void GrantDecomposeMats(EquipInstance equip, bool save = false)
    {
        int mats = CalcDecomposeMats(equip);
        if (mats <= 0) return;
        ResourceWallet.Add(ResourceWallet.ResourceType.DecomposeMat, mats, save: save, notify: true);
    }
}
