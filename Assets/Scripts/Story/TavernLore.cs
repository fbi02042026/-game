using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 叙事 V2.0：酒馆叙事。
/// 数据来源：Docs/像素冒险：裂隙之刃_叙事重制策划案_V2.0.md §7。
/// 承担三件事：世界观补充 / 佣兵侧写（复用现有 22 条 Lore，零新增文本）/ 二周目钩子。
/// 本类只提供「查询文本」，UI 挂载点见 TavernUI.BuildContentOnly()。
/// </summary>
public static class TavernLore
{
    // ---------- 核心道具：靠窗的空座位（6 阶段状态机） ----------

    public enum SeatStage
    {
        /// <summary>序章：还没人坐过。</summary>
        Nothing = 0,
        /// <summary>第 1–4 章：三枚铜板压着一张收据。</summary>
        Coins = 1,
        /// <summary>第 5 章后：多了一张署 M 的纸条。</summary>
        Note = 2,
        /// <summary>第 7 章后：她从不离身的发绳。</summary>
        HairTie = 3,
        /// <summary>通关后（BAD）：三个空杯 + 一个没动过的杯子。</summary>
        ThreeCups = 4,
        /// <summary>通关后（HAPPY）：四个杯子，其中一个收起来了。</summary>
        FourCups = 5,
        /// <summary>《最后一个箱子》达成后：座位上多了一支笔。</summary>
        Pen = 6,
    }

    public static SeatStage ResolveSeatStage()
    {
        var d = SaveSystem.Instance?.Data;
        if (d == null) return SeatStage.Nothing;

        // 结局态优先于进度态
        if (StoryEnding.IsAchieved(StoryEnding.EndingId.True)) return SeatStage.Pen;
        if (StoryEnding.IsAchieved(StoryEnding.EndingId.Happy)) return SeatStage.FourCups;
        if (StoryEnding.IsAchieved(StoryEnding.EndingId.BadHome)
            || StoryEnding.IsAchieved(StoryEnding.EndingId.BadGate)) return SeatStage.ThreeCups;

        int far = HighestClearedChapter(d);
        if (far >= 7) return SeatStage.HairTie;
        if (far >= 5) return SeatStage.Note;
        if (far >= 1) return SeatStage.Coins;
        return SeatStage.Nothing;
    }

    /// <summary>座位上放着的道具名（给美术出图用，也做 UI 副标题）。</summary>
    public static string SeatProp(SeatStage s)
    {
        switch (s)
        {
            case SeatStage.Nothing: return "";
            case SeatStage.Coins: return "三枚铜板压着一张收据";
            case SeatStage.Note: return "铜板旁边多了一张纸条";
            case SeatStage.HairTie: return "她从不离身的发绳";
            case SeatStage.ThreeCups: return "三个空杯，一个没动过";
            case SeatStage.FourCups: return "四个杯子，收起了一个";
            case SeatStage.Pen: return "一支笔";
            default: return "";
        }
    }

    /// <summary>点击座位时显示的文本。</summary>
    public static string SeatText(SeatStage s)
    {
        switch (s)
        {
            case SeatStage.Nothing:
                return "窗边的位置空着。老板娘说这个位置一直没人敢坐。";
            case SeatStage.Coins:
                return "出发前她在这儿坐着，说回来就还。到现在也没还。";
            case SeatStage.Note:
                return "「如果你也下来了……小心公会。——M」\n她留的位置上的东西，被人动过。";
            case SeatStage.HairTie:
                return "有人替她把这东西带回来了。\n但人还没回来。";
            case SeatStage.ThreeCups:
                return "她的杯子一直没人收。\n老板娘说，她欠着酒钱呢。";
            case SeatStage.FourCups:
                return "有个人终于回来把酒钱结了。\n老板娘说她还多付了一倍。";
            case SeatStage.Pen:
                return "老板娘也不解释。\n只是每个月的今天，桌上都会多一份没人吃的面包。";
            default:
                return "";
        }
    }

    /// <summary>每次玩家点开座位调用一次。累积 5 次发放线索页 C26。</summary>
    public static void NoteSeatViewed()
    {
        var d = SaveSystem.Instance?.Data;
        if (d == null) return;

        d.tavernSeatViewCount++;
        if (d.tavernSeatViewCount == 5)
            StoryClue.Grant("C26");
        else
            SaveSystem.Instance.Save();
    }

