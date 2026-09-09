using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 引导提示。软引导不挡点击；硬引导四周遮罩挖空目标，手指滑向按钮。
/// </summary>
public class TutorialHintUI : MonoBehaviour
{
    public static TutorialHintUI Instance { get; private set; }

    CanvasGroup _group;
    Text _label;
    RectTransform _bannerRt;
    RectTransform _follow;
    RectTransform _holeRt;
    Button _holeButton;
    Image _pointerHand;
    RectTransform _pointerRt;
    RectTransform[] _dims;
    GraphicRaycaster _raycaster;
    Vector2 _pointerTarget;
    Vector2 _swipeFrom;
    float _swipeT = 1f;
    bool _pointerActive;
    bool _hasPointerPos;
    /// <summary>true=手在按钮上方朝下指（默认），false=按钮上方放不下，退回下方朝上指。</summary>
    bool _pointerAbove = true;
    float _hideAt = -1f;
    bool _hard;
    const float SwipeDur = 0.45f;
    const float HandSize = 96f;
    const int DefaultSortOrder = GameConfig.UiSort.TutorialHint;
    const string PrefabResourcesPath = "Prefabs/UI/TutorialHintUI";

    public static TutorialHintUI Ensure()
    {
        if (Instance != null) return Instance;

        GameObject go = null;
        var prefab = Resources.Load<GameObject>(PrefabResourcesPath);
        if (prefab != null)
        {
            go = Object.Instantiate(prefab);
            go.name = "TutorialHintUI";
        }
        if (go == null)
            go = new GameObject("TutorialHintUI");

        DontDestroyOnLoad(go);
        var ui = go.GetComponent<TutorialHintUI>();
        if (ui == null) ui = go.AddComponent<TutorialHintUI>();
        ui.EnsureBuilt();
        return ui;
    }

    void Awake()
    {
        Instance = this;
        EnsureBuilt();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void EnsureBuilt()
    {
        if (_group != null && _label != null && _bannerRt != null) return;
        if (TryBindFromHierarchy()) return;
        Build();
    }

    bool TryBindFromHierarchy()
    {
        if (transform.Find("Banner") == null) return false;

        var canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, DefaultSortOrder);
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
        _raycaster = GetComponent<GraphicRaycaster>();

        _group = gameObject.GetComponent<CanvasGroup>();
        if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        _group.blocksRaycasts = false;
        _group.interactable = false;
        if (_group.alpha > 0.99f) _group.alpha = 0f;
        if (_raycaster != null) _raycaster.enabled = false;

        _dims = new RectTransform[4];
        string[] names = { "DimTop", "DimBottom", "DimLeft", "DimRight" };
        for (int i = 0; i < 4; i++)
        {
            var t = transform.Find(names[i]) as RectTransform;
            _dims[i] = t;
            if (t != null) t.gameObject.SetActive(false);
        }

        var hole = transform.Find("Hole");
        if (hole != null)
        {
            _holeRt = hole as RectTransform;
            _holeButton = hole.GetComponent<Button>();
            if (_holeButton != null)
            {
                _holeButton.onClick.RemoveListener(OnHoleClicked);
                _holeButton.onClick.AddListener(OnHoleClicked);
            }
            hole.gameObject.SetActive(false);
        }

        var banner = transform.Find("Banner") as RectTransform;
        _bannerRt = banner;
        if (banner != null)
        {
            var hint = banner.Find("HintText");
            if (hint != null) _label = hint.GetComponent<Text>();
            if (_label != null)
            {
                if (_label.font == null)
                    _label.font = GameFonts.GetChinese();
                _label.color = new Color(0xFC / 255f, 0xFD / 255f, 0xBE / 255f, 1f);
                // 拉伸锚点：Top=10 / Bottom=18 / Left=Right=28（仅改 sizeDelta 且 pos=0 会变成上下各 14）
                var lrt = _label.rectTransform;
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.one;
                lrt.pivot = new Vector2(0.5f, 0.5f);
                lrt.offsetMin = new Vector2(28f, 18f);
                lrt.offsetMax = new Vector2(-28f, -10f);
            }
        }

        var pointer = transform.Find("PointerHand");
        if (pointer != null)
        {
            _pointerRt = pointer as RectTransform;
            _pointerHand = pointer.GetComponent<Image>();
            pointer.gameObject.SetActive(false);
        }

        PlaceBanner(null);
        return _label != null && _bannerRt != null && _group != null;
    }

