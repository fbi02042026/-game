using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 佣兵好感界面（2026-10-26 主人拍板「佣兵好感界面做好了，你看着进行数据化」；同日晚二次拍板修订）。
///
/// 预制体：<c>Resources/Prefabs/Town/佣兵好感</c>（主人手工搭 2026-10-26）：
/// 佣兵好感(Canvas+ScrollRect+Raycaster) → Panel
/// → Left/头像/Row_0..Row_0(5)：已拥有佣兵槽（prefab 摆 6 格作初始池，运行时套 ScrollRect 可滑动，
///   已拥有超过 6 个时克隆 Row_0 补行；Image+Button+mask/Icon 已摆好；
///   Row 本体 = 头像框，随本格佣兵稀有度换三档：普通银/稀有蓝/传奇金，2026-10-26 晚拍板）
/// → Right/Top/mask/Icon：选中佣兵立绘（半身，GetStand）+【代码建】问题对话框（主人口吻提问，
///   2026-10-27 挪出 Mask：挂 Panel/Right/Top（scale=1），mask 只做裁剪不随稀有度换图）
///   （Top/mask 与 Top/bg = 半身像框，随稀有度换同款三档，2026-10-26 晚拍板）
/// → Right/Top/好感进度/爱心1..6（各带「空心」子节点）：全显示空心（2026-10-26 晚拍板）+【代码建】从左到右进度条
///   （条底用主人好感度素材 Resources/Art/UI/Tavern/好感度/_0001_图层-2，320×77 挂 Panel/Right/Top
///    沿黑条排开；粉色填充按 sizeDelta 涨宽 —— 2026-10-27 美术裁决：Filled 无 sprite 不生效；
///    两者都是本脚本运行时新建的图，不碰 prefab 已摆节点 —— 主人点名要进度条，属必要介入）
/// → Right/Top1/佣兵头像(Iconmask/Icon + namebg/Name)：头像 + 名字
/// → Right/Top1/选中对话/1|2|3（各带 Text (Legacy)）：答案槽（prefab 无 Button，代码补）；
///   分值不展示，每天轮换一个特殊答案，每天 3 次、一次选一个
/// → Right/Middle/panl(Iconbg/Icon + Iconbg/Name)：礼物槽模板（代码按持有礼物横排克隆）
/// → Right/bottom/地名名字 + Info + 战斗按钮(Icon + 羁绊地图 + 地图名字)：羁绊地图入口
///
/// 铁律合规：UICanvasSetup.ApplyPopup 四件套；prefab 已摆节点布局一字不写（铁律 20），新建运行时图
/// （进度条/对话框）用锚点比例摆位属主人点名需求的必要介入；Canvas 补 GraphicRaycaster（铁律 8）；
/// 节点认不出 LogError 不静默（铁律 14）；文案/字体以预制体为准；不造图（立绘取不到保留 prefab 原图）。
///
/// 已知边界（见 Docs/《佣兵好感界面数据化》_待办任务清单.md）：
/// - 左列表可滑动（2026-10-27 主人拍板）：prefab 摆 6 格作初始池，已拥有超过 6 个时克隆 Row_0 补行并滚动；
/// - 礼物区无 ScrollRect，持有礼物过多会横向溢出；
/// - 羁绊地图本体 = 好感满佣兵专属单场关（强制进队 / 固定难度 / 一次性 / ×1.5+钻石，见 BondDungeonBattleMode）。
/// </summary>
public class MercFavorUI : MonoBehaviour
{
    /// <summary>预制体路径 —— 与实际位置必须同值，改一边忘另一边 = 运行时加载不到。</summary>
    public const string PrefabPath = "Prefabs/Town/佣兵好感";

    const string LogTag = "[MercFavorUI] ";

    // —— 节点引用 ——
    Text _nameText;            // Top1/佣兵头像/namebg/Name
    Image _headIcon;           // Top1/佣兵头像/Iconmask/Icon
    Image _standIcon;          // Top/mask/Icon（半身立绘）
    Text[] _talkTexts;         // 选中对话/1..3 的 Text (Legacy)
    Button[] _talkBtns;        // 同节点上的 Button（prefab 没有，代码补）
    Image[] _talkImgs;         // 槽底 Image（禁用压灰用）
    Color _talkImgOrigColor = Color.white;   // 槽底 prefab 原色（Bind 时缓存）
    Image[] _heartFills;       // 爱心1..6 本体（实心图；2026-10-26 晚拍板：常隐藏，进度条接力）
    Image[] _heartEmpties;     // 爱心N/空心（常显示）
    Image _favorFill;          // 【代码建】好感进度条填充（粉，从左到右）
    Text _questionText;        // 【代码建】立绘对话框文本（佣兵的问题）
    Text _mapNameText;         // bottom/地名名字
    Text _infoText;            // bottom/Info
    Button _bondBtn;           // bottom/战斗按钮
    Text _bondBtnLabel;        // 战斗按钮/地图名字
    Text _bondBtnTitle;        // 战斗按钮/羁绊地图
    Color _bondLabelOrigColor = Color.white;   // 战斗按钮/地图名字 的 prefab 原色（Bind 时缓存）

    // —— 左列表（2026-10-27 主人拍板：可滑动；prefab 6 格作初始池，超出克隆 Row_0 补行）——
    RectTransform _rosterRoot;         // Left/头像（兼 viewport）
    ScrollRect _rosterScroll;          // 左列表滚动容器（运行时套）
    RectTransform _rosterContent;      // 滚动内容（运行时建，行挂靠这里）
    List<GameObject> _rowGos = new List<GameObject>();   // Row_x 本体 GO（含克隆行，下标 = 佣兵下标）
    List<Button> _rowBtns = new List<Button>();          // Row_x 上的 Button（点击选该佣兵）
    List<Image> _rowIcons = new List<Image>();           // Row_x/mask/Icon
    List<Image> _rowBgs = new List<Image>();             // Row_x 本体 Image（= 头像框，随稀有度换三档 + 选中高亮调色用）
    Color _rowOrigColor = Color.white; // Row 本体 prefab 原色（Bind 时缓存，选中/未选在此之上调明暗）

