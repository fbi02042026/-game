using UnityEngine;

/// <summary>
/// 佣兵 AI：索敌交战，但拴在玩家附近——过远或偏远打太久会收队回玩家再战。
/// <para>【2026-10-23 主人拍板】本文件只留：字段 / 生命周期 / 受伤与被动接线 / 队形与阵亡 / AI 策略选择。
/// 名牌见 .NameLabel.cs、属性与形态见 .Attributes.cs、AI 行为见 .AI.cs。</para>
/// </summary>
public partial class Mercenary : UnitBase
{
    public string mercId;
    public string hireId;
    public int mercLevel = 1;
    /// <summary>
    /// 佣兵星级 1~5（2026-10-06 新增）：MP 池与回复的成长真源。
    /// 由局内佣兵生成处（RunDraftDirector.SpawnRunMerc / RefreshMercUnit）写入；
    /// 非局内来源（城镇/教程）保持 1 星 —— 花名册里 ★1 就是最低档，不是兜底值。
    /// </summary>
    public int mercStar = 1;
    public string DisplayName { get; private set; }
    /// <summary>本局佩戴主动技能（来自存档；空则无主动技）</summary>
    public string equippedSkillId;

    public MercSkillCaster SkillCaster { get; private set; }
    public MercPassiveRunner PassiveRunner { get; private set; }

    private int _partyIndex = -1;
    /// <summary>索敌距内粘滞目标：进距先锁谁打谁，死/出距才换。</summary>
    UnitBase _acquireLock;
    /// <summary>收队回玩家中：到再入距前不索敌。</summary>
    bool _recallingToHero;
    /// <summary>相对玩家偏远时持续交战计时。</summary>
    float _awayFightTimer;
    /// <summary>【2026-10-20】牧师「站住等自动奶」已定身时长，超过 MERC_HEAL_WAIT_MAX_SEC 强制恢复跟队。</summary>
    float _healWaitTimer;
    /// <summary>引导：原地眩晕，不跑 AI，受击不死。</summary>
    public bool TutorialStunned { get; private set; }
    float _stunAnimTimer;
    Transform _nameLabelRoot;
    TextMesh _nameLabel;
    MeshRenderer _nameLabelRenderer;
    TextMesh[] _nameOutlineLabels;
    MeshRenderer[] _nameOutlineRenderers;
    const float NameScaleMul = 1.2f;
    const float NameCharSizeBase = 0.1008f;
    static readonly Vector2[] NameOutlineDirs =
    {
        new Vector2(-1f, 0f), new Vector2(1f, 0f),
        new Vector2(0f, -1f), new Vector2(0f, 1f)
    };

    protected override void Awake()
    {
        firePointOffset = new Vector3(0.3f, 0.32f, 0f);
        hitPointOffset = new Vector3(0f, 0.55f, 0f);

        base.Awake();
        isAlly = true;
        spriteDefaultFacesRight = false;
        KillComboAfterimage.Ensure(this);
    }

    public void SetTutorialStunned(bool on)
    {
        TutorialStunned = on;
        if (rb != null) rb.velocity = Vector2.zero;
        if (unitAnim != null)
        {
            unitAnim.SetMove(false, facingDir);
            if (on) unitAnim.PlayDebuff();
            else unitAnim.ClearDebuff();
        }
        _stunAnimTimer = 0f;
    }

    /// <summary>围殴结束：停眩晕循环动画，但仍可保持 TutorialStunned 定身到对话完。</summary>
    public void StopTutorialStunAnim()
    {
        _stunAnimTimer = 9999f; // 阻止 AIUpdate 里循环 PlayDebuff
        if (unitAnim != null)
            unitAnim.ClearDebuff();
    }

    public void SetupBattleSkills(string activeId, string passiveId)
    {
        equippedSkillId = activeId;
        if (SkillCaster == null) SkillCaster = gameObject.GetComponent<MercSkillCaster>();
        if (SkillCaster == null) SkillCaster = gameObject.AddComponent<MercSkillCaster>();
        if (PassiveRunner == null) PassiveRunner = gameObject.GetComponent<MercPassiveRunner>();
        if (PassiveRunner == null) PassiveRunner = gameObject.AddComponent<MercPassiveRunner>();
        SkillCaster.Bind(this, activeId);
        PassiveRunner.Bind(this, passiveId);
        WirePassiveOnAttack();
    }

