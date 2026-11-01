using UnityEditor;
using UnityEngine;

/// <summary>
/// 章表自检（关卡结构重构验收用）：跑 <see cref="ActProgressionDefs.LogDiagnostics"/>，
/// 并自行对账每章关卡数 / 每关节数 / 节总数 + 关卡跨章连续编号，弹窗出结论。
/// 【2026-11-01 批 B3】章表改「每章 10 关卡 × 节 5/8/10」后同步重写。
/// 铁律 14：不对就 LogError 报出来，不静默。
/// </summary>
public static class ActProgressionSelfCheck
{
    [MenuItem("Tools/自检/篇表与关卡编号")]
    public static void Run()
    {
        ActProgressionDefs.LogDiagnostics();

        var sb = new System.Text.StringBuilder();
        var errs = new System.Text.StringBuilder();
        sb.AppendLine($"每章固定 {ActProgressionDefs.GRANDS_PER_ACT} 个关卡；每关最后一节 = Boss");
        sb.AppendLine();

        int totalStages = 0;
        for (int a = 1; a <= ActProgressionDefs.ActCount; a++)
        {
            int stages = ActProgressionDefs.StageCountOf(a);
            int subs = ActProgressionDefs.SubsPerGrandOf(a);
            int s = ActProgressionDefs.StartGrandOf(a);
            int e = ActProgressionDefs.EndGrandOf(a);

            sb.AppendLine($"第{a}章：10 关 × 每关{subs}节 = {stages} 节（关卡 {s}-{e}）");
            totalStages += stages;

            int expectSubs = a == 1 ? 5 : a == 2 ? 8 : 10;
            if (subs != expectSubs)
                errs.AppendLine($"第{a}章每关节数应为 {expectSubs}，实为 {subs}");
            if (s != (a - 1) * ActProgressionDefs.GRANDS_PER_ACT + 1)
                errs.AppendLine($"第{a}章起始关卡应为 {(a - 1) * ActProgressionDefs.GRANDS_PER_ACT + 1}，实为 {s}（跨章连续编号断了）");
            if (ActProgressionDefs.GrandCountOf(a) != ActProgressionDefs.GRANDS_PER_ACT)
                errs.AppendLine($"第{a}章关卡数应为 10，实为 {ActProgressionDefs.GrandCountOf(a)}");
        }

        sb.AppendLine();
        sb.AppendLine($"合计 {totalStages} 节 / {ActProgressionDefs.ActCount * ActProgressionDefs.GRANDS_PER_ACT} 关卡");

        // 跨章连续编号口径：第 2 章从 11 开始、第 3 章从 21 开始，表尾关卡属第 8 章
        if (ActProgressionDefs.ActOfGrand(11) != 2)
            errs.AppendLine($"关卡 11 应属第 2 章，实为第 {ActProgressionDefs.ActOfGrand(11)} 章");
        if (ActProgressionDefs.ActOfGrand(21) != 3)
            errs.AppendLine($"关卡 21 应属第 3 章，实为第 {ActProgressionDefs.ActOfGrand(21)} 章");
        int lastGrand = ActProgressionDefs.EndGrandOf(ActProgressionDefs.ActCount);
        if (ActProgressionDefs.ActOfGrand(lastGrand) != ActProgressionDefs.ActCount)
            errs.AppendLine($"关卡 {lastGrand}（表尾）应属第 {ActProgressionDefs.ActCount} 章，实为第 {ActProgressionDefs.ActOfGrand(lastGrand)} 章");

        // 节类型口径：每关末节 = Boss（第 1 章 5 节 → 第 5 节 Boss；第 3 章 10 节 → 第 10 节 Boss）
        if (ActProgressionDefs.TypeOf(1, 5) != StageType.Boss)
            errs.AppendLine("第 1 章第 5 节应为 Boss");
        if (ActProgressionDefs.TypeOf(1, 4) == StageType.Boss)
            errs.AppendLine("第 1 章第 4 节不应为 Boss");
        if (ActProgressionDefs.TypeOf(21, 10) != StageType.Boss)
            errs.AppendLine("第 3 章关卡的第 10 节应为 Boss");
        if (ActProgressionDefs.TypeOf(21, 9) == StageType.Boss)
            errs.AppendLine("第 3 章关卡的第 9 节不应为 Boss");

        // 编号显示口径
        string lbl1 = ActProgressionDefs.GlobalStageLabel(1, 0);
        if (lbl1 != "1-1") errs.AppendLine($"第 1 章第 1 节编号应为 1-1，实为 {lbl1}");
        string lbl2 = ActProgressionDefs.GlobalStageLabel(2, 0);
        if (lbl2 != "11-1") errs.AppendLine($"第 2 章第 1 节编号应为 11-1，实为 {lbl2}");
        string lbl3 = ActProgressionDefs.GlobalStageLabel(3, 0);
        if (lbl3 != "21-1") errs.AppendLine($"第 3 章第 1 节编号应为 21-1，实为 {lbl3}");
        string lbl4 = ActProgressionDefs.GlobalStageLabel(1, 49);
        if (lbl4 != "10-5") errs.AppendLine($"第 1 章第 50 节编号应为 10-5，实为 {lbl4}");

        bool ok = errs.Length == 0;
        if (ok)
            Debug.Log($"[ActProgressionSelfCheck] OK 共 {totalStages} 节\n" + sb);
        else
            Debug.LogError("[ActProgressionSelfCheck] FAIL\n" + errs);

        EditorUtility.DisplayDialog(
            ok ? "章表自检通过" : "章表自检失败",
            (ok ? "" : errs + "\n") + sb.ToString(),
            "好的");
    }
}
