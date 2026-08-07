using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 前置关卡系统：进入副本第1关前，从遗产中三选一作为开局装备
/// 支持看广告刷新，无遗产时提供基础装备
/// </summary>
public class PreLevelSystem : Singleton<PreLevelSystem>
{
    /// <summary>
    /// 选择完成事件 (selectedEquip)
    /// </summary>
    public event Action<EquipmentData> OnSelectionComplete;

    /// <summary>
    /// 当前展示的3个选项
    /// </summary>
    public List<EquipmentData> currentOptions { get; private set; } = new List<EquipmentData>();

    /// <summary>
    /// 当前选中的索引 (-1=未选)
    /// </summary>
    public int selectedIndex { get; private set; } = -1;

    /// <summary>
    /// 本局是否已刷新过
    /// </summary>
    public bool hasRefreshedThisRun { get; private set; } = false;

    /// <summary>
    /// 是否已完成选择
    /// </summary>
    public bool hasSelected { get; private set; } = false;

    protected override void Awake()
    {
        base.Awake();
    }

    /// <summary>
    /// 开始前置关卡选择
    /// </summary>
    public void StartPreLevelSelection()
    {
        hasRefreshedThisRun = false;
        hasSelected = false;
        selectedIndex = -1;
        GenerateOptions();

        // TODO: 显示UI界面
        Debug.Log("[PreLevelSystem] 前置关卡：请从3件遗产中选择1件作为开局装备");
    }

    /// <summary>
    /// 生成3个选项
    /// </summary>
    void GenerateOptions()
    {
        currentOptions.Clear();

        var legacyPool = SaveSystem.Instance.Data.legacyEquipPool;

        if (legacyPool.Count >= 3)
        {
            // 从遗产池中随机选3件（不重复）
            List<int> indices = new List<int>();
            for (int i = 0; i < legacyPool.Count; i++) indices.Add(i);

            // Fisher-Yates洗牌
            for (int i = indices.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                int temp = indices[i];
                indices[i] = indices[j];
                indices[j] = temp;
            }

            for (int i = 0; i < 3; i++)
            {
                currentOptions.Add(legacyPool[indices[i]]);
            }
        }
        else if (legacyPool.Count > 0)
        {
            // 遗产不足3件，全部显示 + 补充基础装备
            for (int i = 0; i < legacyPool.Count; i++)
            {
                currentOptions.Add(legacyPool[i]);
            }
            for (int i = legacyPool.Count; i < 3; i++)
            {
                currentOptions.Add(CreateBasicEquip());
            }
        }
        else
        {
            // 没有遗产，提供3件白色基础装备
            for (int i = 0; i < 3; i++)
            {
                currentOptions.Add(CreateBasicEquip());
            }
        }
    }

    /// <summary>
    /// 看广告刷新选项
    /// </summary>
    public bool RefreshOptions()
    {
        if (hasRefreshedThisRun)
        {
            Debug.Log("[PreLevelSystem] 本局已刷新过，无法再次刷新");
            return false;
        }

        // TODO: 接入广告SDK
        bool adWatched = true; // 模拟广告已看完

        if (adWatched)
        {
            hasRefreshedThisRun = true;
            GenerateOptions();
            selectedIndex = -1;
            Debug.Log("[PreLevelSystem] 已刷新遗产选项");
            return true;
        }

        return false;
    }

    /// <summary>
    /// 选择某个选项
    /// </summary>
    public void SelectOption(int index)
    {
        if (index < 0 || index >= currentOptions.Count) return;
        selectedIndex = index;
        Debug.Log($"[PreLevelSystem] 选择了选项 {index}");
    }

    /// <summary>
    /// 确认选择，进入第1关
    /// </summary>
    public void ConfirmSelection()
    {
        if (selectedIndex < 0)
        {
            Debug.LogWarning("[PreLevelSystem] 请先选择一件装备");
            return;
        }

        hasSelected = true;
        var selected = currentOptions[selectedIndex];

        // 将选中的装备穿戴到英雄身上
        EquipToHero(selected);

        OnSelectionComplete?.Invoke(selected);
        Debug.Log($"[PreLevelSystem] 确认选择: {selected.equipId}，进入第1关");
    }

    /// <summary>
    /// 将遗产装备穿戴到英雄
    /// </summary>
    void EquipToHero(EquipmentData equipData)
    {
        // TODO: 根据equipData创建EquipInstance并装备到英雄
        // 这里需要与GridBackpackSystem和HeroCostumeManager配合
        // 简化处理：直接通知英雄穿上
        if (Hero.Instance != null)
        {
            // Hero.Instance.EquipFromLegacy(equipData);
        }
    }

    /// <summary>
    /// 创建一件白色基础装备
    /// </summary>
    EquipmentData CreateBasicEquip()
    {
        // 随机选择一种基础装备类型
        string[] basicIds = { "sword_basic", "armor_basic", "helmet_basic" };
        string id = basicIds[UnityEngine.Random.Range(0, basicIds.Length)];

        return new EquipmentData
        {
            equipId = id,
            rarity = 0, // 白色
            star = 0,
            requireLevel = 1,
            isLegacy = false
        };
    }

    /// <summary>
    /// 跳过选择（调试用，直接不给装备进第1关）
    /// </summary>
    public void Skip()
    {
        hasSelected = true;
        OnSelectionComplete?.Invoke(null);
    }
}
