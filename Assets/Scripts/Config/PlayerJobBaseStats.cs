using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家职业固定基础属性（无账号等级成长；成长列留待专属武器强化二期）。
/// 战斗攻击距离只走 <see cref="AttackRangeTable"/>；CSV「攻击距离」列仅策划对照，已弃用于战斗。
/// </summary>
public static class PlayerJobBaseStats
{
    public struct Row
    {
        public string ConfigId;
        public string Name;
        public float BaseHp;
        public float BaseAtk;
        public float BaseDef;
        public float BaseMoveSpeed;
        public float CritRate;
        public float CritDamage;
        /// <summary>CSV 攻击距离（像素），仅对照；战斗勿用。</summary>
        public float AttackRangePx;
        /// <summary>CSV「攻击间隔」列，单位秒（0.5=半秒）。写入 AttrType.AttackSpeed=1/间隔。</summary>
        public float AttackInterval;
        public float CollisionScale;
        public string StarterWeaponId;
        /// <summary>2026-09-26 新增表列：起步主手武器模板 id（第 14 列）。空 = 回退 PlayerJobDefs 里的写死值。</summary>
        public string StarterMainTemplateId;
        /// <summary>2026-09-26 新增表列：起步副手武器模板 id（第 15 列）。空 = 该职业不发副手。</summary>
        public string StarterOffTemplateId;
        /// <summary>
        /// 2026-09-26 主人拍板新增表列：职业伤害类型（第 16 列）—— "magic"=魔法职业(法师/牧师)，其余(空/"physical")=物理职业。
        /// 决定武器掉落按物理/魔法分池（主人要求「魔法职业只掉魔法装备」），配表不写死。
        /// </summary>
        public string DamageType;
    }

    static readonly Dictionary<string, Row> _byConfigId = new Dictionary<string, Row>();
    static readonly Dictionary<PlayerJobId, Row> _byJob = new Dictionary<PlayerJobId, Row>();
    static bool _loaded;

