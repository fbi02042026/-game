using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【2026-10-24 主人拍板「直接把顶条的资源条提取出来，哪个界面要显示就显示，不要复制，
/// 只是把里面的图标和数值替换」】主界面顶条资源区的**全局唯一实例**。
///
/// 结构：把主界面 TopBar 里的 GoldPanel / 钻石Panel 两个原生列**迁移**（SetParent，非复制）
/// 挂到本条根下 —— 主界面顶条从此显示的就是这一条（同位同观感）；另克隆一列天赋石
/// （预制体里没有天赋石列，一次性构造，随场景失效自动重建）。体力列 / 邮件 / 设置不迁，仍归顶条。
///
/// 用法：
///   · GuildHallUI 绑完金/钻引用后调 <see cref="RegisterHall"/>（幂等；场景重载后旧列销毁自动重提取）；
///   · 任意界面要显示：Show(hostTransform, mode) —— 整条迁过去，同图标同样式，只按模式切显隐；
///   · 用完 Release() 归还主界面（成对调用；宿主直接销毁时条随场景走，下次 RegisterHall 重建）。
///
/// 模式（只切显隐，不动任何几何）：
///   Main         挂回 TopBar 原位：金/钻显示，天赋石隐藏（主界面原生观感）；
///   GoldDiaTalent 宿主界面三列：金币/钻石/天赋石（角色页）；
///   GoldDia      宿主界面两列：金币/钻石，加号隐藏（升阶弹窗 T14 口径「加号不接点击」）。
///
/// 数值唯一真源 = ResourceWallet（Host 轮询、变了才写）；格式唯一出口 = GuildHallUI.FormatResource。
/// 坐标全部运行时从 TopBar/列读取沿用（迁移保留 local 值），不写死（铁律 20）。
/// 缺列 fail closed：LogError 不兜底（AGENTS.md §4）。
/// </summary>
public static class ResourceStrip
{
    public enum Mode { Main, GoldDiaTalent, GoldDia }

    const string StripName = "ResourceStrip";
    const string TalentIconPath = "Art/UI/Icons/Common/icon_talent_stone";

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
        }
    }

    static Transform _root;      // 条根（无 Graphic、无裁剪），平时挂主界面 TopBar，Show 时迁宿主
    static Host _host;
    static Transform _home;      // 当前主界面 TopBar（条的家）
    static Transform _gold, _dia, _talent;

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
        if (_root != null && (_gold == null || _dia == null))
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

        if (_gold == null)
        {
            var src = FindDirectChild(topBar, "GoldPanel");
            if (src == null)
            {
                Debug.LogError("[ResourceStrip] TopBar 下没有 GoldPanel —— 金币列不显示（不兜底）");
                return;
            }
            _gold = src;
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
            src.SetParent(_root, false);
            _host.diaText = FindDeep(_dia, "钻石Text")?.GetComponent<Text>();
            if (_host.diaText == null)
                Debug.LogError("[ResourceStrip] 钻石Panel 里没有 钻石Text —— 钻石数字将不显示（不兜底）");
        }

        if (_talent == null)
            BuildTalentColumn(topBar as RectTransform);

        Home();
    }

    /// <summary>
    /// 天赋石列 = 克隆金币列（一次性；预制体里没有天赋石列，这是必要构造非兜底）：
    /// 换图标为天赋石、藏 PlusButton（天赋石无充值入口），位置沿用顶条体力列的坐标位
    /// （主界面 2×2 格：上排体力+金币、下排钻石，天赋石补下排/上排空位视体力位而定）。
    /// </summary>
    static void BuildTalentColumn(RectTransform topBar)
    {
        if (_gold == null) return;
        var stamina = FindDirectChild(topBar, "体力Panel") as RectTransform;
        if (stamina == null)
        {
            Debug.LogError("[ResourceStrip] TopBar 下没有 体力Panel —— 天赋石列没有可沿用的坐标位（不兜底）");
            return;
        }

        var clone = Object.Instantiate(_gold.gameObject, _root, false);
        clone.name = "TalentPanel";
        var icon = FindDeep(clone.transform, "CoinIcon")?.GetComponent<Image>();
        if (icon != null)
        {
            var sp = Resources.Load<Sprite>(TalentIconPath);
            if (sp != null) icon.sprite = sp;
            else Debug.LogError("[ResourceStrip] 天赋石图标取不到：" + TalentIconPath + " —— 克隆列维持金币图");
        }
        else
            Debug.LogError("[ResourceStrip] 克隆金币列里没有 CoinIcon —— 天赋石图标不换（不兜底）");
        var plus = FindDeep(clone.transform, "PlusButton");
        if (plus != null) plus.gameObject.SetActive(false);
        else Debug.LogError("[ResourceStrip] 克隆金币列里没有 PlusButton —— 天赋石列加号未隐藏（不兜底）");

        var rt = clone.transform as RectTransform;
        if (rt != null)
        {
            rt.anchoredPosition = stamina.anchoredPosition;   // 只沿用坐标，不改锚点/尺寸
            if (_host != null)
                _host.talentText = FindDeep(rt, "GoldText")?.GetComponent<Text>();
        }
        if (_host != null && _host.talentText == null)
            Debug.LogError("[ResourceStrip] TalentPanel 里没有 GoldText —— 天赋石数字将不显示（不兜底）");
        _talent = clone.transform;
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
        SyncTopY();
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
        SyncTopY();
        SetMode(Mode.Main);
    }

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
        if (_talent != null) _talent.gameObject.SetActive(mode == Mode.GoldDiaTalent);

        // 加号显隐：Main 交还原生逻辑（显示，回调是 GuildHallUI 挂的广告领取，随节点迁移仍有效）；
        // GoldDia（升阶弹窗）按 T14 口径不接点击 → 藏；GoldDiaTalent 同升阶口径藏（天赋页加号易误触充值广告）。
        bool plusOn = mode == Mode.Main;
        var gp = _gold != null ? FindDeep(_gold, "PlusButton") : null;
        if (gp != null) gp.gameObject.SetActive(plusOn);
        var dp = _dia != null ? FindDeep(_dia, "PlusButton") : null;
        if (dp != null) dp.gameObject.SetActive(plusOn);
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
