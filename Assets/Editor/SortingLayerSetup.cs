#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// 自动创建项目所需的 SortingLayer
/// 编辑器加载时自动执行，也可通过 Tools → 设置排序层 手动触发
///
/// 排序层级（从底到顶）：
///   Background → Ground → Monster → Player → Effects
/// </summary>
[InitializeOnLoad]
public static class SortingLayerSetup
{
    /// <summary>需要存在的排序层名称（顺序即优先级，越后越在上层）</summary>
    private static readonly string[] RequiredLayers =
    {
        "Background",
        "Ground",
        "Monster",
        "Player",
        "Effects"
    };

    static SortingLayerSetup()
    {
        // 延迟执行，避免在编译过程中过早调用 SerializedObject API
        EditorApplication.delayCall += EnsureSortingLayers;
    }

    [MenuItem("Tools/设置排序层")]
    public static void EnsureSortingLayers()
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var sortingLayersProp = tagManager.FindProperty("m_SortingLayers");
        if (sortingLayersProp == null)
        {
            Debug.LogError("[SortingLayerSetup] 找不到 m_SortingLayers 属性");
            return;
        }

        bool changed = false;

        foreach (string layerName in RequiredLayers)
        {
            if (SortingLayer.NameToID(layerName) != 0 || LayerExists(sortingLayersProp, layerName))
                continue;

            // 在数组末尾添加新元素
            sortingLayersProp.arraySize++;
            var newElement = sortingLayersProp.GetArrayElementAtIndex(sortingLayersProp.arraySize - 1);

            newElement.FindPropertyRelative("name").stringValue = layerName;
            newElement.FindPropertyRelative("uniqueID").intValue = layerName.GetHashCode() & 0x7FFFFFFF;
            newElement.FindPropertyRelative("locked").boolValue = false;

            changed = true;
            Debug.Log($"[SortingLayerSetup] 已添加排序层: {layerName}");
        }

        if (changed)
        {
            tagManager.ApplyModifiedProperties();
            Debug.Log("[SortingLayerSetup] 排序层设置完成");
        }
    }

    /// <summary>检查某排序层是否已存在</summary>
    private static bool LayerExists(SerializedProperty prop, string name)
    {
        for (int i = 0; i < prop.arraySize; i++)
        {
            var element = prop.GetArrayElementAtIndex(i);
            var nameProp = element.FindPropertyRelative("name");
            if (nameProp != null && nameProp.stringValue == name)
                return true;
        }
        return false;
    }
}
#endif
