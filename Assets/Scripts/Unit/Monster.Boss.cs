using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

// 怪物单位 partial —— 2026-10-17 由 Monster.cs 纯位移拆出（无逻辑改动）
// 职责：Boss 二阶段切换、狂暴（Enrage）
public partial class Monster
{
    bool _bossPhase2Started;
    bool _bossPhaseShiftBusy;
    bool _enraged;

    public int GetBossPhase()
    {
        if (!_isBossUnit || attr == null) return 0;
        float maxHp = attr.GetAttr(AttrType.MaxHp);
        if (maxHp < 1f) return 1;
        return (currentHp / maxHp) <= GameConfig.BOSS_PHASE2_HP_RATIO ? 2 : 1;
    }

    void TryBeginBossPhase2()
    {
        if (!_isBossUnit || _bossPhase2Started || isDead) return;
        if (GetBossPhase() < 2) return;
        _bossPhase2Started = true;
        if (!_skillTelegraphing && !_bossPhaseShiftBusy)
            StartCoroutine(CoBossPhaseShift());
    }

    IEnumerator CoBossPhaseShift()
    {
        _bossPhaseShiftBusy = true;
        if (rb != null) rb.velocity = Vector2.zero;
        UIManager.Instance?.ShowToast("Boss 进入狂暴阶段！");
        Vector3 center = new Vector3(transform.position.x, GROUND_Y + 0.02f, transform.position.z);
        GameObject disc = CreateTelegraphDisc(center, 4.5f);
        float t = 0f;
        while (t < GameConfig.BOSS_PHASE_SHIFT_TELEGRAPH)
        {
            if (isDead) break;
            t += Time.deltaTime;
            yield return null;
        }
        if (disc != null) Destroy(disc);
        // 【2026-10-19 圆心锁定】伤害圆心 = 红圈位置（预警开始时锁定），不取砸下瞬间 Boss 当前位置
        if (!isDead)
            ApplySkillDamage(GetMainAttackStat() * 0.85f, 4.5f, null, center.x);
        _bossPhaseShiftBusy = false;
    }

    /// <summary>?????????????????????????/summary>
    // ===== Boss 狂暴机制：仅第 5 章起、总开关控制；只改攻击间隔 =====
    /// <summary>
    /// 运行期标志，不持久化。条件满足即进入狂暴一次：
    /// 是 Boss、未死、总开关开、章节≥阈值、当前/最大血量≤阈值。
    /// 恢复存档后本标志随怪物重建而重置，下一帧 Update 会按当前血量重新判定。
    /// </summary>
    void TryBeginEnrage()
    {
        if (!_isBossUnit || _enraged || isDead) return;
        if (!GameConfig.BOSS_ENRAGE_ENABLED) return;
        if (_chapter < GameConfig.BOSS_ENRAGE_MIN_CHAPTER) return;
        float maxHp = attr != null ? attr.GetAttr(AttrType.MaxHp) : 0f;
        if (maxHp < 1f) return;
        if (currentHp / maxHp > GameConfig.BOSS_ENRAGE_HP_RATIO) return;
        _enraged = true;
        UIManager.Instance?.ShowToast("Boss 进入狂暴！");
        // 视觉反馈：直接复用现有「受击」特效（PlayVictimHit），不新造特效系统
        if (BattleVFXSystem.Instance != null)
            BattleVFXSystem.Instance.PlayVictimHit(GetHitPosition(), false, GetVfxFacingDir());
    }

    /// <summary>狂暴：只把攻击间隔 ÷倍率（即攻速 ×倍率），不动伤害/属性缩放。</summary>
    protected override float GetAttackCooldown()
    {
        float cd = base.GetAttackCooldown();
        if (_enraged)
            cd /= GameConfig.BOSS_ENRAGE_ATK_SPEED_MUL;
        return cd;
    }
}
