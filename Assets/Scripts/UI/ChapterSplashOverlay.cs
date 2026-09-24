using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 开场黑屏+章节标题：完整显示 Hold 秒后，Fade 秒渐隐并自毁。
/// </summary>
public class ChapterSplashOverlay : MonoBehaviour
{
    public bool IsFinished { get; private set; }

    public const float HoldSeconds = 2.5f;
    public const float FadeSeconds = 1.5f;
    /// <summary>新手教学关：标题多留一会儿让玩家读完。</summary>
    public const float TutorialHoldSeconds = 2.88f;
    public const float TutorialFadeSeconds = 1.28f;

    CanvasGroup _group;
    bool _isTutorial;
    bool _waitLoadingBeforeHold;

    public static ChapterSplashOverlay Show(string title, string body = null, bool isTutorial = false,
        bool waitLoadingBeforeHold = false)
    {
        var leftovers = Object.FindObjectsOfType<ChapterSplashOverlay>();
        for (int i = 0; i < leftovers.Length; i++)
        {
            if (leftovers[i] != null)
                Object.Destroy(leftovers[i].gameObject);
        }

        GameObject root = new GameObject("ChapterSplash");
        DontDestroyOnLoad(root);
        var driver = root.AddComponent<ChapterSplashOverlay>();
        driver._isTutorial = isTutorial;
        driver._waitLoadingBeforeHold = waitLoadingBeforeHold;
        driver.Build(title, body);
        driver.StartCoroutine(driver.RunRoutine());
        return driver;
    }

    /// <summary>战斗开场章节卡：与 BattleManager 原逻辑同款文案，供进战斗前（Loading 期间）调用。</summary>
    public static ChapterSplashOverlay ShowBattleChapter(int chapter, bool isTutorial)
    {
        string title = ChapterStoryBeats.IntroTitle(chapter) ?? GameConfig.GetChapterMapName(chapter);
        string body = isTutorial
            ? "阳光还能照进来，怪物也不算太强。\n正好适合一个新人进去摸摸路。"
            : ChapterStoryBeats.OpeningLine(chapter);
        if (isTutorial) title = "森林区域，第一层";
        // 不等 Loading：卡片自己就是进战斗前的过场，演完再加载战斗场景
        return Show(title, body, isTutorial, waitLoadingBeforeHold: false);
    }

