using UnityEngine;

/// <summary>
/// 敌方「玩家形象」武器外观（2026-10-31 主人拍板「为啥敌方没有武器装备什么」）：
/// 敌方玩家形象（<see cref="ArenaRules.HeroFigurePrefab"/> = Units/wanjia）与玩家本体同一份 SPUM 预制体，
/// 但玩家武器走 BattleBagSystem + HeroCostumeManager（读玩家自己的装备），敌方形象没有这条链 → 一直空手。
/// 这里照铁律 11「初始装备 = 同职业 101 佣兵手上武器」给敌方形象挂同款武器（<b>纯外观</b>，数值走战力阀门）：
///   dunbing101（剑盾）→ 主 Sword_1（equip_sword_1）+ 副 Shield_1（equip_shield_1）
///   fashi101（法师）  → 主 New_Weapon_03（weapon_twilight_staff）
///   gongshou101（游侠）→ 主 New_Weapon_12（equip_new_weapon_12，双手弓，无副手）
/// 模板 id 与 PlayerJobDefs.GetJobWeaponKit 的兜底值同值（同一语义：同职业 101 佣兵的武器，改一边必须同步另一边）。
/// 普通敌方佣兵照旧用佣兵预制体自带武器，不经这里。
/// 唯一调用方 = MercenaryManager.SpawnEnemyHeroFigure。
/// </summary>
public static class EnemyFigureWeapon
{
    struct Kit
    {
        public string main;
        public string off;
    }

    /// <summary>属性职业 → 武器套（mercId 是属性真源，模板池三系职业已在 ArenaRules.BotTeamTemplates 收口）。</summary>
    static bool TryGetKit(string statMercId, out Kit kit)
    {
        switch (statMercId)
        {
            case "dunbing101":
                kit = new Kit { main = "equip_sword_1", off = "equip_shield_1" };
                return true;
            case "fashi101":
                kit = new Kit { main = "weapon_twilight_staff", off = null };
                return true;
            case "gongshou101":
                kit = new Kit { main = "equip_new_weapon_12", off = null };
                return true;
            default:
                kit = default;
                return false;
        }
    }

    /// <summary>给敌方玩家形象挂职业武器。缺真值（未知职业/模板缺失/贴图缺失）LogError 不静默（铁律 14）。</summary>
    public static void Apply(GameObject wanjiaRoot, string statMercId)
    {
        if (wanjiaRoot == null || string.IsNullOrEmpty(statMercId)) return;
        if (!TryGetKit(statMercId, out var kit))
        {
            Debug.LogError($"[Arena] 敌方玩家形象武器：未知属性职业 {statMercId}（模板池只允许 剑盾/法师/游侠），不上武器");
            return;
        }

        var spum = wanjiaRoot.GetComponent<SPUM_Prefabs>() ?? wanjiaRoot.GetComponentInChildren<SPUM_Prefabs>(true);
        var lists = wanjiaRoot.GetComponentsInChildren<SPUM_MatchingList>(true);
        if (spum == null || lists == null || lists.Length == 0)
        {
            Debug.LogError("[Arena] 敌方玩家形象缺 SPUM_Prefabs/SPUM_MatchingList，武器挂不上（不静默）");
            return;
        }

        // 先剥预制体默认武器（wanjia 预制体自带默认武器残留，玩家侧 HeroCostumeManager.StripPrefabDefaultWeaponVisuals
        // 同口径 —— 不剥会「默认武器 + 职业武器」叠两把）
        StripDefaultWeapons(spum, lists);

        var rig = HeroWeaponRig.Build(spum, lists);
        if (!rig.IsValid)
        {
            Debug.LogError("[Arena] 敌方玩家形象 HandRig 无效，武器挂不上（不静默）");
            return;
        }

        if (!string.IsNullOrEmpty(kit.main))
            ApplyWeapon(spum, lists, rig, rig.AttackDir, kit.main);
        if (!string.IsNullOrEmpty(kit.off))
            ApplyWeapon(spum, lists, rig, rig.SecondaryDir, kit.off);
    }

