using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 满员替换佣兵弹窗（2026-11-24 主人拍板）。
///
/// <para><b>为什么复用装备替换弹窗</b>：主人明确「类似替换装备的弹窗」→ 直接加载
/// <c>Resources/Prefabs/Battle/EquipReplaceConfirmPopup</c> 当底（面板 / 两栏 / 按钮全是现成美术），
/// 不新建预制体、不重跑生成器。预制体根上的 <see cref="EquipReplaceConfirmUI"/> 实例化后 Destroy 掉，
/// 与本项目 <c>MercObtainPopupUI.Ensure</c> 处理 stale 组件的既有手法一致。</para>
///
/// <para><b>三栏从哪来</b>：原预制体只有 ColumnNew / ColumnOld 两栏，主人要「新佣兵 + 两位现有」共三栏。
/// 640 宽面板放不下三栏 254 → 运行时把 ColumnOld 克隆一栏，三栏统一缩到 <see cref="ColScale"/>，
/// 按钮用同一套克隆（ReplaceButton ×2）+ 原 KeepButton 改成「不要了」，三颗排一行对齐三栏。</para>
///
/// <para>⚠ 排版不是预制体 authored 的，是下面几个常量摆的（改位置只改常量，别动预制体）。
/// 编辑器里看着不对就把 <see cref="ColScale"/> / <see cref="ColX"/> / <see cref="BtnY"/> 调一调。</para>
/// </summary>
public class MercReplaceConfirmUI : MonoBehaviour
{
    /// <summary>与 <see cref="EquipReplaceConfirmUI.PrefabPath"/> 同值：借它的底，不复制资源。</summary>
    public const string PrefabPath = EquipReplaceConfirmUI.PrefabPath;

    /// <summary>与获得佣兵弹窗同一层（925）：波间三选一 920 之上，抽奖 920 之上。</summary>
    const int SortOrder = 925;

    /// <summary>没人点就自动换下较弱的那位（不吞奖励，跟装备替换的「超时按战力选」同精神）。</summary>
    const float TimeoutSec = 15f;

    // —— 三栏布局常量（640×800 面板）——
    /// <summary>单栏缩放：原栏 254 宽，三栏 ×0.72 ≈ 183，含间隙刚好排满 640。</summary>
    const float ColScale = 0.72f;
    /// <summary>左右两栏的 x 偏移（中栏恒 0）：±218 = 183 栏宽 + 35 间隙。</summary>
    const float ColX = 218f;
    /// <summary>栏的 y（沿用预制体 ColumnNew/ColumnOld authored 的 -358，顶对齐）。</summary>
    const float ColY = -358f;
    /// <summary>三颗按钮同一行的 y（沿用预制体按钮行的 -691）。</summary>
    const float BtnY = -691f;
    /// <summary>按钮缩放（跟栏同比例，200×62 → 144×45，三颗间距 218 不打架）。</summary>
    const float BtnScale = 0.72f;

    static MercReplaceConfirmUI _inst;

    /// <summary>弹窗是否开着（波次推进要等它收场，唯一判据）。</summary>
    public static bool IsOpen => _inst != null && _inst._root != null && _inst._root.activeSelf;

    GameObject _root;
    Transform _panel;
    Transform _colNew;
    Transform _colOldA;
    Transform _colOldB;
    Button _btnA;
    Button _btnB;
    Button _cancelBtn;
    Button _dimBtn;
    Text _title;
    Text _verdict;
    bool _built;

    RunMercEntry _newEntry;
    /// <summary>结果回调：0/1 = 换下第几位；-1 = 不要了 / 没得选。</summary>
    System.Action<int> _onPick;
    bool _answered;
    float _timer;

    // ============================================================
    // 对外入口
    // ============================================================

    /// <summary>
    /// 弹「换下谁」。回调只发一次：0/1 = 换下第几位；-1 = 玩家点「不要了」或弹窗建不起来。
    /// 调用方（<see cref="RunDraftDirector"/>）在此回调里做账本替换 + 场上撤离。
    /// </summary>
    public static void Show(RunMercEntry newEntry, System.Action<int> onPick)
    {
        var ui = Ensure();
        if (ui == null)
        {
            Debug.LogError("[MercReplace] 弹窗建不起来，本次佣兵按「不要了」处理");
            onPick?.Invoke(-1);
            return;
        }

        ui._newEntry = newEntry;
        ui._onPick = onPick;
        ui._answered = false;
        ui._timer = 0f;
        ui._lastLeft = -1;
        ui.EnsureBuilt();
        if (!ui._built)
        {
            // 建不起来（缺 Panel/Column/按钮）就别开空窗：按「不要了」放行，绝不卡住波次
            Debug.LogError("[MercReplace] 三栏没建起来（预制体节点缺失），本次佣兵按「不要了」处理");
            ui._onPick = null;
            onPick?.Invoke(-1);
            return;
        }
        ui.Fill();
        ui.OpenRoot();
    }

