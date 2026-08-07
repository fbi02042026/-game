using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 技能表：Ally=玩家+佣兵共用；Monster=敌人主动技
/// </summary>
public class SkillRegistry : Singleton<SkillRegistry>
{
    private Dictionary<string, SkillConfig> _dict = new Dictionary<string, SkillConfig>();

    public const string DefaultPlayerSkillId = "ally_thunder";
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
        LoadFolder("Config/Skills/Ally");
        LoadFolder("Config/Skills/Monster");
        // 兼容旧路径
        LoadFolder("Config/Skills/Player");
        LoadFolder("Config/Skills/Merc");
        Debug.Log($"[SkillRegistry] 已加载 {_dict.Count} 个技能配置");
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

    /// <summary>玩家/佣兵默认技能（可按存档扩展）</summary>
    public string GetPlayerSkillId() => DefaultPlayerSkillId;

    public string GetMercDefaultSkillId(string mercId)
    {
        if (string.IsNullOrEmpty(mercId)) return DefaultMercMeleeSkillId;
        if (mercId.StartsWith("naima")) return DefaultMercHealSkillId;
        if (mercId.StartsWith("gongshou")) return DefaultMercRangedSkillId;
        if (mercId.StartsWith("dunbing")) return DefaultMercMeleeSkillId;
        if (mercId.StartsWith("kuangzhan")) return "ally_atk_speed";
        return DefaultMercMeleeSkillId;
    }

    /// <summary>精英/Boss 主动技：近战重击 / 远程魔法；Boss 两种都会用</summary>
    public string GetMonsterSkillId(MonsterConfig template, bool isEliteWave, bool isBossUnit, MonsterAttackStyle primaryStyle)
    {
        bool ranged = primaryStyle == MonsterAttackStyle.Ranged;
        if (isBossUnit || (template != null && template.isBoss))
        {
            // Boss 两种技能轮换偏好：表内主风格决定首发
            return ranged ? MonsterEliteRangedSkillId : MonsterEliteMeleeSkillId;
        }
        if (isEliteWave)
            return ranged ? MonsterEliteRangedSkillId : MonsterEliteMeleeSkillId;
        return null; // 小怪无主动技，只走普攻套
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
            GameObject go = Object.Instantiate(prefab, pos, Quaternion.identity);
            if (attach != null) go.transform.SetParent(attach, true);
            Object.Destroy(go, 2.5f);
            return;
        }

        if (BattleVFXSystem.Instance == null) return;
        var faction = isAllyCaster ? VfxFaction.Ally : VfxFaction.Enemy;
        if (skillId.Contains("heal") || skillId.Contains("shield"))
            BattleVFXSystem.Instance.PlayHeal(pos, faction);
        else if (skillId.Contains("thunder") || skillId.Contains("magic"))
            BattleVFXSystem.Instance.PlayAttackKit(AttackVfxKit.Orb, faction, pos, pos, 1);
        else
            BattleVFXSystem.Instance.PlayAttackKit(AttackVfxKit.MeleeSlash, faction, pos, pos, 1);
    }
}
