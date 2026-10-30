using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 竞技场商店（2026-10-30 主人拍板「积分版」，覆盖 v1 竞技币方案）：
/// · 唯一货币 = 竞技场积分（SaveData.arenaPoints），🔴 禁新增第二种货币字段；
///   扣分唯一出口 = <see cref="ArenaRules.TrySpendPoints"/>（守卫禁负数），扣积分不动换位链/排名；
/// · 货架 8 件（§三）：逐风长弓（买断）+ 莉娜/索尔/伊芙（首购本体、复购转本命碎片）+ 金币包 + 徽记包×3（周限）；
/// · 逐风长弓 = 运行时注册的稀有弓模板（照 <see cref="ArenaSeasonWeapons.BuildTemplate"/> 先例改 baseRarity=Rare），
///   不落 Resources 目录 → 掉落/抽奖池（Resources.LoadAll）永远扫不到，天然「竞技场独占」；
///   拥有判定真源 = SaveData.arenaShopBowId（购买记录），发放进随身仓库 legacyEquipPool（冠军武器邮件口径）；
/// · 佣兵首购走现有招募链（UnlockMerc 入三选一池 + 图鉴 + 获得演出）；已拥有复购 = 转本命碎片
///   （MercGrowInventory.ConvertDuplicateMerc，稀有×5 / 传奇×1），满星拦截（照酒馆口径，不扣分）；
/// · 发放失败不入账可重试（照里程商店口径）：先扣积分、再发放，发放失败当场退回积分并 LogError；
/// · 周限计数挂 SaveData（arenaShopWeekKey + arenaShopWeekBuyEntries），周 key 照 ShopDefs.TodayKey
///   口径记「周 key」（ISO 周、周一为起点），跨周自动清零恢复次数。
/// </summary>
public static class ArenaShopGoods
{
    public enum GoodKind { Bow, Merc, Gold, Badge }

    /// <summary>逐风长弓模板 id（运行时注册，Resources 里没有 → 掉落池不可见）。</summary>
    public const string BowId = "weapon_arena_shop_bow";
    /// <summary>逐风长弓外观对（现役游侠弓组合，不新增美术资源）。</summary>
    public const string BowArt = "Bow_1";

