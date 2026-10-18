using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 进关抽奖面板 partial —— 2026-10-18 由 BattleEntryDraftPanel.cs 纯位移拆出（无逻辑改动）
// 职责：刷新与点击（Refresh 价格/置灰/免费、抽奖与继续点击、清「新」标、锁抽）
public partial class BattleEntryDraftPanel
{
    /// <summary>
    /// 【2026-10-11 主人拍板】「再来一次」命中后<b>立刻</b>把按钮金额刷成免费，不重开面板。
    /// 主人原话：「抽中再来一次时 抽奖按钮上的金额变成免费一次」。
    /// 用 <see cref="Begin"/> 记下的同一组参数重刷，绝不在这里另算一套（与「抽完锁按钮」同一口径）。
    /// </summary>
    public void RefreshFreeLeft(long coins, int freeLeft)
        => Refresh(_lastRandomPrice, _lastFocusPrice, _lastCats, coins, freeLeft);

    /// <summary>
    /// 刷新价格与可用状态：池子空或币不够 → 置灰，绝不自动扣钱。
    /// <para>⚠ <paramref name="focusPrice"/> <b>已不再使用</b>（2026-10-07）：定向价改由循环里按
    /// <c>slot.Cat</c> 各自的币种算（佣兵 = 佣兵币价，装备 / 技能 = 抽奖币价），
    /// 参数保留只是为了不动三个调用点的签名。<paramref name="coins"/> 只用于「普通」随机按钮。</para>
    /// </summary>
    void Refresh(int randomPrice, int focusPrice, List<DraftCategory> cats, long coins, int freeLeft = 0)
    {
        // 记下这一组参数：抽完锁按钮时要按同一组重刷，绝不另算一套（2026-10-06）
        _lastRandomPrice = randomPrice;
        _lastFocusPrice = focusPrice;
        _lastFreeLeft = freeLeft;
        _lastCoins = coins;
        _lastCats = cats;

        // 【2026-10-18 主人拍板】抽奖币池计数跟着同一组余额走（顶栏那本账是「本局获得金币」，不在这显示）
        if (_poolChipText != null) _poolChipText.text = coins.ToString();

        // 2026-10-05：金币本开局送的免费抽 —— 还有额度时按钮上写「免费 ×N」，不看余额也能点。
        bool free = freeLeft > 0;
        for (int i = 0; i < _slots.Count; i++)
        {
            var s = _slots[i];
            if (s.Btn == null) continue;
            bool isRandom = !s.Cat.HasValue;
            bool has = isRandom || (cats != null && cats.Contains(s.Cat.Value));
            // 价格与余额都按<b>各自币种</b>算（真源 SlotMachineSystem，单出口）：
            // 佣兵定向 = 佣兵币的价与余额；装备/技能定向 = 抽奖币的价与余额。
            int price = isRandom ? randomPrice : SlotMachineSystem.FocusPrice(s.Cat.Value);
            long wallet = isRandom
                ? coins
                : SlotMachineSystem.Balance(SlotMachineSystem.FocusCurrency(s.Cat.Value));
            // 2026-10-06：本拍已抽完（引导局一次一抽）→ 全部置灰，玩家只剩「继续」可点。
            bool on = !_drawLocked && has && (free || wallet >= price);
            // 置灰交给 Button.interactable —— 它会按 disabledColor 作用在 icon 上；
            // 不去改 name/金额 的文字颜色，那是主人调好的美术。
            s.Btn.interactable = on;
            if (s.NameText != null) s.NameText.text = Name(s.Cat);
            bool insufficient = !free && wallet < price;
            if (s.CostText != null)
            {
                // 【2026-10-18 主人拍板】免费抽按钮只写「免费」两字：后面不带次数（×N / 一次都不要），
                // 且「免费」要居中显示（见下方对齐处理）；有价数字仍按原样显示。
                s.CostText.text = !has ? "-"
                               : free ? "免费"
                               : price.ToString();
                // 【2026-10-18 主人拍板】「免费」居中；非免费恢复预制体原对齐（懒缓存，不永久覆盖美术）。
                if (!s.CostAlignDefault.HasValue) s.CostAlignDefault = s.CostText.alignment;
                s.CostText.alignment = free ? TextAnchor.MiddleCenter : s.CostAlignDefault.Value;
                // 【2026-10-13 #7】币不够：价格数字变红；够了恢复主人调好的原色（懒缓存，不永久覆盖）。
                if (!s.CostColorDefault.HasValue) s.CostColorDefault = s.CostText.color;
                s.CostText.color = insufficient ? new Color(1f, 0.42f, 0.42f) : s.CostColorDefault.Value;
            }
            // 【2026-10-13 #7】币不够：价格下方红字「还差 N」（与价格数字三态互斥思路一致）。
            if (s.ShortfallText != null)
            {
                bool showSf = insufficient && has;
                s.ShortfallText.gameObject.SetActive(showSf);
                if (showSf) s.ShortfallText.text = $"还差 {price - wallet}";
            }
            // 免费抽不扣任何币 → 货币图标跟着一起隐（要改成「一直显示」只改这一处）。
            if (s.CoinIcon != null)
            {
                bool showIcon = has && !free && s.CoinIcon.sprite != null;
                if (s.CoinIcon.gameObject.activeSelf != showIcon)
                    s.CoinIcon.gameObject.SetActive(showIcon);
            }
        }
    }

