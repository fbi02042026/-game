using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 冒险日志三期运行时面板：碎片合成 / 里程商店。
/// 2026-10-19 正式化：木质描边弹窗 + 全屏遮罩 + 右上角关闭，
/// 修复旧版行数溢出面板、关闭按钮落在面板外的问题。
/// 2026-10-19 预制体化：层级由 Tools → UI → 生成三期面板预制体 烘焙到
/// Resources/Prefabs/Town/AdventureLogPhase3Panel.prefab；运行时只实例化预制体并按节点名绑定，
/// 缺预制体直接报错（不做代码兜底，出错要能看见）。
/// </summary>
public class AdventureLogPhase3Panel : MonoBehaviour
{
    enum Mode { None, Craft, Shop }

    public const string PrefabResPath = "Prefabs/Town/AdventureLogPhase3Panel";

    // ===== 节点名（预制体与代码按名字对齐；改 prefab 节点名会断绑定，Console 会报缺哪个） =====
    public const string RootName = "Phase3Root";
    public const string BarName = "Phase3Bar";
    public const string DimName = "Phase3Dim";
    public const string PanelName = "Phase3Panel";
    public const string InnerName = "Inner";
    public const string TitleName = "Title";
    public const string BodyName = "Body";
    public const string CraftBtnName = "Btn_Craft";
    public const string ShopBtnName = "Btn_Shop";
    public const string CloseXName = "CloseX";
    public static string RowName(int i) => "Row_" + i;

    AdventureLogUI _host;
    GameObject _bar;
    GameObject _dim;
    GameObject _panel;
    Text _title;
    Text _body;
    Mode _mode;
    readonly Button[] _actionBtns = new Button[6];
    readonly Text[] _actionLabels = new Text[6];
    bool _missingLogged;

    // ===== 正式化配色（暖木 + 羊皮金，与冒险日志整体一致） =====
    static readonly Color FrameColor = new Color(0.38f, 0.27f, 0.16f, 1f);      // 木质外框
    static readonly Color PanelColor = new Color(0.145f, 0.105f, 0.072f, 0.985f); // 面板内衬
    static readonly Color RowBorderColor = new Color(0.27f, 0.19f, 0.115f, 1f);  // 行外框
    static readonly Color RowInnerColor = new Color(0.185f, 0.13f, 0.082f, 1f);  // 行内衬
    static readonly Color GoldColor = new Color(1f, 0.85f, 0.55f, 1f);          // 标题金
    static readonly Color CreamColor = new Color(0.93f, 0.87f, 0.72f, 1f);      // 正文米白
    static readonly Color DividerColor = new Color(0.79f, 0.63f, 0.36f, 0.55f); // 分隔线
    static readonly Color PriceColorHex = new Color32(232, 200, 134, 255);      // 价格亮金
    static readonly Color SubColorHex = new Color32(184, 169, 140, 255);        // 限购次级字
    static readonly Color DisabledColor = new Color(0.55f, 0.50f, 0.44f, 1f);   // 置灰

    const float PanelW = 560f;
    const float PanelH = 880f;
    const float RowW = 500f;
    const float RowH = 42f;
    const float RowStep = 48f;
    const float RowTopY = -158f;

    public static AdventureLogPhase3Panel Ensure(AdventureLogUI host)
    {
        if (host == null) return null;
        var existing = host.GetComponent<AdventureLogPhase3Panel>();
        if (existing != null)
        {
            existing._host = host;
            existing.EnsureUi();
            return existing;
        }
        var p = host.gameObject.AddComponent<AdventureLogPhase3Panel>();
        p._host = host;
        p.EnsureUi();
        return p;
    }

    public void SetVisibleForTab(bool worldOrAch)
    {
        EnsureUi();
        if (_bar != null) _bar.SetActive(worldOrAch);
        if (!worldOrAch && _panel != null)
        {
            _mode = Mode.None;
            _panel.SetActive(false);
            if (_dim != null) _dim.SetActive(false);
        }
    }

