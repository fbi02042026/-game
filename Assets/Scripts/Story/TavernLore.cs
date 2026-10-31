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

        // 【2026-10-23 合并方案】支线章退役、并入主线（1→8 线性）：判断改为「是否打过 3/4 章」。
        // 原 chosenBranch==3/4 派定判断随分叉一起退役。
        bool branchOpen = d != null
            && (StoryEnding.HasCleared(d, 3) || StoryEnding.HasCleared(d, 4));

        if (branchOpen)
            return "裂隙第二层有两条岔路，以前没人活着回来讲过。\n你要么是第一个，要么是死得最慢的那个。";
        if (far >= 6)
            return "你知道最难受的是什么？\n他们真觉得自己在救人。这就是最难办的部分。\n我在这店里蹲了六年，一半为了情报，一半为了她。";
        if (far >= 5)
            return "你找到那个箱子了。\n那我就不装了——<b>装那个箱子的人还活着。</b> 我见过。\n她只说过一句：下面那东西，是在等一个人。";
        if (far >= 3)
            return "公会每年这个时候都会「例行检修」。\n检修什么没人说，但检修单我记得。每年都有。\n——顺便说一句，这店里的酒不好喝。我留下，不是为酒。";
        return "去年也来过一队愣头青，问了一样的问题。\n他们现在不问了。";
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

    /// <summary>老板娘按进度说的话（闲聊正句）。她负责给出数字与情绪。</summary>
    /// <para>【2026-10-31 主人拍板「按情节/点击次数改文案，不要总是这两句」】：
    /// 传累计闲聊次数 count（SaveData.tavernGossipCount）时——每三回里前两回说进度台词（情节线，
    /// 句内多句按 count 轮换），第三回说一句酒馆闲话（InnkeeperChatter 氛围池）；
    /// 不传（旧调用口：Toast / 选项兜底文案）按会话内轮换，口径同 SquadQuote。</para>
    /// </summary>
    public static string InnkeeperLine(int count = -1)
    {
        var d = SaveSystem.Instance?.Data;
        int far = d == null ? 0 : HighestClearedChapter(d);

        string line;
        if (StoryEnding.AnyAchieved())
            line = "这张桌子我留着。谁要坐我都说有人了。\n我不缺这店的钱。我缺一个说法。";
        else if (far >= 7)
            line = "三个月前最后一晚，那孩子在这儿坐到天亮。\n她说下面有人在数人头。我当她是喝多了。";
        else if (far >= 5)
            line = "登记簿我看得懂。有些人回来，有些名字更新了位置。\n名字我另抄了一份。抄了整整一本。";
        else if (far >= 2)
            line = "这几年新来的见习越来越多了。上一波还是去年冬天。\n他们点酒都点最便宜的，好像省下这几枚铜板，就能活着回来。";
        else
            line = "第一次来？酒钱可以先记着。\n这儿的账，还没人不认过。";

        if (count >= 0)
        {
            // 第三回说氛围闲话（池内按 count/3 轮换）；前两回走进度台词
            if (count % 3 == 2)
                return InnkeeperChatter[(count / 3) % InnkeeperChatter.Length];
            return RotateLine(line, count);
        }
        return RotateLine(line, _loreLineIdx++);
    }

    // 【2026-10-31 主人拍板】老板娘闲聊开场招呼池：按累计闲聊次数轮换（TavernUI 开场第一拍）。
    public static readonly string[] InnkeeperGreetings =
    {
        "哟，坐下聊！今天想听点什么？",
        "来啦？杯子自己拿，想听什么挑一个。",
        "又来了。这回想聊人还是聊事？",
        "坐。今天的酒不算你账上。",
        "稀客——虽然你这稀客来得比谁都勤。",
    };

    public static string InnkeeperGreeting(int count)
    {
        return InnkeeperGreetings[Mathf.Abs(count) % InnkeeperGreetings.Length];
    }

    // 【2026-10-31 主人拍板】酒馆闲话池（氛围向，与进度无关；闲聊第三回说一句）。
    // 【2026-10-31 二次拍板】补两条独眼向：老板娘嘴上嫌弃、账上记着，等于把"互有好感"摊在日常里。
    // 文案为代拟稿，主人可随时改字。
    static readonly string[] InnkeeperChatter =
    {
        "雨季一到，来喝酒的佣兵就少。裂隙里那些，倒是一个没少。",
        "楼下那桌又在吹牛，说见着了二层的光。光有什么好吹的，活着回来才算数。",
        "今晚炖肉放了不少胡椒。你们这种卖命的，吃口热的比什么都实在。",
        "有人在墙上刻了一道杠，说是一天一层。后来没再来刻过。",
        "别老盯着那张空桌子看。人没回来，桌子我天天擦。",
        "独眼那家伙，一只眼睛还老往我这儿瞟。装得跟真看不见似的。",
        "他欠我的账，我一条一条都记着。等哪天他想赖，我就拿账本砸他。",
    };

    // 多句台词轮换：按 \n 拆句取一句（idx 传累计闲聊次数即天然轮换；不传 count 的旧口用 _loreLineIdx 会话内轮）。
    // 只接「句子相互独立」的台词——拆开后没头没尾的（情报商的「要么是第一个…」等）不接。
    static int _loreLineIdx;

    static string RotateLine(string line, int idx)
    {
        if (string.IsNullOrEmpty(line)) return line;
        string[] parts = line.Split('\n');
        if (parts.Length <= 1) return line;
        return parts[Mathf.Abs(idx) % parts.Length];
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

    // ---------- 老队员的记忆（2026-10-24 T4：阿尔托/格雷最小台词包） ----------

    // 【2026-10-24 主人拍板「开工·性格尽量鲜明」】两人全工程此前 0 台词，BadHome 结局提「阿尔托的全名」
    // 却没人听过他说话——这里补「出发前」的记忆台词。全部以回忆口吻呈现（人已随队深入，不出现场）。
    // 性格口径：阿尔托=正直热血骑士（凹痕=护住过谁 / 请酒赊账都是flag）；格雷=嘴硬心软老猎人（不写好话，箭备双份）。
    static readonly string[] SquadQuotes =
    {
        "阿尔托（出发前夜）：\n「徽章擦得再亮，不如盾上多一道新凹痕——凹痕说明你把谁护住了。」",
        "阿尔托（出发前夜）：\n「等这趟回来，我请在座所有人的酒。要是没回来……那就算我赖账，账房会记着的。」",
        "格雷（擦着箭头，头也不抬）：\n「委托单上把『普通』写得越起劲的，越不普通。字越少，事越大。」",
        "格雷（把箭壶推过来）：\n「……我这人不会说好话。箭备了双份，有一份是给你们的。别弄断了，箭杆不好配。」",
    };

    static int _squadIdx;

    /// <summary>轮换一条老队员的记忆台词（会话内轮换，避免连出同一条）。</summary>
    public static string SquadQuote()
    {
        if (SquadQuotes == null || SquadQuotes.Length == 0) return "";
        string s = SquadQuotes[_squadIdx % SquadQuotes.Length];
        _squadIdx++;
        return s;
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
