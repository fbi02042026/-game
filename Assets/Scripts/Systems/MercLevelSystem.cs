using UnityEngine;

/// <summary>
/// 佣兵等级（2026-11-26 主人拍板：<b>只在战斗内</b>）。
///
/// 口径（一句话）：<b>战斗内的佣兵等级存在存档；局内再抽到同一个佣兵 → 该佣兵 +1 级。
/// 战斗外完全不受影响：不读 permanentMercs、也不写回 permanentMercs。</b>
///   · 真源：<see cref="SaveData.mercLevels"/>（hireId 键）；<b>无记录 = Lv1</b>，不回落局外的永久等级。
///   · 涨级：局内抽奖 / 三选一再抽到「已在本局队伍」的佣兵 → +1 级
///     （卡由 DraftPool.CollectMercLevelCards 产出，走 DraftCardKind.MercLevelUp）。
///     <para>为什么不用击杀经验：主人 2026-11-26 拍板「打怪升级玩家会刷」；</para>
///     <para>为什么不用局内升星：会与局外 MercStarSystem（★0~3）撞车。</para>
///   · 升级即时写存档、即时生效（RunDraftDirector 的 MercLevelUp 分支调 RefreshMercUnit 刷场上单位）。
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
    /// 等级上限（唯一常量）。抽到重复 +1 级，一局约 2~4 张佣兵卡，30 级要多局累积。
    /// 想改上限只改这一个数，表与 UI 都按它走。
    /// </summary>
    public const int MaxLevel = 30;

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
    /// 加减等级（默认 +1），钳在 1~MaxLevel，返回加完之后的等级。
    /// 只写存档 mercLevels（不镜像 permanentMercs），不碰场上单位 —— 生效由调用方负责
    /// （RunDraftDirector 的 MercLevelUp 分支调 RefreshMercUnit）。
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
        // 【2026-11-26 主人拍板·彻底隔离】不写回 permanentMercs：战斗等级只在战斗内用，
        // 局外界面显示自己的永久等级（招募随机 1~10 那套）。
        return level;
    }
}
