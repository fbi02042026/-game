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

    /// <summary>
    /// 2026-09-29：攻击力已拆成物理(<see cref="AttrType.Attack"/>) / 魔法(<see cref="AttrType.MagicAttack"/>)，
    /// 「固定值攻击条目」的判定必须两侧都认 —— 否则法师副手法杖吃不到副手上限、
    /// 装备攻击力汇总也少算一整条。
    /// </summary>
    static bool IsAttackFlat(AttrType attr, bool isPercent)
        => !isPercent && (attr == AttrType.Attack || attr == AttrType.MagicAttack);

    static void AppendEquipBonuses(List<AttrBonusData> list, EquipInstance equip, bool capOffAttack, float mainAttack)
    {
        if (equip == null) return;

        // 2026-10-09 主人拍板：装备上就在局内生效，出战斗清零。
        // 把模板 globalBonus 并入本局战斗内的属性汇总。globalBonus 是模板固定值，
        // 不乘 EquipEnhanceSystem.GetMultiplier(equip) 强化倍率（否则数值失控），
        // 副手封顶与 attrBonus / enchants 保持一致。
        // 放在最开头：某件装备可能无 attrBonus 却持有 globalBonus，不能被下方
        // 「attrBonus == null」的提前 return 一起跳过。
        if (equip.globalBonus != null)
        {
            var g = equip.globalBonus;
            float v = g.value;
            if (capOffAttack && IsAttackFlat(g.attrType, g.isPercent))
                v = CapOffHandAttack(v, mainAttack);
            list.Add(new AttrBonusData
            {
                attrType = g.attrType,
                value = v,
                isPercent = g.isPercent
            });
        }

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
            if (capOffAttack && IsAttackFlat(b.attrType, b.isPercent))
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
            if (capOffAttack && IsAttackFlat(enchant.attrType, enchant.isPercent))
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
            if (b == null || !IsAttackFlat(b.attrType, b.isPercent)) continue;
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
