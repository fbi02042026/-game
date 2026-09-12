using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// 佣兵技能表 SK001~SK020（读 Cook 后的 merc_skills.bytes）。
/// </summary>
public static class MercSkillTable
{
    public enum SkillCategory
    {
        Physical,
        Defense,
        Heal,
        Magic
    }

    public enum TriggerKind
    {
        None,
        AutoCast,
        OnAttack,
        OnHit,
        LowHp,
        Always,
        OnHeal
    }

    public struct Row
    {
        public string Id;
        public string DisplayName;
        public SkillCategory Category;
        public string SkillRarity;
        public bool IsPassive;
        public string MercRarityMin;
        public TriggerKind Trigger;
        public string TargetType;
        public string RecruitDesc;
        public string EffectDesc;
        public string Formula;
        public float Cooldown;
        public float Duration;
        public string RangeDesc;
        public string IconId;
    }

    static Dictionary<string, Row> _byId;
    static bool _loaded;

    public static bool HasData
    {
        get
        {
            EnsureLoaded();
            return _byId != null && _byId.Count > 0;
        }
    }

    public static void Reload()
    {
        _loaded = false;
        _byId = null;
        EnsureLoaded();
    }

    static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        _byId = new Dictionary<string, Row>();

        string raw = GameTableStore.LoadText(ContentPaths.Data.MercSkills);
        if (string.IsNullOrEmpty(raw))
        {
            Debug.LogError("[MercSkillTable] 战斗表加载失败: Resources/" + ContentPaths.Data.MercSkills + " （空或缺失），using defaults。");
            return;
        }

        string[] lines = raw.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        int ok = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
            if (line.StartsWith("技能ID")) continue;

            string[] cols = SplitCsvLine(line);
            if (cols.Length < 12) continue;
            string id = cols[0].Trim();
            if (string.IsNullOrEmpty(id) || !id.StartsWith("SK")) continue;

