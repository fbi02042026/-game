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

            string[] cols = GameTableCsv.SplitCsvLine(line);
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
        // 2026-10-23 主人拍板：唯一出口 MercRosterDefs.ResolveHireId；
        // 查不到时退回原串（保持本表历史口径：Fallback 要拿原 id 拼提示，不能传 null）。
        string hireId = MercRosterDefs.ResolveHireId(hireIdOrAssetId) ?? hireIdOrAssetId;
        if (!string.IsNullOrEmpty(hireId) && _lines != null
            && _lines.TryGetValue(MakeKey(hireId, scene), out var list) && list != null && list.Count > 0)
            return list[Random.Range(0, list.Count)];

        return Fallback(hireId, scene);
    }

    static string Fallback(string hireId, Scene scene)
    {
        // 【2026-10-11 主人拍板「佣兵名字要统一」】台词前缀只认**真名**（塔克），不再用昵称（重盾 / 小红）。
        // 旧写法取 Nickname → 头顶名签写「塔克」、同一句台词气泡却写「小红」，玩家会当成两个人。
        string who = ResolveDisplayName(hireId);
        switch (scene)
        {
            case Scene.TavernRefresh: return $"{who}：有活吗？";
            case Scene.HireSuccess: return $"{who}：走一趟。";
            case Scene.Mvp: return $"{who}：这趟还行。";
            case Scene.Death: return $"{who}：先撤……";
            default: return $"{who}：上！";
        }
    }

    /// <summary>同一句台词只报一次「缺真名」，别刷屏。</summary>
    static readonly HashSet<string> _missingNameWarned = new HashSet<string>();

    /// <summary>
    /// 台词前缀的名字 —— <b>唯一出口</b>：只认花名册真名（<c>MercRosterDefs.Def.Name</c>）。
    /// <para>真源缺名字 → fail closed：LogError 报出来让主人看见，用 hireId 顶上（铁律 14），
    /// 不拿昵称 / 职业名悄悄顶替。</para>
    /// </summary>
    static string ResolveDisplayName(string hireId)
    {
        MercRosterDefs.Def def;
        if (!string.IsNullOrEmpty(hireId) && MercRosterDefs.TryGetByHireId(hireId, out def)
            && !string.IsNullOrEmpty(def.Name))
            return def.Name;
        if (!string.IsNullOrEmpty(hireId) && MercRosterDefs.TryGetByAssetId(hireId, out def)
            && !string.IsNullOrEmpty(def.Name))
            return def.Name;

        string key = string.IsNullOrEmpty(hireId) ? "(空)" : hireId;
        if (_missingNameWarned.Add(key))
            Debug.LogError($"[MercLineTable] 佣兵 {key} 在花名册里没有真名 Name，台词前缀退回 {key} —— "
                + "去 merc_roster.csv 补真名，别拿昵称顶替");
        return string.IsNullOrEmpty(hireId) ? "佣兵" : hireId;
    }
}
