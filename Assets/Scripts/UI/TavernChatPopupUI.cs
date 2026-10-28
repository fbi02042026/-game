using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 酒馆闲话弹窗（2026-10-24 主人拍板「开工」）：TavernLore 三入口 + 老队员记忆的聚合入口。
/// 由 TavernUI 的「信任交流」卡点开（该卡原 2026-10-15「未开放」口径由本批接线取代；
/// 信赖度展示保留在弹窗页脚，信任系统本体仍未开放）。
///
/// 纯代码搭建、DontDestroyOnLoad、UICanvasSetup.ApplyPopup 统一规范（TownPopup 层）。
/// 只负责「点谁 → 显示谁的一句话」：文本全部来自 TavernLore，本类零剧情文案。
/// </summary>
public class TavernChatPopupUI : MonoBehaviour
{
    static TavernChatPopupUI _inst;
    Canvas _canvas;
    Text _favorLabel;

    public static void Toggle()
    {
        // 剧情正在播时不弹（剧情钩子清单 §三约束：所有新入口防重入）
        if (StoryDirector.Instance != null && StoryDirector.Instance.IsPlaying) return;
        var ui = Ensure();
        if (ui == null) return;
        ui.Open();
    }

    public static void Hide()
    {
        if (_inst != null) _inst.Close();
    }

    static TavernChatPopupUI Ensure()
    {
        if (_inst != null) return _inst;

        var go = new GameObject("TavernChatPopup", typeof(RectTransform), typeof(Canvas));
        DontDestroyOnLoad(go);
        var ui = go.AddComponent<TavernChatPopupUI>();
        ui.Build();
        // 统一规范：Camera 模式 + 720×1280 + TownPopup 层（与 MercenaryRecruitPopup 等城镇弹窗同层口径）
        UICanvasSetup.ApplyPopup(ui._canvas, GameConfig.UiSort.TownPopup);
        // 建完先藏：只有点「酒馆闲话」卡才弹，不许进酒馆就自动糊脸
        go.SetActive(false);
        return ui;
    }

    void Awake()
    {
        if (_inst != null && _inst != this)
        {
            Destroy(gameObject);
            return;
        }
        _inst = this;
    }

    void OnDestroy()
    {
        if (_inst == this) _inst = null;
    }

    // ---------- 搭建（一次性） ----------

    void Build()
    {
        _canvas = GetComponent<Canvas>();
        var root = (RectTransform)transform;
        UICanvasSetup.EnsureRootStretch(root);

        // 遮罩：点击任意处关闭（与 DraftOddsPopupUI 同口径，非模态）
        var dimGo = NewRect(transform, "Dim");
        var dim = dimGo.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.5f);
        Stretch(dimGo.GetComponent<RectTransform>());
        var dimBtn = dimGo.AddComponent<Button>();
        dimBtn.targetGraphic = dim;
        dimBtn.onClick.AddListener(Close);

        // 面板
        var panelGo = NewRect(transform, "Panel");
        var panelImg = panelGo.AddComponent<Image>();
        panelImg.color = new Color(0.12f, 0.09f, 0.06f, 0.96f);
        SetAnchored(panelGo.GetComponent<RectTransform>(),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(640f, 680f));

