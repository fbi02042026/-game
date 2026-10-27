using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 下一关随机滚动弹窗。
///
/// 滚动节奏（2026-10-11 主人拍板改）：
/// 1) 开场即最快转速滚 5 秒，玩家可点「停止」；5 秒到了自动点（不再慢→快，太费时间）
/// 2) 点停之后由快到慢，最后停在中间格子 —— 那就是当前要打的关
/// 3) 停稳后才出现「进入关卡」，没有返回按钮、没有星级、没有关卡编号
///
/// 预制体：Resources/Prefabs/Battle/NextStageRoulette.prefab
/// 用 Tools/UI/生成随机滚动关卡弹窗 生成后，可在 Inspector 换美术。
/// </summary>
public class NextStageRouletteUI : MonoBehaviour
{
    public static NextStageRouletteUI Instance { get; private set; }

    public const string PrefabResourcePath = "Prefabs/Battle/NextStageRoulette";

    const float CardW = 190f;
    const float CardH = 226f;
    const float CardGap = 20f;
    const float Step = CardW + CardGap;
    const int LoopCardCount = 14;

    /// <summary>【2026-10-11 主人拍板】全速滚动窗口时长：面板一打开就是最快转速，不要慢→快（主人原话「刚打开的时候就是转的最快的时候 不要从慢再到快再停 这样太费时间了」）。窗口内可点「停止」，到点自动停。</summary>
    const float AccelSeconds = 5f;
    /// <summary>减速阶段时长：快→慢→停</summary>
    const float DecelSeconds = 2.4f;
    /// <summary>2026-10-11 起转速恒为 SpeedMax，SpeedMin 已无调用（死常量，按铁律只报告不删）。</summary>
    const float SpeedMin = 180f;
    const float SpeedMax = 1400f;
    /// <summary>减速至少再滚过这么多格，避免一停就刹住</summary>
    const int MinDecelCards = 10;
    /// <summary>【2026-10-20 主人拍板】随到恢复关：停稳后到自动弹恢复窗的停顿，让「命运已定」的结果看清一拍</summary>
    const float RestAutoEnterDelay = 1f;

    [Header("可绑预制体节点（空则代码查找）")]
    public RectTransform viewport;
    public RectTransform content;
    public RectTransform centerFrame;
    public Text titleText;
    public Text subTitleText;
    public Text resultNameText;
    public Text resultDescText;
    public Button stopButton;
    public Button enterButton;
    public Image backdrop;
    public Image shade;

    RectTransform _viewport;
    RectTransform _content;
    Text _resultName;
    Text _resultDesc;
    GameObject _stopGo;
    GameObject _enterGo;
    Button _stopBtn;

    StageData _stage;
    int _chapter;
    Action<StageData> _onEnter;
    Coroutine _flow;
    float _prevTimeScale = 1f;

    readonly List<RectTransform> _cards = new List<RectTransform>();
    readonly List<StageType> _cardTypes = new List<StageType>();
    int _winnerSlot;
    float _offset;
    bool _stopRequested;

    public bool IsResultReady =>
        enterButton != null && enterButton.gameObject.activeSelf;

    public string ResultLabel =>
        resultNameText != null ? resultNameText.text : (_resultName != null ? _resultName.text : "");

    public string SubTitle => subTitleText != null ? subTitleText.text : "";

