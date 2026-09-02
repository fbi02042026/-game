using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 全局提示 Toast（DontDestroyOnLoad）：深色半透明底板 + 文案。
/// 登录未勾协议、存档提示、商人关等屏幕浮字一律走这里 / UIManager.ShowToast。
/// </summary>
public class GlobalToastUI : MonoBehaviour
{
    static GlobalToastUI _instance;
    Text _label;
    CanvasGroup _cg;
    GameObject _root;
    RectTransform _rootRt;
    Vector2 _basePos;
    Coroutine _co;

    public static bool IsShowing =>
        _instance != null && _instance._root != null && _instance._root.activeSelf;

    public static string CurrentMessage =>
        _instance != null && _instance._label != null ? _instance._label.text : "";

    public static void Hide()
    {
        if (_instance == null) return;
        if (_instance._co != null)
        {
            _instance.StopCoroutine(_instance._co);
            _instance._co = null;
        }
        if (_instance._cg != null) _instance._cg.alpha = 0f;
        if (_instance._root != null) _instance._root.SetActive(false);
        if (_instance._rootRt != null) _instance._rootRt.anchoredPosition = _instance._basePos;
    }

    public static void Show(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return;
        // 直接调 GlobalToastUI 的路径也要挡气泡
        if (AnyBubbleShowing) return;
        Ensure().Play(msg);
    }

    /// <summary>从左淡入到中央 → 慢右移 1.5s → 快速右出（精英击破等战斗播报）。</summary>
    public static void ShowFlythrough(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return;
        if (AnyBubbleShowing) return;
        Ensure().PlayFlythrough(msg);
    }

    static int _bubbleHolders;

    /// <summary>非 Talker 类气泡（如酒馆图标气泡）显示期间登记，屏蔽屏幕浮字。</summary>
    public static void PushBubble() => _bubbleHolders++;

    public static void PopBubble() => _bubbleHolders = Mathf.Max(0, _bubbleHolders - 1);

    /// <summary>任意对话框 / 气泡正在显示时，不叠屏幕提示字。</summary>
    public static bool AnyBubbleShowing
    {
        get
        {
            if (_bubbleHolders > 0) return true;
            if (DialogueUI.Instance != null && DialogueUI.Instance.IsVisible) return true;
            if (BattleHeadTalkUI.Instance != null && BattleHeadTalkUI.Instance.IsShowing) return true;
            if (SpeechBubbleTalker.AnyBubbleShowing()) return true;
            return false;
        }
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
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.Toast);
        gameObject.AddComponent<GraphicRaycaster>().enabled = false;

        _root = new GameObject("Root", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        _root.transform.SetParent(transform, false);
        _rootRt = _root.GetComponent<RectTransform>();
        _rootRt.anchorMin = new Vector2(0.5f, 0.42f);
        _rootRt.anchorMax = new Vector2(0.5f, 0.42f);
        _rootRt.pivot = new Vector2(0.5f, 0.5f);
        _rootRt.sizeDelta = new Vector2(560f, 72f);
        _basePos = Vector2.zero;
        _rootRt.anchoredPosition = _basePos;
        var plate = _root.GetComponent<Image>();
        plate.color = new Color(0.08f, 0.07f, 0.1f, 0.6f);
        plate.raycastTarget = false;
        _cg = _root.GetComponent<CanvasGroup>();
        _cg.blocksRaycasts = false;
        _cg.interactable = false;

        var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(_root.transform, false);
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

        _root.SetActive(false);
    }

