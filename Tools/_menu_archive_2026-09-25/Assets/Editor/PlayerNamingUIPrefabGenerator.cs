#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 仅首次白模：已有 PlayerNamingUI.prefab 时拒绝覆盖（用户手改后以代码小修，禁止重生覆盖）。
/// </summary>
public static class PlayerNamingUIPrefabGenerator
{
    const string PrefabPath = "Assets/Resources/Prefabs/Town/PlayerNamingUI.prefab";

    [MenuItem("Tools/_归档/UI/生成起名界面预制体（仅首次白模）")]
    public static void Generate()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            EditorUtility.DisplayDialog("起名界面预制体",
                "已存在 PlayerNamingUI.prefab，禁止覆盖。\n\n" +
                "请直接在 Inspector 改 Sprite/布局；逻辑问题只改 PlayerNamingUI.cs。",
                "OK");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            return;
        }

        EnsureFolders();
        BuildAndSave(showDialog: true);
    }

    public static void GenerateBatch()
    {
        EnsureFolders();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            return;
        BuildAndSave(showDialog: false);
    }

    static void BuildAndSave(bool showDialog)
    {
        var root = new GameObject("PlayerNamingUI", typeof(RectTransform));
        var rootRt = root.GetComponent<RectTransform>();
        rootRt.localScale = Vector3.one;
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;
        rootRt.pivot = new Vector2(0.5f, 0.5f);

        var canvas = root.AddComponent<Canvas>();
        root.AddComponent<CanvasScaler>();
        root.AddComponent<GraphicRaycaster>();
        UICanvasSetup.Apply(canvas, null);
        canvas.overrideSorting = true;
        canvas.sortingOrder = 560;

        var ui = root.AddComponent<PlayerNamingUI>();
        ui.BuildHierarchyForPrefab();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[PlayerNamingUIPrefabGenerator] 已生成: {PrefabPath}");
        if (showDialog)
        {
            EditorUtility.DisplayDialog("起名界面",
                "已生成 Resources/Prefabs/Town/PlayerNamingUI.prefab（仅白模）。\n" +
                "之后请只在 Inspector 改资源，勿再跑本菜单。",
                "OK");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null) Selection.activeObject = prefab;
        }
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs"))
            AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs/Town"))
            AssetDatabase.CreateFolder("Assets/Resources/Prefabs", "Town");
    }
}
#endif
