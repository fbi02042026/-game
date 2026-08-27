using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 技能表：Ally=玩家+佣兵共用；Monster=敌人主动技
/// </summary>
public class SkillRegistry : Singleton<SkillRegistry>
{
    private Dictionary<string, SkillConfig> _dict = new Dictionary<string, SkillConfig>();

    public const string DefaultPlayerSkillId = "ally_heal";
    public const string DefaultMercMeleeSkillId = "ally_shield";
    public const string DefaultMercRangedSkillId = "ally_thunder";
    public const string DefaultMercHealSkillId = "ally_heal";

    public const string MonsterEliteMeleeSkillId = "mon_slam_multi";
    public const string MonsterEliteRangedSkillId = "mon_magic_burst";

    protected override void Awake()
    {
        base.Awake();
        LoadAll();
    }

    public void LoadAll()
    {
        _dict.Clear();
        LoadFolder(ContentPaths.Config.SkillsAlly);
        LoadFolder(ContentPaths.Config.SkillsMonster);
        LoadFolder(ContentPaths.Config.SkillsPlayerLegacy);
        LoadFolder(ContentPaths.Config.SkillsMercLegacy);
        Debug.Log($"[SkillRegistry] 已加载 {_dict.Count} 个技能配置");
        GameDataHub.ReportSkills(_dict);
    }

