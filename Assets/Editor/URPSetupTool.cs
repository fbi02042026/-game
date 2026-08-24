#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using System;
using System.Reflection;

/// <summary>
/// URP渲染管线自动配置工具（v4 - 彻底重建版）
/// 菜单：Tools/配置URP渲染管线
///
/// 改进：检测到 RendererData 引用断裂时，删除旧资产并从零重建
/// </summary>
public static class URPSetupTool
{
    private const string URP_ASSET_PATH = "Assets/Settings/URPAsset.asset";
    private const string URP_RENDERER_PATH = "Assets/Settings/URPRendererData.asset";
    private const string URP_GLOBAL_SETTINGS_PATH = "Assets/UniversalRenderPipelineGlobalSettings.asset";
    private const string FLAG_PATH = "Library/URPSetupTool_Initialized.flag";

    private static bool s_Initialized = false;

    /// <summary>
    /// 编辑器加载后自动检测并修复
    /// </summary>
    [InitializeOnLoadMethod]
    static void AutoInit()
    {
        EditorApplication.delayCall += DeferredAutoFix;
    }

    static void DeferredAutoFix()
    {
        if (s_Initialized) return;
        s_Initialized = true;

        try
        {
            AutoFixIfBroken();
        }
        catch (Exception e)
        {
            Debug.LogError("[URPSetupTool] 自动修复异常: " + e.Message + "\n" + e.StackTrace);
        }
    }

    /// <summary>
    /// 检查URP配置是否完整，如果断裂则彻底重建
    /// </summary>
    static void AutoFixIfBroken()
    {
        Type assetType = FindType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset");
        Type rendererDataType = FindType("UnityEngine.Rendering.Universal.UniversalRendererData");
        if (assetType == null || rendererDataType == null)
        {
            Debug.LogWarning("[URPSetupTool] URP类型未找到，跳过自动修复");
            return;
        }

        // 检查 GraphicsSettings 是否已配置
        RenderPipelineAsset currentPipeline = GraphicsSettings.defaultRenderPipeline;
        if (currentPipeline != null)
        {
            // 已有 pipeline，检查 renderer data 是否有效
            ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(URP_ASSET_PATH);
            if (asset != null)
            {
                SerializedObject so = new SerializedObject(asset);
                if (!CheckRendererDataNull(so))
                {
                    // 配置完好，无需修复
                    return;
                }
            }
        }

        Debug.LogWarning("[URPSetupTool] URP配置缺失或断裂，开始重建...");
        RebuildURP(assetType, rendererDataType);
    }

    /// <summary>
    /// 检查 rendererData 是否为 null
    /// </summary>
    static bool CheckRendererDataNull(SerializedObject so)
    {
        SerializedProperty rdlProp = so.FindProperty("m_RendererDataList");
        if (rdlProp != null && rdlProp.isArray)
        {
            if (rdlProp.arraySize == 0)
                return true;
            return rdlProp.GetArrayElementAtIndex(0).objectReferenceValue == null;
        }

        SerializedProperty rdProp = so.FindProperty("m_RendererData");
        return rdProp == null || rdProp.objectReferenceValue == null;
    }

    /// <summary>
    /// 彻底重建 URP 资产
    /// </summary>
    static void RebuildURP(Type assetType, Type rendererDataType)
    {
        // 1. 确保文件夹存在
        if (!AssetDatabase.IsValidFolder("Assets/Settings"))
            AssetDatabase.CreateFolder("Assets", "Settings");

        // 2. 删除旧资产（如果存在）
        DeleteAssetIfExists(URP_ASSET_PATH);
        DeleteAssetIfExists(URP_RENDERER_PATH);

        // 3. 强制刷新以确保删除生效
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 4. 创建新的 RendererData
        ScriptableObject rendererData = (ScriptableObject)ScriptableObject.CreateInstance(rendererDataType);
        AssetDatabase.CreateAsset(rendererData, URP_RENDERER_PATH);
        Debug.Log("[URPSetupTool] 创建新 RendererData: " + URP_RENDERER_PATH);

        // 5. 创建新的 URPAsset
        ScriptableObject asset = (ScriptableObject)ScriptableObject.CreateInstance(assetType);
        AssetDatabase.CreateAsset(asset, URP_ASSET_PATH);
        Debug.Log("[URPSetupTool] 创建新 URPAsset: " + URP_ASSET_PATH);

        // 6. 用 SerializedObject 设置 rendererDataList
        SerializedObject so = new SerializedObject(asset);

        SerializedProperty rdlProp = so.FindProperty("m_RendererDataList");
        if (rdlProp != null && rdlProp.isArray)
        {
            rdlProp.arraySize = 1;
            rdlProp.GetArrayElementAtIndex(0).objectReferenceValue = rendererData;
            Debug.Log("[URPSetupTool] 设置 m_RendererDataList 成功");
        }
        else
        {
            SerializedProperty rdProp = so.FindProperty("m_RendererData");
            if (rdProp != null)
            {
                rdProp.objectReferenceValue = rendererData;
                Debug.Log("[URPSetupTool] 通过 m_RendererData 设置成功");
            }
            else
            {
                Debug.LogError("[URPSetupTool] 找不到 rendererDataList 字段！");
                return;
            }
        }

        // 7. 设置 defaultRendererIndex
        SerializedProperty driProp = so.FindProperty("m_DefaultRendererIndex");
        if (driProp != null) driProp.intValue = 0;

        // 8. 2D游戏推荐设置
        SerializedProperty prop;
        prop = so.FindProperty("m_RequireOpaqueTexture");
        if (prop != null) prop.boolValue = true;
        prop = so.FindProperty("m_SupportsHDR");
        if (prop != null) prop.boolValue = false;
        prop = so.FindProperty("m_MSAA");
        if (prop != null) prop.intValue = 1;

        so.ApplyModifiedPropertiesWithoutUndo();

        // 9. 配置到 GraphicsSettings
        GraphicsSettings.defaultRenderPipeline = asset as RenderPipelineAsset;
        Debug.Log("[URPSetupTool] GraphicsSettings.defaultRenderPipeline 已赋值");

        // 10. 尝试配置 SRPDefaultSettings
        TryAssignSRPDefaultSettings();

        EditorUtility.SetDirty(asset);
        EditorUtility.SetDirty(rendererData);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 11. 验证修复结果
        VerifyFix();

        Debug.Log("[URPSetupTool] URP 重建完成！");
    }

