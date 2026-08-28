using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 装备属性汇总：主手 + 副手（Attack 封顶）+ 护甲 + 装备技能被动。
/// 天赋仍由 AttrSystem 单独处理。
/// </summary>
public static class EquipStatRollup
{
    public const float OffHandAttackCapRatio = 0.85f;

    public static List<AttrBonusData> BuildBonusList(GridBackpackSystem bag)
    {
        var list = new List<AttrBonusData>();
        if (bag == null) return list;

        var main = bag.GetEquippedInLogicalSlot(EquipSlotType.MainHand);
        var off = bag.GetEquippedInLogicalSlot(EquipSlotType.OffHand);
        float mainAttack = SumEquipAttack(main);

        var seen = new HashSet<EquipInstance>();
        foreach (var equip in bag.GetEquippedItems())
        {
            if (equip == null || !seen.Add(equip)) continue;
            bool capOffAttack = equip == off && off != null && off != main;
            AppendEquipBonuses(list, equip, capOffAttack, mainAttack);
            AppendSkillPassives(list, equip);
        }

        return list;
    }

    static void AppendEquipBonuses(List<AttrBonusData> list, EquipInstance equip, bool capOffAttack, float mainAttack)
    {
        if (equip?.attrBonus == null) return;
        float enhanceMul = EquipEnhanceSystem.GetMultiplier(equip);
        int baseCount = Mathf.Clamp(equip.baseAttrCount, 0, equip.attrBonus.Count);

        for (int i = 0; i < equip.attrBonus.Count; i++)
        {
            var b = equip.attrBonus[i];
            if (b == null) continue;
            float v = b.value;
            if (i < baseCount && enhanceMul > 1.001f)
                v *= enhanceMul;
            if (capOffAttack && b.attrType == AttrType.Attack && !b.isPercent)
                v = CapOffHandAttack(v, mainAttack);
            list.Add(new AttrBonusData
            {
                attrType = b.attrType,
                value = v,
                isPercent = b.isPercent
            });
        }

        if (equip.enchants == null) return;
        for (int i = 0; i < equip.enchants.Count; i++)
        {
            var enchant = equip.enchants[i];
            if (enchant == null) continue;
            float v = enchant.value;
            if (capOffAttack && enchant.attrType == AttrType.Attack && !enchant.isPercent)
                v = CapOffHandAttack(v, mainAttack);
            list.Add(new AttrBonusData
            {
                attrType = enchant.attrType,
                value = v,
                isPercent = enchant.isPercent
            });
        }
    }

    static void AppendSkillPassives(List<AttrBonusData> list, EquipInstance equip)
    {
        if (equip?.skillPassives == null) return;
        for (int i = 0; i < equip.skillPassives.Count; i++)
        {
            var b = equip.skillPassives[i];
            if (b == null) continue;
            list.Add(new AttrBonusData
            {
                attrType = b.attrType,
                value = b.value,
                isPercent = b.isPercent
            });
        }
    }

    static float SumEquipAttack(EquipInstance equip)
    {
        if (equip?.attrBonus == null) return 0f;
        float enhanceMul = EquipEnhanceSystem.GetMultiplier(equip);
        int baseCount = Mathf.Clamp(equip.baseAttrCount, 0, equip.attrBonus.Count);
        float sum = 0f;
        for (int i = 0; i < equip.attrBonus.Count; i++)
        {
            var b = equip.attrBonus[i];
            if (b == null || b.attrType != AttrType.Attack || b.isPercent) continue;
            float v = b.value;
            if (i < baseCount && enhanceMul > 1.001f)
                v *= enhanceMul;
            sum += v;
        }
        return sum;
    }

    static float CapOffHandAttack(float offAttack, float mainAttack)
    {
        if (mainAttack <= 0.01f)
            return offAttack;
        float cap = mainAttack * OffHandAttackCapRatio;
        return Mathf.Min(offAttack, cap);
    }

    /// <summary>主手装备授予的技能 id（主手优先，其次副手）。</summary>
    public static string GetEquippedGrantSkillId(GridBackpackSystem bag)
    {
        if (bag == null) return null;
        var main = bag.GetEquippedInLogicalSlot(EquipSlotType.MainHand);
        if (!string.IsNullOrEmpty(main?.grantSkillId)) return main.grantSkillId;
        var off = bag.GetEquippedInLogicalSlot(EquipSlotType.OffHand);
        if (!string.IsNullOrEmpty(off?.grantSkillId)) return off.grantSkillId;
        return null;
    }
}
