using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 恢复关弹窗：回复生命（不是体力），一次回复最大生命的 50%。
/// 预制体：Resources/Prefabs/Battle/RestStagePopup
/// 结构按仙泉图搭好，你只换 Backdrop / Panel / Illustration / 按钮图即可。
///
/// <para>【2026-10-11 主人拍板】加效果 + 接主人自己的恢复美术：<br/>
/// · 效果：整窗淡入 → 面板弹跳（0.78→1.12→1）→ 插画呼吸脉冲 → 生命数字从回血前滚到回血后；
///   点「继续冒险 / ×」先淡出再回调（<b>关掉之后才接着走流程</b>，主人原话）。<br/>
/// · 配图真源 = <c>Resources/Art/UI/关卡/恢复/</c>（恢复体力.png / 背景.png），
///   与老虎机、关卡图同一个美术目录（主人「配图早就有了，在关卡文件夹里」）。<br/>
/// · 时间：动画一律用 <c>Time.unscaledDeltaTime</c>（弹窗期间 timeScale=0，用 deltaTime 会整个卡住）。</para>
/// </summary>
public class RestStagePopupUI : MonoBehaviour
{
    public const string PrefabPath = "Prefabs/Battle/RestStagePopup";
    /// <summary>一次回复的生命比例</summary>
    public const float HealRatio = 0.5f;

    public static RestStagePopupUI Instance { get; private set; }

    [Header("按名字自动绑，也可 Inspector 覆盖")]
    public GameObject root;
    public Image panel;
    public Image illustration;
    public Text titleText;
    public Text flavorText;
    public Text statusText;
    public Text hpValueText;
    public Image statusIcon;
    public Button closeButton;
    public Button continueButton;
    public Text continueLabel;

    Action _onContinue;
    float _prevTimeScale = 1f;
    bool _healed;
    bool _open;
    bool _closing;
    CanvasGroup _group;
    float _hpMax;

    public bool IsOpen => root != null && root.activeSelf;

    public static void Show(Action onContinue = null)
    {
        Ensure().Open(onContinue);
    }

