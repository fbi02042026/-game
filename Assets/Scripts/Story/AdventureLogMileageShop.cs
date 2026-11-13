using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// 里程商店：用日志里程点数兑换资源/招募卷/职业徽记，周限购（计划三期）。
/// 不出装备（法杖/盾的初始武器走里程奖励「累计点数领取」，不是本商店的商品条目）。
/// 【2026-11-13 主人拍板「徽记分家 + 三档上架」】法系徽记**固定上架**：法师/牧师 ×
/// 低级20 / 中级10 / 高级2，每周补货一次（各自周限购 1 + 跨周 mileageShopBought 清零即「补」）；
/// 不再按周轮换单职业。游侠/剑盾徽记在竞技场商店，狂战/重武留后期。
/// </summary>
public static class AdventureLogMileageShop
{
    public enum RewardKind
    {
        Gold,
        Mats,
        Talent,
        Stamina,
        ScrollCommon,
        ScrollRare,
        ScrollLegendary,
        Item,
        /// <summary>好感礼物（2026-10-27 主人拍板：里程商店每日随机刷 1 件传奇礼物，限量 1，调 SaveData.AddFavorGift 发放）。</summary>
        Gift,
        /// <summary>职业徽记（2026-11-08 主人拍板：每周轮换 1 个职业的低级徽记，500点/10个/周限1）。</summary>
        Badge
    }

    public class ShopItem
    {
        public string Id;
        public string Name;
        public int CostPoints;
        public int WeeklyLimit;
        public RewardKind Kind;
        public int Amount;
        /// <summary>item_defs.csv 的道具 id（RewardKind.Item）；徽记 id badge:{职业}:{档位}（RewardKind.Badge）；其余留空。</summary>
        public string DefId;
    }

    public static readonly ShopItem[] Items =
    {
        // 【2026-10-20 主人拍板】里程商店价格 ×10
        new ShopItem { Id = "gold_bag", Name = "金币袋", CostPoints = 200, WeeklyLimit = 5, Kind = RewardKind.Gold, Amount = 50 },
        // 【2026-10-20 主人拍板】里程商店价格 ×10
        // 【2026-10-17 主人拍板】强化石暂无用途 → 原「强化石×3」改成「天赋石×3」（id 不动，周限购计数照旧）
        new ShopItem { Id = "mats_3", Name = "天赋石×3", CostPoints = 250, WeeklyLimit = 5, Kind = RewardKind.Talent, Amount = 3 },
        // 【2026-10-20 主人拍板】里程商店价格 ×10
        // 【2026-10-20 主人拍板】体力药改 20点=10体力（一次冒险的量），原 15点=1体力不够一次冒险基本无用
        new ShopItem { Id = "stamina_1", Name = "体力药", CostPoints = 200, WeeklyLimit = 3, Kind = RewardKind.Stamina, Amount = 10 },
        // 【2026-10-20 主人拍板】里程商店价格 ×10
        // 2026-11-01 名字精简配合货架 Label 收窄：括号说明删掉（面板顶部已有「卷轴库存 普通/高级」行，语义不丢）
        new ShopItem { Id = "scroll_c", Name = "普通招募卷轴", CostPoints = 300, WeeklyLimit = 3, Kind = RewardKind.ScrollCommon, Amount = 1 },
        // 【2026-10-20 主人拍板】里程商店价格 ×10
        // 【2026-10-17 主人拍板】卷轴改版：旧「稀有招募卷」失去用途下架（存量已在存档加载时折算进普通卷轴）；
        // 「传奇招募卷」语义升级为「高级招募卷轴」（招募传奇佣兵专用，id 不动，周限购计数照旧）；
        // 2026-11-01 名字精简配合货架 Label 收窄：括号说明删掉（同上，语义在面板顶部库存行）
        new ShopItem { Id = "scroll_l", Name = "高级招募卷轴", CostPoints = 2000, WeeklyLimit = 1, Kind = RewardKind.ScrollLegendary, Amount = 1 },
        // 【2026-10-21 主人拍板】遗忘药水：洗天赋消耗道具，500 点/周限 1；发放口 = townBagItems（角色页可见）
        new ShopItem { Id = "forget_potion", Name = "遗忘药水", CostPoints = 500, WeeklyLimit = 1, Kind = RewardKind.Item, Amount = 1, DefId = "item_forget_potion" },
        // 【2026-11-13 主人拍板】法系徽记三档固定上架（法师/牧师 × 低级20 / 中级10 / 高级2），
        // 每周补货一次 = 各自 WeeklyLimit 1 + 跨周 mileageShopBought 清零（EnsureWeek 既有机制）。
        // 价格沿原徽记档 500 点锚点分档（500/800/1000），主人没拍板价格，随时可调。
        // 🔴 仍恒放数组末尾：0~5 索引不变，存档 mileageShopBought 按 Id 计数不受影响。
        // 旧 badge_weekly（11-08 单条轮换）随本拍板退役：旧档残留计数无害（id 不在货架即不可再买）。
        MakeBadgeItem("法师", MercRosterDefs.MercRarity.Common,    "mage_low",  20, 500),
        MakeBadgeItem("法师", MercRosterDefs.MercRarity.Rare,      "mage_mid",  10, 800),
        MakeBadgeItem("法师", MercRosterDefs.MercRarity.Legendary, "mage_high",  2, 1000),
        MakeBadgeItem("牧师", MercRosterDefs.MercRarity.Common,    "priest_low",  20, 500),
        MakeBadgeItem("牧师", MercRosterDefs.MercRarity.Rare,      "priest_mid",  10, 800),
        MakeBadgeItem("牧师", MercRosterDefs.MercRarity.Legendary, "priest_high",  2, 1000),
    };