    void Build(string title, string body)
    {
        var canvas = gameObject.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.FullscreenFx);
        var scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler != null)
            scaler.matchWidthOrHeight = 0f;

        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 1f;
        _group.blocksRaycasts = true;
        _group.interactable = false;

        var bgGo = new GameObject("Black");
        bgGo.transform.SetParent(transform, false);
        var bg = bgGo.AddComponent<Image>();
        bg.sprite = CreateSolidSprite();
        // 新铁律（2026-09-19）：全屏遮罩最多半透，不许完全遮住背后 UI。
        // 0.72：衬得住白字，又看得见战斗 UI 轮廓。
        bg.color = new Color(0f, 0f, 0f, 0.72f);
        bg.raycastTarget = true;
        var bgRt = bg.rectTransform;
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        var textGo = new GameObject("Title");
        textGo.transform.SetParent(transform, false);
        var text = textGo.AddComponent<Text>();
        text.text = title ?? "";
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.fontSize = 72;
        text.fontStyle = FontStyle.Bold;
        text.color = new Color(1f, 1f, 1f, 1f); // 纯白不透明
        text.raycastTarget = false;
        text.font = GameFonts.GetChinese(); // 章节中文：fusion-pixel

        var outline = textGo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 1f);
        outline.effectDistance = new Vector2(4f, -4f);

        var trt = text.rectTransform;
        trt.anchorMin = new Vector2(0.05f, string.IsNullOrEmpty(body) ? 0.35f : 0.48f);
        trt.anchorMax = new Vector2(0.95f, string.IsNullOrEmpty(body) ? 0.65f : 0.72f);
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        if (!string.IsNullOrEmpty(body))
        {
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(transform, false);
            var bodyText = bodyGo.AddComponent<Text>();
            bodyText.text = body;
            bodyText.alignment = TextAnchor.MiddleCenter;
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            bodyText.fontSize = 28;
            bodyText.color = new Color(1f, 1f, 1f, 0.92f);
            bodyText.raycastTarget = false;
            bodyText.font = GameFonts.GetChinese();
            var brt = bodyText.rectTransform;
            brt.anchorMin = new Vector2(0.1f, 0.22f);
            brt.anchorMax = new Vector2(0.9f, 0.46f);
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
        }

        // 开章引言：每章一句氛围钩子（ChapterStoryBeats.ChapterQuote）。
        // 落在标题/正文下方的留白处，不改动既有标题/正文的位置字号颜色。
        // 章节号取自 ChapterManager，与战斗内其他叙事取值一致，不写死。
        int qChapter = ChapterManager.Instance != null ? ChapterManager.Instance.currentChapter : 1;
        string quote = ChapterStoryBeats.ChapterQuote(qChapter);
        if (!string.IsNullOrEmpty(quote))
        {
            var quoteGo = new GameObject("Quote");
            quoteGo.transform.SetParent(transform, false);
            var quoteText = quoteGo.AddComponent<Text>();
            quoteText.text = quote;
            quoteText.alignment = TextAnchor.MiddleCenter;
            quoteText.horizontalOverflow = HorizontalWrapMode.Wrap;
            quoteText.verticalOverflow = VerticalWrapMode.Overflow;
            quoteText.fontSize = 26;
            quoteText.fontStyle = FontStyle.Italic;
            quoteText.color = new Color(1f, 1f, 1f, 0.85f); // 纯白，略低于正文以区分层次
            quoteText.raycastTarget = false;
            quoteText.font = GameFonts.GetChinese(); // 与标题/正文同款中文字体
            var qrt = quoteText.rectTransform;
            // 正文底沿在 0.22：引言放在其下方 0.04–0.18 的留白带，避免与标题/正文重叠。
            qrt.anchorMin = new Vector2(0.1f, 0.04f);
            qrt.anchorMax = new Vector2(0.9f, 0.18f);
            qrt.offsetMin = Vector2.zero;
            qrt.offsetMax = Vector2.zero;
        }

        IsFinished = false;
    }

    IEnumerator RunRoutine()
    {
        _group.alpha = 1f;

        if (_waitLoadingBeforeHold)
        {
            const float maxWait = 15f;
            float guard = 0f;
            while ((SceneLoadingCoordinator.IsActive || BattleLoadingOverlay.IsShowing) && guard < maxWait)
            {
                guard += Time.unscaledDeltaTime > 0.0001f ? Time.unscaledDeltaTime : 0.016f;
                yield return null;
            }
            yield return null;
        }

        float holdSec = _isTutorial ? TutorialHoldSeconds : HoldSeconds;
        float fadeSec = _isTutorial ? TutorialFadeSeconds : FadeSeconds;
        float hold = 0f;
        while (hold < holdSec)
        {
            hold += Time.unscaledDeltaTime;
            if (Clicked()) break;
            yield return null;
        }

        float t = 0f;
        while (t < fadeSec)
        {
            t += Time.unscaledDeltaTime;
            if (t < 0.0001f) t += 0.016f;
            _group.alpha = 1f - Mathf.Clamp01(t / fadeSec);
            if (Clicked() && t > 0.2f) break;
            yield return null;
        }
        _group.alpha = 0f;
        IsFinished = true;
        Destroy(gameObject);
    }

    static bool Clicked()
    {
        if (Input.GetMouseButtonDown(0)) return true;
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) return true;
        return false;
    }

    static Sprite CreateSolidSprite()
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
    }

    void OnDestroy()
    {
        IsFinished = true;
    }
}
