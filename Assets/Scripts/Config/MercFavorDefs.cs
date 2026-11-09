using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 佣兵好感配置表（2026-10-26 主人拍板「佣兵好感界面」，预制体 Resources/Prefabs/Town/佣兵好感.prefab）。
///
/// 玩法口径（2026-11-09 口径）：
/// - 好感值真源 = SaveData.mercTrust（与酒馆闲话 +1 共用同一套好感成长，单一出口 AddMercTrust）。
/// - 对话：一天一轮、一轮一问、一次只选一个答案（<see cref="TalksPerDay"/>）。选中后佣兵在气泡里回话，
///   旧三答案依次右滑撤出、次日的新题 + 新三答案依次滑入（当天禁点，次日才可答）。
/// - 三个答案分别对应 +10（额外回 1 个普通品质礼物）/ +5 / +0 三档回报，按 天+佣兵 轮换挂槽，
///   分值不展示、选完才揭晓（<see cref="TalkTierAt"/>）。
/// - 题库：按花名册姓名/昵称/职业与已有专属对话扩写，C# 常量，按 日键 + 佣兵 确定性取题。
/// - 礼物：独立好感礼物仓（SaveData.favorGifts），点图标送出扣 1 加对应好感。
/// - 羁绊地图：好感攒满（FavorMax）解锁入口；通关（二期专属双人关）后该佣兵全属性永久提升（×1.5，文档拍板）。
///
/// 加人 = 本表补对话；加礼物 = 本表补一行。数值全在本表，代码零散值一律禁止。
/// 文案为占位，主人终审（照信任交流文档口径）。
/// </summary>
public static class MercFavorDefs
{
    // —— 核心数值（就这一处，改这里）——

    /// <summary>好感进度爱心颗数（预制体「好感进度/爱心1..6」已摆 6 颗；2026-10-31 主人拍板：
    /// 爱心本体即进度条——每颗心内部随好感一点点涨满，每颗对应 <see cref="FavorPerHeart"/> 分）。</summary>
    public const int HeartCount = 6;

    /// <summary>每颗心对应的好感分（总满值 = HeartCount × FavorPerHeart = 60）。</summary>
    public const int FavorPerHeart = 10;

    /// <summary>好感总满值（羁绊地图解锁线）。</summary>
    public const int FavorMax = HeartCount * FavorPerHeart;

    /// <summary>每名佣兵每天对话轮数（2026-11-09 口径：一天一轮、一轮一问、一次只选一个答案）。</summary>
    public const int TalksPerDay = 1;

    /// <summary>对话选项个数（预制体「选中对话/1|2|3」三槽）。</summary>
    public const int DialogueOptionCount = 3;

    /// <summary>三个答案分别对应的好感回报档：最高档额外回 1 个普通品质礼物。
    /// 哪条答案对应哪档按 天+佣兵 轮换，界面不展示分值，选完才揭晓。数值只此一处。</summary>
    public const int BestTalkFavor = 10;
    public const int MidTalkFavor = 5;
    public const int NoTalkFavor = 0;
    static readonly int[] TalkFavorTiers = { BestTalkFavor, MidTalkFavor, NoTalkFavor };

    /// <summary>按档位序号取好感回报（唯一出口；序号由系统层的每日轮换给出）。</summary>
    public static int TalkTierAt(int tierIndex)
    {
        int n = TalkFavorTiers.Length;
        return TalkFavorTiers[((tierIndex % n) + n) % n];
    }

    /// <summary>通关羁绊地图后的属性乘区（×1.5）。【2026-11-09 口径】范围＝血/攻/防/魔攻/魔防，
    /// 攻速/移速/射程不加；唯一计算出口 = MercRosterDefs 的属性公式唯一实现。</summary>
    public const float BondClearStatMul = 1.5f;

    /// <summary>
    /// 羁绊乘区唯一取值出口：按 hireId 读存档通关标记，未通关恒 1.0、首通后恒 <see cref="BondClearStatMul"/>。
    /// 数值 / 判据只此一处，战斗与所有面板同源，禁止在 UI / 战斗各写一份 ×1.5。
    /// hireId 缺失时 LogError 后按不加成处理（不静默放行）。
    /// </summary>
    public static float BondClearStatMulFor(string hireId)
    {
        if (string.IsNullOrEmpty(hireId))
        {
            Debug.LogError("[MercFavorDefs] 羁绊乘区缺 hireId，按不加成处理（不静默放行）");
            return 1f;
        }
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        return data != null && data.IsMercBondCleared(hireId) ? BondClearStatMul : 1f;
    }

    /// <summary>羁绊副本场景章（≈5~7 章强度；单难度不开放选择，固定噩梦档。两个常量都可单独调）。</summary>
    public const int BondDungeonChapter = 7;

