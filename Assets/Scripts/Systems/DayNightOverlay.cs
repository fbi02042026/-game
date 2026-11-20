using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗昼夜径向遮罩（《战斗昼夜天气》清单 §2 / T3 的 C# 侧）：
/// 全屏 UI Image + <c>Battle/NightVignette</c> Shader（DeepSeek 交付，Assets/Shaders/BattleNightVignette.shader），
/// 中心透明（玩家光圈）四周压暗；中心每帧跟随玩家屏幕 UV，半径由 <see cref="BattleAtmosphere.LightRadiusWorld"/>
/// 唯一驱动（对外暴露 <see cref="SetRadius"/>，遮罩挖洞与 T4 显隐判定同源同半径）；
/// 相位切换 1.5~3s 插值 alpha/色调；排序取 <see cref="GameConfig.UiSort.BattleNightVignette"/>(90)：只压场景(10/15/50~82)、停在底部面板(100)之下 → 面板独立不被压暗，
/// raycastTarget=false 不挡操作；绝不开第二相机（历史黑屏教训）。
/// 非 DDOL：随 Battle 场景销毁；白天全透时自动关掉 Image 省一次全屏 pass。
/// </summary>
public class DayNightOverlay : MonoBehaviour
{
    static DayNightOverlay _instance;

    /// <summary>战斗期按需查找/装配；非 DDOL，随 Battle 场景销毁（与 NightVisionController 同款模式）。</summary>
    public static DayNightOverlay Instance
    {
        get
        {
            if (_instance == null) _instance = FindObjectOfType<DayNightOverlay>();
            if (_instance == null && Application.isPlaying)
            {
                var go = new GameObject("DayNightOverlay");
                _instance = go.AddComponent<DayNightOverlay>();
            }
            return _instance;
        }
    }

    /// <summary>径向遮罩 Shader 名（DeepSeek 交付；Properties：_Center/_Radius/_Aspect/_DarkColor/_Softness）。</summary>
    const string VignetteShaderName = "Battle/NightVignette";
    /// <summary>相位切换插值时长（秒），落在 §2 定稿的 1.5~3s 区间。</summary>
    const float PhaseLerpSeconds = 2.2f;
    /// <summary>边缘过渡宽度，单位与 _Radius 相同（屏幕高度比例）。</summary>
    const float Softness = 0.06f;
    /// <summary>LightRadiusWorld 读不到时的兜底（与 T2 默认值同源，避免除零/全黑）。</summary>
    const float DefaultRadiusWorld = 2.88f;
    /// <summary>炼狱章号：夜相压暗红（§2 定稿「7炼狱=夜(暗红压暗)」）。</summary>
    const int HellChapter = 7;

    /// <summary>相位目标色：RGB=压暗色调，A=圈外峰值压暗强度（§2：夜≈0.5 冷蓝）。</summary>
    static readonly Color DayTarget   = new Color(0.06f, 0.10f, 0.22f, 0f);     // 白天：全透（不压暗）
    static readonly Color DuskTarget  = new Color(0.34f, 0.18f, 0.10f, 0.26f);  // 黄昏：轻度暖压（数值待主人手感微调）
    static readonly Color NightTarget = new Color(0.06f, 0.10f, 0.22f, 0.5f);   // 夜：冷蓝 0.5（主人 2026-11-20 拍板再暗一点）
    static readonly Color HellTarget  = new Color(0.32f, 0.08f, 0.06f, 0.45f);  // 炼狱夜：暗红（数值待主人手感微调）

    static Material _vignetteMat;
    static bool _vignetteMatTried;
    static Sprite _whiteSprite;

    BattleAtmosphere _atmo;
    BattleAtmosphere _subscribed;
    Image _image;
    bool _built;

    Color _curColor  = new Color(0.06f, 0.10f, 0.22f, 0f);
    Color _lerpFrom  = new Color(0.06f, 0.10f, 0.22f, 0f);
    Color _targetColor = new Color(0.06f, 0.10f, 0.22f, 0f);
    float _lerpT = 1f;
    float _radiusWorld = DefaultRadiusWorld;

    /// <summary>按 §3 契约绑定状态源：订阅 OnChanged 并立即同步当前相位；未显式绑定时 Update 也会自动取 Instance。</summary>
    public void Bind(BattleAtmosphere atmo)
    {
        _atmo = atmo;
        if (atmo == null) return;
        if (_subscribed != atmo)
        {
            if (_subscribed != null) _subscribed.OnChanged -= OnAtmosphereChanged;
            _subscribed = atmo;
            atmo.OnChanged += OnAtmosphereChanged;
        }
        OnAtmosphereChanged(atmo.Phase, atmo.Weather);
    }

