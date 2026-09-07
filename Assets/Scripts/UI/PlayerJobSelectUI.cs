using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 进裂隙前职业三选一：优先加载 Prefabs/UI/PlayerJobSelect；缺则运行时白模。
/// </summary>
public class PlayerJobSelectUI : MonoBehaviour
{
    public const string PrefabResourcePath = "Prefabs/UI/PlayerJobSelect";

    public static PlayerJobSelectUI Instance { get; private set; }

    public struct Options
    {
        public bool TutorialForceSwordFirst;
        public string HintOverride;
        public bool RequireSwordShield;
    }

    struct CardBind
    {
        public Button Button;
        public Image Frame;
        public Image Icon;
        public Text Name;
        public Text Desc;
        public Text HpStat;
        public Text AtkStat;
        public Text ControlStat;
        public Transform RatingRoot;
    }

    Action _onEnterRift;
    Options _opts;
    GameObject _root;
    Button _refreshBtn;
    Button _enterBtn;
    Text _hint;
    Text _refreshLabel;
    Text _title;
    readonly List<CardBind> _cards = new List<CardBind>();
    readonly List<PlayerJobId> _offer = new List<PlayerJobId>();
    PlayerJobId? _selected;
    bool _refreshUsed;
    bool _built;
    bool _fromPrefab;

    public static void Show(Action onEnterRift, Options opts = default)
    {
        Ensure().Open(onEnterRift, opts);
    }

    static PlayerJobSelectUI Ensure()
    {
        if (Instance != null) return Instance;

        var prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab != null)
        {
            var inst = Instantiate(prefab);
            inst.name = "PlayerJobSelectUI";
            DontDestroyOnLoad(inst);
            // Missing Script 时 GetComponent 为 null，补挂即可按节点名绑定
            var ui = inst.GetComponent<PlayerJobSelectUI>();
            if (ui == null) ui = inst.AddComponent<PlayerJobSelectUI>();
            ui._fromPrefab = HasJobSelectHierarchy(inst.transform);
            return ui;
        }

