using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 剧情对话窗：左右立绘 + 名牌 + 正文；可选 0~3 个选项；右上「跳过」。
/// AdvanceClick = 全屏近透明点击层：无选项时点任意处推进；有选项时禁用。
/// 立绘按 Sprite 自身像素大小显示，仅水平翻转面对面；对话框随界面宽度拉伸；
/// 我方/对方名牌挂在对话框上沿固定。资源在预制体里替换 Sprite。
/// </summary>
public class DialogueUI : MonoBehaviour
{
    public const int MaxChoices = 3;

    public static DialogueUI Instance { get; private set; }

    public bool IsVisible => gameObject.activeSelf;
    public string CurrentLine => dialogueText != null ? dialogueText.text : "";
    public bool HasChoicesVisible => _choicesVisible;

    [Header("可替换贴图")]
    public Image sceneBackgroundImage;
    public Image dialogueBoxImage;
    public Image leftPortraitImage;
    public Image rightPortraitImage;
    public Image leftNamePlateImage;
    public Image rightNamePlateImage;
    public Image leftNameIcon;
    public Image rightNameIcon;
    public Image nextArrowImage;
    public Image storyPropImage;
    public Image[] choiceButtonImages = new Image[MaxChoices];

    [Header("文本")]
    public Text dialogueText;
    public Text leftNameText;
    public Text rightNameText;
    public Text[] choiceTexts = new Text[MaxChoices];

    [Header("交互")]
    public Button advanceButton;
    public Button skipButton;
    public Button[] choiceButtons = new Button[MaxChoices];
    public UnityEvent onAdvance;
    public UnityEvent onSkip;
    public ChoiceEvent onChoiceSelected;

    [Serializable]
    public class ChoiceEvent : UnityEvent<int> { }

    [Header("立绘朝向")]
    [Tooltip("立绘原图默认是否面朝右；若原图朝左则关掉，翻转逻辑会取反")]
    public bool portraitArtFacesRight = true;

    [Header("打字机")]
    public float typeCharsPerSecond = 12f;

    Action _onAdvance;
    Action _onSkip;
    Action<int> _onChoice;
    bool _wired;
    bool _choicesVisible;
    bool _soloMode;
    StoryPortraitLayout.Profile _portraitProfile;
    bool _layoutCached;
    bool _revealing;
    bool _skipReveal;
    bool _typing;
    bool _typeComplete;
    bool _arrowBounce;
    Coroutine _typeCo;
    Coroutine _propFadeCo;
    string _fullLineText;
    Vector2 _arrowBasePos;
    Image _bgDim;
    Image _revealBlack;
    Text _locationCaption;
    RectTransform _textClip;
    string _initiatorName;
    string _otherName;
    StoryPortraitPresenter.LayoutSnapshot _leftPortraitLayout;
    StoryPortraitPresenter.LayoutSnapshot _rightPortraitLayout;
    RtLayout _leftPlateLayout;
    RtLayout _rightPlateLayout;

    struct RtLayout
    {
        public Vector2 amin, amax, pivot, pos;
        public Vector3 scale;
    }

    const int DialogueBodyFontSize = 28;
    const int DialogueNameFontSize = 18;
    const float LocBlackInDur = 0.55f;
    const float LocHoldDur = 2.0f;
    const float LocTextFadeDur = 1.1f;
    const float LocBgFadeDur = 1.15f;
    const float LocDimDur = 0.85f;

    void Awake()
    {
        Instance = this;
        if (dialogueBoxImage == null)
            AutoBindFromHierarchy();
        if (leftPortraitImage != null) leftPortraitImage.preserveAspect = true;
        if (rightPortraitImage != null) rightPortraitImage.preserveAspect = true;
        if (dialogueText != null)
            dialogueText.fontSize = DialogueBodyFontSize;
        EnsureTextClip();
        if (nextArrowImage != null)
            _arrowBasePos = nextArrowImage.rectTransform.anchoredPosition;
        WireClicks();
        HideChoices();
    }

    void Update()
    {
        if (!_arrowBounce || nextArrowImage == null || !nextArrowImage.gameObject.activeSelf)
            return;
        float y = Mathf.Sin(Time.unscaledTime * 6f) * 8f;
        var rt = nextArrowImage.rectTransform;
        rt.anchoredPosition = _arrowBasePos + new Vector2(0f, y);
    }

    /// <summary>长段剧情开始前：刷新 Canvas/相机，避免回城后立绘不显示。</summary>
    public void PrepareForStoryBeat()
    {
        gameObject.SetActive(true);
        if (transform.localScale == Vector3.zero)
            transform.localScale = Vector3.one;
        transform.SetAsLastSibling();
        var canvas = GetComponent<Canvas>();
        if (canvas != null)
            UICanvasSetup.RefreshPopup(canvas, GameConfig.UiSort.StoryDialogue);
        Canvas.ForceUpdateCanvases();
        if (_layoutCached)
        {
            StoryPortraitPresenter.ResetHost(leftPortraitImage, _leftPortraitLayout);
            StoryPortraitPresenter.ResetHost(rightPortraitImage, _rightPortraitLayout);
        }
        else
            CacheLayoutsIfNeeded();
    }

