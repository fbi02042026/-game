using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗 buff 点击简介浮框（2026-11-18 主人拍板「战斗 buff 点击提示框 开始做」）：
/// 点 buff 图标 → 在图标正上方浮出「属性名 + 数值」；再点同一个图标 → 关闭（toggle）。
/// 显示内容主人拍板：只要名称 + 数值，不显示倒计时。
///
/// <para>交互口径沿用 <see cref="EquipStatTipUI"/>（装备属性框）：运行时建树（纯色 Image + Text + UGUI 自带
/// Outline），不落预制体、不建美术、不 Resources.Load 图片；3 秒 unscaled 自动收起；自带嵌套 Canvas
/// overrideSorting 压在最上层；面板与文本 raycastTarget=false 不抢下层点击；不做全屏遮罩。</para>
///
/// <para>【解耦约定】本类只负责「把调用方传进来的两行文案显示出来」，不读 AttrSystem / BattleManager
/// 任何状态。buff 数据由 BuffHudUI 在刷新时算好、点击时传入 —— 以后加新 buff 属性不用动本类。</para>
/// </summary>
public class BuffInfoTipUI : MonoBehaviour
{
    public static BuffInfoTipUI Instance { get; private set; }

    // 运行时建的纯代码浮框，尺寸只能写在这里（与 EquipStatTipUI 同构，不覆盖任何美术 prefab）。
    const float PanelWidth = 170f;
    const float HeadHeight = 40f;
    const float BodyHeight = 44f;
    const float PadBottom = 10f;
    const int TitleFontSize = 22;
    const int BodyFontSize = 24;
    const float GapAbove = 10f;
    const float EdgePad = 6f;
    /// <summary>自动收起等待时间（unscaled，战斗暂停也照样收；与装备框同档）。</summary>
    const float AutoHideSeconds = 3f;

    RectTransform _rootRt;
    GameObject _panel;
    RectTransform _panelRt;
    Text _title;
    Text _body;
    /// <summary>当前浮框是为哪个图标开的（key 传 buff 图标 GameObject）。</summary>
    object _openKey;
    Coroutine _autoHide;

