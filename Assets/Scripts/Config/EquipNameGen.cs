using UnityEngine;

/// <summary>
/// 装备显示名兜底：模板已填中文名时优先用模板；否则按**真实贴图**判类别再取名。
///
/// 2026-09-27 主人要求「把武器和名字不符合的都找出来换个合适的名字」，根因就在本类的旧实现：
/// 旧 <c>TempName(equipId)</c> 只看 id 字符串 —— 只要 id 里含 "weapon" 就一律判成剑，
/// 于是 equip_new_weapon_02/04/08（斧图）、10/12/15（弓图）、13/14/16（枪图）、03（杖图）、
/// 05/11（匕首图）、07（钉锤图）全被起了「XX剑 / XX刀 / XX刃」的名字，图上却是别的东西。
///
/// 修法：类别一律由 spumName 解析出的**贴图所在目录**决定（与 HeroCostumeManager 的路径缓存同源），
/// id 只作最后兜底，且兜底不再硬塞剑名 —— 认不出就交回调用方用中性名（fail closed）。
/// 要新增类别，只改 <see cref="PoolForFolder"/> 与上面的名字池，别再按 id 猜。
/// </summary>
public static class EquipNameGen
{
    static readonly string[] Sword =
    {
        "见习短剑", "旅人佩剑", "青锋短刃", "守夜钢剑", "林间直剑"
    };
    static readonly string[] Axe =
    {
        "伐木手斧", "裂岩短斧", "蛮力战斧"
    };
    static readonly string[] Dagger =
    {
        "林间匕首", "霜脊短匕", "影袭匕首"
    };
    static readonly string[] Bow =
    {
        "猎手长弓", "赤焰猎弓", "暗潮短弓"
    };
    static readonly string[] Staff =
    {
        "青锋法杖", "元素法杖", "圣光权杖"
    };
    static readonly string[] Spear =
    {
        "守关长枪", "月影长枪", "岩心长枪"
    };
    static readonly string[] Hammer =
    {
        "碎岩战锤", "灰烬钉锤", "裂石重锤"
    };
    static readonly string[] Shield =
    {
        "铁壁圆盾", "木质轻盾", "钢制塔盾"
    };
    static readonly string[] Armor =
    {
        "粗布胸甲", "皮制护心", "铁片甲衣"
    };
    static readonly string[] Generic =
    {
        "旧物", "拾荒之物", "无名装备", "旅途遗物"
    };

    public static string DisplayName(EquipTemplate tpl)
    {
        if (tpl == null) return Generic[0];
        if (!string.IsNullOrEmpty(tpl.equipName) && HasChinese(tpl.equipName))
            return tpl.equipName;
        return TempName(tpl);
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
    /// 随机名（教程掉落等）。**必须传模板**：不传就只知道槽位，弓也会被叫成剑（2026-09-27 修）。
    /// </summary>
    public static string RandomWeaponName(EquipSlotType slot) => RandomWeaponName(null, slot);

    public static string RandomWeaponName(EquipTemplate tpl, EquipSlotType slot)
    {
        string[] pool = PoolForTemplate(tpl);
        if (pool == null)
            pool = IsArmorSlot(slot) ? Armor : Generic;
        return pool[Random.Range(0, pool.Length)];
    }

    /// <summary>兼容旧调用（只有 id）。</summary>
    public static string TempName(string equipId) => TempName(null, equipId);

    /// <summary>无中文模板名时的兜底：先按贴图类别取名，再按 id，最后中性名（不再一律判成剑）。</summary>
    public static string TempName(EquipTemplate tpl, string equipId = null)
    {
        string id = equipId ?? (tpl != null ? tpl.templateId : null);
        string[] pool = PoolForTemplate(tpl);
        if (pool == null) pool = PoolForId(id);
        if (pool == null) pool = Generic;

        if (string.IsNullOrEmpty(id)) return pool[0];
        int h = 0;
        for (int i = 0; i < id.Length; i++)
            h = unchecked(h * 31 + id[i]);
        if (h < 0) h = -h;
        return pool[h % pool.Length];
    }

    static bool IsArmorSlot(EquipSlotType slot)
    {
        return slot == EquipSlotType.Chest || slot == EquipSlotType.Head
               || slot == EquipSlotType.Hands || slot == EquipSlotType.Feet
               || slot == EquipSlotType.Cape;
    }

    /// <summary>按模板真身取名：防具 → 甲池；武器 → 由贴图目录决定。取不到返回 null（交给调用方，不猜）。</summary>
    static string[] PoolForTemplate(EquipTemplate tpl)
    {
        if (tpl == null) return null;
        if (IsArmorSlot(tpl.slotType)) return Armor;
        return PoolForFolder(SpumFolder(tpl.spumName));
    }

    /// <summary>spumName → 贴图所在目录（最后一级）。判名字类别的真源；缓存没建好就返回 null。</summary>
    static string SpumFolder(string spumName)
    {
        if (string.IsNullOrEmpty(spumName)) return null;
        var costume = HeroCostumeManager.Instance;
        string path;
        if (costume == null || !costume.TryResolveSpumPath(spumName, out path) || string.IsNullOrEmpty(path))
            return null;
        string p = path.Replace('\\', '/');
        int i = p.LastIndexOf('/');
        return i >= 0 ? p.Substring(i + 1) : null;
    }

    /// <summary>贴图目录 → 名字池。目录名是美术分类的真源（0_Sword 剑 / 2_Axe 斧 / 3_Bow 弓 …）。</summary>
    static string[] PoolForFolder(string folder)
    {
        switch ((folder ?? "").ToLowerInvariant())
        {
            case "0_sword": return Sword;
            case "6_dagger": return Dagger;
            case "1_axe":
            case "2_axe": return Axe;
            case "2_bow":
            case "3_bow": return Bow;
            case "5_wand": return Staff;
            case "6_hammer":
            case "8_mace": return Hammer;
            case "1_spear":
            case "4_spear": return Spear;
            case "3_shield":
            case "7_shield": return Shield;
        }
        return null;
    }

    /// <summary>只按 id 关键字判（最后兜底）。注意：泛称 "weapon" 不算剑 —— 那正是当年错名的源头。</summary>
    static string[] PoolForId(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        string s = id.ToLowerInvariant();
        if (s.Contains("axe")) return Axe;
        if (s.Contains("bow") || s.Contains("arrow")) return Bow;
        if (s.Contains("staff") || s.Contains("wand")) return Staff;
        if (s.Contains("spear") || s.Contains("pole")) return Spear;
        if (s.Contains("hammer") || s.Contains("mace")) return Hammer;
        if (s.Contains("shield")) return Shield;
        if (s.Contains("dagger")) return Dagger;
        if (s.Contains("sword") || s.Contains("blade")) return Sword;
        if (s.Contains("armor") || s.Contains("chest") || s.Contains("helm")) return Armor;
        return null;
    }
}
