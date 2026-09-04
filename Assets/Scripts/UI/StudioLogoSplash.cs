using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 开机工作室 Logo：黑底上从 1.0 慢慢放大到 1.1；满不透明停留，最后 1 秒才淡隐。约 4 秒，不可跳过。
/// 插在健康忠告之前，纯运行时构建，不改预制体。
/// </summary>
public class StudioLogoSplash : MonoBehaviour
{
    public const float TotalSeconds = 4f;
    const float FadeOutSeconds = 1f;
    const float ScaleFrom = 1.0f;
    const float ScaleTo = 1.1f;
    const float FadeInSeconds = 0.35f;
    const float LogoWidth = 560f;
    const float LogoHeight = 220f;

    static readonly string LogoResourcesPath = ContentPaths.Ui.StudioLogo;

    Action _onFinished;
    bool _finished;
    CanvasGroup _logoGroup;
    RectTransform _logoRt;

    public static void Present(Action onFinished)
    {
        var existing = FindObjectOfType<StudioLogoSplash>();
        if (existing != null)
        {
            existing._onFinished = onFinished;
            return;
        }

        var go = new GameObject("StudioLogoSplash", typeof(RectTransform));
        DontDestroyOnLoad(go);
        var splash = go.AddComponent<StudioLogoSplash>();
        splash._onFinished = onFinished;
        splash.Build();
        splash.StartCoroutine(splash.RunRoutine());
    }

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.FullscreenFx, UICanvasSetup.ResolveUiCamera());

        // 吃点击：片头期间不可点穿
        var blocker = new GameObject("Blocker", typeof(RectTransform), typeof(Image));
        blocker.transform.SetParent(transform, false);
        Stretch(blocker.GetComponent<RectTransform>());
        var blockerImg = blocker.GetComponent<Image>();
        blockerImg.color = Color.black;
        blockerImg.raycastTarget = true;

        var logoGo = new GameObject("Logo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        logoGo.transform.SetParent(transform, false);
        _logoRt = logoGo.GetComponent<RectTransform>();
        _logoRt.anchorMin = _logoRt.anchorMax = new Vector2(0.5f, 0.5f);
        _logoRt.pivot = new Vector2(0.5f, 0.5f);
        _logoRt.sizeDelta = new Vector2(LogoWidth, LogoHeight);
        _logoRt.anchoredPosition = Vector2.zero;
        _logoRt.localScale = Vector3.one * ScaleFrom;

        var logoImg = logoGo.GetComponent<Image>();
        logoImg.sprite = LoadLogoSprite();
        logoImg.preserveAspect = true;
        logoImg.raycastTarget = false;
        logoImg.color = Color.white;

        _logoGroup = logoGo.GetComponent<CanvasGroup>();
        _logoGroup.alpha = 0f;
        _logoGroup.blocksRaycasts = false;
        _logoGroup.interactable = false;
    }

    IEnumerator RunRoutine()
    {
        yield return null;
        BootManager.ReleaseBootVeil();

        float t = 0f;
        while (t < TotalSeconds && !_finished)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / TotalSeconds);
            // 缓入缓出：慢慢放大
            float e = k * k * (3f - 2f * k);

            if (_logoRt != null)
                _logoRt.localScale = Vector3.one * Mathf.Lerp(ScaleFrom, ScaleTo, e);

            // 开头淡入 → 保持不透明 → 仅最后 1 秒淡隐（不与放大同步消失）
            float alpha;
            if (t < FadeInSeconds)
                alpha = Mathf.Clamp01(t / FadeInSeconds);
            else if (t < TotalSeconds - FadeOutSeconds)
                alpha = 1f;
            else
            {
                float fadeT = Mathf.Clamp01((t - (TotalSeconds - FadeOutSeconds)) / FadeOutSeconds);
                alpha = 1f - fadeT;
            }

            if (_logoGroup != null)
                _logoGroup.alpha = alpha;

            yield return null;
        }

        Finish();
    }

    void Finish()
    {
        if (_finished) return;
        _finished = true;
        var cb = _onFinished;
        _onFinished = null;
        cb?.Invoke();
        Destroy(gameObject);
    }

    static Sprite LoadLogoSprite()
    {
        var sp = Resources.Load<Sprite>(LogoResourcesPath);
        if (sp != null) return sp;
        var tex = Resources.Load<Texture2D>(LogoResourcesPath);
        if (tex == null)
        {
            Debug.LogWarning("[StudioLogoSplash] 未找到 Logo Resources/" + LogoResourcesPath);
            return null;
        }
        return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }

    static void Stretch(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
