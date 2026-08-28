using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// 格子背包系统：支持装备占多格，按槽位穿戴，同部位不重复
/// </summary>
public class GridBackpackSystem : Singleton<GridBackpackSystem>
{
    private bool[,] _grid = new bool[GameConfig.BACKPACK_WIDTH, GameConfig.BACKPACK_HEIGHT];
    // 按槽位索引的装备字典，key为EquipSlotType
    private Dictionary<EquipSlotType, EquipInstance> _equippedBySlot = new Dictionary<EquipSlotType, EquipInstance>();
    private List<BackpackItem> _items = new List<BackpackItem>();
    public event System.Action OnBackpackChanged;
    public event System.Action OnCostumeChanged; // 换装事件

    public class BackpackItem
    {
        public EquipInstance equip;
        public int x;
        public int y;
        public int width;
        public int height;
    }

    public void InitNewRun()
    {
        _grid = new bool[GameConfig.BACKPACK_WIDTH, GameConfig.BACKPACK_HEIGHT];
        _equippedBySlot.Clear();
        _items.Clear();
        OnBackpackChanged?.Invoke();
    }

    /// <summary>
    /// 教程/开局：把默认武器装到普攻手（自动检测 Left/Right）。
    /// </summary>
    public bool EnsureStarterWeapon()
    {
        if (GetEquippedInLogicalSlot(EquipSlotType.MainHand) != null) return false;

        EquipTemplate tpl = ConfigManager.Instance != null
            ? ConfigManager.Instance.GetEquipTemplate("equip_training_sword")
            : null;
        if (tpl == null)
            tpl = Resources.Load<EquipTemplate>(ContentPaths.Config.Equips + "/equip_training_sword");
        if (tpl == null)
        {
            Debug.LogWarning("[GridBackpack] 缺少 equip_training_sword，跳过默认武器");
            return false;
        }

        tpl.ResolveIcon();
        int lv = Hero.Instance != null ? Hero.Instance.level : 1;
        var eq = EquipInstance.GenerateFromTemplate(tpl, 0, lv, true, Rarity.Common);
        eq.equipName = tpl.equipName;
        if (eq.icon == null) eq.icon = tpl.icon ?? EquipIcons.Get(tpl.iconFileName);

        if (!TryAcquireLoadoutItem(eq, out BackpackItem item) || item == null)
            return false;

        Hero.Instance?.costumeManager?.RefreshCostume();
        Debug.Log($"[GridBackpack] 已装备默认武器到普攻手: {eq.equipName} hand={eq.weaponHand}");
        return true;
    }

    [System.Obsolete("Use EnsureStarterWeapon")]
    public bool EnsureStarterOffHandWeapon() => EnsureStarterWeapon();

    public bool TryAddItem(EquipInstance equip, out BackpackItem item)
    {
        item = null;
        if (equip == null) return false;
        if (Hero.Instance != null && equip.requireLevel > Hero.Instance.level)
        {
            UIManager.Instance?.ShowToast($"等级不足！{equip.equipName}需要{equip.requireLevel}级才能装备");
            return false;
        }

        if (WeaponLoadoutRules.IsLoadoutItem(equip))
            return TryAcquireLoadoutItem(equip, out item);

        if (FindEmptyPosition(equip.gridWidth, equip.gridHeight, out int x, out int y))
        {
            item = PlaceItemInGrid(equip, x, y);
            OnBackpackChanged?.Invoke();
            AchievementSystem.Instance?.OnObtainEquip(equip.rarity);
            AdventureLogAchievements.OnEquipPicked();
            return true;
        }
        UIManager.Instance?.ShowToast($"背包空间不足！{equip.equipName}需要{equip.gridWidth}x{equip.gridHeight}格空间");
        return false;
    }

