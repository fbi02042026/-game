using System.Collections.Generic;
using UnityEngine;

public static class WaveSlotTable
{
    public struct SlotRule
    {
        public int gameChapter;
        public int stageIndex;
        public StageType stageType;
        public int waveIndex;
        public int slotIndex;
        public int spriteIndex;
        public string styleFilter;
        public bool allowDuplicate;
    }

    struct RawRow
    {
        public int gameChapter;
        public bool gameWildcard;
        public int stageIndex;
        public bool stageWildcard;
        public StageType stageType;
        public bool stageTypeWildcard;
        public int waveIndex;
        public bool waveWildcard;
        public int slotIndex;
        public int spriteIndex;
        public string styleFilter;
        public bool allowDuplicate;
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

        string raw = GameTableStore.LoadText(ContentPaths.Data.WaveSlot);
        if (string.IsNullOrEmpty(raw)) return;

        var lines = GameTableCsv.ParseRows(raw);
        for (int i = 1; i < lines.Count; i++)
        {
            var c = lines[i];
            if (c.Length < 6) continue;

            _rows.Add(new RawRow
            {
                gameWildcard = GameTableCsv.IsWildcard(c[0]),
                gameChapter = GameTableCsv.TryInt(c[0], out int gc) ? gc : 0,
                stageWildcard = GameTableCsv.IsWildcard(c[1]),
                stageIndex = GameTableCsv.TryInt(c[1], out int si) ? si : 0,
                stageTypeWildcard = GameTableCsv.IsWildcard(c[2]),
                stageType = ParseStageType(c[2]),
                waveWildcard = GameTableCsv.IsWildcard(c[3]),
                waveIndex = GameTableCsv.TryInt(c[3], out int wi) ? wi : 0,
                slotIndex = GameTableCsv.TryInt(c[4], out int sl) ? sl : 0,
                spriteIndex = c.Length > 5 && GameTableCsv.TryInt(c[5], out int sp) ? sp : 0,
                styleFilter = c.Length > 6 ? c[6].Trim() : "Any",
                allowDuplicate = c.Length > 7 && GameTableCsv.TryBool(c[7], out bool ad) && ad
            });
        }
        Debug.Log($"[WaveSlot] 已加载 {_rows.Count} 条");
    }

    static StageType ParseStageType(string s)
    {
        if (GameTableCsv.IsWildcard(s)) return StageType.Normal;
        s = s.Trim();
        if (s.Equals("Elite", System.StringComparison.OrdinalIgnoreCase)) return StageType.Elite;
        if (s.Equals("Boss", System.StringComparison.OrdinalIgnoreCase)) return StageType.Boss;
        return StageType.Normal;
    }

    static int MatchScore(RawRow r, int gameChapter, int stageIndex, StageType stageType, int waveIndex, int slotIndex)
    {
        int score = 0;
        if (!r.gameWildcard && r.gameChapter != gameChapter) return -1;
        if (!r.gameWildcard) score += 32;
        if (!r.stageWildcard && r.stageIndex != stageIndex) return -1;
        if (!r.stageWildcard) score += 16;
        if (!r.stageTypeWildcard && r.stageType != stageType) return -1;
        if (!r.stageTypeWildcard) score += 8;
        if (!r.waveWildcard && r.waveIndex != waveIndex) return -1;
        if (!r.waveWildcard) score += 4;
        if (r.slotIndex != slotIndex) return -1;
        score += 2;
        return score;
    }

    public static bool TryGetSlot(int gameChapter, int stageIndex, StageType stageType,
        int waveIndex, int slotIndex, out SlotRule rule)
    {
        EnsureLoaded();
        rule = default;
        if (_rows.Count == 0) return false;

        RawRow? best = null;
        int bestScore = -1;
        for (int i = 0; i < _rows.Count; i++)
        {
            int score = MatchScore(_rows[i], gameChapter, stageIndex, stageType, waveIndex, slotIndex);
            if (score > bestScore)
            {
                bestScore = score;
                best = _rows[i];
            }
        }

        if (!best.HasValue || bestScore < 0) return false;

        var b = best.Value;
        rule = new SlotRule
        {
            gameChapter = gameChapter,
            stageIndex = stageIndex,
            stageType = stageType,
            waveIndex = waveIndex,
            slotIndex = slotIndex,
            spriteIndex = b.spriteIndex,
            styleFilter = string.IsNullOrEmpty(b.styleFilter) ? "Any" : b.styleFilter,
            allowDuplicate = b.allowDuplicate
        };
        return true;
    }
}
