using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 每日登录奖励配置表（CSV → .bytes，见 daily_login.csv / daily_login_special.csv）。
///
/// 三张逻辑表合一（用 table 列区分）：
///   · starter —— 新手 8 日（按累计登录自然日，断签不重置）
///   · cycle   —— 每日循环 7 日一轮，可无限循环
///   · streak  —— 连击档，day 列填连击天数 3/7/14/30
///   · accum   —— 累计登录领限定佣兵（day 列填所需累计天数，如 8）
/// 另有一张节日覆盖表 daily_login_special.csv，仅覆盖「新手格」当天奖励（见 TryGetSpecialForToday）。
///
/// 范式照抄 BossStageVariantTable：缺表 / 空表 / 该行缺失 时各 TryGet 返回 false，
/// 调用方 DailyLoginDefs 回退到硬编码数组，行为与改动前完全一致（"缺表回退，不让登录奖励开不起来"）。
/// 编辑器下 GameTableStore.LoadText 会回退读源 CSV（Assets/Data/Source/Tables/），免 Cook。
/// </summary>
public static class DailyLoginTable
{
    /// <summary>一行奖励配置（主表与节日表通用；主表用 table/day，节日表用 date）。</summary>
    public class Row
    {
        public string id = "";
        public string table = "";     // starter / cycle / streak（仅主表用）
        public int day = 0;           // 主表内第几天；streak 行填连击天数 3/7/14/30
        public string date = "";      // 节日表：MM-DD（年年生效）或 YYYY-MM-DD（仅该年）
        public string name = "";      // 主奖励显示名
        public string grant = "";     // resource / merc / mercfrag / legendfrag / none
        public string param = "";     // grant=resource 时填资源名；merc/mercfrag 时填 hireId；其它留空
        public int amount = 0;        // 主奖励数量
        public string name2 = "";     // 第二份奖励显示名（组合峰值用，如「钻石 + 传说碎片」）
        public string grant2 = "";
        public string param2 = "";
        public int amount2 = 0;
        public string note = "";
    }

    static readonly List<Row> _starter = new List<Row>();
    static readonly List<Row> _cycle = new List<Row>();
    static readonly List<Row> _streak = new List<Row>();
    static readonly List<Row> _special = new List<Row>();
    static readonly List<Row> _accum = new List<Row>();
    static bool _loaded;

    public static bool HasData => _loaded && (_starter.Count + _cycle.Count + _streak.Count) > 0;

