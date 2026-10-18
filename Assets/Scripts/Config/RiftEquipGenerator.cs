using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 裂缝局内程序化装备生成：部位 → 主属性 → 范围随机 → 品质修正 → 副属性。
/// </summary>
public static class RiftEquipGenerator
{
    // 2026-10-09 主人拍板：「抽奖每次都出鞋」不是强制逻辑，是随机连着出同一槽。这里防连续同槽。
    // 记住上一次抽到的防具槽位，本次候选里排除它（候选只剩 1 个时不排除），让随机看起来更大。
    // 只在防具分支读写（武器分支不碰），所以抽到武器不会清掉它。状态放静态字段，跨抽奖调用保留。
    static string _lastArmorSlotId;

    /// <param name="fixedForTutorial">
    /// 2026-10-12 主人拍板「每次引导战力一样」：引导局（宝箱 / 第一拍抽卡）传 true，
    /// 把所有随机点钉死 —— 稀有度恒普通、必为主手武器、部位/主属性/模板取序一定、
    /// 属性值取区间中值。正式关不传（默认 false），概率曲线零影响。
    /// </param>
    public static List<EquipInstance> Generate(int count, StageType stageType, int blacksmithLevel = 1, float attrBonus = 0f, bool forceWeapon = false, int chapter = 1, bool fixedForTutorial = false)
    {
        RiftEquipTables.EnsureLoaded();
        var list = new List<EquipInstance>();
        PlayerJobId job = PlayerJobDefs.GetSelected();
        for (int i = 0; i < count; i++)
        {
            var inst = GenerateOne(stageType, job, blacksmithLevel, attrBonus, forceWeapon, chapter, fixedForTutorial);
            if (inst != null) list.Add(inst);
        }
        return list;
    }

