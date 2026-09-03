using UnityEngine;

/// <summary>
/// 佣兵：与玩家同一套战斗 AI（索敌 → 攻击/前压 → 无目标则右推）。
/// </summary>
public class Mercenary : UnitBase
{
    public string mercId;
    public string hireId;
    public int mercLevel = 1;
    public string DisplayName { get; private set; }
    /// <summary>本局佩戴主动技能（来自存档；空则无主动技）</summary>
    public string equippedSkillId;
    /// <summary>本局佩戴被动技能</summary>
    public string equippedPassiveSkillId;

    public MercSkillCaster SkillCaster { get; private set; }
    public MercPassiveRunner PassiveRunner { get; private set; }

    private int _partyIndex = -1;
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

    public override void TakeDamage(float damage, bool isCrit, bool ignoreDefense = false, bool showHitVfx = true, int hitVfxFacing = 0, UnitBase source = null)
    {
        if (TutorialStunned)
        {
            float defense = ignoreDefense ? 0f : attr.GetAttr(AttrType.Defense);
            float finalDamage = Mathf.Max(1f, damage - defense);
            currentHp = Mathf.Max(1f, currentHp - finalDamage * 0.35f);
            DamageTextSystem.Instance?.SpawnDamageText(GetHitPosition(), Mathf.RoundToInt(finalDamage * 0.35f), isCrit, true, hitVfxFacing);
            if (unitAnim != null)
            {
                unitAnim.PlayDamaged();
                _stunAnimTimer = 0.35f;
            }
            return;
        }

        if (PassiveRunner != null)
            damage = PassiveRunner.ModifyIncomingDamage(damage);

        float before = currentHp;
        base.TakeDamage(damage, isCrit, ignoreDefense, showHitVfx, hitVfxFacing, source);
        if (PassiveRunner != null && !Mathf.Approximately(before, currentHp))
            PassiveRunner.OnHpChanged();
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

    public void SetHireId(string id)
    {
        hireId = id;
    }

    public void SetDisplayName(string displayName, string nickname = null)
    {
        if (!string.IsNullOrEmpty(nickname))
            DisplayName = nickname;
        else if (!string.IsNullOrEmpty(displayName))
            DisplayName = displayName;
        else
        {
            string job = MercenaryManager.Instance != null
                ? MercenaryManager.Instance.GetJobName(mercId)
                : null;
            DisplayName = string.IsNullOrEmpty(job) ? mercId : job;
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
        var rarity = MercRarityColors.ResolveMercRarity(mercId);
        _nameLabel.color = MercRarityColors.Get(rarity);
        if (_nameOutlineLabels == null) return;
        var outline = MercRarityColors.GetOutline();
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
            o.color = MercRarityColors.GetOutline();
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

    static bool IsMeleeMercId(string id)
    {
        if (string.IsNullOrEmpty(id)) return true;
        if (id.StartsWith("gongshou")) return false;
        if (id.StartsWith("naima") || id.StartsWith("fashi") || id.StartsWith("mushi")) return false;
        return true;
    }

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
        level = Mathf.Max(1, level);

        if (MercRosterDefs.TryGetByAssetId(id, out _))
        {
            MercRosterDefs.ApplyCombatStats(id, level,
                out float hp, out float atk, out float def, out float atkSpd, out float move, out float range);
            attr.SetAttr(AttrType.MaxHp, hp);
            attr.SetAttr(AttrType.Attack, atk);
            attr.SetAttr(AttrType.Defense, def);
            attr.SetAttr(AttrType.AttackSpeed, atkSpd);
            attr.SetAttr(AttrType.MoveSpeed, move);
            attr.SetAttr(AttrType.AttackRange, range);
            attr.SetAttr(AttrType.CritRate, GameConfig.BASE_CRIT_RATE);
            currentHp = attr.GetAttr(AttrType.MaxHp);
            ApplyMeleeRangeVsPlayer();
            return;
        }

        bool advanced = GameConfig.GetMercTier(id) == MercTier.Advanced;
        float baseHp, baseAtk, baseDef, atkInterval;
        float atkRange = GameConfig.RangeSword;

        if (id.StartsWith("dunbing"))
        {
            if (advanced) { baseHp = 550; baseAtk = 18; baseDef = 20; atkInterval = 1.1f; }
            else { baseHp = 300; baseAtk = 10; baseDef = 10; atkInterval = 1.2f; }
            atkRange = GameConfig.RangeSword;
        }
        else if (id.StartsWith("gongshou"))
        {
            if (advanced) { baseHp = 280; baseAtk = 35; baseDef = 5; atkInterval = 0.85f; }
            else { baseHp = 150; baseAtk = 20; baseDef = 3; atkInterval = 0.9f; }
            atkRange = GameConfig.RangeBow;
        }
        else if (id.StartsWith("kuangzhan"))
        {
            if (advanced) { baseHp = 280; baseAtk = 35; baseDef = 5; atkInterval = 0.85f; }
            else { baseHp = 150; baseAtk = 20; baseDef = 3; atkInterval = 0.9f; }
            atkRange = GameConfig.RangeSword;
        }
        else if (id.StartsWith("naima") || id.StartsWith("fashi") || id.StartsWith("mushi"))
        {
            if (advanced) { baseHp = 320; baseAtk = 15; baseDef = 8; atkInterval = 1.3f; }
            else { baseHp = 180; baseAtk = 8; baseDef = 4; atkInterval = 1.5f; }
            atkRange = GameConfig.RangeStaff;
        }
        else if (id.StartsWith("zhongzhan"))
        {
            if (advanced) { baseHp = 360; baseAtk = 22; baseDef = 10; atkInterval = 1f; }
            else { baseHp = 200; baseAtk = 12; baseDef = 5; atkInterval = 1.1f; }
            atkRange = GameConfig.RangePolearm;
        }
        else
        {
            if (advanced) { baseHp = 360; baseAtk = 22; baseDef = 10; atkInterval = 1f; }
            else { baseHp = 200; baseAtk = 12; baseDef = 5; atkInterval = 1.1f; }
            atkRange = GameConfig.RangePolearm;
        }

        float hpMul = 1f + (level - 1) * 0.1f;
        float atkAdd = (level - 1) * 2f;
        attr.SetAttr(AttrType.MaxHp, baseHp * hpMul);
        attr.SetAttr(AttrType.Attack, baseAtk + atkAdd);
        attr.SetAttr(AttrType.Defense, baseDef);
        attr.SetAttr(AttrType.AttackSpeed, 1f / Mathf.Max(0.2f, atkInterval));
        attr.SetAttr(AttrType.MoveSpeed, GameConfig.BASE_MOVE_SPEED);
        attr.SetAttr(AttrType.AttackRange, atkRange);
        attr.SetAttr(AttrType.CritRate, GameConfig.BASE_CRIT_RATE);

        currentHp = attr.GetAttr(AttrType.MaxHp);
        ApplyMeleeRangeVsPlayer();
    }

    protected override WeaponAttackType GetAttackType()
    {
        if (mercId == null) return WeaponAttackType.Physical;
        if (mercId.StartsWith("gongshou"))
            return WeaponAttackType.Physical;
        if (mercId.StartsWith("naima") || mercId.StartsWith("fashi") || mercId.StartsWith("mushi"))
            return WeaponAttackType.Magic;
        return WeaponAttackType.Physical;
    }

    protected override AttackVfxKit GetAttackVfxKit()
    {
        if (mercId != null && mercId.StartsWith("gongshou"))
            return AttackVfxKit.Bow;
        if (mercId != null && (mercId.StartsWith("naima") || mercId.StartsWith("fashi") || mercId.StartsWith("mushi")))
            return AttackVfxKit.Orb;
        return AttackVfxKit.MeleeSlash;
    }

    protected override float GetFormationLaneOffset()
    {
        if (_partyIndex == 0) return -0.35f;
        if (_partyIndex == 1) return 0.35f;
        return 0f;
    }

    protected override void OnDeathRelease()
    {
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
                ApplyFacing(facingDir);
                if (unitAnim != null) unitAnim.SetMove(true, facingDir);
            }
            else if (unitAnim != null)
            {
                unitAnim.SetMove(false, facingDir);
            }
            return;
        }

        // 与玩家同一套：索敌 → 进射程攻击 / 否则前压；无目标则向右推进
        base.AIUpdate();
    }
}
