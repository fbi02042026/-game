using System;
using UnityEngine;

/// <summary>
/// 【2026-11-01 主人拍板 方案A】关卡结构重构 · 进度数学层（批 B3）：
/// 章 → 关卡（每章固定 10 个）→ 节（第 1 章每关 5 节、第 2 章每关 8 节、第 3 章起每关 10 节）。
///
/// 口径（施工真源 <c>Docs/关卡结构重构_篇大关小关_待办任务清单.md</c> + 2026-11-01 主人对话拍板）：
/// · 每章 10 个关卡；关卡号**跨章连续编号**：第 1 章 1-1…10-5，第 2 章 11-1…20-8，第 3 章 21-1…30-10（31-10 后面是 32-1 ✓）；
/// · 第 1 章每关 5 节、第 2 章每关 8 节、第 3 章及以后每关 10 节；
/// · 每关最后一节 = Boss；第 3 节 = 精英（概率见 <see cref="StageRoller.PinnedEliteChance"/>，8/10 节章的精英位沿用第 3 节【待主人确认】）。
///
/// 🔴 本文件只做**数学**，不碰战斗、不碰存档、不改任何既有行为。
/// 特殊关（商人/附魔/诅咒/休息/锻造）不在本层决定 —— 仍由 ChapterManager 的分支机制处理（铁律 12：不许删）。
/// </summary>
public static class ActProgressionDefs
{
    /// <summary>每章关卡数（2026-11-01 主人拍板「所有章节都是 10 个关卡」）。</summary>
    public const int GRANDS_PER_ACT = 10;

    /// <summary>每关节数（<b>真源，唯一出处</b>）：第 1 章 5、第 2 章 8、第 3 章及以后 10（越界章按 10 续用）。</summary>
    static readonly int[] ActSubsPerGrand = { 5, 8, 10, 10, 10, 10, 10, 10 };

    /// <summary>某章每关节数。越界章（第 9 章及以后）按 10 续用。</summary>
    public static int SubsPerGrandOf(int act)
    {
        if (act < 1) return ActSubsPerGrand[0];
        return act <= ActSubsPerGrand.Length
            ? ActSubsPerGrand[act - 1]
            : ActSubsPerGrand[ActSubsPerGrand.Length - 1];
    }

    /// <summary>篇数（章号 1-based）。</summary>
    public static int ActCount => ActSubsPerGrand.Length;

    /// <summary>某章的节总数 = 关卡数 × 每关节数（50 / 80 / 100 / …）。</summary>
    public static int StageCountOf(int act)
    {
        if (act < 1) return 0;
        return GRANDS_PER_ACT * SubsPerGrandOf(act);
    }

    /// <summary>某章的关卡数（固定 <see cref="GRANDS_PER_ACT"/>）。</summary>
    public static int GrandCountOf(int act) => GRANDS_PER_ACT;

    /// <summary>某章第一个关卡的全局序号（1-based）。第 1 章 = 1，第 2 章 = 11，第 3 章 = 21。</summary>
    public static int StartGrandOf(int act)
        => (Mathf.Max(1, act) - 1) * GRANDS_PER_ACT + 1;

    /// <summary>某章最后一个关卡的全局序号。</summary>
    public static int EndGrandOf(int act) => StartGrandOf(act) + GRANDS_PER_ACT - 1;

    /// <summary>关卡全局序号 → 章号（1-based）。认不出（&lt;1）按第 1 章算；超出章表按最后一章算。</summary>
    public static int ActOfGrand(int grand)
    {
        if (grand < 1) return 1;
        int a = (grand - 1) / GRANDS_PER_ACT + 1;
        return Mathf.Min(a, ActCount);   // 超出章表：算在最后一章（不新增章，避免解锁链失控）
    }

    /// <summary>关卡全局序号 → 章内第几个关卡（1-based）。</summary>
    public static int GrandIndexInAct(int grand) => grand - StartGrandOf(ActOfGrand(grand)) + 1;

    /// <summary>
    /// (关卡全局序号, 节 1..N) → 章内全局节序号（1-based）。
    /// 经济产出 / 难度曲线用它代替旧的「章内第几关」。
    /// </summary>
    public static int GlobalStageInAct(int grand, int sub)
    {
        int act = ActOfGrand(grand);
        int gi = Mathf.Clamp(GrandIndexInAct(grand), 1, GrandCountOf(act));
        int s = Mathf.Clamp(sub, 1, SubsPerGrandOf(act));
        return (gi - 1) * SubsPerGrandOf(act) + s;
    }

