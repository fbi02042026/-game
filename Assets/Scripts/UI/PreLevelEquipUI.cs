using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战前遗产三选一 UI（代码搭建，样式对齐 StageClearEquipUI）。
/// </summary>
public class PreLevelEquipUI : MonoBehaviour
{
    public static PreLevelEquipUI Instance { get; private set; }

    Action _onDone;
    PreLevelSystem _sys;
    GameObject _root;
    readonly List<Image> _cardBgs = new List<Image>();
    readonly List<Text> _cardLabels = new List<Text>();
    int _selected;
    Text _title;
    Text _hint;

    public static void Show(PreLevelSystem sys, Action onDone)
    {
        if (sys == null)
        {
            onDone?.Invoke();
            return;
        }
        Ensure().Open(sys, onDone);
    }

    static PreLevelEquipUI Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("PreLevelEquipUI");
        DontDestroyOnLoad(go);
        return go.AddComponent<PreLevelEquipUI>();
    }

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Open(PreLevelSystem sys, Action onDone)
    {
        _sys = sys;
        _onDone = onDone;
        _selected = 0;
        if (sys.currentOptions == null || sys.currentOptions.Count == 0)
            sys.StartPreLevelSelection();

        BuildIfNeeded();
        RefreshCards();
        if (_root != null)
        {
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
        }
        EnsureEventSystem();
        GameFonts.ApplyToHierarchy(transform);
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
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.BattleHud);

        _root = new GameObject("Root", typeof(RectTransform));
        _root.transform.SetParent(transform, false);
        Stretch(_root.GetComponent<RectTransform>());

        var dim = CreateUi("Dim", _root.transform, typeof(Image));
        Stretch(dim.GetComponent<RectTransform>());
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

        var panel = CreateUi("Panel", _root.transform, typeof(Image));
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(640f, 520f);
        panel.GetComponent<Image>().color = new Color(0.12f, 0.1f, 0.14f, 0.96f);

        _title = CreateText(panel.transform, "Title", "选择开局装备", 32, TextAnchor.MiddleCenter);
        AnchorTop(_title.rectTransform, -28f, 560f, 40f);

        _hint = CreateText(panel.transform, "Hint", "从遗产或基础装备中选一件", 20, TextAnchor.MiddleCenter);
        AnchorTop(_hint.rectTransform, -68f, 560f, 28f);

        float cardW = 170f;
        float gap = 18f;
        float startX = -((cardW + gap) * 1f);
        for (int i = 0; i < 3; i++)
        {
            var card = CreateUi("Card" + i, panel.transform, typeof(Image), typeof(Button));
            var crt = card.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(cardW, 220f);
            crt.anchoredPosition = new Vector2(startX + i * (cardW + gap), 20f);
            var img = card.GetComponent<Image>();
            img.color = new Color(0.28f, 0.28f, 0.32f, 1f);
            _cardBgs.Add(img);

            var label = CreateText(card.transform, "Label", "", 18, TextAnchor.UpperLeft);
            var lrt = label.rectTransform;
            Stretch(lrt, 10f);
            _cardLabels.Add(label);

            int idx = i;
            card.GetComponent<Button>().onClick.AddListener(() => Select(idx));
        }

        var confirm = CreateUi("Confirm", panel.transform, typeof(Image), typeof(Button));
        var confRt = confirm.GetComponent<RectTransform>();
        confRt.anchorMin = confRt.anchorMax = new Vector2(0.5f, 0f);
        confRt.pivot = new Vector2(0.5f, 0f);
        confRt.anchoredPosition = new Vector2(-90f, 28f);
        confRt.sizeDelta = new Vector2(200f, 52f);
        confirm.GetComponent<Image>().color = new Color(0.25f, 0.55f, 0.35f, 1f);
        CreateText(confirm.transform, "T", "确认装备", 24, TextAnchor.MiddleCenter);
        confirm.GetComponent<Button>().onClick.AddListener(OnConfirm);

        var refresh = CreateUi("Refresh", panel.transform, typeof(Image), typeof(Button));
        var refRt = refresh.GetComponent<RectTransform>();
        refRt.anchorMin = refRt.anchorMax = new Vector2(0.5f, 0f);
        refRt.pivot = new Vector2(0.5f, 0f);
        refRt.anchoredPosition = new Vector2(110f, 28f);
        refRt.sizeDelta = new Vector2(200f, 52f);
        refresh.GetComponent<Image>().color = new Color(0.35f, 0.4f, 0.55f, 1f);
        CreateText(refresh.transform, "T", "刷新(广告)", 22, TextAnchor.MiddleCenter);
        refresh.GetComponent<Button>().onClick.AddListener(OnRefresh);
    }

    void Select(int idx)
    {
        _selected = idx;
        _sys?.SelectOption(idx);
        RefreshCards();
    }

    void RefreshCards()
    {
        var opts = _sys != null ? _sys.currentOptions : null;
        for (int i = 0; i < 3; i++)
        {
            if (i >= _cardBgs.Count) break;
            bool has = opts != null && i < opts.Count && opts[i] != null;
            _cardBgs[i].gameObject.SetActive(has);
            if (!has) continue;
            var d = opts[i];
            _cardLabels[i].text = $"{DisplayName(d)}\n品质{(Rarity)Mathf.Clamp(d.rarity, 0, 4)}\n★{d.star}";
            _cardBgs[i].color = i == _selected
                ? new Color(0.35f, 0.5f, 0.35f, 1f)
                : new Color(0.28f, 0.28f, 0.32f, 1f);
        }
        if (_hint != null && _sys != null)
            _hint.text = _sys.hasRefreshedThisRun ? "本局已刷新过" : "可看广告刷新一次";
    }

    static string DisplayName(EquipmentData d)
    {
        if (d == null) return "？";
        if (!string.IsNullOrEmpty(d.equipId))
        {
            var tpl = ConfigManager.Instance != null ? ConfigManager.Instance.GetEquipTemplate(d.equipId) : null;
            if (tpl != null && !string.IsNullOrEmpty(tpl.equipName))
                return tpl.equipName;
            return EquipNameGen.TempName(d.equipId);
        }
        return "装备";
    }

    void OnConfirm()
    {
        if (_sys == null)
        {
            Close();
            return;
        }
        if (_sys.selectedIndex < 0)
            _sys.SelectOption(Mathf.Clamp(_selected, 0, 2));
        _sys.ConfirmSelection();
        Close();
    }

    void OnRefresh()
    {
        if (_sys == null) return;
        if (_sys.RefreshOptions())
        {
            _selected = 0;
            RefreshCards();
        }
        else
            UIManager.Instance?.ShowToast("本局无法再刷新");
    }

    void Close()
    {
        if (_root != null) _root.SetActive(false);
        var cb = _onDone;
        _onDone = null;
        cb?.Invoke();
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