    /// <summary>羁绊副本固定难度：0=普通 / 1=困难 / 2=噩梦（BattleManager 会把 PendingBattleDifficulty 钳到 0~2）。</summary>
    public const int BondDungeonDifficulty = 2;

    /// <summary>羁绊副本一次性通关奖励钻石数：普通 50 / 稀有 100 / 传奇 200（2026-11-03 主人拍板按稀有度发放）。</summary>
    public const int BondClearDiamondCommon = 50;
    public const int BondClearDiamondRare = 100;
    public const int BondClearDiamondLegendary = 200;

    /// <summary>按佣兵稀有度取首通钻石；稀有度真源 = MercRosterDefs.Def.Rarity，不另造判断。</summary>
    public static int BondClearDiamondOf(MercRosterDefs.MercRarity rarity)
    {
        switch (rarity)
        {
            case MercRosterDefs.MercRarity.Rare: return BondClearDiamondRare;
            case MercRosterDefs.MercRarity.Legendary: return BondClearDiamondLegendary;
            default: return BondClearDiamondCommon;
        }
    }

    // —— 对话文案（占位，主人终审）——

    public class FavorTalk
    {
        public string hireId;      // 专属某佣兵；空 = 通用池
        public string question;    // 佣兵的问题（显示在右上立绘的对话框里）
        public string[] options;   // 3 条答案（对应槽 1/2/3；分值不展示，特殊位每天轮换）
        public string[] replies;   // 3 条佣兵回应（选中后 Toast 展示）
    }

    /// <summary>专属题库（按花名册姓名 / 昵称 / 职业写；每人 3 题，按日轮换）。未配的佣兵走通用池。</summary>
    static readonly FavorTalk[] SpecialTalks =
    {
        // H001 马库斯（老盾·剑盾）
        new FavorTalk
        {
            hireId = "H001",
            question = "你说，一把盾牌最重要的是什么？",
            options = new[] { "够硬就行，扛住就完事了！", "护住身后的人，才算一面好盾。", "……好看也很重要吧？" },
            replies = new[] { "哈哈，够硬是本分！", "……说得好。这话我记下了。", "嘿，你小子还挺懂。" },
        },
        new FavorTalk
        {
            hireId = "H001",
            question = "在酒馆坐了半宿，你猜我在等谁？",
            options = new[] { "等一个能陪我喝到天亮的人", "等老板上菜吧", "等……我？我勒个豆" },
            replies = new[] { "哈哈，就等这句。", "你小子，一点浪漫都不给。", "可不就是等你嘛，老伙计。" },
        },
        new FavorTalk
        {
            hireId = "H001",
            question = "打完这仗，你想去哪儿？",
            options = new[] { "回镇上，好好睡一觉", "哪儿有架打去哪儿", "跟着你，去哪都行" },
            replies = new[] { "踏实。这才是过日子。", "你这脾气，跟我年轻时一个样。", "……那这面盾就更不能撤了。" },
        },
        // H003 塔克（重盾·引导局坦克）
        new FavorTalk
        {
            hireId = "H003",
            question = "打起来我总冲最前面，你不嫌我挡路？",
            options = new[] { "你就是最稳的那堵墙！", "偶尔……也让让我嘛。", "确实有点挡……开玩笑的！" },
            replies = new[] { "哈！这话中听。", "那可不行，盾牌手哪有让路的。", "好家伙，胆子见长啊。" },
        },
        new FavorTalk
        {
            hireId = "H003",
            question = "我这身板，站前排是不是太占地方了？",
            options = new[] { "占得好，后面才安心", "要不往后稍稍？", "你站前面我都看不见怪了" },
            replies = new[] { "那我就一直站着。", "让？没学过。", "哈哈哈，怪也被你挡住了。" },
        },
        new FavorTalk
        {
            hireId = "H003",
            question = "要是有一天我扛不住了，你怎么办？",
            options = new[] { "我替你扛一会儿", "那就一起撤", "不会有那天的" },
            replies = new[] { "……好。记住这话。", "撤也得你先撤。", "嘿，借你吉言。" },
        },
        // H011 路加（男僧侣·牧师）
        new FavorTalk
        {
            hireId = "H011",
            question = "施主近日可有烦心事？",
            options = new[] { "有你在，就安心多了。", "还好，没什么大事。", "那诵段经给我听听吧。" },
            replies = new[] { "阿弥陀佛，贫僧必不负所托。", "心事莫憋着，贫僧随时在。", "也罢，就结个善缘。" },
        },
        new FavorTalk
        {
            hireId = "H011",
            question = "诵经的时候，你听见了什么？",
            options = new[] { "听见你念得挺好听", "听见……我睡着了，抱歉", "听见风，挺静的" },
            replies = new[] { "善哉，那便再诵一段。", "……贫僧的经，竟有安神之效。", "静，最难得。" },
        },
        new FavorTalk
        {
            hireId = "H011",
            question = "若只能许一个愿，施主许什么？",
            options = new[] { "大家都能平安回来", "许我抽卡别再歪了", "许你别总叫我施主" },
            replies = new[] { "与贫僧一般。", "……此愿，佛祖怕是管不着。", "那贫僧便改口，叫你一声同伴。" },
        },
        // H004 维克（钢盾·传奇）
        new FavorTalk
        {
            hireId = "H004",
            question = "……你为什么总盯着我的剑看？",
            options = new[] { "剑很漂亮，人更沉稳。", "没什么，随便看看。", "我在想它能卖多少钱。" },
            replies = new[] { "……哼。会说话。", "随你。", "胆子不小。滚。" },
        },
        new FavorTalk
        {
            hireId = "H004",
            question = "这把剑，沾过不少人的血。",
            options = new[] { "那它护过更多人吧", "……能擦干净吗", "别这么说，它挺好看的" },
            replies = new[] { "……你倒是会挑话。", "擦不干净。", "好看？……哼。" },
        },
        new FavorTalk
        {
            hireId = "H004",
            question = "没人问我累不累。",
            options = new[] { "那我现在问了：累吗", "累了就歇，我守着", "强者不说累吧？" },
            replies = new[] { "……不累。但多谢。", "……守好。", "强者也会累，只是没人问。" },
        },
    };

