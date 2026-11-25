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
    /// <summary>
    /// 固定 UI 锚点（2026-11-22）：非 null 时气泡浮在该 UI 节点上方（宠物界面叫两声用），
    /// 走屏幕像素偏移；null 时保持原战斗单位世界坐标跟随逻辑，一行不改。
    /// </summary>
    Transform _uiAnchor;
    /// <summary>固定 UI 锚点模式下气泡底部相对锚点的抬高（屏幕像素）。纵向偏移可留（铁律20）。</summary>
    const float UiAnchorLiftPx = 0f;    // 【2026-11-24】相机统一后锚点回到顶部；【2026-11-25】主人验证太高，96→76→56→36→16→0（16 那步画面无变化，归零验证是否到下限）
    Coroutine _co;
    bool _skipTyping;
    bool _advance;
    bool _typingDone;
    /// <summary>
    /// 【2026-11-24 主人报「每次弹气泡先在下边闪一下，然后才在头顶出来」】
    /// BeginLine / CoPlayLine 开头的 RefreshPopup → UICanvasSetup.Apply → EnsureRootStretch
    /// 会把本 canvas 根 RectTransform 重设成 anchor 铺满 + offset 归零；父级为 null 时
    /// offsetMin/offsetMax 归零会把 sizeDelta 一起写成 0，于是**同一帧** hostRt.rect 是 0×0。
    /// RefreshFollowPosition 的 minX/maxX/minY/maxY 全由 hostRt.rect 算出，0×0 时 clamp
    /// 把气泡压到屏幕下沿，渲染一帧后才跳回头顶 —— 这就是「闪一下」。
    /// 对策：先记 pending，等画布矩形量好、位置真正算完才 SetActive(true) 显形。
    /// </summary>
    bool _pendingShow;
    /// <summary>等画布矩形的帧数兜底：正常 1 帧就好，10 帧后无论如何照常显形，绝不永久不显示。</summary>
    int _pendingShowFrames;
    /// <summary>【2026-11-24 主人拍板】一次性排序层抬升：上阵气泡要盖过队列弹窗（TownPopup 900），
    /// 临时抬到 940；HideNow 播完自动还原默认 899，战斗侧不受影响。</summary>
    int _sortOrderBoostOnce;
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
    public const int BubbleFontSize = 26;
    /// <summary>头顶气泡正文色（深灰，配米色底框）。引导气泡要与「战斗佣兵说话」同款时读这里。</summary>
    public static readonly Color BubbleTextColor = new Color(0.28f, 0.28f, 0.30f, 1f);
    /// <summary>
    /// 气泡锚点离说话人的抬高（世界单位，脚下原点起算）。【2026-10-20 主人「能不能离得近点」】1.55 → 1.35：
    /// 气泡 pivot 在底部中点，锚点越低气泡越贴头；要再近/再远改这一个常量（低于 1.2 会压到头顶）。
    /// </summary>
    const float HeadOffsetY = 1.2f;

    // pending（已请求显示、还没量到画布矩形）也算「正在显示」：否则这一帧 IsShowing=false，
    // 直接 yield CoPlayLine 的调用方（_co 里没有它）就跳不过「先收掉在播的那句」，气泡计数会多压一次。
    public bool IsShowing => _root != null && (_root.gameObject.activeSelf || _pendingShow);
    public string CurrentLine => _text != null ? _text.text : "";

    /// <summary>一次性抬层：只影响本次气泡，HideNow 播完自动还原默认；0 = 不抬（战斗侧不调即零影响）。</summary>
    public void SetSortOrderBoostOnce(int order) { _sortOrderBoostOnce = order; }

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
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.BattleHeadTalk);
        // 【2026-11-24 主人拍板覆盖 11-05「置顶」】气泡不许比佣兵信息等弹窗层级高：
        // 从 Effects/13001 降回 Default/BattleHeadTalk(899)，低于一切弹窗（TownPopup 900 起）。
        canvas.sortingLayerName = GameConfig.UiSort.DefaultLayer;

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
        _text.color = BubbleTextColor;
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
        _uiAnchor = null;   // 战斗单位跟随模式：现有调用行为零改变
        return BeginLine(speaker, content, hold);
    }

    /// <summary>
    /// 固定 UI 锚点模式（2026-11-22 宠物界面「叫两声」）：气泡浮在 anchor 节点上方。
    /// 与 <see cref="PlayLine"/> 共用 _co / 打字机 / 排序层，天然互斥打断，不新增第二套气泡。
    /// </summary>
    public Coroutine PlayLineAt(Transform anchor, string content, float hold = -1f)
    {
        _uiAnchor = anchor;
        return BeginLine(null, content, hold);
    }

    Coroutine BeginLine(UnitBase speaker, string content, float hold)
    {
        if (_co != null)
        {
            StopCoroutine(_co);
            _co = null;
        }
        var canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            UICanvasSetup.RefreshPopup(canvas, GameConfig.UiSort.BattleHeadTalk);
            // RefreshPopup 只改 order 不改 layer，降层后每次补设 Default。
            canvas.sortingLayerName = GameConfig.UiSort.DefaultLayer;
            if (_sortOrderBoostOnce > 0) canvas.sortingOrder = _sortOrderBoostOnce;   // 【2026-11-24】补回一次性抬层
        }
        _co = StartCoroutine(CoPlayLine(speaker, content, hold));
        return _co;
    }

    /// <summary>可被外部直接 yield（固定 UI 锚点版，宠物叫两声串行播放用）。</summary>
    public IEnumerator CoPlayLineAt(Transform anchor, string content, float hold = -1f)
    {
        _uiAnchor = anchor;
        return CoPlayLine(null, content, hold);
    }

    /// <summary>可被外部直接 yield（TutorialDirector 上跑，不二次 StartCoroutine）。</summary>
    public IEnumerator CoPlayLine(UnitBase speaker, string content, float hold = -1f)
    {
        // 【2026-11-24 宠物气泡跑左下根因】CoPlayLineAt 直接 yield 本协程，绕过 BeginLine 里的
        // RefreshPopup → DDOL 气泡 canvas 的 worldCamera 停在旧场景相机；定位却用新相机，
        // ScreenPointToLocalPointInRectangle 算出的 local 落屏外被 clamp 成左下角。
        // 统一在这里按需重绑（worldCamera 不是当前 UI 相机时才刷，见下；不动战斗侧原逻辑）。
        var bubbleCanvas = GetComponent<Canvas>();
        if (bubbleCanvas != null)
        {
            // 【2026-11-24 主人报「每次弹气泡先在下边闪一下」】只在相机真的换了（跨场景 / canvas 重建）
            // 时才 RefreshPopup。RefreshPopup → UICanvasSetup.Apply → EnsureRootStretch 会把本 canvas 根
            // 的 sizeDelta 归零，同一帧 rect 就是 0×0，RefreshFollowPosition 的 clamp 边界全塌到原点附近，
            // 气泡会被压到屏幕下沿渲染一帧。同场景连播时相机没换，不需要重绑。
            var wantCam = UICanvasSetup.ResolveUiCamera();
            if (bubbleCanvas.worldCamera == null || bubbleCanvas.worldCamera != wantCam)
                UICanvasSetup.RefreshPopup(bubbleCanvas, GameConfig.UiSort.BattleHeadTalk);
            bubbleCanvas.sortingLayerName = GameConfig.UiSort.DefaultLayer;
            if (_sortOrderBoostOnce > 0) bubbleCanvas.sortingOrder = _sortOrderBoostOnce;   // 【2026-11-24】相机重绑后补回一次性抬层
        }

        // 【2026-11-11 主人报「佣兵说的话挡住替换装备弹窗了」】弹窗在屏期间新台词一律不出（单一出口闸）；
        // 2026-11-24 气泡已降到 Default/899 不再盖弹窗，此闸保留作双保险（被弹窗压住的台词白播）。
        if (EquipReplaceConfirmUI.IsOpen) yield break;

        // 【2026-11-23 23:3x 主人「剧情气泡很快的过了很多字」】剧情台词必须独占气泡：
        // 词缀提示(MonsterAffixBanter)/佣兵吐槽走 PlayLine→BeginLine→StartCoroutine 存进 _co，
        // 而剧情这条是由外部 yield 进来的（_co 里没有它）→ 旧协程停不掉。两条协程各自跑打字机、
        // 同时往同一个 Text 写 Substring：字互相覆盖，一眨眼过完一大片，旧协程末尾的 HideNow 还会
        // 把剧情气泡提前关掉。这里开头先收掉在播的那条（连它自己的 _co 一起停），再干净播本句。
        // _uiAnchor 是本次调用入参（CoPlayLineAt 已经设好），HideNow 会清空，收完补回。
        if (IsShowing || _co != null)
        {
            var keepAnchor = _uiAnchor;
            int keepBoost = _sortOrderBoostOnce;   // 【2026-11-24】HideNow 会清抬层，本次气泡的要补回
            HideNow();
            _uiAnchor = keepAnchor;
            _sortOrderBoostOnce = keepBoost;
            if (_sortOrderBoostOnce > 0)
            {
                var bcv = GetComponent<Canvas>();
                if (bcv != null) bcv.sortingOrder = _sortOrderBoostOnce;
            }
        }

        _skipTyping = false;
        _advance = false;
        _typingDone = false;
        if (hold < 0f) hold = DefaultHold;

        // 保留人工换行；多余空白压成单空格，方便长句自动折行成多行
        string raw = (content ?? "").Replace("\r\n", "\n").Replace("\r", "\n").Trim();
        while (raw.Contains("  ")) raw = raw.Replace("  ", " ");
        _follow = speaker;

        if (_root != null)
            _root.localScale = Vector3.one;
        // 不在这里 SetActive：等 RefreshFollowPosition 拿到有效画布矩形、把位置算好再显形。
        _pendingShow = true;
        _pendingShowFrames = 0;
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
        if (_root == null) return;
        // 还没显形（_pendingShow）时也要继续算：算好了才由 RevealBubble 打开。
        if (!_root.gameObject.activeSelf && !_pendingShow) return;
        // 用【本 canvas 实际渲染的相机】而不是 ResolveUiCamera() 的猜测值：
        // 两者不一致时 WorldToScreenPoint / ScreenPointToLocalPointInRectangle 用错相机，
        // 锚点被投到屏幕外 → clamp 到左下角（2026-11-24 根因之一）。
        var bubbleCanvas = GetComponent<Canvas>();
        var uiCam = bubbleCanvas != null ? bubbleCanvas.worldCamera : null;
        if (uiCam == null) uiCam = ResolveUiCamera();
        if (uiCam == null) return;

        var hostRt = transform as RectTransform;
        if (hostRt == null) return;

        // 画布根 rect 还没量好（RefreshPopup 刚把它重锚成 0×0）：这一帧不定位、不显形，
        // 否则 clamp 的四个边界全塌在原点附近，气泡会被压到屏幕下沿闪一下。
        if ((hostRt.rect.width < 1f || hostRt.rect.height < 1f)
            && _pendingShow && _pendingShowFrames++ < 10)
            return;

        Vector3 screen;
        if (_uiAnchor != null)
        {
            // 固定 UI 锚点：照 TutorialHintUI.MeasureFollowLocalRect 已验证口径 ——
            // 目标四角世界坐标用【目标所在 canvas 的相机】投影到屏幕，再用【本气泡 canvas 的相机】转回本地。
            // 旧写法拿本气泡 uiCam 直接投影目标 position：宠物弹窗(TownPopup)与气泡canvas不同相机/层时投影错位，
            // 气泡被 clamp 到左下角（主人 11-23 报「狗的气泡不对」）。战斗单位跟随分支一行不动。
            var anchorRt = _uiAnchor as RectTransform;
            // 【2026-11-24 诊断修正】投影与反投影统一用本气泡 canvas 的相机（uiCam）：
            // 锚点在 DDOL 队列弹窗 canvas 里，其 worldCamera 可能停在旧场景相机（该 canvas 每次 Show 才重绑），
            // 用 followCam 投影、uiCam 反投影时两个相机尺度不一致 → 顶部格子被算到画布中部（气泡掉下来）。
            // 两个 canvas 同为 Camera 模式 + 同 referenceResolution，用同一个相机必然闭合。
            Camera followCam = uiCam;
            Vector3 worldTop;
            if (anchorRt != null)
            {
                var corners = new Vector3[4];
                anchorRt.GetWorldCorners(corners);
                worldTop = (corners[1] + corners[2]) * 0.5f;   // 展示区上沿中点（Unity 角序 0左下 1左上 2右上 3右下）
            }
            else worldTop = _uiAnchor.position;
            screen = RectTransformUtility.WorldToScreenPoint(followCam, worldTop);
            screen.y += UiAnchorLiftPx;
            // 【2026-11-25 根因修复】UI 相机就架在 UI 平面上，锚点投影的 z 恒为 ≈0，
            // 但 screen.x/y 是有效坐标。旧代码把 z<=0 当「落相机后方」→ 每次覆盖成屏幕中心固定值，
            // 气泡位置永远不跟随锚点。这里只把 z 归一为 1 供反投影用，x/y 一律保留。
            screen.z = 1f;
        }
        else
        {
            if (_follow == null) { RevealBubble(); return; }   // 原战斗跟随：无单位不定位（行为不变）
            // 宠物体型矮，按自身身高比例给更小抬高（气泡贴狗头）；人型单位沿用全局 HeadOffsetY。
            float offY = (_follow is Pet petTalker) ? petTalker.GetHeadTalkOffsetY() : HeadOffsetY;
            Vector3 world = _follow.transform.position + new Vector3(0f, offY, 0f);
            screen = uiCam.WorldToScreenPoint(world);
            if (screen.z <= 0f)
            {
                screen.z = 1f;
                screen.x = Mathf.Clamp(screen.x, Screen.width * 0.2f, Screen.width * 0.8f);
                screen.y = Screen.height * 0.55f;
            }
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            hostRt, screen, uiCam, out var local);
        float halfW = _root.rect.width * 0.5f;
        float h = _root.rect.height;
        float minX = hostRt.rect.xMin + halfW + 8f;
        float maxX = hostRt.rect.xMax - halfW - 8f;
        float minY = hostRt.rect.yMin + 8f;
        float maxY = hostRt.rect.yMax - h - 8f;
        _root.anchoredPosition = new Vector2(
            Mathf.Clamp(local.x, minX, maxX),
            Mathf.Clamp(local.y, minY, maxY));

        RevealBubble();
    }

    /// <summary>位置算好后显形：pending 期间不显示，避免用塌掉的画布矩形把气泡定到屏幕下沿。</summary>
    void RevealBubble()
    {
        if (!_pendingShow) return;
        _pendingShow = false;
        _pendingShowFrames = 0;
        if (_root != null && !_root.gameObject.activeSelf)
            _root.gameObject.SetActive(true);
    }

    public void HideNow()
    {
        _pendingShow = false;
        _pendingShowFrames = 0;
        _skipTyping = false;
        _advance = false;
        _typingDone = false;
        if (_co != null)
        {
            StopCoroutine(_co);
            _co = null;
        }
        _follow = null;
        _uiAnchor = null;
        // 【2026-11-24】一次性抬层随本次气泡结束还原默认，战斗气泡层级不受影响
        _sortOrderBoostOnce = 0;
        var cv = GetComponent<Canvas>();
        if (cv != null) cv.sortingOrder = GameConfig.UiSort.BattleHeadTalk;
        if (_group != null) _group.alpha = 1f;
        if (_root != null)
        {
            SpeechBubbleFit.ResetSize(_root, BaseBubbleSize);
            _root.gameObject.SetActive(false);
        }
        GlobalToastUI.PopBubble();
    }
}
