#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 扫描运行时/编辑器脚本中 ScreenSpaceOverlay 违规用法，防止 UI 渲染模式回退。
/// </summary>
public static class UiCanvasAuditTool
{
    static readonly string[] ScanRoots = { "Assets/Scripts", "Assets/Editor" };

    [MenuItem("Tools/UI/检查 Overlay Canvas 违规")]
    public static void AuditOverlayViolations()
    {
        var hits = new List<string>();

        foreach (var root in ScanRoots)
        {
            if (!Directory.Exists(root)) continue;
            foreach (var path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string normalized = path.Replace('\\', '/');
                if (normalized.EndsWith("UiCanvasAuditTool.cs")) continue;
                string text = File.ReadAllText(path);
                if (text.Contains("ScreenSpaceOverlay"))
                    hits.Add(normalized);
            }
        }

        if (hits.Count == 0)
        {
            Debug.Log("[UiCanvasAuditTool] 通过：Scripts + Editor 无 ScreenSpaceOverlay。");
            EditorUtility.DisplayDialog("UI Canvas 审计", "通过：无 Overlay 违规。", "OK");
            return;
        }

        foreach (var h in hits)
            Debug.LogWarning("[UiCanvasAuditTool] Overlay 违规: " + h, AssetDatabase.LoadAssetAtPath<Object>(h));

        EditorUtility.DisplayDialog(
            "UI Canvas 审计",
            $"发现 {hits.Count} 处 ScreenSpaceOverlay（见 Console）。\n请改为 UICanvasSetup.ApplyPopup。",
            "OK");
    }
}
#endif
