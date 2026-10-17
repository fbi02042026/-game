using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 进裂隙前职业三选一：优先加载 Prefabs/UI/PlayerJobSelect；缺则运行时白模。
/// </summary>
public class PlayerJobSelectUI : MonoBehaviour
{
    public const string PrefabResourcePath = "Prefabs/UI/PlayerJobSelect";

    public static PlayerJobSelectUI Instance { get; private set; }

    public struct Options
    {
        public bool TutorialForceSwordFirst;
        public bool RequireSwordShield;
    }

    struct CardBind
    {
        public Button Button;
        public Image Frame;
        public Image Icon;
        public Text Name;
        public Text Desc;
        public Text HpStat;
        public Text AtkStat;
        public Text ControlStat;
        public Transform RatingRoot;
        public CanvasGroup DimGroup;
        public Outline SelectOutline;
    }

    Action _onEnterRift;
    Options _opts;
    GameObject _root;
    Button _refreshBtn;
    Button _enterBtn;
    Button _closeBtn;
    Text _hint;
    Text _refreshLabel;
    Text _title;
    readonly List<CardBind> _cards = new List<CardBind>();
    readonly List<PlayerJobId> _offer = new List<PlayerJobId>();
    PlayerJobId? _selected;
    bool _refreshUsed;
    bool _built;
    bool _fromPrefab;

    public static void Show(Action onEnterRift, Options opts = default)
    {
        Ensure().Open(onEnterRift, opts);
    }

    static PlayerJobSelectUI Ensure()
    {
        if (Instance != null) return Instance;

        var prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab != null)
        {
            var inst = Instantiate(prefab);
            inst.name = "PlayerJobSelectUI";
            DontDestroyOnLoad(inst);
            // Missing Script 时 GetComponent 为 null，补挂即可按节点名绑定
            var ui = inst.GetComponent<PlayerJobSelectUI>();
            if (ui == null) ui = inst.AddComponent<PlayerJobSelectUI>();
            ui._fromPrefab = HasJobSelectHierarchy(inst.transform);
            return ui;
        }

