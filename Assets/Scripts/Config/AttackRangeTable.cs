using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 运行时攻击距离唯一数据源。读 Cook 后的 attack_range.bytes。
/// player_job_base_stats 的「攻击距离」列仅作策划对照，不写入战斗属性。
/// </summary>
public static class AttackRangeTable
{
    static readonly Dictionary<string, float> _px = new Dictionary<string, float>();
    static bool _loaded;
    static bool _emptyWarned;

    public static bool HasData
    {
        get
        {
            EnsureLoaded();
            return _px.Count > 0;
        }
    }

    public static void Reload()
    {
        _loaded = false;
        _emptyWarned = false;
        _px.Clear();
        EnsureLoaded();
    }

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        _px.Clear();

        string raw = GameTableStore.LoadText(ContentPaths.Data.AttackRange);
        if (string.IsNullOrEmpty(raw))
        {
            WarnEmpty("空或缺失");
            return;
        }

        var rows = GameTableCsv.ParseRows(raw);
        int ok = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            var c = rows[i];
            if (c.Length < 2) continue;
            string id = c[0].Trim();
            if (string.IsNullOrEmpty(id) || id == "id" || id.StartsWith("#")) continue;
            if (!GameTableCsv.TryFloat(c[1], out float px) || px <= 0f) continue;
            _px[id] = px;
            ok++;
        }

        if (ok <= 0)
            WarnEmpty("解析 0 条");
        else
            Debug.Log($"[AttackRangeTable] 已加载 {ok} 条射程");
    }

    static void WarnEmpty(string why)
    {
        if (_emptyWarned) return;
        _emptyWarned = true;
        Debug.LogError("[AttackRangeTable] 战斗表加载失败: Resources/" + ContentPaths.Data.AttackRange
            + " （" + why + "），using defaults（GameConfig.RANGE_PX_*）。");
    }

    public static float GetPx(string id, float fallbackPx)
    {
        EnsureLoaded();
        if (!string.IsNullOrEmpty(id) && _px.TryGetValue(id, out float px) && px > 0f)
            return px;
        return fallbackPx;
    }

    public static float GetWorld(string id, float fallbackPx)
    {
        return GameConfig.NormalizeAttackRange(GetPx(id, fallbackPx));
    }

    public static float GetJobWorld(PlayerJobId job)
    {
        EnsureLoaded();
        string key = JobKey(job);
        if (_px.TryGetValue(key, out float px) && px > 0f)
            return GameConfig.NormalizeAttackRange(px);
        string alias = JobConfigAlias(job);
        if (_px.TryGetValue(alias, out px) && px > 0f)
            return GameConfig.NormalizeAttackRange(px);
        return GameConfig.PixelsToUnits(GameConfig.RANGE_PX_SWORD);
    }

    /// <summary>
    /// 武器射程（像素）。fallback 用字面量，避免与 GameConfig.RANGE_PX_* 互相递归。
    /// </summary>
    public static float GetWeaponPx(WeaponCombatTable.WeaponKind kind)
    {
        switch (kind)
        {
            case WeaponCombatTable.WeaponKind.Greatsword:
                return GetPx("weapon_greatsword", 144f);
            case WeaponCombatTable.WeaponKind.Polearm:
                return GetPx("weapon_polearm", 180f);
            case WeaponCombatTable.WeaponKind.Staff:
                return GetPx("weapon_staff", 120f);
            case WeaponCombatTable.WeaponKind.Bow:
                return GetPx("weapon_bow", 300f);
            case WeaponCombatTable.WeaponKind.Shield:
                return GetPx("weapon_shield", 64f);
            default:
                return GetPx("weapon_sword", 96f);
        }
    }

    public static float GetWeaponWorld(WeaponCombatTable.WeaponKind kind)
        => GameConfig.NormalizeAttackRange(GetWeaponPx(kind));

    /// <summary>佣兵：按 AssetId 前缀对齐职业/武器档。</summary>
    public static float GetMercWorld(string assetId)
    {
        if (string.IsNullOrEmpty(assetId))
            return GetWeaponWorld(WeaponCombatTable.WeaponKind.Sword);
        if (assetId.StartsWith("gongshou"))
            return GetWeaponWorld(WeaponCombatTable.WeaponKind.Bow);
        if (assetId.StartsWith("fashi"))
            return GetJobWorld(PlayerJobId.Mage);
        if (assetId.StartsWith("naima") || assetId.StartsWith("mushi"))
            return GetJobWorld(PlayerJobId.Priest);
        if (assetId.StartsWith("zhongzhan") || assetId.StartsWith("qita"))
            return GetWeaponWorld(WeaponCombatTable.WeaponKind.Polearm);
        if (assetId.StartsWith("kuangzhan"))
            return GetWeaponWorld(WeaponCombatTable.WeaponKind.Greatsword);
        return GetWeaponWorld(WeaponCombatTable.WeaponKind.Sword);
    }

    public static float GetMonsterWorld(MonsterAttackStyle style)
    {
        if (style == MonsterAttackStyle.Bow)
            return GetWorld("monster_bow", GameConfig.RANGE_PX_BOW * GameConfig.MONSTER_RANGED_RANGE_MUL);
        if (MonsterAttackStyleTable.IsRanged(style))
            return GetWorld("monster_ranged", GameConfig.RANGE_PX_BOW * GameConfig.MONSTER_RANGED_RANGE_MUL);
        return GetWorld("monster_melee", GameConfig.RANGE_PX_SWORD * GameConfig.MONSTER_MELEE_RANGE_MUL);
    }

    public static string JobKey(PlayerJobId job)
    {
        switch (job)
        {
            case PlayerJobId.Heavy: return "job_heavy";
            case PlayerJobId.Berserker: return "job_berserker";
            case PlayerJobId.Ranger: return "job_ranger";
            case PlayerJobId.Mage: return "job_mage";
            case PlayerJobId.Priest: return "job_priest";
            default: return "job_sword_shield";
        }
    }

    static string JobConfigAlias(PlayerJobId job)
    {
        switch (job)
        {
            case PlayerJobId.Heavy: return "job_P002";
            case PlayerJobId.Berserker: return "job_P003";
            case PlayerJobId.Ranger: return "job_P004";
            case PlayerJobId.Mage: return "job_P005";
            case PlayerJobId.Priest: return "job_P006";
            default: return "job_P001";
        }
    }
}
