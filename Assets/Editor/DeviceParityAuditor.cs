using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 真机对齐审计（2026-09-28 新增，对应「先做 A」）。
///
/// 解决的问题：项目里有一批资源只存在于美术源目录（Assets/Art/…），Resources 下没有副本。
/// 编辑器过去会回退 AssetDatabase 直读美术源，所以「编辑器一切正常、真机图空或旧图」，
/// 只有打包上真机才暴露 —— 太晚。
///
/// 本工具做三件事，全部只读、不改任何资源：
///   ① 扫出代码里所有「纯字面量」的 Resources.Load 路径，逐个检查 Assets/Resources 下是否真有对应资源。
///      不存在的 = 真机必空，直接列出来。
///   ② 扫出所有 #if UNITY_EDITOR 资源分支里的美术源路径，标注该分支是否已被 DeviceParity 收口。
///   ③ 把结果同时打到 Console 和 Docs/ 下的报告文件。
///
/// 菜单：Tools/真机对齐审计/扫描 Resources 缺失（并输出报告）
/// </summary>
public static class DeviceParityAuditor
{
    const string ScriptsDir = "Assets/Scripts";
    const string ResourcesDir = "Assets/Resources";

    /// <summary>Resources 下资源可能的扩展名（判定「有没有这份副本」用）。</summary>
    static readonly string[] Exts =
    {
        ".png", ".jpg", ".jpeg", ".tga", ".psd",
        ".asset", ".prefab", ".mat", ".anim", ".controller",
        ".ttf", ".otf", ".bytes", ".csv", ".json", ".txt",
        ".mp3", ".wav", ".ogg",
    };

    /// <summary>Resources.Load&lt;T&gt;("纯字面量") —— 括号内只有那一个字符串字面量。</summary>
    static readonly Regex ReResLiteral = new Regex(
        @"Resources\.(?:Load|LoadAll)<[^>]*>\(\s*""([^""\r\n]+)""\s*\)", RegexOptions.Compiled);

    /// <summary>AssetDatabase.LoadAssetAtPath/FindAssets 里出现的 "Assets/…" 字面量。</summary>
    static readonly Regex ReEditorAssetLiteral = new Regex(
        @"""((?:Assets/|Assets\\)[^""\r\n]*)""", RegexOptions.Compiled);

    sealed class Site
    {
        public string File;
        public int Line;
        public string Literal;
    }

    sealed class EditorSite
    {
        public string File;
        public int Line;          // #if 行
        public bool Gated;        // 该块内是否出现 DeviceParity.EditorFallbackEnabled
        public List<string> Paths = new List<string>();
    }

    [MenuItem("Tools/真机对齐审计/扫描 Resources 缺失（并输出报告）")]
    public static void Run()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string scriptsAbs = Path.Combine(projectRoot, ScriptsDir.Replace('/', Path.DirectorySeparatorChar));

        if (!Directory.Exists(scriptsAbs))
        {
            EditorUtility.DisplayDialog("真机对齐审计", "找不到 " + ScriptsDir, "好");
            return;
        }

        var missing = new List<Site>();
        var unGated = new List<EditorSite>();
        var allEditorSites = new List<EditorSite>();
        var checkedPaths = new List<string>();

        string[] csFiles = Directory.GetFiles(scriptsAbs, "*.cs", SearchOption.AllDirectories);
        for (int i = 0; i < csFiles.Length; i++)
        {
            string text;
            try { text = File.ReadAllText(csFiles[i]); }
            catch (Exception) { continue; }

            string relFile = "Assets" + csFiles[i]
                .Substring(Application.dataPath.Length)
                .Replace('\\', '/');

            // ① Resources 字面量路径是否存在
            var seen = new HashSet<string>();
            foreach (Match m in ReResLiteral.Matches(text))
            {
                string lit = m.Groups[1].Value;
                if (!seen.Add(lit)) continue;
                if (checkedPaths.Contains(lit)) continue;
                checkedPaths.Add(lit);
                if (ResolveExists(lit)) continue;
                missing.Add(new Site
                {
                    File = relFile,
                    Line = LineOf(text, m.Index),
                    Literal = lit,
                });
            }

            // ② #if UNITY_EDITOR 资源分支
            var found = CollectEditorSites(text, relFile);
            for (int k = 0; k < found.Count; k++)
            {
                allEditorSites.Add(found[k]);
                if (found[k].Paths.Count > 0 && !found[k].Gated) unGated.Add(found[k]);
            }
        }

