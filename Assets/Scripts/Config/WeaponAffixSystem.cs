using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 武器命名（稀有度前缀）+ 按 Kind 的词条池（对齐《武器系统设计》）。
///
/// 2026-10-11 主人拍板「名字不要太长」：删除旧的随机词缀前缀表（裂隙之/疾风之/秘能之…）。
/// 名字现在 = <see cref="RarityPrefix"/> + 模板名，最长 7 字。
/// 词缀前缀表达的「主属性」在属性面板里本来就写着，不算丢信息。
/// </summary>
public static class WeaponAffixSystem
{
    public static List<AttrType> GetRollableAttrs(WeaponCombatTable.WeaponKind kind)
    {
        var list = new List<AttrType>();
        switch (kind)
        {
            case WeaponCombatTable.WeaponKind.Bow:
                list.AddRange(new[] { AttrType.Attack, AttrType.AttackSpeed, AttrType.CritRate, AttrType.AttackRange, AttrType.PhyPower });
                break;
            case WeaponCombatTable.WeaponKind.Staff:
                // 2026-09-29：法杖池原先只有 MagicPower(%)，**没有任何一条固定值魔攻** →
                // 法师的词缀收益只有百分比，永远追不上物理职业的「Attack +N」。补 MagicAttack 作主词条。
                list.AddRange(new[] { AttrType.MagicAttack, AttrType.MagicPower, AttrType.FireDamage, AttrType.IceDamage, AttrType.CooldownReduce, AttrType.AttackSpeed });
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

    /// <summary>
    /// 2026-10-11 主人拍板：传奇 / 稀有装备要在**名字最前面**带稀有度前缀 ——
    /// 旧实现是名字末尾加后缀（「裂隙之黎明断钢·传说」），背包列表一截断就只看得到名字前半段，
    /// 玩家扫一眼分不出品质，所以主人要求「传奇的武器名字前要加个辨识度高点的名字，稀有的也类似」。
    ///
    /// 文案唯一真源 = <see cref="EquipUiText.RarityName"/>（稀有/史诗/传奇），
    /// 本函数只负责加「·」并决定哪几档要加 —— 别在这里抄第二份中文，改词只改 RarityName 一处。
    /// 普通(Common)与优秀(Uncommon)不加：这两档占了全库大多数，加了前缀反而把高品质淹没。
    /// </summary>
    public static string RarityPrefix(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Rare:
            case Rarity.Epic:
            case Rarity.Legendary:
                return EquipUiText.RarityName(rarity) + "·";
            default:
                return "";
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

    /// <summary>
    /// 2026-10-11 主人拍板「名字不要太长」：名字 = 稀有度前缀 + 模板名，**不再叠随机词缀前缀**。
    /// 旧写法是「稀有度前缀 + 词缀前缀(裂隙之/疾风之…) + 模板名」，实测 10 个字
    /// （「传奇·裂隙之黎明断钢」），背包卡片一行放不下。砍掉词缀层后最长 7 字（「传奇·黎明断钢」）。
    /// 词缀前缀表达的「主属性」信息属性面板本来就有，不属于丢失；
    /// 旧的 <c>PickPrefix</c> / <c>Prefixes</c> 表已整段删除，不留开关（主人要加回说一声即可）。
    /// </summary>
    public static string BuildName(WeaponCombatTable.WeaponKind kind, Rarity rarity, string templateBaseName)
    {
        string baseName = templateBaseName;
        if (string.IsNullOrEmpty(baseName) || !HasChinese(baseName) || baseName.Length <= 2)
            baseName = BaseTypeName(kind);
        return RarityPrefix(rarity) + baseName;
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
            // 2026-10-11 主人拍板：特殊武器也带稀有度前缀（「稀有·暮火之杖」），统一命名规则。
            inst.equipName = SpecialWeapons.InstanceName;
            return;
        }

        var kind = WeaponCombatTable.ResolveKind(inst.template);
        var pool = GetRollableAttrs(kind);
        int count = AffixCountForRarity(inst.rarity);
        float mul = AffixValueMul(inst.rarity);

        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int idx = Random.Range(0, pool.Count);
            AttrType attr = pool[idx];
            pool.RemoveAt(idx);
            float value = RollAttrValue(attr, mul, inst);
            inst.attrBonus.Add(new AttrBonusData
            {
                attrType = attr,
                value = EquipRollCeiling.ClampAffixValue(inst, attr, value, IsPercentAttr(attr)),
                isPercent = IsPercentAttr(attr)
            });
        }

        string baseName = EquipNameGen.DisplayName(inst.template);
        inst.equipName = BuildName(kind, inst.rarity, baseName);
    }

    /// <summary>
    /// 2026-10-10 主人拍板：PhyPower / MagicPower 从本表**移除**（即对它们返回 false = 加算）。
    /// 这两条是「加成量」（基础值恒为 0、从不登记），isPercent = true 会走 _attr[type] *= (1 + value) → 0 × 1.12 = 0，
    /// 武器的「物理强度 / 魔法强度」词缀在面板上显示 12%、实际生效 0。改加算后与派生口径一致，真正生效。
    /// </summary>
    static bool IsPercentAttr(AttrType attr)
    {
        switch (attr)
        {
            // 2026-10-21 主人拍板：CritRate/Dodge/LifeSteal/CooldownReduce 改加算（与掉落路径 TryMapAttr、消费端口径一致）。
            // 这四条消费端是扁平分数（暴率概率 / 闪避概率 / 吸血比例 / 冷却缩减比例），基础多为 0，
            // 旧口径 isPercent=true 走乘算 0×(1+v)=0 → 词条完全失效；CritRate 基础 0.05 也仅 +0.25% 却面板显示「+5%」。
            // 攻速(AttackSpeed)是唯一真·乘算（攻击间隔=1/AttackSpeed），保留。
            // ExpBonus/GoldBonus（修复 A，2026-10-21）：消费端均为加算口径（GoldBonus 见 BattleManager.State.cs:121；
            // ExpBonus 当前无消费点/无天赋节点，纯死字段），故从乘算分支移除，与消费端口径一致。
            case AttrType.AttackSpeed:
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
            // 2026-09-29：魔法攻击固定值，与 Attack 同档（法杖词缀池已开始产出）
            case AttrType.MagicAttack: v = (2f + Random.Range(1f, 5f)) * mul; break;
            case AttrType.AttackSpeed: v = (0.04f + Random.Range(0.02f, 0.08f)) * mul; break;
            case AttrType.CritRate: v = (0.02f + Random.Range(0.01f, 0.04f)) * mul; break;
            case AttrType.PhyPower: v = (0.05f + Random.Range(0.02f, 0.08f)) * mul; break;
            case AttrType.MagicPower: v = (0.05f + Random.Range(0.02f, 0.08f)) * mul; break;
            case AttrType.FireDamage:
            case AttrType.IceDamage: v = (3f + Random.Range(1f, 6f)) * mul; break;
            case AttrType.LifeSteal: v = (0.02f + Random.Range(0.01f, 0.03f)) * mul; break;
            case AttrType.Defense: v = (2f + Random.Range(1f, 4f)) * mul; break;
            case AttrType.MaxHp: v = (10f + Random.Range(5f, 20f)) * mul; break;
            // 【2026-10-20 主人拍板】射程改「身位」口径，与 equip_attr_ranges.csv RANGE 行同区间
            //（普通 0.15~0.3）：旧 0.1~0.3 偏小，且掉落路径旧口径 ÷100 后面板显示「射程+0」。
            case AttrType.AttackRange: v = (0.15f + Random.Range(0f, 0.2f)) * mul; break;
            case AttrType.CooldownReduce: v = (0.03f + Random.Range(0.01f, 0.05f)) * mul; break;
            case AttrType.Dodge: v = (0.02f + Random.Range(0.01f, 0.03f)) * mul; break;
            default: v = 1f * mul; break;
        }

        // 副手衰减同样适用于魔法攻击（副手法杖不该和主手同值）
        if (inst != null && inst.weaponHand == WeaponHandSlot.OffHand
            && (attr == AttrType.Attack || attr == AttrType.MagicAttack))
            v *= 0.65f;
        return v;
    }
}
