using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 武器前缀命名 + 按 Kind 的词条池（对齐《武器系统设计》）。
/// </summary>
public static class WeaponAffixSystem
{
    struct Prefix
    {
        public string Name;
        public AttrType Attr;
        public WeaponCombatTable.WeaponKind[] Kinds;
    }

    static readonly Prefix[] Prefixes =
    {
        new Prefix { Name = "裂隙之", Attr = AttrType.Attack, Kinds = new[] { WeaponCombatTable.WeaponKind.Sword, WeaponCombatTable.WeaponKind.Greatsword, WeaponCombatTable.WeaponKind.Polearm } },
        new Prefix { Name = "疾风之", Attr = AttrType.AttackSpeed, Kinds = new[] { WeaponCombatTable.WeaponKind.Bow, WeaponCombatTable.WeaponKind.Sword } },
        new Prefix { Name = "鹰眼之", Attr = AttrType.CritRate, Kinds = new[] { WeaponCombatTable.WeaponKind.Bow } },
        new Prefix { Name = "碎骨之", Attr = AttrType.PhyPower, Kinds = new[] { WeaponCombatTable.WeaponKind.Greatsword, WeaponCombatTable.WeaponKind.Sword } },
        new Prefix { Name = "秘法之", Attr = AttrType.MagicPower, Kinds = new[] { WeaponCombatTable.WeaponKind.Staff } },
        new Prefix { Name = "炽焰之", Attr = AttrType.FireDamage, Kinds = new[] { WeaponCombatTable.WeaponKind.Staff, WeaponCombatTable.WeaponKind.Bow } },
        new Prefix { Name = "霜冻之", Attr = AttrType.IceDamage, Kinds = new[] { WeaponCombatTable.WeaponKind.Staff, WeaponCombatTable.WeaponKind.Bow } },
        new Prefix { Name = "腐蚀之", Attr = AttrType.LifeSteal, Kinds = new[] { WeaponCombatTable.WeaponKind.Sword } },
        new Prefix { Name = "磐石之", Attr = AttrType.Defense, Kinds = new[] { WeaponCombatTable.WeaponKind.Shield } },
        new Prefix { Name = "铁壁之", Attr = AttrType.MaxHp, Kinds = new[] { WeaponCombatTable.WeaponKind.Shield } },
    };

    public static List<AttrType> GetRollableAttrs(WeaponCombatTable.WeaponKind kind)
    {
        var list = new List<AttrType>();
        switch (kind)
        {
            case WeaponCombatTable.WeaponKind.Bow:
                list.AddRange(new[] { AttrType.Attack, AttrType.AttackSpeed, AttrType.CritRate, AttrType.AttackRange, AttrType.PhyPower });
                break;
            case WeaponCombatTable.WeaponKind.Staff:
                list.AddRange(new[] { AttrType.MagicPower, AttrType.FireDamage, AttrType.IceDamage, AttrType.CooldownReduce, AttrType.AttackSpeed });
                break;
            case WeaponCombatTable.WeaponKind.Shield:
                list.AddRange(new[] { AttrType.Defense, AttrType.MaxHp, AttrType.Dodge, AttrType.LifeSteal });
                break;
            case WeaponCombatTable.WeaponKind.Polearm:
                list.AddRange(new[] { AttrType.Attack, AttrType.CritRate, AttrType.PhyPower, AttrType.AttackSpeed, AttrType.AttackRange });
                break;
            case WeaponCombatTable.WeaponKind.Greatsword:
                list.AddRange(new[] { AttrType.Attack, AttrType.PhyPower, AttrType.CritRate, AttrType.LifeSteal });
                break;
            default: // Sword / 斧锤归入
                list.AddRange(new[] { AttrType.Attack, AttrType.AttackSpeed, AttrType.CritRate, AttrType.PhyPower, AttrType.LifeSteal });
                break;
        }
        return list;
    }

