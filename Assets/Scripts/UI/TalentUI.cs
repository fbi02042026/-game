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

    const float LeftNodeH = 86f;
    const float RightRowH = 108f;

    [Header("壳")]
    public Image panelImage;
    public Button closeButton;
    public Text titleText;
    public Text goldText;
    public Text stoneText;
    public Button stonePlusButton;

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

    [Header("底栏")]
    public Text sumAttackText;
    public Text sumHpText;
    public Text sumDefText;
    public Text sumCritText;
    public Text sumAtkSpdText;
    public Button resetButton;

    [Header("选择弹层")]
    public GameObject choicePopup;
    public Text choiceTitleText;
    public Button[] choiceButtons = new Button[3];
    public Text[] choiceLabels = new Text[3];
    public Button choiceCancelButton;

    [Header("事件（对接用）")]
    public UnityEvent onClosed;
    public UnityEvent<int> onLeftUnlockRequested;   // L index 1..40
    public UnityEvent<int, int> onRightChoiceRequested; // R index, option index
    public UnityEvent onResetRequested;

    readonly List<LeftNodeView> _leftViews = new List<LeftNodeView>();
    readonly List<RightRowView> _rightViews = new List<RightRowView>();
    int _pendingRightIndex = -1;
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
    }

    class RightRowView
    {
        public int index;
        public GameObject root;
        public Text titleText;
        public Text costText;
        public Image lockIcon;
        public readonly List<Button> optionButtons = new List<Button>();
        public readonly List<Text> optionLabels = new List<Text>();
        public readonly List<Image> optionIcons = new List<Image>();
    }

    void Awake()
    {
        Instance = this;
        if (panelImage == null)
            AutoBindFromHierarchy();
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
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        EnsureLists();
        RefreshAll();
    }

    public void Hide()
    {
        if (choicePopup != null) choicePopup.SetActive(false);
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
            nextCost = TalentDefs.Left[unlocked].goldCost;

        for (int i = 0; i < _leftViews.Count; i++)
        {
            var v = _leftViews[i];
            var def = TalentDefs.Left[i];
            bool on = i < unlocked;
            bool can = i == unlocked;
            if (v.nameText != null) v.nameText.text = def.name;
            if (v.effectText != null) v.effectText.text = def.effect.display;
            if (v.check != null)
            {
                v.check.gameObject.SetActive(true);
                v.check.color = on ? new Color(0.25f, 0.75f, 0.35f, 1f) : new Color(0.55f, 0.55f, 0.55f, 0.85f);
            }
            if (v.button != null) v.button.interactable = can;
            SetGraphicAlpha(v.root, on || can ? 1f : 0.55f);
            if (v.line != null) v.line.gameObject.SetActive(i < _leftViews.Count - 1);
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
        int rightUnlocked = 0;
        for (int i = 0; i < TalentDefs.Right.Length; i++)
        {
            if (talents != null && talents.TryGetValue(TalentDefs.Right[i].id, out int lv) && lv > 0)
                rightUnlocked++;
            else break;
        }

        int nextCost = 0;
        if (rightUnlocked < TalentDefs.Right.Length)
            nextCost = TalentDefs.Right[rightUnlocked].stoneCost;

        for (int i = 0; i < _rightViews.Count; i++)
        {
            var v = _rightViews[i];
            var def = TalentDefs.Right[i];
            int selLv = 0;
            bool unlocked = talents != null && talents.TryGetValue(def.id, out selLv) && selLv > 0;
            bool canPick = !unlocked && i == rightUnlocked && leftUnlocked >= def.requireLeftIndex;

            if (v.titleText != null) v.titleText.text = def.groupName;
            if (v.costText != null) v.costText.text = def.stoneCost.ToString();
            if (v.lockIcon != null) v.lockIcon.gameObject.SetActive(!unlocked && !canPick);

            int selectedOpt = unlocked ? selLv : 0;
            for (int o = 0; o < v.optionButtons.Count; o++)
            {
                bool has = o < def.options.Length;
                if (v.optionButtons[o] != null)
                {
                    v.optionButtons[o].gameObject.SetActive(has);
                    v.optionButtons[o].interactable = canPick && has;
                }
                if (has && v.optionLabels[o] != null)
                    v.optionLabels[o].text = def.options[o].name;
                if (has && unlocked && selectedOpt == o + 1)
                    SetGraphicAlpha(v.optionButtons[o].gameObject, 1f);
                else if (has)
                    SetGraphicAlpha(v.optionButtons[o].gameObject, canPick ? 1f : 0.5f);
            }
        }

        if (rightCostValueText != null)
            rightCostValueText.text = nextCost > 0 ? nextCost.ToString() : "0";
        if (rightTipText != null)
            rightTipText.text = "消耗天赋石解锁辅助/专精天赋";
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
            for (int i = 0; i < TalentDefs.Right.Length; i++)
            {
                if (!talents.TryGetValue(TalentDefs.Right[i].id, out int opt) || opt <= 0) continue;
                var options = TalentDefs.Right[i].options;
                if (opt > options.Length) continue;
                var e = options[opt - 1].effect;
                switch (e.kind)
                {
                    case TalentDefs.AttrKind.Attack: atk += e.value; break;
                    case TalentDefs.AttrKind.Hp: hp += e.value; break;
                    case TalentDefs.AttrKind.Defense: def += e.value; break;
                    case TalentDefs.AttrKind.CritRate: crit += e.value; break;
                    case TalentDefs.AttrKind.AtkSpeed: spd += e.value; break;
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

    static string FormatCompact(long v)
    {
        if (v >= 1000000) return "999999+";
        if (v >= 10000) return (v / 1000) + "k+";
        return v.ToString();
    }

    static void SetGraphicAlpha(GameObject go, float a)
    {
        if (go == null) return;
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        cg.alpha = a;
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
            resetButton.onClick.AddListener(() =>
            {
                onResetRequested?.Invoke();
                Debug.Log("[TalentUI] 重置天赋（当前版本设计为不可重置，仅预留按钮）");
            });
        }
        if (choiceCancelButton != null)
        {
            choiceCancelButton.onClick.RemoveAllListeners();
            choiceCancelButton.onClick.AddListener(() =>
            {
                if (choicePopup != null) choicePopup.SetActive(false);
            });
        }
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            int opt = i;
            if (choiceButtons[i] == null) continue;
            choiceButtons[i].onClick.RemoveAllListeners();
            choiceButtons[i].onClick.AddListener(() => ConfirmRightChoice(opt));
        }
    }

    void OnClickLeft(int index0)
    {
        onLeftUnlockRequested?.Invoke(index0 + 1);
        Debug.Log($"[TalentUI] 请求解锁左侧 L{index0 + 1}（功能对接中）");
    }

    void OnClickRightOption(int rightIndex0, int optIndex0)
    {
        var def = TalentDefs.Right[rightIndex0];
        if (def.options == null || def.options.Length <= 1)
        {
            onRightChoiceRequested?.Invoke(rightIndex0 + 1, optIndex0 + 1);
            Debug.Log($"[TalentUI] 请求解锁右侧 R{rightIndex0 + 1} 选项{optIndex0 + 1}");
            return;
        }
        OpenChoicePopup(rightIndex0);
    }

    void OpenChoicePopup(int rightIndex0)
    {
        _pendingRightIndex = rightIndex0;
        var def = TalentDefs.Right[rightIndex0];
        if (choicePopup != null) choicePopup.SetActive(true);
        if (choiceTitleText != null) choiceTitleText.text = def.groupName;
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            bool on = i < def.options.Length;
            if (choiceButtons[i] != null) choiceButtons[i].gameObject.SetActive(on);
            if (on && choiceLabels[i] != null)
                choiceLabels[i].text = def.options[i].name + "\n" + def.options[i].effect.display;
        }
    }

    void ConfirmRightChoice(int optIndex0)
    {
        if (_pendingRightIndex < 0) return;
        onRightChoiceRequested?.Invoke(_pendingRightIndex + 1, optIndex0 + 1);
        Debug.Log($"[TalentUI] 选择 R{_pendingRightIndex + 1} 选项{optIndex0 + 1}（功能对接中）");
        if (choicePopup != null) choicePopup.SetActive(false);
        _pendingRightIndex = -1;
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

        leftNodeTemplate.SetActive(false);
        rightRowTemplate.SetActive(false);

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

        for (int i = 0; i < TalentDefs.Right.Length; i++)
        {
            var go = Instantiate(rightRowTemplate, rightContent);
            go.name = "RightRow_" + (i + 1);
            go.SetActive(true);
            var view = BindRightRow(go, i);
            _rightViews.Add(view);
            int ri = i;
            for (int o = 0; o < view.optionButtons.Count; o++)
            {
                int oi = o;
                view.optionButtons[o]?.onClick.AddListener(() => OnClickRightOption(ri, oi));
            }
        }
        float rightH = TalentDefs.Right.Length * RightRowH + 20f;
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
        // 内容比视口高才能滑
        scroll.verticalNormalizedPosition = 1f;
    }

    LeftNodeView BindLeftNode(GameObject go, int index0)
    {
        var v = new LeftNodeView { index = index0 + 1, root = go };
        v.button = go.GetComponent<Button>() ?? go.GetComponentInChildren<Button>(true);
        v.icon = go.transform.Find("Icon")?.GetComponent<Image>();
        v.nameText = go.transform.Find("NameText")?.GetComponent<Text>();
        v.effectText = go.transform.Find("EffectText")?.GetComponent<Text>();
        v.check = go.transform.Find("Check")?.GetComponent<Image>();
        v.line = go.transform.Find("Line")?.GetComponent<Image>();
        var def = TalentDefs.Left[index0];
        if (v.nameText != null) v.nameText.text = def.name;
        if (v.effectText != null) v.effectText.text = def.effect.display;
        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, LeftNodeH - 6f);
            rt.anchoredPosition = new Vector2(0f, -index0 * LeftNodeH - 4f);
        }
        return v;
    }

    RightRowView BindRightRow(GameObject go, int index0)
    {
        var v = new RightRowView { index = index0 + 1, root = go };
        v.titleText = go.transform.Find("TitleText")?.GetComponent<Text>();
        v.costText = go.transform.Find("CostText")?.GetComponent<Text>();
        v.lockIcon = go.transform.Find("Lock")?.GetComponent<Image>();
        var def = TalentDefs.Right[index0];
        if (v.titleText != null) v.titleText.text = def.groupName;
        if (v.costText != null) v.costText.text = def.stoneCost.ToString();

        for (int o = 0; o < 3; o++)
        {
            var opt = go.transform.Find("Opt_" + o);
            if (opt == null) continue;
            var btn = opt.GetComponent<Button>() ?? opt.gameObject.AddComponent<Button>();
            var label = opt.Find("Label")?.GetComponent<Text>();
            var icon = opt.Find("Icon")?.GetComponent<Image>();
            v.optionButtons.Add(btn);
            v.optionLabels.Add(label);
            v.optionIcons.Add(icon);
            bool has = o < def.options.Length;
            opt.gameObject.SetActive(has);
            if (has && label != null) label.text = def.options[o].name;
        }

        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, RightRowH - 8f);
            rt.anchoredPosition = new Vector2(0f, -index0 * RightRowH - 4f);
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
        leftCostText = FindTxt("Panel/Columns/LeftColumn/LeftCostText");
        leftNodeTemplate = transform.Find("LeftNodeTemplate")?.gameObject;

        rightScroll = transform.Find("Panel/Columns/RightColumn/RightScroll")?.GetComponent<ScrollRect>();
        rightContent = transform.Find("Panel/Columns/RightColumn/RightScroll/Viewport/Content") as RectTransform;
        rightTipText = FindTxt("Panel/Columns/RightColumn/RightTip");
        rightCostValueText = FindTxt("Panel/Columns/RightColumn/RightCostText");
        rightRowTemplate = transform.Find("RightRowTemplate")?.gameObject;

        sumAttackText = FindTxt("Panel/Footer/SumAttack");
        sumHpText = FindTxt("Panel/Footer/SumHp");
        sumDefText = FindTxt("Panel/Footer/SumDef");
        sumCritText = FindTxt("Panel/Footer/SumCrit");
        sumAtkSpdText = FindTxt("Panel/Footer/SumAtkSpd");
        resetButton = transform.Find("Panel/Footer/ResetButton")?.GetComponent<Button>();

        choicePopup = transform.Find("ChoicePopup")?.gameObject;
        choiceTitleText = FindTxt("ChoicePopup/Title");
        choiceCancelButton = transform.Find("ChoicePopup/Cancel")?.GetComponent<Button>();
        choiceButtons = new Button[3];
        choiceLabels = new Text[3];
        for (int i = 0; i < 3; i++)
        {
            choiceButtons[i] = transform.Find($"ChoicePopup/Choice_{i}")?.GetComponent<Button>();
            choiceLabels[i] = FindTxt($"ChoicePopup/Choice_{i}/Label");
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

    /// <summary>编辑器首次建树；已换美术的预制体勿覆盖</summary>
    public void BuildHierarchyForPrefab()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        var dim = CreateImage(transform, "Dim", new Color(0f, 0f, 0f, 0.55f));
        Stretch(dim.rectTransform);

        var panel = CreateImage(transform, "Panel", new Color(0.45f, 0.28f, 0.16f, 1f));
        SetAnchored(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(680f, 1100f));

        // Title
        var titleBar = CreateImage(panel.transform, "TitleBar", new Color(0.35f, 0.2f, 0.12f, 1f));
        SetAnchored(titleBar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -8f), new Vector2(420f, 56f));
        var title = CreateText(titleBar.transform, "TitleText", "天赋", 34, Color.white);
        Stretch(title.rectTransform);

        var close = CreateImage(titleBar.transform, "CloseButton", new Color(0.75f, 0.2f, 0.18f, 1f));
        SetAnchored(close.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(36f, 8f), new Vector2(48f, 48f));
        close.gameObject.AddComponent<Button>().targetGraphic = close;
        var closeX = CreateText(close.transform, "X", "X", 28, Color.white);
        Stretch(closeX.rectTransform);

        // Resource row
        var res = CreateRect(panel.transform, "ResourceRow");
        SetAnchored(res.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -78f), new Vector2(560f, 44f));
        var goldIcon = CreateImage(res.transform, "GoldIcon", new Color(0.95f, 0.8f, 0.25f, 1f));
        SetAnchored(goldIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(8f, 0f), new Vector2(36f, 36f));
        var goldTxt = CreateText(res.transform, "GoldText", "999999+", 24, new Color(1f, 0.95f, 0.7f));
        goldTxt.alignment = TextAnchor.MiddleLeft;
        SetAnchored(goldTxt.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(52f, 0f), new Vector2(140f, 36f));

        var stoneIcon = CreateImage(res.transform, "StoneIcon", new Color(0.55f, 0.35f, 0.85f, 1f));
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
        colRt.offsetMin = new Vector2(18f, 150f);
        colRt.offsetMax = new Vector2(-18f, -120f);

        BuildLeftColumn(columns.transform);
        BuildRightColumn(columns.transform);
        BuildFooter(panel.transform);
        BuildTemplates(transform);
        BuildChoicePopup(transform);

        AutoBindFromHierarchy();
        // Fix close button path: moved under TitleBar
        closeButton = titleBar.transform.Find("CloseButton")?.GetComponent<Button>();
        GameFonts.ApplyToHierarchy(transform);
    }

    void BuildLeftColumn(Transform columns)
    {
        var left = CreateImage(columns, "LeftColumn", new Color(0.91f, 0.86f, 0.75f, 1f));
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
        srt.offsetMin = new Vector2(8f, 48f);
        srt.offsetMax = new Vector2(-8f, -56f);
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

        var tip = CreateText(left.transform, "LeftTip", "消耗金币解锁属性天赋", 18, new Color(0.7f, 0.2f, 0.15f));
        tip.alignment = TextAnchor.MiddleLeft;
        SetAnchored(tip.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f),
            new Vector2(12f, 10f), new Vector2(-80f, 32f));
        var cost = CreateText(left.transform, "LeftCostText", "0", 20, new Color(0.85f, 0.65f, 0.15f));
        cost.alignment = TextAnchor.MiddleRight;
        SetAnchored(cost.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-16f, 10f), new Vector2(70f, 32f));
    }

    void BuildRightColumn(Transform columns)
    {
        var right = CreateImage(columns, "RightColumn", new Color(0.78f, 0.74f, 0.86f, 1f));
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
        srt.offsetMin = new Vector2(8f, 48f);
        srt.offsetMax = new Vector2(-8f, -56f);
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

        var tip = CreateText(right.transform, "RightTip", "消耗天赋石解锁辅助/专精天赋", 16, new Color(0.35f, 0.2f, 0.55f));
        tip.alignment = TextAnchor.MiddleLeft;
        SetAnchored(tip.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f),
            new Vector2(12f, 10f), new Vector2(-80f, 32f));
        var cost = CreateText(right.transform, "RightCostText", "0", 20, new Color(0.55f, 0.35f, 0.85f));
        cost.alignment = TextAnchor.MiddleRight;
        SetAnchored(cost.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-16f, 10f), new Vector2(70f, 32f));
    }

    void BuildFooter(Transform panel)
    {
        var footer = CreateImage(panel, "Footer", new Color(0.32f, 0.2f, 0.12f, 1f));
        SetAnchored(footer.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 12f), new Vector2(-24f, 120f));

        var label = CreateText(footer.transform, "SumLabel", "已获得属性加成", 20, new Color(0.75f, 0.95f, 0.7f));
        label.alignment = TextAnchor.MiddleLeft;
        SetAnchored(label.rectTransform, new Vector2(0f, 1f), new Vector2(0.55f, 1f), new Vector2(0f, 1f),
            new Vector2(16f, -8f), new Vector2(0f, 28f));

        CreateSum(footer.transform, "SumAttack", "+0", 0f);
        CreateSum(footer.transform, "SumHp", "+0", 0.18f);
        CreateSum(footer.transform, "SumDef", "+0", 0.36f);
        CreateSum(footer.transform, "SumCrit", "+0%", 0.54f);
        CreateSum(footer.transform, "SumAtkSpd", "+0%", 0.72f);

        var reset = CreateImage(footer.transform, "ResetButton", new Color(0.65f, 0.18f, 0.16f, 1f));
        SetAnchored(reset.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-16f, -8f), new Vector2(160f, 64f));
        reset.gameObject.AddComponent<Button>().targetGraphic = reset;
        var resetTxt = CreateText(reset.transform, "Label", "重置天赋", 26, new Color(1f, 0.92f, 0.75f));
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
        // Left node template
        var left = CreateImage(root, "LeftNodeTemplate", new Color(0.95f, 0.9f, 0.8f, 0.01f));
        left.gameObject.SetActive(false);
        SetAnchored(left.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            Vector2.zero, new Vector2(0f, 80f));
        left.gameObject.AddComponent<Button>().targetGraphic = left;

        var icon = CreateImage(left.transform, "Icon", new Color(0.4f, 0.35f, 0.3f, 1f));
        SetAnchored(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(10f, 0f), new Vector2(56f, 56f));
        var name = CreateText(left.transform, "NameText", "力量 I", 22, new Color(0.25f, 0.15f, 0.1f));
        name.alignment = TextAnchor.MiddleLeft;
        SetAnchored(name.rectTransform, new Vector2(0f, 0.55f), new Vector2(1f, 1f), new Vector2(0f, 0.5f),
            new Vector2(78f, 0f), new Vector2(-120f, 0f));
        var effect = CreateText(left.transform, "EffectText", "攻击 +3", 18, new Color(0.4f, 0.3f, 0.22f));
        effect.alignment = TextAnchor.MiddleLeft;
        SetAnchored(effect.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(78f, 0f), new Vector2(-120f, 0f));
        var check = CreateImage(left.transform, "Check", new Color(0.55f, 0.55f, 0.55f, 1f));
        SetAnchored(check.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-12f, 0f), new Vector2(28f, 28f));
        var line = CreateImage(left.transform, "Line", new Color(0.35f, 0.28f, 0.2f, 0.7f));
        SetAnchored(line.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 1f),
            new Vector2(38f, 0f), new Vector2(4f, 12f));

        // Right row template
        var right = CreateImage(root, "RightRowTemplate", new Color(0.85f, 0.8f, 0.92f, 0.25f));
        right.gameObject.SetActive(false);
        SetAnchored(right.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            Vector2.zero, new Vector2(0f, 100f));
        var title = CreateText(right.transform, "TitleText", "武器专精", 20, new Color(0.25f, 0.15f, 0.35f));
        title.alignment = TextAnchor.MiddleLeft;
        SetAnchored(title.rectTransform, new Vector2(0f, 1f), new Vector2(0.7f, 1f), new Vector2(0f, 1f),
            new Vector2(10f, -4f), new Vector2(0f, 28f));
        var cost = CreateText(right.transform, "CostText", "12", 18, new Color(0.45f, 0.25f, 0.7f));
        cost.alignment = TextAnchor.MiddleRight;
        SetAnchored(cost.rectTransform, new Vector2(0.7f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-36f, -4f), new Vector2(0f, 28f));
        var lockImg = CreateImage(right.transform, "Lock", new Color(0.3f, 0.3f, 0.35f, 0.9f));
        SetAnchored(lockImg.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-8f, 8f), new Vector2(24f, 24f));

        for (int o = 0; o < 3; o++)
        {
            var opt = CreateImage(right.transform, "Opt_" + o, new Color(0.55f, 0.45f, 0.65f, 1f));
            SetAnchored(opt.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(12f + o * 72f, 10f), new Vector2(64f, 64f));
            opt.gameObject.AddComponent<Button>().targetGraphic = opt;
            var oi = CreateImage(opt.transform, "Icon", new Color(0.75f, 0.7f, 0.85f, 1f));
            SetAnchored(oi.rectTransform, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(40f, 40f));
            var ol = CreateText(opt.transform, "Label", "选项", 14, Color.white);
            SetAnchored(ol.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.35f), new Vector2(0.5f, 0f),
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
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
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
