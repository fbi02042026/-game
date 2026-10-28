using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

#if UNITY_EDITOR
using UnityEditor;
#endif

// 冒险界面 partial —— 2026-10-23 新增：主线窗口旁的支线岛屿宝箱
//
// 【2026-10-23 合并方案（主人拍板「岛壳用那边 + 内容用我的」）】
// · 岛壳（本文件，原「2 岛依次解锁」UI 保留）：主线窗口旁 2 个岛槽，依次解锁；
//   不打则岛一直在（固定位置，不随主线窗口滑动定位而消失）
// · 岛内容（2026-10-23 合并方案换装）：岛不再是「切到支线章打整章」——派定值 branchRoll1/2
//   现在记**支线岛 id（1..8，SideIslandDefs）**；点岛 → 确认弹窗（10 体力 + 奖励预览）→
//   进**单 Boss 特殊关**（SideIslandBattleMode），产出 = 固定局外金币 + 天赋石 + 装备掉落，
//   一次性打完锁（跨周目持久，SaveData.clearedSideIslandIds）
// · 派定真源：ChapterRouteTable.EnsureForkRoll（洗牌袋，通关第 1 章派岛 1、通关该岛派岛 2；
//   8 岛全通后洗牌袋重置开新一轮）
// · 旧支线章 2/3/4 并入主线按序解锁（主线 1→8 线性，ChapterRouteTable 合并方案）
//
// 【实现】运行时克隆主人手摆的「支线」示例节点（关卡/支线：岛+剧情+可接徽章+名牌结构）生成两个岛，
//   挂 RightContent/主线/支线岛屿 容器下 —— 不在「关卡」容器内，
//   因此不参与窗口滑动定位，EnsureChestNodes 的槽位扫描也不会误认。
//   摆位以示例节点为准（岛1=示例原位，岛2=示例位上方一个槽位节奏），间距不随屏幕拉伸。
//   剧情图/可接徽章运行时按路径加载（2026-10-23 主人拍板「56字符一律不管，全部路径加载」）；
//   岛图运行时轮换 岛1/岛2（专属岛图等 P0 素材）。
public partial class AdventureUI
{
    /// <summary>支线岛槽数量（岛壳沿用 2 个：依次解锁）。</summary>
    const int BranchIslandCount = 2;
    /// <summary>支线岛容器名（挂在 RightContent/主线 下，别动名字）。</summary>
    const string BranchIslandContainerName = "支线岛屿";
    /// <summary>主人手摆的支线示例节点名（关卡/支线，克隆源 + 摆位真源；运行时隐藏原件只留克隆）。</summary>
    const string BranchIslandSampleNodeName = "支线";
    /// <summary>两个支线岛之间的纵向间距（照主线槽位节奏 146；嫌大嫌小只改这一个数）。</summary>
    const float BranchIslandSpacing = 146f;

    // ── 主线宝箱 lifecycle 动画节奏常量已搬去 ChestNodes.cs（2026-10-23 常量跟使用者走：
    //    BranchGrayHoldDur/BranchExitDur/BranchExitSlide/BranchGrowDur/BranchGrowFromScale 全在宝箱动画里用）──

    /// <summary>支线岛剧情节点图（2026-10-23 主人拍板：56字符 guid 一律不管，图全部路径加载，不依赖 prefab guid 引用）。</summary>
    static readonly string BranchStorySpritePath = ChestResDir + "剧情";
    /// <summary>「可接」徽章图（感叹号；可接时显示，打完隐藏，路径加载）。</summary>
    const string BranchBadgeSpritePath = ChestResDir + "感叹号徽章_128";

    // ── 剧情节点浮动摆动（仅可接时动，打完灰掉静止；幅度都调小，嫌大嫌小只改这四个数）──
    /// <summary>上下浮动幅度（像素）。</summary>
    const float BranchNodeBobAmp = 4f;
    /// <summary>上下浮动速度（弧度/秒）。</summary>
    const float BranchNodeBobSpeed = 1.6f;
    /// <summary>左右微摆角度（度）。</summary>
    const float BranchNodeRotAmp = 4f;
    /// <summary>左右微摆速度（弧度/秒）。</summary>
    const float BranchNodeRotSpeed = 1.1f;

