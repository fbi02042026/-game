using System.Collections.Generic;

/// <summary>
/// 冒险页「获得奖励」弹窗的待消费队列（2026-10-22 新增）。
///
/// 链路：主线普通关通关（BattleManager.FinishStageAfterPortalReached → 仅首次通关）
///       → Push 一条本关实际掉落 → 玩家回到城镇打开冒险页
///       → 【2026-10-24 主人拍板】改为「点击宝箱领取」时才消费：
///         RefreshChestNodes 用 LatestPendingStageInChapter 找刚通关的槽位播「关闭→开启」动效，
///         玩家点宝箱 → Take(章, 关) 取走该条 → RewardPopupUI 弹「获得奖励」。
///
/// · 只记主线冒险（Normal 模式）：金币本 / 无限裂隙 / 引导局各有自己的结算链，不进本队列。
/// · 静态内存队列：战斗场景 → 城镇场景跨场景存活；进程被杀则丢（掉落的金币/物品本身已入存档，弹窗只是展示）。
/// · 弹窗内容与战斗结算同账（BattleRunStats 计数差值），不新造数值。
/// </summary>
public static class AdventureRewardNotice
{
    /// <summary>一条「某小关首次通关」的掉落记录。</summary>
    public class StageClear
    {
        public int chapter;        // 章号（1 起）
        public int stageIndex;     // 小关下标（0 起）
        public long gold;          // 本关实际入账金币（局内 + 局外合计，与结算同账）
        public int talent;         // 天赋石
        public int enchant;        // 附魔石
        public int mats;           // 强化材料
        public readonly List<EquipGainEntry> equips = new List<EquipGainEntry>();
    }

    static readonly List<StageClear> _pending = new List<StageClear>();

    public static bool HasPending => _pending.Count > 0;

    public static void Push(StageClear notice)
    {
        if (notice == null) return;
        _pending.Add(notice);
    }

    /// <summary>
    /// 该章最新一条待领记录的小关下标（0 起；没有返回 -1）。
    /// 从队尾往回找：Push 顺序 = 通关顺序，队尾即刚打完的关（动效只给「最新通关」播）。
    /// </summary>
    public static int LatestPendingStageInChapter(int chapter)
    {
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            var n = _pending[i];
            if (n != null && n.chapter == chapter) return n.stageIndex;
        }
        return -1;
    }

    /// <summary>取走某关的待领记录（调用后即移除，只弹一次）；没有返回 null。</summary>
    public static StageClear Take(int chapter, int stageIndex)
    {
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            var n = _pending[i];
            if (n == null || n.chapter != chapter || n.stageIndex != stageIndex) continue;
            _pending.RemoveAt(i);
            return n;
        }
        return null;
    }
}
