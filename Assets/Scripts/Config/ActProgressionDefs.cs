using System;
using UnityEngine;

/// <summary>
/// 【2026-10-22 主人拍板】关卡结构重构 · 进度数学层：篇 → 大关（固定 5 小关）→ 小关。
///
/// 口径（施工真源 <c>Docs/关卡结构重构_篇大关小关_待办任务清单.md</c>）：
/// · 篇的关卡数由主人直接定表（2026-10-23）：50,50,70,80,90,90,100,100；第 9 篇及以后越界按 ×1.2 取 5 的倍数续算；
/// · 大关**跨篇连续编号**：第一篇 1-1…10-5，第二篇 11-1…20-5（没有「每篇重新从 1 开始」的口径）；
/// · 每大关 5 小关：第 5 小关 = Boss，第 3 小关 = 精英（概率见 <see cref="StageRoller.PinnedEliteChance"/>）。
///
/// 🔴 本文件只做**数学**，不碰战斗、不碰存档、不改任何既有行为（批 A 只新增）。
/// 特殊关（商人/附魔/诅咒/休息/锻造）不在本层决定 —— 仍由 ChapterManager 的分支机制处理（铁律 12：不许删）。
/// </summary>
public static class ActProgressionDefs
{
    /// <summary>大关固定 5 小关（1-1 … 1-5）。</summary>
    public const int SUBS_PER_GRAND = 5;

    /// <summary>
    /// 篇的关卡数（<b>真源，唯一出处</b>）。全 8 篇由主人定表（2026-10-23），均为 5 的倍数。
    /// 大关数 = 本篇关卡数 / <see cref="SUBS_PER_GRAND"/>。
    /// </summary>
    static readonly int[] ActStageCounts = { 50, 50, 70, 80, 90, 90, 100, 100 };

    /// <summary>取 5 的倍数（四舍五入）：96→95、114→115、138→140、168→170、204→205。
    /// ⚠️ 只用于<b>第 9 篇及以后</b>的越界续算；表内 8 篇的关卡数是主人手定的，不是这个公式算出来的。</summary>
    static int SnapToSubMultiple(float raw)
        => Mathf.RoundToInt(raw / (float)SUBS_PER_GRAND) * SUBS_PER_GRAND;

    /// <summary>篇数（篇号 1-based）。</summary>
    public static int ActCount => ActStageCounts.Length;

    /// <summary>
    /// 某篇的关卡数。表内直接给；越界（第 9 篇及以后）按 ×1.2 取 5 的倍数续算 ——
    /// 不抛错也不静默给 0：新篇是设计内扩展，算得出就继续算。
    /// </summary>
    public static int StageCountOf(int act)
    {
        if (act < 1) return 0;
        if (act <= ActStageCounts.Length) return ActStageCounts[act - 1];

        int last = ActStageCounts[ActStageCounts.Length - 1];
        for (int a = ActStageCounts.Length; a < act; a++)
            last = SnapToSubMultiple(last * 1.2f);
        return last;
    }

    /// <summary>某篇的大关数（= 关卡数 / 5，表内均为 5 的倍数故整除）。</summary>
    public static int GrandCountOf(int act) => StageCountOf(act) / SUBS_PER_GRAND;

    /// <summary>某篇第一个大关的全局序号（1-based）。第一篇 = 1。</summary>
    public static int StartGrandOf(int act)
    {
        if (act < 1) return 1;
        int g = 1;
        for (int a = 1; a < act; a++)
            g += GrandCountOf(a);
        return g;
    }

    /// <summary>某篇最后一个大关的全局序号。</summary>
    public static int EndGrandOf(int act) => StartGrandOf(act) + GrandCountOf(act) - 1;

    /// <summary>大关全局序号 → 篇号（1-based）。认不出（&lt;1）按第一篇算。</summary>
    public static int ActOfGrand(int grand)
    {
        if (grand < 1) return 1;
        for (int a = 1; a <= ActCount; a++)
            if (grand <= EndGrandOf(a)) return a;
        return ActCount;   // 超出篇表：算在最后一篇（不新增篇，避免解锁链失控）
    }

    /// <summary>大关全局序号 → 篇内第几个大关（1-based）。</summary>
    public static int GrandIndexInAct(int grand) => grand - StartGrandOf(ActOfGrand(grand)) + 1;

