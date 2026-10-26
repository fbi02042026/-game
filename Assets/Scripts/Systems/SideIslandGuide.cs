using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 支线岛情报指引（2026-10-26 主人拍板「第一章可以出现支线，并出现一个佣兵信息窗口那个对话指引框，
/// 告诉玩家这个支线的名字、可进行探索——比如第七小队的信息什么的」）。
///
/// · 触发：主界面 TownSceneBootstrap.Update 每 0.5s 轮询调 <see cref="TryShowForCurrent"/>；
///   当前有待打的支线岛（QuestBarData.CurrentSideIsland）且该岛本轮没播过 → 播一段会长单人对话。
/// · 每岛每次洗牌轮只播一次：SaveData.sideIslandGuide* 双写记录，ResetSideIslandClears 时清空重播。
/// · 待打岛真源复用 QuestBarData.CurrentSideIsland（任务栏支线行同一口径，别再各算一份）。
/// · 铁律 11：台词走 C# 常量；铁律 14：不兜底，脏数据直接 LogError。
/// </summary>
public static class SideIslandGuide
{
    // —— 台词常量（调性按 2026-10-16 主人拍板：有趣、流行、别端着）——
    const string Speaker = "会长";
    const string Line1Fmt = "我勒个豆，差点忘了——第七小队又在「{0}」那边失联了，就发回来一条消息：";
    const string Line2 = "「岛上似乎藏着不少好东西……滋滋……」得，信号又断了，老毛病。";
    const string Line3 = "算个支线，不打也不耽误你主线。想去的话，点开冒险页就能看到那个岛，左上角任务栏里也挂着。";

    /// <summary>
    /// 轮询入口：当前有待打的支线岛且该岛没播过指引 → 播（播前先写标记，防中途切场景重复弹）。
    /// 引导未完成 / 剧情在播 / 没有待打岛 → 静默返回（这些都是正常状态，不是错误）。
    /// </summary>
    public static void TryShowForCurrent(SaveData data)
    {
        if (data == null) return;
        if (!StoryProgress.TutorialDone) return;
        var dir = StoryDirector.Ensure();
        if (dir == null || dir.IsPlaying) return;

        var side = QuestBarData.CurrentSideIsland();
        if (side == null) return;
        var view = side.Value;
        if (data.HasShownSideIslandGuide(view.islandId)) return;

        var isl = SideIslandDefs.Get(view.islandId);
        if (isl == null)
        {
            // 派定了不存在的岛 id —— 存档脏数据，报出来别静默（铁律 12/14）
            Debug.LogError($"[SideIslandGuide] branchRoll 记了不存在的岛 id={view.islandId}，指引不播");
            return;
        }

        data.MarkSideIslandGuideShown(view.islandId);
        if (SaveSystem.Instance != null) SaveSystem.Instance.Save();

        var beats = new List<StoryBeat>
        {
            StoryDirector.Solo(Speaker, Line1Fmt.Replace("{0}", isl.name), StoryPortraits.GuildMaster),
            StoryDirector.Solo(Speaker, Line2, StoryPortraits.GuildMaster),
            StoryDirector.Solo(Speaker, Line3, StoryPortraits.GuildMaster),
        };
        dir.Play(beats, null, null, keepSceneArt: true);
        Debug.Log($"[SideIslandGuide] 播放支线岛情报指引：岛{view.islandId}「{isl.name}」");
    }
}
