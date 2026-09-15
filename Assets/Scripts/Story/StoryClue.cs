using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 叙事 V2.0：冒险日志「线索页」C01–C27。
/// 数据来源：Docs/像素冒险：裂隙之刃_叙事重制策划案_V2.0.md §6。
/// 设计约束：全部按「打通节点」确定性发放，不依赖随机掉落——承诺感必须确定。
/// Tier 1 = 主线必见；Tier 2 = 探索者；Tier 3 = 考据者。
/// 关键路径不依赖任何 Tier 2/3 即可读懂。
/// </summary>
public static class StoryClue
{
    public struct ClueEntry
    {
        public string Id;
        public string Title;
        public string Unlock;
        public int Tier;
        public bool Mainline;
        public string Summary;
        public string Extra;

        public bool IsKey
        {
            get
            {
                for (int i = 0; i < KeyFive.Length; i++)
                    if (KeyFive[i] == Id) return true;
                return false;
            }
        }
    }

    /// <summary>真结局所需的 5 份关键线索。</summary>
    public static readonly string[] KeyFive = { "C02", "C08", "C11", "C13", "C17" };

    public static readonly ClueEntry[] All =
    {
        C("C01", "面包房的收据", "序章结束", 1, false,
            "一张压在桌上的收据。两份面包，三枚铜板，日期是她出发的前一晚。钱是你付的。",
            "她离开前借了你三枚铜板买面包，至今没还。公会没把这个写进失踪报告。"),

        C("C02", "她的剑痕拓片", "第 1 章 · 通关 Boss", 1, true,
            "石碑上的剑痕是新的。拓下来能对上她的起手式——第三招收势总是偏半寸，改不掉。",
            "她在这儿停留过。她往更深处去了。"),

        C("C03", "无名碑拓片", "第 2 章 · 通关 Boss", 1, true,
            "墓园最里侧有一排没有名字的公会墓碑，登记日期三块一组地排着：十四年前、十一年前、三个月前。",
            "有人把碑提前做好了。名字的位置还空着。"),

        C("C04", "灰鸦小队墓志", "第 2 章 · 精英关首次击败", 2, false,
            "十一年前的一支队伍。碑上只有一个记录员的名字：M。墓是空的。公会登记为「全队阵亡，骸骨未收」。",
            "有人在这碑前放过东西——不是鲜花，是一支笔。"),

        C("C05", "格雷的笔记·其一", "第 2 章 · 第 4 关通关", 2, false,
            "撕掉一半的猎人笔记。" +
            "「第五条：亡灵在往浅处走。它们不是被赶出来的，是逃出来的。」",
            "背面画了一个向下的箭头，旁边写着两个字：三年。"),

        C("C06", "逃亡者的火堆", "第 3 章（二周目）· 通关第 3 关", 2, false,
            "秘境深处的火堆还有余温。旁边散落着至少五个人的脚印，全部朝向更深处。",
            "没有人往回走。一个都没有。"),

        C("C07", "路标纸条·其七", "第 3 章（二周目）· 精英关首次击败", 3, false,
            "同样的纸条你已经捡到第七张了。纸质不同、年份不同，字迹却是同一个人：标示前方安全，标示水，标示不要停下。",
            "「如果你也下来了……小心公会。——M」"),

        C("C08", "装箱记录", "第 3 章（二周目）· 通关 Boss", 3, false,
            "装备科的领用台账。十一年里每一支「特殊委托队」的补给箱，装填人一栏都写着同一个潦草的 M。",
            "最后一页：本次第七队，拟三人。备注栏里只有一个词——不归。"),

        C("C09", "未署名的纸条", "第 3 章（二周目）· 通关 Boss", 2, true,
            "一张新得多的纸条，没有署名。" +
            "「他们每年这个时候都会做例行检修。别问检的是什么。」",
            "她写得很快。最后一笔拖了很长，像是写到一半被人叫走了。"),

        C("C10", "计时风车的刻度图", "第 4 章（二周目）· 通关第 5 关", 2, false,
            "草原上的风车叶片不是用来磨面的。齿轮上刻的是刻度，一圈三年。",
            "风车停住了，正对着某个位置——像是被谁按下了暂停键。"),

        C("C11", "时间轴最后一格", "第 4 章（二周目）· 通关 Boss", 3, false,
            "把十四年、十一年、八年、五年、两年前的档案按顺序摊开，你会发现间隔一模一样：三年。",
            "下一格是今年。那一格上写着三个名字。"),

        C("C12", "补给箱编号单", "第 5 章 · 通关第 3 关", 1, true,
            "搁浅补给船的舱单。箱子上的序列号连着号，是同一批。",
            "这批编号，和艾丽娅小队出发时领走的那一段，完全重合。"),

        C("C13", "任务简报（残）", "第 5 章 · 通关 Boss", 1, true,
            "公会文书，边缘被海水泡得发白。" +
            "「任务目标：将目标小队引导至最深处，确保其与『沉座』接触。」",
            "背面有铅笔写的半行，被人用力擦过：「……别信——」"),

        C("C14", "泡烂的箱底纸条", "第 5 章 · 通关 Boss", 1, true,
            "箱底压着一张新的纸条，署 M：「如果你也下来了……小心公会。」纸是新的，墨却干得很快。",
            "有人知道这条路迟早会有人走。她提前很多年就把话留在这儿了。"),

        C("C15", "十一年的箭镞", "第 6 章 · 精英关首次击败", 2, false,
            "卡在岩壁里的箭镞，腐蚀程度对不上这一层的年纪。箭羽的绑法早就被淘汰了。",
            "公会装备科十一年前换过一次绑法。上一个用这种绑法的人，署名也只有 M。"),

        C("C16", "磨损的盾牌残片", "第 6 章 · 通关第 2 关", 2, false,
            "盾面内侧刻着编号，漆被人反复涂黑过。刮开能看见一行小字：别记我。",
            "刻这行字的那个人，后来没有再刻第二面盾。"),

        C("C17", "沉座观察记录", "第 6 章 · 通关 Boss", 1, true,
            "黑曜结构不是矿物。它有节律，像在替谁喘气。" +
            "研究记录写着：诸神把它按在这里的时候，留了一个位置。",
            "那个位置需要坐一个人。资料不全，不知是为什么。"),

        C("C18", "会长密令（残）", "第 6 章 · 通关 Boss", 1, true,
            "密令烧剩一半，剩下的字还够读：「今年须有人坐上去。若目标不合，取其同行者。」",
            "落款没有名字，只有一个公会的火漆印。"),

        C("C19", "阿尔托的断缨", "第 7 章 · 精英关首次击败", 2, false,
            "枪缨断得很整齐，不是被扯断的——是有人用刀割的。割下来的人想留下一个证明。",
            "阿尔托从不在战前整理东西。这是最后一次例外。"),

        C("C20", "格雷的笔记·其三", "第 7 章 · 通关第 4 关", 2, false,
            "「我知道装我箱子的是谁了。我不怪她。她装了十一年，一天没敢停。」后面还有半句被烧掉了。",
            "剩下的墨迹里能认出一个字：谢。"),

        C("C21", "艾丽娅的发绳", "第 7 章 · 结局选择（A）", 1, true,
            "她的发绳。你把她背回来那天，它缠在你的手腕上，你一直没敢解。",
            "东西带回来了。人，带回来了一半。"),

        C("C22", "共鸣护符", "第 7 章 · 结局选择（B）", 1, true,
            "两块护符的纹路能对上。她说这是违禁品——协同作战的装备，公会三年前就停发了。",
            "停发理由写在一张单子上：「不提倡冒险者之间建立」。后面的字被涂掉了。"),

        C("C23", "沉座封印钥", "第 7 章 · 结局选择（C）", 1, true,
            "一枚不是钥匙的钥匙。它打开的东西在里面，坐上去的人在外面。",
            "你握着它，在裂缝口站了很久，想它的用处。"),

        C("C24", "最后的记录", "第 8 章 · 触发任意结局", 1, true,
            "任务结算单。人选 □，人数 □，结果 □。三个格子都是空的——没有人签字。",
            "公会不是没有记录。公会是让记录变成没有人的样子。"),

        C("C25", "空碑的照片", "《最后一个箱子》达成", 3, false,
            "第八块碑。名字的位置终于空到底了，石头却在风化——它本来就没打算用第二次。",
            "照片背面，有人用铅笔画了一个笑脸。"),

        C("C26", "艾丽娅的信", "酒馆空座位 · 累积查看 5 次后", 2, false,
            "压在收据下面的一封信，没有封口。「如果我没回来，别去问会长。去问装箱子的人。」",
            "她写这封信的时候，还没有人知道装箱子的是谁。"),

        C("C27", "梅莉莎的账单", "《最后一个箱子》达成", 3, false,
            "装备科领用单的最后一张，装填人写着全名：梅莉莎。备注栏第一次填了字。",
            "「下一队之后，我不再装箱。」这一行她写了六遍，每一遍的日期都往后推了三年。"),
    };