    /// <summary>保留兼容；不再改对话框、正文区、名牌——这些以预制体为准。</summary>
    public void EnsureAdaptiveLayout()
    {
        if (leftPortraitImage != null) leftPortraitImage.preserveAspect = true;
        if (rightPortraitImage != null) rightPortraitImage.preserveAspect = true;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void WireClicks()
    {
        if (_wired) return;
        _wired = true;

        if (advanceButton != null)
        {
            advanceButton.onClick.RemoveListener(OnClickAdvance);
            advanceButton.onClick.AddListener(OnClickAdvance);
        }
        if (skipButton != null)
        {
            skipButton.onClick.RemoveListener(OnClickSkip);
            skipButton.onClick.AddListener(OnClickSkip);
        }
        for (int i = 0; i < MaxChoices; i++)
        {
            int idx = i;
            if (choiceButtons == null || idx >= choiceButtons.Length || choiceButtons[idx] == null)
                continue;
            choiceButtons[idx].onClick.RemoveAllListeners();
            choiceButtons[idx].onClick.AddListener(() => OnClickChoice(idx));
        }
    }

    void OnClickAdvance()
    {
        if (_revealing)
        {
            _skipReveal = true;
            return;
        }
        if (_typing)
        {
            CompleteTyping();
            return;
        }
        if (!_typeComplete) return;
        if (_choicesVisible) return; // 有选项时不靠点空白继续
        onAdvance?.Invoke();
        _onAdvance?.Invoke();
    }

    void OnClickSkip()
    {
        if (_revealing)
        {
            _skipReveal = true;
            return;
        }
        onSkip?.Invoke();
        _onSkip?.Invoke();
    }

    void OnClickChoice(int index)
    {
        onChoiceSelected?.Invoke(index);
        _onChoice?.Invoke(index);
    }

    /// <summary>
    /// 全屏剧情背景（会长办公室 / 公会大厅）。sprite 为空则隐藏，露出城镇或战场。
    /// 不改预制体：运行时在 Canvas 最底层生成。
    /// </summary>
    public void SetSceneBackground(Sprite sprite)
    {
        EnsureSceneBackground();
        if (sceneBackgroundImage == null) return;

        Transform host = sceneBackgroundImage.transform.parent != null
            && sceneBackgroundImage.transform.parent != transform
            ? sceneBackgroundImage.transform.parent
            : sceneBackgroundImage.transform;

        if (sprite == null)
        {
            sceneBackgroundImage.sprite = null;
            sceneBackgroundImage.enabled = false;
            host.gameObject.SetActive(false);
            return;
        }

        host.gameObject.SetActive(true);
        sceneBackgroundImage.enabled = true;
        sceneBackgroundImage.sprite = sprite;
        sceneBackgroundImage.color = Color.white;
        sceneBackgroundImage.preserveAspect = true;
        var fitter = sceneBackgroundImage.GetComponent<AspectRatioFitter>();
        if (fitter == null) fitter = sceneBackgroundImage.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        float w = sprite.rect.width;
        float h = Mathf.Max(1f, sprite.rect.height);
        fitter.aspectRatio = w / h;
        EnsureBgDim();
        SetBgDim(0f);
        var dim = transform.Find("Dim");
        if (dim != null)
            dim.gameObject.SetActive(false);
    }

    void EnsureSceneBackground()
    {
        if (sceneBackgroundImage != null) return;

        var existing = transform.Find("SceneBackgroundHost/SceneBackground")
                       ?? transform.Find("SceneBackground");
        if (existing != null)
        {
            sceneBackgroundImage = existing.GetComponent<Image>();
            if (sceneBackgroundImage != null) return;
        }

        var hostGo = new GameObject("SceneBackgroundHost", typeof(RectTransform), typeof(RectMask2D));
        hostGo.transform.SetParent(transform, false);
        hostGo.transform.SetAsFirstSibling();
        var hostRt = hostGo.GetComponent<RectTransform>();
        hostRt.anchorMin = Vector2.zero;
        hostRt.anchorMax = Vector2.one;
        hostRt.offsetMin = Vector2.zero;
        hostRt.offsetMax = Vector2.zero;

        var imgGo = new GameObject("SceneBackground", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
        imgGo.transform.SetParent(hostGo.transform, false);
        var imgRt = imgGo.GetComponent<RectTransform>();
        imgRt.anchorMin = new Vector2(0.5f, 0.5f);
        imgRt.anchorMax = new Vector2(0.5f, 0.5f);
        imgRt.pivot = new Vector2(0.5f, 0.5f);
        imgRt.anchoredPosition = Vector2.zero;
        imgRt.sizeDelta = Vector2.zero;

        sceneBackgroundImage = imgGo.GetComponent<Image>();
        sceneBackgroundImage.raycastTarget = false;
        sceneBackgroundImage.preserveAspect = true;
        var fitter = imgGo.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        hostGo.SetActive(false);
    }

    /// <summary>
    /// 换场景：先亮背景 + 地点名，字淡出，背景压暗，再出立绘和对话框。
    /// </summary>
    public IEnumerator PlayLocationReveal(string title)
    {
        _revealing = true;
        _skipReveal = false;
        SetDialogueChromeVisible(false);
        // 换场景前清掉上一句道具（如委托书），否则会盖住「公会大厅」地点名
        ClearStoryPropForReveal();
        EnsureLocationCaption();
        EnsureBgDim();
        EnsureRevealBlack();
        SetBgDim(0f);
        SetRevealBlack(1f);
        SetSceneBackgroundAlpha(0f);
        if (!string.IsNullOrEmpty(title))
        {
            float blackInT = 0f;
            while (blackInT < LocBlackInDur && !_skipReveal)
            {
                blackInT += Time.unscaledDeltaTime;
                SetRevealBlack(Mathf.Clamp01(blackInT / LocBlackInDur));
                yield return null;
            }
            SetRevealBlack(1f);
            RaiseLocationCaption();
            SetLocationCaption(title, 1f);
            yield return WaitUnscaled(LocHoldDur);
            float fadeT = 0f;
            while (fadeT < LocTextFadeDur && !_skipReveal)
            {
                fadeT += Time.unscaledDeltaTime;
                SetLocationCaption(title, 1f - Mathf.Clamp01(fadeT / LocTextFadeDur));
                yield return null;
            }
        }
        SetLocationCaption(title, 0f);
        // 背景先在黑幕后面开满，再单独淡出黑幕；不做交叉淡入，
        // 否则中途两层都半透明会漏出下面明亮的城镇，看着像闪白。
        SetSceneBackgroundAlpha(1f);
        yield return null;
        float bgFadeT = 0f;
        while (bgFadeT < LocBgFadeDur && !_skipReveal)
        {
            bgFadeT += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(bgFadeT / LocBgFadeDur);
            SetRevealBlack(1f - p);
            yield return null;
        }
        SetSceneBackgroundAlpha(1f);
        SetRevealBlack(0f);

        float dimT = 0f;
        const float dimTarget = 0.42f;
        while (dimT < LocDimDur && !_skipReveal)
        {
            dimT += Time.unscaledDeltaTime;
            SetBgDim(Mathf.Lerp(0f, dimTarget, Mathf.Clamp01(dimT / LocDimDur)));
            yield return null;
        }
        SetBgDim(dimTarget);
        _revealing = false;
        _skipReveal = false;
    }

    IEnumerator WaitUnscaled(float seconds)
    {
        float t = 0f;
        while (t < seconds && !_skipReveal)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    public void SetDialogueChromeVisible(bool on)
    {
        if (dialogueBoxImage != null) dialogueBoxImage.gameObject.SetActive(on);
        if (leftPortraitImage != null && !on) leftPortraitImage.gameObject.SetActive(false);
        if (rightPortraitImage != null && !on) rightPortraitImage.gameObject.SetActive(false);
        if (leftNamePlateImage != null) leftNamePlateImage.gameObject.SetActive(on && !string.IsNullOrEmpty(_initiatorName));
        if (rightNamePlateImage != null) rightNamePlateImage.gameObject.SetActive(on && !string.IsNullOrEmpty(_otherName));
        if (skipButton != null) skipButton.gameObject.SetActive(on);
        if (nextArrowImage != null) nextArrowImage.gameObject.SetActive(on);
        if (advanceButton != null)
        {
            advanceButton.gameObject.SetActive(on);
            advanceButton.interactable = on;
            var img = advanceButton.targetGraphic as Image;
            if (img != null) img.raycastTarget = on;
        }
        var choice = transform.Find("ChoicePanel");
        if (choice != null && !on) choice.gameObject.SetActive(false);
        if (!on) SetLocationCaption("", 0f);
    }

    void EnsureRevealBlack()
    {
        if (_revealBlack != null) return;
        var go = new GameObject("RevealBlack", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        _revealBlack = go.GetComponent<Image>();
        _revealBlack.color = Color.black;
        _revealBlack.raycastTarget = false;
        go.transform.SetSiblingIndex(1);
    }

    void EnsureBgDim()
    {
        if (_bgDim != null) return;
        EnsureSceneBackground();
        if (sceneBackgroundImage == null) return;
        Transform host = sceneBackgroundImage.transform.parent != null
            ? sceneBackgroundImage.transform.parent
            : sceneBackgroundImage.transform;
        var t = host.Find("BgDim");
        if (t != null)
        {
            _bgDim = t.GetComponent<Image>();
            return;
        }
        var go = new GameObject("BgDim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(host, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        _bgDim = go.GetComponent<Image>();
        _bgDim.color = new Color(0f, 0f, 0f, 0f);
        _bgDim.raycastTarget = false;
        go.transform.SetAsLastSibling();
    }

    public void SetRevealBlack(float a)
    {
        EnsureRevealBlack();
        if (_revealBlack == null) return;
        _revealBlack.gameObject.SetActive(a > 0.01f);
        _revealBlack.color = new Color(0f, 0f, 0f, Mathf.Clamp01(a));
    }

    public void SetBgDim(float a)
    {
        EnsureBgDim();
        if (_bgDim == null) return;
        _bgDim.gameObject.SetActive(a > 0.01f);
        _bgDim.color = new Color(0f, 0f, 0f, Mathf.Clamp01(a));
    }

    public void SetSceneBackgroundAlpha(float a)
    {
        if (sceneBackgroundImage == null) return;
        var c = sceneBackgroundImage.color;
        c.a = Mathf.Clamp01(a);
        sceneBackgroundImage.color = c;
        sceneBackgroundImage.enabled = a > 0.01f && sceneBackgroundImage.sprite != null;
    }

    void EnsureLocationCaption()
    {
        if (_locationCaption != null) return;
        var go = new GameObject("LocationCaption", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Shadow));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.15f, 0.44f);
        rt.anchorMax = new Vector2(0.85f, 0.56f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        _locationCaption = go.GetComponent<Text>();
        _locationCaption.alignment = TextAnchor.MiddleCenter;
        _locationCaption.fontSize = 52;
        _locationCaption.fontStyle = FontStyle.Bold;
        _locationCaption.color = new Color(1f, 0.95f, 0.82f, 1f);
        _locationCaption.raycastTarget = false;
        _locationCaption.horizontalOverflow = HorizontalWrapMode.Wrap;
        _locationCaption.verticalOverflow = VerticalWrapMode.Overflow;
        var font = GameFonts.GetChinese();
        if (font != null) _locationCaption.font = font;
        var shadow = go.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.92f);
        shadow.effectDistance = new Vector2(4f, -4f);
        go.transform.SetSiblingIndex(2);
    }

    void SetLocationCaption(string title, float alpha)
    {
        EnsureLocationCaption();
        if (_locationCaption == null) return;
        _locationCaption.text = title ?? "";
        var c = _locationCaption.color;
        c.a = Mathf.Clamp01(alpha);
        _locationCaption.color = c;
        bool show = alpha > 0.02f && !string.IsNullOrEmpty(title);
        _locationCaption.gameObject.SetActive(show);
        if (show) RaiseLocationCaption();
    }

    /// <summary>地点名压在黑幕 / 残留道具之上。</summary>
    void RaiseLocationCaption()
    {
        if (_locationCaption == null) return;
        if (_revealBlack != null)
            _locationCaption.transform.SetSiblingIndex(_revealBlack.transform.GetSiblingIndex() + 1);
        _locationCaption.transform.SetAsLastSibling();
    }

    void ClearStoryPropForReveal()
    {
        if (_propFadeCo != null)
        {
            StopCoroutine(_propFadeCo);
            _propFadeCo = null;
        }
        SetStoryPropImmediate(null);
    }

    /// <summary>
    /// 开一场对话：发起方在左（面朝右），对方在右（面朝左）。
    /// speakerIsInitiator=true 表示当前说话的是发起方。
    /// 立绘按 Sprite 自身像素大小显示（不统一拉伸），仅做水平翻转。
    /// </summary>
    public void ShowLine(
        string initiatorName,
        string otherName,
        string content,
        Sprite initiatorPortrait,
        Sprite otherPortrait,
        Sprite propSprite,
        bool speakerIsInitiator = true,
        Action onAdvance = null,
        Action onSkip = null,
        bool soloCentered = false)
    {
        _initiatorName = initiatorName ?? "";
        _otherName = otherName ?? "";
        _onAdvance = onAdvance;
        _onSkip = onSkip;
        _onChoice = null;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        var canvas = GetComponent<Canvas>();
        if (canvas != null)
            UICanvasSetup.RefreshPopup(canvas, GameConfig.UiSort.StoryDialogue);
        Canvas.ForceUpdateCanvases();
        SetDialogueChromeVisible(true);
        ApplyDialogueBoxLift();
        FitDialogueBoxAspect(); // 只校正九宫格，不改框尺寸

        if (leftNameText != null) leftNameText.text = _initiatorName;
        if (rightNameText != null) rightNameText.text = _otherName;
        ApplyNameTypography(leftNameText);
        ApplyNameTypography(rightNameText);
        BeginTyping(content ?? "");
        SetStoryProp(propSprite);
        if (propSprite != null)
        {
            HidePortraitsForStoryProp();
            RaiseStoryPropAboveDialogue();
        }

        _portraitProfile = StoryPortraitLayout.Unified;

        bool newPortraits = initiatorPortrait != null || otherPortrait != null;
        if (propSprite == null && newPortraits)
        {
            var ctx = BuildPortraitContext();
            CacheLayoutsIfNeeded();
            if (soloCentered)
            {
                _soloMode = true;
                Sprite sp = otherPortrait != null ? otherPortrait : initiatorPortrait;
                StoryPortraitPresenter.ApplySolo(
                    rightPortraitImage, leftPortraitImage, sp,
                    _rightPortraitLayout, _leftPortraitLayout,
                    _portraitProfile, ctx);
                ApplySoloNamePlate(sp != null);
            }
            else
            {
                _soloMode = false;
                RestoreLayout(leftNamePlateImage, _leftPlateLayout);
                RestoreLayout(rightNamePlateImage, _rightPlateLayout);
                StoryPortraitPresenter.ApplyDual(
                    leftPortraitImage, rightPortraitImage,
                    initiatorPortrait, otherPortrait,
                    _leftPortraitLayout, _rightPortraitLayout,
                    _portraitProfile, ctx);
                SetNamePlateIconVisible(leftNameIcon, false);
                SetNamePlateIconVisible(rightNameIcon, false);
            }
        }

        // #region agent log
        if (newPortraits)
        {
            float lH = leftPortraitImage != null ? leftPortraitImage.rectTransform.sizeDelta.y : 0f;
            float rH = rightPortraitImage != null ? rightPortraitImage.rectTransform.sizeDelta.y : 0f;
            DebugAgentLog.Log("H14", "DialogueUI.ShowLine", "portrait_layout",
                $"{{\"solo\":{(soloCentered ? "true" : "false")},\"leftH\":{lH:F1},\"rightH\":{rH:F1},\"leftActive\":{(leftPortraitImage != null && leftPortraitImage.gameObject.activeSelf ? "true" : "false")},\"rightActive\":{(rightPortraitImage != null && rightPortraitImage.gameObject.activeSelf ? "true" : "false")},\"profileH\":{_portraitProfile.screenHeightFrac:F3}}}");
        }
        // #endregion

        if (_soloMode)
            ApplySoloHighlight();
        else
        {
            // 左右立绘朝向中间；右侧资源朝外时再强制翻一次
            ApplyFacing(leftFacingRight: true, rightFacingRight: false);
            ForceRightPortraitFaceInward();
            SetSpeakerHighlight(speakerIsInitiator ? -1 : 1);
        }
        SyncStoryPortraitIdleMotion();
        FitDialogueBodyToBox();
        HideChoices();
        SetAdvanceInteractable(true);
        if (skipButton != null)
            skipButton.gameObject.SetActive(true);
    }

    /// <summary>只校正九宫格切图；框的宽高始终用预制体，字多用框内滚动。</summary>
    void FitDialogueBoxAspect()
    {
        if (dialogueBoxImage == null) return;
        Vector4 border = dialogueBoxImage.sprite != null ? dialogueBoxImage.sprite.border : Vector4.zero;
        bool sliced = (border.x + border.y + border.z + border.w) > 0.01f;
        dialogueBoxImage.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        dialogueBoxImage.preserveAspect = !sliced;
    }

    void BeginTyping(string full)
    {
        if (_typeCo != null)
        {
            StopCoroutine(_typeCo);
            _typeCo = null;
        }
        _fullLineText = full ?? "";
        _typeComplete = false;
        _typing = true;
        HideNextArrow();
        EnsureTextClip();
        _typeCo = StartCoroutine(TypeLineRoutine(_fullLineText));
    }

    IEnumerator TypeLineRoutine(string full)
    {
        if (dialogueText != null) dialogueText.text = "";
        float delay = 1f / Mathf.Max(1f, typeCharsPerSecond);
        for (int i = 1; i <= full.Length; i++)
        {
            if (dialogueText != null)
                dialogueText.text = full.Substring(0, i);
            ScrollDialogueText();
            float t = 0f;
            while (t < delay)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        _typing = false;
        _typeComplete = true;
        _typeCo = null;
        ShowNextArrowBounce();
    }

    void CompleteTyping()
    {
        if (_typeCo != null)
        {
            StopCoroutine(_typeCo);
            _typeCo = null;
        }
        _typing = false;
        _typeComplete = true;
        if (dialogueText != null)
            dialogueText.text = _fullLineText ?? "";
        ScrollDialogueText();
        ShowNextArrowBounce();
    }

    void ShowNextArrowBounce()
    {
        if (nextArrowImage == null) return;
        nextArrowImage.gameObject.SetActive(true);
        _arrowBounce = true;
        nextArrowImage.rectTransform.anchoredPosition = _arrowBasePos;
    }

    void HideNextArrow()
    {
        _arrowBounce = false;
        if (nextArrowImage != null)
            nextArrowImage.gameObject.SetActive(false);
    }

    void EnsureTextClip()
    {
        if (dialogueText == null || _textClip != null) return;
        if (dialogueText.transform.parent != null &&
            dialogueText.transform.parent.GetComponent<RectMask2D>() != null)
        {
            _textClip = dialogueText.transform.parent as RectTransform;
            PrepareDialogueTextForClip();
            return;
        }

        var src = dialogueText.rectTransform;
        var go = new GameObject("TextClip", typeof(RectTransform), typeof(RectMask2D));
        go.transform.SetParent(src.parent, false);
        _textClip = go.GetComponent<RectTransform>();
        _textClip.anchorMin = src.anchorMin;
        _textClip.anchorMax = src.anchorMax;
        _textClip.pivot = src.pivot;
        _textClip.offsetMin = src.offsetMin;
        _textClip.offsetMax = src.offsetMax;
        _textClip.anchoredPosition = src.anchoredPosition;
        _textClip.sizeDelta = src.sizeDelta;
        _textClip.SetSiblingIndex(src.GetSiblingIndex());
        dialogueText.transform.SetParent(_textClip, false);
        PrepareDialogueTextForClip();
    }

    void PrepareDialogueTextForClip()
    {
        if (dialogueText == null) return;
        dialogueText.alignment = TextAnchor.UpperLeft;
        dialogueText.horizontalOverflow = HorizontalWrapMode.Wrap;
        dialogueText.verticalOverflow = VerticalWrapMode.Overflow;
        var rt = dialogueText.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        float h = _textClip != null ? Mathf.Max(1f, _textClip.rect.height) : 160f;
        rt.sizeDelta = new Vector2(0f, h);
    }

    void ScrollDialogueText()
    {
        EnsureTextClip();
        if (dialogueText == null || _textClip == null) return;
        float viewH = Mathf.Max(1f, _textClip.rect.height);
        float pref = Mathf.Max(viewH, dialogueText.preferredHeight);
        var rt = dialogueText.rectTransform;
        rt.sizeDelta = new Vector2(0f, pref);
        float overflow = pref - viewH;
        rt.anchoredPosition = overflow > 1f ? new Vector2(0f, overflow) : Vector2.zero;
    }

    void HidePortraitsForStoryProp()
    {
        if (leftPortraitImage != null) leftPortraitImage.gameObject.SetActive(false);
        if (rightPortraitImage != null) rightPortraitImage.gameObject.SetActive(false);
        if (leftNamePlateImage != null) leftNamePlateImage.gameObject.SetActive(false);
        if (rightNamePlateImage != null) rightNamePlateImage.gameObject.SetActive(false);
        SetNamePlateIconVisible(leftNameIcon, false);
        SetNamePlateIconVisible(rightNameIcon, false);
    }

    StoryPortraitPresenter.Context BuildPortraitContext()
    {
        return new StoryPortraitPresenter.Context
        {
            CanvasRt = transform as RectTransform,
            DialogueBox = dialogueBoxImage,
            MobileLiftY = GetMobileLiftY(),
            PlaceBehindDialogueBox = PlacePortraitBehindDialogueBox
        };
    }

    void ApplySoloNamePlate(bool hasPortrait)
    {
        string name = !string.IsNullOrEmpty(_otherName) ? _otherName : _initiatorName;
        if (leftNamePlateImage != null)
        {
            leftNamePlateImage.gameObject.SetActive(hasPortrait && !string.IsNullOrEmpty(name));
            leftNamePlateImage.color = Color.white;
        }
        if (rightNamePlateImage != null)
            rightNamePlateImage.gameObject.SetActive(false);
        if (leftNameText != null)
        {
            leftNameText.text = name ?? "";
            leftNameText.color = new Color(1f, 0.95f, 0.85f);
            ApplyNameTypography(leftNameText);
        }
        SetNamePlateIconVisible(leftNameIcon, false);
        SetNamePlateIconVisible(rightNameIcon, false);
    }

    void CacheLayoutsIfNeeded()
    {
        if (_layoutCached) return;
        _leftPortraitLayout = StoryPortraitPresenter.Capture(leftPortraitImage);
        _rightPortraitLayout = StoryPortraitPresenter.Capture(rightPortraitImage);
        _leftPlateLayout = Capture(leftNamePlateImage);
        _rightPlateLayout = Capture(rightNamePlateImage);
        _layoutCached = true;
    }

    static RtLayout Capture(Image img)
    {
        var lay = new RtLayout();
        if (img == null) return lay;
        var rt = img.rectTransform;
        lay.amin = rt.anchorMin;
        lay.amax = rt.anchorMax;
        lay.pivot = rt.pivot;
        lay.pos = rt.anchoredPosition;
        lay.scale = rt.localScale;
        return lay;
    }

    static void RestoreLayout(Image img, RtLayout lay)
    {
        if (img == null) return;
        var rt = img.rectTransform;
        rt.anchorMin = lay.amin;
        rt.anchorMax = lay.amax;
        rt.pivot = lay.pivot;
        rt.anchoredPosition = lay.pos;
        rt.localScale = lay.scale;
        img.gameObject.SetActive(true);
    }

    static RectTransform GetPortraitHostRt(Image portrait)
        => StoryPortraitPresenter.GetHostRtPublic(portrait);

    void ApplySoloHighlight()
    {
        bool propOn = storyPropImage != null && storyPropImage.gameObject.activeSelf && storyPropImage.sprite != null;
        SetPortraitDim(rightPortraitImage, propOn);
        bool hasName = !string.IsNullOrEmpty(_otherName) || !string.IsNullOrEmpty(_initiatorName);
        if (leftNamePlateImage != null)
            leftNamePlateImage.gameObject.SetActive(hasName);
        SetPlateActive(leftNamePlateImage, leftNameText, true);
        // 名牌图自带头像位，子 Icon 保持关闭
        SetNamePlateIconVisible(leftNameIcon, false);
        if (rightNamePlateImage != null)
            rightNamePlateImage.gameObject.SetActive(false);
        SetNamePlateIconVisible(rightNameIcon, false);
    }

    /// <summary>兼容旧调用；speakerSide: -1 左(发起方) / 0 旁白 / 1 右(对方)</summary>
    public void Show(string leftName, string rightName, string content, int speakerSide = -1, Action onAdvance = null)
    {
        ShowLine(leftName, rightName, content, null, null, null, speakerIsInitiator: speakerSide != 1, onAdvance: onAdvance);
        if (speakerSide == 0)
            SetSpeakerHighlight(0);
    }

    public void SetContent(string content, bool speakerIsInitiator = true)
    {
        BeginTyping(content ?? "");
        SetSpeakerHighlight(speakerIsInitiator ? -1 : 1);
        HideChoices();
        SetAdvanceInteractable(true);
    }

    /// <summary>
    /// 显示选项（1~3 条）。有选项时隐藏继续箭头，点空白不推进。
    /// </summary>
    public void ShowChoices(string[] labels, Action<int> onChoice)
    {
        _onChoice = onChoice;
        if (labels == null || labels.Length == 0)
        {
            HideChoices();
            return;
        }

        int count = Mathf.Min(labels.Length, MaxChoices);
        _choicesVisible = true;
        SetAdvanceInteractable(false);
        if (nextArrowImage != null)
            nextArrowImage.gameObject.SetActive(false);

        var panel = transform.Find("ChoicePanel");
        if (panel != null) panel.gameObject.SetActive(true);

        for (int i = 0; i < MaxChoices; i++)
        {
            bool on = i < count && !string.IsNullOrEmpty(labels[i]);
            if (choiceButtons != null && i < choiceButtons.Length && choiceButtons[i] != null)
                choiceButtons[i].gameObject.SetActive(on);
            if (on && choiceTexts != null && i < choiceTexts.Length && choiceTexts[i] != null)
                choiceTexts[i].text = labels[i];
        }
    }

    public void HideChoices()
    {
        _choicesVisible = false;
        var panel = transform.Find("ChoicePanel");
        if (panel != null) panel.gameObject.SetActive(false);
        if (choiceButtons == null) return;
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (choiceButtons[i] != null)
                choiceButtons[i].gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 左右立绘朝向。发起方在左默认朝右；对方在右默认朝左（水平翻转）。
    /// 若当前说话方在右，仍保持面对面；仅高亮变化。需要「对调方向」时可再调此方法。
    /// </summary>
    public void ApplyFacing(bool leftFacingRight, bool rightFacingRight)
    {
        SetPortraitFacing(leftPortraitImage, leftFacingRight);
        SetPortraitFacing(rightPortraitImage, rightFacingRight);
    }

    /// <summary>
    /// 对方开口时：对调双方朝向（左改朝左、右改朝右），形成「转头看对方」的感觉；
    /// 也可只用于高亮，朝向仍面对面——默认在 SetSpeakerHighlight 里保持面对面。
    /// </summary>
    public void FaceEachOther()
    {
        ApplyFacing(leftFacingRight: true, rightFacingRight: false);
    }

    void SetPortraitFacing(Image img, bool faceRight)
    {
        if (img == null) return;
        bool flip = portraitArtFacesRight ? !faceRight : faceRight;
        var s = img.rectTransform.localScale;
        float ax = Mathf.Abs(s.x);
        if (ax < 0.01f) ax = 1f;
        s.x = ax * (flip ? -1f : 1f);
        s.y = Mathf.Abs(s.y) < 0.01f ? 1f : Mathf.Abs(s.y);
        s.z = 1f;
        img.rectTransform.localScale = s;
    }

    /// <summary>右侧立绘强制朝左（画面中心），避免背对玩家。</summary>
    void ForceRightPortraitFaceInward()
    {
        if (rightPortraitImage == null) return;
        var s = rightPortraitImage.rectTransform.localScale;
        float ay = Mathf.Abs(s.y);
        if (ay < 0.01f) ay = 1f;
        s.x = -Mathf.Abs(s.x < 0.01f ? 1f : s.x);
        s.y = ay;
        s.z = 1f;
        rightPortraitImage.rectTransform.localScale = s;
    }

    /// <summary>立绘用 Sprite 原始像素尺寸，不统一成固定框</summary>
    public static void ApplyPortraitNativeSize(Image img)
    {
        if (img == null) return;
        img.preserveAspect = true;
        img.type = Image.Type.Simple;
        if (img.sprite == null)
        {
            img.enabled = false;
            return;
        }
        img.enabled = true;
        img.SetNativeSize();
    }

    /// <summary>正文按对话框可视区域自动换行，避免挤成一行或溢出。</summary>
    void FitDialogueBodyToBox()
    {
        EnsureTextClip();
        if (dialogueText == null) return;
        dialogueText.alignment = TextAnchor.UpperLeft;
        dialogueText.horizontalOverflow = HorizontalWrapMode.Wrap;
        dialogueText.verticalOverflow = VerticalWrapMode.Overflow;
        dialogueText.resizeTextForBestFit = false;
        if (DialogueBodyFontSize > 0)
            dialogueText.fontSize = DialogueBodyFontSize;
        ScrollDialogueText();
    }

    public void SetSpeakerHighlight(int speakerSide)
    {
        if (_soloMode)
        {
            ApplySoloHighlight();
            return;
        }
        // -1 左说话 / 1 右说话 / 0 旁白两边正常
        bool leftSpeak = speakerSide == -1 || speakerSide == 0;
        bool rightSpeak = speakerSide == 1 || speakerSide == 0;
        SetPlateActive(leftNamePlateImage, leftNameText, leftSpeak || speakerSide == 0);
        SetPlateActive(rightNamePlateImage, rightNameText, rightSpeak || speakerSide == 0);
        SetNamePlateIconVisible(leftNameIcon, (leftSpeak || speakerSide == 0) && !string.IsNullOrEmpty(_initiatorName));
        SetNamePlateIconVisible(rightNameIcon, (rightSpeak || speakerSide == 0) && !string.IsNullOrEmpty(_otherName));

        if (speakerSide == -1)
        {
            SetPortraitDim(leftPortraitImage, false);
            SetPortraitDim(rightPortraitImage, true);
            FaceEachOther();
        }
        else if (speakerSide == 1)
        {
            SetPortraitDim(leftPortraitImage, true);
            SetPortraitDim(rightPortraitImage, false);
            // 对方说话：对调朝向，右侧说话者面朝左看发起方（仍面对面），并保证翻转正确
            FaceEachOther();
        }
        else
        {
            SetPortraitDim(leftPortraitImage, false);
            SetPortraitDim(rightPortraitImage, false);
            FaceEachOther();
        }
        SyncStoryPortraitIdleMotion();
    }

    /// <summary>剧情立绘待机：呼吸 + 浮动 + 微倾（与酒馆老板娘同款）。</summary>
    void SyncStoryPortraitIdleMotion()
    {
        SyncOnePortraitIdleMotion(leftPortraitImage, 0f);
        SyncOnePortraitIdleMotion(rightPortraitImage, 0.5f);
    }

    static void SyncOnePortraitIdleMotion(Image img, float phaseSeed)
    {
        if (img == null) return;
        var host = GetPortraitHostRt(img);
        if (!img.gameObject.activeSelf || img.sprite == null)
        {
            if (host != null)
            {
                var idleHost = host.GetComponent<PortraitIdleMotion>();
                if (idleHost != null) idleHost.enabled = false;
            }
            var idleImg = img.GetComponent<PortraitIdleMotion>();
            if (idleImg != null) idleImg.enabled = false;
            return;
        }
        var onImg = img.GetComponent<PortraitIdleMotion>();
        if (onImg != null) onImg.enabled = false;
        if (host == null) return;
        host.pivot = new Vector2(0.5f, 0f);
        PortraitIdleMotion.EnsureOn(host, phaseSeed, breathAmount: 0.006f);
    }

    static void SetPortraitDim(Image img, bool dim)
    {
        if (img == null) return;
        img.color = dim ? new Color(0.55f, 0.55f, 0.55f, 1f) : Color.white;
    }

    static void SetPlateActive(Image plate, Text name, bool active)
    {
        if (plate != null)
            plate.color = active ? Color.white : new Color(1f, 1f, 1f, 0.55f);
        if (name != null)
        {
            name.color = active ? new Color(1f, 0.95f, 0.85f) : new Color(0.7f, 0.65f, 0.55f, 0.7f);
            ApplyNameTypography(name);
        }
    }

    static void ApplyNameTypography(Text name)
    {
        if (name == null) return;
        if (name.font == null) name.font = GameFonts.GetChinese();
        int size = name.fontSize > 0 ? name.fontSize : DialogueNameFontSize;
        name.fontSize = Mathf.Max(DialogueNameFontSize, size);
        name.fontStyle = FontStyle.Bold;
    }

    static void SetNamePlateIconVisible(Image icon, bool visible)
    {
        if (icon == null) return;
        // 需求：对话名牌头像 icon 统一关闭。
        icon.gameObject.SetActive(false);
    }

    /// <summary>
    /// 立绘盖在背景上、但必须在对话框下面。
    /// 注意：若立绘本来就在框下方，再 SetSiblingIndex(框的下标) 会把框挤下去，立绘反而跑到前面。
    /// </summary>
    void PlacePortraitBehindDialogueBox(Transform portrait)
    {
        if (portrait == null || dialogueBoxImage == null) return;
        if (portrait.parent != dialogueBoxImage.transform.parent) return;
        int boxIdx = dialogueBoxImage.transform.GetSiblingIndex();
        int pIdx = portrait.GetSiblingIndex();
        if (pIdx >= boxIdx)
            portrait.SetSiblingIndex(boxIdx);
    }

    float GetMobileLiftY()
    {
        var canvasRt = transform as RectTransform;
        float h = canvasRt != null ? canvasRt.rect.height : 0f;
        if (h < 64f) h = GameConfig.DESIGN_HEIGHT;
        return h * 0.08f;
    }

    void ApplyDialogueBoxLift()
    {
        if (dialogueBoxImage == null) return;
        var rt = dialogueBoxImage.rectTransform;
        float baseY = _dialogueBoxBaseY >= 0f ? _dialogueBoxBaseY : rt.anchoredPosition.y;
        _dialogueBoxBaseY = baseY;
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, baseY + GetMobileLiftY());
    }

    float _dialogueBoxBaseY = -1f;

    void SetAdvanceInteractable(bool on)
    {
        if (advanceButton != null)
            advanceButton.interactable = on;
    }

    public void Hide()
    {
        _onAdvance = null;
        _onSkip = null;
        _onChoice = null;
        _revealing = false;
        _typing = false;
        _typeComplete = false;
        _arrowBounce = false;
        if (_typeCo != null)
        {
            StopCoroutine(_typeCo);
            _typeCo = null;
        }
        HideNextArrow();
        HideChoices();
        if (_propFadeCo != null)
        {
            StopCoroutine(_propFadeCo);
            _propFadeCo = null;
        }
        SetStoryPropImmediate(null);
        SetLocationCaption("", 0f);
        SetSceneBackground(null);
        SetRevealBlack(0f);
        SetBgDim(0f);
        var dim = transform.Find("Dim");
        if (dim != null) dim.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    /// <summary>显示或清除剧情道具叠图（null = 立刻隐藏）。</summary>
    public void SetStoryProp(Sprite sprite)
    {
        EnsureStoryProp();
        if (storyPropImage == null) return;
        if (_propFadeCo != null)
        {
            StopCoroutine(_propFadeCo);
            _propFadeCo = null;
        }

        if (sprite == null)
        {
            // 下一句无道具：立刻关掉，避免盖住立绘 / 地点名
            SetStoryPropImmediate(null);
            return;
        }

        storyPropImage.sprite = sprite;
        storyPropImage.color = Color.white;
        storyPropImage.preserveAspect = true;
        storyPropImage.type = Image.Type.Simple;
        storyPropImage.gameObject.SetActive(true);
        ClampStoryPropSize();
        RaiseStoryPropAboveDialogue();
        HidePortraitsForStoryProp();
    }

    /// <summary>道具图必须在对话框之上，否则委托书等会被 DialogueBox 挡住。</summary>
    void RaiseStoryPropAboveDialogue()
    {
        if (storyPropImage == null) return;
        var skip = skipButton != null ? skipButton.transform : null;
        if (skip != null)
            storyPropImage.transform.SetSiblingIndex(skip.GetSiblingIndex());
        else
            storyPropImage.transform.SetAsLastSibling();
    }

    void SetStoryPropImmediate(Sprite sprite)
    {
        EnsureStoryProp();
        if (storyPropImage == null) return;
        if (sprite == null)
        {
            storyPropImage.sprite = null;
            storyPropImage.gameObject.SetActive(false);
            return;
        }
        storyPropImage.sprite = sprite;
        storyPropImage.color = Color.white;
        storyPropImage.preserveAspect = true;
        storyPropImage.type = Image.Type.Simple;
        storyPropImage.gameObject.SetActive(true);
        ClampStoryPropSize();
        RaiseStoryPropAboveDialogue();
        HidePortraitsForStoryProp();
    }

    void ClampStoryPropSize()
    {
        if (storyPropImage == null || storyPropImage.sprite == null) return;
        Canvas.ForceUpdateCanvases();

        var sprite = storyPropImage.sprite;
        storyPropImage.SetNativeSize();
        var rt = storyPropImage.rectTransform;

        Vector2 n = rt.sizeDelta;
        if (n.x < 1f || n.y < 1f)
        {
            float ppu = sprite.pixelsPerUnit > 0.01f ? sprite.pixelsPerUnit : 100f;
            n = new Vector2(sprite.rect.width / ppu, sprite.rect.height / ppu);
            rt.sizeDelta = n;
            if (n.x < 1f || n.y < 1f) return;
        }

        var canvasRt = transform as RectTransform;
        float canvasW = canvasRt != null ? canvasRt.rect.width : 0f;
        float canvasH = canvasRt != null ? canvasRt.rect.height : 0f;
        if (canvasW < 64f) canvasW = GameConfig.DESIGN_WIDTH;
        if (canvasH < 64f) canvasH = GameConfig.DESIGN_HEIGHT;

        float maxW = canvasW * 0.72f;
        float maxH = canvasH * 0.55f;
        if (maxW < 1f || maxH < 1f)
        {
            rt.localScale = Vector3.one;
            return;
        }

        float k = Mathf.Min(1f, maxW / n.x, maxH / n.y);
        if (k < 1f)
            rt.sizeDelta = n * k;
        rt.localScale = Vector3.one;
    }

    IEnumerator FadeOutStoryProp()
    {
        var img = storyPropImage;
        if (img == null) yield break;
        float dur = 0.55f;
        float t = 0f;
        Color c = img.color;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            c.a = 1f - Mathf.Clamp01(t / dur);
            img.color = c;
            yield return null;
        }
        img.sprite = null;
        img.gameObject.SetActive(false);
        img.color = Color.white;
        _propFadeCo = null;
    }

    public void AutoBindFromHierarchy()
    {
        dialogueBoxImage = FindImg("DialogueBox");
        leftPortraitImage = FindImg("LeftPortrait");
        rightPortraitImage = FindImg("RightPortrait");
        // 名牌优先挂在对话框下；兼容旧预制体挂在根节点
        leftNamePlateImage = FindImg("DialogueBox/LeftNamePlate") ?? FindImg("LeftNamePlate");
        rightNamePlateImage = FindImg("DialogueBox/RightNamePlate") ?? FindImg("RightNamePlate");
        leftNameIcon = FindImg("DialogueBox/LeftNamePlate/Icon") ?? FindImg("LeftNamePlate/Icon");
        rightNameIcon = FindImg("DialogueBox/RightNamePlate/Icon") ?? FindImg("RightNamePlate/Icon");
        nextArrowImage = FindImg("DialogueBox/NextArrow");
        storyPropImage = FindImg("StoryProp");
        dialogueText = FindTxt("DialogueBox/DialogueText");
        leftNameText = FindTxt("DialogueBox/LeftNamePlate/NameText") ?? FindTxt("LeftNamePlate/NameText");
        rightNameText = FindTxt("DialogueBox/RightNamePlate/NameText") ?? FindTxt("RightNamePlate/NameText");
        // AdvanceClick：全屏近透明按钮，无选项时点屏幕任意处推进下一句
        advanceButton = transform.Find("AdvanceClick")?.GetComponent<Button>();
        skipButton = transform.Find("SkipButton")?.GetComponent<Button>();

        choiceButtonImages = new Image[MaxChoices];
        choiceButtons = new Button[MaxChoices];
        choiceTexts = new Text[MaxChoices];
        for (int i = 0; i < MaxChoices; i++)
        {
            string path = $"ChoicePanel/Choice_{i}";
            choiceButtonImages[i] = FindImg(path);
            choiceButtons[i] = transform.Find(path)?.GetComponent<Button>();
            choiceTexts[i] = FindTxt($"{path}/Label");
        }

        ApplyPortraitNativeSize(leftPortraitImage);
        ApplyPortraitNativeSize(rightPortraitImage);
    }

    Image FindImg(string path)
    {
        var t = transform.Find(path);
        return t != null ? t.GetComponent<Image>() : null;
    }

    Text FindTxt(string path)
    {
        var t = transform.Find(path);
        return t != null ? t.GetComponent<Text>() : null;
    }

    void EnsureStoryProp()
    {
        if (storyPropImage != null) return;
        var existing = transform.Find("StoryProp");
        if (existing != null)
        {
            storyPropImage = existing.GetComponent<Image>();
            if (storyPropImage != null) return;
        }

        var go = new GameObject("StoryProp", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 110f);
        rt.sizeDelta = new Vector2(360f, 360f);
        storyPropImage = go.GetComponent<Image>();
        storyPropImage.raycastTarget = false;
        storyPropImage.preserveAspect = true;
        storyPropImage.color = Color.white;
        storyPropImage.gameObject.SetActive(false);
        go.transform.SetSiblingIndex(4);
    }

    /// <summary>编辑器首次建树；已换资源的预制体勿覆盖</summary>
    public void BuildHierarchyForPrefab()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        var dim = CreateImage(transform, "Dim", new Color(0.18f, 0.1f, 0.08f, 1f));
        Stretch(dim.rectTransform);

        var skip = CreateImage(transform, "SkipButton", new Color(0.35f, 0.22f, 0.15f, 0.9f));
        SetAnchored(skip.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-28f, -36f), new Vector2(120f, 48f));
        var skipBtn = skip.gameObject.AddComponent<Button>();
        skipBtn.targetGraphic = skip;
        var skipTxt = CreateText(skip.transform, "Label", "跳过", 26, new Color(1f, 0.95f, 0.85f));
        Stretch(skipTxt.rectTransform);

        var choicePanel = CreateRect(transform, "ChoicePanel");
        SetAnchored(choicePanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 280f));
        for (int i = 0; i < MaxChoices; i++)
        {
            float y = 90f - i * 88f;
            var cimg = CreateImage(choicePanel.transform, "Choice_" + i, new Color(0.93f, 0.88f, 0.75f, 1f));
            SetAnchored(cimg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, y), new Vector2(480f, 72f));
            var cbtn = cimg.gameObject.AddComponent<Button>();
            cbtn.targetGraphic = cimg;
            var clabel = CreateText(cimg.transform, "Label", "选项 " + (i + 1), 28, new Color(0.28f, 0.16f, 0.1f));
            Stretch(clabel.rectTransform);
            cimg.gameObject.SetActive(false);
        }
        choicePanel.SetActive(false);

        // 立绘：底部对齐，尺寸用原图像素（换图后 SetNativeSize）
        var lp = CreateImage(transform, "LeftPortrait", new Color(0.35f, 0.45f, 0.4f, 0.85f));
        lp.preserveAspect = true;
        SetAnchored(lp.rectTransform, new Vector2(0.18f, 0f), new Vector2(0.18f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 280f), new Vector2(100f, 100f));
        var rp = CreateImage(transform, "RightPortrait", new Color(0.45f, 0.32f, 0.28f, 0.85f));
        rp.preserveAspect = true;
        SetAnchored(rp.rectTransform, new Vector2(0.82f, 0f), new Vector2(0.82f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 280f), new Vector2(100f, 100f));
        rp.rectTransform.localScale = new Vector3(-1f, 1f, 1f);

        // 对话框：随界面宽度左右拉伸（左右各留 20）
        var box = CreateImage(transform, "DialogueBox", new Color(0.93f, 0.88f, 0.75f, 1f));
        var boxRt = box.rectTransform;
        boxRt.anchorMin = new Vector2(0f, 0f);
        boxRt.anchorMax = new Vector2(1f, 0f);
        boxRt.pivot = new Vector2(0.5f, 0f);
        boxRt.anchoredPosition = new Vector2(0f, 40f);
        boxRt.sizeDelta = new Vector2(-40f, 260f);

        var body = CreateText(box.transform, "DialogueText",
            "在这里写对话正文……\n（点空白继续；有选项时点选项）", 28, new Color(0.22f, 0.14f, 0.1f));
        body.alignment = TextAnchor.UpperLeft;
        body.horizontalOverflow = HorizontalWrapMode.Wrap;
        body.verticalOverflow = VerticalWrapMode.Overflow;
        var bodyRt = body.rectTransform;
        bodyRt.anchorMin = new Vector2(0f, 0f);
        bodyRt.anchorMax = new Vector2(1f, 1f);
        bodyRt.offsetMin = new Vector2(36f, 36f);
        bodyRt.offsetMax = new Vector2(-36f, -28f);

        var arrow = CreateImage(box.transform, "NextArrow", new Color(0.35f, 0.55f, 0.85f, 1f));
        SetAnchored(arrow.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-28f, 20f), new Vector2(28f, 22f));

