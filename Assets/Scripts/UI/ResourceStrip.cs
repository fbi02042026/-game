using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【2026-10-24 主人拍板「直接把顶条的资源条提取出来，哪个界面要显示就显示，不要复制，
/// 只是把里面的图标和数值替换」】主界面顶条资源区的**全局唯一实例**。
///
/// 结构（2026-10-25 起顶条真源 = 独立预制体 Resources/Prefabs/Town/TopBar.prefab，主人自己摆）：
/// 把 TopBar.prefab 里的 GoldPanel / 钻石Panel / 天赋石Panel 三条原生列**迁移**（SetParent，非复制）挂到
/// 本条根下——主界面顶条从此显示的就是这一条（同位同观感）。
/// 天赋石列 2026-10-25 起主人已直接摆进预制体（天赋石Icon/天赋石Text），不再克隆金币列。
/// 体力列不迁，仍归顶条；标题徽章/邮件/设置已由主人从预制体删除。
///
/// 用法：
///   · GuildHallUI 绑完金/钻引用后调 <see cref="RegisterHall"/>（幂等；场景重载后旧列销毁自动重提取）；
///   · 任意界面要显示：Show(hostTransform, mode) —— 整条迁过去，同图标同样式，只按模式切显隐；
///   · 用完 Release() 归还主界面（成对调用；宿主直接销毁时条随场景走，下次 RegisterHall 重建）。
///
/// 模式（只切显隐；几何按 2026-10-27 主人晚間新规：两个并排上排、三个横排一排（天赋石接排在金币
/// 左侧）、四个按 prefab 原生 2×2——见 ApplyRightAlign）：
///   Main          挂回 TopBar：金币/钻石/天赋石/体力四格全显（主界面与冒险页统一观感）；
///   GoldDiaTalent 宿主界面三列一排：金币/钻石/天赋石（角色页），体力隐藏；天赋石可视框对齐金币左侧等距槽位；
///   GoldDia       宿主界面两列：金币/钻石（招募/升阶/抽卡弹窗、酒馆页），天赋石/体力隐藏。
/// 【2026-10-27 主人拍板】原 DiamondStamina（冒险页只显钻石+体力）删除——冒险页参考主界面统一四格。
///
/// 加号分工（2026-10-25 主人拍板）：
///   金币/体力加号 = 每日补给位（前2次免费 → 第3次50钻 → 第4次100钻、每日4次封顶，逻辑在 ResourceAdRewards），
///                  主界面与借条界面都显示——弹窗里也可以购买（2026-10-25 13:5x 主人拍板，T14「借条藏加号」口径作废）；
///   钻石加号     = 暂时隐藏（后期点它直接看广告，接好再放）；
///   天赋石加号   = 直接打开冒险日志里的历程（里程）商店（OpenTalentStoneShop）。
///
/// 右上角自适应（2026-10-25 主人拍板「这个自适应到屏幕右上角」）：各列锚点运行时改挂顶条右缘，
/// 横坐标 = 主人 prefab 摆的原坐标 − 设计宽一半，右边距在任意屏宽下恒定，宽屏自动贴右。
/// 体力列显隐（2026-10-27 起收口在 SetMode 单一出口）：仅 Main（主界面/冒险页）显示；
/// 借走资源条的界面（角色页/招募/升阶/抽卡）与酒馆页一律隐藏。
///
/// 数值唯一真源 = ResourceWallet（Host 轮询、变了才写）；格式唯一出口 = GuildHallUI.FormatResource。
/// 坐标基线全部来自主人在 TopBar.prefab 里的摆位，运行时不写死像素（铁律 20）。
/// 缺列 fail closed：LogError 不兜底（AGENTS.md §4）。
/// </summary>
public static class ResourceStrip
{
    public enum Mode { Main, GoldDiaTalent, GoldDia }

    const string StripName = "ResourceStrip";

    /// <summary>挂在条根上的数字轮询：真源 ResourceWallet，变了才写（与体力 HUD 轮询同口径）。</summary>
    class Host : MonoBehaviour
    {
        public Text goldText, diaText, talentText;
        long _gold = -1, _dia = -1, _talent = -1;

