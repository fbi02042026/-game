using UnityEngine;

/// <summary>
/// 装备弹窗/对比用中文显示（槽位、稀有度、属性名）。
/// </summary>
public static class EquipUiText
{
    public static string Slot(EquipSlotType slot)
    {
        switch (slot)
        {
            case EquipSlotType.Head: return "头部";
            case EquipSlotType.Chest: return "胸部";
            case EquipSlotType.Hands: return "手部";
            case EquipSlotType.Feet: return "脚部";
            case EquipSlotType.Cape: return "披风";
            case EquipSlotType.MainHand: return "主手";
            case EquipSlotType.OffHand: return "副手";
            default: return "装备";
        }
    }

    public static string WeaponHand(WeaponHandSlot hand, WeaponType weaponType)
    {
        if (weaponType == WeaponType.TwoHand) return "双手";
        switch (hand)
        {
            case WeaponHandSlot.MainHand: return "主手";
            case WeaponHandSlot.OffHand: return "副手";
            default: return "武器";
        }
    }

    public static string WeaponHandBadge(EquipInstance eq)
    {
        if (eq == null || !WeaponLoadoutRules.IsLoadoutItem(eq)) return null;
        return "\u3010" + WeaponHand(eq.weaponHand, eq.weaponType) + "\u3011";
    }

    public static string EquipTitleWithHand(EquipInstance eq)
    {
        string badge = WeaponHandBadge(eq);
        string title = EquipTitle(eq);
        return string.IsNullOrEmpty(badge) ? title : badge + title;
    }

    public static string RarityName(Rarity r)
    {
        switch (r)
        {
            case global::Rarity.Uncommon: return "优秀";
            case global::Rarity.Rare: return "稀有";
            case global::Rarity.Epic: return "史诗";
            case global::Rarity.Legendary: return "传奇";
            default: return "普通";
        }
    }

    /// <summary>装备弹窗稀有度字色：普通白、稀有蓝、传奇金。</summary>
    public static Color RarityTextColor(Rarity r)
    {
        switch (r)
        {
            case global::Rarity.Rare:
                return new Color(0.35f, 0.62f, 1f, 1f);
            case global::Rarity.Legendary:
                return new Color(1f, 0.82f, 0.28f, 1f);
            default:
                return Color.white;
        }
    }

    public static string Attr(AttrType t)
    {
        switch (t)
        {
            case AttrType.Strength: return "力量";
            case AttrType.Intelligence: return "智力";
            case AttrType.Agility: return "敏捷";
            case AttrType.Vitality: return "体质";
            case AttrType.MaxHp: return "生命";
            case AttrType.Attack: return "攻击";
            case AttrType.AttackSpeed: return "攻速";
            case AttrType.CritRate: return "暴击";
            case AttrType.CritDamage: return "暴击伤害";
            case AttrType.MoveSpeed: return "移速";
            case AttrType.AttackRange: return "射程";
            case AttrType.Defense: return "防御";
            // 2026-09-29：物攻/魔攻、物防/魔防拆开后必须给中文名，
            // 否则装备面板会直接把枚举名 "MagicAttack" / "MagicDefense" 显示给玩家。
            case AttrType.MagicAttack: return "魔攻";
            case AttrType.MagicDefense: return "魔防";
            case AttrType.LifeSteal: return "吸血";
            case AttrType.Dodge: return "闪避";
            case AttrType.FireDamage: return "火焰伤害";
            case AttrType.IceDamage: return "冰霜伤害";
            case AttrType.ExpBonus: return "经验加成";
            case AttrType.GoldBonus: return "金币加成";
            case AttrType.CooldownReduce: return "冷却缩减";
            case AttrType.MagicPower: return "魔法强度";
            case AttrType.PhyPower: return "物理强度";
            case AttrType.EliteDamage: return "精英伤害";
            // 2026-09-26 新增的中毒词缀也要给中文名，
            // 否则装备被动面板会把枚举名 "Poison" 直接显示给玩家。
            case AttrType.Poison: return "中毒";
            default: return t.ToString();
        }
    }

    /// <summary>
    /// 比例属性集合（2026-10-09 主人拍板）：
    /// 数据层对这些属性统一写成 <c>isPercent = false</c> + 小数值（0.05 = 5%），
    /// 但 UI 必须按百分比显示，否则 0.05 会被 0.# 四舍五入成「0.1」、还丢掉 % 号。
    /// 口径对齐 <see cref="RiftEquipGenerator"/> 的 IsRateAttr（CRIT_RATE / CRIT_DMG / ATK_SPD /
    /// DMG_RED / DODGE / LIFE_STEAL / ELE_DMG / HEAL / HP_REGEN / CONTROL_RES / ANTI_CRIT）。
    /// 攻速在 TryMapAttr 里被强制 isPercent = true（走 Value 的规则①），放进来只为口径一致。
    /// </summary>
    public static bool IsRateAttr(AttrType t)
    {
        switch (t)
        {
            case AttrType.CritRate:
            case AttrType.CritDamage:
            case AttrType.AttackSpeed:
            case AttrType.Dodge:
            case AttrType.LifeSteal:
            case AttrType.FireDamage:
            case AttrType.IceDamage:
            case AttrType.EliteDamage:
            case AttrType.Poison:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// 装备文案属性数值的统一渲染口径（2026-10-09 主人拍板：比例属性 isPercent=false + 小数值要按百分比显示）：
    /// ① isPercent = true → value * 100 + "%"（保持原行为）；
    /// ② isPercent = false 但属于比例属性集合 → value * 100 + "%"（本次新增，修 0.05 显示成「0.1」的缺陷）；
    /// ③ 其它 → value.ToString("0.#")（保持原行为）。
    /// 只改显示，绝不动任何 isPercent 赋值 / 模板 / csv / 战斗结算口径。
    /// </summary>
    public static string Value(AttrType t, float value, bool isPercent)
    {
        if (isPercent || IsRateAttr(t)) return (value * 100f).ToString("0.#") + "%";
        return value.ToString("0.#");
    }

    /// <summary>装备标题：名称 + 强化等级。</summary>
    public static string EquipTitle(EquipInstance eq)
    {
        if (eq == null) return "装备";
        string name = !string.IsNullOrEmpty(eq.equipName) ? eq.equipName : (eq.templateId ?? "装备");
        if (eq.enhanceLevel > 0)
            return $"{name} +{eq.enhanceLevel}";
        return name;
    }
}
