using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 通用信息弹窗：图鉴点怪物/佣兵详情。
/// 预制体：Resources/Prefabs/Town/CodexInfoPopup
/// 节点按名绑定；不写回覆盖用户手做布局。
/// </summary>
public class CodexInfoPopupUI : MonoBehaviour
{
    public const string PrefabPath = "Prefabs/Town/CodexInfoPopup";
    /// <summary>【2026-11-12 主人拍板】任务详情「前往」按钮复用游戏里现成的偏蓝色通用按钮（无字，100×33）。</summary>
    const string GoButtonSpritePath = "UI/Art/Common/复活按钮";

    public static CodexInfoPopupUI Instance { get; private set; }

    public GameObject root;
    public Image panel;
    public Image portrait;
    public Text titleText;
    public Text metaText;
    public Text descText;
    public Text loreText;
    public Button closeButton;
    public Button dimButton;
    /// <summary>top 节点下的小图标：图鉴入口按怪物/佣兵换 guaiwuicon/yongbingicon。</summary>
    public Image topIcon;
    /// <summary>top 节点下的头条文字（预制体文案「怪物信息」）：图鉴入口按怪物/佣兵换成「怪物图鉴/佣兵图鉴」（2026-10-20）。</summary>
    public Text topTitleText;
    /// <summary>可选「解锁」按钮：预制体里没有时运行时补一个，节点名固定 UnlockButton。</summary>
    public Button unlockButton;
    public Text unlockButtonLabel;

    /// <summary>
    /// 奖励图标胶（2026-11-01 主人要求「奖励的字用图标代替」）：
    /// 金币/天赋石等不再写文字行，改弹窗内 icon＋×N 胶，运行时建在 Panel 上。
    /// </summary>
    public struct RewardChip
    {
        public Sprite Icon;
        /// <summary>图标名（缺图报错定位用，不兜底）。</summary>
        public string IconPath;
        public string Label;
    }

    GameObject _rewardRow;
    RectTransform _rewardRowRt;
    /// <summary>top 头条节点（top/Name，「怪物信息/主线任务/支线任务」…）缓存；LayoutContent 里随面板高度归位（2026-11-03 排版修复）。</summary>
    RectTransform _topNode;
    /// <summary>
    /// 奖励胶图标边长 / 数量字字号 / 数量字颜色（跟正文深棕一致）。
    /// 【2026-11-03 主人「奖励图标和数字对齐并放大一点」】图标 40→52、数字 24→28；
    /// 数字补 LayoutElement 与图标同高（52）并 MiddleLeft 居中 → 图标与数字共用一条中线，不再一高一低。
    /// </summary>
    const float RewardIconSize = 52f;
    const int RewardLabelSize = 28;
    /// <summary>
    /// 任务弹窗排版（2026-11-04 主人拍板「没有对齐、下面还单独一行、整体零散、还有大片空地」）：
    /// true 时正文各行统一左边界（说明靠左、进度同行靠右同基线）、奖励行贴说明下方同左边界、
    /// 面板按内容收高；只有主线/支线弹窗传 true。世界图鉴等长正文弹窗保持预制体原版式。
    /// </summary>
    bool _taskLayout;

    /// <summary>top/Name 在预制体里的原始 x（18，为左侧小图标做的补偿）；无图标时归中、有图标时还原。</summary>
    float? _baseTopNameX;
    /// <summary>Desc/Lore 预制体原对齐（首次打开记录）；任务弹窗居中、其他弹窗还原，避免串味。</summary>
    TextAnchor? _baseDescAlign;
    TextAnchor? _baseLoreAlign;
    /// <summary>top 头条中心到面板顶边的内边距：预制体按 720 面板调的（顶边 360 − 头条中心 291 = 69）；2026-11-03 排版修复后头条跟随面板高度，永远保持顶边下 69px。</summary>
    const float TopTitleInset = 69f;
    static readonly Color RewardLabelColor = new Color(0.32f, 0.21f, 0.12f, 1f);

    Action _onUnlock;
    int _baseDescSize;   // Desc/Lore 基准字号（首次打开时记录，降档排版后用于恢复）
    int _baseLoreSize;
    /// <summary>top/Name 与 Title 的预制体基准字号（35 / 32）；每次 Show 从基准重算，防 DDOL 弹窗串味。</summary>
    int _baseTopNameSize;
    int _baseTitleSize;
    /// <summary>top/Name 预制体基准颜色（浅黄绿）；佣兵名按稀有度染色后，其它弹窗要还原。</summary>
    Color _baseTopNameColor;
    bool _baseTopNameColorRecorded;
    /// <summary>面板基准尺寸（首次打开记录；每次打开从基准重算，拉伸不残留）。</summary>
    Vector2 _basePanelSize;
    /// <summary>标题/简介基准 y（首次打开记录；无立绘弹窗上移填补头像框空位用，2026-11-01）。</summary>
    float? _baseTitleY;
    float? _baseMetaY;
    /// <summary>可选大图（佣兵全身立绘）：非空时点弹窗头像放大查看，点任意处关闭（2026-10-20）。</summary>
    Sprite _zoomSprite;
    RectTransform _zoomRoot;
    Image _zoomImg;
    /// <summary>放大层背景（梦幻天空圣殿舞台，2026-10-31）。</summary>
    Image _zoomBgImg;

    /// <summary>放大层背景图（2026-10-31 主人拍板）：走 Resources 路径加载，不进预制体引用。</summary>
    const string ZoomBackdropArt = "UI/Art/对战/梦幻天空圣殿舞台";
    /// <summary>立绘放大倍数（主人要求「立绘放大 50%」）。</summary>
    const float ZoomPicScale = 1.5f;
    /// <summary>背景亮度系数（1=原图，压一点让立绘更突出）。</summary>
    const float ZoomBackdropBrightness = 0.85f;
    /// <summary>放大前的四周留边（沿用原 ZoomPic 的 offset，放大以它为基准）。</summary>
    static readonly Vector2 ZoomPicMargin = new Vector2(60f, 80f);

    public bool IsOpen => root != null && root.activeSelf;

