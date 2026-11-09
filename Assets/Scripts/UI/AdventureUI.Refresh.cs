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
// 职责：刷新与章节外观（RefreshAll 系列、世界地图入口、左滑引导、地图层与章节背景）
public partial class AdventureUI
{

    // ────────────────────────────────────────────────────
    // 刷新
    // ────────────────────────────────────────────────────

    void RefreshAll()
    {
        HideTip();
        RefreshAvailableChapters();
        RefreshModeHighlight();
        RefreshDiffHighlight();
        RefreshChapterChrome();
        RefreshDetailPanel();
        RefreshContentLock();
        RefreshChestNodes();   // 章内 10 小关宝箱节点（2026-10-22 新增，见 AdventureUI.ChestNodes.cs）
        RefreshBranchIslands();   // 主线窗口旁的支线岛屿宝箱（2026-10-23 主人拍板，见 AdventureUI.BranchIslands.cs）
        RefreshFinaleRow();       // 活动副本区「追查真相」横条（2026-10-23 终章追查线，见 AdventureUI.Finale.cs）
        // 活动副本横条状态（2026-11-01 主人拍板：全部显示/未开放灰显/开放的排上面未开放的排下面/选中节点点亮）；
        // 切战斗标签来回也会刷到 → 选中状态不丢。非活动模式时列表节点隐藏，刷了也无副作用。
        if (IsActivityMode(_selectedMode))
        {
            EnsureActivityRowsWired();
            ApplyActivityRowStates();
        }
        // 无限页层数牌 + 虚拟排行榜（2026-10-24 主人拍板，见文末 RiftPage 区）；
        // 只在无限模式刷，别的模式零副作用。
        if (IsEndlessMode(_selectedMode))
            RefreshRiftPage();
    }

    /// <summary>按路线表重算可选章节，并把 _selectedChapter 修正到集合内。</summary>
    void RefreshAvailableChapters()
    {
        // 【2026-10-23 合并方案】补掷入口：branchRoll 未派满且当前派定岛已通关 → 首次进冒险页补掷一次
        // （EnsureForkRoll 内部幂等，绝不重复掷；data 取不到传 null 由函数内部兜底）。
        var data = SaveSystem.Instance?.Data;
        // 难度可选集合只对**主线**生效：活动副本的难度是「逐级解锁」，仍按普通章节集合走。
        bool useDiffList = IsMainMode(_selectedMode) && _selectedDiff > 0;
        // 【2026-11-05 难度隔离】支线派岛只属于普通：难度模式下只查进度，不掷点、不写档。
        if (_selectedDiff <= 0) ChapterRouteTable.EnsureForkRoll(data);
        _availableChapters.Clear();
        var list = useDiffList
            ? ChapterRouteTable.AvailableChaptersAtDiff(_selectedDiff, data)
            : ChapterRouteTable.AvailableChapters(data);
        if (list != null)
        {
            for (int i = 0; i < list.Count; i++)
                _availableChapters.Add(list[i]);
        }
        if (_availableChapters.Count == 0)
            _availableChapters.Add(1);

        // 当前选择不在可选集合内（例如刚通关了最末章、或选中了一支未派定的支线）→
        // 落到「应推进的章节」：可选里第一个未通关，全通关回最后一章（详见 ChapterRouteTable.CurrentChapterToAdvance）。
        // 【2026-11-05 难度落位】难度模式必须用该难度的集合与真源，否则切难度后任何 RefreshAll
        // 都会把选中篇拉回普通篇（自检还会误报）。
        if (!_availableChapters.Contains(_selectedChapter))
            _selectedChapter = useDiffList
                ? ChapterRouteTable.CurrentChapterToAdvanceAtDiff(_selectedDiff, data)
                : ChapterRouteTable.CurrentChapterToAdvance(data);
    }

    /// <summary>分叉点提示：支线可选时列出地图名（不露章节号）。</summary>
    string BuildBranchHint()
    {
        var data = SaveSystem.Instance?.Data;
        var choices = ChapterRouteTable.BranchChoices(data);
        if (choices == null || choices.Count < 2) return "";

        var names = new System.Collections.Generic.List<string>();
        for (int i = 0; i < choices.Count; i++)
            names.Add(GameConfig.GetChapterMapName(choices[i]));
        return "可换路线：" + string.Join(" / ", names.ToArray());
    }

    /// <summary>_selectedChapter 在可选集合里的下标（找不到返回 0）。</summary>
    int AvailableIndex()
    {
        int idx = _availableChapters.IndexOf(_selectedChapter);
        return idx < 0 ? 0 : idx;
    }

