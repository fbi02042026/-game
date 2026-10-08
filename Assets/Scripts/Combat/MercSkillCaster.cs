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

    /// <summary>本技能的蓝条定位（无蓝 = 纯 CD，不参与 MP）。</summary>
    MpArchetype _mpType = MpArchetype.None;

    /// <summary>单次耗蓝（0 = 无蓝技能，不扣蓝）。</summary>
    float _mpCost = 0f;

    /// <summary>有蓝定位却取不到合法耗蓝值 → 拒绝释放（fail closed，Bind 时已 LogError）。</summary>
    bool _mpBlocked = false;

    public string ActiveSkillId => _activeSkillId;
    public float CooldownRemain => _cooldownRemain;
    public float CooldownTotal { get; private set; } = 8f;
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
        // 2026-09-26：技能 CD 延长 1 倍（GameConfig.SKILL_COOLDOWN_MUL）。
        // 佣兵 CD 来自 SkillRegistry（asset 或 merc_skills 表），只有这里一个出口，统一在这乘。
        CooldownTotal = (cfg != null && cfg.cooldown > 0f ? cfg.cooldown : 8f) * GameConfig.SKILL_COOLDOWN_MUL;
        // 开局按满冷却进场：先普攻，冷却走完才轮到第一发主动技（原为 0，进战瞬间就甩技能）
        _cooldownRemain = CooldownTotal;

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
        Debug.Log($"[MercSkillCaster] 绑定 {activeSkillId} 定位={_mpType} 单次耗蓝={_mpCost:0.#} 有效CD={CooldownTotal:0.#}s");
    }

    void Update()
    {
        if (_cooldownRemain > 0f)
            _cooldownRemain -= Time.deltaTime;
        if (_merc == null || _merc.isDead || string.IsNullOrEmpty(_activeSkillId)) return;
        // 眩晕/被控期间不自动施放（含引导「原地眩晕」的引导佣兵 H003 塔克）
        if (_merc.IsStunned || _merc.TutorialStunned) return;
        if (_cooldownRemain > 0f) return;
        if (BattleManager.Instance != null && !BattleManager.Instance.UnitsCanAct) return;
        TryCast();
    }

    public bool TryCast(bool manual = false)
    {
        if (_merc == null || _merc.isDead || string.IsNullOrEmpty(_activeSkillId)) return false;
        if (_cooldownRemain > 0f) return false;
        if (BattleManager.Instance == null) return false;
        if (_mpBlocked) return false;
        // 2026-10-06 主人拍板：蓝不足不能释放。MP 闸在施法之前，扣不到蓝就不放 —— CD 也不重置。
        if (_mpType != MpArchetype.None && !BattleManager.Instance.TrySpendMercMp(_merc, _mpCost)) return false;
        bool ok = BattleManager.Instance.TryCastMercActiveSkill(_merc, _activeSkillId, manual: false);
        if (ok)
            _cooldownRemain = CooldownTotal;
        return ok;
    }

    [Obsolete("语义陷阱：清零冷却 = 立刻可放技能，与 Bind() 的『满冷却进场，先普攻』语义相反；留着易被误调用破坏进战手感。2026-09-26 已确认零调用，若真需要请新建显式方法。")]
    public void ResetCooldown()
    {
        _cooldownRemain = 0f;
    }
}
