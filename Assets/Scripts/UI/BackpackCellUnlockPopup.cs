using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 背包锁格钻石解锁确认弹窗（2026-10-23 主人拍板）。
/// 角色页点击未解锁（带锁图标）的格子 → 确认「是否花费 5 钻石解锁当前格子？」→
/// 确认：扣 5 钻石（唯一出口 ResourceWallet.TrySpend；不足时其内部已 toast「钻石不足」，
/// 此处直接中止，格子保持锁定、任何数据都不动，fail closed）→
/// 记入 SaveData.backpackUnlockedCells → 存档 → toast「解锁成功」→ 回调刷新网格。
/// 底层直接走 StaminaConfirmPopup（默认弹窗预制体 EvacuateConfirmPopupUI 的同一实例，
/// Ensure/DDOL/刷新重绑都在那边），本类只负责拼文案与确认后的扣费落账，不另建 host。
/// </summary>
public static class BackpackCellUnlockPopup
{
    /// <summary>
    /// 弹「解锁格子」确认框。logicalColumns 传调用网格的逻辑列数（角色页 = 8），
    /// 保证存档索引 gridY * logicalColumns + gridX 与 GameConfig.IsBackpackCellUnlocked 的口径一致。
    /// onUnlocked 在扣费 + 落账成功后回调（调用方一般传网格自己的 Refresh）。
    /// </summary>
    public static void Show(int gridX, int gridY, int logicalColumns, Action onUnlocked)
    {
        // 【2026-11-25 主人拍板「双通道 · 先广告后钻石」】广告能播 → 免费解锁；播不了 → 原 5 钻确认框。
        // 两条通道共用同一个确认弹窗皮（硬规则：不新建弹窗），只换文案与确认键文字。
        if (AdGateway.Ready)
        {
            StaminaConfirmPopup.Show("解锁背包格子",
                "看一段广告，免费解锁当前格子？",
                "看广告解锁",
                () => WatchAdThenUnlock(gridX, gridY, logicalColumns, onUnlocked));
            return;
        }

        string body = $"是否花费 {GameConfig.BACKPACK_CELL_UNLOCK_DIAMOND} 钻石解锁当前格子？";
        StaminaConfirmPopup.Show("解锁背包格子", body, "确认解锁",
            () => ConfirmUnlock(gridX, gridY, logicalColumns, onUnlocked));
    }

    /// <summary>
    /// 广告通道：核验通过（真看完）才落账；没看完 / 拉不起来一律不解锁、不扣钻，
    /// 玩家再点一次即可走钻石通道（Show 会按当时 Ready 重新选路）。
    /// </summary>
    static void WatchAdThenUnlock(int gridX, int gridY, int logicalColumns, Action onUnlocked)
    {
        AdGateway.TryWatch(AdGateway.PlacementBagCell, ok =>
        {
            if (!ok)
            {
                UIManager.Instance?.ShowToast("激励未完成，格子未解锁", true);
                return;
            }
            ApplyUnlock(gridX, gridY, logicalColumns, onUnlocked);
            Analytics.AdSlotClick(AdGateway.PlacementBagCell, true);
        });
    }

    static void ConfirmUnlock(int gridX, int gridY, int logicalColumns, Action onUnlocked)
    {
        var save = SaveSystem.Instance;
        var data = save?.Data;
        if (data == null)
        {
            // 不静默兜底：没有存档就落不了账，必须喊出来
            Debug.LogError("[BagCellUnlock] SaveData 为空，无法执行背包格子钻石解锁");
            return;
        }

        // 2026-10-23 主人拍板：背包锁格钻石解锁（5钻/格）。扣费走 TrySpendDiamond 唯一出口
        //（2026-10-24 主人拍板：钻石扣费收口 + 本弹窗即二次确认）；钻石不足时内部已 toast，此处直接 return（fail closed）。
        // save:false —— 扣费与落账合并成一次存档。
        if (!ResourceWallet.TrySpendDiamond(GameConfig.BACKPACK_CELL_UNLOCK_DIAMOND, "背包格子解锁", save: false))
            return;

        ApplyUnlock(gridX, gridY, logicalColumns, onUnlocked);
    }

    /// <summary>
    /// 纯落账：置位 + 存档 + toast + 回调。钻石/广告两条通道共用，本方法**不碰任何货币**，
    /// 扣费与发奖谁先谁后由调用方保证（钻石=先扣后落，广告=核验通过后落）。
    /// </summary>
    static void ApplyUnlock(int gridX, int gridY, int logicalColumns, Action onUnlocked)
    {
        var save = SaveSystem.Instance;
        var data = save?.Data;
        if (data == null)
        {
            Debug.LogError("[BagCellUnlock] SaveData 为空，无法落账背包格子解锁");
            return;
        }

        data.backpackUnlockedCells ??= new List<int>();
        int index = gridY * logicalColumns + gridX;
        if (!data.backpackUnlockedCells.Contains(index))
            data.backpackUnlockedCells.Add(index);
        if (save != null)
            save.Save();

        UIManager.Instance?.ShowToast("解锁成功", true);
        onUnlocked?.Invoke();
    }
}