        // 名牌挂在对话框上沿，相对对话框固定
        BuildNamePlate(box.transform, "LeftNamePlate",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(24f, 0f),
            new Color(0.55f, 0.18f, 0.15f, 1f), "发起方", iconOnLeft: true);
        BuildNamePlate(box.transform, "RightNamePlate",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(-24f, 0f),
            new Color(0.2f, 0.28f, 0.55f, 1f), "对方", iconOnLeft: false);
        EnsureStoryProp();

        // AdvanceClick：全屏透明点击层，无选项时点任意处推进
        var click = CreateImage(transform, "AdvanceClick", new Color(0f, 0f, 0f, 0.01f));
        Stretch(click.rectTransform);
        var adv = click.gameObject.AddComponent<Button>();
        adv.targetGraphic = click;
        adv.transition = Selectable.Transition.None;

        dim.transform.SetAsFirstSibling();
        click.transform.SetSiblingIndex(1);
        lp.transform.SetSiblingIndex(2);
        rp.transform.SetSiblingIndex(3);
        if (storyPropImage != null) storyPropImage.transform.SetSiblingIndex(4);
        box.transform.SetSiblingIndex(5);
        choicePanel.transform.SetAsLastSibling();
        skip.transform.SetAsLastSibling();

        AutoBindFromHierarchy();
        FaceEachOther();
        GameFonts.ApplyToHierarchy(transform);
    }

    static void BuildNamePlate(
        Transform parent, string name,
        Vector2 amin, Vector2 amax, Vector2 pivot, Vector2 pos,
        Color plateColor, string defaultName, bool iconOnLeft)
    {
        var plate = CreateImage(parent, name, plateColor);
        SetAnchored(plate.rectTransform, amin, amax, pivot, pos, new Vector2(220f, 48f));
        var icon = CreateImage(plate.transform, "Icon", new Color(0.85f, 0.75f, 0.4f, 1f));
        if (iconOnLeft)
            SetAnchored(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(10f, 0f), new Vector2(32f, 32f));
        else
            SetAnchored(icon.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-10f, 0f), new Vector2(32f, 32f));
        var nm = CreateText(plate.transform, "NameText", defaultName, 12, new Color(1f, 0.95f, 0.85f));
        nm.alignment = iconOnLeft ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
        if (iconOnLeft)
            SetAnchored(nm.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(50f, 0f), new Vector2(-60f, 40f));
        else
            SetAnchored(nm.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-50f, 0f), new Vector2(-60f, 40f));
    }

    static GameObject CreateRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static Image CreateImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    static Text CreateText(Transform parent, string name, string content, int size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
        t.font = GameFonts.GetChinese();
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    static void SetAnchored(RectTransform rt, Vector2 amin, Vector2 amax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = amin;
        rt.anchorMax = amax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