    void OnDraftClicked(int idx)
    {
        if (!_draftActive || idx < 0 || idx >= _slots.Count) return;
        _comboCount++;            // 【2026-10-13 #9】每点一次抽奖 +1
        RefreshCombo();
        // 【2026-10-18 二次拍板】玩家抽了：收掉当前手指（可能还指着抽奖按钮）、重走 5 秒计时——
        // 抽完后又间隔 5 秒无操作，才再指引点「继续」；不是抽完立刻切目标。
        _hasDrawnThisPanel = true;
        RestartIdleTimerKeepState();
        var cb = _onPick;
        var cat = _slots[idx].Cat;
        cb?.Invoke(cat);
    }

    /// <summary>
    /// 玩家抽了：收掉当前手指、重走 5 秒计时（不清 _hasDrawnThisPanel，归零后按「抽过」口径指「继续」）。
    /// 引导局不启这套（新手引导还是原来的设计，与 RestartIdleGuide 同一口径）。
    /// </summary>
    void RestartIdleTimerKeepState()
    {
        var bm = BattleManager.Instance;
        if (bm != null && bm.IsTutorialRun) return;
        StopIdleGuide();
        _idleGuideCo = StartCoroutine(CoIdleGuide());
    }

    void OnContinueClicked()
    {
        DoContinue();
    }

    /// <summary>
    /// 「继续」的<b>唯一出口</b> —— 玩家点按钮、倒计时归零，两条路都走这里。
    /// <para>2026-10-06 主人拍板：抽完奖励<b>不许自动继续</b>，必须有明确动作；
    /// 倒计时只是「玩家挂着不动」的兜底，不是第二条流程。</para>
    /// </summary>
    void DoContinue()
    {
        if (!_draftActive) return;
        StopContinueCountdown();
        // 【2026-10-06 主人拍板】点「继续」→ 本拍抽奖打上的「新」标记一次性清掉，角标跟着消失。
        ClearNewLootBadges();
        var cb = _onFight;
        cb?.Invoke();
    }

    /// <summary>
    /// 【2026-10-06 主人拍板】清「新」的<b>唯一出口</b>：玩家点「继续」、倒计时归零两条路都走
    /// <see cref="DoContinue"/> → 这里。别处一律不许清，免得「新」字提前消失（或永远不消失）。
    /// <para>顺序不能反：<b>先清标记，再刷 UI</b> —— 反了刷出来的还是带标记的那一版。</para>
    /// <para>没有标记就什么都不做（不无故刷 UI）。</para>
    /// </summary>
    void ClearNewLootBadges()
    {
        if (NewLootMarks.Count <= 0) return;
        NewLootMarks.ClearAll();
        var ui = BattleUI.Instance;
        if (ui == null) return;
        ui.UpdateRunSkillSlots();
        ui.UpdateMercSkillSlots();
        ui.UpdateEquipQuickSlots();
    }

    /// <summary>
    /// 抽完之后锁住抽奖按钮（<b>只置灰，不动显隐 / 不动美术</b>），这一拍玩家只剩「继续」可点。
    /// 正式关连抽本身就是玩法 → <b>不要调它</b>（主人 2026-10-06：正式关抽完也要点继续，但还能接着抽）。
    /// </summary>
    public void SetDrawLocked(bool locked)
    {
        _drawLocked = locked;
        if (!_draftActive) return;
        // 按上一次 Refresh 的同一组参数重刷，不另算一套
        Refresh(_lastRandomPrice, _lastFocusPrice, _lastCats, _lastCoins, _lastFreeLeft);
    }

}
