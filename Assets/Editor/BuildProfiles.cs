using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 包型（渠道粒度）一键切换 + 出包流水（Editor-only）。
/// 包型不是引擎 BuildTarget：切换只写「构建参数 + 对应平台组的 SDK define」，
/// 不切引擎 BuildTarget、不调用平台切换 API（那是 CliAndroidBuild 构建流程自己的事）。
/// SDK 开关真值 = 各平台组的 Scripting Define Symbol（SdkToggles.All 登记表）；
/// EditorPrefs 只存「当前选中哪个包型」这一个 UI 字符串，不入库、不参与构建判定。
/// 版本号完全不自动改：切包型 / 出包 / 记流水都只读取 bundleVersion / AndroidBundleVersionCode，
/// 正式出包改号按 AGENTS.md §4.5 由主人给目标号手动改。
/// </summary>
public static class BuildProfiles
{
    /// <summary>一条包型记录：Id / MenuPath / 构建入口 / 绑定的 SDK Define 列表 / IsRelease / ApkName。</summary>
    public sealed class Profile
    {
        public readonly string Id;
        public readonly string MenuPath;
        public readonly Action Build;
        public readonly SdkToggles.Entry[] EnabledSdks;
        public readonly bool IsRelease;
        public readonly string ApkName;

        public Profile(string id, string menuPath, Action build, SdkToggles.Entry[] enabledSdks, bool isRelease, string apkName)
        {
            Id = id;
            MenuPath = menuPath;
            Build = build;
            EnabledSdks = enabledSdks;
            IsRelease = isRelease;
            ApkName = apkName;
        }
    }

    const string MenuTapTapRelease = "Tools/出包/TapTap 正式包";
    const string MenuAndroidDev = "Tools/出包/测试包";
    const string MenuSpotlight = "Tools/出包/参赛包 Spotlight";
    const string MenuCurrentStatus = "Tools/出包/当前状态";
    const string MenuBuildCurrent = "Tools/出包/打当前包";

    // 仅 UI 选中态：出包时以实际 define 为准，不读它决定 SDK 开关。
    const string SelectedPref = "PixelAdventureTown.BuildProfiles.Selected";
    const string DefaultProfileId = "TapTapRelease";
    const string LogPath = "Docs/出包记录.md";

    // SDK 绑定直接引用 SdkToggles.All 里的 Entry（含 Define + Group），不另抄一份 SDK 名单。
    static readonly SdkToggles.Entry TapTapSdk = FindSdk("TapTap");

    static readonly Profile[] Table =
    {
        new Profile("TapTapRelease", MenuTapTapRelease, CliAndroidBuild.BuildReleaseApk,
            new[] { TapTapSdk }, true, "PixelAdventure-CrackBlade-release.apk"),
        new Profile("AndroidDev", MenuAndroidDev, CliAndroidBuild.BuildDevApk,
            new[] { TapTapSdk }, false, "PixelAdventure-CrackBlade-dev.apk"),
        new Profile("Spotlight", MenuSpotlight, CliAndroidBuild.BuildSpotlightApk,
            new[] { TapTapSdk }, false, "PixelAdventure-Spotlight-android.apk"),
    };

    static string ProjectRoot { get { return Path.GetDirectoryName(Application.dataPath); } }

    // 三条选中菜单：validate 打勾 + 执行函数（写法参照 SdkToggles.cs 菜单三件套）。
    [MenuItem(MenuTapTapRelease)]
    public static void SelectTapTapRelease() { Select(Table[0]); }

    [MenuItem(MenuTapTapRelease, true)]
    public static bool SelectTapTapReleaseValidate() { return Validate(Table[0]); }

    [MenuItem(MenuAndroidDev)]
    public static void SelectAndroidDev() { Select(Table[1]); }

    [MenuItem(MenuAndroidDev, true)]
    public static bool SelectAndroidDevValidate() { return Validate(Table[1]); }

    [MenuItem(MenuSpotlight)]
    public static void SelectSpotlight() { Select(Table[2]); }

    [MenuItem(MenuSpotlight, true)]
    public static bool SelectSpotlightValidate() { return Validate(Table[2]); }

    static void Select(Profile p)
    {
        EditorPrefs.SetString(SelectedPref, p.Id);
        SdkToggles.ApplyDefinesForProfile(p.EnabledSdks);
        RefreshChecks(p.Id);
        Debug.Log("[BuildProfiles] 已切换到 " + p.Id + " / " + SdkStatusText());
    }

    static bool Validate(Profile p)
    {
        string selected = EditorPrefs.GetString(SelectedPref, DefaultProfileId);
        Menu.SetChecked(p.MenuPath, selected == p.Id);
        return true;
    }

    static void RefreshChecks(string selectedId)
    {
        foreach (var p in Table)
            Menu.SetChecked(p.MenuPath, p.Id == selectedId);
    }

    /// <summary>只读打印：当前包型 / 版本号 / versionCode / 每个 SDK ON/OFF / git HEAD 短号。不写任何字段。</summary>
    [MenuItem(MenuCurrentStatus)]
    public static void PrintCurrentStatus()
    {
        string selectedId = EditorPrefs.GetString(SelectedPref, DefaultProfileId);
        Debug.Log("[BuildProfiles] 当前包型: " + selectedId);
        Debug.Log("[BuildProfiles] bundleVersion: " + PlayerSettings.bundleVersion);
        Debug.Log("[BuildProfiles] AndroidBundleVersionCode: " + PlayerSettings.Android.bundleVersionCode);
        foreach (var sdk in SdkToggles.All)
            Debug.Log("[BuildProfiles] SDK " + sdk.Define + "=" + (SdkOn(sdk) ? "ON" : "OFF") + " (" + sdk.Group + ")");
        Debug.Log("[BuildProfiles] git HEAD: " + GitHeadShort());
    }

