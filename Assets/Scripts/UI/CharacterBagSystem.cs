using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 角色页（CharacterUI）**专属**的背包「显示 / 容量」系统。
/// <para>与战斗背包 <see cref="GridBackpackSystem"/>（4 列 × 4 行，GameConfig.BACKPACK_*）**完全解耦**，
/// 两边互不影响：这里只是「角色页自己的显示容量」，8 列 × 4 行 = 32 格，**不是第二套存档**，
/// 不新增任何 SaveData 字段，也不写存档。</para>
/// <para>数据源只读：<see cref="SaveSystem"/> 的全量装备池 legacyEquipPool 优先（角色页 = 仓库视图）；
/// 它为空时才只读地取 GridBackpackSystem 的快照。全程**只读**，
/// 绝不调用 GridBackpackSystem 的任何写入 / 放置 / 移除方法。</para>
/// <para>以后想扩容量：只改这里的常量或 CharacterBagGrid.unlockedDisplayRows（3 → 24 格，4 → 32 格），
/// 一行 8 格，**不要动 GameConfig，也不要动战斗/弹窗背包**。</para>
/// </summary>
public static class CharacterBagSystem
{
    /// <summary>角色页自己的网格列数：8 列。</summary>
    public const int Columns = 8;
    /// <summary>角色页自己的网格行数上限：4 行（8 × 4 = 32 格显示容量）。</summary>
    public const int RowsMax = 4;
    /// <summary>默认解锁行数：2 行 = 16 格（下 2 行上锁）。</summary>
    public const int DefaultUnlockedRows = 2;

    /// <summary>已解锁显示行数 → 可用格数（1 行 = 8 格，行数会被钳到 [1, RowsMax]）。</summary>
    public static int Capacity(int unlockedRows)
    {
        return Mathf.Clamp(unlockedRows, 1, RowsMax) * Columns;
    }

    /// <summary>
    /// 按当前解锁行收纳装备，返回铺设清单。全程 try/catch 兜底：出错返回空列表，绝不把异常抛到 UI。
    /// 坐标是角色页自己的 8 列网格坐标（行优先），由 CharacterBagGrid 负责翻译成格子的实际坐标。
    /// </summary>
    public static List<BackpackGridVisual.ItemPlacement> Collect(int unlockedRows)
    {
        var result = new List<BackpackGridVisual.ItemPlacement>();
        try
        {
            int rows = Mathf.Clamp(unlockedRows <= 0 ? DefaultUnlockedRows : unlockedRows, 1, RowsMax);
            var equips = CollectSourceEquips();
            if (equips == null || equips.Count == 0) return result;

            var occupied = new bool[Columns, RowsMax];
            for (int i = 0; i < equips.Count; i++)
            {
                var eq = equips[i];
                if (eq == null) continue;
                int w = eq.gridWidth > 0 ? eq.gridWidth : 1;
                int h = eq.gridHeight > 0 ? eq.gridHeight : 1;
                if (w > Columns || h > rows) continue;      // 本身比整行还宽 / 比解锁区还高 → 跳过
                int px, py;
                if (!TryFindSlot(occupied, rows, w, h, out px, out py)) continue;  // 放不下 → 跳过
                MarkOccupied(occupied, px, py, w, h, true);
                bool equipped = false;
                try { var sys = GridBackpackSystem.Instance; if (sys != null) equipped = sys.IsEquipped(eq); }
                catch { equipped = false; }
                result.Add(new BackpackGridVisual.ItemPlacement
                {
                    x = px, y = py, w = w, h = h, equip = eq, equipped = equipped
                });
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[CharacterBagSystem] 收集角色页背包失败：" + e.Message);
            return new List<BackpackGridVisual.ItemPlacement>();
        }
        return result;
    }

    /// <summary>
    /// 数据源：优先存档全量装备池 legacyEquipPool（角色页 = 仓库视图）；
    /// 它为空才只读地回退 GridBackpackSystem 的当前快照（按 y * 4 + x 排线性序，只取 equip）。
    /// </summary>
    static List<EquipInstance> CollectSourceEquips()
    {
        var list = new List<EquipInstance>();
        try
        {
            var pool = SaveSystem.Instance?.Data?.legacyEquipPool;
            if (pool != null)
            {
                for (int i = 0; i < pool.Count; i++)
                {
                    var d = pool[i];
                    if (d == null) continue;
                    var eq = TownBackpackGrid.ToEquipInstance(d);   // 复用 TownBackpackGrid 的既有转换
                    if (eq != null) list.Add(eq);
                }
            }
            if (list.Count > 0) return list;

            var bag = GridBackpackSystem.Instance;
            if (bag == null) return list;
            var items = bag.GetAllBackpackItems();
            if (items == null || items.Count == 0) return list;
            var sorted = new List<GridBackpackSystem.BackpackItem>(items);
            sorted.Sort((a, b) => (a != null ? a.y * 4 + a.x : 0).CompareTo(b != null ? b.y * 4 + b.x : 0));
            for (int i = 0; i < sorted.Count; i++)
            {
                var bip = sorted[i];
                if (bip?.equip == null) continue;
                list.Add(bip.equip);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[CharacterBagSystem] 取数据源失败：" + e.Message);
        }
        return list;
    }

    /// <summary>行优先找第一个能放下 w × h 的空位。</summary>
    static bool TryFindSlot(bool[,] occupied, int rows, int w, int h, out int px, out int py)
    {
        px = 0;
        py = 0;
        for (int y = 0; y + h <= rows; y++)
        {
            for (int x = 0; x + w <= Columns; x++)
            {
                if (!IsFree(occupied, x, y, w, h)) continue;
                px = x;
                py = y;
                return true;
            }
        }
        return false;
    }

    static bool IsFree(bool[,] occupied, int x, int y, int w, int h)
    {
        for (int dy = 0; dy < h; dy++)
        for (int dx = 0; dx < w; dx++)
            if (occupied[x + dx, y + dy]) return false;
        return true;
    }

    static void MarkOccupied(bool[,] occupied, int x, int y, int w, int h, bool flag)
    {
        for (int dy = 0; dy < h; dy++)
        for (int dx = 0; dx < w; dx++)
            occupied[x + dx, y + dy] = flag;
    }
}
