using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 裂缝局内程序化装备生成：部位 → 主属性 → 范围随机 → 品质修正 → 副属性。
/// </summary>
public static class RiftEquipGenerator
{
    public static List<EquipInstance> Generate(int count, StageType stageType, int blacksmithLevel = 1)
    {
        RiftEquipTables.EnsureLoaded();
        var list = new List<EquipInstance>();
        PlayerJobId job = PlayerJobDefs.GetSelected();
        for (int i = 0; i < count; i++)
        {
            var inst = GenerateOne(stageType, job, blacksmithLevel);
            if (inst != null) list.Add(inst);
        }
        return list;
    }

    public static EquipInstance GenerateOne(StageType stageType, PlayerJobId job, int blacksmithLevel = 1)
    {
        RiftEquipTables.EnsureLoaded();
        var rarity = RollRarity(stageType, blacksmithLevel);
        bool wantWeapon = Random.value < 0.45f;
        var slot = PickSlot(wantWeapon, job);
        if (slot == null) return null;

        var main = PickWeighted(slot.MainPool);
        if (string.IsNullOrEmpty(main))
            main = slot.IsWeapon ? "ATK" : "HP";

        float mainVal = RollAttrValue(main, rarity.Id, rarity.BaseMul, false);
        var attrs = new List<AttrBonusData>();
        if (TryMapAttr(main, mainVal, out var mainBonus))
            attrs.Add(mainBonus);

        int affixCount = rarity.AffixMin >= rarity.AffixMax
            ? rarity.AffixMin
            : Random.Range(rarity.AffixMin, rarity.AffixMax + 1);
        var used = new HashSet<string> { main };
        for (int a = 0; a < affixCount; a++)
        {
            string id = PickWeighted(slot.RandPool, used);
            if (string.IsNullOrEmpty(id)) break;
            used.Add(id);
            float v = RollAttrValue(id, rarity.Id, rarity.AffixMul, true);
            if (TryMapAttr(id, v, out var bonus))
                attrs.Add(bonus);
        }

        EquipSlotType slotType = MapSlotType(slot.SlotId, job);
        EquipTemplate visual = PickVisualTemplate(slotType, job);
        string appearanceId = EquipAppearanceTables.PickAppearanceId(slotType, slot.IsWeapon);
        string spum = EquipAppearanceTables.ResolveSpumName(appearanceId,
            visual != null ? visual.spumName : null);

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
        if (slot.IsWeapon && WeaponLoadoutRules.IsLoadoutItem(inst))
            inst.slotType = WeaponLoadoutRules.ResolveLogicalSlot(inst);
        inst.rarity = MapToEngineRarity(rarity.Id);
        inst.star = Mathf.Clamp((int)inst.rarity - 1, 0, (int)inst.rarity);
        inst.requireLevel = 1;
        inst.attrBonus = attrs;
        inst.baseAttrCount = attrs.Count > 0 ? 1 : 0;
        inst.equipName = BuildName(rarity, slot, visual);
        return inst;
    }

    static RiftEquipTables.RarityRule RollRarity(StageType stageType, int blacksmithLevel)
    {
        var rules = RiftEquipTables.Rarities;
        if (rules == null || rules.Count == 0)
            return FallbackRarity();

        HiddenLevelSystem.GetRarityWeights(out int wN, out int wR, out int wL);
        int total = 0;
        var weights = new int[rules.Count];
        for (int i = 0; i < rules.Count; i++)
        {
            int w = WeightForHidden(rules[i].Id, wN, wR, wL);
            if (rules[i].Id == "LEGEND" && blacksmithLevel < 2 && stageType == StageType.Normal)
                w = 0;
            weights[i] = w;
            total += w;
        }
        if (total <= 0) return rules[0];

        int roll = Random.Range(0, total);
        int acc = 0;
        for (int i = 0; i < rules.Count; i++)
        {
            acc += weights[i];
            if (roll < acc) return rules[i];
        }
        return rules[rules.Count - 1];
    }

    static int WeightForHidden(string rarityId, int wNormal, int wRare, int wLegend)
    {
        switch (rarityId)
        {
            case "RARE": return wRare;
            case "LEGEND": return wLegend;
            default: return wNormal;
        }
    }

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

