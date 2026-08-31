using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗内关卡石墩图。
///
/// 流程：进图 → 下一关石墩上的锁晃两下再放大消失 → 滚盘选类型
/// → 类型旗落到当前石墩并挂 btn06 描边 → 玩家点石墩进入。
///
/// 石墩三态：
/// · 当前可打：Banner + Icon + btn06 描边，可点
/// · 已打过：ClearedMark（变灰旗子），不可点
/// · 未解锁：只有石墩 + Lock，无旗子
///
/// 从下往上打：Pedestal_0 在最下面，默认无锁。
/// </summary>
public class BattleStageMapUI : MonoBehaviour
{
    public const string PrefabPath = "Prefabs/Battle/BattleStageMap";
    public const int PedestalCount = 10;

    public static BattleStageMapUI Instance { get; private set; }

    [Header("可绑节点")]
    public GameObject root;
    public Image backdrop;
    public Text titleText;
    public Transform pedestalRoot;
    public List<PedestalRefs> pedestals = new List<PedestalRefs>();
    [Tooltip("当前关描边：拖 btn06。空则试 Resources/Materials/btn06")]
    public Material currentOutlineMaterial;

    [Serializable]
    public class PedestalRefs
    {
        public GameObject root;
        public Button button;
        public Image stone;
        public Image banner;
        public Image icon;
        /// <summary>已通关的灰旗（ClearedMark 节点本身就是灰旗图）</summary>
        public GameObject clearedMark;
        /// <summary>未解锁锁头；解锁动画播在这个节点上</summary>
        public GameObject lockIcon;
        public Image outlineTarget; // 挂 btn06 的 Image，默认用 Banner 或 Stone
    }

    StageData _next;
    Action<StageData> _onPick;
    float _prevTimeScale = 1f;
    Coroutine _flow;
    Coroutine _backdropPulse;
    Color _backdropBase = Color.white;
    Material _btn06;
    readonly Dictionary<Image, Material> _savedMats = new Dictionary<Image, Material>();

    /// <summary>进关卡图并跑完整流程（解锁 → 滚盘 → 落旗 → 等点击）。</summary>
    public static void BeginFlow(StageData next, Action<StageData> onPick)
    {
        if (next == null)
        {
            onPick?.Invoke(null);
            return;
        }
        Ensure().StartFlow(next, onPick);
    }

