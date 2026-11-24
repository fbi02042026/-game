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
    WaitDogFled,        // 等狗被打跑（血空退场；看门狗超时强制撤场，绝不把演出卡死）
    DogFleeLine,        // 狗被打跑后的逃跑旁白（贴玩家走气泡，不带黑幕）
    WaitSecondWaveCleared, // 等第 2 波（狗陪打那波）清完再放首领来袭，否则精英又和残怪混在一起
    ReleaseForElite,    // 释放战场占用：放行「首领来袭」大图 + 精英末波（演出本身继续）
    WaitEliteInView,    // 等精英走进战斗镜头再让狗归队（免得狗对着屏外精英发愣）
    DogRejoinElite,     // 狗归队帮打精英（DeployRescuedPet，落怪群前方）
    WaitWaveCleared,    // 等本波怪全清（全程不冻场，狗与怪照常打；波次闸由 IsOccupyingBattlefield 拦住）
    BarkThanksAtDog,    // 战后狗叫两声（贴狗锚点）
    RemarkAtHero,       // 玩家"真是一只有趣的小狗"点评（贴玩家）
    Release,            // 释放战场占用标志，下一帧自然放行首领来袭
}

public static class BattleCutsceneDefs
{
    /// <summary>
    /// 关 3（精英）狗营救演出链（顺序即分镜；真源只此一处）。
    /// 【2026-11-24 主人拍板「狗别跑了，跟着玩家一直打」】简化：第一波刚清完狗已提前入场（TryPetRescueActivate 生成）→
    /// 玩家走到狗入镜 → 冻场玩家喊话 + 狗叫 → 解冻（怪锁玩家）开打 → 第 2 波清完 → 放行首领来袭 →
    /// 精英清完 → 战后狗叫 + 玩家点评。狗全程在场跟着打，不再跑掉/归队
    /// （去掉 DogFleeLine / WaitEliteInView / DogRejoinElite；Pet 侧血空回血续战，绝不退场）。
    /// </summary>
    public static readonly BattleCutsceneStep[] DogRescue = new BattleCutsceneStep[]
    {
        BattleCutsceneStep.ShoutAtHero,        // 冻场段首：玩家喊话
        BattleCutsceneStep.BarkAtDog,          // 冻场段尾：狗叫两声（说完解冻，怪锁玩家开打）
        BattleCutsceneStep.WaitWaveCleared,    // 等第 2 波（狗陪打）清完，期间怪锁玩家
        BattleCutsceneStep.ReleaseForElite,    // 释放占用 → 首领来袭（清怪锁）
        BattleCutsceneStep.WaitWaveCleared,    // 等第 3 波（精英）清完
        BattleCutsceneStep.BarkThanksAtDog,    // 冻场：战后狗叫（狗全程在场，贴真狗）
        BattleCutsceneStep.RemarkAtHero,       // 冻场：玩家点评
        BattleCutsceneStep.Release,
    };
}
