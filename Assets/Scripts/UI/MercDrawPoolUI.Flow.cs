using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>佣兵抽奖池 UI · 大滚动收编与段排版（RestructureMasterScroll 一条大滚动路线）。</summary>
public partial class MercDrawPoolUI : MonoBehaviour
{
    // ============================================================
    // 一条大滚动（2026-10-15 主人拍板：top 池区的字和装饰、已拥有、未获得 全在同一滚动里；
    // 底框背景不滚；down/LockedScroll 停用。只挪父节点和落位，不改任何预制体节点的宽高/字号）
    // ============================================================

    class FlowNode { public RectTransform rt; public float x; public float topOffset; public float h; }

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
        // 2026-10-15 主人拍板：视口必须排在 center 【之后】——center 里还留着 底框（紫色 a≈0.33）与 分割线，
        // 排在它们前面 = 这俩盖在整条滚动内容之上（预制体原设计 底框 是 center 第一个子节点＝OwnedScroll 的背景）。
        viewport.SetSiblingIndex(center.GetSiblingIndex() + 1);
        // 2026-10-15 主人拍板：anchorMin.y 必须是 0（底锚）——写成 1（顶锚）时 offsetMin.y=+16 会量出负高度矩形，
        // RectMask2D 判定 validRect=false 把整条收编内容 cull 掉（不渲染也不响应射线 = 佣兵卡全空），
        // 且透明视口图（raycastTarget=1）的包围盒会盖住右上角 X 关闭按钮 = 点了没反应。
        // (0,0)+(1,1) 才是「底边=面板底+16、顶边=Header 底-8」的正确语义。
        viewport.anchorMin = new Vector2(0f, 0f);
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
        // 2026-10-15 主人拍板：top 是 100×100 的空壳容器（面板正中），PoolGrid/PoolHead/PoolCount/ScrollIco
        // 全飘在壳子上方 100~450px —— 收编【壳子】会把整个池区摆到视口顶上方约 430px，被 RectMask2D 裁没。
        // 改收编 top 的直接子节点（各自有真矩形），簇高按子节点并集算，池区正好贴在 Header 下方。
        var topChildren = new List<RectTransform>();
        for (int i = 0; i < top.childCount; i++)
            if (top.GetChild(i) is RectTransform ch) topChildren.Add(ch);
        _hTop = CollectCluster(topChildren.ToArray(), content, _flowTopNodes, 0f);
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

    /// <summary>按占用高度算 content 高。</summary>
    static void LayoutContent(RectTransform content, float used)
    {
        if (content == null) return;
        float viewH = content.parent != null ? content.parent.GetComponent<RectTransform>().rect.height : 0f;
        content.sizeDelta = new Vector2(0f, Mathf.Max(used, viewH));
    }

#if UNITY_EDITOR
    static bool _flowChecked;
    /// <summary>
    /// 布局自检（每次进游戏只跑一次，2026-10-15 主人拍板加保险）：把收编节点 + 全部卡片的世界矩形取并集，
    /// 与滚动视口比对。整体无交集 = RectMask2D 把内容全裁掉（渲染必空），当场 LogError 给出数字定位，
    /// 不许静默空白。
    /// </summary>
    void CheckFlowLayout()
    {
        if (_flowChecked || !_masterReady || _masterContent == null) return;
        _flowChecked = true;
        var view = _masterContent.parent as RectTransform;
        if (view == null) return;
        var vc = new Vector3[4];
        view.GetWorldCorners(vc);
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        bool any = false;
        void Eat(Transform t)
        {
            var rt = t as RectTransform;
            if (rt == null) return;
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            any = true;
            minX = Mathf.Min(minX, c[0].x); maxX = Mathf.Max(maxX, c[2].x);
            minY = Mathf.Min(minY, c[0].y); maxY = Mathf.Max(maxY, c[1].y);
        }
        foreach (var n in _flowTopNodes) Eat(n.rt);
        foreach (var n in _flowOwnedHeadNodes) Eat(n.rt);
        foreach (var n in _flowLockedHeadNodes) Eat(n.rt);
        for (int i = 0; i < _ownedCards.Count; i++) Eat(_ownedCards[i] != null ? _ownedCards[i].transform : null);
        for (int i = 0; i < _lockedCards.Count; i++) Eat(_lockedCards[i] != null ? _lockedCards[i].transform : null);
        if (!any)
        {
            Debug.LogError("[MercDrawPoolUI] 大滚动收编后没有任何内容节点（簇与卡全空）");
            return;
        }
        bool inside = minY < vc[1].y && maxY > vc[0].y && minX < vc[2].x && maxX > vc[0].x;
        Debug.Log("[MercDrawPoolUI] 布局自检：内容世界矩形 x[" + minX.ToString("F0") + "," + maxX.ToString("F0")
            + "] y[" + minY.ToString("F0") + "," + maxY.ToString("F0")
            + "]，视口 x[" + vc[0].x.ToString("F0") + "," + vc[2].x.ToString("F0")
            + "] y[" + vc[0].y.ToString("F0") + "," + vc[1].y.ToString("F0") + "]"
            + (inside ? "（有交集，正常）" : " ←【无交集：内容整体在视口外，显示必空】"));
        if (!inside)
            Debug.LogError("[MercDrawPoolUI] 收编内容整体在视口外 → 整条滚动渲染必空，按上面数字定位");
    }
#endif
}
