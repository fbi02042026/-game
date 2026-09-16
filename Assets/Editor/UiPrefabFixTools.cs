using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 2026-09-16 一次性修复工具。
/// 关键：所有改动都在 Unity 内部完成并由 Unity 自己保存，
/// 避免「外部改 prefab 被 Unity 内存版本覆盖回去」。
///
/// 历史坑记录：
/// 1) 之前把 Debug.Log 写在 UnloadPrefabContents 之后 —— 那时 root 已被销毁，
///    UnityEngine.Object 的自定义 == 会把已销毁对象判成 null，于是日志全是 False，
///    实际接线其实是好的。所有判空必须在 Unload 之前算成 bool。
/// 2) 外层 Region_X 常常只是空 RectTransform，Image/Button 在同名的内层子节点上，
///    绑定时要用内层节点（WorldMapPopup 里已加 slot.visual）。
/// </summary>
public static class UiPrefabFixTools
{
    const string WmpPath = "Assets/Resources/Prefabs/UI/WorldMapPopup.prefab";
    const string JobSelectPath = "Assets/Resources/Prefabs/UI/PlayerJobSelect.prefab";
    const string TalentPath = "Assets/Resources/Prefabs/Talent/TalentUI.prefab";
    const string ReportPath = "Temp/ui_fix_report.txt";

    static readonly StringBuilder Rep = new StringBuilder();

    static void Log(string s)
    {
        Debug.Log("[UiPrefabFixTools] " + s);
        Rep.AppendLine(s);
    }

