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
    /// 全屏底图（BgStretch / Bg）：四边贴父级 + EnvelopeParent，等比铺满不拉伸变形。
    /// Toggle/Slider 等控件内的 Background 子节点一律跳过。
    /// </summary>
    public static void ApplyBgStretch(RectTransform rt, Image image = null)
    {
        if (rt == null || IsWidgetPart(rt)) return;
        if (!IsBgStretchArtName(rt.gameObject.name)) return;
        ApplyFillScreen(rt);
        if (image == null) image = rt.GetComponent<Image>();
        if (image == null) return;

        image.preserveAspect = true;
        var fitter = rt.GetComponent<AspectRatioFitter>();
        if (fitter == null) fitter = rt.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        var sp = image.sprite;
        if (sp != null)
            fitter.aspectRatio = sp.rect.width / Mathf.Max(1f, sp.rect.height);
        else
            fitter.aspectRatio = GameConfig.DESIGN_WIDTH / GameConfig.DESIGN_HEIGHT;
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

    /// <summary>在 battleUIRoot 下查找 map/Map/MapRoot 并横向拉满。</summary>
    public static void ApplyBattleMapWidth(Transform battleUIRoot)
    {
        if (battleUIRoot == null) return;
        for (int i = 0; i < battleUIRoot.childCount; i++)
        {
            var c = battleUIRoot.GetChild(i);
            if (!IsStretchHorizontalName(c.name)) continue;
            ApplyStretchHorizontal(c as RectTransform);
            return;
        }
    }
}