    // ── 法系徽记三档固定上架（2026-11-13 主人拍板，覆盖 11-08「每周轮换 1 个」）────────────────────────
    // 法师/牧师 × 低级20 / 中级10 / 高级2，六条固定货架、每周补货一次（各自周限购 1 + 跨周计数清零）。
    // 档位 = MercStarDefs.BadgeTierOf、显示名 = MercStarSystem.BadgeTierDisplayName、
    // 徽记 id = MercGrowInventory.BadgeId（badge:{职业}:{档位}，与徽记副本/每日登录/邮件同仓），均不在此抄一份。
    // ⚠ 6 职业全集真源仍是 MercGrowInventory.BadgeJobKeys（关卡掉落/徽记副本/福利邮件共用），这里只摆法系 2 职业。

    /// <summary>构造一条固定徽记商品（静态初始化用；不读存档、不写存档）。</summary>
    static ShopItem MakeBadgeItem(string jobKey, MercRosterDefs.MercRarity rarity, string idTail, int amount, int cost)
    {
        string tier = MercStarDefs.BadgeTierOf(rarity);
        return new ShopItem
        {
            Id = "badge_wk_" + idTail,
            Name = jobKey + "徽记（" + MercStarSystem.BadgeTierDisplayName(tier) + "）×" + amount,
            CostPoints = cost,
            WeeklyLimit = 1,
            Kind = RewardKind.Badge,
            Amount = amount,
            DefId = MercGrowInventory.BadgeId(jobKey, tier),
        };
    }

    /// <summary>
    /// 货架徽记商品 id（badge_wk_{mage|priest}_{low|mid|high}）→ 职业 key + 稀有度。
    /// 供面板图标 / 说明 / 名字颜色按 id 解析（AdventureLogPhase3Panel 用）；非徽记商品返回 false。
    /// </summary>
    public static bool TryParseBadgeItemId(string id, out string jobKey, out MercRosterDefs.MercRarity rarity)
    {
        switch (id)
        {
            case "badge_wk_mage_low":    jobKey = "法师"; rarity = MercRosterDefs.MercRarity.Common;    return true;
            case "badge_wk_mage_mid":    jobKey = "法师"; rarity = MercRosterDefs.MercRarity.Rare;      return true;
            case "badge_wk_mage_high":   jobKey = "法师"; rarity = MercRosterDefs.MercRarity.Legendary; return true;
            case "badge_wk_priest_low":  jobKey = "牧师"; rarity = MercRosterDefs.MercRarity.Common;    return true;
            case "badge_wk_priest_mid":  jobKey = "牧师"; rarity = MercRosterDefs.MercRarity.Rare;      return true;
            case "badge_wk_priest_high": jobKey = "牧师"; rarity = MercRosterDefs.MercRarity.Legendary; return true;
            default: jobKey = null; rarity = default; return false;
        }
    }