    // ============================================================
    // 【2026-11-01 B3】章内「关卡 ↔ 节」换算（进度层唯一出口）
    // 运行时进度一律用「章内节下标 sub0（0 起，0..StageCountOf(章)-1）」当主键：
    //   存档 clearedStages 的 key = "章_sub0"（沿用旧格式；第 1 章 0..49 与老档完全一致）；
    //   关卡号 = sub0 / 每关节数 + 1，关内节 = sub0 % 每关节数。
    // 任何地方要在关卡号与节下标之间换算，只调这里的函数，禁止自己除/取模。
    // ============================================================

    /// <summary>某章的节总数（= <see cref="StageCountOf"/>，别名只为语义清楚）。</summary>
    public static int SubCountOf(int act) => StageCountOf(act);

    /// <summary>章内节下标（0 起）→ 章内第几关（1 起）。越界按首/尾关钳。</summary>
    public static int GrandOfSub0(int act, int sub0)
    {
        int subs = SubsPerGrandOf(act);
        int total = Mathf.Max(subs, StageCountOf(act));
        int s = Mathf.Clamp(sub0, 0, total - 1);
        return s / subs + 1;
    }

    /// <summary>章内节下标（0 起）→ 关内第几节（0 起，0..每关节数-1）。</summary>
    public static int SubInGrand0(int act, int sub0)
    {
        int subs = SubsPerGrandOf(act);
        return ((sub0 % subs) + subs) % subs;
    }

    /// <summary>（章, 关 1 起, 关内节 0 起）→ 章内节下标（0 起）。</summary>
    public static int Sub0Of(int act, int grand, int subInGrand0)
        => (Mathf.Max(1, grand) - 1) * SubsPerGrandOf(act) + Mathf.Clamp(subInGrand0, 0, SubsPerGrandOf(act) - 1);

    /// <summary>关卡显示编号「全局关卡号-节号」（如 1-1 / 10-5 / 11-8 / 21-10），2026-11-01 主人拍板。</summary>
    public static string GlobalStageLabel(int act, int sub0)
        => $"{StartGrandOf(act) + GrandOfSub0(act, sub0) - 1}-{SubInGrand0(act, sub0) + 1}";

    /// <summary>
    /// 节类型（只管 Boss / 精英 / 普通三种战斗型）：
    /// 每关最后一节 = Boss；第 3 节 = 精英（概率真源 <see cref="StageRoller.PinnedEliteChance"/>）；其余普通。
    /// （8/10 节章的精英位沿用第 3 节，2026-11-01 待主人确认。）
    /// </summary>
    public static StageType TypeOf(int grand, int sub)
    {
        int subs = SubsPerGrandOf(ActOfGrand(grand));
        int s = Mathf.Clamp(sub, 1, subs);
        if (s == subs) return StageType.Boss;
        if (s == 3) return UnityEngine.Random.value < StageRoller.PinnedEliteChance
            ? StageType.Elite : StageType.Normal;
        return StageType.Normal;
    }

    /// <summary>
    /// 章表自检（改完本文件任何数字都跑一次）：打印每章关卡数 / 每关节数 / 节总数 / 关卡全局区间，
    /// 并与 <see cref="GRANDS_PER_ACT"/> 对账。铁律 14 口径：不对就 LogError 报出来，不静默。
    /// </summary>
    public static void LogDiagnostics()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[章表自检] 每章 " + GRANDS_PER_ACT + " 个关卡；每关节数 = 5/8/10/10…");
        for (int a = 1; a <= ActCount; a++)
        {
            int stages = StageCountOf(a);
            int subs = SubsPerGrandOf(a);
            sb.AppendLine($"  第{a}章：10 关 × 每关{subs}节 = {stages} 节（关卡 {StartGrandOf(a)}–{EndGrandOf(a)}）");
            if (stages != GRANDS_PER_ACT * subs)
                Debug.LogError($"[章表自检] 第{a}章 节总数 {stages} ≠ 10×{subs}（表坏了）");
        }
        // 跨章连续编号对账
        if (StartGrandOf(2) != 11)
            Debug.LogError($"[章表自检] 第 2 章起始关卡应为 11，实为 {StartGrandOf(2)}（跨章连续编号口径坏了）");
        if (StartGrandOf(3) != 21)
            Debug.LogError($"[章表自检] 第 3 章起始关卡应为 21，实为 {StartGrandOf(3)}");
        if (ActOfGrand(11) != 2)
            Debug.LogError($"[章表自检] 关卡 11 应属第 2 章，实为第 {ActOfGrand(11)} 章");
        if (ActOfGrand(EndGrandOf(ActCount)) != ActCount)
            Debug.LogError($"[章表自检] 表尾关卡 {EndGrandOf(ActCount)} 应属第 {ActCount} 章");
        Debug.Log(sb.ToString());
    }
}
