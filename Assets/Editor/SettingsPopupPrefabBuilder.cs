#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 生成全局设置弹窗预制体（登录 / 城镇 / 战斗共用）。
/// 菜单：Tools/UI/生成设置弹窗预制体。已存在会询问是否覆盖。
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

        var root = new GameObject("SettingsPopup", typeof(RectTransform));
        var ui = root.AddComponent<SettingsPopupUI>();
        ui.BuildFallbackHierarchy();

        EnsureFolders();
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (prefab != null)
        {
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            EditorUtility.DisplayDialog("完成",
                "已生成：\n" + PrefabPath + "\n\n" +
                "登录 / 城镇 / 战斗共用；运行时按场景显隐「撤离」。\n" +
                "可在 Inspector 换美术；预留 Music/Sfx/Weather 行默认隐藏。",
                "好");
        }
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs"))
            AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs/UI"))
            AssetDatabase.CreateFolder("Assets/Resources/Prefabs", "UI");
    }
}
#endif