    /// <summary>
    /// 验证修复是否成功
    /// </summary>
    static void VerifyFix()
    {
        ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(URP_ASSET_PATH);
        if (asset == null)
        {
            Debug.LogError("[URPSetupTool] 验证失败：URPAsset 不存在");
            return;
        }

        SerializedObject so = new SerializedObject(asset);
        SerializedProperty rdlProp = so.FindProperty("m_RendererDataList");

        if (rdlProp == null || !rdlProp.isArray || rdlProp.arraySize == 0)
        {
            Debug.LogError("[URPSetupTool] 验证失败：m_RendererDataList 为空");
            return;
        }

        SerializedProperty elem = rdlProp.GetArrayElementAtIndex(0);
        if (elem.objectReferenceValue == null)
        {
            Debug.LogError("[URPSetupTool] 验证失败：m_RendererDataList[0] 引用为 null");
            return;
        }

        Debug.Log("[URPSetupTool] 验证通过：m_RendererDataList[0] = " + elem.objectReferenceValue.name);

        // 验证 GraphicsSettings
        if (GraphicsSettings.defaultRenderPipeline == null)
        {
            Debug.LogError("[URPSetupTool] 验证失败：GraphicsSettings.defaultRenderPipeline 为 null");
            return;
        }

        Debug.Log("[URPSetupTool] 验证通过：GraphicsSettings.defaultRenderPipeline = " + GraphicsSettings.defaultRenderPipeline.name);
    }

    static void DeleteAssetIfExists(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
        {
            AssetDatabase.DeleteAsset(path);
            Debug.Log("[URPSetupTool] 删除旧资产: " + path);
        }
    }

    [MenuItem("Tools/_归档/配置URP渲染管线")]
    public static void SetupURP()
    {
        Type assetType = FindType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset");
        Type rendererDataType = FindType("UnityEngine.Rendering.Universal.UniversalRendererData");

        if (assetType == null || rendererDataType == null)
        {
            EditorUtility.DisplayDialog("URP配置失败",
                "未找到URP类型。\n请确保URP包已安装。\n\n检查路径：Package Manager > Universal RP", "确定");
            return;
        }

        bool reconfigure = true;
        if (GraphicsSettings.defaultRenderPipeline != null)
        {
            reconfigure = EditorUtility.DisplayDialog("URP配置",
                "URP已配置: " + GraphicsSettings.defaultRenderPipeline.name + "\n是否删除并重建？",
                "重建", "取消");
        }

        if (!reconfigure) return;

        RebuildURP(assetType, rendererDataType);

        EditorUtility.DisplayDialog("URP配置完成",
            "URP渲染管线已重建完成！\n\n" +
            "新资产已创建并正确链接。\n\n" +
            "Asset: " + URP_ASSET_PATH + "\n" +
            "Renderer: " + URP_RENDERER_PATH, "确定");
    }

    /// <summary>
    /// 尝试配置 SRPDefaultSettings
    /// </summary>
    static void TryAssignSRPDefaultSettings()
    {
        try
        {
            ScriptableObject globalSettings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(URP_GLOBAL_SETTINGS_PATH);
            if (globalSettings == null)
                return;

            Type rpType = FindType("UnityEngine.Rendering.Universal.UniversalRenderPipeline");
            if (rpType == null)
                return;

            MethodInfo registerMethod = typeof(GraphicsSettings)
                .GetMethod("RegisterRenderPipelineSettings", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

            if (registerMethod != null)
            {
                MethodInfo generic = registerMethod.MakeGenericMethod(rpType);
                generic.Invoke(null, new object[] { globalSettings });
                Debug.Log("[URPSetupTool] SRPDefaultSettings 已配置");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[URPSetupTool] SRPDefaultSettings 配置跳过: " + e.Message);
        }
    }

    static Type FindType(string fullName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = asm.GetType(fullName);
            if (t != null) return t;
        }
        return null;
    }
}
#endif
