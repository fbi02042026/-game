using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// 冒险界面 partial —— 2026-10-22 新增：大关内小关宝箱节点
//
// 【2026-10-22 主人拍板 + 预制体现状】
// · 主人已在预制体建好节点树：RightContent/关卡 下 4 个窗口节点（1-1…1-4），
//   每个含 bg(编号牌)/Text(编号)/宝箱(Image)/岛(Image)；位置、缩放、排版全以预制体为准，代码不碰。
// · 宝箱/岛的 sprite 因 56 字符 guid 拖不进 prefab（Inspector 里是空的）→
//   由代码按 Resources 路径填图（铁律 24/30 口径：路径加载，不走 prefab 引用）。
// · 【2026-10-23 批 B1】规则改为每**大关** 5 小关（1-1 … 1-5，下一大关变 2-1 …，章身份未动）：
//   第 3 关银箱、第 5 关（Boss）金箱、其余木箱；
//   已通关 → 开启；当前进度（下一关）→ 关闭；还没打到 → 未解锁；
//   一屏 4~5 个槽位（瘦屏自动补），按 y 从低到高 = 关卡从早到晚，窗口随进度自动定位（不许手动滑动）；
//   【2026-10-23 主人「最下面的层级在最上面，现在被上面那个岛挡住了」】槽位兄弟序反转：
//   下面的岛画在上面（当前关 ×1.35 放大后压住上方岛，而不是被压）—— ApplySlotZOrder，只调层级不动位置。
public partial class AdventureUI
{
    /// <summary>预制体节点容器 RightContent/关卡。</summary>
    Transform _guankaRoot;

    /// <summary>窗口槽位（按 y 升序 = 底→顶 = 关卡从早到晚；渲染层级反转：底槽画在最上）。</summary>
    readonly List<RectTransform> _slotNodes = new List<RectTransform>(4);
    readonly List<Image> _slotChestImgs = new List<Image>(4);
    readonly List<Image> _slotIslandImgs = new List<Image>(4);
    readonly List<Text> _slotLabels = new List<Text>(4);
    bool _guankaMissingReported;

    /// <summary>宝箱/岛图 Resources 目录。</summary>
    const string ChestResDir = "Art/UI/Adventure/宝箱/";

    /// <summary>关卡滑动列表视口名（2026-10-24 主人拍板「关卡列改滑动列表」；运行时生成挂主线节点下，翻章手势让路要按名字找，别改）。</summary>
    const string ChestViewportName = "关卡Viewport";
    /// <summary>当前关标识节点名（2026-10-24 主人在 AdventureUI 根下摆好的「头像指示」，代码只挪位置不改样式）。</summary>
    const string StageMarkerNodeName = "头像指示";
    /// <summary>标识里箭头节点名（灰锁章只隐它、头像保留）。</summary>
    const string StageMarkerArrowNodeName = "箭头";
    /// <summary>标识里头像节点名（点击回位挂在它上面）。</summary>
    const string StageMarkerAvatarNodeName = "头像";
    /// <summary>标识与槽顶的间隙（箭头尖到当前关槽顶；嫌贴太紧只改这一个数）。</summary>
    const float StageMarkerGap = 6f;

    /// <summary>三种材质的 sprite 路径（注意：木箱锁图叫「未解锁」，银/金叫「未开启」，素材名如此）。</summary>
    static readonly string[] ChestWoodPaths   = { ChestResDir + "木箱开启",   ChestResDir + "木箱关闭",   ChestResDir + "木箱未解锁" };
    static readonly string[] ChestSilverPaths = { ChestResDir + "银箱开启",   ChestResDir + "银箱关闭",   ChestResDir + "银箱未开启" };
    static readonly string[] ChestGoldPaths   = { ChestResDir + "金箱开启",   ChestResDir + "金箱关闭",   ChestResDir + "金箱未开启" };
    static readonly string[] IslandPaths      = { ChestResDir + "岛1",       ChestResDir + "岛2",       ChestResDir + "岛3" };

    /// <summary>状态下标：0=开启（已通关）1=关闭（当前进度）2=未解锁。</summary>
    const int ChestStateOpen = 0;
    const int ChestStateClosed = 1;
    const int ChestStateLocked = 2;

    /// <summary>银箱小关号（2026-10-23 批 B1：5 关制下金箱占第 5 关，银箱移到第 3 关）。</summary>
    const int ChestSilverStage = 3;

    /// <summary>窗口首尾槽位（底/顶）的透明度（2026-10-22 主人拍板：0.7）。</summary>
    const float ChestEdgeAlpha = 0.7f;

    /// <summary>已通关（奖已领）宝箱的灰度（2026-10-23 主人拍板「领取后的宝箱就还用关闭灰掉就行」）。</summary>
    static readonly Color ChestClearedTint = new Color(0.6f, 0.6f, 0.6f, 1f);

    // ── 主线宝箱 lifecycle 动画节奏（2026-10-23 主人拍板：通关未领=开启图可点领奖，领取完变灰→下移消失、
    //    上面槽位补位、新当前关从小到大开启；2026-10-23 晚常量自 BranchIslands.cs 搬来——常量跟使用者走）──
    /// <summary>打完变灰停留时长（秒）：灰状态停一拍让人看清「已领取」，再开始下移。</summary>
    const float BranchGrayHoldDur = 0.55f;
    /// <summary>打完退场时长（秒）：往下滑出 + 淡出。</summary>
    const float BranchExitDur = 0.45f;
    /// <summary>打完退场下滑距离（像素）。</summary>
    const float BranchExitSlide = 230f;
    /// <summary>新岛开启时长（秒）：从小到大 + 透明到出现。</summary>
    const float BranchGrowDur = 0.4f;
    /// <summary>新岛开启起始缩放（相对基准的倍数）。</summary>
    const float BranchGrowFromScale = 0.15f;

    /// <summary>窗口槽位上限（2026-10-23 主人「分辨率拉长了宝箱要多点」：瘦屏窗口装得下时克隆补槽，
    /// 上限 = 每大关小关数——窗口最多铺满整章，不出现重复关）。</summary>
    static int MaxSlotCount => Mathf.Max(4, GameConfig.STAGES_PER_GRAND);

    /// <summary>当前浮动槽位下标（2026-10-23 主人「只有当前开放的宝箱关卡有浮动」：仅关闭态槽位浮动，
    /// 其余钉回基准；-1 = 无（整章通关后无「关闭」位，全列静止））。RefreshChestNodes 每次刷新重算。</summary>
    int _currentFloatSlot = -1;

    // ── 槽位浮动（2026-10-23 主人反馈「岛和宝箱没有浮动动效」：原只浮岛且幅度太小看不见 →
    //    改整槽位（岛+宝箱+编号牌）一起浮，幅度加大到肉眼可见）──
    /// <summary>浮动幅度（像素，槽位基准 y 上下的偏移；槽位自带 0.7 缩放，屏显约 7px）。</summary>
    const float SlotFloatAmp = 10f;
    /// <summary>浮动角速度（弧度/秒）——约 5.2 秒一个来回，缓慢明显。</summary>
    const float SlotFloatSpeed = 1.2f;

    /// <summary>槽位根节点在预制体里的基准坐标（浮动围绕它做正弦，不写死坐标）。</summary>
    readonly List<Vector2> _slotFloatBasePos = new List<Vector2>(4);

    /// <summary>槽位根节点预制体原始坐标（只增不改的底账）：
    /// 瘦屏重排会改写 _slotFloatBasePos，插值节奏与一键还原都以这份为准（Fit 调 RestoreSlotColumnBaseY 回这里）。</summary>
    readonly List<Vector2> _slotPrefabBasePos = new List<Vector2>(4);

    /// <summary>当前关（关闭态）整槽位放大倍数（2026-10-23 主人反馈：原宝箱单独×2 太大且与岛脱节 →
    /// 改整组一起缩放，倍数降为 ×1.35；嫌大嫌小改这一个数）。</summary>
    const float SlotCurrentScale = 1.35f;

