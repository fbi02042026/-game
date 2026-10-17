using UnityEditor;
using UnityEngine;

/// <summary>
/// 编辑器侧的 _UnscaledTime 驱动：让 Shine 流光在非 Play 模式（场景 / 预制体预览）下也能播放。
/// 运行时由 UnscaledShaderTime 组件负责，这里遇到 Play 模式直接跳过，避免两个时间源打架。
/// </summary>
[InitializeOnLoad]
public static class UnscaledShaderTimeEditor
{
    static readonly int UnscaledTimeId = Shader.PropertyToID("_UnscaledTime");

    static UnscaledShaderTimeEditor()
    {
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (Application.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;
        Shader.SetGlobalFloat(UnscaledTimeId, (float)EditorApplication.timeSinceStartup);
    }
}