    public static bool HasData
    {
        get
        {
            EnsureLoaded();
            return _byJob.Count > 0;
        }
    }

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        string raw = GameTableStore.LoadText(ContentPaths.Data.PlayerJobBaseStats);
        if (string.IsNullOrEmpty(raw))
        {
            Debug.LogError("[PlayerJobBaseStats] 战斗表加载失败: Resources/" + ContentPaths.Data.PlayerJobBaseStats
                + " （空或缺失），using defaults。");
            return;
        }
        var rows = GameTableCsv.ParseRows(raw);
        for (int i = 1; i < rows.Count; i++)
        {
            var c = rows[i];
            if (c.Length < 12) continue;
            var row = new Row
            {
                ConfigId = c[0].Trim(),
                Name = c[1].Trim(),
                StarterWeaponId = c[11].Trim()
            };
            GameTableCsv.TryFloat(c[2], out row.BaseHp);
            GameTableCsv.TryFloat(c[3], out row.BaseAtk);
            GameTableCsv.TryFloat(c[4], out row.BaseDef);
            GameTableCsv.TryFloat(c[5], out row.BaseMoveSpeed);
            row.CritRate = ParsePercent(c[6]);
            row.CritDamage = ParsePercent(c[7]);
            GameTableCsv.TryFloat(c[8], out row.AttackRangePx);
            GameTableCsv.TryFloat(c[9], out row.AttackInterval);
                GameTableCsv.TryFloat(c[10], out row.CollisionScale);
                // 起步武器模板 id：旧表没有这两列，缺列时留空（回退写死值）
                if (c.Length > 13) row.StarterMainTemplateId = c[13].Trim();
                if (c.Length > 14) row.StarterOffTemplateId = c[14].Trim();
                // 2026-09-26 主人拍板：伤害类型列（第 16 列）——magic=魔法职业，缺列留空=物理
                if (c.Length > 15) row.DamageType = c[15].Trim();
                _byConfigId[row.ConfigId] = row;
            if (TryMapJob(row.ConfigId, out PlayerJobId job))
                _byJob[job] = row;
        }
        if (_byJob.Count <= 0)
            Debug.LogError("[PlayerJobBaseStats] 战斗表加载失败: Resources/" + ContentPaths.Data.PlayerJobBaseStats
                + " （解析 0 条），using defaults。");
    }

    public static bool TryGet(PlayerJobId job, out Row row)
    {
        EnsureLoaded();
        return _byJob.TryGetValue(job, out row);
    }

    /// <summary>
    /// 2026-09-26 主人拍板：该职业是否魔法职业——**读表第 16 列 damageType**，不写死。
    /// "magic"=魔法（法师/牧师）→ 武器掉落走魔法池；其余=物理。
    /// 表缺失时兜底：Mage/Priest 算魔法（与表里 P005/P006 填的一致）。
    /// </summary>
    public static bool IsMagicJob(PlayerJobId job)
    {
        if (TryGet(job, out var row) && !string.IsNullOrEmpty(row.DamageType))
            return row.DamageType.Equals("magic", System.StringComparison.OrdinalIgnoreCase);
        return job == PlayerJobId.Mage || job == PlayerJobId.Priest;
    }

    /// <summary>开战套用固定基础属性（覆盖当前战斗属性核心项）。</summary>
    public static void ApplyToHero(Hero hero)
    {
        if (hero == null || hero.attr == null) return;
        ApplyToAttr(hero.attr, PlayerJobDefs.GetSelected());
        hero.currentHp = hero.attr.GetAttr(AttrType.MaxHp);
    }

    /// <summary>把职业表写入当前属性（射程只读 AttackRangeTable）。</summary>
    public static void ApplyToAttr(AttrSystem attr, PlayerJobId job)
    {
        WriteCombat(attr, job, toBase: false);
    }

    /// <summary>职业表直接写入基底，避免先写 GameConfig.BASE_* 再覆盖。</summary>
    public static bool TryWriteCombatBases(AttrSystem attr, PlayerJobId job)
    {
        return WriteCombat(attr, job, toBase: true);
    }

    static bool WriteCombat(AttrSystem attr, PlayerJobId job, bool toBase)
    {
        if (attr == null) return false;
        EnsureLoaded();
        if (!TryGet(job, out Row row)) return false;

        void W(AttrType type, float value)
        {
            if (toBase) attr.SetBaseAndCurrent(type, value);
            else attr.SetAttr(type, value);
        }

        W(AttrType.MaxHp, row.BaseHp);
        W(AttrType.Attack, Mathf.Max(1f, row.BaseAtk + GameConfig.HERO_BASE_ATTACK_OFFSET));
        W(AttrType.Defense, row.BaseDef);
        float ms = GameConfig.BASE_MOVE_SPEED * (row.BaseMoveSpeed / 100f);
        W(AttrType.MoveSpeed, ms);
        W(AttrType.CritRate, row.CritRate);
        // 2026-09-26 主人拍板：暴击倍率统一 = 2，不再读本表「暴击伤害」列（原 150%~180%）。
        // 表里那一列已同步改成 200%，这里仍以 GameConfig.CRIT_MULTIPLIER 为准，避免以后改表又漂回去。
        W(AttrType.CritDamage, GameConfig.CRIT_MULTIPLIER);
        if (row.AttackInterval > 0.05f)
            W(AttrType.AttackSpeed, 1f / row.AttackInterval);
        else if (toBase)
            W(AttrType.AttackSpeed, GameConfig.BASE_ATTACK_SPEED);
        W(AttrType.AttackRange, AttackRangeTable.GetJobWorld(job));
        return true;
    }

    static float ParsePercent(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0f;
        s = s.Trim().Replace("%", "");
        float.TryParse(s, out float v);
        return v * 0.01f;
    }

    static bool TryMapJob(string configId, out PlayerJobId job)
    {
        switch (configId)
        {
            case "P001": job = PlayerJobId.SwordShield; return true;
            case "P002": job = PlayerJobId.Heavy; return true;
            case "P003": job = PlayerJobId.Berserker; return true;
            case "P004": job = PlayerJobId.Ranger; return true;
            case "P005": job = PlayerJobId.Mage; return true;
            case "P006": job = PlayerJobId.Priest; return true;
            default:
                job = PlayerJobId.SwordShield;
                return false;
        }
    }
}
