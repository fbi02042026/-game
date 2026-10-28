using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 刘海屏 / 挖孔屏 / 底部手势条适配：把自身 RectTransform 挪进（或内缩到）Screen.safeArea 之内。
///
/// 设计约定（与项目「UI 用运行时补节点，别改预制体」一致）：
/// 本组件在代码里 AddComponent 挂到需要避开的 UI 根节点上，不碰任何 .prefab。
///
/// 算法：
///   1. Screen.safeArea（像素）按 Screen.width/height 归一化成 0~1 的上/下不安全比例；
///   2. 乘以父级 RectTransform 的高度，得到逻辑单位的内缩量（父级即满屏 Canvas 根，故成立）；
///   3. Shift / Offset（2026-10-28 起）：只挪 anchoredPosition，anchor / sizeDelta / pivot 一律不动；
///      只有 Inset 才把自身锚点强制全拉伸（anchorMin=(0,0) / anchorMax=(1,1)），用 offsetMin/offsetMax 表达矩形。
/// 左右永远不缩（竖屏设计宽度锁死），只处理上/下（由 edge 决定）。
///
/// 为什么必须「记录 base + 每次从 base 重算」：
///   ① 幂等 —— 重复 Apply 不会把内缩量累加进去；
///   ② 挂到的节点原始锚点未必是全拉伸（底部按钮栏常是 anchorMin=(0,0)/anchorMax=(1,0) 贴底锚定）。
///      直接把锚点改成全拉伸却复用原始 offset，offset 的语义会错位
///      （贴底锚定时 offsetMax.y 从底边往上量，全拉伸时从顶边往下量），矩形会跳到错误位置。
///      因此首次 Apply 先把「当前矩形」反推成「全拉伸语义下的四边内缩量」存为 base，之后一律从 base 重算。
///
/// 两种模式（重要）：
///   Shift（默认）：整体向安全区方向平移，尺寸不变 —— 适合固定高度的顶/底栏（不会压扁按钮）。
///       【2026-10-28 重写】平移改为只挪 anchoredPosition，**不再把锚点改成全拉伸** ——
///       旧实现按「当时父级尺寸」捕获全拉伸 base，CanvasScaler 生效后基准失配，
///       矩形会随父级缩放变形甚至变负（战斗顶条 200×-1041 事故同源）。
///   Inset：从被遮挡的那一边裁掉，尺寸变小 —— 适合满屏面板（背景、弹窗遮罩）。
///   Offset（2026-09-28 新增）：只改 anchoredPosition，**完全不碰 anchor / sizeDelta / pivot**。
///       顶栏用这个：顶栏本来就是「贴顶锚定 + 固定高」，Shift 会把它的锚点强行改成全拉伸，
///       一旦父级尺寸变化（编辑器改 Game 视口 / 折叠屏），内缩量是按旧父级高算的会串位；
///       Offset 只做「整体下移 safeArea 顶部高度」，锚点语义保持原样，任何缩放/重锚点都不怕。
///       换算也不再依赖父级矩形高，而是用 Screen.safeArea(像素) ÷ Canvas.scaleFactor
///       （子节点单位 == 画布参考单位，这是精确值；父级高在非满屏容器里会算错）。
///
/// 已知限制（不要拿它去改根 Canvas）：
///   根 Canvas（自己就是最外层画笔）的 RectTransform 由 Canvas 组件每帧驱动，
///   手动改 anchoredPosition 会被覆盖 → 在根 Canvas 上 Offset 也是无效动作。
///   所以刘海避让一律作用在「顶栏那个子节点」上（Town 顶栏 / 战斗顶栏），不要挂到画布根。
///
/// 微信小游戏胶囊（小程序操作框 ●●● | ◉）避让（2026-10-22 主人需求：资源条统一位置、
/// 不被刘海或胶囊遮挡）：
///   胶囊悬浮在状态栏下方右侧，Screen.safeArea 不包含它；本工程未引 WX C# SDK，
///   拿不到 wx.getMenuButtonBoundingClientRect，按微信官方几何常量预留：
///     胶囊 top = 状态栏高 + 4(css px)、高 32(css px) → 胶囊底 ≈ 安全区顶 + 36(css px)，
///     再留 8(css px) 呼吸 → 总预留 44(css px)。
///   css px → 设计单位换算按窗口 css 宽 ≈390 估（主流机型 360~430，取中值误差 ≤15%，
///   方向上宁可多留，不会压住胶囊）。只在 Offset 模式的 Top 边生效 —— 全项目所有
///   资源条 / 顶栏都走 EnsureTopOffset（Offset 模式），一处改动全界面统一。
///   Android APK（TapTap 渠道）/ 编辑器没有胶囊 → 预留 0，行为与之前完全一致。
/// </summary>
public class SafeAreaFitter : MonoBehaviour
{
    public enum Edge { Top, Bottom, Both }

