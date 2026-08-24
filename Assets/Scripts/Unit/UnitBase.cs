using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// 所有战斗单位基类：英雄、怪物、佣兵全部继承这个，自动复用索敌/移动/攻击逻辑
/// 横版：英雄往右走，怪物从右侧刷新
///
/// 动画状态通过 UnitAnimation 统一控制：
/// - 移动时播放 MOVE，停止时播放 IDLE
/// - 攻击时触发 ATTACK
/// - 受伤时触发 DAMAGED
/// - 死亡时播放 DEATH，动画结束后回收对象
/// </summary>
public abstract class UnitBase : MonoBehaviour
{
    [Header("基础组件")]
    public Rigidbody2D rb;
    public SpriteRenderer sr;
    public Animator anim;

    [Header("动画配置")]
    [Tooltip("死亡动画播放时长（秒），播完后才回收对象")]
    public float deathAnimDuration = 0.8f;

    [Header("发射点/受击点")]
    [Tooltip("法球/弹幕发射位置（留空则自动创建在身体中部偏上）")]
    public Transform firePoint;
    [Tooltip("受击位置（留空则自动创建在身体中心）")]
    public Transform hitPoint;
    [Tooltip("发射点相对于根节点的偏移")]
    public Vector3 firePointOffset = new Vector3(0.3f, 0.8f, 0f);
    [Tooltip("受击点相对于根节点的偏移")]
    public Vector3 hitPointOffset = new Vector3(0f, 0.8f, 0f);

    /// <summary>所有单位固定的地面Y坐标（由AutoGameInitializer从SpawnPoint读取）</summary>
    public static float GROUND_Y = -3.5f;

    public AttrSystem attr = new AttrSystem();
    public float currentHp;
    protected float attackCd = 0;
    protected UnitBase target;
    public bool isDead => currentHp <= 0;
    public int facingDir = 1; // 1右 -1左
    public bool isAlly; // true己方 false敌方
    /// <summary>精灵默认是否朝右（SPUM预制体朝向不一致，需子类设置）</summary>
    public bool spriteDefaultFacesRight = true;

    /// <summary>统一动画控制器（自动桥接SPUM或原生Animator）</summary>
    protected UnitAnimation unitAnim;
    /// <summary>是否正在执行死亡流程（防止重复进入）</summary>
    protected bool _isDying = false;

    protected virtual void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (anim == null) anim = GetComponent<Animator>();

        // 初始化动画控制器（没有就自动添加）
        unitAnim = GetComponent<UnitAnimation>();
        if (unitAnim == null)
            unitAnim = gameObject.AddComponent<UnitAnimation>();

