// ============================================================================
//  BoxPrefabRebuilder.cs
//  一次性工具脚本 —— 用于重建丢失的 box 宝箱预制体并接回 Battle 场景。
//
//  背景：
//  Assets/Scenes/Battle.unity 里 WorldRoot/box 节点下挂着一个预制体实例，
//  但源预制体文件从未进过 git、已彻底丢失（引擎报
//  Missing Prefab Asset: 'box (Missing Prefab with guid: 429795ccaa0c615408a6c4b24a06711d)'），
//  导致宝箱的 close / open / effect 子精灵与 Animator 全部缺失，需要重建。
//
//  本脚本做的事全部走 UnityEditor / UnityEngine API，绝不手动修改 YAML 文本，
//  也绝不手写任何 guid 字面量（所有引用都交给引擎序列化）。
//
//  ⚠️ 这是一次性工具：等你在编辑器里确认 box 预制体重建成功、场景接回无误后，
//     可自行删除本文件（Assets/Editor/BoxPrefabRebuilder.cs），无需保留。
//
//  兼容：Unity 2022.3.x（团结引擎 2022.3.62t12），未使用 2023+ 或 C# 8+ 语法。
// ============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

public static class BoxPrefabRebuilder
{
    // ---- 素材与输出路径（全部交给引擎加载 / 序列化，不写 guid）----
    private const string CLOSE_SPRITE_PATH = "Assets/Art/UI/box/mubox_close.png";
    private const string OPEN_SPRITE_PATH  = "Assets/Art/UI/box/mubox_open.png";
    private const string CONTROLLER_PATH   = "Assets/Art/Effects/Ani/box/box.controller";
    private const string PREFAB_DIR        = "Assets/Resources/Prefabs/Battle";
    private const string PREFAB_PATH       = "Assets/Resources/Prefabs/Battle/box.prefab";
    private const string BATTLE_SCENE_PATH = "Assets/Scenes/Battle.unity";

    // ------------------------------------------------------------------
    // 菜单 ①：在内存里搭好结构 → 存成预制体文件
    // ------------------------------------------------------------------
    [MenuItem("Tools/宝箱/① 新建 box 预制体")]
    public static void BuildBoxPrefab()
    {
        Debug.Log("[BoxPrefabRebuilder] 开始新建 box 预制体……");

        // 1) 加载素材（加载不到只告警，不中断）
        Sprite closeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CLOSE_SPRITE_PATH);
        if (closeSprite == null)
        {
            Debug.LogWarning("[BoxPrefabRebuilder] 未能加载 close 精灵：" + CLOSE_SPRITE_PATH);
        }

        Sprite openSprite = AssetDatabase.LoadAssetAtPath<Sprite>(OPEN_SPRITE_PATH);
        if (openSprite == null)
        {
            Debug.LogWarning("[BoxPrefabRebuilder] 未能加载 open 精灵：" + OPEN_SPRITE_PATH);
        }

