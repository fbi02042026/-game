using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一次性脚手架：生成「小游戏合集」弹窗预制体
/// Assets/Resources/Prefabs/UI/MiniGameHubPanel.prefab。
/// 菜单：Tools → 小游戏合集 → 生成合集弹窗预制体。
///
/// 🔴 项目铁律：主人手改过预制体之后【禁止重跑本生成器】——
/// 重跑 = 排版打回旧版 + 手工节点被抹掉。改排版一律直接改 prefab。
///
/// 🎨 美术来源（Resources/UI/Art/小游戏/，6 张全是 56 字符 guid，按路径加载不进资源引用链）：
///   弹窗       600×711  整张弹窗底图（礼盒蝴蝶结/木框/米色面板，右上角留黑色圆孔给关闭按钮）
///   关闭       87×87    圆形 X
///   进入按钮   84×89    橙色圆形按钮
///   竹篓纳福   399×141  横条（自带标题文字）
///   逐福狂奔   399×141  横条（自带标题文字）
///   礼盒派对   100×93   主界面合集入口图标（本预制体不消费，归 MiniGameHubEntryUI）
///
/// 🔴 56 字符 guid 落不了 prefab：本生成器【绝不给任何 Image.sprite 赋图】，sprite 一律留空，
/// 由运行时 Assets/Scripts/UI/MiniGameHubUI.cs 的 Bind() 按 Resources 路径主动设置；
/// 取不到图运行时 Debug.LogError 报路径，不静默跳过、不造占位色块兜底。
///
/// 📐 节点契约（与 MiniGameHubUI.Bind 的节点名一一对应；720×1280 竖屏逻辑坐标，主人手调为准）：
/// MiniGameHubPanel(root: Canvas+CanvasScaler(Match Height 1280)+GraphicRaycaster+MiniGameHubUI, 保存为 inactive)
/// └─ Root（stretch，透明，raycastTarget=false）
///    ├─ Dim（stretch，黑 0.62，raycastTarget=true，Button；onClick 运行时绑）
///    └─ Panel（660×782 @ (0,0)，底图「弹窗」，color=white，raycastTarget=false）
///       ├─ CloseBtn   （96×96   @ ( 248,  237)，透明热区 + Button，运行时填「关闭」图）
///       ├─ RowBamboo  （455×161 @ ( -20,   29)，透明占位，raycastTarget=false，运行时填「竹篓纳福」横条）
///       ├─ EnterBamboo（110×117 @ ( 160,   29)，透明热区 + Button，运行时填「进入按钮」图）
///       ├─ RowRun     （455×161 @ ( -20, -143)，透明占位，raycastTarget=false，运行时填「逐福狂奔」横条）
///       └─ EnterRun   （110×117 @ ( 160, -143)，透明热区 + Button，运行时填「进入按钮」图）
/// 横条图 / 圆形按钮图自带文字与图形，故不建任何 Text 节点、不写文案。
/// </summary>
public static class MiniGameHubPrefabBuilder
{
    const string SavePath = "Assets/Resources/Prefabs/UI/MiniGameHubPanel.prefab";

    // ── 尺寸/坐标（720×1280 竖屏逻辑坐标；主人手调为准）──
    // 面板居中 @ (0,0)
    const float PanelW = 660f;
    const float PanelH = 782f;
    const float PanelX = 0f;
    const float PanelY = 0f;
    // 右上关闭热区
    const float CloseW = 96f;
    const float CloseH = 96f;
    const float CloseX = 248f;
    const float CloseY = 237f;
    // 两个横条
    const float RowW = 455f;
    const float RowH = 161f;
    const float RowBambooX = -20f;
    const float RowBambooY = 29f;
    const float RowRunX = -20f;
    const float RowRunY = -143f;
    // 两个进入按钮
    const float EnterW = 110f;
    const float EnterH = 117f;
    const float EnterBambooX = 160f;
    const float EnterBambooY = 29f;
    const float EnterRunX = 160f;
    const float EnterRunY = -143f;

