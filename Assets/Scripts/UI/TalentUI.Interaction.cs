using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 天赋界面 partial —— 2026-10-18 由 TalentUI.cs 纯位移拆出（无逻辑改动）
// 职责：交互与演出（点击接线、洗点/左列/右列点击、右列开通演出、6选1弹层流程）
public partial class TalentUI
{

    void WireClicks()
    {
        if (_wired) return;
        _wired = true;
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }
        if (resetButton != null)
        {
            resetButton.onClick.RemoveAllListeners();
            resetButton.onClick.AddListener(OnClickReset);
        }
        if (resetButtonGray != null)
        {
            resetButtonGray.onClick.RemoveAllListeners();
            resetButtonGray.interactable = false;
        }
        if (choiceCancelButton != null)
        {
            choiceCancelButton.onClick.RemoveAllListeners();
            choiceCancelButton.onClick.AddListener(CloseChoicePopup);
        }
        WireChoiceConfirmIfNeeded();
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            int opt = i;
            if (choiceButtons[i] == null) continue;
            choiceButtons[i].onClick.RemoveAllListeners();
            choiceButtons[i].onClick.AddListener(() => SelectChoicePopupOption(opt));
        }
    }

    void WireChoiceConfirmIfNeeded()
    {
        EnsureChoicePopupBindings();
        if (choiceConfirmButton == null) return;
        choiceConfirmButton.onClick.RemoveAllListeners();
        choiceConfirmButton.onClick.AddListener(ConfirmChoicePopup);
    }

    /// <summary>
    /// 预制体里已有「确定」，但序列化未挂 choiceConfirmButton，且 Awake 因 panelImage
    /// 已绑而跳过 AutoBind，导致多选天赋弹层无法确认解锁。
    /// </summary>
    void EnsureChoicePopupBindings()
    {
        if (choicePopup == null)
            choicePopup = transform.Find("ChoicePopup")?.gameObject;

        if (choiceConfirmButton == null && choicePopup != null)
        {
            choiceConfirmButton = choicePopup.transform.Find("确定")?.GetComponent<Button>()
                                  ?? choicePopup.transform.Find("Confirm")?.GetComponent<Button>();
        }

        if (choiceCancelButton == null && choicePopup != null)
        {
            choiceCancelButton = choicePopup.transform.Find("Cancel")?.GetComponent<Button>()
                                 ?? choicePopup.transform.Find("取消")?.GetComponent<Button>();
            if (choiceCancelButton != null)
            {
                choiceCancelButton.onClick.RemoveAllListeners();
                choiceCancelButton.onClick.AddListener(CloseChoicePopup);
            }
        }

        if (choiceTitleText == null)
        {
            choiceTitleText = FindTxt("ChoicePopup/Title")
                              ?? FindTxt("ChoicePopup/标头/Title");
        }

        if (choiceDescText == null)
        {
            // 勿占用 Choice_0/Label（那是选项名）；优先独立描述节点
            choiceDescText = FindTxt("ChoicePopup/Desc")
                             ?? FindTxt("ChoicePopup/Description")
                             ?? FindTxt("ChoicePopup/EffectText")
                             ?? FindTxt("ChoicePopup/标头/Desc");
        }

        if (choiceButtons == null || choiceButtons.Length < 3)
            choiceButtons = new Button[3];
        if (choiceLabels == null || choiceLabels.Length < 3)
            choiceLabels = new Text[3];
        if (choiceIcons == null || choiceIcons.Length < 3)
            choiceIcons = new Image[3];

        for (int i = 0; i < 3; i++)
        {
            if (choiceButtons[i] != null) continue;
            var t = transform.Find($"ChoicePopup/Choice_{i}");
            if (t == null) continue;
            choiceButtons[i] = t.GetComponent<Button>();
            if (choiceLabels[i] == null)
                choiceLabels[i] = choiceButtons[i]?.GetComponentInChildren<Text>(true);
            var iconT = t.Find("Icon") ?? t.Find("icon");
            if (choiceIcons[i] == null && iconT != null)
                choiceIcons[i] = iconT.GetComponent<Image>();
        }

        if (choiceOptionTemplate == null && choiceButtons[0] != null)
            choiceOptionTemplate = choiceButtons[0].gameObject;
    }

    void OnClickReset()
    {
        if (!TalentSystem.CanReset(out string reason))
        {
            Debug.Log("[TalentUI] " + reason);
            // 2026-10-10 主人反馈「点击购买天赋，没有弹出toast」→ 洗点被拒也要弹，不然玩家以为按钮没反应
            GlobalToastUI.Show(reason, true);
            return;
        }
        // 二次确认：先弹确认框，确认后才真正洗点（洗点逻辑本身不动）
        OpenResetConfirm();
    }

    void OnClickLeft(int index0)
    {
        // 【2026-10-13 主人拍板】跨过 10/20/30/40 时右列要演「暗→亮 + 放大缩小」，
        // 前后点数在这取（唯一触发点 = 玩家点了左列并真的买成功，打开界面/洗点都不演）。
        int before = TalentDefs.LeftUnlockedCount(GetTalents());
        if (!TalentSystem.TryUnlockLeft(index0 + 1, out string reason))
        {
            Debug.Log("[TalentUI] " + reason);
            // 2026-10-10 主人反馈「点击购买天赋，没有弹出toast」→ 买不动必须把原因（金币不足等）弹出来
            GlobalToastUI.Show(reason, true);
            return;
        }
        int after = TalentDefs.LeftUnlockedCount(GetTalents());
        onLeftUnlockRequested?.Invoke(index0 + 1);
        RefreshAll();
        // 同上：买成功也要有反馈，光是节点变色玩家不确定扣没扣钱
        GlobalToastUI.Show("已激活：" + LeftNodeName(index0), true);
        if (after > before) PlayRightUnlockFx(before, after);
    }

    /// <summary>
    /// 【2026-10-13 主人拍板】左列点数跨过某道门槛（真源 = <c>talent_right.csv</c> 的 unlockLeftIndex：10/20/30/40）
    /// → 那一批右列节点「暗的节点变亮 + 放大缩小」提示玩家：右列又开了一行。
    /// ⚠ 门槛从配表取，不在代码里写死 10/20/30/40。
    /// </summary>
    void PlayRightUnlockFx(int before, int after)
    {
        if (_rightViews.Count == 0) return;

        // 门槛查询只有这一个出口（NextRightThreshold）：跨没跨过就看 next 是不是 ≤ after
        int threshold = NextRightThreshold(before);
        if (threshold <= 0 || threshold > after) return;

        _fxBatch.Clear();
        for (int i = 0; i < _rightViews.Count; i++)
        {
            var v = _rightViews[i];
            if (v.node != null && v.node.unlockLeftIndex == threshold) _fxBatch.Add(v);
        }
        if (_fxBatch.Count == 0) return;
        // 视觉自下而上（reverseArrangement：visualIndex 0 在最底），按从下往上逐个亮
        _fxBatch.Sort((a, b) => a.visualIndex.CompareTo(b.visualIndex));

        ScrollRightRowIntoView(_fxBatch[0]);
        if (_rightUnlockFx != null) StopCoroutine(_rightUnlockFx);
        _rightUnlockFx = StartCoroutine(RightUnlockFxRoutine());
        Debug.Log("[TalentUI] 左列 L" + threshold + " 达成 → 右列 " + _fxBatch.Count + " 个节点开通演出");
    }

    IEnumerator RightUnlockFxRoutine()
    {
        // 演出期间照 _fxBatch 的快照跑（刷新不会改这个列表内容）
        var batch = new List<RightNodeView>(_fxBatch);
        const float per = 0.5f;     // 单个节点「放大→缩小」时长
        const float stagger = 0.09f; // 同批节点依次亮起
        float t = 0f;
        float total = per + stagger * (batch.Count - 1);

        while (t < total)
        {
            t += Time.unscaledDeltaTime;
            for (int i = 0; i < batch.Count; i++)
            {
                var v = batch[i];
                if (v.root == null) continue;
                float k = Mathf.Clamp01((t - stagger * i) / per);
                if (k <= 0f) continue;
                // 两下脉冲（放大→缩回→放大→缩回），尾巴收住，参考 DraftPrizePopupUI 的写法
                float pulse = Mathf.Abs(Mathf.Sin(k * Mathf.PI * 2f)) * (1f - k);
                v.root.transform.localScale = Vector3.one * (1f + 0.18f * pulse);
                // 暗 → 亮：从锁定灰 0.72 拉回 1（主人原话「暗的节点变亮」）
                SetGraphicAlpha(v.root, Mathf.Lerp(0.72f, 1f, Mathf.Clamp01(k * 3f)));
            }
            yield return null;
        }

        for (int i = 0; i < batch.Count; i++)
            if (batch[i].root != null) batch[i].root.transform.localScale = Vector3.one;

        _rightUnlockFx = null;
        // 收尾交给真值：锁/灰/连线/「新」标按当前存档重刷一遍（这里是唯一收尾出口）
        RefreshRight();
    }

    /// <summary>把目标行滚进视野（那批新开通的行离屏的话，玩家根本看不见演出）。</summary>
    void ScrollRightRowIntoView(RightNodeView v)
    {
        if (rightScroll == null || rightContent == null || v == null || v.root == null) return;
        var vp = rightScroll.viewport;
        if (vp == null) vp = rightContent.parent as RectTransform;
        var row = v.root.transform as RectTransform;
        if (vp == null || row == null) return;

        Canvas.ForceUpdateCanvases();
        float contentH = rightContent.rect.height;
        float vpH = vp.rect.height;
        if (contentH <= vpH + 1f) return;

        // 目标行中心相对视口中心的偏移（视口局部系，y 向上为正；行在上方 → delta > 0）
        float delta = vp.InverseTransformPoint(row.TransformPoint(row.rect.center)).y - vp.rect.center.y;
        // Content 顶对齐（anchor y=1）：anchoredPosition.y ∈ [0, contentH-vpH]，0 = 滚到顶。
        // 内容往下走 = 减小 anchoredPosition.y，于是减去 delta 正好把目标行拉到视口中心。
        float maxY = contentH - vpH;
        float want = Mathf.Clamp(rightContent.anchoredPosition.y - delta, 0f, maxY);
        rightScroll.verticalNormalizedPosition = 1f - want / maxY;
    }

    /// <summary>
    /// 下一道「开右列」门槛：比 leftUnlocked 大的最小 unlockLeftIndex（真源 = talent_right.csv）。
    /// 没有更大门槛（全开完）返回 0。门槛查询只有这一个出口，别在别处另写一遍。
    /// </summary>
    static int NextRightThreshold(int leftUnlocked)
    {
        var arr = TalentDefs.RightNodes;
        if (arr == null) return 0;
        int best = 0;
        for (int i = 0; i < arr.Length; i++)
        {
            var n = arr[i];
            if (n == null) continue;
            int t = n.unlockLeftIndex;
            if (t > leftUnlocked && (best <= 0 || t < best)) best = t;
        }
        return best;
    }

    /// <summary>
    /// 左列序号是不是「开右列」的门槛（同上，真源 = 配表 unlockLeftIndex：10/20/30/40）。
    /// 门槛行用金色、普通行用灰，玩家一眼看出「到 L 几会开右侧」。
    /// </summary>
    static bool IsLeftMilestone(int orderNo)
    {
        var arr = TalentDefs.RightNodes;
        if (arr == null) return false;
        for (int i = 0; i < arr.Length; i++)
            if (arr[i] != null && arr[i].unlockLeftIndex == orderNo) return true;
        return false;
    }

    /// <summary>左列第 index0 个节点的显示名（越界返回占位，绝不返回空串把 toast 吞掉）。</summary>
    static string LeftNodeName(int index0)
    {
        var arr = TalentDefs.Left;
        if (arr == null || index0 < 0 || index0 >= arr.Length) return "天赋";
        return string.IsNullOrEmpty(arr[index0].name) ? "天赋" : arr[index0].name;
    }

    /// <summary>右列节点显示名（同上：拿不到就给占位，不让 toast 变成空字符串）。</summary>
    static string RightNodeName(TalentDefs.TalentRightNode node)
    {
        return node != null && !string.IsNullOrEmpty(node.name) ? node.name : "天赋";
    }

    /// <summary>
    /// 右列节点点击（2026-10-18 主人拍板改为「先弹费用确认」）：
    /// · 未解锁 → 拦截原因（门槛/互斥/双修前置）直接 toast；可解锁（含天赋石不足）→ 弹费用确认窗
    ///（让玩家看到效果 + 需要多少天赋石，而不是干巴巴一句「不够」）；确定才真解锁。
    /// · 已解锁 → 弹升级费用确认（当前级→下一级效果 + 消耗），确定才真升级。
    /// JobChoice（6选1）保持原流程：先弹职业选择，确认时才扣费。
    /// </summary>
    void OnClickRightNode(int visualIndex0)
    {
        if (visualIndex0 < 0 || visualIndex0 >= _rightViews.Count) return;
        var v = _rightViews[visualIndex0];
        if (v.node == null) return;

        // 物理/魔法专精合并行：点击走专门的二选一/升级流
        if (v.node2 != null)
        {
            OnClickMutexPairRow(v);
            return;
        }

        var node = v.node;
        var talents = GetTalents();
        int level = TalentDefs.GetRightNodeLevel(talents, node.id);

        if (level <= 0)
        {
            // 【2026-10-17 主人要求】锁定的节点点了要有说法：先弹具体开启条件
            //（左列开到第几个 / 互斥已选其它流派 / 双修前置），再谈解锁。
            if (!TalentSystem.CanUnlockRightNode(node.id, out string lockReason))
            {
                // 【2026-10-18 主人拍板】天赋石不足不再直接 toast——也弹费用确认窗，
                // 让玩家看到「需要多少」；其余拦截原因仍直接 toast。
                if (lockReason != TalentSystem.ReasonNoStones)
                {
                    GlobalToastUI.Show(lockReason, true);
                    return;
                }
                OpenCostConfirm(node, 0);   // 不足：照样弹窗看缺口，确定时校验不过再 toast
                return;
            }
            // 能解锁：JobChoice 先弹职业选择（保持原流程，确认时才扣费）；其余先弹费用确认
            if (node.IsJobChoice)
            {
                OpenRightJobChoice(node);
                return;
            }
            OpenCostConfirm(node, 0);
        }
        else
        {
            // 已解锁：先弹升级费用确认（下一级效果 + 消耗），确定才真升级
            if (!TalentSystem.CanUpgradeRightNode(node.id, out string upReason))
            {
                GlobalToastUI.Show(upReason, true);
                return;
            }
            OpenCostConfirm(node, level);
        }
    }

    /// <summary>
    /// 任务4 / R_JOB 6选1：打开职业选择弹层（最多 6 个职业）。
    /// 【2026-10-20 修】改用运行时建树的 JobChoicePopup（带确定按钮 + 费用行）：
    /// 手工版预制体 ChoicePopup 只有 1 张卡、没有「确定」，旧克隆布局卡重叠飞出 Box，玩家没法选没法确认。
    /// </summary>
    void OpenRightJobChoice(TalentDefs.TalentRightNode node)
    {
        EnsureJobChoicePopup();
        if (_jobChoicePopup == null) return;

        _pendingNode = node;
        _pendingRowIndex = node.index - 1;
        _pendingSelectedOpt = -1;

        FillJobChoiceCards(node);
        SelectChoicePopupOption(0);   // 默认选第 1 个职业，费用行同步刷新

        _jobChoicePopup.SetActive(true);
        _jobChoicePopup.transform.SetAsLastSibling();
    }

    void SelectChoicePopupOption(int optIndex0)
    {
        if (_pendingNode == null || optIndex0 < 0) return;
        _pendingSelectedOpt = optIndex0;
        var opts = _pendingNode.options;
        if (opts == null || optIndex0 >= opts.Length) return;

        // 费用行：选中职业的 1 级效果（JobChoice 各职业 +4%×等级）+ 消耗（持有不足追加提醒）
        if (_jobChoiceCost != null)
        {
            int cost = _pendingNode.costByLevel != null && _pendingNode.costByLevel.Length > 0
                ? _pendingNode.costByLevel[0] : 0;
            int have = SaveSystem.Instance?.Data?.talentPoints ?? 0;
            _jobChoiceCost.text = "选中【" + opts[optIndex0].name + "】：" + _pendingNode.EffectSummary(1, optIndex0 + 1)
                + "\n消耗天赋石：" + cost
                + (have >= cost ? "（持有 " + have + "）" : "（持有 " + have + "，天赋石不足）");
        }

        // 选中高亮：亮 / 半透明（与二选一弹窗同口径）
        if (_jobOptCards != null)
            for (int i = 0; i < _jobOptCards.Length; i++)
                if (_jobOptCards[i] != null && _jobOptCards[i].activeSelf)
                    SetGraphicAlpha(_jobOptCards[i], i == optIndex0 ? 1f : 0.55f);
    }

    void ConfirmChoicePopup()
    {
        if (_pendingNode == null || _pendingSelectedOpt < 0) return;
        int chosenJob1Based = _pendingSelectedOpt + 1;
        if (!TalentSystem.TryUnlockRightNode(_pendingNode.id, chosenJob1Based, out string reason))
        {
            Debug.Log("[TalentUI] " + reason);
            // 2026-10-10 主人反馈「点击购买天赋，没有弹出toast」
            GlobalToastUI.Show(reason, true);
            return;
        }
        onRightChoiceRequested?.Invoke(_pendingNode.index, chosenJob1Based);
        CloseChoicePopup();
        RefreshAll();
        GlobalToastUI.Show("已激活：" + RightNodeName(_pendingNode), true);
    }

    void CloseChoicePopup()
    {
        if (_jobChoicePopup != null) _jobChoicePopup.SetActive(false);
        if (choicePopup != null) choicePopup.SetActive(false);
        _pendingNode = null;
        _pendingRowIndex = -1;
        _pendingSelectedOpt = -1;
    }
}
