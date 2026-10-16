using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 佣兵抽奖池 UI · 列表结构（2026-10-16 主人原话口径二次修正）：
/// 「最上面的top节点是不动的 佣兵池是不动的 只有下面的已获得和未获得是个大的滑动列表」
/// <para>① top 招募池（ScrollIco/PoolHead/PoolCount/PoolGrid 五槽）<b>固定不动</b>，代码只填头像/名字；</para>
/// <para>② top 以下整块 = <b>唯一一条大滚动</b>（center/OwnedScroll 视口顶扩到招募池底），Content 内依次：
///    已获得段头（yiyongyou/OwnedHead/jiantou/PoolCount1 收进 Content，跟随滚动）→ 已获得卡
///    → 未获得段头（down 的 weiyongyou/LockedHead 收进 Content）→ 未获得卡；</para>
/// <para>③ center/down/LockedScroll 停用、分割线隐藏（合并后不再需要）。</para>
/// </summary>
public partial class MercDrawPoolUI
{
    class FlowNode
    {
        public RectTransform rt;
        public float centerX;     // 相对 Content 中心的 x（局部）
        public float topOffset;   // 相对本簇顶边的纵向偏移（局部，向下为正）
        public float h;
    }

    // —— 大滚动视口矩形（center 局部；center 是 100×100 壳、pivot 0.5，与 Panel 中心同轴，数值即面板坐标）——
    // 顶不写死：搭列表时量招募池五槽（含名字牌下垂）最低沿，再留 PoolToListGap；底沿用预制体原 LockedScroll 底。
    const float ListViewBottomY = -485f;
    const float ListViewWidth = 634f;
    const float PoolToListGap = 12f;     // 招募池最低沿与滚动列表顶的间隙

    readonly List<FlowNode> _flowOwnedHeadNodes = new List<FlowNode>();
    readonly List<FlowNode> _flowLockedHeadNodes = new List<FlowNode>();
    RectTransform _masterContent;
    ScrollRect _masterScroll;
    Transform _restructurePanel;
    bool _masterReady;
    float _flowLockedCardsY;
    float _hOwnedHead;
    float _hLockedHead;
    const float GapSection = 24f;        // 已获得段与未获得段的间隔
    const float HeadToCardsGap = 8f;     // 段头与其下卡片区的间隔

