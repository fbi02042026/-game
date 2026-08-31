using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗撤离确认弹窗：确认撤离 / 继续战斗。
/// 预制体：Resources/Prefabs/Battle/EvacuateConfirmPopup
/// </summary>
public class EvacuateConfirmPopupUI : MonoBehaviour
{
    public const string PrefabPath = "Prefabs/Battle/EvacuateConfirmPopup";

    public static EvacuateConfirmPopupUI Instance { get; private set; }

    public GameObject root;
    public Text titleText;
    public Text bodyText;
    public Button evacuateButton;
    public Button continueButton;
    public Button dimButton;

    Action _onEvacuate;
    Action _onContinue;
    float _prevTimeScale = 1f;

    public bool IsOpen => root != null && root.activeSelf;

    public static void Show(Action onEvacuate, Action onContinue = null)
    {
        Ensure().Open(onEvacuate, onContinue);
    }

    public static EvacuateConfirmPopupUI Ensure()
    {
        if (Instance != null) return Instance;

        var prefab = Resources.Load<GameObject>(PrefabPath);
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab);
            go.name = "EvacuateConfirmPopup";
        }
        else
        {
            Debug.LogWarning($"[EvacuateConfirm] 未找到 {PrefabPath}，临时代码搭壳");
            go = new GameObject("EvacuateConfirmPopup", typeof(RectTransform));
            BuildHierarchy(go);
        }
        DontDestroyOnLoad(go);
        return go.GetComponent<EvacuateConfirmPopupUI>() ?? go.AddComponent<EvacuateConfirmPopupUI>();
    }

    void Awake()
    {
        Instance = this;
        BindRefs();
        Wire();
        if (root != null) root.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void BindRefs()
    {
        if (root == null)
            root = transform.Find("Root")?.gameObject ?? gameObject;
        if (titleText == null)
            titleText = FindDeep(root.transform, "Title")?.GetComponent<Text>();
        if (bodyText == null)
            bodyText = FindDeep(root.transform, "Body")?.GetComponent<Text>();
        if (evacuateButton == null)
            evacuateButton = FindDeep(root.transform, "EvacuateButton")?.GetComponent<Button>();
        if (continueButton == null)
            continueButton = FindDeep(root.transform, "ContinueButton")?.GetComponent<Button>();
        if (dimButton == null)
            dimButton = FindDeep(root.transform, "Dim")?.GetComponent<Button>();
    }

    void Wire()
    {
        if (evacuateButton != null)
        {
            evacuateButton.onClick.RemoveAllListeners();
            evacuateButton.onClick.AddListener(OnClickEvacuate);
        }
        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(OnClickContinue);
        }
        if (dimButton != null)
        {
            dimButton.onClick.RemoveAllListeners();
            dimButton.onClick.AddListener(OnClickContinue);
        }
    }

    void Open(Action onEvacuate, Action onContinue)
    {
        BindRefs();
        Wire();
        _onEvacuate = onEvacuate;
        _onContinue = onContinue;
        _prevTimeScale = Time.timeScale;
        if (_prevTimeScale <= 0.01f) _prevTimeScale = 1f;
        Time.timeScale = 0f;

        if (titleText != null) titleText.text = "确认撤离？";
        if (bodyText != null)
            bodyText.text = "撤离将结束本次裂缝探索。\n本局金币会保留；若有装备可再选一件作为遗产带回。";

        if (root != null)
        {
            root.SetActive(true);
            transform.SetAsLastSibling();
        }
        GameFonts.ApplyToHierarchy(transform);
    }

    void OnClickEvacuate()
    {
        CloseInternal();
        var cb = _onEvacuate;
        _onEvacuate = null;
        _onContinue = null;
        cb?.Invoke();
    }

    void OnClickContinue()
    {
        CloseInternal();
        var cb = _onContinue;
        _onEvacuate = null;
        _onContinue = null;
        cb?.Invoke();
    }

    void CloseInternal()
    {
        if (root != null) root.SetActive(false);
        Time.timeScale = _prevTimeScale > 0.01f ? _prevTimeScale : 1f;
    }

    public static void BuildHierarchy(GameObject host)
    {
        var canvas = host.GetComponent<Canvas>() ?? host.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.BattleEvacuate);
        if (host.GetComponent<GraphicRaycaster>() == null)
            host.AddComponent<GraphicRaycaster>();
        if (host.GetComponent<EvacuateConfirmPopupUI>() == null)
            host.AddComponent<EvacuateConfirmPopupUI>();

        var root = Mk(host.transform, "Root");
        Stretch(root);

        var dim = Mk(root, "Dim");
        Stretch(dim);
        dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        dim.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;

        var panel = Mk(root, "Panel");
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(520f, 360f);
        panel.gameObject.AddComponent<Image>().color = new Color(0.14f, 0.11f, 0.16f, 0.98f);

        Label(panel, "Title", "确认撤离？", 30, new Vector2(0f, -36f), new Vector2(460f, 44f));
        Label(panel, "Body", "撤离将结束本次裂缝探索。", 22, new Vector2(0f, -130f), new Vector2(460f, 120f), TextAnchor.UpperCenter);

        var evac = Mk(panel, "EvacuateButton");
        evac.anchorMin = evac.anchorMax = new Vector2(0.28f, 0f);
        evac.pivot = new Vector2(0.5f, 0f);
        evac.anchoredPosition = new Vector2(0f, 28f);
        evac.sizeDelta = new Vector2(180f, 52f);
        evac.gameObject.AddComponent<Image>().color = new Color(0.55f, 0.28f, 0.22f, 1f);
        evac.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
        Label(evac, "Label", "撤离", 24, Vector2.zero, new Vector2(180f, 52f), TextAnchor.MiddleCenter, true);

        var cont = Mk(panel, "ContinueButton");
        cont.anchorMin = cont.anchorMax = new Vector2(0.72f, 0f);
        cont.pivot = new Vector2(0.5f, 0f);
        cont.anchoredPosition = new Vector2(0f, 28f);
        cont.sizeDelta = new Vector2(180f, 52f);
        cont.gameObject.AddComponent<Image>().color = new Color(0.28f, 0.42f, 0.3f, 1f);
        cont.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
        Label(cont, "Label", "继续战斗", 24, Vector2.zero, new Vector2(180f, 52f), TextAnchor.MiddleCenter, true);

        GameFonts.ApplyToHierarchy(host.transform);
    }

    static RectTransform Mk(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static void Label(Transform parent, string name, string text, int size, Vector2 pos, Vector2 sizeDelta,
        TextAnchor align = TextAnchor.MiddleCenter, bool stretch = false)
    {
        var rt = Mk(parent, name);
        if (stretch)
        {
            Stretch(rt);
        }
        else
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
        }
        var t = rt.gameObject.AddComponent<Text>();
        t.font = GameFonts.GetChinese();
        t.fontSize = size;
        t.color = Color.white;
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.text = text;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
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
}