    /// <summary>半径对外暴露口（§3 契约）：世界单位，由 LightRadiusWorld 唯一驱动；遮罩挖洞与 T4 判定同半径。</summary>
    public void SetRadius(float worldRadius)
    {
        _radiusWorld = Mathf.Max(0.01f, worldRadius);
    }

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
        _atmo = null;
        if (_instance == this) _instance = null;
    }

    void Update()
    {
        EnsureSubscription();
        if (!_built && !Build()) return;
        var cam = Camera.main;
        if (cam == null) return;

        // 半径唯一真源：LightRadiusWorld（世界单位）→ 屏幕高度比例（正交相机纵向可视高 = 2*orthoSize）
        if (_atmo != null) _radiusWorld = Mathf.Max(0.01f, _atmo.LightRadiusWorld);

        // 相位插值（1.5~3s）：alpha/色调缓变，杜绝闪屏
        if (_lerpT < 1f)
        {
            _lerpT = Mathf.Min(1f, _lerpT + Time.deltaTime / PhaseLerpSeconds);
            _curColor = Color.Lerp(_lerpFrom, _targetColor, _lerpT);
        }

        // 光心 = 玩家屏幕 UV（WorldToScreenPoint 的 y 自底部起算，与 Shader uv 左下原点一致，无需翻转）
        var hero = Hero.Instance;
        Vector2 uv = new Vector2(0.5f, 0.5f);
        if (hero != null)
        {
            Vector3 sp = cam.WorldToScreenPoint(hero.transform.position);
            uv = new Vector2(sp.x / Mathf.Max(1, cam.pixelWidth), sp.y / Mathf.Max(1, cam.pixelHeight));
        }

        float ortho = cam.orthographicSize > 0.01f ? cam.orthographicSize : 5f;
        float radiusUv = _radiusWorld / (2f * ortho);   // 半径 = 世界半径 / 屏幕高度对应的世界高度

        var mat = _image != null ? _image.material : null;
        if (mat != null)
        {
            mat.SetVector("_Center", new Vector4(uv.x, uv.y, 0f, 0f));
            mat.SetFloat("_Radius", radiusUv);
            mat.SetFloat("_Aspect", cam.aspect);       // 宽高比补偿：x 方向折算成同一物理长度，屏上是正圆
            mat.SetColor("_DarkColor", _curColor);     // RGB=色调，A=圈外峰值压暗强度
            mat.SetFloat("_Softness", Softness);
        }

        // 白天（目标 alpha=0 且插值完成）：关掉 Image，省一次全屏 pass；切相位时 Update 会重新打开
        bool invisible = _targetColor.a <= 0.001f && _lerpT >= 1f;
        if (_image != null && _image.enabled == invisible) _image.enabled = !invisible;
    }

    /// <summary>自动订阅 OnChanged（与 T2/T4 同款懒装配）；换场景后氛围实例更换时自动改订，绝不重复订阅。</summary>
    void EnsureSubscription()
    {
        var atmo = BattleAtmosphere.Instance;
        if (atmo == null) return;
        _atmo = atmo;
        if (_subscribed == atmo) return;
        if (_subscribed != null) _subscribed.OnChanged -= OnAtmosphereChanged;
        _subscribed = atmo;
        atmo.OnChanged += OnAtmosphereChanged;
        OnAtmosphereChanged(atmo.Phase, atmo.Weather);   // 立即同步当前相位，不等下一次事件
    }

    void OnAtmosphereChanged(DayPhase phase, WeatherType weather)
    {
        Color target;
        if (phase == DayPhase.Day) target = DayTarget;
        else if (phase == DayPhase.Dusk) target = DuskTarget;
        else target = IsHellChapter() ? HellTarget : NightTarget;
        if (_lerpT >= 1f && _targetColor == target) return;   // 同相位重复事件：不重启插值
        _lerpFrom = _curColor;
        _targetColor = target;
        _lerpT = 0f;
    }

    /// <summary>炼狱章（第 7 章）夜相压暗红；读不到 BattleManager 时按普通夜冷蓝。</summary>
    static bool IsHellChapter()
    {
        var bm = BattleManager.InstanceQuiet;
        return bm != null && bm.CurrentChapter == HellChapter;
    }

    /// <summary>建全屏 UI Image（Canvas 排序 UiSort.BattleNightVignette=90，只压场景、不压底部面板）。Shader 缺失只报错不兜底（铁律：露白）。</summary>
    bool Build()
    {
        _built = true;
        if (!_vignetteMatTried)
        {
            _vignetteMatTried = true;
            var sh = Shader.Find(VignetteShaderName);
            if (sh != null) _vignetteMat = new Material(sh);
            else Debug.LogError("[DayNightOverlay] 缺少 Shader " + VignetteShaderName
                + "（Assets/Shaders/BattleNightVignette.shader）—— 夜战压暗不可用，请补 Shader 后重进战斗");
        }
        if (_vignetteMat == null) return false;

        var go = new GameObject("DayNightOverlayImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);
        _image = go.GetComponent<Image>();
        _image.sprite = WhiteSprite;          // Shader 纯程序化计算（不采样贴图），任意满屏白图即可撑出几何
        _image.type = Image.Type.Simple;
        _image.color = Color.white;
        _image.raycastTarget = false;         // 不挡操作
        _image.material = _vignetteMat;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        // 排序 90（UiSort.BattleNightVignette）：压住场景 map(10)/单位(15)/特效(50~82)，停在底部面板(100)之下 → 头像/抽奖/装备栏夜战不被压暗
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.BattleNightVignette, UICanvasSetup.ResolveUiCamera());
        return true;
    }

    /// <summary>满屏白Sprite：优先内置 UISprite，取不到再运行时造 4x4 白贴图（静态缓存，进程级一份）。</summary>
    static Sprite WhiteSprite
    {
        get
        {
            if (_whiteSprite == null)
            {
                _whiteSprite = Resources.GetBuiltinResource<Sprite>("UI/UISprite.psd");
                if (_whiteSprite == null)
                {
                    var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                    var px = new Color[16];
                    for (int i = 0; i < px.Length; i++) px[i] = Color.white;
                    tex.SetPixels(px);
                    tex.Apply();
                    _whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 1f);
                }
            }
            return _whiteSprite;
        }
    }
}
