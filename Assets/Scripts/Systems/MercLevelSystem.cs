using UnityEngine;

/// <summary>
/// 佣兵等级（2026-11-26 主人拍板：<b>只在战斗内</b>）。
///
/// 口径（一句话）：<b>战斗内的佣兵等级存在存档；打怪得经验、经验满自动升级；
/// 抽卡升级卡改为给一份经验（不再是抽一次直接 +1 级）。战斗外完全不受影响。</b>
///   · 真源：等级 <see cref="SaveData.mercLevels"/>、当前级内经验 <see cref="SaveData.mercExp"/>
///     （都是 hireId 键）；<b>等级无记录 = Lv1、经验无记录 = 0</b>，不回落局外的永久等级。
///   · 涨级：① 击杀怪物（击杀者全额、其他存活佣兵半额，见 BattleManager.MonsterDeath）；
///     ② 抽到 MercLevelUp 卡 = 给「当前级所需经验的一半」（两张卡约升一级）。
///     <para>2026-11-28 主人拍板覆盖 11-26 的「只靠抽卡 +1 级、不走击杀经验」口径。</para>
///     <para>为什么不用局内升星：会与局外 MercStarSystem（★0~3）撞车。</para>
///   · 升级即时写存档、即时生效（调用方负责 RefreshMercUnit 刷场上单位与头像栏经验条）。
///   · 【2026-11-26 主人拍板·彻底隔离】战斗外界面（酒馆招募 / 进阶 / 佣兵池详情）一律读
///     permanentMercs 的旧等级，禁止改读本类；本类也不许把结果写回 permanentMercs。
///
/// 与其他两套的分工（禁止混用）：
///   · 升星 MercStarSystem / MercStarDefs：★0~3，只管星级乘区；
///   · 技能强化档 MercGrowSystem.SkillTierOf：按等级推导（Lv10 起），本类不改它。
/// </summary>
public static class MercLevelSystem
{
    /// <summary>
    /// 等级上限（唯一常量）。【2026-11-28 主人拍板】30 → 60。
    /// 想改上限只改这一个数，表与 UI 都按它走。
    /// </summary>
    public const int MaxLevel = 60;

    /// <summary>
    /// 升到下一级所需经验 = 160 + 12×当前等级（唯一曲线）。
    /// 按击杀经验 普通8/精英20/Boss50 推：Lv1 约 22 只普通怪、Lv30 约 65 只、Lv59 约 109 只。
    /// 经验曲线与击杀经验常量都不在这里写死第二份，改数值只动本方法。
    /// 满级返回 0（UI 按 0 显示满条）。
    /// </summary>
    public static int ExpForLevel(int currentLevel)
    {
        if (currentLevel >= MaxLevel) return 0;
        return 160 + 12 * Mathf.Max(1, currentLevel);
    }

    /// <summary>当前级内已攒经验（满级恒 0）。</summary>
    public static int ExpOf(string hireIdOrAssetId)
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null) return 0;
        string hireId = MercRosterDefs.ResolveHireId(hireIdOrAssetId);
        if (string.IsNullOrEmpty(hireId) || IsMaxLevel(hireId)) return 0;
        return Mathf.Max(0, data.GetMercExpRaw(hireId));
    }

    /// <summary>经验条比例 0~1（满级 = 1）。</summary>
    public static float ExpRatioOf(string hireIdOrAssetId)
    {
        if (IsMaxLevel(hireIdOrAssetId)) return 1f;
        int need = ExpForLevel(LevelOf(hireIdOrAssetId));
        return need > 0 ? Mathf.Clamp01(ExpOf(hireIdOrAssetId) / (float)need) : 0f;
    }

    /// <summary>该佣兵当前战斗等级（1~MaxLevel；存档无记录 = Lv1，不回落局外永久等级）。</summary>
    public static int LevelOf(string hireIdOrAssetId)
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null) return 1;

        string hireId = MercRosterDefs.ResolveHireId(hireIdOrAssetId);
        if (string.IsNullOrEmpty(hireId)) return 1;

        int raw = data.GetMercLevelRaw(hireId);
        if (raw > 0) return Mathf.Clamp(raw, 1, MaxLevel);

        // 【2026-11-26 主人拍板·彻底隔离】无记录 = Lv1，<b>不再回落</b> permanentMercs.level：
        // 那是战斗外招募摇出来的数，两边必须互不读（否则局外等级会漏进战斗）。
        return 1;
    }

    /// <summary>是否已满级（满级后不再产升级卡，见 DraftPool.CollectMercLevelCards）。</summary>
    public static bool IsMaxLevel(string hireIdOrAssetId) => LevelOf(hireIdOrAssetId) >= MaxLevel;

    /// <summary>
    /// 加经验（唯一涨级出口），可一次连升多级；返回的等级与 levelsGained 由调用方决定
    /// 是否播升级特效 / 刷场上单位。只写存档 mercLevels + mercExp，不碰场上单位。
    /// 满级不再吃经验（不写盘）。
    /// </summary>
    public static int AddExp(string hireIdOrAssetId, int amount, out int levelsGained)
    {
        levelsGained = 0;
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        string hireId = MercRosterDefs.ResolveHireId(hireIdOrAssetId);
        if (data == null || string.IsNullOrEmpty(hireId) || amount <= 0) return LevelOf(hireIdOrAssetId);

        int level = LevelOf(hireId);
        if (level >= MaxLevel) return level;   // 满级：经验不涨、不写盘

        int exp = Mathf.Max(0, data.GetMercExpRaw(hireId)) + amount;
        while (level < MaxLevel)
        {
            int need = ExpForLevel(level);
            if (need <= 0 || exp < need) break;
            exp -= need;          // 只扣本级门槛，余量滚存到下一级
            level++;
            levelsGained++;
        }
        if (level >= MaxLevel) exp = 0;   // 满级不留半截经验

        data.SetMercLevel(hireId, level);
        data.SetMercExp(hireId, exp);
        // 【2026-11-26 主人拍板·彻底隔离】不写回 permanentMercs：战斗等级只在战斗内用。
        return level;
    }

    /// <summary>加经验的简写（不关心升了几级时用）。</summary>
    public static int AddExp(string hireIdOrAssetId, int amount)
        => AddExp(hireIdOrAssetId, amount, out _);

    /// <summary>
    /// 直接加减等级（默认 +1），钳在 1~MaxLevel，返回加完之后的等级。
    /// 【2026-11-28 起不再用于日常涨级】涨级一律走 AddExp；本方法只留给「直接设定等级」的兼容点，
    /// 调用时会清掉该佣兵当前级内经验，避免跨级残留。
    /// </summary>
    public static int AddLevel(string hireIdOrAssetId, int delta = 1)
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        string hireId = MercRosterDefs.ResolveHireId(hireIdOrAssetId);
        if (data == null || string.IsNullOrEmpty(hireId) || delta == 0) return LevelOf(hireIdOrAssetId);

        int before = LevelOf(hireId);
        int level = Mathf.Clamp(before + delta, 1, MaxLevel);
        if (level == before) return level;   // 满级 +1 / 1 级 -1：不写存档

        data.SetMercLevel(hireId, level);
        data.SetMercExp(hireId, 0);
        return level;
    }
}
