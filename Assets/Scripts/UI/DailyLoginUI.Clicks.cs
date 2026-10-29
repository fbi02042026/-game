using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 每日登录弹窗 partial —— 2026-10-18 由 DailyLoginUI.cs 纯位移拆出（无逻辑改动）
// 职责：领取点击（天数/循环/连击三类领取回调）
// 【2026-10-29 主人拍板 B】累计大奖领取入口已撤（底部 InfoBar 改纯进度展示）：原 OnClickAccum 删除；
// 领取逻辑 TryClaimAccum 保留在 DailyLoginSystem（现无调用点，备用）。
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
            // 【2026-10-20】用 TryClaimStarter 记下的基础奖励（LastStarterBaseReward）——
            // 领取动作可能解锁佣兵（D1 塔克），此时再取 EffectiveStarterReward 会二次折算成碎片，与实发不符。
            // X2 双倍日的数量翻倍照旧由 dbl 参数表达（LastStarterBaseReward 是 Doubled 前的基础量）。
            var r = DailyLoginSystem.LastStarterBaseReward.grant != DailyLoginDefs.Grant.None
                ? DailyLoginSystem.LastStarterBaseReward
                : DailyLoginSystem.EffectiveStarterReward(day);
            bool dbl = DailyLoginDefs.IsStarterDouble(day);
            // 【2026-10-20 主人拍板】实发佣兵本体 → 先播「获得佣兵」全屏演出，关闭后再弹奖励弹窗
            if (DailyLoginSystem.LastGrantedMercHireId != null)
                MercGainCinematicUI.Show(DailyLoginSystem.LastGrantedMercHireId, () => ShowRewardPopup(r, dbl ? 2 : 1));
            else
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
            // 实发佣兵本体 → 先播「获得佣兵」演出（同新手格口径）
            if (DailyLoginSystem.LastGrantedMercHireId != null)
                MercGainCinematicUI.Show(DailyLoginSystem.LastGrantedMercHireId, () => ShowRewardPopup(DailyLoginDefs.Cycle[idx], 1));
            else
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
            // 实发佣兵本体 → 先播「获得佣兵」演出（同新手格口径）
            if (def != null && DailyLoginSystem.LastGrantedMercHireId != null)
                MercGainCinematicUI.Show(DailyLoginSystem.LastGrantedMercHireId, () => ShowRewardPopup(def.Value.reward, 1));
            else if (def != null)
                ShowRewardPopup(def.Value.reward, 1);
        }
        Refresh();
    }

    // 【2026-10-29 主人拍板 B】OnClickAccum（底部信息条累计大奖领取）已随入口一并删除，
    // 见文件头注释；满 8 天底部条只当天数进度提示，不可点、不给奖。
}