        void Update()
        {
            var data = SaveSystem.Instance?.Data;
            if (data == null) return;
            long g = ResourceWallet.Get(data, ResourceWallet.ResourceType.Gold);
            if (g != _gold && goldText != null) { goldText.text = GuildHallUI.FormatResource(g); _gold = g; }
            long d = ResourceWallet.Get(data, ResourceWallet.ResourceType.Diamond);
            if (d != _dia && diaText != null) { diaText.text = GuildHallUI.FormatResource(d); _dia = d; }
            long t = ResourceWallet.Get(data, ResourceWallet.ResourceType.TalentPoint);
            if (t != _talent && talentText != null) { talentText.text = GuildHallUI.FormatResource(t); _talent = t; }

            // 【2026-10-25 主人要求「每个界面的位置都统一」；2026-10-26 改为整条世界对齐】
            // 借用期间 TopBar 的几何仍由 SafeAreaFitter 实时管辖（旋转/分屏/刘海变化会动），
            // 条根若停在 Show 时刻的快照就会和主界面顶条错位 → 借用期间每帧按 TopBar 世界矩形重对齐
            // （与 TopBar 同源；Home 态不需要：条撑满 TopBar，天然一致）。
            if (_borrowed) SyncToHomeWorld();
        }
    }

    static Transform _root;      // 条根（无 Graphic、无裁剪），平时挂主界面 TopBar，Show 时迁宿主
    static Host _host;
    static Transform _home;      // 当前主界面 TopBar（条的家）
    static bool _borrowed;       // 是否被某界面借走（Show=true / Home=false）；Host.Update 借用期间实时同 y 用
    static Transform _gold, _dia, _talent;
    static Transform _stamina;   // 体力列（不迁，仍归 TopBar；主界面/冒险页显示，消耗界面隐藏）
    static Vector2 _goldHomePos, _diaHomePos, _talentHomePos, _staminaHomePos;
    //                         ^ 各列「主人在 TopBar.prefab 里摆的原位」= 右上角对齐基线（迁出/对齐前捕获，绝不二次覆盖）
    // 各列可视底框（Bg）相对面板中心的 x 偏移（prefab 真值：金币 -10.9 / 钻石 -15.4 / 天赋石 -2）。
    // 主人的摆位是按可视框对齐的，跨列重排槽位时必须按可视框中心算，直接抄面板 x 会错开十几像素
    // （2026-10-27 主人截图「角色页天赋石没有和钻石对齐」）。体力不参与跨槽位重排，无需捕获。
    static float _goldBgOffsetX, _diaBgOffsetX, _talentBgOffsetX;

