using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家低血时屏幕边缘脉冲红闪，不挡点击。
/// 2026-09-28 主人拍板：不再用代码拼四条边，改用美术给的九宫格图
/// （Assets/Resources/UI/Battle/LowHpScreenEdge.png，左右红条渐隐、中间透明，
/// spriteBorder 84/0/84/0）。图加载失败才退回旧的四条边兜底，保证功能不丢。
/// </summary>
public class LowHpScreenEdgeFlash : MonoBehaviour
{
    public static LowHpScreenEdgeFlash Instance { get; private set; }

    const string SpriteResourcePath = "UI/Battle/LowHpScreenEdge";

    CanvasGroup _group;
    RectTransform[] _edges;
    bool _want;
    const float EdgeThickness = 72f;
    const float MaxAlpha = 0.55f;

    public static LowHpScreenEdgeFlash Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("LowHpScreenEdgeFlash");
        DontDestroyOnLoad(go);
        var ui = go.AddComponent<LowHpScreenEdgeFlash>();
        ui.Build();
        return ui;
    }

    void Awake()
    {
        Instance = this;
        if (_group == null) Build();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Build()
    {
        var canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.FullscreenFx);
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        _group = gameObject.GetComponent<CanvasGroup>();
        if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        _group.blocksRaycasts = false;
        _group.interactable = false;
        _group.alpha = 0f;

        var sprite = Resources.Load<Sprite>(SpriteResourcePath);
        if (sprite != null)
            BuildSpriteFlash(sprite);
        else
            BuildLegacyEdges();

        var ray = GetComponent<GraphicRaycaster>();
        if (ray != null) ray.enabled = false;
        gameObject.SetActive(false);
    }

    /// <summary>九宫格整屏 Image：左右红条贴边、中间全透明，随屏拉伸。</summary>
    void BuildSpriteFlash(Sprite sprite)
    {
        var go = new GameObject("EdgeSprite", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.color = Color.white;
        img.raycastTarget = false;
        _edges = new RectTransform[0];
    }

    /// <summary>旧兜底：代码拼四条纯色边（图缺失时才走）。</summary>
    void BuildLegacyEdges()
    {
        _edges = new RectTransform[4];
        _edges[0] = CreateEdge("EdgeTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -EdgeThickness));
        _edges[1] = CreateEdge("EdgeBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, EdgeThickness));
        _edges[2] = CreateEdge("EdgeLeft", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
            new Vector2(EdgeThickness, 0f));
        _edges[3] = CreateEdge("EdgeRight", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
            new Vector2(-EdgeThickness, 0f));
    }

    RectTransform CreateEdge(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = sizeDelta;
        var img = go.GetComponent<Image>();
        img.color = new Color(0.85f, 0.08f, 0.08f, 1f);
        img.raycastTarget = false;
        return rt;
    }

    /// <summary>与人物低血同相位脉冲。</summary>
    public void Tick(bool lowHp)
    {
        _want = lowHp;
        if (!_want)
        {
            if (_group != null) _group.alpha = 0f;
            if (gameObject.activeSelf) gameObject.SetActive(false);
            return;
        }

        if (!gameObject.activeSelf) gameObject.SetActive(true);
        float pulse = 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6.5f));
        if (_group != null)
            _group.alpha = MaxAlpha * pulse;
    }
}