    public static EquipInstance GenerateOne(StageType stageType, PlayerJobId job, int blacksmithLevel = 1, float attrBonus = 0f, bool forceWeapon = false, int chapter = 1, bool fixedForTutorial = false)
    {
        RiftEquipTables.EnsureLoaded();
        // 引导确定性化：稀有度恒 NORMAL（Ch1 20% 稀有曲线只影响正式关/非引导抽卡）。
        var rarity = fixedForTutorial ? FindRarityRule("NORMAL") : RollRarity(chapter);
        // forceWeapon 由引导宝箱传入：宝箱只掉武器，避免误给防具；否则保留默认 45% 武器概率。
        // 引导确定性化：必为主手武器（引导第一拍定稿就是「一抽(武器)」）。
        bool wantWeapon = forceWeapon || (fixedForTutorial ? true : Random.value < 0.45f);
        var slot = PickSlot(wantWeapon, job, fixedForTutorial);
        if (slot == null) return null;

        var main = PickWeightedLanded(slot.MainPool, job, null, fixedForTutorial);
        if (string.IsNullOrEmpty(main))
            main = slot.IsWeapon ? "ATK" : "HP";

        EquipSlotType slotType = MapSlotType(slot.SlotId, job);
        // 【2026-10-12 主人拍板】外观模板按稀有度取同档（传奇只挑传奇模板，稀有 / 普通不挑传奇模板）
        // 【2026-10-18 方案A 主人拍板】武器数值锚定模板 → 模板挑选前置（主属性 roll 要用模板攻击值）
        EquipTemplate visual = PickVisualTemplate(slotType, job, fixedForTutorial, rarity.Id == "LEGEND", rarity.Id);
        // 本职业没有可用武器模板 → 这件直接不掉，不拿别职业武器凑数
        if (visual == null && slot.IsWeapon) return null;

        // 2026-10-12 主人拍板：掉落数值随章节成长（复用怪物同款 ChapterStatScaleTable，
        // Ch1 森林 0.88 → Ch8 冰川 2.12）——后期掉的传奇 > 前期掉的，
        // 解决「前期掉落的装备到后期还是一样的数值」。词条 +10（RIFT_DROP_ATTR_BONUS）不变。
        // 【2026-10-18 主人拍板】武器数值要有区间、随关卡慢慢涨：
        //   ① 再乘章内关卡爬坡（stage_stat_scale 1.00→1.50），同一章里后打的关掉的装备更好；
        //   ② 主属性**不再吃** RIFT_DROP_ATTR_BONUS(+10)——那是随机词条的加成，
        //      误加到主属性上导致普通武器凭空多 10 点攻击（实锤：旧短刃 普通=区间4.5~6×0.9×0.88≈4，却显示+15）。
        // 【2026-10-18 方案A 主人拍板「怎么一上来抽的武器还不如初始的」根因修复】
        //   旧链：武器主属性 = ATK 区间 roll（普通 4.5~6 × BaseMul0.9 × growth0.88 ≈ 3.6~4.8，
        //   抽奖再 ×1.18 ≈ 4.2~5.6）< 初始武器模板值 6.6 → 怎么抽都不如初始；
        //   且模板自带 ATK（6.6~55，含主人调的 18/19/20=42/45/48）完全不参与数值，只当外观 —— 55 攻外观出 4 点面板。
        //   新链：武器主属性 = 模板攻击值 × roll(1.0~1.1) × chapterGrowth（抽奖路径再 ×DraftWeaponAttackMul 1.18）。
        //   模板分档与品质对齐见 PickVisualTemplate（普通≤15 / 稀有≤25 / 传奇不限）；roll 下限 1.0 = 不低于模板标注。
        //   防具与引导确定性化路径不变（引导数值单独调过，仍走区间中值）。
        int stageIdx0 = ChapterManager.Instance != null ? ChapterManager.Instance.currentStageIndex : 0;
        float chapterGrowth = ChapterStatScaleTable.Get(Mathf.Max(1, chapter)) * GameConfig.GetStageStatScale(stageIdx0);
        float tplAtk = 0f;   // 先赋值：&& 短路时 TryGetTemplateAttack 可能不执行，out 变量需明确赋值
        bool anchorToTemplate = slot.IsWeapon && !fixedForTutorial && visual != null
                                && TryGetTemplateAttack(visual, main == "MAGIC_ATK", out tplAtk);
        float mainVal = anchorToTemplate
            ? tplAtk * Random.Range(1.0f, 1.1f) * chapterGrowth
            : RollAttrValue(main, rarity.Id, rarity.BaseMul, false, fixedForTutorial) * chapterGrowth;
        var attrs = new List<AttrBonusData>();
        if (TryMapAttr(main, mainVal, job, out var mainBonus))
            attrs.Add(mainBonus);

        int affixCount = rarity.AffixMin >= rarity.AffixMax
            ? rarity.AffixMin
            : Random.Range(rarity.AffixMin, rarity.AffixMax + 1);
        // 引导确定性化：NORMAL 本就 0 词条；即便将来配表改了词条数，也按确定性走，不引入随机。
        if (fixedForTutorial) affixCount = Mathf.Max(0, rarity.AffixMin);
        var used = new HashSet<string> { main };
        for (int a = 0; a < affixCount; a++)
        {
            string id = PickWeightedLanded(slot.RandPool, job, used, fixedForTutorial);
            if (string.IsNullOrEmpty(id)) break;
            used.Add(id);
            float v = ApplyDropBonus(id, RollAttrValue(id, rarity.Id, rarity.AffixMul, true, fixedForTutorial) * chapterGrowth, attrBonus);
            if (TryMapAttr(id, v, job, out var bonus))
                attrs.Add(bonus);
        }

        // （slotType / visual 已上移到主属性 roll 之前 —— 方案A 武器锚模板需要先拿到模板）
        // 2026-09-27 主人要求「副本掉的装备名字和图片也要对齐」——根因在这里：
        // 旧写法先按职业/魔法分池挑出正确的模板 visual（名字、图标、spum 三者一致），
        // 紧接着又用「按部位纯随机」的外观表 id 盖掉 spumName —— 于是名字来自模板、
        // 手上拿的却是随机抽到的另一类武器（法师掉「法杖」却举着斧/剑），
        // 而且 ResolveKind 也拿错 spum，攻击距离和特效一并判错。
        // 真源唯一：有模板就用模板自己的 spumName；外观表只在没有模板（防具临时件）时兜底。
        string appearanceId = null;
        string spum = visual != null ? visual.spumName : null;
        if (string.IsNullOrEmpty(spum))
        {
            appearanceId = EquipAppearanceTables.PickAppearanceId(slotType, slot.IsWeapon);
            spum = EquipAppearanceTables.ResolveSpumName(appearanceId, null);
        }

        var inst = new EquipInstance();
        inst.template = visual;
        inst.templateId = visual != null ? visual.templateId : ("rift_" + slot.SlotId.ToLowerInvariant());
        inst.appearanceId = appearanceId;
        inst.spumNameOverride = spum;
        inst.icon = visual != null ? visual.icon : null;
        if (inst.icon == null && visual != null)
        {
            visual.ResolveIcon();
            inst.icon = visual.icon;
        }
        inst.gridWidth = visual != null ? Mathf.Max(1, visual.gridWidth) : 1;
        inst.gridHeight = visual != null ? Mathf.Max(1, visual.gridHeight) : 1;
        inst.slotType = slotType;
        inst.weaponType = visual != null ? visual.weaponType : (slot.IsWeapon ? WeaponType.OneHand : WeaponType.None);
        inst.weaponHand = visual != null ? visual.weaponHand : WeaponHandSlot.MainHand;
        // 2026-10-09 主人拍板：掉落/抽奖实例必须带上武器伤害类型。
        // 旧写法 inst.weaponAttackType 走枚举默认值（Physical），而 PickVisualTemplate 已按
        // PlayerJobBaseStats.IsMagicJob 挑魔法武器模板 → 法师/牧师掉出来的武器 weaponAttackType 可能是物理。
        inst.weaponAttackType = visual != null
            ? visual.weaponAttackType
            : (PlayerJobBaseStats.IsMagicJob(job) ? WeaponAttackType.Magic : WeaponAttackType.Physical);
        if (slot.IsWeapon && WeaponLoadoutRules.IsLoadoutItem(inst))
            inst.slotType = WeaponLoadoutRules.ResolveLogicalSlot(inst);
        inst.rarity = MapToEngineRarity(rarity.Id);
        inst.star = Mathf.Clamp((int)inst.rarity - 1, 0, (int)inst.rarity);
        inst.requireLevel = 1;
        inst.attrBonus = attrs;
        inst.baseAttrCount = attrs.Count > 0 ? 1 : 0;

        // 2026-10-09 主人拍板：模板 globalBonus / 被动在掉落与抽奖路径也生效。
        // 路径A（GenerateOne）原本只读模板外观、属性全来自 csv，于是 16 件传奇武器模板里
        // 写好的 globalBonus / skillPassives 在副本掉落和抽奖里全部拿不到。
        // 回灌进实例的两个字段（仅视觉模板存在时）：
        //   - globalBonus：直接引用模板（与路径B GenerateFromTemplate 第66行一致）。
        //     注意：globalBonus 由 EquipStatRollup.AppendEquipBonuses 并入局内属性汇总
        //     （2026-10-09 主人拍板：装备上就在局内生效，出战斗清零），本处只需把模板值带进实例即可。
        //   - skillPassives：必须显式深拷贝进 inst.skillPassives，不能只靠 inst.template = visual。
        //     因为 EquipStatRollup.AppendSkillPassives 读的是 equip.skillPassives（实例字段，
        //     见 Systems/EquipStatRollup.cs:83），并非 inst.template.skillPassives。
        // ⚠ 绝不要把 globalBonus / skillPassives 塞进 inst.attrBonus：会破坏 baseAttrCount 与
        //   词条数语义（WeaponAffixSystem 依赖它算词条数）。
        // 【2026-10-12 主人拍板】引导确定性化武器 = 白板：模板被动/property 不灌进实例
        // （主人报「为什么一个白色武器还有技能还有两个属性」—— 引导武器只留 主ATK + 名字/图标/SPUM）。
        // 正式关掉落/抽奖不受影响（10-09 拍板原样生效）。
        // 【2026-10-18 主人拍板】模板 globalBonus / skillPassives 是**传奇专属待遇**（见 equip_rarity_rules：
        // 「传奇再挂一条被动」）。稀有/普通品质兜底套了传奇外观模板时不得全额白送
        // —— 否则前期抽到「稀有·王庭礼剑」= 攻击+64、吸血7% 一步登天（一箭一个的元凶）。
        if (visual != null && !fixedForTutorial && rarity.Id == "LEGEND")
        {
            inst.globalBonus = visual.globalBonus;
            inst.skillPassives = new List<AttrBonusData>();
            if (visual.skillPassives != null)
            {
                for (int i = 0; i < visual.skillPassives.Count; i++)
                {
                    var p = visual.skillPassives[i];
                    if (p == null) continue;
                    inst.skillPassives.Add(new AttrBonusData
                    {
                        attrType = p.attrType,
                        value = p.value,
                        isPercent = p.isPercent
                    });
                }
            }
        }

        inst.equipName = BuildName(rarity, slot, visual);
        return inst;
    }

