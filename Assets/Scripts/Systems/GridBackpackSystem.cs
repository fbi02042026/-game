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

    public bool TryAddItem(EquipInstance equip, out BackpackItem item)
    {
        item = null;
        if (equip.requireLevel > Hero.Instance.level)
        {
            UIManager.Instance?.ShowToast($"等级不足！{equip.equipName}需要{equip.requireLevel}级才能装备");
        }
        if (FindEmptyPosition(equip.gridWidth, equip.gridHeight, out int x, out int y))
        {
            item = new BackpackItem
            {
                equip = equip,
                x = x, y = y,
                width = equip.gridWidth, height = equip.gridHeight
            };
            OccupyGrid(x, y, equip.gridWidth, equip.gridHeight, true);
            _items.Add(item);
            OnBackpackChanged?.Invoke();
            return true;
        }
        UIManager.Instance?.ShowToast($"背包空间不足！{equip.equipName}需要{equip.gridWidth}x{equip.gridHeight}格空间");
        return false;
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
        OccupyGrid(item.x, item.y, item.width, item.height, false);
        _items.Remove(item);
        Hero.Instance.RecalcAttr();
        OnBackpackChanged?.Invoke();
    }

    /// <summary>
    /// 穿戴装备（按槽位）
    /// </summary>
    public bool EquipItem(BackpackItem item)
    {
        if (item.equip.requireLevel > Hero.Instance.level)
        {
            UIManager.Instance?.ShowToast($"等级不足，需要{item.equip.requireLevel}级才能装备");
            return false;
        }

        EquipSlotType slot = item.equip.slotType;

        // 双手武器检查：占主手+副手
        if (item.equip.weaponType == WeaponType.TwoHand)
        {
            if (_equippedBySlot.ContainsKey(EquipSlotType.MainHand) || _equippedBySlot.ContainsKey(EquipSlotType.OffHand))
            {
                UIManager.Instance?.ShowToast("双手武器需要主手和副手都为空");
                return false;
            }
        }

        // 盾牌只能配单手武器
        if (slot == EquipSlotType.OffHand && item.equip.weaponType == WeaponType.None)
        {
            // 副手装备（如盾牌），检查主手是否有双手武器
            if (_equippedBySlot.TryGetValue(EquipSlotType.MainHand, out var mainHand))
            {
                if (mainHand.weaponType == WeaponType.TwoHand)
                {
                    UIManager.Instance?.ShowToast("双手武器已占用副手位置");
                    return false;
                }
            }
        }

        // 同槽位卸下旧装备
        if (_equippedBySlot.TryGetValue(slot, out var oldEquip))
        {
            if (!TryAddItem(oldEquip, out _))
            {
                UIManager.Instance?.ShowToast("背包空间不足，无法替换装备");
                return false;
            }
        }

        // 双手武器额外占副手槽
        if (item.equip.weaponType == WeaponType.TwoHand)
        {
            if (_equippedBySlot.TryGetValue(EquipSlotType.OffHand, out var oldOffHand))
            {
                if (!TryAddItem(oldOffHand, out _))
                {
                    UIManager.Instance?.ShowToast("背包空间不足，无法替换副手装备");
                    return false;
                }
            }
            _equippedBySlot[EquipSlotType.OffHand] = item.equip; // 标记副手被双手武器占用
        }

        OccupyGrid(item.x, item.y, item.width, item.height, false);
        _items.Remove(item);
        _equippedBySlot[slot] = item.equip;
        Hero.Instance.RecalcAttr();
        OnBackpackChanged?.Invoke();
        OnCostumeChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 卸下装备
    /// </summary>
    public bool UnequipItem(EquipSlotType slot)
    {
        if (!_equippedBySlot.TryGetValue(slot, out var equip)) return false;

        // 如果是卸下双手武器，同时清理副手标记
        if (equip.weaponType == WeaponType.TwoHand && _equippedBySlot.TryGetValue(EquipSlotType.OffHand, out var offHand))
        {
            if (offHand == equip)
            {
                _equippedBySlot.Remove(EquipSlotType.OffHand);
            }
        }

        if (TryAddItem(equip, out _))
        {
            _equippedBySlot.Remove(slot);
            Hero.Instance.RecalcAttr();
            OnBackpackChanged?.Invoke();
            OnCostumeChanged?.Invoke();
            return true;
        }
        return false;
    }

    public void DecomposeItem(BackpackItem item)
    {
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

    /// <summary>
    /// 获取所有已装备的属性加成
    /// </summary>
    public List<AttrBonusData> GetAllEquippedBonus()
    {
        List<AttrBonusData> allBonus = new List<AttrBonusData>();
        foreach (var equip in GetEquippedItems())
        {
            allBonus.AddRange(equip.attrBonus);
            foreach (var enchant in equip.enchants)
                allBonus.Add(new AttrBonusData { attrType = enchant.attrType, value = enchant.value, isPercent = enchant.isPercent });
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