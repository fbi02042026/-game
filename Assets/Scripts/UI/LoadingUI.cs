using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 切场景 Loading（Resources/Prefabs/Loading/LoadingUI）。
/// 布局与原运行时手搓一致：全屏背景、中下部剧情提示、右下角「加载中」+ 百分比。
/// 跨场景时用 Overlay + 高 sortingOrder，避免 Camera 随场景销毁导致黑屏。
/// </summary>
public class LoadingUI : MonoBehaviour
{
    public const string ResourcePath = "Prefabs/Loading/LoadingUI";

    [Header("绑定")]
    public Image backgroundImage;
    public Text tipText;
    public Text labelText;
    public Text percentText;

    RectTransform _logoRt;
    Vector2 _logoDesignSize;
    bool _logoSizeCaptured;
    bool _progressLayoutReady;

    public void PrepareOverlay()
    {
        if (backgroundImage == null || tipText == null)
            AutoBind();

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

        ApplyResponsiveLayout();
    }

    void OnEnable()
    {
        ApplyResponsiveLayout();
    }

    void OnRectTransformDimensionsChange()
    {
        ApplyResponsiveLayout();
    }

    void ApplyResponsiveLayout()
    {
        var rootRt = transform as RectTransform;
        if (rootRt == null) return;
        float canvasW = rootRt.rect.width;
        if (canvasW < 8f) return;

        ApplyTipLayout(canvasW);
        ApplyLogoLayout(canvasW);
        ApplyProgressLayout();
    }

    /// <summary>
    /// 剧情提示保持屏幕水平居中、中下部相对位置；宽度随画布收窄。
    /// </summary>
    void ApplyTipLayout(float canvasW)
    {
        if (tipText == null)
            tipText = transform.Find("StoryTip")?.GetComponent<Text>();
        if (tipText == null) return;

        const float sidePad = 40f;
        const float maxWidth = 640f;
        const float tipHeight = 160f;
        const float fromBottom = 0.36f;

        tipText.alignment = TextAnchor.MiddleCenter;
        tipText.horizontalOverflow = HorizontalWrapMode.Wrap;
        tipText.verticalOverflow = VerticalWrapMode.Truncate;

        var tipRt = tipText.rectTransform;
        tipRt.anchorMin = new Vector2(0.5f, fromBottom);
        tipRt.anchorMax = new Vector2(0.5f, fromBottom);
        tipRt.pivot = new Vector2(0.5f, 0.5f);
        tipRt.sizeDelta = new Vector2(Mathf.Min(maxWidth, canvasW - sidePad * 2f), tipHeight);
        tipRt.anchoredPosition = Vector2.zero;
    }

    /// <summary>
    /// Logo 按设计尺寸随画布宽度缩放，固定左上角边距。
    /// </summary>
    void ApplyLogoLayout(float canvasW)
    {
        if (_logoRt == null)
        {
            var t = transform.Find("logo");
            if (t == null) t = transform.Find("Logo");
            _logoRt = t as RectTransform;
        }
        if (_logoRt == null) return;

        if (!_logoSizeCaptured)
        {
            _logoSizeCaptured = true;
            var sc = _logoRt.localScale;
            _logoDesignSize = new Vector2(
                Mathf.Abs(_logoRt.sizeDelta.x * sc.x),
                Mathf.Abs(_logoRt.sizeDelta.y * sc.y));
            if (_logoDesignSize.x < 8f || _logoDesignSize.y < 8f)
                _logoDesignSize = new Vector2(128f, 112f);
        }

        float k = Mathf.Clamp(canvasW / GameConfig.DESIGN_WIDTH, 0.72f, 1.2f);
        var img = _logoRt.GetComponent<Image>();
        if (img != null) img.preserveAspect = true;

        _logoRt.localScale = Vector3.one;
        _logoRt.anchorMin = new Vector2(0f, 1f);
        _logoRt.anchorMax = new Vector2(0f, 1f);
        _logoRt.pivot = new Vector2(0f, 1f);
        _logoRt.sizeDelta = _logoDesignSize * k;
        _logoRt.anchoredPosition = new Vector2(24f * k, -24f * k);
    }

    /// <summary>
    /// 「加载中」与百分比按实际文字宽度紧挨排列，右下角对齐。
    /// </summary>
    void ApplyProgressLayout()
    {
        if (labelText == null)
            labelText = transform.Find("ProgressCorner/Label")?.GetComponent<Text>();
        if (percentText == null)
            percentText = transform.Find("ProgressCorner/Percent")?.GetComponent<Text>();
        if (labelText == null || percentText == null) return;

        var corner = labelText.transform.parent as RectTransform;
        if (corner == null) return;

        if (!_progressLayoutReady)
        {
            _progressLayoutReady = true;
            labelText.alignment = TextAnchor.MiddleRight;
            labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
            labelText.verticalOverflow = VerticalWrapMode.Overflow;
            percentText.alignment = TextAnchor.MiddleLeft;
            percentText.horizontalOverflow = HorizontalWrapMode.Overflow;
            percentText.verticalOverflow = VerticalWrapMode.Overflow;
        }

        const float gap = 8f;
        const float height = 40f;
        float labelW = Mathf.Max(8f, labelText.preferredWidth);
        float pctW = Mathf.Max(8f, percentText.preferredWidth);
        float total = labelW + gap + pctW;

        corner.anchorMin = new Vector2(1f, 0f);
        corner.anchorMax = new Vector2(1f, 0f);
        corner.pivot = new Vector2(1f, 0f);
        corner.sizeDelta = new Vector2(total, height);
        corner.anchoredPosition = new Vector2(-36f, 28f);

        var labelRt = labelText.rectTransform;
        labelRt.anchorMin = new Vector2(0f, 0f);
        labelRt.anchorMax = new Vector2(0f, 1f);
        labelRt.pivot = new Vector2(0f, 0.5f);
        labelRt.sizeDelta = new Vector2(labelW, 0f);
        labelRt.anchoredPosition = Vector2.zero;

        var pctRt = percentText.rectTransform;
        pctRt.anchorMin = new Vector2(0f, 0f);
        pctRt.anchorMax = new Vector2(0f, 1f);
        pctRt.pivot = new Vector2(0f, 0.5f);
        pctRt.sizeDelta = new Vector2(pctW, 0f);
        pctRt.anchoredPosition = new Vector2(labelW + gap, 0f);
    }

    public void SetProgress(float progress01)
    {
        float p = Mathf.Clamp01(progress01);
        if (percentText != null)
            percentText.text = Mathf.RoundToInt(p * 100f) + "%";
        ApplyProgressLayout();
    }

    public void SetTip(string tip)
    {
        if (tipText != null && !string.IsNullOrEmpty(tip))
            tipText.text = tip;
        ApplyResponsiveLayout();
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
        tipRt.anchorMin = new Vector2(0.5f, 0.22f);
        tipRt.anchorMax = new Vector2(0.5f, 0.22f);
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

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