    /// <summary>队友（含自己）血量低于约 92% 时需要治疗。</summary>
    bool AllyNeedsHeal()
    {
        if (attr != null)
        {
            float max = attr.GetAttr(AttrType.MaxHp);
            if (max > 0.01f && currentHp / max < 0.92f)
                return true;
        }
        var bm = BattleManager.Instance;
        if (bm?.hero != null && !bm.hero.isDead && bm.hero.attr != null)
        {
            float max = bm.hero.attr.GetAttr(AttrType.MaxHp);
            if (max > 0.01f && bm.hero.currentHp / max < 0.92f)
                return true;
        }
        var mercs = MercenaryManager.Instance != null ? MercenaryManager.Instance.GetActiveMercs() : null;
        if (mercs == null) return false;
        for (int i = 0; i < mercs.Count; i++)
        {
            var m = mercs[i];
            if (m == null || m.isDead || m.attr == null) continue;
            float max = m.attr.GetAttr(AttrType.MaxHp);
            if (max > 0.01f && m.currentHp / max < 0.92f)
                return true;
        }
        return false;
    }

    public override void TakeDamage(float damage, bool isCrit, bool ignoreDefense = false, bool showHitVfx = true, int hitVfxFacing = 0, UnitBase source = null, bool suppressDamageText = false)
    {
        if (TutorialStunned)
        {
            // 【2026-10-24 主人拍板】对齐正式局公式：旧码走 2026-10-09 已废弃的减法减伤
            // Max(1, dmg - defense)，只读物防、不吃装备减伤、不过 FinalHit，引导演出窗口
            // 内的防御表现与正式局不一致。改走标准 FinalHit（物/魔分流由 source 类型定，
            // 与 UnitBase.TakeDamage 同口径），×0.35 演出系数与钳血 1 不变。
            float finalDamage = DamageFormula.FinalHit(
                damage, attr, ignoreDefense,
                source != null && source.IsMagicDamageDealer(),
                IsMagicDamageDealer());
            currentHp = Mathf.Max(1f, currentHp - finalDamage * 0.35f);
            // 与 UnitBase 同口径： suppressDamageText 时不飘字（引导演出判死专用，正式关默认 false）
            if (!suppressDamageText)
                DamageTextSystem.Instance?.SpawnDamageText(GetHitPosition(), Mathf.RoundToInt(finalDamage * 0.35f), isCrit, true, hitVfxFacing);
            PlayHitReaction();
            _stunAnimTimer = 0.35f;
            return;
        }

        if (PassiveRunner != null)
            damage = PassiveRunner.ModifyIncomingDamage(damage);

        float before = currentHp;
        base.TakeDamage(damage, isCrit, ignoreDefense, showHitVfx, hitVfxFacing, source, suppressDamageText);
        if (PassiveRunner != null && !Mathf.Approximately(before, currentHp))
            PassiveRunner.OnHpChanged();
    }

    /// <summary>
    /// 受击一律播 DAMAGED 动画 + 白闪（2026-10-15：原先走路只闪白、站立才播，现统一播放）。
    /// 轮到攻击时攻击动画立即打断 DAMAGED（PlayAttack→InterruptDamaged）。
    /// </summary>
    protected override void PlayHitReaction()
    {
        if (unitAnim == null) return;
        unitAnim.PlayDamaged(playHitAnim: true);
        _stunAnimTimer = 0.35f;
    }

    void WirePassiveOnAttack()
    {
        OnAttack -= OnPassiveBasicAttackHit;
        OnAttack += OnPassiveBasicAttackHit;
    }

    void OnPassiveBasicAttackHit(UnitBase target, float damage, bool isCrit)
    {
        if (PassiveRunner != null && target != null && !target.isDead && damage > 0f)
            PassiveRunner.OnBasicAttackHit(target, attr != null ? attr.GetAttr(AttrType.Attack) : 0f);
    }

    public void Init(string id, int level = 1)
    {
        mercId = id;
        mercLevel = level;
        gameObject.name = "Merc_" + id;

        ResetForReuse();

        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();

        SetupAttributes(id, level);
        Face(1);
        WirePassiveOnAttack();

        Debug.Log($"[Mercenary:{id}] Init完成 | isAlly={isAlly} | facingDir={facingDir} | pos={transform.position}");
    }

    public override void ResetForReuse()
    {
        base.ResetForReuse();
        _acquireLock = null;
        _healWaitTimer = 0f;
    }

