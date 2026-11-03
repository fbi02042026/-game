using System.Collections.Generic;
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

        // 【2026-11-03 方案A】章内节台阶按节数摊满 10 档：首节 1.00、末节 Boss 1.50，中间不再吃尾档
        if (!Mathf.Approximately(GameConfig.GetStageStatScale(0, 5), 1.00f))
            errs.AppendLine($"5节章首节应为 1.00，实为 {GameConfig.GetStageStatScale(0, 5)}");
        if (!Mathf.Approximately(GameConfig.GetStageStatScale(4, 5), 1.50f))
            errs.AppendLine($"5节章末节应为 1.50，实为 {GameConfig.GetStageStatScale(4, 5)}");
        if (!Mathf.Approximately(GameConfig.GetStageStatScale(0, 8), 1.00f))
            errs.AppendLine($"8节章首节应为 1.00，实为 {GameConfig.GetStageStatScale(0, 8)}");
        if (!Mathf.Approximately(GameConfig.GetStageStatScale(5, 8), 1.28f))
            errs.AppendLine($"8节章第6节应为 1.28（修复前吃尾档 1.50），实为 {GameConfig.GetStageStatScale(5, 8)}");
        if (!Mathf.Approximately(GameConfig.GetStageStatScale(7, 8), 1.50f))
            errs.AppendLine($"8节章末节应为 1.50，实为 {GameConfig.GetStageStatScale(7, 8)}");
        if (!Mathf.Approximately(GameConfig.GetStageStatScale(4, 10), 1.18f))
            errs.AppendLine($"10节章第5节应为 1.18（卡点1），实为 {GameConfig.GetStageStatScale(4, 10)}");
        if (!Mathf.Approximately(GameConfig.GetStageStatScale(8, 10), 1.41f))
            errs.AppendLine($"10节章第9节应为 1.41（卡点2），实为 {GameConfig.GetStageStatScale(8, 10)}");
        if (!Mathf.Approximately(GameConfig.GetStageStatScale(9, 10), 1.50f))
            errs.AppendLine($"10节章末节应为 1.50，实为 {GameConfig.GetStageStatScale(9, 10)}");

        // 【2026-11-03 主人拍板】每章 2 个卡点关：半程(第5座)末节Boss + 章末(第10座)末节Boss 各 +10%
        if (!Mathf.Approximately(GameConfig.GetGrandWallBonus(1, 5, 4), 1.10f))
            errs.AppendLine($"第1章半程卡点关(5-5)应为 ×1.10，实为 {GameConfig.GetGrandWallBonus(1, 5, 4)}");
        if (!Mathf.Approximately(GameConfig.GetGrandWallBonus(1, 10, 4), 1.10f))
            errs.AppendLine($"第1章章末卡点关(10-5)应为 ×1.10，实为 {GameConfig.GetGrandWallBonus(1, 10, 4)}");
        if (!Mathf.Approximately(GameConfig.GetGrandWallBonus(1, 4, 4), 1.00f))
            errs.AppendLine("非卡点大关 Boss 不应吃卡点加成");
        if (!Mathf.Approximately(GameConfig.GetGrandWallBonus(1, 5, 3), 1.00f))
            errs.AppendLine("卡点大关的非末节不应吃卡点加成");
        if (!Mathf.Approximately(GameConfig.GetGrandWallBonus(2, 5, 7), 1.10f))
            errs.AppendLine($"第2章半程卡点关(15-8)应为 ×1.10，实为 {GameConfig.GetGrandWallBonus(2, 5, 7)}");
        if (!Mathf.Approximately(GameConfig.GetGrandWallBonus(3, 10, 9), 1.10f))
            errs.AppendLine($"第3章章末卡点关(30-10)应为 ×1.10，实为 {GameConfig.GetGrandWallBonus(3, 10, 9)}");

        // 【2026-11-03 主人拍板「第七第八也要加难度」】章倍率上调断言
        if (!Mathf.Approximately(GameConfig.GetChapterStatScale(7), 1.86f))
            errs.AppendLine($"第7章章倍率应为 1.86，实为 {GameConfig.GetChapterStatScale(7)}（chapter_stat_scale.csv 与 Fallback 不同步？）");
        if (!Mathf.Approximately(GameConfig.GetChapterStatScale(8), 2.55f))
            errs.AppendLine($"第8章章倍率应为 2.55，实为 {GameConfig.GetChapterStatScale(8)}（chapter_stat_scale.csv 与 Fallback 不同步？）");

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

