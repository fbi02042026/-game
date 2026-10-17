using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

// 怪物单位 partial —— 2026-10-17 由 Monster.cs 纯位移拆出（无逻辑改动）
// 职责：入场走位（BeginMapEnter/AIUpdate 入场段）、索敌策略、交战循环
public partial class Monster
{
    // 【2026-10-14 主人拍板】索敌多策略：近战按距离 / 远程先打脆皮职业 / 部分怪挑血少的打。
    // 策略在 Init 里 style 定稿后抽一次；评分与选择在 MonsterTargeting.Pick。
    MonsterTargetingPolicy _targetingPolicy = MonsterTargetingPolicy.Distance;
    public MonsterTargetingPolicy TargetingPolicy => _targetingPolicy;

    // 【2026-10-14 主人拍板】Boss 多段切换索敌策略：每隔 BOSS_TARGET_SHIFT_INTERVAL 按
    // Distance→SquishyJob→LowHp 轮换；目标真变了就在新目标头顶弹红色「锁」（BossTargetMark）。
    float _bossTargetShiftCd;
    UnitBase _lastBossMarkTarget;
    private bool _isEnteringMap;
    /// <summary>
    /// 剧情前置暂停（UnitsCanAct=false 且 AllowMonsterMapEnter=true）时，怪只走到屏幕边缘就停，
    /// 此时 _isEnteringMap 仍为 true（要等「！」之后才继续推进到交战点）。
    /// 引导流程要「最后一只怪站定就弹对白」，靠这个标志判断，不能只等 _isEnteringMap 变 false。
    /// </summary>
    private bool _enterAtPauseStop;
    private Vector3 _enterTargetPos;
    private float _enterSpeed = 1.6f;

    /// <summary>是否仍在从屏外走进交战点。</summary>
    public bool IsEnteringMap => _isEnteringMap;

    /// <summary>已走到「剧情暂停位」（屏幕边缘）并站定；对引导来说等于「这只怪已入场完毕」。</summary>
    public bool EnterAtPauseStop => _enterAtPauseStop;

    /// <summary>从地图边缘缓步走向交战点；faceDir 为入场朝向（左进场朝右=1，右进场朝左=-1）</summary>
    public void BeginMapEnter(Vector3 engagePos, int faceDir = -1)
    {
        // 【2026-10-11 主人拍板】入场不再飞：速度挂钩自身 MoveSpeed（旧常量 MONSTER_ENTER_SPEED=1.0 是正常移速的 2.9 倍）。
        float moveSpeed = attr.GetAttr(AttrType.MoveSpeed);
        if (moveSpeed <= 0f)
        {
            Debug.LogError($"[Monster] {gameObject.name} MoveSpeed={moveSpeed}，入场无法推进。");
            return;
        }
        float enterMul = _isBossUnit ? 1f : Random.Range(0.9f, 1.15f);
        _enterTargetPos = engagePos;
        _enterSpeed = moveSpeed * GameConfig.MONSTER_ENTER_SPEED_MUL * enterMul;
        _isEnteringMap = true;
        if (_isBossUnit)
            BattleBossHpBar.PlayBossIntro(this); // BOSS 出场：血条入场演出，与走进场同时进行
        else if (_eliteWave)
            BattleBossHpBar.RefreshFromField();  // 【2026-10-14 主人拍板】精英出场也弹屏幕血条
                                                 //（原来要等第一次受击才刷新出来，引导宿敌战一直没血条）
        _enterAtPauseStop = false;
        facingDir = faceDir > 0 ? 1 : -1;
        ApplyFacing(facingDir);
        if (rb != null) rb.velocity = Vector2.zero;
    }

    UnitBase _forcedTarget;

    /// <summary>?????????????????????/summary>
    public void SetForcedTarget(UnitBase t) => _forcedTarget = t;

