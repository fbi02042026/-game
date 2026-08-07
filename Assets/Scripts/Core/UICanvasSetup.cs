using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 全项目 UI Canvas 统一规范：
/// Screen Space - Camera、720×1280 竖版、Match Height。
/// Town / Battle 及以后所有界面都走这里，避免各处手写不一致。
/// </summary>
public static class UICanvasSetup
{
    /// <summary>
    /// 将 Canvas 套用统一规范并绑定摄像机。
    /// </summary>
    public static void Apply(Canvas canvas, Camera cam = null)
    {
        if (canvas == null) return;

        if (cam == null) cam = Camera.main;

        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = GameConfig.UI_PLANE_DISTANCE;

        if (canvas.transform.localScale == Vector3.zero)
            canvas.transform.localScale = Vector3.one;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.referenceResolution = new Vector2(GameConfig.DESIGN_WIDTH, GameConfig.DESIGN_HEIGHT);
        // Match Width：竖屏宽度铺满，禁止 Expand letterbox
        scaler.matchWidthOrHeight = 0f;

        if (canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();
    }

    /// <summary>
    /// 查找根上的 Canvas（自身或父级），套用规范。
    /// </summary>
    public static Canvas ApplyOn(GameObject root, Camera cam = null)
    {
        if (root == null) return null;
        Canvas canvas = root.GetComponent<Canvas>();
        if (canvas == null)
            canvas = root.GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = root.GetComponentInChildren<Canvas>(true);
        Apply(canvas, cam);
        return canvas;
    }
}
