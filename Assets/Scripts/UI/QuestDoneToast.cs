using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「任务完成」左侧滑入横幅（2026-11-04 主人需求：任务完成提示仿新邮件模式，
/// 从屏幕左外滑入到左侧停靠位，停留后向左滑出）。【2026-11-12 主人拍板】横幅只写
/// 「任务完成」，不展示具体任务名。多个任务连续完成时排队逐条播，
/// 同时最多显示一条。纯代码插值（无 DOTween），用 Time.unscaledDeltaTime，不受暂停/时间缩放影响。
///
/// ⚠ Canvas 初始化刻意不走 UICanvasSetup.ApplyPopup（原因同 NewMailToast.cs 类注释：
/// Apply 会跑 UiLayoutStretch.ApplyVerticalSplit，把「居中但明显偏侧」的直接子节点重锚到
/// 顶/底，毁掉滑入动画）。这里手动套摄像机模式必要部分，相机绑定仍走
/// UICanvasSetup.ResolveUiCamera，符合「全界面走摄像机模式」的核心要求。
/// </summary>
public class QuestDoneToast : MonoBehaviour
{
    const float SlideInTime = 0.30f;    // 快速滑入
    // 2026-11-07 主人「左边任务完成提示时间缩短20%」：停留 5s → 4s（滑入/滑出动画不动）。
    const float HoldTime = 4.0f;        // 停留 4 秒
    const float SlideOutTime = 0.35f;   // 向左滑出
    const float PadX = 18f;             // 左右内边距
    const float PadY = 10f;             // 上下内边距
    const int FontSize = 24;            // 与邮件提示同款字号（缩放后等效约 31）
    const float MarginLeft = 0f;        // 左缘贴住屏幕左缘（与邮件提示右缘贴右缘对称）
    const float YPos = 320f;            // 与邮件提示同款「距顶边」基准（见 PlayNext）
    const int SortOrder = 950;
    const float ToastScale = 1.3f;      // 整体等比放大（与邮件提示 10-31 拍板同口径）
    const int MaxQueue = 4;             // 排队上限防刷屏（任务通知低频，超出直接丢弃）

    /// <summary>底框颜色：深褐色 + 半透明（与邮件提示同款，视觉统一）。</summary>
    static readonly Color PanelBgColor = new Color(0.20f, 0.13f, 0.07f, 0.6f);

    enum Phase { In, Hold, Out, Done }

    static QuestDoneToast _inst;
    static readonly Queue<string> Queue = new Queue<string>();

    Canvas _canvas;
    RectTransform _panel;
    Text _label;
    Phase _phase = Phase.Done;
    float _timer;
    float _xFinal;   // 停留位 x（锚画布左上：MarginLeft，左缘贴屏幕左缘）
    float _xOff;     // 屏外左侧（pivot 左缘对齐：-自身放大后宽 = 完全出屏左）
    float _yFinal;

    /// <summary>弹一条「任务完成」；正在显示时排队（上限 4，超出丢弃）。
    /// 【2026-11-12 主人拍板】不写具体任务名，所有任务共用同一句。</summary>
    public static void Show()
    {
        if (Queue.Count >= MaxQueue) return;   // 刷屏保护：直接丢弃
        Queue.Enqueue("任务完成");
        if (_inst == null) EnsureInstance();
        if (_inst == null) return;             // 相机未就绪等场景下静默跳过，不崩（队列留待下次）
        if (_inst._phase == Phase.Done) _inst.PlayNext();
    }

    static void EnsureInstance()
    {
        if (_inst != null) return;
        var go = new GameObject("QuestDoneToastRoot", typeof(Canvas), typeof(GraphicRaycaster));
        go.AddComponent<QuestDoneToast>();     // Awake 内完成建面板
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (_inst != null && _inst != this) { Destroy(gameObject); return; }
        _inst = this;
        BuildCanvas();
        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (_inst == this) _inst = null;
    }

    /// <summary>手动套摄像机模式必要项（不走 ApplyPopup，原因见类注释）。同 NewMailToast.BuildCanvas。</summary>
    void BuildCanvas()
    {
        _canvas = GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceCamera;
        _canvas.worldCamera = UICanvasSetup.ResolveUiCamera();
        _canvas.planeDistance = GameConfig.UI_PLANE_DISTANCE;
        _canvas.overrideSorting = true;
        _canvas.sortingOrder = SortOrder;

        var rt = (RectTransform)transform;
        rt.localScale = Vector3.one;
        rt.localPosition = Vector3.zero;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);

