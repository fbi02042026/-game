using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 疾风架势「手动窗口」倒计时条（2026-10-27 主人拍板机制）：
/// 战斗 HUD 上方居中一块「疾风架势！点击屏幕多打」+ 倒计时进度条 + 剩余秒数。
/// 运行时挂到 BattleUI 下（照 BattleSideHud 先例），不依赖预制体；
/// 自身 raycastTarget 全关，不拦截战场点击（点击判定在 BattleManager.IsBattlefieldTap）。
/// </summary>
public class GaleWindowHud : MonoBehaviour
{
    public static GaleWindowHud Instance { get; private set; }

    /// <summary>进度条轨道宽（填充条 = 轨道 − 左右各 2px 内边）。</summary>
    const float TrackWidth = 300f;

    RectTransform _fill;
    Text _countText;

    public static GaleWindowHud EnsureOn(Transform battleUiRoot)
    {
        if (battleUiRoot == null) return null;
        var existing = Instance != null ? Instance : battleUiRoot.GetComponentInChildren<GaleWindowHud>(true);
        if (existing != null)
        {
            Instance = existing;
            existing.Build();
            return existing;
        }

        var go = new GameObject("GaleWindowHud", typeof(RectTransform));
        go.transform.SetParent(battleUiRoot, false);
        go.transform.SetAsLastSibling();
        var hud = go.AddComponent<GaleWindowHud>();
        hud.Build();
        return hud;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Build()
    {
        if (_fill != null) return; // 幂等：已建不再重建（EnsureOn 重复调用安全）

        var rootRt = GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.5f, 1f);
        rootRt.anchorMax = new Vector2(0.5f, 1f);
        rootRt.pivot = new Vector2(0.5f, 1f);
        // 血条（贴顶锚 y≈-163）下方、战场怪带上缘，居中；避开右上 BattleSideHud（右缩 48 宽 210）
        rootRt.anchoredPosition = new Vector2(0f, -290f);
        rootRt.sizeDelta = new Vector2(420f, 96f);

        // 背板（半透明，不挡视线；raycast 关）
        var bgGo = new GameObject("Bg", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(rootRt, false);
        var bgRt = (RectTransform)bgGo.transform;
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        var bgImg = bgGo.GetComponent<Image>();
        bgImg.color = new Color(0.08f, 0.1f, 0.16f, 0.55f);
        bgImg.raycastTarget = false;

        // 标题行
        var title = CreateText(bgGo.transform, "GaleTitle", "疾风架势！点击屏幕多打", 26,
            new Vector2(0f, -32f), new Vector2(410f, 40f));
        title.color = new Color(0.55f, 0.9f, 1f, 1f);
        title.fontStyle = FontStyle.Bold;

        // 进度条：底 + 填充（sizeDelta 方案，与 BattleBossHpBar 同口径，不依赖 Filled sprite）
        var trackGo = new GameObject("Track", typeof(RectTransform), typeof(Image));
        trackGo.transform.SetParent(bgGo.transform, false);
        var trackRt = (RectTransform)trackGo.transform;
        trackRt.anchorMin = new Vector2(0.5f, 0.5f);
        trackRt.anchorMax = new Vector2(0.5f, 0.5f);
        trackRt.pivot = new Vector2(0.5f, 0.5f);
        trackRt.anchoredPosition = new Vector2(0f, -70f);
        trackRt.sizeDelta = new Vector2(TrackWidth, 16f);
        var trackImg = trackGo.GetComponent<Image>();
        trackImg.color = new Color(0f, 0f, 0f, 0.6f);
        trackImg.raycastTarget = false;

        var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(trackRt, false);
        _fill = (RectTransform)fillGo.transform;
        _fill.anchorMin = new Vector2(0f, 0.5f);
        _fill.anchorMax = new Vector2(0f, 0.5f);
        _fill.pivot = new Vector2(0f, 0.5f);
        _fill.anchoredPosition = new Vector2(2f, 0f);
        _fill.sizeDelta = new Vector2(TrackWidth - 4f, 12f);
        var fillImg = fillGo.GetComponent<Image>();
        fillImg.color = new Color(0.35f, 0.95f, 0.65f, 0.95f);
        fillImg.raycastTarget = false;

        // 剩余秒数（条右端外；420/2=210 半宽内）
        _countText = CreateText(bgGo.transform, "GaleCount", "4.0", 20,
            new Vector2(TrackWidth * 0.5f + 22f, -70f), new Vector2(50f, 24f));
        _countText.color = new Color(0.9f, 0.95f, 1f, 0.95f);

        EnsureSortCanvas();
        // 运行时建树默认隐藏（与「Build() 末尾 SetActive(false)」同一口径），Show 时才亮
        gameObject.SetActive(false);
    }

    void EnsureSortCanvas()
    {
        var c = GetComponent<Canvas>();
        if (c == null) c = gameObject.AddComponent<Canvas>();
        c.overrideSorting = true;
        c.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
        // 比 BattleSideHud(105) 高一档，仍远低于弹窗(920)
        c.sortingOrder = 106;
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
    }

    public void Show()
    {
        if (_fill == null) Build();
        gameObject.SetActive(true);
        UpdateFill(1f, GameConfig.GALE_WINDOW_DURATION);
    }

    public void Hide()
    {
        if (this == null) return;
        gameObject.SetActive(false);
    }

    /// <summary>每帧由 BattleManager 推：remainRatio 0~1，remainSec 剩余秒。</summary>
    public void UpdateFill(float remainRatio, float remainSec)
    {
        if (_fill == null) return;
        _fill.sizeDelta = new Vector2((TrackWidth - 4f) * Mathf.Clamp01(remainRatio), 12f);
        if (_countText != null) _countText.text = remainSec.ToString("0.0");
    }

    static Text CreateText(Transform parent, string name, string content, int size,
        Vector2 pos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;
        var t = go.AddComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.font = GameFonts.GetChinese();
        return t;
    }
}

/// <summary>
/// BattleUI 薄封装：疾风窗口条的显示 / 隐藏 / 刷新。
/// 窗口条节点由 GaleWindowHud 运行时建（不碰预制体），挂在本 UI 的 transform 下。
/// </summary>
public partial class BattleUI
{
    public void ShowGaleWindow()
    {
        var hud = GaleWindowHud.EnsureOn(transform);
        if (hud != null) hud.Show();
    }

    public void HideGaleWindow()
    {
        if (GaleWindowHud.Instance != null) GaleWindowHud.Instance.Hide();
    }

    public void UpdateGaleWindow(float remainRatio, float remainSec)
    {
        if (GaleWindowHud.Instance != null) GaleWindowHud.Instance.UpdateFill(remainRatio, remainSec);
    }
}
