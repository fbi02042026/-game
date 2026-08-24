using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class CliAndroidBuild
{
    const string OutDir = "Builds/Android";

    public static void BuildDevApk() => BuildApk("PixelAdventure-CrackBlade-dev.apk", development: true);

    public static void BuildReleaseApk() => BuildApk("PixelAdventure-CrackBlade-release.apk", development: false);

    static void BuildApk(string apkName, bool development)
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

        // 切到 Android 再设架构，否则 batchmode 下会报 Target architecture not specified
        if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            throw new System.Exception("Failed to switch active build target to Android.");

        // ARM64 需要 IL2CPP；仅 ARM64（64 位）
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

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
            + (development ? " (Development ARM64)" : " (Release ARM64)"));
        BuildReport report = BuildPipeline.BuildPlayer(opts);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new System.Exception(
                $"Android build failed: {report.summary.result}, errors={report.summary.totalErrors}");
        }

        Debug.Log("[CliAndroidBuild] Build success: " + report.summary.outputPath);
    }
}
