using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 角色界面（CharacterUI）**专属**背包网格。只服务角色页，不要被别的界面复用。
/// 显示规格：8 列 × 4 行（上面 2 行解锁、下面 2 行锁定）；左右翻页只做显示偏移。
/// <para>**角色页自己的显示容量：8 × 4 = 32 格**，由 <see cref="CharacterBagSystem"/> 单独管理，
/// 与战斗 / 弹窗背包的 4 列网格（GameConfig.BACKPACK_*）<b>完全解耦</b>，两边互不影响；
/// GameConfig 的 BACKPACK_WIDTH / BACKPACK_HEIGHT_MAX / BACKPACK_DEFAULT_ROWS 一个都不碰。</para>
/// </summary>
public class CharacterBagGrid : TownBackpackGrid
{
    /// <summary>显示列数；&lt;= 0 时回退基类默认（逻辑列数）。</summary>
    public int columns = 8;
    /// <summary>
    /// 显示的解锁行数；&lt;= 0 时回退基类默认（存档/天赋真值）。
    /// **以后要扩容量就把这个数往上调：3 → 24 格，4 → 32 格，一行 8 格**
    /// —— 不用动 GameConfig，也不用动战斗背包。
    /// </summary>
    public int unlockedDisplayRows = 2;
    /// <summary>显示的页索引（0 基）；只做逻辑行的显示偏移，不移动/不删除任何存档道具。</summary>
    public int page = 0;

    protected override int DisplayColumns()
    {
        return columns > 0 ? columns : base.DisplayColumns();
    }

    protected override int DisplayUnlockedRows()
    {
        return unlockedDisplayRows > 0 ? unlockedDisplayRows : base.DisplayUnlockedRows();
    }

    protected override int DisplayPageIndex()
    {
        return page > 0 ? page : base.DisplayPageIndex();
    }

    /// <summary>角色页逻辑网格列数 = 8（不再折进 4 列逻辑网）。</summary>
    protected override int LogicalColumns() => CharacterBagSystem.Columns;
    /// <summary>角色页逻辑网格行数上限 = 4（32 格显示容量）。</summary>
    protected override int LogicalRowsMax() => CharacterBagSystem.RowsMax;

    /// <summary>
    /// 角色页走自己的 <see cref="CharacterBagSystem"/>（数据源 = 存档全量装备池，8 列排布），
    /// 不再走基类那条 GridBackpackSystem（4 列）的默认分支。
    /// <para>CharacterBagSystem 返回的坐标就是本网格的逻辑坐标（格子 Cell_{行}_{列} 的 (列,行) 直接 = (x,y)，没有折叠），
    /// 所以这里不做任何换算，原样返回。</para>
    /// </summary>
    protected override List<BackpackGridVisual.ItemPlacement> CollectPlacements(int unlockedRowsDisplay)
    {
        return CharacterBagSystem.Collect(unlockedRowsDisplay > 0 ? unlockedRowsDisplay : CharacterBagSystem.DefaultUnlockedRows);
    }

    /// <summary>已解锁格数走角色页自己的容量算法：行数 × 8 格。</summary>
    public override int UnlockedSlotCount()
    {
        int rows = unlockedDisplayRows > 0 ? unlockedDisplayRows : CharacterBagSystem.DefaultUnlockedRows;
        return CharacterBagSystem.Capacity(rows);
    }
}