    /// <summary>货架商品定义（价格单位：积分）。</summary>
    public class Good
    {
        public string id;
        public GoodKind kind;
        public string name;             // 货架名/弹窗标题兜底（佣兵商品以花名册解析为准）
        public string desc;             // 效果说明（弹窗正文用）
        public int price;               // 首购价
        public int repurchasePrice;     // 复购价（0 = 买断/不可复购）
        public int weeklyLimit;         // 每周限购次数（0 = 不限）
        public string hireId;           // 佣兵货填 H 编号
        public int grantGold;           // 金币包填发放量
        public string badgeJobKey;      // 徽记包职业 key（MercGrowSprites.JobKey 同套字符串）
        public string badgeTier;        // 徽记包档位（普通/稀有/传奇；中级=稀有、高级=传奇）
        public int badgeCount;          // 徽记包个数
        public MercRosterDefs.MercRarity frameRarity = MercRosterDefs.MercRarity.Common;   // 图标底框稀有度

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(hireId) && MercRosterDefs.TryGetByHireId(hireId, out var d))
                    return d.Name + "·" + d.Nickname;
                return name;
            }
        }

        /// <summary>确认弹窗正文：价格 / 效果 / 已拥有说明（标题 = 商品名，走 StaminaConfirmPopup 通用弹窗）。</summary>
        public string BuildConfirmBody(SaveData data)
        {
            int p = ArenaShopGoods.PriceOf(data, this);
            bool owned = ArenaShopGoods.IsOwned(data, this);
            var sb = new System.Text.StringBuilder();
            switch (kind)
            {
                case GoodKind.Bow:
                    sb.AppendLine("游侠限定主手 · 稀有");
                    sb.AppendLine($"价格 {p} 积分 · 买断制");
                    sb.AppendLine("获得时随机附 1 条词条，出战游侠自动替换初始主手。");
                    sb.Append(owned ? "已拥有 · 买断制，不能再买第二把。" : "风一样的箭，一次买断终身保箭。");
                    break;
                case GoodKind.Merc:
                    sb.AppendLine(owned ? "已拥有 · 再次购买转本命碎片" : "首购获得佣兵本体");
                    sb.AppendLine($"价格 {p} 积分"
                        + (repurchasePrice > 0 ? $"（复购 {repurchasePrice}/次 → 本命碎片 ×{MercStarDefs.DupFragCount(frameRarity)}）" : ""));
                    sb.Append(desc);
                    break;
                case GoodKind.Gold:
                    sb.AppendLine($"金币 ×{grantGold}，直接到账。");
                    sb.Append($"价格 {p} 积分 · 每周限 {weeklyLimit} 次。小钱钱到手，安全感拉满。");
                    break;
                case GoodKind.Badge:
                    sb.AppendLine($"{badgeJobKey}徽记（{badgeTier}档）×{badgeCount}。");
                    sb.Append($"价格 {p} 积分 · 每周限 {weeklyLimit} 次。职业固定不迷路，买来就给限定佣兵升星。");
                    break;
            }
            return sb.ToString();
        }
    }

    /// <summary>货架 8 件（§三 定稿：1 弓 + 3 佣兵 + 1 金币包 + 3 徽记包；徽记职业固定跟随限定佣兵）。</summary>
    public static readonly Good[] AllGoods =
    {
        new Good { id = "arena_bow", kind = GoodKind.Bow, name = "逐风长弓",
            desc = "游侠限定，风里来箭里去。",
            price = 2500, repurchasePrice = 0, frameRarity = MercRosterDefs.MercRarity.Rare },
        new Good { id = "arena_merc_h017", kind = GoodKind.Merc, name = "莉娜", hireId = "H017",
            desc = "圣光牧师，奶量管够，安全感的具象化。",
            price = 5000, repurchasePrice = 1000, frameRarity = MercRosterDefs.MercRarity.Rare },
        new Good { id = "arena_merc_h022", kind = GoodKind.Merc, name = "索尔", hireId = "H022",
            desc = "铁面重武者，能抗能打，人形城墙本墙。",
            price = 5000, repurchasePrice = 1000, frameRarity = MercRosterDefs.MercRarity.Rare },
        new Good { id = "arena_merc_h014", kind = GoodKind.Merc, name = "伊芙", hireId = "H014",
            desc = "火舞火法，一切都会燃烧，包括敌人的血条。",
            price = 12000, repurchasePrice = 4000, frameRarity = MercRosterDefs.MercRarity.Legendary },
        new Good { id = "arena_gold_2000", kind = GoodKind.Gold, name = "金币包",
            desc = "", price = 150, weeklyLimit = 5, grantGold = 2000,
            frameRarity = MercRosterDefs.MercRarity.Common },
        new Good { id = "arena_badge_priest_5", kind = GoodKind.Badge, name = "牧师徽记（中级）",
            desc = "", price = 250, weeklyLimit = 2,
            badgeJobKey = "牧师", badgeTier = "稀有", badgeCount = 5,
            frameRarity = MercRosterDefs.MercRarity.Rare },
        new Good { id = "arena_badge_heavy_5", kind = GoodKind.Badge, name = "重武徽记（中级）",
            desc = "", price = 250, weeklyLimit = 2,
            badgeJobKey = "重武", badgeTier = "稀有", badgeCount = 5,
            frameRarity = MercRosterDefs.MercRarity.Rare },
        new Good { id = "arena_badge_mage_1", kind = GoodKind.Badge, name = "法师徽记（高级）",
            desc = "", price = 300, weeklyLimit = 2,
            badgeJobKey = "法师", badgeTier = "传奇", badgeCount = 1,
            frameRarity = MercRosterDefs.MercRarity.Legendary },
    };

    static Good Find(string goodId)
    {
        for (int i = 0; i < AllGoods.Length; i++)
            if (AllGoods[i].id == goodId) return AllGoods[i];
        return null;
    }

    // ───────────────────────── 逐风长弓 · 运行时注册（ArenaSeasonWeapons 先例） ─────────────────────────

    static EquipTemplate _bowTpl;

    /// <summary>注册逐风长弓模板进 ConfigManager（幂等；ArenaRules.EnsurePeriods 调）。
    /// baseRarity=Rare：主攻击由 equip_attr_ranges 稀有档表 roll（ATK 14~20 档），掉落/抽奖池扫不到此 id。</summary>
    public static void EnsureRegistered()
    {
        if (ConfigManager.Instance == null) return;
        if (ConfigManager.Instance.GetEquipTemplate(BowId) != null) return;
        _bowTpl = ScriptableObject.CreateInstance<EquipTemplate>();
        _bowTpl.templateId = BowId;
        _bowTpl.equipName = "逐风长弓";
        _bowTpl.iconFileName = BowArt;     // ResolveIcon 按 EquipIcons 文件名加载（冠军武器同款口径）
        _bowTpl.gridWidth = 2;             // 双手 2×3（照 ArenaSeasonWeapons.ApplyDefaultGridSize 口径）
        _bowTpl.gridHeight = 3;
        _bowTpl.baseRarity = Rarity.Rare;  // 【2026-10-30 拍板】商店限定 = 稀有档
        _bowTpl.minLevel = 1;
        _bowTpl.slotType = EquipSlotType.MainHand;
        _bowTpl.weaponType = WeaponType.TwoHand;
        _bowTpl.weaponHand = WeaponHandSlot.MainHand;
        _bowTpl.weaponAttackType = WeaponAttackType.Physical;
        _bowTpl.attackRange = 96f;
        _bowTpl.weaponKindOverride = (int)WeaponCombatTable.WeaponKind.Bow;
        _bowTpl.spumName = BowArt;         // 外观三者同款：手上贴图 = 图标同一组
        _bowTpl.baseAttr = new List<AttrBonusData>
        {
            // 属性对 equip_attr_ranges 稀有档武器区间（ATK 14~20）；主攻击条目生成时由表按稀有度重掷覆盖
            new AttrBonusData { attrType = AttrType.Attack, value = 16f, isPercent = false },
            new AttrBonusData { attrType = AttrType.MaxHp, value = 60f, isPercent = false },
        };
        ConfigManager.Instance.RegisterRuntimeEquip(_bowTpl);
    }

    /// <summary>逐风长弓模板（EnsureRegistered 后取；缺失 LogError 不兜底）。</summary>
    public static EquipTemplate BowTemplate()
    {
        EnsureRegistered();
        var tpl = ConfigManager.Instance != null ? ConfigManager.Instance.GetEquipTemplate(BowId) : null;
        if (tpl == null)
            Debug.LogError("[ArenaShop] 逐风长弓模板未注册（ConfigManager 未就绪？）");
        return tpl;
    }

    /// <summary>弓图标（货架/弹窗用；缺图 LogError 不静默）。</summary>
    public static Sprite BowIcon()
    {
        var tpl = BowTemplate();
        if (tpl == null) return null;
        tpl.ResolveIcon();
        if (tpl.icon == null) Debug.LogError("[ArenaShop] 逐风长弓图标缺失：" + BowId);
        return tpl.icon;
    }

    /// <summary>是否已购弓（真源 SaveData.arenaShopBowId 购买记录；发放本体进 legacyEquipPool）。</summary>
    public static bool HasBowOwned(SaveData data)
        => data != null && data.arenaShopBowId == BowId;

    /// <summary>逐风长弓特性池：获得时随机附带其中 1 条（暮火之杖特性池先例 SpecialWeapons）；
    /// 出战生成实例时重掷（同「每局开局重发」口径）。数值按 星级×稀有度系数 缩放（与 GenerateFromTemplate 同款公式）。</summary>
    static readonly AttrBonusData[] BowTraitPool =
    {
        new AttrBonusData { attrType = AttrType.AttackSpeed, value = 0.10f, isPercent = true },
        new AttrBonusData { attrType = AttrType.CritRate, value = 0.05f, isPercent = true },
        new AttrBonusData { attrType = AttrType.Attack, value = 6f, isPercent = false },
        new AttrBonusData { attrType = AttrType.MaxHp, value = 40f, isPercent = false },
    };

    /// <summary>生成出战用的稀有品质实例（照 ArenaSeasonWeapons.CreateBattleInstance 钳制口径 + 随机 1 词条）。</summary>
    public static EquipInstance CreateBowBattleInstance(EquipTemplate tpl, WeaponCombatTable.WeaponKind jobKind, int heroLevel)
    {
        if (tpl == null) return null;
        var inst = EquipInstance.GenerateFromTemplate(tpl, 0, Mathf.Max(1, heroLevel), true, Rarity.Rare);
        inst.requireLevel = Mathf.Min(inst.requireLevel, Mathf.Max(1, heroLevel));
        if (inst.weaponHand == WeaponHandSlot.None) inst.weaponHand = WeaponHandSlot.MainHand;
        inst.slotType = EquipSlotType.MainHand;
        inst.weaponKindOverride = (int)jobKind;   // 与职业起步武器同口径：写死逻辑类型
        // 获得时随机 1 词条（暮火之杖先例：数值乘 星级系数×稀有度系数）
        var trait = BowTraitPool[Random.Range(0, BowTraitPool.Length)];
        float mul = (1 + inst.star * 0.1f) * EquipDropRules.RarityBaseMul(inst.rarity);
        inst.attrBonus.Add(new AttrBonusData
        {
            attrType = trait.attrType,
            value = trait.value * mul,
            isPercent = trait.isPercent,
        });
        int unlocked = GameConfig.GetUnlockedBackpackRows(SaveSystem.Instance?.Data);
        if (inst.gridHeight > unlocked) inst.gridHeight = Mathf.Max(1, unlocked);
        if (inst.gridWidth > GameConfig.BACKPACK_WIDTH) inst.gridWidth = GameConfig.BACKPACK_WIDTH;
        return inst;
    }

    // ───────────────────────── 购买判定 / 发放链 ─────────────────────────

    /// <summary>该商品当前是否「已拥有」（弓=买断变灰；佣兵=已拥有后复购转碎片）。</summary>
    public static bool IsOwned(SaveData data, Good good)
    {
        if (good == null || data == null) return false;
        if (good.kind == GoodKind.Bow) return HasBowOwned(data);
        if (!string.IsNullOrEmpty(good.hireId)) return data.IsMercUnlocked(good.hireId);
        return false;
    }

    /// <summary>当前应付价：已拥有且可复购 → 复购价，否则首购价。</summary>
    public static int PriceOf(SaveData data, Good good)
        => good != null && IsOwned(data, good) && good.repurchasePrice > 0 ? good.repurchasePrice : good?.price ?? 0;

    /// <summary>周 key（照 ShopDefs.TodayKey 口径记「周 key」：ISO 周、周一为一周起点，跨周自动恢复次数）。</summary>
    public static string CurrentWeekKey()
    {
        var now = System.DateTime.Now;
        int wk = System.Globalization.CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
            now, System.Globalization.CalendarWeekRule.FirstFourDayWeek, System.DayOfWeek.Monday);
        return $"{now.Year}-W{wk:00}";
    }

    /// <summary>周限计数对周（幂等）：周 key 变了就清零计数（跨周恢复次数）。</summary>
    public static void EnsureWeek(SaveData data)
    {
        if (data == null) return;
        string wk = CurrentWeekKey();
        if (data.arenaShopWeekKey == wk && data.arenaShopWeekBuyEntries != null) return;
        data.arenaShopWeekKey = wk;
        data.arenaShopWeekBuyEntries ??= new List<StringIntEntry>();
        data.arenaShopWeekBuyEntries.Clear();
    }

    /// <summary>本周已购次数（周限商品用）。</summary>
    public static int WeekBuyCount(SaveData data, string goodId)
    {
        if (data?.arenaShopWeekBuyEntries == null || string.IsNullOrEmpty(goodId)) return 0;
        var list = data.arenaShopWeekBuyEntries;
        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i];
            if (e != null && e.id == goodId) return e.value;
        }
        return 0;
    }

    static void WeekBuyAdd(SaveData data, string goodId)
    {
        if (data == null || string.IsNullOrEmpty(goodId)) return;
        data.arenaShopWeekBuyEntries ??= new List<StringIntEntry>();
        var list = data.arenaShopWeekBuyEntries;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && list[i].id == goodId)
            {
                list[i].value++;
                return;
            }
        }
        list.Add(new StringIntEntry { id = goodId, value = 1 });
    }

    /// <summary>
    /// 购买执行（货架点击确认后的唯一入口）：
    /// 校验（买断/满星/周限/余额）→ 扣积分（ArenaRules.TrySpendPoints，守卫禁负数、不动排名）
    /// → 按货架发放（弓=GrantEquip 进 legacyEquipPool；佣兵首购=UnlockMerc 入册、复购=转本命碎片；
    ///   金币=ResourceWallet；徽记=MercGrowInventory）→ 落盘。
    /// 发放失败退回积分不落账，可重试（照里程商店口径）。msg 带结果文案给调用方 toast。
    /// </summary>
    public static bool TryBuy(string goodId, out string msg)
    {
        msg = null;
        var data = SaveSystem.Instance?.Data;
        if (data == null) { msg = "存档未就绪"; return false; }
        var good = Find(goodId);
        if (good == null)
        {
            Debug.LogError($"[ArenaShop] 未知商品 {goodId}（货架定义见 AllGoods）");
            msg = "商品不存在";
            return false;
        }
        EnsureWeek(data);

        if (good.kind == GoodKind.Bow && HasBowOwned(data)) { msg = "已经买过啦（买断制）"; return false; }
        if (good.weeklyLimit > 0 && WeekBuyCount(data, good.id) >= good.weeklyLimit)
        {
            msg = "本周限购已用完，下周再来";
            return false;
        }

        MercRosterDefs.Def mercDef = default;
        bool owned = IsOwned(data, good);
        if (!string.IsNullOrEmpty(good.hireId))
        {
            if (!MercRosterDefs.TryGetByHireId(good.hireId, out mercDef))
            {
                Debug.LogError($"[ArenaShop] 商品 {good.id} 的佣兵 {good.hireId} 在花名册查不到");
                msg = "佣兵数据缺失";
                return false;
            }
            // 满星复购拦截（照酒馆口径）：碎片攒够满星总消耗后不再收钱（按钮不变灰，点了给说法）
            if (owned && MercStarSystem.OutStarOf(good.hireId) >= MercStarDefs.MaxOutStar)
            {
                msg = $"{mercDef.Name} 已满星，先拿碎片去升星吧";
                return false;
            }
        }

        int price = PriceOf(data, good);
        if (data.arenaPoints < price || !ArenaRules.TrySpendPoints(data, price))
        {
            msg = "积分不足";
            return false;
        }

        bool granted;
        if (good.kind == GoodKind.Bow)
        {
            // 弓进随身仓库（legacyEquipPool，冠军武器邮件附件领取同口径）；rarity 0~4 档：2=稀有
            granted = ItemGrantSystem.GrantEquip(BowId, 2);
            if (granted) data.arenaShopBowId = BowId;
            msg = "「逐风长弓」到手！游侠出战自动换装";
        }
        else if (good.kind == GoodKind.Merc)
        {
            if (owned)
            {
                // 复购 = 本命碎片（唯一发碎片入口 MercGrowInventory.GrantFragments，进战斗外背包）
                var frag = MercGrowInventory.ConvertDuplicateMerc(mercDef);
                granted = frag.Total > 0;
                msg = granted ? $"获得 {mercDef.Name} 的本命碎片 ×{MercStarDefs.DupFragCount(mercDef.Rarity)}" : null;
            }
            else
            {
                // 首购 = 现有招募入队链（解锁进三选一池 + 图鉴 + 获得演出，照 TavernUnlockUI.TryUnlock 口径）
                data.UnlockMerc(good.hireId);
                AdventureCodex.MarkMercSeen(good.hireId);
                MercGainCinematicUI.Show(good.hireId, null);
                granted = true;
                msg = $"{mercDef.Name} 已入册，三选一池见！";
            }
        }
        else if (good.kind == GoodKind.Gold)
        {
            // 金币包走 ResourceWallet（notify:false 防双 toast，结果文案由调用方统一弹）
            ResourceWallet.Add(ResourceWallet.ResourceType.Gold, good.grantGold, save: false, notify: false);
            granted = true;
            msg = $"金币 +{good.grantGold} 到账";
        }
        else
        {
            // 徽记包走 MercGrowInventory（badge:{职业key}:{档位} 口径，id 与关卡掉落同仓）
            MercGrowInventory.Add(MercGrowInventory.BadgeId(good.badgeJobKey, good.badgeTier), good.badgeCount);
            granted = true;
            msg = $"获得 {good.badgeJobKey}徽记（{good.badgeTier}）×{good.badgeCount}";
        }

        if (!granted)
        {
            // 发放失败不入账可重试（里程商店口径）：退回积分，LogError 不静默
            data.arenaPoints += price;
            SaveSystem.Instance?.Save();
            Debug.LogError($"[ArenaShop] 商品 {good.id} 发放失败，积分 {price} 已退回（可重试）");
            msg = "发放失败，积分已退回，请重试";
            return false;
        }

        if (good.weeklyLimit > 0) WeekBuyAdd(data, good.id);
        SaveSystem.Instance?.Save();
        return true;
    }

    /// <summary>赛季末提醒：距离月底清零还剩几天（§四.5：最后 3 天挂横幅）。</summary>
    public static int DaysBeforeSeasonClear()
    {
        var now = System.DateTime.Now;
        return System.DateTime.DaysInMonth(now.Year, now.Month) - now.Day;
    }
}
