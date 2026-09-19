using System;
using System.Collections.Generic;

/// <summary>
/// 叙事 V2.0：第 2–8 章的关键对话节点 N2–N8。
/// 数据来源：Docs/像素冒险：裂隙之刃_叙事重制策划案_V2.0.md §5。
/// 承载原则：只放「情绪峰值」和「不可逆决定」，背景与考据一律走冒险日志线索页。
/// 每个节点 ≤ 12 句、≤ 30 秒、可跳过。
/// </summary>
public static class ChapterStoryBeats
{
    /// <summary>
    /// 章节 Boss 后尝试播放节点脚本。
    /// 返回 true 表示有脚本在播（调用方应等回调）；false 表示无脚本，直接走奖励流程。
    /// </summary>
    public static bool TryPlayPostBoss(int chapter, Action onDone)
    {
        switch (chapter)
        {
            case 2: PlayN2(onDone); return true;
            case 3: PlayN3(onDone); return true;
            case 4: PlayN4(onDone); return true;
            case 5: PlayN5(onDone); return true;
            case 6: PlayN6(onDone); return true;
            case 7: PlayN7(onDone); return true;
            case 8: PlayN8(onDone); return true;
            default: return false;
        }
    }

    /// <summary>
    /// 章节进关后、首波怪物出现前尝试播放战前剧情（与战后 TryPlayPostBoss 对称的两段式）。
    /// 返回 true 表示有脚本在播（调用方应等回调）；false 表示无脚本，直接刷首波。
    /// 文案方向（2026-09-19 定调）：世界观 + 「玩家 × 艾丽娅」的过往
    /// （三枚铜板买面包 / 转正仪式 / 她介绍你进公会 / 第七支小队），
    /// 并讲清「这是什么地方 / 为什么出现怪 / 跟主线什么关系」。
    /// 配置方式与战后一致：静态数组 + StoryDirector；后续若出剧情配置表可一并迁表。
    /// </summary>
    public static bool TryPlayPreBattle(int chapter, Action onDone)
    {
        switch (chapter)
        {
            case 1: PlayPre1(onDone); return true;
            case 2: PlayPre2(onDone); return true;
            case 3: PlayPre3(onDone); return true;
            case 4: PlayPre4(onDone); return true;
            case 5: PlayPre5(onDone); return true;
            case 6: PlayPre6(onDone); return true;
            case 7: PlayPre7(onDone); return true;
            case 8: PlayPre8(onDone); return true;
            default: return false;
        }
    }

    // ---------- 战前剧情｜第 1–8 章：每章 2–4 句，首波前弹出 ----------

    static void PlayPre1(Action onDone)
    {
        var beats = new List<StoryBeat>
        {
            StoryDirector.Solo("艾丽娅", "还记得吗？三年前你揣着三枚铜板买面包，排在我后面。", StoryPortraits.Xiaomei),
            StoryDirector.Solo("艾丽娅", "现在你是公会第七支小队的队长了。今天只是新人试炼，别紧张。", StoryPortraits.Xiaomei),
            StoryDirector.Narration("这里是裂隙第一层的暮影森林。怪物是从缝里渗出来的——清完这波，把样本带回去交差。"),
            StoryDirector.Solo("你", "转正仪式上说好的，债没还清之前我哪都不去。走吧。", StoryPortraits.Player),
        };
        StoryDirector.Ensure().Play(beats, onDone);
    }

    static void PlayPre2(Action onDone)
    {
        var beats = new List<StoryBeat>
        {
            StoryDirector.Narration("幽冥墓园：城外的旧墓园，三年前起埋着的东西开始往外爬。"),
            StoryDirector.Solo("艾丽娅", "这片园里埋着以前的冒险者。公会对外说是地脉波动——你信吗？", StoryPortraits.Xiaomei),
            StoryDirector.Solo("你", "第七支小队之前的六支，全都进过裂缝。一块碑都没立回来。", StoryPortraits.Player),
            StoryDirector.Solo("艾丽娅", "所以我们才要走到最里面，看看他们到底碰上了什么。", StoryPortraits.Xiaomei),
        };
        StoryDirector.Ensure().Play(beats, onDone);
    }

