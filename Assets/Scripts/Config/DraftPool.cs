using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>一张抽卡候选（技能 / 佣兵 / 装备 / 强化）。</summary>
public struct DraftCard
{
    public DraftCardKind Kind;
    /// <summary>技能 id、佣兵 AssetId、强化 id；装备卡用 <see cref="Equip"/>。</summary>
    public string Id;
    public string HireId;
    public string Title;
    public string Desc;
    public SkillRarity Rarity;
    public SynergyTag Tag;
    /// <summary>操作后的星级（新技能/新佣兵为初始星）。</summary>
    public int Star;
    /// <summary>操作后的佣兵等级。</summary>
    public int MercLevel;
    public bool IsAffinity;
    /// <summary>预估战力增量（UI 展示「+NNN」）。</summary>
    public int PowerDelta;
    /// <summary>装备卡专用：本局装备实例（撤离不带出）。</summary>
    public EquipInstance Equip;
    /// <summary>金币卡的数量（2026-10-05 安慰奖用；强化石那档已删）。</summary>
    public int Amount;

    public bool IsValid
    {
        get
        {
            if (Kind == DraftCardKind.Equip) return Equip != null;
            // 金币：Id 只是类型标记，真正的判据是数量 > 0（2026-10-05）
            if (Kind == DraftCardKind.Gold) return Amount > 0;
            return !string.IsNullOrEmpty(Id);
        }
    }
}

/// <summary>
/// 局内抽卡池（纯本地随机，不联网）。
/// 分两层：先出「方向标签」（技能 / 佣兵 / 装备），玩家点定方向后出该方向三选一。
/// 全方向都抽不出来时才回落到通用强化卡，保证永不满盘空手。
/// </summary>
public static class DraftPool
{
    public const int CardCount = 3;

    // ============================================================
    // 第一层：方向标签
    // ============================================================

    public static List<DraftCategory> BuildCategories()
    {
        var list = new List<DraftCategory>(3);
        if (HasSkillContent()) list.Add(DraftCategory.Skill);
        if (HasMercContent()) list.Add(DraftCategory.Merc);
        if (HasEquipContent()) list.Add(DraftCategory.Equip);
        if (list.Count == 0) list.Add(DraftCategory.Skill);
        return list;
    }

    public static bool HasSkillContent()
    {
        if (!RunLoadout.IsSkillFull && CollectNewSkills().Count > 0) return true;
        return CollectUpgradableSkills().Count > 0;
    }

    public static bool HasMercContent()
    {
        if (!RunLoadout.IsMercFull && CollectRecruitMercs().Count > 0) return true;
        // 【2026-10-23 主人拍板】战斗佣兵升级暂时注释：升级卡已不进池，这里也不能再把它算作有效内容，
        // 否则会出现「判定佣兵类有货、实际池子没有升级卡」的口径不一致（与 2026-10-20 升星下架同款）。
        // 恢复时把这行注释放开即可。
        //if (CollectMercLevelCards().Count > 0) return true;
        // 【2026-10-20 主人拍板】战斗佣兵升星暂时注释：升星卡已不进池，这里也不能再把它算作有效内容，
        // 否则会出现「判定佣兵类有货、实际池子为空」的空转。恢复时把这行注释放开、删掉 return false。
        //return CollectMercStarCards().Count > 0;
        return false;
    }

    public static bool HasEquipContent() => ConfigManager.Instance != null;

    public static string CategoryHint(DraftCategory c)
    {
        switch (c)
        {
            case DraftCategory.Skill:
                return RunLoadout.IsSkillFull
                    ? $"技能 {RunLoadout.SkillIds().Count}/{RunLoadout.MaxSkillSlots} 已满 · 升星强化"
                    : $"技能 {RunLoadout.SkillIds().Count}/{RunLoadout.MaxSkillSlots} 槽";
            case DraftCategory.Merc:
                return $"佣兵 {RunLoadout.Mercs().Count}/{RunLoadout.MaxRunMercs} 位";
            default:
                return "本局装备 · 不带出";
        }
    }

    // ============================================================
    // 第二层：具体三选一
    // ============================================================

    /// <summary>
    /// 该类的三选一（含兜底）。
    /// </summary>
    public static List<DraftCard> BuildCards(DraftCategory c) => BuildCards(c, true);

    /// <param name="consolation">
    /// ⚠【2026-10-19 主人拍板后已失效】抽奖币改成**独立方向**（<see cref="DraftCategory.Gold"/>，
    /// 独立方向，权重 5 不随佣兵联动），不再混进任何类的池子 → 本参数不再往池里塞金币卡（见 <see cref="AddConsolation"/>）。
    /// 保留签名是为了不动六处调用点；<see cref="SlotMachineSystem.GoldAllowed"/> 才是「这一抽出不出金币方向」的唯一出口。
    /// </param>
    public static List<DraftCard> BuildCards(DraftCategory c, bool consolation)
    {
        switch (c)
        {
            case DraftCategory.Merc: return BuildMercCards(consolation);
            case DraftCategory.Equip: return BuildEquipCards(consolation);
            case DraftCategory.Gold: return new List<DraftCard>(1) { CollectGoldCard().Card };
            default: return BuildSkillCards(consolation);
        }
    }

    static List<DraftCard> BuildSkillCards(bool consolation)
    {
        var cards = new List<DraftCard>(CardCount);
        FillFromWeighted(BuildWeightedPool(DraftCategory.Skill, consolation), cards);
        FillFallback(cards, DraftCategory.Skill);
        return cards;
    }

    static List<DraftCard> BuildMercCards(bool consolation)
    {
        var cards = new List<DraftCard>(CardCount);
        FillFromWeighted(BuildWeightedPool(DraftCategory.Merc, consolation), cards);
        FillFallback(cards, DraftCategory.Merc);
        return cards;
    }

