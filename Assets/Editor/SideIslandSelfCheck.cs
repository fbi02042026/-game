using UnityEditor;
using UnityEngine;

/// <summary>
/// 支线岛自检（2026-10-23 合并方案验收用）：
/// 跑 <see cref="SideIslandDefs.LogDiagnostics"/> + 对账岛表 / 主线线性化 / 岛存档镜像三件事，弹窗出结论。
/// 铁律 14：不对就 LogError 报出来，不静默。
/// </summary>
public static class SideIslandSelfCheck
{
    [MenuItem("Tools/自检/支线岛屿")]
    public static void Run()
    {
        SideIslandDefs.LogDiagnostics();

        var sb = new System.Text.StringBuilder();
        var errs = new System.Text.StringBuilder();

        // ── 1. 岛表对账 ──
        sb.AppendLine($"岛表 {SideIslandDefs.Count} 岛（岛槽 2 个依次解锁 · 单 Boss 关 · 一次性打完锁）");
        if (SideIslandDefs.Count != 8)
            errs.AppendLine($"岛数应为 8，实为 {SideIslandDefs.Count}");
        for (int id = 1; id <= SideIslandDefs.Count; id++)
        {
            var isl = SideIslandDefs.Get(id);
            if (isl == null) { errs.AppendLine($"岛 {id} 定义缺失"); continue; }
            if (string.IsNullOrEmpty(isl.name))
                errs.AppendLine($"岛 {id} 名字为空（命名不用数字，主人可重起但不可为空）");
            if (isl.goldReward <= 0 || isl.talentReward <= 0)
                errs.AppendLine($"岛 {id}「{isl.name}」奖励非法（gold={isl.goldReward} talent={isl.talentReward}）");
            if (isl.act < 1 || isl.act > 8)
                errs.AppendLine($"岛 {id} 背景篇 {isl.act} 越界（1..8）");
        }

        // ── 2. 主线线性化对账（合并方案：1→2→…→8，分叉退役）──
        for (int ch = 1; ch <= 7; ch++)
        {
            if (ChapterRouteTable.DefaultNext(ch) != ch + 1)
                errs.AppendLine($"主线应线性：第 {ch} 章的下一章应为 {ch + 1}，实为 {ChapterRouteTable.DefaultNext(ch)}");
        }
        if (ChapterRouteTable.DefaultNext(8) != -1)
            errs.AppendLine($"第 8 章应为终局（DefaultNext=-1），实为 {ChapterRouteTable.DefaultNext(8)}");

        var fake = new SaveData();
        if (!ChapterRouteTable.CanEnter(1, fake))
            errs.AppendLine("第 1 章应永远开放");
        if (ChapterRouteTable.CanEnter(2, fake))
            errs.AppendLine("未通关第 1 章时第 2 章不应开放（线性解锁坏了）");
        fake.MarkChapterCleared(1, 0);
        if (!ChapterRouteTable.CanEnter(2, fake))
            errs.AppendLine("通关第 1 章后第 2 章应开放");
        if (ChapterRouteTable.CanEnter(3, fake))
            errs.AppendLine("未通关第 2 章时第 3 章不应开放（线性解锁坏了）");
        fake.MarkChapterCleared(2, 0);
        if (!ChapterRouteTable.CanEnter(3, fake))
            errs.AppendLine("通关第 2 章后第 3 章应开放");

        // ── 3. 岛存档镜像对账（List + HashSet 双写）──
        var data = new SaveData();
        if (data.HasClearedSideIsland(3))
            errs.AppendLine("新存档不应已有岛 3 通关记录");
        data.MarkSideIslandCleared(3);
        if (!data.HasClearedSideIsland(3) || data.clearedSideIslandEntries.Count != 1)
            errs.AppendLine("MarkSideIslandCleared 后 HashSet/List 双写不一致");
        data.MarkSideIslandCleared(3);   // 幂等
        if (data.clearedSideIslandEntries.Count != 1)
            errs.AppendLine("MarkSideIslandCleared 不幂等（重复标记多了记录）");
        data.ResetSideIslandClears();
        if (data.HasClearedSideIsland(3) || data.clearedSideIslandEntries.Count != 0)
            errs.AppendLine("ResetSideIslandClears 后仍有残留记录");

        // 派定语义：branchRoll 记岛 id（1..8）；掷点出口只有 EnsureForkRoll（查询函数里严禁掷点，靠 review 保证）
        sb.AppendLine("派定真源：EnsureForkRoll（通关第 1 章派岛1 → 通关岛1 派岛2；8 岛全通洗牌袋重置）");

        bool ok = errs.Length == 0;
        if (ok)
            Debug.Log("[SideIslandSelfCheck] OK\n" + sb);
        else
            Debug.LogError("[SideIslandSelfCheck] FAIL\n" + errs);

        EditorUtility.DisplayDialog(
            ok ? "支线岛自检通过" : "支线岛自检失败",
            (ok ? "" : errs + "\n") + sb.ToString(),
            "好的");
    }
}