    static void FlushReport(string title)
    {
        try
        {
            var full = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, title + "\n" + Rep.ToString(), Encoding.UTF8);
            Debug.Log("[UiPrefabFixTools] 报告已写入 " + ReportPath);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[UiPrefabFixTools] 报告写入失败：" + e.Message);
        }
    }

    /// <summary>递归按名字找节点（Transform.Find 只找直接子节点，容易漏）。</summary>
    static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var r = FindDeep(root.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    static string PathOf(Transform t)
    {
        var sb = new StringBuilder();
        while (t != null)
        {
            sb.Insert(0, "/" + t.name);
            t = t.parent;
        }
        return sb.ToString();
    }

    // ============================================================
    // 0. 只看不改：把 WorldMapPopup 的节点树和接线状态打到报告里
    // ============================================================
    [MenuItem("Tools/修复/0. 诊断：WorldMapPopup 节点树与接线状态")]
    public static void DiagnoseWorldMapPopup()
    {
        Rep.Length = 0;
        var root = PrefabUtility.LoadPrefabContents(WmpPath);
        if (root == null)
        {
            Debug.LogError("[UiPrefabFixTools] 打不开 " + WmpPath);
            return;
        }

        Log("根节点：" + root.name);

        // 挂载的自定义脚本
        var mb = root.GetComponents<MonoBehaviour>();
        Log("根节点自定义脚本数：" + mb.Length);
        for (int i = 0; i < mb.Length; i++)
            Log("  - " + (mb[i] != null ? mb[i].GetType().FullName : "<Missing Script>"));

        var wmp = root.GetComponent<WorldMapPopup>();

        // 节点树（限制深度，避免刷屏）
        DumpTree(root.transform, 0, 3);

        // 关键节点
        var regions = FindDeep(root.transform, "Regions");
        var title = FindDeep(root.transform, "Title");
        var desc = FindDeep(root.transform, "Desc");
        var mapBg = FindDeep(root.transform, "MapBackground");
        var bg = FindDeep(root.transform, "bg");

        Log("--- 关键节点 ---");
        Log("Regions       : " + (regions != null ? PathOf(regions) + "  子节点=" + regions.childCount : "未找到"));
        Log("Title         : " + (title != null ? PathOf(title) : "未找到"));
        Log("Desc          : " + (desc != null ? PathOf(desc) : "未找到"));
        Log("MapBackground : " + (mapBg != null ? PathOf(mapBg) : "未找到"));
        Log("bg            : " + (bg != null ? PathOf(bg) : "未找到"));

        if (regions != null)
        {
            Log("--- Region 子节点 ---");
            for (int i = 0; i < regions.childCount; i++)
            {
                var t = regions.GetChild(i);
                var inner = t.Find(t.name);
                var vis = t;
                if (t.GetComponent<Image>() == null && inner != null && inner.GetComponent<Image>() != null)
                    vis = inner;
                Log(string.Format(
                    "  {0,-10} outer[Img={1} Btn={2}] inner={3} visual={4}[Img={5} Btn={6}] HL={7} Lock={8} Enter={9}",
                    t.name,
                    t.GetComponent<Image>() != null,
                    t.GetComponent<Button>() != null,
                    inner != null ? inner.name : "-",
                    vis.name,
                    vis.GetComponent<Image>() != null,
                    vis.GetComponent<Button>() != null,
                    t.Find("Highlight") != null,
                    t.Find("Lock") != null,
                    t.Find("EnterButton") != null));
            }
        }

        if (wmp != null)
        {
            Log("--- WorldMapPopup 当前字段 ---");
            Log("  regionRoot     = " + (wmp.regionRoot != null ? PathOf(wmp.regionRoot) : "null"));
            Log("  mapBackground  = " + (wmp.mapBackground != null ? PathOf(wmp.mapBackground.transform) : "null"));
            Log("  titleText      = " + (wmp.titleText != null ? PathOf(wmp.titleText.transform) : "null"));
            Log("  descText       = " + (wmp.descText != null ? PathOf(wmp.descText.transform) : "null"));
            Log("  enterButton    = " + (wmp.enterButton != null ? PathOf(wmp.enterButton.transform) : "null"));
            Log("  regions.Length = " + (wmp.regions != null ? wmp.regions.Length.ToString() : "null"));
        }
        else
        {
            Log("!!! 预制体上还没有 WorldMapPopup 组件");
        }

        PrefabUtility.UnloadPrefabContents(root);
        FlushReport("=== WorldMapPopup 诊断 ===");
    }

    static void DumpTree(Transform t, int depth, int maxDepth)
    {
        if (depth > maxDepth) return;
        Log(new string(' ', depth * 2) + "|- " + t.name);
        for (int i = 0; i < t.childCount; i++)
            DumpTree(t.GetChild(i), depth + 1, maxDepth);
    }

    // ============================================================
    // 1. WorldMapPopup 挂脚本并接线
    // ============================================================
    [MenuItem("Tools/修复/1. WorldMapPopup 挂脚本并接线")]
    public static void FixWorldMapPopup()
    {
        Rep.Length = 0;
        var root = PrefabUtility.LoadPrefabContents(WmpPath);
        if (root == null)
        {
            Debug.LogError("[UiPrefabFixTools] 打不开 " + WmpPath);
            return;
        }

        var wmp = root.GetComponent<WorldMapPopup>();
        bool added = false;
        if (wmp == null)
        {
            wmp = root.AddComponent<WorldMapPopup>();
            added = true;
        }
        Log(added ? "已补挂 WorldMapPopup 脚本" : "WorldMapPopup 脚本已存在");

        var regions = FindDeep(root.transform, "Regions");
        var title = FindDeep(root.transform, "Title");
        var desc = FindDeep(root.transform, "Desc");
        var mapBg = FindDeep(root.transform, "MapBackground");
        var bg = FindDeep(root.transform, "bg");

        int regionCount = 0;
        if (regions != null)
        {
            for (int i = 0; i < regions.childCount; i++)
                if (regions.GetChild(i).name.StartsWith("Region_")) regionCount++;
        }

        if (regions != null) wmp.regionRoot = regions;
        if (mapBg != null) wmp.mapBackground = mapBg.GetComponent<Image>();
        if (wmp.mapBackground == null && bg != null) wmp.mapBackground = bg.GetComponent<Image>();
        if (title != null) wmp.titleText = title.GetComponent<Text>();
        if (desc != null) wmp.descText = desc.GetComponent<Text>();

        // 共享「进入」按钮：取 Region_2 下面那颗（Region_1 美术没放）。
        // 实际显示逻辑优先用每个地块自己的 EnterButton。
        var eb = FindDeep(root.transform, "EnterButton");
        if (eb != null)
        {
            wmp.enterButton = eb.GetComponent<Button>();
            wmp.enterButtonText = eb.GetComponentInChildren<Text>();
        }

        EditorUtility.SetDirty(wmp);
        EditorUtility.SetDirty(root);

        // ★ 所有判空必须在这里算完 —— Unload 之后引用会变成"假空"
        bool okRegion = wmp.regionRoot != null;
        bool okBg = wmp.mapBackground != null;
        bool okTitle = wmp.titleText != null;
        bool okDesc = wmp.descText != null;
        bool okEnter = wmp.enterButton != null;

        PrefabUtility.SaveAsPrefabAsset(root, WmpPath);
        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Log("--- 保存前状态 ---");
        Log("  Region 子节点数 = " + regionCount);
        Log("  regionRoot     = " + okRegion);
        Log("  mapBackground  = " + okBg);
        Log("  titleText      = " + okTitle);
        Log("  descText       = " + okDesc);
        Log("  enterButton    = " + okEnter);

        // ---- 重新加载复核（看磁盘上到底写进去了没有）----
        var check = PrefabUtility.LoadPrefabContents(WmpPath);
        var cw = check != null ? check.GetComponent<WorldMapPopup>() : null;
        Log("--- 重新加载复核（磁盘真值）---");
        if (cw == null)
        {
            Log("  !!! 磁盘上仍然没有 WorldMapPopup 组件 —— 保存没生效");
            Log("  常见原因：预制体正开着 Prefab Mode，或场景里有未应用的实例被 Unity 内存版本覆盖回去。");
            Log("  处理：关掉 Prefab Mode 窗口、保存场景，再跑一次本工具。");
        }
        else
        {
            Log("  regionRoot     = " + (cw.regionRoot != null ? PathOf(cw.regionRoot) : "null"));
            Log("  mapBackground  = " + (cw.mapBackground != null ? PathOf(cw.mapBackground.transform) : "null"));
            Log("  titleText      = " + (cw.titleText != null ? PathOf(cw.titleText.transform) : "null"));
            Log("  descText       = " + (cw.descText != null ? PathOf(cw.descText.transform) : "null"));
            Log("  enterButton    = " + (cw.enterButton != null ? PathOf(cw.enterButton.transform) : "null"));
        }
        if (check != null) PrefabUtility.UnloadPrefabContents(check);

        FlushReport("=== WorldMapPopup 修复 ===");
    }

    // ============================================================
    // 2. 移除 PlayerJobSelect 上的 Missing Script
    // ============================================================
    [MenuItem("Tools/修复/2. 移除 PlayerJobSelect 上的 Missing Script")]
    public static void RemoveMissingScripts()
    {
        Rep.Length = 0;
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
            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, JobSelectPath);
            AssetDatabase.SaveAssets();
            Log("PlayerJobSelect 移除 Missing Script " + total + " 个");
        }
        else
        {
            Log("PlayerJobSelect 上没有 Missing Script（Unity 已经自己清掉了）");
        }
        PrefabUtility.UnloadPrefabContents(root);
        FlushReport("=== PlayerJobSelect 清理 ===");
    }

    // ============================================================
    // 3. 死链图 → 空白（只把 sprite 置空，绝不改排版）
    //    目标：TalentUI / TalentUI/Panel/ResourceRow/StonePlus 的 Image
    //    guid 090e1c7b819f3324893f067082340234 在工程里已不存在。
    //    同级另外两个 PlusButton 本来就是 {fileID: 0}，这里保持一致。
    // ============================================================
    [MenuItem("Tools/修复/3. TalentUI 死链图置空白（StonePlus）")]
    public static void BlankDeadSprite()
    {
        Rep.Length = 0;
        var root = PrefabUtility.LoadPrefabContents(TalentPath);
        if (root == null)
        {
            Debug.LogError("[UiPrefabFixTools] 打不开 " + TalentPath);
            return;
        }

        var t = FindDeep(root.transform, "StonePlus");
        if (t == null)
        {
            Log("未找到 StonePlus 节点");
            PrefabUtility.UnloadPrefabContents(root);
            FlushReport("=== TalentUI 死图置空 ===");
            return;
        }

        Log("命中节点：" + PathOf(t));
        var img = t.GetComponent<Image>();
        bool changed = false;
        if (img != null)
        {
            // 只改 sprite 一个字段，RectTransform / 子节点 / 其它组件一个都不动
            img.sprite = null;
            changed = true;
            Log("  Image.sprite 已置空");
        }
        else
        {
            Log("  StonePlus 上没有 Image");
        }

        if (changed)
        {
            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, TalentPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        PrefabUtility.UnloadPrefabContents(root);

        // 复核
        var check = PrefabUtility.LoadPrefabContents(TalentPath);
        var ct = check != null ? FindDeep(check.transform, "StonePlus") : null;
        var cimg = ct != null ? ct.GetComponent<Image>() : null;
        Log("复核：StonePlus sprite = " + (cimg != null ? (cimg.sprite == null ? "null（空白，OK）" : cimg.sprite.name) : "无 Image"));
        if (check != null) PrefabUtility.UnloadPrefabContents(check);

        FlushReport("=== TalentUI 死图置空 ===");
    }
}