    static void PlayPre3(Action onDone)
    {
        var beats = new List<StoryBeat>
        {
            StoryDirector.Solo("艾丽娅", "岔路这边是翡翠秘境。十一年前，有一整支小队消失在这一层。", StoryPortraits.Xiaomei),
            StoryDirector.Solo("艾丽娅", "你转正那天，会长亲自给你别上徽章——他看我们的眼神，像在数还剩几个人。", StoryPortraits.Xiaomei),
            StoryDirector.Solo("你", "路标是新的。有人一直走在我们前面。", StoryPortraits.Player),
        };
        StoryDirector.Ensure().Play(beats, onDone);
    }

    static void PlayPre4(Action onDone)
    {
        var beats = new List<StoryBeat>
        {
            StoryDirector.Narration("晨曦草原：裂隙第二层的风车草原。叶片一圈三年，替谁数着日子。"),
            StoryDirector.Solo("你", "艾丽娅，当年介绍我进公会的时候，你是不是早就知道会有今天？", StoryPortraits.Player),
            StoryDirector.Solo("艾丽娅", "我只知道第七支小队不能白组。走吧——今天风停了，这不正常。", StoryPortraits.Xiaomei),
        };
        StoryDirector.Ensure().Play(beats, onDone);
    }

    static void PlayPre5(Action onDone)
    {
        var beats = new List<StoryBeat>
        {
            StoryDirector.Solo("艾丽娅", "三条岔路在海岛遗迹汇合。那艘补给船的残骸是新的，最多三个月。", StoryPortraits.Xiaomei),
            StoryDirector.Solo("你", "三个月前……正好是我们转正的月份。", StoryPortraits.Player),
            StoryDirector.Solo("艾丽娅", "怪物越来越像被赶过来的。有什么东西在深处催它们。", StoryPortraits.Xiaomei),
        };
        StoryDirector.Ensure().Play(beats, onDone);
    }

    static void PlayPre6(Action onDone)
    {
        var beats = new List<StoryBeat>
        {
            StoryDirector.Narration("巨岩深窟：再往下，诸神留下的东西就压在这一层底下。"),
            StoryDirector.Solo("艾丽娅", "当年我拉你进公会，你说『面包钱还没还我呢』。现在我怕你还想讨这笔债。", StoryPortraits.Xiaomei),
            StoryDirector.Solo("你", "三枚铜板，记着呢。等出去了，你请。", StoryPortraits.Player),
        };
        StoryDirector.Ensure().Play(beats, onDone);
    }

    static void PlayPre7(Action onDone)
    {
        var beats = new List<StoryBeat>
        {
            StoryDirector.Narration("赤焰炼狱：通往最深处前的最后一层。这里的热不是火——是裂缝本身在烧。"),
            StoryDirector.Solo("你", "她把话说完就自己先进来了。炼狱烧不穿她留下的脚印。", StoryPortraits.Player),
            StoryDirector.Solo("你", "第七支小队走到这儿的，只剩我一个。答案和债，今天一起结。", StoryPortraits.Player),
        };
        StoryDirector.Ensure().Play(beats, onDone);
    }

    static void PlayPre8(Action onDone)
    {
        var beats = new List<StoryBeat>
        {
            StoryDirector.Narration("永霜雪境：裂隙最深处，雪一直下。这一趟的终点就在前面。"),
            StoryDirector.Solo("你", "从三枚铜板到今天。艾丽娅，这笔账，马上就能结清了。", StoryPortraits.Player),
        };
        StoryDirector.Ensure().Play(beats, onDone);
    }

    // ---------- N2｜第 2 章（分支 A · 亡者之径）：这不是第一次 ----------

