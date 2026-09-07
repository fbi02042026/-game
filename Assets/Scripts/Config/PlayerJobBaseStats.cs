using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家职业固定基础属性（无账号等级成长；成长列留待专属武器强化二期）。
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
        public float AttackRangePx;
        public float AttackInterval;
        public float CollisionScale;
        public string StarterWeaponId;
    }

    static readonly Dictionary<string, Row> _byConfigId = new Dictionary<string, Row>();
    static readonly Dictionary<PlayerJobId, Row> _byJob = new Dictionary<PlayerJobId, Row>();
    static bool _loaded;

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        string raw = GameTableStore.LoadText(ContentPaths.Data.PlayerJobBaseStats);
        if (string.IsNullOrEmpty(raw))
        {
            Debug.LogWarning("[PlayerJobBaseStats] 未找到 player_job_base_stats 表");
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
        EnsureLoaded();
        if (!TryGet(PlayerJobDefs.GetSelected(), out Row row)) return;

        hero.attr.SetAttr(AttrType.MaxHp, row.BaseHp);
        hero.attr.SetAttr(AttrType.Attack, row.BaseAtk);
        hero.attr.SetAttr(AttrType.Defense, row.BaseDef);
        // 表内移速以 100 为基准，映射到现有世界移速
        float ms = GameConfig.BASE_MOVE_SPEED * (row.BaseMoveSpeed / 100f);
        hero.attr.SetAttr(AttrType.MoveSpeed, ms);
        hero.attr.SetAttr(AttrType.CritRate, row.CritRate);
        if (row.AttackInterval > 0.05f)
            hero.attr.SetAttr(AttrType.AttackSpeed, 1f / row.AttackInterval);
        if (row.AttackRangePx > 0f)
            hero.attr.SetAttr(AttrType.AttackRange, GameConfig.NormalizeAttackRange(row.AttackRangePx));
        hero.currentHp = hero.attr.GetAttr(AttrType.MaxHp);
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
