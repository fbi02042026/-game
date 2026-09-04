using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Android / 聚光灯参赛包构建。Spotlight 包自动带 SPOTLIGHT_BUILD 宏。
/// </summary>
public static class CliAndroidBuild
{
    const string OutDir = "Builds/Android";
    const string SpotlightDefine = "SPOTLIGHT_BUILD";
    /// <summary>TapTap 商店常见目标；与 ProjectSettings 对齐。</summary>
    const AndroidSdkVersions SpotlightTargetSdk = (AndroidSdkVersions)34;

    public static void BuildDevApk() => BuildApk("PixelAdventure-CrackBlade-dev.apk", development: true, spotlight: false);

    public static void BuildReleaseApk() => BuildApk("PixelAdventure-CrackBlade-release.apk", development: false, spotlight: false);

    public static void BuildSpotlightApk() => BuildApk("PixelAdventure-Spotlight-android.apk", development: false, spotlight: true);

    static void BuildApk(string apkName, bool development, bool spotlight)
    {
        if (!Directory.Exists(OutDir))
            Directory.CreateDirectory(OutDir);

        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s != null && s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
            throw new System.Exception("No enabled scenes in EditorBuildSettings.");

        AppIconSetup.Apply();

        if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            throw new System.Exception("Failed to switch active build target to Android.");

        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel22;
        PlayerSettings.Android.targetSdkVersion = SpotlightTargetSdk;
        PlayerSettings.Android.forceInternetPermission = false;

        string prevDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android);
        string nextDefines = spotlight ? AddDefine(prevDefines, SpotlightDefine) : RemoveDefine(prevDefines, SpotlightDefine);
        if (nextDefines != prevDefines)
            PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android, nextDefines);

        EditorUserBuildSettings.development = development;
        EditorUserBuildSettings.connectProfiler = false;
        EditorUserBuildSettings.allowDebugging = development;
        EditorUserBuildSettings.buildAppBundle = false;

        var opts = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(OutDir, apkName),
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = development
                ? BuildOptions.Development | BuildOptions.AllowDebugging
                : BuildOptions.None
        };

        Debug.Log("[CliAndroidBuild] Building APK to: " + opts.locationPathName
            + (development ? " (Development ARM64)" : " (Release ARM64)")
            + (spotlight ? " [SPOTLIGHT]" : ""));
        try
        {
            BuildReport report = BuildPipeline.BuildPlayer(opts);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new System.Exception(
                    $"Android build failed: {report.summary.result}, errors={report.summary.totalErrors}");
            }
            Debug.Log("[CliAndroidBuild] Build success: " + report.summary.outputPath);
        }
        finally
        {
            // Spotlight 宏仅用于参赛包；构建后恢复，避免日常编辑器误开合规裁剪
            if (spotlight && prevDefines != nextDefines)
                PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android, prevDefines);
        }
    }

    public static void BuildSpotlightWindows64()
    {
        const string winDir = "Builds/Windows";
        if (!Directory.Exists(winDir))
            Directory.CreateDirectory(winDir);

        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s != null && s.enabled)
            .Select(s => s.path)
            .ToArray();
        if (scenes.Length == 0)
            throw new System.Exception("No enabled scenes in EditorBuildSettings.");

        if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            throw new System.Exception("Failed to switch active build target to StandaloneWindows64.");

        string prevDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone);
        string nextDefines = AddDefine(prevDefines, SpotlightDefine);
        if (nextDefines != prevDefines)
            PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, nextDefines);

        string outPath = Path.Combine(winDir, "PixelAdventure-Spotlight-windows", "PixelAdventure-Spotlight.exe");
        var opts = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outPath,
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None
        };

        Debug.Log("[CliAndroidBuild] Building Windows64 Spotlight: " + outPath);
        try
        {
            BuildReport report = BuildPipeline.BuildPlayer(opts);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new System.Exception(
                    $"Windows build failed: {report.summary.result}, errors={report.summary.totalErrors}");
            }
            Debug.Log("[CliAndroidBuild] Windows build success: " + report.summary.outputPath);
        }
        finally
        {
            if (prevDefines != nextDefines)
                PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, prevDefines);
        }
    }

    static string AddDefine(string defines, string symbol)
    {
        if (string.IsNullOrEmpty(defines)) return symbol;
        var parts = defines.Split(';');
        for (int i = 0; i < parts.Length; i++)
            if (parts[i].Trim() == symbol) return defines;
        return defines + ";" + symbol;
    }

    static string RemoveDefine(string defines, string symbol)
    {
        if (string.IsNullOrEmpty(defines)) return "";
        var parts = defines.Split(';');
        var kept = new System.Collections.Generic.List<string>();
        for (int i = 0; i < parts.Length; i++)
        {
            string p = parts[i].Trim();
            if (string.IsNullOrEmpty(p) || p == symbol) continue;
            kept.Add(p);
        }
        return string.Join(";", kept);
    }
}
