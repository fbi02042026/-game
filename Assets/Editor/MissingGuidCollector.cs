#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 缺失资源诊断器（团结/Unity 编辑器菜单工具）
///
/// 用途：找出预制体/场景里「引擎自己也解析不了」的 guid 引用。
/// 判据直接调用 AssetDatabase.GUIDToAssetPath —— 引擎说无效就是真无效，
/// 比外部脚本按字符串规则猜更权威。
///
/// 菜单：Tools/诊断/…
/// </summary>
public static class MissingGuidCollector
{
    private const string MenuRoot = "Tools/诊断/";
    private static readonly Regex GuidRegex =
        new Regex(@"guid:\s*([0-9a-zA-Z+/=]{16,})", RegexOptions.Compiled);

    // 只扫这些后缀的引用方
    private static readonly string[] RefExts =
        { "*.prefab", "*.unity", "*.asset", "*.mat", "*.controller", "*.anim", "*.overrideController" };

    // 第三方素材包，不关心
    private static readonly string[] SkipDirs =
        { "/SPUM/", "Epic Toon FX", "Pixel Craft VFX", "Hyperbit", "/Demo/", "/Demos/" };

    private static string ProjectRoot =>
        Path.GetDirectoryName(Application.dataPath);

    [MenuItem(MenuRoot + "1. 收集缺失 guid 清单（输出到工程根目录）", false, 10)]
    public static void Collect()
    {
        var root = ProjectRoot;
        var files = new List<string>();
        foreach (var ext in RefExts)
            files.AddRange(Directory.GetFiles(Path.Combine(root, "Assets"), ext, SearchOption.AllDirectories));

        int scanned = 0;
        // guid -> 引用它的文件
        var badRefs = new Dictionary<string, List<string>>();
        int totalRefs = 0;

        foreach (var f in files)
        {
            var rel = f.Replace("\\", "/").Substring(root.Length + 1);
            bool skip = false;
            foreach (var s in SkipDirs)
                if (rel.Contains(s)) { skip = true; break; }
            if (skip) continue;

            string text;
            try { text = File.ReadAllText(f); }
            catch (Exception) { continue; }
            scanned++;

            var seen = new HashSet<string>();
            foreach (Match m in GuidRegex.Matches(text))
            {
                var g = m.Groups[1].Value;
                if (!seen.Add(g)) continue;
                totalRefs++;

                // 引擎自己的判定
                var path = AssetDatabase.GUIDToAssetPath(g);
                if (!string.IsNullOrEmpty(path)) continue;

                // 引擎内置资源（全 0 开头）不算
                if (g.StartsWith("00000000")) continue;

                if (!badRefs.TryGetValue(g, out var list))
                    badRefs[g] = list = new List<string>();
                list.Add(rel);
            }
        }

        // 输出
        var sb = new StringBuilder();
        sb.AppendLine("缺失 guid 清单（引擎 API 判定：AssetDatabase.GUIDToAssetPath 返回空）");
        sb.AppendLine("生成时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine("引擎版本: " + Application.unityVersion);
        sb.AppendLine(new string('=', 70));
        sb.AppendLine("扫描文件: " + scanned + "   引用总数: " + totalRefs +
                      "   缺失 guid: " + badRefs.Count);
        sb.AppendLine();

        foreach (var kv in badRefs)
        {
            sb.AppendLine(kv.Key);
            for (int i = 0; i < kv.Value.Count && i < 4; i++)
                sb.AppendLine("      " + kv.Value[i]);
            if (kv.Value.Count > 4)
                sb.AppendLine("      ...另 " + (kv.Value.Count - 4) + " 处");
        }

        var outPath = Path.Combine(root, "缺失guid清单.txt");
        File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);

        Debug.Log(string.Format(
            "[缺失资源诊断] 扫描 {0} 个文件，引用 {1} 处，缺失 guid {2} 个。\n报告: {3}",
            scanned, totalRefs, badRefs.Count, outPath));

        EditorUtility.DisplayDialog("缺失资源诊断",
            string.Format("扫描文件: {0}\n引用总数: {1}\n缺失 guid: {2}\n\n报告已写到:\n{3}",
                scanned, totalRefs, badRefs.Count, outPath),
            "知道了");

        EditorUtility.RevealInFinder(outPath);
    }

