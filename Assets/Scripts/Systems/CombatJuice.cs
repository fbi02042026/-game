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

    float _critWindupSavedScale = 1f;
    bool _critWindupActive;

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
        {
            float kb = GameConfig.COMBAT_KNOCKBACK_NORMAL;
            if (isCrit)
                kb = victim.currentHp <= 0
                    ? GameConfig.COMBAT_KNOCKBACK_CRIT_KILL
                    : GameConfig.COMBAT_KNOCKBACK_CRIT;
            victim.ApplyKnockback(-victim.facingDir * kb);
        }
    }

    /// <summary>击杀前摇：慢放 + 镜头拉近（近战 fullWindup=true；远程短版 false）。</summary>
    public void BeginKillWindupJuice(bool fullWindup = true)
    {
        BeginCritWindupSlowMo();
        if (!GameConfig.COMBAT_JUICE_KILL_CAM) return;
        float inDur = fullWindup ? GameConfig.KILL_CAM_ZOOM_IN : GameConfig.KILL_CAM_RANGED_WINDUP;
        float mul = GameConfig.KILL_CAM_ZOOM_MUL;
        GetCameraFollow()?.BeginKillCamZoom(mul, inDur);
        Object.FindObjectOfType<ParallaxBackground>()?.ApplyKillCamZoomMul(mul);
        BattleUI.ApplyKillCamHudCompensation(mul);
        MonsterHealthBar.SetKillCamHidden(true);
        BattleBossHpBar.SetKillCamHidden(true);
    }

    /// <summary>下劈/命中瞬间：还原 timeScale + 镜头瞬间弹回。</summary>
    public void EndKillWindupJuice()
    {
        EndCritWindupSlowMo();
        if (!GameConfig.COMBAT_JUICE_KILL_CAM)
        {
            MonsterHealthBar.SetKillCamHidden(false);
            BattleBossHpBar.SetKillCamHidden(false);
            return;
        }
        GetCameraFollow()?.ForceResetKillCamZoom();
        ResetKillCamScene();
        MonsterHealthBar.SetKillCamHidden(false);
        BattleBossHpBar.SetKillCamHidden(false);
    }

    void ResetKillCamScene()
    {
        Object.FindObjectOfType<ParallaxBackground>()?.ResetKillCamZoom();
        BattleUI.ResetKillCamHudCompensation();
    }

    /// <summary>暴击前摇全屏慢放（BeginKillWindupJuice 内部调用）。</summary>
    public void BeginCritWindupSlowMo()
    {
        if (Time.timeScale <= 0.01f) return;
        if (_critWindupActive) return;
        _critWindupSavedScale = Time.timeScale;
        if (_critWindupSavedScale < 0.01f) _critWindupSavedScale = 1f;
        Time.timeScale = GameConfig.CRIT_WINDUP_TIME_SCALE;
        _critWindupActive = true;
        if (GameConfig.COMBAT_JUICE_SFX)
            TryPlaySfx(_sfxCrit, "crit_windup", 0.35f);
    }

    public void EndCritWindupSlowMo()
    {
        if (!_critWindupActive) return;
        Time.timeScale = _critWindupSavedScale > 0.01f ? _critWindupSavedScale : 1f;
        _critWindupActive = false;
    }

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

        // 精英/Boss 处决一瞬压暗
        if (victim != null && (victim.IsBossUnit || victim.IsEliteWave))
            PlayFinisherDim();
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

    void PlayFinisherDim()
    {
        if (_finisherDimCo != null)
            StopCoroutine(_finisherDimCo);
        _finisherDimCo = StartCoroutine(CoFinisherDim());
    }

    Coroutine _finisherDimCo;
    static CanvasGroup _finisherDimCg;

    IEnumerator CoFinisherDim()
    {
        var cg = EnsureFinisherDim();
        if (cg == null) yield break;
        cg.gameObject.SetActive(true);
        cg.alpha = GameConfig.KILL_FINISHER_DIM_ALPHA;
        float t = 0f;
        float dur = Mathf.Max(0.05f, GameConfig.KILL_FINISHER_DIM_DUR);
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(GameConfig.KILL_FINISHER_DIM_ALPHA, 0f, Mathf.Clamp01(t / dur));
            yield return null;
        }
        cg.alpha = 0f;
        cg.gameObject.SetActive(false);
        _finisherDimCo = null;
    }

    static CanvasGroup EnsureFinisherDim()
    {
        if (_finisherDimCg != null) return _finisherDimCg;
        var go = new GameObject("KillFinisherDim");
        Object.DontDestroyOnLoad(go);
        var canvas = go.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.FullscreenFx);
        go.AddComponent<UnityEngine.UI.GraphicRaycaster>().enabled = false;
        var imgGo = new GameObject("Dim", typeof(RectTransform));
        imgGo.transform.SetParent(go.transform, false);
        var rt = imgGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = imgGo.AddComponent<UnityEngine.UI.Image>();
        img.color = Color.black;
        img.raycastTarget = false;
        _finisherDimCg = go.AddComponent<CanvasGroup>();
        _finisherDimCg.alpha = 0f;
        _finisherDimCg.blocksRaycasts = false;
        _finisherDimCg.interactable = false;
        go.SetActive(false);
        return _finisherDimCg;
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
                GlobalToastUI.ShowFlythrough("连杀 x5");
                if (GameConfig.COMBAT_JUICE_HIT_STOP)
                    RequestHitStop(GameConfig.HIT_STOP_COMBO_ANNOUNCE);
            }
        }

        if (combo >= 10 && !_comboToastShown.Contains("10"))
        {
            _comboToastShown.Add("10");
            GlobalToastUI.ShowFlythrough("连杀 x10");
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

        if (Time.timeScale <= 0.01f)
            Time.timeScale = _hitStopSavedScale > 0.01f ? _hitStopSavedScale : 1f;

        _hitStopUntil = 0f;
        _hitStopCo = null;
    }
}
