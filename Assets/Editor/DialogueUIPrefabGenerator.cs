#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 生成剧情对话预制体（选项 0~3 + 跳过）。已换资源时勿覆盖。
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

        BuildAndSave(showDialog: true);
    }

    public static void GenerateBatch()
    {
        EnsureFolders();
        // 批处理：已有则跳过，不覆盖用户资源
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            return;
        BuildAndSave(showDialog: false);
    }

    static void BuildAndSave(bool showDialog)
    {
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
        if (showDialog)
        {
            EditorUtility.DisplayDialog("对话界面",
                "已生成 Resources/Prefabs/Dialogue/DialogueUI.prefab\n\n" +
                "替换：DialogueBox、Left/RightPortrait、NamePlate、Choice_*、NextArrow、SkipButton\n" +
                "参考图：Art/UI/Dialogue/dialogue_reference.png\n\n" +
                "逻辑：发起方左侧朝右；对方右侧翻转朝左；选项最多 3 个；右上跳过。",
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
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs/Dialogue"))
            AssetDatabase.CreateFolder("Assets/Resources/Prefabs", "Dialogue");
    }
}
#endif
