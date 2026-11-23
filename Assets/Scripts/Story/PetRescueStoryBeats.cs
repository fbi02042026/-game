using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 宠物营救剧情（2026-11-21 定稿，2026-11-23 分镜重构）：第 1 章第 1 座大关第 3 小关（1-3）。
/// 【2026-11-23 主人拍板·完整分镜】不走全屏对白窗，全程战场内演出、保持战斗视角：
///   第一波照常打 → 第二波刷出时怪围着狗，狗"汪汪"+玩家喊话（冻场自动播）→ 解冻打第二波（狗常驻）
///   → 第二波清完 → 狗叫两声往后跑走 + 玩家"真是一只有趣的小狗"点评（冻场）→ 首领来袭 → Boss 半血狗归队帮忙。
/// 真源只此一处 C# 常量，不进 csv/prefab；台词口吻照项目现成调侃风（「我勒个豆」）。
/// 演出协程在 BattleManager.PetRescue.cs（CoPetRescuePreBattle / CoPetRescuePostBattle，要访问 unitRoot/hero 等战斗私有域）。
/// </summary>
public static class PetRescueStoryBeats
{
    /// <summary>营救插叙已看标记 key：复用 SaveData 现成「剧情已看」出口（MarkIntruderStorySeen），不新增存档字段。</summary>
    public const string SeenKey = "petRescue_1_3";

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
    public const string PlayerShout = "我勒个豆，几个打一个还要不要脸？松手，冲我来！";
    /// <summary>第三拍：狗脱困后回应。</summary>
    public const string DogThanks = "汪汪！";
    /// <summary>狗逃走时的旁白（走头顶气泡，不铺全屏黑幕）。</summary>
    public const string DogFlee = "小黄狗叼着半截破布，一溜烟钻进草丛没了影。";

    /// <summary>演出时长档位（秒，unscaled）：气泡各拍停留 / 刀光到清场的停顿。</summary>
    public const float WhimperHold = 1.8f;
    public const float ShoutHold = 2.6f;
    public const float ThanksHold = 1.6f;
    public const float SlashHold = 0.35f;
    public const float RemarkHold = 2.2f;   // 战后玩家点评停留（2026-11-23 分镜）
}
