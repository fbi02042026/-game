using System.Collections;
using UnityEngine;

/// <summary>
/// 宠物专属技能（2026-11-21 用户「攻击的特效要有 特有的技能也要有」）。
///
/// 五只本体各 1 个常驻技，皮肤跟随本体同一套（换皮不换技能，与 PetDefs.skillDesc 口径一致）：
///   修勾/狐大仙 → 猛扑：前冲 + 一记强化物伤（刀光 + 暴击火花）
///   钱嘎嘎/复读机 → 拾金：金光一闪 + 小额伤害（战斗内无金币掉落系统，本批只做表现，不造第二套经济）
///   顶梁柱/菇凉 → 羊毛盾：玩家 + 全体出战佣兵各挂一层小盾（口径照 SkillCastService.ApplyTeamShieldBuff）
///   焰角/小火炭 → 火球术：朝目标放火球，落点小范围法伤（火球 + 火光）
///   嗷呜/影嗷 → 裂爪：三段近身挥爪（三段刀光）
///
/// 调度口径照 MercSkillCaster：UnitsCanAct 闸门 + 眩晕不放 + 无目标 0.3s 节流重试；
/// 但不吃蓝条、不查 merc_skills 表 —— 宠物技能不走 SkillRegistry（那是佣兵表驱动链路，
/// 宠物 id 不在表里，硬走会 fail closed）。伤害统一进 UnitBase.TakeDamage，特效统一进 BattleVFXSystem。
/// </summary>
public partial class Pet
{
    const float SKILL_OPENING_DELAY = 2f;      // 开场暖机（11-22 主人「没看到放技能」：6s→2s，让技能更早露面）
    const float SKILL_RETRY_INTERVAL = 0.3f;   // 无目标/冻场时的重试节流

    Coroutine _skillLoopCo;
    string _skillKey;                          // 本体 id：dog/toucan/goat/lizard/wolf（皮肤已归一）

    /// <summary>由 InitPet 末尾启动；皮肤按 skinOf 归一到本体，同技能。</summary>
    void StartSkillLoop()
    {
        _skillKey = _def != null && !string.IsNullOrEmpty(_def.skinOf) ? _def.skinOf
                  : (_def != null ? _def.id : null);
        if (string.IsNullOrEmpty(_skillKey)) return;
        if (_skillLoopCo != null) StopCoroutine(_skillLoopCo);
        _skillLoopCo = StartCoroutine(CoSkillLoop());
    }

    IEnumerator CoSkillLoop()
    {
        // 开场暖机（受单位冻结影响 → 用 deltaTime 计，与 UnitsCanAct 口径一致）
        float warm = 0f;
        while (warm < SKILL_OPENING_DELAY)
        {
            if (_fled || isDead) yield break;
            warm += Time.deltaTime;
            yield return null;
        }

        while (true)
        {
            if (_fled || isDead) yield break;

            var bm = BattleManager.Instance;
            if (bm == null || !bm.UnitsCanAct || IsStunned)
            {
                yield return new WaitForSeconds(SKILL_RETRY_INTERVAL);
                continue;
            }
            var target = FindNearestEnemyOnField();
            if (target == null || target.isDead)
            {
                yield return new WaitForSeconds(SKILL_RETRY_INTERVAL);
                continue;
            }

            // 11-22：灰狼裂爪是三段近身挥爪，却对全场任意距离的怪照放 → 老远就播攻击动画。
            // 技能路径补进距硬闸（普攻早有 IsInBasicAttackRange，只有技能这条漏了）。
            // 闸按各自身 AttackRange 算：wolf 近战短闸、wolf_demon 远程长闸（_skillKey 归一到本体 wolf，
            // 但 GetEffectiveAttackRange 仍读各自属性），远处目标等待走近再放，不空挥。
            if (_skillKey == "wolf" && !IsInBasicAttackRange(target))
            {
                yield return new WaitForSeconds(SKILL_RETRY_INTERVAL);
                continue;
            }

            yield return StartCoroutine(CoCastSkill(target));
            yield return new WaitForSeconds(SkillCooldown(_skillKey));
        }
    }

    static float SkillCooldown(string key)
    {
        switch (key)
        {
            case "dog": return 9f;
            case "toucan": return 12f;
            case "goat": return 22f;    // 护盾类，间隔拉长
            case "lizard": return 12f;
            case "wolf": return 11f;
            default: return 12f;
        }
    }

    IEnumerator CoCastSkill(UnitBase target)
    {
        PlayAnim("Attack");
        switch (_skillKey)
        {
            case "dog": yield return CoPounce(target); break;
            case "toucan": CastPickGold(target); break;
            case "goat": CastWoolShield(); break;
            case "lizard": CastFireball(target); break;
            case "wolf": yield return CoClawRush(target); break;
        }
    }

