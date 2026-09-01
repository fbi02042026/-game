using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 酒馆老板娘立绘彩蛋：点击会哆嗦 + 升级警告；点满 10 次踢出约 1 分钟。
/// 挂在「人物」立绘节点上（TavernUI 自动挂）。
/// </summary>
[DisallowMultipleComponent]
public class TavernLandladyTease : MonoBehaviour
{
    public const int KickClicks = 10;
    public const int DimFromClick = 5;
    public const float BanSeconds = 60f;
    const string BanUntilUtcKey = "tavern_landlady_ban_until_utc";

    static readonly string[] WarnLines =
    {
        "嘿！别乱摸！",
        "喝多了就早点休息，别拿我当消遣。",
        "再乱摸？小心被赶出酒馆。",
        "说了别碰——你耳朵进酒了？",
        "……我开始认真了。",
        "再点一下，真的请你出去。",
        "最后警告：手拿开。",
        "你是不是把「欢迎光临」听成「随便摸」了？",
        "行，算你有胆。",
        "滚出去醒醒酒！",
    };

    RectTransform _rt;
    Image _img;
    Button _btn;
    PortraitIdleMotion _idle;
    CanvasGroup _dim;
    Coroutine _shakeCo;
    bool _shakeQueued;
    int _clicks;
    bool _kicking;

    public static bool IsBanned
    {
        get
        {
            long until = ReadBanUntilUtc();
            if (until <= 0) return false;
            return DateTime.UtcNow.Ticks < until;
        }
    }

    public static float BanRemainingSeconds
    {
        get
        {
            long until = ReadBanUntilUtc();
            if (until <= 0) return 0f;
            double sec = (until - DateTime.UtcNow.Ticks) / (double)TimeSpan.TicksPerSecond;
            return Mathf.Max(0f, (float)sec);
        }
    }

    public static string BanToastMessage()
    {
        int sec = Mathf.CeilToInt(BanRemainingSeconds);
        if (sec <= 0) return "酒醒了？行，进来吧——但别再乱摸。";
        return $"你被老板娘赶出酒馆，等酒醒后再来（还剩{sec}秒）";
    }

    public static void ClearBan()
    {
        PlayerPrefs.DeleteKey(BanUntilUtcKey);
        PlayerPrefs.Save();
    }

    static long ReadBanUntilUtc()
    {
        string s = PlayerPrefs.GetString(BanUntilUtcKey, "");
        if (string.IsNullOrEmpty(s)) return 0;
        return long.TryParse(s, out long v) ? v : 0;
    }

    static void WriteBan(float seconds)
    {
        long until = DateTime.UtcNow.AddSeconds(seconds).Ticks;
        PlayerPrefs.SetString(BanUntilUtcKey, until.ToString());
        PlayerPrefs.Save();
    }

    public static TavernLandladyTease EnsureOn(RectTransform portrait)
    {
        if (portrait == null) return null;
        var tease = portrait.GetComponent<TavernLandladyTease>();
        if (tease == null) tease = portrait.gameObject.AddComponent<TavernLandladyTease>();
        tease.Setup();
        return tease;
    }

    void Awake() => Setup();

    void OnEnable()
    {
        // 每次打开酒馆：本轮点击清零，但踢出冷却保留
        _clicks = 0;
        _kicking = false;
        ApplyDim(0f);
    }

    void Setup()
    {
        _rt = transform as RectTransform;
        _img = GetComponent<Image>();
        if (_img != null) _img.raycastTarget = true;

        _btn = GetComponent<Button>();
        if (_btn == null) _btn = gameObject.AddComponent<Button>();
        _btn.transition = Selectable.Transition.None;
        if (_img != null) _btn.targetGraphic = _img;
        _btn.onClick.RemoveListener(OnClicked);
        _btn.onClick.AddListener(OnClicked);

        _idle = GetComponent<PortraitIdleMotion>();
        EnsureDim();
    }

