#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 生成角色界面预制体（含技能选择弹层、战斗同款背包格）。
/// 菜单：Tools/UI/生成角色界面预制体
/// </summary>
public static class CharacterUIPrefabGenerator
{
    const string PrefabPath = "Assets/Resources/Prefabs/Town/CharacterUI.prefab";

    [MenuItem("Tools/_归档/UI/生成角色界面预制体")]
    public static void Generate()
    {
        EnsureFolders();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            if (!EditorUtility.DisplayDialog(
                    "角色界面预制体",
                    "已存在 CharacterUI.prefab，是否覆盖？\n（你改过的 Sprite 会被盖掉）",
                    "覆盖", "取消"))
                return;
        }
        BuildAndSave(true);
    }

    public static void GenerateBatch()
    {
        EnsureFolders();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            return;
        BuildAndSave(false);
    }

    static void BuildAndSave(bool showDialog)
    {
        var root = new GameObject("CharacterUI", typeof(RectTransform));
        var canvas = root.AddComponent<Canvas>();
        root.AddComponent<CanvasScaler>();
        root.AddComponent<GraphicRaycaster>();
        UICanvasSetup.Apply(canvas, null);
        canvas.sortingOrder = 20;

        var ui = root.AddComponent<CharacterUI>();
        ui.BuildHierarchyForPrefab();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[CharacterUIPrefabGenerator] 已生成: {PrefabPath}");
        if (showDialog)
        {
            EditorUtility.DisplayDialog("角色界面",
                "已生成 Resources/Prefabs/Town/CharacterUI.prefab\n\n" +
                "· 无左侧装备栏\n" +
                "· 右上天赋/技能；左侧独立按钮也可开技能\n" +
                "· 背包 7×4 与战斗格子一致（含底行锁）\n" +
                "· 底部留出五入口，运行时 RaiseSharedChrome\n" +
                "参考：Art/UI/Character/",
                "OK");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null) Selection.activeObject = prefab;
        }
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs/Town"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs"))
                AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs/Town"))
                AssetDatabase.CreateFolder("Assets/Resources/Prefabs", "Town");
        }
    }
}
#endif