    /// <summary>剥掉 wanjia 预制体上残留的默认武器贴图与路径（玩家侧同款三步：ImageElement / Matching / 内嵌节点）。</summary>
    static void StripDefaultWeapons(SPUM_Prefabs spum, SPUM_MatchingList[] lists)
    {
        if (spum != null && spum.ImageElement != null)
        {
            for (int i = 0; i < spum.ImageElement.Count; i++)
            {
                var ie = spum.ImageElement[i];
                if (ie != null && ie.PartType == "Weapons") ie.ItemPath = "";
            }
        }
        if (lists != null)
        {
            for (int m = 0; m < lists.Length; m++)
            {
                var tables = lists[m]?.matchingTables;
                if (tables == null) continue;
                for (int t = 0; t < tables.Count; t++)
                {
                    var me = tables[t];
                    if (me == null || me.PartType != "Weapons") continue;
                    me.ItemPath = "";
                    if (me.renderer != null) me.renderer.sprite = null;
                }
            }
        }
        var srs = spum != null ? spum.GetComponentsInChildren<SpriteRenderer>(true) : null;
        if (srs == null) return;
        for (int i = 0; i < srs.Length; i++)
        {
            var sr = srs[i];
            if (sr == null || sr.gameObject == null) continue;
            string n = sr.gameObject.name;
            if (n.IndexOf("Weapon", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Shield", System.StringComparison.OrdinalIgnoreCase) >= 0)
                sr.sprite = null;
        }
    }

    /// <summary>把模板武器贴图挂到指定手的挂点（盾走 Shield 渲染器，其余走主武器渲染器）；
    /// ItemPath 同步写回，防 SPUM ResyncData 用旧路径覆盖（玩家侧换装同纪律）。</summary>
    static void ApplyWeapon(SPUM_Prefabs spum, SPUM_MatchingList[] lists, HeroWeaponRig.HandRig rig,
        string dir, string templateId)
    {
        var tpl = Resources.Load<EquipTemplate>(ContentPaths.Config.Equips + "/" + templateId);
        if (tpl == null || string.IsNullOrEmpty(tpl.spumName))
        {
            Debug.LogError($"[Arena] 敌方玩家形象武器模板缺失或没配 spumName：{templateId}（不静默）");
            return;
        }
        string spumName = tpl.spumName;
        if (!TryResolvePath(spumName, out string path))
        {
            Debug.LogError($"[Arena] 敌方玩家形象武器贴图路径解析失败：{spumName}（不静默）");
            return;
        }
        Sprite[] all = Resources.LoadAll<Sprite>(path);
        if (all == null || all.Length == 0)
        {
            Debug.LogError($"[Arena] 敌方玩家形象武器贴图加载失败：{path}（不静默）");
            return;
        }
        Sprite sprite = System.Array.Find(all, s => s != null && s.name == spumName) ?? all[0];
        bool isShield = HeroWeaponRig.IsShieldSpumName(spumName);

        if (spum != null && spum.ImageElement != null)
        {
            for (int i = 0; i < spum.ImageElement.Count; i++)
            {
                var ie = spum.ImageElement[i];
                if (ie != null && ie.PartType == "Weapons" && ie.Dir == dir) ie.ItemPath = path;
            }
        }
        if (lists != null)
        {
            for (int m = 0; m < lists.Length; m++)
            {
                var tables = lists[m]?.matchingTables;
                if (tables == null) continue;
                for (int t = 0; t < tables.Count; t++)
                {
                    var me = tables[t];
                    if (me == null || me.PartType != "Weapons" || me.Dir != dir) continue;
                    bool hit = isShield
                        ? HeroWeaponRig.IsShieldRenderer(me.renderer)
                        : HeroWeaponRig.IsPrimaryWeaponRenderer(me.renderer);
                    me.ItemPath = path;
                    if (me.renderer == null || !hit) continue;
                    me.renderer.sprite = sprite;
                    HeroWeaponRig.ApplyWeaponPresentation(me.renderer, dir, rig);
                }
            }
        }
        // 匹配表没盖到的主武器挂点直挂一次（玩家侧 TryApplyPrimaryWeaponRenderer 同口径）
        if (!isShield && HeroWeaponRig.TryGetPrimaryWeaponRenderer(lists, dir, out var sr))
        {
            sr.sprite = sprite;
            HeroWeaponRig.ApplyWeaponPresentation(sr, dir, rig);
        }
        Debug.Log($"[Arena] 敌方玩家形象已挂武器：{dir} ← {spumName}（{templateId}）");
    }

    /// <summary>spumName → Resources 路径：优先玩家换装缓存（战内已建好），单例不在再按名字直探一次
    ///（玩家侧 FallbackLoad 同口径，不算造数）。</summary>
    static bool TryResolvePath(string spumName, out string path)
    {
        path = null;
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