    /// <summary>
    /// 该类卡池的**完整加权候选**（三选一和「直接抽 1 张」共用同一份，概率公示也从这里算 ——
    /// 只此一处，弹窗上写的概率就永远是真数，不会跟实际抽取对不上）。
    /// ⚠【2026-10-19 主人拍板】金币不再混池 —— 三类池子里只有真货；
    /// 抽奖币是独立方向（<see cref="DraftCategory.Gold"/>），只在 <see cref="PickOne"/> 的 Gold 分支出卡。
    /// </summary>
    /// <param name="forceAtLeastRare">【2026-10-24 主人拍板】保底稀有：true 时装备生成 roll 出普通（白）强制升档稀有。默认 false，概率公示等既有调用点零改动。</param>
    static List<WeightedCard> BuildWeightedPool(DraftCategory c, bool consolation, bool forceAtLeastRare = false)
    {
        var pool = new List<WeightedCard>();
        switch (c)
        {
            case DraftCategory.Merc:
                pool.AddRange(CollectRecruitMercs());
                // 【2026-10-23 主人拍板】战斗佣兵升级暂时注释：升级卡不再进池（佣兵升级改走局外养成线）。
                // 恢复时把下面这行注释放开即可；CollectMercLevelCards 方法本体保留未动。
                //pool.AddRange(CollectMercLevelCards());
                // 【2026-10-20 主人拍板】战斗佣兵升星暂时注释：不会抽取一样的佣兵（已在本局的招募卡直接跳过），
                // 升星卡没有来源口径，先下架不再进池。恢复时把下面这行注释放开即可。
                //pool.AddRange(CollectMercStarCards());
                break;
            case DraftCategory.Equip:
                pool.AddRange(CollectEquipCards(forceAtLeastRare));
                break;
            case DraftCategory.Gold:
                pool.Add(CollectGoldCard());   // 防御：公示 / 三选一真问到 Gold 也不崩（正常抽取不走这）
                break;
            default:
                pool.AddRange(CollectNewSkills());
                pool.AddRange(CollectUpgradableSkills());
                break;
        }
        AddConsolation(pool, consolation);
        return pool;
    }

    /// <summary>
    /// 「点了抽奖按钮直接出结果」用：按权重从整池里抽 <b>1 张</b>。
    /// 概率就是「权重 ÷ 总权重」，跟概率公示弹窗上写的完全一致 ——
    /// 之前是「先造 3 张再等权选 1」，那 3 张是无放回抽的，实际概率跟权重并不相等，
    /// 公示出来就成了假数。
    /// 池子空时回落强化卡，保持「永不空手」。
    /// <para>【2026-10-19 主人拍板】Gold 方向直接出金币卡（不走加权池；
    /// 这一次抽要不要「可能」出金币方向由 <see cref="SlotMachineSystem.GoldAllowed"/> 在 RollCategory 里判）。</para>
    /// </summary>
    public static DraftCard PickOne(DraftCategory c, bool consolation)
    {
        if (c == DraftCategory.Gold) return CollectGoldCard().Card;
        // 【2026-10-24 主人拍板】保底稀有：装备方向且连续白卡到阈值 → 本次候选全部强制稀有及以上
        //（触发条件唯一出口 = SlotMachineSystem.EquipRareGuaranteeActive）。
        // BuildCards（三选一/免费路径）不走这里 —— 不保底、不计数（防白送路径刷爆 streak）。
        bool guarantee = c == DraftCategory.Equip && SlotMachineSystem.EquipRareGuaranteeActive();
        var pool = BuildWeightedPool(c, consolation, guarantee);
        var picked = new HashSet<string>();
        var one = new List<DraftCard>(1);
        if (pool.Count > 0 && TryPickWeighted(pool, 1, picked, one) && one.Count > 0)
        {
            var p = one[0];
            Debug.Log($"[DraftPool诊断] PickOne({c}) 池={pool.Count}张 → 抽到 Kind={p.Kind} Id={p.Id} HireId={p.HireId} Title={p.Title}");
            return p;
        }
        Debug.LogWarning($"[DraftPool诊断] PickOne({c}) 池空({pool.Count})，回落PowerUp");
        return BuildPowerUpCard(0);
    }

    /// <summary>概率公示的一行。<see cref="Section"/> 只有「类型」「品质」两种，弹窗按它分段。</summary>
    public struct OddsRow
    {
        public string Section;
        public string Label;
        public float Percent;
        public Color Color;
    }

    public const string SEC_TYPE = "类型";
    public const string SEC_QUALITY = "品质";

    // 品质段的固定行序：好东西排前面，玩家一眼看到「传说有多稀有」（2026-10-05 主人要的极简口径）
    // 【2026-10-18 叫法口径】末档是「抽奖币」（安慰奖进局内抽奖币池），不写「金币」。
    static readonly string[] QualityOrder = { "传说", "稀有", "普通", "抽奖币" };

    /// <summary>
    /// 概率公示弹窗的表：按**当前真实卡池现算**，不是写死的文案。
    /// 玩家解锁的技能变多、佣兵池变了，这里会跟着变 —— 玩家看到的就是真概率。
    ///
    /// <para>只给两段，主人拍板「简单点方便玩家理解」：<br/>
    /// ① 类型段：抽到 技能 / 佣兵 / 装备 / 抽奖币 各占多少（权重真源 <c>SlotMachineDefs.CategoryWeight</c>，
    ///    与 <c>SlotMachineSystem.RollCategory</c> 同一份 —— 改权重两边一起变，不会各说一套）；<br/>
    /// ② 品质段：抽到 普通 / 稀有 / 传说 各占多少（先按类型权重分层，类内再按品质权重，两层相乘）。
    ///    【2026-10-19 主人拍板】抽奖币升格为独立方向后只在类型段出现，品质段不再有「抽奖币」行
    ///    （三类池子里已无金币卡）。</para>
    ///
    /// <para>「这一抽带不带金币方向」跟实际抽取<b>用同一个口径</b>
    ///（<see cref="SlotMachineSystem.GoldAllowed"/>，唯一出口）——
    /// 引导定序窗口、保底佣兵窗口、第 1 抽、次数超限都不带，公示里也就不出现金币行；
    /// 否则「公示的概率」跟「实际抽到的概率」对不上，就是假数。</para>
    /// </summary>
    public static List<OddsRow> BuildOddsRows()
    {
        var rows = new List<OddsRow>();
        // 这一次抽的随机候选里带不带「抽奖币」方向（第 1 抽 / 引导定序 / 保底佣兵窗口 / 次数超限都不带）
        bool goldDir = SlotMachineSystem.GoldAllowed();
        var cats = BuildCategories();
        if (cats.Count == 0) return rows;

        int catTotal = 0;
        for (int i = 0; i < cats.Count; i++)
            catTotal += Mathf.Max(1, SlotMachineDefs.CategoryWeight(cats[i]));
        // 【2026-10-19 主人拍板】抽奖币是第 4 个方向（与佣兵同权重），公示分母必须把它算进去
        int goldW = goldDir ? Mathf.Max(1, SlotMachineDefs.CategoryWeight(DraftCategory.Gold)) : 0;
        int grandTotal = catTotal + goldW;
        if (grandTotal <= 0) return rows;

        // —— 第一段：抽到哪一类 ——
        for (int i = 0; i < cats.Count; i++)
        {
            var c = cats[i];
            int w = Mathf.Max(1, SlotMachineDefs.CategoryWeight(c));
            rows.Add(new OddsRow
            {
                Section = SEC_TYPE,
                Label = CategoryName(c),
                Percent = w / (float)grandTotal * 100f,
                Color = CategoryColor(c)
            });
        }
        if (goldDir)
        {
            rows.Add(new OddsRow
            {
                Section = SEC_TYPE,
                Label = "抽奖币",
                Percent = goldW / (float)grandTotal * 100f,
                Color = CategoryColor(DraftCategory.Gold)
            });
        }

        // —— 第二段：抽到什么品质 ——
        var acc = new Dictionary<string, float>();
        var colors = new Dictionary<string, Color>();
        for (int i = 0; i < cats.Count; i++)
        {
            var c = cats[i];
            float catP = Mathf.Max(1, SlotMachineDefs.CategoryWeight(c)) / (float)grandTotal;
            var pool = BuildWeightedPool(c, goldDir);
            int total = 0;
            for (int j = 0; j < pool.Count; j++)
                if (pool[j].Card.IsValid) total += Mathf.Max(1, pool[j].Weight);
            if (total <= 0) continue;

            for (int j = 0; j < pool.Count; j++)
            {
                var w = pool[j];
                if (!w.Card.IsValid) continue;
                string label = QualityBucket(w.Card);
                float cur;
                acc.TryGetValue(label, out cur);
                acc[label] = cur + catP * (Mathf.Max(1, w.Weight) / (float)total);
                colors[label] = QualityColor(w.Card);
            }
        }
        for (int i = 0; i < QualityOrder.Length; i++)
        {
            float p;
            if (!acc.TryGetValue(QualityOrder[i], out p)) continue;
            rows.Add(new OddsRow
            {
                Section = SEC_QUALITY,
                Label = QualityOrder[i],
                Percent = p * 100f,
                Color = colors[QualityOrder[i]]
            });
        }
        return rows;
    }

