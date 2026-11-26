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
    // 🔴 2026-11-26：下面是【数据 id】——花名册/技能/属性/专属行为的真源，不可改成 bandit_*；
    // 专属战斗模型（Units/bandit_boss|witch|blade）由 BanditPrefabOfAssetId 单独映射到加载层。
    const string BanditBoss = "zhongzhan201";   // 独眼老雷 · 重武大哥（稀有档重武者）
    const string BanditWitch = "fashi202";      // 夜薇 · 女法师（雷系稀有档）
    const string BanditBlade = "kuangzhan201";  // 阿刃 · 狂战小弟（稀有档狂战士）

    /// <summary>
    /// 【2026-11-22 主人拍板】强盗贯穿8章、出场组合按章递进（小弟→大哥→2人→女法→远近→坦法→三人组→三人组）。
    /// 🔴 组合真源只此一处；intruder_event.csv 的 replaceCount 必须与本数组长度逐章一致（不一致 Execute 会槽位越界回落普通怪）。
    /// 顺序即站位/对白出场序：老雷=大哥(坦) / 夜薇=女法(远) / 阿刃=小弟(近)。
    /// </summary>
    public static string[] BanditIdsForChapter(int chapter)
    {
        switch (chapter)
        {
            case 1: return new[] { BanditBlade };                         // 小弟初遇
            case 2: return new[] { BanditBoss };                          // 大哥单挑
            case 3: return new[] { BanditBlade, BanditBoss };             // 小弟+大哥
            case 4: return new[] { BanditWitch };                         // 女法初遇
            case 5: return new[] { BanditWitch, BanditBlade };            // 远近搭配
            case 6: return new[] { BanditBoss, BanditWitch };             // 重武+法搭配
            case 7: return new[] { BanditBoss, BanditWitch, BanditBlade }; // 三人组
            case 8: return new[] { BanditBoss, BanditWitch, BanditBlade }; // 三人组终章
            default:
                Debug.LogError($"[IntruderPicker] 未登记的强盗章节 chapter={chapter}，无组合（配表章节只应 1~8）");
                return System.Array.Empty<string>();
        }
    }

    /// <summary>
    /// 按章返回固定组合（不再随机抽）。seed 保留入参仅作日志回溯，不参与抽取。
    /// 下方旧随机抽取逻辑（BuildExcludedAssetIds / BuildPool / TryExcludeSquadAssetId）
    /// 保留空转（同 FORMAL_ROUND_WAVE_CUT* 先例）：若回滚随机口径可整体复用，勿删。
    /// </summary>
    public static bool TryPick(int chapter, int seed, out string[] assetIds)
    {
        assetIds = BanditIdsForChapter(chapter);
        if (assetIds == null || assetIds.Length == 0)
        {
            assetIds = null;
            return false;
        }
        Debug.Log($"[IntruderPicker] 第{chapter}章强盗组合 seed={seed} ids={string.Join(",", assetIds)}");
        return true;
    }

    /// <summary>
    /// 【2026-11-26 主人拍板】强盗三人组专属战斗模型（Assets/SPUM/Resources/Units/bandit_*）。
    /// 视觉与数据解耦：上面三个 assetId 继续做花名册/技能/属性/专属行为的真源，一字不改；
    /// 本映射只给 SpawnEnemyMercenary 提供 prefab 名覆盖，强盗不再顶着玩家佣兵的脸。
    /// 非三人组返回 null → 加载链回退原 id，竞技场与我方佣兵零影响。
    /// </summary>
    public static string BanditPrefabOfAssetId(string assetId)
    {
        switch (assetId)
        {
            case BanditBoss: return "bandit_boss";
            case BanditWitch: return "bandit_witch";
            case BanditBlade: return "bandit_blade";
            default: return null;
        }
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
        // 口径：救场小队（加百列 H023 / 艾拉 H015 / 莉娜 H017，2026-11-23 治疗位由塞拉 H012 换牧师莉娜）绝不以敌对乱入身份出现——
        // 同一张脸既当救场队友又当拦路敌人，"同一支队伍暗中保护玩家"的剧情就穿帮。
        // 取数走 MercRosterDefs.TryGetByHireId 的 AssetId（T1 口径），不散写字符串；
        // 艾拉 gongshou101 虽已被上面三职业 101 脸覆盖，仍按拍板三名全列（幂等，防未来 101 口径改动）。
        // 静态推演：花名册去重 20 个 AssetId − 三职业101 − dunbing203 − naima102 = 15 ≥ PickCount*2，抽 3 无压力。
        TryExcludeSquadAssetId(excluded, "H023");   // 加百列 dunbing203
        TryExcludeSquadAssetId(excluded, "H015");   // 艾拉 gongshou101
        TryExcludeSquadAssetId(excluded, "H017");   // 莉娜 naima102（与塞拉同战斗模板，排除的 AssetId 不变）
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
