using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「您有新的邮件」右侧滑入提示：黑底白字，从屏幕右侧快速滑入 → 停留 5 秒 → 向右滑出消失。
/// 纯代码插值（无 DOTween / LeanTween），用 Time.unscaledDeltaTime，不受暂停 / 时间缩放影响。
///
/// ⚠ Canvas 初始化**刻意不走** UICanvasSetup.ApplyPopup：
/// Apply 会跑 UiLayoutStretch.ApplyVerticalSplit，它把画布根的「居中但明显偏侧」的直接子节点
/// 重锚到顶/底（见 UiLayoutStretch.cs:361）。本控件面板正是这种元素，会被搬动而毁掉滑入动画。
/// 故这里只手动套「摄像机模式」的必要部分（renderMode / worldCamera / planeDistance / scaler /
/// raycaster / 根铺满 / 高 sortOrder），规避整套竖屏拆分/垫黑底机。摄像机绑定仍走
/// UICanvasSetup.ResolveUiCamera，符合「全界面走摄像机模式」的核心要求。
/// </summary>
public class NewMailToast : MonoBehaviour
{
    const float SlideInTime = 0.30f;    // 快速滑入
    const float HoldTime = 5.0f;        // 停留 5 秒
    const float SlideOutTime = 0.35f;   // 向右滑出
    // 【2026-10-16 主人拍板】底框不再比字大：宽高按文字 Preferred 尺寸 + 内边距自适应。
    const float PadX = 18f;             // 左右内边距
    const float PadY = 10f;             // 上下内边距
    const int FontSize = 24;            // 主人要求：30 → 24（减 20%）
    const float MarginRight = 0f;       // 主人要求：右缘贴住屏幕右缘（原 24 缝去掉）；框小了，进屏自然不多
    const float YPos = 300f;            // 【基线】参考画布(720×1280)上原中心锚的 y（换算「距顶边」距离用，见 BuildPanel）
    const int SortOrder = 950;
    /// <summary>底框颜色：深褐色 + 半透明（2026-10-27 主人拍板：替换原纯黑 0.85）。R&gt;G&gt;B 偏暖，alpha 0.6 带点半透，去掉糊屏的厚重黑块。</summary>
    static readonly Color PanelBgColor = new Color(0.20f, 0.13f, 0.07f, 0.6f);

    enum Phase { In, Hold, Out, Done }

    static NewMailToast _inst;
    static bool _pendingNotify;

    Canvas _canvas;
    RectTransform _panel;
    Text _label;
    Phase _phase = Phase.Done;
    float _timer;
    float _xFinal;   // 停留位 x（锚画布右上：−MarginRight，右缘贴屏幕右缘）
    float _xOff;     // 屏外右侧（pivot 右缘对齐：+自身宽 = 完全出屏右）
    /// <summary>停留位 y（锚画布右上：负值 = 面板顶边距屏幕顶边的距离，分辨率变化恒贴右上角）。</summary>
    float _yFinal;

    public static void Notify()
    {
        // 引导阶段（首次强制软引导尚未走完）不弹新邮件提示：避免打断新手流程。
        // 引导收尾的欢迎信走 NotifyLater→FlushPending→Notify，那时 FirstRunGuide 已 IsFinished，正常弹出。
        if (FirstRunGuide.Instance != null && !FirstRunGuide.Instance.IsFinished)
            return;
        if (_inst == null) EnsureInstance();
        if (_inst == null) return;                 // 相机未就绪等场景下静默跳过，不崩
        if (_inst._phase != Phase.Done) return;   // 已有提示在显示，忽略新请求（邮件本就低频）
        _inst.Begin();
    }

    /// <summary>
    /// 【2026-10-16 主人拍板】登记「稍后再弹」：引导收尾链里欢迎信发出时先不弹提示，
    /// 等玩家关闭每日登录界面后再冲刷（DailyLoginUI.Hide → FlushPending）。
    /// 标记是进程级的：玩家中途杀游戏就丢一次提示，邮件红点仍在，不兜底。
    /// </summary>
    public static void NotifyLater() => _pendingNotify = true;

    /// <summary>冲刷待弹提示：有登记就立刻弹一次（目前唯一调用方：每日登录界面关闭时）。</summary>
    public static void FlushPending()
    {
        if (!_pendingNotify) return;
        _pendingNotify = false;
        Notify();
    }

    static void EnsureInstance()
    {
        if (_inst != null) return;
        var go = new GameObject("NewMailToastRoot", typeof(Canvas), typeof(GraphicRaycaster));
        go.AddComponent<NewMailToast>();           // Awake 内完成建面板
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (_inst != null && _inst != this) { Destroy(gameObject); return; }
        _inst = this;
        BuildCanvas();
        BuildPanel();
        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (_inst == this) _inst = null;
    }

    /// <summary>手动套摄像机模式必要项（不走 ApplyPopup，原因见类注释）。</summary>
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
    }

    void BuildPanel()
    {
        var panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(transform, false);
        _panel = panel.transform as RectTransform;
        // 【2026-10-25 主人拍板「邮件提示也要根据分辨率改变位置」】改锚画布右上角（pivot 右上）：
        // 停留位 = 右缘贴屏幕右缘 + 「距顶边」恒定距离（由参考画布 720×1280 上的原中心锚位换算），
        // 任意分辨率都贴住右上角，与顶条右上角自适应同一观感；不再受 match 模式/逻辑宽高漂移影响。
        _panel.anchorMin = _panel.anchorMax = new Vector2(1f, 1f);
        _panel.pivot = new Vector2(1f, 1f);
        var img = panel.GetComponent<Image>();
        // 2026-10-27 主人拍板：底框不再用纯黑，改深褐色 + 半透明，去掉糊屏的厚重黑块
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
        _label.text = "您有新的邮件";
        _label.fontSize = FontSize;
        _label.alignment = TextAnchor.MiddleCenter;
        _label.color = Color.white;
        _label.supportRichText = false;
        _label.raycastTarget = false;
        // 不换行不裁剪，保证 PreferredWidth/Height 是纯文本尺寸（底框贴合文字的前提）
        _label.horizontalOverflow = HorizontalWrapMode.Overflow;
        _label.verticalOverflow = VerticalWrapMode.Overflow;

        // 【2026-10-16 主人拍板】底框按文字自适应（不再 400×100 比字大一圈），
        // 右缘贴住屏幕右缘（MarginRight=0），框小了伸进屏幕的部分自然也少。
        float w = _label.preferredWidth + PadX * 2f;
        float h = _label.preferredHeight + PadY * 2f;
        _panel.sizeDelta = new Vector2(w, h);
        _xFinal = -MarginRight;
        _xOff = w;   // pivot 右缘对齐：向右挪自身宽 = 完全出屏
        // 【2026-10-17 主人要求】提示往上移 2 个自身框高 —— 换算成参考画布(720×1280)上的「距顶边」
        // 距离：top = H/2 − (YPos + 2h + h/2)，恒定不随分辨率漂移。
        _yFinal = -(GameConfig.DESIGN_HEIGHT * 0.5f - YPos - h * 2f - h * 0.5f);
    }

    void Begin()
    {
        // 每次显示重绑相机（DDOL 跨场景后相机可能已变）
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
            float e = k * k;                       // EaseInQuad：慢起快出，向右滑走
            _panel.anchoredPosition = new Vector2(Mathf.Lerp(_xFinal, _xOff, e), _yFinal);
            if (_timer >= SlideOutTime)
            {
                _phase = Phase.Done;
                gameObject.SetActive(false);
            }
        }
    }
}
