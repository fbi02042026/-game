using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 通用「获得奖励」弹窗（2026-09-23 新增，2026-09-25 重写排版）。
///
/// 用法：
///   RewardPopupUI.Show(iconSprite, "钻石 ×100");                       // 单个
///   RewardPopupUI.Show(new List<RewardPopupUI.Entry> { ... });         // 多个（面板自动变高）
///   RewardPopupUI.Show(new List<RewardPopupUI.Entry> {
///       RewardPopupUI.Entry.Fragment(MercRosterDefs.MercRarity.Rare, "H001", "碎片 ×10") });  // 碎片走主人碎片预制体
///
/// 结构：预制体 Resources/Prefabs/UI/RewardPopup（主人手摆，自带画布），
///       面板底图九宫格**只上下拉伸**（宽度永远是预制体的 720）；
///       ≤4 个奖励整体保持原样，>4 个分成多行并把框体上下拉伸。
///       找不到预制体时用纯色块兜底，兜底层级与预制体一致，排版代码两条路通用。
/// 字体：Open 时 GameFonts.ApplyToHierarchy 统一刷（中文 fusion-pixel / 数字 PixelFont）。
/// 层级：画布 sortingOrder 用 <see cref="GameConfig.UiSort.RewardPopup"/>(905)——高于 TownPopup(900)，
///       免得与每日登录界面同为 900 时排序不定被盖住；低于 BattlePopup(920) 与 Toast(11000)。
///
/// 铁律：**以主人预制体里的效果为准** —— 尺寸/位置/美术图一律从「首次打开时捕获的预制体原始几何」
/// （见 <see cref="Geo"/> / <see cref="BaseGeo"/>）出发重算，不允许用代码常量覆盖主人调好的值。
/// </summary>
public class RewardPopupUI : MonoBehaviour
{
    public static RewardPopupUI Instance { get; private set; }

    const string PrefabPath = "Prefabs/UI/RewardPopup";

    /// <summary>一条奖励 = 图标 + 文案（文案自带数量，如「钻石 ×100」）。</summary>
    public struct Entry
    {
        public Sprite icon;
        public string label;
        /// <summary>true = 佣兵碎片：用主人碎片预制体（MercFragmentCardUI）渲染，不走 icon。</summary>
        public bool isFragment;
        public MercRosterDefs.MercRarity fragRarity;
        public string fragHireId;

        public Entry(Sprite icon, string label)
        {
            this.icon = icon;
            this.label = label;
            isFragment = false;
            fragRarity = MercRosterDefs.MercRarity.Common;
            fragHireId = null;
        }

        /// <summary>碎片条目：rarity 决定底框，hireId（或 assetId）决定头像。</summary>
        public static Entry Fragment(MercRosterDefs.MercRarity rarity, string hireId, string label)
        {
            var e = new Entry(null, label);
            e.isFragment = true;
            e.fragRarity = rarity;
            e.fragHireId = hireId;
            return e;
        }
    }

    // ---- 排版常量：只用来算「相对预制体原始值的增量」，绝不覆盖预制体几何 ----
    // 2026-09-25 主人定版：≤4 个一行、整体保持原样；>4 个每行 4 个，框体上下拉伸。
    const int PER_ROW_MAX = 4;      // 一行最多 4 个（4×109 + 3×12 = 472 ≤ 面板 720，不会溢出）
    const float ROW_GAP = 4f;       // 行间距：主人要「上下拉伸」，不留大缝
    const float COL_GAP = 12f;      // 列间距

    /// <summary>
    /// 兜底（无预制体）时用的几何值 —— 与主人预制体一一对应，保证兜底和预制体走同一套排版，
    /// 不出现「两套互相打架的常量」。
    /// </summary>
    const float FB_PANEL_W = 720f;
    const float FB_PANEL_H = 537f;
    const float FB_TITLE_W = 360f;
    const float FB_TITLE_H = 80f;
    const float FB_TITLE_Y = 123f;
    const float FB_BTN_W = 363f;
    const float FB_BTN_H = 123f;
    const float FB_BTN_Y = -225f;
    const float FB_CELL_W = 109f;
    const float FB_CELL_H = 108f;
    const float FB_CELL_Y = -55f;   // 主人 CellTemplate 的原始 y：面板中心下方 55
    const float FB_ICON = 84f;
    const float FB_ICON_X = 0.9f;
    const float FB_ICON_Y = 1.5f;
    const float FB_LABEL_W = 130f;
    const float FB_LABEL_H = 30f;
    const float FB_LABEL_Y = -22.6f;