    /// <summary>Shift = 平移保尺寸；Inset = 内缩裁边；Offset = 只挪 anchoredPosition（锚点/尺寸不动，顶栏用）。</summary>
    public enum FitMode { Shift, Inset, Offset }

    /// <summary>要避开的边：Top=只避上边，Bottom=只避下边，Both=上下都避。左右永远不缩。</summary>
    public Edge edge = Edge.Both;

    /// <summary>Shift = 平移（保尺寸，顶/底栏用）；Inset = 内缩（裁边，满屏面板用）；Offset = 只挪位置（推荐顶栏用）。</summary>
    public FitMode mode = FitMode.Shift;

    /// <summary>总开关。false 时恢复 base（还原到未适配的原始矩形），方便出问题一键关。</summary>
    public bool enabledFit = true;

    bool _baseCaptured;
    Vector2 _baseOffsetMin;
    Vector2 _baseOffsetMax;
    bool _applying;

    // —— Offset 模式专用 ——
    Vector2 _basePos;              // 未施加位移前的 anchoredPosition
    bool _basePosCaptured;
    float _appliedY;               // 本组件当前施加的 y 位移（用于「重锚点时反推基准」）
    float _sigMinY, _sigMaxY;      // 基准对应的锚点 y 签名（锚点变了就重新取基准）
    int _lastW = -1, _lastH = -1;  // Offset/Shift：上一次的屏幕尺寸 / 安全区 / 画布缩放 / 父级高（变化检测，不变就不干活）
    Rect _lastSafe = new Rect(0f, 0f, -1f, -1f);
    float _lastSf = -1f;
    float _lastPh = -1f;

    /// <summary>是否已经报过「矩形退化」错误（同一个组件只报一次，避免刷屏）。</summary>
    bool _degenerateReported;

    // ================================================================
    // 2026-10-28 战斗顶条消失根因修复（详见 EnsureConfigured 的注释）
    //
    // AddComponent<SafeAreaFitter>() 会【同步】触发 Awake → Apply()。
    // 那一刻 mode 还是枚举默认值 Shift、edge 还是默认值 Both，
    // Shift 分支会把节点锚点强制改成全拉伸 (0,0)~(1,1)（SetFullStretch），
    // 并且按【当时父级的尺寸】捕获 base。
    // 战斗里 CanvasScaler 生效前画布根 rect 还是屏幕像素（1240×2770），
    // 缩放后变成参考单位（720×1608.75），
    // 于是顶条尺寸 = 新父级尺寸 + 旧基准算出的 sizeDelta(720-1240, 120-2770)=(-520,-2650)
    //            = (200, -1041)  → 高度为负，整条顶条不可见。
    // （2026-10-27 实测到的「rect 200×-1042」就是这个数，一个字不差。）
    //
    // 修法：顶/底栏（Offset 语义）在 AddComponent 之前先把配置登记好，
    // 让 Awake 里的第一次 Apply 就用正确模式，绝不允许默认 Shift 抢跑。
    // ================================================================
    struct PendingConfig
    {
        public Edge edge;
        public FitMode mode;
        public bool enabledFit;
    }

    static readonly System.Collections.Generic.Dictionary<int, PendingConfig> _pending =
        new System.Collections.Generic.Dictionary<int, PendingConfig>();

