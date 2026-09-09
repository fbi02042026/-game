using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗右侧 HUD：击破反馈 + 下一波倒计时（可点击加速出兵换金币）。
/// 运行时挂到 BattleUI 下，不依赖预制体手工摆点。
/// </summary>
public class BattleSideHud : MonoBehaviour
{
    public static BattleSideHud Instance { get; private set; }

    Text _comboTitle;
    Text _comboValue;
    CanvasGroup _comboGroup;
    RectTransform _comboScaleRoot;

    Button _waveBtn;
    Text _waveTitle;
    Text _waveTimer;
    Text _waveHint;
    CanvasGroup _waveGroup;

    // 击破缩放：1 → 弹到 1.5 → 慢收到 0.8 → 再隐藏（不边缩边淡）
    const float ComboScalePop = 1.5f;
    const float ComboScaleEnd = 0.8f;
    const float ComboPopDur = 0.08f;
    const float ComboShrinkDur = 3f;
    const float ComboHideAfterShrink = 0.12f;

    enum ComboAnimPhase { Hidden, Pop, Shrink, Hold, Gone }
    ComboAnimPhase _comboPhase = ComboAnimPhase.Hidden;
    float _comboAnimT;
    float _comboScale = 1f;
    int _comboShown;

    public static BattleSideHud EnsureOn(Transform battleUiRoot)
    {
        if (battleUiRoot == null) return Instance;

        var existing = Instance != null ? Instance : battleUiRoot.GetComponentInChildren<BattleSideHud>(true);
        if (existing != null)
        {
            Instance = existing;
            if (existing._comboGroup == null || existing._waveGroup == null)
                existing.Build();
            return existing;
        }

        var go = new GameObject("BattleSideHud", typeof(RectTransform));
        go.transform.SetParent(battleUiRoot, false);
        go.transform.SetAsLastSibling();
        var hud = go.AddComponent<BattleSideHud>();
        hud.Build();
        return hud;
    }

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Build()
    {
        ClearChildrenImmediate();

        var root = GetComponent<RectTransform>();
        root.anchorMin = new Vector2(1f, 1f);
        root.anchorMax = new Vector2(1f, 1f);
        root.pivot = new Vector2(1f, 1f);
        root.sizeDelta = new Vector2(200f, 250f);
        root.anchoredPosition = new Vector2(-12f, -200f);

        // —— 击破（无圆环倒计时）——
        var comboGo = CreatePanel("ComboPanel", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(180f, 88f), transparent: true);
        _comboGroup = comboGo.AddComponent<CanvasGroup>();
        _comboGroup.alpha = 0f;
        _comboScaleRoot = comboGo.GetComponent<RectTransform>();
        _comboScaleRoot.localScale = Vector3.one;

        _comboTitle = CreateText(comboGo.transform, "ComboTitle", "击破", 28, TextAnchor.MiddleCenter,
            new Vector2(0f, 18f), new Vector2(160f, 32f));
        _comboTitle.color = new Color(1f, 0.9f, 0.35f, 1f);
        _comboTitle.fontStyle = FontStyle.Bold;

        _comboValue = CreateText(comboGo.transform, "ComboValue", "x1", 42, TextAnchor.MiddleCenter,
            new Vector2(0f, -18f), new Vector2(160f, 48f));
        _comboValue.color = new Color(1f, 0.85f, 0.25f, 1f);
        _comboValue.fontStyle = FontStyle.Bold;

        // —— 下一波倒计时（无黑底，大字）——
        var waveGo = CreatePanel("WavePanel", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(200f, 72f), transparent: true);
        _waveGroup = waveGo.AddComponent<CanvasGroup>();
        _waveGroup.alpha = 0f;
        _waveGroup.blocksRaycasts = false;

        _waveBtn = waveGo.AddComponent<Button>();
        var colors = _waveBtn.colors;
        colors.normalColor = Color.clear;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.08f);
        colors.pressedColor = new Color(1f, 1f, 1f, 0.14f);
        colors.disabledColor = Color.clear;
        _waveBtn.colors = colors;
        var img = waveGo.GetComponent<Image>();
        img.color = Color.clear;
        img.raycastTarget = true;
        _waveBtn.targetGraphic = img;
        _waveBtn.onClick.AddListener(OnWaveClicked);

        _waveTitle = CreateText(waveGo.transform, "WaveLine", "下一波 8.0", 36, TextAnchor.MiddleCenter,
            Vector2.zero, new Vector2(196f, 64f));
        _waveTitle.color = new Color(1f, 0.28f, 0.22f, 0.98f);
        _waveTitle.fontStyle = FontStyle.Bold;
        _waveTimer = null;
        _waveHint = null;

