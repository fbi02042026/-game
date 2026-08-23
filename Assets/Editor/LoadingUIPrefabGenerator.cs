#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 生成 Loading 预制体（与运行时 BattleLoadingOverlay 布局一致）。
/// 菜单：Tools/UI/生成Loading界面预制体。已存在则确认后才覆盖。
/// </summary>
public static class LoadingUIPrefabGenerator
{
    const string PrefabPath = "Assets/Resources/Prefabs/Loading/LoadingUI.prefab";

    [MenuItem("Tools/_归档/UI/生成Loading界面预制体")]
    public static void Generate()
    {
        EnsureFolders();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            if (!EditorUtility.DisplayDialog(
                    "Loading 预制体",
                    "已存在 LoadingUI.prefab，是否覆盖？\n（你改过的 Sprite 会被盖掉）",
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
        var root = new GameObject("LoadingUI", typeof(RectTransform));
        var canvas = root.AddComponent<Canvas>();
        root.AddComponent<CanvasScaler>();
        root.AddComponent<GraphicRaycaster>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 9999;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.referenceResolution = new Vector2(GameConfig.DESIGN_WIDTH, GameConfig.DESIGN_HEIGHT);
        scaler.matchWidthOrHeight = GameConfig.UI_MATCH;

        var ui = root.AddComponent<LoadingUI>();
        ui.BuildHierarchyForPrefab();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[LoadingUIPrefabGenerator] 已生成: {PrefabPath}");
        if (showDialog)
        {
            EditorUtility.DisplayDialog("Loading",
                "已生成 Resources/Prefabs/Loading/LoadingUI.prefab\n\n" +
                "全屏背景 UI/loading/loading01\n" +
                "中下部剧情提示（中文 fusion-pixel）\n" +
                "右下角「加载中」+ 百分比（数字 PixelFont）\n" +
                "跨场景用 Overlay sortingOrder 9999",
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
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs/Loading"))
            AssetDatabase.CreateFolder("Assets/Resources/Prefabs", "Loading");
    }
}
#endif
