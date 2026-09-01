using System.Collections.Generic;
using UnityEngine;

public static class MonsterStatsTable
{
    static readonly Dictionary<string, MonsterStatsEntry> _byId = new Dictionary<string, MonsterStatsEntry>();
    static readonly Dictionary<int, MonsterStatsEntry> _byKey = new Dictionary<int, MonsterStatsEntry>();
    static readonly List<MonsterStatsEntry> _all = new List<MonsterStatsEntry>();
    static bool _loaded;

    public static bool HasData => _loaded && _all.Count > 0;

    public static void Reload()
    {
        _loaded = false;
        _byId.Clear();
        _byKey.Clear();
        _all.Clear();
        EnsureLoaded();
    }

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        string raw = GameTableStore.LoadText(ContentPaths.Data.MonsterStats);
        if (string.IsNullOrEmpty(raw))
        {
            Debug.LogWarning("[MonsterStats] 未找到 monster_stats 表，将回退 MonsterConfig SO");
            return;
        }

        var rows = GameTableCsv.ParseRows(raw);
        if (rows.Count < 2) return;

        for (int i = 1; i < rows.Count; i++)
        {
            var c = rows[i];
            if (c.Length < 15) continue;
            if (!GameTableCsv.TryInt(c[1], out int monsterChapter)) continue;
            if (!GameTableCsv.TryInt(c[2], out int spriteIndex)) continue;

            var e = new MonsterStatsEntry
            {
                id = c[0],
                monsterChapter = monsterChapter,
                spriteIndex = spriteIndex,
                monsterName = c[3],
                minWave = GameTableCsv.TryInt(c[4], out int mw) ? mw : 0,
                isBoss = GameTableCsv.TryBool(c[5], out bool boss) && boss,
                unlockClearCount = GameTableCsv.TryInt(c[6], out int uc) ? uc : 0,
                baseHp = GameTableCsv.TryFloat(c[7], out float hp) ? hp : 50f,
                baseAttack = GameTableCsv.TryFloat(c[8], out float atk) ? atk : 5f,
                baseAttackSpeed = GameTableCsv.TryFloat(c[9], out float asp) ? asp : 1.5f,
                attackRange = GameTableCsv.TryFloat(c[10], out float ar) ? ar : 1.5f,
                baseMoveSpeed = GameTableCsv.TryFloat(c[11], out float ms) ? ms : 2.2f,
                baseGoldDrop = GameTableCsv.TryInt(c[12], out int gold) ? gold : 10,
                expDrop = GameTableCsv.TryInt(c[13], out int exp) ? exp : 5,
                spriteScale = GameTableCsv.TryFloat(c[14], out float sc) ? sc : 1f
            };
            if (string.IsNullOrEmpty(e.id))
                e.id = BuildDefaultId(monsterChapter, spriteIndex);

            _all.Add(e);
            _byId[e.id] = e;
            _byKey[Key(monsterChapter, spriteIndex)] = e;
        }
        Debug.Log($"[MonsterStats] 已加载 {_all.Count} 条");
    }

    static int Key(int monsterChapter, int spriteIndex) => monsterChapter * 100 + spriteIndex;

    static string BuildDefaultId(int monsterChapter, int spriteIndex)
    {
        string theme = monsterChapter switch
        {
            1 => "undead", 2 => "jungle", 3 => "sea", 4 => "forest",
            5 => "field", 6 => "cave", 7 => "devil", 8 => "ice", _ => "mob"
        };
        return $"{theme}_{monsterChapter}{spriteIndex:00}";
    }

    public static MonsterStatsEntry GetById(string id)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(id)) return null;
        return _byId.TryGetValue(id, out var e) ? e : null;
    }

    public static MonsterStatsEntry Get(int monsterChapter, int spriteIndex)
    {
        EnsureLoaded();
        return _byKey.TryGetValue(Key(monsterChapter, spriteIndex), out var e) ? e : null;
    }

    public static List<MonsterStatsEntry> GetAllForChapter(int monsterChapter)
    {
        EnsureLoaded();
        var list = new List<MonsterStatsEntry>();
        for (int i = 0; i < _all.Count; i++)
        {
            if (_all[i].monsterChapter == monsterChapter)
                list.Add(_all[i]);
        }
        return list;
    }

    public static IReadOnlyList<MonsterStatsEntry> GetAll()
    {
        EnsureLoaded();
        return _all;
    }
}
