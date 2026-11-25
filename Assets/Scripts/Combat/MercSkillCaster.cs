using System;
using UnityEngine;

/// <summary>
/// 佣兵主动技冷却与自动释放调度（始终自动，无手动开关）。
/// <para>2026-10-06 主人拍板：CD 之外再加一道 MP（蓝条）闸门 ——
/// 冷却好了还得蓝够才放；蓝不足<b>直接不放</b>，CD 不重置、不排队、不透支。</para>
/// </summary>
public class MercSkillCaster : MonoBehaviour
{
    Mercenary _merc;
    string _activeSkillId;
    float _cooldownRemain;
    /// <summary>CD 好但放不出去（无目标/蓝不足）时的重试节流（2026-10-23 主人拍板：0.25s）。</summary>
    float _retryTimer;
    const float RETRY_INTERVAL = 0.25f;

    /// <summary>本技能的蓝条定位（无蓝 = 纯 CD，不参与 MP）。</summary>
    MpArchetype _mpType = MpArchetype.None;

    /// <summary>单次耗蓝（0 = 无蓝技能，不扣蓝）。</summary>
    float _mpCost = 0f;

    /// <summary>有蓝定位却取不到合法耗蓝值 → 拒绝释放（fail closed，Bind 时已 LogError）。</summary>
    bool _mpBlocked = false;

    public string ActiveSkillId => _activeSkillId;
    public float CooldownRemain => _cooldownRemain;
    /// <summary>当前这一轮冷却的总时长（HUD 遮罩分母）。开场那轮是 MERC_SKILL_OPENING_CD，之后是技能固有 CD。</summary>
    public float CooldownTotal { get; private set; } = 8f;
    /// <summary>技能固有 CD（表值 × MERC_SKILL_COOLDOWN_MUL）。首发之后每轮都按它重置。</summary>
    float _normalCooldown = 8f;
    public bool HasActiveSkill => !string.IsNullOrEmpty(_activeSkillId);
    /// <summary>蓝条定位：UI 用它决定「这个槽要不要显示蓝条」。</summary>
    public MpArchetype MpType => _mpType;
    /// <summary>单次耗蓝。</summary>
    public float MpCost => _mpCost;

    public void Bind(Mercenary merc, string activeSkillId)
    {
        _merc = merc;
        _activeSkillId = activeSkillId;
        var cfg = SkillRegistry.Instance != null ? SkillRegistry.Instance.Get(activeSkillId) : null;
        // 佣兵 CD = 表值 × MERC_SKILL_COOLDOWN_MUL（维持历史 ×4，不跟随玩家侧回退到 1f）。
        // 佣兵 CD 来自 SkillRegistry（asset 或 merc_skills 表），只有这里一个出口，统一在这乘。
        _normalCooldown = (cfg != null && cfg.cooldown > 0f ? cfg.cooldown : 8f) * GameConfig.MERC_SKILL_COOLDOWN_MUL;
        // 2026-11-12 主人拍板：佣兵开场先走固定 5 秒冷却（原来直接灌满固有 CD，要等 24~160 秒）。
        // 首发那一轮的 HUD 分母也跟着用开场值，避免 5/(表值×4) 被显示成一条细缝。
        // 倒数仍吃 UnitsCanAct 闸（10-31 口径）→ 5 秒从点「开始战斗」解冻那刻算，不从进场算。
        ResetOpeningCooldown();

        // 2026-10-06：定位与耗蓝只在绑定时取一次，运行时不再查表
        _mpType = MpProfile.OfMercSkill(activeSkillId);
        _mpCost = 0f;
        _mpBlocked = false;
        if (_mpType != MpArchetype.None)
        {
            // 耗蓝唯一出口 MercSkillTable.MpCostOf：-1 = 表里查不到，0 = 该列没填
            float cost = MercSkillTable.MpCostOf(activeSkillId);
            if (cost < 0f)
            {
                Debug.LogError($"[MercSkillCaster] 技能 {activeSkillId} 在 merc_skills 表里查不到 → 拒绝释放（fail closed）");
                _mpBlocked = true;
            }
            else if (cost <= 0f)
            {
                Debug.LogError($"[MercSkillCaster] 技能 {activeSkillId} 是「{_mpType}」型（有蓝）但 merc_skills「耗蓝」列为空/0 → 拒绝释放（fail closed）");
                _mpBlocked = true;
            }
            else
            {
                _mpCost = cost;
            }
        }
        Debug.Log($"[MercSkillCaster] 绑定 {activeSkillId} 定位={_mpType} 单次耗蓝={_mpCost:0.#} 有效CD={_normalCooldown:0.#}s 开场CD={CooldownTotal:0.#}s");
    }

