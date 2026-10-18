using UnityEngine;
using System.Collections.Generic;
using System.Collections;

// 战斗单位基类 partial —— 2026-10-18 由 UnitBase.cs 纯位移拆出（无逻辑改动）
// 职责：属性口径与受击位移（攻速/移速/攻击类型、火冰附加、吸血、受击位移节流）
public partial class UnitBase
{

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
        // 【2026-10-11 主人拍板：删除玩家 10% 加速】原 2026-09-28「玩家行走速度再加快 10%」（只加玩家、佣兵不动）
        // 已作废 —— 主人反馈「玩家的移动速度要和佣兵的一致」，这条补丁本身就是两边不一致的来源之一。
        // 现在玩家 / 佣兵 / 怪物都只吃 attr.MoveSpeed 一个真源，谁快谁慢只看各自配表列，代码不再偷偷加成。
        //   玩家：PlayerJobBaseStats → BASE_MOVE_SPEED × (player_job_base_stats.基础移速 / 100)
        //   佣兵：MercRosterDefs    → BASE_MOVE_SPEED × (merc_roster.MoveSpeed / 3.5)
        // ⚠ GameConfig.PLAYER_MOVE_SPEED_MUL 已无调用（死常量），留着只为旧引用不炸，确认无引用后可删。
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
    /// 吸血：基础吸血已归零（2026-10-09 主人拍板），只有带吸血词缀/技能的单位才回血。
    /// 没带吸血就是 0，不回血（下面的 if (ratio &lt;= 0f) return; 天然覆盖该语义）。
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

}
