using UnityEditor;
using UnityEngine;

/// <summary>
/// 软著 Play 模式自动截图：用户正常玩游戏，识别到清单界面后自动保存。
/// </summary>
[InitializeOnLoad]
public static class SoftCopyrightAutoCapture
{
    const string PrefEnabled = "SoftCopyrightAutoCapture.Enabled";

    static SoftCopyrightAutoCapture()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        if (!EditorPrefs.GetBool(PrefEnabled, false)) return;
        SoftCopyrightAutoCaptureRunner.Spawn();
    }

    public static bool IsEnabled
    {
        get => EditorPrefs.GetBool(PrefEnabled, false);
        set => EditorPrefs.SetBool(PrefEnabled, value);
    }

    [MenuItem("Tools/软著/开启自动截图（Play 模式）", false, 210)]
    public static void Enable()
    {
        IsEnabled = true;
        EditorUtility.DisplayDialog("软著自动截图",
            "已开启。本期只自动截文档「必需」项（18 张，含开机健康忠告）。\n\n进入 Play 后正常玩游戏；\n识别到界面会保存到 Docs/软著附图/。\n\n开机健康忠告约 3 秒，请不要立刻点掉。\n登录页先点「清除存档」。Boss 关、冒险日志、角色、酒馆各停一下。",
            "知道了");
    }

    [MenuItem("Tools/软著/开启自动截图（Play 模式）", true)]
    public static bool EnableValidate() => !IsEnabled;

    [MenuItem("Tools/软著/关闭自动截图", false, 211)]
    public static void Disable()
    {
        IsEnabled = false;
        SoftCopyrightAutoCaptureRunner.Shutdown();
        EditorUtility.DisplayDialog("软著自动截图", "已关闭。", "确定");
    }

    [MenuItem("Tools/软著/关闭自动截图", true)]
    public static bool DisableValidate() => IsEnabled;

    [MenuItem("Tools/软著/重置自动截图进度", false, 212)]
    public static void ResetProgress()
    {
        if (SoftCopyrightAutoCaptureRunner.Instance != null)
            SoftCopyrightAutoCaptureRunner.Instance.ResetProgress();
        else
            EditorUtility.DisplayDialog("软著自动截图", "请先进入 Play 模式再重置；或下次 Play 会自动从 0 开始。", "确定");
    }

    [MenuItem("Tools/软著/自动截图状态", false, 213)]
    public static void ShowStatus()
    {
        string on = IsEnabled ? "已开启" : "已关闭";
        string play = Application.isPlaying && SoftCopyrightAutoCaptureRunner.Instance != null
            ? $"进行中 {_capturingProgress()}"
            : "未在 Play";
        EditorUtility.DisplayDialog("软著自动截图", $"{on}\n{play}\n\n输出：Docs/软著附图/", "确定");
    }

    static string _capturingProgress()
    {
        var r = SoftCopyrightAutoCaptureRunner.Instance;
        return r != null ? $"{r.CapturedCount}/{r.TotalRules}" : "";
    }
}
