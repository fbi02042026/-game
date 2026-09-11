using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 裂缝程序化装备表：部位池 / 品质规则 / 属性数值范围。
/// </summary>
public static class RiftEquipTables
{
    public struct WeightedAttr
    {
        public string AttrId;
        public int Weight;
    }

    public class SlotPool
    {
        public string SlotId;
        public string SlotName;
        public List<WeightedAttr> MainPool = new List<WeightedAttr>();
        public List<WeightedAttr> RandPool = new List<WeightedAttr>();
        public string JobFilter; // 空/全职业 = 防具；否则职业名
        public bool IsWeapon;
    }

    public class RarityRule
    {
        public string Id;
        public string Name;
        public float BaseMul;
        public int AffixMin;
        public int AffixMax;
        public float AffixMul;
        public string[] Prefixes;
        public int W1to5;
        public int W6to10;
        public int W11to20;
        public int W21to30;
        public int W31to60;
    }

    public class AttrRange
    {
        public string AttrId;
        public string AttrName;
        public float CommonMin, CommonMax;
        public float RareMin, RareMax;
        public float LegendMin, LegendMax;
        public bool IsPercent;
    }

    static readonly List<SlotPool> _slots = new List<SlotPool>();
    static readonly List<RarityRule> _rarities = new List<RarityRule>();
    static readonly Dictionary<string, AttrRange> _ranges = new Dictionary<string, AttrRange>();
    static bool _loaded;

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        LoadSlots();
        LoadRarities();
        LoadRanges();
    }

    public static void Reload()
    {
        _loaded = false;
        _slots.Clear();
        _rarities.Clear();
        _ranges.Clear();
        EnsureLoaded();
    }

    public static IReadOnlyList<SlotPool> Slots
    {
        get { EnsureLoaded(); return _slots; }
    }

    public static IReadOnlyList<RarityRule> Rarities
    {
        get { EnsureLoaded(); return _rarities; }
    }

    public static bool TryGetRange(string attrId, out AttrRange range)
    {
        EnsureLoaded();
        return _ranges.TryGetValue(attrId, out range);
    }

    static void LoadSlots()
    {
        string raw = GameTableStore.LoadText(ContentPaths.Data.EquipSlotPools);
        if (string.IsNullOrEmpty(raw)) return;
        var rows = GameTableCsv.ParseRows(raw);
        for (int i = 1; i < rows.Count; i++)
        {
            var c = rows[i];
            if (c.Length < 7) continue;
            if (c[0] == "UNDER") continue; // 不加内衣
            var slot = new SlotPool
            {
                SlotId = c[0].Trim(),
                SlotName = c[1].Trim(),
                JobFilter = c[6].Trim(),
                IsWeapon = c[0].StartsWith("WEAPON_")
            };
            ParseWeighted(c[2], slot.MainPool);
            ParseWeighted(c[4], slot.RandPool);
            _slots.Add(slot);
        }
    }

    static void LoadRarities()
    {
        string raw = GameTableStore.LoadText(ContentPaths.Data.EquipRarityRules);
        if (string.IsNullOrEmpty(raw)) return;
        var rows = GameTableCsv.ParseRows(raw);
        for (int i = 1; i < rows.Count; i++)
        {
            var c = rows[i];
            if (c.Length < 13) continue;
            var rule = new RarityRule
            {
                Id = c[0].Trim(),
                Name = c[1].Trim(),
                Prefixes = c[7].Split(new[] { '|' }, System.StringSplitOptions.RemoveEmptyEntries)
            };
            GameTableCsv.TryFloat(c[3], out rule.BaseMul);
            ParseAffixCount(c[4], out rule.AffixMin, out rule.AffixMax);
            if (!GameTableCsv.TryFloat(c[5], out rule.AffixMul))
                rule.AffixMul = rule.Id == "LEGEND" ? 1.15f : 1f;
            GameTableCsv.TryInt(c[8], out rule.W1to5);
            GameTableCsv.TryInt(c[9], out rule.W6to10);
            GameTableCsv.TryInt(c[10], out rule.W11to20);
            GameTableCsv.TryInt(c[11], out rule.W21to30);
            GameTableCsv.TryInt(c[12], out rule.W31to60);
            _rarities.Add(rule);
        }
    }

    static void LoadRanges()
    {
        string raw = GameTableStore.LoadText(ContentPaths.Data.EquipAttrRanges);
        if (string.IsNullOrEmpty(raw)) return;
        var rows = GameTableCsv.ParseRows(raw);
        for (int i = 1; i < rows.Count; i++)
        {
            var c = rows[i];
            if (c.Length < 10) continue;
            var r = new AttrRange
            {
                AttrId = c[0].Trim(),
                AttrName = c[1].Trim(),
                IsPercent = (c[9] ?? "").Contains("%") || (c[2] ?? "").Contains("百分比")
            };
            r.CommonMin = ParseNum(c[3]);
            r.CommonMax = ParseNum(c[4]);
            r.RareMin = ParseNum(c[5]);
            r.RareMax = ParseNum(c[6]);
            r.LegendMin = ParseNum(c[7]);
            r.LegendMax = ParseNum(c[8]);
            _ranges[r.AttrId] = r;
        }
    }

    static void ParseWeighted(string raw, List<WeightedAttr> dst)
    {
        if (string.IsNullOrEmpty(raw)) return;
        string[] parts = raw.Split('|');
        for (int i = 0; i < parts.Length; i++)
        {
            string p = parts[i].Trim();
            if (string.IsNullOrEmpty(p)) continue;
            int colon = p.LastIndexOf(':');
            if (colon <= 0) continue;
            string name = p.Substring(0, colon).Trim();
            if (!int.TryParse(p.Substring(colon + 1).Trim(), out int w)) continue;
            dst.Add(new WeightedAttr { AttrId = MapCnAttrToId(name), Weight = w });
        }
    }

    /// <summary>
    /// 表 ID 是否已映射到战斗 AttrType。未落地的（毒/回复等）禁止进 EquipInstance / Recalc。
    /// 新词条：先加 AttrType + 一处结算，再把 case 加进这里。
    /// </summary>
    public static bool IsCombatLanded(string attrId)
    {
        return TryResolveCombatAttr(attrId, out _, out _);
    }

    /// <summary>表 ID → AttrType。未列出的一律失败（不要静默发明映射）。</summary>
    public static bool TryResolveCombatAttr(string attrId, out AttrType type, out bool isPercent)
    {
        isPercent = false;
        type = AttrType.Attack;
        if (string.IsNullOrEmpty(attrId)) return false;
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

    /// <summary>按稀有度从 equip_attr_ranges 掷属性（正式表）。</summary>
    public static float RollAttrFlat(string attrId, Rarity rarity, float mul = 1f)
    {
        EnsureLoaded();
        if (!_ranges.TryGetValue(attrId, out var range))
            return 0f;
        float lo, hi;
        switch (rarity)
        {
            case Rarity.Legendary:
            case Rarity.Epic:
                lo = range.LegendMin; hi = range.LegendMax; break;
            case Rarity.Rare:
            case Rarity.Uncommon:
                lo = range.RareMin; hi = range.RareMax; break;
            default:
                lo = range.CommonMin; hi = range.CommonMax; break;
        }
        if (hi < lo) hi = lo;
        return Random.Range(lo, hi) * mul;
    }

    /// <summary>普通档中位 * ratio，用于初始/锚点武器（如训练剑 0.7）。</summary>
    public static float CommonMid(string attrId, float ratio = 1f)
    {
        EnsureLoaded();
        if (!_ranges.TryGetValue(attrId, out var range))
            return 0f;
        return (range.CommonMin + range.CommonMax) * 0.5f * ratio;
    }

    public static string MapCnAttrToId(string cn)
    {
        switch (cn)
        {
            case "生命": return "HP";
            case "攻击": return "ATK";
            case "防御": return "DEF";
            case "移速": return "MS";
            case "生命回复": return "HP_REGEN";
            case "减伤": return "DMG_RED";
            case "暴击率": return "CRIT_RATE";
            case "暴击伤害": return "CRIT_DMG";
            case "攻速": return "ATK_SPD";
            case "射程": return "RANGE";
            case "元素伤害": return "ELE_DMG";
            case "治疗强化": return "HEAL";
            case "受控减免": return "CONTROL_RES";
            case "抗暴": return "ANTI_CRIT";
            case "闪避": return "DODGE";
            case "反弹伤害": return "REFLECT";
            case "眩晕强化": return "STUN_DUR";
            case "流血": return "BLEED";
            case "生命偷取": return "LIFE_STEAL";
            case "击退": return "KNOCK_BACK";
            case "穿透": return "PIERCE";
            case "中毒": return "POISON";
            case "点燃": return "BURN";
            case "减速": return "SLOW";
            case "弹射": return "RICOCHET";
            case "净化": return "PURIFY";
            case "光环强化": return "AURA";
            case "静止增伤": return "STATIC_DMG";
            case "低血增伤": return "LOW_HP_DMG";
            case "范围伤害": return "RANGE_DMG";
            case "嘲讽强化": return "TAUNT";
            case "破甲": return "ARMOR_BREAK";
            default: return cn;
        }
    }

    static void ParseAffixCount(string s, out int min, out int max)
    {
        min = 0;
        max = 0;
        if (string.IsNullOrEmpty(s) || s == "0" || s == "无") return;
        int dash = s.IndexOf('-');
        if (dash > 0)
        {
            int.TryParse(s.Substring(0, dash).Trim(), out min);
            int.TryParse(s.Substring(dash + 1).Trim(), out max);
        }
        else
        {
            int.TryParse(s.Trim(), out min);
            max = min;
        }
    }

    static float ParseNum(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0f;
        s = s.Trim().Replace("%", "").Replace("/秒", "").Replace("秒", "");
        float.TryParse(s, out float v);
        return v;
    }
}
