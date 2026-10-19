using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

#if UNITY_EDITOR
using UnityEditor;
#endif

// 冒险界面 partial —— 2026-10-18 由 AdventureUI.cs 纯位移拆出（无逻辑改动）
// 职责：竖屏专用适配（ApplyAdventureFit 总入口、Fit*/Restore*/Capture 几何工具、底栏占位测量）
public partial class AdventureUI
{
    // ────────────────────────────────────────────────────
    // 竖屏专用适配（主人点名：搬进界面自己文件，不通用；与 DailyLoginUI.ApplyFit 同思路）
    // 仅「更瘦屏」（逻辑高 > 设计高 1280）生效；标准 9:16 保持美术预制体原样，直接 return 不改任何东西。
    // ────────────────────────────────────────────────────

    /// <summary>AdventureUI 专用竖屏适配总开关。false 时还原到预制体原始锚点/偏移，方便紧急一键回退。</summary>
    public static bool EnableAdventureUiFit = true;

    /// <summary>
    /// 改动 4 总开关（主人总方针"中间有背景就平铺"）：MapRoot 自带 MapBg 背景层，瘦屏下拉伸填满中间空隙；不满意一句关掉。
    /// 默认 false：主人反馈「右边的背景框怎么放大了」——MapRoot 一拉伸就把自带的 MapBg 撑到几乎满屏，
    /// 视觉上就是背景框被放大。要恢复中间拉伸，把这里改回 true 即可（拉伸代码与高度上限约束都还在）。
    /// </summary>
    public static bool EnableAdventureMapStretch = false;

    /// <summary>
    /// 底部功能入口留位总开关：true 时 DetailPanel 底距按「运行时量到的 BottomNav 高度 + NAV_GAP_PAD」抬高，
    /// 给底栏（公会/角色/冒险/酒馆/冒险日志）让位；false 时回到原来的「贴屏幕最下」行为，方便一键对比。
    /// </summary>
    public static bool EnableNavGapReserve = true;

    // 防递归守卫（与 _verticalSplitApplying 同思路），避免改锚点触发的重入。
    bool _adventureUiApplying;

    // 记录被重锚定节点的原始锚点/偏移/轴心，用于幂等重算与一键回退还原（按 instanceID）。
    struct AdventureUiBase
    {
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 offsetMin;
        public Vector2 offsetMax;
        public Vector2 pivot;
        public bool applied;
    }
    readonly System.Collections.Generic.Dictionary<int, AdventureUiBase> _adventureUiBase =
        new System.Collections.Generic.Dictionary<int, AdventureUiBase>();

    /// <summary>MapRoot 右溢出修正：原值 35 让地图超出 RightContent 右边 35px，主人嫌地图没居右，归零贴 RightContent 右边。</summary>
    const float MAP_RIGHT_INSET = 0f;
    /// <summary>DetailPanel 底部预留（原行为 / 关掉留位开关时用）：底部贴到屏幕最下。</summary>
    const float DETAIL_PANEL_BOTTOM_GAP = 0f;
    /// <summary>运行时量不到底部功能入口时的兜底预留（主人截图里底栏约 150px 高，取整留一点余量）。</summary>
    const float DETAIL_PANEL_BOTTOM_GAP_FALLBACK = 160f;
    /// <summary>DetailPanel 底边与底部功能入口之间的视觉间距。</summary>
    const float NAV_GAP_PAD = 8f;
    /// <summary>MapRoot 底边与 DetailPanel 顶边之间的间隙；瘦屏拉伸填满中间时作用于此。</summary>
    const float MAP_BOTTOM_GAP = 16f;
    /// <summary>
    /// 瘦屏多余高度的分配比例（2026-10-17 主人「高屏有很大空隙，尽量保持原始比例」）：
    /// 地图不拉伸、保持原始高度，多余高度 = 地图上方留白 × 此比例 + 下方 × (1-此比例)。
    /// 0.5 = 上下均分（地图在顶栏~详情面板带内垂直居中）；0 = 全给下方（原行为，一大块空隙）。
    /// </summary>
    const float MAP_EXTRA_TOP_SHARE = 0.5f;

