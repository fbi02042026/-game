using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 登录页 LOGO：入场、轻微浮动、呼吸缩放、扫光，以及少量像素星点。
/// 运行时挂到 Logo 上，不改登录预制体。
/// </summary>
[DisallowMultipleComponent]
public class LoginLogoMotion : MonoBehaviour
{
    const int SparkleCount = 6;

    RectTransform _rt;
    Image _img;
    CanvasGroup _group;
    Material _shine;
    Vector2 _restPos;
    Vector3 _restScale;
    float _t;
    bool _hasShine;
    Sparkle[] _sparkles;

    struct Sparkle
    {
        public RectTransform rt;
        public Image img;
        public float life;
        public float duration;
        public Vector2 from;
        public Vector2 to;
        public float size;
    }

    void Awake()
    {
        _rt = transform as RectTransform;
        _img = GetComponent<Image>();
        if (_rt == null) return;

        _restPos = _rt.anchoredPosition;
        _restScale = _rt.localScale.sqrMagnitude < 0.0001f ? Vector3.one : _rt.localScale;

        _group = GetComponent<CanvasGroup>();
        if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        _group.blocksRaycasts = false;
        _group.interactable = false;

        SetupShine();
        BuildSparkles();
    }

    void OnEnable()
    {
        _t = 0f;
        if (_rt == null) return;
        _rt.anchoredPosition = _restPos + new Vector2(0f, 18f);
        _rt.localScale = _restScale * 0.88f;
        if (_group != null) _group.alpha = 0f;
        ResetSparkles(true);
    }

    void OnDisable()
    {
        if (_rt != null)
        {
            _rt.anchoredPosition = _restPos;
            _rt.localScale = _restScale;
        }
        if (_group != null) _group.alpha = 1f;
    }

    void OnDestroy()
    {
        if (_shine != null)
            Destroy(_shine);
    }

    void Update()
    {
        if (_rt == null) return;
        float dt = Time.unscaledDeltaTime;
        _t += dt;

        float enter = Mathf.Clamp01(_t / 0.55f);
        float e = 1f - Mathf.Pow(1f - enter, 3f);

        float bob = Mathf.Sin((_t - 0.2f) * 1.35f) * 7f;
        float y = Mathf.Lerp(18f, 0f, e) + bob;
        _rt.anchoredPosition = _restPos + new Vector2(0f, Mathf.Round(y));

        float breath = 1f + Mathf.Sin(_t * 1.1f) * 0.018f;
        _rt.localScale = _restScale * Mathf.Lerp(0.88f, breath, e);

        if (_group != null)
            _group.alpha = e;

        if (_hasShine && _shine != null)
            TickShine();

        TickSparkles(dt, e);
    }

    const float ShineSweepSeconds = 1.2f;
    const float ShineWaitSeconds = 2.8f;
    const float ShineFirstDelay = 0.5f;
    // 只走正弦的一个峰：从画面外进入到另一侧出去
    const float ShineTimeEnter = 0.16f;
    const float ShineTimeExit = -0.22f;

    void TickShine()
    {
        float clock = _t - ShineFirstDelay;
        if (clock < 0f)
        {
            _shine.SetFloat("_ShineFade", 0f);
            return;
        }

        float cycle = ShineSweepSeconds + ShineWaitSeconds;
        float u = Mathf.Repeat(clock, cycle);
        if (u <= ShineSweepSeconds)
        {
            float k = u / ShineSweepSeconds;
            _shine.SetFloat("_ShineFade", 1f);
            _shine.SetFloat("_TimeValue", Mathf.Lerp(ShineTimeEnter, ShineTimeExit, k));
        }
        else
        {
            _shine.SetFloat("_ShineFade", 0f);
        }
    }