    void Awake()
    {
        // 运行时 AddComponent 进来的组件：Awake 早于调用方赋值，
        // 必须用预登记的配置，否则会以默认 Shift 抢跑改坏锚点（见上方注释）。
        // 键 = 宿主 GameObject 的 instanceID（Component 自己的 ID 与 GameObject 不同，别用错）。
        int id = gameObject.GetInstanceID();
        if (_pending.TryGetValue(id, out var cfg))
        {
            edge = cfg.edge;
            mode = cfg.mode;
            enabledFit = cfg.enabledFit;
        }
        Apply();
    }
    void Start() => Apply();

    /// <summary>
    /// Offset / Shift 共用兜底：画布 scaleFactor / 屏幕尺寸 / 安全区 / 父级高度只要变了就重算一次。
    /// （AddComponent 的瞬间 CanvasScaler 可能还没量准，只靠 Awake/Start 会算错一次然后一直错。）
    /// 什么都没变时每帧只做几次比较，不做任何写入。
    /// </summary>
    void LateUpdate()
    {
        if (mode != FitMode.Offset && mode != FitMode.Shift) return;
        var rt = transform as RectTransform;
        if (rt == null) return;

        int w = Screen.width, h = Screen.height;
        Rect safe = Screen.safeArea;
        float sf = ScaleFactorOf(rt);
        var parent = rt.parent as RectTransform;
        float ph = parent != null ? parent.rect.height : 0f;
        if (w == _lastW && h == _lastH && safe == _lastSafe
            && Mathf.Approximately(sf, _lastSf) && Mathf.Approximately(ph, _lastPh))
            return;
        _lastW = w; _lastH = h; _lastSafe = safe; _lastSf = sf; _lastPh = ph;
        ApplyByPosition(rt, mode == FitMode.Offset ? ComputeOffsetDy(rt) : ComputeShiftDy(parent));
    }

    void OnRectTransformDimensionsChange()
    {
        // 改 anchor/offset 本身会再次触发本回调，加守卫防递归
        if (_applying) return;
        Apply();
    }

    void Apply()
    {
        var rt = transform as RectTransform;
        if (rt == null) return;

        // Offset / Shift 统一走「只挪 anchoredPosition，绝不碰锚点」（2026-10-28）：
        // 两者差别只在位移量的算法（Offset 按屏幕像素÷缩放，Shift 按父级高度比例）。
        // 旧 Shift 实现会把锚点强制改成全拉伸并按【当时父级尺寸】捕获 base —— CanvasScaler
        // 生效后基准失配，矩形尺寸会随父级缩放变形甚至变负（战斗顶条 200×-1041 同源事故）。
        if (mode == FitMode.Offset)
        {
            ApplyByPosition(rt, ComputeOffsetDy(rt));
            return;
        }

        var parent = rt.parent as RectTransform;
        // 父级（通常是 Canvas 内容根）还没量好尺寸时等下一次，避免用 0 高度算出错误内缩。
        // 挂在 Canvas 根节点上（parent 不是 RectTransform）时同样直接放弃 —— 惰性 no-op，不影响外观。
        if (parent == null || parent.rect.width <= 0f || parent.rect.height <= 0f) return;

        if (mode == FitMode.Shift)
        {
            ApplyByPosition(rt, ComputeShiftDy(parent));
            return;
        }

        // ── Inset（内缩裁边，全工程当前无调用方，保留原语义）──
        if (!_baseCaptured)
            CaptureBase(rt, parent);

        _applying = true;
        try
        {
            if (!enabledFit)
            {
                // 一键关：全拉伸 + base，还原到挂本组件前的外观
                SetFullStretch(rt, _baseOffsetMin, _baseOffsetMax);
                return;
            }

            float topN = 0f, bottomN = 0f;
            if (Screen.width > 0 && Screen.height > 0)
            {
                Rect safe = Screen.safeArea;
                topN = (Screen.height - (safe.y + safe.height)) / Screen.height;
                bottomN = safe.y / Screen.height;
            }
            float ph = parent.rect.height;
            float topU = topN * ph;
            float botU = bottomN * ph;

            Vector2 omin = _baseOffsetMin;
            Vector2 omax = _baseOffsetMax;

            bool fitTop = edge == Edge.Top || edge == Edge.Both;
            bool fitBottom = edge == Edge.Bottom || edge == Edge.Both;

            // 内缩：只动被遮挡的那一侧，矩形变小（满屏面板不会溢出安全区）
            if (fitTop) omax.y -= topU;
            if (fitBottom) omin.y += botU;

            SetFullStretch(rt, omin, omax);
        }
        finally
        {
            _applying = false;
        }
    }