    void Build()
    {
        var canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, DefaultSortOrder);
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
        _raycaster = GetComponent<GraphicRaycaster>();

        _group = gameObject.GetComponent<CanvasGroup>();
        if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        _group.blocksRaycasts = false;
        _group.interactable = false;
        _group.alpha = 0f;
        if (_raycaster != null) _raycaster.enabled = false;

        _dims = new RectTransform[4];
        string[] names = { "DimTop", "DimBottom", "DimLeft", "DimRight" };
        for (int i = 0; i < 4; i++)
        {
            var img = CreateImage(transform, names[i], new Color(0f, 0f, 0f, 0.38f));
            img.raycastTarget = true;
            _dims[i] = img.rectTransform;
            img.gameObject.SetActive(false);
        }

        var holeGo = new GameObject("Hole", typeof(RectTransform), typeof(Image), typeof(Button));
        holeGo.transform.SetParent(transform, false);
        _holeRt = holeGo.GetComponent<RectTransform>();
        var holeImg = holeGo.GetComponent<Image>();
        holeImg.color = new Color(1f, 1f, 1f, 0f);
        holeImg.raycastTarget = true;
        _holeButton = holeGo.GetComponent<Button>();
        _holeButton.transition = Selectable.Transition.None;
        _holeButton.onClick.AddListener(OnHoleClicked);
        holeGo.SetActive(false);

        var banner = CreateImage(transform, "Banner", Color.white);
        TryApplyBannerPlate(banner);
        _bannerRt = banner.rectTransform;
        _bannerRt.anchorMin = new Vector2(0.02f, 0.58f);
        _bannerRt.anchorMax = new Vector2(0.98f, 0.58f);
        _bannerRt.pivot = new Vector2(0.5f, 0.5f);
        _bannerRt.anchoredPosition = Vector2.zero;
        _bannerRt.sizeDelta = new Vector2(0f, 100f);

        _label = CreateText(banner.transform, "HintText", "", 26, TextAnchor.MiddleCenter);
        _label.font = GameFonts.GetChinese();
        _label.color = new Color(0xFC / 255f, 0xFD / 255f, 0xBE / 255f, 1f);
        var lrt = _label.rectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.pivot = new Vector2(0.5f, 0.5f);
        lrt.offsetMin = new Vector2(28f, 18f);
        lrt.offsetMax = new Vector2(-28f, -10f); // Top=10
        _label.horizontalOverflow = HorizontalWrapMode.Wrap;
        _label.verticalOverflow = VerticalWrapMode.Overflow;

        var pointerGo = new GameObject("PointerHand", typeof(RectTransform), typeof(Image));
        pointerGo.transform.SetParent(transform, false);
        _pointerRt = pointerGo.GetComponent<RectTransform>();
        _pointerHand = pointerGo.GetComponent<Image>();
        _pointerHand.raycastTarget = false;
        _pointerHand.preserveAspect = true;
        _pointerRt.pivot = new Vector2(0.5f, 1f);
        _pointerRt.anchorMin = _pointerRt.anchorMax = new Vector2(0.5f, 0.5f);
        _pointerRt.sizeDelta = new Vector2(HandSize, HandSize);
        pointerGo.SetActive(false);