        var scaler = GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.referenceResolution = new Vector2(GameConfig.DESIGN_WIDTH, GameConfig.DESIGN_HEIGHT);
        scaler.matchWidthOrHeight = DesignAspectLetterbox.ResolveUiMatch();

        BuildPanel();   // 面板一次建好，文案每次播放前刷新
    }

    void BuildPanel()
    {
        var panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(transform, false);
        _panel = panel.transform as RectTransform;
        // 锚画布左上角（pivot 左上）：停留位 = 左缘贴屏幕左缘 + 「距顶边」恒定距离，
        // 与邮件提示（右上角）同高度，任意分辨率都贴住左上角。
        _panel.anchorMin = _panel.anchorMax = new Vector2(0f, 1f);
        _panel.pivot = new Vector2(0f, 1f);
        var img = panel.GetComponent<Image>();
        img.color = PanelBgColor;
        img.raycastTarget = false;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelGo.transform.SetParent(_panel, false);
        var lrt = labelGo.transform as RectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;
        _label = labelGo.GetComponent<Text>();
        _label.font = GameFonts.GetChinese();
        _label.text = "";
        _label.fontSize = FontSize;
        _label.alignment = TextAnchor.MiddleCenter;
        _label.color = Color.white;
        _label.supportRichText = false;
        _label.raycastTarget = false;
        // 不换行不裁剪，保证 PreferredWidth/Height 是纯文本尺寸（底框贴合文字的前提）
        _label.horizontalOverflow = HorizontalWrapMode.Overflow;
        _label.verticalOverflow = VerticalWrapMode.Overflow;
    }

    /// <summary>按队首文案重设文字与面板尺寸（底框按文字自适应），并从屏外左侧起播。</summary>
    void PlayNext()
    {
        if (Queue.Count == 0) { _phase = Phase.Done; gameObject.SetActive(false); return; }
        string msg = Queue.Dequeue();
        _label.text = msg;
        float wRaw = _label.preferredWidth + PadX * 2f;
        float hRaw = _label.preferredHeight + PadY * 2f;
        _panel.sizeDelta = new Vector2(wRaw, hRaw);
        _panel.localScale = Vector3.one * ToastScale;
        _xFinal = MarginLeft;
        _xOff = -(wRaw * ToastScale);   // pivot 左缘对齐：向左挪放大后的总宽 = 完全出屏左
        // 与邮件提示同款换算：面板顶边距屏幕顶边 = H/2 − YPos − 2.5h（参考画布 720×1280 口径）
        _yFinal = -(GameConfig.DESIGN_HEIGHT * 0.5f - YPos - hRaw * 2f - hRaw * 0.5f);

        if (_canvas != null)
            _canvas.worldCamera = UICanvasSetup.ResolveUiCamera() ?? _canvas.worldCamera;

        _phase = Phase.In;
        _timer = 0f;
        gameObject.SetActive(true);
        _panel.anchoredPosition = new Vector2(_xOff, _yFinal);
    }

    void Update()
    {
        if (_phase == Phase.Done) return;
        float dt = Time.unscaledDeltaTime;
        _timer += dt;

        if (_phase == Phase.In)
        {
            float k = Mathf.Clamp01(_timer / SlideInTime);
            float e = 1f - (1f - k) * (1f - k);   // EaseOutQuad：快进慢出
            _panel.anchoredPosition = new Vector2(Mathf.Lerp(_xOff, _xFinal, e), _yFinal);
            if (_timer >= SlideInTime)
            {
                _phase = Phase.Hold;
                _timer = 0f;
                _panel.anchoredPosition = new Vector2(_xFinal, _yFinal);
            }
        }
        else if (_phase == Phase.Hold)
        {
            if (_timer >= HoldTime)
            {
                _phase = Phase.Out;
                _timer = 0f;
            }
        }
        else if (_phase == Phase.Out)
        {
            float k = Mathf.Clamp01(_timer / SlideOutTime);
            float e = k * k;                       // EaseInQuad：慢起快出，向左滑走
            _panel.anchoredPosition = new Vector2(Mathf.Lerp(_xFinal, _xOff, e), _yFinal);
            if (_timer >= SlideOutTime)
            {
                if (Queue.Count > 0) PlayNext();   // 队列还有就接着播下一条
                else { _phase = Phase.Done; gameObject.SetActive(false); }
            }
        }
    }
}
