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
        GamePerf.Log("[PreLevelSystem] 前置关卡：已生成三选一选项");
    }

    void GenerateOptions()
    {
        currentOptions.Clear();
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        var legacyPool = data != null ? data.legacyEquipPool : null;
        if (legacyPool == null) legacyPool = new List<EquipmentData>();

        if (legacyPool.Count >= 3)
        {
            List<int> indices = new List<int>();
            for (int i = 0; i < legacyPool.Count; i++) indices.Add(i);
            for (int i = indices.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                int temp = indices[i];
                indices[i] = indices[j];
                indices[j] = temp;
            }
            for (int i = 0; i < 3; i++)
                currentOptions.Add(legacyPool[indices[i]]);
        }
        else if (legacyPool.Count > 0)
        {
            for (int i = 0; i < legacyPool.Count; i++)
                currentOptions.Add(legacyPool[i]);
            for (int i = legacyPool.Count; i < 3; i++)
                currentOptions.Add(CreateBasicEquip());
        }
        else
        {
            for (int i = 0; i < 3; i++)
                currentOptions.Add(CreateBasicEquip());
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

        bool ok = false;
        RewardedAdBridge.ShowRewarded("prelevel_refresh", success =>
        {
            if (!success) return;
            hasRefreshedThisRun = true;
            GenerateOptions();
            selectedIndex = -1;
            ok = true;
            GamePerf.Log("[PreLevelSystem] 已刷新遗产选项");
        });
        return ok || hasRefreshedThisRun;
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

        // 遗产池消耗：选中且标记为遗产的条目从池中移除（基础白装不在池里）
        RemoveFromLegacyPool(selected);

        // 将选中的装备穿戴到英雄身上
        EquipToHero(selected);

        OnSelectionComplete?.Invoke(selected);
        Debug.Log($"[PreLevelSystem] 确认选择: {selected.equipId}，进入第1关");
    }

    static void RemoveFromLegacyPool(EquipmentData selected)
    {
        if (selected == null || !selected.isLegacy) return;
        var pool = SaveSystem.Instance?.Data?.legacyEquipPool;
        if (pool == null || pool.Count == 0) return;

        int idx = pool.IndexOf(selected);
        if (idx < 0)
        {
            // 选项可能是池中元素的拷贝感：按 id+稀有度+星级匹配第一件
            for (int i = 0; i < pool.Count; i++)
            {
                var e = pool[i];
                if (e == null) continue;
                if (e.equipId == selected.equipId && e.rarity == selected.rarity && e.star == selected.star)
                {
                    idx = i;
                    break;
                }
            }
        }
        if (idx >= 0)
        {
            pool.RemoveAt(idx);
            SaveSystem.Instance?.Save();
        }
    }

    /// <summary>
    /// 将遗产装备穿戴到英雄
    /// </summary>
    void EquipToHero(EquipmentData equipData)
    {
        if (equipData == null)
        {
            Debug.LogWarning("[PreLevelSystem] EquipToHero: equipData 为空");
            return;
        }

        var bag = GridBackpackSystem.Instance;
        if (bag == null)
        {
            Debug.LogWarning("[PreLevelSystem] GridBackpackSystem 为空，无法穿装");
            return;
        }

        EquipInstance inst = BuildEquipFromLegacy(equipData);
        if (inst == null)
        {
            Debug.LogWarning($"[PreLevelSystem] 无法构建装备实例: {equipData.equipId}");
            return;
        }

        if (!bag.TryEquipFromReward(inst))
            Debug.LogWarning($"[PreLevelSystem] 穿装失败: {inst.equipName ?? equipData.equipId}");
        else
            Debug.Log($"[PreLevelSystem] 已穿装: {inst.equipName ?? equipData.equipId} → {inst.slotType}");
    }

    static EquipInstance BuildEquipFromLegacy(EquipmentData d)
    {
        var cfg = ConfigManager.Instance;
        if (cfg == null) return null;

        EquipTemplate tpl = cfg.GetEquipTemplate(d.equipId);
        if (tpl == null)
            tpl = FindFallbackTemplate(d.equipId);
        if (tpl == null) return null;

        int heroLv = Hero.Instance != null ? Hero.Instance.level : 1;
        EquipInstance inst = EquipInstance.GenerateFromTemplate(tpl, 0, heroLv);
        inst.rarity = (Rarity)Mathf.Clamp(d.rarity, 0, (int)Rarity.Legendary);
        if (d.star > 0)
            inst.star = Mathf.Clamp(d.star, 0, (int)inst.rarity);
        if (d.requireLevel > 0)
            inst.requireLevel = d.requireLevel;
        if (d.attrBonus != null && d.attrBonus.Count > 0)
            inst.attrBonus = new List<AttrBonusData>(d.attrBonus);
        return inst;
    }

    static EquipTemplate FindFallbackTemplate(string hintId)
    {
        var cfg = ConfigManager.Instance;
        if (cfg == null) return null;

        // 占位 id（sword_basic 等）按槽位猜一件白色模板
        EquipSlotType prefer = EquipSlotType.MainHand;
        string h = hintId != null ? hintId.ToLowerInvariant() : "";
        if (h.Contains("armor") || h.Contains("chest")) prefer = EquipSlotType.Chest;
        else if (h.Contains("helmet") || h.Contains("head")) prefer = EquipSlotType.Head;

        var samples = cfg.GetRandomEquipInstances(8, 0, 0);
        if (samples == null || samples.Count == 0) return null;
        for (int i = 0; i < samples.Count; i++)
        {
            if (samples[i]?.template != null && samples[i].slotType == prefer)
                return samples[i].template;
        }
        return samples[0]?.template;
    }

    /// <summary>
    /// 创建一件白色基础装备
    /// </summary>
    EquipmentData CreateBasicEquip()
    {
        var samples = ConfigManager.Instance != null
            ? ConfigManager.Instance.GetRandomEquipInstances(1, 0, 0)
            : null;
        if (samples != null && samples.Count > 0 && samples[0] != null)
        {
            var eq = samples[0];
            return new EquipmentData
            {
                equipId = eq.templateId,
                rarity = 0,
                star = 0,
                requireLevel = 1,
                attrBonus = eq.attrBonus != null
                    ? new List<AttrBonusData>(eq.attrBonus)
                    : new List<AttrBonusData>(),
                isLegacy = false
            };
        }

        Debug.LogWarning("[PreLevelSystem] 无可用装备模板，CreateBasicEquip 回退占位 id");
        return new EquipmentData
        {
            equipId = "sword_basic",
            rarity = 0,
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
