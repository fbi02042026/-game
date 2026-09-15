using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 底部网格背包：建格、占位着色、屏幕坐标拾取、拾取态外框。
/// BattleUI 的 partial 分部，与 BattleUI.cs 同属一个类，成员签名保持原名。
/// </summary>
public partial class BattleUI : MonoBehaviour
{
    void EnsureGridCellsBound()
    {
        Transform grid = FindDeepChildIgnoreCase(transform, "GridContainer");
        if (grid == null) return;
        if (gridLayout == null) gridLayout = grid.GetComponent<GridLayoutGroup>();

        // 用户已在 GridContainer 下放了底行锁图案 LockedOverlay，不要再造黑色遮罩
        if (_backpackRowLock == null)
        {
            for (int i = 0; i < grid.childCount; i++)
            {
                Transform c = grid.GetChild(i);
                if (c.name.IndexOf("Locked", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || c.name.Equals("Lock", System.StringComparison.OrdinalIgnoreCase))
                {
                    _backpackRowLock = c.gameObject;
                    break;
                }
            }
        }

        // 新预制体：GridContainer 下混着 bg / BackpackTitle 等装饰节点，不能当格子用
        bool hasNamedCells = false;
        for (int i = 0; i < grid.childCount; i++)
        {
            var c0 = grid.GetChild(i);
            if (c0 != null && c0.name.StartsWith("Cell_", System.StringComparison.OrdinalIgnoreCase))
            {
                hasNamedCells = true;
                break;
            }
        }

        var list = new List<GridCellUI>();
        int cellIndex = 0;
        for (int i = 0; i < grid.childCount; i++)
        {
            Transform cell = grid.GetChild(i);
            // 跳过整行锁遮罩节点
            if (cell.name.IndexOf("Locked", System.StringComparison.OrdinalIgnoreCase) >= 0
                || cell.name.Equals("Lock", System.StringComparison.OrdinalIgnoreCase))
                continue;

            string n = cell.name;
            if (hasNamedCells && !n.StartsWith("Cell_", System.StringComparison.OrdinalIgnoreCase))
                continue;
            if (n.IndexOf("Backpack", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.Equals("bg", System.StringComparison.OrdinalIgnoreCase))
                continue;

            int gx = cellIndex % GameConfig.BACKPACK_WIDTH;
            int gy = cellIndex / GameConfig.BACKPACK_WIDTH;
            if (n.StartsWith("Cell_", System.StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = n.Split('_');
                if (parts.Length >= 3
                    && int.TryParse(parts[1], out int px)
                    && int.TryParse(parts[2], out int py))
                {
                    gx = px;
                    gy = py;
                }
            }
            cellIndex++;

            var ui = new GridCellUI
            {
                root = cell.gameObject,
                cellBg = FindImageNamed(cell, "CellBg", "Bg", "Background")
                    ?? cell.GetComponent<Image>(),
                itemIcon = FindImageNamed(cell, "Icon", "ItemIcon"),
                rarityFrame = FindImageNamed(cell, "Frame", "Rarity", "Border"),
                lockedOverlay = FindDeepChildIgnoreCase(cell, "LockedOverlay")?.gameObject
                    ?? FindDeepChildIgnoreCase(cell, "Locked")?.gameObject,
                gridX = gx,
                gridY = gy
            };
            ui.CaptureDefaultVisual();
            list.Add(ui);
        }
        gridCells = list;
    }

    GameObject _backpackRowLock; // GridContainer 下用户放的底行锁图案

    /// <summary>刷新下方网格背包。现在是 4×3=12 格且默认全开，
    /// 只有当解锁行数少于总行数时（以后加行）才会亮底行锁图案。</summary>
    public void UpdateBackpackGrid()
    {
        // 战斗中捡到装备时可能还没绑过格子，先补绑再判空
        if (gridCells == null || gridCells.Count == 0)
            EnsureGridCellsBound();
        if (gridCells == null || gridCells.Count == 0)
        {
            Debug.LogWarning("[BattleUI] 背包格子未绑定（缺 GridContainer），装备无法显示");
            return;
        }

        int unlockedRows = GameConfig.GetUnlockedBackpackRows(SaveSystem.Instance?.Data);
        bool bottomLocked = unlockedRows < GameConfig.BACKPACK_HEIGHT;

        // 整行锁图案（GridContainer/LockedOverlay）
        if (_backpackRowLock != null)
            _backpackRowLock.SetActive(bottomLocked);

        foreach (var cell in gridCells)
        {
            if (cell == null) continue;
            bool rowLocked = cell.gridY >= unlockedRows;
            cell.SetRowLocked(rowLocked);
            if (!rowLocked)
                cell.Clear();
        }

        var bag = GridBackpackSystem.Instance;
        if (bag == null) return;

        var items = bag.GetAllBackpackItems();
        if (items == null) return;

        var placements = new List<BackpackGridVisual.ItemPlacement>();
        Transform grid = FindDeepChildIgnoreCase(transform, "GridContainer");
        var gridRt = grid as RectTransform;
        foreach (var bip in items)
        {
            if (bip == null) continue;
            if (bip.y >= unlockedRows) continue;

            // 道具：2026-09-15 起背包 12 格只装道具，永远 1×1
            if (bip.item != null)
            {
                placements.Add(new BackpackGridVisual.ItemPlacement
                {
                    x = bip.x, y = bip.y, w = 1, h = 1, item = bip.item
                });
                continue;
            }

            // 装备：新流程下已不再入包，但兼容旧存档/历史数据
            if (bip.equip == null) continue;
            placements.Add(new BackpackGridVisual.ItemPlacement
            {
                x = bip.x, y = bip.y, w = bip.width, h = bip.height, equip = bip.equip,
                equipped = bag.IsEquipped(bip.equip)
            });
        }
        // 传入真实格子：没有 GridLayoutGroup（格子是美术手摆的）时也能算对位置
        BackpackGridVisual.ClearAndPlace(gridRt, gridLayout, placements, FindGridCellRect, BattleLootMode.Active);
        ApplyBackpackCellOccupiedColors(placements);
        // 换装后同步底部 5 个装备快捷槽
        UpdateEquipQuickSlots();
        Debug.Log($"[BattleUI] 背包刷新 items={placements.Count} cells={gridCells.Count} layout={(gridLayout != null)}");
    }

    public bool TryScreenToBackpackCell(Vector2 screenPos, Camera eventCam, out int cellX, out int cellY)
    {
        cellX = 0;
        cellY = 0;
        if (gridCells == null) return false;
        for (int i = 0; i < gridCells.Count; i++)
        {
            var c = gridCells[i];
            if (c == null) continue;
            var rt = c.VisualRect;
            if (rt == null) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, eventCam))
            {
                cellX = c.gridX;
                cellY = c.gridY;
                return true;
            }
        }
        return false;
    }

    public void RefreshLootModeChrome()
    {
        // 阶段切换时收掉可能还开着的道具操作浮层，避免浮在下一阶段界面上
        BackpackItemActionUI.Instance?.Hide();

        if (lootConfirmButton != null)
        {
            var txt = lootConfirmButton.GetComponentInChildren<Text>(true);
            if (txt != null)
                txt.text = "确定";
            // 整理功能已移除：这个按钮只在拾取模式出现
            lootConfirmButton.gameObject.SetActive(BattleLootMode.Active);
        }
        UpdateBackpackGrid();
        // 整理阶段才允许拖动技槽调序（战斗中不开放，避免误触改掉释放优先级）
        RefreshSkillSlotDragState();
        if (BattleLootMode.Active) MaybeShowSkillReorderHint();
        BattleJoystick.Instance?.SetVisible(!BattleLootMode.Active
            && BattleManager.Instance != null
            && BattleManager.Instance.isInBattle
            && BattleManager.Instance.UnitsCanAct);
    }

    /// <summary>首次进入整理阶段且确实有得排（≥2 个技能）时提示一次，之后不再打扰。</summary>
    const string SKILL_REORDER_HINT_KEY = "hint_skill_reorder_shown";

    void MaybeShowSkillReorderHint()
    {
        var ids = RunLoadout.SkillIds();
        if (ids == null || ids.Count < 2) return;      // 1 个技能没什么好排的
        if (PlayerPrefs.GetInt(SKILL_REORDER_HINT_KEY, 0) != 0) return;
        PlayerPrefs.SetInt(SKILL_REORDER_HINT_KEY, 1);
        PlayerPrefs.Save();
        UIManager.Instance?.ShowToast("整理阶段可拖动技能，调整自动释放顺序");
    }

    public void EnsureBattleControls()
    {
        FocusMarkSystem.Ensure();
        BattleJoystick.EnsureOn(transform);
        // 道具操作浮层挂在 BattleUI 下（保证在 Canvas 内且在最上层）
        BackpackItemActionUI.Ensure(transform);
        RefreshLootModeChrome();
    }

    void ApplyBackpackCellOccupiedColors(List<BackpackGridVisual.ItemPlacement> placements)
    {
        if (gridCells == null) return;
        foreach (var cell in gridCells)
        {
            if (cell == null) continue;
            cell.SetEmptyVisual();
        }
        if (placements == null) return;
        for (int i = 0; i < placements.Count; i++)
        {
            var p = placements[i];
            for (int dx = 0; dx < p.w; dx++)
            for (int dy = 0; dy < p.h; dy++)
            {
                var cell = FindGridCell(p.x + dx, p.y + dy);
                cell?.SetOccupiedVisual(p.equipped);
            }
        }
    }

    GridCellUI FindGridCell(int gx, int gy)
    {
        if (gridCells == null) return null;
        for (int i = 0; i < gridCells.Count; i++)
        {
            var c = gridCells[i];
            if (c != null && c.gridX == gx && c.gridY == gy)
                return c;
        }
        return null;
    }

    /// <summary>按格子坐标取真实格子的 RectTransform，供多格装备量取实际占位。</summary>
    RectTransform FindGridCellRect(int gx, int gy)
    {
        if (gridCells == null) return null;
        for (int i = 0; i < gridCells.Count; i++)
        {
            var c = gridCells[i];
            if (c != null && c.gridX == gx && c.gridY == gy)
                return c.VisualRect;
        }
        return null;
    }

    /// <summary>拾取模式的「确定」。整理背包已按需求移除，这里只负责确认。</summary>
    void OnLootConfirm()
    {
        if (BattleLootMode.Active)
            BattleLootMode.Confirm();
    }
}
