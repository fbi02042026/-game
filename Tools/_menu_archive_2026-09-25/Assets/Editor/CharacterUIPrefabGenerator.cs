#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 生成 / 更新角色界面预制体（含技能选择弹层、战斗同款背包格）。
///
/// 铁律：**现有预制体里主人摆好的节点、层级、大小、坐标、已赋好的图一律不动**。
/// 已有 Content 时只对「缺失」的节点做兜底补全（看板 Portrait / 脚下 shadow），
/// 空资源 / 手改资源由 CharacterUI 运行时兜底，生成器与运行时都不覆盖已有值。
///
/// 菜单：Tools/_归档/UI/生成角色界面预制体
/// </summary>
public static class CharacterUIPrefabGenerator
{
    const string PrefabPath = "Assets/Resources/Prefabs/Town/CharacterUI.prefab";
    const string ShadowArtPath = "Assets/SPUM/Core/Basic_Resources/Ect/Shadow.png";

    [MenuItem("Tools/_归档/UI/生成角色界面预制体")]
    public static void Generate()
    {
        EnsureFolders();
        bool existed = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
        if (existed)
        {
            if (!EditorUtility.DisplayDialog(
                    "角色界面预制体",
                    "已存在 CharacterUI.prefab，将以现有内容为基础更新：\n" +
                    "· 保留已摆好的节点 / 图片 / 坐标 / 层级\n" +
                    "· 只补缺失节点，写回同一路径\n\n是否继续？",
                    "更新", "取消"))
                return;
        }
        BuildAndSave(true, existed);
    }

    /// <summary>批量入口：无交互。已存在则按同样规则更新。</summary>
    public static void GenerateBatch()
    {
        EnsureFolders();
        bool existed = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
        BuildAndSave(false, existed);
    }

    static void BuildAndSave(bool showDialog, bool existed)
    {
        // 已有则 LoadPrefabContents 打开原资源改增量；绝不新建覆盖，手改过的 Sprite / 坐标才保得住。
        GameObject root = existed
            ? PrefabUtility.LoadPrefabContents(PrefabPath)
            : CreateRoot();

        try
        {
            EnsureCharacterUi(root, !existed);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            if (existed) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);
        }

        // 注意：这里**不要**调用 AssetDatabase.SaveAssets()——它会把工程里所有内存中未保存的资源一并落盘，
        // 远超本预制体的范围。SaveAsPrefabAsset 本身已经把这个 prefab 写到磁盘了。
        AssetDatabase.Refresh();

        Debug.Log($"[CharacterUIPrefabGenerator] 已生成: {PrefabPath}（{(existed ? "基于现有内容增量更新" : "新建")}）");
        if (showDialog)
        {
            EditorUtility.DisplayDialog("角色界面",
                (existed ? "已基于现有内容更新 " : "已生成 ") + "Resources/Prefabs/Town/CharacterUI.prefab\n\n" +
                "· 已保留你摆好的图片 / 坐标 / 层级\n" +
                "· 缺失的 Portrait / shadow 节点由生成器补齐\n" +
                "· 立绘与脚底阴影由 CharacterUI 运行时兜底定位\n" +
                "· .meta 不手写，交给 Unity 生成",
                "OK");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null) Selection.activeObject = prefab;
        }
    }

    static GameObject CreateRoot()
    {
        var go = new GameObject("CharacterUI", typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();
        UICanvasSetup.Apply(canvas, null);
        canvas.sortingOrder = 20;
        return go;
    }

    static void EnsureCharacterUi(GameObject root, bool fresh)
    {
        if (root.GetComponent<RectTransform>() == null)
            root.AddComponent<RectTransform>();

        var canvas = root.GetComponent<Canvas>();
        if (canvas == null)
        {
            // 缺 Canvas 才套统一规范；已有的一律不重跑 Apply，免得重锚定把主人摆好的坐标改掉
            canvas = root.AddComponent<Canvas>();
            if (root.GetComponent<CanvasScaler>() == null) root.AddComponent<CanvasScaler>();
            if (root.GetComponent<GraphicRaycaster>() == null) root.AddComponent<GraphicRaycaster>();
            UICanvasSetup.Apply(canvas, null);
            canvas.sortingOrder = 20;
        }

        var ui = root.GetComponent<CharacterUI>();
        if (ui == null) ui = root.AddComponent<CharacterUI>();

        if (fresh || root.transform.Find("Content") == null)
        {
            // 只有从没建过树才整棵重建；已有 Content 一律走 AutoBind + 缺节点补全
            ui.BuildHierarchyForPrefab();
            return;
        }

        ui.AutoBind();
        EnsureMissingNodes(root.transform);
    }

    /// <summary>
    /// 只补缺的节点：看板立绘 Portrait、脚底阴影 shadow。
    /// 已有节点（含已赋好的图、坐标、层级）一个字段都不碰。
    /// </summary>
    static void EnsureMissingNodes(Transform root)
    {
        var stage = root.Find("Content/Stage");
        if (stage == null) return;

        if (stage.Find("Portrait") == null)
        {
            var portrait = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            portrait.transform.SetParent(stage, false);
            portrait.GetComponent<Image>().preserveAspect = true;
        }

        if (stage.Find("shadow") == null)
        {
            var shadow = new GameObject("shadow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            shadow.transform.SetParent(stage, false);
            var sImg = shadow.GetComponent<Image>();
            sImg.raycastTarget = false;
            sImg.color = new Color(0f, 0f, 0f, 0.54f);
            var sSp = AssetDatabase.LoadAssetAtPath<Sprite>(ShadowArtPath);
            if (sSp != null) sImg.sprite = sSp;

            // 具体位置与宽度交给 CharacterUI.EnsureStageShadow 按立绘实时算，这里只给默认底噪值
            var sRt = shadow.GetComponent<RectTransform>();
            sRt.anchorMin = new Vector2(0.5f, 0.5f);
            sRt.anchorMax = new Vector2(0.5f, 0.5f);
            sRt.pivot = new Vector2(0.5f, 0.5f);
            sRt.sizeDelta = new Vector2(1024f, 1024f);
            sRt.anchoredPosition = Vector2.zero;
        }
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs/Town"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs"))
                AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs/Town"))
                AssetDatabase.CreateFolder("Assets/Resources/Prefabs", "Town");
        }
    }
}
#endif
