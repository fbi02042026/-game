using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 技能系统：管理技能数值和弹幕特效
/// 技能分为主动技能和被动技能
/// </summary>
public class SkillSystem : Singleton<SkillSystem>, ICombatBoundSingleton
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

    readonly List<string> _cooldownKeyBuffer = new List<string>(8);

    void Update()
    {
        if (_cooldowns.Count == 0) return;

        _cooldownKeyBuffer.Clear();
        foreach (var kv in _cooldowns)
            _cooldownKeyBuffer.Add(kv.Key);

        float dt = Time.deltaTime;
        for (int i = 0; i < _cooldownKeyBuffer.Count; i++)
        {
            string key = _cooldownKeyBuffer[i];
            if (!_cooldowns.TryGetValue(key, out float cd)) continue;
            cd -= dt;
            if (cd <= 0f)
                _cooldowns.Remove(key);
            else
                _cooldowns[key] = cd;
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

        _cooldowns[skill.skillId] = ApplyCooldownReduce(skill.cooldown);

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

        float damage = DamageFormula.ApplyCrit(CalculateDamage(skill, caster), caster.attr, out bool isCrit);
        damage = DamageFormula.ApplyAttackerSpecials(damage, caster, target);
        target.TakeDamage(damage, isCrit, false, true, caster.GetVfxFacingDir(), caster);
    }

    private void ExecuteProjectile(ActiveSkill skill, UnitBase caster)
    {
        List<UnitBase> enemies = GetEnemiesInRange(caster, 10f);
        if (enemies.Count == 0 || caster == null) return;

        float damage = CalculateDamage(skill, caster);
        VfxFaction faction = caster.isAlly ? VfxFaction.Ally : VfxFaction.Enemy;
        Vector3 firePos = caster.GetFirePosition();

        int vfxDir = caster.GetVfxFacingDir();
        for (int i = 0; i < skill.projectileCount && i < enemies.Count; i++)
        {
            UnitBase target = enemies[i];
            if (target == null || target.isDead) continue;
            float finalDamage = DamageFormula.ApplyCrit(damage, caster.attr, out bool isCrit);
            finalDamage = DamageFormula.ApplyAttackerSpecials(finalDamage, caster, target);
            UnitBase locked = target;
            UnitBase src = caster;
            float dmg = finalDamage;
            bool crit = isCrit;

            if (BattleVFXSystem.Instance != null)
            {
                var cfg = SkillRegistry.Instance != null
                    ? SkillRegistry.Instance.Get(skill.skillId) : null;
                AttackVfxKit kit = SkillNaming.ResolveProjectileKit(cfg, skill.skillId);
                if (caster != null && SkillNaming.IsRangedKit(caster.GetBasicAttackVfxKit())
                    && !SkillNaming.IsRangedKit(kit))
                    kit = caster.GetBasicAttackVfxKit();
                GameObject impactOverride = SkillRegistry.Instance != null
                    ? SkillRegistry.Instance.GetSkillVfxPrefab(skill.skillId) : null;
                float rangedDelay = SkillNaming.IsRangedKit(kit) ? GameConfig.RANGED_FIRE_RELEASE_DELAY : 0f;
                if (rangedDelay > 0.001f)
                    StartCoroutine(CoRangedSkillProjectile(
                        src, locked, faction, vfxDir, kit, impactOverride, dmg, crit, rangedDelay));
                else
                {
                    Vector3 hitPos = target.GetHitPosition();
                    BattleVFXSystem.Instance.PlaySkillProjectile(
                        faction, firePos, hitPos, vfxDir, locked.transform, kit,
                        impactOverride, 1f, 1f,
                        () =>
                        {
                            if (locked == null || locked.isDead) return;
                            locked.TakeDamage(dmg, crit, false, true, vfxDir, src);
                        });
                }
            }
            else
            {
                target.TakeDamage(finalDamage, isCrit, false, true, vfxDir, caster);
            }
        }
    }

    IEnumerator CoRangedSkillProjectile(
        UnitBase caster, UnitBase target, VfxFaction faction, int vfxDir,
        AttackVfxKit kit, GameObject impactOverride, float dmg, bool crit, float delay)
    {
        if (delay > 0.001f)
            yield return new WaitForSeconds(delay);
        if (caster == null || caster.isDead || target == null || target.isDead)
            yield break;
        if (BattleVFXSystem.Instance == null)
        {
            target.TakeDamage(dmg, crit, false, true, vfxDir, caster);
            yield break;
        }
        Vector3 from = caster.GetFirePosition();
        Vector3 to = target.GetHitPosition();
        UnitBase locked = target;
        UnitBase src = caster;
        BattleVFXSystem.Instance.PlaySkillProjectile(
            faction, from, to, vfxDir, locked.transform, kit,
            impactOverride, 1f, 1f,
            () =>
            {
                if (locked == null || locked.isDead) return;
                locked.TakeDamage(dmg, crit, false, true, vfxDir, src);
            });
    }

    private void ExecuteAOE(ActiveSkill skill, UnitBase caster)
    {
        float damage = CalculateDamage(skill, caster);
        List<UnitBase> enemies = GetEnemiesInRange(caster, skill.aoeRadius);

        int vfxDir = caster.GetVfxFacingDir();
        foreach (var enemy in enemies)
        {
            float finalDamage = DamageFormula.ApplyCrit(damage, caster.attr, out bool isCrit);
            finalDamage = DamageFormula.ApplyAttackerSpecials(finalDamage, caster, enemy);
            enemy.TakeDamage(finalDamage, isCrit, false, true, vfxDir, caster);
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
        int vfxDir = caster.GetVfxFacingDir();
        foreach (var enemy in enemies)
        {
            float finalDamage = DamageFormula.ApplyCrit(damage, caster.attr, out bool isCrit) * chainMultiplier;
            finalDamage = DamageFormula.ApplyAttackerSpecials(finalDamage, caster, enemy);
            enemy.TakeDamage(finalDamage, isCrit, false, true, vfxDir, caster);
            chainMultiplier *= 0.6f; // 每次连锁递减40%
            if (chainMultiplier < 0.2f) break;
        }
    }

    /// <summary>
    /// 计算技能伤害
    /// </summary>
    private float CalculateDamage(ActiveSkill skill, UnitBase caster)
    {
        if (skill == null || caster == null || caster.attr == null) return DamageFormula.MinDamage;
        return DamageFormula.BuildSkillBase(skill.baseDamage, skill.damageMultiplier, caster.attr);
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
            if (enemy == null || enemy.isDead) continue;
            if (!GameConfig.IsInCombatViewport(enemy)) continue;
            float dist = Vector2.Distance(caster.transform.position, enemy.transform.position);
            if (dist <= range)
                enemies.Add(enemy);
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

    // ============================================================
    // 玩家本局携带技能（局内构筑：0..N 个，各自独立冷却）
    // ============================================================

    private readonly List<ActiveSkill> _playerSkills = new List<ActiveSkill>();

    /// <summary>玩家本局携带的主动技（0 号为职业初始技）。</summary>
    public List<ActiveSkill> GetPlayerSkills() => _playerSkills;

    public void SetPlayerSkills(List<ActiveSkill> skills)
    {
        _playerSkills.Clear();
        if (skills == null) return;
        for (int i = 0; i < skills.Count; i++)
            if (skills[i] != null) _playerSkills.Add(skills[i]);
    }

    public void ClearPlayerSkills() => _playerSkills.Clear();

    /// <summary>取第一个不在冷却中的玩家技能；全部冷却中返回 null。</summary>
    public ActiveSkill GetReadyPlayerSkill()
    {
        for (int i = 0; i < _playerSkills.Count; i++)
        {
            var s = _playerSkills[i];
            if (s == null) continue;
            if (!IsOnCooldown(s.skillId)) return s;
        }
        return null;
    }

    public bool HasReadyPlayerSkill() => GetReadyPlayerSkill() != null;

    /// <summary>玩家技能冷却比例 0~1（1=冷却中，用于技能条填充）。</summary>
    public float GetPlayerSkillCooldownRatio(int index)
    {
        if (index < 0 || index >= _playerSkills.Count) return 0f;
        var s = _playerSkills[index];
        if (s == null || s.cooldown <= 0.01f) return 0f;
        float remain = GetCooldownRemaining(s.skillId);
        return Mathf.Clamp01(remain / s.cooldown);
    }

    public bool IsOnCooldown(string skillId)
    {
        return _cooldowns.ContainsKey(skillId) && _cooldowns[skillId] > 0;
    }

    /// <summary>
    /// 外部登记冷却。Buff/治疗类玩家技能不走 <see cref="UseSkill"/>（走 BattleManager 的兜底分支），
    /// 冷却必须在这里补上，否则同一增益会随能量反复刷屏。
    /// </summary>
    public void RegisterCooldown(string skillId, float seconds)
    {
        if (string.IsNullOrEmpty(skillId) || seconds <= 0f) return;
        // 与 UseSkill 同样享受冷却缩减（Buff/治疗类走这条路，不进 UseSkill）
        _cooldowns[skillId] = ApplyCooldownReduce(seconds);
    }

    /// <summary>
    /// 把「冷却缩减」真正应用到冷却时长上。
    /// 来源：天赋 R10（AttrSystem.SkillCooldown）+ 装备词缀（ArmorAffixSystem/WeaponAffixSystem 都会写 CooldownReduce）。
    /// 之前这两个来源写进了属性却<b>没有任何地方读取</b> —— 点了天赋看不到变化，问题就在这里。
    /// 读取英雄属性：佣兵技能也走 SkillSystem，所以佣兵同样会吃到（与文案不冲突，属可接受范围）。
    /// </summary>
    public float ApplyCooldownReduce(float baseCooldown)
    {
        if (baseCooldown <= 0f) return baseCooldown;
        var hero = Hero.Instance;
        float reduce = hero != null && hero.attr != null
            ? hero.attr.GetAttr(AttrType.CooldownReduce)
            : 0f;
        reduce = Mathf.Clamp(reduce, 0f, GameConfig.SKILL_COOLDOWN_REDUCE_CAP);
        return baseCooldown * (1f - reduce);
    }

    public float GetCooldownRemaining(string skillId)
    {
        return _cooldowns.TryGetValue(skillId, out var cd) ? Mathf.Max(0, cd) : 0;
    }
}