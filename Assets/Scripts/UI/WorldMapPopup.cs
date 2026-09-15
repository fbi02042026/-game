using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 世界地图弹窗：强制选择、无关闭按钮。
/// 点击地块后在该地块上显示「进入」按钮；点击进入后淡出并通知外部开战。
/// </summary>
public class WorldMapPopup : MonoBehaviour
{
    [System.Serializable]
    public class RegionDef
    {
        [Tooltip("内部章节号 1-8")]
        public int chapterId = 1;
        [Tooltip("显示名，空则取 GameConfig 地图名")]
        public string displayName;
        [Tooltip("地块在地图上的归一化位置（0-1）。留 (0,0) 则用内置默认布局")]
        public Vector2 normalizedPosition;
        [Tooltip("地块图标，空则显示数字")]
        public Sprite icon;
        [Tooltip("地块常态颜色（已解锁、未通关）")]
        public Color normalColor = new Color(0.20f, 0.24f, 0.30f, 0.82f);
        [Tooltip("地块选中颜色")]
        public Color selectedColor = new Color(0.35f, 0.55f, 0.35f, 0.95f);
        [Tooltip("地块已通关颜色（暗掉）")]
        public Color clearedColor = new Color(0.13f, 0.14f, 0.17f, 0.72f);
        [Tooltip("地块未解锁颜色（灰显）")]
        public Color lockedColor = new Color(0.32f, 0.32f, 0.32f, 0.55f);
    }

    public static WorldMapPopup Current { get; private set; }

    [Header("地图与标题")]
    public Image mapBackground;
    public Text titleText;
    public Text descText;
    public Sprite defaultMapSprite;

    [Header("地块配置（按世界地图 8 块区域配置）")]
    public RegionDef[] regions = new RegionDef[8];

    [Header("进入按钮")]
    public Button enterButton;
    public Text enterButtonText;

    [Header("选中高亮（美术可换成材质球）")]
    public Material highlightMaterial;

    [Header("运行时生成用（预制体可为空，代码兜底创建）")]
    public Transform regionRoot;

    const string PrefabResourcePath = "Prefabs/UI/WorldMapPopup";
    /// <summary>默认坐标按「参考世界地图」的地块分布，下标 = 章节号 - 1。</summary>
    static readonly Vector2[] DefaultRegionPositions =
    {
        new Vector2(0.50f, 0.22f), // 1 森林（底部中央）
        new Vector2(0.72f, 0.42f), // 2 亡灵（中右）
        new Vector2(0.30f, 0.38f), // 3 雨林（中左）
        new Vector2(0.52f, 0.52f), // 4 田野（正中）
        new Vector2(0.18f, 0.48f), // 5 海岸（左）
        new Vector2(0.25f, 0.65f), // 6 洞穴（左上）
        new Vector2(0.55f, 0.74f), // 7 熔岩（上中）
        new Vector2(0.50f, 0.90f), // 8 冰川（顶部）
    };

    /// <summary>与 GameConfig.ChapterMapNames 同序，下标 = 章节号 - 1。</summary>
    static readonly string[] DefaultRegionNames =
    {
        "暮影森林", "幽冥墓园", "翡翠秘境", "晨曦草原",
        "海岛遗迹", "巨岩深窟", "赤焰炼狱", "永霜雪境"
    };

    Action<int> _onEnterChapter;
    List<int> _availableChapters = new List<int>();
    RegionSlot[] _slots;
    int _selectedChapter;
    bool _entering;

    // ============================================================
    // 公共 API
    // ============================================================

    /// <summary>
    /// 强制弹出世界地图。onEnterChapter(chapterId) 在玩家点击「进入」后调用。
    /// </summary>
    public static WorldMapPopup Show(Action<int> onEnterChapter)
    {
        if (Current != null)
        {
            Current.gameObject.SetActive(true);
            Current._onEnterChapter = onEnterChapter;
            Current.RefreshAvailability();
            Current.ShowInternal();
            return Current;
        }

        GameObject root = null;
        var prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab != null)
        {
            root = Instantiate(prefab);
            root.name = "WorldMapPopup";
        }
        else
        {
            root = new GameObject("WorldMapPopup", typeof(RectTransform));
            var popup = root.AddComponent<WorldMapPopup>();
            popup.BuildHierarchyForPrefab();
        }

