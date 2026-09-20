using System;
using System.Collections.Generic;

/// <summary>
/// 主线任务类型。
///
/// 区别与战斗内的「关卡目标」（battle_quest.csv）：那是单局内的三星条件，
/// 这里是**跨局的长线牵引**，两者互不相干，本表不读 battle_quest.csv。
/// </summary>
public enum MainQuestType
{
    /// <summary>通关指定关卡。param = 章节内关卡下标（0 起，如 "3" 表示 第4关）。</summary>
    ClearStage = 0,
    /// <summary>找 NPC 对话。param = npcId（见 <see cref="MainQuestDefs.NpcName"/>）。</summary>
    TalkNpc = 1,
    /// <summary>播放/回顾一段剧情。param = storyId（见 <see cref="MainQuestDefs.StoryTitle"/>）。</summary>
    WatchStory = 2,
    /// <summary>在酒馆雇佣佣兵。targetCount = 需要新增的雇佣数。</summary>
    RecruitMerc = 3,
    /// <summary>点天赋。targetCount = 需要消耗的天赋石数。</summary>
    UpgradeTalent = 4,
}

/// <summary>
/// 一条主线任务。
/// desc 留空时由 <see cref="MainQuestDefs.AutoDesc"/> 按类型自动生成，
/// 这样调 param / targetCount 不用同步改文案，避免两处对不上。
/// </summary>
public struct MainQuestDef
{
    public string id;
    public int chapter;
    public MainQuestType type;
    public string title;
    public string desc;
    public int targetCount;
    public string param;
    /// <summary>完成时发放的天赋石。0 = 不发（战斗类走现有关卡/章节里程碑，不重复给）。</summary>
    public int rewardStones;

    public bool IsBattle => type == MainQuestType.ClearStage;
}

/// <summary>
/// 主线任务表（2026-09-19 新增）。
///
/// 目的：解决「只会打关卡」的单调感——主线里穿插非战斗目标（找老板娘聊天、
/// 看一段剧情、去酒馆雇人、点一次天赋），让玩家一进游戏就知道下一步该干嘛。
///
/// ⚠ 本表现在是**静态数组**（先跑通链路、验证节奏）。条目稳定后迁 csv：
///    照 ChapterBranchTable / BattleQuestTable 的路子加一个 MainQuestTable.cs，
///    读 Assets/Resources/Config/main_quest.csv，字段与本结构一一对应即可，
///    MainQuestSystem 只依赖 MainQuestDefs.All / QuestsOf，迁移不用动系统层。
///
/// 顺序 = 推进顺序，同一章内按数组顺序逐条解锁（一条完成才轮到下一条）。
/// 与章节推进共存：ClearStage 只在表内占一格，章节本身该怎么推还怎么推，
/// 表里可以写「先去找老板娘，再通关 2-1」，顺序完全由数组顺序决定。
/// </summary>
public static class MainQuestDefs
{
    public const string NpcInnkeeper = "innkeeper";
    public const string NpcGuildmaster = "guildmaster";
    public const string NpcAlia = "alia";
    public const string NpcHaldon = "haldon";
    public const string StoryC1Blade = "c1_blade";
    public const string StoryC3Jungle = "c3_jungle";
    public const string StoryC4Field = "c4_field";
    public const string StoryC6Cave = "c6_cave";
    public const string StoryC8Ice = "c8_ice";

