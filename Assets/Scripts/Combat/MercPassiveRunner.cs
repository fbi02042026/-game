using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 佣兵被动技能运行时（SK002~SK019 等）。
/// </summary>
public class MercPassiveRunner : MonoBehaviour
{
    Mercenary _merc;
    string _passiveId;

    float _regenTimer;
    bool _lowHpAtkOn;
    bool _ironWillUsed;
    float _ironWillTimer;
    float _defBuffTimer;
    float _shieldAmount;
    float _shieldTimer;

    readonly Dictionary<UnitBase, BleedState> _bleeds = new Dictionary<UnitBase, BleedState>();
    readonly Dictionary<UnitBase, ArmorShredState> _shreds = new Dictionary<UnitBase, ArmorShredState>();
    readonly Dictionary<UnitBase, float> _fearTimers = new Dictionary<UnitBase, float>();

    struct BleedState
    {
        public int Stacks;
        public float Timer;
        public float Dps;
    }

    struct ArmorShredState
    {
        public int Stacks;
        public float Timer;
    }

    public void Bind(Mercenary merc, string passiveSkillId)
    {
        _merc = merc;
        _passiveId = passiveSkillId;
        ResetState();
    }

    void ResetState()
    {
        _regenTimer = 0f;
        _lowHpAtkOn = false;
        _ironWillUsed = false;
        _ironWillTimer = 0f;
        _defBuffTimer = 0f;
        _shieldAmount = 0f;
        _shieldTimer = 0f;
        _bleeds.Clear();
        _shreds.Clear();
        _fearTimers.Clear();
    }

    void Update()
    {
        if (_merc == null || _merc.isDead || string.IsNullOrEmpty(_passiveId)) return;
        float dt = Time.deltaTime;
        TickAlways(dt);
        TickBleeds(dt);
        TickShreds(dt);
        TickFear(dt);
        TickIronWill(dt);
        TickDefBuff(dt);
        TickShield(dt);
        TickLowHpAtk();
    }

    void TickAlways(float dt)
    {
        if (_passiveId != "SK012") return;
        _regenTimer -= dt;
        if (_regenTimer > 0f) return;
        _regenTimer = 1f;
        if (_merc.attr == null) return;
        float maxHp = _merc.attr.GetAttr(AttrType.MaxHp);
        float heal = maxHp * 0.01f;
        _merc.currentHp = Mathf.Min(maxHp, _merc.currentHp + heal);
    }

    void TickLowHpAtk()
    {
        if (_passiveId != "SK004" || _merc.attr == null) return;
        float ratio = _merc.currentHp / Mathf.Max(1f, _merc.attr.GetAttr(AttrType.MaxHp));
        bool should = ratio < 0.5f;
        if (should == _lowHpAtkOn) return;
        _lowHpAtkOn = should;
        _merc.attr.AddAttr(AttrType.Attack, should ? 0.25f : -0.25f, true);
    }

    public void OnBasicAttackHit(UnitBase target, float damage)
    {
        if (target == null || _merc == null || string.IsNullOrEmpty(_passiveId)) return;

        if (_passiveId == "SK002" && Random.value < 0.3f)
            ApplyBleed(target);

        if (_passiveId == "SK019")
            ApplyArmorShred(target);
    }

    public void OnDealMagicDamage(ref float damage)
    {
        if (_passiveId == "SK017")
            damage *= 1.15f;
    }

    public float ModifyIncomingDamage(float damage)
    {
        if (_merc == null || string.IsNullOrEmpty(_passiveId)) return damage;

        if (_passiveId == "SK006" && Random.value < 0.2f)
            damage *= 0.7f;

        if (_passiveId == "SK009" && _ironWillTimer > 0f)
            damage *= 0.6f;

        if (_shieldAmount > 0f && _shieldTimer > 0f)
        {
            float absorbed = Mathf.Min(_shieldAmount, damage);
            _shieldAmount -= absorbed;
            damage -= absorbed;
        }
        return damage;
    }

    public void OnHpChanged()
    {
        if (_passiveId != "SK009" || _ironWillUsed || _merc == null || _merc.attr == null) return;
        float ratio = _merc.currentHp / Mathf.Max(1f, _merc.attr.GetAttr(AttrType.MaxHp));
        if (ratio < 0.3f)
        {
            _ironWillUsed = true;
            _ironWillTimer = 5f;
        }
    }