    /// <summary>
    /// 一次性搭好列表结构。【调用前提】Root 已激活（Open 先 SetActive(true)），否则放弃下次重试。
    /// 只做：扩视口 / 隐藏分割线 / 停旧滚动 / 把两段头收进 Content。top 招募池一律不碰。
    /// </summary>
    void SetupMasterList(Transform panel)
    {
        if (_root == null || !_root.activeInHierarchy)
        {
            Debug.LogError("[MercDrawPoolUI] 列表搭建时 Root 未激活 → 本次放弃，下次打开重试");
            return;
        }

        var center = panel.Find("center") as RectTransform;
        var down = center != null ? center.Find("down") : null;
        var viewport = center != null ? center.Find("OwnedScroll") as RectTransform : null;
        var content = viewport != null ? viewport.Find("Content") as RectTransform : null;
        if (center == null || down == null || viewport == null || content == null)
        {
            Debug.LogError("[MercDrawPoolUI] 列表缺节点 center/OwnedScroll/Content/down → 保持预制体原样");
            return;
        }

        Canvas.ForceUpdateCanvases();

        // 1) 视口顶=招募池五槽最低沿（含名字牌）再留 12 间隙（top 以下整块都给大滚动）；底=原 LockedScroll 底。
        //    不挪父、不改锚点族（保持 center 下 0.5/0.5 中心锚）、不碰 Mask 底图（Stencil 靠它，alpha 也不能动）。
        float topY = PoolLowestY(center) - PoolToListGap;
        viewport.anchoredPosition = new Vector2(0f, (topY + ListViewBottomY) * 0.5f);
        viewport.sizeDelta = new Vector2(ListViewWidth, topY - ListViewBottomY);
        _masterScroll = viewport.GetComponent<ScrollRect>();

        // 2) Content 预制体已是顶拉伸（anchor 0,1→1,1、pivot 0.5,1），确保 offset 归零即可
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;
        content.anchoredPosition = Vector2.zero;
        _masterContent = content;

        // 3) 合并后旧分隔物处理：分割线隐藏；旧第二段滚动停用
        var divider = center.Find("分割线");
        if (divider != null) divider.gameObject.SetActive(false);
        var lockedScroll = down.Find("LockedScroll");
        if (lockedScroll != null) lockedScroll.gameObject.SetActive(false);

        // 4) 两段头收进 Content 跟随滚动（此刻 Root 已激活、量得准）；缺节点报错不静默
        CollectCluster(new[]
        {
            center.Find("yiyongyou") as RectTransform,
            center.Find("OwnedHead") as RectTransform,
            center.Find("jiantou") as RectTransform,
            center.Find("PoolCount1") as RectTransform,
        }, new[] { "center/yiyongyou", "center/OwnedHead", "center/jiantou", "center/PoolCount1" },
            content, _flowOwnedHeadNodes, out _hOwnedHead, "已获得段头");
        CollectCluster(new[]
        {
            down.Find("weiyongyou") as RectTransform,
            down.Find("LockedHead") as RectTransform,
        }, new[] { "down/weiyongyou", "down/LockedHead" },
            content, _flowLockedHeadNodes, out _hLockedHead, "未获得段头");

        Canvas.ForceUpdateCanvases();
        _masterReady = true;
        if (_masterScroll != null) _masterScroll.verticalNormalizedPosition = 1f;

#if UNITY_EDITOR
        _flowChecked = false;
#endif
    }

    /// <summary>招募池五槽的最低沿（center 局部 y，含名字牌下垂；量不到退回 69 = 预制体原段头顶沿）。</summary>
    float PoolLowestY(Transform center)
    {
        float lowest = float.MaxValue;
        bool any = false;
        for (int i = 0; i < _poolSlots.Count; i++)
        {
            var rt = _poolSlots[i] != null ? _poolSlots[i].transform as RectTransform : null;
            if (rt == null) continue;
            EatBottom(rt, center, ref lowest, ref any);
            var namebg = rt.Find("namebg") as RectTransform;
            if (namebg != null) EatBottom(namebg, center, ref lowest, ref any);
        }
        return any ? lowest : 69f;
    }

    static void EatBottom(RectTransform rt, Transform center, ref float lowest, ref bool any)
    {
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        var local = center.InverseTransformPoint((c[0] + c[3]) * 0.5f);  // 底边中点
        if (local.y < lowest) lowest = local.y;
        any = true;
    }

