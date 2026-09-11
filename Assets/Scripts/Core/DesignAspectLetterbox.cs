using UnityEngine;

/// <summary>
/// 设备极端宽高比时的纯黑 letterbox 兜底（非战斗主适配）。
/// 战斗以 BattleViewportFit 的 map 条带 + 相机对齐为准。
/// </summary>
public static class DesignAspectLetterbox
{
    /// <summary>相对设计 9:16，偏差超过该比例才启用黑边。</summary>
    public const float ExtremeAspectSlack = 0.12f;

    static Camera _barsCam;
    static int _lastW;
    static int _lastH;

    public static float DesignAspect =>
        GameConfig.DESIGN_WIDTH / Mathf.Max(1f, GameConfig.DESIGN_HEIGHT);

    public static bool IsExtremeAspect()
    {
        float screen = Screen.width / (float)Mathf.Max(1, Screen.height);
        float design = DesignAspect;
        float ratio = screen / design;
        return ratio < 1f - ExtremeAspectSlack || ratio > 1f + ExtremeAspectSlack;
    }

    /// <summary>
    /// 极端比例：主相机/UI 相机裁到 9:16 居中，背后黑底相机清屏。
    /// 非极端：恢复全屏 rect，关掉黑边相机。
    /// </summary>
    public static void ApplyFallbackIfNeeded(Camera primary = null)
    {
        if (primary == null)
            primary = Camera.main != null ? Camera.main : UICanvasSetup.ResolveUiCamera();

        if (!IsExtremeAspect())
        {
            RestoreFull(primary);
            var ui = UICanvasSetup.ResolveUiCamera();
            if (ui != null && ui != primary)
                RestoreFull(ui);
            SetBarsActive(false);
            return;
        }

        Rect letter = ComputeLetterboxRect();
        if (primary != null)
            primary.rect = letter;

        var uiCam = UICanvasSetup.ResolveUiCamera();
        if (uiCam != null && uiCam != primary)
            uiCam.rect = letter;

        EnsureBarsCamera();
        SetBarsActive(true);
        _lastW = Screen.width;
        _lastH = Screen.height;
    }

    public static void RefreshIfResolutionChanged()
    {
        if (Screen.width == _lastW && Screen.height == _lastH) return;
        ApplyFallbackIfNeeded();
    }

    public static Rect ComputeLetterboxRect()
    {
        float design = DesignAspect;
        float screen = Screen.width / (float)Mathf.Max(1, Screen.height);
        if (screen > design)
        {
            // 更宽：左右黑边
            float w = design / screen;
            float x = (1f - w) * 0.5f;
            return new Rect(x, 0f, w, 1f);
        }
        else
        {
            // 更高：上下黑边
            float h = screen / design;
            float y = (1f - h) * 0.5f;
            return new Rect(0f, y, 1f, h);
        }
    }

    static void RestoreFull(Camera cam)
    {
        if (cam == null) return;
        cam.rect = new Rect(0f, 0f, 1f, 1f);
    }

    static void EnsureBarsCamera()
    {
        if (_barsCam != null) return;
        var go = new GameObject("DesignAspectLetterboxCam");
        Object.DontDestroyOnLoad(go);
        _barsCam = go.AddComponent<Camera>();
        _barsCam.clearFlags = CameraClearFlags.SolidColor;
        _barsCam.backgroundColor = Color.black;
        _barsCam.cullingMask = 0;
        _barsCam.depth = -100f;
        _barsCam.orthographic = true;
        _barsCam.rect = new Rect(0f, 0f, 1f, 1f);
        _barsCam.enabled = false;
    }

    static void SetBarsActive(bool on)
    {
        if (_barsCam != null)
            _barsCam.enabled = on;
    }
}
