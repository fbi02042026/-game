using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss 线剧情（2026-11-11 主人拍板「章节线要加上 boss 线，用 boss 线去丰富剧情」）。
/// 与章节线 ChapterStoryBeats.TryPlayPreBattle 并列：章节线只在每章第一关播一次，
/// boss 线在每个主线正式关 Boss 关进场时播（两句引子 + 三选一），补上 Boss 关没有剧情的空缺。
///
/// 名字口径：文案一律用图鉴外号（v2 活梗名，见 Docs/怪物命名与词缀对照_v3_2026-11-10.md §二），
/// 正式名留给任务目标 / 成就 / 图鉴。每章两只首领都可能出场（WavePlanner.Picks.cs:250 随机取），
/// 所以文案同时点出本章两位，不写死某一只，避免说错对象。
/// 引子点出外号 → 引导语再收一次 → 选完的 toast 落账，埋了就收。
/// </summary>
public static class BossStoryBeats
{
    struct Line
    {
        public string Narration;
        public string YouLine;
        public string Lead;
    }

    /// <summary>下标 = chapter-1。外号与 AdventureLogCatalog 的 Nickname 逐字一致。</summary>
    static readonly Line[] Lines =
    {
        new Line
        {
            Narration = "林子尽头有两位守门的：江湖人称「花间祭」的，和那位谁也劝不动的「老林头」。",
            YouLine   = "三枚铜板都还没还，先别急着把我算进账里。",
            Lead      = "花间祭的孢粉、老林头的根，都在前面了。凝一条战意，只在这一战里生效——"
        },
        new Line
        {
            Narration = "墓园最里那张桌子后头坐着「判官大人」，门前还有个「收魂人」替他点名。名册翻到第八支小队那页，墨还没干。",
            YouLine   = "点名就点名。今天我不签到。",
            Lead      = "收魂人先喊号，判官大人再落笔。凝一条战意，只在这一战里生效——"
        },
        new Line
        {
            Narration = "秘境最亮的那朵叫「小太阳」，最不肯露面的那个叫「尖叫怪」——你总是先听见它，才看见它。",
            YouLine   = "先出声的那个，一般也先动手。",
            Lead      = "小太阳晃眼，尖叫怪先声夺人。凝一条战意，只在这一战里生效——"
        },
        new Line
        {
            Narration = "风车塔下「劳达师傅」还在搬石头，塔里那位「起床气」刚被吵醒。整片草原的风，就是这么停的。",
            YouLine   = "风都停了还上班，比我敬业。",
            Lead      = "劳达师傅扛得住，起床气压过来。凝一条战意，只在这一战里生效——"
        },
        new Line
        {
            Narration = "沉船外头「掷戟手」守着舱门，舱底坐着「深海大佬」。那艘补给船看着最多三个月，他们却像等了很久。",
            YouLine   = "船是新的，等的人却不像刚来。",
            Lead      = "掷戟手在明，深海大佬在暗。凝一条战意，只在这一战里生效——"
        },
        new Line
        {
            Narration = "深窟里「骨科大夫」替人拆过太多骨头，「魔界无人机」在头顶一层层记。石壁上那句『如果你读到这——』，大概就是没让谁写完。",
            YouLine   = "那就由我把它读完。",
            Lead      = "骨科大夫在近处，魔界无人机在头顶。凝一条战意，只在这一战里生效——"
        },
        new Line
        {
            Narration = "炼狱核心「喊口号的」把话说得震天响，真正拍板的是后面那位「异种老板」。走到这儿的第八支小队，只剩我一个。",
            YouLine   = "喊口号的废话多，那就先让他闭嘴。",
            Lead      = "喊口号的先上，异种老板在后头。凝一条战意，只在这一战里生效——"
        },
        new Line
        {
            Narration = "裂隙收成一条白线之前，最后两位守门的：「冰霸王」踩得整片冰层都在响，「雪山守门员」一句不问，只站着。",
            YouLine   = "最后一程了，别让守门的等太久。",
            Lead      = "冰霸王压过来，雪山守门员不让路。凝一条战意，只在这一战里生效——"
        },
    };

    static Line Get(int chapter)
    {
        int idx = chapter - 1;
        if (idx < 0 || idx >= Lines.Length)
        {
            Debug.LogError("[BossStoryBeats] 未登记的章节 boss 线: " + chapter);
            return Lines[0];   // 占位：认不出就报错，不静默跳过
        }
        return Lines[idx];
    }

    /// <summary>把本章 boss 线两句引子追加到播放链（三选一之前）。</summary>
    public static void AppendIntro(List<StoryBeat> beats, int chapter)
    {
        var l = Get(chapter);
        beats.Add(StoryDirector.Narration(l.Narration));
        // 与三选一那个 beat 同构（speaker=-1 + 左侧玩家），别用 Solo：
        // Solo 会把立绘居中，接在左侧玩家框后面会跳一下；整段保持同一个框。
        beats.Add(new StoryBeat
        {
            leftName = "你",
            leftPortraitId = StoryPortraits.Player,
            speaker = -1,
            text = l.YouLine
        });
    }

    /// <summary>三选一引导语：按章收口外号，接住上面两句引子。</summary>
    public static string GetBlessingLead(int chapter) => Get(chapter).Lead;
}
