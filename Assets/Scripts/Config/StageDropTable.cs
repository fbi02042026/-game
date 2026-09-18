using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 关卡掉落表：Assets/Data/Source/Tables/stage_drop.csv → Resources/Data/Tables/stage_drop.bytes
///
/// 2026-09-18 新增。掉落率口径出自 Docs/佣兵养成_随机化与抗性_2026-09-17.md §7：
///   徽记    按「章 × 关卡类型 × 主产职业 × 档位」掷，Normal 关 0.10 ≈ 10 只怪 × 1%
///   本命碎片 **不走掉落**，来源是商店买重复佣兵转化（见 MercGrowInventory.ConvertDuplicateMerc）
///
/// 通配写法：gameChapter / stageType / jobKey / tier 写 `*` 表示通配。
/// jobKey / tier 为通配时，从该章 Normal 行的主产组合里随机抽一个。
/// </summary>
public static class StageDropTable
{
    public class DropResult
    {
        public string id;
        public int count;
    }

    enum DropType { Badge, Fragment }

    class Row
    {
        public int chapter;         // 0 = 通配
        public string stageType;    // "" = 通配
        public DropType type;
        public string jobKey;       // "" = 通配
        public string tier;         // "" = 通配
        public float rate;
        public int countMin = 1;
        public int countMax = 1;
        public int firstClearBonus;
    }

    static readonly List<Row> _rows = new List<Row>();
    static bool _loaded;

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

        string raw = GameTableStore.LoadText(ContentPaths.Data.StageDrop);
        if (string.IsNullOrEmpty(raw))
        {
            Debug.LogWarning("[StageDrop] 掉落表缺失：Resources/" + ContentPaths.Data.StageDrop + "（不掉落徽记）");
            return;
        }

        var rows = GameTableCsv.ParseRows(raw);
        if (rows.Count < 2) return;

        for (int i = 1; i < rows.Count; i++)
        {
            var c = rows[i];
            if (c.Length < 9) continue;

            var r = new Row
            {
                chapter = c[0].Trim() == "*" ? 0 : (GameTableCsv.TryInt(c[0], out int ch) ? ch : 0),
                stageType = c[1].Trim() == "*" ? "" : c[1].Trim(),
                jobKey = c[3].Trim() == "*" ? "" : c[3].Trim(),
                tier = c[4].Trim() == "*" ? "" : c[4].Trim(),
                rate = GameTableCsv.TryFloat(c[5], out float rate) ? rate : 0f,
                countMin = GameTableCsv.TryInt(c[6], out int mn) ? mn : 1,
                countMax = GameTableCsv.TryInt(c[7], out int mx) ? mx : 1,
                firstClearBonus = GameTableCsv.TryInt(c[8], out int fb) ? fb : 0
            };
            r.type = c[2].Trim().Equals("Fragment", StringComparison.OrdinalIgnoreCase)
                ? DropType.Fragment : DropType.Badge;
            if (r.countMax < r.countMin) r.countMax = r.countMin;
            _rows.Add(r);
        }
    }

    /// <summary>该章的主产组合（Normal 行、jobKey 明确的行），通配抽取用。</summary>
    static List<Row> MainPool(int chapter)
    {
        var pool = new List<Row>();
        for (int i = 0; i < _rows.Count; i++)
        {
            var r = _rows[i];
            if (r.type != DropType.Badge) continue;
            if (r.chapter != chapter && r.chapter != 0) continue;
            if (!string.IsNullOrEmpty(r.stageType) &&
                !r.stageType.Equals("Normal", StringComparison.OrdinalIgnoreCase)) continue;
            if (string.IsNullOrEmpty(r.jobKey) || string.IsNullOrEmpty(r.tier)) continue;
            if (r.rate <= 0f) continue;
            pool.Add(r);
        }
        return pool;
    }

    /// <summary>
    /// 掷一次关卡的徽记掉落。firstClear 传 true 时额外给保底（§7.4：每章首次通关 +2）。
    /// 返回的 id 是存档口径（badge:{职业}:{档位}），直接喂给 MercGrowInventory.Add。
    /// </summary>
    public static List<DropResult> RollBadges(int gameChapter, string stageType, bool firstClear)
    {
        var res = new List<DropResult>();
        EnsureLoaded();
        var pool = MainPool(gameChapter);
        var rng = new System.Random();
        int bonus = 0;

        for (int i = 0; i < _rows.Count; i++)
        {
            var r = _rows[i];
            if (r.type != DropType.Badge) continue;
            if (r.chapter != 0 && r.chapter != gameChapter) continue;
            if (!string.IsNullOrEmpty(r.stageType) &&
                !r.stageType.Equals(stageType, StringComparison.OrdinalIgnoreCase)) continue;

            if (firstClear && r.firstClearBonus > bonus) bonus = r.firstClearBonus;

            if (r.rate <= 0f) continue;
            if (rng.NextDouble() >= r.rate) continue;

            string job = r.jobKey;
            string tier = r.tier;
            if (string.IsNullOrEmpty(job) || string.IsNullOrEmpty(tier))
            {
                if (pool.Count <= 0) continue;
                var pick = pool[rng.Next(pool.Count)];
                job = pick.jobKey;
                tier = pick.tier;
            }
            int n = r.countMax > r.countMin ? rng.Next(r.countMin, r.countMax + 1) : r.countMin;
            Add(res, MercGrowInventory.BadgeId(job, tier), n);
        }

        // 保底：本章首次通关额外给 N 个（职业/档位同样按主产随机）
        for (int i = 0; i < bonus; i++)
        {
            if (pool.Count <= 0) break;
            var pick = pool[rng.Next(pool.Count)];
            Add(res, MercGrowInventory.BadgeId(pick.jobKey, pick.tier), 1);
        }

        return res;
    }

    static void Add(List<DropResult> list, string id, int count)
    {
        if (string.IsNullOrEmpty(id) || count <= 0) return;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].id == id)
            {
                list[i].count += count;
                return;
            }
        }
        list.Add(new DropResult { id = id, count = count });
    }
}
