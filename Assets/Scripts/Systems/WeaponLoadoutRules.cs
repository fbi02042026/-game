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
        if (IsShield(equip)) return true;
        // 职业发装写了 Kind override，但模板漏标 weaponType=None 时仍算武器组
        if (equip.weaponKindOverride >= 0
            && equip.weaponKindOverride <= (int)WeaponCombatTable.WeaponKind.Shield)
            return true;
        return false;
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
        // 【2026-10-07 主人拍板】防具 / 饰品等非武器组：**按它自己的穿戴槽**走，不许兜底成武器槽。
        // 反例（改前）：鞋子 slotType=Feet、weaponType=None，一路落不到任何分支，末尾 return MainHand
        // → 被判进主手槽，顺着 EquipItem 顶掉并 ScrapEquip **分解了玩家的主武器**（主人报「主武器没了」）。
        if (!IsLoadoutItem(equip)) return equip.slotType;
        if (ReplacesEntireLoadout(equip)) return EquipSlotType.MainHand;
        if (equip.weaponHand == WeaponHandSlot.OffHand || IsShield(equip))
            return EquipSlotType.OffHand;
        return EquipSlotType.MainHand;
    }

    public static EquipSlotType ResolveWearSlot(EquipInstance equip, in HeroWeaponRig.HandRig rig)
    {
        // 同上（唯一出口改一处，四个调用方一起受益）：非武器组不参与 Attack / Secondary 映射，
        // 返回自身槽位 —— fail closed，不兜底成攻击槽。
        if (equip != null && !IsLoadoutItem(equip)) return equip.slotType;

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

    /// <summary>
    /// 拆分/替换旧装备的产出（唯一出口）。【2026-10-18 主人拍板：拆分旧的武器先换成局内币】——
    /// 不再发分解材料（DecomposeMat），折算金额统一走 GameConfig.EquipScrapGold（装备折金唯一口径，
    /// 与宝箱折算/穿装失败折算/丢弃折算同价）。战斗内发抽奖币（GrantRunGold，结算「抽奖币」栏可见）；
    /// 战斗外（城镇拆分）发城镇金币钱包。返回实发金额（0 = 没发），调用方拿去拼提示文案。
    /// </summary>
    public static int GrantScrapGold(EquipInstance equip, bool save = false)
    {
        if (equip == null) return 0;
        int g = GameConfig.EquipScrapGold(equip.rarity, equip.star);
        if (g <= 0) return 0;
        var bm = BattleManager.Instance;
        if (bm != null)
        {
            bm.GrantRunGold(g);
        }
        else
        {
            ResourceWallet.Add(ResourceWallet.ResourceType.Gold, g, save: save, notify: true);
        }
        return g;
    }
}
