using System.Collections.Generic;
using UnityEngine;

public static class BattleQuestTable
{
    public struct Row
    {
        public int gameChapter;
        public bool gameWildcard;
        public StageType stageType;
        public bool stageTypeWildcard;
        public bool isGoldDungeon;
        public bool goldDungeonWildcard;
        public string objective;
        public int clearGold;
        public int normalBase;
        public int normalChapterAdd;
        public float eliteGoldMul;
        public bool useFormula;
    }

    struct RawRow
    {
        public int gameChapter;
        public bool gameWildcard;
        public StageType stageType;
        public bool stageTypeWildcard;
        public int isGoldDungeon;
        public bool goldDungeonWildcard;
        public string objective;
        public int clearGold;
        public int normalBase;
        public int normalChapterAdd;
        public float eliteGoldMul;
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

        string raw = GameTableStore.LoadText(ContentPaths.Data.BattleQuest);
        if (string.IsNullOrEmpty(raw)) return;

        var lines = GameTableCsv.ParseRows(raw);
        for (int i = 1; i < lines.Count; i++)
        {
            var c = lines[i];
            if (c.Length < 5) continue;

            bool goldWild = GameTableCsv.IsWildcard(c[2]);
            int goldFlag = 0;
            if (!goldWild) GameTableCsv.TryInt(c[2], out goldFlag);

            var row = new RawRow
            {
                gameWildcard = GameTableCsv.IsWildcard(c[0]),
                gameChapter = GameTableCsv.TryInt(c[0], out int gc) ? gc : 0,
                stageTypeWildcard = GameTableCsv.IsWildcard(c[1]),
                stageType = ParseStageType(c[1]),
                goldDungeonWildcard = goldWild,
                isGoldDungeon = goldFlag,
                objective = c.Length > 3 ? c[3] : "",
                clearGold = c.Length > 4 && GameTableCsv.TryInt(c[4], out int cg) ? cg : 0,
                normalBase = c.Length > 5 && GameTableCsv.TryInt(c[5], out int nb) ? nb : 0,
                normalChapterAdd = c.Length > 6 && GameTableCsv.TryInt(c[6], out int nca) ? nca : 0,
                eliteGoldMul = c.Length > 7 && GameTableCsv.TryFloat(c[7], out float em) ? em : 0f
            };
            _rows.Add(row);
        }
        Debug.Log($"[BattleQuest] 已加载 {_rows.Count} 条");
    }

    static StageType ParseStageType(string s)
    {
        if (GameTableCsv.IsWildcard(s)) return StageType.Normal;
        s = s.Trim();
        if (s.Equals("Elite", System.StringComparison.OrdinalIgnoreCase)) return StageType.Elite;
        if (s.Equals("Boss", System.StringComparison.OrdinalIgnoreCase)) return StageType.Boss;
        if (s.Equals("Rest", System.StringComparison.OrdinalIgnoreCase)) return StageType.Rest;
        return StageType.Normal;
    }

    static int MatchScore(RawRow r, int gameChapter, StageType stageType, bool isGoldDungeon)
    {
        int score = 0;
        if (!r.goldDungeonWildcard && (r.isGoldDungeon != 0) != isGoldDungeon) return -1;
        if (!r.goldDungeonWildcard) score += 8;
        if (!r.stageTypeWildcard && r.stageType != stageType) return -1;
        if (!r.stageTypeWildcard) score += 4;
        if (!r.gameWildcard && r.gameChapter != gameChapter) return -1;
        if (!r.gameWildcard) score += 2;
        return score;
    }

    public static bool TryResolve(int gameChapter, StageType stageType, bool isGoldDungeon, out Row row)
    {
        EnsureLoaded();
        row = default;
        if (_rows.Count == 0) return false;

        RawRow? best = null;
        int bestScore = -1;
        for (int i = 0; i < _rows.Count; i++)
        {
            int score = MatchScore(_rows[i], gameChapter, stageType, isGoldDungeon);
            if (score > bestScore)
            {
                bestScore = score;
                best = _rows[i];
            }
        }

        if (!best.HasValue || bestScore < 0) return false;

        var b = best.Value;
        row = new Row
        {
            gameChapter = gameChapter,
            gameWildcard = b.gameWildcard,
            stageType = stageType,
            stageTypeWildcard = b.stageTypeWildcard,
            isGoldDungeon = isGoldDungeon,
            goldDungeonWildcard = b.goldDungeonWildcard,
            objective = b.objective,
            clearGold = b.clearGold,
            normalBase = b.normalBase,
            normalChapterAdd = b.normalChapterAdd,
            eliteGoldMul = b.eliteGoldMul > 0f ? b.eliteGoldMul : 1.5f,
            useFormula = b.clearGold <= 0 && b.normalBase > 0
        };
        return true;
    }
}
