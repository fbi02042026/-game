using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 统一资源发放：默认上限 9999999；超出不进背包，提示并入邮件。
/// 某个资源可设特殊上限（如体力）。
/// </summary>
public static class ResourceWallet
{
    public const long DEFAULT_MAX = 9999999L;

    public enum ResourceType
    {
        Gold,
        Diamond,
        Stamina,
        EnchantStone,
        DecomposeMat,
        TalentPoint,
        /// <summary>
        /// 抽奖币（2026-09-29 新增）：只用于「进关抽奖」，与城镇金币完全分开。
        /// 战斗通关产出，抽奖消耗；金币该升级建筑还是升级建筑，两边不抢钱。
        /// </summary>
        SlotCoin,
        /// <summary>
        /// 佣兵币（2026-10-07 主人拍板新增）：只用于「佣兵」这一类的定向抽奖。
        /// 主人原话「定向招募改用特殊的金币 也可以叫佣兵币」，与 Gold / SlotCoin 三边不通用。
        /// 产出见 <see cref="GrantMercGold"/>：主人拍板「击杀精英1个 击杀Boss两个」。
        /// </summary>
        MercGold
    }

    /// <summary>
    /// 佣兵币唯一发放出口（铁律 15：不多入口）。定向招募的花费走
    /// <see cref="TrySpend(ResourceType, long, bool, bool)"/>，发放只走这里。
    /// </summary>
    public static AddResult GrantMercGold(long amount, string reason, bool notify = true)
    {
        if (amount <= 0) return new AddResult();
        // 埋点与钻石/金币分开统计，方便看「定向招募」这条线的产出够不够养活定向招募。
        Analytics.Track("merc_gold_gain", ("amount", amount), ("reason", reason ?? ""));
        return Add(ResourceType.MercGold, amount, save: true, notify: notify);
    }

    public struct AddResult
    {
        public long requested;
        public long added;
        public long overflow;
        public long current;
        public long max;
        public bool hitCap;
    }

    public static long GetMax(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Stamina:
                long baseMax = GameConfig.STAMINA_MAX;
                int bonus = SaveSystem.Instance?.Data?.staminaBonusMax ?? 0;
                return baseMax + Mathf.Max(0, bonus);
            default:
                return DEFAULT_MAX;
        }
    }

    public static long Get(SaveData data, ResourceType type)
    {
        if (data == null) return 0;
        switch (type)
        {
            case ResourceType.Gold: return data.totalGold;
            case ResourceType.Diamond: return data.diamond;
            case ResourceType.Stamina: return data.stamina;
            case ResourceType.EnchantStone: return data.enchantStones;
            case ResourceType.DecomposeMat: return data.decomposeMats;
            case ResourceType.TalentPoint: return data.talentPoints;
            case ResourceType.SlotCoin: return data.slotCoins;
            case ResourceType.MercGold: return data.mercGold;
            default: return 0;
        }
    }

    static void Set(SaveData data, ResourceType type, long value)
    {
        switch (type)
        {
            case ResourceType.Gold: data.totalGold = value; break;
            case ResourceType.Diamond: data.diamond = (int)Mathf.Clamp(value, 0, int.MaxValue); break;
            case ResourceType.Stamina: data.stamina = (int)Mathf.Clamp(value, 0, int.MaxValue); break;
            case ResourceType.EnchantStone: data.enchantStones = (int)Mathf.Clamp(value, 0, int.MaxValue); break;
            case ResourceType.DecomposeMat: data.decomposeMats = (int)Mathf.Clamp(value, 0, int.MaxValue); break;
            case ResourceType.TalentPoint: data.talentPoints = (int)Mathf.Clamp(value, 0, int.MaxValue); break;
            case ResourceType.SlotCoin: data.slotCoins = (int)Mathf.Clamp(value, 0, int.MaxValue); break;
            case ResourceType.MercGold: data.mercGold = (int)Mathf.Clamp(value, 0, int.MaxValue); break;
        }
    }

    public static string DisplayName(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Gold: return "金币";
            case ResourceType.Diamond: return "钻石";
            case ResourceType.Stamina: return "体力";
            case ResourceType.EnchantStone: return "强化石";
            case ResourceType.DecomposeMat: return "分解材料";
            // 2026-09-18 修正：这两行原为「附魔石 / 强化石」，与 ShopDefs.CurrencyName 正好对调，
            // 导致商店页写"强化石"、领取提示弹"附魔石"。现以 ShopDefs 为准统一。
            // 枚举名沿用 TalentPoint（存档字段 talentPoints），对外统一叫「天赋石」
            case ResourceType.TalentPoint: return "天赋石";
            case ResourceType.SlotCoin: return "抽奖币";
            case ResourceType.MercGold: return "佣兵币";
            default: return "资源";
        }
    }

    /// <summary>增加资源；满后溢出进邮件，并 Toast 提示。</summary>
    public static AddResult Add(ResourceType type, long amount, bool save = true, bool notify = true, bool overflowToMail = true)
    {
        var result = new AddResult { requested = amount };
        if (amount <= 0) return result;

        var saveSys = SaveSystem.Instance;
        SaveData data = saveSys != null ? saveSys.Data : null;
        if (data == null)
        {
            Debug.LogWarning("[ResourceWallet] SaveData 为空，无法发放 " + type);
            return result;
        }

        long max = GetMax(type);
        long cur = Get(data, type);
        long room = Math.Max(0L, max - cur);
        long add = Math.Min(amount, room);
        long overflow = amount - add;

        if (add > 0)
            Set(data, type, cur + add);

        result.added = add;
        result.overflow = overflow;
        result.current = Get(data, type);
        result.max = max;
        result.hitCap = overflow > 0 || result.current >= max;
        if (type == ResourceType.Diamond)
            Analytics.DiamondChange("gain", amount, Get(data, type)); // 埋点：钻石收入（不影响返回值）

        if (overflow > 0 && overflowToMail)
            MailSystem.EnqueueResourceOverflow(type, overflow);

        if (notify)
        {
            // 统一「获得」反馈：凡是显式要求 notify 的发放都弹一条，玩家不必去看顶部数字。
            // 战斗内逐次结算的发放走 notify:false，不会刷屏。
            if (add > 0)
                UIManager.Instance?.ShowToast($"获得 {DisplayName(type)} +{add}", true);   // 2026-10-06 主人拍板：资源获得 force 弹
            if (overflow > 0)
                UIManager.Instance?.ShowToast($"{DisplayName(type)}已达到最大值");
        }

        if (save && saveSys != null)
            saveSys.Save();

        GuildHallUI.RefreshAllHudStatic();
        return result;
    }

    /// <summary>消耗资源，不足返回 false。</summary>
    public static bool TrySpend(ResourceType type, long amount, bool save = true, bool notify = true)
    {
        if (amount <= 0) return true;
        var saveSys = SaveSystem.Instance;
        SaveData data = saveSys != null ? saveSys.Data : null;
        if (data == null) return false;

        long cur = Get(data, type);
        if (cur < amount)
        {
            if (notify)
                UIManager.Instance?.ShowToast($"{DisplayName(type)}不足");
            return false;
        }

        Set(data, type, cur - amount);
        if (type == ResourceType.Diamond)
            Analytics.DiamondChange("spend", amount, Get(data, type)); // 埋点：钻石支出（不影响返回值）
        if (save) saveSys.Save();
        GuildHallUI.RefreshAllHudStatic();
        return true;
    }
}
