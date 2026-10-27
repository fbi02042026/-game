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
// · 【2026-10-26 主人「1-5 后面就不显示了，后面应该接上 2-1 并能滑动」】关卡列改跨章连续：
//   从选中章起向上连续铺 ChestSpanChapters 个大关（1-5 上面接 2-1…2-5、3-1…），各章按各自的
//   通关/领取进度让位、未解锁章按难度整章灰锁；铺出的槽位向上堆（瘦屏 Fit 重排基准），
//   超出视口的部分成为滚动余量，窗口定位 ScrollChestToCurrent 真正生效（点头像回位同理）。
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
    const string ChestResDir = "UI/Art/Adventure/宝箱/";

    /// <summary>关卡滑动列表视口名（2026-10-24 主人拍板「关卡列改滑动列表」；运行时生成挂主线节点下，翻章手势让路要按名字找，别改）。</summary>
    const string ChestViewportName = "关卡Viewport";
    /// <summary>滑动列表视口横向裁切外扩（左右各加的逻辑像素）。
    /// 【2026-10-24 主人拍板「滑动列表左右两边裁切宽度改大点，以后还会有支线」】
    /// 「主线」容器只是 100×100 占位框，视口若只撑满它，RectMask2D 可见带仅 ~100 宽，
    /// 岛图/支线节点（示例支线 x≈105、岛半宽≈115 → 右缘 ~220）左右都被裁；
    /// 左右各外扩 200 → 可见带约 [-250,+250]（rel 列中心），支线完整可见，
    /// 且视口左缘（屏 x≈221）不越 LeftSidebar（右缘 182），透明拖拽面不挡侧栏按钮。</summary>
    const float ChestClipExtraWidthX = 200f;
    /// <summary>当前关标识节点名（2026-10-24 主人在 AdventureUI 根下摆好的「头像指示」，代码只挪位置不改样式）。</summary>
    const string StageMarkerNodeName = "头像指示";
    /// <summary>标识里箭头节点名（灰锁章只隐它、头像保留）。</summary>
    const string StageMarkerArrowNodeName = "箭头";
    /// <summary>标识里头像节点名（点击回位挂在它上面）。</summary>
    const string StageMarkerAvatarNodeName = "头像";
    /// <summary>标识与槽顶的间隙（箭头尖到当前关槽顶；嫌贴太紧只改这一个数）。</summary>
    const float StageMarkerGap = 6f;

    /// <summary>三种材质的 sprite 路径（注意：木箱锁图叫「未解锁」，银/金叫「未开启」，素材名如此）。
    /// 【2026-10-26】加第 4 态「领取后」：胜利奖励已在战斗结果界面直接发放，已通关的宝箱直接显示领取后图。</summary>
    static readonly string[] ChestWoodPaths   = { ChestResDir + "木箱开启",   ChestResDir + "木箱关闭",   ChestResDir + "木箱未解锁", ChestResDir + "木箱领取后" };
    static readonly string[] ChestSilverPaths = { ChestResDir + "银箱开启",   ChestResDir + "银箱关闭",   ChestResDir + "银箱未开启", ChestResDir + "银箱领取后" };
    static readonly string[] ChestGoldPaths   = { ChestResDir + "金箱开启",   ChestResDir + "金箱关闭",   ChestResDir + "金箱未开启", ChestResDir + "金箱领取后" };
    static readonly string[] IslandPaths      = { ChestResDir + "岛1",       ChestResDir + "岛2",       ChestResDir + "岛3" };

    /// <summary>状态下标：0=开启（已通关）1=关闭（当前进度）2=未解锁 3=领取后（已通关，奖励战斗里已发）。</summary>
    const int ChestStateOpen = 0;
    const int ChestStateClosed = 1;
    const int ChestStateLocked = 2;
    const int ChestStateClaimed = 3;

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

    /// <summary>关卡列跨章连续数（2026-10-26 主人「1-5 后面就不显示了，后面应该接上 2-1 并能滑动」）：
    /// 从选中章起向上连续铺几章；超过章上限（ChestRouteMaxChapter）自然截断。</summary>
    const int ChestSpanChapters = 3;
    /// <summary>章号上限（与 ChapterRouteTable 的 1~8 口径一致；跨章铺列钳住，不铺 8 之上的假章）。</summary>
    const int ChestRouteMaxChapter = 8;

    /// <summary>窗口槽位上限 = 跨章数 × 每大关小关数（2026-10-26 跨章连续列：15 槽铺满 3 章；
    /// 瘦屏 Fit 只把「可见 5 关」排进带，多出的槽位向视口下方溢出成滚动余量）。</summary>
    static int MaxSlotCount => Mathf.Max(4, GameConfig.STAGES_PER_GRAND * ChestSpanChapters);

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
    /// <summary>每个槽位当前显示的章号（1 起；-1=隐藏）——领奖/反查按「槽位自己的章」算，不再全列当 _selectedChapter（2026-10-26 跨章连续列）。</summary>
    readonly List<int> _slotChapter = new List<int>();
    /// <summary>补位滑动偏移（>0 = 节点在基准上方这么多像素，Update 逐帧衰减到 0）。</summary>
    readonly List<float> _slotSlideOffset = new List<float>();
    /// <summary>领取退场协程（非空 = 动画拥有画面，刷新早退；ShowPage 进页清一次防页面隐藏杀协程后卡死）。</summary>
    Coroutine _claimRoutine;
    /// <summary>各章上次刷新的当前关（当前关前进了才播开启动画；切章/回城不播）。</summary>
    readonly Dictionary<int, int> _lastCurrentByChapter = new Dictionary<int, int>();

    // 【2026-10-26 修「点宝箱无反应」】动效协程若被异常/隐藏等路径杀掉，句柄会残留非空 →
    // 刷新早退(636) + 点击早退(OnClickChestClaim) 双双被挡，本页内永不自愈。
    // 除进页 ClearStaleClaimAnim 外，再加看门狗：超过动效总时长仍未收尾就强制放行并刷新。
    /// <summary>揭示动效起跳时刻（unscaled；看门狗超时判定用，-1=未在播）。</summary>
    float _revealStartedAt = -1f;
    /// <summary>领取退场起跳时刻（unscaled；看门狗超时判定用，-1=未在播）。</summary>
    float _claimStartedAt = -1f;

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
        _slotStage.Clear(); _slotChapter.Clear(); _slotSlideOffset.Clear();

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
            _slotChapter.Add(-1);
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
    /// <summary>滑动内容 sizeDelta.y 的包裹基准值（标准屏/关适配还原用；瘦屏 Fit 按分辨率重写）。
    /// 【2026-10-24 视口按列占高撑出后包裹时恒为 0，保留字段作还原真源】。</summary>
    float _chestContentBaseDeltaY;

    /// <summary>
    /// 关卡容器 → 滑动列表（只包一次）：
    /// 视口纵向对准槽位列实际范围（列中心对列中心，见 2026-10-24 修「岛屿距离标题还有空隙」②）、
    /// 宽随「主线」撑满（滑动拖拽面）；
    /// 关卡容器变成滚动内容（纵向拉伸锚 (0,0)-(1,1)：高度 = 视口高 + sizeDelta.y，
    /// 内容中心 = 视口中心（两者同步上移 colCenter，槽位/示例子节点统一下移 colCenter 抵消）。
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
        var viewportGo = new GameObject(ChestViewportName, typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        var vp = (RectTransform)viewportGo.transform;
        vp.SetParent(parent, false);
        viewportGo.transform.SetSiblingIndex(0);   // 压到「主线」最底层：TitleBar/世界地图钮/支线岛的绘制与点击都在它之上
        // 视口：x 横向拉伸撑满「主线」再左右各外扩 ChestClipExtraWidthX（可见带覆盖岛图/支线，
        // 拖拽面同宽但不越侧栏），y 单点锚（0.5）按列占高撑出（瘦屏 Fit 再按分辨率带重拉）。
        // 【2026-10-24 修「岛屿距离标题还有空隙」②】视口纵向改为对准槽位列实际范围（列中心对列中心）：
        // 旧写法把视口中心钉在容器 pivot 上，而槽位列相对 pivot 偏上（顶槽 349 vs 半高 296.5），
        // 顶槽中心落在视口顶外 52.5 → 最上面的岛被 RectMask2D 裁掉、标题下先空一截。
        Vector2 frameCenter = srcRt.anchoredPosition;
        float spanH = Mathf.Max(srcRt.sizeDelta.y, SlotColumnSpan());
        float colMin = float.MaxValue, colMax = float.MinValue;
        var colSpanSrc = _slotFloatBasePos.Count > 0 ? _slotFloatBasePos : _slotPrefabBasePos;
        for (int i = 0; i < colSpanSrc.Count; i++)
        {
            colMin = Mathf.Min(colMin, colSpanSrc[i].y);
            colMax = Mathf.Max(colMax, colSpanSrc[i].y);
        }
        float colCenter = colSpanSrc.Count > 0 ? (colMin + colMax) * 0.5f : 0f;
        vp.anchorMin = new Vector2(0f, 0.5f);
        vp.anchorMax = new Vector2(1f, 0.5f);
        vp.offsetMin = new Vector2(frameCenter.x - ChestClipExtraWidthX, frameCenter.y + colCenter - spanH * 0.5f);
        vp.offsetMax = new Vector2(frameCenter.x + ChestClipExtraWidthX, frameCenter.y + colCenter + spanH * 0.5f);
        vp.pivot = srcRt.pivot;
        vp.localScale = Vector3.one;
        // 透明接收面：槽位图射线全关（防挡翻章），竖滑拖拽得落在这一层 ScrollRect 才收得到
        var vpImg = viewportGo.GetComponent<Image>();
        vpImg.color = new Color(0f, 0f, 0f, 0f);
        vpImg.raycastTarget = true;

        // 容器变内容：纵向拉伸锚定（顶边与视口顶边重合 = 原位置），向下按内容高度撑出滚动余量
        srcRt.SetParent(vp, false);
        srcRt.anchorMin = new Vector2(0f, 0f);
        srcRt.anchorMax = new Vector2(1f, 1f);
        srcRt.pivot = new Vector2(0.5f, 1f);
        srcRt.anchoredPosition = Vector2.zero;
        float viewH = Mathf.Max(1f, spanH);   // 视口刚按 spanH 撑出，直接用计算值（不依赖 rect 时序）
        float span = SlotColumnSpan();
        float contentH = Mathf.Max(viewH, span);
        srcRt.sizeDelta = new Vector2(0f, contentH - viewH);   // 拉伸锚下 sizeDelta.y = 高度 - 视口高
        _chestContentBaseDeltaY = contentH - viewH;

        // 内容矩形中心比容器 pivot 高 colCenter（视口已按列中心对齐）→ 容器内全部子节点
        //（槽位 + 主人手摆的「支线」示例等）统一下移 colCenter，根帧位置与预制体分毫不差；
        // 再补回向下长高的 d，滚动到顶时仍钉在原屏幕位置（视觉零变化）
        float d = (contentH - viewH) * 0.5f;
        _chestRefShift = d;
        for (int i = 0; i < srcRt.childCount; i++)
        {
            var child = srcRt.GetChild(i) as RectTransform;
            if (child == null) continue;
            if (Mathf.Abs(child.anchorMin.y - child.anchorMax.y) > 1e-4f) continue;   // 非单点锚不动，防错排
            var p = child.anchoredPosition;
            child.anchoredPosition = new Vector2(p.x, p.y - colCenter);
        }
        for (int i = 0; i < _slotFloatBasePos.Count; i++)
            _slotFloatBasePos[i] = new Vector2(_slotFloatBasePos[i].x, _slotFloatBasePos[i].y + d - colCenter);
        for (int i = 0; i < _slotPrefabBasePos.Count; i++)
            _slotPrefabBasePos[i] = new Vector2(_slotPrefabBasePos[i].x, _slotPrefabBasePos[i].y + d - colCenter);

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

    /// <summary>把当前关滚进视口（刷新后调；内容不比视口高时不滚）。
    /// 【2026-10-24 修「岛屿距离标题还有空隙」①】弃「当前关滚到视口正中」——
    /// 顶格（滚到顶）看得见当前关就回顶部，岛屿列钉在标题下方不留空隙；
    /// 确实装不下才滚「刚好露出当前关」的最小量，不再把顶部关卡整格滚出视口。</summary>
    void ScrollChestToCurrent(int slotIdx)
    {
        if (_chestScroll == null || slotIdx < 0 || slotIdx >= _slotNodes.Count) return;
        var content = _chestScroll.content;
        var viewport = _chestScroll.viewport;
        if (content == null || viewport == null) return;
        float viewH = viewport.rect.height;
        float contentH = content.rect.height;
        if (contentH <= viewH + 1f) return;
        // 槽位锚点基准在内容中心（单点锚）：槽中心距内容顶
        float slotBelowTop = contentH * 0.5f - _slotFloatBasePos[slotIdx].y;
        const float halfSlot = 90f;   // 单格视觉半径（SlotColumnSpan 同口径）
        // 顶格时槽位底缘超出视口底的量；≤0 = 顶格就完整可见 → 回顶部
        float overflowAtTop = slotBelowTop + halfSlot - viewH;
        if (overflowAtTop <= 0f)
        {
            _chestScroll.verticalNormalizedPosition = 1f;
            return;
        }
        float t = Mathf.Clamp01(overflowAtTop / Mathf.Max(1f, contentH - viewH));
        _chestScroll.verticalNormalizedPosition = 1f - t;
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

    // 【2026-10-26 修「进度头像不显示」】prefab 里头像/箭头两个 Image 的 sprite guid 已断
    //（整库 meta 无 c497a5ea… / cca90b8e… 两串），图真身在 Resources/Art/UI/头像指示/ 下
    //（且是 56 字符 guid）。按 2026-10-23 拍板走路径加载（同 LoadChestSprite 模式），代码不依赖 prefab guid；
    // prefab 里 sprite 还活着（主人重拖过）则以 prefab 为准，只在空时补路径图。
    const string MarkerAvatarSpritePath = "UI/Art/头像指示/所在头像";
    const string MarkerArrowSpritePath = "UI/Art/头像指示/箭头";

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
        // 【2026-10-26 修「头像钉在 1-1 位置」】SetParent(false) 保留主人预制体里的锚点，
        // 但贴位数学（UpdateStageMarkerFollow）按「视口中心锚点 (0.5,0.5)」写 anchoredPosition；
        // 锚点不是中心时写入值整体偏半屏（视口 ~730px 高 → 钉死在视口底边 ≈ 1-1 位置且不随槽位动）。
        // 节点位置本就每帧由贴位全量接管，这里只统一锚点坐标系，不动尺寸/透明度/子节点。
        if (_stageMarker.anchorMin != new Vector2(0.5f, 0.5f) || _stageMarker.anchorMax != new Vector2(0.5f, 0.5f))
        {
            Debug.LogWarning($"[AdventureUI] 「{StageMarkerNodeName}」预制体锚点({_stageMarker.anchorMin.x:0.##},{_stageMarker.anchorMin.y:0.##})非中心，已统一为(0.5,0.5)（贴位坐标系需要，不改视觉）");
            _stageMarker.anchorMin = new Vector2(0.5f, 0.5f);
            _stageMarker.anchorMax = new Vector2(0.5f, 0.5f);
        }
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
        // 【2026-10-26 修「进度头像不显示」】头像/箭头 sprite 断引用补路径加载
        //（只在空时补：prefab 里主人重拖过活引用则以 prefab 为准）
        var avatarImg = avatar != null ? avatar.GetComponent<Image>() : null;
        if (avatarImg != null && avatarImg.sprite == null)
            avatarImg.sprite = LoadMarkerSprite(MarkerAvatarSpritePath, "头像");
        if (_stageMarkerArrow != null)
        {
            var arrowImg = _stageMarkerArrow.GetComponent<Image>();
            if (arrowImg != null && arrowImg.sprite == null)
                arrowImg.sprite = LoadMarkerSprite(MarkerArrowSpritePath, "箭头");
        }
    }

    /// <summary>标记图路径加载（复用 LoadChestSprite 缓存）；失败 Warning 不静默。</summary>
    Sprite LoadMarkerSprite(string path, string label)
    {
        var sp = LoadChestSprite(path);
        if (sp == null)
            Debug.LogWarning($"[AdventureUI] 路径加载「{label}」图失败：Resources/{path}（查文件是否存在且导入类型为 Sprite）");
        return sp;
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
        // 绑定时视口可能尚未建好（EnsureStageMarkerNode 只绑一次），每次刷新补挂一次
        if (_chestScroll != null && _chestScroll.viewport != null && _stageMarker.parent != _chestScroll.viewport)
            _stageMarker.SetParent(_chestScroll.viewport, false);
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
    /// 槽位增补（2026-10-26 跨章连续列：刷新即拉满 MaxSlotCount=15，不再依赖瘦屏窗口装得下）：
    /// 克隆顶部槽位（岛/宝箱/编号牌结构同源）追加到窗口，返回当前槽位数。want 钳到 [4, MaxSlotCount]。
    /// 克隆槽位基准 = 上一槽基准 + 预制体节奏向上堆（不与源槽重叠）；瘦屏 Fit 会整体重写基准；
    /// 标准屏/还原路径靠这份往上排 + RestoreSlotColumnBaseY 复位。
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
            _slotChapter.Add(-1);
            _slotSlideOffset.Add(0f);
            // 克隆源是已处理槽位：子图射线已关、CanvasGroup 已带（透明度每次刷新重算，无需再调）
            // 克隆槽位的浮动基准 = 上一槽基准 + 预制体节奏（向上堆，不再与源槽重叠）
            int prevIdx = _slotNodes.Count - 2;
            var prevBase = (prevIdx >= 0 && prevIdx < _slotFloatBasePos.Count)
                ? _slotFloatBasePos[prevIdx]
                : (Vector2)src.anchoredPosition;
            var cloneFloatBase = new Vector2(prevBase.x, prevBase.y + PrefabSlotRhythm());
            _slotFloatBasePos.Add(cloneFloatBase);
            // 基准缩放继承源槽位的「基准」而非克隆体现值 —— 源若正处于「当前关 ×1.35」放大态，
            // 克隆会把放大值当基准，之后永远偏大（RefreshChestNodes 按这里记录的基准做倍乘）。
            var srcBaseScale = _slotBaseScales.TryGetValue(src, out var sb) ? sb : src.localScale;
            clone.localScale = srcBaseScale;
            _slotBaseScales[clone] = srcBaseScale;
            // 立即摆到浮动基准位（不等下一帧 Update）
            clone.anchoredPosition = cloneFloatBase;
        }
        ApplySlotZOrder();   // 增补后重排渲染层级（底槽画在最上）
        return _slotNodes.Count;
    }

    /// <summary>预制体槽位节奏（相邻两槽基准 y 间距）；列表异常退化时用 SlotRhythm 的兜底值。</summary>
    float PrefabSlotRhythm()
    {
        if (_slotPrefabBasePos.Count >= 2)
            return Mathf.Abs(_slotPrefabBasePos[1].y - _slotPrefabBasePos[0].y);
        return 146f;
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
            if (last < _slotChapter.Count) _slotChapter.RemoveAt(last);
            if (last < _slotSlideOffset.Count) _slotSlideOffset.RemoveAt(last);
            if (rt != null) _slotBaseScales.Remove(rt);
            if (rt != null) Destroy(rt.gameObject);
        }
        if (_currentFloatSlot >= _slotNodes.Count) _currentFloatSlot = -1;
        if (_markerCurrentSlot >= _slotNodes.Count) _markerCurrentSlot = -1;
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
        // 视口随内容一起显隐【2026-10-24 视口扩到大显示区后，别的模式下不能再留一块吃点击的透明面】
        if (_chestScroll != null && _chestScroll.viewport != null
            && _chestScroll.viewport.gameObject.activeSelf != show)
            _chestScroll.viewport.gameObject.SetActive(show);
        if (!show) return;
        // 领取退场/开启揭示动画拥有画面时早退（揭示动画跨章切换时放行刷新，协程自己还原退出）
        if (_claimRoutine != null || (_openRevealRoutine != null && _revealChapter == _selectedChapter))
        {
            // 【2026-10-26 修「点宝箱无反应」】留一行原因日志：若这里反复出现而动效早已结束，即句柄残留被看门狗兜底
            Debug.Log($"[AdventureUI][宝箱] 刷新跳过：动效占用中（领取退场={_claimRoutine != null}，开启揭示={_openRevealRoutine != null}）");
            return;
        }

        var data = SaveSystem.Instance?.Data;
        int total = GameConfig.STAGES_PER_GRAND;
        int maxCleared = GetMaxClearedStage(_selectedChapter);
        int current = Mathf.Clamp(maxCleared + 1, 1, total + 1);   // 下一关（1 起）；整章通关 = total+1
        _currentFloatSlot = -1;   // 浮动槽位每次刷新重算（只有当前关浮动）

        // 【2026-10-24 主人拍板「难度逐章解锁」】该章在当前难度未开启（上一章该难度没通）
        // → 关卡列全部按「未解锁」灰锁 + 整列压暗，点开始冒险在 Interaction 端统一提示。
        bool diffLocked = !ChapterRouteTable.CanEnterAtDiff(_selectedChapter, _selectedDiff, data);

        // 【2026-10-26 跨章连续列】槽位先拉满（标准屏也增补；瘦屏 Fit 再按带重排基准）
        EnsureSlotCount(MaxSlotCount);
        RebuildSlotContents(-1, data);
        // 槽位铺满 3 章后内容远高于视口：就地补滚动余量（标准屏没有 Fit 重排，全靠这里）
        RefreshChestContentSize();

        // 整列灰锁压暗（锁定时交互全关：宝箱按钮跟着 CanvasGroup.interactable 失效）
        var rootCg = _guankaRoot.GetComponent<CanvasGroup>();
        if (rootCg == null) rootCg = _guankaRoot.gameObject.AddComponent<CanvasGroup>();
        rootCg.alpha = diffLocked ? 0.55f : 1f;
        rootCg.interactable = !diffLocked;

        // 当前关标识（主人预制体「头像指示」）与滑动列表定位：
        // 【2026-10-26 跨章连续列】当前关可能是下一章的「2-1」（本章通关让位后列顶让给后章），
        // 标识优先指进行中的当前关槽（_currentFloatSlot）；指不到（本章通关/灰锁章）时
        // 回退到「本章应打的关」的槽（通关=maxCleared 槽、灰锁=clamp 进度槽）。
        // 灰锁章标识仍显示（只有头像没有箭头）；内容比视口矮时不滚。
        int markerSlot = _currentFloatSlot;
        if (markerSlot < 0)
            markerSlot = FindSlotIndexOfChapterStage(_selectedChapter, Mathf.Clamp(maxCleared + 1, 1, total));
        Debug.Log($"[AdventureUI][标识] 章{_selectedChapter} 当前关{current} → 槽{markerSlot}（槽位[{string.Join(",", _slotChapter)}|{string.Join(",", _slotStage)}]）");
        UpdateStageMarker(markerSlot, diffLocked);
        ScrollChestToCurrent(markerSlot);

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

        // 「轮到剧情关卡」开启动画：该章当前关前进了才播（切章/回城不播；灰锁章不播）。
        // 【2026-10-26 跨章连续列】按（章，关）找槽——列表里有后章同号关（1-3 与 2-3），
        // 只比关号会播错槽位。
        if (!diffLocked && _lastCurrentByChapter.TryGetValue(_selectedChapter, out var lastCur)
            && current > lastCur && current <= total)
        {
            int growSlot = FindSlotIndexOfChapterStage(_selectedChapter, current);
            if (growSlot >= 0) StartCoroutine(ChestCurrentGrowAnim(growSlot));
        }
        _lastCurrentByChapter[_selectedChapter] = current;

        // 【2026-10-26 主人拍板】进页「关闭→开启」揭示动效已随点宝箱领取一并废除：
        // 胜利奖励战斗里直接发，已通关的宝箱进页就是领取后图（WriteSlotContent 四态）。

        // 岛屿连接点（可见槽位两两相连；2026-10-23 主人「岛屿之间加上点连接起来」）
        RebuildSlotConnectors();
    }

    /// <summary>
    /// 重排槽位内容（【2026-10-26 跨章连续列】关卡依次入槽，本章铺完接下一章，直至铺满槽位上限；
    /// 各章按各自的通关/领取进度让位、按难度锁章；skipSlot=退场中的槽位不写内容）。
    /// 内容下移一档的槽位给 +rhythm 滑动偏移，由 Update 逐帧衰减实现补位滑动。
    /// 进度真源 = 存档 clearedStages（键 "章_关卡"，0 起关号）；领取真源 = chestClaimedStages。
    /// </summary>
    void RebuildSlotContents(int skipSlot, SaveData data)
    {
        var oldStage = new int[_slotNodes.Count];
        var oldChapter = new int[_slotNodes.Count];
        for (int i = 0; i < _slotNodes.Count; i++)
        {
            oldStage[i] = i < _slotStage.Count ? _slotStage[i] : -1;
            oldChapter[i] = i < _slotChapter.Count ? _slotChapter[i] : -1;
        }

        float rhythm = SlotRhythm();
        int total = GameConfig.STAGES_PER_GRAND;
        // 跨章铺列的最后一章（选中章起连续 ChestSpanChapters 章，钳到章上限）
        int lastChapter = Mathf.Min(_selectedChapter + ChestSpanChapters - 1, ChestRouteMaxChapter);
        int curCh = _selectedChapter;
        int curStage = 0;
        int currentSlot = -1;
        for (int slot = 0; slot < _slotNodes.Count; slot++)
        {
            // 找下一个入槽的关（本章铺完接下一章；章超出范围 → 槽位隐藏）
            int sCh = -1, sStage = -1, sMax = 0, sCur = 0;
            bool sLocked = true;
            while (curCh <= lastChapter)
            {
                curStage++;
                if (curStage > total) { curCh++; curStage = 0; continue; }
                int chMax = GetMaxClearedStage(curCh);
                int chCur = Mathf.Clamp(chMax + 1, 1, total + 1);
                bool chLocked = !ChapterRouteTable.CanEnterAtDiff(curCh, _selectedDiff, data);
                bool cl = curStage <= chMax;
                bool claimed = cl && data != null && data.IsChestClaimed(curCh, curStage - 1);
                // 通关的关除「刚打完的关」（显示领取后图）外一律让位；灰锁章不让位（整章按未解锁显示全列）
                bool skip = claimed || (!chLocked && cl && curStage != chMax);
                if (skip) continue;
                sCh = curCh; sStage = curStage; sMax = chMax; sCur = chCur; sLocked = chLocked;
                break;
            }
            if (slot < _slotStage.Count) _slotStage[slot] = sStage;
            if (slot < _slotChapter.Count) _slotChapter[slot] = sCh;
            if (slot == skipSlot) continue;   // 退场中的槽位不动（收尾刷新再写新内容）

            if (sStage < 0)
            {
                var go0 = _slotNodes[slot].gameObject;
                if (go0.activeSelf) go0.SetActive(false);
                if (slot < _slotSlideOffset.Count) _slotSlideOffset[slot] = 0f;
                continue;
            }
            if (!_slotNodes[slot].gameObject.activeSelf)
                _slotNodes[slot].gameObject.SetActive(true);
            WriteSlotContent(slot, sCh, sStage, sMax, sCur, sLocked);
            // 「进行中的当前关」（下一关）：列里最早的命中槽 = 进度前沿（多章内只会有一个）
            if (!sLocked && sStage == sCur && sStage <= total && currentSlot < 0)
                currentSlot = slot;
            // 内容下移一档（新关来自更上面的旧槽，跨章按章号比）→ +rhythm 偏移；内容上移/新出现直接就位
            if (slot < _slotSlideOffset.Count)
            {
                bool movedDown = oldStage[slot] >= 0 && oldChapter[slot] >= 0
                    && (sCh > oldChapter[slot] || (sCh == oldChapter[slot] && sStage > oldStage[slot]));
                _slotSlideOffset[slot] = movedDown ? rhythm : 0f;
            }
        }
        _currentFloatSlot = currentSlot;
    }

    /// <summary>写单个槽位的显示内容（四态图/缩放/岛/编号；边沿淡出与滑动偏移不在此）。
    /// 【2026-10-26 跨章连续列】章/关/进度全部按槽位自己的章算（chMax/chCur/chLocked 由游标逐章取）。
    /// 【2026-10-24 主人拍板「难度逐章解锁」】chLocked=true 时一律按「未解锁」图显示。</summary>
    void WriteSlotContent(int slot, int chapter, int stage, int chMaxCleared, int chCurrent, bool chLocked)
    {
        bool isCleared = !chLocked && stage <= chMaxCleared;
        bool isCurrent = !chLocked && stage == chCurrent;

        // 四态换图（【2026-10-26 主人拍板】覆盖 10-24「开启图可点领取」口径）：
        // 胜利奖励已在战斗结果界面直接发放，冒险页不再走「点宝箱领取」——
        // · 已通关 → 领取后图（不可点，主人已补三材质「领取后」素材）；
        // · 当前关（未打）→ 关闭图不可点；没打到 → 未解锁图。
        string[] paths = ChestPathsForStage(stage);
        int state = isCleared ? ChestStateClaimed : (isCurrent ? ChestStateClosed : ChestStateLocked);
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
            // 点击领奖已废除（奖励战斗里直接发）：射线一律关掉（不挡翻章手势）
            SetChestClaimable(slot, false);
        }

        // 当前关整槽放大（基准 ×1.35），非当前关恢复基准
        var slotRt = _slotNodes[slot];
        if (!_slotBaseScales.TryGetValue(slotRt, out var baseScale))
        {
            baseScale = slotRt.localScale;
            _slotBaseScales[slotRt] = baseScale;
        }
        slotRt.localScale = isCurrent ? baseScale * SlotCurrentScale : baseScale;

        // 岛：岛1/2/3 按「章」轮换（2026-10-26 主人拍板：1 章全用岛1、2 章全用岛2、3 章全用岛3，
        // 4 章起回到岛1 循环；不再按关号轮换）
        var islandImg = _slotIslandImgs[slot];
        if (islandImg != null)
        {
            var islandSp = LoadChestSprite(IslandPaths[(chapter - 1) % IslandPaths.Length]);
            if (islandSp != null) islandImg.sprite = islandSp;
        }

        // 编号（章-关，如 1-5 / 2-1）
        var lbl = _slotLabels[slot];
        if (lbl != null) lbl.text = $"{chapter}-{stage}";
    }

    /// <summary>按（章,关）找槽位下标；找不到返回 -1。</summary>
    int FindSlotIndexOfChapterStage(int chapter, int stage)
    {
        for (int i = 0; i < _slotStage.Count && i < _slotChapter.Count; i++)
            if (_slotChapter[i] == chapter && _slotStage[i] == stage) return i;
        return -1;
    }

    /// <summary>
    /// 滑动内容高度就地补齐（2026-10-26 跨章连续列）：槽位铺满 3 章后列高远超视口，
    /// 内容不拉高 ScrollRect 没有滚动余量（1-5 之上滚不到 2-1）。只在内容需要变高时补；
    /// 内容向下长高 → 内容中心下移一半增量，槽位是内容子节点（Update 按基准摆位），浮动基准同步补偿画面不动。
    /// 瘦屏 Fit 重排后 span 与已设内容高一致 → 本方法空跑不干扰；标准屏没有 Fit，全靠这里撑出。
    /// </summary>
    void RefreshChestContentSize()
    {
        if (_chestScroll == null) return;
        var content = _chestScroll.content;
        var viewport = _chestScroll.viewport;
        if (content == null || viewport == null) return;
        float viewH = viewport.rect.height;
        if (viewH <= 1f) return;
        float contentH = Mathf.Max(viewH, SlotColumnSpan());
        float oldH = content.rect.height;
        if (contentH <= oldH + 0.5f) return;
        content.sizeDelta = new Vector2(content.sizeDelta.x, contentH - viewH);
        float half = (contentH - oldH) * 0.5f;
        for (int i = 0; i < _slotFloatBasePos.Count; i++)
            _slotFloatBasePos[i] = new Vector2(_slotFloatBasePos[i].x, _slotFloatBasePos[i].y + half);
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
        WatchdogChestAnims();   // 【2026-10-26】动效句柄超时自愈，防「点宝箱无反应」死锁
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

    /// <summary>
    /// 【2026-10-26 修「点宝箱无反应」】动效看门狗：揭示/领取退场协程若被异常、页面隐藏等路径
    /// 半途杀死，句柄残留非空会同时挡死刷新(636)与点击(OnClickChestClaim)且本页内永不自愈。
    /// 这里按「动效总时长 + 余量」判定超时，强制清标志并全量刷新（可点射线由 WriteSlotContent 重写）。
    /// </summary>
    void WatchdogChestAnims()
    {
        float now = Time.unscaledTime;
        if (_openRevealRoutine != null
            && now - _revealStartedAt > RevealShakeDur + RevealPopDur + 0.5f)
        {
            Debug.LogWarning("[AdventureUI][宝箱] 开启揭示超时未收尾（协程被杀？），看门狗强制放行并刷新");
            _openRevealRoutine = null;
            _revealStartedAt = -1f;
            RefreshChestNodes();
            return;
        }
        if (_claimRoutine != null
            && now - _claimStartedAt > BranchGrayHoldDur + BranchExitDur + 1.5f)
        {
            Debug.LogWarning("[AdventureUI][宝箱] 领取退场超时未收尾（协程被杀？），看门狗强制刷新");
            _claimRoutine = null;
            _claimStartedAt = -1f;
            RefreshChestNodes();
        }
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

    /// <summary>还原槽位浮动基准（标准屏 / 关掉瘦屏适配时调；下一帧 Update 生效）。
    /// 【2026-10-26 跨章连续列】预制体底账只有 4 槽（克隆不再追加底账），多出的克隆槽
    /// 按预制体节奏向上堆恢复，否则克隆槽基准残留在 Fit 帧上、与还原后的 4 槽错位重叠。</summary>
    public void RestoreSlotColumnBaseY()
    {
        int prefabCount = _slotPrefabBasePos.Count;
        if (prefabCount == 0) return;
        var top = _slotPrefabBasePos[prefabCount - 1];
        float rhythm = PrefabSlotRhythm();
        for (int i = 0; i < _slotFloatBasePos.Count; i++)
            _slotFloatBasePos[i] = i < prefabCount
                ? _slotPrefabBasePos[i]
                : new Vector2(top.x, top.y + (i - prefabCount + 1) * rhythm);
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
        _claimStartedAt = -1f;    // 【2026-10-26】看门狗时间戳一并复位
        _revealStartedAt = -1f;
    }

    /// <summary>槽位节奏（相邻基准间距）：Fit 拉伸后取当前基准间距，否则取预制体间距。</summary>
    float SlotRhythm()
    {
        if (_slotFloatBasePos.Count >= 2) return Mathf.Abs(_slotFloatBasePos[1].y - _slotFloatBasePos[0].y);
        if (_slotPrefabBasePos.Count >= 2) return Mathf.Abs(_slotPrefabBasePos[1].y - _slotPrefabBasePos[0].y);
        return 146f;
    }

    /// <summary>
    /// 开/关某槽宝箱的点击领奖（Button；on 只管挂/摘点击，射线恒开）。
    /// 【2026-10-26 修「列表手动滑不了」】点领奖废除后宝箱图是列表里唯一的射线面，
    /// 射线一关 ScrollRect 就收不到拖动事件 → 列表滑不动。故射线常开：
    /// 拖动事件向上冒泡给 ScrollRect 正常滚列表；点击无 Button 监听无效果，也不挡翻章（翻章手势不在本列表内）。
    /// </summary>
    void SetChestClaimable(int slot, bool on)
    {
        if (slot < 0 || slot >= _slotChestImgs.Count) return;
        var chestImg = _slotChestImgs[slot];
        if (chestImg == null) return;
        chestImg.raycastTarget = true;   // 恒开：列表拖动滚动的唯一射线面（见上注释）
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
        // 【2026-10-26 修「点宝箱无反应」】每个早退分支都落一行原因日志，报问题时一眼定位卡在哪
        if (_claimRoutine != null || _openRevealRoutine != null)
        {
            Debug.Log($"[AdventureUI][宝箱] 点击忽略：动效占用中（领取退场={_claimRoutine != null}，开启揭示={_openRevealRoutine != null}，槽{slot}）");
            return;
        }
        if (slot < 0 || slot >= _slotStage.Count || _slotStage[slot] <= 0)
        {
            Debug.LogWarning($"[AdventureUI][宝箱] 点击忽略：槽位上没有关卡（槽{slot}，stage={(slot >= 0 && slot < _slotStage.Count ? _slotStage[slot] : -1)}）");
            return;
        }
        var data = SaveSystem.Instance?.Data;
        if (data == null)
        {
            Debug.LogWarning("[AdventureUI][宝箱] 点击忽略：存档未就绪");
            return;
        }
        // 【2026-10-26 跨章连续列】按「槽位自己的章」取关，不再全列当 _selectedChapter
        int slotCh = slot < _slotChapter.Count ? _slotChapter[slot] : -1;
        if (slotCh <= 0)
        {
            Debug.LogWarning($"[AdventureUI][宝箱] 点击忽略：槽位章号异常（槽{slot}，chapter={slotCh}）");
            return;
        }
        int stage1 = _slotStage[slot];   // 1 起关号
        int maxCleared = GetMaxClearedStage(slotCh);
        bool cleared = stage1 <= maxCleared;
        if (!cleared || data.IsChestClaimed(slotCh, stage1 - 1))
        {
            Debug.LogWarning($"[AdventureUI][宝箱] 点击忽略：章{slotCh} 关{stage1} 未通关或已领（maxCleared={maxCleared}，已领={data.IsChestClaimed(slotCh, stage1 - 1)}）");
            return;
        }
        Debug.Log($"[AdventureUI][宝箱] 领取：章{slotCh} 关{stage1}（槽{slot}）");

        if (!data.MarkChestClaimed(slotCh, stage1 - 1)) return;   // 防重复发奖
        // 先标记再取/发：保证只发一次；Take 返回 null = 该关没有记录（重复通关补领），不弹窗直接退场
        var notice = AdventureRewardNotice.Take(slotCh, stage1 - 1);
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
        _claimStartedAt = Time.unscaledTime;
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
        RebuildSlotContents(slot, data);
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
        _claimStartedAt = -1f;
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
        _revealStartedAt = -1f;
        Debug.Log($"[AdventureUI][宝箱] 开启揭示完成：槽{slot} 已可点领取");
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