    /// <summary>Shift 模式的位移量：按父级高度比例换算（旧算法原样保留，只是不再动锚点）。</summary>
    float ComputeShiftDy(RectTransform parent)
    {
        if (parent == null) return 0f;   // 挂在画布根上量不了父级高 → 惰性 no-op（QuestHudBar 语义）
        bool fitTop = edge == Edge.Top || edge == Edge.Both;
        bool fitBottom = edge == Edge.Bottom || edge == Edge.Both;
        float dy = 0f;
        if (enabledFit && (fitTop || fitBottom) && Screen.width > 0 && Screen.height > 0)
        {
            Rect safe = Screen.safeArea;
            float topN = (Screen.height - (safe.y + safe.height)) / Screen.height;
            float bottomN = safe.y / Screen.height;
            float ph = parent.rect.height;
            if (fitTop) dy -= topN * ph;      // 避上边 = 整体往下挪
            if (fitBottom) dy += bottomN * ph; // 避下边 = 整体往上挪
        }
        return dy;
    }

    /// <summary>Offset 模式的位移量：屏幕像素 ÷ Canvas.scaleFactor（精确换算，不看父级尺寸）。</summary>
    float ComputeOffsetDy(RectTransform rt)
    {
        bool fitTop = edge == Edge.Top || edge == Edge.Both;
        bool fitBottom = edge == Edge.Bottom || edge == Edge.Both;
        float dy = 0f;
        if (enabledFit && (fitTop || fitBottom) && Screen.width > 0 && Screen.height > 0)
        {
            if (fitTop) dy -= TotalTopInsetUnits(rt);  // 避上边 = 安全区顶部 + 微信小游戏胶囊预留，整体往下挪
            if (fitBottom) dy += BottomInsetUnits(rt); // 避下边 = 整体往上挪
        }
        return dy;
    }

