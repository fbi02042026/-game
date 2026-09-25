using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 城镇/角色页背包网格：只绑定预制体里已摆好的格子，不改布局。
/// 逻辑网格列数固定 4；行数上限 BACKPACK_HEIGHT_MAX（4）。默认解锁 BACKPACK_DEFAULT_ROWS(3) 行，
/// 第 4 行由天赋 R_BAG（SaveData.backpackRows）解锁——运行期 BuildGrid 会建满 4 行，未解锁行逐格锁定。
/// 注意：角色页内嵌网格来自预制体（仅 12 格=3 行），4 行只在运行期新建的弹窗网格中出现。
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

    /// <summary>显示的列数（供子类覆盖；默认=逻辑列数，通用界面不要覆盖）。</summary>
    protected virtual int DisplayColumns() => GameConfig.BACKPACK_WIDTH;
    /// <summary>显示的解锁行数（供子类覆盖；默认=存档/天赋真值）。</summary>
    protected virtual int DisplayUnlockedRows() => GameConfig.GetUnlockedBackpackRows(SaveSystem.Instance?.Data);
    /// <summary>显示页索引（供子类覆盖；默认 0，不分页）。</summary>
    protected virtual int DisplayPageIndex() => 0;
    /// <summary>逻辑网格列数（供子类覆盖；默认 = GameConfig.BACKPACK_WIDTH，通用界面不要覆盖）。</summary>
    protected virtual int LogicalColumns() => GameConfig.BACKPACK_WIDTH;
    /// <summary>逻辑网格行数上限（供子类覆盖；默认 = GameConfig.BACKPACK_HEIGHT_MAX）。</summary>
    protected virtual int LogicalRowsMax() => GameConfig.BACKPACK_HEIGHT_MAX;

    /// <summary>
    /// 子类专属数据源钩子（**基类默认返回 null，走原本的 GridBackpackSystem 逻辑，行为一个字节都不变**）。
    /// <para>返回非 null = 由子类全权决定铺什么：Refresh() 拿它直接 ClearAndPlace + ApplyOccupiedColors 后 return，
    /// **不再走下面那条 4 列网格 + `bip.y >= unlockedRows` 的过滤分支**。</para>
    /// <para>坐标语义：子类自己的网格坐标；由子类负责与格子实际使用的逻辑坐标对齐。</para>
    /// </summary>
    protected virtual List<BackpackGridVisual.ItemPlacement> CollectPlacements(int unlockedRowsDisplay) => null;

    public void BindFromHierarchy(Transform searchRoot = null)
    {
        Transform root = searchRoot != null ? searchRoot : transform;
        Transform grid = FindDeep(root, "GridContainer");
        if (grid == null) return;
        gridContainer = grid as RectTransform;
        gridLayout = grid.GetComponent<GridLayoutGroup>();
        // 绑定预制体里已有的 GridContainer：只补「列数正确性」相关的两个字段，
        // 绝不再跑 AlignLayoutWithBattle() —— 它会把主人手调的 cellSize/spacing/padding/对齐方式
        // 覆盖成战斗侧那套（尤其 padding 会被写成 10，8 列 × 82 + 20 = 676 > 容器 660，格子直接溢出）。
        // 铁律：预制体里已有值 → 代码不写；只有代码自己新建的容器才用 AlignLayoutWithBattle() 初始化。
        EnsureLayoutConstraints();

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

            // 格子命名约定（与主人预制体一致）：Cell_{显示行}_{显示列} —— 行在前、列在后。
            int cols = DisplayColumns();
            if (cols <= 0) cols = GameConfig.BACKPACK_WIDTH;
            // 兜底：按兄弟序号算显示坐标（GridLayoutGroup 就是按这个顺序排的，行优先、0 基）
            int dRow = i / cols;
            int dCol = i % cols;
            string n = cell.name;
            if (n.StartsWith("Cell_", System.StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = n.Split('_');
                if (parts.Length >= 3
                    && int.TryParse(parts[1], out int pa)
                    && int.TryParse(parts[2], out int pb))
                {
                    dRow = pa;      // 主人约定：第一个数字 = 显示行(0..3)
                    dCol = pb;      // 主人约定：第二个数字 = 显示列(0..7)
                }
            }
            // 显示坐标 → 逻辑坐标（列折叠 / 页偏移都在这一个映射里）
            int gx, gy;
            MapDisplayIndexToLogical(dRow * cols + dCol, cols, out gx, out gy);

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
            ui.CaptureDefaultVisual();
            if (ui.lockedOverlay == null)
                ui.lockedOverlay = BackpackGridVisual.EnsureLockOverlay(cell);
            cells.Add(ui);
        }
    }

    /// <summary>
    /// 显示索引 → 逻辑坐标（显示规格的核心映射）。
    /// <para>i 是格子在 GridContainer 里的兄弟序号（GridLayoutGroup 就是按这个顺序排的），0 基、行优先。</para>
    /// <para>先拆出显示行列：dCol = i % cols；dRow = i / cols。</para>
    /// <para>列数是 W(4) 的整数倍时（子类把显示列数放大成 W 的整数倍 → perRowLogical = 该倍数），
    /// 一行显示正好装得下这么多逻辑行，于是把「多个逻辑行折进一行显示行」：
    ///     logicX = dCol % W；logicY = dRow * perRowLogical + dCol / W；
    /// 非整数倍（含 cols &lt; W 的退化情况）不折叠，直通：logicX = dCol；logicY = dRow。</para>
    /// <para>最后叠上分页偏移：logicY += DisplayPageIndex() * 每页逻辑行数；
    /// 结果 ≥ BACKPACK_HEIGHT_MAX 的格子由调用方按「超出逻辑范围」处理（铺锁图层、不可放道具）。</para>
    /// </summary>
    void MapDisplayIndexToLogical(int i, int cols, out int logicX, out int logicY)
    {
        if (cols <= 0) cols = LogicalColumns();
        int dCol = i % cols;
        int dRow = i / cols;
        int perRowLogical = LogicalRowsPerDisplayRow(cols);
        if (perRowLogical > 1 && LogicalColumns() > 0)
        {
            logicX = dCol % LogicalColumns();
            logicY = dRow * perRowLogical + dCol / LogicalColumns();
        }
        else
        {
            // 退化：一显示行 = 一逻辑行
            logicX = dCol;
            logicY = dRow;
        }
        logicY += DisplayPageIndex() * LogicalRowsPerPage();
    }

    /// <summary>
    /// 一个显示行折进去几个逻辑行（折叠系数）：cols 是 BACKPACK_WIDTH 的整数倍时才折叠，
    /// 否则（含 cols &lt; BACKPACK_WIDTH）退化成 1 = 不折叠。
    /// </summary>
    int LogicalRowsPerDisplayRow(int cols)
    {
        if (cols <= 0 || LogicalColumns() <= 0) return 1;
        int per = cols / LogicalColumns();
        return per > 1 ? per : 1;
    }

    /// <summary>每页显示的逻辑行数 = 显示总行数(BACKPACK_HEIGHT_MAX) ÷ 折叠系数。</summary>
    public int LogicalRowsPerPage()
    {
        int per = LogicalRowsPerDisplayRow(DisplayColumns());
        int rows = GameConfig.BACKPACK_HEIGHT_MAX / per;
        return rows > 0 ? rows : GameConfig.BACKPACK_HEIGHT_MAX;
    }

    /// <summary>显示行数（总格数 ÷ 显示列数），用于判断「整行锁图案」该不该亮。</summary>
    int DisplayRowCount(int cols)
    {
        if (cells.Count == 0 || cols <= 0) return 0;
        return Mathf.CeilToInt((float)cells.Count / cols);
    }

    /// <summary>
    /// 单个格子是否显示成锁定：
    /// ① 所在显示行 ≥ 可显示的解锁行（DisplayUnlockedRows()）→ 锁；
    /// ② 逻辑坐标超出 BACKPACK_WIDTH / BACKPACK_HEIGHT_MAX（折叠后排到的空位、页偏移越界）→ 锁。
    /// </summary>
    bool IsCellLocked(GridCellUI cell, int cols, int unlockedRowsDisplay)
    {
        // 先把分页偏移去掉，还原成「页内的逻辑行」，再按折叠系数换回显示行
        int baseY = cell.gridY - DisplayPageIndex() * LogicalRowsPerPage();
        int perRowLogical = LogicalRowsPerDisplayRow(cols);
        int dRow = perRowLogical > 1 ? baseY / perRowLogical : baseY;
        if (dRow < 0) dRow = 0;
        // 解锁单位是「显示行」：整行 8 格一起开 / 一起锁。这条判据必须排在所有越界判断之前，
        // 否则同一显示行会出现「前 4 格解锁、后 4 格锁」的半行状态（主人明确禁止）。
        if (dRow >= unlockedRowsDisplay) return true;
        // 逻辑坐标越界（折叠后排到的空位、页偏移越界）→ 锁
        if (cell.gridY < 0 || cell.gridY >= LogicalRowsMax()) return true;
        if (cell.gridX < 0 || cell.gridX >= LogicalColumns()) return true;
        return false;
    }

    /// <summary>子类把显示规格改过（列数折叠 / 分页偏移）时，Refresh 要按显示索引重算格子的逻辑坐标。</summary>
    bool HasDisplayOverride()
    {
        return DisplayColumns() != LogicalColumns() || DisplayPageIndex() != 0;
    }

    /// <summary>按「显示索引 → 逻辑坐标」重算每个格子的 gridX/gridY。</summary>
    void RemapDisplayCoords(int cols)
    {
        if (gridContainer == null || cols <= 0) return;
        for (int i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            if (cell == null || cell.root == null) continue;
            int idx = cell.root.transform.GetSiblingIndex();
            if (idx < 0) continue;
            int gx, gy;
            MapDisplayIndexToLogical(idx, cols, out gx, out gy);
            cell.gridX = gx;
            cell.gridY = gy;
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

        // 铁律：主人的预制体里已经有摆好的格子（CharacterUI.prefab → Content/BackpackPanel/GridContainer
        // 下面 32 个 Cell_{显示行}_{显示列}），代码一律不许销毁重建 —— 一旦 Destroy 掉，
        // 主人的 32 个格子和手调的 GridLayoutGroup 参数（82×82 / spacing 0 / 8 列）全没了。
        // 所以：只要 GridContainer 已存在且格子数量够，就只绑定不重建；只有确实缺的时候才走下面的兜底重建。
        int colsPreset = DisplayColumns();
        Transform preset = bagPanel.Find("GridContainer") ?? FindDeep(bagPanel, "GridContainer");
        if (preset != null)
        {
            var presetGl = preset.GetComponent<GridLayoutGroup>();
            if (presetGl != null)
            {
                int usable = 0;     // 非 LockedOverlay 的子节点数 = 真正的格子数
                for (int k = 0; k < preset.childCount; k++)
                {
                    string kn = preset.GetChild(k).name;
                    if (kn.IndexOf("Locked", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || kn.Equals("Lock", System.StringComparison.OrdinalIgnoreCase))
                        continue;
                    usable++;
                }
                if (usable >= colsPreset * GameConfig.BACKPACK_HEIGHT_MAX)
                {
                    // 只保证「约束列数」正确；cellSize / spacing / padding / startCorner /
                    // startAxis / childAlignment 一个都不许改（都是主人手调的）
                    if (presetGl.constraint != GridLayoutGroup.Constraint.FixedColumnCount
                        || presetGl.constraintCount != colsPreset)
                    {
                        presetGl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                        presetGl.constraintCount = colsPreset;
                    }
                    BindFromHierarchy(bagPanel);
                    return;
                }
            }
        }

        // 重建前记下旧 GridContainer 的矩形（主人手摆的），重建后原样套回去，避免列数一改位置就跑了
        Transform existing = bagPanel.Find("GridContainer");
        bool hadPrevRect = false;
        Vector2 prevAnchorMin = Vector2.zero, prevAnchorMax = Vector2.one;
        Vector2 prevOffsetMin = Vector2.zero, prevOffsetMax = Vector2.zero;
        Vector2 prevPivot = new Vector2(0.5f, 0.5f);
        if (existing != null)
        {
            var prevRt = existing as RectTransform;
            if (prevRt != null)
            {
                hadPrevRect = true;
                prevAnchorMin = prevRt.anchorMin;
                prevAnchorMax = prevRt.anchorMax;
                prevOffsetMin = prevRt.offsetMin;
                prevOffsetMax = prevRt.offsetMax;
                prevPivot = prevRt.pivot;
            }
            Object.DestroyImmediate(existing.gameObject);
        }

        int cols = DisplayColumns();
        var go = new GameObject("GridContainer", typeof(RectTransform), typeof(GridLayoutGroup));
        go.transform.SetParent(bagPanel, false);
        var rt = go.GetComponent<RectTransform>();
        if (hadPrevRect)
        {
            rt.anchorMin = prevAnchorMin;
            rt.anchorMax = prevAnchorMax;
            rt.offsetMin = prevOffsetMin;
            rt.offsetMax = prevOffsetMax;
            rt.pivot = prevPivot;
        }
        else
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(12f, 12f);
            rt.offsetMax = new Vector2(-12f, -48f);
        }

        var gl = go.GetComponent<GridLayoutGroup>();
        gl.cellSize = new Vector2(CellSize, CellSize);
        gl.spacing = new Vector2(CellSpacing, CellSpacing);
        gl.padding = new RectOffset(Pad, Pad, Pad, Pad);
        gl.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gl.startAxis = GridLayoutGroup.Axis.Horizontal;
        gl.childAlignment = TextAnchor.UpperCenter;
        gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gl.constraintCount = cols;

        // 总格数 = 显示列数 × 逻辑行数上限（行数仍是 BACKPACK_HEIGHT_MAX = 4，不改 GameConfig）
        int totalCells = cols * LogicalRowsMax();
        for (int i = 0; i < totalCells; i++)
        {
            int dRow = i / cols;
            int dCol = i % cols;
            int gx, gy;
            MapDisplayIndexToLogical(i, cols, out gx, out gy);
            CreateCell(go.transform, dRow, dCol, gx, gy);
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

    /// <summary>
    /// 与战斗预制体 CellBg 节点一致的着色（BattleUI.prefab 里 m_Color 是 0.48235294 灰），
    /// 保证同一张装备格.png 在两边观感一致。加载不到图时不生效。
    /// </summary>
    static readonly Color BattleCellBgTint = new Color(0.48235294f, 0.48235294f, 0.48235294f, 1f);

    /// <summary>
    /// 建一个格子：节点名按主人约定 `Cell_{显示行}_{显示列}`（行在前），
    /// gx/gy 只作为格子的逻辑坐标存进 GridCellUI（逻辑网格仍是 4 列 × 4 行）。
    /// </summary>
    static void CreateCell(Transform parent, int dRow, int dCol, int gx, int gy)
    {
        var cell = new GameObject($"Cell_{dRow}_{dCol}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        cell.transform.SetParent(parent, false);
        var bg = cell.GetComponent<Image>();
        // 复用战斗背包同款格子底图（装备格.png，预制体里是 Simple）；拿不到就退回原来的奶白底色
        var cellArt = BackpackGridVisual.CellBgSprite();
        if (cellArt != null)
        {
            bg.sprite = cellArt;
            bg.type = Image.Type.Simple;
            bg.color = BattleCellBgTint;
        }
        else
        {
            bg.color = new Color(0.93f, 0.86f, 0.7f, 1f);
        }

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

        // 初始显隐按当前解锁行数（天赋可开出第 4 行），不写死默认 3 行；
        // 具体是否锁定仍由 Refresh() 每次重算。
        var locked = BackpackGridVisual.EnsureLockOverlay(cell.transform);
        if (locked != null)
            locked.SetActive(gy >= GameConfig.UnlockedBackpackRows());
    }

    /// <summary>角色页与战斗页格子规格统一，避免同一套装备两边占位观感不一致。</summary>
    void AlignLayoutWithBattle()
    {
        if (gridLayout == null) return;
        gridLayout.cellSize = new Vector2(CellSize, CellSize);
        gridLayout.spacing = new Vector2(CellSpacing, CellSpacing);
        if (gridLayout.padding == null)
            gridLayout.padding = new RectOffset(Pad, Pad, Pad, Pad);
        else
        {
            gridLayout.padding.left = Pad;
            gridLayout.padding.right = Pad;
            gridLayout.padding.top = Pad;
            gridLayout.padding.bottom = Pad;
        }
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        // 显示列数（子类可覆盖）；不写死 GameConfig.BACKPACK_WIDTH，否则重绑会把子类列数打回逻辑列数
        gridLayout.constraintCount = DisplayColumns();
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        gridLayout.childAlignment = TextAnchor.UpperCenter;
    }

    /// <summary>
    /// 绑定「预制体里已经摆好」的 GridContainer 时用：只保证列数正确，其余一律不写。
    /// 与 <see cref="AlignLayoutWithBattle"/> 的区别：后者会把 cellSize / spacing / padding /
    /// startCorner / startAxis / childAlignment 全部覆盖成战斗侧那套，只对代码自己新建的容器用。
    /// 主人的角色页背包是 8 列 × 82px + padding 0，一旦被覆写成 padding 10 就会溢出容器。
    /// </summary>
    void EnsureLayoutConstraints()
    {
        if (gridLayout == null) return;
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        // 显示列数（子类可覆盖）；不写死 GameConfig.BACKPACK_WIDTH，否则重绑会把子类列数打回逻辑列数
        gridLayout.constraintCount = DisplayColumns();
    }

    /// <summary>本网格实际有几行（= 已绑定格子的最大 gridY + 1），用于判断「整行锁图案」该不该亮。</summary>
    public int GridRowCount()
    {
        if (cells.Count == 0) return GameConfig.BACKPACK_HEIGHT;
        int maxY = 0;
        for (int i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            if (c != null && c.gridY > maxY) maxY = c.gridY;
        }
        return maxY + 1;
    }

    public void Refresh()
    {
        if (cells.Count == 0) BindFromHierarchy();
        if (cells.Count == 0) return;

        // 先强制布局再量格子世界坐标，否则角色页刚 Show 时 sizeDelta 可能还是 0，图标会落到回退算法上偏上
        Canvas.ForceUpdateCanvases();
        if (gridLayout != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(gridContainer);

        int cols = DisplayColumns();
        // 显示规格被子类覆盖时（列折叠 / 分页偏移），按显示索引重算格子逻辑坐标，列数与页一变就地生效
        if (HasDisplayOverride())
            RemapDisplayCoords(cols);

        // 道具真实可放范围仍按存档/天赋算（逻辑行），只把「显示」的锁行单独判
        int unlockedRows = GameConfig.GetUnlockedBackpackRows(SaveSystem.Instance?.Data);
        int unlockedRowsDisplay = DisplayUnlockedRows();
        // 整行锁图案亮的条件 = 显示行数 > 可显示的解锁行数
        bool bottomLocked = unlockedRowsDisplay < DisplayRowCount(cols);
        if (rowLockOverlay != null)
            rowLockOverlay.SetActive(bottomLocked);

        foreach (var cell in cells)
        {
            if (cell == null) continue;
            bool rowLocked = IsCellLocked(cell, cols, unlockedRowsDisplay);
            cell.SetRowLocked(rowLocked);
            if (!rowLocked) cell.Clear();
        }

        // 子类专属数据源（角色页 8 列网格 → CharacterBagSystem）：
        // 子类给了自己的整套排布就直接铺，绝不掺用下面那条 4 列 GridBackpackSystem / bip.y >= unlockedRows 的过滤逻辑。
        var custom = CollectPlacements(unlockedRowsDisplay);
        if (custom != null)
        {
            BackpackGridVisual.ClearAndPlace(gridContainer, gridLayout, custom, FindCellRect);
            ApplyOccupiedColors(custom);
            return;
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
        int cap = unlockedRows * LogicalColumns();
        for (int i = 0; i < data.legacyEquipPool.Count && slot < cap; i++)
        {
            var legacy = data.legacyEquipPool[i];
            if (legacy == null) continue;
            int x = slot % LogicalColumns();
            int y = slot / LogicalColumns();
            var eq = ToEquipInstance(legacy);
            if (eq == null) continue;
            int w = eq.gridWidth > 0 ? eq.gridWidth : 1;
            int h = eq.gridHeight > 0 ? eq.gridHeight : 1;
            if (x + w > LogicalColumns() || y + h > unlockedRows) continue;
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
            if (c != null && c.gridX == gx && c.gridY == gy)
                return c.VisualRect;
        }
        return null;
    }

    // internal：角色页专属系统 CharacterBagSystem 也要复用这份转换（EquipmentData → 仅 UI 用的 EquipInstance），
    // 不在别处重复实现。访问级别放开不改变任何行为。
    internal static EquipInstance ToEquipInstance(EquipmentData d)
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

    public virtual int UnlockedSlotCount()
    {
        int rows = GameConfig.GetUnlockedBackpackRows(SaveSystem.Instance?.Data);
        return rows * GameConfig.BACKPACK_WIDTH;
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
