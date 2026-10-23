using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 章节路线表（2026-09-14 新增 / 2026-10-23 合并方案改线性主线）
/// ------------------------------------------------------------------
/// 【2026-10-23 合并方案】主线改为**线性 1→2→3→4→5→6→7→8**（原「1 →(2/3/4 三选一)→ 5…8」分叉退役）：
///     支线内容整体迁到「支线岛」—— 通关第 1 章后随机派定 2 个支线岛（岛壳依次解锁，岛内容
///     = SideIslandDefs 的单 Boss 特殊关，见 EnsureForkRoll），与主线完全旁挂。
/// · 旧支线章 2/3/4 并入主线按序解锁；chosenBranch/分支选择 UI 随之退役（字段保留不动）。
/// · 通关第 8 章后 branchRoll1/branchRoll2 归零；下一周目重新派岛。
///
/// 注意：内部 chapterId 语义完全不变——怪物素材 / 战斗背景 / 数值 / 成就
/// 仍按 chapterId 取值。本表只决定「下一章是谁」和「哪些章能进」。
/// 表缺失时走 HardcodedFallback，行为与表一致。
/// </summary>
public static class ChapterRouteTable
{
    public struct Row
    {
        public int chapter;
        public int[] next;
        public int[] reqCleared;
        public bool needCh8Clear;
    }

    static readonly List<Row> _rows = new List<Row>();
    static bool _loaded;

    public static bool HasData => _loaded && _rows.Count > 0;

    public static void Reload()
    {
        _loaded = false;
        _rows.Clear();
        EnsureLoaded();
    }

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        string raw = GameTableStore.LoadText(ContentPaths.Data.ChapterRoute);
        if (string.IsNullOrEmpty(raw)) return;