        // ③ 输出报告
        var sb = new StringBuilder();
        sb.AppendLine("真机对齐审计报告    " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine("========================================================");
        sb.AppendLine();
        sb.AppendLine("【一】真机必空的 Resources 路径（代码里写死的字面量，Resources 下找不到）");
        sb.AppendLine("      共 " + missing.Count + " 条 / 检查了 " + checkedPaths.Count + " 个不同路径");
        sb.AppendLine();
        if (missing.Count == 0)
        {
            sb.AppendLine("      ✅ 没有。所有字面量 Resources 路径都能找到对应资源。");
        }
        else
        {
            for (int i = 0; i < missing.Count; i++)
            {
                var s = missing[i];
                sb.AppendLine("      ✗ " + s.Literal);
                sb.AppendLine("         ← " + s.File + ":" + s.Line);
            }
        }
        sb.AppendLine();
        sb.AppendLine("【二】#if UNITY_EDITOR 里还指着美术源目录、且【未】被 DeviceParity 收口的分支");
        sb.AppendLine("      （收口后编辑器只走 Resources；这里列的是漏网的，需要人工看要不要一起收）");
        sb.AppendLine("      共 " + unGated.Count + " 处 / 资源类编辑器分支共 " + allEditorSites.Count + " 处");
        sb.AppendLine();
        if (unGated.Count == 0)
        {
            sb.AppendLine("      ✅ 没有。资源类编辑器分支已全部收口。");
        }
        else
        {
            for (int i = 0; i < unGated.Count; i++)
            {
                var s = unGated[i];
                sb.AppendLine("      · " + s.File + ":" + s.Line);
                for (int k = 0; k < s.Paths.Count; k++)
                    sb.AppendLine("          " + s.Paths[k]);
            }
        }
        sb.AppendLine();
        sb.AppendLine("【三】已收口的资源类编辑器分支（参考，共 "
                      + (allEditorSites.Count - unGated.Count) + " 处）");
        sb.AppendLine();
        for (int i = 0; i < allEditorSites.Count; i++)
        {
            var s = allEditorSites[i];
            if (!s.Gated) continue;
            sb.AppendLine("      · " + s.File + ":" + s.Line + "  (" + s.Paths.Count + " 个美术路径)");
        }
        sb.AppendLine();
        sb.AppendLine("========================================================");
        sb.AppendLine("说明：本工具只读，不改任何资源。");
        sb.AppendLine("      「编辑器是否走 Resources」由 DeviceParity.EditorFallbackEnabled 统一控制，");
        sb.AppendLine("      当前值 = " + DeviceParity.EditorFallbackEnabled
                      + "（false = 真机口径，缺图露白框；true = 旧兜底行为）。");

        string outDir = Path.Combine(projectRoot, "Docs");
        string outFile = Path.Combine(outDir, "真机对齐审计.txt");
        try
        {
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
            File.WriteAllText(outFile, sb.ToString(), new UTF8Encoding(false));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[真机对齐审计] 写报告失败: " + e.Message);
        }

        Debug.Log("[真机对齐审计] 报告 → " + outFile + "\n" + sb);
        EditorUtility.DisplayDialog(
            "真机对齐审计",
            "真机必空路径: " + missing.Count + " 条\n"
            + "未收口的编辑器分支: " + unGated.Count + " 处\n\n"
            + "报告已写到 Docs/真机对齐审计.txt，明细见 Console。",
            "好");
    }

    /// <summary>Resources 相对路径（如 UI/JobSelect/剑盾）在 Assets/Resources 下是否真有资源。</summary>
    static bool ResolveExists(string relPath)
    {
        if (string.IsNullOrEmpty(relPath)) return false;
        relPath = relPath.Replace('\\', '/').Trim('/');
        string assetPath = ResourcesDir + "/" + relPath;

        if (AssetDatabase.IsValidFolder(assetPath)) return true;
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null) return true;

        string dir = Path.GetDirectoryName(assetPath);
        if (!string.IsNullOrEmpty(dir)) dir = dir.Replace('\\', '/');
        string name = Path.GetFileName(assetPath);
        for (int i = 0; i < Exts.Length; i++)
        {
            if (File.Exists(assetPath + Exts[i])) return true;
            if (!string.IsNullOrEmpty(dir) && File.Exists(dir + "/" + name + Exts[i])) return true;
        }
        return false;
    }

    /// <summary>取字符偏移量所在行号（1 起）。</summary>
    static int LineOf(string text, int offset)
    {
        int line = 1;
        int max = Math.Min(offset, text.Length);
        for (int i = 0; i < max; i++)
            if (text[i] == '\n') line++;
        return line;
    }

    /// <summary>抽出所有 #if UNITY_EDITOR 块里出现过的 Assets/… 路径字面量。</summary>
    static List<EditorSite> CollectEditorSites(string text, string relFile)
    {
        var list = new List<EditorSite>();
        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        int i = 0;
        while (i < lines.Length)
        {
            string st = lines[i].TrimStart();
            if (st.StartsWith("#if") && st.Contains("UNITY_EDITOR"))
            {
                int depth = 1;
                int j = i + 1;
                var body = new StringBuilder();
                while (j < lines.Length && depth > 0)
                {
                    string s2 = lines[j].TrimStart();
                    if (s2.StartsWith("#if")) depth++;
                    else if (s2.StartsWith("#endif"))
                    {
                        depth--;
                        if (depth == 0) break;
                    }
                    body.AppendLine(lines[j]);
                    j++;
                }

                string bodyText = body.ToString();
                var site = new EditorSite { File = relFile, Line = i + 1 };
                site.Gated = bodyText.Contains("DeviceParity.EditorFallbackEnabled");
                var paths = new HashSet<string>();
                foreach (Match m in ReEditorAssetLiteral.Matches(bodyText))
                {
                    string p = m.Groups[1].Value;
                    if (paths.Add(p)) site.Paths.Add(p);
                }
                if (site.Paths.Count > 0)
                    list.Add(site);
                i = j;
            }
            i++;
        }
        return list;
    }
}
