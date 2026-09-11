using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 主英雄类
/// </summary>
public class Hero : UnitBase
{
    public static Hero Instance;
    public Transform endPoint;
    public int level = 1;
    public int currentExp = 0;
    public int expToNextLevel = 50;

    [Header("SPUM换装")]
    public HeroCostumeManager costumeManager;

    protected override void Awake()
    {
        // 【关键】必须在base.Awake()之前设置，因为base.Awake()中会调用EnsureHitPoint/EnsureFirePoint
        firePointOffset = new Vector3(0.3f, 0.32f, 0f);
        hitPointOffset = new Vector3(0f, 0.55f, 0f);

        base.Awake();
        Instance = this;
        isAlly = true;
        // SPUM 资源在 +scale.x 下实际朝左，故标记为 false，facingDir=1 时会镜像成朝右
        spriteDefaultFacesRight = false;
        // 不改 SPUM 人物 Sorting，保留预制体层级；换装走 SPUM 规范

        if (costumeManager == null)
            costumeManager = GetComponent<HeroCostumeManager>();
        KillComboAfterimage.Ensure(this);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>玩家受击：只闪白，不播 DAMAGED 动画。</summary>
    protected override void PlayHitReaction()
    {
        if (unitAnim != null)
            unitAnim.PlayDamaged(playHitAnim: false);
    }

    public void InitNewRun()
    {
        if (GridBackpackSystem.Instance != null)
            GridBackpackSystem.Instance.InitNewRun();
        else
            Debug.LogWarning("[Hero] InitNewRun: GridBackpackSystem 为空，跳过背包重置");

        attr.ResetToBase();
        attr.RecalcAllAttr(); // 现在可以安全调用（Awake已完成）
        level = 1;
        currentExp = 0;
        expToNextLevel = LevelSystem.GetExpForLevel(level);
        currentHp = attr.GetAttr(AttrType.MaxHp);
        // 优先用场景 SpawnPoint；无则回退硬编码 X + GROUND_Y
        Vector3 spawnPos = new Vector3(-7f, GROUND_Y, 0f);
        if (BattleManager.Instance != null)
        {
            float z = BattleManager.Instance.unitRoot != null
                ? BattleManager.Instance.unitRoot.position.z
                : 0f;
            if (BattleManager.Instance.spawnPoint != null)
            {
                var sp = BattleManager.Instance.spawnPoint.position;
                spawnPos = new Vector3(sp.x, GROUND_Y, z);
            }
            else
            {
                spawnPos = new Vector3(spawnPos.x, GROUND_Y, z);
            }
        }
        GameConfig.SetWorldPosition(gameObject, spawnPos);
        GameConfig.AttachToUnitRoot(transform);
        transform.localScale = Vector3.one * GameConfig.UNIT_SCALE;
        facingDir = 1;
        ApplyFacing(facingDir); // 默认资源朝左 → 朝右时 scale.x 为负
        gameObject.SetActive(true);

        // 重置死亡状态和动画
        ResetForReuse();

        // 初始化换装后再按武器刷新攻击距离
        if (costumeManager != null)
            costumeManager.RefreshCostume();
        RecalcAttr();

        Debug.Log($"[Hero] InitNewRun完成 | pos={transform.position} | scale={transform.localScale} | facingDir={facingDir} | range={attr.GetAttr(AttrType.AttackRange)}");
    }

    public void AddExp(int exp)
    {
        currentExp += exp;
        while (currentExp >= expToNextLevel)
        {
            currentExp -= expToNextLevel;
            LevelSystem.OnLevelUp(this);
            expToNextLevel = LevelSystem.GetExpForLevel(level);
        }
    }

    public void RecalcAttr()
    {
        var bag = GridBackpackSystem.Instance;
        if (bag == null)
        {
            Debug.LogWarning("[Hero] RecalcAttr: GridBackpackSystem 为空，仅重算基础属性");
            attr.RecalcAllAttr(null);
            currentHp = Mathf.Min(currentHp, attr.GetAttr(AttrType.MaxHp));
            return;
        }

        List<AttrBonusData> allBonus = EquipStatRollup.BuildBonusList(bag);
        if (BattleManager.Instance != null && BattleManager.Instance.tempBuffs != null)
            allBonus.AddRange(BattleManager.Instance.tempBuffs);
        attr.RecalcAllAttr(allBonus);

        // 主手优先，无主手则读副手（教程默认左手剑）
        float weaponRange = AttackRangeTable.GetJobWorld(PlayerJobDefs.GetSelected());
        EquipInstance weaponInst = TryGetEquippedWeaponInstance(bag);
        if (weaponInst?.template != null)
            weaponRange = WeaponCombatTable.GetAttackRangeWorld(WeaponCombatTable.ResolveKind(weaponInst));
        attr.SetAttr(AttrType.AttackRange, weaponRange);
        if (weaponInst?.template != null)
        {
            float swordSpd = WeaponCombatTable.GetBaseAttackSpeed(WeaponCombatTable.WeaponKind.Sword);
            float kindSpd = WeaponCombatTable.GetBaseAttackSpeed(WeaponCombatTable.ResolveKind(weaponInst));
            float mul = swordSpd > 0.01f ? kindSpd / swordSpd : 1f;
            attr.SetAttr(AttrType.AttackSpeed, Mathf.Max(0.2f, attr.GetAttr(AttrType.AttackSpeed) * mul));
        }
        currentHp = Mathf.Min(currentHp, attr.GetAttr(AttrType.MaxHp));

        // 属性重算后同步外观（通关穿装 / 战前遗产等路径未必都走 EquipItem）
        if (costumeManager == null)
            costumeManager = GetComponent<HeroCostumeManager>();
        costumeManager?.RefreshCostume();
    }

    protected override void Update()
    {
        base.Update();
        TickLowHpWarn();
        // 通关只由 BattleManager 在「传送门已激活」后检测，避免未清怪就结算、每帧刷爆 OnStageClear
    }

    void TickLowHpWarn()
    {
        float maxHp = attr != null ? attr.GetAttr(AttrType.MaxHp) : 0f;
        float ratio = maxHp > 0.01f ? currentHp / maxHp : 1f;
        bool dead = isDead || currentHp <= 0f;
        if (unitAnim != null)
            unitAnim.TickLowHpFlash(ratio, dead);
        bool low = !dead && ratio <= GameConfig.LOW_HP_WARN_RATIO + 0.0001f;
        LowHpScreenEdgeFlash.Ensure().Tick(low);
    }

    /// <summary>
    /// 根据当前装备的武器返回攻击类型
    /// </summary>
    protected override WeaponAttackType GetAttackType()
    {
        if (GridBackpackSystem.Instance == null) return WeaponAttackType.Physical;
        var tpl = TryGetEquippedWeaponTemplate(GridBackpackSystem.Instance);
        if (tpl != null)
        {
            foreach (var item in GridBackpackSystem.Instance.GetEquippedItems())
            {
                if (item.template == tpl) return item.weaponAttackType;
            }
        }
        return WeaponAttackType.Physical;
    }

    protected override AttackVfxKit GetAttackVfxKit()
    {
        var bag = GridBackpackSystem.Instance;
        if (bag == null) return AttackVfxKit.MeleeSlash;
        var main = bag.GetEquippedInLogicalSlot(EquipSlotType.MainHand);
        if (main?.template != null && main.weaponType != WeaponType.None)
            return SkillNaming.KitFromWeaponKind(WeaponCombatTable.ResolveKind(main));
        var off = bag.GetEquippedInLogicalSlot(EquipSlotType.OffHand);
        if (off?.template != null && off.weaponType != WeaponType.None)
            return SkillNaming.KitFromWeaponKind(WeaponCombatTable.ResolveKind(off));
        return AttackVfxKit.MeleeSlash;
    }

    /// <summary>攻击特效以逻辑主手武器为准；无主手则不看副手剑。</summary>
    static EquipTemplate TryGetEquippedWeaponTemplate(GridBackpackSystem bag)
        => TryGetEquippedWeaponInstance(bag)?.template;

    static EquipInstance TryGetEquippedWeaponInstance(GridBackpackSystem bag)
    {
        if (bag == null) return null;
        var main = bag.GetEquippedInLogicalSlot(EquipSlotType.MainHand);
        if (main?.template != null && main.weaponType != WeaponType.None)
            return main;
        var off = bag.GetEquippedInLogicalSlot(EquipSlotType.OffHand);
        if (off?.template != null && off.weaponType != WeaponType.None)
            return off;
        return null;
    }

    /// <summary>对外：当前主手武器对应的攻击特效套（技能回退也用）。</summary>
    public AttackVfxKit GetWeaponVfxKit() => GetAttackVfxKit();

    bool _manualMove;
    bool _manualHeld;
    Vector2 _manualDir;
    UnitBase _acquireLock;
    float _acquireUntil;
    float _manualReleaseUntil;

    public bool IsManualMove => _manualMove;

    public void SetManualMove(Vector2 dir)
    {
        _manualHeld = true;
        // 按住期间即使死区也保持手动，避免 AI 抢方向
        _manualMove = true;
        _manualDir = dir;
        _acquireLock = null;
        _acquireUntil = 0f;
        _manualReleaseUntil = 0f;
    }

    public void ClearManualMove()
    {
        _manualMove = false;
        _manualHeld = false;
        _manualDir = Vector2.zero;
        _manualReleaseUntil = Time.time + GameConfig.HERO_MANUAL_RELEASE_HOLD;
    }

    /// <summary>松摇杆：短冷却后再索敌，一律锁最近怪（不按血量追远处）。</summary>
    public void BeginAutoAcquireOnRelease()
    {
        _manualMove = false;
        _manualHeld = false;
        _manualDir = Vector2.zero;
        _manualReleaseUntil = Time.time + GameConfig.HERO_MANUAL_RELEASE_HOLD;
        _acquireLock = FindNearestEnemyOnField();
        _acquireUntil = Time.time + GameConfig.COMBO_WINDOW;
    }

    protected override void AIUpdate()
    {
        if (BattleManager.Instance != null && !BattleManager.Instance.UnitsCanAct)
        {
            base.AIUpdate();
            return;
        }

        if (!_manualMove && TryHoldDuringAttack())
            return;
        // 手动移动时不受击硬直打断走步（仍白闪）
        if (!_manualMove && TryHoldDuringDamaged())
            return;

        // 摇杆优先：手动位移，期间不跑自动追敌（直接改坐标，避免 velocity 被 ApplyLaneY/SetWorldPosition 清掉）
        if (_manualMove)
        {
            float spd = GetCombatMoveSpeed();
            float vx = _manualDir.x * spd * GameConfig.HERO_MANUAL_MOVE_X_MUL;

            if (Mathf.Abs(_manualDir.x) > 0.05f)
            {
                facingDir = _manualDir.x > 0f ? 1 : -1;
                ApplyFacing(facingDir);
            }

            // 车道：固定 ±BATTLE_LANE_HALF，纵向与横向同基础移速
            if (Mathf.Abs(_manualDir.y) > 0.08f)
            {
                float lane = LaneY + _manualDir.y * spd * Time.deltaTime;
                lane = BattleLaneBounds.ClampLaneOffset(lane);
                SetLaneY(lane);
            }

            Vector3 p = transform.position;
            p.x += vx * Time.deltaTime;
            p.y = Mathf.MoveTowards(p.y, FootY, GameConfig.BATTLE_LANE_MOVE_SPEED * Time.deltaTime);
            if (BattleManager.Instance == null || !BattleManager.Instance.PortalWalkMode)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    float halfH = cam.orthographicSize;
                    float halfW = halfH * cam.aspect;
                    float camX = cam.transform.position.x;
                    float margin = 0.3f;
                    p.x = Mathf.Clamp(p.x, camX - halfW + margin, camX + halfW - margin);
                }
            }
            GameConfig.SetWorldPosition(transform, p);
            RefreshDepthSort();

            // 进距仍可普攻最近目标；有水平输入时朝向跟摇杆，不 FaceToward 抢向
            var near = FindNearestEnemyInDetectRange();
            if (near != null && IsInBasicAttackRange(near)
                && attackCd <= 0f
                && (unitAnim == null || !unitAnim.InDamagedRecovery())
                && (unitAnim == null || !unitAnim.InAttackLock))
            {
                target = near;
                if (Mathf.Abs(_manualDir.x) <= 0.05f)
                    FaceToward(near);
                Attack(near);
                attackCd = GetAttackCooldown();
            }

            // 按住摇杆时即使死区也保持走步，避免过中心闪站立
            bool stickWalk = _manualHeld
                || Mathf.Abs(vx) > 0.05f
                || Mathf.Abs(_manualDir.y) > 0.08f;
            if (unitAnim != null)
                unitAnim.SetMove(stickWalk, facingDir);
            return;
        }

