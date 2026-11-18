using System;
using UnityEngine;

/// <summary>
/// 【2026-11-01 主人拍板 路线 B】关卡结构塌缩 · 进度数学层（批 B3）：
/// 8 章 → 每章 1 座大关（GRANDS_PER_ACT=1、运行时 grand 恒 1）→ 每大关 10 节（=10 关），全局 80 关，关=节。
///
/// 口径（施工真源 <c>Docs/关卡结构塌缩_80关一关一战斗_待办任务清单.md</c>）：
/// · 关号（1-based）即章内节号 1..10，不跨章连续编号；
/// · 关 5 = 小 boss（10 号 xiaoboss）、关 10 = 大 boss（11/12 真 Boss）、关 3/8 = 精英（概率真源 <see cref="StageRoller.PinnedEliteChance"/>）、其余普通；
/// · 关型真源 = <see cref="TypeOfStage"/> / <see cref="BossKindOfStage"/> 单一出口。
///
/// 🔴 本文件只做**数学**，不碰战斗、不碰存档、不改任何既有行为。
/// 特殊关（商人/附魔/诅咒/休息/锻造）不在本层决定 —— 仍由 ChapterManager 的分支机制处理（铁律 12：不许删）。
/// </summary>
public static class ActProgressionDefs
{
    /// <summary>每章大关数（路线 B：每章 1 座大关 = 10 关，运行时 grand 恒 1）。</summary>
    public const int GRANDS_PER_ACT = 1;

    /// <summary>每大关节数（<b>真源，唯一出处</b>）：路线 B 下每章 1 座大关、统一 10 节（关），越界章按 10 续用。</summary>
    static readonly int[] ActSubsPerGrand = { 10, 10, 10, 10, 10, 10, 10, 10 };

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
    /// 节类型（只管 Boss / 精英 / 普通三种战斗型）——【路线 B】已转发 <see cref="TypeOfStage"/>：
    /// sub 为 1-based 关号（1..10），实际关型由 TypeOfStage(sub-1) 定；保留签名兼容 Editor 自检等调用点。
    /// </summary>
    public static StageType TypeOf(int grand, int sub)
    {
        return TypeOfStage(sub - 1);
    }

    /// <summary>关型单一出口：关=节，stageIdx0（0-based，0..9）。关5(4)小boss/关10(9)大boss/关3(2)·关8(7)精英/其余普通。</summary>
    public static StageType TypeOfStage(int stageIdx0)
    {
        if (stageIdx0 == 9 || stageIdx0 == 4) return StageType.Boss;
        if (stageIdx0 == 2 || stageIdx0 == 7)
            return UnityEngine.Random.value < StageRoller.PinnedEliteChance ? StageType.Elite : StageType.Normal;
        return StageType.Normal;
    }

    /// <summary>boss 种类单一出口：0=无 1=小boss(10号xiaoboss) 2=大boss(11/12真Boss)。</summary>
    public static int BossKindOfStage(int stageIdx0)
        => stageIdx0 == 9 ? 2 : (stageIdx0 == 4 ? 1 : 0);

    /// <summary>
    /// 章表自检（改完本文件任何数字都跑一次）：打印每章关卡数 / 每关节数 / 节总数 / 关卡全局区间，
    /// 并与 <see cref="GRANDS_PER_ACT"/> 对账。铁律 14 口径：不对就 LogError 报出来，不静默。
    /// </summary>
    public static void LogDiagnostics()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[章表自检] 每章 " + GRANDS_PER_ACT + " 大关 × 10 关；每关节数 = 10");
        for (int a = 1; a <= ActCount; a++)
        {
            int stages = StageCountOf(a);
            int subs = SubsPerGrandOf(a);
            sb.AppendLine($"  第{a}章：{GRANDS_PER_ACT} 大关 × {subs} 关 = {stages} 关（关卡 {StartGrandOf(a)}–{EndGrandOf(a)}）");
            if (stages != GRANDS_PER_ACT * subs)
                Debug.LogError($"[章表自检] 第{a}章 节总数 {stages} ≠ 10×{subs}（表坏了）");
        }
        // 路线 B：关卡号即章号（关卡号 1..ActCount 直连章号），跨章切换自检
        if (ActOfGrand(1) != 1)
            Debug.LogError($"[章表自检] 路线 B：关卡号即章号，关卡 1 应属第 1 章，实为第 {ActOfGrand(1)} 章");
        if (ActOfGrand(8) != 8)
            Debug.LogError($"[章表自检] 路线 B：关卡号即章号，关卡 8 应属第 8 章，实为第 {ActOfGrand(8)} 章");
        if (ActOfGrand(EndGrandOf(ActCount)) != ActCount)
            Debug.LogError($"[章表自检] 表尾关卡 {EndGrandOf(ActCount)} 应属第 {ActCount} 章");
        Debug.Log(sb.ToString());
    }
}
