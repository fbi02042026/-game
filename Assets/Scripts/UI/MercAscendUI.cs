using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 佣兵进阶（局外升星）弹窗（2026-10-22 施工单《佣兵进阶界面》）。
///
/// 预制体 Assets/Resources/Prefabs/Battle/yongbing升阶.prefab 主人已摆好（实际放在 Battle 目录，
/// 几何一个像素不动；缺节点 LogError 不兜底建（AGENTS.md §4）。节点口径（主人 dump 实测）：
///   Root/Panel(800×1050)
///     top/namebg/Name 佣兵名、top/Role 职业徽记位、top/Iconmask/Icon 大立绘
///     zuo / you 结构相同：AttrPanel 七行（AttrXxxRoot/AttrXxx）+ Rating 三星（满星1/2/3）+ Skill 行（只读，不归本类管）
///     碎片/{碎片数量、yongbingsuipian(主人碎片预制体嵌套实例)}
///     徽记/{碎片数量、Icon、iconbg}（2026-10-23 主人手摆，替代旧「金币行克隆」方案）
///     进阶成功/{成功(大图)、effect(粒子+音效)}（2026-10-24 主人手摆：升星成功时猛的弹出 + 特效齐播）
///     shengjieButton(升阶：Label=×N 消耗数字、icon=金币/钻石图) / tihaunButton(取消)；Root/Dim 点击关闭
///
/// 数据口径（2026-10-22 主人拍板）：
///   左侧 = 当前值（含当前 outStar 加成）；右侧 = 升到下一星预览 —— 攻/防 ×(1+8%)、
///   魔攻/魔防 ×(1+7%)（相对左侧显示值再乘一档）；生命/速度/暴击两侧同值（不吃加成）。
///   满星后右侧 = 当前值、升阶按钮置灰。
/// 升阶消耗（2026-10-23 主人拍板二改）：数字挪到升阶按钮 Label（×N，传奇=钻石），icon 随之换钻石图；
///   【2026-10-24 主人拍板三改】Label 能升阶=白、不能升阶=红（CanStarUp 唯一出口）、满星禁用压灰，另加阴影；
///   icon 按币种主动设图（传奇=钻石、普通/稀有=金币，都走路径加载）—— 预制体 icon 挂图已断链，不再依赖。
/// 徽记行 = {档位}徽记\n{需}/{有}（唯一出口 MercStarSystem）；【2026-10-24 主人拍板】徽记是升阶必备道具，
/// 未满星常显（普通/稀有升 1 星消耗 0 也显示 0/{有}），满星才隐藏。
/// 竖屏适配（2026-10-23）：Panel 800 宽 &gt; 720 逻辑宽 → 整体等比缩放 k=720/800（只动 Panel localScale，
///   与开窗缩放动画复合，绝不 stretch、不动子节点）。
/// 打开动画：Panel 缩放 0.2 → 1.0（主人原话「缩放效果速度慢点 0.2的倍数吧」）。
/// </summary>
public class MercAscendUI : MonoBehaviour
{
    const string PrefabPath = "Prefabs/Battle/yongbing升阶";   // 2026-10-23 主人实际摆在 Prefabs/Battle/，原写 Town 找不到报「预制体缺失」

    /// <summary>935：2026-10-22 获得佣兵弹窗（925）可叠开本进阶弹窗，需压在其上；仍压过雇佣队列界面（TownPopup=900）。</summary>
    const int SortOrder = 935;

    /// <summary>打开缩放动画：起点 0.2（主人拍板）、时长 0.28 秒（主人要求 0.25~0.3 慢速）、ease-out。</summary>
    const float OpenScaleFrom = 0.2f;
    const float OpenDuration = 0.28f;

    /// <summary>升阶成功弹出动画（2026-10-24 主人拍板「成功节点从无到有、从小到大猛的弹出」）：
    /// 0 → PopOvershoot（先快，「猛」）→ 1.0（收住），两段式 ease-out，只动「进阶成功」节点自身 localScale。</summary>
    const float PopOvershoot = 1.3f;
    const float PopUpDur = 0.16f;
    const float PopSettleDur = 0.24f;

    /// <summary>竖屏适配（2026-10-23）：Panel 宽超 720 逻辑宽时整体等比缩放系数，Open 时按预制体 sizeDelta 现算。</summary>
    float _fitK = 1f;

    /// <summary>升阶按钮消耗数字颜色（2026-10-24 主人拍板二改：能升阶=白、不能升阶=红；满星禁用压灰）。
    /// 「能不能升阶」唯一出口 = MercStarSystem.CanStarUp（碎片/徽记/资金一体预检），不另写第二份判断。</summary>
    static readonly Color CostOkColor = Color.white;
    static readonly Color CostShortColor = new Color(1f, 0.4f, 0.4f, 1f);
    static readonly Color CostDimColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    /// <summary>满星时升阶按钮底图灰显色（与卡内升阶钮 MercDrawPoolUI.AscendDimColor 同灰值，2026-10-24 主人拍板）。</summary>
    static readonly Color StarUpDimColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    /// <summary>钻石图标（传奇佣兵升阶扣钻石时替换金币图标；Resources 路径已核实存在）。</summary>
    const string DiamondIconPath = "UI/Art/Icons/Common/icon_diamond_blue";