    [MenuItem("Tools/小游戏合集/生成合集弹窗预制体")]
    public static void Build()
    {
        if (File.Exists(SavePath))
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "合集弹窗预制体已存在",
                "MiniGameHubPanel.prefab 已存在。\n\n" +
                "⚠️ 本菜单是生成器脚手架：主人手改过预制体后再重跑 = 排版打回旧版、手工节点被抹掉（项目铁律）。\n\n" +
                "确定要覆盖？",
                "覆盖重来", "取消");
            if (!overwrite) return;
        }

        // ── 根：Canvas + MiniGameHubUI ──
        var root = new GameObject("MiniGameHubPanel", typeof(RectTransform));
        root.AddComponent<Canvas>();
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720f, 1280f);
        scaler.matchWidthOrHeight = 1f;
        root.AddComponent<GraphicRaycaster>();
        root.AddComponent<MiniGameHubUI>();
        Stretch(root.GetComponent<RectTransform>());

        var rootChild = MakeImage(root.transform, "Root", Color.clear, false);
        Stretch(rootChild.rectTransform);

        // ── Dim：全屏遮罩，点击关窗由 MiniGameHubUI 运行时补挂 Button 监听 ──
        var dim = MakeImage(rootChild.transform, "Dim", new Color(0f, 0f, 0f, 0.62f), true);
        Stretch(dim.rectTransform);
        dim.gameObject.AddComponent<Button>();

        // ── Panel：整张弹窗底图（sprite 留空，运行时 Bind 按路径填图）──
        var panel = MakeImage(rootChild.transform, "Panel", Color.white, false);
        SetRT(panel.rectTransform, V(0.5f, 0.5f), V(0.5f, 0.5f), V(0.5f, 0.5f),
            V2(PanelX, PanelY), V2(PanelW, PanelH));
        var p = panel.transform;

        // ── 右上关闭热区（透明，运行时填「关闭」图）──
        MakeHotspot(p, "CloseBtn", CloseX, CloseY, CloseW, CloseH);

        // ── 竹篓纳福：横条占位 + 进入按钮热区 ──
        MakePlaceholder(p, "RowBamboo", RowBambooX, RowBambooY, RowW, RowH);
        MakeHotspot(p, "EnterBamboo", EnterBambooX, EnterBambooY, EnterW, EnterH);

        // ── 逐福狂奔：横条占位 + 进入按钮热区 ──
        MakePlaceholder(p, "RowRun", RowRunX, RowRunY, RowW, RowH);
        MakeHotspot(p, "EnterRun", EnterRunX, EnterRunY, EnterW, EnterH);

        // ── 保存（保存为 inactive：实例化即隐藏，由 MiniGameHubUI 控制显隐）──
        root.SetActive(false);
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs/UI"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs"))
                AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");
            AssetDatabase.CreateFolder("Assets/Resources/Prefabs", "UI");
        }
        PrefabUtility.SaveAsPrefabAsset(root, SavePath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[MiniGameHubBuilder] 合集弹窗预制体已生成：" + SavePath +
            "（Image.sprite 全部留空，图由 MiniGameHubUI.Bind 运行时按路径加载；.meta 由 Unity 自动生成）");
    }

    // ===== 构建辅助（本文件自持最小件；所有 Image 都不赋 sprite）=====

    /// <summary>建 Image 节点：只设颜色与射线，sprite 保持留空（不写 img.sprite，杜绝 guid 落盘）。</summary>
    static Image MakeImage(Transform parent, string name, Color color, bool raycastTarget)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycastTarget;
        return img;
    }

    /// <summary>可点热区：透明 Image + Button（尺寸/坐标按契约）。</summary>
    static void MakeHotspot(Transform parent, string name, float x, float y, float w, float h)
    {
        var img = MakeImage(parent, name, Color.clear, true);
        SetRT(img.rectTransform, V(0.5f, 0.5f), V(0.5f, 0.5f), V(0.5f, 0.5f), V2(x, y), V2(w, h));
        img.gameObject.AddComponent<Button>();
    }

    /// <summary>不可点占位：透明 Image，raycastTarget=false。</summary>
    static void MakePlaceholder(Transform parent, string name, float x, float y, float w, float h)
    {
        var img = MakeImage(parent, name, Color.clear, false);
        SetRT(img.rectTransform, V(0.5f, 0.5f), V(0.5f, 0.5f), V(0.5f, 0.5f), V2(x, y), V2(w, h));
    }

    static void SetRT(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Vector2 V(float x, float y) { return new Vector2(x, y); }
    static Vector2 V2(float x, float y) { return new Vector2(x, y); }
}
