using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 宠物营救剧情（2026-11-21 定稿）：第 1 章第 1 座大关第 3 小关（1-3），
/// 「第一波清完→进下一波前」插叙，3 拍封顶（规格第一节约束）。
/// 分镜：怪围攻浅黄狗 → 玩家上前营救 → 狗跑掉。末波精英战狗回归助战另在战斗侧挂。
///
/// 真源只此一处 C# 常量，不进 csv/prefab；台词口吻照项目现成调侃风（「我勒个豆」），
/// 命名「修勾」= 宠物真源名，不出现任何 IP 原名/谐音（规格红线）。
/// 播放走 StoryDirector.Ensure().Play 唯一出口；取不到导演时 LogError 后放行（不兜底静默）。
/// </summary>
public static class PetRescueStoryBeats
{
    /// <summary>营救插叙已看标记 key：复用 SaveData 现成「剧情已看」出口（MarkIntruderStorySeen），不新增存档字段。</summary>
    public const string SeenKey = "petRescue_1_3";

    /// <summary>浅黄狗本体 id（营救成功即预解锁，真源 PetDefs 的 id 字段）。
    /// 【2026-11-21 修复】原误写 prefabPath 的尾段 "Animal_Dog"，而 PetDefs/PetUI/存档判定一律用 "dog"，
    /// 导致营救成功后界面里狗仍锁着。真源唯一 = PetDefs.Def.id。</summary>
    public const string DogId = "dog";

    /// <summary>
    /// 播 1-3 营救插叙。返回 true = 已交给 StoryDirector 播放；
    /// 播放失败（取不到导演）已回调 onDone，调用方照常放行下一波。
    /// </summary>
    public static bool TryPlay(Action onDone)
    {
        // 【2026-11-21 用户拍板「注意剧情节奏：玩家说的话和宠物之间的互动」】玩家喊话 → 狗汪汪回应 → 收尾。
        // 狗没有剧情立绘（StoryPortraits 全是佣兵立绘），一律用旁白拟声，严禁 Solo 带 id 铺出占位灰盒。
        // 节奏仍按插叙收着：4 拍短句，一来一回即止，不铺成大段对白。
        var beats = new List<StoryBeat>
        {
            StoryDirector.Narration("草丛里传来细碎的呜咽——三只怪正围着一只浅黄狗，小家伙缩在墙角，尾巴都夹起来了。"),
            StoryDirector.Solo("你", "我勒个豆，几个打一个还要不要脸？松手，冲我来！", StoryPortraits.Player),
            StoryDirector.Narration("「汪汪！」小家伙先是一愣，接着绕着你脚边转了半圈，仰头冲你又叫了一声——像是道谢，又像在催你快跑。"),
            StoryDirector.BlackLine("怪散开逃了。小黄狗看了你一眼，叼着半截破布，一溜烟钻进草丛没了影。"),
        };

        Debug.Log("[PetRescueStory] 1-3 营救插叙 beats=" + beats.Count);
        var dir = StoryDirector.Ensure();
        if (dir == null)
        {
            Debug.LogError("[PetRescueStory] StoryDirector 取不到，营救插叙跳过（直接放行下一波）");
            onDone?.Invoke();
            return false;
        }
        dir.Play(beats, onDone);
        return true;
    }
}
