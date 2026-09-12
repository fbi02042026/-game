using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家技能元数据表（读 Cook 后的 player_skills.bytes）。
/// 只提供展示/解锁/Ally 映射；伤害与特效仍走 Ally SkillConfig。
/// </summary>
public static class PlayerSkillTable
{
    public struct Row
    {
        public string Id;
        public string DisplayName;
        public PlayerSkillDefs.Kind Kind;
        public string Desc;
        public string Numbers;
        public float Cooldown;
        public float Duration;
        public string UseHint;
        public int UnlockChapter;
        public string AllyConfigId;
    }

    static readonly List<Row> _rows = new List<Row>();
    static readonly Dictionary<string, Row> _byId = new Dictionary<string, Row>();
    static bool _loaded;

    public static bool HasData
    {
        get
        {
            EnsureLoaded();
            return _rows.Count > 0;
        }
    }

    public static IReadOnlyList<Row> Rows
    {
        get
        {
            EnsureLoaded();
            return _rows;
        }
    }

    public static void Reload()
    {
        _loaded = false;
        _rows.Clear();
        _byId.Clear();
        EnsureLoaded();
    }

    public static bool TryGet(string id, out Row row)
    {
        EnsureLoaded();
        row = default;
        if (string.IsNullOrEmpty(id)) return false;
        return _byId.TryGetValue(id, out row);
    }

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        _rows.Clear();
        _byId.Clear();

        string raw = GameTableStore.LoadText(ContentPaths.Data.PlayerSkills);
        if (string.IsNullOrEmpty(raw))
        {
            Debug.LogWarning("[PlayerSkillTable] 表缺失，使用 PlayerSkillDefs.Fallback");
            return;
        }

        var parsed = GameTableCsv.ParseRows(raw);
        int ok = 0;
        for (int i = 0; i < parsed.Count; i++)
        {
            var c = parsed[i];
            if (c.Length < 10) continue;
            if (c[0] == "id" || c[0].StartsWith("#")) continue;
            string id = c[0].Trim();
            if (string.IsNullOrEmpty(id)) continue;
            if (!TryParseKind(c[2].Trim(), out PlayerSkillDefs.Kind kind))
            {
                Debug.LogError("[PlayerSkillTable] 未知 kind，跳过行: " + id + " / " + c[2]);
                continue;
            }

            GameTableCsv.TryFloat(c[5], out float cd);
            GameTableCsv.TryFloat(c[6], out float dur);
            GameTableCsv.TryInt(c[8], out int unlock);
            var row = new Row
            {
                Id = id,
                DisplayName = c[1].Trim(),
                Kind = kind,
                Desc = c[3].Trim(),
                Numbers = c[4].Trim(),
                Cooldown = cd,
                Duration = dur,
                UseHint = c[7].Trim(),
                UnlockChapter = unlock,
                AllyConfigId = c[9].Trim()
            };
            _rows.Add(row);
            _byId[id] = row;
            ok++;
        }

        if (ok <= 0)
            Debug.LogWarning("[PlayerSkillTable] 解析 0 条，使用 PlayerSkillDefs.Fallback");
        else
            Debug.Log($"[PlayerSkillTable] 已加载 {ok} 条玩家技能元数据");
    }

    static bool TryParseKind(string s, out PlayerSkillDefs.Kind kind)
    {
        kind = PlayerSkillDefs.Kind.Heal;
        if (string.IsNullOrEmpty(s)) return false;
        switch (s)
        {
            case "Heal": kind = PlayerSkillDefs.Kind.Heal; return true;
            case "Shield": kind = PlayerSkillDefs.Kind.Shield; return true;
            case "AtkBuff": kind = PlayerSkillDefs.Kind.AtkBuff; return true;
            case "AtkSpeedBuff": kind = PlayerSkillDefs.Kind.AtkSpeedBuff; return true;
            case "CritBuff": kind = PlayerSkillDefs.Kind.CritBuff; return true;
            case "Aoe": kind = PlayerSkillDefs.Kind.Aoe; return true;
            default: return false;
        }
    }
}
