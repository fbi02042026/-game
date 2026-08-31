using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗中央波次预告图：下一波来袭 / Boss来袭（Resources/UI/Battle/）。
/// 动效见 <see cref="UiBannerPopAnim"/>（300%→100% / 0.3s / 停1s / 淡出）。
/// </summary>
public class BattleWaveAnnounceUI : MonoBehaviour
{
    public enum Kind { NextWave, Boss }

    public static BattleWaveAnnounceUI Instance { get; private set; }

    const string PathNext = "UI/Battle/wave_next_incoming";
    const string PathBoss = "UI/Battle/wave_boss_incoming";

    public static float SlamDuration => UiBannerPopAnim.ShrinkDuration;
    public static float HoldDuration => UiBannerPopAnim.HoldDuration;
    public static float FadeOutDuration => UiBannerPopAnim.FadeOutDuration;

    static Sprite _sprNext;
    static Sprite _sprBoss;

    public static float GetPlayDuration(Kind kind) => UiBannerPopAnim.TotalDuration;

    CanvasGroup _group;
    Image _image;
    Coroutine _playCo;

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
        gameObject.SetActive(true);
        _group.alpha = 0f;
    }

    public static void Play(Kind kind) => Ensure().PlayInternal(kind);

    public static IEnumerator CoPlay(Kind kind)
    {
        var ui = Ensure();
        if (ui._playCo != null)
        {
            yield return ui._playCo;
            yield break;
        }
        yield return ui.CoPlayInternal(kind);
    }

    void PlayInternal(Kind kind)
    {
        if (_playCo != null) return;
        _playCo = StartCoroutine(CoPlayInternal(kind));
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
    }

    public static void Cancel()
    {
        if (Instance != null)
            Instance.CancelAndHide();
    }

    IEnumerator CoPlayInternal(Kind kind)
    {
        Sprite sp = LoadSprite(kind);
        if (sp == null || _image == null || _group == null)
        {
            _playCo = null;
            yield break;
        }

        _image.sprite = sp;
        _image.enabled = true;
        yield return UiBannerPopAnim.CoPlay(_image, _group);

        _image.enabled = false;
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