    /// <summary>通用题库（未配专属文案的佣兵用；名字/职业不进文案，谁都能用）。</summary>
    static readonly FavorTalk[] GenericTalks =
    {
        new FavorTalk
        {
            question = "最近过得怎么样？想聊点什么？",
            options = new[] { "今天也辛苦了，要一起吃点东西吗？", "有什么喜欢的东西吗？下次带给你。", "最近有什么烦心事吗？可以跟我说说。" },
            replies = new[] { "嗯，有你这句话就够了。", "……你猜？聊得来的话就告诉你。", "说出来舒坦多了，多谢关心。" },
        },
        new FavorTalk
        {
            question = "路上捡到个奇怪的小玩意儿，你猜是什么？",
            options = new[] { "发光的石头？", "会不会是陷阱啊", "先给我看看！" },
            replies = new[] { "猜得挺准，送你啦。", "警惕点好，是块旧徽章。", "别抢，是给你的。" },
        },
        new FavorTalk
        {
            question = "今晚的星星挺亮，要不要一起看会儿？",
            options = new[] { "好啊，正好歇歇", "我怕黑，你别走", "不如去吃夜宵" },
            replies = new[] { "那就这么定了。", "不走，坐着。", "……你倒是实在。走。" },
        },
    };

    /// <summary>该佣兵的题库题数（唯一出口；没配专属走通用池）。</summary>
    public static int TalkCountOf(string hireId)
    {
        int n = 0;
        for (int i = 0; i < SpecialTalks.Length; i++)
            if (SpecialTalks[i].hireId == hireId) n++;
        return n > 0 ? n : GenericTalks.Length;
    }

    /// <summary>按 日键 + 佣兵 确定性取今天/明天该出哪道题（同一天不变、跨天换题）。</summary>
    public static int TalkIndexOfDay(string hireId, int dayKey)
    {
        int count = TalkCountOf(hireId);
        long h = 0;
        if (!string.IsNullOrEmpty(hireId))
            for (int i = 0; i < hireId.Length; i++) h = h * 31 + hireId[i];
        long seed = (long)dayKey * 31 + h;
        if (seed < 0) seed = -seed;
        return (int)(seed % count);
    }

    /// <summary>查第 index 道题（唯一出口）：先查专属题库，未配走通用池；index 按题数取模归一。</summary>
    public static void GetTalk(string hireId, int index, out string question, out string[] options, out string[] replies)
    {
        int count = TalkCountOf(hireId);
        int want = ((index % count) + count) % count;
        int seen = 0;
        for (int i = 0; i < SpecialTalks.Length; i++)
        {
            if (SpecialTalks[i].hireId != hireId) continue;
            if (seen == want)
            {
                question = SpecialTalks[i].question;
                options = SpecialTalks[i].options;
                replies = SpecialTalks[i].replies;
                return;
            }
            seen++;
        }
        var t = GenericTalks[want];
        question = t.question;
        options = t.options;
        replies = t.replies;
    }

    // —— 礼物表（图标 = Resources/Art/UI/礼物/32px-itemXXX，主人 2026-10-26 供图）——