    static void PlayN2(Action onDone)
    {
        var beats = new List<StoryBeat>
        {
            StoryDirector.Narration("墓园最里侧有一排没有名字的公会墓碑。你数了数，一共七块。"),
            StoryDirector.Narration("碑上的日期是三块一组排着的：十四年前。十一年前。三个月前。"),
            StoryDirector.Solo("你", "……七块。", StoryPortraits.Player),
            StoryDirector.Narration("最老那块刻着「M. 灰鸦小队 记录员」。再往旁边挪一步，还有第八块——空的。"),
            StoryDirector.Narration("名字的位置留着。石头却是新的。有人提前把碑做好了。"),
            StoryDirector.Solo("你", "他们连名字都不打算写。", StoryPortraits.Player),
        };
        StoryDirector.Ensure().Play(beats, onDone);
    }

    // ---------- N3｜第 3 章（分支 B · 生者之径，二周目）：遇到梅莉莎 ----------

    static void PlayN3(Action onDone)
    {
        var beats = new List<StoryBeat>
        {
            StoryDirector.Narration("遗迹深处的藤蔓后面，居然有一处收拾干净的营地。篝火还热着。"),
            StoryDirector.Solo("?", "别动。你踩到我第二道线了。", StoryPortraits.Melissa),
            StoryDirector.Narration("一个头发花白的女人从阴影里出来，手里攥着一支笔，不是刀。"),
            StoryDirector.Solo("?", "我等了三天才等到下一个活人走进来。坐下，先别问，我写字慢。", StoryPortraits.Melissa),
            StoryDirector.Solo("你", "……箱子里的纸条，是你塞的。", StoryPortraits.Player),
            StoryDirector.Solo("梅莉莎", "每一支被挑中的小队，箱子都是我装的。我往每个里面塞一张。", StoryPortraits.Melissa),
            StoryDirector.Solo("梅莉莎", "他们以为那个 M 是领队的名字。", StoryPortraits.Melissa),
            StoryDirector.Solo("梅莉莎", "其实那只是……装箱的人。", StoryPortraits.Melissa),
            StoryDirector.Narration("她把笔按在纸上，很久没有落下去。"),
            StoryDirector.Solo("梅莉莎", "十一年了。我装箱的时候就预感的那些事，一件都没错过。", StoryPortraits.Melissa),
        };
        StoryDirector.Ensure().Play(beats, () =>
        {
            StoryProgress.AddBond(StoryProgress.NpcMelissa, 20);
            onDone?.Invoke();
        });
    }

    // ---------- N4｜第 4 章（分支 C · 计时之径，二周目）：你的名字也在表上 ----------

    static void PlayN4(Action onDone)
    {
        var beats = new List<StoryBeat>
        {
            StoryDirector.Narration("风车停住了。不是坏了——它的叶片正对着某个位置，像是被谁按下暂停键。"),
            StoryDirector.Narration("齿轮上刻的不是图腾，是刻度。一圈三年。"),
            StoryDirector.Solo("你", "这不是用来稳定裂缝的设备。", StoryPortraits.Player),
            StoryDirector.Narration("轴上还有一列没刻完的名字。最后两个是空的。再往下，有一道新的划痕——"),
            StoryDirector.Solo("你", "……这是三个月前刻的。", StoryPortraits.Player),
            StoryDirector.Narration("你沿着刻度往上数。第七格的位置上，有一个名字你认得：你自己。"),
        };
        StoryDirector.Ensure().Play(beats, onDone);
    }

    // ---------- N5｜第 5 章（三路汇合）：任务简报 ★第一次真正的冲击 ----------

    static void PlayN5(Action onDone)
    {
        var beats = new List<StoryBeat>
        {
            StoryDirector.Narration("搁浅的补给船歪在礁石里。舱门是被人从里面撬开的。"),
            StoryDirector.Narration("补给箱码得整整齐齐，箱面编号你能背出来——那是她的队号。"),
            StoryDirector.Solo("你", "她不是去执行普通委托吗。", StoryPortraits.Player),
            StoryDirector.Narration("箱底压着一张纸条，纸是新的。上面只有一个名字：——M"),
            StoryDirector.Narration("你把它抖开，底下是一份泡得发白的简报。字迹能认出来："),
            StoryDirector.Narration("「任务目标：将目标小队引导至最深处，确保其与『沉座』接触。」"),
            StoryDirector.Solo("你", "……这不是调查任务。", StoryPortraits.Player),
            StoryDirector.Narration("你的手抖了一下。你把手按在膝盖上，按了很久才松开。"),
        };
        StoryDirector.Ensure().Play(beats, () =>
        {
            StoryProgress.AddBond(StoryProgress.NpcMaster, -10);
            onDone?.Invoke();
        });
    }

