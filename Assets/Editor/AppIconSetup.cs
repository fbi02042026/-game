using UnityEditor;
using UnityEngine;

/// <summary>
/// 游戏 Icon：把 Assets/Art/AppIcon/app_icon.png 填进
/// Standalone + Android（Legacy / Round / Adaptive 等全部槽位）。
/// </summary>
public static class AppIconSetup
{
    public const string IconPath = "Assets/Art/AppIcon/app_icon.png";

    [MenuItem("Tools/配置游戏 Icon")]
    public static void ApplyFromMenu()
    {
        string report;
        bool ok = Apply(out report);
        EditorUtility.DisplayDialog(ok ? "游戏 Icon 成功" : "游戏 Icon 失败", report, "好的");
    }

    [MenuItem("Tools/检查游戏 Icon")]
    public static void InspectFromMenu()
    {
        EditorUtility.DisplayDialog("游戏 Icon 检查", BuildInspectReport(), "好的");
    }

    public static bool Apply() => Apply(out _);

    public static bool Apply(out string report)
    {
        var sb = new System.Text.StringBuilder();
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        if (tex == null)
        {
            report = "找不到图标文件：\n" + IconPath + "\n请把红发游戏 icon 放到该路径。";
            Debug.LogError("[AppIconSetup] " + report);
            return false;
        }

        EnsureTextureImportSettings(IconPath);
        tex = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);

        // 1) 旧 API：Standalone / 默认
        ApplyLegacyGroup(BuildTargetGroup.Standalone, tex, sb);
        ApplyLegacyGroup(BuildTargetGroup.Unknown, tex, sb);

        // 2) 新 API：Android 全部 IconKind（Legacy/Round/Adaptive…）
        int androidFilled = ApplyAllPlatformKinds(BuildTargetGroup.Android, tex, sb);

        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(tex);

        sb.Insert(0, "图标：" + IconPath + "\n"
            + "尺寸：" + tex.width + "x" + tex.height + "\n"
            + "Android 已填充槽位：" + androidFilled + "\n\n");
        sb.AppendLine();
        sb.AppendLine("请到 Edit → Project Settings → Player：");
        sb.AppendLine("· Android → Icon → Legacy / Round / Adaptive 都应有图");
        sb.AppendLine("· 再重新打 APK，桌面图标才会更新");

        report = sb.ToString();
        Debug.Log("[AppIconSetup] " + report.Replace('\n', ' '));
        return androidFilled > 0;
    }

    static void ApplyLegacyGroup(BuildTargetGroup group, Texture2D tex, System.Text.StringBuilder sb)
    {
        try
        {
            int[] sizes = PlayerSettings.GetIconSizesForTargetGroup(group);
            if (sizes == null || sizes.Length == 0)
            {
                sb.AppendLine("跳过 " + group + "（无尺寸槽）");
                return;
            }
            var arr = new Texture2D[sizes.Length];
            for (int i = 0; i < arr.Length; i++)
                arr[i] = tex;
            PlayerSettings.SetIconsForTargetGroup(group, arr);
            sb.AppendLine("已写 " + group + " × " + sizes.Length);
        }
        catch (System.Exception e)
        {
            sb.AppendLine(group + " 失败: " + e.Message);
        }
    }

    static int ApplyAllPlatformKinds(BuildTargetGroup group, Texture2D tex, System.Text.StringBuilder sb)
    {
        int filled = 0;
        PlatformIconKind[] kinds;
        try
        {
            kinds = PlayerSettings.GetSupportedIconKindsForPlatform(group);
        }
        catch (System.Exception e)
        {
            sb.AppendLine(group + " GetSupportedIconKinds 失败: " + e.Message);
            return 0;
        }

        if (kinds == null || kinds.Length == 0)
        {
            sb.AppendLine(group + " 无 PlatformIconKind");
            return 0;
        }

        foreach (var kind in kinds)
        {
            try
            {
                var icons = PlayerSettings.GetPlatformIcons(group, kind);
                if (icons == null || icons.Length == 0)
                {
                    sb.AppendLine("  kind=" + kind + " 无槽位");
                    continue;
                }

                for (int i = 0; i < icons.Length; i++)
                {
                    // Adaptive 需要前景；其它 kind 一般 1 张即可
                    int layerCount = icons[i].maxLayerCount;
                    if (layerCount <= 0) layerCount = 1;
                    var layers = new Texture2D[layerCount];
                    for (int L = 0; L < layerCount; L++)
                        layers[L] = tex;
                    icons[i].SetTextures(layers);
                    filled++;
                }
                PlayerSettings.SetPlatformIcons(group, kind, icons);
                sb.AppendLine("  kind=" + kind + " × " + icons.Length);
            }
            catch (System.Exception e)
            {
                sb.AppendLine("  kind=" + kind + " 失败: " + e.Message);
            }
        }
        return filled;
    }

    static void EnsureTextureImportSettings(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        bool dirty = false;
        if (importer.textureType != TextureImporterType.Default)
        {
            importer.textureType = TextureImporterType.Default;
            dirty = true;
        }
        if (importer.npotScale != TextureImporterNPOTScale.None)
        {
            importer.npotScale = TextureImporterNPOTScale.None;
            dirty = true;
        }
        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            dirty = true;
        }
        if (!importer.isReadable)
        {
            // PlayerSettings 缩放 Icon 时更稳
            importer.isReadable = true;
            dirty = true;
        }
        if (importer.maxTextureSize < 1024)
        {
            importer.maxTextureSize = 1024;
            dirty = true;
        }
        if (dirty)
        {
            importer.SaveAndReimport();
        }
    }

    static string BuildInspectReport()
    {
        var sb = new System.Text.StringBuilder();
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        sb.AppendLine("文件: " + IconPath);
        sb.AppendLine(tex != null
            ? ("状态: 找到  " + tex.width + "x" + tex.height)
            : "状态: 找不到文件！");
        sb.AppendLine("GUID: " + AssetDatabase.AssetPathToGUID(IconPath));
        sb.AppendLine();

        try
        {
            var kinds = PlayerSettings.GetSupportedIconKindsForPlatform(BuildTargetGroup.Android);
            sb.AppendLine("Android Icon 槽：");
            foreach (var kind in kinds)
            {
                var icons = PlayerSettings.GetPlatformIcons(BuildTargetGroup.Android, kind);
                int has = 0;
                if (icons != null)
                {
                    for (int i = 0; i < icons.Length; i++)
                    {
                        var ts = icons[i].GetTextures();
                        if (ts != null && ts.Length > 0 && ts[0] != null) has++;
                    }
                }
                sb.AppendLine("  " + kind + ": " + has + "/" + (icons != null ? icons.Length : 0));
            }
        }
        catch (System.Exception e)
        {
            sb.AppendLine("检查失败: " + e.Message);
        }

        sb.AppendLine();
        sb.AppendLine("若 Adaptive/Round 为 0，手机桌面可能仍是默认图。");
        sb.AppendLine("点菜单「Tools/配置游戏 Icon」一键补齐。");
        return sb.ToString();
    }
}
