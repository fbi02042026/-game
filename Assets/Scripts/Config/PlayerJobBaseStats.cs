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
        W(AttrType.Attack, row.BaseAtk);
        W(AttrType.Defense, row.BaseDef);
        float ms = GameConfig.BASE_MOVE_SPEED * (row.BaseMoveSpeed / 100f);
        W(AttrType.MoveSpeed, ms);
        W(AttrType.CritRate, row.CritRate);
        W(AttrType.CritDamage, row.CritDamage > 0f ? row.CritDamage : GameConfig.DefaultCritMultiplier);
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
