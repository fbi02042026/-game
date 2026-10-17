using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// ????Monstersmoban ???????? + ??????????MonsterBody ??????????
/// ????
///   MonsterBody????Rigidbody2D??????
///   ??? Visual / Monstersmoban?Animator?HPBar?Monsters ???beattack/fire??
/// 拆分（2026-10-17 纯位移、无逻辑改动）：
///   Monster.AI.cs（入场走位/索敌/交战）、Monster.Init.cs（出生/贴图/锚点/光环）、
///   Monster.Skills.cs（主动技能/攻击风格）、Monster.Boss.cs（二阶段/狂暴）、
///   Monster.Visual.cs（血条/排序/足印/阴影）、Monster.StackLabel.cs（叠怪标签）
/// </summary>
public partial class Monster : UnitBase
{
    public float goldDrop;
    public int expDrop;
    public MonsterConfig config;
    private int _chapter;

    Transform _bodyRoot;
    Transform _visualRoot;

    /// <summary>???????? Body????Body ?????????/summary>
    public Transform GetBodyTransform() => _bodyRoot != null ? _bodyRoot : transform;
    Transform MoveRoot => GetBodyTransform();
    protected override Transform LaneMoveTransform => GetBodyTransform();

    protected override void Awake()
    {
        // ????????????? ?4+????0.5 ??????????????????
        float rootAbs = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.y));
        hitPointOffset = new Vector3(0f, 0.55f / rootAbs, 0f);
        firePointOffset = new Vector3(-0.08f / rootAbs, 0.55f / rootAbs, 0f);

        base.Awake();
        isAlly = false;
        // 2D Pixel RPG Monster Pack ??????
        spriteDefaultFacesRight = false;

        // ????????Rigidbody2D??????
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0;
            rb.freezeRotation = true;
        }

        // SpriteRenderer ??"Monsters" ????
        if (sr == null)
        {
            Transform monstersChild = transform.Find("Monsters");
            if (monstersChild != null)
                sr = monstersChild.GetComponent<SpriteRenderer>();
        }

        // ??????????SpriteRenderer ???
        FindHPBar();
    }

    private MonsterAttackStyle _attackStyle = MonsterAttackStyle.Melee;

    private bool _isBossUnit;
    private bool _eliteWave;
    // 2026-09-26 主人拍板：本怪物是魔法型(true)还是物理型(false)，攻击/防御二选一不混搭。
    // 该值在 Init 时按 monster_attack_style.csv 的 magicChance 掷骰一次后固定，终身不变。
    private bool _isMagicType;
    /// <summary>本怪物随机判定出的物理/魔法类型（Init 掷一次后固定）。供掉落按类型区分时读取。</summary>
    public bool IsMagicType => _isMagicType;
    bool _eliteGlass;

    public bool IsBossUnit => _isBossUnit;
    public bool IsEliteWave => _eliteWave;
    public bool IsEliteGlass => _eliteGlass;

    /// <summary>
    /// 是否魔法伤害单位（主人 2026-09-26 口径）：法球（MonsterAttackStyle.Ranged）打的是魔法伤害，
    /// 走目标的魔法防御；近战 / 弓走物理防御。小怪、精英、Boss 一视同仁 ——
    /// 主人明确「不止小怪，精英和 Boss 也是魔法伤害的单位」。
    /// </summary>
    // 2026-09-26 主人拍板：伤害类型随「随机判定出的物理/魔法类型」走（与属性路由一致），不再看 style。
    public override bool IsMagicDamageDealer() => _isMagicType;

    /// <summary>2026-09-26 主人拍板：魔法型取魔攻、物理型取物攻（技能 / 阶段技结算用，避免魔法怪 Attack=0 导致技能打不出伤害）。</summary>
    float GetMainAttackStat()
    {
        return _isMagicType ? attr.GetAttr(AttrType.MagicAttack) : attr.GetAttr(AttrType.Attack);
    }

    public override void TakeDamage(float damage, bool isCrit, bool ignoreDefense = false, bool showHitVfx = true, int hitVfxFacing = 0, UnitBase source = null, bool suppressDamageText = false)
    {
        // V6 词缀「铁壁」：减伤在扣血之前生效
        var affix = MonsterAffix.Get(this);
        float inDamage = damage;
        if (affix != null && affix.DamageTakenMul > 0f && !Mathf.Approximately(affix.DamageTakenMul, 1f))
            inDamage = damage * affix.DamageTakenMul;

        // 天赋「精英猎手」：玩家侧对精英 / Boss 的伤害加成
        if (source != null && !(source is Monster) && (_isBossUnit || _eliteWave))
        {
            float bonus = source.attr != null ? source.attr.GetAttr(AttrType.EliteDamage) : 0f;
            if (bonus > 0f) inDamage *= (1f + bonus);
        }

        base.TakeDamage(inDamage, isCrit, ignoreDefense, showHitVfx, hitVfxFacing, source, suppressDamageText);

        // V6 词缀「荆棘」：打我的人按比例吃回一点伤害。
        // 这里不区分攻击者是近战还是远程 —— 用一个「攻击者类型」判定既不可靠也会让词缀显得随机，
        // 统一反弹更好懂。source 传 null 是必须的：否则会弹回来源、形成互弹死循环。
        if (affix != null && affix.ThornsRatio > 0f && source != null && !source.isDead)
        {
            float back = inDamage * affix.ThornsRatio;
            if (back >= 1f)
                source.TakeDamage(back, false, true, true, 0, null);
        }

        if (isDead)
        {
            // V6 词缀「分裂」：死亡时分裂出残血小怪
            if (affix != null) affix.NotifyDead();
            return;
        }

        BattleBossHpBar.RefreshFromField();
        TryBeginBossPhase2();
        TryBeginEnrage();
    }

    protected override void Update()
    {
        // 狂暴重判定：存档恢复后本帧按当前血量立即进入（不持久化，重建即重置）
        if (!isDead && _isBossUnit) TryBeginEnrage();
        if (!isDead && _canUseActiveSkill)
        {
            _skillCooldown -= Time.deltaTime;
            if (_skillCooldown <= 0f)
                _skillEnergy = Mathf.Min(1f, _skillEnergy + Time.deltaTime * 0.28f);
        }
        base.Update();
    }

    protected override void Attack(UnitBase target)
    {
        if (_skillTelegraphing) return;
        if (BattleManager.Instance != null
            && (!BattleManager.Instance.UnitsCanAct || BattleManager.Instance.AllowMonsterMapEnter))
            return;

        _swingStyle = ResolveSwingStyle(target);

        if (_canUseActiveSkill && _skillEnergy >= 0.99f && !string.IsNullOrEmpty(_skillId))
        {
            if (_skillTelegraphing) return;
            UseActiveSkill(target);
            attackCd = GetAttackCooldown();
            return;
        }

        base.Attack(target);
        // 技能冷却中不靠普攻充能，避免刚放完技能立刻叠满
        if (_canUseActiveSkill && _skillCooldown <= 0f)
            _skillEnergy = Mathf.Min(1f, _skillEnergy + 0.15f);
    }

    /// <summary>
    /// 怪物朝向：翻转 Visual 整棵预制体（fire/beattack 一起镜像），血条在 Body 平级不参与翻转。
    /// spriteDefaultFacesRight=false：朝右(dir&gt;0) 时 scale.x 取负。
    /// </summary>
    private int _lastAppliedDir = 0;

    public override Transform GetFlipRoot()
    {
        if (unitAnim != null && (unitAnim.IsProceduralAnim || unitAnim.UsesFlipXFacing)) return null;
        return _visualRoot != null ? _visualRoot : transform;
    }

    protected override void ApplyFacing(int dir)
    {
        if (dir == 0) return;

        Transform flipRoot = _visualRoot != null ? _visualRoot : transform;
        Vector3 scale = flipRoot.localScale;
        float absX = Mathf.Abs(scale.x);
        if (absX < 0.0001f) absX = 1f;

        if (spriteDefaultFacesRight)
            scale.x = dir > 0 ? absX : -absX;
        else
            scale.x = dir > 0 ? -absX : absX;

        flipRoot.localScale = scale;
        if (sr != null)
            sr.flipX = false;

        if (dir != _lastAppliedDir)
            _lastAppliedDir = dir;
    }

    /// <summary>???????</summary>
    public override Vector3 GetFirePosition()
    {
        Transform fire = firePoint != null ? firePoint : transform.Find("fire");
        if (fire != null)
        {
            // ????????????????????????????????
            Vector3 fw = fire.position;
            float centerX = transform.position.x;
            float absX = Mathf.Abs(fw.x - centerX);
            if (absX < 0.05f) absX = 0.35f;
            float y = hitPoint != null ? hitPoint.position.y : fw.y;
            return new Vector3(centerX + (facingDir < 0 ? -absX : absX), y, transform.position.z);
        }
        Vector3 off = firePointOffset;
        float fy = hitPoint != null ? hitPoint.position.y : transform.position.y + off.y;
        return new Vector3(transform.position.x + off.x * facingDir, fy, transform.position.z);
    }

    /// <summary>叠怪数字等仍用旧 Sprite 血条时才需要；现用 MonsterHealthBar 时可忽略。</summary>
    protected void LateUpdate()
    {
        if (_stackLabelRoot != null && _stackLabelRoot.gameObject.activeSelf)
            RefreshStackLabelLayout();

        if (_worldHpBar != null) return;
        if (_hpBarFill != null && _hpBarRoot != null && _hpBarRoot.gameObject.activeSelf)
        {
            float maxHp = attr.GetAttr(AttrType.MaxHp);
            float ratio = maxHp > 0 ? currentHp / maxHp : 0;
            float clamped = Mathf.Clamp01(ratio);
            var fillT = _hpBarFill.transform;
            float w = Mathf.Max(0.01f, _hpBarFillBaseWidth);
            fillT.localScale = new Vector3(clamped, 1f, 1f);
            // ??????????????
            fillT.localPosition = new Vector3(-w * 0.5f * (1f - clamped), 0f, 0f);
        }
    }

    protected override void Die(bool isCritKill = false)
    {
        if (_hpBarRoot != null)
            _hpBarRoot.gameObject.SetActive(false);
        if (_worldHpBar != null)
            _worldHpBar.gameObject.SetActive(false);

        HideStackLabel();
        base.Die(isCritKill);
        BattleBossHpBar.RefreshFromField();
    }

    public override void ResetForReuse()
    {
        CollapseBodyRootForPool();
        base.ResetForReuse();
        _lastAppliedDir = 0;
        _isEnteringMap = false;
        _enterAtPauseStop = false;
        _bossSwingIndex = 0;
        _enraged = false;
        _forcedTarget = null;
        _worldHpBar = null;
        HideStackLabel();
        // 【2026-10-16】回池关掉精英光环：下一只从池里复用的可能是普通怪（显隐唯一出口见 ApplyEliteRingVisible）
        ApplyEliteRingVisible(false);
    }

    /// <summary>??MonsterBody ??????RB ??Body???? Visual ?????????/summary>
    void EnsureBodyRoot()
    {
        if (_bodyRoot != null) return;

        var parentBody = transform.parent != null ? transform.parent.GetComponent<MonsterBodyRoot>() : null;
        if (parentBody != null)
        {
            _bodyRoot = parentBody.transform;
            _visualRoot = transform;
            rb = _bodyRoot.GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = _bodyRoot.gameObject.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0;
                rb.freezeRotation = true;
            }
            return;
        }

        _visualRoot = transform;
        Transform sceneParent = transform.parent;
        Vector3 worldPos = transform.position;

        var bodyGo = new GameObject("MonsterBody");
        bodyGo.transform.SetParent(sceneParent, false);
        bodyGo.transform.position = worldPos;
        bodyGo.transform.rotation = transform.rotation;
        bodyGo.transform.localScale = Vector3.one;
        bodyGo.AddComponent<MonsterBodyRoot>();

        var bodyRb = bodyGo.AddComponent<Rigidbody2D>();
        bodyRb.gravityScale = 0;
        bodyRb.freezeRotation = true;
        if (rb != null)
        {
            bodyRb.velocity = rb.velocity;
            Destroy(rb);
        }
        rb = bodyRb;

        transform.SetParent(bodyGo.transform, true);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        ApplyMonsterVisualScaleRules();

        _bodyRoot = bodyGo.transform;
    }

    void CollapseBodyRootForPool()
    {
        if (_bodyRoot == null) return;
        Transform poolParent = PoolManager.Instance != null ? PoolManager.Instance.transform : null;
        transform.SetParent(poolParent, false);
        if (_bodyRoot != null)
            Destroy(_bodyRoot.gameObject);
        _bodyRoot = null;
        _visualRoot = null;
    }
}
