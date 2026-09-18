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

    /// <summary>
    /// 锁定横向视野：横向半宽恒等于设计比例下的值，更瘦的屏幕纵向自然铺开，
    /// 避免战斗队伍被挤出画面。不比设计更瘦的机型保持用户设定的正交尺寸。
    /// </summary>
    public static float ResolveOrthoSize()
    {
        float design = DesignAspect;
        float screen = CurrentScreenAspect;
        if (screen <= 0f || design <= 0f) return GameConfig.CAMERA_ORTHO_SIZE;
        if (screen >= design) return GameConfig.CAMERA_ORTHO_SIZE;
        return GameConfig.CAMERA_ORTHO_SIZE * design / screen;
    }

    public static void Apply(Camera cam, Canvas rootCanvas = null)
    {
        if (cam == null)
            cam = Camera.main != null ? Camera.main : UICanvasSetup.ResolveUiCamera();

        float orthoSize = GameConfig.CAMERA_ORTHO_SIZE;
        if (cam != null && cam.orthographic)
        {
            cam.orthographicSize = ResolveOrthoSize();
            orthoSize = cam.orthographicSize;
        }

        // 任务4：屏幕更瘦时相机视野变高，按 orthoSize/基础尺寸等比放大视差层铺满
        var parallax = Object.FindObjectOfType<ParallaxBackground>(true);
        if (parallax != null)
            parallax.ApplyViewportZoom(orthoSize / GameConfig.CAMERA_ORTHO_SIZE);

        // 确保世界/UI 共用主相机全屏，清掉上次错误裁切
        if (cam != null)
            cam.rect = new Rect(0f, 0f, 1f, 1f);

        // 极端比例纯黑 letterbox 兜底（必须在 rect 复位之后，避免被覆盖）
        DesignAspectLetterbox.ApplyFallbackIfNeeded(cam);

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
        scaler.matchWidthOrHeight = DesignAspectLetterbox.ResolveUiMatch();

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
        FitBattleBackground(rootCanvas.transform);
    }

    /// <summary>map 战斗条带：顶栏与背包之间自适应</summary>
    public static void FitBattleMapWidth(Transform battleUIRoot) =>
        UiLayoutStretch.ApplyBattleMapWidth(battleUIRoot);

    /// <summary>战斗 UI 全屏底图（Background）envelope 铺满，不受 map 顶栏/背包限制。</summary>
    public static void FitBattleBackground(Transform battleUIRoot)
    {
        if (battleUIRoot == null) return;
        Transform bg = battleUIRoot.Find("Background") ?? FindChild(battleUIRoot, "Background");
        if (bg == null) return;
        var rt = bg as RectTransform;
        if (rt == null) return;
        UiLayoutStretch.ApplyEnvelopeImage(rt);
    }

    static Transform FindChild(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name)) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var f = FindChild(root.GetChild(i), name);
            if (f != null) return f;
        }
        return null;
    }

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
