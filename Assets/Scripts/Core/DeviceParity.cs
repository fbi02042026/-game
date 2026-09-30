using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 编辑器 / 真机资源口径统一开关。
///
/// 2026-09-28 主人拍板：「不要兜底了，直接给我显示白框让我看出来才能改，
/// 你总是兜底我只有打包才能看到，太晚了。」
///
/// 背景：项目里有 30 多处 `#if UNITY_EDITOR` 分支，编辑器会回退到 `AssetDatabase`
/// 去读美术源目录，而真机只能 `Resources.Load`。后果就是「编辑器一切正常、真机图空或旧图」，
/// 只有打包上真机才暴露 —— 太晚。
///
/// 现在默认 `EditorFallbackEnabled = false`：编辑器也只认 Resources，
/// 缺什么就露什么（Image 的 sprite 为空 = 渲染成白框），配套 `ReportMissing` 打一条醒目 Error。
/// 想让编辑器恢复改动前的兜底体验，把这个值改回 true 即可，不需要改任何其他文件。
/// </summary>
public static class DeviceParity
{
    /// <summary>
    /// 编辑器是否允许回退到 AssetDatabase / 代码画的兜底图。
    /// false（默认，2026-09-28 起）= 真机口径：只认 Resources，缺就缺，露白框。
    /// true = 改动前的旧行为（编辑器读美术源目录，真机读 Resources）。
    /// </summary>
    public static bool EditorFallbackEnabled = false;

    /// <summary>同一路径只报一次，避免每帧刷屏。</summary>
    static readonly HashSet<string> _reported = new HashSet<string>();

    /// <summary>
    /// 缺资源上报。Console 里按 `[真机对齐]` 过滤即可拿到本次运行缺的全部资源清单。
    /// </summary>
    public static void ReportMissing(string resourcesPath, string what = null)
    {
        string key = resourcesPath ?? "(null)";
        if (!_reported.Add(key)) return;
        string tag = string.IsNullOrEmpty(what) ? "" : "（" + what + "）";
        Debug.LogError($"[真机对齐] Resources 缺资源{tag}: {resourcesPath} —— 编辑器已按真机口径放弃兜底，此处会显示白框/空白。补图或改路径。");
    }

    /// <summary>清空上报去重表（重新进入一局时想再看一次清单可调）。</summary>
    public static void ResetReported() => _reported.Clear();
}
