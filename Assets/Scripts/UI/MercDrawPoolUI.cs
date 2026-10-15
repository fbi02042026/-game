using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【2026-10-13 主人拍板】酒馆「佣兵招募池」（按主人设计图 v3）：
/// 上区 = 招募池 6 格（初期 4 格，通关第 2/3 章各开 1 格），在池头像右上角红「−」= 下架（系统自动补位）；
/// 中区 = 已获得佣兵（**统一用头像**，按稀有度分组，在池的头像右上角绿「已上架」标）；
/// 下区 = 未获得佣兵（灰头像 + 「未获得」，点卡看获取途径）。
///
/// <para>交互（主人原话口径）：池子<b>最少要补全、会自动补全</b>——下架后系统立即从未上架的
/// 已解锁佣兵按稀有度补满（<see cref="SaveData.RemoveFromDrawPool"/>）；玩家上架走两条路：
/// 池未满直接点卡上架；<b>池满时点已获得佣兵弹「替换」弹窗</b>点选换下谁（不做拖拽）。
/// <b>不能预上架</b>（主人否了点子）。池子内容按稀有度自动排列。</para>
///
/// <para>所有改动<b>即时生效</b>（下架 / 上架 / 替换当场写存档），无「确定」步骤。</para>
///
/// <para>实现约定：运行时建树、不落预制体；头像 / 稀有度头像框 / 职业徽记全部复用现成素材
/// （<see cref="MercPortraitSprites.GetHead"/> / <see cref="MercHireSession.LoadPortraitFrame"/> /
/// <see cref="MercGrowSprites.LoadJobBadge"/>）；图鉴详情复用 <see cref="CodexInfoPopupUI"/>。</para>
/// </summary>
public class MercDrawPoolUI : MonoBehaviour
{
    static MercDrawPoolUI _instance;

    GameObject _root;
    Text _poolCountText;
    readonly List<GameObject> _poolSlots = new List<GameObject>();
    /// <summary>每格当前佣兵 HireId（Refresh 时记录；quxiao 下架钮按格取用）。</summary>
    string[] _slotHireIds;
    RectTransform _ownedContent;
    RectTransform _lockedContent;
    readonly List<GameObject> _ownedCards = new List<GameObject>();
    readonly List<GameObject> _lockedCards = new List<GameObject>();

    // 替换弹窗（池满时点已获得佣兵 → 选池中谁换下）
    GameObject _replacePanel;
    Text _replaceTitle;
    RectTransform _replaceContent;
    readonly List<GameObject> _replaceCards = new List<GameObject>();
    string _replacingInId;

    /// <summary>本地编辑态（每次操作后从存档重读，改动即时写盘）。</summary>
    readonly HashSet<string> _picked = new HashSet<string>();

    const float PanelW = 690f;
    const float PanelH = 1210f;
    const int GridCols = 5;
    const int PoolCols = 6;
    const float PoolSlotW = 100f;
    const float PoolSlotH = 150f;
    const float CardW = 200f;
    const float CardH = 150f;
    const float GroupHeadH = 30f;
    const float GridGap = 10f;
    const float GridPad = 10f;

    public static MercDrawPoolUI Instance => _instance;
    public bool IsOpen => _root != null && _root.activeSelf;

    /// <summary>预制体路径（Resources 相对）。2026-10-15 主人拍板：抽奖池改预制体实例化；缺失时回落运行时建树并报错（不静默）。</summary>
    const string PrefabPath = "Prefabs/Town/MercDrawPoolUI";

    // ============================================================
    // 对外入口（唯一）
    // ============================================================

    public static void Show()
    {
        Ensure().Open();
    }

    static MercDrawPoolUI Ensure()
    {
        if (_instance != null) return _instance;

        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab != null)
        {
            var inst = Object.Instantiate(prefab);
            inst.name = "MercDrawPoolUI";
            DontDestroyOnLoad(inst);
            var ui = inst.GetComponent<MercDrawPoolUI>();
            if (ui != null)
            {
                ui.BindFromHierarchy();
                _instance = ui;
                return _instance;
            }
            Debug.LogError("[MercDrawPoolUI] 预制体缺 MercDrawPoolUI 组件，回落运行时建树");
            Object.Destroy(inst);
        }
        else
        {
            Debug.LogError("[MercDrawPoolUI] 预制体缺失（Resources/" + PrefabPath + "），回落运行时建树");
        }

