using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗头顶气泡。点一下跳过打字，再点一下（或短暂停留后）下一句。
/// </summary>
public class BattleHeadTalkUI : MonoBehaviour
{
    public static BattleHeadTalkUI Instance { get; private set; }

    CanvasGroup _group;
    RectTransform _root;
    Image _bg;
    Text _text;
    UnitBase _follow;
    Coroutine _co;
    bool _skipTyping;
    bool _advance;
    bool _storyBgmHeld;
    bool _typingDone;
    const float TypeCharsPerSecond = 36f;
    const float DefaultHold = 1.35f;

    static readonly Vector2 BaseBubbleSize = new Vector2(200f, 107f);
    const float PadX = 18f;
    const float PadTop = 14f;
    const float PadBottom = 22f;

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
        var canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 560;

        var scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720f, 1280f);
        scaler.matchWidthOrHeight = 1f;

        // 不要 GraphicRaycaster：否则会吞点击，引导气泡点不掉
        var oldRay = GetComponent<GraphicRaycaster>();
        if (oldRay != null) Destroy(oldRay);

        _group = gameObject.GetComponent<CanvasGroup>();
        if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 1f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        var rootGo = new GameObject("Bubble", typeof(RectTransform), typeof(Image));
        rootGo.transform.SetParent(transform, false);
        _root = rootGo.GetComponent<RectTransform>();
        _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
        _root.pivot = new Vector2(0.5f, 0f);
        _root.sizeDelta = BaseBubbleSize;
        _bg = rootGo.GetComponent<Image>();
        _bg.raycastTarget = false;
        _bg.color = Color.white;
        _bg.sprite = StoryProps.Get(StoryProps.SpeechBubble);
        if (_bg.sprite == null)
            _bg.color = new Color(0.93f, 0.90f, 0.86f, 0.96f);
        _bg.type = Image.Type.Simple;
        _bg.preserveAspect = false;

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(rootGo.transform, false);
        _text = textGo.AddComponent<Text>();
        _text.alignment = TextAnchor.MiddleCenter;
        _text.fontSize = 22;
        _text.color = new Color(0.28f, 0.28f, 0.30f, 1f);
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

        rootGo.SetActive(false);
    }

    public Coroutine PlayLine(UnitBase speaker, string content, float hold = -1f)
    {
        if (_co != null)
        {
            StopCoroutine(_co);
            _co = null;
        }
        _co = StartCoroutine(CoPlayLine(speaker, content, hold));
        return _co;
    }

    /// <summary>可被外部直接 yield（TutorialDirector 上跑，不二次 StartCoroutine）。</summary>
    public IEnumerator CoPlayLine(UnitBase speaker, string content, float hold = -1f)
    {
        _skipTyping = false;
        _advance = false;
        _typingDone = false;
        if (hold < 0f) hold = DefaultHold;

        // 保留人工换行；多余空白压成单空格，方便长句自动折行成多行
        string raw = (content ?? "").Replace("\r\n", "\n").Replace("\r", "\n").Trim();
        while (raw.Contains("  ")) raw = raw.Replace("  ", " ");
        _follow = speaker;

        if (_root != null)
        {
            _root.localScale = Vector3.one;
            _root.gameObject.SetActive(true);
        }
        if (_group != null) _group.alpha = 1f;
        string display = SpeechBubbleFit.Apply(_root, _text, raw, BaseBubbleSize);
        if (_text != null) _text.text = "";
        RefreshFollowPosition();
        AcquireStoryBgm();

        // 打字机：只改字，不再每字重算布局
        if (!string.IsNullOrEmpty(display))
        {
            float delay = 1f / TypeCharsPerSecond;
            for (int i = 1; i <= display.Length; i++)
            {
                if (_skipTyping || _advance) break;
                if (_text != null) _text.text = display.Substring(0, i);
                float t = 0f;
                while (t < delay && !_skipTyping && !_advance)
                {
                    t += Mathf.Max(0.008f, Time.unscaledDeltaTime);
                    RefreshFollowPosition();
                    yield return null;
                }
            }
            if (_text != null) _text.text = display;
        }
        _typingDone = true;

        // 说完后短暂停一下；点一下立刻下一句
        float holdT = 0f;
        while (holdT < hold && !_advance)
        {
            holdT += Mathf.Max(0.008f, Time.unscaledDeltaTime);
            RefreshFollowPosition();
            yield return null;
        }

        HideNow();
        _co = null;
    }

    void Update()
    {
        if (!IsShowing) return;
        if (!Clicked()) return;

        if (!_typingDone)
            _skipTyping = true; // 第一次：出完字
        else
            _advance = true;    // 第二次：下一句
    }

    static bool Clicked()
    {
        if (Input.GetMouseButtonDown(0)) return true;
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) return true;
        return false;
    }

    void LateUpdate() => RefreshFollowPosition();

    void RefreshFollowPosition()
    {
        if (_root == null || !_root.gameObject.activeSelf || _follow == null) return;
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 world = _follow.transform.position + new Vector3(0f, 1.55f, 0f);
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
        float minX = hostRt.rect.xMin + halfW + 8f;
        float maxX = hostRt.rect.xMax - halfW - 8f;
        float minY = hostRt.rect.yMin + 8f;
        float maxY = hostRt.rect.yMax - h - 8f;
        _root.anchoredPosition = new Vector2(
            Mathf.Clamp(local.x, minX, maxX),
            Mathf.Clamp(local.y, minY, maxY));
    }

    public void HideNow()
    {
        _skipTyping = false;
        _advance = false;
        _typingDone = false;
        if (_co != null)
        {
            StopCoroutine(_co);
            _co = null;
        }
        _follow = null;
        if (_group != null) _group.alpha = 1f;
        if (_root != null)
        {
            SpeechBubbleFit.ResetSize(_root, BaseBubbleSize);
            _root.gameObject.SetActive(false);
        }
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
