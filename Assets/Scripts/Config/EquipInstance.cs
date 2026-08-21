using System.Collections.Generic;
using UnityEngine;
using System;

/// <summary>
/// 装备实例：运行时掉落的装备都是实例，同模板可以生成不同星级/等级/词条/品质的装备，图标复用模板
/// 优先级：品质 > 星级 > 等级
/// </summary>
[Serializable]
public class EquipInstance
{
    public string templateId;
    [NonSerialized] public EquipTemplate template;

    public Rarity rarity;
    public int star;
    public int requireLevel;
    public List<AttrBonusData> attrBonus = new List<AttrBonusData>();
    public List<EnchantData> enchants = new List<EnchantData>();
    public int gridWidth;
    public int gridHeight;
    public string equipName;
    public Sprite icon;
    public AttrBonusData globalBonus;

    // === 槽位信息 ===
    public EquipSlotType slotType;
    public WeaponType weaponType;
    public WeaponAttackType weaponAttackType;
    public ArmorPrefix armorPrefix;

    /// <summary>
    /// 从模板生成装备实例
    /// </summary>
    public static EquipInstance GenerateFromTemplate(EquipTemplate template, int bonusStar = 0, int heroLevel = 1)
    {
        EquipInstance inst = new EquipInstance();
        template.ResolveIcon();
        inst.template = template;
        inst.templateId = template.templateId;
        inst.icon = template.icon;
        inst.gridWidth = template.gridWidth;
        inst.gridHeight = template.gridHeight;
        inst.equipName = template.equipName;
        inst.globalBonus = template.globalBonus;

        // 槽位信息
        inst.slotType = template.slotType;
        inst.weaponType = template.weaponType;
        inst.weaponAttackType = template.weaponAttackType;
        inst.armorPrefix = template.armorPrefix;

        // 品质和星级
        inst.rarity = template.baseRarity;
        int maxStar = (int)inst.rarity;
        inst.star = UnityEngine.Random.Range(0, Mathf.Min(3, maxStar)) + bonusStar;
        inst.star = Mathf.Clamp(inst.star, 0, maxStar);

        inst.requireLevel = Mathf.Max(1, heroLevel + UnityEngine.Random.Range(-3, 2));

        // 基础属性（按星级加成）
        float starMultiplier = 1 + inst.star * 0.1f;
        foreach (var baseAttr in template.baseAttr)
        {
            inst.attrBonus.Add(new AttrBonusData
            {
                attrType = baseAttr.attrType,
                value = baseAttr.value * starMultiplier,
                isPercent = baseAttr.isPercent
            });
        }

        // 防具前缀生成双属性
        if (IsArmorSlot(inst.slotType) && inst.armorPrefix != ArmorPrefix.None)
        {
            GeneratePrefixAttr(inst);
        }

        // 披风独立生成属性
        if (inst.slotType == EquipSlotType.Cape)
        {
            GenerateCapeAttr(inst);
        }

        // 额外随机属性（高星级）
        int extraAttrCount = inst.star >= 5 ? 2 : inst.star >= 3 ? 1 : 0;
        List<AttrType> possibleAttrs = GetRollableAttrs(inst.slotType);
        for (int i = 0; i < extraAttrCount && possibleAttrs.Count > 0; i++)
        {
            int idx = UnityEngine.Random.Range(0, possibleAttrs.Count);
            AttrType randomAttr = possibleAttrs[idx];
            possibleAttrs.RemoveAt(idx);
            float value = RandomAttrValue(randomAttr, inst.rarity);
            inst.attrBonus.Add(new AttrBonusData
            {
                attrType = randomAttr,
                value = value,
                isPercent = value < 1
            });
        }
        return inst;
    }

    /// <summary>
    /// 判断是否是防具槽位
    /// </summary>
    private static bool IsArmorSlot(EquipSlotType slot)
    {
        return slot == EquipSlotType.Head || slot == EquipSlotType.Chest ||
               slot == EquipSlotType.Hands || slot == EquipSlotType.Feet;
    }

