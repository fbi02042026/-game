using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 城镇/角色页背包网格：只绑定预制体里已摆好的格子，不改布局。
/// 逻辑网格与预制体一致（8×5）；默认解锁上 3 行，最下方两行由天赋 R2/R7 扩容解锁。
/// </summary>
public class TownBackpackGrid : MonoBehaviour
{
    public const float CellSize = 82f;
    public const float CellSpacing = 0f;
    public const int Pad = 10;

    public GridLayoutGroup gridLayout;
    public RectTransform gridContainer;
    public GameObject rowLockOverlay;
    public readonly List<GridCellUI> cells = new List<GridCellUI>();

    public void BindFromHierarchy(Transform searchRoot = null)
    {
        Transform root = searchRoot != null ? searchRoot : transform;
        Transform grid = FindDeep(root, "GridContainer");
        if (grid == null) return;
        gridContainer = grid as RectTransform;
        gridLayout = grid.GetComponent<GridLayoutGroup>();
        // 不改 GridLayout 列数/间距：角色页预制体按手摆布局显示

        rowLockOverlay = null;
        cells.Clear();
        for (int i = 0; i < grid.childCount; i++)
        {
            Transform cell = grid.GetChild(i);
            if (cell.name.IndexOf("Locked", System.StringComparison.OrdinalIgnoreCase) >= 0
                || cell.name.Equals("Lock", System.StringComparison.OrdinalIgnoreCase))
            {
                if (rowLockOverlay == null) rowLockOverlay = cell.gameObject;
                continue;
            }

            int gx = i % GameConfig.BACKPACK_WIDTH;
            int gy = i / GameConfig.BACKPACK_WIDTH;
            string n = cell.name;
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

            cell.gameObject.SetActive(true);
            var ui = new GridCellUI
            {
                root = cell.gameObject,
                cellBg = FindImgNamedOnly(cell, "CellBg", "Bg", "Background")
                    ?? cell.GetComponent<Image>(),
                itemIcon = FindImgNamedOnly(cell, "ItemIcon", "Icon"),
                rarityFrame = FindImgNamedOnly(cell, "Frame", "Rarity", "Border"),
                lockedOverlay = FindDeep(cell, "LockedOverlay")?.gameObject
                    ?? FindDeep(cell, "Locked")?.gameObject,
                gridX = gx,
                gridY = gy
            };
            if (ui.lockedOverlay == null)
                ui.lockedOverlay = CreateLockOverlay(cell);
            cells.Add(ui);
        }
    }

    /// <summary>编辑器/生成器用：按战斗同款规格建 7×4 格</summary>
    public void BuildGrid(Transform bagPanel)
    {
        for (int i = bagPanel.childCount - 1; i >= 0; i--)
        {
            var c = bagPanel.GetChild(i);
            if (c.name == "GridContainer" || c.name == "BagTitle" || c.name == "CapacityText" || c.name == "CapacityPlus")
                continue;
        }

        Transform existing = bagPanel.Find("GridContainer");
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);

        var go = new GameObject("GridContainer", typeof(RectTransform), typeof(GridLayoutGroup));
        go.transform.SetParent(bagPanel, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(12f, 12f);
        rt.offsetMax = new Vector2(-12f, -48f);

        var gl = go.GetComponent<GridLayoutGroup>();
        gl.cellSize = new Vector2(CellSize, CellSize);
        gl.spacing = new Vector2(CellSpacing, CellSpacing);
        gl.padding = new RectOffset(Pad, Pad, Pad, Pad);
        gl.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gl.startAxis = GridLayoutGroup.Axis.Horizontal;
        gl.childAlignment = TextAnchor.UpperCenter;
        gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gl.constraintCount = GameConfig.BACKPACK_WIDTH;

        for (int y = 0; y < GameConfig.BACKPACK_HEIGHT; y++)
        {
            for (int x = 0; x < GameConfig.BACKPACK_WIDTH; x++)
                CreateCell(go.transform, x, y);
        }

        // 整行锁图案（与战斗一致，盖在底行）
        var rowLock = new GameObject("LockedOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        rowLock.transform.SetParent(go.transform, false);
        var lrt = rowLock.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0f, 0f);
        lrt.anchorMax = new Vector2(1f, 0f);
        lrt.pivot = new Vector2(0.5f, 0f);
        lrt.sizeDelta = new Vector2(0f, CellSize + 8f);
        lrt.anchoredPosition = Vector2.zero;
        var limg = rowLock.GetComponent<Image>();
        limg.color = new Color(0.15f, 0.12f, 0.1f, 0.55f);
        limg.raycastTarget = false;

        BindFromHierarchy(bagPanel);
    }

