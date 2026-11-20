using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 切场景 Loading（Resources/Prefabs/Loading/LoadingUI）。
/// 布局以用户手改预制体为准；运行时不改坐标/锚点，只设 Canvas/字体并写百分比文案。
/// 跨场景时绑定持久 UI 相机，避免 Camera 随场景销毁导致黑屏。
/// </summary>
public class LoadingUI : MonoBehaviour
{
    public const string ResourcePath = "Prefabs/Loading/LoadingUI";

    [Header("绑定")]
    public Image backgroundImage;
    public Text tipText;
    public Text labelText;
    public Text percentText;

    public void PrepareCanvas()
    {
        if (backgroundImage == null || tipText == null)
            AutoBind();

        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.enabled = true;
        UICanvasSetup.RefreshPopup(canvas, GameConfig.UiSort.Loading);

        if (backgroundImage != null)
            UiLayoutStretch.ApplyBgStretch(backgroundImage.rectTransform, backgroundImage);

        if (transform.localScale.sqrMagnitude < 0.0001f)
            transform.localScale = Vector3.one;

        GameFonts.ApplyToHierarchy(transform);
        if (labelText != null)
            labelText.font = GameFonts.GetChinese();
        if (percentText != null)
            percentText.font = GameFonts.GetNumber();
        if (tipText != null)
            tipText.font = GameFonts.GetChinese();

        // 【2026-10-22 诊断】真机 loading「整体放大」排查（编辑器 1240x2772 正常、真机放大）。
        // 只打日志不改任何行为，定位后删除。对照四个嫌疑点：
        // ① Screen 尺寸瞬态（真机冷启动宽高颠倒）→ match 判定错；
        // ② matchWidthOrHeight 判定（瘦屏应=0，逻辑宽恒 720）；
        // ③ canvas.scaleFactor / canvas 实际 rect（ScreenSpaceCamera 下由相机 frustum 决定）；
        // ④ 所绑相机是否 null / rect 是否被 letterbox 裁过。
        var fitScaler = canvas.GetComponent<CanvasScaler>();
        var fitCam = canvas.worldCamera;
        var fitRt = transform as RectTransform;
        Debug.Log($"[LoadingFit] screen={Screen.width}x{Screen.height} " +
                  $"match={(fitScaler != null ? fitScaler.matchWidthOrHeight.ToString("0.00") : "noScaler")} " +
                  $"scaleFactor={canvas.scaleFactor.ToString("0.00")} " +
                  $"canvasRect={fitRt.rect} " +
                  $"cam={(fitCam != null ? fitCam.name : "null")} " +
                  $"camRect={(fitCam != null ? fitCam.rect.ToString() : "-")} " +
                  $"camAspect={(fitCam != null ? fitCam.aspect.ToString("0.000") : "-")}");
    }

    /// <summary>兼容旧调用。</summary>
    public void PrepareOverlay() => PrepareCanvas();

    public void SetProgress(float progress01)
    {
        float p = Mathf.Clamp01(progress01);
        if (percentText != null)
            percentText.text = Mathf.RoundToInt(p * 100f) + "%";
    }

    // —— 提示换字限速（2026-10-16 主人反馈：Loading 提示换太快、条数太多）——
    // 进镇预热（TownHubController.CoWarmupPages + StartupPreloader.Run）每步一帧就报一条文案，
    // 一次 Loading 连闪十几条、一秒内闪完。主人拍板「一次出现2-3条就行」：
    // 首条必显，之后两条之间至少隔 MinTipIntervalSeconds 秒，单个实例最多显示 MaxTipLines 条，
    // 超额的换字请求直接忽略。场景切换 Loading 每次都是新实例、只设一条，不受影响。
    const float MinTipIntervalSeconds = 1.5f;
    const int MaxTipLines = 3;
    float _lastTipShownAt;
    int _tipShownCount;

    public void SetTip(string tip)
    {
        if (tipText == null || string.IsNullOrEmpty(tip)) return;
        if (_tipShownCount > 0)
        {
            if (_tipShownCount >= MaxTipLines) return;                        // 一次最多 3 条
            if (Time.unscaledTime - _lastTipShownAt < MinTipIntervalSeconds) return; // 换太快就忽略
        }
        _lastTipShownAt = Time.unscaledTime;
        _tipShownCount++;
        tipText.text = tip;
    }

    public string CurrentTip => tipText != null ? tipText.text : "";

    // =========================================================================
    // Loading 背景图（2026-11-20 主人拍板：目前所有章统一用 loading01 这一张）
    // 早先按章拼 beijing0N，但第三章等章节图一直没有 → 主人改为「先都用 loading01」。
    // 以后美术补齐各章图，只需在此恢复按章取名，别的地方不用动。
    // =========================================================================

    /// <summary>Loading 背景图所在目录（Resources 下，<b>不带扩展名按路径加载</b>，不吃 guid）。</summary>
    public const string ChapterBackdropDir = "UI/loading/";

    /// <summary>
    /// Loading 背景图资源名（唯一出口，别的文件别自己拼这个串）。
    /// 2026-11-20 主人拍板：<b>目前所有章统一用 loading01</b>（第三章等章节图没有）。
    /// </summary>
    public static string ChapterBackdropName(int chapter) => "loading01";

    /// <summary>
    /// 占位章号：无专属章节的玩法（副本/终章/裂隙等）由 GameSceneManager 传这个值。
    /// 2026-11-20 起所有章的 Loading 图统一 loading01，此常量只作流程占位，不再决定用哪张图。
    /// </summary>
    public const int FallbackChapter = 1;

    /// <summary>
    /// 换 Loading 背景图。2026-11-20 主人拍板：目前所有章统一用 loading01 这一张
    ///（chapter 参数只留给接口，不参与取名）。
    /// 取不到图 → <b>LogError 报错、沿用预制体默认图</b>（不静默兜底，铁律）。
    /// <para>换图后必须重跑 <see cref="UiLayoutStretch.ApplyBgStretch"/>：Envelope 是按
    /// <c>sprite.rect</c> 宽高比算的，图换了不重算就会按旧比例拉伸（铁律 5：整体等比缩放，绝不 stretch）。</para>
    /// </summary>
    public void SetChapterBackdrop(int chapter)
    {
        if (backgroundImage == null) AutoBind();
        if (backgroundImage == null) return;
        if (chapter < 1) return;

        string path = ChapterBackdropDir + ChapterBackdropName(chapter);
        var sp = Resources.Load<Sprite>(path);
        if (sp == null)
        {
            Debug.LogError($"[LoadingUI] 缺少 Loading 背景图 Resources/{path}（统一用 loading01.jpg，" +
                           "须为 Sprite 导入）—— 沿用预制体默认图");
            return;
        }
        if (backgroundImage.sprite == sp) return;

        backgroundImage.sprite = sp;
        UiLayoutStretch.ApplyBgStretch(backgroundImage.rectTransform, backgroundImage);
    }

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
