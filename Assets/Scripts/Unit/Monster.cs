using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// ????Monstersmoban ???????? + ??????????MonsterBody ??????????
/// ????
///   MonsterBody????Rigidbody2D??????
///   ??? Visual / Monstersmoban?Animator?HPBar?Monsters ???beattack/fire??
/// </summary>
public class Monster : UnitBase
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

    // ????????SpriteRenderer ???
    private SpriteRenderer _hpBarFill;
    private SpriteRenderer _hpBarBg;
    private Transform _hpBarRoot;
    private MonsterHealthBar _worldHpBar;
    private Transform _stackLabelRoot;
    private TextMesh _stackLabel;
    private MeshRenderer _stackLabelRenderer;
    private TextMesh[] _stackOutlineLabels;
    private MeshRenderer[] _stackOutlineRenderers;

    const float StackLabelCharSizeBase = 0.28f;
    /// <summary>???????? 70%????30%???/summary>
    const float StackLabelSizeScale = 0.3f;
    static readonly Vector2[] StackOutlineDirs =
    {
        new Vector2(-1f, 0f), new Vector2(1f, 0f),
        new Vector2(0f, -1f), new Vector2(0f, 1f)
    };
    static int s_hpBarFrontBoost;

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

    /// <summary>????????HPBar ???Init ????????</summary>
    void FindHPBar()
    {
        _hpBarRoot = transform.Find("HPBar");
        if (_hpBarRoot == null)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "HPBar") { _hpBarRoot = child; break; }
            }
        }
        if (_hpBarRoot == null) return;

        Transform fill = _hpBarRoot.Find("HPBarFill");
        if (fill != null)
            _hpBarFill = fill.GetComponent<SpriteRenderer>();

        Transform bg = _hpBarRoot.Find("HPBarBG") ?? _hpBarRoot.Find("HPBarBg");
        if (bg != null)
        {
            var bgSr = bg.GetComponent<SpriteRenderer>();
            if (bgSr != null)
            {
                bgSr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
                bgSr.sortingOrder = GameConfig.SORT_UNIT;
            }
        }
    }

    void NormalizeHPBarLayout(float monsterRootScale)
    {
        if (_hpBarRoot == null) return;

        float spriteWidth = 0.32f;
        if (sr != null && sr.sprite != null)
            spriteWidth = sr.sprite.bounds.size.x;

        // ????????????? 85%?????????? scale
        float monsterWorldWidth = spriteWidth * monsterRootScale;
        float barSpriteWidth = 1.01f;
        if (_hpBarFill != null && _hpBarFill.sprite != null)
            barSpriteWidth = Mathf.Max(0.01f, _hpBarFill.sprite.bounds.size.x);
        float barLocalScale = (monsterWorldWidth * 0.65f) / (barSpriteWidth * Mathf.Max(0.5f, monsterRootScale));
        barLocalScale = Mathf.Clamp(barLocalScale, 0.22f, 2f);
        _hpBarRoot.localScale = Vector3.one * barLocalScale;

        ApplyAnchorPosition(_hpBarRoot, 0f, ResolveHpBarFootLocalY());

        if (_hpBarFill != null)
        {
            _hpBarFillBaseWidth = _hpBarFill.sprite != null
                ? _hpBarFill.sprite.bounds.size.x
                : 1f;
            var fillT = _hpBarFill.transform;
            fillT.localScale = Vector3.one;
            fillT.localPosition = Vector3.zero;
        }
    }

    float ResolveHpBarFootLocalY()
    {
        float footY = GameConfig.MONSTER_HP_BAR_FOOT_LOCAL_Y;
        if (sr != null && sr.sprite != null)
            footY = Mathf.Min(footY, sr.sprite.bounds.min.y + 0.02f);
        // 锚在脚面略上，禁止被旧 clamp 抬到头部
        return Mathf.Clamp(footY + 0.04f, -3.5f, 0.06f);
    }

    System.Collections.IEnumerator RefreshHpBarLayoutAfterSpriteReady(float rootScale)
    {
        yield return null;
        yield return null;
        if (_worldHpBar != null)
        {
            _worldHpBar.ApplyBarMetricsFromUnit();
        }
    }

    void DisableEmbeddedHpBar()
    {
        FindHPBar();
        if (_hpBarRoot != null)
            _hpBarRoot.gameObject.SetActive(false);

        var embeddedUi = GetComponentsInChildren<MonsterHealthBar>(true);
        for (int i = 0; i < embeddedUi.Length; i++)
        {
            if (embeddedUi[i] == null || embeddedUi[i] == _worldHpBar) continue;
            if (embeddedUi[i].transform.IsChildOf(transform))
                embeddedUi[i].gameObject.SetActive(false);
        }
    }

    void EnsureWorldHealthBar()
    {
        DisableEmbeddedHpBar();
        _worldHpBar = MonsterHealthBar.Create(this);
    }

    public override float GetHpBarWorldWidth()
    {
        if (sr != null && sr.sprite != null)
            return Mathf.Max(0.16f, sr.bounds.size.x * GameConfig.MONSTER_HP_BAR_WIDTH_MUL);
        return base.GetHpBarWorldWidth();
    }

    void ApplyMonsterVisualScaleRules()
    {
        Transform visual = _visualRoot != null ? _visualRoot : transform;
        visual.localScale = Vector3.one;

        Transform monstersChild = transform.Find("Monsters");
        if (monstersChild != null)
        {
            var mp = monstersChild.localPosition;
            mp.z = 0f;
            monstersChild.localPosition = mp;
            // Monsters ?? scale ? ani ????? 1
            monstersChild.localScale = Vector3.one * GameConfig.MONSTER_CHILD_REF_SCALE;
        }
    }

    /// <summary>??/??????????????????LoadSprite ??????/summary>
    void NormalizeMonsterAnchorNodes()
    {
        float rootAbs = Mathf.Max(0.01f, Mathf.Abs(MoveRoot.lossyScale.y));
        float localY = 0.55f / rootAbs;
        float localFireX = -0.12f / rootAbs;
        ApplyAnchorPosition(transform.Find("beattack"), 0f, localY);
        ApplyAnchorPosition(transform.Find("fire"), localFireX, localY);
        Transform be = transform.Find("beattack");
        if (be != null)
        {
            hitPoint = be;
            // ???????????????????????????
            TryPlaceHitPointByOpaqueSprite(be);
            StartCoroutine(CalcHitPointCenter(be));
        }
        Transform fire = transform.Find("fire");
        if (fire != null)
        {
            firePoint = fire;
            StartCoroutine(CalcFirePointCenter(fire));
        }
    }

    System.Collections.IEnumerator CalcFirePointCenter(Transform fireTransform)
    {
        yield return null;
        yield return null;
        if (fireTransform == null) yield break;

        // ?????????????????????????????????
        if (!TryGetBodyBounds(out Bounds body))
            yield break;

        Vector3 center = hitPoint != null ? hitPoint.position : GetBodyCenterWorld(body);
        float side = Mathf.Max(0.12f, body.size.x * 0.35f);
        bool toLeft = fireTransform.localPosition.x < 0f;
        fireTransform.position = new Vector3(center.x + (toLeft ? -side : side), center.y, transform.position.z);
        Vector3 lp = fireTransform.localPosition;
        fireTransform.localPosition = new Vector3(lp.x, lp.y, 0f);
    }

    static void ApplyAnchorPosition(Transform t, float x, float y)
    {
        if (t == null) return;
        if (t is RectTransform rt)
            rt.anchoredPosition3D = new Vector3(x, y, 0f);
        else
            t.localPosition = new Vector3(x, y, 0f);
    }

    private float _hpBarFillBaseWidth = 1f;
    private string _skillId;
    private bool _canUseActiveSkill;
    private float _skillEnergy = 0f;
    private float _skillCooldown = 0f;
    private MonsterAttackStyle _attackStyle = MonsterAttackStyle.Melee;
    private MonsterAttackStyle _swingStyle = MonsterAttackStyle.Melee;
    private int _spriteIndex;
    private bool _isBossUnit;
    private bool _eliteWave;
    private int _bossSwingIndex;
    private bool _isEnteringMap;
    private Vector3 _enterTargetPos;
    private float _enterSpeed = 1.6f;

    /// <summary>从地图边缘缓步走向交战点；faceDir 为入场朝向（左进场朝右=1，右进场朝左=-1）</summary>
    public void BeginMapEnter(Vector3 engagePos, float speed, int faceDir = -1)
    {
        _enterTargetPos = engagePos;
        _enterSpeed = Mathf.Max(0.4f, speed);
        _isEnteringMap = true;
        facingDir = faceDir > 0 ? 1 : -1;
        ApplyFacing(facingDir);
        if (rb != null) rb.velocity = Vector2.zero;
    }

    UnitBase _forcedTarget;

    /// <summary>?????????????????????/summary>
    public void SetForcedTarget(UnitBase t) => _forcedTarget = t;

    protected override void AIUpdate()
    {
        if (_forcedTarget != null)
        {
            if (_forcedTarget.isDead)
                _forcedTarget = null;
            else
            {
                if (_isEnteringMap) _isEnteringMap = false;
                target = _forcedTarget;
                // ?????????????????
                RunForcedCombat();
                return;
            }
        }

        if (_isEnteringMap)
        {
            if (BattleManager.Instance != null && !BattleManager.Instance.UnitsCanAct)
            {
                if (rb != null) rb.velocity = Vector2.zero;
                return;
            }

            UnitBase foe = FindNearestEnemyOnField();
            if (foe != null)
            {
                _isEnteringMap = false;
                target = foe.isAlly == isAlly ? null : foe;
                if (target == null)
                {
                    AdvanceTowardEnemies();
                    return;
                }
                RunForcedCombat();
                return;
            }

            float dx = _enterTargetPos.x - MoveRoot.position.x;
            if (Mathf.Abs(dx) <= 0.08f)
            {
                GameConfig.SetWorldPosition(MoveRoot, new Vector3(_enterTargetPos.x, FootY, MoveRoot.position.z));
                _isEnteringMap = false;
                AdvanceTowardEnemies();
                return;
            }

            int enterFace = _enterTargetPos.x >= MoveRoot.position.x ? 1 : -1;
            facingDir = enterFace;
            ApplyFacing(facingDir);
            float step = _enterSpeed * Time.deltaTime;
            float nx = Mathf.MoveTowards(MoveRoot.position.x, _enterTargetPos.x, step);
            GameConfig.SetWorldPosition(MoveRoot, new Vector3(nx, FootY, MoveRoot.position.z));
            if (unitAnim != null) unitAnim.SetMove(true, facingDir);
            return;
        }

        // ????????????????????????
        target = FindNearestEnemyOnField();
        if (target != null && target.isAlly == isAlly)
            target = null;
        if (target == null)
        {
            AdvanceTowardEnemies();
            return;
        }

        RunForcedCombat();
    }

    /// <summary>?????????????????????????????/summary>
    UnitBase FindNearestEnemyOnField()
    {
        if (BattleManager.Instance == null) return null;
        UnitBase nearest = null;
        float minDist = float.MaxValue;
        float myX = GetCombatX(this);
        var allies = BattleManager.Instance.allyUnits;
        if (allies == null) return null;
        for (int i = 0; i < allies.Count; i++)
        {
            var enemy = allies[i];
            if (enemy == null || enemy.isDead) continue;
            float dist = Mathf.Abs(myX - GetCombatX(enemy));
            if (dist < minDist)
            {
                minDist = dist;
                nearest = enemy;
            }
        }
        return nearest;
    }

    void RunForcedCombat()
    {
        if (BattleManager.Instance != null && !BattleManager.Instance.UnitsCanAct)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            if (unitAnim != null) unitAnim.SetMove(false, facingDir);
            return;
        }
        if (target == null || target.isDead) return;

        float distance = Mathf.Abs(GetCombatX(this) - GetCombatX(target));
        float attackRange = attr.GetAttr(AttrType.AttackRange);
        FaceToward(target);

        bool isMoving = false;
        bool inRange = distance <= attackRange;
        if (inRange)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            if (attackCd <= 0)
            {
                Attack(target);
                attackCd = GetAttackCooldown();
            }
        }
        else
        {
            float spd = attr.GetAttr(AttrType.MoveSpeed);
            if (rb != null) rb.velocity = new Vector2(facingDir * spd, rb.velocity.y);
            isMoving = true;
        }

        if (unitAnim != null) unitAnim.SetMove(isMoving, facingDir);
    }

    void AdvanceTowardEnemies()
    {
        if (BattleManager.Instance != null && !BattleManager.Instance.UnitsCanAct)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            if (unitAnim != null) unitAnim.SetMove(false, facingDir);
            return;
        }

        UnitBase foe = FindNearestEnemyOnField();
        float dir;
        if (foe != null)
        {
            target = foe;
            float dist = Mathf.Abs(GetCombatX(foe) - GetCombatX(this));
            float attackRange = attr.GetAttr(AttrType.AttackRange);
            if (dist <= attackRange)
            {
                RunForcedCombat();
                return;
            }
            dir = GetCombatX(foe) > GetCombatX(this) ? 1f : -1f;
        }
        else
        {
            dir = -1f;
        }

        facingDir = (int)dir;
        ApplyFacing(facingDir);

        float spd = attr.GetAttr(AttrType.MoveSpeed);
        if (rb != null) rb.velocity = new Vector2(dir * spd, rb.velocity.y);
        if (unitAnim != null) unitAnim.SetMove(true, facingDir);
    }

    /// <summary>
    /// ?????
    /// </summary>
    /// <param name="scaleMultiplier">????????.0 / ??1.5 / Boss2.0</param>
    /// <param name="spriteIndexOverride">???????1-12?????????????????spriteIndex</param>
    public void Init(MonsterConfig template, int waveNum, int chapter = 1, float scaleMultiplier = 1f, int spriteIndexOverride = 0)
    {
        _chapter = chapter;
        config = template;
        gameObject.name = "Visual";

        ResetForReuse();
        EnsureBodyRoot();
        MoveRoot.name = template.id;

        // ?? RectTransform?Visual ??????
        RectTransform rootRT = GetComponent<RectTransform>();
        if (rootRT != null)
        {
            Vector3 worldPos = MoveRoot.position;
            rootRT.anchorMin = new Vector2(0.5f, 0.5f);
            rootRT.anchorMax = new Vector2(0.5f, 0.5f);
            rootRT.pivot = new Vector2(0.5f, 0.5f);
            GameConfig.SetWorldPosition(MoveRoot, worldPos);
            transform.localPosition = Vector3.zero;
        }

        // ?????? Body?Visual ?? 1??????????????
        bool eliteWave = scaleMultiplier >= GameConfig.ELITE_SCALE_MULTIPLIER - 0.05f
                         && scaleMultiplier < GameConfig.BOSS_SCALE_MULTIPLIER - 0.05f;
        bool bossUnit = (template != null && template.isBoss) || scaleMultiplier >= GameConfig.BOSS_SCALE_MULTIPLIER - 0.05f;
        float rootScale = GameConfig.RollMonsterRootScale(eliteWave, bossUnit);
        GameConfig.AttachToUnitRoot(MoveRoot);
        MoveRoot.localScale = Vector3.one * rootScale;
        ApplyMonsterVisualScaleRules();
        // ??????????????????????????????????offset??
        int effectiveSpriteIndex = spriteIndexOverride > 0 ? spriteIndexOverride : template.spriteIndex;
        _spriteIndex = effectiveSpriteIndex;
        LoadSprite(template, chapter, effectiveSpriteIndex);
        NormalizeMonsterAnchorNodes();

        // ??????????????????????
        if (template != null)
        {
            if (!string.IsNullOrEmpty(template.id))
                AdventureCodex.MarkMonsterSeen(template.id);
            // ?????? ??forest_4xx ??
            int mc = GameConfig.GetMonsterChapter(chapter);
            string guess = AdventureCodex.GuessAssetIdFromSprite(mc, effectiveSpriteIndex);
            if (!string.IsNullOrEmpty(guess))
                AdventureCodex.MarkMonsterSeen(guess);
        }

        // ????????Monsters/ani ?????attack/run/idle/dead??
        if (unitAnim != null)
        {
            unitAnim.EnableMonsterClipAnimator(sr);
            unitAnim.SetFlipXFacing(false);
            unitAnim.RecacheBaseScale();
            ApplyMonsterVisualScaleRules();
            unitAnim.StabilizeMonsterBodyTransform();
        }

        int monsterChapter = GameConfig.GetMonsterChapter(chapter);
        _attackStyle = MonsterAttackStyleTable.Get(monsterChapter, Mathf.Max(1, effectiveSpriteIndex));
        _isBossUnit = bossUnit || effectiveSpriteIndex >= GameConfig.BOSS_SPRITE_START;
        _swingStyle = _attackStyle;
        _bossSwingIndex = 0;

        // ??????????????????/????
        attr.ResetToBase();
        int guildLv = SaveSystem.Instance?.Data?.guildLevel ?? 0;
        float chapterScale = 1f + GameConfig.CHAPTER_SCALE_PER * Mathf.Max(0, chapter - 1);
        float guildScale = 1f + GameConfig.GUILD_SCALE_PER * guildLv;
        float diffScale = BattleManager.Instance != null ? BattleManager.Instance.DifficultyStatScale : 1f;
        float scale = chapterScale * guildScale * diffScale;

        float baseHp = template != null && template.baseHp > 0 ? template.baseHp : GameConfig.MONSTER_NORMAL_HP;
        float baseAtk = template != null && template.baseAttack > 0 ? template.baseAttack : GameConfig.MONSTER_NORMAL_ATK;
        float baseDef = GameConfig.MONSTER_NORMAL_DEF;
        float atkInterval = GameConfig.MONSTER_NORMAL_ATK_INTERVAL;

        if (bossUnit)
        {
            if (template == null || template.baseHp < GameConfig.MONSTER_BOSS_HP * 0.5f)
                baseHp = GameConfig.MONSTER_BOSS_HP;
            if (template == null || template.baseAttack < GameConfig.MONSTER_BOSS_ATK * 0.5f)
                baseAtk = GameConfig.MONSTER_BOSS_ATK;
            baseDef = GameConfig.MONSTER_BOSS_DEF;
            atkInterval = GameConfig.MONSTER_BOSS_ATK_INTERVAL;
        }
        else if (eliteWave)
        {
            baseHp = GameConfig.MONSTER_ELITE_HP;
            baseAtk = GameConfig.MONSTER_ELITE_ATK;
            baseDef = GameConfig.MONSTER_ELITE_DEF;
            atkInterval = GameConfig.MONSTER_ELITE_ATK_INTERVAL;
        }

        float waveMul = 1f + waveNum * 0.05f;
        float ttkMul = (bossUnit || eliteWave)
            ? WeaponCombatTable.EliteBossHpMul(monsterChapter, bossUnit)
            : (1f + GameConfig.CHAPTER_SCALE_PER * Mathf.Max(0, chapter - 1));
        // ?????? chapterScale????Boss ??TTK ????????
        float hpScale = (bossUnit || eliteWave) ? (guildScale * diffScale * ttkMul) : (scale);
        attr.SetAttr(AttrType.MaxHp, baseHp * hpScale * waveMul * GameConfig.MONSTER_HP_GLOBAL_MUL);
        attr.SetAttr(AttrType.Attack, baseAtk * scale * waveMul * GameConfig.MONSTER_DAMAGE_MULTIPLIER);
        attr.SetAttr(AttrType.Defense, baseDef * scale);
        float atkSpeedMul = GameConfig.MONSTER_ATK_SPEED_MUL;
        if (BattleManager.Instance != null)
            atkSpeedMul *= BattleManager.Instance.runMonsterAtkSpeedMul;
        if (template != null && template.baseAttackSpeed > 0.01f)
            atkSpeedMul *= template.baseAttackSpeed;
        attr.SetAttr(AttrType.AttackSpeed,
            (1f / Mathf.Max(0.2f, atkInterval)) * atkSpeedMul);
        // ??????????40%?????0.6??
        if (!_isBossUnit && MonsterAttackStyleTable.IsRanged(_attackStyle))
        {
            attr.SetAttr(AttrType.AttackSpeed,
                attr.GetAttr(AttrType.AttackSpeed) / 0.6f);
        }
        float moveSpd = template != null && template.baseMoveSpeed > 0.01f
            ? Mathf.Min(template.baseMoveSpeed, GameConfig.MONSTER_DEFAULT_MOVE_SPEED * 1.5f)
            : GameConfig.MONSTER_DEFAULT_MOVE_SPEED;
        // Boss??????????????????????
        float atkRange;
        if (_isBossUnit)
        {
            atkRange = MonsterAttackStyleTable.GetAttackRange(MonsterAttackStyle.Ranged);
        }
        else
        {
            atkRange = MonsterAttackStyleTable.GetAttackRange(_attackStyle);
            if (template != null && template.attackRange > 0.01f)
            {
                float tpl = GameConfig.NormalizeAttackRange(template.attackRange);
                if (MonsterAttackStyleTable.IsRanged(_attackStyle))
                    atkRange = Mathf.Max(atkRange, tpl);
                else
                    atkRange = tpl;
            }
        }
        attr.SetAttr(AttrType.MoveSpeed, moveSpd);
        attr.SetAttr(AttrType.AttackRange, atkRange);
        attr.SetAttr(AttrType.CritRate, 0.05f);

        if (GameConfig.IsOpeningStage() && !bossUnit)
        {
            attr.SetAttr(AttrType.MaxHp, attr.GetAttr(AttrType.MaxHp) * 1.25f);
            attr.SetAttr(AttrType.Attack, attr.GetAttr(AttrType.Attack) * 0.7f);
            attr.SetAttr(AttrType.AttackSpeed, attr.GetAttr(AttrType.AttackSpeed) * 0.7f);
            attr.SetAttr(AttrType.MoveSpeed, attr.GetAttr(AttrType.MoveSpeed) * 0.75f);
        }

        currentHp = attr.GetAttr(AttrType.MaxHp);
        if (currentHp <= 0f)
            currentHp = Mathf.Max(1f, GameConfig.MONSTER_NORMAL_HP);
        isAlly = false; // ??????????
        float goldMul = BattleManager.Instance != null ? BattleManager.Instance.DifficultyGoldMul : 1f;
        if (BattleManager.Instance != null && BattleManager.Instance.IsGoldDungeon)
            goldMul *= 2f;
        goldDrop = Mathf.FloorToInt((template != null ? template.baseGoldDrop : 5) * (1 + waveNum * 0.1f) * scale * goldMul);
        expDrop = Mathf.FloorToInt((template != null ? template.expDrop : 3) * (1 + waveNum * 0.1f) * scale);

        // ??????????????
        facingDir = -1;
        ApplyFacing(-1);

        // 血条挂在 MonsterBody（与 Visual 平级），运行时生成，避免镜像翻转
        EnsureWorldHealthBar();
        StartCoroutine(RefreshHpBarLayoutAfterSpriteReady(rootScale));

        // ??????
        ApplySortingLayer();
        RemovePhysicsCollider();

        // ?? HPBar ???????????
        SetupHPBarSorting();

        _eliteWave = eliteWave;
        _skillId = SkillRegistry.Instance != null
            ? SkillRegistry.Instance.GetMonsterSkillId(template, eliteWave, _isBossUnit, _attackStyle)
            : null;
        // ???????????id????????????????????
        _canUseActiveSkill = !string.IsNullOrEmpty(_skillId);
        bool strong = eliteWave || _isBossUnit;
        bool rangedSkill = _canUseActiveSkill && MonsterAttackStyleTable.IsRanged(_attackStyle);
        _skillEnergy = _canUseActiveSkill
            ? (strong ? 0.5f : (rangedSkill ? 0.88f : 0f))
            : 0f;
        _skillCooldown = _canUseActiveSkill && !strong ? (rangedSkill ? 0f : 3f) : 0f;

        Debug.Log($"[Monster:{template.id}] Init | mCh={monsterChapter} sprite={_spriteIndex} style={_attackStyle} kit={MonsterAttackStyleTable.GetVfxKit(_attackStyle)} boss={_isBossUnit} range={atkRange:F1} skill={_skillId} vfxSys={(BattleVFXSystem.Instance != null)}");
    }

    /// <summary>??????? footprint??????AABB?????????????/summary>
    public void GetFootprintBounds(out float minX, out float maxX, out float minY, out float maxY)
    {
        float halfW = GetOpaqueFootprintHalfWidth();
        float cx = UnitBase.GetCombatX(this);
        float footY = MoveRoot.position.y;
        float height = halfW * 2.2f;
        if (sr != null && sr.sprite != null
            && MonsterSpriteOpaqueTable.TryGet(sr.sprite.name, out MonsterSpriteOpaqueTable.Entry e))
        {
            height = Mathf.Max(0.35f, sr.bounds.size.y * Mathf.Clamp(e.BoxNH, 0.2f, 1f));
        }
        minX = cx - halfW;
        maxX = cx + halfW;
        minY = footY;
        maxY = footY + height;
    }

    /// <summary>?????????????????????bounds???/summary>
    public float GetOpaqueFootprintHalfWidth()
    {
        if (sr == null || sr.sprite == null) return UnitCrowd.MonsterFallbackHalfWidth;
        if (MonsterSpriteOpaqueTable.TryGet(sr.sprite.name, out MonsterSpriteOpaqueTable.Entry e))
        {
            float boxW = Mathf.Clamp(e.BoxNW, 0.12f, 1f);
            float worldW = sr.bounds.size.x * boxW;
            return Mathf.Max(UnitCrowd.MonsterFallbackHalfWidth, worldW * 0.5f);
        }
        return UnitCrowd.MonsterFallbackHalfWidth;
    }

    void RemovePhysicsCollider()
    {
        var box = GetComponent<BoxCollider2D>();
        if (box != null) Destroy(box);
    }

    public void SetOverlapStackCount(int count)
    {
        if (count <= 1)
        {
            if (_stackLabelRoot != null) _stackLabelRoot.gameObject.SetActive(false);
            return;
        }

        EnsureStackLabel();
        ApplyStackLabelFont();
        RefreshStackLabelLayout();
        _stackLabelRoot.gameObject.SetActive(true);
        string label = "x" + count;
        _stackLabel.text = label;
        SyncStackOutlineText(label);
    }

    void SyncStackOutlineText(string label)
    {
        if (_stackOutlineLabels == null) return;
        for (int i = 0; i < _stackOutlineLabels.Length; i++)
        {
            if (_stackOutlineLabels[i] != null)
                _stackOutlineLabels[i].text = label;
        }
    }

    void ApplyStackLabelFont()
    {
        if (_stackLabel == null) return;
        var font = GameFonts.GetNumber();
        if (font == null) return;
        font.RequestCharactersInTexture("x0123456789", _stackLabel.fontSize, FontStyle.Normal);

        _stackLabel.font = font;
        if (_stackLabelRenderer != null && font.material != null)
            _stackLabelRenderer.sharedMaterial = font.material;

        if (_stackOutlineLabels == null) return;
        for (int i = 0; i < _stackOutlineLabels.Length; i++)
        {
            var o = _stackOutlineLabels[i];
            if (o == null) continue;
            o.font = font;
            if (_stackOutlineRenderers != null && i < _stackOutlineRenderers.Length
                && _stackOutlineRenderers[i] != null && font.material != null)
                _stackOutlineRenderers[i].sharedMaterial = font.material;
        }
    }

    void RefreshStackLabelLayout()
    {
        if (_stackLabelRoot == null || _stackLabel == null) return;

        float labelY = 0.85f;
        if (sr != null && sr.sprite != null)
        {
            var topLocal = transform.InverseTransformPoint(sr.bounds.max);
            labelY = Mathf.Max(labelY, topLocal.y + 0.12f);
        }
        else if (_worldHpBar != null)
        {
            labelY = transform.InverseTransformPoint(MoveRoot.position).y + 0.14f;
        }

        float rootAbs = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.y));
        _stackLabelRoot.localPosition = new Vector3(0f, labelY / rootAbs, 0f);
        float charSize = StackLabelCharSizeBase * StackLabelSizeScale / rootAbs;
        _stackLabel.characterSize = charSize;

        if (_stackLabelRenderer != null)
        {
            _stackLabelRenderer.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            _stackLabelRenderer.sortingOrder = GameConfig.SORT_VFX + 30;
        }

        float outlineStep = charSize * 0.2f;
        if (_stackOutlineLabels == null) return;
        for (int i = 0; i < _stackOutlineLabels.Length; i++)
        {
            var o = _stackOutlineLabels[i];
            if (o == null) continue;
            o.characterSize = charSize;
            var dir = StackOutlineDirs[i];
            o.transform.localPosition = new Vector3(dir.x * outlineStep, dir.y * outlineStep, 0.002f);
            if (_stackOutlineRenderers != null && i < _stackOutlineRenderers.Length && _stackOutlineRenderers[i] != null)
            {
                _stackOutlineRenderers[i].sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
                _stackOutlineRenderers[i].sortingOrder = GameConfig.SORT_VFX + 29;
            }
        }
    }

    void EnsureStackLabel()
    {
        if (_stackLabel != null && _stackOutlineLabels != null && _stackOutlineLabels.Length > 0)
            return;
        if (_stackLabelRoot != null)
            Destroy(_stackLabelRoot.gameObject);
        _stackLabel = null;
        _stackLabelRenderer = null;
        _stackOutlineLabels = null;
        _stackOutlineRenderers = null;

        _stackLabelRoot = new GameObject("StackCount").transform;
        _stackLabelRoot.SetParent(transform, false);

        _stackOutlineLabels = new TextMesh[StackOutlineDirs.Length];
        _stackOutlineRenderers = new MeshRenderer[StackOutlineDirs.Length];
        for (int i = 0; i < StackOutlineDirs.Length; i++)
        {
            var oGo = new GameObject("Outline" + i, typeof(TextMesh));
            oGo.transform.SetParent(_stackLabelRoot, false);
            var o = oGo.GetComponent<TextMesh>();
            o.text = "x2";
            o.fontSize = 64;
            o.anchor = TextAnchor.MiddleCenter;
            o.alignment = TextAlignment.Center;
            o.color = new Color(0.08f, 0.05f, 0.02f, 0.95f);
            o.richText = false;
            _stackOutlineLabels[i] = o;
            _stackOutlineRenderers[i] = oGo.GetComponent<MeshRenderer>();
        }

        var fillGo = new GameObject("Fill", typeof(TextMesh));
        fillGo.transform.SetParent(_stackLabelRoot, false);
        _stackLabel = fillGo.GetComponent<TextMesh>();
        _stackLabel.text = "x2";
        _stackLabel.fontSize = 64;
        _stackLabel.anchor = TextAnchor.MiddleCenter;
        _stackLabel.alignment = TextAlignment.Center;
        _stackLabel.color = new Color(1f, 0.92f, 0.35f, 1f);
        _stackLabel.richText = false;

        _stackLabelRenderer = fillGo.GetComponent<MeshRenderer>();
        ApplyStackLabelFont();
        RefreshStackLabelLayout();
    }

    void HideStackLabel()
    {
        if (_stackLabelRoot != null) _stackLabelRoot.gameObject.SetActive(false);
    }

    /// <summary>
    /// ?? HPBar ??SpriteRenderer ????
    /// ????????????????Default??????Effects ????
    /// </summary>
    void SetupHPBarSorting()
    {
        if (_hpBarRoot == null) return;

        Transform bg = _hpBarRoot.Find("HPBarBG") ?? _hpBarRoot.Find("HPBarBg");
        if (bg != null)
        {
            _hpBarBg = bg.GetComponent<SpriteRenderer>();
            if (_hpBarBg != null)
            {
                _hpBarBg.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
                _hpBarBg.sortingOrder = GameConfig.SORT_VFX - 2;
            }
        }

        if (_hpBarFill != null)
        {
            _hpBarFill.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            _hpBarFill.sortingOrder = GameConfig.SORT_VFX - 1;
        }

        // ???????????
        if (sr != null)
        {
            sr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            sr.sortingOrder = GameConfig.SORT_UNIT;
        }
    }

    public bool IsBossUnit => _isBossUnit;
    public bool IsEliteWave => _eliteWave;

    public override void TakeDamage(float damage, bool isCrit, bool ignoreDefense = false, bool showHitVfx = true, int hitVfxFacing = 0)
    {
        base.TakeDamage(damage, isCrit, ignoreDefense, showHitVfx, hitVfxFacing);
        if (!isDead)
        {
            _worldHpBar?.SyncHpVisual(flash: true);
            BringHpBarFront();
        }
    }

    /// <summary>?????????????????????????/summary>
    void BringHpBarFront()
    {
        s_hpBarFrontBoost = (s_hpBarFrontBoost + 2) % 40;
        if (_worldHpBar != null)
        {
            _worldHpBar.BringToFront(s_hpBarFrontBoost);
            return;
        }

        int bgOrder = GameConfig.SORT_VFX - 2 + s_hpBarFrontBoost;
        int fillOrder = bgOrder + 1;
        if (_hpBarBg != null)
        {
            _hpBarBg.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            _hpBarBg.sortingOrder = bgOrder;
        }
        if (_hpBarFill != null)
        {
            _hpBarFill.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
            _hpBarFill.sortingOrder = fillOrder;
        }
    }

    /// <summary>
    /// ??????????Monsters ??????sprite????????scale??
    /// </summary>
    /// <param name="effectiveSpriteIndex">????????-12??0????</param>
    private void LoadSprite(MonsterConfig template, int chapter, int effectiveSpriteIndex = 0)
    {
        if (sr == null)
        {
            Transform monstersChild = transform.Find("Monsters");
            if (monstersChild != null)
                sr = monstersChild.GetComponent<SpriteRenderer>();
            if (sr == null)
                sr = GetComponentInChildren<SpriteRenderer>(true);
        }
        if (sr == null)
        {
            Debug.LogError($"[Monster] LoadSprite ???? SpriteRenderer id={template?.id}");
            return;
        }

        int monsterChapter = GameConfig.GetMonsterChapter(chapter);
        Sprite monsterSprite = null;

        var loader = MonsterSpriteLoader.Instance;
        if (loader != null)
        {
            if (effectiveSpriteIndex > 0)
                monsterSprite = loader.LoadMonsterSprite(monsterChapter, effectiveSpriteIndex - 1);
            else
                monsterSprite = loader.GetRandomMonsterSprite(monsterChapter);
        }

        // ??????????? Resources ???? PNG
        if (monsterSprite == null)
            monsterSprite = LoadSpriteFromResources(monsterChapter, effectiveSpriteIndex);

        if (monsterSprite != null)
        {
            sr.sprite = monsterSprite;
            sr.enabled = true;
            var c = sr.color;
            c.a = 1f;
            sr.color = c;
        }
        else
            Debug.LogWarning($"[Monster] ???????: ??{monsterChapter}, ??{effectiveSpriteIndex}??????????");
    }

    /// <summary>
    /// ????Resources ???????? PNG????????????
    /// </summary>
    Sprite LoadSpriteFromResources(int monsterChapter, int spriteIndex)
    {
        string folderName = null;
        string prefix = null;

        switch (monsterChapter)
        {
            case 1: folderName = "1 Undead"; prefix = "undead_1"; break;
            case 2: folderName = "2 Jungle"; prefix = "jungle_2"; break;
            case 3: folderName = "3 Sea"; prefix = "sea_3"; break;
            case 4: folderName = "4 Forest"; prefix = "forest_4"; break;
            case 5: folderName = "5 Field"; prefix = "field_5"; break;
            case 6: folderName = "6 Cave"; prefix = "cave_6"; break;
            case 7: folderName = "7 Devil"; prefix = "devil_7"; break;
            case 8: folderName = "8 Ice"; prefix = "ice_8"; break;
        }

        if (folderName == null) return null;

        int index = spriteIndex > 0 ? spriteIndex : Random.Range(1, 13);
        string spriteName = $"{prefix}{index:D2}";
        string path = $"Config/MonsterSpriteRegistry/{folderName}/{spriteName}";

        Sprite sprite = Resources.Load<Sprite>(path);

        if (sprite == null)
        {
            Texture2D tex = Resources.Load<Texture2D>(path);
            if (tex != null)
            {
                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0f), 100f);
            }
        }

        if (sprite != null)
            Debug.Log($"[Monster] ?Resources????: {path}");
        return sprite;
    }

    protected override void Update()
    {
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
        if (BattleManager.Instance != null && !BattleManager.Instance.UnitsCanAct)
            return;

        _swingStyle = ResolveSwingStyle(target);

        if (_canUseActiveSkill && _skillEnergy >= 0.99f && !string.IsNullOrEmpty(_skillId))
        {
            UseActiveSkill(target);
            attackCd = GetAttackCooldown();
            return;
        }

        base.Attack(target);
        if (_canUseActiveSkill)
            _skillEnergy = Mathf.Min(1f, _skillEnergy + 0.15f);
    }

    MonsterAttackStyle ResolveSwingStyle(UnitBase target)
    {
        if (!_isBossUnit)
            return _attackStyle;

        // Boss??????????????????????
        float dist = target != null
            ? Mathf.Abs(transform.position.x - target.transform.position.x)
            : 99f;
        const float meleeReach = 1.8f;
        MonsterAttackStyle byDist = dist <= meleeReach ? MonsterAttackStyle.Melee : MonsterAttackStyle.Ranged;
        _bossSwingIndex++;
        if (_bossSwingIndex % 3 == 0)
            return byDist == MonsterAttackStyle.Melee ? MonsterAttackStyle.Ranged : MonsterAttackStyle.Melee;
        return byDist;
    }

    void UseActiveSkill(UnitBase primaryTarget)
    {
        _skillEnergy = 0f;
        _skillCooldown = (_isBossUnit || _eliteWave) ? 2.5f : 5f;
        _swingStyle = ResolveSwingStyle(primaryTarget);

        var skill = SkillRegistry.Instance?.GetActiveSkill(_skillId);
        float mult = skill != null ? skill.damageMultiplier : 2.2f;
        float extra = skill != null ? skill.baseDamage : 0f;
        // ???????????????????????????
        float tier = (_isBossUnit || _eliteWave) ? 1f : GameConfig.MONSTER_NORMAL_SKILL_DAMAGE_MUL;
        float damage = (attr.GetAttr(AttrType.Attack) * mult + extra) * tier;
        float radius = skill != null && skill.aoeRadius > 0 ? skill.aoeRadius : 5f;

        // ???????????????Boss ????? attackKit
        AttackVfxKit kit = MonsterAttackStyleTable.GetVfxKit(_isBossUnit ? _swingStyle : _attackStyle);

        if (_isBossUnit)
        {
            var skillCfg = SkillRegistry.Instance?.Get(_skillId);
            if (skillCfg != null && skillCfg.attackKit != AttackVfxKit.None)
                kit = skillCfg.attackKit;
        }

        Vector3 firePos = GetFirePosition();
        Vector3 hitPos = primaryTarget != null ? primaryTarget.GetHitPosition() : firePos;

        // ?????????????
        if (unitAnim != null)
            unitAnim.PlaySkillCast(kit, (_isBossUnit || _eliteWave) ? 1.15f : 1f);

        if (kit == AttackVfxKit.Bow || kit == AttackVfxKit.Orb)
        {
            GameObject impact = SkillRegistry.Instance?.GetSkillVfxPrefab(_skillId);
            Transform targetTf = primaryTarget != null ? primaryTarget.transform : null;
            BattleVFXSystem.Instance?.PlaySkillProjectile(
                VfxFaction.Enemy, firePos, hitPos, GetVfxFacingDir(), targetTf, kit,
                impact, SkillProjectileScale, SkillProjectileSpeedMul,
                () => ApplySkillDamage(damage, radius, primaryTarget));

            if (BattleVFXSystem.Instance == null)
                ApplySkillDamage(damage, radius, primaryTarget);
            return;
        }

        SkillRegistry.Instance?.PlaySkillVfx(_skillId, hitPos, false, GetVfxFacingDir(), transform);
        ApplySkillDamage(damage, radius, primaryTarget);
    }

    const float SkillProjectileScale = 1.6f;
    const float SkillProjectileSpeedMul = GameConfig.MONSTER_SKILL_PROJECTILE_SPEED_MUL;

    /// <summary>
    /// ????????????????????
    /// ?????????????????????????????????????????
    /// ????????????????????????????????
    /// </summary>
    void ApplySkillDamage(float damage, float radius, UnitBase primaryTarget)
    {
        if (this == null || isDead || !gameObject.activeInHierarchy) return;

        int vfxDir = GetVfxFacingDir();
        var allies = BattleManager.Instance?.allyUnits;
        if (allies != null && allies.Count > 0)
        {
            for (int i = 0; i < allies.Count; i++)
            {
                var u = allies[i];
                if (u == null || u.isDead) continue;
                float dist = Mathf.Abs(u.transform.position.x - transform.position.x);
                if (dist > radius) continue;
                bool crit = Random.value < attr.GetAttr(AttrType.CritRate);
                u.TakeDamage(crit ? damage * 1.5f : damage, crit, false, true, vfxDir);
            }
            return;
        }
        if (primaryTarget != null && !primaryTarget.isDead)
            primaryTarget.TakeDamage(damage, false, false, true, vfxDir);
    }

    /// <summary>
    /// 怪物朝向：翻转 Visual 整棵预制体（fire/beattack 一起镜像），血条在 Body 平级不参与翻转。
    /// spriteDefaultFacesRight=false：朝右(dir&gt;0) 时 scale.x 取负。
    /// </summary>
    private int _lastAppliedDir = 0;

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

    /// <summary>?????Boss ??????????</summary>
    /// <summary>?????????????Bow=???Ranged=????/summary>
    protected override AttackVfxKit GetAttackVfxKit()
    {
        if (!_isBossUnit)
            return MonsterAttackStyleTable.GetVfxKit(_attackStyle);
        return MonsterAttackStyleTable.GetVfxKit(_swingStyle);
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
    }

    public override void ResetForReuse()
    {
        CollapseBodyRootForPool();
        base.ResetForReuse();
        _lastAppliedDir = 0;
        _isEnteringMap = false;
        _bossSwingIndex = 0;
        _forcedTarget = null;
        _worldHpBar = null;
        HideStackLabel();
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
