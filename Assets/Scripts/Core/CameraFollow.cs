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
    /// <summary> false 时 LateUpdate 不跟 X（开战走进场等固定镜头过场） </summary>
    bool _followXEnabled = true;

    public bool FollowXEnabled => _followXEnabled;

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

    Vector2 _shakeOffset;
    float _shakeTimeLeft;
    float _shakeAmp;
    Vector2 _lastShakeOffset;

    void UpdateShake()
    {
        if (_shakeTimeLeft <= 0f)
        {
            _shakeOffset = Vector2.zero;
            _shakeAmp = 0f;
            return;
        }

        _shakeTimeLeft -= Time.unscaledDeltaTime;
        float decay = Mathf.Clamp01(_shakeTimeLeft / 0.15f);
        _shakeOffset = Random.insideUnitCircle * (_shakeAmp * decay);
        if (_shakeTimeLeft <= 0f)
        {
            _shakeOffset = Vector2.zero;
            _shakeAmp = 0f;
        }
    }

    /// <summary>世界单位 XY 微震，unscaled 衰减。</summary>
    public void AddShake(float amplitude, float duration)
    {
        if (amplitude <= 0f || duration <= 0f) return;
        _shakeAmp = Mathf.Max(_shakeAmp, amplitude);
        _shakeTimeLeft = Mathf.Max(_shakeTimeLeft, duration);
    }

    void LateUpdate()
    {
        UpdateShake();

        Vector3 pos = transform.position;
        pos.x -= _lastShakeOffset.x;
        pos.y -= _lastShakeOffset.y;

        if (!_followXEnabled)
        {
            transform.position = new Vector3(pos.x + _shakeOffset.x, pos.y + _shakeOffset.y, pos.z);
            _lastShakeOffset = _shakeOffset;
            return;
        }

        if (target == null)
        {
            if (Hero.Instance != null)
                target = Hero.Instance.transform;
            if (target == null) return;
        }

        float targetX = target.position.x + offset.x;
        if (float.IsNaN(targetX) || float.IsInfinity(targetX))
        {
            WarnInvalidOnce($"目标 {target.name} 的 X 为 {target.position.x}");
            return;
        }
        targetX = Mathf.Clamp(targetX, minX, maxX);

        float currentX = pos.x;
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

        transform.position = new Vector3(newX + _shakeOffset.x, _camY + _shakeOffset.y, _camZ);
        _lastShakeOffset = _shakeOffset;
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

    /// <summary>走进场等：固定镜头，避免与 SnapCamera 抢 X</summary>
    public void PauseFollowX()
    {
        _followXEnabled = false;
        _velocityX = 0f;
    }

    /// <summary>过场结束：对齐当前目标并恢复跟随</summary>
    public void ResumeFollowX(Transform t = null)
    {
        if (t != null) target = t;

        if (!_yzInitialized)
        {
            _camY = transform.position.y;
            _camZ = transform.position.z;
            _yzInitialized = true;
        }

        _velocityX = 0f;
        _followXEnabled = true;

        if (target != null)
        {
            float startX = Mathf.Clamp(target.position.x + offset.x, minX, maxX);
            transform.position = new Vector3(startX, _camY, _camZ);
        }
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