        var go = new GameObject("PlayerJobSelectUI");
        DontDestroyOnLoad(go);
        return go.AddComponent<PlayerJobSelectUI>();
    }

    static bool HasJobSelectHierarchy(Transform root)
    {
        if (root == null) return false;
        return FindDeep(root, "Panel") != null && FindDeep(root, "JobCard0") != null;
    }

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Editor 白模生成器用：搭建完整层级（含本组件挂在根上）。</summary>
    public void BuildHierarchyForPrefab()
    {
        _fromPrefab = true;
        BuildRuntimeWhitebox(isPrefabBuild: true);
        BindFromHierarchy();
        GameFonts.ApplyToHierarchy(transform);
    }

    void Open(Action onEnterRift, Options opts)
    {
        _onEnterRift = onEnterRift;
        _opts = opts;
        _selected = null;
        _refreshUsed = false;
        BuildIfNeeded();
        RollOffers(forceSwordFirst: opts.TutorialForceSwordFirst);
        RefreshCardsVisual();
        UpdateEnterInteractable();

        bool tut = opts.TutorialForceSwordFirst;
        if (_refreshBtn != null)
        {
            _refreshBtn.gameObject.SetActive(!tut);
            _refreshBtn.interactable = !tut;
        }
        if (_refreshLabel != null)
            _refreshLabel.text = tut ? "引导中不可刷新" : "刷新 (1)";
        if (_hint != null)
        {
            _hint.text = !string.IsNullOrEmpty(opts.HintOverride)
                ? opts.HintOverride
                : "请选择你想成为的职业，踏入裂隙，迎接挑战！";
        }
        if (_title != null)
            _title.text = "选择职业";

        if (_root != null)
        {
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
        }
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        GameFonts.ApplyToHierarchy(transform);
    }

    public void Hide()
    {
        if (_root != null) _root.SetActive(false);
        else gameObject.SetActive(false);
    }

    void BuildIfNeeded()
    {
        if (_built) return;
        _built = true;

        if (_fromPrefab || transform.Find("Dim") != null || transform.Find("Panel") != null)
        {
            BindFromHierarchy();
            EnsureCanvas();
            return;
        }

        BuildRuntimeWhitebox(isPrefabBuild: false);
        BindFromHierarchy();
    }

    void EnsureCanvas()
    {
        var canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();
        }
        if (gameObject.GetComponent<CanvasScaler>() == null)
            gameObject.AddComponent<CanvasScaler>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownPopup);
    }

    void BindFromHierarchy()
    {
        _cards.Clear();
        EnsureCanvas();

        _root = gameObject;
        var panel = FindDeep(transform, "Panel") ?? transform;
        _title = FindText(panel, "Title");
        _hint = FindText(panel, "Hint");

        var refreshGo = FindDeep(panel, "RefreshBtn");
        if (refreshGo != null)
        {
            _refreshBtn = refreshGo.GetComponent<Button>() ?? refreshGo.gameObject.AddComponent<Button>();
            _refreshBtn.onClick.RemoveAllListeners();
            _refreshBtn.onClick.AddListener(OnRefresh);
            _refreshLabel = FindText(refreshGo, "Label");
        }

        var enterGo = FindDeep(panel, "EnterRiftBtn");
        if (enterGo != null)
        {
            _enterBtn = enterGo.GetComponent<Button>() ?? enterGo.gameObject.AddComponent<Button>();
            _enterBtn.onClick.RemoveAllListeners();
            _enterBtn.onClick.AddListener(OnEnterRift);
        }

        for (int i = 0; i < 3; i++)
        {
            var cardTf = FindDeep(panel, "JobCard" + i);
            if (cardTf == null) continue;
            var bind = new CardBind
            {
                Button = cardTf.GetComponent<Button>() ?? cardTf.gameObject.AddComponent<Button>(),
                Frame = FindImage(cardTf, "Frame") ?? cardTf.GetComponent<Image>(),
                Icon = FindImage(cardTf, "Icon"),
                Name = FindText(cardTf, "Name"),
                Desc = FindText(cardTf, "Desc"),
                HpStat = FindStatLineText(cardTf, "血量"),
                AtkStat = FindStatLineText(cardTf, "攻击"),
                ControlStat = FindStatLineText(cardTf, "操控"),
                RatingRoot = FindDeep(cardTf, "Rating")
            };
            // 兼容旧运行时白模 Label / 整块 Stats
            if (bind.Name == null) bind.Name = FindText(cardTf, "Label");
            if (bind.HpStat == null && bind.AtkStat == null && bind.ControlStat == null)
            {
                var legacy = FindText(cardTf, "Stats");
                if (legacy != null) bind.HpStat = legacy;
            }
            int idx = i;
            bind.Button.onClick.RemoveAllListeners();
            bind.Button.onClick.AddListener(() => OnCardClicked(idx));
            _cards.Add(bind);
        }
    }

    /// <summary>手做卡：Stats/血量/Stats(Text)；或节点自身带 Text。</summary>
    static Text FindStatLineText(Transform card, string rowName)
    {
        var row = FindDeep(card, rowName);
        if (row == null) return null;
        var nested = FindText(row, "Stats");
        if (nested != null) return nested;
        var label = FindText(row, "Label");
        if (label != null) return label;
        return row.GetComponent<Text>();
    }

    void BuildRuntimeWhitebox(bool isPrefabBuild)
    {
        EnsureCanvas();

        Transform host = transform;
        // 预制体根即 PlayerJobSelect；运行时补 Root 亦可直接挂 Dim/Panel
        var dim = CreateUi("Dim", host, typeof(Image));
        Stretch(dim.GetComponent<RectTransform>());
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);
        dim.GetComponent<Image>().raycastTarget = true;

        var panel = CreateUi("Panel", host, typeof(Image));
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(680f, 980f);
        panel.GetComponent<Image>().color = new Color(0.78f, 0.68f, 0.48f, 0.98f);

        var title = CreateText(panel.transform, "Title", "选择职业", 36, TextAnchor.MiddleCenter);
        AnchorTop(title.rectTransform, -28f, 600f, 44f);
        title.color = new Color(0.35f, 0.22f, 0.08f);

        var hint = CreateText(panel.transform, "Hint",
            "请选择你想成为的职业，踏入裂隙，迎接挑战！", 18, TextAnchor.MiddleCenter);
        AnchorTop(hint.rectTransform, -78f, 600f, 48f);
        hint.color = new Color(0.35f, 0.28f, 0.18f);
        hint.horizontalOverflow = HorizontalWrapMode.Wrap;

        var cardRow = CreateUi("CardRow", panel.transform, typeof(RectTransform)).transform;
        var rowRt = cardRow.GetComponent<RectTransform>();
        rowRt.anchorMin = rowRt.anchorMax = new Vector2(0.5f, 0.5f);
        rowRt.sizeDelta = new Vector2(640f, 520f);
        rowRt.anchoredPosition = new Vector2(0f, 40f);

        float cardW = 196f;
        float gap = 14f;
        float startX = -((cardW + gap));
        for (int i = 0; i < 3; i++)
        {
            var card = CreateUi("JobCard" + i, cardRow, typeof(Image), typeof(Button));
            var crt = card.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(cardW, 500f);
            crt.anchoredPosition = new Vector2(startX + i * (cardW + gap), 0f);
            card.GetComponent<Image>().color = new Color(0.92f, 0.86f, 0.72f, 1f);

            var frame = CreateUi("Frame", card.transform, typeof(Image));
            Stretch(frame.GetComponent<RectTransform>());
            frame.GetComponent<Image>().color = new Color(0.45f, 0.32f, 0.18f, 0.35f);
            frame.GetComponent<Image>().raycastTarget = false;

            var icon = CreateUi("Icon", card.transform, typeof(Image));
            var irt = icon.GetComponent<RectTransform>();
            irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 1f);
            irt.pivot = new Vector2(0.5f, 1f);
            irt.sizeDelta = new Vector2(140f, 140f);
            irt.anchoredPosition = new Vector2(0f, -24f);
            icon.GetComponent<Image>().color = new Color(0.55f, 0.5f, 0.42f, 1f);
            icon.GetComponent<Image>().raycastTarget = false;

            var name = CreateText(card.transform, "Name", "职业", 24, TextAnchor.MiddleCenter);
            var nrt = name.rectTransform;
            nrt.anchorMin = nrt.anchorMax = new Vector2(0.5f, 1f);
            nrt.pivot = new Vector2(0.5f, 1f);
            nrt.sizeDelta = new Vector2(180f, 36f);
            nrt.anchoredPosition = new Vector2(0f, -176f);
            name.color = new Color(0.2f, 0.12f, 0.06f);
            name.fontStyle = FontStyle.Bold;

            var statsRoot = CreateUi("Stats", card.transform, typeof(RectTransform)).transform;
            var srootRt = statsRoot.GetComponent<RectTransform>();
            srootRt.anchorMin = srootRt.anchorMax = new Vector2(0.5f, 1f);
            srootRt.pivot = new Vector2(0.5f, 1f);
            srootRt.sizeDelta = new Vector2(180f, 100f);
            srootRt.anchoredPosition = new Vector2(0f, -220f);
            CreateStatRow(statsRoot, "血量", "血量 中", 0f);
            CreateStatRow(statsRoot, "攻击", "攻击 中", -32f);
            CreateStatRow(statsRoot, "操控", "操控 中", -64f);

            var desc = CreateText(card.transform, "Desc", "描述", 15, TextAnchor.UpperCenter);
            var drt = desc.rectTransform;
            drt.anchorMin = new Vector2(0.05f, 0.12f);
            drt.anchorMax = new Vector2(0.95f, 0.48f);
            drt.offsetMin = Vector2.zero;
            drt.offsetMax = Vector2.zero;
            desc.color = new Color(0.28f, 0.22f, 0.14f);
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            desc.verticalOverflow = VerticalWrapMode.Truncate;

            var rating = CreateUi("Rating", card.transform, typeof(RectTransform)).transform;
            var rrt = rating.GetComponent<RectTransform>();
            rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0f);
            rrt.pivot = new Vector2(0.5f, 0f);
            rrt.sizeDelta = new Vector2(160f, 32f);
            rrt.anchoredPosition = new Vector2(0f, 16f);
            for (int s = 1; s <= 3; s++)
            {
                var full = CreateUi("满星" + s, rating, typeof(Image));
                var frt = full.GetComponent<RectTransform>();
                frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0.5f);
                frt.sizeDelta = new Vector2(28f, 28f);
                frt.anchoredPosition = new Vector2((s - 2) * 34f, 0f);
                full.GetComponent<Image>().color = new Color(0.95f, 0.75f, 0.2f, 1f);
                var empty = CreateUi("空星", full.transform, typeof(Image));
                Stretch(empty.GetComponent<RectTransform>());
                empty.GetComponent<Image>().color = new Color(0.45f, 0.4f, 0.35f, 0.85f);
            }
        }

        var refreshGo = CreateUi("RefreshBtn", panel.transform, typeof(Image), typeof(Button));
        var refrt = refreshGo.GetComponent<RectTransform>();
        refrt.anchorMin = refrt.anchorMax = new Vector2(0.5f, 0f);
        refrt.pivot = new Vector2(0.5f, 0f);
        refrt.sizeDelta = new Vector2(180f, 52f);
        refrt.anchoredPosition = new Vector2(-150f, 40f);
        refreshGo.GetComponent<Image>().color = new Color(0.4f, 0.32f, 0.5f, 1f);
        var refreshLabel = CreateText(refreshGo.transform, "Label", "刷新 (1)", 22, TextAnchor.MiddleCenter);
        Stretch(refreshLabel.rectTransform);

        var enterGo = CreateUi("EnterRiftBtn", panel.transform, typeof(Image), typeof(Button));
        var ert = enterGo.GetComponent<RectTransform>();
        ert.anchorMin = ert.anchorMax = new Vector2(0.5f, 0f);
        ert.pivot = new Vector2(0.5f, 0f);
        ert.sizeDelta = new Vector2(280f, 72f);
        ert.anchoredPosition = new Vector2(110f, 36f);
        enterGo.GetComponent<Image>().color = new Color(0.45f, 0.22f, 0.55f, 1f);
        var enterLabel = CreateText(enterGo.transform, "Label", "进入裂隙", 28, TextAnchor.MiddleCenter);
        Stretch(enterLabel.rectTransform);
        enterLabel.fontStyle = FontStyle.Bold;

        if (isPrefabBuild)
            _built = true;
        _ = isPrefabBuild;
    }

    void RollOffers(bool forceSwordFirst)
    {
        _offer.Clear();
        var pool = new List<PlayerJobId>();
        for (int i = 0; i < PlayerJobDefs.All.Length; i++)
            pool.Add(PlayerJobDefs.All[i].Id);

        if (forceSwordFirst)
        {
            _offer.Add(PlayerJobId.SwordShield);
            pool.Remove(PlayerJobId.SwordShield);
            Shuffle(pool);
            _offer.Add(pool[0]);
            _offer.Add(pool[1]);
        }
        else
        {
            Shuffle(pool);
            _offer.Add(pool[0]);
            _offer.Add(pool[1]);
            _offer.Add(pool[2]);
        }
        _selected = null;
    }

    static void Shuffle(List<PlayerJobId> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            var tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }

    void OnRefresh()
    {
        if (_refreshUsed || _opts.TutorialForceSwordFirst)
        {
            UIManager.Instance?.ShowToast("本局不能再刷新");
            return;
        }
        _refreshUsed = true;
        RollOffers(forceSwordFirst: false);
        RefreshCardsVisual();
        UpdateEnterInteractable();
        if (_refreshBtn != null) _refreshBtn.interactable = false;
        if (_refreshLabel != null) _refreshLabel.text = "已刷新";
    }

    void OnCardClicked(int index)
    {
        if (index < 0 || index >= _offer.Count) return;
        var job = _offer[index];
        if (_opts.RequireSwordShield && job != PlayerJobId.SwordShield)
        {
            UIManager.Instance?.ShowToast("请先选择剑盾卫士");
            return;
        }
        _selected = job;
        RefreshCardsVisual();
        UpdateEnterInteractable();
    }

    void RefreshCardsVisual()
    {
        for (int i = 0; i < _cards.Count; i++)
        {
            if (i >= _offer.Count) continue;
            var def = PlayerJobDefs.Get(_offer[i]);
            var c = _cards[i];
            if (c.Name != null) c.Name.text = def.DisplayName;
            if (c.HpStat != null && c.AtkStat == null && c.ControlStat == null)
            {
                // 旧白模：单块 Stats
                c.HpStat.text = $"血量 {def.HpLabel}\n攻击 {def.AtkLabel}\n操控 {def.DiffLabel}";
            }
            else
            {
                if (c.HpStat != null) c.HpStat.text = $"血量 {def.HpLabel}";
                if (c.AtkStat != null) c.AtkStat.text = $"攻击 {def.AtkLabel}";
                if (c.ControlStat != null) c.ControlStat.text = $"操控 {def.DiffLabel}";
            }
            if (c.Desc != null)
            {
                string body = string.IsNullOrEmpty(def.LongDesc) ? def.Blurb : def.LongDesc;
                if (!string.IsNullOrEmpty(def.AttackStyleLabel))
                    body = body + "\n" + def.AttackStyleLabel;
                c.Desc.text = body;
            }
            ApplyRecommendStars(c.RatingRoot, def.RecommendStars);
            if (c.Icon != null)
            {
                var sp = PlayerJobDefs.TryLoadJobIcon(_offer[i]);
                if (sp != null)
                {
                    c.Icon.sprite = sp;
                    c.Icon.color = Color.white;
                }
                // 无 Resources 图时保留 prefab 已拖 Sprite，不刷色块
            }
            bool sel = _selected.HasValue && _selected.Value == _offer[i];
            if (c.Frame != null)
                c.Frame.color = sel
                    ? new Color(0.85f, 0.65f, 0.2f, 0.65f)
                    : new Color(0.45f, 0.32f, 0.18f, 0.35f);
            else if (c.Button != null)
            {
                var img = c.Button.GetComponent<Image>();
                if (img != null)
                    img.color = sel
                        ? new Color(0.95f, 0.88f, 0.55f, 1f)
                        : new Color(0.92f, 0.86f, 0.72f, 1f);
            }
        }
    }

    static void ApplyRecommendStars(Transform ratingRoot, int stars)
    {
        if (ratingRoot == null) return;
        stars = Mathf.Clamp(stars, 0, 3);
        // 兼容旧 Text Rating
        var legacy = ratingRoot.GetComponent<Text>();
        if (legacy != null && ratingRoot.childCount == 0)
        {
            legacy.text = stars <= 0 ? "◇◇◇" : new string('◆', stars) + new string('◇', 3 - stars);
            return;
        }
        for (int s = 1; s <= 3; s++)
        {
            var full = FindDeep(ratingRoot, "满星" + s);
            if (full == null) continue;
            bool on = s <= stars;
            var fullImg = full.GetComponent<Image>();
            var empty = FindDeep(full, "空星");
            if (empty != null)
            {
                full.gameObject.SetActive(true);
                if (fullImg != null) fullImg.enabled = on;
                empty.gameObject.SetActive(!on);
            }
            else if (fullImg != null)
            {
                full.gameObject.SetActive(true);
                fullImg.enabled = on;
            }
            else
                full.gameObject.SetActive(on);
        }
    }

    static void CreateStatRow(Transform parent, string rowName, string text, float y)
    {
        var row = CreateUi(rowName, parent, typeof(RectTransform)).transform;
        var rrt = row.GetComponent<RectTransform>();
        rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 1f);
        rrt.pivot = new Vector2(0.5f, 1f);
        rrt.sizeDelta = new Vector2(170f, 28f);
        rrt.anchoredPosition = new Vector2(0f, y);
        var t = CreateText(row, "Stats", text, 16, TextAnchor.MiddleLeft);
        Stretch(t.rectTransform);
        t.color = new Color(0.25f, 0.18f, 0.1f);
    }

    void UpdateEnterInteractable()
    {
        if (_enterBtn != null)
            _enterBtn.interactable = _selected.HasValue;
    }

    void OnEnterRift()
    {
        if (!_selected.HasValue)
        {
            UIManager.Instance?.ShowToast("请先选择职业");
            return;
        }
        if (_opts.RequireSwordShield && _selected.Value != PlayerJobId.SwordShield)
        {
            UIManager.Instance?.ShowToast("请先选择剑盾卫士");
            return;
        }
        PlayerJobDefs.SetSelected(_selected.Value);
        Hide();
        var cb = _onEnterRift;
        _onEnterRift = null;
        cb?.Invoke();
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

    static Text FindText(Transform root, string name)
    {
        var t = FindDeep(root, name);
        return t != null ? t.GetComponent<Text>() : null;
    }

    static Image FindImage(Transform root, string name)
    {
        var t = FindDeep(root, name);
        return t != null ? t.GetComponent<Image>() : null;
    }

    static GameObject CreateUi(string name, Transform parent, params Type[] comps)
    {
        var go = new GameObject(name, comps);
        go.transform.SetParent(parent, false);
        return go;
    }

    static Text CreateText(Transform parent, string name, string content, int size, TextAnchor align)
    {
        var go = CreateUi(name, parent, typeof(Text));
        var t = go.GetComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        t.font = GameFonts.GetChinese();
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void AnchorTop(RectTransform rt, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(0f, y);
    }
}
