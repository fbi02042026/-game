using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>战斗全屏闪白/闪红，用于教程「下一波来袭」等提示。</summary>
public static class BattleScreenFlash
{
    static CanvasGroup _group;

    public static IEnumerator Play(Color color, float holdSeconds = 0.22f, float fadeSeconds = 0.35f)
    {
        EnsureFlashCanvas();
        if (_group == null) yield break;

        var img = _group.GetComponent<Image>();
        if (img != null) img.color = color;

        _group.alpha = 0f;
        _group.gameObject.SetActive(true);

        float t = 0f;
        while (t < holdSeconds)
        {
            t += Time.unscaledDeltaTime;
            _group.alpha = Mathf.Lerp(0f, color.a, Mathf.Clamp01(t / Mathf.Max(0.05f, holdSeconds * 0.5f)));
            yield return null;
        }
        _group.alpha = color.a;

        t = 0f;
        while (t < fadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            _group.alpha = color.a * (1f - Mathf.Clamp01(t / fadeSeconds));
            yield return null;
        }
        _group.alpha = 0f;
        _group.gameObject.SetActive(false);
    }

    static void EnsureFlashCanvas()
    {
        if (_group != null) return;

        var go = new GameObject("BattleScreenFlash");
        Object.DontDestroyOnLoad(go);
        var canvas = go.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.FullscreenFx);

        var scaler = go.GetComponent<CanvasScaler>();
        if (scaler != null)
            scaler.matchWidthOrHeight = 0f;

        _group = go.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        var bg = new GameObject("Flash");
        bg.transform.SetParent(go.transform, false);
        var img = bg.AddComponent<Image>();
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        img.color = new Color(1f, 0.35f, 0.25f, 0.55f);
        go.SetActive(false);
    }
}
