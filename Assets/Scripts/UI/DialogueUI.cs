using System;
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

    [Header("可替换贴图")]
    public Image dialogueBoxImage;
    public Image leftPortraitImage;
    public Image rightPortraitImage;
    public Image leftNamePlateImage;
    public Image rightNamePlateImage;
    public Image leftNameIcon;
    public Image rightNameIcon;
    public Image nextArrowImage;
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

    Action _onAdvance;
    Action _onSkip;
    Action<int> _onChoice;
    bool _wired;
    bool _choicesVisible;
    string _initiatorName;
    string _otherName;

    void Awake()
    {
        Instance = this;
        if (dialogueBoxImage == null)
            AutoBindFromHierarchy();
        EnsureAdaptiveLayout();
        WireClicks();
        HideChoices();
    }

    /// <summary>
    /// 对话框随界面宽度拉伸；我方/对方名牌挂在对话框上沿固定；立绘保留自身像素尺寸。
    /// 不覆盖已绑定的 Sprite，只改布局。
    /// </summary>
    public void EnsureAdaptiveLayout()
    {
        if (dialogueBoxImage != null)
        {
            var boxRt = dialogueBoxImage.rectTransform;
            boxRt.anchorMin = new Vector2(0f, 0f);
            boxRt.anchorMax = new Vector2(1f, 0f);
            boxRt.pivot = new Vector2(0.5f, 0f);
            float y = boxRt.anchoredPosition.y;
            if (Mathf.Abs(y) < 1f) y = 40f;
            boxRt.anchoredPosition = new Vector2(0f, y);
            float h = boxRt.sizeDelta.y;
            if (h < 80f) h = 260f;
            boxRt.sizeDelta = new Vector2(-40f, h); // 左右各留 20
        }

        if (dialogueText != null)
        {
            var bodyRt = dialogueText.rectTransform;
            bodyRt.anchorMin = Vector2.zero;
            bodyRt.anchorMax = Vector2.one;
            bodyRt.pivot = new Vector2(0.5f, 0.5f);
            bodyRt.offsetMin = new Vector2(36f, 36f);
            bodyRt.offsetMax = new Vector2(-36f, -28f);
            dialogueText.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        PinNamePlateToBox(leftNamePlateImage, left: true);
        PinNamePlateToBox(rightNamePlateImage, left: false);

        if (leftPortraitImage != null) leftPortraitImage.preserveAspect = true;
        if (rightPortraitImage != null) rightPortraitImage.preserveAspect = true;
        ApplyPortraitNativeSize(leftPortraitImage);
        ApplyPortraitNativeSize(rightPortraitImage);
    }

    void PinNamePlateToBox(Image plate, bool left)
    {
        if (plate == null || dialogueBoxImage == null) return;
        var box = dialogueBoxImage.transform;
        if (plate.transform.parent != box)
            plate.transform.SetParent(box, false);

        var rt = plate.rectTransform;
        if (left)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(24f, 0f);
        }
        else
        {
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-24f, 0f);
        }
        // 保留用户在预制体里调过的名牌宽高
        if (rt.sizeDelta.x < 40f || rt.sizeDelta.y < 20f)
            rt.sizeDelta = new Vector2(220f, 70f);
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
        if (_choicesVisible) return; // 有选项时不靠点空白继续
        onAdvance?.Invoke();
        _onAdvance?.Invoke();
    }

    void OnClickSkip()
    {
        onSkip?.Invoke();
        _onSkip?.Invoke();
    }

    void OnClickChoice(int index)
    {
        onChoiceSelected?.Invoke(index);
        _onChoice?.Invoke(index);
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
        bool speakerIsInitiator = true,
        Action onAdvance = null,
        Action onSkip = null)
    {
        _initiatorName = initiatorName ?? "";
        _otherName = otherName ?? "";
        _onAdvance = onAdvance;
        _onSkip = onSkip;
        _onChoice = null;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (leftNameText != null) leftNameText.text = _initiatorName;
        if (rightNameText != null) rightNameText.text = _otherName;
        if (dialogueText != null) dialogueText.text = content ?? "";

        if (leftPortraitImage != null)
        {
            if (initiatorPortrait != null) leftPortraitImage.sprite = initiatorPortrait;
            ApplyPortraitNativeSize(leftPortraitImage);
            leftPortraitImage.gameObject.SetActive(leftPortraitImage.sprite != null);
        }
        if (rightPortraitImage != null)
        {
            if (otherPortrait != null) rightPortraitImage.sprite = otherPortrait;
            ApplyPortraitNativeSize(rightPortraitImage);
            rightPortraitImage.gameObject.SetActive(rightPortraitImage.sprite != null);
        }

        ApplyFacing(leftFacingRight: true, rightFacingRight: false);
        SetSpeakerHighlight(speakerIsInitiator ? -1 : 1);
        HideChoices();
        SetAdvanceInteractable(true);
        if (nextArrowImage != null)
            nextArrowImage.gameObject.SetActive(true);
        if (skipButton != null)
            skipButton.gameObject.SetActive(true);
    }

    /// <summary>兼容旧调用；speakerSide: -1 左(发起方) / 0 旁白 / 1 右(对方)</summary>
    public void Show(string leftName, string rightName, string content, int speakerSide = -1, Action onAdvance = null)
    {
        ShowLine(leftName, rightName, content, null, null, speakerIsInitiator: speakerSide != 1, onAdvance: onAdvance);
        if (speakerSide == 0)
            SetSpeakerHighlight(0);
    }

    public void SetContent(string content, bool speakerIsInitiator = true)
    {
        if (dialogueText != null) dialogueText.text = content ?? "";
        SetSpeakerHighlight(speakerIsInitiator ? -1 : 1);
        HideChoices();
        SetAdvanceInteractable(true);
        if (nextArrowImage != null)
            nextArrowImage.gameObject.SetActive(true);
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
        img.rectTransform.localScale = s;
    }

    /// <summary>立绘用 Sprite 原始像素尺寸，不统一成固定框</summary>
    public static void ApplyPortraitNativeSize(Image img)
    {
        if (img == null) return;
        img.preserveAspect = true;
        if (img.sprite == null)
        {
            img.enabled = false;
            return;
        }
        img.enabled = true;
        img.SetNativeSize();
    }

    public void SetSpeakerHighlight(int speakerSide)
    {
        // -1 左说话 / 1 右说话 / 0 旁白两边正常
        bool leftSpeak = speakerSide == -1 || speakerSide == 0;
        bool rightSpeak = speakerSide == 1 || speakerSide == 0;
        SetPlateActive(leftNamePlateImage, leftNameText, leftSpeak || speakerSide == 0);
        SetPlateActive(rightNamePlateImage, rightNameText, rightSpeak || speakerSide == 0);

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
            name.color = active ? new Color(1f, 0.95f, 0.85f) : new Color(0.7f, 0.65f, 0.55f, 0.7f);
    }

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
        HideChoices();
        gameObject.SetActive(false);
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
        box.transform.SetSiblingIndex(4);
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
        var nm = CreateText(plate.transform, "NameText", defaultName, 24, new Color(1f, 0.95f, 0.85f));
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
