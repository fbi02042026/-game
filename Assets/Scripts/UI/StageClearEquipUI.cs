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

    public static void Show(List<EquipInstance> rewards, int bonusGold, Action<EquipInstance, bool> onDone)
    {
        var ui = Instance;
        if (ui == null)
        {
            var go = new GameObject("StageClearEquipUI");
            ui = go.AddComponent<StageClearEquipUI>();
            DontDestroyOnLoad(go);
        }
        ui.Open(rewards, bonusGold, onDone);
    }

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Open(List<EquipInstance> rewards, int bonusGold, Action<EquipInstance, bool> onDone)
    {
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
        _root.SetActive(true);
        RefreshCards();
        RefreshCompare();
        RefreshEquipButton();
        GameFonts.ApplyToHierarchy(_root.transform);
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

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var cgo = new GameObject("StageClearCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
        }

        _root = new GameObject("StageClearPanel", typeof(RectTransform));
        _root.transform.SetParent(canvas.transform, false);
        var rootRt = _root.GetComponent<RectTransform>();
        Stretch(rootRt);

        var dim = CreateImage(_root.transform, "Dim", new Color(0f, 0f, 0f, 0.72f));
        Stretch(dim.rectTransform);

        var panel = CreateImage(_root.transform, "Panel", new Color(0.1f, 0.09f, 0.14f, 0.96f));
        var prt = panel.rectTransform;
        prt.anchorMin = new Vector2(0.5f, 0.5f);
        prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(680f, 780f);
        prt.anchoredPosition = Vector2.zero;

        var title = CreateText(panel.transform, "Title", "关卡通关 · 选择一件装备", 30, TextAnchor.MiddleCenter);
        var trt = title.rectTransform;
        trt.anchorMin = new Vector2(0.5f, 1f);
        trt.anchorMax = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -36f);
        trt.sizeDelta = new Vector2(620f, 40f);

        float[] xs = { -210f, 0f, 210f };
        _cardBgs.Clear();
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            var card = CreateImage(panel.transform, "Card" + i, new Color(0.18f, 0.16f, 0.22f, 1f));
            var crt = card.rectTransform;
            crt.anchorMin = new Vector2(0.5f, 1f);
            crt.anchorMax = new Vector2(0.5f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = new Vector2(xs[i], -90f);
            crt.sizeDelta = new Vector2(190f, 280f);
            _cardBgs.Add(card);

            var btn = card.gameObject.AddComponent<Button>();
            btn.targetGraphic = card;
            btn.onClick.AddListener(() => Select(idx));

            CreateText(card.transform, "Name", "—", 20, TextAnchor.UpperCenter).rectTransform.anchoredPosition = new Vector2(0f, -16f);
            CreateText(card.transform, "Meta", "", 16, TextAnchor.UpperCenter).rectTransform.anchoredPosition = new Vector2(0f, -48f);
            var body = CreateText(card.transform, "Body", "", 16, TextAnchor.UpperLeft);
            var brt = body.rectTransform;
            brt.anchorMin = new Vector2(0.5f, 0.5f);
            brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = new Vector2(0f, -20f);
            brt.sizeDelta = new Vector2(170f, 180f);
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
        CreateButton(panel.transform, "DiscardBtn", new Vector2(120f, 48f), new Vector2(220f, 56f),
            new Color(0.55f, 0.28f, 0.28f, 1f), OnDiscard).GetComponentInChildren<Text>().text = "丢弃";
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
        for (int i = 0; i < 3; i++)
        {
            var card = _cardBgs[i];
            bool active = i < _rewards.Count && _rewards[i] != null;
            card.gameObject.SetActive(active);
            if (!active) continue;

            var eq = _rewards[i];
            bool sel = i == _selected;
            card.color = sel ? RarityColor(eq.rarity) : new Color(0.18f, 0.16f, 0.22f, 1f);
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