    // ---------- NPC：独眼（情报商） ----------

    /// <summary>请他喝一杯后他会说的话。按最高通关章节推进，共 5 段。</summary>
    public static string InformantLine()
    {
        var d = SaveSystem.Instance?.Data;
        int far = d == null ? 0 : HighestClearedChapter(d);

        bool branchOpen = d != null
            && (d.chosenBranch == 3 || d.chosenBranch == 4
                || StoryEnding.HasCleared(d, 3) || StoryEnding.HasCleared(d, 4));

        if (branchOpen)
            return "裂隙第二层有两条岔路，以前没人活着回来讲过。\n你要么是第一个，要么是死得最慢的那个。";
        if (far >= 6)
            return "你知道最难受的是什么？\n他们真觉得自己在救人。这就是最难办的部分。";
        if (far >= 5)
            return "你找到那个箱子了。\n那我就不装了——<b>装那个箱子的人还活着。</b> 我见过。";
        if (far >= 3)
            return "公会每年这个时候都会「例行检修」。\n检修什么没人说，但检修单我记得。每年都有。";
        return "三年前也来过一队愣头青，问了一样的问题。\n他们现在不问了。";
    }

    /// <summary>请酒结算：发放对应章节的一份 Tier-2/3 线索页，让玩家有理由主动来找他。</summary>
    public static bool TreatInformant()
    {
        var d = SaveSystem.Instance?.Data;
        if (d == null) return false;

        int far = HighestClearedChapter(d);
        // 不给必得的主线页，只给探索向的那几份；已拿过返回 false 由 UI 改口。
        switch (far)
        {
            case 0:
            case 1: return StoryClue.Grant("C04");
            case 2: return StoryClue.Grant("C05");
            case 3: return StoryClue.Grant("C06");
            case 4: return StoryClue.Grant("C10");
            case 5: return StoryClue.Grant("C15");
            default: return StoryClue.Grant("C19");
        }
    }

    // ---------- NPC：老板娘（新增角色） ----------

    /// <summary>老板娘按进度说的话。她负责给出数字与情绪。</summary>
    public static string InnkeeperLine()
    {
        var d = SaveSystem.Instance?.Data;
        int far = d == null ? 0 : HighestClearedChapter(d);

        if (StoryEnding.AnyAchieved())
            return "这张桌子我留着。谁要坐我都说有人了。";
        if (far >= 7)
            return "三年前最后一晚，那孩子在这儿坐到天亮。\n她只点了一杯，没喝。";
        if (far >= 5)
            return "登记簿我看得懂。有些人回来，有些名字更新了位置。";
        if (far >= 2)
            return "这几年新来的见习越来越多了。上一波还是三年前。";
        return "第一次来？酒钱可以先记着。\n这儿的账，还没人不认过。";
    }

    // ---------- 常驻佣兵：复用现有 Lore，轮换展示 ----------

    /// <summary>
    /// 每次进城随机展示一名已解锁佣兵的一句 Lore——零新增文本成本。
    /// 用访问计数做轮换，避免连续两次是同一人。
    /// </summary>
    public static string MercLoreLine()
    {
        var d = SaveSystem.Instance?.Data;
        if (d == null) return "";

        var pool = new List<AdventureLogCatalog.MercEntry>();
        var all = AdventureLogCatalog.Mercs;
        for (int i = 0; i < all.Length; i++)
        {
            if (string.IsNullOrEmpty(all[i].Lore)) continue;
            if (!AdventureLogCatalog.MercUnlocked(all[i])) continue;
            pool.Add(all[i]);
        }
        if (pool.Count == 0) return "";

        int idx = d.tavernSeatViewCount % pool.Count;
        var e = pool[idx];
        return string.IsNullOrEmpty(e.Name) ? e.Lore : e.Name + "：" + e.Lore;
    }

    // ---------- 工具 ----------

    public static int HighestClearedChapter(SaveData d)
    {
        if (d?.clearedChapterIds == null) return 0;
        int best = 0;
        foreach (int ch in d.clearedChapterIds)
            if (ch > best) best = ch;
        return best;
    }
}
