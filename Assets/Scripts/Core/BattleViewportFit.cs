using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 竖屏适配：宽度 Match Width 铺满；不改相机 ortho（站位由用户调好的 BattleUI/unit 决定）。
/// 战斗条带（map）横向拉满，随分辨率一起变宽。
/// </summary>
public static class BattleViewportFit
{
    public static float DesignAspect => GameConfig.DESIGN_WIDTH / GameConfig.DESIGN_HEIGHT;

    public static float CurrentScreenAspect =>
        Screen.width / (float)Mathf.Max(1, Screen.height);

    /// <summary>保持用户设定的正交尺寸，避免人相对草地忽上忽下</summary>
    public static float ResolveOrthoSize() => GameConfig.CAMERA_ORTHO_SIZE;

    public static void Apply(Camera cam, Canvas rootCanvas = null)
    {
        if (cam != null && cam.orthographic)
            cam.orthographicSize = ResolveOrthoSize();

        if (rootCanvas == null)
        {
            var ui = Object.FindObjectOfType<BattleUI>();
            if (ui != null)
                rootCanvas = ui.GetComponentInParent<Canvas>() ?? ui.GetComponent<Canvas>();
        }
        if (rootCanvas == null) return;

        if (cam == null) cam = Camera.main;
        rootCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        rootCanvas.worldCamera = cam;
        rootCanvas.planeDistance = GameConfig.UI_PLANE_DISTANCE;

        var rootRt = rootCanvas.transform as RectTransform;
        if (rootRt != null)
        {
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            if (rootRt.localScale == Vector3.zero)
                rootRt.localScale = Vector3.one;
        }

        CanvasScaler scaler = rootCanvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = rootCanvas.gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.referenceResolution = new Vector2(GameConfig.DESIGN_WIDTH, GameConfig.DESIGN_HEIGHT);
        scaler.matchWidthOrHeight = 0f; // Match Width

        if (rootCanvas.GetComponent<GraphicRaycaster>() == null)
            rootCanvas.gameObject.AddComponent<GraphicRaycaster>();

        StretchFullRect(FindChildNamed(rootCanvas.transform, "Background"));
        FitBattleMapWidth(rootCanvas.transform);
    }

    static Transform FindChildNamed(Transform root, string name)
    {
        if (root == null) return null;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform c = root.GetChild(i);
            if (c.name.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return c;
        }
        return null;
    }

    static void StretchFullRect(Transform t)
    {
        var rt = t as RectTransform;
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    /// <summary>map 战斗条带：左右拉满父级，高度保持美术设定</summary>
    public static void FitBattleMapWidth(Transform battleUIRoot)
    {
        if (battleUIRoot == null) return;
        Transform map = null;
        for (int i = 0; i < battleUIRoot.childCount; i++)
        {
            Transform c = battleUIRoot.GetChild(i);
            string n = c.name;
            if (n.Equals("map", System.StringComparison.OrdinalIgnoreCase)
                || n.Equals("Map", System.StringComparison.OrdinalIgnoreCase)
                || n.Equals("Maproot", System.StringComparison.OrdinalIgnoreCase))
            {
                map = c;
                break;
            }
        }
        if (map == null) return;
        var rt = map as RectTransform;
        if (rt == null) return;

        float yMin = rt.anchorMin.y;
        float yMax = rt.anchorMax.y;
        float oMinY = rt.offsetMin.y;
        float oMaxY = rt.offsetMax.y;
        Vector2 pivot = rt.pivot;
        float posY = rt.anchoredPosition.y;
        float sizeY = rt.sizeDelta.y;

        // 若原本是中心锚点固定宽，改为左右拉伸，高度不变
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
}
