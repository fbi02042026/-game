using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 切场景 Loading（Resources/Prefabs/Loading/LoadingUI）。
/// 布局以用户预制体为准；运行时只微调 StoryTip 纵向位置，不改 prefab。
/// 跨场景时用 Overlay + 高 sortingOrder，避免 Camera 随场景销毁导致黑屏。
/// </summary>
public class LoadingUI : MonoBehaviour
{
    public const string ResourcePath = "Prefabs/Loading/LoadingUI";

    bool _storyTipOffsetApplied;
    bool _progressCornerAdjusted;

    /// <summary>相对预制体 StoryTip 再下移（不写回 prefab）。</summary>
    const float StoryTipDownOffset = -32f;

    [Header("绑定")]
    public Image backgroundImage;
    public Text tipText;
    public Text labelText;
    public Text percentText;

    public void PrepareOverlay()
    {
        if (backgroundImage == null || tipText == null)
            AutoBind();

        if (backgroundImage != null)
            UiLayoutStretch.ApplyBgStretch(backgroundImage.rectTransform, backgroundImage);

        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.enabled = true;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 9999;

        var scaler = GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.referenceResolution = new Vector2(GameConfig.DESIGN_WIDTH, GameConfig.DESIGN_HEIGHT);
        scaler.matchWidthOrHeight = GameConfig.UI_MATCH;

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        if (transform.localScale.sqrMagnitude < 0.0001f)
            transform.localScale = Vector3.one;

        GameFonts.ApplyToHierarchy(transform);
        if (labelText != null)
            labelText.font = GameFonts.GetChinese();
        if (percentText != null)
            percentText.font = GameFonts.GetNumber();
        if (tipText != null)
            tipText.font = GameFonts.GetChinese();

        ApplyStoryTipOffset();
        ApplyProgressCornerLayout();
    }

    /// <summary>「加载中」与百分比间距（不改 prefab）。</summary>
    void ApplyProgressCornerLayout()
    {
        if (_progressCornerAdjusted) return;
        if (labelText == null || percentText == null) return;
        var labelRt = labelText.rectTransform;
        var pctRt = percentText.rectTransform;
        if (labelRt == null || pctRt == null) return;

        labelRt.anchorMin = new Vector2(0f, 0f);
        labelRt.anchorMax = new Vector2(0f, 1f);
        labelRt.pivot = new Vector2(0f, 0.5f);
        labelRt.anchoredPosition = Vector2.zero;

        pctRt.anchorMin = new Vector2(0f, 0f);
        pctRt.anchorMax = new Vector2(0f, 1f);
        pctRt.pivot = new Vector2(0f, 0.5f);
        pctRt.anchoredPosition = new Vector2(labelRt.sizeDelta.x + 4f, 0f);

        _progressCornerAdjusted = true;
    }

    void ApplyStoryTipOffset()
    {
        if (_storyTipOffsetApplied || tipText == null) return;
        var rt = tipText.rectTransform;
        if (rt == null) return;
        rt.anchoredPosition += new Vector2(0f, StoryTipDownOffset);
        _storyTipOffsetApplied = true;
    }

    public void SetProgress(float progress01)
    {
        float p = Mathf.Clamp01(progress01);
        if (percentText != null)
            percentText.text = Mathf.RoundToInt(p * 100f) + "%";
    }

    public void SetTip(string tip)
    {
        if (tipText != null && !string.IsNullOrEmpty(tip))
            tipText.text = tip;
    }

    public string CurrentTip => tipText != null ? tipText.text : "";

    public void AutoBind()
    {
        if (backgroundImage == null)
            backgroundImage = transform.Find("Bg")?.GetComponent<Image>();
        if (tipText == null)
            tipText = transform.Find("StoryTip")?.GetComponent<Text>();
        if (labelText == null)
            labelText = transform.Find("ProgressCorner/Label")?.GetComponent<Text>();
        if (percentText == null)
            percentText = transform.Find("ProgressCorner/Percent")?.GetComponent<Text>();
    }

