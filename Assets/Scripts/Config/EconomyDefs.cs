using UnityEngine;

/// <summary>
/// 经济与进度节奏的**权威参考表**（2026-09-15 第二版：产出去零 + 按「尝试次数」建模）。
///
/// 第一版犯的错：把「一局」当成「通关一章」，直接按 10 关满关算收入。
/// 实际上玩家**不会第一次就打完**——一局（一次冒险）是从第 1 关一路打到死/撤离/通关，
/// 失败就从头再来。策划给的锚点是：**10 次尝试内通关第 1 章，累计 30 次内打完第 2 章**。
/// 所以收入必须按「失败局 + 最后一次通关局」建模，否则会把玩家购买力高估 4 倍以上。
///
/// 同时执行「产出去零」：怪物 baseGold 与关卡 clearGold 全部 ÷10。
/// 原因：去零前通关第 1 章就能攒 ≈2.2 万金，而商店全部技能加起来才 ≈2.4 万——
/// **第 2 章没打完商店就空了**。
///
/// ⚠️ 本表**不直接发钱**，只做估算与自检。真实产出仍在 monster_stats.csv / battle_quest.csv /
/// GameConfig。改产出请改源头，然后回来同步本表。
///
/// ── 去零口径（2026-09-15 第二轮补齐）──
/// 已去零的**产出**：怪金、关卡通关金、金币副本、连杀奖励、加速出兵折金、装备分解金、
/// 成就奖励、日志成就、日志里程、离线挂机、广告金、章节首通剧情金、金币袋。
///
/// 刻意**不去零**的（别顺手也除一下，会反向破坏节奏）：
/// 1. GameConfig.GOLD_PER_TALENT_POINT = 100 —— 这是金币→天赋石的汇率，不是产出。
///    保持 100 时：第1章 ≈28 石、全程 ≈995 石；右侧天赋树全点满需 491 石，
///    即「打到第 6 章中点满流派树」，节奏刚好。改成 10 会变成第 1 章就能点掉半棵树。
/// 2. TalentDefs.Left.goldCost（50 + i×15，全树 13700 金）—— 它是**金币主水槽**，
///    不是产出。去零前第 1 章收入 2.2 万即可点满全树（这就是「给太多」）；
///    现在需要攒到第 3~5 章，配合商店/佣兵分钱才点得满。
/// 3. 佣兵解锁价 500 / 1500 / 5000（MercRosterDefs.RecruitGold）—— 同为水槽。
///    现在 500 金 ≈ 通关第 1 章总收入的 18%，是个要取舍的决定，不再是随手买。
/// 4. 商店价已在去零时**单独重定**（不是简单 ÷10），见 ShopDefs。
/// </summary>
public static class EconomyDefs
{
    // ============================================================
    // 一、单次尝试（一局）的收入
    // ============================================================

    /// <summary>每章 10 关，第 10 关是 Boss。</summary>
    public const int STAGES_PER_RUN = GameConfig.STAGES_PER_CHAPTER;

    /// <summary>各关期望怪数（读 GameConfig.GetStageMonsterTotal 的随机区间算的期望值）。</summary>
    public static readonly float[] MonstersPerStage =
        { 15.6f, 10.4f, 15.6f, 16.9f, 18.3f, 19.7f, 21.1f, 22.4f, 23.8f, 25.2f };

    /// <summary>波次加成：goldDrop = base × (1 + wave × 0.1)，平均取 1.25。</summary>
    public const float WAVE_GOLD_MUL = 1.25f;

    /// <summary>去零后各章普通怪平均 baseGold（monster_stats.csv 实际值）。</summary>
    public static float AvgMonsterGold(int chapter)
    {
        switch (Mathf.Clamp(chapter, 1, 8))
        {
            case 1: return 2.6f;
            case 2: return 2.5f;
            case 3: return 2.6f;
            case 4: return 2.8f;
            case 5: return 2.7f;
            case 6: return 3.0f;
            case 7: return 3.1f;
            default: return 3.2f;
        }
    }

    /// <summary>Boss 本体的 baseGold（去零后统一 20）。</summary>
    public const float BOSS_MONSTER_GOLD = 20f;

    /// <summary>第 stageNo 关（1..10）的金币收入。</summary>
    public static int StageGold(int chapter, int stageNo) => StageGold(chapter, stageNo, 1f);

