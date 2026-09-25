using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 节点布局角色：与命名规范对应，见 UiLayoutConvention.md。
/// </summary>
public enum UiLayoutRole
{
    Unknown,
    /// <summary>手调美术底图（Background / BgArt），禁止改 rect。</summary>
    Fixed,
    /// <summary>全屏铺满（BgStretch / Bg / Dim）。</summary>
    FillScreen,
    /// <summary>仅横向拉满（map / MapRoot）。</summary>
    StretchHorizontal,
}

/// <summary>
/// 集中 UI 自适应拉伸；禁止对泛化 Background 无差别 Stretch。
/// </summary>
public static class UiLayoutStretch
{
    /// <summary>竖屏「以地图为界」重锚定的总开关。false 时还原到预制体原始锚点，方便一键回退。</summary>
    public static bool EnableVerticalSplit = true;

    /// <summary>竖屏「无背景则垫黑底」的总开关。false 时不创建黑底（已创建则隐藏），方便一键关。</summary>
    public static bool EnableFitBackdrop = true;

    // 防递归守卫（照抄 SafeAreaFitter._applying），避免改锚点触发的重入。
    static bool _verticalSplitApplying;

    // 记录被重锚定节点的原始锚点/偏移，用于幂等重算与一键回退还原（直接子节点粒度，不递归深层）。
    struct VerticalSplitBase
    {
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 offsetMin;
        public Vector2 offsetMax;
        public bool applied;
    }
    static readonly System.Collections.Generic.Dictionary<int, VerticalSplitBase> _verticalSplitBase =
        new System.Collections.Generic.Dictionary<int, VerticalSplitBase>();

    public static UiLayoutRole InferRole(string nodeName)
    {
        if (string.IsNullOrEmpty(nodeName)) return UiLayoutRole.Unknown;
        if (IsFillScreenName(nodeName)) return UiLayoutRole.FillScreen;
        if (IsFixedArtName(nodeName)) return UiLayoutRole.Fixed;
        if (IsStretchHorizontalName(nodeName)) return UiLayoutRole.StretchHorizontal;
        return UiLayoutRole.Unknown;
    }

