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
    /// 不再发分解材料（DecomposeMat）。战斗内发<b>抽奖币</b>（currentGold，战斗中用于抽奖的资源）；
    /// 战斗外（城镇拆分）发城镇金币钱包。返回实发金额（0 = 没发），调用方拿去拼提示文案、
    /// 并据此决定要不要弹折币提示（单一出口，不在别处再判一次）。
    /// 【2026-10-31 主人拍板「替换掉的道具转换成抽奖币 要整十数根据稀有度和部位来」】
    /// 金额改走 <see cref="GameConfig.EquipScrapDraftCoin"/>：整十数、按稀有度 + 部位
    /// （槽位解析走 <see cref="ResolveLogicalSlot"/> 唯一出口）。返回 0 = 没发。
    /// 【2026-11-08 主人拍板·两条口径】
    /// ① 折币改走 BattleManager.AddRunGold(g, false) 进<b>抽奖币池 currentGold</b>：
    ///    顶栏 DraftDisplay 与抽奖按钮置灰同步重算。旧实现走 GrantRunGold 进的是
    ///    「本局获得金币」GoldEarned（抽奖花不到），玩家报「折成抽奖币后就不能再抽」即此因。
    ///    countsAsEarned=false：抬高 _goldAtRunStart，局内循环资源、带不出本局（铁律 21）。
    /// ② 无限裂隙一律不折币：裂隙每层免费抽（GoldAllowed 恒 false）、抽满即收面板，
    ///    币在裂隙里没有消费出口 —— 发币 + 弹「折成抽奖币」只会误导玩家以为还能抽。
    ///    装备增减照常（调用方照旧 ScrapEquip / TryEquipDirect），只是不发钱、不弹折币提示。
    /// </summary>
    public static int GrantScrapGold(EquipInstance equip, bool save = false)
    {
        if (equip == null) return 0;
        var bm = BattleManager.Instance;
        // 裂隙不发抽奖币（口径 ②）；返回 0 由调用方抑制折币提示，装备移除照常。
        if (bm != null && bm.IsEndlessRift) return 0;
        int g = GameConfig.EquipScrapDraftCoin(equip.rarity, ResolveLogicalSlot(equip));
        if (g <= 0) return 0;
        if (bm != null)
        {
            bm.AddRunGold(g, false);
        }
        else
        {
            ResourceWallet.Add(ResourceWallet.ResourceType.Gold, g, save: save, notify: true);
        }
        return g;
    }
}
