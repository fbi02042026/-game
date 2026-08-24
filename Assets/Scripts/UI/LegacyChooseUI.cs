using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 死亡/撤离：从本局装备中选 1 件作为遗产带回。
/// </summary>
public class LegacyChooseUI : MonoBehaviour
{
    public static LegacyChooseUI Instance { get; private set; }

    Action<EquipInstance> _onDone;
    List<EquipInstance> _equips;
    int _selected;
    GameObject _root;
    readonly List<Image> _cardBgs = new List<Image>();
    readonly List<Text> _cardLabels = new List<Text>();
    Text _title;

    public static void Show(List<EquipInstance> equips, Action<EquipInstance> onDone)
    {
        Ensure().Open(equips, onDone);
    }

    static LegacyChooseUI Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("LegacyChooseUI");
        DontDestroyOnLoad(go);
        return go.AddComponent<LegacyChooseUI>();
    }

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Open(List<EquipInstance> equips, Action<EquipInstance> onDone)
    {
        _onDone = onDone;
        _equips = equips != null ? new List<EquipInstance>(equips) : new List<EquipInstance>();
        _selected = 0;
        BuildIfNeeded();
        Refresh();
        if (_root != null)
        {
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
        }
        EnsureEventSystem();
        GameFonts.ApplyToHierarchy(transform);

        if (_equips.Count == 0)
        {
            // 无可选遗产：直接关闭
            Close(null);
        }
    }

    void BuildIfNeeded()
    {
        if (_root != null) return;

        var canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            gameObject.AddComponent<GraphicRaycaster>();
        }
        UICanvasSetup.Apply(canvas);
        canvas.sortingOrder = 960;

        _root = new GameObject("Root", typeof(RectTransform));
        _root.transform.SetParent(transform, false);
        Stretch(_root.GetComponent<RectTransform>());

        var dim = CreateUi("Dim", _root.transform, typeof(Image));
        Stretch(dim.GetComponent<RectTransform>());
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

        var panel = CreateUi("Panel", _root.transform, typeof(Image));
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(660f, 540f);
        panel.GetComponent<Image>().color = new Color(0.12f, 0.1f, 0.14f, 0.97f);

        _title = CreateText(panel.transform, "Title", "选择一件遗产带回", 30, TextAnchor.MiddleCenter);
        AnchorTop(_title.rectTransform, -24f, 600f, 40f);

        CreateText(panel.transform, "Hint", "其余装备本局结束；未选则带回品质最高的一件", 18, TextAnchor.MiddleCenter)
            .rectTransform.anchoredPosition = new Vector2(0f, 180f);

        for (int i = 0; i < 6; i++)
        {
            var card = CreateUi("Card" + i, panel.transform, typeof(Image), typeof(Button));
            var crt = card.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(190f, 110f);
            int row = i / 3;
            int col = i % 3;
            crt.anchoredPosition = new Vector2((col - 1) * 205f, 40f - row * 125f);
            var img = card.GetComponent<Image>();
            img.color = new Color(0.28f, 0.28f, 0.32f, 1f);
            _cardBgs.Add(img);
            var label = CreateText(card.transform, "L", "", 16, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, 8f);
            _cardLabels.Add(label);
            int idx = i;
            card.GetComponent<Button>().onClick.AddListener(() => Select(idx));
        }

        var ok = CreateUi("Confirm", panel.transform, typeof(Image), typeof(Button));
        var ort = ok.GetComponent<RectTransform>();
        ort.anchorMin = ort.anchorMax = new Vector2(0.5f, 0f);
        ort.pivot = new Vector2(0.5f, 0f);
        ort.anchoredPosition = new Vector2(-100f, 24f);
        ort.sizeDelta = new Vector2(200f, 52f);
        ok.GetComponent<Image>().color = new Color(0.25f, 0.55f, 0.35f, 1f);
        CreateText(ok.transform, "T", "确认带回", 24, TextAnchor.MiddleCenter);
        ok.GetComponent<Button>().onClick.AddListener(OnConfirm);

        var skip = CreateUi("Skip", panel.transform, typeof(Image), typeof(Button));
        var srt = skip.GetComponent<RectTransform>();
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0f);
        srt.pivot = new Vector2(0.5f, 0f);
        srt.anchoredPosition = new Vector2(110f, 24f);
        srt.sizeDelta = new Vector2(200f, 52f);
        skip.GetComponent<Image>().color = new Color(0.4f, 0.35f, 0.35f, 1f);
        CreateText(skip.transform, "T", "放弃遗产", 22, TextAnchor.MiddleCenter);
        skip.GetComponent<Button>().onClick.AddListener(() => Close(null));
    }

    void Select(int idx)
    {
        if (idx < 0 || idx >= _equips.Count) return;
        _selected = idx;
        Refresh();
    }

    void Refresh()
    {
        for (int i = 0; i < _cardBgs.Count; i++)
        {
            bool has = i < _equips.Count && _equips[i] != null;
            _cardBgs[i].gameObject.SetActive(has);
            if (!has) continue;
            var e = _equips[i];
            string name = !string.IsNullOrEmpty(e.equipName) ? e.equipName : (e.templateId ?? "装备");
            _cardLabels[i].text = $"{name}\n{e.rarity} ★{e.star}";
            _cardBgs[i].color = i == _selected
                ? new Color(0.35f, 0.5f, 0.35f, 1f)
                : new Color(0.28f, 0.28f, 0.32f, 1f);
        }
    }

    void OnConfirm()
    {
        EquipInstance pick = null;
        if (_equips.Count > 0)
        {
            if (_selected >= 0 && _selected < _equips.Count)
                pick = _equips[_selected];
            else
                pick = PickBest(_equips);
        }
        Close(pick);
    }

    static EquipInstance PickBest(List<EquipInstance> list)
    {
        EquipInstance best = null;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == null) continue;
            if (best == null || list[i].rarity > best.rarity ||
                (list[i].rarity == best.rarity && list[i].star > best.star))
                best = list[i];
        }
        return best;
    }

    void Close(EquipInstance pick)
    {
        if (_root != null) _root.SetActive(false);
        var cb = _onDone;
        _onDone = null;
        cb?.Invoke(pick);
    }

    static void EnsureEventSystem()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<UnityEngine.EventSystems.EventSystem>();
        es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }

    static GameObject CreateUi(string name, Transform parent, params Type[] comps)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        for (int i = 0; i < comps.Length; i++)
            go.AddComponent(comps[i]);
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
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        Stretch(go.GetComponent<RectTransform>());
        return t;
    }

    static void Stretch(RectTransform rt, float inset = 0f)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    static void AnchorTop(RectTransform rt, float y, float w, float h)
    {
        if (rt == null) return;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(w, h);
    }
}