    void EnsureDim()
    {
        if (_dim != null) return;
        var tavern = GetComponentInParent<TavernUI>();
        if (tavern == null) return;
        Transform scene = tavern.transform.Find("TavernScene")
            ?? FindDeep(tavern.transform, "TavernScene");
        if (scene == null) return;

        Transform existing = scene.Find("LandladyTeaseDim");
        GameObject go;
        if (existing != null) go = existing.gameObject;
        else
        {
            go = new GameObject("LandladyTeaseDim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            go.transform.SetParent(scene, false);
            go.transform.SetAsLastSibling();
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = new Color(0.05f, 0.02f, 0.02f, 1f);
            img.raycastTarget = false;
        }
        _dim = go.GetComponent<CanvasGroup>();
        if (_dim == null) _dim = go.AddComponent<CanvasGroup>();
        _dim.blocksRaycasts = false;
        _dim.interactable = false;
        _dim.alpha = 0f;
        go.SetActive(true);
    }

    void OnClicked()
    {
        if (_kicking) return;
        if (IsBanned)
        {
            UIManager.Instance?.ShowToast(BanToastMessage());
            return;
        }

        _clicks++;
        RequestShake();

        int lineIdx = Mathf.Clamp(_clicks - 1, 0, WarnLines.Length - 1);
        UIManager.Instance?.ShowToast(WarnLines[lineIdx]);

        if (_clicks >= DimFromClick)
        {
            float t = Mathf.InverseLerp(DimFromClick, KickClicks, _clicks);
            ApplyDim(0.12f + t * 0.38f);
        }

        if (_clicks >= KickClicks)
            StartCoroutine(CoKickOut());
    }

    IEnumerator CoKickOut()
    {
        _kicking = true;
        WriteBan(BanSeconds);
        ApplyDim(0.55f);
        yield return new WaitForSecondsRealtime(0.35f);
        UIManager.Instance?.ShowToast("滚出去醒醒酒！一分钟后再来。");

        var tavern = GetComponentInParent<TavernUI>();
        tavern?.HidePage();

        var hub = TownHubController.Instance != null
            ? TownHubController.Instance
            : FindObjectOfType<TownHubController>();
        if (hub != null)
            hub.OpenGuild();
        else
            TavernUI.SetGuildHallOverlayMode(false);

        _clicks = 0;
        _kicking = false;
        ApplyDim(0f);
    }

    void RequestShake()
    {
        if (_shakeCo != null)
        {
            _shakeQueued = true;
            return;
        }
        _shakeCo = StartCoroutine(CoShake());
    }

    IEnumerator CoShake()
    {
        do
        {
            _shakeQueued = false;
            if (_idle != null) _idle.enabled = false;

            Vector2 basePos = _rt != null ? _rt.anchoredPosition : Vector2.zero;
            Vector3 baseEuler = transform.localEulerAngles;
            float dodgeAmp = 6f;
            float dodgeDur = 0.12f;
            int dodges = 2;
            for (int d = 0; d < dodges; d++)
            {
                float side = (d % 2 == 0) ? -1f : 1f;
                float t = 0f;
                while (t < dodgeDur)
                {
                    t += Time.unscaledDeltaTime;
                    float u = t / dodgeDur;
                    float ease = Mathf.Sin(u * Mathf.PI);
                    if (_rt != null)
                        _rt.anchoredPosition = basePos + new Vector2(side * dodgeAmp * ease, 0f);
                    yield return null;
                }
                if (_rt != null) _rt.anchoredPosition = basePos;
                if (d < dodges - 1)
                    yield return new WaitForSecondsRealtime(0.08f);
            }

            yield return new WaitForSecondsRealtime(0.1f);
            if (_rt != null) _rt.anchoredPosition = basePos;
            transform.localEulerAngles = baseEuler;
            if (_idle != null) _idle.enabled = true;
        }
        while (_shakeQueued);

        _shakeCo = null;
    }

    void ApplyDim(float alpha)
    {
        EnsureDim();
        if (_dim == null) return;
        _dim.alpha = Mathf.Clamp01(alpha);
    }

    /// <summary>解禁后第一次进酒馆：微醺晃一下。</summary>
    public IEnumerator CoWelcomeBackDrunk()
    {
        EnsureDim();
        if (_dim != null) _dim.alpha = 0.2f;
        RequestShake();
        UIManager.Instance?.ShowToast("酒醒了？行，进来吧——但别再乱摸。");
        float t = 0f;
        while (t < 0.6f)
        {
            t += Time.unscaledDeltaTime;
            if (_dim != null)
                _dim.alpha = Mathf.Lerp(0.2f, 0f, t / 0.6f);
            yield return null;
        }
        ApplyDim(0f);
    }

    static Transform FindDeep(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var r = FindDeep(parent.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }
}
