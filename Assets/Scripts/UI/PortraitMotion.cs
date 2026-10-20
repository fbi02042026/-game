using System.Collections;
using UnityEngine;

/// <summary>
/// 立绘点击动作队列：摸头轻浮（pat）/ 左右躲闪（shake）。
/// 【2026-10-20 主人拍板】从 TavernLandladyTease 抽成共用组件，主界面看板「前台小姐戳一戳」复用同一套动作。
/// 参数与时序照搬 TavernLandladyTease 原实现（2026-10-18 彩蛋批次），行为不许有差异。
/// 挂在立绘所在 GameObject 上，运行时 AddComponent 补挂（不动预制体）。
/// </summary>
[DisallowMultipleComponent]
public class PortraitMotion : MonoBehaviour
{
    // pat：上下轻浮两下，幅度 3.5、单次 0.18 秒
    const float PatAmp = 3.5f;
    const float PatDur = 0.18f;
    const int PatCount = 2;

    // shake：左右躲闪两下，幅度 6、单次 0.12 秒、两下之间 0.08 秒
    const float DodgeAmp = 6f;
    const float DodgeDur = 0.12f;
    const int DodgeCount = 2;
    const float DodgeGap = 0.08f;

    const float RoundGap = 0.1f;   // 每轮结束的停顿（同原实现）

    RectTransform _rt;
    PortraitIdleMotion _idle;
    Coroutine _motionCo;
    int _pendingShakes;
    int _pendingPats;

    /// <summary>运行时挂到立绘节点上（没有就补挂，有就复用）。</summary>
    public static PortraitMotion EnsureOn(RectTransform rt)
    {
        if (rt == null) return null;
        var motion = rt.GetComponent<PortraitMotion>();
        if (motion == null) motion = rt.gameObject.AddComponent<PortraitMotion>();
        motion.Setup();
        return motion;
    }

    void Awake() => Setup();

    void OnDisable()
    {
        // 页面关闭会停掉所有协程：句柄必须一并清掉，否则下次 Enable 后动画队列不再启动
        _motionCo = null;
        _pendingShakes = 0;
        _pendingPats = 0;
    }

    void Setup()
    {
        _rt = transform as RectTransform;
        _idle = GetComponent<PortraitIdleMotion>();
    }

    /// <summary>摸头：上下轻浮两下（与乱摸的左右躲闪区分）。</summary>
    public void RequestPat() => EnqueueMotion(0, 1);

    /// <summary>乱摸：左右躲闪两下。</summary>
    public void RequestShake() => EnqueueMotion(1, 0);

    /// <summary>长按：连排两轮躲闪（抖得更狠）。</summary>
    public void RequestBigShake() => EnqueueMotion(2, 0);

    void EnqueueMotion(int shakes, int pats)
    {
        if (_rt == null) _rt = transform as RectTransform;
        if (_idle == null) _idle = GetComponent<PortraitIdleMotion>();
        _pendingShakes += shakes;
        _pendingPats += pats;
        if (_motionCo == null) _motionCo = StartCoroutine(CoMotion());
    }

    // 哆嗦与摸头共用一条协程，避免并发互抢 anchoredPosition
    IEnumerator CoMotion()
    {
        while (_pendingShakes > 0 || _pendingPats > 0)
        {
            if (_idle != null) _idle.enabled = false;

            Vector2 basePos = _rt != null ? _rt.anchoredPosition : Vector2.zero;
            Vector3 baseEuler = transform.localEulerAngles;

            if (_pendingPats > 0)
            {
                _pendingPats = 0;
                // 摸头：上下轻浮两下，幅度小、节奏柔（与乱摸的左右躲闪区分）
                float dur = PatDur, amp = PatAmp;
                for (int i = 0; i < PatCount; i++)
                {
                    float t = 0f;
                    while (t < dur)
                    {
                        t += Time.unscaledDeltaTime;
                        if (_rt != null)
                            _rt.anchoredPosition = basePos + new Vector2(0f, amp * Mathf.Sin(t / dur * Mathf.PI));
                        yield return null;
                    }
                    if (_rt != null) _rt.anchoredPosition = basePos;
                }
            }

            while (_pendingShakes > 0)
            {
                _pendingShakes--;
                // 乱摸/长按：左右躲闪两下（原有行为原样保留）
                float dodgeAmp = DodgeAmp;
                float dodgeDur = DodgeDur;
                int dodges = DodgeCount;
                for (int d = 0; d < dodges; d++)
                {
                    float side = (d % 2 == 0) ? -1f : 1f;
                    float t = 0f;
                    while (t < dodgeDur)
                    {
                        t += Time.unscaledDeltaTime;
                        float u = t / dodgeDur;
                        float ease = Mathf.Sin(u * Mathf.PI);
                        if (_rt != null)
                            _rt.anchoredPosition = basePos + new Vector2(side * dodgeAmp * ease, 0f);
                        yield return null;
                    }
                    if (_rt != null) _rt.anchoredPosition = basePos;
                    if (d < dodges - 1)
                        yield return new WaitForSecondsRealtime(DodgeGap);
                }
                if (_pendingShakes > 0)
                    yield return new WaitForSecondsRealtime(RoundGap);
            }

            yield return new WaitForSecondsRealtime(RoundGap);
            if (_rt != null) _rt.anchoredPosition = basePos;
            transform.localEulerAngles = baseEuler;
            if (_idle != null) _idle.enabled = true;
        }

        _motionCo = null;
    }
}
