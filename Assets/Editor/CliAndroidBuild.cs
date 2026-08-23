using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class CliAndroidBuild
{
    const string OutDir = "Builds/Android";

    public static void BuildDevApk() => BuildApk("PixelAdventureTown-dev.apk", development: true);

    public static void BuildReleaseApk() => BuildApk("PixelAdventureTown-release.apk", development: false);

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

        // Mono 不支持 ARM64；不设 IL2CPP 时勾选 ARM64 会被清空，报 Target architecture not specified
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        if (string.IsNullOrEmpty(PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android)))
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.pixeladventure.town");

        EditorUserBuildSettings.development = development;
        EditorUserBuildSettings.connectProfiler = false;
        EditorUserBuildSettings.allowDebugging = development;
        EditorUserBuildSettings.buildAppBundle = false;
        EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

        Debug.Log($"[CliAndroidBuild] backend={PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android)} arch={PlayerSettings.Android.targetArchitectures}");

        var opts = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(OutDir, apkName),
            target = BuildTarget.Android,
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
