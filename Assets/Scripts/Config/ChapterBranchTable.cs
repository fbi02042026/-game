using System.Collections.Generic;
using UnityEngine;

public static class ChapterBranchTable
{
    public struct Edge
    {
        public int fromIndex;
        public int toIndex;
        public string edgeKind;
        public int priority;
    }

    public struct BranchRules
    {
        public int branchCountMin;
        public int branchCountMax;
        public int branchPoolFrom;
        public int branchPoolTo;
        public int skipDistance;
    }

    struct RawEdge
    {
        public int gameChapter;
        public bool gameWildcard;
        public int fromIndex;
        public int toIndex;
        public string edgeKind;
        public int priority;
    }

    struct RawRules
    {
        public int gameChapter;
        public bool gameWildcard;
        public int branchCountMin;
        public int branchCountMax;
        public int branchPoolFrom;
        public int branchPoolTo;
        public int skipDistance;
    }

    static readonly List<RawEdge> _edges = new List<RawEdge>();
    static readonly List<RawRules> _rules = new List<RawRules>();
    static bool _loaded;

    public static bool HasData => _loaded && (_edges.Count > 0 || _rules.Count > 0);

    public static void Reload()
    {
        _loaded = false;
        _edges.Clear();
        _rules.Clear();
        EnsureLoaded();
    }

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        LoadEdges();
        LoadRules();
    }

    static void LoadEdges()
    {
        string raw = GameTableStore.LoadText(ContentPaths.Data.ChapterBranch);
        if (string.IsNullOrEmpty(raw)) return;

        var lines = GameTableCsv.ParseRows(raw);
        for (int i = 1; i < lines.Count; i++)
        {
            var c = lines[i];
            if (c.Length < 3) continue;
            if (!GameTableCsv.TryInt(c[1], out int from)) continue;
            if (!GameTableCsv.TryInt(c[2], out int to)) continue;

            _edges.Add(new RawEdge
            {
                gameWildcard = GameTableCsv.IsWildcard(c[0]),
                gameChapter = GameTableCsv.TryInt(c[0], out int gc) ? gc : 0,
                fromIndex = from,
                toIndex = to,
                edgeKind = c.Length > 3 ? c[3].Trim() : "main",
                priority = c.Length > 4 && GameTableCsv.TryInt(c[4], out int p) ? p : 0
            });
        }
        Debug.Log($"[ChapterBranch] 已加载边 {_edges.Count} 条");
    }

    static void LoadRules()
    {
        string raw = GameTableStore.LoadText(ContentPaths.Data.ChapterBranchRules);
        if (string.IsNullOrEmpty(raw)) return;

        var lines = GameTableCsv.ParseRows(raw);
        for (int i = 1; i < lines.Count; i++)
        {
            var c = lines[i];
            if (c.Length < 6) continue;

            _rules.Add(new RawRules
            {
                gameWildcard = GameTableCsv.IsWildcard(c[0]),
                gameChapter = GameTableCsv.TryInt(c[0], out int gc) ? gc : 0,
                branchCountMin = GameTableCsv.TryInt(c[1], out int bmin) ? bmin : 1,
                branchCountMax = GameTableCsv.TryInt(c[2], out int bmax) ? bmax : 2,
                branchPoolFrom = GameTableCsv.TryInt(c[3], out int pf) ? pf : 1,
                branchPoolTo = GameTableCsv.TryInt(c[4], out int pt) ? pt : 5,
                skipDistance = GameTableCsv.TryInt(c[5], out int sd) ? sd : 2
            });
        }
        Debug.Log($"[ChapterBranch] 已加载规则 {_rules.Count} 条");
    }

    static RawRules? FindRules(int gameChapter)
    {
        EnsureLoaded();
        RawRules? best = null;
        int bestScore = -1;
        for (int i = 0; i < _rules.Count; i++)
        {
            int score = _rules[i].gameWildcard ? 0 : (_rules[i].gameChapter == gameChapter ? 2 : -1);
            if (score > bestScore)
            {
                bestScore = score;
                best = _rules[i];
            }
        }
        return best;
    }

    public static List<Edge> GetMainEdges(int gameChapter)
    {
        EnsureLoaded();
        var list = new List<Edge>();
        for (int i = 0; i < _edges.Count; i++)
        {
            var e = _edges[i];
            if (!e.gameWildcard && e.gameChapter != gameChapter) continue;
            if (!e.edgeKind.Equals("main", System.StringComparison.OrdinalIgnoreCase)
                && !e.edgeKind.Equals("skip", System.StringComparison.OrdinalIgnoreCase))
                continue;
            if (e.edgeKind.Equals("main", System.StringComparison.OrdinalIgnoreCase))
            {
                list.Add(new Edge
                {
                    fromIndex = e.fromIndex,
                    toIndex = e.toIndex,
                    edgeKind = e.edgeKind,
                    priority = e.priority
                });
            }
        }
        return list;
    }

    public static List<Edge> GetFixedSkipEdges(int gameChapter)
    {
        EnsureLoaded();
        var list = new List<Edge>();
        for (int i = 0; i < _edges.Count; i++)
        {
            var e = _edges[i];
            if (!e.gameWildcard && e.gameChapter != gameChapter) continue;
            if (!e.edgeKind.Equals("skip", System.StringComparison.OrdinalIgnoreCase)) continue;
            list.Add(new Edge
            {
                fromIndex = e.fromIndex,
                toIndex = e.toIndex,
                edgeKind = e.edgeKind,
                priority = e.priority
            });
        }
        return list;
    }

    public static bool TryGetRules(int gameChapter, out BranchRules rules)
    {
        var found = FindRules(gameChapter);
        if (!found.HasValue)
        {
            rules = default;
            return false;
        }
        var r = found.Value;
        rules = new BranchRules
        {
            branchCountMin = r.branchCountMin,
            branchCountMax = r.branchCountMax,
            branchPoolFrom = r.branchPoolFrom,
            branchPoolTo = r.branchPoolTo,
            skipDistance = r.skipDistance
        };
        return true;
    }
}
