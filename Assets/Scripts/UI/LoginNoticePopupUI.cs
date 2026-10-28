using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 登录页公告弹窗（2026-10-20）。
/// 底板 = Resources/Art/UI/公告/公告板.png（图上自带「公告」标题牌与右上角 X 美术，正文区留空）。
/// 入口：登录页右侧「公告」按钮；首次登录按期号自动弹一次（已读记录走 PlayerPrefs，不进存档）。
/// </summary>
public class LoginNoticePopupUI : MonoBehaviour
{
    /// <summary>公告板图。走 Resources 路径加载：该资源 .meta 是 56 字符 guid，禁止拖进预制体。</summary>
    public const string BoardResourcePath = "UI/Art/公告/公告板";

    /// <summary>
    /// 本期公告期号。改这里 = 全员重新弹一次（2026-10-20 主人测试时点过 X 关闭已写已读，
    /// 编辑器里再进不弹属正常；想再看一次就往下拨一位，如 "2026-10-20c"）。
    /// 已读记录在 PlayerPrefs：老玩家点「清除数据」走 SaveSystem.DeleteLocalSaveAndReset（不碰 PlayerPrefs），
    /// 所以清完档不会再弹；SaveSystem 版本自动清档的 DeleteAll 会把它一起清掉（= 新版正好重弹一次，与清档同节奏）。
    /// </summary>
    public const string CurrentNoticeId = "2026-10-20b";

    const string SeenPrefKey = "login_notice_seen_id";

    const string NoticeBody =
        "这版优化改了很多东西。\n" +
        "\n" +
        "老玩家的存档已自动清空，\n" +
        "会从新档重新开始。\n" +
        "\n" +
        "新玩家当没看见这段，直接开冲。\n" +
        "\n" +
        "给大家添麻烦了，多谢理解。";

    static LoginNoticePopupUI _instance;

    Canvas _canvas;
    Text _body;

    public static LoginNoticePopupUI Instance => _instance;
    public static bool IsOpen => _instance != null && _instance.isActiveAndEnabled;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        _canvas = GetComponent<Canvas>();
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    /// <summary>打开公告（按钮入口与首次自动弹共用）。已有实例就复用，不重复 Instantiate。</summary>
    public static LoginNoticePopupUI Open()
    {
        if (_instance == null)
            Build();
        if (_instance == null)
            return null;    // Build 内部图缺失时已 LogError，此处不兜底
        _instance.gameObject.SetActive(true);
        UICanvasSetup.RefreshPopup(_instance._canvas, GameConfig.UiSort.TownPopup);
        return _instance;
    }

    /// <summary>关闭：右上角 X 专用，同时把本期标记为已读。</summary>
    public void Close()
    {
        MarkSeen();
        gameObject.SetActive(false);
    }

    /// <summary>首次自动弹：本期没看过才弹。</summary>
    public static void TryAutoShow()
    {
        if (SeenNoticeId() == CurrentNoticeId) return;
        Open();
    }

    public static string SeenNoticeId() => PlayerPrefs.GetString(SeenPrefKey, "");

    public static void MarkSeen()
    {
        PlayerPrefs.SetString(SeenPrefKey, CurrentNoticeId);
        PlayerPrefs.Save();
    }

    static void Build()
    {
        var sprite = Resources.Load<Sprite>(BoardResourcePath);
        if (sprite == null)
        {
            Debug.LogError("[LoginNoticePopupUI] 公告板图缺失：Resources/" + BoardResourcePath);
            return;
        }

        var root = new GameObject("LoginNoticePopupUI", typeof(RectTransform));
        var canvas = root.AddComponent<Canvas>();
        // 项目红线：所有界面 Canvas 一律走摄像机模式，禁止裸 Overlay。
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownPopup, UICanvasSetup.ResolveUiCamera());

        var ui = root.AddComponent<LoginNoticePopupUI>();

