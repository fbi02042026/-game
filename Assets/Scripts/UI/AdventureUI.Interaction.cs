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
// 职责：交互逻辑（WireClicks 接线、选模式/章节/难度、滑动翻章手势、进战流程、怪物信息点击）
public partial class AdventureUI
{

    // ────────────────────────────────────────────────────
    // 交互逻辑
    // ────────────────────────────────────────────────────

    void WireClicks()
    {
        for (int i = 0; i < modeButtons.Length; i++)
        {
            if (modeButtons[i] == null) continue;
            int idx = i;
            modeButtons[i].onClick.RemoveAllListeners();
            modeButtons[i].onClick.AddListener(() => OnSelectMode(idx));
        }
        for (int i = 0; i < difficultyButtons.Length; i++)
        {
            if (difficultyButtons[i] == null) continue;
            int idx = i;
            difficultyButtons[i].onClick.RemoveAllListeners();
            difficultyButtons[i].onClick.AddListener(() => OnSelectDiff(idx));
        }
        prevChapterBtn?.onClick.RemoveAllListeners();
        nextChapterBtn?.onClick.RemoveAllListeners();
        startBtn?.onClick.RemoveAllListeners();
        sweepBtn?.onClick.RemoveAllListeners();
        chapterRewardBtn?.onClick.RemoveAllListeners();
        adventureLogBtn?.onClick.RemoveAllListeners();
        addChancesBtn?.onClick.RemoveAllListeners();

        prevChapterBtn?.onClick.AddListener(OnPrevChapter);
        nextChapterBtn?.onClick.AddListener(OnNextChapter);
        startBtn?.onClick.AddListener(OnStartBattle);
        // 扫荡 / 章节奖励未做：隐藏入口
        if (sweepBtn != null) sweepBtn.gameObject.SetActive(false);
        if (chapterRewardBtn != null) chapterRewardBtn.gameObject.SetActive(false);
        adventureLogBtn?.onClick.AddListener(OnAdventureLog);
        addChancesBtn?.onClick.AddListener(OnAddChances);

        HideUnfinishedEntries();
        BindMapLayers();
        WireEnemyIconClicks();
        WireBoxReward();
    }

    /// <summary>隐藏尚未实现的玩法入口，避免误点「即将开放」。</summary>
    void HideUnfinishedEntries()
    {
        // 模式：主线(0) + 无限裂隙(2)（2026-10-18 主人报「没看到无限裂隙」——裂隙玩法 2026-10-17 已建好，放开入口）；
        // 每日副本(1)/BOSS挑战(3)/活动副本(4) 仍隐藏
        for (int i = 0; i < modeButtons.Length; i++)
        {
            if (modeButtons[i] == null) continue;
            modeButtons[i].gameObject.SetActive(i == 0 || i == 2);
        }
        _selectedMode = 0;
        // 难度：2026-09-17「地狱」已删除，按钮只建 3 个，不再需要隐藏逻辑
        if (addChancesBtn != null)
            addChancesBtn.gameObject.SetActive(false);
    }