    /// <summary>
    /// 2026-10-12 主人拍板：掉落 / 抽奖稀有度权重按**章节**走，与隐藏等级彻底脱钩
    /// （主人原话「我说的是整个章节的抽奖几率，跟等级无关」）。
    /// 旧真源 HiddenLevelSystem.GetRarityWeights（hidden_level_rules.csv 等级轴）已废于此用途。
    /// 【2026-10-12 稀有度梯度】主人原话「1，20% 2,25% 3,30% 4,35% 按这个概率走」，
    ///   且「可以多一点少一点，不用非得是整 5 的倍数，35.5 / 46.3 这都可以」：
    ///   Ch1 稀有 20% / Ch2 25% / Ch3 30% / Ch4 35%；Ch5 起沿曲线继续上浮（小数允许）。
    ///   **传奇列不动**（0/1/2/3/3/6/8/10，Ch1 恒 0）。
    /// 想调几率只改这一处（权重值，同章内三档自动归一）。
    /// </summary>
    static void ChapterRarityWeights(int chapter, out float wNormal, out float wRare, out float wLegend)
    {
        switch (Mathf.Clamp(chapter, 1, 8))
        {
            case 2: wNormal = 74f;   wRare = 25f;   wLegend = 1f;  break;
            case 3: wNormal = 68f;   wRare = 30f;   wLegend = 2f;  break;
            case 4: wNormal = 62f;   wRare = 35f;   wLegend = 3f;  break;
            case 5: wNormal = 56.5f; wRare = 40.5f; wLegend = 3f;  break;
            case 6: wNormal = 48.5f; wRare = 45.5f; wLegend = 6f;  break;
            case 7: wNormal = 41.5f; wRare = 50.5f; wLegend = 8f;  break;
            case 8: wNormal = 34.5f; wRare = 55.5f; wLegend = 10f; break;
            default: wNormal = 80f; wRare = 20f; wLegend = 0f; break; // 第 1 章：稀有 20%，无传奇
        }
    }

