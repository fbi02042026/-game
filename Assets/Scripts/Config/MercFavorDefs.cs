using System.Collections.Generic;

/// <summary>
/// 佣兵好感配置表（2026-10-26 主人拍板「佣兵好感界面」，预制体 Resources/Prefabs/Town/佣兵好感.prefab）。
///
/// 玩法口径（2026-10-26 晚二次拍板修订）：
/// - 好感值真源 = SaveData.mercTrust（与酒馆闲话 +1 共用同一套好感成长，单一出口 AddMercTrust）。
/// - 对话：每名佣兵每天 3 次机会、一次选一个答案（<see cref="TalksPerDay"/>）；分值不展示——
///   每天轮换一个「特殊答案」（<see cref="SpecialFavor"/>），另外两个是普通答案（<see cref="NormalFavor"/>），
///   玩家猜哪个最对佣兵胃口；佣兵的问题显示在右上立绘的对话框里（代码建气泡，主人拍板）。
/// - 礼物：独立好感礼物仓（SaveData.favorGifts），点图标送出扣 1 加对应好感。
/// - 羁绊地图：好感攒满（FavorMax）解锁入口；通关（二期专属双人关）后该佣兵全属性永久提升（×1.5，文档拍板）。
///
/// 加人 = 本表补对话；加礼物 = 本表补一行。数值全在本表，代码零散值一律禁止。
/// 文案为占位，主人终审（照信任交流文档口径）。
/// </summary>
public static class MercFavorDefs
{
    // —— 核心数值（就这一处，改这里）——

    /// <summary>好感进度爱心颗数（预制体「好感进度/爱心1..6」已摆 6 颗，全显示为空心 + 进度条填充）。</summary>
    public const int HeartCount = 6;

    /// <summary>每颗心对应的好感分（总满值 = HeartCount × FavorPerHeart = 60）。</summary>
    public const int FavorPerHeart = 10;

    /// <summary>好感总满值（羁绊地图解锁线）。</summary>
    public const int FavorMax = HeartCount * FavorPerHeart;

    /// <summary>每名佣兵每天对话次数（2026-10-26 晚拍板「每天只能选3次 一次只能选一个」）。</summary>
    public const int TalksPerDay = 3;

    /// <summary>对话选项个数（预制体「选中对话/1|2|3」三槽）。</summary>
    public const int DialogueOptionCount = 3;

    /// <summary>「特殊答案」好感分（每天轮换一个，分值不展示）。</summary>
    public const int SpecialFavor = 5;

    /// <summary>普通答案好感分（另外两个，分值不展示）。</summary>
    public const int NormalFavor = 2;

    /// <summary>通关羁绊地图后该佣兵全属性乘区（2026-10-26 信任交流文档拍板 ×1.5；二期挂乘区用）。</summary>
    public const float BondClearStatMul = 1.5f;

    // —— 对话文案（占位，主人终审）——

    public class FavorTalk
    {
        public string hireId;      // 专属某佣兵；空 = 通用池
        public string question;    // 佣兵的问题（显示在右上立绘的对话框里）
        public string[] options;   // 3 条答案（对应槽 1/2/3；分值不展示，特殊位每天轮换）
        public string[] replies;   // 3 条佣兵回应（选中后 Toast 展示）
    }

    /// <summary>专属对话（首批 4 人，照信任交流文档 §2.7 名单）。查不到的佣兵走通用池。</summary>
    static readonly FavorTalk[] SpecialTalks =
    {
        new FavorTalk
        {
            hireId = "H001",   // 马库斯（老盾·剑盾）
            question = "你说，一把盾牌最重要的是什么？",
            options = new[] { "够硬就行，扛住就完事了！", "护住身后的人，才算一面好盾。", "……好看也很重要吧？" },
            replies = new[] { "哈哈，够硬是本分！", "……说得好。这话我记下了。", "嘿，你小子还挺懂。" },
        },
        new FavorTalk
        {
            hireId = "H003",   // 塔克（重盾·引导局坦克）
            question = "打起来我总冲最前面，你不嫌我挡路？",
            options = new[] { "你就是最稳的那堵墙！", "偶尔……也让让我嘛。", "确实有点挡……开玩笑的！" },
            replies = new[] { "哈！这话中听。", "那可不行，盾牌手哪有让路的。", "好家伙，胆子见长啊。" },
        },
        new FavorTalk
        {
            hireId = "H011",   // 路加（男僧侣·牧师）
            question = "施主近日可有烦心事？",
            options = new[] { "有你在，就安心多了。", "还好，没什么大事。", "那诵段经给我听听吧。" },
            replies = new[] { "阿弥陀佛，贫僧必不负所托。", "心事莫憋着，贫僧随时在。", "也罢，就结个善缘。" },
        },
        new FavorTalk
        {
            hireId = "H004",   // 维克（钢盾·传奇）
            question = "……你为什么总盯着我的剑看？",
            options = new[] { "剑很漂亮，人更沉稳。", "没什么，随便看看。", "我在想它能卖多少钱。" },
            replies = new[] { "……哼。会说话。", "随你。", "胆子不小。滚。" },
        },
    };

    /// <summary>通用对话池（未配专属文案的佣兵用）。</summary>
    const string GenericQuestion = "最近过得怎么样？想聊点什么？";
    static readonly string[] GenericOptions =
    {
        "今天也辛苦了，要一起吃点东西吗？",
        "有什么喜欢的东西吗？下次带给你。",
        "最近有什么烦心事吗？可以跟我说说。",
    };
    static readonly string[] GenericReplies =
    {
        "嗯，有你这句话就够了。",
        "……你猜？聊得来的话就告诉你。",
        "说出来舒坦多了，多谢关心。",
    };

    /// <summary>查对话组（唯一出口）：先查专属，未配走通用池。</summary>
    public static void GetTalk(string hireId, out string question, out string[] options, out string[] replies)
    {
        for (int i = 0; i < SpecialTalks.Length; i++)
        {
            if (SpecialTalks[i].hireId == hireId)
            {
                question = SpecialTalks[i].question;
                options = SpecialTalks[i].options;
                replies = SpecialTalks[i].replies;
                return;
            }
        }
        question = GenericQuestion;
        options = GenericOptions;
        replies = GenericReplies;
    }

    // —— 礼物表（图标 = Resources/Art/UI/礼物/32px-itemXXX，主人 2026-10-26 供图）——

    public struct GiftDef
    {
        public string Id;        // 业务 id（gift_207 口径，与图标文件号对应）
        public string IconPath;  // Resources 路径（不带扩展名，铁律 16：按路径加载）
        public string Name;      // 展示名（AI 按图标识图起名，主人可改）
        public int Favor;        // 送出加的好感值
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
        => new GiftDef { Id = id, IconPath = "Art/UI/礼物/32px-item" + fileNo, Name = name, Favor = favor };

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

    // —— 羁绊地图 ——

    /// <summary>羁绊地图名（按佣兵名拼，主人有专属地名随时整表替换）。</summary>
    public static string BondMapName(string hireId)
    {
        return MercRosterDefs.TryGetByHireId(hireId, out var d)
            ? d.Name + "的心防试炼"
            : "羁绊试炼";
    }
}
