using UnityEngine;

/// <summary>
/// 跨场景持久 UI 相机：切场景 Loading、Boot 极早阶段无 Camera.main 时兜底。
/// 不打 MainCamera 标签，避免与场景相机冲突。
/// </summary>
public class PersistentUiCamera : MonoBehaviour
{
    public const float CameraDepth = 100f;

    static PersistentUiCamera _instance;

    public static Camera Camera
    {
        get
        {
            Ensure();
            return _instance != null ? _instance._cam : null;
        }
    }

    Camera _cam;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        Ensure();
    }

    public static void Ensure()
    {
        if (_instance != null) return;
        var go = new GameObject("PersistentUiCamera");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<PersistentUiCamera>();
        _instance.Setup();
    }

    void Setup()
    {
        _cam = gameObject.GetComponent<Camera>();
        if (_cam == null)
            _cam = gameObject.AddComponent<Camera>();
        _cam.orthographic = true;
        _cam.orthographicSize = GameConfig.CAMERA_ORTHO_SIZE;
        // 仅兜底渲染绑定到本相机的 UI，不能 SolidColor 清屏（会把 Main 上的登录/忠告盖成黑屏）
        _cam.clearFlags = CameraClearFlags.Depth;
        _cam.backgroundColor = Color.black;
        _cam.cullingMask = 0;
        _cam.depth = CameraDepth;
        _cam.nearClipPlane = 0.1f;
        _cam.farClipPlane = 200f;
        _cam.useOcclusionCulling = false;
        _cam.allowHDR = false;
        _cam.allowMSAA = false;
        SyncEnabledState();
    }

    void LateUpdate()
    {
        SyncEnabledState();
    }

    /// <summary>有 Main 相机时关闭兜底相机，避免多余 Pass 或盖住场景 UI。</summary>
    void SyncEnabledState()
    {
        if (_cam == null) return;
        var main = Camera.main;
        bool needFallback = main == null || !main.isActiveAndEnabled;
        if (_cam.enabled != needFallback)
        {
            _cam.enabled = needFallback;
            if (needFallback)
                UICanvasSetup.RefreshDdolCanvases();
        }
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}