    protected override void AIUpdate()
    {
        // 入场：默认剧情冻结时暂停；仅 AllowMonsterMapEnter 窗口可继续走进
        var bmEnter = BattleManager.Instance;
        bool preFightPause = bmEnter != null && bmEnter.AllowMonsterMapEnter && !bmEnter.UnitsCanAct;
        if (_isEnteringMap)
        {
            bool canEnter = bmEnter == null
                || bmEnter.UnitsCanAct
                || bmEnter.AllowMonsterMapEnter;
            if (!canEnter)
            {
                if (rb != null) rb.velocity = Vector2.zero;
                if (unitAnim != null) unitAnim.SetMove(false, facingDir);
                ApplyLaneY(Time.deltaTime);
                return;
            }

            float dx = _enterTargetPos.x - MoveRoot.position.x;
            // 进场途中若已贴近英雄，提前结束进场并开打，避免穿身跑到对面
            // 2026-09-28：距离改二维（X+Y），别在别的车道就提前停步「站原地开打」
            if (Hero.Instance != null && !Hero.Instance.isDead
                && bmEnter != null && bmEnter.UnitsCanAct && !bmEnter.AllowMonsterMapEnter)
            {
                float distHero = GetCombatDist(this, Hero.Instance);
                if (distHero <= GetEffectiveAttackRange() + 0.4f)
                {
                    _isEnteringMap = false;
                    _enterAtPauseStop = false;
                    SyncLaneYFromWorld();
                    target = Hero.Instance;
                    FaceToward(Hero.Instance);
                }
            }
            if (_isEnteringMap && Mathf.Abs(dx) <= 0.08f)
            {
                GameConfig.SetWorldPosition(MoveRoot, new Vector3(_enterTargetPos.x, FootY, MoveRoot.position.z));
                _isEnteringMap = false;
                _enterAtPauseStop = false;
                SyncLaneYFromWorld();
                UnitBase enterFoe = FindNearestEnemyOnField();
                if (enterFoe == null && Hero.Instance != null && !Hero.Instance.isDead)
                    enterFoe = Hero.Instance;
                if (enterFoe != null)
                {
                    target = enterFoe;
                    FaceToward(enterFoe);
                }
                // 进场结束若仍在剧情窗：站住等「！」再开打
                if (bmEnter != null && (!bmEnter.UnitsCanAct || bmEnter.AllowMonsterMapEnter))
                {
                    if (rb != null) rb.velocity = Vector2.zero;
                    if (unitAnim != null) unitAnim.SetMove(false, facingDir);
                    ApplyLaneY(Time.deltaTime);
                    return;
                }
            }
            else if (_isEnteringMap)
            {
                int enterFace = _enterTargetPos.x >= MoveRoot.position.x ? 1 : -1;
                facingDir = enterFace;
                ApplyFacing(facingDir);
                // 剧情前置暂停：只走到屏幕边缘即停，不深入；等「！」后 _isEnteringMap 仍未结束，继续推进到交战点
                float effTargetX = _enterTargetPos.x;
                if (preFightPause)
                {
                    BattleManager.GetBattleVisibleX(out float vMin, out float vMax);
                    effTargetX = enterFace > 0 ? vMin + GameConfig.MONSTER_PAUSE_STOP_MARGIN
                                              : vMax - GameConfig.MONSTER_PAUSE_STOP_MARGIN;
                }
                float step = _enterSpeed * Time.deltaTime;
                float nx = Mathf.MoveTowards(MoveRoot.position.x, effTargetX, step);
                GameConfig.SetWorldPosition(MoveRoot, new Vector3(nx, FootY, MoveRoot.position.z));
                bool stillWalking = Mathf.Abs(effTargetX - nx) > 0.02f;
                // 剧情暂停窗内走到屏幕边缘即算「入场完毕」，供引导立即接管（否则要等到超时）
                _enterAtPauseStop = preFightPause && !stillWalking;
                if (unitAnim != null) unitAnim.SetMove(stillWalking, facingDir);
                ApplyLaneY(Time.deltaTime);
                return;
            }
        }

        // 剧情前置暂停：所有怪（含强制目标）原地等待玩家「！」，杜绝某怪绕过暂停直接冲向英雄
        if (preFightPause)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            if (unitAnim != null) unitAnim.SetMove(false, facingDir);
            ApplyLaneY(Time.deltaTime);
            return;
        }

