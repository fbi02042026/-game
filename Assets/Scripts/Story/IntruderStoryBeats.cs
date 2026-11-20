using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 乱入·救场剧情（2026-11-20 批次2 T3 + 批次4 T5/T6）：台词真源只此一处 C# 常量，不进 csv / prefab；
/// 只挂波次节点 / 受伤节点，不主动弹。
/// 批次2 提供 TryPlayIntruder（敌对乱入）；批次4 追加 TryPlayRescue（友军救场，三段）/
/// TryPlayEntry（入场救援·被围攻）/ TryPlayRescueOutcome（入场救援谢幕·救援成功口径）。
/// 立绘：救场小队直接用花名册 HireId 取佣兵立绘；批次6 起敌对反派三人组用主人供图立绘
///（StoryPortraits.BanditBoss/Witch/Blade）；命名 ≤7 汉字。
/// 结构统一：Narration 起手（只首句 .Bg()）→ Solo 对白 → 救场用 BlackLine 收最后一句
///（黑幕由 StoryDirector.Finish 内 ClearEndBlackLine 自动清）。
/// 暗线口径：委托链 = 艾丽娅 → 赛琳 → 小队；第 7 章前只说「有人托了我们」，不点破艾丽娅名字。
/// </summary>
public static class IntruderStoryBeats
{
    /// <summary>敌对乱入剧情入口。返回 true = 已交给 StoryDirector 播放；播放失败已回调 onDone。</summary>
    public static bool TryPlayIntruder(int chapter, string storyKey, Action onDone)
    {
        var beats = chapter == 3 ? BuildCh3Beats() : BuildGenericBeats(chapter);
        StoryPortraits.Warmup(StoryPortraits.Player, BanditBoss, BanditWitch, BanditBlade);
        Debug.Log($"[IntruderStory] 敌对乱入剧情 chapter={chapter} storyKey={storyKey} beats={beats.Count}");

        var dir = StoryDirector.Ensure();
        if (dir == null)
        {
            Debug.LogError("[IntruderStory] StoryDirector 取不到，敌对乱入剧情跳过（直接放行本波）");
            onDone?.Invoke();
            return false;
        }
        dir.Play(beats, onDone);
        return true;
    }

    /// <summary>第 3 章 · 翡翠秘境：路标是新的，石门上的旧字一闪一闪；强盗三人组拦路（批次6 三人对白版）。</summary>
    static List<StoryBeat> BuildCh3Beats()
    {
        return new List<StoryBeat>
        {
            StoryDirector.Narration("藤蔓后面闪出三个人影，一身冒险者行头，却一个比一个不像活人。"),
            StoryDirector.Solo("夜薇", "这条岔路归我们管了。掉头，或者躺下——选一个。", BanditWitch),
            StoryDirector.Solo("阿刃", "薇姐别废话，我手痒三天了，就等贴脸开大这波。", BanditBlade),
            StoryDirector.Solo("独眼老雷", "急什么。来，先把牌子看仔细——正面：此路可通；背面：进去的，没一个回来过。", BanditBoss),
            StoryDirector.Solo("你", "我勒个豆？！谁家路标还搞双面的啊！", StoryPortraits.Player),
            StoryDirector.Narration("你接过牌子翻来覆去看了个遍——两行字都是新刻的，刀口还没磨平。"),
            StoryDirector.Solo("你", "行吧，那我就当这个「第一个回来的」。", StoryPortraits.Player),
        };
    }

    /// <summary>其它章节的通用版（批次6 三人对白版，不含 ch3 专属意象）；后续按章节细化时替换本函数。</summary>
    static List<StoryBeat> BuildGenericBeats(int chapter)
    {
        return new List<StoryBeat>
        {
            StoryDirector.Narration($"第 {chapter} 章的路口，三个人影一字排开，看架势是专门来拦路的。"),
            StoryDirector.Solo("独眼老雷", "前面不让你过。不服气，就试试。", BanditBoss),
            StoryDirector.Solo("阿刃", "雷哥别跟他磨，我手痒了——我们三个，他一个，账很好算。", BanditBlade),
            StoryDirector.Solo("夜薇", "吵完了？先礼后兵：留下买路财，或者留下命。", BanditWitch),
            StoryDirector.Solo("你", "我勒个豆？！好家伙，路上还带随机刷新的？", StoryPortraits.Player),
            StoryDirector.Solo("你", "得，命我留着呢。财嘛——你们自己上来拿。", StoryPortraits.Player),
        };
    }

