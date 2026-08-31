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

    const float LeftNodeH = 108f;
    const float RightRowH = 126f;

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
    readonly List<ChoiceRowView> _rightViews = new List<ChoiceRowView>();
    TalentSystem.Branch _pendingBranch = TalentSystem.Branch.Right;
    int _pendingRowIndex = -1;
    int _pendingSelectedOpt = -1;
    bool _wired;
    bool _listsBuilt;

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
    }

    class ChoiceRowView
    {
        public TalentSystem.Branch branch;
        public int dataIndex;
        public int visualIndex;
        public GameObject root;
        public Text titleText;
        public Text costText;
        public Image lockIcon;
        public Image diamond;
        public Image line;
        public GameObject redDot;
        public readonly List<Button> optionButtons = new List<Button>();
        public readonly List<Text> optionLabels = new List<Text>();
        public readonly List<Image> optionIcons = new List<Image>();
    }

    void Awake()
    {
        Instance = this;
        if (panelImage == null)
            AutoBindFromHierarchy();
        EnsureChoicePopupBindings();
        EnsureVisibleTransform();
        WireClicks();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Show()
    {
        EnsureVisibleTransform();
        EnsureChoicePopupBindings();
        if (!_wired) WireClicks();
        else WireChoiceConfirmIfNeeded();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        var canvas = UICanvasSetup.ApplyOn(gameObject, UICanvasSetup.ResolveUiCamera());
        if (canvas != null)
            UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownVeil, UICanvasSetup.ResolveUiCamera());
        EnsureLists();
        RefreshAll();
    }

    public void Hide()
    {
        CloseChoicePopup();
        gameObject.SetActive(false);
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
                v.check.gameObject.SetActive(true);
                ApplySprite(v.check, on ? sprCheckOn : (can ? sprCheckOn : sprCheckOff), true);
            }
            if (v.button != null) v.button.interactable = can;
            SetRowGray(v.root, locked);
            SetRowRedDot(v.root, ref v.redDot, can);
            if (v.line != null)
            {
                bool showLine = i < _leftViews.Count - 1;
                v.line.gameObject.SetActive(showLine);
                if (showLine) ApplySprite(v.line, on ? sprLeftLinkOn : sprLeftLinkOff, false);
            }
        }

        if (leftCostText != null)
            leftCostText.text = nextCost > 0 ? nextCost.ToString() : "0";
        if (leftTipText != null)
            leftTipText.text = "消耗金币解锁属性天赋";
    }

    void RefreshRight()
    {
        var talents = GetTalents();
        int leftUnlocked = TalentDefs.LeftUnlockedCount(talents);
        int rightUnlocked = TalentDefs.RightUnlockedCount(talents);
        int nextCost = rightUnlocked < TalentDefs.Right.Length
            ? TalentDefs.Right[rightUnlocked].stoneCost
            : 0;

        for (int i = 0; i < _rightViews.Count; i++)
            RefreshOneChoiceRow(_rightViews[i], talents, leftUnlocked, rightUnlocked);

        if (rightCostValueText != null)
            rightCostValueText.text = nextCost > 0 ? nextCost.ToString() : "0";
        if (rightTipText != null)
            rightTipText.text = "消耗天赋石解锁辅助/专精天赋";
    }

    void RefreshOneChoiceRow(ChoiceRowView v, Dictionary<string, int> talents, int leftUnlocked, int rightUnlocked)
    {
        var def = GetRowDef(v);
        if (def == null) return;

        int selLv = 0;
        bool picked = talents != null && talents.TryGetValue(def.id, out selLv) && selLv > 0;
        bool canPick = CanPickRow(v, talents, leftUnlocked, rightUnlocked, picked);
        bool locked = !picked && !canPick;
        bool canAfford = canPick && SaveSystem.Instance?.Data != null &&
                         SaveSystem.Instance.Data.talentPoints >= def.stoneCost;

        if (v.titleText != null) v.titleText.text = def.groupName;
        if (v.costText != null) v.costText.text = def.stoneCost.ToString();
        if (v.lockIcon != null)
        {
            v.lockIcon.gameObject.SetActive(locked);
            ApplySprite(v.lockIcon, sprLock, true);
        }
        if (v.diamond != null)
            ApplySprite(v.diamond, picked || canPick ? sprDiamondOn : sprDiamondOff, true);
            if (v.line != null)
            {
                // 第一行不画竖线，从第二行开始有
                bool showLine = v.visualIndex >= 1 && v.visualIndex < _rightViews.Count - 1;
                v.line.gameObject.SetActive(showLine);
                if (showLine)
                    ApplySprite(v.line, picked || canPick ? sprRightLinkOn : sprRightLinkOff, false);
            }

        SetRowGray(v.root, locked);
        SetRowRedDot(v.root, ref v.redDot, canAfford);

        int selectedOpt = picked ? selLv : 0;
        for (int o = 0; o < v.optionButtons.Count; o++)
        {
            bool has = o < def.options.Length;
            var btnGo = v.optionButtons[o] != null ? v.optionButtons[o].gameObject : null;
            if (!has) { if (btnGo != null) btnGo.SetActive(false); continue; }

            bool isSelected = picked && selectedOpt == o + 1;
            bool showOpt = !picked || isSelected;
            if (btnGo != null) btnGo.SetActive(showOpt);

            if (v.optionLabels[o] != null)
                v.optionLabels[o].text = def.options[o].name;
            if (v.optionIcons[o] != null)
            {
                var sp = TalentIcons.GetTalent(def.options[o].name);
                if (sp != null) ApplySprite(v.optionIcons[o], sp, true);
                v.optionIcons[o].color = locked ? new Color(0.5f, 0.5f, 0.5f, 1f)
                    : (canPick ? Color.white : (isSelected ? Color.white : new Color(0.65f, 0.65f, 0.65f, 1f)));
            }
            if (v.optionButtons[o] != null)
            {
                v.optionButtons[o].interactable = canPick;
                var bg = v.optionButtons[o].targetGraphic as Image;
                if (bg != null)
                    ApplySprite(bg, o == 1 ? sprRightHexAlt : sprRightHex, true);
            }
        }
    }

    static bool CanPickRow(ChoiceRowView v, Dictionary<string, int> talents, int leftUnlocked, int rightUnlocked, bool picked)
    {
        if (picked) return false;
        var def = GetRowDef(v);
        if (def == null || leftUnlocked < def.requireLeftIndex) return false;
        if (v.branch == TalentSystem.Branch.RightExtra) return true;
        return v.dataIndex - 1 == rightUnlocked;
    }

    static TalentDefs.ChoiceNode GetRowDef(ChoiceRowView v)
    {
        if (v.branch == TalentSystem.Branch.RightExtra) return TalentDefs.RightExtra;
        return TalentDefs.GetRight(v.dataIndex);
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
            if (TalentDefs.RightExtra != null &&
                talents.TryGetValue(TalentDefs.RightExtra.id, out int c1) && c1 > 0)
                AccumulateChoiceEffect(TalentDefs.RightExtra.options, c1, ref atk, ref hp, ref def, ref crit, ref spd);
            for (int i = 0; i < TalentDefs.Right.Length; i++)
            {
                if (!talents.TryGetValue(TalentDefs.Right[i].id, out int opt) || opt <= 0) continue;
                AccumulateChoiceEffect(TalentDefs.Right[i].options, opt, ref atk, ref hp, ref def, ref crit, ref spd);
            }
        }

        if (sumAttackText != null) sumAttackText.text = "+" + atk.ToString("0");
        if (sumHpText != null) sumHpText.text = "+" + hp.ToString("0");
        if (sumDefText != null) sumDefText.text = "+" + def.ToString("0");
        if (sumCritText != null) sumCritText.text = "+" + crit.ToString("0.##") + "%";
        if (sumAtkSpdText != null) sumAtkSpdText.text = "+" + spd.ToString("0.##") + "%";
    }

    static void AccumulateChoiceEffect(TalentDefs.ChoiceOption[] options, int opt,
        ref float atk, ref float hp, ref float def, ref float crit, ref float spd)
    {
        if (options == null || opt <= 0 || opt > options.Length) return;
        var e = options[opt - 1].effect;
        if (e == null) return;
        switch (e.kind)
        {
            case TalentDefs.AttrKind.Attack: atk += e.value; break;
            case TalentDefs.AttrKind.Hp: hp += e.value; break;
            case TalentDefs.AttrKind.Defense: def += e.value; break;
            case TalentDefs.AttrKind.CritRate: crit += e.value; break;
            case TalentDefs.AttrKind.AtkSpeed: spd += e.value; break;
        }
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
        SetGraphicAlpha(root, gray ? 0.38f : 1f);
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

    static TalentDefs.ChoiceNode GetRowDefByBranch(TalentSystem.Branch branch, int rowIndex0)
    {
        if (branch == TalentSystem.Branch.RightExtra) return TalentDefs.RightExtra;
        if (rowIndex0 < 0 || rowIndex0 >= TalentDefs.Right.Length) return null;
        return TalentDefs.Right[rowIndex0];
    }

    static int ChoiceDataIndex(TalentSystem.Branch branch, int rowIndex0)
    {
        return branch == TalentSystem.Branch.RightExtra ? 1 : rowIndex0 + 1;
    }

    void OnClickChoiceOption(TalentSystem.Branch branch, int rowIndex0, int optIndex0)
    {
        var def = GetRowDefByBranch(branch, rowIndex0);
        if (def?.options == null || def.options.Length == 0) return;

        var talents = GetTalents();
        if (talents != null && talents.TryGetValue(def.id, out int picked) && picked > 0)
            return;

        if (def.options.Length == 1)
        {
            int dataIndex = ChoiceDataIndex(branch, rowIndex0);
            if (TalentSystem.TryUnlockChoice(branch, dataIndex, 1, out string reason))
            {
                onRightChoiceRequested?.Invoke(dataIndex, 1);
                RefreshAll();
            }
            else Debug.Log("[TalentUI] " + reason);
            return;
        }

        OpenChoicePopup(branch, rowIndex0, optIndex0);
    }

    void OpenChoicePopup(TalentSystem.Branch branch, int rowIndex0, int initialOpt0)
    {
        EnsureChoicePopupBindings();
        WireChoiceConfirmIfNeeded();

        _pendingBranch = branch;
        _pendingRowIndex = rowIndex0;
        _pendingSelectedOpt = initialOpt0;

        var def = GetRowDefByBranch(branch, rowIndex0);

        if (choicePopup != null) choicePopup.SetActive(true);
        if (choiceTitleText != null) choiceTitleText.text = def.groupName;

        EnsureChoicePopupOptions(def.options.Length);
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            bool on = i < def.options.Length;
            if (choiceButtons[i] != null) choiceButtons[i].gameObject.SetActive(on);
            if (!on) continue;
            if (choiceLabels[i] != null)
                choiceLabels[i].text = def.options[i].name;
            if (choiceIcons[i] != null)
            {
                var sp = TalentIcons.GetTalent(def.options[i].name);
                if (sp != null) ApplySprite(choiceIcons[i], sp, true);
            }
        }
        SelectChoicePopupOption(initialOpt0);
    }

    void EnsureChoicePopupOptions(int count)
    {
        if (choiceOptionTemplate == null && choiceButtons[0] != null)
            choiceOptionTemplate = choiceButtons[0].gameObject;

        // 弹层里最多 3 个选项；不足时隐藏多余按钮
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (choiceButtons[i] == null && choiceOptionTemplate != null && i > 0)
            {
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
        if (_pendingRowIndex < 0) return;
        _pendingSelectedOpt = optIndex0;
        var def = GetRowDefByBranch(_pendingBranch, _pendingRowIndex);
        if (optIndex0 < 0 || def == null || optIndex0 >= def.options.Length) return;

        if (choiceDescText != null)
            choiceDescText.text = def.options[optIndex0].effect.display;

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (choiceButtons[i] == null || !choiceButtons[i].gameObject.activeSelf) continue;
            SetGraphicAlpha(choiceButtons[i].gameObject, i == optIndex0 ? 1f : 0.55f);
        }
    }

    void ConfirmChoicePopup()
    {
        if (_pendingRowIndex < 0 || _pendingSelectedOpt < 0) return;
        int dataIndex = ChoiceDataIndex(_pendingBranch, _pendingRowIndex);
        if (!TalentSystem.TryUnlockChoice(_pendingBranch, dataIndex, _pendingSelectedOpt + 1, out string reason))
        {
            Debug.Log("[TalentUI] " + reason);
            return;
        }
        onRightChoiceRequested?.Invoke(dataIndex, _pendingSelectedOpt + 1);
        CloseChoicePopup();
        RefreshAll();
    }

    void CloseChoicePopup()
    {
        if (choicePopup != null) choicePopup.SetActive(false);
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

        for (int i = 0; i < TalentDefs.Left.Length; i++)
        {
            var go = Instantiate(leftNodeTemplate, leftContent);
            go.name = "LeftNode_" + (i + 1);
            go.SetActive(true);
            var view = BindLeftNode(go, i);
            _leftViews.Add(view);
            int idx = i;
            view.button?.onClick.AddListener(() => OnClickLeft(idx));
        }
        float leftH = TalentDefs.Left.Length * LeftNodeH + 20f;
        leftContent.sizeDelta = new Vector2(0f, leftH);
        // 打开时滚到底：最先开启的（力量 I）在最下面
        if (leftScroll != null)
            leftScroll.verticalNormalizedPosition = 0f;

        int visualRow = 0;
        // 右侧顺序：先战斗入门等主列，流派选择放到倒数第二行
        for (int i = 0; i < TalentDefs.Right.Length; i++)
        {
            // 在倒数第二行插入流派选择
            if (TalentDefs.RightExtra != null && i == Mathf.Max(0, TalentDefs.Right.Length - 1))
            {
                var extraTpl = rightExtraRowTemplate != null ? rightExtraRowTemplate : rightRowTemplate;
                var extraGo = Instantiate(extraTpl, rightContent);
                extraGo.name = "RightExtraRow";
                extraGo.SetActive(true);
                var extraView = BindChoiceRow(extraGo, visualRow, TalentSystem.Branch.RightExtra, 1);
                _rightViews.Add(extraView);
                for (int o = 0; o < extraView.optionButtons.Count; o++)
                {
                    int oi = o;
                    extraView.optionButtons[o]?.onClick.AddListener(() =>
                        OnClickChoiceOption(TalentSystem.Branch.RightExtra, 0, oi));
                }
                visualRow++;
            }

            var go = Instantiate(rightRowTemplate, rightContent);
            go.name = "RightRow_" + (i + 1);
            go.SetActive(true);
            var view = BindChoiceRow(go, visualRow, TalentSystem.Branch.Right, i + 1);
            _rightViews.Add(view);
            int ri = i;
            for (int o = 0; o < view.optionButtons.Count; o++)
            {
                int oi = o;
                view.optionButtons[o]?.onClick.AddListener(() =>
                    OnClickChoiceOption(TalentSystem.Branch.Right, ri, oi));
            }
            visualRow++;
        }
        // 若主列为空仍要显示流派
        if (TalentDefs.RightExtra != null && TalentDefs.Right.Length == 0)
        {
            var extraTpl = rightExtraRowTemplate != null ? rightExtraRowTemplate : rightRowTemplate;
            var extraGo = Instantiate(extraTpl, rightContent);
            extraGo.name = "RightExtraRow";
            extraGo.SetActive(true);
            var extraView = BindChoiceRow(extraGo, visualRow, TalentSystem.Branch.RightExtra, 1);
            _rightViews.Add(extraView);
            for (int o = 0; o < extraView.optionButtons.Count; o++)
            {
                int oi = o;
                extraView.optionButtons[o]?.onClick.AddListener(() =>
                    OnClickChoiceOption(TalentSystem.Branch.RightExtra, 0, oi));
            }
            visualRow++;
        }
        float rightH = visualRow * RightRowH + 20f;
        rightContent.sizeDelta = new Vector2(0f, rightH);

        WireScroll(leftScroll, leftContent);
        WireScroll(rightScroll, rightContent);

        _listsBuilt = true;
        WireClicks();
        GameFonts.ApplyToHierarchy(transform);
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
        var def = TalentDefs.Left[index0];
        if (v.nameText != null) v.nameText.text = def.name;
        if (v.effectText != null) v.effectText.text = def.effect.display;
        if (v.icon != null)
        {
            var sp = TalentIcons.GetLeftAttr(index0 % 5);
            if (sp != null) ApplySprite(v.icon, sp, true);
        }
        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, LeftNodeH - 6f);
            // 自下而上：index0（力量 I，最先开）在最底
            int fromTop = TalentDefs.Left.Length - 1 - index0;
            rt.anchoredPosition = new Vector2(0f, -fromTop * LeftNodeH - 4f);
        }
        return v;
    }

    ChoiceRowView BindChoiceRow(GameObject go, int visualIndex0, TalentSystem.Branch branch, int dataIndex1Based)
    {
        var def = branch == TalentSystem.Branch.RightExtra
            ? TalentDefs.RightExtra
            : TalentDefs.GetRight(dataIndex1Based);
        var v = new ChoiceRowView
        {
            branch = branch,
            dataIndex = dataIndex1Based,
            visualIndex = visualIndex0,
            root = go
        };
        v.titleText = go.transform.Find("TitleText")?.GetComponent<Text>();
        v.costText = go.transform.Find("CostText")?.GetComponent<Text>();
        v.lockIcon = go.transform.Find("Lock")?.GetComponent<Image>();
        v.diamond = go.transform.Find("Diamond")?.GetComponent<Image>();
        v.line = go.transform.Find("Line")?.GetComponent<Image>();
        if (def == null) return v;
        if (v.titleText != null) v.titleText.text = def.groupName;
        if (v.costText != null) v.costText.text = def.stoneCost.ToString();

        for (int o = 0; o < 3; o++)
        {
            var opt = go.transform.Find("Opt_" + o);
            if (opt == null) continue;
            var btn = opt.GetComponent<Button>() ?? opt.gameObject.AddComponent<Button>();
            var label = opt.Find("Label")?.GetComponent<Text>();
            var iconT = opt.Find("Icon") ?? opt.Find("icon");
            var icon = iconT != null ? iconT.GetComponent<Image>() : null;
            v.optionButtons.Add(btn);
            v.optionLabels.Add(label);
            v.optionIcons.Add(icon);
            bool has = o < def.options.Length;
            opt.gameObject.SetActive(has);
            if (has && label != null) label.text = def.options[o].name;
            if (has && icon != null)
            {
                var sp = TalentIcons.GetTalent(def.options[o].name);
                if (sp != null) ApplySprite(icon, sp, true);
            }
        }

        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, RightRowH - 8f);
            rt.anchoredPosition = new Vector2(0f, -visualIndex0 * RightRowH - 4f);
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
        goldText = FindTxt("Panel/ResourceRow/GoldText");
        stoneText = FindTxt("Panel/ResourceRow/StoneText");
        stonePlusButton = transform.Find("Panel/ResourceRow/StonePlus")?.GetComponent<Button>();

        leftScroll = transform.Find("Panel/Columns/LeftColumn/LeftScroll")?.GetComponent<ScrollRect>();
        leftContent = transform.Find("Panel/Columns/LeftColumn/LeftScroll/Viewport/Content") as RectTransform;
        leftTipText = FindTxt("Panel/Columns/LeftColumn/LeftTip");
        leftCostText = FindTxt("Panel/Columns/LeftColumn/LeftCostText")
                       ?? FindTxt("Panel/Columns/LeftColumn/GoldBar/LeftCostText");
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
#if UNITY_EDITOR
        if (sprPanelBg == null) sprPanelBg = Ed("天赋_0020_bg.png");
        if (sprClose == null) sprClose = Ed("天赋_0007_关闭.png");
        if (sprLeftCard == null) sprLeftCard = Ed("天赋_0006_属性底.png");
        if (sprRightCard == null) sprRightCard = Ed("天赋_0000_技能底.png");
        if (sprFooter == null) sprFooter = Ed("天赋_0000s_0002_底条.png");
        if (sprReset == null) sprReset = Ed("天赋_0000s_0001_重置天赋亮.png");
        if (sprGoldBar == null) sprGoldBar = Ed("天赋_0001_金币升级.png");
        if (sprStoneBar == null) sprStoneBar = Ed("天赋_0013_天赋石升级.png");
        if (sprLeftHexOff == null) sprLeftHexOff = Ed("天赋_0004_基础属性未解锁.png");
        if (sprLeftHexOn == null) sprLeftHexOn = Ed("天赋_0005_基础属性解锁.png");
        if (sprCheckOff == null) sprCheckOff = Ed("天赋_0011_不可升级.png");
        if (sprCheckOn == null) sprCheckOn = Ed("天赋_0012_可升级.png");
        if (sprLeftLinkOff == null) sprLeftLinkOff = Ed("天赋_0009_链接2.png");
        if (sprLeftLinkOn == null) sprLeftLinkOn = Ed("天赋_0008_lianjie3.png");
        if (sprRightHex == null) sprRightHex = Ed("天赋_0002_图层-2.png");
        if (sprRightHexAlt == null) sprRightHexAlt = Ed("天赋_0003_图层-3.png");
        if (sprDiamondOff == null) sprDiamondOff = Ed("天赋_0016_技能可用-拷贝.png");
        if (sprDiamondOn == null) sprDiamondOn = Ed("天赋_0018_技能可用.png");
        if (sprRightLinkOff == null) sprRightLinkOff = Ed("天赋_0017_技能链接-拷贝.png");
        if (sprRightLinkOn == null) sprRightLinkOn = Ed("天赋_0019_技能链接.png");
        if (sprLock == null) sprLock = Ed("图层 4.png");
        if (sprArrow == null) sprArrow = Ed("天赋_0015_箭头.png");
#endif
    }

#if UNITY_EDITOR
    static Sprite Ed(string fileName)
    {
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Talent/" + fileName);
    }
#endif

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