    public static bool IsFillScreenName(string nodeName)
    {
        return nodeName.Equals("BgStretch", System.StringComparison.OrdinalIgnoreCase)
               || nodeName.Equals("BgFill", System.StringComparison.OrdinalIgnoreCase)
               || nodeName.Equals("Bg", System.StringComparison.OrdinalIgnoreCase)
               || nodeName.Equals("Dim", System.StringComparison.OrdinalIgnoreCase)
               || nodeName.Equals("ModalDim", System.StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsFixedArtName(string nodeName)
    {
        return nodeName.Equals("Background", System.StringComparison.OrdinalIgnoreCase)
               || nodeName.Equals("BgArt", System.StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsStretchHorizontalName(string nodeName)
    {
        return nodeName.Equals("map", System.StringComparison.OrdinalIgnoreCase)
               || nodeName.Equals("Map", System.StringComparison.OrdinalIgnoreCase)
               || nodeName.Equals("MapRoot", System.StringComparison.OrdinalIgnoreCase)
               || nodeName.Equals("Maproot", System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>四边贴父级，用于 Dim / ModalDim 等纯色遮罩。</summary>
    public static void ApplyFillScreen(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        if (rt.localScale.sqrMagnitude < 0.0001f)
            rt.localScale = Vector3.one;
    }

    /// <summary>
    /// 全屏底图（BgStretch / Bg）：中心锚点 + EnvelopeParent（与剧情 BG 一致），避免 stretch+Envelope 在长屏偏左。
    /// Toggle/Slider 等控件内的 Background 子节点一律跳过。
    /// </summary>
    public static void ApplyBgStretch(RectTransform rt, Image image = null)
    {
        if (rt == null || IsWidgetPart(rt)) return;
        if (!IsBgStretchArtName(rt.gameObject.name)) return;
        if (image == null) image = rt.GetComponent<Image>();
        if (image == null) return;
        float aspect = GameConfig.DESIGN_WIDTH / GameConfig.DESIGN_HEIGHT;
        var sp = image.sprite;
        if (sp != null)
            aspect = sp.rect.width / Mathf.Max(1f, sp.rect.height);
        ApplyEnvelopeCenter(rt, aspect);
        image.preserveAspect = false;
    }

    /// <summary>任意美术全屏层（如 Backdrop）：不校验节点名，中心 + Envelope。</summary>
    public static void ApplyEnvelopeImage(RectTransform rt, Image image = null)
    {
        if (rt == null || IsWidgetPart(rt)) return;
        if (image == null) image = rt.GetComponent<Image>();
        if (image == null) return;
        float aspect = GameConfig.DESIGN_WIDTH / GameConfig.DESIGN_HEIGHT;
        var sp = image.sprite;
        if (sp != null)
            aspect = sp.rect.width / Mathf.Max(1f, sp.rect.height);
        ApplyEnvelopeCenter(rt, aspect);
        image.preserveAspect = false;
    }

    /// <summary>全屏视频 RawImage：中心 + Envelope，避免长屏偏左。</summary>
    public static void ApplyEnvelopeRawImage(RectTransform rt, float aspectRatio)
    {
        if (rt == null) return;
        if (aspectRatio < 0.01f)
            aspectRatio = GameConfig.DESIGN_WIDTH / GameConfig.DESIGN_HEIGHT;
        ApplyEnvelopeCenter(rt, aspectRatio);
    }

    /// <summary>中心锚点铺满父级再 EnvelopeParent。</summary>
    public static void ApplyEnvelopeCenter(RectTransform rt, float aspectRatio)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        if (rt.localScale.sqrMagnitude < 0.0001f)
            rt.localScale = Vector3.one;

        var parent = rt.parent as RectTransform;
        float pw = parent != null ? parent.rect.width : GameConfig.DESIGN_WIDTH;
        float ph = parent != null ? parent.rect.height : GameConfig.DESIGN_HEIGHT;
        if (pw < 1f) pw = GameConfig.DESIGN_WIDTH;
        if (ph < 1f) ph = GameConfig.DESIGN_HEIGHT;
        rt.sizeDelta = new Vector2(pw, ph);

        var fitter = rt.GetComponent<AspectRatioFitter>();
        if (fitter == null) fitter = rt.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = Mathf.Max(0.01f, aspectRatio);
    }

    static bool IsBgStretchArtName(string nodeName)
    {
        return nodeName.Equals("BgStretch", System.StringComparison.OrdinalIgnoreCase)
               || nodeName.Equals("BgFill", System.StringComparison.OrdinalIgnoreCase)
               || nodeName.Equals("Bg", System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Unity 标准控件零件（Toggle Background、Slider Handle 等）：永远以预制体 rect 为准。
    /// </summary>
    public static bool IsWidgetPart(Transform t)
    {
        if (t == null) return false;
        var p = t;
        while (p != null)
        {
            if (p.GetComponent<Toggle>() != null
                || p.GetComponent<Slider>() != null
                || p.GetComponent<Scrollbar>() != null
                || p.GetComponent<Dropdown>() != null)
                return true;
            p = p.parent;
        }
        return false;
    }

    /// <summary>是否允许代码改 rect（仅 BgStretch/Bg/Dim/map 等明确节点）。</summary>
    public static bool MayStretchByCode(RectTransform rt)
    {
        if (rt == null || IsWidgetPart(rt)) return false;
        var role = InferRole(rt.gameObject.name);
        return role == UiLayoutRole.FillScreen || role == UiLayoutRole.StretchHorizontal;
    }

    /// <summary>战斗 map 等：左右拉满，高度/纵向 offset 保持预制体设定。</summary>
    public static void ApplyStretchHorizontal(RectTransform rt)
    {
        if (rt == null) return;

        float yMin = rt.anchorMin.y;
        float yMax = rt.anchorMax.y;
        float oMinY = rt.offsetMin.y;
        float oMaxY = rt.offsetMax.y;
        Vector2 pivot = rt.pivot;
        float posY = rt.anchoredPosition.y;
        float sizeY = rt.sizeDelta.y;

        if (Mathf.Abs(rt.anchorMin.x - rt.anchorMax.x) < 0.01f)
        {
            rt.anchorMin = new Vector2(0f, yMin);
            rt.anchorMax = new Vector2(1f, yMax);
            rt.pivot = new Vector2(0.5f, pivot.y);
            rt.offsetMin = new Vector2(0f, oMinY);
            rt.offsetMax = new Vector2(0f, oMaxY);
            if (Mathf.Abs(yMin - yMax) < 0.01f)
            {
                rt.anchoredPosition = new Vector2(0f, posY);
                rt.sizeDelta = new Vector2(0f, sizeY);
            }
        }
        else
        {
            rt.offsetMin = new Vector2(0f, rt.offsetMin.y);
            rt.offsetMax = new Vector2(0f, rt.offsetMax.y);
        }
    }

    /// <summary>按节点名推断角色并应用（Fixed / Unknown 不改动）。</summary>
    public static void ApplyForNode(RectTransform rt)
    {
        if (rt == null) return;
        switch (InferRole(rt.gameObject.name))
        {
            case UiLayoutRole.FillScreen:
                if (IsBgStretchArtName(rt.gameObject.name))
                    ApplyBgStretch(rt);
                else
                    ApplyFillScreen(rt);
                break;
            case UiLayoutRole.StretchHorizontal:
                ApplyStretchHorizontal(rt);
                break;
        }
    }

    /// <summary>在 battleUIRoot 下查找 map，横向拉满并尽量纵向撑满顶栏与背包之间。</summary>
    public static void ApplyBattleMapWidth(Transform battleUIRoot)
    {
        if (battleUIRoot == null) return;
        RectTransform mapRt = null;
        for (int i = 0; i < battleUIRoot.childCount; i++)
        {
            var c = battleUIRoot.GetChild(i);
            if (!IsStretchHorizontalName(c.name)) continue;
            mapRt = c as RectTransform;
            break;
        }
        if (mapRt == null) return;

        var top = FindNamed(battleUIRoot, "TopBar", "ResourceBar", "TopResourceBar", "HUDTop");
        var backpack = FindNamed(battleUIRoot, "BackpackPanel", "Backpack", "EquipPanel");

        if (top == null && backpack == null)
        {
            ApplyStretchHorizontal(mapRt);
            return;
        }

        // 顶栏底 ~ 背包顶：纵向 stretch，左右拉满（仅 map 节点）
        float topInset = 0f;
        float botInset = 0f;
        var parent = mapRt.parent as RectTransform;
        if (parent != null)
        {
            float parentH = Mathf.Max(1f, parent.rect.height);
            if (top != null)
            {
                float topBot = GetWorldRectBottomInParent(top, parent);
                topInset = Mathf.Clamp(parentH - topBot, 0f, parentH * 0.45f);
            }
            if (backpack != null)
            {
                float packTop = GetWorldRectTopInParent(backpack, parent);
                botInset = Mathf.Clamp(packTop, 0f, parentH * 0.55f);
            }
            if (topInset + botInset > parentH * 0.85f)
            {
                // 锚点解析失败时退回仅横向
                ApplyStretchHorizontal(mapRt);
                return;
            }
        }

        mapRt.anchorMin = new Vector2(0f, 0f);
        mapRt.anchorMax = new Vector2(1f, 1f);
        mapRt.pivot = new Vector2(0.5f, 0.5f);
        mapRt.offsetMin = new Vector2(0f, botInset);
        mapRt.offsetMax = new Vector2(0f, -topInset);
        if (mapRt.localScale.sqrMagnitude < 0.0001f)
            mapRt.localScale = Vector3.one;
    }

    /// <summary>
    /// 竖屏「以地图 map 为分界点」重锚定（战斗专用）：逻辑见 ApplyVerticalSplitCore。
    /// 保留原签名与行为，内部改为走核心方法、传入 map 中心作为分界。
    /// </summary>
    public static void ApplyVerticalSplitByMap(Transform battleUIRoot, RectTransform mapRt = null)
    {
        if (battleUIRoot == null) return;
        var parent = battleUIRoot as RectTransform;
        if (parent == null) return;
        // 父级（Canvas 内容根）还没量好尺寸时等下一次，避免用 0 高度算出错误分界。
        if (parent.rect.width <= 0f || parent.rect.height <= 0f) return;

        // 找不到 map 就不用以它为界，直接放弃（不应改任何东西）。
        if (mapRt == null)
        {
            for (int i = 0; i < battleUIRoot.childCount; i++)
            {
                var c = battleUIRoot.GetChild(i);
                if (IsStretchHorizontalName(c.name)) { mapRt = c as RectTransform; break; }
            }
        }
        if (mapRt == null) return;

        // map 当前中心 y（已由其 ApplyBattleMapWidth 撑满顶栏~背包之间），作为上下分界。
        // 注：元素中心 y = 父级高/2 + anchoredPosition.y，map 中心同理含「父级高/2」，
        // 二者比较时父级高/2 抵消，故该分界在任意屏高下等价于设计基准里的 pos.y 比较。
        float splitY = GetRectCenterInParent(mapRt, parent).y;
        ApplyVerticalSplitCore(battleUIRoot, parent, splitY, mapRt);
    }

    /// <summary>
    /// 竖屏「以界面中心为界」重锚定（非战斗界面通用入口，接入 UICanvasSetup.Apply）。
    /// splitY 为 null 时用【父级矩形中心 y】作分界（非战斗界面没有 map）；
    /// 也可传入具体分界（父级本地坐标，原点在父级左下角）精确控制。
    /// 复用 ApplyVerticalSplitByMap 同一套判定与幂等逻辑（见 ApplyVerticalSplitCore），不复制两份代码。
    /// </summary>
    public static void ApplyVerticalSplit(Transform root, float? splitY = null)
    {
        if (root == null) return;
        if (IsIndividuallyFitted(root)) return;
        var parent = root as RectTransform;
        if (parent == null) return;
        if (parent.rect.width <= 0f || parent.rect.height <= 0f) return;
        float s = splitY.HasValue
            ? splitY.Value
            : (parent.rect.min.y + parent.rect.max.y) * 0.5f;   // 父级中心 y

        int applied = ApplyVerticalSplitCore(root, parent, s, null);

        // 下钻一层：直接子节点整层被跳过（例如所有面板都包在一层全 stretch 的 Root 里），
        // 本层没有任何节点被重锚定时，找第一个全 stretch / 铺满父级 的直接子节点，
        // 对其子节点再跑一次核心逻辑。最多下钻 1 层，不无限递归，且仍遵守居中保护与所有跳过规则。
        if (applied <= 0)
        {
            RectTransform drill = FindDrillContainer(root);
            if (drill != null)
                ApplyVerticalSplitCore(drill, drill, s, null);
        }
    }

    /// <summary>
    /// 上下重锚定核心：以 splitY 为分界，把 root 的【直接子节点】里「居中锚点」的元素
    /// 高于分界贴顶、低于分界贴底，保持距边距离不变。
    /// skipRt 为需要强制跳过的节点（战斗中传 map 自身）；通用入口传 null。
    /// 判定/跳过规则、CaptureBase 幂等、防递归守卫全部集中在此，两个公开方法共用，避免两份代码。
    /// </summary>
    static int ApplyVerticalSplitCore(Transform root, RectTransform parent, float splitY, RectTransform skipRt)
    {
        if (_verticalSplitApplying) return 0;
        _verticalSplitApplying = true;
        int appliedCount = 0;
        try
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (skipRt != null && child == skipRt) continue;   // 战斗 map 自身由 ApplyBattleMapWidth 处理
                var rt = child as RectTransform;
                if (rt == null) continue;

                int id = rt.GetInstanceID();
                bool haveBase = _verticalSplitBase.TryGetValue(id, out var b);

                // 一键关：把曾经改过的节点还原到预制体原始锚点，并标记未应用（便于再次开启时重算）。
                if (!EnableVerticalSplit)
                {
                    if (haveBase && b.applied)
                    {
                        rt.anchorMin = b.anchorMin;
                        rt.anchorMax = b.anchorMax;
                        rt.offsetMin = b.offsetMin;
                        rt.offsetMax = b.offsetMax;
                        b.applied = false;
                        _verticalSplitBase[id] = b;
                    }
                    continue;
                }

                // 背景/满屏出血层不重锚定：它们由 FitBattleBackground/ApplyEnvelopeImage 负责平铺铺满。
                var role = InferRole(rt.gameObject.name);
                if (role == UiLayoutRole.FillScreen || role == UiLayoutRole.Fixed) continue;
                if (rt.GetComponent<AspectRatioFitter>() != null) continue;

                // 当前矩形（父级本地坐标），反推四边 —— 即「目标锚点语义下的内缩量」base。
                Vector2 curMin = new Vector2(
                    Mathf.Lerp(parent.rect.min.x, parent.rect.max.x, rt.anchorMin.x),
                    Mathf.Lerp(parent.rect.min.y, parent.rect.max.y, rt.anchorMin.y)) + rt.offsetMin;
                Vector2 curMax = new Vector2(
                    Mathf.Lerp(parent.rect.min.x, parent.rect.max.x, rt.anchorMax.x),
                    Mathf.Lerp(parent.rect.min.y, parent.rect.max.y, rt.anchorMax.y)) + rt.offsetMax;

                float pw = parent.rect.width;
                float ph = parent.rect.height;
                // 满屏出血层（宽度溢出父级、或宽高都铺满父级）跳过，避免破坏背景铺满。
                bool fullBleed = (curMax.x - curMin.x) > pw * 1.001f
                                 || ((curMax.x - curMin.x) >= pw * 0.999f && (curMax.y - curMin.y) >= ph * 0.999f);
                if (fullBleed) continue;

                float aMinY = rt.anchorMin.y;
                float aMaxY = rt.anchorMax.y;
                bool stretchY = Mathf.Abs(aMinY - aMaxY) > 0.001f;
                if (stretchY) continue;                        // 全 stretch（map 等）跳过
                if (Mathf.Abs(aMinY - 1f) < 0.001f && Mathf.Abs(aMaxY - 1f) < 0.001f) continue; // 已贴顶
                if (Mathf.Abs(aMinY - 0f) < 0.001f && Mathf.Abs(aMaxY - 0f) < 0.001f) continue; // 已贴底

                // 居中元素保护：弹窗主面板/居中面板的中心 y 几乎等于父级中心 y，
                // 它们不属于要贴边的顶栏/底栏，不该被上下拆分逻辑搬动（否则整块沉底或沉顶）。
                // 阈值取「父级高度 5%」与「24 逻辑像素」较大者：更瘦竖屏(如 1480)父级更高，
                // 纯比例阈值会放大、反而更易漏判，故用固定 24px 兜底。
                // 战斗走 map 分界时同样保留此保护（Boss 血条、居中提示类元素也不该被搬）。
                float elemCenterY = (curMin.y + curMax.y) * 0.5f;
                float parentCenterY = (parent.rect.min.y + parent.rect.max.y) * 0.5f;
                float centerThresh = Mathf.Max(ph * 0.05f, 24f);
                if (Mathf.Abs(elemCenterY - parentCenterY) < centerThresh) continue;
                // 到这里是「居中锚点且明显偏向某一侧」的元素，需要重锚定。

                // 首次遇到：CaptureBase 记录原始锚点/偏移（用于幂等重算与一键回退）。
                if (!haveBase)
                {
                    b = new VerticalSplitBase
                    {
                        anchorMin = rt.anchorMin,
                        anchorMax = rt.anchorMax,
                        offsetMin = rt.offsetMin,
                        offsetMax = rt.offsetMax,
                        applied = false,
                    };
                    _verticalSplitBase[id] = b;
                    haveBase = true;
                }

                bool toTop = elemCenterY > splitY;
                float A = toTop ? 1f : 0f;

                // 从当前矩形「重算」到目标锚点(A)语义下的 offset（x 轴完全不动）。
                float anchorY = Mathf.Lerp(parent.rect.min.y, parent.rect.max.y, A);
                Vector2 newOffsetMin = new Vector2(rt.offsetMin.x, curMin.y - anchorY);
                Vector2 newOffsetMax = new Vector2(rt.offsetMax.x, curMax.y - anchorY);

                // 只动 y 锚点（贴顶/贴底），x 锚点保持原样。
                rt.anchorMin = new Vector2(rt.anchorMin.x, A);
                rt.anchorMax = new Vector2(rt.anchorMax.x, A);
                rt.offsetMin = newOffsetMin;
                rt.offsetMax = newOffsetMax;

                b.applied = true;
                _verticalSplitBase[id] = b;
                appliedCount++;
            }
        }
        finally
        {
            _verticalSplitApplying = false;
        }
        return appliedCount;
    }

    /// <summary>
    /// 竖屏「无背景则垫黑底」：在更瘦竖屏（多出高度）上，若 root 直接子节点里没有背景层，
    /// 运行时补一个纯黑 Image 垫底，避免中间区域露出透明。
    ///   - 已有背景类节点（Background/Bg/BgStretch/Backdrop/底…或带 Image 且铺满父级）→ 什么都不做；
    ///   - 仅在 root.rect.height > DESIGN_HEIGHT + 1 时才创建/显示；9:16 基准屏完全不出现、不改外观；
    ///   - 幂等：已存在同名 FitBackdrop 节点就复用，不重复创建；
    ///   - EnableFitBackdrop 关掉则不创建（已创建则隐藏）。
    /// 铁律：运行时补节点，绝不碰 .prefab。
    /// </summary>
    public static void EnsureFitBackdrop(RectTransform root)
    {
        if (root == null) return;
        if (IsIndividuallyFitted(root)) return;
        if (root.rect.width <= 0f || root.rect.height <= 0f) return;   // 尺寸未量好时下一帧再判

        const string fitName = "FitBackdrop";
        RectTransform fitRt = FindDirectChild(root, fitName);

        // 一键关：不创建，已创建则隐藏。
        if (!EnableFitBackdrop)
        {
            if (fitRt != null) fitRt.gameObject.SetActive(false);
            return;
        }

        // 已有真正的背景层就不需要黑底（由背景平铺铺满）。
        if (HasBackgroundChild(root))
        {
            if (fitRt != null) fitRt.gameObject.SetActive(false);
            return;
        }

        // 9:16 基准或更宽：不需要黑底，也不应残留黑底节点。
        if (root.rect.height <= GameConfig.DESIGN_HEIGHT + 1f)
        {
            if (fitRt != null) fitRt.gameObject.SetActive(false);
            return;
        }

        if (fitRt == null)
        {
            // 运行时创建纯黑底图，垫在最底层（在所有 UI 之下）。
            var go = new GameObject(fitName);
            go.transform.SetParent(root, false);
            var img = go.AddComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = false;
            fitRt = go.GetComponent<RectTransform>();
            fitRt.anchorMin = Vector2.zero;
            fitRt.anchorMax = Vector2.one;
            fitRt.offsetMin = Vector2.zero;
            fitRt.offsetMax = Vector2.zero;
            fitRt.anchoredPosition = Vector2.zero;
            fitRt.sizeDelta = Vector2.zero;
            fitRt.pivot = new Vector2(0.5f, 0.5f);
            fitRt.localScale = Vector3.one;
            go.transform.SetAsFirstSibling();   // 保证在最底层
        }
        else
        {
            fitRt.gameObject.SetActive(true);
            fitRt.transform.SetAsFirstSibling();
        }
    }

    // ────────────────────────────────────────────────────
    // 已单独适配的界面名单与判定（通用规则必须整块跳过，避免二次改动破坏已调好的效果）
    // ────────────────────────────────────────────────────

    /// <summary>已迁移到界面自己文件里做适配的界面名；通用规则必须整块跳过，避免二次改动破坏已调好的效果。</summary>
    static readonly System.Collections.Generic.HashSet<string> IndividuallyFitted =
        // RewardPopup：排版全部在 RewardPopupUI.cs 里单独做（以主人预制体几何为准），通用规则必须跳过
        new System.Collections.Generic.HashSet<string> { "AdventureUI", "DailyLoginUI", "RewardPopup" };

    /// <summary>
    /// 判断 root 是否属于「已单独适配」的界面：按 root 自身名字、或 root 上挂载的组件类型名，
    /// 并沿整条父级链向上查找（防止传进来的是界面内的某个子节点）。
    /// 命中即返回 true，通用重锚定 / 垫黑底必须整块跳过。
    /// </summary>
    static bool IsIndividuallyFitted(Transform root)
    {
        Transform t = root;
        while (t != null)
        {
            if (IndividuallyFitted.Contains(t.name))
                return true;
            // 按组件类型名判断（例如挂在 root 上的 AdventureUI / DailyLoginUI 组件）
            var comps = t.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++)
            {
                if (comps[i] == null) continue;
                if (IndividuallyFitted.Contains(comps[i].GetType().Name))
                    return true;
            }
            t = t.parent;
        }
        return false;
    }

    /// <summary>是否更瘦屏：逻辑 UI 高 > 设计高 + 1 时为真（9:16 标准屏逻辑高恒 1280，必须保持原样）。纯工具，两个界面共用，不算通用适配逻辑。</summary>
    public static bool IsThinnerScreen()
    {
        float uiLogicalH = DesignAspectLetterbox.ResolveUiMatch() < 0.5f
            ? GameConfig.DESIGN_WIDTH / Mathf.Max(1e-3f, Screen.width / (float)Screen.height)
            : GameConfig.DESIGN_HEIGHT;
        return uiLogicalH > GameConfig.DESIGN_HEIGHT + 1f;
    }

    // （AdventureUI 竖屏适配已迁移至 AdventureUI.ApplyAdventureFit，见 AdventureUI.cs；本文件不再保留专用实现）
    // （每日登录弹窗竖屏适配已迁移至 DailyLoginUI.ApplyFit，见 DailyLoginUI.cs；本文件不再保留通用实现）

    /// <summary>root 直接子节点里是否已有背景类节点（命名匹配，或带 Image/RawImage 且铺满父级）。</summary>
    static bool HasBackgroundChild(RectTransform root)
    {
        if (root == null) return false;
        for (int i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            if (IsBackdropName(c.name)) return true;
            var rt = c as RectTransform;
            if (rt == null) continue;
            if (c.GetComponent<Image>() != null || c.GetComponent<RawImage>() != null)
            {
                Vector2 curMin, curMax;
                GetRectMinMaxInParent(rt, root, out curMin, out curMax);
                float pw = root.rect.width;
                float ph = root.rect.height;
                // 宽高都铺满父级（>=99%）才视为背景铺满，避免误判普通带图面板。
                if ((curMax.x - curMin.x) >= pw * 0.99f && (curMax.y - curMin.y) >= ph * 0.99f)
                    return true;
            }
        }
        return false;
    }

    /// <summary>背景命名词：Background/Bg/BgStretch/BgFill/BgArt/Backdrop/Dim/ModalDim，以及中文「底」。</summary>
    static bool IsBackdropName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        if (IsFillScreenName(name)) return true;   // BgStretch/BgFill/Bg/Dim/ModalDim
        if (IsFixedArtName(name)) return true;     // Background/BgArt
        if (name.Equals("Backdrop", System.StringComparison.OrdinalIgnoreCase)) return true;
        if (name.Contains("底")) return true;       // 中文节点名里的「底」
        return false;
    }

    /// <summary>子节点矩形四边（父级本地坐标，从父级左下角起算），供出血/铺满判定复用。</summary>
    static void GetRectMinMaxInParent(RectTransform rt, RectTransform parent, out Vector2 min, out Vector2 max)
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

    /// <summary>在直接子节点里按名查找（不递归深层），用于定位已创建的 FitBackdrop。</summary>
    static RectTransform FindDirectChild(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name)) return null;
        for (int i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            if (c.name == name) return c as RectTransform;
        }
        return null;
    }

    /// <summary>下钻用：在直接子节点里找第一个「全 stretch（y 方向拉伸）或铺满父级」的容器（如单一 Root）。</summary>
    static RectTransform FindDrillContainer(Transform root)
    {
        if (root == null) return null;
        var parent = root as RectTransform;
        if (parent == null) return null;
        float pw = parent.rect.width;
        float ph = parent.rect.height;
        for (int i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            var rt = c as RectTransform;
            if (rt == null) continue;
            // 全 stretch（y 方向拉伸）即视为容器
            if (Mathf.Abs(rt.anchorMin.y - rt.anchorMax.y) > 0.001f) return rt;
            // 或宽高都铺满父级（>=99%）也视为容器
            Vector2 curMin, curMax;
            GetRectMinMaxInParent(rt, parent, out curMin, out curMax);
            if ((curMax.x - curMin.x) >= pw * 0.999f && (curMax.y - curMin.y) >= ph * 0.999f)
                return rt;
        }
        return null;
    }

    /// <summary>子节点矩形中心（父级本地坐标，从父级左下角起算）。用于以 map 中心为分界。</summary>
    static Vector2 GetRectCenterInParent(RectTransform rt, RectTransform parent)
    {
        Vector2 pMin = parent.rect.min;
        Vector2 pMax = parent.rect.max;
        Vector2 curMin = new Vector2(
            Mathf.Lerp(pMin.x, pMax.x, rt.anchorMin.x),
            Mathf.Lerp(pMin.y, pMax.y, rt.anchorMin.y)) + rt.offsetMin;
        Vector2 curMax = new Vector2(
            Mathf.Lerp(pMin.x, pMax.x, rt.anchorMax.x),
            Mathf.Lerp(pMin.y, pMax.y, rt.anchorMax.y)) + rt.offsetMax;
        return (curMin + curMax) * 0.5f;
    }

    static RectTransform FindNamed(Transform root, params string[] names)
    {
        if (root == null || names == null) return null;
        for (int i = 0; i < names.Length; i++)
        {
            var t = FindDeep(root, names[i]);
            if (t != null) return t as RectTransform;
        }
        return null;
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name)) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var f = FindDeep(root.GetChild(i), name);
            if (f != null) return f;
        }
        return null;
    }

    /// <summary>子节点世界矩形底边，换算到 parent 本地 Y（从 parent 底边向上）。</summary>
    static float GetWorldRectBottomInParent(RectTransform child, RectTransform parent)
    {
        var corners = new Vector3[4];
        child.GetWorldCorners(corners);
        // 0=左下 1=左上
        Vector3 local = parent.InverseTransformPoint(corners[0]);
        return local.y - parent.rect.yMin;
    }

    static float GetWorldRectTopInParent(RectTransform child, RectTransform parent)
    {
        var corners = new Vector3[4];
        child.GetWorldCorners(corners);
        Vector3 local = parent.InverseTransformPoint(corners[1]);
        return local.y - parent.rect.yMin;
    }

    // （每日登录弹窗竖屏适配已迁移至 DailyLoginUI.ApplyFit，见 DailyLoginUI.cs；本文件不再保留通用实现）
}