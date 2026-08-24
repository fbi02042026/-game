using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 离线收益弹窗（农场金币）。DontDestroyOnLoad；Canvas 规范与全项目一致（Screen Space - Camera）。
/// </summary>
public class OfflineRewardPopup : MonoBehaviour
{
    public static OfflineRewardPopup Instance { get; private set; }

    GameObject _root;
    Text _body;
    Action _onClose;

    public bool IsOpen => _root != null && _root.activeSelf;

    public static void Show(long gold, double minutes, Action onClose = null)
    {
        if (gold <= 0)
        {
            onClose?.Invoke();
            return;
        }
        Ensure().Open(gold, minutes, onClose);
    }

    public static void HideIfOpen()
    {
        if (Instance != null)
            Instance.Close();
    }

    static OfflineRewardPopup Ensure()
    {
        if (Instance != null)
        {
            Instance.EnsureCanvasShell();
            return Instance;
        }
        var go = new GameObject("OfflineRewardPopup");
        DontDestroyOnLoad(go);
        return go.AddComponent<OfflineRewardPopup>();
    }

    void Awake()
    {
        Instance = this;
        EnsureCanvasShell();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Open(long gold, double minutes, Action onClose)
    {
        _onClose = onClose;
        EnsureCanvasShell();
        BuildContentIfNeeded();
        if (_root == null)
        {
            Debug.LogWarning("[OfflineRewardPopup] UI 未就绪，跳过离线收益弹窗");
            _onClose?.Invoke();
            _onClose = null;
            return;
        }

        if (_body != null)
            _body.text = $"离线 {minutes:F0} 分钟\n已到账金币 +{gold}";
        _root.SetActive(true);
        _root.transform.SetAsLastSibling();
        GameFonts.ApplyToHierarchy(transform);
    }

    /// <summary>Canvas 必须先于 GraphicRaycaster；切场景后 Open 时刷新 worldCamera。</summary>
    void EnsureCanvasShell()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();
        UICanvasSetup.Apply(canvas, Camera.main);
        canvas.sortingOrder = 980;
    }

    void BuildContentIfNeeded()
    {
        if (_root != null) return;

        _root = new GameObject("Root", typeof(RectTransform));
        _root.transform.SetParent(transform, false);
        var rootRt = _root.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;

        var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        dim.transform.SetParent(_root.transform, false);
        var drt = dim.GetComponent<RectTransform>();
        drt.anchorMin = Vector2.zero;
        drt.anchorMax = Vector2.one;
        drt.offsetMin = drt.offsetMax = Vector2.zero;
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(_root.transform, false);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(420f, 280f);
        panel.GetComponent<Image>().color = new Color(0.12f, 0.1f, 0.14f, 0.96f);

        var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
        titleGo.transform.SetParent(panel.transform, false);
        var trt = titleGo.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -20f);
        trt.sizeDelta = new Vector2(-40f, 40f);
        var title = titleGo.GetComponent<Text>();
        title.text = "离线收益";
        title.fontSize = 30;
        title.alignment = TextAnchor.MiddleCenter;
        title.color = Color.white;

        var bodyGo = new GameObject("Body", typeof(RectTransform), typeof(Text));
        bodyGo.transform.SetParent(panel.transform, false);
        var brt = bodyGo.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.1f, 0.35f);
        brt.anchorMax = new Vector2(0.9f, 0.75f);
        brt.offsetMin = brt.offsetMax = Vector2.zero;
        _body = bodyGo.GetComponent<Text>();
        _body.fontSize = 24;
        _body.alignment = TextAnchor.MiddleCenter;
        _body.color = Color.white;

        var btn = new GameObject("Ok", typeof(RectTransform), typeof(Image), typeof(Button));
        btn.transform.SetParent(panel.transform, false);
        var btnRt = btn.GetComponent<RectTransform>();
        btnRt.anchorMin = btnRt.anchorMax = new Vector2(0.5f, 0f);
        btnRt.pivot = new Vector2(0.5f, 0f);
        btnRt.anchoredPosition = new Vector2(0f, 28f);
        btnRt.sizeDelta = new Vector2(180f, 48f);
        btn.GetComponent<Image>().color = new Color(0.25f, 0.55f, 0.35f, 1f);
        var btnTxtGo = new GameObject("T", typeof(RectTransform), typeof(Text));
        btnTxtGo.transform.SetParent(btn.transform, false);
        var btnTxtRt = btnTxtGo.GetComponent<RectTransform>();
        btnTxtRt.anchorMin = Vector2.zero;
        btnTxtRt.anchorMax = Vector2.one;
        btnTxtRt.offsetMin = btnTxtRt.offsetMax = Vector2.zero;
        var btnTxt = btnTxtGo.GetComponent<Text>();
        btnTxt.text = "确定";
        btnTxt.fontSize = 24;
        btnTxt.alignment = TextAnchor.MiddleCenter;
        btnTxt.color = Color.white;
        btn.GetComponent<Button>().onClick.AddListener(Close);

        _root.SetActive(false);
        GameFonts.ApplyToHierarchy(transform);
    }

    void Close()
    {
        if (_root != null) _root.SetActive(false);
        var cb = _onClose;
        _onClose = null;
        cb?.Invoke();
    }
}
