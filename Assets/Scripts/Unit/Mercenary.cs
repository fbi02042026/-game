using UnityEngine;

/// <summary>
/// 佣兵类：使用SPUM预设角色形象，作为己方战斗单位
/// 索敌范围锁定 → 攻击范围内出手；无目标时保持在主角身后间距
/// </summary>
public class Mercenary : UnitBase
{
    public string mercId;
    public int mercLevel = 1;
    /// <summary>本局佩戴主动技能（来自存档；空则无主动技）</summary>
    public string equippedSkillId;
    /// <summary>本局佩戴被动技能</summary>
    public string equippedPassiveSkillId;

    public MercSkillCaster SkillCaster { get; private set; }
    public MercPassiveRunner PassiveRunner { get; private set; }

    private UnitBase _lastLoggedTarget = null;
    private int _partyIndex = -1;
    /// <summary>引导：原地眩晕，不跑 AI，受击不死。</summary>
    public bool TutorialStunned { get; private set; }
    float _stunAnimTimer;

    protected override void Awake()
    {
        firePointOffset = new Vector3(0.3f, 0.32f, 0f);
        hitPointOffset = new Vector3(0f, 0.55f, 0f);

        base.Awake();
        isAlly = true;
        spriteDefaultFacesRight = false;
    }

    public void SetTutorialStunned(bool on)
    {
        TutorialStunned = on;
        if (rb != null) rb.velocity = Vector2.zero;
        if (unitAnim != null)
        {
            unitAnim.SetMove(false, facingDir);
            if (on) unitAnim.PlayDebuff();
            else unitAnim.ClearDebuff();
        }
        _stunAnimTimer = 0f;
    }

    /// <summary>围殴结束：停眩晕循环动画，但仍可保持 TutorialStunned 定身到对话完。</summary>
    public void StopTutorialStunAnim()
    {
        _stunAnimTimer = 9999f; // 阻止 AIUpdate 里循环 PlayDebuff
        if (unitAnim != null)
            unitAnim.ClearDebuff();
    }

    public void SetupBattleSkills(string activeId, string passiveId)
    {
        equippedSkillId = activeId;
        equippedPassiveSkillId = passiveId;
        if (SkillCaster == null) SkillCaster = gameObject.GetComponent<MercSkillCaster>();
        if (SkillCaster == null) SkillCaster = gameObject.AddComponent<MercSkillCaster>();
        if (PassiveRunner == null) PassiveRunner = gameObject.GetComponent<MercPassiveRunner>();
        if (PassiveRunner == null) PassiveRunner = gameObject.AddComponent<MercPassiveRunner>();
        SkillCaster.Bind(this, activeId);
        PassiveRunner.Bind(this, passiveId);
    }

    public override void TakeDamage(float damage, bool isCrit, bool ignoreDefense = false)
    {
        if (TutorialStunned)
        {
            float defense = ignoreDefense ? 0f : attr.GetAttr(AttrType.Defense);
            float finalDamage = Mathf.Max(1f, damage - defense);
            currentHp = Mathf.Max(1f, currentHp - finalDamage * 0.35f);
            DamageTextSystem.Instance?.SpawnDamageText(GetHitPosition(), Mathf.RoundToInt(finalDamage * 0.35f), isCrit);
            if (unitAnim != null)
            {
                unitAnim.PlayDamaged();
                _stunAnimTimer = 0.35f;
            }
            return;
        }

        if (PassiveRunner != null)
            damage = PassiveRunner.ModifyIncomingDamage(damage);

        float before = currentHp;
        base.TakeDamage(damage, isCrit, ignoreDefense);
        if (PassiveRunner != null && !Mathf.Approximately(before, currentHp))
            PassiveRunner.OnHpChanged();
    }

    protected override void Attack(UnitBase target)
    {
        base.Attack(target);
        if (PassiveRunner != null && target != null && !target.isDead)
            PassiveRunner.OnBasicAttackHit(target, attr != null ? attr.GetAttr(AttrType.Attack) : 0f);
    }

    public void Init(string id, int level = 1)
    {
        mercId = id;
        mercLevel = level;
        gameObject.name = "Merc_" + id;

        ResetForReuse();

        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();

        SetupAttributes(id, level);
        Face(1);

        Debug.Log($"[Mercenary:{id}] Init完成 | isAlly={isAlly} | facingDir={facingDir} | pos={transform.position}");
    }

    // Face() 已在 UnitBase