    public static void Show(string title, string meta, string desc, string lore, Sprite portraitSprite, bool? codexKind = null,
        string topTitleOverride = null, string topIconPath = null, RewardChip[] rewards = null, bool taskLayout = false,
        string goText = null, Action onGo = null)
    {
        // 【2026-11-12】goText/onGo 非空时在弹窗底部显示「前往」：底图换成游戏里的蓝色通用按钮，
        // 文案与回调由调用方给，不动预制体与弹窗排版（底部留白 bottomPad 已按按钮显隐自适应）。
        Ensure().Open(title, meta, desc, lore, portraitSprite, goText, onGo, codexKind: codexKind,
            topTitleOverride: topTitleOverride, topIconPath: topIconPath, rewards: rewards, taskLayout: taskLayout,
            goStyle: onGo != null);
    }

    /// <summary>
    /// 带大图的重载（2026-10-20 用户拍板）：zoomSprite 非空时点弹窗头像可放大查看，
    /// 深色遮罩铺满、点任意处关闭；传 null 则头像不可点。
    /// </summary>
    public static void Show(string title, string meta, string desc, string lore, Sprite portraitSprite, Sprite zoomSprite,
        bool? codexKind = null, string topTitleOverride = null, Color? nameColor = null, bool silhouette = false)
    {
        Ensure().Open(title, meta, desc, lore, portraitSprite, null, null, zoomSprite, codexKind, topTitleOverride,
            nameColor: nameColor, silhouette: silhouette);
    }

    /// <summary>
    /// 带解锁按钮的重载（2026-09-18 用户拍板 Q2）。
    /// unlockText 非空且 onUnlock 非空才显示按钮；不显示时节点 SetActive(false)。
    /// </summary>
    public static void Show(string title, string meta, string desc, string lore, Sprite portraitSprite,
        string unlockText, Action onUnlock)
    {
        Ensure().Open(title, meta, desc, lore, portraitSprite, unlockText, onUnlock);
    }

    public static void HideActive()
    {
        if (Instance != null) Instance.Close();
    }

