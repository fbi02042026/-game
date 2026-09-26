using System.Collections;
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
        // 仅 Boss/精英用屏幕 BossBar；小怪不再创建头顶世界血条
        DisableEmbeddedHpBar();
        _worldHpBar = null;
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
    // 2026-09-26 主人拍板：本怪物是魔法型(true)还是物理型(false)，攻击/防御二选一不混搭。
    // 该值在 Init 时按 monster_attack_style.csv 的 magicChance 掷骰一次后固定，终身不变。
    private bool _isMagicType;
    /// <summary>本怪物随机判定出的物理/魔法类型（Init 掷一次后固定）。供掉落按类型区分时读取。</summary>
    public bool IsMagicType => _isMagicType;
    bool _eliteGlass;
    bool _bossPhase2Started;
    bool _bossPhaseShiftBusy;
    bool _enraged;
    private int _bossSwingIndex;
    private bool _isEnteringMap;
    /// <summary>
    /// 剧情前置暂停（UnitsCanAct=false 且 AllowMonsterMapEnter=true）时，怪只走到屏幕边缘就停，
    /// 此时 _isEnteringMap 仍为 true（要等「！」之后才继续推进到交战点）。
    /// 引导流程要「最后一只怪站定就弹对白」，靠这个标志判断，不能只等 _isEnteringMap 变 false。
    /// </summary>
    private bool _enterAtPauseStop;
    private Vector3 _enterTargetPos;
    private float _enterSpeed = 1.6f;

    /// <summary>是否仍在从屏外走进交战点。</summary>
    public bool IsEnteringMap => _isEnteringMap;

    /// <summary>已走到「剧情暂停位」（屏幕边缘）并站定；对引导来说等于「这只怪已入场完毕」。</summary>
    public bool EnterAtPauseStop => _enterAtPauseStop;

    /// <summary>从地图边缘缓步走向交战点；faceDir 为入场朝向（左进场朝右=1，右进场朝左=-1）</summary>
    public void BeginMapEnter(Vector3 engagePos, float speed, int faceDir = -1)
    {
        _enterTargetPos = engagePos;
        float enterMul = _isBossUnit ? 1f : Random.Range(0.9f, 1.15f);
        _enterSpeed = Mathf.Max(0.4f, speed * enterMul);
        _isEnteringMap = true;
        if (_isBossUnit) BattleBossHpBar.PlayBossIntro(this); // BOSS 出场：血条入场演出，与走进场同时进行
        _enterAtPauseStop = false;
        facingDir = faceDir > 0 ? 1 : -1;
        ApplyFacing(facingDir);
        if (rb != null) rb.velocity = Vector2.zero;
    }

    UnitBase _forcedTarget;

    /// <summary>?????????????????????/summary>
    public void SetForcedTarget(UnitBase t) => _forcedTarget = t;

    protected override void AIUpdate()
    {
        // 入场：默认剧情冻结时暂停；仅 AllowMonsterMapEnter 窗口可继续走进
        var bmEnter = BattleManager.Instance;
        bool preFightPause = bmEnter != null && bmEnter.AllowMonsterMapEnter && !bmEnter.UnitsCanAct;
        if (_isEnteringMap)
        {
            bool canEnter = bmEnter == null
                || bmEnter.UnitsCanAct
                || bmEnter.AllowMonsterMapEnter;
            if (!canEnter)
            {
                if (rb != null) rb.velocity = Vector2.zero;
                if (unitAnim != null) unitAnim.SetMove(false, facingDir);
                ApplyLaneY(Time.deltaTime);
                return;
            }

            float dx = _enterTargetPos.x - MoveRoot.position.x;
            // 进场途中若已贴近英雄，提前结束进场并开打，避免穿身跑到对面
            if (Hero.Instance != null && !Hero.Instance.isDead
                && bmEnter != null && bmEnter.UnitsCanAct && !bmEnter.AllowMonsterMapEnter)
            {
                float distHero = Mathf.Abs(GetCombatX(this) - GetCombatX(Hero.Instance));
                if (distHero <= GetEffectiveAttackRange() + 0.4f)
                {
                    _isEnteringMap = false;
                    _enterAtPauseStop = false;
                    SyncLaneYFromWorld();
                    target = Hero.Instance;
                    FaceToward(Hero.Instance);
                }
            }
            if (_isEnteringMap && Mathf.Abs(dx) <= 0.08f)
            {
                GameConfig.SetWorldPosition(MoveRoot, new Vector3(_enterTargetPos.x, FootY, MoveRoot.position.z));
                _isEnteringMap = false;
                _enterAtPauseStop = false;
                SyncLaneYFromWorld();
                UnitBase enterFoe = FindNearestEnemyOnField();
                if (enterFoe == null && Hero.Instance != null && !Hero.Instance.isDead)
                    enterFoe = Hero.Instance;
                if (enterFoe != null)
                {
                    target = enterFoe;
                    FaceToward(enterFoe);
                }
                // 进场结束若仍在剧情窗：站住等「！」再开打
                if (bmEnter != null && (!bmEnter.UnitsCanAct || bmEnter.AllowMonsterMapEnter))
                {
                    if (rb != null) rb.velocity = Vector2.zero;
                    if (unitAnim != null) unitAnim.SetMove(false, facingDir);
                    ApplyLaneY(Time.deltaTime);
                    return;
                }
            }
            else if (_isEnteringMap)
            {
                int enterFace = _enterTargetPos.x >= MoveRoot.position.x ? 1 : -1;
                facingDir = enterFace;
                ApplyFacing(facingDir);
                // 剧情前置暂停：只走到屏幕边缘即停，不深入；等「！」后 _isEnteringMap 仍未结束，继续推进到交战点
                float effTargetX = _enterTargetPos.x;
                if (preFightPause)
                {
                    BattleManager.GetBattleVisibleX(out float vMin, out float vMax);
                    effTargetX = enterFace > 0 ? vMin + GameConfig.MONSTER_PAUSE_STOP_MARGIN
                                              : vMax - GameConfig.MONSTER_PAUSE_STOP_MARGIN;
                }
                float step = _enterSpeed * Time.deltaTime;
                float nx = Mathf.MoveTowards(MoveRoot.position.x, effTargetX, step);
                GameConfig.SetWorldPosition(MoveRoot, new Vector3(nx, FootY, MoveRoot.position.z));
                bool stillWalking = Mathf.Abs(effTargetX - nx) > 0.02f;
                // 剧情暂停窗内走到屏幕边缘即算「入场完毕」，供引导立即接管（否则要等到超时）
                _enterAtPauseStop = preFightPause && !stillWalking;
                if (unitAnim != null) unitAnim.SetMove(stillWalking, facingDir);
                ApplyLaneY(Time.deltaTime);
                return;
            }
        }

        // 剧情前置暂停：所有怪（含强制目标）原地等待玩家「！」，杜绝某怪绕过暂停直接冲向英雄
        if (preFightPause)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            if (unitAnim != null) unitAnim.SetMove(false, facingDir);
            ApplyLaneY(Time.deltaTime);
            return;
        }

        if (_skillTelegraphing || _bossPhaseShiftBusy)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            if (unitAnim != null) unitAnim.SetMove(false, facingDir);
            ApplyLaneY(Time.deltaTime);
            return;
        }

        if (_forcedTarget != null)
        {
            if (_forcedTarget.isDead)
                _forcedTarget = null;
            else
            {
                target = _forcedTarget;
                if (TryHoldDuringAttack())
                    return;
                RunForcedCombat();
                return;
            }
        }

        // 攻击中不换目标、不滑步
        if (TryHoldDuringAttack())
            return;

        // 场上最近友军（交战寻敌）
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

    /// <summary>场上最近友军（交战寻敌）。</summary>
    public override UnitBase FindNearestEnemyOnField()
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
            if (!GameConfig.IsInCombatViewport(enemy)) continue;
            float dist = Mathf.Abs(myX - GetCombatX(enemy));
            if (dist < minDist)
            {
                minDist = dist;
                nearest = enemy;
            }
        }
        return nearest;
    }

    protected override bool UsesMeleeBasicAttack()
    {
        return !MonsterAttackStyleTable.IsRanged(_attackStyle);
    }

    /// <summary>攻击风格是否为远程（弓/法球）。教程关按「近战厚 / 远程脆」分档配血用。</summary>
    public bool IsRangedStyle => MonsterAttackStyleTable.IsRanged(_attackStyle);

    void RunForcedCombat()
    {
        if (_isEnteringMap) return;
        if (_skillTelegraphing)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            if (unitAnim != null) unitAnim.SetMove(false, facingDir);
            ApplyLaneY(Time.deltaTime);
            return;
        }
        if (BattleManager.Instance != null
            && (!BattleManager.Instance.UnitsCanAct || BattleManager.Instance.AllowMonsterMapEnter))
        {
            if (rb != null) rb.velocity = Vector2.zero;
            if (unitAnim != null) unitAnim.SetMove(false, facingDir);
            ApplyLaneY(Time.deltaTime);
            return;
        }
        if (target == null || target.isDead)
        {
            ApplyLaneY(Time.deltaTime);
            return;
        }

        if (TryHoldDuringAttack())
            return;

        float distance = Mathf.Abs(GetCombatX(this) - GetCombatX(target));
        float attackRange = GetEffectiveAttackRange();
        bool melee = UsesMeleeBasicAttack();
        // 远程：进入后加离开迟滞，避免玩家微移就追着滑步
        const float rangedLeaveSlack = 0.4f;
        bool inHoldRange = melee
            ? IsInBasicAttackRange(target)
            : distance <= attackRange + rangedLeaveSlack;

        if (melee)
        {
            FaceToward(target);
            AdjustLaneTowardTarget(target, Time.deltaTime);
        }

        bool isMoving = false;
        if (inHoldRange)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            if (!melee)
                FaceToward(target);
            if (attackCd <= 0 && (unitAnim == null || !unitAnim.InAttackLock)
                && (melee || IsInBasicAttackRange(target) || distance <= attackRange))
            {
                Attack(target);
                attackCd = GetAttackCooldown();
            }
        }
        else if (melee && distance <= attackRange)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            isMoving = true;
        }
        else
        {
            FaceToward(target);
            float spd = attr.GetAttr(AttrType.MoveSpeed);
            if (rb != null) rb.velocity = new Vector2(facingDir * spd, rb.velocity.y);
            isMoving = true;
        }

        if (unitAnim != null) unitAnim.SetMove(isMoving, facingDir);
        ApplyLaneY(Time.deltaTime);
    }

    void AdvanceTowardEnemies()
    {
        if (BattleManager.Instance != null
            && (!BattleManager.Instance.UnitsCanAct || BattleManager.Instance.AllowMonsterMapEnter))
        {
            if (rb != null) rb.velocity = Vector2.zero;
            if (unitAnim != null) unitAnim.SetMove(false, facingDir);
            ApplyLaneY(Time.deltaTime);
            return;
        }

        UnitBase foe = FindNearestEnemyOnField();
        if (foe == null && Hero.Instance != null && !Hero.Instance.isDead)
            foe = Hero.Instance;

        if (foe == null)
        {
            // 无目标：停步，勿写死往左冲
            if (rb != null) rb.velocity = Vector2.zero;
            if (unitAnim != null) unitAnim.SetMove(false, facingDir);
            ApplyLaneY(Time.deltaTime);
            return;
        }

        target = foe;
        float dist = Mathf.Abs(GetCombatX(foe) - GetCombatX(this));
        float attackRange = GetEffectiveAttackRange();
        // 与 RunForcedCombat 一致：远程用离开迟滞，避免边界抖步
        float hold = UsesMeleeBasicAttack() ? attackRange : attackRange + 0.4f;
        if (dist <= hold)
        {
            RunForcedCombat();
            return;
        }

        float dir = GetCombatX(foe) > GetCombatX(this) ? 1f : -1f;
        facingDir = (int)dir;
        ApplyFacing(facingDir);

        float spd = attr.GetAttr(AttrType.MoveSpeed);
        if (rb != null) rb.velocity = new Vector2(dir * spd, rb.velocity.y);
        if (unitAnim != null) unitAnim.SetMove(true, facingDir);
        ApplyLaneY(Time.deltaTime);
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
        // Boss 不参与本次缩小（主人 2026-09-26 拍板）：MONSTER_SMALL_SHRINK 已乘进 MONSTER_SCALE_MIN/MAX，
        // 这里对 Boss 除回去以恢复原尺寸；普通怪/精英仍保持 ×0.7（Normal : Elite = 1 : 1.3 不变）。
        if (bossUnit && !GameConfig.MONSTER_BOSS_APPLY_SMALL_SHRINK && GameConfig.MONSTER_SMALL_SHRINK > 0.0001f)
            rootScale /= GameConfig.MONSTER_SMALL_SHRINK;
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
        float chapterScale = GameConfig.GetChapterStatScale(chapter);
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
            // 精英不再写死常量：按本章普通怪平均基准推导，避免「精英比本章杂兵还脆」。
            // 表缺该章数据时回退旧常量（MONSTER_ELITE_HP / ATK）。
            float avgHp, avgAtk;
            if (MonsterStatsTable.TryGetChapterAverage(monsterChapter, out avgHp, out avgAtk) && avgHp > 0f)
            {
                baseHp = avgHp * GameConfig.ELITE_HP_FROM_CHAPTER_AVG;
                baseAtk = avgAtk * GameConfig.ELITE_ATK_FROM_CHAPTER_AVG;
            }
            else
            {
                baseHp = GameConfig.MONSTER_ELITE_HP;
                baseAtk = GameConfig.MONSTER_ELITE_ATK;
            }
            baseDef = GameConfig.MONSTER_ELITE_DEF;
            atkInterval = GameConfig.MONSTER_ELITE_ATK_INTERVAL;
            // 分档：关卡波次奇偶 → 血厚 / 血薄
            _eliteGlass = ((waveNum + monsterChapter) & 1) == 1;
            if (_eliteGlass)
            {
                baseHp *= GameConfig.ELITE_GLASS_HP_MUL;
                baseAtk *= GameConfig.ELITE_GLASS_ATK_MUL;
            }
            else
            {
                baseHp *= GameConfig.ELITE_TANK_HP_MUL;
                baseAtk *= GameConfig.ELITE_TANK_ATK_MUL;
            }
        }

        float waveMul = 1f + waveNum * 0.05f;
        float ttkMul = (bossUnit || eliteWave)
            ? WeaponCombatTable.EliteBossHpMul(monsterChapter, bossUnit)
            : GameConfig.GetChapterStatScale(chapter);
        // ?????? chapterScale????Boss ??TTK ????????
        float hpScale = (bossUnit || eliteWave) ? (guildScale * diffScale * ttkMul) : (scale);
        attr.SetAttr(AttrType.MaxHp, baseHp * hpScale * waveMul * GameConfig.MONSTER_HP_GLOBAL_MUL);
        // ⚠ 流程测试开关（2026-09-23，测完注释掉）：Boss 压成 1 滴血，一击通关好验证流程
        if (GameConfig.TEST_BOSS_HP > 0 && bossUnit)
            attr.SetAttr(AttrType.MaxHp, GameConfig.TEST_BOSS_HP);
        // Boss 原先只有血量 TTK 加成，攻击没有 —— 补上，否则后期 Boss 打人比自家远程杂兵还轻
        float atkTtkMul = bossUnit ? GameConfig.BOSS_TTK_ATK_MUL : 1f;
        // 2026-09-26 主人拍板：怪物只分物理 / 魔法，攻击与防御二选一、不混搭。
        // 魔法型（掷骰命中 magicChance）→ 物攻/物防置 0，攻击与防御全部走 MagicAttack / MagicDefense；
        // 物理型 → 魔攻/魔防置 0，只走 Attack / Defense。
        // 判定走 MonsterAttackTypeResolver。2026-09-26 主人拍板：刷怪配置整合到关卡级——
        // 优先按 stage_spawn.csv 本关 magicChance 掷骰（含 Boss 0.5）；本关未配才回退单怪级。
        int gChapter = BattleManager.Instance != null ? BattleManager.Instance.CurrentChapter : monsterChapter;
        StageType gStageType = BattleManager.Instance != null && BattleManager.Instance.currentStage != null
            ? BattleManager.Instance.currentStage.type : StageType.Normal;
        bool isMagicType = MonsterAttackTypeResolver.IsMagicMonster(
            monsterChapter, Mathf.Max(1, effectiveSpriteIndex), gChapter, gStageType);
        _isMagicType = isMagicType;
        // 2026-09-26 主人拍板：魔法型强制改法球弹道(Ranged)→视觉与伤害类型(魔法)一致；物理型保持表内 style。
        if (isMagicType)
            _attackStyle = MonsterAttackStyle.Ranged;
        if (isMagicType)
        {
            attr.SetAttr(AttrType.Attack, 0f);
            attr.SetAttr(AttrType.Defense, 0f);
            // 魔法侧数值：配表(MonsterConfig.baseMagicAttack/Defense)给了就用，没给则等比沿用本单位的 baseAttack / baseDef。
            float baseMagAtk = (template != null && template.baseMagicAttack > 0f)
                ? template.baseMagicAttack
                : baseAtk * GameConfig.MONSTER_MAGIC_ATK_RATIO;
            float baseMagDef = (template != null && template.baseMagicDefense > 0f)
                ? template.baseMagicDefense
                : baseDef * GameConfig.MONSTER_MAGIC_DEF_RATIO;
            attr.SetAttr(AttrType.MagicAttack, baseMagAtk * scale * waveMul * GameConfig.MONSTER_DAMAGE_MULTIPLIER * atkTtkMul);
            attr.SetAttr(AttrType.MagicDefense, baseMagDef * scale);
        }
        else
        {
            attr.SetAttr(AttrType.Attack, baseAtk * scale * waveMul * GameConfig.MONSTER_DAMAGE_MULTIPLIER * atkTtkMul);
            attr.SetAttr(AttrType.Defense, baseDef * scale);
            attr.SetAttr(AttrType.MagicAttack, 0f);
            attr.SetAttr(AttrType.MagicDefense, 0f);
        }
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
        // 怪物移速统一由表驱动（非 Boss 与 Boss 同一逻辑）。
        // 表 moveSpeed 为「世界前」相对值，需乘 MONSTER_MOVE_SPEED_TO_WORLD 转世界单位；
        // monster_stats 已全体减半(2.2→1.1)，1.1 × 0.3142 = 0.3456 世界单位。
        // 注：原 Boss 走 Mathf.Min(表值, DEFAULT×1.5) 会被钳回 1.0368；减半后表值远低于该上限，
        // 若保留上限会把减半后的速度又钳高，故这里不再设上限。
        float moveSpd = (template != null && template.baseMoveSpeed > 0.01f)
            ? template.baseMoveSpeed * GameConfig.MONSTER_MOVE_SPEED_TO_WORLD
            : GameConfig.MONSTER_DEFAULT_MOVE_SPEED;
        float atkRange;
        if (_isBossUnit)
        {
            atkRange = AttackRangeTable.GetMonsterWorld(MonsterAttackStyle.Ranged);
        }
        else
        {
            atkRange = AttackRangeTable.GetMonsterWorld(_attackStyle);
        }
        attr.SetAttr(AttrType.MoveSpeed, moveSpd);
        // 非 Boss：移速小幅岔开，减轻同相位叠走
        if (!_isBossUnit)
        {
            float jitter = Random.Range(0.88f, 1.12f);
            attr.SetAttr(AttrType.MoveSpeed, moveSpd * jitter);
        }
        attr.SetAttr(AttrType.AttackRange, atkRange);
        attr.SetAttr(AttrType.CritRate, 0.05f);

        // 引导关怪物也走正式数值，不再开局 +25% 血 / 减攻速。

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
        if (!eliteWave) _eliteGlass = false;
        _bossPhase2Started = false;

        // V6 词缀：属性全部按表算完之后再叠乘子，避免被后续赋值覆盖。
        // 只给精英/Boss；普通小怪不 roll，否则满屏后缀变成视觉噪音。
        MonsterAffix.RollAndAttach(this, _chapter, _isBossUnit || _eliteWave);
        _bossPhaseShiftBusy = false;
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

        Debug.Log($"[Monster:{(template != null ? template.id : "fallback")}] Init | mCh={monsterChapter} sprite={_spriteIndex} style={_attackStyle} kit={MonsterAttackStyleTable.GetVfxKit(_attackStyle)} boss={_isBossUnit} range={atkRange:F1} skill={_skillId} vfxSys={(BattleVFXSystem.Instance != null)}");
        BattleBossHpBar.RefreshFromField();
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
        if (!GameConfig.SHOW_MONSTER_STACK_LABEL || count <= 1)
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

        Transform body = GetBodyTransform() != null ? GetBodyTransform() : transform;
        float labelY = 0.85f;
        if (sr != null && sr.sprite != null)
        {
            var topLocal = body.InverseTransformPoint(sr.bounds.max);
            labelY = Mathf.Max(labelY, topLocal.y + 0.12f);
        }
        else if (_worldHpBar != null)
        {
            labelY = body.InverseTransformPoint(MoveRoot.position).y + 0.14f;
        }

        float rootAbs = Mathf.Max(0.01f, Mathf.Abs(body.lossyScale.y));
        _stackLabelRoot.localPosition = new Vector3(0f, labelY / rootAbs, 0f);
        // 防父节点万一带负 scale 时仍反字
        float fixX = body.lossyScale.x < 0f ? -1f : 1f;
        _stackLabelRoot.localScale = new Vector3(fixX, 1f, 1f);
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
        // 挂 Body（不参与 Visual 镜像），避免朝左时 ×N 反字
        Transform labelParent = GetBodyTransform() != null ? GetBodyTransform() : transform;
        _stackLabelRoot.SetParent(labelParent, false);

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

    public int GetBossPhase()
    {
        if (!_isBossUnit || attr == null) return 0;
        float maxHp = attr.GetAttr(AttrType.MaxHp);
        if (maxHp < 1f) return 1;
        return (currentHp / maxHp) <= GameConfig.BOSS_PHASE2_HP_RATIO ? 2 : 1;
    }

    public override void TakeDamage(float damage, bool isCrit, bool ignoreDefense = false, bool showHitVfx = true, int hitVfxFacing = 0, UnitBase source = null)
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

        base.TakeDamage(inDamage, isCrit, ignoreDefense, showHitVfx, hitVfxFacing, source);

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

    void TryBeginBossPhase2()
    {
        if (!_isBossUnit || _bossPhase2Started || isDead) return;
        if (GetBossPhase() < 2) return;
        _bossPhase2Started = true;
        if (!_skillTelegraphing && !_bossPhaseShiftBusy)
            StartCoroutine(CoBossPhaseShift());
    }

    IEnumerator CoBossPhaseShift()
    {
        _bossPhaseShiftBusy = true;
        if (rb != null) rb.velocity = Vector2.zero;
        UIManager.Instance?.ShowToast("Boss 进入狂暴阶段！");
        Vector3 center = new Vector3(transform.position.x, GROUND_Y + 0.02f, transform.position.z);
        GameObject disc = CreateTelegraphDisc(center, 4.5f);
        float t = 0f;
        while (t < GameConfig.BOSS_PHASE_SHIFT_TELEGRAPH)
        {
            if (isDead) break;
            t += Time.deltaTime;
            yield return null;
        }
        if (disc != null) Destroy(disc);
        if (!isDead)
            ApplySkillDamage(GetMainAttackStat() * 0.85f, 4.5f, null, transform.position.x);
        _bossPhaseShiftBusy = false;
    }

    /// <summary>?????????????????????????/summary>
    // ===== Boss 狂暴机制：仅第 5 章起、总开关控制；只改攻击间隔 =====
    /// <summary>
    /// 运行期标志，不持久化。条件满足即进入狂暴一次：
    /// 是 Boss、未死、总开关开、章节≥阈值、当前/最大血量≤阈值。
    /// 恢复存档后本标志随怪物重建而重置，下一帧 Update 会按当前血量重新判定。
    /// </summary>
    void TryBeginEnrage()
    {
        if (!_isBossUnit || _enraged || isDead) return;
        if (!GameConfig.BOSS_ENRAGE_ENABLED) return;
        if (_chapter < GameConfig.BOSS_ENRAGE_MIN_CHAPTER) return;
        float maxHp = attr != null ? attr.GetAttr(AttrType.MaxHp) : 0f;
        if (maxHp < 1f) return;
        if (currentHp / maxHp > GameConfig.BOSS_ENRAGE_HP_RATIO) return;
        _enraged = true;
        UIManager.Instance?.ShowToast("Boss 进入狂暴！");
        // 视觉反馈：直接复用现有「受击」特效（PlayVictimHit），不新造特效系统
        if (BattleVFXSystem.Instance != null)
            BattleVFXSystem.Instance.PlayVictimHit(GetHitPosition(), false, GetVfxFacingDir());
    }

    /// <summary>狂暴：只把攻击间隔 ÷倍率（即攻速 ×倍率），不动伤害/属性缩放。</summary>
    protected override float GetAttackCooldown()
    {
        float cd = base.GetAttackCooldown();
        if (_enraged)
            cd /= GameConfig.BOSS_ENRAGE_ATK_SPEED_MUL;
        return cd;
    }

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

        EnsureFootShadow();
    }

    /// <summary>
    /// 怪脚下半透椭圆阴影。关键点：阴影必须挂到「承载身体部件的 SortingGroup」之下，
    /// 与躯干/SPUM 部件处于同一组内，sortingOrder=-20 才能稳定排在躯干(>=0)之后。
    /// 否则作为 _bodyRoot 下的独立节点时，-20 会低于地图(SORT_MAPROOT=10)，
    /// 既可能被背景遮住，也可能因身体部件自身顺序更负而叠到身上（即“阴影跑到身子上面”）。
    /// </summary>
    void EnsureFootShadow()
    {
        Transform body = GetBodyTransform();
        if (body == null) return;

        // 找到承载身体部件的 SortingGroup（ApplyUnitSorting 通常加在 transform 或其子节点上）
        Transform host = transform;
        var sg = GetComponent<UnityEngine.Rendering.SortingGroup>();
        if (sg == null) sg = GetComponentInChildren<UnityEngine.Rendering.SortingGroup>(true);
        if (sg == null)
        {
            GameConfig.ApplyUnitSorting(transform);
            sg = GetComponent<UnityEngine.Rendering.SortingGroup>();
        }
        if (sg != null) host = sg.transform;

        Transform existing = FindFootShadowNode(transform);
        SpriteRenderer shadowSr;
        if (existing != null)
        {
            existing.SetParent(host, false);
            shadowSr = existing.GetComponent<SpriteRenderer>();
            if (shadowSr == null) shadowSr = existing.gameObject.AddComponent<SpriteRenderer>();
        }
        else
        {
            var go = new GameObject("FootShadow");
            go.transform.SetParent(host, false);
            shadowSr = go.AddComponent<SpriteRenderer>();
        }

        Sprite shadowSp = LoadPlayerShadowSprite() ?? MakeCircleSprite();
        shadowSr.sprite = shadowSp;
        shadowSr.color = new Color(0f, 0f, 0f, 0.35f);
        shadowSr.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
        // 组内相对顺序：低于躯干/SPUM 部件(>=0)，阴影藏在身子后面
        shadowSr.sortingOrder = -20;
        shadowSr.sharedMaterial = GetFootShadowMaterial();

        float targetWorldW = 0.9f;
        if (sr != null && sr.sprite != null)
            targetWorldW = Mathf.Max(0.45f, sr.bounds.size.x * 0.56f);
        // 整体再缩小 20%
        targetWorldW *= 0.8f;
        float nativeW = shadowSp != null ? Mathf.Max(0.01f, shadowSp.bounds.size.x) : 1f;
        float lossyX = Mathf.Max(0.001f, Mathf.Abs(host.lossyScale.x));
        float sx = targetWorldW / (nativeW * lossyX);
        // 本地 Y=0.02；椭圆再压扁 20%（0.35→0.28）
        shadowSr.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        shadowSr.transform.localRotation = Quaternion.identity;
        shadowSr.transform.localScale = new Vector3(sx, sx * 0.28f, 1f);
    }

    static Transform FindFootShadowNode(Transform root)
    {
        if (root == null) return null;
        if (root.name.IndexOf("Shadow", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var f = FindFootShadowNode(root.GetChild(i));
            if (f != null) return f;
        }
        return null;
    }

    static Material _footShadowMat;

    static Material GetFootShadowMaterial()
    {
        if (_footShadowMat != null) return _footShadowMat;
        var sh = Shader.Find("Battle/FootShadow");
        if (sh != null)
            _footShadowMat = new Material(sh);
        else
        {
            var fallback = Shader.Find("Sprites/Default");
            if (fallback != null)
                _footShadowMat = new Material(fallback);
        }
        return _footShadowMat;
    }

    static Sprite _cachedPlayerShadow;

    static Sprite LoadPlayerShadowSprite()
    {
        if (_cachedPlayerShadow != null) return _cachedPlayerShadow;
        _cachedPlayerShadow = Resources.Load<Sprite>("SPUM/Shadow");
        if (_cachedPlayerShadow != null) return _cachedPlayerShadow;
        var tex = Resources.Load<Texture2D>("SPUM/Shadow");
        if (tex != null)
        {
            _cachedPlayerShadow = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            return _cachedPlayerShadow;
        }
#if UNITY_EDITOR
        var ed = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/SPUM/Core/Basic_Resources/Ect/Shadow.png");
        if (ed != null)
        {
            _cachedPlayerShadow = ed;
            return _cachedPlayerShadow;
        }
#endif
        return null;
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
            case 1: folderName = "1 Forest"; prefix = "forest_1"; break;
            case 2: folderName = "2 Undead"; prefix = "undead_2"; break;
            case 3: folderName = "3 Jungle"; prefix = "jungle_3"; break;
            case 4: folderName = "4 Field"; prefix = "field_4"; break;
            case 5: folderName = "5 Sea"; prefix = "sea_5"; break;
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

    bool _skillTelegraphing;

    void UseActiveSkill(UnitBase primaryTarget)
    {
        _skillEnergy = 0f;
        _skillCooldown = (_isBossUnit || _eliteWave) ? 2.5f + 3f : 5f;
        // Attack() 已 ResolveSwingStyle；此处复用，避免 _bossSwingIndex 双跳

        var skill = SkillRegistry.Instance?.GetActiveSkill(_skillId);
        float mult = skill != null ? skill.damageMultiplier : 2.2f;
        float extra = skill != null ? skill.baseDamage : 0f;
        float tier = (_isBossUnit || _eliteWave) ? 1f : GameConfig.MONSTER_NORMAL_SKILL_DAMAGE_MUL;
        float damage = (GetMainAttackStat() * mult + extra) * tier;
        float radius = skill != null && skill.aoeRadius > 0 ? skill.aoeRadius : 5f;
        float telegraph = GameConfig.BOSS_PHASE1_TELEGRAPH;

        if (_isBossUnit && GetBossPhase() >= 2)
        {
            damage *= GameConfig.BOSS_PHASE2_DAMAGE_MUL;
            radius *= GameConfig.BOSS_PHASE2_RADIUS_MUL;
            telegraph = GameConfig.BOSS_PHASE2_TELEGRAPH;
            // 阶段 2：更偏向近身砸击节奏
            if (!MonsterAttackStyleTable.IsRanged(_attackStyle))
                _swingStyle = MonsterAttackStyle.Melee;
        }
        else if (_eliteWave)
        {
            telegraph = _eliteGlass ? GameConfig.ELITE_GLASS_TELEGRAPH : GameConfig.ELITE_TANK_TELEGRAPH;
        }

        AttackVfxKit kit = MonsterAttackStyleTable.GetVfxKit(_isBossUnit ? _swingStyle : _attackStyle);

        if (_isBossUnit)
        {
            var skillCfg = SkillRegistry.Instance?.Get(_skillId);
            if (skillCfg != null && skillCfg.attackKit != AttackVfxKit.None)
                kit = skillCfg.attackKit;
        }

        // 精英 / Boss：地面红色警示后再结算
        if (_isBossUnit || _eliteWave)
        {
            StartCoroutine(CoSkillTelegraphThenCast(primaryTarget, damage, radius, kit, telegraph));
            return;
        }

        ExecuteActiveSkillNow(primaryTarget, damage, radius, kit);
    }

    IEnumerator CoSkillTelegraphThenCast(UnitBase primaryTarget, float damage, float radius, AttackVfxKit kit, float telegraphSec)
    {
        _skillTelegraphing = true;
        Vector3 center = primaryTarget != null
            ? new Vector3(primaryTarget.transform.position.x, GROUND_Y + 0.02f, primaryTarget.transform.position.z)
            : new Vector3(transform.position.x, GROUND_Y + 0.02f, transform.position.z);

        GameObject disc = MonsterAttackStyleTable.IsRanged(_swingStyle)
            ? CreateTelegraphDisc(center, radius)
            : CreateTelegraphFan(new Vector3(transform.position.x, GROUND_Y + 0.02f, transform.position.z), radius, GetVfxFacingDir());
        float t = 0f;
        float warn = Mathf.Max(0.5f, telegraphSec);
        while (t < warn)
        {
            if (isDead || this == null)
            {
                if (disc != null) Destroy(disc);
                _skillTelegraphing = false;
                yield break;
            }
            t += Time.deltaTime;
            yield return null;
        }
        if (disc != null) Destroy(disc);

        if (!isDead && gameObject.activeInHierarchy)
        {
            // 预警结束、技能真正释放瞬间触发一次震屏（仅首次释放；Boss 阶段2 连砸不再震）
            CombatJuice.Shake(
                _isBossUnit ? GameConfig.BOSS_SKILL_RELEASE_SHAKE_AMP : GameConfig.ELITE_SKILL_RELEASE_SHAKE_AMP,
                _isBossUnit ? GameConfig.BOSS_SKILL_RELEASE_SHAKE_DUR : GameConfig.ELITE_SKILL_RELEASE_SHAKE_DUR);
            ExecuteActiveSkillNow(primaryTarget, damage, radius, kit);
            // Boss 阶段 2：连砸第二下
            if (_isBossUnit && GetBossPhase() >= 2 && !MonsterAttackStyleTable.IsRanged(_swingStyle))
            {
                yield return new WaitForSeconds(0.35f);
                if (!isDead)
                    ExecuteActiveSkillNow(primaryTarget, damage * 0.75f, radius * 0.85f, kit);
            }
        }
        _skillTelegraphing = false;
    }

    static GameObject CreateTelegraphDisc(Vector3 center, float radius)
    {
        var go = new GameObject("SkillTelegraph");
        go.transform.position = center;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = MakeCircleSprite();
        sr.color = new Color(0.95f, 0.12f, 0.1f, 0.42f);
        sr.sortingOrder = GameConfig.SORT_UNIT - 2;
        float dia = Mathf.Max(1.2f, radius * 2f);
        go.transform.localScale = new Vector3(dia, dia * 0.35f, 1f);
        return go;
    }

    static Sprite _circleSprite;
    static Sprite MakeCircleSprite()
    {
        if (_circleSprite != null) return _circleSprite;
        const int s = 64;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float c = (s - 1) * 0.5f;
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float dx = (x - c) / c;
            float dy = (y - c) / c;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float a = d <= 1f ? Mathf.Clamp01(1.15f - d) : 0f;
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        _circleSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return _circleSprite;
    }

    static Sprite _fanSprite;
    static Sprite MakeFanSprite()
    {
        if (_fanSprite != null) return _fanSprite;
        // 贴图宽 w、高 h=2w：锚点(左中)即扇形顶点，贴图右半为一张 90° 扇形（朝 +X）。
        const int w = 128;
        const int h = 256;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float c = (h - 1) * 0.5f;   // 顶点(左中)对应像素 y
        float maxR = w;             // 扇形最大半径(像素，沿 +X)
        float half = Mathf.PI * 0.25f; // 半角 45°（总 90°，-45°~+45°）
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float dx = x;
            float dy = y - c;
            float radial = Mathf.Sqrt(dx * dx + dy * dy);
            if (radial > maxR || dx < 0f)
            {
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, 0f));
                continue;
            }
            float ang = Mathf.Atan2(dy, dx); // 以 +X 为 0
            if (Mathf.Abs(ang) > half)
            {
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, 0f));
                continue;
            }
            // 半径方向软边衰减
            float aR = 1f - Mathf.SmoothStep(0.82f, 1f, radial / maxR);
            // 角度两侧软边
            float aA = 1f - Mathf.SmoothStep(half * 0.9f, half, Mathf.Abs(ang));
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, aR * aA));
        }
        tex.Apply();
        _fanSprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0f, 0.5f), w);
        return _fanSprite;
    }

    static GameObject CreateTelegraphFan(Vector3 origin, float radius, int facingDir)
    {
        var go = new GameObject("SkillTelegraphFan");
        go.transform.position = origin;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = MakeFanSprite();
        sr.color = new Color(0.95f, 0.12f, 0.1f, 0.42f);
        sr.sortingOrder = GameConfig.SORT_UNIT - 2;
        // 锚点(左中)：扇形从施法者脚下朝外展开；朝左时 localScale.x 取负实现 X 翻转。
        // 贴图 128x256 / ppu=128 → 世界尺寸 (1,2)，故 radius 即 scale.x；Y 压缩比与红圈一致 0.35。
        int sign = facingDir >= 0 ? 1 : -1;
        go.transform.localScale = new Vector3(radius * sign, radius * 0.35f, 1f);
        return go;
    }

    void ExecuteActiveSkillNow(UnitBase primaryTarget, float damage, float radius, AttackVfxKit kit)
    {
        Vector3 hitPos = primaryTarget != null ? primaryTarget.GetHitPosition() : GetFirePosition();

        if (unitAnim != null)
            unitAnim.PlaySkillCast(kit, (_isBossUnit || _eliteWave) ? 1.15f : 1f);

        if (kit == AttackVfxKit.Bow || kit == AttackVfxKit.Orb)
        {
            StartCoroutine(CoRangedSkillProjectile(primaryTarget, damage, radius, kit));
            return;
        }

        SkillRegistry.Instance?.PlaySkillVfx(_skillId, hitPos, false, GetVfxFacingDir(), transform);
        ApplySkillDamage(damage, radius, primaryTarget, hitPos.x);
    }

    IEnumerator CoRangedSkillProjectile(
        UnitBase primaryTarget, float damage, float radius, AttackVfxKit kit)
    {
        float delay = GameConfig.RANGED_FIRE_RELEASE_DELAY;
        if (delay > 0.001f)
            yield return new WaitForSeconds(delay);
        if (this == null || isDead)
            yield break;

        Vector3 firePos = GetFirePosition();
        Vector3 hitPos = primaryTarget != null && !primaryTarget.isDead
            ? primaryTarget.GetHitPosition() : firePos;
        GameObject impact = SkillRegistry.Instance?.GetSkillVfxPrefab(_skillId);
        Transform targetTf = primaryTarget != null ? primaryTarget.transform : null;
        float impactX = hitPos.x;
        if (BattleVFXSystem.Instance != null)
        {
            BattleVFXSystem.Instance.PlaySkillProjectile(
                VfxFaction.Enemy, firePos, hitPos, GetVfxFacingDir(), targetTf, kit,
                impact, SkillProjectileScale, SkillProjectileSpeedMul,
                () => ApplySkillDamage(damage, radius, primaryTarget, impactX));
        }
        else
            ApplySkillDamage(damage, radius, primaryTarget, impactX);
    }

    const float SkillProjectileScale = 1.6f;
    // 2026-09-21：MONSTER_SKILL_PROJECTILE_SPEED_MUL 已迁 combat_tuning 表（运行时读），
    // 右值不再是编译期常量 → const 改 static readonly（仅此一处引用，行为不变）。
    static readonly float SkillProjectileSpeedMul = GameConfig.MONSTER_SKILL_PROJECTILE_SPEED_MUL;

    /// <summary>
    /// 技能结算：AoE 圆心用开火/落点固定 X，不跟目标当前 X（可躲开）。
    /// </summary>
    void ApplySkillDamage(float damage, float radius, UnitBase primaryTarget, float centerX)
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
                float dist = Mathf.Abs(u.transform.position.x - centerX);
                if (dist > radius) continue;
                bool crit = Random.value < attr.GetAttr(AttrType.CritRate);
                u.TakeDamage(crit ? damage * 1.5f : damage, crit, false, true, vfxDir);
            }
            return;
        }
        if (primaryTarget != null && !primaryTarget.isDead)
        {
            if (Mathf.Abs(primaryTarget.transform.position.x - centerX) > radius)
                return;
            primaryTarget.TakeDamage(damage, false, false, true, vfxDir);
        }
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
