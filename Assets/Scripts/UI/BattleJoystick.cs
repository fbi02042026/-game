using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 战斗虚拟摇杆：触摸区=下方装备栏/背包整区；按下时在触点生成浮动摇杆，松手后隐藏底座。
/// 「整理/确定」提到最上层以免被挡。按下优先手动，松手近战最近 / 远程最强。
/// </summary>
public class BattleJoystick : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    public static BattleJoystick Instance { get; private set; }

    RectTransform _pad;
    RectTransform _stickBase;
    RectTransform _knob;
    CanvasGroup _group;
    CanvasGroup _stickGroup;
    bool _held;
    const float MaxRadius = 56f;
    const float IdleY = 70f;

    public static BattleJoystick EnsureOn(Transform battleUiRoot)
    {
        if (Instance != null)
        {
            Instance.RaiseOrganizeAbove();
            return Instance;
        }
        if (battleUiRoot == null) return null;

        Transform backpack = FindDeep(battleUiRoot, "BackpackPanel");
        if (backpack == null) backpack = battleUiRoot;

        var go = new GameObject("BattleJoystick", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        go.transform.SetParent(backpack, false);
        var pad = go.GetComponent<RectTransform>();
        pad.anchorMin = Vector2.zero;
        pad.anchorMax = Vector2.one;
        pad.offsetMin = Vector2.zero;
        pad.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.01f);
        img.raycastTarget = true;

        var joy = go.AddComponent<BattleJoystick>();
        joy._pad = pad;
        joy._group = go.GetComponent<CanvasGroup>();
        joy.BuildKnob(go.transform);
        joy.HideStickVisual();
        joy.RaiseOrganizeAbove();
        return joy;
    }

    void Awake()
    {
        Instance = this;
        if (_group == null) _group = GetComponent<CanvasGroup>();
        if (_pad == null) _pad = GetComponent<RectTransform>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Hero.Instance?.ClearManualMove();
    }

    void BuildKnob(Transform parent)
    {
        var baseGo = new GameObject("StickBase", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        baseGo.transform.SetParent(parent, false);
        _stickBase = baseGo.GetComponent<RectTransform>();
        // 闲置默认底中（教学高亮）；操作时移到触点
        _stickBase.anchorMin = new Vector2(0.5f, 0f);
        _stickBase.anchorMax = new Vector2(0.5f, 0f);
        _stickBase.pivot = new Vector2(0.5f, 0.5f);
        _stickBase.sizeDelta = new Vector2(110f, 110f);
        _stickBase.anchoredPosition = new Vector2(0f, IdleY);
        var baseImg = baseGo.GetComponent<Image>();
        baseImg.color = new Color(0.1f, 0.12f, 0.16f, 0.4f);
        baseImg.raycastTarget = false;
        _stickGroup = baseGo.GetComponent<CanvasGroup>();
        _stickGroup.blocksRaycasts = false;

        var knobGo = new GameObject("StickKnob", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        knobGo.transform.SetParent(baseGo.transform, false);
        _knob = knobGo.GetComponent<RectTransform>();
        _knob.anchorMin = new Vector2(0.5f, 0.5f);
        _knob.anchorMax = new Vector2(0.5f, 0.5f);
        _knob.pivot = new Vector2(0.5f, 0.5f);
        _knob.sizeDelta = new Vector2(52f, 52f);
        _knob.anchoredPosition = Vector2.zero;
        var knobImg = knobGo.GetComponent<Image>();
        knobImg.color = new Color(0.95f, 0.85f, 0.35f, 0.75f);
        knobImg.raycastTarget = false;
    }

    void PlaceStickAtPointer(PointerEventData eventData)
    {
        if (_pad == null || _stickBase == null) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _pad, eventData.position, eventData.pressEventCamera, out Vector2 local))
            return;

        // 相对 pad 中心的本地坐标 → 用 stretch 锚点下的 anchoredPosition
        // pad 为 stretch 满铺时，local 原点在中心；StickBase 用底中锚点，需换算
        float padW = _pad.rect.width;
        float padH = _pad.rect.height;
        // local: 相对 pivot(0.5,0.5) 的坐标；底中锚点 anchored = (local.x, local.y + padH*0.5)
        _stickBase.anchoredPosition = new Vector2(local.x, local.y + padH * 0.5f);
        if (_knob != null) _knob.anchoredPosition = Vector2.zero;
        ShowStickVisual();
    }

    void ShowStickVisual()
    {
        if (_stickGroup == null) return;
        _stickGroup.alpha = 1f;
    }

    void HideStickVisual()
    {
        if (_stickGroup == null) return;
        _stickGroup.alpha = 0f;
        if (_knob != null) _knob.anchoredPosition = Vector2.zero;
        // 回到底中，便于引导高亮
        if (_stickBase != null)
            _stickBase.anchoredPosition = new Vector2(0f, IdleY);
    }

    void RaiseOrganizeAbove()
    {
        var ui = BattleUI.Instance;
        if (ui != null && ui.organizeButton != null)
            ui.organizeButton.transform.SetAsLastSibling();
        transform.SetSiblingIndex(Mathf.Max(0, transform.parent.childCount - 2));
    }

    public RectTransform StickHighlight
    {
        get
        {
            if (_stickBase != null)
            {
                // 教学时短暂显示底中位置
                if (_stickGroup != null && _stickGroup.alpha < 0.1f)
                    ShowStickVisual();
                return _stickBase;
            }
            return _pad;
        }
    }

    public void ResetStickIdle() => HideStickVisual();

    public void SetVisible(bool on)
    {
        if (_group == null) return;
        bool show = on && !BattleLootMode.Active;
        _group.alpha = show ? 1f : 0f;
        _group.blocksRaycasts = show;
        _group.interactable = show;
        if (!show && _held)
            ForceRelease(false);
        if (!show)
            HideStickVisual();
        if (show) RaiseOrganizeAbove();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (BattleLootMode.Active) return;
        if (BattleManager.Instance == null || !BattleManager.Instance.UnitsCanAct) return;
        _held = true;
        PlaceStickAtPointer(eventData);
        UpdateStick(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_held) return;
        UpdateStick(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_held) return;
        ForceRelease(true);
    }

    void ForceRelease(bool acquire)
    {
        _held = false;
        if (_knob != null) _knob.anchoredPosition = Vector2.zero;
        HideStickVisual();
        Hero.Instance?.ClearManualMove();
        if (acquire)
            Hero.Instance?.BeginAutoAcquireOnRelease();
    }

    void UpdateStick(PointerEventData eventData)
    {
        if (_pad == null || _stickBase == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _pad, eventData.position, eventData.pressEventCamera, out Vector2 local);

        // 底座中心在 pad 本地坐标
        Vector2 center = StickBaseCenterInPad();
        Vector2 delta = local - center;
        if (delta.magnitude > MaxRadius)
            delta = delta.normalized * MaxRadius;
        if (_knob != null)
            _knob.anchoredPosition = delta;

        Vector2 dir = delta.magnitude > 8f ? delta.normalized : Vector2.zero;
        Hero.Instance?.SetManualMove(new Vector2(dir.x, dir.y * 0.55f));
    }

    Vector2 StickBaseCenterInPad()
    {
        // StickBase 锚点底中：中心 = (anchored.x, anchored.y - padH/2) 在 pad 本地（相对 pad pivot 中心）
        float padH = _pad.rect.height;
        Vector2 ap = _stickBase.anchoredPosition;
        return new Vector2(ap.x, ap.y - padH * 0.5f);
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        if (string.Equals(root.name, name, System.StringComparison.OrdinalIgnoreCase))
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var f = FindDeep(root.GetChild(i), name);
            if (f != null) return f;
        }
        return null;
    }
}
