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

        // 仅 ARM64（64 位）
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
