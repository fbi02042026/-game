using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「获得佣兵」弹窗（2026-10-14 主人拍板）：抽中佣兵入队时弹出。
/// <para>【2026-10-23 主人拍板】本文件只留：字段 / 单例与 Show 入口 / 生命周期 / 事件 wiring。
/// 节点绑定见 .Binding.cs、交互见 .Actions.cs、内容填充见 .Fill.cs。</para>
/// </summary>
public partial class MercObtainPopupUI : MonoBehaviour
{
    /// <summary>预制体路径（ Resources 下，不带后缀）。</summary>
    const string PrefabPath = "Prefabs/Battle/huodeyongbing";

    /// <summary>无操作自动关兜底（秒）——2026-10-14 主人拍板 4→8，让玩家看清。</summary>
    const float AutoCloseSeconds = 8f;

    /// <summary>Canvas sortingOrder：与预制体 Canvas 一致，压过抽奖面板 920。</summary>
    const int SortOrder = 925;

    /// <summary>预制体只摆了 3 个满星节点（满星1/满星2/满星3）。</summary>
    const int MaxStarNodes = 3;

    /// <summary>进阶流光材质球路径（2026-10-24 主人拍板；源 = Assets/Art/Effects/Material/btn18进阶.mat，
    /// 复制进 Resources/Materials 供运行时按路径加载——进阶预制体里 authored 的材质引用已断链，靠不住）。</summary>
    const string AscendGlowMatPath = "Materials/btn18进阶";

    /// <summary>可升星脉冲动效：缓慢呼吸，幅度 6%、周期 1.6 秒（unscaled 时间，弹窗动画既有口径）。</summary>
    const float AscendPulseAmplitude = 0.06f;
    const float AscendPulsePeriod = 1.6f;

    /// <summary>进阶指引节点（Panel/进阶/指引，2026-10-25 主人手工摆进预制体）：可升星时随进阶钮显示，
    /// 显示期间缓慢上下浮动（提示玩家点进阶去升星）。</summary>
    GameObject _guideNode;
    RectTransform _guideRt;
    /// <summary>指引节点 authored 位置（Bind 时记；浮动只在其基础上加 y 偏移，还原也回它，不碰布局）。</summary>
    Vector3 _guideBasePos;
    Coroutine _guideBobCo;
    /// <summary>指引浮动：幅度 9 逻辑像素、周期 2.2 秒（「缓慢」口径，unscaled 时间与脉冲同源）。</summary>
    const float GuideBobAmplitude = 9f;
    const float GuideBobPeriod = 2.2f;

    static MercObtainPopupUI _instance;

    GameObject _root;
    Button _dimBtn;
    Button _keepBtn;
    /// <summary>确认按钮 RectTransform（查看模式居中用，见 ApplyReplaceButton）。</summary>
    RectTransform _keepRt;
    /// <summary>确认按钮在预制体里的原始位置（并排态；只留确认钮时 x 归 0 居中）。</summary>
    Vector2 _keepBasePos;
    /// <summary>右下「替换」按钮（预制体自带节点 Panel/tihaunButton，主人摆的；仅佣兵池未入队详情入口显示）。</summary>
    Button _replaceBtn;
    /// <summary>「进阶」按钮（2026-10-22 主人拍板：预制体自带节点 Panel/进阶，主人摆的；
    /// 已拥有的佣兵才显示，点击开 MercAscendUI）。节点没 Button 组件时运行时补，几何不动。</summary>
    Button _ascendBtn;
    /// <summary>「进阶」节点底图（2026-10-24 主人拍板：可升星时挂发光材质球用）。</summary>
    Image _ascendImg;
    /// <summary>进阶节点 authored 缩放（Bind 时记；脉冲动效在它基础上呼吸，还原也回它）。</summary>
    Vector3 _ascendBaseScale = Vector3.one;
    /// <summary>进阶流光材质球（Resources/Materials/btn18进阶，懒加载缓存；与底牌 btn09/btn10 同族同加载方式）。</summary>
    Material _ascendGlowMat;
    Coroutine _ascendPulseCo;
    /// <summary>当前弹窗填充的佣兵 hireId（Fill 每次更新；「进阶」钮点击与显隐都认它）。</summary>
    string _curHireId;
    Image _icon;
    /// <summary>四分类类型图标位（Panel/Role，预制体已有节点）。</summary>
    Image _role;
    /// <summary>名字底牌（Panel/zuo/namebg，2026-10-18 起按稀有度换三档图）。</summary>
    Image _nameBg;
    /// <summary>立绘卡框（Panel/zuo 整卡底图，2026-10-18 起随稀有度换三档图，与名字底牌同一套）。</summary>
    Image _zuoImg;
    Text _nameText;
    GameObject[] _stars;
    /// <summary>满星布局的原始位置（Fill 首次记录，重开弹窗从这里重算，不累积偏移）。</summary>
    Vector2[] _starBasePos;

