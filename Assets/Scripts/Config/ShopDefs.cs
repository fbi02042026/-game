using System.Collections.Generic;

/// <summary>
/// 城镇商店的静态商品表（2026-09-15 定稿第二版）。
///
/// 定位：**花钱买时间，不卖独占内容**——所有商品都能通过推图免费拿到，买断只是提前。
///
/// 技能货架按稀有度分三种卖法（这是本版的核心改动）：
///   普通 → 金币直购（便宜，1 局内可攒够）
///   稀有 → 金币 / 天赋石直购（中价）
///   史诗 → **只能买碎片**，攒够合成（长期目标，也让抽卡有存在价值）
///   传说 → **完全不卖**，只走章节 / 成就 / 抽卡重复碎片，保住炫耀价值
///
/// ⚠️ 价格按「第一章一局净收入 ≈ 4500 金币」估的（怪 15~25 只×≈18 金×10 关 + 通关金 ≈ 515），
/// 没有实测支撑。上线前必须按真实一局收入重算，否则要么形同虚设要么通胀。
/// </summary>
public static class ShopDefs
{
    public enum Kind
    {
        Skill,      // 直接解锁一个技能（普通 / 稀有）
        Fragment,   // 技能碎片（史诗）
        Gacha,      // 技能券：随机解锁未解锁的技能，重复转碎片
        Resource,   // 资源补给
        Material,   // 强化 / 分解材料
        Merc        // 解锁佣兵（酒馆名册不列的那部分，见 MercRosterDefs.ShopRoster）
    }

    public class Item
    {
        public string id;
        public string name;
        public string desc;
        public Kind kind;
        public ResourceWallet.ResourceType currency;
        public long price;
        /// <summary>Kind=Skill / Fragment 时填技能 id。</summary>
        public string skillId;
        /// <summary>Kind=Merc 时填佣兵 HireId（H001~H022）。</summary>
        public string mercId;
        /// <summary>Kind=Fragment 时填一次买几片。</summary>
        public int fragmentCount;
        /// <summary>Kind=Gacha 时填抽取次数。</summary>
        public int drawCount;
        /// <summary>Kind=Resource/Material 时填发放的类型与数量。</summary>
        public ResourceWallet.ResourceType grantType;
        public long grantAmount;
        /// <summary>每天限购次数；0 = 不限。</summary>
        public int dailyLimit;
        /// <summary>达到该章节才上架（0 = 一直上架）。</summary>
        public int showFromChapter;
    }

