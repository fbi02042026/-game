using System;
using UnityEngine;

/// <summary>
/// 统一单位动画控制器
/// 自动桥接三种动画模式：
///
/// 1. SPUM模式  → 角色有 SPUM_Prefabs 组件（玩家、佣兵）
/// 2. Animator模式 → 角色有 Animator 但无SPUM
/// 3. 程序化模式 → 都没有（怪物），纯代码实现 idle/move/attack/death
///
/// 动画状态：
/// - IDLE  站立（轻微呼吸缩放，底部固定）
/// - MOVE  移动（上下弹跳+轻微倾斜）
/// - ATTACK 攻击（X轴拉伸前冲感）
/// - DEATH  死亡（旋转倒下+Alpha淡出）
///
/// SPUM Animator参数对照：
/// - Bool "1_Move"   → 移动状态
/// - Bool "isDeath"  → 死亡状态
/// - Trigger "2_Attack"/"ATTACK" → 攻击触发
/// </summary>
[DefaultExecutionOrder(1000)]
public class UnitAnimation : MonoBehaviour
{
    private SPUM_Prefabs _spum;
    private Animator _animator;
    private SpriteRenderer _sr;
    private HeroCostumeManager _costume;

    // 当前状态
    private bool _isMoving;
    private bool _isDead;
    private int _lastFacingDir = 1;

    // 攻击动画锁（防止动画重叠）
    private float _attackAnimLock = 0f;
    private float _attackAnimDuration = 0.5f;
    /// <summary>普攻动作时长；放技能会临时拉长，普攻时要还原</summary>
    private float _baseAttackDuration = 0.5f;

    // ===== 程序化动画（怪物用）=====
    [Header("程序化动画参数（怪物自动启用）")]
    [Tooltip("站立呼吸速度")]
    public float procIdleSpeed = 2f;
    [Tooltip("站立呼吸幅度")]
    public float procIdleAmount = 0.03f;
    [Tooltip("移动弹跳速度")]
    public float procMoveSpeed = 3.5f;
    [Tooltip("SPUM/Animator 移动动画速度倍率（1=原速）")]
    public float moveAnimSpeedScale = 0.4853f;
    [Tooltip("移动弹跳幅度")]
    public float procMoveAmount = 0.08f;
    [Tooltip("移动倾斜角度")]
    public float procMoveTilt = 5f;
    [Tooltip("攻击拉伸幅度")]
    public float procAttackStretch = 0.2f;
    [Tooltip("死亡淡出时间")]
    public float procDeathDuration = 0.8f;

    private bool _procMode;
    private bool _monsterClipMode;
    private bool _flipXFacing;
    static RuntimeAnimatorController _monsterClipController;
    static RuntimeAnimatorController _sanitizedMonsterClipController;
    const int MonsterSanitizeVersion = 3;
    static int _monsterSanitizeVersionApplied;
    Transform _monsterBody;
    private AttackVfxKit _procAttackKit = AttackVfxKit.MeleeSlash;
    private Vector3 _procBaseScale;
    private Vector3 _procBasePos;
    private float _procTime;
    private float _procDeathTime;
    private bool _procDeathCritKill;
    /// <summary>本次攻击的幅度倍率：普攻 1，放技能时放大。</summary>
    private float _procAmp = 1f;
    /// <summary>放技能时向前扑的距离（世界单位，作用在精灵子节点上，不影响 AI 移动）。</summary>
    private float _procLunge;

    SpriteRenderer[] _spumFlashSrs;
    Color[] _spumFlashBaseline;
    int _spumFlashGen;
    float _damagedRecoveryUntil;
    const float DamagedRecoverySeconds = 0.18f;

    void Awake()
    {
        // 强制用配置值，避免预制体里序列化成 1 导致「看起来没减速」
        moveAnimSpeedScale = GameConfig.MOVE_ANIM_SPEED_SCALE;
        procMoveSpeed = 1.75f;

        _spum = GetComponent<SPUM_Prefabs>();
        _animator = GetComponent<Animator>();
        _costume = GetComponent<HeroCostumeManager>();
        if (_costume == null)
            _costume = GetComponentInChildren<HeroCostumeManager>(true);
        _sr = GetComponent<SpriteRenderer>();
        if (_sr == null)
            _sr = FindMonsterBodySprite() ?? GetComponentInChildren<SpriteRenderer>();

        // SPUM初始化
        if (_spum != null && _spum._anim == null)
        {
            _spum._anim = _animator;
        }
        if (_spum != null && _spum.OverrideController == null && _spum._anim != null)
        {
            _spum.OverrideControllerInit();
        }

        // 程序化模式检测：无SPUM且无Animator且有SpriteRenderer → 怪物
        if (_spum == null && _animator == null && _sr != null)
        {
            _procMode = true;
            CacheBaseScale();
        }
        else if (_spum == null && _animator != null && IsMonsterClipController(_animator.runtimeAnimatorController))
        {
            _monsterClipMode = true;
            _procMode = false;
            ConfigureMonsterClipAnimator(_animator);
        }

        if (_spum != null)
            CacheSpumFlashRenderers();
    }

