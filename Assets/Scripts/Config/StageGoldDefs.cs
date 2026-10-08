using UnityEngine;

/// <summary>
/// 【关卡金币 · 唯一调参文件】2026-09-29 主人拍板重做金币发放方式。
///
/// 旧方式：金币 = 怪物掉金的累加 —— 每关拿多少取决于刷了几只怪，是浮动的。
/// 新方式：**每关通关拿固定的金币**，额度章内递增、章节越高越多。
///   打完第 1 章（10 关）= 1000 金；只打了几关就撤离 → 只拿已通关那几关的部分。
///   死亡同样保留已通关的部分（BattleManager 结算侧本来就是这个口径）。
///
/// ── 怎么调 ──
///   ① 每一章给多少        → ChapterGoldTotals（8 章的固定额度，直接改数）
///   ② 章内递增有多陡      → FIRST_STAGE_WEIGHT / LAST_STAGE_WEIGHT 的比值（当前都是 1.0 = 章内不递增）
///
/// ⚠ **与抽奖币（SlotMachineDefs）完全无关**，两套钱不许混（2026-10-05 主人最终口径）：
///   主人原话「**战斗内的都是局内的，每局携带的也都是局内的，
///             只有最后结算界面按通过的关卡数量给局外金币**」。
///   → 本文件管的就是那笔 **局外金币**：**唯一来源 = 结算界面按本局通过了几关发放**
///     （发放出口：`BattleManager.GrantPendingStageGold`，挂在 `PersistBattleGold` 里）。
///   → 战斗内发的（每关通关的抽奖币 `SlotMachineSystem.GrantStageCoins`）是**局内**的，
///     归 `SlotMachineDefs`，不归这儿。
///   金币进商店 / 天赋 / 佣兵，抽奖币只进抽奖。
/// </summary>
public static class StageGoldDefs
{
    /// <summary>
    /// 每一章的**固定**奖励总额（金）。下标 0 = 第 1 章。
    /// 2026-09-29 主人拍板：每章给的奖励金额是固定的 —— 想改哪章就直接改这里的数，
    /// 不套公式、不受任何随机变量影响。
    ///
    /// <b>2026-10-05 主人拍板：第 1 章每关 100、第 2 章 150、第 3 章 200，每章固定 +50%。</b>
    /// 「固定 +50%」= 每章加的是**基数的 50%（= 50 关金）**，不是复利 ——
    ///   第 1 章 100 → 第 2 章 150 → 第 3 章 200 → …… → 第 8 章 450（等差 +50/关/章）。
    ///   （若按复利 50% 滚，第 3 章会是 225 而不是主人说的 200，所以这里是等差不是复利。）
    /// 因为一章 10 关，数组里存的是**整章总额**：每关金额 × 10。
    /// 想让后期更慷慨就把后面几个数调大；想改「每章加多少」就整列重算这一个数组（唯一真源）。
    /// </summary>
    public static readonly int[] ChapterGoldTotals =
        { 1000, 1500, 2000, 2500, 3000, 3500, 4000, 4500 };

    /// <summary>
    /// 第 1 关的权重（相对整章平均 1.0）。
    /// <b>2026-10-05 主人拍板：改成 1.0 = 每关固定 100（不再章内递增）</b> —— 原为 0.55。
    /// 主人原话「每关结束给的奖励先固定 100，一章 10 关就是 1000 金币」。
    /// </summary>
    public const float FIRST_STAGE_WEIGHT = 1.0f;

    /// <summary>
    /// 第 10 关的权重。<b>同改 1.0 = 每关固定</b>（原为 1.45）。
    /// 想恢复「越往后给得越多」就把这两个数拉开（和 &gt; 2 时整章会超出额度，注意合计）。
    /// </summary>
    public const float LAST_STAGE_WEIGHT = 1.0f;

    /// <summary>
    /// 怪物击杀掉金的全局倍率。**设 0 = 每关金币完全来自固定额度**（当前口径）。
    /// 想保留"打怪掉金币"的即时飘字反馈就设 1（旧行为），但那样每关收益又会浮动。
    /// </summary>
    public const float MONSTER_KILL_GOLD_MUL = 0f;

    /// <summary>某章的固定奖励总额（超出表长的章按最后一章的额度发）。</summary>
    public static int ChapterGoldTotal(int chapter)
    {
        int ch = Mathf.Max(1, chapter);
        if (ch <= ChapterGoldTotals.Length) return ChapterGoldTotals[ch - 1];
        return ChapterGoldTotals[ChapterGoldTotals.Length - 1];
    }

    /// <summary>
    /// 本关通关该发的固定金币。
    /// 章内从 FIRST_STAGE_WEIGHT 线性递增到 LAST_STAGE_WEIGHT，
    /// 10 关权重之和 = 10，所以整章正好发满 ChapterGoldTotal。
    /// </summary>
    /// <param name="chapter">第几章（1 起）</param>
    /// <param name="stageIndex0Based">章内第几关（0 起，0~9）</param>
    public static int StageGold(int chapter, int stageIndex0Based)
    {
        int stages = Mathf.Max(2, GameConfig.STAGES_PER_CHAPTER);
        int idx = Mathf.Clamp(stageIndex0Based, 0, stages - 1);
        float t = stages > 1 ? idx / (float)(stages - 1) : 0f;
        float weight = Mathf.Lerp(FIRST_STAGE_WEIGHT, LAST_STAGE_WEIGHT, t);
        float avg = ChapterGoldTotal(chapter) / (float)stages;
        return Mathf.Max(1, Mathf.RoundToInt(avg * weight));
    }

    /// <summary>本关金币（按 BattleManager 当前进度取）。</summary>
    public static int CurrentStageGold()
    {
        int ch = ChapterManager.Instance != null ? ChapterManager.Instance.currentChapter : 1;
        int st = ChapterManager.Instance != null ? ChapterManager.Instance.currentStageIndex : 0;
        return StageGold(ch, st);
    }

    /// <summary>自检：打印各章各关的额度与整章合计。改完任何数值跑一次。</summary>
    public static void LogDiagnostics()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[关卡金币] 各章固定额度 {string.Join(" / ", ChapterGoldTotals)}");
        sb.AppendLine($"[关卡金币] 章内权重 {FIRST_STAGE_WEIGHT} → {LAST_STAGE_WEIGHT}　掉金倍率 {MONSTER_KILL_GOLD_MUL}");
        for (int ch = 1; ch <= 8; ch++)
        {
            int sum = 0;
            sb.Append($"  第{ch}章：");
            for (int s = 0; s < GameConfig.STAGES_PER_CHAPTER; s++)
            {
                int g = StageGold(ch, s);
                sum += g;
                sb.Append(g).Append(" ");
            }
            sb.AppendLine($"　合计 {sum}（额度 {ChapterGoldTotal(ch)}）");
        }
        Debug.Log(sb.ToString());
    }
}
