using System;
using System.Collections.Generic;

/// <summary>
/// 羁绊副本专属剧情（2026-11-09）：开场 / Boss 前 / 通关后三段。
/// 文案按花名册现有资料（姓名 / 昵称 / 职业）拼，C# 常量只此一处；
/// 不写存档标记、不预热点亮触发、不新增任何美术资源（立绘 / 背景都走现有叙事系统）。
/// 播放口 = StoryDirector.Ensure().Play，与终章追查关 PlayFinaleMid 同口径。
/// </summary>
public static class BondDungeonStoryBeats
{
    /// <summary>佣兵展示名（真源 = 花名册；查不到不兜底编名字，用「同伴」并留错误）。</summary>
    static string NameOf(string hireId)
    {
        if (MercRosterDefs.TryGetByHireId(hireId, out var d) && !string.IsNullOrEmpty(d.Name)) return d.Name;
        UnityEngine.Debug.LogError("[羁绊副本] 花名册查不到 " + hireId + "，剧情用「同伴」占位");
        return "同伴";
    }

    /// <summary>佣兵职业名（真源 = 花名册 JobName）。</summary>
    static string JobOf(string hireId)
        => MercRosterDefs.TryGetByHireId(hireId, out var d) && !string.IsNullOrEmpty(d.JobName)
            ? d.JobName : "冒险者";

    /// <summary>开场：进本后、首波前。黑幕字幕点名地点（不挂 .Bg()，无专属背景图）。</summary>
    public static bool PlayIntro(string hireId, Action onDone)
    {
        string name = NameOf(hireId);
        string job = JobOf(hireId);
        var beats = new List<StoryBeat>
        {
            StoryDirector.Narration("心防试炼：好感攒满才会打开的门，门后是" + name + "最不愿意被人看见的那段路。"),
            StoryDirector.Narration(name + "：「你真要进去？里面没什么好看的，就是我当" + job + "那会儿，没扛住的那几次。」"),
            // 【2026-11-11 主人拍板「剧情不要光叙事」】玩家的回应改成真对话（名牌 + 立绘），不再旁白装对白。
            StoryDirector.Solo("你", "那就一起再走一遍。这次不是你一个人。", StoryPortraits.Player),
            StoryDirector.Narration("门在身后合上。风里全是旧铁锈的味道。"),
        };
        var dir = StoryDirector.Ensure();
        if (dir == null)
        {
            UnityEngine.Debug.LogError("[羁绊副本] StoryDirector 取不到，开场剧情跳过");
            onDone?.Invoke();
            return false;
        }
        dir.Play(beats, onDone, null, false, name + "·心防试炼");
        return true;
    }

    /// <summary>Boss 前：Boss 波刷出之前播（战斗场景内，播完再走「首领来袭」大图）。</summary>
    public static bool PlayBeforeBoss(Action onDone)
    {
        var beats = new List<StoryBeat>
        {
            StoryDirector.Narration("路到头了。前面那团影子，是这段记忆自己长出来的形状。"),
            // 【2026-11-11 主人拍板「剧情不要光叙事」】玩家的惊呼改成真对话；影子抬头一拍加震屏。
            StoryDirector.Solo("你", "我勒个豆……这就是你一直不肯说的东西？", StoryPortraits.Player),
            StoryDirector.Narration("影子抬起头。它没有脸，却准确地看向了你身边那个人。").Shake(),
        };
        var dir = StoryDirector.Ensure();
        if (dir == null)
        {
            UnityEngine.Debug.LogError("[羁绊副本] StoryDirector 取不到，Boss 前剧情跳过");
            onDone?.Invoke();
            return false;
        }
        dir.Play(beats, onDone);
        return true;
    }

    /// <summary>通关后：Boss 死 → 剧情 → 回城（回城由回调负责）。</summary>
    public static bool PlayClear(Action onDone)
    {
        var beats = new List<StoryBeat>
        {
            StoryDirector.Narration("影子散了。地上只剩一枚旧徽章，边缘磨得发亮。"),
            StoryDirector.Narration("「走吧，回镇上。今天这顿我请。」"),
            StoryDirector.Narration("从这一刻起，有些话不用说，对方也知道了。"),
        };
        var dir = StoryDirector.Ensure();
        if (dir == null)
        {
            UnityEngine.Debug.LogError("[羁绊副本] StoryDirector 取不到，通关剧情跳过");
            onDone?.Invoke();
            return false;
        }
        dir.Play(beats, onDone);
        return true;
    }
}