    // 七对属性（Root 整行 GameObject + 值 Text；Label 静态文本不碰）
    GameObject _attrHpRoot; Text _attrHp;
    GameObject _attrAtkRoot; Text _attrAtk;
    GameObject _attrMagicAtkRoot; Text _attrMagicAtk;
    GameObject _attrDefRoot; Text _attrDef;
    GameObject _attrMagicDefRoot; Text _attrMagicDef;
    GameObject _attrSpdRoot; Text _attrSpd;
    GameObject _attrCritRoot; Text _attrCrit;

    GameObject _skillRoot;
    Text _skillName;
    Text _skillInfo;
    Image _skillIcon;

    System.Action _onClosed;
    /// <summary>「替换」按钮回调（佣兵池 ShowOwnedDetail 传入；Close 时消费一次）。</summary>
    System.Action _onReplace;
    bool _closeReq;
    Coroutine _autoCo;

    // —— 招募购买模式（2026-10-19 主人拍板：酒馆点卡进详情，按钮换成 购买/取消）——
    bool _recruitMode;
    /// <summary>招募界面传入的成交回调：扣卷/扣金币 + 入队 + 刷新；返回 true 才关弹窗。</summary>
    System.Func<bool> _tryBuy;

    // —— 阵亡复活模式（2026-10-21 主人拍板：点阵亡佣兵头像弹信息框，确定钮换成「复活」+「取消」）——
    bool _fallenMode;
    /// <summary>复活回调：扣重生护符 + 原地复活；返回 true 才关弹窗（没护符 Toast 留在弹窗）。</summary>
    System.Func<bool> _tryRevive;
    /// <summary>复活按钮上方的提示行：护符 icon + 拥有数量（2026-10-21 主人需求；运行时补节点，不碰预制体）。</summary>
    GameObject _reviveCharmHint;
    Text _reviveCharmCount;
    Color _keepLabelColor0 = Color.white;
    Text _keepLabel;
    Text _replaceLabel;
    Text _titleText;
    string _keepLabelText0 = "确认";
    string _replaceLabelText0 = "替换";
    string _titleText0 = "获得佣兵";

    public static MercObtainPopupUI Instance => _instance;

    // ============================================================
    // 对外入口
    // ============================================================

    /// <summary>
    /// 弹「获得佣兵」并等到关闭（调用方 yield 它即可）。
    /// 取不到预制体 / 数据缺失时 LogError 后 onClosed 优雅收尾，绝不卡抽奖协程。
    /// </summary>
    public static IEnumerator CoShow(DraftCard card, System.Action onClosed = null)
    {
        var ui = Ensure();
        // DraftCard 是 struct，判空走 IsValid（default 卡 Id 为空即不合法）
        if (ui == null || !card.IsValid) { onClosed?.Invoke(); yield break; }
        ui.ExitRecruitMode();           // 防串味：任何非招募入口先退出招募模式（还原按钮/标题文案）
        ui._onClosed = onClosed;
        ui._closeReq = false;
        if (!ui.Fill(card)) { ui.Close(); yield break; }
        ui._onReplace = null;
        ui.ApplyReplaceButton(false);   // 抽中路径永远不带替换钮（替换只在佣兵池详情入口）
        // 【2026-10-18 主人拍板】获得类弹窗互斥：本弹窗要开了，先关掉开着的技能弹窗
        SkillObtainPopupUI.CloseIfOpen();
        ui.OpenRoot();
        ui.StopAutoClose();
        ui._autoCo = ui.StartCoroutine(ui.CoAutoClose());
        while (ui._root != null && ui._root.activeSelf) yield return null;
    }

    /// <summary>
    /// 【2026-10-15 主人拍板】点战斗头像栏的佣兵头像查看详情：弹窗不再抽中默认弹出
    /// （抽中当场只流入队横幅 MercJoinBannerUI + Toast，引导局在二抽后教这一步）。
    /// 数据从局内佣兵条目来（hireId / AssetId / 等级 / 星级），填充、开关、无操作自动关
    /// 与抽中路径完全同一套，不另写第二份。
    /// 【2026-10-18 主人拍板】showReplace=true 时右下显示「替换」钮，点击关弹窗并回调 onReplace
    /// （仅佣兵池点未入队的已获得佣兵时传；战斗头像入口/未获得详情走默认值 false，行为不变）。
    /// </summary>
    public static void ShowFromEntry(string hireId, string assetId, int level, int star,
        bool showReplace = false, System.Action onReplace = null)
    {
        var ui = Ensure();
        if (ui == null) return;
        ui.ExitRecruitMode();           // 防串味：任何非招募入口先退出招募模式（还原按钮/标题文案）
        ui.ExitFallenMode();            // 同上：先退阵亡复活模式（「复活/取消」双钮别串到普通详情）
        var card = new DraftCard
        {
            Kind = DraftCardKind.MercRecruit,
            Id = assetId,
            HireId = hireId,
            MercLevel = Mathf.Max(1, level),
            Star = Mathf.Clamp(star < 1 ? 1 : star, 1, 5)
        };
        if (!ui.Fill(card)) { ui.Close(); return; }
        ui._onReplace = onReplace;
        ui.ApplyReplaceButton(showReplace);
        // 【2026-10-18 主人拍板】获得类弹窗互斥：本弹窗要开了，先关掉开着的技能弹窗
        SkillObtainPopupUI.CloseIfOpen();
        ui.OpenRoot();
        ui.StopAutoClose();
        ui._autoCo = ui.StartCoroutine(ui.CoAutoClose());
    }

