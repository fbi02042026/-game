using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 防具/披风命名与词条池（对齐《防具与饰品系统设计》；戒指项链槽位未开时披风走饰品倾向）。
///
/// 2026-10-11 主人拍板「名字不要太长」：删除旧的随机词缀前缀表（铁壁之/厚甲之/流风之…）。
/// 名字现在 = <see cref="WeaponAffixSystem.RarityPrefix"/> + 模板名，最长 7 字。
/// </summary>
public static class ArmorAffixSystem
{
    public static List<AttrType> GetRollableAttrs(EquipSlotType slot)
    {
        var list = new List<AttrType>();
        switch (slot)
        {
            case EquipSlotType.Feet:
                list.AddRange(new[] { AttrType.MoveSpeed, AttrType.Dodge, AttrType.MaxHp, AttrType.Intelligence, AttrType.Strength });
                break;
            case EquipSlotType.Cape:
                list.AddRange(new[] { AttrType.MaxHp, AttrType.Defense, AttrType.CooldownReduce, AttrType.FireDamage, AttrType.IceDamage, AttrType.LifeSteal });
                break;
            // 2026-10-10 主人拍板：手套/鞋出「智力 / 力量」主属性（与掉落路径 RiftEquipTables 同口径）。
            // Hands 从 Head/Chest 分支里拆出来单独加；Head / Chest 维持原样（主人：头和胸主要给血和防御）。
            case EquipSlotType.Hands:
                list.AddRange(new[] { AttrType.Defense, AttrType.MaxHp, AttrType.Dodge, AttrType.LifeSteal, AttrType.Intelligence, AttrType.Strength });
                break;
            case EquipSlotType.Head:
            case EquipSlotType.Chest:
                list.AddRange(new[] { AttrType.Defense, AttrType.MaxHp, AttrType.Dodge, AttrType.LifeSteal });
                break;
            default:
                list.AddRange(new[] { AttrType.MaxHp, AttrType.Defense, AttrType.MoveSpeed, AttrType.Dodge });
                break;
        }
        return list;
    }

    static string BaseName(EquipSlotType slot)
    {
        switch (slot)
        {
            case EquipSlotType.Head: return "头盔";
            case EquipSlotType.Chest: return "胸甲";
            case EquipSlotType.Hands: return "护手";
            case EquipSlotType.Feet: return "皮靴";
            case EquipSlotType.Cape: return "披风";
            default: return "护甲";
        }
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

    /// <summary>
    /// 2026-10-10 主人拍板：模板件路径（EquipInstance.GenerateFromTemplate，初始装 / 引导件）原本**不按职业过滤**，
    /// 掉落路径是词条级自动过滤（RiftEquipGenerator → RiftEquipTables.IsAttrAllowedForJob 读 equip_attr_ranges 第 12 列）。
    /// 这里补上同一口径：智力只给法系、力量只给物理系。
    /// 判定复用 <see cref="PlayerJobBaseStats.IsMagicJob"/>（读职业表第 16 列 damageType），
    /// 🔴 不另起第三套「是否法系」—— 不用 AttrSystem.IsMagicJobNow()（那是写死 Mage/Priest 的版本）。
    /// </summary>
    static void FilterBaseAttrsByJob(List<AttrType> pool)
    {
        if (pool == null || pool.Count == 0) return;
        bool magicJob = PlayerJobBaseStats.IsMagicJob(PlayerJobDefs.GetSelected());
        for (int i = pool.Count - 1; i >= 0; i--)
        {
            if (pool[i] == AttrType.Intelligence && !magicJob) pool.RemoveAt(i);
            else if (pool[i] == AttrType.Strength && magicJob) pool.RemoveAt(i);
        }
    }

    public static void ApplyArmorRoll(EquipInstance inst)
    {
        if (inst == null) return;
        if (inst.slotType == EquipSlotType.MainHand || inst.slotType == EquipSlotType.OffHand)
            return;

        var pool = GetRollableAttrs(inst.slotType);
        FilterBaseAttrsByJob(pool);
        int count = WeaponAffixSystem.AffixCountForRarity(inst.rarity);
        float mul = WeaponAffixSystem.AffixValueMul(inst.rarity);

        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int idx = Random.Range(0, pool.Count);
            AttrType attr = pool[idx];
            pool.RemoveAt(idx);
            float value = RollValue(attr, mul);
            inst.attrBonus.Add(new AttrBonusData
            {
                attrType = attr,
                value = EquipRollCeiling.ClampAffixValue(inst, attr, value, IsPercent(attr)),
                isPercent = IsPercent(attr)
            });
        }

        string baseName = EquipNameGen.DisplayName(inst.template);
        if (!HasChinese(baseName) || baseName.Length <= 1)
            baseName = BaseName(inst.slotType);
        // 名字 = 稀有度前缀 + 模板名，与武器同一个出口（WeaponAffixSystem.RarityPrefix）。
        // 2026-10-11 主人拍板：① 名字前面要能一眼看出品质；②「名字不要太长」——
        // 旧的随机词缀前缀（铁壁之/厚甲之/流风之…）整层删掉，最长 7 字。
        inst.equipName = WeaponAffixSystem.RarityPrefix(inst.rarity) + baseName;
    }

