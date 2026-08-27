using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 通关三选一：三张奖励卡并排；选中后若同槽已有装备则下方显示对比；
/// 底部「装备/替换」「丢弃」。不要求玩家点地上掉落物。
/// </summary>
public class StageClearEquipUI : MonoBehaviour
{
    public static StageClearEquipUI Instance { get; private set; }

    Action<EquipInstance, bool> _onDone; // equip, equippedOrReplaced (false=discarded selected)
    List<EquipInstance> _rewards;
    int _selected = 0;
    GameObject _root;
    readonly List<Image> _cardBgs = new List<Image>();
    Text _compareTitle;
    Text _compareBody;
    GameObject _comparePanel;
    Button _equipBtn;
    Text _equipBtnLabel;
    Button _discardBtn;
    Text _title;
    bool _pickup;

    public static void Show(List<EquipInstance> rewards, int bonusGold, Action<EquipInstance, bool> onDone)
    {
        Ensure().Open(rewards, bonusGold, onDone, pickup: false);
    }

    /// <summary>拾取模式：只展示一件已入包的装备，标题「捡到一件装备」，单个确定按钮。</summary>
    public static void ShowPickup(EquipInstance equip, Action onClosed)
    {
        Ensure().Open(new List<EquipInstance> { equip }, 0,
            (_, __) => onClosed?.Invoke(), pickup: true);
    }

    static StageClearEquipUI Ensure()
    {
        var ui = Instance;
        if (ui == null)
        {
            var go = new GameObject("StageClearEquipUI");
            ui = go.AddComponent<StageClearEquipUI>();
            DontDestroyOnLoad(go);
        }
        return ui;
    }

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Open(List<EquipInstance> rewards, int bonusGold, Action<EquipInstance, bool> onDone, bool pickup)
    {
        _pickup = pickup;
        _onDone = onDone;
        _rewards = new List<EquipInstance>();
        if (rewards != null)
        {
            for (int i = 0; i < rewards.Count && _rewards.Count < 3; i++)
                if (rewards[i] != null) _rewards.Add(rewards[i]);
        }
        if (_rewards.Count == 0)
        {
            Close(null, false);
            return;
        }
        _selected = 0;

        BuildIfNeeded();
        EnsureEventSystem();
        if (_root != null)
        {
            _root.SetActive(true);
            // 每次打开强制居中，防止父 Canvas 被改过后跑偏
            var rt = _root.GetComponent<RectTransform>();
            if (rt != null)
            {
                Stretch(rt);
                rt.anchoredPosition = Vector2.zero;
                rt.localScale = Vector3.one;
            }
            var panel = _root.transform.Find("Panel") as RectTransform;
            if (panel != null)
            {
                panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
                panel.pivot = new Vector2(0.5f, 0.5f);
                panel.anchoredPosition = Vector2.zero;
                panel.localScale = Vector3.one;
            }
            _root.transform.SetAsLastSibling();
        }
        RefreshTitle();
        RefreshCards();
        RefreshCompare();
        RefreshEquipButton();
        GameFonts.ApplyToHierarchy(_root.transform);
    }

    void RefreshTitle()
    {
        if (_title != null)
            _title.text = _rewards.Count <= 1 ? "捡到一件装备" : "选择一件装备";
        if (_discardBtn != null)
            _discardBtn.gameObject.SetActive(!_pickup);
        if (_equipBtn != null)
        {
            var rt = _equipBtn.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(_pickup ? 0f : -120f, 48f);
        }
        var panel = _root != null ? _root.transform.Find("Panel") as RectTransform : null;
        if (panel != null)
            panel.sizeDelta = _pickup ? new Vector2(560f, 620f) : new Vector2(680f, 780f);
    }

