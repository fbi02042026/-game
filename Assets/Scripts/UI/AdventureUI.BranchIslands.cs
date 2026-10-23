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
// 【实现】运行时克隆主线槽位（关卡/1-1：岛+宝箱+名牌结构）生成两个岛，
//   挂 RightContent/主线/支线岛屿 容器下 —— 不在「关卡」容器内，
//   因此不参与窗口滑动定位，EnsureChestNodes 的槽位扫描也不会误认。
//   宝箱用银箱三态（未解锁/待打/已通关）区分主线木箱；岛图轮换 岛1/岛2（专属岛图等 P0 素材）；未解锁显示「？？？」。
public partial class AdventureUI
{
    /// <summary>支线岛槽数量（岛壳沿用 2 个：依次解锁）。</summary>
    const int BranchIslandCount = 2;
    /// <summary>支线岛容器名（挂在 RightContent/主线 下，别动名字）。</summary>
    const string BranchIslandContainerName = "支线岛屿";
    /// <summary>支线岛相对主线槽位列的横向偏移（摆在窗口右侧；嫌远嫌近只改这一个数）。</summary>
    const float BranchIslandOffsetX = 160f;
    /// <summary>支线岛的槽位 y（照主线槽位 1-1/1-2 的预制体 y，纵向对齐）。</summary>
    static readonly float[] BranchIslandY = { -64f, 82f };
    /// <summary>未解锁支线岛的名字占位。</summary>
    const string BranchIslandLockedName = "？？？";

    Transform _branchIslandRoot;
    /// <summary>支线岛浮动组件缓存（EnsureBranchIslands 建好后记下；瘦屏重排/还原写它的 basePos）。</summary>
    BranchIslandFloater _branchFloater;
    readonly List<Transform> _branchIslands = new List<Transform>(BranchIslandCount);
    readonly List<Image> _branchIslandChests = new List<Image>(BranchIslandCount);
    readonly List<Image> _branchIslandIslandImgs = new List<Image>(BranchIslandCount);
    readonly List<Text> _branchIslandLabels = new List<Text>(BranchIslandCount);
    bool _branchIslandsMissingReported;

    /// <summary>第 idx 个岛槽派定的支线岛 id（0 = 未派定/未解锁）。派定真源 = branchRoll1/2（合并方案起记岛 id）。</summary>
    int BranchIslandIdOf(int idx)
        => idx == 0 ? (SaveSystem.Instance?.Data?.branchRoll1 ?? 0)
                    : (SaveSystem.Instance?.Data?.branchRoll2 ?? 0);

    /// <summary>
    /// 建支线岛容器与两个岛（只建一次）。克隆主线槽位 1-1 保结构（岛/宝箱/名牌 + scale 0.7），
    /// 位置摆到主线槽位列右侧固定处（BranchIslandOffsetX / BranchIslandY）。
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

        // 克隆源：主线槽位（关卡 容器下第一个「数字-数字」命名节点）
        Transform srcSlot = null;
        Transform guanka = null;
        for (int i = 0; i < mainNode.childCount; i++)
        {
            if (mainNode.GetChild(i).name == "关卡") { guanka = mainNode.GetChild(i); break; }
        }
        if (guanka != null)
        {
            for (int k = 0; k < guanka.childCount; k++)
            {
                var s = guanka.GetChild(k);
                if (Regex.IsMatch(s.name, @"^\d+-\d+$")) { srcSlot = s; break; }
            }
        }
        if (srcSlot == null)
        {
            if (!_branchIslandsMissingReported)
            {
                _branchIslandsMissingReported = true;
                Debug.LogError("[AdventureUI] 主线/关卡 下找不到槽位节点（数字-数字），支线岛没有克隆源（缺真值不兜底）");
            }
            return;
        }