    static RiftEquipTables.SlotPool PickSlot(bool wantWeapon, PlayerJobId job)
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
        return pool[Random.Range(0, pool.Count)];
    }

    static string PickWeighted(List<RiftEquipTables.WeightedAttr> pool, HashSet<string> exclude = null)
    {
        if (pool == null || pool.Count == 0) return null;
        int total = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            if (exclude != null && exclude.Contains(pool[i].AttrId)) continue;
            total += Mathf.Max(0, pool[i].Weight);
        }
        if (total <= 0) return null;
        int roll = Random.Range(0, total);
        int acc = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            if (exclude != null && exclude.Contains(pool[i].AttrId)) continue;
            acc += Mathf.Max(0, pool[i].Weight);
            if (roll < acc) return pool[i].AttrId;
        }
        return pool[0].AttrId;
    }

    static float RollAttrValue(string attrId, string rarityId, float mul, bool isAffix)
    {
        if (!RiftEquipTables.TryGetRange(attrId, out var range))
            return (10f + Random.Range(0f, 20f)) * mul;

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
        float v = min + (max - min) * Random.value;
        v *= mul;
        // 百分比表内已是百分点数字（如 5 = 5%），运行时 CritRate 用 0~1
        if (IsRateAttr(attrId))
            v *= 0.01f;
        return v;
    }

    static bool IsRateAttr(string id)
    {
        return id == "CRIT_RATE" || id == "CRIT_DMG" || id == "ATK_SPD" || id == "DMG_RED"
            || id == "DODGE" || id == "LIFE_STEAL" || id == "ELE_DMG" || id == "HEAL"
            || id == "HP_REGEN" || id == "CONTROL_RES" || id == "ANTI_CRIT";
    }

    static bool TryMapAttr(string attrId, float value, out AttrBonusData bonus)
    {
        bonus = null;
        if (!TryResolveAttrType(attrId, out AttrType type, out bool isPercent))
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

    /// <summary>只映射现有 AttrType；其余跳过（二期扩展枚举）。</summary>
    static bool TryResolveAttrType(string attrId, out AttrType type, out bool isPercent)
    {
        isPercent = false;
        type = AttrType.Attack;
        switch (attrId)
        {
            case "ATK": type = AttrType.Attack; return true;
            case "DEF": type = AttrType.Defense; return true;
            case "HP": type = AttrType.MaxHp; return true;
            case "MS": type = AttrType.MoveSpeed; return true;
            case "CRIT_RATE": type = AttrType.CritRate; return true;
            case "ATK_SPD": type = AttrType.AttackSpeed; return true;
            case "RANGE": type = AttrType.AttackRange; return true;
            case "DODGE": type = AttrType.Dodge; return true;
            case "LIFE_STEAL": type = AttrType.LifeSteal; return true;
            case "ELE_DMG": type = AttrType.FireDamage; isPercent = true; return true;
            case "CRIT_DMG": type = AttrType.CritDamage; return true;
            case "DMG_RED": type = AttrType.Defense; isPercent = true; return true;
            default: return false;
        }
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

    static string BuildName(RiftEquipTables.RarityRule rarity, RiftEquipTables.SlotPool slot, EquipTemplate visual)
    {
        string prefix = "普通的";
        if (rarity.Prefixes != null && rarity.Prefixes.Length > 0)
            prefix = rarity.Prefixes[Random.Range(0, rarity.Prefixes.Length)];
        string body = slot != null ? slot.SlotName : "装备";
        if (visual != null && !string.IsNullOrEmpty(visual.equipName))
            body = visual.equipName;
        return prefix + body;
    }

    static EquipTemplate PickVisualTemplate(EquipSlotType slot, PlayerJobId job)
    {
        var templates = Resources.LoadAll<EquipTemplate>(ContentPaths.Config.Equips);
        if (templates == null || templates.Length == 0) return null;

        var pool = new List<EquipTemplate>();
        for (int i = 0; i < templates.Length; i++)
        {
            var t = templates[i];
            if (t == null || t.isAnchor) continue;
            if (slot == EquipSlotType.MainHand || slot == EquipSlotType.OffHand)
            {
                if (t.weaponType == WeaponType.None) continue;
                if (!PlayerJobDefs.TemplateMatchesJob(t, job)) continue;
                pool.Add(t);
            }
            else
            {
                if (t.slotType != slot) continue;
                pool.Add(t);
            }
        }
        if (pool.Count == 0)
        {
            for (int i = 0; i < templates.Length; i++)
            {
                var t = templates[i];
                if (t == null || t.isAnchor) continue;
                if (t.slotType == slot || (slot == EquipSlotType.MainHand && t.weaponType != WeaponType.None))
                    pool.Add(t);
            }
        }
        if (pool.Count == 0) return templates[0];
        var pick = pool[Random.Range(0, pool.Count)];
        pick.ResolveIcon();
        return pick;
    }
}