    void RefreshModeHighlight()
    {
        for (int i = 0; i < modeButtons.Length; i++)
        {
            if (modeButtons[i] == null) continue;
            SetChildSelected(modeButtons[i].transform, i == _selectedMode);
            ApplyModeTabLock(modeButtons[i], i);   // 锁态灰显（2026-10-24 主人拍板：裂隙/活动 通1章后解锁）
        }
    }

    Sprite[] _diffSprites;

    void RefreshDiffHighlight()
    {
        bool playable = IsModePlayable(_selectedMode);
        if (_diffSprites == null)
        {
            _diffSprites = new Sprite[difficultyButtons.Length];
            for (int i = 0; i < difficultyButtons.Length; i++)
            {
                if (difficultyButtons[i] == null) continue;
                _diffSprites[i] = difficultyButtons[i].GetComponent<Image>()?.sprite;
            }
        }
        for (int i = 0; i < difficultyButtons.Length; i++)
        {
            if (difficultyButtons[i] == null) continue;
            bool unlocked = playable && IsDiffUnlockedForSelectedMode(i);
            bool on = playable && i == _selectedDiff && unlocked;
            // 2026-11-08 主人拍板：选中统一用普通绿图
            var img = difficultyButtons[i].GetComponent<Image>();
            if (img != null)
            {
                if (on && _diffSprites[0] != null) img.sprite = _diffSprites[0];
                else if (!on && i == 0 && _diffSprites[1] != null) img.sprite = _diffSprites[1];
                else if (!on && i != 0 && _diffSprites[i] != null) img.sprite = _diffSprites[i];
            }
            var selImg = difficultyButtons[i].transform.Find("选中")?.GetComponent<Image>();
            if (selImg != null && _diffSprites[0] != null) selImg.sprite = _diffSprites[0];
            SetChildSelected(difficultyButtons[i].transform, on);
            SetGraphicDim(difficultyButtons[i].transform, !unlocked);
        }
    }

    static void SetChildSelected(Transform btn, bool on)
    {
        var sel = btn.Find("选中");
        if (sel == null) return;
        sel.gameObject.SetActive(on);
    }

    void RefreshChapterChrome()
    {
        if (chapterTitle != null)
        {
            // 玩家看不到「第X章」：只显示地图名
            // 【2026-10-31 主人拍板「只要章节名字 不要后面的」】B2 曾在大关进度后拼「第 N/M 座」，
            // 标题栏宽 238（ChapterTitle 锚 0.1~0.9 × 298.42）、30 号字一行只放 ~7.9 汉字 → 断行后
            // 只剩「暮影森林　第」半截。主人明确只要名字，进度整段删除（要进度改挂别处，别再往标题塞）。
            string name = GameConfig.GetChapterMapName(_selectedChapter);
            string branchHint = BuildBranchHint();
            chapterTitle.text = string.IsNullOrEmpty(branchHint) ? name : name + "\n" + branchHint;
        }

        int idx = AvailableIndex();
        if (prevChapterBtn != null)
            prevChapterBtn.interactable = _availableChapters.Count > 1;
        if (nextChapterBtn != null)
            nextChapterBtn.interactable = _availableChapters.Count > 1;
        _ = idx; // 保留：后续若要改成非循环箭头可用

        // MapBg 是内容区底框，不整页盖死大厅。章节地图在 StageNodes/Node_1..8。
        Sprite overrideBg = GetChapterBackground(_selectedChapter);
        if (overrideBg != null && mapBg != null)
            mapBg.sprite = overrideBg;
        SoftenMapBgOverHall();

        RefreshMapLayers();
        EnsureWorldMapEntry();
        RefreshWorldMapGuide();
    }

    // ────────────────────────────────────────────────────
    // 世界地图入口 + 滑动翻章引导（2026-10-17 主人拍板：章末不再弹「回城/下一章」二选一，
    // 下一章由世界地图第二地块或本页左滑进入）
    // ────────────────────────────────────────────────────

    Button _worldMapBtn;
    RectTransform _wmGuide;
    Coroutine _wmGuideAnim;
    /// <summary>「点击世界地图选章节」引导只提示一次，点开世界地图即永久关闭（2026-10-24 主人拍板）。</summary>
    const string WorldMapGuideDoneKey = "adventure.guide.worldmap.done";

