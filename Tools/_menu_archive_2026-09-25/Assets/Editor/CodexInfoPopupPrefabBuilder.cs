#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 生成图鉴信息弹窗：Resources/Prefabs/Town/CodexInfoPopup.prefab
/// 已存在时确认，绝不静默覆盖。
/// </summary>
public static class CodexInfoPopupPrefabBuilder
{
    const string PrefabAssetPath = "Assets/Resources/Prefabs/Town/CodexInfoPopup.prefab";

    [MenuItem("Tools/UI/生成图鉴信息弹窗预制体")]
    public static void Build()
    {
        if (File.Exists(PrefabAssetPath))
        {
            bool ok = EditorUtility.DisplayDialog("图鉴信息弹窗",
                "已存在预制体：\n" + PrefabAssetPath +
                "\n\n覆盖会丢掉你在 Inspector 里换过的图和位置。\n确定重新生成吗？",
                "覆盖重新生成", "取消");
            if (!ok) return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(PrefabAssetPath));
        var host = new GameObject("CodexInfoPopup", typeof(RectTransform));
        try
        {
            CodexInfoPopupUI.BuildHierarchy(host);
            var saved = PrefabUtility.SaveAsPrefabAsset(host, PrefabAssetPath, out bool success);
            if (!success || saved == null)
            {
                EditorUtility.DisplayDialog("图鉴信息弹窗", "保存失败，请看 Console。", "好的");
                return;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = saved;
            EditorGUIUtility.PingObject(saved);
            EditorUtility.DisplayDialog("图鉴信息弹窗",
                "已生成：\n" + PrefabAssetPath +
                "\n\n换图节点：Panel / PortraitFrame / Portrait / CloseButton\n" +
                "文案节点：Title / Meta / Desc / Lore",
                "好的");
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    /// <summary>批处理：-executeMethod CodexInfoPopupPrefabBuilder.BuildBatch</summary>
    public static void BuildBatch()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabAssetPath));
        var host = new GameObject("CodexInfoPopup", typeof(RectTransform));
        try
        {
            CodexInfoPopupUI.BuildHierarchy(host);
            PrefabUtility.SaveAsPrefabAsset(host, PrefabAssetPath);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }
}
#endif
