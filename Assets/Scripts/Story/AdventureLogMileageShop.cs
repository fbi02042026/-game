using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// 里程商店：用日志里程点数兑换资源/招募卷，周限购（计划三期）。
/// 不出装备。
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
        Gift
    }

    public class ShopItem
    {
        public string Id;
        public string Name;
        public int CostPoints;
        public int WeeklyLimit;
        public RewardKind Kind;
        public int Amount;
        /// <summary>item_defs.csv 的道具 id（仅 RewardKind.Item 用；非 Item 类商品留空）。</summary>
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
        new ShopItem { Id = "scroll_c", Name = "普通卷轴（招募稀有佣兵）", CostPoints = 300, WeeklyLimit = 3, Kind = RewardKind.ScrollCommon, Amount = 1 },
        // 【2026-10-20 主人拍板】里程商店价格 ×10
        // 【2026-10-17 主人拍板】卷轴改版：旧「稀有招募卷」失去用途下架（存量已在存档加载时折算进普通卷轴）；
        // 「传奇招募卷」语义升级为「高级招募卷轴」（招募传奇佣兵专用，id 不动，周限购计数照旧）
        new ShopItem { Id = "scroll_l", Name = "高级招募卷轴（招募传奇佣兵）", CostPoints = 2000, WeeklyLimit = 1, Kind = RewardKind.ScrollLegendary, Amount = 1 },
        // 【2026-10-21 主人拍板】遗忘药水：洗天赋消耗道具，500 点/周限 1；发放口 = townBagItems（角色页可见）
        new ShopItem { Id = "forget_potion", Name = "遗忘药水", CostPoints = 500, WeeklyLimit = 1, Kind = RewardKind.Item, Amount = 1, DefId = "item_forget_potion" },
    };

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
        if (item.Kind == RewardKind.Item && string.IsNullOrEmpty(item.DefId))
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
        }

        SaveSystem.Instance.Save();
        RedDot.RefreshCommon();
        msg = $"兑换成功：{item.Name}";
        if (item.Kind == RewardKind.ScrollCommon || item.Kind == RewardKind.ScrollLegendary)
            msg += $"（卷轴库存 普通{data.mercScrollCommon}/高级{data.mercScrollLegendary}）";
        if (item.Kind == RewardKind.Item)
            msg += $"（持有 {data.TownBagCount(item.DefId)}）";
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
