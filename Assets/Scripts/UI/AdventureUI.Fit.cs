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

    /// <summary>根矩形未就绪的补跑协程在跑（防重复起协程；ApplyAdventureFit 顶部会清标志，页面隐藏杀协程后也不会卡死后续重试）。</summary>
    bool _adventureFitWaitingRoot;
    /// <summary>「根矩形未就绪挂起重试」诊断只打一次（每次 Play 会话一条，帮助定位进页太早问题）。</summary>
    bool _adventureFitWaitLogged;

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
        _adventureFitWaitingRoot = false;   // 重跑即视为补跑流程结束：页面隐藏杀协程后标志不残留，后续还能再挂
        if (_adventureUiApplying) return;
        _adventureUiApplying = true;
        try
        {
            // 左列标签随手机宽度等比缩放（2026-10-24 主人拍板「左侧标签可以根据手机宽度等比缩放，
            // 否则在宽的手机上看的就太小了」）：宽屏逻辑宽>720，按可见逻辑宽/720 放大保持占屏宽比例；
            // 瘦屏/标准屏逻辑宽恒 720 → s=1 与现状零差别。挂在瘦屏闸门之前——宽屏也要生效；
            // s 每次现算，s=1 即自然回预制体缩放，无需单独还原口。
            FitSidebarWidthScale();

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
                // 整列容器（主线/无限/活动副本）随资源条下移的还原（2026-10-27；未处理过 = no-op）
                if (mainNodeRestore != null) RestoreAdventureBase(mainNodeRestore as RectTransform);
                var endlessNodeRestore = right != null ? right.Find(EndlessNodeName) : null;
                if (endlessNodeRestore != null) RestoreAdventureBase(endlessNodeRestore as RectTransform);
                var activityNodeRestore = right != null ? right.Find(ActivityNodeName) : null;
                if (activityNodeRestore != null) RestoreAdventureBase(activityNodeRestore as RectTransform);
                var wmBtnRestore = mainNodeRestore != null ? mainNodeRestore.Find("WorldMapBtn") as RectTransform : null;
                if (wmBtnRestore != null) RestoreAdventureBase(wmBtnRestore);
                // 关卡Viewport 拉伸还原 + 滑动内容高度还原（2026-10-24 瘦屏拉伸的配套回退；未拉伸过 = no-op）
                var vpRestore = mainNodeRestore != null ? mainNodeRestore.Find(ChestViewportName) as RectTransform : null;
                if (vpRestore != null) RestoreAdventureBase(vpRestore);
                if (_chestScroll != null && _chestScroll.content != null)
                {
                    var contentRestore = _chestScroll.content;
                    contentRestore.sizeDelta = new Vector2(contentRestore.sizeDelta.x, _chestContentBaseDeltaY);
                }
                // 无限页关卡列还原（2026-10-24 主人「无限节点要和主线一样」的配套回退；未处理过 = no-op）
                var endlessRestore = mainNodeRestore != null ? mainNodeRestore.Find(EndlessNodeName) : null;
                var riftTitleRestore = endlessRestore != null ? endlessRestore.Find("TitleBar") as RectTransform : null;
                if (riftTitleRestore != null) RestoreAdventureBase(riftTitleRestore);
                RestoreRiftColumnFit();
                // 顶部两块（横幅/左列）让位资源条的还原（2026-10-26；未处理过 = no-op）
                var topBannerRestore = transform.Find("Top") as RectTransform;
                if (topBannerRestore != null) RestoreAdventureBase(topBannerRestore);
                var sidebarRestore = transform.Find("LeftSidebar") as RectTransform;
                if (sidebarRestore != null) RestoreAdventureBase(sidebarRestore);
                // 主线列表瘦屏重排的还原（槽位浮动基准 / 支线岛基准；列表未绑定时内部自动 no-op）
                // 【2026-10-26 跨章连续列】不再撤克隆槽：列表常态 15 槽，还原只需把基准收回归位
                //（克隆槽由 RestoreSlotColumnBaseY 按预制体节奏向上堆恢复）。
                ShrinkSlotCount(MaxSlotCount);
                RestoreSlotColumnBaseY();
                RefreshChestContentSize();   // 【2026-10-26 跨章连续列】还原收回内容高后按 15 槽重新撑出滚动余量（含基准补偿）
                RestoreBranchIslandBaseY();
                RestoreActivityRowsScale();
                RestoreAdventureSidebarEven();
                RestoreAdventureSidebarOrder(right);
                return;
            }

            // 首帧 Canvas 可能尚未 rebuild，rect 仍为 0 → 旧代码直接 return 会永久失效。
            // 先强制刷新一次画布再取尺寸（保持同步调用，不改用协程）；仍为 0 则挂起逐帧重试（见 BeginFitWhenRootReady）。
            var canvas = root.GetComponentInParent<Canvas>();
            if (canvas != null) Canvas.ForceUpdateCanvases();
            var rootRt = root as RectTransform;
            if (rootRt == null || rootRt.rect.width <= 0f || rootRt.rect.height <= 0f)
            {
                BeginFitWhenRootReady();
                return;
            }

            // 底部五入口（公会/角色/冒险/酒馆/冒险日志）占住屏幕底部，详情面板必须给它让位，
            // 否则面板底部（进入关卡按钮那一排）会被压在导航栏下面看不见。
            float navReserved = EnableNavGapReserve ? ResolveBottomNavReserved(rootRt) : 0f;
            float detailBottomGap = DETAIL_PANEL_BOTTOM_GAP;
            if (EnableNavGapReserve)
            {
                // 【2026-10-27 修】量不到导航高度（导航未就绪，navReserved=0）必须走兜底常量。
                // 旧写法 gap = 0 + NAV_GAP_PAD(8) = 8 恒 > 0，兜底永远不触发 →
                // 详情面板贴到屏幕最底（编辑器实测 detailBottom=8、detailTop=398，与正常帧 148/538 来回跳），
                // 主列重排带 bandBottom 也跟着在 611/471 之间翻转，瘦屏重排与滚动位置两帧不一致。
                detailBottomGap = navReserved > 0f
                    ? navReserved + NAV_GAP_PAD
                    : DETAIL_PANEL_BOTTOM_GAP_FALLBACK;
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
            // 顶部两块（主线冒险横幅 Top + 左列 LeftSidebar）整体下移到资源条之下
            // （2026-10-26 主人「冒险界面的资源条没有适配」：资源条挂 SafeAreaFitter 下移了，
            //   页面顶部还停在预制体贴顶位 → 真机上资源条压住模式按钮、横幅吊在最顶）。
            FitAdventureTopBelowResourceStrip(rootRt);
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

            // ── 2026-10-27 追加诊断（主人反馈「左列标签不见 / 岛屿跑出屏」）──
            // 每次瘦屏适配落位后打一帧实测全家福：左列按钮/横幅/标题条的真实落点 + 滚动位置 + 槽位基准。
            // 用真实数字定位根因；定位完这一段可整段删除。
            if (rootRt != null)
            {
                float rootMinY = rootRt.rect.min.y;
                string EdgeInfo(RectTransform r)
                {
                    if (r == null) return "null";
                    var cc = new Vector3[4];
                    r.GetWorldCorners(cc);
                    float y0 = rootRt.InverseTransformPoint(cc[0]).y - rootMinY;
                    float y1 = rootRt.InverseTransformPoint(cc[1]).y - rootMinY;
                    float x0 = rootRt.InverseTransformPoint(cc[0]).x - rootRt.rect.min.x;
                    return $"{r.name} act={r.gameObject.activeInHierarchy} sib={r.GetSiblingIndex()} x0={x0:F0} yAboveBottom[{y0:F0}~{y1:F0}]";
                }
                var sbLog = new System.Text.StringBuilder();
                var sidebarLog = transform.Find("LeftSidebar") as RectTransform;
                if (sidebarLog != null)
                {
                    sbLog.Append(EdgeInfo(sidebarLog));
                    if (modeButtons != null)
                        for (int i = 0; i < modeButtons.Length; i++)
                        {
                            var b = modeButtons[i];
                            if (b == null) { sbLog.Append($" btn{i}=null"); continue; }
                            var brt = b.transform as RectTransform;
                            var cgLog = b.GetComponent<CanvasGroup>();
                            sbLog.Append($" btn{i}(act={b.gameObject.activeInHierarchy},ap={brt.anchoredPosition.x:F0}/{brt.anchoredPosition.y:F0},alpha={cgLog?.alpha.ToString("0.##") ?? "-"})");
                        }
                }
                else sbLog.Append("LeftSidebar=null");
                var titleLog = right != null ? right.Find(MainNodeName + "/TitleBar") as RectTransform : null;
                string scrollLog = "noScroll";
                if (_chestScroll != null && _chestScroll.content != null && _chestScroll.viewport != null)
                    scrollLog = $"norm={_chestScroll.verticalNormalizedPosition:F3} contentH={_chestScroll.content.rect.height:F0} viewH={_chestScroll.viewport.rect.height:F0}";
                var slotLog = new System.Text.StringBuilder();
                if (_slotNodes != null && _slotFloatBasePos != null)
                {
                    int nLog = Mathf.Min(3, _slotNodes.Count);
                    for (int i = 0; i < nLog; i++)
                    {
                        var sLog = _slotNodes[i];
                        if (sLog == null) continue;
                        var cc = new Vector3[4];
                        ((RectTransform)sLog).GetWorldCorners(cc);
                        float midY = rootRt.InverseTransformPoint((cc[0] + cc[1]) * 0.5f).y - rootMinY;
                        float baseY = i < _slotFloatBasePos.Count ? _slotFloatBasePos[i].y : -1f;
                        slotLog.Append($" slot{i}(base={baseY:F0},aboveBot={midY:F0},ap={((RectTransform)sLog).anchoredPosition.y:F0},act={sLog.gameObject.activeInHierarchy})");
                    }
                    slotLog.Append($" markerSlot={_markerCurrentSlot}");
                }
                Debug.Log($"[AdventureFit诊断] Top[{EdgeInfo(transform.Find("Top") as RectTransform)}] " +
                          $"TitleBar[{EdgeInfo(titleLog)}] LeftSidebar[{sbLog}] {scrollLog} {slotLog}");
            }
        }
        finally
        {
            _adventureUiApplying = false;
        }
    }

    /// <summary>
    /// 根矩形未就绪时的补跑（2026-10-24 修「退出战斗回冒险页标题下移」）。
    /// 病根：战斗回城 LoadScene(Town) 后 TownHubController.Start() 同帧调 OpenAdventure → ShowPage →
    /// ApplyAdventureFit，此刻页面刚实例化、宿主 Canvas 还没跑过一帧布局，rootRt.rect=0 →
    /// 旧代码直接 return 放弃；而页面已在前台，底部导航点「冒险」不会重跑 ShowPage
    /// → 适配永远没补跑，整页停在预制体位：瘦屏上 TitleBar 比适配位低 (H−1280)/2 ≈ 165，
    /// 即主人截图里的「标题下移」（同帧可见佐证：槽位仍是预制体 4 槽，适配后应为 5 槽）。
    /// 修法：rect=0 时起协程逐帧等根矩形有效，再补跑一次 ApplyAdventureFit（幂等，等价于晚一帧进页）。
    /// </summary>
    void BeginFitWhenRootReady()
    {
        if (_adventureFitWaitingRoot) return;
        _adventureFitWaitingRoot = true;
        if (!_adventureFitWaitLogged)
        {
            _adventureFitWaitLogged = true;
            Debug.Log("[AdventureFit] 进页时根矩形未量出（rect=0），挂起重试待布局就绪后补跑适配");
        }
        StartCoroutine(CoFitWhenRootReady());
    }

    IEnumerator CoFitWhenRootReady()
    {
        while (transform is RectTransform rt && (rt.rect.width <= 0f || rt.rect.height <= 0f))
            yield return null;
        _adventureFitWaitingRoot = false;
        ApplyAdventureFit();
        // 【2026-10-26 修「打完关卡出来左侧标签拉开间隔」】适配真正落位后，补放 ShowPage 时因 rect=0 挂起的左侧标签滑入
        FlushLeftTabsPopPending();
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
        // 帧解析改用视口（中心=原关卡容器预制体帧中心，纵向按列占高/分辨率带撑出），槽位换算走内容实际中心（见 3.5）。
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
        if (!TryResolveMainChildFrames(right, mainNode, guankaFrame, H, out float gAnchorNow, out float gPivotNow)
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
        // 上限压点按「可见带 5 关」算（2026-10-26 跨章连续列：列里常态 15 槽，只把可见 5 关排进带，
        // 多出的槽位向下溢出视口成滚动余量——若按 15 槽算节奏会把可见 5 关挤成密不透风）：
        // 带里装得下整章 5 关时无需更大间隔，间隔最多压回「5 关正好排满带」的值；
        // 带特别矮时最多压回预制体节奏，再矮的部分被详情面板挡住（窗口语义）。
        float rhythm = Mathf.Clamp((bandTop - bandBottom) / Mathf.Max(1, GameConfig.STAGES_PER_GRAND - 1),
                                   rhythmPrefab, rhythmPrefab * MainSlotSpacingBoost);

        // 窗口槽位拉满跨章数（2026-10-26 跨章连续列：15 槽铺 3 章，可见 5 关按带排，其余滚出来）
        int slotsBefore = _slotNodes.Count;
        int slotCount = EnsureSlotCount(MaxSlotCount);
        // 一次性诊断（真机把这条发回来即可核对节奏与带高）
        if (!_mainColDiagLogged)
        {
            _mainColDiagLogged = true;
            Debug.Log($"[AdventureFit] 主列重排诊断 H={H:F0} bandBottom={bandBottom:F0} bandTop={bandTop:F0} " +
                      $"rhythmPrefab={rhythmPrefab:F0} rhythm={rhythm:F0} slotCount={slotCount} prefab={prefabPos.Count}");
        }

        // 根帧槽位目标 y（顶槽贴 bandTop 往下铺；先算根帧，再按内容实际中心换算本地 y）
        var ysRoot = new List<float>(slotCount);
        for (int i = 0; i < slotCount; i++)
            ysRoot.Add(bandTop - (slotCount - 1 - i) * rhythm);

        // ── 3.5) 关卡Viewport 按分辨率拉伸 + 滑动内容高度自适应
        // 【2026-10-24 主人拍板「滑动列表显示区域太小，要根据分辨率进行拉伸」】
        // 视口顶边钉 TitleBar（保持设计间距，随钉顶平移）、底边钉 DetailPanel 顶（保持设计间距）；
        // 内容高度 = max(视口高, 列占高)（列超出视口的部分才滚得动）；
        // 槽位换算直接用内容实际中心（视口顶 − 内容高/2，内容顶边恒钉视口顶），不依赖 refShift 恒等式。
        // 幂等：base 恒为原值，rt 每次从 base + 本轮参考值重算；标准屏/关开关由 ApplyAdventureFit 顶部还原。
        var vpBase = CaptureAdventureBase(guankaFrame);
        float vpPivotY = vpBase.pivot.y;
        float vpH0 = vpBase.offsetMax.y - vpBase.offsetMin.y;
        float vpTopDesign = gPivotDesign + (1f - vpPivotY) * vpH0;
        float vpBotDesign = gPivotDesign - vpPivotY * vpH0;
        float vpTopTarget = (tbPivotNow + tbShift) - (tbPivotDesign - vpTopDesign);
        float vpBotTarget = detailTopNow + (vpBotDesign - detailTopDesign);
        float vpHNew = vpTopTarget - vpBotTarget;
        float vpTopUsed, vpHUsed;
        if (Mathf.Abs(vpPivotY - 0.5f) > 0.01f)
        {
            WarnMainColumnOnce($"关卡Viewport pivot.y={vpPivotY:F2} 非 0.5，放弃拉伸（槽位换算按中心锚推导）");
            vpTopUsed = gPivotNow + (1f - vpPivotY) * vpH0;
            vpHUsed = vpH0;
        }
        else if (vpHNew < 1f)
        {
            WarnMainColumnOnce($"视口拉伸异常（bottom={vpBotTarget:F0} top={vpTopTarget:F0}），保持原视口");
            vpTopUsed = gPivotNow + (1f - vpPivotY) * vpH0;
            vpHUsed = vpH0;
        }
        else
        {
            guankaFrame.offsetMin = new Vector2(vpBase.offsetMin.x, vpBotTarget - gAnchorNow);
            guankaFrame.offsetMax = new Vector2(vpBase.offsetMax.x, vpTopTarget - gAnchorNow);
            vpBase.applied = true;
            _adventureUiBase[guankaFrame.GetInstanceID()] = vpBase;
            vpTopUsed = vpTopTarget;
            vpHUsed = vpHNew;
        }

        // 滑动内容高度自适应：max(视口高, 列占高+上下半格余量)，拉伸锚下 sizeDelta.y = 高度 − 视口高
        float spanRoot = ysRoot[ysRoot.Count - 1] - ysRoot[0] + 180f;   // 90=单格视觉半径（SlotColumnSpan 同口径）
        float contentH = Mathf.Max(vpHUsed, spanRoot);
        var contentRt = _chestScroll != null ? _chestScroll.content : null;
        if (contentRt != null)
            contentRt.sizeDelta = new Vector2(contentRt.sizeDelta.x, contentH - vpHUsed);

        // 内容实际中心（root 帧）= 视口顶 − 内容高/2（内容顶边恒钉视口顶）
        float contentCenterY = vpTopUsed - contentH * 0.5f;
        var ys = new List<float>(slotCount);
        for (int i = 0; i < slotCount; i++)
            ys.Add(ysRoot[i] - contentCenterY);
        ApplySlotColumnStretchY(ys);
        // 槽位增补后内容窗口变了（2026-10-26 跨章列常态一次增补 11 槽）：立刻刷新把各关三态铺上。
        // 放在 ApplySlotColumnStretchY 之后——此时基准/内容高已定稿，刷新里算的滚动余量与定位不会被再覆盖。
        if (slotCount != slotsBefore)
            RefreshChestNodes();
        // 基准/内容高重写后滚动余量变了：把当前关重新滚进视口（无标识或内容不比视口高时内部 no-op）
        ScrollChestToCurrent(_markerCurrentSlot);

        // ── 3.6) 无限页关卡列与主线同带重排【2026-10-24 主人「无限裂隙节点有点偏下了 要和主线一样的」】
        // 无限/关卡 与 主线/关卡 同构（1-1..1-4 同名同基准），主线列瘦屏被拉进带内后无限列还在预制体位 → 显偏下。
        // 修 = 无限/TitleBar 随主线同一 tbShift 钉顶；无限槽位按序号对齐主线根帧目标 y（ysRoot[0..3] 底对齐同节奏，
        // 主线第 5 槽是增补槽，无限保持预制体 4 槽不增补）。x 不动。
        // 幂等：槽位基准首触记录、目标每次从 ysRoot 现算；标准屏/关开关由 RestoreRiftColumnFit 还原。
        var endlessNode = right.Find(EndlessNodeName) as RectTransform;
        if (endlessNode != null)
        {
            var riftTitle = endlessNode.Find("TitleBar") as RectTransform;
            if (riftTitle != null)
            {
                var rtb = CaptureAdventureBase(riftTitle);
                riftTitle.offsetMin = new Vector2(rtb.offsetMin.x, rtb.offsetMin.y + tbShift);
                riftTitle.offsetMax = new Vector2(rtb.offsetMax.x, rtb.offsetMax.y + tbShift);
                rtb.applied = true;
                _adventureUiBase[riftTitle.GetInstanceID()] = rtb;
            }
            var riftGuanKa = endlessNode.Find("关卡") as RectTransform;
            if (riftGuanKa != null
                && TryResolveMainChildFrames(right, endlessNode, riftGuanKa, H, out _, out float eCenterNow))
            {
                var riftSlots = CollectNumberedSlots(riftGuanKa);
                EnsureRiftSlotBase(riftSlots);
                // 【2026-10-26 跨章连续列】ysRoot 已是全列 15 槽；无限列只有 4 槽，
                // 对齐「可见带 5 关」的根帧位（bandTop 顶格往下按 rhythm 铺），与主线视觉带一致。
                int n = Mathf.Min(riftSlots.Count, GameConfig.STAGES_PER_GRAND);
                for (int i = 0; i < n; i++)
                {
                    var srt = riftSlots[i];
                    float bandYRoot = bandTop - (GameConfig.STAGES_PER_GRAND - 1 - i) * rhythm;
                    float targetY = bandYRoot - eCenterNow;   // 槽位中心锚：根帧目标 − 容器中心
                    var p = srt.anchoredPosition;
                    if (!Mathf.Approximately(p.y, targetY))
                        srt.anchoredPosition = new Vector2(p.x, targetY);
                }
            }
        }

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

    /// <summary>
    /// 左列模式按钮紧凑化——【2026-10-27 主人拍板「左侧标签不见了，是上次改的间隔的事」】已禁用。
    /// 运行时按隐藏钮空位重排会在真机上导致左列标签错位/丢失；主人已在预制体手动调好 95 等距，
    /// 以预制体为准，运行时不再重排（2026-10-23「直接用我的预制体的数值」口径）。
    /// 改回旧行为：删此注释段、还原下方方法体即可（还原分支 RestoreAdventureSidebarEven 已就位、no-op 安全）。
    /// </summary>
    void FitSidebarEvenSpacing()
    {
        // 故意空实现：左列标签位置完全交给预制体，运行时零改动。
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

    // ────────────────────────────────────────────────────
    // 顶部两块让位资源条（2026-10-26 主人「冒险界面的资源条没有适配」）：
    // 病根：共享资源条（GuildHall 的 TopBar）挂 SafeAreaFitter.Offset，整条下移
    // TotalTopInsetUnits（安全区顶 + 微信胶囊预留 + 固定 20）；而冒险页顶部的
    // 「主线冒险」横幅（Top）与左列模式按钮容器（LeftSidebar）还是预制体贴顶位
    // （横幅顶边距顶 19、首颗模式按钮 142）——瘦屏真机上资源条（≈safeTop+20 起）
    // 正好压在模式按钮上，横幅孤零零吊在最顶（主人截图实锤）。
    // 治法：量出资源条底边（世界四角 → 页根本地，与 ResolveBottomNavReserved 同口径），
    // 把 Top + LeftSidebar 两块用【同一个位移量】下移到「资源条底 + 间距」之下——
    // 两块共用一个 delta，预制体里横幅→按钮的相对节奏保持不变。
    // 幂等：全部从 CaptureAdventureBase 的预制体 base 重算，不累积；
    // 标准屏 / 关开关由 ApplyAdventureFit 的 restore 分支还原（本方法只在瘦屏路径调用）。
    // 缺真值不兜底：资源条节点找不到 / 还没量出有效位置时不动页面，等补跑协程。
    // ────────────────────────────────────────────────────

    /// <summary>资源条底边与页面顶部内容（横幅顶边）之间的视觉间距（含药丸列悬出条底 ~5 的余量）。</summary>
    const float TopStripGapUnits = 16f;

    void FitAdventureTopBelowResourceStrip(RectTransform rootRt)
    {
        if (rootRt == null || rootRt.rect.height <= 0f) return;

        // 资源条真源：主界面 TopBar；独立打开（无大厅）时是本页挂的 TopBar / SharedResourceBar。
        Transform strip = null;
        if (GuildHallUI.Instance != null)
            strip = FindDeepChild(GuildHallUI.Instance.transform, "TopBar");
        if (strip == null)
            strip = FindDeepChild(transform, "TopBar") ?? FindDeepChild(transform, "SharedResourceBar");
        var stripRt = strip as RectTransform;
        if (stripRt == null) return;

        // 资源条底边（世界四角 [0]=左下）→ 页根本地 →「距页根顶边多少」
        var corners = new Vector3[4];
        stripRt.GetWorldCorners(corners);
        float rootBottomY = rootRt.rect.min.y;
        float aboveBottom = rootRt.InverseTransformPoint(corners[0]).y - rootBottomY;
        float stripBottomFromTop = rootRt.rect.height - aboveBottom;
        if (stripBottomFromTop <= 1f) return;   // 量不到有效位置（刚实例化/未布局）→ 等下一轮补跑

        // 位移量以横幅 base 顶边为基准：横幅顶边 → 资源条底 + 间距。
        var topBanner = transform.Find("Top") as RectTransform;
        if (topBanner == null) return;
        var bb = CaptureAdventureBase(topBanner);
        float topEdgeY = Mathf.Lerp(rootRt.rect.min.y, rootRt.rect.max.y, bb.anchorMax.y) + bb.offsetMax.y;
        float bannerTopFromTop = rootRt.rect.height - topEdgeY;
        float shift = bannerTopFromTop - (stripBottomFromTop + TopStripGapUnits);   // 负 = 下移

        ApplyTopNodeShift(topBanner, shift);
        ApplyTopNodeShift(transform.Find("LeftSidebar") as RectTransform, shift);
        // 【2026-10-27 主人「章节标题和资源条重叠」】主线/无限/活动副本整列内容随同一 shift 下移，避资源条。
        // 整列容器下移后，FitMainColumnStretch 已算好的「标题↔槽位↔支线岛」内部节奏整体跟着走，相对不变；
        // 这些容器不被其它 fit 改动自身 offset（FitMainColumnStretch 只动其子节点），套同一 shift 安全幂等。
        var rcShift = transform.Find("RightContent") as RectTransform;
        if (rcShift != null)
        {
            ApplyTopNodeShift(rcShift.Find(MainNodeName) as RectTransform, shift);
            ApplyTopNodeShift(rcShift.Find(EndlessNodeName) as RectTransform, shift);
            ApplyTopNodeShift(rcShift.Find(ActivityNodeName) as RectTransform, shift);
        }
    }

    /// <summary>顶部块整体平移 shift（负 = 下移），offset 从 base 重算保证幂等。null 安全。</summary>
    void ApplyTopNodeShift(RectTransform rt, float shift)
    {
        if (rt == null) return;
        var b = CaptureAdventureBase(rt);
        rt.offsetMin = new Vector2(b.offsetMin.x, b.offsetMin.y + shift);
        rt.offsetMax = new Vector2(b.offsetMax.x, b.offsetMax.y + shift);
        b.applied = true;
        _adventureUiBase[rt.GetInstanceID()] = b;
    }

    // ────────────────────────────────────────────────────
    // 左列标签随手机宽度等比缩放（2026-10-24 主人拍板）：
    // 宽屏（比 9:16 更方）走高度匹配 → 逻辑宽 = 1280×宽高比 > 720，标签保持预制体尺寸
    // 占屏宽比例就变小（主人「宽的手机上看的就太小了」）→ 按可见逻辑宽/720 等比放大，
    // 标签占屏宽比例与 9:16 恒一致；瘦屏/标准屏/极端黑边机型逻辑宽恒 720 → s=1 零改动。
    // 幂等：s 每次现算乘预制体基准缩放，s=1 即自然还原，不走 _adventureUiBase（那是锚点/偏移账本）。
    // ────────────────────────────────────────────────────

    /// <summary>LeftSidebar 预制体基准缩放（首次触碰记录；0 向量 = 未记录）。</summary>
    Vector3 _sidebarBaseScale;

    void FitSidebarWidthScale()
    {
        var sidebar = transform.Find("LeftSidebar") as RectTransform;
        if (sidebar == null) return;
        float s = Mathf.Clamp(UiLayoutStretch.ResolveUiLogicalWidth() / GameConfig.DESIGN_WIDTH, 1f, 1.25f);
        if (_sidebarBaseScale.x <= 0f && _sidebarBaseScale.y <= 0f && _sidebarBaseScale.z <= 0f)
            _sidebarBaseScale = sidebar.localScale;   // 首次记预制体基准（含主人手摆的缩放，如果有）
        var target = new Vector3(_sidebarBaseScale.x * s, _sidebarBaseScale.y * s, _sidebarBaseScale.z);
        if (sidebar.localScale != target)
            sidebar.localScale = target;
    }

    // ────────────────────────────────────────────────────
    // 无限页关卡列重排（2026-10-24 主人「无限裂隙节点有点偏下了 要和主线一样的」）：
    // 槽位（名字「数字-数字」，与主线槽位识别同款口径）基准首触记录；Fit 每次从主线 ysRoot 现算；
    // 标准屏/关开关由 RestoreRiftColumnFit 还原。
    // ────────────────────────────────────────────────────

    /// <summary>无限/关卡 已重排过的槽位（还原用）。</summary>
    readonly List<RectTransform> _riftFitSlots = new List<RectTransform>();
    /// <summary>对应槽位的预制体基准 anchoredPosition。</summary>
    readonly List<Vector2> _riftSlotBasePos = new List<Vector2>();

    /// <summary>收集容器下名字形如「数字-数字」的槽位，按当前 y 升序（底→顶）。</summary>
    static List<RectTransform> CollectNumberedSlots(RectTransform container)
    {
        var list = new List<RectTransform>();
        for (int i = 0; i < container.childCount; i++)
        {
            if (container.GetChild(i) is RectTransform rt
                && System.Text.RegularExpressions.Regex.IsMatch(rt.name, @"^\d+-\d+$"))
                list.Add(rt);
        }
        list.Sort((a, b) => a.anchoredPosition.y.CompareTo(b.anchoredPosition.y));
        return list;
    }

    /// <summary>槽位基准首触记录（节点集变化才重记；正常一次记录后不再变）。</summary>
    void EnsureRiftSlotBase(List<RectTransform> slots)
    {
        if (_riftFitSlots.Count == slots.Count)
        {
            bool same = true;
            for (int i = 0; i < slots.Count; i++)
            {
                if (_riftFitSlots[i] != slots[i]) { same = false; break; }
            }
            if (same) return;
        }
        _riftFitSlots.Clear();
        _riftSlotBasePos.Clear();
        for (int i = 0; i < slots.Count; i++)
        {
            _riftFitSlots.Add(slots[i]);
            _riftSlotBasePos.Add(slots[i].anchoredPosition);
        }
    }

    /// <summary>无限页关卡列重排回退：槽位回预制体位（没处理过则 no-op）。</summary>
    void RestoreRiftColumnFit()
    {
        for (int i = 0; i < _riftFitSlots.Count; i++)
        {
            var s = _riftFitSlots[i];
            if (s != null) s.anchoredPosition = _riftSlotBasePos[i];
        }
        _riftFitSlots.Clear();
        _riftSlotBasePos.Clear();
    }
}
