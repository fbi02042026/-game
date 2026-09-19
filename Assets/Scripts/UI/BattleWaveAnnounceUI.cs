using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗中央波次预告图：下一波来袭 / Boss来袭（Resources/UI/Battle/）。
/// 动效见 <see cref="UiBannerPopAnim.CoPlayWaveIncoming"/>（大幅砸入 / 落地颤 / 渐隐）。
/// </summary>
public class BattleWaveAnnounceUI : MonoBehaviour
{
    public enum Kind { NextWave, Boss }

    public static BattleWaveAnnounceUI Instance { get; private set; }

    const string PathNext = "UI/Battle/wave_next_incoming";
    const string PathBoss = "UI/Battle/wave_boss_incoming";

    public static float SlamDuration => UiBannerPopAnim.WaveSlamDuration;
    public static float HoldDuration => UiBannerPopAnim.WaveHoldDuration;
    public static float FadeOutDuration => UiBannerPopAnim.WaveFadeOutDuration;

    static Sprite _sprNext;
    static Sprite _sprBoss;

    /// <summary>换图后清缓存，避免运行中仍用旧 Sprite。</summary>
    public static void InvalidateSpriteCache()
    {
        _sprNext = null;
        _sprBoss = null;
    }

    public static float GetPlayDuration(Kind kind) => UiBannerPopAnim.WaveIncomingTotalDuration;

    CanvasGroup _group;
    Image _image;
    Coroutine _playCo;

    // —— 波次原型播报（2026-09-18 用户要求「波次每波变一下」）——
    // 预告图只写死「下一波来袭」，玩家看不出这波是箭雨还是夹击；
    // 这里在预告图下方补一行原型名 + 播报 + 应对提示（文案来自 wave_archetype.csv）。
    // 底图优先用项目素材（UI/AdventureLog/Frames/字底），找不到就只显示描边文字。
    Image _subBg;
    Text _subText;

    public static BattleWaveAnnounceUI Ensure()
    {
        if (Instance != null) return Instance;

        var go = new GameObject("BattleWaveAnnounceUI", typeof(RectTransform));
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<BattleWaveAnnounceUI>();
        Instance.Build();
        return Instance;
    }

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.FullscreenFx);

        var scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler != null)
            scaler.matchWidthOrHeight = 0.5f;

        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        var imgGo = new GameObject("Banner", typeof(RectTransform), typeof(Image));
        imgGo.transform.SetParent(transform, false);
        var rt = imgGo.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.59f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;

        _image = imgGo.GetComponent<Image>();
        _image.raycastTarget = false;
        _image.preserveAspect = true;
        _image.color = Color.white;
        BuildSubtitle();
        gameObject.SetActive(true);
        _group.alpha = 0f;
    }

    /// <summary>预告图下方的原型播报条：字底 + 文字（程序生成，底图走项目素材）。</summary>
    void BuildSubtitle()
    {
        var bgGo = new GameObject("SubtitleBg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bgGo.transform.SetParent(transform, false);
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = bgRt.anchorMax = new Vector2(0.5f, 0.36f);
        bgRt.pivot = new Vector2(0.5f, 0.5f);
        bgRt.anchoredPosition = Vector2.zero;
        bgRt.sizeDelta = new Vector2(680f, 96f);
        _subBg = bgGo.GetComponent<Image>();
        _subBg.raycastTarget = false;
        _subBg.preserveAspect = false;
        // 项目素材：日志「字底」。拿不到就退回一层半透明黑底，不至于糊在场景上看不清
        if (!UiKeyedBackgrounds.ApplyLogFrame(_subBg, "字底", preserveAspect: false))
            _subBg.color = new Color(0.06f, 0.05f, 0.08f, 0.62f);
        _subBg.gameObject.SetActive(false);

        var tGo = new GameObject("SubtitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        tGo.transform.SetParent(bgGo.transform, false);
        var tRt = tGo.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = new Vector2(24f, 8f);
        tRt.offsetMax = new Vector2(-24f, -8f);
        _subText = tGo.GetComponent<Text>();
        _subText.raycastTarget = false;
        _subText.alignment = TextAnchor.MiddleCenter;
        _subText.fontSize = 30;
        _subText.color = new Color(1f, 0.94f, 0.78f, 1f);
        _subText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _subText.verticalOverflow = VerticalWrapMode.Overflow;
        var f = GameFonts.GetChinese();
        if (f != null) _subText.font = f;
        var ol = tGo.AddComponent<Outline>();
        ol.effectColor = new Color(0f, 0f, 0f, 0.9f);
        ol.effectDistance = new Vector2(1.5f, -1.5f);
    }

    /// <summary>设置本波播报文案；空 = 不显示播报条（只留预告图）。</summary>
    void SetSubtitle(string text)
    {
        bool show = !string.IsNullOrEmpty(text);
        if (_subBg != null) _subBg.gameObject.SetActive(show);
        if (_subText != null && show) _subText.text = text;
    }

    public static void Play(Kind kind, string subtitle = null) => Ensure().PlayInternal(kind, subtitle);

    /// <param name="subtitle">本波原型播报（如「第2波 · 箭雨：远程压制 — 近战顶住，远程先点」）；空 = 只放预告图。</param>
    public static IEnumerator CoPlay(Kind kind, string subtitle = null)
    {
        var ui = Ensure();
        if (ui._playCo != null)
        {
            yield return ui._playCo;
            yield break;
        }
        yield return ui.CoPlayInternal(kind, subtitle);
    }

    void PlayInternal(Kind kind, string subtitle = null)
    {
        if (_playCo != null) return;
        _playCo = StartCoroutine(CoPlayInternal(kind, subtitle));
    }

    public void CancelAndHide()
    {
        if (_playCo != null)
        {
            StopCoroutine(_playCo);
            _playCo = null;
        }
        if (_group != null) _group.alpha = 0f;
        if (_image != null) _image.enabled = false;
        SetSubtitle(null);
    }

    public static void Cancel()
    {
        if (Instance != null)
            Instance.CancelAndHide();
    }

    IEnumerator CoPlayInternal(Kind kind, string subtitle = null)
    {
        var canvas = GetComponent<Canvas>();
        if (canvas != null)
            UICanvasSetup.RefreshPopup(canvas, GameConfig.UiSort.FullscreenFx);

        // 允许运行前替换 Resources 图后立刻生效
        InvalidateSpriteCache();
        Sprite sp = LoadSprite(kind);
        if (sp == null || _image == null || _group == null)
        {
            _playCo = null;
            yield break;
        }

        _image.sprite = sp;
        _image.enabled = true;
        SetSubtitle(subtitle);
        yield return UiBannerPopAnim.CoPlayWaveIncoming(_image, _group);

        _image.enabled = false;
        SetSubtitle(null);
        _playCo = null;
    }

    static Sprite LoadSprite(Kind kind)
    {
        string path = kind == Kind.Boss ? PathBoss : PathNext;
        if (kind == Kind.Boss)
        {
            if (_sprBoss != null) return _sprBoss;
            _sprBoss = Resources.Load<Sprite>(path);
            if (_sprBoss == null) _sprBoss = SpriteFromTexture(path);
            return _sprBoss;
        }
        if (_sprNext != null) return _sprNext;
        _sprNext = Resources.Load<Sprite>(path);
        if (_sprNext == null) _sprNext = SpriteFromTexture(path);
        return _sprNext;
    }

    static Sprite SpriteFromTexture(string resourcePath)
    {
        var tex = Resources.Load<Texture2D>(resourcePath);
        if (tex == null) return null;
        return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
