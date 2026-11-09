using System.Collections.Generic;
using UnityEngine;

/// <summary>跑酷配表（V1）：key/value/note 三列键值表，读 Resources/Data/Tables/run_track.bytes。
/// 数值真源见 Docs/《跑酷小游戏》_待办任务清单.md §3 / §4；缺表 LogError 不兜底。</summary>
public static class RunTrackTable
{
    static readonly Dictionary<string, string> _rows = new Dictionary<string, string>();
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

        string raw = GameTableStore.LoadText(ContentPaths.Data.RunTrack);
        if (string.IsNullOrEmpty(raw))
        {
            Debug.LogError("[RunTrack] 缺少跑酷配表 " + ContentPaths.Data.RunTrack + "（.bytes 与源 .csv 均不存在）");
            _rows.Clear();
            return;
        }

        var lines = GameTableCsv.ParseRows(raw);
        for (int i = 1; i < lines.Count; i++)
        {
            var c = lines[i];
            if (c.Length < 2) continue;
            string key = c[0];
            if (string.IsNullOrEmpty(key)) continue;
            _rows[key] = c[1];
        }
    }

    public static float GetFloat(string key, float def)
    {
        EnsureLoaded();
        return _rows.TryGetValue(key, out string v) && GameTableCsv.TryFloat(v, out float f) ? f : def;
    }

    public static int GetInt(string key, int def)
    {
        EnsureLoaded();
        return _rows.TryGetValue(key, out string v) && GameTableCsv.TryInt(v, out int n) ? n : def;
    }

    public static string GetRaw(string key, string def)
    {
        EnsureLoaded();
        return _rows.TryGetValue(key, out string v) ? v : def;
    }
}
