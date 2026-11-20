using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 乱入事件·敌对乱入战斗单位（批次6 2026-11-20 拍板）：固定反派三人组 merc AssetId（非随机）。
/// 🔴 只产出数据（AssetId 数组），不加载 prefab、不生成单位、不改波次 —— 生成交给 T2。
/// 🔴 绝不调 MercenaryManager.SpawnEnemyHeroFigure，绝不加载 ArenaRules.HeroFigurePrefab（"Units/wanjia"）。
/// 池与去重口径复用 ArenaRules.MercPoolFor / PickMercAssetIds（Assets/Scripts/Combat/ArenaRules.cs:624/648）：
/// MercRosterDefs.All 按 AssetId 去重、排除玩家形象、队内不重复；随机用 System.Random(seed) 与 ArenaRules 同款。
/// 【Q2 拍板建议 · 本批按「排除」实现】英雄位 heroAssetId 连同三职业 101 脸一起排除：
/// 见 ArenaRules.BotTeamTemplates[].heroAssetId（ArenaRules.cs:562-567）与
/// PlayerJobDefs.Playable（PlayerJobDefs.cs:299-304）。
/// </summary>
public static class IntruderPicker
{
    public const int PickCount = 3;

    /// <summary>
    /// 批次6 拍板：固定反派三人组战斗模板，与剧情立绘同一套人设（IntruderStoryBeats.BanditBoss/
    /// BanditWitch/BanditBlade），避免「剧情独眼老雷、战斗随机路人」对不上。
    /// 三档均为非 101 模板：玩家六职业 MercAssetId 全是 *101（PlayerJobDefs.cs:77-191），
    /// 救场小队 dunbing203/gongshou101/naima102（MercRosterDefs），零撞脸。
    /// </summary>
    static readonly string[] FixedBanditAssetIds =
    {
        "zhongzhan201",   // 独眼老雷 · 重武大哥（稀有档重武者）
        "fashi202",       // 夜薇 · 女法师（雷系稀有档）
        "kuangzhan201",   // 阿刃 · 狂战小弟（稀有档狂战士）
    };

    /// <summary>
    /// 批次6 拍板：固定返回反派三人组（不再随机抽）。seed 保留入参仅作日志回溯，不参与抽取。
    /// 下方旧随机抽取逻辑（BuildExcludedAssetIds / BuildPool / TryExcludeSquadAssetId）
    /// 保留空转（同 FORMAL_ROUND_WAVE_CUT* 先例）：若回滚随机口径可整体复用，勿删。
    /// </summary>
    public static bool TryPick(int seed, out string[] assetIds)
    {
        assetIds = (string[])FixedBanditAssetIds.Clone();
        Debug.Log($"[IntruderPicker] 固定反派三人组 seed={seed} ids={string.Join(",", assetIds)}");
        return true;
    }

    /// <summary>排除集：ArenaRules 英雄位 heroAssetId + PlayerJobDefs 三职业 101 脸（Q2 建议排除）。</summary>
    static HashSet<string> BuildExcludedAssetIds()
    {
        var excluded = new HashSet<string>(System.StringComparer.Ordinal);
        for (int i = 0; i < ArenaRules.BotTeamTemplates.Length; i++)
        {
            string hero = ArenaRules.BotTeamTemplates[i].heroAssetId;
            if (!string.IsNullOrEmpty(hero)) excluded.Add(hero);
        }
        for (int i = 0; i < PlayerJobDefs.Playable.Length; i++)
        {
            var def = PlayerJobDefs.Get(PlayerJobDefs.Playable[i]);
            if (!string.IsNullOrEmpty(def.MercAssetId)) excluded.Add(def.MercAssetId);
        }
        // ===== 修点③（2026-11-20 二次拍板⑦）：排除救场小队三名 =====
        // 口径：救场小队（加百列 H023 / 艾拉 H015 / 塞拉 H012）绝不以敌对乱入身份出现——
        // 同一张脸既当救场队友又当拦路敌人，"同一支队伍暗中保护玩家"的剧情就穿帮。
        // 取数走 MercRosterDefs.TryGetByHireId 的 AssetId（T1 口径），不散写字符串；
        // 艾拉 gongshou101 虽已被上面三职业 101 脸覆盖，仍按拍板三名全列（幂等，防未来 101 口径改动）。
        // 静态推演：花名册去重 20 个 AssetId − 三职业101 − dunbing203 − naima102 = 15 ≥ PickCount*2，抽 3 无压力。
        TryExcludeSquadAssetId(excluded, "H023");   // 加百列 dunbing203
        TryExcludeSquadAssetId(excluded, "H015");   // 艾拉 gongshou101
        TryExcludeSquadAssetId(excluded, "H012");   // 塞拉 naima102
        return excluded;
    }

    /// <summary>修点③：按 hireId 取花名册 AssetId 入排除集；查不到 = 表被改，LogError 露出不静默兜底。</summary>
    static void TryExcludeSquadAssetId(HashSet<string> excluded, string hireId)
    {
        // MercRosterDefs.Def 是 struct（MercRosterDefs.cs:19），只判 AssetId 空值
        if (!MercRosterDefs.TryGetByHireId(hireId, out var d)
            || string.IsNullOrEmpty(d.AssetId))
        {
            Debug.LogError($"[IntruderPicker] 救场小队 {hireId} 在 MercRosterDefs 查不到 AssetId，排除集缺员（花名册被改？）IntruderPicker.cs TryExcludeSquadAssetId");
            return;
        }
        excluded.Add(d.AssetId);
    }

    /// <summary>同 ArenaRules.MercPoolFor：花名册按 AssetId 去重，排除英雄位 / 101 脸 / 已选。</summary>
    static List<string> BuildPool(HashSet<string> excluded, List<string> taken)
    {
        var pool = new List<string>();
        var all = MercRosterDefs.All;
        for (int i = 0; i < all.Count; i++)
        {
            string id = all[i].AssetId;
            if (string.IsNullOrEmpty(id)) continue;
            if (excluded.Contains(id)) continue;
            if (taken.Contains(id)) continue;       // 队内不重复（ArenaRules.MercPoolFor:658 口径）
            if (!pool.Contains(id)) pool.Add(id);   // 同 AssetId 多 hire 只留一个（:659 口径）
        }
        return pool;
    }
}
