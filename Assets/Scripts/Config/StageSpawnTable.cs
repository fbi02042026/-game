using System.Collections.Generic;
using UnityEngine;

public static class StageSpawnTable
{
    public struct SpawnRule
    {
        public int gameChapter;
        public int stageIndex;
        public StageType stageType;
        public int monsterTotal;
        public int waveCountMin;
        public int waveCountMax;
        public float eliteScaleMul;
        public bool useFormulaForTotal;
    }

    struct RawRow
    {
        public int gameChapter;
        public int stageIndex;
        public bool gameWildcard;
        public bool stageWildcard;
        public StageType stageType;
        public int monsterTotal;
        public int waveCountMin;
        public int waveCountMax;
        public float eliteScaleMul;
        public bool useFormulaForTotal;
    }

    static readonly List<RawRow> _rows = new List<RawRow>();
    static bool _loaded;

    public static bool HasData => _loaded && _rows.Count > 0;

    public static void Reload()
    {
        _loaded = false;
        _rows.Clear();
        EnsureLoaded();
    }

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        string raw = GameTableStore.LoadText(ContentPaths.Data.StageSpawn);
        if (string.IsNullOrEmpty(raw)) return;

        var lines = GameTableCsv.ParseRows(raw);
        for (int i = 1; i < lines.Count; i++)
        {
            var c = lines[i];
            if (c.Length < 7) continue;

            var row = new RawRow
            {
                gameWildcard = GameTableCsv.IsWildcard(c[0]),
                stageWildcard = GameTableCsv.IsWildcard(c[1]),
                gameChapter = GameTableCsv.TryInt(c[0], out int gc) ? gc : 0,
                stageIndex = GameTableCsv.TryInt(c[1], out int si) ? si : 0,
                stageType = ParseStageType(c[2]),
                monsterTotal = GameTableCsv.TryInt(c[3], out int mt) ? mt : 0,
                waveCountMin = GameTableCsv.TryInt(c[4], out int wmin) ? wmin : 3,
                waveCountMax = GameTableCsv.TryInt(c[5], out int wmax) ? wmax : 6,
                eliteScaleMul = GameTableCsv.TryFloat(c[6], out float esm) ? esm : 1f,
                useFormulaForTotal = !GameTableCsv.TryInt(c[3], out _) || mt <= 0
            };
            _rows.Add(row);
        }
        Debug.Log($"[StageSpawn] 已加载 {_rows.Count} 条");
    }

    static StageType ParseStageType(string s)
    {
        if (string.IsNullOrEmpty(s)) return StageType.Normal;
        s = s.Trim();
        if (s.Equals("Elite", System.StringComparison.OrdinalIgnoreCase)) return StageType.Elite;
        if (s.Equals("Boss", System.StringComparison.OrdinalIgnoreCase)) return StageType.Boss;
        return StageType.Normal;
    }

    static int MatchScore(RawRow r, int gameChapter, int stageIndex, StageType stageType)
    {
        if (r.stageType != stageType) return -1;
        int score = 0;
        if (!r.gameWildcard && r.gameChapter != gameChapter) return -1;
        if (!r.stageWildcard && r.stageIndex != stageIndex) return -1;
        if (!r.gameWildcard) score += 4;
        if (!r.stageWildcard) score += 2;
        return score;
    }

    public static bool TryResolve(int gameChapter, int stageIndex, StageType stageType, out SpawnRule rule)
    {
        EnsureLoaded();
        rule = default;
        if (_rows.Count == 0) return false;

        RawRow? best = null;
        int bestScore = -1;
        for (int i = 0; i < _rows.Count; i++)
        {
            int score = MatchScore(_rows[i], gameChapter, stageIndex, stageType);
            if (score > bestScore)
            {
                bestScore = score;
                best = _rows[i];
            }
        }

        if (!best.HasValue || bestScore < 0) return false;

        var b = best.Value;
        rule = new SpawnRule
        {
            gameChapter = gameChapter,
            stageIndex = stageIndex,
            stageType = stageType,
            monsterTotal = b.monsterTotal,
            waveCountMin = b.waveCountMin,
            waveCountMax = b.waveCountMax,
            eliteScaleMul = b.eliteScaleMul,
            useFormulaForTotal = b.useFormulaForTotal
        };
        return true;
    }
}
