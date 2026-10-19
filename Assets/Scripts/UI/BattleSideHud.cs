using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗右上角 HUD：击败累计计数（常驻）+ 下一波倒计时（可点击加速出兵换金币）。
/// 运行时挂到 BattleUI 下，不依赖预制体手工摆点。
/// </summary>
public class BattleSideHud : MonoBehaviour
{
    public static BattleSideHud Instance { get; private set; }

    Image _killLabel;              // 「击败」标签图（Resources/Icons/击败/击败）
    readonly System.Collections.Generic.List<Image> _killDigits = new();   // 数字图，按位数扩充
    readonly Sprite[] _digitSprites = new Sprite[10];
    Sprite _killLabelSprite;
    bool _killSpritesMissing;      // 图片缺失已报过错，不再刷屏

    CanvasGroup _comboGroup;
    RectTransform _comboScaleRoot;

    Button _waveBtn;
    Text _waveTitle;
    Text _waveTimer;
    Text _waveHint;
    CanvasGroup _waveGroup;

    // 击败计数位置：【2026-10-19 主人截图定位置】贴 Boss 血条右端下方、靠右缘——
    // 右缩 48、距顶 114；想微调只改这两个数。
    const float KillCounterRightInset = 48f;
    const float KillCounterTopOffset = 114f;
    // 击杀反馈：每杀一次「字和数字」放大一下 → 立马缩回原大。
    // 【2026-10-19 主人拍板】放大效果保留但节奏加快（原 0.08/0.18 偏拖）。
    const float ComboScalePop = 1.5f;
    const float ComboScaleEnd = 1f;      // 缩回原大（不再缩到 0.8）
    const float ComboPopDur = 0.05f;     // 放大（加快）
    const float ComboShrinkDur = 0.1f;   // 立马缩回（加快）
    // 【2026-10-18 主人拍板】出场/退场不做位移，直接渐显/渐隐；
    // 数字从旧值逐个跳到新值，每跳一格重播一次「放大缩小」。
    const float ComboFadeInDur = 0.1f;   // 渐显
    const float ComboRollStep = 0.05f;   // 数字每跳一格的间隔

    enum ComboAnimPhase { Hidden, FadeIn, Pop, Shrink, Hold, Gone }
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
        root.sizeDelta = new Vector2(210f, 250f);
        // 【2026-10-19 主人截图定位置】右上角，贴 Boss 血条右端下方：右缩 48、距顶 114。
        // （原 10-15 拍板的 -12,-140 与新位置重叠才改；微调只动上面两个常量）
        root.anchoredPosition = new Vector2(-KillCounterRightInset, -KillCounterTopOffset);

        // —— 击败累计计数（常驻；图源 Resources/Icons/击败，位置样式按主人 2026-10-19 截图）——
        LoadKillSprites();
        var comboGo = CreatePanel("ComboPanel", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(250f, 70f), transparent: true);
        _comboGroup = comboGo.AddComponent<CanvasGroup>();
        _comboGroup.alpha = 0f;
        _comboScaleRoot = comboGo.GetComponent<RectTransform>();
        _comboScaleRoot.localScale = Vector3.one;

