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
    /// 【2026-10-23 主人二次拍板「8岛全通」】N3/N4 恢复周目分流，判据改 8 岛全通
    /// （SaveData.HasClearedAllSideIslands，跨洗牌轮持久）：未全通播平替版 Alt，全通后（NG+ 重打）播完整版。
    /// </summary>
    public static bool TryPlayPostBoss(int chapter, Action onDone)
    {
        switch (chapter)
        {
            case 1: PlayN1(onDone); return true;
            case 2: PlayN2(onDone); return true;
            case 3: if (SaveSystem.Instance?.Data?.HasClearedAllSideIslands() == true) PlayN3(onDone); else PlayN3Alt(onDone); return true;
            case 4: if (SaveSystem.Instance?.Data?.HasClearedAllSideIslands() == true) PlayN4(onDone); else PlayN4Alt(onDone); return true;
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
    /// （三枚铜板买面包 / 转正仪式 / 她介绍你进公会 / 第八支小队），
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

    /// <summary>
    /// 【2026-10-19 防卡死】进关时把本章战前/战后剧情会用到的立绘与背景提前读进缓存
    /// （StoryAssetLoader 有缓存，重复调无害；同步加载几张图，毫秒级）。
    /// 缺图不中断：StoryPortraits / StoryBackgrounds 自带占位图降级 + LogWarning。
    /// </summary>
    public static void WarmupChapterAssets(int chapter)
    {
        // 立绘：全章固定阵容（player/xiaomei 全章，melissa 第 3/8 章，guildmaster 第 8 章结局）
        StoryPortraits.Warmup(
            StoryPortraits.Player, StoryPortraits.Xiaomei,
            StoryPortraits.Melissa, StoryPortraits.GuildMaster);
        string[] bgs;
        switch (chapter)
        {
            case 2: bgs = new[] { StoryBackgrounds.GraveSeven }; break;
            case 3: bgs = new[] { StoryBackgrounds.MelissaCamp }; break;
            case 4: bgs = new[] { StoryBackgrounds.Windmill }; break;
            case 5: bgs = new[] { StoryBackgrounds.WreckShip }; break;
            case 6: bgs = new[] { StoryBackgrounds.ThroneSeat }; break;
            case 7: bgs = new[] { StoryBackgrounds.AbyssChoice }; break;
            case 8: bgs = new[] { StoryBackgrounds.EndHappy, StoryBackgrounds.EndBadHome, StoryBackgrounds.EndBadGate, StoryBackgrounds.EndTrue }; break;
            default: bgs = null; break;
        }
        if (bgs == null) return;
        for (int i = 0; i < bgs.Length; i++)
            StoryBackgrounds.Get(bgs[i]);
    }

    static void PlayPre1(Action onDone)
    {
        var beats = new List<StoryBeat>
        {
            StoryDirector.Solo("艾丽娅", "还记得吗？三年前你揣着三枚铜板买面包，排在我后面。", StoryPortraits.Xiaomei),
            StoryDirector.Solo("艾丽娅", "现在你是公会第八支小队的队长了。今天只是新人试炼，别紧张。", StoryPortraits.Xiaomei),
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
            StoryDirector.Solo("你", "第八支小队之前的七支，全都进过裂缝。一块碑都没立回来。", StoryPortraits.Player),
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
            StoryDirector.Solo("艾丽娅", "我只知道我这支小队不能白组。走吧——今天风停了，这不正常。", StoryPortraits.Xiaomei),
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
            StoryDirector.Solo("你", "第八支小队走到这儿的，只剩我一个。答案和债，今天一起结。", StoryPortraits.Player),
        };
        StoryDirector.Ensure().Play(beats, onDone);
    }

    static void PlayPre8(Action onDone)
    {
        var beats = new List<StoryBeat>
        {
            StoryDirector.Narration("永霜雪境：裂隙最深处，雪一直下。这一趟的终点就在前面。"),
        };
        // 【2026-10-29 叙事体检】第 7 章选 A（先带她回去）后艾丽娅已送回城，不再对她说话；
        // B（一起砸了）/ C（让她坐上去）她仍同行，原台词保留。战前剧情每周目每章只播一次（看过即不重播）。
        var data = SaveSystem.Instance?.Data;
        if (data != null && data.endingChoice == 1)
            beats.Add(StoryDirector.Solo("你", "从三枚铜板到今天。剩下的路，我一个人把它走完。", StoryPortraits.Player));
        else
            beats.Add(StoryDirector.Solo("你", "从三枚铜板到今天。艾丽娅，这笔账，马上就能结清了。", StoryPortraits.Player));
        StoryDirector.Ensure().Play(beats, onDone);
    }

    // ---------- N1｜第 1 章：箱子里的第一张纸条（2026-10-29 主人拍板「剧情是钩子」补最早上钩） ----------
    // 钩子 = 「公会发的箱子，谁会提前写好这种话？」——装箱人（M）悬念的最早一针，
    // 往后接 ch3 路标纸条（C07）、N3「箱子里的纸条，是你塞的」、N5 署名 M 的箱底纸条。
    // 结尾带一个趣味选项（不写存档、不影响走向，只换一句回响）——选项机制的轻量首秀。

    static void PlayN1(Action onDone)
    {
        var beats = new List<StoryBeat>
        {
            StoryDirector.Narration("森林层的雾散了。艾丽娅把今天采的样本码进补给箱，忽然停了手。"),
            StoryDirector.Solo("艾丽娅", "箱子底下压着张纸条，字很新，只有三个字：「别停下。」", StoryPortraits.Xiaomei),
            StoryDirector.Solo("艾丽娅", "公会发的箱子，谁会提前写好这种话？", StoryPortraits.Xiaomei),
            new StoryBeat
            {
                leftName = "你",
                leftPortraitId = StoryPortraits.Player,
                text = "这张纸条，怎么处理？",
                speaker = -1,
                choices = new[]
                {
                    "先收好。回去路上慢慢想。",
                    "万一是催债的呢？那可不能丢。"
                }
            }
        };
        StoryDirector.Ensure().Play(beats, null, idx => ApplyN1Choice(idx, onDone));
    }

    static void ApplyN1Choice(int idx, Action onDone)
    {
        StoryBeat echo = idx == 0
            ? StoryDirector.Solo("艾丽娅", "收好了就走吧。「别停下」——哼，说得倒轻巧。", StoryPortraits.Xiaomei)
            : StoryDirector.Solo("你", "开玩笑的。……不过这张纸，我先收在剑鞘边上。", StoryPortraits.Player);
        StoryDirector.Ensure().Play(new List<StoryBeat> { echo }, () => onDone?.Invoke());
    }

    // ---------- N2｜第 2 章（分支 A · 亡者之径）：这不是第一次 ----------

    static void PlayN2(Action onDone)
    {
        var beats = new List<StoryBeat>
        {
            StoryDirector.Narration("墓园最里侧有一排没有名字的公会墓碑。你数了数，一共七块。").Bg(StoryBackgrounds.GraveSeven),
            StoryDirector.Narration("碑上的日期一块挨一块往下排，间隔都是三年：十七年前、十四年前、十一年前……只有最后一块是三个月前。"),
            StoryDirector.Solo("你", "……七块。", StoryPortraits.Player),
            StoryDirector.Narration("十一年前那块刻了名字：「M. 灰鸦小队 记录员」。再往旁边挪一步，还有第八块——空的。"),
            StoryDirector.Narration("名字的位置留着。石头却是新的。有人提前把碑做好了。"),
            StoryDirector.Solo("你", "他们连名字都不打算写。", StoryPortraits.Player),
            // 【2026-10-29 主人拍板「有些剧情可以加些选项，不主导剧情走向，只增加趣味」】
            new StoryBeat
            {
                leftName = "你",
                leftPortraitId = StoryPortraits.Player,
                text = "这几块碑，怎么办？",
                speaker = -1,
                choices = new[]
                {
                    "记下来，回去写进小队日志。",
                    "别碰。让它这么立着。"
                }
            }
        };
        StoryDirector.Ensure().Play(beats, null, idx => ApplyN2Choice(idx, onDone));
    }

    /// <summary>N2 趣味回响：不写存档、不影响任何走向，只换一句收尾。</summary>
    static void ApplyN2Choice(int idx, Action onDone)
    {
        StoryBeat echo = idx == 0
            ? StoryDirector.Narration("回去你真的写了。第八支小队的日志第一页：碑比人多一。")
            : StoryDirector.Narration("你退开半步。立着的碑，比倒着的诚实。");
        StoryDirector.Ensure().Play(new List<StoryBeat> { echo }, () => onDone?.Invoke());
    }

    // ---------- N3｜第 3 章：遇到梅莉莎（2026-10-23 二次拍板「8岛全通」：恢复周目分流，全通播完整版/未全通播平替版） ----------

    static void PlayN3(Action onDone)
    {
        var beats = new List<StoryBeat>
        {
            StoryDirector.Narration("遗迹深处的藤蔓后面，居然有一处收拾干净的营地。篝火还热着。").Bg(StoryBackgrounds.MelissaCamp),
            StoryDirector.Solo("?", "别动。你踩到我第二道线了。", StoryPortraits.Melissa),
            StoryDirector.Narration("一个头发花白的女人从阴影里出来，手里攥着一支笔，不是刀。"),
            StoryDirector.Solo("?", "我等了三天才等到下一个活人走进来。坐下，先别问，我写字慢。", StoryPortraits.Melissa),
            StoryDirector.Solo("你", "……箱子里的纸条，是你塞的。", StoryPortraits.Player),
            StoryDirector.Solo("梅莉莎", "每一支被挑中的小队，箱子都是我装的。我往每个里面塞一张。", StoryPortraits.Melissa),
            StoryDirector.Solo("梅莉莎", "他们以为那个 M 是领队的名字。", StoryPortraits.Melissa),
            StoryDirector.Solo("梅莉莎", "其实那只是……装箱的人。", StoryPortraits.Melissa),
            StoryDirector.Narration("她把笔按在纸上，很久没有落下去。"),
            StoryDirector.Solo("梅莉莎", "十一年了。我装箱的时候就预感到的那些事，一件都没错过。", StoryPortraits.Melissa),
        };
        StoryDirector.Ensure().Play(beats, () =>
        {
            StoryProgress.AddBond(StoryProgress.NpcMelissa, 20);
            onDone?.Invoke();
        });
    }

    /// <summary>
    /// N3 首周平替版（旁白版）：未 8 岛全通时播（【2026-10-23 二次拍板】恢复分流，本函数回现役）。
    /// 口径：翡翠秘境的战后只留氛围与悬念，不出现梅莉莎、不用 MelissaCamp 背景 / Melissa 立绘、
    /// 不加 StoryProgress.AddBond、不挂 .Bg()；完整版等 8 岛全通后（NG+ 重打第 3 章）才放。
    /// </summary>
    static void PlayN3Alt(Action onDone)
    {
        var beats = new List<StoryBeat>
        {
            StoryDirector.Narration("遗迹深处的藤蔓后面，居然有一处收拾干净的营地。篝火还热着。"),
            StoryDirector.Narration("地上是按年份摞的纸条，最新的一张墨都没干：「别停下。往前走，别回头。」"),
            StoryDirector.Narration("你数了数脚印。至少五个人的，全部朝向更深处。没有一个往回走。"),
        };
        StoryDirector.Ensure().Play(beats, onDone);
    }

    // ---------- N4｜第 4 章：你的名字也在表上（2026-10-23 二次拍板「8岛全通」：恢复周目分流，全通播完整版/未全通播平替版） ----------

    static void PlayN4(Action onDone)
    {
        var beats = new List<StoryBeat>
        {
            StoryDirector.Narration("风车停住了。不是坏了——它的叶片正对着某个位置，像是被谁按下暂停键。").Bg(StoryBackgrounds.Windmill),
            StoryDirector.Narration("齿轮上刻的不是图腾，是刻度。一圈三年。"),
            StoryDirector.Solo("你", "这不是用来稳定裂缝的设备。", StoryPortraits.Player),
            StoryDirector.Narration("轴上还有一列没刻完的名字。最后两个是空的。再往下，有一道新的划痕——"),
            StoryDirector.Solo("你", "……这是三个月前刻的。", StoryPortraits.Player),
            StoryDirector.Narration("你沿着刻度往上数。第八格的位置上，有一个名字你认得：你自己。"),
        };
        StoryDirector.Ensure().Play(beats, onDone);
    }

    /// <summary>
    /// N4 首周平替版：未 8 岛全通时播（【2026-10-23 二次拍板】恢复分流，本函数回现役）。
    /// 口径：晨曦草原的战后不揭「你认得：你自己」，只留风车停住 / 刻度一圈三年 / 名字刻到一半的悬念，
    /// 不挂 .Bg()；完整版等 8 岛全通后（NG+ 重打第 4 章）才放。
    /// </summary>
    static void PlayN4Alt(Action onDone)
    {
        var beats = new List<StoryBeat>
        {
            StoryDirector.Narration("风车停住了。不是坏了——它的叶片正对着某个位置，像是被谁按下暂停键。"),
            StoryDirector.Narration("齿轮上刻的不是图腾，是刻度。一圈三年。"),
            StoryDirector.Solo("你", "这不是用来稳定裂缝的设备。", StoryPortraits.Player),
            StoryDirector.Narration("轴上的名字刻到一半就停了，最后一道划痕很新。你没敢去数那是第几格。"),
        };
        StoryDirector.Ensure().Play(beats, onDone);
    }

    // ---------- N5｜第 5 章（三路汇合）：任务简报 ★第一次真正的冲击 ----------

    static void PlayN5(Action onDone)
    {
        var beats = new List<StoryBeat>
        {
            StoryDirector.Narration("搁浅的补给船歪在礁石里。舱门是被人从里面撬开的。").Bg(StoryBackgrounds.WreckShip),
            StoryDirector.Narration("补给箱码得整整齐齐，箱面编号你能背出来——那是她的队号。"),
            StoryDirector.Solo("你", "她不是去执行普通委托吗。", StoryPortraits.Player),
            StoryDirector.Narration("箱底压着一张纸条，纸是新的。上面只有一个名字：——M"),
            StoryDirector.Narration("你把它抖开，底下是一份泡得发白的简报。字迹能认出来："),
            StoryDirector.Narration("「任务目标：将目标小队引导至最深处，确保其与『沉座』接触。」"),
            StoryDirector.Solo("你", "……这不是调查任务。", StoryPortraits.Player),
            StoryDirector.Narration("你的手抖了一下。你把手按在膝盖上，按了很久才松开。"),
            // 【2026-10-29 主人拍板「选项只加趣味不主导」】会长扣好感照旧无条件结，回响二选一
            new StoryBeat
            {
                leftName = "你",
                leftPortraitId = StoryPortraits.Player,
                text = "这份简报，怎么处理？",
                speaker = -1,
                choices = new[]
                {
                    "收好。这是证据。",
                    "现在就烧了它。"
                }
            }
        };
        StoryDirector.Ensure().Play(beats, null, idx => ApplyN5Choice(idx, onDone));
    }

    /// <summary>N5 趣味回响：会长好感 -10 与选项无关照旧结；回响不影响走向。</summary>
    static void ApplyN5Choice(int idx, Action onDone)
    {
        StoryProgress.AddBond(StoryProgress.NpcMaster, -10);
        StoryBeat echo = idx == 0
            ? StoryDirector.Solo("你", "纸会烂，账不会。这张我贴身收着。", StoryPortraits.Player)
            : StoryDirector.Narration("火苗舔到「沉座」两个字的时候，你又把它按灭了。烧不掉的。");
        StoryDirector.Ensure().Play(new List<StoryBeat> { echo }, () => onDone?.Invoke());
    }

    // ---------- N6｜第 6 章：沉座 + 不再救援 ----------

    static void PlayN6(Action onDone)
    {
        var beats = new List<StoryBeat>
        {
            StoryDirector.Narration("洞穴正中悬着一枚黑曜结构，有节奏地搏动，像在替什么人喘气。").Bg(StoryBackgrounds.ThroneSeat),
            StoryDirector.Narration("周围散落着更旧的战斗痕迹。十四年前的盾。十一年前那批的箭。"),
            StoryDirector.Solo("你", "沉座。是要有人坐上去的东西。", StoryPortraits.Player),
            StoryDirector.Narration("你在十一年前的那堆残骸里翻到半张烧过的纸。只剩两行字："),
            StoryDirector.Narration("「若目标小队成功削弱核心，派遣第二梯队收尾。若失败，则封锁入口，不再派遣救援。」"),
            StoryDirector.Solo("你", "他们不是没派人救。他们压根就没打算派。", StoryPortraits.Player),
            // 【2026-10-29 主人拍板「选项只加趣味不主导」】
            new StoryBeat
            {
                leftName = "你",
                leftPortraitId = StoryPortraits.Player,
                text = "看完这半张纸，你——",
                speaker = -1,
                choices = new[]
                {
                    "冷笑一声。",
                    "把纸叠好，收进胸口。"
                }
            }
        };
        StoryDirector.Ensure().Play(beats, null, idx => ApplyN6Choice(idx, onDone));
    }

    /// <summary>N6 趣味回响：不写存档、不影响走向。</summary>
    static void ApplyN6Choice(int idx, Action onDone)
    {
        StoryBeat echo = idx == 0
            ? StoryDirector.Narration("你笑了半声，剩下半声咽了回去——洞里回音太大，笑给谁听。")
            : StoryDirector.Narration("纸条贴着心口。往下的每一步，它都比剑沉。");
        StoryDirector.Ensure().Play(new List<StoryBeat> { echo }, () => onDone?.Invoke());
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
            StoryDirector.Narration("黑雾从她身上崩散。她软下去，你把她接住。").Bg(StoryBackgrounds.AbyssChoice),
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
            // 通关第 8 章：本周目支线派定归零；下一周目重通第 1 章后由 EnsureForkRoll 重新随机派定（2026-10-21 拍板）
            // 【2026-10-23 两条制】两次派定记录一并清（与 SaveData.MarkChapterCleared 同口径）
            if (data != null)
            {
                data.chosenBranch = 0;
                data.branchRoll1 = 0;
                data.branchRoll2 = 0;
            }
            SaveSystem.Instance?.Save();
            // 【2026-10-23 主人拍板「最后一关后剧情二选一」】结局演出之后接终章余音：
            // 第一次打完主线 → 出「回家 / 追查真相」二选一（选过就不再播，幂等）
            TryPlayFinaleChoice(onDone);
        });
    }

    // ---------- 终章追查线（2026-10-23 主人拍板「最后一关后剧情二选一，选完再出几关进完整剧情」） ----------
    // 流程：N8 结局演完 → PlayFinaleChoice（二选一，只播一次）→ 选「追查」→ 冒险页出现 2 个追查关
    //       （FinaleBattleMode）→ 第 1 关打完播 PlayFinaleMid（揭底中段）→ 第 2 关打完播 PlayFinaleFinale（完整揭底）。
    // 文案为草拟稿（调性：有趣 / 结果先于隐喻），主人可全改；名字 ≤7 汉字。

    /// <summary>终章余音二选一：只在第一次打完主线时播（postFinalChoice==0），选过永不重播。</summary>
    static void TryPlayFinaleChoice(Action onDone)
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null || data.postFinalChoice != 0)
        {
            onDone?.Invoke();
            return;
        }

        var beats = new List<StoryBeat>
        {
            // 【2026-10-31 修】原来固定用 EndHappy 背景 +「庆功宴摆了三天」，但选 C 的《留下的是她》
            // 里艾丽娅已经坐进沉座，不可能有庆功宴。改成中性场景（公会大厅、长桌没撤），任何结局都接得住。
            StoryDirector.Narration("回城后的第三个夜里，公会大厅的长桌还没撤。你弯腰捡东西时，在桌底摸到一张没写完的领用单。").Bg(StoryBackgrounds.GuildHall),
            StoryDirector.Narration("装填人一栏，签着一个花体的 M。日期从十一年前排到三个月前——中间没断过。"),
            StoryDirector.Solo("你", "……十一年。每一支进裂隙的队伍，箱子都是同一个人装的？", StoryPortraits.Player),
            StoryDirector.Solo("你", "这账要是真的，公会大门上那块『英魂长存』的匾，怕是要换个挂法。", StoryPortraits.Player),
            new StoryBeat
            {
                leftName = "你",
                leftPortraitId = StoryPortraits.Player,
                text = "这张单子，怎么处理？",
                speaker = -1,
                choices = new[]
                {
                    "算了，先回家补觉。明天的事交给明天的我。",   // 回家：本周目到此收束
                    "不行。这事我现在就去装备科问个明白。"         // 追查：解锁 2 个追查关
                }
            }
        };

        StoryDirector.Ensure().Play(beats, null, choice => ApplyFinaleChoice(choice, onDone));
    }

    /// <summary>分支选择落账 + 即时回响（必须有回响，否则选择感会消失——照第 7 章口径）。</summary>
    static void ApplyFinaleChoice(int index, Action onDone)
    {
        var data = SaveSystem.Instance?.Data;
        if (data != null) data.MarkFinaleChoice(index + 1);   // 1=回家 2=追查

        StoryBeat echo;
        if (index == 0)
        {
            echo = StoryDirector.Narration("你把领用单原样压回桌底。有些账，还是别算得太清楚。");
        }
        else
        {
            echo = StoryDirector.Narration("装备科的门没锁，里面亮着一盏灯。冒险页上，那盏灯也为你亮了起来。");
        }

        StoryDirector.Ensure().Play(new List<StoryBeat> { echo }, () =>
        {
            SaveSystem.Instance?.Save();
            onDone?.Invoke();
        });
    }

    /// <summary>追查第 1 关打完：揭底中段（FinaleBattleMode 结算链调，战斗场景内播）。</summary>
    public static void PlayFinaleMid(Action onDone)
    {
        var beats = new List<StoryBeat>
        {
            StoryDirector.Narration("装备科的库房比想象里深。最里面的柜子上了三把锁，锁上贴着同一张封条：内部流转，勿问。"),
            StoryDirector.Narration("柜子里没有钱，也没有装备——只有一摞码得整整齐齐的空纸箱。每个箱面写着一支队伍的编号。"),
            StoryDirector.Solo("你", "第九支……领用单上写的就是他们。进裂隙前一天，箱子被人单独领走了。", StoryPortraits.Player),
            StoryDirector.Narration("箱底压着一张字条，墨迹很新：「想知道答案，就去它们回来的地方。」"),
        };
        // 【2026-10-24 主人拍板「黑幕可以加地点或需要玩家知道的字幕」】本段没有背景图，黑幕字幕直接点名地点
        StoryDirector.Ensure().Play(beats, onDone, null, false, "装备科·库房");
    }

    /// <summary>
    /// 追查第 2 关打完：完整揭底演出（本设计线的终点）。播完置 finaleStoryPlayed 防重播（幂等：已播过直接放行）。
    /// </summary>
    public static void PlayFinaleFinale(Action onDone)
    {
        var data = SaveSystem.Instance?.Data;
        if (data != null && data.finaleStoryPlayed)
        {
            onDone?.Invoke();
            return;
        }

        var beats = new List<StoryBeat>
        {
            StoryDirector.Narration("箱子堆的最深处，一个头发花白的女人正就着灯装箱。手法快得完全不像这个年纪。"),
            StoryDirector.Solo("梅莉莎", "手别伸过来。箱子很沉，你抱不动。", StoryPortraits.Melissa),
            // 【2026-10-31 修】原「两百多支队伍」与碑上七支、每三年一次的量级差两个数量级，改成不报数
            StoryDirector.Solo("你", "M。装备科记录员。十一年，每一支进裂隙的队伍的箱子，全是你装的。", StoryPortraits.Player),
            StoryDirector.Solo("梅莉莎", "装了十一年，就赌一件事——总有一支队伍，能把我预感的那些事，一件不差全应验。", StoryPortraits.Melissa),
            StoryDirector.Solo("梅莉莎", "今天，应验了。", StoryPortraits.Melissa),
            StoryDirector.Narration("她把最后一个箱子推到你面前。箱面写着：给追到这儿的人。"),
            StoryDirector.Solo("梅莉莎", "回去吧。以后进裂隙的箱子，换你来装。", StoryPortraits.Melissa),
            StoryDirector.Narration("——《像素冒险：裂隙之刃》 装了十一年箱子的人，今天把钥匙交了出来。"),
        };

        // 【2026-10-24 主人拍板】同 PlayFinaleMid：无背景图，黑幕字幕点名地点
        StoryDirector.Ensure().Play(beats, () =>
        {
            if (data != null)
            {
                data.finaleStoryPlayed = true;
                SaveSystem.Instance?.Save();
            }
            onDone?.Invoke();
        }, null, false, "装备科·库房");
    }

    static void BuildHappy(List<StoryBeat> b)
    {
        b.Add(StoryDirector.Narration("黑曜结构在你面前碎成八瓣。第八块空碑上的名字，终于没人刻得上去了。").Bg(StoryBackgrounds.EndHappy));
        b.Add(StoryDirector.Solo("艾丽娅", "结束了。对我们来说，结束了。", StoryPortraits.Xiaomei));
        b.Add(StoryDirector.Narration("你们拼命往回跑。跑出口的时候天亮得刺眼。"));
        b.Add(StoryDirector.Solo("艾丽娅", "……三枚铜板。我还没还。", StoryPortraits.Xiaomei));
        b.Add(StoryDirector.Solo("你", "不急。你欠账的时间比我长多了。", StoryPortraits.Player));
        b.Add(StoryDirector.Solo("艾丽娅", "那我用以后还。够不够长？", StoryPortraits.Xiaomei));
        b.Add(StoryDirector.Narration("——《像素冒险：裂隙之刃》 追上她的人，终于赶上了。"));
        // 收尾钩子：裂缝意志不出立绘，改成纯黑屏 + 一句话（定案 2026-09-21）
        b.Add(StoryDirector.BlackLine("……你们以为砸了它，就结束了？"));
    }

    static void BuildBadHome(List<StoryBeat> b)
    {
        b.Add(StoryDirector.Narration("你先把她背了回去。剩下的那段路，你一个人又走了一遍。").Bg(StoryBackgrounds.EndBadHome));
        b.Add(StoryDirector.Narration("她确实活下来了。医官说恢复得不错。"));
        b.Add(StoryDirector.Narration("只是从那以后，她每隔几天就会忘记一件事。先是裂隙里的名字，再是阿尔托的全名。"));
        b.Add(StoryDirector.Solo("艾丽娅", "……今天我是不是又问过你一遍，我欠你多少钱？", StoryPortraits.Xiaomei));
        b.Add(StoryDirector.Solo("你", "三枚铜板。你每天都问，我每天都答。", StoryPortraits.Player));
        b.Add(StoryDirector.Solo("艾丽娅", "那你别嫌烦。", StoryPortraits.Xiaomei));
        b.Add(StoryDirector.Narration("她回来了一半。剩下那一半，留在了最深处的雪里。"));
        b.Add(StoryDirector.Narration("——《像素冒险：裂隙之刃》 你救回来了她，但没救回来全部。"));
    }

    static void BuildBadGate(List<StoryBeat> b)
    {
        b.Add(StoryDirector.Narration("折返到巨岩深窟的洞窟时，她坐了上去。黑曜合拢的时候，她回头看了你最后一眼。").Bg(StoryBackgrounds.EndBadGate));
        b.Add(StoryDirector.Narration("裂隙真的止住了。王国今年没有新的裂缝记录。公会照常发任务、照常清点人数、照常涨价。"));
        b.Add(StoryDirector.Solo("会长", "牺牲少数保全多数——这就是结算方式。", StoryPortraits.GuildMaster));
        b.Add(StoryDirector.Solo("你", "她的名字，写进去吗。", StoryPortraits.Player));
        b.Add(StoryDirector.Narration("会长没有回答。你也知道答案。"));
        b.Add(StoryDirector.Narration("每年这个时候，你都会去那道裂缝口站着，等一件不会发生的事。"));
        b.Add(StoryDirector.Narration("——《像素冒险：裂隙之刃》 世界保住了。这笔账，只有你一个人记得。"));
    }

    static void BuildTrue(List<StoryBeat> b)
    {
        b.Add(StoryDirector.Narration("沉座崩解的最后，你看到一个不属于这里的东西卡在晶格里——一张叠了十一年的纸条。").Bg(StoryBackgrounds.EndTrue));
        b.Add(StoryDirector.Solo("梅莉莎", "那是我塞的。第九支队伍的箱子。", StoryPortraits.Melissa));
        b.Add(StoryDirector.Narration("她从崩落的那头走过来，径直走到裂隙的中心，走得比你还稳。"));
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

    /// <summary>
    /// 开章引言：每章一句，轻剧情重氛围（V1 补录，见 Docs/正式文案_V1）。
    /// 与 OpeningLine 分工：OpeningLine 是「世界观 + 氛围」两行正文；本章引言是单句钩子。
    /// 落点：章节过场（ChapterSplash）。当前 UI 调用方只消费 IntroTitle / OpeningLine，
    /// 要显示本章引言，在 ChapterSplash 里追加一行 ChapterQuote(chapter) 即可（未改动 UI）。
    /// </summary>
    public static string ChapterQuote(int chapter)
    {
        switch (chapter)
        {
            case 1: return "「林子里的光是斜的，照不到地上。」";
            case 2: return "「碑上的名字，大多只剩一半。」";
            case 3: return "「藤蔓比路还老。」";
            case 4: return "「风是从更远的地方吹来的。」";
            case 5: return "「潮水每天退一次，退一次就露出一点旧东西。」";
            case 6: return "「越往下走，石壁上的字越多。」";
            case 7: return "「这里的火不烧木头。」";
            case 8: return "「雪落下来，就不化了。」";
            default: return null;
        }
    }
}
