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
        if (CollectMercLevelCards().Count > 0) return true;
        return CollectMercStarCards().Count > 0;
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
    /// 该类的三选一（含安慰奖）。
    /// </summary>
    public static List<DraftCard> BuildCards(DraftCategory c) => BuildCards(c, true);

    /// <param name="consolation">
    /// 要不要把「金币」安慰奖放进池子。（2026-10-05：强化石那档主人拍板删了，只剩金币一档）
    /// 2026-10-05：<b>引导局定序那几抽必须传 false</b> —— 主人拍板「第一次装备、第二次佣兵、第三次技能」，
    /// 若安慰奖混进去，玩家花第一抽的钱抽到一堆币，教学就白教了（保底不用写出来，但得真的给到）。
    /// 正式关恒传 true —— 主人拍板「正式关应该都是随机的」，安慰奖从第 1 抽起就在池里。
    /// 免费老虎机、通关三选一这类「白送的」继续带安慰奖。
    /// </param>
    public static List<DraftCard> BuildCards(DraftCategory c, bool consolation)
    {
        switch (c)
        {
            case DraftCategory.Merc: return BuildMercCards(consolation);
            case DraftCategory.Equip: return BuildEquipCards(consolation);
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
    /// </summary>
    static List<WeightedCard> BuildWeightedPool(DraftCategory c, bool consolation)
    {
        var pool = new List<WeightedCard>();
        switch (c)
        {
            case DraftCategory.Merc:
                pool.AddRange(CollectRecruitMercs());
                pool.AddRange(CollectMercLevelCards());
                pool.AddRange(CollectMercStarCards());
                break;
            case DraftCategory.Equip:
                pool.AddRange(CollectEquipCards());
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
    /// </summary>
    public static DraftCard PickOne(DraftCategory c, bool consolation)
    {
        var pool = BuildWeightedPool(c, consolation);
        var picked = new HashSet<string>();
        var one = new List<DraftCard>(1);
        if (pool.Count > 0 && TryPickWeighted(pool, 1, picked, one) && one.Count > 0)
            return one[0];
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
    static readonly string[] QualityOrder = { "传说", "稀有", "普通", "金币" };

    /// <summary>
    /// 概率公示弹窗的表：按**当前真实卡池现算**，不是写死的文案。
    /// 玩家解锁的技能变多、佣兵池变了，这里会跟着变 —— 玩家看到的就是真概率。
    ///
    /// <para>只给两段，主人拍板「简单点方便玩家理解」：<br/>
    /// ① 类型段：抽到 技能 / 佣兵 / 装备 各占多少（权重真源 <c>SlotMachineDefs.CategoryWeight</c>，
    ///    与 <c>SlotMachineSystem.RollCategory</c> 同一份 —— 改权重两边一起变，不会各说一套）；<br/>
    /// ② 品质段：抽到 普通 / 稀有 / 传说 / 金币 各占多少（先按类型权重分层，类内再按品质权重，两层相乘）。</para>
    ///
    /// <para>2026-10-05：安慰奖带不带，跟 <c>PickOne</c> 这一次抽<b>用同一个口径</b>
    ///（<c>SlotMachineSystem.InGuaranteeWindow()</c>）—— 引导局那几抽不带安慰奖，公示里也就不能出现金币；
    /// 否则「公示的概率」跟「实际抽到的概率」对不上，就是假数。</para>
    /// </summary>
    public static List<OddsRow> BuildOddsRows()
    {
        var rows = new List<OddsRow>();
        bool consolation = !SlotMachineSystem.InGuaranteeWindow();
        var cats = BuildCategories();
        if (cats.Count == 0) return rows;

        int catTotal = 0;
        for (int i = 0; i < cats.Count; i++)
            catTotal += Mathf.Max(1, SlotMachineDefs.CategoryWeight(cats[i]));
        if (catTotal <= 0) return rows;

        // —— 第一段：抽到哪一类 ——
        for (int i = 0; i < cats.Count; i++)
        {
            var c = cats[i];
            int w = Mathf.Max(1, SlotMachineDefs.CategoryWeight(c));
            rows.Add(new OddsRow
            {
                Section = SEC_TYPE,
                Label = CategoryName(c),
                Percent = w / (float)catTotal * 100f,
                Color = CategoryColor(c)
            });
        }

        // —— 第二段：抽到什么品质 ——
        var acc = new Dictionary<string, float>();
        var colors = new Dictionary<string, Color>();
        for (int i = 0; i < cats.Count; i++)
        {
            var c = cats[i];
            float catP = Mathf.Max(1, SlotMachineDefs.CategoryWeight(c)) / (float)catTotal;
            var pool = BuildWeightedPool(c, consolation);
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
            default: return new Color(0.62f, 0.82f, 1f);
        }
    }

    /// <summary>品质段把卡片归到哪个桶（同一桶的概率会累加）—— 只有四档，没有「史诗」。</summary>
    static string QualityBucket(DraftCard card)
    {
        if (card.Kind == DraftCardKind.Gold) return "金币";
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
        if (card.Kind == DraftCardKind.Gold) return new Color(1f, 0.85f, 0.35f);
        switch (card.Rarity)
        {
            case SkillRarity.Legendary: return new Color(1f, 0.62f, 0.25f);
            case SkillRarity.Epic: return new Color(1f, 0.62f, 0.25f);
            case SkillRarity.Rare: return new Color(0.42f, 0.72f, 0.98f);
            default: return new Color(0.88f, 0.88f, 0.90f);
        }
    }

    /// <summary>往池子里塞安慰奖（只有金币一档）。权重见 <see cref="SlotMachineDefs"/>。</summary>
    static void AddConsolation(List<WeightedCard> pool, bool consolation)
    {
        if (!consolation) return;
        pool.Add(CollectGoldCard());
    }

    /// <summary>安慰奖一：金币（= 抽奖币，本局抽奖用的那套）。</summary>
    static WeightedCard CollectGoldCard()
    {
        int amount = SlotMachineSystem.GoldPrize();
        return new WeightedCard
        {
            Card = new DraftCard
            {
                Kind = DraftCardKind.Gold,
                Id = "gold",
                Title = $"金币 ×{amount}",
                Desc = "抽奖币回血 —— 接着抽！",
                Rarity = SkillRarity.Common,
                Tag = SynergyTag.None,
                Star = 1,
                Amount = amount,
                PowerDelta = 0
            },
            Weight = SlotMachineDefs.WEIGHT_GOLD,
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

    /// <summary>装备类的加权候选。生成逻辑只此一处（BuildWeightedPool 与概率公示都走它）。</summary>
    static List<WeightedCard> CollectEquipCards()
    {
        var pool = new List<WeightedCard>();
        int blacksmith = TownSystem.Instance != null
            ? TownSystem.Instance.GetBuildingLevel(BuildingType.Blacksmith)
            : 1;
        StageType st = ResolveDraftStageType();

        // 2026-10-05：装备也走**加权**了（跟技能 / 佣兵同一把尺子），
        // 之前是「生成几件就直接全塞进去」，橙色跟白色一样常见 —— 主人要的
        // 「好的佣兵技能装备什么的都是低概率的」在装备这边根本没生效。
        // 【钩子2·2026-10-07】引导局第一抽（DrawCount==0）装备必出蓝色及以上（reroll 白色）：
        // 让玩家下一波立刻验证「变强」，喂首次留存（方案见 Docs/引导流程优化建议_2026-10-07.md）。
        // reroll 上限 8 次：blacksmith 低时可能多白，出不来到保底不空池，用最后一次。
        bool guaranteeRare = BattleManager.Instance != null
            && BattleManager.Instance.IsTutorialRun
            && RunLoadout.DrawCount == 0;

        for (int i = 0; i < CardCount; i++)
        {
            EquipInstance eq = null;
            int rareRoll = 0;
            do
            {
                try
                {
                    var one = ConfigManager.Instance.GetRandomEquipInstances(
                        1, blacksmith, 0, st, GameConfig.RIFT_DROP_ATTR_BONUS);
                    if (one != null && one.Count > 0) eq = one[0];
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[DraftPool] 装备卡生成失败: " + e.Message);
                }
                if (eq == null) break;
                rareRoll++;
            } while (guaranteeRare && MapRarity(eq.rarity) == SkillRarity.Common && rareRoll < 8);
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
                    PowerDelta = (int)eq.rarity * 60 + Mathf.Max(1, eq.star) * 80
                                 + (eq.attrBonus != null ? eq.attrBonus.Count : 0) * 30,
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
        var sb = new StringBuilder();
        sb.Append(EquipUiText.Slot(eq.slotType));
        int shown = 0;
        if (eq.attrBonus != null)
        {
            for (int i = 0; i < eq.attrBonus.Count && shown < 3; i++)
            {
                var a = eq.attrBonus[i];
                if (a == null) continue;
                sb.Append("　").Append(EquipUiText.Attr(a.attrType)).Append(FormatAttrValue(a));
                shown++;
            }
        }
        if (shown == 0 && eq.globalBonus != null)
        {
            sb.Append("　").Append(EquipUiText.Attr(eq.globalBonus.attrType))
              .Append(FormatAttrValue(eq.globalBonus));
            shown = 1;
        }
        if (shown == 0) sb.Append("　无附加属性");
        if (eq.enhanceLevel > 0) sb.Append("　+").Append(eq.enhanceLevel);
        return sb.ToString();
    }

    static string FormatAttrValue(AttrBonusData a)
    {
        if (a == null) return "";
        if (a.isPercent) return "+" + Mathf.RoundToInt(a.value * 100f) + "%";
        return "+" + Mathf.RoundToInt(a.value);
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
                    PowerDelta = 90 + (int)rar * 90
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

        // 2026-09-14 改版：招募池 = 酒馆已解锁的佣兵，不再是全量花名册。
        // 酒馆只负责解锁，真正的招募挪进战斗内三选一。
        if (data == null || data.unlockedMercIds == null || data.unlockedMercIds.Count == 0)
            return list;

        int guild = data.guildLevel;
        int heroLv = RunLoadout.HeroLevel;

        foreach (string hireId in data.unlockedMercIds)
        {
            if (string.IsNullOrEmpty(hireId)) continue;
            if (!MercRosterDefs.TryGetByHireId(hireId, out var def)) continue;
            if (string.IsNullOrEmpty(def.AssetId)) continue;
            if (RunLoadout.HasMerc(def.HireId) || RunLoadout.HasMerc(def.AssetId)) continue;

            MercTier tier = GameConfig.GetMercTier(def.AssetId);
            if (tier == MercTier.Npc) continue;
            // 高级佣兵要公会等级，局内同理（不联网也保留成长动机）
            if (tier == MercTier.Advanced && guild < GameConfig.ADVANCED_MERC_GUILD_LEVEL) continue;

            int star = def.Rarity == MercRosterDefs.MercRarity.Legendary ? 3
                : def.Rarity == MercRosterDefs.MercRarity.Rare ? 2 : 1;
            int level = Mathf.Max(1, 1 + heroLv / 2);
            int weight = def.Rarity == MercRosterDefs.MercRarity.Legendary ? 2
                : def.Rarity == MercRosterDefs.MercRarity.Rare ? 5 : 8;
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
                    Title = $"{def.Name}·{def.Nickname}",
                    Desc = $"[{rarName}] {def.JobName}　Lv{level}　★{star}",
                    Rarity = rar,
                    Tag = SynergyTag.Summon,
                    Star = star,
                    MercLevel = level,
                    IsAffinity = false,
                    PowerDelta = level * 70 + star * 110
                },
                Weight = weight,
                Key = "merc_" + def.HireId
            });
        }
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
                    PowerDelta = 70
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
                    PowerDelta = 110 + 70
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
        for (int i = 0; i < pool.Count; i++)
        {
            if (picked.Contains(pool[i].Key)) continue;
            if (!pool[i].Card.IsValid) continue;
            roll -= Mathf.Max(1, pool[i].Weight * mul);
            if (roll < 0)
            {
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
            picked.Add(pool[i].Key);
            outCards.Add(pool[i].Card);
            return true;
        }
        return false;
    }
}
