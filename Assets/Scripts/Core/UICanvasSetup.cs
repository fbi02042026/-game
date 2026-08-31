using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 全项目 UI Canvas 统一规范：
/// Screen Space - Camera、720×1280 竖版、Match Height。
/// Town / Battle 及以后所有界面都走这里，避免各处手写不一致。
/// </summary>
public static class UICanvasSetup
{
    static bool _sceneHooked;

    /// <summary>
    /// 将 Canvas 套用统一规范并绑定摄像机。
    /// </summary>
    public static void Apply(Canvas canvas, Camera cam = null)
    {
        if (canvas == null) return;
        EnsureSceneHook();

        if (cam == null) cam = ResolveUiCamera();

        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = GameConfig.UI_PLANE_DISTANCE;

        EnsureRootStretch(canvas.transform as RectTransform);

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.referenceResolution = new Vector2(GameConfig.DESIGN_WIDTH, GameConfig.DESIGN_HEIGHT);
        scaler.matchWidthOrHeight = GameConfig.UI_MATCH;

        if (canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();

#if UNITY_EDITOR
        if (cam == null)
            Debug.LogError("[UICanvasSetup] worldCamera 为空，UI 可能缩到一角：" + canvas.name, canvas);
#endif
    }

    /// <summary>弹窗/浮层统一入口：Camera 模式 + sortOrder + 根铺满。</summary>
    public static void ApplyPopup(Canvas canvas, int sortOrder, Camera cam = null)
    {
        if (canvas == null) return;
        Apply(canvas, cam);
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortOrder;
    }

    /// <summary>DDOL 弹窗每次 Show/Open 时调用：重绑相机 + 根铺满（无全场景扫描）。</summary>
    public static void RefreshPopup(Canvas canvas, int sortOrder)
    {
        ApplyPopup(canvas, sortOrder, ResolveUiCamera());
    }

    /// <summary>
    /// 城镇/战斗 UI 摄像机：优先场景 Main，其次大厅 Canvas，最后持久 UI 相机。
    /// </summary>
    public static Camera ResolveUiCamera()
    {
        var main = Camera.main;
        if (main != null && main.isActiveAndEnabled)
            return main;

        if (GuildHallUI.Instance != null)
        {
            var hallCanvas = GuildHallUI.Instance.GetComponentInParent<Canvas>();
            if (hallCanvas != null && hallCanvas.worldCamera != null
                && hallCanvas.worldCamera.isActiveAndEnabled)
                return hallCanvas.worldCamera;
        }

        return PersistentUiCamera.Camera;
    }

    /// <summary>Screen Space Camera 根节点必须铺满，否则 DDOL 弹窗会缩到左下角。</summary>
    public static void EnsureRootStretch(RectTransform rt)
    {
        if (rt == null) return;
        if (rt.localScale == Vector3.zero)
            rt.localScale = Vector3.one;
        rt.localPosition = Vector3.zero;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    /// <summary>场景切换后重绑 DDOL Canvas 的 worldCamera。</summary>
    public static void RefreshDdolCanvases()
    {
        var cam = ResolveUiCamera();
        if (cam == null) return;

        var canvases = Object.FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            var c = canvases[i];
            if (c == null || c.renderMode != RenderMode.ScreenSpaceCamera) continue;
            if (!IsDdol(c.gameObject)) continue;
            c.worldCamera = cam;
            EnsureRootStretch(c.transform as RectTransform);
        }
    }

    static bool IsDdol(GameObject go)
    {
        if (go == null || go.scene.name == null) return false;
        return go.scene.name == "DontDestroyOnLoad";
    }

    static void EnsureSceneHook()
    {
        if (_sceneHooked) return;
        _sceneHooked = true;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshDdolCanvases();
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