    /// <summary>
    /// 以主人预制体几何为准（2026-09-25 主人原话：「弹窗的字和碎片还有按钮没有自适应位置和大小，
    /// 那个确定按钮也没有按我的预制体来」）。
    /// 关掉 = 回到改动前的老排版（格子挂回 Grid、面板缩成 540），只用于 A/B 对比。
    /// </summary>
    public static bool RespectPrefabGeometry = true;

    /// <summary>整屏点击关闭：给预制体已有的全屏 Mask 节点挂 Button（不新建节点）。关掉则只能点确定按钮。</summary>
    public static bool EnableTapAnywhereToClose = true;

    /// <summary>
    /// B12 冲击力开关（2026-09-25 主人要求：领取弹窗出现的时候要有冲击力）。
    /// 一键关掉：在任意地方写 <c>RewardPopupUI.EnableImpactAnim = false;</c> 即完全回到改动前的行为——
    /// 面板 localScale 恒 1、alpha 恒 1，不再跑任何协程（动画本身也只动 scale / alpha）。
    /// </summary>
    public static bool EnableImpactAnim = true;

    /// <summary>整体缩放系数（主人 2026-09-26 要求：获得奖励弹窗整体缩小 30%）。关掉开关即回到预制体原始大小。</summary>
    public const float OverallScale = 0.7f;
    /// <summary>一键回退开关：置 false 完全还原预制体尺寸。</summary>
    public static bool EnableOverallScale = true;

    // B12 入场动画参数（只动 scale 与 alpha，**不改面板尺寸 / 位置 / 九宫格**，主人铁律）
    const float IMPACT_TIME = 0.24f;    // 总时长（秒）
    const float IMPACT_FROM = 0.72f;    // 起始缩放（小 → 弹出来）
    const float IMPACT_OVER = 1.06f;    // 过冲峰值（轻微超出，制造冲击感）

    /// <summary>一个 RectTransform 的原始几何快照：首次打开（或首次排版）时从预制体实例捕获。</summary>
    struct Geo
    {
        public bool Has;
        public Vector2 AnchorMin;
        public Vector2 AnchorMax;
        public Vector2 Pivot;
        public Vector2 AnchoredPos;
        public Vector2 SizeDelta;

        public static Geo Capture(RectTransform rt)
        {
            if (rt == null) return default;
            var g = new Geo { Has = true };
            g.AnchorMin = rt.anchorMin;
            g.AnchorMax = rt.anchorMax;
            g.Pivot = rt.pivot;
            g.AnchoredPos = rt.anchoredPosition;
            g.SizeDelta = rt.sizeDelta;
            return g;
        }

        /// <summary>原样还原（每次排版都先还原再算增量，杜绝偏移累积）。</summary>
        public void Apply(RectTransform rt)
        {
            if (!Has || rt == null) return;
            rt.anchorMin = AnchorMin;
            rt.anchorMax = AnchorMax;
            rt.pivot = Pivot;
            rt.anchoredPosition = AnchoredPos;
            rt.sizeDelta = SizeDelta;
        }
    }

    /// <summary>主人预制体里各节点的原始几何（Panel/Title/ConfirmBtn/Grid/CellTemplate/Icon/Label）。</summary>
    class BaseGeo
    {
        public Geo Panel;
        public Geo Title;
        public Geo Confirm;
        public Geo Grid;
        public Geo Cell;
        public Geo Icon;
        public Geo Label;
    }

    GameObject _panel;
    RectTransform _grid;
    RectTransform _cellTemplate;
    Button _confirmBtn;
    RectTransform _titleRt;
    RectTransform _confirmRt;
    Button _maskBtn;
    // 本次生成的格子（挂在 Panel 下；Panel 还挂着 Title/ConfirmBtn/Grid，所以只能按名单删，不能清空子节点）
    readonly List<GameObject> _cells = new List<GameObject>();

    BaseGeo _base;
    bool _baseCaptured;

    // B12: 入场动画协程句柄（同一时刻只允许一个动画在跑）；面板上的 CanvasGroup（没有就运行时补）
    Coroutine _impactCo;
    CanvasGroup _panelGroup;
    // B12: 面板在预制体里的原始 localScale（首次动画前记一次）——动画以它为基准，绝不写死 1
    Vector3 _panelBaseScale = Vector3.one;
    bool _panelScaleCaptured;

