#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 每日登录「完整弹窗预制体」生成器（2026-09-23）。
/// ⚠ 手动触发：菜单 Tools/每日登录/生成完整弹窗预制体。没有 [InitializeOnLoadMethod]，不会自动跑。
///
/// 为什么用脚本而不是手写 YAML：素材 guid 是 56 字符 base64，团结 TextPPtr 解析不了
/// （MEMORY 预制体纪律 #9：资源序列化引用一律让引擎自己写）。
/// 本脚本用 AssetDatabase.LoadAssetAtPath 取 Sprite 赋给 Image，SaveAsPrefabAsset 时由引擎写引用。
///
/// 节点名 = DailyLoginUI.TryBindFullPrefab 的绑定契约，改名前先改代码。
/// </summary>
public static class DailyLoginFullBuilder
{
    const string PrefabPath = "Assets/Resources/Prefabs/UI/DailyLoginFull.prefab";
    const string ArtDir = "Assets/Art/UI/每日登录";

    // 配色（对齐 DailyLoginUI 的常量）
    static readonly Color ColGold = new Color(1f, 0.84f, 0.42f, 1f);
    static readonly Color ColGoldDeep = new Color(0.72f, 0.50f, 0.14f, 1f);
    static readonly Color ColBlueTag = new Color(0.13f, 0.22f, 0.40f, 1f);
    static readonly Color ColText = new Color(0.94f, 0.92f, 0.88f, 1f);
    static readonly Color ColIconBg = new Color(1f, 1f, 1f, 0.06f);
    static readonly Color ColMercIconBg = new Color(1f, 1f, 1f, 0.08f);

    [MenuItem("Tools/每日登录/生成完整弹窗预制体")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog("生成每日登录预制体",
            "将覆盖重建 " + PrefabPath + "\n（节点树 + 素材引用全部重写，旧版已建议先备份）",
            "生成", "取消"))
            return;

