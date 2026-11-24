using System.Collections;
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
    private float _baseOrthoSize;
    private float _zoomMul = 1f;
    Coroutine _zoomCo;

    // ===== 视野缩放 =====
    // 【2026-11-24 主人拍板·下线】原「队伍聚散动态拉远视野」已去掉：镜头只跟玩家，
    // 不再按佣兵距离调 ortho。_formationZoom 恒收敛到 1，仅保留击杀特写 _zoomMul 通路。
    const float FORMATION_SMOOTH = 0.35f;       // _formationZoom 收敛平滑时间
    float _formationZoom = 1f;                  // 恒 1（ApplyOrthoZoom 用它算 total，保留便于恢复）
    float _formationZoomVel;                    // Mathf.SmoothDamp ref 参数
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
        if (_cam != null)
            _baseOrthoSize = _cam.orthographicSize;
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
        // 【2026-11-12 主人报「一进来就在屏幕边走」实锤根因】入场序列在 Awake 里已把镜头对准
        // 开战位（SnapCameraToBattleView）并把队伍摆到屏外；这里再按 hero 吸附，会被 minX
        // (=spawn-0.5) 钳到 -2.69，把镜头抢走 → 随后的 ClampHeroInCamera 把队伍钳在屏缘。
        // 入场期间不抢镜头。
        var bm = BattleManager.Instance;
        bool introRunning = bm != null && bm.PartyIntroWalking;
        if (target != null && !introRunning)
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

    /// <summary>
    /// 【2026-11-24 主人拍板·下线】去掉队伍聚散缩放：镜头只跟玩家，不用管佣兵在不在镜头里。
    /// wantZoom 恒 1 → _formationZoom 平滑回到 1 → ortho / map 都恢复基准大小；
    /// 击杀特写 _zoomMul 通路（ApplyOrthoZoom）保留不动。
    /// <para>原逻辑（已删）：每帧按「玩家到最远存活我方佣兵的 X 距离」把 ortho 拉远，
    /// 让落后佣兵进画面；同倍率同步缩放 map 保持单位落点不动。主人决定不要此功能。</para>
    /// </summary>
    void UpdateFormationView()
    {
        const float wantZoom = 1f;
        _formationZoom = Mathf.SmoothDamp(_formationZoom, wantZoom, ref _formationZoomVel, FORMATION_SMOOTH);
        // map scale 跟随回到 1（恢复基准大小）；ortho 由 ApplyOrthoZoom 写回
        BattleEntryDraftPanel.SetFormationMapScale(_formationZoom);
        ApplyOrthoZoom();
    }

    void ApplyOrthoZoom()
    {
        if (_cam == null) return;
        // 【2026-11-24】基准改读适配真源：_baseOrthoSize 是 Awake 的一次性缓存，
        // ViewportFitDriver 分辨率变化重跑 BattleViewportFit.Apply 后会过期，
        // 每帧写回会把新适配值覆盖回旧值、打破瘦屏横向视野锁定。
        // ResolveOrthoSize 与判定真源（GameConfig.CombatJudgeOrthoSize）同源，画面/判定一致。
        float baseSize = _cam.orthographic
            ? BattleViewportFit.ResolveOrthoSize()
            : (_baseOrthoSize < 0.01f ? 1f : _baseOrthoSize);
        // 【2026-11-24 主人拍板·下线】原「队伍聚散拉远视野」已去掉，_formationZoom 恒 1，
        // total 实际就等于击杀特写 _zoomMul（<1 = 拉近特写）；map 同倍率同步缩放保持落点不变。
        float total = _zoomMul / Mathf.Max(0.01f, _formationZoom);
        _cam.orthographicSize = baseSize * total;
        BattleUI.ApplyKillCamHudCompensation(total);
    }

    /// <summary>击杀前摇：ortho 拉近（targetMul &lt; 1），unscaled 时长。</summary>
    public void BeginKillCamZoom(float targetMul, float inDurationUnscaled)
    {
        if (_cam == null) return;
        if (_baseOrthoSize < 0.01f)
            _baseOrthoSize = _cam.orthographicSize;
        if (_zoomCo != null)
            StopCoroutine(_zoomCo);
        _zoomCo = StartCoroutine(CoZoomTo(targetMul, inDurationUnscaled));
    }

    /// <summary>下劈/命中瞬间：快速还原 ortho。</summary>
    public void EndKillCamZoom(float outDurationUnscaled)
    {
        if (_cam == null) return;
        if (_zoomCo != null)
            StopCoroutine(_zoomCo);
        _zoomCo = StartCoroutine(CoZoomTo(1f, outDurationUnscaled));
    }

    /// <summary>战斗结束或异常：立即还原 zoom。</summary>
    public void ForceResetKillCamZoom()
    {
        if (_zoomCo != null)
        {
            StopCoroutine(_zoomCo);
            _zoomCo = null;
        }
        _zoomMul = 1f;
        ApplyOrthoZoom();
        Object.FindObjectOfType<ParallaxBackground>()?.ResetKillCamZoom();
        BattleUI.ResetKillCamHudCompensation();
    }

    IEnumerator CoZoomTo(float targetMul, float durationUnscaled)
    {
        float start = _zoomMul;
        if (durationUnscaled <= 0.0001f)
        {
            _zoomMul = targetMul;
            ApplyOrthoZoom();
            _zoomCo = null;
            yield break;
        }

        float t = 0f;
        while (t < durationUnscaled)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / durationUnscaled);
            _zoomMul = Mathf.Lerp(start, targetMul, u);
            ApplyOrthoZoom();   // HUD 反缩放已并入本方法，随倍率同步
            yield return null;
        }

        _zoomMul = targetMul;
        ApplyOrthoZoom();
        _zoomCo = null;
    }

    void LateUpdate()
    {
        UpdateShake();
        UpdateFormationView();   // _formationZoom 收敛回 1 + 写回 ortho/map（击杀特写通路），每帧先算

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
        // 跟随目标必须是主角，不能是 unit/WorldRoot（否则镜头锁共享根，或看起来像全场平移）
        if (target.GetComponent<Hero>() == null && Hero.Instance != null)
            target = Hero.Instance.transform;

        // 【2026-11-24 主人拍板】相机只跟主角：不做队伍平移，也不拉远视野（原聚散缩放已下线）。
        // 平移会让世界相对地图整体位移（看着就是单位在地面上滑），别再加回来。
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
        // 只移动本相机；禁止把 WorldRoot/unit 挂到相机下，否则跟随会拖走全部战斗单位。
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
        if (t != null && t.GetComponent<Hero>() == null && Hero.Instance != null)
            t = Hero.Instance.transform;
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