    // 顶部体力面板已删（2026-10-18 主人拍板：与大厅共享顶栏的「回满」倒计时重叠，
    // 冒险页不再自建一份，统一走 GuildHallUI 刷新的大厅 TopBar 那份）

    /// <summary>
    /// 世界地图入口显隐：按 WorldMapPortal.ShouldAppear()（已通关第 1 章）控制。
    /// 两个入口（预制体 RightContent/WorldMapBtn 与标题栏代码生成那颗）一并处理；
    /// 点击回调 OnOpenWorldMap（WorldMapPopup.Show(OnWorldMapEnter)）原样保留。
    /// </summary>
    void EnsureWorldMapEntry()
    {
        // 【2026-10-22 主人拍板】世界地图入口只在主线冒险模式显示（裂隙等其它模式一概隐藏）。
        // 【2026-10-23 主人新布局】入口节点搬到 RightContent/主线 下。
        // 【2026-10-31】门槛改走 FeatureUnlock.WorldMapOpen（第 1 座大关通关 = D1）——它是主线导航，
        // B2 后若还挂「通第 1 章」= 50 小关，首日连地图都打不开。
        bool show = IsMainMode(_selectedMode)
                    && FeatureUnlock.WorldMapOpen(SaveSystem.Instance?.Data);
        // 【2026-11-01 主人拍板】世界地图点击无反应 + 入口字挡图，先整体隐藏（要恢复删掉下一行即可）
        show = false;
        var pre = transform.Find("RightContent/" + MainNodeName + "/WorldMapBtn");
        if (pre == null) pre = transform.Find("RightContent/WorldMapBtn");   // 旧布局残留路径
        if (pre != null) pre.gameObject.SetActive(show);

        if (_worldMapBtn != null) _worldMapBtn.gameObject.SetActive(show);
    }

    void OnOpenWorldMap()
    {
        // 【2026-10-24 主人拍板】把当前难度与「该难度下应推进的章」带进世界地图：
        // 地块按难度过滤灰锁、当前章地块挂头像+箭头标识；进入后回冒险页选章（不再直接开战）。
        var data = SaveSystem.Instance?.Data;
        WorldMapPopup.Show(OnWorldMapEnter, _selectedDiff,
            ChapterRouteTable.CurrentChapterToAdvanceAtDiff(_selectedDiff, data));
        MarkWorldMapGuideDone();
    }

    /// <summary>世界地图入口解锁后，在按钮下挂「点击世界地图」脉冲引导；点开一次永久关闭。</summary>
    void RefreshWorldMapGuide()
    {
        bool need = IsMainMode(_selectedMode)
                    && FeatureUnlock.WorldMapOpen(SaveSystem.Instance?.Data)
                    && !PlayerPrefs.HasKey(WorldMapGuideDoneKey);
        if (!need)
        {
            if (_wmGuide != null) _wmGuide.gameObject.SetActive(false);
            return;
        }
        var btnRt = FindWorldMapBtnRt();
        if (btnRt == null) return;
        if (_wmGuide == null) _wmGuide = BuildWorldMapGuideNode();
        if (_wmGuide == null) return;
        if (_wmGuide.parent != btnRt) _wmGuide.SetParent(btnRt, false);
        _wmGuide.gameObject.SetActive(true);
        if (_wmGuideAnim != null) StopCoroutine(_wmGuideAnim);
        _wmGuideAnim = StartCoroutine(CoWmGuidePulse());
    }

    /// <summary>世界地图按钮节点唯一出口（预制体新布局 → 旧布局残留 → 代码生成那颗）。</summary>
    RectTransform FindWorldMapBtnRt()
    {
        var pre = transform.Find("RightContent/" + MainNodeName + "/WorldMapBtn") as RectTransform;
        if (pre == null) pre = transform.Find("RightContent/WorldMapBtn") as RectTransform;
        if (pre == null && _worldMapBtn != null) pre = _worldMapBtn.transform as RectTransform;
        return pre;
    }

