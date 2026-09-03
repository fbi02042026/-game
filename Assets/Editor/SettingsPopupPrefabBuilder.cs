#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 生成全局设置弹窗预制体（登录 / 城镇 / 战斗共用）。
/// 菜单：Tools/UI/生成设置弹窗预制体。已存在会询问是否覆盖。
/// 批处理：-executeMethod SettingsPopupPrefabBuilder.BuildBatch
/// </summary>
public static class SettingsPopupPrefabBuilder
{
    const string PrefabPath = "Assets/Resources/Prefabs/UI/SettingsPopup.prefab";

    [MenuItem("Tools/UI/生成设置弹窗预制体")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            if (!EditorUtility.DisplayDialog(
                    "设置弹窗",
                    "已存在 SettingsPopup.prefab。\n是否覆盖？\n（会丢掉你在预制体上的手改）",
                    "覆盖", "取消"))
                return;
        }

        SavePrefab();
    }

    /// <summary>批处理：-executeMethod SettingsPopupPrefabBuilder.BuildBatch</summary>
    public static void BuildBatch()
    {
        SavePrefab();
    }

    static void SavePrefab()
    {
        EnsureFolders();
        var root = new GameObject("SettingsPopup", typeof(RectTransform));
        try
        {
            var ui = root.AddComponent<SettingsPopupUI>();
            ui.BuildFallbackHierarchy();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            if (!success || prefab == null)
            {
                Debug.LogError("[SettingsPopup] 保存预制体失败");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log("[SettingsPopup] 已生成：" + PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    static void EnsureFolders()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs"))
            AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs/UI"))
            AssetDatabase.CreateFolder("Assets/Resources/Prefabs", "UI");
    }
}
#endif
