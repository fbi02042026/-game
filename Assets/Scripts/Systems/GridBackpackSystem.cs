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
        if (item == null) return;
        if (item.equip != null && IsEquipped(item.equip))
            UnequipItem(item.equip.slotType);
        OccupyGrid(item.x, item.y, item.width, item.height, false);
        _items.Remove(item);
        Hero.Instance?.RecalcAttr();
        OnBackpackChanged?.Invoke();
    }

    /// <summary>通关奖励：先入包再尝试穿戴（同槽自动替换）</summary>
    public bool TryEquipFromReward(EquipInstance equip)
    {
        if (equip == null) return false;
        if (!TryAddItem(equip, out BackpackItem item) || item == null)
        {
            UIManager.Instance?.ShowToast("背包已满，无法获得装备");
            return false;
        }
        if (EquipItem(item))
            return true;
        // 穿戴失败仍留在背包
        UIManager.Instance?.ShowToast("已放入背包");
        return true;
    }

    /// <summary>
    /// 穿戴装备：仍留在背包格子里显示，仅标记槽位；已装备的会在 UI 上变暗并标「已装备」。
    /// </summary>
    public bool EquipItem(BackpackItem item)
    {
        if (item == null || item.equip == null) return false;
        if (Hero.Instance != null && item.equip.requireLevel > Hero.Instance.level)
        {
            UIManager.Instance?.ShowToast($"等级不足，需要{item.equip.requireLevel}级才能装备");
            return false;
        }

        EquipSlotType slot = item.equip.slotType;

        // 盾牌 / 副手：主手若是双手武器则冲突
        if (slot == EquipSlotType.OffHand && item.equip.weaponType == WeaponType.None)
        {
            if (_equippedBySlot.TryGetValue(EquipSlotType.MainHand, out var mainHand)
                && mainHand != null && mainHand.weaponType == WeaponType.TwoHand
                && mainHand != item.equip)
            {
                UIManager.Instance?.ShowToast("双手武器已占用副手位置");
                return false;
            }
        }

        // 双手武器：先清掉主手/副手旧装备标记（装备本身仍在背包）
        if (item.equip.weaponType == WeaponType.TwoHand)
        {
            ClearSlotIfOccupied(EquipSlotType.MainHand, item.equip);
            ClearSlotIfOccupied(EquipSlotType.OffHand, item.equip);
            _equippedBySlot[EquipSlotType.MainHand] = item.equip;
            _equippedBySlot[EquipSlotType.OffHand] = item.equip;
        }
        else
        {
            ClearSlotIfOccupied(slot, item.equip);
            // 若新装主手，且副手被旧双手占用，清副手标记
            if (slot == EquipSlotType.MainHand
                && _equippedBySlot.TryGetValue(EquipSlotType.OffHand, out var off)
                && off != null && off.weaponType == WeaponType.TwoHand)
            {
                _equippedBySlot.Remove(EquipSlotType.OffHand);
                if (_equippedBySlot.TryGetValue(EquipSlotType.MainHand, out var mh) && mh == off)
                    _equippedBySlot.Remove(EquipSlotType.MainHand);
            }
            _equippedBySlot[slot] = item.equip;
        }

        Hero.Instance?.RecalcAttr();
        OnBackpackChanged?.Invoke();
        OnCostumeChanged?.Invoke();
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
            if (_equippedBySlot.TryGetValue(EquipSlotType.MainHand, out var m) && m == equip)
                _equippedBySlot.Remove(EquipSlotType.MainHand);
            if (_equippedBySlot.TryGetValue(EquipSlotType.OffHand, out var o) && o == equip)
                _equippedBySlot.Remove(EquipSlotType.OffHand);
        }
        else
            _equippedBySlot.Remove(slot);

        Hero.Instance?.RecalcAttr();
        OnBackpackChanged?.Invoke();
        OnCostumeChanged?.Invoke();
        return true;
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