    static RiftEquipTables.RarityRule RollRarity(int chapter)
    {
        var rules = RiftEquipTables.Rarities;
        if (rules == null || rules.Count == 0)
            return FallbackRarity();

        // 稀有度唯一真源：章节曲线（ChapterRarityWeights）。勿改回隐藏等级或关卡段权重。
        ChapterRarityWeights(chapter, out float wN, out float wR, out float wL);
        float total = 0f;
        var weights = new float[rules.Count];
        for (int i = 0; i < rules.Count; i++)
        {
            float w = WeightForRarity(rules[i].Id, wN, wR, wL);
            // 第 1 章绝不掉传奇（双保险：Ch1 曲线权重本就为 0，防将来调曲线误给）。
            if (rules[i].Id == "LEGEND" && Mathf.Max(1, chapter) <= 1)
                w = 0f;
            weights[i] = w;
            total += w;
        }
        if (total <= 0f) return rules[0];

        float roll = Random.Range(0f, total);
        float acc = 0f;
        for (int i = 0; i < rules.Count; i++)
        {
            acc += weights[i];
            if (roll < acc) return rules[i];
        }
        return rules[rules.Count - 1];
    }

    /// <summary>品质ID → 章节权重映射（2026-10-12 前叫 WeightForHidden，彼时真源是隐藏等级）。</summary>
    static float WeightForRarity(string rarityId, float wNormal, float wRare, float wLegend)
    {
        switch (rarityId)
        {
            case "RARE": return wRare;
            case "LEGEND": return wLegend;
            default: return wNormal;
        }
    }

    [System.Obsolete("稀有度真源是 ChapterRarityWeights 章节曲线，不要接关卡段权重。")]
    static int WeightForStage(RiftEquipTables.RarityRule r, StageType stageType)
    {
        switch (stageType)
        {
            case StageType.Boss: return r.W21to30;
            case StageType.Elite: return r.W11to20;
            default: return r.W1to5;
        }
    }

    static RiftEquipTables.RarityRule FallbackRarity()
    {
        return new RiftEquipTables.RarityRule
        {
            Id = "NORMAL",
            Name = "普通",
            BaseMul = 0.9f,
            AffixMin = 0,
            AffixMax = 0,
            AffixMul = 1f,
            Prefixes = new[] { "普通的" },
            W1to5 = 100
        };
    }

    /// <summary>副手掉落池的部位 id（equip_slot_pools.csv 的 WEAPON_OFFHAND 行）。</summary>
    const string OffHandSlotId = "WEAPON_OFFHAND";

    /// <summary>
    /// 【2026-10-11 主人拍板】副手武器占武器掉落的比重。
    /// 主人原话「抽奖的也可以抽到副手的武器」—— 副手是独立的一件，但主手仍是主流，
    /// 所以给 30%：抽到副手是「赚到一件」，不至于抢掉主手的位置。
    /// </summary>
    const float OffHandWeaponChance = 0.30f;

    static RiftEquipTables.SlotPool PickSlot(bool wantWeapon, PlayerJobId job, bool fixedForTutorial = false)
    {
        var slots = RiftEquipTables.Slots;
        if (slots == null || slots.Count == 0) return null;

        var pool = new List<RiftEquipTables.SlotPool>();
        string jobName = PlayerJobDefs.Get(job).DisplayName;
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            if (wantWeapon)
            {
                if (!s.IsWeapon) continue;
                // 【2026-10-11】副手池单列，不跟职业武器池混在一起掷（见下面那段副手分支）。
                if (s.SlotId == OffHandSlotId) continue;
                if (s.JobFilter == jobName || s.JobFilter.Contains(jobName))
                    pool.Add(s);
            }
            else
            {
                if (s.IsWeapon) continue;
                // 仅头胸手脚
                if (s.SlotId == "HEAD" || s.SlotId == "CHEST" || s.SlotId == "HAND" || s.SlotId == "FOOT")
                    pool.Add(s);
            }
        }

