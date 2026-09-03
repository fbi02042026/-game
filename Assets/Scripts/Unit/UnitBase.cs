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
    /// <summary>相对站立线的上下偏移，用于加宽地面后的前后排站位。</summary>
    public float LaneY { get; private set; }
    public float FootY => GROUND_Y + LaneY;

    public void SetLaneY(float offset)
    {
        LaneY = Mathf.Clamp(offset, -GameConfig.BATTLE_LANE_HALF, GameConfig.BATTLE_LANE_HALF);
    }

    /// <summary>把 LaneY 同步成当前脚底相对站立线的偏移（入场中断 / 刷怪纠偏）。</summary>
    public void SyncLaneYFromWorld()
    {
        var t = LaneMoveTransform;
        if (t == null) t = transform;
        SetLaneY(t.position.y - GROUND_Y);
    }

    /// <summary>目标实际站位车道（优先世界 Y，避免 LaneY 与脚底脱节）。</summary>
    public float GetWorldLaneOffset()
    {
        Transform moveTf = transform;
        if (this is Monster mon)
            moveTf = mon.GetBodyTransform();
        if (moveTf == null) moveTf = transform;
        return Mathf.Clamp(moveTf.position.y - GROUND_Y, -GameConfig.BATTLE_LANE_HALF, GameConfig.BATTLE_LANE_HALF);
    }

    protected virtual Transform LaneMoveTransform => transform;

    protected void ApplyLaneY(float dt)
    {
        var t = LaneMoveTransform;
        if (t == null) return;
        var p = t.position;
        float target = FootY;
        if (Mathf.Abs(p.y - target) < 0.002f) return;
        p.y = Mathf.MoveTowards(p.y, target, GameConfig.BATTLE_LANE_MOVE_SPEED * dt);
        GameConfig.SetWorldPosition(t, p);
    }

    /// <summary>最近一次造成伤害的来源（结算 MVP 击杀归属）。</summary>
    public UnitBase LastDamageSource { get; private set; }

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
            // 预制体挂点位置不可信（常年贴脚），一律重算到躯干中心
            StartCoroutine(CalcHitPointCenter(existing));
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

    protected System.Collections.IEnumerator CalcHitPointCenter(Transform hpTransform)
    {
        yield return null; // 等一帧，确保精灵已加载
        yield return null; // 再等一帧，SPUM可能需要两帧才完全加载
        if (hpTransform == null) yield break;

        // 怪物图多为 32×32 透明填充：优先用不透明像素包围盒中心，避免整图画布中心
        if (TryPlaceHitPointByOpaqueSprite(hpTransform))
            yield break;

        if (!TryGetBodyBounds(out Bounds body))
        {
            hpTransform.localPosition = hitPointOffset;
            yield break;
        }

        // 世界空间定位躯干中心。预制体根节点常带缩放（SPUM），
        // 在局部空间夹取会把挂点压到脚底，这里一律走 world position。
        hpTransform.position = GetBodyCenterWorld(body);
        Vector3 lp = hpTransform.localPosition;
        hpTransform.localPosition = new Vector3(lp.x, lp.y, 0f);
    }

    /// <summary>按贴图不透明像素中心摆受击点（怪物表）；成功返回 true。</summary>
    protected virtual bool TryPlaceHitPointByOpaqueSprite(Transform hpTransform)
    {
        if (hpTransform == null) return false;
        if (!TryGetPrimaryBodySprite(out SpriteRenderer bodySr)) return false;
        if (!MonsterSpriteOpaqueTable.TryGetOpaqueCenterWorld(bodySr, out Vector3 world))
            return false;
        hpTransform.position = world;
        Vector3 lp = hpTransform.localPosition;
        hpTransform.localPosition = new Vector3(lp.x, lp.y, 0f);
        return true;
    }

    /// <summary>躯干主精灵（怪物优先 Monsters 子节点）。</summary>
    protected virtual bool TryGetPrimaryBodySprite(out SpriteRenderer bodySr)
    {
        bodySr = null;
        Transform monsters = transform.Find("Monsters");
        if (monsters != null)
        {
            bodySr = monsters.GetComponent<SpriteRenderer>();
            if (bodySr != null && bodySr.sprite != null) return true;
        }
        SpriteRenderer[] all = GetComponentsInChildren<SpriteRenderer>(true);
        if (all == null) return false;
        for (int i = 0; i < all.Length; i++)
        {
            var r = all[i];
            if (r == null || r.sprite == null || !r.enabled) continue;
            if (IsIgnoredHitPointRenderer(r)) continue;
            bodySr = r;
            return true;
        }
        return false;
    }

    /// <summary>躯干中心（世界坐标）：包围盒中线偏下一点，避免武器/头顶透明把点抬高。</summary>
    protected Vector3 GetBodyCenterWorld(Bounds body)
    {
        float y = body.min.y + body.size.y * 0.42f;
        return new Vector3(body.center.x, y, transform.position.z);
    }

    /// <summary>合并本单位躯干精灵的世界包围盒（排除影子/血条/特效）。</summary>
    protected bool TryGetBodyBounds(out Bounds bounds)
    {
        bounds = new Bounds();
        SpriteRenderer[] allSrs = GetComponentsInChildren<SpriteRenderer>(true);
        if (allSrs == null || allSrs.Length == 0) return false;

        bool hasValid = false;
        foreach (var r in allSrs)
        {
            if (r == null || r.sprite == null || !r.enabled) continue;
            if (IsIgnoredHitPointRenderer(r)) continue;

            if (!hasValid)
            {
                bounds = r.bounds;
                hasValid = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }
        return hasValid && bounds.size.y > 0.0001f;
    }

    /// <summary>世界空间：血条宽度（默认；怪物按精灵 bounds 覆盖）。</summary>
    public virtual float GetHpBarWorldWidth()
    {
        return 0.55f;
    }

    /// <summary>世界空间：脚底到血条锚点 Y 偏移；挂 Body 原点时用 0。</summary>
    public virtual float GetHpBarWorldYOffset()
    {
        return 0f;
    }

    static bool IsIgnoredHitPointRenderer(SpriteRenderer r)
    {
        if (r == null) return true;
        Transform p = r.transform;
        while (p != null)
        {
            string n = p.name;
            if (!string.IsNullOrEmpty(n))
            {
                string low = n.ToLowerInvariant();
                if (low.Contains("shadow") || low.Contains("阴影") || low == "hpbar"
                    || low.Contains("bar_bg") || low.Contains("damage") || low.Contains("vfx")
                    || low.Contains("weapon") || low.Contains("武器") || low.Contains("bow")
                    || low.Contains("fire") || low.Contains("beattack") || low.Contains("hitpoint"))
                    return true;
            }
            p = p.parent;
        }
        return false;
    }

    /// <summary>
    /// 获取发射点世界坐标（考虑朝向翻转）
    /// </summary>
    public virtual Vector3 GetFirePosition()
    {
        if (firePoint != null)
        {
            Vector3 p = firePoint.position;
            // 出手高度跟躯干中心走，避免弹道从脚下飞出
            if (hitPoint != null) p.y = hitPoint.position.y;
            return p;
        }
        float y = hitPoint != null
            ? hitPoint.position.y
            : transform.position.y + firePointOffset.y;
        return new Vector3(transform.position.x + firePointOffset.x * facingDir, y, transform.position.z);
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
        // 开战传送演出 / 左屏外走进场：冻结 AI，走进场时仍播走路
        if (BattleManager.Instance != null && !BattleManager.Instance.UnitsCanAct)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            if (BattleManager.Instance.PartyIntroWalking && isAlly)
            {
                facingDir = 1;
                ApplyFacing(facingDir);
                if (unitAnim != null) unitAnim.SetMove(true, facingDir);
            }
            else if (unitAnim != null)
            {
                unitAnim.SetMove(false, facingDir);
            }
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
            FaceToward(target);
            AdjustLaneTowardTarget(target, Time.deltaTime);

            if (distance <= attackRange)
            {
                // 攻击范围内停步输出，避免贴到目标中心导致双方朝向来回抖
                if (rb != null) rb.velocity = Vector2.zero;
                if (attackCd <= 0 && (unitAnim == null || !unitAnim.InDamagedRecovery()))
                {
                    Attack(target);
                    attackCd = GetAttackCooldown();
                }
            }
            else
            {
                // 有索敌目标但未进射程：持续前压（不因前方友军挡路而停步）
                if (rb != null)
                    rb.velocity = new Vector2(facingDir * attr.GetAttr(AttrType.MoveSpeed), rb.velocity.y);
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
                AdjustFormationLane(Time.deltaTime);
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
        ApplyLaneY(Time.deltaTime);
    }

    /// <summary>对外改朝向（传送门、入队站位等）</summary>
    public void Face(int dir)
    {
        facingDir = dir == 0 ? 1 : (dir > 0 ? 1 : -1);
        ApplyFacing(facingDir);
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

    /// <summary>特效朝向：SPUM 用 scale.x；怪物 clip/flipX 用 flipX 判断。</summary>
    public virtual int GetVfxFacingDir()
    {
        if (unitAnim != null && unitAnim.UsesFlipXFacing && sr != null)
        {
            bool visualRight = spriteDefaultFacesRight ? !sr.flipX : sr.flipX;
            return visualRight ? 1 : -1;
        }
        bool proc = unitAnim != null && unitAnim.IsProceduralAnim;
        if (!proc)
        {
            float sx = transform.localScale.x;
            if (Mathf.Abs(sx) > 0.001f)
            {
                bool visualRight = spriteDefaultFacesRight ? sx >= 0f : sx < 0f;
                return visualRight ? 1 : -1;
            }
        }
        return facingDir >= 0 ? 1 : -1;
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

    /// <summary>在索敌范围内找最近敌人（索敌范围=屏幕宽+缓冲，与攻击射程无关）</summary>
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

        // 目标粘滞：新目标需明显更近才切换，避免贴身时左右抖
        if (target != null && !target.isDead && nearest != null && nearest != target)
        {
            float curDist = Mathf.Abs(myX - GetCombatX(target));
            float newDist = Mathf.Abs(myX - GetCombatX(nearest));
            const float switchMargin = 0.45f;
            if (curDist <= detectRange && newDist > curDist - switchMargin)
                nearest = target;
        }
        return nearest;
    }

    /// <summary>朝目标转身；贴身时保持当前朝向，避免左右来回闪。</summary>
    protected void FaceToward(UnitBase other)
    {
        if (other == null) return;
        float dx = GetCombatX(other) - GetCombatX(this);
        const float deadZone = 0.22f;
        if (Mathf.Abs(dx) < deadZone) return;
        int dir = dx > 0f ? 1 : -1;
        if (dir == facingDir) return;
        facingDir = dir;
        ApplyFacing(facingDir);
    }

    /// <summary>索敌范围：与攻击射程无关，见 GameConfig.GetCombatDetectRange。</summary>
    public virtual float GetDetectRange() => GameConfig.GetCombatDetectRange();

    /// <summary>索敌/攻击距离：以可见 transform.x 为准，并纠正偏离的 Rigidbody2D</summary>
    public static float GetCombatX(UnitBase u)
    {
        if (u == null) return 0f;
        Transform moveTf = u.transform;
        if (u is Monster m)
            moveTf = m.GetBodyTransform();
        float x = moveTf.position.x;
        if (u.rb != null && Mathf.Abs(u.rb.position.x - x) > 0.05f)
            u.rb.position = new Vector2(x, moveTf.position.y);
        return x;
    }

    protected virtual float GetFormationLaneOffset() => 0f;

    protected void AdjustLaneTowardTarget(UnitBase chaseTarget, float dt)
    {
        if (chaseTarget == null || attr == null) return;
        float laneSpeed = Mathf.Max(0.55f, attr.GetAttr(AttrType.MoveSpeed) * 0.85f);
        float targetLane = chaseTarget.GetWorldLaneOffset();
        SetLaneY(Mathf.MoveTowards(LaneY, targetLane, laneSpeed * dt));
    }

    protected void AdjustFormationLane(float dt)
    {
        float targetLane = GetFormationLaneOffset();
        if (Mathf.Abs(LaneY - targetLane) < 0.015f) return;
        SetLaneY(Mathf.MoveTowards(LaneY, targetLane, GameConfig.BATTLE_LANE_MOVE_SPEED * dt));
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
        bool allyMelee = isAlly && kit == AttackVfxKit.MeleeSlash;
        bool allyRanged = isAlly && (kit == AttackVfxKit.Bow || kit == AttackVfxKit.Orb);
        bool killWindup = isAlly && ShouldUseKillWindup(target, damage, isCrit, openingHit);
        if (unitAnim != null)
            unitAnim.PlayAttack(kit, allyMelee && (isCrit || killWindup));

        VfxFaction faction = isAlly ? VfxFaction.Ally : VfxFaction.Enemy;
        Vector3 firePos = GetFirePosition();
        Vector3 hitPos = target.GetHitPosition();
        Transform hitTf = target.transform;
        int facingDir = GetVfxFacingDir();

        // 普攻：近战即时结算；敌方弓/法球飞到再结算；我方近战下落时再结算
        if (!isAlly && (kit == AttackVfxKit.Bow || kit == AttackVfxKit.Orb) && BattleVFXSystem.Instance != null)
        {
            float pendingDamage = damage;
            bool pendingCrit = isCrit;
            bool pendingOpening = openingHit;
            BattleVFXSystem.Instance.PlaySkillProjectile(
                faction, firePos, hitPos, facingDir, hitTf, kit, null, 1.2f,
                GameConfig.MONSTER_BASIC_PROJECTILE_SPEED_MUL,
                () => ResolveBasicAttackHit(target, pendingDamage, pendingCrit, pendingOpening));
            return;
        }

        if (allyMelee)
        {
            StartCoroutine(CoAllyMeleeAttack(
                target, damage, isCrit, openingHit, kit, faction, firePos, hitPos, facingDir, hitTf, killWindup));
            return;
        }

        if (allyRanged && killWindup)
        {
            StartCoroutine(CoAllyRangedKillWindup(
                target, damage, isCrit, openingHit, kit, faction, firePos, hitPos, facingDir, hitTf));
            return;
        }

        ResolveBasicAttackHit(target, damage, isCrit, openingHit);
        if (kit == AttackVfxKit.MeleeSlash)
            CombatJuice.Instance?.OnMeleeAttackLunge(this);
        if (BattleVFXSystem.Instance != null)
            BattleVFXSystem.Instance.PlayAttackKit(kit, faction, firePos, hitPos, facingDir, hitTf, isCrit);
    }

    bool ShouldUseKillWindup(UnitBase target, float damage, bool isCrit, bool openingHit)
    {
        if (target == null || target.isDead || target.attr == null) return false;
        if (isCrit) return true;
        if (target is Monster m && (m.IsBossUnit || m.IsEliteWave))
            return target.currentHp <= PredictBasicAttackDamage(target, damage, openingHit);
        return false;
    }

    float PredictBasicAttackDamage(UnitBase target, float damage, bool openingHit)
    {
        float d = damage;
        if (this is Hero)
        {
            d *= SpecialWeapons.GetDamageMultiplier(target);
            float fire = SpecialWeapons.GetFlatFireBonus();
            if (fire > 0f && target != null && !target.isDead)
                d += DamageFormula.FinalHit(fire, target.attr, false);
        }
        return DamageFormula.FinalHit(d, target.attr, false);
    }

    IEnumerator CoAllyMeleeAttack(
        UnitBase target, float damage, bool isCrit, bool openingHit,
        AttackVfxKit kit, VfxFaction faction,
        Vector3 firePos, Vector3 hitPos, int facingDir, Transform hitTf, bool killWindup)
    {
        float delay;
        if (killWindup)
        {
            CombatJuice.Instance?.BeginKillWindupJuice(true);
            delay = GameConfig.CRIT_WINDUP_UNSCALED;
        }
        else
        {
            delay = unitAnim != null
                ? unitAnim.GetAllyMeleeHitDelay()
                : GameConfig.ALLY_MELEE_HIT_NORM * 0.5f;
        }

        yield return new WaitForSecondsRealtime(delay);

        if (killWindup)
            CombatJuice.Instance?.EndKillWindupJuice();

        if (this == null || isDead || target == null || target.isDead)
            yield break;

        ResolveBasicAttackHit(target, damage, isCrit, openingHit);
        if (BattleVFXSystem.Instance != null)
            BattleVFXSystem.Instance.PlayAttackKit(kit, faction, firePos, hitPos, facingDir, hitTf, isCrit);
    }

    IEnumerator CoAllyRangedKillWindup(
        UnitBase target, float damage, bool isCrit, bool openingHit,
        AttackVfxKit kit, VfxFaction faction,
        Vector3 firePos, Vector3 hitPos, int facingDir, Transform hitTf)
    {
        CombatJuice.Instance?.BeginKillWindupJuice(false);
        yield return new WaitForSecondsRealtime(GameConfig.KILL_CAM_RANGED_WINDUP);
        CombatJuice.Instance?.EndKillWindupJuice();

        if (this == null || isDead || target == null || target.isDead)
            yield break;

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
            DamageTextSystem.Instance?.SpawnDodgeText(target.GetHitPosition(), target.isAlly);
            CombatJuice.Instance?.OnDodge(target.isAlly);
            OnAttack?.Invoke(target, 0, false);
            return;
        }

        int vfxDir = GetVfxFacingDir();
        if (this is Hero)
        {
            damage *= SpecialWeapons.GetDamageMultiplier(target);
            float fire = SpecialWeapons.GetFlatFireBonus();
            if (fire > 0f && !target.isDead)
                target.TakeDamage(fire, false, openingHit, false, vfxDir, this);
        }

        target.TakeDamage(damage, isCrit, openingHit, true, vfxDir, this);
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

    float _lastHitVfxTime = -999f;
    const float HitVfxCooldown = 0.08f;
    float _lastKnockbackTime = -999f;
    Coroutine _knockbackCo;

    protected virtual Transform GetKnockbackRoot()
    {
        if (this is Monster m) return m.GetBodyTransform();
        return transform;
    }

    /// <summary>受击/出手短位移（不改 prefab scale，100ms 内节流）。我方根节点禁止位移。</summary>
    public void ApplyKnockback(float dx, float duration = 0.08f)
    {
        if (!GameConfig.COMBAT_JUICE_KNOCKBACK || _isDying || isDead || isAlly) return;
        if (Mathf.Abs(dx) < 0.001f) return;
        if (Time.time - _lastKnockbackTime < 0.1f) return;
        _lastKnockbackTime = Time.time;

        Transform root = GetKnockbackRoot();
        if (root == null) return;
        if (_knockbackCo != null)
            StopCoroutine(_knockbackCo);
        _knockbackCo = StartCoroutine(CoKnockback(root, dx, duration));
    }

    System.Collections.IEnumerator CoKnockback(Transform root, float dx, float duration)
    {
        Vector3 start = root.position;
        Vector3 peak = start + Vector3.right * dx;
        float half = duration * 0.45f;
        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            root.position = Vector3.Lerp(start, peak, half > 0.0001f ? t / half : 1f);
            SyncKnockbackRigidbody(root);
            yield return null;
        }
        float back = duration - half;
        t = 0f;
        while (t < back)
        {
            t += Time.deltaTime;
            root.position = Vector3.Lerp(peak, start, back > 0.0001f ? t / back : 1f);
            SyncKnockbackRigidbody(root);
            yield return null;
        }
        root.position = start;
        SyncKnockbackRigidbody(root);
        _knockbackCo = null;
    }

    static void SyncKnockbackRigidbody(Transform root)
    {
        if (root == null) return;
        var body = root.GetComponent<Rigidbody2D>();
        if (body == null) return;
        body.position = new Vector2(root.position.x, body.position.y);
        body.velocity = Vector2.zero;
    }

    public virtual void TakeDamage(float damage, bool isCrit, bool ignoreDefense = false, bool showHitVfx = true, int hitVfxFacing = 0, UnitBase source = null)
    {
        if (_isDying) return;

        float finalDamage = DamageFormula.FinalHit(damage, attr, ignoreDefense);

        if (source != null && finalDamage > 0f)
            LastDamageSource = source;

        currentHp -= finalDamage;
        // 怪物受击飘字：传受害者面向，由 DamageTextSystem 固定往其后方滑
        int textFacing = isAlly ? hitVfxFacing : GetVfxFacingDir();
        DamageTextSystem.Instance?.SpawnDamageText(GetHitPosition(), Mathf.RoundToInt(finalDamage), isCrit, isAlly, textFacing);

        if (showHitVfx && finalDamage > 0f && BattleVFXSystem.Instance != null
            && Time.time - _lastHitVfxTime >= HitVfxCooldown)
        {
            _lastHitVfxTime = Time.time;
            int dir = hitVfxFacing != 0 ? hitVfxFacing : -GetVfxFacingDir();
            BattleVFXSystem.Instance.PlayVictimHit(GetHitPosition(), isAlly, dir);
        }

        CombatJuice.Instance?.OnHit(this, finalDamage, isCrit, showHitVfx);

        var bm = BattleManager.Instance;
        if (bm != null && finalDamage > 0f)
        {
            if (this is Monster mon)
            {
                bm.RecordDamageDealt(finalDamage, mon.IsBossUnit);
                if (isCrit) bm.RecordCrit();
                if (source != null && source.isAlly)
                    bm.RecordAllyDamage(source, finalDamage);
            }
            else
                bm.RecordDamageTaken(finalDamage);
        }

        if (unitAnim != null)
            unitAnim.PlayDamaged();

        if (currentHp <= 0)
            Die(isCrit && finalDamage > 0f);
    }

    protected virtual void Die(bool isCritKill = false)
    {
        if (_isDying) return;
        _isDying = true;

        // 停止移动
        if (rb != null) rb.velocity = Vector2.zero;

        // 立即触发死亡事件（给奖励、UI更新等游戏逻辑）
        OnDead?.Invoke(this);

        // 播放死亡动画
        if (unitAnim != null)
            unitAnim.PlayDeath(GetVfxFacingDir(), isCritKill);

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