    static void CreateCell(Transform parent, int x, int y)
    {
        var cell = new GameObject($"Cell_{x}_{y}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        cell.transform.SetParent(parent, false);
        var bg = cell.GetComponent<Image>();
        bg.color = new Color(0.93f, 0.86f, 0.7f, 1f);

        var frame = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        frame.transform.SetParent(cell.transform, false);
        Stretch(frame.GetComponent<RectTransform>(), 2f);
        var fi = frame.GetComponent<Image>();
        fi.color = new Color(0.3f, 0.2f, 0.1f, 0.5f);
        fi.raycastTarget = false;

        var icon = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        icon.transform.SetParent(cell.transform, false);
        Stretch(icon.GetComponent<RectTransform>(), 8f);
        var ii = icon.GetComponent<Image>();
        ii.color = Color.white;
        ii.preserveAspect = true;
        ii.raycastTarget = false;
        ii.enabled = false;

        var locked = new GameObject("LockedOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        locked.transform.SetParent(cell.transform, false);
        Stretch(locked.GetComponent<RectTransform>(), 0f);
        var li = locked.GetComponent<Image>();
        li.color = new Color(0.2f, 0.18f, 0.15f, 0.75f);
        li.raycastTarget = false;
        locked.SetActive(y >= GameConfig.BACKPACK_DEFAULT_ROWS);
    }

    public void Refresh()
    {
        if (cells.Count == 0) BindFromHierarchy();
        if (cells.Count == 0) return;

        int unlockedRows = GameConfig.GetUnlockedBackpackRows(SaveSystem.Instance?.Data);
        bool bottomLocked = unlockedRows < GameConfig.BACKPACK_HEIGHT;
        if (rowLockOverlay != null)
            rowLockOverlay.SetActive(bottomLocked);

        foreach (var cell in cells)
        {
            if (cell == null) continue;
            bool rowLocked = cell.gridY >= unlockedRows;
            cell.SetRowLocked(rowLocked);
            if (!rowLocked) cell.Clear();
        }

        var placements = new List<BackpackGridVisual.ItemPlacement>();

        // 优先战斗背包；城镇无战斗背包时展示遗产池（顺序铺格）
        var bag = GridBackpackSystem.Instance;
        if (bag != null)
        {
            var items = bag.GetAllBackpackItems();
            if (items != null && items.Count > 0)
            {
                foreach (var bip in items)
                {
                    if (bip?.equip == null || bip.y >= unlockedRows) continue;
                    placements.Add(new BackpackGridVisual.ItemPlacement
                    {
                        x = bip.x, y = bip.y, w = bip.width, h = bip.height, equip = bip.equip,
                        equipped = bag.IsEquipped(bip.equip)
                    });
                }
                BackpackGridVisual.ClearAndPlace(gridContainer, gridLayout, placements, FindCellRect);
                ApplyOccupiedColors(placements);
                return;
            }
        }

        var data = SaveSystem.Instance?.Data;
        if (data?.legacyEquipPool == null)
        {
            BackpackGridVisual.ClearAndPlace(gridContainer, gridLayout, placements, FindCellRect);
            ApplyOccupiedColors(placements);
            return;
        }
        int slot = 0;
        int cap = unlockedRows * GameConfig.BACKPACK_WIDTH;
        for (int i = 0; i < data.legacyEquipPool.Count && slot < cap; i++)
        {
            var legacy = data.legacyEquipPool[i];
            if (legacy == null) continue;
            int x = slot % GameConfig.BACKPACK_WIDTH;
            int y = slot / GameConfig.BACKPACK_WIDTH;
            var eq = ToEquipInstance(legacy);
            if (eq == null) continue;
            int w = eq.gridWidth > 0 ? eq.gridWidth : 1;
            int h = eq.gridHeight > 0 ? eq.gridHeight : 1;
            if (x + w > GameConfig.BACKPACK_WIDTH || y + h > unlockedRows) continue;
            placements.Add(new BackpackGridVisual.ItemPlacement { x = x, y = y, w = w, h = h, equip = eq });
            slot += w * h;
        }
        BackpackGridVisual.ClearAndPlace(gridContainer, gridLayout, placements, FindCellRect);
        ApplyOccupiedColors(placements);
    }

    void ApplyOccupiedColors(List<BackpackGridVisual.ItemPlacement> placements)
    {
        foreach (var cell in cells)
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
                var cell = FindCell(p.x + dx, p.y + dy);
                cell?.SetOccupiedVisual(p.equipped);
            }
        }
    }

    GridCellUI FindCell(int gx, int gy)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            if (c != null && c.gridX == gx && c.gridY == gy)
                return c;
        }
        return null;
    }

    RectTransform FindCellRect(int gx, int gy)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            if (c != null && c.root != null && c.gridX == gx && c.gridY == gy)
                return c.root.GetComponent<RectTransform>();
        }
        return null;
    }

    static EquipInstance ToEquipInstance(EquipmentData d)
    {
        if (d == null) return null;
        // 轻量展示：用存档字段拼一个仅 UI 用的实例
        var eq = new EquipInstance
        {
            templateId = d.equipId,
            rarity = (Rarity)Mathf.Clamp(d.rarity, 0, 4),
            icon = null
        };
        try
        {
            var tpl = ConfigManager.Instance != null ? ConfigManager.Instance.GetEquipTemplate(d.equipId) : null;
            if (tpl != null)
            {
                tpl.ResolveIcon();
                eq.icon = tpl.icon;
                eq.template = tpl;
                eq.templateId = tpl.templateId;
                eq.gridWidth = tpl.gridWidth;
                eq.gridHeight = tpl.gridHeight;
            }
        }
        catch { /* 配置未就绪 */ }
        return eq;
    }

    public int UnlockedSlotCount()
    {
        int rows = GameConfig.GetUnlockedBackpackRows(SaveSystem.Instance?.Data);
        return rows * GameConfig.BACKPACK_WIDTH;
    }

    static GameObject CreateLockOverlay(Transform cell)
    {
        var go = new GameObject("LockedOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(cell, false);
        var rt = go.GetComponent<RectTransform>();
        Stretch(rt, 0f);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.12f, 0.1f, 0.08f, 0.62f);
        img.raycastTarget = false;
        go.SetActive(false);
        return go;
    }

    static Image FindImgNamedOnly(Transform root, params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            var t = FindDeep(root, names[i]);
            if (t != null)
            {
                var img = t.GetComponent<Image>();
                if (img != null) return img;
            }
        }
        return null;
    }

    static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var f = FindDeep(root.GetChild(i), name);
            if (f != null) return f;
        }
        return null;
    }
}