    void CacheSpumFlashRenderers()
    {
        if (_spum == null) return;
        _spumFlashSrs = GetComponentsInChildren<SpriteRenderer>(true);
        _spumFlashBaseline = new Color[_spumFlashSrs.Length];
        for (int i = 0; i < _spumFlashSrs.Length; i++)
        {
            if (_spumFlashSrs[i] != null && !IsShadowRenderer(_spumFlashSrs[i]))
                _spumFlashBaseline[i] = _spumFlashSrs[i].color;
        }
    }

    void RestoreSpumFlashFromBaseline()
    {
        var srs = _spumFlashSrs;
        if (srs == null || _spumFlashBaseline == null) return;
        for (int i = 0; i < srs.Length; i++)
        {
            if (srs[i] == null || IsShadowRenderer(srs[i])) continue;
            if (i < _spumFlashBaseline.Length)
                srs[i].color = _spumFlashBaseline[i];
            else
                srs[i].color = Color.white;
        }
    }

    static bool IsMonsterClipController(RuntimeAnimatorController ctrl)
    {
        if (ctrl == null) return false;
        if (ctrl.name == "Monster") return true;
        if (ctrl is AnimatorOverrideController over)
        {
            var baseCtrl = over.runtimeAnimatorController;
            return baseCtrl != null && baseCtrl.name == "Monster";
        }
        return false;
    }

    static RuntimeAnimatorController LoadMonsterClipController()
    {
        if (_monsterClipController == null)
            _monsterClipController = Resources.Load<RuntimeAnimatorController>("Prefabs/Monster/ani/Monster");
        return _monsterClipController;
    }