    /// <summary>
    /// 优先加载 Resources/Prefabs/Battle/RestStagePopup（用户手做预制体）。
    /// 仅缺失时才代码搭壳；绝不写回覆盖磁盘预制体。
    /// </summary>
    public static RestStagePopupUI Ensure()
    {
        if (Instance != null) return Instance;

        var prefab = Resources.Load<GameObject>(PrefabPath);
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab);
            go.name = "RestStagePopup";
            Debug.Log("[RestStagePopup] 已加载用户预制体: " + PrefabPath);
        }
        else
        {
            Debug.LogWarning($"[RestStagePopup] 未找到预制体 {PrefabPath}，改用代码搭建（不影响磁盘预制体）");
            go = new GameObject("RestStagePopup", typeof(RectTransform));
            BuildHierarchy(go);
        }
        DontDestroyOnLoad(go);

        var ui = go.GetComponent<RestStagePopupUI>();
        if (ui == null) ui = go.AddComponent<RestStagePopupUI>();
        return ui;
    }

    void Awake()
    {
        Instance = this;
        BindRefs();
        if (root != null) root.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void BindRefs()
    {
        if (root == null)
        {
            var t = transform.Find("Root");
            root = t != null ? t.gameObject : gameObject;
        }
        if (panel == null)
        {
            var t = root.transform.Find("Panel");
            if (t != null) panel = t.GetComponent<Image>();
        }
        if (illustration == null)
        {
            var t = FindDeep(root.transform, "Illustration");
            if (t != null) illustration = t.GetComponent<Image>();
        }
        if (titleText == null)
        {
            var t = FindDeep(root.transform, "Title");
            if (t != null) titleText = t.GetComponent<Text>();
        }
        if (flavorText == null)
        {
            var t = FindDeep(root.transform, "FlavorText");
            if (t != null) flavorText = t.GetComponent<Text>();
        }
        if (statusText == null)
        {
            var t = FindDeep(root.transform, "StatusText");
            if (t != null) statusText = t.GetComponent<Text>();
        }
        if (hpValueText == null)
        {
            var t = FindDeep(root.transform, "HpValue");
            if (t != null) hpValueText = t.GetComponent<Text>();
        }
        if (statusIcon == null)
        {
            var t = FindDeep(root.transform, "StatusIcon");
            if (t != null) statusIcon = t.GetComponent<Image>();
        }
        if (closeButton == null)
        {
            var t = FindDeep(root.transform, "CloseButton");
            if (t != null) closeButton = t.GetComponent<Button>();
        }
        if (continueButton == null)
        {
            var t = FindDeep(root.transform, "ContinueButton");
            if (t != null) continueButton = t.GetComponent<Button>();
        }
        if (continueLabel == null && continueButton != null)
        {
            var t = continueButton.transform.Find("Label");
            if (t != null) continueLabel = t.GetComponent<Text>();
        }
    }

    void Open(Action onContinue)
    {
        BindRefs();
        _onContinue = onContinue;
        _healed = false;

        EnsureCanvas();
        _prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        float hpBefore = ReadHeroHp(out _hpMax);
        ApplyHeal();
        GrantRestMaterials();
        AdventureCodex.UnlockWorld("W005");
        RefreshTexts();
        float hpAfter = ReadHeroHp(out _hpMax);

        // —— 效果：淡入 + 面板弹跳 + 生命数字滚动（2026-10-11 主人要的「做下效果」）——
        _open = true;
        _closing = false;
        StopAllCoroutines();
        _group = root != null ? root.GetComponent<CanvasGroup>() : null;
        if (_group == null && root != null) _group = root.AddComponent<CanvasGroup>();
        if (_group != null) _group.alpha = 0f;
        if (panel != null) panel.rectTransform.localScale = Vector3.one * 0.78f;

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(OnContinue);
        }
        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(OnContinue);
        }

        if (root != null) root.SetActive(true);
        transform.SetAsLastSibling();
        GameFonts.ApplyToHierarchy(transform);

        StartCoroutine(CoOpen());
        if (hpAfter > hpBefore + 0.01f) StartCoroutine(CoHpRoll(hpBefore, hpAfter));
    }

    /// <summary>读英雄当前生命与上限（效果里的数字滚动用）。</summary>
    static float ReadHeroHp(out float max)
    {
        max = 0f;
        var hero = BattleManager.Instance != null ? BattleManager.Instance.hero : null;
        if (hero == null || hero.attr == null) return 0f;
        max = hero.attr.GetAttr(AttrType.MaxHp);
        return hero.currentHp;
    }

    IEnumerator CoOpen()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / 0.28f;
            if (_group != null) _group.alpha = Mathf.Clamp01(t);
            yield return null;
        }
        if (_group != null) _group.alpha = 1f;

        // 弹跳：0.78 → 1.12 → 1
        float p = 0f;
        while (p < 1f)
        {
            p += Time.unscaledDeltaTime / 0.34f;
            float u = Mathf.Clamp01(p);
            float s = 0.78f + 0.34f * Mathf.Sin(u * Mathf.PI * 0.5f);
            if (u > 0.6f) s = Mathf.Lerp(1.12f, 1f, (u - 0.6f) / 0.4f);
            if (panel != null) panel.rectTransform.localScale = Vector3.one * s;
            yield return null;
        }
        if (panel != null) panel.rectTransform.localScale = Vector3.one;

        StartCoroutine(CoPulse());
    }

    /// <summary>插画呼吸 + 状态图标脉冲（弹窗开着就一直跑）。</summary>
    IEnumerator CoPulse()
    {
        while (_open && !_closing)
        {
            float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.4f);
            if (illustration != null)
                illustration.rectTransform.localScale = Vector3.one * (1f + k * 0.06f);
            if (statusIcon != null)
                statusIcon.rectTransform.localScale = Vector3.one * (0.94f + k * 0.14f);
            yield return null;
        }
    }

    /// <summary>生命数字从回血前滚到回血后，让「回了多少」看得见。</summary>
    IEnumerator CoHpRoll(float from, float to)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / 0.6f;
            float v = Mathf.Lerp(from, to, Mathf.Clamp01(t));
            if (hpValueText != null)
                hpValueText.text = $"{Mathf.CeilToInt(v)} / {Mathf.CeilToInt(_hpMax)}";
            yield return null;
        }
        if (hpValueText != null)
            hpValueText.text = $"{Mathf.CeilToInt(to)} / {Mathf.CeilToInt(_hpMax)}";
    }

    /// <summary>给英雄 + 所有存活佣兵各回 50% 最大生命。</summary>
    void ApplyHeal()
    {
        if (_healed) return;
        _healed = true;

        var bm = BattleManager.Instance;
        if (bm == null) return;

        HealUnit(bm.hero);
        if (bm.allyUnits != null)
        {
            for (int i = 0; i < bm.allyUnits.Count; i++)
                HealUnit(bm.allyUnits[i]);
        }
        BattleUI.Instance?.UpdateCharacterSlots();
    }

    void GrantRestMaterials()
    {
        // 【2026-10-18 主人拍板：材料产出全部改发抽奖币】原发强化材料 → 按锚价折成抽奖币（结算「抽奖币」栏同账）
        var bm = BattleManager.Instance;
        int stageIdx = bm != null && bm.currentStage != null ? bm.currentStage.stageIndex : 0;
        int chapter = bm != null ? bm.CurrentChapter : 1;
        int gold = StageRoller.RestRewardGold(stageIdx, chapter);
        if (gold <= 0) return;
        if (bm != null)
            bm.GrantRunGold(gold);
        else
            ResourceWallet.Add(ResourceWallet.ResourceType.Gold, gold, save: true, notify: true);
        UIManager.Instance?.ShowToast($"休息补给：金币 +{gold}");   // 【2026-10-18 双钱包拆分】休息关折金进「本局获得金币」（顶部那本账）
    }

    static void HealUnit(UnitBase u)
    {
        if (u == null || u.isDead || u.attr == null) return;
        float max = u.attr.GetAttr(AttrType.MaxHp);
        if (max <= 0.01f) return;
        float add = max * HealRatio;
        u.currentHp = Mathf.Min(max, u.currentHp + add);
    }

    void RefreshTexts()
    {
        var hero = BattleManager.Instance != null ? BattleManager.Instance.hero : null;
        float cur = 0f, max = 0f;
        if (hero != null && hero.attr != null)
        {
            max = hero.attr.GetAttr(AttrType.MaxHp);
            cur = hero.currentHp;
        }

        if (titleText != null) titleText.text = "生命恢复";
        if (flavorText != null) flavorText.text = "仙泉的力量恢复了你的生命！";
        if (statusText != null) statusText.text = "生命已恢复";
        if (hpValueText != null)
        {
            hpValueText.font = GameFonts.GetNumber();
            hpValueText.text = $"{Mathf.CeilToInt(cur)} / {Mathf.CeilToInt(max)}";
        }
        if (continueLabel != null) continueLabel.text = "继续冒险";
    }

    /// <summary>
    /// 玩家点「继续冒险 / ×」：先播淡出，<b>播完才回调</b> ——
    /// 【2026-10-11 主人拍板】「等玩家关闭回复界面弹窗后再开始走流程」，回调不能抢在动画前面。
    /// </summary>
    void OnContinue()
    {
        if (_closing) return;
        _closing = true;
        StartCoroutine(CoClose());
    }

    IEnumerator CoClose()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / 0.22f;
            float u = Mathf.Clamp01(t);
            if (_group != null) _group.alpha = 1f - u;
            if (panel != null) panel.rectTransform.localScale = Vector3.one * (1f + u * 0.10f);
            yield return null;
        }

        _open = false;
        // 复原现场，免得下次打开带着上次的残影 / 缩放
        if (_group != null) _group.alpha = 1f;
        if (panel != null) panel.rectTransform.localScale = Vector3.one;
        if (illustration != null) illustration.rectTransform.localScale = Vector3.one;
        if (statusIcon != null) statusIcon.rectTransform.localScale = Vector3.one;

        Time.timeScale = _prevTimeScale > 0.01f ? _prevTimeScale : 1f;
        if (root != null) root.SetActive(false);
        var cb = _onContinue;
        _onContinue = null;
        cb?.Invoke();
    }

    void EnsureCanvas()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        // 【2026-10-20】层级抬到技槽(921)之上（RestStagePopup=930）：弹窗用 BattlePopup(920) 时，
        // 技槽嵌套 Canvas 会穿到弹窗前面（主人报「技能槽怎么又跑到前面来了」）。
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.RestStagePopup);
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();
    }

    static void EnsureEventSystem()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null) return;
        var go = new GameObject("EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.EventSystems.StandaloneInputModule));
        go.hideFlags = HideFlags.DontSave;
    }

    // ===== 预制体结构（Editor / 运行时兜底共用）=====

    /// <summary>
    /// 节点名固定，方便换美术：
    /// Root/Dim、Panel、CloseButton、Title、TitleDecorL/R、
    /// Illustration、FlavorText、StatusRow(StatusIcon/StatusText/HpValue)、ContinueButton
    /// </summary>
    public static void BuildHierarchy(GameObject host)
    {
        if (host == null) return;
        var rootRt = host.GetComponent<RectTransform>();
        if (rootRt == null) rootRt = host.AddComponent<RectTransform>();

        for (int i = host.transform.childCount - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(host.transform.GetChild(i).gameObject);

        var rootGo = new GameObject("Root", typeof(RectTransform));
        rootGo.transform.SetParent(host.transform, false);
        Stretch(rootGo.GetComponent<RectTransform>());

        var dim = NewImage(rootGo.transform, "Dim", new Color(0f, 0f, 0f, 0.72f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        // 外框：深色木纹占位，你换成带边框的图
        var panelImg = NewImage(rootGo.transform, "Panel", new Color(0.18f, 0.14f, 0.12f, 0.98f));
        var prt = panelImg.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(620f, 920f);
        prt.anchoredPosition = Vector2.zero;

        // 内衬（主人美术：Resources/Art/UI/关卡/恢复/背景.png，没图就保持深色衬底）
        var inner = NewImage(prt, "Inner", new Color(0.10f, 0.12f, 0.18f, 1f));
        Stretch(inner.rectTransform);
        inner.rectTransform.offsetMin = new Vector2(18f, 18f);
        inner.rectTransform.offsetMax = new Vector2(-18f, -18f);
        var innerSp = Resources.Load<Sprite>("Art/UI/关卡/恢复/背景");
        if (innerSp != null)
        {
            inner.sprite = innerSp;
            inner.color = Color.white;
        }

        // 关闭
        var closeImg = NewImage(prt, "CloseButton", new Color(0.45f, 0.22f, 0.2f, 1f));
        closeImg.raycastTarget = true;
        var crt = closeImg.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(1f, 1f);
        crt.sizeDelta = new Vector2(64f, 64f);
        crt.anchoredPosition = new Vector2(-12f, -12f);
        var closeBtn = closeImg.gameObject.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        var closeX = NewText(crt, "Label", "×", 36, TextAnchor.MiddleCenter);
        Stretch(closeX.rectTransform);

        // 标题（生命恢复，不是体力）
        var title = NewText(prt, "Title", "生命恢复", 40, TextAnchor.MiddleCenter);
        title.color = new Color(0.45f, 0.75f, 1f, 1f);
        var trt = title.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -48f);
        trt.sizeDelta = new Vector2(360f, 52f);

        var decorL = NewImage(prt, "TitleDecorL", new Color(0.35f, 0.65f, 0.95f, 0.9f));
        var dl = decorL.rectTransform;
        dl.anchorMin = dl.anchorMax = new Vector2(0.5f, 1f);
        dl.pivot = new Vector2(0.5f, 0.5f);
        dl.sizeDelta = new Vector2(28f, 28f);
        dl.anchoredPosition = new Vector2(-150f, -72f);

        var decorR = NewImage(prt, "TitleDecorR", new Color(0.35f, 0.65f, 0.95f, 0.9f));
        var dr = decorR.rectTransform;
        dr.anchorMin = dr.anchorMax = new Vector2(0.5f, 1f);
        dr.pivot = new Vector2(0.5f, 0.5f);
        dr.sizeDelta = new Vector2(28f, 28f);
        dr.anchoredPosition = new Vector2(150f, -72f);

        // 插画区：把仙泉图拖到 Illustration 的 Image.sprite
        var illuBg = NewImage(prt, "IllustrationFrame", new Color(0.08f, 0.1f, 0.14f, 1f));
        var ifr = illuBg.rectTransform;
        ifr.anchorMin = ifr.anchorMax = new Vector2(0.5f, 1f);
        ifr.pivot = new Vector2(0.5f, 1f);
        ifr.anchoredPosition = new Vector2(0f, -110f);
        ifr.sizeDelta = new Vector2(540f, 420f);

        var illu = NewImage(ifr, "Illustration", new Color(0.25f, 0.35f, 0.45f, 0.55f));
        Stretch(illu.rectTransform);
        illu.rectTransform.offsetMin = new Vector2(8f, 8f);
        illu.rectTransform.offsetMax = new Vector2(-8f, -8f);
        illu.preserveAspect = true;
        // 【2026-10-11 主人「配图早就有了，在关卡文件夹里」】
        // 恢复关插画真源 = Resources/Art/UI/关卡/恢复/恢复体力.png（旧的 RestStage/illustration 全库没有这张图）。
        var sp = Resources.Load<Sprite>("Art/UI/关卡/恢复/恢复体力");
        if (sp != null)
        {
            illu.sprite = sp;
            illu.color = Color.white;
        }

        // 文案
        var flavor = NewText(prt, "FlavorText", "仙泉的力量恢复了你的生命！", 26, TextAnchor.MiddleCenter);
        flavor.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        var frt = flavor.rectTransform;
        frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0.5f);
        frt.anchoredPosition = new Vector2(0f, -80f);
        frt.sizeDelta = new Vector2(520f, 40f);

        var statusRow = new GameObject("StatusRow", typeof(RectTransform));
        statusRow.transform.SetParent(prt, false);
        var srt = statusRow.GetComponent<RectTransform>();
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
        srt.sizeDelta = new Vector2(520f, 48f);
        srt.anchoredPosition = new Vector2(0f, -140f);

        var icon = NewImage(srt, "StatusIcon", new Color(0.35f, 0.85f, 0.45f, 1f));
        var irt = icon.rectTransform;
        irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
        irt.pivot = new Vector2(0f, 0.5f);
        irt.sizeDelta = new Vector2(40f, 40f);
        irt.anchoredPosition = new Vector2(40f, 0f);

        var status = NewText(srt, "StatusText", "生命已恢复", 26, TextAnchor.MiddleLeft);
        var stRt = status.rectTransform;
        stRt.anchorMin = stRt.anchorMax = new Vector2(0f, 0.5f);
        stRt.pivot = new Vector2(0f, 0.5f);
        stRt.sizeDelta = new Vector2(220f, 40f);
        stRt.anchoredPosition = new Vector2(96f, 0f);

        var hp = NewText(srt, "HpValue", "0 / 0", 28, TextAnchor.MiddleRight);
        hp.font = GameFonts.GetNumber();
        hp.color = new Color(1f, 0.92f, 0.45f, 1f);
        var hrt = hp.rectTransform;
        hrt.anchorMin = hrt.anchorMax = new Vector2(1f, 0.5f);
        hrt.pivot = new Vector2(1f, 0.5f);
        hrt.sizeDelta = new Vector2(200f, 40f);
        hrt.anchoredPosition = new Vector2(-40f, 0f);

        // 继续冒险
        var contImg = NewImage(prt, "ContinueButton", new Color(0.22f, 0.55f, 0.32f, 1f));
        contImg.raycastTarget = true;
        var contRt = contImg.rectTransform;
        contRt.anchorMin = contRt.anchorMax = new Vector2(0.5f, 0f);
        contRt.pivot = new Vector2(0.5f, 0f);
        contRt.sizeDelta = new Vector2(420f, 84f);
        contRt.anchoredPosition = new Vector2(0f, 48f);
        var contBtn = contImg.gameObject.AddComponent<Button>();
        contBtn.targetGraphic = contImg;
        var contLabel = NewText(contRt, "Label", "继续冒险", 32, TextAnchor.MiddleCenter);
        contLabel.color = new Color(1f, 0.95f, 0.75f, 1f);
        Stretch(contLabel.rectTransform);

        var ui = host.GetComponent<RestStagePopupUI>();
        if (ui == null) ui = host.AddComponent<RestStagePopupUI>();
        ui.root = rootGo;
        ui.panel = panelImg;
        ui.illustration = illu;
        ui.titleText = title;
        ui.flavorText = flavor;
        ui.statusText = status;
        ui.hpValueText = hp;
        ui.statusIcon = icon;
        ui.closeButton = closeBtn;
        ui.continueButton = contBtn;
        ui.continueLabel = contLabel;

        GameFonts.ApplyToHierarchy(host.transform);
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var f = FindDeep(root.GetChild(i), name);
            if (f != null) return f;
        }
        return null;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Image NewImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static Text NewText(Transform parent, string name, string content, int size, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = GameFonts.GetChinese();
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }
}
