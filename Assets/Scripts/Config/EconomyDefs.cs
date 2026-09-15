using UnityEngine;

/// <summary>
/// 经济与进度节奏的**权威参考表**（2026-09-15 新增）。
///
/// 这一份存在的理由：商店定价、碎片产出、登录奖励全都要拿「一局到底能挣多少」当尺子，
/// 而这个数字之前散落在 monster_stats.csv / battle_quest.csv / GameConfig 三处，谁也说不清。
/// 现在集中到这里，改经济只需要动这一张表 + 对得上号的源头。
///
/// ⚠️ 本表**不直接发钱**，只做估算与自检——避免没实测就把产出改崩。
/// 真实产出仍在：怪 goldDrop（Monster.cs）、通关金（BattleQuestConfig）、
/// 天赋石（GOLD_PER_TALENT_POINT=100 换算）。要改产出请改源头，然后回来同步本表。
/// </summary>
public static class EconomyDefs
{
    // ============================================================
    // 一、一局净收入估算
    // ============================================================

    /// <summary>每章 10 关，其中约 2 关是特殊关。</summary>
    public const int STAGES_PER_RUN = GameConfig.STAGES_PER_CHAPTER;

    /// <summary>每关平均击杀数（含波次，按第一章首关 9 只、后面递增估的）。</summary>
    public const int KILLS_PER_STAGE = 18;

    /// <summary>每章怪物的平均基础金币（读 monster_stats.csv 的 baseGold 列估的）。</summary>
    public static int AvgMonsterGold(int chapter)
    {
        switch (Mathf.Clamp(chapter, 1, 8))
        {
            case 1: return 18;
            case 2: return 28;
            case 3: return 40;
            case 4: return 55;
            case 5: return 72;
            case 6: return 92;
            case 7: return 115;
            default: return 140;
        }
    }

    /// <summary>波次加成：goldDrop = base × (1 + wave × 0.1)，平均取 1.25。</summary>
    public const float WAVE_GOLD_MUL = 1.25f;

    /// <summary>一局的怪物金币收入。</summary>
    public static int MonsterGoldPerRun(int chapter) =>
        Mathf.RoundToInt(AvgMonsterGold(chapter) * WAVE_GOLD_MUL * KILLS_PER_STAGE * STAGES_PER_RUN);

    /// <summary>一局的通关金收入（9 普通关 + 1 Boss）。</summary>
    public static int ClearGoldPerRun(int chapter, int difficulty)
    {
        int ch = Mathf.Clamp(chapter, 1, 8);
        int normal = 25 + ch * 10;                       // battle_quest.csv 普通关公式
        int boss = BossClearGold(ch);
        float mul = GameConfig.GetDifficultyGoldMul(difficulty);
        return Mathf.RoundToInt((normal * (STAGES_PER_RUN - 1) + boss) * mul);
    }

    static int BossClearGold(int ch)
    {
        switch (ch)
        {
            case 1: return 200;
            case 2: return 300;
            case 3: return 400;
            case 4: return 500;
            case 5: return 600;
            case 6: return 700;
            case 7: return 800;
            default: return 2000;
        }
    }

    /// <summary>一局总金币（怪物 + 通关金，不含装备分解）。</summary>
    public static int RunGold(int chapter, int difficulty = 0) =>
        MonsterGoldPerRun(chapter) + ClearGoldPerRun(chapter, difficulty);

    /// <summary>一局折算的天赋石（GOLD_PER_TALENT_POINT = 100）。</summary>
    public static int RunTalentPoints(int chapter, int difficulty = 0) =>
        RunGold(chapter, difficulty) / GameConfig.GOLD_PER_TALENT_POINT;

    // ============================================================
    // 二、节奏曲线：前期不卡 → 中期卡构筑 → 后期卡星级
    // ============================================================

    public enum GateKind
    {
        None,      // 不设卡，纯爽
        Build,     // 卡构筑：需要特定技能 / 佣兵 / 装备才过得去
        Stat       // 卡数值：需要星级 / 强化等级
    }

    public struct StageGate
    {
        public int chapter;
        public GateKind kind;
        /// <summary>建议通关所需局数（含失败重试）。</summary>
        public int expectedRuns;
        /// <summary>卡点描述。</summary>
        public string note;
    }

    /// <summary>
    /// 八章的卡点设计。**前期（1~2）刻意不卡**——新玩家前 30 分钟不该被拦；
    /// 中期（3~5）开始用「构筑」卡：你能过不是因为你等级高，而是因为你配对了东西；
    /// 后期（6~8）才轮到数值（星级 / 强化）接管。
    /// </summary>
    public static readonly StageGate[] Gates =
    {
        new StageGate { chapter = 1, kind = GateKind.None,  expectedRuns = 1, note = "教学章，必然通过；通关奖励够买 1 个普通技能" },
        new StageGate { chapter = 2, kind = GateKind.None,  expectedRuns = 1, note = "仍不卡，让玩家尝到构筑成长的甜头" },
        new StageGate { chapter = 3, kind = GateKind.Build, expectedRuns = 2, note = "首个卡点：怪开始成群，没 AOE 或群体控制会打得很难受" },
        new StageGate { chapter = 4, kind = GateKind.Build, expectedRuns = 3, note = "主卡点：生存压力陡增，缺治疗/护盾/佣兵会反复失败" },
        new StageGate { chapter = 5, kind = GateKind.Build, expectedRuns = 3, note = "Boss 有硬机制，需要对的技能组合而不是更高数值" },
        new StageGate { chapter = 6, kind = GateKind.Stat,  expectedRuns = 4, note = "数值墙开始：需要装备强化等级" },
        new StageGate { chapter = 7, kind = GateKind.Stat,  expectedRuns = 5, note = "星级墙：技能星级成为主要差距" },
        new StageGate { chapter = 8, kind = GateKind.Stat,  expectedRuns = 6, note = "终章：金币不再是瓶颈，瓶颈是星级与词条" },
    };

    public static StageGate GateFor(int chapter) =>
        Gates[Mathf.Clamp(chapter, 1, Gates.Length) - 1];

    // ============================================================
    // 三、定价自检
    // ============================================================

    /// <summary>某个价格相当于第 1 章几局的收入。用来判断商店定价是否合理。</summary>
    public static string DescribePrice(long price)
    {
        int run = RunGold(1);
        if (run <= 0) return price.ToString();
        float n = price / (float)run;
        return $"{price}（≈{n:0.0} 局 / 第1章）";
    }

    /// <summary>打日志自检：把各章一局收入与主要定价打出来，方便实测后对照调整。</summary>
    public static void LogDiagnostics()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[Economy] 一局净收入估算（难度 0）");
        for (int ch = 1; ch <= 8; ch++)
            sb.AppendLine($"  第{ch}章：金币 {RunGold(ch)}　≈天赋石 {RunTalentPoints(ch)}");

        sb.AppendLine("[Economy] 商店定价自检");
        for (int i = 0; i < ShopDefs.All.Length; i++)
        {
            var it = ShopDefs.All[i];
            if (it.currency != ResourceWallet.ResourceType.Gold) continue;
            float n = it.price / (float)RunGold(1);
            sb.AppendLine($"  {it.name}：{DescribePrice(it.price)}　限购 {it.dailyLimit}");
            if (n > 6f) sb.AppendLine($"    ⚠ 超过 6 局收入，可能永远没人买");
        }
        Debug.Log(sb.ToString());
    }
}