        var canvas = root.GetComponent<Canvas>() ?? root.GetComponentInChildren<Canvas>(true);
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = GameConfig.UiSort.TownPopup;
        }

        var instance = root.GetComponent<WorldMapPopup>();
        if (instance == null) instance = root.AddComponent<WorldMapPopup>();
        instance._onEnterChapter = onEnterChapter;
        Current = instance;
        instance.RefreshAvailability();
        instance.ShowInternal();
        return instance;
    }

    public static void EnsureDestroyed()
    {
        if (Current != null && Current.gameObject != null)
            Destroy(Current.gameObject);
        Current = null;
    }

    // ============================================================
    // 生命周期
    // ============================================================

    void Awake()
    {
        Current = this;
        EnsureDefaults();
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    void EnsureDefaults()
    {
        // 注意：字段初始化只建了 8 个空槽，必须逐格补齐，否则 BuildRegionSlots 会空引用
        if (regions == null || regions.Length != 8)
            regions = new RegionDef[8];

        for (int i = 0; i < 8; i++)
        {
            if (regions[i] == null) regions[i] = new RegionDef();
            var d = regions[i];
            if (d.chapterId != i + 1) d.chapterId = i + 1;
            if (string.IsNullOrEmpty(d.displayName)) d.displayName = DefaultRegionNames[i];
            if (d.normalizedPosition == Vector2.zero) d.normalizedPosition = DefaultRegionPositions[i];
        }
    }

    // ============================================================
    // 显示 / 隐藏
    // ============================================================

    void ShowInternal()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        _selectedChapter = 0;
        _entering = false;
        if (enterButton != null)
            enterButton.gameObject.SetActive(false);
        RefreshRegions();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    // ============================================================
    // 可用章节
    // ============================================================

    void RefreshAvailability()
    {
        _availableChapters = ChapterRouteTable.AvailableChapters(SaveSystem.Instance?.Data);
        if (_availableChapters == null) _availableChapters = new List<int>();
        if (_availableChapters.Count == 0) _availableChapters.Add(1);
    }

    bool IsUnlocked(int chapterId)
    {
        return _availableChapters.Contains(chapterId);
    }

    /// <summary>该章是否已通关（通关的地块要暗掉）。</summary>
    static bool IsCleared(int chapterId)
    {
        var data = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (data == null) return false;
        if (data.HasClearedChapter(chapterId)) return true;
        if (data.chapterClearCounts == null) return false;
        for (int i = 0; i < data.chapterClearCounts.Count; i++)
        {
            var e = data.chapterClearCounts[i];
            if (e != null && e.chapter == chapterId && e.clearCount > 0) return true;
        }
        return false;
    }

    // ============================================================
    // 区域按钮刷新
    // ============================================================

    void RefreshRegions()
    {
        if (_slots == null || _slots.Length == 0) BindOrBuildSlots();
        for (int i = 0; i < _slots.Length; i++)
        {
            var slot = _slots[i];
            if (slot == null || slot.root == null) continue;
            bool unlocked = IsUnlocked(slot.chapterId);
            bool selected = slot.chapterId == _selectedChapter;
            bool cleared = IsCleared(slot.chapterId);
            // 已通关的地块不可再进（暗掉 + 不可点）
            var btn = slot.root.GetComponent<Button>();
            if (btn != null) btn.interactable = unlocked && !cleared;

            var img = slot.root.GetComponent<Image>();
            if (img != null && slot.def != null)
            {
                if (!unlocked) img.color = slot.def.lockedColor;        // 没解锁 → 灰显
                else if (selected) img.color = slot.def.selectedColor;  // 选中 → 高亮
                else if (cleared) img.color = slot.def.clearedColor;    // 打完 → 暗掉
                else img.color = slot.def.normalColor;
            }

            if (slot.lockRoot != null)
                slot.lockRoot.SetActive(!unlocked);
            if (slot.highlight != null)
                slot.highlight.SetActive(selected);
        }

        if (titleText != null)
        {
            if (_selectedChapter > 0)
                titleText.text = GameConfig.GetChapterTitleText(_selectedChapter);
            else
                titleText.text = "选择冒险区域";
        }

        if (descText != null)
        {
            // 复用叙事 V2.0 的章节开场文案，避免和战斗内 ChapterSplash 两套文案不同步
            string intro = _selectedChapter > 0 ? ChapterStoryBeats.OpeningLine(_selectedChapter) : null;
            descText.text = string.IsNullOrEmpty(intro)
                ? "点击地图上的区域，选择你要前往的地方。"
                : intro;
        }
    }

    // ============================================================
    // 地块点击
    // ============================================================

    void OnRegionClicked(int index)
    {
        if (_entering) return;
        if (_slots == null || index < 0 || index >= _slots.Length) return;
        var slot = _slots[index];
        if (!IsUnlocked(slot.chapterId)) return;
        if (IsCleared(slot.chapterId)) return; // 已通关的地块不可再进

        _selectedChapter = slot.chapterId;
        RefreshRegions();
        MoveEnterButtonTo(slot.root.transform as RectTransform);
    }

    void MoveEnterButtonTo(RectTransform target)
    {
        if (enterButton == null || target == null) return;
        var ert = enterButton.transform as RectTransform;
        ert.SetParent(target, false);
        ert.anchorMin = new Vector2(0.5f, 1f);
        ert.anchorMax = new Vector2(0.5f, 1f);
        ert.pivot = new Vector2(0.5f, 0f);
        ert.anchoredPosition = new Vector2(0, 12f);
        ert.sizeDelta = new Vector2(120f, 52f);
        ert.localScale = Vector3.one;
        enterButton.gameObject.SetActive(true);
        enterButton.transform.SetAsLastSibling();
    }

    void OnEnterClicked()
    {
        if (_entering) return;
        if (_selectedChapter < 1) return;
        _entering = true;

        // 黑屏由外部切场景流程负责（GameSceneManager.LoadBattleScene -> LoadingOverlay）
        // 这里先隐藏弹窗并回调，保证玩家不会在选职业/Loading 时仍看到地图
        Hide();
        _onEnterChapter?.Invoke(_selectedChapter);
    }

    // ============================================================
    // 预制体构建（运行时/编辑器生成均可）
    // ============================================================

    public void BuildHierarchyForPrefab()
    {
        // 注意：UnityEngine.Object 不能配合 ?? / ?. 使用（自定义 == 会让"假空"绕过判空），
        // 这里一律用显式 == null 检查，否则会抛 MissingComponentException。
        var rt = GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();
        UICanvasSetup.EnsureRootStretch(rt);

        var canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownPopup);

        var cg = gameObject.GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = true;
        cg.interactable = true;

        EnsureDefaults();

        // 地图背景
        if (mapBackground == null)
        {
            var bgGo = new GameObject("MapBackground", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(transform, false);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            mapBackground = bgGo.GetComponent<Image>();
            mapBackground.color = new Color(0.10f, 0.08f, 0.05f, 1f);
            var fitter = bgGo.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = GameConfig.DESIGN_WIDTH / GameConfig.DESIGN_HEIGHT;
        }

        if (defaultMapSprite != null && mapBackground != null)
        {
            mapBackground.sprite = defaultMapSprite;
            mapBackground.color = Color.white;
        }

        // 标题栏
        if (titleText == null)
        {
            var tGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            tGo.transform.SetParent(transform, false);
            var trt = tGo.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.05f, 0.88f);
            trt.anchorMax = new Vector2(0.95f, 0.96f);
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            titleText = tGo.GetComponent<Text>();
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.fontSize = 38;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = new Color(1f, 0.95f, 0.75f, 1f);
        }

        // 描述
        if (descText == null)
        {
            var dGo = new GameObject("Desc", typeof(RectTransform), typeof(Text));
            dGo.transform.SetParent(transform, false);
            var drt = dGo.GetComponent<RectTransform>();
            drt.anchorMin = new Vector2(0.08f, 0.06f);
            drt.anchorMax = new Vector2(0.92f, 0.16f);
            drt.offsetMin = drt.offsetMax = Vector2.zero;
            descText = dGo.GetComponent<Text>();
            descText.alignment = TextAnchor.MiddleCenter;
            descText.fontSize = 24;
            descText.color = new Color(0.95f, 0.90f, 0.80f, 0.95f);
            descText.horizontalOverflow = HorizontalWrapMode.Wrap;
            descText.verticalOverflow = VerticalWrapMode.Overflow;
        }

        // 地块根
        if (regionRoot == null)
        {
            var rGo = new GameObject("Regions", typeof(RectTransform));
            rGo.transform.SetParent(transform, false);
            regionRoot = rGo.transform;
            var rrt = rGo.GetComponent<RectTransform>();
            rrt.anchorMin = Vector2.zero;
            rrt.anchorMax = Vector2.one;
            rrt.offsetMin = rrt.offsetMax = Vector2.zero;
        }

        BuildRegionSlots();

        // 进入按钮（默认隐藏，选中地块后挂载到该地块下）
        if (enterButton == null)
        {
            var eGo = new GameObject("EnterButton", typeof(RectTransform), typeof(Image), typeof(Button));
            eGo.transform.SetParent(transform, false);
            var ert = eGo.GetComponent<RectTransform>();
            ert.anchorMin = new Vector2(0.5f, 0.5f);
            ert.anchorMax = new Vector2(0.5f, 0.5f);
            ert.pivot = new Vector2(0.5f, 0.5f);
            ert.sizeDelta = new Vector2(120f, 52f);
            enterButton = eGo.GetComponent<Button>();
            var eImg = eGo.GetComponent<Image>();
            eImg.color = new Color(0.22f, 0.55f, 0.22f, 1f);

            var lblGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            lblGo.transform.SetParent(eGo.transform, false);
            var lrt = lblGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            enterButtonText = lblGo.GetComponent<Text>();
            enterButtonText.text = "进入";
            enterButtonText.alignment = TextAnchor.MiddleCenter;
            enterButtonText.fontSize = 24;
            enterButtonText.color = Color.white;
        }

        enterButton.gameObject.SetActive(false);
        enterButton.onClick.RemoveAllListeners();
        enterButton.onClick.AddListener(OnEnterClicked);

        GameFonts.ApplyToHierarchy(transform);
    }

    void BuildRegionSlots()
    {
        if (regionRoot == null) return;
        for (int i = regionRoot.childCount - 1; i >= 0; i--)
            DestroyImmediate(regionRoot.GetChild(i).gameObject);

        _slots = new RegionSlot[regions.Length];
        for (int i = 0; i < regions.Length; i++)
        {
            var def = regions[i];
            var go = new GameObject($"Region_{def.chapterId}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(regionRoot, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = def.normalizedPosition;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(96f, 96f);

            var img = go.GetComponent<Image>();
            img.sprite = def.icon;
            img.color = def.normalColor;
            img.type = Image.Type.Simple;
            img.raycastTarget = true;

            // 高亮框
            var hiGo = new GameObject("Highlight", typeof(RectTransform), typeof(Image));
            hiGo.transform.SetParent(go.transform, false);
            var hiRt = hiGo.GetComponent<RectTransform>();
            hiRt.anchorMin = Vector2.zero;
            hiRt.anchorMax = Vector2.one;
            hiRt.offsetMin = new Vector2(-8, -8);
            hiRt.offsetMax = new Vector2(8, 8);
            var hiImg = hiGo.GetComponent<Image>();
            hiImg.color = new Color(1f, 0.92f, 0.45f, 0.45f);
            if (highlightMaterial != null) hiImg.material = highlightMaterial;
            hiGo.SetActive(false);

            // 锁定图标
            var lockGo = new GameObject("Lock", typeof(RectTransform), typeof(Text));
            lockGo.transform.SetParent(go.transform, false);
            var lrt = lockGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            var lockTxt = lockGo.GetComponent<Text>();
            lockTxt.text = "🔒";
            lockTxt.alignment = TextAnchor.MiddleCenter;
            lockTxt.fontSize = 32;
            lockTxt.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);
            lockGo.SetActive(false);

            // 章节号 / 名称
            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameGo.transform.SetParent(go.transform, false);
            var nrt = nameGo.GetComponent<RectTransform>();
            nrt.anchorMin = Vector2.zero;
            nrt.anchorMax = Vector2.one;
            nrt.offsetMin = new Vector2(0, -24);
            nrt.offsetMax = new Vector2(0, 0);
            var nameTxt = nameGo.GetComponent<Text>();
            nameTxt.text = string.IsNullOrEmpty(def.displayName)
                ? GameConfig.GetChapterMapName(def.chapterId)
                : def.displayName;
            nameTxt.alignment = TextAnchor.MiddleCenter;
            nameTxt.fontSize = 18;
            nameTxt.color = Color.white;

            var btn = go.GetComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.9f, 1f, 0.9f, 1f);
            colors.pressedColor = new Color(0.75f, 0.85f, 0.75f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            btn.colors = colors;

            int idx = i;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnRegionClicked(idx));

            _slots[i] = new RegionSlot
            {
                def = def,
                chapterId = def.chapterId,
                root = go,
                highlight = hiGo,
                lockRoot = lockGo
            };
        }
    }

    void BindOrBuildSlots()
    {
        if (regionRoot != null && regionRoot.childCount >= regions.Length)
        {
            var list = new System.Collections.Generic.List<RegionSlot>();
            for (int i = 0; i < regionRoot.childCount; i++)
            {
                var t = regionRoot.GetChild(i);
                if (!t.name.StartsWith("Region_")) continue;
                int ch = ParseChapterFromName(t.name);
                if (ch < 1) continue;
                var hiT = t.Find("Highlight");
                var lockT = t.Find("Lock");
                var slot = new RegionSlot
                {
                    chapterId = ch,
                    root = t.gameObject,
                    highlight = hiT != null ? hiT.gameObject : null,
                    lockRoot = lockT != null ? lockT.gameObject : null,
                    def = FindDef(ch)
                };
                var btn = t.GetComponent<Button>();
                if (btn != null)
                {
                    int idx = list.Count;
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => OnRegionClicked(idx));
                }
                list.Add(slot);
            }
            if (list.Count >= regions.Length)
            {
                _slots = list.ToArray();
            }
            else
            {
                BuildRegionSlots();
            }
        }
        else
        {
            BuildRegionSlots();
        }

        if (enterButton == null)
        {
            var found = transform.Find("EnterButton");
            if (found != null)
            {
                enterButton = found.GetComponent<Button>();
                enterButtonText = found.GetComponentInChildren<Text>();
            }
        }
        if (enterButton != null)
        {
            enterButton.onClick.RemoveAllListeners();
            enterButton.onClick.AddListener(OnEnterClicked);
        }
    }

    RegionDef FindDef(int chapterId)
    {
        for (int i = 0; i < regions.Length; i++)
            if (regions[i].chapterId == chapterId) return regions[i];
        return null;
    }

    static int ParseChapterFromName(string name)
    {
        if (string.IsNullOrEmpty(name)) return 0;
        var s = name.Replace("Region_", "");
        if (int.TryParse(s, out int n)) return n;
        return 0;
    }

    // ============================================================
    // 内部数据结构
    // ============================================================

    class RegionSlot
    {
        public RegionDef def;
        public int chapterId;
        public GameObject root;
        public GameObject highlight;
        public GameObject lockRoot;
    }
}