    /// <summary>运行时：只从预制体实例化，不代码构建（唯一路径）。</summary>
    void EnsureUi()
    {
        if (_bar != null) return;
        var prefab = Resources.Load<GameObject>(PrefabResPath);
        if (prefab == null)
        {
            if (!_missingLogged)
            {
                _missingLogged = true;
                Debug.LogError($"[Phase3Panel] 缺少预制体 Assets/Resources/{PrefabResPath}.prefab —— 请执行 Tools → UI → 生成三期面板预制体");
            }
            return;
        }
        var canvas = _host != null ? _host.transform : transform;
        var root = Instantiate(prefab, canvas, false);
        root.name = RootName;
        // 统一画布设置：Screen Space - Camera + UI 相机 + 720×1280 缩放 + GraphicRaycaster，
        // 嵌套 Canvas overrideSorting 抬到城镇弹窗档（与商店/酒馆弹窗同层）
        var rootCanvas = root.GetComponent<Canvas>();
        if (rootCanvas != null)
            UICanvasSetup.ApplyPopup(rootCanvas, GameConfig.UiSort.TownPopup);
        else
            Debug.LogError("[Phase3Panel] 预制体根缺 Canvas 组件，请重新生成预制体");
        WireRefs(root.transform);
        _bar.SetActive(false);
        _dim.SetActive(false);
        _panel.SetActive(false);
    }

    void WireRefs(Transform root)
    {
        _bar = FindReq(root, BarName);
        _dim = FindReq(root, DimName);
        _panel = FindReq(root, PanelName);
        if (_bar == null || _dim == null || _panel == null) return;

        BindButton(_bar.transform, CraftBtnName, OpenCraft);
        BindButton(_bar.transform, ShopBtnName, OpenShop);

        var dimBtn = _dim.GetComponent<Button>();
        if (dimBtn != null)
        {
            dimBtn.onClick.RemoveAllListeners();
            dimBtn.onClick.AddListener(ClosePanel);
        }

        var inner = _panel.transform.Find(InnerName);
        _title = inner != null ? inner.Find(TitleName)?.GetComponent<Text>() : null;
        _body = inner != null ? inner.Find(BodyName)?.GetComponent<Text>() : null;
        if (_title == null) Debug.LogError($"[Phase3Panel] 预制体缺节点 {PanelName}/{InnerName}/{TitleName}（Text）");
        if (_body == null) Debug.LogError($"[Phase3Panel] 预制体缺节点 {PanelName}/{InnerName}/{BodyName}（Text）");

        for (int i = 0; i < 6; i++)
        {
            var row = inner != null ? inner.Find(RowName(i)) : null;
            if (row == null)
            {
                Debug.LogError($"[Phase3Panel] 预制体缺节点 {PanelName}/{InnerName}/{RowName(i)}");
                continue;
            }
            _actionBtns[i] = row.GetComponent<Button>();
            _actionLabels[i] = row.GetComponentInChildren<Text>();
            if (_actionBtns[i] == null || _actionLabels[i] == null)
                Debug.LogError($"[Phase3Panel] 节点 {RowName(i)} 缺 Button 或 Label Text");
        }

        BindButton(inner, CloseXName, ClosePanel);
        GameFonts.ApplyToHierarchy(root);
    }

    static GameObject FindReq(Transform root, string name)
    {
        var t = root.Find(name);
        if (t == null) Debug.LogError($"[Phase3Panel] 预制体缺节点 {name}");
        return t != null ? t.gameObject : null;
    }

    static void BindButton(Transform parent, string name, UnityEngine.Events.UnityAction onClick)
    {
        if (parent == null) return;
        var t = parent.Find(name);
        if (t == null)
        {
            Debug.LogError($"[Phase3Panel] 预制体缺按钮 {parent.name}/{name}");
            return;
        }
        var btn = t.GetComponent<Button>();
        if (btn == null)
        {
            Debug.LogError($"[Phase3Panel] 节点 {name} 缺 Button 组件");
            return;
        }
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(onClick);
    }

