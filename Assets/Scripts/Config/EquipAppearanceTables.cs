using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 装备外观资源映射：外观ID → SPUM 资源名；按部位抽外观。
/// </summary>
public static class EquipAppearanceTables
{
    public class Row
    {
        public string Id;
        public string ResourceType;
        public string DisplayName;
        public string Part;
        public string SpumName;
        public string[] EquipIds;
    }

    static readonly List<Row> _all = new List<Row>();
    static readonly Dictionary<string, Row> _byId = new Dictionary<string, Row>();
    static readonly Dictionary<string, List<Row>> _byPart = new Dictionary<string, List<Row>>();
    static bool _loaded;

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        string raw = GameTableStore.LoadText(ContentPaths.Data.EquipAppearanceMap);
        if (string.IsNullOrEmpty(raw))
        {
            Debug.LogWarning("[EquipAppearance] 未找到 equip_appearance_map 表");
            return;
        }
        var rows = GameTableCsv.ParseRows(raw);
        for (int i = 1; i < rows.Count; i++)
        {
            var c = rows[i];
            if (c.Length < 8) continue;
            var row = new Row
            {
                Id = c[0].Trim(),
                ResourceType = c[1].Trim(),
                DisplayName = c[2].Trim(),
                Part = c[3].Trim(),
                SpumName = c[7].Trim()
            };
            if (string.IsNullOrEmpty(row.Id) || row.Part.Contains("内衣")) continue;
            string ids = c[4].Trim();
            row.EquipIds = string.IsNullOrEmpty(ids)
                ? System.Array.Empty<string>()
                : ids.Split('|');
            _all.Add(row);
            _byId[row.Id] = row;
            string partKey = NormalizePart(row.Part);
            if (!_byPart.TryGetValue(partKey, out var list))
            {
                list = new List<Row>();
                _byPart[partKey] = list;
            }
            list.Add(row);
        }
    }

    public static void Reload()
    {
        _loaded = false;
        _all.Clear();
        _byId.Clear();
        _byPart.Clear();
        EnsureLoaded();
    }

    public static bool TryGet(string appearanceId, out Row row)
    {
        EnsureLoaded();
        row = null;
        if (string.IsNullOrEmpty(appearanceId)) return false;
        return _byId.TryGetValue(appearanceId, out row);
    }

    public static string ResolveSpumName(string appearanceId, string fallback = null)
    {
        if (TryGet(appearanceId, out var row) && !string.IsNullOrEmpty(row.SpumName))
            return row.SpumName;
        return fallback;
    }

    /// <summary>按装备槽抽一个外观 ID。</summary>
    public static string PickAppearanceId(EquipSlotType slot, bool isWeapon)
    {
        EnsureLoaded();
        string key = PartKeyFromSlot(slot, isWeapon);
        if (!_byPart.TryGetValue(key, out var list) || list.Count == 0)
        {
            // 双手/主手回退武器池
            if (isWeapon && _byPart.TryGetValue("weapon", out var w) && w.Count > 0)
                list = w;
            else
                return null;
        }
        return list[Random.Range(0, list.Count)].Id;
    }

    static string NormalizePart(string part)
    {
        if (string.IsNullOrEmpty(part)) return "";
        if (part.Contains("头")) return "head";
        if (part.Contains("胸")) return "chest";
        if (part.Contains("手") && !part.Contains("主") && !part.Contains("副")) return "hands";
        if (part.Contains("脚")) return "feet";
        if (part.Contains("副手")) return "offhand";
        if (part.Contains("主手") || part.Contains("双手") || part.Contains("武器")) return "weapon";
        return part.ToLowerInvariant();
    }

    static string PartKeyFromSlot(EquipSlotType slot, bool isWeapon)
    {
        if (isWeapon || slot == EquipSlotType.MainHand)
            return "weapon";
        if (slot == EquipSlotType.OffHand)
            return "offhand";
        switch (slot)
        {
            case EquipSlotType.Head: return "head";
            case EquipSlotType.Chest: return "chest";
            case EquipSlotType.Hands: return "hands";
            case EquipSlotType.Feet: return "feet";
            default: return "weapon";
        }
    }
}