        // 【2026-10-14 主人拍板】Boss 多段切换索敌策略：定时轮换（Distance→SquishyJob→LowHp）。
        // 换完不在这里直接选人——下一帧 FindNearestEnemyOnField 按新策略重选，
        // 目标真的变了由下面「换目标弹锁」检测统一提示。入场/剧情暂停/技能预警期间不计时。
        if (_isBossUnit)
        {
            _bossTargetShiftCd -= Time.deltaTime;
            if (_bossTargetShiftCd <= 0f)
            {
                _bossTargetShiftCd = GameConfig.BOSS_TARGET_SHIFT_INTERVAL;
                _targetingPolicy = NextBossPolicy(_targetingPolicy);
            }
        }

        if (_skillTelegraphing || _bossPhaseShiftBusy || _leaping)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            if (unitAnim != null) unitAnim.SetMove(false, facingDir);
            // 【2026-10-17】精英蓄力跳滞空期间不走归道：ApplyLaneY 每帧把 Y 拉回 FootY，跳不起来。
            if (!_leaping)
                ApplyLaneY(Time.deltaTime);
            return;
        }

        if (_forcedTarget != null)
        {
            if (_forcedTarget.isDead)
                _forcedTarget = null;
            else
            {
                target = _forcedTarget;
                if (TryHoldDuringAttack())
                    return;
                RunForcedCombat();
                return;
            }
        }

        // 攻击中不换目标、不滑步
        if (TryHoldDuringAttack())
            return;

        // 场上最近友军（交战寻敌）
        target = FindNearestEnemyOnField();
        if (target != null && target.isAlly == isAlly)
            target = null;
        if (target == null)
        {
            AdvanceTowardEnemies();
            return;
        }

        // 【2026-10-14 主人拍板】Boss 换目标提示：目标变更（策略轮换 / 原目标死亡）→ 头顶弹「锁」
        if (_isBossUnit && target != _lastBossMarkTarget)
        {
            _lastBossMarkTarget = target;
            BossTargetMark.Show(target, this);
        }

        RunForcedCombat();
    }

    /// <summary>场上最近友军（交战寻敌）。距离用二维（X+Y），与 UnitBase 索敌同尺（2026-09-28）。
    /// 【2026-10-14 主人拍板】改为按本单位策略选目标（近战距离/远程脆皮/部分怪挑血少的）；
    /// Distance 策略与旧「取最近」逐字节等价（同过滤、同 GetCombatDist）。</summary>
    public override UnitBase FindNearestEnemyOnField()
    {
        if (BattleManager.Instance == null) return null;
        var allies = BattleManager.Instance.allyUnits;
        if (allies == null) return null;
        return MonsterTargeting.Pick(this, allies);
    }

    protected override bool UsesMeleeBasicAttack()
    {
        return !MonsterAttackStyleTable.IsRanged(_attackStyle);
    }

    /// <summary>Boss 策略轮换顺序：按距离 → 打脆皮 → 收割残血 → 回到按距离，循环可被玩家学习。</summary>
    MonsterTargetingPolicy NextBossPolicy(MonsterTargetingPolicy cur)
    {
        switch (cur)
        {
            case MonsterTargetingPolicy.SquishyJob: return MonsterTargetingPolicy.LowHp;
            case MonsterTargetingPolicy.LowHp: return MonsterTargetingPolicy.Distance;
            default: return MonsterTargetingPolicy.SquishyJob;
        }
    }

    /// <summary>攻击风格是否为远程（弓/法球）。教程关按「近战厚 / 远程脆」分档配血用。</summary>
    public bool IsRangedStyle => MonsterAttackStyleTable.IsRanged(_attackStyle);

    void RunForcedCombat()
    {
        if (_isEnteringMap) return;
        if (_skillTelegraphing)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            if (unitAnim != null) unitAnim.SetMove(false, facingDir);
            ApplyLaneY(Time.deltaTime);
            return;
        }
        if (BattleManager.Instance != null
            && (!BattleManager.Instance.UnitsCanAct || BattleManager.Instance.AllowMonsterMapEnter))
        {
            if (rb != null) rb.velocity = Vector2.zero;
            if (unitAnim != null) unitAnim.SetMove(false, facingDir);
            ApplyLaneY(Time.deltaTime);
            return;
        }
        if (target == null || target.isDead)
        {
            ApplyLaneY(Time.deltaTime);
            return;
        }

        if (TryHoldDuringAttack())
            return;

        float distance = Mathf.Abs(GetCombatX(this) - GetCombatX(target));
        float attackRange = GetEffectiveAttackRange();
        bool melee = UsesMeleeBasicAttack();
        // 【2026-10-16 主人拍板】停走只认一条线：够得着（IsInBasicAttackRange = range+0.12）才站定，
        // 够不着就一直走进去打 —— 任何情况都不允许「停下来却出不了手」的站桩死区。
        // 旧版远程把站定线画在 range+0.4、出手线却只有 range+0.12（UnitBase.Attack 的进距闸），
        // 从远处走来的怪停在 2.7 永远够不着 2.42，只能干站着（引导大波最后两只远程即此）。
        // 代价：玩家微移时远程会跟着小幅挪步 —— 主人拍板「会去找着打」优先于「防滑步」。
        bool inHoldRange = IsInBasicAttackRange(target);

        if (melee)
        {
            // 【2026-10-14 修复】上面的怪不下来找玩家：未贴脸时恢复并道——追击阶段一边沿 X 追
            // 一边沉到目标车道，并到同排后站桩开打。敌方仍受 MONSTER_LANE_CHASE_MAX 上限约束。
            // 2026-10-11 拍板的「站定后继续下沉/上浮」不会复发：inHoldRange（已能开打）时跳过并道，
            // 站桩期间 Y 一动不动。
            FaceToward(target);
            if (!inHoldRange)
                AdjustLaneTowardTarget(target, Time.deltaTime);
        }

        bool isMoving = false;
        if (inHoldRange)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            if (!melee)
                FaceToward(target);
            if (attackCd <= 0 && (unitAnim == null || !unitAnim.InAttackLock))
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
            FaceToward(target);
            float spd = attr.GetAttr(AttrType.MoveSpeed);
            if (rb != null) rb.velocity = new Vector2(facingDir * spd, rb.velocity.y);
            isMoving = true;
        }

        if (unitAnim != null) unitAnim.SetMove(isMoving, facingDir);
        ApplyLaneY(Time.deltaTime);
    }

    void AdvanceTowardEnemies()
    {
        if (BattleManager.Instance != null
            && (!BattleManager.Instance.UnitsCanAct || BattleManager.Instance.AllowMonsterMapEnter))
        {
            if (rb != null) rb.velocity = Vector2.zero;
            if (unitAnim != null) unitAnim.SetMove(false, facingDir);
            ApplyLaneY(Time.deltaTime);
            return;
        }

        UnitBase foe = FindNearestEnemyOnField();
        if (foe == null && Hero.Instance != null && !Hero.Instance.isDead)
            foe = Hero.Instance;

        if (foe == null)
        {
            // 无目标：停步，勿写死往左冲
            if (rb != null) rb.velocity = Vector2.zero;
            if (unitAnim != null) unitAnim.SetMove(false, facingDir);
            ApplyLaneY(Time.deltaTime);
            return;
        }

        target = foe;
        float dist = Mathf.Abs(GetCombatX(foe) - GetCombatX(this));
        float attackRange = GetEffectiveAttackRange();
        // 与 RunForcedCombat 一致：远程用离开迟滞，避免边界抖步
        float hold = UsesMeleeBasicAttack() ? attackRange : attackRange + 0.4f;
        if (dist <= hold)
        {
            RunForcedCombat();
            return;
        }

        float dir = GetCombatX(foe) > GetCombatX(this) ? 1f : -1f;
        facingDir = (int)dir;
        ApplyFacing(facingDir);

        float spd = attr.GetAttr(AttrType.MoveSpeed);
        if (rb != null) rb.velocity = new Vector2(dir * spd, rb.velocity.y);
        if (unitAnim != null) unitAnim.SetMove(true, facingDir);
        ApplyLaneY(Time.deltaTime);
    }
}
