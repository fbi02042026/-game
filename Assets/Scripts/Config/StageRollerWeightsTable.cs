using System.Collections.Generic;
using UnityEngine;

public static class StageRollerWeightsTable
{
    static readonly Dictionary<string, float> _floats = new Dictionary<string, float>();
    static readonly Dictionary<string, int> _ints = new Dictionary<string, int>();
    static bool _loaded;

    public static bool HasData => _loaded && (_floats.Count > 0 || _ints.Count > 0);

    public static void Reload()
    {
        _loaded = false;
        _floats.Clear();
        _ints.Clear();
        EnsureLoaded();
    }

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        string raw = GameTableStore.LoadText(ContentPaths.Data.StageRollerWeights);
        if (string.IsNullOrEmpty(raw)) return;

        var lines = GameTableCsv.ParseRows(raw);
        for (int i = 1; i < lines.Count; i++)
        {
            var c = lines[i];
            if (c.Length < 2) continue;
            string key = c[0].Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(key)) continue;

            if (GameTableCsv.TryInt(c[1], out int iv) && !c[1].Contains("."))
                _ints[key] = iv;
            else if (GameTableCsv.TryFloat(c[1], out float fv))
                _floats[key] = fv;
        }
        Debug.Log($"[StageRollerWeights] 已加载 {_floats.Count + _ints.Count} 条");
    }

    public static int GetInt(string key, int defaultValue)
    {
        EnsureLoaded();
        key = key.ToLowerInvariant();
        return _ints.TryGetValue(key, out int v) ? v : defaultValue;
    }

    public static float GetFloat(string key, float defaultValue)
    {
        EnsureLoaded();
        key = key.ToLowerInvariant();
        return _floats.TryGetValue(key, out float v) ? v : defaultValue;
    }
}