    void OpenCraft()
    {
        _mode = Mode.Craft;
        RefreshPanel();
    }

    void OpenShop()
    {
        _mode = Mode.Shop;
        AdventureLogMileageShop.EnsureWeek();
        RefreshPanel();
    }

    void ClosePanel()
    {
        _mode = Mode.None;
        if (_panel != null) _panel.SetActive(false);
        if (_dim != null) _dim.SetActive(false);
    }

    void RefreshPanel()
    {
        EnsureUi();
        if (_panel == null) return;
        if (_mode == Mode.None)
        {
            _panel.SetActive(false);
            _dim.SetActive(false);
            return;
        }
        if (_dim != null)
        {
            _dim.SetActive(true);
            _dim.transform.SetAsLastSibling();
        }
        _panel.SetActive(true);
        _panel.transform.SetAsLastSibling();

        if (_mode == Mode.Craft)
        {
            if (_title != null) _title.text = "碎片合成";
            if (_body != null) _body.text = AdventureLogFragments.FormatInventory();
            for (int i = 0; i < 6; i++)
            {
                if (_actionBtns[i] == null) continue;
                bool on = i < AdventureLogFragments.Recipes.Length;
                _actionBtns[i].gameObject.SetActive(on);
                if (!on) continue;
                int idx = i;
                bool can = AdventureLogFragments.CanCraft(idx);
                if (_actionLabels[i] != null)
                {
                    _actionLabels[i].text = can
                        ? AdventureLogFragments.FormatRecipeLine(idx)
                        : $"<color=#{ColorUtility.ToHtmlStringRGB(DisabledColor)}>{AdventureLogFragments.FormatRecipeLine(idx)}</color>";
                    _actionLabels[i].color = can ? CreamColor : DisabledColor;
                }
                _actionBtns[i].interactable = can;
                _actionBtns[i].onClick.RemoveAllListeners();
                _actionBtns[i].onClick.AddListener(() =>
                {
                    if (AdventureLogFragments.TryCraft(idx, out string msg))
                        UIManager.Instance?.ShowToast(msg);
                    else
                        UIManager.Instance?.ShowToast(msg ?? "无法合成");
                    RefreshPanel();
                    _host?.RefreshAfterPhase3();
                });
            }
        }
        else
        {
            if (_title != null) _title.text = "里程商店";
            if (_body != null)
            {
                _body.text = AdventureLogMileage.FormatStatusLine() + "\n本周：" + AdventureLogMileageShop.CurrentWeekKey()
                             + $"\n卷轴库存 普通{SaveSystem.Instance?.Data?.mercScrollCommon ?? 0}"
                             + $"/高级{SaveSystem.Instance?.Data?.mercScrollLegendary ?? 0}";
            }
            for (int i = 0; i < 6; i++)
            {
                if (_actionBtns[i] == null) continue;
                bool on = i < AdventureLogMileageShop.Items.Length;
                _actionBtns[i].gameObject.SetActive(on);
                if (!on) continue;
                int idx = i;
                bool can = AdventureLogMileageShop.CanBuy(idx, out _);
                if (_actionLabels[i] != null)
                {
                    _actionLabels[i].text = ShopLine(idx, can);
                    _actionLabels[i].color = can ? CreamColor : DisabledColor;
                }
                _actionBtns[i].interactable = can;
                _actionBtns[i].onClick.RemoveAllListeners();
                _actionBtns[i].onClick.AddListener(() =>
                {
                    if (AdventureLogMileageShop.TryBuy(idx, out string msg))
                        UIManager.Instance?.ShowToast(msg);
                    else
                        UIManager.Instance?.ShowToast(msg ?? "无法兑换");
                    RefreshPanel();
                });
            }
        }
    }