    static MercAscendUI _instance;

    GameObject _root;          // Root（Panel/Dim 的父级，开窗开关作用于它）
    RectTransform _panel;      // Panel（缩放动画只动它的 localScale）

    Text _nameText;
    Image _roleImage;
    Image _iconImage;

    Text[] _leftVals;          // 左（当前）七行数值，下标 = RowValueNames 顺序
    Text[] _rightVals;         // 右（下一星预览）七行数值
    GameObject[] _leftRowRoots;   // 左七行 Root 节点（物攻/魔攻行二选一隐藏用，2026-10-22 主人拍板）
    GameObject[] _rightRowRoots;  // 右七行 Root 节点（同上）
    GameObject[] _leftStars;   // 满星1→满星2→满星3 顺序
    GameObject[] _rightStars;

    Text _fragCountText;       // 碎片/碎片数量（两行：佣兵名+碎片 / 需{}/{有}）
    MercFragmentCardUI _fragCard;

    // 徽记行（2026-10-23 主人已手摆预制体 Panel/徽记/{碎片数量,Icon,iconbg}，代码只绑节点填数据，几何不动）
    GameObject _badgeRow;
    Text _badgeCountText;      // 两行：{档位}徽记 / 需{}/{有}
    Image _badgeIconImage;

    // 升阶按钮消耗（2026-10-23 主人拍板：数字挪到 shengjieButton 上 Label=×N，icon=金币/钻石图）
    Text _costLabel;
    Image _costIcon;

    Button _starUpBtn;
    Image _starUpImg;          // 升阶按钮底图（满星灰显只调它的颜色，2026-10-24 主人拍板）
    Color _starUpImgBase;      // 预制体 authored 底色（非满星还原用，不写死白色）
    Button _cancelBtn;
    Button _dimBtn;

    // 顶条资源条（2026-10-24 主人拍板二次）：不再克隆，直接显示全局唯一 ResourceStrip
    // （提取自主界面顶条的金币/钻石原生列），开窗 Show、关窗 Release 归还；数字由其内部轮询统一刷。

    string _hireId;            // 当前展示佣兵（hireId）
    Coroutine _scaleCo;

    // 升阶成功动效（2026-10-24 主人预制体 Panel/进阶成功 下手摆：成功大图 + effect 特效；代码只绑节点播动画，几何不动）
    RectTransform _successRt;      // 进阶成功（弹出动画只动它的 localScale）
    ParticleSystem[] _successFx;   // effect 子树下粒子（Build 时统一关 playOnAwake，播放只走 Clear+Play）
    Coroutine _popCo;              // 成功弹出协程（Close/Fill 停）

    /// <summary>七行行名（左/右两侧同名）：顺序按主人 dump：Hp/Spd/MagicDef/Crit/Def/Atk/MagicAtk。</summary>
    static readonly string[] RowRootNames =
        { "AttrHpRoot", "AttrSpdRoot", "AttrMagicDefRoot", "AttrCritRoot", "AttrDefRoot", "AttrAtkRoot", "AttrMagicAtkRoot" };
    static readonly string[] RowValueNames =
        { "AttrHp", "AttrSpd", "AttrMagicDef", "AttrCrit", "AttrDef", "AttrAtk", "AttrMagicAtk" };

    // ============================================================
    // 对外入口（唯一）
    // ============================================================

    /// <summary>打开进阶弹窗。hireIdOrUid：佣兵 hireId 或 AssetId（MercStarSystem 同口径）。</summary>
    public static void Show(string hireIdOrUid)
    {
        if (string.IsNullOrEmpty(hireIdOrUid))
        {
            Debug.LogError("[MercAscend] Show 传入佣兵 id 为空，不开窗");
            return;
        }
        var ui = Ensure();
        if (ui != null) ui.Open(hireIdOrUid);
    }

    static MercAscendUI Ensure()
    {
        if (_instance != null) return _instance;

        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[MercAscend] 预制体缺失（Resources/" + PrefabPath + "），无法开窗");
            return null;
        }