    // ---------- 查询 ----------

    public static ClueEntry Get(string id)
    {
        for (int i = 0; i < All.Length; i++)
            if (All[i].Id == id) return All[i];
        return default(ClueEntry);
    }

    public static bool Has(string id)
    {
        var set = SaveSystem.Instance?.Data?.seenClueIds;
        return set != null && set.Contains(id);
    }

    public static bool HasAll(SaveData d, string[] ids)
    {
        if (d?.seenClueIds == null || ids == null) return false;
        for (int i = 0; i < ids.Length; i++)
            if (!d.seenClueIds.Contains(ids[i])) return false;
        return true;
    }

    public static bool AllKeyClues => HasAll(SaveSystem.Instance?.Data, KeyFive);

    public static int GrantedCount
    {
        get
        {
            var set = SaveSystem.Instance?.Data?.seenClueIds;
            return set != null ? set.Count : 0;
        }
    }

    // ---------- 发放 ----------

    /// <summary>统一发放入口。重复发放返回 false，调用方不必去重。</summary>
    public static bool Grant(string id, bool save = true)
    {
        if (string.IsNullOrEmpty(id)) return false;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return false;
        if (data.seenClueIds == null) data.seenClueIds = new HashSet<string>();

        if (data.seenClueIds.Contains(id)) return false;
        data.seenClueIds.Add(id);

        var e = Get(id);
        AdventureCodex.CompleteSide(e.Id);
        if (save) SaveSystem.Instance.Save();
        Debug.Log("[StoryClue] 获得线索页 " + id + " " + e.Title);
        return true;
    }

