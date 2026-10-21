using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// 2026-10-21 主人需求：全项目图片做安卓端压缩优化（Unity 自带能力）。
/// 规则（主人拍板「按图片最接近的比例优化 宁大勿小」）：
///   1. MaxSize = 原生最大边向上取 2 的幂（不低于原生分辨率，缩放比例无损）；
///      已有比它更小 MaxSize 的（原本就有意的降采样）尊重原设置不放大，只补压缩格式。
///   2. Android 平台覆写 ASTC_6x6（团结/微信小游戏标准压缩，比默认 ETC2 更小且质量更好）。
/// 只覆写 Android 平台 —— 编辑器里显示、其他平台不受影响；改完打 APK 看包体差。
/// </summary>
public static class TextureCompressTool
{
    const string ReportPath = ".workbuddy/backup/20261021-texture-compress/report.csv";

    [MenuItem("Tools/纹理压缩/安卓优化（宁大勿小·ASTC6x6）")]
    public static void RunFromMenu()
    {
        Run();
        AssetDatabase.Refresh();
    }

    /// <summary>批处理入口：Tuanjie.exe -batchmode -quit -projectPath ... -executeMethod TextureCompressTool.RunFromCLI</summary>
    public static void RunFromCLI()
    {
        Run();
        AssetDatabase.SaveAssets();
    }

    static void Run()
    {
        var sw = Stopwatch.StartNew();
        var guids = AssetDatabase.FindAssets("t:Texture2D");
        int total = 0, changed = 0, untouched = 0, keepSmaller = 0, noSource = 0, errors = 0;
        long oldPixels = 0, newPixels = 0; // 采样：改动的纹理 旧/新运行时最大边² 之和（内存近似代理）
        var rows = new StringBuilder();
        rows.AppendLine("path,srcW,srcH,native,pow2,newMax,oldMax,oldFormat");
        foreach (var g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/")) continue;
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) continue;
            total++;
            try
            {
                imp.GetSourceTextureWidthAndHeight(out int w, out int h);
                if (w <= 0 || h <= 0) { noSource++; continue; }
                int native = Mathf.Max(w, h);
                int target = Pow2Ceil(native);
                var plat = imp.GetPlatformTextureSettings("Android");
                var oldFormat = plat.format;
                int oldMax = plat.maxTextureSize;
                int newMax = target;
                if (plat.overridden && oldMax > 0 && oldMax < target)
                {
                    newMax = oldMax; // 尊重原本更紧的降采样意图，不放大
                    keepSmaller++;
                }
                bool need = !plat.overridden
                            || plat.maxTextureSize != newMax
                            || plat.format != TextureImporterFormat.ASTC_6x6;
                if (!need) { untouched++; continue; }

                long area = (long)w * h;
                long oldRuntime = (long)Mathf.Min(oldMax > 0 ? oldMax : 8192, native);
                oldRuntime *= oldRuntime;
                long newRuntime = (long)Mathf.Min(newMax, native);
                newRuntime *= newRuntime;
                oldPixels += oldRuntime;
                newPixels += newRuntime;

                plat.overridden = true;
                plat.maxTextureSize = newMax;
                plat.format = TextureImporterFormat.ASTC_6x6;
                imp.SetPlatformTextureSettings(plat);
                changed++;
                rows.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3},{4},{5},{6},{7}", path, w, h, native, target, newMax, oldMax, oldFormat));
            }
            catch (System.Exception e)
            {
                errors++;
                Debug.LogError("[TextureCompress] 出错跳过 " + path + " : " + e.Message);
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log(string.Format(
            "[TextureCompress] total={0} changed={1} untouched={2} keepSmaller={3} noSource={4} errors={5} 运行时像素和(改动项) {6}->{7} ({8:F1}%) 耗时={9:F0}s",
            total, changed, untouched, keepSmaller, noSource, errors,
            oldPixels, newPixels, oldPixels > 0 ? 100f * newPixels / oldPixels : 100f,
            sw.Elapsed.TotalSeconds));
        try
        {
            string full = Path.GetFullPath(ReportPath);
            string dir = Path.GetDirectoryName(full);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(full, rows.ToString());
            Debug.Log("[TextureCompress] 明细已写 " + full);
        }
        catch { /* 批处理里写不了就不写 */ }
    }

    static int Pow2Ceil(int v)
    {
        int p = 32;
        while (p < v && p < 8192) p <<= 1;
        return p;
    }
}