    /// <summary>
    /// Offset / Shift 共用的写入段（2026-10-28）：只改 anchoredPosition，anchor / sizeDelta / pivot 一律不动。
    /// 位移量 dy 由调用方按模式算好（Offset = 屏幕像素÷scaleFactor；Shift = 父级高度比例）。
    /// 幂等：base 只在「首次」或「锚点被别的布局逻辑（如 UiLayoutStretch）改过」时记录，
    /// 且记录时先减掉本组件已施加的位移，所以重锚定不会导致二次叠加或位置跳变。
    /// </summary>
    void ApplyByPosition(RectTransform rt, float dy)
    {
        bool anchorsChanged = !Mathf.Approximately(rt.anchorMin.y, _sigMinY)
                              || !Mathf.Approximately(rt.anchorMax.y, _sigMaxY);
        if (!_basePosCaptured || anchorsChanged)
        {
            // 反推「没有本组件时」的位置：当前值减掉本组件已经施加的位移。
            _basePos = new Vector2(rt.anchoredPosition.x, rt.anchoredPosition.y - _appliedY);
            _sigMinY = rt.anchorMin.y;
            _sigMaxY = rt.anchorMax.y;
            _basePosCaptured = true;
        }

        _appliedY = dy;
        _applying = true;
        try
        {
            // x 一律不动（左右不缩）。值没变就不写，避免无谓地弄脏布局。
            var want = new Vector2(rt.anchoredPosition.x, _basePos.y + dy);
            if (rt.anchoredPosition != want)
                rt.anchoredPosition = want;
        }
        finally
        {
            _applying = false;
        }

        // 2026-10-28：矩形为负（宽/高 < 0）= 这条栏根本显示不出来。
        // 战斗顶条 2026-10-27 就是这么消失的（rect 200×-1041，高度为负）。
        // 这里直接报错，不静默 —— 免得下次又只能靠肉眼猜。
        // ⚠ 只在「父级已量好尺寸」后才判：画布还没布局时（如 Instantiate 当帧），横向拉伸
        //   子节点的宽 = 父宽0 + 负 sizeDelta（GuildHallUI 底栏跳板节点 sizeDelta.x=-670），
        //   瞬时为负是正常的，不是退化 —— 别把真报错的额度烧在这种瞬时态上。
        var layoutParent = rt.parent as RectTransform;
        bool parentLaidOut = layoutParent == null
                             || (layoutParent.rect.width > 0f && layoutParent.rect.height > 0f);
        // 【2026-10-28 主人拍板（方案1）】横拉 + 负 sizeDelta.x 的「跳板节点」不做退化告警：
        // 主界面底栏 BottomNav（sizeDelta.x=-670）宽度设计上就是 父宽-670，子节点再 +670 撑回满宽，
        // 跳板自己宽度为负是设计使然——挂载首帧父级还没按参考宽 720 铺开（实测 573）时跳板必为负
        //（573-670=-97），但可见栏体是子节点，显示不受影响。这类节点每次回主城都新建，
        // 不跳过就会每次登录误报一条 LogError。其余节点照旧报错，真退化不让它悄悄烂掉。
        bool isNegPadSpringboard =
            Mathf.Approximately(rt.anchorMin.x, 0f) && Mathf.Approximately(rt.anchorMax.x, 1f)
            && rt.sizeDelta.x < 0f;
        if (!_degenerateReported && !isNegPadSpringboard && parentLaidOut && (rt.rect.width < 0f || rt.rect.height < 0f))
        {
            _degenerateReported = true;
            Debug.LogError($"[SafeAreaFitter] {name} 的矩形退化为负（size={rt.rect.size}，" +
                           $"anchor={rt.anchorMin}~{rt.anchorMax}），这条栏不会显示。" +
                           "请检查锚点/尺寸是否被别的布局逻辑改过（本组件的 Offset 模式只挪位置，不改锚点）。");
        }
    }

    /// <summary>屏幕像素 → 画布参考单位的换算系数（取最外层带 CanvasScaler 的 Canvas）。</summary>
    static float ScaleFactorOf(RectTransform rt)
    {
        var canvases = rt.GetComponentsInParent<Canvas>(true);
        for (int i = canvases.Length - 1; i >= 0; i--)
        {
            var c = canvases[i];
            if (c == null) continue;
            if (c.GetComponent<CanvasScaler>() != null && c.scaleFactor > 0f)
                return c.scaleFactor;
        }
        return 1f;
    }

    /// <summary>
    /// 安全区顶部内缩量（画布逻辑单位 = 屏幕像素 ÷ Canvas.scaleFactor）。
    /// 供「自己写排版」的界面直接把这层 padding 加进布局（如登录页底部忠告），
    /// 换算与 Offset 模式同源，全项目只此一份实现。
    /// </summary>
    public static float TopInsetUnits(RectTransform rt)
    {
        if (rt == null || Screen.width <= 0 || Screen.height <= 0) return 0f;
        float px = Mathf.Max(0f, Screen.height - (Screen.safeArea.y + Screen.safeArea.height));
        return px <= 0f ? 0f : px / Mathf.Max(0.01f, ScaleFactorOf(rt));
    }

    /// <summary>安全区底部内缩量（画布逻辑单位）。与 TopInsetUnits 同源。</summary>
    public static float BottomInsetUnits(RectTransform rt)
    {
        if (rt == null || Screen.width <= 0 || Screen.height <= 0) return 0f;
        float px = Mathf.Max(0f, Screen.safeArea.y);
        return px <= 0f ? 0f : px / Mathf.Max(0.01f, ScaleFactorOf(rt));
    }

