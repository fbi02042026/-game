using System;

/// <summary>
/// 战斗内演出步骤真源（2026-11-23 统一编排器架构 / 解耦达）。
/// 把散落在 4 个文件、多个协程、phase 0~4 + 5 个 bool 的抢时序，收敛成一张有序步骤表 + 单协程解释器。
/// 纯数据、无逻辑，与项目现成 C# Defs 表同款（不引入 ScriptableObject——项目无 SO 资产管线）。
/// </summary>
public enum BattleCutsceneStep
{
    SpawnPetFight,      // 狗作为真战斗单位上场（DeployRescuedPet → SpawnBattlePet 唯一出口）
    BarkAtDog,          // 狗"汪汪"气泡（贴狗锚点，unscaled，LateUpdate 跟随）
    ShoutAtHero,        // 玩家喊话气泡（贴玩家，自动播）
    WaitWaveCleared,    // 等本波怪全清（不冻场；波次闸由 IsOccupyingBattlefield 拦住）
    BarkThanksAtDog,    // 战后狗叫两声（贴狗锚点）
    RemarkAtHero,       // 玩家"真是一只有趣的小狗"点评（贴玩家）
    Release,            // 释放战场占用标志，下一帧自然放行首领来袭
}

public static class BattleCutsceneDefs
{
    /// <summary>1-3 狗营救演出链（顺序即分镜；真源只此一处）。</summary>
    public static readonly BattleCutsceneStep[] DogRescue = new BattleCutsceneStep[]
    {
        BattleCutsceneStep.SpawnPetFight,
        BattleCutsceneStep.BarkAtDog,
        BattleCutsceneStep.ShoutAtHero,
        BattleCutsceneStep.WaitWaveCleared,
        BattleCutsceneStep.BarkThanksAtDog,
        BattleCutsceneStep.RemarkAtHero,
        BattleCutsceneStep.Release,
    };
}