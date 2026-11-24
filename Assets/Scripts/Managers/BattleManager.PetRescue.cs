using System;
using UnityEngine;

/// <summary>
/// 宠物营救剧情（整数制关 3 · 精英关）——【2026-11-23 统一编排器重构后】本文件只剩两件事：
///   ① IsPetRescueStageUncleared：本关是否符合的唯一判定，波次编排（WavePlanner 拆 3 波）与
///      演出激活（TryPetRescueActivate）共用，禁在别处再写第二份关卡判定；
///   ② TryPetRescueActivate：第一波清完激活 + 【2026-11-24】提前生成狗开打（不插叙、不冻场）。
/// 演出主体（玩家喊话 / 狗叫 / 怪锁玩家开打 / 狗主动跑掉 / 首领来袭 / 精英半血狗归队 / 等清波 / 点评 / 放行）
/// 已全部迁到 BattleManager.CutsceneDirector.cs，由单协程按 BattleCutsceneDefs.DogRescue 步骤表驱动。
/// 旧版 4 协程 + phase 0~4 + 纯视觉狗 + 狗位真源特判已删除（正是 8 轮抢时序的根源）。
/// 未通关时每次进关 3 都演，通关后不再演（真源 = clearedStages）。
/// </summary>
public partial class BattleManager
{
    bool _petRescueStageActive;      // 本关是否符合（整数制关 3 未通关 + 正式主线 + 普通难度）

    /// <summary>
    /// 狗营救剧情关唯一判定：正式主线 + 普通难度 + 关 3 未通关。
    /// 消费方 = TryPetRescueActivate（演出激活）与 WavePlanner.Plan（波数 2→3），禁再写第二份。
    /// </summary>
    internal bool IsPetRescueStageUncleared
        => IsFormalMainRun && NormalOnlyContentAllowed(BattleDifficulty) && PetRescueStoryBeats.IsDogRescueStage();

    /// <summary>
    /// 第一波清完调用（CoCheckWaveClearOrAdvance）：激活判定 + 置位 + 【2026-11-24 主人拍板】
    /// 提前生成狗开打——玩家打完第一波往前走时，看到的是狗已经在和怪战斗，不是走到了才凭空出现。
    /// 不冻场、不锁怪目标：狗与怪自然真打；玩家走到狗附近才由 Update → TryPlayDogRescueCutscene
    /// 触发冻场气泡。解锁存档也提前到这里（狗生成即解锁，演出启动时不再重复）。
    /// </summary>
    void TryPetRescueActivate()
    {
        if (_petRescueStageActive) return;
        if (!IsPetRescueStageUncleared) return;
        _petRescueStageActive = true;

        var rescueData = SaveSystem.Instance?.Data;
        if (rescueData != null)
        {
            rescueData.UnlockPet(PetRescueStoryBeats.DogId);
            rescueData.MarkIntruderStorySeen(PetRescueStoryBeats.SeenKey);
            SaveSystem.Instance.Save();
        }
        // 不传 spawnX → 落玩家前方怪群（ResolveRescueDogSpawnX：无活怪时落前方默认距），直接开打
        DeployRescuedPet();
        Debug.Log("[PetRescue] 狗营救关未通关，第一波清完狗提前入场开打（等玩家走近触发气泡）");
    }
}