    /// <summary>
    /// 第 1~2 章为首批样例（各 4 条，含非战斗目标）；
    /// 第 3~8 章于 2026-09-20 配满（各 4 条），标题为正式文案 V1
    /// （见 Docs/正式文案_V1_2026-09-20.md）。
    /// </summary>
    public static readonly MainQuestDef[] All = new MainQuestDef[]
    {
        // ===== 第 1 章：先打一关，再认识老板娘，回来打一关，看段剧情 =====
        new MainQuestDef { id = "C1_Q1", chapter = 1, type = MainQuestType.ClearStage,
            title = "\u8e0f\u51fa\u7b2c\u4e00\u6b65", desc = "", targetCount = 1, param = "0", rewardStones = 0 },

        // 非战斗：刚进游戏就给一个「不用打也能做」的目标，降低上来就推关的压迫感
        new MainQuestDef { id = "C1_Q2", chapter = 1, type = MainQuestType.TalkNpc,
            title = "\u53bb\u89c1\u89c1\u8001\u677f\u5a18", desc = "", targetCount = 1, param = NpcInnkeeper, rewardStones = 1 },

        new MainQuestDef { id = "C1_Q3", chapter = 1, type = MainQuestType.ClearStage,
            title = "\u6df1\u5165\u68ee\u6797\u5c42", desc = "", targetCount = 1, param = "2", rewardStones = 0 },

        // 非战斗：章节中段插一段世界观，给「继续打下去」一点理由
        new MainQuestDef { id = "C1_Q4", chapter = 1, type = MainQuestType.WatchStory,
            title = "\u88c2\u9699\u4e4b\u5203\u7684\u65e7\u7f3a\u53e3", desc = "", targetCount = 1, param = StoryC1Blade, rewardStones = 1 },

        // ===== 第 2 章：打一关 → 点天赋 → 去酒馆雇人 → 再推关 =====
        new MainQuestDef { id = "C2_Q1", chapter = 2, type = MainQuestType.ClearStage,
            title = "\u65b0\u7684\u59d4\u6258", desc = "", targetCount = 1, param = "0", rewardStones = 0 },

        // 非战斗：把玩家推去天赋页（顺带教会养成入口在哪）
        new MainQuestDef { id = "C2_Q2", chapter = 2, type = MainQuestType.UpgradeTalent,
            title = "\u5148\u5f3a\u5316\u81ea\u5df1", desc = "", targetCount = 1, param = "", rewardStones = 1 },

        // 非战斗：把玩家推去酒馆（佣兵系统曝光）
        new MainQuestDef { id = "C2_Q3", chapter = 2, type = MainQuestType.RecruitMerc,
            title = "\u627e\u4e2a\u642d\u5b50", desc = "", targetCount = 1, param = "", rewardStones = 1 },

        new MainQuestDef { id = "C2_Q4", chapter = 2, type = MainQuestType.ClearStage,
            title = "\u8d70\u5230\u534a\u7a0b", desc = "", targetCount = 1, param = "4", rewardStones = 0 },

        // ===== 第 3 章：密林 =====
        new MainQuestDef { id = "C3_Q1", chapter = 3, type = MainQuestType.TalkNpc,
            title = "密林的另一头", desc = "", targetCount = 1, param = NpcInnkeeper, rewardStones = 1 },

        new MainQuestDef { id = "C3_Q2", chapter = 3, type = MainQuestType.ClearStage,
            title = "穿过藤蔓", desc = "", targetCount = 1, param = "2", rewardStones = 0 },

        // 非战斗：章节中段插一段世界观
        new MainQuestDef { id = "C3_Q3", chapter = 3, type = MainQuestType.WatchStory,
            title = "秘境守望者", desc = "", targetCount = 1, param = StoryC3Jungle, rewardStones = 1 },

        new MainQuestDef { id = "C3_Q4", chapter = 3, type = MainQuestType.ClearStage,
            title = "翡翠尽头", desc = "", targetCount = 1, param = "9", rewardStones = 0 },

        // ===== 第 4 章：草原 =====
        new MainQuestDef { id = "C4_Q1", chapter = 4, type = MainQuestType.RecruitMerc,
            title = "再添一人", desc = "", targetCount = 1, param = "", rewardStones = 1 },

        new MainQuestDef { id = "C4_Q2", chapter = 4, type = MainQuestType.ClearStage,
            title = "草原的风", desc = "", targetCount = 1, param = "3", rewardStones = 0 },

        // 非战斗：章节中段插一段世界观
        new MainQuestDef { id = "C4_Q3", chapter = 4, type = MainQuestType.WatchStory,
            title = "风里的名字", desc = "", targetCount = 1, param = StoryC4Field, rewardStones = 1 },

        new MainQuestDef { id = "C4_Q4", chapter = 4, type = MainQuestType.ClearStage,
            title = "长风尽头", desc = "", targetCount = 1, param = "9", rewardStones = 0 },

        // ===== 第 5 章：出海 =====
        new MainQuestDef { id = "C5_Q1", chapter = 5, type = MainQuestType.TalkNpc,
            title = "出海前的委托", desc = "", targetCount = 1, param = NpcGuildmaster, rewardStones = 1 },

        new MainQuestDef { id = "C5_Q2", chapter = 5, type = MainQuestType.ClearStage,
            title = "潮汐之上", desc = "", targetCount = 1, param = "2", rewardStones = 0 },

        // 非战斗：把玩家推去天赋页
        new MainQuestDef { id = "C5_Q3", chapter = 5, type = MainQuestType.UpgradeTalent,
            title = "沉下心来", desc = "", targetCount = 3, param = "", rewardStones = 1 },

        new MainQuestDef { id = "C5_Q4", chapter = 5, type = MainQuestType.ClearStage,
            title = "遗迹之门", desc = "", targetCount = 1, param = "9", rewardStones = 0 },

        // ===== 第 6 章：深窟 =====
        new MainQuestDef { id = "C6_Q1", chapter = 6, type = MainQuestType.ClearStage,
            title = "走进深窟", desc = "", targetCount = 1, param = "1", rewardStones = 0 },

        // 非战斗：章节中段插一段世界观
        new MainQuestDef { id = "C6_Q2", chapter = 6, type = MainQuestType.WatchStory,
            title = "石壁旧字", desc = "", targetCount = 1, param = StoryC6Cave, rewardStones = 1 },

        new MainQuestDef { id = "C6_Q3", chapter = 6, type = MainQuestType.RecruitMerc,
            title = "带上帮手", desc = "", targetCount = 1, param = "", rewardStones = 1 },

        new MainQuestDef { id = "C6_Q4", chapter = 6, type = MainQuestType.ClearStage,
            title = "深窟尽头", desc = "", targetCount = 1, param = "9", rewardStones = 0 },

        // ===== 第 7 章：烈焰 =====
        new MainQuestDef { id = "C7_Q1", chapter = 7, type = MainQuestType.TalkNpc,
            title = "老板娘的劝阻", desc = "", targetCount = 1, param = NpcInnkeeper, rewardStones = 1 },

        new MainQuestDef { id = "C7_Q2", chapter = 7, type = MainQuestType.ClearStage,
            title = "烈焰之前", desc = "", targetCount = 1, param = "2", rewardStones = 0 },

        // 非战斗：把玩家推去天赋页
        new MainQuestDef { id = "C7_Q3", chapter = 7, type = MainQuestType.UpgradeTalent,
            title = "炼心", desc = "", targetCount = 3, param = "", rewardStones = 1 },

        new MainQuestDef { id = "C7_Q4", chapter = 7, type = MainQuestType.ClearStage,
            title = "炼狱尽头", desc = "", targetCount = 1, param = "9", rewardStones = 0 },

        // ===== 第 8 章：裂隙尽头 =====
        // 非战斗：章节中段插一段世界观
        new MainQuestDef { id = "C8_Q1", chapter = 8, type = MainQuestType.WatchStory,
            title = "裂隙的尽头", desc = "", targetCount = 1, param = StoryC8Ice, rewardStones = 1 },

        new MainQuestDef { id = "C8_Q2", chapter = 8, type = MainQuestType.ClearStage,
            title = "雪境行", desc = "", targetCount = 1, param = "3", rewardStones = 0 },

        new MainQuestDef { id = "C8_Q3", chapter = 8, type = MainQuestType.TalkNpc,
            title = "最后一杯", desc = "", targetCount = 1, param = NpcInnkeeper, rewardStones = 1 },

        new MainQuestDef { id = "C8_Q4", chapter = 8, type = MainQuestType.ClearStage,
            title = "终末之刃", desc = "", targetCount = 1, param = "9", rewardStones = 0 },
    };

