using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>掉落装备弹窗的三种形态。</summary>
public enum EquipDropMode
{
    /// <summary>身上该部位没装备：装备 / 放入背包。</summary>
    EmptySlot = 0,
    /// <summary>身上该部位已有装备：显示对比，替换 / 丢弃。</summary>
    ReplaceWorn = 1,
    /// <summary>宝箱三选一。</summary>
    ChooseOne = 2,
}

/// <summary>
/// 战斗中掉落装备弹窗。预制体：Resources/Prefabs/Battle/EquipDropPopup。
/// 预制体缺失时用代码搭同样的结构，保证功能不断。
/// </summary>
public class EquipDropPopupUI : MonoBehaviour
{
    public const string PrefabPath = "Prefabs/Battle/EquipDropPopup";

    public static EquipDropPopupUI Instance { get; private set; }

    [Header("自动按名字绑定，可在 Inspector 覆盖")]
    public GameObject root;
    public Text titleText;
    public Button closeButton;
    public GameObject comparePanel;
    public Text compareTitle;
    public Text compareBody;
    public Button primaryButton;
    public Text primaryLabel;
    public Button secondaryButton;
    public Text secondaryLabel;
    public List<CardRefs> cards = new List<CardRefs>();

    [Serializable]
    public class CardRefs
    {
        public GameObject root;
        public Image background;
        public Image icon;
        public Text name;
        public Text meta;
        public Text attrs;
        public GameObject selectedMark;
    }

    [Tooltip("代码搭建时才由脚本摆卡片位置；用美术预制体时保持关闭，避免覆盖手摆布局")]
    public bool autoLayoutCards;

    EquipDropMode _mode;
    readonly List<EquipInstance> _drops = new List<EquipInstance>();
    int _selected;
    Action<EquipInstance, bool> _onDone;
    Vector2[] _cardHomePos;
    bool _fontSizeAdjusted;

    public bool IsOpen => root != null && root.activeSelf;
    public EquipDropMode Mode => _mode;

    // ===== 对外入口 =====

    /// <summary>战斗中掉落一件：自动判断「没装备」还是「要替换」。</summary>
    public static void ShowSingle(EquipInstance equip, Action<EquipInstance, bool> onDone = null)
    {
        if (equip == null)
        {
            onDone?.Invoke(null, false);
            return;
        }
        bool hasWorn = GridBackpackSystem.Instance != null
            && GridBackpackSystem.Instance.GetEquippedInLogicalSlot(
                WeaponLoadoutRules.IsLoadoutItem(equip)
                    ? WeaponLoadoutRules.ResolveLogicalSlot(equip)
                    : equip.slotType) != null;
        Ensure().Open(hasWorn ? EquipDropMode.ReplaceWorn : EquipDropMode.EmptySlot,
            new List<EquipInstance> { equip }, onDone);
    }

    /// <summary>宝箱三选一。</summary>
    public static void ShowChooseOne(List<EquipInstance> rewards, Action<EquipInstance, bool> onDone = null)
    {
        Ensure().Open(EquipDropMode.ChooseOne, rewards, onDone);
    }

    public static EquipDropPopupUI Ensure()
    {
        if (Instance != null) return Instance;

        var prefab = Resources.Load<GameObject>(PrefabPath);
        GameObject go;
        bool built = false;
        if (prefab != null)
        {
            go = Instantiate(prefab);
            go.name = "EquipDropPopup";
        }
        else
        {
            Debug.LogWarning($"[EquipDropPopup] 未找到预制体 {PrefabPath}，改用代码搭建");
            go = new GameObject("EquipDropPopup");
            BuildHierarchy(go);
            built = true;
        }
        DontDestroyOnLoad(go);

        var ui = go.GetComponent<EquipDropPopupUI>();
        if (ui == null) ui = go.AddComponent<EquipDropPopupUI>();
        if (built) ui.autoLayoutCards = true;
        return ui;
    }

