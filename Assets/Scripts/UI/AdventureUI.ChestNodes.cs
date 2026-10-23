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
//   一屏只显示 4 个：预制体 4 个节点就是窗口 4 个槽位（按 y 从低到高 = 关卡从早到晚），
//   窗口随进度自动定位（不许手动滑动）；窗口首（底）尾（顶）两个透明度 0.7。
public partial class AdventureUI
{
    /// <summary>预制体节点容器 RightContent/关卡。</summary>
    Transform _guankaRoot;

    /// <summary>窗口槽位（按 y 升序 = 底→顶 = 关卡从早到晚）。</summary>
    readonly List<RectTransform> _slotNodes = new List<RectTransform>(4);
    readonly List<Image> _slotChestImgs = new List<Image>(4);
    readonly List<Image> _slotIslandImgs = new List<Image>(4);
    readonly List<Text> _slotLabels = new List<Text>(4);
    bool _guankaMissingReported;

    /// <summary>宝箱/岛图 Resources 目录。</summary>
    const string ChestResDir = "Art/UI/Adventure/宝箱/";

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

            // 槽位根的基准坐标/缩放记预制体值：浮动围绕基准做正弦，当前关放大做倍乘
            _slotFloatBasePos.Add(slot.anchoredPosition);
            _slotPrefabBasePos.Add(slot.anchoredPosition);
            _slotBaseScales[slot] = slot.localScale;

