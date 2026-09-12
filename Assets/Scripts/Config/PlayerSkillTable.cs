using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家技能表（读 Cook 后的 player_skills.bytes）。
/// 元数据 + 战斗数值（倍率/范围/治疗/Buff）。伤害数学不读 Ally SO。
/// </summary>
public static class PlayerSkillTable
{
    public struct Row
    {
        public string Id;
        public string DisplayName;
        public PlayerSkillDefs.Kind Kind;
        public string Desc;
        public string Numbers;
        public float Cooldown;
        public float Duration;
        public string UseHint;
        public int UnlockChapter;
        public string AllyConfigId;
        public SkillSystem.SkillType SkillType;
        public AttackVfxKit AttackKit;
        public float DamageMultiplier;
        public float BaseDamage;
        public float AoeRadius;
        public int ProjectileCount;
        public float ProjectileSpeed;
        public AttrType BuffAttr;
        public float BuffValue;
        public bool BuffIsPercent;
        public float HealBase;
        public float HealPercentOfMax;
        public float EnergyCost;
        public bool HasCombat;
    }

    static readonly List<Row> _rows = new List<Row>();
    static readonly Dictionary<string, Row> _byId = new Dictionary<string, Row>();
    static bool _loaded;

    public static bool HasData
    {
        get
        {
            EnsureLoaded();
            return _rows.Count > 0;
        }
    }

    public static IReadOnlyList<Row> Rows
    {
        get
        {
            EnsureLoaded();
            return _rows;
        }
    }

    public static void Reload()
    {
        _loaded = false;
        _rows.Clear();
        _byId.Clear();
        EnsureLoaded();
    }

    public static bool TryGet(string id, out Row row)
    {
        EnsureLoaded();
        row = default;
        if (string.IsNullOrEmpty(id)) return false;
        return _byId.TryGetValue(id, out row);
    }

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        _rows.Clear();
        _byId.Clear();

        string raw = GameTableStore.LoadText(ContentPaths.Data.PlayerSkills);
        if (string.IsNullOrEmpty(raw))
        {
            Debug.LogWarning("[PlayerSkillTable] 表缺失，使用 PlayerSkillDefs.Fallback");
            return;
        }

        var parsed = GameTableCsv.ParseRows(raw);
        int ok = 0;
        for (int i = 0; i < parsed.Count; i++)
        {
            var c = parsed[i];
            if (c.Length < 10) continue;
            if (c[0] == "id" || c[0].StartsWith("#")) continue;
            string id = c[0].Trim();
            if (string.IsNullOrEmpty(id)) continue;
            if (!TryParseKind(c[2].Trim(), out PlayerSkillDefs.Kind kind))
            {
                Debug.LogError("[PlayerSkillTable] 未知 kind，跳过行: " + id + " / " + c[2]);
                continue;
            }

            GameTableCsv.TryFloat(c[5], out float cd);
            GameTableCsv.TryFloat(c[6], out float dur);
            GameTableCsv.TryInt(c[8], out int unlock);
            var row = new Row
            {
                Id = id,
                DisplayName = c[1].Trim(),
                Kind = kind,
                Desc = c[3].Trim(),
                Numbers = c[4].Trim(),
                Cooldown = cd,
                Duration = dur,
                UseHint = c[7].Trim(),
                UnlockChapter = unlock,
                AllyConfigId = c[9].Trim(),
                SkillType = SkillSystem.SkillType.Buff,
                AttackKit = AttackVfxKit.None,
                ProjectileCount = 1,
                ProjectileSpeed = 12f,
                EnergyCost = 1f
            };
            if (c.Length >= 22)
            {
                row.HasCombat = true;
                row.SkillType = ParseSkillType(Col(c, 10));
                row.AttackKit = ParseAttackKit(Col(c, 11));
                GameTableCsv.TryFloat(Col(c, 12), out float dmgMul);
                GameTableCsv.TryFloat(Col(c, 13), out float baseDmg);
                GameTableCsv.TryFloat(Col(c, 14), out float aoe);
                GameTableCsv.TryInt(Col(c, 15), out int pn);
                GameTableCsv.TryFloat(Col(c, 16), out float projSpd);
                GameTableCsv.TryFloat(Col(c, 18), out float buffVal);
                GameTableCsv.TryBool(Col(c, 19), out bool buffPct);
                GameTableCsv.TryFloat(Col(c, 20), out float healBase);
                GameTableCsv.TryFloat(Col(c, 21), out float healPct);
                row.DamageMultiplier = dmgMul;
                row.BaseDamage = baseDmg;
                row.AoeRadius = aoe;
                if (pn > 0) row.ProjectileCount = pn;
                if (projSpd > 0f) row.ProjectileSpeed = projSpd;
                row.BuffAttr = ParseBuffAttr(Col(c, 17));
                row.BuffValue = buffVal;
                row.BuffIsPercent = buffPct;
                row.HealBase = healBase;
                row.HealPercentOfMax = healPct;
                if (c.Length > 22)
                {
                    GameTableCsv.TryFloat(Col(c, 22), out float energy);
                    if (energy > 0f) row.EnergyCost = energy;
                }
            }
            _rows.Add(row);
            _byId[id] = row;
            ok++;
        }