    /// <summary>
    /// AdventureUI 专用竖屏适配（主人点名要求的三条改动统一入口）。
    /// 触发：ShowPage() 末尾 RefreshAll 之后调用一次。
    /// 铁律：仅在「更瘦屏」（逻辑高 > 设计高 1280）生效；标准 9:16 必须保持美术预制体原样，直接 return 不改任何东西。
    /// 幂等：按 instanceID 记录首次 base，重复调用从 base 重算，不累积偏移；EnableAdventureUiFit=false 或标准屏时还原到预制体原始状态。
    /// 节点不存在（RightContent/DetailPanel/MapRoot 缺失）则 no-op，不报错。
    /// </summary>
    void ApplyAdventureFit()
    {
        Transform root = transform;
        if (root == null) return;
        if (_adventureUiApplying) return;
        _adventureUiApplying = true;
        try
        {
            var right = root.Find("RightContent") as RectTransform;
            var detail = root.Find("DetailPanel") as RectTransform;
            var mapRoot = right != null ? right.Find("MapRoot") as RectTransform : null;

            // 紧急关闭 或 标准屏：还原到预制体原始锚点/偏移即可，不改动任何东西。
            if (!EnableAdventureUiFit || !UiLayoutStretch.IsThinnerScreen())
            {
                if (right != null) RestoreAdventureBase(right);
                if (mapRoot != null) RestoreAdventureBase(mapRoot);
                if (detail != null) RestoreAdventureBase(detail);
                if (mapBg != null) RestoreAdventureBase(mapBg.rectTransform);
                var wmBtnRestore = right != null ? right.Find("WorldMapBtn") as RectTransform : null;
                if (wmBtnRestore != null) RestoreAdventureBase(wmBtnRestore);
                RestoreAdventureSidebarOrder(right);
                return;
            }

            // 首帧 Canvas 可能尚未 rebuild，rect 仍为 0 → 直接 return 会永久失效。
            // 先强制刷新一次画布再取尺寸（保持同步调用，不改用协程）；仍为 0 才放弃本次。
            var canvas = root.GetComponentInParent<Canvas>();
            if (canvas != null) Canvas.ForceUpdateCanvases();
            var rootRt = root as RectTransform;
            if (rootRt == null || rootRt.rect.width <= 0f || rootRt.rect.height <= 0f) return;

            // 底部五入口（公会/角色/冒险/酒馆/冒险日志）占住屏幕底部，详情面板必须给它让位，
            // 否则面板底部（进入关卡按钮那一排）会被压在导航栏下面看不见。
            float navReserved = EnableNavGapReserve ? ResolveBottomNavReserved(rootRt) : 0f;
            float detailBottomGap = DETAIL_PANEL_BOTTOM_GAP;
            if (EnableNavGapReserve)
            {
                float gap = navReserved + NAV_GAP_PAD;
                // 量不到导航节点（独立打开 / 组件未就绪）时用兜底常量，不能退回 0 让面板贴到屏幕最下
                detailBottomGap = gap > 0f ? gap : DETAIL_PANEL_BOTTOM_GAP_FALLBACK;
            }

            // 改动 5 必须最先：要在 RightContent / MapRoot 动锚点之前量世界坐标（见方法注释）。
            FitAdventureMapBgCoverSidebar(rootRt);
            // 改动 5 配套：预制体里 LeftSidebar 排在 RightContent 前面（下层），
            // 地图图左扩后会盖住「主线冒险」按钮 → 把左侧栏抬到 RightContent 之上、DetailPanel 之下。
            FitAdventureSidebarAboveMap(right);

            if (right != null)
            {
                FitAdventureRightContent(right);
                if (mapRoot != null) FitAdventureMapRootRight(mapRoot);
            }
            if (detail != null) FitAdventureDetailPanel(detail, detailBottomGap);
            // 改动 4（拉伸填满）保留但默认关——主人否了「背景框放大」的效果（EnableAdventureMapStretch=false）。
            if (mapRoot != null && right != null && detail != null && EnableAdventureMapStretch)
                FitAdventureMapRootStretch(mapRoot, right, detail, navReserved);
            // 2026-10-17 主人「高屏有很大空隙，尽量保持原始比例」：地图不拉伸、保持预制体原始高度，
            // 多余高度按 MAP_EXTRA_TOP_SHARE 上下分配（地图在顶栏~详情面板带内垂直居中）。
            // 必须在 DetailPanel 定位之后跑（要拿它的最终顶边）。
            if (mapRoot != null && right != null && detail != null)
                FitAdventureMapRootCentered(mapRoot, right, detail);
            // 世界地图按钮是 RightContent 居中锚点，屏越高越往上漂（相对地图的位置随屏高变化）；
            // 钉回设计屏（1280 高）时与地图底边的相对几何，x 不动。
            var worldMapBtn = right != null ? right.Find("WorldMapBtn") as RectTransform : null;
            if (worldMapBtn != null && mapRoot != null && right != null)
                FitWorldMapBtnToMap(worldMapBtn, right, mapRoot);

            // 2026-09-28 主人反馈真机「下面空了大片黑底」：排版不敢盲改，先打一行诊断数据，
            // 真机跑一次把这条 log 发回来，用真实数字定根因（导航预留量大了？还是面板没沉底？）。
            if (detail != null && rootRt != null)
            {
                float mapTopLog = -1f, mapBotLog = -1f;
                if (mapRoot != null && right != null)
                {
                    float rcTopLog = rootRt.rect.height + right.offsetMax.y;
                    mapTopLog = rcTopLog + mapRoot.offsetMax.y;
                    mapBotLog = rcTopLog + mapRoot.offsetMin.y;
                }
                Debug.Log($"[AdventureFit] rootH={rootRt.rect.height:F0} navReserved={navReserved:F0} " +
                          $"detailBottomGap={detailBottomGap:F0} detailBottom={detail.offsetMin.y:F0} " +
                          $"detailTop={detail.offsetMax.y:F0} mapTop={mapTopLog:F0} mapBottom={mapBotLog:F0} " +
                          $"screenW={Screen.width} screenH={Screen.height}");
            }
        }
        finally
        {
            _adventureUiApplying = false;
        }
    }