    // —— 右上选中佣兵区 ——
    Image _standFrame;                 // Top/mask 本体 Image（半身像框，随稀有度换三档）
    Image _standFrameBg;               // Top/bg（同款背框，随稀有度一起换）

    // —— 礼物区 ——
    RectTransform _giftTemplate;       // Middle/panl（模板；第三批起 prefab 内常态隐藏，克隆进 GiftScroll/Content）
    RectTransform _giftContent;        // Middle/GiftScroll/Viewport/Content（第三批礼物横排容器；未构建时为 null 走旧定位）
    GameObject _giftScrollGo;          // Middle/GiftScroll（空列表时整体隐藏用）
    List<Text> _rowPcts = new List<Text>();   // Row_x/FavorPct（左列每格好感百分比，2026-10-27 主人拍板）
    Image _giftIcon;                   // panl/Iconbg/Icon
    Text _giftName;                    // panl/Iconbg/Name

    // —— 状态 ——
    string[] _mercIds;                 // 左列表 6 格对应的 hireId
    int _selected = -1;                // 当前选中下标
    string _currentHireId => _mercIds != null && _selected >= 0 && _selected < _mercIds.Length
        ? _mercIds[_selected] : null;

    static readonly Color UnselectedColor = new Color(0.62f, 0.62f, 0.62f, 1f);   // 乘在原色上做「未选中变暗」
    static readonly Color DisabledLabelColor = new Color(0.55f, 0.55f, 0.55f, 1f);

    /// <summary>打开佣兵好感界面。hireId 传空 = 默认选第一个已拥有佣兵。
    /// （静态暂存传参：Instantiate/AddComponent 的 Awake 立即执行，事后赋字段会晚一步。）</summary>
    static string _pendingHireId;
    string _initialHireId;

