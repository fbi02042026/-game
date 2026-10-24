using UnityEngine;

/// <summary>
/// AI 行为：索敌、交战、离队收绳（UpdateHeroLeash / 回召 / 目标刷新 / 跟随待机）。
/// 【2026-10-23 主人拍板】原 Mercenary.cs 已 1025 行（项目约定 >600 行必须拆），
/// 按职责切成 4 个 partial，本文件是其中之一。代码为原样搬运，逻辑未改。
/// </summary>
public partial class Mercenary : UnitBase
{
    protected override void AIUpdate()
    {
        if (TutorialStunned)
        {
            if (rb != null) rb.velocity = Vector2.zero;
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
            if (BattleManager.Instance.PartyIntroWalking)
            {
                facingDir = 1;
                if (unitAnim != null) unitAnim.SetMove(true, facingDir);
            }
            else if (unitAnim != null)
            {
                unitAnim.SetMove(false, facingDir);
            }
            return;
        }

        if (TryHoldDuringAttack())
            return;
        if (TryHoldDuringDamaged())
            return;

        EnsureAiPolicy();

        // 牧师：有伤员且治疗技可放时，站住等自动奶。
        // 【2026-10-20 主人拍板】原实现无条件定身 return，两条路会脱队/死锁：
        // ① 定住时不过拴绳、不走 10-19 的「边打边跟」，玩家往前跑牧师钉在原地；
        // ② 施法失败（蓝不足回蓝慢 / merc_skills 表缺耗蓝 fail closed）时 CooldownRemain 一直 <= 0
        //    而 AllyNeedsHeal() 战斗中几乎恒真（任一队友含自己 < 92% 血）→ 无限站桩。
        // 现加两道保险：离玩家超过 MERC_STICK_RADIUS 不许定住；定住超过 MERC_HEAL_WAIT_MAX_SEC 强制跟队。
        // 治疗是 MercSkillCaster.Update 自动放的，不要求站着 —— 边走边奶成立，本分支只保留「奶人时站着」的表现。
        if (SkillCaster != null && SkillCaster.HasActiveSkill && SkillCaster.CooldownRemain <= 0f
            && AllyNeedsHeal())
        {
            var waitHero = Hero.Instance;
            bool tooFarFromHero = waitHero != null && !waitHero.isDead
                && Mathf.Abs(GetCombatX(this) - GetCombatX(waitHero)) > GameConfig.MERC_STICK_RADIUS;
            _healWaitTimer += Time.deltaTime;
            if (tooFarFromHero || _healWaitTimer > GameConfig.MERC_HEAL_WAIT_MAX_SEC)
            {
                _healWaitTimer = 0f;
                // 不定住，落到底部正常 AI（跟队 / 粘跟随），治疗照常自动放
            }
            else
            {
                if (rb != null) rb.velocity = Vector2.zero;
                if (unitAnim != null) unitAnim.SetMove(false, facingDir);
                ApplyLaneY(Time.deltaTime);
                return;
            }
        }
        else
        {
            _healWaitTimer = 0f;
        }

        // 拴绳：离玩家太远 / 偏远交战过久 → 清目标回队
        if (UpdateHeroLeash(Time.deltaTime))
        {
            bool recallingMove = false;
            FollowHeroOrIdle(ref recallingMove);
            if (unitAnim != null)
                unitAnim.SetMove(recallingMove, facingDir);
            if (BattleManager.Instance == null || !BattleManager.Instance.PortalWalkMode)
                ClampToScreen();
            ApplyLaneY(Time.deltaTime);
            return;
        }

        RefreshAcquireTarget();

        bool isMoving = false;
        if (target != null)
        {
            float distance = Mathf.Abs(GetCombatX(this) - GetCombatX(target));
            float attackRange = GetEffectiveAttackRange();
            bool melee = UsesMeleeBasicAttack();
            FaceToward(target);
            // 2026-10-09 主人拍板：已进入攻击范围则站桩不并道，抗怪跑动抖动
            if (!ShouldHoldGround(target) && melee)
                AdjustLaneTowardTarget(target, Time.deltaTime);

            if (IsInBasicAttackRange(target))
            {
                // 【2026-10-19 主人拍板】交战跟随上限：离玩家超过 MERC_STICK_RADIUS 就「边打边跟」——
                // 朝玩家方向移动（攻距内照常按 CD 开火），保证佣兵始终聚在玩家身边；
                // 半径内仍维持站桩不并道（2026-10-09 拍板不变）。
                var stickHero = Hero.Instance;
                bool mustStickBack = stickHero != null && !stickHero.isDead
                    && Mathf.Abs(GetCombatX(this) - GetCombatX(stickHero)) > GameConfig.MERC_STICK_RADIUS;
                if (mustStickBack)
                {
                    int backDir = GetCombatX(stickHero) > GetCombatX(this) ? 1 : -1;
                    if (rb != null)
                        rb.velocity = new Vector2(backDir * GetCombatMoveSpeed(), rb.velocity.y);
                    isMoving = true;
                    if (attackCd <= 0f
                        && (unitAnim == null || !unitAnim.InAttackLock))
                    {
                        Attack(target);
                        attackCd = GetAttackCooldown();
                    }
                }
                else
                {
                    if (rb != null) rb.velocity = Vector2.zero;
                    // 受击动画不挡出手：轮到攻击时 PlayAttack 立即打断 DAMAGED
                    if (attackCd <= 0f
                        && (unitAnim == null || !unitAnim.InAttackLock))
                    {
                        Attack(target);
                        attackCd = GetAttackCooldown();
                    }
                }
            }
            else if (melee && distance <= attackRange)
            {
                if (rb != null) rb.velocity = Vector2.zero;
                isMoving = true;
            }
            else
            {
                // 已锁目标但未进攻击距：朝目标走，朝向只看怪（不用走位方向翻身）
                if (rb != null)
                    rb.velocity = new Vector2(facingDir * GetCombatMoveSpeed(), rb.velocity.y);
                isMoving = true;
            }
        }
        else
        {
            // 索敌距内无怪：跟玩家站位 / 推图，朝右，不左右找怪
            FollowHeroOrIdle(ref isMoving);
        }

        if (unitAnim != null)
            unitAnim.SetMove(isMoving, facingDir);
        if (BattleManager.Instance == null || !BattleManager.Instance.PortalWalkMode)
            ClampToScreen();
        ApplyLaneY(Time.deltaTime);
    }