    // ────────────────────────────────────────────────
    // 微信小游戏胶囊（小程序操作框）预留 —— 见类注释。
    // ────────────────────────────────────────────────

    /// <summary>胶囊底 ≈ 安全区顶 + 36(css px)（状态栏下 4 + 胶囊高 32，微信官方几何）。</summary>
    const float CapsuleBottomCssPx = 36f;
    /// <summary>胶囊底再留的呼吸空隙（css px），吸收估算误差，方向宁可多留。</summary>
    const float CapsuleGapCssPx = 8f;
    /// <summary>估算的窗口 css 宽（主流机型 360~430，取中值；误差 ≤15% 且方向安全）。</summary>
    const float AssumedWindowCssWidth = 390f;

    static int _wxMiniGame = -1;   // -1 未判定，0 否，1 是（缓存，别每帧 ToString 分配）

    /// <summary>当前是否跑在微信小游戏端（团结引擎 UnityWeixinMiniGame 播放器）。</summary>
    public static bool IsWeixinMiniGame
    {
        get
        {
            if (_wxMiniGame < 0)
            {
#if UNITY_WEIXINMINIGAME
                _wxMiniGame = 1;
#else
                // 团结引擎新增 RuntimePlatform.WeixinMiniGamePlayer（标准 Unity 枚举里没有，
                // 直接引用成员会编不过，用平台名字符串判定，编辑器 / Android 包恒 false）
                _wxMiniGame = Application.platform.ToString().IndexOf(
                    "WeixinMiniGame", System.StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 0;
#endif
            }
            return _wxMiniGame == 1;
        }
    }

    /// <summary>
    /// 胶囊避让预留（设计单位）：胶囊底比安全区顶低约 44(css px)，按窗口宽换算成设计单位。
    /// 非微信小游戏端恒 0（编辑器 / Android APK 行为不变）。
    /// </summary>
    public static float CapsuleReserveUnits(RectTransform rt)
    {
        if (!IsWeixinMiniGame || rt == null || Screen.width <= 0) return 0f;
        float sf = ScaleFactorOf(rt);
        if (sf <= 0.01f) return 0f;
        float unitsPerCss = Screen.width / AssumedWindowCssWidth / sf;
        return (CapsuleBottomCssPx + CapsuleGapCssPx) * unitsPerCss;
    }

    /// <summary>
    /// 顶栏统一额外下移量（设计单位，2026-10-23 主人「顶部的资源条往下点避免顶部的刘海」；
    /// 同日晚主人「有点太往下了 回一半的距离」→ 40 减半为 20）：
    /// 安全区避让之外再给整条顶栏固定呼吸量；全项目顶栏统一走 TotalTopInsetUnits，一处改全界面一致。
    /// </summary>
    public const float TopBarExtraDropUnits = 20f;

    /// <summary>
    /// 顶栏总避让量 = 安全区顶部内缩 + 微信胶囊预留 + 固定额外下移（设计单位）。
    /// 全项目顶栏统一走这里取值（Offset 模式内部也用它），保证所有界面资源条同一个位置。
    /// </summary>
    public static float TotalTopInsetUnits(RectTransform rt)
    {
        return TopInsetUnits(rt) + CapsuleReserveUnits(rt) + TopBarExtraDropUnits;
    }

    /// <summary>纯工具：把节点矩形四边换算成父级本地坐标（原点=父级左下角）。</summary>
    static void RectMinMaxInParent(RectTransform rt, RectTransform parent, out Vector2 min, out Vector2 max)
    {
        Vector2 pMin = parent.rect.min;
        Vector2 pMax = parent.rect.max;
        min = new Vector2(
            Mathf.Lerp(pMin.x, pMax.x, rt.anchorMin.x),
            Mathf.Lerp(pMin.y, pMax.y, rt.anchorMin.y)) + rt.offsetMin;
        max = new Vector2(
            Mathf.Lerp(pMin.x, pMax.x, rt.anchorMax.x),
            Mathf.Lerp(pMin.y, pMax.y, rt.anchorMax.y)) + rt.offsetMax;
    }

    /// <summary>给一个「顶部横条」节点挂上 Offset 适配（幂等：已挂就复用，只校正参数）。</summary>
    public static SafeAreaFitter EnsureTopOffset(RectTransform rt)
    {
        return EnsureConfigured(rt, Edge.Top, FitMode.Offset, true);
    }

    /// <summary>给一个「底部横条」节点挂上 Offset 适配（幂等：已挂就复用，只校正参数）。</summary>
    public static SafeAreaFitter EnsureBottomOffset(RectTransform rt)
    {
        return EnsureConfigured(rt, Edge.Bottom, FitMode.Offset, true);
    }

    /// <summary>
    /// 带配置的挂载入口（2026-10-28 公开）：外部代码**一律走这里，禁止直接 AddComponent&lt;SafeAreaFitter&gt;()** ——
    /// AddComponent 会同步触发 Awake，那一刻 mode/edge 还是枚举默认值（Shift + Both），会抢跑。
    /// 已挂就复用并校正参数，幂等。
    /// </summary>
    public static SafeAreaFitter AddConfigured(RectTransform rt, Edge edge, FitMode mode, bool enabledFit = true)
    {
        return EnsureConfigured(rt, edge, mode, enabledFit);
    }

    /// <summary>
    /// 统一挂载入口（2026-10-28）：AddComponent 之前先登记配置，让组件 Awake 里的第一次 Apply
    /// 就用正确的 edge / mode，绝不允许枚举默认值（Shift + Both）抢跑。
    ///
    /// 为什么必须这样：AddComponent 同步触发 Awake → Apply()，默认 mode=Shift 会走
    /// SetFullStretch 把节点锚点改成全拉伸，并按【当时的父级尺寸】捕获 base。
    /// 战斗画布在 CanvasScaler 生效前父级 rect 还是屏幕像素（1240×2770），缩放后变参考单位
    /// （720×1608.75），旧基准算出的 sizeDelta(-520,-2650) 会让顶条实际尺寸变成 (200,-1041)
    /// ——高度为负，整条顶条直接不可见（2026-10-27「战斗顶条消失」的根因）。
    /// 顶栏/底栏一律走 Offset（只挪 anchoredPosition，绝不碰锚点），此路不通。
    ///
    /// 另外挂完会核对锚点有没有被改过 —— 万一以后谁又改了挂载顺序，这里立刻报错，不让它悄悄烂掉。
    /// </summary>
    static SafeAreaFitter EnsureConfigured(RectTransform rt, Edge edge, FitMode mode, bool enabledFit)
    {
        if (rt == null) return null;

        var f = rt.GetComponent<SafeAreaFitter>();
        if (f == null)
        {
            var aminBefore = rt.anchorMin;
            var amaxBefore = rt.anchorMax;

            int id = rt.gameObject.GetInstanceID();
            _pending[id] = new PendingConfig { edge = edge, mode = mode, enabledFit = enabledFit };
            try
            {
                f = rt.gameObject.AddComponent<SafeAreaFitter>();
            }
            finally
            {
                _pending.Remove(id);
            }

            // 兜底再校准一次（Awake 里已按登记配置跑过，这里保证最终参数就是调用方要的）
            f.edge = edge;
            f.mode = mode;
            f.enabledFit = enabledFit;
            f.Apply();

            if (rt.anchorMin != aminBefore || rt.anchorMax != amaxBefore)
            {
                Debug.LogError($"[SafeAreaFitter] {rt.name} 的锚点在挂载时被改动 " +
                               $"({aminBefore}~{amaxBefore} → {rt.anchorMin}~{rt.anchorMax})。" +
                               "顶/底栏必须是 Offset 模式（只挪位置、不改锚点）；" +
                               "请检查是否有别处在挂载前把它按 Shift 处理了。");
            }
        }
        else
        {
            f.edge = edge;
            f.mode = mode;
            f.enabledFit = enabledFit;
            f.Apply();
        }
        return f;
    }

    /// <summary>
    /// 在 root 子树里按名字找「顶部横条」并挂 Offset 适配。规则：
    ///   - 候选里只处理**最外层**那个（父级链上已有别的命中节点就跳过），避免父子同时下推变成双倍；
    ///   - 只处理矩形中心位于父级上半部的节点（同名但长在屏幕中下部的节点不碰）；
    ///   - 返回实际挂上的个数。
    /// 注意：不要挂到根 Canvas 上（其 RectTransform 由 Canvas 每帧驱动，改了无效，见类注释）。
    /// </summary>
    public static int ApplyTopOffset(Transform root, params string[] nodeNames)
    {
        if (root == null || nodeNames == null || nodeNames.Length == 0) return 0;

        var hits = new System.Collections.Generic.List<Transform>();
        for (int i = 0; i < nodeNames.Length; i++)
        {
            if (string.IsNullOrEmpty(nodeNames[i])) continue;
            CollectByName(root, nodeNames[i], hits);
        }
        if (hits.Count == 0) return 0;

        int applied = 0;
        for (int i = 0; i < hits.Count; i++)
        {
            var t = hits[i];
            if (t == null) continue;

            bool nested = false;
            for (int j = 0; j < hits.Count; j++)
            {
                if (i == j || hits[j] == null || hits[j] == t) continue;
                if (t.IsChildOf(hits[j])) { nested = true; break; }
            }
            if (nested) continue;

            var rt = t as RectTransform;
            if (rt == null) continue;
            if (!IsTopArea(rt)) continue;

            if (EnsureTopOffset(rt) != null) applied++;
        }
        return applied;
    }

    static void CollectByName(Transform root, string name, System.Collections.Generic.List<Transform> into)
    {
        if (root == null) return;
        if (root.name == name && !into.Contains(root)) into.Add(root);
        for (int i = 0; i < root.childCount; i++)
            CollectByName(root.GetChild(i), name, into);
    }

    /// <summary>矩形中心是否落在父级上半部（本地坐标原点在父级左下角，中心 y &gt; 0 即上半）。</summary>
    static bool IsTopArea(RectTransform rt)
    {
        var parent = rt.parent as RectTransform;
        if (parent == null || parent.rect.height <= 0f) return true;   // 判不了就放行
        Vector2 min, max;
        RectMinMaxInParent(rt, parent, out min, out max);
        return (min.y + max.y) * 0.5f > 0f;
    }

    static void SetFullStretch(RectTransform rt, Vector2 omin, Vector2 omax)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = omin;
        rt.offsetMax = omax;
    }