        // 【2026-10-11 主人拍板】副手武器也能抽到：命中就把候选整池换成副手池，
        // 抽不中就还是走本职业武器池。副手只占 30%，主手仍是主流。
        // 引导确定性化：跳过这 30% 随机（固定走本职业主手武器池）。
        if (wantWeapon && !fixedForTutorial && Random.value < OffHandWeaponChance)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].SlotId != OffHandSlotId) continue;
                pool.Clear();
                pool.Add(slots[i]);
                break;
            }
        }

        if (pool.Count == 0)
        {
            // 武器池空则回退防具
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (!s.IsWeapon && (s.SlotId == "HEAD" || s.SlotId == "CHEST" || s.SlotId == "HAND" || s.SlotId == "FOOT"))
                    pool.Add(s);
            }
        }
        if (pool.Count == 0) return null;

        // 2026-10-09 主人拍板：防连续同槽。pool 全为防具部位时，排除上一次抽到的槽位，
        // 让「每次都是鞋」不再连着出现；候选只剩 1 个时保持原样（不排除）。武器分支不受影响。
        if (!pool[0].IsWeapon && pool.Count > 1 && !string.IsNullOrEmpty(_lastArmorSlotId))
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i].SlotId == _lastArmorSlotId) { pool.RemoveAt(i); break; }
            }
        }

        // 引导确定性化：固定取候选第一个（表顺序稳定，跨次运行一致），不掷随机。
        if (fixedForTutorial) return pool[0];

        var picked = pool[Random.Range(0, pool.Count)];
        if (!picked.IsWeapon) _lastArmorSlotId = picked.SlotId;
        return picked;
    }

    /// <summary>只从已落地词条里抽，未映射 ID 不占词条位、不进实例。
    /// 2026-09-26 主人拍板：毒伤只给游侠，职业白名单走表，不写死——非本职业白名单的词缀（如毒）直接不 roll。</summary>
    static string PickWeightedLanded(List<RiftEquipTables.WeightedAttr> pool, PlayerJobId job, HashSet<string> exclude = null, bool fixedForTutorial = false)
    {
        if (pool == null || pool.Count == 0) return null;
        int total = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            string id = pool[i].AttrId;
            if (exclude != null && exclude.Contains(id)) continue;
            if (!RiftEquipTables.IsCombatLanded(id)) continue;
            if (!RiftEquipTables.IsAttrAllowedForJob(id, job)) continue;
            total += Mathf.Max(0, pool[i].Weight);
        }
        if (total <= 0) return null;

        // 引导确定性化：取权重最大的落地词条（并列取表序第一）—— 武器主池即 ATK，跨次运行一致。
        if (fixedForTutorial)
        {
            int bestW = -1;
            string bestId = null;
            for (int i = 0; i < pool.Count; i++)
            {
                string id = pool[i].AttrId;
                if (exclude != null && exclude.Contains(id)) continue;
                if (!RiftEquipTables.IsCombatLanded(id)) continue;
                if (!RiftEquipTables.IsAttrAllowedForJob(id, job)) continue;
                int w = Mathf.Max(0, pool[i].Weight);
                if (w > bestW) { bestW = w; bestId = id; }
            }
            return bestId;
        }

        int roll = Random.Range(0, total);
        int acc = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            string id = pool[i].AttrId;
            if (exclude != null && exclude.Contains(id)) continue;
            if (!RiftEquipTables.IsCombatLanded(id)) continue;
            if (!RiftEquipTables.IsAttrAllowedForJob(id, job)) continue;
            acc += Mathf.Max(0, pool[i].Weight);
            if (roll < acc) return id;
        }
        return null;
    }

    static float RollAttrValue(string attrId, string rarityId, float mul, bool isAffix, bool fixedForTutorial = false)
    {
        if (!RiftEquipTables.TryGetRange(attrId, out var range))
            // 无表兜底区间同样确定性化：中值 20（原 10~30 随机）
            return (fixedForTutorial ? 20f : 10f + Random.Range(0f, 20f)) * mul;

        float min, max;
        switch (rarityId)
        {
            case "RARE":
                min = range.RareMin;
                max = range.RareMax;
                break;
            case "LEGEND":
                min = range.LegendMin;
                max = range.LegendMax;
                break;
            default:
                min = range.CommonMin;
                max = range.CommonMax;
                break;
        }
        if (max < min) max = min;
        // 引导确定性化：区间中值（跨次运行一致）
        float v = fixedForTutorial ? (min + max) * 0.5f : min + (max - min) * Random.value;
        v *= mul;
        // 百分比表内已是百分点数字（如 5 = 5%），运行时 CritRate 用 0~1
        if (IsRateAttr(attrId))
            v *= 0.01f;
        return v;
    }

    static bool IsRateAttr(string id)
    {
        // 【2026-10-12 主人拍板】MS（移速）必须按百分比折算：装备移速基准是 0.96 世界单位/秒
        // （GameConfig.BASE_MOVE_SPEED），表里 MS 区间 1.5~12.5 若按「点」直加到速度上
        // 就是十几倍速（主人报「移速加12的鞋 走的跟飞一样快」）。×0.01 后 = 1.5%~12.5%，
        // 与天赋移速（AttrSystem:494 同口径）一致。
        return id == "CRIT_RATE" || id == "CRIT_DMG" || id == "ATK_SPD" || id == "DMG_RED"
            || id == "DODGE" || id == "LIFE_STEAL" || id == "ELE_DMG" || id == "HEAL"
            || id == "HP_REGEN" || id == "CONTROL_RES" || id == "ANTI_CRIT" || id == "MS";
    }

    /// <summary>
    /// 掉落装备属性整体加成：非百分比、非射程词条数值 +bonus。
    /// 速率/百分比类（暴击、攻速、吸血、射程…）不参与，避免 +10 变成 +1000%。
    /// </summary>
    static float ApplyDropBonus(string attrId, float value, float bonus)
    {
        if (bonus == 0f) return value;
        if (string.IsNullOrEmpty(attrId)) return value;
        if (IsRateAttr(attrId)) return value;
        if (attrId == "RANGE") return value; // 射程走像素→世界单位换算，不适合直接加
        return value + bonus;
    }

    /// <summary>
    /// 2026-09-26 主人拍板：毒伤只给游侠，职业白名单走表，不写死。
    /// 用带职业参数的 TryResolveCombatAttr：不在本职业白名单内的词缀（如毒）映射直接失败，不进 EquipInstance。
    /// </summary>
    static bool TryMapAttr(string attrId, float value, PlayerJobId job, out AttrBonusData bonus)
    {
        bonus = null;
        if (!RiftEquipTables.TryResolveCombatAttr(attrId, job, out AttrType type, out bool isPercent))
            return false;
        if (attrId == "RANGE")
            value = GameConfig.PixelsToUnits(Mathf.Max(1f, value));
        if (type == AttrType.CritRate || type == AttrType.CritDamage || type == AttrType.Dodge || type == AttrType.LifeSteal)
            isPercent = false;
        if (type == AttrType.AttackSpeed)
            isPercent = true;
        bonus = new AttrBonusData { attrType = type, value = value, isPercent = isPercent };
        return true;
    }

    static EquipSlotType MapSlotType(string slotId, PlayerJobId job)
    {
        switch (slotId)
        {
            case "HEAD": return EquipSlotType.Head;
            case "CHEST": return EquipSlotType.Chest;
            case "HAND": return EquipSlotType.Hands;
            case "FOOT": return EquipSlotType.Feet;
            case "WEAPON_SWORD":
                return EquipSlotType.MainHand;
            // 【2026-10-11 主人拍板】副手武器独立成槽：抽到 / 掉到就是一件副手武器，不再挤进主手。
            case "WEAPON_OFFHAND":
                return EquipSlotType.OffHand;
            default:
                return EquipSlotType.MainHand;
        }
    }

    static Rarity MapToEngineRarity(string id)
    {
        switch (id)
        {
            case "RARE": return Rarity.Rare;
            case "LEGEND": return Rarity.Legendary;
            default: return Rarity.Common;
        }
    }

    /// <summary>
    /// 2026-10-11 主人拍板「以后都统一这种命名规则，不管是什么」：
    /// 裂隙装备不再用自己那套前缀词库（equip_rarity_rules.csv 的「传说的|远古的|神圣的|毁灭的」），
    /// 一律改走全项目唯一出口 <see cref="WeaponAffixSystem.RarityPrefix"/>，
    /// 与掉落 / 初始装 / 剧情武器同一个写法（普通无前缀、稀有「稀有·」、传奇「传奇·」）。
    /// 旧词库列在 csv 里留着不动（数据列，不影响运行），但代码不再读它。
    /// </summary>
    static string BuildName(RiftEquipTables.RarityRule rarity, RiftEquipTables.SlotPool slot, EquipTemplate visual)
    {
        string body = slot != null ? slot.SlotName : "装备";
        if (visual != null && !string.IsNullOrEmpty(visual.equipName))
            body = visual.equipName;
        return WeaponAffixSystem.RarityPrefix(MapToEngineRarity(rarity != null ? rarity.Id : null)) + body;
    }

    /// <summary>
    /// 这件模板算不算「传奇档」—— 决定它能不能被传奇/非传奇品质抽到。
    ///
    /// <para>判据顺序：<c>isLegendary</c> 字段 → <c>globalBonus</c> 有真实数值 → <c>skillPassives</c> 非空。
    /// ⚠ 现网 16 件传奇武器模板的 <c>isLegendary</c> 实测仍是 0（历史遗留死字段，只报告未动），
    /// 所以真正起作用的是后两条 —— 也正是「白送吸血被动」的那两条。
    /// 将来若把 <c>isLegendary</c> 补成 1，这里自然以它为准，无需改调用方。</para>
    /// <para>【2026-10-18 主人拍板】globalBonus 判定必须带 value != 0：
    /// Unity 会把 YAML 里的占位零值（attrType:0 value:0，见见习短剑等普通模板）反序列化成非空对象，
    /// 旧写法 t.globalBonus != null 因此把**全部 61 个模板**都判成传奇档 → 同档池永远为空 →
    /// 「稀有/普通不套传奇模板」的 10-12 拍板完全失效，第一章就抽到「稀有·王庭礼剑」白送攻击+64 和吸血7%。
    /// 吸血被动是极稀有词条，只允许传奇品质掉落。</para>
    /// </summary>
    static bool IsLegendaryTemplate(EquipTemplate t)
    {
        if (t == null) return false;
        if (t.isLegendary) return true;
        if (t.globalBonus != null && t.globalBonus.value != 0) return true;
        return t.skillPassives != null && t.skillPassives.Count > 0;
    }

    /// <summary>
    /// 模板自带的攻击类数值（baseAttr 里 Attack / MagicAttack 的非百分比项）。
    /// 【2026-10-18 方案A】武器主属性数值锚 —— 旧路径模板数值完全不参与实例，55 攻外观出 4 点面板。
    /// preferMagic：魔法职业武器优先取 MagicAttack，物理取 Attack；模板没配该类时回退另一种攻击值，
    /// 都没有（旧模板/盾/防具）返回 false → 调用方走旧区间 roll 兜底。
    /// </summary>
    static bool TryGetTemplateAttack(EquipTemplate t, bool preferMagic, out float atk)
    {
        atk = 0f;
        if (t == null || t.baseAttr == null) return false;
        var primary = preferMagic ? AttrType.MagicAttack : AttrType.Attack;
        var fallback = preferMagic ? AttrType.Attack : AttrType.MagicAttack;
        float fb = -1f;
        for (int i = 0; i < t.baseAttr.Count; i++)
        {
            var b = t.baseAttr[i];
            if (b == null || b.isPercent) continue;
            if (b.attrType == primary)
            {
                if (b.value > 0f) { atk = b.value; return true; }
                return false;
            }
            if (b.attrType == fallback && b.value > 0f && fb < 0f) fb = b.value;
        }
        if (fb > 0f) { atk = fb; return true; }
        return false;
    }

    /// <summary>武器模板品质分档阈值（方案A）：普通档只出 ATK≤此值的模板。</summary>
    const float TemplateTierNormalMax = 15f;
    /// <summary>武器模板品质分档阈值（方案A）：稀有档只出 ATK≤此值的模板，更高档留给传奇。</summary>
    const float TemplateTierRareMax = 25f;

    static EquipTemplate PickVisualTemplate(EquipSlotType slot, PlayerJobId job, bool fixedForTutorial = false, bool wantLegendary = false, string rarityId = null)
    {
        var templates = Resources.LoadAll<EquipTemplate>(ContentPaths.Config.Equips);
        if (templates == null || templates.Length == 0) return null;

        // 【2026-10-12 主人拍板】外观模板必须与稀有度**同档**：稀有 / 普通品质不得套传奇武器模板。
        // 起因：主人抽到「稀有·誓约法杖」却带【被动】吸血 —— 根因就是这里不按稀有度筛模板，
        // 稀有品质抽中了传奇武器模板，把它的 skillPassives / globalBonus 一起白送了
        // （主人原话「怎么前期就能抽中这么好的　两个属性还有被动技能」）。
        // tierMismatch = 档位不符的模板，**只在同档池为空时**兜底 —— 否则某些职业
        // （典型是"武器模板全是传奇"）会挑不到任何外观 → 武器直接不掉、抽奖出不了卡。
        var pool = new List<EquipTemplate>();
        var tierMismatch = new List<EquipTemplate>();
        for (int i = 0; i < templates.Length; i++)
        {
            var t = templates[i];
            // 【2026-10-11 主人拍板】副手武器模板（equip_offhand_*）全是锚点装备（isAnchor=1），
            // 旧口径一律跳过锚点 → 副手池永远是空的 → 「抽奖能抽到副手」根本落不了地。
            // 只给副手槽放行锚点；主手 / 防具的锚点过滤一字未改。
            if (t == null) continue;
            if (t.isAnchor && slot != EquipSlotType.OffHand) continue;
            var bucket = IsLegendaryTemplate(t) == wantLegendary ? pool : tierMismatch;
            if (slot == EquipSlotType.MainHand)
            {
                if (t.weaponType == WeaponType.None) continue;
                if (!PlayerJobDefs.TemplateMatchesJob(t, job)) continue;
                // 【2026-10-18 方案A 主人拍板】武器模板按 ATK 分档与品质对齐：
                // 普通 ≤15（sword_1/06/12/01/02/axe/bow 等低档）、稀有 15~25（03/04/05/07/08/sword_3~5 等）、
                // 传奇不限（30~55 高档 + 带被动模板）。档不符进 tierMismatch（同档空时兜底，宁越档不可没外观）。
                // 起因：旧逻辑普通品质能抽到 42~55 攻的高档外观模板，面板却是 4~5 点 roll —— 「抽的还不如初始的」。
                // 盾/防具不走此分支；引导确定性化不分档（固定模板序列不能变）。
                if (!fixedForTutorial && !string.IsNullOrEmpty(rarityId)
                    && TryGetTemplateAttack(t, PlayerJobBaseStats.IsMagicJob(job), out float tAtk))
                {
                    bool tierMismatchByAtk =
                        (rarityId == "NORMAL" && tAtk > TemplateTierNormalMax)
                        || (rarityId == "RARE" && (tAtk > TemplateTierRareMax || tAtk <= TemplateTierNormalMax));
                    if (tierMismatchByAtk)
                    {
                        // t 可能已因 IsLegendaryTemplate 分进 tierMismatch —— 防重复 Add（重复会让选中概率翻倍）
                        if (!tierMismatch.Contains(t)) tierMismatch.Add(t);
                        pool.Remove(t);
                        continue;
                    }
                }
                bucket.Add(t);
            }
            else if (slot == EquipSlotType.OffHand)
            {
                // 【2026-10-18 主人拍板】副手槽只给盾牌：offhand 剑/匕/斧这类「副手武器」不再进抽奖池，
                // 选剑盾只能获得剑与盾，副手替换弹窗只会盾对盾。盾是否匹配职业走 TemplateMatchesJob（盾仅剑盾）。
                // 2026-10-11「放行锚点让副手武器能抽到」随狂战下架作废；以后双持职业回归再在此放开。
                if (t.weaponType != WeaponType.None) continue;           // 副手武器（非盾）一律排除
                if (!PlayerJobDefs.TemplateMatchesJob(t, job)) continue;
                bucket.Add(t);
            }
            else
            {
                if (t.slotType != slot) continue;
                bucket.Add(t);
            }
        }
        if (pool.Count == 0 && tierMismatch.Count > 0)
            pool = tierMismatch;   // 兜底：同档一件模板都没有 → 退回不限档（宁外观重复，不可没外观）
        // 2026-09-26 主人拍板：武器按职业伤害类型分池——魔法职业(法师/牧师)优先掉魔法武器，物理职业优先掉物理武器。
        // 判定走表（player_job_base_stats.伤害类型），不写死职业。
        // ⚠ 做成「优先」而非「强制」：优先池为空时回退到按职业过滤的池，避免某职业一件武器都不掉。
        //    2026-09-26 补了 6 个魔法武器模板（equip_magic_weapon_01/03/05/09/11/16），
        //    加上原有的 weapon_twilight_staff / equip_weapon_002，魔法池共 8 个。
        //    若主人要改成「强制」，删掉下面 prefer.Count > 0 的回退分支即可。
        if (slot == EquipSlotType.MainHand || slot == EquipSlotType.OffHand)
        {
            bool wantMagic = PlayerJobBaseStats.IsMagicJob(job);
            var prefer = new List<EquipTemplate>();
            for (int i = 0; i < pool.Count; i++)
            {
                var t = pool[i];
                if (t == null) continue;
                bool isMagicWeapon = t.weaponAttackType == WeaponAttackType.Magic;
                if (isMagicWeapon == wantMagic) prefer.Add(t);
            }
            if (prefer.Count > 0) pool = prefer;
        }

        if (pool.Count == 0)
        {
            // 2026-09-26：武器池按职业过滤后为空 → 宁可这件不掉，也绝不退回「不过滤」的随机武器
            // （旧兜底正是重武者/法师开到别职业武器的原因）。防具部位无职业限制，保持原兜底。
            if (slot == EquipSlotType.MainHand || slot == EquipSlotType.OffHand) return null;
            for (int i = 0; i < templates.Length; i++)
            {
                var t = templates[i];
                if (t == null || t.isAnchor) continue;
                if (t.slotType == slot)
                    pool.Add(t);
            }
        }
        if (pool.Count == 0) return null;
        // 引导确定性化：按 templateId 字典序取第一（Resources.LoadAll 顺序不稳，排序后才跨次运行一致）。
        if (fixedForTutorial)
        {
            pool.Sort((a, b) => string.CompareOrdinal(a.templateId, b.templateId));
            var fixedPick = pool[0];
            fixedPick.ResolveIcon();
            return fixedPick;
        }
        var pick = pool[Random.Range(0, pool.Count)];
        pick.ResolveIcon();
        return pick;
    }

    /// <summary>按品质 ID 找稀有度规则（引导确定性化用，找不到回落 NORMAL 兜底规则）。</summary>
    static RiftEquipTables.RarityRule FindRarityRule(string id)
    {
        var rules = RiftEquipTables.Rarities;
        if (rules != null)
        {
            for (int i = 0; i < rules.Count; i++)
            {
                if (rules[i] != null && rules[i].Id == id) return rules[i];
            }
        }
        return FallbackRarity();
    }
}
