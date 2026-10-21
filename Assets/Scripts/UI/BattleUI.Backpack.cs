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
            // 预制体格子没有锁图案时补一层（半透明遮罩 + 锁图标 + 拦截点击），
            // 显隐仍由后续的 SetRowLocked 决定。
            if (ui.lockedOverlay == null)
                ui.lockedOverlay = BackpackGridVisual.EnsureLockOverlay(cell);
            list.Add(ui);
        }
        gridCells = list;
    }

    GameObject _backpackRowLock; // GridContainer 下用户放的底行锁图案

    // ===== 战斗遮罩（BackpackPanel/zhezhao）=====
    GameObject _battleMask;
    bool _battleMaskOn = true;   // 初始与预制体 m_IsActive:1 对齐，首帧 Tick 会纠正

    void EnsureBattleMask()
    {
        if (_battleMask != null) return;
        _battleMask = FindDeepChildIgnoreCase(transform, "zhezhao")?.gameObject;
        RaiseSlotCanvasAboveParent(skillSlotRoot);
    }

    /// <summary>
    /// 把槽位容器自身的嵌套 Canvas 抬到「父链里最高的 Canvas order + 1」。
    ///
    /// <para>【2026-10-11 主人报「抽出来就不显示，抽奖瞬间闪一下又没了」→ 根因】：
    /// 旧口径只抬到 <c>BackpackPanel(100) + 1 = 101</c>。但嵌套 Canvas 的 sortingOrder 是
    /// <b>全局比较</b>的，而 BattleUI 根 Canvas 是 <see cref="GameConfig.UiSort.BattleHud"/>(950) ——
    /// 101 &lt; 950 → 整个根 Canvas 上的内容（底部面板 / HUD / 全屏层）都画在技槽<b>之上</b>，
    /// 技槽只在面板重排的那一瞬露一下，随即被盖住 —— 与主人描述的现象完全吻合。</para>
    ///
    /// <para>现口径：沿父链取 <b>最大的</b> sortingOrder + 1（不再只认 BackpackPanel），
    /// 保证技槽在所有祖先画布之上。</para>
    /// </summary>
    static void RaiseSlotCanvasAboveParent(Transform t)
    {
        if (t == null) return;
        var self = t.GetComponent<Canvas>();
        if (self == null) return;

        // 沿父链取最高 order：嵌套 Canvas 之间不按父子关系排序，只看 order 大小
        int baseOrder = 0;
        for (var p = t.parent; p != null; p = p.parent)
        {
            var pc = p.GetComponent<Canvas>();
            if (pc != null && pc.sortingOrder > baseOrder)
                baseOrder = pc.sortingOrder;
        }

        // 【2026-10-11 主人「技能还是不显示」】实测数据：技槽 101 > 父链最高 100、屏幕矩形 (310,244)~(410,344)
        // 在屏内、无同父兄弟覆盖、sprite/alpha/激活全正常 —— 但主人还是看不见。
        // 真凶：主人抽奖时**抽奖面板(920)一直开着**（连抽循环不收起），920 > 101 → 技槽被面板整片压住，
        // 只有面板重排那一瞬露一下 = 「闪一下又没了」。父链里没有 920（面板是独立 Canvas），
        // 所以沿父链取最大永远够不着它 → 基准再加一条：至少抬到 BattlePopup(920) 之上（=921）。
        // 替换确认(925)/概率公示(926)/结算(990)/装备提示(1000)/Toast 仍压得住技槽，不受影响。
        int target = Mathf.Max(baseOrder + 1, GameConfig.UiSort.BattlePopup + 1);
        if (self.sortingOrder < target)
        {
            self.overrideSorting = true;
            self.sortingOrder = target;
        }
    }

    /// <summary>
    /// 战斗中盖住底部 HUD（血条/技槽/背包变暗且不可点），摇杆与主动技槽例外——
    /// 它们会被顶到遮罩之上（见 RefreshSkillBarMaskState）。
    /// 整理阶段（BattleLootMode）必须关掉，否则背包格子和技槽点不动。
    /// </summary>
    public void TickBattleMask()
    {
        EnsureBattleMask();
        if (_battleMask == null) return;
        // 系统尚未装配（BattleUI.Awake 早于 AutoGameInitializer）时 bm 可能为 null，
        // 用 InstanceQuiet 静默查询，不刷 Error。
        var bm = BattleManager.InstanceQuiet;
        // 2026-10-05 主人拍板：抽奖面板展开期间<b>不许压暗</b>。
        // zhezhao 是 BackpackPanel 的最后一个子节点（画在最上层，730×610 覆盖面板 y≈75~685），
        // 而 BattleManager 进战斗就置 isInBattle=true、抽奖协程在它之后才跑 →
        // 抽奖期间遮罩一开，DraftRoot(抽奖按钮)/zhuangbei(装备)/BtnBackpack(背包) 全被压掉。
        // 判据只有一个：BattleEntryDraftPanel.IsLotteryOpen。遮罩显隐仍归本方法独家管。
        // 【2026-10-08 主人拍板「背包不用遮罩」】战斗中不再用黑幕压暗底部 HUD ——
        //   主人要「战斗过程中能点开背包查看」，而遮罩一开背包格子就点不动（见上方 157 行原注释：
        //   「整理阶段必须关掉，否则背包格子和技槽点不动」）。
        //   判据（原样备用，改回一行即可恢复）：
        //   bool on = bm != null && bm.isInBattle && !BattleLootMode.Active
        //             && !BattleEntryDraftPanel.IsLotteryOpen;
        //   遮罩恒关 → 下面 SetGraphicsRaycast(skillSlotRoot, !on) 会把技槽射线恢复成可点。
        bool on = false;
        if (on != _battleMaskOn)
        {
            _battleMaskOn = on;
            _battleMask.SetActive(on);
            // 【2026-10-05 主人拍板「不要摇杆了」→ 整条链路停用，先注释不删】
            // if (on) BattleJoystick.Instance?.RaiseOrganizeAbove();
            SetGraphicsRaycast(skillSlotRoot, !on);
        }
        // 每帧幂等保证：prefab 重导入 / 别的代码动过 sibling 顺序也能立即纠正，
        // 避免技能栏再次被遮罩盖掉（只比较 index，无 GC，代价可忽略）。
        if (_battleMaskOn && skillSlotRoot != null)
        {
            var maskTr = _battleMask.transform;
            if (skillSlotRoot.parent == maskTr.parent
                && skillSlotRoot.GetSiblingIndex() < maskTr.GetSiblingIndex())
                skillSlotRoot.SetSiblingIndex(maskTr.GetSiblingIndex() + 1);
        }
    }

    /// <summary>
    /// 战斗中把 4 个主动技槽（SkillBar）顶到遮罩之上。
    /// 遮罩 zhezhao 是 BackpackPanel 的最后一个子节点（黑幕 alpha≈0.59，730×670），
    /// 绘制顺序压在 SkillBar 上，战斗中整条技能栏被吃掉，表现就是「技能不见了」。
    /// 顶到遮罩之上后关掉射线，保留「战斗中无法调整」的语义（遮罩只是变暗，不接点击）。
    /// 整理阶段遮罩关闭，射线恢复，槽位照常可拖拽调序。
    /// </summary>
    public void RefreshSkillBarMaskState()
    {
        if (skillSlotRoot == null || _battleMask == null) return;
        // 技槽有自己的 Canvas 时，必须先抬层级，否则下面的兄弟排序对绘制顺序无效。
        RaiseSlotCanvasAboveParent(skillSlotRoot);
        if (!_battleMaskOn)
        {
            SetGraphicsRaycast(skillSlotRoot, true);
            return;
        }
        var maskTr = _battleMask.transform;
        if (skillSlotRoot.parent == maskTr.parent
            && skillSlotRoot.GetSiblingIndex() < maskTr.GetSiblingIndex())
            skillSlotRoot.SetSiblingIndex(maskTr.GetSiblingIndex() + 1);
        SetGraphicsRaycast(skillSlotRoot, false);
    }

    static void SetGraphicsRaycast(Transform root, bool on)
    {
        if (root == null) return;
        var gs = root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < gs.Length; i++)
            if (gs[i] != null) gs[i].raycastTarget = on;
    }

    /// <summary>本网格实际有几行（= 已绑定格子的最大 gridY + 1），用于判断「整行锁图案」该不该亮。</summary>
    int GridRowCount()
    {
        if (gridCells == null || gridCells.Count == 0) return GameConfig.BACKPACK_HEIGHT;
        int maxY = 0;
        for (int i = 0; i < gridCells.Count; i++)
        {
            var c = gridCells[i];
            if (c != null && c.gridY > maxY) maxY = c.gridY;
        }
        return maxY + 1;
    }

    // ===== 背包抽屉（BackpackPanel/GridContainer 的显隐，2026-10-09 主人拍板）=====

    /// <summary>玩家有没有手动点开过网格背包（点右上角「背包」按钮）。默认 false = 收起 ——
    /// 主人原话「背包弹窗只有在点击背包按钮的时候才出现」，所以战斗里默认不显示。</summary>
    bool _bagDrawerOpen;

    /// <summary>网格背包节点缓存，别每次刷新都递归找一遍。</summary>
    Transform _bagGridRoot;

    /// <summary>抽屉当前是否开着（供 BattleEntryDraftPanel 判「开打要不要自动收」）。</summary>
    public bool BagDrawerOpen => _bagDrawerOpen;

    /// <summary>
    /// 网格背包（GridContainer）显隐的<b>唯一出口</b>：<b>只认玩家自己点开</b>。
    /// <para>2026-10-11 主人拍板：整理阶段不再强制展开背包 —— 主人原话
    /// 「打完一关获得了100金币，那个背包怎么自动开启了，而且还跑到了最上面」。
    /// 旧实现把 <c>BattleLootMode.Active</c> 也算进 want，于是每关结算都把背包顶出来；
    /// 而这一关宝箱的装备已经全部折金（<c>ScrapRewardEquips</c>），根本没新东西可整理，
    /// 这一步对玩家纯属多余。想整理照样点右上角「背包」按钮（<see cref="ToggleBagDrawer"/>）。</para>
    /// </summary>
    void ApplyBagDrawerVisible()
    {
        // FindDeepChildIgnoreCase 是纯 Transform 递归，能找到 inactive 子节点 → 隐藏后仍取得到，安全。
        if (_bagGridRoot == null) _bagGridRoot = FindDeepChildIgnoreCase(transform, "GridContainer");
        if (_bagGridRoot == null) return;
        bool want = _bagDrawerOpen;
        if (_bagGridRoot.gameObject.activeSelf != want) _bagGridRoot.gameObject.SetActive(want);
    }

    /// <summary>「背包」按钮：切换网格背包的出现/收起（2026-10-09 主人拍板）。
    /// 2026-10-21 起打开时顺带刷新一次格子 —— 掉落碎片直接写存档不走背包系统，
    /// 不刷新就会显示上一次的旧内容（碎片数量对不上）。</summary>
    public void ToggleBagDrawer()
    {
        _bagDrawerOpen = !_bagDrawerOpen;
        ApplyBagDrawerVisible();
        if (_bagDrawerOpen) UpdateBackpackGrid();
    }

    /// <summary>收起网格背包 —— 开打那一刻由 BattleEntryDraftPanel 调（2026-10-09 主人拍板「战斗的时候自动关闭背包弹窗」）。</summary>
    public void CloseBagDrawer()
    {
        _bagDrawerOpen = false;
        ApplyBagDrawerVisible();
    }

    /// <summary>刷新下方网格背包。预制体是 4×3=12 格；2026-09-21 起战斗内只开放前 2 行（8 格），
    /// 第 3 行起上锁（<see cref="GameConfig.BATTLE_BACKPACK_ROWS"/>）；城镇/角色页不受此限制。</summary>
    public void UpdateBackpackGrid()
    {
        // 2026-10-09 主人拍板：每次刷新背包都顺带纠正一次显隐 —— 默认收起，整理阶段强制显示。
        ApplyBagDrawerVisible();

        // 战斗中捡到装备时可能还没绑过格子，先补绑再判空
        if (gridCells == null || gridCells.Count == 0)
            EnsureGridCellsBound();
        if (gridCells == null || gridCells.Count == 0)
        {
            Debug.LogWarning("[BattleUI] 背包格子未绑定（缺 GridContainer），装备无法显示");
            return;
        }

        // 2026-09-21：战斗内只开放前 BATTLE_BACKPACK_ROWS(2) 行，第 3 行起上锁（主人定）。
        int unlockedRows = GameConfig.GetBattleBackpackRows(SaveSystem.Instance?.Data);
        // 整行锁图案亮的条件 = 本网格实际行数 > 已解锁行数。
        // 原写法拿 GameConfig.BACKPACK_HEIGHT(3) 去比，12 格网格解锁 3 行时永远 false → 锁图案从不亮。
        bool bottomLocked = unlockedRows < GridRowCount();

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
                var def = bip.item.Def;
                // 【2026-10-21 主人需求「卷轴/材料也不可用」→ 追加「去掉治疗药水吧 会把技能和佣兵弱化」】
                // 战斗背包里**所有道具**一律降级为纯展示位：图标 + 数量，点开只看说明，
                // 不弹「使用/丢弃」浮层（与天赋石/碎片/金币同口径）——战斗内没有任何可用道具。
                if (def != null)
                {
                    placements.Add(new BackpackGridVisual.ItemPlacement
                    {
                        x = bip.x, y = bip.y, w = 1, h = 1,
                        displayName = bip.item.Name,
                        displayInfo = BuildBattleItemDisplayInfo(def),
                        displayIcon = ItemDefs.LoadIcon(bip.item.defId),
                        displayCount = bip.item.count
                    });
                    continue;
                }
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
        // 【2026-10-21 主人需求】真道具/装备摆完后，把「本局获得」的天赋石、日志碎片塞进剩余空格展示
        AppendRunGainDisplays(placements, unlockedRows);
        BackpackGridVisual.ClearAndPlace(gridRt, gridLayout, placements, FindGridCellRect, BattleLootMode.Active);
        RestoreBackpackCellVisuals();
        // 换装后同步底部 5 个装备快捷槽
        UpdateEquipQuickSlots();
        Debug.Log($"[BattleUI] 背包刷新 items={placements.Count} cells={gridCells.Count} layout={(gridLayout != null)}");
    }

    /// <summary>把「本局获得」的天赋石 / 日志碎片塞进背包剩余空格做<b>纯展示</b>（可查看，不可使用）。
    ///
    /// <para>【2026-10-21 主人需求「战斗背包只显示当前战斗里获得的东西」】这里只展示<b>本局获得</b>的量：
    /// 天赋石用 <see cref="GridBackpackSystem.RunGainedTalents"/>（开局对钱包快照的差值），
    /// 日志碎片用 <see cref="AdventureLogFragments.GetRunGained"/>；二者都为 0 时不占格。
    /// 点击弹通用告知框看说明/配方进度（见 <see cref="BackpackGridVisual"/> 展示位分支）。</para>
    ///
    /// <para>摆格顺序：真道具/装备已占的格先算，展示位从第 0 行起逐行找第一个空格；
    /// 解锁行全满时 LogWarning（不静默吞）。</para>
    /// </summary>
    void AppendRunGainDisplays(List<BackpackGridVisual.ItemPlacement> placements, int unlockedRows)
    {
        // 真道具/装备已占的格先记下（按多格占位算）
        var occupied = new HashSet<long>();
        foreach (var pl in placements)
        {
            for (int dy = 0; dy < pl.h; dy++)
                for (int dx = 0; dx < pl.w; dx++)
                    occupied.Add(((long)(pl.x + dx) << 32) | (uint)(pl.y + dy));
        }

        int width = GameConfig.BACKPACK_WIDTH;

        // 1) 日志碎片（本局获得）
        var recipes = AdventureLogFragments.Recipes;
        if (recipes != null)
        {
            for (int i = 0; i < recipes.Length; i++)
            {
                var r = recipes[i];
                int gained = AdventureLogFragments.GetRunGained(r.FragmentId);
                if (gained <= 0) continue;
                TryPlaceDisplay(occupied, width, unlockedRows, placements,
                    AdventureLogFragments.DisplayName(r.FragmentId),
                    "材料类碎片，不能直接使用。\n" + AdventureLogFragments.FormatRecipeLine(i) + "\n通关关卡有概率掉落。",
                    BackpackGridVisual.LogFragmentIcon(), gained);
            }
        }

        // 2) 金币（本局获得）
        long gold = 0;
        var bag = GridBackpackSystem.Instance;
        if (bag != null) gold = bag.RunGainedGold;
        if (gold > 0)
        {
            TryPlaceDisplay(occupied, width, unlockedRows, placements,
                "金币",
                "本局获得的金币（掉落 / 首通奖励 / 宝箱等）。\n回城后在商店、天赋页等消费。",
                CommonIconSprites.Load("icon_gold"), (int)gold);
        }

        // 3) 天赋石（本局获得）
        long stones = 0;
        if (bag != null) stones = bag.RunGainedTalents;
        if (stones > 0)
        {
            TryPlaceDisplay(occupied, width, unlockedRows, placements,
                "天赋石",
                "本局击杀精英 / Boss 掉落的天赋石。\n回城后在天赋页点亮、升级节点。",
                CommonIconSprites.Load("icon_talent_stone"), (int)stones);
        }
    }

    /// <summary>在已占用集合中找第一个空格塞一个 1×1 展示位；放不下就 LogWarning（不静默吞）。</summary>
    void TryPlaceDisplay(HashSet<long> occupied, int width, int unlockedRows,
        List<BackpackGridVisual.ItemPlacement> placements,
        string name, string info, Sprite icon, int count)
    {
        for (int y = 0; y < unlockedRows; y++)
        {
            for (int x = 0; x < width; x++)
            {
                long key = ((long)x << 32) | (uint)y;
                if (occupied.Contains(key)) continue;
                occupied.Add(key);
                placements.Add(new BackpackGridVisual.ItemPlacement
                {
                    x = x, y = y, w = 1, h = 1,
                    displayName = name, displayInfo = info, displayIcon = icon, displayCount = count
                });
                return;
            }
        }
        Debug.LogWarning("[BattleUI] 背包解锁行已满，「" + name + "」没有空格可展示");
    }

    /// <summary>战斗背包里「不可用道具」展示位的告知正文：道具说明 + 不可用原因。</summary>
    static string BuildBattleItemDisplayInfo(ItemDef def)
    {
        string why = def.type == ItemType.Material
            ? "材料类道具，不能直接使用。"
            : "战斗中不可使用。";
        string desc = string.IsNullOrEmpty(def.desc) ? "" : def.desc + "\n";
        return desc + why;
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

        if (BattleLootMode.Active && lootConfirmButton == null)
        {
            // 2026-10-11：整理阶段开了却没有确认按钮 = 玩家必卡在这一步（主人报「没有继续按钮、
            // 也没出传送门」）。同上，打 Error 让主人一眼看到是按钮没建/没绑上。
            Debug.LogError("[BattleUI] 整理阶段已开启，但 lootConfirmButton 为空 → 玩家无法确认，流程卡死");
        }

        if (lootConfirmButton != null)
        {
            var txt = lootConfirmButton.GetComponentInChildren<Text>(true);
            if (txt != null)
                txt.text = LootConfirmLabel;    // 唯一出口，与创建处同值
            // 整理功能已移除：这个按钮只在拾取模式出现
            lootConfirmButton.gameObject.SetActive(BattleLootMode.Active);
            // 竖屏适配会重锚/改高面板，创建时算好的落点会失效 → 每次显隐都按当前面板重摆。
            if (BattleLootMode.Active)
            {
                ReanchorLootConfirmButton();
                // 诊断：把按钮最终落点打出来，主人卡住时看日志就知道它跑到哪去了
                // （旧实现按面板底边下方 60 算，面板贴底时按钮掉到屏幕外，就是这个症状）。
                var rt = lootConfirmButton.transform as RectTransform;
                if (rt != null)
                    Debug.Log($"[BattleUI] 整理「{LootConfirmLabel}」落点 anchoredPosition={rt.anchoredPosition} " +
                              $"父节点={rt.parent?.name}");
            }
        }
        UpdateBackpackGrid();
        // 整理阶段才允许拖动技槽调序（战斗中不开放，避免误触改掉释放优先级）
        RefreshSkillSlotDragState();
        if (BattleLootMode.Active) MaybeShowSkillReorderHint();
        TickBattleMask();
        // 【2026-10-05 主人拍板「不要摇杆了」→ 整条链路停用，先注释不删】
        // 原每帧校正摇杆开关：
        // var bm = BattleManager.InstanceQuiet;
        // if (bm != null)
        // {
        //     BattleJoystick.Instance?.SetVisible(!BattleLootMode.Active
        //         && bm.isInBattle
        //         && bm.UnitsCanAct);
        // }
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
        TargetIndicator.Ensure();
        // 【2026-10-05 主人拍板「不要摇杆了」→ 整条链路停用，先注释不删】
        // 摇杆就建在这里；注释掉 = 摇杆永远不会被创建，下面所有 BattleJoystick.Instance?. 都是空转。
        // BattleJoystick.EnsureOn(transform);
        // 道具操作浮层挂在 BattleUI 下（保证在 Canvas 内且在最上层）
        BackpackItemActionUI.Ensure(transform);
        EnsureBattleMask();
        // 2026-10-09 主人拍板：进关必是收起态（本方法由 BattleManager 进战斗时调），
        // 上一局残留的「开着」不会带到下一局。
        _bagDrawerOpen = false;
        ApplyBagDrawerVisible();
        RefreshLootModeChrome();
    }

    /// <summary>
    /// 【2026-10-06 主人拍板】战斗背包格子底图恒为美术原色 —— 不再按「有装备 / 已穿戴」刷代码色
    /// （旧实现刷深蓝 0.24,0.30,0.38 / 铜色 0.30,0.26,0.16，把美术底图盖掉了）。
    /// 稀有度改由图标外沿那圈描边表达（BackpackGridVisual → EquipRarityRim），与底色无关。
    /// 这里只负责把上次刷新可能残留的染色还原。
    /// </summary>
    void RestoreBackpackCellVisuals()
    {
        if (gridCells == null) return;
        foreach (var cell in gridCells)
        {
            if (cell == null) continue;
            cell.SetEmptyVisual();
        }
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