    public static CodexInfoPopupUI Ensure()
    {
        if (Instance != null) return Instance;

        var prefab = Resources.Load<GameObject>(PrefabPath);
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab);
            go.name = "CodexInfoPopup";
        }
        else
        {
            Debug.LogWarning($"[CodexInfoPopup] 未找到 {PrefabPath}，临时代码搭壳");
            go = new GameObject("CodexInfoPopup", typeof(RectTransform));
            BuildHierarchy(go);
        }
        DontDestroyOnLoad(go);
        var ui = go.GetComponent<CodexInfoPopupUI>() ?? go.AddComponent<CodexInfoPopupUI>();
        return ui;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        BindRefs();
        // 【2026-11-13 主人拍板】头部名字（top/Name）加描边：预制体名字色是浅黄绿、佣兵名还按稀有度
        // 染亮色（RarityPalette 亮档本就是「深色底上可读」的配色），羊皮纸浅底上看不清（截图反馈）。
        // 描边色走 RarityPalette.Outline（项目统一描线色）；只加组件，不动字号/框宽/位置。
        // 阴影（Shadow）只往一侧偏、描边（Outline）四面包，浅字浅底用描边分隔更明显，故选描边。
        if (topTitleText != null && topTitleText.GetComponent<Outline>() == null)
        {
            var nameOutline = topTitleText.gameObject.AddComponent<Outline>();
            nameOutline.effectColor = RarityPalette.Outline;
            nameOutline.effectDistance = new Vector2(1f, -1f);
        }
        Wire();
        // 2026-10-31 主人报「怪物信息排版优化」时发现：本弹窗此前从未走 UICanvasSetup
        // （预制体 Canvas 是 Screen Space Camera 但相机为空、Match 写死按高），
        // 按 AGENTS 4.6 拍板统一接管：绑场景 UI 相机 + 720×1280 参考分辨率 +
        // 瘦屏自动按宽匹配，瘦屏上不再偏移/裁边。
        var canvas = GetComponent<Canvas>();
        if (canvas != null)
            UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownConfirm);
        ApplyProjectArt();
        if (root != null) root.SetActive(false);
    }

    /// <summary>
    /// 用项目里的现成素材给程序搭的壳“换皮”（2026-09-18 用户要求：程序生成的先在项目里找素材拼）。
    /// 走 UiKeyedBackgrounds 运行时替换，找不到素材时保持原样，不动预制体文件。
    /// 面板 = Frames/内容底（羊皮纸），关闭钮 = Frames/图层 1，解锁钮 = Frames/字底；
    /// 头像框保持预制体原样，不再换成「图层 1」（2026-10-20 用户要求：头像里去掉紫色板背景）。
    /// 纸底上白字看不清，正文统一改深棕。
    /// </summary>
    void ApplyProjectArt()
    {
        if (panel != null && UiKeyedBackgrounds.ApplyLogFrame(panel, "内容底", preserveAspect: false))
        {
            // 【2026-10-21 主人拍板】换图可以，图的显示方式不许代码改 —— 之前这里强制
            // panel.type = Simple，把主人做好的九宫格「内容底」压成整图拉伸（边框被拉粗、
            // 中间可写区被吃掉），正是文字骑到框上的根因。现在只换图，不动 type。
            var dark = new Color(0.24f, 0.15f, 0.08f, 1f);
            if (titleText != null) titleText.color = dark;
            if (metaText != null) metaText.color = new Color(0.35f, 0.24f, 0.14f, 1f);
            if (descText != null) descText.color = dark;
            if (loreText != null) loreText.color = new Color(0.32f, 0.21f, 0.12f, 1f);
        }

        var closeImg = closeButton != null ? closeButton.GetComponent<Image>() : null;
        if (closeImg != null)
            UiKeyedBackgrounds.ApplyLogFrame(closeImg, "图层 1", preserveAspect: false);

        if (unlockButton != null)
        {
            var ubImg = unlockButton.GetComponent<Image>();
            if (ubImg != null)
                UiKeyedBackgrounds.ApplyLogFrame(ubImg, "字底", preserveAspect: false);
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void BindRefs()
    {
        if (root == null)
            root = transform.Find("Root")?.gameObject ?? gameObject;
        if (panel == null)
            panel = FindDeep(root.transform, "Panel")?.GetComponent<Image>();
        if (portrait == null)
            portrait = FindDeep(root.transform, "Portrait")?.GetComponent<Image>();
        if (titleText == null)
            titleText = FindDeep(root.transform, "Title")?.GetComponent<Text>();
        if (metaText == null)
            metaText = FindDeep(root.transform, "Meta")?.GetComponent<Text>();
        if (descText == null)
            descText = FindDeep(root.transform, "Desc")?.GetComponent<Text>();
        if (loreText == null)
            loreText = FindDeep(root.transform, "Lore")?.GetComponent<Text>();
        if (closeButton == null)
            closeButton = FindDeep(root.transform, "CloseButton")?.GetComponent<Button>();
        if (dimButton == null)
            dimButton = FindDeep(root.transform, "Dim")?.GetComponent<Button>();
        if (topIcon == null)
            topIcon = FindDeep(root.transform, "icon")?.GetComponent<Image>();
        if (topTitleText == null)
            topTitleText = FindDeep(root.transform, "Name")?.GetComponent<Text>();
    }

    void Wire()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }
        if (dimButton != null)
        {
            dimButton.onClick.RemoveAllListeners();
            dimButton.onClick.AddListener(Close);
        }
    }

    /// <summary>
    /// 取解锁按钮：预制体里找 UnlockButton，找不到才运行时补建（不动预制体结构）。
    /// create=false 时只查找，不补建。
    /// </summary>
    Button ResolveUnlockButton(bool create)
    {
        if (unlockButton == null)
            unlockButton = FindDeep(root.transform, "UnlockButton")?.GetComponent<Button>();
        if (unlockButton == null && create)
            unlockButton = BuildUnlockButton();
        if (unlockButton != null && unlockButtonLabel == null)
            unlockButtonLabel = unlockButton.GetComponentInChildren<Text>();
        return unlockButton;
    }

    Button BuildUnlockButton()
    {
        var host = panel != null ? panel.transform : (root != null ? root.transform : transform);
        var rt = CreateUi(host, "UnlockButton", true);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 96f);   // 关闭按钮（y=28，高 52）上方，不压住它
        rt.sizeDelta = new Vector2(240f, 56f);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = new Color(0.28f, 0.46f, 0.30f, 1f);
        // 程序生成的按钮底也换成项目素材（字底横条），找不到素材时保留绿色兜底
        UiKeyedBackgrounds.ApplyLogFrame(img, "字底", preserveAspect: false);
        var btn = rt.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.ColorTint;
        unlockButtonLabel = AddLabel(rt, "Label", "解锁", 24, Vector2.zero,
            new Vector2(240f, 56f), TextAnchor.MiddleCenter, stretch: true);
        GameFonts.ApplyToHierarchy(rt);
        return btn;
    }

    void OnClickUnlock()
    {
        var cb = _onUnlock;
        if (cb == null) return;
        cb();
    }

    void Open(string title, string meta, string desc, string lore, Sprite portraitSprite,
        string unlockText, Action onUnlock, Sprite zoomSprite = null, bool? codexKind = null,
        string topTitleOverride = null, string topIconPath = null, RewardChip[] rewards = null, bool taskLayout = false,
        Color? nameColor = null, bool silhouette = false, bool goStyle = false)
    {
        BindRefs();
        Wire();
        _taskLayout = taskLayout;
        // DDOL 弹窗每次 Show 先重绑相机 + 重算适配（AGENTS 4.6 拍板，幂等）
        var canvas = GetComponent<Canvas>();
        if (canvas != null)
            UICanvasSetup.RefreshPopup(canvas, GameConfig.UiSort.TownConfirm);

        _onUnlock = onUnlock;
        bool showUnlock = !string.IsNullOrEmpty(unlockText) && onUnlock != null;
        var ub = ResolveUnlockButton(showUnlock);
        if (ub != null)
        {
            ub.gameObject.SetActive(showUnlock);
            if (showUnlock)
            {
                ub.onClick.RemoveAllListeners();
                ub.onClick.AddListener(OnClickUnlock);
                if (unlockButtonLabel != null) unlockButtonLabel.text = unlockText;
                // 【2026-11-12 主人拍板】「前往」用游戏里现成的偏蓝色通用按钮底图，
                // 白字「前往」由 unlockButtonLabel 写；「解锁」仍走冒险日志「字底」原样。
                // 每次 Show 都重设，避免同一弹窗实例在两种按钮间切换时残留上一张图。
                // 取不到蓝色图直接 LogError，不兜底。
                var ubImg = ub.GetComponent<Image>();
                if (ubImg != null)
                {
                    if (!goStyle) UiKeyedBackgrounds.ApplyLogFrame(ubImg, "字底", false);
                    else
                    {
                        var sp = Resources.Load<Sprite>(GoButtonSpritePath);
                        if (sp == null) Debug.LogError("[CodexInfoPopup] 缺「前往」按钮底图：" + GoButtonSpritePath);
                        else { ubImg.sprite = sp; ubImg.color = Color.white; }
                    }
                }
            }
        }
        // 【2026-11-07 主人拍板】图鉴详情：头部 top/Name 固定放名字，大字 Title 放出现地点（怪物）/职业（佣兵）。
        // 所以 title 不再固定是名字（2026-10-20「Title 永远是名字」的旧口径作废）；调用方按新口径传。
        if (titleText != null) titleText.text = title ?? "";
        // 图鉴入口（2026-10-20 主人拍板）：按怪物/佣兵换 top/icon 图标；顶部头条先落版块名，
        // 随后由 topTitleOverride 覆盖成名字（2026-11-07 口径）。其他调用方不传 codexKind，保持原样。
        if (codexKind.HasValue)
        {
            // 缺名字 = 没按新口径传，报出来让人看见，不静默留着旧版块名
            if (string.IsNullOrEmpty(topTitleOverride))
                Debug.LogError("[CodexInfoPopup] 图鉴弹窗头部应传名字（topTitleOverride），当前缺失");
            if (topTitleText != null)
                topTitleText.text = codexKind.Value ? "佣兵图鉴" : "怪物图鉴";
            if (topIcon != null)
            {
                topIcon.gameObject.SetActive(true);
                topIcon.sprite = Resources.Load<Sprite>(codexKind.Value
                    ? "UI/Art/冒险日志/yongbingicon"
                    : "UI/Art/冒险日志/guaiwuicon");
            }
        }
        // 2026-11-01 主人拍板：世界页条目详情也走本弹窗，头部文案/图标按入口自定义（不传保持原样）；
        // 同日晚追加：topIconPath 传空串 = 明确不要图标（主线/支线/世界弹窗），节点收起防残留上一单的图
        if (!string.IsNullOrEmpty(topTitleOverride) && topTitleText != null)
            topTitleText.text = topTitleOverride;
        if (topIconPath != null && topIcon != null)
        {
            if (topIconPath.Length == 0)
                topIcon.gameObject.SetActive(false);
            else
            {
                topIcon.gameObject.SetActive(true);
                topIcon.sprite = Resources.Load<Sprite>(topIconPath);
            }
        }
        // 【2026-11-03 主人报「主线任务/支线任务/世界图鉴标题歪了」】预制体 top/Name 的 x=18 是给
        // 「怪物信息/佣兵图鉴」左侧小图标（icon x=-97）做的视觉补偿；主线/支线/世界这些弹窗把图标
        // 收起了，文字仍按 18 右偏 → 整条标题看着歪。这里按图标是否显示归位：有图标保持预制体值，
        // 没图标归中 x=0。只动这一个 x，宽度/字号/两条装饰线一律不碰。
        if (topTitleText != null)
        {
            var nrt = topTitleText.rectTransform;
            if (!_baseTopNameX.HasValue) _baseTopNameX = nrt.anchoredPosition.x;
            bool iconShown = topIcon != null && topIcon.gameObject.activeSelf;
            nrt.anchoredPosition = new Vector2(
                iconShown ? _baseTopNameX.Value : 0f, nrt.anchoredPosition.y);
        }
        if (metaText != null) metaText.text = meta ?? "";
        if (descText != null) descText.text = desc ?? "";
        if (loreText != null) loreText.text = lore ?? "";
        ApplyRewardChips(rewards);   // 2026-11-01 主人要求：奖励用图标胶代替文字行
        if (portrait != null)
        {
            portrait.sprite = portraitSprite;
            portrait.preserveAspect = true;
            // 【2026-11-11 主人拍板】没遇到的怪也点得开看预告，形象给黑影剪影
            // （和图鉴格子上未记录时的黑色 92% 同一套色）。
            portrait.color = silhouette ? new Color(0f, 0f, 0f, 0.92f)
                : (portraitSprite != null ? Color.white : new Color(1f, 1f, 1f, 0.15f));
            // 2026-11-01 主人拍板「弹窗不用加图标，排版也不好」：任务/世界类弹窗不传立绘，
            // 头像框整个收起（留着就是一块空框），标题/简介上移填补，弹窗不空一大块。
            var frameTf = portrait.transform.parent;
            if (frameTf != null && frameTf.name == "PortraitFrame")
                frameTf.gameObject.SetActive(portraitSprite != null);
            // 【2026-11-03 排版修复】上移量 200→172：头条跟随面板后字底在顶边下 94（69+50/2），
            // Title 上移 172 后框顶在顶边下 108，留 14px 间距；200 会叠到头条上。
            float textShift = portraitSprite != null ? 0f : 172f;   // 标题/简介上移量（补头像框空位）
            if (titleText != null)
            {
                var trt = titleText.rectTransform;
                if (!_baseTitleY.HasValue) _baseTitleY = trt.anchoredPosition.y;
                trt.anchoredPosition = new Vector2(trt.anchoredPosition.x, _baseTitleY.Value + textShift);
            }
            if (metaText != null)
            {
                var mrt2 = metaText.rectTransform;
                if (!_baseMetaY.HasValue) _baseMetaY = mrt2.anchoredPosition.y;
                mrt2.anchoredPosition = new Vector2(mrt2.anchoredPosition.x, _baseMetaY.Value + textShift);
            }
            if (portraitSprite != null)
                PortraitIdleMotion.EnsureOn(portrait.rectTransform, 0.18f);
            else
            {
                var idle = portrait.GetComponent<PortraitIdleMotion>();
                if (idle != null) idle.enabled = false;
            }

            // 点头像放大查看大图：有大图才可点（怪物弹窗不传大图，保持不可点）
            _zoomSprite = (zoomSprite != null && portraitSprite != null) ? zoomSprite : null;
            // 【2026-10-22 修】预制体 Portrait 的 Image 是 m_RaycastTarget: 0（CodexInfoPopup.prefab:383），
            // 射线收不到 → 挂上去的 Button 永远不响应，黑底立绘层弹不出来。只有可放大时才打开，
            // 怪物弹窗（无大图）保持预制体原样关闭。
            portrait.raycastTarget = _zoomSprite != null;
            var zoomBtn = portrait.GetComponent<Button>();
            if (zoomBtn == null && _zoomSprite != null)
            {
                zoomBtn = portrait.gameObject.AddComponent<Button>();
                zoomBtn.transition = Selectable.Transition.None;
            }
            if (zoomBtn != null)
            {
                zoomBtn.onClick.RemoveAllListeners();
                zoomBtn.onClick.AddListener(ShowZoom);
                zoomBtn.enabled = _zoomSprite != null;
            }
        }
        if (root != null)
        {
            root.SetActive(true);
            transform.SetAsLastSibling();
        }
        GameFonts.ApplyToHierarchy(transform);
        ApplyCodexHeaderStyle(codexKind, nameColor);
        LayoutContent();
    }

    /// <summary>
    /// 【2026-11-07 主人拍板】图鉴弹窗头部 / 大字样式，一次 Show 一个出口：
    /// ① 头部 top/Name 以后固定放名字，预制体该框只有 160 宽 / 35 号字，5 字以上名字会换行被截断
    ///    → 只按字数缩字号（字体可改，框宽 / x / 锚点一律不动），短名字保持预制体 35 号；
    /// ② 佣兵名字按稀有度染色（nameColor，调用方走 RarityPalette 取），怪物 / 其它弹窗还原预制体色；
    /// ③ 佣兵详情大字（职业）比原来名字放大两号：32 → 34；怪物详情大字保持 32；
    /// ④ 每次 Show 都从基准恢复，任务 / 世界 / 商品等其它弹窗不被串味。
    /// </summary>
    void ApplyCodexHeaderStyle(bool? codexKind, Color? nameColor)
    {
        if (topTitleText != null)
        {
            if (_baseTopNameSize <= 0) _baseTopNameSize = topTitleText.fontSize;
            if (!_baseTopNameColorRecorded)
            {
                _baseTopNameColor = topTitleText.color;
                _baseTopNameColorRecorded = true;
            }
            topTitleText.fontSize = codexKind.HasValue
                ? FitTopNameSize(topTitleText.text, _baseTopNameSize)
                : _baseTopNameSize;
            topTitleText.color = nameColor ?? _baseTopNameColor;
        }
        if (titleText != null)
        {
            if (_baseTitleSize <= 0) _baseTitleSize = titleText.fontSize;
            titleText.fontSize = codexKind == true ? _baseTitleSize + 2 : _baseTitleSize;
        }
    }

    /// <summary>按名字字数缩到 160 宽一行能放下（每字宽约等于字号，留 10px 余量）。</summary>
    static int FitTopNameSize(string name, int baseSize)
    {
        if (string.IsNullOrEmpty(name) || name.Length <= 4) return baseSize;
        return Mathf.Max(16, Mathf.Min(baseSize, 150 / name.Length));
    }

    public void Close()
    {
        HideZoom();
        if (root != null) root.SetActive(false);
    }

    /// <summary>点任意处关闭大图（整层遮罩就是一个按钮）。</summary>
    public void HideZoom()
    {
        if (_zoomRoot != null && _zoomRoot.gameObject.activeSelf) _zoomRoot.gameObject.SetActive(false);
    }

    void ShowZoom()
    {
        if (_zoomSprite == null) return;
        EnsureZoomLayer();
        if (_zoomImg != null)
        {
            _zoomImg.sprite = _zoomSprite;
            _zoomImg.preserveAspect = true;
        }
        if (_zoomRoot != null)
        {
            _zoomRoot.gameObject.SetActive(true);
            _zoomRoot.SetAsLastSibling();
        }
        FitZoomLayer();
    }

    /// <summary>
    /// 大图层：舞台背景 + 居中立绘（保持比例）。运行时按需补建一次，不动预制体；
    /// 立绘自身 raycastTarget 关掉，点哪里都算点遮罩 → 任意处关闭。
    /// </summary>
    void EnsureZoomLayer()
    {
        if (_zoomRoot != null) return;
        var host = root != null ? root.transform : transform;
        _zoomRoot = CreateUi(host, "ZoomLayer", true);
        Stretch(_zoomRoot);
        var maskImg = _zoomRoot.gameObject.AddComponent<Image>();
        maskImg.color = new Color(0f, 0f, 0f, 0.88f);
        var btn = _zoomRoot.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(HideZoom);

        // 背景层（2026-10-31 主人拍板：立绘后面放「梦幻天空圣殿舞台」）：
        // 铺在黑色遮罩之上、立绘之下；等比 cover 铺满由 FitZoomLayer 每次打开时算。
        var bgRt = CreateUi(_zoomRoot, "ZoomBg", true);
        Stretch(bgRt);
        _zoomBgImg = bgRt.gameObject.AddComponent<Image>();
        _zoomBgImg.raycastTarget = false;   // 点背景算点遮罩 → 关闭
        var bg = Resources.Load<Sprite>(ZoomBackdropArt);
        if (bg != null)
        {
            _zoomBgImg.sprite = bg;
            _zoomBgImg.color = new Color(ZoomBackdropBrightness, ZoomBackdropBrightness, ZoomBackdropBrightness, 1f);
        }
        else
        {
            // 缺图不兜底：报出来让人看见，背景保持原黑色遮罩
            Debug.LogError($"[CodexInfoPopup] 放大层背景缺失：Resources/{ZoomBackdropArt}（确认 jpg 与 .meta 都在）");
            bgRt.gameObject.SetActive(false);
        }

        var picRt = CreateUi(_zoomRoot, "ZoomPic", true);
        // 居中锚点：尺寸由 FitZoomLayer 按「可用区 × 1.5」算，preserveAspect 保证不变形
        picRt.anchorMin = picRt.anchorMax = new Vector2(0.5f, 0.5f);
        picRt.pivot = new Vector2(0.5f, 0.5f);
        picRt.anchoredPosition = Vector2.zero;
        _zoomImg = picRt.gameObject.AddComponent<Image>();
        _zoomImg.raycastTarget = false;
    }

    /// <summary>
    /// 放大层适配（2026-10-31 主人拍板「注意要适配」）：
    /// 背景等比 cover 铺满（只等比、绝不拉伸变形，多出屏幕的部分溢出裁掉）；
    /// 立绘按「原可用区 × 1.5」居中放大，preserveAspect 不变形，超出屏幕部分自然出血。
    /// 每次点开都重算 —— 不同机型 / 瘦屏的长宽比此时才是实际值。
    /// </summary>
    void FitZoomLayer()
    {
        if (_zoomRoot == null) return;
        Canvas.ForceUpdateCanvases();   // 先让布局算好，父级 rect 才是实际值
        float w = _zoomRoot.rect.width, h = _zoomRoot.rect.height;
        if (w <= 0f || h <= 0f) return;

        if (_zoomBgImg != null && _zoomBgImg.sprite != null)
        {
            var src = _zoomBgImg.sprite.rect;
            if (src.width > 0f && src.height > 0f)
            {
                float k = Mathf.Max(w / src.width, h / src.height);
                var bgRt = _zoomBgImg.rectTransform;
                bgRt.anchorMin = bgRt.anchorMax = new Vector2(0.5f, 0.5f);
                bgRt.pivot = new Vector2(0.5f, 0.5f);
                bgRt.anchoredPosition = Vector2.zero;
                bgRt.sizeDelta = new Vector2(src.width * k, src.height * k);
            }
        }

        if (_zoomImg != null)
        {
            float availW = Mathf.Max(0f, w - ZoomPicMargin.x * 2f);
            float availH = Mathf.Max(0f, h - ZoomPicMargin.y * 2f);
            _zoomImg.rectTransform.sizeDelta = new Vector2(availW * ZoomPicScale, availH * ZoomPicScale);
        }
    }

    /// <summary>
    /// 奖励图标行：挂在 Panel 上，锚点/宽度照抄 Desc（只换内容，不另立版式）。
    /// 横排居中，胶 = icon(40×40) ＋ ×N 数量字；行高由 ContentSizeFitter 按内容算。
    /// </summary>
    void BuildRewardRow()
    {
        var descRt = descText.rectTransform;
        _rewardRowRt = CreateUi(panel.transform, "RewardRow", true);
        _rewardRowRt.anchorMin = descRt.anchorMin;
        _rewardRowRt.anchorMax = descRt.anchorMax;
        _rewardRowRt.pivot = descRt.pivot;
        _rewardRowRt.sizeDelta = new Vector2(descRt.sizeDelta.x, 0f);
        _rewardRowRt.anchoredPosition = new Vector2(descRt.anchoredPosition.x, descRt.anchoredPosition.y);
        var hlg = _rewardRowRt.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 36f;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        var csf = _rewardRowRt.gameObject.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _rewardRow = _rewardRowRt.gameObject;
        _rewardRow.SetActive(false);
    }

    /// <summary>
    /// 2026-11-01 主人要求「奖励的字用图标代替」：desc 下方一行 icon＋×N 胶
    /// （金币/天赋石走 CommonIconSprites 统一出口）；无奖励传 null/空 → 整行收起。
    /// </summary>
    void ApplyRewardChips(RewardChip[] chips)
    {
        if (_rewardRow == null && panel != null)
            BuildRewardRow();
        if (_rewardRow == null) return;
        if (chips == null || chips.Length == 0)
        {
            _rewardRow.SetActive(false);
            return;
        }
        // 旧胶先停用再销毁：当帧布局重建不会把已销毁节点算进 preferred
        for (int i = _rewardRow.transform.childCount - 1; i >= 0; i--)
        {
            var c = _rewardRow.transform.GetChild(i);
            c.gameObject.SetActive(false);
            Destroy(c.gameObject);
        }
        for (int i = 0; i < chips.Length; i++)
        {
            var chip = chips[i];
            var chipRt = CreateUi(_rewardRow.transform, "RewardChip" + i, true);
            var hlg = chipRt.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.spacing = 8f;
            var csf = chipRt.gameObject.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var iconRt = CreateUi(chipRt, "Icon", true);
            iconRt.sizeDelta = new Vector2(RewardIconSize, RewardIconSize);
            var img = iconRt.gameObject.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            if (chip.Icon != null)
                img.sprite = chip.Icon;
            else
            {
                // 缺图不兜底：数量字仍可读，图标位空着让人看见
                img.enabled = false;
                Debug.LogError("[CodexInfoPopup] 奖励图标缺失：" + chip.IconPath);
            }
            var le = iconRt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = RewardIconSize;
            le.preferredHeight = RewardIconSize;

            var num = CreateChipText(chipRt, "Num", chip.Label ?? "", RewardLabelSize, RewardLabelColor);
            // 【2026-11-03 对齐】数字框与图标同高（52）+ MiddleLeft → 图标和数字共用中线。
            // 宽度不写，交给 Text 自己算（LayoutElement 只压高度，宽度留 -1）。
            var numLe = num.gameObject.AddComponent<LayoutElement>();
            numLe.preferredHeight = RewardIconSize;
            num.verticalOverflow = VerticalWrapMode.Overflow;
        }
        _rewardRow.SetActive(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(_rewardRowRt);
    }

    static Text CreateChipText(Transform parent, string name, string content, int size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAnchor.MiddleLeft;
        t.raycastTarget = false;
        t.font = GameFonts.GetChinese();
        return t;
    }

    /// <summary>
    /// 【2026-10-19 排版优化】Desc/Lore 按实际文本高度流式排列。
    /// 【2026-10-21 主人拍板】图 / 字可以换，**字的宽度不许代码改**。
    /// 所以这里只写纵向：Desc 紧贴 Meta 底、Lore 紧跟 Desc 实际高度下方；
    /// Desc/Lore 的 Width、x、sizeDelta 全部以预制体为准，代码一笔都不碰
    /// （旧写法把 sizeDelta 读出来再原样写回，仍是代码在动预制体的值，且拉伸锚点下会算错宽度）。
    /// 文本长度超出可用区时才降字号，避免压到底部「关闭/解锁」按钮。
    /// 【2026-11-01 排版优化（主人要求）】①Meta 为空（任务/世界弹窗不传副标题）不再白占一格；
    /// ②奖励图标行插在 Desc 与 Lore 之间；③内容少时面板对称收高，底部不再空一大块。
    /// </summary>
    void LayoutContent()
    {
        if (panel == null || descText == null || loreText == null) return;
        var panelRt = panel.transform as RectTransform;
        var descRt = descText.rectTransform;
        var loreRt = loreText.rectTransform;
        if (panelRt == null || descRt == null || loreRt == null) return;

        // 2026-11-01 主人拍板「位置不够可以上下拉伸」：先记面板基准尺寸，每次打开从基准重算
        //（内容放不下时面板上下对称加高，pivot 0.5 居中长；基准高只记第一次，拉伸不残留）。
        if (_basePanelSize.x <= 0f) _basePanelSize = panelRt.sizeDelta;
        else panelRt.sizeDelta = _basePanelSize;

        // 2026-10-31 主人报「怪物信息排版优化」：Meta 与正文、描述与趣闻两段间距拉开
        // （6→12 / 16→24），正文行距 1→1.12 更透气；只动纵向间距与行距，不动宽度/字号。
        // 【2026-11-04 任务弹窗排版（主人拍板「统一左边界 / 进度不再单独一行 / 奖励贴紧说明 / 收掉空地」）】
        // 任务类弹窗（主线/支线，_taskLayout）：说明独占上部；状态字与奖励行同一 y，
        // 左对齐状态字、右对齐奖励图标，互不重叠；面板按内容收高。
        float loreGap = 24f;            // Desc 与 Lore 的间距（超长时自动压缩）
        float metaGap = 12f;            // Meta 与 Desc 的间距
        float rewardGapAbove = 10f;     // Desc 与奖励行间距
        float rewardGapBelow = 20f;     // 奖励行与 Lore 间距
        bool task = _taskLayout;
        if (task)
        {
            loreGap = 0f;               // 进度与说明同一行，不再单独占一行
            rewardGapAbove = 22f;       // 说明（含进度行）与奖励行的统一间距
            rewardGapBelow = 0f;
        }

        // 顶部起点 = Meta 框底 + 间距（预制体原值 -360 与 Meta 框底 -366 有 6px 重叠）
        float top = -372f;
        var metaRt = metaText != null ? metaText.rectTransform : null;
        if (metaRt != null)
        {
            // 2026-11-01 排版优化：Meta 空（任务/世界弹窗）时不再占整格，desc 贴 meta 框顶下 8px
            // 【2026-11-03 主人「任务名字和详情分开一点」】任务弹窗标题到详情原本只隔 21px，太挤；
            // 改 34px（标题底到详情顶 ≈ 47px），标题与正文分成两块。
            top = string.IsNullOrEmpty(metaText.text)
                ? metaRt.anchoredPosition.y - (task ? 34f : 8f)
                : metaRt.anchoredPosition.y - metaRt.sizeDelta.y - metaGap;
        }

        // 底部红线：关闭按钮（y=28 + 高 52）上方留 12px；有解锁按钮时抬高红线
        float bottomPad = unlockButton != null && unlockButton.gameObject.activeSelf ? 164f : 92f;
        float bottomLimit = -panelRt.rect.height + bottomPad;

        // 首次记录基准字号；之后每次先恢复，避免上一次降档残留。
        // 【2026-10-31 主人报「这个字可以再大点」】基准在预制体字号上各 +4（22→26 / 20→24）。
        // 【2026-11-03 主人报「有的字大小要统一」】预制体 Desc=22、Lore=20，+4 后变成 26/24 两个号，
        // 同一块正文大小不一 → 基准统一取两者较大值 +4（26），Desc/Lore 从此同号。
        if (_baseDescSize <= 0)
        {
            _baseDescSize = Mathf.Max(descText.fontSize, loreText.fontSize) + 4;
            _baseLoreSize = _baseDescSize;
        }
        descText.fontSize = _baseDescSize;
        loreText.fontSize = _baseLoreSize;
        // 【2026-11-03 字号层级统一】副信息 Meta 预制体 20 比正文小一圈显得突兀，统一到 22
        //（层级：头条 35 → 名称 32 → 正文 26 → 副信息 22）。只改字号，颜色/框/对齐不动。
        if (metaText != null) metaText.fontSize = 22;
        // 2026-10-31 排版优化：正文行距放宽（每次打开重设，无需恢复基准）
        descText.lineSpacing = 1.12f;
        loreText.lineSpacing = 1.12f;

        // 文本高度按预制体给的宽度算（preferredHeight 自动取 rect 宽，代码不写宽度）
        float descH = descText.preferredHeight;
        float loreH = loreText.preferredHeight;

        // 【2026-11-04 对齐规则（主人「统一左边界」）】任务弹窗：说明靠左、进度靠右，同一行两条边
        // 都对齐到预制体给定的 450 宽内区（560 面板两侧各留 55，左右对称）；同字号同基线，行高取较大者。
        // 世界图鉴这类长正文保持预制体左对齐（长段落居中难读）。只切对齐方式，不碰宽度/位置。
        if (!_baseDescAlign.HasValue) _baseDescAlign = descText.alignment;
        if (!_baseLoreAlign.HasValue) _baseLoreAlign = loreText.alignment;
        descText.alignment = task ? TextAnchor.UpperLeft : _baseDescAlign.Value;
        loreText.alignment = task ? TextAnchor.UpperLeft : _baseLoreAlign.Value;

        // 奖励图标行高度（active 才占位）
        float rewardH = 0f;
        if (_rewardRow != null && _rewardRow.activeSelf && _rewardRowRt != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rewardRowRt);
            rewardH = _rewardRowRt.rect.height;
        }
        float rewardBlock = rewardH > 0f ? rewardGapAbove + rewardH + rewardGapBelow : 0f;
        // 【2026-11-04】任务弹窗里说明与进度同一行，行高取两者较大者，Lore 不再额外占一段高度。
        // 进度为空（推进条件已达成、无需显示 x/y）时按 0 高处理，避免空行把行高撑大。
        bool loreBlank = string.IsNullOrEmpty(loreText.text);
        if (task && loreBlank) loreH = 0f;
        float rowH = task ? Mathf.Max(descH, loreH) : descH;

        float avail = top - bottomLimit;
        // 2026-11-01 主人拍板「条的位置不够可以上下拉伸」：内容放不下先把面板上下加高
        //（上限近满屏，参考高 1280 留 100 边）；顶栏内容锚顶、关闭/解锁钮锚底，跟着边走不压字。
        float need = task ? rowH + rewardBlock : rowH + rewardBlock + loreGap + loreH;
        const float maxPanelH = 1180f;
        // 【2026-11-03 排版修复（主人报支线任务弹窗排版）】收高下限 560 写死 → 任务/世界类
        // 3 行内容收不满，底部空一大块。改按内容动态算：内容起点距顶边(-top) + 内容 need
        // + 底部避让 bottomPad + 24 呼吸；420 绝对下限兜底。
        float minPanelH = Mathf.Max(420f, -top + need + bottomPad + 24f);
        if (need > avail)
        {
            float grow = Mathf.Min(need - avail, Mathf.Max(0f, maxPanelH - panelRt.rect.height));
            if (grow > 0f)
            {
                panelRt.sizeDelta = new Vector2(panelRt.sizeDelta.x, panelRt.sizeDelta.y + grow);
                bottomLimit = -panelRt.rect.height + bottomPad;
                avail = top - bottomLimit;
            }
        }
        else if (need < avail)
        {
            // 2026-11-01 排版优化：内容少时面板对称收高（pivot 0.5 居中缩），底部不再空一大块；
            // 下限 minPanelH 防收过头挤到标题/关闭钮。
            float shrink = Mathf.Min(avail - need, Mathf.Max(0f, panelRt.rect.height - minPanelH));
            if (shrink > 0f)
            {
                panelRt.sizeDelta = new Vector2(panelRt.sizeDelta.x, panelRt.sizeDelta.y - shrink);
                bottomLimit = -panelRt.rect.height + bottomPad;
                avail = top - bottomLimit;
            }
        }
        // 【2026-11-03 排版修复】top 头条跟随面板高度：头条锚在面板中心上方固定 291（按 720
        // 面板调的），面板收高后顶边升到 280 以上，头条字（高 50）骑出顶边框（截图实锤）。
        // 改为永远保持「顶边下 69px」，与怪物信息弹窗（720 面板）观感一致；程序搭壳无 top 节点，判空跳过。
        if (root != null && _topNode == null)
            _topNode = FindDeep(root.transform, "top") as RectTransform;
        if (_topNode != null)
            _topNode.anchoredPosition = new Vector2(
                _topNode.anchoredPosition.x, panelRt.sizeDelta.y * 0.5f - TopTitleInset);
        if (need > avail)
        {
            // 降档阶梯随新基准同步上移（原 19/17/16、18/16/15）。
            // 【2026-11-03 字号统一】Desc/Lore 原本两套阶梯（23/21/19 vs 21/19/17），降档后又会
            // 差一号；改为一套阶梯同时套两个节点，任何档位都同号。
            int[] steps = { 23, 21, 19 };
            for (int i = 0; i < steps.Length; i++)
            {
                descText.fontSize = steps[i];
                loreText.fontSize = steps[i];
                descH = descText.preferredHeight;
                loreH = loreText.preferredHeight;
                if (task && loreBlank) loreH = 0f;
                rowH = task ? Mathf.Max(descH, loreH) : descH;
                need = task ? rowH + rewardBlock : rowH + rewardBlock + loreGap + loreH;
                if (need <= avail) break;
            }
            if (!task && need > avail)
                loreGap = Mathf.Max(4f, avail - descH - rewardBlock - loreH);
        }

        // 【2026-11-03 排版修复】内容在可用区内垂直居中：内容少时上下留白对称，不再全挤
        // 在上半截、底部空一大块。偏移后内容底 = bottomLimit + slack/2，永不越底部红线。
        // 任务弹窗（task）不居中：说明/进度/奖励贴顶部成一组，面板已按内容收高（底部只留 24 呼吸）。
        float slack = avail - need;
        if (slack > 0f && !task)
            top -= slack * 0.5f;

        // 只动 y：宽度、x、sizeDelta 一律保留预制体原值
        descRt.anchoredPosition = new Vector2(descRt.anchoredPosition.x, top);
        if (_rewardRowRt != null && _rewardRow != null && _rewardRow.activeSelf)
        {
            // 【2026-11-04 主人「奖励行贴紧说明」】奖励行紧跟说明（含进度行）下方，间距统一 22；
            // 同一左边界：任务弹窗胶左对齐（行宽 = Desc 宽 450，左边缘与说明文字左边缘重合）。
            _rewardRowRt.anchoredPosition = new Vector2(
                _rewardRowRt.anchoredPosition.x, top - rowH - rewardGapAbove);
            var hlg = _rewardRow.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null) hlg.childAlignment = task ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter;
        }
        // 任务弹窗：状态字与奖励行同一 y，左对齐状态字、右对齐奖励图标，互不重叠
        float loreY = task ? top - rowH - rewardGapAbove : top - descH - rewardBlock - loreGap;
        loreRt.anchoredPosition = new Vector2(loreRt.anchoredPosition.x, loreY);
    }

    /// <summary>编辑器生成 / 运行时兜底共用。</summary>
    public static void BuildHierarchy(GameObject host)
    {
        var canvas = host.GetComponent<Canvas>() ?? host.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.BattleLegacyChoose);
        if (host.GetComponent<GraphicRaycaster>() == null)
            host.AddComponent<GraphicRaycaster>();
        if (host.GetComponent<CodexInfoPopupUI>() == null)
            host.AddComponent<CodexInfoPopupUI>();

        var root = CreateUi(host.transform, "Root", true);
        Stretch(root);

        var dim = CreateUi(root, "Dim", true);
        Stretch(dim);
        var dimImg = dim.gameObject.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.55f);
        dim.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;

        var panelRt = CreateUi(root, "Panel", true);
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(560f, 720f);
        var panelImg = panelRt.gameObject.AddComponent<Image>();
        panelImg.color = new Color(0.16f, 0.12f, 0.1f, 0.98f);

        var frame = CreateUi(panelRt, "PortraitFrame", true);
        frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 1f);
        frame.pivot = new Vector2(0.5f, 1f);
        frame.anchoredPosition = new Vector2(0f, -36f);
        frame.sizeDelta = new Vector2(220f, 220f);
        frame.gameObject.AddComponent<Image>().color = new Color(0.3f, 0.22f, 0.16f, 1f);

        var portraitRt = CreateUi(frame, "Portrait", true);
        Stretch(portraitRt);
        portraitRt.offsetMin = new Vector2(10f, 10f);
        portraitRt.offsetMax = new Vector2(-10f, -10f);
        var pImg = portraitRt.gameObject.AddComponent<Image>();
        pImg.color = new Color(1f, 1f, 1f, 0.2f);
        pImg.preserveAspect = true;

        AddLabel(panelRt, "Title", "名称", 32, new Vector2(0f, -280f), new Vector2(500f, 44f));
        AddLabel(panelRt, "Meta", "类型 · 地点", 20, new Vector2(0f, -330f), new Vector2(500f, 36f));
        AddLabel(panelRt, "Desc", "描述", 22, new Vector2(0f, -430f), new Vector2(500f, 140f), TextAnchor.UpperLeft);
        AddLabel(panelRt, "Lore", "补充", 20, new Vector2(0f, -560f), new Vector2(500f, 100f), TextAnchor.UpperLeft);

        var close = CreateUi(panelRt, "CloseButton", true);
        close.anchorMin = close.anchorMax = new Vector2(0.5f, 0f);
        close.pivot = new Vector2(0.5f, 0f);
        close.anchoredPosition = new Vector2(0f, 28f);
        close.sizeDelta = new Vector2(200f, 52f);
        close.gameObject.AddComponent<Image>().color = new Color(0.42f, 0.3f, 0.18f, 1f);
        close.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
        AddLabel(close, "Label", "关闭", 24, Vector2.zero, new Vector2(200f, 52f), TextAnchor.MiddleCenter, stretch: true);

        GameFonts.ApplyToHierarchy(host.transform);
    }

    static RectTransform CreateUi(Transform parent, string name, bool withRect)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static Text AddLabel(Transform parent, string name, string text, int size, Vector2 pos, Vector2 sizeDelta,
        TextAnchor align = TextAnchor.MiddleCenter, bool stretch = false)
    {
        var rt = CreateUi(parent, name, true);
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
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static Transform FindDeep(Transform root, string name)
    {
        // 2026-10-28 收敛：实现已统一到 UiFind/UiFactory 唯一真源，此处保留私有签名以免动调用点
        return UiFind.Deep(root, name);
    }
}
