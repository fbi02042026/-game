using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 任务表加载器（2026-10-26 新增，任务体系单独成模块的数据层）。
///
/// 表：Assets/Data/Source/Tables/quest.csv → Resources/Data/Tables/quest.bytes（CookAll → CookQuest）。
/// 内容：主线（category=主线，8 章 × 5~6 条，含 clear_stages 密度行）+ 日常（category=日常，7 条）
///       + 周常（category=周常，1 条）。（2026-10-29 扩编：日常 2→7、新增周常，见 DailyTaskSystem）
/// 支线不进表：支线 = 支线岛（2026-10-23 合并方案），运行时读 SaveData.branchRoll1/2，见 QuestBarData.cs。
///
/// 消费方：MainQuestDefs（主线迁表，结构体字段 1:1 对应）与 MainQuestSystem.DailyViews（日常文案参数化）。
/// 解析：GameTableCsv（跳 # 注释/空行、引号内逗号不切分），与全项目其它表同一套。
/// 缺表不兜底：LogError 后返回空，任务面板走「本章主线任务配置中」空态（主人纪律：让我看见）。
/// </summary>
public static class QuestTable
{
    public struct Row
    {
        public string id;
        public string category;     // 主线 / 日常
        public int chapter;         // 章节号；日常填 0
        public string title;
        public string desc;         // 主线留空走 AutoDesc；日常可带 {0}/{1} 占位
        public string targetType;   // clear_stage / clear_stages / talk_npc / watch_story / recruit_merc / upgrade_talent / daily_login / daily_clear_stages
        public string param;
        public int targetCount;
        public int rewardStones;    // 主线完成发天赋石
        public int order;           // 同章内推进顺序
        public string rewardText;   // 日常展示用奖励文案
        public int rewardExp;       // 任务奖励经验（2026-10-29 扩编末列；0=不发，DailyTaskSystem 随达标自动发）
    }

    static readonly List<Row> _rows = new List<Row>();
    static bool _loaded;

    public static bool HasData => _loaded && _rows.Count > 0;

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        string raw = GameTableStore.LoadText(ContentPaths.Data.Quest);
        if (string.IsNullOrEmpty(raw))
        {
            Debug.LogError("[Quest] 任务表缺失：Resources/Data/Tables/quest.bytes（源表 Assets/Data/Source/Tables/quest.csv，跑 CookAll 生成）");
            return;
        }

        var lines = GameTableCsv.ParseRows(raw);
        for (int i = 1; i < lines.Count; i++)   // 第 0 行 = 表头
        {
            var c = lines[i];
            if (c.Length < 10) continue;

            var row = new Row
            {
                id = c[0],
                category = c[1],
                chapter = GameTableCsv.TryInt(c[2], out int ch) ? ch : 0,
                title = c[3],
                desc = c[4],
                targetType = c[5],
                param = c[6],
                targetCount = GameTableCsv.TryInt(c[7], out int tc) ? tc : 0,
                rewardStones = GameTableCsv.TryInt(c[8], out int rs) ? rs : 0,
                order = GameTableCsv.TryInt(c[9], out int od) ? od : 0,
                rewardText = c.Length > 10 ? c[10] : "",
                rewardExp = c.Length > 12 && GameTableCsv.TryInt(c[12], out int rExp) ? rExp : 0   // 末列（2026-10-29 扩编）
            };
            _rows.Add(row);
        }
        Debug.Log($"[Quest] 已加载 {_rows.Count} 条任务");
    }

    /// <summary>主线行，按 (chapter, order) 升序 —— 顺序即推进顺序。</summary>
    public static List<Row> MainRows()
    {
        EnsureLoaded();
        var list = _rows.FindAll(r => r.category == "主线");
        list.Sort((a, b) => a.chapter != b.chapter
            ? a.chapter.CompareTo(b.chapter)
            : a.order.CompareTo(b.order));
        return list;
    }

    /// <summary>按 id 取一条日常行（daily_login / daily_clear）。</summary>
    public static bool TryDaily(string id, out Row row)
    {
        EnsureLoaded();
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].category == "日常" && _rows[i].id == id)
            {
                row = _rows[i];
                return true;
            }
        }
        row = default;
        return false;
    }

    /// <summary>某类别全部行，按 order 升序（2026-10-29 扩编：日常 7 条 + 周常，见 DailyTaskSystem）。</summary>
    public static List<Row> RowsOfCategory(string category)
    {
        EnsureLoaded();
        var list = _rows.FindAll(r => r.category == category);
        list.Sort((a, b) => a.order.CompareTo(b.order));
        return list;
    }
}
