using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【2026-10-17 主人拍板】日志里程等级展示与领取弹窗 —— <b>预制体生成器</b>。
///
/// <para>原来 <see cref="AchievementMilestoneUI"/> 是运行时代码白模（纯色块 + 硬编码坐标），
/// 与 DraftOddsPopup / EquipReplaceConfirm 同一套路子预制体化：<b>外观 100% 由预制体负责，
/// 脚本只填数据</b>。节点结构 / 坐标 / 配色与白模路径完全一致（Root / Dim / Panel / Title /
/// Close / Body / Claim），生成后运行时自动走预制体，代码白模仅作预制体缺失时的过渡。</para>
///
/// <para>生成后<b>主人手工调过之后就别再重跑</b>（会覆盖主人改的东西）—— 见 AGENTS.md 铁律 22。</para>
/// </summary>
public static class AchievementMilestonePrefabBuilder
{
    // ===== 出入口（唯一） =====
    const string PrefabPath = "Assets/Resources/Prefabs/UI/AchievementMilestoneUI.prefab";
    /// <summary>运行时 <see cref="AchievementMilestoneUI"/> 加载的路径，两处必须一致。</summary>
    const string RuntimeResourcePath = "Prefabs/UI/AchievementMilestoneUI";

    // ===== 字体（项目现有资源，只引用不复制） =====
    const string FontPath = "Assets/Resources/Fonts/fusion-pixel.ttf";

    // ===== 排版真值（与 AchievementMilestoneUI 白模路径同一把尺子） =====
    const float PanelW = 520f;
    const float PanelH = 560f;
    const float CloseSize = 40f;

    [MenuItem("Tools/UI/生成日志里程弹窗预制体")]
    public static void Build()
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (font == null)
        {
            Debug.LogError($"[AchievementMilestone] 找不到字体 {FontPath}，停止生成");
            return;
        }

        // ---- 根：Canvas + GraphicRaycaster（新 Canvas 必补 GraphicRaycaster）+ 业务脚本 ----
        var go = new GameObject("AchievementMilestoneUI",
            typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = GameConfig.UiSort.BattleMilestone;
        if (go.GetComponent<AchievementMilestoneUI>() == null)
            go.AddComponent<AchievementMilestoneUI>();

        var root = Node("Root", go.transform);
        Stretch(root);

        // ---- 遮罩：点它就关 ----
        var dim = Node("Dim", root);
        Stretch(dim);
        var dimImg = dim.gameObject.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.55f);
        var dimBtn = dim.gameObject.AddComponent<Button>();
        dimBtn.targetGraphic = dimImg;
        dimBtn.transition = Selectable.Transition.None;

        // ---- Panel ----
        var panel = Node("Panel", root);
        SetAnchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(PanelW, PanelH);
        var panelImg = panel.gameObject.AddComponent<Image>();
        panelImg.color = new Color(0.12f, 0.1f, 0.14f, 0.97f);

        // ---- 标题 ----
        var title = Node("Title", panel);
        TopAnchor(title, 0f, -16f, PanelW - 24f, 36f);
        var titleText = MakeText(title.gameObject, font, 28, Color.white, TextAnchor.MiddleCenter);
        titleText.text = "日志里程";

        // ---- 右上角关闭 ----
        var close = Node("Close", panel);
        SetAnchor(close, new Vector2(1f, 1f), new Vector2(1f, 1f));
        close.anchoredPosition = new Vector2(-12f, -12f);
        close.sizeDelta = new Vector2(CloseSize, CloseSize);
        var closeImg = close.gameObject.AddComponent<Image>();
        closeImg.color = new Color(0.35f, 0.25f, 0.18f, 1f);
        var closeBtn = close.gameObject.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        closeBtn.transition = Selectable.Transition.None;
        var closeText = MakeText(CreateChild(close, "Label").gameObject, font, 28, Color.white, TextAnchor.MiddleCenter);
        StretchText(closeText);
        closeText.text = "×";

        // ---- 正文（运行时由脚本填 Lv2–6 状态行） ----
        var body = Node("Body", panel);
        SetAnchor(body, new Vector2(0f, 0f), new Vector2(1f, 1f));
        body.offsetMin = new Vector2(20f, 70f);
        body.offsetMax = new Vector2(-20f, -60f);
        var bodyText = MakeText(body.gameObject, font, 18, Color.white, TextAnchor.UpperLeft);
        bodyText.text = "日志里程";

        // ---- 一键领取 ----
        var claim = Node("Claim", panel);
        SetAnchor(claim, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
        claim.anchoredPosition = new Vector2(0f, 16f);
        claim.sizeDelta = new Vector2(220f, 44f);
        var claimImg = claim.gameObject.AddComponent<Image>();
        claimImg.color = new Color(0.45f, 0.32f, 0.18f, 1f);
        var claimBtn = claim.gameObject.AddComponent<Button>();
        claimBtn.targetGraphic = claimImg;
        claimBtn.transition = Selectable.Transition.None;
        var claimText = MakeText(CreateChild(claim, "Label").gameObject, font, 22, Color.white, TextAnchor.MiddleCenter);
        StretchText(claimText);
        claimText.text = "一键领取";

        // 统一一次字体口径（与 DraftOddsPopup 生成器同款）
        GameFonts.ApplyToHierarchy(go.transform);

        EnsureFolder("Assets/Resources/Prefabs/UI");
        root.gameObject.SetActive(false);   // 建完先收好，运行时 Open 再放出来
        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[AchievementMilestone] 已生成 {PrefabPath}　（运行时加载路径：Resources/{RuntimeResourcePath}）");
    }

    // ===== 小工具（与 DraftOddsPopupPrefabBuilder 同一把尺子） =====

    static RectTransform Node(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static RectTransform CreateChild(RectTransform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static Text MakeText(GameObject go, Font font, int size, Color color, TextAnchor align)
    {
        var t = go.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    static void StretchText(Text t)
    {
        var rt = t.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>「父顶锚点」：往下为负，跟主人在 Inspector 里量 Panel 内节点同一把尺子。</summary>
    static void TopAnchor(RectTransform rt, float x, float y, float w, float h)
    {
        SetAnchor(rt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    static void SetAnchor(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        var leaf = System.IO.Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent))
            AssetDatabase.CreateFolder("Assets/Resources/Prefabs", "UI");
        else
            AssetDatabase.CreateFolder(parent, leaf);
    }
}
