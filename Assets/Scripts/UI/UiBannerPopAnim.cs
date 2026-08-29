using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Resources UI Banner 标准动效（波次预告、同类切图弹图）：
/// · 终态：preserveAspect + SetNativeSize = 100% native，localScale 恒为 1
/// · 入场：sizeDelta 从 native×300% 在 0.3s 缩到 native（100%）
/// · 停 1s → CanvasGroup 淡出
/// </summary>
public static class UiBannerPopAnim
{
    public const float StartSizeRatio = 3f;
    public const float ShrinkDuration = 0.3f;
    public const float HoldDuration = 1f;
    public const float FadeOutDuration = 0.32f;

    public static float TotalDuration => ShrinkDuration + HoldDuration + FadeOutDuration;

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
}
