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
///
/// 2026-09-26 主人拍板（掉落按怪物类型区分，V1 结构版）：
///   第 10 列 monsterType（可选，空=any）：physical / magic / any。
///   带类型的行只在「本关主导怪物类型」匹配时才参与掷骰；空/any 行对所有关都生效（旧行为不变）。
///   主导类型由 BattleManager 按本关击杀的物理/魔法怪数量推导，透传到 RollDrops(monsterTypeBias)。
///   注：装备(RiftEquipGenerator)本身无物理/魔法向字段，故本版只在掉落池（道具/徽记）层按类型分池，
///   具体 physical/magic 各掉什么由主人在 csv 里加带类型的行决定（本版不写死任何掉落物）。
/// </summary>
public static class StageDropTable
{
    public class DropResult
    {
        public DropType type;
        public string id;
        public int count;
    }

    public enum DropType { Badge, Fragment, Item }

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
        /// <summary>
        /// 2026-09-26 主人拍板：掉落按怪物主导类型分池。
        /// "" = 通配（任何类型都参与，旧行行为不变）；physical / magic = 仅该类型参与；any = 显式通配。
        /// </summary>
        public string monsterType = "";
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
                firstClearBonus = GameTableCsv.TryInt(c[8], out int fb) ? fb : 0,
                monsterType = c.Length > 9 ? c[9].Trim() : ""
            };
            r.type = c[2].Trim().Equals("Fragment", StringComparison.OrdinalIgnoreCase)
                ? DropType.Fragment
                : c[2].Trim().Equals("Item", StringComparison.OrdinalIgnoreCase)
                ? DropType.Item
                : DropType.Badge;
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
    /// 掷一次关卡掉落（徽记 / 本命碎片 / 道具）。firstClear 传 true 时徽记额外给保底。
    /// 返回的 DropResult.type 区分三类；id 已是存档口径：
    ///   Badge  → badge:{职业}:{档位}（MercGrowInventory.Add）
    ///   Fragment → frag:{hireId}（按职业随机抽取本命碎片归属的佣兵）
    ///   Item   → itemId（jobKey 列复用为 itemId，喂背包系统）
    /// </summary>
    public static List<DropResult> RollDrops(int gameChapter, string stageType, bool firstClear)
    {
        return RollDrops(gameChapter, stageType, firstClear, null);
    }

    /// <summary>
    /// 掷一次关卡掉落。monsterTypeBias 为本关主导怪物类型（null/空=不限制，按旧行为全生效）；
    /// 带 monsterType 的行仅在该类型匹配时参与掷骰。其余逻辑同旧版。
    /// </summary>
    public static List<DropResult> RollDrops(int gameChapter, string stageType, bool firstClear, string monsterTypeBias)
    {
        var res = new List<DropResult>();
        EnsureLoaded();
        var pool = MainPool(gameChapter);
        var rng = new System.Random();
        int bonus = 0;

        for (int i = 0; i < _rows.Count; i++)
        {
            var r = _rows[i];
            if (r.chapter != 0 && r.chapter != gameChapter) continue;
            if (!string.IsNullOrEmpty(r.stageType) &&
                !r.stageType.Equals(stageType, StringComparison.OrdinalIgnoreCase)) continue;
            // 2026-09-26 主人拍板：按怪物类型分池——带类型的行只在主导类型匹配时生效
            if (!string.IsNullOrEmpty(r.monsterType) &&
                !r.monsterType.Equals("any", StringComparison.OrdinalIgnoreCase) &&
                (monsterTypeBias == null ||
                 !r.monsterType.Equals(monsterTypeBias, StringComparison.OrdinalIgnoreCase)))
                continue;

            // 保底只针对徽记（与旧逻辑一致）
            if (r.type == DropType.Badge && firstClear && r.firstClearBonus > bonus) bonus = r.firstClearBonus;

            if (r.rate <= 0f) continue;
            if (rng.NextDouble() >= r.rate) continue;

            AddRolled(res, r, pool, rng);
        }

        // 保底：本章首次通关额外给 N 个徽记（职业/档位按主产随机）
        for (int i = 0; i < bonus; i++)
        {
            if (pool.Count <= 0) break;
            var pick = pool[rng.Next(pool.Count)];
            Add(res, DropType.Badge, MercGrowInventory.BadgeId(pick.jobKey, pick.tier), 1);
        }

        return res;
    }

    /// <summary>徽记专用入口（兼容旧调用）；只返回 Badge 类结果。</summary>
    public static List<DropResult> RollBadges(int gameChapter, string stageType, bool firstClear)
    {
        var all = RollDrops(gameChapter, stageType, firstClear);
        var badges = new List<DropResult>();
        for (int i = 0; i < all.Count; i++)
            if (all[i].type == DropType.Badge) badges.Add(all[i]);
        return badges;
    }

    static void AddRolled(List<DropResult> res, Row r, List<Row> pool, System.Random rng)
    {
        int n = r.countMax > r.countMin ? rng.Next(r.countMin, r.countMax + 1) : r.countMin;
        if (n <= 0) return;

        if (r.type == DropType.Badge)
        {
            string job = r.jobKey;
            string tier = r.tier;
            if (string.IsNullOrEmpty(job) || string.IsNullOrEmpty(tier))
            {
                if (pool.Count <= 0) return;
                var pick = pool[rng.Next(pool.Count)];
                job = pick.jobKey;
                tier = pick.tier;
            }
            Add(res, DropType.Badge, MercGrowInventory.BadgeId(job, tier), n);
        }
        else if (r.type == DropType.Fragment)
        {
            string hireId = ResolveFragmentHireId(r.jobKey, pool, rng);
            if (string.IsNullOrEmpty(hireId)) return;
            Add(res, DropType.Fragment, MercGrowInventory.FragmentId(hireId), n);
        }
        else // Item：jobKey 列复用为 itemId
        {
            if (string.IsNullOrEmpty(r.jobKey)) return;
            Add(res, DropType.Item, r.jobKey, n);
        }
    }

    /// <summary>本命碎片按职业随机归属到某个佣兵（jobKey 为 csv 短职业名，如 剑盾/狂战）。</summary>
    static string ResolveFragmentHireId(string shortJob, List<Row> pool, System.Random rng)
    {
        string jobName = MapShortJob(shortJob);
        var hireIds = new List<string>();
        var all = MercRosterDefs.All;
        for (int i = 0; i < all.Count; i++)
            if (all[i].JobName == jobName) hireIds.Add(all[i].HireId);

        if (hireIds.Count == 0 && pool.Count > 0)
        {
            // 通配：从本章主产徽记池随机抽一个职业再取该职业佣兵
            var pick = pool[rng.Next(pool.Count)];
            jobName = MapShortJob(pick.jobKey);
            for (int i = 0; i < all.Count; i++)
                if (all[i].JobName == jobName) hireIds.Add(all[i].HireId);
        }
        if (hireIds.Count == 0) return null;
        return hireIds[rng.Next(hireIds.Count)];
    }

    /// <summary>csv 短职业名 → 花名册 JobName 口径。</summary>
    static string MapShortJob(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        switch (s.Trim())
        {
            case "剑盾": return "剑盾卫士";
            case "狂战": return "狂战士";
            case "游侠": return "游侠";
            case "法师": return "法师";
            case "牧师": return "牧师";
            case "重武": return "重武者";
            default: return s.Trim();
        }
    }

    static void Add(List<DropResult> list, DropType type, string id, int count)
    {
        if (string.IsNullOrEmpty(id) || count <= 0) return;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].type == type && list[i].id == id)
            {
                list[i].count += count;
                return;
            }
        }
        list.Add(new DropResult { type = type, id = id, count = count });
    }
}