        // 容器：与关卡容器同锚同位（内部坐标和槽位同系，摆位常量直接可用）
        var srcRt = srcSlot.parent as RectTransform;
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
            crt.anchoredPosition = new Vector2(BranchIslandOffsetX, BranchIslandY[i]);

            // 克隆自带槽位的 CanvasGroup（边缘槽位 alpha 0.7）→ 重置成不透明可交互
            var cg = clone.GetComponent<CanvasGroup>();
            if (cg == null) cg = clone.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;

            var islandImg = clone.Find("岛")?.GetComponent<Image>();
            var chestImg = clone.Find("宝箱")?.GetComponent<Image>();
            var label = clone.Find("bg/Text (Legacy)")?.GetComponent<Text>();
            var slotBg = clone.Find("bg")?.GetComponent<Image>();

            // 槽位克隆前射线全关（防挡滑动翻章）；支线岛要可点 → 岛/宝箱/名牌的射线打开
            if (islandImg != null) islandImg.raycastTarget = true;
            if (chestImg != null) chestImg.raycastTarget = true;
            if (slotBg != null) slotBg.raycastTarget = true;

            var btn = clone.GetComponent<Button>();
            if (btn == null) btn = clone.gameObject.AddComponent<Button>();
            if (chestImg != null) btn.targetGraphic = chestImg;
            btn.transition = Selectable.Transition.None;   // 选中/灰暗全由 RefreshBranchIslands 控制
            btn.onClick.RemoveAllListeners();
            int idx = i;
            btn.onClick.AddListener(() => OnClickBranchIsland(idx));

