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
///   ① 每关给多少（按类型）→ STAGE_GOLD_NORMAL / STAGE_GOLD_ELITE / STAGE_GOLD_BOSS
///   ② 章间涨多快          → CHAPTER_GOLD_GROWTH（每章 +10% 复利，主人 2026-10-23 拍板）
///
/// ⚠ **与抽奖币（SlotMachineDefs）完全无关**，两套钱不许混（2026-10-05 主人最终口径）：
///   主人原话「**战斗内的都是局内的，每局携带的也都是局内的，
///             只有最后结算界面按通过的关卡数量给局外金币**」。
///   → 本文件管的就是那笔 **局外金币**：**唯一来源 = 结算界面按本局通过了几关发放**
///     （发放出口：`BattleManager.GrantPendingStageGold`，挂在 `PersistBattleGold` 里）。
///   → 战斗内发的（每关通关的局内金币 `SlotMachineSystem.GrantStageCoins`）是**局内**的，
///     归 `SlotMachineDefs`，不归这儿。【2026-10-10】载体已换成局内钱包 `BattleManager.currentGold`，
///     靠 `AddRunGold(n, false)` 保证它带不出去。
///   局外金币进商店 / 天赋 / 佣兵；局内金币只进战斗（抽奖）。
///
/// 【2026-10-23 批 E4 结构变更】「每章 10 关、章内线性递增」→「每大关 5 小关、按关卡类型分档」：
///   普通关 80 / 精英关 100 / Boss 关 200（第一章），第 2 章起每章 +10% 复利。
///   旧口径 FIRST_STAGE_WEIGHT / LAST_STAGE_WEIGHT / ChapterGoldTotals 退役无引用（保留不删）。
/// </summary>
public static class StageGoldDefs
{
    /// <summary>
    /// 【2026-10-23 主人拍板】每关通关的固定金币（局外）按**关卡类型**分档，第一章：
    /// 普通关 **80** / 精英关 **100** / Boss 关 **200**。
    /// 【2026-10-23 主人拍板】**第 2 章起每章涨 10%**（复利，见 <see cref="CHAPTER_GOLD_GROWTH"/>）：
    /// ch2 ×1.1、ch3 ×1.21 … ch8 ×1.9487。
    /// ⚠️ 与抽奖币（SlotMachineDefs）完全无关，两套钱不许混（2026-10-05 主人最终口径）。
    /// </summary>
    public const int STAGE_GOLD_NORMAL = 80;
    /// <summary>精英关每关固定金币（第一章）。</summary>
    public const int STAGE_GOLD_ELITE = 100;
    /// <summary>Boss 关（大关第 5 小关）每关固定金币（第一章）。</summary>
    public const int STAGE_GOLD_BOSS = 200;

    /// <summary>
    /// 章间每关固定金的增长率（复利）：第 N 章 = 第一章 × (1 + 此值)^(N-1)。
    /// 【2026-10-23 主人拍板】0.1 = 每章 +10%。想改增速只改这一个数。
    /// </summary>
    public const float CHAPTER_GOLD_GROWTH = 0.1f;

    /// <summary>
    /// 精英小关在大关内的 0-based 下标（= 第 3 小关）。
    /// 与 <see cref="StageRoller.RollByPosition"/>、冒险页银箱位同一口径（铁律 12/15：不写散魔数）。
    /// </summary>
    const int ELITE_STAGE_INDEX = 2;

    /// <summary>
    /// 每一章的**固定**奖励总额（金）。下标 0 = 第 1 章。
    /// 2026-09-29 主人拍板：每章给的奖励金额是固定的 —— 想改哪章就直接改这里的数，
    /// 不套公式、不受任何随机变量影响。
    ///
    /// <b>2026-10-05 主人拍板：第 1 章每关 100、第 2 章 150、第 3 章 200，每章固定 +50%。</b>
    /// 「固定 +50%」= 每章加的是**基数的 50%（= 50 关金）**，不是复利 ——
    ///   第 1 章 100 → 第 2 章 150 → 第 3 章 200 → …… → 第 8 章 450（等差 +50/关/章）。
    ///   （若按复利 50% 滚，第 3 章会是 225 而不是主人说的 200，所以这里是等差不是复利。）
    ///
    /// 【2026-10-23 批 E4 退役】每关金额改按关卡类型分档（普通 80 / 精英 100 / Boss 200）、
    /// 章间增速改走 <see cref="CHAPTER_GOLD_GROWTH"/>（每章 +10% 复利）后，本数组不再参与计算。
    /// 常量保留不删（下线≠删除），当前无引用；留作「想按章手定数额时」的参考旧值。
    /// </summary>
    public static readonly int[] ChapterGoldTotals =
        { 1000, 1500, 2000, 2500, 3000, 3500, 4000, 4500 };

