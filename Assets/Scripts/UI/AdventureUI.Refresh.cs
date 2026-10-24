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
        // 活动副本横条选中高亮（2026-10-23 活动一拆二；非活动模式时列表节点隐藏，刷了也无副作用）
        if (IsActivityMode(_selectedMode))
        {
            EnsureActivityRowsWired();
            RefreshActivityRowHighlight();
        }
    }

    /// <summary>按路线表重算可选章节，并把 _selectedChapter 修正到集合内。</summary>
    void RefreshAvailableChapters()
    {
        // 【2026-10-23 合并方案】补掷入口：branchRoll 未派满且当前派定岛已通关 → 首次进冒险页补掷一次
        // （EnsureForkRoll 内部幂等，绝不重复掷；data 取不到传 null 由函数内部兜底）。
        var data = SaveSystem.Instance?.Data;
        ChapterRouteTable.EnsureForkRoll(data);
        _availableChapters.Clear();
        var list = ChapterRouteTable.AvailableChapters(data);
        if (list != null)
        {
            for (int i = 0; i < list.Count; i++)
                _availableChapters.Add(list[i]);
        }
        if (_availableChapters.Count == 0)
            _availableChapters.Add(1);

        // 当前选择不在可选集合内（例如刚通关了最末章、或选中了一支未派定的支线）→
        // 落到「应推进的章节」：可选里第一个未通关，全通关回最后一章（详见 ChapterRouteTable.CurrentChapterToAdvance）。
        if (!_availableChapters.Contains(_selectedChapter))
            _selectedChapter = ChapterRouteTable.CurrentChapterToAdvance(data);
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
        }
    }

    void RefreshDiffHighlight()
    {
        bool playable = IsModePlayable(_selectedMode);
        for (int i = 0; i < difficultyButtons.Length; i++)
        {
            if (difficultyButtons[i] == null) continue;
            bool unlocked = playable && IsDiffUnlockedForSelectedMode(i);
            SetChildSelected(difficultyButtons[i].transform, playable && i == _selectedDiff && unlocked);
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
        // 【2026-10-22 主人拍板】世界地图入口只在主线冒险模式显示（裂隙等其它模式一概隐藏），
        // 原解锁条件（已通关第 1 章）保留。
        // 【2026-10-23 主人新布局】入口节点搬到 RightContent/主线 下。
        bool show = IsMainMode(_selectedMode)
                    && SaveSystem.Instance != null && SaveSystem.Instance.Data != null
                    && SaveSystem.Instance.Data.HasClearedChapter(1);
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

    /// <summary>通关第 1 章后，在世界地图入口按钮下挂「点击世界地图」脉冲引导；点开一次永久关闭。</summary>
    void RefreshWorldMapGuide()
    {
        bool need = IsMainMode(_selectedMode)
            && SaveSystem.Instance != null && SaveSystem.Instance.Data != null
            && SaveSystem.Instance.Data.HasClearedChapter(1)
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
}
