using System;
using UnityEngine;

/// <summary>
/// TapTap 好评引导（2026-10-25 主人拍板：做 C 导流、不做邀请码）。
///
/// 触发：章末 Boss 通关（BattleManager.SettleNormalStageClear 置 <see cref="PendingPrompt"/>）
/// → 回城后 TownHubController.Start 消费（先例 = PendingOpenAdventure）。
/// 每日最多弹一次（SaveData.taptapRateDayKey，UTC yyyyMMdd，模式同 ResourceAdRewards.EnsureDay；
/// 弹过即记，玩家点取消也占当日名额，防骚扰）。
///
/// 弹窗复用默认确认弹窗（StaminaConfirmPopup，Ensure/DDOL/重绑都在那边），本类零 UI 新建。
/// 点「去好评」→ 跳 TapTap 商店详情页（GameConfig.TAPTAP_STORE_URL，**链接待主人提供**，
/// 为空 = LogError 不弹，不静默、不造 URL）。
/// </summary>
public static class TapTapRatePopup
{
    /// <summary>章末通关置位，回城后由 TownHubController.ConsumePendingPrompt 消费（跨场景静态位）。</summary>
    public static bool PendingPrompt;

    /// <summary>回城消费点（TownHubController.Start 调）。
    /// 【2026-10-25 主人拍板】总开关 GameConfig.TAPTAP_RATE_ENABLED=false 期间整体停用
    /// （清残留标记直接返回，不弹不报错；恢复时改开关即可）。</summary>
    public static void ConsumePendingPrompt()
    {
        if (!GameConfig.TAPTAP_RATE_ENABLED)
        {
            PendingPrompt = false;
            return;
        }
        if (!PendingPrompt) return;
        PendingPrompt = false;
        MaybeShow("chapter_boss_clear");
    }

    /// <summary>弹好评引导（每日一次；链接未配 = LogError 不弹并占当日名额与否见下）。</summary>
    public static void MaybeShow(string fromScene)
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null)
        {
            Debug.LogError("[TapTapRatePopup] SaveData 为空，好评引导弹不了（不发不吞）");
            return;
        }
        if (string.IsNullOrEmpty(GameConfig.TAPTAP_STORE_URL))
        {
            // 缺真值不静默（铁律 14）：不写日键，主人补上链接后当天即可生效
            Debug.LogError("[TapTapRatePopup] GameConfig.TAPTAP_STORE_URL 为空（待主人提供 TapTap 商店详情页链接），好评引导不弹");
            return;
        }

        string today = DateTime.UtcNow.ToString("yyyyMMdd");
        if (data.taptapRateDayKey == today) return;
        data.taptapRateDayKey = today;   // 弹过即记（点取消也占当日名额，防骚扰）

        GameAnalytics.ReportTapTapRateShow(fromScene);
        StaminaConfirmPopup.Show("给个好评",
            "如果喜欢《像素冒险：裂隙之刃》，去 TapTap 给我们打个分吧！",
            "去好评",
            () =>
            {
                GameAnalytics.ReportTapTapRateClick(fromScene);
                Application.OpenURL(GameConfig.TAPTAP_STORE_URL);
                GameAnalytics.ReportTapTapOpen("store");   // OpenURL 无回调，口径=已发起跳转
            });
    }
}