    /// <summary>
    /// 第 1 关的权重（相对整章平均 1.0）。
    /// <b>2026-10-05 主人拍板：改成 1.0 = 每关固定 100（不再章内递增）</b> —— 原为 0.55。
    /// 主人原话「每关结束给的奖励先固定 100，一章 10 关就是 1000 金币」。
    /// 【2026-10-23 批 E4 退役】改按关卡类型分档后，章内位置权重不再参与计算；
    /// 常量保留不删（下线≠删除），当前无引用。
    /// </summary>
    public const float FIRST_STAGE_WEIGHT = 1.0f;

    /// <summary>
    /// 第 10 关的权重。<b>同改 1.0 = 每关固定</b>（原为 1.45）。
    /// 想恢复「越往后给得越多」就把这两个数拉开（和 &gt; 2 时整章会超出额度，注意合计）。
    /// 【2026-10-23 批 E4 退役】同 <see cref="FIRST_STAGE_WEIGHT"/>，当前无引用。
    /// </summary>
    public const float LAST_STAGE_WEIGHT = 1.0f;

    /// <summary>
    /// 怪物击杀掉金的全局倍率。**设 0 = 每关金币完全来自固定额度**（当前口径）。
    /// 想保留"打怪掉金币"的即时飘字反馈就设 1（旧行为），但那样每关收益又会浮动。
    /// </summary>
    public const float MONSTER_KILL_GOLD_MUL = 0f;

    /// <summary>某章的固定奖励总额（超出表长的章按最后一章的额度发）。
    /// 【2026-10-23 批 E4】语义改为「章间比例基准」，不再是整章可发总额。</summary>
    public static int ChapterGoldTotal(int chapter)
    {
        int ch = Mathf.Max(1, chapter);
        if (ch <= ChapterGoldTotals.Length) return ChapterGoldTotals[ch - 1];
        return ChapterGoldTotals[ChapterGoldTotals.Length - 1];
    }

    /// <summary>本关通关该发的固定金币（局外）。按关卡类型分档，章间每章 +10% 复利。</summary>
    /// <param name="chapter">第几章（1 起）</param>
    /// <param name="stageIndex0Based">大关内第几小关（0 起，0~4；第 2 = 精英、第 4 = Boss）</param>
    public static int StageGold(int chapter, int stageIndex0Based)
    {
        int ch = Mathf.Clamp(chapter, 1, 8);
        int idx = Mathf.Clamp(stageIndex0Based, 0, GameConfig.STAGES_PER_GRAND - 1);

        // 章间倍率：第 1 章 = 1.0，第 8 章 = 1.1^7 ≈ 1.9487
        float mul = Mathf.Pow(1f + CHAPTER_GOLD_GROWTH, ch - 1);

        int gold = idx == GameConfig.STAGES_PER_GRAND - 1 ? STAGE_GOLD_BOSS
                 : idx == ELITE_STAGE_INDEX ? STAGE_GOLD_ELITE
                 : STAGE_GOLD_NORMAL;
        return Mathf.Max(1, Mathf.RoundToInt(gold * mul));
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
        sb.AppendLine($"[关卡金币] 每关固定金（第1章）普通 {STAGE_GOLD_NORMAL} / 精英 {STAGE_GOLD_ELITE} / Boss {STAGE_GOLD_BOSS}"
                      + $"　章间增速 每章 +{CHAPTER_GOLD_GROWTH * 100f:0.#}%　掉金倍率 {MONSTER_KILL_GOLD_MUL}");
        for (int ch = 1; ch <= 8; ch++)
        {
            int sum = 0;
            sb.Append($"  第{ch}章：");
            // 【2026-10-23 批 E4】章内按大关 5 小关走（0~4）
            for (int s = 0; s < GameConfig.STAGES_PER_GRAND; s++)
            {
                int g = StageGold(ch, s);
                sum += g;
                sb.Append(g).Append(" ");
            }
            sb.AppendLine($"　合计 {sum}");
        }
        Debug.Log(sb.ToString());
    }
}
