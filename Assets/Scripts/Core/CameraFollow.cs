using UnityEngine;

/// <summary>
/// 2D横版相机跟随脚本
/// 跟随目标X轴移动，Y轴固定，Z轴固定
/// 带平滑跟随和边界限制
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("跟随目标")]
    [Tooltip("跟随的目标（玩家），留空自动查找Hero.Instance")]
    public Transform target;

    [Header("跟随参数")]
    [Tooltip("跟随平滑度，0=瞬时跟随，越大越平滑")]
    public float smoothTime = 0.15f;

    [Header("偏移")]
    [Tooltip("相机相对于目标的偏移")]
    public Vector2 offset = new Vector2(0f, 0f);

    [Header("边界限制")]
    [Tooltip("最小X坐标（不限制设为-999）")]
    public float minX = -999f;
    [Tooltip("最大X坐标（不限制设为999）")]
    public float maxX = 999f;

    private Camera _cam;
    private float _velocityX = 0f;
    private float _camY;
    private float _camZ;
    private bool _yzInitialized = false;

    void Awake()
    {
        _cam = GetComponent<Camera>();
        // 在Awake中读取Y和Z，确保SetTarget在Start之前调用时也能正确工作
        _camY = transform.position.y;
        _camZ = transform.position.z;
        _yzInitialized = true;
    }

    void Start()
    {
        // 如果Awake没读到有效值（比如相机刚创建就在原点），这里再读一次
        if (!_yzInitialized || (_camY == 0f && _camZ == 0f))
        {
            _camY = transform.position.y;
            _camZ = transform.position.z;
            _yzInitialized = true;
        }

        if (target == null)
        {
            if (Hero.Instance != null)
                target = Hero.Instance.transform;
        }

        // 初始位置直接对齐目标（无平滑）
        if (target != null)
        {
            float startX = Mathf.Clamp(target.position.x + offset.x, minX, maxX);
            transform.position = new Vector3(startX, _camY, _camZ);
        }
    }

    void LateUpdate()
    {
        if (target == null)
        {
            if (Hero.Instance != null)
                target = Hero.Instance.transform;
            if (target == null) return;
        }

        // 目标X位置
        float targetX = target.position.x + offset.x;
        if (float.IsNaN(targetX) || float.IsInfinity(targetX))
        {
            // 目标被物理/缩放算坏了，这一帧不跟随，否则 NaN 会污染相机后再也回不来
            WarnInvalidOnce($"目标 {target.name} 的 X 为 {target.position.x}");
            return;
        }
        targetX = Mathf.Clamp(targetX, minX, maxX);

        // 平滑跟随（仅X轴）
        float currentX = transform.position.x;
        if (float.IsNaN(currentX) || float.IsInfinity(currentX))
        {
            currentX = targetX;
            _velocityX = 0f;
        }

        float newX = Mathf.SmoothDamp(currentX, targetX, ref _velocityX, Mathf.Max(0.0001f, smoothTime));
        if (float.IsNaN(newX) || float.IsInfinity(newX))
        {
            newX = targetX;
            _velocityX = 0f;
        }

        transform.position = new Vector3(newX, _camY, _camZ);
    }

    static bool _warnedInvalid;

    static void WarnInvalidOnce(string detail)
    {
        if (_warnedInvalid) return;
        _warnedInvalid = true;
        Debug.LogWarning($"[CameraFollow] 跟随目标坐标非法，已跳过跟随：{detail}");
    }

    /// <summary>从当前相机位置重新锁定 Y/Z（调整战斗带高度后调用）</summary>
    public void LockYZFromCurrent()
    {
        _camY = transform.position.y;
        _camZ = transform.position.z;
        _yzInitialized = true;
    }

    /// <summary>设置跟随目标</summary>
    public void SetTarget(Transform t)
    {
        target = t;

        // 确保Y和Z已初始化（从当前相机位置读取）
        if (!_yzInitialized)
        {
            _camY = transform.position.y;
            _camZ = transform.position.z;
            _yzInitialized = true;
        }

        if (t != null)
        {
            float startX = Mathf.Clamp(t.position.x + offset.x, minX, maxX);
            transform.position = new Vector3(startX, _camY, _camZ);
        }
    }
}