    /// <summary>拷贝 ani 片段并去掉空事件；仅钉 z=0（scale/xy 以用户录制的 scale=1 为准）。</summary>
    static RuntimeAnimatorController LoadSanitizedMonsterClipController()
    {
        if (_sanitizedMonsterClipController != null && _monsterSanitizeVersionApplied == MonsterSanitizeVersion)
            return _sanitizedMonsterClipController;

        _sanitizedMonsterClipController = null;
        var baseCtrl = LoadMonsterClipController();
        if (baseCtrl == null) return null;

        var over = new AnimatorOverrideController(baseCtrl);
        var clips = over.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            var src = clips[i];
            if (src == null) continue;
            over[src.name] = SanitizeMonsterTransformClip(src);
        }
        _sanitizedMonsterClipController = over;
        _monsterSanitizeVersionApplied = MonsterSanitizeVersion;
        return _sanitizedMonsterClipController;
    }

    static AnimationClip SanitizeMonsterTransformClip(AnimationClip source)
    {
        if (source == null) return null;

        var clip = UnityEngine.Object.Instantiate(source);
        clip.name = source.name;
        float len = Mathf.Max(0.05f, clip.length);
        const string path = "Monsters";
        clip.SetCurve(path, typeof(Transform), "localPosition.z", AnimationCurve.Constant(0f, len, 0f));
        StripEmptyAnimationEvents(clip);
        return clip;
    }

    void ConfigureMonsterClipAnimator(Animator animator)
    {
        if (animator == null) return;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var ctrl = LoadSanitizedMonsterClipController();
        if (ctrl != null)
            animator.runtimeAnimatorController = ctrl;
        SanitizeMonsterClipEvents(animator.runtimeAnimatorController);
        PlayMonsterClip("idle");
        StabilizeMonsterBodyTransform();
    }

    /// <summary>ani 片段里若留了空 AnimationEvent，Unity 会刷报错。</summary>
    static void SanitizeMonsterClipEvents(RuntimeAnimatorController ctrl)
    {
        if (ctrl == null) return;
        var clips = ctrl.animationClips;
        if (clips == null) return;
        for (int i = 0; i < clips.Length; i++)
            StripEmptyAnimationEvents(clips[i]);
    }

    static void StripEmptyAnimationEvents(AnimationClip clip)
    {
        if (clip == null) return;
        var evts = clip.events;
        if (evts == null || evts.Length == 0) return;
        int keep = 0;
        for (int i = 0; i < evts.Length; i++)
        {
            if (!string.IsNullOrEmpty(evts[i].functionName))
                keep++;
        }
        if (keep == evts.Length) return;
        if (keep == 0)
        {
            clip.events = System.Array.Empty<AnimationEvent>();
            return;
        }
        var filtered = new AnimationEvent[keep];
        int w = 0;
        for (int i = 0; i < evts.Length; i++)
        {
            if (!string.IsNullOrEmpty(evts[i].functionName))
                filtered[w++] = evts[i];
        }
        clip.events = filtered;
    }

    Transform GetMonsterBodyTransform()
    {
        if (_monsterBody != null) return _monsterBody;
        if (_sr != null)
        {
            _monsterBody = _sr.transform;
            return _monsterBody;
        }
        _monsterBody = transform.Find("Monsters");
        return _monsterBody;
    }

    /// <summary>
    /// ani 片段已按 scale=1 录制；仅修正 Visual 内 Monsters 贴图节点 z=0。
    /// 战斗位移在 MonsterBody 父级，互不干扰。
    /// </summary>
    public void StabilizeMonsterBodyTransform()
    {
        if (!_monsterClipMode) return;
        Transform body = GetMonsterBodyTransform();
        if (body == null) return;

        // 只钉 z，不覆盖 Animator 驱动的 scale/xy/rotation
        Vector3 lp = body.localPosition;
        if (Mathf.Abs(lp.z) > 0.0001f)
            body.localPosition = new Vector3(lp.x, lp.y, 0f);
    }

    void PlayMonsterClip(string stateName)
    {
        if (!_monsterClipMode || _animator == null || string.IsNullOrEmpty(stateName)) return;
        _animator.Play(stateName, 0, 0f);
    }

    float GetMonsterClipLength(string stateName, float fallback)
    {
        if (_animator == null || _animator.runtimeAnimatorController == null) return fallback;
        var clips = _animator.runtimeAnimatorController.animationClips;
        if (clips == null) return fallback;
        for (int i = 0; i < clips.Length; i++)
        {
            var c = clips[i];
            if (c != null && c.name == stateName)
                return Mathf.Max(0.05f, c.length);
        }
        return fallback;
    }

    void RestoreMonsterLocomotionClip()
    {
        if (!_monsterClipMode || _isDead || _attackAnimLock > 0f) return;
        PlayMonsterClip(_isMoving ? "run" : "idle");
    }

    void OnEnable()
    {
        if (_procMode)
            CacheBaseScale();
    }

    void CacheBaseScale()
    {
        if (_sr != null)
        {
            _procBaseScale = _sr.transform.localScale;
            _procBasePos = _sr.transform.localPosition;
        }
    }

    /// <summary>
    /// 公开方法：精灵加载后缩放已变化，重新缓存基础缩放
    /// 怪物从对象池取出并调用 LoadSprite 后需要调用此方法
    /// </summary>
    public void RecacheBaseScale()
    {
        CacheBaseScale();
    }

    void Update()
    {
        // 攻击动画锁递减
        if (_attackAnimLock > 0)
        {
            float prev = _attackAnimLock;
            _attackAnimLock -= Time.deltaTime;
            if (_monsterClipMode && prev > 0f && _attackAnimLock <= 0f)
                RestoreMonsterLocomotionClip();
        }

        // 程序化动画更新
        if (_procMode)
            UpdateProcedural();

        if (_monsterClipMode)
            StabilizeMonsterBodyTransform();
    }

    /// <summary>
    /// 程序化动画：纯代码实现 idle/move/attack/death
    /// 只修改 scale 和 rotation，不碰 position（避免与AI移动冲突）
    /// </summary>
    void UpdateProcedural()
    {
        if (_sr == null) return;
        _procTime += Time.deltaTime;

        Transform t = _sr.transform;

        if (_isDead)
        {
            _procDeathTime += Time.deltaTime;
            float dt = Mathf.Clamp01(_procDeathTime / procDeathDuration);
            float targetAngle = _flipXFacing ? (-90f * _lastFacingDir) : (90f * _lastFacingDir);
            t.localRotation = Quaternion.Lerp(Quaternion.identity, Quaternion.Euler(0, 0, targetAngle), dt);
            if (_procDeathCritKill)
            {
                float slide = GameConfig.CRIT_KILL_DEATH_SLIDE * (1f - dt) * -_lastFacingDir;
                t.localPosition = _procBasePos + new Vector3(slide, 0f, 0f);
            }
            Color c = _sr.color;
            c.a = 1f - dt;
            _sr.color = c;
            return;
        }

        if (_attackAnimLock > 0)
        {
            // 攻击动画。_procAmp>1 时是在放技能，整体幅度放大
            float atkProgress = 1f - (_attackAnimLock / _attackAnimDuration);
            float wave = Mathf.Sin(atkProgress * Mathf.PI);
            float amp = Mathf.Max(0.1f, _procAmp);

            if (_procAttackKit == AttackVfxKit.Bow)
            {
                // 弓箭：后拉再前送
                float pull = wave * 0.14f * amp;
                t.localScale = new Vector3(
                    _procBaseScale.x * (1f - pull * 0.6f),
                    _procBaseScale.y * (1f + pull * 0.25f),
                    _procBaseScale.z);
                float tilt = -wave * 8f * amp * _lastFacingDir;
                t.localRotation = Quaternion.Euler(0, 0, tilt);
            }
            else if (_procAttackKit == AttackVfxKit.Orb)
            {
                float pulse = wave * 0.16f * amp;
                t.localScale = new Vector3(
                    _procBaseScale.x * (1f + pulse * 0.3f),
                    _procBaseScale.y * (1f + pulse),
                    _procBaseScale.z);
                // 施法时加一点回摆，别只是干缩放
                t.localRotation = Quaternion.Euler(0, 0, -wave * 6f * (amp - 1f) * _lastFacingDir);
            }
            else
            {
                float stretch = wave * procAttackStretch * amp;
                t.localScale = new Vector3(
                    _procBaseScale.x * (1f + stretch),
                    _procBaseScale.y * (1f - stretch * 0.5f),
                    _procBaseScale.z);
                t.localRotation = Quaternion.Euler(0, 0, -wave * 10f * (amp - 1f) * _lastFacingDir);
            }

            // 前扑：只动精灵子节点，AI 的根节点位移不受影响
            if (_procLunge > 0.0001f)
            {
                float push = wave * _procLunge * _lastFacingDir;
                t.localPosition = _procBasePos + new Vector3(push, wave * _procLunge * 0.25f, 0f);
            }
            else if (t.localPosition != _procBasePos)
                t.localPosition = _procBasePos;
        }
        else if (_isMoving)
        {
            // 攻击结束后把前扑位移还回去，否则会永久偏一格
            if (t.localPosition != _procBasePos) t.localPosition = _procBasePos;
            // 移动：上下弹跳 + 轻微倾斜（底部固定，靠BottomCenter pivot）
            float bounce = Mathf.Abs(Mathf.Sin(_procTime * procMoveSpeed)) * procMoveAmount;
            float tilt = Mathf.Sin(_procTime * procMoveSpeed) * procMoveTilt * _lastFacingDir;
            t.localScale = new Vector3(
                _procBaseScale.x * (1f - bounce * 0.4f),
                _procBaseScale.y * (1f + bounce),
                _procBaseScale.z
            );
            t.localRotation = Quaternion.Euler(0, 0, tilt);
        }
        else
        {
            if (t.localPosition != _procBasePos) t.localPosition = _procBasePos;
            // 站立：呼吸（Y轴轻微缩放，底部固定）
            float breath = Mathf.Sin(_procTime * procIdleSpeed) * procIdleAmount;
            t.localScale = new Vector3(
                _procBaseScale.x * (1f + breath * 0.5f),
                _procBaseScale.y * (1f - breath),
                _procBaseScale.z
            );
            t.localRotation = Quaternion.identity;
        }
    }

    /// <summary>
    /// 设置移动状态（站立/移动切换）
    /// 注意：朝向翻转由 UnitBase.ApplyFacing() 统一处理，这里不再重复翻转
    /// </summary>
    public void SetMove(bool isMoving, int facingDir = 1)
    {
        if (_isDead) return;

        _lastFacingDir = facingDir;
        bool stateChanged = _isMoving != isMoving;
        _isMoving = isMoving;

        // SPUM模式
        if (_spum != null && _spum.OverrideController != null)
        {
            if (stateChanged)
            {
                try
                {
                    _spum.PlayAnimation(isMoving ? PlayerState.MOVE : PlayerState.IDLE, 0);
                }
                catch { /* SPUM列表为空时静默跳过 */ }
            }
            ApplyMoveAnimSpeed(isMoving);
        }
        // 原生Animator模式
        else if (_animator != null)
        {
            if (_monsterClipMode)
            {
                if (stateChanged && _attackAnimLock <= 0f)
                    PlayMonsterClip(isMoving ? "run" : "idle");
            }
            else if (stateChanged)
            {
                _animator.SetBool("1_Move", isMoving);
                _animator.SetBool("IsMoving", isMoving);
            }
            ApplyMoveAnimSpeed(isMoving);
        }
    }

    void ApplyMoveAnimSpeed(bool isMoving)
    {
        float spd = isMoving ? moveAnimSpeedScale : 1f;
        if (_animator != null) _animator.speed = spd;
        if (_spum != null && _spum._anim != null && _spum._anim != _animator)
            _spum._anim.speed = spd;
    }

    void LateUpdate()
    {
        if (_monsterClipMode)
            StabilizeMonsterBodyTransform();

        // 每帧锁住移动动画速率（SPUM PlayAnimation 可能把 speed 打回 1）
        if (_isMoving && !_isDead)
            ApplyMoveAnimSpeed(true);

        // 攻击全程 SPUM 会改武器贴图/ItemPath，LateUpdate 脏检查回填（比协程只补 2 帧更稳）
        if (_attackAnimLock > 0f)
        {
            var costume = GetCostumeManager();
            if (costume != null)
            {
                costume.ReapplyWeaponVisuals();
                costume.SuppressUnequippedSecondaryHand();
            }
        }
    }

    HeroCostumeManager GetCostumeManager()
    {
        if (_costume == null)
            _costume = HeroCostumeManager.Instance;
        return _costume;
    }

    void ResyncWeaponBeforeSpumAttack()
    {
        var costume = GetCostumeManager();
        if (costume != null)
            costume.ReapplyWeaponVisuals();
    }

    /// <summary>绑定 Prefabs/Monster/ani 下的 idle/run/attack/dead 片段动画。</summary>
    public void EnableMonsterClipAnimator(SpriteRenderer bodySr = null)
    {
        _procMode = false;
        _monsterClipMode = true;
        _monsterBody = null;
        if (bodySr != null)
            _sr = bodySr;
        if (_sr == null)
            _sr = FindMonsterBodySprite() ?? GetComponentInChildren<SpriteRenderer>(true);

        if (_animator == null)
            _animator = GetComponent<Animator>();
        if (_animator == null)
            _animator = gameObject.AddComponent<Animator>();

        _animator.enabled = true;
        var ctrl = LoadSanitizedMonsterClipController();
        if (ctrl != null)
            _animator.runtimeAnimatorController = ctrl;

        if (!IsMonsterClipController(_animator.runtimeAnimatorController))
        {
            _monsterClipMode = false;
            ForceProceduralMode(_sr);
            return;
        }

        ConfigureMonsterClipAnimator(_animator);
    }

    /// <summary>强制程序化动画（无 Monster 控制器时的兜底）</summary>
    public void ForceProceduralMode(SpriteRenderer bodySr = null)
    {
        _monsterClipMode = false;
        if (_animator != null)
        {
            Destroy(_animator);
            _animator = null;
        }
        _spum = null;
        if (bodySr != null)
            _sr = bodySr;
        if (_sr == null)
            _sr = FindMonsterBodySprite() ?? GetComponentInChildren<SpriteRenderer>(true);
        _procMode = _sr != null;
        if (_procMode)
            CacheBaseScale();
    }

    /// <summary>优先用 Monsters 子节点，避免误绑到 HPBar 导致「怪不动」</summary>
    SpriteRenderer FindMonsterBodySprite()
    {
        Transform body = transform.Find("Monsters");
        if (body != null)
        {
            var s = body.GetComponent<SpriteRenderer>();
            if (s != null) return s;
        }
        var all = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            Transform p = all[i].transform;
            bool underHp = false;
            while (p != null && p != transform)
            {
                if (p.name == "HPBar") { underHp = true; break; }
                p = p.parent;
            }
            if (!underHp) return all[i];
        }
        return null;
    }

    /// <summary>我方近战命中延迟（unscaled 秒）。</summary>
    public float GetAllyMeleeHitDelay()
    {
        return _baseAttackDuration * GameConfig.ALLY_MELEE_HIT_NORM;
    }

    public bool InDamagedRecovery() => Time.unscaledTime < _damagedRecoveryUntil;

    /// <summary>攻击出手时打断受击白闪与 recovery。</summary>
    public void InterruptDamaged()
    {
        _damagedRecoveryUntil = 0f;
        _spumFlashGen++;
        RestoreSpumFlashColors();
    }

    /// <summary>
    /// 播放攻击动画。传入武器套装可选中 SPUM 里的弓/法术专用挥击。
    /// </summary>
    public void PlayAttack(AttackVfxKit kit = AttackVfxKit.MeleeSlash, bool isCritAmp = false)
    {
        if (_isDead) return;
        InterruptDamaged();
        if (_attackAnimLock > 0) return; // 动画锁定中
        _attackAnimDuration = _baseAttackDuration;
        _attackAnimLock = _attackAnimDuration;
        _procAttackKit = kit;
        _procAmp = isCritAmp ? GameConfig.ALLY_MELEE_CRIT_AMP : 1f;
        _procLunge = 0f;

        // SPUM模式
        if (_spum != null && _spum.OverrideController != null)
        {
            ResyncWeaponBeforeSpumAttack();
            try
            {
                _spum.PlayAnimation(PlayerState.ATTACK, ResolveSpumAttackIndex(kit));
            }
            catch { }
            ResyncWeaponBeforeSpumAttack();
        }
        // 原生Animator模式
        else if (_animator != null)
        {
            if (_monsterClipMode)
            {
                PlayMonsterClip("attack");
                _attackAnimDuration = GetMonsterClipLength("attack", _baseAttackDuration);
                _attackAnimLock = _attackAnimDuration;
            }
            else
            {
                SetTriggerSafe("2_Attack");
                SetTriggerSafe("Attack");
                SetTriggerSafe("attack");
            }
        }
        // 程序化模式：Update自动处理攻击拉伸
    }

    [Header("技能动作（怪物放技能时的夸张程度）")]
    [Tooltip("放技能时的幅度倍率：1=和普攻一样")]
    public float procSkillAmp = 2.2f;
    [Tooltip("放技能时向前扑的距离（世界单位）")]
    public float procSkillLunge = 0.22f;
    [Tooltip("放技能的动作时长")]
    public float procSkillDuration = 0.6f;

    /// <summary>
    /// 释放技能的动作：比普攻幅度大一截，外加一个向前扑的位移。
    /// 位移只作用在精灵子节点上，不动根节点，免得和 AI 寻路打架。
    /// 技能必须能打断普攻动画锁，否则刚普攻完放技能会看不到动作。
    /// </summary>
    public void PlaySkillCast(AttackVfxKit kit = AttackVfxKit.MeleeSlash, float ampMul = 1f)
    {
        if (_isDead) return;

        _attackAnimDuration = Mathf.Max(0.2f, procSkillDuration);
        _attackAnimLock = _attackAnimDuration;
        _procAttackKit = kit;
        _procAmp = Mathf.Max(1f, procSkillAmp * Mathf.Max(0.1f, ampMul));
        // 远程施法原地不动更合理，近战才往前扑
        _procLunge = kit == AttackVfxKit.MeleeSlash ? procSkillLunge : procSkillLunge * 0.35f;

        if (_spum != null && _spum.OverrideController != null)
        {
            ResyncWeaponBeforeSpumAttack();
            try { _spum.PlayAnimation(PlayerState.ATTACK, ResolveSpumAttackIndex(kit)); }
            catch { }
            ResyncWeaponBeforeSpumAttack();
        }
        else if (_animator != null)
        {
            if (_monsterClipMode)
            {
                PlayMonsterClip("attack");
                _attackAnimDuration = GetMonsterClipLength("attack", procSkillDuration);
                _attackAnimLock = _attackAnimDuration;
            }
            else
            {
                SetTriggerSafe("3_Skill");
                SetTriggerSafe("2_Attack");
                SetTriggerSafe("Attack");
            }
        }
    }

    // 套装 → ATTACK_List 下标，首次解析后缓存
    readonly System.Collections.Generic.Dictionary<AttackVfxKit, int> _spumAttackIndex
        = new System.Collections.Generic.Dictionary<AttackVfxKit, int>();

    /// <summary>
    /// 在 SPUM 的 ATTACK 片段里按名字挑对应武器的挥击。
    /// SPUM 命名形如 0_Attack_Bow / 1_Skill_Bow / 0_Attack_Magic / 0_Attack_Normal。
    /// </summary>
    int ResolveSpumAttackIndex(AttackVfxKit kit)
    {
        if (_spumAttackIndex.TryGetValue(kit, out int cached)) return cached;

        int index = 0;
        var list = _spum != null ? _spum.ATTACK_List : null;
        if (list != null && list.Count > 1)
        {
            string[] keys = KeywordsFor(kit);
            int fallback = -1;
            for (int i = 0; i < list.Count && index == 0; i++)
            {
                if (list[i] == null) continue;
                string n = list[i].name.ToLowerInvariant();
                for (int k = 0; k < keys.Length; k++)
                {
                    if (n.IndexOf(keys[k], System.StringComparison.Ordinal) < 0) continue;
                    // 优先普攻片段（Attack_），技能片段（Skill_）只作备选
                    if (n.IndexOf("skill", System.StringComparison.Ordinal) >= 0)
                    {
                        if (fallback < 0) fallback = i;
                    }
                    else
                    {
                        index = i;
                    }
                    break;
                }
            }
            if (index == 0 && fallback >= 0) index = fallback;
        }

        _spumAttackIndex[kit] = index;
        return index;
    }

    static string[] KeywordsFor(AttackVfxKit kit)
    {
        switch (kit)
        {
            case AttackVfxKit.Bow: return new[] { "bow", "arrow", "range" };
            case AttackVfxKit.Orb: return new[] { "magic", "staff", "wand" };
            default: return new[] { "normal", "sword", "melee" };
        }
    }

    /// <summary>
    /// 播放死亡动画（facingDir 用于程序化后倒方向）
    /// </summary>
    public void PlayDeath(int facingDir = 0, bool isCritKill = false)
    {
        _isDead = true;
        _isMoving = false;
        _procDeathTime = 0f;
        _procDeathCritKill = isCritKill;
        if (facingDir != 0) _lastFacingDir = facingDir;

        if (_spum != null && _spum.OverrideController != null)
        {
            try
            {
                _spum.PlayAnimation(PlayerState.DEATH, 0);
            }
            catch { }
        }
        else if (_animator != null)
        {
            if (_monsterClipMode)
            {
                PrepareMonsterClipDeathFacing(facingDir);
                PlayMonsterClip("dead");
            }
            else
            {
                _animator.SetBool("isDeath", true);
                _animator.SetBool("IsDead", true);
                SetTriggerSafe("Death");
                SetTriggerSafe("3_Death");
            }
        }
    }

    void PrepareMonsterClipDeathFacing(int facingDir)
    {
        if (_sr == null) return;
        _sr.flipX = false;
        Transform body = GetMonsterBodyTransform();
        if (body == null) return;
        // 朝向已在 Monster Visual 根节点镜像，此处保持 Monsters 子节点 scale 为正，避免死亡动画双重翻转
        float absX = Mathf.Max(0.001f, Mathf.Abs(body.localScale.x));
        body.localScale = new Vector3(absX, body.localScale.y, body.localScale.z);
    }

    public void SetFlipXFacing(bool on) => _flipXFacing = on;

    /// <summary>
    /// 播放眩晕/Debuff 动画（SPUM 的 DEBUFF）。
    /// </summary>
    public void PlayDebuff()
    {
        if (_isDead) return;
        if (_spum != null && _spum.OverrideController != null)
        {
            try
            {
                _spum.PlayAnimation(PlayerState.DEBUFF, 0);
            }
            catch { }
            if (_animator != null)
                _animator.SetBool("5_Debuff", true);
        }
        else if (_animator != null)
        {
            _animator.SetBool("5_Debuff", true);
            SetTriggerSafe("Debuff");
            SetTriggerSafe("5_Debuff");
        }
    }

    public void ClearDebuff()
    {
        if (_animator != null)
            _animator.SetBool("5_Debuff", false);
        if (_spum != null && _spum.OverrideController != null)
        {
            try { _spum.PlayAnimation(PlayerState.IDLE, 0); }
            catch { }
        }
    }

    /// <summary>
    /// 播放受伤动画
    /// </summary>
    public void PlayDamaged()
    {
        if (_isDead) return;
        _damagedRecoveryUntil = Time.unscaledTime + DamagedRecoverySeconds;

        // SPUM模式
        if (_spum != null && _spum.OverrideController != null)
        {
            try
            {
                _spum.PlayAnimation(PlayerState.DAMAGED, 0);
            }
            catch { }
        }
        // 原生Animator模式
        else if (_animator != null)
        {
            SetTriggerSafe("Damaged");
            SetTriggerSafe("4_Damaged");
            SetTriggerSafe("Hit");
        }
        // 程序化模式：受伤时短暂闪烁
        if (_procMode && _sr != null)
        {
            StartCoroutine(ProcDamagedFlash());
        }
        // SPUM/Hero：身体贴图白闪
        else if (_spum != null)
        {
            StartCoroutine(SpumDamagedFlash());
        }
    }

    public void RestoreSpumFlashColors()
    {
        if (_spum == null) return;
        CacheSpumFlashRenderers();
        RestoreSpumFlashFromBaseline();
    }

    System.Collections.IEnumerator SpumDamagedFlash()
    {
        int gen = ++_spumFlashGen;
        // 闪白期间禁止把白闪采成基准，否则还原会永远停在白色
        if (_spumFlashBaseline == null || _spumFlashSrs == null || _spumFlashSrs.Length == 0)
            CacheSpumFlashRenderers();
        var srs = _spumFlashSrs;
        if (srs == null || srs.Length == 0 || _spumFlashBaseline == null) yield break;

        RestoreSpumFlashFromBaseline();

        int shadowCount = 0;
        int flashed = 0;
        for (int i = 0; i < srs.Length; i++)
        {
            if (srs[i] == null) continue;
            if (IsShadowRenderer(srs[i]))
            {
                shadowCount++;
                continue;
            }
            var c = _spumFlashBaseline[i];
            srs[i].color = new Color(1f, 1f, 1f, c.a);
            flashed++;
        }
        // #region agent log
        DebugAgentLog.Log("H1", "UnitAnimation.SpumDamagedFlash", "flash_start",
            $"{{\"gen\":{gen},\"flashed\":{flashed},\"shadowSr\":{shadowCount},\"unit\":\"{EscapeJson(gameObject.name)}\"}}");
        // #endregion

        yield return new WaitForSecondsRealtime(0.12f);

        if (gen != _spumFlashGen || _isDead) yield break;
        RestoreSpumFlashFromBaseline();
        // #region agent log
        DebugAgentLog.Log("H1", "UnitAnimation.SpumDamagedFlash", "flash_restore_baseline",
            $"{{\"gen\":{gen},\"latestGen\":{_spumFlashGen}}}");
        // #endregion
    }

    static bool IsShadowRenderer(SpriteRenderer sr)
    {
        if (sr == null || sr.gameObject == null) return false;
        if (HeroWeaponRig.IsShieldRenderer(sr)) return false;
        string n = sr.gameObject.name;
        return n.IndexOf("shadow", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static string EscapeJson(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    System.Collections.IEnumerator ProcDamagedFlash()
    {
        Color origColor = _sr.color;
        _sr.color = new Color(1f, 0.3f, 0.3f, origColor.a);
        yield return new WaitForSecondsRealtime(0.12f);
        if (_sr != null && !_isDead)
            _sr.color = origColor;
    }

    /// <summary>
    /// 重置到站立状态（复活/重置时调用）
    /// </summary>
    public void ResetToIdle()
    {
        _isDead = false;
        _isMoving = false;
        _attackAnimLock = 0;
        _procDeathTime = 0f;
        _procDeathCritKill = false;

        // 程序化模式：恢复缩放、旋转、颜色
        if (_procMode && _sr != null)
        {
            _sr.transform.localScale = _procBaseScale;
            _sr.transform.localRotation = Quaternion.identity;
            _sr.color = Color.white;
        }

        if (_spum != null && _spum.OverrideController != null)
        {
            try
            {
                _spum.PlayAnimation(PlayerState.IDLE, 0);
            }
            catch { }
        }
        else if (_animator != null)
        {
            if (_monsterClipMode)
            {
                _animator.speed = 1f;
                PlayMonsterClip("idle");
            }
            else
            {
                _animator.SetBool("isDeath", false);
                _animator.SetBool("1_Move", false);
            }
        }
    }

    /// <summary>
    /// 安全设置Trigger（参数不存在时静默跳过）
    /// </summary>
    void SetTriggerSafe(string paramName)
    {
        if (_animator == null) return;
        foreach (var param in _animator.parameters)
        {
            if (param.name == paramName && param.type == AnimatorControllerParameterType.Trigger)
            {
                _animator.SetTrigger(paramName);
                return;
            }
        }
    }

    /// <summary>
    /// 设置攻击动画持续时间
    /// </summary>
    public void SetAttackDuration(float duration)
    {
        _attackAnimDuration = duration;
        _baseAttackDuration = duration;
    }

    public bool IsDead => _isDead;
    public bool IsMoving => _isMoving;
    public bool IsProcMode => _procMode || _monsterClipMode;
    public bool IsProceduralAnim => _procMode;
    public bool UsesFlipXFacing => _flipXFacing;
}