    /// <summary>
    /// 把一段头（图标/标题/计数等）收进 Content：激活状态量 Content 局部矩形，挪父后记录相对布局。
    /// 只量这一次；每刷用 PlaceCluster 摆位。缺节点打 Error 报名字（不静默）。
    /// </summary>
    void CollectCluster(RectTransform[] nodes, string[] names, RectTransform content, List<FlowNode> into, out float clusterH, string label)
    {
        into.Clear();
        clusterH = 0f;
        var list = new List<RectTransform>(nodes.Length);
        var missing = new List<string>();
        for (int i = 0; i < nodes.Length; i++)
        {
            if (nodes[i] != null) list.Add(nodes[i]);
            else missing.Add(i < names.Length ? names[i] : ("第" + (i + 1) + "个节点"));
        }
        if (missing.Count > 0)
            Debug.LogError("[MercDrawPoolUI] " + label + "缺节点: " + string.Join(", ", missing.ToArray()) + " → 按剩余节点搭建");
        if (list.Count == 0) return;

        var cc = new Vector3[4];
        content.GetWorldCorners(cc);
        float contentCx = (cc[0].x + cc[2].x) * 0.5f;
        float contentTopY = cc[1].y;
        float kx = content.lossyScale.x, ky = content.lossyScale.y;
        if (kx < 0.0001f || ky < 0.0001f) return;

        var recs = new List<(RectTransform rt, float cx, float topLocal, float w, float h)>();
        float clusterTop = float.MaxValue;
        for (int i = 0; i < list.Count; i++)
        {
            var c = new Vector3[4];
            list[i].GetWorldCorners(c);
            float cxw = ((c[0].x + c[2].x) * 0.5f - contentCx) / kx;
            float topLocal = (contentTopY - c[1].y) / ky;
            float w = (c[2].x - c[0].x) / kx;
            float h = (c[1].y - c[0].y) / ky;
            recs.Add((list[i], cxw, topLocal, w, h));
            if (topLocal < clusterTop) clusterTop = topLocal;
        }

        for (int i = 0; i < recs.Count; i++)
        {
            var r = recs[i];
            r.rt.SetParent(content, false);
            r.rt.anchorMin = new Vector2(0.5f, 1f);
            r.rt.anchorMax = new Vector2(0.5f, 1f);
            r.rt.pivot = new Vector2(0.5f, 1f);
            r.rt.sizeDelta = new Vector2(r.w, r.h);
            float topOffset = r.topLocal - clusterTop;
            into.Add(new FlowNode { rt = r.rt, centerX = r.cx, topOffset = topOffset, h = r.h });
            clusterH = Mathf.Max(clusterH, topOffset + r.h);
        }
    }

    /// <summary>每刷把一簇段头摆到指定流位置（flowY = 簇顶相对 Content 顶的距离）。</summary>
    void PlaceCluster(List<FlowNode> nodes, float flowY)
    {
        if (_masterContent == null) return;
        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            if (n?.rt == null) continue;
            n.rt.anchoredPosition = new Vector2(n.centerX, -(flowY + n.topOffset));
        }
    }

    /// <summary>按占用高度算 content 高（至少撑满视口）。</summary>
    static void LayoutContent(RectTransform content, float used)
    {
        if (content == null) return;
        float viewH = content.parent != null ? content.parent.GetComponent<RectTransform>().rect.height : 0f;
        content.sizeDelta = new Vector2(0f, Mathf.Max(used, viewH));
    }

#if UNITY_EDITOR
    static bool _flowChecked;
    /// <summary>布局自检（每次打开跑一次）：卡片+段头世界矩形与滚动视口无交集 = 渲染必空，当场报错给数字。</summary>
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
        foreach (var n in _flowOwnedHeadNodes) Eat(n.rt);
        foreach (var n in _flowLockedHeadNodes) Eat(n.rt);
        for (int i = 0; i < _ownedCards.Count; i++) Eat(_ownedCards[i] != null ? _ownedCards[i].transform : null);
        for (int i = 0; i < _lockedCards.Count; i++) Eat(_lockedCards[i] != null ? _lockedCards[i].transform : null);
        if (!any)
        {
            Debug.LogError("[MercDrawPoolUI] 列表里没有任何卡（已获得/未获得全空）");
            return;
        }
        // 列表允许滚出视口（内容比视口高），只拦截「整张内容横向全在视口外」这类整体错位
        bool xOverlap = minX < vc[2].x && maxX > vc[0].x;
        Debug.Log("[MercDrawPoolUI] 布局自检：内容世界矩形 x[" + minX.ToString("F0") + "," + maxX.ToString("F0")
            + "] y[" + minY.ToString("F0") + "," + maxY.ToString("F0")
            + "]，视口 x[" + vc[0].x.ToString("F0") + "," + vc[2].x.ToString("F0")
            + "] y[" + vc[0].y.ToString("F0") + "," + vc[1].y.ToString("F0") + "]"
            + (xOverlap ? "（横向有交集，正常）" : " ←【横向无交集：卡全在视口侧外，显示必错】"));
        if (!xOverlap)
            Debug.LogError("[MercDrawPoolUI] 卡片整体在视口侧外 → 按上面数字定位");
    }
#endif
}
