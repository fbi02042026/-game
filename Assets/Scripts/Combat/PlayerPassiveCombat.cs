using UnityEngine;

/// <summary>
/// 玩家职业常驻被动战斗逻辑（每职业一个主被动）。
/// </summary>
public class PlayerPassiveCombat : MonoBehaviour
{
    public static PlayerPassiveCombat Instance { get; private set; }

    string _passiveId;
    PlayerJobId _job;
    float _rangerAtkSpdUntil;
    float _rangerAtkSpdMul = 1.25f;
    float _priestTick;
    float _priestInterval = 5f;
    float _priestHealPct = 0.05f;
    float _shieldAuraPct = 0.15f;
    float _shieldAuraPx = 120f;
    float _mageAtkBonusPct = 0.20f;
    float _mageStillThreshold = 0.05f;
    float _heavyStunSec = 1f;
    bool _mageStandingBonus;
    Vector3 _lastPos;
    float _stillTimer;
    float _berserkBonusApplied;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static void EnsureOn(Hero hero)
    {
        if (hero == null) return;
        var c = hero.GetComponent<PlayerPassiveCombat>();
        if (c == null) c = hero.gameObject.AddComponent<PlayerPassiveCombat>();
        c.BindForBattle();
    }

    public void BindForBattle()
    {
        _job = PlayerJobDefs.GetSelected();
        _passiveId = PlayerPassiveTables.GetPrimaryPassiveId(_job);
        if (PlayerPassiveTables.TryGetPrimary(_job, out var row))
        {
            if (row.ValueNumber > 0.01f)
            {
                float v = row.ValueNumber;
                if (v > 1f && (row.ValueRaw.Contains("%") || v >= 5f))
                    v *= 0.01f;
                switch (_passiveId)
                {
                    case "PS001": _shieldAuraPct = v; break;
                    case "PS008": _rangerAtkSpdMul = 1f + v; break;
                    case "PS009": _mageAtkBonusPct = v; break;
                    case "PS012": _priestHealPct = v; break;
                    case "PS004":
                        if (row.CooldownSeconds > 0.01f) _heavyStunSec = row.CooldownSeconds;
                        else if (row.ValueNumber > 0.01f && row.ValueNumber < 10f)
                            _heavyStunSec = row.ValueNumber;
                        break;
                }
            }
            if (row.CooldownSeconds > 0.01f)
            {
                if (_passiveId == "PS008") { /* duration from cooldown col = 3 */ }
                else if (_passiveId == "PS012") _priestInterval = row.CooldownSeconds;
            }
            // PS008: 冷却列=3秒持续时间
            if (_passiveId == "PS008" && row.CooldownSeconds > 0.01f)
                _rangerBuffDuration = row.CooldownSeconds;
        }
        _lastPos = transform.position;
        _stillTimer = 0f;
        _mageStandingBonus = false;
        _berserkBonusApplied = 0f;
        _priestTick = 0f;
        _rangerAtkSpdUntil = 0f;
    }

    float _rangerBuffDuration = 3f;

    void Update()
    {
        if (Hero.Instance == null || Hero.Instance.isDead) return;
        if (BattleManager.Instance == null || !BattleManager.Instance.isInBattle) return;
        if (string.IsNullOrEmpty(_passiveId)) return;

        switch (_passiveId)
        {
            case "PS005": TickBerserk(); break;
            case "PS009": TickMageStand(); break;
            case "PS012": TickPriestHeal(); break;
        }
    }

    /// <summary>暴击命中后由 UnitBase 调用。</summary>
    public void OnHeroCritHit(UnitBase target)
    {
        if (target == null || target.isDead) return;
        if (_passiveId == "PS004")
            target.ApplyStun(_heavyStunSec);
        else if (_passiveId == "PS008")
            _rangerAtkSpdUntil = Time.time + _rangerBuffDuration;
    }