    /// <summary>按选中包型转调 CliAndroidBuild 对应入口；不重复实现构建逻辑。</summary>
    [MenuItem(MenuBuildCurrent)]
    public static void BuildCurrent()
    {
        Profile p = FindById(EditorPrefs.GetString(SelectedPref, DefaultProfileId));
        if (p == null)
        {
            Debug.LogError("[BuildProfiles] 未登记的选中包型，请先从 Tools/出包 选一个包型。");
            return;
        }
        DateTime beforeUtc = DateTime.UtcNow;
        p.Build();
        string apk = Path.Combine(ProjectRoot, "Builds/Android", p.ApkName);
        if (File.Exists(apk) && File.GetLastWriteTimeUtc(apk) >= beforeUtc.AddSeconds(-1))
            AppendBuildRecord(p, apk);
    }

    /// <summary>按选定包型往 Docs/出包记录.md 追加一行（含当时的版本号，只记录不修改）。</summary>
    static void AppendBuildRecord(Profile p, string outputPath)
    {
        string profileId = p != null ? p.Id : "(未知)";
        bool isRelease = p != null && p.IsRelease;
        string line = "| " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            + " | " + profileId
            + " | " + PlayerSettings.bundleVersion
            + " | " + PlayerSettings.Android.bundleVersionCode
            + " | " + SdkStatusText()
            + " | " + outputPath
            + " | " + GitHeadShort()
            + " | " + (isRelease ? "是" : "否") + " |";
        AppendLinePreservingEol(Path.Combine(ProjectRoot, LogPath), line);
    }

    /// <summary>按文件实际行尾追加一行，UTF-8 无 BOM；不改动既有任何字节。</summary>
    static void AppendLinePreservingEol(string path, string line)
    {
        byte[] data = File.ReadAllBytes(path);
        bool crlf = false;
        for (int i = 0; i + 1 < data.Length; i++)
        {
            if (data[i] == (byte)'\r' && data[i + 1] == (byte)'\n') { crlf = true; break; }
        }
        string eol = crlf ? "\r\n" : "\n";
        // 表头没有结尾换行时，先补一个与文件行尾一致的换行，避免新行粘连。
        string leading = (data.Length > 0 && data[data.Length - 1] != (byte)'\n') ? eol : "";
        byte[] append = new UTF8Encoding(false).GetBytes(leading + line + eol);
        using (var fs = new FileStream(path, FileMode.Append, FileAccess.Write))
            fs.Write(append, 0, append.Length);
    }

    static bool SdkOn(SdkToggles.Entry sdk)
    {
        string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(sdk.Group);
        return !string.IsNullOrEmpty(defines)
               && defines.Split(';').Any(d => d.Trim() == sdk.Define);
    }

    static string SdkStatusText()
    {
        var parts = new List<string>();
        foreach (var sdk in SdkToggles.All)
            parts.Add(sdk.Define + "=" + (SdkOn(sdk) ? "ON" : "OFF"));
        return string.Join(", ", parts.ToArray());
    }

    /// <summary>git HEAD 短号：直接读 .git/HEAD（是 ref 再读对应 refs 文件），取前 7 位，不启动外部进程。</summary>
    static string GitHeadShort()
    {
        try
        {
            string gitDir = Path.Combine(ProjectRoot, ".git");
            string head = File.ReadAllText(Path.Combine(gitDir, "HEAD")).Trim();
            string sha;
            if (head.StartsWith("ref:"))
            {
                string refName = head.Substring(4).Trim();
                string refPath = Path.Combine(gitDir, refName.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(refPath)) return "(unknown: " + refName + " packed)";
                sha = File.ReadAllText(refPath).Trim();
            }
            else
            {
                sha = head; // detached HEAD
            }
            if (string.IsNullOrEmpty(sha)) return "(unknown)";
            return sha.Length > 7 ? sha.Substring(0, 7) : sha;
        }
        catch (Exception e)
        {
            return "(unknown: " + e.Message + ")";
        }
    }

    static SdkToggles.Entry FindSdk(string id)
    {
        foreach (var e in SdkToggles.All)
            if (e.Id == id) return e;
        throw new Exception("[BuildProfiles] SdkToggles.All 里没有 Id=" + id);
    }

    static Profile FindById(string id)
    {
        foreach (var p in Table)
            if (p.Id == id) return p;
        return null;
    }

    static Profile FindByApkName(string outputPath)
    {
        string fileName = Path.GetFileName(outputPath);
        foreach (var p in Table)
            if (string.Equals(p.ApkName, fileName, StringComparison.OrdinalIgnoreCase)) return p;
        return null;
    }

    // 验收钩子（写进注释即可，不必自动跑）：
    // 1) 切包型后执行 Tools/出包/当前状态，打印的 SDK ON/OFF 应与实际 define 一致；
    // 2) 三个入口都能从菜单打出包，且 Docs/出包记录.md 各多一行；
    // 3) git diff 里 bundleVersion / AndroidBundleVersionCode 零变化。
}
