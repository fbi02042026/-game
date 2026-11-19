using UnityEngine;

/// <summary>
/// 雨雪粒子控制器（《战斗昼夜天气》清单 §2 / T6）：订阅 BattleAtmosphere.OnChanged，
/// 雨/雪各循环发射一坨全屏氛围粒子，晴/白天收回。复用 BattleVFXSystem.PlayPreparedVfx（:931）
/// + PoolManager；排序随其 PrepareSlashParticles 落到 Default 层 SORT_VFX+5(55)，在单位(15)与 HUD(950) 之间；
/// 关「天气特效」开关（SettingsPopupUI WeatherFx，PlayerPrefs fx.weather.on）无粒子、无报错（debuff 与提示照常）。
/// 退战回收：实例挂在 BattleVFXSystem 下随战斗场景销毁；切天气仅停粒子+隐藏，由 PlayPreparedVfx 的
/// ScheduleRelease 到期自动回池（不手动 Release，避免与到期协程双 release 同对象）。
/// </summary>
public class WeatherVfxController : MonoBehaviour
{
    static WeatherVfxController _instance;

    /// <summary>战斗期按需查找/装配；非 DDOL，随 Battle 场景销毁（与 NightVisionController 同款模式）。</summary>
    public static WeatherVfxController Instance
    {
        get
        {
            if (_instance == null) _instance = FindObjectOfType<WeatherVfxController>();
            if (_instance == null && Application.isPlaying)
            {
                var go = new GameObject("WeatherVfxController");
                _instance = go.AddComponent<WeatherVfxController>();
            }
            return _instance;
        }
    }

    /// <summary>Resources 路径（T7 落盘：Assets/Resources/VFX/Weather/，连 .meta 一起）。</summary>
    const string RainPrefabPath = "VFX/Weather/RainLight";
    const string SnowPrefabPath = "VFX/Weather/SnowSpinny";
    /// <summary>天气特效开关（SettingsPopupUI.WeatherFx 同 key；默认开）。</summary>
    const string WeatherFxPrefKey = "fx.weather.on";
    /// <summary>
    /// 持有寿命（秒）：远长于单关；到期由 BattleVFXSystem.ScheduleRelease 自动回池（双保险），
    /// 平时切天气/退战不走手动 Release（见类注释：避免双 release）。
    /// </summary>
    const float HoldLifetime = 600f;
    /// <summary>预制体发射圆半径（RainLight / SnowSpinny 的 ShapeModule m_Scale 均为 4）。</summary>
    const float EmitRadiusWorld = 4f;

    /// <summary>每类天气首次遭遇提示（进程级一次，§2/T7）。</summary>
    static bool _rainToastDone;
    static bool _snowToastDone;