        // 松摇杆短冷却：不追怪，避免与手动抢方向；进距仍可普攻
        if (Time.time < _manualReleaseUntil)
        {
            if (rb != null) rb.velocity = new Vector2(0f, rb.velocity.y);
            var nearHold = FindNearestEnemyInDetectRange();
            if (nearHold != null && IsInBasicAttackRange(nearHold)
                && attackCd <= 0f
                && (unitAnim == null || !unitAnim.InDamagedRecovery())
                && (unitAnim == null || !unitAnim.InAttackLock))
            {
                target = nearHold;
                FaceToward(nearHold);
                Attack(nearHold);
                attackCd = GetAttackCooldown();
            }
            if (unitAnim != null) unitAnim.SetMove(false, facingDir);
            if (BattleManager.Instance == null || !BattleManager.Instance.PortalWalkMode)
                ClampToScreen();
            ApplyLaneY(Time.deltaTime);
            return;
        }

        // 松手索敌窗口：进距内有更近可打的怪时打断远锁
        if (_acquireLock != null)
        {
            if (_acquireLock.isDead || Time.time > _acquireUntil)
                _acquireLock = null;
            else
            {
                var nearer = FindNearestEnemyOnField();
                if (nearer != null && nearer != _acquireLock && IsInBasicAttackRange(nearer))
                    _acquireLock = nearer;
                target = _acquireLock;
            }
        }

