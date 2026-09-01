using System.Collections.Generic;
using UnityEngine;

public static class SpritePickWeightTable
{
    struct WeightRow
    {
        public int stageIndexMin;
        public int stageIndexMax;
        public int spriteIndex;
        public float weight;
        public string formula;
        public float minWeight;
        public bool useFormula;
    }

    static readonly List<WeightRow> _rows = new List<WeightRow>();
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

        string raw = GameTableStore.LoadText(ContentPaths.Data.SpritePickWeight);
        if (string.IsNullOrEmpty(raw)) return;

        var lines = GameTableCsv.ParseRows(raw);
        for (int i = 1; i < lines.Count; i++)
        {
            var c = lines[i];
            if (c.Length < 4) continue;
            if (!GameTableCsv.TryInt(c[0], out int min)) continue;
            int max = GameTableCsv.TryInt(c[1], out int mx) ? mx : min;
            int sprite = GameTableCsv.TryInt(c[2], out int sp) ? sp : 0;
            float weight = c.Length > 3 && GameTableCsv.TryFloat(c[3], out float w) ? w : 1f;
            string formula = c.Length > 4 ? c[4].Trim() : "";
            float minW = c.Length > 5 && GameTableCsv.TryFloat(c[5], out float mw) ? mw : 1f;

            _rows.Add(new WeightRow
            {
                stageIndexMin = min,
                stageIndexMax = max,
                spriteIndex = sprite,
                weight = weight,
                formula = formula,
                minWeight = minW,
                useFormula = !string.IsNullOrEmpty(formula)
            });
        }
        Debug.Log($"[SpritePickWeight] 已加载 {_rows.Count} 条");
    }

    static bool InRange(WeightRow r, int stageIndex) =>
        stageIndex >= r.stageIndexMin && stageIndex <= r.stageIndexMax;

    public static float GetWeight(int stageIndex, int spriteIndex)
    {
        EnsureLoaded();
        if (_rows.Count == 0) return -1f;

        WeightRow? exact = null;
        WeightRow? fallback = null;
        for (int i = 0; i < _rows.Count; i++)
        {
            if (!InRange(_rows[i], stageIndex)) continue;
            if (_rows[i].spriteIndex == spriteIndex)
                exact = _rows[i];
            else if (_rows[i].spriteIndex == 0)
                fallback = _rows[i];
        }

        WeightRow? row = exact ?? fallback;
        if (!row.HasValue) return 1f;

        var r = row.Value;
        if (r.useFormula)
        {
            float v = EvalFormula(r.formula, spriteIndex);
            return Mathf.Max(r.minWeight, v);
        }
        return r.weight > 0f ? r.weight : 1f;
    }

    static float EvalFormula(string formula, int spriteIndex)
    {
        if (string.IsNullOrEmpty(formula)) return 1f;
        formula = formula.Trim().Replace("spriteIndex", spriteIndex.ToString());
        if (formula.Contains("*"))
        {
            var parts = formula.Split('*');
            float result = 1f;
            for (int i = 0; i < parts.Length; i++)
            {
                if (float.TryParse(parts[i].Trim(), out float f))
                    result *= f;
            }
            return result;
        }
        if (float.TryParse(formula, out float single)) return single;
        return 1f;
    }
}