    public static BattleStageMapUI Ensure()
    {
        if (Instance != null) return Instance;

        var prefab = Resources.Load<GameObject>(PrefabPath);
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab);
            go.name = "BattleStageMap";
        }
        else
        {
            Debug.LogWarning($"[BattleStageMap] 未找到预制体 {PrefabPath}，代码搭建临时界面");
            go = new GameObject("BattleStageMap", typeof(RectTransform));
            BuildHierarchy(go);
        }
        DontDestroyOnLoad(go);

        var ui = go.GetComponent<BattleStageMapUI>();
        if (ui == null) ui = go.AddComponent<BattleStageMapUI>();
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

    void StartFlow(StageData next, Action<StageData> onPick)
    {
        BindRefs();
        _next = next;
        _onPick = onPick;

        EnsureCanvas();
        _prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        int chapter = ChapterManager.Instance != null ? ChapterManager.Instance.currentChapter : 1;
        if (titleText != null)
            titleText.text = GameConfig.GetChapterTitleText(chapter);

        // 滚盘前：当前关只显示锁（第 0 关除外），已通关灰旗，未解锁光墩
        RefreshPedestals(Phase.BeforeRoll);

        if (root != null) root.SetActive(true);
        transform.SetAsLastSibling();
        GameFonts.ApplyToHierarchy(transform);

        StartBackdropPulse();
        if (_flow != null) StopCoroutine(_flow);
        _flow = StartCoroutine(CoFlow());
    }

    enum Phase
    {
        BeforeRoll, // 下一关：锁；已通关：灰旗；未来：光墩
        AfterRoll   // 下一关：彩色旗 + btn06；其余同上
    }

    IEnumerator CoFlow()
    {
        int nextIdx = _next != null ? _next.stageIndex : 0;

        // 第 0 关默认没锁，直接进滚盘；其它关先播解锁
        if (nextIdx > 0)
            yield return PlayUnlockAnim(nextIdx);
        else
            yield return WaitUnscaled(0.25f);

        // 滚盘（类型已抽好，这里只演出）
        int chapter = ChapterManager.Instance != null ? ChapterManager.Instance.currentChapter : 1;
        bool rolled = false;
        StageData picked = _next;
        NextStageRouletteUI.Show(chapter, _next, p =>
        {
            if (p != null) picked = p;
            rolled = true;
        });
        while (!rolled) yield return null;

        _next = picked;
        // 类型旗落到当前石墩 + btn06 描边
        RefreshPedestals(Phase.AfterRoll);
        ApplyCurrentOutline(nextIdx);

        _flow = null;
        // 之后等玩家点石墩（OnPedestalClicked）
    }

    IEnumerator PlayUnlockAnim(int index)
    {
        if (index < 0 || index >= pedestals.Count) yield break;
        var p = pedestals[index];
        if (p == null || p.lockIcon == null)
        {
            yield return WaitUnscaled(0.2f);
            yield break;
        }

        var lockTf = p.lockIcon.transform;
        p.lockIcon.SetActive(true);
        Vector3 baseScale = Vector3.one;
        Vector3 basePos = lockTf.localPosition;
        lockTf.localScale = baseScale;

        // 晃两下
        for (int shake = 0; shake < 2; shake++)
        {
            float t = 0f;
            const float dur = 0.22f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / dur;
                float w = Mathf.Sin(t * Mathf.PI * 4f) * (1f - t) * 12f;
                lockTf.localPosition = basePos + new Vector3(w, 0f, 0f);
                lockTf.localRotation = Quaternion.Euler(0f, 0f, w * 0.8f);
                yield return null;
            }
            lockTf.localPosition = basePos;
            lockTf.localRotation = Quaternion.identity;
            yield return WaitUnscaled(0.06f);
        }

        // 放大消失
        float u = 0f;
        const float pop = 0.35f;
        while (u < 1f)
        {
            u += Time.unscaledDeltaTime / pop;
            float e = Mathf.Clamp01(u);
            lockTf.localScale = baseScale * (1f + e * 1.4f);
            SetLockAlpha(p.lockIcon, 1f - e);
            yield return null;
        }

        p.lockIcon.SetActive(false);
        lockTf.localScale = baseScale;
        lockTf.localPosition = basePos;
        SetLockAlpha(p.lockIcon, 1f);
    }

    static void SetLockAlpha(GameObject lockGo, float a)
    {
        if (lockGo == null) return;
        var imgs = lockGo.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < imgs.Length; i++)
        {
            var c = imgs[i].color;
            c.a = a;
            imgs[i].color = c;
        }
    }

    void RefreshPedestals(Phase phase)
    {
        var cm = ChapterManager.Instance;
        int currentDone = cm != null ? cm.currentStageIndex : -1;
        // 刚通关后 currentStageIndex 还是刚打完的那关
        int nextIdx = _next != null ? _next.stageIndex : currentDone + 1;

        ClearAllOutlines();

        for (int i = 0; i < pedestals.Count; i++)
        {
            var p = pedestals[i];
            if (p == null || p.root == null) continue;

            bool cleared = i <= currentDone;
            bool isNext = i == nextIdx;
            bool locked = i > nextIdx;

            // 永远关掉旧的 Highlight（如果预制体里还留着）
            var hi = p.root.transform.Find("Highlight");
            if (hi != null) hi.gameObject.SetActive(false);

            if (isNext)
            {
                if (phase == Phase.BeforeRoll)
                {
                    // 滚盘前：第 0 关无锁直接可点外观；其它关显示锁、暂无旗
                    bool showLock = nextIdx > 0;
                    SetFlag(p, false, false);
                    if (p.clearedMark != null) p.clearedMark.SetActive(false);
                    if (p.lockIcon != null) p.lockIcon.SetActive(showLock);
                    if (p.stone != null) p.stone.color = Color.white;
                    // 解锁动画播完前先不可点；第 0 关可先不可点等滚盘
                    SetClickable(p, false, i);
                }
                else
                {
                    // 滚盘后：彩色旗 + 可点
                    if (p.lockIcon != null) p.lockIcon.SetActive(false);
                    if (p.clearedMark != null) p.clearedMark.SetActive(false);
                    SetFlag(p, true, false);
                    if (_next != null) ApplyTypeIcon(p, _next.type);
                    if (p.stone != null) p.stone.color = Color.white;
                    SetClickable(p, true, i);
                }
            }
            else if (cleared)
            {
                // 已通关：灰旗（ClearedMark）
                if (p.lockIcon != null) p.lockIcon.SetActive(false);
                SetFlag(p, false, false);
                if (p.clearedMark != null) p.clearedMark.SetActive(true);
                if (p.stone != null) p.stone.color = new Color(0.85f, 0.85f, 0.85f, 1f);
                SetClickable(p, false, i);
            }
            else
            {
                // 未解锁：只有石墩 + 锁，无旗
                SetFlag(p, false, false);
                if (p.clearedMark != null) p.clearedMark.SetActive(false);
                if (p.lockIcon != null) p.lockIcon.SetActive(true);
                if (p.stone != null) p.stone.color = new Color(0.55f, 0.55f, 0.55f, 0.95f);
                SetClickable(p, false, i);
            }
        }
    }

    void SetFlag(PedestalRefs p, bool showBanner, bool gray)
    {
        if (p.banner != null)
        {
            p.banner.gameObject.SetActive(showBanner);
            if (showBanner)
                p.banner.color = gray ? new Color(0.45f, 0.45f, 0.45f, 1f) : Color.white;
        }
        if (p.icon != null)
            p.icon.gameObject.SetActive(showBanner);
    }

    void SetClickable(PedestalRefs p, bool on, int index)
    {
        if (p.button == null) return;
        p.button.onClick.RemoveAllListeners();
        p.button.interactable = on;
        if (on)
        {
            int capture = index;
            p.button.onClick.AddListener(() => OnPedestalClicked(capture));
        }
    }

    void ApplyTypeIcon(PedestalRefs p, StageType type)
    {
        Sprite sp = LoadStageIcon(type);
        if (p.icon != null)
        {
            if (sp != null)
            {
                p.icon.sprite = sp;
                p.icon.color = Color.white;
                p.icon.preserveAspect = true;
            }
            else
                p.icon.color = StageRoller.Tint(type);
        }
        if (p.banner != null)
            p.banner.color = Color.white;
    }

    void ApplyCurrentOutline(int index)
    {
        if (index < 0 || index >= pedestals.Count) return;
        var p = pedestals[index];
        if (p == null) return;

        Image target = p.outlineTarget;
        if (target == null) target = p.banner != null ? p.banner : p.stone;
        if (target == null) return;

        Material mat = ResolveBtn06();
        if (mat == null)
        {
            Debug.LogWarning("[BattleStageMap] 找不到 btn06 材质，请在组件上拖入 currentOutlineMaterial");
            return;
        }
        if (!_savedMats.ContainsKey(target))
            _savedMats[target] = target.material;
        target.material = mat;
    }

    void ClearAllOutlines()
    {
        foreach (var kv in _savedMats)
        {
            if (kv.Key != null)
                kv.Key.material = kv.Value;
        }
        _savedMats.Clear();
    }

    Material ResolveBtn06()
    {
        if (currentOutlineMaterial != null) return currentOutlineMaterial;
        if (_btn06 != null) return _btn06;
        _btn06 = Resources.Load<Material>("Materials/btn06");
        if (_btn06 == null) _btn06 = Resources.Load<Material>("btn06");
        return _btn06;
    }

    void OnPedestalClicked(int index)
    {
        if (_next == null || index != _next.stageIndex) return;
        if (_flow != null) return; // 还在解锁/滚盘中

        StopBackdropPulse();
        ClearAllOutlines();
        Time.timeScale = _prevTimeScale > 0.01f ? _prevTimeScale : 1f;
        if (root != null) root.SetActive(false);

        var stage = _next;
        var cb = _onPick;
        _onPick = null;
        _next = null;
        cb?.Invoke(stage);
    }

    public void Hide()
    {
        if (_flow != null)
        {
            StopCoroutine(_flow);
            _flow = null;
        }
        StopBackdropPulse();
        ClearAllOutlines();
        if (root != null) root.SetActive(false);
        Time.timeScale = _prevTimeScale > 0.01f ? _prevTimeScale : 1f;
    }

    // ===== Backdrop 灯光昏暗抖动 =====

    void StartBackdropPulse()
    {
        if (backdrop == null) return;
        _backdropBase = backdrop.color;
        if (_backdropPulse != null) StopCoroutine(_backdropPulse);
        _backdropPulse = StartCoroutine(CoBackdropPulse());
    }

    void StopBackdropPulse()
    {
        if (_backdropPulse != null)
        {
            StopCoroutine(_backdropPulse);
            _backdropPulse = null;
        }
        if (backdrop != null) backdrop.color = _backdropBase;
    }

    IEnumerator CoBackdropPulse()
    {
        // 颜色略变深再回来，不规则一点像火把抖动
        while (true)
        {
            float deep = UnityEngine.Random.Range(0.72f, 0.88f);
            float toDark = UnityEngine.Random.Range(0.35f, 0.7f);
            float toLight = UnityEngine.Random.Range(0.4f, 0.9f);
            yield return LerpBackdrop(_backdropBase, _backdropBase * deep, toDark);
            yield return LerpBackdrop(_backdropBase * deep, _backdropBase, toLight);
            // 偶尔多顿一下
            if (UnityEngine.Random.value < 0.35f)
            {
                float flick = UnityEngine.Random.Range(0.65f, 0.8f);
                yield return LerpBackdrop(_backdropBase, _backdropBase * flick, 0.12f);
                yield return LerpBackdrop(_backdropBase * flick, _backdropBase, 0.18f);
            }
        }
    }

    IEnumerator LerpBackdrop(Color from, Color to, float seconds)
    {
        float t = 0f;
        seconds = Mathf.Max(0.05f, seconds);
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / seconds;
            if (backdrop != null)
            {
                Color c = Color.Lerp(from, to, Mathf.Clamp01(t));
                c.a = _backdropBase.a;
                backdrop.color = c;
            }
            yield return null;
        }
    }

    static IEnumerator WaitUnscaled(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    // ===== 绑定 =====

    void BindRefs()
    {
        if (root == null)
        {
            var t = transform.Find("Root");
            root = t != null ? t.gameObject : gameObject;
        }
        if (backdrop == null)
        {
            var t = root.transform.Find("Backdrop");
            if (t != null) backdrop = t.GetComponent<Image>();
        }
        if (titleText == null)
        {
            var t = FindDeep(root.transform, "Title");
            if (t != null) titleText = t.GetComponent<Text>();
        }
        if (pedestalRoot == null)
        {
            var t = root.transform.Find("Pedestals");
            if (t != null) pedestalRoot = t;
        }

        if (pedestals == null) pedestals = new List<PedestalRefs>();
        if (pedestals.Count < PedestalCount && pedestalRoot != null)
        {
            pedestals.Clear();
            for (int i = 0; i < PedestalCount; i++)
            {
                Transform p = pedestalRoot.Find($"Pedestal_{i}")
                    ?? pedestalRoot.Find($"Stage_{i}")
                    ?? pedestalRoot.Find($"Node_{i}");
                if (p == null) continue;
                pedestals.Add(BindOne(p.gameObject));
            }
        }
        else
        {
            for (int i = 0; i < pedestals.Count; i++)
            {
                if (pedestals[i] != null && pedestals[i].root != null
                    && pedestals[i].lockIcon == null)
                {
                    // 补绑 Lock
                    var L = pedestals[i].root.transform.Find("Lock")
                        ?? pedestals[i].root.transform.Find("LockIcon");
                    if (L != null) pedestals[i].lockIcon = L.gameObject;
                }
            }
        }
    }

    static PedestalRefs BindOne(GameObject go)
    {
        var r = new PedestalRefs { root = go };
        r.button = go.GetComponent<Button>() ?? go.GetComponentInChildren<Button>(true);
        r.stone = go.transform.Find("Stone")?.GetComponent<Image>()
            ?? go.GetComponent<Image>();
        r.banner = go.transform.Find("Banner")?.GetComponent<Image>();
        r.icon = go.transform.Find("Icon")?.GetComponent<Image>()
            ?? go.transform.Find("Banner/Icon")?.GetComponent<Image>();
        var cleared = go.transform.Find("ClearedMark");
        if (cleared != null) r.clearedMark = cleared.gameObject;
        var lockT = go.transform.Find("Lock") ?? go.transform.Find("LockIcon");
        if (lockT != null) r.lockIcon = lockT.gameObject;
        r.outlineTarget = r.banner != null ? r.banner : r.stone;
        // Highlight 不再使用
        var hi = go.transform.Find("Highlight");
        if (hi != null) hi.gameObject.SetActive(false);
        return r;
    }

    void EnsureCanvas()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.BattleStageMap);
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

    static Sprite LoadStageIcon(StageType type)
    {
        string n = StageRoller.IconName(type);
        var sp = Resources.Load<Sprite>($"Art/UI/StageIcons/{n}");
        if (sp == null) sp = Resources.Load<Sprite>($"UI/StageIcons/{n}");
        if (sp == null) sp = Resources.Load<Sprite>($"Art/UI/关卡/{n}");
        return sp;
    }

    // ===== 预制体结构（从下往上：0 在最底）=====

    public static void BuildHierarchy(GameObject host)
    {
        if (host == null) return;
        if (host.GetComponent<RectTransform>() == null)
            host.AddComponent<RectTransform>();

        for (int i = host.transform.childCount - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(host.transform.GetChild(i).gameObject);

        var rootGo = new GameObject("Root", typeof(RectTransform));
        rootGo.transform.SetParent(host.transform, false);
        Stretch(rootGo.GetComponent<RectTransform>());

        var bg = NewImage(rootGo.transform, "Backdrop", new Color(0.12f, 0.08f, 0.16f, 1f));
        Stretch(bg.rectTransform);
        var mapSp = Resources.Load<Sprite>("Art/UI/StageSelect/bg_stage_select");
        if (mapSp != null)
        {
            bg.sprite = mapSp;
            bg.color = Color.white;
        }

        var title = NewText(rootGo.transform, "Title", "第X章", 36, TextAnchor.MiddleCenter);
        title.color = new Color(0.85f, 0.75f, 1f, 1f);
        var trt = title.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -80f);
        trt.sizeDelta = new Vector2(560f, 50f);

        var tip = NewText(rootGo.transform, "Tip", "点击石墩上的关卡继续冒险", 22, TextAnchor.MiddleCenter);
        tip.color = new Color(0.8f, 0.75f, 0.65f, 1f);
        var tipRt = tip.rectTransform;
        tipRt.anchorMin = tipRt.anchorMax = new Vector2(0.5f, 1f);
        tipRt.pivot = new Vector2(0.5f, 1f);
        tipRt.anchoredPosition = new Vector2(0f, -130f);
        tipRt.sizeDelta = new Vector2(560f, 36f);

        var pedRoot = new GameObject("Pedestals", typeof(RectTransform));
        pedRoot.transform.SetParent(rootGo.transform, false);
        Stretch(pedRoot.GetComponent<RectTransform>());

        // Pedestal_0 在最下面，往上到 Pedestal_9
        Vector2[] pos =
        {
            new Vector2( 20f, -440f), // 0 最底
            new Vector2(-80f, -350f),
            new Vector2( 60f, -260f),
            new Vector2(-40f, -170f),
            new Vector2( 80f,  -80f),
            new Vector2(-70f,   10f),
            new Vector2( 50f,  100f),
            new Vector2(-90f,  190f),
            new Vector2( 70f,  280f),
            new Vector2(-30f,  380f), // 9 最上
        };

        var ui = host.GetComponent<BattleStageMapUI>() ?? host.AddComponent<BattleStageMapUI>();
        ui.root = rootGo;
        ui.backdrop = bg;
        ui.titleText = title;
        ui.pedestalRoot = pedRoot.transform;
        ui.pedestals = new List<PedestalRefs>();
        ui.currentOutlineMaterial = Resources.Load<Material>("Materials/btn06");

        for (int i = 0; i < PedestalCount; i++)
            ui.pedestals.Add(BuildPedestal(pedRoot.transform, i, pos[i]));

        GameFonts.ApplyToHierarchy(host.transform);
    }

    static PedestalRefs BuildPedestal(Transform parent, int index, Vector2 anchored)
    {
        var go = new GameObject($"Pedestal_{index}", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(120f, 100f);
        rt.anchoredPosition = anchored;

        var stone = go.GetComponent<Image>();
        stone.color = new Color(0.45f, 0.4f, 0.38f, 1f);
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = stone;

        var banner = NewImage(rt, "Banner", Color.white);
        var brt = banner.rectTransform;
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 1f);
        brt.pivot = new Vector2(0.5f, 0f);
        brt.sizeDelta = new Vector2(72f, 88f);
        brt.anchoredPosition = new Vector2(0f, 8f);
        banner.gameObject.SetActive(false);

        var icon = NewImage(brt, "Icon", Color.white);
        var irt = icon.rectTransform;
        irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
        irt.sizeDelta = new Vector2(48f, 48f);
        irt.anchoredPosition = new Vector2(0f, 6f);
        icon.preserveAspect = true;

        // ClearedMark = 灰旗（换灰旗图）
        var cleared = NewImage(rt, "ClearedMark", new Color(0.4f, 0.4f, 0.4f, 1f));
        var crt = cleared.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 1f);
        crt.pivot = new Vector2(0.5f, 0f);
        crt.sizeDelta = new Vector2(72f, 88f);
        crt.anchoredPosition = new Vector2(0f, 8f);
        cleared.gameObject.SetActive(false);

        // Lock
        var lockImg = NewImage(rt, "Lock", new Color(0.9f, 0.75f, 0.2f, 1f));
        var lrt = lockImg.rectTransform;
        lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
        lrt.sizeDelta = new Vector2(48f, 48f);
        lrt.anchoredPosition = new Vector2(0f, 28f);
        lockImg.gameObject.SetActive(false);

        // 不生成 Highlight

        return new PedestalRefs
        {
            root = go,
            button = btn,
            stone = stone,
            banner = banner,
            icon = icon,
            clearedMark = cleared.gameObject,
            lockIcon = lockImg.gameObject,
            outlineTarget = banner
        };
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
