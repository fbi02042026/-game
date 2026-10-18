using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 进关抽奖面板 partial —— 2026-10-18 由 BattleEntryDraftPanel.cs 纯位移拆出（无逻辑改动）
// 职责：继续倒计时（起表/冻结/每帧递减/数字节点）与背包按钮
public partial class BattleEntryDraftPanel
{
    /// <summary>
    /// 抽完奖励后开「继续」倒计时：「继续」按钮上方显示剩余秒数，
    /// 归零自动走 <see cref="DoContinue"/>（防玩家挂着不动把战斗卡死）。
    /// <para>用 <c>WaitForSecondsRealtime</c>：抽奖阶段战斗是冻住的（timeScale 可能为 0），
    /// 普通 WaitForSeconds 会永远不走。</para>
    /// </summary>
    /// <summary>
    /// 倒计时<b>唯一</b>起表 / 重置口（铁律 15：不多入口）。<b>只有一个调用点</b>：
    /// <list type="bullet">
    /// <item><see cref="Begin"/>：面板一露头就开始走（不是抽完才开始）。</item>
    /// </list>
    /// ⚠【2026-10-12 纠正误记】这里原本还列了第二个调用点「每抽一次结算完由
    /// <c>BattleManager.CoStageEntryDraft</c> 再调一次 = 抽一次重置一次计时」——
    /// 那是 2026-10-11 的**误记**：它引用的主人原话恰恰是「时间是固定的 不是抽一次重置一次计时」，
    /// 引文与结论相反。主人 2026-10-12 又强调了一遍，该调用点已改成 <c>SetCountdownPaused(false)</c>
    /// （只解冻、不补满）。<b>总预算就是面板弹出时这 60s</b>，别再把第二个调用点加回来。
    ///
    /// <para>为什么不用协程：协程会随 GameObject 显隐被掐断/延后，主人在外面看到的就是
    /// 「计时好像没在走」。现在改 <see cref="TickContinueCountdown"/> 每帧
    /// 按 <c>Time.unscaledDeltaTime</c> 递减 —— 抽奖阶段 timeScale 冻住也不受影响。</para>
    /// </summary>
    public void StartContinueCountdown(float seconds = ContinueCountdownSec)
    {
        EnsureCountdownText();
        if (_countdownText == null) return;
        _countdownLeft = Mathf.Max(1f, seconds);
        _countdownPaused = false;
        _countdownText.gameObject.SetActive(true);
        _countdownText.text = Mathf.CeilToInt(_countdownLeft).ToString();
        Debug.Log($"[BattleEntryDraftPanel] 倒计时起表/重置 {_countdownLeft:F0}s（面板出现即开始，抽一次重置一次）");
    }

    /// <summary>
    /// 冻结 / 解冻倒计时（抽奖结算、三选一这类挡住玩家的流程）。
    /// <b>只暂停不清零</b> —— 解冻后接着走剩下的秒数，不偷偷补满。
    /// </summary>
    public void SetCountdownPaused(bool paused)
    {
        _countdownPaused = paused;
    }

    public void StopContinueCountdown()
    {
        _countdownLeft = 0f;
        _countdownPaused = false;
        if (_countdownText != null) _countdownText.gameObject.SetActive(false);
    }

    void TickContinueCountdown()
    {
        if (_countdownLeft <= 0f || _countdownPaused) return;
        _countdownLeft -= Time.unscaledDeltaTime;
        if (_countdownLeft <= 0f)
        {
            _countdownLeft = 0f;
            // 归零 = 玩家挂着不动 → 与手点「继续」同一个出口
            DoContinue();
            return;
        }
        if (_countdownText != null)
        {
            _countdownText.text = Mathf.CeilToInt(_countdownLeft).ToString();
            // 【2026-10-13 #7】最后 5 秒：变红 + 放大到 84 + 每秒一次 1.15 缩放脉冲（紧迫感，非惩罚）。
            if (_countdownLeft <= 5f)
            {
                _countdownText.color = new Color(1f, 0.42f, 0.42f); // #FF6B6B
                _countdownText.fontSize = 84;
                float pulse = 1f + 0.15f * Mathf.Abs(Mathf.Sin(Mathf.PI * (5f - _countdownLeft)));
                _countdownText.transform.localScale = Vector3.one * pulse;
            }
            else
            {
                _countdownText.color = new Color(0.66f, 0.68f, 0.74f); // #A8AEBC 常态灰
                _countdownText.fontSize = CountdownFontSize;            // 56
                _countdownText.transform.localScale = Vector3.one;
            }
        }
    }

    /// <summary>
    /// 倒计时数字：挂在<b>面板节点</b>右上角（2026-10-13 #7 从 map 正中挪开，把视觉焦点还给抽奖区），
    /// 不新建美术节点、不改预制体（铁律 3）。
    /// </summary>
    void EnsureCountdownText()
    {
        if (_countdownText != null) return;

        // 【2026-10-13 #7】倒计时挪到面板右上角（锚点 1,1 偏移 -24,-24），不再压屏幕正中。
        // 宿主用面板自身：它常驻全屏高度，右上角即屏幕右上角，位置稳定醒目。
        var host = _panel;
        if (host == null)
        {
            Debug.LogError("[BattleEntryDraftPanel] 找不到 BackpackPanel 节点，倒计时无处可挂");
            return;
        }

        var go = new GameObject("ContinueCountdown", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(host, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-24f, -24f);
        rt.sizeDelta = new Vector2(300f, 120f);

        var t = go.GetComponent<Text>();
        t.text = "";
        t.fontSize = CountdownFontSize;           // 56（常态）
        t.color = new Color(0.66f, 0.68f, 0.74f); // #A8AEBC 常态灰
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var ff = GameFonts.GetChinese();
        if (ff != null) t.font = ff;
        // 大字号更要描边：压在地图美术上才不会糊成一团
        var ol = go.AddComponent<Outline>();
        ol.effectColor = new Color(0f, 0f, 0f, 0.9f);
        ol.effectDistance = new Vector2(4f, -4f);

        _countdownText = t;
    }

    /// <summary>「背包」按钮：现在只切网格背包（BackpackPanel/GridContainer）的出现/收起。
    /// 【2026-10-09 主人拍板】背包按钮不再走 600↔750 两态 —— 那一套 SetExpanded 只留给抽奖展开用。</summary>
    void OnBackpackClicked()
    {
        // 【2026-10-09 主人拍板，先注释不删】原实现：SetExpanded(!_expanded);
        BattleUI.Instance?.ToggleBagDrawer();
    }

}