    // ============================================================
    // 批次4（2026-11-20 T5/T6）：友军救场 + 入场救援
    // 固定小队 SQ_MAIN：加百列 H023（剑盾）/ 艾拉 H015（游侠）/ 塞拉 H012（水系法）。
    // 立绘直接用花名册 HireId（MercPortraitSprites 通道），不新造资源。
    // ============================================================

    /// <summary>救场小队立绘 id（花名册 HireId，剧情窗与战斗单位同一套真源）。</summary>
    const string SquadGabriel = "H023";   // 加百列 · 剑盾
    const string SquadElla = "H015";      // 艾拉 · 游侠
    const string SquadSera = "H012";      // 塞拉 · 水系法

    // ============================================================
    // 批次6（2026-11-20）：敌对乱入固定反派三人组（主人供立绘三张并授权改名）。
    // 剧情立绘走 MercStand 通道（文件名即 id）；战斗单位同一套人设走职业模板
    //（IntruderPicker 固定返回 zhongzhan201 / fashi202 / kuangzhan201，均为非 101 玩家脸），
    // 避免「剧情张三、战斗李四」对不上。三人关系：老雷领队 / 夜薇算账 / 阿刃莽。
    // ============================================================
    /// <summary>独眼老雷 · 重武大哥（立绘 bandit_boss / 战斗 zhongzhan201）。</summary>
    const string BanditBoss = "bandit_boss";
    /// <summary>夜薇 · 女法师（立绘 bandit_witch / 战斗 fashi202）。</summary>
    const string BanditWitch = "bandit_witch";
    /// <summary>阿刃 · 狂战小弟（立绘 bandit_blade / 战斗 kuangzhan201）。</summary>
    const string BanditBlade = "bandit_blade";

    /// <summary>
    /// T5 友军救场剧情入口。按 chapter / islandId 取口吻：
    /// islandId 非空 → 支线岛版；chapter ≥ 7 → 揭底版；其余（含第 1 章首遇）→ 首遇版。
    /// 返回 true = 已交给 StoryDirector 播放；播放失败已回调 onDone（调用方照常放行友军入场）。
    /// </summary>
    public static bool TryPlayRescue(int chapter, string islandId, Action onDone)
    {
        List<StoryBeat> beats;
        if (!string.IsNullOrEmpty(islandId))
        {
            int.TryParse(islandId, out int island);
            beats = BuildRescueIslandBeats(island);
        }
        else if (chapter >= 7)
            beats = BuildRescueRevealBeats(chapter);
        else
            beats = BuildRescueFirstMeetBeats(chapter);

        Debug.Log($"[IntruderStory] 友军救场剧情 chapter={chapter} island={islandId ?? "-"} beats={beats.Count}");
        return PlayRescueBeats(beats, onDone);
    }

    /// <summary>
    /// T6 入场救援剧情入口：进 Boss 关撞见小队被围攻、剩一口血。播完由调用方生成低血友军。
    /// storyKey 仅作日志（台词按 chapter 分派：ch5 沉船湾版 / ch7 版 / 其它章通用版）。
    /// </summary>
    public static bool TryPlayEntry(int chapter, string storyKey, Action onDone)
    {
        List<StoryBeat> beats;
        if (chapter == 7)
            beats = BuildEntryCh7Beats();
        else if (chapter == 5)
            beats = BuildEntryCh5Beats();
        else
            beats = BuildEntryGenericBeats(chapter);

        Debug.Log($"[IntruderStory] 入场救援剧情 chapter={chapter} storyKey={storyKey} beats={beats.Count}");
        return PlayRescueBeats(beats, onDone);
    }

    /// <summary>
    /// T6 谢幕段（2026-11-20 三次拍板）：只要我方赢（波末通关检测点必然已赢）就是救援成功——
    /// 小队三人一律「受伤、没有死亡」，剧情不出现死亡暗示。旧「全灭撤退版」已删。
    /// 只播剧情不拦结算；播点 = 波末撤场前（调用方保证）。
    /// </summary>
    public static bool TryPlayRescueOutcome(Action onDone)
    {
        var beats = BuildOutcomeSuccessBeats();
        Debug.Log($"[IntruderStory] 入场救援谢幕剧情（救援成功口径） beats={beats.Count}");
        return PlayRescueBeats(beats, onDone);
    }