        var go = Object.Instantiate(prefab);
        go.name = "MercAscendUI";
        Object.DontDestroyOnLoad(go);
        _instance = go.AddComponent<MercAscendUI>();
        _instance.Build();   // 末尾 SetActive(false)（AGENTS.md §4 运行时建树口径）
        return _instance;
    }

    void Awake()
    {
        if (_instance == null) _instance = this;
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // ============================================================
    // 建树绑定（缺节点 LogError，不兜底建）
    // ============================================================

    void Build()
    {
        _root = transform.Find("Root")?.gameObject;
        if (_root == null) Debug.LogError("[MercAscend] 缺节点 Root");
        _panel = (_root != null ? _root.transform.Find("Panel") : null) as RectTransform;
        if (_panel == null) Debug.LogError("[MercAscend] 缺节点 Root/Panel");

        Transform p = _panel;

        // ---- top：佣兵名 / 职业徽记 / 大立绘 ----
        _nameText = FindDeep(p, "Name")?.GetComponent<Text>();
        if (_nameText == null) Debug.LogError("[MercAscend] 缺节点 top/namebg/Name（佣兵名 Text）");
        _roleImage = FindDeep(p, "Role")?.GetComponent<Image>();
        if (_roleImage == null) Debug.LogError("[MercAscend] 缺节点 top/Role（职业徽记 Image）");
        _iconImage = FindDeep(p, "Icon")?.GetComponent<Image>();
        if (_iconImage == null) Debug.LogError("[MercAscend] 缺节点 top/Iconmask/Icon（大立绘 Image）");

        // ---- 左（当前）/ 右（下一星预览）----
        var zuo = p.Find("zuo");
        var you = p.Find("you");
        if (zuo == null) Debug.LogError("[MercAscend] 缺节点 zuo（当前属性栏）");
        if (you == null) Debug.LogError("[MercAscend] 缺节点 you（升星预览栏）");
        BindSide(zuo, out _leftVals, out _leftRowRoots, out _leftStars, "zuo");
        BindSide(you, out _rightVals, out _rightRowRoots, out _rightStars, "you");

        // ---- 碎片区（2026-10-23 改局部查找：徽记行也有同名「碎片数量」，全局深搜会串绑到徽记行）----
        var suipianNode = FindDeep(p, "碎片");
        _fragCountText = suipianNode != null ? FindDeep(suipianNode, "碎片数量")?.GetComponent<Text>() : null;
        if (_fragCountText == null) Debug.LogError("[MercAscend] 缺节点 碎片/碎片数量（Text）");
        var fragGo = FindDeep(p, "yongbingsuipian")?.gameObject;
        if (fragGo == null) Debug.LogError("[MercAscend] 缺节点 碎片/yongbingsuipian（主人碎片预制体实例）");
        else _fragCard = MercFragmentCardUI.Bind(fragGo);

        // ---- 徽记行（2026-10-23 主人已手摆 Panel/徽记/{碎片数量,Icon,iconbg}，直接绑，不再克隆建行）----
        _badgeRow = FindDeep(p, "徽记")?.gameObject;
        if (_badgeRow == null) Debug.LogError("[MercAscend] 缺节点 Panel/徽记（主人手摆的徽记行）");
        else
        {
            _badgeCountText = FindDeep(_badgeRow.transform, "碎片数量")?.GetComponent<Text>();
            _badgeIconImage = FindDeep(_badgeRow.transform, "Icon")?.GetComponent<Image>();
            if (_badgeCountText == null) Debug.LogError("[MercAscend] 缺节点 徽记/碎片数量（Text）");
            if (_badgeIconImage == null) Debug.LogError("[MercAscend] 缺节点 徽记/Icon（Image）");
        }

        // ---- 升阶成功动效节点（2026-10-24 主人预制体 Panel/进阶成功 下手摆，缺节点 LogError 不兜底建）----
        _successRt = FindDeep(p, "进阶成功") as RectTransform;
        if (_successRt == null) Debug.LogError("[MercAscend] 缺节点 Panel/进阶成功（升阶成功动效节点）");
        var fxNode = _successRt != null ? FindDeep(_successRt, "effect") : null;
        if (_successRt != null && fxNode == null)
            Debug.LogError("[MercAscend] 缺节点 进阶成功/effect（升阶成功特效节点）");
        _successFx = fxNode != null ? fxNode.GetComponentsInChildren<ParticleSystem>(true) : null;
        if (_successFx != null)
        {
            // 关 Play On Awake + 清残留，播放只走 PlaySuccessFx 一个入口（MercGainCinematicUI.cs:89-96 同口径）；
            // AudioSource 保留 authored playOnAwake=1 —— 节点激活即播音，与粒子同帧，不另起播放入口。
            for (int i = 0; i < _successFx.Length; i++)
            {
                var ps = _successFx[i];
                if (ps == null) continue;
                var pm = ps.main;
                pm.playOnAwake = false;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        // ---- 按钮 ----
        _starUpBtn = BindButton(p, "shengjieButton", OnStarUpClick, "升阶");
        if (_starUpBtn != null)
        {
            _starUpImg = _starUpBtn.GetComponent<Image>();
            if (_starUpImg != null) _starUpImgBase = _starUpImg.color;   // 记 authored 底色，灰显还原用
            _costLabel = _starUpBtn.transform.Find("Label")?.GetComponent<Text>();
            if (_costLabel == null) Debug.LogError("[MercAscend] 缺节点 shengjieButton/Label（消耗数字 Text）");
            else EnsureCostLabelShadow(_costLabel);   // 2026-10-24 主人拍板：消耗数字加阴影（字号/排版一个值不动）
            _costIcon = _starUpBtn.transform.Find("icon")?.GetComponent<Image>();
            if (_costIcon == null) Debug.LogError("[MercAscend] 缺节点 shengjieButton/icon（金币/钻石图标 Image）");
        }
        _cancelBtn = BindButton(p, "tihaunButton", Close, "取消");
        _dimBtn = _root != null ? BindButton(_root.transform, "Dim", Close, "Dim") : null;

        if (_root != null) _root.SetActive(false);   // AGENTS.md §4：建树末尾必须收起
    }

    /// <summary>绑一侧（zuo/you）：AttrPanel 七行数值 + 七行 Root 节点 + Rating 三星。sideName 仅用于报错定位。</summary>
    void BindSide(Transform side, out Text[] vals, out GameObject[] rowRoots, out GameObject[] stars, string sideName)
    {
        vals = new Text[RowRootNames.Length];
        rowRoots = new GameObject[RowRootNames.Length];
        stars = new GameObject[3];
        if (side == null) return;

        var attrPanel = side.Find("AttrPanel");
        if (attrPanel == null) Debug.LogError("[MercAscend] 缺节点 " + sideName + "/AttrPanel");
        for (int i = 0; i < RowRootNames.Length; i++)
        {
            var row = attrPanel != null ? attrPanel.Find(RowRootNames[i]) : null;
            if (row == null)
            {
                Debug.LogError("[MercAscend] 缺节点 " + sideName + "/AttrPanel/" + RowRootNames[i]);
                continue;
            }
            rowRoots[i] = row.gameObject;   // 物攻/魔攻行二选一隐藏要动整个 Root（2026-10-22 主人拍板）
            vals[i] = FindDeep(row, RowValueNames[i])?.GetComponent<Text>();
            if (vals[i] == null)
                Debug.LogError("[MercAscend] 缺数值 Text：" + sideName + "/AttrPanel/" + RowRootNames[i] + "/" + RowValueNames[i]);
        }

        var rating = FindDeep(side, "Rating");
        if (rating == null)
        {
            Debug.LogError("[MercAscend] 缺节点 " + sideName + "/Rating");
            return;
        }
        for (int i = 0; i < 3; i++)
        {
            stars[i] = rating.Find("满星" + (i + 1))?.gameObject;
            if (stars[i] == null) Debug.LogError("[MercAscend] 缺节点 " + sideName + "/Rating/满星" + (i + 1));
        }
    }

    /// <summary>绑按钮：无 Button 运行时补（预制体只有 Image 也接得上，MercDrawPoolUI 同口径）。</summary>
    Button BindButton(Transform from, string name, UnityEngine.Events.UnityAction onClick, string tag)
    {
        var t = FindDeep(from, name);
        if (t == null)
        {
            Debug.LogError("[MercAscend] 缺节点 " + name + "（" + tag + "）");
            return null;
        }
        var img = t.GetComponent<Image>();
        if (img != null) img.raycastTarget = true;
        var btn = t.GetComponent<Button>();
        if (btn == null) btn = t.gameObject.AddComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(onClick);
        return btn;
    }

    // ============================================================
    // 开窗 / 关窗
    // ============================================================

    void Open(string hireIdOrUid)
    {
        // DDOL 弹窗每次 Show 重套画布（MercDrawPoolUI.Open 同口径，防切场景后弹窗变大）
        var canvas = GetComponent<Canvas>();
        if (canvas != null)
            UICanvasSetup.RefreshPopup(canvas, SortOrder);

        // fail closed：数据不合法不开窗（窗口保持上一次状态）
        if (!Fill(hireIdOrUid)) return;

        EnsureResBar();   // 顶条金币+钻石克隆条（2026-10-24 主人拍板）；数字在 Fill 末尾统一刷

        if (_root != null)
        {
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
        }
        ApplyFit();   // 竖屏适配先算好缩放系数，动画目标值跟着它走
        PlayOpenScale();
    }

    /// <summary>
    /// 竖屏适配（2026-10-23）：Panel 预制体宽 800 &gt; 720 逻辑宽 → 整体等比缩放 k=720/800。
    /// 只动 Panel 的 localScale（先例 DailyLoginUI.ApplyFit 整体缩放口径），绝不 stretch、不动子节点；
    /// 每次 Open 现算幂等，不累积。逻辑高更瘦屏变大不受影响（Panel 高 1050×0.9=945 仍居中放得下）。
    /// </summary>
    void ApplyFit()
    {
        if (_panel == null) return;
        float w = Mathf.Abs(_panel.sizeDelta.x);
        _fitK = w > GameConfig.DESIGN_WIDTH ? GameConfig.DESIGN_WIDTH / w : 1f;
    }

    void Close()
    {
        if (_scaleCo != null) { StopCoroutine(_scaleCo); _scaleCo = null; }
        if (_popCo != null) { StopCoroutine(_popCo); _popCo = null; }   // 成功弹出进行中关窗：停协程防关窗后还在跑
        if (_panel != null) _panel.localScale = Vector3.one * _fitK;   // 动画只动 Panel 缩放，关窗还原到适配系数
        if (_successRt != null) _successRt.localScale = Vector3.one;   // 防关窗瞬间停在过冲值（2026-10-24）
        if (_root != null) _root.SetActive(false);
        ResourceStrip.Release();   // 2026-10-24 二次：资源条归还主界面顶条（与 EnsureResBar 的 Show 成对）
        // 升星可能改了星级/材料/资金：酒馆队列界面若开着，重刷卡内「升阶」钮与相关显示（2026-10-23）
        MercDrawPoolUI.RefreshForAscendClosed();
    }

    // ============================================================
    // 顶条资源条（2026-10-24 主人拍板二次）：全局唯一 ResourceStrip，开窗 Show / 关窗 Release
    // ============================================================

    /// <summary>
    /// 开窗显示资源条（金币+钻石两列，加号不接点击 T14 口径由 ResourceStrip 统一处理）。
    /// 主界面未注册（GuildHallUI 未初始化）时 Show 返回 false，本窗不显示资源条（不兜底）。
    /// </summary>
    void EnsureResBar()
    {
        if (_root == null) return;
        ResourceStrip.Show(_root.transform, ResourceStrip.Mode.GoldDia);
    }

    // ============================================================
    // 数据填充
    // ============================================================

    bool Fill(string hireIdOrUid)
    {
        if (!MercRosterDefs.TryGetByHireId(hireIdOrUid, out var def)
            && !MercRosterDefs.TryGetByAssetId(hireIdOrUid, out def))
        {
            Debug.LogError("[MercAscend] 花名册查不到佣兵：" + hireIdOrUid + "，不开窗");
            return false;
        }
        _hireId = def.HireId;

        var data = SaveSystem.Instance?.Data;
        if (data == null || !data.IsMercUnlocked(_hireId))
        {
            UIManager.Instance?.ShowToast("尚未获得该佣兵", true);
            return false;
        }

        int cur = MercStarSystem.OutStarOf(_hireId);
        if (cur < 0) cur = 0;
        bool maxed = cur >= MercStarDefs.MaxOutStar;

        // ---- 头部 ----
        if (_nameText != null) _nameText.text = def.Name;
        // 【2026-10-24 主人拍板】Role 位 = 佣兵类型（物攻/法术/防御/恢复 四分类徽标），
        // 不是养成徽记那套图（MercGrowSprites.LoadJobBadge 是升星消耗用图，别混进来）。
        // 唯一出口 = JobIconResolver.CombatBadge；取不到隐藏节点，不换图兜底（JobIconResolver R2 口径）。
        if (_roleImage != null)
        {
            var typeIcon = JobIconResolver.CombatBadge(def.JobName);
            if (typeIcon != null)
            {
                _roleImage.sprite = typeIcon;
                _roleImage.enabled = true;   // DDOL 复用：防上一次取不到图留下的隐藏残留
            }
            else _roleImage.enabled = false;
        }
        if (_iconImage != null)
        {
            _iconImage.sprite = MercPortraitSprites.GetStand(_hireId);
            _iconImage.preserveAspect = true;   // 大立绘保比例（施工单口径）
        }

        // ---- 属性数值：基底走 ApplyCombatStats（与「获得佣兵」弹窗同源），等级取存档 ----
        MercRosterDefs.ApplyCombatStats(def.AssetId, ResolveSavedLevel(_hireId, def),
            out float hp, out float atk, out float defV, out float atkSpd, out float move, out float range);
        bool magic = Mercenary.UsesMagicAttack(def.AssetId);

        // ---- 攻/魔攻行二选一（2026-10-22 主人拍板「物攻佣兵只显示物攻 魔攻只显示魔攻」）----
        // 判定与战斗/获得弹窗同源（Mercenary.UsesMagicAttack，不另写前缀表）；
        // 物攻佣兵隐藏魔攻行、魔攻佣兵隐藏物攻行；魔防/防御等其余行保持显示不变。
        // 弹窗是 DDOL 复用的，切换佣兵时保留行要显式 SetActive(true) 防上一次残留。
        SetAtkRowVisible(_leftRowRoots, magic);
        SetAtkRowVisible(_rightRowRoots, magic);

        float atkCur = atk * (1f + MercStarDefs.AtkPctPerStar * cur);
        float defCur = defV * (1f + MercStarDefs.AtkPctPerStar * cur);
        float matkCur = (magic ? atk : 0f) * (1f + MercStarDefs.MagicPctPerStar * cur);
        float mdefCur = defV * (1f + MercStarDefs.MagicPctPerStar * cur);
        // 右侧 = 相对左侧显示值再乘一档；满星后右侧 = 当前值（2026-10-22 主人拍板）
        float atkNext = maxed ? atkCur : atkCur * (1f + MercStarDefs.AtkPctPerStar);
        float defNext = maxed ? defCur : defCur * (1f + MercStarDefs.AtkPctPerStar);
        float matkNext = maxed ? matkCur : matkCur * (1f + MercStarDefs.MagicPctPerStar);
        float mdefNext = maxed ? mdefCur : mdefCur * (1f + MercStarDefs.MagicPctPerStar);

        // 暴击 = 本项目基础值；0.05 这类小数按百分比显示（MercObtainPopupUI 同口径）
        float crit = GameConfig.BASE_CRIT_RATE;
        if (crit <= 1.5f) crit *= 100f;
        string critStr = crit.ToString("0.#") + "%";

        // 行序 = RowValueNames：Hp / Spd / MagicDef / Crit / Def / Atk / MagicAtk
        // 生命/速度/暴击不吃加成，两侧同值（2026-10-22 主人确认「不加属性的可以一样 只是预览下一星属性」）
        string[] leftStrs =
        {
            Mathf.RoundToInt(hp).ToString(),
            // 【2026-10-24 主人拍板】速度显示 ×100 取整不用小数（0.85 → 85），与获得佣兵弹窗同口径
            Mathf.RoundToInt(move * 100f).ToString(),
            Mathf.RoundToInt(mdefCur).ToString(),
            critStr,
            Mathf.RoundToInt(defCur).ToString(),
            Mathf.RoundToInt(atkCur).ToString(),
            Mathf.RoundToInt(matkCur).ToString(),
        };
        string[] rightStrs =
        {
            leftStrs[0],
            leftStrs[1],
            Mathf.RoundToInt(mdefNext).ToString(),
            critStr,
            Mathf.RoundToInt(defNext).ToString(),
            Mathf.RoundToInt(atkNext).ToString(),
            Mathf.RoundToInt(matkNext).ToString(),
        };
        for (int i = 0; i < leftStrs.Length; i++)
        {
            if (_leftVals != null && i < _leftVals.Length && _leftVals[i] != null) _leftVals[i].text = leftStrs[i];
            if (_rightVals != null && i < _rightVals.Length && _rightVals[i] != null) _rightVals[i].text = rightStrs[i];
        }

        // ---- 星星：亮的颗数 = 星数，点亮顺序 满星1→满星2→满星3，多余隐藏，0 星全隐藏 ----
        SetStars(_leftStars, cur);
        SetStars(_rightStars, maxed ? cur : cur + 1);

        // ---- 碎片区：两行「{佣兵名}碎片\n{当次需要}/{拥有}」（需要在前拥有在后，主人原话 44/55）----
        int have = MercStarSystem.GetBagFragCount(_hireId);
        int need = MercStarSystem.GetNextStarCost(_hireId);   // 满星返回 0
        if (_fragCountText != null)
            _fragCountText.text = maxed
                ? def.Name + "碎片\n" + have
                : def.Name + "碎片\n" + need + "/" + have;
        if (_fragCard != null) _fragCard.Apply(def.Rarity, _hireId);

        // ---- 升阶消耗（2026-10-23 主人拍板二改：数字挪到升阶按钮 Label=×N，icon 随之换金币/钻石）----
        // 普通/稀有=金币（预制体原 icon 不动）；传奇=钻石（icon 换钻石图）。满星显示 ×0。
        // Label 颜色（2026-10-24 主人拍板）：钱够暖金、不够红色、满星禁用压灰；资金判断走 FundEnough 唯一出口。
        bool legend = def.Rarity == MercRosterDefs.MercRarity.Legendary;
        long cost = maxed
            ? 0
            : (legend
                ? MercStarDefs.DiamondCost(def.Rarity, cur + 1)
                : MercStarDefs.GoldCost(def.Rarity, cur + 1));
        if (_costLabel != null)
        {
            _costLabel.text = "×" + cost;
            // 【2026-10-24 主人拍板二改】能升阶=白、不能升阶=红（碎片/徽记/资金一体预检走 CanStarUp 唯一出口）；
            // 满星禁用压灰不变。只换颜色，字号一个值不动（铁律 32）。
            _costLabel.color = maxed ? CostDimColor
                : MercStarSystem.CanStarUp(_hireId, out _) ? CostOkColor : CostShortColor;
        }
        if (_costIcon != null)
        {
            // 【2026-10-24】icon 按消耗币种主动设图：传奇=钻石、普通/稀有=金币。
            // 根因：预制体 authored 的 icon 挂图 guid 已断链（团结重导 56 字符），金币档不能再依赖预制体残留引用
            // —— 这正是「传奇有图标、普通佣兵进阶没图标」的原因；金币/钻石两条路统一走路径加载（铁律 30 口径）。
            if (legend && !maxed)
            {
                var dia = Resources.Load<Sprite>(DiamondIconPath);
                if (dia != null) _costIcon.sprite = dia;
                else Debug.LogError("[MercAscend] 钻石图标缺失：Resources/" + DiamondIconPath + "（不兜底）");
            }
            else
            {
                var gold = CommonIconSprites.Load("icon_gold");
                if (gold != null) _costIcon.sprite = gold;
                else Debug.LogError("[MercAscend] 金币图标缺失：CommonIconSprites icon_gold（不兜底）");
            }
        }

        // ---- 徽记行（2026-10-23 主人拍板）：两行「{低级/中级/高级}徽记\n{需}/{有}」。
        // 【2026-10-24 主人拍板】徽记 = 升阶必备道具，未满星就常显（废除旧「0 消耗整行隐藏」——
        // 普通/稀有升 1 星徽记消耗为 0，整行藏掉导致普通佣兵进阶看不到徽记）；
        // need=0 照常显示「低级徽记 0/{有}」。满星仍隐藏（升阶已不存在）。
        // 图标 = MercGrowSprites 按职业+档位取图，缺图报错不兜底；档位名唯一出口 = MercStarSystem.BadgeTierDisplayName。
        if (_badgeRow != null)
        {
            int badgeNeed = maxed ? 0 : MercStarDefs.BadgeCost(def.Rarity, cur + 1);
            if (maxed)
            {
                _badgeRow.SetActive(false);
            }
            else
            {
                _badgeRow.SetActive(true);
                int badgeHave = MercStarSystem.GetBadgeCount(_hireId);
                string badgeName = MercStarSystem.BadgeTierDisplayName(MercStarDefs.BadgeTierOf(def.Rarity)) + "徽记";
                if (_badgeCountText != null)
                    _badgeCountText.text = badgeName + "\n" + badgeNeed + "/" + badgeHave;
                if (_badgeIconImage != null)
                {
                    var badgeSprite = MercGrowSprites.LoadJobBadge(def.JobName, def.Rarity);
                    if (badgeSprite != null) _badgeIconImage.sprite = badgeSprite;
                    else Debug.LogError("[MercAscend] 徽记图缺失：" + def.JobName + "/" + MercStarDefs.BadgeTierOf(def.Rarity)
                        + "（MercGrowSprites.LoadJobBadge 返回空，不兜底）");
                }
            }
        }

        // ---- 升阶按钮：满星灰显 + 点击提示已满星（2026-10-24 主人拍板）----
        // 灰显只调底图颜色（与卡内升阶钮 MercDrawPoolUI.AscendDimColor 同灰值 0.5）；
        // interactable 保持 true —— 设 false 会吞掉点击，OnStarUpClick 里的满星 Toast 就弹不出来了。
        if (_starUpBtn != null && _starUpImg != null)
            _starUpImg.color = maxed ? StarUpDimColor : _starUpImgBase;

        // ---- 升阶成功节点收起态（2026-10-24）：每次填充都重置，防 DDOL 复用残留；播放只由 PlaySuccessFx 触发 ----
        if (_popCo != null) { StopCoroutine(_popCo); _popCo = null; }
        if (_successRt != null)
        {
            _successRt.gameObject.SetActive(false);
            _successRt.localScale = Vector3.one;   // 回 authored 值（弹出动画只是临时借它）
        }

        // ---- 顶条资源条数字（2026-10-24 二次）：全局 ResourceStrip 内部轮询统一刷，这里不再自管 ----
        return true;
    }

    void SetStars(GameObject[] stars, int lit)
    {
        if (stars == null) return;
        for (int i = 0; i < stars.Length; i++)
            if (stars[i] != null) stars[i].SetActive(i < lit);
    }

    /// <summary>
    /// 攻/魔攻行二选一显示（2026-10-22 主人拍板）：RowRootNames 顺序下 5=AttrAtkRoot、6=AttrMagicAtkRoot。
    /// 物攻佣兵（magic=false）隐藏魔攻行；魔攻佣兵隐藏物攻行；保留的一侧显式 SetActive(true) 防复用残留。
    /// 行 Root 缺节点（绑定时报过错）不兜底、直接跳过 —— fail closed。
    /// </summary>
    void SetAtkRowVisible(GameObject[] rowRoots, bool magic)
    {
        if (rowRoots == null) return;
        const int idxAtk = 5, idxMagicAtk = 6;
        if (rowRoots.Length <= idxMagicAtk) return;
        if (rowRoots[idxAtk] != null) rowRoots[idxAtk].SetActive(magic);
        if (rowRoots[idxMagicAtk] != null) rowRoots[idxMagicAtk].SetActive(!magic);
    }

    /// <summary>存档里该佣兵等级（无实例按 1 级显示；口径同 MercDrawPoolUI.ShowMercDetail）。</summary>
    static int ResolveSavedLevel(string hireId, MercRosterDefs.Def def)
    {
        var data = SaveSystem.Instance?.Data;
        if (data?.permanentMercs != null)
            for (int i = 0; i < data.permanentMercs.Count; i++)
            {
                var m = data.permanentMercs[i];
                if (m == null) continue;
                if (m.mercId == hireId || m.mercId == def.AssetId) return Mathf.Max(1, m.level);
            }
        return 1;
    }

    // ============================================================
    // 升阶流程（fail closed：每一步校验不过就 toast 具体原因）
    // ============================================================

    void OnStarUpClick()
    {
        if (string.IsNullOrEmpty(_hireId)) return;
        if (!MercRosterDefs.TryGetByHireId(_hireId, out var def)) return;

        int cur = MercStarSystem.OutStarOf(_hireId);
        if (cur < 0) cur = 0;
        if (cur >= MercStarDefs.MaxOutStar) { Toast(def.Name + " 已满星"); return; }

        // 碎片校验（MercStarSystem 权威；msg 说明缺什么）
        if (!MercStarSystem.CanStarUp(_hireId, out string msg)) { Toast(msg); return; }

        // 【2026-10-24 主人拍板】所有花费钻石的地方都要二次确认：
        // 本次升星要花钻石（传奇）才弹 DiamondSpendGate 确认门；只花金币/徽记/碎片的升星保持原样不打扰。
        // 资金数字直接读 MercStarDefs（与 TryStarUp 同一张表，规则不抄第二遍）。
        int target = cur + 1;
        long diaCost = MercStarDefs.DiamondCost(def.Rarity, target);
        if (diaCost > 0)
        {
            long goldCost = MercStarDefs.GoldCost(def.Rarity, target);
            string body = $"{def.Name} 升至 {target} 星";
            if (goldCost > 0) body += $"\n消耗金币 ×{goldCost}";
            body += "\n另消耗升星徽记与本命碎片（数量见面板预览）";
            DiamondSpendGate.Request("确认升星", body, diaCost, DoStarUp, "确定升星");
            return;
        }

        DoStarUp();
    }

    void DoStarUp()
    {
        if (string.IsNullOrEmpty(_hireId)) return;
        // 执行升星（【2026-10-23 主人拍板】一次事务做完：徽记 → 碎片 → 金币/钻石 → outStar+1 → 存档；
        // 任一步扣不动就整笔回滚，星级不变。UI 不再在升星之后单独扣钱 —— 那样会留下「星升了钱没扣」。）
        if (!MercStarSystem.TryStarUp(_hireId, out string upMsg)) { Toast(upMsg); return; }

        Toast(upMsg);       // 「XX 升星成功（N 星）」
        Fill(_hireId);      // 左=新当前，右=再下一星预览；满星自动置灰（末尾会把成功节点收起）
        PlaySuccessFx();    // 2026-10-24 主人拍板：成功节点猛的弹出 + effect 特效齐播（必须在 Fill 收起之后）
    }

    void Toast(string s) => UIManager.Instance?.ShowToast(s, true);

    /// <summary>
    /// 消耗数字加阴影（2026-10-24 主人拍板）：只补 Shadow 组件，字号/排版/rect 一个值不动（铁律 32）。
    /// 预制体里已有 Shadow 的只有 tihaunButton/Label；shengjieButton/Label 没有 → 缺才补、有就不碰 authored 值。
    /// </summary>
    static void EnsureCostLabelShadow(Text label)
    {
        var sh = label.GetComponent<Shadow>();
        if (sh != null) return;
        sh = label.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.55f);
        sh.effectDistance = new Vector2(1.5f, -1.5f);
        sh.useGraphicAlpha = true;
    }

    // 【2026-10-23 主人拍板】资金预检本体已挪到 MercStarSystem.FundEnough（与扣费同事务的唯一出口），
    // 本类不再有自己的实现；Fill 的消耗数字变色（2026-10-24 二改）转调 MercStarSystem.CanStarUp
    //（碎片/徽记/资金一体预检，内部同走 FundEnough），「能不能升阶」判断不抄第二遍。

    // ============================================================
    // 打开缩放动画（Panel 0.2 → 1.0，ease-out；只动 Panel 的 localScale）
    // ============================================================

    void PlayOpenScale()
    {
        if (_scaleCo != null) { StopCoroutine(_scaleCo); _scaleCo = null; }
        if (_panel == null) return;
        _scaleCo = StartCoroutine(CoOpenScale());
    }

    /// <summary>unscaled 时间（防 timescale=0 卡死，项目弹窗动画既有口径）；ease-out 二次曲线。
    /// 目标缩放 = 适配系数 _fitK（Panel 800 宽整体缩到 720 逻辑宽内）。</summary>
    System.Collections.IEnumerator CoOpenScale()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / OpenDuration;
            float k = Mathf.Clamp01(t);
            k = 1f - (1f - k) * (1f - k);   // ease-out quad
            _panel.localScale = Vector3.one * _fitK * (OpenScaleFrom + (1f - OpenScaleFrom) * k);
            yield return null;
        }
        _panel.localScale = Vector3.one * _fitK;
        _scaleCo = null;
    }

    // ============================================================
    // 升阶成功动效（2026-10-24 主人拍板：进阶成功节点从无到有猛的弹出，effect 特效一起播）
    // ============================================================

    /// <summary>播成功动效：节点显形 + 猛的弹出 + effect 粒子/音效齐播。
    /// 缺节点绑定期已 LogError，这里 fail closed 直接返回（不兜底建、不静默）。</summary>
    void PlaySuccessFx()
    {
        if (_successRt == null) return;
        _successRt.localScale = Vector3.zero;               // 先归零再显形：防激活后第一帧满尺寸闪一下
        _successRt.gameObject.SetActive(true);
        if (_successFx != null)
        {
            for (int i = 0; i < _successFx.Length; i++)
            {
                var ps = _successFx[i];
                if (ps == null || !ps.gameObject.activeInHierarchy) continue;
                ps.Clear(true);
                ps.Play(true);   // StageClearRewardDirector.cs:665-666 同口径；AudioSource 靠 authored playOnAwake 同帧播音
            }
        }
        if (_popCo != null) { StopCoroutine(_popCo); _popCo = null; }
        _popCo = StartCoroutine(CoSuccessPop());
    }

    /// <summary>unscaled 时间（弹窗动画既有口径，防 timescale=0 卡死）；两段式 ease-out：
    /// 0→过冲（先快，「猛」）→1.0 落定。只动 _successRt 自身 localScale，
    /// 不写子节点 sizeDelta/anchoredPosition（铁律 32）；与 Panel 开窗 _fitK 缩放复合互不干扰。</summary>
    System.Collections.IEnumerator CoSuccessPop()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / PopUpDur;
            float k = Mathf.Clamp01(t);
            k = 1f - (1f - k) * (1f - k);   // ease-out quad
            _successRt.localScale = Vector3.one * (PopOvershoot * k);
            yield return null;
        }
        t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / PopSettleDur;
            float k = Mathf.Clamp01(t);
            k = 1f - (1f - k) * (1f - k);   // ease-out quad
            _successRt.localScale = Vector3.one * (PopOvershoot + (1f - PopOvershoot) * k);
            yield return null;
        }
        _successRt.localScale = Vector3.one;
        _popCo = null;
    }

    // ============================================================
    // 工具
    // ============================================================

    /// <summary>按名字深度优先找子节点 —— 主人以后往中间插一层也不会断（MercFragmentCardUI 同口径）。</summary>
    static Transform FindDeep(Transform from, string childName)
    {
        if (from == null || string.IsNullOrEmpty(childName)) return null;
        for (int i = 0; i < from.childCount; i++)
        {
            var c = from.GetChild(i);
            if (c.name == childName) return c;
            var hit = FindDeep(c, childName);
            if (hit != null) return hit;
        }
        return null;
    }
}