    /// <summary>友军受击减伤倍率（1=无减）。</summary>
    public float GetAllyIncomingDamageMul(UnitBase victim)
    {
        if (_passiveId != "PS001" || victim == null || !victim.isAlly) return 1f;
        var hero = Hero.Instance;
        if (hero == null || hero.isDead) return 1f;
        float maxDist = _shieldAuraPx / GameConfig.PIXEL_PER_UNIT;
        if (Vector3.Distance(hero.transform.position, victim.transform.position) > maxDist)
            return 1f;
        return Mathf.Clamp01(1f - _shieldAuraPct);
    }

    public float GetAttackSpeedMul()
    {
        if (_passiveId == "PS008" && Time.time < _rangerAtkSpdUntil)
            return _rangerAtkSpdMul;
        return 1f;
    }

    void TickBerserk()
    {
        var hero = Hero.Instance;
        if (hero?.attr == null) return;
        float maxHp = Mathf.Max(1f, hero.attr.GetAttr(AttrType.MaxHp));
        float missing = 1f - Mathf.Clamp01(hero.currentHp / maxHp);
        float stacks = Mathf.Floor(missing / 0.10f);
        float bonusPct = Mathf.Min(0.30f, stacks * 0.03f);
        float delta = bonusPct - _berserkBonusApplied;
        if (Mathf.Abs(delta) < 0.0001f) return;
        // 用百分比攻击加成叠在 AttrSystem 上：相对当前攻击调整
        float atk = hero.attr.GetAttr(AttrType.Attack);
        float baseWithout = atk / Mathf.Max(0.01f, 1f + _berserkBonusApplied);
        hero.attr.SetAttr(AttrType.Attack, baseWithout * (1f + bonusPct));
        _berserkBonusApplied = bonusPct;
    }

    void TickMageStand()
    {
        var hero = Hero.Instance;
        if (hero?.attr == null) return;
        Vector3 p = hero.transform.position;
        float moved = Vector3.Distance(p, _lastPos);
        _lastPos = p;
        if (moved > _mageStillThreshold)
        {
            _stillTimer = 0f;
            if (_mageStandingBonus)
            {
                float atk = hero.attr.GetAttr(AttrType.Attack);
                hero.attr.SetAttr(AttrType.Attack, atk / (1f + _mageAtkBonusPct));
                _mageStandingBonus = false;
            }
            return;
        }
        _stillTimer += Time.deltaTime;
        if (!_mageStandingBonus && _stillTimer >= 0.15f)
        {
            float atk = hero.attr.GetAttr(AttrType.Attack);
            hero.attr.SetAttr(AttrType.Attack, atk * (1f + _mageAtkBonusPct));
            _mageStandingBonus = true;
        }
    }

    void TickPriestHeal()
    {
        _priestTick += Time.deltaTime;
        if (_priestTick < _priestInterval) return;
        _priestTick = 0f;
        var hero = Hero.Instance;
        if (hero?.attr == null) return;
        float heal = hero.attr.GetAttr(AttrType.MaxHp) * _priestHealPct;
        var bm = BattleManager.Instance;
        if (bm == null) return;
        HealUnit(hero, heal);
        var mercs = MercenaryManager.Instance?.GetActiveMercs();
        if (mercs == null) return;
        for (int i = 0; i < mercs.Count; i++)
        {
            var m = mercs[i];
            if (m == null || m.isDead || m.attr == null) continue;
            HealUnit(m, m.attr.GetAttr(AttrType.MaxHp) * _priestHealPct);
        }
    }

    static void HealUnit(UnitBase unit, float heal)
    {
        if (unit == null || unit.isDead || unit.attr == null) return;
        float before = unit.currentHp;
        float maxHp = unit.attr.GetAttr(AttrType.MaxHp);
        unit.currentHp = Mathf.Min(maxHp, unit.currentHp + heal);
        int gained = Mathf.RoundToInt(unit.currentHp - before);
        if (gained > 0)
            DamageTextSystem.Instance?.SpawnHealText(unit.GetHitPosition(), gained);
    }
}
