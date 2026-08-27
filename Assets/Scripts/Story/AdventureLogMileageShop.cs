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
        Stamina,
        ScrollCommon,
        ScrollRare,
        ScrollLegendary
    }

    public struct ShopItem
    {
        public string Id;
        public string Name;
        public int CostPoints;
        public int WeeklyLimit;
        public RewardKind Kind;
        public int Amount;
    }

    public static readonly ShopItem[] Items =
    {
        new ShopItem { Id = "gold_bag", Name = "金币袋", CostPoints = 20, WeeklyLimit = 5, Kind = RewardKind.Gold, Amount = 500 },
        new ShopItem { Id = "mats_3", Name = "强化石×3", CostPoints = 25, WeeklyLimit = 5, Kind = RewardKind.Mats, Amount = 3 },
        new ShopItem { Id = "stamina_1", Name = "体力药", CostPoints = 15, WeeklyLimit = 3, Kind = RewardKind.Stamina, Amount = 1 },
        new ShopItem { Id = "scroll_c", Name = "普通招募卷", CostPoints = 30, WeeklyLimit = 3, Kind = RewardKind.ScrollCommon, Amount = 1 },
        new ShopItem { Id = "scroll_r", Name = "稀有招募卷", CostPoints = 80, WeeklyLimit = 2, Kind = RewardKind.ScrollRare, Amount = 1 },
        new ShopItem { Id = "scroll_l", Name = "传奇招募卷", CostPoints = 200, WeeklyLimit = 1, Kind = RewardKind.ScrollLegendary, Amount = 1 },
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
        }

        SaveSystem.Instance.Save();
        RedDot.RefreshCommon();
        msg = $"兑换成功：{item.Name}";
        if (item.Kind == RewardKind.ScrollCommon || item.Kind == RewardKind.ScrollRare || item.Kind == RewardKind.ScrollLegendary)
            msg += $"（卷轴库存 普{data.mercScrollCommon}/稀{data.mercScrollRare}/传{data.mercScrollLegendary}）";
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
}