    /// <summary>救场类剧情公共播放口：预热小队立绘 → StoryDirector 播放；取不到导演时 LogError 后放行。</summary>
    static bool PlayRescueBeats(List<StoryBeat> beats, Action onDone)
    {
        StoryPortraits.Warmup(StoryPortraits.Player, SquadGabriel, SquadElla, SquadSera);
        var dir = StoryDirector.Ensure();
        if (dir == null)
        {
            Debug.LogError("[IntruderStory] StoryDirector 取不到，救场类剧情跳过（直接放行后续流程）");
            onDone?.Invoke();
            return false;
        }
        dir.Play(beats, onDone);
        return true;
    }

    // ---------- 救场三段 ----------

    /// <summary>救场·首遇版（第 1 章拍板口径；2~6 章若未来配表也走本段，措辞不绑第 1 章专属名词）。</summary>
    static List<StoryBeat> BuildRescueFirstMeetBeats(int chapter)
    {
        var beats = new List<StoryBeat>
        {
            StoryDirector.Narration("怪堆侧翼杀出三个人影——盾、弓、法杖，配合得像排练过一百遍。"),
        };
        if (chapter == 1)
            beats[0] = beats[0].Bg(StoryBackgrounds.Chapter1Stele);
        beats.Add(StoryDirector.Solo("加百列", "让让！盾在这儿立住了，一只都别想越线！", SquadGabriel));
        beats.Add(StoryDirector.Solo("艾拉", "我勒个豆，全队打成这样？胆子肥嘟嘟的啊你们。", SquadElla));
        beats.Add(StoryDirector.Solo("塞拉", "先回血，别的回头说。", SquadSera));
        beats.Add(StoryDirector.Solo("你", "你们是哪位？我没点外援啊，还是那种不讲价的。", StoryPortraits.Player));
        beats.Add(StoryDirector.Solo("加百列", "有人托了我们，让我们盯着你点。细节打完再聊——先活着。", SquadGabriel));
        beats.Add(StoryDirector.BlackLine("三个人影站进你身侧，像早就该在那儿。"));
        return beats;
    }

    /// <summary>救场·支线岛版：接 IslandStoryBeats 的「独眼字条 / 岛况」口吻，不绑主线进度。</summary>
    static List<StoryBeat> BuildRescueIslandBeats(int islandId)
    {
        var beats = new List<StoryBeat>
        {
            StoryDirector.Narration("岛的怪围上来之前，三个人先一步替你们顶住了潮头。").Bg(IslandBg(islandId)),
            StoryDirector.Solo("加百列", "这岛的怪比字条上写的多三倍——独眼又开始瞒报了。", SquadGabriel),
            StoryDirector.Solo("艾拉", "队形没散，侧面归我们。你们打你们的，闪身步我们兜着。", SquadElla),
            StoryDirector.Solo("塞拉", "血别硬撑，喝口水再上。", SquadSera),
            StoryDirector.Solo("你", "你们怎么跟到岛上来的？这船票挺贵吧。", StoryPortraits.Player),
            StoryDirector.Solo("加百列", "有人托了我们。托我们的人说，你在哪，我们就在哪。", SquadGabriel),
            StoryDirector.BlackLine("潮声里，三个人默默往你身前又挪了半步。"),
        };
        return beats;
    }

    /// <summary>救场·揭底版（第 7 章后）：把委托链说透——赛琳受托，托她的那位一直没说破的名字此刻落地。</summary>
    static List<StoryBeat> BuildRescueRevealBeats(int chapter)
    {
        var beats = new List<StoryBeat>
        {
            StoryDirector.Narration("熟悉的三个身影又切了进来。这一次，他们不再遮掩来意。"),
        };
        if (chapter == 7)
            beats[0] = beats[0].Bg(StoryBackgrounds.AbyssChoice);
        beats.Add(StoryDirector.Solo("加百列", "别问怎么又是我们。委托人你其实天天见——公会前台，赛琳。", SquadGabriel));
        beats.Add(StoryDirector.Solo("艾拉", "前台背后还有一位。那位姑娘带队走之前就料到凶多吉少，才提前把话托给赛琳。", SquadElla));
        beats.Add(StoryDirector.Solo("塞拉", "她的原话是：看着他，别让他进裂隙。拉不住，就护着他别走太深。", SquadSera));
        beats.Add(StoryDirector.Solo("你", "……艾丽娅。你们一路跟着我，就为了这句嘱咐？", StoryPortraits.Player));
        beats.Add(StoryDirector.Solo("加百列", "对。所以别让那句嘱咐白托付——活着，一起回去。", SquadGabriel));
        beats.Add(StoryDirector.BlackLine("话说透了，盾墙反而立得更稳了。"));
        return beats;
    }

