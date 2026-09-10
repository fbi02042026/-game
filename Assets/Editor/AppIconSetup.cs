using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 游戏 Icon：Legacy/Round 用完整 app_icon；
/// Android Adaptive 用带安全边距的前景 + 纯色背景，避免桌面遮罩裁成「放大」。
/// </summary>
public static class AppIconSetup
{
    public const string IconPath = "Assets/Art/AppIcon/app_icon.png";
    public const string AdaptiveBgPath = "Assets/Art/AppIcon/app_icon_adaptive_bg.png";
    public const string AdaptiveFgPath = "Assets/Art/AppIcon/app_icon_adaptive_fg.png";

    /// <summary>Adaptive 前景内容占画布比例（Android 安全区约 66%，略放大到 68%）。</summary>
    const float AdaptiveContentScale = 0.68f;

    [MenuItem("Tools/_归档/配置游戏 Icon")]
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

        if (!EnsureAdaptiveLayers(tex, sb))
        {
            report = sb.ToString();
            return false;
        }

        var adaptiveBg = AssetDatabase.LoadAssetAtPath<Texture2D>(AdaptiveBgPath);
        var adaptiveFg = AssetDatabase.LoadAssetAtPath<Texture2D>(AdaptiveFgPath);

        ApplyLegacyGroup(BuildTargetGroup.Standalone, tex, sb);
        ApplyLegacyGroup(BuildTargetGroup.Unknown, tex, sb);

        int androidFilled = ApplyAllPlatformKinds(BuildTargetGroup.Android, tex, adaptiveBg, adaptiveFg, sb);

        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(tex);

        sb.Insert(0, "图标：" + IconPath + "\n"
            + "尺寸：" + tex.width + "x" + tex.height + "\n"
            + "Adaptive 前景：" + AdaptiveFgPath + "（内容约 " + (int)(AdaptiveContentScale * 100f) + "%）\n"
            + "Adaptive 背景：" + AdaptiveBgPath + "\n"
            + "Android 已填充槽位：" + androidFilled + "\n\n");
        sb.AppendLine();
        sb.AppendLine("请到 Edit → Project Settings → Player：");
        sb.AppendLine("· Android → Icon → Legacy / Round 用原图；Adaptive 为缩略前景 + 纯色底");
        sb.AppendLine("· 再重新打 APK，桌面图标才会更新");