    public void OnOwnerHealed(float amount)
    {
        if (_passiveId != "SK014" || amount <= 0f) return;
        // HoT on self when healing others — simplified: self regen burst
        if (_merc != null && _merc.attr != null)
        {
            float hot = _merc.attr.GetAttr(AttrType.Attack) * 0.3f * 3f;
            _merc.currentHp = Mathf.Min(_merc.attr.GetAttr(AttrType.MaxHp), _merc.currentHp + hot * 0.33f);
        }
    }

    public void ApplyTeamShieldFromActive(float ratio, float duration)
    {
        if (_merc == null || _merc.attr == null) return;
        _shieldAmount = _merc.attr.GetAttr(AttrType.MaxHp) * ratio;
        _shieldTimer = duration;
    }

    public void ApplySelfDefBuff(float duration)
    {
        _defBuffTimer = duration;
        if (_merc != null && _merc.attr != null)
            _merc.attr.AddAttr(AttrType.Defense, 0.35f, true);
    }

    public float ModifyTargetAttack(UnitBase target, float atk)
    {
        if (target == null) return atk;
        if (_fearTimers.TryGetValue(target, out float t) && t > 0f)
            return atk * 0.8f;
        return atk;
    }

    void ApplyBleed(UnitBase target)
    {
        if (!_bleeds.TryGetValue(target, out var st))
            st = new BleedState();
        st.Stacks = Mathf.Min(2, st.Stacks + 1);
        st.Timer = 3f;
        st.Dps = (_merc.attr != null ? _merc.attr.GetAttr(AttrType.Attack) : 10f) * 0.25f * st.Stacks;
        _bleeds[target] = st;
    }

    void ApplyArmorShred(UnitBase target)
    {
        if (!_shreds.TryGetValue(target, out var st))
            st = new ArmorShredState();
        st.Stacks = Mathf.Min(3, st.Stacks + 1);
        st.Timer = 5f;
        _shreds[target] = st;
        if (target.attr != null)
            target.attr.AddAttr(AttrType.Defense, -0.1f, true);
    }

    public void ApplyFearDebuff(UnitBase target, float duration)
    {
        if (target == null) return;
        _fearTimers[target] = duration;
    }

    void TickBleeds(float dt)
    {
        if (_bleeds.Count == 0) return;
        var keys = new List<UnitBase>(_bleeds.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            var target = keys[i];
            if (target == null || target.isDead)
            {
                _bleeds.Remove(target);
                continue;
            }
            var st = _bleeds[target];
            st.Timer -= dt;
            target.TakeDamage(st.Dps * dt, false, true);
            if (st.Timer <= 0f) _bleeds.Remove(target);
            else _bleeds[target] = st;
        }
    }

    void TickShreds(float dt)
    {
        if (_shreds.Count == 0) return;
        var keys = new List<UnitBase>(_shreds.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            var target = keys[i];
            if (target == null || target.isDead)
            {
                _shreds.Remove(target);
                continue;
            }
            var st = _shreds[target];
            st.Timer -= dt;
            if (st.Timer <= 0f) _shreds.Remove(target);
            else _shreds[target] = st;
        }
    }

    void TickFear(float dt)
    {
        if (_fearTimers.Count == 0) return;
        var keys = new List<UnitBase>(_fearTimers.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            var t = keys[i];
            if (t == null || t.isDead)
            {
                _fearTimers.Remove(t);
                continue;
            }
            float remain = _fearTimers[t] - dt;
            if (remain <= 0f) _fearTimers.Remove(t);
            else _fearTimers[t] = remain;
        }
    }

    void TickIronWill(float dt)
    {
        if (_ironWillTimer <= 0f) return;
        _ironWillTimer -= dt;
    }

    void TickDefBuff(float dt)
    {
        if (_defBuffTimer <= 0f) return;
        _defBuffTimer -= dt;
        if (_defBuffTimer <= 0f && _merc != null && _merc.attr != null)
            _merc.attr.AddAttr(AttrType.Defense, -0.35f, true);
    }

    void TickShield(float dt)
    {
        if (_shieldTimer <= 0f) return;
        _shieldTimer -= dt;
        if (_shieldTimer <= 0f) _shieldAmount = 0f;
    }
}
