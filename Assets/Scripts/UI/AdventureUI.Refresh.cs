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
    }

    /// <summary>按路线表重算可选章节，并把 _selectedChapter 修正到集合内。</summary>
    void RefreshAvailableChapters()
    {
        _availableChapters.Clear();
        var data = SaveSystem.Instance?.Data;
        var list = ChapterRouteTable.AvailableChapters(data);
        if (list != null)
        {
            for (int i = 0; i < list.Count; i++)
                _availableChapters.Add(list[i]);
        }
        if (_availableChapters.Count == 0)
            _availableChapters.Add(1);

        if (!_availableChapters.Contains(_selectedChapter))
            _selectedChapter = _availableChapters[_availableChapters.Count - 1];
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
            bool unlocked = playable && IsDiffUnlocked(i);
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
        RefreshChapterSwipeGuide();
    }

    // ────────────────────────────────────────────────────
    // 世界地图入口 + 滑动翻章引导（2026-10-17 主人拍板：章末不再弹「回城/下一章」二选一，
    // 下一章由世界地图第二地块或本页左滑进入）
    // ────────────────────────────────────────────────────

    Button _worldMapBtn;
    RectTransform _swipeGuide;
    Coroutine _swipeGuideAnim;
    /// <summary>「左滑前往下一章」引导只提示一次，成功滑到后续章节即永久关闭。</summary>
    const string SwipeGuideDoneKey = "adventure.guide.ch2swipe.done";

    // 顶部体力面板已删（2026-10-18 主人拍板：与大厅共享顶栏的「回满」倒计时重叠，
    // 冒险页不再自建一份，统一走 GuildHallUI 刷新的大厅 TopBar 那份）

    /// <summary>
    /// 【2026-10-21 主人拍板】世界地图入口暂停显示（地块状态/点击链路未理顺前先不露出来）。
    /// 隐藏是唯一出口：预制体 RightContent/WorldMapBtn 与标题栏代码生成那颗，两个入口一律关掉。
    /// 恢复显示时改回读 WorldMapPortal.ShouldAppear() 即可（点击回调 OnOpenWorldMap 原样保留）。
    /// </summary>
    void EnsureWorldMapEntry()
    {
        var pre = transform.Find("RightContent/WorldMapBtn");
        if (pre != null && pre.gameObject.activeSelf)
            pre.gameObject.SetActive(false);

        if (_worldMapBtn != null && _worldMapBtn.gameObject.activeSelf)
            _worldMapBtn.gameObject.SetActive(false);
    }

    void OnOpenWorldMap()
    {
        WorldMapPopup.Show(OnWorldMapEnter);
    }

    /// <summary>通关第 1 章后，在地图区底部提示左滑翻章；引导完成后永久隐藏。</summary>
    void RefreshChapterSwipeGuide()
    {
        bool need = _selectedChapter <= 1
            && SaveSystem.Instance != null && SaveSystem.Instance.Data != null
            && SaveSystem.Instance.Data.HasClearedChapter(1)
            && !PlayerPrefs.HasKey(SwipeGuideDoneKey);
        if (!need)
        {
            if (_swipeGuide != null) _swipeGuide.gameObject.SetActive(false);
            return;
        }
        if (_swipeGuide == null) _swipeGuide = BuildSwipeGuideNode();
        if (_swipeGuide == null) return;
        _swipeGuide.gameObject.SetActive(true);
        if (_swipeGuideAnim != null) StopCoroutine(_swipeGuideAnim);
        _swipeGuideAnim = StartCoroutine(CoSwipeGuidePulse());
    }

    RectTransform BuildSwipeGuideNode()
    {
        var right = transform.Find("RightContent");
        var mapRoot = right != null ? right.Find("MapRoot") : transform.Find("MapRoot");
        if (mapRoot == null) return null;
        var go = new GameObject("ChapterSwipeGuide", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(mapRoot, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 40f);
        rt.sizeDelta = new Vector2(420f, 48f);
        var t = go.GetComponent<Text>();
        t.text = "←  左滑前往下一章";
        t.fontSize = 30;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color(1f, 0.92f, 0.6f, 1f);
        return rt;
    }

    IEnumerator CoSwipeGuidePulse()
    {
        while (_swipeGuide != null && _swipeGuide.gameObject.activeSelf)
        {
            var t = _swipeGuide.GetComponent<Text>();
            if (t != null)
            {
                float a = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f);
                var c = t.color; c.a = a; t.color = c;
            }
            yield return null;
        }
        _swipeGuideAnim = null;
    }

    void MarkSwipeGuideDone()
    {
        if (!PlayerPrefs.HasKey(SwipeGuideDoneKey))
        {
            PlayerPrefs.SetInt(SwipeGuideDoneKey, 1);
            PlayerPrefs.Save();
        }
        if (_swipeGuide != null && _swipeGuide.gameObject.activeSelf)
            _swipeGuide.gameObject.SetActive(false);
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

    void RefreshMapLayers()
    {
        if (stageNodeContainer == null) return;
        for (int i = 0; i < stageNodeContainer.childCount; i++)
        {
            var child = stageNodeContainer.GetChild(i);
            int ch = ParseNodeChapter(child.name, i + 1);
            child.gameObject.SetActive(ch == _selectedChapter);
        }
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
