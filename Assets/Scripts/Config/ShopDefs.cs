using System.Collections.Generic;

/// <summary>
/// 城镇商店的静态商品表（2026-09-15 新增）。
/// 分四个货架：技能解锁 / 抽卡券 / 资源补给 / 材料。
///
/// 为什么要有商店：技能分层解锁之后，「天赋」和「成就」两条途径都有明确入口，
/// 只有「商店」缺一个能花钱的地方。商店的性格是**花钱买时间**——
/// 所有商品都不提供独占内容，只让你比纯推图更早拿到。
///
/// ⚠️ 所有价格都是 [PLACEHOLDER]：按「一局金币收入」和「一次十连的心理价位」估的，
/// 没有真实经济数据支撑。上线前必须按实际产出速度重算，否则要么形同虚设要么通胀。
/// </summary>
public static class ShopDefs
{
    public enum Kind
    {
        Skill,      // 直接解锁一个技能
        Gacha,      // 技能券：随机解锁未解锁的技能
        Resource,   // 资源补给
        Material    // 强化/分解材料
    }

    public class Item
    {
        public string id;
        public string name;
        public string desc;
        public Kind kind;
        public ResourceWallet.ResourceType currency;
        public long price;
        /// <summary>Kind=Skill 时填技能 id。</summary>
        public string skillId;
        /// <summary>Kind=Gacha 时填抽取次数。</summary>
        public int drawCount;
        /// <summary>Kind=Resource/Material 时填发放的类型与数量。</summary>
        public ResourceWallet.ResourceType grantType;
        public long grantAmount;
        /// <summary>每天限购次数；0 = 不限。</summary>
        public int dailyLimit;
        /// <summary>达到该章节才上架（0 = 一直上架）。配合分层解锁，避免开局就看到买不起的东西。</summary>
        public int showFromChapter;
    }

    public static readonly Item[] All =
    {
        // ============ 技能货架：花钱提前拿到中期技能 ============
        new Item {
            id = "shop_skill_hawk_eye", name = "鹰眼", kind = Kind.Skill,
            desc = "全队暴击 +35%，持续 10 秒。冷却 20 秒。\n原本要推进到第 4 章才解锁，买断可立即获得。",
            currency = ResourceWallet.ResourceType.Gold, price = 3000, skillId = "hawk_eye",
            showFromChapter = 2
        },
        new Item {
            id = "shop_skill_flame_burst", name = "烈焰爆裂", kind = Kind.Skill,
            desc = "260% 攻击的范围伤害，半径 7。冷却 20 秒。\n火流派的核心输出技。",
            currency = ResourceWallet.ResourceType.Gold, price = 8000, skillId = "flame_burst",
            showFromChapter = 2
        },
        new Item {
            id = "shop_skill_thunder_chain", name = "连锁闪电", kind = Kind.Skill,
            desc = "160% 攻击连锁 4 段（合计 2.176 倍）。冷却 18 秒。\n原本走天赋树，这里用天赋石买断。",
            currency = ResourceWallet.ResourceType.TalentPoint, price = 2, skillId = "thunder_chain",
            showFromChapter = 3
        },
        new Item {
            id = "shop_skill_war_banner", name = "战旗号令", kind = Kind.Skill,
            desc = "全队攻击 +35%，持续 10 秒。冷却 24 秒。\n持续时间是同类技能里最长的。",
            currency = ResourceWallet.ResourceType.TalentPoint, price = 3, skillId = "war_banner",
            showFromChapter = 3
        },

        // ============ 抽卡券：随机解锁，比直接买断便宜但有随机性 ============
        new Item {
            id = "shop_gacha_1", name = "技能券 · 单抽", kind = Kind.Gacha,
            desc = "从当前未解锁的技能里随机解锁 1 个。\n比直接买断便宜，但抽到哪个不一定。",
            currency = ResourceWallet.ResourceType.Diamond, price = 500, drawCount = 1,
            showFromChapter = 2
        },
        new Item {
            id = "shop_gacha_10", name = "技能券 · 十连", kind = Kind.Gacha,
            desc = "连续抽取 10 次，相当于 9 折。\n至少出 1 个稀有及以上。",
            currency = ResourceWallet.ResourceType.Diamond, price = 4500, drawCount = 10,
            showFromChapter = 2
        },

        // ============ 资源补给 ============
        new Item {
            id = "shop_stamina_30", name = "体力药水", kind = Kind.Resource,
            desc = "立即恢复 30 点体力。",
            currency = ResourceWallet.ResourceType.Gold, price = 800,
            grantType = ResourceWallet.ResourceType.Stamina, grantAmount = 30,
            dailyLimit = 3
        },
        new Item {
            id = "shop_gold_bag", name = "金币袋", kind = Kind.Resource,
            desc = "立即获得 5000 金币。",
            currency = ResourceWallet.ResourceType.Diamond, price = 50,
            grantType = ResourceWallet.ResourceType.Gold, grantAmount = 5000,
            dailyLimit = 5
        },
        new Item {
            id = "shop_talent_stone_3", name = "天赋石 ×3", kind = Kind.Resource,
            desc = "天赋树与技能解锁的通用货币。",
            currency = ResourceWallet.ResourceType.Diamond, price = 200,
            grantType = ResourceWallet.ResourceType.TalentPoint, grantAmount = 3,
            dailyLimit = 3
        },

        // ============ 材料 ============
        new Item {
            id = "shop_enchant_5", name = "强化石 ×5", kind = Kind.Material,
            desc = "装备强化材料。",
            currency = ResourceWallet.ResourceType.Gold, price = 1500,
            grantType = ResourceWallet.ResourceType.EnchantStone, grantAmount = 5,
            dailyLimit = 5
        },
        new Item {
            id = "shop_decompose_10", name = "分解材料 ×10", kind = Kind.Material,
            desc = "装备分解产物，用于兑换与合成。",
            currency = ResourceWallet.ResourceType.Gold, price = 800,
            grantType = ResourceWallet.ResourceType.DecomposeMat, grantAmount = 10,
            dailyLimit = 5
        },
    };

    /// <summary>每日限购用的日期键（本地时间 yyyyMMdd）。</summary>
    public static string TodayKey() => System.DateTime.Now.ToString("yyyyMMdd");

    public static Item Get(string id)
    {
        for (int i = 0; i < All.Length; i++)
            if (All[i].id == id) return All[i];
        return null;
    }

    public static string KindName(Kind kind)
    {
        switch (kind)
        {
            case Kind.Skill: return "技能";
            case Kind.Gacha: return "抽卡";
            case Kind.Resource: return "补给";
            default: return "材料";
        }
    }

    public static string CurrencyName(ResourceWallet.ResourceType type)
    {
        switch (type)
        {
            case ResourceWallet.ResourceType.Gold: return "金币";
            case ResourceWallet.ResourceType.Diamond: return "钻石";
            case ResourceWallet.ResourceType.TalentPoint: return "天赋石";
            case ResourceWallet.ResourceType.Stamina: return "体力";
            case ResourceWallet.ResourceType.EnchantStone: return "强化石";
            default: return "分解材料";
        }
    }
}
