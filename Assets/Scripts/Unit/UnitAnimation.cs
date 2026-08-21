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
public class UnitAnimation : MonoBehaviour
{
    private SPUM_Prefabs _spum;
    private Animator _animator;
    private SpriteRenderer _sr;

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
    private AttackVfxKit _procAttackKit = AttackVfxKit.MeleeSlash;
    private Vector3 _procBaseScale;
    private Vector3 _procBasePos;
    private float _procTime;
    private float _procDeathTime;
    /// <summary>本次攻击的幅度倍率：普攻 1，放技能时放大。</summary>
    private float _procAmp = 1f;
    /// <summary>放技能时向前扑的距离（世界单位，作用在精灵子节点上，不影响 AI 移动）。</summary>
    private float _procLunge;

    void Awake()
    {
        // 强制用配置值，避免预制体里序列化成 1 导致「看起来没减速」
        moveAnimSpeedScale = GameConfig.MOVE_ANIM_SPEED_SCALE;
        procMoveSpeed = 1.75f;

        _spum = GetComponent<SPUM_Prefabs>();
        _animator = GetComponent<Animator>();
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
            _attackAnimLock -= Time.deltaTime;

        // 程序化动画更新
        if (_procMode)
            UpdateProcedural();
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
            // 死亡：向后倒（与朝向相反一侧）+ Alpha淡出
            // 面朝左(dir=-1) → 向右倒(Z=-90)；面朝右 → 向左倒(Z=+90)
            _procDeathTime += Time.deltaTime;
            float dt = Mathf.Clamp01(_procDeathTime / procDeathDuration);
            float targetAngle = 90f * _lastFacingDir;
            t.localRotation = Quaternion.Lerp(Quaternion.identity, Quaternion.Euler(0, 0, targetAngle), dt);
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
            if (stateChanged)
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
        // 每帧锁住移动动画速率（SPUM PlayAnimation 可能把 speed 打回 1）
        if (_isMoving && !_isDead)
            ApplyMoveAnimSpeed(true);
    }

    /// <summary>强制程序化动画（怪物删掉 Animator 后必须调用，否则攻击/移动动画不播）</summary>
    public void ForceProceduralMode(SpriteRenderer bodySr = null)
    {
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

    /// <summary>
    /// 播放攻击动画。传入武器套装可选中 SPUM 里的弓/法术专用挥击。
    /// </summary>
    public void PlayAttack(AttackVfxKit kit = AttackVfxKit.MeleeSlash)
    {
        if (_isDead) return;
        if (_attackAnimLock > 0) return; // 动画锁定中
        _attackAnimDuration = _baseAttackDuration;
        _attackAnimLock = _attackAnimDuration;
        _procAttackKit = kit;
        _procAmp = 1f;
        _procLunge = 0f;

        // SPUM模式
        if (_spum != null && _spum.OverrideController != null)
        {
            try
            {
                _spum.PlayAnimation(PlayerState.ATTACK, ResolveSpumAttackIndex(kit));
            }
            catch { }
        }
        // 原生Animator模式
        else if (_animator != null)
        {
            SetTriggerSafe("2_Attack");
            SetTriggerSafe("Attack");
            SetTriggerSafe("attack");
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
            try { _spum.PlayAnimation(PlayerState.ATTACK, ResolveSpumAttackIndex(kit)); }
            catch { }
        }
        else if (_animator != null)
        {
            SetTriggerSafe("3_Skill");
            SetTriggerSafe("2_Attack");
            SetTriggerSafe("Attack");
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
    public void PlayDeath(int facingDir = 0)
    {
        _isDead = true;
        _isMoving = false;
        _procDeathTime = 0f;
        if (facingDir != 0) _lastFacingDir = facingDir;

        // SPUM模式
        if (_spum != null && _spum.OverrideController != null)
        {
            try
            {
                _spum.PlayAnimation(PlayerState.DEATH, 0);
            }
            catch { }
        }
        // 原生Animator模式
        else if (_animator != null)
        {
            _animator.SetBool("isDeath", true);
            _animator.SetBool("IsDead", true);
            SetTriggerSafe("Death");
            SetTriggerSafe("3_Death");
        }
        // 程序化模式：Update自动处理倒下+淡出
    }

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
    }

    System.Collections.IEnumerator ProcDamagedFlash()
    {
        Color origColor = _sr.color;
        _sr.color = new Color(1f, 0.3f, 0.3f, origColor.a);
        yield return new WaitForSeconds(0.1f);
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
            _animator.SetBool("isDeath", false);
            _animator.SetBool("1_Move", false);
            _animator.Play("IDLE", 0, 0f);
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
    public bool IsProcMode => _procMode;
}