    /// <summary>稀有度 → 随机词条数（对齐设计：普通0 / 稀有1 / 史诗1 / 传奇2）。</summary>
    public static int AffixCountForRarity(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Legendary: return 2;
            case Rarity.Epic:
            case Rarity.Rare: return 1;
            default: return 0; // Common / Uncommon
        }
    }

    /// <summary>史诗词条数值相对稀有上浮。</summary>
    public static float AffixValueMul(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Legendary: return 1.35f;
            case Rarity.Epic: return 1.4f;
            case Rarity.Rare: return 1f;
            default: return 0.85f;
        }
    }

    public static string RaritySuffix(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Rare: return "·精良";
            case Rarity.Epic: return "·卓越";
            case Rarity.Legendary: return "·传说";
            default: return "";
        }
    }

    public static string BaseTypeName(WeaponCombatTable.WeaponKind kind)
    {
        switch (kind)
        {
            case WeaponCombatTable.WeaponKind.Bow: return "长弓";
            case WeaponCombatTable.WeaponKind.Staff: return "法杖";
            case WeaponCombatTable.WeaponKind.Polearm: return "长矛";
            case WeaponCombatTable.WeaponKind.Greatsword: return "双手斧";
            case WeaponCombatTable.WeaponKind.Shield: return "圆盾";
            default: return "短剑";
        }
    }

    static Prefix PickPrefix(WeaponCombatTable.WeaponKind kind, AttrType preferAttr)
    {
        var matched = new List<Prefix>();
        for (int i = 0; i < Prefixes.Length; i++)
        {
            var p = Prefixes[i];
            if (p.Kinds == null) continue;
            bool ok = false;
            for (int k = 0; k < p.Kinds.Length; k++)
            {
                if (p.Kinds[k] == kind) { ok = true; break; }
            }
            if (!ok) continue;
            if (p.Attr == preferAttr) return p;
            matched.Add(p);
        }
        if (matched.Count > 0)
            return matched[Random.Range(0, matched.Count)];
        return new Prefix { Name = "裂隙之", Attr = AttrType.Attack, Kinds = null };
    }

    public static string BuildName(WeaponCombatTable.WeaponKind kind, AttrType primaryAttr, Rarity rarity, string templateBaseName)
    {
        var prefix = PickPrefix(kind, primaryAttr);
        string baseName = templateBaseName;
        if (string.IsNullOrEmpty(baseName) || !HasChinese(baseName) || baseName.Length <= 2)
            baseName = BaseTypeName(kind);
        // 已带前缀的模板名不再叠前缀（如「暮火之杖」）
        if (HasChinese(templateBaseName) && templateBaseName.Contains("之"))
            return templateBaseName + RaritySuffix(rarity);
        return prefix.Name + baseName + RaritySuffix(rarity);
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

    /// <summary>为主手/副手武器追加稀有度词条并重命名。</summary>
    public static void ApplyWeaponRoll(EquipInstance inst)
    {
        if (inst == null || inst.template == null) return;
        if (inst.slotType != EquipSlotType.MainHand && inst.slotType != EquipSlotType.OffHand)
            return;
        if (SpecialWeapons.IsTwilightStaff(inst))
        {
            inst.equipName = SpecialWeapons.DisplayName;
            return;
        }

        var kind = WeaponCombatTable.ResolveKind(inst.template);
        var pool = GetRollableAttrs(kind);
        int count = AffixCountForRarity(inst.rarity);
        float mul = AffixValueMul(inst.rarity);
        AttrType primary = pool.Count > 0 ? pool[0] : AttrType.Attack;

        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int idx = Random.Range(0, pool.Count);
            AttrType attr = pool[idx];
            pool.RemoveAt(idx);
            if (i == 0) primary = attr;
            float value = RollAttrValue(attr, mul, inst);
            inst.attrBonus.Add(new AttrBonusData
            {
                attrType = attr,
                value = EquipRollCeiling.ClampAffixValue(inst, attr, value, IsPercentAttr(attr)),
                isPercent = IsPercentAttr(attr)
            });
        }

        string baseName = EquipNameGen.DisplayName(inst.template);
        inst.equipName = BuildName(kind, primary, inst.rarity, baseName);
    }

    static bool IsPercentAttr(AttrType attr)
    {
        switch (attr)
        {
            case AttrType.AttackSpeed:
            case AttrType.CritRate:
            case AttrType.Dodge:
            case AttrType.LifeSteal:
            case AttrType.PhyPower:
            case AttrType.MagicPower:
            case AttrType.CooldownReduce:
            case AttrType.ExpBonus:
            case AttrType.GoldBonus:
                return true;
            default:
                return false;
        }
    }

    static float RollAttrValue(AttrType attr, float mul, EquipInstance inst = null)
    {
        float v;
        switch (attr)
        {
            case AttrType.Attack: v = (2f + Random.Range(1f, 5f)) * mul; break;
            case AttrType.AttackSpeed: v = (0.04f + Random.Range(0.02f, 0.08f)) * mul; break;
            case AttrType.CritRate: v = (0.02f + Random.Range(0.01f, 0.04f)) * mul; break;
            case AttrType.PhyPower: v = (0.05f + Random.Range(0.02f, 0.08f)) * mul; break;
            case AttrType.MagicPower: v = (0.05f + Random.Range(0.02f, 0.08f)) * mul; break;
            case AttrType.FireDamage:
            case AttrType.IceDamage: v = (3f + Random.Range(1f, 6f)) * mul; break;
            case AttrType.LifeSteal: v = (0.02f + Random.Range(0.01f, 0.03f)) * mul; break;
            case AttrType.Defense: v = (2f + Random.Range(1f, 4f)) * mul; break;
            case AttrType.MaxHp: v = (10f + Random.Range(5f, 20f)) * mul; break;
            case AttrType.AttackRange: v = (0.1f + Random.Range(0.05f, 0.2f)) * mul; break;
            case AttrType.CooldownReduce: v = (0.03f + Random.Range(0.01f, 0.05f)) * mul; break;
            case AttrType.Dodge: v = (0.02f + Random.Range(0.01f, 0.03f)) * mul; break;
            default: v = 1f * mul; break;
        }

        if (inst != null && inst.weaponHand == WeaponHandSlot.OffHand && attr == AttrType.Attack)
            v *= 0.65f;
        return v;
    }
}
