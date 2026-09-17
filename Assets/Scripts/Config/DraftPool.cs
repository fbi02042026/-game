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

    public bool IsValid
    {
        get
        {
            if (Kind == DraftCardKind.Equip) return Equip != null;
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

    public static List<DraftCard> BuildCards(DraftCategory c)
    {
        switch (c)
        {
            case DraftCategory.Merc: return BuildMercCards();
            case DraftCategory.Equip: return BuildEquipCards();
            default: return BuildSkillCards();
        }
    }

    static List<DraftCard> BuildSkillCards()
    {
        var cards = new List<DraftCard>(CardCount);
        var pool = new List<WeightedCard>();
        pool.AddRange(CollectNewSkills());
        pool.AddRange(CollectUpgradableSkills());
        FillFromWeighted(pool, cards);
        FillFallback(cards, DraftCategory.Skill);
        return cards;
    }

    static List<DraftCard> BuildMercCards()
    {
        var cards = new List<DraftCard>(CardCount);
        var pool = new List<WeightedCard>();
        pool.AddRange(CollectRecruitMercs());
        pool.AddRange(CollectMercLevelCards());
        pool.AddRange(CollectMercStarCards());
        FillFromWeighted(pool, cards);
        FillFallback(cards, DraftCategory.Merc);
        return cards;
    }

    static List<DraftCard> BuildEquipCards()
    {
        var cards = new List<DraftCard>(CardCount);
        if (!HasEquipContent()) return cards;

        int blacksmith = TownSystem.Instance != null
            ? TownSystem.Instance.GetBuildingLevel(BuildingType.Blacksmith)
            : 1;
        var job = PlayerJobDefs.GetSelected();
        StageType st = ResolveDraftStageType();

        for (int i = 0; i < CardCount; i++)
        {
            EquipInstance eq = null;
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

            cards.Add(new DraftCard
            {
                Kind = DraftCardKind.Equip,
                Id = "equip_" + eq.templateId + "_" + i,
                Title = string.IsNullOrEmpty(eq.equipName) ? eq.templateId : eq.equipName,
                Desc = FormatEquipDesc(eq),
                Rarity = MapRarity(eq.rarity),
                Tag = SynergyTag.None,
                Star = eq.star < 1 ? 1 : eq.star,
                PowerDelta = (int)eq.rarity * 60 + Mathf.Max(1, eq.star) * 80
                             + (eq.attrBonus != null ? eq.attrBonus.Count : 0) * 30,
                Equip = eq
            });
        }
        return cards;
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

    static string FormatEquipDesc(EquipInstance eq)
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

    static SkillRarity MapRarity(Rarity r)
    {
        switch (r)
        {
            case Rarity.Legendary: return SkillRarity.Legendary;
            case Rarity.Epic: return SkillRarity.Epic;
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
