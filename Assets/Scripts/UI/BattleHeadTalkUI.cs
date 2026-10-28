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
    bool _typingDone;
    /// <summary>
    /// 打字机速度（字/秒）。【2026-10-07 主人拍板】36 → 18（慢一倍）：
    /// 主人原话「佣兵说的话太快了没看到…字出现的速度慢点」（此处的佣兵＝引导局那位 H003 塔克）。
    /// 战斗头顶气泡只有一两秒的窗口，字一闪就没了，玩家根本读不完。
    /// </summary>
    const float TypeCharsPerSecond = 18f;
    /// <summary>
    /// 打完字之后的停留（秒）。【2026-10-07 主人拍板】1.35 → 2.4：
    /// 主人原话「剧情对话时间都长点」。停留太短 = 刚打完就消失，等于没说。
    /// </summary>
    const float DefaultHold = 2.4f;

    /// <summary>
    /// 气泡基准尺寸。【2026-10-10 主人反馈「玩家头顶气泡字太挤」】宽 200 → 260。
    /// 【2026-10-12 主人再报「我不是让你放大一点吗，看着太挤了」】→ 字号 26 → 32（与 FirstRunGuide 同档），
    /// 基准宽高 260×107 → 320×132 等比放大；具体尺寸仍由 SpeechBubbleFit 按文本适配，这里只抬基准。
    /// 【2026-10-14 主人「只把对话框放大一点」】320×132 → 360×152 再抬一档（宽高比不变，仍等比缩放）。
    /// 【2026-10-21 主人「战斗里的对话框不小不要给我改 给我还原…不要很长一行把框拉那么宽」】
    /// → 回到贴图原始像素 260×136：360 宽时贴图被横向拉伸 1.38 倍，框显得又宽又扁。
    /// 现在宽度锁死不再横向拉伸，长句只往上长高（配 SpeechBubbleFit 的 growHeightOnly）。
    /// </summary>
    static readonly Vector2 BaseBubbleSize = new Vector2(260f, 136f);
    /// <summary>
    /// 【2026-10-14 主人「根据字的多少要实时的调整对话框大小」】最小等比缩小倍数。
    /// 0.8 = 短句最多收到基准的 80%，长句照旧最多放大到 200%（SpeechBubbleFit 的 maxScale 默认值）。
    /// 字号不跟着缩 —— 只缩框，文字始终 26。
    /// </summary>
    const float BubbleMinScale = 0.8f;
    /// <summary>左右内边距：同步 18 → 22，气泡拉宽后留白要跟上，别让字贴边框。</summary>
    const float PadX = 22f;
    const float PadTop = 14f;
    const float PadBottom = 22f;
    /// <summary>头顶气泡正文字号。2026-10-10 主人拍板 22→26；2026-10-12 主人再报太挤 → 26→32。
    /// 【2026-10-21 还原窄框】框宽退回 260 后，32 号字一行只放得下 6 个字（长句要折 4 行、框变得又高又窄），
    /// 一并退回 26（同 10-10 那版），一行 8 个字，两句即收。要再调只改这一个常量。</summary>
    const int BubbleFontSize = 26;
    /// <summary>
    /// 气泡锚点离说话人的抬高（世界单位，脚下原点起算）。【2026-10-20 主人「能不能离得近点」】1.55 → 1.35：
    /// 气泡 pivot 在底部中点，锚点越低气泡越贴头；要再近/再远改这一个常量（低于 1.2 会压到头顶）。
    /// </summary>
    const float HeadOffsetY = 1.35f;

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
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
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
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.StoryDialogue);

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
        _text.fontSize = BubbleFontSize;
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
        var canvas = GetComponent<Canvas>();
        if (canvas != null)
            UICanvasSetup.RefreshPopup(canvas, GameConfig.UiSort.StoryDialogue);
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
        GlobalToastUI.PushBubble();
        TutorialHintUI.Instance?.Hide();
        string display = SpeechBubbleFit.Apply(_root, _text, raw, BaseBubbleSize,
            maxScale: 2f, minScale: BubbleMinScale, growHeightOnly: true);
        if (_text != null) _text.text = "";
        RefreshFollowPosition();
        // 【2026-10-20 主人拍板「战斗中佣兵的吐槽什么的不要算剧情对话」】
        // 头顶气泡只是碎碎念，不再抢占 BGM（原来每次台词都会切到剧情曲、台词一完战斗曲又从头播）。
        // 真正的战斗剧情对话仍由 StoryDirector 走 GameBgm.BeginBattleStory/EndBattleStory。

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

    Camera ResolveUiCamera()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay && canvas.worldCamera != null)
            return canvas.worldCamera;
        return UICanvasSetup.ResolveUiCamera();
    }

    void RefreshFollowPosition()
    {
        if (_root == null || !_root.gameObject.activeSelf || _follow == null) return;
        var uiCam = ResolveUiCamera();
        if (uiCam == null) return;
        Vector3 world = _follow.transform.position + new Vector3(0f, HeadOffsetY, 0f);
        Vector3 screen = uiCam.WorldToScreenPoint(world);
        if (screen.z <= 0f)
        {
            screen.z = 1f;
            screen.x = Mathf.Clamp(screen.x, Screen.width * 0.2f, Screen.width * 0.8f);
            screen.y = Screen.height * 0.55f;
        }
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            transform as RectTransform, screen, uiCam, out var local);
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
        GlobalToastUI.PopBubble();
    }
}
