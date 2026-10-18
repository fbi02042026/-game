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

    // 击破缩放：【2026-10-15 主人拍板】每杀一次「字和数字」放大一下 → 立马缩回原大 → 短暂停留后消失。
    // （旧版是弹到 1.5 后用 3 秒慢慢缩到 0.8，主人嫌拖沓）
    const float ComboScalePop = 1.5f;
    const float ComboScaleEnd = 1f;      // 缩回原大（不再缩到 0.8）
    const float ComboPopDur = 0.08f;     // 放大
    const float ComboShrinkDur = 0.18f;  // 立马缩回
    const float ComboHideAfterShrink = 0.6f;  // 缩回后再看 0.6s 才消失
    // 【2026-10-18 主人拍板】出场/退场不做位移，直接渐显/渐隐；
    // 连杀数字从旧值逐个跳到新值，每跳一格重播一次「放大缩小」。
    const float ComboFadeInDur = 0.1f;   // 渐显
    const float ComboFadeOutDur = 0.1f;  // 渐隐
    const float ComboRollStep = 0.05f;   // 数字每跳一格的间隔

    enum ComboAnimPhase { Hidden, FadeIn, Pop, Shrink, Hold, FadeOut, Gone }
    ComboAnimPhase _comboPhase = ComboAnimPhase.Hidden;
    float _comboAnimT;
    float _comboScale = 1f;
    int _comboShown;
    int _displayCombo;      // 数字滚动：当前逐字显示到的值
    float _rollTimer;       // 数字滚动：距下一次跳格

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
        // 【2026-10-15 主人拍板】连杀挪到战斗场景**右上角**（原 y=-200 挂在右半腰）。
        // 距顶 140：让开 TopBar / 右上角设置按钮那一带；想再挪只改这一个数。
        root.anchoredPosition = new Vector2(-12f, -140f);

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

        // 数字逐个跳变：每跳一格重播一次放大缩小（Pop→Shrink 原节奏）
        if (_displayCombo < _comboShown)
        {
            _rollTimer -= dt;
            if (_rollTimer <= 0f)
            {
                _displayCombo++;
                ApplyComboText(_displayCombo);
                _rollTimer = ComboRollStep;
                _comboGroup.alpha = 1f;
                _comboPhase = ComboAnimPhase.Pop;
                _comboAnimT = 0f;
                _comboScale = 1f;
            }
        }

        if (_comboPhase == ComboAnimPhase.FadeIn)
        {
            float u = Mathf.Clamp01(_comboAnimT / ComboFadeInDur);
            _comboGroup.alpha = u;
            if (u >= 1f)
            {
                _comboPhase = ComboAnimPhase.Pop;
                _comboAnimT = 0f;
                _comboScale = 1f;
            }
        }
        else if (_comboPhase == ComboAnimPhase.Pop)
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
                _comboPhase = ComboAnimPhase.FadeOut;
                _comboAnimT = 0f;
            }
        }
        else if (_comboPhase == ComboAnimPhase.FadeOut)
        {
            float u = Mathf.Clamp01(_comboAnimT / ComboFadeOutDur);
            _comboGroup.alpha = 1f - u;
            if (u >= 1f)
            {
                _comboGroup.alpha = 0f;
                _comboPhase = ComboAnimPhase.Gone;
                _comboScale = 1f;
            }
        }

        _comboScaleRoot.localScale = Vector3.one * _comboScale;
    }

    void ApplyComboText(int combo)
    {
        if (_comboValue != null)
        {
            _comboValue.text = "x" + combo;
            _comboValue.color = combo >= 3
                ? new Color(1f, 0.55f, 0.2f, 1f)
                : new Color(1f, 0.85f, 0.25f, 1f);
        }
    }

    public void SetCombo(int combo)
    {
        if (_comboGroup == null) return;

        // 【2026-10-18 删除】引导战强隐连杀的旧判断（TutorialRules.HideKillComboHud）已整条删除：
        // 主人 10-15 拍板连杀挪右上角、10-17 拍板「右上角击破计数常驻」，引导局同样显示。
        if (combo <= 0)
        {
            _comboGroup.alpha = 0f;
            _comboPhase = ComboAnimPhase.Hidden;
            _comboShown = 0;
            _displayCombo = 0;
            _rollTimer = 0f;
            if (_comboValue != null) _comboValue.text = "";
            if (_comboScaleRoot != null) _comboScaleRoot.localScale = Vector3.one;
            _comboScale = 1f;
            return;
        }

        bool wasHidden = _comboPhase == ComboAnimPhase.Hidden || _comboPhase == ComboAnimPhase.Gone;
        _comboShown = combo;
        _comboGroup.alpha = 1f;
        if (_comboTitle != null) _comboTitle.text = "击破";

        if (wasHidden)
        {
            // 首杀：直接渐显 + 显示目标值，随后走放大缩小；不滚动数字
            _displayCombo = combo;
            ApplyComboText(combo);
            _rollTimer = 0f;
            _comboGroup.alpha = 0f;   // 从 FadeIn 起步
            _comboPhase = ComboAnimPhase.FadeIn;
            _comboAnimT = 0f;
            _comboScale = 1f;
            if (_comboScaleRoot != null)
                _comboScaleRoot.localScale = Vector3.one;
            return;
        }

        // 连杀：数字从当前显示值逐个跳到新值（每跳一格重播放大缩小），渐隐中也会被打断拉回
        if (_displayCombo < combo)
            _rollTimer = 0f;   // 立刻跳第一格
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