    /// <summary>手写货架：技能 / 碎片 / 抽卡 / 补给 / 材料。</summary>
    static readonly Item[] Fixed =
    {
        // ================= 技能 · 普通（金币直购）=================
        // 这四个都是「等 2~4 章才会白给」的技能，花钱提前 1~2 章拿到。
        new Item {
            id = "shop_skill_stone_skin", name = "石肤术", kind = Kind.Skill,
            desc = "防御 +30%，持续 10 秒。冷却 16 秒。\n原本通关第 2 章解锁，买断可立即获得。",
            currency = ResourceWallet.ResourceType.Gold, price = 2000, skillId = "stone_skin"
        },
        new Item {
            id = "shop_skill_iron_wall", name = "铁壁壁垒", kind = Kind.Skill,
            desc = "防御 +45%，持续 8 秒。冷却 22 秒。\n石肤术的上位版，减伤更猛但更短。",
            currency = ResourceWallet.ResourceType.Gold, price = 2500, skillId = "iron_wall"
        },
        new Item {
            id = "shop_skill_spirit_wolf", name = "灵狼突袭", kind = Kind.Skill,
            desc = "3 × 130% 弹幕伤害。冷却 18 秒。\n原本通关第 3 章解锁。",
            currency = ResourceWallet.ResourceType.Gold, price = 2000, skillId = "spirit_wolf"
        },
        new Item {
            id = "shop_skill_hawk_eye", name = "鹰眼", kind = Kind.Skill,
            desc = "全队暴击 +35%，持续 10 秒。冷却 20 秒。\n原本推进到第 4 章才解锁。",
            currency = ResourceWallet.ResourceType.Gold, price = 2000, skillId = "hawk_eye"
        },

        // ================= 技能 · 稀有（金币 / 天赋石直购）=================
        new Item {
            id = "shop_skill_quake_slam", name = "震地重击", kind = Kind.Skill,
            desc = "300% 范围伤害，半径 8。冷却 21 秒。\n近战专属——远程职业不会看到本条。",
            currency = ResourceWallet.ResourceType.Gold, price = 6000, skillId = "quake_slam",
            showFromChapter = 2
        },
        new Item {
            id = "shop_skill_thunder_chain", name = "连锁闪电", kind = Kind.Skill,
            desc = "160% 攻击连锁 4 段（合计 2.176 倍）。冷却 18 秒。\n原本走天赋树，这里用天赋石买断。",
            currency = ResourceWallet.ResourceType.TalentPoint, price = 5, skillId = "thunder_chain",
            showFromChapter = 2
        },
        new Item {
            id = "shop_skill_war_banner", name = "战旗号令", kind = Kind.Skill,
            desc = "全队攻击 +35%，持续 10 秒。冷却 24 秒。\n持续时间是同类增益里最长的。",
            currency = ResourceWallet.ResourceType.TalentPoint, price = 7, skillId = "war_banner",
            showFromChapter = 3
        },

        // ================= 技能 · 史诗（只卖碎片）=================
        // 史诗不直购：攒 80 片合成，让顶级技能是「长期目标」而不是「一次大额消费」。
        new Item {
            id = "shop_frag_flame_burst", name = "烈焰爆裂残卷 ×5", kind = Kind.Fragment,
            desc = "260% 攻击的范围伤害，半径 7。冷却 20 秒。\n合成需 80 片，本条一次给 5 片。",
            currency = ResourceWallet.ResourceType.Gold, price = 800, skillId = "flame_burst",
            fragmentCount = 5, dailyLimit = 2, showFromChapter = 2
        },
        new Item {
            id = "shop_frag_sacred_revival", name = "圣愈术残卷 ×5", kind = Kind.Fragment,
            desc = "48% 最大生命 + 4.0×攻击的治疗。冷却 22 秒。\n合成需 80 片。",
            currency = ResourceWallet.ResourceType.Gold, price = 800, skillId = "sacred_revival",
            fragmentCount = 5, dailyLimit = 2, showFromChapter = 3
        },
        new Item {
            id = "shop_frag_arrow_storm", name = "箭雨风暴残卷 ×5", kind = Kind.Fragment,
            desc = "5 × 90% 弹幕（合计 4.5 倍）。冷却 22 秒。\n合成需 80 片。",
            currency = ResourceWallet.ResourceType.Gold, price = 800, skillId = "arrow_storm",
            fragmentCount = 5, dailyLimit = 2, showFromChapter = 3
        },
        new Item {
            id = "shop_frag_arcane_flame", name = "秘法烈焰残卷 ×5", kind = Kind.Fragment,
            desc = "310% 范围伤害，半径 8。冷却 26 秒。\n火流派后期核心，合成需 80 片。",
            currency = ResourceWallet.ResourceType.Gold, price = 800, skillId = "arcane_flame",
            fragmentCount = 5, dailyLimit = 2, showFromChapter = 4
        },

        // ================= 抽卡券 =================
        // ⚠ 2026-09-18 重定价：当前版本**不做内购**，钻石是纯免费货币，
        // 免费产出 ≈ 240~340 钻/月（签到周峰值 60 + 连击档位）。
        // 原 500/4500 意味着 2 个月才够一次单抽，抽卡对免费玩家等于关闭。
        // 现按「首月 3 次单抽、4 个月 1 次十连」重定：150 / 1350（十连仍是 9 折）。
        // 换算效率：单抽 150 钻换一个技能（普通直购 2000 金、稀有 6000 金）≈ 13~40 金/钻，
        // 与金币袋的 16 金/钻 同一量级，两者都值得买、会形成取舍。
        new Item {
            id = "shop_gacha_1", name = "技能券 · 单抽", kind = Kind.Gacha,
            desc = "从当前未解锁的技能里随机解锁 1 个。\n抽到已拥有的会转成该技能碎片。",
            currency = ResourceWallet.ResourceType.Diamond, price = 150, drawCount = 1,
            showFromChapter = 2
        },
        new Item {
            id = "shop_gacha_10", name = "技能券 · 十连", kind = Kind.Gacha,
            desc = "连续抽取 10 次，相当于 9 折。\n至少出 1 个稀有及以上。",
            currency = ResourceWallet.ResourceType.Diamond, price = 1350, drawCount = 10,
            showFromChapter = 2
        },

        // ================= 资源补给 =================
        new Item {
            id = "shop_stamina_30", name = "体力药水", kind = Kind.Resource,
            desc = "立即恢复 30 点体力（一次冒险消耗 10 点）。",
            currency = ResourceWallet.ResourceType.Gold, price = 400,
            grantType = ResourceWallet.ResourceType.Stamina, grantAmount = 30,
            dailyLimit = 3
        },
        new Item {
            id = "shop_gold_bag", name = "金币袋", kind = Kind.Resource,
            desc = "立即获得 800 金币。",
            currency = ResourceWallet.ResourceType.Diamond, price = 50,
            grantType = ResourceWallet.ResourceType.Gold, grantAmount = 800,
            dailyLimit = 5
        },
        new Item {
            id = "shop_talent_stone_3", name = "天赋石 ×3", kind = Kind.Resource,
            desc = "天赋树与技能解锁的通用货币。",
            currency = ResourceWallet.ResourceType.Diamond, price = 200,
            grantType = ResourceWallet.ResourceType.TalentPoint, grantAmount = 3,
            dailyLimit = 3
        },

        // ================= 材料 =================
        new Item {
            id = "shop_enchant_5", name = "强化石 ×5", kind = Kind.Material,
            desc = "装备强化材料。",
            currency = ResourceWallet.ResourceType.Gold, price = 600,
            grantType = ResourceWallet.ResourceType.EnchantStone, grantAmount = 5,
            dailyLimit = 5
        },
        new Item {
            id = "shop_decompose_10", name = "分解材料 ×10", kind = Kind.Material,
            desc = "装备分解产物，用于兑换与合成。",
            currency = ResourceWallet.ResourceType.Gold, price = 300,
            grantType = ResourceWallet.ResourceType.DecomposeMat, grantAmount = 10,
            dailyLimit = 5
        },
    };

