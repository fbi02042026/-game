#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 生成冒险界面预制体（不含资源条/底栏，运行时 RaiseSharedChrome 复用）。
/// 菜单：Tools/UI/生成冒险界面预制体
/// </summary>
public static class AdventureUIPrefabGenerator
{
    const string PrefabPath = "Assets/Resources/Prefabs/Town/AdventureUI.prefab";

    [MenuItem("Tools/_归档/UI/生成冒险界面预制体")]
    public static void Generate()
    {
        GenerateInternal(showDialog: true);
    }

    /// <summary>供 -executeMethod 批处理调用</summary>
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
                    "冒险界面预制体",
                    "已存在 AdventureUI.prefab，是否覆盖？\n（你改过的 Sprite 会被盖掉）",
                    "覆盖", "取消"))
                return;
        }

        var root = new GameObject("AdventureUI", typeof(RectTransform));
        var canvas = root.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownPage);

        var ui = root.AddComponent<AdventureUI>();
        ui.BuildHierarchyForPrefab();

        // 绑侧栏 Icon 到 modeButtonIcons
        var left = root.transform.Find("LeftSidebar");
        if (left != null)
        {
            for (int i = 0; i < 5; i++)
            {
                var btnT = left.Find($"ModeBtn_{i}");
                if (btnT == null) continue;
                var iconImg = btnT.Find("Icon")?.GetComponent<Image>();
                if (ui.modeButtonIcons != null && ui.modeButtonIcons.Length > i)
                    ui.modeButtonIcons[i] = iconImg;
            }
        }

        GameFonts.ApplyToHierarchy(root.transform);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[AdventureUIPrefabGenerator] 已生成: {PrefabPath}");
        if (showDialog)
            EditorUtility.DisplayDialog("冒险界面",
                "已生成 Resources/Prefabs/Town/AdventureUI.prefab\n\n" +
                "可替换：MapBg、ModeBtn_*/IconImage、关卡节点 Image、EnemyIcons/DropIcons。\n" +
                "顶部资源条与底部五入口运行时复用主界面，预制体里没有。",
                "OK");
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