    void Awake()
    {
        Instance = this;
        Bind();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ===== 打开 / 刷新 =====

    void Open(EquipDropMode mode, List<EquipInstance> drops, Action<EquipInstance, bool> onDone)
    {
        Bind();
        _mode = mode;
        _onDone = onDone;
        _drops.Clear();
        if (drops != null)
        {
            for (int i = 0; i < drops.Count && _drops.Count < 3; i++)
                if (drops[i] != null) _drops.Add(drops[i]);
        }
        if (_drops.Count == 0)
        {
            Finish(null, false);
            return;
        }
        if (_mode != EquipDropMode.ChooseOne && _drops.Count > 1)
            _drops.RemoveRange(1, _drops.Count - 1);

        _selected = 0;
        EnsureEventSystem();
        EnsureCanvas();
        if (root != null)
        {
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }
        RefreshAll();
        if (!_fontSizeAdjusted)
        {
            BumpPopupFontSize(+2);
            _fontSizeAdjusted = true;
        }
        GameFonts.ApplyToHierarchy(transform);
    }

    void BumpPopupFontSize(int delta)
    {
        if (delta == 0) return;
        BumpTextFont(titleText, delta);
        BumpTextFont(compareTitle, delta);
        BumpTextFont(compareBody, delta);
        BumpTextFont(primaryLabel, delta);
        BumpTextFont(secondaryLabel, delta);
        if (cards == null) return;
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            if (c == null) continue;
            BumpTextFont(c.name, delta);
            BumpTextFont(c.meta, delta);
            BumpTextFont(c.attrs, delta);
        }
    }

    static void BumpTextFont(Text t, int delta)
    {
        if (t == null || delta == 0) return;
        t.fontSize = Mathf.Max(12, t.fontSize + delta);
    }

    void RefreshAll()
    {
        RefreshTitle();
        RefreshCards();
        RefreshCompare();
        RefreshButtons();
    }

    void RefreshTitle()
    {
        if (titleText == null) return;
        switch (_mode)
        {
            case EquipDropMode.ChooseOne:
                titleText.text = "选择一件装备";
                break;
            case EquipDropMode.ReplaceWorn:
                titleText.text = "捡到一件装备";
                break;
            default:
                titleText.text = "捡到一件装备";
                break;
        }
    }