    /// <summary>槽位根节点预制体基准缩放缓存（key=槽位 rectTransform，首次刷新时记录，代码只做倍乘不写死数值）。</summary>
    readonly Dictionary<Transform, Vector3> _slotBaseScales = new Dictionary<Transform, Vector3>();

    readonly Dictionary<string, Sprite> _chestSpriteCache = new Dictionary<string, Sprite>();

    // ── 主线宝箱 lifecycle（2026-10-23 主人拍板：通关未领=开启图可点领奖；领完变灰→下移消失、
    //    上面槽位补位滑动；轮到新当前关=从小到大开启。同日晚主人「宝箱上面不用要那个感叹号
    //    只有剧情关才有」→ 主线槽位不再挂「可打」徽章（支线岛剧情节点的「可接」徽章保留）──
    /// <summary>宝箱领取奖励：首通 +3 钻【2026-10-24 主人拍板：只有 Boss 关（金箱）给，并进「获得奖励」弹窗一起发】。</summary>
    const int ChestClaimDiamond = 3;
    /// <summary>每个槽位当前显示的关号（1 起；-1=隐藏）——点击领奖反查用，刷新时写。</summary>
    readonly List<int> _slotStage = new List<int>();
    /// <summary>补位滑动偏移（>0 = 节点在基准上方这么多像素，Update 逐帧衰减到 0）。</summary>
    readonly List<float> _slotSlideOffset = new List<float>();
    /// <summary>领取退场协程（非空 = 动画拥有画面，刷新早退；ShowPage 进页清一次防页面隐藏杀协程后卡死）。</summary>
    Coroutine _claimRoutine;
    /// <summary>各章上次刷新的当前关（当前关前进了才播开启动画；切章/回城不播）。</summary>
    readonly Dictionary<int, int> _lastCurrentByChapter = new Dictionary<int, int>();

    // ── 刚通关宝箱「关闭→开启」动效（2026-10-24 主人拍板：回冒险页 → 刚打完那格
    //    左右快速晃 + 慢慢缩小 → 约 2 秒后猛地放大切成开启图回复原大小 → 才可点领奖）──
    /// <summary>开启揭示协程（非空 = 动效拥有画面，刷新早退；ShowPage 清残留）。</summary>
    Coroutine _openRevealRoutine;
    /// <summary>揭示动画所属章（玩家切到别的章时协程自灭还原、刷新放行；回切不重播）。</summary>
    int _revealChapter = -1;
    /// <summary>本次进页已播过揭示（同一次回城只播一次；ClearStaleClaimAnim 重置）。</summary>
    bool _revealPlayedThisVisit;
    /// <summary>晃动 + 缩小段总时长（秒）＝主人说的「2 秒后猛地放大」前段。</summary>
    const float RevealShakeDur = 2f;
    /// <summary>晃动幅度（度，localRotation.z 正弦摆动）。</summary>
    const float RevealShakeAmp = 8f;
    /// <summary>晃动角速度（弧度/秒，约 0.7 秒一个来回）。</summary>
    const float RevealShakeSpeed = 9f;
    /// <summary>缩小段终点（相对宝箱子图基准 scale 的倍数）。</summary>
    const float RevealShrinkTo = 0.6f;
    /// <summary>猛放大的过冲峰值（倍数）。</summary>
    const float RevealPopOver = 1.15f;
    /// <summary>猛放大段时长（秒）。</summary>
    const float RevealPopDur = 0.28f;

    Sprite LoadChestSprite(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (_chestSpriteCache.TryGetValue(path, out var sp) && sp != null) return sp;
        sp = Resources.Load<Sprite>(path);
        if (sp != null) _chestSpriteCache[path] = sp;
        return sp;
    }

    /// <summary>
    /// 绑定预制体 RightContent/主线/关卡 下的窗口节点（只绑一次）。
    /// 【2026-10-23 主人新布局】关卡容器搬到「主线」节点下（无限节点下也有一份同名的，宝箱进度只属主线）。
    /// 槽位识别：名字形如「数字-数字」的子节点，按 y 升序（底→顶）。
    /// </summary>
    void EnsureChestNodes()
    {
        if (_guankaRoot != null) return;
        var right = transform.Find("RightContent");
        _guankaRoot = right != null ? right.Find(MainNodeName + "/关卡") : null;
        if (_guankaRoot == null && right != null)
            _guankaRoot = right.Find("关卡");   // 旧布局残留路径
        if (_guankaRoot == null)
            _guankaRoot = transform.Find("关卡");
        if (_guankaRoot == null)
        {
            if (!_guankaMissingReported)
            {
                _guankaMissingReported = true;
                Debug.LogError("[AdventureUI] 预制体里找不到 RightContent/主线/关卡 容器，宝箱节点不显示（缺真值不兜底）");
            }
            return;
        }

        _slotNodes.Clear(); _slotChestImgs.Clear(); _slotIslandImgs.Clear(); _slotLabels.Clear();
        _slotFloatBasePos.Clear(); _slotBaseScales.Clear(); _slotPrefabBasePos.Clear();
        _slotStage.Clear(); _slotSlideOffset.Clear();

        var slots = new List<RectTransform>();
        for (int i = 0; i < _guankaRoot.childCount; i++)
        {
            var child = _guankaRoot.GetChild(i) as RectTransform;
            if (child == null) continue;
            if (!Regex.IsMatch(child.name, @"^\d+-\d+$")) continue;   // 只认「数字-数字」命名的槽位节点
            slots.Add(child);
        }
        // 按 y 升序：底→顶 = 关卡从早到晚
        slots.Sort((a, b) => a.anchoredPosition.y.CompareTo(b.anchoredPosition.y));

        foreach (var slot in slots)
        {
            _slotNodes.Add(slot);
            var chestImg = slot.Find("宝箱")?.GetComponent<Image>();
            var islandImg = slot.Find("岛")?.GetComponent<Image>();
            _slotChestImgs.Add(chestImg);
            _slotIslandImgs.Add(islandImg);
            _slotLabels.Add(slot.Find("bg/Text (Legacy)")?.GetComponent<Text>());
            _slotStage.Add(-1);
            _slotSlideOffset.Add(0f);

            // 槽位根的基准坐标/缩放记预制体值：浮动围绕基准做正弦，当前关放大做倍乘
            _slotFloatBasePos.Add(slot.anchoredPosition);
            _slotPrefabBasePos.Add(slot.anchoredPosition);
            _slotBaseScales[slot] = slot.localScale;

            // 不参与点按：关掉射线，避免挡地图左右滑翻章手势（关卡容器不在 MapRoot 子树内）
            var graphics = slot.GetComponentsInChildren<Graphic>(true);
            for (int g = 0; g < graphics.Length; g++)
                graphics[g].raycastTarget = false;
        }

        if (_slotNodes.Count < 4)
            Debug.LogError($"[AdventureUI] 关卡 容器下槽位节点数={_slotNodes.Count}（至少 4），窗口显示会不完整（缺真值不兜底）");

        // 渲染层级：下面的岛画在最上（2026-10-23 主人拍板）
        ApplySlotZOrder();

        // 【2026-10-24 主人拍板「关卡列改滑动列表」】运行时给 关卡 容器套竖向滑动视口（不改预制体排版）
        EnsureChestScrollWrap();
    }

    ScrollRect _chestScroll;
    /// <summary>滑动列表包裹后，内容参考中心相对原「关卡」容器 pivot 点的下移量 d（Fit 换算 ys 用；未包裹 = 0）。</summary>
    public float ChestScrollRefShift => _chestRefShift;
    float _chestRefShift;

