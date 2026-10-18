using UnityEngine;
using System.Collections.Generic;
using System.Collections;

// 战斗单位基类 partial —— 2026-10-18 由 UnitBase.cs 纯位移拆出（无逻辑改动）
// 职责：索敌与移动 AI（Update/AIUpdate、攻击锁/受击硬直、朝向翻转、屏幕钳制、索敌防抖）
public partial class UnitBase
{

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
            // 2026-10-09 主人拍板：已进入攻击范围则站桩不并道，抗怪跑动抖动
            if (!ShouldHoldGround(target) && (melee || !alliesEngaged))
                AdjustLaneTowardTarget(target, Time.deltaTime);

            if (IsInBasicAttackRange(target))
            {
                // 攻击范围内停步输出，避免贴到目标中心导致双方朝向来回抖
                if (rb != null) rb.velocity = Vector2.zero;
                // 受击动画不挡出手：轮到攻击时 PlayAttack 立即打断 DAMAGED（TryHoldDuringDamaged 同步放行）
                if (attackCd <= 0 && (unitAnim == null || !unitAnim.InAttackLock))
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
                    // 2026-10-10 主人拍板：清场后不再换道归队（原 AdjustFormationLane 会把我方
                    // 立刻搬回队形车道，看着像「打完敌人又往上走一步」）。保持击杀时的车道继续推图，
                    // 下一波索敌时由 AdjustLaneTowardTarget 自然并回。
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

    /// <summary>
    /// 受击硬直：清零速度并停移动动画，避免前进滑步。
    /// 但「轮到攻击」（攻击 CD 已好、目标就在射程内、当前不在出手中）时不硬直——
    /// 立即放行给下方攻击判定，Attack→PlayAttack 会打断 DAMAGED 改播攻击动画。
    /// </summary>
    protected bool TryHoldDuringDamaged()
    {
        if (unitAnim == null || !unitAnim.InDamagedRecovery()) return false;

        if (AttackDueDuringDamaged())
            return false;

        if (rb != null) rb.velocity = Vector2.zero;
        unitAnim.SetMove(false, facingDir);
        if (isAlly && (BattleManager.Instance == null || !BattleManager.Instance.PortalWalkMode))
            ClampToScreen();
        ApplyLaneY(Time.deltaTime);
        return true;
    }

    /// <summary>
    /// 受击动画播放中是否「轮到攻击」：CD 已好、当前锁定目标有效且就在普攻射程内、没在另一击出手锁中。
    /// 命中即放行，由 PlayAttack 立即打断 DAMAGED。
    /// </summary>
    protected bool AttackDueDuringDamaged()
    {
        return attackCd <= 0f
            && unitAnim != null && !unitAnim.InAttackLock
            && target != null && !target.isDead
            && target.gameObject.activeInHierarchy
            && target.isAlly != isAlly
            && IsInBasicAttackRange(target);
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

    /// <summary>刀光可挂载的翻转父级；程序化/flipX 朝向没有可挂节点。</summary>
    public virtual Transform GetFlipRoot()
    {
        bool usesSpriteFlip = unitAnim != null && (unitAnim.IsProcMode || unitAnim.UsesFlipXFacing);
        return usesSpriteFlip ? null : transform;
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

    /// <summary>在索敌范围内找最近敌人（索敌范围=屏幕宽+缓冲，与攻击射程无关）。
    /// 2026-09-28 主人反馈「不按距离、总先打最下面一排」：根因是旧逻辑只比 X，上下车道不参与比较；
    /// 现改为 X+Y 二维距离（<see cref="GetCombatDist"/>）。</summary>
    public virtual UnitBase FindNearestEnemyInDetectRange()
    {
        if (BattleManager.Instance == null) return null;

        float detectRange = GetDetectRange();
        UnitBase nearest = null;
        float minDist = detectRange;
        bool currentTargetCandidate = false;
        IEnumerable<UnitBase> enemyList = isAlly ? BattleManager.Instance.monsters : BattleManager.Instance.allyUnits;
        if (enemyList == null) return null;
        foreach (var enemy in enemyList)
        {
            if (enemy == null || enemy.isDead || !enemy.gameObject.activeInHierarchy) continue;
            if (isAlly && enemy.isAlly) continue;
            if (!isAlly && !enemy.isAlly) continue;
            if (!GameConfig.IsInCombatViewport(enemy)) continue;
            float dist = GetCombatDist(this, enemy);
            if (dist > detectRange) continue;
            if (enemy == target) currentTargetCandidate = true;
            if (dist <= minDist)
            {
                minDist = dist;
                nearest = enemy;
            }
        }
        return ApplyNearestTargetStickiness(nearest, currentTargetCandidate);
    }

    const float TargetSwitchMargin = 0.45f;

    /// <summary>
    /// 切换规则：当前目标死亡、销毁、失活、离开敌方列表/镜头/本次索敌范围时立即改选；
    /// 否则新目标至少近 0.45 才立即切换，距离差小于 0.45 时才保留当前目标防抖。
    /// 2026-09-28：距离比较从 |ΔX| 升级为二维距离（X+Y），与索敌同一把尺。
    /// </summary>
    UnitBase ApplyNearestTargetStickiness(UnitBase nearest, bool currentTargetCandidate)
    {
        if (!currentTargetCandidate || target == null || nearest == null || nearest == target)
            return nearest;

        float curDist = GetCombatDist(this, target);
        float newDist = GetCombatDist(this, nearest);

        // 2026-09-27 主人反馈：「移动到别的怪前面，却还在打第一个索敌的敌人」。
        // 根因：原来只有「新目标比当前目标近 ≥0.45」这一条防抖规则，
        // 当当前目标还挂在锁定上（没死、没出距）时，哪怕玩家已经站到另一只怪脸前也不会改打。
        // 补一条硬规则：**新目标已经进攻击范围、而当前目标还在范围外** → 立刻改打新目标。
        // 防抖阈值只用于「两只都够得着」时的抖动，不该拦住「够得着的 vs 够不着的」。
        // （射程取当前攻击距离；取不到时本条不生效，退回原来的防抖逻辑，不改原行为。）
        float reach = attr != null ? attr.GetAttr(AttrType.AttackRange) : 0f;
        if (reach > 0.01f && newDist <= reach && curDist > reach)
            return nearest;

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

    /// <summary>
    /// 出手那一刻强制转向目标（无死区）：只翻「翻转父级」这一个节点。
    /// 人物身体由 ApplyFacing 写父级、刀光由 GetVfxFacingDir 读父级，两端同源；
    /// 谁都不许再按目标向量单独算一遍，否则必然再次不同步。
    /// 只在出手时调用一次，不做每帧逻辑（自动战斗下每帧转身会左右抖）。
    /// </summary>
    protected void FaceTowardOnAttack(UnitBase other)
    {
        if (other == null) return;
        facingDir = GetCombatX(other) >= GetCombatX(this) ? 1 : -1;
        ApplyFacing(facingDir);
    }

    /// <summary>场上最近敌（交战寻敌进距）。仍要求目标在镜头内，避免打屏外怪。距离用二维（X+Y），与索敌同尺。</summary>
    public virtual UnitBase FindNearestEnemyOnField()
    {
        if (BattleManager.Instance == null) return null;
        UnitBase nearest = null;
        float minDist = float.MaxValue;
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
            float dist = GetCombatDist(this, enemy);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = enemy;
            }
        }
        return ApplyNearestTargetStickiness(nearest, currentTargetCandidate);
    }
}