        bool alliesEngaged = HasAliveEnemyOnField();
        if (_acquireLock == null)
        {
            target = alliesEngaged
                ? FindNearestEnemyOnField()
                : FindNearestEnemyInDetectRange();
        }
        if (target != null && target.isAlly == isAlly)
            target = null;

        // 复用基类其余逻辑：临时改写后调用会重新选目标，故内联走基类流程太重。
        // 把锁定目标塞进一次「伪 base」：直接复制移动/攻击分支。
        bool isMoving = false;
        if (target != null)
        {
            float distance = Mathf.Abs(GetCombatX(this) - GetCombatX(target));
            float attackRange = GetEffectiveAttackRange();
            bool melee = UsesMeleeBasicAttack();
            FaceToward(target);
            if (melee || !alliesEngaged)
                AdjustLaneTowardTarget(target, Time.deltaTime);

            if (IsInBasicAttackRange(target))
            {
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
                if (rb != null) rb.velocity = Vector2.zero;
                isMoving = true;
            }
            else
            {
                if (rb != null)
                    rb.velocity = new Vector2(facingDir * GetCombatMoveSpeed(), rb.velocity.y);
                isMoving = true;
            }
        }
        else
        {
            if (alliesEngaged)
            {
                facingDir = 1;
                ApplyFacing(facingDir);
                if (rb != null) rb.velocity = Vector2.zero;
                isMoving = false;
            }
            else
            {
                facingDir = 1;
                ApplyFacing(facingDir);
                AdjustFormationLane(Time.deltaTime);
                if (rb != null)
                    rb.velocity = new Vector2(GetCombatMoveSpeed(), rb.velocity.y);
                isMoving = true;
            }
        }

        if (unitAnim != null)
            unitAnim.SetMove(isMoving, facingDir);
        if (BattleManager.Instance == null || !BattleManager.Instance.PortalWalkMode)
            ClampToScreen();
        ApplyLaneY(Time.deltaTime);
    }

    protected override void Die(bool isCritKill = false)
    {
        base.Die(isCritKill);
        if (BattleManager.Instance != null)
            BattleManager.Instance.OnHeroDead();
        else
            Debug.LogWarning("[Hero] Die: BattleManager 为空，跳过 OnHeroDead");
    }

    /// <summary>
    /// Hero不回对象池，死亡动画播完后直接隐藏
    /// </summary>
    protected override void OnDeathRelease()
    {
        gameObject.SetActive(false);
    }
}