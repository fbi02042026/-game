using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 启动健康游戏忠告：登录界面之前全屏展示标准忠告文案。
/// 优先 Resources 预制体（字体 Inspector 预绑 fusion-pixel），缺失时运行时回退构建。
/// </summary>
public class HealthNoticeUI : MonoBehaviour
{
    public const float DisplaySeconds = 3f;
    const string PrefabPath = ContentPaths.Prefab.HealthNotice;
    const string FontResourcesPath = "Fonts/fusion-pixel";

    public static HealthNoticeUI Instance { get; private set; }

    [SerializeField] Text titleText;
    [SerializeField] Text bodyText;

    bool _finished;
    Action _onFinished;

    public bool IsVisible => gameObject.activeInHierarchy && !_finished;
    public string TitleText => titleText != null ? titleText.text : "";

    public static void Present(Action onFinished)
    {
        var existing = FindObjectOfType<HealthNoticeUI>();
        if (existing != null)
        {
            existing._onFinished = onFinished;
            return;
        }

        GameObject go;
        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab != null)
        {
            go = Instantiate(prefab);
        }
        else
        {
            Debug.LogWarning("[HealthNoticeUI] 预制体 Resources/" + PrefabPath +
                             " 未加载，使用运行时回退（请在编辑器运行 Tools/UI/生成健康忠告预制体）");
            go = CreateRuntimeFallback();
        }

        go.name = "HealthNoticeUI";
        var ui = go.GetComponent<HealthNoticeUI>();
        if (ui == null)
            ui = go.AddComponent<HealthNoticeUI>();
        var canvas = go.GetComponent<Canvas>();
        if (canvas != null)
            UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.StoryDialogue);
        ui._onFinished = onFinished;
        ui.BindReferences();
        ui.StartCoroutine(ui.PresentRoutine());
    }

    static GameObject CreateRuntimeFallback()
    {
        var font = Resources.Load<Font>(FontResourcesPath);
        if (font == null)
            font = GameFonts.GetChinese();

        var root = new GameObject("HealthNoticeUI", typeof(RectTransform));
        var canvas = root.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.StoryDialogue);

        var ui = root.AddComponent<HealthNoticeUI>();

        var bgGo = new GameObject("Bg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bgGo.transform.SetParent(root.transform, false);
        var bg = bgGo.GetComponent<Image>();
        bg.color = Color.black;
        bg.raycastTarget = false;
        Stretch(bgGo.GetComponent<RectTransform>());

        ui.titleText = CreateText(root.transform, "Title", "健康游戏忠告", 42, TextAnchor.MiddleCenter, font);
        ui.titleText.color = new Color(1f, 0.92f, 0.55f, 1f);
        var titleRt = ui.titleText.rectTransform;
        titleRt.anchorMin = new Vector2(0.08f, 0.62f);
        titleRt.anchorMax = new Vector2(0.92f, 0.76f);
        titleRt.offsetMin = titleRt.offsetMax = Vector2.zero;

        ui.bodyText = CreateText(root.transform, "Body",
            "抵制不良游戏，拒绝盗版游戏。\n" +
            "注意自我保护，谨防受骗上当。\n" +
            "适度游戏益脑，沉迷游戏伤身。\n" +
            "合理安排时间，享受健康生活。",
            26, TextAnchor.MiddleCenter, font);
        ui.bodyText.lineSpacing = 1.45f;
        ui.bodyText.color = new Color(0.92f, 0.9f, 0.86f, 1f);
        var bodyRt = ui.bodyText.rectTransform;
        bodyRt.anchorMin = new Vector2(0.08f, 0.28f);
        bodyRt.anchorMax = new Vector2(0.92f, 0.58f);
        bodyRt.offsetMin = bodyRt.offsetMax = Vector2.zero;

        return root;
    }

    static Text CreateText(Transform parent, string name, string content, int size, TextAnchor align, Font font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.font = font;
        t.alignment = align;
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

    void BindReferences()
    {
        if (titleText == null)
            titleText = transform.Find("Title")?.GetComponent<Text>();
        if (bodyText == null)
            bodyText = transform.Find("Body")?.GetComponent<Text>();
    }

    IEnumerator PresentRoutine()
    {
        Canvas.ForceUpdateCanvases();
        yield return null;
        BootManager.ReleaseBootVeil();
        yield return HoldRoutine();
    }

    void Awake()
    {
        Instance = this;
        BindReferences();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
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
}
