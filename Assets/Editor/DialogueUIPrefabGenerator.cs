#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 首次生成对话预制体。已存在会确认覆盖——请勿覆盖已换好资源的版本。
/// 菜单：Tools/UI/生成对话界面预制体
/// </summary>
public static class DialogueUIPrefabGenerator
{
    const string PrefabPath = "Assets/Resources/Prefabs/Dialogue/DialogueUI.prefab";

    [MenuItem("Tools/UI/生成对话界面预制体")]
    public static void Generate()
    {
        EnsureFolders();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            if (!EditorUtility.DisplayDialog(
                    "对话界面预制体",
                    "已存在 DialogueUI.prefab，是否覆盖？\n（你改过的 Sprite 会被盖掉）",
                    "覆盖", "取消"))
                return;
        }

        var root = new GameObject("DialogueUI", typeof(RectTransform));
        var canvas = root.AddComponent<Canvas>();
        root.AddComponent<CanvasScaler>();
        root.AddComponent<GraphicRaycaster>();
        UICanvasSetup.Apply(canvas, null);
        canvas.sortingOrder = 80;

        var ui = root.AddComponent<DialogueUI>();
        ui.BuildHierarchyForPrefab();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[DialogueUIPrefabGenerator] 已生成: {PrefabPath}");
        EditorUtility.DisplayDialog("对话界面",
            "已生成 Resources/Prefabs/Dialogue/DialogueUI.prefab\n\n" +
            "请替换：Frame、PatternBg、BannerLeft/Right、Left/RightPortrait、\n" +
            "DialogueBox、NamePlate、Icon、NextArrow。\n" +
            "参考图：Art/UI/Dialogue/dialogue_reference.png",
            "OK");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab != null) Selection.activeObject = prefab;
    }

    public static void GenerateBatch()
    {
        EnsureFolders();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            return; // 批处理不覆盖已有

        var root = new GameObject("DialogueUI", typeof(RectTransform));
        var canvas = root.AddComponent<Canvas>();
        root.AddComponent<CanvasScaler>();
        root.AddComponent<GraphicRaycaster>();
        UICanvasSetup.Apply(canvas, null);
        canvas.sortingOrder = 80;
        root.AddComponent<DialogueUI>().BuildHierarchyForPrefab();
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs"))
            AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs/Dialogue"))
            AssetDatabase.CreateFolder("Assets/Resources/Prefabs", "Dialogue");
    }
}
#endif
