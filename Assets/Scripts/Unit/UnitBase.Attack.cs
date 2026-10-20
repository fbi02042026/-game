using UnityEngine;
using System.Collections.Generic;
using System.Collections;

// 战斗单位基类 partial —— 2026-10-18 由 UnitBase.cs 纯位移拆出（无逻辑改动）
// 职责：攻击与并道（射程判定、站桩保持、普攻流程、弹道协程、击杀前摇、车道追击）
public partial class UnitBase
{

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

    /// <summary>
    /// 是否应「站桩」不并道。已能攻击到目标→站桩；怪跑动导致短暂脱离时用 0.5s 粘滞保持站桩，
    /// 避免「打一下→挪一下→再打一下」抖动；粘滞结束后若仍脱靶则正常并道/追击（不会卡死）。
    /// </summary>
    // 2026-10-09 主人拍板：近战车道未对齐=根本打不到，禁止站桩/刷新粘滞，立即并道对齐。
    public bool ShouldHoldGround(UnitBase other)
    {
        if (other == null) return false;
        // 近战且车道未对齐：站桩毫无意义，直接放行让调用方并道
        if (UsesMeleeBasicAttack() && !IsMeleeLaneAligned(other)) return false;
        if (IsInBasicAttackRange(other))
        {
            // 在攻击范围内：每帧刷新粘滞窗口，静态目标可持续站桩
            _standHoldUntil = Time.time + 0.5f;
            return true;
        }
        // 已脱离但仍在粘滞窗口内：维持站桩，等待怪走回或窗口结束
        return Time.time < _standHoldUntil;
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

    /// <summary>战斗用纵向坐标（与 GetCombatX 同一参照：怪取身体节点）。</summary>
    public static float GetCombatY(UnitBase u)
    {
        if (u == null) return 0f;
        Transform moveTf = u.transform;
        if (u is Monster m)
            moveTf = m.GetBodyTransform();
        return moveTf != null ? moveTf.position.y : 0f;
    }

    /// <summary>
    /// 战斗索敌/换目标统一距离：X + Y 二维。
    /// 2026-09-28 主人反馈「不按距离、总先打最下面一排、隔车道也开打」——旧逻辑只比 X；
    /// 上下车道差现在也计入距离，追击/换目标仍由车道归位（ApplyLaneY）拉回同一排再出刀。
    /// </summary>
    public static float GetCombatDist(UnitBase a, UnitBase b)
    {
        if (a == null || b == null) return float.MaxValue;
        float dx = GetCombatX(a) - GetCombatX(b);
        float dy = GetCombatY(a) - GetCombatY(b);
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>仅播攻击动画（奥义演出用，不带弹道/刀光）。speedMul>1 加速播放（演出用），forceRestart 允许打断上一段攻击动画重来。</summary>
    public void PlayAttackAnimOnly(AttackVfxKit kit, bool critAmp = false, float speedMul = 1f, bool forceRestart = false)
    {
        if (unitAnim != null)
            unitAnim.PlayAttack(kit, critAmp, -1f, speedMul, forceRestart);
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

        // 敌方换道锚点：只在换目标（或首次进战斗）时记一次，之后沿用，绝不每帧重设，
        // 否则锚点会跟着自己一起漂，等于没上限。
        if (_laneChaseAnchorOwner != chaseTarget)
        {
            _laneChaseAnchorOwner = chaseTarget;
            _laneChaseAnchorY = LaneY;
        }

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

        // 2026-09-28 主人反馈「最上面的敌人能打到最下面的玩家」：敌方换道加上限，
        // 只能在「锁定目标时自己那一排」上下 MONSTER_LANE_CHASE_MAX 范围内并道。
        // 我方（玩家/佣兵）不受限，否则会追不上怪。
        if (!isAlly && GameConfig.MONSTER_LANE_CHASE_MAX > 0f)
        {
            float lo = _laneChaseAnchorY - GameConfig.MONSTER_LANE_CHASE_MAX;
            float hi = _laneChaseAnchorY + GameConfig.MONSTER_LANE_CHASE_MAX;
            targetLane = Mathf.Clamp(targetLane, lo, hi);
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

        // 【临时打点 2026-10-18】排查「普攻608/暴击120」矛盾：只打主角每次普攻的攻防实况，定位后删除
        if (this is Hero && attr != null)
            Debug.Log($"[DamageDebug] 主角出手 raw={damage:F1} crit={isCrit} " +
                      $"魔攻={attr.GetAttr(AttrType.MagicAttack):F1} 物攻={attr.GetAttr(AttrType.Attack):F1} " +
                      $"暴伤={attr.GetAttr(AttrType.CritDamage):F2} 暴率={attr.GetAttr(AttrType.CritRate):F2} " +
                      $"魔法单位={IsMagicDamageDealer()} 目标={(target != null ? target.name : "null")}");

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
        // 2026-10-10 主人拍板：朝哪边打就朝哪边挥剑 —— 出手这一刻把「翻转父级」转向目标（无死区）。
        // 只翻这一个父级节点，人物身体与刀光随它一起翻；只在出手时翻一次，不做每帧，避免自动战斗下左右抖。
        FaceTowardOnAttack(target);
        Vector3 firePos = GetFirePosition();
        Vector3 hitPos = target.GetHitPosition();
        Transform hitTf = target.transform;
        // 2026-10-10 主人拍板：人物与刀光挂同一个翻转父级，朝向只由父级决定 —— 这里读父级当前朝向。
        // 不许再按「攻击者→目标」向量单独算一遍：分开算必然再次出现人物朝一边、刀光朝另一边。
        // 父级已在上面 FaceTowardOnAttack 转向目标，所以读出来必然指向被攻击者。
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
            BattleVFXSystem.Instance.PlayAttackKit(kit, faction, firePos, hitPos, facingDir, hitTf, isCrit, this);
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
        {
            // 引导宿敌挂死亡门：血到底不判死（钳1点等遗言），致死预测必然落空，
            // 慢放会演出成「慢下来又没死」。死亡门挂着期间不进击杀前摇。
            if (m.holdAtDeathDoor) return false;
            return target.currentHp <= PredictBasicAttackDamage(target, damage, openingHit);
        }
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
            BattleVFXSystem.Instance.PlayAttackKit(kit, faction, firePos, hitPos, facingDir, hitTf, isCrit, this);
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

        // 2026-10-10 主人拍板：受击刀光与出手侧同源，也读「翻转父级」当前朝向（GetVfxFacingDir）。
        // 出手前 FaceTowardOnAttack 已把父级转向目标，故它必然指向被攻击者，不可能与人物反着。
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
}