        report = sb.ToString();
        Debug.Log("[AppIconSetup] " + report.Replace('\n', ' '));
        return androidFilled > 0;
    }

    static bool EnsureAdaptiveLayers(Texture2D src, System.Text.StringBuilder sb)
    {
        try
        {
            EnsureTextureImportSettings(IconPath);
            src = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (src == null || !src.isReadable)
            {
                sb.AppendLine("源图标不可读，无法生成 Adaptive 层");
                return false;
            }

            Color32 bgColor = SampleBackgroundColor(src);
            int size = 1024;
            WritePng(AdaptiveBgPath, BuildSolid(size, bgColor));
            WritePng(AdaptiveFgPath, BuildPaddedForeground(src, size, AdaptiveContentScale));

            AssetDatabase.ImportAsset(AdaptiveBgPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(AdaptiveFgPath, ImportAssetOptions.ForceUpdate);
            EnsureTextureImportSettings(AdaptiveBgPath);
            EnsureTextureImportSettings(AdaptiveFgPath);

            sb.AppendLine("已生成 Adaptive 层 bg=" + bgColor + " scale=" + AdaptiveContentScale);
            return true;
        }
        catch (System.Exception e)
        {
            sb.AppendLine("生成 Adaptive 层失败: " + e.Message);
            return false;
        }
    }

    static Color32 SampleBackgroundColor(Texture2D src)
    {
        // 圆角框内侧深色底，避免采到透明/外边纯黑
        int x = Mathf.Clamp(src.width / 12, 8, src.width - 1);
        int y = Mathf.Clamp(src.height / 12, 8, src.height - 1);
        Color c = src.GetPixel(x, y);
        return (Color32)c;
    }

    static Texture2D BuildSolid(int size, Color32 color)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = color;
        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        return tex;
    }

    static Texture2D BuildPaddedForeground(Texture2D src, int size, float contentScale)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var clear = new Color32[size * size];
        for (int i = 0; i < clear.Length; i++)
            clear[i] = new Color32(0, 0, 0, 0);
        tex.SetPixels32(clear);

        int box = Mathf.Max(1, Mathf.RoundToInt(size * contentScale));
        float scale = Mathf.Min(box / (float)src.width, box / (float)src.height);
        int nw = Mathf.Max(1, Mathf.RoundToInt(src.width * scale));
        int nh = Mathf.Max(1, Mathf.RoundToInt(src.height * scale));
        int ox = (size - nw) / 2;
        int oy = (size - nh) / 2;

        // 点采样缩放，保持像素风
        for (int dy = 0; dy < nh; dy++)
        {
            int sy = Mathf.Clamp(Mathf.FloorToInt(dy / scale), 0, src.height - 1);
            for (int dx = 0; dx < nw; dx++)
            {
                int sx = Mathf.Clamp(Mathf.FloorToInt(dx / scale), 0, src.width - 1);
                tex.SetPixel(ox + dx, oy + dy, src.GetPixel(sx, sy));
            }
        }
        tex.Apply(false, false);
        return tex;
    }

    static void WritePng(string assetPath, Texture2D tex)
    {
        string abs = Path.GetFullPath(assetPath);
        string dir = Path.GetDirectoryName(abs);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllBytes(abs, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
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

    static int ApplyAllPlatformKinds(
        BuildTargetGroup group, Texture2D legacy,
        Texture2D adaptiveBg, Texture2D adaptiveFg,
        System.Text.StringBuilder sb)
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

                bool adaptive = IsAdaptiveKind(kind, icons);
                for (int i = 0; i < icons.Length; i++)
                {
                    int layerCount = icons[i].maxLayerCount;
                    if (layerCount <= 0) layerCount = 1;
                    var layers = new Texture2D[layerCount];
                    if (adaptive && layerCount >= 2 && adaptiveBg != null && adaptiveFg != null)
                    {
                        // Unity Adaptive：第 0 层背景，第 1 层前景
                        layers[0] = adaptiveBg;
                        layers[1] = adaptiveFg;
                        for (int L = 2; L < layerCount; L++)
                            layers[L] = adaptiveFg;
                    }
                    else
                    {
                        for (int L = 0; L < layerCount; L++)
                            layers[L] = legacy;
                    }
                    icons[i].SetTextures(layers);
                    filled++;
                }
                PlayerSettings.SetPlatformIcons(group, kind, icons);
                sb.AppendLine("  kind=" + kind + (adaptive ? " [Adaptive bg+fg]" : " [Legacy]") + " × " + icons.Length);
            }
            catch (System.Exception e)
            {
                sb.AppendLine("  kind=" + kind + " 失败: " + e.Message);
            }
        }
        return filled;
    }

    static bool IsAdaptiveKind(PlatformIconKind kind, PlatformIcon[] icons)
    {
        string name = kind.ToString();
        if (name.IndexOf("Adaptive", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        if (icons != null && icons.Length > 0 && icons[0].maxLayerCount >= 2)
            return true;
        return false;
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
            importer.isReadable = true;
            dirty = true;
        }
        if (importer.maxTextureSize < 1024)
        {
            importer.maxTextureSize = 1024;
            dirty = true;
        }
        if (importer.filterMode != FilterMode.Point)
        {
            importer.filterMode = FilterMode.Point;
            dirty = true;
        }
        if (dirty)
            importer.SaveAndReimport();
    }

    static string BuildInspectReport()
    {
        var sb = new System.Text.StringBuilder();
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        sb.AppendLine("文件: " + IconPath);
        sb.AppendLine(tex != null
            ? ("状态: 找到  " + tex.width + "x" + tex.height)
            : "状态: 找不到文件！");
        sb.AppendLine("Adaptive BG: " + (AssetDatabase.LoadAssetAtPath<Texture2D>(AdaptiveBgPath) != null ? "有" : "无"));
        sb.AppendLine("Adaptive FG: " + (AssetDatabase.LoadAssetAtPath<Texture2D>(AdaptiveFgPath) != null ? "有" : "无"));
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
        sb.AppendLine("点菜单「Tools/_归档/配置游戏 Icon」可重新生成 Adaptive 安全边距层。");
        return sb.ToString();
    }
}
