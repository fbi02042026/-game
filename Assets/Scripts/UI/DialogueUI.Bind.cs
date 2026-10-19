using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 剧情对话窗 partial —— 2026-10-18 由 DialogueUI.cs 纯位移拆出（无逻辑改动）
// 职责：绑定与建树（AutoBindFromHierarchy、编辑器 BuildHierarchyForPrefab、Find/Create 小件）
public partial class DialogueUI
{

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

    // ---------- 自动播放（AUTO）按钮 ----------
    // 【2026-10-19 主人拍板】剧情自动播放：黑底白字圆角，放右上「跳过」左边。
    // 开启时底色变亮绿做明显高亮；状态记在 StoryDirector.AutoPlay（会话内记住）。

    /// <summary>创建/复用 AUTO 按钮（幂等，Awake 与编辑器建树都可调）。</summary>
    void EnsureAutoButton()
    {
        if (_autoButton != null) return;
        var existing = transform.Find("AutoButton");
        if (existing != null)
        {
            _autoButton = existing.GetComponent<Button>();
            _autoImage = existing.GetComponent<Image>();
            _autoLabel = existing.GetComponentInChildren<Text>();
            if (_autoButton != null) return;
        }

        var go = new GameObject("AutoButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        // 位置以 SkipButton 实际口径为基准：贴其左侧留 12px，同锚系同高 —— 不写死坐标，prefab 怎么手调都跟得住
        var srt = skipButton != null ? skipButton.GetComponent<RectTransform>() : null;
        if (srt != null)
        {
            rt.anchorMin = srt.anchorMin;
            rt.anchorMax = srt.anchorMax;
            rt.pivot = srt.pivot;
            Vector2 size = srt.sizeDelta;
            size.x *= 0.92f;
            rt.sizeDelta = size;
            rt.anchoredPosition = srt.anchoredPosition
                + new Vector2(-((size.x + srt.sizeDelta.x) * 0.5f + 12f), 0f);
        }
        else
        {
            SetAnchored(rt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-160f, -36f), new Vector2(110f, 48f));
        }
        _autoImage = go.GetComponent<Image>();
        _autoImage.sprite = RoundedButtonSprite();
        _autoImage.type = Image.Type.Sliced;
        _autoButton = go.AddComponent<Button>();
        _autoButton.targetGraphic = _autoImage;
        _autoButton.transition = Selectable.Transition.ColorTint;
        var label = CreateText(go.transform, "Label", "AUTO", 24, Color.white);
        Stretch(label.rectTransform);
        _autoLabel = label;
        go.transform.SetAsLastSibling();
        // 这里也绑一次（幂等）：BuildHierarchyForPrefab 重建路径不会重跑 WireClicks（有 _wired 守卫）
        _autoButton.onClick.RemoveListener(ToggleAuto);
        _autoButton.onClick.AddListener(ToggleAuto);
        RefreshAutoButton();
    }

    /// <summary>按当前 AutoPlay 状态刷新按钮外观：关=黑底白字，开=亮绿高亮。</summary>
    void RefreshAutoButton()
    {
        if (_autoImage != null)
            _autoImage.color = StoryDirector.AutoPlay
                ? new Color(0.13f, 0.55f, 0.30f, 0.95f)
                : new Color(0f, 0f, 0f, 0.85f);
        if (_autoLabel != null)
        {
            _autoLabel.text = StoryDirector.AutoPlay ? "AUTO·开" : "AUTO";
            _autoLabel.color = Color.white;
        }
    }

    void ToggleAuto()
    {
        StoryDirector.AutoPlay = !StoryDirector.AutoPlay;
        RefreshAutoButton();
        Debug.Log("[DialogueUI] 剧情自动播放 = " + StoryDirector.AutoPlay);
    }

    /// <summary>
    /// 程序生成的圆角矩形（白 alpha 蒙版 + 九宫格 border），配纯色 Image 就是黑底圆角按钮；
    /// 静态缓存一份，所有 DialogueUI 实例共用。
    /// </summary>
    static Sprite _roundedBtnSprite;
    static Sprite RoundedButtonSprite()
    {
        if (_roundedBtnSprite != null) return _roundedBtnSprite;
        const int size = 64;
        const float radius = 18f;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Min(x, size - 1 - x);
                float dy = Mathf.Min(y, size - 1 - y);
                float a;
                if (dx >= radius || dy >= radius)
                    a = 1f;
                else
                {
                    float ex = radius - dx;
                    float ey = radius - dy;
                    a = Mathf.Clamp01(radius - Mathf.Sqrt(ex * ex + ey * ey));
                }
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        _roundedBtnSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        return _roundedBtnSprite;
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
        // 上面的清空重建会把 Awake 里建好的 AUTO 按钮删掉，这里补建回来（_autoButton 已是假 null，会重走创建分支）
        EnsureAutoButton();
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
        var nm = CreateText(plate.transform, "NameText", defaultName, 18, new Color(1f, 0.95f, 0.85f));
        nm.alignment = TextAnchor.MiddleCenter;
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