    // ---------- N6｜第 6 章：沉座 + 不再救援 ----------

    static void PlayN6(Action onDone)
    {
        var beats = new List<StoryBeat>
        {
            StoryDirector.Narration("洞穴正中悬着一枚黑曜结构，有节奏地搏动，像在替什么人喘气。"),
            StoryDirector.Narration("周围散落着更旧的战斗痕迹。十四年前的盾。十一年前那批的箭。"),
            StoryDirector.Solo("你", "沉座。是要有人坐上去的东西。", StoryPortraits.Player),
            StoryDirector.Narration("你在十一年的那堆残骸里翻到半张烧过的纸。只剩两行字："),
            StoryDirector.Narration("「若目标小队成功削弱核心，派遣第二梯队收尾。若失败，则封锁入口，不再派遣救援。」"),
            StoryDirector.Solo("你", "他们不是没派人救。他们压根就没打算派。", StoryPortraits.Player),
        };
        StoryDirector.Ensure().Play(beats, onDone);
    }

    // ---------- N7｜第 7 章：关键转折点 · 三选一（唯一改变结局的选择） ----------

    /// <summary>是否已经做过第 7 章的结局选择（同一周目不可更改）。</summary>
    public static bool EndingChoiceDone
    {
        get { return (SaveSystem.Instance?.Data?.endingChoice ?? 0) > 0; }
    }

    static void PlayN7(Action onDone)
    {
        var beats = new List<StoryBeat>
        {
            StoryDirector.Narration("黑雾从她身上崩散。她软下去，你把她接住。"),
            StoryDirector.Solo("艾丽娅", "……你追到这儿来了。真笨。", StoryPortraits.Xiaomei),
            StoryDirector.Solo("艾丽娅", "听我说，别浪费时间。它不是想杀我们——", StoryPortraits.Xiaomei),
            StoryDirector.Solo("艾丽娅", "它是想让我坐上去。坐进那个沉座。", StoryPortraits.Xiaomei),
            StoryDirector.Solo("你", "坐在上面会怎么样。", StoryPortraits.Player),
            StoryDirector.Solo("艾丽娅", "不清楚。也许谁坐上去，就不是谁了。", StoryPortraits.Xiaomei),
            StoryDirector.Narration("她抬起手，抓住你的袖口，力气小得可怜。"),
            StoryDirector.Solo("艾丽娅", "我欠你三枚铜板，买面包那次。我一直记着。", StoryPortraits.Xiaomei),
            StoryDirector.Solo("艾丽娅", "现在这笔账，你可以自己挑怎么还。", StoryPortraits.Xiaomei),
            new StoryBeat
            {
                leftName = "你",
                leftPortraitId = StoryPortraits.Player,
                text = "现在这笔账，你想怎么算？",
                speaker = -1,
                choices = new[]
                {
                    "先带你回去。剩下的我以后再说。",        // A → BadHome
                    "一起去最深处。赌一把，把那东西砸了。",   // B → Happy / True
                    "……如果坐上去能封住它，那就坐。"          // C → BadGate
                }
            }
        };

        StoryDirector.Ensure().Play(beats, null, choice => ApplyEndingChoice(choice, onDone));
    }

