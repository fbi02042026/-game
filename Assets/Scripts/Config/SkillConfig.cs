using UnityEngine;

/// <summary>
/// 主动技能配置（玩家/佣兵共用 Ally；怪物用 Monster）
/// id 与 Resources/VFX/Skills 下预制体文件名一致。
/// </summary>
[CreateAssetMenu(fileName = "SkillConfig", menuName = "Config/Skill")]
public class SkillConfig : ScriptableObject
{
    public string id;
    public string skillName;
    [TextArea] public string desc;

  [Header("施放")]
    public SkillSystem.SkillType skillType = SkillSystem.SkillType.AOE;
    public AttackVfxKit attackKit = AttackVfxKit.None; // 普攻套（与技能特效无关时可留 None）
    public float damageMultiplier = 2f;
    public float baseDamage = 0f;
    public float cooldown = 0.1f;
    public float aoeRadius = 4f;
    public int projectileCount = 1;
    public float projectileSpeed = 48f;

    [Header("增益类（Buff/治疗）")]
    public AttrType buffAttr = AttrType.Attack;
    public float buffValue = 0.15f;
    public bool buffIsPercent = true;
    public float duration = 0f;
    public float healBase = 80f;
    [Tooltip("按最大生命百分比治疗，>0 时优先于 healBase")]
    public float healPercentOfMax = 0f;

    [Header("特效预制体（可选，不填则按 id 从 Resources/VFX/Skills 加载）")]
    public GameObject vfxPrefab;

    public SkillSystem.ActiveSkill ToActiveSkill()
    {
        return new SkillSystem.ActiveSkill
        {
            skillId = id,
            skillName = skillName,
            baseDamage = baseDamage,
            damageMultiplier = damageMultiplier,
            cooldown = cooldown,
            skillType = skillType,
            projectileCount = projectileCount,
            projectileSpeed = projectileSpeed,
            aoeRadius = aoeRadius
        };
    }
}
