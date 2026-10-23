using UnityEngine;

/// <summary>
/// 佣兵 AI：索敌交战，但拴在玩家附近——过远或偏远打太久会收队回玩家再战。
/// </summary>
public class Mercenary : UnitBase
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
    /// <summary>本局佩戴被动技能</summary>
    public string equippedPassiveSkillId;

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
        equippedPassiveSkillId = passiveId;
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
            float defense = ignoreDefense ? 0f : attr.GetAttr(AttrType.Defense);
            float finalDamage = Mathf.Max(1f, damage - defense);
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

    void RefreshNameLabel()
    {
        if (string.IsNullOrEmpty(DisplayName))
        {
            HideNameLabel();
            return;
        }
        EnsureNameLabel();
        _nameLabelRoot.gameObject.SetActive(true);
        _nameLabel.text = DisplayName;
        ApplyNameLabelFont();
        if (_nameOutlineLabels != null)
        {
            for (int i = 0; i < _nameOutlineLabels.Length; i++)
            {
                if (_nameOutlineLabels[i] != null)
                    _nameOutlineLabels[i].text = DisplayName;
            }
        }
        RefreshNameLabelLayout();
    }

    void ApplyNameLabelFont()
    {
        if (_nameLabel == null) return;
        var font = GameFonts.GetChinese();
        _nameLabel.font = font;
        if (font != null)
        {
            font.RequestCharactersInTexture(DisplayName, _nameLabel.fontSize, _nameLabel.fontStyle);
            if (_nameLabelRenderer != null && font.material != null)
                _nameLabelRenderer.sharedMaterial = font.material;
            if (_nameOutlineLabels != null)
            {
                for (int i = 0; i < _nameOutlineLabels.Length; i++)
                {
                    if (_nameOutlineLabels[i] == null) continue;
                    _nameOutlineLabels[i].font = font;
                    if (_nameOutlineRenderers != null && i < _nameOutlineRenderers.Length
                        && _nameOutlineRenderers[i] != null && font.material != null)
                        _nameOutlineRenderers[i].sharedMaterial = font.material;
                }
            }
        }
        ApplyNameLabelColor();
    }

    void ApplyNameLabelColor()
    {
        if (_nameLabel == null) return;
        var rarity = RarityPalette.ResolveMercRarity(mercId);
        _nameLabel.color = RarityPalette.Get(rarity);
        if (_nameOutlineLabels == null) return;
        var outline = RarityPalette.Outline;
        for (int i = 0; i < _nameOutlineLabels.Length; i++)
        {
            if (_nameOutlineLabels[i] != null)
                _nameOutlineLabels[i].color = outline;
        }
    }

    void RefreshNameLabelLayout()
    {
        if (_nameLabelRoot == null || _nameLabel == null) return;
        float rootAbs = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.y));
        float charSize = NameCharSizeBase / rootAbs;
        _nameLabel.characterSize = charSize;
        if (_nameLabelRenderer != null)
        {
            _nameLabelRenderer.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            _nameLabelRenderer.sortingOrder = GameConfig.SORT_VFX + 24;
        }
        float outlineStep = charSize * 0.22f;
        if (_nameOutlineLabels != null)
        {
            for (int i = 0; i < _nameOutlineLabels.Length; i++)
            {
                var o = _nameOutlineLabels[i];
                if (o == null) continue;
                o.characterSize = charSize;
                o.text = _nameLabel.text;
                var dir = NameOutlineDirs[i];
                o.transform.localPosition = new Vector3(dir.x * outlineStep, dir.y * outlineStep, 0.002f);
                if (_nameOutlineRenderers != null && i < _nameOutlineRenderers.Length && _nameOutlineRenderers[i] != null)
                {
                    _nameOutlineRenderers[i].sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
                    _nameOutlineRenderers[i].sortingOrder = GameConfig.SORT_VFX + 23;
                }
            }
        }
    }

    static Transform GetUnitLabelsRoot()
    {
        var bm = BattleManager.Instance;
        if (bm == null || bm.unitRoot == null) return null;
        Transform labels = bm.unitRoot.Find("UnitLabels");
        if (labels != null) return labels;
        var go = new GameObject("UnitLabels");
        labels = go.transform;
        labels.SetParent(bm.unitRoot, false);
        return labels;
    }

    Vector3 GetNameLabelWorldPos()
    {
        float yOff = 0.94f;
        return transform.position + new Vector3(0f, yOff, transform.position.z);
    }

    void LateUpdate()
    {
        SyncNameLabelTransform();
        TickLowHpWarn();
    }

    void TickLowHpWarn()
    {
        float maxHp = attr != null ? attr.GetAttr(AttrType.MaxHp) : 0f;
        float ratio = maxHp > 0.01f ? currentHp / maxHp : 1f;
        bool dead = isDead || currentHp <= 0f;
        if (unitAnim != null)
            unitAnim.TickLowHpFlash(ratio, dead);
    }

    void SyncNameLabelTransform()
    {
        if (_nameLabelRoot == null || !_nameLabelRoot.gameObject.activeSelf) return;
        _nameLabelRoot.position = GetNameLabelWorldPos();
        _nameLabelRoot.localScale = new Vector3(NameScaleMul, NameScaleMul, NameScaleMul);
    }

    void EnsureNameLabel()
    {
        if (_nameLabel != null) return;

        Transform parent = GetUnitLabelsRoot() ?? transform;
        _nameLabelRoot = new GameObject("MercName").transform;
        _nameLabelRoot.SetParent(parent, false);

        _nameOutlineLabels = new TextMesh[NameOutlineDirs.Length];
        _nameOutlineRenderers = new MeshRenderer[NameOutlineDirs.Length];
        for (int i = 0; i < NameOutlineDirs.Length; i++)
        {
            var oGo = new GameObject("Outline" + i, typeof(TextMesh));
            oGo.transform.SetParent(_nameLabelRoot, false);
            var o = oGo.GetComponent<TextMesh>();
            o.text = DisplayName ?? "";
            o.fontSize = 22;
            o.anchor = TextAnchor.MiddleCenter;
            o.alignment = TextAlignment.Center;
            o.fontStyle = FontStyle.Bold;
            o.color = RarityPalette.Outline;
            _nameOutlineLabels[i] = o;
            _nameOutlineRenderers[i] = oGo.GetComponent<MeshRenderer>();
        }

        var fillGo = new GameObject("Fill", typeof(TextMesh));
        fillGo.transform.SetParent(_nameLabelRoot, false);
        _nameLabel = fillGo.GetComponent<TextMesh>();
        _nameLabel.text = DisplayName ?? "";
        _nameLabel.fontSize = 22;
        _nameLabel.anchor = TextAnchor.MiddleCenter;
        _nameLabel.alignment = TextAlignment.Center;
        _nameLabel.fontStyle = FontStyle.Bold;

        _nameLabelRenderer = fillGo.GetComponent<MeshRenderer>();
        if (_nameLabelRenderer != null)
            _nameLabelRenderer.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;

        ApplyNameLabelFont();
        RefreshNameLabelLayout();
    }

    void HideNameLabel()
    {
        if (_nameLabelRoot != null)
            Destroy(_nameLabelRoot.gameObject);
        _nameLabelRoot = null;
        _nameLabel = null;
        _nameLabelRenderer = null;
        _nameOutlineLabels = null;
        _nameOutlineRenderers = null;
    }

    /// <summary>近战前排角色（剑盾/狂战/重武等）；弓手法师牧师为远程后排。</summary>
    public static bool IsMeleeRoleId(string id)
    {
        if (string.IsNullOrEmpty(id)) return true;
        string asset = id;
        if (MercRosterDefs.TryGetByHireId(id, out var byHire))
            asset = byHire.AssetId;
        else if (MercRosterDefs.TryGetByAssetId(id, out var byAsset))
            asset = byAsset.AssetId;

        if (asset.StartsWith("gongshou")) return false;
        if (asset.StartsWith("naima") || asset.StartsWith("fashi") || asset.StartsWith("mushi")) return false;

        string job = MercRosterDefs.GetJobName(asset) ?? MercRosterDefs.GetJobName(id);
        if (!string.IsNullOrEmpty(job))
        {
            if (job.Contains("游侠") || job.Contains("法师") || job.Contains("牧师")) return false;
        }
        return true;
    }

    /// <summary>
    /// 该佣兵是否用法术攻击（魔攻）。hireId / assetId 都吃。
    /// <para>「获得佣兵」弹窗属性行二选一（攻击/魔攻、防御/魔防）的<b>唯一判据</b>——
    /// 与 <see cref="GetAttackType"/> 同源，UI 侧不许自己复制前缀表。</para>
    /// </summary>
    public static bool UsesMagicAttack(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        if (IsMeleeRoleId(id)) return false;                     // 近战 = 物攻
        return !ResolveAssetId(id).StartsWith("gongshou");       // 非近战里弓手 = 物攻，其余（法师/牧师）= 魔攻
    }

    static bool IsMeleeMercId(string id) => IsMeleeRoleId(id);

    void ApplyMeleeRangeVsPlayer()
    {
        if (!IsMeleeMercId(mercId)) return;
        float playerRange = GameConfig.RangeSword;
        if (Hero.Instance?.attr != null)
            playerRange = Hero.Instance.attr.GetAttr(AttrType.AttackRange);
        if (GameConfig.IsRangedAttackRange(playerRange)) return;
        attr.SetAttr(AttrType.AttackRange, playerRange * 0.8f);
    }

    // Face() 已在 UnitBase

    void SetupAttributes(string id, int level)
    {
        attr.ResetToBase();
        _outStarApplied = false;   // 【2026-10-22 T8】基底重建后加成要重挂（SetHireId/下方出口会再调）
        _growApplied = false;      // 【2026-10-23 施工单 T4】养成资质加成同款幂等闸，与升星并联互不干扰
        level = Mathf.Max(1, level);

        if (MercRosterDefs.TryGetByAssetId(id, out _))
        {
            MercRosterDefs.ApplyCombatStats(id, level,
                out float hp, out float atk, out float def, out float atkSpd, out float move, out float range);
            attr.SetBaseAndCurrent(AttrType.MaxHp, hp);
            attr.SetBaseAndCurrent(AttrType.Attack, atk);
            attr.SetBaseAndCurrent(AttrType.Defense, def);
            // 2026-09-27 主人拍板：防御拆物/魔两条，佣兵同样登记魔防键（伤害公式缺键会报 DF-003）
            attr.SetBaseAndCurrent(AttrType.MagicDefense, def);
            attr.SetBaseAndCurrent(AttrType.MagicAttack, GetAttackType() == WeaponAttackType.Magic ? atk : 0f);
            attr.SetBaseAndCurrent(AttrType.AttackSpeed, atkSpd);
            attr.SetBaseAndCurrent(AttrType.MoveSpeed, move);
            attr.SetBaseAndCurrent(AttrType.AttackRange, range);
            attr.SetBaseAndCurrent(AttrType.CritRate, GameConfig.BASE_CRIT_RATE);
            currentHp = attr.GetAttr(AttrType.MaxHp);
            ApplyMeleeRangeVsPlayer();
            // 【2026-10-22 主人拍板 v4.1】石像赐福等局内 buff（tempBuffs）佣兵也要吃到：
            // 装配时机 = 属性基底写完后统一叠加，与主角 Hero.RecalcAttr 的 extraBonus 同款口径。
            ApplyRunTempBuffs();
            ApplyOutStarBonus();   // 【2026-10-22 施工单 T8】局外星攻防加成（hireId 已设时在此生效，未设等 SetHireId）
            ApplyGrowBonus();      // 【2026-10-23 施工单 T4】养成资质加成（外层乘区，与升星并联）
            return;
        }

        bool advanced = GameConfig.GetMercTier(id) == MercTier.Advanced;
        float baseHp, baseAtk, baseDef, atkInterval;
        float atkRange = AttackRangeTable.GetMercWorld(id);

        if (id.StartsWith("dunbing"))
        {
            if (advanced) { baseHp = 550; baseAtk = 18; baseDef = 20; atkInterval = 1.1f; }
            else { baseHp = 300; baseAtk = 10; baseDef = 10; atkInterval = 1.2f; }
        }
        else if (id.StartsWith("gongshou"))
        {
            if (advanced) { baseHp = 280; baseAtk = 35; baseDef = 5; atkInterval = 0.85f; }
            else { baseHp = 150; baseAtk = 20; baseDef = 3; atkInterval = 0.9f; }
        }
        else if (id.StartsWith("kuangzhan"))
        {
            if (advanced) { baseHp = 280; baseAtk = 35; baseDef = 5; atkInterval = 0.85f; }
            else { baseHp = 150; baseAtk = 20; baseDef = 3; atkInterval = 0.9f; }
        }
        else if (id.StartsWith("naima") || id.StartsWith("fashi") || id.StartsWith("mushi"))
        {
            if (advanced) { baseHp = 320; baseAtk = 15; baseDef = 8; atkInterval = 1.3f; }
            else { baseHp = 180; baseAtk = 8; baseDef = 4; atkInterval = 1.5f; }
        }
        else if (id.StartsWith("zhongzhan"))
        {
            if (advanced) { baseHp = 360; baseAtk = 22; baseDef = 10; atkInterval = 1f; }
            else { baseHp = 200; baseAtk = 12; baseDef = 5; atkInterval = 1.1f; }
        }
        else
        {
            if (advanced) { baseHp = 360; baseAtk = 22; baseDef = 10; atkInterval = 1f; }
            else { baseHp = 200; baseAtk = 12; baseDef = 5; atkInterval = 1.1f; }
        }

        float hpMul = 1f + (level - 1) * 0.1f;
        float atkAdd = (level - 1) * 2f;
        attr.SetBaseAndCurrent(AttrType.MaxHp, baseHp * hpMul);
        attr.SetBaseAndCurrent(AttrType.Attack, baseAtk + atkAdd);
        attr.SetBaseAndCurrent(AttrType.Defense, baseDef);
        // 2026-09-27 主人拍板：防御拆物/魔两条，佣兵同样登记魔防键（伤害公式缺键会报 DF-003）
        attr.SetBaseAndCurrent(AttrType.MagicDefense, baseDef);
        attr.SetBaseAndCurrent(AttrType.MagicAttack, GetAttackType() == WeaponAttackType.Magic ? (baseAtk + atkAdd) : 0f);
        attr.SetBaseAndCurrent(AttrType.AttackSpeed, 1f / Mathf.Max(0.2f, atkInterval));
        attr.SetBaseAndCurrent(AttrType.MoveSpeed, GameConfig.BASE_MOVE_SPEED);
        attr.SetBaseAndCurrent(AttrType.AttackRange, atkRange);
        attr.SetBaseAndCurrent(AttrType.CritRate, GameConfig.BASE_CRIT_RATE);

        currentHp = attr.GetAttr(AttrType.MaxHp);
        ApplyMeleeRangeVsPlayer();
        // 【2026-10-22 主人拍板 v4.1】石像赐福等局内 buff（tempBuffs）佣兵也要吃到（与上面花名册分支同口径）。
        ApplyRunTempBuffs();
        ApplyOutStarBonus();
        ApplyGrowBonus();      // 【2026-10-23 施工单 T4】养成资质加成（外层乘区，与升星并联）
    }

    // ============================================================
    // 【2026-10-22 施工单 T8】局外升星加成进战斗
    // ============================================================

    /// <summary>本次属性基底是否已挂过局外星加成（SetupAttributes 置 false，ApplyOutStarBonus 置 true，防重复乘区）。</summary>
    bool _outStarApplied;

    /// <summary>
    /// 局外星（outStar）加成：每星 物攻(Attack)/物防(Defense) +8%、魔攻(MagicAttack)/魔防(MagicDefense) +7%
    /// （2026-10-22 三次拍板「先按8%加吧 魔按7%」）。外层乘区（attr.AddAttr isPercent，与局内 tempBuffs
    /// 同一条乘法链），血/移速等不动；不改 MercRosterDefs.ApplyCombatStats 内部。
    /// hireId 未设（Init 时序）或非花名册佣兵 → 跳过且<b>不置位</b>，等 SetHireId 再来；
    /// 0 星佣兵数值与现在完全一致。数值唯一真源 = MercStarDefs。
    /// </summary>
    public void ApplyOutStarBonus()
    {
        if (attr == null || _outStarApplied) return;
        string hid = !string.IsNullOrEmpty(hireId) ? hireId : mercId;
        int outStar = MercStarSystem.OutStarOf(hid);
        if (outStar < 0) return;   // 解析不到（hireId 未设 / 非花名册）：不置位，等 SetHireId 再补
        _outStarApplied = true;
        if (outStar <= 0) return;

        attr.AddAttr(AttrType.Attack,      MercStarDefs.AtkPctPerStar * outStar, true);
        attr.AddAttr(AttrType.Defense,     MercStarDefs.AtkPctPerStar * outStar, true);
        attr.AddAttr(AttrType.MagicAttack, MercStarDefs.MagicPctPerStar * outStar, true);
        attr.AddAttr(AttrType.MagicDefense, MercStarDefs.MagicPctPerStar * outStar, true);
        Debug.Log($"[Mercenary] 局外星加成生效：{hid} ★{outStar} → 攻/防 +{MercStarDefs.AtkPctPerStar * outStar:0%}，魔攻/魔防 +{MercStarDefs.MagicPctPerStar * outStar:0%}");
    }

    // ============================================================
    // 【2026-10-23 主人拍板 C2=A / 施工单 T4】佣兵升阶（局外养成等级）资质加成进战斗
    // ============================================================

    /// <summary>本次属性基底是否已挂过养成资质加成（与 _outStarApplied 同款幂等闸，互不干扰）。</summary>
    bool _growApplied;

    /// <summary>
    /// 【2026-10-23 主人拍板 C2=A / 施工单 T4】佣兵升阶（局外养成等级）资质加成：
    /// 按 MercGrowSystem 累计资质（升级时掷定即存，本函数只读乘一次）做<b>外层乘区</b>
    /// （attr.AddAttr isPercent，与升星加成并联叠乘、互不干扰）；
    /// 🔴 不改 MercRosterDefs.ApplyCombatStats 内部公式（施工红线）。
    /// 口径：物攻/魔攻 ← aptAtk（魔攻与物攻同口径，Q7 拍板）；物防/魔防 ← aptDef；血 ← aptHp。
    /// 数值为 0 的项不调 AddAttr（省日志噪音）；无养成记录（Lv1）→ 数值与改动前完全一致。
    /// hireId 未设 / 非花名册（Get 返回 null）→ 跳过且<b>不置位</b>，等 SetHireId 再补。
    /// 数值唯一真源 = MercGrowSystem（存档累计值）。
    /// </summary>
    public void ApplyGrowBonus()
    {
        if (attr == null || _growApplied) return;
        string hid = !string.IsNullOrEmpty(hireId) ? hireId : mercId;
        var entry = MercGrowSystem.Get(hid);
        if (entry == null) return;   // 解析不到 hireId（Init 时序）或无养成记录：不置位，等 SetHireId 再补
        _growApplied = true;

        float atkPct = MercGrowSystem.AptitudePct(hid, 0);
        float defPct = MercGrowSystem.AptitudePct(hid, 1);
        float hpPct  = MercGrowSystem.AptitudePct(hid, 2);
        if (atkPct != 0f)
        {
            attr.AddAttr(AttrType.Attack,      atkPct, true);
            attr.AddAttr(AttrType.MagicAttack, atkPct, true);   // 魔攻与物攻同口径（Q7 拍板）
        }
        if (defPct != 0f)
        {
            attr.AddAttr(AttrType.Defense,      defPct, true);
            attr.AddAttr(AttrType.MagicDefense, defPct, true);
        }
        if (hpPct != 0f)
            attr.AddAttr(AttrType.MaxHp, hpPct, true);
        Debug.Log($"[Mercenary] 养成资质加成生效：{hid} Lv{MercGrowSystem.LevelOf(hid)} → 攻/魔攻 +{atkPct:0%}，防/魔防 +{defPct:0%}，血 +{hpPct:0%}");
    }

    // ============================================================
    // 【2026-10-22 主人拍板 v4.1】石像赐福：佣兵也吃 BattleManager.tempBuffs
    // ============================================================

    /// <summary>
    /// 【2026-10-22 主人拍板 v4.1】把 BattleManager.tempBuffs（石像赐福 / 局内强化卡，与主角同款口径：
    /// 读同一份 tempBuffs 列表，逐条 AttrBonusData 叠加）叠进佣兵属性。
    /// 装配时机 = SetupAttributes 基底写完后，所以中途抽奖入队 / 阵亡复活（SpawnMercenary → Init）
    /// 的新佣兵生成时自然带上，无需额外接线。
    /// </summary>
    void ApplyRunTempBuffs()
    {
        var bm = BattleManager.Instance;
        if (bm == null || bm.tempBuffs == null || bm.tempBuffs.Count == 0 || attr == null) return;
        for (int i = 0; i < bm.tempBuffs.Count; i++)
        {
            var b = bm.tempBuffs[i];
            if (b == null) continue;
            attr.AddAttr(b.attrType, b.value, b.isPercent);
        }
        currentHp = Mathf.Min(currentHp, attr.GetAttr(AttrType.MaxHp));
    }

    /// <summary>
    /// 【2026-10-22 主人拍板 v4.1】赐福瞬间对在场佣兵触发的重算入口：
    /// 全量重建基底（SetupAttributes）后由 ApplyRunTempBuffs 统一叠加 tempBuffs —— 幂等，
    /// 不会与既有加成算重；当前血量保持绝对值（与 Hero.RecalcAttr 同口径，只钳上限不回满）。
    /// 死亡/复活不回退：复活链 SpawnMercenary → Init → SetupAttributes 会重新带上全部 tempBuffs。
    /// </summary>
    public void RecalcAttr()
    {
        if (attr == null || string.IsNullOrEmpty(mercId)) return;
        float hpBefore = currentHp;
        SetupAttributes(mercId, Mathf.Max(1, mercLevel));
        currentHp = Mathf.Min(hpBefore, attr.GetAttr(AttrType.MaxHp));
    }

    protected override WeaponAttackType GetAttackType()
        => UsesMagicAttack(mercId) ? WeaponAttackType.Magic : WeaponAttackType.Physical;

    protected override AttackVfxKit GetAttackVfxKit()
    {
        if (!IsMeleeRoleId(mercId))
        {
            string asset = ResolveAssetId(mercId);
            if (asset.StartsWith("gongshou"))
                return AttackVfxKit.Bow;
            return AttackVfxKit.Orb;
        }
        return AttackVfxKit.MeleeSlash;
    }

    static string ResolveAssetId(string id)
    {
        if (string.IsNullOrEmpty(id)) return "";
        if (MercRosterDefs.TryGetByHireId(id, out var byHire))
            return byHire.AssetId ?? id;
        if (MercRosterDefs.TryGetByAssetId(id, out var byAsset))
            return byAsset.AssetId ?? id;
        return id;
    }

    /// <summary>
    /// 队形车道（相对玩家 0）。【2026-10-11 主人拍板】原值 -0.35 / +0.35：
    /// 0 号站在最下面那条，被底部 UI（背包 / 技能条）挡住，主人原话「招募到的佣兵位置往上移点 现在在最下面被挡住了」。
    /// 现在整体往上抬到 -0.15 / +0.45（仍在可行走区 -0.809 ~ 0.526 内，超出由 BattleLaneBounds 自己钳）。
    /// 想再调只改这两个数，别在别处另写一套队形。
    /// </summary>
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
    string _cachedJobKey;
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

        string key = (job ?? "") + "|" + (int)rarity;
        if (_aiPolicyReady && _cachedJobKey == key) return;
        _cachedJobKey = key;
        _aiPolicy = MercAiPolicy.Get(job, rarity);
        _aiPolicyReady = true;
    }

    protected override void AIUpdate()
    {
        if (TutorialStunned)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            if (_stunAnimTimer < 9000f)
            {
                _stunAnimTimer -= Time.deltaTime;
                if (_stunAnimTimer <= 0f && unitAnim != null)
                {
                    unitAnim.PlayDebuff();
                    _stunAnimTimer = 1.6f;
                }
            }
            return;
        }

        if (BattleManager.Instance != null && !BattleManager.Instance.UnitsCanAct)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            if (BattleManager.Instance.PartyIntroWalking)
            {
                facingDir = 1;
                if (unitAnim != null) unitAnim.SetMove(true, facingDir);
            }
            else if (unitAnim != null)
            {
                unitAnim.SetMove(false, facingDir);
            }
            return;
        }

        if (TryHoldDuringAttack())
            return;
        if (TryHoldDuringDamaged())
            return;

        EnsureAiPolicy();

        // 牧师：有伤员且治疗技可放时，站住等自动奶。
        // 【2026-10-20 主人拍板】原实现无条件定身 return，两条路会脱队/死锁：
        // ① 定住时不过拴绳、不走 10-19 的「边打边跟」，玩家往前跑牧师钉在原地；
        // ② 施法失败（蓝不足回蓝慢 / merc_skills 表缺耗蓝 fail closed）时 CooldownRemain 一直 <= 0
        //    而 AllyNeedsHeal() 战斗中几乎恒真（任一队友含自己 < 92% 血）→ 无限站桩。
        // 现加两道保险：离玩家超过 MERC_STICK_RADIUS 不许定住；定住超过 MERC_HEAL_WAIT_MAX_SEC 强制跟队。
        // 治疗是 MercSkillCaster.Update 自动放的，不要求站着 —— 边走边奶成立，本分支只保留「奶人时站着」的表现。
        if (SkillCaster != null && SkillCaster.HasActiveSkill && SkillCaster.CooldownRemain <= 0f
            && AllyNeedsHeal())
        {
            var waitHero = Hero.Instance;
            bool tooFarFromHero = waitHero != null && !waitHero.isDead
                && Mathf.Abs(GetCombatX(this) - GetCombatX(waitHero)) > GameConfig.MERC_STICK_RADIUS;
            _healWaitTimer += Time.deltaTime;
            if (tooFarFromHero || _healWaitTimer > GameConfig.MERC_HEAL_WAIT_MAX_SEC)
            {
                _healWaitTimer = 0f;
                // 不定住，落到底部正常 AI（跟队 / 粘跟随），治疗照常自动放
            }
            else
            {
                if (rb != null) rb.velocity = Vector2.zero;
                if (unitAnim != null) unitAnim.SetMove(false, facingDir);
                ApplyLaneY(Time.deltaTime);
                return;
            }
        }
        else
        {
            _healWaitTimer = 0f;
        }

        // 拴绳：离玩家太远 / 偏远交战过久 → 清目标回队
        if (UpdateHeroLeash(Time.deltaTime))
        {
            bool recallingMove = false;
            FollowHeroOrIdle(ref recallingMove);
            if (unitAnim != null)
                unitAnim.SetMove(recallingMove, facingDir);
            if (BattleManager.Instance == null || !BattleManager.Instance.PortalWalkMode)
                ClampToScreen();
            ApplyLaneY(Time.deltaTime);
            return;
        }

        RefreshAcquireTarget();

        bool isMoving = false;
        if (target != null)
        {
            float distance = Mathf.Abs(GetCombatX(this) - GetCombatX(target));
            float attackRange = GetEffectiveAttackRange();
            bool melee = UsesMeleeBasicAttack();
            FaceToward(target);
            // 2026-10-09 主人拍板：已进入攻击范围则站桩不并道，抗怪跑动抖动
            if (!ShouldHoldGround(target) && melee)
                AdjustLaneTowardTarget(target, Time.deltaTime);

            if (IsInBasicAttackRange(target))
            {
                // 【2026-10-19 主人拍板】交战跟随上限：离玩家超过 MERC_STICK_RADIUS 就「边打边跟」——
                // 朝玩家方向移动（攻距内照常按 CD 开火），保证佣兵始终聚在玩家身边；
                // 半径内仍维持站桩不并道（2026-10-09 拍板不变）。
                var stickHero = Hero.Instance;
                bool mustStickBack = stickHero != null && !stickHero.isDead
                    && Mathf.Abs(GetCombatX(this) - GetCombatX(stickHero)) > GameConfig.MERC_STICK_RADIUS;
                if (mustStickBack)
                {
                    int backDir = GetCombatX(stickHero) > GetCombatX(this) ? 1 : -1;
                    if (rb != null)
                        rb.velocity = new Vector2(backDir * GetCombatMoveSpeed(), rb.velocity.y);
                    isMoving = true;
                    if (attackCd <= 0f
                        && (unitAnim == null || !unitAnim.InAttackLock))
                    {
                        Attack(target);
                        attackCd = GetAttackCooldown();
                    }
                }
                else
                {
                    if (rb != null) rb.velocity = Vector2.zero;
                    // 受击动画不挡出手：轮到攻击时 PlayAttack 立即打断 DAMAGED
                    if (attackCd <= 0f
                        && (unitAnim == null || !unitAnim.InAttackLock))
                    {
                        Attack(target);
                        attackCd = GetAttackCooldown();
                    }
                }
            }
            else if (melee && distance <= attackRange)
            {
                if (rb != null) rb.velocity = Vector2.zero;
                isMoving = true;
            }
            else
            {
                // 已锁目标但未进攻击距：朝目标走，朝向只看怪（不用走位方向翻身）
                if (rb != null)
                    rb.velocity = new Vector2(facingDir * GetCombatMoveSpeed(), rb.velocity.y);
                isMoving = true;
            }
        }
        else
        {
            // 索敌距内无怪：跟玩家站位 / 推图，朝右，不左右找怪
            FollowHeroOrIdle(ref isMoving);
        }

        if (unitAnim != null)
            unitAnim.SetMove(isMoving, facingDir);
        if (BattleManager.Instance == null || !BattleManager.Instance.PortalWalkMode)
            ClampToScreen();
        ApplyLaneY(Time.deltaTime);
    }

    /// <summary>
    /// 维护与玩家的拴绳。返回 true 表示本帧应只回队、不交战。
    /// </summary>
    bool UpdateHeroLeash(float dt)
    {
        var hero = Hero.Instance;
        if (hero == null || hero.isDead)
        {
            _recallingToHero = false;
            _awayFightTimer = 0f;
            return false;
        }

        float distHero = Mathf.Abs(GetCombatX(this) - GetCombatX(hero));

        if (_recallingToHero)
        {
            if (distHero <= GameConfig.MERC_REJOIN_RADIUS)
            {
                _recallingToHero = false;
                _awayFightTimer = 0f;
                return false;
            }
            ClearAcquireLock();
            return true;
        }

        if (distHero > GameConfig.MERC_LEASH_RADIUS)
        {
            BeginRecallToHero();
            return true;
        }

        // 偏远交战：打一会就回去找玩家
        bool fightingAway = target != null && !target.isDead && distHero > GameConfig.MERC_SOFT_AWAY_RADIUS;
        if (fightingAway)
        {
            _awayFightTimer += dt;
            if (_awayFightTimer >= GameConfig.MERC_AWAY_FIGHT_SEC)
            {
                BeginRecallToHero();
                return true;
            }
        }
        else
        {
            _awayFightTimer = 0f;
        }

        return false;
    }

    void BeginRecallToHero()
    {
        _recallingToHero = true;
        _awayFightTimer = 0f;
        ClearAcquireLock();
    }

    void ClearAcquireLock()
    {
        _acquireLock = null;
        target = null;
    }

    /// <summary>只认索敌距离：进距最近者锁定，直至死亡或出距。收队中不索敌。</summary>
    void RefreshAcquireTarget()
    {
        if (_recallingToHero)
        {
            ClearAcquireLock();
            return;
        }

        float detect = GetDetectRange();
        float myX = GetCombatX(this);
        const float leaveSlack = 0.75f;

        if (_acquireLock != null)
        {
            if (_acquireLock.isDead || _acquireLock.isAlly == isAlly)
                _acquireLock = null;
            else if (Mathf.Abs(myX - GetCombatX(_acquireLock)) > detect + leaveSlack)
                _acquireLock = null;
            else if (!GameConfig.IsInCombatViewport(_acquireLock))
                _acquireLock = null;
        }

        if (_acquireLock == null)
            _acquireLock = FindNearestEnemyInDetectRange();

        target = _acquireLock;
        if (target != null && target.isAlly == isAlly)
            ClearAcquireLock();
    }

    /// <summary>无锁定目标：贴玩家编队站位；玩家在推图则跟着走。</summary>
    void FollowHeroOrIdle(ref bool isMoving)
    {
        facingDir = 1;
        ApplyFacing(facingDir);
        // 2026-10-10 主人拍板：清场后不再换道归队（原 AdjustFormationLane 会把佣兵
        // 立刻搬回队形车道，看着像「打完敌人又往上走一步」）。场上还有怪时照常归队站位。
        if (HasAliveEnemyOnField())
            AdjustFormationLane(Time.deltaTime);

        var hero = Hero.Instance;
        if (hero == null || hero.isDead)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            isMoving = false;
            return;
        }

        float desiredX = UnitCrowd.GetMercDesiredCombatX(hero, this, ResolvePartyIndex());
        float dx = desiredX - GetCombatX(this);
        if (Mathf.Abs(dx) > 0.22f)
        {
            int moveDir = dx > 0f ? 1 : -1;
            // 【2026-10-19 主人拍板】追赶加速：落后编队位超过起步距离 → 比玩家快一档追回来。
            // 同速追赶永远追不上（见 GameConfig.MERC_CATCHUP_SPEED_MUL 注释），
            // 不加这条佣兵只会越掉越远。回队（拴绳触发）也走这里，一并享受加速。
            float speed = GetCombatMoveSpeed();
            if (Mathf.Abs(dx) > GameConfig.MERC_CATCHUP_ENTER_DIST)
                speed *= GameConfig.MERC_CATCHUP_SPEED_MUL;
            if (rb != null)
                rb.velocity = new Vector2(moveDir * speed, rb.velocity.y);
            isMoving = true;
        }
        else if (!HasAliveEnemyOnField())
        {
            // 场上无怪：与玩家一起右推
            if (rb != null)
                rb.velocity = new Vector2(GetCombatMoveSpeed(), rb.velocity.y);
            isMoving = true;
        }
        else
        {
            // 场上有怪但不在自己索敌距：站住跟队，等怪进距再锁
            if (rb != null) rb.velocity = Vector2.zero;
            isMoving = false;
        }
    }
}