    /// <summary>
    /// 关卡容器 → 滑动列表（只包一次）：
    /// 视口 = 关卡容器预制体原帧（同锚同位同尺寸，标准屏视觉零变化）；
    /// 关卡容器变成滚动内容（顶部锚定，高度按槽位列占高撑出，比视口高才滑得动，Clamped 不滑过头）。
    /// 槽位坐标不动：浮动/补位/拉伸全部继续在容器内坐标系里跑。
    /// </summary>
    void EnsureChestScrollWrap()
    {
        if (_chestScroll != null) return;
        if (_guankaRoot == null) return;
        var parent = _guankaRoot.parent as RectTransform;
        if (parent == null) return;
        var old = parent.Find(ChestViewportName);
        if (old != null)
        {
            _chestScroll = old.GetComponent<ScrollRect>();
            return;
        }

        var srcRt = (RectTransform)_guankaRoot;
        // 视口帧 = 容器预制体原帧
        var viewportGo = new GameObject(ChestViewportName, typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        var vp = (RectTransform)viewportGo.transform;
        vp.SetParent(parent, false);
        vp.anchorMin = srcRt.anchorMin;
        vp.anchorMax = srcRt.anchorMax;
        vp.pivot = srcRt.pivot;
        vp.anchoredPosition = srcRt.anchoredPosition;
        vp.sizeDelta = srcRt.sizeDelta;
        vp.localScale = Vector3.one;
        // 透明接收面：槽位图射线全关（防挡翻章），竖滑拖拽得落在这一层 ScrollRect 才收得到
        var vpImg = viewportGo.GetComponent<Image>();
        vpImg.color = new Color(0f, 0f, 0f, 0f);
        vpImg.raycastTarget = true;

        // 容器变内容：顶部拉伸锚定（顶边与视口顶边重合 = 原位置），向下按内容高度撑出滚动余量
        srcRt.SetParent(vp, false);
        srcRt.anchorMin = new Vector2(0f, 1f);
        srcRt.anchorMax = new Vector2(1f, 1f);
        srcRt.pivot = new Vector2(0.5f, 1f);
        srcRt.anchoredPosition = Vector2.zero;
        float viewH = Mathf.Max(1f, vp.rect.height);
        float span = SlotColumnSpan();
        float contentH = Mathf.Max(viewH, span);
        srcRt.sizeDelta = new Vector2(0f, contentH - viewH);   // 拉伸锚下 sizeDelta.y = 高度 - 视口高

        // 内容向下长高 d 后，槽位的锚点参考中心（内容矩形中心）跟着下移 d ——
        // 把槽位基准 y 补回 d，滚动到顶时槽位仍钉在原来的屏幕位置（视觉零变化）
        float d = (contentH - viewH) * 0.5f;
        _chestRefShift = d;
        for (int i = 0; i < _slotFloatBasePos.Count; i++)
            _slotFloatBasePos[i] = new Vector2(_slotFloatBasePos[i].x, _slotFloatBasePos[i].y + d);
        for (int i = 0; i < _slotPrefabBasePos.Count; i++)
            _slotPrefabBasePos[i] = new Vector2(_slotPrefabBasePos[i].x, _slotPrefabBasePos[i].y + d);

        var scroll = viewportGo.AddComponent<ScrollRect>();
        scroll.content = srcRt;
        scroll.viewport = vp;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;
        scroll.verticalNormalizedPosition = 1f;   // 从顶部起步（= 原视觉）
        _chestScroll = scroll;
    }

    /// <summary>槽位列占高（含上下各半格余量）：按预制体基准坐标算，拉伸/增补后以当前基准为准。</summary>
    float SlotColumnSpan()
    {
        var list = _slotFloatBasePos.Count > 0 ? _slotFloatBasePos : _slotPrefabBasePos;
        if (list.Count == 0) return 0f;
        float min = float.MaxValue, max = float.MinValue;
        for (int i = 0; i < list.Count; i++)
        {
            min = Mathf.Min(min, list[i].y);
            max = Mathf.Max(max, list[i].y);
        }
        float halfSlot = 90f;   // 单格视觉半径近似（岛+宝箱组），嫌余量不够只调这一个数
        return (max - min) + halfSlot * 2f;
    }

    /// <summary>把当前关滚到视口中部（刷新后调；内容不比视口高时不滚）。</summary>
    void ScrollChestToCurrent(int slotIdx)
    {
        if (_chestScroll == null || slotIdx < 0 || slotIdx >= _slotNodes.Count) return;
        var content = _chestScroll.content;
        var viewport = _chestScroll.viewport;
        if (content == null || viewport == null) return;
        float viewH = viewport.rect.height;
        float contentH = content.rect.height;
        if (contentH <= viewH + 1f) return;
        // 槽位锚点基准在内容中心（单点锚）：中心以下为 contentH/2
        float slotBelowTop = contentH * 0.5f - _slotFloatBasePos[slotIdx].y;
        float t = Mathf.Clamp01((slotBelowTop - viewH * 0.5f) / Mathf.Max(1f, contentH - viewH));
        _chestScroll.verticalNormalizedPosition = 1f - t;
    }

    /// <summary>按关号（1 起）找槽位下标；找不到（整章通关 current=total+1）返回 -1。</summary>
    int FindSlotIndexOfStage(int stage)
    {
        for (int i = 0; i < _slotStage.Count; i++)
            if (_slotStage[i] == stage) return i;
        return -1;
    }

    // ────────────────────────────────────────────────────
    // 当前关标识（2026-10-24 主人拍板 + 主人预制体节点「头像指示」）：
    // 真源 = 主人在 AdventureUI 根下摆的节点（头像/箭头图、尺寸、缩放全以预制体为准，代码不碰样式）。
    // 行为（主人原话「跟着滑动界面一起滑动并且一直指向当前关卡」「手动上滑时在最上或最下指向当前该打的关卡」）：
    // ① 节点挪进滑动列表视口（不进内容，不被裁掉），每帧贴着当前关槽位上方走；
    // ② 当前关滚出视口 → 钳在视口顶边/底边，箭头（主人的「旋转」容器）翻转指向当前关方向；
    // ③ 跨章后该难度没有可打的关（灰锁章）→ 只有头像没有箭头；
    // ④ 点头像 = 一键滚回该打的关。
    // ────────────────────────────────────────────────────

    RectTransform _stageMarker;
    RectTransform _stageMarkerArrow;
    RectTransform _stageMarkerArrowRot;   // 主人的「旋转」容器（箭头方向翻转用）
    int _markerCurrentSlot = -1;

    /// <summary>绑定主人摆的「头像指示」节点（只绑一次；缺真值 LogError 不兜底）。</summary>
    void EnsureStageMarkerNode()
    {
        if (_stageMarker != null) return;
        var root = transform.Find(StageMarkerNodeName);
        if (root == null)
        {
            Debug.LogError($"[AdventureUI] 根下找不到「{StageMarkerNodeName}」节点（主人预制体缺失），当前关标识无从显示（缺真值不兜底）");
            return;
        }
        _stageMarker = (RectTransform)root;
        _stageMarkerArrow = FindChildRecursive(_stageMarker, StageMarkerArrowNodeName);
        _stageMarkerArrowRot = _stageMarkerArrow != null ? _stageMarkerArrow.parent as RectTransform : null;
        // 挪进滑动列表视口（视口先于本方法建好，见 EnsureChestNodes → EnsureChestScrollWrap）；
        // 不进内容节点 → 不被 RectMask2D 沿内容裁掉，钳边时仍可见
        if (_chestScroll != null && _chestScroll.viewport != null && _stageMarker.parent != _chestScroll.viewport)
            _stageMarker.SetParent(_chestScroll.viewport, false);
        var avatar = FindChildRecursive(_stageMarker, StageMarkerAvatarNodeName);
        if (avatar != null)
        {
            var btn = avatar.GetComponent<Button>();
            if (btn == null) btn = avatar.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OnClickStageMarker);
        }
        else
        {
            Debug.LogError($"[AdventureUI] 「{StageMarkerNodeName}」下找不到「{StageMarkerAvatarNodeName}」子节点，点击回位无效（缺真值不兜底）");
        }
    }

    /// <summary>点击头像 = 一键回到该打的关卡（滑动列表滚回当前关；灰锁章同样生效）。</summary>
    void OnClickStageMarker()
    {
        ScrollChestToCurrent(_markerCurrentSlot);
    }