    /// <summary>
    /// 第 stageNo 关的金币收入。monsterGoldMul = 难度倍率，
    /// 只放大**怪物掉金**（含 Boss 本体），通关金是固定奖励、不随难度放大。
    /// </summary>
    public static int StageGold(int chapter, int stageNo, float monsterGoldMul)
    {
        int s = Mathf.Clamp(stageNo, 1, STAGES_PER_RUN);
        float g = AvgMonsterGold(chapter) * WAVE_GOLD_MUL * monsterGoldMul;
        float monsters = MonstersPerStage[s - 1];

        if (s >= STAGES_PER_RUN) // Boss 关：小怪 + Boss 本体 + Boss 通关金
            return Mathf.RoundToInt((monsters - 1f) * g + BOSS_MONSTER_GOLD * monsterGoldMul + BossClearGold(chapter));

        return Mathf.RoundToInt(monsters * g + NormalClearGold(chapter));
    }

    /// <summary>普通关通关金（battle_quest.csv：normalBase 3 + normalChapterAdd 1 × 章节）。</summary>
    public static int NormalClearGold(int chapter) => 3 + chapter;

    /// <summary>Boss 关通关金（去零后：20/30/40/50/60/70/80/200）。</summary>
    public static int BossClearGold(int chapter)
    {
        switch (Mathf.Clamp(chapter, 1, 8))
        {
            case 1: return 20;
            case 2: return 30;
            case 3: return 40;
            case 4: return 50;
            case 5: return 60;
            case 6: return 70;
            case 7: return 80;
            default: return 200;
        }
    }

    /// <summary>一次尝试清掉 n 关（可带小数）的收入。</summary>
    public static int RunGold(int chapter, float stagesCleared) => RunGold(chapter, stagesCleared, 1f);

    /// <summary>一次尝试清掉 n 关的收入，怪物掉金再乘 monsterGoldMul（难度倍率）。</summary>
    public static int RunGold(int chapter, float stagesCleared, float monsterGoldMul)
    {
        float n = Mathf.Clamp(stagesCleared, 0f, STAGES_PER_RUN);
        int full = Mathf.FloorToInt(n);
        int gold = 0;
        for (int s = 1; s <= full; s++) gold += StageGold(chapter, s, monsterGoldMul);
        float frac = n - full;
        if (frac > 0f && full < STAGES_PER_RUN)
            gold += Mathf.RoundToInt(StageGold(chapter, full + 1, monsterGoldMul) * frac);
        return gold;
    }

    // ============================================================
    // 二、节奏：按「尝试次数」建模
    // ============================================================

    public enum GateKind { None, Build, Stat }

    public struct StageGate
    {
        public int chapter;
        public GateKind kind;
        /// <summary>这一章预期需要**多少次尝试**（含失败重试）。</summary>
        public int expectedTries;
        /// <summary>失败局平均能清到第几关（玩家在变强，所以逐章递增）。</summary>
        public float failStages;
        public string note;
    }

    /// <summary>
    /// 八章节奏。**前两章的锚点由策划给定：第 1 章 ≤10 次、第 2 章累计 ≤30 次。**
    /// 体力口径校验：满体力 100 点 = 10 次冒险，正好是第 1 章的预算；
    /// 第 2 章 20 次 = 200 体力 ≈ 满体力 2 次 + 约 4 天自然回复（24 点/天）。
    /// </summary>
    public static readonly StageGate[] Gates =
    {
        new StageGate { chapter = 1, kind = GateKind.None,  expectedTries = 10, failStages = 4.5f, note = "教学章，10 次内必过；满体力 100 点正好 = 10 次冒险" },
        new StageGate { chapter = 2, kind = GateKind.None,  expectedTries = 20, failStages = 5.5f, note = "累计 30 次。仍不卡，让玩家尝到构筑成长的甜头" },
        new StageGate { chapter = 3, kind = GateKind.Build, expectedTries = 22, failStages = 6.0f, note = "首个卡点：怪开始成群，没 AOE 会打得很难受" },
        new StageGate { chapter = 4, kind = GateKind.Build, expectedTries = 25, failStages = 6.5f, note = "主卡点：生存压力陡增，缺治疗/护盾/佣兵会反复失败" },
        new StageGate { chapter = 5, kind = GateKind.Build, expectedTries = 25, failStages = 7.0f, note = "Boss 有硬机制，需要对的技能组合而不是更高数值" },
        new StageGate { chapter = 6, kind = GateKind.Stat,  expectedTries = 28, failStages = 7.5f, note = "数值墙开始：需要装备强化等级" },
        new StageGate { chapter = 7, kind = GateKind.Stat,  expectedTries = 30, failStages = 8.0f, note = "星级墙：技能星级成为主要差距" },
        new StageGate { chapter = 8, kind = GateKind.Stat,  expectedTries = 35, failStages = 8.5f, note = "终章：金币不再是瓶颈，瓶颈是星级与词条" },
    };