    /// <summary>
    /// 【2026-10-21 主人拍板】阵亡佣兵信息框：点变灰头像弹出，确认钮换成「复活」、右下「取消」，
    /// 标题「佣兵信息」（复用招募模式的双钮布局与文案切换，见 ApplyRecruitButtons）。
    /// <para><paramref name="tryRevive"/> 返回 true（有护符、扣款生效、复活成功）才关弹窗；
    /// 返回 false（没护符等）Toast 已提示，留在弹窗里继续看信息。</para>
    /// </summary>
    public static void ShowFallenFromEntry(string hireId, string assetId, int level, int star,
        System.Func<bool> tryRevive)
    {
        var ui = Ensure();
        if (ui == null) return;
        ui.ExitRecruitMode();           // 防串味：任何非招募入口先退出招募模式
        var card = new DraftCard
        {
            Kind = DraftCardKind.MercRecruit,
            Id = assetId,
            HireId = hireId,
            MercLevel = Mathf.Max(1, level),
            Star = Mathf.Clamp(star < 1 ? 1 : star, 1, 5)
        };
        if (!ui.Fill(card)) { ui.Close(); return; }
        ui._fallenMode = true;
        ui._tryRevive = tryRevive;
        // 复用招募模式的全套切换：双钮并排 + KeepButton→「复活」/ tihaunButton→「取消」+ 标题「佣兵信息」
        ui.ApplyRecruitButtons(true, "复活");
        ui.ApplyFallenCharm();   // 复活钮上方「护符 icon + ×N」提示行；没护符时复活钮字变红
        SkillObtainPopupUI.CloseIfOpen();
        ui.OpenRoot();
        ui.StopAutoClose();
        ui._autoCo = ui.StartCoroutine(ui.CoAutoClose());
    }

    static MercObtainPopupUI Ensure()
    {
        if (_instance != null) return _instance;

        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            // 不静默跳过：没预制体就没得谈，交给上层 onClosed 收尾
            Debug.LogError("[MercObtainPopup] 找不到预制体 Resources/Prefabs/Battle/huodeyongbing，" +
                           "请确认 Assets/Resources/Prefabs/Battle/huodeyongbing.prefab 存在");
            return null;
        }

        var go = Instantiate(prefab);
        go.name = "MercObtainPopup";
        // 防回归：根上若还挂着旧的 EquipReplaceConfirmUI（主人已删）， Destroy 掉，别让它抢按钮回调
        var stale = go.GetComponent<EquipReplaceConfirmUI>();
        if (stale != null) Destroy(stale);
        var ui = go.GetComponent<MercObtainPopupUI>();
        if (ui == null) ui = go.AddComponent<MercObtainPopupUI>();
        // 【2026-10-14 主人拍板「以后的界面都要走摄像机模式」】与 EquipReplaceConfirmPopup 同口径：
        // 实例化后套全项目统一规范（Screen Space - Camera + 925 层 + 根铺满），不裸 Overlay。
        var canvas = go.GetComponent<Canvas>();
        if (canvas == null) canvas = go.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, SortOrder);
        // 与 EquipReplaceConfirmPopup 同一套口径：不挂在 BattleUI 下、不随场景销毁
        UnityEngine.Object.DontDestroyOnLoad(go);
        return ui;
    }

    void Awake()
    {
        _instance = this;
        Bind();
        Wire();
        if (_root != null) _root.SetActive(false);
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // ============================================================
    // 节点绑定 / 事件 wiring（只认预制体里的既有节点，取不到置空，Fill 里判空报错）
    // ============================================================

    void Wire()
    {
        // 确认钮：普通模式=关弹窗；招募模式=购买（2026-10-19 主人拍板：按钮换成 购买/取消）
        if (_keepBtn != null)
        {
            _keepBtn.onClick.RemoveAllListeners();
            _keepBtn.onClick.AddListener(OnKeepClicked);
        }
        // 点暗带都关（预制体本身带了 Dim Button）
        if (_dimBtn != null)
        {
            _dimBtn.onClick.RemoveAllListeners();
            _dimBtn.onClick.AddListener(Close);
        }
        if (_replaceBtn != null)
        {
            _replaceBtn.onClick.RemoveAllListeners();
            _replaceBtn.onClick.AddListener(OnReplaceClicked);
        }
        // 【2026-10-22 主人拍板】「进阶」钮：点击开 MercAscendUI，佣兵取弹窗当前填充的 hireId
        if (_ascendBtn != null)
        {
            _ascendBtn.onClick.RemoveAllListeners();
            _ascendBtn.onClick.AddListener(OnAscendClicked);
        }
    }

    /// <summary>确认钮：招募模式下是「购买」，阵亡复活模式下是「复活」，其余模式是「确认=关」。</summary>
}
