using UnityEngine;

/// <summary>
/// 六武器距离 / 攻速表：与 GameConfig 像素表对齐，供装备与怪物共用。
/// </summary>
public static class WeaponCombatTable
{
    public enum WeaponKind
    {
        Sword,
        Greatsword,
        Polearm,
        Staff,
        Bow,
        Shield
    }

    /// <summary>攻速（次/秒）基准，对齐数值表常见档位。</summary>
    public static float GetBaseAttackSpeed(WeaponKind kind)
    {
        switch (kind)
        {
            case WeaponKind.Bow: return 0.85f;
            case WeaponKind.Staff: return 0.9f;
            case WeaponKind.Polearm: return 0.75f;
            case WeaponKind.Greatsword: return 0.65f;
            case WeaponKind.Shield: return 0.8f;
            default: return 1.0f; // Sword
        }
    }

    public static float GetAttackRangeWorld(WeaponKind kind)
    {
        switch (kind)
        {
            case WeaponKind.Greatsword: return GameConfig.RangeGreatsword;
            case WeaponKind.Polearm: return GameConfig.RangePolearm;
            case WeaponKind.Staff: return GameConfig.RangeStaff;
            case WeaponKind.Bow: return GameConfig.RangeBow;
            case WeaponKind.Shield: return GameConfig.RangeShield;
            default: return GameConfig.RangeSword;
        }
    }

    public static WeaponKind ResolveKind(EquipTemplate tpl)
    {
        if (tpl == null) return WeaponKind.Sword;
        if (tpl.weaponKindOverride >= 0 && tpl.weaponKindOverride <= (int)WeaponKind.Shield)
            return (WeaponKind)tpl.weaponKindOverride;

        string hint = ((tpl.spumName ?? "") + " " + (tpl.equipName ?? "") + " " + (tpl.name ?? "") + " " + (tpl.templateId ?? "")).ToLowerInvariant();
        if (hint.Contains("bow") || hint.Contains("arrow") || hint.Contains("弓") || hint.Contains("弩"))
            return WeaponKind.Bow;
        if (hint.Contains("staff") || hint.Contains("wand") || hint.Contains("杖") || hint.Contains("魔杖") || hint.Contains("权杖"))
            return WeaponKind.Staff;
        if (hint.Contains("spear") || hint.Contains("pole") || hint.Contains("枪") || hint.Contains("矛") || hint.Contains("halberd") || hint.Contains("戟") || hint.Contains("lance"))
            return WeaponKind.Polearm;
        if (hint.Contains("shield") || hint.Contains("盾"))
            return WeaponKind.Shield;
        // 斧/锤：双手倾向大剑档位（慢、远一点），单手归剑
        if (hint.Contains("axe") || hint.Contains("斧") || hint.Contains("hammer") || hint.Contains("锤") || hint.Contains("槌"))
            return tpl.weaponType == WeaponType.TwoHand ? WeaponKind.Greatsword : WeaponKind.Sword;
        if (hint.Contains("great") || hint.Contains("大剑") || hint.Contains("twohand") || hint.Contains("双手"))
            return WeaponKind.Greatsword;
        if (tpl.weaponAttackType == WeaponAttackType.Magic)
            return WeaponKind.Staff;
        if (tpl.weaponType == WeaponType.TwoHand)
            return WeaponKind.Greatsword;
        return WeaponKind.Sword;
    }

    /// <summary>精英/Boss 期望 TTK 缩放：章节越高血量系数越高，便于调表。</summary>
    public static float EliteBossHpMul(int chapter, bool isBoss)
    {
        float ch = 1f + GameConfig.CHAPTER_SCALE_PER * Mathf.Max(0, chapter - 1);
        // Boss 额外抬高，目标 TTK 更长
        float role = isBoss ? GameConfig.BOSS_TTK_HP_MUL : GameConfig.ELITE_TTK_HP_MUL;
        return ch * role;
    }
}
