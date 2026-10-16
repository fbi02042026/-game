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
    const float PanelW = 400f;
    const float PanelH = 100f;
    const float MarginRight = 24f;
    const float YPos = 300f;            // 右上区域（canvas 高 1280，中心 0，故 +300 在偏上）
    const float OffX = 720f;            // 屏幕外右侧（设计宽 720，half=360，+720 必在屏外）
    const int SortOrder = 950;

    enum Phase { In, Hold, Out, Done }

    static NewMailToast _inst;

    Canvas _canvas;
    RectTransform _panel;
    Text _label;
    Phase _phase = Phase.Done;
    float _timer;
    float _xFinal;

    public static void Notify()
    {
        if (_inst == null) EnsureInstance();
        if (_inst == null) return;                 // 相机未就绪等场景下静默跳过，不崩
        if (_inst._phase != Phase.Done) return;   // 已有提示在显示，忽略新请求（邮件本就低频）
        _inst.Begin();
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
        _xFinal = (GameConfig.DESIGN_WIDTH * 0.5f) - (PanelW * 0.5f) - MarginRight;

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(transform, false);
        _panel = panel.transform as RectTransform;
        _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0.5f);
        _panel.pivot = new Vector2(0.5f, 0.5f);
        _panel.sizeDelta = new Vector2(PanelW, PanelH);
        _panel.anchoredPosition = new Vector2(OffX, YPos);
        var img = panel.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.85f);
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
        _label.fontSize = 30;
        _label.alignment = TextAnchor.MiddleCenter;
        _label.color = Color.white;
        _label.supportRichText = false;
        _label.raycastTarget = false;
    }

    void Begin()
    {
        // 每次显示重绑相机（DDOL 跨场景后相机可能已变）
        if (_canvas != null)
            _canvas.worldCamera = UICanvasSetup.ResolveUiCamera() ?? _canvas.worldCamera;

        _phase = Phase.In;
        _timer = 0f;
        gameObject.SetActive(true);
        _panel.anchoredPosition = new Vector2(OffX, YPos);
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
            _panel.anchoredPosition = new Vector2(Mathf.Lerp(OffX, _xFinal, e), YPos);
            if (_timer >= SlideInTime)
            {
                _phase = Phase.Hold;
                _timer = 0f;
                _panel.anchoredPosition = new Vector2(_xFinal, YPos);
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
            _panel.anchoredPosition = new Vector2(Mathf.Lerp(_xFinal, OffX, e), YPos);
            if (_timer >= SlideOutTime)
            {
                _phase = Phase.Done;
                gameObject.SetActive(false);
            }
        }
    }
}
