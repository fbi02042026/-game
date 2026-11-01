using System.Collections.Generic;

/// <summary>
/// 主线任务用到的对话文案（2026-09-19 新增）。
///
/// 文案：2026-09-20 已换**正式版 V1**（见 Docs/正式文案_V1_2026-09-20.md）。
/// 老板娘按章节分 5 档递进，剧情段 5 条；结构（说话人 / 立绘 / 句数）保持稳定，
/// 后续要改只需替换引号内的字符串。
///
/// 立绘：老板娘用专属立绘（StoryPortraits.Innkeeper → MercPortraitSprites landlady → 酒吧老板娘.png，2026-10-15 接上）。
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
    // 【2026-11-01 主人拍板「不要回顾剧情这个模式，改成关卡里的或者酒馆对话里的」】
    // Story(storyId) 整个方法已删：5 段剧情文案（c1_blade/c3_jungle/c4_field/c6_cave/c8_ice）
    // 并入各章战前剧情 ChapterStoryBeats.PlayPre1/3/4/6/8，随进关自动播；
    // quest.csv 的 watch_story 任务行同步全删（C1_Q4/C3_Q3/C4_Q3/C6_Q2/C8_Q1）。
}
