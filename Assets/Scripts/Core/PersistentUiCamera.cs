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
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = Color.black;
        _cam.cullingMask = 0;
        _cam.depth = CameraDepth;
        _cam.nearClipPlane = 0.1f;
        _cam.farClipPlane = 200f;
        _cam.useOcclusionCulling = false;
        _cam.allowHDR = false;
        _cam.allowMSAA = false;
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}