    /// <summary>类型段的三个名字（只此一处，弹窗不许再写一份）。</summary>
    static string CategoryName(DraftCategory c)
    {
        switch (c)
        {
            case DraftCategory.Merc: return "佣兵";
            case DraftCategory.Equip: return "装备";
            default: return "技能";
        }
    }

    static Color CategoryColor(DraftCategory c)
    {
        switch (c)
        {
            case DraftCategory.Merc: return new Color(0.55f, 0.90f, 0.70f);
            case DraftCategory.Equip: return new Color(0.98f, 0.78f, 0.42f);
            case DraftCategory.Gold: return RarityPalette.Gold;
            default: return new Color(0.62f, 0.82f, 1f);
        }
    }

    /// <summary>品质段把卡片归到哪个桶（同一桶的概率会累加）—— 只有四档，没有「史诗」。
    /// 【2026-10-18 叫法口径】钱卡这档叫「抽奖币」（与 QualityOrder 末档同串，改一处必改另一处）。</summary>
    static string QualityBucket(DraftCard card)
    {
        if (card.Kind == DraftCardKind.Gold) return "抽奖币";
        return RarityBucket(card.Rarity);
    }

    static string RarityBucket(SkillRarity r)
    {
        switch (r)
        {
            // 2026-10-05 主人拍板：没有「史诗」品质 —— 紫装 / 原史诗技能都已并进传说档，
            // 这一档不再出现在概率公示里（SkillRarity.Epic 只为旧存档留着，新代码不产出）。
            case SkillRarity.Legendary: return "传说";
            case SkillRarity.Epic: return "传说";
            case SkillRarity.Rare: return "稀有";
            default: return "普通";
        }
    }

    static Color QualityColor(DraftCard card)
    {
        // 2026-10-13 #4：金币色转发 RarityPalette.Gold（与开奖卡金币分支同源，别再抄一份）
        if (card.Kind == DraftCardKind.Gold) return RarityPalette.Gold;
        switch (card.Rarity)
        {
            case SkillRarity.Legendary: return new Color(1f, 0.62f, 0.25f);
            case SkillRarity.Epic: return new Color(1f, 0.62f, 0.25f);
            case SkillRarity.Rare: return new Color(0.42f, 0.72f, 0.98f);
            default: return new Color(0.88f, 0.88f, 0.90f);
        }
    }

    /// <summary>
    /// 【2026-10-19 主人拍板后已停用】往池子里塞安慰奖（只有金币一档）。
    /// 抽奖币已升格为**独立方向**（<see cref="DraftCategory.Gold"/>，权重 5 不随佣兵联动，
    /// 见 <see cref="SlotMachineDefs.CATEGORY_WEIGHT_GOLD"/>），不再混进任何类的池子 →
    /// 本函数不再做任何事（参数与调用点保留是为了不动六处签名）。
    /// 「这一抽出不出金币方向」唯一出口 = <see cref="SlotMachineSystem.GoldAllowed"/>。
    /// </summary>
    static void AddConsolation(List<WeightedCard> pool, bool consolation)
    {
        // 2026-10-19：金币不进池子。想恢复旧「安慰奖混池」口径在这里把 CollectGoldCard 加回 pool。
    }

    /// <summary>
    /// 安慰奖一：抽奖币（本局抽奖用的那套，进局内抽奖币池，带不出裂隙）。
    /// 【2026-10-13 #4】改「折扣」语义：Title「抽奖币 +N」/ Desc「回血 N」——
    /// 只留一个数字（「下一抽再补 N」交给面板小位，不重复出现两次）。
    /// Desc 同时是开奖卡第 3 行的文案（BattleManager.ShowDrawPrizeCard 金币分支直接取用，不另写一份）。
    /// 【2026-10-18 双钱包拆分 + 叫法口径】战斗内抽卡钱卡的玩家可见文案一律「抽奖币」，
    /// 禁止写「金币」（金币 = 城镇那笔；这笔进的是局内抽奖币池 currentGold，Title 旧写「金币」是漏改）。
    /// </summary>
    static WeightedCard CollectGoldCard()
    {
        int amount = SlotMachineSystem.GoldPrize();
        return new WeightedCard
        {
            Card = new DraftCard
            {
                Kind = DraftCardKind.Gold,
                Id = "gold",
                Title = $"抽奖币 +{amount}",
                Desc = $"回血 {amount}",
                Rarity = SkillRarity.Common,
                Tag = SynergyTag.None,
                Star = 1,
                Amount = amount,
                PowerDelta = 0
            },
            // 2026-10-19 起金币不参与池内加权（独立方向直接出卡），这权重只为满足 struct 字段，给 1 即可
            Weight = 1,
            Key = "gold"
        };
    }

    // ⚠【2026-10-05 已删除】CollectMaterialCard() —— 主人拍板「强化石不能抽奖得到」。
    // 强化石只剩铁匠铺 / 分解这两条路，抽奖池里彻底没有它。删了就删了，不留开关。

    static List<DraftCard> BuildEquipCards(bool consolation)
    {
        var cards = new List<DraftCard>(CardCount);
        if (!HasEquipContent()) return cards;

        FillFromWeighted(BuildWeightedPool(DraftCategory.Equip, consolation), cards);
        FillFallback(cards, DraftCategory.Equip);
        return cards;
    }