        if (!_killSpritesMissing)
        {
            var labelGo = new GameObject("KillLabel", typeof(RectTransform), typeof(Image));
            labelGo.transform.SetParent(comboGo.transform, false);
            _killLabel = labelGo.GetComponent<Image>();
            _killLabel.sprite = _killLabelSprite;
            _killLabel.raycastTarget = false;
            _killLabel.preserveAspect = true;

            EnsureKillDigits(3);
            ApplyKillDigits(0);
        }

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
                ApplyKillDigits(_displayCombo);
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
            // 【2026-10-19 主人拍板】击败计数常驻不消失（原「0.6s 后渐隐」已删），新一局才归位
            _comboScale = ComboScaleEnd;
            _comboGroup.alpha = 1f;
        }

        _comboScaleRoot.localScale = Vector3.one * _comboScale;
    }

    // ────── 击败计数：图片拼数字（图源 Resources/Icons/击败）──────

    const string KillIconPath = "Icons/击败";
    const float KillDigitW = 38f;    // 数字图显示宽（原图 48x50，等比略缩）
    const float KillDigitH = 38f;
    const float KillDigitGap = -4f;  // 数字间距（收紧，轻微重叠更紧凑）
    const float KillLabelW = 124f;   // 「击败」标签显示宽（原图 200x68 等比）
    const float KillLabelH = 42f;
    const float KillLabelGap = -2f;  // 标签与数字的间距（收紧）

    void LoadKillSprites()
    {
        if (_digitSprites[0] != null || _killSpritesMissing) return;   // 已加载 / 已报过错
        _killLabelSprite = Resources.Load<Sprite>(KillIconPath + "/击败");
        for (int d = 0; d <= 9; d++)
            _digitSprites[d] = Resources.Load<Sprite>(KillIconPath + "/" + d);
        bool ok = _killLabelSprite != null;
        for (int d = 0; d <= 9 && ok; d++)
            if (_digitSprites[d] == null) ok = false;
        if (!ok)
        {
            _killSpritesMissing = true;
            Debug.LogError("[BattleSideHud] Resources/Icons/击败 下缺「击败/0~9」图片——击败计数无法显示");
        }
    }

    void EnsureKillDigits(int count)
    {
        while (_killDigits.Count < count)
        {
            var go = new GameObject("KillDigit" + _killDigits.Count, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_comboScaleRoot, false);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(KillDigitW, KillDigitH);
            _killDigits.Add(img);
        }
    }

    /// <summary>按数值点亮数字图（三位补零 001/033；超过 999 自动加位），标签贴在数字左侧。</summary>
    void ApplyKillDigits(int value)
    {
        if (_killSpritesMissing || _killLabel == null) return;
        string s = value > 0 ? value.ToString("D3") : "000";
        EnsureKillDigits(s.Length);
        for (int i = 0; i < s.Length; i++)
            _killDigits[i].sprite = _digitSprites[s[i] - '0'];
        LayoutKillCounter(s.Length);
    }

    /// <summary>数字从右缘往左排；「击败」标签贴最左位数字左边，整块右对齐。</summary>
    void LayoutKillCounter(int digitCount)
    {
        float digitsW = digitCount * KillDigitW + (digitCount - 1) * KillDigitGap;
        for (int i = 0; i < _killDigits.Count; i++)
        {
            bool used = i < digitCount;
            _killDigits[i].enabled = used && _killDigits[i].sprite != null;
            if (!used) continue;
            var rt = _killDigits[i].rectTransform;
            rt.anchoredPosition = new Vector2(-((digitCount - 1 - i) * (KillDigitW + KillDigitGap)) - KillDigitW * 0.5f, 0f);
            rt.sizeDelta = new Vector2(KillDigitW, KillDigitH);
        }
        if (_killLabel != null)
        {
            var rt = _killLabel.rectTransform;
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-(digitsW + KillLabelGap), 0f);
            rt.sizeDelta = new Vector2(KillLabelW, KillLabelH);
        }
    }

    /// <summary>
    /// 刷新右上角「击败」累计总数（唯一驱动点 = BattleManager.OnMonsterDead）。
    /// 首杀渐显入场，之后常驻不消失；数字从旧值逐个跳到新值，每跳一格播放放大缩小。
    /// </summary>
    public void SetKillTotal(int total)
    {
        if (_comboGroup == null) return;

        if (total <= 0)
        {
            _comboGroup.alpha = 0f;
            _comboPhase = ComboAnimPhase.Hidden;
            _comboShown = 0;
            _displayCombo = 0;
            _rollTimer = 0f;
            if (_comboScaleRoot != null) _comboScaleRoot.localScale = Vector3.one;
            _comboScale = 1f;
            return;
        }

        bool wasHidden = _comboPhase == ComboAnimPhase.Hidden || _comboPhase == ComboAnimPhase.Gone;
        _comboShown = total;

        if (wasHidden)
        {
            // 首杀：直接渐显 + 显示目标值，随后走放大缩小；不滚动数字
            _displayCombo = total;
            ApplyKillDigits(total);
            _rollTimer = 0f;
            _comboGroup.alpha = 0f;   // 从 FadeIn 起步
            _comboPhase = ComboAnimPhase.FadeIn;
            _comboAnimT = 0f;
            _comboScale = 1f;
            if (_comboScaleRoot != null)
                _comboScaleRoot.localScale = Vector3.one;
            return;
        }

        // 累计推进：数字从当前显示值逐个跳到新值（每跳一格重播放大缩小）
        if (_displayCombo < total)
            _rollTimer = 0f;   // 立刻跳第一格
    }

    /// <summary>新一局清零归位（唯一调用点 = BattleManager.StartNewRunInternal）。</summary>
    public void ResetKillTotal()
    {
        SetKillTotal(0);
    }

    /// <summary>断点续战恢复：开场把累计击杀数直接亮出来（DelayedUpdateSlots 调用）。</summary>
    public void SyncKillTotalFromRunStats()
    {
        int total = BattleManager.Instance != null ? BattleManager.Instance.RunStats.KillCount : 0;
        if (total > 0) SetKillTotal(total);
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
