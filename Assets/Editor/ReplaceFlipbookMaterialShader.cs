using UnityEditor;
using UnityEngine;
using System.Linq;

/// <summary>
/// 把 Flipbook VFX URP 包里所有引用 Vefects 自定义 shader 的材质，
/// 一键替换为 Unity 自带 URP 无光照 shader（Universal Render Pipeline/Unlit）。
/// 目的：让那些缺失 include / 宏的自定义 shader 不再被引用 → 不参与编译 → 消除报错，
/// 同时贴图与粒子动画保留，特效仍可显示播放。
///
/// 用法：团结编辑器顶部菜单 Tools/特效/Flipbook材质替换为URP无光照。
/// 仅扫描 Assets/Art/Effects/Flipbook VFX URP，不影响其他包。
/// </summary>
public class ReplaceFlipbookMaterialShader
{
    private const string RootFolder = "Assets/Art/Effects/Flipbook VFX URP";
    private const string TargetShaderName = "Universal Render Pipeline/Unlit";

    [MenuItem("Tools/特效/Flipbook材质替换为URP无光照")]
    public static void Replace()
    {
        Shader target = Shader.Find(TargetShaderName);
        if (target == null)
        {
            Debug.LogError("[ReplaceFlipbook] 未找到内置 shader: " + TargetShaderName +
                           "。请确认当前为 URP 项目且 URP 包已导入。");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { RootFolder });
        int replaced = 0;
        int skipped = 0;

        foreach (string g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null || m.shader == null)
            {
                skipped++;
                continue;
            }

            // 只替换引用 Vefects 自定义 shader 的材质；已替换过的（URP Unlit）自动跳过。
            if (!m.shader.name.Contains("Vefects"))
            {
                skipped++;
                continue;
            }

            // 保留主贴图：Vefects 包常用 _MainTex / _MainTexture，URP Unlit 用 _BaseMap。
            Texture main = m.GetTexture("_MainTex")
                       ?? m.GetTexture("_MainTexture")
                       ?? m.GetTexture("_BaseMap");

            m.shader = target;

            if (main != null)
                m.SetTexture("_BaseMap", main);

            // 设为透明表面，保证特效的 alpha / 半透明正常（溶解、辉光等仍会丢失，仅保留基础显示）。
            m.SetFloat("_Surface", 1f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            EditorUtility.SetDirty(m);
            replaced++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[ReplaceFlipbook] 已将 " + replaced + " 个材质替换为 URP 无光照，" +
                  "跳过 " + skipped + " 个（非 Vefects 或已处理）。" +
                  "注意：原 Vefects 自定义 shader 现已无材质引用 → 不再编译 → 报错消除。" +
                  "如需 Additive（叠加）混合，请在 Inspector 的 Surface/Blending 模式里按需微调。");
    }
}
