using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 酒馆佣兵队列卡内「升阶」标志：可升星时缓慢呼吸缩放 + 流光材质。
/// 2026-11-08 需求：给这个标志加缩放效果和流光。
/// 【17:4x 口径纠正】红点不挂在这里 —— 红点只挂入口（酒馆页「佣兵队列」图标、
/// 主界面底部导航「酒馆」按钮），由 MercStarRedDot 统一驱动，本类只管标志本身的动效。
///
/// 运行时挂到克隆卡的「升阶」节点上（预制体不动）；状态唯一入口 = SetState(bool)，
/// bool 由 MercDrawPoolUI.CanActuallyAscend 给出，本类不自己判「能不能升星」。
/// 流光材质 = Resources/Materials/btn18进阶（与「获得佣兵」弹窗进阶按钮同族同款）；
/// 缩放只动节点自身 localScale，在预制体 authored 基准上呼吸，关闭时原样还原（不碰 sizeDelta / 位置）。
/// </summary>
public class MercAscendBadgeFx : MonoBehaviour
{
    const string GlowMatPath = "Materials/btn18进阶";
    /// <summary>与 MercObtainPopupUI 进阶按钮同一套呼吸参数：幅度 6%、周期 1.6 秒。</summary>
    const float PulseAmplitude = 0.06f;
    const float PulsePeriod = 1.6f;

    static Material _glowMat;
    static bool _glowMissingLogged;

    Image _img;
    RectTransform _rt;
    Vector3 _baseScale;
    Coroutine _co;
    bool _on;
    bool _initialized;

    void Awake() => Init();

    void Init()
    {
        if (_initialized) return;
        _initialized = true;
        _img = GetComponent<Image>();
        _rt = transform as RectTransform;
        if (_rt != null) _baseScale = _rt.localScale;
    }

    /// <summary>刷新时由列表卡调用：true = 该佣兵可升星（呼吸 + 流光），false = 全部还原。</summary>
    public void SetState(bool on)
    {
        Init();
        if (_img == null || _rt == null) return;
        if (on == _on) return;
        _on = on;

        if (on)
        {
            if (_glowMat == null && !_glowMissingLogged)
            {
                _glowMat = Resources.Load<Material>(GlowMatPath);
                if (_glowMat == null)
                {
                    _glowMissingLogged = true;
                    Debug.LogError("[MercAscendBadgeFx] 升阶流光材质缺失：Resources/" + GlowMatPath + "（不兜底）");
                }
            }
            if (_glowMat != null) _img.material = _glowMat;
            if (_co == null) _co = StartCoroutine(CoPulse());
        }
        else
        {
            if (_co != null) { StopCoroutine(_co); _co = null; }
            _rt.localScale = _baseScale;
            _img.material = null;   // 与 MercObtainPopupUI.ApplyAscendGlow(false) 同口径：回 UI 内置默认材质
        }
    }

    /// <summary>卡被回收 / 升阶节点隐藏时：停协程并把缩放、材质还原到预制体原值，防串味。</summary>
    void OnDisable()
    {
        if (_co != null) { StopCoroutine(_co); _co = null; }
        if (_rt != null) _rt.localScale = _baseScale;
        if (_img != null) _img.material = null;
        _on = false;
    }

    /// <summary>缓慢呼吸：authored 缩放 ×（1.0 → 1.06 → 1.0）正弦往复，unscaled 时间。</summary>
    IEnumerator CoPulse()
    {
        while (true)
        {
            float t = (Time.unscaledTime % PulsePeriod) / PulsePeriod;
            float k = 1f + PulseAmplitude * (0.5f - 0.5f * Mathf.Cos(t * Mathf.PI * 2f));
            _rt.localScale = _baseScale * k;
            yield return null;
        }
    }
}
