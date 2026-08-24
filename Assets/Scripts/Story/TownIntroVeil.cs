using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 片头/开场剧情期间的全屏黑幕，避免露出空城镇。
/// </summary>
public class TownIntroVeil : MonoBehaviour
{
    public static TownIntroVeil Instance { get; private set; }

    CanvasGroup _group;

    public static bool IsBlocking =>
        Instance != null
        && Instance.gameObject.activeInHierarchy
        && Instance._group != null
        && Instance._group.alpha > 0.05f;

    public static void EnsureShown()
    {
        if (Instance != null)
        {
            Instance._group.alpha = 1f;
            Instance.gameObject.SetActive(true);
            return;
        }

        var go = new GameObject("TownIntroVeil");
        DontDestroyOnLoad(go);
        var veil = go.AddComponent<TownIntroVeil>();
        veil.Build();
    }

    public static IEnumerator FadeOutRoutine(float duration = 0.7f)
    {
        if (Instance == null) yield break;
        yield return Instance.FadeOut(duration);
    }

    /// <summary>Loading 结束或引导完成后强制移除，避免中间全黑挡操作。</summary>
    public static void ForceDestroy()
    {
        if (Instance == null) return;
        Object.Destroy(Instance.gameObject);
    }

    void Build()
    {
        Instance = this;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 260;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720f, 1280f);
        scaler.matchWidthOrHeight = 1f;

        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 1f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        var bgGo = new GameObject("Black", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bgGo.transform.SetParent(transform, false);
        var bg = bgGo.GetComponent<Image>();
        bg.color = Color.black;
        bg.raycastTarget = false;
        var rt = bg.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    IEnumerator FadeOut(float duration)
    {
        float t = 0f;
        duration = Mathf.Max(0.05f, duration);
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            if (_group != null)
                _group.alpha = 1f - Mathf.Clamp01(t / duration);
            yield return null;
        }
        if (_group != null) _group.alpha = 0f;
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
