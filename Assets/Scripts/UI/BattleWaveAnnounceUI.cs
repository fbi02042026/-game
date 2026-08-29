using System.Collections;

using UnityEngine;

using UnityEngine.UI;



/// <summary>

/// 战斗中央波次预告图：下一波来袭 / Boss来袭（Resources/UI/Battle/）。

/// </summary>

public class BattleWaveAnnounceUI : MonoBehaviour

{

    public enum Kind { NextWave, Boss }



    public static BattleWaveAnnounceUI Instance { get; private set; }



    const string PathNext = "UI/Battle/wave_next_incoming";

    const string PathBoss = "UI/Battle/wave_boss_incoming";



    static Sprite _sprNext;

    static Sprite _sprBoss;



    CanvasGroup _group;

    Image _image;

    RectTransform _rt;

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

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        canvas.sortingOrder = 32750;



        var scaler = gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        scaler.referenceResolution = new Vector2(720, 1280);

        scaler.matchWidthOrHeight = 0.5f;



        _group = gameObject.AddComponent<CanvasGroup>();

        _group.alpha = 0f;

        _group.blocksRaycasts = false;

        _group.interactable = false;



        var imgGo = new GameObject("Banner", typeof(RectTransform), typeof(Image));

        imgGo.transform.SetParent(transform, false);

        _rt = imgGo.GetComponent<RectTransform>();

        _rt.anchorMin = _rt.anchorMax = new Vector2(0.5f, 0.55f);

        _rt.pivot = new Vector2(0.5f, 0.5f);

        _rt.anchoredPosition = Vector2.zero;



        _image = imgGo.GetComponent<Image>();

        _image.raycastTarget = false;

        _image.preserveAspect = true;

        _image.color = Color.white;
        gameObject.SetActive(true);

        _group.alpha = 0f;

    }



    public static void Play(Kind kind)

    {

        Ensure().PlayInternal(kind);

    }



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

        _image.preserveAspect = true;

        _image.SetNativeSize();



        const float slamDuration = 0.24f;
        const float endScale = 2f;
        const float startScale = endScale * 2.65f;

        _rt.localScale = Vector3.one * startScale;
        _group.alpha = 0.92f;

        float t = 0f;
        while (t < slamDuration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / slamDuration);
            float easeIn = u * u * u;
            float s = Mathf.Lerp(startScale, endScale, easeIn);
            _rt.localScale = Vector3.one * s;
            _group.alpha = Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(u * 2.5f));
            yield return null;
        }

        _rt.localScale = Vector3.one * endScale;

        _group.alpha = 1f;

        _group.alpha = 0f;

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


