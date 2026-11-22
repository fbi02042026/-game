using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 城镇入口 · 大地图派遣掉落表（slg_drop.csv → Data/Tables/slg_drop.bytes）。
/// <para>🔴 道具 ID 与主线 item_defs.csv 同源：SLG 掉的道具就是主线在用的天赋石/附魔石/招募卷/碎片，
/// 经 ItemGrantSystem 入「随身仓库」，不新造第二套体系（主人 2026-11-22 拍板）。</para>
/// <para>筛选键 = 近/远档 × 点位类型 × 章节；每行独立掷骰，可一次掉多种。
/// 缺表只 LogError 一次并给空结果——不静默兜底、不造默认掉落（铁律 10）。</para>
/// </summary>
public static class SlgDropTable
{
    struct Row
    {
        public string distTier;
        public string pointType;
        public int chapter;
        public string itemId;
        public float rate;
        public int countMin;
        public int countMax;
    }

    static List<Row> _rows;
    static bool _loaded;
    static bool _loggedMissing;

    /// <summary>改表后重读（编辑器热更用；运行时正常不用调）。</summary>
    public static void Reload()
    {
        _rows = null;
        _loaded = false;
        _loggedMissing = false;
    }

    static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        _rows = new List<Row>();

        string raw = GameTableStore.LoadText(ContentPaths.Data.SlgDrop);
        if (string.IsNullOrEmpty(raw))
        {
            if (!_loggedMissing)
            {
                Debug.LogError("[SlgDrop] 掉落表缺失或为空：" + ContentPaths.Data.SlgDrop
                               + "（跑 Tools/Data/Cook Tables 或同步 slg_drop.bytes）");
                _loggedMissing = true;
            }
            return;
        }

        var lines = GameTableCsv.ParseRows(raw);
        for (int i = 0; i < lines.Count; i++)
        {
            var c = lines[i];
            if (c == null || c.Length < 7) continue;
            if (c[0].Trim() == "distTier") continue;   // 表头

            var r = new Row
            {
                distTier = c[0].Trim().ToLowerInvariant(),
                pointType = c[1].Trim(),
                chapter = GameTableCsv.TryInt(c[2], out int ch) ? ch : 0,
                itemId = c[3].Trim(),
                rate = GameTableCsv.TryFloat(c[4], out float rt) ? rt : 0f,
                countMin = GameTableCsv.TryInt(c[5], out int mn) ? mn : 0,
                countMax = GameTableCsv.TryInt(c[6], out int mx) ? mx : 0,
            };
            if (string.IsNullOrEmpty(r.itemId) || r.rate <= 0f) continue;
            if (r.countMin <= 0) continue;
            if (r.countMax < r.countMin) r.countMax = r.countMin;
            _rows.Add(r);
        }
    }

    static bool Match(Row r, SlgDefs.DistTier tier, int chapter, string pointType)
    {
        string want = tier == SlgDefs.DistTier.Near ? "near" : "far";
        if (r.distTier != "*" && r.distTier != want) return false;
        if (r.chapter != 0 && r.chapter != chapter) return false;
        if (!string.IsNullOrEmpty(r.pointType) && r.pointType != "*"
            && !string.Equals(r.pointType, pointType, System.StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    /// <summary>
    /// 按档位掷掉落。rateScale=0.5 用于通商减半（数量不变）。
    /// 返回 itemId → 数量（可能 0~多行）；战败由调用方保证不调用。
    /// </summary>
    public static List<StringIntEntry> RollDrops(SlgDefs.DistTier tier, int chapter, string pointType, float rateScale = 1f)
    {
        var result = new List<StringIntEntry>();
        EnsureLoaded();
        if (_rows == null) return result;

        for (int i = 0; i < _rows.Count; i++)
        {
            var r = _rows[i];
            if (!Match(r, tier, chapter, pointType)) continue;
            if (Random.value > Mathf.Clamp01(r.rate * rateScale)) continue;
            int n = r.countMin == r.countMax ? r.countMin : Random.Range(r.countMin, r.countMax + 1);
            if (n <= 0) continue;
            result.Add(new StringIntEntry { id = r.itemId, value = n });
        }
        return result;
    }

    /// <summary>派遣弹窗用：该档位/章节/类型的可能掉落道具名（不掷骰、只列池子）。</summary>
    public static string Describe(SlgDefs.DistTier tier, int chapter, string pointType)
    {
        EnsureLoaded();
        var names = new List<string>();
        if (_rows != null)
            for (int i = 0; i < _rows.Count; i++)
            {
                var r = _rows[i];
                if (!Match(r, tier, chapter, pointType)) continue;
                var def = ItemDefs.Get(r.itemId);
                string nm = def != null ? def.name : r.itemId;
                if (!names.Contains(nm)) names.Add(nm);
            }
        return names.Count == 0 ? "无" : string.Join("/", names);
    }
}