        GameFonts.ApplyToHierarchy(transform);
        if (_label != null && _label.font == null)
            _label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        PlaceBanner(null);
    }

    static void TryApplyBannerPlate(Image banner)
    {
        if (banner == null) return;
#if UNITY_EDITOR
        var sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/引导/引导底框.png");
        if (sp != null)
        {
            banner.sprite = sp;
            banner.type = Image.Type.Simple;
            banner.color = Color.white;
            return;
        }
#endif
        banner.color = new Color(0.08f, 0.1f, 0.16f, 0.88f);
    }

    static Image CreateImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static Text CreateText(Transform parent, string name, string content, int size, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        t.font = GameFonts.GetChinese();
        if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return t;
    }

    public void ShowHard(string text, RectTransform highlight)
    {
        Show(text, highlight, -1f, hard: true);
    }

    public void Show(string text, RectTransform highlight = null, float autoHideSeconds = 8f)
    {
        Show(text, highlight, autoHideSeconds, hard: false);
    }

    public bool IsVisible => _group != null && _group.alpha > 0.05f && _label != null && !string.IsNullOrEmpty(_label.text);
    public string CurrentHint => _label != null ? _label.text : "";

    public void Show(string text, RectTransform highlight, float autoHideSeconds, bool hard)
    {
        if (BattleHeadTalkUI.Instance != null && BattleHeadTalkUI.Instance.IsShowing)
            return;

        EnsureBuilt();
        bool targetChanged = highlight != _follow;
        if (_label != null) _label.text = text ?? "";
        _follow = highlight;
        // 没有目标就不能上硬引导：否则全屏挡住点击又没有挖空，直接卡死
        _hard = hard && highlight != null;
        hard = _hard;
        ApplySortForHighlight(highlight, hard);
        _group.alpha = 1f;
        _group.blocksRaycasts = hard;
        _group.interactable = hard;
        // 软引导绝不能挡点击：关掉 Raycaster，并强制收掉四周遮罩
        if (_raycaster == null) _raycaster = GetComponent<GraphicRaycaster>();
        if (_raycaster != null) _raycaster.enabled = hard;
        _hideAt = autoHideSeconds < 0f ? -1f : Time.unscaledTime + autoHideSeconds;
        SetDimsActive(hard);
        if (_holeRt != null)
            _holeRt.gameObject.SetActive(hard && highlight != null);
        if (!hard)
            ForceClearBlockers();
        RefreshLayout();
        if (highlight != null && (targetChanged || !_pointerActive))
            BeginSwipeToTarget();
        else if (highlight == null)
            HidePointer();
    }

    /// <summary>硬引导盖过目标所在 Canvas（如 TownPopup 选职页）。</summary>
    void ApplySortForHighlight(RectTransform highlight, bool hard)
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) return;
        int order = DefaultSortOrder;
        if (hard && highlight != null)
        {
            var followCanvas = highlight.GetComponentInParent<Canvas>();
            if (followCanvas != null)
                order = Mathf.Max(order, followCanvas.sortingOrder + 10);
            else
                order = Mathf.Max(order, GameConfig.UiSort.TownPopup + 10);
        }
        canvas.sortingOrder = order;
    }

    void RestoreDefaultSort()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas != null)
            canvas.sortingOrder = DefaultSortOrder;
    }

    /// <summary>确保软引导/隐藏后不留遮罩挡底栏。</summary>
    void ForceClearBlockers()
    {
        SetDimsActive(false);
        if (_holeRt != null) _holeRt.gameObject.SetActive(false);
        if (_group != null)
        {
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }
        if (_raycaster == null) _raycaster = GetComponent<GraphicRaycaster>();
        if (_raycaster != null) _raycaster.enabled = false;
    }

    public void Hide()
    {
        _hard = false;
        if (_group != null)
        {
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }
        _hideAt = -1f;
        _follow = null;
        ForceClearBlockers();
        HidePointer();
        RestoreDefaultSort();
    }

    /// <summary>设置弹窗等更高层 UI 打开时，暂时关掉硬遮罩挡点击。</summary>
    public void SetHardBlocking(bool on)
    {
        _hard = on && _follow != null;
        if (_group != null)
        {
            _group.blocksRaycasts = _hard;
            _group.interactable = _hard;
        }
        if (_raycaster == null) _raycaster = GetComponent<GraphicRaycaster>();
        if (_raycaster != null) _raycaster.enabled = _hard;
        SetDimsActive(_hard);
        if (_holeRt != null)
            _holeRt.gameObject.SetActive(_hard && _follow != null);
        if (!_hard)
            ForceClearBlockers();
    }

    void OnHoleClicked()
    {
        if (!_hard || _follow == null) return;
        var btn = _follow.GetComponent<Button>();
        if (btn == null) btn = _follow.GetComponentInParent<Button>();
        if (btn != null && btn.interactable)
            btn.onClick.Invoke();
    }

    void Update()
    {
        if (_group == null || _group.alpha < 0.01f) return;
        if (!_hard)
            ForceClearBlockers();
        if (_hideAt > 0f && Time.unscaledTime >= _hideAt)
        {
            Hide();
            return;
        }
        RefreshLayout();
        UpdatePointerMotion();
    }

    void SetDimsActive(bool on)
    {
        if (_dims == null) return;
        for (int i = 0; i < _dims.Length; i++)
        {
            if (_dims[i] != null)
                _dims[i].gameObject.SetActive(on);
        }
    }

    void RefreshLayout()
    {
        if (_follow == null || !_follow.gameObject.activeInHierarchy)
        {
            if (_holeRt != null) _holeRt.gameObject.SetActive(false);
            // 目标没了（换场景等）就必须收掉遮罩，否则会留下几条黑带盖在新界面上
            SetDimsActive(false);
            PlaceBanner(null);
            return;
        }

        if (!TryGetFollowLocalRect(out float minX, out float maxX, out float minY, out float maxY))
            return;

        float pad = 8f;
        minX -= pad;
        maxX += pad;
        minY -= pad;
        maxY += pad;

        var root = transform as RectTransform;
        float left = root.rect.xMin;
        float right = root.rect.xMax;
        float bot = root.rect.yMin;
        float top = root.rect.yMax;

        if (_hard && _dims != null && _dims.Length == 4)
        {
            PlaceStrip(_dims[0], left, right, maxY, top);
            PlaceStrip(_dims[1], left, right, bot, minY);
            PlaceStrip(_dims[2], left, minX, minY, maxY);
            PlaceStrip(_dims[3], maxX, right, minY, maxY);
            SetDimsActive(true);
        }
        else
            SetDimsActive(false);

        if (_hard && _holeRt != null)
        {
            _holeRt.gameObject.SetActive(true);
            PlaceStrip(_holeRt, minX, maxX, minY, maxY);
        }

        float centerX = (minX + maxX) * 0.5f;

        // 指尖始终落在 anchoredPosition 上（pivot 顶部）：
        // 上方模式旋转 180°，手身往上伸，不会盖住按钮；上方塞不下才退回下方朝上指。
        float handH = _pointerRt != null ? _pointerRt.sizeDelta.y : HandSize;
        const float gap = 10f;
        _pointerAbove = (maxY + gap + handH) <= (top - 8f);
        if (_pointerAbove)
            _pointerTarget = new Vector2(centerX, maxY + gap);
        else
            _pointerTarget = new Vector2(centerX, minY - gap);
        _pointerTarget = ClampPointerTarget(root, _pointerTarget);
        if (_pointerRt != null)
            _pointerRt.localRotation = Quaternion.Euler(0f, 0f, _pointerAbove ? 180f : 0f);

        PlaceBanner(new Vector2(centerX, minY), maxY, top, bot, _pointerAbove ? handH + gap : 0f);
    }

    /// <summary>文字横幅：高度固定 100，左右少留白，不按字高拉扁底图。</summary>
    void PlaceBanner(Vector2? target, float targetTop = 0f, float top = 0f, float bot = 0f,
        float extraTopClearance = 0f)
    {
        if (_bannerRt == null) return;
        var root = transform as RectTransform;
        if (root == null) return;

        const float bannerH = 100f;
        // 左右各约 12px，避免两侧空一大截
        float width = Mathf.Max(280f, root.rect.width - 24f);
        _bannerRt.anchorMin = _bannerRt.anchorMax = new Vector2(0.5f, 0.5f);
        _bannerRt.pivot = new Vector2(0.5f, 0.5f);
        _bannerRt.sizeDelta = new Vector2(width, bannerH);

        bool hasText = _label != null && !string.IsNullOrEmpty(_label.text);
        _bannerRt.gameObject.SetActive(hasText);

        if (!target.HasValue)
        {
            _bannerRt.anchoredPosition = new Vector2(0f, root.rect.height * 0.22f);
            return;
        }

        float half = bannerH * 0.5f;
        // 手在按钮上方时，横幅要让开整只手，否则字压在手背上
        float y = targetTop + 24f + extraTopClearance + half;
        if (y + half > top - 12f)
            y = target.Value.y - 24f - half;
        y = Mathf.Clamp(y, bot + half + 12f, top - half - 12f);
        _bannerRt.anchoredPosition = new Vector2(0f, y);
    }

    void BeginSwipeToTarget()
    {
        if (_pointerRt == null) return;
        var sprite = TutorialPointerArt.Get();
        if (sprite == null) return;

        _pointerHand.sprite = sprite;
        _pointerHand.SetNativeSize();
        var native = _pointerRt.sizeDelta;
        float scale = HandSize / Mathf.Max(native.x, native.y, 1f);
        _pointerRt.sizeDelta = native * scale;
        _pointerRt.gameObject.SetActive(true);
        _pointerActive = true;

        var root = transform as RectTransform;
        _swipeFrom = _hasPointerPos
            ? _pointerRt.anchoredPosition
            : new Vector2(root.rect.xMin + 80f, root.rect.center.y);
        _swipeT = 0f;
        _hasPointerPos = true;
    }

    void UpdatePointerMotion()
    {
        if (!_pointerActive || _pointerRt == null) return;

        Vector2 pos;
        if (_swipeT < 1f)
        {
            _swipeT = Mathf.Clamp01(_swipeT + Time.unscaledDeltaTime / SwipeDur);
            float e = 1f - Mathf.Pow(1f - _swipeT, 3f);
            pos = Vector2.Lerp(_swipeFrom, _pointerTarget, e);
        }
        else
        {
            float y = Mathf.Sin(Time.unscaledTime * 6f) * 10f;
            pos = _pointerTarget + new Vector2(0f, y);
        }

        var root = transform as RectTransform;
        _pointerRt.anchoredPosition = ClampPointerTarget(root, pos);
    }

    void HidePointer()
    {
        _pointerActive = false;
        _swipeT = 1f;
        if (_pointerRt != null) _pointerRt.gameObject.SetActive(false);
    }

    bool TryGetFollowLocalRect(out float minX, out float maxX, out float minY, out float maxY)
    {
        minX = maxX = minY = maxY = 0f;
        var root = transform as RectTransform;
        if (root == null || _follow == null) return false;

        var followCanvas = _follow.GetComponentInParent<Canvas>();
        var hintCanvas = GetComponent<Canvas>();
        Camera hintCam = hintCanvas != null ? hintCanvas.worldCamera : UICanvasSetup.ResolveUiCamera();
        Camera followCam = followCanvas != null ? followCanvas.worldCamera : hintCam;
        if (followCam == null) followCam = hintCam;

        var corners = new Vector3[4];
        _follow.GetWorldCorners(corners);
        minX = minY = float.MaxValue;
        maxX = maxY = float.MinValue;
        for (int i = 0; i < 4; i++)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(followCam, corners[i]);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, hintCam, out var local);
            minX = Mathf.Min(minX, local.x);
            maxX = Mathf.Max(maxX, local.x);
            minY = Mathf.Min(minY, local.y);
            maxY = Mathf.Max(maxY, local.y);
        }
        return maxX > minX && maxY > minY;
    }

    Vector2 ClampPointerTarget(RectTransform root, Vector2 p)
    {
        if (root == null) return p;
        float halfW = (_pointerRt != null ? _pointerRt.sizeDelta.x : HandSize) * 0.5f;
        float handH = _pointerRt != null ? _pointerRt.sizeDelta.y : HandSize;
        float minX = root.rect.xMin + halfW + 6f;
        float maxX = root.rect.xMax - halfW - 6f;
        float minY, maxY;
        if (_pointerAbove)
        {
            // 旋转 180°：指尖在锚点，手身往上伸，顶部要留出整只手
            minY = root.rect.yMin + 8f;
            maxY = root.rect.yMax - handH - 8f;
        }
        else
        {
            // pivot 在顶部：锚点往下才是手掌，底部要留出整只手的高度
            minY = root.rect.yMin + handH + 8f;
            maxY = root.rect.yMax - 16f;
        }
        return new Vector2(Mathf.Clamp(p.x, minX, maxX), Mathf.Clamp(p.y, Mathf.Min(minY, maxY), maxY));
    }

    static void PlaceStrip(RectTransform rt, float xmin, float xmax, float ymin, float ymax)
    {
        if (rt == null) return;
        float w = Mathf.Max(0f, xmax - xmin);
        float h = Mathf.Max(0f, ymax - ymin);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2((xmin + xmax) * 0.5f, (ymin + ymax) * 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.localScale = Vector3.one;
        rt.gameObject.SetActive(w > 0.5f && h > 0.5f);
    }
}
