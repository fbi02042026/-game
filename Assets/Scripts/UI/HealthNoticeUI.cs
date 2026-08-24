using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 启动健康游戏忠告：登录界面之前全屏展示标准忠告文案。
/// </summary>
public class HealthNoticeUI : MonoBehaviour
{
    public const float DisplaySeconds = 3f;

    public static HealthNoticeUI Instance { get; private set; }

    Text _title;
    bool _finished;
    Action _onFinished;

    public bool IsVisible => gameObject.activeInHierarchy && !_finished;
    public string TitleText => _title != null ? _title.text : "";

    public static void Present(Action onFinished)
    {
        var existing = FindObjectOfType<HealthNoticeUI>();
        if (existing != null)
        {
            existing._onFinished = onFinished;
            return;
        }

        var go = new GameObject("HealthNoticeUI", typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        go.AddComponent<GraphicRaycaster>();
        UICanvasSetup.Apply(canvas, Camera.main);
        canvas.overrideSorting = true;
        canvas.sortingOrder = 90;

        var ui = go.AddComponent<HealthNoticeUI>();
        ui._onFinished = onFinished;
        ui.Build();
        GameFonts.ApplyToHierarchy(go.transform);
        ui.StartCoroutine(ui.HoldRoutine());
    }

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Build()
    {
        var rootRt = transform as RectTransform;
        Stretch(rootRt);

        var bg = CreateUi("Bg", transform, typeof(Image));
        Stretch(bg.GetComponent<RectTransform>());
        bg.GetComponent<Image>().color = Color.black;
        bg.GetComponent<Image>().raycastTarget = false;

        _title = CreateText(transform, "Title", "健康游戏忠告", 42, TextAnchor.MiddleCenter);
        _title.color = new Color(1f, 0.92f, 0.55f, 1f);
        var titleRt = _title.rectTransform;
        titleRt.anchorMin = new Vector2(0.08f, 0.62f);
        titleRt.anchorMax = new Vector2(0.92f, 0.76f);
        titleRt.offsetMin = titleRt.offsetMax = Vector2.zero;

        var body = CreateText(transform, "Body",
            "抵制不良游戏，拒绝盗版游戏。\n" +
            "注意自我保护，谨防受骗上当。\n" +
            "适度游戏益脑，沉迷游戏伤身。\n" +
            "合理安排时间，享受健康生活。",
            26, TextAnchor.MiddleCenter);
        body.lineSpacing = 1.45f;
        body.color = new Color(0.92f, 0.9f, 0.86f, 1f);
        var bodyRt = body.rectTransform;
        bodyRt.anchorMin = new Vector2(0.08f, 0.28f);
        bodyRt.anchorMax = new Vector2(0.92f, 0.58f);
        bodyRt.offsetMin = bodyRt.offsetMax = Vector2.zero;
    }

    IEnumerator HoldRoutine()
    {
        float t = 0f;
        while (t < DisplaySeconds && !_finished)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        Finish();
    }

    void Finish()
    {
        if (_finished) return;
        _finished = true;
        var cb = _onFinished;
        _onFinished = null;
        cb?.Invoke();
        Destroy(gameObject);
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
        t.raycastTarget = false;
        Stretch(go.GetComponent<RectTransform>());
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