    public void SetHireId(string id)
    {
        hireId = id;
        // 【2026-10-22 施工单 T8】局外星加成的装配时机：所有出场链路（RunDraftDirector.SpawnRunMerc /
        // RefreshMercUnit / PromoteMerc、BattleManager 出战队伍、引导替身）都是 Init → SetHireId 顺序，
        // 在这里统一补挂加成最稳 —— 幂等（_outStarApplied 挡重复），0 星是无操作。
        ApplyOutStarBonus();
        // 【2026-10-23 施工单 T4】养成资质加成同款补挂时机（幂等：_growApplied 挡重复，无养成记录是无操作）。
        ApplyGrowBonus();
        // 【2026-10-27 羁绊】通关羁绊后全属性 ×1.5 同款补挂时机（幂等：_bondApplied 挡重复，未通关是无操作）。
        ApplyBondClearBonus();
    }

    /// <summary>
    /// 佣兵名字的<b>唯一出口</b>：只认真名（displayName）。
    /// <para>【2026-10-11 主人拍板】旧实现是「有昵称就用昵称，没有就退回职业名」三级取用，
    /// 于是同一个 H005 在<b>抽奖提示</b>写「米娅」、<b>场景头顶</b>写「小红」、<b>头像栏</b>写着「游侠」，
    /// 主人反馈「佣兵的名字还是没有统一」—— 玩家会当成三个人。
    /// 现在一律用真名；昵称（小红）只在<b>明确并排展示</b>的地方出现
    /// （冒险日志 / 商店 / 抽卡卡面那种「米娅·小红」），不再顶替主名字。</para>
    /// </summary>
    public void SetDisplayName(string displayName)
    {
        if (!string.IsNullOrEmpty(displayName))
        {
            DisplayName = displayName;
        }
        else
        {
            // 真源缺名字 → fail closed：报出来让主人看见，不拿职业名 / 编号悄悄顶上（铁律 14）。
            Debug.LogError($"[Mercenary] 佣兵 {mercId}（hireId={hireId}）没有 displayName，名字条留空" +
                           "—— 去 MercRosterDefs / merc_roster.csv 补名字，别拿职业名顶替");
            DisplayName = string.Empty;
        }
        RefreshNameLabel();
    }

    protected override float GetFormationLaneOffset()
    {
        if (_partyIndex == 0) return -0.15f;
        if (_partyIndex == 1) return 0.45f;
        return 0f;
    }

    protected override void OnDeathRelease()
    {
        // 【2026-10-17 诊断日志】死亡动画播完 → Destroy 的最终退场时刻，与 OnMercenaryDead 配对
        Debug.LogWarning($"[Mercenary] 退场销毁：{DisplayName}({mercId}) 死亡动画已播完");
        HideNameLabel();
        Destroy(gameObject);
    }

    int ResolvePartyIndex()
    {
        if (_partyIndex >= 0) return _partyIndex;
        var mm = MercenaryManager.Instance;
        if (mm == null) return 0;
        var list = mm.GetActiveMercs();
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == this) { _partyIndex = i; return i; }
        }
        return 0;
    }

    public void SetPartyIndex(int index) => _partyIndex = index;

    public int GetPartyIndexOrZero() => ResolvePartyIndex();

    MercAiPolicy.Policy _aiPolicy;
    string _cachedJob;
    MercRosterDefs.MercRarity _cachedRarity = MercRosterDefs.MercRarity.Common;
    bool _aiPolicyReady;

    void EnsureAiPolicy()
    {
        string job = null;
        MercRosterDefs.MercRarity rarity = MercRosterDefs.MercRarity.Common;
        if (!string.IsNullOrEmpty(hireId) && MercRosterDefs.TryGetByHireId(hireId, out var byHire))
        {
            job = byHire.JobName;
            rarity = byHire.Rarity;
        }
        else if (MercRosterDefs.TryGetByAssetId(mercId, out var byAsset))
        {
            job = byAsset.JobName;
            rarity = byAsset.Rarity;
        }

        // 【2026-10-23 主人拍板】不再每帧拼 "job|rarity" 字符串比较 key
        //（原实现即使命中缓存也每帧分配 GC），改为 job 引用/内容比较 + rarity 枚举比较。
        if (_aiPolicyReady && _cachedJob == job && _cachedRarity == rarity) return;
        _cachedJob = job;
        _cachedRarity = rarity;
        _aiPolicy = MercAiPolicy.Get(job, rarity);
        _aiPolicyReady = true;
    }

}
