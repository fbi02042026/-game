using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「再来一次！」横幅 —— <b>2026-10-06 主人拍板</b>：
/// 播放顺序 = <b>从屏幕左侧滑入 → 匀速慢慢滑行 1 秒 → 快速向右侧滑走</b>。
///
/// <para>资源：<c>Resources/Art/UI/SlotMachine/again_banner</c>（主人给的「再来一次」大字图）。
/// 取不到就 <c>LogError</c> 并直接不播，绝不静默当成功（规则 R2）。</para>
///
/// <para>⚠ 动画全程走 <b>unscaled</b> 时间：抽奖阶段战斗是冻住的（<c>Time.timeScale</c> 可能为 0），
/// 用普通 <c>deltaTime</c> 横幅会定在屏外不动。</para>
///
/// <para>⚠ 自建独立 Overlay Canvas（排序 32000），不往 BattleUI 里塞节点、不改任何预制体。</para>
/// </summary>
public class SlotMachineAgainBanner : MonoBehaviour
{
    public const string SpritePath = "Art/UI/SlotMachine/again_banner";

    /// <summary>压在所有战斗 UI 之上（BattleStageMap 那一档远低于此）。</summary>
    const int SortOrder = 32000;

    const float SlideInSec = 0.30f;    // 左侧屏外 → 居中
    const float CruiseSec = 1.00f;     // 居中后慢慢滑行 1 秒（主人指定）
    const float SlideOutSec = 0.22f;   // 快速向右滑走
    /// <summary>缓慢滑行阶段走过的屏宽比例。</summary>
    const float CruiseScreenRatio = 0.12f;
    /// <summary>横幅显示宽度 = 屏宽 × 该比例。</summary>
    const float DisplayWidthRatio = 0.62f;

    static SlotMachineAgainBanner _instance;
    static bool _warnedMissingSprite;

    RectTransform _rt;
    Image _img;
    Coroutine _co;
    float _canvasWidth = GameConfig.DESIGN_WIDTH;
    float _shownWidth = 512f;

    /// <summary>播一次「再来一次」横幅（重复调用会重头播）。</summary>
    public static void Show()
    {
        var ui = Ensure();
        if (ui == null) return;
        ui.Play();
    }

    static SlotMachineAgainBanner Ensure()
    {
        if (_instance != null) return _instance;

        var go = new GameObject("SlotMachineAgainBanner", typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortOrder;
        go.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(go);

        _instance = go.AddComponent<SlotMachineAgainBanner>();
        return _instance;
    }

    void Awake()
    {
        _instance = this;
        Build();
        if (_rt != null) _rt.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    void Build()
    {
        if (_img != null) return;

        var canvasRt = transform as RectTransform;
        if (canvasRt != null && canvasRt.rect.width > 1f) _canvasWidth = canvasRt.rect.width;

        var go = new GameObject("Banner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);

        _rt = go.GetComponent<RectTransform>();
        _rt.anchorMin = new Vector2(0.5f, 0.5f);
        _rt.anchorMax = new Vector2(0.5f, 0.5f);
        _rt.pivot = new Vector2(0.5f, 0.5f);
        _rt.anchoredPosition = Vector2.zero;

        _img = go.GetComponent<Image>();
        _img.raycastTarget = false;
        _img.preserveAspect = true;
        _img.sprite = LoadSprite();

        float spW = _img.sprite != null ? Mathf.Max(1f, _img.sprite.rect.width) : 512f;
        float spH = _img.sprite != null ? Mathf.Max(1f, _img.sprite.rect.height) : 256f;
        _shownWidth = _canvasWidth * DisplayWidthRatio;
        _rt.sizeDelta = new Vector2(_shownWidth, _shownWidth * (spH / spW));
    }

    void Play()
    {
        Build();
        if (_rt == null) return;
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoPlay());
    }

    IEnumerator CoPlay()
    {
        // 每一轮都以「当前真实屏宽」重算，避免切分辨率后算错
        var canvasRt = transform as RectTransform;
        if (canvasRt != null && canvasRt.rect.width > 1f)
        {
            _canvasWidth = canvasRt.rect.width;
            float spW = _img != null && _img.sprite != null ? Mathf.Max(1f, _img.sprite.rect.width) : 512f;
            float spH = _img != null && _img.sprite != null ? Mathf.Max(1f, _img.sprite.rect.height) : 256f;
            _shownWidth = _canvasWidth * DisplayWidthRatio;
            _rt.sizeDelta = new Vector2(_shownWidth, _shownWidth * (spH / spW));
        }

        float half = _canvasWidth * 0.5f;
        float offscreen = half + _shownWidth;      // 整块横幅完全在屏外
        float cruiseTo = _canvasWidth * CruiseScreenRatio;

        _rt.gameObject.SetActive(true);

        // ① 左侧屏外 → 屏幕中间（起步快、末端收住）
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / SlideInSec;
            float e = 1f - (1f - Mathf.Clamp01(t)) * (1f - Mathf.Clamp01(t));
            _rt.anchoredPosition = new Vector2(Mathf.Lerp(-offscreen, 0f, e), 0f);
            yield return null;
        }

        // ② 居中后**慢慢**向右滑行 1 秒（匀速，主人指定）
        t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / CruiseSec;
            _rt.anchoredPosition = new Vector2(Mathf.Lerp(0f, cruiseTo, Mathf.Clamp01(t)), 0f);
            yield return null;
        }

        // ③ **快速**向右滑走（越走越快）
        t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / SlideOutSec;
            float e = Mathf.Clamp01(t) * Mathf.Clamp01(t);
            _rt.anchoredPosition = new Vector2(Mathf.Lerp(cruiseTo, offscreen, e), 0f);
            yield return null;
        }

        _rt.gameObject.SetActive(false);
        _co = null;
    }

    static Sprite LoadSprite()
    {
        var sp = Resources.Load<Sprite>(SpritePath);
        if (sp != null) return sp;

        // 兜底：png 被按「默认贴图」导入时 Resources.Load<Sprite> 取不到，读 Texture2D 现造
        var tex = Resources.Load<Texture2D>(SpritePath);
        if (tex != null)
            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);

        if (!_warnedMissingSprite)
        {
            _warnedMissingSprite = true;
            Debug.LogError("[SlotMachineAgainBanner] 取不到横幅图 " + SpritePath +
                           " —— 请确认 Assets/Resources/Art/UI/SlotMachine/again_banner.png 存在且已导入。");
        }
        return null;
    }
}