        // 全屏遮罩：半透明黑，吃掉外部点击（公告开着时点不到底下的「设置」按钮，避免两个同层 900 弹窗排序不定）。
        // 名字用 Dim：UiLayoutStretch 视其为 FillScreen 背景层，不做上下重锚定。
        var dim = NewImage("Dim", root.transform);
        dim.color = new Color(0f, 0f, 0f, 0.5f);
        UiLayoutStretch.ApplyFillScreen(dim.rectTransform);

        // 公告板：500×604 原尺寸 1:1 居中（像素图不缩放）。中心 y 与父级中心重合，
        // 命中 UiLayoutStretch 的「居中元素保护」，不会被上下拆分逻辑搬动。
        var board = NewImage("Board", root.transform);
        board.sprite = sprite;
        board.SetNativeSize();
        board.raycastTarget = true;
        var boardRt = board.rectTransform;
        boardRt.anchorMin = boardRt.anchorMax = new Vector2(0.5f, 0.5f);
        boardRt.pivot = new Vector2(0.5f, 0.5f);
        boardRt.anchoredPosition = Vector2.zero;

        // 正文：按原图纸面安全区换算后两边再各缩进 15px（2026-10-20 主人拍板「框体两边往里缩，
        // 不要跟图片一样宽」），纸面 x∈[55,448] 宽 393 → 正文框 360 居中，两侧留白 ≈20px。
        // 字号 24 降 22：最长行 16 个全角字 = 352px，22 号在 360 宽内放得下不折行
        //（24 号需 384px，缩窄后会把「点右侧「设置」，选「清除数据」，」折得很难看）。
        var body = NewText("Body", board.transform, NoticeBody);
        body.fontSize = 22;
        body.lineSpacing = 1.4f;
        body.alignment = TextAnchor.UpperCenter;
        body.color = new Color(0.29f, 0.22f, 0.16f, 1f);   // 深棕，配米色纸面，别用白色
        body.raycastTarget = false;
        var bodyRt = body.rectTransform;
        bodyRt.anchorMin = bodyRt.anchorMax = new Vector2(0.5f, 0.5f);
        bodyRt.pivot = new Vector2(0.5f, 0.5f);
        bodyRt.sizeDelta = new Vector2(360f, 355f);
        // 14:59 主人看真机截图拍板「字往下点，太往上了」：27 → -18（下移 45px）。
        // 文字块高 ≈246px，下移后底部离纸面书本装饰（y<-153）还有约 66px，不会压书；
        // 中心偏移 |-18| < 64，仍命中居中元素保护，不会被上下重锚定搬走。
        bodyRt.anchoredPosition = new Vector2(0f, -18f);
        ui._body = body;

        // 右上角关闭热区：X 美术中心≈(475,52)，相对图心(250,302) → (222,248)。
        // 图上已画 X，这里只放透明热区接点击；按压反馈由 ApplyPopup 里的 UiButtonPressFeedback 统一挂。
        var closeGo = new GameObject("Close", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        closeGo.transform.SetParent(board.transform, false);
        var closeImg = closeGo.GetComponent<Image>();
        closeImg.color = new Color(1f, 1f, 1f, 0f);
        closeImg.raycastTarget = true;
        var closeRt = closeGo.GetComponent<RectTransform>();
        closeRt.anchorMin = closeRt.anchorMax = new Vector2(0.5f, 0.5f);
        closeRt.pivot = new Vector2(0.5f, 0.5f);
        closeRt.sizeDelta = new Vector2(56f, 56f);
        closeRt.anchoredPosition = new Vector2(222f, 248f);
        var closeBtn = closeGo.GetComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        closeBtn.transition = Selectable.Transition.None;
        closeBtn.onClick.AddListener(ui.Close);

        GameFonts.ApplyToHierarchy(root.transform);
        // 五坑⑤：运行时建树的弹窗 Build 末尾必须 SetActive(false)，由 Open() 再显示。
        root.SetActive(false);
    }

    static Image NewImage(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        return go.GetComponent<Image>();
    }

    static Text NewText(string name, Transform parent, string content)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.text = content;
        t.font = GameFonts.GetChinese();
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.supportRichText = true;
        return t;
    }
}