    void RefreshCards()
    {
        CacheCardHomePositions();
        int count = _drops.Count;
        // 单件：关掉另外两张，把留下的那张摆到三卡布局的水平中心（不写回预制体）
        bool centerSingle = count == 1;
        bool layoutMulti = !centerSingle && ShouldLayoutCards();
        float step = CardLayoutStep();
        Vector2 singleCenter = ComputeCardsCenter();

        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            if (c == null || c.root == null) continue;
            bool active = i < count;
            c.root.SetActive(active);
            if (!active)
            {
                // 还原闲置卡的家位置，下次三选一不歪
                RestoreCardHome(i);
                continue;
            }

            var rt = c.root.GetComponent<RectTransform>();
            if (rt != null)
            {
                if (centerSingle)
                    rt.anchoredPosition = new Vector2(singleCenter.x, HomeY(i));
                else if (layoutMulti)
                    rt.anchoredPosition = new Vector2((i - (count - 1) * 0.5f) * step, HomeY(i));
                else
                    RestoreCardHome(i);
            }

            var eq = _drops[i];
            if (eq == null) continue;
            bool sel = i == _selected;
            EnsureEquipIcon(eq);
            if (c.icon == null && c.root != null)
                c.icon = FindDeep(c.root.transform, "Icon")?.GetComponent<Image>();
            if (c.background != null)
                c.background.color = sel ? RarityColor(eq.rarity) : new Color(0.18f, 0.16f, 0.22f, 1f);
            if (c.selectedMark != null)
                c.selectedMark.SetActive(sel && count > 1);
            if (c.icon != null)
            {
                c.icon.gameObject.SetActive(true);
                c.icon.sprite = eq.icon;
                c.icon.color = Color.white;
                c.icon.preserveAspect = true;
                c.icon.enabled = eq.icon != null;
                if (eq.icon == null)
                    Debug.LogWarning($"[EquipDropPopup] 卡片{i}无图标 name={eq.equipName} file={eq.template?.iconFileName}");
            }
            if (c.name != null) c.name.text = EquipUiText.EquipTitleWithHand(eq);
            if (c.meta != null)
            {
                c.meta.text = EquipUiText.RarityName(eq.rarity);
                c.meta.color = EquipUiText.RarityTextColor(eq.rarity);
            }
            if (c.attrs != null) c.attrs.text = FormatAttrs(eq);
        }
    }

    void CacheCardHomePositions()
    {
        if (cards == null || cards.Count == 0) return;
        if (_cardHomePos != null && _cardHomePos.Length == cards.Count) return;
        _cardHomePos = new Vector2[cards.Count];
        for (int i = 0; i < cards.Count; i++)
        {
            var rt = cards[i]?.root != null ? cards[i].root.GetComponent<RectTransform>() : null;
            _cardHomePos[i] = rt != null ? rt.anchoredPosition : Vector2.zero;
        }
    }

    Vector2 ComputeCardsCenter()
    {
        if (_cardHomePos == null || _cardHomePos.Length == 0) return Vector2.zero;
        Vector2 sum = Vector2.zero;
        int n = 0;
        for (int i = 0; i < _cardHomePos.Length; i++)
        {
            sum += _cardHomePos[i];
            n++;
        }
        return n > 0 ? sum / n : Vector2.zero;
    }

    float HomeY(int i)
    {
        if (_cardHomePos != null && i >= 0 && i < _cardHomePos.Length)
            return _cardHomePos[i].y;
        return 0f;
    }

    void RestoreCardHome(int i)
    {
        if (_cardHomePos == null || i < 0 || i >= _cardHomePos.Length) return;
        var rt = cards[i]?.root != null ? cards[i].root.GetComponent<RectTransform>() : null;
        if (rt != null) rt.anchoredPosition = _cardHomePos[i];
    }

    /// <summary>
    /// 预制体里 Canvas 是 Screen Space - Camera 但没存相机（DontDestroyOnLoad 后也拿不到），
    /// 每次打开重绑一次 Camera.main，避免缩放/层级错位。
    /// </summary>
    void EnsureCanvas()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) return;
        if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null)
            UICanvasSetup.Apply(canvas, Camera.main);
    }

    /// <summary>
    /// 多件且允许自动摆位时才动坐标。单件由 RefreshCards 单独居中。
    /// 美术预制体三选一保持手摆位置（autoLayoutCards=false）。
    /// </summary>
    bool ShouldLayoutCards()
    {
        if (_drops.Count <= 1) return false;
        if (!autoLayoutCards) return false;
        var parent = cards.Count > 0 && cards[0]?.root != null
            ? cards[0].root.transform.parent
            : null;
        return parent == null || parent.GetComponent<LayoutGroup>() == null;
    }

    /// <summary>按卡片实际宽度算间距，别再写死 210 导致宽卡重叠。</summary>
    float CardLayoutStep()
    {
        float width = 196f;
        for (int i = 0; i < cards.Count; i++)
        {
            var rt = cards[i]?.root != null ? cards[i].root.GetComponent<RectTransform>() : null;
            if (rt != null && rt.rect.width > 1f)
            {
                width = rt.rect.width;
                break;
            }
        }
        float gap = 14f;
        float step = width + gap;

        // 三张卡不能超出面板可用宽度
        var panel = cards.Count > 0 && cards[0]?.root != null
            ? cards[0].root.transform.parent as RectTransform
            : null;
        if (panel != null && panel.rect.width > 1f && _drops.Count > 1)
        {
            float maxStep = (panel.rect.width - 24f - width) / (_drops.Count - 1);
            if (maxStep > 0f && maxStep < step) step = maxStep;
        }
        return step;
    }

    void RefreshCompare()
    {
        var sel = GetSelected();
        if (sel == null)
        {
            if (comparePanel != null) comparePanel.SetActive(false);
            return;
        }

        EquipInstance worn = null;
        if (GridBackpackSystem.Instance != null)
        {
            EquipSlotType compareSlot = WeaponLoadoutRules.IsLoadoutItem(sel)
                ? WeaponLoadoutRules.ResolveLogicalSlot(sel)
                : sel.slotType;
            worn = GridBackpackSystem.Instance.GetEquippedInLogicalSlot(compareSlot);
        }

        // 下部对比区始终显示：有则属性，无则「当前部位无装备」
        if (comparePanel != null) comparePanel.SetActive(true);
        string slotName = WeaponLoadoutRules.IsLoadoutItem(sel)
            ? EquipUiText.WeaponHand(sel.weaponHand, sel.weaponType)
            : EquipUiText.Slot(sel.slotType);
        if (worn != null)
        {
            if (compareTitle != null) compareTitle.text = $"当前已装备（{slotName}）";
            if (compareBody != null)
                compareBody.text = FormatAttrs(worn);
        }
        else
        {
            if (compareTitle != null) compareTitle.text = $"当前部位（{slotName}）";
            if (compareBody != null) compareBody.text = "当前部位无装备";
        }
    }

    void RefreshButtons()
    {
        var sel = GetSelected();
        bool hasWorn = sel != null && GridBackpackSystem.Instance != null
            && GridBackpackSystem.Instance.GetEquippedInLogicalSlot(
                WeaponLoadoutRules.IsLoadoutItem(sel)
                    ? WeaponLoadoutRules.ResolveLogicalSlot(sel)
                    : sel.slotType) != null;

        if (primaryLabel != null)
            primaryLabel.text = hasWorn ? "替换" : "装备";

        if (secondaryButton != null) secondaryButton.gameObject.SetActive(true);
        if (secondaryLabel != null)
            secondaryLabel.text = _mode == EquipDropMode.ReplaceWorn ? "丢弃" : "放入背包";
    }

    static void EnsureEquipIcon(EquipInstance eq) => EquipIcons.Resolve(eq);

    EquipInstance GetSelected()
    {
        if (_selected < 0 || _selected >= _drops.Count) return null;
        return _drops[_selected];
    }

    void SelectCard(int idx)
    {
        if (idx < 0 || idx >= _drops.Count) return;
        if (_mode != EquipDropMode.ChooseOne) return;
        _selected = idx;
        RefreshCards();
        RefreshCompare();
        RefreshButtons();
    }

    // ===== 按钮 =====

    void OnPrimary()
    {
        var sel = GetSelected();
        if (sel == null)
        {
            UIManager.Instance?.ShowToast("请先选一件装备");
            return;
        }
        var bag = GridBackpackSystem.Instance;
        if (bag == null)
        {
            Finish(sel, true);
            return;
        }

        if (WeaponLoadoutRules.IsLoadoutItem(sel))
        {
            if (!bag.TryAcquireLoadoutItem(sel, out _))
            {
                UIManager.Instance?.ShowToast("无法装备该武器");
                return;
            }
            BattleUI.Instance?.UpdateBackpackGrid();
            Finish(sel, true);
            return;
        }

        var item = FindBackpackItem(bag, sel);
        if (item == null && !bag.TryAddItem(sel, out item))
        {
            UIManager.Instance?.ShowToast("背包空间不足，先整理一下");
            return;
        }
        bag.EquipItem(item);
        BattleUI.Instance?.UpdateBackpackGrid();
        Finish(sel, true);
    }

    void OnSecondary()
    {
        var sel = GetSelected();
        var bag = GridBackpackSystem.Instance;

        if (_mode == EquipDropMode.ReplaceWorn)
        {
            // 丢弃：已经在包里就移除
            if (bag != null && sel != null)
            {
                var item = FindBackpackItem(bag, sel);
                if (item != null) bag.DropItem(item);
            }
            BattleUI.Instance?.UpdateBackpackGrid();
            Finish(sel, false);
            return;
        }

        // 放入背包（不穿戴）
        if (bag != null && sel != null && FindBackpackItem(bag, sel) == null
            && !bag.TryAddItem(sel, out _))
        {
            UIManager.Instance?.ShowToast("背包空间不足，先整理一下");
            return;
        }
        BattleUI.Instance?.UpdateBackpackGrid();
        UIManager.Instance?.ShowToast("已放入背包，点格子可装备并实时换装");
        Finish(sel, false);
    }

    void OnClose()
    {
        // 关闭等于「留在背包不穿」，绝不让装备凭空消失
        var sel = GetSelected();
        var bag = GridBackpackSystem.Instance;
        if (bag != null && sel != null && FindBackpackItem(bag, sel) == null)
            bag.TryAddItem(sel, out _);
        BattleUI.Instance?.UpdateBackpackGrid();
        Finish(sel, false);
    }

    void Finish(EquipInstance selected, bool equipped)
    {
        if (root != null) root.SetActive(false);
        var cb = _onDone;
        _onDone = null;
        cb?.Invoke(selected, equipped);
    }

    static GridBackpackSystem.BackpackItem FindBackpackItem(GridBackpackSystem bag, EquipInstance equip)
    {
        var all = bag.GetAllBackpackItems();
        if (all == null) return null;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] != null && all[i].equip == equip) return all[i];
        }
        return null;
    }

    // ===== 绑定 =====

    void Bind()
    {
        if (root == null)
            root = FindDeep(transform, "Root")?.gameObject ?? gameObject;
        if (titleText == null) titleText = FindText("Title");
        if (compareTitle == null) compareTitle = FindText("CompareTitle");
        if (compareBody == null) compareBody = FindText("CompareBody");
        if (comparePanel == null) comparePanel = FindDeep(transform, "ComparePanel")?.gameObject;

        if (closeButton == null) closeButton = FindButton("CloseButton");
        if (primaryButton == null) primaryButton = FindButton("PrimaryButton");
        if (secondaryButton == null) secondaryButton = FindButton("SecondaryButton");
        if (primaryLabel == null && primaryButton != null)
            primaryLabel = primaryButton.GetComponentInChildren<Text>(true);
        if (secondaryLabel == null && secondaryButton != null)
            secondaryLabel = secondaryButton.GetComponentInChildren<Text>(true);

        WireOnce(closeButton, OnClose);
        WireOnce(primaryButton, OnPrimary);
        WireOnce(secondaryButton, OnSecondary);

        if (cards == null) cards = new List<CardRefs>();
        if (cards.Count == 0)
        {
            for (int i = 0; i < 3; i++)
            {
                Transform t = FindDeep(transform, "Card" + i);
                if (t == null) continue;
                cards.Add(new CardRefs
                {
                    root = t.gameObject,
                    background = t.GetComponent<Image>(),
                    icon = FindDeep(t, "Icon")?.GetComponent<Image>(),
                    name = FindDeep(t, "Name")?.GetComponent<Text>(),
                    meta = FindDeep(t, "Meta")?.GetComponent<Text>(),
                    attrs = FindDeep(t, "Attrs")?.GetComponent<Text>(),
                    selectedMark = FindDeep(t, "SelectedMark")?.gameObject
                });
            }
        }
        // Inspector 已填 cards 时也要补全缺失的 Icon 引用
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            if (c == null || c.root == null) continue;
            if (c.icon == null)
                c.icon = FindDeep(c.root.transform, "Icon")?.GetComponent<Image>();
            if (c.name == null)
                c.name = FindDeep(c.root.transform, "Name")?.GetComponent<Text>();
            if (c.meta == null)
                c.meta = FindDeep(c.root.transform, "Meta")?.GetComponent<Text>();
            if (c.attrs == null)
                c.attrs = FindDeep(c.root.transform, "Attrs")?.GetComponent<Text>();
        }
        // 点击一律重绑：Inspector 里手填 cards 时也要能选卡
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            if (c == null || c.root == null) continue;
            int idx = i;
            var btn = c.root.GetComponent<Button>();
            if (btn == null) btn = c.root.AddComponent<Button>();
            if (btn.targetGraphic == null) btn.targetGraphic = c.background;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => SelectCard(idx));
        }

        var canvas = GetComponent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 520;
        }
        if (root != null && root != gameObject) root.SetActive(false);
    }

    static void WireOnce(Button btn, UnityEngine.Events.UnityAction action)
    {
        if (btn == null) return;
        btn.onClick.RemoveListener(action);
        btn.onClick.AddListener(action);
    }

    Text FindText(string name) => FindDeep(transform, name)?.GetComponent<Text>();
    Button FindButton(string name) => FindDeep(transform, name)?.GetComponent<Button>();

    static Transform FindDeep(Transform parent, string name)
    {
        if (parent == null) return null;
        if (string.Equals(parent.name, name, StringComparison.OrdinalIgnoreCase)) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var r = FindDeep(parent.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    static void EnsureEventSystem()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<UnityEngine.EventSystems.EventSystem>();
        go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }

    static string FormatAttrs(EquipInstance eq)
    {
        if (eq?.attrBonus == null || eq.attrBonus.Count == 0) return "（无额外属性）";
        var sb = new System.Text.StringBuilder();
        int n = Mathf.Min(6, eq.attrBonus.Count);
        for (int i = 0; i < n; i++)
        {
            var a = eq.attrBonus[i];
            if (a == null) continue;
            string v = a.isPercent ? $"{a.value * 100f:0.#}%" : a.value.ToString("0.#");
            sb.Append(EquipUiText.Attr(a.attrType)).Append(" +").Append(v);
            if (i < n - 1) sb.Append('\n');
        }
        return sb.ToString();
    }

    static Color RarityColor(Rarity r)
    {
        switch (r)
        {
            case Rarity.Uncommon: return new Color(0.2f, 0.45f, 0.25f, 1f);
            case Rarity.Rare: return new Color(0.2f, 0.35f, 0.55f, 1f);
            case Rarity.Epic: return new Color(0.4f, 0.25f, 0.55f, 1f);
            case Rarity.Legendary: return new Color(0.55f, 0.4f, 0.15f, 1f);
            default: return new Color(0.35f, 0.35f, 0.38f, 1f);
        }
    }

    // ===== 结构搭建（运行时兜底 + 编辑器生成预制体共用）=====

    /// <summary>在 host 上搭出完整弹窗结构。编辑器生成预制体也走这里，保证两边一致。</summary>
    public static void BuildHierarchy(GameObject host)
    {
        if (host == null) return;

        var canvas = host.GetComponent<Canvas>();
        if (canvas == null) canvas = host.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 520;

        var scaler = host.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = host.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720f, 1280f);
        scaler.matchWidthOrHeight = 1f;

        if (host.GetComponent<GraphicRaycaster>() == null)
            host.AddComponent<GraphicRaycaster>();

        var rootGo = new GameObject("Root", typeof(RectTransform));
        rootGo.transform.SetParent(host.transform, false);
        Stretch(rootGo.GetComponent<RectTransform>());

        var dim = CreateImage(rootGo.transform, "Dim", new Color(0f, 0f, 0f, 0.72f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        var panel = CreateImage(rootGo.transform, "Panel", new Color(0.1f, 0.09f, 0.14f, 0.97f));
        var prt = panel.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = Vector2.zero;
        prt.sizeDelta = new Vector2(680f, 800f);

        var title = CreateText(panel.transform, "Title", "捡到一件装备", 32, TextAnchor.MiddleCenter);
        var trt = title.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -34f);
        trt.sizeDelta = new Vector2(600f, 44f);

        CreateButton(panel.transform, "CloseButton", "×", new Vector2(1f, 1f),
            new Vector2(-36f, -36f), new Vector2(56f, 56f), new Color(0.42f, 0.22f, 0.22f, 1f));

        for (int i = 0; i < 3; i++)
            BuildCard(panel.transform, i);

        var compare = CreateImage(panel.transform, "ComparePanel", new Color(0.14f, 0.18f, 0.22f, 1f));
        var crt = compare.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0f);
        crt.pivot = new Vector2(0.5f, 0f);
        crt.anchoredPosition = new Vector2(0f, 168f);
        crt.sizeDelta = new Vector2(620f, 170f);

        var ct = CreateText(compare.transform, "CompareTitle", "当前已装备", 22, TextAnchor.UpperCenter);
        var ctrt = ct.rectTransform;
        ctrt.anchorMin = ctrt.anchorMax = new Vector2(0.5f, 1f);
        ctrt.pivot = new Vector2(0.5f, 1f);
        ctrt.anchoredPosition = new Vector2(0f, -12f);
        ctrt.sizeDelta = new Vector2(580f, 30f);

        var cb = CreateText(compare.transform, "CompareBody", "", 18, TextAnchor.UpperLeft);
        var cbrt = cb.rectTransform;
        cbrt.anchorMin = cbrt.anchorMax = new Vector2(0.5f, 1f);
        cbrt.pivot = new Vector2(0.5f, 1f);
        cbrt.anchoredPosition = new Vector2(0f, -46f);
        cbrt.sizeDelta = new Vector2(580f, 118f);

        CreateButton(panel.transform, "PrimaryButton", "装备", new Vector2(0.5f, 0f),
            new Vector2(-118f, 54f), new Vector2(220f, 62f), new Color(0.25f, 0.55f, 0.35f, 1f));
        CreateButton(panel.transform, "SecondaryButton", "放入背包", new Vector2(0.5f, 0f),
            new Vector2(118f, 54f), new Vector2(220f, 62f), new Color(0.4f, 0.36f, 0.3f, 1f));

        GameFonts.ApplyToHierarchy(host.transform);
    }

    static void BuildCard(Transform parent, int index)
    {
        var card = CreateImage(parent, "Card" + index, new Color(0.18f, 0.16f, 0.22f, 1f));
        var rt = card.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2((index - 1) * 210f, -96f);
        rt.sizeDelta = new Vector2(196f, 350f);
        card.gameObject.AddComponent<Button>().targetGraphic = card;

        // 选中高亮：子节点必然盖在卡片底图上，所以用半透明描边色，不要实心
        // 想要好看的选中框，把 9 宫格边框图拖到这个 Image 上即可
        var mark = CreateImage(card.transform, "SelectedMark", new Color(1f, 0.86f, 0.4f, 0.22f));
        var mrt = mark.rectTransform;
        mrt.anchorMin = Vector2.zero;
        mrt.anchorMax = Vector2.one;
        mrt.offsetMin = new Vector2(-4f, -4f);
        mrt.offsetMax = new Vector2(4f, 4f);
        mark.raycastTarget = false;
        mark.transform.SetAsFirstSibling();

        var icon = CreateImage(card.transform, "Icon", Color.white);
        var irt = icon.rectTransform;
        irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 1f);
        irt.pivot = new Vector2(0.5f, 1f);
        irt.anchoredPosition = new Vector2(0f, -14f);
        irt.sizeDelta = new Vector2(100f, 100f);
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        AnchorTop(CreateText(card.transform, "Name", "—", 22, TextAnchor.UpperCenter).rectTransform, -122f, 176f, 30f);
        AnchorTop(CreateText(card.transform, "Meta", "", 18, TextAnchor.UpperCenter).rectTransform, -156f, 176f, 26f);
        AnchorTop(CreateText(card.transform, "Attrs", "", 18, TextAnchor.UpperLeft).rectTransform, -188f, 176f, 150f);
    }

    static void AnchorTop(RectTransform rt, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Image CreateImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    static Text CreateText(Transform parent, string name, string content, int size, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.font = GameFonts.GetChinese();
        if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return t;
    }

    static Button CreateButton(Transform parent, string name, string label, Vector2 anchor,
        Vector2 pos, Vector2 size, Color color)
    {
        var img = CreateImage(parent, name, color);
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var text = CreateText(img.transform, "Label", label, 26, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform);
        return btn;
    }
}
