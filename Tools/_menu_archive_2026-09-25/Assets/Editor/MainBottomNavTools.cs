#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 从主界面提取共用壳。已存在目标预制体时会先确认，绝不静默覆盖。
/// </summary>
public static class MainBottomNavTools
{
    const string HallPrefabPath = "Assets/Resources/Prefabs/Town/GuildHallUI.prefab";
    const string NavPrefabPath = "Assets/Resources/Prefabs/Town/MainBottomNav.prefab";
    const string ResourceBarPrefabPath = "Assets/Resources/Prefabs/Town/ResourceBar.prefab";

    [MenuItem("Tools/_归档/UI/一键提取资源条+底栏")]
    public static void ExtractAllSharedChrome()
    {
        if (!ConfirmOverwriteIfExists(ResourceBarPrefabPath, "ResourceBar") ||
            !ConfirmOverwriteIfExists(NavPrefabPath, "MainBottomNav"))
            return;

        if (!ExtractResourceBarPrefab(silent: true)) return;
        if (!ExtractBottomNavPrefab(silent: true)) return;

        EditorUtility.DisplayDialog("完成",
            "已提取：\n" + ResourceBarPrefabPath + "\n" + NavPrefabPath,
            "确定");
    }

    static bool ConfirmOverwriteIfExists(string path, string label)
    {
        if (!System.IO.File.Exists(path)) return true;
        return EditorUtility.DisplayDialog(
            "确认覆盖？",
            "已存在「" + label + "」：\n" + path + "\n\n要覆盖吗？选「否」则整次取消。",
            "覆盖", "取消");
    }

    static bool ExtractBottomNavPrefab(bool silent)
    {
        var hall = AssetDatabase.LoadAssetAtPath<GameObject>(HallPrefabPath);
        if (hall == null)
        {
            EditorUtility.DisplayDialog("提取失败", "找不到 GuildHallUI：\n" + HallPrefabPath, "确定");
            return false;
        }

        string hallPath = AssetDatabase.GetAssetPath(hall);
        GameObject hallRoot = PrefabUtility.LoadPrefabContents(hallPath);
        try
        {
            Transform bottom = FindOuterBottomNav(hallRoot.transform);
            if (bottom == null)
            {
                EditorUtility.DisplayDialog("提取失败", "GuildHallUI 内找不到 BottomNav", "确定");
                return false;
            }

            if (bottom.GetComponent<MainBottomNav>() == null)
                bottom.gameObject.AddComponent<MainBottomNav>();

            GameObject clone = Object.Instantiate(bottom.gameObject);
            clone.name = "MainBottomNav";
            if (clone.GetComponent<MainBottomNav>() == null)
                clone.AddComponent<MainBottomNav>();

            EnsureDir("Assets/Resources/Prefabs/Town");
            GameFonts.ApplyToHierarchy(clone.transform);
            PrefabUtility.SaveAsPrefabAsset(clone, NavPrefabPath);
            Object.DestroyImmediate(clone);

            GameFonts.ApplyToHierarchy(hallRoot.transform);
            PrefabUtility.SaveAsPrefabAsset(hallRoot, hallPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!silent) Debug.Log("[MainBottomNavTools] 已提取 " + NavPrefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(hallRoot);
        }
    }

    static bool ExtractResourceBarPrefab(bool silent)
    {
        var hall = AssetDatabase.LoadAssetAtPath<GameObject>(HallPrefabPath);
        if (hall == null)
        {
            EditorUtility.DisplayDialog("提取失败", "找不到 GuildHallUI：\n" + HallPrefabPath, "确定");
            return false;
        }

        GameObject hallRoot = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(hall));
        try
        {
            Transform top = FindDeep(hallRoot.transform, "TopBar");
            if (top == null)
            {
                EditorUtility.DisplayDialog("提取失败", "GuildHallUI 内找不到 TopBar", "确定");
                return false;
            }

            EnsureDir("Assets/Resources/Prefabs/Town");
            GameObject clone = Object.Instantiate(top.gameObject);
            clone.name = "ResourceBar";
            GameFonts.ApplyToHierarchy(clone.transform);
            PrefabUtility.SaveAsPrefabAsset(clone, ResourceBarPrefabPath);
            Object.DestroyImmediate(clone);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!silent) Debug.Log("[MainBottomNavTools] 已提取 " + ResourceBarPrefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(hallRoot);
        }
    }

    static Transform FindOuterBottomNav(Transform root)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null || all[i].name != "BottomNav") continue;
            for (int c = 0; c < all[i].childCount; c++)
            {
                if (all[i].GetChild(c).name == "BottomNavBG")
                    return all[i];
            }
        }
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].name == "BottomNav")
                return all[i];
        return null;
    }

    static Transform FindDeep(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var r = FindDeep(parent.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    static void EnsureDir(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string[] parts = path.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
}
#endif