        var log = new StringBuilder();
        var root = new GameObject("DailyLoginFull", typeof(RectTransform));
        string report;
        try
        {
            BuildTree(root, log);
            report = log.ToString();   // 日志在保存前拼完（项目纪律）
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
        Debug.Log("[DailyLoginFullBuilder] 生成完成 → " + PrefabPath + "\n" + report,
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        EditorUtility.DisplayDialog("每日登录预制体", "生成完成（详见 Console）", "好");
    }

    // ============================================================
    // 节点树
    // ============================================================

    static void BuildTree(GameObject root, StringBuilder log)
    {
        root.layer = 5;

        // ---- 画布层：Canvas + CanvasScaler（GraphicRaycaster 运行时由 UICanvasSetup 补）----
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 900;                       // = GameConfig.UiSort.TownPopup

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.referenceResolution = new Vector2(720f, 1280f);
        scaler.matchWidthOrHeight = 1f;                  // Match Height（排版铁律：逻辑高恒 1280）

        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        // ---- 背景 ----
        var bg = Node(root, "Bg_Full", V(0, 0), V(1, 1), V(0, 0), V(0, 0), V(0.5f, 0.5f));
        Img(bg.gameObject, Color.white, Art(log, "每日登录_0009_每日登录背景.png"));

        // ---- 格子区底板（纯装饰，格子挂在根下，坐标由 Layout() 运行时算）----
        var panel = Node(root, "CellsPanel", V(0.5f, 0.5f), V(0.5f, 0.5f), V(0f, 60f), V(672f, 445f), V(0.5f, 0.5f));
        Img(panel.gameObject, Color.white, Art(log, "每日登录_0006_天数底.png"), true);

        // ---- 8 天格子（挂根下，Layout() 按 canvas 宽度重排）----
        for (int i = 1; i <= 8; i++)
            BuildCell(root, i, log);

        // ---- 标题 ----
        var title = Node(root, "Title", V(0.5f, 1f), V(0.5f, 1f), V(0f, -6f), V(340f, 188f), V(0.5f, 1f));
        Img(title.gameObject, Color.white, Art(log, "每日登录_0007_每日登录字.png"), true);

        // ---- 三条横带 + 关闭 ----
        BuildCycleBar(root);
        BuildStreakBar(root);
        BuildInfoBar(root);
        BuildCloseButton(root);
    }

    static void BuildCell(GameObject root, int day, StringBuilder log)
    {
        var cell = Node(root, "Cell_" + day, V(0.5f, 0.5f), V(0.5f, 0.5f), V(0f, 0f), V(160f, 255f), V(0.5f, 0.5f));
        var isLast = day == 8;
        var bg = Img(cell.gameObject, Color.white,
            Art(log, isLast ? "每日登录_0004_限定英雄天.png" : "每日登录_0002_普通天.png"), true);
        bg.raycastTarget = true;                          // 有 Button，必须可点
        var btn = cell.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;

        // 第 N 天标签条
        var tag = Node(cell, "DayTag", V(0f, 1f), V(1f, 1f), V(0f, 0f), V(0f, 36f), V(0.5f, 1f));
        Img(tag.gameObject, ColBlueTag, null);
        var tagLabel = Node(tag, "Label", V(0f, 0f), V(1f, 1f), V(0f, 0f), V(0f, 0f), V(0.5f, 0.5f));
        Txt(tagLabel.gameObject, "第 " + day + " 天", 22, TextAnchor.MiddleCenter, ColGold);

        // 图标区
        var icon = Node(cell, "IconHolder", V(0.5f, 0.5f), V(0.5f, 0.5f), V(0f, 34f), V(96f, 96f), V(0.5f, 0.5f));
        Img(icon.gameObject, ColIconBg, null);
        var fb = Node(icon, "Fallback", V(0f, 0f), V(1f, 1f), V(0f, 0f), V(0f, 0f), V(0.5f, 0.5f));
        Txt(fb.gameObject, "", 40, TextAnchor.MiddleCenter, new Color(0.9f, 0.85f, 0.7f, 0.9f));

        // 奖励名
        var reward = Node(cell, "Reward", V(0.5f, 0f), V(0.5f, 0f), V(0f, 34f), V(150f, 52f), V(0.5f, 0.5f));
        Txt(reward.gameObject, "", 18, TextAnchor.MiddleCenter, ColText);

        // X2 角标（双倍日才显示）
        var x2 = Node(cell, "X2", V(1f, 1f), V(1f, 1f), V(-4f, -4f), V(44f, 44f), V(1f, 1f));
        Img(x2.gameObject, Color.white, Art(log, "每日登录_0001_x2.png"));
        x2.gameObject.SetActive(false);

        // 限定角标（最后一日）
        var rare = Node(cell, "Rare", V(1f, 1f), V(1f, 1f), V(-4f, -4f), V(56f, 32f), V(1f, 1f));
        Img(rare.gameObject, Color.white, Art(log, "每日登录_0003_限定字底.png"));
        var rareLabel = Node(rare, "Label", V(0f, 0f), V(1f, 1f), V(0f, 0f), V(0f, 0f), V(0.5f, 0.5f));
        Txt(rareLabel.gameObject, "限定", 18, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.5f, 1f));
        rare.gameObject.SetActive(false);

        // ★ 45° 已领斜字（新增；claimed 状态才显示）
        var mark = Node(cell, "Mark", V(0.5f, 0.5f), V(0.5f, 0.5f), V(0f, 0f), V(220f, 44f), V(0.5f, 0.5f));
        mark.localRotation = Quaternion.Euler(0f, 0f, 45f);
        var markTxt = Txt(mark.gameObject, "已 领", 34, TextAnchor.MiddleCenter, Color.white);
        var ol = mark.gameObject.AddComponent<Outline>();
        ol.effectColor = new Color(0f, 0f, 0f, 0.85f);
        ol.effectDistance = new Vector2(2f, -2f);
        mark.gameObject.SetActive(false);
    }

    static void BuildCycleBar(GameObject root)
    {
        var bar = Node(root, "CycleBar", V(0.03f, 0.5f), V(0.97f, 0.5f), V(0f, -300f), V(0f, 76f), V(0.5f, 0.5f));
        Img(bar.gameObject, Color.white, Art(null, "每日登录_0000_累计登录底.png"));

        var tag = Node(bar, "Tag", V(0f, 0.5f), V(0f, 0.5f), V(16f, 0f), V(120f, 40f), V(0f, 0.5f));
        Txt(tag.gameObject, "每日循环", 20, TextAnchor.MiddleLeft, ColGold);

        var name = Node(bar, "Name", V(0f, 0.5f), V(0f, 0.5f), V(140f, 0f), V(300f, 44f), V(0f, 0.5f));
        Txt(name.gameObject, "", 20, TextAnchor.MiddleLeft, ColText);

        var btn = Node(bar, "ClaimBtn", V(0f, 0.5f), V(0f, 0.5f), V(0f, 0f), V(140f, 52f), V(0.5f, 0.5f));
        Img(btn.gameObject, ColGoldDeep, null).raycastTarget = true;
        btn.gameObject.AddComponent<Button>();
        var bl = Node(btn, "Label", V(0f, 0f), V(1f, 1f), V(0f, 0f), V(0f, 0f), V(0.5f, 0.5f));
        Txt(bl.gameObject, "领取", 22, TextAnchor.MiddleCenter, new Color(0.16f, 0.06f, 0.04f, 1f));
    }

