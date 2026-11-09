using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【2026-11-02 主人拍板】toast 口吻分类：以前所有屏幕浮字都统一接一句傲娇尾巴，
/// 结果「奖励发放失败，请稍后重试」后面也挂「才、才不是关心你呢！」，功能提示全变了味。
/// 现在按类走：默认 <see cref="ToastTone.Plain"/>（原文照发），
/// 只有「看板娘主动说话」的引导 / 闲聊文案才显式标 <see cref="ToastTone.Persona"/>。
/// </summary>
public enum ToastTone
{
    /// <summary>系统 / 奖励 / 错误 / 操作指引：一个字都不加工（默认）。</summary>
    Plain = 0,
    /// <summary>看板娘口吻：句尾可以接一句傲娇短语（只给引导 / 闲聊）。</summary>
    Persona = 1,
}

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
    Canvas _canvas;

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

    public static void Show(string msg) => Show(msg, false, ToastTone.Plain);

    /// <summary>
    /// 【2026-10-06 主人拍板】物品变动（获得 / 分解 / 丢弃）必须让玩家看到 ——
    /// 哪怕屏幕上正有对话 / 引导遮罩 / 气泡在显示，也要弹出来，不能被 <see cref="AnyBubbleShowing"/> 吞掉。
    /// 走 <paramref name="force"/> = true 这一条；普通提示（「暂时无法前往」之类）仍走默认压制，避免盖住对话。
    /// 出口仍是这一个 <see cref="Show"/>，不另起一套。
    /// </summary>
    public static void Show(string msg, bool force) => Show(msg, force, ToastTone.Plain);

    /// <summary>看板娘口吻 toast（句尾可能接一句傲娇短语）：只给引导 / 闲聊文案用。</summary>
    public static void ShowPersona(string msg) => Show(msg, false, ToastTone.Persona);
    public static void ShowPersona(string msg, bool force) => Show(msg, force, ToastTone.Persona);

    /// <summary>唯一实现：按 <paramref name="tone"/> 决定要不要套人设，其余行为与以前一致。</summary>
    public static void Show(string msg, bool force, ToastTone tone)
    {
        if (string.IsNullOrEmpty(msg)) return;
        if (tone == ToastTone.Persona) msg = Persona(msg);
        // 直接调 GlobalToastUI 的路径也要挡气泡 —— 除非 force（物品变动）
        if (!force && AnyBubbleShowing) return;
        // 【2026-11-01 修「loading 界面弹任务完成」】Loading 遮罩显示期间先把 toast 攥住，
        // 遮罩收掉（BattleLoadingOverlay.Hide）时再放出来——日常任务结算（QuestBarBinder 轮询）
        // 一进城就跑，toast 会弹在还没退场的 Loading 上面。只挡显示时机，结算/发奖照旧。
        if (BattleLoadingOverlay.IsShowing)
        {
            _heldMsg = msg;      // 同 CoPlay「后一条顶掉前一条」的既有语义，只留最后一条
            _heldForce = force;
            return;
        }
        Ensure().Play(msg);
    }

    // ============ 【2026-11-06 主人拍板】任务奖励汇总 toast 排队播报 ============
    // 连续多条任务同拍结算 → 排队逐条播：一条向上飘走淡出后，下一条从原位淡入；不许后顶前。
    // 普通 Show / ShowPersona / Hide 的「后顶前」语义一行未改，队列只服务于任务奖励汇总。
    static readonly Queue<string> _queued = new Queue<string>();
    const int QUEUE_MAX = 8;   // 防刷屏：等待队列封顶

    /// <summary>
    /// 排队播报一条汇总 toast（任务奖励明细专用）。固定走实例 Play：Plain 原文照发、
    /// 不套 Persona 尾巴、不被 AnyBubbleShowing 压制、不套 Show 的 Loading 攥住逻辑，重绑相机也不丢。
    /// 空闲（_co == null）时立即起播，否则只入队；CoPlay 收尾会自动接播下一条。
    /// 已知边界：普通 toast 打断队列播报时，被顶掉的只是「正在播的那条」（后顶前语义不变），
    /// 队列里剩余未播条目会在那条普通 toast 的 CoPlay 结束后接着播，信息不丢。
    /// </summary>
    public static void ShowQueued(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return;
        if (_queued.Count >= QUEUE_MAX) return;   // 队列满：丢弃后来的，不顶掉正在播的
        _queued.Enqueue(msg);
        if (_instance == null || _instance._co == null)
            PlayNextQueued();
    }

    /// <summary>出队一条走实例 Play；队列空则什么也不做。ShowQueued 起播与 CoPlay 收尾共用。</summary>
    static void PlayNextQueued()
    {
        if (_queued.Count == 0) return;
        Ensure().Play(_queued.Dequeue());
    }

    /// <summary>攥住的 toast（Loading 期间来的），等遮罩收掉后冲刷。</summary>
    static string _heldMsg;
    static bool _heldForce;

    /// <summary>【2026-11-01】域重载关闭时静态字段会跨 Play 残留（同 BattleLoadingOverlay.ResetStatics 口径），启动时清掉，防上一局的攥留 toast 开局冒出来。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetHeldToast()
    {
        _heldMsg = null;
        _heldForce = false;
    }

    /// <summary>
    /// Loading 遮罩真正收掉时由 <see cref="BattleLoadingOverlay.Hide"/> 调：把攥住的 toast 放出来。
    /// 若此刻又有新的一场 Loading 开着（连续切场景），继续攥着等下一次收。
    /// </summary>
    /// <summary>【2026-11-06】进程级静态队列跨 Play 残留，与 _heldMsg 同口径：启动时清掉。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetQueuedToast()
    {
        _queued.Clear();
    }

    public static void FlushHeldToast()
    {
        if (string.IsNullOrEmpty(_heldMsg)) return;
        var msg = _heldMsg;
        var force = _heldForce;
        _heldMsg = null;
        _heldForce = false;
        if (BattleLoadingOverlay.IsShowing) return;
        Show(msg, force);
    }

    /// <summary>
    /// 【2026-10-31 主人拍板】看板娘人设＝前台小姐（口吻傲娇）。
    /// 【2026-11-02 主人拍板·分类】只在 <see cref="ToastTone.Persona"/> 的文案上生效 ——
    /// 系统 / 奖励 / 错误类提示一律走 Plain，原文照发，不再自动接尾巴。
    /// 文案里已带口吻字（啦/～/哦/嘛/哼/呢/呀）的视为已按人设写好，不再叠尾巴防重复。
    /// 战斗飞字（<see cref="ShowFlythrough"/>）不走本包装。
    /// </summary>
    static string Persona(string msg)
    {
        const string marks = "啦～哦嘛哼呢呀";
        for (int i = 0; i < marks.Length; i++)
            if (msg.IndexOf(marks[i]) >= 0) return msg;
        string[] tails =
        {
            "哼、才不是特意提醒你呢！",
            "记住了哦？",
            "别让人家说第二遍！",
            "……才、才不是关心你呢！",
            "有我在就放心吧～",
            "别大意哦～",
        };
        // Persona 文案若没以标点收尾（如「暂未开放」），直接怼尾巴会连成一串，补个停顿符
        if (msg.Length > 0 && "。！？～…".IndexOf(msg[msg.Length - 1]) < 0) msg += "。";
        return msg + tails[Random.Range(0, tails.Length)];
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
        _canvas = canvas;
        // 【2026-11-07 真机黑屏根因修复】不新建任何相机。
        // 原先那台独立 ToastCamera（depth=110 / cullingMask=UI / clearFlags=Nothing）
        // 在跨 Activity 返回后会把整屏抹成纯黑只剩粒子，切后台、改分辨率、进主城都不自愈，
        // 编辑器与模拟器不复现。删掉它、toast 走统一出口绑通用 UI 相机即可消除。
        // 当前 toast 绑的是会震的主相机，打击震屏时会跟着抖。
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.Toast);
        gameObject.AddComponent<GraphicRaycaster>().enabled = false;

        _root = new GameObject("Root", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        _root.transform.SetParent(transform, false);
        _rootRt = _root.GetComponent<RectTransform>();
        _rootRt.anchorMin = new Vector2(0.5f, 0.55f);
        _rootRt.anchorMax = new Vector2(0.5f, 0.55f);
        _rootRt.pivot = new Vector2(0.5f, 0.5f);
        _rootRt.sizeDelta = new Vector2(560f, 72f);
        _basePos = new Vector2(0f, 20f);
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
            UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.Toast);
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoPlay(msg));
    }

    void PlayFlythrough(string msg)
    {
        if (_label == null || _root == null) Build();
        var canvas = GetComponent<Canvas>();
        if (canvas != null)
            UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.Toast);
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoFlythrough(msg));
    }

    IEnumerator CoPlay(string msg)
    {
        _label.text = msg;
        // 【2026-10-18 修「登录欢迎语换行别扭」】底板原固定 72 高，两行文案会上下溢出底板。
        // 宽度不变，按文字实际高度自动加高底板；单行提示 preferredHeight+16 不足 72，维持原样。
        if (_rootRt != null)
        {
            var sd = _rootRt.sizeDelta;
            // 钳到 400 封顶：防首帧画布矩形未就绪时 preferredHeight 算出天量值
            sd.y = Mathf.Clamp(_label.preferredHeight + 16f, 72f, 400f);
            _rootRt.sizeDelta = sd;
        }
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

        // 【2026-10-08 主人拍板】战斗内提示字（「获得 XXX」/「抽中 XXX」…）一闪而过看不清
        // → **停留时长 ×2**（1s → 2s）。淡入 / 淡出是过场动画，保持原速，只把「看得见的时间」翻倍。
        yield return new WaitForSecondsRealtime(2f);

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
        PlayNextQueued();   // 【2026-11-06】coroutine 收尾：队列非空就接播下一条（上飘淡出后从原位淡入）
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
        // 【2026-10-08 主人拍板】飞字停留 ×2（1.5s → 3s）：「连杀 x5」「精英击破！」这类战斗播报。
        // 左右飞入 / 飞出的过场动画保持原速，只把中间「看得见的时间」翻倍。
        const float driftDur = 3f;
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
        PlayNextQueued();   // 【2026-11-06】coroutine 收尾：队列非空就接播下一条（上飘淡出后从原位淡入）
    }
}
