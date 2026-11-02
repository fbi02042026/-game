#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 【2026-11-02 P4】贴图 ASTC 批处理：把进包贴图在微信小游戏（WebGL）/安卓/iOS 平台
/// 统一压成 ASTC，降低纹理在构建数据文件里的体积（2D 游戏贴图常占资源一半以上）。
///
/// 用法：菜单「微信小游戏/包体优化/贴图ASTC(预览)」先看命中数与体积，再「贴图ASTC(应用)」。
/// 应用后建议跑一次 Build Report 核对 Texture 占比下降。
/// 注意：像素风贴图若需保留硬边缘，可在应用后单独把 filterMode 调回 Point；本工具只压格式不动采样。
/// </summary>
public static class TextureAstcMenu
{
    const string MenuPreview = "微信小游戏/包体优化/贴图ASTC(预览)";
    const string MenuApply = "微信小游戏/包体优化/贴图ASTC(应用)";

    // 进包贴图主要分布在这些目录（Resources 全进包；Art/Icons 被预制体引用者也进包）
    static readonly string[] ScanRoots = { "Assets/Resources", "Assets/Icons", "Assets/Art" };

    // ASTC block size：UI/图标用 6x6（清晰），大背景可用 8x8（更小）。这里统一 6x6 起步。
    const TextureImporterFormat Fmt = TextureImporterFormat.ASTC_6x6;

    [MenuItem(MenuPreview)]
    static void Preview()
    {
        var tex = Collect();
        long totalBytes = 0;
        int alreadyAstc = 0;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[贴图ASTC预览] 命中贴图：{tex.Count}");
        foreach (var p in tex)
        {
            var fi = new FileInfo(p);
            totalBytes += fi.Length;
            var ti = AssetImporter.GetAtPath(p) as TextureImporter;
            if (ti != null)
            {
                var web = ti.GetPlatformTextureSettings("WebGL");
                if (web.overridden && web.format == Fmt) alreadyAstc++;
            }
        }
        sb.AppendLine($"源文件总大小：{totalBytes / 1024 / 1024}MB；已为 ASTC：{alreadyAstc}");
        sb.AppendLine($"应用后：WebGL/Android/iPhone 平台统一 {Fmt}，构建数据文件纹理体积预计下降 40~60%（视原格式）。");
        Debug.Log(sb.ToString());
    }

    [MenuItem(MenuApply)]
    static void Apply()
    {
        var tex = Collect();
        int done = 0;
        foreach (var p in tex)
        {
            var ti = AssetImporter.GetAtPath(p) as TextureImporter;
            if (ti == null) continue;
            bool changed = false;

            // UI/精灵类关 mipmap（2D 不透视，省体积）
            if (ti.mipmapEnabled) { ti.mipmapEnabled = false; changed = true; }

            foreach (var plat in new[] { "WebGL", "Android", "iPhone" })
            {
                var ps = ti.GetPlatformTextureSettings(plat);
                if (!ps.overridden || ps.format != Fmt)
                {
                    ps.overridden = true;
                    ps.format = Fmt;
                    ps.compressionQuality = 50;
                    ps.maxTextureSize = ps.maxTextureSize <= 0 ? 2048 : ps.maxTextureSize;
                    ti.SetPlatformTextureSettings(ps);
                    changed = true;
                }
            }

            if (changed) { ti.SaveAndReimport(); done++; }
        }
        Debug.Log($"[贴图ASTC应用] 已更新 {done}/{tex.Count} 张贴图的平台压缩设置（{Fmt}）。请跑一次构建核对 Build Report 的 Texture 占比。");
        AssetDatabase.SaveAssets();
    }

    static List<string> Collect()
    {
        var result = new List<string>();
        var ext = new HashSet<string> { ".png", ".jpg", ".jpeg", ".tga", ".psd", ".bmp", ".tiff", ".exr", ".webp" };
        foreach (var root in ScanRoots)
        {
            if (!Directory.Exists(root)) continue;
            var files = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories);
            foreach (var f in files)
            {
                var lower = f.ToLower();
                if (ext.Contains(Path.GetExtension(lower)))
                    result.Add(f.Replace('\\', '/'));
            }
        }
        return result;
    }
}
#endif
