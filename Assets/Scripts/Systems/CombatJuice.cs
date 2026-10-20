using System.Collections;
using UnityEngine;

/// <summary>
/// 战斗打击感统一入口：顿帧 / 镜头震 / 音效 / 连杀递进。
/// 各子效果由 GameConfig.COMBAT_JUICE_* 分步开关控制。
/// </summary>
public class CombatJuice : Singleton<CombatJuice>, ICombatBoundSingleton
{
    const float HitJuiceCooldown = 0.08f;
    const float SfxCooldown = 0.08f;

    float _lastOnHitTime = -999f;
    float _hitStopUntil;
    Coroutine _hitStopCo;
    float _hitStopSavedScale = 1f;

    float _lastSfxTime = -999f;
    string _lastSfxKey;

    CameraFollow _cameraFollow;

    AudioClip _sfxHitOut;
    AudioClip _sfxHitIn;
    AudioClip _sfxCrit;
    AudioClip _sfxDodge;
    bool _sfxWarned;

    static readonly System.Collections.Generic.HashSet<string> _comboToastShown =
        new System.Collections.Generic.HashSet<string>();

    protected override void Awake()
    {
        base.Awake();
        CacheClips();
    }

    void CacheClips()
    {
        _sfxHitOut = Resources.Load<AudioClip>("Audio/SFX/hit_melee_out");
        _sfxHitIn = Resources.Load<AudioClip>("Audio/SFX/hit_melee_in");
        _sfxCrit = Resources.Load<AudioClip>("Audio/SFX/hit_crit");
        _sfxDodge = Resources.Load<AudioClip>("Audio/SFX/dodge");
        if (!_sfxWarned && _sfxHitOut == null && _sfxHitIn == null)
        {
            _sfxWarned = true;
            GamePerf.Log("[CombatJuice] 未找到 Resources/Audio/SFX/hit_* ，音效步骤可后补资源");
        }
    }

    CameraFollow GetCameraFollow()
    {
        if (_cameraFollow != null) return _cameraFollow;
        _cameraFollow = Object.FindObjectOfType<CameraFollow>();
        return _cameraFollow;
    }

    /// <summary>
    /// 精英/Boss 红圈/扇形预警结束、技能真正释放瞬间的屏幕震动。
    /// 通过单例取相机跟随组件触发，无需持有实例。
    /// </summary>
    public static void Shake(float amplitude, float duration)
    {
        Instance?.GetCameraFollow()?.AddShake(amplitude, duration);
    }

    /// <summary>受击结算后调用（finalDamage &gt; 0）。</summary>
    public void OnHit(UnitBase victim, float finalDamage, bool isCrit, bool showJuice)
    {
        if (victim == null || finalDamage <= 0f || !showJuice) return;
        if (Time.unscaledTime - _lastOnHitTime < HitJuiceCooldown) return;
        _lastOnHitTime = Time.unscaledTime;

        if (GameConfig.COMBAT_JUICE_HIT_STOP)
            RequestHitStop(ResolveHitStopDuration(victim, isCrit));

        if (GameConfig.COMBAT_JUICE_CAMERA_SHAKE)
            ApplyHitShake(victim, isCrit);

        if (GameConfig.COMBAT_JUICE_SFX)
            PlayHitSfx(victim, isCrit);

        // 我方单位不做受击击退，避免「往后顿」手感发飘
        if (GameConfig.COMBAT_JUICE_KNOCKBACK && !victim.isAlly)
        {
            float kb = GameConfig.COMBAT_KNOCKBACK_NORMAL;
            if (isCrit)
                kb = victim.currentHp <= 0
                    ? GameConfig.COMBAT_KNOCKBACK_CRIT_KILL
                    : GameConfig.COMBAT_KNOCKBACK_CRIT;
            victim.ApplyKnockback(-victim.facingDir * kb);
        }

        // 受击挤压回弹：同样只压敌人（我方挨打不压，避免「往后顿」发飘）
        if (GameConfig.COMBAT_JUICE_SQUASH && !victim.isAlly)
            victim.GetComponent<UnitAnimation>()?.PunchHitSquash();
    }

