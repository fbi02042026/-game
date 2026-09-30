using UnityEngine;

/// <summary>
/// 【抽奖经济 · 唯一调参文件】进关抽奖的**全部数值**都在这个文件里，别处没有可调的数。
/// 2026-09-29 主人要求：抽奖币的**获得**和**价格**要能自己调，集中一处随时改。
///
/// ── 五分钟上手：想改什么就改哪一组 ──
///   ① 抽奖整体变贵 / 变便宜       → 一、价格（BASE_PRICE / PRICE_STEP / PRICE_CAP）
///   ② 定向比随机贵多少             → 一、价格（FOCUS_PRICE_MULT）
///   ③ 玩家攒币的快慢（抽得更频繁） → 二、产出（COIN_PER_NORMAL / ELITE / BOSS）
///   ④ 开局能抽几次                 → 三、开局启动（STARTER_COINS）
///   ⑤ 免费「再来一次」的概率       → 四、再来一次（REROLL_BASE_CHANCE）
///   ⑥ 老虎机演多久                   → 五、演出（JACKPOT_REVEAL_SEC，设 0 = 跳过演出）
///      （四个抽奖按钮点了**直接出结果**，不走演出，所以想改快慢只能改老虎机）
///
/// 改完**任何**一个数，跑一次 SlotMachineSystem.LogDiagnostics() 自检，
/// 会打印每关的随机价 / 定向价、一章能抽几次、开局能抽几次。
///
/// ⚠ 有两处在别的文件，改这里的同时必须同步（不然后台对不上）：
///   · R_FUND「初始资金」每级给多少启动币  → TalentDefs 的 R_FUND 节点 optValues（现为 4/8/12）
///   · R_LUCK「幸运提升」每级给多少概率    → TalentDefs 的 R_LUCK 节点 optValues（现为 3%/6%/9%）
/// 其余（存档、扣款、展示名）在 ResourceWallet / SlotMachineSystem，不含可调数值。
///
/// 计价货币：抽奖币（ResourceWallet.ResourceType.SlotCoin）。
/// 抽奖币与战斗奖励的**金币完全分开**，不与城镇升级 / 商店 / 天赋抢钱，两边各算各的。
/// </summary>
public static class SlotMachineDefs
{
    // ============================================================
    // 一、价格（花多少）
    //
    // 本局第 n 关的随机价 = BASE_PRICE + (n - 1) × PRICE_STEP，封顶 PRICE_CAP。
    // n 跨章累计（第 2 章第 1 关 = 第 11 关），所以后期会自然顶到 PRICE_CAP。
    //
    // 递增的理由：后面的关卡怪更多、产出也更多；不递增玩家后期会无脑抽满，
    // 抽奖就从「取舍」退化成「每关必点的上班打卡」。
    // ============================================================

    /// <summary>本局第 1 关的随机抽奖价（抽奖币）。</summary>
    public const int BASE_PRICE = 1;

    /// <summary>每推进一关涨多少抽奖币。</summary>
    public const int PRICE_STEP = 1;

    /// <summary>价格上限，防止后期天价。</summary>
    public const int PRICE_CAP = 8;

    /// <summary>
    /// 定向抽奖（指定佣兵 / 装备 / 技能）的价格倍率。
    /// 2026-09-29 主人拍板取 2：定向买的是「确定性」而不是折扣。
    /// 2 倍 = 一次定向的钱能抽两次随机 —— 玩家用一半的抽奖次数换精度，这是有意的取舍。
    /// 调 1 = 定向不再有代价（随机按钮会死）；调 3 以上 = 定向基本没人点得起。
    /// </summary>
    public const int FOCUS_PRICE_MULT = 2;

    // ============================================================
    // 二、产出（每关通关给多少）
    //
    // 抽奖币目前**只有「通关」一条来源**：SlotMachineSystem.GrantStageCoins。
    // 一章 10 关、末关固定 Boss，中间 8 关的精英数量由 StageRoller 每关现抽（不固定），
    // 按「7 普通 + 2 精英 + 1 Boss」估：7×3 + 2×5 + 1×8 = 39 币/章。
    //
    // 💡 2026-09-29 已修：产出现在随本局关卡数递增（见下方 COIN_GROWTH_PER_STAGE），
    //    「越往后每章能抽次数越少」的旧张力不再存在。要回到固定产出就把增长率设 0。
    //    精英关位置由 StageRoller 每关现抽（不固定），自检按「第 4、8 关是精英」估。
    // ============================================================

