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
            Debug.LogError("[MonsterStatsTable] 战斗表加载失败: Resources/" + ContentPaths.Data.MonsterStats
                + " （空或缺失），using defaults（回退 MonsterConfig SO）。");
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
                spriteScale = GameTableCsv.TryFloat(c[14], out float sc) ? sc : 1f,
                // 2026-09-26 新增：魔法攻击 / 魔法防御（旧 .bytes 只有 15 列 → 缺列时留 0，
                // 由 Monster.Init 等比沿用 baseAttack / baseDef，不发明数值）
                baseMagicAttack = (c.Length > 15 && GameTableCsv.TryFloat(c[15], out float matk)) ? matk : 0f,
                baseMagicDefense = (c.Length > 16 && GameTableCsv.TryFloat(c[16], out float mdef)) ? mdef : 0f
            };
            if (string.IsNullOrEmpty(e.id))
                e.id = BuildDefaultId(monsterChapter, spriteIndex);

            _all.Add(e);
            _byId[e.id] = e;
            _byKey[Key(monsterChapter, spriteIndex)] = e;
        }
        if (_all.Count <= 0)
            Debug.LogError("[MonsterStatsTable] 战斗表加载失败: Resources/" + ContentPaths.Data.MonsterStats
                + " （解析 0 条），using defaults（回退 MonsterConfig SO）。");
        else
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

    /// <summary>
    /// 取某章【普通怪】（排除 isBoss）的 baseHp / baseAttack 平均值。
    /// 用途：精英兜底值不该写死常量（第 3 章起会低于本章杂兵），改由本章普通怪基准推导。
    /// 表缺失兜底（分级）：本章无普通怪 → 依次向低章（chapter-1 … 1）借基准；
    /// 全部章节都没数据才返回 false，调用方回退 GameConfig 常量。
    /// </summary>
    public static bool TryGetChapterAverage(int monsterChapter, out float avgHp, out float avgAtk)
    {
        avgHp = 0f;
        avgAtk = 0f;
        EnsureLoaded();

        int from = monsterChapter;
        if (from < 1) from = 1;
        for (int ch = from; ch >= 1; ch--)
        {
            float hp = 0f, atk = 0f;
            int n = 0;
            for (int i = 0; i < _all.Count; i++)
            {
                var e = _all[i];
                if (e == null || e.monsterChapter != ch || e.isBoss) continue;
                hp += e.baseHp;
                atk += e.baseAttack;
                n++;
            }
            if (n <= 0) continue;

            avgHp = hp / n;
            avgAtk = atk / n;
            if (ch != monsterChapter)
                Debug.LogWarning($"[MonsterStatsTable] 第 {monsterChapter} 章无普通怪数据，精英基准借用第 {ch} 章。");
            return true;
        }
        Debug.LogWarning("[MonsterStatsTable] 所有章节均无普通怪数据，精英回退 GameConfig 常量。");
        return false;
    }

    public static IReadOnlyList<MonsterStatsEntry> GetAll()
    {
        EnsureLoaded();
        return _all;
    }
}
