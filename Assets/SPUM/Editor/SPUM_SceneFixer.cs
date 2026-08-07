#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// 修复 SPUM 场景中缺失的 SPUM_Manager 预制体实例。
/// 运行菜单: SPUM/修复场景 Manager
/// </summary>
public class SPUM_SceneFixer : EditorWindow
{
    [MenuItem("SPUM/修复场景 Manager")]
    public static void FixSPUMManager()
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene == null || !activeScene.name.Contains("SPUM"))
        {
            Debug.LogWarning("请先打开 SPUM 场景");
            return;
        }

        // 查找现有的 SPUM_Manager
        GameObject existing = GameObject.Find("SPUM_Manager");
        SPUM_Manager mgr = existing != null ? existing.GetComponent<SPUM_Manager>() : null;

        if (existing != null && mgr != null)
        {
            // 修复现有 Manager 的缺失引用
            Debug.Log("SPUM_Manager 已存在，正在修复缺失引用...");
            bool fixedSomething = false;

            // 修复 PreviewPrefab
            if (mgr.PreviewPrefab == null)
            {
                var previews = Resources.LoadAll<SPUM_Prefabs>("");
                if (previews.Length > 0)
                {
                    mgr.PreviewPrefab = previews[0];
                    Debug.Log("已修复 PreviewPrefab 引用: " + previews[0].name);
                    fixedSomething = true;
                }
            }

            // 修复 UIManager 引用
            if (mgr.UIManager == null)
            {
                var uiMgrs = GameObject.FindObjectsByType<SPUM_UIManager>(FindObjectsSortMode.None);
                if (uiMgrs.Length > 0)
                {
                    mgr.UIManager = uiMgrs[0];
                    Debug.Log("已修复 UIManager 引用");
                    fixedSomething = true;
                }
            }

            // 修复 animationManager 引用
            if (mgr.animationManager == null)
            {
                var animMgrs = GameObject.FindObjectsByType<SPUM_AnimationManager>(FindObjectsSortMode.None);
                if (animMgrs.Length > 0)
                {
                    mgr.animationManager = animMgrs[0];
                    Debug.Log("已修复 animationManager 引用");
                    fixedSomething = true;
                }
            }

            // 修复 paginationManager 引用
            if (mgr.paginationManager == null)
            {
                var pagMgrs = GameObject.FindObjectsByType<SPUM_PaginationManager>(FindObjectsSortMode.None);
                if (pagMgrs.Length > 0)
                {
                    mgr.paginationManager = pagMgrs[0];
                    Debug.Log("已修复 paginationManager 引用");
                    fixedSomething = true;
                }
            }

            // 修复 IFileHandler 组件
            if (mgr.fileHandler == null)
            {
                var handler = existing.GetComponent<IPrefabFileHandler>();
                if (handler == null)
                {
                    existing.AddComponent<IPrefabFileHandler>();
                    Debug.Log("已添加缺失的 IPrefabFileHandler 组件");
                    fixedSomething = true;
                }
            }

            if (fixedSomething)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                Debug.Log("SPUM_Manager 引用修复完成！请保存场景。");
            }
            else
            {
                Debug.Log("SPUM_Manager 所有引用正常，无需修复。");
            }
            return;
        }

        // 如果 existing 存在但没有 SPUM_Manager 组件，先删除它
        if (existing != null && mgr == null)
        {
            Debug.Log("发现失效的 SPUM_Manager 对象，正在删除...");
            DestroyImmediate(existing);
            existing = null;
        }

        // 删除所有名称包含 SPUM_Manager 但没有 SPUM_Manager 组件的对象
        var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var obj in allObjects)
        {
            if (obj == null) continue;
            if (obj.scene != activeScene) continue;
            if (!obj.name.Contains("SPUM_Manager")) continue;
            if (obj.GetComponent<SPUM_Manager>() != null) continue;

            Debug.Log("删除失效对象: " + obj.name);
            DestroyImmediate(obj);
        }

        // 创建新的 SPUM_Manager 游戏对象
        GameObject go = new GameObject("SPUM_Manager");
        go.transform.position = Vector3.zero;
        go.transform.rotation = Quaternion.identity;

        // 添加所有必需组件，并检查是否成功
        SPUM_Manager manager = SafeAddComponent<SPUM_Manager>(go);
        SPUM_UIManager uiManager = SafeAddComponent<SPUM_UIManager>(go);
        SPUM_AnimationManager animManager = SafeAddComponent<SPUM_AnimationManager>(go);
        SPUM_PaginationManager paginationManager = SafeAddComponent<SPUM_PaginationManager>(go);
        IPrefabFileHandler fileHandler = SafeAddComponent<IPrefabFileHandler>(go);

        if (manager == null)
        {
            Debug.LogError("[SPUM] 无法创建 SPUM_Manager 组件。请检查 SPUM_Manager.cs 是否编译正确。");
            DestroyImmediate(go);
            return;
        }

        // 设置组件间引用
        manager.UIManager = uiManager;
        manager.animationManager = animManager;
        manager.paginationManager = paginationManager;

        // 查找场景中的 UI 引用并尝试连接
        TrySetupReferences(manager, uiManager, animManager, paginationManager);

        // 标记场景为已修改
        EditorSceneManager.MarkSceneDirty(activeScene);
        Debug.Log("SPUM_Manager 修复完成！请在 Inspector 中检查并保存场景。");
    }

    /// <summary>
    /// 安全地添加组件，如果失败则返回 null 并输出日志
    /// </summary>
    private static T SafeAddComponent<T>(GameObject go) where T : Component
    {
        if (go == null)
        {
            Debug.LogError($"[SPUM] SafeAddComponent: GameObject 为 null，无法添加 {typeof(T).Name}");
            return null;
        }

        T comp = go.AddComponent<T>();
        if (comp == null)
        {
            Debug.LogError($"[SPUM] 无法为 {go.name} 添加组件 {typeof(T).Name}。" +
                "可能原因：脚本编译错误、脚本在不可访问的程序集中、或 Unity 内部错误。");
        }
        else
        {
            Debug.Log($"[SPUM] 成功添加组件 {typeof(T).Name} 到 {go.name}");
        }
        return comp;
    }

    private static void TrySetupReferences(SPUM_Manager manager, SPUM_UIManager uiManager, SPUM_AnimationManager animManager, SPUM_PaginationManager paginationManager)
    {
        if (manager == null) return;

        // 查找所有 toggle 控件
        var toggles = Resources.FindObjectsOfTypeAll<Toggle>();
        foreach (var t in toggles)
        {
            if (t == null) continue;
            if (t.name == "RandomColor" || t.name.Contains("RandomColor"))
            {
                manager.RandomColorButton = t;
                break;
            }
        }

        // 查找所有 SPUM_Animator 数组 - 从 Resources 中加载动画控制器
        var animators = Resources.LoadAll<RuntimeAnimatorController>("");
        if (animators.Length > 0)
        {
            var animList = new System.Collections.Generic.List<SPUM_Animator>();
            foreach (var anim in animators)
            {
                if (anim == null) continue;
                string folder = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(anim)));
                if (!string.IsNullOrEmpty(folder))
                {
                    animList.Add(new SPUM_Animator { Type = folder, RuntimeAnimator = anim });
                }
            }
            if (animList.Count > 0)
            {
                manager.SPUM_Animator = animList.ToArray();
            }
        }

        // 查找 PreviewPrefab (SPUM_Prefabs)
        var previewPrefabs = Resources.LoadAll<SPUM_Prefabs>("");
        if (previewPrefabs.Length > 0)
        {
            manager.PreviewPrefab = previewPrefabs[0];
        }
    }

    [MenuItem("SPUM/检查场景缺失引用")]
    public static void CheckMissingReferences()
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene == null) return;

        GameObject[] roots = activeScene.GetRootGameObjects();
        int missingCount = 0;
        int totalComponents = 0;

        foreach (var root in roots)
        {
            if (root == null) continue;
            CheckGameObject(root, ref missingCount, ref totalComponents);
        }

        Debug.Log($"检查完成: {totalComponents} 个组件, {missingCount} 个缺失引用");
    }

    private static void CheckGameObject(GameObject go, ref int missingCount, ref int totalComponents)
    {
        if (go == null) return;
        Component[] components = go.GetComponents<Component>();
        foreach (var comp in components)
        {
            totalComponents++;
            if (comp == null)
            {
                missingCount++;
                Debug.LogWarning($"缺失组件: {go.name} (路径: {GetPath(go)})", go);
            }
        }

        foreach (Transform child in go.transform)
        {
            if (child == null) continue;
            CheckGameObject(child.gameObject, ref missingCount, ref totalComponents);
        }
    }

    private static string GetPath(GameObject go)
    {
        if (go == null) return "null";
        string path = go.name;
        Transform parent = go.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }
}
#endif
