using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 佣兵被动运行时——【调度器】。
/// 2026-09-26 主人拍板：被动逻辑按 SK0xx 拆到 Assets/Scripts/Combat/Passives/* 各模块类
/// （SK002_Bleed / SK006_Block / SK009_IronWill / SK011_Regen / SK012_Fury /
/// SK014_LifeBloom / SK017_MagicSurge / SK019_ArmorShred），本类只做分发 +
/// 持有「共享状态」（团队护盾层 / 恐惧减益 / 自身防御 buff——这些来自主动技、与被动无关，
/// 故留在调度器，见各字段注释）。
///
/// ⚠️ 对外 public API 签名与行为不变（这是防连带 bug 的关键）：
/// Bind / OnBasicAttackHit / OnDealMagicDamage / ModifyIncomingDamage / OnHpChanged /
/// OnOwnerHealed / ApplyTeamShieldFromActive / ApplySelfDefBuff / ModifyTargetAttack /
/// ApplyFearDebuff / ShieldAmount / ShieldMax。
/// 外部调用方（Mercenary.cs / UnitBase.cs / SkillCastService.cs）无需改动。
/// </summary>
public class MercPassiveRunner : MonoBehaviour
{
    Mercenary _merc;
    string _passiveId;
    MercPassiveModule _module;

    // —— 以下为「共享状态」：不是某被动私有，而是主动技落到本佣兵上的效果层 ——
    float _defBuffTimer;                       // SK007 坚守：自身防御 buff 计时
    float _shieldAmount;                       // 团队护盾层（来自 SK008/holy_barrier 等）
    float _shieldTimer;
    float _shieldMax;                          // 护盾上限（2026-09-26 主人拍板补上，勿丢）
    readonly Dictionary<UnitBase, float> _fearTimers = new Dictionary<UnitBase, float>(); // SK018 恐惧减益

    /// <summary>本佣兵单元（供模块访问 _merc）。</summary>
    public Mercenary Merc { get { return _merc; } }

    public void Bind(Mercenary merc, string passiveSkillId)
    {
        _merc = merc;
        _passiveId = passiveSkillId;
        _module = CreateModule(_passiveId);
        if (_module != null) _module.Runner = this;
        // 2026-09-26 主人拍板：数值走表，模块只管模式。查 merc_skills 表行灌给模块 Configure；
        // 表缺失时模块用自身默认硬编码值兜底，行为不变（对外 API 不变）。
        if (_module != null && MercSkillTable.TryGet(_passiveId, out var row))
            _module.Configure(row);
        ResetState();
    }

    void ResetState()
    {
        _defBuffTimer = 0f;
        _shieldAmount = 0f;
        _shieldMax = 0f;
        _shieldTimer = 0f;
        _fearTimers.Clear();
    }

    void Update()
    {
        // 原版：_passiveId 为空时整体早退，共享 tick（恐惧/防御buff/护盾）也一并跳过——这里完全等价保留
        if (_merc == null || _merc.isDead || string.IsNullOrEmpty(_passiveId)) return;
        float dt = Time.deltaTime;
        if (_module != null) _module.OnUpdate(dt); // 被动私有 tick（SK002/SK009/SK011/SK012/SK019…）
        TickFear(dt);     // 共享：SK018 恐惧减益计时
        TickDefBuff(dt);  // 共享：SK007 自身防御 buff 计时
        TickShield(dt);   // 共享：团队护盾层计时
    }

    // === 被动事件转发（签名不变）===
    public void OnBasicAttackHit(UnitBase target, float damage)
    {
        if (target == null || _merc == null || string.IsNullOrEmpty(_passiveId)) return;
        if (_module != null) _module.OnBasicAttackHit(target, damage);
    }

    public void OnDealMagicDamage(ref float damage)
    {
        if (_module != null) _module.OnDealMagicDamage(ref damage);
    }

    public float ModifyIncomingDamage(float damage)
    {
        if (_merc == null || string.IsNullOrEmpty(_passiveId)) return damage;
        float dmg = damage;
        if (_module != null) _module.ModifyIncomingDamage(ref dmg); // SK006 格挡 / SK009 铁意
        // 共享：团队护盾层 1:1 抵扣，必须在被动减伤之后（顺序与旧版一致）
        if (_shieldAmount > 0f && _shieldTimer > 0f)
        {
            float absorbed = Mathf.Min(_shieldAmount, dmg);
            _shieldAmount -= absorbed;
            dmg -= absorbed;
        }
        return dmg;
    }

    public void OnHpChanged()
    {
        if (_module != null) _module.OnHpChanged();
    }

    public void OnOwnerHealed(float amount)
    {
        if (_module != null) _module.OnOwnerHealed(amount);
    }

    // === 共享：团队护盾层（来自主动技 SK008 / holy_barrier，数值由 MercSkillExecutor.TeamShieldParams 传入）===
    public void ApplyTeamShieldFromActive(float ratio, float duration)
    {
        if (_merc == null || _merc.attr == null) return;
        _shieldAmount = _merc.attr.GetAttr(AttrType.MaxHp) * ratio;
        _shieldMax = _shieldAmount;
        _shieldTimer = duration;
    }

    /// <summary>佣兵护盾当前值（到期读成 0）。只给盾条读，不改战斗数值。</summary>
    public float ShieldAmount => (_shieldAmount > 0f && _shieldTimer > 0f) ? _shieldAmount : 0f;

    /// <summary>佣兵护盾上限。没盾时 0，盾条据此隐藏。</summary>
    public float ShieldMax => ShieldAmount > 0f ? Mathf.Max(1f, _shieldMax) : 0f;

    // === 共享：自身防御 buff（SK007 坚守）===
    public void ApplySelfDefBuff(float duration)
    {
        _defBuffTimer = duration;
        if (_merc != null && _merc.attr != null)
            _merc.attr.AddAttr(AttrType.Defense, 0.35f, true);
    }

    // === 共享：恐惧减益（SK018 威慑凝视）===
    public float ModifyTargetAttack(UnitBase target, float atk)
    {
        if (target == null) return atk;
        if (_fearTimers.TryGetValue(target, out float t) && t > 0f)
            return atk * 0.8f;
        return atk;
    }

    public void ApplyFearDebuff(UnitBase target, float duration)
    {
        if (target == null) return;
        _fearTimers[target] = duration;
    }

    // === 共享 tick ===
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

    // === 模块工厂（按 passiveId 选模块；未识别返回 null，行为与旧版一致）===
    static MercPassiveModule CreateModule(string id)
    {
        switch (id)
        {
            case "SK002": return new SK002_Bleed();
            case "SK006": return new SK006_Block();
            case "SK009": return new SK009_IronWill();
            case "SK011": return new SK011_Regen();
            case "SK012": return new SK012_Fury();
            case "SK014": return new SK014_LifeBloom();
            case "SK017": return new SK017_MagicSurge();
            case "SK019": return new SK019_ArmorShred();
            default: return null;
        }
    }
}