    /// <summary>
    /// 重新挂开场 CD（2026-11-12 主人拍板「第二关重新算，每关都先走 5 秒」）。
    /// Bind 与 MercenaryManager.ResetMercenaries 共用此出口；不重算 _normalCooldown（技能固有 CD 不随关卡变）。
    /// 只重置计时，不动 _mpType / _mpCost / _mpBlocked（绑定期常量）。
    /// </summary>
    public void ResetOpeningCooldown()
    {
        CooldownTotal = GameConfig.MERC_SKILL_OPENING_CD;
        _cooldownRemain = CooldownTotal;
        _retryTimer = 0f;
    }

    void Update()
    {
        // 【2026-10-31 主人拍板「佣兵进队别先走CD，开打后再走」】冷却倒数也吃 UnitsCanAct 闸：
        // 进场走位/剧情冻场/中途抽奖冻结期间 CD 不倒数（挂上的开场 CD 原样保留），
        // 解冻（开打）那一刻才开始走 —— 与普攻/施放的 UnitsCanAct 口径一致。
        // 战斗管理器不在场时同样不倒数（反正 TryCast 也放不出）。
        if (BattleManager.Instance == null || !BattleManager.Instance.UnitsCanAct) return;
        if (_cooldownRemain > 0f)
            _cooldownRemain -= Time.deltaTime;
        if (_merc == null || _merc.isDead || string.IsNullOrEmpty(_activeSkillId)) return;
        // 眩晕/被控期间不自动施放（含引导「原地眩晕」的引导佣兵 H003 塔克）
        if (_merc.IsStunned || _merc.TutorialStunned) return;
        if (_cooldownRemain > 0f) return;
        // 【2026-10-23 主人拍板】CD 好但放不出去（场上无怪/蓝不足）时按 0.25s 节流重试，
        // 不再每帧跑 MercSkillNeedsTarget 查表 + FindNearestEnemy 空转。
        // 只影响「放不出去」的局面；能放时 _retryTimer 恒为 0，首发时机与原来完全一致。
        if (_retryTimer > 0f)
        {
            _retryTimer -= Time.deltaTime;
            return;
        }
        if (!TryCast())
            _retryTimer = RETRY_INTERVAL;
    }

    public bool TryCast(bool manual = false)
    {
        return TryCastInternal(manual, false);
    }

