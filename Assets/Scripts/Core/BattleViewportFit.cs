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

        if (cam == null) cam = UICanvasSetup.ResolveUiCamera();
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

        FitBattleMapWidth(rootCanvas.transform);
    }

    /// <summary>map 战斗条带：左右拉满父级，高度保持美术设定</summary>
    public static void FitBattleMapWidth(Transform battleUIRoot) =>
        UiLayoutStretch.ApplyBattleMapWidth(battleUIRoot);
}
