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
    /// 主线槽位间隔放大倍数（2026-10-23 主人「岛屿之间的间隔再大 50%」）。
    /// 实际节奏 = min(带高 ÷ (槽位上限 - 1), 预制体节奏 × 此倍数)：
    /// 带里装得下整章时恰好排满、底部空隙消失；带特别高时间隔封顶在 ×1.5。
    /// </summary>
    const float MainSlotSpacingBoost = 1.5f;

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
                // TitleBar/WorldMapBtn 在 2026-10-23 预制体重构后挂 主线 下，right.Find 直接找不到 → 经 MainNodeName 查
                var mainNodeRestore = right != null ? right.Find(MainNodeName) : null;
                var tbRestore = mainNodeRestore != null ? mainNodeRestore.Find("TitleBar") as RectTransform : null;
                if (tbRestore != null) RestoreAdventureBase(tbRestore);
                var wmBtnRestore = mainNodeRestore != null ? mainNodeRestore.Find("WorldMapBtn") as RectTransform : null;
                if (wmBtnRestore != null) RestoreAdventureBase(wmBtnRestore);
                // 主线列表瘦屏重排的还原（槽位浮动基准 / 支线岛基准；列表未绑定时内部自动 no-op）
                ShrinkSlotCount(4);   // 克隆增补的槽位撤掉，窗口回到预制体 4 槽
                RestoreSlotColumnBaseY();
                RestoreBranchIslandBaseY();
                RestoreActivityRowsScale();
                RestoreAdventureSidebarEven();
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
            // 左列模式按钮：2026-10-23 晚主人已手动调好预制体位置 → 运行时不再重排（预制体为准）。
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
            // 2026-10-23 主人「右边列表瘦屏要跟着拉长，别跟下面关卡信息有大空隙」：
            // 主线列表（TitleBar / 槽位 / 支线岛 / 世界地图按钮）在带内重排。
            // 必须在 FitAdventureDetailPanel 之后（要拿它的最终顶边）。
            if (right != null && detail != null)
                FitMainColumnStretch(right, detail);
            // 活动副本横条等比放大（2026-10-23 主人「活动副本右侧这个标签应该随着宽度等比放大」）。
            FitActivityRowsScale(rootRt);
            // 左列模式按钮紧凑化（2026-10-23 晚主人「直接用我的预制体的数值」）：
            // 主人把 5 钮调成 95 等距，但隐藏的每日/BOSS 占住 1/3 号位 → 可见的 0/2/4 视觉间距 190。
            // 这里按主人节奏把可见钮向下紧凑排（首钮不动），标准屏/关开关走 RestoreAdventureSidebarEven 还原。
            FitSidebarEvenSpacing();
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

    // ────────────────────────────────────────────────────
    // 主线列表瘦屏拉伸（2026-10-23 主人「右边这个是个列表，手机高度拉长时右边也要相应拉长，
    // 不要跟下面的关卡信息有太大空隙」）
    //
    // 病根（prefab 实测）：主线 / 关卡 / 槽位全是 center 单点锚，槽位中心 root y = H/2 + 本地 y，
    //   瘦屏时整列随屏幕中心往下漂；而 DetailPanel 底端锚定沉底不动，
    //   列底与详情面板之间、列顶与标题栏之间的空隙随屏高越拉越大。
    // 治法（只重排不缩放，保持预制体排版节奏）：
    //   · TitleBar 上移 (H-设计高)/2 —— 钉住「距 RightContent 顶」的设计间距；
    //   · WorldMapBtn 下移 (H-设计高)/2 —— 钉回设计屏的绝对位置（与沉底 DetailPanel 间距不变）；
    //   · 槽位列 + 支线岛在「DetailPanel 顶 + 设计底间距 ~ TitleBar 底 - 设计顶间距」带内，
    //     按预制体 y 节奏等比插值重排（底槽贴面板、顶槽贴标题栏，中间保持原比例）；
    //   · 支线岛容器与关卡容器同锚同位 → 同一参考系，frac 沿主列跨度插值。
    // 幂等：全部从 CaptureAdventureBase 的预制体 base 解析，重复调用不累积；
    //       标准屏 / 关开关由 ApplyAdventureFit 的 restore 分支还原（含槽位/岛浮动基准）。
    // 注意：WorldMapBtn 的旧跟随逻辑 FitWorldMapBtnToMap 因 MapRoot 已不在预制体而长期 no-op，
    //       本方法接手它的纵向钉位；将来若 MapRoot 回归需二选一，别双写。
    // ────────────────────────────────────────────────────

    /// <summary>主线列表重排的缺失/异常只报一次（不影响幂等——重排本身始终从 base 重算）。</summary>
    bool _mainColumnStretchWarned;

    /// <summary>主列重排诊断只打一次（瘦屏槽位容量排查）。</summary>
    bool _mainColDiagLogged;

    void WarnMainColumnOnce(string reason)
    {
        if (_mainColumnStretchWarned) return;
        _mainColumnStretchWarned = true;
        Debug.LogError($"[AdventureFit] 主线列表瘦屏拉伸不生效：{reason}（缺真值不兜底）");
    }

    /// <summary>RightContent 顶边在 root 帧 y（bottom-origin；RC 垂直方向从无 fit 改动，base 即当前）。</summary>
    float ResolveRightTopY(RectTransform right, float H)
    {
        var rb = CaptureAdventureBase(right);
        return Mathf.Lerp(0f, H, rb.anchorMax.y) + rb.offsetMax.y;
    }

    /// <summary>
    /// 主线子节点的两个参考点在 root 帧 y（bottom-origin）：
    ///   anchorY = 节点锚点位置（其子节点 anchoredPosition 的参考点）；
    ///   pivotY  = 节点 pivot 参考点位置（= anchorY + anchoredPosition.y，即美术摆的「节点位置」）。
    /// 链：RightContent(base) → 主线(base) → 节点(base)；base 均为预制体原始值（主线子树无其它 fit 改动）。
    /// H 传当前逻辑高 / 设计高，分别得现在值 / 设计值。
    /// 节点须为垂直单点锚（anchorMin.y == anchorMax.y），否则返回 false（调用方 no-op，不静默错排）。
    /// </summary>
    bool TryResolveMainChildFrames(RectTransform right, RectTransform mainNode, RectTransform node,
                                   float H, out float anchorY, out float pivotY)
    {
        anchorY = 0f; pivotY = 0f;
        if (right == null || mainNode == null || node == null) return false;
        var rb = CaptureAdventureBase(right);
        var mb = CaptureAdventureBase(mainNode);
        var nb = CaptureAdventureBase(node);
        if (Mathf.Abs(nb.anchorMin.y - nb.anchorMax.y) > 1e-4f) return false;

        float rcMin = Mathf.Lerp(0f, H, rb.anchorMin.y) + rb.offsetMin.y;
        float rcMax = Mathf.Lerp(0f, H, rb.anchorMax.y) + rb.offsetMax.y;
        float mMin = Mathf.Lerp(rcMin, rcMax, mb.anchorMin.y) + mb.offsetMin.y;
        float mMax = Mathf.Lerp(rcMin, rcMax, mb.anchorMax.y) + mb.offsetMax.y;
        anchorY = Mathf.Lerp(mMin, mMax, nb.anchorMin.y);
        // 单点锚：offsetMin = anchoredPosition - pivot * sizeDelta → 反推 anchoredPosition.y
        float h = nb.offsetMax.y - nb.offsetMin.y;
        pivotY = anchorY + nb.offsetMin.y + nb.pivot.y * h;
        return true;
    }

    /// <summary>主线列表瘦屏重排总入口（ApplyAdventureFit 调，须在 FitAdventureDetailPanel 之后）。</summary>
    void FitMainColumnStretch(RectTransform right, RectTransform detail)
    {
        // Transform.Find 返回 Transform（不是 RectTransform）→ as 转一次给帮手传参用
        var mainNode = right.Find(MainNodeName) as RectTransform;
        if (mainNode == null || detail == null) return;   // 缺主线时 EnsureChestNodes/EnsureBranchIslands 已报过

        // 绑定（幂等）：槽位 / 支线岛列表就绪（RefreshAll 已跑过，这里兜一次）
        EnsureChestNodes();
        EnsureBranchIslands();

        // 【2026-10-24】关卡容器被滑动列表包裹后搬到 主线/关卡Viewport/关卡：
        // 帧解析改用视口（= 原关卡容器预制体帧，同锚同位同尺寸），槽位换算走 ChestScrollRefShift 补偿。
        var guankaFrame = mainNode.Find(ChestViewportName) as RectTransform;
        if (guankaFrame == null)
            guankaFrame = mainNode.Find("关卡") as RectTransform;   // 未包裹（异常路径）兜查
        var titleBar = mainNode.Find("TitleBar") as RectTransform;
        var wmBtn = mainNode.Find("WorldMapBtn") as RectTransform;
        var rootRt = right.parent as RectTransform;
        float H = rootRt != null && rootRt.rect.height > 0f ? rootRt.rect.height : GameConfig.DESIGN_HEIGHT;
        float designH = GameConfig.DESIGN_HEIGHT;

        // ── 1) TitleBar：钉「距 RC 顶」的设计间距（= 上移 (H-设计高)/2）──
        float rcTopNow = ResolveRightTopY(right, H);
        float rcTopDesign = ResolveRightTopY(right, designH);
        if (!TryResolveMainChildFrames(right, mainNode, titleBar, H, out _, out float tbPivotNow)
            || !TryResolveMainChildFrames(right, mainNode, titleBar, designH, out _, out float tbPivotDesign))
        {
            WarnMainColumnOnce("找不到 主线/TitleBar 或其锚型不是垂直单点锚");
            return;
        }
        float tbPivotTarget = rcTopNow - (rcTopDesign - tbPivotDesign);
        float tbShift = tbPivotTarget - tbPivotNow;
        var tbBase = CaptureAdventureBase(titleBar);
        titleBar.offsetMin = new Vector2(tbBase.offsetMin.x, tbBase.offsetMin.y + tbShift);
        titleBar.offsetMax = new Vector2(tbBase.offsetMax.x, tbBase.offsetMax.y + tbShift);
        tbBase.applied = true;
        _adventureUiBase[titleBar.GetInstanceID()] = tbBase;

        // ── 2) WorldMapBtn：钉回设计屏绝对位置（= 下移 (H-设计高)/2；与沉底 DetailPanel 间距不变）──
        if (wmBtn != null
            && TryResolveMainChildFrames(right, mainNode, wmBtn, H, out _, out float wmPivotNow)
            && TryResolveMainChildFrames(right, mainNode, wmBtn, designH, out _, out float wmPivotDesign))
        {
            float wmShift = wmPivotDesign - wmPivotNow;   // 瘦屏为负 = 下移
            var wmBase = CaptureAdventureBase(wmBtn);
            wmBtn.offsetMin = new Vector2(wmBase.offsetMin.x, wmBase.offsetMin.y + wmShift);
            wmBtn.offsetMax = new Vector2(wmBase.offsetMax.x, wmBase.offsetMax.y + wmShift);
            wmBase.applied = true;
            _adventureUiBase[wmBtn.GetInstanceID()] = wmBase;
        }

        // ── 3) 槽位列 + 支线岛：带内按预制体节奏插值重排 ──
        var prefabPos = SlotPrefabBasePos;
        if (prefabPos == null || prefabPos.Count < 2)
        {
            WarnMainColumnOnce("主线槽位节点不足 2 个，无法定插值节奏");
            return;
        }
        if (!TryResolveMainChildFrames(right, mainNode, guankaFrame, H, out _, out float gPivotNow)
            || !TryResolveMainChildFrames(right, mainNode, guankaFrame, designH, out _, out float gPivotDesign))
        {
            WarnMainColumnOnce("找不到 主线/关卡Viewport（原关卡容器帧）或其锚型不是垂直单点锚");
            return;
        }

        // DetailPanel 顶（fit 后，root 帧）——照 FitAdventureMapRootCentered 的现成口径
        var detailBase = CaptureAdventureBase(detail);
        float origDetailH = detailBase.offsetMax.y - detailBase.offsetMin.y;
        float detailTopNow = detail.offsetMin.y + origDetailH;
        // 设计屏的 DetailPanel 顶（base 解析；底端锚与屏高无关）
        float detailTopDesign = Mathf.Lerp(0f, designH, detailBase.anchorMax.y) + detailBase.offsetMax.y;

        // 设计口径间距（解析自 base，不硬编码）：底槽中心在面板顶上方多少、TitleBar 顶在顶槽中心上方多少。
        // 槽位 anchoredPosition 的参考点：包裹滑动列表后 = 内容矩形中心（比原容器 pivot 点低 ChestScrollRefShift），
        // 设计间距按原容器口径算 → prefabPos 减掉 ChestScrollRefShift 还原成「相对原 pivot」的值。
        float refShift = ChestScrollRefShift;
        float colBottomDesign = gPivotDesign + prefabPos[0].y - refShift;
        float colTopDesign = gPivotDesign + prefabPos[prefabPos.Count - 1].y - refShift;
        float bottomGap = colBottomDesign - detailTopDesign;
        float topGap = tbPivotDesign - colTopDesign;

        // 瘦屏目标带：底 = 面板顶(fit后) + 设计底间距；顶 = TitleBar(钉顶后)顶 - 设计顶间距
        float bandBottom = detailTopNow + bottomGap;
        float bandTop = (tbPivotNow + tbShift) - topGap;
        if (bandTop - bandBottom < 1f)
        {
            WarnMainColumnOnce($"重排带异常（bandBottom={bandBottom:F0} bandTop={bandTop:F0}）");
            return;
        }

        // 预制体节奏（平均槽间距）
        float spanPrefab = prefabPos[prefabPos.Count - 1].y - prefabPos[0].y;
        float rhythmPrefab = spanPrefab / (prefabPos.Count - 1);

        // 2026-10-23 主人「岛屿之间的间隔再大 50%」：节奏 ×1.5（见 MainSlotSpacingBoost）。
        // 上限压点：带高 ÷ (槽位上限 - 1) —— 带里装得下整章 5 关时无需更大间隔，
        // 间隔最多压回「5 关正好排满带」的值，1-1…1-5 全部可见、底部空隙消失；
        // 带特别矮时最多压回预制体节奏，再矮的部分被详情面板挡住（窗口语义）。
        float rhythm = Mathf.Clamp((bandTop - bandBottom) / Mathf.Max(1, MaxSlotCount - 1),
                                   rhythmPrefab, rhythmPrefab * MainSlotSpacingBoost);

        // 窗口槽位固定拉满整章（间隔已按带高自适应，装得下就全摆）
        int slotCount = EnsureSlotCount(MaxSlotCount);
        // 一次性诊断（真机把这条发回来即可核对节奏与带高）
        if (!_mainColDiagLogged)
        {
            _mainColDiagLogged = true;
            Debug.Log($"[AdventureFit] 主列重排诊断 H={H:F0} bandBottom={bandBottom:F0} bandTop={bandTop:F0} " +
                      $"rhythmPrefab={rhythmPrefab:F0} rhythm={rhythm:F0} slotCount={slotCount} prefab={prefabPos.Count}");
        }

        var ys = new List<float>(slotCount);
        // 顶槽（最晚关）贴 bandTop，往下铺（root y → 内容帧本地 y）：参考中心比原容器 pivot 点低 refShift，加回
        for (int i = 0; i < slotCount; i++)
            ys.Add(bandTop - (slotCount - 1 - i) * rhythm - gPivotNow + refShift);
        if (slotCount != prefabPos.Count)
            RefreshChestNodes();   // 补/撤槽后内容窗口变了，立刻刷新把各关三态铺上，不等下次 ShowPage
        ApplySlotColumnStretchY(ys);

        // 岛屿 x 一律用预制体数值（2026-10-23 晚主人「直接用我的预制体的数值吧」：
        // ×3 错位放大已删——放大后左右不均且首尾槽没动，主人不要）。

        // 支线岛不参与瘦屏重排（2026-10-23 主人拍板：按示例摆位，间距不变），
        // 若此前被拉伸过（旧版本改动）这里复位一次。
        RestoreBranchIslandBaseY();

        // 槽位挪完位重铺岛屿连接点（2026-10-23 主人「岛屿之间加上点连接起来」）
        RebuildSlotConnectors();
    }

    // ────────────────────────────────────────────────────
    // 左列模式按钮紧凑化（2026-10-23 晚主人「直接用我的预制体的数值」）：
    // 病根不是适配代码（被冤枉半天）——主人把 5 钮调成 95 等距（0/-95/-190/-285/-380），
    // 但隐藏的每日副本/BOSS挑战占住 1/3 号位，可见的 0/2/4 号中心距 = 190，视觉上「还是分的开」。
    // 治法：瘦屏下把可见钮按主人节奏（全钮平均间距）自顶钮往下紧凑排——首钮钉预制体位，
    // 第 2/3 颗填进隐藏钮的槽位（= 0 / -95 / -190）。标准屏 / 关开关走 RestoreAdventureSidebarEven 还原。
    // ────────────────────────────────────────────────────

    /// <summary>紧凑化处理过的模式按钮（还原用）。</summary>
    readonly List<RectTransform> _sidebarEvenBtns = new List<RectTransform>();

    void FitSidebarEvenSpacing()
    {
        var left = transform.Find("LeftSidebar");
        var rootRt = transform as RectTransform;
        if (left == null || rootRt == null || rootRt.rect.height <= 0f) return;
        float H = rootRt.rect.height;
        _sidebarEvenBtns.Clear();

        // 全部 ModeBtn（含隐藏的）预制体中心 y（bottom-origin root 帧）→ 全钮平均间距 = 主人节奏
        var lb = CaptureAdventureBase(left as RectTransform);
        float lsMin = Mathf.Lerp(0f, H, lb.anchorMin.y) + lb.offsetMin.y;
        float lsMax = Mathf.Lerp(0f, H, lb.anchorMax.y) + lb.offsetMax.y;
        var allY = new List<float>();
        var visible = new List<(RectTransform rt, float y)>();
        for (int i = 0; i < left.childCount; i++)
        {
            var c = left.GetChild(i) as RectTransform;
            if (c == null || !c.name.StartsWith("ModeBtn_", StringComparison.Ordinal)) continue;
            var b = CaptureAdventureBase(c);
            if (Mathf.Abs(b.anchorMin.y - b.anchorMax.y) > 1e-4f) continue;   // 非垂直单点锚不动，防错排
            float anchorY = Mathf.Lerp(lsMin, lsMax, b.anchorMin.y);
            float h = b.offsetMax.y - b.offsetMin.y;
            float centerY = anchorY + b.offsetMin.y + b.pivot.y * h;
            allY.Add(centerY);
            if (c.gameObject.activeInHierarchy) visible.Add((c, centerY));
        }
        if (visible.Count < 2 || allY.Count < 2) return;
        allY.Sort();
        float step = (allY[allY.Count - 1] - allY[0]) / (allY.Count - 1);
        visible.Sort((a, b) => b.y.CompareTo(a.y));   // 屏幕高 → 低

        // 首钮（主线冒险）钉在预制体位，其余往下按主人节奏填隐藏钮的空位
        for (int k = 0; k < visible.Count; k++)
        {
            var rt = visible[k].rt;
            _sidebarEvenBtns.Add(rt);
            float target = visible[0].y - k * step;
            float delta = target - visible[k].y;
            if (Mathf.Abs(delta) < 0.5f) continue;
            var b = CaptureAdventureBase(rt);
            rt.offsetMin = new Vector2(b.offsetMin.x, b.offsetMin.y + delta);
            rt.offsetMax = new Vector2(b.offsetMax.x, b.offsetMax.y + delta);
            b.applied = true;
            _adventureUiBase[rt.GetInstanceID()] = b;
        }
    }

    /// <summary>左列紧凑化回退：把处理过的按钮还原到预制体位（没处理过则 no-op）。</summary>
    void RestoreAdventureSidebarEven()
    {
        for (int i = 0; i < _sidebarEvenBtns.Count; i++)
        {
            if (_sidebarEvenBtns[i] != null) RestoreAdventureBase(_sidebarEvenBtns[i]);
        }
        _sidebarEvenBtns.Clear();
    }
}
