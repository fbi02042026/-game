using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 进关老虎机抽奖的逻辑层（2026-09-29 新增）。
/// 只管四件事：算本关价格、扣钱、定类型、判定「再来一次」。
/// 不碰 UI，也不生成卡 —— 卡池仍走 <see cref="DraftPool"/>，生效仍走 <see cref="RunDraftDirector.ApplyCard"/>。
///
/// 抽奖时机由 <see cref="BattleManager"/> 编排：进关后 → 战前剧情（若有）播完 → 首波怪物之前。
/// </summary>
public static class SlotMachineSystem
{
    /// <summary>本局第几关（1 起，跨章累计），用于算价格。</summary>
    public static int RunStageIndex()
    {
        var cm = ChapterManager.Instance;
        if (cm == null) return 1;
        int n = (cm.currentChapter - 1) * GameConfig.STAGES_PER_CHAPTER + cm.currentStageIndex + 1;
        return Mathf.Max(1, n);
    }

    /// <summary>本关抽奖价：越往后越贵，封顶 <see cref="SlotMachineDefs.PRICE_CAP"/>。</summary>
    public static int Price(int runStageIndex)
    {
        int n = Mathf.Max(1, runStageIndex);
        int p = SlotMachineDefs.BASE_PRICE + (n - 1) * SlotMachineDefs.PRICE_STEP;
        return Mathf.Min(p, SlotMachineDefs.PRICE_CAP);
    }

    /// <summary>
    /// 定向抽奖价（指定佣兵 / 装备 / 技能）= 随机价 × <see cref="SlotMachineDefs.FOCUS_PRICE_MULT"/>。
    /// 2026-09-29 主人拍板：面板常驻、有钱就能抽，不限次数；
    /// 定向的 2 倍价是「精度税」而不是次数限制 —— 玩家用一半的抽奖次数换确定性。
    /// </summary>
    public static int FocusPrice(int runStageIndex)
    {
        return Price(runStageIndex) * SlotMachineDefs.FOCUS_PRICE_MULT;
    }