    /// <summary>首次遇到节点记录预制体原始锚点/偏移/轴心（用于幂等重算与一键回退）。返回 base（值类型，改完需写回字典）。</summary>
    AdventureUiBase CaptureAdventureBase(RectTransform rt)
    {
        int id = rt.GetInstanceID();
        if (!_adventureUiBase.TryGetValue(id, out var b))
        {
            b = new AdventureUiBase
            {
                anchorMin = rt.anchorMin,
                anchorMax = rt.anchorMax,
                offsetMin = rt.offsetMin,
                offsetMax = rt.offsetMax,
                pivot = rt.pivot,
                applied = false,
            };
            _adventureUiBase[id] = b;
        }
        return b;
    }

    /// <summary>把曾经改过的节点还原到预制体原始锚点/偏移/轴心（一键回退 / 标准屏 / 关开关时调用）。</summary>
    void RestoreAdventureBase(RectTransform rt)
    {
        int id = rt.GetInstanceID();
        if (_adventureUiBase.TryGetValue(id, out var b) && b.applied)
        {
            rt.anchorMin = b.anchorMin;
            rt.anchorMax = b.anchorMax;
            rt.offsetMin = b.offsetMin;
            rt.offsetMax = b.offsetMax;
            rt.pivot = b.pivot;
            b.applied = false;
            _adventureUiBase[id] = b;
        }
    }

    /// <summary>锚点在父矩形内插值出的参考点（Unity 锚点语义：x/y 各自按 anchor 的分量插值）。注意是 Mathf.Lerp 逐分量，不是 Vector2.Lerp。</summary>
    static Vector2 AnchorPointInParent(Vector2 pMin, Vector2 pMax, Vector2 anchor)
    {
        return new Vector2(
            Mathf.Lerp(pMin.x, pMax.x, anchor.x),
            Mathf.Lerp(pMin.y, pMax.y, anchor.y));
    }