    /// <summary>
    /// (大关全局序号, 小关 1..5) → 篇内全局小关序号（1-based）。
    /// 经济产出 / 难度曲线用它代替旧的「章内第几关」。
    /// </summary>
    public static int GlobalStageInAct(int grand, int sub)
    {
        int gi = Mathf.Clamp(GrandIndexInAct(grand), 1, GrandCountOf(ActOfGrand(grand)));
        int s = Mathf.Clamp(sub, 1, SUBS_PER_GRAND);
        return (gi - 1) * SUBS_PER_GRAND + s;
    }

    // ============================================================
    // 【2026-10-31 B2 落地】篇内「大关 ↔ 小关」换算（进度层唯一出口）
    // 运行时进度一律用「篇内小关下标 sub0（0 起，0..StageCountOf(篇)-1）」当主键：
    //   存档 clearedStages 的 key = "章_sub0"（沿用旧格式，老档 0..4 正好是第一座大关）；
    //   大关号 = sub0 / 5 + 1，大关内小关 = sub0 % 5。
    // 任何地方要在大关号与小关下标之间换算，只调这四个函数，禁止自己除 5 取模。
    // ============================================================

    /// <summary>某篇的小关总数（= <see cref="StageCountOf"/>，别名只为语义清楚）。</summary>
    public static int SubCountOf(int act) => StageCountOf(act);

    /// <summary>篇内小关下标（0 起）→ 篇内第几座大关（1 起）。越界按首/尾大关钳。</summary>
    public static int GrandOfSub0(int act, int sub0)
    {
        int total = Mathf.Max(SUBS_PER_GRAND, StageCountOf(act));
        int s = Mathf.Clamp(sub0, 0, total - 1);
        return s / SUBS_PER_GRAND + 1;
    }

    /// <summary>篇内小关下标（0 起）→ 大关内第几小关（0 起，0~4）。</summary>
    public static int SubInGrand0(int sub0)
        => ((sub0 % SUBS_PER_GRAND) + SUBS_PER_GRAND) % SUBS_PER_GRAND;

    /// <summary>（大关, 大关内小关 0 起）→ 篇内小关下标（0 起）。</summary>
    public static int Sub0Of(int grand, int sub0)
        => (Mathf.Max(1, grand) - 1) * SUBS_PER_GRAND + Mathf.Clamp(sub0, 0, SUBS_PER_GRAND - 1);

    /// <summary>
    /// 小关类型（只管 Boss / 精英 / 普通三种战斗型）：
    /// 第 5 小关 = Boss；第 3 小关 = 精英（概率真源 <see cref="StageRoller.PinnedEliteChance"/>）；其余普通。
    /// </summary>
    public static StageType TypeOf(int grand, int sub)
    {
        int s = Mathf.Clamp(sub, 1, SUBS_PER_GRAND);
        if (s == SUBS_PER_GRAND) return StageType.Boss;
        if (s == 3) return UnityEngine.Random.value < StageRoller.PinnedEliteChance
            ? StageType.Elite : StageType.Normal;
        return StageType.Normal;
    }

    /// <summary>
    /// 篇表自检（改完本文件任何数字都跑一次）：打印每篇关卡数 / 大关数 / 大关区间，
    /// 并与 <see cref="SUBS_PER_GRAND"/> 整除关系对账。铁律 14 口径：不对就 LogError 报出来，不静默。
    /// </summary>
    public static void LogDiagnostics()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[篇表自检] 大关固定 " + SUBS_PER_GRAND + " 小关");
        for (int a = 1; a <= ActCount; a++)
        {
            int stages = StageCountOf(a);
            int grands = GrandCountOf(a);
            sb.AppendLine($"  第{a}篇：{stages}关 = {grands}大关（大关 {StartGrandOf(a)}–{EndGrandOf(a)}）");
            if (stages % SUBS_PER_GRAND != 0)
                Debug.LogError($"[篇表自检] 第{a}篇 {stages} 关不是 {SUBS_PER_GRAND} 的倍数 → 大关数会丢尾数（grands={grands}）");
        }
        // 跨篇连续编号对账
        if (StartGrandOf(2) != 11)
            Debug.LogError($"[篇表自检] 第二篇起始大关应为 11，实为 {StartGrandOf(2)}（跨篇连续编号口径坏了）");
        if (ActOfGrand(11) != 2)
            Debug.LogError($"[篇表自检] 大关 11 应属第二篇，实为第 {ActOfGrand(11)} 篇");
        Debug.Log(sb.ToString());
    }
}
