using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗内角色头顶对话气泡（引导专用）。
/// </summary>
public class BattleHeadTalkUI : MonoBehaviour
{
    public static BattleHeadTalkUI Instance { get; private set; }

    Canvas _canvas;
    CanvasGroup _group;
    RectTransform _root;
    Image _bg;
    Text _text;
    Image _tail;
    UnitBase _follow;
    Coroutine _co;
    Coroutine _animCo;
    bool _skipRequested;
    bool _storyBgmHeld;
    const float TypeCharsPerSecond = 28f;

    public bool IsShowing => _root != null && _root.gameObject.activeSelf;
    public string CurrentLine => _text != null ? _text.text : "";

    public static BattleHeadTalkUI Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("BattleHeadTalkUI", typeof(RectTransform));
        DontDestroyOnLoad(go);
        return go.AddComponent<BattleHeadTalkUI>();
    }

    void Awake()
    {
        Instance = this;
        Build();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Build()
    {
        _canvas = gameObject.GetComponent<Canvas>();
        if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 560;

        var scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720f, 1280f);
        scaler.matchWidthOrHeight = 1f;

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
        _group = gameObject.GetComponent<CanvasGroup>();
        if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 1f;

        var rootGo = new GameObject("Bubble", typeof(RectTransform), typeof(Image));
        rootGo.transform.SetParent(transform, false);
        _root = rootGo.GetComponent<RectTransform>();
        _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
        _root.pivot = new Vector2(0.5f, 0f);
        _root.sizeDelta = new Vector2(360f, 120f);
        _bg = rootGo.GetComponent<Image>();
        _bg.raycastTarget = false;
        _bg.color = Color.white;
        _bg.sprite = LoadBubbleSprite();
        if (_bg.sprite == null)
            _bg.color = new Color(0.93f, 0.88f, 0.75f, 0.96f);
        ApplyBubbleStretchMode();
        // 禁止非等比缩放，气泡只按文字改宽高
        _root.localScale = Vector3.one;

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(rootGo.transform, false);
        _text = textGo.AddComponent<Text>();
        _text.alignment = TextAnchor.MiddleCenter;
        _text.fontSize = 24;
        _text.color = new Color(0.22f, 0.14f, 0.1f, 1f);
        _text.horizontalOverflow = HorizontalWrapMode.Wrap;
        _text.verticalOverflow = VerticalWrapMode.Overflow;
        _text.resizeTextForBestFit = false;
        _text.raycastTarget = false;
        _text.font = GameFonts.GetChinese();
        var tr = _text.rectTransform;
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(PadX, PadBottom);
        tr.offsetMax = new Vector2(-PadX, -PadTop);
        // 不用 RectMask2D 切字；多行靠加高气泡容纳

        var tailGo = new GameObject("Tail", typeof(RectTransform), typeof(Image));
        tailGo.transform.SetParent(rootGo.transform, false);
        _tail = tailGo.GetComponent<Image>();
        _tail.raycastTarget = false;
        _tail.color = new Color(0.94f, 0.9f, 0.84f, 1f);
        var tailRt = _tail.rectTransform;
        tailRt.anchorMin = tailRt.anchorMax = new Vector2(0.5f, 0f);
        tailRt.pivot = new Vector2(0.5f, 1f);
        tailRt.anchoredPosition = new Vector2(0f, -2f);
        tailRt.sizeDelta = new Vector2(20f, 16f);
        // 底图自带尖角时隐藏额外白方块尾巴，避免像「缺头像」
        if (_bg != null && _bg.sprite != null)
            tailGo.SetActive(false);

        rootGo.SetActive(false);
    }

    /// <summary>
    /// 气泡底图有九宫格 border 才能随意拉伸；没设 border 的图用 Sliced 等于 Simple，
    /// 会被拉成扁条（看起来「被压缩了」）。没 border 就改成等比 Simple，靠尺寸保住比例。
    /// </summary>
    void ApplyBubbleStretchMode()
    {
        if (_bg == null) return;
        if (_bg.sprite == null)
        {
            // 无底图时用纯色块，可自由随文字改尺寸
            _bg.type = Image.Type.Simple;
            _bg.preserveAspect = false;
            _bubbleSliced = false;
            _bubbleAspect = 0f;
            return;
        }
        // 有底图：一律原图比例缩放，禁止九宫格拉扁
        Rect r = _bg.sprite.rect;
        _bubbleAspect = r.height > 1f ? r.width / r.height : 2.4f;
        _bubbleSliced = false;
        _bg.type = Image.Type.Simple;
        _bg.preserveAspect = true;
    }

    bool _bubbleSliced = true;
    float _bubbleAspect;

    Sprite LoadBubbleSprite()
    {
        return StoryProps.Get(StoryProps.SpeechBubble);
    }

    public Coroutine PlayLine(UnitBase speaker, string content, float hold = 1.8f)
    {
        if (_co != null)
        {
            StopCoroutine(_co);
            _co = null;
            HideNow();
        }
        _skipRequested = false;
        _co = StartCoroutine(CoPlayLine(speaker, content, hold));
        return _co;
    }

    /// <summary>供外部 yield：在 BattleHeadTalkUI 上跑完一整句台词。</summary>
    public IEnumerator CoPlayLine(UnitBase speaker, string content, float hold = 1.8f)
    {
        _skipRequested = false;
        yield return PlayLineRoutine(speaker, content, hold);
        _co = null;
    }

    void Update()
    {
        if (!IsShowing) return;
        if (Clicked())
            _skipRequested = true;
    }

    static bool Clicked()
    {
        if (Input.GetMouseButtonDown(0)) return true;
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) return true;
        return false;
    }

    IEnumerator PlayLineRoutine(UnitBase speaker, string content, float hold)
    {
        _follow = speaker;
        string raw = (content ?? "").Replace("\r", "").Trim();
        float maxDur = hold + raw.Length * 0.12f + 4f;
        float elapsed = 0f;

        if (_text != null) _text.text = "";
        if (_root != null)
        {
            _root.localScale = Vector3.one;
            _root.gameObject.SetActive(true);
            FitBubbleToText(raw);
        }
        if (_group != null) _group.alpha = 1f;
        PlayPopIn();
        RefreshFollowPosition();
        AcquireStoryBgm();

        string full = _text != null ? _text.text : raw;
        if (_text != null && full.Length > 0 && !_skipRequested)
        {
            // 打字机按无换行原文逐字，再每帧按气泡最宽处重排
            float delay = 1f / Mathf.Max(8f, TypeCharsPerSecond);
            for (int i = 1; i <= raw.Length && !_skipRequested; i++)
            {
                FitBubbleToText(raw.Substring(0, i));
                float tType = 0f;
                while (tType < delay && !_skipRequested)
                {
                    tType += Time.unscaledDeltaTime;
                    elapsed += Time.unscaledDeltaTime;
                    RefreshFollowPosition();
                    yield return null;
                }
            }
            if (!_skipRequested)
                FitBubbleToText(raw);
        }

        float t = 0f;
        while (t < hold && !_skipRequested && elapsed < maxDur)
        {
            t += Time.unscaledDeltaTime;
            elapsed += Time.unscaledDeltaTime;
            RefreshFollowPosition();
            yield return null;
        }

        yield return FadeOutAndHide(0.16f);
    }

    void LateUpdate()
    {
        RefreshFollowPosition();
    }

    const float MaxBubbleW = 360f;
    const float MinBubbleH = 100f;
    const float PadX = 28f;
    const float PadTop = 18f;
    const float PadBottom = 24f;

    TextGenerator _textGen;

    /// <summary>
    /// 按气泡底图「最宽可视宽度」做像素换行，多行只加高不挤扁。
    /// preserveAspect 时可视宽 = min(MaxBubbleW, H * aspect)。
    /// </summary>
    void FitBubbleToText(string raw)
    {
        if (_root == null || _text == null) return;
        raw = (raw ?? "").Replace("\r", "").Replace("\n", " ").Trim();
        if (string.IsNullOrEmpty(raw))
        {
            _text.text = "";
            _root.sizeDelta = new Vector2(MaxBubbleW, MinBubbleH);
            return;
        }

        float aspect = _bubbleAspect > 0.1f ? _bubbleAspect : 2.4f;
        // 先定宽：不超过 MaxBubbleW，且保持底图比例时的最宽
        float bubbleW = MaxBubbleW;
        float visualW = bubbleW;
        if (!_bubbleSliced && _bg != null && _bg.sprite != null && _bg.preserveAspect)
        {
            // 用基准高度估可视宽；短句可再收窄
            float baseH = MinBubbleH;
            visualW = Mathf.Min(bubbleW, baseH * aspect);
        }
        float wrapW = Mathf.Max(80f, visualW - PadX * 2f);

        if (_textGen == null) _textGen = new TextGenerator();
        var settings = _text.GetGenerationSettings(new Vector2(wrapW, 0f));
        settings.horizontalOverflow = HorizontalWrapMode.Wrap;
        settings.verticalOverflow = VerticalWrapMode.Overflow;
        settings.generateOutOfBounds = true;
        settings.resizeTextForBestFit = false;
        _textGen.Populate(raw, settings);
        float prefH = _textGen.GetPreferredHeight(raw, settings);
        float prefW = _textGen.GetPreferredWidth(raw, settings);

        int lines = Mathf.Max(1, _textGen.lineCount);
        float textH = Mathf.Max(prefH, _text.fontSize * 1.2f * lines);
        float bubbleH = Mathf.Max(MinBubbleH, textH + PadTop + PadBottom);

        // 有底图等比：高度决定可视宽，再反推根尺寸让可视区吃满
        if (!_bubbleSliced && _bg != null && _bg.sprite != null && _bg.preserveAspect)
        {
            float needVisualW = Mathf.Clamp(prefW + PadX * 2f, 160f, MaxBubbleW);
            // letterbox：根宽=可视宽，根高=可视高（aspect 固定）
            bubbleH = Mathf.Max(bubbleH, needVisualW / aspect);
            bubbleW = bubbleH * aspect;
            if (bubbleW > MaxBubbleW)
            {
                bubbleW = MaxBubbleW;
                bubbleH = bubbleW / aspect;
            }
            // 文字区宽按最终可视宽
            wrapW = Mathf.Max(80f, bubbleW - PadX * 2f);
            settings = _text.GetGenerationSettings(new Vector2(wrapW, 0f));
            settings.horizontalOverflow = HorizontalWrapMode.Wrap;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            settings.resizeTextForBestFit = false;
            _textGen.Populate(raw, settings);
        }
        else
        {
            bubbleW = Mathf.Clamp(prefW + PadX * 2f, 160f, MaxBubbleW);
        }

        _root.sizeDelta = new Vector2(bubbleW, bubbleH);
        _root.localScale = Vector3.one;
        _text.text = raw;
        Canvas.ForceUpdateCanvases();
    }

    void RefreshFollowPosition()
    {
        if (_root == null || !_root.gameObject.activeSelf || _follow == null) return;
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 world = _follow.transform.position + new Vector3(0f, 1.65f, 0f);
        Vector3 screen = cam.WorldToScreenPoint(world);
        if (screen.z <= 0f)
        {
            screen.z = 1f;
            screen.x = Mathf.Clamp(screen.x, Screen.width * 0.2f, Screen.width * 0.8f);
            screen.y = Screen.height * 0.55f;
        }
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            transform as RectTransform, screen, null, out var local);
        var hostRt = transform as RectTransform;
        float halfW = _root.rect.width * 0.5f;
        float h = _root.rect.height;
        // pivot 在底中：钳 X 与 Y，保证整泡在屏内，仍尽量贴说话人
        float minX = hostRt.rect.xMin + halfW + 8f;
        float maxX = hostRt.rect.xMax - halfW - 8f;
        float minY = hostRt.rect.yMin + 8f;
        float maxY = hostRt.rect.yMax - h - 8f;
        float clampedX = Mathf.Clamp(local.x, minX, maxX);
        float clampedY = Mathf.Clamp(local.y, minY, maxY);
        _root.anchoredPosition = new Vector2(clampedX, clampedY);
        RefreshTail(local.x - clampedX);
    }

    void RefreshTail(float speakerXOffset)
    {
        if (_tail == null || _root == null) return;
        float half = _root.rect.width * 0.5f - 18f;
        float x = Mathf.Clamp(speakerXOffset, -half, half);
        var rt = _tail.rectTransform;
        rt.anchoredPosition = new Vector2(x, -2f);
        float angle = Mathf.Clamp(-x * 0.08f, -24f, 24f);
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    void PlayPopIn()
    {
        if (_root == null) return;
        _root.localScale = new Vector3(0.9f, 0.9f, 1f);
        if (_animCo != null) StopCoroutine(_animCo);
        _animCo = StartCoroutine(PopInRoutine());
    }

    IEnumerator PopInRoutine()
    {
        if (_root == null) yield break;
        float t = 0f;
        const float dur = 0.12f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / dur);
            float s = Mathf.Lerp(0.9f, 1f, 1f - Mathf.Pow(1f - p, 3f));
            _root.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        _root.localScale = Vector3.one;
        _animCo = null;
    }

    IEnumerator FadeOutAndHide(float duration)
    {
        if (_group == null)
        {
            HideNow();
            yield break;
        }
        float t = 0f;
        float startA = _group.alpha;
        duration = Mathf.Max(0.05f, duration);
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            _group.alpha = Mathf.Lerp(startA, 0f, Mathf.Clamp01(t / duration));
            yield return null;
        }
        _group.alpha = 1f;
        HideNow();
    }

    public void HideNow()
    {
        _skipRequested = false;
        if (_co != null)
        {
            StopCoroutine(_co);
            _co = null;
        }
        if (_animCo != null)
        {
            StopCoroutine(_animCo);
            _animCo = null;
        }
        _follow = null;
        if (_group != null) _group.alpha = 1f;
        if (_root != null) _root.gameObject.SetActive(false);
        ReleaseStoryBgm();
    }

    void AcquireStoryBgm()
    {
        if (_storyBgmHeld) return;
        _storyBgmHeld = true;
        GameBgm.BeginBattleStory();
    }

    void ReleaseStoryBgm()
    {
        if (!_storyBgmHeld) return;
        _storyBgmHeld = false;
        GameBgm.EndBattleStory();
    }
}
