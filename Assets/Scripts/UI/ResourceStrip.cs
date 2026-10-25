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
/// 模式（只切显隐，不动几何；位置全按主人在 prefab 里摆的 2×2 格：上排金币+钻石、下排天赋石+体力，
/// 两列就只占第一行、三列补第二行天赋石位——2026-10-25 主人口径「该隐藏隐藏，哪个该补位置就补位置」）：
///   Main          挂回 TopBar：金币/钻石/天赋石/体力四格全显（主界面与冒险页共用观感）；
///   GoldDiaTalent 宿主界面三列：金币/钻石/天赋石（角色页），体力隐藏；
///   GoldDia       宿主界面两列：金币/钻石（只占第一行），天赋石/体力隐藏（升阶/招募/抽卡弹窗）。
///
/// 加号分工（2026-10-25 主人拍板）：
///   金币/体力加号 = 每日补给位（前2次免费 → 第3次50钻 → 第4次100钻、每日4次封顶，逻辑在 ResourceAdRewards），
///                  主界面与借条界面都显示——弹窗里也可以购买（2026-10-25 13:5x 主人拍板，T14「借条藏加号」口径作废）；
///   钻石加号     = 暂时隐藏（后期点它直接看广告，接好再放）；
///   天赋石加号   = 直接打开冒险日志里的历程（里程）商店（OpenTalentStoneShop）。
///
/// 右上角自适应（2026-10-25 主人拍板「这个自适应到屏幕右上角」）：各列锚点运行时改挂顶条右缘，
/// 横坐标 = 主人 prefab 摆的原坐标 − 设计宽一半，右边距在任意屏宽下恒定，宽屏自动贴右。
/// 体力列显隐（2026-10-25 主人拍板）：主界面/冒险页显示；借走资源条的消耗界面（招募/升阶/抽卡/角色页）隐藏。
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

            // 【2026-10-25 主人要求「每个界面的位置都统一」】借用期间 TopBar 的 y 仍由
            // SafeAreaFitter 实时管辖（旋转/分屏/刘海变化会动），条根的 y 是 Show 时刻的快照，
            // 不跟着走就会和主界面顶条错位 → 借用期间每帧同 y（与 TopBar 同源，Home 态不需要：
            // 条撑满 TopBar，y 天然跟它一致）。
            if (_borrowed) SyncTopY();
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
        _borrowed = true;        // Host.Update 借用期间每帧同 y（位置与主界面顶条恒统一）
        ApplyTopBarGeometry();   // 宿主页与 TopBar 同处画布空间：恢复「拷 TopBar 几何」形态（Home 是撑满形态，来回切必须重施）
        SyncTopY();
        SetMode(mode);
        SetStaminaVisible(false);   // 【2026-10-25 主人拍板】消耗金币/钻石的界面不显示体力列（归还时恢复）
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
        SetStaminaVisible(true);   // 归还主界面：体力列恢复（主界面/冒险页显示）
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

    /// <summary>宿主页形态几何：按 TopBar 当前画布空间值拷锚/pivot/尺寸（y 由 SyncTopY 现同步）。</summary>
    static void ApplyTopBarGeometry() => CopyTopBarGeometry(_home as RectTransform);

    /// <summary>只同步条根的 y 与主界面 TopBar 当前值一致（TopBar 自身挂 SafeAreaFitter，效果经此传导，条根自己不挂，防双倍偏移）。x 恒 0、锚 stretch-top，与 TopBar 同构。</summary>
    static void SyncTopY()
    {
        var rt = _root as RectTransform;
        var home = _home as RectTransform;
        if (rt == null || home == null) return;
        rt.anchoredPosition = new Vector2(0f, home.anchoredPosition.y);
    }

    static void SetMode(Mode mode)
    {
        if (_gold != null) _gold.gameObject.SetActive(true);
        if (_dia != null) _dia.gameObject.SetActive(true);
        // 天赋石列：Main（主界面四格全显）与 GoldDiaTalent（角色页三列）显示；GoldDia（两列弹窗）只占第一行 → 隐藏
        if (_talent != null) _talent.gameObject.SetActive(mode != Mode.GoldDia);

        // 加号显隐（2026-10-25 主人拍板）：金币加号 = 每日补给位（前2次免费→50钻→100钻，逻辑在
        // ResourceAdRewards），主界面与借条界面（招募/升阶/抽卡弹窗）都显示——弹窗里也可以购买
        // （2026-10-25 13:5x 主人拍板「可以购买」，T14「借条藏加号」口径作废）；
        // 钻石加号已在迁入时压掉（后期接广告），这里不再放开；天赋石加号随预制体显示（已接历程商店）。
        var gp = _gold != null ? FindDeep(_gold, "PlusButton") : null;
        if (gp != null && !gp.gameObject.activeSelf) gp.gameObject.SetActive(true);

        ApplyRightAlign();
    }

    /// <summary>体力列显隐（体力列不迁，仍归主界面 TopBar）：主界面/冒险页显示；借走资源条的
    /// 消耗界面（招募/升阶/抽卡/角色页）隐藏（2026-10-25 主人拍板）。体力列被主人从 prefab
    /// 删掉时静默跳过（摆件归主人管，不作缺真值报错）。</summary>
    static void SetStaminaVisible(bool visible)
    {
        if (_stamina == null) return;
        if (_stamina.gameObject.activeSelf != visible)
            _stamina.gameObject.SetActive(visible);
    }

    /// <summary>【2026-10-25 主人拍板「这个自适应到屏幕右上角」】把各列整体贴到顶条右缘：
    /// 三列（金币/钻石/天赋石，条根子级）与体力列（TopBar 原生子级）的锚点改挂右缘（anchor x=1），
    /// 横坐标 = 主人在 TopBar.prefab 里摆的原坐标 − 设计宽一半（720/2，GameConfig.DESIGN_WIDTH）。
    /// 主人是按 720 设计宽摆的，减去设计宽一半后「距右缘的边距」在任意屏宽下恒定：
    /// 瘦屏（Match Width，逻辑宽恒 720）观感不变；宽屏（逻辑宽 > 720）整组自动贴住右上角跟着右缘走。
    /// 纵坐标/锚 y/尺寸一律不动（基线全来自 prefab，不写死像素）。锚 x=1 后跟随右缘是锚点天性，无需轮询。</summary>
    static void ApplyRightAlign()
    {
        float halfDesignW = GameConfig.DESIGN_WIDTH * 0.5f;
        AlignColumn(_gold, _goldHomePos, halfDesignW);
        AlignColumn(_dia, _diaHomePos, halfDesignW);
        AlignColumn(_talent, _talentHomePos, halfDesignW);
        AlignColumn(_stamina, _staminaHomePos, halfDesignW);
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

    /// <summary>建根时按 TopBar 当前几何拷贝锚/pivot/尺寸（原值沿用，不写死；y 每次 Home/Show 现同步）。</summary>
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
