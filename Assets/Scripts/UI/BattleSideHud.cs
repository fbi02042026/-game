using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗右侧 HUD：连杀 + 下一波倒计时（可点击加速出兵换金币）。
/// 运行时挂到 BattleUI 下，不依赖预制体手工摆点。
/// </summary>
public class BattleSideHud : MonoBehaviour
{
    public static BattleSideHud Instance { get; private set; }

    Text _comboTitle;
    Text _comboValue;
    CanvasGroup _comboGroup;

    Button _waveBtn;
    Text _waveTitle;
    Text _waveTimer;
    Text _waveHint;
    CanvasGroup _waveGroup;

    float _comboPunch;

    public static BattleSideHud EnsureOn(Transform battleUiRoot)
    {
        if (battleUiRoot == null) return Instance;

        var existing = Instance != null ? Instance : battleUiRoot.GetComponentInChildren<BattleSideHud>(true);
        if (existing != null)
        {
            Instance = existing;
            // 预制体里带的空壳没跑过 Build，补建一次，否则连杀/倒计时永远不显示
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
        // 右上角锚定，避免居中+偏移在不同 Canvas 缩放下飞出屏外
        root.anchorMin = new Vector2(1f, 1f);
        root.anchorMax = new Vector2(1f, 1f);
        root.pivot = new Vector2(1f, 1f);
        root.sizeDelta = new Vector2(200f, 250f);
        root.anchoredPosition = new Vector2(-12f, -200f);

        // —— 连杀 ——
        var comboGo = CreatePanel("ComboPanel", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(180f, 88f));
        _comboGroup = comboGo.AddComponent<CanvasGroup>();
        _comboGroup.alpha = 0f;
        _comboTitle = CreateText(comboGo.transform, "ComboTitle", "连杀", 22, TextAnchor.MiddleCenter,
            new Vector2(0f, 22f), new Vector2(160f, 28f));
        _comboValue = CreateText(comboGo.transform, "ComboValue", "x0", 46, TextAnchor.MiddleCenter,
            new Vector2(0f, -18f), new Vector2(160f, 52f));
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

    /// <summary>保证在 map 等嵌套 Canvas 之上绘制</summary>
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
        // 数字控件用 PixelFont，其余 fusion-pixel
        bool isNum = name.IndexOf("Value", System.StringComparison.OrdinalIgnoreCase) >= 0
                     || name.IndexOf("Timer", System.StringComparison.OrdinalIgnoreCase) >= 0
                     || name.IndexOf("Count", System.StringComparison.OrdinalIgnoreCase) >= 0;
        t.font = isNum ? GameFonts.GetNumber() : GameFonts.GetChinese();
        return t;
    }

    void Update()
    {
        if (_comboPunch > 0f)
        {
            _comboPunch -= Time.unscaledDeltaTime;
            float s = _comboPunch > 0f ? 1f + Mathf.Clamp01(_comboPunch) * 0.35f : 1f;
            if (_comboValue != null)
                _comboValue.rectTransform.localScale = Vector3.one * s;
        }
    }

    public void SetCombo(int combo)
    {
        if (_comboGroup == null) return;

        if (combo <= 0)
        {
            _comboGroup.alpha = 0f;
            if (_comboValue != null) _comboValue.text = "";
            return;
        }

        _comboGroup.alpha = 1f;
        if (_comboValue == null) return;
        _comboValue.text = "x" + combo;
        _comboValue.color = combo >= 3
            ? new Color(1f, 0.55f, 0.2f, 1f)
            : new Color(1f, 0.85f, 0.25f, 1f);
        _comboPunch = 0.25f;
    }

    public void ResetCombo()
    {
        SetCombo(0);
    }

    /// <param name="visible">是否显示倒计时面板</param>
    /// <param name="secondsLeft">剩余秒</param>
    /// <param name="canSkip">是否可点击加速</param>
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
        {
            _waveHint.text = canSkip ? "点击加速出兵" : "即将出兵…";
        }
    }

    void OnWaveClicked()
    {
        BattleManager.Instance?.TrySkipToNextWave();
    }
}
