using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 微信小游戏子平台（activeSubplatform=1）下，URP 14.2.0-t1 的 ShadingRateImageGenerator
/// 无条件调用 ComputeShader.FindKernel("GenerateLuminanceSRI_8")。
/// 该子平台按 WebGL 导入 shader，compute kernel 被剥离；但编辑器宿主仍报 supportsComputeShaders=true，
/// 于是 FindKernel 抛 ArgumentException，每帧 SceneView.Repaint 刷异常导致编辑器卡死。
///
/// 本脚本在每次程序集加载时检查包源码是否已有保护，缺失则自动打补丁（Initialize 顶部直接 return）。
/// VRS 本项目未启用，跳过初始化不影响任何画面。
/// 幂等：已有标记则跳过；不修改项目内任何资源，只改 Library/PackageCache 下的包源码。
///
/// 注意：URP 是 builtin 包，Unity 每次启动会从引擎安装目录还原到 Library/PackageCache。
/// 本机已在引擎源目录同步打补丁（...BuiltInPackages/com.unity.render-pipelines.universal/...），
/// 因此正常启动不会触发本脚本。本脚本仅作为换机/引擎重装后的兜底：首次启动仍会报一轮，
/// 脚本补上并重编译后即稳定；换机后按同样方式给引擎源打补丁即可一劳永逸。
/// </summary>
[InitializeOnLoad]
internal static class VrsKernelGuardPatcher
{
    const string Marker = "// [VrsKernelGuard]";
    const string RelDir = "Library/PackageCache/com.unity.render-pipelines.universal@14.2.0-t1/" +
                          "Runtime/RendererFeatures/ImageBasedVRS/ShadingRateImageGenerator.cs";

    // VRS is never enabled in this project (m_RendererFeatures empty, VRS mode 0).
    // Return at the very top of Initialize so no FindKernel is ever called under the
    // minigame subplatform (where WebGL strips compute kernels and the editor spams errors).
    const string OldBlock =
        "        internal void Initialize(ComputeShader generateShader, ComputeShader reprojectShader, Shader convertShader)\n" +
        "        {\n" +
        "\n" +
        "            if (!SystemInfo.supportsComputeShaders)\n" +
        "                return;";

    const string NewBlock =
        "        internal void Initialize(ComputeShader generateShader, ComputeShader reprojectShader, Shader convertShader)\n" +
        "        {\n" +
        "            " + Marker + " VRS unused in this project; skip init to avoid WebGL kernel-strip hang.\n" +
        "            return;\n" +
        "\n" +
        "            if (!SystemInfo.supportsComputeShaders)\n" +
        "                return;";

    static VrsKernelGuardPatcher()
    {
        EditorApplication.delayCall += Patch;
    }

    static void Patch()
    {
        try
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string path = Path.Combine(projectRoot, RelDir.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                Debug.LogWarning("[VrsKernelGuard] 未找到 URP 包源码，跳过：" + path);
                return;
            }

            string text = File.ReadAllText(path);
            if (text.IndexOf(Marker, StringComparison.Ordinal) >= 0)
                return; // 已打补丁

            if (!text.Contains(OldBlock))
            {
                Debug.LogWarning("[VrsKernelGuard] URP 包源码结构变化，未找到目标代码块，跳过自动补丁：" + path);
                return;
            }

            File.WriteAllText(path, text.Replace(OldBlock, NewBlock));
            AssetDatabase.Refresh();
            Debug.Log("[VrsKernelGuard] 已为 URP VRS 初始化加上 kernel 异常保护（下次脚本编译生效）。");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[VrsKernelGuard] 自动补丁失败：" + e.Message);
        }
    }
}
