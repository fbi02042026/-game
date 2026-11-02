#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 【2026-11-02 P1-B】音频转码工具：把进包的音频（Resources 下 AudioClip）统一
/// ForceToMono + Vorbis 低码率 + 采样率降档，显著降低微信小游戏构建数据文件体积。
///
/// 背景：Resources/Audio 约 44MB（BGM mp3 2~5MB ×10、战斗失败.wav 5MB），
/// 全部进包。微信小游戏要求音频 mp3/ogg 且压码率，这条是最容易见效的瘦身点。
///
/// 档位（2026-11-02 二档收紧，主人反馈"少了一点"）：
///   BGM：Vorbis quality 0.5 + 32kHz；SFX：quality 0.4 + 22kHz；全部 ForceToMono。
///   预期：44MB 源 → 构建内约 6~9MB（省 35MB+）。像素/芯片风听感基本无损；
///   试听若觉发闷，把 Q_BGM 回 0.6、SR_BGM 回 44100 单独回调即可。
///
/// 说明：Unity 在构建时按导入设置重新编码，"转码"本质是改 AudioImporter 设置，
/// 不需要手工 ffmpeg 替换源文件（源 WAV/MP3 保留便于迭代）。
/// 用法：菜单「微信小游戏/包体优化/音频转码(预览)」先看影响面，再「音频转码(应用)」。
/// </summary>
public static class AudioTranscodeMenu
{
    const string MenuPreview = "微信小游戏/包体优化/音频转码(预览)";
    const string MenuApply = "微信小游戏/包体优化/音频转码(应用)";

    // 这些目录下的 AudioClip 视为进包资源（Resources 全进包；Resources 外被引用者也进包，这里一并扫）
    static readonly string[] ScanRoots = { "Assets/Resources" };

    // 二档收紧参数：BGM 稍高保听感，SFX 压更低
    const float Q_BGM = 0.5f;
    const float Q_SFX = 0.4f;
    const int SR_BGM = 32000;
    const int SR_SFX = 22050;

    static bool IsBgm(string name)
    {
        var n = name.ToLower();
        return n.Contains("bgm") || n.Contains("music") || n.Contains("背景") || n.Contains("主题");
    }

    static void Fill(AudioImporterSampleSettings st, float q, int sr)
    {
        st.compressionFormat = AudioCompressionFormat.Vorbis;
        st.quality = q;
        st.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
        st.sampleRateOverride = (uint)sr;   // 团结引擎此字段为 uint
        st.loadType = AudioClipLoadType.CompressedInMemory;
    }

    static bool Same(AudioImporterSampleSettings st, float q, int sr)
    {
        return st.compressionFormat == AudioCompressionFormat.Vorbis
            && st.quality == q
            && st.sampleRateSetting == AudioSampleRateSetting.OverrideSampleRate
            && st.sampleRateOverride == sr
            && st.loadType == AudioClipLoadType.CompressedInMemory;
    }

    [MenuItem(MenuPreview)]
    static void Preview()
    {
        var clips = Collect();
        long totalBytes = 0;
        int bigCount = 0;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[音频转码预览] 命中 AudioClip：{clips.Count}");
        foreach (var p in clips)
        {
            var fi = new FileInfo(p);
            totalBytes += fi.Length;
            if (fi.Length > 1 * 1024 * 1024) { bigCount++; sb.AppendLine($"  >1MB: {fi.Length / 1024 / 1024}MB  {p}"); }
        }
        sb.AppendLine($"源文件总大小：{totalBytes / 1024 / 1024}MB；>1MB 文件数：{bigCount}");
        sb.AppendLine($"应用后预计：ForceToMono + Vorbis(BGM {Q_BGM}/{SR_BGM}Hz、SFX {Q_SFX}/{SR_SFX}Hz) → 构建内约 6~9MB（省 35MB+）。");
        Debug.Log(sb.ToString());
    }

    [MenuItem(MenuApply)]
    static void Apply()
    {
        var clips = Collect();
        int done = 0;
        foreach (var p in clips)
        {
            var ai = AssetImporter.GetAtPath(p) as AudioImporter;
            if (ai == null) continue;
            bool changed = false;

            bool isBgm = IsBgm(Path.GetFileNameWithoutExtension(p));
            float q = isBgm ? Q_BGM : Q_SFX;
            int sr = isBgm ? SR_BGM : SR_SFX;

            if (!ai.forceToMono) { ai.forceToMono = true; changed = true; }

            var d = ai.defaultSampleSettings;
            if (!Same(d, q, sr)) { Fill(d, q, sr); ai.defaultSampleSettings = d; changed = true; }

            // 微信小游戏构建目标按 WebGL 处理；同时压安卓/iOS 以兼容其它出包路径。
            // 注：是否启用平台覆盖由 Get/SetOverrideSampleSettings 本身控制，
            //     AudioImporterSampleSettings 结构体上没有 overrideSampleSettings 字段。
            foreach (var plat in new[] { "WebGL", "Android", "iPhone" })
            {
                var ps = ai.GetOverrideSampleSettings(plat);
                if (!Same(ps, q, sr)) { Fill(ps, q, sr); ai.SetOverrideSampleSettings(plat, ps); changed = true; }
            }

            if (changed) { ai.SaveAndReimport(); done++; }
        }
        Debug.Log($"[音频转码应用] 已更新 {done}/{clips.Count} 个 AudioClip（ForceToMono + Vorbis BGM {Q_BGM}/SFX {Q_SFX} + 采样率降档）。请试听回归，并观察构建数据文件体积下降。");
        AssetDatabase.SaveAssets();
    }

    static List<string> Collect()
    {
        var result = new List<string>();
        foreach (var root in ScanRoots)
        {
            var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { root });
            foreach (var g in guids)
                result.Add(AssetDatabase.GUIDToAssetPath(g));
        }
        return result;
    }
}
#endif
