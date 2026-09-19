using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 天赋界面（参考 Art/UI/Talent/talent_reference.png）。
/// 左侧属性天赋（金币）+ 右侧辅助/专精（天赋石）；底部属性汇总与重置按钮。
/// 数据来自 TalentDefs；解锁逻辑预留事件，后续对接存档。
/// </summary>
public class TalentUI : MonoBehaviour
{
    public static TalentUI Instance { get; private set; }

    // 设计真值（TalentUI.prefab）：Columns 宽 648（stretch -72），左右列各 -4。
    // Columns 是横向 stretch，宽随 aspect 漂；节点宽取模板自身设计宽，不跟着列宽被拉/压。
    const float DesignColumnsW = 648f;
    const float LeftNodeH = 108f;
    const float RightRowH = 126f;
    const float LeftNodeW = 258.6f;   // 模板 stretch 到 720 根下的原始宽
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
    bool _scrollSyncing;

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
    }

    class RightNodeView
    {
        public TalentDefs.TalentRightNode node;
        public int visualIndex;
        public GameObject root;
        public Text titleText;
        public Text costText;
        public Image lockIcon;
        public Image diamond;
        public Image line;
        public GameObject redDot;
        public Button button;
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
        if (leftScroll != null)
            leftScroll.onValueChanged.RemoveListener(OnLeftScroll);
        if (rightScroll != null)
            rightScroll.onValueChanged.RemoveListener(OnRightScroll);
        if (Instance == this) Instance = null;
    }

    public void Show()
    {
        EnsureVisibleTransform();
        EnsureCurrencyBindings();
        EnsureChoicePopupBindings();
        CloseChoicePopup(); // 预制体 ChoicePopup 默认可能为 active，打开时强制关
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
    }

    public void Hide()
    {
        CloseChoicePopup();
        gameObject.SetActive(false);
        TavernUI.SetGuildHallOverlayMode(false);
        onClosed?.Invoke();
    }

    /// <summary>刷新货币、节点状态、底部加成（读档后可再调）</summary>
    public void RefreshAll()
    {
        RefreshCurrency();
        RefreshLeft();
        RefreshRight();
        RefreshSummary();
        RefreshResetButton();
    }

    public void RefreshCurrency()
    {
        long gold = 0;
        int stone = 0;
        try
        {
            if (SaveSystem.Instance != null && SaveSystem.Instance.Data != null)
            {
                gold = SaveSystem.Instance.Data.totalGold;
                stone = SaveSystem.Instance.Data.talentPoints;
            }
        }
        catch { /* 编辑器预览无存档 */ }

        if (goldText != null) goldText.text = FormatCompact(gold);
        if (stoneText != null) stoneText.text = FormatCompact(stone);
    }

    void RefreshLeft()
    {
        var talents = GetTalents();
        int unlocked = TalentDefs.LeftUnlockedCount(talents);
        int nextCost = 0;
        if (unlocked < TalentDefs.Left.Length)
            nextCost = TalentSystem.GetLeftGoldCost(TalentDefs.Left[unlocked], talents);

        for (int i = 0; i < _leftViews.Count; i++)
        {
            var v = _leftViews[i];
            var def = TalentDefs.Left[i];
            bool on = i < unlocked;
            bool can = TalentSystem.IsLeftUpgradeable(i, talents);
            bool locked = !on && !can;

            if (v.nameText != null) v.nameText.text = def.name;
            if (v.effectText != null) v.effectText.text = def.effect.display;
            if (v.icon != null)
            {
                var sp = TalentIcons.GetLeftAttr(i % 5);
                if (sp != null) ApplySprite(v.icon, sp, true);
                v.icon.preserveAspect = true;
                v.icon.type = Image.Type.Simple;
                v.icon.color = locked ? new Color(0.55f, 0.55f, 0.55f, 1f) : Color.white;
            }
            if (v.check != null)
            {
                // Check 仅作可升级提示；已解锁/锁定时隐藏
                v.check.gameObject.SetActive(can);
                if (can) ApplySprite(v.check, sprCheckOn, true);
            }
            if (v.upgradeButton != null)
            {
                v.upgradeButton.SetActive(can);
                if (can && v.upgradeCostText != null && nextCost > 0 && i == unlocked)
                    v.upgradeCostText.text = nextCost.ToString();
            }
            if (v.button != null) v.button.interactable = can;
            // 节点始终显示；仅灰态表示锁定
            if (v.root != null) v.root.SetActive(true);
            SetRowGray(v.root, locked);
            SetRowRedDot(v.root, ref v.redDot, can);
            if (v.line != null)
            {
                // 最底 L1（index 0）不画向下连接线；其余连线
                bool showLine = i > 0;
                v.line.gameObject.SetActive(showLine);
                if (showLine) ApplySprite(v.line, on ? sprLeftLinkOn : sprLeftLinkOff, false);
            }
        }

        if (leftCostText != null)
            leftCostText.text = nextCost > 0 ? nextCost.ToString() : "0";
        if (leftTipText != null)
            leftTipText.text = "消耗金币解锁属性天赋";

        // GoldPanel 的 Plus：仅当前有可升级左天赋时显示
        var goldPlus = transform.Find("Panel/ResourceRow/GoldPanel/PlusButton");
        if (goldPlus != null)
            goldPlus.gameObject.SetActive(nextCost > 0 && TalentSystem.IsLeftUpgradeable(unlocked, talents));
    }

    void RefreshRight()
    {
        var talents = GetTalents();
        int leftUnlocked = TalentDefs.LeftUnlockedCount(talents);

        for (int i = 0; i < _rightViews.Count; i++)
            RefreshOneRightNode(_rightViews[i], talents, leftUnlocked);

        if (rightTipText != null)
            rightTipText.text = "消耗天赋石解锁/升级天赋（分批开放）";
        if (rightCostValueText != null)
            rightCostValueText.text = "";
    }

    void RefreshOneRightNode(RightNodeView v, Dictionary<string, int> talents, int leftUnlocked)
    {
        var node = v.node;
        if (node == null) return;

        var data = SaveSystem.Instance?.Data;
        int level = TalentDefs.GetRightNodeLevel(talents, node.id);
        bool unlocked = level > 0;
        bool mutexLocked = !unlocked && TalentDefs.IsRightNodeMutexLocked(node, talents);
        bool thresholdLocked = !unlocked && leftUnlocked < node.unlockLeftIndex;
        int cap = TalentDefs.RightNodeLevelCap(node, leftUnlocked);

        string status;
        bool canAct = false;
        if (unlocked && level >= node.maxLevel)
            status = "MAX";
        else if (unlocked)
        {
            // 任务3：等级上限受左列已点数量限制
            if (level >= cap)
                status = "Lv" + cap + " (左列↑)";
            else
            {
                int cost = node.costByLevel != null && level < node.costByLevel.Length ? node.costByLevel[level] : 0;
                status = "升级 " + cost;
                canAct = data != null && data.talentPoints >= cost;
            }
        }
        else if (thresholdLocked)
            status = "左列 L" + node.unlockLeftIndex;   // 任务3：分批开放提示
        else if (mutexLocked)
            status = "互斥";                            // 任务4：互斥置灰
        else
        {
            int cost = node.costByLevel != null && node.costByLevel.Length > 0 ? node.costByLevel[0] : 0;
            status = "解锁 " + cost;
            canAct = data != null && data.talentPoints >= cost;
        }

        if (v.titleText != null)
            v.titleText.text = node.name + (unlocked ? "  Lv" + level : "");
        if (v.costText != null)
            v.costText.text = status;

        bool locked = !unlocked && (thresholdLocked || mutexLocked);
        EnsureRightLock(v);
        if (v.lockIcon != null)
        {
            v.lockIcon.gameObject.SetActive(locked);
            if (locked) ApplyLockSprite(v.lockIcon);
        }
        if (v.diamond != null)
            ApplySprite(v.diamond, unlocked ? sprDiamondOn : sprDiamondOff, true);
        if (v.line != null)
        {
            bool showLine = v.visualIndex < _rightViews.Count - 1;
            v.line.gameObject.SetActive(showLine);
            if (showLine)
                ApplySprite(v.line, unlocked ? sprRightLinkOn : sprRightLinkOff, false);
        }

        if (v.root != null) v.root.SetActive(true);
        SetRowGray(v.root, locked ? 0.72f : 1f);
        SetRowRedDot(v.root, ref v.redDot, canAct);
        if (v.button != null) v.button.interactable = !locked;
    }

    void ApplyLockSprite(Image img)
    {
        if (img == null || img.sprite != null) return;
        var lockSp = sprLock ?? Resources.Load<Sprite>("UI/Common/锁");
#if UNITY_EDITOR
        if (lockSp == null)
            lockSp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Common/锁.png");
#endif
        if (lockSp != null) ApplySprite(img, lockSp, true);
    }

    void RefreshResetButton()
    {
        bool canReset = TalentSystem.CanReset(out _);
        if (resetButton != null)
        {
            resetButton.gameObject.SetActive(canReset);
            resetButton.interactable = canReset;
        }
        if (resetButtonGray != null)
        {
            resetButtonGray.gameObject.SetActive(!canReset);
            resetButtonGray.interactable = false;
        }
    }

    void RefreshSummary()
    {
        float atk = 0, hp = 0, def = 0, crit = 0, spd = 0;
        var talents = GetTalents();
        if (talents != null)
        {
            int leftN = TalentDefs.LeftUnlockedCount(talents);
            for (int i = 0; i < leftN; i++)
            {
                var e = TalentDefs.Left[i].effect;
                switch (e.kind)
                {
                    case TalentDefs.AttrKind.Attack: atk += e.value; break;
                    case TalentDefs.AttrKind.Hp: hp += e.value; break;
                    case TalentDefs.AttrKind.Defense: def += e.value; break;
                    case TalentDefs.AttrKind.CritRate: crit += e.value; break;
                    case TalentDefs.AttrKind.AtkSpeed: spd += e.value; break;
                }
            }
            // 右列节点：攻击/生命/防御/暴击/攻速 计入面板汇总
            for (int i = 0; i < TalentDefs.RightNodes.Length; i++)
            {
                var node = TalentDefs.RightNodes[i];
                int lv = TalentDefs.GetRightNodeLevel(talents, node.id);
                if (lv <= 0) continue;
                int job = TalentDefs.GetRightNodeChosenJob(talents, node.id);
                var opt = node.IsJobChoice ? node.ChosenOption(job)
                                           : (node.options != null && node.options.Length > 0 ? node.options[0] : null);
                if (opt == null) continue;
                switch (opt.kind)
                {
                    case TalentDefs.AttrKind.Attack: atk += node.EffectValue(lv, job); break;
                    case TalentDefs.AttrKind.Hp: hp += node.EffectValue(lv, job); break;
                    case TalentDefs.AttrKind.Defense: def += node.EffectValue(lv, job); break;
                    case TalentDefs.AttrKind.CritRate: crit += node.EffectValue(lv, job); break;
                    case TalentDefs.AttrKind.AtkSpeed: spd += node.EffectValue(lv, job); break;
                }
            }
        }

        if (sumAttackText != null) sumAttackText.text = "+" + atk.ToString("0");
        if (sumHpText != null) sumHpText.text = "+" + hp.ToString("0");
        if (sumDefText != null) sumDefText.text = "+" + def.ToString("0");
        if (sumCritText != null) sumCritText.text = "+" + crit.ToString("0.##") + "%";
        if (sumAtkSpdText != null) sumAtkSpdText.text = "+" + spd.ToString("0.##") + "%";
    }

    static Dictionary<string, int> GetTalents()
    {
        try
        {
            if (SaveSystem.Instance != null && SaveSystem.Instance.Data != null)
                return SaveSystem.Instance.Data.talents;
        }
        catch { }
        return null;
    }

    /// <summary>当前存档天赋字典（对外只读入口，引导用来判断"点了没有"）。</summary>
    public static Dictionary<string, int> CurrentTalents()
    {
        return GetTalents();
    }

    /// <summary>已解锁的左栏节点数（0..TalentDefs.Left.Length）。</summary>
    public static int LeftUnlockedCount()
    {
        return TalentDefs.LeftUnlockedCount(GetTalents());
    }

    /// <summary>
    /// 取左栏第 index0 个节点的高亮目标（优先升级按钮，退回节点根）。
    /// 新手引导「回城点一次天赋」用；列表还没建好就返回 null，引导自己要能容错。
    /// </summary>
    public RectTransform GetLeftNode(int index0)
    {
        EnsureLists();
        if (index0 < 0 || index0 >= _leftViews.Count) return null;
        var v = _leftViews[index0];
        if (v == null) return null;
        if (v.upgradeButton != null)
        {
            var rt = v.upgradeButton.GetComponent<RectTransform>();
            if (rt != null && v.upgradeButton.gameObject.activeInHierarchy) return rt;
        }
        return v.root != null ? v.root.GetComponent<RectTransform>() : null;
    }

    /// <summary>界面当前是否打开（引导等待"玩家关掉天赋页"用）。</summary>
    public bool IsOpen => gameObject.activeInHierarchy;

    static string FormatCompact(long v)
    {
        // 直接显示真实数量，不再压成 999999+
        return v.ToString();
    }

    static void ApplySprite(Image img, Sprite sp, bool preserveAspect)
    {
        if (img == null || sp == null) return;
        img.sprite = sp;
        img.color = Color.white;
        img.preserveAspect = preserveAspect;
        img.type = Image.Type.Simple;
    }

    static void SetGraphicAlpha(GameObject go, float a)
    {
        if (go == null) return;
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        cg.alpha = a;
    }

    static void SetRowGray(GameObject root, bool gray)
    {
        SetGraphicAlpha(root, gray ? 0.72f : 1f);
    }

    static void SetRowGray(GameObject root, float alpha)
    {
        SetGraphicAlpha(root, Mathf.Clamp01(alpha));
    }

    static void SetRowRedDot(GameObject rowRoot, ref GameObject dot, bool show)
    {
        if (rowRoot == null) return;
        if (!show)
        {
            if (dot != null) dot.SetActive(false);
            return;
        }
        if (dot == null)
        {
            dot = new GameObject("UpgradeRedDot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dot.transform.SetParent(rowRoot.transform, false);
            var rt = dot.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(18f, 18f);
            rt.anchoredPosition = new Vector2(-6f, -6f);
            var img = dot.GetComponent<Image>();
            img.sprite = RedDot.Sprite;
            img.raycastTarget = false;
            img.preserveAspect = true;
        }
        dot.SetActive(true);
    }

    void WireClicks()
    {
        if (_wired) return;
        _wired = true;
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }
        if (resetButton != null)
        {
            resetButton.onClick.RemoveAllListeners();
            resetButton.onClick.AddListener(OnClickReset);
        }
        if (resetButtonGray != null)
        {
            resetButtonGray.onClick.RemoveAllListeners();
            resetButtonGray.interactable = false;
        }
        if (choiceCancelButton != null)
        {
            choiceCancelButton.onClick.RemoveAllListeners();
            choiceCancelButton.onClick.AddListener(CloseChoicePopup);
        }
        WireChoiceConfirmIfNeeded();
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            int opt = i;
            if (choiceButtons[i] == null) continue;
            choiceButtons[i].onClick.RemoveAllListeners();
            choiceButtons[i].onClick.AddListener(() => SelectChoicePopupOption(opt));
        }
    }

    void WireChoiceConfirmIfNeeded()
    {
        EnsureChoicePopupBindings();
        if (choiceConfirmButton == null) return;
        choiceConfirmButton.onClick.RemoveAllListeners();
        choiceConfirmButton.onClick.AddListener(ConfirmChoicePopup);
    }

    /// <summary>
    /// 预制体里已有「确定」，但序列化未挂 choiceConfirmButton，且 Awake 因 panelImage
    /// 已绑而跳过 AutoBind，导致多选天赋弹层无法确认解锁。
    /// </summary>
    void EnsureChoicePopupBindings()
    {
        if (choicePopup == null)
            choicePopup = transform.Find("ChoicePopup")?.gameObject;

        if (choiceConfirmButton == null && choicePopup != null)
        {
            choiceConfirmButton = choicePopup.transform.Find("确定")?.GetComponent<Button>()
                                  ?? choicePopup.transform.Find("Confirm")?.GetComponent<Button>();
        }

        if (choiceCancelButton == null && choicePopup != null)
        {
            choiceCancelButton = choicePopup.transform.Find("Cancel")?.GetComponent<Button>()
                                 ?? choicePopup.transform.Find("取消")?.GetComponent<Button>();
            if (choiceCancelButton != null)
            {
                choiceCancelButton.onClick.RemoveAllListeners();
                choiceCancelButton.onClick.AddListener(CloseChoicePopup);
            }
        }

        if (choiceTitleText == null)
        {
            choiceTitleText = FindTxt("ChoicePopup/Title")
                              ?? FindTxt("ChoicePopup/标头/Title");
        }

        if (choiceDescText == null)
        {
            // 勿占用 Choice_0/Label（那是选项名）；优先独立描述节点
            choiceDescText = FindTxt("ChoicePopup/Desc")
                             ?? FindTxt("ChoicePopup/Description")
                             ?? FindTxt("ChoicePopup/EffectText")
                             ?? FindTxt("ChoicePopup/标头/Desc");
        }

        if (choiceButtons == null || choiceButtons.Length < 3)
            choiceButtons = new Button[3];
        if (choiceLabels == null || choiceLabels.Length < 3)
            choiceLabels = new Text[3];
        if (choiceIcons == null || choiceIcons.Length < 3)
            choiceIcons = new Image[3];

        for (int i = 0; i < 3; i++)
        {
            if (choiceButtons[i] != null) continue;
            var t = transform.Find($"ChoicePopup/Choice_{i}");
            if (t == null) continue;
            choiceButtons[i] = t.GetComponent<Button>();
            if (choiceLabels[i] == null)
                choiceLabels[i] = choiceButtons[i]?.GetComponentInChildren<Text>(true);
            var iconT = t.Find("Icon") ?? t.Find("icon");
            if (choiceIcons[i] == null && iconT != null)
                choiceIcons[i] = iconT.GetComponent<Image>();
        }

        if (choiceOptionTemplate == null && choiceButtons[0] != null)
            choiceOptionTemplate = choiceButtons[0].gameObject;
    }

    void OnClickReset()
    {
        if (!TalentSystem.CanReset(out string reason))
        {
            Debug.Log("[TalentUI] " + reason);
            return;
        }
        onResetRequested?.Invoke();
        RefreshAll();
    }

    void OnClickLeft(int index0)
    {
        if (!TalentSystem.TryUnlockLeft(index0 + 1, out string reason))
        {
            Debug.Log("[TalentUI] " + reason);
            return;
        }
        onLeftUnlockRequested?.Invoke(index0 + 1);
        RefreshAll();
    }

    /// <summary>
    /// 右列节点点击：未解锁→解锁（JobChoice 先弹 6选1）；已解锁→升级。
    /// 分批开放/互斥/双修前置由 TalentSystem 统一判定并返回 reason。
    /// </summary>
    void OnClickRightNode(int visualIndex0)
    {
        if (visualIndex0 < 0 || visualIndex0 >= _rightViews.Count) return;
        var node = _rightViews[visualIndex0].node;
        if (node == null) return;

        var talents = GetTalents();
        int level = TalentDefs.GetRightNodeLevel(talents, node.id);

        if (level <= 0)
        {
            // 任务4：JobChoice 必须先选职业
            if (node.IsJobChoice)
                OpenRightJobChoice(node);
            else if (TalentSystem.TryUnlockRightNode(node.id, 0, out string reason))
                RefreshAll();
            else
                Debug.Log("[TalentUI] " + reason);
        }
        else
        {
            // 已解锁：升级（JobChoice 沿用已选职业）
            if (TalentSystem.TryUpgradeRightNode(node.id, out string reason))
                RefreshAll();
            else
                Debug.Log("[TalentUI] " + reason);
        }
    }

    /// <summary>任务4 / R_JOB 6选1：打开职业选择弹层（最多 6 个职业）。</summary>
    void OpenRightJobChoice(TalentDefs.TalentRightNode node)
    {
        EnsureChoicePopupBindings();
        WireChoiceConfirmIfNeeded();

        _pendingNode = node;
        _pendingRowIndex = node.index - 1;
        _pendingSelectedOpt = -1;

        if (choicePopup != null) choicePopup.SetActive(true);
        if (choiceTitleText != null) choiceTitleText.text = node.name;

        var opts = node.options ?? new TalentDefs.TalentRightOption[0];
        EnsureChoicePopupOptions(opts.Length);
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            bool on = i < opts.Length;
            if (choiceButtons[i] != null) choiceButtons[i].gameObject.SetActive(on);
            if (!on) continue;
            if (choiceLabels[i] != null) choiceLabels[i].text = opts[i].name;
            if (choiceIcons[i] != null)
            {
                var sp = TalentIcons.GetTalent(opts[i].name);
                if (sp != null) ApplySprite(choiceIcons[i], sp, true);
            }
        }
        SelectChoicePopupOption(0);
    }

    /// <summary>弹层选项数量可能超过 3（如 6 职业）；按需克隆按钮并保留原有 Click 绑定。</summary>
    void EnsureChoicePopupOptions(int count)
    {
        if (choiceOptionTemplate == null && choiceButtons != null && choiceButtons.Length > 0 && choiceButtons[0] != null)
            choiceOptionTemplate = choiceButtons[0].gameObject;

        // 数组扩到 count（默认只有 3 个，6 职业需扩容）
        if (choiceButtons == null || choiceButtons.Length < count)
        {
            int n = count;
            var nb = new Button[n];
            var nl = new Text[n];
            var ni = new Image[n];
            int copy = choiceButtons != null ? System.Math.Min(choiceButtons.Length, n) : 0;
            for (int i = 0; i < copy; i++)
            {
                nb[i] = choiceButtons[i];
                nl[i] = choiceLabels != null ? choiceLabels[i] : null;
                ni[i] = choiceIcons != null ? choiceIcons[i] : null;
            }
            choiceButtons = nb;
            choiceLabels = nl;
            choiceIcons = ni;
        }

        for (int i = 0; i < count; i++)
        {
            if (choiceButtons[i] == null)
            {
                if (choiceOptionTemplate == null) break;
                var clone = Instantiate(choiceOptionTemplate, choicePopup.transform);
                clone.name = "Choice_" + i;
                float xOff = -120f + i * 120f;
                var rt = clone.GetComponent<RectTransform>();
                if (rt != null) rt.anchoredPosition = new Vector2(xOff, 104f);
                choiceButtons[i] = clone.GetComponent<Button>();
                choiceLabels[i] = clone.GetComponentInChildren<Text>(true);
                var iconT = clone.transform.Find("Icon") ?? clone.transform.Find("icon");
                choiceIcons[i] = iconT != null ? iconT.GetComponent<Image>() : null;
                int opt = i;
                choiceButtons[i].onClick.RemoveAllListeners();
                choiceButtons[i].onClick.AddListener(() => SelectChoicePopupOption(opt));
            }
        }
    }

    void SelectChoicePopupOption(int optIndex0)
    {
        if (_pendingNode == null || optIndex0 < 0) return;
        _pendingSelectedOpt = optIndex0;
        var opts = _pendingNode.options;
        if (opts == null || optIndex0 >= opts.Length) return;

        // 弹层文案显示该职业 1 级效果（JobChoice 各职业 +4%×等级）
        if (choiceDescText != null)
            choiceDescText.text = _pendingNode.EffectSummary(1, optIndex0 + 1);

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (choiceButtons[i] == null || !choiceButtons[i].gameObject.activeSelf) continue;
            SetGraphicAlpha(choiceButtons[i].gameObject, i == optIndex0 ? 1f : 0.55f);
        }
    }

    void ConfirmChoicePopup()
    {
        if (_pendingNode == null || _pendingSelectedOpt < 0) return;
        int chosenJob1Based = _pendingSelectedOpt + 1;
        if (!TalentSystem.TryUnlockRightNode(_pendingNode.id, chosenJob1Based, out string reason))
        {
            Debug.Log("[TalentUI] " + reason);
            return;
        }
        onRightChoiceRequested?.Invoke(_pendingNode.index, chosenJob1Based);
        CloseChoicePopup();
        RefreshAll();
    }

    void CloseChoicePopup()
    {
        if (choicePopup != null) choicePopup.SetActive(false);
        _pendingNode = null;
        _pendingRowIndex = -1;
        _pendingSelectedOpt = -1;
    }

    void EnsureLists()
    {
        if (_listsBuilt) return;
        if (leftContent == null || rightContent == null)
            AutoBindFromHierarchy();
        if (leftNodeTemplate == null || rightRowTemplate == null)
        {
            Debug.LogWarning("[TalentUI] 缺少节点模板，请重新生成预制体");
            return;
        }

        if (rightExtraRowTemplate == null)
        {
            var alt = transform.Find("RightRowTemplate (1)");
            if (alt != null) rightExtraRowTemplate = alt.gameObject;
        }

        leftNodeTemplate.SetActive(false);
        rightRowTemplate.SetActive(false);
        if (rightExtraRowTemplate != null) rightExtraRowTemplate.SetActive(false);

        // 屏幕比设计稿更瘦时 Columns 被压窄 → 左列会被裁；整列等比缩小，不裁也不拉伸
        FitColumnsScale();

        // 列表只用 LayoutGroup 排，禁止手写改每个框的坐标/宽高
        EnsureScrollContentLayout(leftContent);
        EnsureScrollContentLayout(rightContent);

        for (int i = 0; i < TalentDefs.Left.Length; i++)
        {
            var go = Instantiate(leftNodeTemplate, leftContent);
            go.name = "LeftNode_" + (i + 1);
            go.SetActive(true);
            PrepareListItemUnderContent(go.transform as RectTransform, leftContent, LeftNodeW, LeftNodeH);
            var view = BindLeftNode(go, i);
            _leftViews.Add(view);
            int idx = i;
            view.button?.onClick.AddListener(() => OnClickLeft(idx));
        }

        for (int i = 0; i < TalentDefs.RightNodes.Length; i++)
        {
            var go = Instantiate(rightRowTemplate, rightContent);
            go.name = "RightRow_" + (i + 1);
            go.SetActive(true);
            PrepareListItemUnderContent(go.transform as RectTransform, rightContent, RightRowW, RightRowH);
            var node = TalentDefs.RightNodes[i];
            var view = BindRightNode(go, i, node);
            _rightViews.Add(view);
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(leftContent);
        LayoutRebuilder.ForceRebuildLayoutImmediate(rightContent);

        WireScroll(leftScroll, leftContent);
        WireScroll(rightScroll, rightContent);
        WireScrollSync();

        _listsBuilt = true;
        WireClicks();
    }

    /// <summary>Content 用 VerticalLayoutGroup 排布；不拉伸子项宽高。</summary>
    static void EnsureScrollContentLayout(RectTransform content)
    {
        if (content == null) return;
        var vlg = content.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(0, 0, 4, 4);
        vlg.spacing = 8f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;
        vlg.reverseArrangement = true; // 与原先「底端先解锁」一致：L1 在视觉底部

        var fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    /// <summary>
    /// Columns 是横向 stretch（sizeDelta.x = -72），屏幕比设计稿瘦时会被压窄，
    /// 左列内容因此被裁。这里整列等比缩小：宽屏不放大（只留白），窄屏缩到放得下。
    /// </summary>
    void FitColumnsScale()
    {
        var columns = FindAncestorNamed(leftContent, "Columns");
        if (columns == null) return;
        float w = columns.rect.width;
        if (w <= 1f) return;
        float s = Mathf.Min(1f, w / DesignColumnsW);
        columns.localScale = new Vector3(s, s, 1f);
    }

    static RectTransform FindAncestorNamed(RectTransform from, string name)
    {
        for (var p = from; p != null; p = p.parent as RectTransform)
            if (p.name == name) return p;
        return null;
    }

    /// <summary>
    /// 模板原挂在根下（大负边距 stretch）。进 Content 后只烘焙为固定高宽，
    /// 不改子节点；宽锁模板设计宽，高用调用方的行高常量。
    /// </summary>
    static void PrepareListItemUnderContent(RectTransform item, RectTransform content, float defaultW, float defaultH)
    {
        if (item == null) return;
        // 模板是 stretch 时 sizeDelta.y 是负边距不是高度，abs 会算出几百的假高度；只有正值才可信
        float h = item.sizeDelta.y > 8f ? item.sizeDelta.y : defaultH;
        float w = defaultW;
        item.anchorMin = item.anchorMax = new Vector2(0.5f, 1f);
        item.pivot = new Vector2(0.5f, 1f);
        item.sizeDelta = new Vector2(w, h);
        item.anchoredPosition = Vector2.zero;
        item.localScale = Vector3.one;
    }

    static void WireScroll(ScrollRect scroll, RectTransform content)
    {
        if (scroll == null || content == null) return;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.content = content;
        if (scroll.viewport == null)
        {
            var vp = content.parent as RectTransform;
            if (vp != null) scroll.viewport = vp;
        }
        // 内容比视口高才能滑；自下而上时滚到底，最先解锁的在视野底部
        scroll.verticalNormalizedPosition = 0f;
    }

    void WireScrollSync()
    {
        if (leftScroll == null || rightScroll == null) return;
        leftScroll.onValueChanged.RemoveListener(OnLeftScroll);
        rightScroll.onValueChanged.RemoveListener(OnRightScroll);
        leftScroll.onValueChanged.AddListener(OnLeftScroll);
        rightScroll.onValueChanged.AddListener(OnRightScroll);
        leftScroll.verticalNormalizedPosition = 0f;
        rightScroll.verticalNormalizedPosition = 0f;
    }

    void OnLeftScroll(Vector2 _)
    {
        if (_scrollSyncing || rightScroll == null || leftScroll == null) return;
        _scrollSyncing = true;
        rightScroll.verticalNormalizedPosition = leftScroll.verticalNormalizedPosition;
        _scrollSyncing = false;
    }

    void OnRightScroll(Vector2 _)
    {
        if (_scrollSyncing || rightScroll == null || leftScroll == null) return;
        _scrollSyncing = true;
        leftScroll.verticalNormalizedPosition = rightScroll.verticalNormalizedPosition;
        _scrollSyncing = false;
    }

    /// <summary>右列缺 Lock 时按手做模板补一个右下角锁（不改已有坐标）。</summary>
    void EnsureRightLock(RightNodeView v)
    {
        if (v == null || v.root == null) return;
        if (v.lockIcon != null) return;
        var existing = v.root.transform.Find("Lock");
        if (existing != null)
        {
            v.lockIcon = existing.GetComponent<Image>();
            if (v.lockIcon != null) return;
        }

        var go = new GameObject("Lock", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(v.root.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(4.4f, -14.2f);
        rt.sizeDelta = new Vector2(36f, 55f);
        rt.localScale = new Vector3(0.7f, 0.7f, 0.7f);
        v.lockIcon = go.GetComponent<Image>();
        v.lockIcon.raycastTarget = false;
        v.lockIcon.preserveAspect = true;
        var lockSp = sprLock ?? Resources.Load<Sprite>("UI/Common/锁");
#if UNITY_EDITOR
        if (lockSp == null)
            lockSp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Common/锁.png");
#endif
        if (lockSp != null)
            v.lockIcon.sprite = lockSp;
        go.SetActive(false);
    }

    LeftNodeView BindLeftNode(GameObject go, int index0)
    {
        var v = new LeftNodeView { index = index0 + 1, root = go };
        v.button = go.GetComponent<Button>() ?? go.GetComponentInChildren<Button>(true);
        v.icon = go.transform.Find("Icon")?.GetComponent<Image>()
                 ?? go.transform.Find("icon")?.GetComponent<Image>()
                 ?? FindDeepImage(go.transform, "Icon")
                 ?? FindDeepImage(go.transform, "icon");
        v.nameText = go.transform.Find("NameText")?.GetComponent<Text>();
        v.effectText = go.transform.Find("EffectText")?.GetComponent<Text>();
        v.check = go.transform.Find("Check")?.GetComponent<Image>();
        v.line = go.transform.Find("Line")?.GetComponent<Image>();
        var upgradeTf = go.transform.Find("升级") ?? go.transform.Find("Upgrade");
        if (upgradeTf != null)
        {
            v.upgradeButton = upgradeTf.gameObject;
            v.upgradeCostText = upgradeTf.GetComponentInChildren<Text>(true);
            var upBtn = upgradeTf.GetComponent<Button>() ?? upgradeTf.gameObject.AddComponent<Button>();
            upBtn.onClick.RemoveAllListeners();
            int upIdx = index0;
            upBtn.onClick.AddListener(() => OnClickLeft(upIdx));
        }
        var def = TalentDefs.Left[index0];
        if (v.nameText != null) v.nameText.text = def.name;
        if (v.effectText != null) v.effectText.text = def.effect.display;
        if (v.icon != null)
        {
            var sp = TalentIcons.GetLeftAttr(index0 % 5);
            if (sp != null) ApplySprite(v.icon, sp, true);
        }
        // 布局交给 Content 的 VerticalLayoutGroup；禁止再改本节点/子节点坐标
        return v;
    }

    RightNodeView BindRightNode(GameObject go, int visualIndex0, TalentDefs.TalentRightNode node)
    {
        var v = new RightNodeView
        {
            node = node,
            visualIndex = visualIndex0,
            root = go
        };
        v.titleText = go.transform.Find("TitleText")?.GetComponent<Text>();
        v.costText = go.transform.Find("CostText")?.GetComponent<Text>();
        v.lockIcon = go.transform.Find("Lock")?.GetComponent<Image>();
        v.diamond = go.transform.Find("Diamond")?.GetComponent<Image>();
        v.line = go.transform.Find("Line")?.GetComponent<Image>();
        EnsureRightLock(v);
        if (v.titleText != null) v.titleText.text = node.name;
        if (v.costText != null) v.costText.text = "";

        // 整行点击：解锁 / 升级 / JobChoice 弹 6选1。右列每行一个节点，无分行选项按钮。
        var rowBtn = go.GetComponent<Button>() ?? go.AddComponent<Button>();
        rowBtn.targetGraphic = go.GetComponent<Image>();
        rowBtn.onClick.RemoveAllListeners();
        rowBtn.onClick.AddListener(() => OnClickRightNode(visualIndex0));
        v.button = rowBtn;

        // 单选项节点：把唯一选项显示到第 1 个 Opt 槽（图标+名）。JobChoice 不在此显示，靠弹层选。
        var singleOpt = (!node.IsJobChoice && node.options != null && node.options.Length > 0) ? node.options[0] : null;
        for (int o = 0; o < 3; o++)
        {
            var opt = go.transform.Find("Opt_" + o);
            if (opt == null) continue;
            bool show = o == 0 && singleOpt != null;
            opt.gameObject.SetActive(show);
            if (show)
            {
                var label = opt.Find("Label")?.GetComponent<Text>();
                var iconT = opt.Find("Icon") ?? opt.Find("icon");
                var icon = iconT != null ? iconT.GetComponent<Image>() : null;
                if (label != null) label.text = singleOpt.name;
                if (icon != null)
                {
                    var sp = TalentIcons.GetTalent(singleOpt.name);
                    if (sp != null) ApplySprite(icon, sp, true);
                }
            }
            // 整行点击即可，禁用子选项独立响应（避免点中选项区不触发整行）
            var ob = opt.GetComponent<Button>();
            if (ob != null) ob.enabled = false;
        }
        return v;
    }

    void EnsureVisibleTransform()
    {
        if (transform.localScale.sqrMagnitude < 0.0001f)
            transform.localScale = Vector3.one;
    }

    public void AutoBindFromHierarchy()
    {
        panelImage = FindImg("Panel");
        closeButton = transform.Find("Panel/TitleBar/CloseButton")?.GetComponent<Button>()
                      ?? transform.Find("Panel/CloseButton")?.GetComponent<Button>();
        titleText = FindTxt("Panel/TitleBar/TitleText");
        BindCurrencyTexts();
        stonePlusButton = transform.Find("Panel/ResourceRow/StonePlus")?.GetComponent<Button>()
                          ?? transform.Find("Panel/ResourceRow/GoldPanel/PlusButton")?.GetComponent<Button>();

        leftScroll = transform.Find("Panel/Columns/LeftColumn/LeftScroll")?.GetComponent<ScrollRect>();
        leftContent = transform.Find("Panel/Columns/LeftColumn/LeftScroll/Viewport/Content") as RectTransform;
        leftTipText = FindTxt("Panel/Columns/LeftColumn/LeftTip");
        leftNodeTemplate = transform.Find("LeftNodeTemplate")?.gameObject;

        rightScroll = transform.Find("Panel/Columns/RightColumn/RightScroll")?.GetComponent<ScrollRect>();
        rightContent = transform.Find("Panel/Columns/RightColumn/RightScroll/Viewport/Content") as RectTransform;
        rightTipText = FindTxt("Panel/Columns/RightColumn/RightTip");
        rightCostValueText = FindTxt("Panel/Columns/RightColumn/RightCostText")
                             ?? FindTxt("Panel/Columns/RightColumn/StoneBar/RightCostText");
        rightRowTemplate = transform.Find("RightRowTemplate")?.gameObject;
        rightExtraRowTemplate = transform.Find("RightRowTemplate (1)")?.gameObject;

        sumAttackText = FindTxt("Panel/Footer/SumAttack");
        sumHpText = FindTxt("Panel/Footer/SumHp");
        sumDefText = FindTxt("Panel/Footer/SumDef");
        sumCritText = FindTxt("Panel/Footer/SumCrit");
        sumAtkSpdText = FindTxt("Panel/Footer/SumAtkSpd");
        resetButton = transform.Find("Panel/Footer/ResetButton")?.GetComponent<Button>();
        resetButtonGray = transform.Find("Panel/Footer/ResetButton灰")?.GetComponent<Button>()
                          ?? transform.Find("Panel/Footer/ResetButtonGray")?.GetComponent<Button>();

        choicePopup = transform.Find("ChoicePopup")?.gameObject;
        choiceTitleText = FindTxt("ChoicePopup/Title")
                          ?? FindTxt("ChoicePopup/标头/Title");
        choiceDescText = FindTxt("ChoicePopup/Choice_0/Label");
        choiceCancelButton = transform.Find("ChoicePopup/Cancel")?.GetComponent<Button>();
        choiceConfirmButton = transform.Find("ChoicePopup/确定")?.GetComponent<Button>()
                              ?? transform.Find("ChoicePopup/Confirm")?.GetComponent<Button>();
        choiceOptionTemplate = transform.Find("ChoicePopup/Choice_0")?.gameObject;
        choiceButtons = new Button[3];
        choiceLabels = new Text[3];
        choiceIcons = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            choiceButtons[i] = transform.Find($"ChoicePopup/Choice_{i}")?.GetComponent<Button>();
            if (choiceButtons[i] != null)
            {
                choiceLabels[i] = choiceButtons[i].GetComponentInChildren<Text>(true);
                var iconT = choiceButtons[i].transform.Find("Icon") ?? choiceButtons[i].transform.Find("icon");
                choiceIcons[i] = iconT != null ? iconT.GetComponent<Image>() : null;
            }
        }
    }

    /// <summary>
    /// panelImage 已序列化时 Awake 会跳过 AutoBind，导致手做 GoldPanel 文案未挂上。
    /// </summary>
    void EnsureCurrencyBindings()
    {
        BindCurrencyTexts();
        WireGoldPlusIfNeeded();
    }

    void BindCurrencyTexts()
    {
        // 手做：ResourceRow/GoldText 常为隐藏白模；真金显示在 GoldPanel/GoldText
        if (goldText == null || !goldText.gameObject.activeInHierarchy)
        {
            goldText = FindTxt("Panel/ResourceRow/GoldPanel/GoldText")
                       ?? FindTxt("Panel/ResourceRow/GoldText")
                       ?? goldText;
        }
        if (stoneText == null || !stoneText.gameObject.activeInHierarchy)
        {
            stoneText = FindDeepTextByParentName("天赋石Panel", "GoldText")
                        ?? FindTxt("Panel/ResourceRow/StoneText")
                        ?? stoneText;
        }
        if (leftCostText == null)
        {
            leftCostText = FindTxt("Panel/Columns/LeftColumn/LeftCostText")
                           ?? FindTxt("Panel/Columns/LeftColumn/GoldBar/LeftCostText")
                           ?? FindTxt("Panel/ResourceRow/GoldPanel/LeftCostText");
        }
        rightCostValueText = rightCostValueText
                             ?? FindTxt("Panel/Columns/RightColumn/RightCostText")
                             ?? FindTxt("Panel/Columns/RightColumn/StoneBar/RightCostText");
    }

    void WireGoldPlusIfNeeded()
    {
        var plus = transform.Find("Panel/ResourceRow/GoldPanel/PlusButton")?.GetComponent<Button>();
        if (plus == null) return;
        plus.onClick.RemoveAllListeners();
        plus.onClick.AddListener(OnClickGoldPlusUpgrade);
    }

    void OnClickGoldPlusUpgrade()
    {
        var talents = GetTalents();
        int unlocked = TalentDefs.LeftUnlockedCount(talents);
        if (unlocked >= TalentDefs.Left.Length) return;
        OnClickLeft(unlocked);
    }

    Image FindImg(string path)
    {
        var t = transform.Find(path);
        return t != null ? t.GetComponent<Image>() : null;
    }

    Text FindTxt(string path)
    {
        var t = transform.Find(path);
        return t != null ? t.GetComponent<Text>() : null;
    }

    Text FindDeepTextByParentName(string parentName, string childName)
    {
        if (string.IsNullOrEmpty(parentName)) return null;
        var parent = FindDeepNamed(transform, parentName);
        if (parent == null) return null;
        var child = parent.Find(childName);
        return child != null ? child.GetComponent<Text>() : null;
    }

    static Transform FindDeepNamed(Transform root, string name)
    {
        if (root == null) return null;
        for (int i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            if (c.name == name) return c;
            var nested = FindDeepNamed(c, name);
            if (nested != null) return nested;
        }
        return null;
    }

    static Image FindDeepImage(Transform root, string name)
    {
        if (root == null) return null;
        for (int i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            if (c.name == name)
            {
                var img = c.GetComponent<Image>();
                if (img != null) return img;
            }
            var nested = FindDeepImage(c, name);
            if (nested != null) return nested;
        }
        return null;
    }

    /// <summary>编辑器首次建树；已换美术的预制体勿覆盖</summary>
    public void BuildHierarchyForPrefab()
    {
        LoadArtSprites();
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        var dim = CreateImage(transform, "Dim", new Color(0f, 0f, 0f, 0.62f));
        Stretch(dim.rectTransform);

        var panel = CreateImage(transform, "Panel", Color.white, sprPanelBg, false);
        SetAnchored(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(700f, 1240f));

        var titleBar = CreateRect(panel.transform, "TitleBar").GetComponent<RectTransform>();
        SetAnchored(titleBar, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -58f), new Vector2(280f, 52f));
        var title = CreateText(titleBar, "TitleText", "天赋", 36, new Color(1f, 0.93f, 0.72f));
        Stretch(title.rectTransform);

        var close = CreateImage(panel.transform, "CloseButton", Color.white, sprClose, true);
        SetAnchored(close.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-18f, -18f), new Vector2(64f, 64f));
        close.gameObject.AddComponent<Button>().targetGraphic = close;

        // Resource row
        var res = CreateRect(panel.transform, "ResourceRow");
        SetAnchored(res.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -118f), new Vector2(520f, 40f));
        var goldIcon = CreateImage(res.transform, "GoldIcon", Color.white, sprCheckOn, true);
        SetAnchored(goldIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(8f, 0f), new Vector2(32f, 32f));
        var goldTxt = CreateText(res.transform, "GoldText", "999999+", 24, new Color(1f, 0.95f, 0.7f));
        goldTxt.alignment = TextAnchor.MiddleLeft;
        SetAnchored(goldTxt.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(52f, 0f), new Vector2(140f, 36f));

        var stoneIcon = CreateImage(res.transform, "StoneIcon", Color.white, sprDiamondOn, true);
        SetAnchored(stoneIcon.rectTransform, new Vector2(0.55f, 0.5f), new Vector2(0.55f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(0f, 0f), new Vector2(36f, 36f));
        var stoneTxt = CreateText(res.transform, "StoneText", "999+", 24, new Color(0.9f, 0.8f, 1f));
        stoneTxt.alignment = TextAnchor.MiddleLeft;
        SetAnchored(stoneTxt.rectTransform, new Vector2(0.55f, 0.5f), new Vector2(0.55f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(44f, 0f), new Vector2(100f, 36f));
        var stonePlus = CreateImage(res.transform, "StonePlus", new Color(0.4f, 0.7f, 0.35f, 1f));
        SetAnchored(stonePlus.rectTransform, new Vector2(0.55f, 0.5f), new Vector2(0.55f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(150f, 0f), new Vector2(36f, 36f));
        stonePlus.gameObject.AddComponent<Button>().targetGraphic = stonePlus;
        var plusTxt = CreateText(stonePlus.transform, "Label", "+", 28, Color.white);
        Stretch(plusTxt.rectTransform);

        // Columns
        var columns = CreateRect(panel.transform, "Columns");
        var colRt = columns.GetComponent<RectTransform>();
        colRt.anchorMin = new Vector2(0f, 0f);
        colRt.anchorMax = new Vector2(1f, 1f);
        colRt.offsetMin = new Vector2(36f, 168f);
        colRt.offsetMax = new Vector2(-36f, -168f);

        BuildLeftColumn(columns.transform);
        BuildRightColumn(columns.transform);
        BuildFooter(panel.transform);
        BuildTemplates(transform);
        BuildChoicePopup(transform);

        AutoBindFromHierarchy();
        closeButton = panel.transform.Find("CloseButton")?.GetComponent<Button>();
        GameFonts.ApplyToHierarchy(transform);
    }

    void BuildLeftColumn(Transform columns)
    {
        var left = CreateImage(columns, "LeftColumn", new Color(1f, 1f, 1f, 0.04f));
        var lrt = left.rectTransform;
        lrt.anchorMin = new Vector2(0f, 0f);
        lrt.anchorMax = new Vector2(0.5f, 1f);
        lrt.offsetMin = new Vector2(0f, 0f);
        lrt.offsetMax = new Vector2(-4f, 0f);

        var head = CreateImage(left.transform, "Header", new Color(0.55f, 0.5f, 0.45f, 1f));
        SetAnchored(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -8f), new Vector2(260f, 40f));
        var headTxt = CreateText(head.transform, "Label", "属性天赋", 24, Color.white);
        Stretch(headTxt.rectTransform);

        var scrollGo = CreateRect(left.transform, "LeftScroll");
        var srt = scrollGo.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 0f);
        srt.anchorMax = new Vector2(1f, 1f);
        srt.offsetMin = new Vector2(4f, 50f);
        srt.offsetMax = new Vector2(-4f, -46f);
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        var viewport = CreateImage(scrollGo.transform, "Viewport", new Color(1f, 1f, 1f, 0.02f));
        Stretch(viewport.rectTransform);
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        var content = CreateRect(viewport.transform, "Content");
        var crt = content.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 1f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta = new Vector2(0f, 800f);
        scroll.viewport = viewport.rectTransform;
        scroll.content = crt;

        var goldBar = CreateImage(left.transform, "GoldBar", Color.white, sprGoldBar, true);
        SetAnchored(goldBar.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-10f, 8f), new Vector2(90f, 36f));
        var tip = CreateText(left.transform, "LeftTip", "消耗金币解锁属性天赋", 16, new Color(0.45f, 0.22f, 0.12f));
        tip.alignment = TextAnchor.MiddleLeft;
        SetAnchored(tip.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f),
            new Vector2(10f, 8f), new Vector2(-100f, 32f));
        var cost = CreateText(goldBar.transform, "LeftCostText", "0", 18, new Color(1f, 0.93f, 0.55f));
        cost.alignment = TextAnchor.MiddleCenter;
        Stretch(cost.rectTransform);
    }

    void BuildRightColumn(Transform columns)
    {
        var right = CreateImage(columns, "RightColumn", new Color(1f, 1f, 1f, 0.04f));
        var rrt = right.rectTransform;
        rrt.anchorMin = new Vector2(0.5f, 0f);
        rrt.anchorMax = new Vector2(1f, 1f);
        rrt.offsetMin = new Vector2(4f, 0f);
        rrt.offsetMax = new Vector2(0f, 0f);

        var head = CreateImage(right.transform, "Header", new Color(0.45f, 0.35f, 0.6f, 1f));
        SetAnchored(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -8f), new Vector2(280f, 40f));
        var headTxt = CreateText(head.transform, "Label", "辅助/专精天赋", 22, Color.white);
        Stretch(headTxt.rectTransform);

        var scrollGo = CreateRect(right.transform, "RightScroll");
        var srt = scrollGo.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 0f);
        srt.anchorMax = new Vector2(1f, 1f);
        srt.offsetMin = new Vector2(4f, 50f);
        srt.offsetMax = new Vector2(-4f, -46f);
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        var viewport = CreateImage(scrollGo.transform, "Viewport", new Color(1f, 1f, 1f, 0.02f));
        Stretch(viewport.rectTransform);
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        var content = CreateRect(viewport.transform, "Content");
        var crt = content.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 1f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta = new Vector2(0f, 800f);
        scroll.viewport = viewport.rectTransform;
        scroll.content = crt;

        var stoneBar = CreateImage(right.transform, "StoneBar", Color.white, sprStoneBar, true);
        SetAnchored(stoneBar.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-10f, 8f), new Vector2(90f, 36f));
        var tip = CreateText(right.transform, "RightTip", "消耗天赋石解锁辅助/专精天赋", 15, new Color(0.32f, 0.18f, 0.5f));
        tip.alignment = TextAnchor.MiddleLeft;
        SetAnchored(tip.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f),
            new Vector2(10f, 8f), new Vector2(-100f, 32f));
        var cost = CreateText(stoneBar.transform, "RightCostText", "0", 18, new Color(0.92f, 0.82f, 1f));
        cost.alignment = TextAnchor.MiddleCenter;
        Stretch(cost.rectTransform);
    }

    void BuildFooter(Transform panel)
    {
        var footer = CreateImage(panel, "Footer", Color.white, sprFooter, false);
        footer.type = Image.Type.Sliced;
        SetAnchored(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 28f), new Vector2(620f, 118f));

        var label = CreateText(footer.transform, "SumLabel", "已获得属性加成", 20, new Color(0.75f, 0.95f, 0.7f));
        label.alignment = TextAnchor.MiddleLeft;
        SetAnchored(label.rectTransform, new Vector2(0f, 1f), new Vector2(0.55f, 1f), new Vector2(0f, 1f),
            new Vector2(16f, -8f), new Vector2(0f, 28f));

        CreateSum(footer.transform, "SumAttack", "+0", 0f);
        CreateSum(footer.transform, "SumHp", "+0", 0.18f);
        CreateSum(footer.transform, "SumDef", "+0", 0.36f);
        CreateSum(footer.transform, "SumCrit", "+0%", 0.54f);
        CreateSum(footer.transform, "SumAtkSpd", "+0%", 0.72f);

        var reset = CreateImage(footer.transform, "ResetButton", Color.white, sprReset, false);
        SetAnchored(reset.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-12f, -6f), new Vector2(196f, 70f));
        reset.gameObject.AddComponent<Button>().targetGraphic = reset;
        var resetTxt = CreateText(reset.transform, "Label", "重置天赋", 24, new Color(1f, 0.92f, 0.75f));
        Stretch(resetTxt.rectTransform);
    }

    static void CreateSum(Transform footer, string name, string value, float xNorm)
    {
        var t = CreateText(footer, name, value, 20, new Color(0.7f, 0.95f, 0.65f));
        t.alignment = TextAnchor.MiddleCenter;
        SetAnchored(t.rectTransform, new Vector2(xNorm, 0f), new Vector2(xNorm + 0.16f, 0.55f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
    }

    void BuildTemplates(Transform root)
    {
        var left = CreateImage(root, "LeftNodeTemplate", Color.white, sprLeftCard, false);
        left.gameObject.SetActive(false);
        SetAnchored(left.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            Vector2.zero, new Vector2(0f, 100f));
        left.gameObject.AddComponent<Button>().targetGraphic = left;

        var line = CreateImage(left.transform, "Line", Color.white, sprLeftLinkOn, false);
        SetAnchored(line.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 1f),
            new Vector2(46f, -28f), new Vector2(12f, 56f));
        line.raycastTarget = false;

        var icon = CreateImage(left.transform, "Icon", Color.white, sprLeftHexOff, true);
        // 左对齐内缩，避免被卡片 Mask/边框裁掉一半
        SetAnchored(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(52f, 0f), new Vector2(56f, 56f));
        icon.raycastTarget = false;
        icon.preserveAspect = true;
        var name = CreateText(left.transform, "NameText", "力量 I", 22, new Color(0.25f, 0.15f, 0.1f));
        name.alignment = TextAnchor.MiddleLeft;
        SetAnchored(name.rectTransform, new Vector2(0f, 0.52f), new Vector2(1f, 1f), new Vector2(0f, 0.5f),
            new Vector2(88f, 0f), new Vector2(-128f, 0f));
        var effect = CreateText(left.transform, "EffectText", "攻击 +3", 18, new Color(0.4f, 0.28f, 0.18f));
        effect.alignment = TextAnchor.MiddleLeft;
        SetAnchored(effect.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.52f), new Vector2(0f, 0.5f),
            new Vector2(88f, 0f), new Vector2(-128f, 0f));
        var check = CreateImage(left.transform, "Check", Color.white, sprCheckOff, true);
        SetAnchored(check.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-10f, 0f), new Vector2(32f, 28f));
        check.raycastTarget = false;

        var right = CreateImage(root, "RightRowTemplate", Color.white, sprRightCard, false);
        right.gameObject.SetActive(false);
        SetAnchored(right.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            Vector2.zero, new Vector2(0f, 118f));

        var rline = CreateImage(right.transform, "Line", Color.white, sprRightLinkOn, false);
        SetAnchored(rline.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 1f),
            new Vector2(18f, -16f), new Vector2(10f, 72f));
        rline.raycastTarget = false;
        var diamond = CreateImage(right.transform, "Diamond", Color.white, sprDiamondOff, true);
        SetAnchored(diamond.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(18f, 8f), new Vector2(30f, 26f));
        diamond.raycastTarget = false;

        var title = CreateText(right.transform, "TitleText", "武器专精", 18, new Color(0.22f, 0.12f, 0.32f));
        title.alignment = TextAnchor.MiddleLeft;
        SetAnchored(title.rectTransform, new Vector2(0f, 1f), new Vector2(0.72f, 1f), new Vector2(0f, 1f),
            new Vector2(38f, -4f), new Vector2(0f, 26f));
        var cost = CreateText(right.transform, "CostText", "12", 16, new Color(0.42f, 0.22f, 0.62f));
        cost.alignment = TextAnchor.MiddleRight;
        SetAnchored(cost.rectTransform, new Vector2(0.72f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-36f, -4f), new Vector2(0f, 26f));
        var lockImg = CreateImage(right.transform, "Lock", Color.white, sprLock, true);
        SetAnchored(lockImg.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-6f, 8f), new Vector2(28f, 40f));
        lockImg.raycastTarget = false;

        for (int o = 0; o < 3; o++)
        {
            var opt = CreateImage(right.transform, "Opt_" + o, Color.white, o == 1 ? sprRightHexAlt : sprRightHex, true);
            SetAnchored(opt.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(40f + o * 68f, 8f), new Vector2(62f, 62f));
            opt.gameObject.AddComponent<Button>().targetGraphic = opt;
            var oi = CreateImage(opt.transform, "Icon", new Color(1f, 1f, 1f, 0.15f));
            SetAnchored(oi.rectTransform, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(36f, 36f));
            oi.raycastTarget = false;
            var ol = CreateText(opt.transform, "Label", "选项", 12, Color.white);
            SetAnchored(ol.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.32f), new Vector2(0.5f, 0f),
                Vector2.zero, Vector2.zero);
        }
    }

    void BuildChoicePopup(Transform root)
    {
        var pop = CreateImage(root, "ChoicePopup", new Color(0f, 0f, 0f, 0.65f));
        Stretch(pop.rectTransform);
        pop.gameObject.SetActive(false);
        var box = CreateImage(pop.transform, "Box", new Color(0.93f, 0.88f, 0.78f, 1f));
        SetAnchored(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(480f, 420f));
        var title = CreateText(pop.transform, "Title", "选择天赋", 28, new Color(0.25f, 0.15f, 0.1f));
        // Title under popup root for AutoBind path ChoicePopup/Title
        SetAnchored(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 160f), new Vector2(400f, 40f));
        for (int i = 0; i < 3; i++)
        {
            var c = CreateImage(pop.transform, "Choice_" + i, new Color(0.55f, 0.35f, 0.55f, 1f));
            SetAnchored(c.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 70f - i * 90f), new Vector2(400f, 72f));
            c.gameObject.AddComponent<Button>().targetGraphic = c;
            var lab = CreateText(c.transform, "Label", "选项", 22, Color.white);
            Stretch(lab.rectTransform);
        }
        var cancel = CreateImage(pop.transform, "Cancel", new Color(0.45f, 0.3f, 0.2f, 1f));
        SetAnchored(cancel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -170f), new Vector2(200f, 48f));
        cancel.gameObject.AddComponent<Button>().targetGraphic = cancel;
        var ct = CreateText(cancel.transform, "Label", "取消", 24, Color.white);
        Stretch(ct.rectTransform);
    }

    static GameObject CreateRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static Image CreateImage(Transform parent, string name, Color color)
    {
        return CreateImage(parent, name, color, null, false);
    }

    static Image CreateImage(Transform parent, string name, Color color, Sprite sprite, bool preserveAspect)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = sprite != null ? Color.white : color;
        img.preserveAspect = preserveAspect;
        img.raycastTarget = true;
        return img;
    }

    void LoadArtSprites()
    {
        EnsureSprite(ref sprPanelBg, "天赋_0020_bg");
        EnsureSprite(ref sprClose, "天赋_0007_关闭");
        EnsureSprite(ref sprLeftCard, "天赋_0006_属性底");
        EnsureSprite(ref sprRightCard, "天赋_0000_技能底");
        EnsureSprite(ref sprFooter, "天赋_0000s_0002_底条");
        EnsureSprite(ref sprReset, "重置天赋亮");
        EnsureSprite(ref sprGoldBar, "天赋_0001_金币升级");
        EnsureSprite(ref sprStoneBar, "天赋_0013_天赋石升级");
        EnsureSprite(ref sprLeftHexOff, "天赋_0004_基础属性未解锁");
        EnsureSprite(ref sprLeftHexOn, "天赋_0005_基础属性解锁");
        EnsureSprite(ref sprCheckOff, "天赋_0011_不可升级");
        EnsureSprite(ref sprCheckOn, "天赋_0012_可升级");
        EnsureSprite(ref sprLeftLinkOff, "天赋_0009_链接2");
        EnsureSprite(ref sprLeftLinkOn, "天赋_0008_lianjie3");
        EnsureSprite(ref sprRightHex, "天赋_0002_图层-2");
        EnsureSprite(ref sprRightHexAlt, "天赋_0003_图层-3");
        EnsureSprite(ref sprDiamondOff, "天赋_0016_技能可用-拷贝");
        EnsureSprite(ref sprDiamondOn, "天赋_0018_技能可用");
        EnsureSprite(ref sprRightLinkOff, "天赋_0017_技能链接-拷贝");
        EnsureSprite(ref sprRightLinkOn, "天赋_0019_技能链接");
        EnsureSprite(ref sprLock, "图层 3");
        if (sprLock == null)
            sprLock = Resources.Load<Sprite>("UI/Common/锁");
#if UNITY_EDITOR
        if (sprLock == null)
            sprLock = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Common/锁.png");
#endif
        EnsureSprite(ref sprArrow, "天赋_0015_箭头");
    }

    static void EnsureSprite(ref Sprite field, string fileStem)
    {
        if (field != null) return;
        field = Resources.Load<Sprite>("UI/Talent/" + fileStem);
#if UNITY_EDITOR
        if (field == null)
            field = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Art/UI/Talent/" + fileStem + ".png");
#endif
    }

    static Text CreateText(Transform parent, string name, string content, int size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
        t.font = GameFonts.GetChinese();
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    static void SetAnchored(RectTransform rt, Vector2 amin, Vector2 amax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = amin;
        rt.anchorMax = amax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
