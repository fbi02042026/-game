using System.Collections.Generic;

/// <summary>
/// 主线任务用到的对话文案（2026-09-19 新增）。
///
/// ⚠ 全部是**占位文案**（主人还没给正式稿）。只有 2~4 句，够验证链路：
///   点任务条 → 播对话 → 播完标记完成。正式文案来了直接替换引号里的字符串即可，
///   结构（说话人 / 立绘 / 句数）不用动。
///
/// 立绘：老板娘复用前台小姐（StoryPortraits.Innkeeper，美术已确认，不另出图）。
/// 播法：交给 StoryDirector，与开章/战后剧情同一套表现，不另做一套对话 UI。
/// </summary>
public static class MainQuestDialogues
{
    const string Landlady = "\u8001\u677f\u5a18";

    /// <summary>NPC 对话。npcId 见 MainQuestDefs.NpcName；找不到就给一句兜底旁白。</summary>
    public static List<StoryBeat> NpcTalk(string npcId, int chapter)
    {
        if (npcId == MainQuestDefs.NpcInnkeeper)
        {
            // 第 1 章：第一次照面——给世界观 + 一点压迫感
            if (chapter <= 1)
            {
                return new List<StoryBeat>
                {
                    StoryDirector.Solo(Landlady,
                        "\u7b2c\u4e00\u6b21\u8fdb\u88c2\u9699\uff1f\u5148\u5750\u4e0b\uff0c\u9152\u94b1\u53ef\u4ee5\u8d49\u3002",
                        StoryPortraits.Innkeeper),
                    StoryDirector.Solo(Landlady,
                        "\u5357\u8fb9\u68ee\u6797\u90a3\u961f\u65b0\u4eba\uff0c\u56de\u6765\u7684\u8fd8\u4e0d\u5230\u4e00\u534a\u3002",
                        StoryPortraits.Innkeeper),
                    StoryDirector.Narration("\u5979\u64e6\u7740\u676f\u5b50\uff0c\u6ca1\u518d\u5f80\u4e0b\u8bf4\u3002"),
                };
            }

            // 第 2 章起：熟客口吻，顺手把玩家推向酒馆（配合 RecruitMerc 任务）
            return new List<StoryBeat>
            {
                StoryDirector.Solo(Landlady,
                    "\u4f60\u8fd8\u6d3b\u7740\u554a\u2014\u2014\u90a3\u8fd9\u676f\u7b97\u6211\u8bf7\u7684\u3002",
                    StoryPortraits.Innkeeper),
                StoryDirector.Solo(Landlady,
                    "\u4e00\u4e2a\u4eba\u6253\u592a\u7d2f\u4e86\u5427\uff1f\u53bb\u90a3\u8fb9\u6311\u4e2a\u642d\u5b50\uff0c\u522b\u786c\u6491\u3002",
                    StoryPortraits.Innkeeper),
            };
        }

        return new List<StoryBeat>
        {
            StoryDirector.Narration("\u4f60\u8d70\u8fc7\u53bb\uff0c\u5bf9\u65b9\u53ea\u662f\u70b9\u4e86\u70b9\u5934\u3002"),
        };
    }

    /// <summary>可回顾的剧情段。storyId 见 MainQuestDefs.StoryTitle。</summary>
    public static List<StoryBeat> Story(string storyId)
    {
        if (storyId == MainQuestDefs.StoryC1Blade)
        {
            return new List<StoryBeat>
            {
                StoryDirector.Narration(
                    "\u4f60\u6478\u4e86\u6478\u8170\u95f4\u7684\u88c2\u9699\u4e4b\u5203\uff0c\u5203\u53e3\u6709\u4e00\u9053\u65e7\u7f3a\u53e3\u3002"),
                StoryDirector.Narration(
                    "\u516c\u4f1a\u6863\u6848\u5199\u7740\uff1a\u8fd9\u628a\u5251\u7684\u524d\u4e00\u4efb\u4e3b\u4eba\uff0c\u4e09\u5e74\u524d\u5728\u68ee\u6797\u5c42\u5931\u8e2a\u3002"),
            };
        }

        return new List<StoryBeat>
        {
            StoryDirector.Narration("\u4e00\u6bb5\u8fd8\u6ca1\u5199\u7684\u5267\u60c5\u3002"),
        };
    }
}