    /// <summary>编辑器首次建树；已换美术勿覆盖</summary>
    public void BuildHierarchyForPrefab()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        var bg = CreateImg(transform, "Bg", Color.white);
        Stretch(bg.rectTransform);
        bg.preserveAspect = false;
        Sprite sp = Resources.Load<Sprite>("UI/loading/loading01");
        if (sp != null) bg.sprite = sp;
        else bg.color = new Color(0.05f, 0.05f, 0.12f, 1f);

        var tip = CreateTxt(transform, "StoryTip", "加载中…", 26, new Color(1f, 0.96f, 0.88f, 0.95f),
            TextAnchor.MiddleCenter, GameFonts.GetChinese());
        tip.horizontalOverflow = HorizontalWrapMode.Wrap;
        tip.verticalOverflow = VerticalWrapMode.Overflow;
        var tipRt = tip.rectTransform;
        tipRt.anchorMin = new Vector2(0.5f, 0.19f);
        tipRt.anchorMax = new Vector2(0.5f, 0.19f);
        tipRt.pivot = new Vector2(0.5f, 0.5f);
        tipRt.anchoredPosition = Vector2.zero;
        tipRt.sizeDelta = new Vector2(640f, 120f);
        var tipOutline = tip.gameObject.AddComponent<Outline>();
        tipOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        tipOutline.effectDistance = new Vector2(1.5f, -1.5f);

        var corner = new GameObject("ProgressCorner", typeof(RectTransform));
        corner.transform.SetParent(transform, false);
        var cornerRt = corner.GetComponent<RectTransform>();
        cornerRt.anchorMin = new Vector2(1f, 0f);
        cornerRt.anchorMax = new Vector2(1f, 0f);
        cornerRt.pivot = new Vector2(1f, 0f);
        cornerRt.anchoredPosition = new Vector2(-36f, 48f);
        cornerRt.sizeDelta = new Vector2(160f, 40f);

        var label = CreateTxt(corner.transform, "Label", "加载中", 24, Color.white,
            TextAnchor.MiddleRight, GameFonts.GetChinese());
        var labelRt = label.rectTransform;
        labelRt.anchorMin = new Vector2(0f, 0f);
        labelRt.anchorMax = new Vector2(0f, 1f);
        labelRt.pivot = new Vector2(0f, 0.5f);
        labelRt.sizeDelta = new Vector2(72f, 0f);
        labelRt.anchoredPosition = Vector2.zero;

        var pct = CreateTxt(corner.transform, "Percent", "0%", 26, Color.white,
            TextAnchor.MiddleLeft, GameFonts.GetNumber());
        var pctRt = pct.rectTransform;
        pctRt.anchorMin = new Vector2(0f, 0f);
        pctRt.anchorMax = new Vector2(0f, 1f);
        pctRt.pivot = new Vector2(0f, 0.5f);
        pctRt.sizeDelta = new Vector2(64f, 0f);
        pctRt.anchoredPosition = new Vector2(80f, 0f);

        AutoBind();
        GameFonts.ApplyToHierarchy(transform);
    }

    static Image CreateImg(Transform p, string n, Color c)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(p, false);
        var img = go.GetComponent<Image>();
        img.color = c;
        return img;
    }

    static Text CreateTxt(Transform p, string n, string t, int size, Color c, TextAnchor align, Font font)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(p, false);
        var tx = go.GetComponent<Text>();
        tx.text = t;
        tx.fontSize = size;
        tx.color = c;
        tx.alignment = align;
        tx.raycastTarget = false;
        tx.horizontalOverflow = HorizontalWrapMode.Overflow;
        tx.verticalOverflow = VerticalWrapMode.Overflow;
        tx.font = font != null ? font : GameFonts.GetChinese();
        return tx;
    }

    static void Stretch(RectTransform rt) => UiLayoutStretch.ApplyFillScreen(rt);
}