    /// <summary>挂在 BattleUI 的 Canvas 下（保证在 Canvas 内）。重复调用返回同一实例。</summary>
    public static BuffInfoTipUI Ensure(Transform parent)
    {
        if (Instance != null) return Instance;
        if (parent == null) return null;

        var go = new GameObject("BuffInfoTipUI", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Instance = go.AddComponent<BuffInfoTipUI>();
        Instance.Build();
        return Instance;
    }

    void Build()
    {
        _rootRt = GetComponent<RectTransform>();
        Stretch(_rootRt);   // 铺满 Canvas：后续用 anchoredPosition 在它内部定位

        // 与 EquipStatTipUI 同口径：自带嵌套 Canvas + 高 sortingOrder，压在最上层
        //（高于 BattleSettlement 990，低于 Toast 11000 / FullscreenFx 12000）。
        var rootCanvas = gameObject.GetComponent<Canvas>();
        if (rootCanvas == null) rootCanvas = gameObject.AddComponent<Canvas>();
        rootCanvas.overrideSorting = true;
        rootCanvas.sortingOrder = 1000;
        // 刻意不加 GraphicRaycaster：面板与文本都 raycastTarget=false，不抢下层点击。

        _panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        _panel.transform.SetParent(transform, false);
        var pImg = _panel.GetComponent<Image>();
        pImg.color = new Color(0.12f, 0.12f, 0.15f, 0.96f);
        pImg.raycastTarget = false;
        _panelRt = _panel.GetComponent<RectTransform>();
        _panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        _panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        _panelRt.pivot = new Vector2(0.5f, 0f);   // 底边对齐定位点 → 天然浮在图标上方
        _panelRt.sizeDelta = new Vector2(PanelWidth, HeadHeight + BodyHeight + PadBottom);

        // 属性名（顶行）
        _title = MakeText(_panel.transform, "Title", TitleFontSize, TextAnchor.MiddleCenter, Color.white);
        var tRt = _title.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 1f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.pivot = new Vector2(0.5f, 1f);
        tRt.anchoredPosition = new Vector2(0f, -8f);
        tRt.sizeDelta = new Vector2(-16f, HeadHeight - 8f);

        // 数值（正文，居中单行）
        _body = MakeText(_panel.transform, "Body", BodyFontSize, TextAnchor.MiddleCenter, new Color(0.96f, 0.88f, 0.62f, 1f));
        var bRt = _body.GetComponent<RectTransform>();
        bRt.anchorMin = Vector2.zero;
        bRt.anchorMax = Vector2.one;
        bRt.offsetMin = new Vector2(10f, 6f);
        bRt.offsetMax = new Vector2(-10f, -HeadHeight);

        // 项目五坑第五条：运行时建的弹窗默认隐藏，否则一创建就糊在屏幕上
        gameObject.SetActive(false);
    }

    // ===== 对外入口 =====

    /// <summary>toggle 入口（唯一）：开着就关、没开就开。key 传 buff 图标 GameObject。</summary>
    public static void Toggle(string name, string value, RectTransform anchor, object key, Transform parent = null)
    {
        if (parent == null && anchor != null)
            parent = anchor.GetComponentInParent<Canvas>()?.transform;
        if (parent == null)
        {
            Debug.LogError("[BuffInfoTipUI] 找不到 Canvas 父节点，无法弹出 buff 简介浮框");
            return;
        }
        var tip = Ensure(parent);
        if (tip == null) return;
        if (string.IsNullOrEmpty(name) || anchor == null) { tip.Hide(); return; }

        if (tip.IsOpenFor(key)) { tip.Hide(); return; }
        tip.Show(name, value, anchor, key);
    }

    public bool IsOpenFor(object key)
    {
        if (!gameObject.activeSelf) return false;
        if (_panel == null || !_panel.activeSelf) return false;
        return ReferenceEquals(_openKey, key);
    }

    public void Show(string name, string value, RectTransform anchor, object key)
    {
        if (anchor == null) return;

        // 与装备属性框互斥：两个框都浮在 HUD 上，同时开会叠在一起。
        // （只做本类单向收起装备框，不改 EquipStatTipUI —— 范围只限本次点名的 buff 提示。）
        if (EquipStatTipUI.Instance != null) EquipStatTipUI.Instance.Hide();

        _title.text = name;
        _body.text = value;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();     // 抬层级：不抬会被别的层盖住（项目踩过的坑）
        _panel.SetActive(true);
        PlaceAbove(anchor, _panelRt.sizeDelta.y);
        _openKey = key;

        if (_autoHide != null) StopCoroutine(_autoHide);
        _autoHide = StartCoroutine(CoAutoHide());
    }

    public void Hide()
    {
        if (this == null) return;   // 重开战斗后静态 Instance 可能指向已销毁对象，先挡销毁态
        if (_autoHide != null) { StopCoroutine(_autoHide); _autoHide = null; }
        _openKey = null;
        if (_panel != null) _panel.SetActive(false);
        gameObject.SetActive(false);
    }

    System.Collections.IEnumerator CoAutoHide()
    {
        float t = 0f;
        while (t < AutoHideSeconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        _autoHide = null;
        Hide();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ===== 定位 =====

    /// <summary>把面板底边贴到图标顶边上方，并夹在 Canvas 矩形内（不出屏）。</summary>
    void PlaceAbove(RectTransform anchor, float height)
    {
        if (_rootRt == null || _panelRt == null || anchor == null) return;
        Canvas.ForceUpdateCanvases();   // 首帧 rect 还没算好，先刷一次再量

        // 2026-11-23：浮框与 anchor 同处战斗 Canvas 的世界空间，直接反向变换到 _rootRt 本地坐标。
        // 旧写法走 WorldToScreenPoint/ScreenPointToLocalPoint 往返；浮框自身挂了 Overlay Canvas，
        // GetComponentInParent<Canvas>() 取到它自己 → cam 判成 null → 把 ScreenSpaceCamera 下的
        // 世界坐标当成屏幕像素，浮框整体偏移，跑到佣兵头像上。改为同空间直接反变换，不出屏。
        var corners = new Vector3[4];
        anchor.GetWorldCorners(corners);
        Vector3 topCenter = (corners[1] + corners[2]) * 0.5f;
        Vector3 localTop = _rootRt.InverseTransformPoint(topCenter);
        Vector2 lp = new Vector2(localTop.x, localTop.y);

        Rect r = _rootRt.rect;
        float halfW = r.width * 0.5f;
        float halfH = r.height * 0.5f;
        float w = _panelRt.sizeDelta.x;
        float x = Mathf.Clamp(lp.x, -halfW + w * 0.5f + EdgePad, halfW - w * 0.5f - EdgePad);
        float y = Mathf.Clamp(lp.y + GapAbove, -halfH + EdgePad, halfH - height - EdgePad);
        _panelRt.anchoredPosition = new Vector2(x, y);
    }

    // ===== 构建辅助 =====

    static Text MakeText(Transform parent, string name, int size, TextAnchor anchor, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = GameFonts.GetChinese();
        t.fontSize = size;
        t.alignment = anchor;
        t.color = color;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var ol = go.AddComponent<Outline>();
        ol.effectColor = new Color(0f, 0f, 0f, 0.85f);
        ol.effectDistance = new Vector2(1f, -1f);
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
