using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 按钮按下变暗 + 轻微回弹。由 UICanvasSetup 运行时自动挂载，不改 prefab。
/// </summary>
[DisallowMultipleComponent]
public class UiButtonPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    const float PressScale = 0.94f;
    const float PressColorMul = 0.72f;

    RectTransform _rt;
    Graphic _graphic;
    Vector3 _baseScale = Vector3.one;
    Color _baseColor = Color.white;
    bool _pressed;
    bool _captured;

    void Awake()
    {
        CaptureBaseline();
    }

    void OnEnable()
    {
        if (!_captured)
            CaptureBaseline();
        Restore();
    }

    void OnDisable()
    {
        Restore();
    }

    void CaptureBaseline()
    {
        _rt = transform as RectTransform;
        if (_rt != null)
            _baseScale = _rt.localScale;
        var btn = GetComponent<Button>();
        _graphic = btn != null ? btn.targetGraphic : GetComponent<Graphic>();
        if (_graphic != null)
            _baseColor = _graphic.color;
        _captured = true;
    }

    /// <summary>外部改选中放大后同步基准，松手不会把 scale 冲回 1。</summary>
    public void SyncBaseScale(Vector3 baseScale)
    {
        _baseScale = baseScale;
        _captured = true;
        if (!_pressed && _rt != null)
            _rt.localScale = _baseScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        var btn = GetComponent<Button>();
        if (btn != null && !btn.interactable) return;
        if (!_captured) CaptureBaseline();
        _pressed = true;
        if (_rt != null)
            _rt.localScale = _baseScale * PressScale;
        if (_graphic != null)
            _graphic.color = new Color(
                _baseColor.r * PressColorMul,
                _baseColor.g * PressColorMul,
                _baseColor.b * PressColorMul,
                _baseColor.a);
    }

    public void OnPointerUp(PointerEventData eventData) => Restore();

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_pressed) Restore();
    }

    void Restore()
    {
        _pressed = false;
        if (_rt != null)
            _rt.localScale = _baseScale;
        if (_graphic != null)
            _graphic.color = _baseColor;
    }

    /// <summary>给 Canvas 子树里的 Button 自动挂反馈（跳过遮罩/透明点穿层）。</summary>
    public static void AttachUnder(Transform root)
    {
        if (root == null) return;
        var buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            var btn = buttons[i];
            if (btn == null || ShouldSkip(btn)) continue;
            if (btn.GetComponent<UiButtonPressFeedback>() != null) continue;
            btn.gameObject.AddComponent<UiButtonPressFeedback>();
        }
    }

    static bool ShouldSkip(Button btn)
    {
        if (btn == null) return true;
        string n = btn.gameObject.name ?? "";
        if (ContainsIgnoreCase(n, "Dim") || ContainsIgnoreCase(n, "Hole")
            || ContainsIgnoreCase(n, "Joystick") || ContainsIgnoreCase(n, "摇杆")
            || ContainsIgnoreCase(n, "Blocker") || ContainsIgnoreCase(n, "Mask"))
            return true;
        var g = btn.targetGraphic != null ? btn.targetGraphic : btn.GetComponent<Graphic>();
        if (g == null) return true;
        // 全透明命中层不做视觉反馈
        if (g.color.a < 0.02f && !(g is Image img && img.sprite != null && img.color.a >= 0.02f))
        {
            if (g is Image emptyImg && emptyImg.sprite == null && emptyImg.color.a < 0.02f)
                return true;
        }
        return false;
    }

    static bool ContainsIgnoreCase(string hay, string needle)
    {
        if (string.IsNullOrEmpty(hay) || string.IsNullOrEmpty(needle)) return false;
        return hay.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
