using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌方「玩家形象」护甲外观（2026-10-31 主人拍板「可以用三个职业随机一个基础装备，再配上其他的装备、
/// 技能和佣兵，然后计算战力」）。
///
/// 背景：敌方玩家形象用的是玩家白模 <see cref="ArenaRules.HeroFigurePrefab"/>（Units/wanjia），
/// 而 wanjia 预制体里 Armor / Helmet / Pant / Back 等槽位本来就是空的 —— 玩家本体靠
/// <see cref="HeroCostumeManager"/> 读存档装备在运行时换装，副本没人装配 → 看起来「什么都没穿」。
///
/// 口径：
/// · 清单由 <see cref="ArenaRules.BuildBotBattleSquad"/> 按展示战力的稀有度档给出（每个部位一件：
///   前期普通 / 中期稀有+普通 / 后期传奇+稀有，与佣兵、技能、装备同一套档位）；
/// · 贴图取 <see cref="EquipTemplate.spumName"/> → SPUM Resources 路径，按匹配表 PartType/Dir 挂上；
/// · 武器不在这里（职业固定套走 <see cref="EnemyFigureWeapon"/>，纯外观）；
/// · 缺真值（模板/贴图/匹配表缺失）LogError 不静默（铁律 14），不造贴图兜底。
/// 唯一调用方 = MercenaryManager.SpawnEnemyHeroFigure。
/// </summary>
public static class EnemyFigureOutfit
{
    /// <summary>外观清单一件：部位 + 品质（品质决定用哪张模板贴图）。</summary>
    public struct Piece
    {
        public EquipSlotType slot;
        public Rarity rarity;
    }

    /// <summary>部位 → SPUM 匹配表 PartType。Head 按模板走头盔（Helmet）或发型（Hair）。</summary>
    static readonly Dictionary<EquipSlotType, string> SlotToPartType = new Dictionary<EquipSlotType, string>
    {
        { EquipSlotType.Chest, "Armor" },
        { EquipSlotType.Hands, "Cloth" },
        { EquipSlotType.Feet,  "Pant" },
        { EquipSlotType.Cape,  "Back" },
    };

    /// <summary>模板池缓存（按部位分组，首次装配时扫一次）。</summary>
    static Dictionary<EquipSlotType, List<EquipTemplate>> _pool;

    /// <summary>
    /// 给敌方玩家形象穿护甲。清单为空 = 不装配（调用方保证非空；空清单不报错，代表无外观需求）。
    /// </summary>
    public static void Apply(GameObject root, IList<Piece> pieces)
    {
        if (root == null || pieces == null || pieces.Count == 0) return;

        var spum = root.GetComponent<SPUM_Prefabs>() ?? root.GetComponentInChildren<SPUM_Prefabs>(true);
        var lists = root.GetComponentsInChildren<SPUM_MatchingList>(true);
        if (spum == null || lists == null || lists.Length == 0)
        {
            Debug.LogError("[Arena] 敌方玩家形象缺 SPUM_Prefabs/SPUM_MatchingList，护甲穿不上（不静默）");
            return;
        }

        EnsurePool();
        int worn = 0;
        for (int i = 0; i < pieces.Count; i++)
        {
            var piece = pieces[i];
            var tpl = PickTemplate(piece.slot, piece.rarity, i);
            if (tpl == null)
            {
                Debug.LogError($"[Arena] 敌方玩家形象护甲：部位 {piece.slot} 品质 {piece.rarity} 没有可用装备模板（Config/Equips 缺档），该部位空着");
                continue;
            }
            if (string.IsNullOrEmpty(tpl.spumName))
            {
                Debug.LogError($"[Arena] 敌方玩家形象护甲：模板 {tpl.templateId} 没配 spumName，该部位空着");
                continue;
            }
            if (!TryResolvePath(tpl.spumName, out string path))
            {
                Debug.LogError($"[Arena] 敌方玩家形象护甲：贴图路径解析失败 {tpl.spumName}（模板 {tpl.templateId}）");
                continue;
            }
            Sprite[] slices = Resources.LoadAll<Sprite>(path);
            if (slices == null || slices.Length == 0)
            {
                Debug.LogError($"[Arena] 敌方玩家形象护甲：贴图加载失败 {path}（模板 {tpl.templateId}）");
                continue;
            }

            string partType = ResolvePartType(piece.slot, tpl.spumName);
            if (string.IsNullOrEmpty(partType)) continue;

            // ImageElement 的 ItemPath 同步（防 SPUM ResyncData 用旧路径覆盖，玩家侧换装同纪律）
            if (spum.ImageElement != null)
            {
                for (int e = 0; e < spum.ImageElement.Count; e++)
                {
                    var ie = spum.ImageElement[e];
                    if (ie != null && ie.PartType == partType) ie.ItemPath = path;
                }
            }

            bool hit = false;
            for (int m = 0; m < lists.Length; m++)
            {
                var tables = lists[m] != null ? lists[m].matchingTables : null;
                if (tables == null) continue;
                for (int t = 0; t < tables.Count; t++)
                {
                    var me = tables[t];
                    if (me == null || me.PartType != partType || me.renderer == null) continue;
                    Sprite slice = HeroCostumeManager.PickSlice(slices, tpl.spumName, partType, me.Structure, me.Dir);
                    if (slice == null) continue;
                    me.ItemPath = path;
                    me.renderer.sprite = slice;
                    EquipRarityMaterials.Apply(me.renderer);
                    hit = true;
                }
            }
            if (hit) worn++;
            else
                Debug.LogError($"[Arena] 敌方玩家形象护甲：匹配表里没有 PartType={partType} 的挂点，{tpl.templateId} 没挂上");
        }
        Debug.Log($"[Arena] 敌方玩家形象护甲已装配 {worn}/{pieces.Count} 件");
    }

