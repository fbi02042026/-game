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
        // 【2026-10-20 修复「Boss 打到一半僵直」】旧版先置 _bossPhase2Started 再判断能不能起手：
        // 血量跨过 70% 那一刻若正在放技能预警（_skillTelegraphing=true），标志已被置 true
        // 但协程没启动 → 二阶段切换永久丢失。改为：本轮起不了手就不置位，
        // 由 Monster.Update 每帧重查（与 TryBeginEnrage 同口径），预警结束的下一帧补起手。
        // 【2026-11-16】只拦「地上已经铺着攒槽红圈」的攒槽（近战 Boss）：这时插狂暴砸地会两套圈叠在一起。
        // 远程 Boss / 没技能的 Boss 不建圈（_hpChargeDisc 恒 null），阶段切换逐字不变，不会被攒槽拖住。
        if (_skillTelegraphing || _bossPhaseShiftBusy || _hpChargeDisc != null) return;
        _bossPhase2Started = true;
        StartCoroutine(CoBossPhaseShift());
    }

    IEnumerator CoBossPhaseShift()
    {
        _bossPhaseShiftBusy = true;
        GameObject disc = null;
        GameObject progressDisc = null;
        Vector3 baseScale = Vector3.one;
        try
        {
            if (rb != null) rb.velocity = Vector2.zero;
            UIManager.Instance?.ShowToast("Boss 进入狂暴阶段！");
            Vector3 center = new Vector3(transform.position.x, GROUND_Y + 0.02f, transform.position.z);
            baseScale = CreateTelegraphPair(center, GameConfig.TELEGRAPH_BOSS_RADIUS, out disc, out progressDisc);
            float t = 0f;
            float frozenReal = 0f;   // 【2026-10-20 保险】连续冻场真实时间累计
            // 冻场感知与技能预警同口径（2026-10-16）：UnitsCanAct=false 期间不倒数，
            // 红圈原地等，解冻才继续 —— 也修掉「对白冻场中照样倒数、到点隔着对白砸人」。
            while (t < GameConfig.TELEGRAPH_BOSS_SEC
                   || (BattleManager.Instance != null && !BattleManager.Instance.UnitsCanAct))
            {
                if (isDead || this == null) yield break;   // 收尾统一走 finally
                if (BattleManager.Instance == null || BattleManager.Instance.UnitsCanAct)
                {
                    t += Time.deltaTime;
                    // 【2026-11-09】深色进度圈：从 0 放大到满范围，用时＝预警时长
                    if (progressDisc != null)
                    {
                        float grow = Mathf.Clamp01(t / GameConfig.TELEGRAPH_BOSS_SEC);
                        progressDisc.transform.localScale = new Vector3(baseScale.x * grow, baseScale.y * grow, 1f);
                    }
                    frozenReal = 0f;
                }
                else
                {
                    // 【保险】冻场超上限：放弃本次阶段切换（走 finally 清标志+销毁红圈），不砸地
                    frozenReal += Time.unscaledDeltaTime;
                    if (frozenReal >= SkillFrozenBailoutSec)
                    {
                        Debug.LogWarning($"[Monster.Boss] 阶段切换冻场超 {SkillFrozenBailoutSec}s，保险中断（放弃本次砸地）");
                        yield break;
                    }
                }
                yield return null;
            }
            if (disc != null) { Destroy(disc); disc = null; }
            if (progressDisc != null) { Destroy(progressDisc); progressDisc = null; }
            // 【2026-10-19 圆心锁定】伤害圆心 = 红圈位置（预警开始时锁定），不取砸下瞬间 Boss 当前位置
            if (!isDead)
                ApplySkillDamage(GetMainAttackStat() * 0.85f, GameConfig.TELEGRAPH_BOSS_RADIUS, null, center);
        }
        finally
        {
            // 【2026-10-20 保险】协程任何路径退出（冻场中被杀/被停协程/正常收尾）都清标志+销毁红圈，
            // 绝不让 _bossPhaseShiftBusy 卡死成永久站桩（参照 2026-10-17 波次闸门 try/finally 教训）
            if (disc != null) Destroy(disc);
            if (progressDisc != null) Destroy(progressDisc);
            _bossPhaseShiftBusy = false;
        }
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

    /// <summary>
    /// 【2026-11-16 主人「boss 血量越少 CD 越短、攻击越快，就是狂暴状态的数值」】
    /// 从 BOSS_ENRAGE_HP_RATIO 血线开始，随血量线性加速：血线处 = 原速（1.0），
    /// 血量见底 = BOSS_ENRAGE_ATK_SPEED_MUL（默认 1.5）倍速；血线以上不变。
    /// 复用原有狂暴数值与章节/总开关闸（BOSS_ENRAGE_ENABLED、BOSS_ENRAGE_MIN_CHAPTER），不另起一套。
    /// </summary>
    float HpScaledAttackSpeedMul()
    {
        if (!_isBossUnit || attr == null) return 1f;
        if (!GameConfig.BOSS_ENRAGE_ENABLED) return 1f;
        if (_chapter < GameConfig.BOSS_ENRAGE_MIN_CHAPTER) return 1f;
        float maxHp = attr.GetAttr(AttrType.MaxHp);
        if (maxHp < 1f) return 1f;
        float hp01 = Mathf.Clamp01(currentHp / maxHp);
        float startRatio = Mathf.Max(0.01f, GameConfig.BOSS_ENRAGE_HP_RATIO);
        if (hp01 >= startRatio) return 1f;
        float t = 1f - hp01 / startRatio;          // 血线处 0 → 血空 1
        return Mathf.Lerp(1f, GameConfig.BOSS_ENRAGE_ATK_SPEED_MUL, t);
    }

    /// <summary>狂暴：攻击间隔随时血量缩短（原「过线一次性 ×1.5」改为连续曲线，见 HpScaledAttackSpeedMul）。</summary>
    protected override float GetAttackCooldown()
    {
        float cd = base.GetAttackCooldown();
        float mul = HpScaledAttackSpeedMul();
        if (mul > 1.0001f) cd /= mul;
        return cd;
    }
}
