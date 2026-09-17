using UnityEngine;

/// <summary>
/// 佣兵主动技冷却与自动释放调度（始终自动，无手动开关）。
/// </summary>
public class MercSkillCaster : MonoBehaviour
{
    Mercenary _merc;
    string _activeSkillId;
    float _cooldownRemain;

    public string ActiveSkillId => _activeSkillId;
    public float CooldownRemain => _cooldownRemain;
    public float CooldownTotal { get; private set; } = 8f;
    public bool HasActiveSkill => !string.IsNullOrEmpty(_activeSkillId);

    public void Bind(Mercenary merc, string activeSkillId)
    {
        _merc = merc;
        _activeSkillId = activeSkillId;
        var cfg = SkillRegistry.Instance != null ? SkillRegistry.Instance.Get(activeSkillId) : null;
        CooldownTotal = cfg != null && cfg.cooldown > 0f ? cfg.cooldown : 8f;
        // 开局按满冷却进场：先普攻，冷却走完才轮到第一发主动技（原为 0，进战瞬间就甩技能）
        _cooldownRemain = CooldownTotal;
    }

    void Update()
    {
        if (_cooldownRemain > 0f)
            _cooldownRemain -= Time.deltaTime;
        if (_merc == null || _merc.isDead || string.IsNullOrEmpty(_activeSkillId)) return;
        // 眩晕/被控期间不自动施放（含引导「原地眩晕」的小白）
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
        bool ok = BattleManager.Instance.TryCastMercActiveSkill(_merc, _activeSkillId, manual: false);
        if (ok)
            _cooldownRemain = CooldownTotal;
        return ok;
    }

    public void ResetCooldown()
    {
        _cooldownRemain = 0f;
    }
}
