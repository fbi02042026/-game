using System.Collections;
using UnityEngine;

/// <summary>
/// 战斗打击感统一入口：顿帧 / 镜头震 / 音效 / 连杀递进。
/// 各子效果由 GameConfig.COMBAT_JUICE_* 分步开关控制。
/// </summary>
public class CombatJuice : Singleton<CombatJuice>
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
            victim.ApplyKnockback(-victim.facingDir * (isCrit ? 0.12f : 0.06f));
    }

    /// <summary>击杀最后一击：加强振屏（BattleManager.OnMonsterDead 调用）。</summary>
    public void OnKillFinisher()
    {
        if (GameConfig.COMBAT_JUICE_CAMERA_SHAKE)
            GetCameraFollow()?.AddShake(0.12f, 0.18f);
        if (GameConfig.COMBAT_JUICE_HIT_STOP)
            RequestHitStop(0.04f);
    }

    /// <summary>近战出手方前冲（Attack 里调用）。</summary>
    public void OnMeleeAttackLunge(UnitBase attacker)
    {
        if (attacker == null || !GameConfig.COMBAT_JUICE_KNOCKBACK) return;
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
        }

        if (combo >= 10 && !_comboToastShown.Contains("10"))
        {
            _comboToastShown.Add("10");
            UIManager.Instance?.ShowToast("连击！");
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
            return GameConfig.HIT_STOP_CRIT;
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

        if (Time.timeScale <= 0.01f)
            Time.timeScale = _hitStopSavedScale > 0.01f ? _hitStopSavedScale : 1f;

        _hitStopUntil = 0f;
        _hitStopCo = null;
    }
}