            var row = new Row
            {
                Id = id,
                DisplayName = cols[2].Trim(),
                Category = ParseCategory(cols[3].Trim()),
                SkillRarity = cols[4].Trim(),
                IsPassive = cols[5].Trim().Contains("被动"),
                MercRarityMin = cols[6].Trim(),
                Trigger = ParseTrigger(cols[7].Trim()),
                TargetType = cols[8].Trim(),
                RecruitDesc = cols.Length > 9 ? cols[9].Trim() : "",
                EffectDesc = cols.Length > 10 ? cols[10].Trim() : "",
                Formula = cols.Length > 11 ? cols[11].Trim() : "",
                Cooldown = ParseFloat(cols.Length > 12 ? cols[12] : ""),
                Duration = ParseFloat(cols.Length > 13 ? cols[13] : ""),
                RangeDesc = cols.Length > 14 ? cols[14].Trim() : "",
                IconId = id
            };
            _byId[id] = row;
            ok++;
        }
        if (ok <= 0)
            Debug.LogError("[MercSkillTable] 战斗表加载失败: Resources/" + ContentPaths.Data.MercSkills + " （解析 0 条），using defaults。");
        else
            Debug.Log($"[MercSkillTable] 已加载 {ok} 条技能");
    }

    static string[] SplitCsvLine(string line)
    {
        return line.Split(',');
    }

    static SkillCategory ParseCategory(string s)
    {
        if (s.Contains("防御")) return SkillCategory.Defense;
        if (s.Contains("恢复")) return SkillCategory.Heal;
        if (s.Contains("法术")) return SkillCategory.Magic;
        return SkillCategory.Physical;
    }

    static TriggerKind ParseTrigger(string s)
    {
        if (string.IsNullOrEmpty(s)) return TriggerKind.None;
        if (s.Contains("自动")) return TriggerKind.AutoCast;
        if (s.Contains("攻击触发")) return TriggerKind.OnAttack;
        if (s.Contains("受击")) return TriggerKind.OnHit;
        if (s.Contains("生命触发")) return TriggerKind.LowHp;
        if (s.Contains("常驻")) return TriggerKind.Always;
        if (s.Contains("治疗触发")) return TriggerKind.OnHeal;
        return TriggerKind.None;
    }

    static float ParseFloat(string s)
    {
        if (string.IsNullOrEmpty(s) || s == "—" || s == "-") return 0f;
        if (float.TryParse(s, out float v)) return v;
        return 0f;
    }

    public static bool TryGet(string id, out Row row)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(id))
        {
            row = default;
            return false;
        }
        return _byId.TryGetValue(id, out row);
    }

    public static string GetDisplayName(string id)
    {
        return TryGet(id, out var r) ? r.DisplayName : id;
    }

    public static bool IsPassive(string id)
    {
        return TryGet(id, out var r) && r.IsPassive;
    }

    public static bool IsMercSkillId(string id)
    {
        return !string.IsNullOrEmpty(id) && id.StartsWith("SK") && TryGet(id, out _);
    }

    /// <summary>从表行生成运行时 SkillConfig（SkillRegistry 查不到 asset 时回退）。</summary>
    public static SkillConfig BuildRuntimeConfig(string id)
    {
        if (!TryGet(id, out var row) || row.IsPassive) return null;

        var cfg = ScriptableObject.CreateInstance<SkillConfig>();
        cfg.id = id;
        cfg.skillName = row.DisplayName;
        cfg.desc = string.IsNullOrEmpty(row.RecruitDesc) ? row.EffectDesc : row.RecruitDesc;
        cfg.cooldown = row.Cooldown > 0f ? row.Cooldown : 8f;
        cfg.duration = row.Duration;
        cfg.aoeRadius = ResolveAoeRadius(row);
        cfg.attackKit = ResolveAttackKit(row);

        ApplyFormula(cfg, row);
        cfg.skillType = ResolveSkillType(row, cfg);
        return cfg;
    }

    static void ApplyFormula(SkillConfig cfg, Row row)
    {
        string f = row.Formula ?? "";
        float atkMul = ParseAttackMultiplier(f);
        if (atkMul > 0f)
            cfg.damageMultiplier = atkMul;

        bool healish = row.Category == SkillCategory.Heal || f.Contains("治疗");
        if (healish)
        {
            if (f.Contains("攻击"))
            {
                cfg.healBase = 0f;
                cfg.healPercentOfMax = 0f;
                cfg.damageMultiplier = atkMul > 0f ? atkMul : 1.2f;
            }
            else if (f.Contains("生命上限") || f.Contains("最大生命"))
            {
                var m = Regex.Match(f, @"(\d+(?:\.\d+)?)\s*%");
                cfg.healPercentOfMax = m.Success ? float.Parse(m.Groups[1].Value) / 100f : 0.1f;
            }
        }

        // 表驱 Buff：防御+ / 生命上限× / 目标攻击-。禁止再按 Id == SKxxx。
        if (Regex.IsMatch(f, @"防御\s*\+"))
        {
            cfg.buffAttr = AttrType.Defense;
            cfg.buffValue = ParseSignedPercent(f, 0.35f);
            cfg.buffIsPercent = true;
            cfg.duration = row.Duration > 0f ? row.Duration : 5f;
        }
        else if (!healish && f.Contains("生命上限"))
        {
            cfg.buffAttr = AttrType.MaxHp;
            cfg.buffValue = ParseSignedPercent(f, 0.10f);
            cfg.buffIsPercent = true;
            cfg.duration = row.Duration > 0f ? row.Duration : 6f;
        }
        else if (f.Contains("目标攻击"))
        {
            float v = ParseSignedPercent(f, -0.20f);
            cfg.buffAttr = AttrType.Attack;
            cfg.buffValue = v > 0f ? -v : v;
            cfg.buffIsPercent = true;
            cfg.duration = row.Duration > 0f ? row.Duration : 8f;
        }
    }

    static float ParseSignedPercent(string formula, float fallback)
    {
        if (string.IsNullOrEmpty(formula)) return fallback;
        var m = Regex.Match(formula, @"([+-]?\d+(?:\.\d+)?)\s*%");
        if (!m.Success) return fallback;
        return float.Parse(m.Groups[1].Value) / 100f;
    }

    static float ParseAttackMultiplier(string formula)
    {
        if (string.IsNullOrEmpty(formula)) return 0f;
        var m = Regex.Match(formula, @"攻击\s*[×xX*]\s*(\d+(?:\.\d+)?)\s*%");
        if (m.Success)
            return float.Parse(m.Groups[1].Value) / 100f;
        m = Regex.Match(formula, @"(\d+(?:\.\d+)?)\s*%");
        if (m.Success && formula.Contains("攻击"))
            return float.Parse(m.Groups[1].Value) / 100f;
        return 0f;
    }

    static float ResolveAoeRadius(Row row)
    {
        string range = row.RangeDesc ?? "";
        if (range.Contains("全体")) return 8f;
        if (range.Contains("扇形")) return 5f;
        // SK005 现网 2.5（公式含「溅射攻击」）；SK020 等同溅射仍 4
        if (range.Contains("溅射") && (row.Formula ?? "").Contains("溅射攻击")) return 2.5f;
        if (row.TargetType != null && row.TargetType.Contains("单体")) return 0f;
        return 4f;
    }

    static AttackVfxKit ResolveAttackKit(Row row)
    {
        switch (row.Category)
        {
            case SkillCategory.Heal: return AttackVfxKit.Heal;
            case SkillCategory.Magic: return AttackVfxKit.Orb;
            case SkillCategory.Defense: return AttackVfxKit.Heal;
            // 物攻不写死刀光：由施法者 GetBasicAttackVfxKit 在 PlaySkillVfx 时纠正
            default: return AttackVfxKit.None;
        }
    }

    static SkillSystem.SkillType ResolveSkillType(Row row, SkillConfig cfg)
    {
        if (row.Category == SkillCategory.Heal || cfg.healPercentOfMax > 0f || IsHealActiveId(row.Id))
            return SkillSystem.SkillType.Buff;
        if (row.Category == SkillCategory.Defense && row.TargetType != null
            && (row.TargetType.Contains("自身") || row.TargetType.Contains("全体")))
            return SkillSystem.SkillType.Buff;
        if (row.Category == SkillCategory.Magic && (row.Formula ?? "").Contains("目标攻击"))
            return SkillSystem.SkillType.Buff;
        if (row.RangeDesc != null && (row.RangeDesc.Contains("全体") || row.RangeDesc.Contains("扇形") || row.RangeDesc.Contains("溅射")))
            return SkillSystem.SkillType.AOE;
        if (row.TargetType != null && row.TargetType.Contains("单体"))
            return SkillSystem.SkillType.SingleTarget;
        return SkillSystem.SkillType.AOE;
    }

    /// <summary>主动治疗技：优先表 Category=恢复；缺表回退现网 SK004/013/015。</summary>
    public static bool IsHealActiveId(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        if (TryGet(id, out var row))
            return !row.IsPassive && row.Category == SkillCategory.Heal;
        return id == "SK004" || id == "SK013" || id == "SK015";
    }

    public static Sprite LoadIcon(string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return null;
        string path = ContentPaths.Icons.MercSkill + "/" + skillId;
        var sp = Resources.Load<Sprite>(path);
        if (sp != null) return sp;
        var all = Resources.LoadAll<Sprite>(path);
        if (all != null && all.Length > 0) return all[0];
        return null;
    }
}
