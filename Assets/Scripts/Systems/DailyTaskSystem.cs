using UnityEngine;

/// <summary>
/// 每日/周常任务结算器（2026-10-29 新增，主人拍板扩编任务种类）。
///
/// 职责：读 quest.csv（category=日常/周常），按存档计数器（SaveData.GetTaskCounter）判达标
/// → 自动发奖（主人拍板「达标自动发」，与「今日通关」同款；不做手动领取 UI）。
/// 每任务一个发放键防重复：日任务 = 今天 yyyyMMdd、周任务 = 本周周一 yyyyMMdd
/// （存 SaveData.taskRewardClaimDay，跨天/跨周后同任务可再完成再发）。
///
/// 计数挂钩（写入侧，散在各咽喉；计数器本体在 SaveData.Count*）：
///   金币收入 = ResourceWallet.Add(Gold)；击败小怪/击杀Boss = BattleManager.OnMonsterDead；
///   获取佣兵 = SaveData.UnlockMerc（新解锁）；升星佣兵 = MercStarSystem.TryStarUp（成功）；
///   竞技场对战场次 = ArenaRules.ResolveChallenge（一场一计，不论胜负，2026-10-30 新增）。
/// 结算驱动 = QuestBarBinder 每 0.5s 轮询（城镇内），奖励与 Toast 统一在城镇落地，不在战斗里弹。
/// daily_clear 不走本类（ChapterManager 独立发经验链路，2026-10-30 起改发经验不再发石）；daily_login 走通用路径
/// （2026-10-29 主人拍板「打开游戏就给奖励」：进度=loginLastDay==今天，不再强制点开登录弹窗领取）。
/// 奖励 = quest.csv 的 reward_stones（日常=天赋石、周常=钻石）+ reward_exp（经验，
/// 走 LevelSystem.AddDirectExp，末列；改表即调）。
/// </summary>
public static class DailyTaskSystem
{
    /// <summary>结算一遍所有达标未发的任务奖励（幂等，可随时多调）。驱动方：任务栏 0.5s 轮询。</summary>
    public static void Settle()
    {
        var saveSys = SaveSystem.Instance;
        SaveData data = saveSys != null ? saveSys.Data : null;
        if (data == null) return;
        if (!QuestTable.HasData) return;

        data.EnsureDailyTaskKeys();
        SettleRows(data, "日常", int.Parse(ShopDefs.TodayKey()), daily: true);
        SettleRows(data, "周常", SaveData.WeekMondayKey(), daily: false);
    }

    static void SettleRows(SaveData data, string category, int keyInt, bool daily)
    {
        var rows = QuestTable.RowsOfCategory(category);
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.id == "daily_clear") continue;   // 独立链路（ChapterManager 达标发经验，2026-10-30 起不再发天赋石），不在这里发
            // daily_login 从 2026-10-29 起走通用路径：进度=loginLastDay==今天（GetTaskCounter），打开游戏即达标自动发。
            int progress = data.GetTaskCounter(row.targetType);
            if (progress < row.targetCount) continue;
            if (data.IsTaskRewardClaimed(row.id, keyInt)) continue;
            if (row.rewardStones <= 0 && row.rewardExp <= 0) continue;   // 表没配奖励就不发不吞（报一次让人看见）

            data.MarkTaskRewardClaimed(row.id, keyInt);

            // 发奖：天赋石（日常）/ 钻石（周常）+ 经验（2026-10-29 扩编，主人拍板「有些奖励可以给些经验」）
            if (row.rewardStones > 0)
                ResourceWallet.Add(daily ? ResourceWallet.ResourceType.TalentPoint : ResourceWallet.ResourceType.Diamond,
                    row.rewardStones, save: true, notify: true);
            if (row.rewardExp > 0)
                LevelSystem.AddDirectExp(row.rewardExp);

            var rewardParts = new System.Collections.Generic.List<string>(2);
            if (row.rewardStones > 0) rewardParts.Add((daily ? "天赋石×" : "钻石×") + row.rewardStones);
            if (row.rewardExp > 0) rewardParts.Add("经验×" + row.rewardExp);
            GlobalToastUI.Show((daily ? "日常任务完成：" : "周任务完成：") + row.title + "　获得 " + string.Join("、", rewardParts));

            Debug.Log("[DailyTask] settle: " + row.id + " +" + row.rewardStones + (daily ? " 石" : " 钻")
                + (row.rewardExp > 0 ? " +" + row.rewardExp + " exp" : ""));
            Analytics.QuestComplete(row.id);
            MainQuestSystem.NotifyChanged();
        }
    }
}
