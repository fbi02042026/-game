using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 宠物营救剧情（2026-11-21 定稿，2026-11-23 改战场演出）：第 1 章第 1 座大关第 3 小关（1-3），
/// 「第一波清完→进下一波前」插叙。分镜：狗被围攻 → 玩家上前 → 狗跑掉；末波精英战狗回归助战另在战斗侧挂。
///
/// 【2026-11-23 主人拍板】这段不再走全屏对白窗（「不是剧情几句话带过的」），改为<b>战场内演出</b>：
/// 场上生成一只挨打的狗 + 头顶气泡对白 + 刀光齐出 + 狗逃走，全程保持战斗视角。
/// 真源只此一处 C# 常量，不进 csv/prefab；台词口吻照项目现成调侃风（「我勒个豆」）。
/// 演出协程在 BattleManager.PetRescue.cs 的 CoPetRescueBattleScene（要访问 unitRoot/hero 等战斗私有域）。
/// </summary>
public static class PetRescueStoryBeats
{
    /// <summary>营救插叙已看标记 key：复用 SaveData 现成「剧情已看」出口（MarkIntruderStorySeen），不新增存档字段。</summary>
    public const string SeenKey = "petRescue_1_3";

    /// <summary>浅黄狗本体 id（营救成功即预解锁，真源 PetDefs.Def.id）。</summary>
    public const string DogId = "dog";

    // —— 战场演出台词（头顶气泡，真源只此一处）——
    /// <summary>第一拍：狗被围时呜咽。</summary>
    public const string DogWhimper = "呜……汪汪！";
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
}