    /// <summary>选择后的即时回响——必须有，否则选择感会消失。</summary>
    static void ApplyEndingChoice(int index, Action onDone)
    {
        var data = SaveSystem.Instance?.Data;
        if (data != null) data.endingChoice = index + 1;

        string id = index == 0 ? "A" : index == 1 ? "B" : "C";
        StoryProgress.SetChoice(7, id);
        StoryClue.OnEndingChoice(index + 1);

        StoryBeat echo;
        if (index == 0)
        {
            echo = StoryDirector.Solo("艾丽娅", "……你这个人，怎么老选这种答案。\n但谢谢你。真的。", StoryPortraits.Xiaomei);
            StoryProgress.AddBond(StoryProgress.NpcXiaomei, 10);
        }
        else if (index == 1)
        {
            echo = StoryDirector.Solo("艾丽娅", "好啊。\n那这次换我跟在你后面。", StoryPortraits.Xiaomei);
            StoryProgress.AddBond(StoryProgress.NpcXiaomei, 15);
        }
        else
        {
            echo = StoryDirector.Solo("艾丽娅", "……我以为你会骂我傻。", StoryPortraits.Xiaomei);
            StoryProgress.AddBond(StoryProgress.NpcXiaomei, 5);
        }

        StoryDirector.Ensure().Play(new List<StoryBeat> { echo }, () =>
        {
            SaveSystem.Instance?.Save();
            onDone?.Invoke();
        });
    }

    // ---------- N8｜第 8 章：结局演出 ----------

    static void PlayN8(Action onDone)
    {
        var data = SaveSystem.Instance?.Data;
        var id = StoryEnding.Resolve(data);

        if (id == StoryEnding.EndingId.None)
        {
            // 理论上不会发生（第 8 章必然已选过）。兜底：不留黑屏，直接放行。
            onDone?.Invoke();
            return;
        }

        StoryEnding.MarkAchieved(id);

        var beats = new List<StoryBeat>();
        switch (id)
        {
            case StoryEnding.EndingId.Happy: BuildHappy(beats); break;
            case StoryEnding.EndingId.BadHome: BuildBadHome(beats); break;
            case StoryEnding.EndingId.BadGate: BuildBadGate(beats); break;
            case StoryEnding.EndingId.True: BuildTrue(beats); break;
        }

        StoryDirector.Ensure().Play(beats, () =>
        {
            // 通关第 8 章：清空本周目分叉锁定，二周目可重选第 3/4 章
            if (data != null) data.chosenBranch = 0;
            SaveSystem.Instance?.Save();
            onDone?.Invoke();
        });
    }

    static void BuildHappy(List<StoryBeat> b)
    {
        b.Add(StoryDirector.Narration("黑曜结构在你面前碎成八瓣。第八块空碑上的名字，终于没人刻得上去了。"));
        b.Add(StoryDirector.Solo("裂缝意志", "……你们以为砸了它，就结束了？", StoryPortraits.RiftWill));
        b.Add(StoryDirector.Solo("艾丽娅", "结束了。对我们来说，结束了。", StoryPortraits.Xiaomei));
        b.Add(StoryDirector.Narration("你们拼命往回跑。跑出口的时候天亮得刺眼。"));
        b.Add(StoryDirector.Solo("艾丽娅", "……三枚铜板。我还没还。", StoryPortraits.Xiaomei));
        b.Add(StoryDirector.Solo("你", "不急。你欠账的时间比我长多了。", StoryPortraits.Player));
        b.Add(StoryDirector.Solo("艾丽娅", "那我用以后还。够不够长？", StoryPortraits.Xiaomei));
        b.Add(StoryDirector.Narration("——《像素冒险：裂隙之刃》 追上她的人，终于赶上了。"));
    }

    static void BuildBadHome(List<StoryBeat> b)
    {
        b.Add(StoryDirector.Narration("你没往下走。你把她背了回来。"));
        b.Add(StoryDirector.Narration("她确实活下来了。医官说恢复得不错。"));
        b.Add(StoryDirector.Narration("只是从那以后，她每隔几天就会忘记一件事。先是裂隙里的名字，再是阿尔托的全名。"));
        b.Add(StoryDirector.Solo("艾丽娅", "……今天我是不是又问过你一遍，我欠你多少钱？", StoryPortraits.Xiaomei));
        b.Add(StoryDirector.Solo("你", "三枚铜板。你每天都问，我每天都答。", StoryPortraits.Player));
        b.Add(StoryDirector.Solo("艾丽娅", "那你别嫌烦。", StoryPortraits.Xiaomei));
        b.Add(StoryDirector.Narration("她回来了一半。剩下那一半，留给了没下去的那个人。"));
        b.Add(StoryDirector.Narration("——《像素冒险：裂隙之刃》 你救回来了她，但没救回来全部。"));
    }

