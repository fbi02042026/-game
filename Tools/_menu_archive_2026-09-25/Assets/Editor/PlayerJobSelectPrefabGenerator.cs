#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 仅「文件完全不存在」时生成一次白模。
/// 2026-09-16 起：**不再因为 Missing Script 删除/重建预制体** ——
/// 历史上这个自动重建把美术手工摆好的 PlayerJobSelect 反复覆盖成白模，
/// 最后一次甚至把 .prefab 和 .meta 整个删掉。
/// </summary>
public static class PlayerJobSelectPrefabGenerator
{
    const string PrefabPath = "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab";
    const string RebuildOnceKey = "PlayerJobSelectPrefabGenerator.RebuiltMissing";
    const string MissingWarnKey = "PlayerJobSelectPrefabGenerator.WarnedMissing";

    /// <summary>缺 prefab 时延迟创建；已存在则什么都不做（绝不删除/重建）。</summary>
    [InitializeOnLoadMethod]
    static void EnsurePrefabExistsOnce()
    {
        EditorApplication.delayCall += TryEnsurePrefab;
    }

    static void TryEnsurePrefab()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        // 只在「文件真的不存在」时生成一次白模。
        // 绝不再因为 Missing Script 就删掉美术调好的预制体 —— 历史上这个自动重建
        // 把用户手工摆好的 PlayerJobSelect 反复覆盖成白模，甚至整文件删掉。
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existing != null)
        {
            int miss = CountMissingScripts(existing);
            // 每个编辑器会话只提示一次（delayCall 会在每次编译后重跑，否则会反复弹同一条警告）
            if (miss > 0 && !SessionState.GetBool(MissingWarnKey, false))
            {
                SessionState.SetBool(MissingWarnKey, true);
                Debug.LogWarning("[PlayerJobSelectPrefabGenerator] PlayerJobSelect.prefab 有 " + miss +
                                 " 个 Missing Script 组件（历史上随外部资源入库带进来的，不是业务脚本）。" +
                                 "点菜单 Tools/UI/清理选中预制体的 Missing Script 一键移除，或手动在根节点 Inspector 删除。");
            }
            return;
        }

        EnsureFolders();
        BuildAndSave(showDialog: false);
        Debug.Log("[PlayerJobSelectPrefabGenerator] 首次自动生成白模 prefab（之后不会覆盖）");
    }

    /// <summary>已废弃：自动重建已永久禁用，不再删除美术成果。</summary>
    [System.Obsolete("禁止自动重建：会覆盖美术成果")]
    static bool TryRebuildIfBrokenOrMissing()
    {
        return false;
    }

    [MenuItem("Tools/UI/生成职业三选一预制体（仅首次白模）")]
    public static void Generate()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            EditorUtility.DisplayDialog("职业三选一预制体",
                "已存在 PlayerJobSelect.prefab，禁止覆盖、禁止删除。\n\n" +
                "请直接在 Inspector 改 Sprite/布局；逻辑问题只改 PlayerJobSelectUI.cs。",
                "OK");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            return;
        }

        EnsureFolders();
        BuildAndSave(showDialog: true);
    }

    public static void GenerateBatch()
    {
        // 已存在就什么都不做 —— 不再删除重建
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
        EnsureFolders();
        BuildAndSave(showDialog: false);
    }

    /// <summary>统计预制体（含子节点）上 Missing Script 组件数量。</summary>
    static int CountMissingScripts(GameObject go)
    {
        if (go == null) return 0;
        int n = 0;
        var comps = go.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < comps.Length; i++)
        {
            var c = comps[i];
            if (c == null) n++;   // Unity 对 Missing Script 的组件返回 fake null
        }
        return n;
    }

    /// <summary>
    /// 一键移除当前选中预制体上的全部 Missing Script 组件（含子节点）。
    /// 只删「脚本已丢失的空壳组件」，不碰任何美术数据，是纯垃圾清理。
    /// </summary>
    [MenuItem("Tools/UI/清理选中预制体的 Missing Script")]
    public static void CleanMissingScriptsOfSelection()
    {
        var sel = Selection.activeGameObject;
        // 选中了预制体就处理它；没选中则默认处理 PlayerJobSelect.prefab
        string assetPath = sel != null ? AssetDatabase.GetAssetPath(sel) : PrefabPath;
        if (string.IsNullOrEmpty(assetPath)
            || !assetPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning("[PlayerJobSelectPrefabGenerator] 请先在 Project 里选中一个预制体；" +
                             "不选则默认处理 PlayerJobSelect.prefab。");
            return;
        }

        // 直接改预制体资源本体：LoadPrefabContents → 清理 → SaveAsPrefabAsset，
        // 不在场景里留实例，也不需要手动 Apply。
        var root = PrefabUtility.LoadPrefabContents(assetPath);
        int n = 0;
        try
        {
            // GameObjectUtility 是官方提供的删除 Missing Script 的 API
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] == null) continue;
                n += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transforms[i].gameObject);
            }
            if (n > 0)
                PrefabUtility.SaveAsPrefabAsset(root, assetPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        Debug.Log("[PlayerJobSelectPrefabGenerator] " + assetPath + " 清理完成：移除 " + n + " 个 Missing Script 组件。");
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
