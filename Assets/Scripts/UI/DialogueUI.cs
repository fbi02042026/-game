using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 双人立绘对话框。资源请在预制体里直接替换 Sprite。
/// 参考图：Art/UI/Dialogue/dialogue_reference.png
/// </summary>
public class DialogueUI : MonoBehaviour
{
    public static DialogueUI Instance { get; private set; }

    [Header("可替换贴图")]
    public Image frameImage;
    public Image patternBgImage;
    public Image bannerLeftImage;
    public Image bannerRightImage;
    public Image leftPortraitImage;
    public Image rightPortraitImage;
    public Image dialogueBoxImage;
    public Image leftNamePlateImage;
    public Image rightNamePlateImage;
    public Image leftNameIcon;
    public Image rightNameIcon;
    public Image nextArrowImage;

    [Header("文本")]
    public Text dialogueText;
    public Text leftNameText;
    public Text rightNameText;

    [Header("交互")]
    public Button advanceButton;
    public UnityEvent onAdvance;

    Action _onAdvance;
    bool _wired;

    void Awake()
    {
        Instance = this;
        if (dialogueBoxImage == null)
            AutoBindFromHierarchy();
        WireClicks();
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
    }

    void OnClickAdvance()
    {
        onAdvance?.Invoke();
        _onAdvance?.Invoke();
    }