    /// <summary>
    /// 【2026-10-07 主人拍板】抽奖给的**武器**，攻击再高一档。
    /// 主人原话「抽奖给的武器的攻击要稍微高点，这才能体现出抽奖的重要性」——
    /// 开局那把初始武器打两波，抽出来的武器必须让人一眼看出「更强」，抽奖这件事才有分量。
    /// 🔴 只作用在<b>抽奖池里的主手武器</b>：防具、副手盾牌不动；倍率只有这一个出口，
    /// 不进 <c>EquipInstance</c>（那是所有掉落共用的生成逻辑，抽奖的加成不该污染它）。
    /// </summary>
    const float DraftWeaponAttackMul = 1.18f;

    static void BoostDraftWeaponAttack(EquipInstance eq)
    {
        // 只认主手武器槽：盾牌（副手）与防具都不算「武器」
        if (eq == null || eq.slotType != EquipSlotType.MainHand) return;
        var list = eq.attrBonus;
        if (list == null) return;
        AttrType atkAttr = PlayerJobBaseStats.CurrentAttackAttr();
        for (int i = 0; i < list.Count; i++)
        {
            var b = list[i];
            if (b == null || b.attrType != atkAttr || b.isPercent) continue;
            b.value *= DraftWeaponAttackMul;
            return;
        }
    }

    // ============================================================
    // 【2026-10-20 主人拍板「抽奖两次抽出同一个武器 数值都一样」】本局已抽中装备指纹。
    // 抽奖给出的装备在生效时记一笔（NoteDrawnEquip，唯一生效出口 = RunDraftDirector.CoApplyEquipCard），
    // 后续抽奖生成候选时避开同模板重掷洗开 —— 根因是武器模板按职业+品质档+伤害类型过滤后
    // 低章节普通档池子只有一两把，且普通品质 0 词条、主属性取整后撞车，两次抽出完全一样的武器。
    // 每局开始清零（ResetDrawnEquipTemplates，唯一调用点 = RunDraftDirector.OnRunStart 新局分支）；
    // 续关不清 —— 续关还是同一局，避开延续。
    // ============================================================

    static readonly HashSet<string> _drawnEquipTemplateIds = new HashSet<string>();

    /// <summary>每局开始清零（续关不清，同一局延续避开）。</summary>
    public static void ResetDrawnEquipTemplates() => _drawnEquipTemplateIds.Clear();

    /// <summary>装备奖励生效时记一笔（唯一调用点 = RunDraftDirector.CoApplyEquipCard）。</summary>
    public static void NoteDrawnEquip(EquipInstance eq)
    {
        if (eq == null || string.IsNullOrEmpty(eq.templateId)) return;
        _drawnEquipTemplateIds.Add(eq.templateId);
    }

    /// <summary>装备类的加权候选。生成逻辑只此一处（BuildWeightedPool 与概率公示都走它）。
    /// <param name="forceAtLeastRare">【2026-10-24 主人拍板】保底稀有透传：true 时生成的装备 roll 出普通（白）强制升档稀有（引导局不受影响，互斥判定在 RiftEquipGenerator.GenerateOne 内）。</param>
    /// </summary>
    static List<WeightedCard> CollectEquipCards(bool forceAtLeastRare = false)
    {
        var pool = new List<WeightedCard>();
        int blacksmith = TownSystem.Instance != null
            ? TownSystem.Instance.GetBuildingLevel(BuildingType.Blacksmith)
            : 1;
        StageType st = ResolveDraftStageType();

        // 2026-10-05：装备也走**加权**了（跟技能 / 佣兵同一把尺子），
        // 之前是「生成几件就直接全塞进去」，橙色跟白色一样常见 —— 主人要的
        // 「好的佣兵技能装备什么的都是低概率的」在装备这边根本没生效。
        // 2026-10-12 主人拍板：删掉「引导首抽必出蓝色及以上」的保底 reroll（原 2026-10-07 钩子2）。
        // 引导武器太强会直接秒怪，违反「前期普通装备概率大、慢慢加稀有 / 传奇」的初衷。
        // 现首抽与所有抽奖共用 hidden_level_rules.csv 概率曲线（隐藏等级 1 = 100% 普通）。

        // 2026-10-12 主人拍板「每次引导战力一样」：引导局装备卡走确定性生成
        // （恒普通 / 必主手武器 / 部位·主属性·模板钉死 / 属性取区间中值），正式关不受影响。
        bool tutorialFixed = BattleManager.Instance != null && BattleManager.Instance.IsTutorialRun;

        // 【2026-10-20 主人拍板】本批三张互不重名；同时避开本局已抽中的装备模板
        //（连续两次抽奖不再出「同一个武器数值都一样」，见 NoteDrawnEquip / ResetDrawnEquipTemplates）。
        var batchSeen = new HashSet<string>(_drawnEquipTemplateIds);

        for (int i = 0; i < CardCount; i++)
        {
            EquipInstance eq = null;
            // 2026-10-12：传奇章节门槛 + 数值章节成长需要章节号；局内构筑时 ChapterManager 仍是当前章。
            int chapter = ChapterManager.Instance != null ? ChapterManager.Instance.currentChapter : 1;
            try
            {
                // 撞同模板就重掷洗开；池子只剩同一把时最后一次强收 —— 宁可重复也不减卡、不卡流程。
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    var one = ConfigManager.Instance.GetRandomEquipInstances(
                        1, blacksmith, 0, st, GameConfig.RIFT_DROP_ATTR_BONUS, false, chapter, tutorialFixed, forceAtLeastRare);
                    if (one == null || one.Count == 0 || one[0] == null) { eq = null; break; }
                    eq = one[0];
                    bool fresh = batchSeen.Add(eq.templateId);
                    if (fresh || attempt >= 4) break;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[DraftPool] 装备卡生成失败: " + e.Message);
            }
            if (eq == null) break;
            BoostDraftWeaponAttack(eq);

            var rar = MapRarity(eq.rarity);
            pool.Add(new WeightedCard
            {
                Card = new DraftCard
                {
                    Kind = DraftCardKind.Equip,
                    Id = "equip_" + eq.templateId + "_" + i,
                    Title = string.IsNullOrEmpty(eq.equipName) ? eq.templateId : eq.equipName,
                    Desc = FormatEquipDesc(eq),
                    Rarity = rar,
                    Tag = SynergyTag.None,
                    Star = eq.star < 1 ? 1 : eq.star,
                    // 【2026-10-20 方案 B】卡面战力 = 换装真值（副本 AttrSystem 按 Hero.RecalcAttr 全链路模拟，
                    // 顶掉同槽旧件的净差）；城镇等非战斗环境内部自动回退卡面预估 EquipPowerDelta。
                    PowerDelta = PowerCalc.EquipSwapPowerDelta(eq),
                    Equip = eq
                },
                // 好装备低概率：橙 1 / 紫 3 / 蓝 6 / 白 10，与技能、佣兵同一套权重
                Weight = SkillRarityUtil.Weight(rar),
                Key = "equip_" + eq.templateId + "_" + i
            });
        }
        return pool;
    }

