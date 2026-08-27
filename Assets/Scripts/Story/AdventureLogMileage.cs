using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 冒险日志里程：收集解锁发点 → Lv1–6 一次性等级奖。
/// 与战斗 AchievementSystem 成就点数分离；成就完成时折算进本系统。
/// </summary>
public static class AdventureLogMileage
{
    public const int MaxLevel = 6;

    /// <summary>各等级所需累计点数（Lv1=0）。</summary>
    public static readonly int[] LevelThresholds = { 0, 100, 300, 600, 1000, 1500 };

    public struct LevelReward
    {
        public int Gold;
        public int Diamond;
        public int EnchantStone;
        public int TalentPoint;
        public string TitleId;
        public string Label;
    }

    /// <summary>Lv2–6 可领；Lv1 为起点无奖。</summary>
    public static readonly LevelReward[] LevelRewards =
    {
        new LevelReward { Label = "见习记载", Gold = 0 },
        new LevelReward { Label = "森林行者", Gold = 500, Diamond = 1 },
        new LevelReward { Label = "探索者", Gold = 1000, EnchantStone = 3 },
        new LevelReward { Label = "裂缝行者", Gold = 2000, Diamond = 3 },
        new LevelReward { Label = "日志学者", Gold = 3000, TalentPoint = 2 },
        new LevelReward { Label = "传说记录者", Gold = 5000, Diamond = 5, TalentPoint = 3, TitleId = "log_chronicler" }
    };

    public static int Points => SaveSystem.Instance?.Data?.logMileagePoints ?? 0;

    public static int GetLevel()
    {
        int pts = Points;
        int lv = 1;
        for (int i = 1; i < LevelThresholds.Length; i++)
        {
            if (pts >= LevelThresholds[i])
                lv = i + 1;
            else
                break;
        }
        return Mathf.Clamp(lv, 1, MaxLevel);
    }

    public static int NextThreshold()
    {
        int lv = GetLevel();
        if (lv >= MaxLevel) return LevelThresholds[MaxLevel - 1];
        return LevelThresholds[lv];
    }

    public static int PointsIntoCurrentSpan(out int spanNeed)
    {
        int pts = Points;
        int lv = GetLevel();
        if (lv >= MaxLevel)
        {
            spanNeed = 0;
            return 0;
        }
        int lo = LevelThresholds[lv - 1];
        int hi = LevelThresholds[lv];
        spanNeed = hi - lo;
        return Mathf.Clamp(pts - lo, 0, spanNeed);
    }

    public static bool IsLevelClaimed(int level)
    {
        var set = SaveSystem.Instance?.Data?.claimedLogMileageLevels;
        return set != null && set.Contains(level);
    }

    public static bool CanClaimLevel(int level)
    {
        if (level < 2 || level > MaxLevel) return false;
        if (GetLevel() < level) return false;
        return !IsLevelClaimed(level);
    }

    public static bool HasUnclaimedLevel()
    {
        for (int lv = 2; lv <= MaxLevel; lv++)
        {
            if (CanClaimLevel(lv)) return true;
        }
        return false;
    }

    public static int FirstClaimableLevel()
    {
        for (int lv = 2; lv <= MaxLevel; lv++)
        {
            if (CanClaimLevel(lv)) return lv;
        }
        return 0;
    }

    public static bool ClaimLevel(int level)
    {
        if (!CanClaimLevel(level)) return false;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return false;

        data.claimedLogMileageLevels ??= new HashSet<int>();
        if (!data.claimedLogMileageLevels.Add(level)) return false;

        var reward = LevelRewards[level - 1];
        if (reward.Gold > 0)
            ResourceWallet.Add(ResourceWallet.ResourceType.Gold, reward.Gold, save: false, notify: true);
        if (reward.Diamond > 0)
            ResourceWallet.Add(ResourceWallet.ResourceType.Diamond, reward.Diamond, save: false, notify: true);
        if (reward.EnchantStone > 0)
            ResourceWallet.Add(ResourceWallet.ResourceType.EnchantStone, reward.EnchantStone, save: false, notify: true);
        if (reward.TalentPoint > 0)
            ResourceWallet.Add(ResourceWallet.ResourceType.TalentPoint, reward.TalentPoint, save: false, notify: true);
        if (!string.IsNullOrEmpty(reward.TitleId))
            data.logMileageTitleId = reward.TitleId;

        // 设计：里程 Lv2「森林行者」可领暮火之杖（与教程发放共用防重 key）
        if (level == 2)
            SpecialWeapons.TryGrantTwilightStaff(showToast: true);

        SaveSystem.Instance.Save();
        RedDot.RefreshCommon();
        return true;
    }

