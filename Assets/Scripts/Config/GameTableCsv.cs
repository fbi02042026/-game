using System.Collections.Generic;

/// <summary>表 CSV 轻量解析（跳过 # 注释与空行）。</summary>
public static class GameTableCsv
{
    public static List<string[]> ParseRows(string raw)
    {
        var rows = new List<string[]>();
        if (string.IsNullOrEmpty(raw)) return rows;

        string[] lines = raw.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
            rows.Add(SplitCsvLine(line));
        }
        return rows;
    }

    static string[] SplitCsvLine(string line)
    {
        var cols = new List<string>();
        int start = 0;
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }
            if (c == ',' && !inQuotes)
            {
                cols.Add(line.Substring(start, i - start).Trim());
                start = i + 1;
            }
        }
        cols.Add(line.Substring(start).Trim());
        return cols.ToArray();
    }

    public static bool IsWildcard(string s) =>
        string.IsNullOrEmpty(s) || s == "*" || s == "-";

    public static bool TryInt(string s, out int v)
    {
        v = 0;
        if (IsWildcard(s)) return false;
        return int.TryParse(s.Trim(), out v);
    }

    public static bool TryFloat(string s, out float v)
    {
        v = 0f;
        if (IsWildcard(s)) return false;
        return float.TryParse(s.Trim(), out v);
    }

    public static bool TryBool(string s, out bool v)
    {
        v = false;
        if (IsWildcard(s)) return false;
        s = s.Trim().ToLowerInvariant();
        if (s == "1" || s == "true" || s == "yes") { v = true; return true; }
        if (s == "0" || s == "false" || s == "no") { v = false; return true; }
        return false;
    }
}