    /// <summary>打开轮盘。stage.type 必须已由 StageRoller 抽好。</summary>
    public static void Show(int chapter, StageData stage, Action<StageData> onEnter)
    {
        if (stage == null)
        {
            Debug.LogWarning("[NextStageRoulette] 没有下一关数据，直接跳过");
            onEnter?.Invoke(null);
            return;
        }

        if (Instance != null)
            Destroy(Instance.gameObject);

        NextStageRouletteUI ui = null;
        var prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab != null)
        {
            var go = UnityEngine.Object.Instantiate(prefab);
            go.name = "NextStageRoulette";
            ui = go.GetComponent<NextStageRouletteUI>();
            if (ui == null) ui = go.AddComponent<NextStageRouletteUI>();
            if (go.GetComponent<Canvas>() == null)
            {
                var canvas = go.AddComponent<Canvas>();
                UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownPopup);
            }
            else
                UICanvasSetup.ApplyPopup(go.GetComponent<Canvas>(), GameConfig.UiSort.TownPopup);
            if (go.GetComponent<GraphicRaycaster>() == null)
                go.AddComponent<GraphicRaycaster>();
        }
        else
        {
            var go = new GameObject("NextStageRoulette", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownPopup);
            go.AddComponent<GraphicRaycaster>();
            ui = go.AddComponent<NextStageRouletteUI>();
            BuildHierarchy(go);
        }

        EnsureEventSystem();
        ui.Open(chapter, stage, onEnter);
    }

    /// <summary>
    /// 搭好整棵 UI 树（运行时兜底 + Editor 生成预制体共用）。
    /// 节点名固定，方便你在 Inspector 换图。
    /// </summary>
    public static void BuildHierarchy(GameObject host)
    {
        if (host == null) return;
        var root = host.GetComponent<RectTransform>();
        if (root == null) root = host.AddComponent<RectTransform>();

        // 清掉旧子节点（重新生成时）
        for (int i = host.transform.childCount - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(host.transform.GetChild(i).gameObject);

        var bg = NewImage(root, "Backdrop", new Color(0.06f, 0.05f, 0.09f, 1f));
        Stretch(bg.rectTransform);
        Sprite bgSprite = LoadBackground();
        if (bgSprite != null)
        {
            bg.sprite = bgSprite;
            bg.color = Color.white;
            bg.preserveAspect = false;
        }

        var shade = NewImage(root, "Shade", new Color(0f, 0f, 0f, bgSprite != null ? 0.45f : 0.25f));
        Stretch(shade.rectTransform);

        var title = NewText(root, "Title", "第X章", 38, TextAnchor.MiddleCenter);
        var trt = title.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -110f);
        trt.sizeDelta = new Vector2(600f, 56f);

        var sub = NewText(root, "SubTitle", "点击停止，或等待自动停下", 24, TextAnchor.MiddleCenter);
        sub.color = new Color(0.82f, 0.76f, 0.62f);
        var srt = sub.rectTransform;
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 1f);
        srt.pivot = new Vector2(0.5f, 1f);
        srt.anchoredPosition = new Vector2(0f, -168f);
        srt.sizeDelta = new Vector2(600f, 34f);

        var viewportImg = NewImage(root, "ReelViewport", new Color(0.04f, 0.03f, 0.06f, 0.72f));
        var viewport = viewportImg.rectTransform;
        viewport.anchorMin = viewport.anchorMax = new Vector2(0.5f, 0.5f);
        viewport.sizeDelta = new Vector2(660f, CardH + 24f);
        viewport.anchoredPosition = new Vector2(0f, 60f);
        viewportImg.gameObject.AddComponent<RectMask2D>();

        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(viewport, false);
        var content = contentGo.GetComponent<RectTransform>();
        content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
        content.pivot = new Vector2(0.5f, 0.5f);
        content.sizeDelta = Vector2.zero;
        content.anchoredPosition = Vector2.zero;

        // 卡片占位：生成预制体时放几张样例，运行时会清掉重填
        for (int i = 0; i < 5; i++)
            BuildCardVisual(content, StageType.Normal, i);

        BuildCenterFrame(root, viewport.anchoredPosition);

        var resultName = NewText(root, "ResultName", string.Empty, 34, TextAnchor.MiddleCenter);
        var nrt = resultName.rectTransform;
        nrt.anchorMin = nrt.anchorMax = new Vector2(0.5f, 0.5f);
        nrt.anchoredPosition = new Vector2(0f, -110f);
        nrt.sizeDelta = new Vector2(620f, 46f);

        var resultDesc = NewText(root, "ResultDesc", string.Empty, 24, TextAnchor.MiddleCenter);
        resultDesc.color = new Color(0.85f, 0.8f, 0.68f);
        var drt = resultDesc.rectTransform;
        drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0.5f);
        drt.anchoredPosition = new Vector2(0f, -156f);
        drt.sizeDelta = new Vector2(620f, 40f);

        // 停止按钮（加速阶段）
        var stopImg = NewImage(root, "StopButton", new Color(0.55f, 0.22f, 0.18f, 1f));
        stopImg.raycastTarget = true;
        var stopRt = stopImg.rectTransform;
        stopRt.anchorMin = stopRt.anchorMax = new Vector2(0.5f, 0f);
        stopRt.pivot = new Vector2(0.5f, 0f);
        stopRt.sizeDelta = new Vector2(360f, 84f);
        stopRt.anchoredPosition = new Vector2(0f, 150f);
        var stopBtn = stopImg.gameObject.AddComponent<Button>();
        stopBtn.targetGraphic = stopImg;
        var stopLabel = NewText(stopRt, "Label", "停止", 32, TextAnchor.MiddleCenter);
        Stretch(stopLabel.rectTransform);

        // 进入按钮（停稳后）
        var enterImg = NewImage(root, "EnterButton", new Color(0.52f, 0.36f, 0.16f, 1f));
        enterImg.raycastTarget = true;
        var enterRt = enterImg.rectTransform;
        enterRt.anchorMin = enterRt.anchorMax = new Vector2(0.5f, 0f);
        enterRt.pivot = new Vector2(0.5f, 0f);
        enterRt.sizeDelta = new Vector2(360f, 84f);
        enterRt.anchoredPosition = new Vector2(0f, 150f);
        var enterBtn = enterImg.gameObject.AddComponent<Button>();
        enterBtn.targetGraphic = enterImg;
        var enterLabel = NewText(enterRt, "Label", "进入关卡", 32, TextAnchor.MiddleCenter);
        Stretch(enterLabel.rectTransform);
        enterImg.gameObject.SetActive(false);

        var ui = host.GetComponent<NextStageRouletteUI>();
        if (ui == null) ui = host.AddComponent<NextStageRouletteUI>();
        ui.backdrop = bg;
        ui.shade = shade;
        ui.titleText = title;
        ui.subTitleText = sub;
        ui.viewport = viewport;
        ui.content = content;
        ui.resultNameText = resultName;
        ui.resultDescText = resultDesc;
        ui.stopButton = stopBtn;
        ui.enterButton = enterBtn;
        ui.centerFrame = root.Find("CenterFrame") as RectTransform;

        GameFonts.ApplyToHierarchy(host.transform);
    }

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_flow != null) StopCoroutine(_flow);
    }

    void Open(int chapter, StageData stage, Action<StageData> onEnter)
    {
        _chapter = chapter;
        _stage = stage;
        _onEnter = onEnter;
        _stopRequested = false;
        _offset = 0f;

        BindRefs();
        if (titleText != null)
            titleText.text = GameConfig.GetChapterMapName(Mathf.Max(1, chapter));
        if (subTitleText != null)
            subTitleText.text = "滚动中…点停止或等 5 秒";

        FillReel();
        LayoutCards();

        if (_stopGo != null) _stopGo.SetActive(true);
        if (_enterGo != null) _enterGo.SetActive(false);
        if (_resultName != null) _resultName.text = string.Empty;
        if (_resultDesc != null) _resultDesc.text = string.Empty;

        if (_stopBtn != null)
        {
            _stopBtn.onClick.RemoveAllListeners();
            _stopBtn.onClick.AddListener(OnStopClicked);
        }
        if (enterButton != null)
        {
            enterButton.onClick.RemoveAllListeners();
            enterButton.onClick.AddListener(OnEnterClicked);
        }

        GameFonts.ApplyToHierarchy(transform);

        _prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        _flow = StartCoroutine(SpinFlow());
    }

    void BindRefs()
    {
        if (viewport == null)
        {
            var t = transform.Find("ReelViewport");
            if (t != null) viewport = t as RectTransform;
        }
        if (content == null && viewport != null)
        {
            var t = viewport.Find("Content");
            if (t != null) content = t as RectTransform;
        }
        if (titleText == null)
        {
            var t = transform.Find("Title");
            if (t != null) titleText = t.GetComponent<Text>();
        }
        if (subTitleText == null)
        {
            var t = transform.Find("SubTitle");
            if (t != null) subTitleText = t.GetComponent<Text>();
        }
        if (resultNameText == null)
        {
            var t = transform.Find("ResultName");
            if (t != null) resultNameText = t.GetComponent<Text>();
        }
        if (resultDescText == null)
        {
            var t = transform.Find("ResultDesc");
            if (t != null) resultDescText = t.GetComponent<Text>();
        }
        if (stopButton == null)
        {
            var t = transform.Find("StopButton");
            if (t != null) stopButton = t.GetComponent<Button>();
        }
        if (enterButton == null)
        {
            var t = transform.Find("EnterButton");
            if (t != null) enterButton = t.GetComponent<Button>();
        }

        _viewport = viewport;
        _content = content;
        _resultName = resultNameText;
        _resultDesc = resultDescText;
        _stopBtn = stopButton;
        _stopGo = stopButton != null ? stopButton.gameObject : null;
        _enterGo = enterButton != null ? enterButton.gameObject : null;
    }

    void FillReel()
    {
        _cards.Clear();
        _cardTypes.Clear();
        if (_content == null) return;

        for (int i = _content.childCount - 1; i >= 0; i--)
            Destroy(_content.GetChild(i).gameObject);

        var state = ChapterManager.Instance?.RollState;
        List<StageType> pool = StageRoller.BuildReel(state, _stage.stageIndex,
            GameConfig.STAGES_PER_GRAND, _stage.type, LoopCardCount);

        // 保证环里至少有一张赢家，并记下下标
        _winnerSlot = -1;
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] == _stage.type) { _winnerSlot = i; break; }
        }
        if (_winnerSlot < 0)
        {
            _winnerSlot = pool.Count / 2;
            pool[_winnerSlot] = _stage.type;
        }

        for (int i = 0; i < pool.Count; i++)
        {
            var card = BuildCardVisual(_content, pool[i], i);
            _cards.Add(card);
            _cardTypes.Add(pool[i]);
        }
    }

    IEnumerator SpinFlow()
    {
        // 【2026-10-11 主人拍板】阶段1：**开场即最快转速**，窗口内可手动停 ——
        // 旧写法 SpeedMin→SpeedMax 慢慢爬（ease 二次曲线），主人原话「刚打开的时候就是转的最快的时候，
        // 不要从慢再到快再停 这样太费时间了」。加速段整段删除，转速恒为 SpeedMax。
        float runT = 0f;
        while (!_stopRequested && runT < AccelSeconds)
        {
            // 【2026-10-11 主人「正在停下还倒着转了」】unscaledDeltaTime 不受 maximumDeltaTime 封顶
            // （编辑器暂停恢复 / 掉帧的那一帧会给"整段暂停时长"）→ 阶段2 的 Lerp 一次性跳老远，
            // 看起来就是卡片猛地倒着蹿。这里把每帧 dt 钳到 ≤0.05s，两段同口径。
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            runT += dt;
            _offset += SpeedMax * dt;
            LayoutCards();
            PulseNearCenter();
            if (subTitleText != null)
                subTitleText.text = $"滚动中… {Mathf.CeilToInt(Mathf.Max(0f, AccelSeconds - runT))} 秒后自动停止";
            yield return null;
        }

        if (!_stopRequested)
            _stopRequested = true; // 5 秒到了视作自动点停

        if (_stopGo != null) _stopGo.SetActive(false);
        if (subTitleText != null)
            subTitleText.text = "正在停下…";

        // —— 阶段2：快→慢，精确停到中间赢家格 ——
        float cycle = Mathf.Max(Step, _cards.Count * Step);
        float curSlot = _offset / Step;
        int curFloor = Mathf.FloorToInt(curSlot);
        int n = Mathf.Max(1, _cards.Count);
        int mod = ((curFloor % n) + n) % n;
        int delta = (_winnerSlot - mod + n) % n;
        while (delta < MinDecelCards) delta += n;
        float targetOffset = (curFloor + delta) * Step;
        // 对齐到赢家格中心（winner 的理论位置）
        // offset ≡ winnerSlot * Step (mod cycle) 时，赢家卡 x≈0
        float aligned = targetOffset;
        // 微调：保证精确落在格子中心
        float frac = (aligned / Step) - Mathf.Floor(aligned / Step);
        if (frac > 0.001f) aligned += (1f - frac) * Step;

        float from = _offset;
        float to = aligned;
        // 【2026-10-11 主人「正在停下还倒着转了」】终点必须严格在前方：只许往前收，绝不允许倒退。
        // 旧写法 `if (to - from < MinDecelCards*Step) to += n*Step` 只补一次，边界上仍可能只前进半格不到；
        // 改成 while 一次补够，保证 to - from ≥ MinDecelCards*Step，Lerp 全程单调正转。
        while (to - from < MinDecelCards * Step)
            to += n * Step;

        float decelT = 0f;
        int lastPulse = -1;
        while (decelT < DecelSeconds)
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            decelT += dt;
            float u = Mathf.Clamp01(decelT / DecelSeconds);
            // 快→慢：前快后慢的 ease-out cubic
            float e = 1f - Mathf.Pow(1f - u, 3f);
            _offset = Mathf.Lerp(from, to, e);
            LayoutCards();

            int slot = Mathf.RoundToInt(_offset / Step);
            if (slot != lastPulse)
            {
                lastPulse = slot;
                PulseNearCenter();
            }
            yield return null;
        }

        _offset = to;
        LayoutCards();
        RevealResult();
        _flow = null;
    }

    void LayoutCards()
    {
        if (_cards.Count == 0) return;
        float cycle = _cards.Count * Step;
        float o = _offset % cycle;
        if (o < 0f) o += cycle;

        for (int i = 0; i < _cards.Count; i++)
        {
            float x = i * Step - o;
            // 把卡片 wrap 到视口附近，避免整环跑远
            while (x < -cycle * 0.5f) x += cycle;
            while (x >= cycle * 0.5f) x -= cycle;
            _cards[i].anchoredPosition = new Vector2(x, 0f);
            _cards[i].localScale = Vector3.one;
        }
    }

    void PulseNearCenter()
    {
        int nearest = -1;
        float best = float.MaxValue;
        for (int i = 0; i < _cards.Count; i++)
        {
            float ax = Mathf.Abs(_cards[i].anchoredPosition.x);
            if (ax < best) { best = ax; nearest = i; }
        }
        for (int i = 0; i < _cards.Count; i++)
            _cards[i].localScale = (i == nearest) ? Vector3.one * 1.06f : Vector3.one;
    }

    void OnStopClicked()
    {
        if (_stopRequested) return;
        _stopRequested = true;
    }

    void RevealResult()
    {
        // 把赢家卡钉在正中并放大一下
        for (int i = 0; i < _cards.Count; i++)
        {
            if (Mathf.Abs(_cards[i].anchoredPosition.x) < Step * 0.35f)
            {
                _cards[i].anchoredPosition = new Vector2(0f, 0f);
                _cards[i].localScale = Vector3.one * 1.08f;
            }
            else
                _cards[i].localScale = Vector3.one;
        }

        if (_resultName != null)
        {
            _resultName.text = StageRoller.Label(_stage.type);
            _resultName.color = Color.Lerp(StageRoller.Tint(_stage.type), Color.white, 0.55f);
        }
        if (_resultDesc != null)
            _resultDesc.text = StageRoller.Desc(_stage.type, _stage.stageIndex, _chapter);
        if (subTitleText != null)
            subTitleText.text = "命运已定";
        if (_stopGo != null) _stopGo.SetActive(false);

        // 【2026-10-20 主人拍板】恢复关：随到立即弹恢复窗，不走「进入关卡」按钮——
        // 停稳稍停一拍（让结果被看清）后自动交还关卡数据，
        // 关卡图收到回调直接弹「生命恢复」窗，全程不进战斗。
        if (_stage.type == StageType.Rest)
        {
            StartCoroutine(CoAutoEnterForRest());
            StartCoroutine(PopCenterCard());
            return;
        }

        if (_enterGo != null) _enterGo.SetActive(true);

        StartCoroutine(PopCenterCard());
    }

    IEnumerator PopCenterCard()
    {
        RectTransform center = null;
        for (int i = 0; i < _cards.Count; i++)
        {
            if (Mathf.Abs(_cards[i].anchoredPosition.x) < 1f)
            {
                center = _cards[i];
                break;
            }
        }
        if (center == null) yield break;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / 0.28f;
            float s = 1.05f + Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 0.12f;
            center.localScale = Vector3.one * s;
            yield return null;
        }
        center.localScale = Vector3.one * 1.08f;
    }

    void OnEnterClicked()
    {
        // 还在滚就当点停止
        if (_flow != null && !_stopRequested)
        {
            _stopRequested = true;
            return;
        }
        if (_flow != null) return; // 减速中不允许进关，等停稳

        FinishAndReturnStage();
    }

    /// <summary>
    /// 还原时间流速 → 自毁 → 回调交还关卡数据（「进入关卡」按钮与恢复关自动进入共用出口）。
    /// 2026-10-11：原样恢复进入前的时间流速，不再「≤0 就当成 1」。
    /// 老虎机是被 BattleStageMapUI（关卡图）调起的，而关卡图一开就把 Time.timeScale 压到 0
    /// 冻住底下的战斗场景；这里若擅自恢复成 1，玩家在关卡图上看旗子的那几秒
    /// 战斗场景又跑起来了。时间该由**把它压住的那个人**还原 ——
    /// 关卡图会在点石墩时（OnPedestalClicked）把 _prevTimeScale 还原回去。
    /// </summary>
    void FinishAndReturnStage()
    {
        // 【2026-10-20 保险】本面板只被关卡图调起，_prevTimeScale 正常只会是关卡图压下的 0（原样还原，
        // 2026-10-11 口径不变）。这里只收口一种时序交错：混进了受击顿帧的瞬态 0.01（没人会替它还原，
        // 还原回去 = 全场永久 1% 速僵直）→ 收敛成 1。0 与正常值一律原样还原。
        Time.timeScale = (Mathf.Approximately(_prevTimeScale, 0f) || _prevTimeScale > 0.015f)
            ? _prevTimeScale
            : 1f;
        var stage = _stage;
        var cb = _onEnter;
        _onEnter = null;
        Destroy(gameObject);
        cb?.Invoke(stage);
    }

    /// <summary>恢复关专用（2026-10-20 主人拍板）：停稳后稍停一拍自动「进入」，不弹按钮不等人。</summary>
    IEnumerator CoAutoEnterForRest()
    {
        yield return new WaitForSecondsRealtime(RestAutoEnterDelay);
        FinishAndReturnStage();
    }

    static RectTransform BuildCardVisual(Transform parent, StageType type, int index)
    {
        Color tint = StageRoller.Tint(type);
        var card = NewImage(parent, $"Card_{index}", tint * new Color(1f, 1f, 1f, 0.95f));
        var rt = card.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(CardW, CardH);
        rt.anchoredPosition = new Vector2(index * Step, 0f);

        var inner = NewImage(rt, "Inner", tint * 1.35f);
        Stretch(inner.rectTransform);
        inner.rectTransform.offsetMin = new Vector2(6f, 6f);
        inner.rectTransform.offsetMax = new Vector2(-6f, -6f);

        // 图标占位：有图用图，没有就空着，方便你在预制体里拖 Sprite
        var iconImg = NewImage(rt, "Icon", Color.white);
        iconImg.raycastTarget = false;
        Sprite icon = LoadStageIcon(type);
        if (icon != null)
        {
            iconImg.sprite = icon;
            iconImg.preserveAspect = true;
            iconImg.color = Color.white;
        }
        else
            iconImg.color = new Color(1f, 1f, 1f, 0.15f);
        var iconRt = iconImg.rectTransform;
        iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
        iconRt.sizeDelta = new Vector2(110f, 110f);
        iconRt.anchoredPosition = new Vector2(0f, 22f);

        var label = NewText(rt, "Name", StageRoller.Label(type), 28, TextAnchor.MiddleCenter);
        var lrt = label.rectTransform;
        lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0f);
        lrt.pivot = new Vector2(0.5f, 0f);
        lrt.anchoredPosition = new Vector2(0f, 22f);
        lrt.sizeDelta = new Vector2(CardW - 16f, 40f);

        // 类型标记，方便美术按类型换图
        var typeMark = NewText(rt, "TypeTag", type.ToString(), 14, TextAnchor.UpperRight);
        typeMark.color = new Color(1f, 1f, 1f, 0.35f);
        var trt = typeMark.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(1f, 1f);
        trt.anchoredPosition = new Vector2(-8f, -6f);
        trt.sizeDelta = new Vector2(100f, 24f);

        return rt;
    }

    static void BuildCenterFrame(RectTransform root, Vector2 viewportPos)
    {
        var frame = new GameObject("CenterFrame", typeof(RectTransform));
        frame.transform.SetParent(root, false);
        var frt = frame.GetComponent<RectTransform>();
        frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0.5f);
        frt.sizeDelta = new Vector2(CardW + 18f, CardH + 18f);
        frt.anchoredPosition = viewportPos;

        Color line = new Color(1f, 0.86f, 0.42f, 1f);
        const float th = 5f;
        AddEdge(frt, "Top", new Vector2(0.5f, 1f), new Vector2(frt.sizeDelta.x, th), line);
        AddEdge(frt, "Bottom", new Vector2(0.5f, 0f), new Vector2(frt.sizeDelta.x, th), line);
        AddEdge(frt, "Left", new Vector2(0f, 0.5f), new Vector2(th, frt.sizeDelta.y), line);
        AddEdge(frt, "Right", new Vector2(1f, 0.5f), new Vector2(th, frt.sizeDelta.y), line);
    }

    static void AddEdge(RectTransform parent, string name, Vector2 anchor, Vector2 size, Color c)
    {
        var img = NewImage(parent, name, c);
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
    }

    /// <summary>
    /// 【2026-10-11 主人「老虎机没有背景，背景应该是关卡界面」】
    /// 背景真源 = Resources/Art/UI/关卡/层级调整组件_0007_背景.png，与关卡图（BattleStageMapUI）同一张。
    /// 旧的三条 StageSelect 路径全库都没有对应资源，只会一路取到 null → 老虎机是纯色底，删掉不留第二入口。
    /// </summary>
    static Sprite LoadBackground()
        => Resources.Load<Sprite>("UI/Art/关卡/层级调整组件_0007_背景");

    /// <summary>
    /// 【2026-10-11 主人「老虎机没有关卡类型图标」】
    /// 类型图标真源 = Resources/Art/UI/关卡/stage_*.png，与关卡图同一套、同一个出口。
    /// </summary>
    static Sprite LoadStageIcon(StageType type)
        => Resources.Load<Sprite>($"UI/Art/关卡/{StageRoller.IconName(type)}");

    static void EnsureEventSystem()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null) return;
        var go = new GameObject("EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.EventSystems.StandaloneInputModule));
        go.hideFlags = HideFlags.DontSave;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Image NewImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static Text NewText(Transform parent, string name, string content, int size, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = GameFonts.GetChinese();
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }
}