    public static int ClaimAllAvailable()
    {
        int n = 0;
        for (int lv = 2; lv <= MaxLevel; lv++)
        {
            if (ClaimLevel(lv)) n++;
        }
        return n;
    }

    /// <summary>防重复发点。返回实际增加点数（0=已发过）。</summary>
    public static int TryGrantOnce(string sourceKey, int points, bool save = true)
    {
        if (string.IsNullOrEmpty(sourceKey) || points <= 0) return 0;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return 0;
        data.logMileageGrantedKeys ??= new HashSet<string>();
        if (!data.logMileageGrantedKeys.Add(sourceKey)) return 0;
        data.logMileagePoints = Mathf.Max(0, data.logMileagePoints + points);
        if (save) SaveSystem.Instance.Save();
        RedDot.RefreshCommon();
        return points;
    }

    /// <summary>里程商店消费点数。</summary>
    public static bool TrySpendPoints(int points, bool save = false)
    {
        if (points <= 0) return true;
        var data = SaveSystem.Instance?.Data;
        if (data == null || data.logMileagePoints < points) return false;
        data.logMileagePoints -= points;
        if (save) SaveSystem.Instance.Save();
        return true;
    }

    public static int PointsForMonsterKind(string kind)
    {
        if (string.IsNullOrEmpty(kind)) return 5;
        if (kind.Contains("首领") || kind.Contains("Boss") || kind.Contains("boss"))
            return 20;
        if (kind.Contains("精英"))
            return 10;
        return 5;
    }

    public static int PointsForMercRarity(MercRosterDefs.MercRarity rarity)
    {
        switch (rarity)
        {
            case MercRosterDefs.MercRarity.Legendary: return 50;
            case MercRosterDefs.MercRarity.Rare: return 20;
            default: return 10;
        }
    }

    public static int PointsForAchievementPoints(int achievementPoints)
    {
        if (achievementPoints >= 50) return 50;
        if (achievementPoints >= 25) return 20;
        return 10;
    }

    public static int GrantMonsterSeen(string catalogId, string kind)
    {
        return TryGrantOnce("monster:" + catalogId, PointsForMonsterKind(kind));
    }

    public static int GrantMercSeen(string catalogId, MercRosterDefs.MercRarity rarity)
    {
        return TryGrantOnce("merc:" + catalogId, PointsForMercRarity(rarity));
    }

    public static int GrantAchievement(string achievementId, int achievementPoints)
    {
        return TryGrantOnce("ach:" + achievementId, PointsForAchievementPoints(achievementPoints));
    }

    public static int GrantWorld(string worldId)
    {
        return TryGrantOnce("world:" + worldId, 5);
    }

    public static int GrantMain(string mainId)
    {
        return TryGrantOnce("main:" + mainId, 15);
    }

    public static int GrantSide(string sideId)
    {
        return TryGrantOnce("side:" + sideId, 20);
    }

    public static string FormatStatusLine()
    {
        int lv = GetLevel();
        int pts = Points;
        string name = LevelRewards[lv - 1].Label;
        if (lv >= MaxLevel)
            return $"日志里程 Lv{lv} {name}  {pts}点（满级）";
        int need = NextThreshold();
        return $"日志里程 Lv{lv} {name}  {pts}/{need}";
    }

    public static string FormatRewardPreview(int level)
    {
        if (level < 1 || level > MaxLevel) return "";
        var r = LevelRewards[level - 1];
        var parts = new List<string>();
        if (r.Gold > 0) parts.Add($"金币×{r.Gold}");
        if (r.Diamond > 0) parts.Add($"钻石×{r.Diamond}");
        if (r.EnchantStone > 0) parts.Add($"附魔石×{r.EnchantStone}");
        if (r.TalentPoint > 0) parts.Add($"天赋石×{r.TalentPoint}");
        if (!string.IsNullOrEmpty(r.TitleId)) parts.Add("称号·传说记录者");
        if (level == 2) parts.Add("暮火之杖");
        if (parts.Count == 0) return r.Label;
        return r.Label + "：" + string.Join("，", parts);
    }
}
