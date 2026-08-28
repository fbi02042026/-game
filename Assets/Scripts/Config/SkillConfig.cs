using UnityEngine;

/// <summary>
/// 主动技能配置（玩家/佣兵共用 Ally；怪物用 Monster）
///
/// 【加新技能必看】特效不会静默乱套：
/// 1) 优先：拖 vfxPrefab，或放 Resources/VFX/Skills/{Ally|Monster|Merc}/{id}.prefab
/// 2) 否则：必须设 attackKit（MeleeSlash/Bow/Orb/Heal），运行时播共用套
/// 3) attackKit=None 且无专属预制体 → 编辑器校验会报错；运行时会打 Error 并尽量兜底
/// </summary>
[CreateAssetMenu(fileName = "SkillConfig", menuName = "Config/Skill")]
public class SkillConfig : ScriptableObject
{
    public string id;
    public string skillName;
    [TextArea] public string desc;

    [Header("施放")]
    public SkillSystem.SkillType skillType = SkillSystem.SkillType.AOE;
    [Tooltip("无专属预制体时的共用特效套。新技能务必设好；None=仅依赖专属 prefab")]
    public AttackVfxKit attackKit = AttackVfxKit.None;
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

    [Header("特效预制体（可选；不填则按 id 从 Resources/VFX/Skills 加载）")]
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
