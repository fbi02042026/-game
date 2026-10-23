using UnityEditor;
using UnityEngine;

/// <summary>
/// 篇表自检（关卡结构重构 · 批 A 验收用）：跑 <see cref="ActProgressionDefs.LogDiagnostics"/>，
/// 并自行对账每篇关卡数 / 大关数 / 大关全局区间 + 跨篇连续编号，弹窗出结论。
/// 铁律 14：不对就 LogError 报出来，不静默。
/// </summary>
public static class ActProgressionSelfCheck
{
    private const int SUBS_PER_GRAND = ActProgressionDefs.SUBS_PER_GRAND;

    [MenuItem("Tools/自检/篇表与关卡编号")]
    public static void Run()
    {
        ActProgressionDefs.LogDiagnostics();

        var sb = new System.Text.StringBuilder();
        var errs = new System.Text.StringBuilder();
        sb.AppendLine($"大关固定 {SUBS_PER_GRAND} 小关（第5=Boss，第3=50%精英）");
        sb.AppendLine();

        int totalStages = 0, totalGrands = 0;
        int expectStart = 1;
        for (int a = 1; a <= ActProgressionDefs.ActCount; a++)
        {
            int stages = ActProgressionDefs.StageCountOf(a);
            int grands = ActProgressionDefs.GrandCountOf(a);
            int s = ActProgressionDefs.StartGrandOf(a);
            int e = ActProgressionDefs.EndGrandOf(a);

            sb.AppendLine($"第{a}篇：{stages} 关 = {grands} 大关（大关 {s}-{e}）");
            totalStages += stages;
            totalGrands += grands;

            if (stages % SUBS_PER_GRAND != 0)
                errs.AppendLine($"第{a}篇 {stages} 关不是 {SUBS_PER_GRAND} 的倍数（grands={grands} 会丢尾数）");
            if (s != expectStart)
                errs.AppendLine($"第{a}篇起始大关应为 {expectStart}，实为 {s}（跨篇连续编号断了）");
            expectStart = e + 1;
        }

        sb.AppendLine();
        sb.AppendLine($"合计 {totalStages} 关 / {totalGrands} 大关");

        // 跨篇连续编号口径：第二篇从 11 开始，大关 11 属第二篇
        if (ActProgressionDefs.ActOfGrand(11) != 2)
            errs.AppendLine($"大关 11 应属第二篇，实为第 {ActProgressionDefs.ActOfGrand(11)} 篇");
        if (ActProgressionDefs.ActOfGrand(126) != 8)
            errs.AppendLine($"大关 126 应属第八篇，实为第 {ActProgressionDefs.ActOfGrand(126)} 篇");

        // 小关类型口径
        if (ActProgressionDefs.TypeOf(1, 5) != StageType.Boss)
            errs.AppendLine("第 5 小关应为 Boss");
        if (ActProgressionDefs.TypeOf(1, 4) == StageType.Boss)
            errs.AppendLine("第 4 小关不应为 Boss");

        bool ok = errs.Length == 0;
        if (ok)
            Debug.Log($"[ActProgressionSelfCheck] OK 共 {totalStages} 关 / {totalGrands} 大关\n" + sb);
        else
            Debug.LogError("[ActProgressionSelfCheck] FAIL\n" + errs);

        EditorUtility.DisplayDialog(
            ok ? "篇表自检通过" : "篇表自检失败",
            (ok ? "" : errs + "\n") + sb.ToString(),
            "好的");
    }
}