        var title = NewText(panelGo.transform, "Title", "酒 馆 闲 话", 32, new Color(1f, 0.92f, 0.75f));
        SetAnchored(title.rectTransform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -52f), new Vector2(560f, 56f));

        // 四个入口：空座位 / 独眼 / 老板娘 / 老队员的话
        //（中心锚定：面板 680 高、上下各 340；四张卡 108 高从上往下排，页脚贴底）
        MakeOption(panelGo.transform, "Seat", "靠窗的空座位", "去看看她留过什么",
            new Vector2(0f, 196f), OnSeatClicked);
        MakeOption(panelGo.transform, "Drink", "请独眼喝一杯", "他知道的比说出来的多",
            new Vector2(0f, 74f), OnDrinkClicked);
        MakeOption(panelGo.transform, "Innkeeper", "找老板娘搭话", "她记的账比公会的档案都全",
            new Vector2(0f, -48f), OnInnkeeperClicked);
        MakeOption(panelGo.transform, "Squad", "老队员的话", "出发前夜，他们说过什么",
            new Vector2(0f, -170f), OnSquadClicked);

        // 页脚：信赖度展示（2026-10-18 口径保留，信任系统本体仍未开放）
        var favor = NewText(panelGo.transform, "FavorLabel", "", 16, new Color(0.85f, 0.8f, 0.7f, 0.9f));
        SetAnchored(favor.rectTransform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, -298f), new Vector2(560f, 40f));
        _favorLabel = favor;

        GameFonts.ApplyToHierarchy(transform);
    }

    void MakeOption(Transform parent, string name, string title, string desc, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        var go = NewRect(parent, name);
        SetAnchored(go.GetComponent<RectTransform>(),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            pos, new Vector2(560f, 108f));

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.15f, 0.1f, 0.9f);

        var t = NewText(go.transform, "Label", title, 26, new Color(1f, 0.92f, 0.75f));
        var trt = t.rectTransform;
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.offsetMin = new Vector2(24f, -56f);
        trt.offsetMax = new Vector2(-16f, -10f);
        t.alignment = TextAnchor.MiddleLeft;

        var d = NewText(go.transform, "Desc", desc, 16, new Color(0.85f, 0.8f, 0.7f, 0.85f));
        var drt = d.rectTransform;
        drt.anchorMin = new Vector2(0f, 0f);
        drt.anchorMax = new Vector2(1f, 0f);
        drt.offsetMin = new Vector2(24f, 10f);
        drt.offsetMax = new Vector2(-16f, 44f);
        d.alignment = TextAnchor.MiddleLeft;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = bg;
        btn.onClick.AddListener(onClick);
    }

    // ---------- 四个入口的行为（文本全部出自 TavernLore，本类不写剧情） ----------

    void OnSeatClicked()
    {
        var stage = TavernLore.ResolveSeatStage();
        string prop = TavernLore.SeatProp(stage);
        string text = TavernLore.SeatText(stage);
        string show = string.IsNullOrEmpty(prop) ? text : prop + "\n" + text;
        // 计数在显示之后走：第 5 次查看发 C26（TavernLore 内部判定）
        TavernLore.NoteSeatViewed();
        UIManager.Instance?.ShowToast(show, true);
    }

    void OnDrinkClicked()
    {
        bool got = TavernLore.TreatInformant();
        // 已拿过当期线索 → 独眼改口（性格口径：别浪费他的酒）
        string line = got
            ? TavernLore.InformantLine()
            : "「该说的都说了，酒也喝了。\n下次带点新消息来，再谈下一杯。」";
        UIManager.Instance?.ShowToast(line, true);
    }

    void OnInnkeeperClicked()
    {
        UIManager.Instance?.ShowToast(TavernLore.InnkeeperLine(), true);
    }

    void OnSquadClicked()
    {
        string line = TavernLore.SquadQuote();
        if (!string.IsNullOrEmpty(line))
            UIManager.Instance?.ShowToast(line, true);
    }

    // ---------- 开关 ----------

    void Open()
    {
        if (_favorLabel != null)
            _favorLabel.text = $"信赖：{TavernEggState.FavorStageName}（{TavernEggState.Favor}）　信任交流功能即将开放";
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        // DDOL 弹窗每次打开重绑相机 + 重套缩放（DraftOddsPopupUI 2026-10-24 同款补丁口径）
        if (_canvas != null)
            UICanvasSetup.RefreshPopup(_canvas, GameConfig.UiSort.TownPopup);
    }

    void Close()
    {
        gameObject.SetActive(false);
    }

    // ---------- 小工具（与 TavernUI 同款写法，独立自包含） ----------

    static GameObject NewRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static Text NewText(Transform parent, string name, string content, int size, Color color)
    {
        var go = NewRect(parent, name);
        var t = go.AddComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.font = GameFonts.GetChinese();
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void SetAnchored(RectTransform rt, Vector2 amin, Vector2 amax, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = amin;
        rt.anchorMax = amax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}
