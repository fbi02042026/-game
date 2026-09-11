using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 节点布局角色：与命名规范对应，见 UiLayoutConvention.md。
/// </summary>
public enum UiLayoutRole
{
    Unknown,
    /// <summary>手调美术底图（Background / BgArt），禁止改 rect。</summary>
    Fixed,
    /// <summary>全屏铺满（BgStretch / Bg / Dim）。</summary>
    FillScreen,
    /// <summary>仅横向拉满（map / MapRoot）。</summary>
    StretchHorizontal,
}

/// <summary>
/// 集中 UI 自适应拉伸；禁止对泛化 Background 无差别 Stretch。
/// </summary>
public static class UiLayoutStretch
{
    public static UiLayoutRole InferRole(string nodeName)
    {
        if (string.IsNullOrEmpty(nodeName)) return UiLayoutRole.Unknown;
        if (IsFillScreenName(nodeName)) return UiLayoutRole.FillScreen;
        if (IsFixedArtName(nodeName)) return UiLayoutRole.Fixed;
        if (IsStretchHorizontalName(nodeName)) return UiLayoutRole.StretchHorizontal;
        return UiLayoutRole.Unknown;
    }

    public static bool IsFillScreenName(string nodeName)
    {
        return nodeName.Equals("BgStretch", System.StringComparison.OrdinalIgnoreCase)
               || nodeName.Equals("BgFill", System.StringComparison.OrdinalIgnoreCase)
               || nodeName.Equals("Bg", System.StringComparison.OrdinalIgnoreCase)
               || nodeName.Equals("Dim", System.StringComparison.OrdinalIgnoreCase)
               || nodeName.Equals("ModalDim", System.StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsFixedArtName(string nodeName)
    {
        return nodeName.Equals("Background", System.StringComparison.OrdinalIgnoreCase)
               || nodeName.Equals("BgArt", System.StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsStretchHorizontalName(string nodeName)
    {
        return nodeName.Equals("map", System.StringComparison.OrdinalIgnoreCase)
               || nodeName.Equals("Map", System.StringComparison.OrdinalIgnoreCase)
               || nodeName.Equals("MapRoot", System.StringComparison.OrdinalIgnoreCase)
               || nodeName.Equals("Maproot", System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>四边贴父级，用于 Dim / ModalDim 等纯色遮罩。</summary>
    public static void ApplyFillScreen(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        if (rt.localScale.sqrMagnitude < 0.0001f)
            rt.localScale = Vector3.one;
    }

    /// <summary>
    /// 全屏底图（BgStretch / Bg）：中心锚点 + EnvelopeParent（与剧情 BG 一致），避免 stretch+Envelope 在长屏偏左。
    /// Toggle/Slider 等控件内的 Background 子节点一律跳过。
    /// </summary>
    public static void ApplyBgStretch(RectTransform rt, Image image = null)
    {
        if (rt == null || IsWidgetPart(rt)) return;
        if (!IsBgStretchArtName(rt.gameObject.name)) return;
        if (image == null) image = rt.GetComponent<Image>();
        if (image == null) return;
        float aspect = GameConfig.DESIGN_WIDTH / GameConfig.DESIGN_HEIGHT;
        var sp = image.sprite;
        if (sp != null)
            aspect = sp.rect.width / Mathf.Max(1f, sp.rect.height);
        ApplyEnvelopeCenter(rt, aspect);
        image.preserveAspect = false;
    }

    /// <summary>任意美术全屏层（如 Backdrop）：不校验节点名，中心 + Envelope。</summary>
    public static void ApplyEnvelopeImage(RectTransform rt, Image image = null)
    {
        if (rt == null || IsWidgetPart(rt)) return;
        if (image == null) image = rt.GetComponent<Image>();
        if (image == null) return;
        float aspect = GameConfig.DESIGN_WIDTH / GameConfig.DESIGN_HEIGHT;
        var sp = image.sprite;
        if (sp != null)
            aspect = sp.rect.width / Mathf.Max(1f, sp.rect.height);
        ApplyEnvelopeCenter(rt, aspect);
        image.preserveAspect = false;
    }

    /// <summary>全屏视频 RawImage：中心 + Envelope，避免长屏偏左。</summary>
    public static void ApplyEnvelopeRawImage(RectTransform rt, float aspectRatio)
    {
        if (rt == null) return;
        if (aspectRatio < 0.01f)
            aspectRatio = GameConfig.DESIGN_WIDTH / GameConfig.DESIGN_HEIGHT;
        ApplyEnvelopeCenter(rt, aspectRatio);
    }

    /// <summary>中心锚点铺满父级再 EnvelopeParent。</summary>
    public static void ApplyEnvelopeCenter(RectTransform rt, float aspectRatio)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        if (rt.localScale.sqrMagnitude < 0.0001f)
            rt.localScale = Vector3.one;

        var parent = rt.parent as RectTransform;
        float pw = parent != null ? parent.rect.width : GameConfig.DESIGN_WIDTH;
        float ph = parent != null ? parent.rect.height : GameConfig.DESIGN_HEIGHT;
        if (pw < 1f) pw = GameConfig.DESIGN_WIDTH;
        if (ph < 1f) ph = GameConfig.DESIGN_HEIGHT;
        rt.sizeDelta = new Vector2(pw, ph);

        var fitter = rt.GetComponent<AspectRatioFitter>();
        if (fitter == null) fitter = rt.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = Mathf.Max(0.01f, aspectRatio);
    }

    static bool IsBgStretchArtName(string nodeName)
    {
        return nodeName.Equals("BgStretch", System.StringComparison.OrdinalIgnoreCase)
               || nodeName.Equals("BgFill", System.StringComparison.OrdinalIgnoreCase)
               || nodeName.Equals("Bg", System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Unity 标准控件零件（Toggle Background、Slider Handle 等）：永远以预制体 rect 为准。
    /// </summary>
    public static bool IsWidgetPart(Transform t)
    {
        if (t == null) return false;
        var p = t;
        while (p != null)
        {
            if (p.GetComponent<Toggle>() != null
                || p.GetComponent<Slider>() != null
                || p.GetComponent<Scrollbar>() != null
                || p.GetComponent<Dropdown>() != null)
                return true;
            p = p.parent;
        }
        return false;
    }

    /// <summary>是否允许代码改 rect（仅 BgStretch/Bg/Dim/map 等明确节点）。</summary>
    public static bool MayStretchByCode(RectTransform rt)
    {
        if (rt == null || IsWidgetPart(rt)) return false;
        var role = InferRole(rt.gameObject.name);
        return role == UiLayoutRole.FillScreen || role == UiLayoutRole.StretchHorizontal;
    }

    /// <summary>战斗 map 等：左右拉满，高度/纵向 offset 保持预制体设定。</summary>
    public static void ApplyStretchHorizontal(RectTransform rt)
    {
        if (rt == null) return;

        float yMin = rt.anchorMin.y;
        float yMax = rt.anchorMax.y;
        float oMinY = rt.offsetMin.y;
        float oMaxY = rt.offsetMax.y;
        Vector2 pivot = rt.pivot;
        float posY = rt.anchoredPosition.y;
        float sizeY = rt.sizeDelta.y;

        if (Mathf.Abs(rt.anchorMin.x - rt.anchorMax.x) < 0.01f)
        {
            rt.anchorMin = new Vector2(0f, yMin);
            rt.anchorMax = new Vector2(1f, yMax);
            rt.pivot = new Vector2(0.5f, pivot.y);
            rt.offsetMin = new Vector2(0f, oMinY);
            rt.offsetMax = new Vector2(0f, oMaxY);
            if (Mathf.Abs(yMin - yMax) < 0.01f)
            {
                rt.anchoredPosition = new Vector2(0f, posY);
                rt.sizeDelta = new Vector2(0f, sizeY);
            }
        }
        else
        {
            rt.offsetMin = new Vector2(0f, rt.offsetMin.y);
            rt.offsetMax = new Vector2(0f, rt.offsetMax.y);
        }
    }

    /// <summary>按节点名推断角色并应用（Fixed / Unknown 不改动）。</summary>
    public static void ApplyForNode(RectTransform rt)
    {
        if (rt == null) return;
        switch (InferRole(rt.gameObject.name))
        {
            case UiLayoutRole.FillScreen:
                if (IsBgStretchArtName(rt.gameObject.name))
                    ApplyBgStretch(rt);
                else
                    ApplyFillScreen(rt);
                break;
            case UiLayoutRole.StretchHorizontal:
                ApplyStretchHorizontal(rt);
                break;
        }
    }

    /// <summary>在 battleUIRoot 下查找 map，横向拉满并尽量纵向撑满顶栏与背包之间。</summary>
    public static void ApplyBattleMapWidth(Transform battleUIRoot)
    {
        if (battleUIRoot == null) return;
        RectTransform mapRt = null;
        for (int i = 0; i < battleUIRoot.childCount; i++)
        {
            var c = battleUIRoot.GetChild(i);
            if (!IsStretchHorizontalName(c.name)) continue;
            mapRt = c as RectTransform;
            break;
        }
        if (mapRt == null) return;

        var top = FindNamed(battleUIRoot, "TopBar", "ResourceBar", "TopResourceBar", "HUDTop");
        var backpack = FindNamed(battleUIRoot, "BackpackPanel", "Backpack", "EquipPanel");

        if (top == null && backpack == null)
        {
            ApplyStretchHorizontal(mapRt);
            return;
        }

        // 顶栏底 ~ 背包顶：纵向 stretch，左右拉满（仅 map 节点）
        float topInset = 0f;
        float botInset = 0f;
        var parent = mapRt.parent as RectTransform;
        if (parent != null)
        {
            float parentH = Mathf.Max(1f, parent.rect.height);
            if (top != null)
            {
                float topBot = GetWorldRectBottomInParent(top, parent);
                topInset = Mathf.Clamp(parentH - topBot, 0f, parentH * 0.45f);
            }
            if (backpack != null)
            {
                float packTop = GetWorldRectTopInParent(backpack, parent);
                botInset = Mathf.Clamp(packTop, 0f, parentH * 0.55f);
            }
            if (topInset + botInset > parentH * 0.85f)
            {
                // 锚点解析失败时退回仅横向
                ApplyStretchHorizontal(mapRt);
                return;
            }
        }

        mapRt.anchorMin = new Vector2(0f, 0f);
        mapRt.anchorMax = new Vector2(1f, 1f);
        mapRt.pivot = new Vector2(0.5f, 0.5f);
        mapRt.offsetMin = new Vector2(0f, botInset);
        mapRt.offsetMax = new Vector2(0f, -topInset);
        if (mapRt.localScale.sqrMagnitude < 0.0001f)
            mapRt.localScale = Vector3.one;
    }

    static RectTransform FindNamed(Transform root, params string[] names)
    {
        if (root == null || names == null) return null;
        for (int i = 0; i < names.Length; i++)
        {
            var t = FindDeep(root, names[i]);
            if (t != null) return t as RectTransform;
        }
        return null;
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name)) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var f = FindDeep(root.GetChild(i), name);
            if (f != null) return f;
        }
        return null;
    }

    /// <summary>子节点世界矩形底边，换算到 parent 本地 Y（从 parent 底边向上）。</summary>
    static float GetWorldRectBottomInParent(RectTransform child, RectTransform parent)
    {
        var corners = new Vector3[4];
        child.GetWorldCorners(corners);
        // 0=左下 1=左上
        Vector3 local = parent.InverseTransformPoint(corners[0]);
        return local.y - parent.rect.yMin;
    }

    static float GetWorldRectTopInParent(RectTransform child, RectTransform parent)
    {
        var corners = new Vector3[4];
        child.GetWorldCorners(corners);
        Vector3 local = parent.InverseTransformPoint(corners[1]);
        return local.y - parent.rect.yMin;
    }
}