            floater.slots.Add(crt);
            floater.basePos.Add(crt.anchoredPosition);
            _branchIslands.Add(clone);
            _branchIslandChests.Add(chestImg);
            _branchIslandIslandImgs.Add(islandImg);
            _branchIslandLabels.Add(label);
        }
        GameFonts.ApplyToHierarchy(rt);
    }

    /// <summary>
    /// 支线岛状态刷新（RefreshAll 调，仅主线模式显示）：
    /// 名字（未解锁「？？？」/ 支线地图名）+ 银箱三态（未解锁/待打/已通关）+ 岛图 + 灰暗度。
    /// </summary>
    void RefreshBranchIslands()
    {
        EnsureBranchIslands();
        if (_branchIslandRoot == null) return;
        bool show = _selectedMode == 0;
        if (_branchIslandRoot.gameObject.activeSelf != show)
            _branchIslandRoot.gameObject.SetActive(show);
        if (!show) return;

        var data = SaveSystem.Instance?.Data;
        for (int i = 0; i < _branchIslands.Count && i < BranchIslandCount; i++)
        {
            int islandId = BranchIslandIdOf(i);
            bool unlocked = islandId > 0;   // 已派定（岛槽1=通关第1章后；岛槽2=通关岛槽1的岛后）
            var isl = unlocked ? SideIslandDefs.Get(islandId) : null;
            bool cleared = unlocked && data != null && data.HasClearedSideIsland(islandId);

            var lbl = _branchIslandLabels[i];
            if (lbl != null)
                lbl.text = isl != null ? isl.name : BranchIslandLockedName;

            // 宝箱三态（银箱，区分主线木箱）
            var chestImg = _branchIslandChests[i];
            if (chestImg != null)
            {
                int state = !unlocked ? ChestStateLocked
                          : cleared ? ChestStateOpen
                          : ChestStateClosed;
                var sp = LoadChestSprite(ChestSilverPaths[state]);
                if (sp != null)
                {
                    chestImg.sprite = sp;
                    chestImg.color = Color.white;
                }
                else
                {
                    chestImg.color = new Color(1f, 1f, 1f, 0.15f);
                    Debug.LogError($"[AdventureUI] 支线宝箱图缺失：{ChestSilverPaths[state]}");
                }
            }

            // 岛图轮换（岛1/岛2 各一张，跟主线槽位同素材目录）
            var islandImg = _branchIslandIslandImgs[i];
            if (islandImg != null)
            {
                var islandSp = LoadChestSprite(IslandPaths[i % IslandPaths.Length]);
                if (islandSp != null) islandImg.sprite = islandSp;
            }

            // 灰暗度：待打全亮；已通关微暗；未解锁压暗
            var cg = _branchIslands[i].GetComponent<CanvasGroup>();
            if (cg != null)
                cg.alpha = !unlocked ? 0.45f
                         : cleared ? 0.85f
                         : 1f;
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

            // 进本标记（照徽记/金币本口径：Pending 全量重设，BattleManager 读后即清）
            AdventureUI.PendingSideIsland = isl.id;
            AdventureUI.PendingBattleChapter = isl.act;   // 背景篇（怪池/背景资产同源复用）
            AdventureUI.PendingBattleDifficulty = 0;
            AdventureUI.PendingGoldDungeon = false;
            AdventureUI.PendingBadgeDungeon = false;
            AdventureUI.PendingEndlessRift = false;
            ChapterManager.Instance?.SetChapter(isl.act);

            // 不弹职业选择（2026-10-18 裂隙同款口径：刷图玩法用当前职业直接进本）
            HidePage();
            GameSceneManager.Instance?.LoadBattleScene();
        });
    }

    // ────────────────────────────────────────────────────
    // 瘦屏支线岛重排（AdventureUI.Fit.FitMainColumnStretch 调用）：
    // 支线岛容器与关卡容器同锚同位（克隆时复制）→ 岛槽与主线槽位同一参考系，
    // Fit 用主列跨度算出带内节奏 y 后写进 floater.basePos（浮动基准）——
    // 不绕过浮动动画，浮动照常围绕新基准做正弦。标准屏 / 关开关走 RestoreBranchIslandBaseY。
    // ────────────────────────────────────────────────────

    /// <summary>瘦屏重排：按 ysLocal（关卡容器帧 y）改写支线岛浮动基准；x 保持横向偏移不动。</summary>
    public void ApplyBranchIslandStretchY(IReadOnlyList<float> ysLocal)
    {
        if (_branchFloater == null || ysLocal == null) return;
        for (int i = 0; i < _branchFloater.basePos.Count && i < ysLocal.Count; i++)
            _branchFloater.basePos[i] = new Vector2(_branchFloater.basePos[i].x, ysLocal[i]);
    }

    /// <summary>还原支线岛浮动基准到预制体口径（BranchIslandOffsetX / BranchIslandY）。</summary>
    public void RestoreBranchIslandBaseY()
    {
        if (_branchFloater == null) return;
        for (int i = 0; i < _branchFloater.basePos.Count && i < BranchIslandY.Length; i++)
            _branchFloater.basePos[i] = new Vector2(BranchIslandOffsetX, BranchIslandY[i]);
    }

    /// <summary>
    /// 支线岛浮动（照主线槽位同款正弦：SlotFloatAmp/SlotFloatSpeed 复用，相位错开不同步起落）。
    /// 独立组件挂在支线岛容器上，不混进 ChestNodes 的槽位列表（那边有自己的浮动管理）。
    /// </summary>
    sealed class BranchIslandFloater : MonoBehaviour
    {
        public readonly List<RectTransform> slots = new List<RectTransform>();
        public readonly List<Vector2> basePos = new List<Vector2>();
        float _phase;

        void Awake() => _phase = UnityEngine.Random.Range(0f, 6.28f);

        void Update()
        {
            float t = Time.unscaledTime + _phase;
            for (int i = 0; i < slots.Count && i < basePos.Count; i++)
            {
                if (slots[i] == null) continue;
                var p = slots[i].anchoredPosition;
                p.y = basePos[i].y + Mathf.Sin(t * SlotFloatSpeed + i * 1.57f) * SlotFloatAmp;
                slots[i].anchoredPosition = p;
            }
        }
    }
}