    // 【2026-10-20 主人拍板「不要这个了 玩家玩的时候看的一卡一卡的」】
    // 精英/Boss 致死一击的整套特殊演出已删除：
    //   BeginKillWindupJuice / EndKillWindupJuice（致死前摇慢放 timeScale 0.1×0.5s）
    //   BeginCritWindupSlowMo / EndCritWindupSlowMo、玩家跳起放大（CoCritHeroScalePop）
    //   击杀镜头拉近（KILL_CAM）与血条藏显（SetKillCamHidden 调用方）
    //   精英/Boss 处决压暗（PlayFinisherDim / CoFinisherDim）
    // 现在致死一击只走 OnHit + OnKillFinisher，与普通击杀一致。
    // （CameraFollow/ParallaxBackground/BattleUI/BattleBossHpBar/MonsterHealthBar 上的
    //   KillCam* 辅助方法随之闲置，按死代码口径只报告不清理。）

    /// <summary>击杀收刀：Boss 略长顿帧 + 击杀帧轻震（不依赖全局 shake 开关）。</summary>
    public void OnKillFinisher(Monster victim = null)
    {
        float hitStop = GameConfig.KILL_FINISHER_HIT_STOP;
        if (victim != null && victim.IsBossUnit)
            hitStop += GameConfig.KILL_FINISHER_HIT_STOP_BOSS_EXTRA;

        if (GameConfig.COMBAT_JUICE_HIT_STOP)
            RequestHitStop(hitStop);

        if (GameConfig.COMBAT_JUICE_KILL_FINISHER_SHAKE)
            GetCameraFollow()?.AddShake(GameConfig.KILL_FINISHER_SHAKE_AMP, GameConfig.KILL_FINISHER_SHAKE_DUR);

        if (GameConfig.COMBAT_JUICE_CAMERA_SHAKE)
            GetCameraFollow()?.AddShake(0.12f, 0.18f);
    }

    /// <summary>近战/远程出手破空音（命中音仍走 OnHit）。</summary>
    public void PlaySwingSfx()
    {
        if (!GameConfig.COMBAT_JUICE_SFX) return;
        TryPlaySfx(_sfxHitOut, "swing", 0.55f);
    }

    /// <summary>近战出手方前冲（Attack 里调用；仅敌方，我方根节点不位移防滑步）。</summary>
    public void OnMeleeAttackLunge(UnitBase attacker)
    {
        if (attacker == null || !GameConfig.COMBAT_JUICE_KNOCKBACK || attacker.isAlly) return;
        attacker.ApplyKnockback(attacker.facingDir * 0.1f);
    }

    public void OnDodge(bool victimIsAlly)
    {
        if (GameConfig.COMBAT_JUICE_SFX)
            TryPlaySfx(_sfxDodge, "dodge", 0.65f);
        if (GameConfig.COMBAT_JUICE_CAMERA_SHAKE)
        {
            var cam = GetCameraFollow();
            if (cam != null)
                cam.AddShake(0.03f, 0.1f);
        }
    }