    /// <summary>当前抽奖币。</summary>
    public static long Coins()
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        return data == null ? 0L : data.slotCoins;
    }

    /// <summary>
    /// 扣本关抽奖的抽奖币。
    /// 抽奖币直接存在存档里，不经过 <c>BattleManager.currentGold</c> 那套金币镜像，
    /// 所以既不需要同步镜像，也不需要再调 PersistBattleGold（那是为金币对齐加的）。
    /// </summary>
    public static bool TrySpend(int price)
    {
        return ResourceWallet.TrySpend(ResourceWallet.ResourceType.SlotCoin, price, save: true, notify: false);
    }

    /// <summary>本关通关该发的抽奖币（类型基础值，不含进度增长）。</summary>
    public static int CoinsForStage(StageType stageType)
    {
        switch (stageType)
        {
            case StageType.Boss: return SlotMachineDefs.COIN_PER_BOSS;
            case StageType.Elite: return SlotMachineDefs.COIN_PER_ELITE;
            default: return SlotMachineDefs.COIN_PER_NORMAL;
        }
    }

    /// <summary>
    /// 本关通关实际发的抽奖币 = 类型基础值 × 进度增长倍率。
    /// 倍率 = 1 + (本局第几关 - 1) × <see cref="SlotMachineDefs.COIN_GROWTH_PER_STAGE"/>，
    /// 封顶 <see cref="SlotMachineDefs.COIN_GROWTH_CAP"/>。
    /// 2026-09-29 主人拍板：抽奖币「前期少、后期逐渐增加」。
    /// </summary>
    public static int CoinsForStage(StageType stageType, int runStageIndex)
    {
        int baseCoins = CoinsForStage(stageType);
        int n = Mathf.Max(1, runStageIndex);
        float mul = Mathf.Min(1f + (n - 1) * SlotMachineDefs.COIN_GROWTH_PER_STAGE,
                              SlotMachineDefs.COIN_GROWTH_CAP);
        return Mathf.Max(1, Mathf.RoundToInt(baseCoins * mul));
    }

    /// <summary>通关发抽奖币。按本局关卡数递增，越往后给得越多。</summary>
    public static void GrantStageCoins(StageType stageType)
    {
        int n = CoinsForStage(stageType, RunStageIndex());
        if (n <= 0) return;
        ResourceWallet.Add(ResourceWallet.ResourceType.SlotCoin, n, save: true, notify: true);
    }

    /// <summary>本关可参与老虎机的类型：池空的类型不进（判空直接复用 DraftPool）。</summary>
    public static List<DraftCategory> AvailableCategories()
    {
        var list = new List<DraftCategory>(3);
        if (DraftPool.HasSkillContent()) list.Add(DraftCategory.Skill);
        if (DraftPool.HasMercContent()) list.Add(DraftCategory.Merc);
        if (DraftPool.HasEquipContent()) list.Add(DraftCategory.Equip);
        return list;
    }

    /// <summary>等权随机一个类型。结果在这里就定死，老虎机只负责把它演出来。</summary>
    public static DraftCategory RollCategory(List<DraftCategory> cats)
    {
        if (cats == null || cats.Count == 0) return DraftCategory.Skill;
        return cats[Random.Range(0, cats.Count)];
    }

    /// <summary>类型的中文名，用于抽奖结果的提示文案。</summary>
    public static string CategoryName(DraftCategory c)
    {
        switch (c)
        {
            case DraftCategory.Skill: return "技能";
            case DraftCategory.Merc: return "佣兵";
            default: return "装备";
        }
    }

    /// <summary>「再来一次」概率 = 基础 + 幸运提升天赋每级加成。</summary>
    public static float RerollChance()
    {
        float c = SlotMachineDefs.REROLL_BASE_CHANCE + SlotMachineDefs.REROLL_PER_TALENT * LuckTalentLevel();
        return Mathf.Clamp01(c);
    }

    /// <summary>
    /// 右列天赋「幸运提升」(R_LUCK) 的等级。
    /// 它 kind=Custom，不进属性系统，所以在这里按 id 直接读存档等级。
    /// </summary>
    public static int LuckTalentLevel()
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null || data.talents == null) return 0;
        data.talents.TryGetValue("R_LUCK", out int lv);
        return lv > 0 ? lv : 0;
    }

    /// <summary>
    /// 开局启动抽奖币：不足才补到「基础 + 初始资金天赋加成」，已攒的币不会清零。
    /// 每局进关时调一次，这样「初始资金」天赋在每一局都有效，而不只是新档那一次。
    /// </summary>
    public static void EnsureStarterCoins()
    {
        int need = SlotMachineDefs.STARTER_COINS + SlotMachineDefs.COINS_PER_FUND_TALENT * FundTalentLevel();
        long have = Coins();
        if (have >= need) return;
        ResourceWallet.Add(ResourceWallet.ResourceType.SlotCoin, need - (int)have, save: true, notify: false);
    }

    /// <summary>右列天赋「初始资金」(R_FUND) 的等级（kind=Custom，按 id 直接读）。</summary>
    public static int FundTalentLevel()
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null || data.talents == null) return 0;
        data.talents.TryGetValue("R_FUND", out int lv);
        return lv > 0 ? lv : 0;
    }

    /// <summary>是否触发「再来一次」（免费再转一轮，不再扣钱）。</summary>
    public static bool RollReroll() => Random.value < RerollChance();

    /// <summary>
    /// 抽奖经济自检：改完 <see cref="SlotMachineDefs"/> 里**任何**一个数，跑一次看曲线对不对。
    /// 打印每关的随机价 / 定向价 / 通产产出、一章能抽几次、开局能连抽几次。
    /// ⚠ 精英关位置由 StageRoller 每关现抽（不固定），这里按「第 4、8 关是精英」估。
    /// </summary>
    public static void LogDiagnostics()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[抽奖经济] 价格 BASE=" + SlotMachineDefs.BASE_PRICE +
                      " STEP=" + SlotMachineDefs.PRICE_STEP + " CAP=" + SlotMachineDefs.PRICE_CAP +
                      " 定向×" + SlotMachineDefs.FOCUS_PRICE_MULT);
        sb.AppendLine("[抽奖经济] 产出 普通 " + SlotMachineDefs.COIN_PER_NORMAL +
                      " / 精英 " + SlotMachineDefs.COIN_PER_ELITE + " / Boss " + SlotMachineDefs.COIN_PER_BOSS +
                      "　启动下限 " + SlotMachineDefs.STARTER_COINS);

        int stages = GameConfig.STAGES_PER_CHAPTER;
        int sumPrice = 0, sumFocus = 0, sumIncome = 0;
        for (int n = 1; n <= stages; n++)
        {
            int p = Price(n);
            int f = FocusPrice(n);
            sumPrice += p;
            sumFocus += f;
            int inc = n == stages ? CoinsForStage(StageType.Boss, n)
                    : (n == 4 || n == 8) ? CoinsForStage(StageType.Elite, n)
                    : CoinsForStage(StageType.Normal, n);
            sumIncome += inc;
            sb.AppendLine($"  第{n}关：随机 {p}　定向 {f}　通关 +{inc}");
        }

        sb.AppendLine($"[抽奖经济] 一章合计：产出 {sumIncome}　全随机抽满要 {sumPrice}　全定向抽满要 {sumFocus}");
        float r = sumPrice > 0 ? sumIncome / (float)sumPrice * stages : 0f;
        float fo = sumFocus > 0 ? sumIncome / (float)sumFocus * stages : 0f;
        sb.AppendLine($"[抽奖经济] 一章约能抽：随机 {r:0.0} 关次　定向 {fo:0.0} 关次（按每关抽一次估）");
        sb.AppendLine($"[抽奖经济] 开局 {SlotMachineDefs.STARTER_COINS} 币在第 1 关能连抽：" +
                      $"随机 {SlotMachineDefs.STARTER_COINS / Mathf.Max(1, Price(1))} 次　" +
                      $"定向 {SlotMachineDefs.STARTER_COINS / Mathf.Max(1, FocusPrice(1))} 次（面板常驻，币够就能一直抽）");
        sb.AppendLine($"[抽奖经济] 再来一次概率 {(RerollChance() * 100f):0.0}%（含 R_LUCK {LuckTalentLevel()} 级）");
        Debug.Log(sb.ToString());
    }
}