    void SetupAttributes(string id, int level)
    {
        attr.ResetToBase();
        level = Mathf.Max(1, level);

        if (MercRosterDefs.TryGetByAssetId(id, out _))
        {
            MercRosterDefs.ApplyCombatStats(id, level,
                out float hp, out float atk, out float def, out float atkSpd, out float move, out float range);
            attr.SetAttr(AttrType.MaxHp, hp);
            attr.SetAttr(AttrType.Attack, atk);
            attr.SetAttr(AttrType.Defense, def);
            attr.SetAttr(AttrType.AttackSpeed, atkSpd);
            attr.SetAttr(AttrType.MoveSpeed, move);
            attr.SetAttr(AttrType.AttackRange, range);
            attr.SetAttr(AttrType.CritRate, GameConfig.BASE_CRIT_RATE);
            currentHp = attr.GetAttr(AttrType.MaxHp);
            return;
        }

        bool advanced = GameConfig.GetMercTier(id) == MercTier.Advanced;
        float baseHp, baseAtk, baseDef, atkInterval;
        float atkRange = GameConfig.RangeSword;

        if (id.StartsWith("dunbing"))
        {
            if (advanced) { baseHp = 550; baseAtk = 18; baseDef = 20; atkInterval = 1.1f; }
            else { baseHp = 300; baseAtk = 10; baseDef = 10; atkInterval = 1.2f; }
            atkRange = GameConfig.RangeSword;
        }
        else if (id.StartsWith("gongshou"))
        {
            if (advanced) { baseHp = 280; baseAtk = 35; baseDef = 5; atkInterval = 0.85f; }
            else { baseHp = 150; baseAtk = 20; baseDef = 3; atkInterval = 0.9f; }
            atkRange = GameConfig.RangeBow;
        }
        else if (id.StartsWith("kuangzhan"))
        {
            if (advanced) { baseHp = 280; baseAtk = 35; baseDef = 5; atkInterval = 0.85f; }
            else { baseHp = 150; baseAtk = 20; baseDef = 3; atkInterval = 0.9f; }
            atkRange = GameConfig.RangeSword;
        }
        else if (id.StartsWith("naima") || id.StartsWith("fashi") || id.StartsWith("mushi"))
        {
            if (advanced) { baseHp = 320; baseAtk = 15; baseDef = 8; atkInterval = 1.3f; }
            else { baseHp = 180; baseAtk = 8; baseDef = 4; atkInterval = 1.5f; }
            atkRange = GameConfig.RangeStaff;
        }
        else if (id.StartsWith("zhongzhan"))
        {
            if (advanced) { baseHp = 360; baseAtk = 22; baseDef = 10; atkInterval = 1f; }
            else { baseHp = 200; baseAtk = 12; baseDef = 5; atkInterval = 1.1f; }
            atkRange = GameConfig.RangePolearm;
        }
        else
        {
            if (advanced) { baseHp = 360; baseAtk = 22; baseDef = 10; atkInterval = 1f; }
            else { baseHp = 200; baseAtk = 12; baseDef = 5; atkInterval = 1.1f; }
            atkRange = GameConfig.RangePolearm;
        }

        float hpMul = 1f + (level - 1) * 0.1f;
        float atkAdd = (level - 1) * 2f;
        attr.SetAttr(AttrType.MaxHp, baseHp * hpMul);
        attr.SetAttr(AttrType.Attack, baseAtk + atkAdd);
        attr.SetAttr(AttrType.Defense, baseDef);
        attr.SetAttr(AttrType.AttackSpeed, 1f / Mathf.Max(0.2f, atkInterval));
        attr.SetAttr(AttrType.MoveSpeed, GameConfig.BASE_MOVE_SPEED);
        attr.SetAttr(AttrType.AttackRange, atkRange);
        attr.SetAttr(AttrType.CritRate, GameConfig.BASE_CRIT_RATE);

        currentHp = attr.GetAttr(AttrType.MaxHp);
    }

    protected override WeaponAttackType GetAttackType()
    {
        if (mercId == null) return WeaponAttackType.Physical;
        if (mercId.StartsWith("gongshou"))
            return WeaponAttackType.Physical;
        if (mercId.StartsWith("naima") || mercId.StartsWith("fashi") || mercId.StartsWith("mushi"))
            return WeaponAttackType.Magic;
        return WeaponAttackType.Physical;
    }

    protected override AttackVfxKit GetAttackVfxKit()
    {
        if (mercId != null && mercId.StartsWith("gongshou"))
            return AttackVfxKit.Bow;
        if (mercId != null && (mercId.StartsWith("naima") || mercId.StartsWith("fashi") || mercId.StartsWith("mushi")))
            return AttackVfxKit.Orb;
        return AttackVfxKit.MeleeSlash;
    }

    protected override void OnDeathRelease()
    {
        Destroy(gameObject);
    }