    /// <summary>商品行文本：名称左对齐，价格亮金、限购灰金靠语义分段。</summary>
    static string ShopLine(int idx, bool can)
    {
        var item = AdventureLogMileageShop.Items[idx];
        int left = AdventureLogMileageShop.RemainThisWeek(item);
        if (!can)
            return $"{item.Name}　{item.CostPoints}点　本周余{left}/{item.WeeklyLimit}";
        return $"{item.Name}　<color=#{ColorUtility.ToHtmlStringRGB(PriceColorHex)}>{item.CostPoints}点</color>"
             + $"<color=#{ColorUtility.ToHtmlStringRGB(SubColorHex)}>　本周余{left}/{item.WeeklyLimit}</color>";
    }

    // =====================================================================
    // 层级构建（Editor 烘焙专用）：Tools → UI → 生成三期面板预制体 调用；
    // 运行时不走这里，只加载烘焙好的预制体（单一来源，改这里后需重新生成预制体）。
    // =====================================================================
    public static void BuildHierarchy(RectTransform canvasRoot)
    {
        // 统一画布（组件配置对齐 CodexInfoPopup.prefab：Canvas + CanvasScaler + GraphicRaycaster）
        var canvas = canvasRoot.GetComponent<Canvas>();
        if (canvas == null) canvas = canvasRoot.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.planeDistance = GameConfig.UI_PLANE_DISTANCE;
        canvas.sortingOrder = GameConfig.UiSort.TownPopup;
        var scaler = canvasRoot.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvasRoot.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.referenceResolution = new Vector2(GameConfig.DESIGN_WIDTH, GameConfig.DESIGN_HEIGHT);
        scaler.matchWidthOrHeight = 1f;
        if (canvasRoot.GetComponent<GraphicRaycaster>() == null)
            canvasRoot.gameObject.AddComponent<GraphicRaycaster>();

        // 底部入口条：碎片合成 / 里程商店
        var bar = new GameObject(BarName, typeof(RectTransform));
        bar.transform.SetParent(canvasRoot, false);
        var barRt = bar.GetComponent<RectTransform>();
        barRt.anchorMin = new Vector2(0.5f, 0f);
        barRt.anchorMax = new Vector2(0.5f, 0f);
        barRt.pivot = new Vector2(0.5f, 0f);
        barRt.anchoredPosition = new Vector2(0f, 118f);
        barRt.sizeDelta = new Vector2(420f, 44f);
        MakeWoodButton(bar.transform, CraftBtnName, "碎片合成", new Vector2(-110f, 0f), null, 200f, 44f, 17);
        MakeWoodButton(bar.transform, ShopBtnName, "里程商店", new Vector2(110f, 0f), null, 200f, 44f, 17);

        // 全屏遮罩：压暗日志页并拦截点击；点遮罩直接关闭
        var dim = new GameObject(DimName, typeof(RectTransform), typeof(Image), typeof(Button));
        dim.transform.SetParent(canvasRoot, false);
        var dimRt = dim.GetComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero;
        dimRt.offsetMax = Vector2.zero;
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.62f);
        var dimBtn = dim.GetComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;

        // 弹窗面板：外层图当木质描边，内衬再铺深色底
        var panel = new GameObject(PanelName, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasRoot, false);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(PanelW, PanelH);
        prt.anchoredPosition = Vector2.zero;
        panel.GetComponent<Image>().color = FrameColor;

        var inner = new GameObject(InnerName, typeof(RectTransform), typeof(Image));
        inner.transform.SetParent(panel.transform, false);
        var irt = inner.GetComponent<RectTransform>();
        irt.anchorMin = Vector2.zero;
        irt.anchorMax = Vector2.one;
        irt.offsetMin = new Vector2(6f, 6f);
        irt.offsetMax = new Vector2(-6f, -6f);
        inner.GetComponent<Image>().color = PanelColor;
        inner.GetComponent<Image>().raycastTarget = false;

