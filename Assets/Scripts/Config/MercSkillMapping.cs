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
            Debug.LogError("[MercSkillMapping] 战斗表加载失败: Resources/" + ContentPaths.Data.MercSkillMap
                + " （空或缺失），using defaults。");
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
        if (ok <= 0)
            Debug.LogError("[MercSkillMapping] 战斗表加载失败: Resources/" + ContentPaths.Data.MercSkillMap
                + " （解析 0 条），using defaults。");
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

    // 【2026-10-22 主人拍板 T13】RarityToStar 掷点已删除：佣兵获得时零星（招募实例 star=0），
    // 局外升星走 MercStarDefs/MercStarSystem；StarToRarity 保留（稀有度兜底解析还在用）。

    /// <summary>
    /// 按稀有度从花名册抽一个 hireId。
    /// 【2026-10-22 主人拍板 T9】满星降权：背包碎片攒够满星总消耗（普通90/稀有70/传奇10，
    /// MercStarDefs.FullStarFragTotal）的佣兵在本稀有度池内权重 ×0.2（5→1）—— 不退池、重掷照旧；
    /// 稀有度概率口径（酒馆 66.5/33/0.5）在调用方 RollRarityByBattleWeight，这里不碰。
    /// </summary>
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

        // 权重 = 5，满星碎片攒够 → ×0.2 = 1（整数权重，等价主人建议的 ×0.2）
        int baseWeight = 5;
        int downWeight = Mathf.Max(1, Mathf.RoundToInt(baseWeight * MercStarDefs.FullStarRecruitDownWeight));
        int total = 0;
        var weights = new int[pool.Count];
        for (int i = 0; i < pool.Count; i++)
        {
            weights[i] = MercStarSystem.IsFullStarFragReady(pool[i].HireId) ? downWeight : baseWeight;
            total += weights[i];
        }
        int roll = Random.Range(0, total);
        for (int i = 0; i < pool.Count; i++)
        {
            if (roll < weights[i])
            {
                row = pool[i];
                return true;
            }
            roll -= weights[i];
        }
        row = pool[pool.Count - 1];
        return true;
    }

    public static IReadOnlyList<MapRow> All
    {
        get { EnsureLoaded(); return _all; }
    }
}