    /// <summary>每关通关产出的抽奖币（普通关）。</summary>
    public const int COIN_PER_NORMAL = 3;

    /// <summary>每关通关产出的抽奖币（精英关）。</summary>
    public const int COIN_PER_ELITE = 5;

    /// <summary>每关通关产出的抽奖币（Boss 关）。</summary>
    public const int COIN_PER_BOSS = 8;

    /// <summary>
    /// 每推进一关，抽奖币产出涨多少（比例）。**设 0 = 不随进度增长**（回到旧的固定 3/5/8）。
    /// 2026-09-29 主人拍板：抽奖币要「前期少、后面逐渐增加」。
    /// 0.06 的手感：第 1 关 ×1.00、第 10 关 ×1.54、第 25 关起顶到 ×2.00。
    ///
    /// 为什么要涨：抽奖价本来就随关卡数涨到 8 封顶，如果产出一直固定，
    /// 越往后「每关能抽几次」只会一路下滑，后期抽奖就从取舍退化成「一关一次打卡」。
    /// </summary>
    public const float COIN_GROWTH_PER_STAGE = 0.06f;

    /// <summary>产出增长倍率上限，防止后期币多到「每关随便抽」。</summary>
    public const float COIN_GROWTH_CAP = 2.0f;

    // ============================================================
    // 三、开局启动
    //
    // 每局开始时：若身上的币不足 STARTER_COINS 就补到这个数（**已攒的不清零**），
    // 再叠加「初始资金」(R_FUND) 天赋每级 COINS_PER_FUND_TALENT 币。
    // 当前 8 币 + 第 1 关价 1 = 开局理论能连抽多次（面板常驻，币够就能一直抽）。
    // ============================================================

    /// <summary>开局启动抽奖币的下限。</summary>
    public const int STARTER_COINS = 8;

    /// <summary>
    /// 天赋「初始资金」(R_FUND) 每级额外给的启动抽奖币。
    /// ⚠ 与 TalentDefs 的 R_FUND 节点 optValues 必须一致（现为 4/8/12 = 每级 4）。
    /// </summary>
    public const int COINS_PER_FUND_TALENT = 4;

    // ============================================================
    // 四、免费「再来一次」
    //
    // 抽完一次后按这个概率免费再抽一轮（沿用刚才那一类，不扣钱、不用再点按钮）。
    // 实际概率 = REROLL_BASE_CHANCE + REROLL_PER_TALENT × R_LUCK 等级，封顶 100%。
    // ============================================================

    /// <summary>「再来一次」的基础概率（0~1）。</summary>
    public const float REROLL_BASE_CHANCE = 0.05f;

    /// <summary>
    /// 天赋「幸运提升」(R_LUCK) 每级给的概率加成（0~1）。
    /// ⚠ 与 TalentDefs 的 R_LUCK 节点 optValues 必须一致（现为 3%/6%/9% = 每级 3%）。
    /// </summary>
    public const float REROLL_PER_TALENT = 0.03f;

    // ============================================================
    // 五、演出（只影响手感，不影响经济）
    // ============================================================

    /// <summary>
    /// 「幸运老虎机」（免费奖励）的演出时长（秒）：前 60% 快闪扫格、后 40% 停在抽中的那一类。
    /// 走完演出才弹**该类型的三选一**。
    ///
    /// 2026-09-29 主人拍板：四个抽奖按钮点了**直接出结果、一秒不等**（所以按钮不占任何演出时长）；
    /// 老虎机只在「再来一次」触发时出场，是**免费奖励**，走个形式给点仪式感，所以可以慢一点。
    /// 这个数只影响老虎机，把它设 0 = 老虎机跳过演出直接弹三选一。
    /// </summary>
    public const float JACKPOT_REVEAL_SEC = 1.2f;

    // ————————————————————————————————————————————————
    // 【已停用】下面四个数现在没有任何代码在读，调它们不会有任何效果。
    // 2026-09-29 老虎机演出改由 JACKPOT_REVEAL_SEC 驱动后不再需要。
    // 先留着不删（避免误删后被别处引用），将来要恢复长演出再一起启用。
    // ————————————————————————————————————————————————
    public const float ROLL_TICK_START = 0.07f;
    public const float ROLL_TICK_END = 0.40f;
    public const int ROLL_MIN_TICKS = 12;
    public const int ROLL_MAX_TICKS = 26;
}
