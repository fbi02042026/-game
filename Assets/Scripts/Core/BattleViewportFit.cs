using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗适配：框体整页 Canvas 等比；map 在顶栏与背包间纵向自适应铺满。
/// 与 BattleUI 共用 Camera.main，不裁 camera.rect（双相机会导致黑屏）。
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
        // 极端比例纯黑 letterbox 兜底；正常机型不启用
        DesignAspectLetterbox.ApplyFallbackIfNeeded(cam);

        if (cam == null)
            cam = Camera.main != null ? Camera.main : UICanvasSetup.ResolveUiCamera();

        if (cam != null && cam.orthographic)
            cam.orthographicSize = ResolveOrthoSize();

        // 确保世界/UI 共用主相机全屏，清掉上次错误裁切
        if (cam != null)
            cam.rect = new Rect(0f, 0f, 1f, 1f);

        if (rootCanvas == null)
        {
            var ui = Object.FindObjectOfType<BattleUI>();
            if (ui != null)
                rootCanvas = ui.GetComponentInParent<Canvas>() ?? ui.GetComponent<Canvas>();
        }
        if (rootCanvas == null) return;

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
        scaler.matchWidthOrHeight = GameConfig.UI_MATCH;

        if (rootCanvas.GetComponent<GraphicRaycaster>() == null)
            rootCanvas.gameObject.AddComponent<GraphicRaycaster>();

        // 条带外若仍露出，用暗色清屏（非天蓝）避免「丢背景」感
        if (cam != null)
        {
            if (cam.clearFlags == CameraClearFlags.Skybox)
                cam.clearFlags = CameraClearFlags.SolidColor;
            if (cam.clearFlags == CameraClearFlags.SolidColor)
            {
                var c = cam.backgroundColor;
                if (c.r > 0.4f && c.b > 0.4f)
                    cam.backgroundColor = new Color(0.08f, 0.08f, 0.1f, 1f);
            }
        }

        DisableLegacySplitCameras();

        Canvas.ForceUpdateCanvases();
        FitBattleMapWidth(rootCanvas.transform);
    }

    /// <summary>map 战斗条带：顶栏与背包之间自适应</summary>
    public static void FitBattleMapWidth(Transform battleUIRoot) =>
        UiLayoutStretch.ApplyBattleMapWidth(battleUIRoot);

    /// <summary>离开战斗或切场景时恢复世界相机全屏，并关掉错误拆分相机。</summary>
    public static void RestoreWorldCameraFull(Camera worldCam = null)
    {
        if (worldCam == null) worldCam = Camera.main;
        if (worldCam != null)
            worldCam.rect = new Rect(0f, 0f, 1f, 1f);
        DisableLegacySplitCameras();
    }

    /// <summary>关掉此前双相机方案残留的 DDOL 相机，避免继续盖黑屏。</summary>
    static void DisableLegacySplitCameras()
    {
        var ui = GameObject.Find("BattleUiCamera");
        if (ui != null)
        {
            var c = ui.GetComponent<Camera>();
            if (c != null) c.enabled = false;
            Object.Destroy(ui);
        }
        var band = GameObject.Find("BattleMapBandClearCam");
        if (band != null)
        {
            var c = band.GetComponent<Camera>();
            if (c != null) c.enabled = false;
            Object.Destroy(band);
        }
    }
}