    public void OnKillCombo(int combo)
    {
        if (!GameConfig.COMBAT_JUICE_COMBO || combo < 3) return;

        if (combo >= 3)
        {
            DamageTextSystem.SetNextTextScaleMul(1.08f);
            var cam = GetCameraFollow();
            cam?.AddShake(0.05f, 0.12f);
        }

        if (combo >= 5)
        {
            if (GameConfig.COMBAT_JUICE_SFX)
                TryPlaySfx(_sfxCrit, "combo5", 0.75f);
            GetCameraFollow()?.AddShake(0.07f, 0.14f);
            if (!_comboToastShown.Contains("5"))
            {
                _comboToastShown.Add("5");
                DamageTextSystem.SetNextTextScaleMul(1.2f);
                // 【2026-10-17 主人拍板】连杀不再横条飞字，右上角击破计数（BattleSideHud）常驻即可
                if (GameConfig.COMBAT_JUICE_HIT_STOP)
                    RequestHitStop(GameConfig.HIT_STOP_COMBO_ANNOUNCE);
            }
        }

        if (combo >= 10 && !_comboToastShown.Contains("10"))
        {
            _comboToastShown.Add("10");
            DamageTextSystem.SetNextTextScaleMul(1.35f);
            GetCameraFollow()?.AddShake(0.1f, 0.18f);
            if (GameConfig.COMBAT_JUICE_HIT_STOP)
                RequestHitStop(GameConfig.HIT_STOP_COMBO_ANNOUNCE);
        }

        if (combo >= 20 && !_comboToastShown.Contains("20"))
        {
            _comboToastShown.Add("20");
            DamageTextSystem.SetNextTextScaleMul(1.5f);
            GetCameraFollow()?.AddShake(0.14f, 0.22f);
            if (GameConfig.COMBAT_JUICE_HIT_STOP)
                RequestHitStop(GameConfig.HIT_STOP_COMBO_ANNOUNCE);
        }
    }

    public static void ResetComboToast()
    {
        _comboToastShown.Clear();
    }

    float ResolveHitStopDuration(UnitBase victim, bool isCrit)
    {
        if (victim is Monster mon && mon.IsBossUnit)
            return GameConfig.HIT_STOP_BOSS;
        if (isCrit)
            return GameConfig.CRIT_STRIKE_HIT_STOP;
        return GameConfig.HIT_STOP_NORMAL;
    }

    void ApplyHitShake(UnitBase victim, bool isCrit)
    {
        var cam = GetCameraFollow();
        if (cam == null) return;

        float amp;
        if (victim.isAlly)
            amp = isCrit ? 0.09f : 0.06f;
        else
            amp = isCrit ? 0.08f : 0.04f;
        cam.AddShake(amp, isCrit ? 0.14f : 0.12f);
    }

    void PlayHitSfx(UnitBase victim, bool isCrit)
    {
        if (isCrit)
        {
            TryPlaySfx(_sfxCrit, "crit", 0.85f);
            return;
        }
        if (victim.isAlly)
            TryPlaySfx(_sfxHitIn, "in", 0.7f);
        else
            TryPlaySfx(_sfxHitOut, "out", 0.75f);
    }

    void TryPlaySfx(AudioClip clip, string key, float volume)
    {
        if (clip == null) return;
        float now = Time.unscaledTime;
        if (key == _lastSfxKey && now - _lastSfxTime < SfxCooldown) return;
        _lastSfxKey = key;
        _lastSfxTime = now;
        GameAudio.PlaySfx(clip, volume);
    }

    public void RequestHitStop(float durationUnscaled)
    {
        if (durationUnscaled <= 0f) return;
        float end = Time.unscaledTime + durationUnscaled;
        if (end > _hitStopUntil)
            _hitStopUntil = end;
        if (_hitStopCo == null)
            _hitStopCo = StartCoroutine(CoHitStop());
    }

    IEnumerator CoHitStop()
    {
        if (Time.timeScale <= 0.01f)
        {
            _hitStopCo = null;
            yield break;
        }

        _hitStopSavedScale = Time.timeScale;
        Time.timeScale = 0.01f;

        while (Time.unscaledTime < _hitStopUntil)
            yield return null;

        // 【2026-10-20 卡顿修复】只在 timeScale 仍是本协程压下的 0.01 时才恢复；
        // 若期间被剧情/弹窗压成 0（或被别的系统改动），恢复责任归压 0 的一方 ——
        // 旧写法 `<= 0.01f` 会把剧情的定格 0 顶成 1，造成时序交错。
        if (Mathf.Approximately(Time.timeScale, 0.01f))
            Time.timeScale = _hitStopSavedScale > 0.01f ? _hitStopSavedScale : 1f;

        _hitStopUntil = 0f;
        _hitStopCo = null;
    }
}
