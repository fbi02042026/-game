#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 剧情立绘：黑底转透明 + 裁掉空白边，再同步到 Resources/Story/Portraits。
/// 美术导出 RGB 黑底全身图时跑一遍即可。
/// </summary>
public static class StoryPortraitProcessTool
{
    const string ArtDir = "Assets/Art/UI/Story";
    const string ResDir = "Assets/Resources/Story/Portraits";

    [MenuItem("Tools/UI/处理剧情立绘（抠黑底+裁剪+同步）")]
    public static void ProcessAndSync()
    {
        if (!Directory.Exists(ArtDir))
        {
            EditorUtility.DisplayDialog("剧情立绘", "未找到 " + ArtDir, "OK");
            return;
        }

        string py = Path.GetFullPath("Tools/process_story_portraits.py");
        if (!File.Exists(py))
        {
            EditorUtility.DisplayDialog("剧情立绘", "缺少 Tools/process_story_portraits.py", "OK");
            return;
        }

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "python",
            Arguments = $"\"{py}\"",
            WorkingDirectory = Path.GetFullPath("."),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var proc = System.Diagnostics.Process.Start(psi);
        string stdout = proc.StandardOutput.ReadToEnd();
        string stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        if (proc.ExitCode != 0)
        {
            Debug.LogError("[StoryPortraitProcessTool] " + stderr + stdout);
            EditorUtility.DisplayDialog("剧情立绘", "处理失败，见 Console。", "OK");
            return;
        }

        AssetDatabase.Refresh();
        Debug.Log("[StoryPortraitProcessTool]\n" + stdout);
        EditorUtility.DisplayDialog("剧情立绘", "黑底已抠除并同步到 Resources。\n" + stdout, "OK");
    }
}
#endif
