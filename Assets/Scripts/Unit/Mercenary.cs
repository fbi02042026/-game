using UnityEngine;

/// <summary>
/// 佣兵类：使用SPUM预设角色形象，作为己方战斗单位
/// 索敌范围锁定 → 攻击范围内出手；无目标时保持在主角身后间距
/// </summary>
public class Mercenary : UnitBase
{
    public string mercId;
    public int mercLevel = 1;

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
        base.TakeDamage(damage, isCrit, ignoreDefense);
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

    public void Face(int dir)
    {
        facingDir = dir == 0 ? 1 : dir;
        ApplyFacing(facingDir);
    }

    void SetupAttributes(string id, int level)
    {
        attr.ResetToBase();

        bool advanced = GameConfig.GetMercTier(id) == MercTier.Advanced;
        float baseHp, baseAtk, baseDef, atkInterval;
        // 攻击距离对齐数值表武器「攻击范围(像素)」→ 世界单位
        float atkRange = GameConfig.RangeSword;

        if (id.StartsWith("dunbing"))
        {
            // 刀盾：近战，按单手剑射程
            if (advanced) { baseHp = 550; baseAtk = 18; baseDef = 20; atkInterval = 1.1f; }
            else { baseHp = 300; baseAtk = 10; baseDef = 10; atkInterval = 1.2f; }
            atkRange = GameConfig.RangeSword;
        }
        else if (id.StartsWith("gongshou"))
        {
            // 弓箭 300px
            if (advanced) { baseHp = 280; baseAtk = 35; baseDef = 5; atkInterval = 0.85f; }
            else { baseHp = 150; baseAtk = 20; baseDef = 3; atkInterval = 0.9f; }
            atkRange = GameConfig.RangeBow;
        }
        else if (id.StartsWith("kuangzhan"))
        {
            // 双刀/近战输出：单手剑射程；大剑感可用 Greatsword
            if (advanced) { baseHp = 280; baseAtk = 35; baseDef = 5; atkInterval = 0.85f; }
            else { baseHp = 150; baseAtk = 20; baseDef = 3; atkInterval = 0.9f; }
            atkRange = GameConfig.RangeSword;
        }
        else if (id.StartsWith("naima") || id.StartsWith("fashi") || id.StartsWith("mushi"))
        {
            // 法杖 120px
            if (advanced) { baseHp = 320; baseAtk = 15; baseDef = 8; atkInterval = 1.3f; }
            else { baseHp = 180; baseAtk = 8; baseDef = 4; atkInterval = 1.5f; }
            atkRange = GameConfig.RangeStaff;
        }
        else
        {
            // 长柄/控制 180px
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

    protected override void AIUpdate()
    {
        if (TutorialStunned)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            _stunAnimTimer -= Time.deltaTime;
            if (_stunAnimTimer <= 0f && unitAnim != null)
            {
                unitAnim.PlayDebuff();
                _stunAnimTimer = 1.6f;
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
            else
            {
                if (rb != null)
                    rb.velocity = new Vector2(dir * attr.GetAttr(AttrType.MoveSpeed), rb.velocity.y);
                isMoving = true;
            }
        }
        else
        {
            // 无敌人：保持在主角身后固定间距（拉开站位）
            Hero h = Hero.Instance;
            facingDir = 1;
            ApplyFacing(facingDir);
            if (h != null && !h.isDead)
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
                    // 贴住理想站位，跟英雄同速前进
                    if (rb != null) rb.velocity = new Vector2(spd, rb.velocity.y);
                    isMoving = true;
                }
            }
            else if (rb != null)
            {
                rb.velocity = new Vector2(attr.GetAttr(AttrType.MoveSpeed), rb.velocity.y);
                isMoving = true;
            }
        }

        if (unitAnim != null)
            unitAnim.SetMove(isMoving, facingDir);
        // 不用 ClampToScreen，避免把佣兵挤到玩家身上
    }
}
