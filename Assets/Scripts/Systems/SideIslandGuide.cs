using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 支线岛情报指引（2026-10-26 主人拍板「第一章可以出现支线，并出现一个佣兵信息窗口那个对话指引框，
/// 告诉玩家这个支线的名字、可进行探索——比如第七小队的信息什么的」）。
///
/// · 触发（2026-10-28 主人拍板改）：玩家进冒险界面时播（AdventureUI.ShowPage），
///   不再在主界面轮询主动弹——「以后相关剧情不要主动出来」，只随玩家动作/进度触发。
///   当前有待打的支线岛（QuestBarData.CurrentSideIsland）且该岛本轮没播过 → 播一段会长单人对话。
/// · 每岛每次洗牌轮只播一次：SaveData.sideIslandGuide* 双写记录，ResetSideIslandClears 时清空重播。
/// · 待打岛真源复用 QuestBarData.CurrentSideIsland（任务栏支线行同一口径，别再各算一份）。
/// · 铁律 11：台词走 C# 常量；铁律 14：不兜底，脏数据直接 LogError。
/// </summary>
public static class SideIslandGuide
{
    // —— 台词常量 ——
    // 【2026-10-27 主人拍板】会长不玩梗（原「我勒个豆」调撤掉），情报口吻正常说话；
    // 补会长办公室背景图（原没给 backgroundId，只铺暗底被主人报「没有背景图」）。
    const string Speaker = "会长";
    const string Line1Fmt = "差点忘了说正事——第七小队在「{0}」那边失联了，只发回来一条消息：";
    const string Line2 = "「岛上似乎藏着不少好东西……滋滋……」信号到这里就断了，老毛病了。";
    const string Line3 = "这条我先记成支线，不打也不耽误你主线。想去的话，就在这张地图上点那个岛，左上角任务栏里也挂着。";

    /// <summary>
    /// 播放入口（2026-10-28 起由 AdventureUI.ShowPage 调）：当前有待打的支线岛且该岛没播过指引 →
    /// 播（播前先写标记，防中途切场景重复弹）。
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
            // 首句带会长办公室背景（换背景自带地点揭示黑场，快节奏场景名单里已有 guild_office）；
            // 【2026-10-27】keepSceneArt 改 false：这是主界面上的插播指引，播完必须整体收窗——
            // 原 true 只藏对话框，IntroVeil 留下的 42% 黑幕和常驻 DialogueUI 全留在主界面上（主人报「剧情框消失后屏幕是脏的」）。
            StoryDirector.Solo(Speaker, Line1Fmt.Replace("{0}", isl.name), StoryPortraits.GuildMaster)
                .Bg(StoryBackgrounds.GuildOffice),
            StoryDirector.Solo(Speaker, Line2, StoryPortraits.GuildMaster),
            StoryDirector.Solo(Speaker, Line3, StoryPortraits.GuildMaster),
        };
        dir.Play(beats, null, null);
        Debug.Log($"[SideIslandGuide] 播放支线岛情报指引：岛{view.islandId}「{isl.name}」");
    }
}