    /// <summary>礼物品质（2026-10-27 主人拍板「礼物分三个品质」）：与 Favor 档一一对应——
    /// 普通(2) / 稀有(4) / 传奇(8)。传奇礼物只在<b>副本</b>（礼物本 / 羁绊本）与<b>里程商店每日随机</b>获得，
    /// 每日对话回礼不掉传奇（MercFavorSystem.GrantRandomGifts 的 includeLegendary=false 拦）。</summary>
    public enum GiftQuality
    {
        Common = 1,    // 吃食档 Favor=2
        Rare = 2,      // 稀罕物档 Favor=4
        Legendary = 3, // 珍品档 Favor=8
    }

    public struct GiftDef
    {
        public string Id;        // 业务 id（gift_207 口径，与图标文件号对应）
        public string IconPath;  // Resources 路径（不带扩展名，铁律 16：按路径加载）
        public string Name;      // 展示名（AI 按图标识图起名，主人可改）
        public int Favor;        // 送出加的好感值
        public GiftQuality Quality; // 品质（由 Favor 推导，见 G）
    }

    /// <summary>礼物 20 种，分值三档：吃食 2 / 稀罕物 4 / 珍品 8（数值主人可整批调）。</summary>
    public static readonly GiftDef[] Gifts =
    {
        G("gift_207", 207, "金块",     8),
        G("gift_208", 208, "紫水晶",   8),
        G("gift_209", 209, "水晶球",   8),
        G("gift_289", 289, "龙虾",     4),
        G("gift_292", 292, "小章鱼",   4),
        G("gift_295", 295, "火腿",     4),
        G("gift_298", 298, "烤肉排",   4),
        G("gift_301", 301, "面包",     2),
        G("gift_302", 302, "苹果",     2),
        G("gift_303", 303, "香蕉",     2),
        G("gift_304", 304, "葡萄",     2),
        G("gift_305", 305, "草莓",     2),
        G("gift_370", 370, "折扇",     4),
        G("gift_371", 371, "玉佩",     8),
        G("gift_375", 375, "风筝",     4),
        G("gift_390", 390, "蝴蝶标本", 4),
        G("gift_391", 391, "陈年好酒", 8),
        G("gift_392", 392, "麦酒",     4),
        G("gift_393", 393, "旅行行囊", 8),
        G("gift_394", 394, "幸运骰子", 4),
    };

    static GiftDef G(string id, int fileNo, string name, int favor)
    {
        var q = favor >= 8 ? GiftQuality.Legendary : favor >= 4 ? GiftQuality.Rare : GiftQuality.Common;
        return new GiftDef { Id = id, IconPath = "UI/Art/礼物/32px-item" + fileNo, Name = name, Favor = favor, Quality = q };
    }

    /// <summary>查礼物定义（唯一出口；查不到 = 表里没有这个 id，返回 false）。</summary>
    public static bool TryGetGift(string giftId, out GiftDef def)
    {
        for (int i = 0; i < Gifts.Length; i++)
        {
            if (Gifts[i].Id == giftId) { def = Gifts[i]; return true; }
        }
        def = default;
        return false;
    }

    /// <summary>是否传奇礼物（唯一判据，别在别处抄 Favor>=8）。</summary>
    public static bool IsLegendary(GiftDef def) => def.Quality == GiftQuality.Legendary;

    /// <summary>品质展示名（送礼界面标签 / 里程商店用，单一出口）。</summary>
    public static string QualityName(GiftQuality q)
        => q == GiftQuality.Legendary ? "传奇" : q == GiftQuality.Rare ? "稀有" : "普通";

    /// <summary>品质配色（16 进制 RGB，送礼界面标签着色用；色值唯一来源 = RarityPalette，2026-11-03 主人拍板全项目统一，本方法只做转发、不抄色值）。</summary>
    public static string QualityColorHex(GiftQuality q)
        => q == GiftQuality.Legendary ? UnityEngine.ColorUtility.ToHtmlStringRGB(RarityPalette.Legendary)   // 橙金 #FFB833
         : q == GiftQuality.Rare ? UnityEngine.ColorUtility.ToHtmlStringRGB(RarityPalette.Rare)        // 蓝 #59ADFA
         : UnityEngine.ColorUtility.ToHtmlStringRGB(RarityPalette.Common);   // 灰白 #CCD1DB

    // —— 羁绊地图 ——

    /// <summary>羁绊地图名（按佣兵名拼，主人有专属地名随时整表替换）。</summary>
    public static string BondMapName(string hireId)
    {
        return MercRosterDefs.TryGetByHireId(hireId, out var d)
            ? d.Name + "的心防试炼"
            : "羁绊试炼";
    }
}