    static MercReplaceConfirmUI Ensure()
    {
        if (_inst != null) return _inst;

        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[MercReplace] 找不到预制体 Resources/" + PrefabPath + "（装备替换弹窗的底）");
            return null;
        }

        var go = Instantiate(prefab);
        go.name = "MercReplaceConfirmPopup";
        // 借底不借逻辑：装备替换的控制器必须干掉，否则它也在同一个 Root 上抢按钮/刷内容
        var stale = go.GetComponent<EquipReplaceConfirmUI>();
        if (stale != null) Destroy(stale);

        var canvas = go.GetComponent<Canvas>();
        if (canvas == null) canvas = go.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, SortOrder);
        DontDestroyOnLoad(go);

        var ui = go.GetComponent<MercReplaceConfirmUI>();
        if (ui == null) ui = go.AddComponent<MercReplaceConfirmUI>();
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
        Bind();
        if (_root != null) _root.SetActive(false);
    }

    void OnDestroy()
    {
        if (_inst == this) _inst = null;
    }

    // ============================================================
    // 绑定 / 建三栏
    // ============================================================

    void Bind()
    {
        _root = transform.Find("Root")?.gameObject ?? gameObject;
        _panel = UiFind.Deep(_root.transform, "Panel");
        _colNew = UiFind.Deep(_root.transform, "ColumnNew");
        _colOldA = UiFind.Deep(_root.transform, "ColumnOld");
        _title = GetText(UiFind.Deep(_root.transform, "Title"));
        _verdict = GetText(UiFind.Deep(_root.transform, "Verdict"));
        _cancelBtn = UiFind.Deep(_root.transform, "KeepButton")?.GetComponent<Button>();
        _dimBtn = UiFind.Deep(_root.transform, "Dim")?.GetComponent<Button>();

        if (_panel == null) Debug.LogError("[MercReplace] 预制体缺少 Panel，三栏摆不出来");
        if (_colNew == null) Debug.LogError("[MercReplace] 预制体缺少 ColumnNew");
        if (_colOldA == null) Debug.LogError("[MercReplace] 预制体缺少 ColumnOld");
        if (_cancelBtn == null) Debug.LogError("[MercReplace] 预制体缺少 KeepButton");
    }

    /// <summary>
    /// 只建一次：克隆第三栏 + 两颗「替换」钮，三栏/三钮统一摆位。
    /// 预制体本身一个字节不动（运行时克隆，Unity 自己生成 fileID）。
    /// </summary>
    void EnsureBuilt()
    {
        if (_built) return;
        if (_panel == null || _colOldA == null || _cancelBtn == null) return;

        // 第三栏：克隆 ColumnOld（与另两栏同结构：Iconbg/Icon/Name/Info/Desc）
        var colB = Instantiate(_colOldA.gameObject, _panel);
        colB.name = "ColumnOld2";
        _colOldB = colB.transform;

        // 两颗「替换」钮：克隆预制体现成的 ReplaceButton（同一套底图/字号）
        var srcBtn = UiFind.Deep(_root.transform, "ReplaceButton")?.GetComponent<Button>();
        if (srcBtn == null)
        {
            Debug.LogError("[MercReplace] 预制体缺少 ReplaceButton，无法克隆「替换」钮");
            _built = true;
            return;
        }
        _btnA = CloneButton(srcBtn, "ReplaceBtnA");
        _btnB = CloneButton(srcBtn, "ReplaceBtnB");
        srcBtn.gameObject.SetActive(false);   // 原按钮不用，改由两颗克隆各管一栏

        // 三栏摆位（顶对齐，x 均匀）
        PlaceColumn(_colNew, -ColX);
        PlaceColumn(_colOldA, 0f);
        PlaceColumn(_colOldB, ColX);

        // 三颗钮一行：左「替换」/ 中「替换」/ 右「不要了」（原 KeepButton 挪到右位）
        PlaceButton(_btnA, -ColX);
        PlaceButton(_btnB, 0f);
        PlaceButton(_cancelBtn, ColX);
        SetLabel(_cancelBtn, "不要了");

        _btnA.onClick.AddListener(() => Answer(0));
        _btnB.onClick.AddListener(() => Answer(1));
        _cancelBtn.onClick.RemoveAllListeners();
        _cancelBtn.onClick.AddListener(() => Answer(-1));
        // 点暗带也当「不要了」：玩家必须给个结果，但不要卡死；与装备替换「不许点空白关」不同，
        // 这里关掉等于放弃本次奖励，语义明确，不需要二次确认。
        if (_dimBtn != null)
        {
            _dimBtn.onClick.RemoveAllListeners();
            _dimBtn.onClick.AddListener(() => Answer(-1));
        }

        _built = true;
    }

    Button CloneButton(Button src, string name)
    {
        var go = Instantiate(src.gameObject, _panel);
        go.name = name;
        go.SetActive(true);
        var btn = go.GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        return btn;
    }

    static void PlaceColumn(Transform col, float x)
    {
        var rt = col as RectTransform;
        if (rt == null) return;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, ColY);
        rt.localScale = new Vector3(ColScale, ColScale, 1f);
    }

    static void PlaceButton(Button btn, float x)
    {
        var rt = btn.transform as RectTransform;
        if (rt == null) return;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, BtnY);
        rt.localScale = new Vector3(BtnScale, BtnScale, 1f);
    }

    static void SetLabel(Button btn, string text)
    {
        if (btn == null) return;
        var t = btn.transform.Find("Label");
        var label = t != null ? t.GetComponent<Text>() : btn.GetComponentInChildren<Text>(true);
        if (label != null) label.text = text;
    }

    // ============================================================
    // 填充
    // ============================================================

    void Fill()
    {
        if (_newEntry == null) return;
        var mercs = RunLoadout.Mercs();

        if (_title != null) _title.text = "换下谁？";
        if (_verdict != null) _verdict.text = "点哪位就换下哪位，新佣兵顶上";

        // 第一栏 = 新佣兵（只读，没有替换钮）
        FillColumn(_colNew, _newEntry, isNew: true);

        // 后两栏 = 现有两位（有替换钮）；没有第二位就把整栏和按钮藏掉
        FillColumn(_colOldA, mercs != null && mercs.Count > 0 ? mercs[0] : null, isNew: false);
        bool hasB = mercs != null && mercs.Count > 1;
        FillColumn(_colOldB, hasB ? mercs[1] : null, isNew: false);
        if (_colOldB != null) _colOldB.gameObject.SetActive(hasB);
        if (_btnB != null) _btnB.gameObject.SetActive(hasB);

        // 只有一位（理论不该发生：满员才弹）→ 左钮居中，别摆空位
        if (!hasB && _btnA != null) PlaceButton(_btnA, 0f);

        if (_btnA != null) _btnA.gameObject.SetActive(mercs != null && mercs.Count > 0);

        if (_root != null)
        {
            _root.SetActive(true);
            transform.SetAsLastSibling();
            GameFonts.ApplyToHierarchy(transform);
        }
    }

    void FillColumn(Transform col, RunMercEntry e, bool isNew)
    {
        if (col == null) return;

        if (e == null)
        {
            SetText(col, "Name", "无");
            SetText(col, "Info", "");
            SetText(col, "Desc", "");
            SetIconBg(col, MercRosterDefs.MercRarity.Common, false);
            SetIcon(col, null);
            return;
        }

        bool hasRoster = MercRosterDefs.TryGetByHireId(e.hireId, out var def);
        if (!hasRoster) hasRoster = MercRosterDefs.TryGetByAssetId(e.mercId, out def);
        var rarity = hasRoster ? def.Rarity : MercRosterDefs.MercRarity.Common;

        string name = !string.IsNullOrEmpty(e.displayName)
            ? e.displayName
            : (hasRoster && !string.IsNullOrEmpty(def.Name) ? def.Name : e.mercId);
        SetText(col, "Name", name);

        string rarityName = SkillRarityUtil.DisplayName(SkillRarityUtil.FromMerc(rarity));
        int star = Mathf.Clamp(e.star, 1, 5);
        SetText(col, "Info", rarityName + "　★" + star);

        int power = PowerCalc.MercPowerOf(
            string.IsNullOrEmpty(e.hireId) ? e.mercId : e.hireId, e.mercId, Mathf.Max(1, e.level));
        string desc = "战力 " + power;
        if (isNew) desc += "\n新获得";
        else desc += e.fallen ? "\n已阵亡" : "\n在场";
        SetText(col, "Desc", desc);

        // 名字/稀有度行跟稀有度同色（与头像栏、卡面同一张 RarityPalette）
        var nameT = GetText(UiFind.Deep(col, "Name"));
        if (nameT != null) nameT.color = RarityPalette.Get(rarity);
        var infoT = GetText(UiFind.Deep(col, "Info"));
        if (infoT != null) infoT.color = RarityPalette.Get(rarity);

        SetIcon(col, MercPortraitSprites.GetHead(string.IsNullOrEmpty(e.hireId) ? e.mercId : e.hireId));
        SetIconBg(col, rarity, true);

        // 装备专用的「战力行 / 新角标」在本弹窗没有意义：显式关掉，避免克隆栏带着残留
        var zhanli = UiFind.Deep(col, "zhanli");
        if (zhanli != null) zhanli.gameObject.SetActive(false);
        var newMark = UiFind.Deep(col, "NewMark");
        if (newMark != null) newMark.gameObject.SetActive(false);
    }

    static void SetText(Transform col, string node, string text)
    {
        var t = GetText(UiFind.Deep(col, node));
        if (t != null) t.text = text;
    }

    static Text GetText(Transform t) => t != null ? t.GetComponent<Text>() : null;

    static void SetIcon(Transform col, Sprite sp)
    {
        var icon = UiFind.Deep(col, "Icon")?.GetComponent<Image>();
        if (icon == null) return;
        icon.sprite = sp;
        icon.enabled = sp != null;
        icon.preserveAspect = true;   // 佣兵头像是方的，别被框拉扁
    }

    static void SetIconBg(Transform col, MercRosterDefs.MercRarity rarity, bool on)
    {
        var bg = UiFind.Deep(col, "Iconbg")?.GetComponent<Image>();
        if (bg == null) return;
        if (!on) { bg.sprite = null; bg.enabled = false; return; }
        // 佣兵头像框真源 = MercHireSession.LoadPortraitFrame（与战斗头像栏同一套三档框）
        var sp = MercHireSession.LoadPortraitFrame(rarity);
        bg.sprite = sp;
        bg.enabled = sp != null;
    }

    // ============================================================
    // 收场
    // ============================================================

    void Update()
    {
        if (_root == null || !_root.activeSelf || _answered) return;
        _timer += Time.unscaledDeltaTime;
        int left = Mathf.Max(0, Mathf.CeilToInt(TimeoutSec - _timer));
        if (_verdict != null && left != _lastLeft)
        {
            _lastLeft = left;
            _verdict.text = "点哪位就换下哪位，新佣兵顶上\n<b>" + left + "</b> 秒后自动换下较弱的";
        }
        if (_timer >= TimeoutSec) Answer(AutoPickWeaker());
    }

    int _lastLeft = -1;

    /// <summary>超时兜底：有阵亡的换阵亡的，否则换战力低的（与装备替换「超时按战力选」同精神）。</summary>
    int AutoPickWeaker()
    {
        var mercs = RunLoadout.Mercs();
        if (mercs == null || mercs.Count == 0) return -1;
        if (mercs.Count == 1) return 0;

        var a = mercs[0];
        var b = mercs[1];
        if (a != null && a.fallen && (b == null || !b.fallen)) return 0;
        if (b != null && b.fallen && (a == null || !a.fallen)) return 1;

        int pa = a == null ? 0 : PowerCalc.MercPowerOf(
            string.IsNullOrEmpty(a.hireId) ? a.mercId : a.hireId, a.mercId, Mathf.Max(1, a.level));
        int pb = b == null ? 0 : PowerCalc.MercPowerOf(
            string.IsNullOrEmpty(b.hireId) ? b.mercId : b.hireId, b.mercId, Mathf.Max(1, b.level));
        return pa <= pb ? 0 : 1;
    }

    void Answer(int index)
    {
        if (_answered) return;
        _answered = true;
        Debug.Log("[MercReplace] 玩家选择：" + (index < 0 ? "不要了" : "换下第 " + (index + 1) + " 位"));
        if (_root != null) _root.SetActive(false);
        var cb = _onPick;
        _onPick = null;
        cb?.Invoke(index);
    }

    void OpenRoot()
    {
        if (_root == null) return;
        _root.SetActive(true);
        transform.SetAsLastSibling();
        GameFonts.ApplyToHierarchy(transform);
        var c = GetComponent<Canvas>();
        if (c != null) UICanvasSetup.RefreshPopup(c, SortOrder);
    }
}
