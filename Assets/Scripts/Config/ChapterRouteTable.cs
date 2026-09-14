using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 章节路线表（2026-09-14 新增）
/// ------------------------------------------------------------------
/// 玩家看到的不是一条线性 1→2→3→4→5→6→7→8，而是：
///     第1章 →（第2章 / 第3章 / 第4章 三选一）→ 第5章 → 第6章 → 第7章 → 第8章
/// · 前期只解锁第 2 章；第 3、4 章是支线，**通关第 8 章后**才开放。
/// · 一次周目只能走 2/3/4 中的一条（chosenBranch 锁定），通关第 8 章后归零可重选。
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

    /// <summary>主线默认序列</summary>
    static readonly int[] FallbackMain = { 1, 2, 5, 6, 7, 8 };
    /// <summary>通关第 8 章后才开放的支线</summary>
    static readonly int[] FallbackBranch = { 3, 4 };

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

        // 兜底
        if (chapter == 1)
        {
            result.Add(2);
            result.AddRange(FallbackBranch);
            return result;
        }
        for (int i = 0; i < FallbackMain.Length - 1; i++)
        {
            if (FallbackMain[i] == chapter)
            {
                result.Add(FallbackMain[i + 1]);
                return result;
            }
        }
        // 支线章节同样汇合到第 5 章
        for (int i = 0; i < FallbackBranch.Length; i++)
        {
            if (FallbackBranch[i] == chapter)
            {
                result.Add(5);
                return result;
            }
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
        for (int i = 0; i < FallbackBranch.Length; i++)
            if (FallbackBranch[i] == chapter) return 5;
        return -1;
    }

    /// <summary>考虑了玩家已选支线的下一章（第 1 章通关后用）。</summary>
    public static int NextChapter(int chapter, SaveData data)
    {
        if (chapter == 1)
        {
            int chosen = data != null ? data.chosenBranch : 0;
            var cands = NextChapters(1);
            if (chosen > 0 && cands.Contains(chosen) && CanEnter(chosen, data))
                return chosen;
            foreach (int c in cands)
                if (CanEnter(c, data)) return c;
            return DefaultNext(1);
        }
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
            if (row.needCh8Clear && !cleared(8)) return false;
            // 支线互斥：本周目已选了另一条支线就进不去
            if (row.needCh8Clear && data != null && data.chosenBranch > 0 && data.chosenBranch != chapter)
                return false;
            return true;
        }

        // 兜底
        if (chapter == 2) return cleared(1);
        if (chapter == 3 || chapter == 4)
        {
            if (!cleared(1) || !cleared(8)) return false;
            if (data != null && data.chosenBranch > 0 && data.chosenBranch != chapter) return false;
            return true;
        }
        if (chapter == 5) return cleared(2) || cleared(3) || cleared(4);
        if (chapter == 6) return cleared(5);
        if (chapter == 7) return cleared(6);
        if (chapter == 8) return cleared(7);
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

    /// <summary>分叉点可选的多条支线（当前能进的 2/3/4）。</summary>
    public static List<int> BranchChoices(SaveData data)
    {
        var list = new List<int>();
        foreach (int ch in NextChapters(1))
            if (CanEnter(ch, data)) list.Add(ch);
        list.Sort();
        return list;
    }
}
