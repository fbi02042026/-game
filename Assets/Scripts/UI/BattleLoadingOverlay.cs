using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 切 Battle/Town 场景时的 Loading：
/// 全屏背景 + 屏幕中下部剧情提示 + 右下角「加载中」百分比。
/// </summary>
public static class BattleLoadingOverlay
{
    const string BgResourcePath = "UI/loading/loading01";
    const string BgAssetPath = "Assets/Art/UI/loading/loading01.jpg";

    static GameObject _root;
    static Text _percentText;
    static Text _labelText;
    static Text _tipText;

    public static void Show(string tip = null)
    {
        Hide();
        _root = new GameObject("BattleLoadingOverlay", typeof(RectTransform));
        Object.DontDestroyOnLoad(_root);

        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        _root.AddComponent<GraphicRaycaster>();

        var scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(GameConfig.DESIGN_WIDTH, GameConfig.DESIGN_HEIGHT);
        scaler.matchWidthOrHeight = GameConfig.UI_MATCH;

        // —— 全屏背景 ——
        var bgGo = new GameObject("Bg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bgGo.transform.SetParent(_root.transform, false);
        Stretch(bgGo.GetComponent<RectTransform>());
        var bgImg = bgGo.GetComponent<Image>();
        bgImg.color = Color.white;
        bgImg.preserveAspect = false;
        Sprite bg = LoadBgSprite();
        if (bg != null)
            bgImg.sprite = bg;
        else
            bgImg.color = new Color(0.05f, 0.05f, 0.12f, 1f);

        // —— 屏幕中下部：剧情提示 ——
        string tipMsg = string.IsNullOrEmpty(tip) ? "加载中…" : tip;
        _tipText = CreateText(_root.transform, "StoryTip", tipMsg, 26, TextAnchor.MiddleCenter,
            GameFonts.GetChinese());
        _tipText.color = new Color(1f, 0.96f, 0.88f, 0.95f);
        var tipRt = _tipText.rectTransform;
        tipRt.anchorMin = new Vector2(0.5f, 0f);
        tipRt.anchorMax = new Vector2(0.5f, 0f);
        tipRt.pivot = new Vector2(0.5f, 0.5f);
        tipRt.anchoredPosition = new Vector2(0f, 280f);
        tipRt.sizeDelta = new Vector2(640f, 100f);
        _tipText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _tipText.verticalOverflow = VerticalWrapMode.Overflow;
        var tipOutline = _tipText.gameObject.AddComponent<Outline>();
        tipOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        tipOutline.effectDistance = new Vector2(1.5f, -1.5f);

        // —— 右下角：加载中 + 百分比 ——
        var corner = new GameObject("ProgressCorner", typeof(RectTransform));
        corner.transform.SetParent(_root.transform, false);
        var cornerRt = corner.GetComponent<RectTransform>();
        cornerRt.anchorMin = new Vector2(1f, 0f);
        cornerRt.anchorMax = new Vector2(1f, 0f);
        cornerRt.pivot = new Vector2(1f, 0f);
        cornerRt.anchoredPosition = new Vector2(-36f, 48f);
        cornerRt.sizeDelta = new Vector2(280f, 40f);

        _labelText = CreateText(corner.transform, "Label", "加载中", 24, TextAnchor.MiddleLeft,
            GameFonts.GetChinese());
        var labelRt = _labelText.rectTransform;
        labelRt.anchorMin = new Vector2(0f, 0f);
        labelRt.anchorMax = new Vector2(0.55f, 1f);
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;

        _percentText = CreateText(corner.transform, "Percent", "0%", 26, TextAnchor.MiddleRight,
            GameFonts.GetNumber());
        var pctRt = _percentText.rectTransform;
        pctRt.anchorMin = new Vector2(0.55f, 0f);
        pctRt.anchorMax = new Vector2(1f, 1f);
        pctRt.offsetMin = Vector2.zero;
        pctRt.offsetMax = Vector2.zero;

        SetProgress(0f);
        GameFonts.ApplyToHierarchy(_root.transform);
    }

    /// <summary>0~1 进度</summary>
    public static void SetProgress(float progress01)
    {
        float p = Mathf.Clamp01(progress01);
        if (_percentText != null) _percentText.text = Mathf.RoundToInt(p * 100f) + "%";
        if (_labelText != null) _labelText.text = "加载中";
    }

    public static void SetTip(string tip)
    {
        if (_tipText != null && !string.IsNullOrEmpty(tip))
            _tipText.text = tip;
    }

    public static bool IsShowing => _root != null;

    public static void Hide()
    {
        if (_root != null)
        {
            Object.Destroy(_root);
            _root = null;
        }
        _percentText = null;
        _labelText = null;
        _tipText = null;
    }

    static Sprite LoadBgSprite()
    {
        Sprite sp = Resources.Load<Sprite>(BgResourcePath);
        if (sp != null) return sp;

        Texture2D tex = Resources.Load<Texture2D>(BgResourcePath);
#if UNITY_EDITOR
        if (tex == null)
            tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(BgAssetPath);
        if (sp == null)
            sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(BgAssetPath);
        if (sp != null) return sp;
#endif
        if (tex == null) return null;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }

    static Text CreateText(Transform parent, string name, string content, int size, TextAnchor align, Font font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.font = font != null ? font : GameFonts.GetChinese();
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
