#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 仅首次白模：已有完好 PlayerJobSelect.prefab 时拒绝覆盖。
/// Missing Script / 文件缺失时允许重建一次。
/// </summary>
public static class PlayerJobSelectPrefabGenerator
{
    const string PrefabPath = "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab";
    const string RebuildOnceKey = "PlayerJobSelectPrefabGenerator.RebuiltMissing";

    /// <summary>缺 prefab 时延迟创建；Missing Script 时本会话重建一次。</summary>
    [InitializeOnLoadMethod]
    static void EnsurePrefabExistsOnce()
    {
        EditorApplication.delayCall += TryEnsurePrefab;
    }

    static void TryEnsurePrefab()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (TryRebuildIfBrokenOrMissing())
            return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
        EnsureFolders();
        BuildAndSave(showDialog: false);
        Debug.Log("[PlayerJobSelectPrefabGenerator] 首次自动生成白模 prefab（之后不会覆盖）");
    }

    [MenuItem("Tools/UI/生成职业三选一预制体（仅首次白模）")]
    public static void Generate()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null && !PrefabHasMissingScript())
        {
            EditorUtility.DisplayDialog("职业三选一预制体",
                "已存在完好的 PlayerJobSelect.prefab，禁止覆盖。\n\n" +
                "请直接在 Inspector 改 Sprite/布局；逻辑问题只改 PlayerJobSelectUI.cs。",
                "OK");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            return;
        }

        if (PrefabHasMissingScript())
            AssetDatabase.DeleteAsset(PrefabPath);

        EnsureFolders();
        BuildAndSave(showDialog: true);
    }

    public static void GenerateBatch()
    {
        EnsureFolders();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null && !PrefabHasMissingScript())
            return;
        if (PrefabHasMissingScript())
            AssetDatabase.DeleteAsset(PrefabPath);
        BuildAndSave(showDialog: false);
    }

    static bool TryRebuildIfBrokenOrMissing()
    {
        if (!PrefabHasMissingScript()) return false;
        if (SessionState.GetBool(RebuildOnceKey, false)) return false;
        SessionState.SetBool(RebuildOnceKey, true);
        AssetDatabase.DeleteAsset(PrefabPath);
        EnsureFolders();
        BuildAndSave(showDialog: false);
        Debug.Log("[PlayerJobSelectPrefabGenerator] 检测到 Missing Script，已重建白模 prefab（本会话仅一次）");
        return true;
    }

    static bool PrefabHasMissingScript()
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (go == null) return false;
        if (go.GetComponent<PlayerJobSelectUI>() == null) return true;
        var monos = go.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < monos.Length; i++)
        {
            if (monos[i] == null) return true;
        }
        return false;
    }

    static void BuildAndSave(bool showDialog)
    {
        var root = new GameObject("PlayerJobSelect", typeof(RectTransform));
        var rootRt = root.GetComponent<RectTransform>();
        rootRt.localScale = Vector3.one;
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;
        rootRt.pivot = new Vector2(0.5f, 0.5f);

        var canvas = root.AddComponent<Canvas>();
        root.AddComponent<CanvasScaler>();
        root.AddComponent<GraphicRaycaster>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownPopup);

        var ui = root.AddComponent<PlayerJobSelectUI>();
        ui.BuildHierarchyForPrefab();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[PlayerJobSelectPrefabGenerator] 已生成: {PrefabPath}");
        if (showDialog)
        {
            EditorUtility.DisplayDialog("职业三选一",
                "已生成 Resources/Prefabs/UI/PlayerJobSelect.prefab（仅白模）。\n" +
                "之后请只在 Inspector 换资源，勿再跑本菜单。\n" +
                "职业图标 Resources 路径：UI/JobSelect/{id}",
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
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs/UI"))
            AssetDatabase.CreateFolder("Assets/Resources/Prefabs", "UI");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/UI"))
            AssetDatabase.CreateFolder("Assets/Resources", "UI");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/UI/JobSelect"))
            AssetDatabase.CreateFolder("Assets/Resources/UI", "JobSelect");
    }
}
#endif