    /// <summary>
    /// 运行时量出底部功能入口在屏幕底部占用的高度；量不到返回 0（调用方再退回兜底常量）。
    /// 优先 MainBottomNav 单例所在节点，其次在页面 root 里按名字找 BottomNav / MainBottomNav。
    /// 只量不改：不碰预制体，也不写死常量，换屏/换预制体都能跟上。
    /// </summary>
    static float ResolveBottomNavReserved(RectTransform pageRoot)
    {
        RectTransform nav = null;
        if (MainBottomNav.Instance != null)
            nav = MainBottomNav.Instance.transform as RectTransform;
        if (nav == null && pageRoot != null)
        {
            Transform found = FindDeepChild(pageRoot, "BottomNav")
                              ?? FindDeepChild(pageRoot, "MainBottomNav");
            if (found != null) nav = found as RectTransform;
        }
        if (nav == null || pageRoot == null) return 0f;

        // 2026-10-19 修「详情面板下面大片空隙」：旧实现拿 nav 父级局部坐标的 y 当
        // 「页面根底边以上的高度」用。nav 实际挂在共享底栏层级（GuildHallUI/Hub），
        // 父级原点/轴心跟 AdventureUI 页面根不一致；且 nav 根节点被 SafeAreaFitter
        // 重锚成全拉伸后 rect 高度还可能带陈旧基准漂移，量出来偏大（真机 ≈334，视觉 ≈140），
        // 详情面板被顶高一大截。改成：世界四角 → InverseTransformPoint 进 pageRoot 本地，
        // 只量「看得见的栏体」（BottomNav / BottomNavBG 子节点）顶边，坐标系恒正确。
        float rootBottomY = pageRoot.rect.min.y;
        float topAboveBottom = float.NegativeInfinity;
        int visualNodes = 0;
        for (int i = 0; i < nav.childCount; i++)
        {
            var child = nav.GetChild(i) as RectTransform;
            if (child == null) continue;
            if (child.name != "BottomNavBG" && child.name != "BottomNav") continue;
            topAboveBottom = Mathf.Max(topAboveBottom,
                TopEdgeAbovePageBottom(child, pageRoot, rootBottomY));
            visualNodes++;
        }
        if (visualNodes == 0)   // 栏体子节点缺失时退回量根节点
            topAboveBottom = TopEdgeAbovePageBottom(nav, pageRoot, rootBottomY);

        if (topAboveBottom <= 0f)
            return 0f;   // 量不到交回调用方走兜底常量；别拿可能漂移的 rect.height 充数
        return topAboveBottom;
    }

    /// <summary>节点视觉顶边（世界四角实测）换算进 pageRoot 本地后，高过页面根底边多少。不受父级轴心/缩放影响。</summary>
    static float TopEdgeAbovePageBottom(RectTransform rt, RectTransform pageRoot, float rootBottomY)
    {
        if (rt == null || pageRoot == null) return float.NegativeInfinity;
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        return pageRoot.InverseTransformPoint(corners[1]).y - rootBottomY;   // corners[1] = 左上角
    }

