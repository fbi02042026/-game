using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 技能系统：管理技能数值和弹幕特效
/// 技能分为主动技能和被动技能
/// </summary>
public class SkillSystem : Singleton<SkillSystem>
{
    private List<ActiveSkill> _skills = new List<ActiveSkill>();
    private Dictionary<string, float> _cooldowns = new Dictionary<string, float>();

    /// <summary>
    /// 技能数据
    /// </summary>
    [System.Serializable]
    public class ActiveSkill
    {
        public string skillId;
        public string skillName;
        public float baseDamage;        // 基础伤害
        public float damageMultiplier;  // 伤害倍率（基于攻击力）
        public float cooldown;          // 冷却时间
        public SkillType skillType;     // 技能类型
        public int projectileCount;     // 弹幕数量
        public float projectileSpeed;   // 弹幕速度
        public float aoeRadius;         // 范围伤害半径
        public Sprite icon;             // 技能图标
    }

    public enum SkillType
    {
        SingleTarget,   // 单体
        Projectile,     // 弹幕
        AOE,            // 范围
        Buff,           // 增益
        Chain           // 连锁
    }

    void Update()
    {
        // 更新冷却
        var keys = new List<string>(_cooldowns.Keys);
        foreach (var key in keys)
        {
            _cooldowns[key] -= Time.deltaTime;
            if (_cooldowns[key] <= 0)
                _cooldowns.Remove(key);
        }
    }

    /// <summary>
    /// 使用技能
    /// </summary>
    public bool UseSkill(ActiveSkill skill, UnitBase caster)
    {
        if (IsOnCooldown(skill.skillId))
        {
            Debug.Log($"[SkillSystem] {skill.skillName} 冷却中");
            return false;
        }

        _cooldowns[skill.skillId] = skill.cooldown;

        switch (skill.skillType)
        {
            case SkillType.SingleTarget:
                ExecuteSingleTarget(skill, caster);
                break;
            case SkillType.Projectile:
                ExecuteProjectile(skill, caster);
                break;
            case SkillType.AOE:
                ExecuteAOE(skill, caster);
                break;
            case SkillType.Buff:
                ExecuteBuff(skill, caster);
                break;
            case SkillType.Chain:
                ExecuteChain(skill, caster);
                break;
        }

        return true;
    }

    private void ExecuteSingleTarget(ActiveSkill skill, UnitBase caster)
    {
        UnitBase target = caster.FindNearestEnemy();
        if (target == null) return;

        float damage = CalculateDamage(skill, caster);
        bool isCrit = Random.value < caster.attr.GetAttr(AttrType.CritRate);
        if (isCrit) damage *= 2;
        target.TakeDamage(damage, isCrit);
    }

    private void ExecuteProjectile(ActiveSkill skill, UnitBase caster)
    {
        List<UnitBase> enemies = GetEnemiesInRange(caster, 10f);
        if (enemies.Count == 0) return;

        float damage = CalculateDamage(skill, caster);

        for (int i = 0; i < skill.projectileCount && i < enemies.Count; i++)
        {
            UnitBase target = enemies[i];
            bool isCrit = Random.value < caster.attr.GetAttr(AttrType.CritRate);
            float finalDamage = isCrit ? damage * 2 : damage;
            target.TakeDamage(finalDamage, isCrit);
        }
    }

    private void ExecuteAOE(ActiveSkill skill, UnitBase caster)
    {
        float damage = CalculateDamage(skill, caster);
        List<UnitBase> enemies = GetEnemiesInRange(caster, skill.aoeRadius);

        foreach (var enemy in enemies)
        {
            bool isCrit = Random.value < caster.attr.GetAttr(AttrType.CritRate);
            float finalDamage = isCrit ? damage * 2 : damage;
            enemy.TakeDamage(finalDamage, isCrit);
        }
    }

    private void ExecuteBuff(ActiveSkill skill, UnitBase caster)
    {
        // Buff类技能：给自身加临时属性
        BattleManager.Instance.tempBuffs.Add(new AttrBonusData
        {
            attrType = AttrType.Attack,
            value = skill.damageMultiplier,
            isPercent = true
        });
        Hero.Instance.RecalcAttr();
    }

    private void ExecuteChain(ActiveSkill skill, UnitBase caster)
    {
        float damage = CalculateDamage(skill, caster);
        List<UnitBase> enemies = GetEnemiesInRange(caster, 8f);

        // 连锁伤害递减
        float chainMultiplier = 1f;
        foreach (var enemy in enemies)
        {
            bool isCrit = Random.value < caster.attr.GetAttr(AttrType.CritRate);
            float finalDamage = (isCrit ? damage * 2 : damage) * chainMultiplier;
            enemy.TakeDamage(finalDamage, isCrit);
            chainMultiplier *= 0.6f; // 每次连锁递减40%
            if (chainMultiplier < 0.2f) break;
        }
    }

    /// <summary>
    /// 计算技能伤害
    /// </summary>
    private float CalculateDamage(ActiveSkill skill, UnitBase caster)
    {
        float attack = caster.attr.GetAttr(AttrType.Attack);
        float phyPower = caster.attr.GetAttr(AttrType.PhyPower);
        float magicPower = caster.attr.GetAttr(AttrType.MagicPower);

        return skill.baseDamage + attack * skill.damageMultiplier * (1 + phyPower + magicPower);
    }

    /// <summary>
    /// 获取范围内的敌人
    /// </summary>
    private List<UnitBase> GetEnemiesInRange(UnitBase caster, float range)
    {
        List<UnitBase> enemies = new List<UnitBase>();
        var enemyList = caster.isAlly ? BattleManager.Instance.monsters : BattleManager.Instance.allyUnits;

        foreach (var enemy in enemyList)
        {
            if (enemy.isDead) continue;
            float dist = Vector2.Distance(caster.transform.position, enemy.transform.position);
            if (dist <= range)
            {
                enemies.Add(enemy);
            }
        }
        return enemies;
    }

    /// <summary>
    /// 按索引使用技能（供UI按钮调用）
    /// </summary>
    public bool UseSkill(int index)
    {
        if (index < 0 || index >= _skills.Count) return false;
        UnitBase caster = Hero.Instance;
        if (caster == null) return false;
        return UseSkill(_skills[index], caster);
    }

    /// <summary>
    /// 添加技能
    /// </summary>
    public void AddSkill(ActiveSkill skill)
    {
        if (skill != null) _skills.Add(skill);
    }

    /// <summary>
    /// 获取已装备技能列表
    /// </summary>
    public List<ActiveSkill> GetSkills() => _skills;

    public bool IsOnCooldown(string skillId)
    {
        return _cooldowns.ContainsKey(skillId) && _cooldowns[skillId] > 0;
    }

    public float GetCooldownRemaining(string skillId)
    {
        return _cooldowns.TryGetValue(skillId, out var cd) ? Mathf.Max(0, cd) : 0;
    }
}