    BattleAtmosphere _subscribed;
    GameObject _rainGo;
    GameObject _snowGo;
    GameObject _rainPrefab;
    GameObject _snowPrefab;
    bool _rainLoadTried;
    bool _snowLoadTried;
    bool _vfxMissingLogged;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }
        _instance = this;
    }

    void OnDestroy()
    {
        if (_subscribed != null) _subscribed.OnChanged -= OnAtmosphereChanged;
        _subscribed = null;
        // 退战回收：停粒子并隐藏（实例随 BattleVFXSystem/场景销毁，不手动回池）
        Hide(_rainGo);
        Hide(_snowGo);
        _rainGo = null;
        _snowGo = null;
        if (_instance == this) _instance = null;
    }

    void Update()
    {
        EnsureSubscription();
        FollowCamera();
    }

    /// <summary>按 §3 契约绑定状态源：订阅传入氛围实例的 OnChanged 并立即同步当前天气。</summary>
    public void Bind(BattleAtmosphere atmo)
    {
        if (atmo == null) return;
        Subscribe(atmo);
        ApplyWeather(atmo.Weather);
    }

    /// <summary>订阅指定氛围实例（换实例先退订旧的，绝不重复订阅），并立即同步一次当前相位/天气。</summary>
    void Subscribe(BattleAtmosphere atmo)
    {
        if (_subscribed == atmo) return;
        if (_subscribed != null) _subscribed.OnChanged -= OnAtmosphereChanged;
        _subscribed = atmo;
        atmo.OnChanged += OnAtmosphereChanged;
        OnAtmosphereChanged(atmo.Phase, atmo.Weather);
    }

    /// <summary>自动订阅（与 T2/T3/T4 同款懒装配）：没人显式 Bind 时每帧兜底，氛围实例更换时自动改订。</summary>
    void EnsureSubscription()
    {
        var atmo = BattleAtmosphere.Instance;
        if (atmo == null) return;
        Subscribe(atmo);
    }

    void OnAtmosphereChanged(DayPhase phase, WeatherType weather)
    {
        ApplyWeather(weather);
    }

    /// <summary>按天气切换粒子：雨/雪互斥；晴全收。开关关着只提示+debuff（T5 管），无粒子无报错。</summary>
    void ApplyWeather(WeatherType weather)
    {
        ShowWeatherToast(weather);   // 提示与粒子开关无关（debuff 照常生效，T5 已挂）

        if (PlayerPrefs.GetInt(WeatherFxPrefKey, 1) == 0)
        {
            StopAll();
            return;
        }

        if (weather == WeatherType.Rain)
        {
            Hide(_snowGo);
            _snowGo = null;
            EnsureRain();
        }
        else if (weather == WeatherType.Snow)
        {
            Hide(_rainGo);
            _rainGo = null;
            EnsureSnow();
        }
        else
        {
            StopAll();
        }
    }

    /// <summary>雨：取池 → 循环 → 播放（BattleVFXSystem.PlayPreparedVfx 统一入口，禁手搓 Instantiate）。</summary>
    void EnsureRain()
    {
        if (_rainGo != null) return;
        var prefab = LoadPrefab(RainPrefabPath, ref _rainPrefab, ref _rainLoadTried, "雨");
        if (prefab == null) return;
        _rainGo = SpawnLooping(prefab);
    }

    /// <summary>雪：缓落带漂移（SnowSpinny 预制体自身行为）。</summary>
    void EnsureSnow()
    {
        if (_snowGo != null) return;
        var prefab = LoadPrefab(SnowPrefabPath, ref _snowPrefab, ref _snowLoadTried, "雪");
        if (prefab == null) return;
        _snowGo = SpawnLooping(prefab);
    }

    GameObject SpawnLooping(GameObject prefab)
    {
        var vfx = BattleVFXSystem.Instance;
        if (vfx == null)
        {
            if (!_vfxMissingLogged)
            {
                _vfxMissingLogged = true;
                Debug.LogError("[WeatherVfxController] BattleVFXSystem 未装配，天气粒子无法播放（应由 AutoGameInitializer.EnsureGameRoot 装配）");
            }
            return null;
        }
        var cam = Camera.main;
        Vector3 pos = cam != null ? cam.transform.position : transform.position;
        // PlayPreparedVfx：池化取实例 + 修粒子(排序 SORT_VFX+5) + Play + ScheduleRelease(HoldLifetime 到期回池)
        var go = vfx.PlayPreparedVfx(prefab, pos, 1, HoldLifetime, ComputeCoverScale(), prepareParticles: true);
        if (go == null) return null;
        SetLooping(go);   // 雨/雪必须循环：与 BattleVFXSystem.SetParticlesLooping(:862) 同款（那里是 private，这里自持一份）
        return go;
    }

    /// <summary>把实例上全部粒子临时改成循环播放（运行时改实例，不碰预制体；团结引擎 2022.3.62t16 用 main.loop）。</summary>
    static void SetLooping(GameObject go)
    {
        if (go == null) return;
        var systems = go.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] == null) continue;
            var main = systems[i].main;
            main.loop = true;
        }
    }

    /// <summary>
    /// 覆盖缩放下样：发射圆半径 4 世界单位，按当前正交相机视口半宽/半高 + 余量换算 scaleMul，
    /// 保证雨雪盖满整屏（含 ViewportFit / 击杀 zoom 改变 ortho 时的当前视口）。
    /// </summary>
    static float ComputeCoverScale()
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return 1f;
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        float need = Mathf.Max(halfW, halfH) + 1.5f;
        return Mathf.Clamp(need / EmitRadiusWorld, 0.5f, 5f);
    }

    /// <summary>每帧把发射器摆到相机视口中心（CameraFollow 跟玩家，雨雪框必须跟着走）。</summary>
    void FollowCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 c = cam.transform.position;
        Recenter(_rainGo, c);
        Recenter(_snowGo, c);
    }

    static void Recenter(GameObject go, Vector3 camPos)
    {
        if (go == null) return;
        Vector3 p = go.transform.position;
        p.x = camPos.x;
        p.y = camPos.y;
        go.transform.position = p;   // z 保持 PlayPreparedVfx 摆好的值（排序层/Order 已管先后）
    }

    GameObject LoadPrefab(string path, ref GameObject cache, ref bool tried, string label)
    {
        if (cache != null) return cache;
        if (tried) return null;   // 缺资源只报一次错，不兜底（铁律：露白）
        tried = true;
        cache = Resources.Load<GameObject>(path);
        if (cache == null)
            Debug.LogError("[WeatherVfxController] 缺少" + label + "粒子预制体：Assets/Resources/" + path + ".prefab（T7 应连 .meta 落盘）");
        return cache;
    }

    void StopAll()
    {
        Hide(_rainGo);
        _rainGo = null;
        Hide(_snowGo);
        _snowGo = null;
    }

    /// <summary>停粒子 + 隐藏；不手动回池（避免与 PlayPreparedVfx 的到期 Release 协程双 release 同一对象）。</summary>
    static void Hide(GameObject go)
    {
        if (go == null) return;
        var pss = go.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < pss.Length; i++)
        {
            if (pss[i] == null) continue;
            pss[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        go.SetActive(false);
    }

    /// <summary>首次遭遇天气一行 toast（每类进程级只提示一次；与 T5 挂的 debuff 数值保持一致）。</summary>
    static void ShowWeatherToast(WeatherType weather)
    {
        if (weather == WeatherType.Rain)
        {
            if (_rainToastDone) return;
            _rainToastDone = true;
            UIManager.Instance?.ShowToast("雨中作战：我方攻速 -5%", true);
        }
        else if (weather == WeatherType.Snow)
        {
            if (_snowToastDone) return;
            _snowToastDone = true;
            UIManager.Instance?.ShowToast("雪中作战：我方移速 -5%", true);
        }
    }
}