    /// <summary>抽卡用关卡类型：只保留精英/Boss 的品质倾斜，其余按普通关，避免休息/商人关出怪池。</summary>
    static StageType ResolveDraftStageType()
    {
        var bm = BattleManager.Instance;
        if (bm == null || bm.currentStage == null) return StageType.Normal;
        var t = bm.currentStage.type;
        if (t == StageType.Boss || t == StageType.Elite) return t;
        return StageType.Normal;
    }

    /// <summary>装备的一句话描述（部位 + 最多三条属性 + 强化等级）。替换确认弹窗复用，不另写一份。</summary>
    public static string FormatEquipDesc(EquipInstance eq)
    {
        return string.Join("　", FormatEquipDescLines(eq).ToArray());
    }

    /// <summary>
    /// 装备描述的<b>逐条</b>文本：部位 / 每条属性 / 每条传奇被动 / 强化等级，各占一个元素。
    ///
    /// 【2026-10-12 主人拍板】「装备的属性按一个属性一行，竖着往下排，不要混在一起看的不舒服」
    /// → <see cref="EquipReplaceConfirmUI"/> 用 <c>"\n"</c> 拼接、一条一行竖排；
    /// 抽卡卡面等「一句话」场景仍用全角空格拼接（<see cref="FormatEquipDesc"/>）。
    /// <b>条目内容只有这一处真源，两处只差分隔符，不许各写各的。</b>
    /// </summary>
    /// <summary>
    /// 装备描述的<b>逐条</b>文本：部位 / 每条属性 / 每条传奇被动 / 强化等级，各占一个元素。
    /// <paramref name="oldForCompare"/> 非空时（替换确认弹窗「新装备」栏）：同属性前加 ▲/▼ 对比箭头。
    /// 其它调用方（抽卡卡面、装备掉落弹窗）保持原样——不传即不加箭头。
    /// </summary>
    public static List<string> FormatEquipDescLines(EquipInstance eq, EquipInstance oldForCompare = null)
    {
        var lines = new List<string>();
        lines.Add(EquipUiText.Slot(eq.slotType));
        int shown = 0;

        // 旧件属性表（attrType → 数值），仅用于新装备同属性对比箭头
        var oldByType = new System.Collections.Generic.Dictionary<AttrType, AttrBonusData>();
        if (oldForCompare != null)
        {
            if (oldForCompare.attrBonus != null)
                foreach (var o in oldForCompare.attrBonus)
                    if (o != null && !oldByType.ContainsKey(o.attrType)) oldByType[o.attrType] = o;
            if (oldForCompare.globalBonus != null && !oldByType.ContainsKey(oldForCompare.globalBonus.attrType))
                oldByType[oldForCompare.globalBonus.attrType] = oldForCompare.globalBonus;
        }

        if (eq.attrBonus != null)
        {
            for (int i = 0; i < eq.attrBonus.Count && shown < 3; i++)
            {
                var a = eq.attrBonus[i];
                if (a == null) continue;
                lines.Add(CompareAttrLine(a, oldByType));
                shown++;
            }
        }
        if (shown == 0 && eq.globalBonus != null)
        {
            lines.Add(CompareAttrLine(eq.globalBonus, oldByType));
            shown = 1;
        }
        // 2026-10-09 主人拍板：传奇被动要在 UI 上看得见。
        // skillPassives 的数值早就由 EquipStatRollup.AppendSkillPassives 生效了，这里只补文字。
        // 没有被动就一个字都不加（不显示「【被动】无」）。
        // 数值渲染与 EquipStatTipUI / EquipDropPopupUI 同一套口径（2026-10-09 起统一走 EquipUiText.Value）。
        // 【2026-10-12】每条各占一个元素，由调用方决定分隔符：一句话场景全角空格、竖排场景换行。
        if (eq.skillPassives != null)
        {
            for (int i = 0; i < eq.skillPassives.Count; i++)
            {
                var p = eq.skillPassives[i];
                if (p == null) continue;
                string pv = EquipUiText.Value(p.attrType, p.value, p.isPercent);
                lines.Add("【被动】" + EquipUiText.Attr(p.attrType) + " " + pv);
                shown++;
            }
        }
        if (shown == 0) lines.Add("无附加属性");
        if (eq.enhanceLevel > 0) lines.Add("+" + eq.enhanceLevel);
        return lines;
    }

    /// <summary>
    /// 单条属性文案：若旧件有<b>同属性</b>且口径一致（是否百分比相同），按数值涨跌前缀
    /// ▲（增长，绿）/▼（降低，红）；持平或无法对比则不加箭头。颜色用固定绿/红，不依赖外部调色板。
    /// </summary>
    static string CompareAttrLine(AttrBonusData a, System.Collections.Generic.Dictionary<AttrType, AttrBonusData> oldByType)
    {
        string arrow = "";
        if (a != null && oldByType.TryGetValue(a.attrType, out var oa) && a.isPercent == oa.isPercent)
        {
            if (a.value > oa.value + 1e-4f)
                arrow = "<color=#3FD66B>▲</color> ";
            else if (a.value < oa.value - 1e-4f)
                arrow = "<color=#FF6B6B>▼</color> ";
        }
        return arrow + EquipUiText.Attr(a.attrType) + FormatAttrValue(a);
    }

    static string FormatAttrValue(AttrBonusData a)
    {
        if (a == null) return "";
        // 2026-10-09 主人拍板：比例属性（isPercent=false + 小数值）走百分比口径，
        // 否则吸血 0.05 会被 Mathf.RoundToInt 抹成「+0」。百分号/整数分支保持原有取整口径不动。
        if (!a.isPercent && EquipUiText.IsRateAttr(a.attrType))
            return "+" + EquipUiText.Value(a.attrType, a.value, false);
        if (a.isPercent) return "+" + Mathf.RoundToInt(a.value * 100f) + "%";
        // 【2026-10-19 主人拍板】整数/小数兜底改走 EquipUiText.Value 统一口径：
        // 输出与旧 RoundToInt 完全一致，但火/冰（点口径）在这里拿到专属显示「+8.4%」，
        // 不再被 RoundToInt 抹成「+8」丢掉百分号。
        return "+" + EquipUiText.Value(a.attrType, a.value, false);
    }

    // ============================================================
    // 候选收集：技能
    // ============================================================

    struct WeightedCard
    {
        public DraftCard Card;
        public int Weight;
        public string Key;
    }