    // ============================================================
    // 对外入口
    // ============================================================

    public static void Show(Sprite icon, string label)
        => Show(new List<Entry> { new Entry(icon, label) });

    public static void Show(List<Entry> items)
    {
        if (items == null || items.Count == 0) return;
        Ensure().Open(items);
    }

    static RewardPopupUI Ensure()
    {
        if (Instance != null) return Instance;

        // 完整弹窗预制体优先（自带画布）
        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab != null && prefab.GetComponent<Canvas>() != null)
        {
            var go = UnityEngine.Object.Instantiate(prefab);
            go.name = "RewardPopup";
            DontDestroyOnLoad(go);
            var ui = go.GetComponent<RewardPopupUI>();
            if (ui == null) ui = go.AddComponent<RewardPopupUI>();
            ui.BuildFromPrefab();
            return ui;
        }

        // 兜底：纯代码生成（预制体没生成过时也能用）
        var fb = new GameObject("RewardPopup", typeof(RectTransform));
        DontDestroyOnLoad(fb);
        var ui2 = fb.AddComponent<RewardPopupUI>();
        ui2.BuildFallback();
        return ui2;
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

    // ============================================================
    // 构建
    // ============================================================

    void BuildFromPrefab()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) { BuildFallback(); return; }
        // 运行时补 GraphicRaycaster / 相机 / 缩放器（预制体里没有也不许加，铁律：不碰 .prefab）
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.RewardPopup);

        var panelT = FindChild(transform, "Panel");
        _panel = panelT != null ? panelT.gameObject : null;
        var gridT = panelT != null ? FindChild(panelT, "Grid") : null;
        _grid = gridT as RectTransform;
        var tplT = FindChild(transform, "CellTemplate");
        _cellTemplate = tplT as RectTransform;
        var btnT = panelT != null ? FindChild(panelT, "ConfirmBtn") : null;

        if (_panel == null || _grid == null || _cellTemplate == null || btnT == null)
        {
            Debug.LogError("[RewardPopup] 预制体缺 Panel/Grid/CellTemplate/ConfirmBtn，回退纯色兜底");
            BuildFallback();
            return;
        }

        SetupConfirmButton(btnT);     // B4-1：修预制体自带的确定按钮
        SetupTapAnywhereToClose();    // B4-2：点屏幕任意地方关闭（用预制体已有的 Mask 节点）

        _cellTemplate.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    /// <summary>
    /// B4-1：确定按钮。预制体里**已经带了 Button**（m_Interactable=1、m_TargetGraphic 已设），
    /// 所以只取不建（取不到才补），绝不 AddComponent 一个新的把主人的配置顶掉。
    /// 之前「点不了」的三个成因一并处理：①transition 显式设 None，避免颜色/精灵过渡把交互态改坏；
    /// ②onClick RemoveAllListeners 后再挂 Hide，避免重复 Show 挂上多份监听；
    /// ③子节点 Text (Legacy) 的 raycastTarget 关掉，避免文字层抢射线。
    /// </summary>
    void SetupConfirmButton(Transform btnT)
    {
        if (btnT == null) return;
        _confirmRt = btnT as RectTransform;
        _confirmBtn = btnT.GetComponent<Button>();
        if (_confirmBtn == null) _confirmBtn = btnT.gameObject.AddComponent<Button>();

        _confirmBtn.transition = Selectable.Transition.None;
        _confirmBtn.interactable = true;
        _confirmBtn.onClick.RemoveAllListeners();
        _confirmBtn.onClick.AddListener(Hide);

        // 按钮本体必须能吃到射线：GraphicRaycaster 只认 raycastTarget=1 的 Graphic
        var own = btnT.GetComponent<Graphic>();
        if (own != null) own.raycastTarget = true;
        if (_confirmBtn.targetGraphic != null) _confirmBtn.targetGraphic.raycastTarget = true;

        // 子节点（Text (Legacy) 等）只做展示，关掉射线
        SetRaycastOff(btnT, false);
    }

    /// <summary>
    /// B4-2：点屏幕任意地方关闭。用预制体已有的全屏 <c>Mask</c> 节点（不新建节点），
    /// 它的 Image raycastTarget=1 会吃掉空白处点击 —— 正是我们要的。
    /// 层级复核：Mask 是 root 的第一个子节点，Panel 在它之后（后画的在上），
    /// 所以 Mask 在最底层，**不会挡住 Panel 上的确定按钮**，只有点到面板外才触发关闭。
    /// </summary>
    void SetupTapAnywhereToClose()
    {
        if (!EnableTapAnywhereToClose) return;
        var maskT = FindChild(transform, "Mask");
        if (maskT == null) return;

        var img = maskT.GetComponent<Image>();
        if (img != null) img.raycastTarget = true;

        _maskBtn = maskT.GetComponent<Button>();
        if (_maskBtn == null) _maskBtn = maskT.gameObject.AddComponent<Button>();
        _maskBtn.transition = Selectable.Transition.None;
        _maskBtn.interactable = true;
        _maskBtn.onClick.RemoveAllListeners();
        _maskBtn.onClick.AddListener(Hide);
    }

    /// <summary>兜底：无预制体时用色块搭一个能用的（层级与预制体一致，排版逻辑与预制体版完全相同）。</summary>
    void BuildFallback()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.RewardPopup);

        var rt = (RectTransform)transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        // 遮罩：全屏，同时兼作「点任意地方关闭」的按钮节点（与预制体同款）
        var mask = CreateImg(transform, "Mask", new Color(0f, 0f, 0f, 0.55f));
        Stretch(mask.rectTransform);

        _panel = CreateImg(transform, "Panel", new Color(0.10f, 0.10f, 0.18f, 1f)).gameObject;
        var prt = _panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = Vector2.zero;
        prt.sizeDelta = new Vector2(FB_PANEL_W, FB_PANEL_H);

        // Grid：与预制体同款（贴 Panel 顶边、零高）；格子不再挂它下面，仅保留结构一致
        var gridGo = new GameObject("Grid", typeof(RectTransform));
        gridGo.transform.SetParent(_panel.transform, false);
        _grid = (RectTransform)gridGo.transform;
        _grid.anchorMin = _grid.anchorMax = new Vector2(0.5f, 1f);
        _grid.pivot = new Vector2(0.5f, 1f);
        _grid.anchoredPosition = Vector2.zero;
        _grid.sizeDelta = new Vector2(500f, 0f);

        var title = CreateTxt(_panel.transform, "Title", "获得奖励", 40, TextAnchor.MiddleCenter);
        var trt = title.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
        trt.pivot = new Vector2(0.5f, 0.5f);
        trt.anchoredPosition = new Vector2(0f, FB_TITLE_Y);
        trt.sizeDelta = new Vector2(FB_TITLE_W, FB_TITLE_H);

        // 模板格子（色块版，几何与主人预制体一致）
        var tpl = CreateImg(transform, "CellTemplate", new Color(0.16f, 0.14f, 0.10f, 1f));
        tpl.raycastTarget = false;
        _cellTemplate = tpl.rectTransform;
        _cellTemplate.anchorMin = _cellTemplate.anchorMax = new Vector2(0.5f, 0.5f);
        _cellTemplate.pivot = new Vector2(0.5f, 0.5f);
        _cellTemplate.anchoredPosition = new Vector2(0f, FB_CELL_Y);
        _cellTemplate.sizeDelta = new Vector2(FB_CELL_W, FB_CELL_H);

        var icon = CreateImg(tpl.transform, "Icon", new Color(1f, 1f, 1f, 0.12f));
        icon.raycastTarget = false;
        var irt = icon.rectTransform;
        irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
        irt.pivot = new Vector2(0.5f, 0.5f);
        irt.anchoredPosition = new Vector2(FB_ICON_X, FB_ICON_Y);
        irt.sizeDelta = new Vector2(FB_ICON, FB_ICON);

        var lbl = CreateTxt(tpl.transform, "Label", "", 20, TextAnchor.MiddleCenter);
        lbl.color = new Color(0.95f, 0.93f, 0.88f, 1f);
        var lrt = lbl.rectTransform;
        lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0f);
        lrt.pivot = new Vector2(0.5f, 0f);
        lrt.anchoredPosition = new Vector2(0f, FB_LABEL_Y);
        lrt.sizeDelta = new Vector2(FB_LABEL_W, FB_LABEL_H);
        tpl.gameObject.SetActive(false);

        var btn = CreateImg(_panel.transform, "ConfirmBtn", new Color(0.72f, 0.50f, 0.14f, 1f));
        var brt = btn.rectTransform;
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.pivot = new Vector2(0.5f, 0.5f);
        brt.anchoredPosition = new Vector2(0f, FB_BTN_Y);
        brt.sizeDelta = new Vector2(FB_BTN_W, FB_BTN_H);
        var btxt = CreateTxt(btn.transform, "Text (Legacy)", "确 定", 55, TextAnchor.MiddleCenter);
        Stretch(btxt.rectTransform);
        btxt.color = new Color(0.16f, 0.06f, 0.04f, 1f);

        SetupConfirmButton(btn.transform);
        SetupTapAnywhereToClose();

        gameObject.SetActive(false);
    }

    // ============================================================
    // 打开 / 排版
    // ============================================================

    void Open(List<Entry> items)
    {
        if (_panel == null) BuildFallback();
        gameObject.SetActive(true);
        // DDOL 弹窗每次 Show 重新绑一次相机（切场景后旧相机会失效）
        var cv = GetComponent<Canvas>();
        if (cv != null) UICanvasSetup.RefreshPopup(cv, GameConfig.UiSort.RewardPopup);
        Layout(items);                              // 尺寸先自己定好，再交给字体/动画
        transform.SetAsLastSibling();
        GameFonts.ApplyToHierarchy(transform);
        // B12: 入场冲击力放在最后——此时面板尺寸/位置已由 Layout 定好，动画只叠 scale 与 alpha
        PlayImpact();
    }

    public void Hide()
    {
        if (!gameObject.activeSelf) return;         // 幂等：同帧被确定按钮 / 整屏点击多处调用只生效一次
        ResetImpact();          // B12: 关掉前把动画状态清干净，避免下次打开带着旧进度
        gameObject.SetActive(false);
    }

    /// <summary>
    /// B12: 面板入场「弹一下」——localScale 由 0.72 冲到 1.06（轻微过冲）再回到 1，
    /// 同时 CanvasGroup.alpha 0 → 1，总时长 IMPACT_TIME 秒。
    /// CanvasGroup 优先用面板上已有的，没有才运行时 AddComponent（运行时加组件不算改预制体）。
    /// 重复 Show 时先 StopCoroutine，保证同一时刻只有一个动画协程在跑。
    /// 只动 localScale 与 alpha，绝不碰 Panel 的 sizeDelta / anchoredPosition（主人铁律）。
    /// </summary>
    void PlayImpact()
    {
        if (_panel == null) return;
        if (!EnableImpactAnim) { ResetImpact(); return; }
        if (_impactCo != null) StopCoroutine(_impactCo);
        _impactCo = StartCoroutine(CoImpact());
    }

    /// <summary>B12: 把面板还原成「无动画」状态（关开关 / 关闭弹窗时用）。</summary>
    void ResetImpact()
    {
        if (_impactCo != null) { StopCoroutine(_impactCo); _impactCo = null; }
        ApplyPanelScale();   // 统一走一处：关掉整体缩放开关也能正确还原
        if (_panelGroup != null) _panelGroup.alpha = 1f;
    }

    /// <summary>面板的目标缩放 = 预制体原始缩放 × 整体系数（未捕获原始值时用 Vector3.one 代替）。只此一处产出缩放，避免与 B12 动画互相覆盖。</summary>
    Vector3 PanelTargetScale()
        => (_panelScaleCaptured ? _panelBaseScale : Vector3.one) * (EnableOverallScale ? OverallScale : 1f);

    /// <summary>把目标缩放写到 Panel 上（Panel 为空直接什么都不做）。只动 localScale，不碰任何尺寸 / 位置。</summary>
    void ApplyPanelScale()
    {
        if (_panel == null) return;
        _panel.transform.localScale = PanelTargetScale();
    }

    System.Collections.IEnumerator CoImpact()
    {
        if (_panelGroup == null)
        {
            _panelGroup = _panel.GetComponent<CanvasGroup>();
            if (_panelGroup == null) _panelGroup = _panel.AddComponent<CanvasGroup>();
        }
        var tr = _panel.transform;
        if (!_panelScaleCaptured)
        {
            _panelBaseScale = tr.localScale;   // 记下预制体缩放，动画从它出发、也回到它
            _panelScaleCaptured = true;
        }
        // 整体系数在这里一次性并入基准：动画前后都回到同一个「目标缩放」，不会把 0.7 打回 1
        var target = PanelTargetScale();
        float t = 0f;
        while (t < IMPACT_TIME)
        {
            t += Time.unscaledDeltaTime;      // 不吃游戏暂停 / 时间缩放，UI 动画永远跑完
            float p = Mathf.Clamp01(t / IMPACT_TIME);
            // 两段线性：前 70% 冲到过冲峰值，后 30% 回落到 1（比缓动曲线更好调、好回退）
            float s = p < 0.7f
                ? Mathf.Lerp(IMPACT_FROM, IMPACT_OVER, p / 0.7f)
                : Mathf.Lerp(IMPACT_OVER, 1f, (p - 0.7f) / 0.3f);
            tr.localScale = new Vector3(target.x * s, target.y * s, target.z);
            _panelGroup.alpha = Mathf.Clamp01(p * 1.6f);   // 透明度比缩放更快到位，避免"半透明软弹出"
            yield return null;
        }
        ApplyPanelScale();
        _panelGroup.alpha = 1f;
        _impactCo = null;
    }

    /// <summary>
    /// 首次打开时把主人预制体（或兜底层级）的原始几何记进 <see cref="_base"/>。
    /// 之后每次排版都从 base 重算，不硬编码坐标、不累积偏移。
    /// </summary>
    void EnsureBase()
    {
        if (_baseCaptured) return;
        var panelT = _panel != null ? _panel.transform : null;

        // T2：顺手把 Panel 的预制体原始 localScale 也登记一份（**不乘系数**，原始值绝不能被 0.7 污染）。
        // 这样即使 EnableImpactAnim=false、CoImpact 的捕获块没跑过，整体缩放也有正确基准。
        if (!_panelScaleCaptured && panelT != null)
        {
            _panelBaseScale = panelT.localScale;
            _panelScaleCaptured = true;
        }

        _titleRt = FindChild(panelT, "Title") as RectTransform;
        _confirmRt = _confirmBtn != null
            ? _confirmBtn.transform as RectTransform
            : FindChild(panelT, "ConfirmBtn") as RectTransform;

        var b = new BaseGeo
        {
            Panel = Geo.Capture(_panel != null ? _panel.transform as RectTransform : null),
            Title = Geo.Capture(_titleRt),
            Confirm = Geo.Capture(_confirmRt),
            Grid = Geo.Capture(_grid),
            Cell = Geo.Capture(_cellTemplate),
            Icon = Geo.Capture(_cellTemplate != null ? FindChild(_cellTemplate, "Icon") as RectTransform : null),
            Label = Geo.Capture(_cellTemplate != null ? FindChild(_cellTemplate, "Label") as RectTransform : null),
        };
        _base = b;
        _baseCaptured = true;
    }

    /// <summary>清空旧格子 → 按数量生成 → 面板九宫格拉伸到合适高度（全部相对 base 重算）。</summary>
    void Layout(List<Entry> items)
    {
        if (_panel == null) BuildFallback();
        if (_panel == null || _cellTemplate == null) return;

        EnsureBase();
        ClearCells();

        int n = items != null ? items.Count : 0;
        if (n <= 0) { RestoreBase(); return; }

        if (!RespectPrefabGeometry) { LayoutLegacy(items); return; }

        int perRow = Mathf.Min(PER_ROW_MAX, n);
        int rows = Mathf.Max(1, Mathf.CeilToInt((float)n / (float)perRow));

        // B2：格子尺寸一律取自 base（主人定版 109×108），行距 = 格子高 + ROW_GAP
        float cellW = _base.Cell.Has ? _base.Cell.SizeDelta.x : FB_CELL_W;
        float cellH = _base.Cell.Has ? _base.Cell.SizeDelta.y : FB_CELL_H;
        float rowStep = cellH + ROW_GAP;
        float grow = (rows - 1) * rowStep;   // >4 个才 >0：框体上下拉伸的总量
        float half = grow * 0.5f;            // 面板是居中对称拉伸的，上下各分一半

        // 面板：只改高度（九宫格 m_Type=1，中间被拉伸、边框不变形）；宽度永远是 base 的 720
        var prt = _panel.transform as RectTransform;
        if (prt != null && _base.Panel.Has)
            prt.sizeDelta = new Vector2(_base.Panel.SizeDelta.x, _base.Panel.SizeDelta.y + grow);

        // Title 上移半程、ConfirmBtn 下移半程：先 Apply 回 base，再叠半程增量 —— 从 base 重算，不累积
        if (_titleRt != null)
        {
            _base.Title.Apply(_titleRt);
            _titleRt.anchoredPosition = _base.Title.AnchoredPos + new Vector2(0f, half);
        }
        if (_confirmRt != null)
        {
            _base.Confirm.Apply(_confirmRt);
            _confirmRt.anchoredPosition = _base.Confirm.AnchoredPos - new Vector2(0f, half);
        }

        Vector2 cellAnchor = _base.Cell.Has ? _base.Cell.AnchorMin : new Vector2(0.5f, 0.5f);
        Vector2 cellPivot = _base.Cell.Has ? _base.Cell.Pivot : new Vector2(0.5f, 0.5f);
        Vector2 cellSize = _base.Cell.Has ? _base.Cell.SizeDelta : new Vector2(FB_CELL_W, FB_CELL_H);
        float baseY = _base.Cell.Has ? _base.Cell.AnchoredPos.y : FB_CELL_Y;   // 主人原值 -55

        for (int i = 0; i < n; i++)
        {
            int row = i / perRow;
            int col = i % perRow;
            int inRow = (row == rows - 1) ? (n - row * perRow) : perRow;

            // B3：格子挂到 **Panel** 下（不再挂 Grid —— Grid 零高且贴在 Panel 顶边，
            // 挂过去 y 会从「面板中心−55」变成「面板顶边−55」，整整上飘约 268 像素压住标题）。
            // 因为 Panel 与 root 一样是居中的，锚点 (0.5,0.5) 下 y=−55 的屏幕位置与主人预制体完全一致。
            var cell = Instantiate(_cellTemplate.gameObject, _panel.transform, false);
            cell.name = "Cell_" + (i + 1);
            cell.SetActive(true);
            _cells.Add(cell);

            var crt = cell.transform as RectTransform;
            crt.anchorMin = cellAnchor;
            crt.anchorMax = cellAnchor;
            crt.pivot = cellPivot;
            crt.sizeDelta = cellSize;   // 主人定版 109×108，一个像素都不改

            float x = (col - (inRow - 1) * 0.5f) * (cellW + COL_GAP);
            // 面板对称拉伸后，格子块同步上移半程，最后一行才不会压到确定按钮；
            // rows==1 时 half=0，y 就是主人的原始 −55（「≤4 个整体保持原样」）。
            float y = baseY + half - row * rowStep;
            crt.anchoredPosition = new Vector2(x, y);

            FillCell(cell.transform, items[i]);
        }

        // 整体缩放在几何算完后施加；随后 PlayImpact 会在同一基准上叠加缩放动画
        ApplyPanelScale();
    }

    /// <summary>把一个格子的图标 / 文案 / 碎片卡填好（几何一律不动）。</summary>
    void FillCell(Transform cell, Entry e)
    {
        var iconT = FindChild(cell, "Icon");
        var iconImg = iconT != null ? iconT.GetComponent<Image>() : null;

        var lblT = FindChild(cell, "Label");
        if (lblT != null)
        {
            var t = lblT.GetComponent<Text>();
            if (t != null) t.text = e.label ?? "";
            // 文字只做展示：关掉射线，避免它压在确定按钮上方时把点击吃掉
            var lg = lblT.GetComponent<Graphic>();
            if (lg != null) lg.raycastTarget = false;
        }

        if (e.isFragment)
        {
            // B5：碎片走主人碎片预制体 —— 底框里的 Icon 让位（enabled=false，别留白框）
            if (iconImg != null) { iconImg.enabled = false; iconImg.raycastTarget = false; }

            // 卡片挂在格子根下；MercFragmentCardUI.Create 只换 sprite，卡片的 RectTransform（100×100）一个值都不改
            var card = MercFragmentCardUI.Create(cell, "FragmentCard", e.fragRarity, e.fragHireId);
            if (card != null)
            {
                // 卡片是纯展示层，关掉射线，保证确定按钮永远点得到
                SetRaycastOff(card.Root != null ? card.Root.transform : null, true);
            }
            else if (iconImg != null)
            {
                // 主人碎片预制体没加载到 / 开关关闭 → 退回老路（底版 sprite），不至于开天窗
                iconImg.enabled = true;
                ApplyIconSprite(iconImg, e.icon);
            }
            return;
        }

        if (iconImg != null)
        {
            iconImg.enabled = true;
            ApplyIconSprite(iconImg, e.icon);
        }
    }

    static void ApplyIconSprite(Image img, Sprite sp)
    {
        if (img == null) return;
        img.sprite = sp;
        img.preserveAspect = true;
        img.color = sp != null ? Color.white : new Color(1f, 1f, 1f, 0.12f);
    }

    /// <summary>把 base 里的面板 / 标题 / 按钮原样还原（n≤4 或没有奖励时「整体保持原样」）。</summary>
    void RestoreBase()
    {
        var prt = _panel != null ? _panel.transform as RectTransform : null;
        if (prt != null) _base.Panel.Apply(prt);
        if (_titleRt != null) _base.Title.Apply(_titleRt);
        if (_confirmRt != null) _base.Confirm.Apply(_confirmRt);
    }

    void ClearCells()
    {
        for (int i = 0; i < _cells.Count; i++)
            if (_cells[i] != null) Destroy(_cells[i]);
        _cells.Clear();
        // 老布局（RespectPrefabGeometry=false）把格子挂在 Grid 下，这里顺手清干净
        if (_grid != null)
        {
            for (int i = _grid.childCount - 1; i >= 0; i--)
            {
                var c = _grid.GetChild(i);
                if (c != null && c.name.StartsWith("Cell_")) Destroy(c.gameObject);
            }
        }
    }

    /// <summary>
    /// 回退路径（仅供 A/B 对比）：<c>RespectPrefabGeometry=false</c> 时走改动前的老排版——
    /// 面板缩成 540、格子挂回 Grid。常量全部写在这一个方法里，不污染 base 模式。
    /// </summary>
    void LayoutLegacy(List<Entry> items)
    {
        const float artScale = 0.55f;
        const float panelW = 540f;
        const float titleH = 160f;
        const float cellW = 96f;
        const float cellH = 88f;
        const float labelH = 34f;
        const float rowGap = 16f;
        const float colGap = 10f;
        const int maxPerRow = 5;
        const float topPad = 150f;
        const float bottomPad = 96f;

        int n = items.Count;
        int perRow = Mathf.Min(maxPerRow, n);
        int rows = Mathf.Max(1, Mathf.CeilToInt((float)n / (float)perRow));
        float rowH = cellH + labelH + rowGap;
        float panelH = topPad + rows * rowH + bottomPad;

        var prt = _panel.transform as RectTransform;
        prt.sizeDelta = new Vector2(panelW, panelH);

        if (_titleRt != null)
        {
            _titleRt.sizeDelta = new Vector2(835f * artScale, titleH);
            _titleRt.anchoredPosition = new Vector2(0f, panelH / 2f - 52f);
        }
        if (_confirmRt != null)
        {
            _confirmRt.anchorMin = _confirmRt.anchorMax = new Vector2(0.5f, 0.5f);
            _confirmRt.pivot = new Vector2(0.5f, 0.5f);
            _confirmRt.anchoredPosition = new Vector2(0f, -panelH / 2f + 52f);
            _confirmRt.sizeDelta = new Vector2(404f * artScale, 84f * artScale);
        }

        for (int i = 0; i < n; i++)
        {
            int row = i / perRow;
            int col = i % perRow;
            int inRow = (row == rows - 1) ? (n - row * perRow) : perRow;

            var cell = Instantiate(_cellTemplate.gameObject, _grid != null ? _grid : _panel.transform, false);
            cell.name = "Cell_" + (i + 1);
            cell.SetActive(true);
            _cells.Add(cell);
            var crt = cell.transform as RectTransform;
            float x = (col - (inRow - 1) * 0.5f) * (cellW + colGap);
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = new Vector2(x, -topPad - row * rowH);
            crt.sizeDelta = new Vector2(cellW, cellH);
            FillCell(cell.transform, items[i]);
        }
    }

    // ============================================================
    // 小工具（与 DailyLoginUI 同款）
    // ============================================================

    static Transform FindChild(Transform parent, string name)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c != null && c.name == name) return c;
        }
        return null;
    }

    /// <summary>关掉子树里的射线（includeSelf=false 时保留根节点自己的 Graphic）。</summary>
    static void SetRaycastOff(Transform root, bool includeSelf)
    {
        if (root == null) return;
        var gs = root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < gs.Length; i++)
        {
            var g = gs[i];
            if (g == null) continue;
            if (!includeSelf && g.transform == root) continue;
            g.raycastTarget = false;
        }
    }

    static Image CreateImg(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    static Text CreateTxt(Transform parent, string name, string content, int size, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
