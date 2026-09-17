using System.Collections.Generic;

/// <summary>
/// 佣兵解锁门槛（2026-09-17 新增）。
///
/// 起因：原先用「酒馆 Lv2 / Lv3」当门槛，但**酒馆根本没有等级功能**
/// （townLevel.tavern 只是个被 clamp 到 0~2 的残留字段，玩家看不到也升不了），
/// 玩家只会看到一句永远达不成的"需酒馆 Lv3"。
///
/// 改成**明确可达成、能写在卡面上的条件**，让未解锁的卡本身成为目标：
/// 玩家看到「通关第 3 章解锁」就知道该去做什么。
///
/// 条件只依赖现有存档数据，不新增计数器：
///   MercCount      —— 已解锁佣兵数（SaveData.unlockedMercIds.Count）
///   ChapterCleared —— 已通关章节（SaveData.clearedChapterIds）
/// </summary>
public static class MercUnlockGate
{
    public enum Kind
    {
        /// <summary>无条件，随时可解锁。</summary>
        None = 0,
        /// <summary>已解锁佣兵数 ≥ Value。</summary>
        MercCount = 1,
        /// <summary>已通关第 Value 章。</summary>
        ChapterCleared = 2
    }

    public struct Rule
    {
        public Kind kind;
        public int value;
        /// <summary>未达成时卡面按钮上显示的文案（要短，≤8 字）。</summary>
        public string text;
    }

    // 只在酒馆名册出现的稀有 + 传说才有门槛；普通档在商店直售，无门槛。
    static readonly Dictionary<string, Rule> Table = new Dictionary<string, Rule>
    {
        // ---- 稀有：先用「已解锁人数」做软门槛，边买边开 ----
        { "H002", new Rule { kind = Kind.MercCount,     value = 5, text = "解锁 5 名佣兵" } },
        { "H016", new Rule { kind = Kind.MercCount,     value = 5, text = "解锁 5 名佣兵" } },
        { "H008", new Rule { kind = Kind.MercCount,     value = 7, text = "解锁 7 名佣兵" } },
        { "H012", new Rule { kind = Kind.MercCount,     value = 7, text = "解锁 7 名佣兵" } },
        { "H017", new Rule { kind = Kind.MercCount,     value = 9, text = "解锁 9 名佣兵" } },

        // ---- 稀有（高价）：推图进度 ----
        { "H003", new Rule { kind = Kind.ChapterCleared, value = 2, text = "通关第 2 章" } },
        { "H020", new Rule { kind = Kind.ChapterCleared, value = 2, text = "通关第 2 章" } },
        { "H009", new Rule { kind = Kind.ChapterCleared, value = 2, text = "通关第 2 章" } },
        { "H006", new Rule { kind = Kind.ChapterCleared, value = 3, text = "通关第 3 章" } },
        { "H013", new Rule { kind = Kind.ChapterCleared, value = 3, text = "通关第 3 章" } },
        { "H022", new Rule { kind = Kind.ChapterCleared, value = 3, text = "通关第 3 章" } },

        // ---- 传说：长线目标，只走酒馆，不进商店 ----
        { "H004", new Rule { kind = Kind.ChapterCleared, value = 4, text = "通关第 4 章" } },
        { "H010", new Rule { kind = Kind.ChapterCleared, value = 5, text = "通关第 5 章" } },
        { "H014", new Rule { kind = Kind.ChapterCleared, value = 6, text = "通关第 6 章" } },
    };

    /// <summary>该佣兵是否已达解锁条件（没配规则的视为无条件）。</summary>
    public static bool IsOpen(string hireId, SaveData data)
    {
        if (string.IsNullOrEmpty(hireId)) return false;
        if (!Table.TryGetValue(hireId, out var rule)) return true;
        if (data == null) return false;

        switch (rule.kind)
        {
            case Kind.MercCount:
                return data.unlockedMercIds != null && data.unlockedMercIds.Count >= rule.value;
            case Kind.ChapterCleared:
                return data.clearedChapterIds != null && data.clearedChapterIds.Contains(rule.value);
            default:
                return true;
        }
    }

    /// <summary>未达成时显示的短文案；已达成或无门槛返回 null。</summary>
    public static string ConditionText(string hireId, SaveData data)
    {
        if (!Table.TryGetValue(hireId ?? "", out var rule)) return null;
        return IsOpen(hireId, data) ? null : rule.text;
    }

    /// <summary>给详情页用：把条件说完整（"解锁条件：通关第 3 章"）。</summary>
    public static string FullText(string hireId)
    {
        return Table.TryGetValue(hireId ?? "", out var rule) ? rule.text : "无解锁条件";
    }

    /// <summary>当前进度 / 目标，用于卡面进度提示（"3/5"）。没有门槛返回 null。</summary>
    public static string ProgressText(string hireId, SaveData data)
    {
        if (!Table.TryGetValue(hireId ?? "", out var rule)) return null;
        if (data == null) return null;

        switch (rule.kind)
        {
            case Kind.MercCount:
                int have = data.unlockedMercIds != null ? data.unlockedMercIds.Count : 0;
                return $"{have}/{rule.value}";
            case Kind.ChapterCleared:
                return data.clearedChapterIds != null && data.clearedChapterIds.Contains(rule.value)
                    ? "已通关"
                    : "未通关";
            default:
                return null;
        }
    }
}