        // 自动创建发射点和受击点
        EnsureFirePoint();
        EnsureHitPoint();
    }

    /// <summary>
    /// 尊重预制体原有的SortingLayer/SortingOrder，不再强制覆盖
    /// 只在子节点没有设置层级时，才根据 isAlly 赋予默认值
    /// 这样用户可以在预制体中自由调整层级关系
    /// </summary>
    public void ApplySortingLayer()
    {
        GameConfig.ApplyUnitSorting(transform);
    }

    void SetSortingLayerRecursive(Transform t, string defaultLayerName, int defaultOrder)
    {
        SpriteRenderer childSr = t.GetComponent<SpriteRenderer>();
        if (childSr != null)
        {
            // 如果当前SortingLayer是Default(0)或空，才使用默认值
            // 否则保留预制体中设置好的层级
            if (string.IsNullOrEmpty(childSr.sortingLayerName) || childSr.sortingLayerName == "Default")
            {
                childSr.sortingLayerName = defaultLayerName;
                childSr.sortingOrder = defaultOrder;
            }
        }
        foreach (Transform child in t)
        {
            SetSortingLayerRecursive(child, defaultLayerName, defaultOrder);
        }
    }

    /// <summary>
    /// 确保发射点存在（法球/弹幕从这里飞出）
    /// 兼容预制体中已有的 "FirePoint" 或 "fire" 节点
    /// </summary>
    protected void EnsureFirePoint()
    {
        if (firePoint != null) return;

        // 查找已有子物体（兼容多种命名）
        Transform existing = transform.Find("FirePoint");
        if (existing == null) existing = transform.Find("fire");
        if (existing != null)
        {
            firePoint = existing;
            return;
        }

        // 自动创建
        GameObject fp = new GameObject("FirePoint");
        fp.transform.SetParent(transform, false);
        fp.transform.localPosition = firePointOffset;
        firePoint = fp.transform;
    }

    /// <summary>
    /// 确保受击点存在（法球飞到这里后爆炸，伤害数字从这里弹出）
    /// 兼容预制体中已有的 "HitPoint" 或 "beattack" 节点
    /// 自动计算精灵的视觉中心位置（而非图片原点/pivot）
    /// </summary>
    protected void EnsureHitPoint()
    {
        if (hitPoint != null) return;

        // 查找已有子物体（兼容多种命名）
        Transform existing = transform.Find("HitPoint");
        if (existing == null) existing = transform.Find("beattack");
        if (existing != null)
        {
            hitPoint = existing;
            return;
        }

        // 创建VfxCenter父节点，位置在精灵视觉中心
        GameObject hp = new GameObject("HitPoint");
        hp.transform.SetParent(transform, false);

        // 延迟到下一帧计算中心（精灵可能还没加载）
        StartCoroutine(CalcHitPointCenter(hp.transform));
        // 先用偏移兜底
        hp.transform.localPosition = hitPointOffset;
        hitPoint = hp.transform;
    }

    System.Collections.IEnumerator CalcHitPointCenter(Transform hpTransform)
    {
        yield return null; // 等一帧，确保精灵已加载
        yield return null; // 再等一帧，SPUM可能需要两帧才完全加载

        // 获取所有SpriteRenderer，合并bounds计算视觉中心
        SpriteRenderer[] allSrs = GetComponentsInChildren<SpriteRenderer>(true);
        if (allSrs.Length == 0) yield break;

        // 初始化bounds为第一个有sprite的Renderer
        Bounds combinedBounds = new Bounds();
        bool hasValid = false;
        foreach (var r in allSrs)
        {
            if (r == null || r.sprite == null) continue;
            if (!hasValid)
            {
                combinedBounds = r.bounds;
                hasValid = true;
            }
            else
            {
                combinedBounds.Encapsulate(r.bounds);
            }
        }

        if (hasValid)
        {
            // 用合并bounds的中心作为受击点（世界坐标转本地坐标）
            Vector3 worldCenter = combinedBounds.center;
            Vector3 localCenter = transform.InverseTransformPoint(worldCenter);
            hpTransform.localPosition = localCenter;
        }
    }

    /// <summary>
    /// 获取发射点世界坐标（考虑朝向翻转）
    /// </summary>
    public virtual Vector3 GetFirePosition()
    {
        if (firePoint != null) return firePoint.position;
        return transform.position + new Vector3(firePointOffset.x * facingDir, firePointOffset.y, 0);
    }

    /// <summary>
    /// 获取受击点世界坐标
    /// </summary>
    public virtual Vector3 GetHitPosition()
    {
        if (hitPoint != null) return hitPoint.position;
        return transform.position + hitPointOffset;
    }

    protected virtual void Update()
    {
        if (isDead) return;
        attackCd -= Time.deltaTime;
        AIUpdate();
    }

    protected virtual void AIUpdate()
    {
        // 开战传送演出期间冻结
        if (BattleManager.Instance != null && !BattleManager.Instance.UnitsCanAct)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            if (unitAnim != null) unitAnim.SetMove(false, facingDir);
            return;
        }

        target = FindNearestEnemyInDetectRange();
        // 安全检查：绝不对同阵营单位出手
        if (target != null && target.isAlly == isAlly)
        {
            target = null;
        }
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
                // 攻击范围内：停步攻击
                if (rb != null) rb.velocity = Vector2.zero;
                if (attackCd <= 0)
                {
                    Attack(target);
                    attackCd = GetAttackCooldown();
                }
            }
            else
            {
                // 索敌范围内、攻击范围外：靠近目标
                if (rb != null)
                    rb.velocity = new Vector2(dir * attr.GetAttr(AttrType.MoveSpeed), rb.velocity.y);
                isMoving = true;
            }
        }
        else
        {
            if (isAlly)
            {
                // 索敌范围内无敌人：向右推进
                facingDir = 1;
                ApplyFacing(facingDir);
                if (rb != null)
                    rb.velocity = new Vector2(attr.GetAttr(AttrType.MoveSpeed), rb.velocity.y);
                isMoving = true;
            }
            else
            {
                // 敌方无目标：向左推进
                facingDir = -1;
                ApplyFacing(facingDir);
                if (rb != null)
                    rb.velocity = new Vector2(-attr.GetAttr(AttrType.MoveSpeed), rb.velocity.y);
                isMoving = true;
            }
        }

        // 更新移动/站立动画
        if (unitAnim != null)
            unitAnim.SetMove(isMoving, facingDir);

        // 仅钳制己方，避免屏外刷怪被拉到右缘导致「够不着/不攻击」
        // 通关走向传送门时放宽
        if (isAlly && (BattleManager.Instance == null || !BattleManager.Instance.PortalWalkMode))
            ClampToScreen();
    }

    /// <summary>
    /// 应用朝向：程序化动画怪物用sr.flipX，SPUM角色翻转整个transform
    /// SPUM角色有多个身体部件SpriteRenderer，必须翻转整个transform才能让所有部件一起翻转
    /// 【v5】proc模式也考虑spriteDefaultFacesRight，与Monster override保持一致
    /// </summary>
    protected virtual void ApplyFacing(int dir)
    {
        if (dir == 0) return;

        bool isProc = unitAnim != null && unitAnim.IsProcMode;
        if (isProc)
        {
            // 程序化模式（怪物等）：用sr.flipX翻转
            // 必须考虑精灵默认朝向：spriteDefaultFacesRight=false(朝左)时需要反转dir
            int visualDir = spriteDefaultFacesRight ? dir : -dir;
            if (sr != null) sr.flipX = visualDir < 0;
            return;
        }

        // SPUM角色：翻转整个transform
        Vector3 scale = transform.localScale;
        float absX = Mathf.Abs(scale.x);
        if (absX < 0.0001f) absX = 1f;

        // 原始预制体面朝右时：朝右=+X，朝左=-X
        // 原始预制体面朝左时：视觉朝右需要 scale.x=-1（镜像），朝左=+1
        if (spriteDefaultFacesRight)
        {
            scale.x = (dir > 0) ? absX : -absX;
        }
        else
        {
            scale.x = (dir > 0) ? -absX : absX;
        }
        transform.localScale = scale;
    }

    /// <summary>限制单位不超出屏幕可见范围（放宽边距，减少贴边卡顿）</summary>
    protected void ClampToScreen()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        float camX = cam.transform.position.x;
        float margin = 1.5f;
        float minX = camX - halfW - margin;
        float maxX = camX + halfW + margin;
        Vector3 pos = transform.position;
        if (pos.x < minX || pos.x > maxX)
        {
            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            GameConfig.SetWorldPosition(transform, pos);
        }
    }

    public virtual UnitBase FindNearestEnemy()
    {
        return FindNearestEnemyInDetectRange();
    }

    /// <summary>只在索敌范围内找最近敌人；超出索敌范围不锁定（避免全图追杀）</summary>
    public virtual UnitBase FindNearestEnemyInDetectRange()
    {
        if (BattleManager.Instance == null) return null;

        float detectRange = GetDetectRange();
        UnitBase nearest = null;
        float minDist = detectRange;
        float myX = GetCombatX(this);
        IEnumerable<UnitBase> enemyList = isAlly ? BattleManager.Instance.monsters : BattleManager.Instance.allyUnits;
        foreach (var enemy in enemyList)
        {
            if (enemy == null || enemy.isDead) continue;
            if (isAlly && enemy.isAlly) continue;
            if (!isAlly && !enemy.isAlly) continue;
            float dist = Mathf.Abs(myX - GetCombatX(enemy));
            if (dist <= minDist)
            {
                minDist = dist;
                nearest = enemy;
            }
        }
        return nearest;
    }

    /// <summary>
    /// 索敌范围：比攻击范围略大，避免擦肩而过不打架。
    /// </summary>
    public virtual float GetDetectRange()
    {
        float atk = attr != null ? attr.GetAttr(AttrType.AttackRange) : GameConfig.BASE_ATTACK_RANGE;
        return GameConfig.GetDetectRangeFromAttackRange(atk);
    }

    /// <summary>索敌/攻击距离：以可见 transform.x 为准，并纠正偏离的 Rigidbody2D</summary>
    public static float GetCombatX(UnitBase u)
    {
        if (u == null) return 0f;
        float x = u.transform.position.x;
        if (u.rb != null && Mathf.Abs(u.rb.position.x - x) > 0.05f)
            u.rb.position = new Vector2(x, u.transform.position.y);
        return x;
    }

    protected virtual void Attack(UnitBase target)
    {
        if (target == null || target.attr == null || attr == null)
            return;

        float damage = DamageFormula.BuildAttackRaw(attr, out bool isCrit);

        bool openingHit = isAlly && GameConfig.IsOpeningStage();
        if (openingHit)
            damage = GameConfig.RollOpeningAllyHitDamage(isCrit);

        AttackVfxKit kit = GetAttackVfxKit();
        if (unitAnim != null)
            unitAnim.PlayAttack(kit);

        VfxFaction faction = isAlly ? VfxFaction.Ally : VfxFaction.Enemy;
        Vector3 firePos = GetFirePosition();
        Vector3 hitPos = target.GetHitPosition();
        Transform hitTf = target.transform;

        // 弓/法球：飞到再结算，与技能弹道一致，便于后续扩展命中点玩法
        if ((kit == AttackVfxKit.Bow || kit == AttackVfxKit.Orb) && BattleVFXSystem.Instance != null)
        {
            UnitBase locked = target;
            float dmg = damage;
            bool crit = isCrit;
            bool opening = openingHit;
            BattleVFXSystem.Instance.PlaySkillProjectile(
                faction, firePos, hitPos, facingDir, hitTf, kit,
                null, 1f, 1f,
                () => ResolveBasicAttackHit(locked, dmg, crit, opening));
            return;
        }

        ResolveBasicAttackHit(target, damage, isCrit, openingHit);
        if (BattleVFXSystem.Instance != null)
            BattleVFXSystem.Instance.PlayAttackKit(kit, faction, firePos, hitPos, facingDir, hitTf, isCrit);
    }

    void ResolveBasicAttackHit(UnitBase target, float damage, bool isCrit, bool openingHit)
    {
        if (target == null || target.isDead || target.attr == null) return;

        float dodgeChance = target.attr.GetAttr(AttrType.Dodge);
        if (dodgeChance > 0 && Random.value < dodgeChance)
        {
            DamageTextSystem.Instance?.SpawnDodgeText(target.GetHitPosition());
            OnAttack?.Invoke(target, 0, false);
            return;
        }

        target.TakeDamage(damage, isCrit, openingHit);
        OnAttack?.Invoke(target, damage, isCrit);
    }

    /// <summary>
    /// 普攻特效套装。盾兵等近战走刀光；弓走飞行箭；法术走飞行法球。
    /// </summary>
    protected virtual AttackVfxKit GetAttackVfxKit()
    {
        WeaponAttackType atkType = GetAttackType();
        float range = attr != null ? attr.GetAttr(AttrType.AttackRange) : 1.5f;
        return SkillNaming.KitFromAttackType(atkType, range);
    }

    /// <summary>攻击间隔；弓/法球额外乘 PROJECTILE_ATK_SPEED_MUL（降发射频率）</summary>
    protected float GetAttackCooldown()
    {
        float atkSpd = Mathf.Max(0.05f, attr.GetAttr(AttrType.AttackSpeed));
        AttackVfxKit kit = GetAttackVfxKit();
        if (kit == AttackVfxKit.Bow || kit == AttackVfxKit.Orb)
            atkSpd *= GameConfig.PROJECTILE_ATK_SPEED_MUL;
        if (isAlly && GameConfig.IsOpeningStage())
            atkSpd *= 0.55f;
        return 1f / Mathf.Max(0.05f, atkSpd);
    }

    /// <summary>
    /// 获取单位的攻击类型，子类可重写
    /// </summary>
    protected virtual WeaponAttackType GetAttackType()
    {
        return WeaponAttackType.Physical;
    }

    public virtual void TakeDamage(float damage, bool isCrit, bool ignoreDefense = false)
    {
        if (_isDying) return;

        float finalDamage = DamageFormula.FinalHit(damage, attr, ignoreDefense);

        currentHp -= finalDamage;
        DamageTextSystem.Instance?.SpawnDamageText(GetHitPosition(), Mathf.RoundToInt(finalDamage), isCrit);

        if (unitAnim != null)
            unitAnim.PlayDamaged();

        if (currentHp <= 0)
            Die();
    }

    protected virtual void Die()
    {
        if (_isDying) return;
        _isDying = true;

        // 停止移动
        if (rb != null) rb.velocity = Vector2.zero;

        // 立即触发死亡事件（给奖励、UI更新等游戏逻辑）
        OnDead?.Invoke(this);

        // 播放死亡动画
        if (unitAnim != null)
            unitAnim.PlayDeath(facingDir);

        // 延迟回收（等死亡动画播完）
        StartCoroutine(DeathReleaseCoroutine());
    }

    /// <summary>
    /// 死亡动画播完后的回收逻辑，子类可重写
    /// 默认：回对象池
    /// Hero：隐藏自身（不回池）
    /// Mercenary：Destroy
    /// </summary>
    protected virtual void OnDeathRelease()
    {
        PoolManager.Instance?.Release(gameObject);
    }

    private IEnumerator DeathReleaseCoroutine()
    {
        if (deathAnimDuration > 0f)
            yield return new WaitForSeconds(deathAnimDuration);
        OnDeathRelease();
    }

    /// <summary>
    /// 重置单位状态（从对象池复用时调用）
    /// 子类的 Init 方法中应调用 base.ResetForReuse()
    /// </summary>
    public virtual void ResetForReuse()
    {
        _isDying = false;
        attackCd = 0;
        target = null;
        OnDead = null;
        if (unitAnim != null)
            unitAnim.ResetToIdle();
        if (rb != null)
            rb.velocity = Vector2.zero;
    }

    public System.Action<UnitBase, float, bool> OnAttack;
    public System.Action<UnitBase> OnDead;
}