/// <summary>
/// 大关进度迁移自检（2026-11-03 修「3-4 打完进 3-5 变 1-1」验收用）：
/// 构造四类存档走 <see cref="SaveData.SyncRuntimeFromLists"/>，验证 grandProgress 钳制与 CurrentGrand：
/// · 坏档（打过大关 Boss 被误标整章通关，key 只到 1_13）→ 修回 2，CurrentGrand=3（玩家从 3-5 续打）；
/// · 正常新档（只通第 1 大关）→ 不受钳制影响，CurrentGrand=2；
/// · 整章真通关（50 key 全齐）→ 记满 10，CurrentGrand=1（重打口径）；
/// · 旧 10 关制老档（key 0~9）→ 推导 2 座大关，CurrentGrand=3（2026-11-03 主人确认口径）。
/// </summary>
public static class GrandProgressMigrationSelfCheck
{
    [MenuItem("Tools/自检/大关进度迁移修复")]
    public static void Run()
    {
        var errs = new System.Text.StringBuilder();

        // ① 坏档：打过大关 Boss（chapterClearCounts≥1 / entries 误标）+ key 只到 1_13 + grandProgress 被顶成 10
        var bad = new SaveData();
        for (int s0 = 0; s0 <= 13; s0++) bad.clearedStageEntries.Add(new StringIdEntry { id = "1_" + s0 });
        bad.clearedChapterEntries.Add(new IntIdEntry { id = 1 });   // 模拟误标后的整章通关标记
        bad.grandProgress = new List<int> { 10, 0, 0, 0, 0, 0, 0, 0 };
        bad.SyncRuntimeFromLists();
        if (bad.GrandClearedCount(1) != 2)
            errs.AppendLine($"坏档应修回 2 座大关，实为 {bad.GrandClearedCount(1)}");
        if (bad.CurrentGrand(1) != 3)
            errs.AppendLine($"坏档 CurrentGrand 应为 3（从 3-1 起打、页面/进本同口径落 3-5），实为 {bad.CurrentGrand(1)}");

        // ② 正常新档：只通第 1 大关（1_0..1_4），grandProgress=1
        var normal = new SaveData();
        for (int s0 = 0; s0 <= 4; s0++) normal.clearedStageEntries.Add(new StringIdEntry { id = "1_" + s0 });
        normal.chapterClearCounts.Add(new ChapterClearCountEntry { chapter = 1, clearCount = 1 });
        normal.grandProgress = new List<int> { 1, 0, 0, 0, 0, 0, 0, 0 };
        normal.SyncRuntimeFromLists();
        if (normal.GrandClearedCount(1) != 1)
            errs.AppendLine($"正常档大关数应为 1，实为 {normal.GrandClearedCount(1)}（钳制误伤了合法进度）");
        if (normal.CurrentGrand(1) != 2)
            errs.AppendLine($"正常档 CurrentGrand 应为 2，实为 {normal.CurrentGrand(1)}");

        // ③ 整章真通关：50 key 全齐 → 记满 10，CurrentGrand 回 1（整章重打口径）
        var full = new SaveData();
        for (int s0 = 0; s0 < ActProgressionDefs.StageCountOf(1); s0++)
            full.clearedStageEntries.Add(new StringIdEntry { id = "1_" + s0 });
        full.grandProgress = new List<int> { 10, 0, 0, 0, 0, 0, 0, 0 };
        full.SyncRuntimeFromLists();
        if (full.GrandClearedCount(1) != ActProgressionDefs.GRANDS_PER_ACT)
            errs.AppendLine($"整章通关应记满 {ActProgressionDefs.GRANDS_PER_ACT}，实为 {full.GrandClearedCount(1)}");
        if (full.CurrentGrand(1) != 1)
            errs.AppendLine($"整章通关重打 CurrentGrand 应为 1，实为 {full.CurrentGrand(1)}");

        // ④ 旧 10 关制老档：key 0~9、无 grandProgress → 推导 2 座大关，从 3-1 续打（主人 2026-11-03 确认）
        var legacy = new SaveData();
        for (int s0 = 0; s0 <= 9; s0++) legacy.clearedStageEntries.Add(new StringIdEntry { id = "1_" + s0 });
        legacy.SyncRuntimeFromLists();
        if (legacy.GrandClearedCount(1) != 2)
            errs.AppendLine($"老档应推导 2 座大关，实为 {legacy.GrandClearedCount(1)}");
        if (legacy.CurrentGrand(1) != 3)
            errs.AppendLine($"老档 CurrentGrand 应为 3，实为 {legacy.CurrentGrand(1)}");

        bool ok = errs.Length == 0;
        if (ok) Debug.Log("[GrandProgressMigrationSelfCheck] OK 四类档全部通过");
        else Debug.LogError("[GrandProgressMigrationSelfCheck] FAIL\n" + errs);

        EditorUtility.DisplayDialog(
            ok ? "大关迁移自检通过" : "大关迁移自检失败",
            ok ? "坏档修复 / 正常档不误伤 / 整章通关 / 老档推导 四类全部通过"
               : errs.ToString(),
            "好的");
    }
}
