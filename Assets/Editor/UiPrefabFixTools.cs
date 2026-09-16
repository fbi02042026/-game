using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 2026-09-16 一次性修复工具。
/// 关键：所有改动都在 Unity 内部完成并由 Unity 自己保存，
/// 避免「外部改 prefab 被 Unity 内存版本覆盖回去」。
/// </summary>
public static class UiPrefabFixTools
{
    const string WmpPath = "Assets/Resources/Prefabs/UI/WorldMapPopup.prefab";
    const string JobSelectPath = "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab";

    [MenuItem("Tools/修复/1. WorldMapPopup 挂脚本并接线")]
    public static void FixWorldMapPopup()
    {
        var root = PrefabUtility.LoadPrefabContents(WmpPath);
        if (root == null)
        {
            Debug.LogError("[UiPrefabFixTools] 打不开 " + WmpPath);
            return;
        }

        var wmp = root.GetComponent<WorldMapPopup>();
        if (wmp == null)
        {
            wmp = root.AddComponent<WorldMapPopup>();
            Debug.Log("[UiPrefabFixTools] 已补挂 WorldMapPopup 脚本");
        }

        var regions = root.transform.Find("Regions");
        var title = root.transform.Find("Title");
        var desc = root.transform.Find("Desc");
        var mapBg = root.transform.Find("MapBackground");
        var bg = root.transform.Find("bg");

        if (mapBg != null) wmp.mapBackground = mapBg.GetComponent<Image>();
        if (wmp.mapBackground == null && bg != null) wmp.mapBackground = bg.GetComponent<Image>();
        if (title != null) wmp.titleText = title.GetComponent<Text>();
        if (desc != null) wmp.descText = desc.GetComponent<Text>();
        if (regions != null) wmp.regionRoot = regions;

        // 共享「进入」按钮取 Region_2 下面那颗（Region_1 美术没放）。
        // 实际显示逻辑已改成优先用每个地块自己的 EnterButton。
        var eb = root.transform.Find("Regions/Region_2/EnterButton");
        if (eb != null)
        {
            wmp.enterButton = eb.GetComponent<Button>();
            wmp.enterButtonText = eb.GetComponentInChildren<Text>();
        }

        int regionCount = 0;
        if (regions != null)
        {
            for (int i = 0; i < regions.childCount; i++)
            {
                if (regions.GetChild(i).name.StartsWith("Region_")) regionCount++;
            }
        }

        PrefabUtility.SaveAsPrefabAsset(root, WmpPath);
        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.SaveAssets();

        Debug.Log("[UiPrefabFixTools] WorldMapPopup 接线完成：Region 子节点 " + regionCount +
                  " 个，regionRoot=" + (regions != null) +
                  "，enterButton=" + (wmp.enterButton != null) +
                  "，mapBackground=" + (wmp.mapBackground != null) +
                  "，title=" + (wmp.titleText != null) + "，desc=" + (wmp.descText != null));
    }

    [MenuItem("Tools/修复/2. 移除 PlayerJobSelect 上的 Missing Script")]
    public static void RemoveMissingScripts()
    {
        var root = PrefabUtility.LoadPrefabContents(JobSelectPath);
        if (root == null)
        {
            Debug.LogError("[UiPrefabFixTools] 打不开 " + JobSelectPath);
            return;
        }

        int total = 0;
        var transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            total += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transforms[i].gameObject);
        }

        if (total > 0)
        {
            PrefabUtility.SaveAsPrefabAsset(root, JobSelectPath);
            Debug.Log("[UiPrefabFixTools] PlayerJobSelect 移除 Missing Script " + total + " 个");
        }
        else
        {
            Debug.Log("[UiPrefabFixTools] PlayerJobSelect 上没有 Missing Script");
        }
        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.SaveAssets();
    }
}
