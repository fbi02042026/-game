using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 每日任务结算器（2026-10-29 新增；2026-11-25 主人拍板「周常不要单独设置」→ 周常分类取消，改为限定星期出现）。
///
/// 职责：读 quest.csv（category=日常），按存档计数器（SaveData.GetTaskCounter）判达标
/// → 自动发奖（主人拍板「达标自动发」，与「今日通关」同款；不做手动领取 UI）。
/// 每任务一个发放键防重复：按天 yyyyMMdd 记（存 SaveData.taskRewardClaimDay），跨天可再完成再发；
/// 出现规则由 MainQuestSystem.DailyRowActiveToday 统一判（weekdays 列 + 功能门槛），结算侧调同一出口。
///
/// 计数挂钩（写入侧，散在各咽喉；计数器本体在 SaveData.Count*）：
///   金币收入 = ResourceWallet.Add(Gold)；击败小怪/击杀Boss = BattleManager.OnMonsterDead；
///   获取佣兵 = SaveData.UnlockMerc（新解锁）；升星佣兵 = MercStarSystem.TryStarUp（成功）；
///   竞技场对战场次 = ArenaRules.ResolveChallenge（一场一计，不论胜负，2026-10-30 新增）；
///   喂养宠物 = PetUI.Actions.TryFeed（扣费成功）；点亮/升级天赋 = TalentSystem.TryUnlock/TryUpgradeRightNode（成功）；
///   活动副本 = ActivityDungeonRules.MarkCleared（四本共同出口）；裂隙 = BattleManager.SettleEndlessStageClear；
///   佣兵闲话 = MercFavorSystem.Talk（成功）。后四项 2026-11-25 新增（配套限定日任务）。
/// 开启门槛（2026-11-25 主人拍板）：日常整体在正式主线 1-3（精英）通关后开启，
///   真源 = SaveData.DailyTasksUnlocked；未开启时本类直接不结算、计数器不累加、任务栏/面板只显示解锁提示。
/// 结算驱动 = QuestBarBinder 每 0.5s 轮询（城镇内），奖励与 Toast 统一在城镇落地，不在战斗里弹。
/// daily_clear 不走本类（ChapterManager 独立发经验链路，2026-10-30 起改发经验不再发石）；daily_login 走通用路径
/// （2026-10-29 主人拍板「打开游戏就给奖励」：进度=loginLastDay==今天，不再强制点开登录弹窗领取）。
/// 奖励 = quest.csv 的 reward_stones（日常=天赋石）+ reward_exp（经验，
/// 走 LevelSystem.AddDirectExp，末列；改表即调）。
/// </summary>
    public static class DailyTaskSystem
    {
        /// <summary>失败局（阵亡/撤离）标记：BattleManager.TriggerLegacyFlow 在失败回城时置 true，
        /// 本 Settle 据此把「每日通关任务经验」(dailyClearExpPending) 压到约 1/3；成功局不置位照发全额。
        /// Settle 消费后清掉，避免污染后续成功局（即便本局 pending 为 0 也要清）。</summary>
        public static bool LastRunFailed { get; set; }

        /// <summary>结算一遍所有达标未发的任务奖励（幂等，可随时多调）。驱动方：任务栏 0.5s 轮询。</summary>
    public static void Settle()
    {
        var saveSys = SaveSystem.Instance;
        SaveData data = saveSys != null ? saveSys.Data : null;
        if (data == null) return;
        if (!QuestTable.HasData) return;

        // 【2026-11-25 主人拍板「把每日任务在第三关通关后开启」】未开启（正式主线 1-3 未通）一律不结算：
        // 任务栏/面板此时只显示解锁提示，计数器与自动发奖必须同口径关掉，否则玩家会在开启前就收到奖励。
        // LastRunFailed 照清，防止 1-3 那一局的失败标记污染解锁后的第一次成功补发。
        if (!data.DailyTasksUnlocked)
        {
            LastRunFailed = false;
            return;
        }

        data.EnsureDailyTaskKeys();

        // 【2026-11-12 主人拍板】每日通关任务经验回城再算，不与关卡经验连算：
        // 通关时只累加 dailyClearExpPending，本 Settle 在城镇轮询时补发；
        // 其它日常任务经验同受额度约束，升级弹窗走 LevelUpPopup.DeferUntilBattle 攒着，
        // 等玩家真打完一关再跟着结算一起弹（【2026-11-12】不再一登录就弹「恭喜升级」）。
        // 【2026-11-12 主人拍板】失败局（LastRunFailed）通关任务经验只发约 1/3，与成功局区分。
        if (data.dailyClearExpPending > 0)
        {
            int pending = data.dailyClearExpPending;
            data.dailyClearExpPending = 0;
            if (LastRunFailed)
                pending = Mathf.RoundToInt(pending / 3f);
            saveSys.Save();                       // 先落盘「已消费」，避免 AddDirectExp 内部 Save 前进程被杀导致重复发
            // 本关战斗里已升过级时，这笔只入账不升级，经验留到下一关（受 data.stageLevelUpCapLevel 额度约束）。
            // 【2026-11-12 主人拍板】额度只在「通关→回城」同一会话有效，载入存档时已清 0（SaveSystem.FinalizeLoadedData）；
            // 0 = 跨会话登录补发 → 按「这笔最多升 1 级」重算，否则额度过期会把该升的经验吞在账上。
            int cap = data.stageLevelUpCapLevel > 0 ? data.stageLevelUpCapLevel : data.playerLevel + 1;
            LevelSystem.AddDirectExp(pending, cap);
            // 【2026-11-14 主人拍板】「今日通关」是日常任务之一（SettleRows 里被 continue 跳过，
            // 奖在这儿发），它的完成提示同样走左侧横幅；经验按实际发放值写，失败局 1/3 不虚报。
            string clearTitle = QuestTable.TryDaily("daily_clear", out var clearRow) ? clearRow.title : null;
            QuestDoneToast.Show(clearTitle, "经验 ×" + pending);
            Debug.Log("[DailyTask] settle: daily_clear " + (LastRunFailed ? "失败局1/3 " : "") + "回城补发经验 +" + pending);
        }
        LastRunFailed = false;   // 无论本局有无 pending，失败标记都要清，防止漏清污染下一局成功补发

        SettleRows(data, "日常", int.Parse(DayKeyUtil.TodayKey()), daily: true);
        // 【2026-11-25】周常分类已取消（表里 0 行），这行保留只为空跑兼容；以后要恢复周常任务才用得上。
        SettleRows(data, "周常", SaveData.WeekMondayKey(), daily: false);
    }

    static void SettleRows(SaveData data, string category, int keyInt, bool daily)
    {
        var rows = QuestTable.RowsOfCategory(category);
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            // 【2026-11-25】限定日（weekdays）+ 功能门槛：与展示层同一出口
            // （MainQuestSystem.DailyRowActiveToday），今天不出现在列表里的行也不发奖。
            if (!MainQuestSystem.DailyRowActiveToday(row)) continue;
            if (row.id == "daily_clear") continue;   // 独立链路（ChapterManager 达标发经验，2026-10-30 起不再发天赋石），不在这里发
            // daily_login 从 2026-10-29 起走通用路径：进度=loginLastDay==今天（GetTaskCounter），打开游戏即达标自动发。
            int progress = data.GetTaskCounter(row.targetType);
            if (progress < row.targetCount) continue;
            if (data.IsTaskRewardClaimed(row.id, keyInt)) continue;
            if (row.rewardStones <= 0 && row.rewardExp <= 0 && row.rewardDiamond <= 0) continue;   // 表没配奖励就不发不吞（报一次让人看见）

            data.MarkTaskRewardClaimed(row.id, keyInt);

            // 发奖：天赋石（日常）/ 钻石（daily:false 分支；周常分类 2026-11-25 取消、分支留作兼容）+ 经验（2026-10-29 扩编）
            // 【2026-11-13 主人拍板「剧情后不要这一大段文字」】中央那条「完成任务XXX / 获得 …」两行汇总
            // 已整段删净，奖励照发、任务栏/升级弹窗照常反馈；ResourceWallet 仍 notify:false，别让它自己再弹一条。
            // 【2026-11-14 主人拍板】完成提示改由左侧滑入横幅承担（见下方 QuestDoneToast.Show）。
            if (row.rewardStones > 0)
            {
                var type = daily ? ResourceWallet.ResourceType.TalentPoint : ResourceWallet.ResourceType.Diamond;
                ResourceWallet.Add(type, row.rewardStones, save: true, notify: false);
            }
            // 【2026-11-25 主人拍板「选一条限定日给钻石，最好能看广告，现在不用看、以后可以接」】
            // reward_diamond 列独立于 reward_stones（后者的日常语义是天赋石，别混用）。
            // 以后接激励视频（看广告翻倍 / 看广告领取）就在这一支加分支：先把钻石按 0 发，
            // 看广告成功后再补发一份，广告次数与频次走现成口径（ArenaRules.AdBonusTries / ResourceAdRewards）。
            if (row.rewardDiamond > 0)
                ResourceWallet.Add(ResourceWallet.ResourceType.Diamond, row.rewardDiamond, save: true, notify: false);
            // 2026-11-12 主人拍板：其它日常任务按当前等级重算额度（单个任务最多升 1 级），
            // 多个任务累计可升好几级、弹窗逐个显示；不能用固定的关卡额度，否则主线通关后卡在满级之前。
            if (row.rewardExp > 0)
            {
                LevelSystem.AddDirectExp(row.rewardExp, data.playerLevel + 1);
            }

            // 【2026-11-14 主人拍板】任务提示统一走左侧滑入横幅「完成XX任务 / 获得XX奖励」，
            // 任务名与奖励文案都取 quest.csv 同一行（title / reward_text），不另写死文案。
            QuestDoneToast.Show(row.title, row.rewardText);

            Debug.Log("[DailyTask] settle: " + row.id + " +" + row.rewardStones + (daily ? " 石" : " 钻")
                + (row.rewardDiamond > 0 ? " +" + row.rewardDiamond + " 钻" : "")
                + (row.rewardExp > 0 ? " +" + row.rewardExp + " exp" : ""));
            Analytics.QuestComplete(row.id);
            MainQuestSystem.NotifyChanged();
        }
    }
}
