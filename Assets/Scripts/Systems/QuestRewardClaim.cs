using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 主线 / 支线任务奖励统一领取出口：任务完成瞬间不再发奖，
/// 由冒险日志「可领取」按钮调用本类发放。领取态走 SaveData 的 entries + HashSet 双写，
/// 旧档首次进入时把已完成任务一律标记为已领取，避免补发。
/// </summary>
public static class QuestRewardClaim
{
    /// <summary>
    /// 幂等迁移：把老存档里已完成的主线任务、已通关的支线岛全部视为已领取。
    /// questClaimMigrated 落盘，只跑一次；之后所有公开方法开头都会先调它。
    /// </summary>
    public static void EnsureMigrated()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null)
        {
            Debug.LogError("[QuestRewardClaim] EnsureMigrated: SaveData 为空");
            return;
        }
        if (data.questClaimMigrated) return;

        data.claimedMainQuestIds ??= new HashSet<string>();
        data.claimedSideIslandIds ??= new HashSet<int>();

        var all = MainQuestDefs.All;
        for (int i = 0; i < all.Length; i++)
        {
            var def = all[i];
            // 【2026-11-23】C1_Q0 是「首次引导·点日志领奖」的专属任务：它的 done 由 guideStep>=LogReward
            // 派生（MainQuestSystem.ProgressOf），预热/续跑时 IsDone 恒真，迁移会把奖励提前标成已领，
            // 导致引导走到日志页时领奖按钮不出现、引导秒结束。此任务排除出迁移，由引导让玩家亲手领。
            if (def.id == "C1_Q0") continue;
            if (MainQuestSystem.IsDone(def))
                data.claimedMainQuestIds.Add(def.id);
        }

        if (data.clearedSideIslandIds != null)
        {
            foreach (int id in data.clearedSideIslandIds)
                data.claimedSideIslandIds.Add(id);
        }

        data.questClaimMigrated = true;
        SaveSystem.Instance.Save();
    }

    public static bool IsClaimedMain(string questId)
    {
        EnsureMigrated();
        var data = SaveSystem.Instance?.Data;
        if (data == null)
        {
            Debug.LogError("[QuestRewardClaim] IsClaimedMain: SaveData 为空");
            return false;
        }
        data.claimedMainQuestIds ??= new HashSet<string>();
        return !string.IsNullOrEmpty(questId) && data.claimedMainQuestIds.Contains(questId);
    }

    public static bool IsClaimedSide(int islandId)
    {
        EnsureMigrated();
        var data = SaveSystem.Instance?.Data;
        if (data == null)
        {
            Debug.LogError("[QuestRewardClaim] IsClaimedSide: SaveData 为空");
            return false;
        }
        data.claimedSideIslandIds ??= new HashSet<int>();
        return data.claimedSideIslandIds.Contains(islandId);
    }

    /// <summary>主线任务已完成且未领取。</summary>
    public static bool CanClaimMain(string questId)
    {
        EnsureMigrated();
        var data = SaveSystem.Instance?.Data;
        if (data == null)
        {
            Debug.LogError("[QuestRewardClaim] CanClaimMain: SaveData 为空");
            return false;
        }
        if (!MainQuestDefs.TryFind(questId, out var def))
        {
            Debug.LogError("[QuestRewardClaim] CanClaimMain: 找不到主线任务定义 " + questId);
            return false;
        }
        data.claimedMainQuestIds ??= new HashSet<string>();
        return MainQuestSystem.IsDone(def) && !data.claimedMainQuestIds.Contains(questId);
    }

    /// <summary>支线岛已通关且未领取。</summary>
    public static bool CanClaimSide(int islandId)
    {
        EnsureMigrated();
        var data = SaveSystem.Instance?.Data;
        if (data == null)
        {
            Debug.LogError("[QuestRewardClaim] CanClaimSide: SaveData 为空");
            return false;
        }
        if (SideIslandDefs.Get(islandId) == null)
        {
            Debug.LogError("[QuestRewardClaim] CanClaimSide: 找不到支线岛定义 " + islandId);
            return false;
        }
        data.claimedSideIslandIds ??= new HashSet<int>();
        return SideIslandDefs.IsCleared(islandId, data) && !data.claimedSideIslandIds.Contains(islandId);
    }

    /// <summary>发放主线任务奖励：标记领取态 → 发金币/天赋石/经验 → 汇总弹窗 → 埋点 → 存档。
    /// 【2026-11-15 主人拍板「主线任务要给经验」】经验真源 = quest.csv 的 reward_exp（该章普通关卡经验的一半），
    /// 与通关关卡经验叠加不抵扣；领取即入账、升级当场弹，连续领取允许多级连升；奖励窗不展示经验。</summary>
    public static bool ClaimMain(string questId)
    {
        EnsureMigrated();
        if (!CanClaimMain(questId)) return false;
        var data = SaveSystem.Instance?.Data;
        if (data == null)
        {
            Debug.LogError("[QuestRewardClaim] ClaimMain: SaveData 为空");
            return false;
        }
        if (!MainQuestDefs.TryFind(questId, out var def))
        {
            Debug.LogError("[QuestRewardClaim] ClaimMain: 找不到主线任务定义 " + questId);
            return false;
        }
        data.claimedMainQuestIds ??= new HashSet<string>();
        if (!data.claimedMainQuestIds.Add(questId)) return false;

        long gotGold = 0, gotStone = 0;
        var overflow = new List<string>();
        if (def.rewardGold > 0)
        {
            var gold = ResourceWallet.Add(ResourceWallet.ResourceType.Gold, def.rewardGold, save: true, notify: false);
            if (gold.added > 0) gotGold += gold.added;
            if (gold.overflow > 0)
                overflow.Add(ResourceWallet.DisplayName(ResourceWallet.ResourceType.Gold) + "已满，溢出进邮箱");
        }
        if (def.rewardStones > 0)
        {
            var stone = ResourceWallet.Add(ResourceWallet.ResourceType.TalentPoint, def.rewardStones, save: true, notify: false);
            if (stone.added > 0) gotStone += stone.added;
            if (stone.overflow > 0)
                overflow.Add(ResourceWallet.DisplayName(ResourceWallet.ResourceType.TalentPoint) + "已满，溢出进邮箱");
        }
        // 【2026-11-12 主人拍板】领奖统一弹「获得奖励」窗；溢出提示仍走 Toast（不是奖励本体）
        // 【2026-11-23 修「奖励窗和升级窗叠在一起」】顺序反转：先弹奖励窗，升级窗由关窗回调再弹。
        // 根因：旧顺序先 AddDirectExp(showLevelUpNow:true) → LevelUpPopup.FlushPending 当场弹 996，
        // 再 ShowClaimRewards 弹 905，两窗同帧并存、升级窗整个盖住奖励窗（关两次才干净）。
        // 现在：奖励窗在屏时 LevelUpPopup.FlushPending 会让位（见其方法内 IsShowing 闸），
        // 升级整段留在待弹队列，玩家点掉奖励窗 → onClosed → FlushPending 才弹，与结算链
        // 「奖励展示完再弹升级窗」的既有口径（BattleManager.Settlement）一致。
        // 奖励清单为空时 Show(List,Action) 会直接回调，FlushPending 照常立刻弹，不会卡住。
        RewardPopupUI.ShowClaimRewards(gotGold, 0, gotStone, 0, null, LevelUpPopup.FlushPending);

        // 【2026-11-15 主人拍板「主线任务要给经验」】经验在领取时发放（不在完成瞬间），
        // capLevel=0 不封顶 → 一次领奖可连升；showLevelUpNow=true → 升级当场弹、关掉后可接着领下一条。
        // 奖励窗按拍板不展示经验；主线经验不在 DailyTaskSystem.SettleRows 里发，两者互不干扰。
        // 入账与存档必须留在关窗之前（玩家不点确定就退游戏，经验不能丢）；弹窗由上面那行回调负责。
        if (def.rewardExp > 0)
            LevelSystem.AddDirectExp(def.rewardExp, 0, showLevelUpNow: true);
        if (overflow.Count > 0)
            GlobalToastUI.ShowQueued(string.Join("\n", overflow));

        Analytics.QuestComplete(questId); // 埋点：主线任务奖励领取（原在 Complete 完成瞬间）
        SaveSystem.Instance.Save();
        return true;
    }

    /// <summary>发放支线岛奖励：局外直发金币/天赋石（不走局内 GrantRunGold），标记领取态 + 存档。</summary>
    public static bool ClaimSide(int islandId)
    {
        EnsureMigrated();
        if (!CanClaimSide(islandId)) return false;
        var data = SaveSystem.Instance?.Data;
        if (data == null)
        {
            Debug.LogError("[QuestRewardClaim] ClaimSide: SaveData 为空");
            return false;
        }
        var island = SideIslandDefs.Get(islandId);
        if (island == null)
        {
            Debug.LogError("[QuestRewardClaim] ClaimSide: 找不到支线岛定义 " + islandId);
            return false;
        }
        data.claimedSideIslandIds ??= new HashSet<int>();
        if (!data.claimedSideIslandIds.Add(islandId)) return false;

        long gotGold = 0, gotStone = 0;
        var overflow = new List<string>();
        if (island.goldReward > 0)
        {
            var gold = ResourceWallet.Add(ResourceWallet.ResourceType.Gold, island.goldReward, save: true, notify: false);
            if (gold.added > 0) gotGold += gold.added;
            if (gold.overflow > 0)
                overflow.Add(ResourceWallet.DisplayName(ResourceWallet.ResourceType.Gold) + "已满，溢出进邮箱");
        }
        if (island.talentReward > 0)
        {
            var stone = ResourceWallet.Add(ResourceWallet.ResourceType.TalentPoint, island.talentReward, save: true, notify: false);
            if (stone.added > 0) gotStone += stone.added;
            if (stone.overflow > 0)
                overflow.Add(ResourceWallet.DisplayName(ResourceWallet.ResourceType.TalentPoint) + "已满，溢出进邮箱");
        }
        // 【2026-11-12 主人拍板】领奖统一弹「获得奖励」窗；溢出提示仍走 Toast（不是奖励本体）
        RewardPopupUI.ShowClaimRewards(gotGold, 0, gotStone, 0);
        if (overflow.Count > 0)
            GlobalToastUI.ShowQueued(string.Join("\n", overflow));

        SaveSystem.Instance.Save();
        return true;
    }
}
