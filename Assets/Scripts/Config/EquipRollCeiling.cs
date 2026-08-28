using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 装备数值锚点：随机掉落不得超过同 category + 稀有度锚点上限。
/// 数据：Assets/Data/Source/Tables/equip_anchors.csv
/// </summary>
public static class EquipRollCeiling
{
    struct AnchorKey
    {
        public string Category;
        public Rarity Rarity;
    }

    class AnchorLimit
    {
        public Dictionary<AttrType, float> MaxFlat = new Dictionary<AttrType, float>();
        public Dictionary<AttrType, float> MaxPercent = new Dictionary<AttrType, float>();
    }

    static Dictionary<AnchorKey, AnchorLimit> _limits;
    static Dictionary<string, string> _templateCategory;
    static bool _loaded;

    public static string ResolveCategory(EquipTemplate tpl)
    {
        if (tpl == null) return "Unknown";
        if (tpl.weaponType == WeaponType.TwoHand)
        {
            if (tpl.weaponAttackType == WeaponAttackType.Magic) return "TwoHandStaff";
            return "TwoHandBow";
        }
        if (tpl.weaponHand == WeaponHandSlot.OffHand)
        {
            if (tpl.weaponType == WeaponType.None) return "Shield";
            return "OffHandWeapon";
        }
        if (tpl.slotType == EquipSlotType.MainHand) return "MainHandWeapon";
        if (tpl.slotType == EquipSlotType.OffHand) return "Shield";
        if (tpl.slotType == EquipSlotType.Head) return "Head";
        if (tpl.slotType == EquipSlotType.Chest) return "Chest";
        if (tpl.slotType == EquipSlotType.Hands) return "Hands";
        if (tpl.slotType == EquipSlotType.Feet) return "Feet";
        if (tpl.slotType == EquipSlotType.Cape) return "Cape";
        return tpl.slotType.ToString();
    }

    public static void Reload()
    {
        _loaded = false;
        _limits = null;
        _templateCategory = null;
        EnsureLoaded();
    }

    static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        _limits = new Dictionary<AnchorKey, AnchorLimit>();
        _templateCategory = new Dictionary<string, string>();

        string raw = GameTableStore.LoadText(ContentPaths.Data.EquipAnchors);
        if (string.IsNullOrEmpty(raw)) return;

        string[] lines = raw.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
            if (line.StartsWith("category", System.StringComparison.OrdinalIgnoreCase)) continue;

            string[] cols = line.Split(',');
            if (cols.Length < 7) continue;

            string category = cols[0].Trim();
            string templateId = cols[1].Trim();
            if (!System.Enum.TryParse(cols[2].Trim(), out Rarity rarity)) continue;
            if (!System.Enum.TryParse(cols[3].Trim(), out AttrType attr)) continue;
            if (!float.TryParse(cols[4].Trim(), out float value)) continue;
            bool isPercent = cols[5].Trim() == "1" || cols[5].Trim().Equals("true", System.StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(templateId))
                _templateCategory[templateId] = category;

            var key = new AnchorKey { Category = category, Rarity = rarity };
            if (!_limits.TryGetValue(key, out var limit))
            {
                limit = new AnchorLimit();
                _limits[key] = limit;
            }

            if (isPercent)
            {
                if (!limit.MaxPercent.TryGetValue(attr, out float cur) || value > cur)
                    limit.MaxPercent[attr] = value;
            }
            else
            {
                if (!limit.MaxFlat.TryGetValue(attr, out float cur) || value > cur)
                    limit.MaxFlat[attr] = value;
            }
        }
    }

    public static float ClampValue(EquipInstance inst, AttrType attr, float value, bool isPercent)
    {
        if (inst?.template == null) return value;
        EnsureLoaded();
        string category = ResolveCategory(inst.template);
        if (_templateCategory.TryGetValue(inst.templateId, out string mapped))
            category = mapped;

        var key = new AnchorKey { Category = category, Rarity = inst.rarity };
        if (!_limits.TryGetValue(key, out var limit))
            return value;

        if (isPercent)
        {
            if (limit.MaxPercent.TryGetValue(attr, out float max))
                return Mathf.Min(value, max);
        }
        else if (limit.MaxFlat.TryGetValue(attr, out float max))
        {
            return Mathf.Min(value, max);
        }

        return value;
    }

    public static void ClampInstanceBaseAttrs(EquipInstance inst)
    {
        if (inst?.attrBonus == null) return;
        for (int i = 0; i < inst.attrBonus.Count; i++)
        {
            var b = inst.attrBonus[i];
            if (b == null) continue;
            b.value = ClampValue(inst, b.attrType, b.value, b.isPercent);
        }
    }

    public static float ClampAffixValue(EquipInstance inst, AttrType attr, float value, bool isPercent)
        => ClampValue(inst, attr, value, isPercent);
}
