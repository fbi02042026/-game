// UI 图片引用丢失守卫（引擎内报错版）
//
// 为什么要这个脚本（主人 2026-09-30 拍板：不要兜底，要报错）：
//   prefab 里 Image 的 m_Sprite 指向的贴图一旦丢失（典型原因：团结重新导入时把 .meta 的
//   guid 写成 56 字符长串），引擎**不会报任何错**，Image 只是静默退化成纯色矩形，
//   打包也不中断 —— 肉眼根本看不出来。这正是项目纪律第 12 条要顶掉的那种静默兜底：
//   取不到资源就 LogError，不许悄悄降级。
//
// 判定口径（与 Tools/CheckDanglingRefs.py 保持一致，零误报）：
//   只报「写了引用但解析不出资源」的槽位；本来就没图的（m_Sprite: {fileID: 0}）不算。
//   guid 形态非法的（例如 56 字符）同样报错 —— 规范只认 hex32。
//
// 只读：本脚本不修改任何资源、不删任何文件。
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class UiSpriteRefGuard
{
    const string MENU = "Tools/诊断/UI 图片引用丢失检查（LogError）";
    const string MENU_ALL = "Tools/诊断/UI 图片引用丢失检查（含第三方素材包）";
    const string MENU_AUTO = "Tools/诊断/UI 图片引用丢失：保存时自动检查";
    const string PREF_AUTO = "PixelAdventureTown.UiSpriteRefGuard.Auto";   // 默认开
    const int MAX_REPORT = 40;   // 防刷屏：最多列这么多条，多出来的只报总数

    // 自家 UI 目录。第三方素材包（SPUM / RPG Props / Monster Pack 等）的 demo 与 preview
    // 本身就有大量引用指向随包没导入的图，混进来会把真问题淹掉 —— 默认不看它们，
    // 要看走 MENU_ALL。
    static readonly string[] OwnDirs =
    {
        "Assets/Resources/Prefabs",
        "Assets/Scenes",
    };

    // m_Sprite: {fileID: 21300000, guid: xxxx, type: 3}
    static readonly Regex SpriteRef = new Regex(
        @"m_Sprite:\s*\{fileID:\s*(-?\d+),\s*guid:\s*([^,\s}]+),\s*type:\s*\d+\}",
        RegexOptions.Compiled);
    static readonly Regex Hex32 = new Regex(@"^[0-9a-fA-F]{32}$", RegexOptions.Compiled);
    static readonly Regex NameLine = new Regex(@"^\s*m_Name:\s*(.*)$", RegexOptions.Compiled);

    static HashSet<string> _allow;

    /// <summary>豁免清单（与 Python 工具共用 Tools/dangling_allowlist.txt）。</summary>
    static HashSet<string> AllowList()
    {
        if (_allow != null) return _allow;
        _allow = new HashSet<string>();
        try
        {
            string p = Path.Combine(Application.dataPath, "../Tools/dangling_allowlist.txt");
            if (File.Exists(p))
            {
                foreach (var raw in File.ReadAllLines(p))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    string[] seg = line.Split('|');
                    string g = seg[0].Trim();
                    if (g.Length == 32) _allow.Add(g.ToLower());
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[UiSpriteRefGuard] 读豁免清单失败: " + e.Message);
        }
        return _allow;
    }

    /// <summary>保存/导入 prefab、scene 时是否自动检查。git pull 后批量导入嫌吵就关掉。</summary>
    public static bool AutoEnabled
    {
        get { return EditorPrefs.GetBool(PREF_AUTO, true); }
        set { EditorPrefs.SetBool(PREF_AUTO, value); }
    }

    [MenuItem(MENU_AUTO)]
    public static void ToggleAuto()
    {
        AutoEnabled = !AutoEnabled;
        Menu.SetChecked(MENU_AUTO, AutoEnabled);
        Debug.Log("[UiSpriteRefGuard] 保存时自动检查: " + (AutoEnabled ? "开" : "关"));
    }

    [MenuItem(MENU_AUTO, true)]
    public static bool ToggleAutoValidate()
    {
        Menu.SetChecked(MENU_AUTO, AutoEnabled);
        return true;
    }

    [MenuItem(MENU)]
    public static void CheckAll()
    {
        Run(OwnDirs, "自家 UI（Resources/Prefabs + Scenes）");
    }

    [MenuItem(MENU_ALL)]
    public static void CheckAllIncludingThirdParty()
    {
        Run(new[] { "Assets" }, "全 Assets（含第三方素材包，可能会有大量素材包自带的噪音）");
    }

    static void Run(string[] dirsUnderAssets, string scopeName)
    {
        var files = new List<string>();
        foreach (var d in dirsUnderAssets)
        {
            string abs = Path.Combine(Application.dataPath, "..", d).Replace('\\', '/');
            if (!Directory.Exists(abs)) continue;
            files.AddRange(Directory.GetFiles(abs, "*.prefab", SearchOption.AllDirectories));
            files.AddRange(Directory.GetFiles(abs, "*.unity", SearchOption.AllDirectories));
        }
        files.Sort(StringComparer.Ordinal);

        var bad = new List<string>();
        foreach (var f in files) Collect(f, bad);

        if (bad.Count == 0)
        {
            Debug.Log("[UiSpriteRefGuard] ✅ 没发现图片引用丢失。范围: " + scopeName
                      + "，扫描 prefab/scene: " + files.Count + " 个。");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("[UiSpriteRefGuard] 🔴 发现 " + bad.Count + " 处图片引用丢失（UI 上会退化成纯色块，引擎默认不报错）：");
        for (int i = 0; i < bad.Count && i < MAX_REPORT; i++) sb.AppendLine("  " + bad[i]);
        if (bad.Count > MAX_REPORT)
            sb.AppendLine("  ...还有 " + (bad.Count - MAX_REPORT) + " 条，修完这批再跑一次。");
        sb.AppendLine("修法：把该槽位指向的贴图 .meta 的 guid 改回合法 hex32，或在编辑器里重新拖图。改完重启引擎。");
        Debug.LogError(sb.ToString());
    }

    /// <summary>检查单个 prefab/scene（传 Assets/ 开头的相对路径）。有问题就 LogError。</summary>
    public static int CheckFile(string assetPath)
    {
        var bad = new List<string>();
        Collect(assetPath, bad);
        if (bad.Count == 0) return 0;
        var sb = new StringBuilder();
        sb.AppendLine("[UiSpriteRefGuard] 🔴 " + assetPath + " 有 " + bad.Count + " 处图片引用丢失：");
        for (int i = 0; i < bad.Count && i < MAX_REPORT; i++) sb.AppendLine("  " + bad[i]);
        Debug.LogError(sb.ToString());
        return bad.Count;
    }

    static void Collect(string assetPathOrAbs, List<string> bad)
    {
        string abs = assetPathOrAbs;
        if (!Path.IsPathRooted(abs))
            abs = Path.Combine(Application.dataPath, "..", assetPathOrAbs).Replace('\\', '/');
        abs = abs.Replace('\\', '/');
        if (!File.Exists(abs)) return;

        // 显示用：统一成 Assets/... 的相对路径，别把整条绝对路径糊到 Console 里
        string rel = abs;
        string root = Application.dataPath.Replace('\\', '/');
        if (rel.StartsWith(root, StringComparison.Ordinal))
            rel = "Assets" + rel.Substring(root.Length);

        string[] lines;
        try { lines = File.ReadAllLines(abs); }
        catch { return; }

        string lastName = "?";
        var allow = AllowList();
        var seen = new HashSet<string>();

        for (int i = 0; i < lines.Length; i++)
        {
            var nm = NameLine.Match(lines[i]);
            if (nm.Success) lastName = nm.Groups[1].Value.Trim();

            var m = SpriteRef.Match(lines[i]);
            if (!m.Success) continue;

            string fileId = m.Groups[1].Value;
            string guid = m.Groups[2].Value;
            if (fileId == "0" || fileId == "-1") continue;          // 本来就空着，不算丢失
            string low = guid.ToLower();
            // 引擎内置资源（unity builtin extra / default resources 等）：
            // AssetDatabase 认得，只是磁盘上没有 .meta，不能当丢失报。
            if (low.StartsWith("0000000000000000", StringComparison.Ordinal)) continue;
            if (allow.Contains(low)) continue;                       // 豁免清单
            if (!seen.Add(low + "@" + i)) continue;

            if (!Hex32.IsMatch(guid))
            {
                bad.Add(string.Format("{0}  行{1}  节点[{2}]  guid 形态非法（{3} 字符，规范只认 32 位 hex）: {4}",
                    rel, i + 1, lastName, guid.Length, guid));
                continue;
            }

            string hit = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(hit))
            {
                bad.Add(string.Format("{0}  行{1}  节点[{2}]  贴图丢失  guid={3}",
                    rel, i + 1, lastName, guid));
            }
        }
    }
}

/// <summary>
/// 保存/导入 prefab 或 scene 时立刻检查那一个文件并报错。
/// 只查被动到的文件，低频、不刷屏；不修改任何资源。
/// </summary>
public class UiSpriteRefGuardPost : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
                                       string[] movedAssets, string[] movedFromAssetPaths)
    {
        if (!UiSpriteRefGuard.AutoEnabled) return;
        foreach (var p in importedAssets)
        {
            if (p == null) continue;
            if (!p.EndsWith(".prefab", StringComparison.Ordinal) &&
                !p.EndsWith(".unity", StringComparison.Ordinal)) continue;
            UiSpriteRefGuard.CheckFile(p);
        }
    }
}
