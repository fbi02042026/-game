using System;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 从当前打开的 Battle 场景同步完整运行时预制体，并修复损坏的 Talent 引用。
/// 菜单：Tools/战斗/
/// </summary>
public static class BattleRuntimePrefabTool
{
    const string PrefabPath = "Assets/Resources/Prefabs/Battle/BattleUI.prefab";
    const string TalentScriptPath = "Assets/Scripts/Config/TalentConfig.cs";
    const string TalentAssetPath = "Assets/Resources/Config/Talents/backpack_row4.asset";

    [MenuItem("Tools/_归档/战斗/从场景同步 BattleUI 运行时预制体")]
    public static void BakeBattleUIFromScene()
    {
        var battleUI = UnityEngine.Object.FindObjectOfType<BattleUI>();
        if (battleUI == null)
        {
            EditorUtility.DisplayDialog("同步失败", "场景里找不到 BattleUI。请先打开 Battle 场景。", "OK");
            return;
        }

        // 同步前把 CharacterBar 放回 BattleUI 下可见位置（若在背包内）
        EnsureCharacterBarVisible(battleUI.transform);

        // 去掉运行时临时加的 ViewportFitDriver，避免写进预制体重复
        var drivers = battleUI.GetComponentsInChildren<ViewportFitDriver>(true);
        for (int i = 0; i < drivers.Length; i++)
            UnityEngine.Object.DestroyImmediate(drivers[i]);

        string dir = System.IO.Path.GetDirectoryName(PrefabPath).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(dir))
        {
            // Assets/Resources/Prefabs/Battle
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs"))
                AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs/Battle"))
                AssetDatabase.CreateFolder("Assets/Resources/Prefabs", "Battle");
        }

        GameObject source = battleUI.gameObject;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, PrefabPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (prefab != null)
        {
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            EditorUtility.DisplayDialog("同步完成",
                "已用当前场景 BattleUI 覆盖运行时预制体：\n" + PrefabPath +
                "\n\n请在 Project 里打开该预制体检查头像栏/背包/map。\n之后改 UI 请改这份预制体，或改场景后再点本菜单同步。",
                "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("同步失败", "SaveAsPrefabAsset 返回空，请查看 Console。", "OK");
        }
    }

    [MenuItem("Tools/_归档/战斗/修复 TalentConfig GUID（backpack_row4）")]
    public static void FixTalentGuid()
    {
        string newGuid = StableHexGuid(TalentScriptPath);
        string metaPath = TalentScriptPath + ".meta";
        if (!System.IO.File.Exists(metaPath))
        {
            EditorUtility.DisplayDialog("失败", "找不到 TalentConfig.cs.meta", "OK");
            return;
        }

        string meta = System.IO.File.ReadAllText(metaPath, Encoding.UTF8);
        meta = System.Text.RegularExpressions.Regex.Replace(meta, @"^guid: .+$", "guid: " + newGuid,
            System.Text.RegularExpressions.RegexOptions.Multiline);
        System.IO.File.WriteAllText(metaPath, meta, new UTF8Encoding(false));

        if (System.IO.File.Exists(TalentAssetPath))
        {
            string asset = System.IO.File.ReadAllText(TalentAssetPath, Encoding.UTF8);
            asset = System.Text.RegularExpressions.Regex.Replace(asset,
                @"m_Script: \{fileID: 11500000, guid: [^,]+, type: 3\}",
                "m_Script: {fileID: 11500000, guid: " + newGuid + ", type: 3}");
            // 修正错误的 YAML tag
            asset = asset.Replace("%TAG !u! tag:yousandi.cn,2023:", "%TAG !u! tag:unity3d.com,2011:");
            System.IO.File.WriteAllText(TalentAssetPath, asset, new UTF8Encoding(false));
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("完成",
            "已重写 TalentConfig GUID 并修复 backpack_row4.asset。\n新 GUID: " + newGuid,
            "OK");
    }

    [MenuItem("Tools/_归档/战斗/强制显示 CharacterBar 头像栏")]
    public static void ForceShowCharacterBar()
    {
        var battleUI = UnityEngine.Object.FindObjectOfType<BattleUI>();
        if (battleUI == null)
        {
            EditorUtility.DisplayDialog("失败", "场景里没有 BattleUI", "OK");
            return;
        }
        EnsureCharacterBarVisible(battleUI.transform);
        EditorUtility.DisplayDialog("完成", "CharacterBar 已挂回 BattleUI 并设为可见。", "OK");
    }

    static void EnsureCharacterBarVisible(Transform battleUIRoot)
    {
        Transform bar = FindDeep(battleUIRoot, "CharacterBar");
        if (bar == null) return;

        // 从背包里挪回 BattleUI，避免被 Mask/WorldSpace 裁掉
        if (bar.parent != battleUIRoot)
            bar.SetParent(battleUIRoot, true);

        bar.gameObject.SetActive(true);

        // 保证画在 map(10)/背包之上
        Canvas c = bar.GetComponent<Canvas>();
        if (c == null) c = bar.gameObject.AddComponent<Canvas>();
        c.overrideSorting = true;
        c.sortingOrder = 110;
        if (bar.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            bar.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // 子槽强制可见
        for (int i = 0; i < bar.childCount; i++)
            bar.GetChild(i).gameObject.SetActive(true);
    }

    static Transform FindDeep(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name.Equals(name, StringComparison.OrdinalIgnoreCase)) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var r = FindDeep(parent.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    static string StableHexGuid(string seed)
    {
        using (var md5 = MD5.Create())
        {
            byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes("PixelAdventureTown|" + seed));
            var sb = new StringBuilder(32);
            for (int i = 0; i < hash.Length; i++)
                sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }
    }
}