    public static string CurrentWeekKey()
    {
        // ISO 周：yyyy-Www
        var cal = CultureInfo.InvariantCulture.Calendar;
        var now = DateTime.UtcNow;
        int week = cal.GetWeekOfYear(now, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        return now.Year + "-W" + week.ToString("00");
    }

    public static void EnsureWeek()
    {
        // 2026-11-13 徽记改三档固定上架，原 RefreshWeeklyBadge 跨周刷新随轮换机制一并退役；
        // 「每周补货」由下方周 key 变化清空 mileageShopBought 实现（限购次数恢复即补货）。
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        string key = CurrentWeekKey();
        if (data.mileageShopWeekKey == key) return;
        data.mileageShopWeekKey = key;
        data.mileageShopBought = new Dictionary<string, int>();
        SaveSystem.Instance.Save();
    }

    public static int BoughtThisWeek(string itemId)
    {
        EnsureWeek();
        var dict = SaveSystem.Instance?.Data?.mileageShopBought;
        if (dict == null || string.IsNullOrEmpty(itemId)) return 0;
        return dict.TryGetValue(itemId, out int n) ? n : 0;
    }

    public static int RemainThisWeek(ShopItem item)
    {
        return Mathf.Max(0, item.WeeklyLimit - BoughtThisWeek(item.Id));
    }

    public static bool CanBuy(int index, out string reason)
    {
        reason = null;
        EnsureWeek();
        if (index < 0 || index >= Items.Length)
        {
            reason = "无效商品";
            return false;
        }
        var item = Items[index];
        if ((item.Kind == RewardKind.Item || item.Kind == RewardKind.Badge) && string.IsNullOrEmpty(item.DefId))
        {
            reason = "配置错误";
            return false;
        }
        if (RemainThisWeek(item) <= 0)
        {
            reason = "本周已达限购";
            return false;
        }
        if (AdventureLogMileage.Points < item.CostPoints)
        {
            reason = $"里程点数不足（需{item.CostPoints}）";
            return false;
        }
        return true;
    }

    public static bool TryBuy(int index, out string msg)
    {
        msg = null;
        if (!CanBuy(index, out string reason))
        {
            msg = reason;
            return false;
        }
        var data = SaveSystem.Instance?.Data;
        if (data == null)
        {
            msg = "存档未就绪";
            return false;
        }

        var item = Items[index];
        if (!AdventureLogMileage.TrySpendPoints(item.CostPoints))
        {
            msg = "里程点数不足";
            return false;
        }

        data.mileageShopBought ??= new Dictionary<string, int>();
        data.mileageShopBought.TryGetValue(item.Id, out int bought);
        data.mileageShopBought[item.Id] = bought + 1;

        switch (item.Kind)
        {
            case RewardKind.Gold:
                ResourceWallet.Add(ResourceWallet.ResourceType.Gold, item.Amount, save: false, notify: true);
                break;
            case RewardKind.Mats:
                ResourceWallet.Add(ResourceWallet.ResourceType.DecomposeMat, item.Amount, save: false, notify: true);
                break;
            case RewardKind.Talent:
                ResourceWallet.Add(ResourceWallet.ResourceType.TalentPoint, item.Amount, save: false, notify: true);
                break;
            case RewardKind.Stamina:
                ResourceWallet.Add(ResourceWallet.ResourceType.Stamina, item.Amount, save: false, notify: true);
                break;
            case RewardKind.ScrollCommon:
                data.mercScrollCommon += item.Amount;
                break;
            case RewardKind.ScrollRare:
                data.mercScrollRare += item.Amount;
                break;
            case RewardKind.ScrollLegendary:
                data.mercScrollLegendary += item.Amount;
                break;
            case RewardKind.Item:
                data.TownBagAdd(item.DefId, item.Amount);
                break;
            case RewardKind.Badge:
                // 徽记 id = badge:{职业}:{档位}，与徽记副本 / 每日登录 / 邮件附件同仓同出口
                MercGrowInventory.Add(item.DefId, item.Amount);
                break;
        }

        SaveSystem.Instance.Save();
        RedDot.RefreshCommon();
        msg = $"兑换成功：{item.Name}";
        if (item.Kind == RewardKind.ScrollCommon || item.Kind == RewardKind.ScrollLegendary)
            msg += $"（卷轴库存 普通{data.mercScrollCommon}/高级{data.mercScrollLegendary}）";
        if (item.Kind == RewardKind.Item)
            msg += $"（持有 {data.TownBagCount(item.DefId)}）";
        // 徽记存隐藏养成仓（背包不显示），toast 带持有数才看得见到账
        if (item.Kind == RewardKind.Badge)
            msg += $"（持有 {MercGrowInventory.Get(item.DefId)}）";
        return true;
    }

    public static bool HasAffordable()
    {
        EnsureWeek();
        int pts = AdventureLogMileage.Points;
        for (int i = 0; i < Items.Length; i++)
        {
            if (RemainThisWeek(Items[i]) > 0 && pts >= Items[i].CostPoints)
                return true;
        }
        return false;
    }

    public static string FormatItemLine(int index)
    {
        if (index < 0 || index >= Items.Length) return "";
        var item = Items[index];
        int left = RemainThisWeek(item);
        return $"{item.Name}  {item.CostPoints}点  本周余{left}/{item.WeeklyLimit}";
    }

    // ============================================================
    // 每日随机传奇礼物（2026-10-27 主人拍板「传奇礼物只在副本获得，也可以在里程商店里加上，每天随机出现 只有1个」）
    // 来源二：里程商店每日（本地日）随机刷 1 件传奇礼物，限量 1，用里程点数兑换（调 SaveData.AddFavorGift 发放）。
    // 与周限购商品（Items）完全独立：这里是「每日」计数（mileageDailyGiftBought + 日键），不走 WeeklyLimit / 周键。
    // ============================================================

    /// <summary>每日传奇礼物的里程点数价格（可调）。</summary>
    public const int DailyLegendaryCost = 800;

    /// <summary>今日随机传奇礼物（确定性：同一本地日恒定；跨天换一件）。无传奇礼物表项时返回 null。</summary>
    public static ShopItem GetDailyLegendaryGift()
    {
        var legendaries = System.Array.FindAll(MercFavorDefs.Gifts, g => MercFavorDefs.IsLegendary(g));
        if (legendaries.Length == 0) return null;
        string dayKey = DateTime.Now.ToString("yyyyMMdd");
        int hash = 0;
        foreach (char c in dayKey) hash = hash * 31 + c;
        int idx = System.Math.Abs(hash) % legendaries.Length;
        var g = legendaries[idx];
        return new ShopItem
        {
            Id = "mileage_daily_gift",
            Name = "每日传奇·" + g.Name,
            CostPoints = DailyLegendaryCost,
            WeeklyLimit = 1,
            Kind = RewardKind.Gift,
            Amount = 1,
            DefId = g.Id
        };
    }

    /// <summary>日键跨天清零每日兑换计数（照 EndlessRiftRules.EnsureDay 日键模式，本地日判定）。</summary>
    static void EnsureDayDaily(SaveData data)
    {
        if (data == null) return;
        string today = DateTime.Now.ToString("yyyyMMdd");
        if (data.mileageDailyGiftDayKey != today)
        {
            data.mileageDailyGiftDayKey = today;
            data.mileageDailyGiftBought = 0;
            SaveSystem.Instance?.Save();
        }
    }

    /// <summary>今日是否已兑换过每日传奇礼物。</summary>
    public static bool IsDailyLegendaryBoughtToday()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return false;
        EnsureDayDaily(data);
        return data.mileageDailyGiftBought > 0;
    }

