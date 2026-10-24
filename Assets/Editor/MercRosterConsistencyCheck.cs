using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 花名册「三处同值」校验（2026-10-23 主人拍板）。
/// <para>同一批数值被抄在三处：源码表 CSV、cooked .bytes、以及 MercRosterDefs.cs 里的硬编码数组。
/// 只要有一处漏改，运行时「表优先」会用 cooked 值、而读代码的人看到的是另一套，排查起来极难受。
/// 本工具把三处摊开逐字段比对，不一致直接 LogError 指出是哪一行哪一列。</para>
/// 用法：菜单 Tools → 佣兵 → 花名册三处同值校验。改完花名册数值记得跑一次。
/// </summary>
public static class MercRosterConsistencyCheck
{
    const string SrcCsv = "Assets/Data/Source/Tables/merc_roster.csv";
    const string CookedBytes = "Assets/Resources/Data/Tables/merc_roster.bytes";
    const string CodeFile = "Assets/Scripts/Config/MercRosterDefs.cs";

    // 与 MercRosterDefs.LoadTableFromCsv 的列序一一对应，改表结构时两处一起改
    const int ColHireId = 0, ColName = 1, ColNick = 2, ColAsset = 3, ColJob = 4, ColRarity = 5;
    const int ColGrowth = 6, ColHp = 7, ColAtk = 8, ColDef = 9, ColAtkSpd = 10, ColMove = 11;
    const int ColActive = 12, ColPassive = 13, ColGold = 14, ColInitial = 15, ColShop = 16;

    class Row
    {
        public string HireId, Name, Nick, Asset, Job, Active, Passive;
        public int Rarity, Gold;
        public float Growth, Hp, Atk, Def, AtkSpd, Move;
        public bool Initial, Shop;
        public string Source;   // csv / bytes / code
        public int LineNo;
    }

    [MenuItem("Tools/佣兵/花名册三处同值校验")]
    public static void Run()
    {
        var csv = LoadFromTable(SrcCsv, "源码CSV");
        var bytes = LoadFromTable(CookedBytes, "cooked .bytes");
        var code = LoadFromCode();

        int problems = 0;
        problems += CheckRowsExist("源码CSV", csv, "cooked .bytes", bytes);
        problems += CheckRowsExist("源码CSV", csv, "硬编码数组", code);
        problems += Compare("源码CSV", csv, "cooked .bytes", bytes);
        problems += Compare("源码CSV", csv, "硬编码数组", code);

        if (problems == 0)
            Debug.Log($"[花名册校验] 三处完全一致 ✓（{csv.Count} 名佣兵：源码CSV / .bytes / 硬编码数组）");
        else
            Debug.LogError($"[花名册校验] 发现 {problems} 处不一致，见上方逐条 —— 数值真源是 {SrcCsv}，"
                + "改完记得重新 Cook 生成 .bytes，并同步 MercRosterDefs.cs 的硬编码数组（三处必须同值）。");
    }

    // ============ 载入 ============

