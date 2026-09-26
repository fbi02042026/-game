using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

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

    /// <summary>
    /// 章节文字整体上抬幅度：占逻辑高度的比例（2026-09-26 主人要求「章节开头文字往上移一点」）。
    /// 0.05 = 5%，落在主人给的 4%~6% 区间内；要再高/再低只改这一个数。
    /// 作用于 Title / Body / Quote 三段的共同父容器 Content —— 整体位移一次，
    /// 不动各段的字号、颜色、锚点，也不动淡入淡出时长与不透明底那套逻辑。
    /// </summary>
    public const float ContentLiftY = 0.05f;

    /// <summary>
    /// 章节文字逐字显现（打字机）总开关（2026-09-26 主人要求「一个一个出现」）。
    /// 改 false = 三段文字整段直接出现，回到原来的行为。
    /// 只影响显现方式：不动文案、颜色、字号、位置（含 ContentLiftY）、淡入淡出时长与不透明底。
    /// </summary>
    public const bool CharRevealEnabled = true;
    /// <summary>
    /// 每字间隔（秒）：标题+描述+引用合计约 60 字 ≈ 2.7s，刚好在 Hold 2.5s 前后铺完，不额外拖长卡片。
    /// 中文按 char 计，一个汉字算一个字。
    /// </summary>
    public const float CharRevealInterval = 0.045f;
    /// <summary>标题 → 描述 → 引用 之间的停顿（秒）：三段依次显现，显得有节奏。</summary>
    public const float GroupRevealGap = 0.16f;

    /// <summary>一段待逐字显现的文字：Text 引用 + 缓存的目标串（避免逐帧重新取 text）。</summary>
    struct RevealItem
    {
        public Text text;
        public string target;
    }

    CanvasGroup _group;
    bool _isTutorial;
    bool _waitLoadingBeforeHold;
    /// <summary>Loading 期间的战斗章节卡：底要全不透明，否则会透出 Loading 的「加载中/百分比」。</summary>
    bool _opaqueBackdrop;
    /// <summary>战斗章节卡：Loading 没关之前一直不透明盖着，别把底下的 LoadingUI 又露出来。</summary>
    bool _coverUntilLoadingGone;
    /// <summary>需要逐字显现的三段（标题/描述/引用），按显现顺序登记。</summary>
    readonly List<RevealItem> _revealItems = new List<RevealItem>();
    /// <summary>文字是否已全部显现（点击跳过也视为已显现）；外部用它判断「卡片字演完了」。</summary>
    public bool IsRevealed { get; private set; }

    public static ChapterSplashOverlay Show(string title, string body = null, bool isTutorial = false,
        bool waitLoadingBeforeHold = false, bool opaqueBackdrop = false)
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
        driver._opaqueBackdrop = opaqueBackdrop;
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
        // 不等 Loading：卡片自己就是进战斗前的过场，演完再加载战斗场景。
        // 底要全不透明：卡片排在 LoadingUI 之上，半透会把「加载中 x%」透出来和章节文字叠在一起。
        var card = Show(title, body, isTutorial, waitLoadingBeforeHold: false, opaqueBackdrop: true);
        // 演完先别淡出：等 Loading 关掉再淡出，直接露出战斗场景，
        // 否则卡片一淡出底下的 LoadingUI 就又冒出来（2026-09-26 主人反馈的 bug）。
        if (card != null) card._coverUntilLoadingGone = true;
        return card;
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
        // 例外（2026-09-26）：Loading 期间的战斗章节卡（_opaqueBackdrop）背后只有 LoadingUI，
        // 半透会让「加载中 x%」和章节文字两层同时可见，故这里用全不透明，演完渐隐再露出 Loading。
        bg.color = new Color(0f, 0f, 0f, _opaqueBackdrop ? 1f : 0.72f);
        bg.raycastTarget = true;
        var bgRt = bg.rectTransform;
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        // 章节文字（标题/描述/引用）统一挂在这个容器下，整体往上抬 ContentLiftY。
        // 底图 Black 留在根节点不跟着动；文字三段的相对排版保持原样。
        var contentGo = new GameObject("Content");
        contentGo.transform.SetParent(transform, false);
        var contentRt = contentGo.AddComponent<RectTransform>();
        // 上抬用锚点整体 +lift 表达：与分辨率/参考分辨率无关，就是「逻辑高度的百分比」。
        contentRt.anchorMin = new Vector2(0f, ContentLiftY);
        contentRt.anchorMax = new Vector2(1f, 1f + ContentLiftY);
        contentRt.pivot = new Vector2(0.5f, 0.5f);
        contentRt.offsetMin = Vector2.zero;
        contentRt.offsetMax = Vector2.zero;

        var textGo = new GameObject("Title");
        textGo.transform.SetParent(contentRt, false);
        var text = textGo.AddComponent<Text>();
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.fontSize = 72;
        text.fontStyle = FontStyle.Bold;
        text.color = new Color(1f, 1f, 1f, 1f); // 纯白不透明
        text.raycastTarget = false;
        text.font = GameFonts.GetChinese(); // 章节中文：fusion-pixel
        _revealItems.Add(new RevealItem { text = text, target = title ?? "" });

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
            bodyGo.transform.SetParent(contentRt != null ? (Transform)contentRt : transform, false);
            var bodyText = bodyGo.AddComponent<Text>();
            bodyText.alignment = TextAnchor.MiddleCenter;
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            bodyText.fontSize = 28;
            bodyText.color = new Color(1f, 1f, 1f, 0.92f);
            bodyText.raycastTarget = false;
            bodyText.font = GameFonts.GetChinese();
            _revealItems.Add(new RevealItem { text = bodyText, target = body });
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
            quoteGo.transform.SetParent(contentRt != null ? (Transform)contentRt : transform, false);
            var quoteText = quoteGo.AddComponent<Text>();
            quoteText.alignment = TextAnchor.MiddleCenter;
            quoteText.horizontalOverflow = HorizontalWrapMode.Wrap;
            quoteText.verticalOverflow = VerticalWrapMode.Overflow;
            quoteText.fontSize = 26;
            quoteText.fontStyle = FontStyle.Italic;
            quoteText.color = new Color(1f, 1f, 1f, 0.85f); // 纯白，略低于正文以区分层次
            quoteText.raycastTarget = false;
            quoteText.font = GameFonts.GetChinese(); // 与标题/正文同款中文字体
            _revealItems.Add(new RevealItem { text = quoteText, target = quote });
            var qrt = quoteText.rectTransform;
            // 正文底沿在 0.22：引言放在其下方 0.04–0.18 的留白带，避免与标题/正文重叠。
            qrt.anchorMin = new Vector2(0.1f, 0.04f);
            qrt.anchorMax = new Vector2(0.9f, 0.18f);
            qrt.offsetMin = Vector2.zero;
            qrt.offsetMax = Vector2.zero;
        }

        ApplyRevealState();
        IsFinished = false;
    }

    /// <summary>
    /// 开关关掉：三段直接整段出现（原行为）。
    /// 开关打开：先清空，交给 CoRevealTexts 逐字填。
    /// </summary>
    void ApplyRevealState()
    {
        if (!CharRevealEnabled)
        {
            FinishRevealAll();
            return;
        }
        for (int i = 0; i < _revealItems.Count; i++)
        {
            var t = _revealItems[i].text;
            if (t != null) t.text = "";
        }
    }

    /// <summary>把还没显完的字一次性补齐（点击跳过 / 开关关闭时用）。</summary>
    void FinishRevealAll()
    {
        for (int i = 0; i < _revealItems.Count; i++)
        {
            var t = _revealItems[i].text;
            if (t != null) t.text = _revealItems[i].target;
        }
        IsRevealed = true;
    }

    /// <summary>
    /// 标题 → 描述 → 引用 依次逐个字显现：缓存目标串后每字只做一次 Substring，不整段重建。
    /// </summary>
    IEnumerator CoRevealTexts()
    {
        for (int g = 0; g < _revealItems.Count; g++)
        {
            var item = _revealItems[g];
            if (item.text == null || string.IsNullOrEmpty(item.target)) continue;

            if (g > 0)
            {
                float gap = 0f;
                while (gap < GroupRevealGap && !IsRevealed)
                {
                    gap += DeltaT();
                    yield return null;
                }
            }

            int len = item.target.Length;
            for (int i = 1; i <= len; i++)
            {
                if (IsRevealed) yield break; // 已被跳过后补齐，不用再逐字
                if (item.text == null) yield break;
                item.text.text = item.target.Substring(0, i);
                float w = 0f;
                while (w < CharRevealInterval)
                {
                    w += DeltaT();
                    yield return null;
                }
            }
        }
        IsRevealed = true;
    }

    /// <summary>与既有时序一致：用未缩放时间，退化时按一帧 0.016s 兜底。</summary>
    static float DeltaT() => Time.unscaledDeltaTime > 0.0001f ? Time.unscaledDeltaTime : 0.016f;

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

        // 逐字显现与 Hold 共用同一计时：卡片停留 = max(Hold, 显现耗时)，不额外拖长时间。
        if (CharRevealEnabled) StartCoroutine(CoRevealTexts());
        float hold = 0f;
        while (hold < holdSec || !IsRevealed)
        {
            hold += Time.unscaledDeltaTime;
            if (Clicked()) break;
            yield return null;
        }
        if (!IsRevealed) FinishRevealAll(); // 点击跳过：补齐剩下的字，别停在半句
        IsRevealed = true;

        // 战斗章节卡：Loading 还没关就保持不透明盖着（2026-09-26 修「章节字完后 loading 又冒出来」）。
        // 卡片排在 LoadingUI 之上，一旦先淡出就会露出「加载中 x%」，故这里等 Loading 关掉再淡出。
        if (_coverUntilLoadingGone)
        {
            const float coverMaxWait = 8f;
            float coverT = 0f;
            while ((SceneLoadingCoordinator.IsActive || BattleLoadingOverlay.IsShowing) && coverT < coverMaxWait)
            {
                coverT += DeltaT();
                if (Clicked()) break;
                yield return null;
            }
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