            // 不参与点按：关掉射线，避免挡地图左右滑翻章手势（关卡容器不在 MapRoot 子树内）
            var graphics = slot.GetComponentsInChildren<Graphic>(true);
            for (int g = 0; g < graphics.Length; g++)
                graphics[g].raycastTarget = false;
        }

        if (_slotNodes.Count != 4)
            Debug.LogError($"[AdventureUI] 关卡 容器下槽位节点数={_slotNodes.Count}（应为 4），窗口显示会不完整（缺真值不兜底）");
    }

    /// <summary>
    /// 按当前选中章刷新窗口内容：算窗口（4 关）→ 每个槽位换宝箱三态图 + 岛 + 编号 → 首尾淡出。
    /// 进度真源 = 存档 clearedStages（键 "章_关卡"，0 起关号）：
    /// 已通关最大关 = maxCleared；i ≤ maxCleared → 开启；i == maxCleared+1 → 关闭；其余 → 未解锁。
    /// 整章通关后无「关闭」位，全开；窗口停在最后 4 关。
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

        int total = GameConfig.STAGES_PER_GRAND;
        int maxCleared = GetMaxClearedStage(_selectedChapter);
        int current = Mathf.Clamp(maxCleared + 1, 1, total + 1);   // 下一关（1 起）；整关通关 = 6

        // 窗口起点：让「当前关」落在窗口第 2 位（底数第 2，首尾留给淡出的上下边缘）；越界往回收
        int windowSize = Mathf.Max(1, _slotNodes.Count);
        int start = Mathf.Clamp(current - 1, 1, total - windowSize + 1);

        for (int slot = 0; slot < _slotNodes.Count; slot++)
        {
            int stage = start + slot;                 // 本槽位显示的关号（1 起）
            if (stage > total)                        // 窗口越出章尾（理论上 start 钳制后不会发生）
            {
                if (_slotNodes[slot].gameObject.activeSelf)
                    _slotNodes[slot].gameObject.SetActive(false);
                continue;
            }
            if (!_slotNodes[slot].gameObject.activeSelf)
                _slotNodes[slot].gameObject.SetActive(true);

            // 三态换图：【2026-10-23 批 B1】每大关 5 小关 → 第 3 关银、第 5 关（Boss）金，其余木。
            // （旧 10 关制是「第 5 关银、第 10 关金」；5 关制下两者会重合到第 5 关，故银改到第 3 关。
            //  摆法照 Docs/关卡结构重构_篇大关小关_待办任务清单.md 批 D 建议，主人可否决。）
            int state = stage <= maxCleared ? ChestStateOpen
                      : stage == maxCleared + 1 ? ChestStateClosed
                      : ChestStateLocked;
            string[] paths = stage == ChestSilverStage ? ChestSilverPaths
                           : stage == total ? ChestGoldPaths
                           : ChestWoodPaths;

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
            }

            // 2026-10-23 主人反馈：当前关不再只放大宝箱（×2 太大且与岛脱节），
            // 改为整槽位（岛+宝箱+编号牌）一起缩放，倍数 SlotCurrentScale=1.35。
            // 基准缩放取预制体现值，代码只做倍乘，不写死数值；非当前关恢复基准。
            var slotRt = _slotNodes[slot];
            if (!_slotBaseScales.TryGetValue(slotRt, out var baseScale))
            {
                baseScale = slotRt.localScale;
                _slotBaseScales[slotRt] = baseScale;
            }
            slotRt.localScale = state == ChestStateClosed ? baseScale * SlotCurrentScale : baseScale;

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

            // 首尾淡出（底/顶两个槽位）
            bool edge = slot == 0 || slot == _slotNodes.Count - 1;
            var cg = _slotNodes[slot].GetComponent<CanvasGroup>();
            if (cg == null) cg = _slotNodes[slot].gameObject.AddComponent<CanvasGroup>();
            cg.alpha = edge ? ChestEdgeAlpha : 1f;
        }
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
    // 槽位浮动（2026-10-23 主人反馈「岛和宝箱没有浮动动效」）
    // 整槽位（岛+宝箱+编号牌）一起围绕预制体基准 y 做小幅正弦；
    // 各槽位相位错开，不同步起落。幅度/速度见 SlotFloatAmp/SlotFloatSpeed。
    // AdventureUI 无其他 Update，动画集中在此一处。
    // ────────────────────────────────────────────────────
    void Update()
    {
        if (_guankaRoot == null || !_guankaRoot.gameObject.activeInHierarchy) return;
        float t = Time.unscaledTime;
        for (int i = 0; i < _slotNodes.Count && i < _slotFloatBasePos.Count; i++)
        {
            var rt = _slotNodes[i];
            if (rt == null) continue;
            var p = rt.anchoredPosition;
            p.y = _slotFloatBasePos[i].y + Mathf.Sin(t * SlotFloatSpeed + i * 1.57f) * SlotFloatAmp;
            rt.anchoredPosition = p;
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

    /// <summary>还原槽位浮动基准到预制体原始值（标准屏 / 关掉瘦屏适配时调；下一帧 Update 生效）。</summary>
    public void RestoreSlotColumnBaseY()
    {
        for (int i = 0; i < _slotFloatBasePos.Count && i < _slotPrefabBasePos.Count; i++)
            _slotFloatBasePos[i] = _slotPrefabBasePos[i];
    }

    // ────────────────────────────────────────────────────
    // 「获得奖励」弹窗消费（2026-10-22 主人拍板：打完一关回来，宝箱已开 + 自动弹本关实际掉落）
    // ────────────────────────────────────────────────────

    /// <summary>
    /// 进冒险页时消费 AdventureRewardNotice 待弹队列（ShowPage 调用，每次进来只弹一次）。
    /// 多关累积时合并成一张弹窗：金币/天赋石/附魔石/强化材料求和，装备逐件列。
    /// 内容与战斗结算同账（RunStats 差值），不新造数值。
    /// </summary>
    void ConsumeAdventureRewardNotices()
    {
        if (!AdventureRewardNotice.HasPending) return;
        var list = AdventureRewardNotice.Drain();
        if (list == null || list.Count == 0) return;

        long gold = 0;
        int talent = 0, enchant = 0, mats = 0;
        var equips = new List<EquipGainEntry>();
        for (int i = 0; i < list.Count; i++)
        {
            var n = list[i];
            if (n == null) continue;
            gold += n.gold;
            talent += n.talent;
            enchant += n.enchant;
            mats += n.mats;
            if (n.equips != null) equips.AddRange(n.equips);
        }

        var entries = new List<RewardPopupUI.Entry>();
        // 金币 ≥2 才上格（与结算同口径：1 个金币视为没获得）
        if (gold >= 2)
            entries.Add(new RewardPopupUI.Entry(CommonIconSprites.Load("icon_gold"), $"金币 ×{gold}"));
        if (talent > 0)
            entries.Add(new RewardPopupUI.Entry(CommonIconSprites.Load("icon_talent_stone"), $"天赋石 ×{talent}"));
        if (enchant > 0)
            entries.Add(new RewardPopupUI.Entry(CommonIconSprites.Load("icon_enchant_stone"), $"附魔石 ×{enchant}"));
        if (mats > 0)
            entries.Add(new RewardPopupUI.Entry(CommonIconSprites.Load("icon_decompose_mat"), $"强化材料 ×{mats}"));
        for (int i = 0; i < equips.Count; i++)
        {
            var g = equips[i];
            entries.Add(new RewardPopupUI.Entry(
                g.icon,
                string.IsNullOrEmpty(g.name) ? "装备 ×1" : $"{g.name} ×1"));
        }

        if (entries.Count == 0) return;
        RewardPopupUI.Show(entries);
    }
}