    /// <summary>
    /// 武器组入包：按 weaponHand 替换对应部位；双手清空主+副；旧件直接变强化石。入包即装备。
    /// </summary>
    public bool TryAcquireLoadoutItem(EquipInstance equip, out BackpackItem item)
    {
        item = null;
        if (equip == null) return false;

        WeaponLoadoutRules.GetSlotsToReplace(equip, out bool clearMain, out bool clearOff);
        var rig = GetHeroHandRig();

        if (clearMain)
            ConsumeLoadoutInLogicalSlot(EquipSlotType.MainHand, rig);
        if (clearOff)
            ConsumeLoadoutInLogicalSlot(EquipSlotType.OffHand, rig);

        equip.slotType = WeaponLoadoutRules.ResolveLogicalSlot(equip);
        if (!FindEmptyPosition(equip.gridWidth, equip.gridHeight, out int x, out int y))
        {
            UIManager.Instance?.ShowToast($"背包空间不足！{equip.equipName}需要{equip.gridWidth}x{equip.gridHeight}格空间");
            return false;
        }

        item = PlaceItemInGrid(equip, x, y);
        EquipItem(item);
        OnBackpackChanged?.Invoke();
        AchievementSystem.Instance?.OnObtainEquip(equip.rarity);
        AdventureLogAchievements.OnEquipPicked();
        return true;
    }

    BackpackItem PlaceItemInGrid(EquipInstance equip, int x, int y)
    {
        var item = new BackpackItem
        {
            equip = equip,
            x = x, y = y,
            width = equip.gridWidth, height = equip.gridHeight
        };
        OccupyGrid(x, y, equip.gridWidth, equip.gridHeight, true);
        _items.Add(item);
        return item;
    }

    void ConsumeLoadoutInLogicalSlot(EquipSlotType logicalSlot, HeroWeaponRig.HandRig rig)
    {
        EquipSlotType wearSlot = logicalSlot;
        if (rig.IsValid)
        {
            wearSlot = logicalSlot == EquipSlotType.OffHand ? rig.SecondarySlot : rig.AttackSlot;
        }

        if (!_equippedBySlot.TryGetValue(wearSlot, out var old) || old == null)
        {
            // 兜底：双手武器可能只记在攻击槽
            if (logicalSlot == EquipSlotType.OffHand) return;
            wearSlot = logicalSlot;
            if (!_equippedBySlot.TryGetValue(wearSlot, out old) || old == null)
                return;
        }

        ConsumeLoadoutEquip(old, wearSlot);
    }

    void ConsumeLoadoutEquip(EquipInstance equip, EquipSlotType wearSlot)
    {
        if (equip == null) return;

        var bi = FindBackpackItemByEquip(equip);
        if (bi != null)
        {
            OccupyGrid(bi.x, bi.y, bi.width, bi.height, false);
            _items.Remove(bi);
        }

        if (equip.weaponType == WeaponType.TwoHand)
        {
            var rig = GetHeroHandRig();
            if (rig.IsValid)
            {
                if (_equippedBySlot.TryGetValue(rig.AttackSlot, out var a) && a == equip)
                    _equippedBySlot.Remove(rig.AttackSlot);
                if (_equippedBySlot.TryGetValue(rig.SecondarySlot, out var s) && s == equip)
                    _equippedBySlot.Remove(rig.SecondarySlot);
            }
            else
            {
                if (_equippedBySlot.TryGetValue(EquipSlotType.MainHand, out var m) && m == equip)
                    _equippedBySlot.Remove(EquipSlotType.MainHand);
                if (_equippedBySlot.TryGetValue(EquipSlotType.OffHand, out var o) && o == equip)
                    _equippedBySlot.Remove(EquipSlotType.OffHand);
            }
        }
        else
        {
            _equippedBySlot.Remove(wearSlot);
        }

        int mats = WeaponLoadoutRules.CalcDecomposeMats(equip);
        WeaponLoadoutRules.GrantDecomposeMats(equip, save: false);
        UIManager.Instance?.ShowToast($"{equip.equipName} 已变为强化石 ×{mats}");
        Hero.Instance?.RecalcAttr();
    }

