using UnityEngine;
using System.Collections.Generic;
using System.Collections;

// 战斗单位基类 partial —— 2026-10-18 由 UnitBase.cs 纯位移拆出（无逻辑改动）
// 职责：承伤与死亡（无敌帧、TakeDamage 全流程、死亡动画与回收、ResetForReuse、事件）
public partial class UnitBase
{
    /// <summary>子类可重写：当前是否处于无敌（如 Hero 闪避无敌帧）。默认走技能型无敌计时。</summary>
    // 技能型无敌（SK010 神圣庇佑「全体无敌 1.5 秒」）：到期前免疫敌方伤害（source 为敌方才拦，
    // 与闪避无敌帧同口径——关卡机制伤害不免疫）。仅免疫敌方伤害，与子类无敌叠加时各算各的。
    float _skillInvulnUntil = -1f;

    /// <summary>授予一段技能型无敌（重复授予取更晚到期时间）。</summary>
    public void ApplyInvulnerability(float duration)
        => _skillInvulnUntil = Mathf.Max(_skillInvulnUntil, Time.time + Mathf.Max(0f, duration));

    protected virtual bool IsInvincibleNow() => Time.time < _skillInvulnUntil;

    /// <summary>【引导演出专用】血量被打到底时先不判死（血钳到 1 点，站着把遗言说完），
    /// 剧情侧（TutorialDirector）说完后解除再手动判死。默认 false，正式关完全不经过该分支。</summary>
    public bool holdAtDeathDoor;

    public virtual void TakeDamage(float damage, bool isCrit, bool ignoreDefense = false, bool showHitVfx = true, int hitVfxFacing = 0, UnitBase source = null, bool suppressDamageText = false)
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

        // 2026-10-24 主人拍板「类型克制·双向」：补传目标类型（本单位的伤害类型），
        // 攻击方与目标类型不同 → FinalHit 内伤害 ×1.1
        float finalDamage = DamageFormula.FinalHit(damage, attr, ignoreDefense, magicHit, IsMagicDamageDealer());

        // 【临时打点 2026-10-18】排查「普攻608/暴击120」矛盾：主角打出的每一击落地实况，定位后删除
        if (source is Hero)
            Debug.Log($"[DamageDebug] 主角命中 final={finalDamage:F1} crit={isCrit} " +
                      $"raw入参={damage:F1} 目标={name} " +
                      $"目标防御={(magicHit ? attr.GetAttr(AttrType.MagicDefense) : attr.GetAttr(AttrType.Defense)):F1} magicHit={magicHit}");
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

        // 2026-09-27 主人拍板：引导局我方不死人。自动战斗也会掉血到 0，一旦死掉整套引导流程就崩
        // （死亡 → 回城 → 直接跳到引导后续剧情）。只在这一个入口钳血：最低留 1 点，别处不再改。
        // 玩家和引导佣兵（现为 H003 塔克，非牧师）都算「我方」，他中途死了同样会走进队失败的分支。
        var tutBm = BattleManager.Instance;
        if (isAlly && tutBm != null && tutBm.IsTutorialRun && finalDamage >= currentHp - 1f)
            finalDamage = Mathf.Max(0f, currentHp - 1f);

        // 2026-10-09 主人拍板：结算「总伤害」只记实际打掉的量，不含溢出 —— 引导关塔克天降那一击
        // 对每只 20 血小怪写死 999999 伤害（演出用），旧口径按 999999 全记，12 只就是千万级，
        // 正是主人报的「总伤害不可能那么高」。钳到扣血前的剩余血量即可（hpBefore 为扣血前值）。
        float hpBefore = currentHp;
        currentHp -= finalDamage;
        float effectiveDamage = Mathf.Clamp(finalDamage, 0f, Mathf.Max(0f, hpBefore));

        // 【2026-10-16 主人确认「受击掉血要过一会才显示」】根因 = HUD 血条走 0.1s 节流刷新
        //（BattleUI.Update 的 LiveBarInterval），观感就是「打中了血条半天不动」。
        // 我方受击当场推一次刷新，不等节拍；RefreshLiveBars 自带变化检测，没变化时是空转，推着刷不亏。
        if (isAlly)
            BattleUI.Instance?.ForceRefreshLiveBars();

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
        // 【2026-10-13 主人拍板】suppressDamageText：引导演出的判死不飘字
        // （999999 演出伤害会炸出一大片紫色暴击大字，主人原话「那一大块看的不舒服」；正式关照常飘）。
        if (!suppressDamageText)
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
                // 统计用 effectiveDamage（实际扣血），不用未钳制的 finalDamage
                bm.RecordDamageDealt(effectiveDamage, mon.IsBossUnit);
                if (isCrit) bm.RecordCrit();
                if (source != null && source.isAlly)
                {
                    bm.RecordAllyDamage(source, effectiveDamage);
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
        {
            // 【引导演出专用】死亡门：血条到底先拦住不判死（血钳到 1 点），
            // 遗言由剧情侧接管，说完解除后手动判死。默认 false 走原判死。
            if (holdAtDeathDoor)
                currentHp = 1f;
            else
                Die(isCrit && finalDamage > 0f);
        }
    }

    /// <summary>受击表现：默认播受击动画+闪白；玩家/佣兵可覆盖。</summary>
    protected virtual void PlayHitReaction()
    {
        if (unitAnim != null)
            unitAnim.PlayDamaged(playHitAnim: true);
    }

    /// <summary>
    /// 原地复活（2026-10-24 主人拍板「战斗内复活，扣钻石」）：回血、清死亡态、复位动画、
    /// 若死亡回收已把本体隐藏则重新显示。当前只有主角会走（BattleManager.DoReviveHero），
    /// 怪物/佣兵不许调（怪物回池复用走 ResetForReuse，佣兵阵亡走各自清场）。
    /// </summary>
    /// <returns>true = 复活成功；false = 本来就没死，不重复处理。</returns>
    public bool Revive(float hpRatio)
    {
        if (!_isDying && currentHp > 0f) return false;
        _isDying = false;
        float maxHp = attr != null ? Mathf.Max(1f, attr.GetAttr(AttrType.MaxHp)) : 1f;
        currentHp = Mathf.Max(1f, maxHp * Mathf.Clamp01(hpRatio));
        if (unitAnim != null)
            unitAnim.ResetToIdle();
        if (rb != null)
            rb.velocity = Vector2.zero;
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);   // 死亡动画播完已隐藏（Hero.OnDeathRelease）→ 重新上屏
        return true;
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
        holdAtDeathDoor = false;
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
