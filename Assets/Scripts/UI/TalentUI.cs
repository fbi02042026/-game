using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 天赋界面（参考 Art/UI/Talent/talent_reference.png）。
/// 左侧属性天赋（金币）+ 右侧辅助/专精（天赋石）；底部属性汇总与重置按钮。
/// 数据来自 TalentDefs；解锁逻辑预留事件，后续对接存档。
/// </summary>
public partial class TalentUI : MonoBehaviour
{
    public static TalentUI Instance { get; private set; }

    // 设计真值（TalentUI.prefab，2026-10-16 实测）：Panel 700×1100 → Columns 宽 628（stretch -72），
    // 左右列各 -4，左列视口约 302。节点宽取模板自身设计宽，不跟着列宽被拉/压。
    // 🔴 2026-10-16 主人拍板「以预制体的表现为准」：删掉旧 FitColumnsScale——它按 648 设计宽
    //    把 628 的列永久缩到 96.9%，运行时从来没和预制体一致过；现在一律按预制体原尺寸渲染，不缩放。
    const float LeftNodeH = 96f;      // 兜底行高：模板 sizeDelta.y>8 时以模板为准（当前左模板 100）
    const float RightRowH = 126f;     // 兜底行高：同上（当前右模板 150）
    const float LeftNodeW = 258.6f;   // 模板 stretch 到 720 根下的原始宽（720-461.405）
    const float RightRowW = 269.2f;

    [Header("壳")]
    public Image panelImage;
    public Button closeButton;
    public Text titleText;
    public Text goldText;
    public Text stoneText;
    public Button stonePlusButton;

    [Header("美术（Inspector 里直接替换 Sprite）")]
    public Sprite sprPanelBg;
    public Sprite sprClose;
    public Sprite sprLeftCard;
    public Sprite sprRightCard;
    public Sprite sprFooter;
    public Sprite sprReset;
    public Sprite sprGoldBar;
    public Sprite sprStoneBar;
    public Sprite sprLeftHexOff;
    public Sprite sprLeftHexOn;
    public Sprite sprCheckOff;
    public Sprite sprCheckOn;
    public Sprite sprLeftLinkOff;
    public Sprite sprLeftLinkOn;
    public Sprite sprRightHex;
    public Sprite sprRightHexAlt;
    public Sprite sprDiamondOff;
    public Sprite sprDiamondOn;
    public Sprite sprRightLinkOff;
    public Sprite sprRightLinkOn;
    public Sprite sprLock;
    public Sprite sprArrow;

    [Header("左栏")]
    public ScrollRect leftScroll;
    public RectTransform leftContent;
    public Text leftTipText;
    public Text leftCostText;
    public GameObject leftNodeTemplate;

    [Header("右栏")]
    public ScrollRect rightScroll;
    public RectTransform rightContent;
    public Text rightTipText;
    public Text rightCostValueText;
    public GameObject rightRowTemplate;
    [Tooltip("右列首行（两选项，如物理/魔法专精）；默认识别 RightRowTemplate (1)")]
    public GameObject rightExtraRowTemplate;

    [Header("底栏")]
    public Text sumAttackText;
    public Text sumMagicText;
    public Text sumHpText;
    public Text sumDefText;
    public Text sumCritText;
    public Text sumAtkSpdText;
    public Button resetButton;
    public Button resetButtonGray;

    [Header("选择弹层")]
    public GameObject choicePopup;
    public Text choiceTitleText;
    public Text choiceDescText;
    public Button[] choiceButtons = new Button[3];
    public Text[] choiceLabels = new Text[3];
    public Image[] choiceIcons = new Image[3];
    public Button choiceConfirmButton;
    public Button choiceCancelButton;
    public GameObject choiceOptionTemplate;

    [Header("事件（对接用）")]
    public UnityEvent onClosed;
    public UnityEvent<int> onLeftUnlockRequested;   // L index 1..40
    public UnityEvent<int, int> onRightChoiceRequested; // R index, option index
    public UnityEvent onResetRequested;

    readonly List<LeftNodeView> _leftViews = new List<LeftNodeView>();
    readonly List<RightNodeView> _rightViews = new List<RightNodeView>();
    int _pendingRowIndex = -1;
    int _pendingSelectedOpt = -1;
    TalentDefs.TalentRightNode _pendingNode;
    bool _wired;
    bool _listsBuilt;

    // 右列「开通演出」（2026-10-13 主人拍板）：左列点数跨过门槛（10/20/30/40）时，
    // 对应那批右列节点「暗→亮 + 放大缩小」提示玩家。⚠ 只在 OnClickLeft 里触发一次，
    // 打开界面 / 重复刷新都不重播（RefreshRight 不碰这两个字段）。
    Coroutine _rightUnlockFx;
    readonly List<RightNodeView> _fxBatch = new List<RightNodeView>();

    // 洗点二次确认弹窗（运行时建树，不碰 prefab）
    GameObject _resetConfirmPopup;
    Text _resetConfirmDesc;

    // 【2026-10-18 主人拍板】二选一弹窗（物理/魔法专精解锁入口，带软引导）+ 费用确认弹窗
    //（解锁/升级前看效果与消耗；天赋石不足也照弹，能看到「需要多少」）。均运行时建树，不碰 prefab。
    GameObject _mutexChoicePopup;
    Text _mutexChoiceCost;
    GameObject[] _mutexOptCards;
    Text[] _mutexOptLabels;
    Image[] _mutexOptIcons;
    TalentDefs.TalentRightNode _pendingMutexA;
    TalentDefs.TalentRightNode _pendingMutexB;
    int _pendingMutexSelected = -1;