    BackpackItem FindBackpackItemByEquip(EquipInstance equip)
    {
        if (equip == null) return null;
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i]?.equip == equip) return _items[i];
        }
        return null;
    }

    private bool FindEmptyPosition(int w, int h, out int outX, out int outY)
    {
        outX = -1; outY = -1;
        int unlockedRows = GameConfig.GetUnlockedBackpackRows(SaveSystem.Instance?.Data);
        int maxY = Mathf.Min(GameConfig.BACKPACK_HEIGHT, unlockedRows);
        if (h > maxY) return false;
        for (int y = 0; y <= maxY - h; y++)
            for (int x = 0; x <= GameConfig.BACKPACK_WIDTH - w; x++)
                if (IsAreaEmpty(x, y, w, h)) { outX = x; outY = y; return true; }
        return false;
    }

    private bool IsAreaEmpty(int x, int y, int w, int h)
    {
        for (int i = x; i < x + w; i++)
            for (int j = y; j < y + h; j++)
                if (_grid[i, j]) return false;
        return true;
    }

    private void OccupyGrid(int x, int y, int w, int h, bool occupy)
    {
        for (int i = x; i < x + w; i++)
            for (int j = y; j < y + h; j++)
                _grid[i, j] = occupy;
    }

    public void DropItem(BackpackItem item)
    {
        if (item == null) return;
        if (item.equip != null && IsEquipped(item.equip))
            UnequipItem(item.equip.slotType);
        OccupyGrid(item.x, item.y, item.width, item.height, false);
        _items.Remove(item);
        Hero.Instance?.RecalcAttr();
        OnBackpackChanged?.Invoke();
    }

    /// <summary>通关奖励：武器组走替换链，护甲正常入包。</summary>
    public bool TryEquipFromReward(EquipInstance equip)
    {
        if (equip == null) return false;
        if (WeaponLoadoutRules.IsLoadoutItem(equip))
        {
            if (!TryAcquireLoadoutItem(equip, out _))
                return false;
            return true;
        }
        if (!TryAddItem(equip, out BackpackItem item) || item == null)
        {
            UIManager.Instance?.ShowToast("背包已满，无法获得装备");
            return false;
        }
        if (EquipItem(item))
            return true;
        UIManager.Instance?.ShowToast("已放入背包");
        return true;
    }

    HeroWeaponRig.HandRig GetHeroHandRig()
    {
        if (Hero.Instance != null && Hero.Instance.costumeManager != null)
            return Hero.Instance.costumeManager.HandRig;
        if (HeroCostumeManager.Instance != null)
            return HeroCostumeManager.Instance.HandRig;
        return default;
    }

    /// <summary>
    /// 穿戴装备：仍留在背包格子里显示，仅标记槽位；已装备的会在 UI 上变暗并标「已装备」。
    /// 武器槽按 HeroWeaponRig 解析：攻击武器→攻击手，盾→另一只手。
    /// </summary>
    public bool EquipItem(BackpackItem item)
    {
        if (item == null || item.equip == null) return false;
        if (Hero.Instance != null && item.equip.requireLevel > Hero.Instance.level)
        {
            UIManager.Instance?.ShowToast($"等级不足，需要{item.equip.requireLevel}级才能装备");
            return false;
        }

        var rig = GetHeroHandRig();
        EquipSlotType slot = rig.IsValid
            ? WeaponLoadoutRules.ResolveWearSlot(item.equip, rig)
            : WeaponLoadoutRules.ResolveLogicalSlot(item.equip);
        item.equip.slotType = slot;

        if (WeaponLoadoutRules.IsLoadoutItem(item.equip))
        {
            if (item.equip.weaponType == WeaponType.TwoHand)
            {
                if (rig.IsValid)
                    _equippedBySlot[rig.AttackSlot] = item.equip;
                else
                    _equippedBySlot[EquipSlotType.MainHand] = item.equip;
            }
            else
            {
                _equippedBySlot[slot] = item.equip;
            }
        }
        else
        {
            ClearSlotIfOccupied(item.equip.slotType, item.equip);
            _equippedBySlot[item.equip.slotType] = item.equip;
        }

        Hero.Instance?.RecalcAttr();
        OnBackpackChanged?.Invoke();
        NotifyCostumeChanged();
        return true;
    }

    void ClearSlotIfOccupied(EquipSlotType slot, EquipInstance keep)
    {
        if (!_equippedBySlot.TryGetValue(slot, out var old) || old == null || old == keep)
            return;
        // 双手武器占两槽：一并清掉
        if (old.weaponType == WeaponType.TwoHand)
        {
            if (_equippedBySlot.TryGetValue(EquipSlotType.MainHand, out var m) && m == old)
                _equippedBySlot.Remove(EquipSlotType.MainHand);
            if (_equippedBySlot.TryGetValue(EquipSlotType.OffHand, out var o) && o == old)
                _equippedBySlot.Remove(EquipSlotType.OffHand);
        }
        else
            _equippedBySlot.Remove(slot);
    }

    /// <summary>
    /// 卸下装备：只清槽位标记，装备继续留在背包格子。
    /// </summary>
    public bool UnequipItem(EquipSlotType slot)
    {
        if (!_equippedBySlot.TryGetValue(slot, out var equip) || equip == null) return false;

        if (equip.weaponType == WeaponType.TwoHand)
        {
            var rig = GetHeroHandRig();
            if (rig.IsValid)
            {
                if (_equippedBySlot.TryGetValue(rig.AttackSlot, out var m) && m == equip)
                    _equippedBySlot.Remove(rig.AttackSlot);
            }
            else if (_equippedBySlot.TryGetValue(EquipSlotType.MainHand, out var m) && m == equip)
                _equippedBySlot.Remove(EquipSlotType.MainHand);
        }
        else
            _equippedBySlot.Remove(slot);

        Hero.Instance?.RecalcAttr();
        OnBackpackChanged?.Invoke();
        NotifyCostumeChanged();
        return true;
    }

    /// <summary>穿脱后立刻刷新英雄 SPUM 外观（事件 + 直调，避免订阅时机漏掉）。</summary>
    void NotifyCostumeChanged()
    {
        OnCostumeChanged?.Invoke();
        var hero = Hero.Instance;
        if (hero != null && hero.costumeManager != null)
            hero.costumeManager.RefreshCostume();
        else if (HeroCostumeManager.Instance != null)
            HeroCostumeManager.Instance.RefreshCostume();
        // 城镇角色页预览（无战斗 Hero 时）
        if (CharacterUI.Instance != null)
            TownHeroCostumePreview.EnsureOn(CharacterUI.Instance)?.RefreshCostume();
    }

    public bool IsEquipped(EquipInstance equip)
    {
        if (equip == null) return false;
        foreach (var kv in _equippedBySlot)
        {
            if (kv.Value == equip) return true;
        }
        return false;
    }

    public void DecomposeItem(BackpackItem item)
    {
        if (item?.equip != null && IsEquipped(item.equip))
            UnequipItem(item.equip.slotType);
        int gold = (int)item.equip.rarity * 10 * (1 + item.equip.star);
        BattleManager.Instance.currentGold += gold;
        DropItem(item);
        UIManager.Instance?.ShowToast($"分解{item.equip.equipName}获得{gold}金币");
    }

    public List<BackpackItem> GetAllBackpackItems() => _items;

    /// <summary>
    /// 整理背包：大件靠左、小件靠右，均从上往下排，尽量腾出右侧空地。
    /// </summary>
    public void OrganizeBackpack()
    {
        if (_items.Count == 0)
        {
            OnBackpackChanged?.Invoke();
            return;
        }

        var pending = new List<BackpackItem>(_items);
        _items.Clear();
        _grid = new bool[GameConfig.BACKPACK_WIDTH, GameConfig.BACKPACK_HEIGHT];

        // 面积大优先，其次高/宽，尽量先占左侧
        pending.Sort((a, b) =>
        {
            int areaA = a.width * a.height;
            int areaB = b.width * b.height;
            if (areaA != areaB) return areaB.CompareTo(areaA);
            if (a.height != b.height) return b.height.CompareTo(a.height);
            return b.width.CompareTo(a.width);
        });

        var failed = new List<BackpackItem>();
        for (int i = 0; i < pending.Count; i++)
        {
            var bip = pending[i];
            if (bip == null || bip.equip == null) continue;
            bip.width = bip.equip.gridWidth;
            bip.height = bip.equip.gridHeight;
            if (FindEmptyPosition(bip.width, bip.height, out int x, out int y))
            {
                bip.x = x;
                bip.y = y;
                OccupyGrid(x, y, bip.width, bip.height, true);
                _items.Add(bip);
            }
            else
            {
                failed.Add(bip);
            }
        }

        // 极端情况放不下的仍尝试塞回（保持不丢装备）
        for (int i = 0; i < failed.Count; i++)
        {
            var bip = failed[i];
            if (FindEmptyPosition(bip.width, bip.height, out int x, out int y))
            {
                bip.x = x;
                bip.y = y;
                OccupyGrid(x, y, bip.width, bip.height, true);
                _items.Add(bip);
            }
            else
            {
                Debug.LogWarning($"[Backpack] 整理后无法放置: {bip.equip?.equipName}");
                _items.Add(bip);
            }
        }

        OnBackpackChanged?.Invoke();
        UIManager.Instance?.ShowToast("背包已整理");
    }

    /// <summary>
    /// 获取所有已装备的装备列表
    /// </summary>
    public List<EquipInstance> GetEquippedItems()
    {
        return _equippedBySlot.Values.Distinct().ToList();
    }

    /// <summary>
    /// 获取指定槽位的装备
    /// </summary>
    public EquipInstance GetEquippedInSlot(EquipSlotType slot)
    {
        _equippedBySlot.TryGetValue(slot, out var equip);
        return equip;
    }

    /// <summary>按逻辑主手/副手查当前武器组（映射到 HandRig 实际穿戴槽）。</summary>
    public EquipInstance GetEquippedInLogicalSlot(EquipSlotType logicalSlot)
    {
        var rig = GetHeroHandRig();
        EquipSlotType wearSlot = logicalSlot;
        if (rig.IsValid)
            wearSlot = logicalSlot == EquipSlotType.OffHand ? rig.SecondarySlot : rig.AttackSlot;
        return GetEquippedInSlot(wearSlot);
    }

    /// <summary>
    /// 获取所有已装备的属性加成
    /// </summary>
    public List<AttrBonusData> GetAllEquippedBonus()
    {
        List<AttrBonusData> allBonus = new List<AttrBonusData>();
        foreach (var equip in GetEquippedItems())
        {
            if (equip?.attrBonus == null) continue;
            float enhanceMul = EquipEnhanceSystem.GetMultiplier(equip);
            int baseCount = Mathf.Clamp(equip.baseAttrCount, 0, equip.attrBonus.Count);
            for (int i = 0; i < equip.attrBonus.Count; i++)
            {
                var b = equip.attrBonus[i];
                if (b == null) continue;
                float v = b.value;
                if (i < baseCount && enhanceMul > 1.001f)
                    v *= enhanceMul;
                allBonus.Add(new AttrBonusData
                {
                    attrType = b.attrType,
                    value = v,
                    isPercent = b.isPercent
                });
            }
            if (equip.enchants != null)
            {
                foreach (var enchant in equip.enchants)
                {
                    if (enchant == null) continue;
                    allBonus.Add(new AttrBonusData { attrType = enchant.attrType, value = enchant.value, isPercent = enchant.isPercent });
                }
            }
        }
        return allBonus;
    }

    public List<EquipInstance> GetAllItemsForLegacy()
    {
        List<EquipInstance> all = _items.Select(i => i.equip).ToList();
        all.AddRange(GetEquippedItems());
        all = all.OrderByDescending(e => e.GetSortWeight()).ToList();
        return all;
    }
}