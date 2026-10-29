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
public partial class DialogueUI : MonoBehaviour
{
    public const int MaxChoices = 3;

    public static DialogueUI Instance { get; private set; }

    public bool IsVisible => gameObject.activeSelf;
    public string CurrentLine => dialogueText != null ? dialogueText.text : "";
    public bool HasChoicesVisible => _choicesVisible;
    /// <summary>打字机是否已播完当前句（情报问答等要等结束后再出选项）。</summary>
    public bool IsTypeComplete => _typeComplete;

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

    // —— 自动播放（AUTO）按钮：代码建，预制体里没有（2026-10-19 主人拍板加剧情自动播放）——
    Button _autoButton;
    Image _autoImage;
    Text _autoLabel;

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
    /// <summary>对话正文手动回看用的滚动容器（打字播完且文本溢出时才启用）。</summary>
    ScrollRect _dialogueScrollRect;
    string _initiatorName;
    string _otherName;
    StoryPortraitPresenter.LayoutSnapshot _leftPortraitLayout;
    StoryPortraitPresenter.LayoutSnapshot _rightPortraitLayout;
    RtLayout _leftPlateLayout;
    RtLayout _rightPlateLayout;
    /// <summary>对话框体原始尺寸（预制体口径），放大 5% 时以此为基准，避免每句台词重复累乘。</summary>
    Vector2 _dialogueBoxBaseSize;

    struct RtLayout
    {
        public Vector2 amin, amax, pivot, pos;
        public Vector3 scale;
    }

    /// <summary>剧情对白正文字号。2026-10-10 主人拍板：字太小看费劲，28→32；
    /// 【2026-10-16 主人「剧情对话框里的字小1号」】32→31。
    /// 【2026-10-29 主人拍板「方案A·全对齐」】31→24：fusion-pixel 是 12px 点阵字体，
    /// 字号取 12 的倍数才能整像素对齐（24 在 720p/1080p/2K/3K 屏全部锐利），31 等非整倍发虚。
    /// 名牌/选项/跳过（预制体 m_FontSize）同日一并 26/32/28→24。要再调改这一个常量。</summary>
    const int DialogueBodyFontSize = 24;
    const float LocBlackInDur = 0.55f;
    const float LocHoldDur = 2.0f;
    const float LocTextFadeDur = 1.1f;
    const float LocBgFadeDur = 1.15f;
    const float LocDimDur = 0.85f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
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
        EnsureAutoButton();
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
}