    GameObject _costConfirmPopup;
    Text _costConfirmDesc;
    Text _costConfirmOkLabel;
    System.Action _costConfirmAction;

    class LeftNodeView
    {
        public int index;
        public GameObject root;
        public Button button;
        public Image icon;
        public Text nameText;
        public Text effectText;
        public Image check;
        public Image line;
        public GameObject redDot;
        public GameObject upgradeButton;
        public Text upgradeCostText;
        /// <summary>【2026-10-13 主人拍板】左列序号 L1…L40（贴在卡片左端，Iconbg 左边那块空位）。</summary>
        public Text orderText;
    }

    class RightNodeView
    {
        public TalentDefs.TalentRightNode node;
        /// <summary>【2026-10-18 主人拍板】合并行的第二个节点（流派·物理专精 + 流派·魔法专精 二选一合一行）。
        /// null = 普通单节点行。数据/存档仍是两个节点，只合 UI。</summary>
        public TalentDefs.TalentRightNode node2;
        /// <summary>合并行专用：两个选项槽（Opt_0/Opt_1）的图标与名字，普通行为 null。</summary>
        public Image[] pairIcons;
        public Text[] pairLabels;
        public int visualIndex;
        public GameObject root;
        public Text titleText;
        public Text costText;
        public Image lockIcon;
        public Image diamond;
        /// <summary>菱形**上方**那段连线（预制体 Line）——最上一行没得连，隐藏。</summary>
        public Image lineUp;
        /// <summary>【2026-10-13 主人拍板】菱形**下方**那段连线（预制体 Line (1)，主人手工复制）——最下一行隐藏。</summary>
        public Image lineDown;
        public GameObject redDot;
        public Button button;
        /// <summary>2026-09-29：右上角红色「新」字（节点刚开通、还没点过才显示，点了就消失）。</summary>
        public Text newTag;
    }

    void Awake()
    {
        Instance = this;
        LoadArtSprites();
        if (panelImage == null)
            AutoBindFromHierarchy();
        EnsureCurrencyBindings();
        EnsureChoicePopupBindings();
        CloseChoicePopup();
        EnsureVisibleTransform();
        WireClicks();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 预热（T1，治「首次点击天赋卡顿」）：
    /// 进城镇空闲 / Loading 遮罩期间提前 Instantiate 并隐藏常驻，把同步重活
    /// （LoadArtSprites 22 个精灵 + EnsureLists 实例化全部节点 + 强制布局重建 +
    /// RefreshAll 首帧全量刷新，含红点 / 新标签 / 锁精灵的创建与加载）从「点击瞬间」
    /// 挪到「进城镇时」。点击时 Instance 已非 null，Show 仅走轻量刷新路径。
    /// </summary>
    public static void Prewarm()
    {
        if (Instance != null) return;
        var prefab = Resources.Load<GameObject>("Prefabs/Talent/TalentUI");
        if (prefab == null) return;
        var go = Instantiate(prefab);
        go.name = "TalentUI";
        // Instantiate 后物体 active：先跑重活（active 态下布局重建才正确），再隐藏常驻。
        Instance.EnsureLists();
        Instance.RefreshAll();
        go.SetActive(false);
    }

    public void Show()
    {
        EnsureVisibleTransform();
        EnsureCurrencyBindings();
        EnsureChoicePopupBindings();
        CloseChoicePopup(); // 预制体 ChoicePopup 默认可能为 active，打开时强制关
        CloseResetConfirm(); // 顺手关掉可能残留的洗点确认框
        CloseMutexChoice();  // 顺手关掉可能残留的二选一/费用确认弹窗
        CloseCostConfirm();
        LoadArtSprites();
        if (!_wired) WireClicks();
        else WireChoiceConfirmIfNeeded();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        var canvas = UICanvasSetup.ApplyOn(gameObject, UICanvasSetup.ResolveUiCamera());
        if (canvas != null)
            UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownVeil, UICanvasSetup.ResolveUiCamera());
        // 已有自带 Dim 则不再叠 TownPageDim，避免过黑
        if (transform.Find("Dim") == null)
            TownPageDim.Ensure(transform);
        TavernUI.SetGuildHallOverlayMode(true);
        EnsureLists();
        RefreshAll();
        // 2026-09-29 主人纠正：**打开天赋页不清红点** —— 玩家点左列时本来就在天赋页里，
        // 那样红点等于永远看不到。红点只在「点了右侧那个新天赋」之后才灭，见 RefreshRight / TalentSystem。
    }

    public void Hide()
    {
        CloseChoicePopup();
        CloseMutexChoice();
        CloseCostConfirm();
        StopRightUnlockFx(); // 演出中途关界面：别把半截缩放/半截灰度留在节点上
        gameObject.SetActive(false);
        TavernUI.SetGuildHallOverlayMode(false);
        onClosed?.Invoke();
    }

    /// <summary>停掉右列开通演出并把缩放还原（下一次 RefreshRight 会把灰度/锁按真值重刷）。</summary>
    void StopRightUnlockFx()
    {
        if (_rightUnlockFx != null)
        {
            StopCoroutine(_rightUnlockFx);
            _rightUnlockFx = null;
        }
        for (int i = 0; i < _rightViews.Count; i++)
            if (_rightViews[i].root != null) _rightViews[i].root.transform.localScale = Vector3.one;
    }
}