    static List<WeightedCard> CollectUpgradableSkills()
    {
        var list = new List<WeightedCard>();
        if (RunLoadout.Data?.skills == null) return list;

        for (int i = 0; i < RunLoadout.Data.skills.Count; i++)
        {
            var s = RunLoadout.Data.skills[i];
            if (s == null || string.IsNullOrEmpty(s.id)) continue;
            var rar = SkillDraftMeta.Rarity(s.id);
            if (s.star >= SkillRarityUtil.StarCap(rar)) continue;

            var def = PlayerSkillDefs.GetById(s.id);
            int nextStar = s.star + 1;
            string tagName = SynergyTagUtil.DisplayName(SkillDraftMeta.Tag(s.id));
            string prefix = string.IsNullOrEmpty(tagName) ? "" : $"[{tagName}] ";

            list.Add(new WeightedCard
            {
                Card = new DraftCard
                {
                    Kind = DraftCardKind.SkillUp,
                    Id = s.id,
                    Title = def != null ? def.displayName : s.id,
                    Desc = $"{prefix}★{s.star} → ★{nextStar}　伤害 +28%，冷却 -8%",
                    Rarity = rar,
                    Tag = SkillDraftMeta.Tag(s.id),
                    Star = nextStar,
                    IsAffinity = SkillDraftMeta.IsAffinity(s.id, PlayerJobDefs.GetSelected()),
                    PowerDelta = 140
                },
                Weight = SkillRarityUtil.Weight(rar) * 2,
                Key = "skill_" + s.id
            });
        }
        return list;
    }

    static List<WeightedCard> CollectNewSkills()
    {
        var list = new List<WeightedCard>();
        if (RunLoadout.IsSkillFull) return list;

        // 2026-10-12 主人拍板「每次引导战力一样」：引导局第三拍固定发 SlotMachineDefs.TutorialFixedSkillId
        // （治愈之泉，Common 档）—— 否则随机技能的稀有度差会让引导战力每次不同。
        // 取不到定义 / 不在抽卡池 → LogError 并回落下面的随机池（绝不静默、不卡流程）。
        if (BattleManager.Instance != null && BattleManager.Instance.IsTutorialRun)
        {
            var fixedDef = PlayerSkillDefs.GetById(SlotMachineDefs.TutorialFixedSkillId);
            if (fixedDef != null && SkillDraftMeta.InDraftPool(fixedDef.id))
            {
                var fixedRar = SkillDraftMeta.Rarity(fixedDef.id);
                var fixedTag = SkillDraftMeta.Tag(fixedDef.id);
                string fixedTagName = SynergyTagUtil.DisplayName(fixedTag);
                string fixedPrefix = string.IsNullOrEmpty(fixedTagName) ? "" : $"[{fixedTagName}] ";
                string fixedBody = string.IsNullOrEmpty(fixedDef.numbers) ? fixedDef.desc : fixedDef.numbers;
                list.Add(new WeightedCard
                {
                    Card = new DraftCard
                    {
                        Kind = DraftCardKind.SkillNew,
                        Id = fixedDef.id,
                        Title = fixedDef.displayName,
                        Desc = fixedPrefix + fixedBody,
                        Rarity = fixedRar,
                        Tag = fixedTag,
                        Star = 1,
                        IsAffinity = SkillDraftMeta.IsAffinity(fixedDef.id, PlayerJobDefs.GetSelected()),
                        PowerDelta = PowerCalc.SkillPowerDelta(fixedRar, 1)
                    },
                    Weight = 1,
                    Key = "skill_" + fixedDef.id
                });
                return list;
            }
            Debug.LogError($"[DraftPool] 引导固定技能 {SlotMachineDefs.TutorialFixedSkillId} 取不到定义或不在抽卡池，回落随机技能池");
        }

        var all = PlayerSkillDefs.All;
        if (all == null) return list;

        for (int i = 0; i < all.Length; i++)
        {
            var def = all[i];
            if (def == null || string.IsNullOrEmpty(def.id)) continue;
            if (RunLoadout.HasSkill(def.id)) continue;
            if (!SkillDraftMeta.InDraftPool(def.id)) continue;
            // 分层解锁（2026-09-15）：未解锁的技能不进局内抽卡池。
            // 前期只会遇到 Early 那 10 个，中后期技能靠章节 / 天赋 / 商店 / 成就逐步放出。
            // 详见 Docs/玩家技能分层解锁_2026-09-15.md
            if (!PlayerSkillDefs.IsUnlocked(def, SaveSystem.Instance?.Data)) continue;
            // 近战专属：远程职业（游侠/法师/牧师）抽不到冲锋、剑刃风暴这类贴身技。
            // 只过滤语义上过不去的少数几个，其余技能对全职业通用。
            if (def.meleeOnly && !PlayerJobDefs.IsSelectedMelee()) continue;

            var rar = SkillDraftMeta.Rarity(def.id);
            var tag = SkillDraftMeta.Tag(def.id);
            string tagName = SynergyTagUtil.DisplayName(tag);
            string prefix = string.IsNullOrEmpty(tagName) ? "" : $"[{tagName}] ";
            string body = string.IsNullOrEmpty(def.numbers) ? def.desc : def.numbers;

            list.Add(new WeightedCard
            {
                Card = new DraftCard
                {
                    Kind = DraftCardKind.SkillNew,
                    Id = def.id,
                    Title = def.displayName,
                    Desc = prefix + body,
                    Rarity = rar,
                    Tag = tag,
                    Star = 1,
                    IsAffinity = SkillDraftMeta.IsAffinity(def.id, PlayerJobDefs.GetSelected()),
                    PowerDelta = PowerCalc.SkillPowerDelta(rar, 1)
                },
                Weight = SkillRarityUtil.Weight(rar),
                Key = "skill_" + def.id
            });
        }
        return list;
    }

    // ============================================================
    // 候选收集：佣兵
    // ============================================================

