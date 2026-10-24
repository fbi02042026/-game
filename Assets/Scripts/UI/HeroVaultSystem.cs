using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 角色页（CharacterUI）**专属**的背包「显示 / 容量」系统。
/// <para>与战斗背包 <see cref="BattleBagSystem"/>（4 列 × 4 行，GameConfig.BACKPACK_*）**完全解耦**，
/// 两边互不影响：这里只是「角色页自己的显示容量」，8 列 × 4 行 = 32 格，**不是第二套存档**，
/// 不新增任何 SaveData 字段，也不写存档。</para>
    /// <para>数据源只读：<see cref="SaveSystem"/> 的全量装备池 legacyEquipPool（角色页 = 持久仓库视图）。
    /// 【2026-10-24 主人拍板·去串味】**不再**回退去读战斗背包 BattleBagSystem（局内背包，出局清零），
    /// 两者彻底解耦，角色页绝不许显示战斗里的东西。出战斗后获得的道具走 settlement →
    /// SaveData.townBagItems（AppendTownDisplays 已显示），与 legacyEquipPool 互不影响。</para>
/// <para>以后想扩容量：只改这里的常量或 HeroVaultGrid.unlockedDisplayRows（3 → 24 格，4 → 32 格），
/// 一行 8 格，**不要动 GameConfig，也不要动战斗/弹窗背包**。</para>
/// </summary>
public static class HeroVaultSystem
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
    /// 【2026-10-24 主人拍板】背包排序开关（角色页「整理」按钮切换，纯显示层，不碰存档）：
    /// false = 默认**获得顺序**（同类物品先沾满前面的格子到 99，再按获得顺序往后占格）；
    /// true = **整理态**：同类型聚拢成片，组内稀有度高的在前（无稀有度的保持原相对顺序）。
    /// </summary>
    public static bool SortByRarity;

    /// <summary>
    /// 按当前解锁行收纳装备，返回铺设清单。全程 try/catch 兜底：出错返回空列表，绝不把异常抛到 UI。
    /// 坐标是角色页自己的 8 列网格坐标（行优先），由 HeroVaultGrid 负责翻译成格子的实际坐标。
    /// </summary>
    public static List<BackpackGridVisual.ItemPlacement> Collect(int unlockedRows)
    {
        var result = new List<BackpackGridVisual.ItemPlacement>();
        try
        {
            int rows = Mathf.Clamp(unlockedRows <= 0 ? DefaultUnlockedRows : unlockedRows, 1, RowsMax);
            var equips = CollectSourceEquips();
            // 【2026-10-24 主人拍板】整理态：装备稀有度高的在前；同稀有度保持获得顺序（按下标决胜=稳定排序）
            if (SortByRarity && equips != null && equips.Count > 1)
            {
                var order = new List<int>(equips.Count);
                for (int i = 0; i < equips.Count; i++) order.Add(i);
                order.Sort((a, b) =>
                {
                    int ra = equips[a] != null ? (int)equips[a].rarity : -1;
                    int rb = equips[b] != null ? (int)equips[b].rarity : -1;
                    if (ra != rb) return rb - ra;
                    return a - b;
                });
                var sorted = new List<EquipInstance>(equips.Count);
                for (int i = 0; i < order.Count; i++) sorted.Add(equips[order[i]]);
                equips = sorted;
            }
            var occupied = new bool[Columns, RowsMax];
            // 【2026-10-24 主人拍板·修「随身仓库空白」】装备池为空**不再提前 return** ——
            // 原来这里 equips.Count==0 直接返回，把下面的城镇道具/日志碎片/天赋石/徽记/卷轴
            // 展示位全部跳过（存档 legacyEquipPool=0 时整个仓库永远空白，且无任何告警）。
            // 现在：装备有就铺装备，没有就跳装备段，展示位照常铺设。
            if (equips != null)
            {
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
                    try { var sys = BattleBagSystem.Instance; if (sys != null) equipped = sys.IsEquipped(eq); }
                    catch { equipped = false; }
                    result.Add(new BackpackGridVisual.ItemPlacement
                    {
                        x = px, y = py, w = w, h = h, equip = eq, equipped = equipped
                    });
                }
            }

            // 2026-10-21 主人需求：出战斗后在角色页也要能看到「战斗里获得的」东西——
            // 城镇道具（局内道具结算时转入 SaveData.townBagItems）+ 日志碎片（存档总持有）+ 天赋石（钱包总持有）。
            // 全为纯展示位：图标 + 数量角标，点击弹告知框，不可使用/丢弃/拖拽。
            AppendTownDisplays(result, occupied, rows);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[HeroVaultSystem] 收集角色页背包失败：" + e.Message);
            return new List<BackpackGridVisual.ItemPlacement>();
        }
        return result;
    }

    /// <summary>展示位收集条目：先按获得顺序收集，再按需排序，最后统一铺格。</summary>
    struct DisplayEntry
    {
        /// <summary>类型组：0 本命碎片 / 1 职业徽记 / 2 招募卷轴 / 3 日志碎片 / 4 其他道具。</summary>
        public int category;
        /// <summary>组内稀有度（高 = 更稀有）；无稀有度概念的条目 = 0，排序时保持原相对顺序。</summary>
        public int rarityRank;
        public string name, info, fragHireId;
        public Sprite icon;
        public int count;
    }

    /// <summary>
    /// 在角色页背包追加纯展示位（出战斗后也能看到战斗里获得的）：
    /// ① 城镇道具（局内道具结算转入 SaveData.townBagItems）② 日志碎片（存档总持有）
    /// ③ 天赋石 ④ 重生护符 ⑤ 职业徽记 ⑥ 招募卷轴。全部占 1×1，图标 + 数量角标，点开弹告知框。
    /// 【2026-10-24 主人拍板·两段式】先按**获得顺序**收集（同类先沾满前面的格子到 99，
    /// 再按获得顺序往后占格 —— 由收集顺序 + TryAddDisplay 的 99 分格天然保证）；
    /// SortByRarity=true（点「整理」）时改为**同类型聚拢、组内稀有度高的在前**，无稀有度保持原顺序。
    /// 容量 = rows × Columns，放不下整条放弃并 LogWarning（不静默吞，也不超框）。
    /// </summary>
    static void AppendTownDisplays(List<BackpackGridVisual.ItemPlacement> result, bool[,] occupied, int rows)
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return;

        var entries = new List<DisplayEntry>();

        // ① 城镇道具（字典迭代顺序 = 领取顺序；本命碎片（item_frag_）走主人的碎片预制体，谁的碎片用谁的头像）
        if (data.townBagItems != null)
        {
            foreach (var kv in data.townBagItems)
            {
                if (string.IsNullOrEmpty(kv.Key) || kv.Value <= 0) continue;
                var def = ItemDefs.Get(kv.Key);
                if (def == null)
                {
                    Debug.LogWarning("[HeroVaultSystem] 城镇道具不在 item_defs 表里，跳过：" + kv.Key);
                    continue;
                }
                bool isFrag = kv.Key.StartsWith(MercStarDefs.FragmentIdPrefix);
                string info = (string.IsNullOrEmpty(def.desc) ? "" : def.desc + "\n") + "持有 ×" + kv.Value;
                entries.Add(new DisplayEntry
                {
                    category = isFrag ? 0 : 4,
                    rarityRank = isFrag ? MercRarityRank(MercStarDefs.HireIdOfFragmentItem(kv.Key)) : 0,
                    name = def.name ?? kv.Key,
                    info = info,
                    icon = ItemDefs.LoadIcon(kv.Key),
                    count = kv.Value,
                    fragHireId = isFrag ? MercStarDefs.HireIdOfFragmentItem(kv.Key) : null
                });
            }
        }

        // ② 日志碎片（存档总持有，点开看配方进度）
        var recipes = AdventureLogFragments.Recipes;
        if (recipes != null)
        {
            for (int i = 0; i < recipes.Length; i++)
            {
                var r = recipes[i];
                int have = AdventureLogFragments.GetCount(r.FragmentId);
                if (have <= 0) continue;
                entries.Add(new DisplayEntry
                {
                    category = 3,
                    rarityRank = 0,
                    name = AdventureLogFragments.DisplayName(r.FragmentId),
                    info = "材料类碎片，不能直接使用。\n" + AdventureLogFragments.FormatRecipeLine(i),
                    icon = BackpackGridVisual.LogFragmentIcon(),
                    count = have
                });
            }
        }

        // ③ 天赋石（钱包总持有）
        long stones = ResourceWallet.Get(data, ResourceWallet.ResourceType.TalentPoint);
        if (stones > 0)
        {
            entries.Add(new DisplayEntry
            {
                category = 4,
                rarityRank = 0,
                name = "天赋石",
                info = "用于城镇天赋页点亮、升级节点。\n击杀精英 / Boss、通关里程碑都会掉落。\n持有 ×" + stones,
                icon = CommonIconSprites.Load("icon_talent_stone"),
                count = (int)stones
            });
        }

        // ④ 重生护符（当前持有，2026-10-21 主人需求：战斗内关卡商店买的救命道具，出战斗也看得到存量）
        if (data.reviveCharms > 0)
        {
            entries.Add(new DisplayEntry
            {
                category = 4,
                rarityRank = 0,
                name = "重生护符",
                info = "佣兵阵亡时，点击其变灰的头像消耗 1 枚原地满血复活。\n战斗内关卡商店有售（50% 概率上架）。\n持有 ×" + data.reviveCharms,
                icon = ItemDefs.LoadIcon("item_revive_charm"),
                count = data.reviveCharms
            });
        }

        // ⑤ 职业徽记（佣兵升星材料；数据真源 = SaveData.mercGrowItems 的 badge:{职业key}:{档位}，
        //    升星扣费读写同仓；此处只做展示，键解析不出就 LogWarning 跳过（不静默吞））
        if (data.mercGrowItems != null)
        {
            foreach (var kv in data.mercGrowItems)
            {
                if (string.IsNullOrEmpty(kv.Key) || kv.Value <= 0) continue;
                if (!kv.Key.StartsWith("badge:")) continue;
                var parts = kv.Key.Substring(6).Split(':');
                if (parts.Length < 2 || string.IsNullOrEmpty(parts[0]))
                {
                    Debug.LogWarning("[HeroVaultSystem] 徽记键口径异常，跳过：" + kv.Key);
                    continue;
                }
                string jobKey = parts[0], tier = parts[1];
                string tierName = tier == "传奇" ? "高级" : tier == "稀有" ? "中级" : tier == "普通" ? "低级" : tier;
                entries.Add(new DisplayEntry
                {
                    category = 1,
                    rarityRank = tier == "传奇" ? 2 : tier == "稀有" ? 1 : 0,
                    name = jobKey + "·" + tierName + "徽记",
                    info = "佣兵升星消耗的职业徽记。\n徽记副本可获得。持有 ×" + kv.Value,
                    // 图标路径与 MercGrowSprites.LoadBadge 同源：Icons/JobBadge/{jobKey}{档位}
                    icon = Resources.Load<Sprite>(ContentPaths.Icons.JobBadge + "/" + jobKey + tier),
                    count = kv.Value
                });
            }
        }

        // ⑥ 招募卷轴（存档独立字段 mercScrollCommon / mercScrollLegendary；
        //    酒馆三选一招募时金币/卷轴双支付可抵扣；邮件补偿、里程商店、Boss 掉落可获得）
        if (data.mercScrollCommon > 0)
        {
            entries.Add(new DisplayEntry
            {
                category = 2,
                rarityRank = 0,
                name = "普通招募卷轴",
                info = "酒馆招募佣兵时可抵扣金币。\n持有 ×" + data.mercScrollCommon,
                icon = ItemDefs.LoadIcon("item_scroll_merc_common"),
                count = data.mercScrollCommon
            });
        }
        if (data.mercScrollLegendary > 0)
        {
            var advIcon = Resources.Load<Sprite>("Art/UI/Icons/Common/高级招募卷轴");
            entries.Add(new DisplayEntry
            {
                category = 2,
                rarityRank = 1,
                name = "高级招募卷轴",
                info = "酒馆招募传奇佣兵专用，可抵扣金币。\n持有 ×" + data.mercScrollLegendary,
                icon = advIcon != null ? advIcon : ItemDefs.LoadIcon("item_scroll_merc_common"),
                count = data.mercScrollLegendary
            });
        }

        // 【2026-10-24 主人拍板】整理态：同类型聚拢成片（组号小的在前），组内稀有度高的在前；
        // 同组同稀有度按下标决胜 = 稳定排序，保持获得顺序。默认态不排 = 纯获得顺序。
        if (SortByRarity && entries.Count > 1)
        {
            var order = new List<int>(entries.Count);
            for (int i = 0; i < entries.Count; i++) order.Add(i);
            order.Sort((a, b) =>
            {
                if (entries[a].category != entries[b].category)
                    return entries[a].category - entries[b].category;
                if (entries[a].rarityRank != entries[b].rarityRank)
                    return entries[b].rarityRank - entries[a].rarityRank;
                return a - b;
            });
            var sorted = new List<DisplayEntry>(entries.Count);
            for (int i = 0; i < order.Count; i++) sorted.Add(entries[order[i]]);
            entries = sorted;
        }

        // 【2026-10-24 主人拍板·修「徽记/别的道具不显示」】单个展示位放不下**只跳过它自己**，
        // 绝不 return 挡后面的条目 —— 原来洛恩碎片要 11 格装不下就整条 return，
        // 排在后面的徽记/天赋石/卷轴全被掐死（仓库还有空格也铺不上去）。
        // 放不下的仍由 TryAddDisplay 内部 LogWarning（不静默吞），继续铺下一条。
        foreach (var e in entries)
            TryAddDisplay(result, occupied, rows, e.name, e.info, e.icon, e.count, e.fragHireId);
    }

    /// <summary>本命碎片稀有度排序档（高 = 更稀有）：传奇 2 / 稀有 1 / 普通 0；识别不出按普通。</summary>
    static int MercRarityRank(string hireId)
    {
        if (string.IsNullOrEmpty(hireId)) return 0;
        if (!MercRosterDefs.TryGetByHireId(hireId, out var def)) return 0;
        switch (def.Rarity)
        {
            case MercRosterDefs.MercRarity.Legendary: return 2;
            case MercRosterDefs.MercRarity.Rare: return 1;
            default: return 0;
        }
    }

    /// <summary>
    /// 在已占用网格里找空格塞 1×1 展示位；满则 LogWarning 并返回 false。
    /// 【2026-10-24 主人拍板】一格最多 99，超了就**分格**：99 / 99 / … / 余数各占一格
    /// （例 250 → ×99 ×99 ×52 三格）。先数空格，装不下整条放弃——绝不摆一半冒充全量。
    /// 【类型不混排】分格优先找**同一行的连续空段**整段落下（同道具连成一片）；
    /// 没有整段（装备留了散洞）才退回逐格找洞，按扫描顺序尽量靠前。
    /// fragHireId 非空 = 本命碎片展示位，铺格时实例化主人的碎片预制体叠佣兵头像。
    /// </summary>
    static bool TryAddDisplay(List<BackpackGridVisual.ItemPlacement> result, bool[,] occupied, int rows,
        string name, string info, Sprite icon, int count, string fragHireId = null)
    {
        const int PerCellCap = 99;
        int remaining = Mathf.Max(1, count);
        int chunks = (remaining + PerCellCap - 1) / PerCellCap;

        int freeCells = 0;
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < Columns; x++)
                if (!occupied[x, y]) freeCells++;
        if (freeCells < chunks)
        {
            Debug.LogWarning("[HeroVaultSystem] 角色页背包已满，「" + name + "」要分 " + chunks +
                             " 格只空 " + freeCells + " 格，放弃展示");
            return false;
        }

        // 优先：同一行连续空段整段落下，分格绝不被别的类型插开
        if (chunks > 1 && TryFindRun(occupied, rows, chunks, out int rx, out int ry))
        {
            for (int c = 0; c < chunks; c++)
            {
                int chunk = Mathf.Min(PerCellCap, remaining);
                remaining -= chunk;
                MarkOccupied(occupied, rx + c, ry, 1, 1, true);
                result.Add(new BackpackGridVisual.ItemPlacement
                {
                    x = rx + c, y = ry, w = 1, h = 1,
                    displayName = name, displayInfo = info, displayIcon = icon, displayCount = chunk,
                    displayFragHireId = fragHireId
                });
            }
            return true;
        }

        // 退路：逐格找空位（散洞场景），同道具格子仍按扫描顺序连续领取
        for (int c = 0; c < chunks; c++)
        {
            int px = 0, py = 0;
            if (!TryFindSlot(occupied, rows, 1, 1, out px, out py))
            {
                Debug.LogWarning("[HeroVaultSystem] 角色页背包已满，「" + name + "」没有空格可展示");
                return false;
            }
            MarkOccupied(occupied, px, py, 1, 1, true);
            int chunk = Mathf.Min(PerCellCap, remaining);
            remaining -= chunk;
            result.Add(new BackpackGridVisual.ItemPlacement
            {
                x = px, y = py, w = 1, h = 1,
                displayName = name, displayInfo = info, displayIcon = icon, displayCount = chunk,
                displayFragHireId = fragHireId
            });
        }
        return true;
    }

    /// <summary>
    /// 找「同一行里 runLen 个连续空格」的整段位置（行优先扫描，取最靠前的）。
    /// 分格展示位用它保证同道具的格子横向连排；找不到返回 false。
    /// </summary>
    static bool TryFindRun(bool[,] occupied, int rows, int runLen, out int px, out int py)
    {
        px = 0;
        py = 0;
        if (runLen <= 1) return TryFindSlot(occupied, rows, 1, 1, out px, out py);
        for (int y = 0; y < rows; y++)
        {
            int run = 0;
            for (int x = 0; x < Columns; x++)
            {
                if (!occupied[x, y])
                {
                    run++;
                    if (run >= runLen)
                    {
                        px = x - runLen + 1;
                        py = y;
                        return true;
                    }
                }
                else run = 0;
            }
        }
        return false;
    }

    /// <summary>
    /// 数据源：只读取存档全量装备池 legacyEquipPool（角色页 = 持久仓库视图）。
    /// 【2026-10-24 主人拍板·去串味】只认 legacyEquipPool，**绝不**回退去读战斗背包 BattleBagSystem，
    /// 两者彻底解耦（见类注释）。legacyEquipPool 为空就返回空列表 —— 不留兜底、不静默拉战斗背包凑数。
    /// </summary>
    static List<EquipInstance> CollectSourceEquips()
    {
        var list = new List<EquipInstance>();
        try
        {
            var pool = SaveSystem.Instance?.Data?.legacyEquipPool;
            if (pool == null) return list;
            for (int i = 0; i < pool.Count; i++)
            {
                var d = pool[i];
                if (d == null) continue;
                var eq = TownBackpackGrid.ToEquipInstance(d);   // 复用 TownBackpackGrid 的既有转换
                if (eq != null) list.Add(eq);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[HeroVaultSystem] 取数据源失败：" + e.Message);
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
