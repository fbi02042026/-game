using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 佣兵台词表（读 Cook 后的 merc_lines.bytes）。
/// 场景：战斗中 / 酒馆刷新 / 招募成功 / MVP / 战死。
/// </summary>
public static class MercLineTable
{
    public enum Scene
    {
        Combat,
        TavernRefresh,
        HireSuccess,
        Mvp,
        Death
    }

    static Dictionary<string, List<string>> _lines;
    static bool _loaded;

    public static void Reload()
    {
        _loaded = false;
        _lines = null;
        EnsureLoaded();
    }

    static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        _lines = new Dictionary<string, List<string>>();

        string raw = GameTableStore.LoadText(ContentPaths.Data.MercLines);
        if (string.IsNullOrEmpty(raw))
        {
            Debug.LogWarning("[MercLineTable] 未找到 merc_lines 表");
            return;
        }

        string[] rows = raw.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        int ok = 0;
        for (int i = 0; i < rows.Length; i++)
        {
            string line = rows[i].Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
            if (line.StartsWith("佣兵ID") || line.StartsWith("hireId")) continue;

            string[] cols = SplitCsvLine(line);
            if (cols.Length < 8) continue;
            string hireId = cols[0].Trim();
            if (string.IsNullOrEmpty(hireId) || !hireId.StartsWith("H")) continue;
            Scene scene;
            if (!TryParseScene(cols[5].Trim(), out scene)) continue;
            string text = cols[7].Trim();
            if (string.IsNullOrEmpty(text)) continue;

            string key = MakeKey(hireId, scene);
            if (!_lines.TryGetValue(key, out var list))
            {
                list = new List<string>();
                _lines[key] = list;
            }
            list.Add(text);
            ok++;
        }
        Debug.Log($"[MercLineTable] 加载台词 {ok} 条");
    }

    static string MakeKey(string hireId, Scene scene) => hireId + "|" + ((int)scene);

    static bool TryParseScene(string name, out Scene scene)
    {
        switch (name)
        {
            case "战斗中": scene = Scene.Combat; return true;
            case "酒馆刷新": scene = Scene.TavernRefresh; return true;
            case "招募成功": scene = Scene.HireSuccess; return true;
            case "MVP": scene = Scene.Mvp; return true;
            case "战死": scene = Scene.Death; return true;
            default:
                scene = Scene.Combat;
                return false;
        }
    }

    /// <summary>随机一条；无表则回退短句。支持 H 编号或 AssetId。</summary>
    public static string Pick(string hireIdOrAssetId, Scene scene)
    {
        EnsureLoaded();
        string hireId = ResolveHireId(hireIdOrAssetId);
        if (!string.IsNullOrEmpty(hireId) && _lines != null
            && _lines.TryGetValue(MakeKey(hireId, scene), out var list) && list != null && list.Count > 0)
            return list[Random.Range(0, list.Count)];

        return Fallback(hireId, scene);
    }

    static string ResolveHireId(string id)
    {
        if (string.IsNullOrEmpty(id)) return id;
        if (MercRosterDefs.TryGetByHireId(id, out _)) return id;
        if (MercRosterDefs.TryGetByAssetId(id, out var byAsset) && !string.IsNullOrEmpty(byAsset.HireId))
            return byAsset.HireId;
        string resolved = MercPortraitSprites.ResolveHireId(id);
        return !string.IsNullOrEmpty(resolved) ? resolved : id;
    }

    static string Fallback(string hireId, Scene scene)
    {
        string nick = hireId;
        if (MercRosterDefs.TryGetByHireId(hireId, out var d) && !string.IsNullOrEmpty(d.Nickname))
            nick = d.Nickname;
        else if (MercRosterDefs.TryGetByAssetId(hireId, out var a) && !string.IsNullOrEmpty(a.Nickname))
            nick = a.Nickname;
        switch (scene)
        {
            case Scene.TavernRefresh: return $"{nick}：有活吗？";
            case Scene.HireSuccess: return $"{nick}：走一趟。";
            case Scene.Mvp: return $"{nick}：这趟还行。";
            case Scene.Death: return $"{nick}：先撤……";
            default: return $"{nick}：上！";
        }
    }

    static string[] SplitCsvLine(string line)
    {
        var cols = new List<string>();
        if (string.IsNullOrEmpty(line)) return cols.ToArray();
        var sb = new System.Text.StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }
            if (c == ',' && !inQuotes)
            {
                cols.Add(sb.ToString());
                sb.Length = 0;
                continue;
            }
            sb.Append(c);
        }
        cols.Add(sb.ToString());
        return cols.ToArray();
    }
}