    /// <summary>NPC 显示名（新增 NPC 在这里加，UI 与自动文案都读这里）。</summary>
    public static string NpcName(string npcId)
    {
        if (npcId == NpcInnkeeper) return "\u9152\u9986\u8001\u677f\u5a18";
        if (npcId == NpcGuildmaster) return "会长";
        if (npcId == NpcAlia) return "艾丽娅";
        if (npcId == NpcHaldon) return "劳顿";
        return string.IsNullOrEmpty(npcId) ? "NPC" : npcId;
    }

    /// <summary>剧情标题（新增剧情在这里加）。</summary>
    public static string StoryTitle(string storyId)
    {
        if (storyId == StoryC1Blade) return "\u88c2\u9699\u4e4b\u5203\u7684\u65e7\u7f3a\u53e3";
        if (storyId == StoryC3Jungle) return "翡翠秘境的守望者";
        if (storyId == StoryC4Field) return "风里带回来的名字";
        if (storyId == StoryC6Cave) return "石壁上的旧字";
        if (storyId == StoryC8Ice) return "裂隙的尽头";
        return string.IsNullOrEmpty(storyId) ? "\u4e00\u6bb5\u5267\u60c5" : storyId;
    }

    /// <summary>desc 留空时的自动文案。</summary>
    public static string AutoDesc(MainQuestDef def)
    {
        if (!string.IsNullOrEmpty(def.desc)) return def.desc;
        switch (def.type)
        {
            case MainQuestType.ClearStage:
                int idx = StageIndexOf(def);
                return "\u901a\u5173 " + def.chapter + "-" + (idx + 1);
            case MainQuestType.TalkNpc:
                return "\u53bb\u627e" + NpcName(def.param) + "\u804a\u804a";
            case MainQuestType.WatchStory:
                return "\u56de\u987e\u5267\u60c5\uff1a" + StoryTitle(def.param);
            case MainQuestType.RecruitMerc:
                return "\u5728\u9152\u9986\u96c7\u4f63 " + Math.Max(1, def.targetCount) + " \u540d\u4f63\u5175";
            case MainQuestType.UpgradeTalent:
                return "\u70b9 " + Math.Max(1, def.targetCount) + " \u6b21\u5929\u8d4b";
            default:
                return "";
        }
    }

    /// <summary>把 param 解析成关卡下标（0 起）；解析失败返回 0。</summary>
    public static int StageIndexOf(MainQuestDef def)
    {
        int v;
        if (!int.TryParse(def.param, out v)) return 0;
        return v < 0 ? 0 : v;
    }

    /// <summary>某章的全部任务（按配置顺序）。</summary>
    public static List<MainQuestDef> QuestsOf(int chapter)
    {
        var list = new List<MainQuestDef>();
        for (int i = 0; i < All.Length; i++)
            if (All[i].chapter == chapter) list.Add(All[i]);
        return list;
    }

    public static bool TryFind(string id, out MainQuestDef def)
    {
        for (int i = 0; i < All.Length; i++)
        {
            if (All[i].id == id) { def = All[i]; return true; }
        }
        def = default(MainQuestDef);
        return false;
    }
}