    /// <summary>
    /// 全部商品 = 手写货架 + 佣兵货架。
    /// 佣兵不手写：价格与归属跟着 MercRosterDefs 走，避免两处各抄一份后漂移。
    /// </summary>
    public static readonly Item[] All = BuildAll();

    static Item[] BuildAll()
    {
        var list = new List<Item>(Fixed);
        var shopMercs = MercRosterDefs.ShopRoster;
        for (int i = 0; i < shopMercs.Count; i++)
            list.Add(MercItem(shopMercs[i]));
        return list.ToArray();
    }

    /// <summary>
    /// 佣兵商品。**商店只挂普通档，直接卖、不加门槛**（用户 2026-09-17 定：好货只走酒馆）。
    /// 稀有 / 传说留在酒馆名册，门槛见 <see cref="MercUnlockGate"/>。
    /// </summary>
    static Item MercItem(MercRosterDefs.Def def)
    {
        return new Item
        {
            id = "shop_merc_" + def.HireId,
            name = def.Name + "·" + def.Nickname,
            desc = MercDesc(def),
            kind = Kind.Merc,
            currency = ResourceWallet.ResourceType.Gold,
            price = MercRosterDefs.UnlockCost(def),
            mercId = def.HireId,
            showFromChapter = 0
        };
    }

    static string MercDesc(MercRosterDefs.Def def)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(def.JobName).Append("　HP ").Append(def.BaseHp)
          .Append(" / 攻 ").Append(def.BaseAtk).Append(" / 防 ").Append(def.BaseDef);

        string act = MercRosterDefs.SkillDisplayName(def.ActiveSkillId);
        string pas = MercRosterDefs.SkillDisplayName(def.PassiveSkillId);
        if (!string.IsNullOrEmpty(act)) sb.Append("\n主动：").Append(act);
        if (!string.IsNullOrEmpty(pas)) sb.Append("\n被动：").Append(pas);
        sb.Append("\n解锁后进入战斗内的「佣兵」三选一池。");
        return sb.ToString();
    }

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
            case Kind.Fragment: return "碎片";
            case Kind.Gacha: return "抽卡";
            case Kind.Resource: return "补给";
            case Kind.Merc: return "佣兵";
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

    /// <summary>标签页顺序（ShopUI 按这个排，索引与枚举值一一对应）。</summary>
    public static readonly Kind[] Kinds =
    {
        Kind.Skill, Kind.Fragment, Kind.Gacha, Kind.Resource, Kind.Material, Kind.Merc
    };
}
