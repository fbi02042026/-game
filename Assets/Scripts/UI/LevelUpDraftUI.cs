using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 局内抽卡弹层，两层结构：
/// 第一层「方向标签」（技能 / 佣兵 / 装备）→ 第二层该方向的三选一。
/// 只有一个方向可选时自动跳过第一层。
/// 运行时建树，不依赖预制体；弹出期间冻结战斗（<see cref="BattleManager.UnitsCanAct"/>），选完恢复。
/// </summary>
public class LevelUpDraftUI : MonoBehaviour
{
    public static LevelUpDraftUI Instance { get; private set; }

    /// <summary>当前是否有弹层在等玩家选择。</summary>
    public static bool IsShowing { get; private set; }

    Action<DraftCard> _onPick;
    Action _onSkip;
    bool _restoreUnitsCanAct;
    bool _manageFreeze;

    List<DraftCard> _cards;
    List<DraftCategory> _cats;
    Func<DraftCategory, List<DraftCard>> _provider;

    const float CardW = 186f;
    const float CardH = 300f;
    const float CardGap = 16f;
    const float TagW = 178f;
    const float TagH = 182f;
    // V6 排序条：已装备技能的横向条，拖拽调整自动释放优先级
    const float OrderChipW = 132f;
    const float OrderChipH = 56f;
    const float OrderGap = 12f;

    static readonly Color BgDark = new Color(0.10f, 0.09f, 0.13f, 0.96f);
    static readonly Color CardBg = new Color(0.17f, 0.16f, 0.21f, 1f);
    static readonly Color TagBg = new Color(0.16f, 0.155f, 0.20f, 1f);

    // ============================================================
    // 对外入口
    // ============================================================

    /// <summary>直接出三选一（无方向层）。</summary>
    public static LevelUpDraftUI Show(List<DraftCard> cards, string title, Action<DraftCard> onPick,
        Action onSkip = null, bool manageFreeze = true)
    {
        if (cards == null || cards.Count == 0)
        {
            onPick?.Invoke(default);
            return null;
        }
        var ui = Ensure();
        if (ui == null) return null;
        ui._useOrderConfirm = false;
        ui._onOrderConfirmed = null;
        ui.Open(null, cards, null, title, onPick, onSkip, manageFreeze);
        return ui;
    }

    /// <summary>
    /// V6 教程专用：挑完卡<b>不立刻关弹层</b>，先进入「排序确认」阶段。
    /// 进入排序阶段前会先调用 <paramref name="onPick"/> 让效果立即生效
    /// —— 新技能必须先进 RunLoadout，顺序条上才看得到它。
    /// 确认按钮回调 <paramref name="onConfirmed"/> 只调一次。
    /// </summary>
    public static LevelUpDraftUI ShowOrderConfirm(List<DraftCard> cards, string title,
        Action<DraftCard> onPick, Action onConfirmed, bool manageFreeze = true)
    {
        if (cards == null || cards.Count == 0)
        {
            onPick?.Invoke(default);
            onConfirmed?.Invoke();
            return null;
        }
        var ui = Ensure();
        if (ui == null)
        {
            onConfirmed?.Invoke();
            return null;
        }
        ui._useOrderConfirm = true;
        ui._onOrderConfirmed = onConfirmed;
        ui.Open(null, cards, null, title, onPick, null, manageFreeze);
        return ui;
    }

    /// <summary>先选方向，再出该方向的三选一。</summary>
    public static LevelUpDraftUI ShowCategorized(List<DraftCategory> cats,
        Func<DraftCategory, List<DraftCard>> cardProvider, string title,
        Action<DraftCard> onPick, bool manageFreeze = true)
    {
        if (cats == null || cats.Count == 0 || cardProvider == null)
        {
            onPick?.Invoke(default);
            return null;
        }
        var ui = Ensure();
        if (ui == null) return null;
        ui._useOrderConfirm = false;
        ui._onOrderConfirmed = null;
        ui.Open(cats, null, cardProvider, title, onPick, null, manageFreeze);
        return ui;
    }