        var go = new GameObject("MercDrawPoolUI");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<MercDrawPoolUI>();
        return _instance;
    }

    void Awake() => _instance = this;

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    void Open()
    {
        TownSaveAlign.AlignAll();
        BuildIfNeeded();
        ReloadPicked();
        Refresh();
        if (_root != null)
        {
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
        }
        GameFonts.ApplyToHierarchy(transform);
    }

    void ReloadPicked()
    {
        _picked.Clear();
        var data = SaveSystem.Instance?.Data;
        if (data?.mercDrawPool == null) return;
        foreach (string hireId in data.mercDrawPool)
            if (!string.IsNullOrEmpty(hireId)) _picked.Add(hireId);
    }

    /// <summary>单次改动落盘 + 重读 + 全量刷新（改动即时生效，无「确定」步骤）。</summary>
    void Commit()
    {
        SaveSystem.Instance?.Save();
        ReloadPicked();
        Refresh();
    }

    void Close()
    {
        if (_replacePanel != null) _replacePanel.SetActive(false);
        if (_root != null) _root.SetActive(false);
    }

    // ============================================================
    // 预制体实例化：层级回绑 + 事件重绑
    // ============================================================

    /// <summary>
    /// 预制体实例化后调用：从层级按名回填字段 + 重绑画布（摄像机模式）+ 重绑按钮事件。
    /// 2026-10-15 主人拍板改预制体路线；私有 List 字段不进 prefab，全靠按名回找。
    /// </summary>
    void BindFromHierarchy()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas != null)
            UICanvasSetup.RefreshPopup(canvas, GameConfig.UiSort.TownPopup);

        _root = transform.Find("Root")?.gameObject;
        var panel = _root != null ? _root.transform.Find("Panel") : null;

        _poolCountText = panel != null ? panel.Find("top/PoolCount")?.GetComponent<Text>() : null;

        var poolGrid = panel != null ? panel.Find("top/PoolGrid") : null;
        if (poolGrid != null)
        {
            for (int i = 0; i < poolGrid.childCount; i++)
                _poolSlots.Add(poolGrid.GetChild(i).gameObject);
        }
        _slotHireIds = new string[_poolSlots.Count];

        _ownedContent = panel != null ? panel.Find("center/OwnedScroll/Content")?.GetComponent<RectTransform>() : null;
        _lockedContent = panel != null ? panel.Find("center/down/LockedScroll/Content")?.GetComponent<RectTransform>() : null;

        _replacePanel = panel != null ? panel.Find("ReplacePanel")?.gameObject : null;
        if (_replacePanel != null)
        {
            var box = _replacePanel.transform.Find("Box");
            _replaceTitle = box != null ? box.Find("RTitle")?.GetComponent<Text>() : null;
            _replaceContent = box != null ? box.Find("RViewport/RContent")?.GetComponent<RectTransform>() : null;
        }

        // 一条大滚动（2026-10-15 主人拍板）：top 池区的字和装饰、已拥有、未获得 全在同一滚动里。
        // 【教训】不能在实例化当帧收编 —— Canvas 布局还没跑，世界坐标量出来全是错的，
        // 整条列表会被摆飞裁没（主人实测「什么也没有了」）。推迟到首次 Refresh 再收编。
        _restructurePanel = panel;

        WireClicks();
        if (_root != null) _root.SetActive(false);
    }

    /// <summary>按钮事件重绑（预制体实例化路；prefab 序列化不存匿名监听，必须代码补绑）。</summary>
    void WireClicks()
    {
        if (_root == null) return;
        var panel = _root.transform.Find("Panel");
        if (panel == null) return;

        var dimBtn = _root.transform.Find("Dim")?.GetComponent<Button>();
        if (dimBtn != null)
        {
            dimBtn.onClick.RemoveAllListeners();
            dimBtn.onClick.AddListener(Close);
        }

        var closeBtn = panel.Find("BtnCloseX")?.GetComponent<Button>();
        if (closeBtn != null)
        {
            closeBtn.onClick.RemoveAllListeners();
            closeBtn.onClick.AddListener(Close);
        }

        for (int i = 0; i < _poolSlots.Count; i++)
        {
            // 预制体 SlotN 只有 Image（无 Button），运行时补：点格 = 看图鉴 / 锁格提示
            var slotImg = _poolSlots[i].GetComponent<Image>();
            if (slotImg != null) slotImg.raycastTarget = true;
            var btn = _poolSlots[i].GetComponent<Button>();
            if (btn == null) btn = _poolSlots[i].AddComponent<Button>();
            if (btn.targetGraphic == null && slotImg != null) btn.targetGraphic = slotImg;
            int idx = i;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnPoolSlotClick(idx));

            // quxiao = 下架钮（预制体也只有 Image，运行时补 Button；hireId 每次刷新记在 _slotHireIds）
            var qx = _poolSlots[i].transform.Find("quxiao");
            if (qx != null)
            {
                var qImg = qx.GetComponent<Image>();
                if (qImg != null) qImg.raycastTarget = true;
                var qBtn = qx.GetComponent<Button>();
                if (qBtn == null) qBtn = qx.gameObject.AddComponent<Button>();
                if (qBtn.targetGraphic == null && qImg != null) qBtn.targetGraphic = qImg;
                qBtn.onClick.RemoveAllListeners();
                qBtn.onClick.AddListener(() =>
                {
                    string hid = idx < _slotHireIds.Length ? _slotHireIds[idx] : null;
                    if (!string.IsNullOrEmpty(hid)) OnRemoveClick(hid);
                });
            }
        }

        if (_replacePanel != null)
        {
            var rpDim = _replacePanel.GetComponent<Button>();
            if (rpDim != null)
            {
                rpDim.onClick.RemoveAllListeners();
                rpDim.onClick.AddListener(() => _replacePanel.SetActive(false));
            }
            var rpBox = _replacePanel.transform.Find("Box")?.GetComponent<Button>();
            if (rpBox != null)
            {
                rpBox.onClick.RemoveAllListeners();
                rpBox.onClick.AddListener(() => { });
            }
            var rpCancel = _replacePanel.transform.Find("Box/RCancel")?.GetComponent<Button>();
            if (rpCancel != null)
            {
                rpCancel.onClick.RemoveAllListeners();
                rpCancel.onClick.AddListener(() => _replacePanel.SetActive(false));
            }
        }
    }

    // ============================================================
    // 一条大滚动（2026-10-15 主人拍板：top 池区的字和装饰、已拥有、未获得 全在同一滚动里；
    // 底框背景不滚；down/LockedScroll 停用。只挪父节点和落位，不改任何预制体节点的宽高/字号）
    // ============================================================

    class FlowNode { public RectTransform rt; public float x; public float topOffset; public float h; }
    readonly List<FlowNode> _flowTopNodes = new List<FlowNode>();
    readonly List<FlowNode> _flowOwnedHeadNodes = new List<FlowNode>();
    readonly List<FlowNode> _flowLockedHeadNodes = new List<FlowNode>();
    RectTransform _masterContent;
    Transform _restructurePanel;     // 待收编的 Panel（推迟到首次 Refresh，等布局跑过）
    bool _masterReady;
    float _flowOwnedCardsY;
    float _flowLockedCardsY;
    float _hTop;
    float _hOwnedHead;
    float _hLockedHead;
    const float GapSection = 24f;        // 大滚动里段与段的间隔
    const float HeadToCardsGap = 8f;     // 段头与其下卡片区的间隔
    const float ScrollSidePad = 16f;     // 滚动视口左右内缩（要调贴边改这里）
    const float ScrollBottomPad = 16f;   // 滚动视口底部内缩

    /// <summary>
    /// 收编成一条大滚动：OwnedScroll 视口扩到 Header 以下整块内容区，
    /// Content 依次收 top 池区 → 已拥有头 → (已拥有卡) → 未获得头 → (未获得卡)。
    /// 只改父节点和落位（两段式世界坐标校正），不改任何节点的宽高/字号。
    /// </summary>
    void RestructureMasterScroll(Transform panel)
    {
        // 布局先跑一遍：刚实例化/刚改锚点的节点，世界矩形要等 Canvas 更新才是真的
        Canvas.ForceUpdateCanvases();

        var center = panel.Find("center") as RectTransform;
        var scrollTr = center != null ? center.Find("OwnedScroll") : null;
        var down = center != null ? center.Find("down") : null;
        var top = panel.Find("top") as RectTransform;
        if (center == null || scrollTr == null || down == null || top == null)
        {
            Debug.LogError("[MercDrawPoolUI] 一条大滚动缺节点 center/OwnedScroll/top/down → 保持原两段滚动");
            return;
        }
        var viewport = scrollTr as RectTransform;
        var content = scrollTr.Find("Content") as RectTransform;
        var prt = panel as RectTransform;
        var header = panel.Find("Header") as RectTransform;
        if (viewport == null || content == null || prt == null)
        {
            Debug.LogError("[MercDrawPoolUI] OwnedScroll/Content 缺失 → 保持原两段滚动");
            return;
        }

        // 1) 先量：面板矩形 / Header 底（世界坐标），再挪视口
        var pc = new Vector3[4];
        prt.GetWorldCorners(pc);
        float headerBottomWorld = pc[1].y;
        if (header != null)
        {
            var hc = new Vector3[4];
            header.GetWorldCorners(hc);
            headerBottomWorld = hc[0].y;
        }
        float ky = prt.lossyScale.y;
        float headerHLocal = ky > 0.0001f ? (pc[1].y - headerBottomWorld) / ky : 0f;

        // 2) 视口挪到 Panel 下、插到 center 原位（ReplacePanel 仍在其上），顶格 Header 以下；
        //    自身 Image 置透明（Mask 仍生效），不再画底
        viewport.SetParent(panel, false);
        viewport.SetSiblingIndex(center.GetSiblingIndex());
        viewport.anchorMin = new Vector2(0f, 1f);
        viewport.anchorMax = new Vector2(1f, 1f);
        viewport.pivot = new Vector2(0.5f, 1f);
        viewport.offsetMin = new Vector2(ScrollSidePad, ScrollBottomPad);
        viewport.offsetMax = new Vector2(-ScrollSidePad, -(headerHLocal + 8f));
        var vimg = viewport.GetComponent<Image>();
        if (vimg != null)
        {
            vimg.sprite = null;
            var vc = vimg.color;
            vimg.color = new Color(vc.r, vc.g, vc.b, 0f);
            vimg.raycastTarget = true;
        }

        // 3) Content 归一成顶拉伸（宽随视口，顶边=视口顶）
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = new Vector2(0f, 0f);
        content.offsetMax = new Vector2(0f, 0f);
        content.anchoredPosition = new Vector2(0f, 0f);
        Canvas.ForceUpdateCanvases();   // 让视口/Content 的新矩形先落定，再量世界坐标收编

        // 4) 收编各段（先记原世界矩形再挪，簇内相对摆法原样保留）
        _masterContent = content;
        _hTop = CollectCluster(new[] { top }, content, _flowTopNodes, 0f);
        PlaceFlowCluster(_flowTopNodes, 0f);
        float yCur = _hTop + GapSection;
        _hOwnedHead = CollectCluster(new[]
        {
            center.Find("yiyongyou") as RectTransform,
            center.Find("OwnedHead") as RectTransform,
            center.Find("jiantou") as RectTransform,
            center.Find("PoolCount1") as RectTransform,
        }, content, _flowOwnedHeadNodes, yCur);
        PlaceFlowCluster(_flowOwnedHeadNodes, yCur);
        _flowOwnedCardsY = yCur + _hOwnedHead + HeadToCardsGap;
        float lockedHeadY0 = _flowOwnedCardsY;             // 初始估位；真实位随已拥有卡数每刷重算
        _hLockedHead = CollectCluster(new[]
        {
            down.Find("weiyongyou") as RectTransform,
            down.Find("LockedHead") as RectTransform,
        }, content, _flowLockedHeadNodes, lockedHeadY0);
        PlaceFlowCluster(_flowLockedHeadNodes, lockedHeadY0);
        _flowLockedCardsY = lockedHeadY0 + _hLockedHead + HeadToCardsGap;

        // 5) 旧的第二段滚动整个停用
        var lockedScroll = down.Find("LockedScroll");
        if (lockedScroll != null) lockedScroll.gameObject.SetActive(false);
        Canvas.ForceUpdateCanvases();   // 收编完再刷一遍，后面 BuildTemplateCards 量的才是最终位置
        _masterReady = true;
    }

    /// <summary>
    /// 把一串节点收编进 Content：先记原世界矩形，挪父后用两段式世界校正落位，
    /// 簇内相对摆法（x、上下相对距离）原样保留。返回簇高（Content 局部）。
    /// </summary>
    float CollectCluster(RectTransform[] nodes, RectTransform content, List<FlowNode> sink, float clusterFlowY)
    {
        sink.Clear();
        var cc = new Vector3[4];
        content.GetWorldCorners(cc);
        float contentLeftX = cc[0].x, contentTopY = cc[1].y;
        float kx = content.lossyScale.x, ky = content.lossyScale.y;
        if (kx < 0.0001f || ky < 0.0001f) return 0f;

        var recs = new List<(RectTransform rt, float leftX, float topY, float h, float w)>();
        float clusterTopWorld = float.MaxValue;
        for (int i = 0; i < nodes.Length; i++)
        {
            var rt = nodes[i];
            if (rt == null) continue;
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            recs.Add((rt, c[0].x, c[1].y, (c[1].y - c[0].y) / ky, (c[2].x - c[0].x) / kx));
            if (c[1].y < clusterTopWorld) clusterTopWorld = c[1].y;
        }
        if (recs.Count == 0) return 0f;
        float clusterTopLocal = (contentTopY - clusterTopWorld) / ky;

        for (int i = 0; i < recs.Count; i++)
        {
            var rt = recs[i].rt;
            rt.SetParent(content, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            // 【关键】拉伸锚点节点的 sizeDelta 是「偏移量」不是尺寸（可能是负数），
            // 挂点锚前必须把刚量到的世界尺寸烘成 sizeDelta，否则节点塌成负宽高直接消失。
            // 烘的是实测值，视觉宽高一分不变，不碰主人的排版参数。
            rt.sizeDelta = new Vector2(recs[i].w, recs[i].h);
            rt.anchoredPosition = Vector2.zero;            // provisional，下面按世界差校正
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            float targetXLocal = (recs[i].leftX - contentLeftX) / kx;
            float targetTopLocal = clusterFlowY + (contentTopY - recs[i].topY) / ky - clusterTopLocal;
            var worldDelta = new Vector2(cc[0].x + targetXLocal * kx - c[0].x,
                (cc[1].y - targetTopLocal * ky) - c[1].y);
            rt.anchoredPosition += (Vector2)content.InverseTransformVector(worldDelta);
            sink.Add(new FlowNode
            {
                rt = rt,
                x = targetXLocal,
                topOffset = targetTopLocal - clusterFlowY,
                h = recs[i].h,
            });
        }
        float h = 0f;
        for (int i = 0; i < sink.Count; i++)
            h = Mathf.Max(h, sink[i].topOffset + sink[i].h);
        return h;
    }

    /// <summary>每刷重摆一簇（x 与簇内偏移首次已记录，这里只更新落位 y）。</summary>
    void PlaceFlowCluster(List<FlowNode> nodes, float clusterFlowY)
    {
        if (!_masterReady || nodes.Count == 0 || _masterContent == null) return;
        var cc = new Vector3[4];
        _masterContent.GetWorldCorners(cc);
        float kx = _masterContent.lossyScale.x, ky = _masterContent.lossyScale.y;
        if (kx < 0.0001f || ky < 0.0001f) return;
        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            if (n.rt == null) continue;
            var c = new Vector3[4];
            n.rt.GetWorldCorners(c);
            float targetTopLocal = clusterFlowY + n.topOffset;
            var worldDelta = new Vector2(cc[0].x + n.x * kx - c[0].x,
                (cc[1].y - targetTopLocal * ky) - c[1].y);
            n.rt.anchoredPosition += (Vector2)_masterContent.InverseTransformVector(worldDelta);
        }
    }

    // ============================================================
    // 建树
    // ============================================================

    void BuildIfNeeded()
    {
        if (_root != null) return;

        var canvas = gameObject.GetComponent<Canvas>() ?? gameObject.AddComponent<Canvas>();
        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownPopup);

        _root = new GameObject("Root", typeof(RectTransform));
        _root.transform.SetParent(transform, false);
        Stretch(_root.GetComponent<RectTransform>());

        // 点暗处关闭
        var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image), typeof(Button));
        dim.transform.SetParent(_root.transform, false);
        Stretch(dim.GetComponent<RectTransform>());
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        dim.GetComponent<Button>().onClick.AddListener(Close);

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(_root.transform, false);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(PanelW, PanelH);
        // 面板底板：复用项目通用窗体1（九宫格 Sliced，大尺寸拉伸不变形）；缺图才回落纯色
        var panelImg = panel.GetComponent<Image>();
        var winSp = Art(ArtCommon + "通用窗体1");
        if (winSp != null)
        {
            panelImg.sprite = winSp;
            panelImg.type = Image.Type.Sliced;
            panelImg.color = Color.white;
        }
        else
        {
            panelImg.color = new Color(0.13f, 0.10f, 0.08f, 0.98f);
        }

        // 窗体标头：复用通用窗体标头1（横向九宫格，只左右不变、竖向拉伸）
        var header = new GameObject("Header", typeof(RectTransform), typeof(Image));
        header.transform.SetParent(panel.transform, false);
        var hrt = header.GetComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0f, 1f);
        hrt.anchorMax = new Vector2(1f, 1f);
        hrt.pivot = new Vector2(0.5f, 1f);
        hrt.anchoredPosition = new Vector2(0f, -6f);
        hrt.sizeDelta = new Vector2(-40f, 64f);
        var hImg = header.GetComponent<Image>();
        var hSp = Art(ArtCommon + "通用窗体标头1");
        if (hSp != null)
        {
            hImg.sprite = hSp;
            hImg.type = Image.Type.Sliced;
            hImg.color = Color.white;
        }
        else
        {
            hImg.color = new Color(0.25f, 0.20f, 0.14f, 1f);
        }
        header.transform.SetAsFirstSibling();

        // 标题
        var title = MakeText(panel.transform, "Title", "酒馆 · 佣兵", 28);
        Anchor(title.rectTransform, new Vector2(0.04f, 0.955f), new Vector2(0.96f, 0.995f));

        // 右上角 X：复用 Common/关闭.png（与 MercenaryRecruitPopup 同款）
        var xBtn = new GameObject("BtnCloseX", typeof(RectTransform), typeof(Image), typeof(Button));
        xBtn.transform.SetParent(panel.transform, false);
        var xrt = xBtn.GetComponent<RectTransform>();
        xrt.anchorMin = xrt.anchorMax = new Vector2(1f, 1f);
        xrt.pivot = new Vector2(1f, 1f);
        xrt.anchoredPosition = new Vector2(-10f, -10f);
        xrt.sizeDelta = new Vector2(48f, 48f);
        var xImg = xBtn.GetComponent<Image>();
        var xSp = Art(ArtCommon + "关闭");
        if (xSp != null)
        {
            xImg.sprite = xSp;
            xImg.type = Image.Type.Simple;
            xImg.color = Color.white;
        }
        xBtn.GetComponent<Button>().onClick.AddListener(Close);

        // —— 区一：招募池（6 格一行，初期 4 格）——
        // 卷轴图标：复用 icon_hire_scroll（点题「抽奖池」）
        var scrollIco = NewImage(panel.transform, "ScrollIco", Color.white);
        var sirt = scrollIco.rectTransform;
        sirt.anchorMin = sirt.anchorMax = new Vector2(0f, 1f);
        sirt.pivot = new Vector2(0f, 1f);
        sirt.anchoredPosition = new Vector2(24f, -80f);
        sirt.sizeDelta = new Vector2(28f, 28f);
        var scrollSp = Art("Art/UI/Icons/Common/icon_hire_scroll");
        scrollIco.sprite = scrollSp;
        scrollIco.preserveAspect = true;
        scrollIco.enabled = scrollSp != null;

        var poolHead = MakeText(panel.transform, "PoolHead", "招募池", 22);
        poolHead.alignment = TextAnchor.MiddleLeft;
        poolHead.color = new Color(1f, 0.9f, 0.6f);
        Anchor(poolHead.rectTransform, new Vector2(0.095f, 0.905f), new Vector2(0.5f, 0.948f));
        _poolCountText = MakeText(panel.transform, "PoolCount", "", 17);
        _poolCountText.alignment = TextAnchor.MiddleRight;
        Anchor(_poolCountText.rectTransform, new Vector2(0.5f, 0.905f), new Vector2(0.96f, 0.948f));

        var poolGrid = new GameObject("PoolGrid", typeof(RectTransform));
        poolGrid.transform.SetParent(panel.transform, false);
        var pgrt = poolGrid.GetComponent<RectTransform>();
        pgrt.anchorMin = new Vector2(0.5f, 1f);
        pgrt.anchorMax = new Vector2(0.5f, 1f);
        pgrt.pivot = new Vector2(0.5f, 1f);
        pgrt.anchoredPosition = new Vector2(0f, -122f);
        pgrt.sizeDelta = new Vector2(PoolCols * PoolSlotW + (PoolCols - 1) * GridGap, PoolSlotH);
        for (int i = 0; i < SaveData.DrawPoolMaxSlots; i++)
        {
            int col = i % PoolCols;
            var slot = new GameObject("Slot" + i, typeof(RectTransform), typeof(Image), typeof(Button));
            slot.transform.SetParent(poolGrid.transform, false);
            var srt = slot.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0f, 0f);
            srt.anchorMax = new Vector2(0f, 0f);
            srt.pivot = new Vector2(0f, 0f);
            srt.anchoredPosition = new Vector2(col * (PoolSlotW + GridGap), 0f);
            srt.sizeDelta = new Vector2(PoolSlotW, PoolSlotH);
            slot.GetComponent<Image>().color = new Color(0.18f, 0.14f, 0.11f, 1f);
            int idx = i;
            slot.GetComponent<Button>().onClick.AddListener(() => OnPoolSlotClick(idx));
            _poolSlots.Add(slot);
        }

        // —— 区二：已获得（滚动，按稀有度分组）——
        var ownedHead = MakeText(panel.transform, "OwnedHead", "已获得佣兵", 22);
        ownedHead.alignment = TextAnchor.MiddleLeft;
        ownedHead.color = new Color(1f, 0.9f, 0.6f);
        Anchor(ownedHead.rectTransform, new Vector2(0.04f, 0.745f), new Vector2(0.96f, 0.79f));
        BuildScroll(panel.transform, "OwnedScroll",
            new Vector2(0.04f, 0.452f), new Vector2(0.96f, 0.742f), out _ownedContent);

        // —— 区三：未获得（滚动，按稀有度分组）——
        var lockedHead = MakeText(panel.transform, "LockedHead", "未获得佣兵", 22);
        lockedHead.alignment = TextAnchor.MiddleLeft;
        lockedHead.color = new Color(0.75f, 0.75f, 0.8f);
        Anchor(lockedHead.rectTransform, new Vector2(0.04f, 0.412f), new Vector2(0.96f, 0.452f));
        BuildScroll(panel.transform, "LockedScroll",
            new Vector2(0.04f, 0.075f), new Vector2(0.96f, 0.412f), out _lockedContent);

        // 底部：关闭（改动即时生效，无「确定」）—— 钮底复用取消按钮
        var closeBtn = MakeBtn(panel.transform, "Close", "关闭", new Vector2(0.5f, 0.028f), Close);
        SetBtnSprite(closeBtn, ArtCommon + "取消按钮");

        // —— 替换弹窗（池满时点已获得佣兵 → 选池中谁换下）——
        BuildReplacePanel(panel.transform);

        _root.SetActive(false);
    }

    void BuildReplacePanel(Transform parent)
    {
        _replacePanel = new GameObject("ReplacePanel", typeof(RectTransform), typeof(Image));
        _replacePanel.transform.SetParent(parent, false);
        Stretch(_replacePanel.GetComponent<RectTransform>());
        _replacePanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        // 遮罩不挡关闭主弹窗：点遮罩 = 取消替换
        var dimBtn = _replacePanel.AddComponent<Button>();
        dimBtn.onClick.AddListener(() => _replacePanel.SetActive(false));

        var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(_replacePanel.transform, false);
        var brt = box.GetComponent<RectTransform>();
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.sizeDelta = new Vector2(520f, 560f);
        // 底板复用通用窗体1（Sliced）
        var boxImg = box.GetComponent<Image>();
        var boxSp = Art(ArtCommon + "通用窗体1");
        if (boxSp != null)
        {
            boxImg.sprite = boxSp;
            boxImg.type = Image.Type.Sliced;
            boxImg.color = Color.white;
        }
        else
        {
            boxImg.color = new Color(0.15f, 0.12f, 0.09f, 0.99f);
        }
        // 挡住穿透：Box 自己不吃点击但要在遮罩 Button 之上拦截射线
        box.AddComponent<Button>().onClick.AddListener(() => { });

        _replaceTitle = MakeText(box.transform, "RTitle", "替换池中的佣兵", 24);
        Anchor(_replaceTitle.rectTransform, new Vector2(0.05f, 0.9f), new Vector2(0.95f, 0.97f));

        var tip = MakeText(box.transform, "RTip", "点选要换下的佣兵，换上刚点的这位", 16);
        tip.color = new Color(1f, 1f, 1f, 0.65f);
        Anchor(tip.rectTransform, new Vector2(0.05f, 0.84f), new Vector2(0.95f, 0.9f));

        var viewport = new GameObject("RViewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(box.transform, false);
        var vrt = viewport.GetComponent<RectTransform>();
        vrt.anchorMin = new Vector2(0.04f, 0.14f);
        vrt.anchorMax = new Vector2(0.96f, 0.83f);
        vrt.offsetMin = vrt.offsetMax = Vector2.zero;
        viewport.GetComponent<Image>().color = new Color(0.09f, 0.07f, 0.06f, 0.9f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        var cont = new GameObject("RContent", typeof(RectTransform));
        cont.transform.SetParent(viewport.transform, false);
        var crt = cont.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 1f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.offsetMin = new Vector2(GridPad, 0f);
        crt.offsetMax = new Vector2(-GridPad, 0f);
        crt.sizeDelta = new Vector2(0f, 300f);
        _replaceContent = crt;

        var scroll = viewport.AddComponent<ScrollRect>();
        scroll.content = crt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        var rcBtn = MakeBtn(box.transform, "RCancel", "取消", new Vector2(0.5f, 0.055f), () => _replacePanel.SetActive(false));
        SetBtnSprite(rcBtn, ArtCommon + "取消按钮");

        _replacePanel.SetActive(false);
    }

    /// <summary>滚动区（ScrollRect + Mask）。</summary>
    static void BuildScroll(Transform parent, string name, Vector2 min, Vector2 max, out RectTransform content)
    {
        var viewport = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(parent, false);
        var vrt = viewport.GetComponent<RectTransform>();
        vrt.anchorMin = min;
        vrt.anchorMax = max;
        vrt.offsetMin = vrt.offsetMax = Vector2.zero;
        viewport.GetComponent<Image>().color = new Color(0.09f, 0.07f, 0.06f, 0.9f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        var cont = new GameObject("Content", typeof(RectTransform));
        cont.transform.SetParent(viewport.transform, false);
        var crt = cont.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 1f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.offsetMin = new Vector2(GridPad, 0f);
        crt.offsetMax = new Vector2(-GridPad, 0f);
        crt.sizeDelta = new Vector2(0f, 400f);
        content = crt;

        var scroll = viewport.AddComponent<ScrollRect>();
        scroll.content = crt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
    }

    // ============================================================
    // 刷新
    // ============================================================

    void Refresh()
    {
        // 首次刷新才收编大滚动（此时至少跑过一次 Canvas 布局，世界坐标才可信）
        if (!_masterReady && _restructurePanel != null)
        {
            RestructureMasterScroll(_restructurePanel);
            _restructurePanel = null;
        }

        var data = SaveSystem.Instance?.Data;
        int slots = data != null ? data.DrawPoolSlots : SaveData.DrawPoolDefaultSlots;

        if (_poolCountText != null)
            _poolCountText.text = "当前可获得 " + _picked.Count + " / " + SaveData.DrawPoolMaxSlots + " 名佣兵";

        // 池子按稀有度自动排列（主人拍板）：传说 → 稀有 → 普通，同档按花名册表序
        var ordered = SortByRarity(_picked);

        if (_slotHireIds == null || _slotHireIds.Length != _poolSlots.Count)
            _slotHireIds = new string[_poolSlots.Count];
        for (int i = 0; i < _poolSlots.Count; i++)
        {
            bool open = i < slots;
            string hireId = i < ordered.Count ? ordered[i] : null;
            _slotHireIds[i] = hireId;
            RefreshPoolSlot(_poolSlots[i], i, open, hireId);
        }

        RefreshOwned();
        RefreshLocked();
    }

    void RefreshPoolSlot(GameObject slot, int index, bool open, string hireId)
    {
        // 预制体格（有卡组模板结构）走填充，不销毁子节点；回落建树格才走旧逻辑
        if (slot.transform.Find("Iconmask") != null)
        {
            RefreshPrefabPoolSlot(slot, open, hireId);
            return;
        }

        for (int c = slot.transform.childCount - 1; c >= 0; c--)
            Destroy(slot.transform.GetChild(c).gameObject);
        var bg = slot.GetComponent<Image>();
        var btn = slot.GetComponent<Button>();

        if (!open)
        {
            bg.color = new Color(0.11f, 0.09f, 0.08f, 1f);
            btn.interactable = true;   // 点了有提示，不让点击石沉大海
            // 锁图标：复用 Common/锁.png（36×55，preserveAspect）
            var lockIco = NewImage(slot.transform, "LockIco", Color.white);
            var lirt = lockIco.rectTransform;
            lirt.anchorMin = lirt.anchorMax = new Vector2(0.5f, 0.5f);
            lirt.pivot = new Vector2(0.5f, 0.5f);
            lirt.anchoredPosition = new Vector2(0f, 22f);
            lirt.sizeDelta = new Vector2(24f, 36f);
            var lockSp = Art(ArtCommon + "锁");
            lockIco.sprite = lockSp;
            lockIco.preserveAspect = true;
            lockIco.enabled = lockSp != null;
            var lockT = MakeText(slot.transform, "Locked", "暂未开启", 13);
            lockT.color = new Color(1f, 1f, 1f, 0.35f);
            var lockTip = MakeText(slot.transform, "LockTip", LockHintOf(index), 11);
            lockTip.color = new Color(1f, 1f, 1f, 0.25f);
            var lrt = lockTip.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0f);
            lrt.anchorMax = new Vector2(1f, 0.32f);
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            return;
        }

        if (string.IsNullOrEmpty(hireId))
        {
            bg.color = new Color(0.18f, 0.14f, 0.11f, 1f);
            btn.interactable = true;
            var empty = MakeText(slot.transform, "Empty", "空格", 13);
            empty.color = new Color(1f, 1f, 1f, 0.30f);
            return;
        }

        MercRosterDefs.TryGetByHireId(hireId, out var def);
        bg.color = new Color(0.24f, 0.19f, 0.13f, 1f);
        BuildMercFace(slot.transform, hireId, def, def.Name, true);

        // 红「−」下架按钮（右上角，主人设计图）
        var minus = MakeBtn(slot.transform, "Remove", "−", new Vector2(1f, 1f), () => OnRemoveClick(hireId));
        var mrt = RtOf(minus);
        mrt.anchorMin = new Vector2(1f, 1f);
        mrt.anchorMax = new Vector2(1f, 1f);
        mrt.pivot = new Vector2(1f, 1f);
        mrt.anchoredPosition = new Vector2(-2f, -2f);
        mrt.sizeDelta = new Vector2(24f, 24f);
        minus.GetComponent<Image>().color = new Color(0.78f, 0.22f, 0.22f, 1f);
        var mlabel = minus.transform.Find("Label")?.GetComponent<Text>();
        if (mlabel != null) mlabel.fontSize = 18;
    }

    /// <summary>
    /// 预制体池格填充（2026-10-15 主人重做预制体后）：不销毁子节点，按状态填
    /// 头像 / 名字 / Role / quxiao / suo + 稀有度框；空格与锁格清空卡面。
    /// </summary>
    void RefreshPrefabPoolSlot(GameObject slot, bool open, string hireId)
    {
        EnsurePoolSlotRole(slot);

        var icon = slot.transform.Find("Iconmask/Icon")?.GetComponent<Image>();
        var nameT = slot.transform.Find("namebg/Name")?.GetComponent<Text>();
        var qxTr = slot.transform.Find("quxiao");
        var suoTr = slot.transform.Find("suo");

        bool filled = open && !string.IsNullOrEmpty(hireId);
        if (!filled)
        {
            if (icon != null)
            {
                icon.sprite = null;
                icon.enabled = false;
                icon.color = Color.white;
            }
            if (nameT != null) nameT.text = open ? "空格" : "暂未开启";
            HideRoleIcon(slot.transform);
            if (qxTr != null) qxTr.gameObject.SetActive(false);
            if (suoTr != null) suoTr.gameObject.SetActive(!open);
            return;
        }

        MercRosterDefs.TryGetByHireId(hireId, out var def);
        var sp = MercPortraitSprites.GetHead(hireId);
        if (sp == null && !string.IsNullOrEmpty(def.AssetId)) sp = MercPortraitSprites.GetHead(def.AssetId);
        if (icon != null)
        {
            icon.sprite = sp;
            icon.enabled = sp != null;
            icon.color = Color.white;
        }
        if (nameT != null) nameT.text = def.Name;
        ApplyRoleIcon(slot.transform, def, true);
        if (qxTr != null) qxTr.gameObject.SetActive(true);
        if (suoTr != null) suoTr.gameObject.SetActive(false);
    }

    /// <summary>池格缺 Role（Slot1-4）时运行时从模板 Slot0 克隆一份，布局参数原样复制。</summary>
    void EnsurePoolSlotRole(GameObject slot)
    {
        if (slot.transform.Find("Role") != null) return;
        var tmpl = CardTemplate();
        if (tmpl == null) return;
        var src = tmpl.transform.Find("Role");
        if (src == null) return;
        var clone = Object.Instantiate(src.gameObject);
        clone.name = "Role";
        clone.transform.SetParent(slot.transform, false);
        var srt = src as RectTransform;
        var crt = clone.GetComponent<RectTransform>();
        if (srt != null && crt != null)
        {
            crt.anchorMin = srt.anchorMin;
            crt.anchorMax = srt.anchorMax;
            crt.pivot = srt.pivot;
            crt.anchoredPosition = srt.anchoredPosition;
            crt.sizeDelta = srt.sizeDelta;
            crt.localScale = srt.localScale;
        }
    }

    /// <summary>锁格提示（第 4 格 = 通关第 2 章、第 5 格 = 通关第 3 章，条件在 SaveData.DrawPoolSlots）。</summary>
    static string LockHintOf(int index)
    {
        return index == 3 ? "通关第 2 章" : index == 4 ? "通关第 3 章" : "";
    }

    void RefreshOwned()
    {
        ClearCards(_ownedCards);
        var data = SaveSystem.Instance?.Data;
        if (data?.unlockedMercIds == null) return;

        // 按稀有度排序（传说 → 稀有 → 普通），不再插组头（2026-10-15 主人拍板）
        var ordered = SortByRarity(data.unlockedMercIds);
        float end = BuildTemplateCards(_masterContent != null ? _masterContent : _ownedContent,
            ordered, true, _flowOwnedCardsY);
        // 一条大滚动：未获得段头跟着已拥有卡的结束位置往下挪（卡数变化每刷重摆）
        if (_masterReady)
        {
            PlaceFlowCluster(_flowLockedHeadNodes, end + GapSection);
            _flowLockedCardsY = end + GapSection + _hLockedHead + HeadToCardsGap;
        }
    }

    void RefreshLocked()
    {
        ClearCards(_lockedCards);
        var data = SaveSystem.Instance?.Data;
        IReadOnlyList<MercRosterDefs.Def> all = MercRosterDefs.All;
        var ids = new List<string>();
        for (int i = 0; i < all.Count; i++)
        {
            var def = all[i];
            if (string.IsNullOrEmpty(def.HireId)) continue;
            if (data != null && data.unlockedMercIds != null && data.unlockedMercIds.Contains(def.HireId)) continue;
            // 剧情 NPC 不进图鉴视野（与局内池同口径）
            if (GameConfig.GetMercTier(def.AssetId) == MercTier.Npc) continue;
            ids.Add(def.HireId);
        }

        var ordered = SortByRarity(new HashSet<string>(ids));
        float end = BuildTemplateCards(_masterContent != null ? _masterContent : _lockedContent,
            ordered, false, _flowLockedCardsY);
        LayoutContent(_masterContent != null ? _masterContent : _lockedContent, end);
    }

    /// <summary>
    /// 一组（组头 + 3 列头像卡）。<paramref name="memberIds"/> = 该组归属判定用的 id 集
    /// （已获得区传 unlockedMercIds，未获得区传「未解锁且非 NPC」集合）。
    /// owned = 已获得（可上架），false = 未获得（灰卡）。返回占用高度。
    /// </summary>
    float BuildGroup(RectTransform content, string headLabel, MercRosterDefs.MercRarity rarity,
        HashSet<string> memberIds, bool owned)
    {
        IReadOnlyList<MercRosterDefs.Def> all = MercRosterDefs.All;
        var ids = new List<string>();
        for (int i = 0; i < all.Count; i++)
        {
            var d = all[i];
            if (d.Rarity != rarity || string.IsNullOrEmpty(d.HireId)) continue;
            if (!memberIds.Contains(d.HireId)) continue;
            ids.Add(d.HireId);
        }
        if (ids.Count == 0) return 0f;

        float used = 0f;

        Color headColor = RarityPalette.Get(
            rarity == MercRosterDefs.MercRarity.Legendary ? SkillRarity.Legendary
            : rarity == MercRosterDefs.MercRarity.Rare ? SkillRarity.Rare
            : SkillRarity.Common);

        var head = MakeText(content, "GroupHead_" + headLabel, "◆ " + headLabel, 19);
        head.alignment = TextAnchor.MiddleLeft;
        head.color = headColor;
        var hrt = head.rectTransform;
        hrt.anchorMin = new Vector2(0f, 1f);
        hrt.anchorMax = new Vector2(1f, 1f);
        hrt.pivot = new Vector2(0f, 1f);
        hrt.anchoredPosition = new Vector2(0f, -used);
        hrt.sizeDelta = new Vector2(0f, GroupHeadH);

        float y = used + GroupHeadH;
        for (int i = 0; i < ids.Count; i++)
        {
            int col = i % GridCols;
            int row = i / GridCols;
            MercRosterDefs.TryGetByHireId(ids[i], out var def);
            GameObject card = owned ? BuildOwnedCard(ids[i], def) : BuildLockedCard(ids[i], def);
            var crt = card.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(0f, 1f);
            crt.pivot = new Vector2(0f, 1f);
            crt.anchoredPosition = new Vector2(col * (CardW + GridGap), -(y + row * (CardH + GridGap)));
        }
        int rows = (ids.Count + GridCols - 1) / GridCols;
        return y - used + rows * (CardH + GridGap);
    }

    // ============================================================
    // 卡片构建（统一头像，主人拍板）
    // ============================================================

    /// <summary>
    /// 头像脸：稀有度头像框（跟头像同区，不铺全卡防拉变形）+ 头像 + 名字 + 职业徽记（左上）。
    /// usedForPool = 池格（更高瘦）。
    /// </summary>
    void BuildMercFace(Transform parent, string hireId, MercRosterDefs.Def def, string nameLabel, bool usedForPool)
    {
        var frame = NewImage(parent, "Frame", Color.white);
        var frt = frame.rectTransform;
        frt.anchorMin = new Vector2(0.5f, 1f);
        frt.anchorMax = new Vector2(0.5f, 1f);
        frt.pivot = new Vector2(0.5f, 1f);
        var frameSp = MercHireSession.LoadPortraitFrame(def.Rarity);
        frame.sprite = frameSp;
        frame.enabled = frameSp != null;
        var head = NewImage(parent, "Head", Color.white);
        var hrt = head.rectTransform;
        hrt.anchorMin = new Vector2(0.5f, 1f);
        hrt.anchorMax = new Vector2(0.5f, 1f);
        hrt.pivot = new Vector2(0.5f, 1f);
        if (usedForPool)
        {
            frt.anchoredPosition = new Vector2(0f, -22f);
            frt.sizeDelta = new Vector2(88f, 88f);
            hrt.anchoredPosition = new Vector2(0f, -26f);
            hrt.sizeDelta = new Vector2(72f, 72f);
        }
        else
        {
            frt.anchoredPosition = new Vector2(0f, -6f);
            frt.sizeDelta = new Vector2(136f, 116f);
            hrt.anchoredPosition = new Vector2(0f, -10f);
            hrt.sizeDelta = new Vector2(120f, 100f);
        }
        var sp = MercPortraitSprites.GetHead(hireId);
        if (sp == null && !string.IsNullOrEmpty(def.AssetId)) sp = MercPortraitSprites.GetHead(def.AssetId);
        head.sprite = sp;
        head.preserveAspect = true;
        head.enabled = sp != null;

        var nameT = MakeText(parent, "Name", nameLabel, usedForPool ? 13 : 17);
        var nrt = nameT.rectTransform;
        nrt.anchorMin = new Vector2(0f, 0f);
        nrt.anchorMax = new Vector2(1f, 0f);
        nrt.pivot = new Vector2(0.5f, 0f);
        nrt.offsetMin = new Vector2(2f, 4f);
        nrt.offsetMax = new Vector2(-2f, usedForPool ? 26f : 30f);

        var badge = NewImage(parent, "Badge", Color.white);
        var brt = badge.rectTransform;
        brt.anchorMin = new Vector2(0f, 1f);
        brt.anchorMax = new Vector2(0f, 1f);
        brt.pivot = new Vector2(0f, 1f);
        brt.anchoredPosition = new Vector2(5f, -5f);
        brt.sizeDelta = new Vector2(30f, 30f);
        var badgeSp = MercGrowSprites.LoadJobBadge(def.JobName, def.Rarity);
        badge.sprite = badgeSp;
        badge.enabled = badgeSp != null;
    }

    GameObject BuildOwnedCard(string hireId, MercRosterDefs.Def def)
    {
        var card = new GameObject("OwnedCard", typeof(RectTransform), typeof(Image), typeof(Button));
        card.transform.SetParent(_ownedContent, false);
        card.GetComponent<RectTransform>().sizeDelta = new Vector2(CardW, CardH);
        var bg = card.GetComponent<Image>();
        bg.color = new Color(0.17f, 0.13f, 0.10f, 1f);
        var btn = card.GetComponent<Button>();
        btn.targetGraphic = bg;

        BuildMercFace(card.transform, hireId, def, def.Name, false);

        // 在池标识：头像右上角绿「已上架」（主人拍板）
        bool inPool = _picked.Contains(hireId);
        var mark = MakeText(card.transform, "InPool", "已上架", 13);
        var mrt = mark.rectTransform;
        mrt.anchorMin = new Vector2(1f, 1f);
        mrt.anchorMax = new Vector2(1f, 1f);
        mrt.pivot = new Vector2(1f, 1f);
        mrt.anchoredPosition = new Vector2(-4f, -4f);
        mrt.sizeDelta = new Vector2(52f, 22f);
        mark.color = inPool ? RarityPalette.PowerGain : new Color(1f, 1f, 1f, 0.12f);

        // 详情小按钮（右下）
        var info = MakeBtn(card.transform, "Info", "?", new Vector2(1f, 0f), () => ShowMercCodex(hireId, def));
        var irt = RtOf(info);
        irt.anchorMin = new Vector2(1f, 0f);
        irt.anchorMax = new Vector2(1f, 0f);
        irt.pivot = new Vector2(1f, 0f);
        irt.anchoredPosition = new Vector2(-4f, 6f);
        irt.sizeDelta = new Vector2(38f, 32f);

        // 升级入口（本期占位，主人拍板「先不升级 先留出位置和接口」）
        var grow = MakeBtn(card.transform, "Grow", "养成", new Vector2(0f, 0f), () =>
        {
            UIManager.Instance?.ShowToast("佣兵养成即将开放");
        });
        var grt = RtOf(grow);
        grt.anchorMin = new Vector2(0f, 0f);
        grt.anchorMax = new Vector2(0f, 0f);
        grt.pivot = new Vector2(0f, 0f);
        grt.anchoredPosition = new Vector2(4f, 6f);
        grt.sizeDelta = new Vector2(56f, 32f);
        grow.GetComponent<Image>().color = new Color(0.25f, 0.22f, 0.18f, 1f);

        string hid = hireId;
        btn.onClick.AddListener(() => OnOwnedCardClick(hid));
        _ownedCards.Add(card);
        return card;
    }

    GameObject BuildLockedCard(string hireId, MercRosterDefs.Def def)
    {
        var card = new GameObject("LockedCard", typeof(RectTransform), typeof(Image), typeof(Button));
        card.transform.SetParent(_lockedContent, false);
        card.GetComponent<RectTransform>().sizeDelta = new Vector2(CardW, CardH);
        var bg = card.GetComponent<Image>();
        bg.color = new Color(0.13f, 0.11f, 0.10f, 1f);
        var btn = card.GetComponent<Button>();
        btn.targetGraphic = bg;

        BuildMercFace(card.transform, hireId, def, "未获得", false);

        var head = card.transform.Find("Head")?.GetComponent<Image>();
        if (head != null) head.color = new Color(0.40f, 0.40f, 0.40f, 0.9f);
        var frame = card.transform.Find("Frame")?.GetComponent<Image>();
        if (frame != null) frame.color = new Color(0.5f, 0.5f, 0.5f, 0.8f);

        string hid = hireId;
        btn.onClick.AddListener(() => ShowMercCodex(hid, def));
        _lockedCards.Add(card);
        return card;
    }

    // ============================================================
    // 模板克隆建卡（2026-10-15 主人拍板：已拥有/未拥有直接复制 top 卡组模板）
    // ============================================================

    /// <summary>卡组模板（top/PoolGrid/Slot0，按名找不到就取第一格）。</summary>
    GameObject CardTemplate()
    {
        for (int i = 0; i < _poolSlots.Count; i++)
            if (_poolSlots[i] != null && _poolSlots[i].name == "Slot0") return _poolSlots[i];
        return _poolSlots.Count > 0 ? _poolSlots[0] : null;
    }

    /// <summary>
    /// 模板卡在列表里的排版尺寸：**世界坐标实测**（模板当前世界矩形 ÷ Content 的 lossyScale），
    /// 不用 sizeDelta×localScale 推算 —— 主人在预制体里怎么摆（含缩放）就量出什么样，代码不反推不改。
    /// </summary>
    static Vector2 MeasureInContent(RectTransform rt, RectTransform content)
    {
        if (rt == null || content == null) return new Vector2(CardW, CardH);
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        float kx = content.lossyScale.x, ky = content.lossyScale.y;
        if (kx < 0.0001f || ky < 0.0001f) return new Vector2(CardW, CardH);
        return new Vector2((c[2].x - c[0].x) / kx, (c[1].y - c[0].y) / ky);
    }

    /// <summary>名字牌（namebg）伸出卡底的距离（Content 局部；没伸出返回 0）—— 行距要用它，否则下一行压名字。</summary>
    static float TemplateNameDrop(GameObject tmpl, RectTransform content)
    {
        var nb = tmpl != null ? tmpl.transform.Find("namebg") as RectTransform : null;
        var rt = tmpl != null ? tmpl.GetComponent<RectTransform>() : null;
        if (nb == null || rt == null || content == null) return 0f;
        var cb = new Vector3[4];
        nb.GetWorldCorners(cb);
        var cc = new Vector3[4];
        rt.GetWorldCorners(cc);
        float dropWorld = cc[0].y - cb[0].y;              // 卡底 y − 名字牌底 y，>0 = 伸出卡底
        float ky = content.lossyScale.y;
        if (dropWorld <= 0f || ky < 0.0001f) return 0f;
        return dropWorld / ky;
    }

    /// <summary>
    /// 列距：直接量 top 池 Slot0→Slot1 的实际横向间距（与主人摆的 5 格同口径），
    /// 量不到就退回 卡宽 + GridGap。
    /// </summary>
    static float TemplateColPitch(GameObject tmpl, RectTransform content, float fallback)
    {
        var s0 = tmpl != null ? tmpl.transform as RectTransform : null;
        var s1 = tmpl != null && tmpl.transform.parent != null
            ? tmpl.transform.parent.Find("Slot1") as RectTransform : null;
        if (s0 == null || s1 == null || content == null) return fallback;
        var c0 = new Vector3[4];
        s0.GetWorldCorners(c0);
        var c1 = new Vector3[4];
        s1.GetWorldCorners(c1);
        float kx = content.lossyScale.x;
        if (kx < 0.0001f) return fallback;
        float pitch = Mathf.Abs(c1[0].x - c0[0].x) / kx;
        return pitch > 1f ? pitch : fallback;
    }

    /// <summary>
    /// 克隆模板建一列表卡：按稀有度排好序传入，**5 列**网格（与 top 池同宽同口径，整行居中），
    /// 行距 = 卡高 + 名字牌下垂量 + 间隔，下一行绝不压上一行名字牌（2026-10-15 主人要求）。
    /// startY = 本段在 Content 里的起始 y（一条大滚动用）。返回段落结束 y。
    /// </summary>
    float BuildTemplateCards(RectTransform content, List<string> orderedIds, bool owned, float startY)
    {
        if (content == null || orderedIds == null || orderedIds.Count == 0) return startY;
        var tmpl = CardTemplate();
        if (tmpl == null || tmpl.transform.Find("Iconmask") == null)
        {
            Debug.LogError("[MercDrawPoolUI] 找不到卡组模板 top/PoolGrid/Slot0（含 Iconmask），列表无法建卡");
            return startY;
        }

        var size = MeasureInContent(tmpl.GetComponent<RectTransform>(), content);
        float nameDrop = TemplateNameDrop(tmpl, content);
        float pitchX = TemplateColPitch(tmpl, content, size.x + GridGap);
        float rowPitch = size.y + nameDrop + GridGap;     // 行距含名字牌伸出量，行与行留 GridGap 空隙
        int cols = GridCols;
        float cw = content.rect.width;
        if (cw < 100f) cw = 690f;                          // 布局未跑时按面板内容宽兜底（只影响首帧居中）
        float rowW = (cols - 1) * pitchX + size.x;
        float x0 = Mathf.Max(0f, (cw - rowW) * 0.5f);
        for (int i = 0; i < orderedIds.Count; i++)
        {
            int col = i % cols;
            int row = i / cols;
            MercRosterDefs.TryGetByHireId(orderedIds[i], out var def);
            var card = Object.Instantiate(tmpl);
            card.name = owned ? "OwnedCard" : "LockedCard";
            card.transform.SetParent(content, false);
            FillTemplateCard(card, orderedIds[i], def, owned);
            var crt = card.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(0f, 1f);
            crt.pivot = new Vector2(0f, 1f);
            crt.anchoredPosition = new Vector2(x0 + col * pitchX, -(startY + row * rowPitch));
            if (owned) _ownedCards.Add(card);
            else _lockedCards.Add(card);
        }
        int rows = (orderedIds.Count + cols - 1) / cols;
        return startY + (rows - 1) * rowPitch + size.y + nameDrop;
    }

    /// <summary>
    /// 模板克隆卡填数据：隐藏池专用 quxiao / suo；头像（GetHead，null 报错不造图）、
    /// 名字、Role 四分类图标（T5）、点击事件（口径沿用旧卡）。
    /// 【2026-10-15 主人拍板】卡上框美术主人预制体里已摆好（Slot0 本身就是头像框位），
    /// 代码不再生成/贴任何 Tavern 框（T4 的运行时贴框已整段撤销）。
    /// </summary>
    void FillTemplateCard(GameObject card, string hireId, MercRosterDefs.Def def, bool owned)
    {
        var qx = card.transform.Find("quxiao");
        if (qx != null) qx.gameObject.SetActive(false);
        var suo = card.transform.Find("suo");
        if (suo != null) suo.gameObject.SetActive(false);

        var icon = card.transform.Find("Iconmask/Icon")?.GetComponent<Image>();
        var sp = MercPortraitSprites.GetHead(hireId);
        if (sp == null && !string.IsNullOrEmpty(def.AssetId)) sp = MercPortraitSprites.GetHead(def.AssetId);
        if (icon != null)
        {
            icon.sprite = sp;
            icon.enabled = sp != null;
            icon.color = owned ? Color.white : new Color(0.40f, 0.40f, 0.40f, 0.9f);
        }

        var nameT = card.transform.Find("namebg/Name")?.GetComponent<Text>();
        if (nameT != null) nameT.text = owned ? def.Name : "未获得";

        ApplyRoleIcon(card.transform, def, true);

        // 模板本身没有 Button（Slot 只有 Image），克隆后运行时补，口径沿用：已拥有 = 上架/替换，未获得 = 看图鉴
        var img = card.GetComponent<Image>();
        if (img != null) img.raycastTarget = true;
        var btn = card.GetComponent<Button>();
        if (btn == null) btn = card.AddComponent<Button>();
        if (btn.targetGraphic == null && img != null) btn.targetGraphic = img;
        btn.onClick.RemoveAllListeners();
        string hid = hireId;
        if (owned) btn.onClick.AddListener(() => OnOwnedCardClick(hid));
        else btn.onClick.AddListener(() => ShowMercCodex(hid, def));
    }

    /// <summary>
    /// Role 四分类图标 = JobIconResolver.CombatBadge(def.JobName)（T5）；
    /// 取不到就隐藏 Role 节点（CombatBadge 自带 WarnOnce）。show=false 直接隐藏。
    /// </summary>
    static void ApplyRoleIcon(Transform cardRoot, MercRosterDefs.Def def, bool show)
    {
        var roleTr = cardRoot.Find("Role");
        if (roleTr == null) return;
        var rimg = roleTr.GetComponent<Image>();
        var rsp = show && rimg != null && !string.IsNullOrEmpty(def.JobName)
            ? JobIconResolver.CombatBadge(def.JobName) : null;
        if (rimg != null)
        {
            rimg.sprite = rsp;
            rimg.enabled = rsp != null;
        }
        roleTr.gameObject.SetActive(rsp != null);
    }

    /// <summary>隐藏 Role 节点（空格/锁格用，不留上一张的图）。</summary>
    static void HideRoleIcon(Transform cardRoot)
    {
        var roleTr = cardRoot.Find("Role");
        if (roleTr == null) return;
        var rimg = roleTr.GetComponent<Image>();
        if (rimg != null)
        {
            rimg.sprite = null;
            rimg.enabled = false;
        }
        roleTr.gameObject.SetActive(false);
    }

    // ============================================================
    // 交互（全部即时生效：改存档 → Save → 重读 → 刷新）
    // ============================================================

    /// <summary>点池格头像 = 看图鉴详情（下架走右上角红「−」，主人设计图口径）。</summary>
    void OnPoolSlotClick(int index)
    {
        var data = SaveSystem.Instance?.Data;
        int slots = data != null ? data.DrawPoolSlots : SaveData.DrawPoolDefaultSlots;
        if (index >= slots)
        {
            UIManager.Instance?.ShowToast(LockHintOf(index) + "后开启");
            return;
        }
        var ordered = SortByRarity(_picked);
        if (index >= ordered.Count) return;
        if (MercRosterDefs.TryGetByHireId(ordered[index], out var def))
            ShowMercCodex(ordered[index], def);
    }

    /// <summary>红「−」= 下架 → 系统立即自动补位（主人拍板「池子最少要补全 会自动补全」）。</summary>
    void OnRemoveClick(string hireId)
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        if (data.RemoveFromDrawPool(hireId))
        {
            MercRosterDefs.TryGetByHireId(hireId, out var def);
            UIManager.Instance?.ShowToast("已下架 " + def.Name + "，系统自动补位");
            Commit();
        }
    }

    /// <summary>点已获得卡 = 上架；池满 → 弹替换弹窗（主人拍板「点佣兵弹出替换按钮」，不做拖拽）。</summary>
    void OnOwnedCardClick(string hireId)
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        if (_picked.Contains(hireId))
        {
            // 在池的卡点了不做事（下架走池格红「−」），避免误触把人摘下来
            return;
        }
        if (data.AddToDrawPool(hireId))
        {
            MercRosterDefs.TryGetByHireId(hireId, out var def);
            UIManager.Instance?.ShowToast(def.Name + " 已上架");
            Commit();
            return;
        }
        if (_picked.Count >= data.DrawPoolSlots)
        {
            // 池满：弹替换弹窗，点选池中谁换下
            _replacingInId = hireId;
            MercRosterDefs.TryGetByHireId(hireId, out var def);
            if (_replaceTitle != null)
                _replaceTitle.text = "替换池中的佣兵（换上 " + def.Name + "）";
            RefreshReplacePanel();
            if (_replacePanel != null)
            {
                _replacePanel.SetActive(true);
                _replacePanel.transform.SetAsLastSibling();
            }
        }
    }

    void RefreshReplacePanel()
    {
        for (int i = 0; i < _replaceCards.Count; i++)
            if (_replaceCards[i] != null) Destroy(_replaceCards[i]);
        _replaceCards.Clear();

        var ordered = SortByRarity(_picked);
        for (int i = 0; i < ordered.Count; i++)
        {
            string outId = ordered[i];
            MercRosterDefs.TryGetByHireId(outId, out var def);
            var card = new GameObject("RCard", typeof(RectTransform), typeof(Image), typeof(Button));
            card.transform.SetParent(_replaceContent, false);
            card.GetComponent<RectTransform>().sizeDelta = new Vector2(CardW, CardH);
            card.GetComponent<Image>().color = new Color(0.20f, 0.16f, 0.12f, 1f);
            var btn = card.GetComponent<Button>();
            btn.targetGraphic = card.GetComponent<Image>();

            BuildMercFace(card.transform, outId, def, def.Name, false);
            var minus = MakeBtn(card.transform, "Swap", "换下", new Vector2(0.5f, 0f), null);
            var mrt = RtOf(minus);
            mrt.anchorMin = new Vector2(0.5f, 0f);
            mrt.anchorMax = new Vector2(0.5f, 0f);
            mrt.pivot = new Vector2(0.5f, 0f);
            mrt.anchoredPosition = new Vector2(0f, 4f);
            mrt.sizeDelta = new Vector2(84f, 30f);
            minus.GetComponent<Image>().color = new Color(0.78f, 0.22f, 0.22f, 0.9f);
            var mlabel = minus.transform.Find("Label")?.GetComponent<Text>();
            if (mlabel != null) mlabel.fontSize = 15;

            string captured = outId;
            UnityEngine.Events.UnityAction swap = () =>
            {
                var d = SaveSystem.Instance?.Data;
                if (d != null && d.SwapInDrawPool(_replacingInId, captured))
                {
                    UIManager.Instance?.ShowToast("替换成功");
                    Commit();
                    if (_replacePanel != null) _replacePanel.SetActive(false);
                }
            };
            btn.onClick.AddListener(swap);
            minus.onClick.AddListener(swap);
            _replaceCards.Add(card);
        }
        LayoutReplace();
    }

    void LayoutReplace()
    {
        if (_replaceContent == null) return;
        int n = _replaceCards.Count;
        int rows = Mathf.Max(1, (n + GridCols - 1) / GridCols);
        float h = GridPad * 2f + rows * CardH + (rows - 1) * GridGap;
        float viewH = _replaceContent.parent != null ? _replaceContent.parent.GetComponent<RectTransform>().rect.height : 0f;
        _replaceContent.sizeDelta = new Vector2(0f, Mathf.Max(h, viewH));
    }

    /// <summary>图鉴详情（未获得的 meta 里带获取途径）。</summary>
    static void ShowMercCodex(string hireId, MercRosterDefs.Def def)
    {
        var stand = MercPortraitSprites.GetHead(hireId);
        if (stand == null && !string.IsNullOrEmpty(def.AssetId)) stand = MercPortraitSprites.GetHead(def.AssetId);
        bool unlocked = SaveSystem.Instance != null && SaveSystem.Instance.Data != null
            && SaveSystem.Instance.Data.unlockedMercIds != null
            && SaveSystem.Instance.Data.unlockedMercIds.Contains(hireId);
        string meta = def.JobName + " · "
            + SkillRarityUtil.DisplayName(SkillRarityUtil.FromMerc(def.Rarity))
            + (unlocked ? " · 已解锁" : " · 未获得");
        string lore = unlocked ? "" : MercUnlockWayOf(def);
        string desc;
        // 图鉴条目按 catalogId 找：AssetId → catalogId
        string catalogId = !string.IsNullOrEmpty(def.AssetId) ? AdventureCodex.ResolveMercId(def.AssetId) : hireId;
        if (!string.IsNullOrEmpty(catalogId) && AdventureLogCatalog.TryFindMerc(catalogId, out var entry))
            desc = entry.Desc;
        else
            desc = "冒险日志里还没有这位的详细记录。";
        CodexInfoPopupUI.Show(def.Name, meta, desc, lore, stand);
    }

    static string MercUnlockWayOf(MercRosterDefs.Def def)
    {
        var tier = GameConfig.GetMercTier(def.AssetId);
        if (tier == MercTier.Advanced)
            return "获取途径：酒馆解锁，需要公会等级 " + GameConfig.ADVANCED_MERC_GUILD_LEVEL + "。";
        if (def.RecruitGold > 0)
            return "获取途径：酒馆解锁，需要 " + def.RecruitGold + " 金币。";
        return "获取途径：酒馆解锁。";
    }

    // ============================================================
    // 构件
    // ============================================================

    /// <summary>复用美术资源目录（Resources 相对路径前缀）。2026-10-15 主人拍板：优先用项目能复用的资源。</summary>
    const string ArtCommon = "Art/UI/Common/";

    static readonly Dictionary<string, Sprite> _artCache = new Dictionary<string, Sprite>();

    /// <summary>按 Resources 相对路径取 sprite（带缓存；取不到返回 null，调用方回落纯色，不静默）。</summary>
    static Sprite Art(string resPath)
    {
        if (string.IsNullOrEmpty(resPath)) return null;
        if (_artCache.TryGetValue(resPath, out var c) && c != null) return c;
        var sp = Resources.Load<Sprite>(resPath);
        _artCache[resPath] = sp;
        return sp;
    }

    /// <summary>给 MakeBtn 生成的按钮换复用素材钮底（Simple + 白底；取不到保持原木色）。</summary>
    static void SetBtnSprite(Button btn, string resPath)
    {
        if (btn == null) return;
        var img = btn.targetGraphic as Image;
        if (img == null) return;
        var sp = Art(resPath);
        if (sp == null) return;
        img.sprite = sp;
        img.type = Image.Type.Simple;
        img.color = Color.white;
    }

    /// <summary>稀有度降序（传说 → 稀有 → 普通），同档按花名册表序。</summary>
    static List<string> SortByRarity(HashSet<string> ids)
    {
        var result = new List<string>();
        IReadOnlyList<MercRosterDefs.Def> all = MercRosterDefs.All;
        for (int pass = 0; pass < 3; pass++)
        {
            var tier = pass == 0 ? MercRosterDefs.MercRarity.Legendary
                : pass == 1 ? MercRosterDefs.MercRarity.Rare
                : MercRosterDefs.MercRarity.Common;
            for (int i = 0; i < all.Count; i++)
                if (all[i].Rarity == tier && ids.Contains(all[i].HireId))
                    result.Add(all[i].HireId);
        }
        return result;
    }

    void ClearCards(List<GameObject> list)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null) Destroy(list[i]);
        list.Clear();
    }

    /// <summary>按占用高度（BuildGroup 返回值累计）算 content 高。</summary>
    static void LayoutContent(RectTransform content, float used)
    {
        if (content == null) return;
        float viewH = content.parent != null ? content.parent.GetComponent<RectTransform>().rect.height : 0f;
        content.sizeDelta = new Vector2(0f, Mathf.Max(used, viewH));
    }

    static Text MakeText(Transform parent, string name, string content, int size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch(go.GetComponent<RectTransform>());
        var t = go.AddComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.font = GameFonts.GetChinese();
        if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.raycastTarget = false;
        return t;
    }

    static Image NewImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static Button MakeBtn(Transform parent, string name, string label, Vector2 anchor, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(150f, 48f);
        go.GetComponent<Image>().color = new Color(0.45f, 0.32f, 0.20f, 1f);
        var t = MakeText(go.transform, "Label", label, 20);
        t.raycastTarget = false;
        if (onClick != null) go.GetComponent<Button>().onClick.AddListener(onClick);
        return go.GetComponent<Button>();
    }

    /// <summary>Button 没有 rectTransform 属性（Graphic 才有），从 transform 取（2026-10-13 编译教训）。</summary>
    static RectTransform RtOf(Component c)
    {
        return c != null ? c.transform as RectTransform : null;
    }

    static void Anchor(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void Stretch(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