    void SetupShine()
    {
        if (_img == null || _img.material == null) return;
        if (!_img.material.HasProperty("_ShineSpeed")) return;

        _shine = Instantiate(_img.material);
        _img.material = _shine;
        _hasShine = true;

        _shine.SetFloat("_ShineSpeed", 1f);
        _shine.SetFloat("_ShineWidth", 0.1f);
        _shine.SetFloat("_ShineScale", 0.2f);
        _shine.SetFloat("_ShineSmoothness", 2.2f);
        _shine.SetFloat("_ShineRotation", 32f);
        _shine.SetFloat("_ShineContrast", 1.4f);
        _shine.SetFloat("_ShineSaturation", 0.4f);
        _shine.SetFloat("_ShineFade", 0f);
        _shine.SetColor("_ShineColor", new Color(3.2f, 2.9f, 1.7f, 1f));
        _shine.SetFloat("_TimeScale", 0f);

        _shine.shaderKeywords = new[] { "_SHADERSPACE_UV_RAW", "_TIMESETTINGS_CUSTOM_VALUE" };
        _shine.EnableKeyword("_TIMESETTINGS_CUSTOM_VALUE");
        _shine.EnableKeyword("_SHADERSPACE_UV_RAW");
        _shine.SetFloat("_TimeSettings", 5f);
        _shine.SetFloat("_TimeValue", ShineTimeEnter);
        _shine.SetFloat("_ShaderSpace", 1f);
    }

    void BuildSparkles()
    {
        _sparkles = new Sparkle[SparkleCount];
        for (int i = 0; i < SparkleCount; i++)
        {
            var go = new GameObject("Sparkle_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            var img = go.GetComponent<Image>();
            img.sprite = WhitePixel();
            img.raycastTarget = false;
            img.color = new Color(1f, 0.95f, 0.72f, 0f);
            _sparkles[i].rt = rt;
            _sparkles[i].img = img;
        }
    }

    void ResetSparkles(bool stagger)
    {
        if (_sparkles == null) return;
        for (int i = 0; i < _sparkles.Length; i++)
            Respawn(i, stagger ? Random.Range(0f, 1.2f) : 0f);
    }

    void TickSparkles(float dt, float enter)
    {
        if (_sparkles == null) return;
        for (int i = 0; i < _sparkles.Length; i++)
        {
            var s = _sparkles[i];
            if (s.rt == null) continue;
            s.life += dt;
            if (s.life >= s.duration)
            {
                Respawn(i, 0f);
                continue;
            }

            float u = Mathf.Clamp01(s.life / s.duration);
            float fade = u < 0.2f ? u / 0.2f : 1f - (u - 0.2f) / 0.8f;
            s.rt.anchoredPosition = Vector2.Lerp(s.from, s.to, u);
            float sz = Mathf.Round(s.size * (1.1f - 0.4f * u));
            s.rt.sizeDelta = new Vector2(sz, sz);
            if (s.img != null)
                s.img.color = new Color(1f, 0.94f, 0.7f, fade * 0.9f * enter);
            _sparkles[i] = s;
        }
    }

    void Respawn(int i, float delay)
    {
        var s = _sparkles[i];
        float w = _rt != null ? _rt.rect.width : 412f;
        float h = _rt != null ? _rt.rect.height : 360f;
        s.from = new Vector2(Random.Range(-w * 0.28f, w * 0.28f), Random.Range(-h * 0.55f, h * 0.05f));
        s.to = s.from + new Vector2(Random.Range(-12f, 12f), Random.Range(28f, 56f));
        s.duration = Random.Range(1.1f, 1.9f);
        s.life = -delay;
        s.size = Random.value > 0.55f ? 4f : 3f;
        if (s.img != null) s.img.color = new Color(1f, 0.95f, 0.72f, 0f);
        if (s.rt != null)
        {
            s.rt.anchoredPosition = s.from;
            s.rt.sizeDelta = new Vector2(s.size, s.size);
        }
        _sparkles[i] = s;
    }

    static Sprite _white;
    static Sprite WhitePixel()
    {
        if (_white != null) return _white;
        var tex = Texture2D.whiteTexture;
        _white = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 4f);
        _white.name = "LoginLogoSparkle";
        return _white;
    }
}