    /// <summary>每日传奇礼物当前能否兑换（判据：有货 / 今日未兑 / 里程点数够）。</summary>
    public static bool CanBuyDailyLegendaryGift(out string reason)
    {
        reason = null;
        var data = SaveSystem.Instance?.Data;
        if (data == null) { reason = "存档未就绪"; return false; }
        if (GetDailyLegendaryGift() == null) { reason = "今日无传奇礼物"; return false; }
        EnsureDayDaily(data);
        if (data.mileageDailyGiftBought > 0) { reason = "今日已兑换"; return false; }
        if (AdventureLogMileage.Points < DailyLegendaryCost) { reason = $"里程点数不足（需{DailyLegendaryCost}）"; return false; }
        return true;
    }

    /// <summary>兑换每日传奇礼物：扣里程点数 → 发好感礼物（SaveData.AddFavorGift）→ 记今日已兑。成功返回 true + 文案。</summary>
    public static bool TryBuyDailyLegendaryGift(out string msg)
    {
        msg = null;
        if (!CanBuyDailyLegendaryGift(out string reason)) { msg = reason; return false; }
        var data = SaveSystem.Instance?.Data;
        if (data == null) { msg = "存档未就绪"; return false; }
        var gift = GetDailyLegendaryGift();
        if (!AdventureLogMileage.TrySpendPoints(DailyLegendaryCost)) { msg = "里程点数不足"; return false; }
        data.mileageDailyGiftBought = 1;
        data.AddFavorGift(gift.DefId, gift.Amount);
        SaveSystem.Instance?.Save();
        RedDot.RefreshCommon();
        msg = $"兑换成功：{gift.Name}";
        return true;
    }
}
