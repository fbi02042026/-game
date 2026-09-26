using UnityEngine;

/// <summary>
/// 玩家职业常驻被动战斗逻辑——【调度器】。
/// 2026-09-26 主人拍板：按 PS0xx 拆到 Assets/Scripts/Combat/Passives/* 各模块类
/// （PS001_Aura / PS004_Stun / PS005_Berserk / PS008_Ranger / PS009_Mage / PS012_Priest），
/// 本类只做分发 + 持有「团队护盾层」（来自佣兵主动技 SK008/holy_barrier，与玩家被动无关，留在调度器）。
///
/// ⚠️ 对外 public 成员签名与行为不变（防连带 bug 关键）：
/// Instance / EnsureOn / BindForBattle / OnHeroCritHit / GetAllyIncomingDamageMul /
/// ApplyTeamShieldFromActive / TeamShieldAmount / TeamShieldMax / AbsorbTeamShield / GetAttackSpeedMul。
/// 外部调用方（UnitBase.cs / BattleUI.CharacterBar.cs / PlayerJobDefs.cs / SkillCastService.cs）无需改动。
/// </summary>
public class PlayerPassiveCombat : MonoBehaviour
{
    public static PlayerPassiveCombat Instance { get; private set; }

    string _passiveId;
    PlayerJobId _job;
    PlayerPassiveModule _module;

    // 团队护盾层（佣兵主动技给玩家那一层；数值来自 merc_skills 表，不在此写死）
    float _teamShieldAmount;
    float _teamShieldUntil;
    float _teamShieldMax; // 护盾上限（2026-09-26 主人拍板补上，勿丢）

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
        _module = CreateModule(_passiveId);
        if (_module != null) _module.Runner = this;

        if (PlayerPassiveTables.TryGetPrimary(_job, out var row) && _module != null)
            _module.Configure(row);

        _teamShieldAmount = 0f;
        _teamShieldMax = 0f;
        _teamShieldUntil = 0f;
    }

    void Update()
    {
        if (Hero.Instance == null || Hero.Instance.isDead) return;
        if (BattleManager.Instance == null || !BattleManager.Instance.isInBattle) return;
        if (string.IsNullOrEmpty(_passiveId)) return;
        if (_module != null) _module.OnUpdate();
    }

    /// <summary>暴击命中后由 UnitBase 调用。</summary>
    public void OnHeroCritHit(UnitBase target)
    {
        if (target == null || target.isDead) return;
        if (_module != null) _module.OnHeroCritHit(target);
    }

    /// <summary>友军受击减伤倍率（1=无减）。</summary>
    public float GetAllyIncomingDamageMul(UnitBase victim)
    {
        if (_module != null) return _module.GetAllyIncomingDamageMul(victim);
        return 1f;
    }

    /// <summary>
    /// 团队护盾（佣兵主动技）落到玩家身上：按比例吸收、到期作废，后放的覆盖先放的（不叠加）。
    /// ratio/duration 由 MercSkillExecutor.TeamShieldParams 从 merc_skills 表解析，此处不发明数值。
    /// </summary>
    public void ApplyTeamShieldFromActive(float ratio, float duration)
    {
        var hero = Hero.Instance;
        if (hero == null || hero.attr == null || duration <= 0f) return;
        float amount = hero.attr.GetAttr(AttrType.MaxHp) * Mathf.Max(0f, ratio);
        if (amount <= 0f) return;
        _teamShieldAmount = amount;
        _teamShieldMax = amount;
        _teamShieldUntil = Time.time + duration;
    }

    /// <summary>玩家身上团队护盾的当前值（过期一律读成 0）。只给盾条读，不改战斗数值。</summary>
    public float TeamShieldAmount
        => (_teamShieldAmount > 0f && Time.time < _teamShieldUntil) ? _teamShieldAmount : 0f;

    /// <summary>玩家身上团队护盾的上限（= 施加时的生命上限 × ratio）。没盾时 0，盾条据此隐藏。</summary>
    public float TeamShieldMax => TeamShieldAmount > 0f ? Mathf.Max(1f, _teamShieldMax) : 0f;

    /// <summary>用玩家身上的团队护盾吸收伤害，返回吸收后的剩余伤害（护盾耗尽/过期则原样返回）。</summary>
    public float AbsorbTeamShield(float damage)
    {
        if (_teamShieldAmount <= 0f || Time.time >= _teamShieldUntil)
        {
            _teamShieldAmount = 0f;
            return damage;
        }
        float absorbed = Mathf.Min(_teamShieldAmount, damage);
        _teamShieldAmount -= absorbed;
        return damage - absorbed;
    }

    public float GetAttackSpeedMul()
    {
        if (_module != null) return _module.GetAttackSpeedMul();
        return 1f;
    }

    /// <summary>团队治疗（PS012 牧师）共用的静音治疗 helper，保持原实现不动。</summary>
    public static void HealUnit(UnitBase unit, float heal)
    {
        if (unit == null || unit.isDead || unit.attr == null) return;
        float before = unit.currentHp;
        float maxHp = unit.attr.GetAttr(AttrType.MaxHp);
        unit.currentHp = Mathf.Min(maxHp, unit.currentHp + heal);
        int gained = Mathf.RoundToInt(unit.currentHp - before);
        if (gained > 0)
            DamageTextSystem.Instance?.SpawnHealText(unit.GetHitPosition(), gained);
    }

    // === 模块工厂（按职业被动 id 选模块；未识别返回 null，行为与旧版一致）===
    static PlayerPassiveModule CreateModule(string id)
    {
        switch (id)
        {
            case "PS001": return new PS001_Aura();
            case "PS004": return new PS004_Stun();
            case "PS005": return new PS005_Berserk();
            case "PS008": return new PS008_Ranger();
            case "PS009": return new PS009_Mage();
            case "PS012": return new PS012_Priest();
            default: return null;
        }
    }
}
