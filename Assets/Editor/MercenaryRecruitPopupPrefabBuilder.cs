#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 生成招募佣兵弹窗预制体（不存在才写；已存在会先询问）。
/// 菜单：Tools/UI/生成招募佣兵弹窗预制体
/// </summary>
public static class MercenaryRecruitPopupPrefabBuilder
{
    const string PrefabPath = "Assets/Resources/Prefabs/Town/MercenaryRecruitPopup.prefab";

    [MenuItem("Tools/UI/生成招募佣兵弹窗预制体")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            if (!EditorUtility.DisplayDialog(
                    "招募佣兵弹窗",
                    "已存在 MercenaryRecruitPopup.prefab。\n是否覆盖？\n（会丢掉你在预制体上的手改）",
                    "覆盖", "取消"))
                return;
        }

        var root = new GameObject("MercenaryRecruitPopup", typeof(RectTransform));
        var ui = root.AddComponent<MercenaryRecruitPopupUI>();
        ui.BuildFallbackHierarchy();

        string dir = System.IO.Path.GetDirectoryName(PrefabPath).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(dir))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs"))
                AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs/Town"))
                AssetDatabase.CreateFolder("Assets/Resources/Prefabs", "Town");
        }

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (prefab != null)
        {
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            EditorUtility.DisplayDialog("完成",
                "已生成：\n" + PrefabPath + "\n\n可在 Inspector 替换底图/立绘框等美术。\n运行时优先加载此预制体。",
                "好");
        }
    }
}
#endif