    /// <summary>部位 + 目标品质 → 模板（同品质多件时按序号错开，制造外观差异；缺档逐级降级，最低普通）。
    /// 只挑贴图能解析到的模板 —— 挑中一张加载不到的图等于该部位裸着，不如换同档另一件。</summary>
    static EquipTemplate PickTemplate(EquipSlotType slot, Rarity want, int index)
    {
        if (_pool == null || !_pool.TryGetValue(slot, out var list) || list.Count == 0) return null;
        for (int r = (int)want; r >= (int)Rarity.Common; r--)
        {
            var sameRarity = new List<EquipTemplate>();
            for (int i = 0; i < list.Count; i++)
            {
                var tpl = list[i];
                if (tpl == null || (int)tpl.baseRarity != r) continue;
                if (!TryResolvePath(tpl.spumName, out _)) continue;
                sameRarity.Add(tpl);
            }
            if (sameRarity.Count > 0) return sameRarity[index % sameRarity.Count];
        }
        return null;
    }

    /// <summary>模板池：扫 Config/Equips 一次，只留护甲部位（头/身/手/脚/披风），按部位分组。</summary>
    static void EnsurePool()
    {
        if (_pool != null) return;
        _pool = new Dictionary<EquipSlotType, List<EquipTemplate>>();
        var all = Resources.LoadAll<EquipTemplate>(ContentPaths.Config.Equips);
        if (all == null || all.Length == 0)
        {
            Debug.LogError($"[Arena] 敌方玩家形象护甲：{ContentPaths.Config.Equips} 下没有装备模板（不静默）");
            return;
        }
        for (int i = 0; i < all.Length; i++)
        {
            var tpl = all[i];
            if (tpl == null || string.IsNullOrEmpty(tpl.spumName)) continue;
            if (!SlotToPartType.ContainsKey(tpl.slotType)
                && tpl.slotType != EquipSlotType.Head) continue;
            if (!_pool.TryGetValue(tpl.slotType, out var list))
            {
                list = new List<EquipTemplate>();
                _pool[tpl.slotType] = list;
            }
            list.Add(tpl);
        }
        // 模板顺序固定（Resources 加载顺序不稳定时也能保证同机器人每次外观一致）
        foreach (var kv in _pool) kv.Value.Sort((a, b) => string.CompareOrdinal(a.templateId, b.templateId));
    }

    /// <summary>部位 → 匹配表 PartType：Head 看模板是头盔图还是发型图，其余查表。</summary>
    static string ResolvePartType(EquipSlotType slot, string spumName)
    {
        if (slot == EquipSlotType.Head)
            return IsHelmetAsset(spumName) ? "Helmet" : "Hair";
        return SlotToPartType.TryGetValue(slot, out var partType) ? partType : null;
    }

    static bool IsHelmetAsset(string spumName)
    {
        if (string.IsNullOrEmpty(spumName)) return false;
        if (spumName.IndexOf("Helmet", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return TryResolvePath(spumName, out string path)
            && path.IndexOf("4_Helmet", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>spumName → Resources 路径：优先玩家换装缓存（战内已建好），单例不在再按名字直探一次。</summary>
    static bool TryResolvePath(string spumName, out string path)
    {
        path = null;
        if (string.IsNullOrEmpty(spumName)) return false;
        var cm = HeroCostumeManager.Instance;
        if (cm != null && cm.TryResolveSpumPath(spumName, out path)) return true;
        var probe = Resources.LoadAll<Sprite>(spumName);
        if (probe != null && probe.Length > 0)
        {
            path = spumName;
            return true;
        }
        return false;
    }
}
