using UnityEngine;

/// <summary>
/// 立绘轻量待机：呼吸缩放 + 上下浮动 + 微倾。叠加在 Awake 基准 transform 之上。
/// </summary>
[DisallowMultipleComponent]
public class PortraitIdleMotion : MonoBehaviour
{
    [Header("呼吸")]
    [Range(0f, 0.05f)] public float breathAmount = 0.012f;
    [Min(0.1f)] public float breathPeriod = 3.2f;
    [Range(0f, 1f)] public float breathPhase;

    [Header("浮动")]
    [Min(0f)] public float bobPixels = 6f;
    [Min(0.1f)] public float bobPeriod = 2.6f;
    [Range(0f, 1f)] public float bobPhase = 0.35f;

    [Header("微倾")]
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

        if (_rt != null && bobPixels > 0f && bobPeriod > 0f)
        {
            float bob = bobPixels * Mathf.Sin(TwoPi(time, bobPeriod, bobPhase));
            _rt.anchoredPosition = _baseAnchoredPos + new Vector2(0f, bob);
        }

        if (tiltDegrees > 0f && tiltPeriod > 0f)
        {
            float tilt = tiltDegrees * Mathf.Sin(TwoPi(time, tiltPeriod, tiltPhase));
            transform.localEulerAngles = _baseLocalEuler + new Vector3(0f, 0f, tilt);
        }
    }

    static float TwoPi(float time, float period, float phase01)
    {
        return (time / period + phase01) * Mathf.PI * 2f;
    }
}