    static List<Row> LoadFromTable(string path, string tag)
    {
        var list = new List<Row>();
        if (!File.Exists(path))
        {
            Debug.LogError($"[花名册校验] 找不到 {tag}：{path}");
            return list;
        }
        string[] lines = File.ReadAllText(path).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
            string[] c = line.Split(',');
            // 表头行跳过：必须严格匹配 H+数字（表头列名叫 "HireId"，也会被 StartsWith("H") 命中 → 会误报）
            if (c.Length < 12 || !Regex.IsMatch(c[0].Trim(), @"^H\d+$")) continue;
            list.Add(new Row
            {
                HireId = Get(c, ColHireId), Name = Get(c, ColName), Nick = Get(c, ColNick),
                Asset = Get(c, ColAsset), Job = Get(c, ColJob),
                Rarity = AsInt(Get(c, ColRarity)), Gold = AsInt(Get(c, ColGold)),
                Growth = AsFloat(Get(c, ColGrowth)), Hp = AsFloat(Get(c, ColHp)), Atk = AsFloat(Get(c, ColAtk)),
                Def = AsFloat(Get(c, ColDef)), AtkSpd = AsFloat(Get(c, ColAtkSpd)), Move = AsFloat(Get(c, ColMove)),
                Active = Get(c, ColActive), Passive = Get(c, ColPassive),
                Initial = AsBool(Get(c, ColInitial)), Shop = AsBool(Get(c, ColShop)),
                Source = tag, LineNo = i + 1
            });
        }
        return list;
    }

    /// <summary>从 MercRosterDefs.cs 里的 D(...) 硬编码行反解出同一套字段（不运行游戏逻辑，纯文本解析）。</summary>
    static List<Row> LoadFromCode()
    {
        var list = new List<Row>();
        if (!File.Exists(CodeFile))
        {
            Debug.LogError($"[花名册校验] 找不到硬编码数组源文件：{CodeFile}");
            return list;
        }
        string[] lines = File.ReadAllText(CodeFile).Split(new[] { '\r', '\n' }, StringSplitOptions.None);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            int d = line.IndexOf("D(", StringComparison.Ordinal);
            if (d < 0) continue;
            int open = line.IndexOf('(', d);
            if (open < 0) continue;
            int depth = 0, close = -1;
            for (int j = open; j < line.Length; j++)
            {
                if (line[j] == '(') depth++;
                else if (line[j] == ')') { depth--; if (depth == 0) { close = j; break; } }
            }
            if (close < 0) continue;

            string[] p = SplitArgs(line.Substring(open + 1, close - open - 1));
            if (p.Length < 16 || !Regex.IsMatch(Trim(p[0]), @"^H\d+$")) continue;

            list.Add(new Row
            {
                HireId = Trim(p[0]), Name = Trim(p[1]), Nick = Trim(p[2]), Asset = Trim(p[3]), Job = Trim(p[4]),
                Rarity = RarityOf(Trim(p[5])),
                Growth = AsFloat(Trim(p[6])), Hp = AsFloat(Trim(p[7])), Atk = AsFloat(Trim(p[8])),
                Def = AsFloat(Trim(p[9])), AtkSpd = AsFloat(Trim(p[10])), Move = AsFloat(Trim(p[11])),
                Active = Trim(p[12]), Passive = Trim(p[13]),
                Gold = AsInt(Trim(p[14])), Initial = AsBool(Trim(p[15])),
                Shop = p.Length > 16 ? AsBool(Trim(p[16])) : false,
                Source = "硬编码数组", LineNo = i + 1
            });
        }
        return list;
    }

    /// <summary>按顶层逗号切参数（本表参数里不含逗号，简单切即可）。</summary>
    static string[] SplitArgs(string s) => s.Split(',');

    // ============ 比对 ============

    static int CheckRowsExist(string tagA, List<Row> a, string tagB, List<Row> b)
    {
        int bad = 0;
        var ids = new HashSet<string>();
        foreach (var r in b) ids.Add(r.HireId);
        foreach (var r in a)
            if (!ids.Contains(r.HireId))
            {
                Debug.LogError($"[花名册校验] {r.HireId}（{r.Name}）只存在于 {tagA} 第 {r.LineNo} 行，{tagB} 里没有 —— 漏同步了");
                bad++;
            }
        return bad;
    }

    static int Compare(string tagA, List<Row> a, string tagB, List<Row> b)
    {
        int bad = 0;
        var map = new Dictionary<string, Row>();
        foreach (var r in b) map[r.HireId] = r;

        foreach (var x in a)
        {
            if (!map.TryGetValue(x.HireId, out var y)) continue;   // 缺失已在上一步报过
            bad += Diff(tagA, tagB, x, y, "名字", x.Name, y.Name);
            bad += Diff(tagA, tagB, x, y, "昵称", x.Nick, y.Nick);
            bad += Diff(tagA, tagB, x, y, "AssetId", x.Asset, y.Asset);
            bad += Diff(tagA, tagB, x, y, "职业", x.Job, y.Job);
            bad += Diff(tagA, tagB, x, y, "稀有度", x.Rarity, y.Rarity);
            bad += Diff(tagA, tagB, x, y, "成长", x.Growth, y.Growth);
            bad += Diff(tagA, tagB, x, y, "基础生命", x.Hp, y.Hp);
            bad += Diff(tagA, tagB, x, y, "基础攻击", x.Atk, y.Atk);
            bad += Diff(tagA, tagB, x, y, "基础防御", x.Def, y.Def);
            bad += Diff(tagA, tagB, x, y, "攻速", x.AtkSpd, y.AtkSpd);
            bad += Diff(tagA, tagB, x, y, "移速", x.Move, y.Move);
            bad += Diff(tagA, tagB, x, y, "主动技", x.Active, y.Active);
            bad += Diff(tagA, tagB, x, y, "被动技", x.Passive, y.Passive);
            bad += Diff(tagA, tagB, x, y, "招募金", x.Gold, y.Gold);
            bad += Diff(tagA, tagB, x, y, "初始池", x.Initial, y.Initial);
            bad += Diff(tagA, tagB, x, y, "商店售卖", x.Shop, y.Shop);
        }
        return bad;
    }

    static int Diff(string tagA, string tagB, Row x, Row y, string field, string va, string vb)
    {
        if (string.Equals(va ?? "", vb ?? "", StringComparison.Ordinal)) return 0;
        Debug.LogError($"[花名册校验] {x.HireId}（{x.Name}）的「{field}」不一致：{tagA}={Show(va)}（第 {x.LineNo} 行） vs {tagB}={Show(vb)}（第 {y.LineNo} 行）");
        return 1;
    }

    static int Diff(string tagA, string tagB, Row x, Row y, string field, int va, int vb)
    {
        if (va == vb) return 0;
        Debug.LogError($"[花名册校验] {x.HireId}（{x.Name}）的「{field}」不一致：{tagA}={va}（第 {x.LineNo} 行） vs {tagB}={vb}（第 {y.LineNo} 行）");
        return 1;
    }

    static int Diff(string tagA, string tagB, Row x, Row y, string field, bool va, bool vb)
    {
        if (va == vb) return 0;
        Debug.LogError($"[花名册校验] {x.HireId}（{x.Name}）的「{field}」不一致：{tagA}={va}（第 {x.LineNo} 行） vs {tagB}={vb}（第 {y.LineNo} 行）");
        return 1;
    }

    static int Diff(string tagA, string tagB, Row x, Row y, string field, float va, float vb)
    {
        if (Math.Abs(va - vb) < 0.001f) return 0;
        Debug.LogError($"[花名册校验] {x.HireId}（{x.Name}）的「{field}」不一致：{tagA}={va}（第 {x.LineNo} 行） vs {tagB}={vb}（第 {y.LineNo} 行）");
        return 1;
    }

    // ============ 小工具 ============

    static string Show(string s) => string.IsNullOrEmpty(s) ? "(空)" : s;

    static string Get(string[] c, int i) => i >= 0 && i < c.Length ? c[i].Trim() : "";

    static string Trim(string s)
    {
        s = (s ?? "").Trim().Trim('"');
        if (s.EndsWith("f", StringComparison.Ordinal) && s.Length > 1) s = s.Substring(0, s.Length - 1);
        if (string.Equals(s, "null", StringComparison.Ordinal)) return "";
        return s;
    }

    static int AsInt(string s) { int v; return int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v) ? v : 0; }

    static float AsFloat(string s) { float v; return float.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v) ? v : 0f; }

    static bool AsBool(string s)
    {
        s = (s ?? "").Trim().ToLowerInvariant();
        return s == "true" || s == "1" || s == "yes";
    }

    static int RarityOf(string s)
    {
        if (s.IndexOf("Legendary", StringComparison.OrdinalIgnoreCase) >= 0) return 2;
        if (s.IndexOf("Rare", StringComparison.OrdinalIgnoreCase) >= 0) return 1;
        return 0;
    }
}