        var lines = GameTableCsv.ParseRows(raw);
        for (int i = 1; i < lines.Count; i++)
        {
            var c = lines[i];
            if (c == null || c.Length < 1) continue;
            if (!GameTableCsv.TryInt(c[0], out int chapter)) continue;

            _rows.Add(new Row
            {
                chapter = chapter,
                next = ParseIntList(c.Length > 1 ? c[1] : ""),
                reqCleared = ParseIntList(c.Length > 2 ? c[2] : ""),
                needCh8Clear = c.Length > 3 && GameTableCsv.TryInt(c[3], out int n) && n != 0
            });
        }
        Debug.Log($"[ChapterRoute] 已加载 {_rows.Count} 条");
    }

    static int[] ParseIntList(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return new int[0];
        var parts = raw.Split(';');
        var list = new List<int>(parts.Length);
        for (int i = 0; i < parts.Length; i++)
        {
            if (GameTableCsv.TryInt(parts[i].Trim(), out int v) && v > 0)
                list.Add(v);
        }
        return list.ToArray();
    }

    static bool TryGet(int chapter, out Row row)
    {
        EnsureLoaded();
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].chapter == chapter)
            {
                row = _rows[i];
                return true;
            }
        }
        row = default;
        return false;
    }

    // ============================================================
    // 硬编码兜底（表缺失时）
    // ============================================================

    /// <summary>主线默认序列（2026-10-23 合并方案：线性 1→8）</summary>
    static readonly int[] FallbackMain = { 1, 2, 3, 4, 5, 6, 7, 8 };
    /// <summary>【2026-10-23 退役】旧支线章（3/4 章）序列——分叉退役后无引用，保留不删（下线≠删除）。</summary>
    static readonly int[] FallbackBranch = { 3, 4 };

    // ============================================================
    // 支线岛随机派定（2026-10-23 合并方案：岛壳用「2 岛依次解锁」+ 岛内容用 SideIslandDefs 单 Boss 关）
    // ============================================================

    /// <summary>
    /// 【2026-10-23 合并方案】通关第 1 章后随机派定**支线岛**（洗牌袋，只掷没通关过的岛；
    /// 岛定义见 SideIslandDefs，branchRoll1/branchRoll2 记岛 id 1..8）。
    /// 通关该岛后**再次调用本方法自动派第二个岛**（岛通关结算处触发，见 BattleManager 岛结算）。
    /// 本周目最多 2 个岛（两条制）；8 岛全通后洗牌袋重置（清岛通关记录，开启新一轮）。
    /// 幂等：当前派定岛没打完、或两条已派满时直接返回，绝不重复掷。
    /// 调用点：通关第 1 章（ChapterManager.OnStageComplete）与补掷入口
    /// （AdventureUI.RefreshAvailableChapters / WorldMapPopup.RefreshAvailability）。
    /// 🔴 掷点逻辑严禁出现在 CanEnter / NextChapter / AvailableChapters 等查询函数里
    /// （它们被循环调用，会重复掷）。
    /// </summary>
    public static void EnsureForkRoll(SaveData data)
    {
        if (data == null) return;
        if (!data.HasClearedChapter(1)) return;
        if (data.branchRoll2 != 0) return;                       // 两条已派满（两条制）
        // 当前派定的岛还没打完 → 保留不动（正在打/没打完）
        if (data.branchRoll1 != 0 && !data.HasClearedSideIsland(data.branchRoll1)) return;

        var cands = new List<int>();
        var cleared = data.clearedSideIslandIds; // 跨周目持久（一次性打完锁）；可能为 null，按空集合处理
        for (int id = 1; id <= SideIslandDefs.Count; id++)
            if (cleared == null || !cleared.Contains(id)) cands.Add(id);
        if (cands.Count == 0)
        {
            // 8 岛全通 → 洗牌袋重置：清岛通关记录，开启新一轮（所有岛重新可掷可打）
            data.ResetSideIslandClears();
            for (int id = 1; id <= SideIslandDefs.Count; id++) cands.Add(id);
            Debug.Log("[ChapterRoute] 支线岛 8 岛全通，洗牌袋重置，开启新一轮");
        }

        int pick = cands[UnityEngine.Random.Range(0, cands.Count)];
        if (data.branchRoll1 == 0) data.branchRoll1 = pick;
        else data.branchRoll2 = pick;
        SaveSystem.Instance.Save();
        Debug.Log($"[ChapterRoute] 随机派定支线岛（第 {(data.branchRoll2 != 0 ? 2 : 1)} 个）：{SideIslandDefs.Get(pick)?.name ?? "?"}（岛{pick}）");
    }

    // ============================================================
    // 查询
    // ============================================================

    /// <summary>该章的下一章候选（1 → {2,3,4}；2/3/4 → {5}；8 → 空）。</summary>
    public static List<int> NextChapters(int chapter)
    {
        EnsureLoaded();
        var result = new List<int>();
        if (TryGet(chapter, out var row))
        {
            if (row.next != null)
                for (int i = 0; i < row.next.Length; i++)
                    result.Add(row.next[i]);
            return result;
        }

        // 兜底（2026-10-23 合并方案：线性 1→8，与表口径一致）
        if (chapter >= 1 && chapter <= 7)
        {
            result.Add(chapter + 1);
            return result;
        }
        return result;
    }

    /// <summary>主线默认下一章（不看存档）；已是最后一章返回 -1（终局）。</summary>
    public static int DefaultNext(int chapter)
    {
        EnsureLoaded();
        if (TryGet(chapter, out var row) && row.next != null && row.next.Length > 0)
            return row.next[0];

        if (chapter == 1) return 2;
        for (int i = 0; i < FallbackMain.Length - 1; i++)
            if (FallbackMain[i] == chapter) return FallbackMain[i + 1];
        return -1;
    }

    /// <summary>考虑了玩家进度的下一章（2026-10-23 合并方案：主线线性，等价 DefaultNext；保留 data 参数兼容旧调用）。</summary>
    public static int NextChapter(int chapter, SaveData data)
    {
        return DefaultNext(chapter);
    }

    /// <summary>该章现在能不能进。</summary>
    public static bool CanEnter(int chapter, SaveData data)
    {
        if (chapter < 1 || chapter > 8) return false;

        // 第 1 章永远开放
        if (chapter == 1) return true;

        bool cleared(int ch) => data != null && data.HasClearedChapter(ch);

        if (TryGet(chapter, out var row))
        {
            if (row.reqCleared != null && row.reqCleared.Length > 0)
            {
                bool any = false;
                for (int i = 0; i < row.reqCleared.Length; i++)
                    if (cleared(row.reqCleared[i])) { any = true; break; }
                if (!any) return false;
            }
            // 【2026-10-23 合并方案】2/3/4 章并入主线按序解锁（reqCleared 由表给：2→1、3→2、4→3），
            // 原「chosenBranch 派定门 + needCh8Clear 门 + 支线互斥」全部退役；支线内容迁支线岛（SideIslandDefs）。
            return true;
        }

        // 兜底（与表路径同口径）
        // 【2026-10-23 合并方案】线性 1→8：每章解锁 = 通关前一章。
        if (chapter >= 2 && chapter <= 8) return cleared(chapter - 1);
        return false;
    }

    /// <summary>当前所有可进章节（升序）——冒险页箭头在这个集合内循环。</summary>
    public static List<int> AvailableChapters(SaveData data)
    {
        EnsureLoaded();
        var list = new List<int>();
        for (int ch = 1; ch <= 8; ch++)
            if (CanEnter(ch, data)) list.Add(ch);
        list.Sort();
        return list;
    }

    /// <summary>【2026-10-23 退役】分叉点可选支线（原 2/3/4 三选一）——主线线性后最多返回 1 条，
    /// 分叉提示 UI（BuildBranchHint 的 choices.Count &lt; 2 判断）自然不再显示。保留不删（下线≠删除）。</summary>
    public static List<int> BranchChoices(SaveData data)
    {
        var list = new List<int>();
        foreach (int ch in NextChapters(1))
            if (CanEnter(ch, data)) list.Add(ch);
        list.Sort();
        return list;
    }

    // ============================================================
    // 继续 / 当前应推进的章节（2026-10-21 续）
    // ============================================================

    /// <summary>
    /// 【2026-10-21 续】玩家当前应推进的章节：在「可选章节（CanEnter 通过的集合）」里取
    /// 第一个未通关的；若全部通关（本周目路径已走完）则返回可选集合的最后一章（= 第 8 章，用于 NG+ 重打）。
    /// · 纯查询，严禁触发掷点 / 写存档（不要在这里调 EnsureForkRoll / SaveSystem.Save）。
    /// · 只从 ChapterRouteTable 自身与 SaveData 读，不依赖 UI 状态。
    /// </summary>
    public static int CurrentChapterToAdvance(SaveData data)
    {
        var avail = AvailableChapters(data);
        if (avail == null || avail.Count == 0) return 1;
        bool cleared(int ch) => data != null && data.HasClearedChapter(ch);
        for (int i = 0; i < avail.Count; i++)
            if (!cleared(avail[i]))
                return avail[i];
        return avail[avail.Count - 1]; // 全通关 → 最后一章（第 8 章，NG+ 重打入口）
    }
}
