using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 冒险日志图鉴遭遇/已读存档。
/// 章解锁 → 该章格子可见；未见过 = 黑影；见过 = 亮图；未见详情 = 红点。
/// </summary>
public static class AdventureCodex
{
    public static bool ChapterUnlocked(int chapter)
    {
        if (chapter <= 1) return true;
        int max = SaveSystem.Instance?.Data?.maxUnlockedChapter ?? 1;
        return chapter <= max;
    }

    public static int MonsterChapter(AdventureLogCatalog.MonsterEntry e)
    {
        if (string.IsNullOrEmpty(e.Id) || e.Id.Length < 2) return 1;
        if (!int.TryParse(e.Id.Substring(1), out int n)) return 1;
        return n / 100 + 1;
    }

    public static string SceneTitle(AdventureLogCatalog.MonsterEntry e)
    {
        string place = e.Place ?? "";
        int cut = place.IndexOf('·');
        if (cut > 0) return place.Substring(0, cut);
        int ch = MonsterChapter(e);
        return GameConfig.GetChapterMapName(ch);
    }

    public static bool IsSeenMonster(string catalogId)
    {
        var set = SaveSystem.Instance?.Data?.seenMonsterIds;
        return set != null && !string.IsNullOrEmpty(catalogId) && set.Contains(catalogId);
    }

    public static bool IsViewedMonster(string catalogId)
    {
        var set = SaveSystem.Instance?.Data?.viewedMonsterIds;
        return set != null && !string.IsNullOrEmpty(catalogId) && set.Contains(catalogId);
    }

    public static bool IsSeenMerc(string catalogId)
    {
        var set = SaveSystem.Instance?.Data?.seenMercIds;
        return set != null && !string.IsNullOrEmpty(catalogId) && set.Contains(catalogId);
    }

    public static bool IsViewedMerc(string catalogId)
    {
        var set = SaveSystem.Instance?.Data?.viewedMercIds;
        return set != null && !string.IsNullOrEmpty(catalogId) && set.Contains(catalogId);
    }

    public static bool IsDefeatedMonster(string catalogId)
    {
        var set = SaveSystem.Instance?.Data?.defeatedMonsterIds;
        return set != null && !string.IsNullOrEmpty(catalogId) && set.Contains(catalogId);
    }