    static void BuildStreakBar(GameObject root)
    {
        var bar = Node(root, "StreakBar", V(0.03f, 0.5f), V(0.97f, 0.5f), V(0f, -382f), V(0f, 76f), V(0.5f, 0.5f));
        Img(bar.gameObject, Color.white, Art(null, "每日登录_0005_连续登录底.png"));

        var tag = Node(bar, "Tag", V(0f, 0.5f), V(0f, 0.5f), V(16f, 0f), V(150f, 40f), V(0f, 0.5f));
        Txt(tag.gameObject, "连击加成", 20, TextAnchor.MiddleLeft, ColGold);

        // 与 DailyLoginDefs.Streak 的天数对齐（当前 3/7/14/30；改名后重跑本生成器即可）
        int[] days = { 3, 7, 14, 30 };
        foreach (var d in days)
        {
            var b = Node(bar, "Streak_" + d, V(0f, 0.5f), V(0f, 0.5f), V(0f, 0f), V(100f, 52f), V(0.5f, 0.5f));
            Img(b.gameObject, new Color(0.22f, 0.18f, 0.12f, 1f), null).raycastTarget = true;
            b.gameObject.AddComponent<Button>();
            var bl = Node(b, "Label", V(0f, 0f), V(1f, 1f), V(0f, 0f), V(0f, 0f), V(0.5f, 0.5f));
            Txt(bl.gameObject, "", 15, TextAnchor.MiddleCenter, ColText);
        }
    }

    static void BuildInfoBar(GameObject root)
    {
        var bar = Node(root, "InfoBar", V(0.03f, 0.5f), V(0.97f, 0.5f), V(0f, -515f), V(0f, 118f), V(0.5f, 0.5f));
        Img(bar.gameObject, Color.white, Art(null, "每日登录_0008_累计大底.png"));

        var cap = Node(bar, "Cap", V(0f, 0.5f), V(0f, 0.5f), V(20f, 30f), V(200f, 34f), V(0f, 0.5f));
        Txt(cap.gameObject, "累计登录", 20, TextAnchor.MiddleLeft, ColGold);

        var days = Node(bar, "Days", V(0f, 0.5f), V(0f, 0.5f), V(20f, -18f), V(320f, 56f), V(0f, 0.5f));
        Txt(days.gameObject, "", 40, TextAnchor.MiddleLeft, ColText);

        var icon = Node(bar, "MercIcon", V(0f, 0.5f), V(0f, 0.5f), V(340f, 0f), V(88f, 88f), V(0.5f, 0.5f));
        Img(icon.gameObject, ColMercIconBg, null);

        var merc = Node(bar, "MercText", V(0f, 0.5f), V(0f, 0.5f), V(396f, 0f), V(420f, 80f), V(0f, 0.5f));
        Txt(merc.gameObject, "", 16, TextAnchor.MiddleLeft, ColText);
    }

    static void BuildCloseButton(GameObject root)
    {
        var close = Node(root, "CloseButton", V(1f, 1f), V(1f, 1f), V(-28f, -28f), V(64f, 64f), V(1f, 1f));
        Img(close.gameObject, ColGoldDeep, null).raycastTarget = true;
        close.gameObject.AddComponent<Button>();
        var l = Node(close, "Label", V(0f, 0f), V(1f, 1f), V(0f, 0f), V(0f, 0f), V(0.5f, 0.5f));
        Txt(l.gameObject, "X", 30, TextAnchor.MiddleCenter, new Color(0.98f, 0.94f, 0.86f, 1f));
    }

    // ============================================================
    // 工具
    // ============================================================

    static Sprite Art(StringBuilder log, string file)
    {
        var p = ArtDir + "/" + file;
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(p);
        if (s == null && log != null)
            log.AppendLine("⚠ 找不到 Sprite: " + p);
        return s;
    }

    static RectTransform Node(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Vector2 pivot)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.anchoredPosition = pos; rt.sizeDelta = size; rt.pivot = pivot;
        return rt;
    }

    /// <summary>GameObject 版重载：建树时手上的往往是 GameObject，省得每处写 .transform。</summary>
    static RectTransform Node(GameObject parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Vector2 pivot)
        => Node(parent != null ? parent.transform : null, name, aMin, aMax, pos, size, pivot);

    static Image Img(GameObject go, Color c, Sprite sp, bool preserveAspect = false)
    {
        var img = go.AddComponent<Image>();
        img.color = c;
        img.sprite = sp;
        img.preserveAspect = preserveAspect;
        img.raycastTarget = false;
        return img;
    }

    static Text Txt(GameObject go, string s, int size, TextAnchor align, Color c)
    {
        var t = go.AddComponent<Text>();
        t.text = s;
        t.fontSize = size;
        t.alignment = align;
        t.color = c;
        t.raycastTarget = false;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.font = GetFont();
        return t;
    }

    static Font GetFont()
    {
        Font f = null;
        try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (f == null)
        {
            try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
        }
        return f;   // 运行时 Open() 里 GameFonts.ApplyToHierarchy 还会统一刷一遍
    }

    static Vector2 V(float x, float y) => new Vector2(x, y);
}
#endif