    // ---------- 入场救援（被围攻） ----------

    /// <summary>入场救援·第 5 章（沉船湾意象，配 EV02 preBattle:5）。</summary>
    static List<StoryBeat> BuildEntryCh5Beats()
    {
        return new List<StoryBeat>
        {
            StoryDirector.Narration("Boss 关的门还没关严，你就看见三个人被围在残骸中间——血条见底，盾上全是豁口。").Bg(StoryBackgrounds.WreckShip),
            StoryDirector.Solo("加百列", "来的正好！我们被咬住了——搭把手！", SquadGabriel),
            StoryDirector.Solo("艾拉", "半管血，剩三支箭，闪身步还能再秀三回合，别嫌弃！", SquadElla),
            StoryDirector.Solo("你", "我勒个豆，这不是上次救我的那队吗？撑住，我来了！", StoryPortraits.Player),
            StoryDirector.BlackLine("你冲进围圈，战局从这一刻改写。"),
        };
    }

    /// <summary>入场救援·第 7 章（配 EV05 preBattle:7，口吻贴宿敌线）。</summary>
    static List<StoryBeat> BuildEntryCh7Beats()
    {
        return new List<StoryBeat>
        {
            StoryDirector.Narration("Boss 关开场，那支熟悉的队伍正被围在深处——这一次，轮到你了。").Bg(StoryBackgrounds.AbyssChoice),
            StoryDirector.Solo("加百列", "别愣着！我们扛得住，但没多少余量了！", SquadGabriel),
            StoryDirector.Solo("艾拉", "说好的守护者当到头了——该你护我们一回！", SquadElla),
            StoryDirector.Solo("你", "行，这份人情我记下了。都别死！", StoryPortraits.Player),
            StoryDirector.BlackLine("三个人退到你身侧，血都只剩最后一口。"),
        };
    }

    /// <summary>入场救援·通用版（表配到其它章时兜底，不绑章节专属意象）。</summary>
    static List<StoryBeat> BuildEntryGenericBeats(int chapter)
    {
        return new List<StoryBeat>
        {
            StoryDirector.Narration($"第 {chapter} 章的 Boss 关里，三个人被围在怪群中央，血都只剩一口。"),
            StoryDirector.Solo("加百列", "来的正好！我们顶不住了——搭把手！", SquadGabriel),
            StoryDirector.Solo("艾拉", "快！这圈越缩越小了！", SquadElla),
            StoryDirector.Solo("你", "撑住，我来了！", StoryPortraits.Player),
            StoryDirector.BlackLine("你冲进围圈，战局从这一刻改写。"),
        };
    }

    // ---------- 谢幕（救援成功口径，2026-11-20 三次拍板） ----------

    /// <summary>谢幕·救援成功版（2026-11-20 三次拍板）：我方赢即成功，小队受伤没死；暗线句「是她让我们看着你的」不点名字。</summary>
    /// <summary>谢幕·救援成功版（2026-11-20 三次拍板：小队受伤没死、我方赢=救援成功；暗线句保留）。</summary>
    static List<StoryBeat> BuildOutcomeSuccessBeats()
    {
        return new List<StoryBeat>
        {
            StoryDirector.Narration("最后一只怪倒下。挂了彩的、差点躺下的，都被你从围圈里捞了回来——三个人，一个不少。"),
            StoryDirector.Solo("加百列", "呼——都活着。谢了，是她让我们看着你的。", SquadGabriel),
            StoryDirector.Solo("艾拉", "就是惨了点，箭袋比脸都干净，闪身步都跑出火星子了。你走的每一步，我们都替她看着。", SquadElla),
            StoryDirector.Solo("塞拉", "伤都归我管，回镇上喝两天热汤就好。别把挂彩当勋章。", SquadSera),
            StoryDirector.BlackLine("三人互相搀着退进通道深处。受伤，但没人掉队。"),
        };
    }

    /// <summary>岛 id → 岛背景（真源 StoryBackgrounds.Island01..08，越界返回 null 不铺图）。</summary>
    static string IslandBg(int islandId)
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
}
