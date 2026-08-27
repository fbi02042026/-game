using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 冒险日志成就 A001–A015：条件达成 → 可领奖（与战斗 AchievementSystem 分离）。
/// 达成时发日志里程；资源奖在日志成就页领取。
/// </summary>
public static class AdventureLogAchievements
{
    public const string ProgressForestKills = "forest_kills";
    public const string ProgressRiftEntries = "rift_entries";
    public const string ProgressLaodunBattles = "laodun_battles";
    public const string ProgressEquipPick = "equip_pick";
    public const string ProgressEnhanced = "enhanced_once";

    public struct AchReward
    {
        public int Gold;
        public int Mats;
        public int Talent;
        public int BackpackSlots;
        public int StaminaMax;
        public string TitleId;
        public string FrameId;
        public string SkinId;
        public string Label;
    }

    public static AchReward GetReward(string id)
    {
        switch (id)
        {
            case "A001": return new AchReward { Gold = 100, Label = "金币×100" };
            case "A002": return new AchReward { Gold = 50, Label = "金币×50" };
            case "A003": return new AchReward { Mats = 5, Label = "强化石×5" };
            case "A004": return new AchReward { Gold = 200, Label = "金币×200" };
            case "A005": return new AchReward { Gold = 100, Label = "金币×100" };
            case "A006": return new AchReward { Talent = 3, Label = "天赋石×3" };
            case "A007": return new AchReward { BackpackSlots = 1, Label = "背包扩容+1" };
            case "A008": return new AchReward { Mats = 10, Label = "强化石×10" };
            case "A009": return new AchReward { Gold = 150, Label = "金币×150" };
            case "A010": return new AchReward { StaminaMax = 1, Label = "体力上限+1" };
            case "A011": return new AchReward { Gold = 500, Label = "金币×500" };
            case "A012": return new AchReward { TitleId = "forest_untouched", Label = "称号「森林无伤者」" };
            case "A013": return new AchReward { Talent = 5, Label = "天赋石×5" };
            case "A014": return new AchReward { FrameId = "frame_nightmare", Label = "限定头像框" };
            case "A015": return new AchReward { SkinId = "skin_laodun_rust", Label = "老盾皮肤「锈迹盾卫」" };
            default: return new AchReward { Label = "" };
        }
    }

    public static int GetProgress(string key)
    {
        var dict = SaveSystem.Instance?.Data?.logAchProgress;
        if (dict == null || string.IsNullOrEmpty(key)) return 0;
        return dict.TryGetValue(key, out int n) ? n : 0;
    }

    public static void AddProgress(string key, int add = 1, bool evaluate = true)
    {
        if (string.IsNullOrEmpty(key) || add == 0) return;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        data.logAchProgress ??= new Dictionary<string, int>();
        data.logAchProgress.TryGetValue(key, out int cur);
        data.logAchProgress[key] = cur + add;
        if (evaluate) EvaluateAll();
        else SaveSystem.Instance.Save();
    }

    public static void SetProgressAtLeast(string key, int value, bool evaluate = true)
    {
        if (string.IsNullOrEmpty(key)) return;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        data.logAchProgress ??= new Dictionary<string, int>();
        data.logAchProgress.TryGetValue(key, out int cur);
        if (value <= cur) return;
        data.logAchProgress[key] = value;
        if (evaluate) EvaluateAll();
        else SaveSystem.Instance.Save();
    }

    public static bool IsCompleted(string id)
    {
        var set = SaveSystem.Instance?.Data?.completedLogAchIds;
        return set != null && !string.IsNullOrEmpty(id) && set.Contains(id);
    }

    public static bool IsClaimed(string id)
    {
        var set = SaveSystem.Instance?.Data?.claimedLogAchIds;
        return set != null && !string.IsNullOrEmpty(id) && set.Contains(id);
    }

    public static bool CanClaim(string id) => IsCompleted(id) && !IsClaimed(id);

    public static bool HasUnclaimed()
    {
        var list = AdventureLogCatalog.Achievements;
        for (int i = 0; i < list.Length; i++)
        {
            if (CanClaim(list[i].Id)) return true;
        }
        return false;
    }

