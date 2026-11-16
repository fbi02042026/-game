using UnityEngine;

/// <summary>
/// 竞技场「次数用完 → 看广告加次数」箭头引导（2026-11-16 主人拍板）：
/// 打开竞技场界面时若今日挑战次数已用完、且还能看广告加次数，弹一个箭头气泡指向顶部「看广告 +N」加号，
/// 提示玩家点它加次数。气泡在按钮下方、尖角朝上指按钮（用 TutorialHintUI 的一次性尖角朝上开关）。
///
/// 视觉全复用现成件：底框 = BattleManager.DraftHintBubble（「像素风空白对话框指针」，静态缓存）；
/// 载体 = TutorialHintUI 软引导（SetBannerSpriteOnce + SetBubblePointUpOnce + Show），
/// 不叠手指、不挡点击、autoHideSeconds 到点自收。零新增 UI / 图片 / 预制体 / 美术排版。
///
/// 🔴 ADS_ENABLED=false（广告未接入过渡期）期间加号按钮本身被 ArenaPopupUI 折叠隐藏
/// —— 本引导同步不弹（buttonRt 不可见即返回）；开关打开、按钮显示后自动生效，无需再改这里。
///
/// 账号全局只弹一次，标记存 SaveData.arenaAdHintShown（随云存档一致、清档归零）；
/// 只有气泡真的 Show 出来才写标记（照 MercStarReadyHint 口径）。
/// </summary>
public static class ArenaAdHint
{
    /// <summary>气泡文案（两行，400×192 气泡内居中）。</summary>
    public const string Text = "点这里看广告\n加挑战次数";
    /// <summary>气泡停留时长（秒）。</summary>
    const float HoldSeconds = 5f;

    /// <summary>
    /// 打开竞技场界面时调用一次。条件不满足安静返回；true = 本次真的弹出来了。
    /// </summary>
    public static bool TryShow(SaveData data, RectTransform adBtnRt)
    {
        // 广告档未开：加号按钮本就隐藏，不弹
        if (!GameConfig.ADS_ENABLED) return false;
        if (data == null || data.arenaAdHintShown) return false;
        // 次数没用完不用提示
        if (ArenaRules.TriesLeft(data) > 0) return false;
        // 今日广告加次数已满（按钮显示「已满」）→ 点了也没用，不弹
        if (data.arenaAdViewsToday >= ArenaRules.AdViewCapPerDay) return false;
        // 加号按钮此刻不可见（折叠/换页）→ 这次不弹、不写标记
        if (adBtnRt == null || !adBtnRt.gameObject.activeInHierarchy) return false;

        // 别的引导正占着这条横幅：这次不弹、不记标记，等下次进界面再试
        var existing = TutorialHintUI.Instance;
        if (existing != null && existing.IsVisible) return false;

        var bubble = BattleManager.DraftHintBubble;
        if (bubble == null)
        {
            Debug.LogError("[ArenaAdHint] 取不到底框 Sprite：UI/Art/Common/像素风空白对话框指针");
            return false;
        }

        var hint = TutorialHintUI.Ensure();
        if (hint == null) return false;
        hint.SetBannerSpriteOnce(bubble);
        hint.SetBubblePointUpOnce(true);   // 尖角朝上：气泡在按钮下方、指上方加号
        hint.SetSortOrderOnce(GameConfig.UiSort.TownPopup + 40);   // 竞技场弹窗是 TownPopup 层，抬到其上
        hint.Show(Text, adBtnRt, HoldSeconds);

        // Show 内部遇到战斗头顶台词会直接 return：只有真显示出来才记档
        if (!hint.IsVisible || hint.CurrentHint != Text)
        {
            hint.SetBannerSpriteOnce(null);
            hint.SetBubblePointUpOnce(false);
            return false;
        }

        data.arenaAdHintShown = true;
        SaveSystem.Instance?.Save();
        Debug.Log("[ArenaAdHint] 次数用完 → 看广告加次数引导已弹出（加号下方尖角朝上气泡）");
        return true;
    }
}