    /// <summary>
    /// 把「当前矩形」换算成全拉伸语义下的四边内缩量，作为 base。
    /// Unity 语义：offsetMin = rect.min - Lerp(parent.min, parent.max, anchorMin)
    ///            offsetMax = rect.max - Lerp(parent.min, parent.max, anchorMax)
    /// 全拉伸时 anchorMin=(0,0)→parent.min、anchorMax=(1,1)→parent.max，故：
    ///   baseOffsetMin = curMin - parent.min
    ///   baseOffsetMax = curMax - parent.max   （注意是 curMax - pMax，方向不能反）
    /// </summary>
    void CaptureBase(RectTransform rt, RectTransform parent)
    {
        Vector2 pMin = parent.rect.min;
        Vector2 pMax = parent.rect.max;

        // 注意：anchor 是逐轴的（x 用父级 x 区间插值，y 用父级 y 区间插值），
        // 不能写成 Vector2.Lerp(pMin, pMax, anchor)——第三个参数只接受 float。
        Vector2 curMin = new Vector2(
            Mathf.Lerp(pMin.x, pMax.x, rt.anchorMin.x),
            Mathf.Lerp(pMin.y, pMax.y, rt.anchorMin.y)) + rt.offsetMin;
        Vector2 curMax = new Vector2(
            Mathf.Lerp(pMin.x, pMax.x, rt.anchorMax.x),
            Mathf.Lerp(pMin.y, pMax.y, rt.anchorMax.y)) + rt.offsetMax;

        _baseOffsetMin = curMin - pMin;
        _baseOffsetMax = curMax - pMax;
        _baseCaptured = true;
    }
}
