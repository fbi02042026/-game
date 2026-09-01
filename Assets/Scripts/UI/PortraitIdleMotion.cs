using UnityEngine;

/// <summary>
/// 立绘待机。默认：以底部为轴的呼吸缩放（不左右晃）。
/// 酒馆老板娘可开 sway：额外上下浮动 + 微倾。
/// </summary>
[DisallowMultipleComponent]
public class PortraitIdleMotion : MonoBehaviour
{
    [Header("呼吸（底部为轴）")]
    [Range(0f, 0.08f)] public float breathAmount = 0.016f;
    [Min(0.1f)] public float breathPeriod = 3.2f;
    [Range(0f, 1f)] public float breathPhase;

    [Header("浮动 / 微倾（仅酒馆老板娘）")]
    public bool tavernSway;
    [Min(0f)] public float bobPixels = 6f;
    [Min(0.1f)] public float bobPeriod = 2.6f;
    [Range(0f, 1f)] public float bobPhase = 0.35f;
    [Range(0f, 5f)] public float tiltDegrees = 0.8f;
    [Min(0.1f)] public float tiltPeriod = 4.5f;
    [Range(0f, 1f)] public float tiltPhase = 0.65f;

    RectTransform _rt;
    Vector3 _baseScale;
    Vector2 _baseAnchoredPos;
    Vector3 _baseLocalEuler;
    float _time;

    void Awake()
    {
        _rt = transform as RectTransform;
        CaptureBase();
    }

    void OnEnable()
    {
        if (_rt == null) _rt = transform as RectTransform;
        CaptureBase();
        _time = 0f;
        ApplyMotion(0f);
    }

    void OnDisable()
    {
        RestoreBase();
    }

    void Update()
    {
        _time += Time.unscaledDeltaTime;
        ApplyMotion(_time);
    }

    /// <summary>运行时挂到立绘节点。tavernSway=true 才左右/上下晃。breathAmount&lt;0 用默认。</summary>
    public static PortraitIdleMotion EnsureOn(
        RectTransform rt,
        float phaseSeed = 0f,
        bool tavernSway = false,
        float breathAmount = -1f)
    {
        if (rt == null) return null;
        var motion = rt.GetComponent<PortraitIdleMotion>();
        if (motion == null) motion = rt.gameObject.AddComponent<PortraitIdleMotion>();
        motion.tavernSway = tavernSway;
        if (!tavernSway)
        {
            motion.bobPixels = 0f;
            motion.tiltDegrees = 0f;
            motion.breathAmount = breathAmount >= 0f ? breathAmount : 0.016f;
        }
        else
        {
            motion.bobPixels = 6f;
            motion.tiltDegrees = 0.8f;
            motion.breathAmount = breathAmount >= 0f ? breathAmount : 0.012f;
        }
        ApplyPhaseSeed(motion, phaseSeed);
        motion.enabled = true;
        motion.RefreshBase();
        return motion;
    }

    static void ApplyPhaseSeed(PortraitIdleMotion motion, float phaseSeed)
    {
        float s = Mathf.Repeat(phaseSeed, 1f);
        motion.breathPhase = s;
        motion.bobPhase = Mathf.Repeat(s + 0.35f, 1f);
        motion.tiltPhase = Mathf.Repeat(s + 0.65f, 1f);
    }

    /// <summary>布局或朝向改完后重采基准，避免与翻转 scale 打架。</summary>
    public void RefreshBase()
    {
        if (_rt == null) _rt = transform as RectTransform;
        CaptureBase();
        ApplyMotion(_time);
    }

    void CaptureBase()
    {
        _baseScale = transform.localScale;
        _baseLocalEuler = transform.localEulerAngles;
        _baseAnchoredPos = _rt != null ? _rt.anchoredPosition : Vector2.zero;
    }

    void RestoreBase()
    {
        transform.localScale = _baseScale;
        transform.localEulerAngles = _baseLocalEuler;
        if (_rt != null)
            _rt.anchoredPosition = _baseAnchoredPos;
    }

    void ApplyMotion(float time)
    {
        float breath = breathAmount > 0f && breathPeriod > 0f
            ? 1f + breathAmount * Mathf.Sin(TwoPi(time, breathPeriod, breathPhase))
            : 1f;
        transform.localScale = _baseScale * breath;

        Vector2 pos = _baseAnchoredPos;
        // pivot 在底部时 Unity 已以底为轴缩放；仅非底 pivot 时补位移
        if (_rt != null && _rt.pivot.y > 0.05f)
        {
            float h = Mathf.Abs(_rt.rect.height);
            float extra = h * (breath - 1f) * Mathf.Abs(_baseScale.y);
            pos.y += extra * _rt.pivot.y;
        }

        if (tavernSway && _rt != null && bobPixels > 0f && bobPeriod > 0f)
            pos.y += bobPixels * Mathf.Sin(TwoPi(time, bobPeriod, bobPhase));

        if (_rt != null)
            _rt.anchoredPosition = pos;

        if (tavernSway && tiltDegrees > 0f && tiltPeriod > 0f)
        {
            float tilt = tiltDegrees * Mathf.Sin(TwoPi(time, tiltPeriod, tiltPhase));
            transform.localEulerAngles = _baseLocalEuler + new Vector3(0f, 0f, tilt);
        }
        else
            transform.localEulerAngles = _baseLocalEuler;
    }

    static float TwoPi(float time, float period, float phase01)
    {
        return (time / period + phase01) * Mathf.PI * 2f;
    }
}