    /// <summary>StageNodes 下是整张章节地图（Node_1..8），不是关卡图标。关掉 Button 避免点按变色。</summary>
    void BindMapLayers()
    {
        _stageNodeBtns.Clear();
        if (stageNodeContainer == null) return;
        for (int i = 0; i < stageNodeContainer.childCount; i++)
        {
            var t = stageNodeContainer.GetChild(i);
            var btn = t.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.enabled = false;
                btn.transition = Selectable.Transition.None;
            }
            var img = t.GetComponent<Image>();
            if (img != null) img.raycastTarget = false;
        }
    }

    void WireEnemyIconClicks()
    {
        if (enemyIconContainer == null) return;
        for (int i = 0; i < enemyIconContainer.childCount; i++)
        {
            var slot = enemyIconContainer.GetChild(i);
            if (slot.name == "BOSS" || slot.name == "direnxinxi") continue;
            var btn = slot.GetComponent<Button>();
            if (btn == null) btn = slot.gameObject.AddComponent<Button>();
            int idx = ParseIconIndex(slot.name, i);
            // 【2026-10-11】把被点的图标节点一起传下去 —— 弹窗要显示在这个敌人上面
            var slotRt = slot as RectTransform;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnClickEnemyIcon(idx, slotRt));
        }
    }

    void WireBoxReward()
    {
        Transform mapBottom = null;
        var right = transform.Find("RightContent");
        var mapRoot = right != null ? right.Find("MapRoot") : null;
        if (mapRoot != null) mapBottom = mapRoot.Find("MapBottomBar");
        if (mapBottom == null) return;
        var box = mapBottom.Find("boxbg") ?? mapBottom.Find("boxbg_kelingqu");
        if (box == null) return;
        var btn = box.GetComponent<Button>() ?? box.gameObject.AddComponent<Button>();
        if (box.GetComponent<Image>() != null) btn.targetGraphic = box.GetComponent<Image>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnChapterReward);
    }

    static int ParseIconIndex(string name, int fallback)
    {
        if (string.IsNullOrEmpty(name)) return fallback;
        for (int i = name.Length - 1; i >= 0; i--)
        {
            if (char.IsDigit(name[i]))
            {
                int end = i;
                while (i >= 0 && char.IsDigit(name[i])) i--;
                if (int.TryParse(name.Substring(i + 1, end - i), out int n))
                    return n;
                break;
            }
        }
        return fallback;
    }

    void OnSelectMode(int idx)
    {
        _selectedMode = idx;
        if (!IsDiffUnlocked(_selectedDiff))
            _selectedDiff = 0;
        HideTip();
        if (!IsModePlayable(idx))
            Toast($"{ModeLabel(idx)}即将开放");
        RefreshAll();
    }

    void OnSelectChapter(int chapter)
    {
        if (chapter < 1 || chapter > 8) return;
        if (!ChapterRouteTable.CanEnter(chapter, SaveSystem.Instance?.Data))
        {
            Toast("通关当前区域后开启");
            return;
        }
        _selectedChapter = chapter;
        RefreshAll();
    }

    /// <summary>
    /// 任务跳转用（2026-09-19 新增）：把冒险页定位到指定章节。
    /// 只改选中章 + 刷新，不做其他事；章节现在进不去就静默保留当前选择，
    /// 免得从任务条点进来被一句「未解锁」打断。
    /// </summary>
    public void FocusChapter(int chapter)
    {
        if (chapter < 1 || chapter > 8) return;
        if (!ChapterRouteTable.CanEnter(chapter, SaveSystem.Instance?.Data)) return;

        _selectedChapter = chapter;
        if (_built && gameObject.activeInHierarchy) RefreshAll();
    }

    void OnSelectDiff(int idx)
    {
        if (!IsModePlayable(_selectedMode))
        {
            Toast($"{ModeLabel(_selectedMode)}即将开放");
            return;
        }
        if (!IsDiffUnlocked(idx))
        {
            // 噩梦门槛：困难难度下通关第 8 章（不再按通关地图数判定）
            if (idx >= 2) Toast("在困难难度下通关一次冒险后开启噩梦");
            else Toast($"通关 {GameConfig.DIFF_HARD_NEED_CLEARS} 张地图后开启{DiffLabel(idx)}");
            return;
        }
        _selectedDiff = idx;
        RefreshDiffHighlight();
        RefreshDetailPanel();
    }

    void OnPrevChapter()
    {
        if (_availableChapters.Count <= 1)
        {
            Toast("暂无其它区域");
            return;
        }
        int idx = AvailableIndex();
        idx = (idx - 1 + _availableChapters.Count) % _availableChapters.Count;
        _selectedChapter = _availableChapters[idx];
        RefreshAll();
    }

    void OnNextChapter()
    {
        if (_availableChapters.Count <= 1)
        {
            Toast("通关本区域后开启下一张地图");
            return;
        }
        int idx = AvailableIndex();
        idx = (idx + 1) % _availableChapters.Count;
        _selectedChapter = _availableChapters[idx];
        RefreshAll();
        // 成功滑到后续章节 → 「左滑引导」永久关闭
        if (_selectedChapter > 1) MarkSwipeGuideDone();
    }

    // ────────────────────────────────────────────────────
    // 滑动翻章（2026-10-17 主人要求）：在地图区横向拖拽切上一章/下一章
    // 事件冒泡到根：按下点在 RightContent/MapRoot 子树内才响应，其他区域忽略。
    // ────────────────────────────────────────────────────

    /// <summary>滑动翻章触发阈值（屏幕像素）；不足视为轻点/误触，不翻页。</summary>
    const float ChapterSwipeThresholdPx = 100f;

    bool _chapterSwipeActive;
    float _chapterSwipeDx;

    /// <summary>拖拽起点是否落在地图区（RightContent/MapRoot 子树）内。</summary>
    bool IsMapDragStart(PointerEventData eventData)
    {
        var hit = eventData.pointerPressRaycast.gameObject;
        if (hit == null) return false;
        var right = transform.Find("RightContent");
        var mapRoot = right != null ? right.Find("MapRoot") : transform.Find("MapRoot");
        return mapRoot != null && hit.transform.IsChildOf(mapRoot);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _chapterSwipeActive = IsMapDragStart(eventData);
        _chapterSwipeDx = 0f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_chapterSwipeActive) _chapterSwipeDx += eventData.delta.x;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_chapterSwipeActive) return;
        _chapterSwipeActive = false;
        if (Mathf.Abs(_chapterSwipeDx) < ChapterSwipeThresholdPx) return;
        if (_chapterSwipeDx < 0f) OnNextChapter();  // 向左滑 → 下一章
        else OnPrevChapter();                       // 向右滑 → 上一章
    }

    void OnChapterReward() => Toast("章节奖励（即将开放）");
    void OnAdventureLog()
    {
        var hub = TownHubController.Instance;
        if (hub != null) hub.OpenAdventureLog();
        else Toast("请从底栏打开冒险日志");
    }
    void OnAddChances()    => Toast("主线不限次数");

    void OnStartBattle()
    {
        if (!IsModePlayable(_selectedMode))
        {
            Toast($"{ModeLabel(_selectedMode)}即将开放");
            return;
        }
        if (!IsDiffUnlocked(_selectedDiff))
        {
            if (_selectedDiff >= 2) Toast("在困难难度下通关一次冒险后开启噩梦");
            else Toast($"通关 {GameConfig.DIFF_HARD_NEED_CLEARS} 张地图后开启{DiffLabel(_selectedDiff)}");
            return;
        }

        // 无限裂隙不播剧情（2026-10-18 主人拍板）：大厅开场剧情只挂在主线冒险入口
        if (StoryProgress.TutorialDone && !StoryProgress.Chapter1IntroDone
            && _selectedChapter <= 1 && !IsEndlessMode(_selectedMode))
        {
            Chapter1Story.PlayHallIntro(TryEnterBattle);
            return;
        }

        TryEnterBattle();
    }

    public void ForceSelectChapterForTutorial(int chapter, int diff)
    {
        _selectedChapter = Mathf.Max(1, chapter);
        _selectedDiff = Mathf.Clamp(diff, 0, 2);
        _selectedMode = 0;
        RefreshAll();
    }

    void TryEnterBattle()
    {
        // 兜底清掉中断存档（正常情况下启动阶段就已结算完毕）
        BattleStateSaver.ClearInterruptedSave();

        // 无限裂隙：进本标记每次重设（防上次残留污染）；先查每日次数，再查体力（20/关）
        bool tutorialRun = !StoryProgress.TutorialDone
            && !StoryProgress.TutorialBattleCleared
            && StoryProgress.TutorialIntroDone;

        // 引导期一律走引导局，不让裂隙跟引导抢流程
        bool endless = IsEndlessMode(_selectedMode) && !tutorialRun;
        int enterCost = endless ? StaminaSystem.ENDLESS_RIFT_COST : StaminaSystem.ADVENTURE_COST;
        PendingEndlessRift = endless;
        if (endless && EndlessRiftRules.TriesLeft(SaveSystem.Instance?.Data) <= 0)
        {
            Toast($"今天的裂隙挑战次数已用完（{GameConfig.ENDLESS_DAILY_TRIES}/{GameConfig.ENDLESS_DAILY_TRIES}）");
            PendingEndlessRift = false;
            return;
        }

        if (StaminaSystem.Current < enterCost)
        {
            Toast("体力不足");
            PendingEndlessRift = false;
            return;
        }

        TutorialHintUI.Ensure().Hide();

        var opts = tutorialRun
            ? new PlayerJobSelectUI.Options
            {
                TutorialForceSwordFirst = true,
                RequireSwordShield = true
            }
            : default;

        // 先选职业，确认「进入裂隙」再扣体力进场景（引导也只在此弹一次）
        void EnterAfterJobSelect()
        {
            // 无限裂隙：次数 + 20 体力一起扣（TryConsumeEnter 内含提示），成功后标记进本
            if (endless)
            {
                if (!EndlessRiftRules.TryConsumeEnter())
                {
                    PendingEndlessRift = false;
                    return;
                }
                PendingBattleChapter = 0;
                PendingBattleDifficulty = 0;
                PendingGoldDungeon = false;
                // 不调 SetChapter：裂隙章节状态由 StartEndlessRift 全权负责
            }
            else if (!StaminaSystem.TrySpendForAdventure())
            {
                Toast("体力不足");
                return;
            }

            if (tutorialRun)
            {
                StoryProgress.QueueTutorialBattle();
                StoryProgress.ResetTutorialRunInventoryIfNeeded();
                PendingBattleChapter = 1;
                PendingBattleDifficulty = 0;
                PendingGoldDungeon = false;
                ChapterManager.Instance?.SetChapter(1);
            }
            else if (!endless)
            {
                PendingBattleChapter = _selectedChapter;
                PendingBattleDifficulty = _selectedDiff;
                PendingGoldDungeon = IsActivityMode(_selectedMode);
                ChapterManager.Instance?.SetChapter(_selectedChapter);
                NoteChosenBranch(_selectedChapter);
            }
            HidePage();
            GameSceneManager.Instance?.LoadBattleScene();
        }

        // 【2026-10-18 主人拍板】无限裂隙不弹职业选择、不限制职业 —— 刷图玩法用当前职业直接进本；
        // 职业三选一是引导局 / 正式关的仪式感，裂隙每次都弹一遍太拖节奏。
        if (endless)
            EnterAfterJobSelect();
        else
            PlayerJobSelectUI.Show(EnterAfterJobSelect, opts);
    }

    /// <summary>
    /// 在分叉点（第 2/3/4 章）第一次开打时锁定本周目路线，锁上后另外两条支线进不去，
    /// 直到通关第 8 章由 SaveData.MarkChapterCleared 归零，下一周目可重选。
    /// </summary>
    void NoteChosenBranch(int chapter)
    {
        if (chapter < 2 || chapter > 4) return;
        var data = SaveSystem.Instance?.Data;
        if (data == null || data.chosenBranch == chapter) return;
        if (data.chosenBranch != 0) return;   // 已经选过，不覆盖
        data.chosenBranch = chapter;
        SaveSystem.Instance.Save();
        Debug.Log($"[AdventureUI] 本周目选定支线：{GameConfig.GetChapterMapName(chapter)}（内部第{chapter}章）");
    }

    void OnSweep() => Toast("扫荡（即将开放）");

    /// <summary>
    /// 点敌人图标 → 弹该怪信息。
    /// 【2026-10-11 主人拍板】弹窗要显示在「当前点击的敌人上面」→ 把被点的图标节点带下去定位。
    /// </summary>
    void OnClickEnemyIcon(int idx, RectTransform icon = null)
    {
        if (idx < 0 || idx >= _previewMonsters.Count)
        {
            HideTip();
            return;
        }
        ShowMonsterTip(_previewMonsters[idx], icon);
    }
}
