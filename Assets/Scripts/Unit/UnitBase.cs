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

    /// <summary>当前生效的车道对齐容错偏移（Y，世界单位）。未启用则为 0。
    /// 追击方要扣掉目标身上这份偏移，否则「我追你+偏移、你追我」会两边一起漂到车道边界。</summary>
    public float LaneAlignBiasY { get; private set; }
    /// <summary>当前生效的左右容错（世界单位，恒 ≥0：表示站位比射程再近多少）。</summary>
    public float LaneAlignBiasX { get; private set; }
    /// <summary>是否启用车道对齐容错（基类默认关；Hero 打开）。关=完全回退成严丝合缝对齐。</summary>
    protected virtual bool UseLaneAlignTolerance => false;
    /// <summary>当前这份偏移是为哪个目标抽的（换目标才重抽，绝不每帧重抽）。</summary>
    UnitBase _laneBiasOwner;

    /// <summary>清空容错偏移（换局/复位）。</summary>
    protected void ClearLaneAlignBias()
    {
        _laneBiasOwner = null;
        LaneAlignBiasY = 0f;
        LaneAlignBiasX = 0f;
    }

    /// <summary>换目标或首次进战斗时抽一次偏移方向（上下 ± 与一点点左右），之后固定不变，避免每帧抖动。</summary>
    void RollLaneAlignBias(UnitBase chaseTarget)
    {
        _laneBiasOwner = chaseTarget;
        if (!GameConfig.ENABLE_LANE_ALIGN_TOLERANCE)
        {
            LaneAlignBiasY = 0f;
            LaneAlignBiasX = 0f;
            return;
        }
        float amp = GameConfig.LANE_ALIGN_TOLERANCE
            * Random.Range(Mathf.Clamp01(GameConfig.LANE_ALIGN_TOLERANCE_MIN_RATIO), 1f);
        LaneAlignBiasY = Random.value < 0.5f ? -amp : amp;
        LaneAlignBiasX = Mathf.Max(0f, Random.Range(0f, GameConfig.LANE_ALIGN_TOLERANCE_X));
    }

    public void SetLaneY(float offset)
    {
        LaneY = BattleLaneBounds.ClampLaneOffset(offset);
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
        return BattleLaneBounds.ClampLaneOffset(moveTf.position.y - GROUND_Y);
    }

    protected virtual Transform LaneMoveTransform => transform;

    protected void ApplyLaneY(float dt)
    {
        var t = LaneMoveTransform;
        if (t == null) return;
        var p = t.position;
        float target = FootY;
        if (Mathf.Abs(p.y - target) < 0.002f)
        {
            RefreshDepthSort();
            return;
        }
        p.y = Mathf.MoveTowards(p.y, target, GameConfig.BATTLE_LANE_MOVE_SPEED * dt);
        // SetWorldPosition 会清 velocity；保留水平速度，避免与追敌/手动位移打架
        float keepVx = rb != null ? rb.velocity.x : 0f;
        GameConfig.SetWorldPosition(t, p);
        if (rb != null)
            rb.velocity = new Vector2(keepVx, 0f);
        RefreshDepthSort();
    }

    /// <summary>按脚底 Y 刷新前后遮挡（越靠下越前）。只动 SortingGroup，不改 SPUM 部件 order。</summary>
    protected void RefreshDepthSort()
    {
        var foot = LaneMoveTransform;
        if (foot == null) foot = transform;
        GameConfig.ApplyUnitSorting(transform, foot.position.y);
    }

    /// <summary>最近一次造成伤害的来源（结算 MVP 击杀归属）。</summary>
    public UnitBase LastDamageSource { get; private set; }

    public AttrSystem attr = new AttrSystem();

    void BindAttrOwnerKind()
    {
        if (attr == null) attr = new AttrSystem();
        if (this is Hero) attr.SetOwnerKind(AttrOwnerKind.Player);
        else if (this is Mercenary) attr.SetOwnerKind(AttrOwnerKind.Merc);
        else if (this is Monster) attr.SetOwnerKind(AttrOwnerKind.Monster);
    }
    public float currentHp;
    protected float attackCd = 0;
    protected UnitBase target;
    /// <summary>当前战斗目标（只读，供技能 VFX 等外部查询）。</summary>
    public UnitBase CurrentTarget => target;
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
        BindAttrOwnerKind();
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
        if (IsStunned)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            if (unitAnim != null) unitAnim.SetMove(false, facingDir);
            return;
        }
        AIUpdate();
    }

    float _stunUntil;
    /// <summary>眩晕（控制）：期间停 AI。</summary>
    public bool IsStunned => Time.time < _stunUntil;

    public void ApplyStun(float seconds)
    {
        if (seconds <= 0f || isDead || isAlly) return; // 本玩法只眩晕敌人
        _stunUntil = Mathf.Max(_stunUntil, Time.time + seconds);
        if (rb != null) rb.velocity = Vector2.zero;
        if (unitAnim != null)
        {
            unitAnim.SetMove(false, facingDir);
            unitAnim.PlayDebuff();
        }
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

        // 攻击动画中：锁目标、停步、不转身并道，避免中途换目标/滑步
        if (TryHoldDuringAttack())
            return;

        // 受击 recovery：停步，避免前压 + DAMAGED 造成滑步
        if (TryHoldDuringDamaged())
            return;

        bool alliesEngaged = isAlly && HasAliveEnemyOnField();
        target = alliesEngaged
            ? FindNearestEnemyOnField()
            : FindNearestEnemyInDetectRange();
        // 安全检查：绝不对同阵营单位出手
        if (target != null && target.isAlly == isAlly)
        {
            target = null;
        }
        bool isMoving = false;

        if (target != null)
        {
            float distance = Mathf.Abs(GetCombatX(this) - GetCombatX(target));
            float attackRange = GetEffectiveAttackRange();
            bool melee = UsesMeleeBasicAttack();
            FaceToward(target);
            // 近战始终并道到目标水平对面；远程交战锁 Y 不追道
            if (melee || !alliesEngaged)
                AdjustLaneTowardTarget(target, Time.deltaTime);

            if (IsInBasicAttackRange(target))
            {
                // 攻击范围内停步输出，避免贴到目标中心导致双方朝向来回抖
                if (rb != null) rb.velocity = Vector2.zero;
                if (attackCd <= 0 && (unitAnim == null || !unitAnim.InDamagedRecovery())
                    && (unitAnim == null || !unitAnim.InAttackLock))
                {
                    Attack(target);
                    attackCd = GetAttackCooldown();
                }
            }
            else if (melee && distance <= attackRange)
            {
                // X 已进距、车道未齐：停 X 只并道，不开砍
                if (rb != null) rb.velocity = Vector2.zero;
                isMoving = true;
            }
            else
            {
                // 有索敌目标但未进射程：持续前压走近再打
                if (rb != null)
                    rb.velocity = new Vector2(facingDir * GetCombatMoveSpeed(), rb.velocity.y);
                isMoving = true;
            }
        }
        else
        {
            if (isAlly)
            {
                if (alliesEngaged)
                {
                    // 有怪但索敌范围内无目标：停推图
                    facingDir = 1;
                    ApplyFacing(facingDir);
                    if (rb != null) rb.velocity = Vector2.zero;
                    isMoving = false;
                }
                else
                {
                    // 无怪：向右推图
                    facingDir = 1;
                    ApplyFacing(facingDir);
                    AdjustFormationLane(Time.deltaTime);
                    if (rb != null)
                        rb.velocity = new Vector2(GetCombatMoveSpeed(), rb.velocity.y);
                    isMoving = true;
                }
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

    /// <summary>
    /// 攻击动画锁定：保留当前目标，停步且不转身/并道。
    /// 目标死亡、失活或离开战斗镜头则不锁定，允许立刻重索敌。
    /// </summary>
    protected bool TryHoldDuringAttack()
    {
        if (unitAnim == null || !unitAnim.InAttackLock) return false;
        if (target == null || target.isDead || target.isAlly == isAlly
            || !target.gameObject.activeInHierarchy || !GameConfig.IsInCombatViewport(target))
            return false;

        if (rb != null) rb.velocity = Vector2.zero;
        if (unitAnim != null) unitAnim.SetMove(false, facingDir);
        if (isAlly && (BattleManager.Instance == null || !BattleManager.Instance.PortalWalkMode))
            ClampToScreen();
        ApplyLaneY(Time.deltaTime);
        return true;
    }

    /// <summary>受击硬直：清零速度并停移动动画，避免前进滑步。</summary>
    protected bool TryHoldDuringDamaged()
    {
        if (unitAnim == null || !unitAnim.InDamagedRecovery()) return false;

        if (rb != null) rb.velocity = Vector2.zero;
        unitAnim.SetMove(false, facingDir);
        if (isAlly && (BattleManager.Instance == null || !BattleManager.Instance.PortalWalkMode))
            ClampToScreen();
        ApplyLaneY(Time.deltaTime);
        return true;
    }

    protected static bool HasAliveEnemyOnField()
    {
        var bm = BattleManager.Instance;
        return bm != null && bm.GetAliveMonsterCount() > 0;
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

    /// <summary>
    /// 仅玩家跟镜头钳制屏幕。佣兵/怪物必须保留世界坐标：
    /// 若按相机左右缘夹单位，镜头跟随主角时会把整队/全场一起拖走。
    /// </summary>
    protected void ClampToScreen()
    {
        if (!(this is Hero)) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        float camX = cam.transform.position.x;
        float margin = 0.3f;
        float minX = camX - halfW + margin;
        float maxX = camX + halfW - margin;
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
        bool currentTargetCandidate = false;
        IEnumerable<UnitBase> enemyList = isAlly ? BattleManager.Instance.monsters : BattleManager.Instance.allyUnits;
        if (enemyList == null) return null;
        foreach (var enemy in enemyList)
        {
            if (enemy == null || enemy.isDead || !enemy.gameObject.activeInHierarchy) continue;
            if (isAlly && enemy.isAlly) continue;
            if (!isAlly && !enemy.isAlly) continue;
            if (!GameConfig.IsInCombatViewport(enemy)) continue;
            float dist = Mathf.Abs(myX - GetCombatX(enemy));
            if (dist > detectRange) continue;
            if (enemy == target) currentTargetCandidate = true;
            if (dist <= minDist)
            {
                minDist = dist;
                nearest = enemy;
            }
        }
        return ApplyNearestTargetStickiness(nearest, myX, currentTargetCandidate);
    }

    const float TargetSwitchMargin = 0.45f;

    /// <summary>
    /// 切换规则：当前目标死亡、销毁、失活、离开敌方列表/镜头/本次索敌范围时立即改选；
    /// 否则新目标至少近 0.45 才立即切换，距离差小于 0.45 时才保留当前目标防抖。
    /// </summary>
    UnitBase ApplyNearestTargetStickiness(UnitBase nearest, float myX, bool currentTargetCandidate)
    {
        if (!currentTargetCandidate || target == null || nearest == null || nearest == target)
            return nearest;

        float curDist = Mathf.Abs(myX - GetCombatX(target));
        float newDist = Mathf.Abs(myX - GetCombatX(nearest));
        return newDist <= curDist - TargetSwitchMargin ? nearest : target;
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

    /// <summary>场上最近敌（交战寻敌进距）。仍要求目标在镜头内，避免打屏外怪。</summary>
    public virtual UnitBase FindNearestEnemyOnField()
    {
        if (BattleManager.Instance == null) return null;
        UnitBase nearest = null;
        float minDist = float.MaxValue;
        float myX = GetCombatX(this);
        bool currentTargetCandidate = false;
        IEnumerable<UnitBase> enemyList = isAlly ? BattleManager.Instance.monsters : BattleManager.Instance.allyUnits;
        if (enemyList == null) return null;
        foreach (var enemy in enemyList)
        {
            if (enemy == null || enemy.isDead || !enemy.gameObject.activeInHierarchy) continue;
            if (isAlly && enemy.isAlly) continue;
            if (!isAlly && !enemy.isAlly) continue;
            if (!GameConfig.IsInCombatViewport(enemy)) continue;
            if (enemy == target) currentTargetCandidate = true;
            float dist = Mathf.Abs(myX - GetCombatX(enemy));
            if (dist < minDist)
            {
                minDist = dist;
                nearest = enemy;
            }
        }
        return ApplyNearestTargetStickiness(nearest, myX, currentTargetCandidate);
    }

    /// <summary>普攻有效射程；近战钳到不超过长柄，避免表配过大导致半屏开砍。</summary>
    public float GetEffectiveAttackRange()
    {
        float r = attr != null ? attr.GetAttr(AttrType.AttackRange) : GameConfig.RangeSword;
        if (UsesMeleeBasicAttack())
        {
            if (isAlly)
            {
                r = Mathf.Min(r, GameConfig.RangePolearm);
                // 我方近战再缩 10%，贴身手感
                r *= 0.9f;
            }
            else
            {
                // 敌方近战不超过单手剑×倍率，避免比玩家砍得更远
                r = Mathf.Min(r, AttackRangeTable.GetMonsterWorld(MonsterAttackStyle.Melee));
            }
        }
        return Mathf.Max(0.2f, r);
    }

    /// <summary>普攻是否近战刀光（弓/法球除外）。</summary>
    protected virtual bool UsesMeleeBasicAttack()
    {
        return GetAttackVfxKit() == AttackVfxKit.MeleeSlash;
    }

    const float BasicAttackRangeSlack = 0.12f;

    /// <summary>近战是否与目标差不多同一车道（水平对面，允许小误差）。</summary>
    public bool IsMeleeLaneAligned(UnitBase other)
    {
        if (other == null) return false;
        float dy = Mathf.Abs(GetWorldLaneOffset() - other.GetWorldLaneOffset());
        return dy <= GameConfig.MELEE_LANE_ALIGN_TOL;
    }

    public bool IsInBasicAttackRange(UnitBase other)
    {
        if (other == null) return false;
        float d = Mathf.Abs(GetCombatX(this) - GetCombatX(other));
        if (d > GetEffectiveAttackRange() + BasicAttackRangeSlack)
            return false;
        // 近战必须并到水平对面再砍；远程只看 X
        if (UsesMeleeBasicAttack() && !IsMeleeLaneAligned(other))
            return false;
        return true;
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

    /// <summary>仅播攻击动画（奥义演出用，不带弹道/刀光）。</summary>
    public void PlayAttackAnimOnly(AttackVfxKit kit, bool critAmp = false)
    {
        if (unitAnim != null)
            unitAnim.PlayAttack(kit, critAmp);
    }

    protected virtual float GetFormationLaneOffset() => 0f;

    protected void AdjustLaneTowardTarget(UnitBase chaseTarget, float dt)
    {
        if (chaseTarget == null || attr == null) return;
        // 换道速度：必须恒 < 直行移速，否则「换道比直行快」会看着怪异。
        // 移速减半后（怪物≈0.3456）0.3456×0.85=0.294 < 0.55，原硬下限 0.55 会把换道钳得比直行还快，
        // 故去掉绝对硬下限，改用 MoveSpeed 的比例（0.35 为防极端慢速的兜底，恒 < 0.85）。
        float moveSpd = attr.GetAttr(AttrType.MoveSpeed);
        float laneSpeed = Mathf.Max(moveSpd * 0.35f, moveSpd * 0.85f);
        // 目标自身的容错偏移要扣掉：否则「我追你+偏移、你追我」双方会一起漂到车道边界。
        float targetLane = chaseTarget.GetWorldLaneOffset() - chaseTarget.LaneAlignBiasY;
        if (UseLaneAlignTolerance)
        {
            // 只在换目标（或首次进战斗）时重抽一次，之后沿用，绝不每帧重抽
            if (_laneBiasOwner != chaseTarget)
                RollLaneAlignBias(chaseTarget);

            float wanted = targetLane + LaneAlignBiasY;
            float clamped = BattleLaneBounds.ClampLaneOffset(wanted);
            // 贴车道边界时偏移会被钳没（又变回严丝合缝对齐），这时翻向另一侧
            if (Mathf.Abs(clamped - targetLane) < 0.02f)
            {
                LaneAlignBiasY = -LaneAlignBiasY;
                clamped = BattleLaneBounds.ClampLaneOffset(targetLane + LaneAlignBiasY);
            }
            targetLane = clamped;
        }
        SetLaneY(Mathf.MoveTowards(LaneY, targetLane, laneSpeed * GameConfig.LANE_ALIGN_SPEED_MUL * dt));
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
        // 进距才打：防配置过大射程 / AI 漏判导致半屏开砍
        if (!IsInBasicAttackRange(target))
            return;
        // 屏外目标：不进入攻击（索敌已过滤；此处防 OnField/技能漏网）
        if (!GameConfig.IsInCombatViewport(target))
            return;

        // 2026-09-26：魔法伤害单位（法师/牧师、法球怪）用魔法攻击力起手
        float damage = DamageFormula.BuildAttackRaw(attr, out bool isCrit, GetLowHpCritRateBonus(), IsMagicDamageDealer());
        // 友方「降低目标攻击」类减益（如 SK018 威慑凝视 -20%）：数值已在 MercPassiveRunner 里按表实现，
        // 之前没有任何调用点，等于白放。这里在普攻出伤害前消费一次。
        damage = ApplyAllyAttackDebuffs(damage);

        // 引导关/开局也走正式 ATK，不再使用 2~5 点假伤害压制。
        bool openingHit = false;

        AttackVfxKit kit = GetAttackVfxKit();
        bool allyMelee = isAlly && kit == AttackVfxKit.MeleeSlash;
        bool allyRanged = isAlly && (kit == AttackVfxKit.Bow || kit == AttackVfxKit.Orb);
        bool killWindup = isAlly && ShouldUseKillWindup(target, damage, isCrit, openingHit);
        float atkCd = GetAttackCooldown();
        if (unitAnim != null)
            unitAnim.PlayAttack(kit, allyMelee && (isCrit || killWindup), atkCd);
        CombatJuice.Instance?.PlaySwingSfx();

        VfxFaction faction = isAlly ? VfxFaction.Ally : VfxFaction.Enemy;
        Vector3 firePos = GetFirePosition();
        Vector3 hitPos = target.GetHitPosition();
        Transform hitTf = target.transform;
        int facingDir = GetVfxFacingDir();

        // 远程前摇：游侠（P004）用更大的 0.35，其他远程统一 0.2
        float releaseDelay = SkillNaming.IsRangedKit(kit)
            ? (this is Hero && PlayerJobDefs.GetSelected() == PlayerJobId.Ranger
                ? GameConfig.RANGED_FIRE_RELEASE_DELAY_RANGER
                : GameConfig.RANGED_FIRE_RELEASE_DELAY)
            : 0f;

        // 普攻：近战即时/下落时结算；弓/法球（敌我）FirePoint→HitPoint 飞到再结算
        if (!isAlly && SkillNaming.IsRangedKit(kit) && BattleVFXSystem.Instance != null)
        {
            StartCoroutine(CoRangedBasicProjectile(
                target, damage, isCrit, openingHit, kit, faction, facingDir,
                releaseDelay,
                speedMul: GameConfig.MONSTER_BASIC_PROJECTILE_SPEED_MUL, scaleMul: 1.2f, dodgeOnMiss: true));
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

        if (allyRanged)
        {
            FireAllyRangedBasicProjectile(
                target, damage, isCrit, openingHit, kit, faction, facingDir,
                releaseDelay);
            return;
        }

        ResolveBasicAttackHit(target, damage, isCrit, openingHit);
        if (kit == AttackVfxKit.MeleeSlash)
            CombatJuice.Instance?.OnMeleeAttackLunge(this);
        if (BattleVFXSystem.Instance != null)
            BattleVFXSystem.Instance.PlayAttackKit(kit, faction, firePos, hitPos, facingDir, hitTf, isCrit);
    }

    /// <summary>我方弓/法球普攻：点到点飞行，落地再结算（与敌方远程一致）。远程额外等出手延迟。</summary>
    void FireAllyRangedBasicProjectile(
        UnitBase target, float damage, bool isCrit, bool openingHit,
        AttackVfxKit kit, VfxFaction faction,
        int facingDir, float releaseDelay = 0f)
    {
        StartCoroutine(CoRangedBasicProjectile(
            target, damage, isCrit, openingHit, kit, faction, facingDir, releaseDelay,
            speedMul: 1f, scaleMul: 1f, dodgeOnMiss: false));
    }

    /// <summary>
    /// 远程普攻：可选出手延迟后再从当前 FirePoint 出弹。
    /// 延迟期间重采样发射/受击点，对齐释放帧而不是举弓/抬杖第一帧。
    /// </summary>
    IEnumerator CoRangedBasicProjectile(
        UnitBase target, float damage, bool isCrit, bool openingHit,
        AttackVfxKit kit, VfxFaction faction, int facingDir, float releaseDelay,
        float speedMul = 1f, float scaleMul = 1.2f, bool dodgeOnMiss = true)
    {
        if (releaseDelay > 0.001f)
            yield return new WaitForSeconds(releaseDelay);

        if (this == null || isDead || target == null || target.isDead)
            yield break;

        Vector3 firePos = GetFirePosition();
        Vector3 hitPos = target.GetHitPosition();
        Transform hitTf = target.transform;
        float pendingDamage = damage;
        bool pendingCrit = isCrit;
        bool pendingOpening = openingHit;
        Vector3 impactPos = hitPos;
        UnitBase pendingTarget = target;
        bool checkMiss = dodgeOnMiss && !isAlly;

        if (BattleVFXSystem.Instance != null)
        {
            BattleVFXSystem.Instance.PlaySkillProjectile(
                faction, firePos, hitPos, facingDir, hitTf, kit, null, scaleMul, speedMul,
                () =>
                {
                    if (pendingTarget == null || pendingTarget.isDead) return;
                    if (checkMiss)
                    {
                        Vector3 cur = pendingTarget.GetHitPosition();
                        float missDist = Vector2.Distance(
                            new Vector2(impactPos.x, impactPos.y),
                            new Vector2(cur.x, cur.y));
                        if (missDist > GameConfig.PROJECTILE_IMPACT_MISS_DIST)
                            return;
                    }
                    ResolveBasicAttackHit(pendingTarget, pendingDamage, pendingCrit, pendingOpening);
                });
        }
        else
            ResolveBasicAttackHit(target, damage, isCrit, openingHit);
    }

    /// <summary>友方施加在「我」身上的降攻减益合计倍率（MercPassiveRunner.ModifyTargetAttack 的消费点）。</summary>
    float ApplyAllyAttackDebuffs(float damage)
    {
        var mercs = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetActiveMercs() : null;
        if (mercs == null || mercs.Count <= 0) return damage;
        for (int i = 0; i < mercs.Count; i++)
        {
            var m = mercs[i];
            if (m == null || m.isDead || m.PassiveRunner == null) continue;
            damage = m.PassiveRunner.ModifyTargetAttack(this, damage);
        }
        return damage;
    }

    bool ShouldUseKillWindup(UnitBase target, float damage, bool isCrit, bool openingHit)
    {
        if (target == null || target.isDead || target.attr == null) return false;
        // Boss/精英：仅预测致死的最后一击才慢放+放大（平时暴击不触发）
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
            CombatJuice.Instance?.BeginKillWindupJuice(true, this, target);
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
        {
            if (killWindup)
                CombatJuice.Instance?.RevealKillCamBars();
            yield break;
        }

        ResolveBasicAttackHit(target, damage, isCrit, openingHit);
        if (BattleVFXSystem.Instance != null)
            BattleVFXSystem.Instance.PlayAttackKit(kit, faction, firePos, hitPos, facingDir, hitTf, isCrit);
        if (killWindup)
            CombatJuice.Instance?.RevealKillCamBars();
    }

    IEnumerator CoAllyRangedKillWindup(
        UnitBase target, float damage, bool isCrit, bool openingHit,
        AttackVfxKit kit, VfxFaction faction,
        Vector3 firePos, Vector3 hitPos, int facingDir, Transform hitTf)
    {
        CombatJuice.Instance?.BeginKillWindupJuice(false, this);
        yield return new WaitForSecondsRealtime(GameConfig.KILL_CAM_RANGED_WINDUP);
        CombatJuice.Instance?.EndKillWindupJuice();

        if (this == null || isDead || target == null || target.isDead)
        {
            CombatJuice.Instance?.RevealKillCamBars();
            yield break;
        }

        // 击杀前摇已等过；此处不再叠放箭延迟，立刻出弹
        FireAllyRangedBasicProjectile(
            target, damage, isCrit, openingHit, kit, faction, facingDir, 0f);
        CombatJuice.Instance?.RevealKillCamBars();
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

    /// <summary>对外读取普攻套（佣兵技能 VFX 需按施法者弓/法球回退）。</summary>
    public AttackVfxKit GetBasicAttackVfxKit() => GetAttackVfxKit();

    /// <summary>
    /// 攻击间隔（秒）= 1 / AttackSpeed。
    /// 玩家职业表 AttackInterval 单位就是秒（P004 游侠 0.5 = 半秒一刀）。
    /// 仅敌方弓/法球再乘 PROJECTILE_ATK_SPEED_MUL；我方不叠，否则表上的 0.5s 会变成 1s。
    /// 实际出手还受 UnitAnimation 攻击锁限制（PlayAttack 会把锁钳到不超过本冷却）。
    /// 2026-09-20：改为 virtual —— Monster 需要覆写它实现 Boss 狂暴（狂暴时把冷却 ÷倍率）。
    /// 其它子类行为完全不变（它们不覆写）。
    /// </summary>
    protected virtual float GetAttackCooldown()
    {
        float atkSpd = Mathf.Max(0.05f, attr.GetAttr(AttrType.AttackSpeed));
        AttackVfxKit kit = GetAttackVfxKit();
        if (!isAlly && SkillNaming.IsRangedKit(kit))
            atkSpd *= GameConfig.PROJECTILE_ATK_SPEED_MUL;
        // 开局不再给己方攻速 0.55 惩罚，与正式战斗一致。
        if (isAlly && BattleManager.Instance != null)
            atkSpd *= BattleManager.Instance.KillComboSpeedMul;
        // 攻速增益技能（gale_stance）的计时倍率，过期自动回到 1
        if (isAlly)
            atkSpd *= SkillCastService.GetTeamAttackSpeedMul();
        if (this is Hero && PlayerPassiveCombat.Instance != null)
            atkSpd *= PlayerPassiveCombat.Instance.GetAttackSpeedMul();
        // 玩家攻速 -20%（仅 Hero；佣兵/怪物节奏不变）
        if (this is Hero)
            atkSpd *= GameConfig.PLAYER_ATTACK_SPEED_MUL;
        // 残血加成（R1）：低血时提升攻速，越打越快制造反扑手感
        atkSpd *= GetLowHpAttackSpeedMul();
        // 游侠攻速减速（仅 Hero 且职业为游侠 P004）：额外乘 0.85，让游侠出手更慢、与前摇统一手感
        if (this is Hero && PlayerJobDefs.GetSelected() == PlayerJobId.Ranger)
            atkSpd *= 0.85f;
        return 1f / Mathf.Max(0.05f, atkSpd);
    }

    /// <summary>残血加成（R1）攻速倍率：默认 1（无加成）。仅 Hero 覆写。</summary>
    protected virtual float GetLowHpAttackSpeedMul() => 1f;

    /// <summary>残血加成（R1）普攻额外暴击率（绝对值）。默认 0。仅 Hero 覆写。</summary>
    protected virtual float GetLowHpCritRateBonus() => 0f;

    /// <summary>友军连杀加速后的移速。</summary>
    protected float GetCombatMoveSpeed()
    {
        float spd = attr != null ? attr.GetAttr(AttrType.MoveSpeed) : 1f;
        if (isAlly && BattleManager.Instance != null)
            spd *= BattleManager.Instance.KillComboSpeedMul;
        return spd;
    }

    /// <summary>
    /// 获取单位的攻击类型，子类可重写
    /// </summary>
    protected virtual WeaponAttackType GetAttackType()
    {
        return WeaponAttackType.Physical;
    }

    /// <summary>
    /// 是否「魔法伤害单位」（主人 2026-09-26 口径）：本单位打出的伤害算魔法伤害，走目标的魔法防御。
    /// 法师 / 牧师 = 魔法；其余职业 = 物理。默认按武器攻击类型判（佣兵法师/牧师已标 Magic），
    /// Hero 按所选职业判（<see cref="Hero"/>），怪物按攻击风格判（<see cref="Monster"/>）。
    /// </summary>
    public virtual bool IsMagicDamageDealer() => GetAttackType() == WeaponAttackType.Magic;

    /// <summary>
    /// 火/冰附加伤害：2026-09-26 主人拍板「冰火附加按百分比」。
    /// 口径：附加伤害 = 最终伤害 × 词缀值 × GameConfig.FIRE/ICE_BONUS_PER_POINT（默认 0.01f = 1%/点，词缀值 5 → +5%）。
    /// 飘字颜色保持：火=橙、冰=蓝；加在**扣防之后**的最终伤害上（不被防御吃掉，穿了必看得见）。
    /// 调数值只改 GameConfig.FIRE/ICE_BONUS_PER_POINT。
    /// 返回本次伤害该用的飘字元素（火=橙 / 冰=蓝 / 都没有 = None）。
    /// </summary>
    static DamageTextSystem.DamageElement ResolveElementalBonus(UnitBase source, ref float finalDamage)
    {
        if (source == null || source.attr == null || finalDamage <= 0f)
            return DamageTextSystem.DamageElement.None;

        float fire = source.attr.GetAttr(AttrType.FireDamage);
        float ice = source.attr.GetAttr(AttrType.IceDamage);
        float bonus = 0f;
        DamageTextSystem.DamageElement element = DamageTextSystem.DamageElement.None;
        // 同时有火有冰时取数值大的那一系（相等取火）
        if (ice > fire)
        {
            if (ice > 0f) { bonus = finalDamage * ice * GameConfig.ICE_BONUS_PER_POINT; element = DamageTextSystem.DamageElement.Ice; }
        }
        else if (fire > 0f)
        {
            bonus = finalDamage * fire * GameConfig.FIRE_BONUS_PER_POINT; element = DamageTextSystem.DamageElement.Fire;
        }
        if (bonus > 0f) finalDamage += bonus;
        return element;
    }

    /// <summary>
    /// 吸血：造成伤害的一方按「GameConfig.LIFESTEAL_RATIO(10%) + 装备词缀吸血比例」回血。
    /// 只在我方（Hero / 佣兵）身上生效——装备词缀是玩家侧的东西；怪物侧回血仍走 V6 词缀「吸血」。
    /// </summary>
    static void ApplyLifesteal(UnitBase dealer, float finalDamage)
    {
        if (dealer == null || dealer.isDead || dealer.attr == null || finalDamage <= 0f) return;
        float ratio = GameConfig.LIFESTEAL_RATIO + Mathf.Max(0f, dealer.attr.GetAttr(AttrType.LifeSteal));
        if (ratio <= 0f) return;

        float maxHp = Mathf.Max(1f, dealer.attr.GetAttr(AttrType.MaxHp));
        float before = dealer.currentHp;
        dealer.currentHp = Mathf.Min(maxHp, before + finalDamage * ratio);
        float healed = dealer.currentHp - before;
        if (healed >= 1f)
            DamageTextSystem.Instance?.SpawnHealText(dealer.GetHitPosition(), Mathf.RoundToInt(healed));
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

    /// <summary>子类可重写：当前是否处于无敌（如 Hero 闪避无敌帧）。默认 false。</summary>
    protected virtual bool IsInvincibleNow() => false;

    public virtual void TakeDamage(float damage, bool isCrit, bool ignoreDefense = false, bool showHitVfx = true, int hitVfxFacing = 0, UnitBase source = null)
    {
        if (_isDying) return;
        // 闪避无敌帧：仅免疫敌方伤害，关卡机制（source 为 null 或友方）仍生效
        if (IsInvincibleNow() && source != null && !source.isAlly)
            return;

        // 2026-09-26 主人口径：先归类本次伤害 —— 法师/牧师（法球怪）算魔法，走魔法防御
        bool magicHit = source != null && source.IsMagicDamageDealer();

        // SK017「魔法伤害 +15%」：佣兵被动 MercPassiveRunner.OnDealMagicDamage 的消费点
        // （之前没有任何调用点，等于白放）。只吃魔法伤害那一档。
        if (magicHit && source is Mercenary hitMerc && hitMerc.PassiveRunner != null)
            hitMerc.PassiveRunner.OnDealMagicDamage(ref damage);

        float finalDamage = DamageFormula.FinalHit(damage, attr, ignoreDefense, magicHit);
        if (isAlly && PlayerPassiveCombat.Instance != null)
            finalDamage *= PlayerPassiveCombat.Instance.GetAllyIncomingDamageMul(this);
        // 佣兵「圣光庇护」类团队护盾落在玩家身上的那层：先吸收再扣血（数值取自 merc_skills 表）
        if (this is Hero && PlayerPassiveCombat.Instance != null)
            finalDamage = PlayerPassiveCombat.Instance.AbsorbTeamShield(finalDamage);

        // 火/冰附加伤害（装备词缀 FireDamage / IceDamage）：2026-09-26 改百分比 = 最终伤害 × 词缀值 × PER_POINT，
        // 加在扣防之后的最终伤害上，保证穿了就能在飘字上看见（详见 ResolveElementalBonus 注释）。
        DamageTextSystem.DamageElement element = ResolveElementalBonus(source, ref finalDamage);

        if (source != null && finalDamage > 0f)
            LastDamageSource = source;

        currentHp -= finalDamage;

        // V6 词缀「吸血」：造成伤害的一方（精英/Boss）按比回血
        if (source is Monster && finalDamage > 0f)
        {
            var srcAffix = MonsterAffix.Get((Monster)source);
            if (srcAffix != null) srcAffix.OnDealtDamage(finalDamage);
        }

        // 吸血（主人 2026-09-26）：造成伤害的一方（我方）按 10%（+ 装备词缀「吸血」）回血
        if (source != null && source.isAlly && finalDamage > 0f)
            ApplyLifesteal(source, finalDamage);

        // 怪物受击飘字：传受害者面向，由 DamageTextSystem 固定往其后方滑
        int textFacing = isAlly ? hitVfxFacing : GetVfxFacingDir();
        DamageTextSystem.Instance?.SpawnDamageText(GetHitPosition(), Mathf.RoundToInt(finalDamage), isCrit, isAlly, textFacing, element);

        if (showHitVfx && finalDamage > 0f && BattleVFXSystem.Instance != null
            && Time.time - _lastHitVfxTime >= HitVfxCooldown)
        {
            AttackVfxKit srcKit = source != null ? source.GetBasicAttackVfxKit() : AttackVfxKit.MeleeSlash;
            // 远程弹道落地已播 Bow/Orb hit；再套刀光会让法师/弓手看起来在砍
            if (!SkillNaming.IsRangedKit(srcKit))
            {
                _lastHitVfxTime = Time.time;
                int dir = hitVfxFacing != 0 ? hitVfxFacing : -GetVfxFacingDir();
                BattleVFXSystem.Instance.PlayVictimHit(GetHitPosition(), isAlly, dir);
            }
        }

        CombatJuice.Instance?.OnHit(this, finalDamage, isCrit, showHitVfx);

        if (isCrit && source is Hero && !isAlly)
            PlayerPassiveCombat.Instance?.OnHeroCritHit(this);

        var bm = BattleManager.Instance;
        if (bm != null && finalDamage > 0f)
        {
            if (this is Monster mon)
            {
                bm.RecordDamageDealt(finalDamage, mon.IsBossUnit);
                if (isCrit) bm.RecordCrit();
                if (source != null && source.isAlly)
                {
                    bm.RecordAllyDamage(source, finalDamage);
                    // 技能能量不再靠出手/时间，只按下方法在受击时按伤害占比回充
                }
            }
            else
            {
                bm.RecordDamageTaken(finalDamage);
                if (isAlly)
                {
                    float maxHp = attr != null ? Mathf.Max(1f, attr.GetAttr(AttrType.MaxHp)) : 1f;
                    bm.AddCombatSkillEnergy(this, finalDamage / maxHp);
                }
            }
        }

        if (unitAnim != null)
            PlayHitReaction();

        if (currentHp <= 0)
            Die(isCrit && finalDamage > 0f);
    }

    /// <summary>受击表现：默认播受击动画+闪白；玩家/佣兵可覆盖。</summary>
    protected virtual void PlayHitReaction()
    {
        if (unitAnim != null)
            unitAnim.PlayDamaged(playHitAnim: true);
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