    void LoadFolder(string resourcesPath)
    {
        var list = Resources.LoadAll<SkillConfig>(resourcesPath);
        if (list == null) return;
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i] == null || string.IsNullOrEmpty(list[i].id)) continue;
            _dict[list[i].id] = list[i];
        }
    }

    public SkillConfig Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return _dict.TryGetValue(id, out var c) ? c : null;
    }

    public SkillSystem.ActiveSkill GetActiveSkill(string id)
    {
        var cfg = Get(id);
        return cfg != null ? cfg.ToActiveSkill() : null;
    }

    /// <summary>玩家当前携带技能（角色页选择；映射到现有 Ally SkillConfig）</summary>
    public string GetPlayerSkillId()
    {
        string selected = SaveSystem.Instance?.Data?.selectedPlayerSkillId;
        var def = PlayerSkillDefs.GetById(selected);
        if (def != null && !string.IsNullOrEmpty(def.allyConfigId))
            return def.allyConfigId;
        return DefaultPlayerSkillId;
    }

    public string GetMercDefaultSkillId(string mercId)
    {
        if (string.IsNullOrEmpty(mercId)) return DefaultMercMeleeSkillId;
        string fromRoster = MercRosterDefs.GetDefaultSkillId(mercId);
        if (!string.IsNullOrEmpty(fromRoster)) return fromRoster;
        if (mercId.StartsWith("naima")) return DefaultMercHealSkillId;
        if (mercId.StartsWith("fashi")) return "ally_atk_up";
        if (mercId.StartsWith("gongshou")) return DefaultMercRangedSkillId;
        if (mercId.StartsWith("dunbing")) return DefaultMercMeleeSkillId;
        if (mercId.StartsWith("kuangzhan")) return "ally_atk_speed";
        if (mercId.StartsWith("zhongzhan")) return DefaultMercMeleeSkillId;
        return DefaultMercMeleeSkillId;
    }

    /// <summary>优先用存档佣兵佩戴技能；空则回退职业默认。</summary>
    public string GetMercSkillId(MercenaryData data)
    {
        if (data != null && !string.IsNullOrEmpty(data.skillId) && Get(data.skillId) != null)
            return data.skillId;
        return GetMercDefaultSkillId(data != null ? data.mercId : null);
    }

    public string GetMercSkillId(string mercId, string preferredSkillId)
    {
        if (!string.IsNullOrEmpty(preferredSkillId) && Get(preferredSkillId) != null)
            return preferredSkillId;
        return GetMercDefaultSkillId(mercId);
    }

    /// <summary>
    /// 怪物主动技：近战重击 / 远程魔法弹；Boss 两种都会用。
    /// 远程小怪也给远程技（伤害在 Monster 侧按非精英打折），
    /// 否则远程怪整场只有普攻、玩家看不到技能子弹。
    /// </summary>
    public string GetMonsterSkillId(MonsterConfig template, bool isEliteWave, bool isBossUnit, MonsterAttackStyle primaryStyle)
    {
        bool ranged = MonsterAttackStyleTable.IsRanged(primaryStyle);
        if (isBossUnit || (template != null && template.isBoss))
        {
            // Boss 两种技能轮换偏好：表内主风格决定首发
            return ranged ? MonsterEliteRangedSkillId : MonsterEliteMeleeSkillId;
        }
        if (isEliteWave)
            return ranged ? MonsterEliteRangedSkillId : MonsterEliteMeleeSkillId;
        // 近战小怪保持只有普攻，避免开局被贴脸重击
        return ranged ? MonsterEliteRangedSkillId : null;
    }

    /// <summary>技能专属特效预制体；给「子弹命中点用技能特效」这类场景取原始 prefab。</summary>
    public GameObject GetSkillVfxPrefab(string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return null;
        var cfg = Get(skillId);
        if (cfg != null && cfg.vfxPrefab != null) return cfg.vfxPrefab;
        string folder = skillId.StartsWith("mon_") ? "Monster" : "Ally";
        return Resources.Load<GameObject>($"VFX/Skills/{folder}/{skillId}");
    }

    /// <summary>兼容旧调用</summary>
    public string GetMonsterSkillId(MonsterConfig template, bool isEliteWave, float attackRange)
    {
        var style = attackRange >= GameConfig.RangeBow - 0.05f ? MonsterAttackStyle.Ranged : MonsterAttackStyle.Melee;
        bool boss = template != null && template.isBoss;
        return GetMonsterSkillId(template, isEliteWave, boss, style);
    }

    /// <summary>播放技能专属特效（预制体放在 VFX/Skills/Ally 或 Monster，文件名=id）</summary>
    public void PlaySkillVfx(string skillId, Vector3 pos, bool isAllyCaster, int facingDir = 1, Transform attach = null)
    {
        if (string.IsNullOrEmpty(skillId)) return;
        var cfg = Get(skillId);
        GameObject prefab = cfg != null ? cfg.vfxPrefab : null;

        if (prefab == null)
        {
            string folder = skillId.StartsWith("mon_") ? "Monster" : "Ally";
            prefab = Resources.Load<GameObject>($"VFX/Skills/{folder}/{skillId}");
        }

        if (prefab != null)
        {
            if (BattleVFXSystem.Instance != null)
                BattleVFXSystem.Instance.PlayWorldPrefab(prefab, pos, 2.5f, facingDir);
            else
            {
                GameObject go = Object.Instantiate(prefab, pos, Quaternion.identity);
                if (facingDir < 0)
                {
                    var s = go.transform.localScale;
                    s.x = -Mathf.Abs(s.x);
                    go.transform.localScale = s;
                }
                Object.Destroy(go, 2.5f);
            }
            return;
        }

        if (BattleVFXSystem.Instance == null) return;
        var faction = isAllyCaster ? VfxFaction.Ally : VfxFaction.Enemy;
        AttackVfxKit kit = cfg != null ? cfg.attackKit : AttackVfxKit.None;

        // 恢复类固定 Heal；玩家（英雄）无专属 prefab 时按主手武器选文件夹
        bool healSkill = skillId.IndexOf("heal", System.StringComparison.OrdinalIgnoreCase) >= 0
            || (cfg != null && cfg.attackKit == AttackVfxKit.Heal);
        if (healSkill)
            kit = AttackVfxKit.Heal;
        else if (isAllyCaster && IsHeroCaster(attach))
            kit = Hero.Instance.GetWeaponVfxKit();
        else if (kit == AttackVfxKit.None)
        {
            if (skillId.Contains("thunder") || skillId.Contains("magic") || skillId.Contains("orb"))
                kit = AttackVfxKit.Orb;
            else if (skillId.Contains("bow") || skillId.Contains("arrow"))
                kit = AttackVfxKit.Bow;
            else
                kit = AttackVfxKit.MeleeSlash;
        }

        if (kit == AttackVfxKit.Heal)
            BattleVFXSystem.Instance.PlayHeal(pos, faction);
        else
            BattleVFXSystem.Instance.PlayAttackKit(kit, faction, pos, pos, facingDir);
    }

    static bool IsHeroCaster(Transform attach)
    {
        if (Hero.Instance == null) return false;
        if (attach == null) return true; // 历史调用未传挂点，默认当玩家
        return attach == Hero.Instance.transform || attach.IsChildOf(Hero.Instance.transform);
    }
}
