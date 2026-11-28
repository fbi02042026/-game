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
/// <para>【2026-11-28 主人「点 buff 弹出的弹窗，在有其他界面出来的时候自动关闭」】除 3 秒超时外，
/// 还会在「图标没了 / 别的大界面冒出来（剧情、章节卡、Loading、升级抽卡、奖励弹窗）/ 点了别处」时立刻收起，
/// 见 <c>Update</c>。不再出现浮框孤零零挂在别的界面上面。</para>
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
    /// <summary>浮框挂在哪个图标上方（2026-11-28：图标没了 / 别处被点 / 别的界面盖上来都要据此收框）。</summary>
    RectTransform _anchor;
    Coroutine _autoHide;
    /// <summary>本次弹出的时间（unscaled）。刚弹出的 0.1s 不判点击，避免开框那一下把自己关掉。</summary>
    float _openedAt;

    /// <summary>挂在战斗【根】Canvas 下（保证浮框的定位矩形＝整屏）。重复调用返回同一实例。</summary>
    public static BuffInfoTipUI Ensure(Transform parent)
    {
        if (parent == null) return Instance;
        if (Instance != null)
        {
            // 换场景/换 Canvas 后父级可能变了：重新挂到根 Canvas 并铺满，否则 rect 不是整屏、定位会被夹歪。
            Instance.ReattachTo(parent);
            return Instance;
        }

        var go = new GameObject("BuffInfoTipUI", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Instance = go.AddComponent<BuffInfoTipUI>();
        Instance.Build();
        return Instance;
    }

    void ReattachTo(Transform parent)
    {
        if (parent == null || transform.parent == parent) return;
        transform.SetParent(parent, false);
        if (_rootRt != null) Stretch(_rootRt);
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
        // 🔴 2026-11-28：父级必须取【根 Canvas】，不能取 anchor 的最近 Canvas。
        // buff 图标挂在 CharacterBar 下，而 CharacterBar 的祖先是 BackpackPanel（自带一个 Canvas）——
        // 取最近 Canvas 会把浮框挂进 730×600 的背包面板坐标系，PlaceAbove 的 clamp 上限只有 y=200，
        // 图标却在 y≈500，浮框被硬夹低 300 多单位 → 永远压在佣兵头像上（11-23 / 11-24 两次改坐标算法都没治到根）。
        if (parent == null && anchor != null)
        {
            var anchorCanvas = anchor.GetComponentInParent<Canvas>();
            if (anchorCanvas != null) parent = anchorCanvas.rootCanvas.transform;
        }
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
        _anchor = anchor;
        _openedAt = Time.unscaledTime;

        if (_autoHide != null) StopCoroutine(_autoHide);
        _autoHide = StartCoroutine(CoAutoHide());
    }

    public void Hide()
    {
        if (this == null) return;   // 重开战斗后静态 Instance 可能指向已销毁对象，先挡销毁态
        if (_autoHide != null) { StopCoroutine(_autoHide); _autoHide = null; }
        _openKey = null;
        _anchor = null;
        if (_panel != null) _panel.SetActive(false);
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 【2026-11-28 主人「点 buff 弹出的弹窗，在有其他界面出来的时候自动关闭」】
    /// 三条收框口径（本类只在开着时才跑 Update —— 关掉时 gameObject 是 inactive）：
    /// ① 图标本身没了（换关 / 战斗结束 / buff 条收起）→ 关；
    /// ② 有别的大界面盖上来（剧情窗 / 章节卡 / Loading / 升级抽卡 / 奖励弹窗）→ 关；
    /// ③ 点了别处（任何按钮 / 面板 / 空地）→ 关。点图标本身不在这里关，交给 Button 的 toggle。
    /// 只做「有没有别的界面」的只读判断，不接管任何界面的显隐。
    /// </summary>
    void Update()
    {
        if (_panel == null || !_panel.activeSelf) return;
        if (Time.unscaledTime - _openedAt < 0.1f) return;   // 刚弹出那一拍不判，防止被开框的点击带掉

        if (_anchor == null || !_anchor.gameObject.activeInHierarchy) { Hide(); return; }
        if (AnyOtherUiShowing()) { Hide(); return; }
        if (Input.GetMouseButtonDown(0) && !PointerOnAnchor()) Hide();
    }

    /// <summary>
    /// 是否有别的大界面正盖在战斗 HUD 之上。只列「不靠点击也会自己冒出来」的几种
    /// （点出来的界面已被上面的点击判定收掉）；以后新增这类全屏界面，往这里补一行即可。
    /// </summary>
    static bool AnyOtherUiShowing()
    {
        var dlg = DialogueUI.Instance;
        if (dlg != null && dlg.IsVisible) return true;
        if (ChapterSplashOverlay.Current != null) return true;
        if (BattleLoadingOverlay.IsShowing) return true;
        if (LevelUpDraftUI.IsShowing) return true;
        if (RewardPopupUI.IsShowing) return true;
        return false;
    }

    /// <summary>当前指针是否落在 buff 图标上（图标挂在 CharacterBar 缩放子树下，必须用屏幕坐标判）。</summary>
    bool PointerOnAnchor()
    {
        if (_anchor == null) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(_anchor, Input.mousePosition, AnchorCamera());
    }

    /// <summary>图标所属根 Canvas 的摄像机（本浮框自己的 Canvas 是运行时加的，worldCamera 不一定有）。</summary>
    Camera AnchorCamera()
    {
        if (_anchor == null) return null;
        var anchorCanvas = _anchor.GetComponentInParent<Canvas>();
        Canvas root = anchorCanvas != null ? anchorCanvas.rootCanvas : null;
        return root != null && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
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

        // 定位口径：世界坐标 → 屏幕像素 → _rootRt 本地。
        // 必须走屏幕像素这一步：buff 图标挂在预制体 CharacterBar（localScale=1.3）子树下，
        // 而本浮框 _rootRt 挂在战斗根 Canvas 下，两者隔了这个缩放节点；
        // 屏幕坐标法把世界坐标归一到屏幕像素再反投，自动吸收中间任何缩放 / 嵌套 Canvas。
        // cam 必须从 anchor 取 rootCanvas.worldCamera：不能从本浮框 GetComponentInParent<Canvas>，
        // 否则会命中浮框自己身上挂的子 Canvas，其 worldCamera 可能为 null，同样导致偏移（2026-11-23 踩过）。
        var anchorCanvas = anchor.GetComponentInParent<Canvas>();
        Canvas rootCanvas = anchorCanvas != null ? anchorCanvas.rootCanvas : null;
        Camera cam = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera
            : null;

        var corners = new Vector3[4];
        anchor.GetWorldCorners(corners);
        Vector2 lpTop = WorldToTipLocal((corners[1] + corners[2]) * 0.5f, cam);   // 图标顶边中点
        Vector2 lpBottom = WorldToTipLocal((corners[0] + corners[3]) * 0.5f, cam); // 图标底边中点

        Rect r = _rootRt.rect;
        float halfW = r.width * 0.5f;
        float halfH = r.height * 0.5f;
        // 定位容器必须是整屏（根 Canvas 铺满）。若不是，说明父级又被挂到了嵌套 Canvas 上 ——
        // 夹取范围会缩成那块小矩形，浮框被夹到错误位置（2026-11-28 佣兵头像那次就是这个原因），这里报错留痕。
        if (r.width < 100f || r.height < 100f)
            Debug.LogError("[BuffInfoTipUI] 定位容器 rect 异常（" + r.width + "x" + r.height + "），浮框位置会不准");

        float w = _panelRt.sizeDelta.x;
        float x = Mathf.Clamp(lpTop.x, -halfW + w * 0.5f + EdgePad, halfW - w * 0.5f - EdgePad);
        float minY = -halfH + EdgePad;
        float maxY = halfH - height - EdgePad;

        float y = lpTop.y + GapAbove;                 // 优先浮在图标上方
        if (y > maxY)                                 // 上方没地方（图标贴屏顶）：翻到图标下方
            y = lpBottom.y - GapAbove - height;
        y = Mathf.Clamp(y, minY, Mathf.Max(minY, maxY));
        _panelRt.anchoredPosition = new Vector2(x, y);
    }

    /// <summary>世界坐标 → 本浮框 _rootRt 本地坐标（走屏幕像素，吸收中间所有缩放/嵌套 Canvas）。</summary>
    Vector2 WorldToTipLocal(Vector3 world, Camera cam)
    {
        Vector2 sp = RectTransformUtility.WorldToScreenPoint(cam, world);
        Vector2 lp;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rootRt, sp, cam, out lp))
            return Vector2.zero;
        return lp;
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