    // ── 出现动效（2026-10-27 主人拍板恢复：岛「出现的时候要有动效」= 渐入 + 从下浮入；
    //    2026-10-23 回退的是消失/补位/开启那套 lifecycle，出现动效另算，不冲突）──
    /// <summary>出现动效时长（秒）。</summary>
    const float BranchEntryDur = 0.45f;
    /// <summary>浮入起点：从基准位下方多少像素浮上来。</summary>
    const float BranchEntryFromY = 60f;

    Transform _branchIslandRoot;
    /// <summary>支线岛浮动组件缓存（EnsureBranchIslands 建好后记下；瘦屏重排/还原写它的 basePos）。</summary>
    BranchIslandFloater _branchFloater;
    /// <summary>示例节点在关卡容器里的预制位（岛1基准 = 它；还原/重建用）。</summary>
    Vector2 _branchSampleBasePos;
    readonly List<Transform> _branchIslands = new List<Transform>(BranchIslandCount);
    readonly List<Image> _branchIslandChests = new List<Image>(BranchIslandCount);
    /// <summary>「可接」徽章（prefab 里主人摆的 剧情/可接 节点，随浮动一起动；下标与岛槽对齐）。</summary>
    readonly List<Image> _branchIslandBadges = new List<Image>(BranchIslandCount);
    readonly List<Image> _branchIslandIslandImgs = new List<Image>(BranchIslandCount);
    readonly List<Text> _branchIslandLabels = new List<Text>(BranchIslandCount);
    bool _branchIslandsMissingReported;

    /// <summary>第 idx 个岛槽派定的支线岛 id（0 = 未派定/未解锁）。派定真源 = branchRoll1/2（合并方案起记岛 id）。</summary>
    int BranchIslandIdOf(int idx)
        => idx == 0 ? (SaveSystem.Instance?.Data?.branchRoll1 ?? 0)
                    : (SaveSystem.Instance?.Data?.branchRoll2 ?? 0);

    /// <summary>
    /// 建支线岛容器与两个岛（只建一次）。克隆主人手摆的「支线」示例保结构
    /// （岛/剧情/可接徽章/名牌 + scale 0.7），岛1 = 示例原位，岛2 = 上方一个槽位节奏；
    /// 示例本体克隆完即隐藏（模板只留克隆件，游戏里不双份显示）。
    /// </summary>
    void EnsureBranchIslands()
    {
        if (_branchIslandRoot != null) return;
        var right = transform.Find("RightContent");
        var mainNode = right != null ? right.Find(MainNodeName) : null;
        if (mainNode == null)
        {
            if (!_branchIslandsMissingReported)
            {
                _branchIslandsMissingReported = true;
                Debug.LogError("[AdventureUI] 找不到 RightContent/主线 节点，支线岛无从挂载（缺真值不兜底）");
            }
            return;
        }

        // 克隆源：主人手摆的「支线」示例节点（关卡 容器下）
        // 【2026-10-24】关卡容器被滑动列表包裹后搬到 主线/关卡Viewport/关卡，两条路径都找
        Transform srcSlot = null;
        Transform guanka = null;
        for (int i = 0; i < mainNode.childCount; i++)
        {
            if (mainNode.GetChild(i).name == "关卡") { guanka = mainNode.GetChild(i); break; }
        }
        if (guanka == null)
        {
            var vp = mainNode.Find(ChestViewportName);
            guanka = vp != null ? vp.Find("关卡") : null;
        }
        if (guanka != null)
        {
            for (int k = 0; k < guanka.childCount; k++)
            {
                var s = guanka.GetChild(k);
                if (s.name == BranchIslandSampleNodeName) { srcSlot = s; break; }
            }
        }
        if (srcSlot == null)
        {
            if (!_branchIslandsMissingReported)
            {
                _branchIslandsMissingReported = true;
                Debug.LogError($"[AdventureUI] 主线/关卡 下找不到示例节点「{BranchIslandSampleNodeName}」，支线岛没有克隆源（缺真值不兜底）");
            }
            return;
        }
        if (srcSlot is RectTransform sampleRt)
        {
            // 岛1基准 = 示例原位。【2026-10-24】关卡容器被滑动列表包裹后，示例锚点参考中心
            // = 视口中心（视口与原容器同中心）低 ChestScrollRefShift —— 岛容器按视口帧摆，基准补回该偏移。
            _branchSampleBasePos = sampleRt.anchoredPosition - new Vector2(0f, ChestScrollRefShift);
        }

        // 容器：与关卡容器同锚同位（内部坐标和槽位同系，摆位常量直接可用）
        // 【2026-10-24】关卡容器已变成滑动列表的滚动内容（帧被改写）→ 帧改抄视口（= 原关卡容器预制体帧）
        var viewportRt = mainNode.Find(ChestViewportName) as RectTransform;
        var srcRt = viewportRt != null ? viewportRt : srcSlot.parent as RectTransform;
        var rootGo = new GameObject(BranchIslandContainerName, typeof(RectTransform));
        rootGo.transform.SetParent(mainNode, false);
        var rt = (RectTransform)rootGo.transform;
        if (srcRt != null)
        {
            rt.anchorMin = srcRt.anchorMin;
            rt.anchorMax = srcRt.anchorMax;
            rt.pivot = srcRt.pivot;
            rt.anchoredPosition = srcRt.anchoredPosition;
            rt.sizeDelta = srcRt.sizeDelta;
        }
        else
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(26f, -238f);
            rt.sizeDelta = new Vector2(100f, 100f);
        }
        _branchIslandRoot = rt;

