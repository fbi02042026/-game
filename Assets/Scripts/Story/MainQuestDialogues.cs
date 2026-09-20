using System.Collections.Generic;

/// <summary>
/// 主线任务用到的对话文案（2026-09-19 新增）。
///
/// 文案：2026-09-20 已换**正式版 V1**（见 Docs/正式文案_V1_2026-09-20.md）。
/// 老板娘按章节分 5 档递进，剧情段 5 条；结构（说话人 / 立绘 / 句数）保持稳定，
/// 后续要改只需替换引号内的字符串。
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
        if (npcId == MainQuestDefs.NpcGuildmaster)
        {
            return new List<StoryBeat>
            {
                StoryDirector.Solo("会长",
                    "裂隙不缺勇敢的人，缺活着回来的人。",
                    StoryPortraits.GuildMaster),
                StoryDirector.Solo("会长",
                    "委托接了就要走完。走不完的，我替他们把名字记上。",
                    StoryPortraits.GuildMaster),
            };
        }

        if (npcId == MainQuestDefs.NpcAlia)
        {
            return new List<StoryBeat>
            {
                StoryDirector.Solo("艾丽娅",
                    "你也是从那边下来的？那我们算是同路。",
                    StoryPortraits.Xiaomei),
                StoryDirector.Solo("艾丽娅",
                    "别问我三年前的事。问了我也不会答。",
                    StoryPortraits.Xiaomei),
            };
        }

        if (npcId == MainQuestDefs.NpcHaldon)
        {
            return new List<StoryBeat>
            {
                StoryDirector.Solo("劳顿",
                    "剑要常擦。人也是。",
                    StoryPortraits.LaoDun),
                StoryDirector.Solo("劳顿",
                    "我在这条路上走了十一年。你猜我见过几个回来的？",
                    StoryPortraits.LaoDun),
            };
        }

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

            // 第 2 章起：熟客口吻，按章递进
            if (chapter <= 3)
            {
                return new List<StoryBeat>
                {
                    StoryDirector.Solo(Landlady,
                        "你还活着啊——那这杯算我请的。",
                        StoryPortraits.Innkeeper),
                    StoryDirector.Solo(Landlady,
                        "一个人打太累了罢？去那边挑个搭子，别硬撑。",
                        StoryPortraits.Innkeeper),
                };
            }
            else if (chapter <= 5)
            {
                return new List<StoryBeat>
                {
                    StoryDirector.Solo(Landlady,
                        "海风把招牌吹掉漆了，你倒是越走越远。",
                        StoryPortraits.Innkeeper),
                    StoryDirector.Solo(Landlady,
                        "带上的人够不够？不够就去名册上再翻翻。",
                        StoryPortraits.Innkeeper),
                };
            }
            else if (chapter <= 7)
            {
                return new List<StoryBeat>
                {
                    StoryDirector.Solo(Landlady,
                        "炼狱那边的火，晚上能映红半条街。",
                        StoryPortraits.Innkeeper),
                    StoryDirector.Solo(Landlady,
                        "你要是回不来，我就把这杯倒进土里。",
                        StoryPortraits.Innkeeper),
                };
            }
            else
            {
                return new List<StoryBeat>
                {
                    StoryDirector.Solo(Landlady,
                        "最后一杯，烈一点。",
                        StoryPortraits.Innkeeper),
                    StoryDirector.Solo(Landlady,
                        "回来的时候，把那把剑也带回来。",
                        StoryPortraits.Innkeeper),
                };
            }
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

        if (storyId == MainQuestDefs.StoryC3Jungle)
        {
            return new List<StoryBeat>
            {
                StoryDirector.Narration(
                    "藤蔓后头有一座塌了的石门，门上刻着不属于这个时代的字。"),
                StoryDirector.Narration(
                    "守在这里的东西没有名字——它只是记得，有人曾经从这儿走进去，再没出来。"),
            };
        }

        if (storyId == MainQuestDefs.StoryC4Field)
        {
            return new List<StoryBeat>
            {
                StoryDirector.Narration(
                    "草原的风里混着铁锈味，那是更远处的战火被吹了过来。"),
                StoryDirector.Narration(
                    "你在草根下捡到一块木牌，背面刻着一个名字，笔画很新。"),
            };
        }

        if (storyId == MainQuestDefs.StoryC6Cave)
        {
            return new List<StoryBeat>
            {
                StoryDirector.Narration(
                    "深窟的石壁上有人用刀刻过字，一层压着一层，像很多人写过同一句话。"),
                StoryDirector.Narration(
                    "最上面那行还没写完：『如果你读到这——』"),
            };
        }

        if (storyId == MainQuestDefs.StoryC8Ice)
        {
            return new List<StoryBeat>
            {
                StoryDirector.Narration(
                    "雪落到剑刃上就化了，像它认得这把剑。"),
                StoryDirector.Narration(
                    "裂隙在前面收成一条白线。你想起老板娘那句没说完的话，忽然明白她为什么没往下说。"),
            };
        }

        return new List<StoryBeat>
        {
            StoryDirector.Narration("\u4e00\u6bb5\u8fd8\u6ca1\u5199\u7684\u5267\u60c5\u3002"),
        };
    }
}
