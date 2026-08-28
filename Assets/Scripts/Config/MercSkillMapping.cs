using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 佣兵 H001~H022 与主动/被动技能对应表。
/// </summary>
public static class MercSkillMapping
{
    public struct MapRow
    {
        public string HireId;
        public string MercName;
        public MercRosterDefs.MercRarity Rarity;
        public string Job;
        public string ActiveSkillId;
        public string PassiveSkillId;
    }

    static Dictionary<string, MapRow> _byHire;
    static Dictionary<string, MapRow> _byAsset;
    static List<MapRow> _all;
    static bool _loaded;

    public static void Reload()
    {
        _loaded = false;
        _byHire = null;
        _byAsset = null;
        _all = null;
        EnsureLoaded();
    }

    static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        _byHire = new Dictionary<string, MapRow>();
        _byAsset = new Dictionary<string, MapRow>();
        _all = new List<MapRow>();

        string raw = GameTableStore.LoadText(ContentPaths.Data.MercSkillMap);
        if (string.IsNullOrEmpty(raw))
        {
            Debug.LogWarning("[MercSkillMapping] 未找到 merc_skill_map 表");
            return;
        }

        string[] lines = raw.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        int ok = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("佣兵ID")) continue;

            string[] cols = line.Split(',');
            if (cols.Length < 9) continue;
            string hireId = cols[0].Trim();
            if (!hireId.StartsWith("H")) continue;

            var row = new MapRow
            {
                HireId = hireId,
                MercName = cols[1].Trim(),
                Rarity = ParseMercRarity(cols[2].Trim()),
                Job = cols[3].Trim(),
                ActiveSkillId = NormalizeSkillId(cols[4]),
                PassiveSkillId = NormalizeSkillId(cols[7])
            };
            _byHire[hireId] = row;
            _all.Add(row);
            ok++;

            if (MercRosterDefs.TryGetByHireId(hireId, out var def))
            {
                string asset = def.AssetId;
                if (!_byAsset.ContainsKey(asset))
                    _byAsset[asset] = row;
            }
        }
        Debug.Log($"[MercSkillMapping] 已加载 {ok} 条映射");
    }

    static string NormalizeSkillId(string s)
    {
        if (string.IsNullOrEmpty(s) || s == "—" || s == "-") return null;
        return s.Trim();
    }

    static MercRosterDefs.MercRarity ParseMercRarity(string s)
    {
        if (s.Contains("传奇")) return MercRosterDefs.MercRarity.Legendary;
        if (s.Contains("稀有")) return MercRosterDefs.MercRarity.Rare;
        return MercRosterDefs.MercRarity.Common;
    }

    public static bool TryGetByHireId(string hireId, out MapRow row)
    {
        EnsureLoaded();
        return _byHire.TryGetValue(hireId ?? "", out row);
    }

    public static bool TryGetByAssetId(string assetId, out MapRow row)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(assetId))
        {
            row = default;
            return false;
        }
        if (_byAsset.TryGetValue(assetId, out row))
            return true;

        // 同 asset 多条 H 记录时扫表
        for (int i = 0; i < _all.Count; i++)
        {
            if (!MercRosterDefs.TryGetByHireId(_all[i].HireId, out var d)) continue;
            if (d.AssetId == assetId)
            {
                row = _all[i];
                _byAsset[assetId] = row;
                return true;
            }
        }
        row = default;
        return false;
    }

    public static void GetDefaultSkills(string assetId, out string activeId, out string passiveId)
    {
        activeId = null;
        passiveId = null;
        if (TryGetByAssetId(assetId, out var row))
        {
            activeId = row.ActiveSkillId;
            passiveId = row.PassiveSkillId;
        }
    }

    public static MercRosterDefs.MercRarity StarToRarity(int star)
    {
        if (star >= 5) return MercRosterDefs.MercRarity.Legendary;
        if (star >= 3) return MercRosterDefs.MercRarity.Rare;
        return MercRosterDefs.MercRarity.Common;
    }

    public static int RarityToStar(MercRosterDefs.MercRarity rarity)
    {
        switch (rarity)
        {
            case MercRosterDefs.MercRarity.Legendary: return 5;
            case MercRosterDefs.MercRarity.Rare: return Random.Range(3, 5);
            default: return Random.Range(1, 3);
        }
    }

    /// <summary>按稀有度从花名册抽一个 hireId。</summary>
    public static bool TryPickHireId(MercRosterDefs.MercRarity rarity, out MapRow row)
    {
        EnsureLoaded();
        var pool = new List<MapRow>();
        for (int i = 0; i < _all.Count; i++)
        {
            if (_all[i].Rarity == rarity)
                pool.Add(_all[i]);
        }
        if (pool.Count == 0)
        {
            row = default;
            return false;
        }
        row = pool[Random.Range(0, pool.Count)];
        return true;
    }

    public static IReadOnlyList<MapRow> All
    {
        get { EnsureLoaded(); return _all; }
    }
}