    /// <summary>
    /// 维护与玩家的拴绳。返回 true 表示本帧应只回队、不交战。
    /// </summary>
    bool UpdateHeroLeash(float dt)
    {
        var hero = Hero.Instance;
        if (hero == null || hero.isDead)
        {
            _recallingToHero = false;
            _awayFightTimer = 0f;
            return false;
        }

        float distHero = Mathf.Abs(GetCombatX(this) - GetCombatX(hero));

        if (_recallingToHero)
        {
            if (distHero <= GameConfig.MERC_REJOIN_RADIUS)
            {
                _recallingToHero = false;
                _awayFightTimer = 0f;
                return false;
            }
            ClearAcquireLock();
            return true;
        }

        if (distHero > GameConfig.MERC_LEASH_RADIUS)
        {
            BeginRecallToHero();
            return true;
        }

        // 偏远交战：打一会就回去找玩家
        bool fightingAway = target != null && !target.isDead && distHero > GameConfig.MERC_SOFT_AWAY_RADIUS;
        if (fightingAway)
        {
            _awayFightTimer += dt;
            if (_awayFightTimer >= GameConfig.MERC_AWAY_FIGHT_SEC)
            {
                BeginRecallToHero();
                return true;
            }
        }
        else
        {
            _awayFightTimer = 0f;
        }

        return false;
    }

    void BeginRecallToHero()
    {
        _recallingToHero = true;
        _awayFightTimer = 0f;
        ClearAcquireLock();
    }

    void ClearAcquireLock()
    {
        _acquireLock = null;
        target = null;
    }

    /// <summary>只认索敌距离：进距最近者锁定，直至死亡或出距。收队中不索敌。</summary>
    void RefreshAcquireTarget()
    {
        if (_recallingToHero)
        {
            ClearAcquireLock();
            return;
        }

        float detect = GetDetectRange();
        float myX = GetCombatX(this);
        const float leaveSlack = 0.75f;

        if (_acquireLock != null)
        {
            if (_acquireLock.isDead || _acquireLock.isAlly == isAlly)
                _acquireLock = null;
            else if (Mathf.Abs(myX - GetCombatX(_acquireLock)) > detect + leaveSlack)
                _acquireLock = null;
            else if (!GameConfig.IsInCombatViewport(_acquireLock))
                _acquireLock = null;
        }

        if (_acquireLock == null)
            _acquireLock = FindNearestEnemyInDetectRange();

        target = _acquireLock;
        if (target != null && target.isAlly == isAlly)
            ClearAcquireLock();
    }

    /// <summary>无锁定目标：贴玩家编队站位；玩家在推图则跟着走。</summary>
    void FollowHeroOrIdle(ref bool isMoving)
    {
        facingDir = 1;
        ApplyFacing(facingDir);
        // 2026-10-10 主人拍板：清场后不再换道归队（原 AdjustFormationLane 会把佣兵
        // 立刻搬回队形车道，看着像「打完敌人又往上走一步」）。场上还有怪时照常归队站位。
        if (HasAliveEnemyOnField())
            AdjustFormationLane(Time.deltaTime);

        var hero = Hero.Instance;
        if (hero == null || hero.isDead)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            isMoving = false;
            return;
        }

        float desiredX = UnitCrowd.GetMercDesiredCombatX(hero, this, ResolvePartyIndex());
        float dx = desiredX - GetCombatX(this);
        if (Mathf.Abs(dx) > 0.22f)
        {
            int moveDir = dx > 0f ? 1 : -1;
            // 【2026-10-19 主人拍板】追赶加速：落后编队位超过起步距离 → 比玩家快一档追回来。
            // 同速追赶永远追不上（见 GameConfig.MERC_CATCHUP_SPEED_MUL 注释），
            // 不加这条佣兵只会越掉越远。回队（拴绳触发）也走这里，一并享受加速。
            float speed = GetCombatMoveSpeed();
            if (Mathf.Abs(dx) > GameConfig.MERC_CATCHUP_ENTER_DIST)
                speed *= GameConfig.MERC_CATCHUP_SPEED_MUL;
            if (rb != null)
                rb.velocity = new Vector2(moveDir * speed, rb.velocity.y);
            isMoving = true;
        }
        else if (!HasAliveEnemyOnField())
        {
            // 场上无怪：与玩家一起右推
            if (rb != null)
                rb.velocity = new Vector2(GetCombatMoveSpeed(), rb.velocity.y);
            isMoving = true;
        }
        else
        {
            // 场上有怪但不在自己索敌距：站住跟队，等怪进距再锁
            if (rb != null) rb.velocity = Vector2.zero;
            isMoving = false;
        }
    }
}