    public static void Open(string hireId = null)
    {
        if (!Application.isPlaying) return;
        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError(LogTag + "找不到预制体 Resources/" + PrefabPath + "，好感界面不弹");
            return;
        }
        _pendingHireId = hireId;
        var go = Object.Instantiate(prefab);
        go.AddComponent<MercFavorUI>();
    }

    void Awake()
    {
        var canvas = GetComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownPopup);
        // 铁律 8 保险：Canvas 必配 GraphicRaycaster（prefab 已有则不重复加）
        if (canvas != null && canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();

        if (!BindNodes()) { Destroy(gameObject); return; }
        _initialHireId = _pendingHireId;
        _pendingHireId = null;
        RefreshAll();
    }

    /// <summary>绑定全部节点；认不出 = LogError + 关界面（不静默，铁律 14）。</summary>
    bool BindNodes()
    {
        bool ok = true;

        var closeBtn = FindButton("Panel/CloseX", "关闭按钮");
        if (closeBtn == null) ok = false;
        else closeBtn.onClick.AddListener(Close);

        // 左列表：prefab 摆好的 6 格 Row 作为初始池；运行时套 ScrollRect + Content，
        // 已拥有佣兵超过 6 个时克隆 Row_0 补行（2026-10-27 主人拍板「左列是滑动列表可以滑动」）
        _rosterRoot = transform.Find("Panel/Left/头像") as RectTransform;
        if (_rosterRoot == null) { Debug.LogError(LogTag + "缺 Panel/Left/头像 节点（结构被改过？）"); ok = false; }
        else
        {
            string[] rowNames = { "Row_0", "Row_0 (1)", "Row_0 (2)", "Row_0 (3)", "Row_0 (4)", "Row_0 (5)" };
            for (int i = 0; i < rowNames.Length; i++)
            {
                var rowT = _rosterRoot.Find(rowNames[i]);
                if (rowT == null) { Debug.LogError(LogTag + "左列表缺 " + rowNames[i] + "（结构被改过？）"); ok = false; continue; }
                RegisterRow(rowT.gameObject);
            }
            SetupRosterScroll();   // 套 ScrollRect + Content，把已注册的行 reparent 进 Content
        }

        // 右上：立绘 + 头像 + 名字
        var standT = FindT("Panel/Right/Top/mask/Icon", "立绘");
        _standIcon = standT != null ? standT.GetComponent<Image>() : null;
        if (_standIcon == null) ok = false;

        // 【2026-10-26 晚拍板】半身像框随稀有度换三档（主人点名）：mask 是遮罩底图、bg 是同款背框，两张一起换
        var standFrameT = transform.Find("Panel/Right/Top/mask");
        _standFrame = standFrameT != null ? standFrameT.GetComponent<Image>() : null;
        var standFrameBgT = transform.Find("Panel/Right/Top/bg");
        _standFrameBg = standFrameBgT != null ? standFrameBgT.GetComponent<Image>() : null;
        if (_standFrame == null || _standFrameBg == null)
            Debug.LogError(LogTag + "缺 Panel/Right/Top/mask 或 bg 的 Image（半身像框换稀有度图用，结构被改过？）");

        var headT = FindT("Panel/Right/Top1/佣兵头像/Iconmask/Icon", "佣兵头像");
        _headIcon = headT != null ? headT.GetComponent<Image>() : null;
        var nameT = FindT("Panel/Right/Top1/佣兵头像/namebg/Name", "佣兵名字");
        _nameText = nameT != null ? nameT.GetComponent<Text>() : null;
        if (_headIcon == null || _nameText == null) ok = false;

        // 好感进度：爱心1..6 + 空心
        var favorRoot = FindT("Panel/Right/Top/好感进度", "好感进度");
        if (favorRoot == null) { ok = false; }
        else
        {
            _heartFills = new Image[MercFavorDefs.HeartCount];
            _heartEmpties = new Image[MercFavorDefs.HeartCount];
            for (int i = 0; i < MercFavorDefs.HeartCount; i++)
            {
                var heartT = favorRoot.Find("爱心" + (i + 1));
                _heartFills[i] = heartT != null ? heartT.GetComponent<Image>() : null;
                var emptyT = heartT != null ? heartT.Find("空心") : null;
                _heartEmpties[i] = emptyT != null ? emptyT.GetComponent<Image>() : null;
                if (_heartFills[i] == null || _heartEmpties[i] == null)
                {
                    Debug.LogError(LogTag + "好感进度缺 爱心" + (i + 1) + " 或其「空心」子节点（结构被改过？）");
                    ok = false;
                }
            }

            BuildFavorBar(favorRoot);   // 【2026-10-26 晚拍板】空心爱心 + 从左到右进度条（代码建图）
        }

        // 选中对话 1..3（prefab 无 Button，代码补；interaction 必需，不算改 prefab）
        var talkRoot = FindT("Panel/Right/Top1/选择对话", "选择对话");
        if (talkRoot == null) { ok = false; }
        else
        {
            _talkTexts = new Text[3];
            _talkBtns = new Button[3];
            _talkImgs = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var slotT = talkRoot.Find((i + 1).ToString());
                var textT = slotT != null ? slotT.Find("Text (Legacy)") : null;
                _talkTexts[i] = textT != null ? textT.GetComponent<Text>() : null;
                _talkBtns[i] = slotT != null ? slotT.GetComponent<Button>() : null;
                if (_talkBtns[i] == null && slotT != null)
                    _talkBtns[i] = slotT.gameObject.AddComponent<Button>();   // prefab 只摆了 Image，补点击
                _talkImgs[i] = slotT != null ? slotT.GetComponent<Image>() : null;
                if (_talkImgs[i] != null) _talkImgOrigColor = _talkImgs[i].color;
                if (_talkTexts[i] == null || _talkBtns[i] == null)
                {
                    Debug.LogError(LogTag + "选中对话缺槽 " + (i + 1) + " 或其 Text (Legacy)（结构被改过？）");
                    ok = false;
                }
                else
                {
                    int slot = i;
                    _talkBtns[i].onClick.AddListener(() => OnTalkClicked(slot));
                }
            }
            // 按压反馈（2026-10-27 二批）：UICanvasSetup.ApplyPopup 先于 BindNodes 执行，
            // prefab 无 Button、这里的按钮都是代码补的，没被自动挂；对话槽底图非全透明，AttachUnder 能挂上
            UiButtonPressFeedback.AttachUnder(talkRoot);
        }

        // 礼物模板（panl）
        var panlT = FindT("Panel/Right/Middle/panl", "礼物槽模板");
        if (panlT == null) { ok = false; }
        else
        {
            _giftTemplate = panlT as RectTransform;
            // 第三批礼物横排容器（Tools/好感界面/第三批构建 生成；没跑菜单时为 null，RefreshGifts 走旧步进定位）
            var scrollT = transform.Find("Panel/Right/Middle/GiftScroll");
            _giftScrollGo = scrollT != null ? scrollT.gameObject : null;
            _giftContent = scrollT != null ? scrollT.Find("Viewport/Content") as RectTransform : null;
            var iconT = panlT.Find("Iconbg/Icon");
            var nameT2 = panlT.Find("Iconbg/Name");
            _giftIcon = iconT != null ? iconT.GetComponent<Image>() : null;
            _giftName = nameT2 != null ? nameT2.GetComponent<Text>() : null;
            if (_giftIcon == null || _giftName == null)
            {
                Debug.LogError(LogTag + "礼物模板缺 Iconbg/Icon 或 Iconbg/Name（结构被改过？）");
                ok = false;
            }
        }

        // 底部：地名/Info/战斗按钮
        var mapNameT = FindT("Panel/Right/bottom/地图名字", "地图名字");
        _mapNameText = mapNameT != null ? mapNameT.GetComponent<Text>() : null;
        var infoT = FindT("Panel/Right/bottom/Info", "Info");
        _infoText = infoT != null ? infoT.GetComponent<Text>() : null;
        var bondBtnT = FindT("Panel/Right/bottom/战斗按钮", "战斗按钮");
        _bondBtn = bondBtnT != null ? bondBtnT.GetComponent<Button>() : null;
        var bondLabelT = bondBtnT != null ? bondBtnT.Find("地图名字") : null;
        _bondBtnLabel = bondLabelT != null ? bondLabelT.GetComponent<Text>() : null;
        var bondTitleT = bondBtnT != null ? bondBtnT.Find("羁绊地图") : null;
        _bondBtnTitle = bondTitleT != null ? bondTitleT.GetComponent<Text>() : null;
        if (_bondBtnLabel != null) _bondLabelOrigColor = _bondBtnLabel.color;
        if (_mapNameText == null || _infoText == null || _bondBtn == null)
        {
            Debug.LogError(LogTag + "bottom 区缺 地名名字/Info/战斗按钮（结构被改过？）");
            ok = false;
        }
        else
        {
            _bondBtn.onClick.AddListener(OnBondClicked);
            if (_bondBtnTitle != null) _bondBtnTitle.text = "羁绊地图";
        }

        // 【2026-10-26 晚拍板】立绘上的问题对话框（佣兵提问，答案在下方「选中对话」里选）。
        // 必须放在 _nameText 赋值之后：BuildQuestionBubble 首行判 _nameText == null 直接 return，
        // 之前调用点在赋值前导致气泡永远建不出来（2026-10-27 二批修复）。
        // 挂点用 standT.parent.parent（= Panel/Right/Top，scale=1）：挂在 standT.parent（Top/mask，
        // scale 0.45）下会被 Mask 裁剪、有效字号只剩 ~11（2026-10-27 挪出）。
        if (standT != null) BuildQuestionBubble(standT.parent.parent);

        return ok;
    }

    Transform FindT(string path, string label)
    {
        var t = transform.Find(path);
        if (t == null) Debug.LogError(LogTag + "缺节点 " + path + "（" + label + "，结构被改过？）");
        return t;
    }

    Button FindButton(string path, string label)
    {
        var t = FindT(path, label);
        var c = t != null ? t.GetComponent<Button>() : null;
        if (c == null) Debug.LogError(LogTag + path + " 上没有 Button");
        return c;
    }

    // —— 左列表滑动支持（2026-10-27 主人拍板：可滑动，超出 6 个克隆 Row_0 补行）——
    // 行几何沿用 prefab 实测（勿按旧常量猜）：行宽 109 / 行高 108 / 步距 110 / 左列横位 x=-195
    const float RosterRowX = -195f;
    const float RosterRowH = 108f;
    const float RosterRowStep = 110f;
    const float RosterTopPad = 10f;

    /// <summary>把一行（prefab 行或克隆行）注册进平行 List，并绑定「点击 = 选该下标佣兵」。
    /// idx 用闭包捕获当前下标（绑定时即正确；列表与 _mercIds 1:1）。</summary>
    void RegisterRow(GameObject go)
    {
        var btn = go.GetComponent<Button>();
        var bg = go.GetComponent<Image>();
        var iconT = go.transform.Find("mask/Icon");
        var icon = iconT != null ? iconT.GetComponent<Image>() : null;
        if (btn == null || icon == null)
            Debug.LogError(LogTag + "左列表行 " + go.name + " 缺 Button 或 mask/Icon（结构被改过？）");
        else
        {
            int idx = _rowBtns.Count;   // 当前下标 = 入列顺序
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => Select(idx));
        }
        if (bg != null && _rowBgs.Count == 0) _rowOrigColor = bg.color;   // 取首个行的 prefab 原色作基准
        _rowGos.Add(go);
        _rowBtns.Add(btn);
        _rowIcons.Add(icon);
        _rowBgs.Add(bg);
        var pctT = go.transform.Find("FavorPct");
        _rowPcts.Add(pctT != null ? pctT.GetComponent<Text>() : null);   // 没跑第三批构建时为 null，渲染时跳过
    }

    /// <summary>运行时给左列表套 ScrollRect（照 AdventureLogPhase3Panel.BuildRowScroll 同款结构：ScrollRect→Viewport(RectMask2D)→Content），
    /// 行 reparent 进 Content；头像节点保持原样，不被改动布局。</summary>
    void SetupRosterScroll()
    {
        if (_rosterRoot == null || _rosterContent != null) return;

        // ScrollRect 容器：盖住 头像 区（与 头像 同尺寸），纯竖向滚动 + 惯性
        var scroll = new GameObject("RowScroll", typeof(RectTransform), typeof(ScrollRect));
        scroll.transform.SetParent(_rosterRoot, false);
        var srt = scroll.GetComponent<RectTransform>();
        srt.anchorMin = Vector2.zero;
        srt.anchorMax = Vector2.one;
        srt.offsetMin = Vector2.zero;
        srt.offsetMax = Vector2.zero;

        var vp = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        vp.transform.SetParent(srt, false);
        var vrt = vp.GetComponent<RectTransform>();
        vrt.anchorMin = Vector2.zero;
        vrt.anchorMax = Vector2.one;
        vrt.offsetMin = Vector2.zero;
        vrt.offsetMax = Vector2.zero;

        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(vrt, false);
        _rosterContent = content.GetComponent<RectTransform>();
        _rosterContent.anchorMin = new Vector2(0.5f, 1f);
        _rosterContent.anchorMax = new Vector2(0.5f, 1f);
        _rosterContent.pivot = new Vector2(0.5f, 1f);
        _rosterContent.anchoredPosition = Vector2.zero;
        _rosterContent.sizeDelta = new Vector2(0f, 0f);   // 高度后续按行数设

        // 已注册的 prefab 行 reparent 进 Content（本地方位不变，只换挂靠点）
        for (int i = 0; i < _rowGos.Count; i++)
            if (_rowGos[i] != null) _rowGos[i].transform.SetParent(_rosterContent, false);

        var sr = scroll.GetComponent<ScrollRect>();
        sr.content = _rosterContent;
        sr.viewport = vrt;
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 15f;
        sr.inertia = true;
        sr.decelerationRate = 0.135f;
        _rosterScroll = sr;
    }

    /// <summary>保证行数 = n（已拥有佣兵数）：超出当前池时克隆 Row_0 补行；多余行隐藏。随后布位 / 设内容高 / 滚到选中。</summary>
    void EnsureRowCount(int n)
    {
        GameObject template = _rowGos.Count > 0 ? _rowGos[0] : null;
        for (int i = _rowGos.Count; i < n; i++)
        {
            if (template == null) { Debug.LogError(LogTag + "左列表缺克隆模板 Row_0，无法补行"); break; }
            var clone = Object.Instantiate(template);
            clone.name = "Row_0 (clone " + i + ")";
            clone.SetActive(true);
            var rt = clone.GetComponent<RectTransform>();
            rt.SetParent(_rosterContent, false);
            RegisterRow(clone);   // 注册到平行 List 并绑定 Select(i)
        }
        for (int i = n; i < _rowGos.Count; i++)
            if (_rowGos[i] != null) _rowGos[i].SetActive(false);
        LayoutRosterRows(n);
        UpdateRosterContentHeight(n);
        ScrollSelectedIntoView();
    }

    /// <summary>行布位：顶部锚定、左列横位、逐个下移 step（沿用 prefab 步距，不发明布局数值）。</summary>
    void LayoutRosterRows(int n)
    {
        for (int i = 0; i < n && i < _rowGos.Count; i++)
        {
            if (_rowGos[i] == null) continue;
            var rt = _rowGos[i].GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(RosterRowX, -(RosterTopPad + RosterRowH * 0.5f + i * RosterRowStep));
        }
    }

    /// <summary>内容高度随行数伸缩：≤一屏不滚，超出可滚。</summary>
    void UpdateRosterContentHeight(int n)
    {
        if (_rosterContent == null) return;
        float h = RosterTopPad * 2f + (Mathf.Max(1, n) - 1) * RosterRowStep + RosterRowH;
        _rosterContent.sizeDelta = new Vector2(0f, h);
    }

    /// <summary>滚动让当前选中行进入视口（居中优先，越界夹到顶/底）。</summary>
    void ScrollSelectedIntoView()
    {
        if (_rosterScroll == null || _rosterScroll.viewport == null || _rosterContent == null || _selected < 0) return;
        float vp = _rosterScroll.viewport.rect.height;
        float contentH = _rosterContent.sizeDelta.y;
        if (contentH <= vp) { _rosterScroll.verticalNormalizedPosition = 1f; return; }
        float rowCenter = RosterTopPad + RosterRowH * 0.5f + _selected * RosterRowStep;
        float offset = rowCenter - vp * 0.5f;
        float norm = 1f - Mathf.Clamp(offset, 0f, contentH - vp) / (contentH - vp);
        _rosterScroll.verticalNormalizedPosition = Mathf.Clamp(norm, 0f, 1f);
    }

    // —— 运行时建图（2026-10-26 晚主人点名要的两件：进度条 + 问题对话框；锚点比例摆位，不碰 prefab 节点）——

    /// <summary>进度条素材路径：主人好感度图组里的金边条框（436×105，左端原有心形位）。</summary>
    const string FavorBarSpritePath = "UI/Art/Tavern/好感度/_0001_图层-2";
    static readonly Color FavorFillColor = new Color(0.96f, 0.42f, 0.55f, 1f);   // mockup 同款粉

    /// <summary>
    /// 好感进度条（2026-10-27 美术裁决）：
    /// 条框底图 320×77（素材 436×105 ≈4.15:1 保比例）挂在 Panel/Right/Top（scale=1）下，
    /// 世界 center ≈(71,156)，沿黑条 Top/Image(373×50) 排开、覆盖整排爱心；
    /// SetSiblingIndex 排到「好感进度」之前：track 先画、prefab 爱心后画盖在上面。
    /// （不再挂「好感进度」48×48 小图标下——那会被图标盖住、track 只剩 ~46×42。）
    /// 填充不用 Image.Type.Filled（没给 sprite 时 fillAmount 不生效、好感 0 也满条），
    /// 改 sizeDelta 方案：锚点 (0,0.2)~(0,0.8) 定填充区 y、pivot (0,0.5)、
    /// 起点右移 0.26 避开底图左端心形位，宽 = trackWidth × 0.71 × 好感比例（SetFavorFill 更新）。
    /// </summary>
    void BuildFavorBar(Transform favorRoot)
    {
        var topT = favorRoot != null ? favorRoot.parent : null;   // Panel/Right/Top
        if (topT == null)
        {
            Debug.LogError(LogTag + "好感进度缺父节点 Panel/Right/Top，进度条不建（结构被改过？）");
            return;
        }
        var track = NewUiImage(topT, "FavorBarTrack", FavorBarSpritePath,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Color.white, -1);
        if (track == null) return;   // 素材缺失已 LogError，条不建（不静默也不兜底造图）
        var trackRt = track.GetComponent<RectTransform>();
        trackRt.pivot = new Vector2(0.5f, 0.5f);
        trackRt.sizeDelta = new Vector2(320f, 77f);          // 底图 436×105 保比例
        trackRt.anchoredPosition = new Vector2(75f, 166f);   // Top 中心 (-4,-10) → 世界 center ≈(71,156)
        trackRt.SetSiblingIndex(favorRoot.GetSiblingIndex());   // 排到「好感进度」之前，先画在底层

        // 填充条：sizeDelta 方案（不用 Filled —— 无 sprite 时 fillAmount 不生效、恒满）
        var fillGo = new GameObject("FavorBarFill", typeof(RectTransform), typeof(Image));
        var fillT = fillGo.GetComponent<RectTransform>();
        fillT.SetParent(track.transform, false);
        fillT.anchorMin = new Vector2(0f, 0.20f);   // 填充区 y
        fillT.anchorMax = new Vector2(0f, 0.80f);
        fillT.pivot = new Vector2(0f, 0.5f);
        fillT.anchoredPosition = new Vector2(320f * 0.26f, 0f);   // 填充起点右移到 0.26，避开底图左端心形位
        var fillImg = fillGo.GetComponent<Image>();
        fillImg.color = FavorFillColor;
        fillImg.raycastTarget = false;
        _favorFill = fillImg;
    }

    /// <summary>好感比例 → 填充条宽（sizeDelta 方案：0.97-0.26=0.71 为可用宽度）。</summary>
    void SetFavorFill(float ratio)
    {
        if (_favorFill == null) return;
        const float trackWidth = 320f;
        const float usable = 0.71f;
        _favorFill.rectTransform.sizeDelta =
            new Vector2(trackWidth * usable * Mathf.Clamp01(ratio), 0f);
    }

    /// <summary>
    /// 问题对话框：盖在立绘框上方的条形泡（底图复用 prefab「选中对话」同款槽底图，风格一致），
    /// 文本用 prefab 佣兵名字同款字体 + BestFit 自适应。
    /// </summary>
    void BuildQuestionBubble(Transform standRoot)
    {
        if (standRoot == null || _nameText == null) return;
        var bubble = NewUiImage(standRoot, "QuestionBubble", null,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Color.white, -1);
        if (bubble == null) return;

        // 2026-10-27 美术给值：气泡盖立绘框上方，世界框 y≈[293,337]、x≈立绘框宽的 0.05~0.95。
        // 换算（挂点 Panel/Right/Top 是 100×100 哑容器、中心 (-4,-10)、scale=1；早期给的比例锚点
        // (0.05,0.78)~(0.95,0.98) 是按立绘框矩形算的，直接用在哑容器上会落到屏幕中部，故改中心锚点）：
        // 立绘框 bg/mask（835×470、scale 0.45、本地 (73,244)）→ 世界 rect 中心 (69,234)、375.75×211.5；
        // 取 x 0.05~0.95、y [293,337] → 世界 center (69,315)、338×44 → 本地 pos (73,325)、sizeDelta (338,44)。
        var bubbleRt = bubble.GetComponent<RectTransform>();
        bubbleRt.pivot = new Vector2(0.5f, 0.5f);
        bubbleRt.sizeDelta = new Vector2(338f, 44f);
        bubbleRt.anchoredPosition = new Vector2(73f, 325f);

        var slotImgT = transform.Find("Panel/Right/Top1/选择对话/1");
        var slotImg = slotImgT != null ? slotImgT.GetComponent<Image>() : null;
        if (slotImg != null && slotImg.sprite != null)
            bubble.GetComponent<Image>().sprite = slotImg.sprite;   // 同款底图（不造图）

        var textGo = new GameObject("QuestionText", typeof(RectTransform), typeof(Text));
        var textT = textGo.GetComponent<RectTransform>();
        textT.SetParent(bubble.transform, false);
        textT.anchorMin = Vector2.zero;
        textT.anchorMax = Vector2.one;
        textT.offsetMin = new Vector2(14f, 4f);
        textT.offsetMax = new Vector2(-14f, -4f);
        var text = textGo.GetComponent<Text>();
        text.font = _nameText.font;
        text.fontSize = 24;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.resizeTextForBestFit = true;              // 框高随屏幕适配，字号自适应不写死
        text.resizeTextMinSize = 14;
        text.resizeTextMaxSize = 26;
        text.raycastTarget = false;
        _questionText = text;
    }

    /// <summary>建一个带 Image 的 UI 子节点（锚点用比例；siblingIndex 0 = 垫底，-1 = 最上层）。</summary>
    GameObject NewUiImage(Transform parent, string name, string spritePath,
        Vector2 anchorMin, Vector2 anchorMax, Color color, int siblingIndex)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        if (siblingIndex < 0) rt.SetAsLastSibling();
        else rt.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, parent.childCount - 1));
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        if (spritePath != null)
        {
            var sp = Resources.Load<Sprite>(spritePath);
            if (sp == null)
            {
                Debug.LogError(LogTag + "进度条底图加载失败：Resources/" + spritePath +
                               "（好感度素材在但没进 Resources？）；改用纯色底不静默");
                // 素材缺 → 纯色底照样能看（进度条仍可用），但错误必须留在日志里（铁律 14）
                img.color = new Color(0.10f, 0.13f, 0.22f, 0.92f);
            }
            else
            {
                img.sprite = sp;
                img.color = color;
            }
        }
        else img.color = color;
        img.raycastTarget = false;
        return go;
    }

    // ———————— 刷新 ————————

    void Select(int idx)
    {
        if (idx < 0 || idx >= _mercIds.Length || _mercIds[idx] == null) return;
        _selected = idx;
        RefreshAll();
    }

    SaveData Data => SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;

    void RefreshAll()
    {
        var d = Data;
        if (d == null) return;
        MercFavorSystem.EnsureTalkDay(d);

        // 左列表：已拥有佣兵（花名册表序过滤 unlocked），可滚动、不封顶 6 格（2026-10-27 主人拍板）
        if (_mercIds == null)
        {
            var all = MercRosterDefs.All;
            var owned = new List<string>();
            for (int i = 0; i < all.Count; i++)
                if (d.IsMercUnlocked(all[i].HireId)) owned.Add(all[i].HireId);
            _mercIds = owned.ToArray();
        }

        // 默认选中：外部指定 hireId → 找它；否则第一个
        if (_selected < 0)
        {
            int want = -1;
            if (!string.IsNullOrEmpty(_initialHireId))
            {
                for (int i = 0; i < _mercIds.Length; i++)
                    if (_mercIds[i] == _initialHireId) { want = i; break; }
            }
            _selected = want >= 0 ? want : (_mercIds.Length > 0 ? 0 : -1);
        }

        // 保证行数 = 已拥有数（超出 6 个克隆 Row_0 补行），并布位 / 设内容高 / 滚到选中
        EnsureRowCount(_mercIds.Length);

        // 左列表渲染
        for (int i = 0; i < _mercIds.Length; i++)
        {
            if (i >= _rowGos.Count || _rowGos[i] == null) continue;
            _rowGos[i].SetActive(true);
            var sp = MercPortraitSprites.GetHead(_mercIds[i]);
            if (sp != null && i < _rowIcons.Count && _rowIcons[i] != null) _rowIcons[i].sprite = sp;
            // 【2026-10-26 晚拍板】头像框随本格佣兵稀有度换三档（普通银/稀有蓝/传奇金）
            if (i < _rowBgs.Count && _rowBgs[i] != null)
            {
                if (MercRosterDefs.TryGetByHireId(_mercIds[i], out var rowDef))
                    _rowBgs[i].sprite = MercHireSession.LoadTavernCardFrame(rowDef.Rarity);
                else
                    Debug.LogError(LogTag + "花名册里查不到佣兵 " + _mercIds[i] + "，头像框不换（表被改过？）");
                _rowBgs[i].color = i == _selected ? _rowOrigColor : _rowOrigColor * UnselectedColor;
            }
        }
        // 左列每格好感百分比（2026-10-27 主人拍板：爱心旁显示当前进度；FavorPct 由 Tools/好感界面/第三批构建 生成）
        for (int i = 0; i < _mercIds.Length && i < _rowPcts.Count; i++)
        {
            if (_rowPcts[i] == null) continue;
            float ratio = Mathf.Clamp01(MercFavorSystem.GetFavor(d, _mercIds[i]) / (float)MercFavorDefs.FavorMax);
            _rowPcts[i].text = Mathf.RoundToInt(ratio * 100f) + "%";
        }
        // 多余行（正常不会有多余，随 GO 销毁；此处保险隐藏）
        for (int i = _mercIds.Length; i < _rowGos.Count; i++)
            if (_rowGos[i] != null) _rowGos[i].SetActive(false);

        RefreshMercPanel(d);
        RefreshGifts(d);
    }

    void RefreshMercPanel(SaveData d)
    {
        string hireId = _currentHireId;
        if (string.IsNullOrEmpty(hireId)) return;
        if (!MercRosterDefs.TryGetByHireId(hireId, out var def))
        {
            Debug.LogError(LogTag + "花名册里查不到佣兵 " + hireId + "（表被改过？）");
            return;   // fail closed：查不到就整块不刷，不给半套 UI
        }

        // 立绘 / 头像：取不到保留 prefab 原图（不造图）
        var stand = MercPortraitSprites.GetStand(hireId);
        if (stand != null) _standIcon.sprite = stand;
        var head = MercPortraitSprites.GetHead(hireId);
        if (head != null) _headIcon.sprite = head;

        // 【2026-10-26 晚拍板】半身像框随选中佣兵稀有度换三档（主人点名）
        var frameSp = MercHireSession.LoadTavernCardFrame(def.Rarity);
        if (frameSp != null)
        {
            // 2026-10-27 美术裁决：mask 只做裁剪不显示、裁剪形状不随稀有度变（实测三档框图中心
            // 区域不透明，立绘显示正常）；框由 _standFrameBg（Top/bg）显示。
            // 原先这行把 _standFrame（Top/mask 本体 Image）也换图，已删；字段与绑定保留不动。
            if (_standFrameBg != null) _standFrameBg.sprite = frameSp;
        }
        else
            Debug.LogError(LogTag + "稀有度头像框加载失败：Resources/" + MercHireSession.TavernCardFrameDir +
                           "/（普通|稀有|传奇）头像框，半身像框不换（fail closed 不静默）");

        // 名字
        if (_nameText != null)
        {
            string nick = def.Nickname;
            _nameText.text = string.IsNullOrEmpty(nick) ? def.Name : def.Name + "·" + nick;
        }

        // 好感进度（2026-10-26 晚拍板）：6 颗空心爱心常显 + 进度条按 好感/满值 从左到右填充
        int favor = MercFavorSystem.GetFavor(d, hireId);
        for (int i = 0; i < MercFavorDefs.HeartCount; i++)
        {
            _heartFills[i].enabled = false;    // 实心常隐藏（进度交给条）
            _heartEmpties[i].enabled = true;
        }
        SetFavorFill(Mathf.Clamp01(favor / (float)MercFavorDefs.FavorMax));

        // 问题对话框：佣兵先开口提问（答案在下方三选一，分值不展示）
        MercFavorDefs.GetTalk(hireId, out var question, out var options, out var replies);
        if (_questionText != null) _questionText.text = question;

        // 答案 3 槽：每天 3 次、一次选一个；分值不展示（每天轮换一个特殊答案，玩家猜）
        bool canTalk = MercFavorSystem.CanTalk(d, hireId);
        for (int i = 0; i < MercFavorDefs.DialogueOptionCount; i++)
        {
            _talkBtns[i].interactable = canTalk;
            if (_talkImgs[i] != null)
                _talkImgs[i].color = canTalk ? _talkImgOrigColor : DisabledLabelColor;
            if (_talkTexts[i] != null)
                _talkTexts[i].text = canTalk ? options[i] : "今日已聊";
        }

        // 底部羁绊区
        if (_mapNameText != null) _mapNameText.text = MercFavorDefs.BondMapName(hireId);
        bool full = MercFavorSystem.IsBondOpen(d, hireId);
        bool cleared = MercFavorSystem.IsBondCleared(d, hireId);
        if (_infoText != null)
        {
            if (cleared)
                _infoText.text = "羁绊已建立，" + def.Name + " 全属性永久提升！";
            else if (full)
                _infoText.text = "好感已满！羁绊地图已开放，通关后 " + def.Name + " 属性提升。";
            else
                _infoText.text = "好感 " + favor + "/" + MercFavorDefs.FavorMax + "，每日对话、送礼可提升好感，攒满解锁羁绊地图。";
        }
        _bondBtn.interactable = full && !cleared;
        if (_bondBtnLabel != null)
        {
            _bondBtnLabel.text = cleared ? "已建立" : full ? "可挑战" : "未开放";
            // 禁用压灰用存档色恢复原色（不改 prefab 摆好的颜色值，只在禁/可用间切换）
            _bondBtnLabel.color = _bondBtn.interactable ? _bondLabelOrigColor : DisabledLabelColor;
        }
    }

    void RefreshGifts(SaveData d)
    {
        // 模板隐藏是常态：panl 本体只在「第一个礼物」时复用，其余克隆
        ClearGiftClones();

        var gifts = MercFavorDefs.Gifts;
        int shown = 0;
        bool useScroll = _giftContent != null;    // 第三批 GiftScroll 结构已构建（Tools/好感界面/第三批构建）
        float step = _giftTemplate.sizeDelta.x;   // 旧定位（结构未构建时的过渡路径）：步进 = 模板自身宽（不发明布局数值）
        Vector2 basePos = _giftTemplate.anchoredPosition;
        for (int i = 0; i < gifts.Length; i++)
        {
            int count = d.GetFavorGiftCount(gifts[i].Id);
            if (count <= 0) continue;

            RectTransform slot;
            if (useScroll)
            {
                // 新结构：克隆进 Content，排布交给 Content 上的 HorizontalLayoutGroup（prefab 内可调）
                slot = Object.Instantiate(_giftTemplate, _giftContent);
                slot.gameObject.SetActive(true);          // 模板在 prefab 里常态隐藏，克隆激活
                slot.name = "gift_" + gifts[i].Id;
            }
            else if (shown == 0)
            {
                slot = _giftTemplate;
                slot.gameObject.SetActive(true);
            }
            else
            {
                slot = Object.Instantiate(_giftTemplate, _giftTemplate.parent);
                slot.name = "gift_" + gifts[i].Id;
                // 横排步进 = 模板自身宽度：主人点名的「礼物横排列表」列表填充必要介入（铁律 20 例外注明）
                slot.anchoredPosition = basePos + new Vector2(step * shown, 0f);
            }
            shown++;

            var iconT = slot.Find("Iconbg/Icon");
            var icon = iconT != null ? iconT.GetComponent<Image>() : null;
            var sp = Resources.Load<Sprite>(gifts[i].IconPath);
            if (icon != null && sp != null) icon.sprite = sp;
            else if (sp == null) Debug.LogError(LogTag + "礼物图标加载失败：" + gifts[i].IconPath);

            var nameT = slot.Find("Iconbg/Name");
            var nameTx = nameT != null ? nameT.GetComponent<Text>() : null;
            if (nameTx != null)
            {
                // 【2026-10-27 主人拍板】送礼区显示品质标签 + 品质着色；数量 ×count 原本就有
                string qTag = MercFavorDefs.QualityName(gifts[i].Quality);
                nameTx.text = "【" + qTag + "】" + gifts[i].Name + " ×" + count + " +" + gifts[i].Favor;
                if (ColorUtility.TryParseHtmlString("#" + MercFavorDefs.QualityColorHex(gifts[i].Quality), out var qc))
                    nameTx.color = qc;
            }

            string giftId = gifts[i].Id;
            var btn = slot.GetComponent<Button>();
            if (btn == null) btn = slot.gameObject.AddComponent<Button>();   // Unity 伪 null 不能走 ?? 判空
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnGiftClicked(giftId));

            // 命中区覆盖整槽（2026-10-27 二批）：panl 本体无 Image，按钮没有可点的自身 Graphic，
            // 默认只能点到子节点图上；补一块透明命中图作 targetGraphic（画在子图之下，纯命中用）
            if (slot.GetComponent<Image>() == null)
            {
                var hitImg = slot.gameObject.AddComponent<Image>();
                hitImg.color = new Color(1f, 1f, 1f, 0f);
                hitImg.raycastTarget = true;
                btn.targetGraphic = hitImg;
            }

            // 按压反馈：UICanvasSetup.ApplyPopup 先于 BindNodes 执行，运行时补的按钮没被自动挂。
            // 注意 AttachUnder 的 ShouldSkip 会跳过全透明命中层，故对漏挂的直接补组件
            //（缩放反馈仍生效；变暗作用在透明图上无视觉副作用）
            if (slot.GetComponent<UiButtonPressFeedback>() == null)
            {
                UiButtonPressFeedback.AttachUnder(slot);   // prefab 已挂（第三批构建）就跳过，防重复
                if (slot.GetComponent<UiButtonPressFeedback>() == null)
                    slot.gameObject.AddComponent<UiButtonPressFeedback>();   // AttachUnder 会跳过全透明命中层，此处兜底
            }
        }

        if (_giftScrollGo != null) _giftScrollGo.SetActive(shown > 0);   // 新结构：没礼物整排隐藏
        else if (shown == 0)
        {
            _giftTemplate.gameObject.SetActive(false);   // 没有礼物：整排隐藏（来源 = 每日回礼 + 礼物副本掉落，见 SaveData.favorGifts）
        }
    }

    void ClearGiftClones()
    {
        if (_giftContent != null)
        {
            for (int i = _giftContent.childCount - 1; i >= 0; i--)
            {
                var child = _giftContent.GetChild(i);
                if (child.name.StartsWith("gift_")) Destroy(child.gameObject);
            }
            return;
        }
        var parent = _giftTemplate.parent;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
            if (child.name.StartsWith("gift_")) Destroy(child.gameObject);
        }
        _giftTemplate.gameObject.SetActive(true);   // 旧结构复位（没礼物时下方再统一隐藏）
    }

    // ———————— 交互 ————————

    void OnTalkClicked(int slot)
    {
        var d = Data;
        if (d == null || string.IsNullOrEmpty(_currentHireId)) return;
        int gain = MercFavorSystem.Talk(d, _currentHireId, slot, out var grantedGiftId);
        if (gain < 0) return;   // 今日已聊（按钮已禁点，这里再拦一道）

        MercFavorDefs.GetTalk(_currentHireId, out _, out _, out var replies);
        MercRosterDefs.TryGetByHireId(_currentHireId, out var def);
        string msg = def.Name + "：「" + replies[slot] + "」 好感+" + gain;   // 选完才揭晓加了多少
        // 【2026-10-26 主人拍板】当日 3 次聊完随机回礼（Toast 由 GrantRandomGift 自己 force 弹，这里不重复）
        if (grantedGiftId != null) msg += "，还收到了回礼！";
        GlobalToastUI.Show(msg);
        SaveSystem.Instance.Save();
        RefreshAll();
    }

    void OnGiftClicked(string giftId)
    {
        var d = Data;
        if (d == null || string.IsNullOrEmpty(_currentHireId)) return;
        int gain = MercFavorSystem.GiveGift(d, _currentHireId, giftId);
        if (gain < 0) { RefreshGifts(d); return; }   // 礼物没了等异常：静默刷一遍，下次必显示正确状态

        MercFavorDefs.TryGetGift(giftId, out var def);
        MercRosterDefs.TryGetByHireId(_currentHireId, out var m);
        GlobalToastUI.Show("送出" + def.Name + "，" + (m.Name ?? _currentHireId) + " 好感+" + gain);
        SaveSystem.Instance.Save();
        RefreshAll();
    }

    void OnBondClicked()
    {
        var d = Data;
        if (d == null || string.IsNullOrEmpty(_currentHireId)) return;
        // 一次性：未开放 / 已通关都不进本（按钮已置灰，这里双保险）
        if (!MercFavorSystem.IsBondOpen(d, _currentHireId) || MercFavorSystem.IsBondCleared(d, _currentHireId)) return;
        // 【2026-10-27 主人拍板】羁绊副本 = 照礼物副本框架：好感满佣兵专属单场关，该佣兵强制进队，
        // 难度固定 ≈5~7 章、一次性通关发钻石 + 永久 ×1.5 属性（详见 BondDungeonBattleMode）。
        // 进本标记（2026-10-28 起统一走 ResetPendingFlags 全清，再立羁绊本标记；BattleManager 读后即清）
        AdventureUI.ResetPendingFlags();
        AdventureUI.PendingBondDungeon = true;
        AdventureUI.PendingBondHireId = _currentHireId;
        AdventureUI.PendingBattleDifficulty = MercFavorDefs.BondDungeonDifficulty;
        AdventureUI.PendingBattleChapter = MercFavorDefs.BondDungeonChapter;
        // 关掉好感界面再进本（进战斗会切场景，这里先 Destroy 干净，避免叠界面）
        Destroy(gameObject);
        if (GameSceneManager.Instance != null) GameSceneManager.Instance.LoadBattleScene();
        else Debug.LogError(LogTag + "找不到 GameSceneManager，羁绊副本进本失败");
    }

    void Close()
    {
        Destroy(gameObject);
    }
}
