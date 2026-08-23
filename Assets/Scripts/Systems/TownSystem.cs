using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 城镇系统：建筑由天赋解锁，解锁后出现在城镇中
/// 管理建筑状态、升级、品质解锁
/// </summary>
public class TownSystem : Singleton<TownSystem>
{
    /// <summary>
    /// 建筑状态变化事件 (buildingType, newLevel)
    /// </summary>
    public event Action<BuildingType, int> OnBuildingLevelChanged;

    /// <summary>
    /// 建筑解锁事件 (buildingType)
    /// </summary>
    public event Action<BuildingType> OnBuildingUnlocked;

    /// <summary>
    /// 当前解锁的建筑列表
    /// </summary>
    public Dictionary<BuildingType, BuildingInfo> buildings { get; private set; } = new Dictionary<BuildingType, BuildingInfo>();

    /// <summary>
    /// 铁匠铺品质解锁等级
    /// </summary>
    public Dictionary<int, Rarity> blacksmithQualityUnlock = new Dictionary<int, Rarity>
    {
        { 1, Rarity.Uncommon },  // 1级解锁绿色
        { 2, Rarity.Rare },      // 2级解锁蓝色
        { 3, Rarity.Epic },      // 3级解锁紫色
        { 5, Rarity.Legendary }, // 5级解锁橙色
    };

    protected override void Awake()
    {
        base.Awake();
        InitBuildings();
    }

    void InitBuildings()
    {
        buildings.Clear();

        // 初始解锁的建筑
        UnlockBuilding(BuildingType.HeroStatue, 1); // 英雄雕像初始存在
        UnlockBuilding(BuildingType.Altar, 1);      // 祭坛（天赋）初始存在
        UnlockBuilding(BuildingType.AchievementHall, 1); // 成就殿堂初始存在

        // 检查存档中已解锁的建筑
        var data = SaveSystem.Instance.Data;
        if (data.talents.ContainsKey("blacksmith_unlock") && data.talents["blacksmith_unlock"] > 0)
        {
            UnlockBuilding(BuildingType.Blacksmith, data.townLevel.blacksmith);
        }
        if (data.talents.ContainsKey("tavern_unlock") && data.talents["tavern_unlock"] > 0)
        {
            UnlockBuilding(BuildingType.Tavern, data.townLevel.tavern);
        }
        if (data.talents.ContainsKey("farm_unlock") && data.talents["farm_unlock"] > 0)
        {
            UnlockBuilding(BuildingType.Farm, data.townLevel.farm);
        }
    }

    #region 建筑解锁

    /// <summary>
    /// 解锁建筑（由天赋系统调用）
    /// </summary>
    public void UnlockBuilding(BuildingType type, int initialLevel = 1)
    {
        if (buildings.ContainsKey(type))
        {
            Debug.Log($"[TownSystem] 建筑已解锁: {type}");
            return;
        }

        var info = new BuildingInfo
        {
            type = type,
            level = initialLevel,
            isUnlocked = true
        };

        buildings[type] = info;
        OnBuildingUnlocked?.Invoke(type);

        Debug.Log($"[TownSystem] 解锁建筑: {type} (Lv.{initialLevel})");
    }

    /// <summary>
    /// 是否已解锁某建筑
    /// </summary>
    public bool IsUnlocked(BuildingType type)
    {
        return buildings.ContainsKey(type) && buildings[type].isUnlocked;
    }

    #endregion

    #region 建筑升级

    /// <summary>
    /// 升级建筑
    /// </summary>
    public bool UpgradeBuilding(BuildingType type)
    {
        if (!IsUnlocked(type))
        {
            Debug.LogWarning($"[TownSystem] 建筑未解锁，无法升级: {type}");
            return false;
        }

        var info = buildings[type];
        int nextLevel = info.level + 1;
        int cost = GetUpgradeCost(type, nextLevel);

        var data = SaveSystem.Instance.Data;
        if (data.totalGold < cost)
        {
            Debug.Log($"[TownSystem] 金币不足，需要{cost}金");
            return false;
        }

        if (!ResourceWallet.TrySpend(ResourceWallet.ResourceType.Gold, cost, save: false, notify: true))
            return false;
        info.level = nextLevel;

        // 同步到存档
        SyncToSaveData(type, info.level);

        OnBuildingLevelChanged?.Invoke(type, info.level);
        SaveSystem.Instance.Save();

        Debug.Log($"[TownSystem] {type} 升级到 Lv.{nextLevel}，消耗{cost}金");
        return true;
    }

