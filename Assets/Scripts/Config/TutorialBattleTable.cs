using System.Collections.Generic;
using UnityEngine;

public static class TutorialBattleTable
{
    public struct Step
    {
        public int order;
        public string action;
        public int count;
        public int spriteMelee;
        public int spriteRanged;
        public bool ambush;
        public string mercId;
        public float mercHpRatio;
        public float aheadDist;
        public bool stunned;
        public int eliteCount;
        public string note;
    }

    static readonly List<Step> _steps = new List<Step>();
    static bool _loaded;

    public static bool HasData => _loaded && _steps.Count > 0;

    public static void Reload()
    {
        _loaded = false;
        _steps.Clear();
        EnsureLoaded();
    }

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        string raw = GameTableStore.LoadText(ContentPaths.Data.TutorialBattle);
        if (string.IsNullOrEmpty(raw)) return;

        var lines = GameTableCsv.ParseRows(raw);
        for (int i = 1; i < lines.Count; i++)
        {
            var c = lines[i];
            if (c.Length < 5) continue;
            if (!GameTableCsv.TryInt(c[0], out int order)) continue;

            _steps.Add(new Step
            {
                order = order,
                action = c.Length > 1 ? c[1].Trim().ToLowerInvariant() : "normal",
                count = GameTableCsv.TryInt(c[2], out int cnt) ? cnt : 2,
                spriteMelee = GameTableCsv.TryInt(c[3], out int sm) ? sm : 2,
                spriteRanged = GameTableCsv.TryInt(c[4], out int sr) ? sr : 1,
                ambush = c.Length > 5 && GameTableCsv.TryBool(c[5], out bool amb) && amb,
                mercId = c.Length > 6 ? c[6].Trim() : "",
                mercHpRatio = c.Length > 7 && GameTableCsv.TryFloat(c[7], out float hp) ? hp : 0f,
                aheadDist = c.Length > 8 && GameTableCsv.TryFloat(c[8], out float ad) ? ad : 0f,
                stunned = c.Length > 9 && GameTableCsv.TryBool(c[9], out bool st) && st,
                eliteCount = c.Length > 10 && GameTableCsv.TryInt(c[10], out int ec) ? ec : 0,
                note = c.Length > 11 ? c[11] : ""
            });
        }
        _steps.Sort((a, b) => a.order.CompareTo(b.order));
        Debug.Log($"[TutorialBattle] 已加载 {_steps.Count} 条");
    }

    public static IReadOnlyList<Step> GetSteps()
    {
        EnsureLoaded();
        return _steps;
    }

    public static bool TryGetStep(int order, out Step step)
    {
        EnsureLoaded();
        for (int i = 0; i < _steps.Count; i++)
        {
            if (_steps[i].order == order)
            {
                step = _steps[i];
                return true;
            }
        }
        step = default;
        return false;
    }

    public static Step GetStepOrDefault(int order)
    {
        if (TryGetStep(order, out var step)) return step;
        return new Step
        {
            order = order,
            action = "normal",
            count = 2,
            spriteMelee = 2,
            spriteRanged = 1
        };
    }
}
