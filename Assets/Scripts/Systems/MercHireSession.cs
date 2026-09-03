using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 本局雇佣的佣兵（下本结束离队）。图鉴见 AdventureCodex / seenMerc。
/// </summary>
public static class MercHireSession
{
    public const int RefreshCooldownSeconds = 30 * 60;

    public static List<MercenaryData> GetHired()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return new List<MercenaryData>();
        data.hiredMercs ??= new List<MercenaryData>();
        return data.hiredMercs;
    }

    public static int HiredCount()
    {
        var list = GetHired();
        int n = 0;
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null && !string.IsNullOrEmpty(list[i].mercId)) n++;
        return n;
    }

    public static bool CanHireMore()
    {
        int max = MercenaryManager.Instance != null
            ? MercenaryManager.Instance.GetMaxMercSlots()
            : Mathf.Clamp(SaveSystem.Instance?.Data?.townLevel?.tavern ?? 0, 0, 2);
        if (max < 1) max = 1; // 酒馆至少允许招 1 人进临时队
        return HiredCount() < max;
    }

    public static void AddHired(MercenaryData m)
    {
        if (m == null) return;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        data.hiredMercs ??= new List<MercenaryData>();
        data.hiredMercs.Add(m);
        if (data.townLevel == null) data.townLevel = new TownLevel();
        if (data.townLevel.tavern < 1) data.townLevel.tavern = 1;
        AdventureCodex.MarkMercSeen(m.mercId);
        AdventureLogAchievements.OnMercRecruited();
        SaveSystem.Instance.Save();
    }

    /// <summary>下本结束回城：记下上局出战 hireId 后清空临时雇佣。</summary>
    public static void ClearHired(bool save = true)
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        data.lastRunMercHireIds ??= new List<string>();
        data.lastRunMercHireIds.Clear();
        if (data.hiredMercs != null)
        {
            for (int i = 0; i < data.hiredMercs.Count; i++)
            {
                var m = data.hiredMercs[i];
                if (m == null) continue;
                string id = !string.IsNullOrEmpty(m.hireId) ? m.hireId : m.mercId;
                if (string.IsNullOrEmpty(id)) continue;
                if (!data.lastRunMercHireIds.Contains(id))
                    data.lastRunMercHireIds.Add(id);
            }
            data.hiredMercs.Clear();
        }
        if (save) SaveSystem.Instance.Save();
    }

    public static bool WasInLastRun(string hireIdOrMercId)
    {
        if (string.IsNullOrEmpty(hireIdOrMercId)) return false;
        var data = SaveSystem.Instance?.Data;
        var list = data?.lastRunMercHireIds;
        if (list == null) return false;
        return list.Contains(hireIdOrMercId);
    }

    public static bool IsAlreadyHired(MercenaryData offer)
    {
        if (offer == null) return false;
        var list = GetHired();
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null) continue;
            if (!string.IsNullOrEmpty(offer.hireId) && m.hireId == offer.hireId)
                return true;
            if (!string.IsNullOrEmpty(offer.mercId) && m.mercId == offer.mercId
                && (string.IsNullOrEmpty(offer.hireId) || string.IsNullOrEmpty(m.hireId)))
                return true;
        }
        return false;
    }

    public static int GoldCost(MercenaryData offer)
    {
        if (offer == null) return 500;
        if (MercRosterDefs.TryGetByAssetId(offer.mercId, out var def) && def.RecruitGold > 0)
            return def.RecruitGold;
        var rarity = MercSkillMapping.StarToRarity(offer.star);
        if (rarity == MercRosterDefs.MercRarity.Legendary) return 5000;
        if (rarity == MercRosterDefs.MercRarity.Rare) return 1500;
        return 500;
    }

    public static MercRosterDefs.MercRarity OfferRarity(MercenaryData offer)
    {
        if (offer == null) return MercRosterDefs.MercRarity.Common;
        return MercSkillMapping.StarToRarity(offer.star);
    }

    public static bool HasScrollFor(MercRosterDefs.MercRarity rarity)
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return false;
        if (rarity == MercRosterDefs.MercRarity.Legendary) return data.mercScrollLegendary > 0;
        if (rarity == MercRosterDefs.MercRarity.Rare) return data.mercScrollRare > 0;
        return false;
    }

    public static bool TrySpendScroll(MercRosterDefs.MercRarity rarity)
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return false;
        if (rarity == MercRosterDefs.MercRarity.Legendary)
        {
            if (data.mercScrollLegendary <= 0) return false;
            data.mercScrollLegendary--;
            return true;
        }
        if (rarity == MercRosterDefs.MercRarity.Rare)
        {
            if (data.mercScrollRare <= 0) return false;
            data.mercScrollRare--;
            return true;
        }
        return false;
    }

    public static void EnsureDailyOfferRefresh()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        // 本地日历日 0 点
        string today = DateTime.Now.ToString("yyyyMMdd");
        if (data.mercOfferDayKey == today) return;
        data.mercOfferDayKey = today;
        data.mercOfferRefreshUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        data.mercOfferDirty = true;
    }

    public static bool CanManualRefresh(out int remainSec)
    {
        remainSec = 0;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return true;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long last = data.mercOfferRefreshUtc;
        long elapsed = now - last;
        if (last <= 0 || elapsed >= RefreshCooldownSeconds) return true;
        remainSec = (int)(RefreshCooldownSeconds - elapsed);
        return false;
    }

    public static void MarkRefreshed()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        data.mercOfferRefreshUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        data.mercOfferDayKey = DateTime.Now.ToString("yyyyMMdd");
        data.mercOfferDirty = false;
        SaveSystem.Instance.Save();
    }

    public static Sprite LoadJobIcon(string jobName)
    {
        string file = JobIconFile(jobName);
        string path = "Icons/Job/" + file;
        var sp = Resources.Load<Sprite>(path);
        if (sp != null) return sp;
        var all = Resources.LoadAll<Sprite>(path);
        return all != null && all.Length > 0 ? all[0] : null;
    }

    public static string JobIconFile(string jobName)
    {
        if (string.IsNullOrEmpty(jobName)) return "物攻";
        if (jobName.Contains("盾") || jobName.Contains("卫") || jobName.Contains("重武") || jobName.Contains("防御"))
            return "防御";
        if (jobName.Contains("牧") || jobName.Contains("恢复") || jobName.Contains("圣"))
            return "恢复";
        if (jobName.Contains("法") || jobName.Contains("术") || jobName.Contains("水系") || jobName.Contains("雷系") || jobName.Contains("火系"))
            return "法术";
        return "物攻";
    }

    public static Material LoadScrollButtonMaterial(MercRosterDefs.MercRarity rarity)
    {
        if (rarity == MercRosterDefs.MercRarity.Legendary)
            return Resources.Load<Material>("Materials/btn10_chuanqi");
        if (rarity == MercRosterDefs.MercRarity.Rare)
            return Resources.Load<Material>("Materials/btn09_xiyou");
        return null;
    }

    public static Sprite LoadRarityFrame(MercRosterDefs.MercRarity rarity)
    {
        string name = rarity == MercRosterDefs.MercRarity.Legendary ? "frame_legendary"
            : rarity == MercRosterDefs.MercRarity.Rare ? "frame_rare" : "frame_common";
        var sp = Resources.Load<Sprite>("UI/Recruit/" + name);
        if (sp != null) return sp;
        var all = Resources.LoadAll<Sprite>("UI/Recruit/" + name);
        return all != null && all.Length > 0 ? all[0] : null;
    }
}