    void Play(string msg)
    {
        if (_label == null || _root == null) Build();
        var canvas = GetComponent<Canvas>();
        if (canvas != null)
            UICanvasSetup.RefreshPopup(canvas, GameConfig.UiSort.Toast);
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoPlay(msg));
    }

    void PlayFlythrough(string msg)
    {
        if (_label == null || _root == null) Build();
        var canvas = GetComponent<Canvas>();
        if (canvas != null)
            UICanvasSetup.RefreshPopup(canvas, GameConfig.UiSort.Toast);
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoFlythrough(msg));
    }

    IEnumerator CoPlay(string msg)
    {
        _label.text = msg;
        var plate = _root.GetComponent<Image>();
        if (plate != null)
            plate.color = new Color(0.08f, 0.07f, 0.1f, 0.6f);
        _root.SetActive(true);
        _root.transform.SetAsLastSibling();
        if (_rootRt != null) _rootRt.anchoredPosition = _basePos;

        _cg.alpha = 0f;
        float t = 0f;
        const float fadeIn = 0.15f;
        while (t < fadeIn)
        {
            t += Time.unscaledDeltaTime;
            _cg.alpha = Mathf.Clamp01(t / fadeIn);
            yield return null;
        }
        _cg.alpha = 1f;

        yield return new WaitForSecondsRealtime(1f);

        // 往上飘并淡出
        t = 0f;
        const float fadeOut = 0.45f;
        const float rise = 80f;
        while (t < fadeOut)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / fadeOut);
            float ease = 1f - (1f - u) * (1f - u);
            _cg.alpha = 1f - u;
            if (_rootRt != null)
                _rootRt.anchoredPosition = _basePos + new Vector2(0f, rise * ease);
            yield return null;
        }

        _cg.alpha = 0f;
        if (_rootRt != null) _rootRt.anchoredPosition = _basePos;
        _root.SetActive(false);
        _co = null;
    }

    IEnumerator CoFlythrough(string msg)
    {
        _label.text = msg;
        var plate = _root.GetComponent<Image>();
        const int normalFontSize = 28;
        var normalTextColor = new Color(1f, 0.94f, 0.72f, 1f);
        var normalPlateColor = new Color(0.08f, 0.07f, 0.1f, 0.6f);

        _label.fontSize = 34;
        _label.color = new Color(1f, 0.88f, 0.38f, 1f);
        if (plate != null)
            plate.color = new Color(0.06f, 0.05f, 0.08f, 0.42f);

        _root.SetActive(true);
        _root.transform.SetAsLastSibling();

        const float inDur = 0.22f;
        const float driftDur = 1.5f;
        const float outDur = 0.22f;
        const float startX = -520f;
        const float centerX = 0f;
        const float driftEndX = 180f;
        const float exitX = 620f;

        // 1) 左侧快速淡入到中央
        float t = 0f;
        while (t < inDur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / inDur);
            float ease = 1f - (1f - u) * (1f - u);
            if (_rootRt != null)
                _rootRt.anchoredPosition = _basePos + new Vector2(Mathf.Lerp(startX, centerX, ease), 0f);
            _cg.alpha = ease;
            yield return null;
        }
        if (_rootRt != null)
            _rootRt.anchoredPosition = _basePos + new Vector2(centerX, 0f);
        _cg.alpha = 1f;

        // 2) 中央慢向右漂
        t = 0f;
        while (t < driftDur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / driftDur);
            if (_rootRt != null)
                _rootRt.anchoredPosition = _basePos + new Vector2(Mathf.Lerp(centerX, driftEndX, u), 0f);
            yield return null;
        }

        // 3) 快速向右淡出
        t = 0f;
        float outStartX = driftEndX;
        while (t < outDur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / outDur);
            float ease = u * u;
            if (_rootRt != null)
                _rootRt.anchoredPosition = _basePos + new Vector2(Mathf.Lerp(outStartX, exitX, ease), 0f);
            _cg.alpha = 1f - u;
            yield return null;
        }

        _cg.alpha = 0f;
        if (_rootRt != null) _rootRt.anchoredPosition = _basePos;
        _label.fontSize = normalFontSize;
        _label.color = normalTextColor;
        if (plate != null) plate.color = normalPlateColor;
        _root.SetActive(false);
        _co = null;
    }
}