    int ResolvePartyIndex()
    {
        if (_partyIndex >= 0) return _partyIndex;
        var mm = MercenaryManager.Instance;
        if (mm == null) return 0;
        var list = mm.GetActiveMercs();
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == this) { _partyIndex = i; return i; }
        }
        return 0;
    }

    public void SetPartyIndex(int index) => _partyIndex = index;

    public override float GetDetectRange()
    {
        float baseRange = base.GetDetectRange();
        // 佣兵缩在玩家身后时，默认索敌距离不够；场上有怪则扩大
        if (BattleManager.Instance != null && BattleManager.Instance.GetAliveMonsterCount() > 0)
            return Mathf.Max(baseRange, 10f);
        return baseRange;
    }

    protected override void AIUpdate()
    {
        if (TutorialStunned)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            // 仅在仍需眩晕表现时循环；StopTutorialStunAnim 会把 timer 拉高关掉
            if (_stunAnimTimer < 9000f)
            {
                _stunAnimTimer -= Time.deltaTime;
                if (_stunAnimTimer <= 0f && unitAnim != null)
                {
                    unitAnim.PlayDebuff();
                    _stunAnimTimer = 1.6f;
                }
            }
            return;
        }

        if (BattleManager.Instance != null && !BattleManager.Instance.UnitsCanAct)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            if (unitAnim != null) unitAnim.SetMove(false, facingDir);
            return;
        }

        // 索敌范围内才锁定
        target = FindNearestEnemyInDetectRange();

        if (target != _lastLoggedTarget)
        {
            if (target != null)
                Debug.Log($"[Mercenary:{mercId}] 索敌锁定: {target.name} dist={Mathf.Abs(GetCombatX(this) - GetCombatX(target)):F1}");
            _lastLoggedTarget = target;
        }

        if (target != null && target.isAlly) target = null;
        if (target != null && BattleManager.Instance != null && BattleManager.Instance.allyUnits.Contains(target))
            target = null;

        bool isMoving = false;

        if (target != null)
        {
            float distance = Mathf.Abs(GetCombatX(this) - GetCombatX(target));
            float attackRange = attr.GetAttr(AttrType.AttackRange);
            float dir = GetCombatX(target) > GetCombatX(this) ? 1 : -1;
            facingDir = (int)dir;
            ApplyFacing(facingDir);
            if (distance <= attackRange)
            {
                if (rb != null) rb.velocity = Vector2.zero;
                if (attackCd <= 0)
                {
                    Attack(target);
                    attackCd = GetAttackCooldown();
                }
            }
            else if (UnitCrowd.IsBlockedByFrontAlly(this, dir))
            {
                if (rb != null) rb.velocity = Vector2.zero;
                isMoving = false;
            }
            else
            {
                if (rb != null)
                    rb.velocity = new Vector2(dir * attr.GetAttr(AttrType.MoveSpeed), rb.velocity.y);
                isMoving = true;
            }
        }
        else
        {
            bool noMonsters = BattleManager.Instance == null
                || BattleManager.Instance.GetAliveMonsterCount() <= 0;
            Hero h = Hero.Instance;
            facingDir = 1;
            ApplyFacing(facingDir);
            if (noMonsters)
            {
                if (rb != null) rb.velocity = new Vector2(0f, rb.velocity.y);
                isMoving = false;
            }
            else
            {
                // 有怪但索敌没锁到：主动前压接战，不要只跟在身后
                UnitBase foe = FindNearestEnemyInDetectRange();
                if (foe != null)
                {
                    float distance = Mathf.Abs(GetCombatX(this) - GetCombatX(foe));
                    float attackRange = attr.GetAttr(AttrType.AttackRange);
                    float dir = GetCombatX(foe) > GetCombatX(this) ? 1 : -1;
                    facingDir = (int)dir;
                    ApplyFacing(facingDir);
                    if (distance <= attackRange)
                    {
                        if (rb != null) rb.velocity = Vector2.zero;
                        if (attackCd <= 0)
                        {
                            Attack(foe);
                            attackCd = GetAttackCooldown();
                        }
                    }
                    else if (UnitCrowd.IsBlockedByFrontAlly(this, dir))
                    {
                        if (rb != null) rb.velocity = Vector2.zero;
                    }
                    else
                    {
                        if (rb != null)
                            rb.velocity = new Vector2(dir * attr.GetAttr(AttrType.MoveSpeed), rb.velocity.y);
                        isMoving = true;
                    }
                }
                else if (h != null && !h.isDead)
                {
                float desiredX = GetCombatX(h) - BattleManager.MERC_BEHIND_SPACING * (ResolvePartyIndex() + 1);
                float dx = desiredX - GetCombatX(this);
                float spd = attr.GetAttr(AttrType.MoveSpeed);
                if (Mathf.Abs(dx) > 0.08f)
                {
                    float vx = Mathf.Sign(dx) * Mathf.Min(spd * 1.25f, Mathf.Abs(dx) * 8f);
                    if (rb != null) rb.velocity = new Vector2(vx, rb.velocity.y);
                    isMoving = true;
                }
                else
                {
                    if (rb != null) rb.velocity = new Vector2(0f, rb.velocity.y);
                    isMoving = false;
                }
                }
                else if (rb != null)
                {
                    rb.velocity = new Vector2(0f, rb.velocity.y);
                    isMoving = false;
                }
            }
        }

        if (unitAnim != null)
            unitAnim.SetMove(isMoving, facingDir);
        // 不用 ClampToScreen，避免把佣兵挤到玩家身上
    }
}
