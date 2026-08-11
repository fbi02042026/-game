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
        TalentPoint
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
                return GameConfig.STAMINA_MAX;
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
        }
    }

    public static string DisplayName(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Gold: return "金币";
            case ResourceType.Diamond: return "钻石";
            case ResourceType.Stamina: return "体力";
            case ResourceType.EnchantStone: return "附魔石";
            case ResourceType.DecomposeMat: return "分解材料";
            case ResourceType.TalentPoint: return "天赋点";
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

        if (overflow > 0 && overflowToMail)
            MailSystem.EnqueueResourceOverflow(type, overflow);

        if (notify && overflow > 0)
            UIManager.Instance?.ShowToast($"{DisplayName(type)}已达到最大值");

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
        if (save) saveSys.Save();
        GuildHallUI.RefreshAllHudStatic();
        return true;
    }
}