    // —— 对外：主界面注册（GuildHallUI.AutoBindMissingRefs 末尾调）——
    public static void RegisterHall(Transform hallRoot)
    {
        var topBar = hallRoot != null ? TownSharedChrome.FindDeep(hallRoot, "TopBar") : null;
        if (topBar == null)
        {
            Debug.LogError("[ResourceStrip] 主界面顶条找不到（TopBar）——资源条不启用");
            return;
        }

        // 旧根还在但列已随旧场景销毁（Unity 假 null）：整根重建，绝不在旧根上缝补
        if (_root != null && (_gold == null || _dia == null || _talent == null))
        {
            Object.Destroy(_root.gameObject);
            _root = null; _host = null; _gold = null; _dia = null; _talent = null;
        }

        if (_root == null)
        {
            var go = new GameObject(StripName, typeof(RectTransform));
            _root = go.transform;
            _host = go.AddComponent<Host>();
            CopyTopBarGeometry(topBar as RectTransform);
        }
        _home = topBar;

        if (_stamina == null)
        {
            _stamina = FindDirectChild(topBar, "体力Panel");
            if (_stamina != null)
                _staminaHomePos = ((RectTransform)_stamina).anchoredPosition;   // 对齐基线（迁出前捕获，体力列不迁）
            // 体力列被主人从 prefab 删掉时不报错：显隐与对齐都静默跳过（摆件归主人管）
        }

        if (_gold == null)
        {
            var src = FindDirectChild(topBar, "GoldPanel");
            if (src == null)
            {
                Debug.LogError("[ResourceStrip] TopBar 下没有 GoldPanel —— 金币列不显示（不兜底）");
                return;
            }
            _gold = src;
            _goldHomePos = ((RectTransform)src).anchoredPosition;   // 迁出前记主人摆的原位（右上角对齐基线）
            _goldBgOffsetX = BgOffsetX(src, "GoldBg");
            src.SetParent(_root, false);   // 原生节点迁出 = 提取（非复制），local 坐标原样保留
            _host.goldText = FindDeep(_gold, "GoldText")?.GetComponent<Text>();
            if (_host.goldText == null)
                Debug.LogError("[ResourceStrip] GoldPanel 里没有 GoldText —— 金币数字将不显示（不兜底）");
        }

        if (_dia == null)
        {
            var src = FindDirectChild(topBar, "钻石Panel");
            if (src == null)
            {
                Debug.LogError("[ResourceStrip] TopBar 下没有 钻石Panel —— 钻石列不显示（不兜底）");
                return;
            }
            _dia = src;
            _diaHomePos = ((RectTransform)src).anchoredPosition;   // 迁出前记原位（三列模式还原用）
            _diaBgOffsetX = BgOffsetX(src, "钻石Bg");
            src.SetParent(_root, false);
            _host.diaText = FindDeep(_dia, "钻石Text")?.GetComponent<Text>();
            if (_host.diaText == null)
                Debug.LogError("[ResourceStrip] 钻石Panel 里没有 钻石Text —— 钻石数字将不显示（不兜底）");
            // 【2026-10-25 主人拍板】钻石加号先隐藏（后期点它直接看广告，接好再放出来）；
            // SetMode 不再放开它，只有这里这一处显隐出口
            var dplus = FindDeep(_dia, "PlusButton");
            if (dplus != null) dplus.gameObject.SetActive(false);
        }

        if (_talent == null)
        {
            var src = FindDirectChild(topBar, "天赋石Panel");
            if (src == null)
            {
                Debug.LogError("[ResourceStrip] TopBar 下没有 天赋石Panel —— 天赋石列不显示（不兜底）");
                return;
            }
            _talent = src;
            _talentHomePos = ((RectTransform)src).anchoredPosition;   // 迁出前记主人摆的原位（右上角对齐基线）
            _talentBgOffsetX = BgOffsetX(src, "天赋石Bg");
            src.SetParent(_root, false);
            _host.talentText = FindDeep(_talent, "天赋石Text")?.GetComponent<Text>();
            if (_host.talentText == null)
                Debug.LogError("[ResourceStrip] 天赋石Panel 里没有 天赋石Text —— 天赋石数字将不显示（不兜底）");
            // 【2026-10-25 主人拍板】天赋石加号 = 直接打开冒险日志里的历程（里程）商店；
            // 图标/显隐仍以预制体原样为准，这里只接点击
            var tbtn = FindDeep(_talent, "PlusButton")?.GetComponent<Button>();
            if (tbtn != null)
            {
                tbtn.onClick.RemoveAllListeners();
                tbtn.onClick.AddListener(OpenTalentStoneShop);
            }
            else
                Debug.LogError("[ResourceStrip] 天赋石Panel 里没有 PlusButton —— 历程商店入口接不上（不兜底）");
        }

        Home();
    }

    /// <summary>宿主界面显示：整条迁到 host 根下（贴顶，同主界面顶条几何），按 mode 切列显隐。</summary>
    public static bool Show(Transform host, Mode mode)
    {
        if (_root == null || _home == null)
        {
            Debug.LogError("[ResourceStrip] 主界面顶条尚未注册（GuildHallUI 未初始化）——资源条无处可迁");
            return false;
        }
        if (host == null) return false;

        _root.SetParent(host, false);
        _root.SetAsLastSibling();
        _root.gameObject.SetActive(true);
        _borrowed = true;        // Host.Update 借用期间每帧世界对齐（位置与主界面顶条恒统一）
        SyncToHomeWorld();       // 【2026-10-26 主人反馈「角色页资源条没贴右上角」】按 TopBar 世界矩形落位，
                                 // 不再拷局部几何 —— 宿主页根层级/锚点与大厅不同构时拷数字必错位，
                                 // 世界对齐后借条与主界面顶条像素级重合（任意页面/任意屏比都成立）
        // 【2026-10-27 主人拍板】体力列显隐收口在 SetMode 内（先切列显隐再算槽位，不会把旧态算进可见数）
        SetMode(mode);
        return true;
    }

    /// <summary>宿主用完归还主界面（与 Show 成对调用）。</summary>
    public static void Release() => Home();

