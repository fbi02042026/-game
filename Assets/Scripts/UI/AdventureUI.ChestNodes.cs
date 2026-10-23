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

    /// <summary>已通关（奖已领）宝箱的灰度（2026-10-23 主人拍板「领取后的宝箱就还用关闭灰掉就行」）。</summary>
    static readonly Color ChestClearedTint = new Color(0.6f, 0.6f, 0.6f, 1f);

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
    //    上面槽位补位滑动；轮到新当前关=从小到大开启+感叹号徽章蹦出颤一下）──
    /// <summary>宝箱领取奖励：每关首通 +3 钻（2026-10-20 停发的结算首通钻，改到宝箱点击领取时发）。</summary>
    const int ChestClaimDiamond = 3;
    /// <summary>每个槽位当前显示的关号（1 起；-1=隐藏）——点击领奖反查用，刷新时写。</summary>
    readonly List<int> _slotStage = new List<int>();
    /// <summary>补位滑动偏移（>0 = 节点在基准上方这么多像素，Update 逐帧衰减到 0）。</summary>
    readonly List<float> _slotSlideOffset = new List<float>();
    /// <summary>「可打」感叹号徽章（挂当前关宝箱上；按槽位缓存，懒建）。</summary>
    readonly List<Image> _slotBadges = new List<Image>();
    /// <summary>领取退场协程（非空 = 动画拥有画面，刷新早退；ShowPage 进页清一次防页面隐藏杀协程后卡死）。</summary>
    Coroutine _claimRoutine;
    /// <summary>各章上次刷新的当前关（当前关前进了才播开启动画；切章/回城不播）。</summary>
    readonly Dictionary<int, int> _lastCurrentByChapter = new Dictionary<int, int>();

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
        _slotStage.Clear(); _slotSlideOffset.Clear(); _slotBadges.Clear();

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
            _slotBadges.Add(null);

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
            _slotBadges.Add(null);
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
            if (last < _slotBadges.Count) _slotBadges.RemoveAt(last);
            if (rt != null) _slotBaseScales.Remove(rt);
            if (rt != null) Destroy(rt.gameObject);
        }
        if (_currentFloatSlot >= _slotNodes.Count) _currentFloatSlot = -1;
        return _slotNodes.Count;
    }

    /// <summary>
    /// 按当前选中章刷新窗口内容。2026-10-23 主人拍板改「领取制」：
    /// · 已领取的关从列里消失（未领关依次入槽），上面的槽位补位滑动；
    /// · 通关未领的宝箱 = 开启图 + 可点击（点击发 +3 钻原结算首通钻）；
    /// · 轮到新当前关（当前关前进了）播开启动画：从小到大+透明到出现+「可打」感叹号蹦出颤一下；
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
        if (_claimRoutine != null) return;   // 领取退场动画拥有画面，收尾自己全量刷新

        var data = SaveSystem.Instance?.Data;
        int total = GameConfig.STAGES_PER_GRAND;
        int maxCleared = GetMaxClearedStage(_selectedChapter);
        int current = Mathf.Clamp(maxCleared + 1, 1, total + 1);   // 下一关（1 起）；整章通关 = total+1
        _currentFloatSlot = -1;   // 浮动槽位每次刷新重算（只有当前关浮动）

        RebuildSlotContents(-1, data, total, maxCleared, current);

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

        // 「轮到剧情关卡」开启动画：该章当前关前进了才播（切章/回城不播）
        if (_lastCurrentByChapter.TryGetValue(_selectedChapter, out var lastCur)
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
    }

    /// <summary>
    /// 重排槽位内容（窗口映射：未领关依次入槽，已领的关消失；skipSlot=领取退场中的槽位不写内容）。
    /// 内容下移一档的槽位给 +rhythm 滑动偏移，由 Update 逐帧衰减实现补位滑动。
    /// </summary>
    void RebuildSlotContents(int skipSlot, SaveData data, int total, int maxCleared, int current)
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
                if (slot < _slotBadges.Count && _slotBadges[slot] != null && _slotBadges[slot].gameObject.activeSelf)
                    _slotBadges[slot].gameObject.SetActive(false);
                if (slot < _slotSlideOffset.Count) _slotSlideOffset[slot] = 0f;
                continue;
            }
            if (!_slotNodes[slot].gameObject.activeSelf)
                _slotNodes[slot].gameObject.SetActive(true);
            WriteSlotContent(slot, stage, maxCleared, current);
            if (stage == current) currentSlot = slot;
            // 内容下移一档（新 stage 来自更上面的旧槽）→ +rhythm 偏移；内容上移/新出现直接就位
            if (slot < _slotSlideOffset.Count)
                _slotSlideOffset[slot] = (oldStage[slot] >= 0 && stage > oldStage[slot]) ? rhythm : 0f;
        }
        _currentFloatSlot = currentSlot;
    }

    /// <summary>写单个槽位的显示内容（三态图/缩放/岛/编号/可点领奖/「可打」徽章；边沿淡出与滑动偏移不在此）。</summary>
    void WriteSlotContent(int slot, int stage, int maxCleared, int current)
    {
        var data = SaveSystem.Instance?.Data;
        int total = GameConfig.STAGES_PER_GRAND;
        bool isCleared = stage <= maxCleared;
        bool isCurrent = stage == current;
        bool isClaimable = isCleared && (data == null || !data.IsChestClaimed(_selectedChapter, stage - 1));

        // 三态换图 + 领取态：可领=开启图（打开状态等点击）；当前关=关闭原图；没打到=未解锁图。
        // 第 3 关银、第 5 关（Boss）金，其余木（【2026-10-23 批 B1】沿用）。
        string[] paths = stage == ChestSilverStage ? ChestSilverPaths
                       : stage == total ? ChestGoldPaths
                       : ChestWoodPaths;
        int state = isClaimable ? ChestStateOpen : (isCleared || isCurrent ? ChestStateClosed : ChestStateLocked);
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

        // 「可打」感叹号徽章：只挂当前关
        var badge = EnsureSlotBadge(slot);
        if (badge != null && badge.gameObject.activeSelf != isCurrent)
            badge.gameObject.SetActive(isCurrent);
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

    // ────────────────────────────────────────────────────
    // 宝箱领取 lifecycle（2026-10-23 主人拍板：通关未领=开启图可点；领取完变灰停一拍→下移消失、
    // 上面槽位补位滑动；轮到新当前关=从小到大开启+「可打」感叹号蹦出颤一下）
    // ────────────────────────────────────────────────────

    /// <summary>进页清残留领取动画标志（页面 SetActive(false) 会杀协程，标志不清刷新会永远早退）。ShowPage 调。</summary>
    public void ClearStaleClaimAnim()
    {
        _claimRoutine = null;
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
    /// 点宝箱领奖：+3 钻（2026-10-20 停发的结算首通钻改到这里发）→ 标记已领（防重复发奖）→
    /// 变灰停一拍 → 下滑淡出消失；上面槽位补位滑动（协程收尾全量刷新）。
    /// </summary>
    void OnClickChestClaim(int slot)
    {
        if (_claimRoutine != null) return;
        if (slot < 0 || slot >= _slotStage.Count || _slotStage[slot] <= 0) return;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        int stage1 = _slotStage[slot];   // 1 起关号
        bool cleared = stage1 <= GetMaxClearedStage(_selectedChapter);
        if (!cleared || data.IsChestClaimed(_selectedChapter, stage1 - 1)) return;

        if (!data.MarkChestClaimed(_selectedChapter, stage1 - 1)) return;   // 防重复发奖
        ResourceWallet.Add(ResourceWallet.ResourceType.Diamond, ChestClaimDiamond, save: false, notify: true);
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

        // 段一：变灰停一拍（原地不动，点击关掉、「可打」徽章收掉）
        SetChestClaimable(slot, false);
        if (slot < _slotBadges.Count && _slotBadges[slot] != null && _slotBadges[slot].gameObject.activeSelf)
            _slotBadges[slot].gameObject.SetActive(false);
        if (chestImg != null) chestImg.color = ChestClearedTint;
        yield return new WaitForSecondsRealtime(BranchGrayHoldDur);

        // 段二：自己下滑淡出（改浮动基准走 Update，不硬写 anchoredPosition 不打架）；
        // 同时上面槽位重排（内容下移一档 + 滑动偏移）
        var data = SaveSystem.Instance?.Data;
        int total = GameConfig.STAGES_PER_GRAND;
        int maxCleared = GetMaxClearedStage(_selectedChapter);
        int current = Mathf.Clamp(maxCleared + 1, 1, total + 1);
        RebuildSlotContents(slot, data, total, maxCleared, current);
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
    /// 轮到新当前关的开启动画：整槽从小到大（ease-out-back 微冲过头）+ 透明到出现，
    /// 长好后「可打」感叹号徽章突然蹦出来颤一下（四段回弹+摆头）。
    /// </summary>
    System.Collections.IEnumerator ChestCurrentGrowAnim(int slot)
    {
        var rt = _slotNodes[slot];
        if (rt == null) yield break;
        var cg = rt.GetComponent<CanvasGroup>();
        if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();
        var baseScale = _slotBaseScales.TryGetValue(rt, out var bs) ? bs : rt.localScale;
        float alpha1 = cg.alpha;   // 刷新刚写过边沿淡出，以它为终点

        var badge = EnsureSlotBadge(slot);
        if (badge != null) badge.gameObject.SetActive(false);   // 徽章等主体长出来再蹦

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

        yield return StartCoroutine(SlotBadgePopAnim(badge));
    }

    /// <summary>徽章蹦出颤一下：0 → 1.3（突然蹦出来）→ 0.85 → 1.08 → 1（来回颤），配一点左右摆头。</summary>
    System.Collections.IEnumerator SlotBadgePopAnim(Image badge)
    {
        if (badge == null) yield break;
        var tr = badge.transform;
        var baseScale = tr.localScale;
        badge.gameObject.SetActive(true);
        float[] until = { 0.10f, 0.20f, 0.30f, BranchBadgePopDur };
        float[] sMul  = { 1.30f, 0.85f, 1.08f, 1.00f };
        float[] rot   = { 10f,  -8f,   5f,   0f };
        float t = 0f, prevS = 0f, prevR = 0f;
        for (int seg = 0; seg < until.Length; seg++)
        {
            float segFrom = seg == 0 ? 0f : until[seg - 1];
            while (t < until[seg])
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01((t - segFrom) / Mathf.Max(1e-3f, until[seg] - segFrom));
                k = 1f - (1f - k) * (1f - k);
                tr.localScale = baseScale * Mathf.Lerp(prevS, sMul[seg], k);
                var eu = tr.localEulerAngles;
                tr.localEulerAngles = new Vector3(eu.x, eu.y, Mathf.Lerp(prevR, rot[seg], k));
                yield return null;
            }
            prevS = sMul[seg]; prevR = rot[seg];
        }
        tr.localScale = baseScale;
        var e2 = tr.localEulerAngles;
        tr.localEulerAngles = new Vector3(e2.x, e2.y, 0f);
    }

    /// <summary>槽位「可打」感叹号徽章（懒建：挂宝箱图上方 44×44，纯装饰不挡点击；图路径加载）。</summary>
    Image EnsureSlotBadge(int slot)
    {
        if (slot < _slotBadges.Count && _slotBadges[slot] != null) return _slotBadges[slot];
        if (slot >= _slotChestImgs.Count) return null;
        var chestImg = _slotChestImgs[slot];
        if (chestImg == null) return null;
        var go = new GameObject("可打", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(chestImg.transform, false);
        var brt = (RectTransform)go.transform;
        brt.anchorMin = new Vector2(0.5f, 1f);
        brt.anchorMax = new Vector2(0.5f, 1f);
        brt.pivot = new Vector2(0.5f, 0.5f);
        brt.anchoredPosition = new Vector2(0f, 18f);
        brt.sizeDelta = new Vector2(44f, 44f);
        var img = go.GetComponent<Image>();
        var sp = LoadChestSprite(ChestResDir + "感叹号徽章_128");
        if (sp != null) img.sprite = sp;
        else Debug.LogError($"[AdventureUI] 感叹号徽章图缺失：{ChestResDir}感叹号徽章_128");
        img.raycastTarget = false;
        go.SetActive(false);
        _slotBadges[slot] = img;
        return img;
    }

    /// <summary>ease-out-back：终点前轻微冲过头再回落，给「长出来」一点弹性。</summary>
    static float EaseOutBack(float k)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float x = k - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }
}