        EnsureSortCanvas();
        GameFonts.ApplyToHierarchy(transform);
    }

    void ClearChildrenImmediate()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);
    }

    void EnsureSortCanvas()
    {
        var c = GetComponent<Canvas>();
        if (c == null) c = gameObject.AddComponent<Canvas>();
        c.overrideSorting = true;
        c.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
        c.sortingOrder = 105;
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
    }

    static GameObject CreatePanel(string name, RectTransform parent, Vector2 amin, Vector2 amax,
        Vector2 pivot, Vector2 pos, Vector2 size, bool transparent = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = amin;
        rt.anchorMax = amax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.color = transparent ? Color.clear : new Color(0.08f, 0.1f, 0.16f, 0.72f);
        img.raycastTarget = !transparent;
        return go;
    }

    static Text CreateText(Transform parent, string name, string content, int size,
        TextAnchor align, Vector2 pos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;
        var t = go.AddComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        bool isNum = name.IndexOf("Value", System.StringComparison.OrdinalIgnoreCase) >= 0
                     || name.IndexOf("Timer", System.StringComparison.OrdinalIgnoreCase) >= 0
                     || name.IndexOf("Count", System.StringComparison.OrdinalIgnoreCase) >= 0;
        t.font = isNum ? GameFonts.GetNumber() : GameFonts.GetChinese();
        return t;
    }

    void Update()
    {
        if (_comboScaleRoot == null || _comboGroup == null) return;
        if (_comboPhase == ComboAnimPhase.Hidden || _comboPhase == ComboAnimPhase.Gone)
            return;

        float dt = Time.unscaledDeltaTime;
        _comboAnimT += dt;

        if (_comboPhase == ComboAnimPhase.Pop)
        {
            float u = Mathf.Clamp01(_comboAnimT / ComboPopDur);
            _comboScale = Mathf.Lerp(1f, ComboScalePop, u);
            if (u >= 1f)
            {
                _comboPhase = ComboAnimPhase.Shrink;
                _comboAnimT = 0f;
                _comboScale = ComboScalePop;
            }
        }
        else if (_comboPhase == ComboAnimPhase.Shrink)
        {
            float u = Mathf.Clamp01(_comboAnimT / ComboShrinkDur);
            _comboScale = Mathf.Lerp(ComboScalePop, ComboScaleEnd, u);
            // 全程不淡出
            _comboGroup.alpha = 1f;
            if (u >= 1f)
            {
                _comboPhase = ComboAnimPhase.Hold;
                _comboAnimT = 0f;
                _comboScale = ComboScaleEnd;
            }
        }
        else if (_comboPhase == ComboAnimPhase.Hold)
        {
            _comboScale = ComboScaleEnd;
            _comboGroup.alpha = 1f;
            if (_comboAnimT >= ComboHideAfterShrink)
            {
                _comboGroup.alpha = 0f;
                _comboPhase = ComboAnimPhase.Gone;
                _comboScale = 1f;
            }
        }

        _comboScaleRoot.localScale = Vector3.one * _comboScale;
    }

    public void SetCombo(int combo)
    {
        if (_comboGroup == null) return;

        // 引导战不显示连杀「击破」
        if (BattleManager.Instance != null && BattleManager.Instance.IsTutorialRun)
            combo = 0;

        if (combo <= 0)
        {
            _comboGroup.alpha = 0f;
            _comboPhase = ComboAnimPhase.Hidden;
            _comboShown = 0;
            if (_comboValue != null) _comboValue.text = "";
            if (_comboScaleRoot != null) _comboScaleRoot.localScale = Vector3.one;
            _comboScale = 1f;
            return;
        }

        _comboShown = combo;
        _comboGroup.alpha = 1f;
        if (_comboTitle != null) _comboTitle.text = "击破";
        if (_comboValue != null)
        {
            _comboValue.text = "x" + combo;
            _comboValue.color = combo >= 3
                ? new Color(1f, 0.55f, 0.2f, 1f)
                : new Color(1f, 0.85f, 0.25f, 1f);
        }

        // 再杀：立刻弹回 1.5 重播（消失前可继续计数）
        _comboPhase = ComboAnimPhase.Pop;
        _comboAnimT = 0f;
        _comboScale = 1f;
        if (_comboScaleRoot != null)
            _comboScaleRoot.localScale = Vector3.one;
    }

    public void ResetCombo()
    {
        SetCombo(0);
    }

    public void SetWaveCountdown(bool visible, float secondsLeft, bool canSkip)
    {
        if (_waveGroup == null) return;
        _waveGroup.alpha = visible ? 1f : 0f;
        _waveGroup.blocksRaycasts = visible && canSkip;
        if (_waveBtn != null) _waveBtn.interactable = visible && canSkip;

        if (!visible) return;

        int sec = Mathf.Max(0, Mathf.CeilToInt(secondsLeft));
        if (_waveTitle != null)
            _waveTitle.text = "下一波 " + sec;
        else if (_waveTimer != null)
            _waveTimer.text = secondsLeft.ToString("0.0");
        if (_waveHint != null)
            _waveHint.text = canSkip ? "点击加速出兵" : "即将出兵…";
    }

    void OnWaveClicked()
    {
        BattleManager.Instance?.TrySkipToNextWave();
    }
}
