using UnityEngine;

/// <summary>
/// 佣兵主动技冷却与自动/手动释放调度。
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
        _cooldownRemain = 0f;
        var cfg = SkillRegistry.Instance != null ? SkillRegistry.Instance.Get(activeSkillId) : null;
        CooldownTotal = cfg != null && cfg.cooldown > 0f ? cfg.cooldown : 8f;
    }

    void Update()
    {
        if (_cooldownRemain > 0f)
            _cooldownRemain -= Time.deltaTime;
        if (!MercSkillMigrate.IsMercSkillAutoCast()) return;
        if (_merc == null || _merc.isDead || string.IsNullOrEmpty(_activeSkillId)) return;
        if (_cooldownRemain > 0f) return;
        if (BattleManager.Instance != null && !BattleManager.Instance.UnitsCanAct) return;
        TryCast();
    }

    public bool TryCast(bool manual = false)
    {
        if (_merc == null || _merc.isDead || string.IsNullOrEmpty(_activeSkillId)) return false;
        if (_cooldownRemain > 0f) return false;
        if (BattleManager.Instance == null) return false;
        bool ok = BattleManager.Instance.TryCastMercActiveSkill(_merc, _activeSkillId, manual);
        if (ok)
            _cooldownRemain = CooldownTotal;
        return ok;
    }

    public void ResetCooldown()
    {
        _cooldownRemain = 0f;
    }
}