    static void EnsureEventSystem()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<UnityEngine.EventSystems.EventSystem>();
        go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }

    void BuildIfNeeded()
    {
        if (_root != null) return;

        // 独立 Overlay Canvas，避免挂到战斗 UI Canvas 上导致偏左/缩放错乱
        var cgo = new GameObject("StageClearCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        DontDestroyOnLoad(cgo);
        var canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 220;
        canvas.pixelPerfect = false;
        var scaler = cgo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720f, 1280f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        _root = new GameObject("StageClearPanel", typeof(RectTransform));
        _root.transform.SetParent(cgo.transform, false);
        var rootRt = _root.GetComponent<RectTransform>();
        Stretch(rootRt);
        rootRt.anchoredPosition = Vector2.zero;
        rootRt.localScale = Vector3.one;

        var dim = CreateImage(_root.transform, "Dim", new Color(0f, 0f, 0f, 0.72f));
        Stretch(dim.rectTransform);

        var panel = CreateImage(_root.transform, "Panel", new Color(0.1f, 0.09f, 0.14f, 0.96f));
        var prt = panel.rectTransform;
        prt.anchorMin = new Vector2(0.5f, 0.5f);
        prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = Vector2.zero;
        prt.sizeDelta = new Vector2(680f, 780f);
        prt.localScale = Vector3.one;

        _title = CreateText(panel.transform, "Title", "选择一件装备", 30, TextAnchor.MiddleCenter);
        var trt = _title.rectTransform;
        trt.anchorMin = new Vector2(0.5f, 1f);
        trt.anchorMax = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -36f);
        trt.sizeDelta = new Vector2(620f, 40f);

        _cardBgs.Clear();
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            var card = CreateImage(panel.transform, "Card" + i, new Color(0.18f, 0.16f, 0.22f, 1f));
            var crt = card.rectTransform;
            crt.anchorMin = new Vector2(0.5f, 1f);
            crt.anchorMax = new Vector2(0.5f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = new Vector2(0f, -90f);
            crt.sizeDelta = new Vector2(190f, 340f);
            _cardBgs.Add(card);

            var btn = card.gameObject.AddComponent<Button>();
            btn.targetGraphic = card;
            btn.onClick.AddListener(() => Select(idx));

            var icon = CreateImage(card.transform, "Icon", Color.white);
            var irt = icon.rectTransform;
            irt.anchorMin = new Vector2(0.5f, 1f);
            irt.anchorMax = new Vector2(0.5f, 1f);
            irt.pivot = new Vector2(0.5f, 1f);
            irt.anchoredPosition = new Vector2(0f, -12f);
            irt.sizeDelta = new Vector2(96f, 96f);
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            AnchorTop(CreateText(card.transform, "Name", "—", 20, TextAnchor.UpperCenter).rectTransform, -116f, 170f, 28f);
            AnchorTop(CreateText(card.transform, "Meta", "", 16, TextAnchor.UpperCenter).rectTransform, -148f, 170f, 24f);
            var body = CreateText(card.transform, "Body", "", 16, TextAnchor.UpperLeft);
            var brt = body.rectTransform;
            brt.anchorMin = new Vector2(0.5f, 1f);
            brt.anchorMax = new Vector2(0.5f, 1f);
            brt.pivot = new Vector2(0.5f, 1f);
            brt.anchoredPosition = new Vector2(0f, -180f);
            brt.sizeDelta = new Vector2(170f, 150f);
        }

        _comparePanel = CreateImage(panel.transform, "Compare", new Color(0.14f, 0.18f, 0.22f, 1f)).gameObject;
        var cprt = _comparePanel.GetComponent<RectTransform>();
        cprt.anchorMin = new Vector2(0.5f, 0f);
        cprt.anchorMax = new Vector2(0.5f, 0f);
        cprt.pivot = new Vector2(0.5f, 0f);
        cprt.anchoredPosition = new Vector2(0f, 150f);
        cprt.sizeDelta = new Vector2(620f, 160f);
        _compareTitle = CreateText(_comparePanel.transform, "CompareTitle", "当前已装备", 22, TextAnchor.UpperCenter);
        _compareTitle.rectTransform.anchoredPosition = new Vector2(0f, -14f);
        _compareBody = CreateText(_comparePanel.transform, "CompareBody", "", 18, TextAnchor.UpperLeft);
        var cbrt = _compareBody.rectTransform;
        cbrt.anchoredPosition = new Vector2(0f, -20f);
        cbrt.sizeDelta = new Vector2(580f, 120f);

        _equipBtn = CreateButton(panel.transform, "EquipBtn", new Vector2(-120f, 48f), new Vector2(220f, 56f),
            new Color(0.25f, 0.55f, 0.35f, 1f), OnEquipOrReplace);
        _equipBtnLabel = _equipBtn.GetComponentInChildren<Text>();
        _discardBtn = CreateButton(panel.transform, "DiscardBtn", new Vector2(120f, 48f), new Vector2(220f, 56f),
            new Color(0.55f, 0.28f, 0.28f, 1f), OnDiscard);
        _discardBtn.GetComponentInChildren<Text>().text = "丢弃";
    }

    void Select(int idx)
    {
        if (idx < 0 || idx >= _rewards.Count || _rewards[idx] == null) return;
        _selected = idx;
        RefreshCards();
        RefreshCompare();
        RefreshEquipButton();
    }

    void RefreshCards()
    {
        int count = _rewards != null ? _rewards.Count : 0;
        for (int i = 0; i < 3; i++)
        {
            var card = _cardBgs[i];
            bool active = i < count && _rewards[i] != null;
            card.gameObject.SetActive(active);
            if (!active) continue;

            // 一张卡时居中，两三张时左右铺开
            float step = 210f;
            card.rectTransform.anchoredPosition = new Vector2((i - (count - 1) * 0.5f) * step, -90f);

            var eq = _rewards[i];
            bool sel = i == _selected;
            card.color = sel ? RarityColor(eq.rarity) : new Color(0.18f, 0.16f, 0.22f, 1f);
            EquipIcons.Resolve(eq);
            var icon = card.transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null)
            {
                icon.gameObject.SetActive(true);
                icon.sprite = eq.icon;
                icon.enabled = eq.icon != null;
                icon.preserveAspect = true;
                icon.color = Color.white;
                if (eq.icon == null)
                    Debug.LogWarning($"[StageClearEquip] 卡片{i}无图标 name={eq.equipName} file={eq.template?.iconFileName} id={eq.templateId}");
            }
            var name = card.transform.Find("Name")?.GetComponent<Text>();
            var meta = card.transform.Find("Meta")?.GetComponent<Text>();
            var body = card.transform.Find("Body")?.GetComponent<Text>();
            if (name != null) name.text = eq.equipName ?? "装备";
            if (meta != null) meta.text = $"{eq.slotType}  ★{eq.star}  {eq.rarity}";
            if (body != null) body.text = FormatAttrs(eq);
        }
    }

    void RefreshCompare()
    {
        if (_pickup)
        {
            if (_comparePanel != null) _comparePanel.SetActive(false);
            return;
        }
        var sel = GetSelected();
        EquipInstance worn = null;
        if (sel != null && GridBackpackSystem.Instance != null)
            worn = GridBackpackSystem.Instance.GetEquippedInSlot(sel.slotType);

        bool show = worn != null;
        if (_comparePanel != null) _comparePanel.SetActive(show);
        if (!show) return;
        if (_compareTitle != null) _compareTitle.text = $"当前已装备（{worn.slotType}）";
        if (_compareBody != null)
            _compareBody.text = $"{worn.equipName}  ★{worn.star}  {worn.rarity}\n{FormatAttrs(worn)}";
    }

    void RefreshEquipButton()
    {
        if (_pickup)
        {
            if (_equipBtnLabel != null) _equipBtnLabel.text = "放入背包";
            return;
        }
        var sel = GetSelected();
        bool hasWorn = false;
        if (sel != null && GridBackpackSystem.Instance != null)
            hasWorn = GridBackpackSystem.Instance.GetEquippedInSlot(sel.slotType) != null;
        if (_equipBtnLabel != null)
            _equipBtnLabel.text = hasWorn ? "替换" : "装备";
    }

    EquipInstance GetSelected()
    {
        if (_rewards == null || _selected < 0 || _selected >= _rewards.Count) return null;
        return _rewards[_selected];
    }

    void OnEquipOrReplace()
    {
        var sel = GetSelected();
        if (sel == null)
        {
            UIManager.Instance?.ShowToast("请选择一件装备");
            return;
        }
        Close(sel, true);
    }

    void OnDiscard()
    {
        var sel = GetSelected();
        Close(sel, false);
    }

    void Close(EquipInstance selected, bool equipOrReplace)
    {
        if (_root != null) _root.SetActive(false);
        var cb = _onDone;
        _onDone = null;
        cb?.Invoke(selected, equipOrReplace);
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
            sb.Append(a.attrType).Append(" +").Append(v);
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

    static void AnchorTop(RectTransform rt, float y, float w, float h)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
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
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    static Text CreateText(Transform parent, string name, string content, int size, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(180f, 28f);
        var t = go.AddComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.font = GameFonts.GetChinese();
        if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return t;
    }

    static Button CreateButton(Transform parent, string name, Vector2 pos, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick)
    {
        var img = CreateImage(parent, name, color);
        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);
        var label = CreateText(img.transform, "Label", name, 24, TextAnchor.MiddleCenter);
        label.rectTransform.anchoredPosition = Vector2.zero;
        label.rectTransform.sizeDelta = size;
        return btn;
    }
}