    public static void Reload()
    {
        _loaded = false;
        _starter.Clear();
        _cycle.Clear();
        _streak.Clear();
        _special.Clear();
        _accum.Clear();
        EnsureLoaded();
    }

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        LoadMain(ContentPaths.Data.DailyLogin);
        LoadSpecial(ContentPaths.Data.DailyLoginSpecial);
    }

    /// <summary>解析主表（daily_login.csv）：id,table,day,name,grant,param,amount,name2,grant2,param2,amount2,note。</summary>
    static void LoadMain(string id)
    {
        string raw = GameTableStore.LoadText(id);
        if (string.IsNullOrEmpty(raw)) return;

        var lines = GameTableCsv.ParseRows(raw);
        for (int i = 1; i < lines.Count; i++)   // 第 0 行是表头，跳过
        {
            var c = lines[i];
            if (c.Length < 5) continue;
            string rowId = c[0].Trim();
            if (string.IsNullOrEmpty(rowId) || rowId.StartsWith("#")) continue;

            var r = new Row
            {
                id = rowId,
                table = c.Length > 1 ? c[1].Trim() : "",
                day = GameTableCsv.TryInt(c, 2, 0),
                name = c.Length > 3 ? c[3].Trim() : "",
                grant = c.Length > 4 ? c[4].Trim() : "",
                param = c.Length > 5 ? c[5].Trim() : "",
                amount = GameTableCsv.TryInt(c, 6, 0),
                name2 = c.Length > 7 ? c[7].Trim() : "",
                grant2 = c.Length > 8 ? c[8].Trim() : "",
                param2 = c.Length > 9 ? c[9].Trim() : "",
                amount2 = GameTableCsv.TryInt(c, 10, 0),
                note = c.Length > 11 ? c[11].Trim() : "",
            };

            // 按 table 列分流到对应逻辑表；未知 table 直接忽略（不炸）。
            // accum 段同样按 table 列归入 _accum（大小写不敏感、去空格）。
            string tkey = r.table.Trim().ToLowerInvariant();
            switch (tkey)
            {
                case "starter": _starter.Add(r); break;
                case "cycle": _cycle.Add(r); break;
                case "streak": _streak.Add(r); break;
                case "accum": _accum.Add(r); break;
                default: break;
            }
        }

        // 确保按 day 升序，取模访问时与累计天数对齐
        _starter.Sort((a, b) => a.day.CompareTo(b.day));
        _cycle.Sort((a, b) => a.day.CompareTo(b.day));
        _streak.Sort((a, b) => a.day.CompareTo(b.day));
        _accum.Sort((a, b) => a.day.CompareTo(b.day));
    }

    /// <summary>解析节日表（daily_login_special.csv）：id,date,name,grant,param,amount,name2,grant2,param2,amount2,note。</summary>
    static void LoadSpecial(string id)
    {
        string raw = GameTableStore.LoadText(id);
        if (string.IsNullOrEmpty(raw)) return;

        var lines = GameTableCsv.ParseRows(raw);
        for (int i = 1; i < lines.Count; i++)   // 第 0 行是表头，跳过
        {
            var c = lines[i];
            if (c.Length < 4) continue;
            string rowId = c[0].Trim();
            if (string.IsNullOrEmpty(rowId) || rowId.StartsWith("#")) continue;

            var r = new Row
            {
                id = rowId,
                date = c.Length > 1 ? c[1].Trim() : "",
                name = c.Length > 2 ? c[2].Trim() : "",
                grant = c.Length > 3 ? c[3].Trim() : "",
                param = c.Length > 4 ? c[4].Trim() : "",
                amount = GameTableCsv.TryInt(c, 5, 0),
                name2 = c.Length > 6 ? c[6].Trim() : "",
                grant2 = c.Length > 7 ? c[7].Trim() : "",
                param2 = c.Length > 8 ? c[8].Trim() : "",
                amount2 = GameTableCsv.TryInt(c, 9, 0),
                note = c.Length > 10 ? c[10].Trim() : "",
            };
            if (!string.IsNullOrEmpty(r.date)) _special.Add(r);
        }
    }

    public static int StarterCount => _starter.Count;
    public static int CycleCount => _cycle.Count;
    public static int StreakCount => _streak.Count;

    // accum 段：累计登录领限定佣兵（表 daily_login.csv 的 accum 行）。按 day 升序。
    public static int AccumCount => _accum.Count;
    public static List<Row> AccumRows => _accum;
    /// <summary>按累计天数精确匹配 accum 行（如 day=8）。</summary>
    public static bool TryGetAccum(int day, out Row row)
    {
        row = null;
        for (int i = 0; i < _accum.Count; i++)
            if (_accum[i].day == day) { row = _accum[i]; return true; }
        return false;
    }

    /// <summary>供 DailyLoginDefs 建数组用：streak 行按 day（连击天数）升序。</summary>
    public static List<Row> StreakRows => _streak;

    /// <summary>新手格：day 从 1 起，超出表长按表长取模——与 DailyLoginDefs 原 Starter[(day-1)%Len] 一致。</summary>
    public static bool TryGetStarter(int day, out Row row)
    {
        row = null;
        if (_starter.Count == 0 || day < 1) return false;
        row = _starter[(day - 1) % _starter.Count];
        return true;
    }

    /// <summary>每日循环：day 从 1 起，超出表长按表长取模——与 DailyLoginDefs 原 Cycle[(day-1)%Len] 一致。</summary>
    public static bool TryGetCycle(int day, out Row row)
    {
        row = null;
        if (_cycle.Count == 0 || day < 1) return false;
        row = _cycle[(day - 1) % _cycle.Count];
        return true;
    }

    /// <summary>连击档：按连击天数（days）精确匹配 3/7/14/30。</summary>
    public static bool TryGetStreak(int days, out Row row)
    {
        row = null;
        for (int i = 0; i < _streak.Count; i++)
            if (_streak[i].day == days) { row = _streak[i]; return true; }
        return false;
    }

    /// <summary>
    /// 节日 / 特定日覆盖：按今天日期查节日表。
    /// 命中规则：**先查 YYYY-MM-DD 精确匹配（仅该年），没有再查 MM-DD（年年生效）**。
    /// 命中则返回该天的新手格奖励，由 DailyLoginDefs 整条覆盖到「今天」那一格——
    /// **只覆盖 Starter 新手格**，Cycle / Streak 不覆盖（范围不扩大）。
    /// 时间源用 System.DateTime.Now（项目已在 DailyLoginSystem 用同一本地时间口径）。
    /// </summary>
    public static bool TryGetSpecialForToday(out Row row)
    {
        row = null;
        if (_special.Count == 0) return false;

        var today = System.DateTime.Now;
        string ymd = today.ToString("yyyy-MM-dd");
        string md = today.ToString("MM-dd");

        for (int i = 0; i < _special.Count; i++)
            if (_special[i].date == ymd) { row = _special[i]; return true; }
        for (int i = 0; i < _special.Count; i++)
            if (_special[i].date == md) { row = _special[i]; return true; }
        return false;
    }
}
