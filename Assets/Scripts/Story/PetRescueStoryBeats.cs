using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 宠物营救剧情（2026-11-21 定稿，2026-11-23 分镜重构）：第 1 章第 1 座大关第 3 小关（1-3）。
/// 【2026-11-23 主人拍板·完整分镜】不走全屏对白窗，全程战场内演出、保持战斗视角：
///   第一波照常打 → 第二波刷出时怪围着狗，狗"汪汪"+玩家喊话（对话步骤冻场，说完解冻）
///   → 狗被打跑（血空退场 + 逃跑旁白）→ 释放占用 →「首领来袭」大图 + 第 3 波精英出场
///   → 狗归队帮打精英 → 精英清完 → 狗叫两声 + 玩家"真是一只有趣的小狗"点评 → 胜利界面功能开启 toast。
/// 【波次】1-3 剧情关由 2 波拆成 3 波（总怪量不变：走 FormalStageMonsterTotal 按 3 波摊），
/// 末波 = 精英波（formalEliteSplit 天然生效），第二波 = 纯普通怪（狗陪打）。
/// 真源只此一处 C# 常量，不进 csv/prefab；台词口吻照项目现成调侃风（「我勒个豆」）。
/// 演出协程在 BattleManager.CutsceneDirector.cs（单协程按 BattleCutsceneDefs.DogRescue 步骤表驱动）。
/// 未通关时每次进 1-3 都演，通关后不再演（真源 = clearedStages）。
/// </summary>
public static class PetRescueStoryBeats
{
    /// <summary>营救插叙已看标记 key：复用 SaveData 现成「剧情已看」出口（MarkIntruderStorySeen），不新增存档字段。</summary>
    public const string SeenKey = "petRescue_1_3";

    /// <summary>
    /// 1-3 是否还需要播营救剧情（章节/座/小关 + 未通关）。唯一真源：
    /// 波次编排（WavePlanner.Plan 拆 3 波）与演出激活（BattleManager.TryPetRescueActivate）共用，
    /// 禁在别处再写第二份关卡判定。
    /// </summary>
    public static bool IsStage13Uncleared()
    {
        var cm = ChapterManager.Instance;
        if (cm == null || cm.currentChapter != 1 || cm.currentGrand != 1) return false;
        if (cm.CurrentSubIndex0 != 2) return false;   // 1-3（0 基 sub0=2）
        var save = SaveSystem.Instance?.Data;
        return save != null && !save.HasStageCleared(1, 2);
    }

    /// <summary>浅黄狗本体 id（营救成功即预解锁，真源 PetDefs.Def.id）。</summary>
    public const string DogId = "dog";

    // —— 战场演出台词（头顶气泡，真源只此一处）——
    /// <summary>第一拍：狗被围时呜咽（旧分镜保留，现用 DogBark）。</summary>
    public const string DogWhimper = "呜……汪汪！";
    /// <summary>【2026-11-23 分镜】狗被围着叫 / 战后叫两声（同一声汪汪，气泡贴狗）。</summary>
    public const string DogBark = "汪汪！汪汪！";
    /// <summary>【2026-11-23 分镜】战后玩家点评。</summary>
    public const string PlayerRemark = "真是一只有趣的小狗。";
    /// <summary>第二拍：玩家喊话上前。</summary>
    public const string PlayerShout = "哪里来的修狗？这里很危险啊！";
    /// <summary>第三拍：狗脱困后回应。</summary>
    public const string DogThanks = "汪汪！";
    /// <summary>狗跑走后玩家说的话（挂玩家头顶气泡，不铺全屏黑幕）。2026-11-23 主人「那句旁白太难听，说点正常的」。</summary>
    public const string DogFlee = "真是只奇怪的小狗。";

    /// <summary>演出时长档位（秒，unscaled）：气泡各拍停留 / 刀光到清场的停顿。</summary>
    public const float WhimperHold = 1.8f;
    public const float ShoutHold = 2.6f;
    public const float ThanksHold = 1.6f;
    public const float SlashHold = 0.35f;
    public const float RemarkHold = 2.2f;   // 战后玩家点评停留（2026-11-23 分镜）
    public const float FleeHold = 2.4f;     // 狗被打跑后的逃跑旁白停留（2026-11-23 分镜）
}