    /// <summary>引导节点挂在按钮下（跟随按钮摆位，不猜绝对坐标），文字摆按钮正下方。</summary>
    RectTransform BuildWorldMapGuideNode()
    {
        var go = new GameObject("WorldMapGuide", typeof(RectTransform), typeof(Text));
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -6f);
        rt.sizeDelta = new Vector2(360f, 72f);
        var t = go.GetComponent<Text>();
        t.text = "↓ 点击世界地图\n选择要挑战的章节";
        t.fontSize = 26;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color(1f, 0.92f, 0.6f, 1f);
        t.raycastTarget = false;   // 不吃射线：点引导文字照样点到下面的按钮
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.font = GameFonts.GetChinese();
        return rt;
    }

    IEnumerator CoWmGuidePulse()
    {
        while (_wmGuide != null && _wmGuide.gameObject.activeSelf)
        {
            var t = _wmGuide.GetComponent<Text>();
            if (t != null)
            {
                float a = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f);
                var c = t.color; c.a = a; t.color = c;
            }
            yield return null;
        }
        _wmGuideAnim = null;
    }

    void MarkWorldMapGuideDone()
    {
        if (!PlayerPrefs.HasKey(WorldMapGuideDoneKey))
        {
            PlayerPrefs.SetInt(WorldMapGuideDoneKey, 1);
            PlayerPrefs.Save();
        }
        if (_wmGuide != null && _wmGuide.gameObject.activeSelf)
            _wmGuide.gameObject.SetActive(false);
    }

    /// <summary>大厅当底时：MapBg 不做全屏偏左 Envelope；仅内容区等比居中。</summary>
    void SoftenMapBgOverHall()
    {
        if (mapBg == null) return;
        var rt = mapBg.rectTransform;
        if (rt == null) return;

        // 若 MapBg 几乎铺满整页（盖住大厅），改为半透明以免「换底」
        var page = transform as RectTransform;
        if (page != null && page.rect.height > 1f)
        {
            float fill = rt.rect.height / Mathf.Max(1f, page.rect.height);
            if (fill > 0.85f && rt.parent == transform)
            {
                var c = mapBg.color;
                c.a = Mathf.Min(c.a, 0.35f);
                mapBg.color = c;
                return;
            }
        }

        // 内容区：中心锚点 + Envelope，避免长屏偏左
        var parent = rt.parent as RectTransform;
        if (parent == null) return;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        float pw = Mathf.Max(1f, parent.rect.width);
        float ph = Mathf.Max(1f, parent.rect.height);
        rt.sizeDelta = new Vector2(pw, ph);
        mapBg.preserveAspect = false;
        var fitter = mapBg.GetComponent<AspectRatioFitter>();
        if (fitter == null) fitter = mapBg.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        var sp = mapBg.sprite;
        if (sp != null)
            fitter.aspectRatio = sp.rect.width / Mathf.Max(1f, sp.rect.height);
        else
            fitter.aspectRatio = GameConfig.DESIGN_WIDTH / GameConfig.DESIGN_HEIGHT;
    }

    /// <summary>StageNodes 下预制体自带的整图节点名（主人摆的，别动名字）。</summary>
    const string RiftMapNodeName = "无限裂隙";

    /// <summary>RightContent 下页面节点显隐（2026-10-23 主人新布局：主线/无限/活动副本 三节点）。</summary>
    void SetRightNodeVisible(string nodeName, bool on)
    {
        var right = transform.Find("RightContent");
        var node = right != null ? right.Find(nodeName) : null;
        if (node == null)
        {
            if (on)
                Debug.LogError($"[AdventureUI] RightContent 下找不到「{nodeName}」节点（预制体缺失），该模式页面无从显示");
            return;
        }
        if (node.gameObject.activeSelf != on)
            node.gameObject.SetActive(on);
    }

    void RefreshMapLayers()
    {
        if (stageNodeContainer == null) return;
        // 【2026-10-23 主人新布局】点左侧标签 → 右侧出对应节点 + 根下 StageNodes 出对应背景：
        //   主线   → RightContent/主线   + StageNodes/Node_{ch}
        //   无限   → RightContent/无限   + StageNodes/无限裂隙
        //   活动副本 → RightContent/活动副本 + StageNodes/金币副本（金币/徽记横条共用此背景）
        bool mainMode = IsMainMode(_selectedMode);
        bool riftMode = IsEndlessMode(_selectedMode);
        bool activityMode = IsActivityMode(_selectedMode);

        SetRightNodeVisible(MainNodeName, mainMode);
        SetRightNodeVisible(EndlessNodeName, riftMode);
        SetRightNodeVisible(ActivityNodeName, activityMode);

        Transform riftNode = null;
        Transform activityBgNode = null;
        for (int i = 0; i < stageNodeContainer.childCount; i++)
        {
            var child = stageNodeContainer.GetChild(i);
            if (child.name == RiftMapNodeName)
            {
                riftNode = child;
                continue;
            }
            if (child.name == ActivityBgNodeName)
            {
                activityBgNode = child;
                if (_activityBgImg == null) _activityBgImg = child.GetComponent<Image>();
                continue;
            }
            int ch = ParseNodeChapter(child.name, i + 1);
            child.gameObject.SetActive(mainMode && ch == _selectedChapter);
        }
        if (riftNode != null)
            riftNode.gameObject.SetActive(riftMode);
        else if (riftMode)
            Debug.LogError($"[AdventureUI] StageNodes 下找不到「{RiftMapNodeName}」节点（预制体缺失），裂隙地图无从显示");
        if (activityBgNode != null)
        {
            activityBgNode.gameObject.SetActive(activityMode);
            // 活动副本背景换「活动」图（2026-10-23 主人拍板）：prefab 该节点沿用 Node_8 雪山图未适配，
            // 活动图 meta 是 56 字符 guid 不进 prefab 引用 → 按路径加载设图（只改 sprite，不动排版）。
            if (activityMode && _activityBgImg != null)
            {
                if (_activityBgSprite == null)
                    _activityBgSprite = Resources.Load<Sprite>(ActivityBgSpritePath);
                if (_activityBgSprite != null)
                {
                    if (_activityBgImg.sprite != _activityBgSprite)
                        _activityBgImg.sprite = _activityBgSprite;
                }
                else
                {
                    Debug.LogError($"[AdventureUI] 活动副本背景图加载失败：Resources/{ActivityBgSpritePath}（缺真值不兜底）");
                }
            }
        }
        else if (activityMode)
            Debug.LogError($"[AdventureUI] StageNodes 下找不到「{ActivityBgNodeName}」节点（预制体缺失），活动背景无从显示");
    }

    static int ParseNodeChapter(string name, int fallback)
    {
        if (string.IsNullOrEmpty(name)) return fallback;
        int us = name.LastIndexOf('_');
        if (us >= 0 && us + 1 < name.Length && int.TryParse(name.Substring(us + 1), out int n) && n >= 1 && n <= 8)
            return n;
        if (int.TryParse(name, out n) && n >= 1 && n <= 8)
            return n;
        return fallback;
    }

        Sprite GetChapterBackground(int chapter)
        {
            int idx = chapter - 1;
            if (chapterBackgrounds != null && idx >= 0 && idx < chapterBackgrounds.Length && chapterBackgrounds[idx] != null)
                return chapterBackgrounds[idx];
            // 统一从 Resources/UI/Adventure 尝试加载 chapter_1 … chapter_8
            return UiKeyedBackgrounds.Load(UiKeyedBackgrounds.AdventurePages, "chapter_" + chapter)
                   ?? UiKeyedBackgrounds.Load(UiKeyedBackgrounds.AdventurePages, GameConfig.GetChapterMapName(chapter));
        }

    // ────────────────────────────────────────────────────
    // 无限页：层数牌 + 虚拟排行榜（2026-10-24 主人拍板）
    // ① 「无限关卡不要设关卡号 只按层数来 第一层 第二层这种 没有上限」——
    //    无限/关卡 下 4 张槽位牌（与主线同构，prefab 烤死文案「1-1」）改写「第N层」：
    //    底槽 = 下次起始层（记录点+1，EndlessRiftRules.StartStage 唯一出口），往上逐层 +1；
    //    层数跟着存档进度走，永远没有「最后一关」。
    // ② 虚拟排行榜（无真实服务器，公式在 EndlessRiftRules.RankPercentOf）：
    //    一行字挂在「关卡」容器里跟无限页一起显示，「您当前层数已领先 X% 的玩家」；
    //    打得越多百分比越高，隔几天不玩就回落（闲置衰减，锚点=真实进本日键）。
    // ────────────────────────────────────────────────────

    /// <summary>排行榜文字节点名（挂在 无限/关卡 容器下；不是「数字-数字」不会混进槽位识别）。</summary>
    const string RiftRankLabelName = "虚拟排行";
    /// <summary>排行榜文字与顶槽位的纵向关系（2026-11-09 主人拍板整体往上 30）：y 跟顶槽现算，不写死坐标。补第 5 槽后顶槽升到 bandTop，再往上过多会撞 TitleBar。</summary>
    const float RiftRankLabelGapY = 30f;
    /// <summary>排行榜半透底框节点名（不是「数字-数字」不会混进槽位识别）。</summary>
    const string RiftRankFrameName = "虚拟排行底框";

    /// <summary>刷新无限页：5 张层数牌写「第N层」+ 出生点头像指示 + 两点排行榜（RefreshAll 调，仅无限模式）。</summary>
    void RefreshRiftPage()
    {
        // 2026-11-08 主人拍板：右边缘 x 要用 right.rect.xMax 运行时换算，显式转 RectTransform
        var right = transform.Find("RightContent") as RectTransform;
        var endless = right != null ? right.Find(EndlessNodeName) : null;
        var guanka = endless != null ? endless.Find("关卡") as RectTransform : null;
        if (guanka == null)
        {
            Debug.LogError("[AdventureUI] RightContent/无限/关卡 节点缺失，层数牌与虚拟排行榜无从显示（缺真值不兜底）");
            return;
        }

        EnsureRiftSlotCount(guanka, GameConfig.STAGES_PER_GRAND);
        var slots = CollectNumberedSlots(guanka);
        // 【2026-11-02 晚·直达预览】进本确认里点了「直达第 N 层」后，层数牌窗口停在预览起点
        //（N 层），让玩家在二次确认期间亲眼看到「自己要跳到哪」。弹窗已关（点暗底直接关掉、
        // 或回城刷新）→ 预览失效，回到真实起始层窗口。真实路径都有主动 Restore，这里只兜暗底。
        if (_riftPreviewStart > 0 && !StaminaConfirmPopup.IsOpen)
            _riftPreviewStart = 0;
        int start = _riftPreviewStart > 0 ? _riftPreviewStart : EndlessRiftRules.StartStage();
        ApplyRiftPlateNumbers(slots, start);

        // 虚拟排行榜：与最上面那个岛右侧并排、整体上移 30（2026-11-09；瘦屏重排后 RefreshRiftPage 会重算，不脱节）
        var data = SaveSystem.Instance?.Data;
        var label = EnsureRiftRankLabel(guanka);
        if (label != null)
        {
            // 【2026-11-02 主人拍板】排行榜要「两点」：① 打的最高层是多少 ② 超过了多少人。
            // 最高层走生涯最高（bestStage，只增不减）—— 重置进度也不清，永久保留。
            int best = EndlessRiftRules.BestStage(data);
            // 【2026-11-02 晚·主人拍板「都可以」】百分比 ±1 微波动：固定数字显得假，虚拟榜每次进页
            // 略有浮动更像活的榜单（纯展示，不动 RankPercentOf 真源）。
            int pct = Mathf.Clamp(EndlessRiftRules.RankPercentOf(data) + UnityEngine.Random.Range(-1, 2), 0, 100);
            // 【2026-11-09 主人拍板】三行：新纪录 / 最高第N层 / 超过X%的玩家；best==0 仍显示「未挑战」。
            // 新纪录是条件首行（打破生涯纪录 → 保留到下次进本，TryConsumeEnter 清账），金色沿用原暖金。
            string recordLine = EndlessRiftRules.NewRecordPendingShow && best > 0
                ? "<color=#FFD97A>新纪录</color>\n"
                : "";
            label.text = best > 0
                ? recordLine + $"最高第{best}层\n超过{pct}%的玩家"
                : "未挑战";
            ApplyRiftRankLabelSize(label);   // 文案定稿后才量宽度（黑底跟着收缩）
            if (slots.Count > 0)
            {
                var top = slots[slots.Count - 1];
                var lrt = label.rectTransform;
                // 2026-11-08 主人拍板：x 不写死，取 RightContent 右边缘(留 12)现算成「关卡」局部坐标；y 在顶槽基础上上移 30
                float rightEdgeLocalX = guanka.InverseTransformPoint(
                    right.TransformPoint(new Vector3(right.rect.xMax - 12f, 0f, 0f))).x;
                lrt.anchoredPosition = new Vector2(rightEdgeLocalX, top.anchoredPosition.y + RiftRankLabelGapY);
            }
            // 【2026-11-08 主人拍板】label 位置定稿后再复制几何给半透底框，保证与文字同框
            EnsureRiftRankFrame(guanka, label);
        }

        // 【2026-11-02 主人拍板】玩家头像指向所在的层（= 出生点 = 底槽 = 保存点 + 1）。
        // 直达预览期间底槽位置不变（5 张牌是固定布局），箭头跟着预览窗口的底槽 —— 「一直指向」。
        UpdateRiftStageMarker(guanka, slots.Count > 0 ? slots[0] : null);
        // 【2026-11-02 晚·主人拍板「都可以」】死亡回城 → 箭头连脉冲两下，把视线引到「你从这里继续」
        //（主动撤离不置位）。读后即清：只在对账消费一次。
        if (EndlessRiftRules.PulseMarkerOnDeathReturn)
        {
            EndlessRiftRules.PulseMarkerOnDeathReturn = false;
            PlayDeathReturnPulse();
        }
        // 重置徽章跟着这一页刷（状态：本周能不能重置 / 冷却还剩几天）
        RefreshEndlessResetButton(true);
    }

    /// <summary>
    /// 无限页层数牌补足到与主线可见 5 关同位（2026-11-08 16:0x 确认改成 5 个岛）。
    /// 预制体「无限/关卡」只有 4 槽，按主线 EnsureSlotCount 同款口径克隆顶槽补第 5 槽：
    /// 名字沿用「9-9」（能被 CollectNumberedSlots 的 ^\d+-\d+$ 收进去）、x 用源槽原值不写、
    /// y 从现有相邻两槽的间距现算（纵向 y 排版属必要介入）；幂等，重复进页不重复克隆。
    /// </summary>
    void EnsureRiftSlotCount(RectTransform guanka, int want)
    {
        want = Mathf.Min(want, GameConfig.STAGES_PER_GRAND);
        var slots = CollectNumberedSlots(guanka);
        if (slots.Count >= want) return;
        if (slots.Count < 2)
        {
            Debug.LogError("[AdventureUI] 无限/关卡 槽位不足 2 个，现算不出节奏，不补第 5 槽（缺真值不兜底）");
            return;
        }
        float rhythm = slots[1].anchoredPosition.y - slots[0].anchoredPosition.y;
        if (rhythm <= 1f)
        {
            Debug.LogError("[AdventureUI] 无限/关卡 槽位节奏异常，不补第 5 槽（缺真值不兜底）");
            return;
        }
        while (slots.Count < want)
        {
            var top = slots[slots.Count - 1];
            var clone = Instantiate(top, guanka) as RectTransform;
            if (clone == null)
            {
                Debug.LogError("[AdventureUI] 无限/关卡 克隆第 5 槽失败（缺真值不兜底）");
                return;
            }
            clone.name = "9-9";
            var p = top.anchoredPosition;   // x 取源槽原值，只写 y（铁律 20）
            clone.anchoredPosition = new Vector2(p.x, p.y + rhythm);
            slots.Add(clone);
        }
    }

    /// <summary>
    /// 排行榜文字框按文案实际宽高收缩（2026-11-08 16:0x「黑底也收缩一下」）：
    /// label 几何的唯一出口，黑底框由 EnsureRiftRankFrame 从这里现读复制，不另开位置出口。
    /// 必须在 label.text 完全定稿之后调用（preferredWidth/Height 要读到最终文本）；
    /// 高度按内容算，避免三行（新纪录 / 最高层 / 超过玩家）被固定高度截断。
    /// </summary>
    static void ApplyRiftRankLabelSize(Text label)
    {
        float w = Mathf.Clamp(label.preferredWidth + 20f, 96f, 240f);
        float h = Mathf.Clamp(label.preferredHeight + 8f, 48f, 104f);  // 三行（新纪录/最高层/超过玩家）也不被截断
        label.rectTransform.sizeDelta = new Vector2(w, h);
    }

    /// <summary>存档点编号的金色（10 的倍数的层）；Rich gold 与排行榜角标同一颗暖金。</summary>
    static readonly Color RiftSaveStageColor = new Color(1f, 0.85f, 0.42f, 1f);
    /// <summary>非存档层用预制体原色 —— 颜色只捕获一次（首次刷新时牌还是预制体原值，之后只读不写）。</summary>
    static Color? _riftPlateBaseColor;

    /// <summary>
    /// 5 张层数牌写编号（底槽 = start，往上 +1；从 RefreshRiftPage 抽出，直达预览翻滚也走这里，
    /// 保证两条路写出的牌面完全一致）。
    /// 【2026-11-02 晚·主人拍板「都可以」】10 的倍数（保存点）编号变金色 —— 翻滚扫过 20/30/40 时
    /// 存档点一颗颗闪过，玩家对「打到这张牌就能存档」建立直觉。非存档层每次刷回预制体原色。
    /// </summary>
    void ApplyRiftPlateNumbers(List<RectTransform> slots, int start)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            // prefab 实测：编号牌 = 槽位/bg/Text (Legacy)（fontSize 30、烤死「1-1」；
            // 取法与主线槽位 ChestNodes.cs:202 同款路径）。只改文案，牌的图/位置不动
            var plate = slots[i].Find("bg/Text (Legacy)")?.GetComponent<Text>();
            if (plate == null) continue;
            if (!_riftPlateBaseColor.HasValue) _riftPlateBaseColor = plate.color;   // 首次捕获预制体真值
            plate.text = $"第{start + i}层";   // 底槽=起始层，往上 +1；无限层永不封顶
            // 层数无上限（三位数层号比烤死的「1-1」宽，牌面 125px 装不下会折行）→ 溢出展开防折行。
            // 只改文字展示属性，不动排版（铁律 20 例外口径：文案/字体类）。
            plate.horizontalOverflow = HorizontalWrapMode.Overflow;
            plate.color = (start + i) % GameConfig.ENDLESS_SAVE_INTERVAL == 0
                ? RiftSaveStageColor
                : _riftPlateBaseColor.Value;
        }
    }

    /// <summary>排行榜文字（运行时建一次；纯 Text 无新图，颜色对齐页内暖金奶油口径）。
    /// 【2026-11-02 两点改版】排版属性每次刷新都重写一遍（幂等）—— 老节点若是单行口径的旧尺寸，
    /// 进页即被纠正成按内容收缩的框，不用重启。</summary>
    Text EnsureRiftRankLabel(RectTransform guanka)
    {
        var found = guanka.Find(RiftRankLabelName);
        Text txt = found != null ? found.GetComponent<Text>() : null;
        if (txt == null)
        {
            if (guanka.GetComponentInParent<Canvas>() == null)
            {
                Debug.LogError("[AdventureUI] 无限/关卡 容器不在 Canvas 下，虚拟排行榜建不出来（缺真值不兜底）");
                return null;
            }
            var go = new GameObject(RiftRankLabelName, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(guanka, false);
            txt = go.GetComponent<Text>();
            txt.font = GameFonts.GetChinese();
            txt.color = new Color(1f, 0.92f, 0.7f, 1f);   // 与怪物信息弹窗标题同款暖金奶油色
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.raycastTarget = false;
        }
        // 【2026-11-08 主人拍板】锚点回到 (0.5,0.5) 与槽位同一坐标参照（关卡容器中心）：
        // 「关卡」是 100×100 小容器、右缘只到屏幕 x≈521，锚其右缘 (1,0.5) 反让文字往左跑约 66px；
        // 真正右侧靠边的 x 改由 RefreshRiftPage 用 RightContent 右边缘运行时换算（此处不写死坐标）。
        // pivot 右下、fontSize/alignment 保持不变；框尺寸由 ApplyRiftRankLabelSize 在文案定稿后按实际宽度收缩
        //（此处不写 sizeDelta，避免同一值两处出口；黑底框由 EnsureRiftRankFrame 现读复制跟着收缩）。
        var rt = txt.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);   // 与槽位同一坐标参照（关卡容器中心）
        rt.pivot = new Vector2(1f, 0f);
        txt.fontSize = 24;
        txt.alignment = TextAnchor.MiddleCenter;
        return txt;
    }

    /// <summary>排行榜纯色半透底框（运行时建一次；纯 Image 无新图）。
    /// 【2026-11-08 主人拍板】文字右靠边后加纯黑半透底框衬底，几何每次从 label 现读复制（label 唯一位置出口）。</summary>
    Image EnsureRiftRankFrame(RectTransform guanka, Text label)
    {
        var found = guanka.Find(RiftRankFrameName);
        Image img = found != null ? found.GetComponent<Image>() : null;
        if (img == null)
        {
            var go = new GameObject(RiftRankFrameName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(guanka, false);
            go.transform.SetSiblingIndex(label.transform.GetSiblingIndex());   // 排在 label 之前 → 画在文字下面，不盖字
            img = go.GetComponent<Image>();
            img.raycastTarget = false;                          // 不吃点击，别挡层节点
            img.color = new Color(0f, 0f, 0f, 0.5f);            // 纯黑半透（sprite 保持 null、type 不写）
        }
        // 每次刷新从 label 现读复制 5 个几何参数：label 定稿后调用，底框与文字同框不脱节
        var src = label.rectTransform;
        var dst = img.rectTransform;
        dst.anchorMin = src.anchorMin;
        dst.anchorMax = src.anchorMax;
        dst.pivot = src.pivot;
        dst.anchoredPosition = src.anchoredPosition;
        dst.sizeDelta = src.sizeDelta;
        return img;
    }
}