    /// <summary>按名字深找子孙节点（含自身）。</summary>
    static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var r = FindDeepChild(parent.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    /// <summary>
    /// 改动 1 — RightContent 整体右锚定：保持当前实际矩形（相对父的 left/right/top/bottom 像素）不变，
    /// 仅把水平锚点从 stretch(0→1) 改为右锚定(anchorMin.x=anchorMax.x=1)；垂直方向保持 stretch(0→1) 不动。
    /// 顶边超出父顶 98.15px 等美术摆出的偏移属原始设定，不擅自改，只还原到同样的矩形。
    /// </summary>
    void FitAdventureRightContent(RectTransform rt)
    {
        var b = CaptureAdventureBase(rt);
        var parent = rt.parent as RectTransform;
        if (parent == null) return;
        Vector2 pMin = parent.rect.min;
        Vector2 pMax = parent.rect.max;
        // 从 base 重算当前矩形边缘（幂等：始终以预制体原始锚点/偏移为准，重复调用不累积）。
        Vector2 curMin = AnchorPointInParent(pMin, pMax, b.anchorMin) + b.offsetMin;
        Vector2 curMax = AnchorPointInParent(pMin, pMax, b.anchorMax) + b.offsetMax;

        // 水平右锚定，垂直保持 stretch。
        rt.anchorMin = new Vector2(1f, b.anchorMin.y);
        rt.anchorMax = new Vector2(1f, b.anchorMax.y);
        Vector2 newMinA = new Vector2(1f, b.anchorMin.y);
        Vector2 newMaxA = new Vector2(1f, b.anchorMax.y);
        // 用同样的矩形边缘反推右锚定下的 offset，精确还原 left/right/top/bottom。
        rt.offsetMin = curMin - AnchorPointInParent(pMin, pMax, newMinA);
        rt.offsetMax = curMax - AnchorPointInParent(pMin, pMax, newMaxA);

        b.applied = true;
        _adventureUiBase[rt.GetInstanceID()] = b;
    }

    /// <summary>
    /// 改动 2 — MapRoot 右侧溢出归零：offsetMax.x 由原始 35 改为 MAP_RIGHT_INSET(0)，
    /// 让地图贴 RightContent 右边（不再超出右边 35px）；高度与纵向偏移保持预制体设定不变。
    /// </summary>
    void FitAdventureMapRootRight(RectTransform rt)
    {
        var b = CaptureAdventureBase(rt);
        // 仅改右溢出，纵向(offsetMax.y)与左/下偏移均用 base 原始值，保证幂等。
        rt.offsetMin = b.offsetMin;
        rt.offsetMax = new Vector2(MAP_RIGHT_INSET, b.offsetMax.y);

        b.applied = true;
        _adventureUiBase[rt.GetInstanceID()] = b;
    }

    /// <summary>
    /// 改动 5 — MapBg 左扩吃掉左侧栏大空隙（仅瘦屏）。
    /// 病根（主人 2026-10-17 真机截图）：左侧栏 5 个副本入口只开放「主线冒险」一颗（其余未开放被隐藏），
    /// 长比例屏上内容区变高后，按钮以下到详情面板之间是一整块空底，屏越长空得越多。
    /// 治法：把地图背景图（MapBg）的左边缘外扩到页面左缘，章节景色垫到「主线冒险」按钮底下，
    /// 空隙由图填掉；MapRoot 本体 / TitleBar / 关卡节点 / DetailPanel 一概不动。
    /// 细节：右边现在留了多少内缩，左边对称留同样宽度，视觉两边一致。
    /// 时序：必须在改动 1/2 动 RightContent / MapRoot 锚点之前调用 —— 本方法量的是世界坐标，
    /// 先量后改才能拿到预制体原始摆位；幂等靠 base：已外扩量从当前 offsetMin 减回 base 再重算，不累积。
    /// 标准屏 / 关开关 → ApplyAdventureFit 顶部的 RestoreAdventureBase(mapBg) 还原。
    /// </summary>
    void FitAdventureMapBgCoverSidebar(RectTransform pageRoot)
    {
        var bgTf = mapBg != null
            ? mapBg.rectTransform
            : transform.Find("RightContent/MapRoot/MapBg") as RectTransform;
        if (bgTf == null || pageRoot == null) return;
        var b = CaptureAdventureBase(bgTf);

        var corners = new Vector3[4];
        var rootCorners = new Vector3[4];
        bgTf.GetWorldCorners(corners);
        pageRoot.GetWorldCorners(rootCorners);
        float scaleX = bgTf.lossyScale.x;
        if (scaleX <= 1e-4f) return;

        // 把「已外扩量」减掉，还原出 base 状态的左边缘世界 x（幂等关键：重复调用从 base 重算）。
        float dApplied = (bgTf.offsetMin.x - b.offsetMin.x) * scaleX;
        float baseLeftX = corners[0].x - dApplied;

        // 右内缩对称：右边缘离页面右缘多少，左边缘就离页面左缘多少（内缩为负时按 0 处理，直接贴边）。
        float rightInset = rootCorners[2].x - corners[2].x;
        float targetLeftX = rootCorners[0].x + Mathf.Max(0f, rightInset);

        bgTf.offsetMin = new Vector2(
            b.offsetMin.x + (targetLeftX - baseLeftX) / scaleX,
            b.offsetMin.y);

        b.applied = true;
        _adventureUiBase[bgTf.GetInstanceID()] = b;
    }

    /// <summary>左侧栏原始兄弟序（-1 = 本会话还没动过）；改动 5 抬层后标准屏按它还原。</summary>
    int _leftSidebarBaseSibling = -1;

    /// <summary>
    /// 改动 5 配套 — 左侧栏抬层：预制体根节点下 LeftSidebar 排在 RightContent 之前（先渲染 = 下层），
    /// 地图背景左扩后会把它压在图下面（按钮看不见也点不到）。这里把 LeftSidebar 挪到 RightContent
    /// 之上、DetailPanel 之下（DetailPanel 是全宽面板，必须保持压住左侧栏底部）。
    /// 只在瘦屏适配路径调用；记录原始兄弟序，标准屏 / 关开关时由 RestoreAdventureSidebarOrder 还原。
    /// </summary>
    void FitAdventureSidebarAboveMap(RectTransform right)
    {
        var left = transform.Find("LeftSidebar");
        if (left == null || right == null) return;
        if (_leftSidebarBaseSibling < 0)
            _leftSidebarBaseSibling = left.GetSiblingIndex();
        int target = right.GetSiblingIndex() + 1;
        if (left.GetSiblingIndex() < target)
            left.SetSiblingIndex(target);
    }

    /// <summary>改动 5 配套回退：把 LeftSidebar 放回预制体原始兄弟序（没抬过层则 no-op）。</summary>
    void RestoreAdventureSidebarOrder(RectTransform right)
    {
        if (_leftSidebarBaseSibling < 0) return;
        var left = transform.Find("LeftSidebar");
        if (left != null) left.SetSiblingIndex(_leftSidebarBaseSibling);
        _leftSidebarBaseSibling = -1;
    }

    /// <summary>
    /// 改动 3 — DetailPanel 宽对齐 + 下沉到底：
    ///   - 宽对齐：水平从单点(0.5)改左右 stretch(0→1)，左右边距对称化后**夹进父级可视宽**，宽度随父自适应；
    ///   - 下沉到底：垂直改底部锚定(anchorMin.y=anchorMax.y=0) + pivot.y=0，底部抬到底部功能入口之上(offsetMin.y=bottomGap)；
    ///   - 高度保持原始值不变：offsetMax.y = offsetMin.y + 原始高度。
    /// </summary>
    /// <remarks>
    /// 2026-09-27 主人拍板修复「两边被切」——别再改回纯对称化：
    ///   根因：预制体里 DetailPanel 宽硬编码 711.4，而父级 RightContent 只有 720-170=550 宽，
    ///         原始矩形已向两侧各溢出 80.7；旧代码取 margin = Max(leftMargin, rightMargin)，
    ///         两侧都是 **负** −80.7，max 后仍为负 → 负边距被原样保留 → 继续溢出 → 被父级裁掉，
    ///         表现就是主人截图里「左边吃掉主线冒险标签、右边吃掉噩梦按钮」。
    ///   修法（单一入口）：对称化之后再显式夹到父级可视宽内 —— margin 不许为负，宽度不许超过父级宽。
    ///   「对称化」≠「夹进父级」，负边距是**溢出信号**，不是可用的边距。见 Docs/分辨率适配规范_v2_2026-09-27.md §3 必查 1。
    ///   本修复只在本界面自己的文件里做，不进通用文件（主人 2026-09-27 约束）。
    /// </remarks>
    void FitAdventureDetailPanel(RectTransform rt, float bottomGap)
    {
        var b = CaptureAdventureBase(rt);
        var parent = rt.parent as RectTransform;
        if (parent == null) return;
        Vector2 pMin = parent.rect.min;
        Vector2 pMax = parent.rect.max;
        Vector2 curMin = AnchorPointInParent(pMin, pMax, b.anchorMin) + b.offsetMin;
        Vector2 curMax = AnchorPointInParent(pMin, pMax, b.anchorMax) + b.offsetMax;

        float origH = curMax.y - curMin.y;   // 保持原始高度不变
        float parentW = pMax.x - pMin.x;
        float leftMargin = curMin.x - pMin.x;
        float rightMargin = pMax.x - curMax.x;
        float margin = Mathf.Max(leftMargin, rightMargin);   // 对称化：取两侧边距较大者

        // 2026-09-27 主人拍板：夹进父级可视宽。子面板宽度不许超过父级，否则会被父级裁掉（两边被切）。
        // 负边距 = 溢出信号，必须抬到 0；父级太窄时至少给 1px，避免算出负宽度。
        margin = Mathf.Max(0f, margin);
        if (parentW - margin * 2f < 1f)
            margin = Mathf.Max(0f, (parentW - 1f) * 0.5f);

        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.offsetMin = new Vector2(margin, bottomGap);
        rt.offsetMax = new Vector2(-margin, bottomGap + origH);

        b.applied = true;
        _adventureUiBase[rt.GetInstanceID()] = b;
    }

    /// <summary>
    /// 改动 4 — MapRoot 中间拉伸填满：MapRoot 自带 MapBg 背景层，瘦屏下把"地图内容上方挤、中间空一大块"的空隙补满。
    ///   - 顶边不变：offsetMax.y 保持 base（RightContent 顶下 14），只改高度；
    ///   - 底边落到「DetailPanel 顶边 + MAP_BOTTOM_GAP」：高度 = 顶边 - (DetailPanel顶 + MAP_BOTTOM_GAP)；
    ///     手算 offsetMin.y = offsetMax.y - h（offsetMax.y 不动），高度=h、顶边固定、底边自然下移到目标；
    ///   - DetailPanel 顶边按"上次改完后的实际位置"算（detail.offsetMin.y + 原始高度，detail 现在底部锚定）。
    /// 仅更瘦屏 + EnableAdventureMapStretch 为真时执行；关开关或标准屏由 ApplyAdventureFit 的 RestoreAdventureBase 还原。
    /// </summary>
    void FitAdventureMapRootStretch(RectTransform mapRoot, RectTransform right, RectTransform detail, float navReserved)
    {
        var b = CaptureAdventureBase(mapRoot);
        var rightBase = CaptureAdventureBase(right);
        var detailBase = CaptureAdventureBase(detail);

        // 逻辑高 H：RightContent 的父即 AdventureUI 根（stretch 全屏），取其 rect.height。
        var rootRt = right.parent as RectTransform;
        float H = rootRt != null && rootRt.rect.height > 0f ? rootRt.rect.height : GameConfig.DESIGN_HEIGHT;

        // MapRoot 顶边（bottom-origin，root 帧）：RightContent 顶(H + rightBase.offsetMax.y) + MapRoot 顶溢出(b.offsetMax.y)，保持不动。
        float mapTopY = H + rightBase.offsetMax.y + b.offsetMax.y;
        // DetailPanel 顶边（bottom-origin）：底部锚定，顶 = 当前底距 offsetMin.y + 原始高度。
        float origDetailH = detailBase.offsetMax.y - detailBase.offsetMin.y;
        float detailTopY = detail.offsetMin.y + origDetailH;

        float h = mapTopY - (detailTopY + MAP_BOTTOM_GAP);
        // 上限约束：MapRoot 自带 MapBg 背景层，拉过头就是主人说的「背景框放大了」。
        // 可视范围 = root 高 H - 顶部占用 - 底部功能入口预留 - DetailPanel 高度 - 中间间隙，超出部分不再拉。
        float topOccupied = Mathf.Max(0f, H - mapTopY);
        float maxH = H - topOccupied - navReserved - origDetailH - MAP_BOTTOM_GAP;
        if (maxH > 0f && h > maxH) h = maxH;
        if (h < 1f) h = 1f;   // 极端屏高兜底，避免算出负值

        // anchor y=(1,1) 单点 + pivot.y=1：sizeDelta.y = offsetMax.y - offsetMin.y。
        // 保持 offsetMax.y（顶边）不变，只把 offsetMin.y 推到 offsetMax.y - h，即高度=h、顶边不动、底边落到 detailTop+gap。
        mapRoot.offsetMax = new Vector2(MAP_RIGHT_INSET, b.offsetMax.y);
        mapRoot.offsetMin = new Vector2(b.offsetMin.x, b.offsetMax.y - h);

        b.applied = true;
        _adventureUiBase[mapRoot.GetInstanceID()] = b;
    }

    /// <summary>
    /// 瘦屏空隙治理（主人 2026-10-17「高屏有很大空隙，尽量保持原始比例」）：
    /// MapRoot 保持预制体原始高度不变，把「地图顶边 ~ 详情面板顶边」带内的多余高度
    /// 按 MAP_EXTRA_TOP_SHARE 上下分配 —— 地图整体在带内垂直居中，只平移、零缩放，
    /// MapBg 背景框 / 关卡立绘 / 标题栏全部保持原始尺寸与比例（改动 4 拉伸方案主人已否，不启用）。
    /// 幂等：drop 始终从 base 重算，不累积；标准屏 / 关开关由 ApplyAdventureFit 的 RestoreAdventureBase 还原。
    /// 依赖顺序：必须在 FitAdventureDetailPanel 之后调用（要拿详情面板最终顶边）。
    /// </summary>
    void FitAdventureMapRootCentered(RectTransform mapRoot, RectTransform right, RectTransform detail)
    {
        var b = CaptureAdventureBase(mapRoot);
        var rightBase = CaptureAdventureBase(right);
        var detailBase = CaptureAdventureBase(detail);

        // 逻辑高 H：RightContent 的父即 AdventureUI 根（stretch 全屏），取其 rect.height。
        var rootRt = right.parent as RectTransform;
        float H = rootRt != null && rootRt.rect.height > 0f ? rootRt.rect.height : GameConfig.DESIGN_HEIGHT;

        // 地图顶边（bottom-origin，root 帧）：RightContent 顶(H + rightBase.offsetMax.y) + MapRoot 顶溢出(b.offsetMax.y)。
        float mapTopBase = H + rightBase.offsetMax.y + b.offsetMax.y;
        float mapH = b.offsetMax.y - b.offsetMin.y;   // 预制体原始高度（654），保持不变
        // 详情面板顶边（底部锚定后，root 帧）：底距 + 原始高度。
        float origDetailH = detailBase.offsetMax.y - detailBase.offsetMin.y;
        float detailTop = detail.offsetMin.y + origDetailH;

        // 带内多余高度 → 按比例上下分配（drop = 地图整体下移量，地图上留 drop、下留 extra-drop）。
        float bandH = mapTopBase - (detailTop + MAP_BOTTOM_GAP);
        float extra = bandH - mapH;
        float drop = extra > 0f ? extra * MAP_EXTRA_TOP_SHARE : 0f;

        // MapRoot 锚 y=(1,1) 单点贴 RC 顶：offset 都是相对 RC 顶边，同时减 drop = 整体下移、高度不变。
        mapRoot.offsetMax = new Vector2(MAP_RIGHT_INSET, b.offsetMax.y - drop);
        mapRoot.offsetMin = new Vector2(b.offsetMin.x, b.offsetMin.y - drop);

        b.applied = true;
        _adventureUiBase[mapRoot.GetInstanceID()] = b;
    }

    /// <summary>
    /// WorldMapBtn 竖屏跟随（2026-10-17）：按钮是 RightContent 居中锚点，逻辑高变大时
    /// 它的绝对位置随屏心漂移，和地图的相对关系会变（设计屏上压在地图底边左下角，高屏会飘进空隙）。
    /// 这里按 base 锚点在设计屏高（1280）下反推「按钮中心 − 地图底边」的相对量，
    /// 再把按钮中心钉到「当前地图底边 + 该相对量」，只平移 y、x 不动，尺寸不变。
    /// 幂等：始终从 base 重算；标准屏 / 关开关由 RestoreAdventureBase 还原。
    /// </summary>
    void FitWorldMapBtnToMap(RectTransform btn, RectTransform right, RectTransform mapRoot)
    {
        var btnBase = CaptureAdventureBase(btn);
        var rightBase = CaptureAdventureBase(right);
        var mapBase = CaptureAdventureBase(mapRoot);

        var rootRt = right.parent as RectTransform;
        float H = rootRt != null && rootRt.rect.height > 0f ? rootRt.rect.height : GameConfig.DESIGN_HEIGHT;

        // 设计屏高下的几何（bottom-origin，root 帧）：反推「按钮中心在地图底边上方多少」。
        float designH = GameConfig.DESIGN_HEIGHT;
        float rcMinDesign = rightBase.offsetMin.y;
        float rcMaxDesign = designH + rightBase.offsetMax.y;
        float btnMinDesign = Mathf.Lerp(rcMinDesign, rcMaxDesign, btnBase.anchorMin.y) + btnBase.offsetMin.y;
        float btnMaxDesign = Mathf.Lerp(rcMinDesign, rcMaxDesign, btnBase.anchorMax.y) + btnBase.offsetMax.y;
        float btnCenterDesign = (btnMinDesign + btnMaxDesign) * 0.5f;
        float mapBottomDesign = rcMaxDesign + mapBase.offsetMin.y;
        float offsetAboveMapBottom = btnCenterDesign - mapBottomDesign;   // ≈146（设计屏：按钮中心在地图底边上方146）

        // 运行时：地图底边（已含居中下移）→ 目标按钮中心 → 反推按钮 offset（x 保持 base）。
        float rcMaxNow = H + rightBase.offsetMax.y;
        float mapBottomNow = rcMaxNow + mapRoot.offsetMin.y;
        float btnCenterNow = mapBottomNow + offsetAboveMapBottom;
        float anchorYNow = Mathf.Lerp(rightBase.offsetMin.y, rcMaxNow, btnBase.anchorMin.y);
        float halfSumNow = btnCenterNow - anchorYNow;                        // (offsetMin.y+offsetMax.y)/2 的目标值
        float halfSumBase = (btnBase.offsetMin.y + btnBase.offsetMax.y) * 0.5f;
        float d = halfSumNow - halfSumBase;

        btn.offsetMin = new Vector2(btnBase.offsetMin.x, btnBase.offsetMin.y + d);
        btn.offsetMax = new Vector2(btnBase.offsetMax.x, btnBase.offsetMax.y + d);

        btnBase.applied = true;
        _adventureUiBase[btn.GetInstanceID()] = btnBase;
    }
}