    /// <summary>
    /// 获取建筑当前等级
    /// </summary>
    public int GetBuildingLevel(BuildingType type)
    {
        if (buildings.TryGetValue(type, out var info))
            return info.level;
        return 0;
    }

    /// <summary>
    /// 获取升级消耗
    /// </summary>
    public int GetUpgradeCost(BuildingType type, int targetLevel)
    {
        switch (type)
        {
            case BuildingType.Blacksmith:
                return (int)(100 * Mathf.Pow(1.5f, targetLevel - 1));
            case BuildingType.Tavern:
                return (int)(150 * Mathf.Pow(1.5f, targetLevel - 1));
            case BuildingType.Farm:
                return (int)(80 * Mathf.Pow(1.5f, targetLevel - 1));
            default:
                return 0;
        }
    }

    void SyncToSaveData(BuildingType type, int level)
    {
        var data = SaveSystem.Instance.Data;
        switch (type)
        {
            case BuildingType.Blacksmith:
                data.townLevel.blacksmith = level;
                break;
            case BuildingType.Tavern:
                data.townLevel.tavern = level;
                break;
            case BuildingType.Farm:
                data.townLevel.farm = level;
                break;
        }
    }

    #endregion

    #region 铁匠铺品质

    /// <summary>
    /// 获取铁匠铺当前解锁的最高品质
    /// </summary>
    public Rarity GetUnlockedMaxRarity()
    {
        int blacksmithLevel = GetBuildingLevel(BuildingType.Blacksmith);
        Rarity maxRarity = Rarity.Common;

        foreach (var kvp in blacksmithQualityUnlock)
        {
            if (blacksmithLevel >= kvp.Key && kvp.Value > maxRarity)
            {
                maxRarity = kvp.Value;
            }
        }

        return maxRarity;
    }

    /// <summary>
    /// 某品质是否已解锁
    /// </summary>
    public bool IsQualityUnlocked(Rarity rarity)
    {
        return GetUnlockedMaxRarity() >= rarity;
    }

    #endregion

    #region 农场离线收益

    /// <summary>
    /// 计算离线收益（统一走 OfflineGoldCalc；实际领取仅 TownSceneBootstrap）
    /// </summary>
    public long CalculateOfflineReward(DateTime lastOnlineTime)
    {
        if (!IsUnlocked(BuildingType.Farm))
            return 0;

        var farmLevel = GetBuildingLevel(BuildingType.Farm);
        if (farmLevel <= 0) return 0;

        long secs = (long)Math.Max(0, (DateTime.Now - lastOnlineTime).TotalSeconds);
        return OfflineGoldCalc.FromSeconds(secs, farmLevel);
    }

    #endregion

    #region 天赋解锁回调

    /// <summary>
    /// 天赋系统调用：当玩家点了某个解锁天赋时
    /// </summary>
    public void OnTalentUnlocked(string talentId)
    {
        switch (talentId)
        {
            case "blacksmith_unlock":
                UnlockBuilding(BuildingType.Blacksmith, 1);
                break;
            case "tavern_unlock":
                UnlockBuilding(BuildingType.Tavern, 1);
                break;
            case "farm_unlock":
                UnlockBuilding(BuildingType.Farm, 1);
                break;
        }
    }

    #endregion
}

/// <summary>
/// 建筑类型
/// </summary>
public enum BuildingType
{
    Blacksmith,      // 铁匠铺
    Tavern,          // 酒馆
    Altar,           // 祭坛（天赋）
    Farm,            // 农场
    HeroStatue,      // 英雄雕像
    AchievementHall  // 成就殿堂
}

/// <summary>
/// 建筑信息
/// </summary>
public class BuildingInfo
{
    public BuildingType type;
    public int level;
    public bool isUnlocked;
}