    public static void MarkMonsterSeen(string catalogIdOrAssetId)
    {
        if (string.IsNullOrEmpty(catalogIdOrAssetId)) return;
        string id = ResolveMonsterId(catalogIdOrAssetId);
        if (string.IsNullOrEmpty(id)) return;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        data.seenMonsterIds ??= new HashSet<string>();
        if (!data.seenMonsterIds.Add(id)) return;
        string kind = "";
        var list = AdventureLogCatalog.Monsters;
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i].Id == id) { kind = list[i].Kind; break; }
        }
        AdventureLogMileage.GrantMonsterSeen(id, kind);
        SaveSystem.Instance.Save();
        RefreshRedDots();
    }

    /// <summary>首次击败：解锁完整图鉴描述（不重复发里程点）。</summary>
    public static void MarkMonsterDefeated(string catalogIdOrAssetId)
    {
        if (string.IsNullOrEmpty(catalogIdOrAssetId)) return;
        string id = ResolveMonsterId(catalogIdOrAssetId);
        if (string.IsNullOrEmpty(id)) return;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        data.defeatedMonsterIds ??= new HashSet<string>();
        data.seenMonsterIds ??= new HashSet<string>();
        bool firstDefeat = data.defeatedMonsterIds.Add(id);
        bool firstSeen = data.seenMonsterIds.Add(id);
        if (firstSeen)
        {
            string kind = "";
            var list = AdventureLogCatalog.Monsters;
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i].Id == id) { kind = list[i].Kind; break; }
            }
            AdventureLogMileage.GrantMonsterSeen(id, kind);
        }
        if (!firstDefeat && !firstSeen) return;
        SaveSystem.Instance.Save();
        RefreshRedDots();
    }

    public static void MarkMonsterViewed(string catalogId)
    {
        if (string.IsNullOrEmpty(catalogId)) return;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        data.seenMonsterIds ??= new HashSet<string>();
        data.viewedMonsterIds ??= new HashSet<string>();
        data.seenMonsterIds.Add(catalogId);
        if (!data.viewedMonsterIds.Add(catalogId)) return;
        SaveSystem.Instance.Save();
        RefreshRedDots();
    }

    public static void MarkMercSeen(string catalogIdOrAssetId)
    {
        if (string.IsNullOrEmpty(catalogIdOrAssetId)) return;
        string id = ResolveMercId(catalogIdOrAssetId);
        if (string.IsNullOrEmpty(id)) return;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        data.seenMercIds ??= new HashSet<string>();
        if (!data.seenMercIds.Add(id)) return;
        AdventureLogCatalog.MercEntry merc = default;
        bool found = false;
        var list = AdventureLogCatalog.Mercs;
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i].Id == id) { merc = list[i]; found = true; break; }
        }
        var rarity = found ? GetMercRarity(merc) : MercRosterDefs.MercRarity.Common;
        AdventureLogMileage.GrantMercSeen(id, rarity);
        SaveSystem.Instance.Save();
        RefreshRedDots();
    }

    public static void MarkMercViewed(string catalogId)
    {
        if (string.IsNullOrEmpty(catalogId)) return;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;
        data.seenMercIds ??= new HashSet<string>();
        data.viewedMercIds ??= new HashSet<string>();
        data.seenMercIds.Add(catalogId);
        if (!data.viewedMercIds.Add(catalogId)) return;
        SaveSystem.Instance.Save();
        RefreshRedDots();
    }

    public static string ResolveMonsterId(string catalogOrAsset)
    {
        if (string.IsNullOrEmpty(catalogOrAsset)) return null;
        var list = AdventureLogCatalog.Monsters;
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i].Id == catalogOrAsset || list[i].AssetId == catalogOrAsset)
                return list[i].Id;
        }
        return catalogOrAsset.StartsWith("M") ? catalogOrAsset : null;
    }

    public static string ResolveMercId(string catalogOrAsset)
    {
        if (string.IsNullOrEmpty(catalogOrAsset)) return null;
        var list = AdventureLogCatalog.Mercs;
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i].Id == catalogOrAsset || list[i].AssetId == catalogOrAsset)
                return list[i].Id;
        }
        return catalogOrAsset;
    }

    public static List<AdventureLogCatalog.MonsterEntry> MonstersForChapter(int chapter)
    {
        var result = new List<AdventureLogCatalog.MonsterEntry>();
        var list = AdventureLogCatalog.Monsters;
        for (int i = 0; i < list.Length; i++)
        {
            if (MonsterChapter(list[i]) == chapter)
                result.Add(list[i]);
        }
        return result;
    }

    public static int MaxMonsterChapter()
    {
        int max = 1;
        var list = AdventureLogCatalog.Monsters;
        for (int i = 0; i < list.Length; i++)
            max = Mathf.Max(max, MonsterChapter(list[i]));
        return max;
    }

    public static bool HasUnviewedCodex()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return false;
        if (data.seenMonsterIds != null)
        {
            foreach (string id in data.seenMonsterIds)
            {
                if (data.viewedMonsterIds == null || !data.viewedMonsterIds.Contains(id))
                    return true;
            }
        }
        if (data.seenMercIds != null)
        {
            foreach (string id in data.seenMercIds)
            {
                if (data.viewedMercIds == null || !data.viewedMercIds.Contains(id))
                    return true;
            }
        }
        return false;
    }

    public static bool HasUnviewedMonsters()
    {
        var data = SaveSystem.Instance?.Data;
        if (data?.seenMonsterIds == null) return false;
        foreach (string id in data.seenMonsterIds)
        {
            if (data.viewedMonsterIds == null || !data.viewedMonsterIds.Contains(id))
                return true;
        }
        return false;
    }

    public static bool HasUnviewedMercs()
    {
        var data = SaveSystem.Instance?.Data;
        if (data?.seenMercIds == null) return false;
        foreach (string id in data.seenMercIds)
        {
            if (data.viewedMercIds == null || !data.viewedMercIds.Contains(id))
                return true;
        }
        return false;
    }

    public static void RefreshRedDots()
    {
        // 新账号 / 引导未完成：冒险日志不亮红点
        if (!StoryProgress.TutorialDone)
        {
            RedDot.Set(RedDot.Achievement, false);
            RedDot.Set(RedDot.LogMonster, false);
            RedDot.Set(RedDot.LogMerc, false);
            RedDot.Set(RedDot.Log, false);
            return;
        }

        bool reward = AdventureLogMileage.HasUnclaimedLevel()
                      || AdventureLogAchievements.HasUnclaimed()
                      || AdventureLogFragments.HasAnyCraftable();
        bool codex = HasUnviewedCodex();
        RedDot.Set(RedDot.Achievement, reward);
        RedDot.Set(RedDot.LogMonster, HasUnviewedMonsters());
        RedDot.Set(RedDot.LogMerc, HasUnviewedMercs());
        RedDot.Set(RedDot.Log, reward || codex);
    }

    public static MercRosterDefs.MercRarity GetMercRarity(AdventureLogCatalog.MercEntry e)
    {
        if (e.StoryNpc) return MercRosterDefs.MercRarity.Common;
        if (MercRosterDefs.TryGetByHireId(e.Id, out var def))
            return def.Rarity;
        // 文案里写了传奇的兜底
        string unlock = e.Unlock ?? "";
        if (unlock.Contains("传奇")) return MercRosterDefs.MercRarity.Legendary;
        return MercRosterDefs.MercRarity.Common;
    }

    /// <summary>资源奖已收归日志里程等级；格子详情仅展示文案。</summary>
    public static int CodexRewardGold(bool bossOrLegendary) => 0;

    public static bool CanClaimCodexReward(string catalogId) => false;

    public static bool TryClaimCodexReward(string catalogId, bool bossOrLegendary, out int gold)
    {
        gold = 0;
        return false;
    }

    public static string GuessAssetIdFromSprite(int monsterChapter, int spriteIndex)
    {
        string prefix = null;
        switch (monsterChapter)
        {
            case 1: prefix = "undead_1"; break;
            case 2: prefix = "jungle_2"; break;
            case 3: prefix = "sea_3"; break;
            case 4: prefix = "forest_4"; break;
            case 5: prefix = "field_5"; break;
            case 6: prefix = "cave_6"; break;
            case 7: prefix = "devil_7"; break;
            case 8: prefix = "ice_8"; break;
        }
        if (prefix == null) return null;
        int idx = Mathf.Clamp(spriteIndex > 0 ? spriteIndex : 1, 1, 12);
        return prefix + idx.ToString("D2");
    }

    public static Sprite LoadMonsterSprite(AdventureLogCatalog.MonsterEntry e)
    {
        int gameCh = MonsterChapter(e);
        int packCh = GameConfig.GetMonsterChapter(gameCh);
        int idx = 1;
        if (!string.IsNullOrEmpty(e.AssetId))
        {
            int underscore = e.AssetId.LastIndexOf('_');
            string num = underscore >= 0 ? e.AssetId.Substring(underscore + 1) : e.AssetId;
            if (int.TryParse(num, out int n))
                idx = Mathf.Max(1, n % 100);
        }
        var loader = MonsterSpriteLoader.Instance;
        if (loader != null)
        {
            var sp = loader.LoadMonsterSprite(packCh, idx - 1);
            if (sp != null) return sp;
            sp = loader.LoadMonsterSprite(packCh, idx);
            if (sp != null) return sp;
        }
        return null;
    }

    public static Sprite LoadMercSprite(AdventureLogCatalog.MercEntry e)
    {
        if (!string.IsNullOrEmpty(e.Id))
        {
            var sp = MercPortraitSprites.GetStand(e.Id);
            if (sp != null) return sp;
        }
        if (string.IsNullOrEmpty(e.AssetId)) return null;
        if (MercenaryManager.Instance != null)
            return MercenaryManager.Instance.GetIcon(e.AssetId);
        return MercPortraitSprites.GetHead(e.AssetId);
    }

    public static bool UnlockWorld(string worldId)
    {
        if (string.IsNullOrEmpty(worldId)) return false;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return false;
        data.unlockedWorldIds ??= new HashSet<string>();
        if (!data.unlockedWorldIds.Add(worldId)) return false;
        AdventureLogMileage.GrantWorld(worldId);
        SaveSystem.Instance.Save();
        return true;
    }

    public static bool CompleteMain(string mainId)
    {
        if (string.IsNullOrEmpty(mainId)) return false;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return false;
        data.completedMainIds ??= new HashSet<string>();
        if (!data.completedMainIds.Add(mainId)) return false;
        AdventureLogMileage.GrantMain(mainId);
        SaveSystem.Instance.Save();
        return true;
    }

    public static bool CompleteSide(string sideId)
    {
        if (string.IsNullOrEmpty(sideId)) return false;
        var data = SaveSystem.Instance?.Data;
        if (data == null) return false;
        data.completedSideIds ??= new HashSet<string>();
        if (!data.completedSideIds.Add(sideId)) return false;
        AdventureLogMileage.GrantSide(sideId);
        SaveSystem.Instance.Save();
        return true;
    }

    public static bool IsMainCompleted(string mainId)
    {
        var set = SaveSystem.Instance?.Data?.completedMainIds;
        return set != null && !string.IsNullOrEmpty(mainId) && set.Contains(mainId);
    }

    public static bool IsWorldUnlockedFlag(string worldId)
    {
        var set = SaveSystem.Instance?.Data?.unlockedWorldIds;
        return set != null && !string.IsNullOrEmpty(worldId) && set.Contains(worldId);
    }
}
