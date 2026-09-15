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
        /// <summary>
        /// 道具。2026-09-15：背包 12 格改为**只装道具**——
        /// 装备直接穿、替换下来的自动分解，不再占格子。
        /// </summary>
        public ItemInstance item;
        public int x;
        public int y;
        public int width;
        public int height;

        /// <summary>这一格装的是道具（而非装备）。</summary>
        public bool IsItem => item != null;
    }

    /// <summary>撤离/死亡回城：清空局内背包与已装备（裂缝装备不带出）。</summary>
    public void ClearRunEquipment()
    {
        InitNewRun();
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

    // ===== 道具（2026-09-15：背包格子只装道具，装备不再入包）=====

    /// <summary>
    /// 往背包放一份道具，能叠就叠（受 stackMax 限制），全部放不下返回 false。
    /// 道具一律 1×1。
    /// </summary>
    public bool TryAddItemStack(string defId, int count, out BackpackItem placed)
    {
        placed = null;
        if (string.IsNullOrEmpty(defId) || count <= 0) return false;

        var def = ItemDefs.Get(defId);
        if (def == null)
        {
            Debug.LogWarning($"[背包] 未知道具 id：{defId}（道具表里没有这一行）");
            return false;
        }

        int left = count;

        // 1) 先往已有堆叠里塞
        if (def.stackMax > 1)
        {
            for (int i = 0; i < _items.Count && left > 0; i++)
            {
                var it = _items[i];
                if (it?.item == null || it.item.defId != defId) continue;
                if (it.item.count >= def.stackMax) continue;
                int add = Mathf.Min(def.stackMax - it.item.count, left);
                it.item.count += add;
                left -= add;
                if (placed == null) placed = it;
            }
        }

        // 2) 剩下的开新格
        while (left > 0)
        {
            if (!FindEmptyPosition(1, 1, out int x, out int y)) break;
            int add = def.stackMax > 1 ? Mathf.Min(def.stackMax, left) : 1;
            var bi = new BackpackItem
            {
                item = new ItemInstance(defId, add),
                x = x, y = y, width = 1, height = 1
            };
            OccupyGrid(x, y, 1, 1, true);
            _items.Add(bi);
            left -= add;
            if (placed == null) placed = bi;
        }

        if (placed == null) return false;
        NotifyBackpackChanged();
        return true;
    }

    /// <summary>
    /// 使用一份道具。具体效果等道具内容定稿后按 def.useEffect 派发（见 ItemDef.useEffect 注释），
    /// 这里只负责判定可用性与扣数量。
    /// </summary>
    public bool UseItem(BackpackItem bi)
    {
        if (bi?.item == null) return false;
        var def = bi.item.Def;
        if (def == null || !def.IsUsable) return false;

        // TODO(道具内容定稿后)：按 def.useEffect 派发，如
        //   heal_hp:30     -> Hero.Instance 回血 30
        //   buff_atk:20:15 -> 攻击 +20，持续 15 秒
        Debug.Log($"[背包] 使用道具 {def.name}（{def.useEffect}）——效果尚未实现，先扣数量");
        ConsumeItem(bi, 1);
        return true;
    }

    /// <summary>整格丢弃。任务道具等 canDrop=false 的返回 false（UI 应直接不显示丢弃按钮）。</summary>
    public bool DropItemStack(BackpackItem bi)
    {
        if (bi?.item == null) return false;
        var def = bi.item.Def;
        if (def != null && !def.canDrop) return false;
        RemoveItemCell(bi);
        NotifyBackpackChanged();
        return true;
    }

    void ConsumeItem(BackpackItem bi, int n)
    {
        if (bi?.item == null) return;
        bi.item.count -= n;
        if (bi.item.count > 0)
        {
            NotifyBackpackChanged();
            return;
        }
        RemoveItemCell(bi);
        NotifyBackpackChanged();
    }

    void RemoveItemCell(BackpackItem bi)
    {
        if (bi == null) return;
        OccupyGrid(bi.x, bi.y, bi.width, bi.height, false);
        _items.Remove(bi);
    }

    void NotifyBackpackChanged()
    {
        OnBackpackChanged?.Invoke();
        BattleUI.Instance?.UpdateBackpackGrid();
    }

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

        // 防具：同部位唯一
        return TryAddUniqueBySlot(equip, out item);
    }

    /// <summary>
    /// 武器组入包：按 weaponHand 替换对应部位；双手清空主+副；旧件直接变强化石。只入包不穿槽。
    /// </summary>
    public bool TryAcquireLoadoutItem(EquipInstance equip, out BackpackItem item)
    {
        item = null;
        if (equip == null) return false;

        WeaponLoadoutRules.GetSlotsToReplace(equip, out bool clearMain, out bool clearOff);

        if (clearMain)
            ConsumeBagLogicalWeapon(EquipSlotType.MainHand);
        if (clearOff)
            ConsumeBagLogicalWeapon(EquipSlotType.OffHand);

        equip.slotType = WeaponLoadoutRules.ResolveLogicalSlot(equip);
        if (!FindEmptyPosition(equip.gridWidth, equip.gridHeight, out int x, out int y))
        {
            UIManager.Instance?.ShowToast($"背包空间不足！{equip.equipName}需要{equip.gridWidth}x{equip.gridHeight}格空间");
            return false;
        }

        item = PlaceItemInGrid(equip, x, y);
        Hero.Instance?.RecalcAttr();
        OnBackpackChanged?.Invoke();
        NotifyCostumeChanged();
        AchievementSystem.Instance?.OnObtainEquip(equip.rarity);
        AdventureLogAchievements.OnEquipPicked();
        return true;
    }

    /// <summary>掉落/通关选中：同部位最多 1 件，只进背包（无穿戴槽）。</summary>
    public bool TryAddUniqueBySlot(EquipInstance equip, out BackpackItem item)
    {
        item = null;
        if (equip == null) return false;
        if (WeaponLoadoutRules.IsLoadoutItem(equip))
            return TryAcquireLoadoutItem(equip, out item);

        // 同部位防具：旧件折强化石
        var old = FindBagEquipByArmorSlot(equip.slotType);
        if (old != null && old != equip)
            ScrapBagEquip(old);

        if (FindEmptyPosition(equip.gridWidth, equip.gridHeight, out int x, out int y))
        {
            item = PlaceItemInGrid(equip, x, y);
            Hero.Instance?.RecalcAttr();
            OnBackpackChanged?.Invoke();
            NotifyCostumeChanged();
            AchievementSystem.Instance?.OnObtainEquip(equip.rarity);
            AdventureLogAchievements.OnEquipPicked();
            return true;
        }
        UIManager.Instance?.ShowToast($"背包空间不足！{equip.equipName}需要{equip.gridWidth}x{equip.gridHeight}格空间");
        return false;
    }

    void ConsumeBagLogicalWeapon(EquipSlotType logicalSlot)
    {
        var old = GetEquippedInLogicalSlot(logicalSlot);
        if (old != null)
            ScrapBagEquip(old);
    }

    // 背包已满时自动让位：与「替换/丢掉直接分解」是同一套，统一走 ScrapEquip
    void ScrapBagEquip(EquipInstance equip) => ScrapEquip(equip);

    /// <summary>同部位替换折强化石：按当前职业给一点口吻。</summary>
    static string BuildScrapToast(EquipInstance equip, int mats)
    {
        string name = equip != null && !string.IsNullOrEmpty(equip.equipName) ? equip.equipName : "旧装备";
        string matsTxt = mats > 0 ? $" ×{mats}" : "";
        switch (PlayerJobDefs.GetSelected())
        {
            case PlayerJobId.SwordShield:
                return $"盾墙换防！{name} 拆成强化石{matsTxt}";
            case PlayerJobId.Heavy:
                return $"沉甸甸的不要了——{name} 砸成强化石{matsTxt}";
            case PlayerJobId.Berserker:
                return $"旧货碍手！{name} 撕成强化石{matsTxt}";
            case PlayerJobId.Ranger:
                return $"换新的更好射——{name} 拆成强化石{matsTxt}";
            case PlayerJobId.Mage:
                return $"魔力重组：{name} → 强化石{matsTxt}";
            case PlayerJobId.Priest:
                return $"旧物归尘，化为强化石{matsTxt}（原：{name}）";
            default:
                return $"{name} 已变为强化石{matsTxt}";
        }
    }

    EquipInstance FindBagEquipByArmorSlot(EquipSlotType slot)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            var e = _items[i]?.equip;
            if (e == null || WeaponLoadoutRules.IsLoadoutItem(e)) continue;
            if (e.slotType == slot) return e;
        }
        return null;
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
        UIManager.Instance?.ShowToast(BuildScrapToast(equip, mats));
        Hero.Instance?.RecalcAttr();
    }

    public BackpackItem FindItem(EquipInstance equip)
    {
        if (equip == null) return null;
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i]?.equip == equip) return _items[i];
        }
        return null;
    }

    /// <summary>按道具实例回查背包条目。overlay 重建后旧引用可能失效，用实例比对最稳。</summary>
    public BackpackItem FindItemByItem(ItemInstance it)
    {
        if (it == null) return null;
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i] != null && ReferenceEquals(_items[i].item, it)) return _items[i];
        }
        return null;
    }

    BackpackItem FindBackpackItemByEquip(EquipInstance equip) => FindItem(equip);

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

    /// <summary>通关奖励：只入包（同部位唯一），不穿槽。</summary>
    public bool TryEquipFromReward(EquipInstance equip)
    {
        if (equip == null) return false;
        if (!TryAddUniqueBySlot(equip, out _))
        {
            UIManager.Instance?.ShowToast("背包已满，无法获得装备");
            return false;
        }
        UIManager.Instance?.ShowToast("已放入背包");
        return true;
    }

    HeroWeaponRig.HandRig GetHeroHandRig()
    {
        if (Hero.Instance != null && Hero.Instance.costumeManager != null)
        {
            Hero.Instance.costumeManager.EnsureRigReady();
            return Hero.Instance.costumeManager.HandRig;
        }
        if (HeroCostumeManager.Instance != null)
        {
            HeroCostumeManager.Instance.EnsureRigReady();
            return HeroCostumeManager.Instance.HandRig;
        }
        var townPreview = TownHeroCostumePreview.ActiveCostumeManager;
        if (townPreview != null)
        {
            townPreview.EnsureRigReady();
            return townPreview.HandRig;
        }
        return default;
    }

    /// <summary>
    /// HandRig 就绪后把误挂在逻辑槽位的武器挪到攻击手/副手实际槽，修复「主手空、副手双剑」。
    /// </summary>
    public void RemapWeaponWearSlots(in HeroWeaponRig.HandRig rig)
    {
        if (!rig.IsValid || _equippedBySlot.Count == 0) return;

        var moves = new List<(EquipInstance eq, EquipSlotType from, EquipSlotType to)>();
        foreach (var kv in _equippedBySlot)
        {
            EquipInstance eq = kv.Value;
            if (!WeaponLoadoutRules.IsLoadoutItem(eq)) continue;

            EquipSlotType target = WeaponLoadoutRules.ResolveWearSlot(eq, rig);
            if (eq.weaponType == WeaponType.TwoHand)
                target = rig.AttackSlot;
            if (kv.Key == target) continue;
            moves.Add((eq, kv.Key, target));
        }

        if (moves.Count == 0) return;

        for (int i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            if (!_equippedBySlot.TryGetValue(move.from, out var current) || current != move.eq)
                continue;
            _equippedBySlot.Remove(move.from);
        }

        for (int i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            _equippedBySlot[move.to] = move.eq;
            move.eq.slotType = move.to;
        }

        Debug.Log($"[GridBackpack] 武器穿戴槽已按 HandRig 重映射 {moves.Count} 件");
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

        // #region agent log
        if (WeaponLoadoutRules.IsLoadoutItem(item.equip))
        {
            DebugAgentLog.Log("H8", "GridBackpackSystem.EquipItem", "weapon_equip",
                $"{{\"wearSlot\":\"{slot}\",\"weaponHand\":\"{item.equip.weaponHand}\",\"rigAttack\":\"{(rig.IsValid ? rig.AttackSlot.ToString() : "invalid")}\",\"rigSecondary\":\"{(rig.IsValid ? rig.SecondarySlot.ToString() : "invalid")}\",\"tpl\":\"{item.equip.templateId}\"}}");
        }
        // #endregion

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
            PurgeStaleWeaponSlotKeys(item.equip, slot, rig);
            if (rig.IsValid)
                RemapWeaponWearSlots(rig);
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

    /// <summary>
    /// 掉落/奖励装备「直接穿上」，**不进背包格子**。
    /// 2026-09-15 需求：装备永不进背包，12 个格子留给道具；被替换下来的旧件自动分解。
    /// 复用 EquipItem 的穿戴逻辑（含武器实际槽位解析与 SPUM 时装刷新），这里只包一层。
    /// </summary>
    public bool TryEquipDirect(EquipInstance equip)
    {
        if (equip == null) return false;

        var rig = GetHeroHandRig();
        EquipSlotType slot = rig.IsValid
            ? WeaponLoadoutRules.ResolveWearSlot(equip, rig)
            : WeaponLoadoutRules.ResolveLogicalSlot(equip);
        // 换下谁，先记下来（穿成功后再分解它）
        var old = GetEquippedInLogicalSlot(slot);

        var tmp = new BackpackItem
        {
            equip = equip,
            x = 0, y = 0,
            width = equip.gridWidth,
            height = equip.gridHeight
        };
        if (!EquipItem(tmp)) return false;

        // 与入包穿戴同一套统计：拿到装备就算数（不进背包也要记）
        AchievementSystem.Instance?.OnObtainEquip(equip.rarity);
        AdventureLogAchievements.OnEquipPicked();

        if (old != null && old != equip)
            ScrapEquip(old);

        return true;
    }

    /// <summary>
    /// 直接分解一件装备，**不要求它在背包里**（替换下来的旧件、没穿的掉落件都走这里）。
    /// 给强化石；不会回流背包。分解即消失，不做二次确认（2026-09-15 需求）。
    /// </summary>
    public void ScrapEquip(EquipInstance equip)
    {
        if (equip == null) return;

        // 万一它还在背包格子里，先把格子清出来
        var bi = FindBackpackItemByEquip(equip);
        if (bi != null)
        {
            OccupyGrid(bi.x, bi.y, bi.width, bi.height, false);
            _items.Remove(bi);
        }
        // 清掉可能残留的已装备标记
        var keys = new List<EquipSlotType>(_equippedBySlot.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            if (_equippedBySlot.TryGetValue(keys[i], out var cur) && cur == equip)
                _equippedBySlot.Remove(keys[i]);
        }

        int mats = WeaponLoadoutRules.CalcDecomposeMats(equip);
        WeaponLoadoutRules.GrantDecomposeMats(equip, save: false);
        UIManager.Instance?.ShowToast(BuildScrapToast(equip, mats));
        OnBackpackChanged?.Invoke();
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

    void PurgeStaleWeaponSlotKeys(EquipInstance equip, EquipSlotType wearSlot, HeroWeaponRig.HandRig rig)
    {
        if (equip == null) return;
        var keys = new List<EquipSlotType>(_equippedBySlot.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            EquipSlotType key = keys[i];
            if (key == wearSlot) continue;
            if (_equippedBySlot.TryGetValue(key, out var cur) && cur == equip)
                _equippedBySlot.Remove(key);
        }
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
        if (equip == null || !WeaponLoadoutRules.IsLoadoutItem(equip)) return false;
        var logical = WeaponLoadoutRules.ResolveLogicalSlot(equip);
        return GetEquippedInLogicalSlot(logical) == equip;
    }

    /// <summary>
    /// 获取所有生效装备（包内全部；同部位唯一约束下即当前套）。
    /// </summary>
    public List<EquipInstance> GetEquippedItems()
    {
        var list = new List<EquipInstance>();
        var seen = new HashSet<EquipInstance>();
        for (int i = 0; i < _items.Count; i++)
        {
            var e = _items[i]?.equip;
            if (e == null || !seen.Add(e)) continue;
            list.Add(e);
        }
        return list;
    }

    /// <summary>
    /// 获取指定槽位的装备（读背包，无穿戴字典）。
    /// </summary>
    public EquipInstance GetEquippedInSlot(EquipSlotType slot)
    {
        var rig = GetHeroHandRig();
        if (rig.IsValid)
        {
            if (slot == rig.AttackSlot)
                return GetEquippedInLogicalSlot(EquipSlotType.MainHand);
            if (slot == rig.SecondarySlot)
                return GetEquippedInLogicalSlot(EquipSlotType.OffHand);
        }

        for (int i = 0; i < _items.Count; i++)
        {
            var e = _items[i]?.equip;
            if (e == null) continue;
            if (e.slotType == slot) return e;
        }
        return null;
    }

    /// <summary>按逻辑主手/副手查包内武器。</summary>
    public EquipInstance GetEquippedInLogicalSlot(EquipSlotType logicalSlot)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            var e = _items[i]?.equip;
            if (e == null || !WeaponLoadoutRules.IsLoadoutItem(e)) continue;
            if (MatchesLogicalWeaponSlot(e, logicalSlot))
                return e;
        }
        return null;
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

    /// <summary>开箱整理：把物品挪到新格子（不重叠）。</summary>
    public bool TryMoveItem(BackpackItem item, int newX, int newY)
    {
        if (item == null || (item.equip == null && item.item == null)) return false;
        int unlockedRows = GameConfig.GetUnlockedBackpackRows(SaveSystem.Instance?.Data);
        int maxY = Mathf.Min(GameConfig.BACKPACK_HEIGHT, unlockedRows);
        if (newX < 0 || newY < 0 || newX + item.width > GameConfig.BACKPACK_WIDTH || newY + item.height > maxY)
            return false;

        OccupyGrid(item.x, item.y, item.width, item.height, false);
        if (!IsAreaEmpty(newX, newY, item.width, item.height))
        {
            OccupyGrid(item.x, item.y, item.width, item.height, true);
            return false;
        }
        item.x = newX;
        item.y = newY;
        OccupyGrid(newX, newY, item.width, item.height, true);
        OnBackpackChanged?.Invoke();
        return true;
    }

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

    static bool MatchesLogicalWeaponSlot(EquipInstance eq, EquipSlotType logicalSlot)
    {
        if (eq == null || !WeaponLoadoutRules.IsLoadoutItem(eq)) return false;
        bool offHandRole = WeaponLoadoutRules.IsShield(eq) || WeaponLoadoutRules.IsOffHandWeapon(eq);
        if (logicalSlot == EquipSlotType.OffHand) return offHandRole;
        return !offHandRole;
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
        List<EquipInstance> all = _items.Select(i => i.equip).Where(e => e != null).Distinct().ToList();
        all = all.OrderByDescending(e => e.GetSortWeight()).ToList();
        return all;
    }
}