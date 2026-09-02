using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Resources UI Banner 标准动效（波次预告、同类切图弹图）：
/// · 终态：preserveAspect + SetNativeSize = 100% native，localScale 恒为 1
/// · 入场：sizeDelta 从 native×300% 在 0.3s 缩到 native（100%）
/// · 停 1s → CanvasGroup 淡出
/// · 波次来袭专用：<see cref="CoPlayWaveIncoming"/>（更大入场、落地颤、再淡出）
/// </summary>
public static class UiBannerPopAnim
{
    public const float StartSizeRatio = 3f;
    public const float ShrinkDuration = 0.3f;
    public const float HoldDuration = 1f;
    public const float FadeOutDuration = 0.32f;

    public static float TotalDuration => ShrinkDuration + HoldDuration + FadeOutDuration;

    public const float WaveStartSizeRatio = 4.2f;
    public const float WaveSlamDuration = 0.26f;
    public const float WaveLandShakeDuration = 0.14f;
    public const float WaveHoldDuration = 0.52f;
    public const float WaveFadeOutDuration = 0.34f;

    public static float WaveIncomingTotalDuration =>
        WaveSlamDuration + WaveLandShakeDuration + WaveHoldDuration + WaveFadeOutDuration;

    /// <summary>播放标准 Banner 动效（调用前需已设好 sprite 并 enabled）</summary>
    public static IEnumerator CoPlay(Image image, CanvasGroup group)
    {
        if (image == null || group == null)
            yield break;

        RectTransform rt = image.rectTransform;
        image.preserveAspect = true;
        image.SetNativeSize();
        rt.localScale = Vector3.one;

        Vector2 nativeSize = rt.sizeDelta;
        Vector2 startSize = nativeSize * StartSizeRatio;
        rt.sizeDelta = startSize;
        group.alpha = 1f;

        float t = 0f;
        while (t < ShrinkDuration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / ShrinkDuration);
            float easeOut = 1f - (1f - u) * (1f - u) * (1f - u);
            rt.sizeDelta = Vector2.Lerp(startSize, nativeSize, easeOut);
            yield return null;
        }

        rt.sizeDelta = nativeSize;
        rt.localScale = Vector3.one;
        group.alpha = 1f;

        t = 0f;
        while (t < HoldDuration)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        t = 0f;
        while (t < FadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / FadeOutDuration);
            group.alpha = 1f - u;
            yield return null;
        }

        group.alpha = 0f;
    }

    /// <summary>波次/Boss 来袭：大幅砸入 → 缩到 100% 时颤一下 → 停驻 → 渐隐。</summary>
    public static IEnumerator CoPlayWaveIncoming(Image image, CanvasGroup group)
    {
        if (image == null || group == null)
            yield break;

        RectTransform rt = image.rectTransform;
        image.preserveAspect = true;
        image.SetNativeSize();
        rt.localScale = Vector3.one;

        Vector2 nativeSize = rt.sizeDelta;
        Vector2 basePos = rt.anchoredPosition;
        Vector2 startSize = nativeSize * WaveStartSizeRatio;
        rt.sizeDelta = startSize;
        group.alpha = 0.35f;

        float t = 0f;
        while (t < WaveSlamDuration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / WaveSlamDuration);
            float ease = EaseOutBack(u, 1.55f);
            rt.sizeDelta = Vector2.LerpUnclamped(startSize, nativeSize, ease);
            group.alpha = Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(u * 1.15f));
            yield return null;
        }

        rt.sizeDelta = nativeSize;
        rt.localScale = Vector3.one;
        group.alpha = 1f;

        t = 0f;
        while (t < WaveLandShakeDuration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / WaveLandShakeDuration);
            float falloff = 1f - u;
            float wobble = Mathf.Sin(u * Mathf.PI * 7.5f) * 10f * falloff;
            float lift = Mathf.Sin(u * Mathf.PI) * 3.5f * falloff;
            rt.anchoredPosition = basePos + new Vector2(wobble, lift);
            float scalePulse = 1f + 0.06f * falloff * Mathf.Sin(u * Mathf.PI * 4f);
            rt.localScale = Vector3.one * scalePulse;
            yield return null;
        }

        rt.anchoredPosition = basePos;
        rt.localScale = Vector3.one;

        t = 0f;
        while (t < WaveHoldDuration)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        t = 0f;
        while (t < WaveFadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / WaveFadeOutDuration);
            group.alpha = 1f - u * u;
            yield return null;
        }

        group.alpha = 0f;
        rt.anchoredPosition = basePos;
        rt.localScale = Vector3.one;
    }

    static float EaseOutBack(float t, float overshoot)
    {
        float c1 = overshoot;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