    /// <summary>显示对话；speakerSide: -1 左 / 0 旁白双灭 / 1 右</summary>
    public void Show(string leftName, string rightName, string content, int speakerSide = -1, Action onAdvance = null)
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        if (leftNameText != null) leftNameText.text = leftName ?? "";
        if (rightNameText != null) rightNameText.text = rightName ?? "";
        if (dialogueText != null) dialogueText.text = content ?? "";
        SetSpeakerHighlight(speakerSide);
        _onAdvance = onAdvance;
        if (nextArrowImage != null)
            nextArrowImage.gameObject.SetActive(true);
    }

    public void SetContent(string content)
    {
        if (dialogueText != null) dialogueText.text = content ?? "";
    }

    public void SetSpeakerHighlight(int speakerSide)
    {
        bool leftOn = speakerSide != 1;
        bool rightOn = speakerSide != -1;
        SetPlateActive(leftNamePlateImage, leftNameText, leftOn);
        SetPlateActive(rightNamePlateImage, rightNameText, rightOn);
        if (leftPortraitImage != null)
            leftPortraitImage.color = leftOn ? Color.white : new Color(0.55f, 0.55f, 0.55f, 1f);
        if (rightPortraitImage != null)
            rightPortraitImage.color = rightOn ? Color.white : new Color(0.55f, 0.55f, 0.55f, 1f);
    }

    static void SetPlateActive(Image plate, Text name, bool active)
    {
        if (plate != null)
            plate.color = active ? Color.white : new Color(1f, 1f, 1f, 0.55f);
        if (name != null)
            name.color = active ? new Color(1f, 0.95f, 0.85f) : new Color(0.7f, 0.65f, 0.55f, 0.7f);
    }

    public void Hide()
    {
        _onAdvance = null;
        gameObject.SetActive(false);
    }

    public void AutoBindFromHierarchy()
    {
        frameImage = FindImg("Frame");
        patternBgImage = FindImg("PatternBg");
        bannerLeftImage = FindImg("BannerLeft");
        bannerRightImage = FindImg("BannerRight");
        leftPortraitImage = FindImg("LeftPortrait");
        rightPortraitImage = FindImg("RightPortrait");
        dialogueBoxImage = FindImg("DialogueBox");
        leftNamePlateImage = FindImg("LeftNamePlate");
        rightNamePlateImage = FindImg("RightNamePlate");
        leftNameIcon = FindImg("LeftNamePlate/Icon");
        rightNameIcon = FindImg("RightNamePlate/Icon");
        nextArrowImage = FindImg("DialogueBox/NextArrow");
        dialogueText = FindTxt("DialogueBox/DialogueText");
        leftNameText = FindTxt("LeftNamePlate/NameText");
        rightNameText = FindTxt("RightNamePlate/NameText");
        advanceButton = transform.Find("AdvanceClick")?.GetComponent<Button>();
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

    /// <summary>编辑器首次建树；之后请直接在预制体里换图，勿覆盖用户资源</summary>
    public void BuildHierarchyForPrefab()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        // 外框（整窗装饰底）
        var frame = CreateImage(transform, "Frame", new Color(0.22f, 0.14f, 0.1f, 1f));
        Stretch(frame.rectTransform);
        frame.rectTransform.offsetMin = new Vector2(24f, 180f);
        frame.rectTransform.offsetMax = new Vector2(-24f, -80f);

        // 立绘区菱形底纹
        var pattern = CreateImage(transform, "PatternBg", new Color(0.18f, 0.12f, 0.1f, 1f));
        SetAnchored(pattern.rectTransform, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 40f), new Vector2(620f, 400f));

        // 角旗
        var bl = CreateImage(transform, "BannerLeft", new Color(0.75f, 0.15f, 0.12f, 1f));
        SetAnchored(bl.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(48f, -96f), new Vector2(48f, 72f));
        var br = CreateImage(transform, "BannerRight", new Color(0.75f, 0.15f, 0.12f, 1f));
        SetAnchored(br.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-48f, -96f), new Vector2(48f, 72f));

        // 左右立绘
        var lp = CreateImage(transform, "LeftPortrait", new Color(0.35f, 0.45f, 0.4f, 0.85f));
        SetAnchored(lp.rectTransform, new Vector2(0.28f, 0.52f), new Vector2(0.28f, 0.52f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(220f, 280f));
        var rp = CreateImage(transform, "RightPortrait", new Color(0.45f, 0.35f, 0.4f, 0.85f));
        SetAnchored(rp.rectTransform, new Vector2(0.72f, 0.52f), new Vector2(0.72f, 0.52f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(220f, 280f));

        // 对话框
        var box = CreateImage(transform, "DialogueBox", new Color(0.93f, 0.88f, 0.75f, 1f));
        SetAnchored(box.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 220f), new Vector2(600f, 200f));

        var body = CreateText(box.transform, "DialogueText",
            "在这里写对话正文……\n（点击任意处继续）", 26, new Color(0.22f, 0.14f, 0.1f));
        body.alignment = TextAnchor.UpperLeft;
        body.horizontalOverflow = HorizontalWrapMode.Wrap;
        body.verticalOverflow = VerticalWrapMode.Overflow;
        SetAnchored(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 8f), new Vector2(540f, 140f));

        var arrow = CreateImage(box.transform, "NextArrow", new Color(0.35f, 0.22f, 0.12f, 1f));
        SetAnchored(arrow.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-28f, 16f), new Vector2(28f, 22f));

        // 名牌（压在对话框上沿）
        BuildNamePlate(transform, "LeftNamePlate", new Vector2(0.22f, 0f), new Vector2(0f, 430f), "小美");
        BuildNamePlate(transform, "RightNamePlate", new Vector2(0.78f, 0f), new Vector2(0f, 430f), "玩家");

        // 全屏点击继续（最上层）
        var click = CreateImage(transform, "AdvanceClick", new Color(0f, 0f, 0f, 0.01f));
        Stretch(click.rectTransform);
        var btn = click.gameObject.AddComponent<Button>();
        btn.targetGraphic = click;
        btn.transition = Selectable.Transition.None;

        AutoBindFromHierarchy();
        GameFonts.ApplyToHierarchy(transform);
    }

    static void BuildNamePlate(Transform parent, string name, Vector2 anchor, Vector2 pos, string defaultName)
    {
        var plate = CreateImage(parent, name, new Color(0.28f, 0.18f, 0.12f, 1f));
        SetAnchored(plate.rectTransform, anchor, anchor, new Vector2(0.5f, 0.5f),
            pos, new Vector2(200f, 44f));
        var icon = CreateImage(plate.transform, "Icon", new Color(0.55f, 0.55f, 0.55f, 1f));
        SetAnchored(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(8f, 0f), new Vector2(28f, 28f));
        var nm = CreateText(plate.transform, "NameText", defaultName, 22, new Color(1f, 0.95f, 0.85f));
        nm.alignment = TextAnchor.MiddleLeft;
        SetAnchored(nm.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(44f, 0f), new Vector2(-52f, 36f));
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