    /// <summary>
    /// committed=true = 读条已满的真结算，跳过读条段直接扣蓝+放技能；
    /// committed=false = 首次尝试，治疗技走 1 秒读条，读满回调再以 true 重入。
    /// </summary>
    bool TryCastInternal(bool manual, bool committed)
    {
        if (_merc == null || _merc.isDead || string.IsNullOrEmpty(_activeSkillId)) return false;
        if (_cooldownRemain > 0f) return false;
        if (BattleManager.Instance == null) return false;
        if (_mpBlocked) return false;
        // 2026-10-18 主人拍板：攻击技在场上没有目标（无活怪）时不释放 ——
        // 不扣蓝、不进冷却，出怪后下一帧自动补发；治疗/增益/护盾类目标是自己人，不受此限。
        // 闸必须放在扣蓝之前：放后面的话没目标时返回 false，蓝已扣、CD 不重置，会每帧空扣蓝。
        if (MercSkillNeedsTarget(_activeSkillId) && _merc.FindNearestEnemy() == null) return false;

        // 【2026-11-25 主人报「路加站着不动、头顶条一截一截退涨」】两个「反正放不出去」的闸前置到读条之前：
        // ① 治疗技没人需要奶（全队 ≥92% 且无人引导眩晕）→ 结算必被拦；
        // ② 蓝不够 → 结算必被拦。
        // 这两条原来都在读满 1 秒之后才判：返回 false → CD 不进 → 0.25 秒重试 → 又读 1 秒，
        // 满血站桩就变成头顶条无限重来（每轮还白扣一次蓝）。前置后根本不启动读条。
        // 口径不在这里另写一份：MercHealHasPatient / HasMercMp 都是结算侧同一个出口。
        if (IsGatedHealActiveSkill() && !BattleManager.Instance.MercHealHasPatient(_merc)) return false;
        if (_mpType != MpArchetype.None && !BattleManager.Instance.HasMercMp(_merc, _mpCost)) return false;

        // 【2026-11-18 主人拍板】治疗技读满 1 秒才扣蓝 / 进 CD / 出特效 / 出左侧条。
        // 读条期间施法者定身（UnitBase.Update 里 HealCastBar.IsCasting 早退）、防重入。
        // 眩晕砸地 → 读条取消 → 回调不触发 → 不扣蓝、不进 CD。非治疗技（攻击/护盾/Buff）不读条。
        if (!committed && GameConfig.HEAL_CAST_SEC > 0f && IsHealActiveSkill())
        {
            if (HealCastBar.IsCasting(_merc)) return false;
            HealCastBar.Begin(_merc, () =>
            {
                if (_merc == null || _merc.isDead || _merc.IsStunned) return;
                TryCastInternal(manual, true);
            });
            return true;
        }
        // 2026-10-06 主人拍板：蓝不足不能释放。MP 闸在施法之前，扣不到蓝就不放 —— CD 也不重置。
        if (_mpType != MpArchetype.None && !BattleManager.Instance.TrySpendMercMp(_merc, _mpCost)) return false;
        bool ok = BattleManager.Instance.TryCastMercActiveSkill(_merc, _activeSkillId, manual: false);
        if (ok)
        {
            // 首发之后回到技能固有 CD（开场那 5 秒只走一轮）。
            CooldownTotal = _normalCooldown;
            _cooldownRemain = CooldownTotal;
        }
        return ok;
    }

    /// <summary>
    /// 当前主动技是否是「会吃 0.92 满血闸」的治疗技（单奶 / 群奶）。
    /// 团队治疗攻击技（TeamHealAtk）另算：它同时打伤害，全员满血也该放，不能被满血闸前置拦掉。
    /// </summary>
    bool IsGatedHealActiveSkill()
    {
        var kind = MercSkillExecutor.Resolve(_activeSkillId);
        return kind == MercSkillExecutor.Kind.HealSingle
            || kind == MercSkillExecutor.Kind.HealTeam;
    }

    /// <summary>当前主动技是否是治疗类（读条口径）：单奶 / 群奶 / 团队治疗攻击技。</summary>
    bool IsHealActiveSkill()
    {
        var kind = MercSkillExecutor.Resolve(_activeSkillId);
        return kind == MercSkillExecutor.Kind.HealSingle
            || kind == MercSkillExecutor.Kind.HealTeam
            || kind == MercSkillExecutor.Kind.TeamHealAtk;
    }

    // 【2026-10-23 主人拍板】[Obsolete] ResetCooldown 已删除：语义陷阱（清零冷却 = 立刻可放），确认零调用。

    /// <summary>
    /// 攻击类主动技（作用对象是敌人）才要求场上先有目标；治疗/增益/护盾类目标是自己人，不受此限。
    /// 分类口径与 SkillCastService.TryCastMercActiveSkill 的分派分支一致。
    /// </summary>
    static bool MercSkillNeedsTarget(string skillId)
    {
        var kind = MercSkillExecutor.Resolve(skillId);
        if (kind == MercSkillExecutor.Kind.HealSingle
            || kind == MercSkillExecutor.Kind.HealTeam
            || kind == MercSkillExecutor.Kind.SelfDefBuff
            || kind == MercSkillExecutor.Kind.TeamShield
            || kind == MercSkillExecutor.Kind.TeamHealAtk)
            return false;
        // 减益类（SK018 威慑凝视）作用对象是敌人，必须有目标 —— 不看 skillType
        //（2026-10-18 数值走表后它是 Buff 型，但照样不能在没怪时空放扣蓝）。
        if (kind == MercSkillExecutor.Kind.FearThenFallback)
            return true;
        var cfg = SkillRegistry.Instance != null ? SkillRegistry.Instance.Get(skillId) : null;
        return cfg == null || cfg.skillType != SkillSystem.SkillType.Buff;
    }
}