    static bool IsPercent(AttrType attr)
    {
        switch (attr)
        {
            // 2026-10-21 主人拍板：Dodge/LifeSteal/CooldownReduce 改加算（与掉落路径、消费端口径一致）。
            // 旧 isPercent=true 乘算 → 基础 0 时 0×(1+v)=0 完全失效。MoveSpeed 是乘算（移速速率）保留；
            // PhyPower/MagicPower 不在防具词条池（见 GetRollableAttrs 注释），此处保持原样不碰。
            case AttrType.MoveSpeed:
            case AttrType.PhyPower:
            case AttrType.MagicPower:
                return true;
            default:
                return false;
        }
    }

    static float RollValue(AttrType attr, float mul)
    {
        switch (attr)
        {
            case AttrType.MaxHp: return (12f + Random.Range(5f, 18f)) * mul;
            case AttrType.Defense: return (2f + Random.Range(1f, 4f)) * mul;
            case AttrType.MoveSpeed: return (0.04f + Random.Range(0.02f, 0.06f)) * mul;
            case AttrType.Dodge: return (0.02f + Random.Range(0.01f, 0.03f)) * mul;
            case AttrType.LifeSteal: return (0.02f + Random.Range(0.01f, 0.03f)) * mul;
            case AttrType.CooldownReduce: return (0.03f + Random.Range(0.01f, 0.04f)) * mul;
            case AttrType.FireDamage:
            case AttrType.IceDamage: return (2f + Random.Range(1f, 4f)) * mul;
            // 2026-10-10 主人拍板：智力 / 力量 走副属性档（equip_attr_ranges 的 INTELLIGENCE / STRENGTH 普通档 2.5~6），
            // 与 DEF(3~6) 同量级，再乘稀有度 mul。🔴 不写 case 会走 default 只给 1 点。
            // ⚠️ 本函数只拿到 mul、拿不到 rarity，所以只能拿「普通档」当基线（与 MaxHp / Defense 同一写法），
            //    传奇件这里 ≈ 3.4~8.1，低于掉落路径的 28~44 —— 两条路径的绝对量级本就不是一套。
            case AttrType.Intelligence:
            case AttrType.Strength: return (2.5f + Random.Range(0f, 3.5f)) * mul;
            // 2026-10-10 警告：**不要把 PhyPower / MagicPower 加进防具词条池**（GetRollableAttrs）。
            // 本函数没有这两条 case，一旦进池就会落 default 的 1f * mul —— 那是 +100% 起跳的数值炸弹。
            // 真要加，必须先在这里写 case（与 WeaponAffixSystem.RollAttrValue 的 0.05f~0.13f 同档），再加进池子。
            case AttrType.PhyPower:
            case AttrType.MagicPower:
                Debug.LogError("[ArmorAffixSystem] PhyPower/MagicPower 落进防具 RollValue：本函数没有对应的数值档，"
                    + "只能走 default 的 1f * mul（+100%，数值炸弹）。先在 RollValue 补 case 再加进池子。");
                return 1f * mul;
            default: return 1f * mul;
        }
    }
}