        RuntimeAnimatorController controller =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CONTROLLER_PATH);
        if (controller == null)
        {
            Debug.LogWarning("[BoxPrefabRebuilder] 未能加载动画控制器：" + CONTROLLER_PATH + "（仍会建立预制体，运行前请确认）");
        }

        // 2) 在内存里搭根节点 box
        GameObject root = new GameObject("box");

        // 根节点挂 Animator，并绑定 controller（由引擎序列化引用）
        Animator animator = root.AddComponent<Animator>();
        if (controller != null)
        {
            animator.runtimeAnimatorController = controller;
        }

        // 3) 子节点 close
        GameObject closeGo = new GameObject("close");
        closeGo.transform.SetParent(root.transform, false);
        SpriteRenderer closeSr = closeGo.AddComponent<SpriteRenderer>();
        closeSr.sprite = closeSprite;
        closeSr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
        closeSr.sortingOrder = GameConfig.SORT_MAPROOT + 2;

        // 4) 子节点 open
        GameObject openGo = new GameObject("open");
        openGo.transform.SetParent(root.transform, false);
        SpriteRenderer openSr = openGo.AddComponent<SpriteRenderer>();
        openSr.sprite = openSprite;
        openSr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
        openSr.sortingOrder = GameConfig.SORT_MAPROOT + 3;

        // 5) 子节点 effect（空 GameObject，仅作粒子根占位）
        GameObject effectGo = new GameObject("effect");
        effectGo.transform.SetParent(root.transform, false);

        // 6) 确保目标文件夹存在
        if (!AssetDatabase.IsValidFolder(PREFAB_DIR))
        {
            AssetDatabase.CreateFolder("Assets/Resources/Prefabs", "Battle");
            Debug.Log("[BoxPrefabRebuilder] 已创建文件夹：" + PREFAB_DIR);
        }

        // 7) 存成预制体（引用由引擎写入，不手改 YAML）
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
        if (saved != null)
        {
            Debug.Log("[BoxPrefabRebuilder] 预制体已保存：" + PREFAB_PATH);
        }
        else
        {
            Debug.LogError("[BoxPrefabRebuilder] 预制体保存失败：" + PREFAB_PATH);
        }

        // 8) 清理内存里的临时 GameObject
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BoxPrefabRebuilder] 完成。AssetDatabase 已保存并刷新。");
    }

    // ------------------------------------------------------------------
    // 菜单 ②：把预制体实例化挂回 Battle 场景的 box 节点下
    // ------------------------------------------------------------------
    [MenuItem("Tools/宝箱/② 把 box 预制体接回 Battle 场景")]
    public static void ReconnectBoxToBattle()
    {
        Debug.Log("[BoxPrefabRebuilder] 开始把 box 预制体接回 Battle 场景……");

        // 1) 必须当前活动场景就是 Battle.unity，避免覆盖主人未保存的编辑
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != BATTLE_SCENE_PATH)
        {
            EditorUtility.DisplayDialog(
                "场景不对",
                "请先在编辑器里打开 Assets/Scenes/Battle.unity 再执行。",
                "知道了");
            Debug.LogWarning("[BoxPrefabRebuilder] 当前活动场景不是 Battle.unity（实际：" + scene.path + "），已取消。");
            return;
        }
        Debug.Log("[BoxPrefabRebuilder] 当前活动场景确认为 Battle.unity。");

        // 2) 找到场景里的 box 节点
        GameObject boxNode = GameObject.Find("box");
        if (boxNode == null)
        {
            // 退化方案：遍历场景根，找 WorldRoot，再在它下面找 box
            GameObject worldRoot = null;
            List<GameObject> roots = new List<GameObject>(scene.GetRootGameObjects());
            for (int i = 0; i < roots.Count; i++)
            {
                if (roots[i].name == "WorldRoot")
                {
                    worldRoot = roots[i];
                    break;
                }
            }
            if (worldRoot != null)
            {
                Transform child = worldRoot.transform.Find("box");
                if (child != null)
                {
                    boxNode = child.gameObject;
                }
            }
        }

        if (boxNode == null)
        {
            EditorUtility.DisplayDialog(
                "找不到 box 节点",
                "当前 Battle 场景里没有名为 box 的节点（WorldRoot 下也没有），请检查场景结构。",
                "知道了");
            Debug.LogError("[BoxPrefabRebuilder] 未找到 box 节点，已取消。");
            return;
        }
        Debug.Log("[BoxPrefabRebuilder] 已找到 box 节点：" + boxNode.name);

        // 3) 遍历 box 的直接子级，删掉状态为 Missing 的旧实例
        int removedCount = 0;
        // 先收集直接子级，避免遍历时修改集合出问题
        List<GameObject> directChildren = new List<GameObject>();
        for (int i = 0; i < boxNode.transform.childCount; i++)
        {
            directChildren.Add(boxNode.transform.GetChild(i).gameObject);
        }

        for (int i = 0; i < directChildren.Count; i++)
        {
            GameObject child = directChildren[i];
            bool isMissing = false;
            try
            {
                if (PrefabUtility.GetPrefabInstanceStatus(child) == PrefabInstanceStatus.MissingAsset)
                {
                    isMissing = true;
                }
                else if (PrefabUtility.GetPrefabAssetType(child) == PrefabAssetType.MissingAsset)
                {
                    isMissing = true;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[BoxPrefabRebuilder] 判定子节点 " + child.name + " 是否缺失时出错：" + e.Message);
            }

            if (isMissing)
            {
                try
                {
                    Object.DestroyImmediate(child);
                    removedCount++;
                    Debug.Log("[BoxPrefabRebuilder] 已删除缺失的旧实例：" + child.name);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[BoxPrefabRebuilder] 删除子节点 " + child.name + " 失败：" + e.Message);
                }
            }
        }
        Debug.Log("[BoxPrefabRebuilder] 共删除缺失旧实例 " + removedCount + " 个。");

        // 4) 加载预制体并实例化挂到 box 节点下
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
        if (prefabAsset == null)
        {
            EditorUtility.DisplayDialog(
                "找不到预制体",
                "未能加载 " + PREFAB_PATH + "，请先执行菜单 ① 新建 box 预制体。",
                "知道了");
            Debug.LogError("[BoxPrefabRebuilder] 预制体加载失败：" + PREFAB_PATH);
            return;
        }

        Object instance = PrefabUtility.InstantiatePrefab(prefabAsset, boxNode.transform);
        if (instance != null)
        {
            Debug.Log("[BoxPrefabRebuilder] 已把预制体实例化挂到 box 节点下：" + prefabAsset.name);
        }
        else
        {
            Debug.LogError("[BoxPrefabRebuilder] 实例化预制体失败：" + PREFAB_PATH);
            return;
        }

        // 5) 标记场景为脏并保存（走引擎 API，不手改 YAML）
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        if (saved)
        {
            Debug.Log("[BoxPrefabRebuilder] Battle 场景已保存。");
        }
        else
        {
            Debug.LogWarning("[BoxPrefabRebuilder] Battle 场景保存失败，请手动保存。");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BoxPrefabRebuilder] 接回流程完成。");
    }
}