        // 浮动组件（照主线槽位同款正弦；独立管理不混进 ChestNodes 的槽位列表）
        var floater = rootGo.AddComponent<BranchIslandFloater>();
        _branchFloater = floater;   // 缓存给瘦屏重排/还原用（Fit 调 Apply/RestoreBranchIslandStretchY）

        for (int i = 0; i < BranchIslandCount; i++)
        {
            var clone = Instantiate(srcSlot, rt);
            clone.name = "支线岛_" + (i + 1);
            var crt = clone as RectTransform;
            // 岛1 = 示例原位，岛2 = 上方一个槽位节奏（间距不随屏幕拉伸）
            crt.anchoredPosition = _branchSampleBasePos + new Vector2(0f, i * BranchIslandSpacing);

            // 克隆自带槽位的 CanvasGroup（边缘槽位 alpha 0.7）→ 重置成不透明可交互
            var cg = clone.GetComponent<CanvasGroup>();
            if (cg == null) cg = clone.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;

            var islandImg = clone.Find("岛")?.GetComponent<Image>();
            var chestImg = clone.Find("剧情")?.GetComponent<Image>();
            var label = clone.Find("bg/Text (Legacy)")?.GetComponent<Text>();
            var slotBg = clone.Find("bg")?.GetComponent<Image>();
            // 「可接」徽章：prefab 里主人摆好的（剧情/可接），直接引用
            var badgeImg = clone.Find("剧情/可接")?.GetComponent<Image>();

            // 槽位克隆前射线全关（防挡滑动翻章）；支线岛要可点 → 岛/剧情/名牌的射线打开
            if (islandImg != null) islandImg.raycastTarget = true;
            if (chestImg != null) chestImg.raycastTarget = true;
            if (slotBg != null) slotBg.raycastTarget = true;
            if (badgeImg != null) badgeImg.raycastTarget = false;   // 徽章纯装饰，不挡点击
            _branchIslandBadges.Add(badgeImg);

            var btn = clone.GetComponent<Button>();
            if (btn == null) btn = clone.gameObject.AddComponent<Button>();
            if (chestImg != null) btn.targetGraphic = chestImg;
            btn.transition = Selectable.Transition.None;   // 选中/灰暗全由 RefreshBranchIslands 控制
            btn.onClick.RemoveAllListeners();
            int idx = i;
            btn.onClick.AddListener(() => OnClickBranchIsland(idx));

            // 剧情节点摆动注册（可接时浮动+微摆；打完复位静止；下标与岛槽对齐）
            floater.RegisterWobble(chestImg != null ? (RectTransform)chestImg.transform : null);

            floater.slots.Add(crt);
            floater.basePos.Add(crt.anchoredPosition);
            _branchIslands.Add(clone);
            _branchIslandChests.Add(chestImg);
            _branchIslandIslandImgs.Add(islandImg);
            _branchIslandLabels.Add(label);
        }
        // 示例本体隐藏（模板只留克隆件，游戏里不双份显示）
        srcSlot.gameObject.SetActive(false);
        GameFonts.ApplyToHierarchy(rt);
    }

    /// <summary>
    /// 支线岛状态刷新（RefreshAll 调，仅主线模式显示）：
    /// 名字（支线地图名）+ 剧情节点图（可接原色/打完灰）+ 可接徽章（打完隐藏）+ 岛图 + 灰暗度。
    /// 2026-10-23 主人拍板回退：lifecycle 动画（下移消失/补位/开启）只属于主线宝箱，支线岛不做。
    /// 2026-10-27 主人拍板恢复「出现动效」：岛从隐藏→出现的那一次播 渐入+从下浮入（BranchEntry* 常量）；
    /// 消失/补位/开启依旧不做。
    /// </summary>
    void RefreshBranchIslands()
    {
        bool firstBuild = _branchIslandRoot == null;
        EnsureBranchIslands();
        if (_branchIslandRoot == null) return;
        // 2026-10-23 主人「右边那几个岛在前期不会出现支线」：还没派定的岛不出现（不再摆「？？？」空岛），
        // 两个岛槽全未派定（第 1 章未通关的前期）整组隐藏。
        var data = SaveSystem.Instance?.Data;
        bool anyAssignedEarly = false;
        for (int i = 0; i < BranchIslandCount; i++)
        {
            if (BranchIslandIdOf(i) > 0) { anyAssignedEarly = true; break; }
        }
        bool show = _selectedMode == 0 && anyAssignedEarly;
        // 容器由隐藏转显示（或首次建出来）的这一帧 = 岛「出现」，解锁的岛槽要播出现动效
        bool rootBecameShown = show && (firstBuild || !_branchIslandRoot.gameObject.activeSelf);
        if (_branchIslandRoot.gameObject.activeSelf != show)
            _branchIslandRoot.gameObject.SetActive(show);
        if (!show) return;

        for (int i = 0; i < _branchIslands.Count && i < BranchIslandCount; i++)
        {
            int islandId = BranchIslandIdOf(i);
            bool unlocked = islandId > 0;   // 已派定（岛槽1=通关第1章后；岛槽2=通关岛槽1的岛后）
            var isl = unlocked ? SideIslandDefs.Get(islandId) : null;
            bool cleared = unlocked && data != null && data.HasClearedSideIsland(islandId);

            // 未派定的岛槽直接隐藏（不显示「？？？」占位）
            var slotGo = _branchIslands[i].gameObject;
            // 出现时机：槽位自身由隐藏转显示，或容器这次整体刚出现 → 播出现动效
            bool slotBecameShown = unlocked && (!slotGo.activeSelf || rootBecameShown);
            if (slotGo.activeSelf != unlocked)
                slotGo.SetActive(unlocked);
            if (!unlocked) continue;
            if (slotBecameShown)
                _branchFloater?.PlayEntry(i);

            var lbl = _branchIslandLabels[i];
            if (lbl != null)
                lbl.text = isl != null ? isl.name : "？？？";

            // 剧情节点图：路径加载（2026-10-23 主人拍板「56字符一律不管，全部路径加载」）
            // 可接原色；打完灰掉（ChestClearedTint，与主线宝箱同色）
            var chestImg = _branchIslandChests[i];
            if (chestImg != null)
            {
                var sp = LoadChestSprite(BranchStorySpritePath);
                if (sp != null) chestImg.sprite = sp;
                chestImg.color = cleared ? ChestClearedTint : Color.white;
            }

            // 「可接」徽章：可接显示，打完隐藏；图同样路径加载
            bool wantBadge = !cleared;
            var badge = _branchIslandBadges[i];
            if (badge != null)
            {
                var bsp = LoadChestSprite(BranchBadgeSpritePath);
                if (bsp != null) badge.sprite = bsp;
                if (badge.gameObject.activeSelf != wantBadge)
                    badge.gameObject.SetActive(wantBadge);
            }

            // 浮动+微摆只在可接时跑；打完（灰）静止复位
            _branchFloater?.SetWobble(i, wantBadge);

            // 岛图轮换（岛1/岛2 各一张，跟主线槽位同素材目录）
            var islandImg = _branchIslandIslandImgs[i];
            if (islandImg != null)
            {
                var islandSp = LoadChestSprite(IslandPaths[i % IslandPaths.Length]);
                if (islandSp != null) islandImg.sprite = islandSp;
            }

            // 灰暗度：待打全亮；已通关微暗（出现动效播放期间别抢 alpha，等动效走完）
            var cg = _branchIslands[i].GetComponent<CanvasGroup>();
            if (cg != null && (_branchFloater == null || !_branchFloater.IsEntryActive(i)))
                cg.alpha = cleared ? 0.85f : 1f;
        }
    }

    /// <summary>
    /// 点支线岛（2026-10-23 合并方案）：待打 → 确认弹窗（10 体力 + 奖励预览）→ 进单 Boss 特殊关；
    /// 未解锁/已通关 → 提示。不再 FocusChapter 切章（旧支线章已并入主线）。
    /// </summary>
    void OnClickBranchIsland(int idx)
    {
        if (idx < 0 || idx >= BranchIslandCount) return;
        int islandId = BranchIslandIdOf(idx);
        var data = SaveSystem.Instance?.Data;
        if (islandId <= 0)
        {
            Toast(idx == 0 ? "通关第一章后开启支线岛" : "先打通前面的支线岛，这里才会开启");
            return;
        }
        var isl = SideIslandDefs.Get(islandId);
        if (isl == null)
        {
            Debug.LogError($"[AdventureUI] 支线岛点击：岛定义缺失（islandId={islandId}）");
            return;
        }
        if (data != null && data.HasClearedSideIsland(islandId))
        {
            Toast($"{isl.name} 已经通关了");
            return;
        }

        // 确认弹窗（硬规则：双按钮确认框一律走 StaminaConfirmPopup.Show）——预览消耗与产出
        string body = $"消耗体力 {StaminaSystem.SIDE_ISLAND_COST}\n"
                    + $"通关奖励：金币 {isl.goldReward} ＋ 天赋石 {isl.talentReward} ＋ 装备掉落";
        StaminaConfirmPopup.Show($"支线岛·{isl.name}", body, "出航", () =>
        {
            // 体力不足：TrySpend 内已弹「体力不足」，这里只终止
            if (!StaminaSystem.TrySpendForSideIsland())
                return;

            // 进本标记（2026-10-28 起统一走 ResetPendingFlags 全清，再立本岛标记；BattleManager 读后即清）
            AdventureUI.ResetPendingFlags();
            AdventureUI.PendingSideIsland = isl.id;
            AdventureUI.PendingBattleChapter = isl.act;   // 背景篇（怪池/背景资产同源复用）
            AdventureUI.PendingBattleDifficulty = 0;
            ChapterManager.Instance?.SetChapter(isl.act);

            // 不弹职业选择（2026-10-18 裂隙同款口径：刷图玩法用当前职业直接进本）
            HidePage();
            GameSceneManager.Instance?.LoadBattleScene();
        });
    }

    // ────────────────────────────────────────────────────
    // 支线岛摆位（2026-10-23 主人拍板「按我摆放的位置，岛之间的距离不变」）：
    // 岛1 = 示例节点原位，岛2 = 上方一个槽位节奏；瘦屏不拉伸支线岛（Fit 不再调 Apply）。
    // 标准屏 / 关开关走 RestoreBranchIslandBaseY 复位。
    // ────────────────────────────────────────────────────

    /// <summary>还原支线岛浮动基准到示例口径（岛1 = 示例原位，岛2 = 上方一个槽位节奏）。</summary>
    public void RestoreBranchIslandBaseY()
    {
        if (_branchFloater == null) return;
        for (int i = 0; i < _branchFloater.basePos.Count; i++)
            _branchFloater.basePos[i] = _branchSampleBasePos + new Vector2(0f, i * BranchIslandSpacing);
    }

    /// <summary>
    /// 支线岛浮动（照主线槽位同款正弦：SlotFloatAmp/SlotFloatSpeed 复用，相位错开不同步起落）。
    /// 独立组件挂在支线岛容器上，不混进 ChestNodes 的槽位列表（那边有自己的浮动管理）。
    /// 2026-10-23 增补：剧情节点（宝箱位）自身的浮动+微摆（BranchNode* 常量，幅度小），
    /// 仅「可接」时跑；打完灰掉后复位静止。
    /// </summary>
    sealed class BranchIslandFloater : MonoBehaviour
    {
        public readonly List<RectTransform> slots = new List<RectTransform>();
        public readonly List<Vector2> basePos = new List<Vector2>();

        // 剧情节点摆动（下标与岛槽对齐；允许 null 占位）
        readonly List<RectTransform> wobbleNodes = new List<RectTransform>();
        readonly List<Vector3> wobbleBasePos = new List<Vector3>();
        readonly List<bool> wobbleActive = new List<bool>();
        // 出现动效状态（下标与 slots 对齐；PlayEntry 懒扩容，建岛时不必预置）
        readonly List<bool> entryActive = new List<bool>();
        readonly List<float> entryT = new List<float>();
        readonly List<CanvasGroup> entryCg = new List<CanvasGroup>();
        float _phase;

        void Awake() => _phase = UnityEngine.Random.Range(0f, 6.28f);

        /// <summary>该槽是否正在播出现动效（刷新灰暗度时别写它的 alpha）。</summary>
        public bool IsEntryActive(int idx)
            => idx >= 0 && idx < entryActive.Count && entryActive[idx];

        /// <summary>
        /// 播出现动效（2026-10-27 主人拍板：出现要有动效）：alpha 归 0，
        /// 从基准位下方 BranchEntryFromY 处渐入+浮入到位（Update 里推进，easeOutCubic）。
        /// </summary>
        public void PlayEntry(int idx)
        {
            if (idx < 0 || idx >= slots.Count || slots[idx] == null) return;
            while (entryActive.Count <= idx) { entryActive.Add(false); entryT.Add(0f); entryCg.Add(null); }
            entryActive[idx] = true;
            entryT[idx] = 0f;
            entryCg[idx] = slots[idx].GetComponent<CanvasGroup>();
            if (entryCg[idx] != null) entryCg[idx].alpha = 0f;
        }

        /// <summary>注册剧情节点（建岛时一次；基准取注册时的预制体 localPose）。</summary>
        public void RegisterWobble(RectTransform node)
        {
            wobbleNodes.Add(node);
            wobbleBasePos.Add(node != null ? node.localPosition : Vector3.zero);
            wobbleActive.Add(false);
        }

        /// <summary>切换摆动开关；关时把节点复位回预制体姿势（只复位一次，不逐帧写）。</summary>
        public void SetWobble(int idx, bool active)
        {
            if (idx < 0 || idx >= wobbleNodes.Count) return;
            if (wobbleNodes[idx] == null || wobbleActive[idx] == active) return;
            wobbleActive[idx] = active;
            if (!active)
            {
                wobbleNodes[idx].localPosition = wobbleBasePos[idx];
                wobbleNodes[idx].localRotation = Quaternion.identity;
            }
        }

        void Update()
        {
            float t = Time.unscaledTime + _phase;
            for (int i = 0; i < slots.Count && i < basePos.Count; i++)
            {
                if (slots[i] == null) continue;
                // 出现动效推进：alpha 渐入 + y 从下方浮上来（与常驻浮动叠加，走完即停在基准位）
                float entryOff = 0f;
                if (i < entryActive.Count && entryActive[i])
                {
                    entryT[i] += Time.unscaledDeltaTime;
                    float k = Mathf.Clamp01(entryT[i] / BranchEntryDur);
                    float eased = 1f - (1f - k) * (1f - k) * (1f - k);   // easeOutCubic
                    entryOff = (1f - eased) * -BranchEntryFromY;
                    if (entryCg[i] != null) entryCg[i].alpha = eased;
                    if (k >= 1f) entryActive[i] = false;
                }
                var p = slots[i].anchoredPosition;
                p.y = basePos[i].y + entryOff + Mathf.Sin(t * SlotFloatSpeed + i * 1.57f) * SlotFloatAmp;
                slots[i].anchoredPosition = p;
            }
            for (int i = 0; i < wobbleNodes.Count; i++)
            {
                if (!wobbleActive[i] || wobbleNodes[i] == null) continue;
                var n = wobbleNodes[i];
                var lp = wobbleBasePos[i];
                lp.y += Mathf.Sin(t * BranchNodeBobSpeed + i * 1.9f) * BranchNodeBobAmp;
                n.localPosition = lp;
                n.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.Sin(t * BranchNodeRotSpeed + i * 1.3f) * BranchNodeRotAmp);
            }
        }
    }
}
