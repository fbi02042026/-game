using System;
using System.IO;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor.ShortcutManagement;
#endif

/// <summary>
/// Play 模式下截图到 Docs/软著附图/。
/// 快捷键 F12；菜单 Tools → 软著 → 保存当前 Game 视图截图。
/// </summary>
public static class SoftCopyrightScreenshotTool
{
    const string OutputDir = "Docs/软著附图";

#if UNITY_EDITOR
    [Shortcut("软著/保存 Game 视图截图", KeyCode.F12)]
    static void ShortcutCapture()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[软著截图] 请先进入 Play 模式再按 F12。");
            return;
        }
        CaptureNow();
    }

    [MenuItem("Tools/软著/保存当前 Game 视图截图", false, 200)]
    public static void CaptureNow()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("软著截图", "请先进入 Play 模式，停在要截的界面后再执行。", "确定");
            return;
        }

        EnsureOutputDir();
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"SC_像素冒险裂隙之刃_V1.0_{stamp}.png";
        string absPath = Path.GetFullPath(Path.Combine(OutputDir, fileName));
        ScreenCapture.CaptureScreenshot(absPath);
        Debug.Log($"[软著截图] 已保存: {absPath}");
        EditorUtility.DisplayDialog("软著截图", $"已保存到:\n{absPath}", "确定");
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/软著/打开软著附图文件夹", false, 201)]
    public static void OpenOutputFolder()
    {
        EnsureOutputDir();
        EditorUtility.RevealInFinder(Path.GetFullPath(OutputDir));
    }

    static void EnsureOutputDir()
    {
        string abs = Path.GetFullPath(OutputDir);
        if (!Directory.Exists(abs))
            Directory.CreateDirectory(abs);
    }
#endif
}