        if (ok <= 0)
            Debug.LogWarning("[PlayerSkillTable] 解析 0 条，使用 PlayerSkillDefs.Fallback");
        else
            Debug.Log($"[PlayerSkillTable] 已加载 {ok} 条玩家技能元数据");
    }

    static bool TryParseKind(string s, out PlayerSkillDefs.Kind kind)
    {
        kind = PlayerSkillDefs.Kind.Heal;
        if (string.IsNullOrEmpty(s)) return false;
        switch (s)
        {
            case "Heal": kind = PlayerSkillDefs.Kind.Heal; return true;
            case "Shield": kind = PlayerSkillDefs.Kind.Shield; return true;
            case "AtkBuff": kind = PlayerSkillDefs.Kind.AtkBuff; return true;
            case "AtkSpeedBuff": kind = PlayerSkillDefs.Kind.AtkSpeedBuff; return true;
            case "CritBuff": kind = PlayerSkillDefs.Kind.CritBuff; return true;
            case "Aoe": kind = PlayerSkillDefs.Kind.Aoe; return true;
            default: return false;
        }
    }

    static string Col(string[] c, int i)
    {
        return i < c.Length ? c[i] : "";
    }

    static SkillSystem.SkillType ParseSkillType(string s)
    {
        switch ((s ?? "").Trim())
        {
            case "SingleTarget": return SkillSystem.SkillType.SingleTarget;
            case "Projectile": return SkillSystem.SkillType.Projectile;
            case "AOE": return SkillSystem.SkillType.AOE;
            case "Chain": return SkillSystem.SkillType.Chain;
            default: return SkillSystem.SkillType.Buff;
        }
    }

    static AttackVfxKit ParseAttackKit(string s)
    {
        switch ((s ?? "").Trim())
        {
            case "MeleeSlash": return AttackVfxKit.MeleeSlash;
            case "Bow": return AttackVfxKit.Bow;
            case "Orb": return AttackVfxKit.Orb;
            case "Heal": return AttackVfxKit.Heal;
            default: return AttackVfxKit.None;
        }
    }

    static AttrType ParseBuffAttr(string s)
    {
        switch ((s ?? "").Trim())
        {
            case "MaxHp": return AttrType.MaxHp;
            case "Attack": return AttrType.Attack;
            case "AttackSpeed": return AttrType.AttackSpeed;
            case "CritRate": return AttrType.CritRate;
            case "Defense": return AttrType.Defense;
            case "MoveSpeed": return AttrType.MoveSpeed;
            default: return AttrType.Attack;
        }
    }

    public static bool TryGetByAllyConfigId(string allyId, out Row row)
    {
        EnsureLoaded();
        row = default;
        if (string.IsNullOrEmpty(allyId)) return false;
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].AllyConfigId == allyId)
            {
                row = _rows[i];
                return true;
            }
        }
        return false;
    }

    /// <summary>用表行合成运行时 SkillConfig。id 用 allyConfigId 以便 VFX 路径不变。</summary>
    public static SkillConfig BuildRuntimeConfig(PlayerSkillDefs.Def def)
    {
        if (def == null) return null;
        var cfg = ScriptableObject.CreateInstance<SkillConfig>();
        cfg.id = string.IsNullOrEmpty(def.allyConfigId) ? def.id : def.allyConfigId;
        cfg.skillName = def.displayName;
        cfg.desc = def.desc;
        cfg.skillType = def.skillType;
        cfg.attackKit = def.attackKit;
        cfg.damageMultiplier = def.damageMultiplier;
        cfg.baseDamage = def.baseDamage;
        cfg.cooldown = def.cooldown;
        cfg.aoeRadius = def.aoeRadius;
        cfg.projectileCount = def.projectileCount > 0 ? def.projectileCount : 1;
        cfg.projectileSpeed = def.projectileSpeed;
        cfg.buffAttr = def.buffAttr;
        cfg.buffValue = def.buffValue;
        cfg.buffIsPercent = def.buffIsPercent;
        cfg.duration = def.duration;
        cfg.healBase = def.healBase;
        cfg.healPercentOfMax = def.healPercentOfMax;
        return cfg;
    }
}
