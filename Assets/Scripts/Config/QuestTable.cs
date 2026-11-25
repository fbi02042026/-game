using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 任务表加载器（2026-10-26 新增，任务体系单独成模块的数据层）。
///
/// 表：Assets/Data/Source/Tables/quest.csv → Resources/Data/Tables/quest.bytes（CookAll → CookQuest）。
/// 内容：主线（category=主线，8 章 × 5~6 条，含 clear_stages 密度行）+ 日常（category=日常，12 条：8 条每天 + 4 条限定星期）
///       + 挑战（category=挑战，2026-11-05 新增：困难 8 条 / 噩梦 6 条）。
///       （2026-10-29 扩编：日常 2→7；2026-11-25 主人拍板「周常不要单独设置，可以在固定 7 天中的某一天或两天或三天出现一次」
///         → 原周常「百战精锐」并入日常（周四五六出现），周常分类取消；同日删「清剿 50」、经验整体降一档、新增资源副本/裂隙探底/佣兵闲话）
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
        public string targetType;   // clear_stage / clear_stages / talk_npc / watch_story / recruit_merc / upgrade_talent / daily_login / daily_clear_stages / daily_pet_feed / daily_talent_click / daily_merc_starup / daily_activity_clear / daily_rift_clear / daily_merc_talk
        public string param;
        public int targetCount;
        public int rewardStones;    // 主线完成发天赋石
        public int order;           // 同章内推进顺序
        public string rewardText;   // 日常展示用奖励文案
        public int rewardExp;       // 任务奖励经验（2026-10-29 扩编末列；0=不发，DailyTaskSystem 随达标自动发）
        public int rewardGold;      // 主线完成发金币（2026-11-01 主人拍板：主线多少都要给点奖励，哪怕几十金币）
        /// <summary>
        /// 【2026-11-05 建议修】任务自身难度（0=普通 / 1=困难 / 2=噩梦；缺列/缺值 → 0）。
        /// 现有任务表没有这一列，全部隐含普通难度；任务跳转按本字段落位。
        /// </summary>
        public int difficulty;
        /// <summary>
        /// 出现日（2026-11-25 主人拍板「周常不要单独设置，可以在固定 7 天中的某一天或两天或三天出现一次」）：
        /// 空 = 每天出现；否则是 ISO 星期数字串（1=周一 … 7=周日，如 "16" = 周一/周六、"456" = 周四五六）。
        /// 只影响**展示与结算**，不改变任务内容；功能未解锁时的过滤见 MainQuestSystem.DailyRowActiveToday。
        /// </summary>
        public string weekdays;
        /// <summary>
        /// 钻石奖励（2026-11-25 新增，第 17 列）：日常限定日任务专用，0/缺省 = 不发。
        /// 目前只有 daily_starup（百战精锐）用，沿用原周常「钻石 ×50」口径。
        /// 以后要接激励视频（看广告翻倍 / 看广告领取）在 DailyTaskSystem.SettleRows 发奖处加分支，
        /// 广告次数与频次口径参考 ArenaRules.AdBonusTries / ResourceAdRewards，别另造一套。
        /// </summary>
        public int rewardDiamond;
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
                rewardExp = c.Length > 12 && GameTableCsv.TryInt(c[12], out int rExp) ? rExp : 0,  // 末列（2026-10-29 扩编）
                rewardGold = c.Length > 13 && GameTableCsv.TryInt(c[13], out int rGold) ? rGold : 0, // 主线金币奖励（2026-11-01）
                // 第 15 列（下标 14）可选：任务难度。源表不加列时全部按 0（普通）——老表/老档不受影响。
                difficulty = c.Length > 14 && GameTableCsv.TryInt(c[14], out int rDiff) ? Mathf.Clamp(rDiff, 0, 2) : 0,
                // 第 16 列（下标 15）可选：出现日（2026-11-25）。主线/挑战行只有 15 列 → 空 = 每天，老表零兼容。
                weekdays = c.Length > 15 ? c[15].Trim() : "",
                // 第 17 列（下标 16）可选：钻石奖励（2026-11-25）。缺省 0 = 不发。
                rewardDiamond = c.Length > 16 && GameTableCsv.TryInt(c[16], out int rDia) ? Mathf.Max(0, rDia) : 0
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

    /// <summary>
    /// 挑战行（2026-11-05 新增，category=挑战：困难/噩梦的章末 Boss），按 (chapter, order) 升序。
    /// 🔴 独立于主线链：不进 <see cref="MainRows"/>，不参与主线推进/「本章 X/Y」计数。
    /// </summary>
    public static List<Row> ChallengeRows()
    {
        EnsureLoaded();
        var list = _rows.FindAll(r => r.category == "挑战");
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

    /// <summary>今天是否是该行的出现日（2026-11-25 新增；weekdays 空 = 每天出现）。</summary>
    public static bool AppearsToday(Row row) => AppearsOn(row, TodayWeekday1Based());

    /// <summary>
    /// 指定 ISO 星期（1=周一 … 7=周日）是否命中 weekdays。
    /// weekdays 空 / 星期越界 → true（按每天处理，缺省即老口径）；"16" 命中周一周六。
    /// </summary>
    public static bool AppearsOn(Row row, int weekday1Based)
    {
        if (string.IsNullOrEmpty(row.weekdays)) return true;
        if (weekday1Based < 1 || weekday1Based > 7) return true;
        return row.weekdays.IndexOf((char)('0' + weekday1Based)) >= 0;
    }

    /// <summary>今天的 ISO 星期（1=周一 … 7=周日，本地时间，与活动副本开放日同一口径）。</summary>
    public static int TodayWeekday1Based()
    {
        int dow = (int)System.DateTime.Now.DayOfWeek;   // DayOfWeek: 周日 = 0
        return dow == 0 ? 7 : dow;
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