    // ============================================================
    // 教程专用：外部驱动（高亮 / 超时自动选 / 强制进下一步）
    // ============================================================

    /// <summary>当前是否停在「排序确认」阶段（教程靠它判断要不要推玩家点确认）。</summary>
    public bool InOrderPhase => _orderConfirmPhase;

    /// <summary>当前卡面数量（教程轮询等待玩家选完时用）。</summary>
    public int CardCount => _cards != null ? _cards.Count : 0;

    /// <summary>取第 index 张卡的 RectTransform，用于新手引导挖空高亮。</summary>
    public RectTransform CardRectAt(int index)
    {
        if (_cardRow == null || index < 0 || index >= _cardRow.childCount) return null;
        return _cardRow.GetChild(index) as RectTransform;
    }

    /// <summary>顺序条容器（教程拖不动时可高亮整条）。</summary>
    public RectTransform OrderRowRect => _orderRow;

    /// <summary>确认顺序按钮（排序阶段高亮它）。</summary>
    public RectTransform ConfirmRect =>
        _confirmBtn != null ? _confirmBtn.GetComponent<RectTransform>() : null;

    /// <summary>
    /// 教程超时兜底：按 index 直接替玩家选一张；不在选卡阶段返回 false。
    /// 处于排序阶段时改为强制确认，保证引导不会卡死在这一屏。
    /// </summary>
    public bool TryAutoPick(int index)
    {
        if (!IsShowing || !gameObject.activeInHierarchy) return false;

        if (_orderConfirmPhase)
        {
            ConfirmOrderPhase();
            return true;
        }
        if (_cards == null || _cards.Count == 0) return false;
        int i = Mathf.Clamp(index, 0, _cards.Count - 1);
        Pick(_cards[i]);
        return true;
    }

    /// <summary>把指定技能拖到第一位（教程：没动手就替玩家把护盾排到 ①）。</summary>
    public void ForceSkillToFront(string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return;
        if (!RunLoadout.HasSkill(skillId)) return;
        int at = RunLoadout.SkillIds().IndexOf(skillId);
        if (at <= 0) return;
        RunLoadout.MoveSkill(at, 0);
        RunLoadout.Save();
        var dir = RunDraftDirector.Instance;
        if (dir == null && BattleManager.Instance != null)
            dir = RunDraftDirector.Ensure(BattleManager.Instance);
        if (dir != null) dir.RebuildPlayerSkills();
        RunSkillBarUI.Refresh();
    }