        // 标题 + 分隔线
        var title = MakeText(inner.transform, TitleName, 23, TextAnchor.MiddleCenter, new Vector2(0f, -40f), new Vector2(PanelW - 120f, 36f));
        title.color = GoldColor;
        title.fontStyle = FontStyle.Bold;
        var outline = title.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.16f, 0.09f, 0.04f, 1f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        var divider = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        divider.transform.SetParent(inner.transform, false);
        var drt = divider.GetComponent<RectTransform>();
        drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0.5f);
        drt.anchoredPosition = new Vector2(0f, -68f);
        drt.sizeDelta = new Vector2(500f, 2f);
        divider.GetComponent<Image>().color = DividerColor;
        divider.GetComponent<Image>().raycastTarget = false;

        // 里程状态 / 说明
        var body = MakeText(inner.transform, BodyName, 15, TextAnchor.UpperLeft, new Vector2(0f, -106f), new Vector2(500f, 64f));
        body.color = CreamColor;

        // 商品 / 合成行（6 槽，按模式显隐）
        for (int i = 0; i < 6; i++)
        {
            float y = RowTopY - i * RowStep;
            MakeShopRow(inner.transform, RowName(i), new Vector2(0f, y));
        }

        // 右上角关闭：两根旋转细条拼 X，不依赖字体字形
        var close = new GameObject(CloseXName, typeof(RectTransform), typeof(Image), typeof(Button));
        close.transform.SetParent(inner.transform, false);
        var crt = close.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.anchoredPosition = new Vector2(PanelW / 2f - 42f, -40f);
        crt.sizeDelta = new Vector2(46f, 46f);
        close.GetComponent<Image>().color = FrameColor;
        MakeXMark(close.transform);

        GameFonts.ApplyToHierarchy(canvasRoot);
    }

    /// <summary>商品/合成行：木质外框 + 内衬按钮。</summary>
    static GameObject MakeShopRow(Transform parent, string name, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(RowW, RowH);
        go.GetComponent<Image>().color = RowBorderColor;

        var inner = new GameObject(InnerName, typeof(RectTransform), typeof(Image));
        inner.transform.SetParent(go.transform, false);
        var irt = inner.GetComponent<RectTransform>();
        irt.anchorMin = Vector2.zero;
        irt.anchorMax = Vector2.one;
        irt.offsetMin = new Vector2(3f, 3f);
        irt.offsetMax = new Vector2(-3f, -3f);
        inner.GetComponent<Image>().color = RowInnerColor;
        inner.GetComponent<Image>().raycastTarget = false;

        var tx = MakeText(go.transform, "Label", 15, TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(RowW - 30f, RowH - 8f));
        return go;
    }

    /// <summary>木质按钮：底栏入口/通用。</summary>
    static GameObject MakeWoodButton(Transform parent, string name, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick, float w, float h, int fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(w, h);
        go.GetComponent<Image>().color = RowBorderColor;
        if (onClick != null) go.GetComponent<Button>().onClick.AddListener(onClick);

        var inner = new GameObject(InnerName, typeof(RectTransform), typeof(Image));
        inner.transform.SetParent(go.transform, false);
        var irt = inner.GetComponent<RectTransform>();
        irt.anchorMin = Vector2.zero;
        irt.anchorMax = Vector2.one;
        irt.offsetMin = new Vector2(3f, 3f);
        irt.offsetMax = new Vector2(-3f, -3f);
        inner.GetComponent<Image>().color = RowInnerColor;
        inner.GetComponent<Image>().raycastTarget = false;

        var tx = MakeText(go.transform, "Label", fontSize, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(w - 8f, h - 4f));
        tx.color = CreamColor;
        tx.text = label;
        return go;
    }

    static void MakeXMark(Transform parent)
    {
        for (int i = 0; i < 2; i++)
        {
            var bar = new GameObject("X" + i, typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(parent, false);
            var rt = bar.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(18f, 3f);
            rt.localEulerAngles = new Vector3(0f, 0f, i == 0 ? 45f : -45f);
            var img = bar.GetComponent<Image>();
            img.color = GoldColor;
            img.raycastTarget = false;
        }
    }

    static Text MakeText(Transform parent, string name, int size, TextAnchor anchor, Vector2 pos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;
        var t = go.GetComponent<Text>();
        t.font = GameFonts.GetChinese();
        t.fontSize = size;
        t.color = Color.white;
        t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }
}
