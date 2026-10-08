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

    /// <summary>
    /// 某一类的定向抽奖用哪种币 —— <b>唯一出口</b>（2026-10-07 主人拍板）。
    /// 佣兵 = <see cref="ResourceWallet.ResourceType.MercGold"/>（佣兵币），其余 = 抽奖币。
    /// ⚠ 其余三类按钮开放后要换币，只改这一个函数；面板上的货币图标也查它
    /// （<c>BattleEntryDraftPanel.CurrencyOf</c> 必须与这里同口径）。
    /// </summary>
    public static ResourceWallet.ResourceType FocusCurrency(DraftCategory cat)
    {
        return cat == DraftCategory.Merc
            ? ResourceWallet.ResourceType.MercGold
            : ResourceWallet.ResourceType.SlotCoin;
    }

    /// <summary>
    /// 某一类定向抽奖的价格（按各自的币种算）。
    /// 佣兵 = <see cref="SlotMachineDefs.MERC_FOCUS_PRICE"/> 枚佣兵币；
    /// 装备 / 技能 = 随机价 × <see cref="SlotMachineDefs.FOCUS_PRICE_MULT"/> 枚抽奖币。
    /// </summary>
    public static int FocusPrice(DraftCategory cat)
    {
        return cat == DraftCategory.Merc
            ? SlotMachineDefs.MERC_FOCUS_PRICE
            : FocusPrice(RunStageIndex());
    }

    /// <summary>查某个币种的余额（面板判「买得起吗」用，与扣费同一套口径）。</summary>
    public static long Balance(ResourceWallet.ResourceType type)
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        return ResourceWallet.Get(data, type);
    }

    /// <summary>
    /// 扣某一类定向抽奖的币（按 <see cref="FocusCurrency"/> 选币种）。这是定向抽奖的<b>唯一扣费出口</b>。
    /// </summary>
    public static bool TrySpendFocus(DraftCategory cat, int price)
    {
        if (price <= 0) return true;
        return ResourceWallet.TrySpend(FocusCurrency(cat), price, save: true, notify: false);
    }

    /// <summary>当前抽奖币。</summary>
    public static long Coins()
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        return data == null ? 0L : data.slotCoins;
    }

    /// <summary>
    /// 扣本关**随机**抽奖的抽奖币（定向走 <see cref="TrySpendFocus"/>）。
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
    /// 通关发**局内抽奖币**。
    /// <b>2026-10-05 主人最终口径：「战斗内的都是局内的，每局携带的也都是局内的，
    /// 只有最后结算界面按通过的关卡数量给局外金币」</b>
    /// → 通关这一笔发生在<b>战斗内</b>，所以是局内币，进 <c>ResourceWallet.ResourceType.SlotCoin</c>。
    /// ⚠ 中途曾误改成 Gold 又改回来 —— <b>这里只能发 SlotCoin</b>。
    /// 局外金币（Gold）唯一来源是<b>结算界面按通过关卡数发放</b>，见 <c>StageGoldDefs</c>。
    /// </summary>
    public static void GrantStageCoins(StageType stageType)
    {
        int n = CoinsForStage(stageType, RunStageIndex());
        if (n <= 0) return;
        ResourceWallet.Add(ResourceWallet.ResourceType.SlotCoin, n, save: true, notify: true);
    }

    /// <summary>
    /// 抽中「金币」这一档给多少**抽奖币**。
    /// 主人 2026-10-05 拍板：「抽中的金币是当局抽奖用的」—— 给的是抽奖币，不是城镇那套通用金币。
    /// 数额 = 本关一抽价格 × <see cref="SlotMachineDefs.GOLD_PRIZE_PRICE_MULT"/>（回血一半，不会变成无限抽）。
    /// </summary>
    public static int GoldPrize()
    {
        int n = RunStageIndex();
        return Mathf.Max(1, Mathf.RoundToInt(Price(n) * SlotMachineDefs.GOLD_PRIZE_PRICE_MULT));
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
    /// 按 <see cref="SlotMachineDefs.CategoryWeight"/> **加权**随机一个类型（技能 5 / 装备 3 / 佣兵 2）。
    /// <para>2026-10-05 主人拍板「佣兵的概率应该比装备低」—— 以前是三类等权（各 1/3），现在加权。</para>
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
    //   而且是第二套给币口径（跟 COIN_PER_* / EnsureStarterCoins 抢同一件事）。
    //   主人拍板「清零 + 不要总打补丁」→ 整条链路删掉，不是留个开关设 0。
    //   引导三拍的币现在**只有一个来源**：开局 240（EnsureStarterCoins）。

    // ============================================================
    // 免费抽（2026-10-05 主人拍板：金币本开局免费抽 N 次，不耗金币）
    //
    // 次数**写在模式上**（IBattleMode.FreeEntryDraws），这里只管「本关用掉几次 / 还剩几次」。
    // ⚠ 计数是**进关清零**的静态值：免费抽是该模式进关送的额度，不该跨关累积，
    //   也不该落进 RunLoadout（那是本局构筑，免费额度不属于构筑）。
    // ============================================================

    static int _freeDrawsUsed = 0;

    /// <summary>进关时清掉免费抽计数（唯一入口：BattleManager.CoStageEntryDraft）。</summary>
    public static void ResetStageFreeDraws()
    {
        _freeDrawsUsed = 0;
    }

    /// <summary>本关还剩几次免费抽。0 = 没有了（正常扣币）。</summary>
    public static int FreeDrawsLeft()
    {
        int total = TotalFreeDraws();
        if (total <= 0) return 0;
        return Mathf.Max(0, total - _freeDrawsUsed);
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
    /// 技能类的保底内容：本局<b>第一次</b>拿到技能时必给该职业的初始技能，其余时候返回无效卡（走正常随机池）。
    /// 开局不带初始技能（见 <c>RunLoadout.BeginNew</c>），所以这一下一定是「拿到本命技能」的爽点，
    /// 不会开出个跟职业八竿子打不着的技能。
    /// <para>2026-10-05 改：以前挂在「本局第 3 抽」这个序号上（= 定序里 Skill 的下标）。
    /// 正式关改成纯随机之后序号不再有意义（第 3 抽不一定是技能），
    /// → 改成按「身上还没有技能」判定。引导局行为<b>完全不变</b>：
    /// 三拍定序走到第 3 拍抽技能时，身上确实还没有技能。</para>
    /// </summary>
    public static DraftCard BuildGuaranteedSkillCard()
    {
        if (RunLoadout.HasAnySkill) return default;
        // GetSelected() 给的是职业枚举，要再 Get(job) 才拿到带 DefaultSkillId 的职业定义
        string id = PlayerJobDefs.Get(PlayerJobDefs.GetSelected()).DefaultSkillId;
        if (string.IsNullOrEmpty(id) || RunLoadout.HasSkill(id)) return default;
        return RunDraftDirector.BuildSkillCardById(id);
    }

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
            Title = $"{def.Name}·{def.Nickname}",
            Desc = $"[{rarName}] {def.JobName}　Lv{level}　★{star}",
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

    /// <summary>
    /// **局末清零**抽奖币。
    /// <para>2026-10-05 主人拍板：「打完一章后清零，不过玩家可以选择是否进行下一章，这时不清零」。</para>
    /// <para>唯一出口 = <c>BattleManager.EndRunLoadout()</c>（死亡 / 撤离 / 回城三条收尾都走它），
    /// 而「进下一章」那条分支<b>不</b>走它 → 币自然跟着带进下一章。<b>别在别处再写第二份清零。</b></para>
    /// 下一局进关时由 <see cref="EnsureStarterCoins"/> 重新补到启动值，所以清零不会卡死玩家。
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
        // 2026-10-05：启动币改成「每局一次」，每关靠产出；这里把「每关产出够不够下一关一抽」打出来自检
        sb.AppendLine("[抽奖经济] 启动币每局只补一次（EnableRunDraft 开新局那关）；每关产出已抬到 ≥ 下一关一抽：");
        for (int n = 1; n <= stages; n++)
        {
            StageType st = n == stages ? StageType.Boss : (n == 4 || n == 8) ? StageType.Elite : StageType.Normal;
            int inc = CoinsForStage(st, n);
            int nextPrice = Price(n + 1);
            sb.AppendLine($"  第{n}关通关 +{inc}　第{n + 1}关价 {nextPrice}　→ 能抽 {inc / Mathf.Max(1, nextPrice)} 次");
        }
        sb.AppendLine($"[抽奖经济] 再来一次概率 {(RerollChance() * 100f):0.0}%（含 R_LUCK {LuckTalentLevel()} 级）");
        Debug.Log(sb.ToString());
    }
}
