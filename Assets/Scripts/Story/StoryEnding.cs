using System.Collections.Generic;

/// <summary>
/// 叙事 V2.0：双结局（+ 真结局）判定与文案。
/// 数据来源：Docs/像素冒险：裂隙之刃_叙事重制策划案_V2.0.md §3。
/// 规则：A/C 不需要任何前置，第一次就能选；
///       真结局的门槛只加在 B 上——重玩的动力来自「我想看更好的那个」，
///       而不是「必须满足条件才能看懂」。
/// </summary>
public static class StoryEnding
{
    public enum EndingId
    {
        None = 0,
        /// <summary>《回来的不是她》 BAD · 选项 A</summary>
        BadHome = 1,
        /// <summary>《回来的是两个人》 HAPPY · 选项 B</summary>
        Happy = 2,
        /// <summary>《留下的是她》 BAD · 选项 C</summary>
        BadGate = 3,
        /// <summary>《最后一个箱子》 TRUE · NG+ 隐藏</summary>
        True = 4,
    }

    /// <summary>按第 7 章的选择 + 分支/线索完成度，判定最终结局。</summary>
    public static EndingId Resolve(SaveData d)
    {
        if (d == null) return EndingId.None;

        // 1) 先看第 7 章的选择
        int choice = d.endingChoice;
        if (choice <= 0) return EndingId.None;

        // 2) BAD 分支：立即结算，不需要任何附加条件
        if (choice == 1) return EndingId.BadHome;   // A：先带她回去
        if (choice == 3) return EndingId.BadGate;   // C：让她坐上去

        // 3) B：判定是否达成真结局
        bool allBranches = HasCleared(d, 2) && HasCleared(d, 3) && HasCleared(d, 4);
        bool allKeyLore = StoryClue.HasAll(d, StoryClue.KeyFive);

        return (allBranches && allKeyLore) ? EndingId.True : EndingId.Happy;
    }

    public static bool HasCleared(SaveData d, int chapter)
    {
        if (d?.clearedChapterIds == null) return false;
        return d.clearedChapterIds.Contains(chapter);
    }

    /// <summary>结算画面第二行：结局标题。</summary>
    public static string Title(EndingId id)
    {
        switch (id)
        {
            case EndingId.BadHome: return "回来的不是她";
            case EndingId.Happy: return "回来的是两个人";
            case EndingId.BadGate: return "留下的是她";
            case EndingId.True: return "最后一个箱子";
            default: return "";
        }
    }

    /// <summary>结算画面第三行 / 图鉴说明：一句话讲清发生了什么。</summary>
    public static string Blurb(EndingId id)
    {
        switch (id)
        {
            case EndingId.BadHome: return "你把她背了回来。但她每隔几天就忘一件事。";
            case EndingId.Happy: return "沉座碎了。你们一起走出裂隙，天亮得刺眼。";
            case EndingId.BadGate: return "她坐进沉座成了封印。世界保住了，她没回来。";
            case EndingId.True: return "梅莉莎装了十一年箱子。今天她不用装了。";
            default: return "";
        }
    }

    /// <summary>程序内部代号，仅用于日志与图鉴排序，不显示给玩家。</summary>
    public static string Code(EndingId id)
    {
        switch (id)
        {
            case EndingId.BadHome: return "BAD_END_HOME";
            case EndingId.Happy: return "HAPPY_END";
            case EndingId.BadGate: return "BAD_END_GATE";
            case EndingId.True: return "TRUE_END";
            default: return "NONE";
        }
    }

    /// <summary>结局图鉴未解锁时显示的一行条件提示——这本身就是二周目钩子。</summary>
    public static string UnlockHint(EndingId id)
    {
        switch (id)
        {
            case EndingId.BadHome: return "在最深处，先带她走。";
            case EndingId.BadGate: return "在最深处，让她坐下。";
            case EndingId.True: return "三条岔路都走过的人，才知道箱子是谁装的。";
            default: return "";
        }
    }

    /// <summary>四个结局在图鉴里的固定展示顺序。</summary>
    public static readonly EndingId[] Order =
    {
        EndingId.BadHome, EndingId.Happy, EndingId.BadGate, EndingId.True
    };

    // ---------- 已达成记录 ----------

    public static bool IsAchieved(EndingId id)
    {
        var set = SaveSystem.Instance?.Data?.seenEndingIds;
        return set != null && set.Contains(Code(id));
    }

    /// <summary>写入「已见过这个结局」。返回是否为首次达成。</summary>
    public static bool MarkAchieved(EndingId id)
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null || id == EndingId.None) return false;
        if (data.seenEndingIds == null) data.seenEndingIds = new HashSet<string>();

        string key = Code(id);
        bool first = data.seenEndingIds.Add(key);
        StoryClue.OnEndingResolved(id);
        SaveSystem.Instance.Save();
        return first;
    }

    /// <summary>已经看过至少一个结局——此时图鉴底部提示「还有别的走法。」</summary>
    public static bool AnyAchieved()
    {
        for (int i = 0; i < Order.Length; i++)
            if (IsAchieved(Order[i])) return true;
        return false;
    }
}
