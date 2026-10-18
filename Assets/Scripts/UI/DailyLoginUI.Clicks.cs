using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 每日登录弹窗 partial —— 2026-10-18 由 DailyLoginUI.cs 纯位移拆出（无逻辑改动）
// 职责：领取点击（天数/循环/连击/累计大奖四类领取回调）
public partial class DailyLoginUI
{

    // ============================================================
    // 点击
    // ============================================================

    void OnClickStarter(int day)
    {
        string msg;
        bool ok = DailyLoginSystem.TryClaimStarter(day, out msg);
        GlobalToastUI.Show(msg);
        if (ok)
        {
            // 2026-10-15 主人拍板「领取后就不用再弹了」：天数奖领到手，本次进程关掉后不再自动弹
            MarkClaimConsumed();
            RedDot.RefreshCommon();   // 【2026-10-18 修·主人报「领完红点不灭」】领取后立即同步红点
            Debug.Log($"[DailyLogin] {msg}");
            // B12: 领完弹「获得奖励」弹窗（主人反馈：领了没弹窗，不知道拿到啥）。
            // 必须按**实发**构造：TryClaimStarter 内部对 X2 双倍日做了 Doubled，这里同样复算一遍
            // （不改 DailyLoginSystem 接口，UI 侧自算），保证弹窗数量 = 实际到账数量。
            var r = DailyLoginSystem.EffectiveStarterReward(day);
            bool dbl = DailyLoginDefs.IsStarterDouble(day);
            ShowRewardPopup(r, dbl ? 2 : 1);
        }
        Refresh();
    }

    void OnClickCycle()
    {
        string msg;
        // 领取**前**先取序号：TryClaimCycle 成功后会把 CycleIndex 往前推一格，之后再取就取到下一条了
        int idx = DailyLoginSystem.CycleIndex;
        bool ok = DailyLoginSystem.TryClaimCycle(out msg);
        GlobalToastUI.Show(msg);
        if (ok)
        {
            // 2026-10-15 主人拍板「领取后就不用再弹了」：循环奖领到手，本次进程关掉后不再自动弹
            MarkClaimConsumed();
            RedDot.RefreshCommon();   // 【2026-10-18 修】领取后立即同步红点
            Debug.Log($"[DailyLogin] {msg}");
            ShowRewardPopup(DailyLoginDefs.Cycle[idx], 1);
        }
        Refresh();
    }

    void OnClickStreak(int days)
    {
        string msg;
        var def = DailyLoginDefs.StreakFor(days);
        bool ok = DailyLoginSystem.TryClaimStreak(days, out msg);
        GlobalToastUI.Show(msg);
        if (ok)
        {
            // 2026-10-15 主人拍板「领取后就不用再弹了」：连签奖领到手，本次进程关掉后不再自动弹
            MarkClaimConsumed();
            RedDot.RefreshCommon();   // 【2026-10-18 修】领取后立即同步红点
            Debug.Log($"[DailyLogin] {msg}");
            if (def != null) ShowRewardPopup(def.Value.reward, 1);
        }
        Refresh();
    }

    /// <summary>
    /// B4: 底部信息条「累计大奖」领取入口（与 OnClickStreak 写法一致）。
    /// 走预制体已有 InfoBar 节点的 Button，不新建；领取结果弹 Toast 后刷新整页。
    /// </summary>
    void OnClickAccum()
    {
        string msg;
        bool ok = DailyLoginSystem.TryClaimAccum(out msg);
        GlobalToastUI.Show(msg);
        if (ok)
        {
            // 2026-10-15 主人拍板「领取后就不用再弹了」：累计大奖领到手，本次进程关掉后不再自动弹
            MarkClaimConsumed();
            RedDot.RefreshCommon();   // 【2026-10-18 修】领取后立即同步红点
            Debug.Log($"[DailyLogin] {msg}");
            // B12: 累计大奖同样弹「获得奖励」弹窗（主人反馈：领了没弹窗）
            ShowRewardPopup(DailyLoginDefs.AccumReward, 1);
        }
        Refresh();
    }
}
