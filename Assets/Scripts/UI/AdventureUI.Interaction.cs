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
        // 模式：主线(0) + 无限裂隙(2) + 活动副本(4)（2026-10-23 主人拍板「活动副本一拆二」：
        // 点活动标签右侧出金币/徽记两横条列表）；每日副本(1)/BOSS挑战(3) 仍隐藏
        for (int i = 0; i < modeButtons.Length; i++)
        {
            if (modeButtons[i] == null) continue;
            modeButtons[i].gameObject.SetActive(i == 0 || i == 2 || i == 4);
        }
        // 不再无条件重置主线：切页保留上次选择；只有停在了未开放入口（每日/BOSS）或还没解锁的标签（裂隙/活动，2026-10-24 拍板）才回主线
        if (_selectedMode == 1 || _selectedMode == 3 || !IsModePlayable(_selectedMode) || IsModeTabLocked(_selectedMode))
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
        // 【2026-10-24 主人拍板】标签锁：无限裂隙/活动副本 通关第 1 章后才可切入；
        // 没通就停在当前页 + 提示（标签常显示灰显锁态，方案=主人「或者把无限和活动也常显示」那条）
        if (IsModeTabLocked(idx))
        {
            Toast($"通关第 1 章后开启{ModeLabel(idx)}");
            return;
        }
        _selectedMode = idx;
        if (!IsDiffUnlockedForSelectedMode(_selectedDiff))
            _selectedDiff = 0;
        HideTip();
        if (IsActivityMode(idx))
        {
            EnsureActivityRowsWired();
            // 默认选中今天开放且门槛已到的副本：当前选择仍有效则保留（切标签来回不丢选中，2026-10-24 主人拍板），
            // 失效才切到别的开放本（三本周一至周日每天恰有一本开：金=一三六 / 徽=二四日 / 卷=五）。
            // 显隐/灰显/选中节点统一由 ApplyActivityRowStates 处理（RefreshAll 里会刷）。
            if (!IsActivityRowSelectable(_selectedActivityKind))
            {
            if (IsActivityRowSelectable(ActivityDungeonKind.Gold)) _selectedActivityKind = ActivityDungeonKind.Gold;
            else if (IsActivityRowSelectable(ActivityDungeonKind.Badge)) _selectedActivityKind = ActivityDungeonKind.Badge;
            else if (IsActivityRowSelectable(ActivityDungeonKind.Scroll)) _selectedActivityKind = ActivityDungeonKind.Scroll;
            else if (IsActivityRowSelectable(ActivityDungeonKind.Gift)) _selectedActivityKind = ActivityDungeonKind.Gift;
            }
        }
        if (!IsModePlayable(idx))
            Toast($"{ModeLabel(idx)}即将开放");
        RefreshAll();
        // 【2026-10-23 主人要求】切模式后新页面弹出（见 AdventureUI.Anim.cs）
        PlayRightModePopIn();
    }

    // ────────────────────────────────────────────────────
    // 左侧模式标签锁（2026-10-24 主人拍板）：无限裂隙(2)/活动副本(4) 通关第 1 章后才能切入。
    // 标签常显示、锁态灰显（alpha 0.5）、点击拦截弹提示（主人「或者把无限和活动也常显示
    // 只是点击切不过去 提示通关第一章后开启」那条方案）；主线(0) 恒开，每日(1)/BOSS(3) 本就隐藏。
    // 门槛不看 TEST_OPEN_ALL 总闸（总闸只旁路开放日/进本，方便进本测试；锁不旁路才能看到锁态）。
    // ────────────────────────────────────────────────────

    /// <summary>该模式标签当前是否锁定（未通关第 1 章）。</summary>
    bool IsModeTabLocked(int idx)
    {
        if (idx == 0 || idx == 1 || idx == 3) return false;   // 主线恒开；每日/BOSS 隐藏位无锁概念
        var data = SaveSystem.Instance?.Data;
        return data == null || !data.HasClearedChapter(1);
    }

    /// <summary>标签锁态视觉：灰显 alpha 0.5（保持可点——点了要弹提示），解锁即回 1。RefreshAll 每次刷。</summary>
    void ApplyModeTabLock(Button btn, int idx)
    {
        var cg = btn.GetComponent<CanvasGroup>();
        if (cg == null) cg = btn.gameObject.AddComponent<CanvasGroup>();
        float target = IsModeTabLocked(idx) ? 0.5f : 1f;
        if (!Mathf.Approximately(cg.alpha, target)) cg.alpha = target;
        cg.blocksRaycasts = true;   // 锁态也要接点击（弹提示），只降透明度不关射线
    }

    // ────────────────────────────────────────────────────
    // 活动副本列表（2026-10-23 主人拍板「活动副本一拆二」）
    // prefab 已摆好 RightContent/活动副本 下金币/徽记两个横条（bg/icon/Name/Info），
    // 但没挂 Button、没有选中态 → 运行时补按钮 + 用底图颜色做选中高亮（不改 prefab、不动排版）。
    // ────────────────────────────────────────────────────

    /// <summary>给两个横条补 Button 并接线（只做一次；找不到横条 LogError 不兜底，不重试防刷屏）。</summary>
    void EnsureActivityRowsWired()
    {
        if (_activityRowsWired) return;
        _activityRowsWired = true;   // 无论成败只绑一次：失败的情形（预制体被改）每次刷新都重报只会刷屏
        var list = transform.Find("RightContent/" + ActivityNodeName);
        if (list == null)
        {
            Debug.LogError($"[AdventureUI] RightContent 下找不到「{ActivityNodeName}」节点，活动列表无法交互（缺真值不兜底）");
            return;
        }
        WireActivityRow(list.Find(ActivityGoldRowName), ActivityDungeonKind.Gold);
        WireActivityRow(list.Find(ActivityBadgeRowName), ActivityDungeonKind.Badge);
        WireActivityRow(list.Find(ActivityScrollRowName), ActivityDungeonKind.Scroll);
        WireActivityRow(EnsureGiftRow(list), ActivityDungeonKind.Gift);
    }

    /// <summary>
    /// 礼物横条（2026-10-26 主人拍板「新加一个掉落礼物的活动副本周六开启」，2026-10-27 已落盘为 prefab 实体条）：
    /// prefab 已摆好「礼物」实体条，本方法 exist != null 直接返回；克隆卷轴条仅作兜底（prefab 被改缺实体条才触发）。
    /// 文案「礼物副本 / 好感礼物每周六开启」，选中高亮子节点照用；bg/选中 复用原背景图（prefab 引用，不做单独图片）。
    /// </summary>
    Transform EnsureGiftRow(Transform list)
    {
        var exist = list.Find(ActivityGiftRowName);
        if (exist != null) return exist;

        var src = list.Find(ActivityScrollRowName);
        if (src == null)
        {
            Debug.LogError($"[AdventureUI] 活动列表缺「{ActivityScrollRowName}」横条，礼物横条无从克隆（缺真值不兜底）");
            return null;
        }
        var clone = Instantiate(src, list);
        clone.name = ActivityGiftRowName;

        // 摆位：贴在卷轴条下方一行（纵向 y 排版 = 列表填充必要介入，行距照追查条口径运行时实测）
        var gold = list.Find(ActivityGoldRowName) as RectTransform;
        var badge = list.Find(ActivityBadgeRowName) as RectTransform;
        var scroll = src as RectTransform;
        var crt = clone as RectTransform;
        if (gold != null && badge != null && scroll != null)
        {
            float rowDist = (gold as RectTransform).anchoredPosition.y - badge.anchoredPosition.y;
            crt.anchoredPosition = new Vector2(scroll.anchoredPosition.x, scroll.anchoredPosition.y - rowDist);
        }

        var nameTxt = clone.Find("Name")?.GetComponent<Text>();
        if (nameTxt != null) nameTxt.text = "礼物副本";
        else Debug.LogError("[AdventureUI] 礼物横条缺 Name 文本节点（缺真值不兜底）");
        var infoTxt = clone.Find("Info")?.GetComponent<Text>();
        if (infoTxt != null) infoTxt.text = "好感礼物每周六开启";
        return clone;
    }

    void WireActivityRow(Transform row, ActivityDungeonKind kind)
    {
        if (row == null)
        {
            Debug.LogError($"[AdventureUI] 活动列表缺横条节点（{ActivityDungeonRules.DungeonName(kind)}），该副本无法选择（缺真值不兜底）");
            return;
        }
        var btn = row.GetComponent<Button>();
        if (btn == null) btn = row.gameObject.AddComponent<Button>();
        var bgImg = row.Find("bg")?.GetComponent<Image>();
        if (bgImg != null)
        {
            btn.targetGraphic = bgImg;
            if (!_activityRowBaseColors.ContainsKey(bgImg))
                _activityRowBaseColors[bgImg] = bgImg.color;
            // prefab 里若关了射线，点击会整个失效 → 接按钮时强制打开（只有 bg 这一个接收面）
            bgImg.raycastTarget = true;
        }
        else
        {
            Debug.LogError($"[AdventureUI] 活动横条 {ActivityDungeonRules.DungeonName(kind)} 下找不到 bg 底图，选中高亮失效（缺真值不兜底）");
        }
        btn.transition = Selectable.Transition.ColorTint;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => OnSelectActivityRow(kind));
    }

    void OnSelectActivityRow(ActivityDungeonKind kind)
    {
        // 没轮到（今天不开放）：不选中，只提示开启日期（2026-10-24 主人拍板「点击提示开启日期」）
        if (!ActivityDungeonRules.IsOpenToday(kind))
        {
            Toast(ActivityDungeonRules.OpenDayHint(kind));
            return;
        }
        _selectedActivityKind = kind;
        ApplyActivityRowStates();
        RefreshDetailPanel();
    }

    /// <summary>
    /// 该横条当前能不能作为选中项：门槛已到 + 今天开放。显隐/灰显/选中判断同源（2026-10-24 主人拍板）。
    /// 【2026-10-24 晚拍板】章节门槛<b>不再被</b> TEST_OPEN_ALL 总闸旁路（主人「没达到开放条件的不要显示」）
    /// ——总闸只旁路开放日/进本，显示层永远按真实存档门槛走。
    /// </summary>
    bool IsActivityRowSelectable(ActivityDungeonKind kind)
    {
        if (!ActivityDungeonRules.IsOpenToday(kind)) return false;
        var data = SaveSystem.Instance?.Data;
        return data != null && data.HasClearedChapter(ActivityDungeonRules.GateChapter(kind));
    }

    /// <summary>
    /// 活动横条状态统一出口（2026-10-24 主人拍板）：
    /// ① 未达到开启条件（章节门槛没到）→ 整条隐藏；
    /// ② 没轮到（今天不开放）→ 灰显（bg 压灰，点击由 OnSelectActivityRow 弹开启日期）；
    /// ③ 选中效果 = 主人在 prefab 摆好的「选中」子节点，开启即亮（不改排版、不改颜色）。
    /// 切战斗标签来回时 RefreshAll 会重刷本方法 → 选中状态不丢。
    /// </summary>
    void ApplyActivityRowStates()
    {
        var list = transform.Find("RightContent/" + ActivityNodeName);
        if (list == null) return;
        var kinds = new[] { ActivityDungeonKind.Gold, ActivityDungeonKind.Badge, ActivityDungeonKind.Scroll, ActivityDungeonKind.Gift };
        var names = new[] { ActivityGoldRowName, ActivityBadgeRowName, ActivityScrollRowName, ActivityGiftRowName };
        for (int i = 0; i < kinds.Length; i++)
        {
            var row = list.Find(names[i]);
            if (row == null) continue;
            // ① 门槛没到 → 整条隐藏【2026-10-24 晚主人拍板：显示层不被 TEST_OPEN_ALL 总闸旁路，
            //    总闸只旁路开放日/进本（测试便利），显示永远按真实存档门槛】
            bool gateOk = false;
            {
                var data = SaveSystem.Instance?.Data;
                gateOk = data != null && data.HasClearedChapter(ActivityDungeonRules.GateChapter(kinds[i]));
            }
            row.gameObject.SetActive(gateOk);
            if (!gateOk) continue;
            bool openToday = ActivityDungeonRules.IsOpenToday(kinds[i]);
            // ② 灰显：bg 压灰（原色缓存 × 灰度系数），正常日恢复原色
            var bgImg = row.Find("bg")?.GetComponent<Image>();
            if (bgImg != null)
            {
                if (!_activityRowBaseColors.TryGetValue(bgImg, out var baseColor))
                {
                    baseColor = bgImg.color;
                    _activityRowBaseColors[bgImg] = baseColor;
                }
                bgImg.color = openToday ? baseColor : baseColor * ActivityRowGrayFactor;
            }
            // ③ 选中节点：主人 prefab 已摆（bg/选中/Name/Info），开启即亮
            var sel = row.Find("选中");
            if (sel != null)
                sel.gameObject.SetActive(openToday && _selectedActivityKind == kinds[i]);
        }
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
    /// <summary>
    /// 定位到指定章（切选中章并刷新）。
    /// 【2026-10-26 主人拍板「任务栏前往即使没开启也可以定位到」】任务点击走 ignoreLock=true：
    /// 锁着的章也照样切过去看（关卡列表自带锁态展示），普通点击（玩家手点页签）仍受 CanEnter 把关。
    /// </summary>
    public void FocusChapter(int chapter, bool ignoreLock = false)
    {
        if (chapter < 1 || chapter > 8) return;
        if (!ignoreLock && !ChapterRouteTable.CanEnter(chapter, SaveSystem.Instance?.Data)) return;

        _selectedChapter = chapter;
        if (_built && gameObject.activeInHierarchy)
        {
            PlayChapterFadeIn();
            RefreshAll();
        }
    }

    void OnSelectDiff(int idx)
    {
        if (!IsModePlayable(_selectedMode))
        {
            Toast($"{ModeLabel(_selectedMode)}即将开放");
            return;
        }
        if (!IsDiffUnlockedForSelectedMode(idx))
        {
            // 活动副本（2026-10-23 主人拍板「打通前一难度才开下一难度」）：提示口径与主线不同
            if (IsActivityMode(_selectedMode))
            {
                Toast("先通关上一难度才开启下一难度");
                return;
            }
            // 主线难度锁定提示统一出口（2026-10-23 主人拍板：困难=通关第一章、噩梦=通关第三章）
            Toast(DiffLockHint(idx));
            return;
        }
        _selectedDiff = idx;
        // 【2026-10-24 主人拍板「难度逐章解锁」】切难度后当前章在该难度可能未开启
        // （上一章该难度没通）→ 选择回退到该难度下应推进的章，标识跟着走。
        // 只对主线生效：活动副本难度走 ActivityDungeonRules 自己的逐级解锁，不牵动章节。
        if (IsMainMode(_selectedMode)
            && !ChapterRouteTable.CanEnterAtDiff(_selectedChapter, _selectedDiff, SaveSystem.Instance?.Data))
        {
            _selectedChapter = ChapterRouteTable.CurrentChapterToAdvanceAtDiff(_selectedDiff, SaveSystem.Instance?.Data);
            PlayChapterFadeIn();
        }
        RefreshDiffHighlight();
        RefreshDetailPanel();
        RefreshChestNodes();   // 【2026-10-24 难度逐章解锁】难度变了：关卡列灰锁/当前关标识按新难度重算
    }

    /// <summary>
    /// 手动翻章（滑屏/箭头共用）能不能去该章。
    /// 【2026-10-24 主人拍板「手动滑屏只能滑到已在世界地图开启的章」】：没在世界地图看到开启的章
    /// 不能滑跨章——进世界地图开图即记档（WorldMapPopup.MaxSeenChapterId），看过才能滑过去。
    /// 不通过时已 Toast，调用方留在原地即可。
    /// </summary>
    bool CanManualSwipeTo(int chapter)
    {
        if (chapter <= WorldMapPopup.MaxSeenChapterId()) return true;
        Toast("先在世界地图开启新的区域");
        return false;
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
        int target = _availableChapters[idx];
        // 【2026-10-24 主人拍板】没在世界地图看到开启的章不许滑跨章（含循环回绕的那一下）
        if (!CanManualSwipeTo(target)) return;
        // 【2026-10-24 主人拍板「难度逐章解锁」】该章在当前难度未开启 → 提示后留在原地
        if (IsMainMode(_selectedMode)
            && !ChapterRouteTable.CanEnterAtDiff(target, _selectedDiff, SaveSystem.Instance?.Data))
        {
            Toast(ChapterRouteTable.DiffChapterLockHint);
            return;
        }
        _selectedChapter = target;
        PlayChapterFadeIn();
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
        // 【2026-10-19 主人拍板 B】最后一章不再循环回第 1 章，提示后留在原地
        if (idx >= _availableChapters.Count - 1)
        {
            Toast("已经是最后一章");
            return;
        }
        int target = _availableChapters[idx + 1];
        // 【2026-10-24 主人拍板】手动翻章只能去世界地图里看到开启过的章；没看过 → 提示后留在原地
        if (!CanManualSwipeTo(target)) return;
        _selectedChapter = target;
        // 【2026-10-24 主人拍板「难度逐章解锁」】该章在当前难度未开启 → 提示后留在原地
        if (IsMainMode(_selectedMode)
            && !ChapterRouteTable.CanEnterAtDiff(_selectedChapter, _selectedDiff, SaveSystem.Instance?.Data))
        {
            _selectedChapter = _availableChapters[idx];
            Toast(ChapterRouteTable.DiffChapterLockHint);
            return;
        }
        PlayChapterFadeIn();
        RefreshAll();
        // 成功滑到后续章节 → 「世界地图引导」永久关闭（滑动到后续章也算完成了引导目标）
        if (_selectedChapter > 1) MarkWorldMapGuideDone();
    }

    // ────────────────────────────────────────────────────
    // 翻章过渡动效（2026-10-19 主人拍板 A）：切章后新地图淡入 0.18s
    // 只动 StageNodes 的 CanvasGroup 透明度（运行时补挂，不改预制体）；
    // 连续切章会重启动画，页面隐藏时复原，不会停在半透明。
    // ────────────────────────────────────────────────────

    /// <summary>新地图淡入时长（秒）。</summary>
    const float ChapterFadeInDur = 0.18f;

    Coroutine _chapterFadeRoutine;

    void PlayChapterFadeIn()
    {
        if (stageNodeContainer == null || !gameObject.activeInHierarchy) return;
        var cg = stageNodeContainer.GetComponent<CanvasGroup>();
        if (cg == null) cg = stageNodeContainer.gameObject.AddComponent<CanvasGroup>();
        if (_chapterFadeRoutine != null) StopCoroutine(_chapterFadeRoutine);
        cg.alpha = 0f;
        _chapterFadeRoutine = StartCoroutine(CoChapterFadeIn(cg));
    }

    IEnumerator CoChapterFadeIn(CanvasGroup cg)
    {
        float t = 0f;
        while (t < ChapterFadeInDur)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Clamp01(t / ChapterFadeInDur);
            yield return null;
        }
        cg.alpha = 1f;
        _chapterFadeRoutine = null;
    }

    void OnDisable()
    {
        // 页面隐藏时停掉淡入并复原透明度，避免下次打开停在半透明
        if (_chapterFadeRoutine != null)
        {
            StopCoroutine(_chapterFadeRoutine);
            _chapterFadeRoutine = null;
        }
        if (stageNodeContainer != null)
        {
            var cg = stageNodeContainer.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f;
        }
        // 【2026-10-23 主人要求】弹出动效收尾：停协程 + 还原缩放/透明度，别停在半程
        // 【2026-10-24 主人拍板】左侧标签滑入动效半途隐藏页面时归位，防止标签永久停在屏幕外
        RestorePopAnims();
    }

    // ────────────────────────────────────────────────────
    // 滑动翻章（2026-10-17 主人要求）：在地图区横向拖拽切上一章/下一章
    // 事件冒泡到根：按下点在 RightContent/MapRoot 子树内才响应，其他区域忽略。
    // ────────────────────────────────────────────────────

    /// <summary>滑动翻章触发阈值（屏幕像素）；不足视为轻点/误触，不翻页。</summary>
    const float ChapterSwipeThresholdPx = 100f;

    bool _chapterSwipeActive;
    float _chapterSwipeDx;

    /// <summary>拖拽起点是否落在地图区（主线节点 / StageNodes 子树）内。
    /// 【2026-10-23 主人新布局】翻章只对主线有意义：RightContent/主线 节点 + 根下 StageNodes；
    /// 无限/活动模式拖拽不响应（裂隙无章、活动无章）。</summary>
    bool IsMapDragStart(PointerEventData eventData)
    {
        if (_selectedMode != 0) return false;
        var hit = eventData.pointerPressRaycast.gameObject;
        if (hit == null) return false;
        var right = transform.Find("RightContent");
        var mainNode = right != null ? right.Find(MainNodeName) : null;
        if (mainNode != null && hit.transform.IsChildOf(mainNode)) return true;
        return stageNodeContainer != null && hit.transform.IsChildOf(stageNodeContainer);
    }

    /// <summary>
    /// 按下点是否落在关卡滑动列表（关卡Viewport，2026-10-24 主人拍板「关卡列改滑动列表」）子树内。
    /// 滑动列表自己吃竖向拖拽，翻章手势要在这里让路，否则竖着滑关卡会误触发横向翻章。
    /// </summary>
    bool IsInsideChestViewport(GameObject hit)
    {
        if (hit == null) return false;
        var right = transform.Find("RightContent");
        var mainNode = right != null ? right.Find(MainNodeName) : null;
        var viewport = mainNode != null ? mainNode.Find(ChestViewportName) : null;
        return viewport != null && hit.transform.IsChildOf(viewport);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // 关卡滑动列表子树内的拖拽归 ScrollRect（竖滑关卡），不触发翻章
        _chapterSwipeActive = IsMapDragStart(eventData) && !IsInsideChestViewport(eventData.pointerPressRaycast.gameObject);
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
        if (!IsDiffUnlockedForSelectedMode(_selectedDiff))
        {
            // 活动副本（2026-10-23 主人拍板）：逐级解锁，提示口径与主线不同
            if (IsActivityMode(_selectedMode))
            {
                Toast("先通关上一难度才开启下一难度");
                return;
            }
            Toast(DiffLockHint(_selectedDiff));
            return;
        }
        // 【2026-10-24 主人拍板「难度逐章解锁」】该章在当前难度未开启（上一章该难度没通）→ 提示不进
        if (IsMainMode(_selectedMode)
            && !ChapterRouteTable.CanEnterAtDiff(_selectedChapter, _selectedDiff, SaveSystem.Instance?.Data))
        {
            Toast(ChapterRouteTable.DiffChapterLockHint);
            return;
        }

        // 无限裂隙不播剧情（2026-10-18 主人拍板）：大厅开场剧情只挂在主线冒险入口
        // 活动副本同样不播（2026-10-23 活动一拆二：金币/徽记都是单场战斗，不该被剧情拦一道）
        if (StoryProgress.TutorialDone && !StoryProgress.Chapter1IntroDone
            && _selectedChapter <= 1 && !IsEndlessMode(_selectedMode) && !IsActivityMode(_selectedMode))
        {
            // 【2026-10-24 主人报「点开始冒险弹剧情，主界面 BGM 还在响」】
            // 根因：StoryDirector.PlayRoutine 只在战斗场景（IsBattleScene）才切剧情曲，
            // 大厅开场剧情在城镇场景播 → bgm_town 整段照播。
            // 修法：本入口先切剧情曲（BeginBattleStory=切 bgm_story 并记住恢复曲），
            // 播完在 onDone 里恢复；若被 StopPlaying 强停，其内部也会调 EndBattleStory 归位，
            // 计数不漏。随后 TryEnterBattle 进加载时 MuteForLoading 接管，恢复协程会让位。
            GameBgm.BeginBattleStory();
            Chapter1Story.PlayHallIntro(() =>
            {
                GameBgm.EndBattleStory();
                TryEnterBattle();
            });
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

    /// <summary>
    /// 【2026-10-25 主人需求「冒险界面显示体力和钻石；体力不足点购买与顶条加号效果一致」】
    /// 进本体力不足弹窗：双按钮确认框一律走 StaminaConfirmPopup（硬规则）。
    /// 弹窗里显示主人要的两项——当前体力（含上限）与持有钻石；
    /// 确认键 = ResourceAdRewards.TryClaimStamina()，与顶条体力加号同一条链、同一阶梯
    /// （每日 2 免费 → 2 广告 → 50 钻 → 100 钻 → 封顶，含飘字/埋点，效果天然一致，不另写一份）。
    /// 档位文案现算现显（ResolveClaimKind 唯一出口，这里不抄阶梯逻辑）；领取后不自动进本，玩家再点一次进入。
    /// </summary>
    void ShowStaminaLackPopup(int enterCost)
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null)
        {
            Toast("体力不足");
            return;
        }
        ResourceAdRewards.EnsureDay(data);
        var kind = ResourceAdRewards.ResolveClaimKind(
            data.adStaminaClaimCount, ResourceAdRewards.StaminaFreePerDay, out int price);
        string tier;
        if (kind == ResourceAdRewards.ClaimKind.Free)
            tier = "本次免费领 " + ResourceAdRewards.StaminaPerAd + " 体力";
        else if (kind == ResourceAdRewards.ClaimKind.Ad)
            tier = "看广告领 " + ResourceAdRewards.StaminaPerAd + " 体力";
        else if (kind == ResourceAdRewards.ClaimKind.Diamond)
            tier = "花 " + price + " 钻领 " + ResourceAdRewards.StaminaPerAd + " 体力";
        else
            tier = "今日补给已达上限（可等体力自然回复）";

        string body = "进本需 " + enterCost + " 体力，当前 " + StaminaSystem.Current + "/" + StaminaSystem.Max
                    + "\n持有钻石 " + ResourceWallet.Get(data, ResourceWallet.ResourceType.Diamond)
                    + "\n体力补给：" + tier;
        StaminaConfirmPopup.Show("体力不足", body, "购买体力", () => ResourceAdRewards.TryClaimStamina());
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
        // 活动副本（2026-10-23 主人拍板「活动副本一拆二」+ 2026-10-24 加卷轴）：金币/徽记/卷轴按横条选中态分流
        bool activity = IsActivityMode(_selectedMode) && !tutorialRun;
        ActivityDungeonKind actKind = _selectedActivityKind;

        // 【2026-10-23 主人拍板】解锁门槛：金币/徽记=通关第 1 章；卷轴=通关第 3 章（2026-10-24 拍板），
        // 打通第 2 章开启无限裂隙（活动门槛受 ActivityDungeonRules.TEST_OPEN_ALL 测试总闸旁路，
        // 打包前总闸改回 false 即恢复）
        var gateData = SaveSystem.Instance?.Data;
        if (activity && !ActivityDungeonRules.TEST_OPEN_ALL
            && (gateData == null || !gateData.HasClearedChapter(ActivityDungeonRules.GateChapter(actKind))))
        {
            Toast($"通关第 {ActivityDungeonRules.GateChapter(actKind)} 章后开启{ActivityDungeonRules.DungeonName(actKind)}");
            return;
        }
        if (endless && (gateData == null || !gateData.HasClearedChapter(2)))
        {
            Toast("通关第 2 章后开启无限裂隙");
            return;
        }

        int enterCost = endless ? StaminaSystem.ENDLESS_RIFT_COST
                      : activity ? ActivityDungeonRules.EntryStaminaCost(actKind)   // 2026-10-24 拍板：金币/徽记 10、卷轴 15
                      : StaminaSystem.ADVENTURE_COST;
        PendingEndlessRift = endless;
        if (endless && EndlessRiftRules.TriesLeft(SaveSystem.Instance?.Data) <= 0)
        {
            Toast($"今天的裂隙挑战次数已用完（{GameConfig.ENDLESS_DAILY_TRIES}/{GameConfig.ENDLESS_DAILY_TRIES}）");
            PendingEndlessRift = false;
            return;
        }

        // 活动副本：先查交替开放日（金=一三六 / 徽=二四日 / 卷=五），再查每日各 2 次（2026-10-24 主人拍板）
        if (activity)
        {
            if (!ActivityDungeonRules.IsOpenToday(actKind))
            {
                Toast(ActivityDungeonRules.OpenDayHint(actKind));
                return;
            }
            if (ActivityDungeonRules.TriesLeft(gateData, actKind) <= 0)
            {
                Toast($"今天的{ActivityDungeonRules.DungeonName(actKind)}次数已用完（{ActivityDungeonRules.DAILY_TRIES}/{ActivityDungeonRules.DAILY_TRIES}）");
                return;
            }
        }

        if (StaminaSystem.Current < enterCost)
        {
            PendingEndlessRift = false;
            ShowStaminaLackPopup(enterCost);   // 【2026-10-25 主人需求】显示体力/钻石 + 购买走顶条加号同链
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
                PendingBadgeDungeon = false;
                PendingScrollDungeon = false;
                PendingGiftDungeon = false;
                PendingBondDungeon = false;
                PendingBondHireId = null;
                // 不调 SetChapter：裂隙章节状态由 StartEndlessRift 全权负责
            }
            else if (activity)
            {
                // 活动副本（金币/徽记/卷轴，2026-10-24 主人拍板「都是2次」+ 卷轴 15 体力）：
                // 每日次数 + 体力一起扣（TryConsumeEnter 内含提示），成功后标记进本
                if (!ActivityDungeonRules.TryConsumeEnter(actKind))
                    return;
                PendingBattleChapter = _selectedChapter;
                PendingBattleDifficulty = _selectedDiff;
                PendingGoldDungeon = actKind == ActivityDungeonKind.Gold;
                PendingBadgeDungeon = actKind == ActivityDungeonKind.Badge;
                PendingScrollDungeon = actKind == ActivityDungeonKind.Scroll;
                PendingGiftDungeon = actKind == ActivityDungeonKind.Gift;
                ChapterManager.Instance?.SetChapter(_selectedChapter);
            }
            else if (!StaminaSystem.TrySpendForAdventure())
            {
                ShowStaminaLackPopup(StaminaSystem.ADVENTURE_COST);   // 同上：显示体力/钻石 + 顶条同链购买
                return;
            }

            if (tutorialRun)
            {
                StoryProgress.QueueTutorialBattle();
                StoryProgress.ResetTutorialRunInventoryIfNeeded();
                PendingBattleChapter = 1;
                PendingBattleDifficulty = 0;
                PendingGoldDungeon = false;
                PendingBadgeDungeon = false;
                PendingScrollDungeon = false;
                PendingGiftDungeon = false;
                ChapterManager.Instance?.SetChapter(1);
            }
            else if (!endless && !activity)
            {
                // 主线（活动副本已在上面 activity 分支处理完进本标记）
                PendingBattleChapter = _selectedChapter;
                PendingBattleDifficulty = _selectedDiff;
                PendingGoldDungeon = false;
                PendingBadgeDungeon = false;
                PendingScrollDungeon = false;
                PendingGiftDungeon = false;
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
    /// 【2026-10-23 合并方案退役】原「进 2/3/4 时兜底补写 chosenBranch」保险丝——
    /// 主线改线性（ChapterRouteTable 合并方案）、派定改掷支线岛后不再需要，直接空转保留（下线≠删除）。
    /// </summary>
    void NoteChosenBranch(int chapter)
    {
        // 退役空转：主线 1→8 线性化后进章无需派定；支线岛派定见 ChapterRouteTable.EnsureForkRoll。
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
