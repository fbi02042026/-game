using System;
using System.Collections.Generic;

/// <summary>
/// 支线岛剧情（8 岛，2026-10-31 落码）。
/// 数据来源：Docs/《支线岛剧情文案与背景提示词》_2026-10-24.md。
///
/// 结构（每岛固定）：黑幕字幕（岛名 · 副标）→ 旁白 1 句 → 独眼字条 1 句 → 你 1 句；战后你收 1 句。
/// 叙事声音统一为「独眼的字条」：他在每个岛入口贴一张，回酒馆能找他追问——
/// 酒馆日常和支线岛就此串成一条线（打岛 → 回酒馆找独眼对答案 → 请酒拿线索）。
/// 不绑艾丽娅 / 具体队友（岛与主线并行推进，任何进度打都不穿帮）。
///
/// 岛名真源 = SideIslandDefs（本文件的副标只在这里用，不进 SideIslandDefs）。
/// 背景真源 = StoryBackgrounds.Island01..08（缺图自动占位降级，不拦流程）。
/// </summary>
public static class IslandStoryBeats
{
    struct IslandText
    {
        public string Caption;      // 黑幕大字：岛名 · 副标
        public string Narration;    // 旁白
        public string Note;         // 独眼字条
        public string YouLine;      // 你
        public string AfterLine;    // 战后
    }

    // 顺序 = 岛 id 1..8（与 SideIslandDefs 对齐）
    static readonly IslandText[] Texts =
    {
        new IslandText
        {
            Caption   = "迷雾屿 · 雾有面包味",
            Narration = "岛的雾不散。凑近了闻，是烤面包的味道。",
            Note      = "「面包味是诱饵，别吃岛上的任何东西。——独眼（附：打听费记账上）」",
            YouLine   = "……用面包味钓冒险者，这岛比公会诚实。",
            AfterLine = "雾散了。面包味还在——就当是它认输了。",
        },
        new IslandText
        {
            Caption   = "沉船湾 · 潮水退了",
            Narration = "湾里横七竖八搁着十几条船，桅杆全朝着同一个方向倒。",
            Note      = "「桅杆倒向哪，怪就在哪。这句值五十金，先赊着。——独眼」",
            YouLine   = "你这条消息是从遇难者嘴里问出来的吧。",
            AfterLine = "船是空的，货还在。这一趟，公会的账本上不会有一分钱。",
        },
        new IslandText
        {
            Caption   = "藤蔓屿 · 藤蔓比路老",
            Narration = "藤蔓在岛上织了一座温室，里面的花全朝着地底开。",
            Note      = "「别碰花。上一个小队带了三朵回去，公会说他们是『主动留队』。——独眼」",
            YouLine   = "留队。这词他们用得真顺手。",
            AfterLine = "花全蔫了。原来它们也是被人按在这里的。",
        },
        new IslandText
        {
            Caption   = "伏草原 · 风停了",
            Narration = "草原上的草伏得整整齐齐，像被谁一寸寸压平的。",
            Note      = "「草伏的方向就是安全路线。倒着走。——独眼（这条免费，因为我也不知道为什么）」",
            YouLine   = "免费的消息最贵，上次免费那条差点把我留在岛上。",
            AfterLine = "风又吹起来了。它等这个等的，恐怕比我等转正还久。",
        },
        new IslandText
        {
            Caption   = "搁浅地 · 箱子堆成山",
            Narration = "补给箱从滩头一直码到山腰，编号被人用刀一个个划掉了。",
            Note      = "「箱子里没有补给。一个开着的都别开。要问为什么，请我喝酒。——独眼」",
            YouLine   = "又是你。怎么哪儿都有你的字条。",
            AfterLine = "划掉编号的人想让人忘掉这些箱子。偏不。",
        },
        new IslandText
        {
            Caption   = "王座间 · 小一号的沉座",
            Narration = "洞厅正中蹲着一枚黑曜结晶，只有半人高——像大深处那个东西的胚。",
            Note      = "「它在喘。别管它喘的是什么，砸就是了。——独眼（这条我写得手都在抖）」",
            YouLine   = "知道害怕还把字条贴这么整齐，你这家伙到底来过多少个岛。",
            AfterLine = "不喘了。深处那个大的，今天应该打了个喷嚏。",
        },
        new IslandText
        {
            Caption   = "深渊口 · 风往外吹",
            Narration = "岛心裂着一道口子，往外呼呼地吹热风——和裂隙里吹的正好相反。",
            Note      = "「这口子在往外吐东西。吐出来的年代，比我爷爷还老。——独眼」",
            YouLine   = "吐出来的东西再老，今天也得服新规矩。",
            AfterLine = "口子安静了。老东西，回去接着睡吧。",
        },
        new IslandText
        {
            Caption   = "黑曜祭坛 · 摆得整整齐齐",
            Narration = "黑曜碎片在岛顶拼成一座祭坛，每块碎片都打磨过——不是天然断口。",
            Note      = "「这是八座岛里唯一有人常来打扫的一座。是谁我不说，说了你就白打了。——独眼」",
            YouLine   = "……行。打完我自己去问。",
            AfterLine = "祭坛塌了。那个常来打扫的人，迟早也会知道我的名字。",
        },
    };