    [MenuItem(MenuRoot + "2. 统计空 Sprite（m_Sprite 被清成 None 的节点）", false, 11)]
    public static void CountEmptySprites()
    {
        var root = ProjectRoot;
        var guids = AssetDatabase.FindAssets("t:Prefab");
        int total = 0;
        var sb = new StringBuilder();
        sb.AppendLine("空 Sprite 统计（m_Sprite: {fileID: 0}）");
        sb.AppendLine("生成时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine(new string('=', 70));

        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            if (string.IsNullOrEmpty(path)) continue;
            bool skip = false;
            foreach (var s in SkipDirs)
                if (path.Contains(s)) { skip = true; break; }
            if (skip) continue;
            if (!path.Contains("/Resources/Prefabs/") && !path.Contains("/Scenes/")) continue;

            string text;
            try { text = File.ReadAllText(Path.Combine(root, path)); }
            catch (Exception) { continue; }

            int n = 0, idx = 0;
            while ((idx = text.IndexOf("m_Sprite: {fileID: 0}", idx, StringComparison.Ordinal)) >= 0)
            { n++; idx += 21; }

            if (n <= 0) continue;
            total += n;
            sb.AppendLine(string.Format("{0,4}  {1}", n, path));
        }

        sb.Insert(0, "合计: " + total + " 处\n\n");
        var outPath = Path.Combine(root, "空Sprite清单.txt");
        File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);

        Debug.Log("[空 Sprite 统计] 合计 " + total + " 处，报告: " + outPath);
        EditorUtility.DisplayDialog("空 Sprite 统计",
            "合计 " + total + " 处\n\n报告已写到:\n" + outPath, "知道了");
        EditorUtility.RevealInFinder(outPath);
    }

    [MenuItem(MenuRoot + "3. 校验指定 guid 是否有效（弹窗输入）", false, 12)]
    public static void CheckOne()
    {
        // 读取剪贴板作为默认值，方便把报告里的 guid 直接粘过来
        string def = EditorGUIUtility.systemCopyBuffer ?? "";
        var input = EditorInputDialog.Show("校验 guid",
            "输入 guid，用引擎 API 查它是否有效；多个用逗号分隔", def);
        if (input == null) return;

        var sb = new StringBuilder();
        foreach (var raw in input.Split(new[] { ',', ';', ' ', '\n', '\r' },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var g = raw.Trim();
            if (string.IsNullOrEmpty(g)) continue;
            var p = AssetDatabase.GUIDToAssetPath(g);
            sb.AppendLine(string.IsNullOrEmpty(p)
                ? g + "  =>  【无效 / Missing】"
                : g + "  =>  " + p);
        }
        Debug.Log("[guid 校验]\n" + sb);
        EditorUtility.DisplayDialog("guid 校验结果", sb.ToString(), "关闭");
    }
}

/// <summary>简易输入弹窗</summary>
public class EditorInputDialog : EditorWindow
{
    private string _title, _desc, _value;
    private Action<string> _ok;
    private bool _done;

    public static string Show(string title, string desc, string def)
    {
        var w = CreateInstance<EditorInputDialog>();
        w._title = title; w._desc = desc; w._value = def ?? "";
        w.titleContent = new GUIContent(title);
        w.minSize = new Vector2(520, 150);
        w.ShowModal();
        return w._done ? w._value : null;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(_desc, EditorStyles.wordWrappedLabel);
        EditorGUILayout.Space(4);
        GUI.SetNextControlName("input");
        _value = EditorGUILayout.TextField(_value);
        EditorGUILayout.Space(8);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("取消", GUILayout.Width(80))) { _done = false; Close(); }
            if (GUILayout.Button("确定", GUILayout.Width(80))) { _done = true; Close(); }
        }
        EditorGUI.FocusTextInControl("input");
    }
}
#endif