    static List<WeightedCard> CollectRecruitMercs()
    {
        var list = new List<WeightedCard>();
        if (RunLoadout.IsMercFull) return list;

        SaveData data = SaveSystem.Instance?.Data;

        // 【2026-10-19 主人拍板「战斗里招募的佣兵只能抽出酒馆上架队列里的」】
        // 队列空 = 没有招募卡（不回落全池，覆盖 2026-10-13「空池不筛选」口径）。
        // 空池实际只会发生在「还没解锁任何佣兵」（解锁即 EnsurePoolFilled 自动补满队列，
        // 见 SaveData.EnsurePoolFilled / MailSystem）→ 全池同样为空，行为等价，不会卡死新手。
        if (data == null || data.mercDrawPool == null || data.mercDrawPool.Count == 0)
            return list;

        int guild = data.guildLevel;
        int heroLv = RunLoadout.HeroLevel;
        // 【2026-10-17 主人拍板 + 2026-10-19 只从队列出】队列内每个上架佣兵单独等概率（权重全 1），
        // 旧的「空池回落全池按稀有度加权」已随空池回落一起作废（见上方「队列空 = 没有招募卡」注释）。

        // 【2026-10-19 诊断】玩家报「战斗里只能抽出队列中第一个佣兵」，打出池子实际内容定位是「池里只剩一个」还是「随机没生效」
        var dbgQueue = new System.Text.StringBuilder();
        foreach (string q in data.mercDrawPool) { if (dbgQueue.Length > 0) dbgQueue.Append(','); dbgQueue.Append(q); }
        var dbgUnlocked = new System.Text.StringBuilder();
        foreach (string u in data.unlockedMercIds) { if (dbgUnlocked.Length > 0) dbgUnlocked.Append(','); dbgUnlocked.Append(u); }
        Debug.Log($"[DraftPool诊断] 队列={dbgQueue} 解锁={dbgUnlocked} 公会Lv={guild} 英雄Lv={heroLv} 队列槽={data.DrawPoolSlots}");

        foreach (string hireId in data.unlockedMercIds)
        {
            if (string.IsNullOrEmpty(hireId)) continue;
            // 【2026-10-13 主人拍板】佣兵抽奖池：在酒馆勾过池子就只从勾选的里面出（空池 = 不筛选，维持全池加权）
            if (!data.IsMercInDrawPool(hireId)) { Debug.Log($"[DraftPool诊断] {hireId} 不在队列，跳过"); continue; }
            if (!MercRosterDefs.TryGetByHireId(hireId, out var def)) { Debug.Log($"[DraftPool诊断] {hireId} 花名册查不到，跳过"); continue; }
            if (string.IsNullOrEmpty(def.AssetId)) { Debug.Log($"[DraftPool诊断] {hireId} 无AssetId，跳过"); continue; }
            if (RunLoadout.HasMerc(def.HireId) || RunLoadout.HasMerc(def.AssetId)) { Debug.Log($"[DraftPool诊断] {hireId} 已在本局，跳过"); continue; }

            MercTier tier = GameConfig.GetMercTier(def.AssetId);
            if (tier == MercTier.Npc) { Debug.Log($"[DraftPool诊断] {hireId} 是NPC，跳过"); continue; }
            // 【2026-10-20 主人拍板】只要在队列中的佣兵，无条件进战斗抽奖池，差别只有抽中概率 ——
            // 原「高级佣兵要公会Lv5」的门槛整行删除。它就是「战斗里只能抽出队列第一两个」的主因：
            // 塔克 H003 的 AssetId 是 dunbing201（尾号2开头=高级档），公会不满5级时被这道门
            // 永久拦在池外；再叠加「已在本局」过滤把已招募的逐个剔除，池子中期就只剩一两个，
            // 看起来就像「永远抽中队列第一个」。（城镇出战队伍的 IsMercAvailable 门槛不受影响，那是另一条规则。）

            // 【2026-10-22 主人拍板 T13】战斗内抽到已拥有佣兵 → 局内星级 = 该佣兵 outStar（0 星/未拥有
            // 直接用现 ★1 档，MpProfile/PowerCalc 均 max(1,star) 天然对齐）；旧「按稀有度定 3/2/1 星」作废。
            // 局内 star（1~5）与 RunLoadout.MaxMercStar 体系不动，只改入场初值；攻防加成由 T8 按 outStar 另乘。
            int star = MercStarSystem.InRunStarOf(def.HireId);
            int level = Mathf.Max(1, 1 + heroLv / 2);
            // 【2026-10-19 主人拍板】只能从上架队列出 → 每名上架佣兵单独等概率（权重全 1），
            // 旧的「空池全池按稀有度加权（普通62/稀有33/传奇5）」随空池回落一起作废。
            int weight = 1;
            // 2026-09-17 稀有度映射统一走 SkillRarityUtil（三档对三档，佣兵不出「史诗」），
            // 卡面文字也随之统一，别再各写一份三目。
            SkillRarity rar = SkillRarityUtil.FromMerc(def.Rarity);
            string rarName = SkillRarityUtil.DisplayName(rar);

            list.Add(new WeightedCard
            {
                Card = new DraftCard
                {
                    Kind = DraftCardKind.MercRecruit,
                    Id = def.AssetId,
                    HireId = def.HireId,
                    // 【2026-10-10 主人拍板】卡面只写真名（塔克），不带花名（重盾）——
                    // 主人原话「佣兵的名字要用塔克不要用重盾」。
                    Title = def.Name,
                    // 【2026-10-11 主人拍板】同上（SlotMachineSystem.BuildGuaranteedMercCard 那一处）：
                    // 佣兵名字只取真名，描述行也不再写职业名（旧写法会出现「[普通] 游侠」这种
                    // 「名字栏写着职业」的错位，主人反馈「不要取职业名字 剑盾卫士什么的」）。
                    // 【2026-10-24 主人拍板】局内佣兵没有升星体系（后面再开放），卡面只显示等级数字，
                    // 不写 ★N 与局外升星混淆；star 仍按 InRunStarOf 供战力计算（显示不露出）。
                    Desc = $"[{rarName}]　Lv{level}",
                    Rarity = rar,
                    Tag = SynergyTag.Summon,
                    Star = star,
                    MercLevel = level,
                    IsAffinity = false,
                    PowerDelta = PowerCalc.MercPowerDelta(level, star)
                },
                Weight = weight,
                Key = "merc_" + def.HireId
            });
            Debug.Log($"[DraftPool诊断] {hireId}({def.Name}) 入池，权重={weight}");
        }
        var dbgPool = new System.Text.StringBuilder();
        for (int i = 0; i < list.Count; i++) { if (dbgPool.Length > 0) dbgPool.Append(','); dbgPool.Append(list[i].Key); }
        Debug.Log($"[DraftPool诊断] 招募池最终={list.Count}个 [{dbgPool}]");
        return list;
    }

    static List<WeightedCard> CollectMercLevelCards()
    {
        var list = new List<WeightedCard>();
        var mercs = RunLoadout.Mercs();
        for (int i = 0; i < mercs.Count; i++)
        {
            var m = mercs[i];
            if (m == null || string.IsNullOrEmpty(m.mercId)) continue;
            string key = m.hireId ?? m.mercId;
            int next = Mathf.Max(1, m.level) + 1;

            list.Add(new WeightedCard
            {
                Card = new DraftCard
                {
                    Kind = DraftCardKind.MercLevelUp,
                    Id = key,
                    HireId = m.hireId,
                    Title = m.displayName,
                    Desc = $"等级 Lv{m.level} → Lv{next}　生命/攻击成长",
                    Rarity = SkillRarityUtil.FromMercStar(m.star),
                    Tag = SynergyTag.Summon,
                    Star = Mathf.Max(1, m.star),
                    MercLevel = next,
                    // 升 1 级的战力增量：用同一套 MercPowerDelta 求差，保证与 RunLoadout 入账口径一致
                    PowerDelta = PowerCalc.MercPowerDelta(next, Mathf.Max(1, m.star))
                              - PowerCalc.MercPowerDelta(Mathf.Max(1, m.level), Mathf.Max(1, m.star))
                },
                Weight = 8,
                Key = "merc_lv_" + key
            });
        }
        return list;
    }

