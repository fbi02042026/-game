using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 隐藏经验等级：不展示给玩家。裂缝掉落稀有度的唯一真源（勿接 equip_rarity_rules 关卡段权重）。
/// </summary>
public static class HiddenLevelSystem
{
    public struct LevelRow
    {
        public int Level;
        public string Name;
        public int ExpToReach;
        public int CumExp;
        public int WNormal;
        public int WRare;
        public int WLegend;
    }

    static readonly List<LevelRow> _rows = new List<LevelRow>();
    static readonly Dictionary<int, LevelRow> _byLevel = new Dictionary<int, LevelRow>();
    static bool _loaded;

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        string raw = GameTableStore.LoadText(ContentPaths.Data.HiddenLevelRules);
        if (string.IsNullOrEmpty(raw))
        {
            Debug.LogWarning("[HiddenLevel] 未找到 hidden_level_rules 表");
            return;
        }
        var rows = GameTableCsv.ParseRows(raw);
        for (int i = 1; i < rows.Count; i++)
        {
            var c = rows[i];
            if (c.Length < 7) continue;
            if (!GameTableCsv.TryInt(c[0], out int lv) || lv <= 0) continue;
            var row = new LevelRow
            {
                Level = lv,
                Name = c[1].Trim()
            };
            GameTableCsv.TryInt(c[2], out row.ExpToReach);
            GameTableCsv.TryInt(c[3], out row.CumExp);
            GameTableCsv.TryInt(c[4], out row.WNormal);
            GameTableCsv.TryInt(c[5], out row.WRare);
            GameTableCsv.TryInt(c[6], out row.WLegend);
            _rows.Add(row);
            _byLevel[lv] = row;
        }
    }

    public static void Reload()
    {
        _loaded = false;
        _rows.Clear();
        _byLevel.Clear();
        EnsureLoaded();
    }

    public static int MaxLevel
    {
        get
        {
            EnsureLoaded();
            return GameConfig.HIDDEN_LEVEL_MAX;
        }
    }

    public static int GetLevel(SaveData data)
    {
        if (data == null) return 1;
        return Mathf.Clamp(data.hiddenLevel <= 0 ? 1 : data.hiddenLevel, 1, MaxLevel);
    }

    public static int GetExp(SaveData data) => data != null ? Mathf.Max(0, data.hiddenExp) : 0;

    public static bool TryGetRow(int level, out LevelRow row)
    {
        EnsureLoaded();
        return _byLevel.TryGetValue(Mathf.Clamp(level, 1, MaxLevel), out row);
    }

    /// <summary>当前隐藏等级的普通/稀有/传奇权重。</summary>
    public static void GetRarityWeights(out int wNormal, out int wRare, out int wLegend)
    {
        wNormal = 100;
        wRare = 0;
        wLegend = 0;
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (!TryGetRow(GetLevel(data), out var row)) return;
        wNormal = Mathf.Max(0, row.WNormal);
        wRare = Mathf.Max(0, row.WRare);
        wLegend = Mathf.Max(0, row.WLegend);
    }

    public static void AddExp(int amount)
    {
        if (amount <= 0) return;
        var save = SaveSystem.Instance;
        if (save?.Data == null) return;
        EnsureLoaded();

        var data = save.Data;
        if (data.hiddenLevel <= 0) data.hiddenLevel = 1;
        if (data.hiddenLevel >= MaxLevel) return;

        data.hiddenExp += amount;
        int guard = 0;
        while (data.hiddenLevel < MaxLevel && guard++ < 64)
        {
            int next = data.hiddenLevel + 1;
            if (!_byLevel.TryGetValue(next, out var nextRow))
                break;
            int need = Mathf.Max(0, nextRow.ExpToReach);
            if (need <= 0 || data.hiddenExp < need)
                break;
            data.hiddenExp -= need;
            data.hiddenLevel = next;
        }
        if (data.hiddenLevel >= MaxLevel)
            data.hiddenExp = 0;

        save.Save();
    }

    public static void AddKillExp(Monster m)
    {
        if (m == null) return;
        int exp = GameConfig.HIDDEN_EXP_KILL_NORMAL;
        if (m.IsBossUnit) exp = GameConfig.HIDDEN_EXP_KILL_BOSS;
        else if (m.IsEliteWave) exp = GameConfig.HIDDEN_EXP_KILL_ELITE;
        AddExp(exp);
    }

    public static void AddStageClearExp(int chapter)
    {
        int exp = GameConfig.HIDDEN_EXP_STAGE_CLEAR
            + Mathf.Max(0, chapter - 1) * GameConfig.HIDDEN_EXP_STAGE_CLEAR_PER_CHAPTER;
        AddExp(exp);
    }
}
