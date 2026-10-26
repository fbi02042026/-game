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
    RectTransform _digitRoot;      // 数字独立缩放根：弹跳缩放只作用于数字，「击败」标签不缩放

    Button _waveBtn;
    Text _waveTitle;
    Text _waveTimer;
    Text _waveHint;
    CanvasGroup _waveGroup;

    // 击败计数横向位置：【2026-10-19 主人定位置】靠右缘、右缩 48（逻辑宽恒 720，横向不随屏漂）。
    const float KillCounterRightInset = 48f;
    // 【2026-10-20 主人报「有的手机上击败跑到别处了」】纵向改为跟随 BossBar/血条 的实际位置。
    // 根因：BossBar 原是画布中心锚点（y=477），屏比越瘦逻辑画布越高、血条离屏顶越远；
    // 原写死「距顶 114」只在 720×1280 设计屏成立，瘦屏上计数飘到血条上方、9:16 矮屏上压到血条上。
    // 【2026-10-22 血条偏下修复】BossBar 预制体改贴顶锚（y=-163）并加入刘海避让，不再随屏漂；
    // 折算公式同步改为锚点无关（见 RelayoutKillCounter），720×1280 上结果与旧公式分毫不差（-114）。
    // 现口径：HUD 根顶 = 血条底边上方 54.4（在 720×1280 上与原「距顶 114」分毫不差，其它屏比贴着血条走）。
    const float KillCounterRootGapAboveBarBottom = 54.4f;
    // BossBar/血条 绑定失败时的落点（= 设计屏 720×1280 摆位；会 LogError，不静默换值）。
    const float KillCounterDesignTopOffset = 114f;
    // 击杀反馈：每杀一次「数字」放大一下 → 立马缩回原大（【2026-10-20 主人拍板】「击败」标签不缩放）。
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

    // —— 纵向跟随 BossBar（2026-10-20 修正「有的手机上击败跑到别处了」）——
    RectTransform _bossBarNode;    // BossBar（2026-10-22 起预制体里贴顶锚 y=-163，并加入刘海避让）
    RectTransform _bossBarFill;    // BossBar/血条（红条；与 BattleBossHpBar 绑的是同一个节点名）
    int _layoutHDone = -1;         // 上次完成纵向布局时的画布逻辑高（四舍五入，变了才重算）
    bool _bossBarMissingLogged;    // 绑定失败只报一次错
    bool _counterDiagLogged;       // 击败计数位置诊断只打一次（2026-10-23 主人报「跑到战斗背包下」排查用）

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
        // 初始先落设计屏（720×1280）摆位；随后 Update 里按 BossBar/血条 实际位置重算
        //（见 RelayoutKillCounter，2026-10-20 主人报「有的手机上击败跑到别处了」的修正）。
        root.anchoredPosition = new Vector2(-KillCounterRightInset, -KillCounterDesignTopOffset);
        _layoutHDone = -1;   // 强制下一帧按画布实际高度重算

        // —— 击败累计计数（常驻；图源 Resources/Icons/击败，位置样式按主人 2026-10-19 截图）——
        LoadKillSprites();
        // 【2026-10-20 主人拍板】「击败」字+数字整体下移 50 像素（10+10+10+20，只动这块，下一波倒计时不动）
        // 【2026-10-24 主人拍板「击败字太靠下，应在 Boss 血条右下角」】下挂偏移 -50 → -12；
        // 【2026-10-24 二次拍板「还是太往下了，还得往上几十」】-12 → +28（再上移 40）：
        // 根顶在血条底边上方 54.4、panel 高 70（pivot=顶沿），标签中心≈血条底边上方 47，整块贴住血条右端。
        // 【2026-10-26 主人拍板「血条挡住了击败的字，让字往下 30」】+28 → -2（下移 30）。
        // 要再微调只动这一个数（每 +1 上移 1）。
        const float ComboPanelTopOffset = -2f;
        var comboGo = CreatePanel("ComboPanel", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, ComboPanelTopOffset), new Vector2(250f, 70f), transparent: true);
        _comboGroup = comboGo.AddComponent<CanvasGroup>();
        _comboGroup.alpha = 0f;
        _comboScaleRoot = comboGo.GetComponent<RectTransform>();
        _comboScaleRoot.localScale = Vector3.one;

        // 数字独立缩放根：弹跳缩放只作用于数字，「击败」标签留在外层不缩放。
        // 锚在面板右缘中点、绕右缘缩放——数字向左弹出，不会越过右屏边。
        var digitRootGo = new GameObject("DigitRoot", typeof(RectTransform));
        _digitRoot = (RectTransform)digitRootGo.transform;
        _digitRoot.SetParent(_comboScaleRoot, false);
        _digitRoot.anchorMin = new Vector2(1f, 0.5f);
        _digitRoot.anchorMax = new Vector2(1f, 0.5f);
        _digitRoot.pivot = new Vector2(1f, 0.5f);
        _digitRoot.anchoredPosition = Vector2.zero;
        _digitRoot.sizeDelta = Vector2.zero;

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
        TickKillCounterLayout();   // 画布逻辑高变了就按 BossBar 实际位置重摆（见 2026-10-20 修正注释）
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

        if (_digitRoot != null) _digitRoot.localScale = Vector3.one * _comboScale;   // 只缩数字，「击败」标签不动
    }

    // ────── 击败计数：图片拼数字（图源 Resources/Icons/击败）──────

    /// <summary>
    /// 画布逻辑高变化时按 BossBar/血条 实际位置重摆纵向（横向恒右缩 48 不随屏漂）。
    /// 口径：HUD 根顶 = 血条底边上方 54.4 —— 720×1280 上正好还原「距顶 114」的拍板位，
    /// 瘦屏（逻辑画布更高）上血条下移多少、计数就跟着下移多少，永远贴着血条右端。
    /// </summary>
    void TickKillCounterLayout()
    {
        var parent = transform.parent as RectTransform;
        if (parent == null) return;
        float h = parent.rect.height;
        if (h <= 0f) return;   // 画布还没量好尺寸，等下一帧
        if (Mathf.RoundToInt(h) == _layoutHDone) return;   // 没变不干活
        RelayoutKillCounter(parent, h);
    }

    void RelayoutKillCounter(RectTransform parent, float canvasH)
    {
        _layoutHDone = Mathf.RoundToInt(canvasH);
        if (_bossBarFill == null && !TryBindBossBar(parent))
        {
            if (!_bossBarMissingLogged)
            {
                _bossBarMissingLogged = true;
                Debug.LogError("[BattleSideHud] BattleUI 下找不到 BossBar/血条 —— 击败计数保持设计屏摆位（距顶 114）");
            }
            return;   // 不静默换值：保持 Build 时的设计屏摆位（铁律：缺真值要让我看见）
        }
        // 血条底边（BossBar 本地，向上为正）= 中心 y − 显示半高；
        // 含自身缩放（预制体里血条 localScale=0.7），算法与 BattleBossHpBar.EnsureLeftAligned 同款。
        float barBottomLocal = _bossBarFill.anchoredPosition.y
                             - _bossBarFill.sizeDelta.y * Mathf.Abs(_bossBarFill.localScale.y) * _bossBarFill.pivot.y;
        // 【2026-10-22 血条改贴顶锚】锚点无关折算：锚点参考线 + anchoredPosition = BossBar pivot 点
        // 在画布本地坐标的 y。中心锚 (0.5,0.5)+477 与贴顶锚 (0.5,1)−163 在 720×1280 上算出同一点，
        // 瘦屏上贴顶锚不再随画布中心下漂（血条跟顶栏走，计数跟血条走）。
        float bossPivotY = Mathf.Lerp(parent.rect.min.y, parent.rect.max.y, _bossBarNode.anchorMin.y)
                         + _bossBarNode.anchoredPosition.y;
        // 血条底边（画布本地 y，自底向上）→ HUD 根（锚画布右上、pivot 右上）的 y：根顶在血条底边上方 54.4
        // 【2026-10-24 根因修复】末项减数必须是根节点锚点参考线（rect.max.y，中心原点坐标里画布上边缘=+h/2），
        // 不能减 canvasH —— 原代码把「中心原点坐标」混配「底边原点换算」，整体多下移 h/2
        //（720×1280 差 640、1610 高屏差 805，这就是计数器一直钉在右下角的根因；
        //  团结日志实锤：counterY=-939，正确值应为 -134）。
        // 修复后：720×1280 无刘海 = -114（与最初拍板位分毫不差），其它屏比贴着血条走。
        float anchorLineY = Mathf.Lerp(parent.rect.min.y, parent.rect.max.y,
            ((RectTransform)transform).anchorMin.y);
        float y = bossPivotY + barBottomLocal + KillCounterRootGapAboveBarBottom - anchorLineY;
        ((RectTransform)transform).anchoredPosition = new Vector2(-KillCounterRightInset, y);
        if (!_counterDiagLogged)
        {
            _counterDiagLogged = true;
            var bag = FindChildRecursive(parent, "BtnBackpack") as RectTransform;
            float safeTop = SafeAreaFitter.TotalTopInsetUnits(_bossBarNode as RectTransform);
            Debug.Log($"[BattleSideHud] 击败计数诊断 canvasH={canvasH:F0} " +
                      $"bossBar aMin={_bossBarNode.anchorMin} pos={_bossBarNode.anchoredPosition} " +
                      $"fill pos={_bossBarFill.anchoredPosition} size={_bossBarFill.sizeDelta} scale={_bossBarFill.localScale.y:F2} " +
                      $"safeTop={safeTop:F0} counterY={y:F0} " +
                      $"bag aMin={(bag != null ? bag.anchorMin.ToString() : "无")} bagPos={(bag != null ? bag.anchoredPosition.ToString() : "-")}");
        }
    }

    bool TryBindBossBar(RectTransform parent)
    {
        _bossBarNode = FindChildRecursive(parent, "BossBar") as RectTransform;
        if (_bossBarNode == null) return false;
        _bossBarFill = FindChildRecursive(_bossBarNode, "血条") as RectTransform;
        return _bossBarFill != null;
    }

    static Transform FindChildRecursive(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name)) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var hit = FindChildRecursive(root.GetChild(i), name);
            if (hit != null) return hit;
        }
        return null;
    }

    const string KillIconPath = "Icons/击败";
    // 【2026-10-20 主人拍板】「击败」标签和数字整体放大 30%（38→49.4、124/42→161.2/54.6），间距不动
    const float KillDigitW = 49.4f;  // 数字图显示宽（原图 48x50，等比略缩）
    const float KillDigitH = 49.4f;
    const float KillDigitGap = -12f; // 数字间距（2026-10-20 主人要求数字之间间隔再小点，-12 更紧凑）
    const float KillLabelW = 161.2f; // 「击败」标签显示宽（原图 200x68 等比）
    const float KillLabelH = 54.6f;
    const float KillLabelGap = -26f; // 标签与数字的间距（2026-10-20 主人要求数字离「击败」离开点，-36 收太紧放回 -26）

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
            go.transform.SetParent(_digitRoot, false);
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

    /// <summary>按数值点亮数字图（【2026-10-20 主人拍板】不补零：1 显示 1 位、10 起两位，以此类推），标签贴在数字左侧。</summary>
    void ApplyKillDigits(int value)
    {
        if (_killSpritesMissing || _killLabel == null) return;
        string s = Mathf.Max(0, value).ToString();
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
            if (_digitRoot != null) _digitRoot.localScale = Vector3.one;
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
            if (_digitRoot != null)
                _digitRoot.localScale = Vector3.one;
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