    static void BuildBadGate(List<StoryBeat> b)
    {
        b.Add(StoryDirector.Narration("她坐了上去。黑曜合拢的时候，她回头看了你最后一眼。"));
        b.Add(StoryDirector.Narration("裂隙真的止住了。王国今年没有新的裂缝记录。公会照常发任务、照常清点人数、照常涨价。"));
        b.Add(StoryDirector.Solo("会长", "牺牲少数保全多数——这就是结算方式。", StoryPortraits.GuildMaster));
        b.Add(StoryDirector.Solo("你", "她的名字，写进去吗。", StoryPortraits.Player));
        b.Add(StoryDirector.Narration("会长没有回答。你也知道答案。"));
        b.Add(StoryDirector.Narration("每年这个时候，你都会去那道裂缝口站着，等一件不会发生的事。"));
        b.Add(StoryDirector.Narration("——《像素冒险：裂隙之刃》 世界保住了。这笔账，只有你一个人记得。"));
    }

    static void BuildTrue(List<StoryBeat> b)
    {
        b.Add(StoryDirector.Narration("沉座崩解的最后，你看到一个不属于这里的东西卡在晶格里——一张叠了十一年的纸条。"));
        b.Add(StoryDirector.Solo("梅莉莎", "那是我塞的。第九支队伍的箱子。", StoryPortraits.Melissa));
        b.Add(StoryDirector.Narration("她从崩落的裂缝出口走进来，径直走到裂隙的中心，走得比你还稳。"));
        b.Add(StoryDirector.Solo("梅莉莎", "我装箱装了十一年。今天开始，我不用装了。", StoryPortraits.Melissa));
        b.Add(StoryDirector.Narration("——《像素冒险：裂隙之刃》 装了十一年箱子的人，今天空着手回家。"));
    }

    // ---------- 第 1–8 章开场：标题 + 介绍（写入 ChapterSplash） ----------

    /// <summary>章节过场大标题。地图名取自 chapter_theme_map，与关卡内一致。</summary>
    public static string IntroTitle(int chapter)
    {
        switch (chapter)
        {
            case 1: return "第 1 章 · 暮影森林";
            case 2: return "第 2 章 · 幽冥墓园";
            case 3: return "第 3 章 · 翡翠秘境";
            case 4: return "第 4 章 · 晨曦草原";
            case 5: return "第 5 章 · 海岛遗迹";
            case 6: return "第 6 章 · 巨岩深窟";
            case 7: return "第 7 章 · 赤焰炼狱";
            case 8: return "第 8 章 · 永霜雪境";
            default: return null;
        }
    }

    /// <summary>
    /// 章节过场正文：两行，第一行交代「这是什么地方 / 发生了什么」，
    /// 第二行是氛围。控制在 40 字内，过场只停留 2.5 秒。
    /// </summary>
    public static string OpeningLine(int chapter)
    {
        switch (chapter)
        {
            case 1: return "公会的新人试炼场，裂隙第一层。\n阳光还照得进来，怪物也不算太狠。";
            case 2: return "城外的旧墓园。三年前起，埋着的东西开始往外爬。\n公会对外说是地脉波动。";
            case 3: return "裂隙第二层的岔路之一，植被疯长。\n有人在这儿留过路标，而且一直在翻新。";
            case 4: return "裂隙第二层的岔路之一，风车还在转。\n没人记得它到底在替谁数日子。";
            case 5: return "三条岔路在这里汇合，遗迹半沉在水下。\n你会在箱子里翻出第一批不该存在的新东西。";
            case 6: return "往更深处的路从这里开始。\n底下压着一样东西，是诸神留下的。";
            case 7: return "通往最深处之前的最后一段。\n这里的热，不是火。";
            case 8: return "裂隙的最深处，也是这一趟的终点。\n该做的决定你已经做完了。";
            default: return null;
        }
    }
}