    /// <summary>
    /// 防具前缀生成双属性
    /// </summary>
    private static void GeneratePrefixAttr(EquipInstance inst)
    {
        switch (inst.armorPrefix)
        {
            case ArmorPrefix.Berserk:
                inst.attrBonus.Add(new AttrBonusData { attrType = AttrType.Strength, value = 2 + (int)inst.rarity, isPercent = false });
                inst.attrBonus.Add(new AttrBonusData { attrType = AttrType.Attack, value = 0.1f + (int)inst.rarity * 0.02f, isPercent = true });
                break;
            case ArmorPrefix.Arcane:
                inst.attrBonus.Add(new AttrBonusData { attrType = AttrType.Intelligence, value = 2 + (int)inst.rarity, isPercent = false });
                inst.attrBonus.Add(new AttrBonusData { attrType = AttrType.MagicPower, value = 0.1f + (int)inst.rarity * 0.02f, isPercent = true });
                break;
            case ArmorPrefix.Holy:
                inst.attrBonus.Add(new AttrBonusData { attrType = AttrType.Vitality, value = 2 + (int)inst.rarity, isPercent = false });
                inst.attrBonus.Add(new AttrBonusData { attrType = AttrType.MaxHp, value = 0.1f + (int)inst.rarity * 0.03f, isPercent = true });
                break;
            case ArmorPrefix.Steadfast:
                inst.attrBonus.Add(new AttrBonusData { attrType = AttrType.Vitality, value = 2 + (int)inst.rarity, isPercent = false });
                inst.attrBonus.Add(new AttrBonusData { attrType = AttrType.Defense, value = 0.1f + (int)inst.rarity * 0.03f, isPercent = true });
                break;
            case ArmorPrefix.Sage:
                inst.attrBonus.Add(new AttrBonusData { attrType = AttrType.Intelligence, value = 2 + (int)inst.rarity, isPercent = false });
                inst.attrBonus.Add(new AttrBonusData { attrType = AttrType.CooldownReduce, value = 0.05f + (int)inst.rarity * 0.01f, isPercent = true });
                break;
            case ArmorPrefix.Swift:
                inst.attrBonus.Add(new AttrBonusData { attrType = AttrType.Agility, value = 2 + (int)inst.rarity, isPercent = false });
                inst.attrBonus.Add(new AttrBonusData { attrType = AttrType.Dodge, value = 0.03f + (int)inst.rarity * 0.01f, isPercent = true });
                break;
        }
    }

    /// <summary>
    /// 披风独立生成属性
    /// </summary>
    private static void GenerateCapeAttr(EquipInstance inst)
    {
        // 披风随机给一个基础属性加成
        AttrType[] capeAttrs = { AttrType.Strength, AttrType.Intelligence, AttrType.Agility, AttrType.Vitality };
        AttrType randomAttr = capeAttrs[UnityEngine.Random.Range(0, capeAttrs.Length)];
        inst.attrBonus.Add(new AttrBonusData
        {
            attrType = randomAttr,
            value = 1 + (int)inst.rarity * 0.5f,
            isPercent = false
        });
    }

    /// <summary>
    /// 获取可随机到的属性列表（按槽位过滤）
    /// </summary>
    private static List<AttrType> GetRollableAttrs(EquipSlotType slot)
    {
        List<AttrType> attrs = new List<AttrType>();
        if (slot == EquipSlotType.MainHand || slot == EquipSlotType.OffHand)
        {
            // 武器可随机到的属性
            attrs.AddRange(new[] { AttrType.Attack, AttrType.AttackSpeed, AttrType.CritRate,
                AttrType.FireDamage, AttrType.IceDamage, AttrType.LifeSteal,
                AttrType.PhyPower, AttrType.MagicPower });
        }
        else
        {
            // 防具可随机到的属性
            attrs.AddRange(new[] { AttrType.MaxHp, AttrType.Defense, AttrType.MoveSpeed,
                AttrType.Dodge, AttrType.LifeSteal, AttrType.CooldownReduce,
                AttrType.ExpBonus, AttrType.GoldBonus });
        }
        return attrs;
    }

    private static float RandomAttrValue(AttrType attr, Rarity rarity)
    {
        float rarityMultiplier = (int)rarity * 0.5f;
        switch (attr)
        {
            case AttrType.Attack: return 2 + UnityEngine.Random.Range(1, 5) * rarityMultiplier;
            case AttrType.MaxHp: return 10 + UnityEngine.Random.Range(5, 20) * rarityMultiplier;
            case AttrType.AttackSpeed: return 0.05f + UnityEngine.Random.Range(0.02f, 0.1f) * rarityMultiplier;
            case AttrType.CritRate: return 0.02f + UnityEngine.Random.Range(0.01f, 0.05f) * rarityMultiplier;
            case AttrType.MoveSpeed: return 0.1f + UnityEngine.Random.Range(0.05f, 0.2f) * rarityMultiplier;
            case AttrType.Defense: return 2 + UnityEngine.Random.Range(1, 4) * rarityMultiplier;
            case AttrType.LifeSteal: return 0.03f + UnityEngine.Random.Range(0.01f, 0.03f) * rarityMultiplier;
            case AttrType.Dodge: return 0.02f + UnityEngine.Random.Range(0.01f, 0.04f) * rarityMultiplier;
            case AttrType.FireDamage: return 3 + UnityEngine.Random.Range(1, 6) * rarityMultiplier;
            case AttrType.IceDamage: return 3 + UnityEngine.Random.Range(1, 6) * rarityMultiplier;
            case AttrType.CooldownReduce: return 0.05f + UnityEngine.Random.Range(0.02f, 0.08f) * rarityMultiplier;
            case AttrType.PhyPower: return 0.05f + UnityEngine.Random.Range(0.02f, 0.1f) * rarityMultiplier;
            case AttrType.MagicPower: return 0.05f + UnityEngine.Random.Range(0.02f, 0.1f) * rarityMultiplier;
            case AttrType.ExpBonus: return 0.05f + UnityEngine.Random.Range(0.02f, 0.1f) * rarityMultiplier;
            case AttrType.GoldBonus: return 0.05f + UnityEngine.Random.Range(0.02f, 0.1f) * rarityMultiplier;
            default: return 1;
        }
    }

    public int GetSortWeight()
    {
        return (int)rarity * 100 + star * 10 + requireLevel;
    }
}