    /// <summary>序章结束。</summary>
    public static void OnPrologueDone() { Grant("C01", false); Save(); }

    /// <summary>章节 Boss 通关。第 1/2/3/4/5/6 章有对应线索页。</summary>
    public static void OnChapterBossCleared(int chapter)
    {
        switch (chapter)
        {
            case 1: Grant("C02", false); break;
            case 2: Grant("C03", false); break;
            case 3: Grant("C08", false); Grant("C09", false); break;
            case 4: Grant("C11", false); break;
            case 5: Grant("C13", false); Grant("C14", false); break;
            case 6: Grant("C17", false); Grant("C18", false); break;
        }
        Save();
    }

    /// <summary>章节精英关首次击败。</summary>
    public static void OnEliteFirstKill(int chapter)
    {
        switch (chapter)
        {
            case 2: Grant("C04", false); break;
            case 3: Grant("C07", false); break;
            case 6: Grant("C15", false); break;
            case 7: Grant("C19", false); break;
        }
        Save();
    }

    /// <summary>普通关推进。stageIndex 从 0 起算。</summary>
    public static void OnStageCleared(int chapter, int stageIndex)
    {
        switch (chapter)
        {
            case 2: if (stageIndex >= 3) Grant("C05", false); break;
            case 3: if (stageIndex >= 2) Grant("C06", false); break;
            case 4: if (stageIndex >= 4) Grant("C10", false); break;
            case 5: if (stageIndex >= 2) Grant("C12", false); break;
            case 6: if (stageIndex >= 1) Grant("C16", false); break;
            case 7: if (stageIndex >= 3) Grant("C20", false); break;
        }
        Save();
    }

    /// <summary>第 7 章转折点：choice 1=A / 2=B / 3=C。</summary>
    public static void OnEndingChoice(int choice)
    {
        if (choice == 1) Grant("C21", false);
        else if (choice == 2) Grant("C22", false);
        else if (choice == 3) Grant("C23", false);
        Save();
    }

    /// <summary>结局触发。trueEnd 时额外发放 Tier-3 两份。</summary>
    public static void OnEndingResolved(StoryEnding.EndingId id)
    {
        Grant("C24", false);
        if (id == StoryEnding.EndingId.True)
        {
            Grant("C25", false);
            Grant("C27", false);
        }
        Save();
    }

    static void Save() { SaveSystem.Instance?.Save(); }

    static ClueEntry C(string id, string title, string unlock, int tier, bool mainline, string summary, string extra)
    {
        return new ClueEntry
        {
            Id = id, Title = title, Unlock = unlock, Tier = tier,
            Mainline = mainline, Summary = summary, Extra = extra
        };
    }
}
