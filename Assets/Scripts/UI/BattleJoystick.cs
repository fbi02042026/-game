using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 战斗虚拟摇杆：触摸区=下方装备栏/背包整区；按下时在触点生成浮动摇杆，松手回背包中部常显（半透）。
/// 「整理/确定」提到最上层以免被挡。按下优先手动，松手近战最近 / 远程最强。
/// </summary>
public class BattleJoystick : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    public static BattleJoystick Instance { get; private set; }

    const string ArtRoot = "Assets/Art/UI/摇杆/";
    const string ResourcesPath = "UI/Joystick/";

    RectTransform _pad;
    RectTransform _stickBase;
    RectTransform _knob;
    CanvasGroup _group;
    CanvasGroup _stickGroup;
    bool _held;
    public bool IsHeld => _held;
    const float MaxRadius = 146f;
    const float IdleYFallback = 280f;
    const float StickAlpha = 0.7f;

    // —— 2026-09-24 主人要求：摇杆一直显示，只有「战斗结束、可以调整」时（整理阶段 / 不可操作）才消失——
    // 整体显隐统一由 _group 控制（SetVisible(false) 会连同摇杆头一起隐藏）；
    // 摇杆头自身不再做「松手淡出」，保持 StickAlpha 常显（2026-09-22 的「按住显示、松手隐藏」作废）。
    /// <summary>进场预览时长：这段时间摇杆常显，过后继续常显（不再淡出）。</summary>
    const float IntroShowSeconds = 3f;
    float _introHideAt = -1f;

    /// <summary>进场/SetVisible(true) 时调用：常显 IntroShowSeconds 秒。</summary>
    public void BeginIntroPreview()
    {
        _introHideAt = Time.unscaledTime + IntroShowSeconds;
        ShowStickVisual();
    }

    float ResolveIdleY()
    {
        float h = _pad != null ? _pad.rect.height : 0f;
        return h > 1f ? h * 0.5f : IdleYFallback;
    }

    public static BattleJoystick EnsureOn(Transform battleUiRoot)
    {
        if (Instance != null)
        {
            Instance.RaiseOrganizeAbove();
            return Instance;
        }
        if (battleUiRoot == null) return null;

        Transform backpack = FindDeep(battleUiRoot, "BackpackPanel");
        if (backpack == null) backpack = battleUiRoot;

        var go = new GameObject("BattleJoystick", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        go.transform.SetParent(backpack, false);
        var pad = go.GetComponent<RectTransform>();
        pad.anchorMin = Vector2.zero;
        pad.anchorMax = Vector2.one;
        pad.offsetMin = Vector2.zero;
        pad.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.01f);
        img.raycastTarget = true;

        var joy = go.AddComponent<BattleJoystick>();
        joy._pad = pad;
        joy._group = go.GetComponent<CanvasGroup>();
        joy.BuildKnob(go.transform);
        joy.ResetStickIdle();
        joy.RaiseOrganizeAbove();
        joy.EnsureAboveSkillBar();
        joy.BeginIntroPreview();
        return joy;
    }

    /// <summary>
    /// 2026-09-22：摇杆必须**始终**压过 SkillBar（技能栏被抬到 BackpackPanel+1 的嵌套 Canvas，
    /// 单靠 sibling 排序会被它盖住——这就是「技能 UI 跑到摇杆上面」的根源）。
    /// 给摇杆也补一个 overrideSorting 嵌套 Canvas，order 取「父 Canvas + 2」。
    /// </summary>
    void EnsureAboveSkillBar()
    {
        if (GetComponent<Canvas>() != null) return;
        var parentCanvas = GetComponentInParent<Canvas>();
        int order = 0;
        if (parentCanvas != null)
        {
            // parentCanvas 可能就是摇杆自己刚被加的？不会——此刻还没加。取 BackpackPanel 链上的
            order = parentCanvas.sortingOrder + 2;
        }
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = order;
    }

    void Awake()
    {
        Instance = this;
        if (_group == null) _group = GetComponent<CanvasGroup>();
        if (_pad == null) _pad = GetComponent<RectTransform>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Hero.Instance?.ClearManualMove();
    }

    void BuildKnob(Transform parent)
    {
        var baseGo = new GameObject("StickBase", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        baseGo.transform.SetParent(parent, false);
        _stickBase = baseGo.GetComponent<RectTransform>();
        // 闲置默认背包垂直中部；操作时移到触点
        _stickBase.anchorMin = new Vector2(0.5f, 0f);
        _stickBase.anchorMax = new Vector2(0.5f, 0f);
        _stickBase.pivot = new Vector2(0.5f, 0.5f);
        _stickBase.sizeDelta = new Vector2(286f, 286f);
        _stickBase.anchoredPosition = new Vector2(0f, ResolveIdleY());
        var baseImg = baseGo.GetComponent<Image>();
        baseImg.raycastTarget = false;
        ApplyJoystickSprite(baseImg, "di", new Color(0.1f, 0.12f, 0.16f, 0.4f));
        _stickGroup = baseGo.GetComponent<CanvasGroup>();
        _stickGroup.blocksRaycasts = false;
        _stickGroup.alpha = StickAlpha;

        var knobGo = new GameObject("StickKnob", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        knobGo.transform.SetParent(baseGo.transform, false);
        _knob = knobGo.GetComponent<RectTransform>();
        _knob.anchorMin = new Vector2(0.5f, 0.5f);
        _knob.anchorMax = new Vector2(0.5f, 0.5f);
        _knob.pivot = new Vector2(0.5f, 0.5f);
        _knob.sizeDelta = new Vector2(156f, 156f);
        _knob.anchoredPosition = Vector2.zero;
        var knobImg = knobGo.GetComponent<Image>();
        knobImg.raycastTarget = false;
        ApplyJoystickSprite(knobImg, "gan", new Color(0.95f, 0.85f, 0.35f, 0.75f));
    }

    static void ApplyJoystickSprite(Image img, string fileStem, Color fallbackTint)
    {
        if (img == null) return;
        var sp = LoadJoystickSprite(fileStem);
        if (sp != null)
        {
            img.sprite = sp;
            img.color = Color.white;
            img.preserveAspect = true;
        }
        else
            img.color = fallbackTint;
    }

    static Sprite LoadJoystickSprite(string fileStem)
    {
        if (string.IsNullOrEmpty(fileStem)) return null;
        string resPath = ResourcesPath + fileStem;
        var sp = Resources.Load<Sprite>(resPath);
        if (sp != null) return sp;
        var tex = Resources.Load<Texture2D>(resPath);
        if (tex != null)
        {
            var made = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            made.name = fileStem;
            return made;
        }
#if UNITY_EDITOR
        string assetPath = ArtRoot + fileStem + ".png";
        var ed = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (ed != null) return ed;
        var edAll = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        if (edAll != null)
        {
            for (int i = 0; i < edAll.Length; i++)
            {
                if (edAll[i] is Sprite s) return s;
            }
        }
        var edTex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (edTex != null)
        {
            var made = Sprite.Create(edTex, new Rect(0f, 0f, edTex.width, edTex.height), new Vector2(0.5f, 0.5f), 100f);
            made.name = fileStem;
            return made;
        }
#endif
        return null;
    }

    void PlaceStickAtPointer(PointerEventData eventData)
    {
        if (_pad == null || _stickBase == null) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _pad, eventData.position, eventData.pressEventCamera, out Vector2 local))
            return;

        // 相对 pad 中心的本地坐标 → 用 stretch 锚点下的 anchoredPosition
        // pad 为 stretch 满铺时，local 原点在中心；StickBase 用底中锚点，需换算
        float padW = _pad.rect.width;
        float padH = _pad.rect.height;
        // local: 相对 pivot(0.5,0.5) 的坐标；底中锚点 anchored = (local.x, local.y + padH*0.5)
        _stickBase.anchoredPosition = new Vector2(local.x, local.y + padH * 0.5f);
        if (_knob != null) _knob.anchoredPosition = Vector2.zero;
        ShowStickVisual();
    }

    void ShowStickVisual()
    {
        if (_stickGroup == null) return;
        _stickGroup.alpha = StickAlpha;
    }

    /// <summary>松手：回背包中部 Idle 并**隐藏**，等下次按住再出现（2026-09-22 主人要求）。</summary>
    void ResetStickToIdlePos()
    {
        if (_knob != null) _knob.anchoredPosition = Vector2.zero;
        if (_stickBase != null)
            _stickBase.anchoredPosition = new Vector2(0f, ResolveIdleY());
    }

    /// <summary>
    /// 松手 / 预览结束：不再淡出，保持常显（主人 2026-09-24 要求「摇杆一直显示」）。
    /// 真正要隐藏走 <see cref="SetVisible"/>(false) / TickAutoVisibility 关掉 _group。
    /// </summary>
    void HideStickVisual()
    {
        if (_stickGroup != null) _stickGroup.alpha = StickAlpha;
    }

    void Update()
    {
        // 每帧自愈摇杆交互开关：系统就绪后即便外部很久没调 SetVisible，也能自行把摇杆打开
        TickAutoVisibility();
        // 进场预览时间到（且没按着）→ 收掉，等玩家按住再显示
        if (!_held && _introHideAt > 0f && Time.unscaledTime >= _introHideAt)
        {
            _introHideAt = -1f;
            HideStickVisual();
        }
    }

    /// <summary>
    /// 每帧自愈摇杆的「交互可见」开关：只操作 <see cref="_group"/>（整体 alpha / 射线 / 可交互），
    /// 不碰 <c>_stickGroup</c> 视觉、不调 BeginIntroPreview / ResetStickToIdlePos /
    /// RaiseOrganizeAbove / EnsureAboveSkillBar（这些有副作用，每帧调会刷爆）。
    /// 判定条件沿用 SetVisible 既有逻辑；bm 用 InstanceQuiet 取，避免在系统未装配时刷 Error；
    /// bm 为 null（系统尚未就绪）时保持当前开关状态，不主动关。
    /// 关键：保留「按住显示、松手隐藏」的视觉行为，仅修开关链。
    /// </summary>
    void TickAutoVisibility()
    {
        var bm = BattleManager.InstanceQuiet;
        if (bm == null) return;   // 系统尚未装配：保留当前状态，交回首帧/外部校正

        bool show = !BattleLootMode.Active && bm.isInBattle && bm.UnitsCanAct;
        if (_group == null) return;

        // 状态未变则跳过，避免每帧重复写属性
        if (_group.alpha == (show ? 1f : 0f)
            && _group.blocksRaycasts == show
            && _group.interactable == show)
            return;

        // 关闭时若正按着，按现有 SetVisible 里的逻辑释放
        if (!show && _held)
            ForceRelease(false);

        _group.alpha = show ? 1f : 0f;
        _group.blocksRaycasts = show;
        _group.interactable = show;

        // 自愈打开时补齐层级，避免压在 zhezhao 之下
        if (show)
        {
            ResetStickToIdlePos();
            RaiseOrganizeAbove();
            EnsureAboveSkillBar();
        }
    }

    /// <summary>
    /// 把「确定」按钮和摇杆压到最上层。
    /// 注意：BackpackPanel 下现在有战斗遮罩 zhezhao，摇杆必须排在它<b>之后</b>，
    /// 否则射线先被遮罩吃掉，战斗中摇杆完全拖不动（需求是摇杆在遮罩之上）。
    /// </summary>
    public void RaiseOrganizeAbove()
    {
        var p = transform.parent;
        if (p == null) return;

        Transform mask = null;
        for (int i = 0; i < p.childCount; i++)
        {
            var c = p.GetChild(i);
            if (c == transform) continue;
            if (c != null && c.name.Equals("zhezhao", System.StringComparison.OrdinalIgnoreCase))
            {
                mask = c;
                break;
            }
        }
        transform.SetSiblingIndex(mask != null
            ? mask.GetSiblingIndex() + 1
            : Mathf.Max(0, p.childCount - 2));

        // 确定按钮最后再压一层，保证在摇杆之上（它只在整理阶段出现，那时摇杆已隐藏）
        var ui = BattleUI.Instance;
        if (ui != null && ui.lootConfirmButton != null)
            ui.lootConfirmButton.transform.SetAsLastSibling();
    }

    public RectTransform StickHighlight
    {
        get
        {
            if (_stickBase != null)
            {
                ShowStickVisual();
                // 引导指着摇杆期间别让它自动收起：续一次预览时长
                _introHideAt = Time.unscaledTime + IntroShowSeconds;
                return _stickBase;
            }
            return _pad;
        }
    }

    public void ResetStickIdle() => ResetStickToIdlePos();

    public void SetVisible(bool on)
    {
        if (_group == null) return;
        bool show = on && !BattleLootMode.Active;
        _group.alpha = show ? 1f : 0f;
        _group.blocksRaycasts = show;
        _group.interactable = show;
        if (!show && _held)
            ForceRelease(false);
        if (show)
        {
            ResetStickToIdlePos();
            RaiseOrganizeAbove();
            EnsureAboveSkillBar();
            BeginIntroPreview();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (BattleLootMode.Active) return;
        // 用静默查询：系统未装配时只是不响应，不刷 Error
        var bm = BattleManager.InstanceQuiet;
        if (bm == null || !bm.UnitsCanAct) return;
        _held = true;
        PlaceStickAtPointer(eventData);
        UpdateStick(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_held) return;
        UpdateStick(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_held) return;
        ForceRelease(true);
    }

    void ForceRelease(bool acquire)
    {
        _held = false;
        ResetStickToIdlePos();
        HideStickVisual();          // 2026-09-22：松手就藏，按住再出现
        _introHideAt = -1f;
        Hero.Instance?.ClearManualMove();
        if (acquire)
            Hero.Instance?.BeginAutoAcquireOnRelease();
    }

    void UpdateStick(PointerEventData eventData)
    {
        if (_pad == null || _stickBase == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _pad, eventData.position, eventData.pressEventCamera, out Vector2 local);

        // 底座中心在 pad 本地坐标
        Vector2 center = StickBaseCenterInPad();
        Vector2 delta = local - center;
        if (delta.magnitude > MaxRadius)
            delta = delta.normalized * MaxRadius;
        if (_knob != null)
            _knob.anchoredPosition = delta;

        Vector2 dir = delta.magnitude > 4f ? delta.normalized : Vector2.zero;
        Hero.Instance?.SetManualMove(dir);
    }

    Vector2 StickBaseCenterInPad()
    {
        // StickBase 锚点底中：中心 = (anchored.x, anchored.y - padH/2) 在 pad 本地（相对 pad pivot 中心）
        float padH = _pad.rect.height;
        Vector2 ap = _stickBase.anchoredPosition;
        return new Vector2(ap.x, ap.y - padH * 0.5f);
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        if (string.Equals(root.name, name, System.StringComparison.OrdinalIgnoreCase))
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var f = FindDeep(root.GetChild(i), name);
            if (f != null) return f;
        }
        return null;
    }
}
