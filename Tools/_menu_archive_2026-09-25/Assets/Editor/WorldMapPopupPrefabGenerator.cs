#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 生成世界地图弹窗预制体。
/// 菜单：Tools/_归档/UI/生成世界地图弹窗预制体
/// </summary>
public static class WorldMapPopupPrefabGenerator
{
    const string PrefabPath = "Assets/Resources/Prefabs/UI/WorldMapPopup.prefab";

    [MenuItem("Tools/_归档/UI/生成世界地图弹窗预制体")]
    public static void Generate()
    {
        GenerateInternal(showDialog: true);
    }

    public static void GenerateBatch()
    {
        GenerateInternal(showDialog: false);
    }

    static void GenerateInternal(bool showDialog)
    {
        EnsureFolders();

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            if (showDialog && !EditorUtility.DisplayDialog(
                    "世界地图弹窗预制体",
                    "已存在 WorldMapPopup.prefab，是否覆盖？\n（你改过的 Sprite 会被盖掉）",
                    "覆盖", "取消"))
                return;
        }

        var root = new GameObject("WorldMapPopup", typeof(RectTransform));
        var canvas = root.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownPopup);

        var popup = root.AddComponent<WorldMapPopup>();
        popup.BuildHierarchyForPrefab();

        // 回填公开引用（BuildHierarchyForPrefab 里已设置，保险再绑一次）
        // UnityEngine.Object 不用 ?? / ?.，一律显式判空
        var tTitle = root.transform.Find("Title");
        if (tTitle != null) popup.titleText = tTitle.GetComponent<Text>();

        var tDesc = root.transform.Find("Desc");
        if (tDesc != null) popup.descText = tDesc.GetComponent<Text>();

        var tBg = root.transform.Find("MapBackground");
        if (tBg != null) popup.mapBackground = tBg.GetComponent<Image>();

        popup.regionRoot = root.transform.Find("Regions");

        var tEnter = root.transform.Find("EnterButton");
        if (tEnter != null)
        {
            popup.enterButton = tEnter.GetComponent<Button>();
            if (popup.enterButton != null)
                popup.enterButtonText = popup.enterButton.GetComponentInChildren<Text>();
        }

        GameFonts.ApplyToHierarchy(root.transform);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[WorldMapPopupPrefabGenerator] 已生成: {PrefabPath}");
        if (showDialog)
            EditorUtility.DisplayDialog("世界地图弹窗",
                "已生成 Resources/Prefabs/UI/WorldMapPopup.prefab\n\n" +
                "后续替换：MapBackground 的 Sprite、Regions 下各按钮的图标与位置。",
                "OK");
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