    public static StageGate GateFor(int chapter) => Gates[Mathf.Clamp(chapter, 1, Gates.Length) - 1];

    /// <summary>通关某一章的金币收入（前 n-1 次失败 + 最后一次通关）。</summary>
    public static int ChapterGold(int chapter)
    {
        var g = GateFor(chapter);
        return (g.expectedTries - 1) * RunGold(chapter, g.failStages) + RunGold(chapter, STAGES_PER_RUN);
    }

    /// <summary>从头打到第 chapter 章结束的**累计**金币。</summary>
    public static int CumulativeGold(int chapter)
    {
        int sum = 0;
        for (int ch = 1; ch <= Mathf.Clamp(chapter, 1, 8); ch++) sum += ChapterGold(ch);
        return sum;
    }

    /// <summary>从头打到第 chapter 章结束的**累计**尝试次数。</summary>
    public static int CumulativeTries(int chapter)
    {
        int sum = 0;
        for (int ch = 1; ch <= Mathf.Clamp(chapter, 1, 8); ch++) sum += GateFor(ch).expectedTries;
        return sum;
    }

    /// <summary>累计尝试次数换算的体力消耗（每次冒险 10 点）。</summary>
    public static int CumulativeStamina(int chapter) => CumulativeTries(chapter) * StaminaSystem.ADVENTURE_COST;

    /// <summary>一章折算的天赋石（GOLD_PER_TALENT_POINT = 100）。</summary>
    public static int ChapterTalentPoints(int chapter) => ChapterGold(chapter) / GameConfig.GOLD_PER_TALENT_POINT;

    // ============================================================
    // 三、定价自检
    // ============================================================

    /// <summary>某个金币价格相当于「通关第 1 章累计收入」的几倍。</summary>
    public static string DescribePrice(long price)
    {
        int c1 = ChapterGold(1);
        if (c1 <= 0) return price.ToString();
        float n = price / (float)c1;
        return $"{price}（≈{n:0.00} × 通关第1章总收入）";
    }

    /// <summary>
    /// 打日志自检：各章收入、累计购买力、所有金币定价，并标出可能失衡的商品。
    /// 改完任何经济数值都跑一次。
    /// </summary>
    public static void LogDiagnostics()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[Economy] 单章收入（按尝试次数建模，产出去零后）");
        for (int ch = 1; ch <= 8; ch++)
        {
            var g = GateFor(ch);
            sb.AppendLine($"  第{ch}章：尝试 {g.expectedTries} 次（失败清 {g.failStages:0.0} 关）　" +
                          $"单次失败 {RunGold(ch, g.failStages)}　单次通关 {RunGold(ch, STAGES_PER_RUN)}　" +
                          $"本章 {ChapterGold(ch)}　累计 {CumulativeGold(ch)}　" +
                          $"累计体力 {CumulativeStamina(ch)}");
        }

        sb.AppendLine("[Economy] 单局金币峰值（用于校准「千金在手」阈值 1000；难度只放大怪物掉金）");
        for (int ch = 1; ch <= 8; ch++)
        {
            sb.AppendLine($"  第{ch}章满关：普通 {RunGold(ch, STAGES_PER_RUN, GameConfig.GetDifficultyGoldMul(0))}　" +
                          $"困难 {RunGold(ch, STAGES_PER_RUN, GameConfig.GetDifficultyGoldMul(1))}　" +
                          $"噩梦 {RunGold(ch, STAGES_PER_RUN, GameConfig.GetDifficultyGoldMul(2))}");
        }

        sb.AppendLine("[Economy] 天赋石节奏（GOLD_PER_TALENT_POINT=100，右侧流派树全满需 491 石）");
        int stones = 0;
        for (int ch = 1; ch <= 8; ch++)
        {
            stones += ChapterTalentPoints(ch);
            sb.AppendLine($"  打完第{ch}章：累计 {stones} 石");
        }

        sb.AppendLine("[Economy] 商店金币定价自检（基准：通关第1章累计 " + ChapterGold(1) + " 金）");
        for (int i = 0; i < ShopDefs.All.Length; i++)
        {
            var it = ShopDefs.All[i];
            if (it.currency != ResourceWallet.ResourceType.Gold) continue;
            float n = it.price / (float)ChapterGold(1);
            sb.AppendLine($"  {it.name}：{DescribePrice(it.price)}　限购 {it.dailyLimit}");
            if (n > 3f) sb.AppendLine("    ⚠ 超过 3 倍章节收入，玩家可能攒到弃游");
            if (n < 0.05f) sb.AppendLine("    ⚠ 不到 5% 章节收入，太便宜会被秒空");
        }
        Debug.Log(sb.ToString());
    }
}