    /// <summary>
    /// 刷新标识状态（RefreshChestNodes 调）：记录当前关槽位、灰锁章只隐箭头；
    /// 位置由 <see cref="UpdateStageMarkerFollow"/> 每帧贴。
    /// </summary>
    void UpdateStageMarker(int slotIdx, bool chapterDiffLocked)
    {
        EnsureStageMarkerNode();
        if (_stageMarker == null) return;
        _markerCurrentSlot = slotIdx;
        bool hasSlot = slotIdx >= 0 && slotIdx < _slotNodes.Count && _slotNodes[slotIdx] != null;
        // 灰锁章（跨章后该难度没有可打的关）：只有头像没有箭头
        if (_stageMarkerArrow != null && _stageMarkerArrow.gameObject.activeSelf == chapterDiffLocked)
            _stageMarkerArrow.gameObject.SetActive(!chapterDiffLocked);
        if (_stageMarker.gameObject.activeSelf != hasSlot)
            _stageMarker.gameObject.SetActive(hasSlot);
        if (hasSlot)
            _stageMarker.SetAsLastSibling();
    }

    /// <summary>
    /// 标识每帧贴位（Update 调，槽位位置写入之后）：
    /// 当前关在视口内 → 贴其正上方；滚出视口 → 钳在顶/底边缘，箭头翻转指向当前关方向。
    /// </summary>
    void UpdateStageMarkerFollow()
    {
        if (_stageMarker == null || !_stageMarker.gameObject.activeSelf || _chestScroll == null) return;
        var viewport = _chestScroll.viewport;
        var slotRt = _markerCurrentSlot >= 0 && _markerCurrentSlot < _slotNodes.Count
            ? _slotNodes[_markerCurrentSlot] : null;
        if (viewport == null || slotRt == null || !slotRt.gameObject.activeInHierarchy)
        {
            if (_stageMarker.gameObject.activeSelf) _stageMarker.gameObject.SetActive(false);
            return;
        }
        var canvas = _stageMarker.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, slotRt.position, cam, out var slotLocal))
            return;
        // localPoint 以 viewport.pivot 为原点 → 换到中心系（钳边/贴位都按中心±halfH 算）
        slotLocal += new Vector2((0.5f - viewport.pivot.x) * viewport.rect.width,
                                 (0.5f - viewport.pivot.y) * viewport.rect.height);
        float tip = MarkerTipOffset();
        float halfH = viewport.rect.height * 0.5f;
        float centerY;
        bool flip;   // true = 箭头朝上（当前关在视口上方）
        if (slotLocal.y > halfH)
        {
            centerY = -halfH + tip;   // 钳底边，箭头朝上
            flip = true;
        }
        else if (slotLocal.y < -halfH)
        {
            centerY = halfH - tip;    // 钳顶边，箭头朝下
            flip = false;
        }
        else
        {
            centerY = Mathf.Clamp(slotLocal.y + tip, -halfH + tip, halfH - tip);
            flip = false;
        }
        _stageMarker.anchoredPosition = new Vector2(slotLocal.x, centerY);
        if (_stageMarkerArrowRot != null)
        {
            var e = _stageMarkerArrowRot.localEulerAngles;
            float z = flip ? 180f : 0f;
            if (Mathf.Abs(Mathf.DeltaAngle(e.z, z)) > 0.5f)
                _stageMarkerArrowRot.localEulerAngles = new Vector3(0f, 0f, z);
        }
    }

    /// <summary>箭头尖相对标识中心的渲染偏移（按主人预制体箭头摆位换算，主人改箭头 y 自动跟随）。</summary>
    float MarkerTipOffset()
    {
        if (_stageMarkerArrow == null) return 90f;
        return (Mathf.Abs(_stageMarkerArrow.anchoredPosition.y) + _stageMarkerArrow.rect.height * 0.5f)
               * Mathf.Max(0.01f, Mathf.Abs(_stageMarker.localScale.y));
    }

    /// <summary>递归找子节点（按名字，主人层级顺序不依赖）。</summary>
    static RectTransform FindChildRecursive(Transform root, string name)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            if (c.name == name) return (RectTransform)c;
            var r = FindChildRecursive(c, name);
            if (r != null) return r;
        }
        return null;
    }

    /// <summary>
    /// 槽位渲染层级反转（2026-10-23 主人「最下面的层级在最上面，现在被上面那个岛挡住了」）：
    /// 底槽（当前关）画在最上、顶槽（最晚关）画在最下 —— 当前关 ×1.35 放大后压住上方岛，而不是被压。
    /// 只调兄弟序（渲染顺序），不改位置/内容/预制体；槽位绑定、增补、缩减三个出口各调一次。
    /// 「连接点」容器（若有）永远垫在所有岛屿下层。
    /// </summary>
    void ApplySlotZOrder()
    {
        var dots = _guankaRoot != null ? _guankaRoot.Find(ConnectorRootName) : null;
        for (int i = 0; i < _slotNodes.Count; i++)
        {
            if (_slotNodes[i] == null) continue;
            _slotNodes[i].SetSiblingIndex(_slotNodes.Count - i - 1 + (dots != null ? 1 : 0));
        }
        if (dots != null) dots.SetSiblingIndex(0);
    }

    /// <summary>
    /// 瘦屏窗口装得下时增补槽位（2026-10-23 主人「摆放是 720×1280 摆 4 个，分辨率拉长了宝箱要多点」）：
    /// 克隆顶部槽位（岛/宝箱/编号牌结构同源）追加到窗口，返回当前槽位数。
    /// want 钳到 [4, MaxSlotCount]；克隆槽位Prefab基准记顶槽基准（还原/标准屏由 ShrinkSlotCount 撤掉）。
    /// </summary>
    public int EnsureSlotCount(int want)
    {
        EnsureChestNodes();
        if (_guankaRoot == null) return _slotNodes.Count;
        want = Mathf.Clamp(want, 4, MaxSlotCount);
        while (_slotNodes.Count < want)
        {
            var src = _slotNodes[_slotNodes.Count - 1];
            var clone = Instantiate(src, _guankaRoot) as RectTransform;
            // 命名沿用「数字-数字」槽位命名规则（9-9 不是真实关号，仅防再绑定误认；列表只绑一次不重扫）
            clone.name = "9-9";
            _slotNodes.Add(clone);
            _slotChestImgs.Add(clone.Find("宝箱")?.GetComponent<Image>());
            _slotIslandImgs.Add(clone.Find("岛")?.GetComponent<Image>());
            _slotLabels.Add(clone.Find("bg/Text (Legacy)")?.GetComponent<Text>());
            _slotStage.Add(-1);
            _slotSlideOffset.Add(0f);
            // 克隆源是已处理槽位：子图射线已关、CanvasGroup 已带（透明度每次刷新重算，无需再调）
            // 克隆槽位的浮动基准 = 源槽位当前基准（瘦屏下与 Fit 算出的列位对齐，下一帧 Update 生效）
            int srcIdx = _slotNodes.Count - 2;   // 源槽位在克隆加入前的下标
            var cloneFloatBase = (srcIdx >= 0 && srcIdx < _slotFloatBasePos.Count)
                ? _slotFloatBasePos[srcIdx]
                : src.anchoredPosition;
            var basePos = _slotPrefabBasePos[_slotPrefabBasePos.Count - 1];
            _slotPrefabBasePos.Add(basePos);
            _slotFloatBasePos.Add(cloneFloatBase);
            // 基准缩放继承源槽位的「基准」而非克隆体现值 —— 源若正处于「当前关 ×1.35」放大态，
            // 克隆会把放大值当基准，之后永远偏大（RefreshChestNodes 按这里记录的基准做倍乘）。
            var srcBaseScale = _slotBaseScales.TryGetValue(src, out var sb) ? sb : src.localScale;
            clone.localScale = srcBaseScale;
            _slotBaseScales[clone] = srcBaseScale;
            // 立即摆到浮动基准位（不等下一帧 Update，避免与源槽位重叠闪一帧）
            clone.anchoredPosition = cloneFloatBase;
        }
        ApplySlotZOrder();   // 增补后重排渲染层级（底槽画在最上）
        return _slotNodes.Count;
    }

    /// <summary>还原/标准屏：把克隆增补的槽位撤掉（销毁节点并从各列表剔除），窗口回到预制体 4 槽。</summary>
    public int ShrinkSlotCount(int want)
    {
        want = Mathf.Max(4, want);
        while (_slotNodes.Count > want)
        {
            int last = _slotNodes.Count - 1;
            var rt = _slotNodes[last];
            _slotNodes.RemoveAt(last);
            if (last < _slotChestImgs.Count) _slotChestImgs.RemoveAt(last);
            if (last < _slotIslandImgs.Count) _slotIslandImgs.RemoveAt(last);
            if (last < _slotLabels.Count) _slotLabels.RemoveAt(last);
            if (last < _slotFloatBasePos.Count) _slotFloatBasePos.RemoveAt(last);
            if (last < _slotPrefabBasePos.Count) _slotPrefabBasePos.RemoveAt(last);
            if (last < _slotStage.Count) _slotStage.RemoveAt(last);
            if (last < _slotSlideOffset.Count) _slotSlideOffset.RemoveAt(last);
            if (rt != null) _slotBaseScales.Remove(rt);
            if (rt != null) Destroy(rt.gameObject);
        }
        if (_currentFloatSlot >= _slotNodes.Count) _currentFloatSlot = -1;
        ApplySlotZOrder();   // 缩减后重排渲染层级（底槽画在最上）
        return _slotNodes.Count;
    }

    /// <summary>
        /// 按当前选中章刷新窗口内容。2026-10-23 主人拍板改「领取制」：
        /// · 已领取的关从列里消失（未领关依次入槽），上面的槽位补位滑动；
        /// · 最新通关且未领的宝箱 = 开启图（刚打完那格播「关闭→开启」揭示动效后可点领奖）；
        /// · 更早通关未领 = 关闭图，仍可点（奖励不丢）【2026-10-24 主人拍板】；
        /// · 轮到新当前关（当前关前进了）播开启动画：从小到大+透明到出现；
        /// · 可领宝箱所在的槽位 = 列底，不再做底槽淡出。
    /// 进度真源 = 存档 clearedStages（键 "章_关卡"，0 起关号）；领取真源 = chestClaimedStages。
    /// </summary>
    void RefreshChestNodes()
    {
        // 只在主线冒险模式显示；裂隙等模式整组隐藏
        bool show = _selectedMode == 0;
        EnsureChestNodes();
        if (_guankaRoot == null) return;
        if (_guankaRoot.gameObject.activeSelf != show)
            _guankaRoot.gameObject.SetActive(show);
        if (!show) return;
        // 领取退场/开启揭示动画拥有画面时早退（揭示动画跨章切换时放行刷新，协程自己还原退出）
        if (_claimRoutine != null || (_openRevealRoutine != null && _revealChapter == _selectedChapter)) return;

        var data = SaveSystem.Instance?.Data;
        int total = GameConfig.STAGES_PER_GRAND;
        int maxCleared = GetMaxClearedStage(_selectedChapter);
        int current = Mathf.Clamp(maxCleared + 1, 1, total + 1);   // 下一关（1 起）；整章通关 = total+1
        _currentFloatSlot = -1;   // 浮动槽位每次刷新重算（只有当前关浮动）

        // 【2026-10-24 主人拍板「难度逐章解锁」】该章在当前难度未开启（上一章该难度没通）
        // → 关卡列全部按「未解锁」灰锁 + 整列压暗，点开始冒险在 Interaction 端统一提示。
        bool diffLocked = !ChapterRouteTable.CanEnterAtDiff(_selectedChapter, _selectedDiff, data);

        RebuildSlotContents(-1, data, total, maxCleared, current, diffLocked);

        // 整列灰锁压暗（锁定时交互全关：宝箱按钮跟着 CanvasGroup.interactable 失效）
        var rootCg = _guankaRoot.GetComponent<CanvasGroup>();
        if (rootCg == null) rootCg = _guankaRoot.gameObject.AddComponent<CanvasGroup>();
        rootCg.alpha = diffLocked ? 0.55f : 1f;
        rootCg.interactable = !diffLocked;

        // 当前关标识（主人预制体「头像指示」）与滑动列表定位：
        // 灰锁章标识仍显示（只有头像没有箭头，指向该打的关）；内容比视口矮时不滚
        int markerSlot = FindSlotIndexOfStage(current);
        UpdateStageMarker(markerSlot, diffLocked);
        ScrollChestToCurrent(diffLocked ? -1 : _currentFloatSlot);

        // 首尾淡出：只淡最顶可见槽（列底=最早的未领关，可领宝箱不淡出；中段全亮）
        int lastVisible = -1;
        for (int i = 0; i < _slotStage.Count; i++)
            if (_slotStage[i] > 0) lastVisible = i;
        for (int slot = 0; slot < _slotNodes.Count; slot++)
        {
            if (slot >= _slotStage.Count || _slotStage[slot] <= 0) continue;
            var cg = _slotNodes[slot].GetComponent<CanvasGroup>();
            if (cg == null) cg = _slotNodes[slot].gameObject.AddComponent<CanvasGroup>();
            cg.alpha = slot == lastVisible ? ChestEdgeAlpha : 1f;
        }

        // 「轮到剧情关卡」开启动画：该章当前关前进了才播（切章/回城不播；灰锁章不播）
        if (!diffLocked && _lastCurrentByChapter.TryGetValue(_selectedChapter, out var lastCur)
            && current > lastCur && current <= total)
        {
            for (int slot = 0; slot < _slotNodes.Count && slot < _slotStage.Count; slot++)
            {
                if (_slotStage[slot] != current) continue;
                StartCoroutine(ChestCurrentGrowAnim(slot));
                break;
            }
        }
        _lastCurrentByChapter[_selectedChapter] = current;

        // 【2026-10-24 主人拍板】刚通关那格宝箱播「关闭→开启」动效：
        // 有待领掉落（AdventureRewardNotice 队尾=刚打完的关）才播，同一次进页只播一次；
        // 切到别的章不播（等切回来那一章再播，记录不消费）。
        if (!_revealPlayedThisVisit && !diffLocked)
        {
            int pendingStage0 = AdventureRewardNotice.LatestPendingStageInChapter(_selectedChapter);
            if (pendingStage0 >= 0)
            {
                int pendingStage1 = pendingStage0 + 1;
                for (int slot = 0; slot < _slotNodes.Count && slot < _slotStage.Count; slot++)
                {
                    if (_slotStage[slot] != pendingStage1) continue;
                    _revealPlayedThisVisit = true;
                    _openRevealRoutine = StartCoroutine(ChestOpenRevealAnim(slot));
                    break;
                }
            }
        }

        // 岛屿连接点（可见槽位两两相连；2026-10-23 主人「岛屿之间加上点连接起来」）
        RebuildSlotConnectors();
    }

    /// <summary>
    /// 重排槽位内容（窗口映射：未领关依次入槽，已领的关消失；skipSlot=领取退场中的槽位不写内容）。
    /// 内容下移一档的槽位给 +rhythm 滑动偏移，由 Update 逐帧衰减实现补位滑动。
    /// </summary>
    void RebuildSlotContents(int skipSlot, SaveData data, int total, int maxCleared, int current, bool diffLocked)
    {
        var oldStage = new int[_slotNodes.Count];
        for (int i = 0; i < _slotNodes.Count; i++)
            oldStage[i] = i < _slotStage.Count ? _slotStage[i] : -1;

        float rhythm = SlotRhythm();
        int cursor = 1;
        int currentSlot = -1;
        for (int slot = 0; slot < _slotNodes.Count; slot++)
        {
            // 找下一个未领关（已领的跳过）
            int stage = -1;
            while (cursor <= total)
            {
                bool cl = cursor <= maxCleared;
                bool claimed = cl && data != null && data.IsChestClaimed(_selectedChapter, cursor - 1);
                if (claimed) { cursor++; continue; }
                stage = cursor;
                cursor++;
                break;
            }
            if (slot < _slotStage.Count) _slotStage[slot] = stage;
            if (slot == skipSlot) continue;   // 退场中的槽位不动（收尾刷新再写新内容）

            if (stage < 0)
            {
                var go0 = _slotNodes[slot].gameObject;
                if (go0.activeSelf) go0.SetActive(false);
                if (slot < _slotSlideOffset.Count) _slotSlideOffset[slot] = 0f;
                continue;
            }
            if (!_slotNodes[slot].gameObject.activeSelf)
                _slotNodes[slot].gameObject.SetActive(true);
            WriteSlotContent(slot, stage, maxCleared, current, diffLocked);
            if (stage == current) currentSlot = slot;
            // 内容下移一档（新 stage 来自更上面的旧槽）→ +rhythm 偏移；内容上移/新出现直接就位
            if (slot < _slotSlideOffset.Count)
                _slotSlideOffset[slot] = (oldStage[slot] >= 0 && stage > oldStage[slot]) ? rhythm : 0f;
        }
        _currentFloatSlot = currentSlot;
    }

    /// <summary>写单个槽位的显示内容（三态图/缩放/岛/编号/可点领奖；边沿淡出与滑动偏移不在此）。
    /// 【2026-10-24 主人拍板「难度逐章解锁」】diffLocked=true 时一律按「未解锁」图显示且不可领。</summary>
    void WriteSlotContent(int slot, int stage, int maxCleared, int current, bool diffLocked)
    {
        var data = SaveSystem.Instance?.Data;
        bool isCleared = !diffLocked && stage <= maxCleared;
        bool isCurrent = !diffLocked && stage == current;
        bool isClaimable = isCleared && (data == null || !data.IsChestClaimed(_selectedChapter, stage - 1));

        // 三态换图 + 领取态（2026-10-24 主人拍板收紧口径）：
        // · 最新通关且未领（stage == maxCleared）→ 开启图（刚打完那格，等播「关闭→开启」动效后可点领奖）；
        // · 更早通关未领（stage < maxCleared）→ 关闭图，仍可点（奖励不丢）；
        // · 当前关（未打）→ 关闭图不可点；没打到 → 未解锁图。
        // 修「打了一关后两关都是开启」：旧口径凡通关未领一律开图，多关未领时全开。
        string[] paths = ChestPathsForStage(stage);
        int state = (isCleared || isCurrent) ? ChestStateClosed : ChestStateLocked;
        if (isClaimable && stage == maxCleared) state = ChestStateOpen;
        var sp = LoadChestSprite(paths[state]);
        var chestImg = _slotChestImgs[slot];
        if (chestImg != null)
        {
            if (sp != null)
            {
                chestImg.sprite = sp;
                chestImg.color = Color.white;
            }
            else
            {
                // 不兜底：素材缺失要被看见（铁律 15）
                chestImg.color = new Color(1f, 1f, 1f, 0.15f);
                Debug.LogError($"[AdventureUI] 宝箱图缺失：{paths[state]}");
            }
            // 点击领奖：只有「可领」的宝箱开射线挂按钮；其余关掉（不挡翻章手势）
            SetChestClaimable(slot, isClaimable);
        }

        // 当前关整槽放大（基准 ×1.35），非当前关恢复基准
        var slotRt = _slotNodes[slot];
        if (!_slotBaseScales.TryGetValue(slotRt, out var baseScale))
        {
            baseScale = slotRt.localScale;
            _slotBaseScales[slotRt] = baseScale;
        }
        slotRt.localScale = isCurrent ? baseScale * SlotCurrentScale : baseScale;

        // 岛：岛1/2/3 按关号轮换
        var islandImg = _slotIslandImgs[slot];
        if (islandImg != null)
        {
            var islandSp = LoadChestSprite(IslandPaths[(stage - 1) % IslandPaths.Length]);
            if (islandSp != null) islandImg.sprite = islandSp;
        }

        // 编号（章-关，如 1-5 / 2-1）
        var lbl = _slotLabels[slot];
        if (lbl != null) lbl.text = $"{_selectedChapter}-{stage}";
    }

    /// <summary>按小关号取三态图路径（2026-10-24 从 WriteSlotContent 抽出：动效切图与刷新共用一个出口）。
    /// 第 3 关银、第 5 关（Boss）金，其余木（【2026-10-23 批 B1】沿用）。</summary>
    static string[] ChestPathsForStage(int stage)
    {
        int total = GameConfig.STAGES_PER_GRAND;
        return stage == ChestSilverStage ? ChestSilverPaths
             : stage == total ? ChestGoldPaths
             : ChestWoodPaths;
    }

    /// <summary>该章已通关的最大小关号（1 起；一关没通返回 0）。真源 clearedStages，只读不写。</summary>
    int GetMaxClearedStage(int chapter)
    {
        var cleared = SaveSystem.Instance?.Data?.clearedStages;
        if (cleared == null || cleared.Count == 0) return 0;
        int max = 0;
        string prefix = chapter + "_";
        foreach (var key in cleared)
        {
            if (string.IsNullOrEmpty(key) || !key.StartsWith(prefix, StringComparison.Ordinal)) continue;
            if (int.TryParse(key.Substring(prefix.Length), out int stage0)
                && stage0 >= 0 && stage0 < GameConfig.STAGES_PER_GRAND)
                max = Mathf.Max(max, stage0 + 1);
        }
        return max;
    }

    // ────────────────────────────────────────────────────
    // 槽位浮动 + 补位滑动（Update 集中驱动）
    // 浮动：整槽位围绕基准 y 做小幅正弦，仅 _currentFloatSlot（当前关）浮动；
    // 滑动：_slotSlideOffset（补位偏移）逐帧指数衰减，叠加在基准上实现「上面的槽位滑下来补位」。
    // ────────────────────────────────────────────────────
    void Update()
    {
        if (_guankaRoot == null || !_guankaRoot.gameObject.activeInHierarchy) return;
        float t = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < _slotNodes.Count && i < _slotFloatBasePos.Count; i++)
        {
            var rt = _slotNodes[i];
            if (rt == null) continue;
            // 补位滑动偏移衰减（指数缓出，≈0.35s 收敛）
            if (i < _slotSlideOffset.Count && _slotSlideOffset[i] != 0f)
            {
                var off = _slotSlideOffset[i];
                off -= off * Mathf.Min(1f, dt * 9f);
                if (Mathf.Abs(off) < 0.5f) off = 0f;
                _slotSlideOffset[i] = off;
            }
            float slide = i < _slotSlideOffset.Count ? _slotSlideOffset[i] : 0f;
            float target = _slotFloatBasePos[i].y + slide
                + (i == _currentFloatSlot ? Mathf.Sin(t * SlotFloatSpeed + i * 1.57f) * SlotFloatAmp : 0f);
            var p = rt.anchoredPosition;
            if (!Mathf.Approximately(p.y, target))
            {
                p.y = target;
                rt.anchoredPosition = p;
            }
        }
        // 当前关标识钳边跟随（槽位位置写入之后算，含浮动/补位滑动；随滚动实时贴/钳边）
        UpdateStageMarkerFollow();
    }

    // ────────────────────────────────────────────────────
    // 瘦屏槽位列拉伸（AdventureUI.Fit.FitMainColumnStretch 调用；主人 2026-10-23
    // 「右边列表手机拉长时要跟着拉长，别跟下面关卡信息有大空隙」）
    // 病根：槽位全 center 单点锚，瘦屏时整列随屏幕中心漂移，而 DetailPanel 沉底不动，
    //       列底与详情面板之间空隙随屏高越拉越大。
    // 治法：Fit 解析出「关卡容器帧」的目标 y 后写进 _slotFloatBasePos（浮动基准），
    //       Update 每帧从这里摆位 —— 不绕过浮动动画，浮动照常围绕新基准做正弦。
    // ────────────────────────────────────────────────────

    /// <summary>槽位列预制体基准 y（绑定时的 anchoredPosition，升序底→顶；拉伸插值节奏与还原都用它）。</summary>
    public IReadOnlyList<Vector2> SlotPrefabBasePos => _slotPrefabBasePos;

    /// <summary>
    /// 瘦屏重排：按 ysLocal（关卡容器帧 y，升序对应槽位）改写槽位浮动基准，x 不动。
    /// 标准屏 / 关掉瘦屏适配走 RestoreSlotColumnBaseY 还原预制体值。
    /// </summary>
    public void ApplySlotColumnStretchY(IReadOnlyList<float> ysLocal)
    {
        if (ysLocal == null) return;
        for (int i = 0; i < _slotFloatBasePos.Count && i < ysLocal.Count; i++)
        {
            var p = _slotFloatBasePos[i];
            p.y = ysLocal[i];
            _slotFloatBasePos[i] = p;
        }
    }

    /// <summary>还原槽位浮动基准到预制体原始值（标准屏 / 关掉瘦屏适配时调；下一帧 Update 生效）。</summary>
    public void RestoreSlotColumnBaseY()
    {
        for (int i = 0; i < _slotFloatBasePos.Count && i < _slotPrefabBasePos.Count; i++)
            _slotFloatBasePos[i] = _slotPrefabBasePos[i];
    }

    // ────────────────────────────────────────────────────
    // 「获得奖励」弹窗消费（2026-10-22 新增；2026-10-24 主人拍板改造：
    //   进页不再自动弹，改为玩家点击宝箱时按关取用 AdventureRewardNotice.Take；
    //   Boss 关（金箱）首通 +3 钻并入同一张弹窗一起发）
    // ────────────────────────────────────────────────────

    /// <summary>
    /// 按单关掉落构建「获得奖励」条目（口径与旧进页自动弹一致：金币 ≥2 才上格、
    /// 天赋石/附魔石/强化材料、装备逐件列）；bossDiamond=true 追加「钻石 ×3」一条。
    /// </summary>
    List<RewardPopupUI.Entry> BuildChestRewardEntries(AdventureRewardNotice.StageClear n, bool bossDiamond)
    {
        var entries = new List<RewardPopupUI.Entry>();
        if (n != null)
        {
            // 金币 ≥2 才上格（与结算同口径：1 个金币视为没获得）
            if (n.gold >= 2)
                entries.Add(new RewardPopupUI.Entry(CommonIconSprites.Load("icon_gold"), $"金币 ×{n.gold}"));
            if (n.talent > 0)
                entries.Add(new RewardPopupUI.Entry(CommonIconSprites.Load("icon_talent_stone"), $"天赋石 ×{n.talent}"));
            if (n.enchant > 0)
                entries.Add(new RewardPopupUI.Entry(CommonIconSprites.Load("icon_enchant_stone"), $"附魔石 ×{n.enchant}"));
            if (n.mats > 0)
                entries.Add(new RewardPopupUI.Entry(CommonIconSprites.Load("icon_decompose_mat"), $"强化材料 ×{n.mats}"));
            for (int i = 0; i < n.equips.Count; i++)
            {
                var g = n.equips[i];
                if (g == null) continue;
                entries.Add(new RewardPopupUI.Entry(
                    g.icon,
                    string.IsNullOrEmpty(g.name) ? "装备 ×1" : $"{g.name} ×1"));
            }
        }
        // 首通钻只有 Boss 关给（2026-10-24 主人拍板：每关首通 +3 钻口径废除，并入 Boss 金箱弹窗）
        if (bossDiamond)
            entries.Add(new RewardPopupUI.Entry(CommonIconSprites.Load("icon_diamond_blue"), $"钻石 ×{ChestClaimDiamond}"));
        return entries;
    }

    // ────────────────────────────────────────────────────
    // 宝箱领取 lifecycle（2026-10-23 主人拍板：通关未领=开启图可点；领取完变灰停一拍→下移消失、
    // 上面槽位补位滑动；轮到新当前关=从小到大开启）
    // ────────────────────────────────────────────────────

    /// <summary>进页清残留领取/揭示动画标志（页面 SetActive(false) 会杀协程，标志不清刷新会永远早退）。ShowPage 调。</summary>
    public void ClearStaleClaimAnim()
    {
        _claimRoutine = null;
        _openRevealRoutine = null;
        _revealPlayedThisVisit = false;
    }

    /// <summary>槽位节奏（相邻基准间距）：Fit 拉伸后取当前基准间距，否则取预制体间距。</summary>
    float SlotRhythm()
    {
        if (_slotFloatBasePos.Count >= 2) return Mathf.Abs(_slotFloatBasePos[1].y - _slotFloatBasePos[0].y);
        if (_slotPrefabBasePos.Count >= 2) return Mathf.Abs(_slotPrefabBasePos[1].y - _slotPrefabBasePos[0].y);
        return 146f;
    }

    /// <summary>
    /// 开/关某槽宝箱的点击领奖（射线+Button；只有可领的宝箱可点，其余关掉不挡翻章手势）。
    /// 槽位本就射线全关（防挡滑动翻章），这里只对宝箱子图操作。
    /// </summary>
    void SetChestClaimable(int slot, bool on)
    {
        if (slot < 0 || slot >= _slotChestImgs.Count) return;
        var chestImg = _slotChestImgs[slot];
        if (chestImg == null) return;
        chestImg.raycastTarget = on;
        var btn = chestImg.GetComponent<Button>();
        if (on)
        {
            if (btn == null)
            {
                btn = chestImg.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;   // 灰/亮全由刷新控制
            }
            btn.onClick.RemoveAllListeners();
            int captured = slot;   // 按值捕获槽位号
            btn.onClick.AddListener(() => OnClickChestClaim(captured));
        }
        else if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
        }
    }

    /// <summary>
    /// 点宝箱领奖（2026-10-24 主人拍板改造）：
    /// · 该关有待领掉落（AdventureRewardNotice.Take）→ 弹「获得奖励」弹窗（单关内容）；
    /// · Boss 关（金箱）首通 +3 钻并入同一张弹窗一起发；普通关不再发钻；
    /// · 发放后标记已领（防重复发奖）→ 变灰停一拍 → 下滑淡出消失（补位滑动）。
    /// </summary>
    void OnClickChestClaim(int slot)
    {
        if (_claimRoutine != null || _openRevealRoutine != null) return;
        if (slot < 0 || slot >= _slotStage.Count || _slotStage[slot] <= 0) return;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        int stage1 = _slotStage[slot];   // 1 起关号
        bool cleared = stage1 <= GetMaxClearedStage(_selectedChapter);
        if (!cleared || data.IsChestClaimed(_selectedChapter, stage1 - 1)) return;

        if (!data.MarkChestClaimed(_selectedChapter, stage1 - 1)) return;   // 防重复发奖
        // 先标记再取/发：保证只发一次；Take 返回 null = 该关没有记录（重复通关补领），不弹窗直接退场
        var notice = AdventureRewardNotice.Take(_selectedChapter, stage1 - 1);
        bool isBossChest = stage1 == GameConfig.STAGES_PER_GRAND;
        var entries = BuildChestRewardEntries(notice, bossDiamond: isBossChest);
        if (isBossChest)
        {
            ResourceWallet.Add(ResourceWallet.ResourceType.Diamond, ChestClaimDiamond, save: false, notify: true);
        }
        if (entries.Count > 0)
            RewardPopupUI.Show(entries);
        SaveSystem.Instance?.Save();

        _claimRoutine = StartCoroutine(ChestClaimExitAnim(slot));
    }

    /// <summary>
    /// 领取退场两段式：先变灰停一拍（看得清「已领取」）→ 自己下滑淡出 + 上面槽位重排补位滑动
    /// → 收尾全量刷新（新内容铺进空槽、边沿淡出/徽章/浮动槽位重算）。
    /// </summary>
    System.Collections.IEnumerator ChestClaimExitAnim(int slot)
    {
        var rt = _slotNodes[slot];
        var chestImg = slot < _slotChestImgs.Count ? _slotChestImgs[slot] : null;
        var cg = rt != null ? rt.GetComponent<CanvasGroup>() : null;
        float alpha0 = cg != null ? cg.alpha : 1f;

        // 段一：变灰停一拍（原地不动，点击关掉）
        SetChestClaimable(slot, false);
        if (chestImg != null) chestImg.color = ChestClearedTint;
        yield return new WaitForSecondsRealtime(BranchGrayHoldDur);

        // 段二：自己下滑淡出（改浮动基准走 Update，不硬写 anchoredPosition 不打架）；
        // 同时上面槽位重排（内容下移一档 + 滑动偏移）
        var data = SaveSystem.Instance?.Data;
        int total = GameConfig.STAGES_PER_GRAND;
        int maxCleared = GetMaxClearedStage(_selectedChapter);
        int current = Mathf.Clamp(maxCleared + 1, 1, total + 1);
        bool diffLocked = !ChapterRouteTable.CanEnterAtDiff(_selectedChapter, _selectedDiff, data);
        RebuildSlotContents(slot, data, total, maxCleared, current, diffLocked);
        float exitFrom = slot < _slotFloatBasePos.Count ? _slotFloatBasePos[slot].y : 0f;
        float t = 0f;
        while (t < BranchExitDur && rt != null)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / BranchExitDur);
            float e = k * k;   // 越走越快，像被收走
            if (slot < _slotFloatBasePos.Count)
            {
                var fb = _slotFloatBasePos[slot];
                fb.y = exitFrom - BranchExitSlide * e;
                _slotFloatBasePos[slot] = fb;
            }
            if (cg != null) cg.alpha = alpha0 * (1f - e);
            yield return null;
        }
        if (rt != null) rt.gameObject.SetActive(false);
        if (cg != null) cg.alpha = 1f;
        if (slot < _slotFloatBasePos.Count)   // 基准复位（Fit/还原不受退场残影影响）
        {
            var fb = _slotFloatBasePos[slot];
            fb.y = exitFrom;
            _slotFloatBasePos[slot] = fb;
        }

        _claimRoutine = null;
        RefreshChestNodes();
    }

    /// <summary>
    /// 刚通关那格宝箱的「关闭→开启」揭示（2026-10-24 主人拍板）：
    /// 关闭图 → 左右快速晃 + 慢慢缩小（约 2 秒）→ 猛地放大（过冲）峰值切开启图 → 回基准大小
    /// → 收尾才开射线可点领奖。只动「宝箱」子图 scale/rotation/sprite，不碰槽位根
    /// （槽位根由 Update 浮动系统每帧驱动，动根必打架）。玩家切章则协程立即还原退出。
    /// </summary>
    System.Collections.IEnumerator ChestOpenRevealAnim(int slot)
    {
        _revealChapter = _selectedChapter;
        var chestImg = slot >= 0 && slot < _slotChestImgs.Count ? _slotChestImgs[slot] : null;
        var rt = slot >= 0 && slot < _slotNodes.Count ? _slotNodes[slot] : null;
        if (chestImg == null || rt == null)
        {
            _openRevealRoutine = null;
            yield break;
        }
        var tr = chestImg.transform;
        int stage1 = slot < _slotStage.Count ? _slotStage[slot] : -1;
        string[] paths = stage1 > 0 ? ChestPathsForStage(stage1) : null;
        var closedSp = paths != null ? LoadChestSprite(paths[ChestStateClosed]) : null;
        var openSp = paths != null ? LoadChestSprite(paths[ChestStateOpen]) : null;
        if (closedSp == null || openSp == null)
        {
            // 素材缺失要被看见（不兜底，铁律 15）：LogError 后按开启图定场，可点交还玩家
            Debug.LogError($"[AdventureUI] 宝箱开启动效素材缺失：{paths?[ChestStateClosed]} / {paths?[ChestStateOpen]}");
            if (openSp != null) chestImg.sprite = openSp;
            SetChestClaimable(slot, true);
            _openRevealRoutine = null;
            yield break;
        }

        // 基准缓存（宝箱子图自身，动画结束/被打断全还原）
        Vector3 baseScale = tr.localScale;
        Quaternion baseRot = tr.localRotation;
        SetChestClaimable(slot, false);   // 动效全程不可点
        chestImg.sprite = closedSp;       // 从关闭图起跳

        // 段一：左右快速晃 + 慢慢缩小
        float t = 0f;
        while (t < RevealShakeDur && rt != null)
        {
            t += Time.unscaledDeltaTime;
            if (_selectedChapter != _revealChapter)
            {
                tr.localScale = baseScale; tr.localRotation = baseRot;
                _openRevealRoutine = null;
                yield break;
            }
            float k = Mathf.Clamp01(t / RevealShakeDur);
            tr.localRotation = baseRot * Quaternion.Euler(0f, 0f, Mathf.Sin(t * RevealShakeSpeed) * RevealShakeAmp);
            tr.localScale = baseScale * Mathf.Lerp(1f, RevealShrinkTo, k);
            yield return null;
        }

        // 段二：猛地放大（过冲再回落），峰值处切成开启图
        t = 0f;
        bool swapped = false;
        while (t < RevealPopDur && rt != null)
        {
            t += Time.unscaledDeltaTime;
            if (_selectedChapter != _revealChapter)
            {
                tr.localScale = baseScale; tr.localRotation = baseRot;
                _openRevealRoutine = null;
                yield break;
            }
            float k = Mathf.Clamp01(t / RevealPopDur);
            if (!swapped && k >= 0.5f)
            {
                swapped = true;
                chestImg.sprite = openSp;
            }
            tr.localScale = baseScale * Mathf.Lerp(RevealShrinkTo, RevealPopOver, EaseOutBack(k));
            tr.localRotation = baseRot * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 30f) * 2f);
            yield return null;
        }
        tr.localScale = baseScale;
        tr.localRotation = baseRot;
        chestImg.sprite = openSp;

        // 收尾：最新通关未领 = 开启图可点（与 WriteSlotContent 口径一致）
        SetChestClaimable(slot, true);
        _openRevealRoutine = null;
    }

    /// <summary>
    /// 轮到新当前关的开启动画：整槽从小到大（ease-out-back 微冲过头）+ 透明到出现。
    /// 【2026-10-23 主人「宝箱上面不用要那个感叹号 只有剧情关才有」】主线槽位不再挂「可打」徽章
    /// （支线岛剧情节点的「可接」徽章保留，见 BranchIslands）。
    /// </summary>
    System.Collections.IEnumerator ChestCurrentGrowAnim(int slot)
    {
        var rt = _slotNodes[slot];
        if (rt == null) yield break;
        var cg = rt.GetComponent<CanvasGroup>();
        if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();
        var baseScale = _slotBaseScales.TryGetValue(rt, out var bs) ? bs : rt.localScale;
        float alpha1 = cg.alpha;   // 刷新刚写过边沿淡出，以它为终点

        float t = 0f;
        while (t < BranchGrowDur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / BranchGrowDur);
            rt.localScale = baseScale * SlotCurrentScale * Mathf.Lerp(BranchGrowFromScale, 1f, EaseOutBack(k));
            cg.alpha = alpha1 * Mathf.Clamp01(k * 1.6f);   // 前 60% 走完透明→出现
            yield return null;
        }
        rt.localScale = baseScale * SlotCurrentScale;
        cg.alpha = alpha1;
    }

    /// <summary>ease-out-back：终点前轻微冲过头再回落，给「长出来」一点弹性。</summary>
    static float EaseOutBack(float k)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float x = k - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }
}
