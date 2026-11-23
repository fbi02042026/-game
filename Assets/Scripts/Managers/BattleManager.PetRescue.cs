using System;
using UnityEngine;

/// <summary>
/// 宠物营救剧情（1-3）——【2026-11-23 统一编排器重构后】本文件只剩两件事：
///   ① IsPetRescueStageUncleared：本关是否符合的唯一判定，波次编排（WavePlanner 拆 3 波）与
///      演出激活（TryPetRescueActivate）共用，禁在别处再写第二份关卡判定；
///   ② TryPetRescueActivate：第一波清完只做激活判定 + 置位（不插叙、不冻场）。
/// 演出主体（狗真参战上场 / 汪汪 / 玩家喊话 / 狗被打跑 / 首领来袭 / 狗归队 / 等清波 / 点评 / 放行）
/// 已全部迁到 BattleManager.CutsceneDirector.cs，由单协程按 BattleCutsceneDefs.DogRescue 步骤表驱动。
/// 旧版 4 协程 + phase 0~4 + 纯视觉狗 + 狗位真源特判已删除（正是 8 轮抢时序的根源）。
/// 【2026-11-23 主人拍板】旧「Boss 半血归队」（TickPetRescueRejoin + _petRescuePendingDeploy + 半血常量）
/// 已整段删除：1-3 没有 Boss，归队改由演出步骤 DogRejoinElite 在精英波出场后驱动（同一 DeployRescuedPet 出口）。
/// 未通关时每次进 1-3 都演，通关后不再演（真源 = clearedStages）。
/// </summary>
public partial class BattleManager
{
    bool _petRescueStageActive;      // 本关是否符合（1-3 未通关 + 正式主线 + 普通难度）

    /// <summary>
    /// 1-3 营救剧情关唯一判定：正式主线 + 普通难度 + 1-3 未通关。
    /// 消费方 = TryPetRescueActivate（演出激活）与 WavePlanner.Plan（波数 2→3），禁再写第二份。
    /// </summary>
    internal bool IsPetRescueStageUncleared
        => IsFormalMainRun && NormalOnlyContentAllowed(BattleDifficulty) && PetRescueStoryBeats.IsStage13Uncleared();

    /// <summary>第一波清完调用：只判定+置位，不插叙不冻场（第一波照常打、玩家继续往前走）。</summary>
    void TryPetRescueActivate()
    {
        if (_petRescueStageActive) return;
        if (!IsPetRescueStageUncleared) return;
        _petRescueStageActive = true;
        Debug.Log("[PetRescue] 1-3 未通关，狗剧情激活（等第二波单协程演出）");
    }
}
