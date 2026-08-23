using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 全局浮字 Toast（DontDestroyOnLoad），供 UIManager.ShowToast 使用。
/// </summary>
public class GlobalToastUI : MonoBehaviour
{
    static GlobalToastUI _instance;
    Text _label;
    CanvasGroup _cg;
    Coroutine _co;

    public static void Show(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return;
        Ensure().Play(msg);
    }

    static GlobalToastUI Ensure()
    {
        if (_instance != null) return _instance;
        var go = new GameObject("GlobalToastUI");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<GlobalToastUI>();
        _instance.Build();
        return _instance;
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        UICanvasSetup.Apply(canvas);
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32000;
        gameObject.AddComponent<GraphicRaycaster>().enabled = false;

        var root = new GameObject("Root", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        root.transform.SetParent(transform, false);
        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.42f);
        rt.anchorMax = new Vector2(0.5f, 0.42f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(560f, 72f);
        root.GetComponent<Image>().color = new Color(0.08f, 0.07f, 0.1f, 0.82f);
        root.GetComponent<Image>().raycastTarget = false;
        _cg = root.GetComponent<CanvasGroup>();
        _cg.blocksRaycasts = false;
        _cg.interactable = false;

        var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(root.transform, false);
        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(16f, 8f);
        trt.offsetMax = new Vector2(-16f, -8f);
        _label = textGo.GetComponent<Text>();
        _label.font = GameFonts.GetChinese();
        if (_label.font == null)
            _label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _label.fontSize = 28;
        _label.alignment = TextAnchor.MiddleCenter;
        _label.color = new Color(1f, 0.94f, 0.72f, 1f);
        _label.horizontalOverflow = HorizontalWrapMode.Wrap;
        _label.verticalOverflow = VerticalWrapMode.Overflow;
        _label.raycastTarget = false;

        root.SetActive(false);
    }

    void Play(string msg)
    {
        if (_label == null) Build();
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoPlay(msg));
    }

    IEnumerator CoPlay(string msg)
    {
        _label.text = msg;
        var root = _label.transform.parent.gameObject;
        root.SetActive(true);
        root.transform.SetAsLastSibling();
        _cg.alpha = 0f;
        float t = 0f;
        while (t < 0.15f)
        {
            t += Time.unscaledDeltaTime;
            _cg.alpha = Mathf.Clamp01(t / 0.15f);
            yield return null;
        }
        _cg.alpha = 1f;
        yield return new WaitForSecondsRealtime(1.6f);
        t = 0f;
        while (t < 0.35f)
        {
            t += Time.unscaledDeltaTime;
            _cg.alpha = 1f - Mathf.Clamp01(t / 0.35f);
            yield return null;
        }
        root.SetActive(false);
        _co = null;
    }
}