    public static bool CheckCondition(string id)
    {
        var data = SaveSystem.Instance?.Data;
        switch (id)
        {
            case "A001": return StoryProgress.TutorialDone;
            case "A002": return GetProgress("evacuated") > 0;
            case "A003": return GetProgress("died") > 0;
            case "A004": return GetProgress(ProgressForestKills) >= 100;
            case "A005": return GetProgress("elite_kill") > 0;
            case "A006": return GetProgress("boss_ch1") > 0 || AdventureLogCatalog.ChapterCleared(1);
            case "A007": return GetProgress(ProgressEquipPick) >= 50
                               || AdventureLogCatalog.AchProgress("equip_collect_50") >= 50
                               || AdventureLogCatalog.AchDone("equip_collect_50");
            case "A008": return GetProgress(ProgressEnhanced) > 0;
            case "A009": return GetProgress("merc_recruited") > 0
                               || AdventureLogCatalog.HasMerc("dunbing101")
                               || AdventureLogCatalog.HasMerc("dunbing102")
                               || AdventureLogCatalog.HasMerc("gongshou101")
                               || AdventureLogCatalog.HasMerc("kuangzhan101")
                               || AdventureLogCatalog.HasMerc("naima101");
            case "A010": return GetProgress(ProgressRiftEntries) >= 10;
            case "A011": return GetProgress("gold_run_10k") > 0;
            case "A012": return GetProgress("perfect_ch1") > 0;
            case "A013": return (data?.ch1BestClearDifficulty ?? -1) >= 1;
            case "A014": return (data?.ch1BestClearDifficulty ?? -1) >= 2;
            case "A015": return GetProgress(ProgressLaodunBattles) >= 20;
            default: return false;
        }
    }

    public static string FormatProgress(string id)
    {
        switch (id)
        {
            case "A004": return $"{Mathf.Min(100, GetProgress(ProgressForestKills))}/100";
            case "A007": return $"{Mathf.Min(50, GetProgress(ProgressEquipPick))}/50";
            case "A010": return $"{Mathf.Min(10, GetProgress(ProgressRiftEntries))}/10";
            case "A015": return $"{Mathf.Min(20, GetProgress(ProgressLaodunBattles))}/20";
            default:
                if (IsCompleted(id)) return "已完成";
                if (IsClaimed(id)) return "已领取";
                return "进行中";
        }
    }

