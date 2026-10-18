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

    // ⚠【2026-10-10 已删除】Price(int runStageIndex) —— 旧「按关卡进度涨价」口径。
    //   抽奖价现在只跟**本局已抽数**走（每抽 ×1.1，主人拍板不封顶），唯一算式 =
    //   `PriceAfterDraws(draws)`，对外出口 = `DrawPrice()` / `DrawFocusPrice()`。
    //   留着就会变成第二套价格口径（面板一个价、扣费又一个价），所以删掉不留开关。

    // ============================================================
    // 【2026-10-10 主人拍板】抽奖价格真源（新）：按**本局已抽数**递增 ——
    // 每抽一次，下一抽贵 10%（主人原话「每抽一次奖增加下一次抽奖花费的金币 每次+10%」）。
    // 扣的币 = **战斗内金币**（currentGold，= 局外金币的战斗镜像，经 PersistBattleGold 差额写回）。
    // 旧「按关卡进度涨价」口径（Price(int)）已删除；FocusPrice(DraftCategory) 保留，
    // 但它现在直接返回 DrawFocusPrice()（同一种币、同一套价），不会再出现两套价格。
    // ============================================================

    /// <summary>每次抽奖后下一抽的涨价系数（+10%）。想调幅度改这一个常量。</summary>
    public const float DRAW_PRICE_GROWTH = 1.1f;

    /// <summary>已抽 draws 次之后，下一抽的价格（<b>价格曲线的唯一算式</b>，诊断 / 预估也走它）。</summary>
    public static int PriceAfterDraws(int draws)
    {
        float p = SlotMachineDefs.BASE_PRICE * Mathf.Pow(DRAW_PRICE_GROWTH, Mathf.Max(0, draws));
        return Mathf.Max(1, Mathf.RoundToInt(p));
    }

    /// <summary>已抽 draws 次之后，下一抽的定向价 = 随机价 × 精度税（同上，唯一算式）。</summary>
    public static int FocusPriceAfterDraws(int draws)
    {
        return PriceAfterDraws(draws) * SlotMachineDefs.FOCUS_PRICE_MULT;
    }

    /// <summary>下一抽随机价（单一出口，面板与扣费都必须走这里）。</summary>
    public static int DrawPrice()
    {
        return PriceAfterDraws(DrawIndex);
    }

    /// <summary>下一抽定向价 = 随机价 × <see cref="SlotMachineDefs.FOCUS_PRICE_MULT"/>（精度税，同一种币）。</summary>
    public static int DrawFocusPrice()
    {
        return FocusPriceAfterDraws(DrawIndex);
    }

    /// <summary>
    /// 定向抽奖的币 —— <b>唯一出口</b>。【2026-10-10 主人拍板】全部统一为战斗内金币（Gold）：
    /// 佣兵定向也不再花佣兵币（主人口径：抽奖花金币）。面板 <c>BattleEntryDraftPanel.CurrencyOf</c> 与这里同值。
    /// </summary>
    public static ResourceWallet.ResourceType FocusCurrency(DraftCategory cat)
    {
        return ResourceWallet.ResourceType.Gold;
    }

    /// <summary>
    /// 某一类定向抽奖的价格。【2026-10-10 主人拍板】统一金币后不再有佣兵币特例：
    /// 全部 = 随机价 × <see cref="SlotMachineDefs.FOCUS_PRICE_MULT"/>。
    /// </summary>
    public static int FocusPrice(DraftCategory cat)
    {
        return DrawFocusPrice();
    }

    /// <summary>查某个币种的余额（面板判「买得起吗」用，与扣费同一套口径）。</summary>
    public static long Balance(ResourceWallet.ResourceType type)
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        return ResourceWallet.Get(data, type);
    }

    // ⚠【2026-10-10 已删除】TrySpendFocus / TrySpend —— 抽奖扣费现在**唯一出口**是
    //   `BattleManager.SpendBattleGold`（扣局内金币 currentGold）。这两个扣的是另一套币（SlotCoin /
    //   甚至可能扣到城镇 Gold），留着迟早被人误用成第二套扣费口径，所以删掉不留开关。

    /// <summary>当前抽奖币（存档字段 slotCoins）。【2026-10-10 起不再发放、不再消费，仅存档兼容留着。】</summary>
    public static long Coins()
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        return data == null ? 0L : data.slotCoins;
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
    /// 本关通关实际发的局内金币 = 类型基础值 × 进度增长倍率。
    /// 倍率 = <b>(1 + <see cref="SlotMachineDefs.COIN_GROWTH_PER_STAGE"/>) ^ (本局第几关 - 1)</b>，
    /// 封顶 <see cref="SlotMachineDefs.COIN_GROWTH_CAP"/>。
    /// 2026-09-29 主人拍板：抽奖币「前期少、后期逐渐增加」；
    /// 【2026-10-10 主人拍板】改成**复利**（每关比上一关多 40%，主人区间 40%~50%），
    /// 因为抽奖价本身是每抽 ×1.1 的复利 —— 线性产出追不上指数价格，越往后反而越紧。
    /// </summary>
    public static int CoinsForStage(StageType stageType, int runStageIndex)
    {
        int baseCoins = CoinsForStage(stageType);
        int n = Mathf.Max(1, runStageIndex);
        float mul = Mathf.Min(Mathf.Pow(1f + SlotMachineDefs.COIN_GROWTH_PER_STAGE, n - 1),
                              SlotMachineDefs.COIN_GROWTH_CAP);
        int coins = Mathf.Max(1, Mathf.RoundToInt(baseCoins * mul));
        return coins;

        // ⚠【2026-10-05 已删除】原来这里还有一条「产出下限」：
        //   max(..., Price(n+1) × GuaranteeDraws(stageType))。
        //   主人拍板「删掉安全网」→ 连同 SlotMachineDefs.GUARANTEE_DRAWS_* 一起删了。
        //   那条下限恒不生效（价 80、产出 100），却跟 COIN_PER_* 抢同一件事（第二套产出口径）。
        //   「保证每关能抽」现在只由 COIN_PER_*（100 > 一抽 80）一处保证。
    }

    // ⚠【2026-10-05 已删除】GuaranteeDraws(StageType) —— 随 GUARANTEE_DRAWS_* 一起删。
    //   它只被上面那条下限用，删了没有别的调用点（全仓已确认）。

    /// <summary>
    /// 通关发**局内金币**（进 <c>BattleManager.currentGold</c>）。
    /// <b>2026-10-05 主人最终口径：「战斗内的都是局内的，每局携带的也都是局内的，
    /// 只有最后结算界面按通过的关卡数量给局外金币」</b> —— 这条仍然成立，只是「局内的」载体换了：
    /// 【2026-10-10 主人拍板】抽奖与启动金统一成局内金币，这套币不再有第二套，
    /// 所以通关补贴进 <c>currentGold</c>（局内可花），但 <c>countsAsEarned=false</c> →
    /// 抬高本局起点、结算差额不含它，<b>带不出裂隙</b>。
    /// 局外金币（Gold）来源：<b>结算界面按通过关卡数发放</b>（<c>StageGoldDefs</c>）＋ 局内真正赚到的部分。
    /// </summary>
    public static void GrantStageCoins(StageType stageType)
    {
        int n = CoinsForStage(stageType, RunStageIndex());
        if (n <= 0) return;
        var bm = BattleManager.Instance;
        if (bm == null)
        {
            Debug.LogError("[SlotMachine] 通关补贴发不出去：BattleManager.Instance 为空（不在战斗中）");
            return;
        }
        // 通关补贴跟启动金同性质：局内可花（保证每关还抽得起），但带不出去（不算本局收益）。
        // 局外金币仍然只有「结算按通过关卡数发放」那一笔。
        bm.AddRunGold(n, false);
        // 【2026-10-10】换进局内钱包后没有 ResourceWallet 那套 notify 了，玩家看不见「打完一关给了钱」
        // → 按 2026-10-06 主人拍板「金币获得要 force 弹出」补一条 toast。
        GlobalToastUI.Show($"通关奖励：抽奖币 +{n}", true);
    }

    /// <summary>
    /// 抽中「金币」这一档给多少**局内金币**。
    /// 主人 2026-10-05 拍板：「抽中的金币是当局抽奖用的」—— 给的是局内能继续抽的钱，带不出裂隙。
    /// 数额 = 当前这一抽的价格 × <see cref="SlotMachineDefs.GOLD_PRIZE_PRICE_MULT"/>（回血一半，不会变成无限抽）。
    /// 【2026-10-10】价改按已抽数走，所以这里也跟着走 <see cref="DrawPrice"/>（旧口径按关卡算的 Price 已删）。
    /// </summary>
    public static int GoldPrize()
    {
        return Mathf.Max(1, Mathf.RoundToInt(DrawPrice() * SlotMachineDefs.GOLD_PRIZE_PRICE_MULT));
    }

    // ⚠【2026-10-05 已删除】MaterialPrize()：主人拍板「强化石不能抽奖得到」，
    //   安慰奖只剩「金币」一档。强化石照旧走铁匠铺 / 休息关 / 商店（那几处不受影响）。

    /// <summary>本关可参与老虎机的类型：池空的类型不进（判空直接复用 DraftPool）。</summary>
    public static List<DraftCategory> AvailableCategories()
    {
        var list = new List<DraftCategory>(3);
        if (DraftPool.HasSkillContent()) list.Add(DraftCategory.Skill);
        if (DraftPool.HasMercContent()) list.Add(DraftCategory.Merc);
        if (DraftPool.HasEquipContent()) list.Add(DraftCategory.Equip);
        return list;
    }

    /// <summary>
    /// 按 <see cref="SlotMachineDefs.CategoryWeight"/> **加权**随机一个类型（装备 85 / 技能 10 / 佣兵 5）。
    /// <para>【2026-10-12 主人拍板】主人原话「装备权重改成70 技能20 佣兵10」；
    /// 【2026-10-13 主人拍板】主人反馈「佣兵和技能的机率太高了」→ 再压成 85/10/5。
    /// 概率只认 <see cref="SlotMachineDefs"/> 那三个常量，这里不另写一份。</para>
    /// 结果在这里就定死，老虎机只负责把它演出来。
    /// </summary>
    public static DraftCategory RollCategory(List<DraftCategory> cats)
    {
        if (cats == null || cats.Count == 0) return DraftCategory.Skill;

        int total = 0;
        for (int i = 0; i < cats.Count; i++)
            total += Mathf.Max(1, SlotMachineDefs.CategoryWeight(cats[i]));
        int roll = Random.Range(0, total);
        for (int i = 0; i < cats.Count; i++)
        {
            roll -= Mathf.Max(1, SlotMachineDefs.CategoryWeight(cats[i]));
            if (roll < 0) return cats[i];
        }
        return cats[cats.Count - 1];
    }

    // ⚠【2026-10-05 已删除】GrantTutorialWaveBonus() + SlotMachineDefs.TUTORIAL_WAVE_BONUS_COINS。
    //   它是当年「开局只带 100 币」时的补丁：一抽 80 抽完剩 20，第二抽点不起，所以每两波补 100。
    //   开局抬到 STARTER_COINS = 240（= 正好 3 抽）之后，引导三拍本来就走得完 → 这 100 是白送，
    //   而且是第二套给币口径（跟 COIN_PER_* / GrantRunStarterGold 抢同一件事）。
    //   主人拍板「清零 + 不要总打补丁」→ 整条链路删掉，不是留个开关设 0。
    //   引导三拍的币现在**只有一个来源**：开局启动金（GrantRunStarterGold，进局内钱包 currentGold；
    //   2026-10-14 主人拍板引导局 270、三拍照常扣钱，见 SlotMachineDefs.TUTORIAL_STARTER_COINS）。

    // ============================================================
    // 免费抽（2026-10-05 主人拍板：金币本开局免费抽 N 次，不耗金币）
    //
    // 次数**写在模式上**（IBattleMode.FreeEntryDraws），这里只管「本关用掉几次 / 还剩几次」。
    // ⚠ 计数是**进关清零**的静态值：免费抽是该模式进关送的额度，不该跨关累积，
    //   也不该落进 RunLoadout（那是本局构筑，免费额度不属于构筑）。
    // ============================================================

    static int _freeDrawsUsed = 0;

    /// <summary>
    /// 【2026-10-11 主人拍板】「再来一次」送的免费抽次数。
    /// 主人原话：「再来一次不是技能，是不用花费再抽一次，注意这次抽不用涨费用」。
    /// 与模式给的免费额度（<c>_freeDrawsUsed</c> / <see cref="TotalFreeDraws"/>）分开记：
    /// 奖励抽消耗的是这里，且**不推进 <see cref="DrawIndex"/>** —— 下一抽的价格不因此 +10%。
    /// 同样是进关清零（见 <see cref="ResetStageFreeDraws"/>）。
    /// </summary>
    static int _bonusFreeDraws = 0;

    /// <summary>进关时清掉免费抽计数（唯一入口：BattleManager.CoStageEntryDraft）。</summary>
    public static void ResetStageFreeDraws()
    {
        _freeDrawsUsed = 0;
        _bonusFreeDraws = 0;
    }

    /// <summary>本关还剩几次免费抽（模式额度 + 「再来一次」送的）。0 = 没有了（正常扣币）。</summary>
    public static int FreeDrawsLeft()
    {
        return Mathf.Max(0, TotalFreeDraws() - _freeDrawsUsed) + _bonusFreeDraws;
    }

    /// <summary>本模式给的免费抽总额度。0 = 该模式没有免费抽。</summary>
    public static int TotalFreeDraws()
    {
        var bm = BattleManager.Instance;
        if (bm == null || bm.Mode == null) return 0;
        return Mathf.Max(0, bm.Mode.FreeEntryDraws);
    }

    /// <summary>
    /// 用掉一次免费抽。**只在结果确认生效之后**调（与扣币同一位置），
    /// 出不了卡 / 玩家拒绝就不消耗额度 —— 免费的不代表可以浪费。
    /// </summary>
    public static void ConsumeFreeDraw()
    {
        _freeDrawsUsed++;
        Debug.Log($"[SlotMachineSystem] 免费抽已用 {_freeDrawsUsed}/{TotalFreeDraws()}（本次未扣抽奖币）");
    }

    /// <summary>【2026-10-11 主人拍板】「再来一次」命中：白送一次免费抽。</summary>
    public static void GrantBonusFreeDraw()
    {
        _bonusFreeDraws++;
        Debug.Log($"[SlotMachineSystem] 「再来一次」+1 次免费抽（待用 {_bonusFreeDraws}）");
    }

    /// <summary>
    /// 【2026-10-11 主人拍板】用掉一次「再来一次」送的免费抽。
    /// 返回 true = 这一抽走的是奖励额度（调用方据此**不推进抽数**，下一抽不涨价）。
    /// </summary>
    public static bool ConsumeBonusFreeDraw()
    {
        if (_bonusFreeDraws <= 0) return false;
        _bonusFreeDraws--;
        return true;
    }

    /// <summary>本局已抽奖次数（引导定序的序号；正式关只作统计）。没开局时是 0。</summary>
    public static int DrawIndex => RunLoadout.DrawCount;

    /// <summary>记一次抽奖：把本局抽数往前推一格（跨关累计，随本局构筑一起落盘）。</summary>
    public static void NoteDraw()
    {
        // 2026-10-06 主人拍板：引导三拍只加一条自检日志，节拍一处都不挪 —— 指望日志核对三拍依次是
        // 装备 / 佣兵 / 技能。序号取推进**之前**的 DrawCount：这一抽正是按 order[n] 定的类别（见 ResolveCategory）。
        if (IsTutorialRun)
        {
            int n = RunLoadout.DrawCount;
            var order = SlotMachineDefs.TutorialDrawOrder;
            string name = (order != null && n >= 0 && n < order.Length) ? CategoryName(order[n]) : "随机";
            Debug.Log($"[SlotMachineSystem] 引导局抽奖：序号 {n}（本局第 {n + 1} 抽）类别 = {name}");
        }
        RunLoadout.NoteDraw();
    }

    /// <summary>
    /// 现在是不是引导局 —— 决定走不走「前三抽定序」。
    /// <para>真源 = <see cref="BattleManager.IsTutorialRun"/>（规则包 <c>TutorialRules.Active</c>），
    /// <b>这里不写第二份判定</b>；加引导特例请加规则包。</para>
    /// </summary>
    static bool IsTutorialRun => BattleManager.Instance != null && BattleManager.Instance.IsTutorialRun;

    /// <summary>
    /// 本次抽奖给哪一类。
    /// <para><b>引导局</b>：本局前 <see cref="SlotMachineDefs.TutorialDrawOrder"/> 次走定序（装备→佣兵→技能）；
    /// <b>正式关</b>：从第一抽起就等权随机 —— 2026-10-05 主人拍板
    ///「正式关的时候是不是应该都是随机的了，按我设定的来」。</para>
    /// <para>定序的那一类当前取不到内容（池空）时自动落回随机 —— 绝不返回一个出不了卡的类型。</para>
    /// <para>【2026-10-17 主人拍板「开局卡关」】正式关保底佣兵：本局一只佣兵都没有时，第 3 抽起
    /// 强制出佣兵方向 —— 佣兵方向权重只有 5%（85/10/5），启动金只够 2 抽，新档大概率开局
    /// 0 佣兵、单人硬扛第一关（佣兵普通档攻击约玩家两倍，是前期战力主来源）。
    /// 佣兵池取不到内容时照旧落回随机（沿用上面「取不到内容自动落回随机」口径）；
    /// 引导局有自己的定序，不叠加。</para>
    /// </summary>
    public static DraftCategory ResolveCategory(List<DraftCategory> cats)
    {
        var order = SlotMachineDefs.TutorialDrawOrder;
        int n = RunLoadout.DrawCount;
        if (IsTutorialRun && order != null && n >= 0 && n < order.Length && cats != null && cats.Count > 0)
        {
            var want = order[n];
            if (cats.Contains(want)) return want;
        }
        // 正式关保底佣兵：触发条件唯一出口 = MercGuaranteeActive()（n 是当前这抽的 0 基序号，
        // NoteDraw 在抽完后才推进，n>=2 = 第 3 抽起）。窗口内安慰奖由 ConsolationAllowed 同步挡掉 ——
        // 保底必须真的给佣兵，不能让玩家花钱抽回币（2026-10-18 主人报「抽奖 3 次全是抽奖币」）。
        // 佣兵池取不到内容时照旧落回随机（沿用上面「取不到内容自动落回随机」口径）；
        // 引导局有自己的定序，不叠加。
        if (MercGuaranteeActive()
            && cats != null && cats.Contains(DraftCategory.Merc)
            && DraftPool.BuildCards(DraftCategory.Merc).Count > 0)
        {
            return DraftCategory.Merc;
        }
        return RollCategory(cats);
    }

    /// <summary>
    /// 这一抽是不是还在「引导定序」窗口里（引导局前 <see cref="SlotMachineDefs.TutorialDrawOrder"/> 次）。
    /// <para>窗口内<b>不把安慰奖（金币）放进卡池</b> —— 主人拍板引导三拍必须是
    /// 装备 → 佣兵 → 技能；玩家花第一抽的钱结果抽到一堆币，这拍教学就白教了。
    /// 定序本身不写在明面上（主人要求），窗口只用来决定卡池构成。</para>
    /// <para>正式关恒为 false → 安慰奖从第 1 抽起就在池里（纯随机的代价；概率公示弹窗里写得很清楚）。</para>
    /// </summary>
    public static bool InGuaranteeWindow()
    {
        var order = SlotMachineDefs.TutorialDrawOrder;
        int n = RunLoadout.DrawCount;
        return IsTutorialRun && order != null && n >= 0 && n < order.Length;
    }

    /// <summary>
    /// 这一抽能不能出金币安慰奖 —— 【2026-10-13 抽奖优化 #3】**唯一出口**。
    /// <para>口径 = <c>!InGuaranteeWindow() &amp;&amp; DrawIndex &gt; 0</c>：引导定序窗口内不安慰
    /// （沿用 <see cref="InGuaranteeWindow"/> 既有语义，它本身不动，还被 <see cref="ResolveCategory"/> 用）；
    /// 正式关<b>第 1 抽永不安慰</b> —— 防「开局花 80 抽回 40」的开局挫败，第 2 抽起恢复纯随机。</para>
    /// <para>【2026-10-18 主人报「第二关抽奖 3 次全是抽奖币」】新增第二道窗口：<see cref="MercGuaranteeActive"/>。
    /// 保底佣兵窗口内（非引导、已抽 ≥2 次、本局没佣兵）这一抽会被 <see cref="ResolveCategory"/>
    /// 强制成佣兵方向 —— 若池里还混着金币（权重 6），而酒馆上架队列每名佣兵权重只有 1
    /// （2026-10-19 拍板「上架等概率」），金币就以 6:3 盖过全体佣兵，保底变成「三分之二概率抽回币」。
    /// → 窗口内保底必须真的给佣兵，安慰奖整抽不发；佣兵抽到手窗口自动关闭，安慰奖恢复。
    /// 与「佣兵确有内容可给」同源判定（<see cref="AvailableCategories"/> 含 Merc 才挡），
    /// 真没佣兵可给时维持原口径，不扩大化。</para>
    /// <para>两处调用点（抽取与概率公示）都改问这里，公示与实际自动同源：
    /// <c>BattleManager.CoInstantPick</c> 的 <c>DraftPool.PickOne</c>、<c>DraftPool.BuildOddsRows</c>。
    /// ⚠ 免费老虎机三选一（<c>DraftPool.BuildCards(cat)</c>）按现有意图硬编码带安慰奖，不归这里管。</para>
    /// </summary>
    public static bool ConsolationAllowed()
    {
        if (InGuaranteeWindow()) return false;
        if (MercGuaranteeActive() && AvailableCategories().Contains(DraftCategory.Merc))
            return false;
        return DrawIndex > 0;
    }

    /// <summary>
    /// 【2026-10-17 主人拍板「开局卡关」】正式关保底佣兵的**触发条件**（唯一出口）：
    /// 非引导局、本局已抽 ≥2 次（第 3 抽起）、且本局一只佣兵都没有。
    /// <see cref="ResolveCategory"/> 据此强制佣兵方向；<see cref="ConsolationAllowed"/>
    /// 据此把金币安慰奖挡在保底窗口外（2026-10-18 主人报「抽奖 3 次全是抽奖币」的根因修复）。
    /// ⚠ 是否真的强制还要看佣兵方向**有内容可给**（cats 含 Merc），那道可用性判定留在 <see cref="ResolveCategory"/>。
    /// </summary>
    public static bool MercGuaranteeActive()
    {
        return !IsTutorialRun
            && DrawIndex >= 2
            && RunLoadout.Mercs().Count == 0;
    }

    // 【2026-10-11 主人拍板】「职业白送初始技能」整条作废 ——
    //   原 BuildGuaranteedSkillCard()：本局第一次抽到技能时**必给**该职业的 DefaultSkillId，
    //   等于每个职业开局都白送一个（法师送的还是传说天雷裁决，主人反馈「这么容易抽到吗」）。
    //   主人口径：**不管什么职业开局都不白送技能，大家的抽中概率一样**（一律按稀有度权重走随机池）。
    //   → 函数本体连同 BattleManager.CoMidBattleDraft 里的调用一并删除，不留开关、不留空壳。
    // ⚠ 别再往回加：技能只认 RunLoadout 的本局构筑，职业只决定**武器 / 属性 / 立绘**，不决定技能。

    /// <summary>
    /// 引导局：塔克（<see cref="StoryProgress.TutorialMercHireId"/>，H003 剑盾卫士）**已经抽到了吗**。
    /// <para><b>2026-10-06 主人拍板：「引导佣兵招没招」只许有一个出口</b> ——
    /// 原来是两套状态（<c>BattleUI.TutorialMercDrawn</c> 一份、<c>TutorialDirector.ShowMercHud</c> 一份），会对不上。
    /// 现在全项目只有这一处判据，两边都来读它。</para>
    /// <para>判据 = 本局抽数<b>越过</b> <see cref="SlotMachineDefs.TutorialDrawOrder"/> 里「佣兵」那一格。
    /// 抽数是<b>局内</b>数据（<c>RunLoadout.drawCount</c>），每局归零 → 不需要手动置 false/true。</para>
    /// </summary>
    public static bool TutorialMercDrawn =>
        IsTutorialRun && DrawIndex > System.Array.IndexOf(SlotMachineDefs.TutorialDrawOrder, DraftCategory.Merc);

    /// <summary>
    /// 佣兵类的保底内容：引导局第 2 抽（定序里「佣兵」那一格）= <b>直接招募塔克本人入队</b>。
    /// <para>2026-10-06 主人拍板：「第 2 抽就是直接招募引导佣兵入队」，碎片那条口径作废。</para>
    /// <para>正式关 / 序号不匹配 → 返回 <c>default</c>，走正常随机池；
    /// 取不到花名册定义或 AssetId 为空 → <c>LogError</c> + <c>default</c>（fail closed，绝不静默回退随机）。</para>
    /// <para>序号一律从 <see cref="SlotMachineDefs.TutorialDrawOrder"/> 取，<b>不写死 1</b>。</para>
    /// </summary>
    public static DraftCard BuildGuaranteedMercCard()
    {
        var order = SlotMachineDefs.TutorialDrawOrder;
        int mercSlot = order != null ? System.Array.IndexOf(order, DraftCategory.Merc) : -1;
        if (!IsTutorialRun || mercSlot < 0 || DrawIndex != mercSlot) return default;

        // ⚠ MercRosterDefs.Def 是 struct，判空只能看字段（HireId / AssetId）
        if (!MercRosterDefs.TryGetByHireId(StoryProgress.TutorialMercHireId, out var def)
            || string.IsNullOrEmpty(def.HireId))
        {
            Debug.LogError($"[SlotMachineSystem] 引导招募保底取不到花名册定义 hireId={StoryProgress.TutorialMercHireId}（不回退随机池）");
            return default;
        }
        if (string.IsNullOrEmpty(def.AssetId))
        {
            Debug.LogError($"[SlotMachineSystem] 引导招募保底花名册缺 AssetId：hireId={def.HireId}（不回退随机池）");
            return default;
        }

        // 花名册里没有星级 / 等级字段 → 取初始 1 星 1 级（与 BuildMercData 的兜底一致）
        int star = 1;
        int level = 1;
        // 稀有度映射照 DraftPool.CollectRecruitMercs 的写法走 SkillRarityUtil，不自己发明三目
        SkillRarity rar = SkillRarityUtil.FromMerc(def.Rarity);
        string rarName = SkillRarityUtil.DisplayName(rar);
        return new DraftCard
        {
            Kind = DraftCardKind.MercRecruit,
            Id = def.AssetId,
            HireId = def.HireId,
            // 【2026-10-11 主人拍板】佣兵名字**只取真名**（马库斯 / 塔克 / 米娅…），
            // 不写昵称、更不写职业名。旧写法 Title=「米娅·小红」、Desc=「[普通] 游侠 Lv1 ★1」，
            // 于是同一位佣兵在抽卡卡面、场景头顶、头像栏显示成三个不同的名字
            //（主人反馈「抽奖提示米娅 / 场景小红 / 头像游侠」）。
            // 职业名（剑盾卫士 / 游侠…）是**职业**不是名字，别再拿来当名字用。
            Title = def.Name,
            Desc = $"[{rarName}]　Lv{level}　★{star}",
            Rarity = rar,
            Tag = SynergyTag.Summon,
            Star = star,
            MercLevel = level
        };
    }

    // 【2026-10-06 撤回】这里原本加过 BuildGuaranteedEquipCard（引导第 1 抽必给木制圆盾 equip_shield_1）。
    // 主人当天澄清：**第一抽是「随机拿到一件新装备」**，木制圆盾只是**起步装备**换成它
    // （player_job_base_stats P001 副手 = equip_shield_1），跟抽奖无关。
    // 装备类一律走 DraftPool 随机池，不再有装备保底 —— 旧实现整段删除，不留开关。

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
    /// 【2026-10-10 主人拍板】开局启动金 = 基础 + 「初始资金」天赋每级加成，
    /// 进**局内金币钱包**（<c>BattleManager.currentGold</c>），不再是另一套抽奖币。
    /// 每局进关时调一次（续关不调），这样「初始资金」天赋每一局都有效，而不只是新档那一次。
    /// <para>【2026-10-14 主人拍板】基础值分两档：**正式关 200**（<see cref="SlotMachineDefs.STARTER_COINS"/>）、
    /// **引导局 270**（<see cref="SlotMachineDefs.TUTORIAL_STARTER_COINS"/>，兜住引导三拍扣费 265）。</para>
    /// ⚠ 这笔<b>不算本局收益</b>（countsAsEarned=false）→ 结算差额不把它当赚的钱写回存档，不能白刷。
    /// </summary>
    public static int RunStarterGold(bool tutorial)
    {
        int baseGold = tutorial ? SlotMachineDefs.TUTORIAL_STARTER_COINS : SlotMachineDefs.STARTER_COINS;
        return baseGold + SlotMachineDefs.COINS_PER_FUND_TALENT * FundTalentLevel();
    }

    /// <summary>
    /// 把启动金发进局内钱包。<b>唯一调用点 = <c>RunDraftDirector.OnRunStart</c>（非续关那一局）。</b>
    /// <paramref name="tutorial"/> = 本局是否引导局（决定启动金 270 / 200 档位）。
    /// 不在战斗中 → LogError（铁律 14：fail closed，绝不静默跳过让开局没钱抽）。
    /// </summary>
    public static void GrantRunStarterGold(bool tutorial)
    {
        var bm = BattleManager.Instance;
        if (bm == null)
        {
            Debug.LogError("[SlotMachine] 启动金发不出去：BattleManager.Instance 为空（不在战斗中）");
            return;
        }
        bm.AddRunGold(RunStarterGold(tutorial), false);
    }

    /// <summary>
    /// **局末清零**抽奖币（存档字段 slotCoins）。
    /// 【2026-10-10 主人拍板】局内钱包已统一成 <c>BattleManager.currentGold</c>，抽奖币不再发放也不再消费，
    /// 这个清零现在只清老存档残留，留作存档兼容（不删是为了不动既有存档结构）。
    /// <para>2026-10-05 主人拍板：「打完一章后清零，不过玩家可以选择是否进行下一章，这时不清零」。</para>
    /// <para>唯一出口 = <c>BattleManager.EndRunLoadout()</c>（死亡 / 撤离 / 回城三条收尾都走它），
    /// 而「进下一章」那条分支<b>不</b>走它 → 币自然跟着带进下一章。<b>别在别处再写第二份清零。</b></para>
    /// 下一局进关时由 <see cref="GrantRunStarterGold"/> 重新补启动金，所以清零不会卡死玩家。
    /// </summary>
    public static void ClearRunCoins()
    {
        long have = Coins();
        if (have <= 0) return;
        ResourceWallet.TrySpend(ResourceWallet.ResourceType.SlotCoin, have, save: true, notify: false);
    }

    /// <summary>右列天赋「初始资金」(R_FUND) 的等级（kind=Custom，按 id 直接读）。</summary>
    public static int FundTalentLevel()
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null || data.talents == null) return 0;
        data.talents.TryGetValue("R_FUND", out int lv);
        return lv > 0 ? lv : 0;
    }

    /// <summary>【2026-10-12 主人拍板】这一抽的结果是不是「再来一次」——
    /// 抽奖**开始前**掷（命中则本次不出类型奖品，只送一次免费抽，见 BattleManager.CoStageEntryDraft）。</summary>
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
        // 【2026-10-10】价不再跟关卡走，而是跟**本局已抽数**走（每抽 ×1.1）。
        // 这里按「每关只抽一次」估：第 n 关那一抽 = 已抽 n-1 次。
        for (int n = 1; n <= stages; n++)
        {
            int p = PriceAfterDraws(n - 1);
            int f = FocusPriceAfterDraws(n - 1);
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
                      $"随机 {SlotMachineDefs.STARTER_COINS / Mathf.Max(1, PriceAfterDraws(0))} 次　" +
                      $"定向 {SlotMachineDefs.STARTER_COINS / Mathf.Max(1, FocusPriceAfterDraws(0))} 次（面板常驻，币够就能一直抽）");
        // 2026-10-05：启动币改成「每局一次」，每关靠产出；这里把「每关产出够不够下一关一抽」打出来自检
        sb.AppendLine("[抽奖经济] 启动币每局只补一次（EnableRunDraft 开新局那关）；每关产出已抬到 ≥ 下一关一抽：");
        for (int n = 1; n <= stages; n++)
        {
            StageType st = n == stages ? StageType.Boss : (n == 4 || n == 8) ? StageType.Elite : StageType.Normal;
            int inc = CoinsForStage(st, n);
            int nextPrice = PriceAfterDraws(n);   // 通关这一关时已抽 n 次 → 下一抽的价
            sb.AppendLine($"  第{n}关通关 +{inc}　第{n + 1}关价 {nextPrice}　→ 能抽 {inc / Mathf.Max(1, nextPrice)} 次");
        }
        sb.AppendLine($"[抽奖经济] 再来一次概率 {(RerollChance() * 100f):0.0}%（含 R_LUCK {LuckTalentLevel()} 级）");
        Debug.Log(sb.ToString());
    }
}
