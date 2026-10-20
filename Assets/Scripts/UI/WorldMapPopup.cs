using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 世界地图弹窗：点击地块后在该地块上显示「进入」按钮；点击进入后淡出并通知外部开战。
/// 点击地图四周空白处（未命中地块/地图区域）直接关闭弹窗（2026-10-20 主人要求）。
/// </summary>
public class WorldMapPopup : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler
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
    // 地图拖拽 / 缩放（单指拖动、双指捏合、鼠标滚轮）
    // ============================================================

    [Header("地图缩放范围（1 = 原始大小，稍微放大即可）")]
    public float mapMinZoom = 1f;
    public float mapMaxZoom = 1.5f;

    /// <summary>运行时容器：包住地图背景 + 地块根，整体平移/缩放；预制体结构不动。</summary>
    RectTransform _mapContent;
    /// <summary>底部「点击四周空白处关闭」提示（运行时生成，不进预制体）。</summary>
    Text _closeHint;
    /// <summary>根节点按下位置（点空白关闭用）：按下/抬起没移动才算一次「点四周」。</summary>
    Vector2 _pressPos;
    bool _pressValid;
    float _mapZoom = 1f;
    Vector2 _panLast;
    bool _panLastValid;
    float _panMoved;
    bool _pinchActive;
    float _pinchPrevDist;
    Vector2 _pinchPrevMid;
    float _lastPanTime = -10f;
    /// <summary>拖动/捏合后的短时间内吞掉地块点击，避免拖完地图误选区域。</summary>
    const float PanClickSuppressSeconds = 0.2f;
    /// <summary>手指移动超过该像素才认定为拖动（过滤点击时的抖动）。</summary>
    const float PanStartPixels = 12f;

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
        ResolveRegionRoot();
        ResolveStaticRefs();
        EnsureMapViewContent();
        EnsureCloseHint();
        ResetMapView();
        _selectedChapter = 0;
        _entering = false;
        _pressValid = false;
        if (enterButton != null)
            enterButton.gameObject.SetActive(false);
        RefreshRegions();
        PlayNewUnlockFxIfNeeded();
    }

    /// <summary>每次打开恢复原始缩放和位置，避免上次的拖动/缩放状态残留。</summary>
    void ResetMapView()
    {
        if (_mapContent == null) return;
        _mapZoom = 1f;
        _mapContent.localScale = Vector3.one;
        _mapContent.anchoredPosition = Vector2.zero;
        _pinchActive = false;
        _panLastValid = false;
        _panMoved = 0f;
    }

    /// <summary>
    /// 地块根（regionRoot）唯一出口。
    /// 2026-10-20：实测预制体根节点上的 WorldMapPopup 组件已被引擎清掉（历史 56 字符 guid 修复后
    /// 判为 Missing Script），运行时只能 AddComponent 补一个字段全空的组件 → regionRoot 为 null →
    /// BuildRegionSlots 直接 return、_slots 保持 null → RefreshRegions 抛 NRE。
    /// 这里按名字把预制体里现成的 Regions 节点找回来，找不到就 LogError（fail closed，不静默）。
    /// 必须在 EnsureMapViewContent 之前执行，否则地块不会被收进可拖动/缩放的 MapViewContent。
    /// </summary>
    void ResolveRegionRoot()
    {
        if (regionRoot != null) return;
        var found = FindChildRecursive(transform, "Regions");
        if (found == null)
        {
            Debug.LogError("[WorldMapPopup] 找不到地块根节点 Regions：请给预制体根节点补挂 WorldMapPopup 组件，"
                + "并把 Region Root 拖到 Regions 节点上。", this);
            return;
        }
        regionRoot = found;
    }

    /// <summary>
    /// 静态引用（地图背景/标题/描述）唯一出口：预制体没挂 WorldMapPopup 组件时按名字找回。
    /// 2026-10-20 主人截图实证：mapBackground 不绑的话，EnsureMapViewContent 会把 MapViewContent
    /// 插到 index 0，背景留在根节点压在它上面 → 8 个地块全被背景盖住（地图只见背景不见地块）。
    /// </summary>
    void ResolveStaticRefs()
    {
        if (mapBackground == null)
        {
            var bg = FindChildRecursive(transform, "MapBackground");
            if (bg != null) mapBackground = bg.GetComponent<Image>();
            else Debug.LogError("[WorldMapPopup] 找不到 MapBackground 节点，地块会被背景盖住。", this);
        }
        if (titleText == null)
        {
            var t = FindChildRecursive(transform, "Title");
            if (t != null) titleText = t.GetComponent<Text>();
        }
        if (descText == null)
        {
            var d = FindChildRecursive(transform, "Desc");
            if (d != null) descText = d.GetComponent<Text>();
        }
    }

    static Transform FindChildRecursive(Transform parent, string name)
    {
        var direct = parent.Find(name);
        if (direct != null) return direct;
        for (int i = 0; i < parent.childCount; i++)
        {
            var hit = FindChildRecursive(parent.GetChild(i), name);
            if (hit != null) return hit;
        }
        return null;
    }

    /// <summary>
    /// 建一个运行时容器，把地图背景和地块根包进去整体拖拽/缩放。
    /// 只动层级不动预制体资产，标题/描述仍留在弹窗根节点上不跟着动。
    /// </summary>
    void EnsureMapViewContent()
    {
        if (_mapContent != null) return;
        var rootRt = transform as RectTransform;
        var go = new GameObject("MapViewContent", typeof(RectTransform));
        var rt = go.transform as RectTransform;
        rt.SetParent(rootRt, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        // 占住地图背景原来的层级位置，保证标题/描述仍压在地图上面
        int index = mapBackground != null ? mapBackground.transform.GetSiblingIndex() : 0;
        rt.SetSiblingIndex(index);
        if (mapBackground != null) mapBackground.transform.SetParent(rt, false);
        if (regionRoot != null) regionRoot.SetParent(rt, false);
        _mapContent = rt;
    }

    // ============================================================
    // 点四周空白关闭（2026-10-20 主人要求）
    // 原理：MapBackground 全屏铺满但本身没有任何事件处理器，射线命中后会
    // 一路冒泡到弹窗根节点；地块 / 进入按钮带 Button（自带处理器）会拦住事件，
    // 不会冒泡到根。所以根节点收到的点击 = 没点中任何地块 = 点四周 → 关闭。
    // ============================================================

    public void OnPointerDown(PointerEventData eventData)
    {
        _pressPos = eventData.position;
        _pressValid = true;
    }

    /// <summary>底部关闭提示（2026-10-20 主人要求）：运行时生成，挂弹窗根节点不跟地图拖动。</summary>
    void EnsureCloseHint()
    {
        if (_closeHint != null) return;
        var go = new GameObject("CloseHint", typeof(RectTransform), typeof(Text));
        var rt = go.transform as RectTransform;
        rt.SetParent(transform, false);
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 12f);
        rt.sizeDelta = new Vector2(420f, 34f);
        _closeHint = go.GetComponent<Text>();
        _closeHint.text = "点击四周空白处关闭";
        _closeHint.alignment = TextAnchor.MiddleCenter;
        _closeHint.fontSize = 22;
        _closeHint.color = new Color(1f, 0.95f, 0.85f, 0.6f);
        _closeHint.raycastTarget = false; // 不吃射线：点在提示文字上照样走「点四周关闭」
        GameFonts.ApplyToHierarchy(go.transform);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_pressValid) return;
        _pressValid = false;
        if (_entering) return;
        // 按下和抬起位置差超过点击抖动阈值 → 是拖图/缩放松手，不关闭
        if (((Vector2)eventData.position - _pressPos).sqrMagnitude >
            PanStartPixels * PanStartPixels) return;
        if (IsRecentPan()) return; // 刚拖完地图松手，吞掉这次误触
        if (IsPressInsideAnyRegion(_pressPos)) return; // 点在地块 bounding box 内不关
        Hide();
    }

    /// <summary>按下点是否落在任一地块的矩形内（地块上的名字/锁图标没有 Button，
    /// 点击会冒泡到根节点，靠这里挡住，避免点地块文字误关地图）。</summary>
    bool IsPressInsideAnyRegion(Vector2 screenPos)
    {
        if (_slots == null) return false;
        var cam = GetPopupCamera();
        for (int i = 0; i < _slots.Length; i++)
        {
            var slot = _slots[i];
            if (slot == null || slot.root == null) continue;
            var rt = slot.root.transform as RectTransform;
            if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, cam))
                return true;
        }
        return false;
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
    // 新区域解锁动效（2026-10-19 主人要求）
    // ============================================================

    /// <summary>已播过解锁动效的最大章节号（PlayerPrefs 记档），避免每次打开地图都重复播。</summary>
    const string SeenUnlockKey = "worldmap.seen.maxchapter";

    /// <summary>
    /// 打开世界地图时，若有比"已播过"更新的解锁地块：对其脉冲放大一下 + 飘字提示，
    /// 然后记档，下次打开不再播。典型场景：通关第一章后第一次开图，幽冥墓园亮起。
    /// </summary>
    void PlayNewUnlockFxIfNeeded()
    {
        int seen = PlayerPrefs.GetInt(SeenUnlockKey, 1);
        int maxSeen = seen;
        var newNames = new List<string>();
        for (int i = 0; i < _slots.Length; i++)
        {
            var slot = _slots[i];
            if (slot == null || slot.root == null) continue;
            if (!IsUnlocked(slot.chapterId)) continue;
            if (slot.chapterId <= seen) continue;
            if (slot.chapterId > maxSeen) maxSeen = slot.chapterId;
            var rt = slot.root.transform as RectTransform;
            if (rt != null) StartCoroutine(CoUnlockPulse(rt));
            newNames.Add(slot.def != null && !string.IsNullOrEmpty(slot.def.displayName)
                ? slot.def.displayName
                : GameConfig.GetChapterMapName(slot.chapterId));
        }
        if (newNames.Count > 0)
            UIManager.Instance?.ShowToast("解锁新区域：" + string.Join("、", newNames.ToArray()));
        if (maxSeen > seen)
        {
            PlayerPrefs.SetInt(SeenUnlockKey, maxSeen);
            PlayerPrefs.Save();
        }
    }

    System.Collections.IEnumerator CoUnlockPulse(Transform t)
    {
        if (t == null) yield break;
        Vector3 baseScale = t.localScale;
        float dur = 0.45f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = 1f + Mathf.Sin((elapsed / dur) * Mathf.PI) * 0.25f;
            t.localScale = baseScale * k;
            yield return null;
        }
        t.localScale = baseScale;
    }

    // ============================================================
    // 区域按钮刷新
    // ============================================================

    void RefreshRegions()
    {
        if (_slots == null || _slots.Length == 0) BindOrBuildSlots();
        if (_slots == null)
        {
            Debug.LogError("[WorldMapPopup] 地块槽位生成失败（regionRoot 为空？），地图本次不刷新。", this);
            return;
        }
        bool sharedEnterUsed = false;
        for (int i = 0; i < _slots.Length; i++)
        {
            var slot = _slots[i];
            if (slot == null || slot.root == null) continue;
            bool unlocked = IsUnlocked(slot.chapterId);
            bool selected = slot.chapterId == _selectedChapter;
            bool cleared = IsCleared(slot.chapterId);
            // 已通关的地块不可再进（暗掉 + 不可点）
            var btn = (slot.visual != null ? slot.visual : slot.root).GetComponent<Button>();
            if (btn != null) btn.interactable = unlocked && !cleared;

            var img = (slot.visual != null ? slot.visual : slot.root).GetComponent<Image>();
            if (img != null && slot.def != null)
            {
                if (!unlocked) img.color = slot.def.lockedColor;        // 没解锁 → 灰显
                else if (selected) img.color = slot.def.selectedColor;  // 选中 → 高亮
                else if (cleared) img.color = slot.def.clearedColor;    // 打完 → 暗掉
                else img.color = slot.def.normalColor;
            }

            // 解锁态由代码切（美术存的是「Region_1 解锁 / 其余锁定」的快照）：
            // 解锁 → 显示地块本体 + 名字底框、收掉锁图标；未解锁 → 反过来。
            // 代码自建的槽（BuildRegionSlots）本体就是 root 自己，不参与这套切换（灰显靠颜色）。
            if (slot.visual != null && slot.visual != slot.root)
                slot.visual.SetActive(unlocked);
            if (slot.nameRoot != null)
                slot.nameRoot.SetActive(unlocked);
            if (slot.lockRoot != null)
                slot.lockRoot.SetActive(!unlocked);
            if (slot.highlight != null)
                slot.highlight.SetActive(selected);

            // 「进入」按钮：优先用地块自带的那颗，没有才把共享的那颗挪过来
            if (slot.enterButton != null)
            {
                slot.enterButton.gameObject.SetActive(selected && !cleared);
            }
            else if (selected && !cleared)
            {
                MoveEnterButtonTo(slot.root.transform as RectTransform);
                sharedEnterUsed = true;
            }
        }

        if (!sharedEnterUsed && enterButton != null)
            enterButton.gameObject.SetActive(false);

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
        if (IsRecentPan()) return; // 刚拖完地图，吞掉这次误触
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
        if (IsRecentPan()) return; // 刚拖完地图松手压在按钮上，不算点击
        if (_selectedChapter < 1) return;
        _entering = true;

        // 黑屏由外部切场景流程负责（GameSceneManager.LoadBattleScene -> LoadingOverlay）
        // 这里先隐藏弹窗并回调，保证玩家不会在选职业/Loading 时仍看到地图
        Hide();
        _onEnterChapter?.Invoke(_selectedChapter);
    }

    // ============================================================
    // 地图拖拽 / 缩放实现
    // ============================================================

    bool IsRecentPan()
    {
        return Time.realtimeSinceStartup - _lastPanTime < PanClickSuppressSeconds;
    }

    void Update()
    {
        if (_entering || _mapContent == null) return;
        if (Input.touchCount >= 2) HandlePinch();
        else if (Input.touchCount == 1) HandleSingleTouch();
        else HandleMouse();
    }

    void HandleSingleTouch()
    {
        var t = Input.GetTouch(0);
        if (t.phase == TouchPhase.Began)
        {
            _panLast = t.position;
            _panLastValid = true;
            _panMoved = 0f;
            return;
        }
        if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
        {
            _panLastValid = false;
            return;
        }
        // Moved / Stationary：捏合切回单指时 _panLastValid 为 false，先重新定位避免跳变
        if (!_panLastValid)
        {
            _panLast = t.position;
            _panLastValid = true;
            return;
        }
        var delta = (Vector2)t.position - _panLast;
        _panLast = t.position;
        FeedPanDelta(delta);
    }

    void HandleMouse()
    {
        // 编辑器/PC 用滚轮缩放，以鼠标位置为中心
        float wheel = Input.mouseScrollDelta.y;
        if (!Mathf.Approximately(wheel, 0f))
            ZoomBy(1f + wheel * 0.15f, Input.mousePosition);

        if (Input.GetMouseButtonDown(0))
        {
            _panLast = Input.mousePosition;
            _panLastValid = true;
            _panMoved = 0f;
        }
        else if (Input.GetMouseButton(0) && _panLastValid)
        {
            var delta = (Vector2)Input.mousePosition - _panLast;
            _panLast = Input.mousePosition;
            FeedPanDelta(delta);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            _panLastValid = false;
        }
    }

    void HandlePinch()
    {
        var t0 = Input.GetTouch(0);
        var t1 = Input.GetTouch(1);
        float dist = Vector2.Distance(t0.position, t1.position);
        Vector2 mid = (t0.position + t1.position) * 0.5f;
        bool begin = !_pinchActive || t0.phase == TouchPhase.Began || t1.phase == TouchPhase.Began;
        if (begin)
        {
            _pinchActive = true;
            _pinchPrevDist = dist;
            _pinchPrevMid = mid;
            _panLastValid = false; // 切回单指时重新定位，避免跳变
            return;
        }
        if (dist > 1f && _pinchPrevDist > 1f)
            ZoomBy(dist / _pinchPrevDist, mid);
        var midDelta = mid - _pinchPrevMid;
        if (midDelta.sqrMagnitude > 0f)
        {
            _lastPanTime = Time.realtimeSinceStartup;
            PanByScreenDelta(midDelta);
        }
        _pinchPrevDist = dist;
        _pinchPrevMid = mid;
    }

    void FeedPanDelta(Vector2 screenDelta)
    {
        if (screenDelta.sqrMagnitude <= 0f) return;
        _panMoved += screenDelta.magnitude;
        if (_panMoved < PanStartPixels) return; // 点击抖动不拖图
        _lastPanTime = Time.realtimeSinceStartup;
        PanByScreenDelta(screenDelta);
    }

    void ZoomBy(float factor, Vector2 screenPivot)
    {
        float target = Mathf.Clamp(_mapZoom * factor, mapMinZoom, mapMaxZoom);
        if (Mathf.Approximately(target, _mapZoom)) return;
        float k = target / _mapZoom;

        var rootRt = transform as RectTransform;
        if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(rootRt, screenPivot, GetPopupCamera(), out var pivotWorld))
            return;
        // 以手指/滚轮位置为锚点缩放：让锚点压住的地图点保持不动
        Vector3 deltaWorld = (pivotWorld - _mapContent.position) * (1f - k);
        deltaWorld.z = 0f;
        _mapContent.anchoredPosition += (Vector2)rootRt.InverseTransformVector(deltaWorld);
        _mapContent.localScale = Vector3.one * target;
        _mapZoom = target;
        ClampMapOffset();
    }

    void PanByScreenDelta(Vector2 screenDelta)
    {
        var rootRt = transform as RectTransform;
        var cam = GetPopupCamera();
        if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(rootRt, Vector2.zero, cam, out var w0)) return;
        if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(rootRt, screenDelta, cam, out var w1)) return;
        Vector3 deltaWorld = w1 - w0;
        deltaWorld.z = 0f;
        _mapContent.anchoredPosition += (Vector2)rootRt.InverseTransformVector(deltaWorld);
        ClampMapOffset();
    }

    void ClampMapOffset()
    {
        var max = CalcMaxPanOffset();
        var p = _mapContent.anchoredPosition;
        p.x = Mathf.Clamp(p.x, -max.x, max.x);
        p.y = Mathf.Clamp(p.y, -max.y, max.y);
        _mapContent.anchoredPosition = p;
    }

    /// <summary>
    /// 允许的平移范围：地图出画余量的一半，外加保底小幅余量（不缩放也能上下左右挪一点），
    /// 拖多了边缘会露出底图，属正常效果。
    /// </summary>
    Vector2 CalcMaxPanOffset()
    {
        var rootRt = transform as RectTransform;
        Vector2 view = rootRt.rect.size;
        float baseSlack = 40f * _mapZoom; // 保底小幅拖动余量
        if (mapBackground == null)
            return new Vector2(baseSlack, baseSlack);
        var bgRt = mapBackground.transform as RectTransform;
        float allowX = (bgRt.rect.size.x * _mapZoom - view.x) * 0.5f;
        float allowY = (bgRt.rect.size.y * _mapZoom - view.y) * 0.5f;
        return new Vector2(Mathf.Max(allowX, baseSlack), Mathf.Max(allowY, baseSlack));
    }

    Camera GetPopupCamera()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = GetComponentInChildren<Canvas>(true);
        return canvas != null ? canvas.worldCamera : null;
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
        EnsureSharedEnterButton();

        GameFonts.ApplyToHierarchy(transform);
    }

    /// <summary>
    /// 共享「进入」按钮的唯一出口：根节点下没有 EnterButton 就按现有样式建一颗。
    /// 2026-10-20：预制体只在 Region_2~8 下各放了一颗，Region_1（第一章）没有，
    /// 不补这颗的话选中第一章点不出「进入」按钮。
    /// </summary>
    void EnsureSharedEnterButton()
    {
        if (enterButton == null)
        {
            var found = transform.Find("EnterButton");
            if (found != null) enterButton = found.GetComponent<Button>();
        }

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
            var lbl = lblGo.GetComponent<Text>();
            lbl.text = "进入";
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.fontSize = 24;
            lbl.color = Color.white;
        }

        if (enterButton == null) return;
        enterButtonText = enterButton.GetComponentInChildren<Text>();
        enterButton.gameObject.SetActive(false);
        enterButton.onClick.RemoveAllListeners();
        enterButton.onClick.AddListener(OnEnterClicked);
    }

    void BuildRegionSlots()
    {
        if (regionRoot == null)
        {
            Debug.LogError("[WorldMapPopup] regionRoot 为空，无法生成地块：请给预制体根节点补挂 WorldMapPopup 组件，"
                + "并把 Region Root 拖到 Regions 节点上。", this);
            return;
        }
        // 保险：共享「进入」按钮可能正挂在某个地块下，重建地块前先收回弹窗根节点，免得被一起 Destroy
        if (enterButton != null && enterButton.transform.IsChildOf(regionRoot))
            enterButton.transform.SetParent(transform, false);

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

                // 外层 Region_X 可能只是空容器，真正的 Image/Button 在同名的内层子节点上。
                // 地块本体（Image + Button）预制体里统一放在内层同名节点上；外层只是容器。
                // 实测 Region_1 内层 m_IsActive=1（已解锁态），Region_2~8 内层 m_IsActive=0（美术存的是
                // 未解锁态：只留 Lock 锁图标）。所以这里按「谁带 Image」挑本体层，不管它当前开没开，
                // 显隐交给 RefreshRegions 按解锁状态切 —— 否则解锁了的章节也看不见、点不到。
                var visualT = t;
                var inner = t.Find(t.name);
                if (t.GetComponent<Image>() == null && inner != null && inner.GetComponent<Image>() != null)
                    visualT = inner;

                var slot = new RegionSlot
                {
                    chapterId = ch,
                    root = t.gameObject,
                    visual = visualT.gameObject,
                    highlight = hiT != null ? hiT.gameObject : null,
                    lockRoot = lockT != null ? lockT.gameObject : null,
                    nameRoot = FindChildByName(t, "bg"),
                    def = FindDef(ch)
                };

                // 地块自带的「进入」按钮（第一章没有）
                var ebT = t.Find("EnterButton");
                if (ebT != null)
                {
                    var eb = ebT.GetComponent<Button>();
                    if (eb != null)
                    {
                        eb.onClick.RemoveAllListeners();
                        eb.onClick.AddListener(OnEnterClicked);
                        eb.gameObject.SetActive(false);
                        slot.enterButton = eb;
                    }
                }

                // 点击：优先挂在内层（有 Image 才有射线命中）；内层没有就退回外层
                var btn = visualT.GetComponent<Button>();
                if (btn == null) btn = t.GetComponent<Button>();
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

        EnsureSharedEnterButton();
    }

    RegionDef FindDef(int chapterId)
    {
        for (int i = 0; i < regions.Length; i++)
            if (regions[i].chapterId == chapterId) return regions[i];
        return null;
    }

    static GameObject FindChildByName(Transform t, string name)
    {
        var f = t.Find(name);
        return f != null ? f.gameObject : null;
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
        /// <summary>真正带 Image 的节点（外层 Region_X 常常只是空容器，图在同名的内层子节点上）。</summary>
        public GameObject visual;
        public GameObject highlight;
        public GameObject lockRoot;
        /// <summary>名字底框（预制体里叫 bg），解锁才显示。</summary>
        public GameObject nameRoot;
        /// <summary>该地块自己的「进入」按钮（美术在每个 Region_X 下各放了一个）。为空则回退到共享按钮。</summary>
        public Button enterButton;
    }
}