    static List<WeightedCard> CollectMercStarCards()
    {
        var list = new List<WeightedCard>();
        var mercs = RunLoadout.Mercs();
        for (int i = 0; i < mercs.Count; i++)
        {
            var m = mercs[i];
            if (m == null || string.IsNullOrEmpty(m.mercId)) continue;
            int star = Mathf.Max(1, m.star);
            if (star >= 5) continue;
            string key = m.hireId ?? m.mercId;

            list.Add(new WeightedCard
            {
                Card = new DraftCard
                {
                    Kind = DraftCardKind.MercStarUp,
                    Id = key,
                    HireId = m.hireId,
                    Title = m.displayName,
                    Desc = $"★{star} → ★{star + 1}　星级 +1，同时等级 +1",
                    Rarity = SkillRarityUtil.FromMercStar(star + 1),
                    Tag = SynergyTag.Summon,
                    Star = star + 1,
                    MercLevel = Mathf.Max(1, m.level) + 1,
                    // 升 1 星且升 1 级的战力增量：用同一套 MercPowerDelta 求差，保证与 RunLoadout 入账口径一致
                    PowerDelta = PowerCalc.MercPowerDelta(Mathf.Max(1, m.level) + 1, star + 1)
                              - PowerCalc.MercPowerDelta(Mathf.Max(1, m.level), star)
                },
                Weight = 4,
                Key = "merc_star_" + key
            });
        }
        return list;
    }

    // 2026-09-17 删除私有 MercRarityToSkillRarity(star)：它与 MercSkillMapping.StarToRarity 口径不一致
    // （把 ★4 判成 Epic、★2 判成 Rare），导致同一佣兵图鉴是「稀有」、抽卡卡面是「史诗」。
    // 现统一走 SkillRarityUtil.FromMercStar。

    /// <summary>装备品质 → 卡牌稀有度。对外公开：替换确认弹窗要显示品质名，不能自己再写一份映射。</summary>
    /// <para>2026-10-05 主人拍板「没有史诗品质」→ 卡面统一三档 普通 / 稀有 / 传说，
    /// 紫装（<c>Rarity.Epic</c>）并入传说档，跟技能那边 5 张原史诗技能并进传说是同一把尺子。
    /// 要改成并进「稀有」就改这一行，别处没有第二份映射。
    /// （抽奖池真源 RiftEquipGenerator 本来就只出 普通/稀有/传说 三档，Epic 只有表没加载的回退路径才会碰到。）</para>
    public static SkillRarity MapRarity(Rarity r)
    {
        switch (r)
        {
            case Rarity.Legendary:
            case Rarity.Epic: return SkillRarity.Legendary;
            case Rarity.Rare:
            case Rarity.Uncommon: return SkillRarity.Rare;
            default: return SkillRarity.Common;
        }
    }

    // ============================================================
    // 通用强化卡（兜底，永不空手）
    // ============================================================

    /// <summary>兜底三选一（任何方向都抽不出来时用）。</summary>
    public static List<DraftCard> BuildFallbackCards()
    {
        var cards = new List<DraftCard>(CardCount);
        FillFallback(cards, DraftCategory.Skill);
        return cards;
    }

    /// <summary>兜底强化卡，按已被占用的数量错开，避免同一次三张全一样。</summary>
    static void FillFallback(List<DraftCard> cards, DraftCategory cat)
    {
        int guard = 0;
        while (cards.Count < CardCount && guard++ < 8)
            cards.Add(BuildPowerUpCard(cards.Count));
    }

    static DraftCard BuildPowerUpCard(int index)
    {
        switch (index % 3)
        {
            case 1:
                return PowerUp("pow_hp", "生命强化", "本局生命上限 +12%", SynergyTag.Guard, 120);
            case 2:
                return PowerUp("pow_spd", "疾行强化", "本局攻速 +10%", SynergyTag.Combo, 140);
            default:
                return PowerUp("pow_atk", "力量强化", "本局攻击 +14%", SynergyTag.Fire, 160);
        }
    }

    static DraftCard PowerUp(string id, string title, string desc, SynergyTag tag, int powerDelta)
    {
        return new DraftCard
        {
            Kind = DraftCardKind.PowerUp,
            Id = id,
            Title = title,
            Desc = desc,
            Rarity = SkillRarity.Common,
            Tag = tag,
            Star = 1,
            PowerDelta = powerDelta
        };
    }

    // ============================================================
    // 加权抽取
    // ============================================================

    static void FillFromWeighted(List<WeightedCard> pool, List<DraftCard> outCards)
    {
        var picked = new HashSet<string>();
        int guard = 0;
        while (outCards.Count < CardCount && pool.Count > 0 && guard++ < 12)
        {
            if (!TryPickWeighted(pool, 1, picked, outCards)) break;
        }
    }

    static bool TryPickWeighted(List<WeightedCard> pool, int weightMul, HashSet<string> picked, List<DraftCard> outCards)
    {
        if (pool == null || pool.Count == 0) return false;
        int mul = weightMul < 1 ? 1 : weightMul;

        int total = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            if (!pool[i].Card.IsValid) continue;
            if (picked.Contains(pool[i].Key)) continue;
            total += Mathf.Max(1, pool[i].Weight * mul);
        }
        if (total <= 0) return false;

        int roll = Random.Range(0, total);
        var dbgItems = new System.Text.StringBuilder();
        for (int i = 0; i < pool.Count; i++)
        {
            if (!pool[i].Card.IsValid || picked.Contains(pool[i].Key)) continue;
            if (dbgItems.Length > 0) dbgItems.Append(',');
            dbgItems.Append($"{pool[i].Key}(w{Mathf.Max(1, pool[i].Weight * mul)})");
        }
        Debug.Log($"[DraftPool诊断] TryPickWeighted total={total} roll={roll} 候选=[{dbgItems}]");
        for (int i = 0; i < pool.Count; i++)
        {
            if (picked.Contains(pool[i].Key)) continue;
            if (!pool[i].Card.IsValid) continue;
            roll -= Mathf.Max(1, pool[i].Weight * mul);
            if (roll < 0)
            {
                Debug.Log($"[DraftPool诊断] 命中 #{i} {pool[i].Key}");
                picked.Add(pool[i].Key);
                outCards.Add(pool[i].Card);
                return true;
            }
        }
        // 浮点边界兜底：取第一个未抽中的
        for (int i = 0; i < pool.Count; i++)
        {
            if (picked.Contains(pool[i].Key)) continue;
            if (!pool[i].Card.IsValid) continue;
            Debug.LogWarning($"[DraftPool诊断] roll边界兜底，取第一个未抽中 #{i} {pool[i].Key}");
            picked.Add(pool[i].Key);
            outCards.Add(pool[i].Card);
            return true;
        }
        return false;
    }
}