    static void Home()
    {
        if (_root == null) return;
        if (_home == null)
        {
            _root.gameObject.SetActive(false);   // 主界面不在（战斗中/切场景）：收起，等 RegisterHall 重建
            return;
        }
        _root.SetParent(_home, false);
        _root.SetAsLastSibling();
        _root.gameObject.SetActive(true);
        _borrowed = false;       // 回家：条撑满 TopBar，y 天然一致，不再需要每帧同 y
        FillHomeBar();           // 条在 TopBar 内部：撑满 TopBar（TopBar 局部空间 ≠ 画布空间，照搬它的 y 会整体下沉半条高）
        SetMode(Mode.Main);
        // 酒馆页开着（如招募弹窗关闭归还主界面）：资源条别以四列裸露在酒馆，回到「只留金币/钻石」
        if (TavernUI.Instance != null && TavernUI.Instance.gameObject.activeSelf)
            SetMode(Mode.GoldDia);
    }

    /// <summary>条挂在 TopBar 下时的几何 = 撑满父级（锚拉伸 + offset 全 0，不写死像素）；
    /// 条的局部空间与 TopBar 完全一致，迁进来的 GoldPanel/钻石Panel 原局部坐标即回原位。</summary>
    static void FillHomeBar()
    {
        var rt = _root as RectTransform;
        if (rt == null || _home == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    /// <summary>
    /// 【2026-10-26 主人反馈「角色页资源条没贴右上角」】借出形态落位：按 TopBar 的**世界矩形**对齐，
    /// 替代旧的「拷 TopBar 局部几何」（锚/pivot/sizeDelta/y 逐项抄）——宿主页根（角色页/弹窗）与
    /// 大厅根不同构时，局部数字相同 ≠ 屏上位置相同，必错位；世界对齐后借条与主界面顶条像素级重合。
    /// 条根锚定到宿主父级中心 (0.5,0.5)、pivot (0.5,0.5)，sizeDelta = TopBar 世界尺寸 ÷ 宿父级 lossyScale，
    /// anchoredPosition = TopBar 世界中心换算到宿父级局部 − 锚点原点。列的右缘锚（ApplyRightAlign）
    /// 相对条根右缘计算，条根右缘 = TopBar 右缘 ⇒ 各界面与主界面右上角观感恒一致。
    /// SafeAreaFitter 对 TopBar 的实时调整经 Host.Update 每帧重对齐传导（条根自己不挂，防双倍偏移）。
    /// </summary>
    static readonly Vector3[] _worldCorners = new Vector3[4];
    static void SyncToHomeWorld()
    {
        var rt = _root as RectTransform;
        var home = _home as RectTransform;
        if (rt == null || home == null) return;
        var parent = rt.parent as RectTransform;
        if (parent == null) return;

        home.GetWorldCorners(_worldCorners);
        Vector3 worldCenter = (_worldCorners[0] + _worldCorners[2]) * 0.5f;
        float worldW = Vector3.Distance(_worldCorners[0], _worldCorners[3]);   // 底边（左→右）
        float worldH = Vector3.Distance(_worldCorners[0], _worldCorners[1]);   // 左边（下→上）
        float sx = Mathf.Abs(parent.lossyScale.x); if (sx < 1e-4f) sx = 1e-4f;
        float sy = Mathf.Abs(parent.lossyScale.y); if (sy < 1e-4f) sy = 1e-4f;

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        rt.sizeDelta = new Vector2(worldW / sx, worldH / sy);

        // anchoredPosition 原点 = 锚点 (0.5,0.5) 在父级局部的位置（父 rect 是 pivot 相对系，做差校正）
        Vector3 local = parent.InverseTransformPoint(worldCenter);
        var pr = parent.rect;
        Vector2 anchorOrigin = new Vector2(pr.width * (0.5f - parent.pivot.x), pr.height * (0.5f - parent.pivot.y));
        rt.anchoredPosition = (Vector2)local - anchorOrigin;
    }

    /// <summary>切资源条列显隐（体力列显隐一并收口在此，2026-10-27 主人拍板）：
    /// 金币/钻石恒显；天赋石除 GoldDia（两列）外都显；体力仅 Main（主界面/冒险页）显示。</summary>
    public static void SetMode(Mode mode)
    {
        // 钻石列：所有模式都显示（主界面/冒险页/酒馆/各类弹窗都看钻石）
        if (_dia != null) _dia.gameObject.SetActive(true);
        // 金币列：所有模式都显示（冒险页 2026-10-27 起参考主界面统一，四格全显）
        if (_gold != null) _gold.gameObject.SetActive(true);
        // 天赋石列：仅 GoldDia（两列弹窗/酒馆页）隐藏
        bool talentOn = mode != Mode.GoldDia;
        if (_talent != null) _talent.gameObject.SetActive(talentOn);

        // 体力列：仅 Main 显示（原 Show/Home 两处的显隐收口到这一处）
        SetStaminaVisible(mode == Mode.Main);

        // 金币加号（每日补给入口）：随金币列恒显（2026-10-25 主人拍板，弹窗内也可购买）
        var gp = _gold != null ? FindDeep(_gold, "PlusButton") : null;
        if (gp != null && !gp.gameObject.activeSelf)
            gp.gameObject.SetActive(true);

        ApplyRightAlign();
    }

    /// <summary>体力列显隐（体力列不迁，仍归主界面 TopBar）：仅 Main（主界面/冒险页）显示；
    /// 角色页/招募/升阶/抽卡等借条界面与酒馆页隐藏（2026-10-27 主人拍板，显隐唯一调用点 = SetMode）。
    /// 体力列被主人从 prefab 删掉时静默跳过（摆件归主人管，不作缺真值报错）。</summary>
    static void SetStaminaVisible(bool visible)
    {
        if (_stamina == null) return;
        if (_stamina.gameObject.activeSelf != visible)
            _stamina.gameObject.SetActive(visible);
    }

    /// <summary>【2026-10-25 主人拍板「这个自适应到屏幕右上角」+ 2026-10-27 槽位新规（三个横排一排）】把各列整体贴到顶条右缘：
    /// 列的锚点改挂右缘（anchor x=1），横坐标 = 目标槽位原坐标 − 设计宽一半（720/2，GameConfig.DESIGN_WIDTH）。
    /// 右缘锚后「距右缘的边距」在任意屏宽下恒定：瘦屏（Match Width，逻辑宽恒 720）观感不变；
    /// 宽屏（逻辑宽 > 720）整组自动贴住右上角跟着右缘走。
    /// 纵坐标/锚 y/尺寸一律不动（基线全来自 prefab，不写死像素）。锚 x=1 后跟随右缘是锚点天性，无需轮询。</summary>
    static void ApplyRightAlign()
    {
        float halfDesignW = GameConfig.DESIGN_WIDTH * 0.5f;

        // —— 槽位（2026-10-27 主人「角色界面把这三个变成一排」，覆盖同日早前折行口径）——
        // 可见列按显示序（金币→钻石→天赋石→体力，即 Main 模式阅读顺序）收集，按可见数分配槽位：
        //   N=4（Main）：prefab 原生 2×2 原样（各回各家：金币+钻石上排、天赋石+体力下排）；
        //   N=3（角色页）：三列横排一排——第 1/2 个占上排左/右（金币/钻石槽），第 3 个接排在金币左侧、
        //        列距=金币→钻石可视框间距、y 同上排（按可视框中心算，见分支内注释）；
        //   N=2：并排占上排左/右两槽（金币/钻石各自 home，即主人要的并排）；
        //   N<=1：各回各家。
        // 槽坐标全部取自主人在 TopBar.prefab 里摆的列位（上排=金币/钻石 home），不写死像素。
        var visible = new List<(Transform col, Vector2 home)>();
        if (_gold != null && _gold.gameObject.activeSelf) visible.Add((_gold, _goldHomePos));
        if (_dia != null && _dia.gameObject.activeSelf) visible.Add((_dia, _diaHomePos));
        if (_talent != null && _talent.gameObject.activeSelf) visible.Add((_talent, _talentHomePos));
        if (_stamina != null && _stamina.gameObject.activeSelf) visible.Add((_stamina, _staminaHomePos));

        for (int i = 0; i < visible.Count; i++)
        {
            Vector2 slot = visible[i].home;
            if (visible.Count == 2)
            {
                slot = i == 0 ? _goldHomePos : _diaHomePos;
            }
            else if (visible.Count == 3)
            {
                if (i == 2)
                {
                    // 【2026-10-27 主人拍板「角色界面把这三个变成一排」】三个可见列横排一排（覆盖同日早前
                    // 「第一排 2 个+第二排 1 个」折行口径）：金币/钻石各回 home（上排），天赋石接排到金币左侧，
                    // 列距 = 主人摆的金币→钻石「可视框中心」间距，y 与金币同排。按可视框中心算——各列 Bg
                    // 偏移不同（金币 -10.9 / 钻石 -15.4 / 天赋石 -2），直接抄面板 x 会错开十几像素。
                    float visualSpacing = (_diaHomePos.x + _diaBgOffsetX) - (_goldHomePos.x + _goldBgOffsetX);
                    float talentX = (_goldHomePos.x + _goldBgOffsetX) - visualSpacing - _talentBgOffsetX;
                    slot = new Vector2(talentX, _goldHomePos.y);
                }
                // i=0/1 即金币/钻石 home（上排左/右），home 本身就是槽位
            }
            AlignColumn(visible[i].col, slot, halfDesignW);
        }
    }

    /// <summary>列可视底框（Bg）相对面板中心的 x 偏移。Bg 是主人手摆摆件：改名/删除按 0 处理
    /// （退化为按面板矩形对齐，不报错——与体力列摆件「删了静默跳过」同一口径）。</summary>
    static float BgOffsetX(Transform panel, string bgName)
    {
        var bg = FindDirectChild(panel, bgName) as RectTransform;
        return bg != null ? bg.anchoredPosition.x : 0f;
    }

    static void AlignColumn(Transform col, Vector2 homePos, float halfDesignW)
    {
        if (col == null) return;
        var rt = col as RectTransform;
        if (rt == null) return;
        rt.anchorMin = new Vector2(1f, rt.anchorMin.y);
        rt.anchorMax = new Vector2(1f, rt.anchorMax.y);
        rt.anchoredPosition = new Vector2(homePos.x - halfDesignW, homePos.y);
    }

    /// <summary>
    /// 天赋石加号（2026-10-25 主人拍板）：切到冒险日志页并直接打开历程（里程）商店。
    /// 链路 = TownHubController.OpenAdventureLog()（与底栏切页同一条路）→
    /// AdventureLogPhase3Panel.Ensure(log).OpenShopPanel()（与侧栏 Emblem 徽记同一条路）。
    /// </summary>
    static void OpenTalentStoneShop()
    {
        var hub = TownHubController.Instance;
        if (hub == null)
        {
            UIManager.Instance?.ShowToast("主界面未就绪，请从底栏打开冒险日志");
            return;
        }
        hub.OpenAdventureLog();   // 切到冒险日志页（已在日志页时仅保持选中，幂等）
        var log = AdventureLogUI.Instance;
        if (log == null)
        {
            Debug.LogError("[ResourceStrip] 冒险日志页实例取不到（AdventureLogUI.Instance）——历程商店打不开");
            return;
        }
        var phase3 = AdventureLogPhase3Panel.Ensure(log);
        if (phase3 == null)
        {
            Debug.LogError("[ResourceStrip] 三期面板创建失败（AdventureLogPhase3Panel.Ensure）——历程商店打不开");
            return;
        }
        phase3.OpenShopPanel();
    }

    /// <summary>建根时按 TopBar 当前几何拷贝锚/pivot/尺寸（仅初始占位；借出形态实际由 SyncToHomeWorld 世界对齐，
    /// Home 形态由 FillHomeBar 撑满，原值沿用不写死）。</summary>
    static void CopyTopBarGeometry(RectTransform topBar)
    {
        var rt = _root as RectTransform;
        if (rt == null || topBar == null) return;
        rt.anchorMin = topBar.anchorMin;
        rt.anchorMax = topBar.anchorMax;
        rt.pivot = topBar.pivot;
        rt.sizeDelta = topBar.sizeDelta;
        rt.anchoredPosition = new Vector2(0f, topBar.anchoredPosition.y);
        rt.localScale = Vector3.one;
    }

    static Transform FindDirectChild(Transform parent, string name)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c != null && c.name == name) return c;
        }
        return null;
    }

    static Transform FindDeep(Transform parent, string name)
    {
        if (parent == null || string.IsNullOrEmpty(name)) return null;
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var hit = FindDeep(parent.GetChild(i), name);
            if (hit != null) return hit;
        }
        return null;
    }
}
