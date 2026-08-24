using UnityEngine;

/// <summary>
/// 传送门动画组件
/// 挂在EndPoint（传送门）上，让传送门有旋转/脉动效果
/// 正式通关请走 chuansongmen（StageClearRewardDirector）；本组件也可挂在旧 EndPoint 上作视觉。
/// </summary>
public class PortalAnimator : MonoBehaviour
{
    [Header("动画参数")]
    public float rotateSpeed = 60f;
    public float pulseSpeed = 2f;
    public float pulseScale = 1.2f;

    Transform _ring;
    Vector3 _baseScale;
    float _time;
    bool _warmed;

    void Awake()
    {
        Warm();
    }

    /// <summary>预缓存子节点/缩放，避免首次激活时扫层级</summary>
    public void Warm()
    {
        if (_warmed && _baseScale.sqrMagnitude > 1e-8f) return;
        _baseScale = transform.localScale;
        if (_baseScale.sqrMagnitude < 1e-8f) _baseScale = Vector3.one;
        _ring = transform.childCount > 0 ? transform.GetChild(0) : null;
        _warmed = true;
    }

    void OnEnable()
    {
        Warm();
        _time = 0f;
        transform.localScale = _baseScale;
    }

    void Update()
    {
        _time += Time.deltaTime;

        if (_ring != null && _ring != transform)
            _ring.Rotate(0, 0, rotateSpeed * Time.deltaTime);
        else
            transform.Rotate(0, 0, rotateSpeed * 0.3f * Time.deltaTime);

        float pulse = 1f + (Mathf.Sin(_time * pulseSpeed) * 0.5f + 0.5f) * (pulseScale - 1f);
        transform.localScale = _baseScale * pulse;
    }
}
