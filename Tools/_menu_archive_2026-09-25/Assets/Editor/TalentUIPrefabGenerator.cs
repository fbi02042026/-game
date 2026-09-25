#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 生成天赋界面预制体。已换资源时勿覆盖。
/// 菜单：Tools/UI/生成天赋界面预制体
/// </summary>
public static class TalentUIPrefabGenerator
{
    const string PrefabPath = "Assets/Resources/Prefabs/Talent/TalentUI.prefab";

    [MenuItem("Tools/_归档/UI/生成天赋界面预制体")]
    public static void Generate()
    {
        EnsureFolders();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            if (!EditorUtility.DisplayDialog(
                    "天赋界面预制体",
                    "已存在 TalentUI.prefab，将按 Art/UI/Talent 切片重新生成。\n" +
                    "结构会覆盖；之后你仍可在 Inspector 替换 Sprite。",
                    "覆盖生成", "取消"))
                return;
        }

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
        var root = new GameObject("TalentUI", typeof(RectTransform));
        var canvas = root.AddComponent<Canvas>();
        root.AddComponent<CanvasScaler>();
        root.AddComponent<GraphicRaycaster>();
        UICanvasSetup.Apply(canvas, null);
        canvas.sortingOrder = 200;

        var ui = root.AddComponent<TalentUI>();
        ui.BuildHierarchyForPrefab();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[TalentUIPrefabGenerator] 已生成: {PrefabPath}");
        if (showDialog)
        {
            EditorUtility.DisplayDialog("天赋界面",
                "已生成 Resources/Prefabs/Talent/TalentUI.prefab\n\n" +
                "参考图：Art/UI/Talent/talent_reference.png\n" +
                "设计文档：Docs/像素冒险：裂缝之刃_天赋系统设计.md\n\n" +
                "运行时会按 TalentDefs 填充 L1-L40 / R1-R10。\n" +
                "解锁逻辑通过 onLeftUnlockRequested / onRightChoiceRequested 对接。",
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
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs/Talent"))
            AssetDatabase.CreateFolder("Assets/Resources/Prefabs", "Talent");
        if (!AssetDatabase.IsValidFolder("Assets/Art/UI/Talent"))
            AssetDatabase.CreateFolder("Assets/Art/UI", "Talent");
    }
}
#endif