    static bool TryGet(int islandId, out IslandText t)
    {
        t = default(IslandText);
        if (islandId < 1 || islandId > Texts.Length) return false;
        t = Texts[islandId - 1];
        return true;
    }

    static string BgId(int islandId)
    {
        switch (islandId)
        {
            case 1: return StoryBackgrounds.Island01;
            case 2: return StoryBackgrounds.Island02;
            case 3: return StoryBackgrounds.Island03;
            case 4: return StoryBackgrounds.Island04;
            case 5: return StoryBackgrounds.Island05;
            case 6: return StoryBackgrounds.Island06;
            case 7: return StoryBackgrounds.Island07;
            case 8: return StoryBackgrounds.Island08;
            default: return null;
        }
    }

    /// <summary>
    /// 进岛前把本次要用的背景/立绘读进缓存（几张图，毫秒级）：播到一半才首次加载会卡一下。
    /// 缺图时 StoryBackgrounds / StoryPortraits 自带占位降级 + 告警，不会中断流程。
    /// </summary>
    public static void Warmup(int islandId)
    {
        if (!TryGet(islandId, out _)) return;
        StoryBackgrounds.Get(BgId(islandId));
        StoryPortraits.Warmup(StoryPortraits.Player);
    }

    /// <summary>
    /// 进岛后、首波怪前的开场：黑幕字幕（岛名·副标）+ 岛背景 + 旁白/字条/你三句。
    /// 返回 false = 岛 id 非法，调用方直接开打（不拦流程）。
    /// </summary>
    public static bool TryPlayPreBattle(int islandId, Action onDone)
    {
        if (!TryGet(islandId, out var t)) return false;

        var beats = new List<StoryBeat>
        {
            StoryDirector.Narration(t.Narration).Bg(BgId(islandId)),
            StoryDirector.Narration(t.Note),
            StoryDirector.Solo("你", t.YouLine, StoryPortraits.Player),
        };
        // 黑幕字幕优先用 Caption（岛名·副标），而不是背景 DisplayName —— 见 StoryDirector 首拍换背景分支
        StoryDirector.Ensure().Play(beats, onDone, null, false, t.Caption);
        return true;
    }

    /// <summary>
    /// 打完岛、结算奖励前的一句收尾。首拍挂 skipLocationReveal：同景续话，不再弹一次岛名黑幕。
    /// 返回 false = 岛 id 非法或没有战后文案，调用方直接走奖励链。
    /// </summary>
    public static bool TryPlayPostClear(int islandId, Action onDone)
    {
        if (!TryGet(islandId, out var t)) return false;
        if (string.IsNullOrEmpty(t.AfterLine)) return false;

        var beat = StoryDirector.Solo("你", t.AfterLine, StoryPortraits.Player);
        beat.skipLocationReveal = true;   // 还在战斗场景、背景没换：别再播一次地点黑幕
        StoryDirector.Ensure().Play(new List<StoryBeat> { beat }, onDone);
        return true;
    }
}