        var go = new GameObject("PlayerJobSelectUI");
        DontDestroyOnLoad(go);
        return go.AddComponent<PlayerJobSelectUI>();
    }

    static bool HasJobSelectHierarchy(Transform root)
    {
        if (root == null) return false;
        return FindDeep(root, "Panel") != null && FindDeep(root, "JobCard0") != null;
    }

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Editor 白模生成器用：搭建完整层级（含本组件挂在根上）。</summary>
    public void BuildHierarchyForPrefab()
    {
        _fromPrefab = true;
        BuildRuntimeWhitebox(isPrefabBuild: true);
        BindFromHierarchy();
        GameFonts.ApplyToHierarchy(transform);
    }

    void Open(Action onEnterRift, Options opts)
    {
        _onEnterRift = onEnterRift;
        _opts = opts;
        _selected = null;
        _refreshUsed = false;
        BuildIfNeeded();
        // 【2026-10-17 主人报「弹窗有几率变大」】画布参数（绑定相机 + CanvasScaler）只在创建时套过一次，
        // 场景切换的 RefreshDdolCanvases 只换 worldCamera 不重套缩放，城镇/战斗相机来回切后
        // 弹窗会带着烙死的旧映射渲染整块变大。按 UICanvasSetup.RefreshPopup 的既有口径
        // （「DDOL 弹窗每次 Show/Open 时调用」）每次开界面重套一遍，与 BattleSettlementUI 等一致。
        var popupCanvas = GetComponent<Canvas>();
        if (popupCanvas != null)
            UICanvasSetup.RefreshPopup(popupCanvas, GameConfig.UiSort.TownPopup);
        // 2026-10-15 主人拍板 每次开界面都补一次右上角关闭按钮 —— 机理：预制体路径和运行时白模路径都在 BuildIfNeeded 之后汇合到这一行，
        // 所以无论走哪条构建路线都一定有关闭按钮。
        EnsureCloseButton(FindDeep(transform, "Panel") ?? transform);
        RollOffers(forceSwordFirst: opts.TutorialForceSwordFirst);
        RefreshCardsVisual();
        UpdateEnterInteractable();

        bool tut = opts.TutorialForceSwordFirst;   // 下面 ShowTutorialPickSword 还要用，别删

        // 【2026-10-11 主人拍板】职业池收到 3 个（剑盾/法师/游侠）后，刷新只能换顺序、刷不出新东西
        // → 刷新按钮**整颗隐藏**（主人原话「把那个刷新按钮隐藏」）。旧口径是引导局才隐藏。
        if (_refreshBtn != null)
        {
            _refreshBtn.gameObject.SetActive(false);
            _refreshBtn.interactable = false;
        }

        // 手做 prefab：绝不碰 Hint（字号/字体/文案），也不整树套字体
        if (!_fromPrefab)
        {
            if (_hint != null)
                _hint.text = "请选择你想成为的职业，踏入裂隙，迎接挑战！";
            if (_title != null)
                _title.text = "选择职业";
        }

        if (_root != null)
        {
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
        }
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        if (!_fromPrefab)
            GameFonts.ApplyToHierarchy(transform);
        TownPageDim.Ensure(transform);

        // 【2026-10-17 主人拍板】引导局强制选剑盾的硬提示必须弹出——
        // 先把 FirstRunGuide 还盖着的「开始冒险」气泡暂收（FirstBattle 步骤未完成，
        // 协程继续等进战斗，之后 CoTalent 会重新弹气泡），否则让位逻辑会把硬提示吞掉
        if (tut && FirstRunGuide.Instance != null && !FirstRunGuide.Instance.IsFinished)
            FirstRunGuide.Instance.Hide();

        if (tut)
            ShowTutorialPickSword();
    }

    public void Hide()
    {
        if (_opts.TutorialForceSwordFirst)
        {
            TutorialHintUI.Instance?.Hide();
            // 【2026-10-17】取消路径（没进裂隙就关掉界面）把 FirstRunGuide 气泡请回来：
            // Open 时暂收的是「点开始冒险」气泡，FirstBattle 步骤还在等进战斗，
            // 不请回的话玩家关掉界面就失去指引。进裂隙路径 OnEnterRift 已先摘回调，不会走到这。
            if (_onEnterRift != null && FirstRunGuide.Instance != null
                && !FirstRunGuide.Instance.IsFinished)
                FirstRunGuide.Instance.NudgeReshow();
        }
        if (_root != null) _root.SetActive(false);
        else gameObject.SetActive(false);
    }

    void ShowTutorialPickSword()
    {
        // 【2026-10-15 主人报「上面一个引导 下面一个引导」】首次强制软引导（FirstRunGuide.FirstBattle）
        // 的气泡实际显示在屏上时，这条旧硬提示让位——同一个位置只准一层指引。
        // 【2026-10-17 主人拍板】判断从「步骤未完成」改成「气泡实际可见」：职业选择弹出时
        // Open() 已把气泡暂收，步骤虽然没完成，但屏上没有别的指引了，硬提示必须弹出来。
        if (FirstRunGuide.Instance != null && FirstRunGuide.Instance.IsOverlayVisible) return;
        RectTransform target = null;
        if (_cards.Count > 0 && _cards[0].Button != null)
            target = _cards[0].Button.transform as RectTransform;
        if (target == null) return;
        TutorialHintUI.Ensure().ShowHard("请选择剑盾卫士。", target);
    }

    void ShowTutorialEnterRift()
    {
        // 同上（2026-10-17 口径）：FirstRunGuide 气泡实际显示在屏上时才让位，
        // 只判「步骤未完成」会把引导局的硬提示整个吞掉（FirstBattle 到进战斗才算完成）。
        if (FirstRunGuide.Instance != null && FirstRunGuide.Instance.IsOverlayVisible) return;
        if (_enterBtn == null) return;
        var target = _enterBtn.transform as RectTransform;
        if (target == null) return;
        TutorialHintUI.Ensure().ShowHard("再点「进入裂隙」。", target);
    }

    /// <summary>
    /// 右上角关闭按钮：点一下等于 <see cref="Hide"/>，整个选择职业界面收掉。
    /// </summary>
    void EnsureCloseButton(Transform panel)
    {
        // 2026-10-15 主人拍板 预制体/层级里已经有 CloseBtn 就复用那颗，没有才由代码现场建 ——
        // 机理：美术摆好的节点优先，跑的是「缺了就自己生一个」，不是拿 null 兜底一条链。
        // 【2026-10-17】主人已在预制体里亲手摆好 CloseBtn（自带图标 / 缩放 0.3 / 自定位置）——
        // 手摆的只绑点击 + 引导局显隐，位置 / 尺寸 / 颜色 / 「×」字一概不碰；
        // 旧逻辑会把美术资源改坏：挪回右上角缩成 72×72（再乘手摆的 0.3 缩放只剩 ~22 像素）、
        // Image 染成紫褐底、图标上再叠印一个「×」。样式只由代码白模路径负责。
        var closeGo = FindDeep(panel, "CloseBtn");
        bool handPlaced = closeGo != null;
        if (closeGo == null)
        {
            closeGo = CreateUi("CloseBtn", panel.transform, typeof(Image), typeof(Button)).transform;

            // 2026-10-15 主人拍板 右上角定位：锚点和轴心都钉在右上，sizeDelta 定死 72×72，再往左下各缩 16 ——
            // 机理：锚在角上，面板怎么拉伸它都死死贴住右上角，不会被职业卡挤走。
            var rt = closeGo.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(72f, 72f);
            rt.anchoredPosition = new Vector2(-16f, -16f);

            // 2026-10-15 主人拍板 底图跟 RefreshBtn 同一套紫褐色 —— 机理：同色系读起来是一组界面控件，不抢中间职业卡的视觉重心。
            closeGo.GetComponent<Image>().color = new Color(0.35f, 0.28f, 0.42f, 1f);

            // 2026-10-15 主人拍板 复用节点底下已经有 Label 就不再建 —— 机理：重复建会是两个「×」叠印糊成一坨黑。
            if (FindText(closeGo, "Label") == null)
            {
                var label = CreateText(closeGo, "Label", "×", 34, TextAnchor.MiddleCenter);
                Stretch(label.rectTransform);
            }
        }

        // 2026-10-15 主人拍板 先把旧监听清干净再挂 —— 机理：每次 Open 都会走到这里，不去重就会一次点击触发多次 Hide。
        _closeBtn = closeGo.GetComponent<Button>() ?? closeGo.gameObject.AddComponent<Button>();
        _closeBtn.onClick.RemoveAllListeners();
        _closeBtn.onClick.AddListener(Hide);

        // 2026-10-15 主人拍板 排到最后画 —— 机理：挂在面板最上层，遮不住也点得到（不会被职业卡盖在下面失效）。
        // 【2026-10-17】只对代码现建的生效；手摆的尊重预制体里的顺序，不乱动层级。
        if (!handPlaced)
            closeGo.SetAsLastSibling();

        // 2026-10-15 主人拍板 引导局（强制先选剑盾那次）不显示这颗按钮 ——
        // 机理：引导局必须选了职业才能往下走，此时关掉界面会把引导卡死在中途，所以引导局直接不让它出现。
        if (_opts.TutorialForceSwordFirst)
            closeGo.gameObject.SetActive(false);
        else
            closeGo.gameObject.SetActive(true);
    }

    void BuildIfNeeded()
    {
        if (_built) return;
        _built = true;

        if (_fromPrefab || transform.Find("Dim") != null || transform.Find("Panel") != null)
        {
            BindFromHierarchy();
            EnsureCanvas();
            return;
        }

        BuildRuntimeWhitebox(isPrefabBuild: false);
        BindFromHierarchy();
    }

    void EnsureCanvas()
    {
        var canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();
        }
        if (gameObject.GetComponent<CanvasScaler>() == null)
            gameObject.AddComponent<CanvasScaler>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownPopup);
    }

    void BindFromHierarchy()
    {
        _cards.Clear();
        EnsureCanvas();

        _root = gameObject;
        var panel = FindDeep(transform, "Panel") ?? transform;
        _title = FindText(panel, "Title");
        _hint = FindText(panel, "Hint");

        var refreshGo = FindDeep(panel, "RefreshBtn");
        if (refreshGo != null)
        {
            _refreshBtn = refreshGo.GetComponent<Button>() ?? refreshGo.gameObject.AddComponent<Button>();
            _refreshBtn.onClick.RemoveAllListeners();
            _refreshBtn.onClick.AddListener(OnRefresh);
            _refreshLabel = FindText(refreshGo, "Label");
        }

        var enterGo = FindDeep(panel, "EnterRiftBtn");
        if (enterGo != null)
        {
            _enterBtn = enterGo.GetComponent<Button>() ?? enterGo.gameObject.AddComponent<Button>();
            _enterBtn.onClick.RemoveAllListeners();
            _enterBtn.onClick.AddListener(OnEnterRift);
        }

        for (int i = 0; i < 3; i++)
        {
            var cardTf = FindDeep(panel, "JobCard" + i);
            if (cardTf == null) continue;
            var bind = new CardBind
            {
                Button = cardTf.GetComponent<Button>() ?? cardTf.gameObject.AddComponent<Button>(),
                Frame = FindImage(cardTf, "Frame") ?? cardTf.GetComponent<Image>(),
                Icon = FindImage(cardTf, "Icon"),
                Name = FindText(cardTf, "Name"),
                Desc = FindText(cardTf, "Desc"),
                HpStat = FindStatLineText(cardTf, "血量"),
                AtkStat = FindStatLineText(cardTf, "攻击"),
                ControlStat = FindStatLineText(cardTf, "操控"),
                RatingRoot = FindDeep(cardTf, "Rating")
            };
            // 兼容旧运行时白模 Label / 整块 Stats
            if (bind.Name == null) bind.Name = FindText(cardTf, "Label");
            if (bind.HpStat == null && bind.AtkStat == null && bind.ControlStat == null)
            {
                var legacy = FindText(cardTf, "Stats");
                if (legacy != null) bind.HpStat = legacy;
            }
            EnsureCardSelectFx(ref bind, cardTf);
            int idx = i;
            bind.Button.onClick.RemoveAllListeners();
            bind.Button.onClick.AddListener(() => OnCardClicked(idx));
            _cards.Add(bind);
        }
    }

    /// <summary>
    /// 手做卡的一行属性：行节点（名字=「血量 / 攻击 / 操控」）下面的 Text。
    ///
    /// <para>⚠ 2026-10-05 根因：预制体里这三个行节点是<b>图标大小</b>（26×24 / 28×29 / 31×31），
    /// 而它们下面的 <c>Stats</c> Text 是 <c>sizeDelta 0×0 + anchor 0~1 拉伸</c> →
    /// 文字被压进图标框里，配上 <c>Wrap + Truncate</c>，「血量 高」这种 4 个字被折行截断，
    /// 看着就像「没绑定数据 / 显示不对」。</para>
    ///
    /// <para>数据其实<b>一直在绑</b>。真正的病是那个畸形的小框。本方法只做一件兜底：
    /// 把 Text 的横向溢出改成 <see cref="HorizontalWrapMode.Overflow"/>，
    /// 让文字按原样完整画出来 —— <b>不动任何位置 / 尺寸 / 字体 / 颜色</b>（那些是美术的），
    /// 也不改预制体。要根治请主人在团结里把三个行节点的框拉宽（见汇报）。</para>
    /// </summary>
    static Text FindStatLineText(Transform card, string rowName)
    {
        var row = FindDeep(card, rowName);
        if (row == null) return null;
        var nested = FindText(row, "Stats");
        if (nested != null) return RelaxTextClipping(nested);
        var label = FindText(row, "Label");
        if (label != null) return RelaxTextClipping(label);
        return RelaxTextClipping(row.GetComponent<Text>());
    }

    /// <summary>
    /// 文字被塞进过小的框时，允许它溢出绘制（只放宽 overflow，不改 sizeDelta / 位置）。
    /// 用于属性行那种「框只有图标大小」的预制体结构。
    /// </summary>
    static Text RelaxTextClipping(Text t)
    {
        if (t == null) return null;
        var rt = t.rectTransform;
        if (rt == null) return t;
        // 只在框确实装不下时才放宽；框够大就完全不碰（保持美术的换行设置）
        float needW = t.fontSize * (t.text != null ? t.text.Length : 0) + 8f;   // 粗略：一个全角字 ≈ 一个 fontSize
        if (rt.rect.width >= needW) return t;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    void BuildRuntimeWhitebox(bool isPrefabBuild)
    {
        EnsureCanvas();

        Transform host = transform;
        // 预制体根即 PlayerJobSelect；运行时补 Root 亦可直接挂 Dim/Panel
        var dim = CreateUi("Dim", host, typeof(Image));
        Stretch(dim.GetComponent<RectTransform>());
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);
        dim.GetComponent<Image>().raycastTarget = true;

        var panel = CreateUi("Panel", host, typeof(Image));
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(680f, 980f);
        panel.GetComponent<Image>().color = new Color(0.78f, 0.68f, 0.48f, 0.98f);

        var title = CreateText(panel.transform, "Title", "选择职业", 36, TextAnchor.MiddleCenter);
        AnchorTop(title.rectTransform, -28f, 600f, 44f);
        title.color = new Color(0.35f, 0.22f, 0.08f);

        var hint = CreateText(panel.transform, "Hint",
            "请选择你想成为的职业，踏入裂隙，迎接挑战！", 18, TextAnchor.MiddleCenter);
        AnchorTop(hint.rectTransform, -78f, 600f, 48f);
        hint.color = new Color(0.35f, 0.28f, 0.18f);
        hint.horizontalOverflow = HorizontalWrapMode.Wrap;

        var cardRow = CreateUi("CardRow", panel.transform, typeof(RectTransform)).transform;
        var rowRt = cardRow.GetComponent<RectTransform>();
        rowRt.anchorMin = rowRt.anchorMax = new Vector2(0.5f, 0.5f);
        rowRt.sizeDelta = new Vector2(640f, 520f);
        rowRt.anchoredPosition = new Vector2(0f, 40f);

        float cardW = 196f;
        float gap = 14f;
        float startX = -((cardW + gap));
        for (int i = 0; i < 3; i++)
        {
            var card = CreateUi("JobCard" + i, cardRow, typeof(Image), typeof(Button));
            var crt = card.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(cardW, 500f);
            crt.anchoredPosition = new Vector2(startX + i * (cardW + gap), 0f);
            card.GetComponent<Image>().color = new Color(0.92f, 0.86f, 0.72f, 1f);

            var frame = CreateUi("Frame", card.transform, typeof(Image));
            Stretch(frame.GetComponent<RectTransform>());
            frame.GetComponent<Image>().color = new Color(0.45f, 0.32f, 0.18f, 0.35f);
            frame.GetComponent<Image>().raycastTarget = false;

            var icon = CreateUi("Icon", card.transform, typeof(Image));
            var irt = icon.GetComponent<RectTransform>();
            irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 1f);
            irt.pivot = new Vector2(0.5f, 1f);
            irt.sizeDelta = new Vector2(140f, 140f);
            irt.anchoredPosition = new Vector2(0f, -24f);
            icon.GetComponent<Image>().color = new Color(0.55f, 0.5f, 0.42f, 1f);
            icon.GetComponent<Image>().raycastTarget = false;

            var name = CreateText(card.transform, "Name", "职业", 24, TextAnchor.MiddleCenter);
            var nrt = name.rectTransform;
            nrt.anchorMin = nrt.anchorMax = new Vector2(0.5f, 1f);
            nrt.pivot = new Vector2(0.5f, 1f);
            nrt.sizeDelta = new Vector2(180f, 36f);
            nrt.anchoredPosition = new Vector2(0f, -176f);
            name.color = new Color(0.2f, 0.12f, 0.06f);
            name.fontStyle = FontStyle.Bold;

            var statsRoot = CreateUi("Stats", card.transform, typeof(RectTransform)).transform;
            var srootRt = statsRoot.GetComponent<RectTransform>();
            srootRt.anchorMin = srootRt.anchorMax = new Vector2(0.5f, 1f);
            srootRt.pivot = new Vector2(0.5f, 1f);
            srootRt.sizeDelta = new Vector2(180f, 100f);
            srootRt.anchoredPosition = new Vector2(0f, -220f);
            CreateStatRow(statsRoot, "血量", "血量 中", 0f);
            CreateStatRow(statsRoot, "攻击", "攻击 中", -32f);
            CreateStatRow(statsRoot, "操控", "操控 中", -64f);

            var desc = CreateText(card.transform, "Desc", "描述", 15, TextAnchor.UpperCenter);
            var drt = desc.rectTransform;
            drt.anchorMin = new Vector2(0.05f, 0.12f);
            drt.anchorMax = new Vector2(0.95f, 0.48f);
            drt.offsetMin = Vector2.zero;
            drt.offsetMax = Vector2.zero;
            desc.color = new Color(0.28f, 0.22f, 0.14f);
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            desc.verticalOverflow = VerticalWrapMode.Truncate;

            var rating = CreateUi("Rating", card.transform, typeof(RectTransform)).transform;
            var rrt = rating.GetComponent<RectTransform>();
            rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0f);
            rrt.pivot = new Vector2(0.5f, 0f);
            rrt.sizeDelta = new Vector2(160f, 32f);
            rrt.anchoredPosition = new Vector2(0f, 16f);
            for (int s = 1; s <= 3; s++)
            {
                var full = CreateUi("满星" + s, rating, typeof(Image));
                var frt = full.GetComponent<RectTransform>();
                frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0.5f);
                frt.sizeDelta = new Vector2(28f, 28f);
                frt.anchoredPosition = new Vector2((s - 2) * 34f, 0f);
                full.GetComponent<Image>().color = new Color(0.95f, 0.75f, 0.2f, 1f);
                var empty = CreateUi("空星", full.transform, typeof(Image));
                Stretch(empty.GetComponent<RectTransform>());
                empty.GetComponent<Image>().color = new Color(0.45f, 0.4f, 0.35f, 0.85f);
            }
        }

        var refreshGo = CreateUi("RefreshBtn", panel.transform, typeof(Image), typeof(Button));
        var refrt = refreshGo.GetComponent<RectTransform>();
        refrt.anchorMin = refrt.anchorMax = new Vector2(0.5f, 0f);
        refrt.pivot = new Vector2(0.5f, 0f);
        refrt.sizeDelta = new Vector2(180f, 52f);
        refrt.anchoredPosition = new Vector2(-150f, 40f);
        refreshGo.GetComponent<Image>().color = new Color(0.4f, 0.32f, 0.5f, 1f);
        var refreshLabel = CreateText(refreshGo.transform, "Label", "刷新 (1)", 22, TextAnchor.MiddleCenter);
        Stretch(refreshLabel.rectTransform);

        var enterGo = CreateUi("EnterRiftBtn", panel.transform, typeof(Image), typeof(Button));
        var ert = enterGo.GetComponent<RectTransform>();
        ert.anchorMin = ert.anchorMax = new Vector2(0.5f, 0f);
        ert.pivot = new Vector2(0.5f, 0f);
        ert.sizeDelta = new Vector2(280f, 72f);
        ert.anchoredPosition = new Vector2(110f, 36f);
        enterGo.GetComponent<Image>().color = new Color(0.45f, 0.22f, 0.55f, 1f);
        var enterLabel = CreateText(enterGo.transform, "Label", "进入裂隙", 28, TextAnchor.MiddleCenter);
        Stretch(enterLabel.rectTransform);
        enterLabel.fontStyle = FontStyle.Bold;

        if (isPrefabBuild)
            _built = true;
        _ = isPrefabBuild;
    }

    void RollOffers(bool forceSwordFirst)
    {
        _offer.Clear();
        // 【2026-10-11 主人拍板】只从**开放职业**里出牌（剑盾 / 法师 / 游侠），
        // 不再从 PlayerJobDefs.All 全量池里随机 —— 单一出口：要开/关职业改 PlayerJobDefs.Playable。
        var pool = new List<PlayerJobId>();
        for (int i = 0; i < PlayerJobDefs.Playable.Length; i++)
            pool.Add(PlayerJobDefs.Playable[i]);

        int want = Mathf.Min(3, pool.Count);
        if (forceSwordFirst)
        {
            _offer.Add(PlayerJobId.SwordShield);
            pool.Remove(PlayerJobId.SwordShield);
            Shuffle(pool);
            for (int i = 0; i < want - 1 && i < pool.Count; i++)
                _offer.Add(pool[i]);
        }
        else
        {
            Shuffle(pool);
            for (int i = 0; i < want && i < pool.Count; i++)
                _offer.Add(pool[i]);
        }
        _selected = null;
    }

    static void Shuffle(List<PlayerJobId> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            var tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }

    void OnRefresh()
    {
        if (_refreshUsed || _opts.TutorialForceSwordFirst)
        {
            UIManager.Instance?.ShowToast("本局不能再刷新");
            return;
        }
        _refreshUsed = true;
        RollOffers(forceSwordFirst: false);
        RefreshCardsVisual();
        UpdateEnterInteractable();
        if (_refreshBtn != null) _refreshBtn.interactable = false;
        if (_refreshLabel != null) _refreshLabel.text = "已刷新";
    }

    void OnCardClicked(int index)
    {
        if (index < 0 || index >= _offer.Count) return;
        var job = _offer[index];
        if (_opts.RequireSwordShield && job != PlayerJobId.SwordShield)
        {
            UIManager.Instance?.ShowToast("请先选择剑盾卫士");
            return;
        }
        _selected = job;
        RefreshCardsVisual();
        UpdateEnterInteractable();
        if (_opts.TutorialForceSwordFirst && job == PlayerJobId.SwordShield)
            ShowTutorialEnterRift();
    }

    void RefreshCardsVisual()
    {
        bool hasSel = _selected.HasValue;
        // 2026-09-28 主人要求：推荐角标（tuijian）只给本次三张里星数最高的卡，并列最高则都给。
        int maxStars = 0;
        for (int i = 0; i < _offer.Count && i < _cards.Count; i++)
            maxStars = Mathf.Max(maxStars, PlayerJobDefs.Get(_offer[i]).RecommendStars);
        for (int i = 0; i < _cards.Count; i++)
        {
            if (i >= _offer.Count) continue;
            var def = PlayerJobDefs.Get(_offer[i]);
            var c = _cards[i];
            if (c.Name != null) c.Name.text = def.DisplayName;
            if (c.HpStat != null && c.AtkStat == null && c.ControlStat == null)
            {
                // 旧白模：单块 Stats
                c.HpStat.text = $"血量 {def.HpLabel}\n攻击 {def.AtkLabel}\n操控 {def.DiffLabel}";
            }
            else
            {
                if (c.HpStat != null) c.HpStat.text = $"血量 {def.HpLabel}";
                if (c.AtkStat != null) c.AtkStat.text = $"攻击 {def.AtkLabel}";
                if (c.ControlStat != null) c.ControlStat.text = $"操控 {def.DiffLabel}";
            }
            if (c.Desc != null)
            {
                string body = string.IsNullOrEmpty(def.LongDesc) ? def.Blurb : def.LongDesc;
                c.Desc.text = body;
            }
            ApplyRecommendStars(c.RatingRoot, def.RecommendStars);
            // 推荐角标：预制体每张卡有一颗 tuijian（三颗星左边），默认常开 —— 这里按「星数是否并列最高」控制显隐。
            if (c.Button != null)
            {
                var badge = FindDeep(c.Button.transform, "tuijian");
                if (badge != null)
                    badge.gameObject.SetActive(maxStars > 0 && def.RecommendStars >= maxStars);
            }
            if (c.Icon != null)
            {
                var sp = PlayerJobDefs.TryLoadJobIcon(_offer[i]);
                if (sp != null)
                {
                    c.Icon.sprite = sp;
                    c.Icon.color = Color.white;
                }
                // 无 Resources 图时保留 prefab 已拖 Sprite，不刷色块
            }
            bool sel = hasSel && _selected.Value == _offer[i];
            ApplyCardSelectVisual(c, sel, hasSel);
        }
    }

    static void EnsureCardSelectFx(ref CardBind bind, Transform cardTf)
    {
        if (cardTf == null) return;
        bind.DimGroup = cardTf.GetComponent<CanvasGroup>();
        if (bind.DimGroup == null)
            bind.DimGroup = cardTf.gameObject.AddComponent<CanvasGroup>();

        var outlineHost = bind.Frame != null ? bind.Frame.gameObject : cardTf.gameObject;
        bind.SelectOutline = outlineHost.GetComponent<Outline>();
        if (bind.SelectOutline == null)
            bind.SelectOutline = outlineHost.AddComponent<Outline>();
        bind.SelectOutline.effectColor = new Color(1f, 0.85f, 0.25f, 0.95f);
        bind.SelectOutline.effectDistance = new Vector2(4f, -4f);
        bind.SelectOutline.useGraphicAlpha = true;
        bind.SelectOutline.enabled = false;
    }

    static void ApplyCardSelectVisual(CardBind c, bool selected, bool anySelected)
    {
        if (c.DimGroup != null)
        {
            c.DimGroup.alpha = anySelected && !selected ? 0.4f : 1f;
            // 默认三卡 0.98；有选中时：选中 1.02，另外两张 0.95
            if (!anySelected)
                c.DimGroup.transform.localScale = Vector3.one * 0.98f;
            else
                c.DimGroup.transform.localScale = selected
                    ? Vector3.one * 1.02f
                    : Vector3.one * 0.95f;
        }

        if (c.SelectOutline != null)
            c.SelectOutline.enabled = selected;

        // 选中态只做「描边 + 缩放 + 透明度」，不改 Frame/Button 的颜色，
        // 保留预制体里美术配好的卡面配色。
    }

    static void ApplyRecommendStars(Transform ratingRoot, int stars)
    {
        if (ratingRoot == null) return;
        stars = Mathf.Clamp(stars, 0, 3);
        // 兼容旧 Text Rating
        var legacy = ratingRoot.GetComponent<Text>();
        if (legacy != null && ratingRoot.childCount == 0)
        {
            legacy.text = stars <= 0 ? "◇◇◇" : new string('◆', stars) + new string('◇', 3 - stars);
            return;
        }
        for (int s = 1; s <= 3; s++)
        {
            var full = FindDeep(ratingRoot, "满星" + s);
            if (full == null) continue;
            bool on = s <= stars;
            var fullImg = full.GetComponent<Image>();
            var empty = FindDeep(full, "空星");
            if (empty != null)
            {
                full.gameObject.SetActive(true);
                if (fullImg != null) fullImg.enabled = on;
                empty.gameObject.SetActive(!on);
            }
            else if (fullImg != null)
            {
                full.gameObject.SetActive(true);
                fullImg.enabled = on;
            }
            else
                full.gameObject.SetActive(on);
        }
    }

    static void CreateStatRow(Transform parent, string rowName, string text, float y)
    {
        var row = CreateUi(rowName, parent, typeof(RectTransform)).transform;
        var rrt = row.GetComponent<RectTransform>();
        rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 1f);
        rrt.pivot = new Vector2(0.5f, 1f);
        rrt.sizeDelta = new Vector2(170f, 28f);
        rrt.anchoredPosition = new Vector2(0f, y);
        var t = CreateText(row, "Stats", text, 16, TextAnchor.MiddleLeft);
        Stretch(t.rectTransform);
        t.color = new Color(0.25f, 0.18f, 0.1f);
    }

    void UpdateEnterInteractable()
    {
        if (_enterBtn != null)
            _enterBtn.interactable = _selected.HasValue;
    }

    void OnEnterRift()
    {
        if (!_selected.HasValue)
        {
            UIManager.Instance?.ShowToast("请先选择职业");
            return;
        }
        if (_opts.RequireSwordShield && _selected.Value != PlayerJobId.SwordShield)
        {
            UIManager.Instance?.ShowToast("请先选择剑盾卫士");
            return;
        }

        // 【2026-10-17 主人要求】点「进入裂隙」先弹体力消耗确认（与 StaminaSystem 实时挂钩：
        // 显示当前体力与恢复倒计时，不足时确认按钮置灰），玩家点「进入裂隙」确认后才走
        // 原「定职业 → 收界面 → 回调里扣体力进裂隙」；取消则留在本界面可换职业重选。
        PlayerJobId job = _selected.Value;
        if (_opts.TutorialForceSwordFirst)
            TutorialHintUI.Instance?.Hide();   // 确认弹窗盖着「进入裂隙」按钮，旧硬提示先收
        StaminaConfirmPopupUI.Show(confirmed =>
        {
            if (!confirmed)
            {
                // 取消回到职业选择：引导局把「再点进入裂隙」硬提示重新指上（内部有让位判断）
                if (_opts.TutorialForceSwordFirst)
                    ShowTutorialEnterRift();
                return;
            }
            PlayerJobDefs.SetSelected(job);
            if (_opts.TutorialForceSwordFirst)
                TutorialHintUI.Instance?.Hide();
            var cb = _onEnterRift;
            _onEnterRift = null;   // 【2026-10-17】先摘回调再收界面：Hide 的气泡回弹只服务取消路径
            Hide();
            cb?.Invoke();
        });
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

    static Text FindText(Transform root, string name)
    {
        var t = FindDeep(root, name);
        return t != null ? t.GetComponent<Text>() : null;
    }

    static Image FindImage(Transform root, string name)
    {
        var t = FindDeep(root, name);
        return t != null ? t.GetComponent<Image>() : null;
    }

    static GameObject CreateUi(string name, Transform parent, params Type[] comps)
    {
        var go = new GameObject(name, comps);
        go.transform.SetParent(parent, false);
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

    static void AnchorTop(RectTransform rt, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(0f, y);
    }
}
