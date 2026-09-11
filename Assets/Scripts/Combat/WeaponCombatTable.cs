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
        return AttackRangeTable.GetWeaponWorld(kind);
    }

    public static WeaponKind ResolveKind(EquipTemplate tpl)
    {
        return ResolveKind(tpl, tpl != null ? tpl.spumName : null, instanceOverride: -1);
    }

    public static WeaponKind ResolveKind(EquipInstance inst)
    {
        if (inst == null) return WeaponKind.Sword;
        return ResolveKind(inst.template, inst.ResolveSpumName(), inst.weaponKindOverride);
    }

    /// <summary>
    /// 解析武器逻辑类型。优先实例/模板 override；其次 SPUM 资源路径文件夹（如 3_Bow）；
    /// 再回退名称关键词。游侠起步弓 New_Weapon_12 显示名是「短刃」且无 bow 字样，必须靠路径。
    /// </summary>
    public static WeaponKind ResolveKind(EquipTemplate tpl, string spumNameHint, int instanceOverride = -1)
    {
        if (instanceOverride >= 0 && instanceOverride <= (int)WeaponKind.Shield)
            return (WeaponKind)instanceOverride;
        if (tpl == null) return WeaponKind.Sword;
        if (tpl.weaponKindOverride >= 0 && tpl.weaponKindOverride <= (int)WeaponKind.Shield)
            return (WeaponKind)tpl.weaponKindOverride;

        if (tpl.weaponAttackType == WeaponAttackType.Magic)
            return WeaponKind.Staff;

        string spum = !string.IsNullOrEmpty(spumNameHint) ? spumNameHint : tpl.spumName;
        if (TryKindFromSpum(spum, out var fromSpum))
            return fromSpum;

        string hint = ((spum ?? "") + " " + (tpl.equipName ?? "") + " " + (tpl.name ?? "") + " " + (tpl.templateId ?? "")).ToLowerInvariant();
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
        if (tpl.weaponType == WeaponType.TwoHand)
            return WeaponKind.Greatsword;
        return WeaponKind.Sword;
    }

    /// <summary>兼容旧调用：仅传模板 + spum 提示。</summary>
    public static WeaponKind ResolveKind(EquipTemplate tpl, string spumNameHint)
        => ResolveKind(tpl, spumNameHint, instanceOverride: -1);

    static bool TryKindFromSpum(string spumName, out WeaponKind kind)
    {
        kind = WeaponKind.Sword;
        if (string.IsNullOrEmpty(spumName)) return false;

        string path = null;
        var costume = HeroCostumeManager.Instance;
        if (costume != null && costume.TryResolveSpumPath(spumName, out path) && !string.IsNullOrEmpty(path))
        {
            if (TryKindFromResourcePath(path, out kind))
                return true;
        }

        // Costume 未就绪时：Ver300 Index 已知 New_Weapon_* 文件夹映射
        switch (spumName)
        {
            case "New_Weapon_10":
            case "New_Weapon_12":
                kind = WeaponKind.Bow;
                return true;
            case "New_Weapon_03":
                kind = WeaponKind.Staff;
                return true;
            case "New_Weapon_09":
                kind = WeaponKind.Polearm;
                return true;
            case "New_Weapon_01":
            case "New_Weapon_05":
            case "New_Weapon_06":
            case "New_Weapon_11":
                kind = WeaponKind.Sword;
                return true;
            case "New_Weapon_04":
            case "New_Weapon_08":
            case "New_Weapon_07":
                kind = WeaponKind.Sword; // 斧/锤单手按剑档
                return true;
            default:
                return false;
        }
    }

    static bool TryKindFromResourcePath(string path, out WeaponKind kind)
    {
        kind = WeaponKind.Sword;
        if (string.IsNullOrEmpty(path)) return false;
        string p = path.Replace('\\', '/').ToLowerInvariant();
        if (p.Contains("/3_bow/") || p.Contains("/2_bow/") || p.Contains("/bow/"))
        {
            kind = WeaponKind.Bow;
            return true;
        }
        if (p.Contains("/5_wand/") || p.Contains("/wand/") || p.Contains("/staff/"))
        {
            kind = WeaponKind.Staff;
            return true;
        }
        if (p.Contains("/1_spear/") || p.Contains("/spear/") || p.Contains("/polearm/"))
        {
            kind = WeaponKind.Polearm;
            return true;
        }
        if (p.Contains("/shield/") || p.Contains("shield"))
        {
            kind = WeaponKind.Shield;
            return true;
        }
        if (p.Contains("/0_sword/") || p.Contains("/6_dagger/") || p.Contains("/2_axe/") || p.Contains("/8_mace/"))
        {
            kind = WeaponKind.Sword;
            return true;
        }
        return false;
    }

    /// <summary>精英/Boss 期望 TTK 缩放：章节越高血量系数越高，便于调表。</summary>
    public static float EliteBossHpMul(int chapter, bool isBoss)
    {
        float ch = GameConfig.GetChapterStatScale(chapter);
        // Boss 额外抬高，目标 TTK 更长
        float role = isBoss ? GameConfig.BOSS_TTK_HP_MUL : GameConfig.ELITE_TTK_HP_MUL;
        return ch * role;
    }
}