    // ── 猛扑：朝目标短促前冲，再补一记强化物伤 ──
    IEnumerator CoPounce(UnitBase target)
    {
        if (rb != null && target != null)
        {
            float dir = target.transform.position.x >= transform.position.x ? 1f : -1f;
            facingDir = dir > 0 ? 1 : -1;
            ApplyFacing(facingDir);
            float speed = GameConfig.BASE_MOVE_SPEED * 2.6f;
            float t = 0f;
            while (t < 0.18f && !_fled)
            {
                if (rb != null) rb.velocity = new Vector2(dir * speed, rb.velocity.y);
                t += Time.deltaTime;
                yield return null;
            }
        }
        if (target == null || target.isDead || _fled) yield break;

        float dmg = attr.GetAttr(AttrType.Attack) * 1.8f;
        target.TakeDamage(dmg, false, false, true, facingDir, this);

        var vfx = BattleVFXSystem.Instance;
        if (vfx != null)
        {
            vfx.PlaySlash(target.GetHitPosition(), facingDir, VfxFaction.Ally);
            vfx.PlayCritical(target.GetHitPosition(), target.transform, VfxFaction.Ally);
        }
    }

    // ── 拾金：金光一闪 + 小额伤害（只做表现，不新增金币掉落系统）──
    void CastPickGold(UnitBase target)
    {
        if (target == null) return;
        float dmg = attr.GetAttr(AttrType.Attack) * 0.9f;
        target.TakeDamage(dmg, false, false, false, facingDir, this);

        var vfx = BattleVFXSystem.Instance;
        if (vfx != null) vfx.PlayCritical(target.GetHitPosition(), target.transform, VfxFaction.Ally);
    }

    // ── 羊毛盾：玩家 + 全部出战佣兵各挂一层小盾（不做治疗）──
    void CastWoolShield()
    {
        const float ratio = 0.08f;    // 生命上限 8%
        const float duration = 6f;

        PlayerPassiveCombat.Instance?.ApplyTeamShieldFromActive(ratio, duration, 1f);
        var mercs = MercenaryManager.Instance?.GetActiveMercs();
        if (mercs != null)
        {
            for (int i = 0; i < mercs.Count; i++)
            {
                var m = mercs[i];
                if (m != null && m.PassiveRunner != null)
                    m.PassiveRunner.ApplyTeamShieldFromActive(ratio, duration, 1f);
            }
        }
        BattleUI.Instance?.RefreshShieldBars();

        var vfx = BattleVFXSystem.Instance;
        if (vfx != null)
        {
            var hero = Hero.Instance;
            if (hero != null && !hero.isDead) vfx.PlayShield(hero.GetHitPosition(), VfxFaction.Ally);
            vfx.PlayShield(GetHitPosition(), VfxFaction.Ally);
        }
    }

    // ── 火球术：朝目标放火球，落点周围小范围法伤 ──
    void CastFireball(UnitBase target)
    {
        if (target == null) return;
        var vfx = BattleVFXSystem.Instance;
        if (vfx != null) vfx.PlayFireball(GetFirePosition(), facingDir, VfxFaction.Ally);

        float dmg = attr.GetAttr(AttrType.Attack) * 1.4f;
        const float radius = 1.6f;
        Vector3 center = target.GetHitPosition();
        Vector3 targetPos = target.transform.position;

        var bm = BattleManager.Instance;
        if (bm != null && bm.monsters != null)
        {
            for (int i = 0; i < bm.monsters.Count; i++)
            {
                var m = bm.monsters[i];
                if (m == null || m.isDead) continue;
                if (Vector3.Distance(m.transform.position, targetPos) > radius) continue;
                m.TakeDamage(dmg, false, false, true, facingDir, this);
            }
        }

        if (vfx != null) vfx.PlayFireImpact(center, VfxFaction.Ally);
    }

    // ── 裂爪：三段近身挥爪 ──
    IEnumerator CoClawRush(UnitBase target)
    {
        float dmg = attr.GetAttr(AttrType.Attack) * 0.65f;
        var vfx = BattleVFXSystem.Instance;
        for (int i = 0; i < 3; i++)
        {
            if (target == null || target.isDead || _fled) yield break;
            PlayAnim("Attack");
            target.TakeDamage(dmg, false, false, true, facingDir, this);
            if (vfx != null) vfx.PlaySlash(target.GetHitPosition(), facingDir, VfxFaction.Ally);
            yield return new WaitForSeconds(0.12f);
        }
    }
}
