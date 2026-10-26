using System;
using System.Collections.Generic;
using UnityEngine;

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
    /// <summary>本章累计通关 targetCount 关（2026-10-26 密度行：打几次就能推进一点主线）。
    /// 进度基线 = 首次评估时本章已通关数，见 MainQuestSystem.ProgressOf。</summary>
    ClearStages = 5,
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
/// 主线任务表（2026-09-19 新增；**2026-10-26 迁表**，主人拍板「任务单独做 不管代码还是数据库」）。
///
/// 数据已 1:1 迁入 quest.csv（Assets/Data/Source/Tables/quest.csv → Resources/Data/Tables/quest.bytes，
/// 加载器 <see cref="QuestTable"/>，CookAll → CookQuest）。原静态数组 id 与文案保持不变（保旧档
/// mainQuestDone 进度），每章另加 1~2 条 ClearStages 密度行（新 id C?_Q5/Q6）。
/// MainQuestSystem 只依赖 All / QuestsOf，迁表不动系统层；改任务数值/文案直接改 quest.csv。
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

    static MainQuestDef[] _all;

    /// <summary>
    /// 全部主线任务（按 章节 → order 排序，顺序 = 推进顺序）。
    /// 懒加载读表：不在静态构造期读表（工程坑 ④「构造期禁读表」），首次访问才加载。
    /// </summary>
    public static MainQuestDef[] All
    {
        get
        {
            if (_all == null) _all = BuildFromTable();
            return _all;
        }
    }

    static MainQuestDef[] BuildFromTable()
    {
        var rows = QuestTable.MainRows();
        var list = new List<MainQuestDef>(rows.Count);
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            list.Add(new MainQuestDef
            {
                id = r.id,
                chapter = r.chapter,
                type = ParseType(r.targetType),
                title = r.title,
                desc = r.desc,
                targetCount = r.targetCount,
                param = r.param,
                rewardStones = r.rewardStones
            });
        }
        return list.ToArray();
    }

    static MainQuestType ParseType(string s)
    {
        switch ((s ?? "").Trim())
        {
            case "clear_stage": return MainQuestType.ClearStage;
            case "clear_stages": return MainQuestType.ClearStages;
            case "talk_npc": return MainQuestType.TalkNpc;
            case "watch_story": return MainQuestType.WatchStory;
            case "recruit_merc": return MainQuestType.RecruitMerc;
            case "upgrade_talent": return MainQuestType.UpgradeTalent;
            default:
                Debug.LogError("[MainQuest] quest.csv 未知 target_type: " + s + "（按 ClearStage 处理）");
                return MainQuestType.ClearStage;
        }
    }

    /// <summary>NPC 显示名（新增 NPC 在这里加，UI 与自动文案都读这里）。</summary>
    public static string NpcName(string npcId)
    {
        if (npcId == NpcInnkeeper) return "酒馆老板娘";
        if (npcId == NpcGuildmaster) return "会长";
        if (npcId == NpcAlia) return "艾丽娅";
        if (npcId == NpcHaldon) return "劳顿";
        return string.IsNullOrEmpty(npcId) ? "NPC" : npcId;
    }

    /// <summary>剧情标题（新增剧情在这里加）。</summary>
    public static string StoryTitle(string storyId)
    {
        if (storyId == StoryC1Blade) return "裂隙之刃的旧缺口";
        if (storyId == StoryC3Jungle) return "翡翠秘境的守望者";
        if (storyId == StoryC4Field) return "风里带回来的名字";
        if (storyId == StoryC6Cave) return "石壁上的旧字";
        if (storyId == StoryC8Ice) return "裂隙的尽头";
        return string.IsNullOrEmpty(storyId) ? "一段剧情" : storyId;
    }

    /// <summary>desc 留空时的自动文案。</summary>
    public static string AutoDesc(MainQuestDef def)
    {
        if (!string.IsNullOrEmpty(def.desc)) return def.desc;
        switch (def.type)
        {
            case MainQuestType.ClearStage:
                int idx = StageIndexOf(def);
                return "通关 " + def.chapter + "-" + (idx + 1);
            case MainQuestType.ClearStages:
                return "本章累计通关 " + Math.Max(1, def.targetCount) + " 关";
            case MainQuestType.TalkNpc:
                return "去找" + NpcName(def.param) + "聊聊";
            case MainQuestType.WatchStory:
                return "回顾剧情：" + StoryTitle(def.param);
            case MainQuestType.RecruitMerc:
                return "在酒馆雇佣 " + Math.Max(1, def.targetCount) + " 名佣兵";
            case MainQuestType.UpgradeTalent:
                return "点 " + Math.Max(1, def.targetCount) + " 次天赋";
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