    /// <summary>扫描全部成就，新达成则记完成并发里程、Toast。</summary>
    public static void EvaluateAll()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        data.completedLogAchIds ??= new HashSet<string>();
        var list = AdventureLogCatalog.Achievements;
        bool any = false;
        for (int i = 0; i < list.Length; i++)
        {
            string id = list[i].Id;
            if (data.completedLogAchIds.Contains(id)) continue;
            if (!CheckCondition(id)) continue;
            data.completedLogAchIds.Add(id);
            AdventureLogMileage.GrantAchievement(id, MileageTier(id));
            any = true;
            UIManager.Instance?.ShowToast($"成就达成：{list[i].Name}（打开冒险日志领取）");
        }
        if (any)
        {
            SaveSystem.Instance.Save();
            RefreshRedDots();
        }
        else
            SaveSystem.Instance.Save();
        TryAutoCompleteSides();
    }

    static void TryAutoCompleteSides()
    {
        var list = AdventureLogCatalog.Side;
        for (int i = 0; i < list.Length; i++)
        {
            var e = list[i];
            if (!AdventureLogCatalog.SideUnlocked(e)) continue;
            AdventureCodex.CompleteSide(e.Id);
        }
    }

    static int MileageTier(string id)
    {
        // 挑战/社交偏稀有
        if (id == "A012" || id == "A013" || id == "A014" || id == "A015") return 50;
        if (id == "A006" || id == "A007" || id == "A010" || id == "A011") return 20;
        return 10;
    }

    public static bool Claim(string id)
    {
        if (!CanClaim(id)) return false;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return false;
        data.claimedLogAchIds ??= new HashSet<string>();
        if (!data.claimedLogAchIds.Add(id)) return false;

        var r = GetReward(id);
        if (r.Gold > 0)
            ResourceWallet.Add(ResourceWallet.ResourceType.Gold, r.Gold, save: false, notify: true);
        if (r.Mats > 0)
            ResourceWallet.Add(ResourceWallet.ResourceType.DecomposeMat, r.Mats, save: false, notify: true);
        if (r.Talent > 0)
            ResourceWallet.Add(ResourceWallet.ResourceType.TalentPoint, r.Talent, save: false, notify: true);
        if (r.BackpackSlots > 0)
            data.backpackExtraSlots += r.BackpackSlots;
        if (r.StaminaMax > 0)
            data.staminaBonusMax += r.StaminaMax;
        if (!string.IsNullOrEmpty(r.TitleId))
        {
            data.unlockedTitleIds ??= new HashSet<string>();
            data.unlockedTitleIds.Add(r.TitleId);
        }
        if (!string.IsNullOrEmpty(r.FrameId))
        {
            data.unlockedFrameIds ??= new HashSet<string>();
            data.unlockedFrameIds.Add(r.FrameId);
        }
        if (!string.IsNullOrEmpty(r.SkinId))
        {
            data.unlockedSkinIds ??= new HashSet<string>();
            data.unlockedSkinIds.Add(r.SkinId);
        }

        SaveSystem.Instance.Save();
        RefreshRedDots();
        UIManager.Instance?.ShowToast($"领取成就奖励：{r.Label}");
        return true;
    }

    public static int ClaimAll()
    {
        int n = 0;
        var list = AdventureLogCatalog.Achievements;
        for (int i = 0; i < list.Length; i++)
        {
            if (Claim(list[i].Id)) n++;
        }
        return n;
    }

    public static void RefreshRedDots()
    {
        bool ach = HasUnclaimed() || AdventureLogMileage.HasUnclaimedLevel();
        RedDot.Set(RedDot.Achievement, ach);
        AdventureCodex.RefreshRedDots();
    }

    // ---- 事件钩子 ----

    public static void OnTutorialDone()
    {
        EvaluateAll();
    }

    public static void OnEvacuated(bool tutorial)
    {
        AddProgress("evacuated", 1);
    }

    public static void OnDied()
    {
        AddProgress("died", 1);
    }

    public static void OnRiftEntered()
    {
        AddProgress(ProgressRiftEntries, 1);
    }

    public static void OnMonsterKilled(Monster m, int chapter)
    {
        if (m == null) return;
        if (chapter <= 1)
            AddProgress(ProgressForestKills, 1, evaluate: false);
        if (m.IsEliteWave)
            AddProgress("elite_kill", 1, evaluate: false);
        if (m.IsBossUnit && chapter <= 1)
            AddProgress("boss_ch1", 1, evaluate: false);
        EvaluateAll();
    }

    public static void OnEquipPicked()
    {
        AddProgress(ProgressEquipPick, 1);
    }

    public static void OnEnhanced()
    {
        SetProgressAtLeast(ProgressEnhanced, 1);
    }

    public static void OnMercRecruited()
    {
        SetProgressAtLeast("merc_recruited", 1);
    }

    public static void OnLaodunBattled()
    {
        AddProgress(ProgressLaodunBattles, 1);
    }

    public static void OnRunGoldPeak(long gold)
    {
        if (gold >= 10000)
            SetProgressAtLeast("gold_run_10k", 1);
    }

    public static void OnChapterCleared(int chapter, int difficulty, bool perfectNoDamage)
    {
        if (chapter != 1) return;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        if (difficulty > data.ch1BestClearDifficulty)
            data.ch1BestClearDifficulty = difficulty;
        if (perfectNoDamage && difficulty == 0)
            SetProgressAtLeast("perfect_ch1", 1, evaluate: false);
        AdventureCodex.CompleteMain("C1Z");
        AdventureCodex.CompleteMain("C1G");
        EvaluateAll();
    }
}