    static LevelUpDraftUI Ensure()
    {
        if (Instance != null) return Instance;
        Transform parent = BattleUI.Instance != null ? BattleUI.Instance.transform : null;
        if (parent == null)
        {
            var canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
            parent = canvas != null ? canvas.transform : null;
        }
        if (parent == null)
        {
            Debug.LogWarning("[LevelUpDraftUI] 找不到 UI 父节点，跳过抽卡弹层");
            return null;
        }
        var go = new GameObject("LevelUpDraftUI", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.AddComponent<LevelUpDraftUI>();
    }

    void Awake()
    {
        Instance = this;
        BuildShell();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        IsShowing = false;
    }

    // ============================================================
    // 骨架
    // ============================================================

    RectTransform _root;
    RectTransform _tagRow;
    RectTransform _cardRow;
    /// <summary>V6：已装备技能的顺序条（拖拽调整释放优先级）。</summary>
    RectTransform _orderRow;
    Button _confirmBtn;
    Button _backBtn;
    Text _title;
    Text _hint;

    /// <summary>排序确认阶段：挑完卡不立刻关，先让玩家定顺序再确认（教程用）。</summary>
    bool _orderConfirmPhase;
    bool _useOrderConfirm;
    Action _onOrderConfirmed;
    DraftCard _pendingCard;

    void BuildShell()
    {
        _root = GetComponent<RectTransform>();
        _root.anchorMin = Vector2.zero;
        _root.anchorMax = Vector2.one;
        _root.offsetMin = Vector2.zero;
        _root.offsetMax = Vector2.zero;

        // 全屏遮罩：挡输入，防误点
        var dim = CreateImage("Dim", _root, new Color(0f, 0f, 0f, 0.62f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        var panel = CreateImage("Panel", _root, BgDark);
        // V6：540 → 620，腾出顶部排序条的空间
        Center(panel.rectTransform, 680f, 620f);

        var border = CreateImage("Border", panel.transform, new Color(0.42f, 0.36f, 0.24f, 1f));
        Stretch(border.rectTransform);
        border.transform.SetAsFirstSibling();

        _title = CreateText("Title", panel.transform, "升级！", 32, new Color(1f, 0.94f, 0.78f));
        Anchor(_title.rectTransform, 0.5f, 1f, 0.5f, 1f, 0.5f, 1f, 0f, -26f, 600f, 44f);

        // —— 第一层：方向标签 ——
        _tagRow = CreateRect("Tags", panel.transform);
        Anchor(_tagRow, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0f, 6f, 640f, TagH + 10f);
        var tagLayout = _tagRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        tagLayout.spacing = 20f;
        tagLayout.childAlignment = TextAnchor.MiddleCenter;
        tagLayout.childControlWidth = false;
        tagLayout.childControlHeight = false;
        tagLayout.childForceExpandWidth = false;
        tagLayout.childForceExpandHeight = false;

        // —— 第二层：卡片 ——
        _cardRow = CreateRect("Cards", panel.transform);
        // V6：整体下移，给顶部排序条让位
        Anchor(_cardRow, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0f, -20f, 620f, CardH + 10f);
        var layout = _cardRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = CardGap;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        // —— V6：已装备技能的顺序条（① = 最先释放）——
        _orderRow = CreateRect("OrderRow", panel.transform);
        Anchor(_orderRow, 0.5f, 1f, 0.5f, 1f, 0.5f, 1f, 0f, -76f, 620f, OrderChipH + 18f);
        var orderLayout = _orderRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        orderLayout.spacing = OrderGap;
        orderLayout.childAlignment = TextAnchor.MiddleCenter;
        orderLayout.childControlWidth = false;
        orderLayout.childControlHeight = false;
        orderLayout.childForceExpandWidth = false;
        orderLayout.childForceExpandHeight = false;

        // 排序确认按钮（仅排序确认阶段显示）
        _confirmBtn = CreateButton("Confirm", panel.transform, "确认顺序", 22, new Color(1f, 0.92f, 0.72f));
        Anchor(_confirmBtn.GetComponent<RectTransform>(), 1f, 0f, 1f, 0f, 1f, 0f, -24f, 20f, 180f, 46f);
        _confirmBtn.onClick.AddListener(ConfirmOrderPhase);
        _confirmBtn.gameObject.SetActive(false);

        // 返回（只在两段式流程的第二层显示）
        _backBtn = CreateButton("Back", panel.transform, "← 返回方向", 20, new Color(0.78f, 0.76f, 0.72f));
        Anchor(_backBtn.GetComponent<RectTransform>(), 0f, 1f, 0f, 1f, 0f, 1f, 20f, -26f, 150f, 36f);
        _backBtn.onClick.AddListener(ShowTags);
        _backBtn.gameObject.SetActive(false);

        _hint = CreateText("Hint", panel.transform,
            "选择后立即生效，本局结束时构筑作废（金币与天赋石保留）", 20, new Color(0.72f, 0.70f, 0.66f));
        Anchor(_hint.rectTransform, 0.5f, 0f, 0.5f, 0f, 0.5f, 0f, 0f, 28f, 640f, 50f);

        gameObject.SetActive(false);
    }

    // ============================================================
    // 打开 / 关闭
    // ============================================================

    void Open(List<DraftCategory> cats, List<DraftCard> preCards,
        Func<DraftCategory, List<DraftCard>> provider, string title,
        Action<DraftCard> onPick, Action onSkip, bool manageFreeze)
    {
        _cats = cats;
        _cards = preCards;
        _provider = provider;
        _onPick = onPick;
        _onSkip = onSkip;
        _orderConfirmPhase = false;
        _pendingCard = default;

        if (_title != null) _title.text = string.IsNullOrEmpty(title) ? "三选一" : title;

        // manageFreeze=false：调用方已在冻结中，弹层不要抢着恢复
        var bm = BattleManager.Instance;
        _restoreUnitsCanAct = manageFreeze && bm != null && bm.UnitsCanAct;
        if (manageFreeze && bm != null) bm.UnitsCanAct = false;

        IsShowing = true;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (_cats != null && _cats.Count > 1) ShowTags();
        else GoToCards(_cats != null && _cats.Count == 1 ? _cats[0] : (DraftCategory?)null);
    }

    /// <summary>第一层：方向标签。</summary>
    void ShowTags()
    {
        if (_tagRow == null) return;
        ClearChildren(_tagRow);
        ClearChildren(_cardRow);

        _tagRow.gameObject.SetActive(true);
        _cardRow.gameObject.SetActive(false);
        SetOrderRowVisible(false);
        if (_backBtn != null) _backBtn.gameObject.SetActive(false);
        if (_hint != null)
            _hint.text = "先定一个方向，再从该方向里挑 1 个（本局构筑，撤离/死亡即作废）";

        if (_cats == null) return;
        for (int i = 0; i < _cats.Count; i++)
            BuildTag(_tagRow, _cats[i]);
    }

    /// <summary>第二层：具体三选一。cat 为 null 时用预置 cards。</summary>
    void GoToCards(DraftCategory? cat)
    {
        List<DraftCard> cards = cat.HasValue && _provider != null ? _provider(cat.Value) : _cards;
        if (cards == null || cards.Count == 0)
            cards = DraftPool.BuildFallbackCards();

        if (_tagRow != null) _tagRow.gameObject.SetActive(false);
        if (_cardRow != null) _cardRow.gameObject.SetActive(true);
        bool twoStage = _cats != null && _cats.Count > 1;
        if (_backBtn != null) _backBtn.gameObject.SetActive(twoStage);
        if (_hint != null)
            _hint.text = twoStage
                ? "点「← 返回方向」可换方向　·　本局构筑撤离/死亡即作废（金币与天赋石保留）"
                : "选择后立即生效，本局结束时构筑作废（金币与天赋石保留）";

        ClearChildren(_cardRow);
        for (int i = 0; i < cards.Count; i++)
            BuildCard(_cardRow, cards[i], i);

        // V6：第二层同时展示「当前技能释放顺序」，可拖拽调整
        SetOrderRowVisible(RunLoadout.SkillIds().Count > 0);
        BuildOrderRow();
    }

    // ============================================================
    // V6：技能顺序条（拖拽 → 释放优先级）
    // ============================================================

    void SetOrderRowVisible(bool on)
    {
        if (_orderRow != null) _orderRow.gameObject.SetActive(on && !_orderConfirmPhase);
        if (!on && _confirmBtn != null) _confirmBtn.gameObject.SetActive(false);
    }

    /// <summary>按 RunLoadout 当前技能顺序重建顺序条。</summary>
    void BuildOrderRow()
    {
        if (_orderRow == null) return;
        ClearChildren(_orderRow);

        var ids = RunLoadout.SkillIds();
        var job = PlayerJobDefs.GetSelected();
        for (int i = 0; i < ids.Count; i++)
        {
            string id = ids[i];
            var def = PlayerSkillDefs.GetById(id);
            string name = def != null ? def.displayName : id;
            int star = RunLoadout.StarOf(id);
            string label = $"{OrderMark(i)}{name}\n★{star}";

            var img = CreateImage("OrderChip_" + i, _orderRow, new Color(0.20f, 0.19f, 0.25f, 1f));
            img.rectTransform.sizeDelta = new Vector2(OrderChipW, OrderChipH);
            var le = img.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = OrderChipW;
            le.preferredHeight = OrderChipH;

            var tint = SkillRarityUtil.Tint(SkillDraftMeta.Rarity(id));
            var text = CreateText("Label", img.transform, label, 18, tint);
            Stretch(text.rectTransform);
            text.alignment = TextAnchor.MiddleCenter;

            var chip = img.gameObject.AddComponent<SkillOrderChip>();
            chip.SkillId = id;
            chip.SlotStep = OrderChipW + OrderGap;
        }
    }

    static string OrderMark(int index)
    {
        switch (index)
        {
            case 0: return "①";
            case 1: return "②";
            case 2: return "③";
            case 3: return "④";
            default: return (index + 1) + ".";
        }
    }

    /// <summary>
    /// 把玩家拖动出来的顺序写回 RunLoadout（用单向 MoveSkill 逐步到位），
    /// 再重建战斗技能（槽序 = SkillSystem.GetReadyPlayerSkill 的优先级）并刷新 HUD。
    /// </summary>
    void CommitReorder()
    {
        if (_orderRow == null) return;

        var want = new List<string>();
        for (int i = 0; i < _orderRow.childCount; i++)
        {
            var chip = _orderRow.GetChild(i).GetComponent<SkillOrderChip>();
            if (chip != null && !string.IsNullOrEmpty(chip.SkillId)) want.Add(chip.SkillId);
        }
        if (want.Count < 2) return;

        bool changed = false;
        var cur = RunLoadout.SkillIds();
        for (int target = 0; target < want.Count; target++)
        {
            int at = cur.IndexOf(want[target]);
            if (at < 0 || at == target) continue;
            if (RunLoadout.MoveSkill(at, target))
            {
                var moved = cur[at];
                cur.RemoveAt(at);
                cur.Insert(target, moved);
                changed = true;
            }
        }
        if (!changed) return;

        RunLoadout.Save();
        var dir = RunDraftDirector.Instance;
        if (dir == null && BattleManager.Instance != null)
            dir = RunDraftDirector.Ensure(BattleManager.Instance);
        if (dir != null) dir.RebuildPlayerSkills();
        RunSkillBarUI.Refresh();
    }

    /// <summary>进入排序确认阶段：卡已挑好，但不关弹层，先让玩家定顺序。</summary>
    void EnterOrderPhase(DraftCard card)
    {
        _orderConfirmPhase = true;
        _pendingCard = card;

        if (_cardRow != null) _cardRow.gameObject.SetActive(false);
        if (_tagRow != null) _tagRow.gameObject.SetActive(false);
        if (_backBtn != null) _backBtn.gameObject.SetActive(false);
        if (_orderRow != null) _orderRow.gameObject.SetActive(true);
        if (_confirmBtn != null) _confirmBtn.gameObject.SetActive(true);

        // 新技能已由调用方落进 RunLoadout，重建顺序条即可看到它
        BuildOrderRow();
        if (_hint != null)
            _hint.text = "拖动调整技能顺序：① 最先释放，② ③ 是它的替补（能量各自累积）";
        if (_title != null) _title.text = "调整释放顺序";
    }

    /// <summary>确认顺序 → 提交 → 关弹层 → 通知调用方。</summary>
    void ConfirmOrderPhase()
    {
        _orderConfirmPhase = false;
        CommitReorder();
        _pendingCard = default;

        var done = _onOrderConfirmed;
        _onOrderConfirmed = null;
        Close();
        done?.Invoke();
    }

    void ClearChildren(RectTransform row)
    {
        if (row == null) return;
        for (int i = row.childCount - 1; i >= 0; i--)
        {
            var c = row.GetChild(i);
            // 立刻脱离层级，避免同帧布局把已标记销毁的项也算进去
            c.SetParent(null, false);
            Destroy(c.gameObject);
        }
    }

    // ============================================================
    // 方向标签
    // ============================================================

    void BuildTag(Transform parent, DraftCategory cat)
    {
        var tint = DraftCategoryUtil.Tint(cat);
        var root = CreateImage($"Tag_{cat}", parent, TagBg);
        root.rectTransform.sizeDelta = new Vector2(TagW, TagH);
        var le = root.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = TagW;
        le.preferredHeight = TagH;

        var band = CreateImage("Band", root.transform, tint);
        Anchor(band.rectTransform, 0f, 1f, 1f, 1f, 0f, 1f, 0f, 0f, 0f, 10f);

        var glyph = CreateText("Glyph", root.transform, DraftCategoryUtil.Glyph(cat), 56, tint);
        Anchor(glyph.rectTransform, 0.5f, 1f, 0.5f, 1f, 0.5f, 1f, 0f, -62f, TagW - 20f, 70f);

        var name = CreateText("Name", root.transform, DraftCategoryUtil.DisplayName(cat), 26, Color.white);
        Anchor(name.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0f, -6f, TagW - 20f, 34f);

        var hint = CreateText("TagHint", root.transform, DraftPool.CategoryHint(cat), 17,
            new Color(0.74f, 0.74f, 0.78f));
        Anchor(hint.rectTransform, 0.5f, 0f, 0.5f, 0f, 0.5f, 0f, 0f, 34f, TagW - 18f, 44f);
        hint.alignment = TextAnchor.UpperCenter;

        var btn = root.gameObject.AddComponent<Button>();
        btn.targetGraphic = root;
        btn.transition = Selectable.Transition.ColorTint;
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        btn.colors = colors;
        var picked = cat;
        btn.onClick.AddListener(() => GoToCards(picked));
    }

    // ============================================================
    // 卡片
    // ============================================================

    void BuildCard(Transform parent, DraftCard card, int index)
    {
        var rarityTint = SkillRarityUtil.Tint(card.Rarity);
        var cardRoot = CreateImage($"Card_{card.Kind}_{index}", parent, CardBg);
        cardRoot.rectTransform.sizeDelta = new Vector2(CardW, CardH);
        var cardLayout = cardRoot.gameObject.AddComponent<LayoutElement>();
        cardLayout.preferredWidth = CardW;
        cardLayout.preferredHeight = CardH;

        var topBand = CreateImage("TopBand", cardRoot.transform, rarityTint);
        Anchor(topBand.rectTransform, 0f, 1f, 1f, 1f, 0f, 1f, 0f, 0f, 0f, 12f);

        // 稀有度角标
        var rarTag = CreateText("Rarity", cardRoot.transform, SkillRarityUtil.DisplayName(card.Rarity), 20,
            new Color(0.08f, 0.07f, 0.10f));
        Anchor(rarTag.rectTransform, 0f, 1f, 0f, 1f, 0f, 1f, 12f, -22f, 70f, 26f);
        rarTag.alignment = TextAnchor.MiddleCenter;

        // 类型标签
        var kindTag = CreateText("Kind", cardRoot.transform, KindLabel(card), 20, new Color(0.88f, 0.86f, 0.82f));
        Anchor(kindTag.rectTransform, 1f, 1f, 1f, 1f, 1f, 1f, -12f, -22f, 130f, 26f);
        kindTag.alignment = TextAnchor.MiddleRight;

        // 图标块（无美术资源时用稀有度色块占位）
        var icon = CreateImage("Icon", cardRoot.transform, new Color(rarityTint.r, rarityTint.g, rarityTint.b, 0.35f));
        Anchor(icon.rectTransform, 0.5f, 1f, 0.5f, 1f, 0.5f, 1f, 0f, -92f, 92f, 92f);
        var iconLabel = CreateText("IconLabel", icon.transform, Initial(card.Title), 40, rarityTint);
        Stretch(iconLabel.rectTransform);

        // 标题
        var title = CreateText("Title", cardRoot.transform, card.Title, 24, Color.white);
        Anchor(title.rectTransform, 0.5f, 1f, 0.5f, 1f, 0.5f, 1f, 0f, -152f, CardW - 20f, 34f);

        // 描述
        var desc = CreateText("Desc", cardRoot.transform, card.Desc, 20, new Color(0.78f, 0.78f, 0.80f));
        Anchor(desc.rectTransform, 0.5f, 1f, 0.5f, 1f, 0.5f, 1f, 0f, -200f, CardW - 22f, 62f);
        desc.alignment = TextAnchor.UpperCenter;

        // 战力增量（职业亲和直接并入本行，避免卡内拥挤）
        string powerLabel = card.IsAffinity ? $"+{card.PowerDelta} 战力　▲亲和" : $"+{card.PowerDelta} 战力";
        var power = CreateText("Power", cardRoot.transform, powerLabel, 20, new Color(1f, 0.82f, 0.36f));
        Anchor(power.rectTransform, 0.5f, 0f, 0.5f, 0f, 0.5f, 0f, 0f, 12f, CardW - 20f, 30f);

        // 点击热区
        var btn = cardRoot.gameObject.AddComponent<Button>();
        btn.targetGraphic = cardRoot;
        btn.transition = Selectable.Transition.ColorTint;
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        btn.colors = colors;
        var picked = card;
        btn.onClick.AddListener(() => Pick(picked));
    }

    static string KindLabel(DraftCard card)
    {
        switch (card.Kind)
        {
            case DraftCardKind.SkillNew: return "新技能";
            case DraftCardKind.SkillUp: return $"升星 ★{card.Star}";
            case DraftCardKind.MercRecruit: return "招募佣兵";
            case DraftCardKind.MercLevelUp: return $"佣兵升级 Lv{card.MercLevel}";
            case DraftCardKind.MercStarUp: return $"佣兵升星 ★{card.Star}";
            case DraftCardKind.Equip: return "本局装备";
            default: return "强化";
        }
    }

    void Pick(DraftCard card)
    {
        // 排序确认：先应用效果（新技能必须先进 RunLoadout，顺序条上才看得到它），
        // 再进入排序阶段，此时不关弹层。
        if (_useOrderConfirm && !_orderConfirmPhase)
        {
            var apply = _onPick;
            _onPick = null;              // 只应用一次
            apply?.Invoke(card);
            EnterOrderPhase(card);
            return;
        }

        // 玩家可能先在排序条上拖过 —— 落盘后才能关
        CommitReorder();

        var cb = _onPick;
        Close();
        cb?.Invoke(card);
    }

    void Close()
    {
        gameObject.SetActive(false);
        IsShowing = false;
        ClearChildren(_cardRow);
        ClearChildren(_tagRow);
        ClearChildren(_orderRow);
        if (_confirmBtn != null) _confirmBtn.gameObject.SetActive(false);

        if (_restoreUnitsCanAct && BattleManager.Instance != null)
            BattleManager.Instance.UnitsCanAct = true;
        _restoreUnitsCanAct = false;
        _cats = null;
        _cards = null;
        _provider = null;
        _useOrderConfirm = false;
        _orderConfirmPhase = false;
        _onOrderConfirmed = null;
        _pendingCard = default;
    }

    // ============================================================
    // UI 小工具
    // ============================================================

    static string Initial(string s)
    {
        if (string.IsNullOrEmpty(s)) return "?";
        return s.Substring(0, 1);
    }

    static Image CreateImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static Text CreateText(string name, Transform parent, string text, int size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        var f = GameFonts.GetChinese();
        if (f != null) t.font = f;
        return t;
    }

    static Button CreateButton(string name, Transform parent, string label, int size, Color color)
    {
        var img = CreateImage(name, parent, new Color(0.22f, 0.21f, 0.26f, 0.94f));
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var text = CreateText("Label", img.transform, label, size, color);
        Stretch(text.rectTransform);
        return btn;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void Center(RectTransform rt, float w, float h)
    {
        Anchor(rt, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0f, 0f, w, h);
    }

    static void Anchor(RectTransform rt, float aminX, float aminY, float amaxX, float amaxY,
        float px, float py, float x, float y, float w, float h)
    {
        rt.anchorMin = new Vector2(aminX, aminY);
        rt.anchorMax = new Vector2(amaxX, amaxY);
        rt.pivot = new Vector2(px, py);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }
}
