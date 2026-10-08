using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ⚠️ 已废弃（2026-09-30 主人拍板）：进关抽奖改成装在战斗 HUD 底部的 BackpackPanel 里，
/// 走 <see cref="BattleEntryDraftPanel"/>。本类已无任何调用点，保留只为可回退，勿再接线。
///
/// 进关抽奖面板（2026-09-29 改造：两按钮「抽奖/跳过」→ 四个抽奖按钮 +「开始战斗」）。
///
/// 面板在**开战前常驻**：只要抽奖币够，玩家可以一直抽；点「开始战斗」才关面板开打。
/// 四个按钮：随机（基准价） / 佣兵 / 装备 / 技能（定向，价格 ×2，买的是确定性）。
/// 某类池子为空或币不够 → 该按钮置灰，绝不自动扣钱。
///
/// 🔴 战斗一开始本面板必须完全隐藏（SetActive(false)）——战斗画面里只留战斗，
/// 不留任何抽奖入口。开战路径统一走 <see cref="Hide"/>，由 BattleManager 兜底调用。
///
/// 运行时建树，不依赖预制体。
/// </summary>
public class SlotOfferUI : MonoBehaviour
{
    public static SlotOfferUI Instance { get; private set; }

    const float PanelW = 470f;
    const float PanelH = 392f;
    const float BtnRandomW = 390f;
    const float BtnRandomH = 74f;
    const float BtnFocusW = 118f;
    const float BtnFocusH = 72f;
    const float BtnFocusGap = 18f;
    const float BtnFightW = 240f;
    const float BtnFightH = 58f;

    static readonly Color DimColor = new Color(0f, 0f, 0f, 0.55f);
    static readonly Color PanelColor = new Color(0.12f, 0.11f, 0.15f, 0.96f);
    static readonly Color BtnRollColor = new Color(0.95f, 0.72f, 0.25f, 1f);
    static readonly Color BtnFocusColor = new Color(0.36f, 0.58f, 0.86f, 1f);
    static readonly Color BtnSkipColor = new Color(0.26f, 0.25f, 0.31f, 1f);
    static readonly Color LabelDim = new Color(0.62f, 0.62f, 0.66f, 1f);
    static readonly Color WalletColor = new Color(0.80f, 0.76f, 0.64f, 1f);

    /// <summary>定向按钮顺序：与 SlotMachineSystem.AvailableCategories 的返回顺序一致。</summary>
    static readonly DraftCategory[] FocusCats = { DraftCategory.Skill, DraftCategory.Merc, DraftCategory.Equip };

    Action<DraftCategory?> _onPick;
    Action _onFight;
    Text _priceText;
    Text _walletText;
    Button _randomBtn;
    Text _randomLabel;
    readonly List<Button> _focusBtns = new List<Button>();
    readonly List<Text> _focusLabels = new List<Text>();

    /// <summary>
    /// 弹出（开战前常驻）。
    /// <paramref name="onPick"/> 回调：null = 点了随机，有值 = 点了该类型的定向抽奖。
    /// <paramref name="onFight"/> 回调：点了「开始战斗」。
    /// </summary>
    public static SlotOfferUI Show(int randomPrice, int focusPrice, List<DraftCategory> cats, long coins,
                                   Action<DraftCategory?> onPick, Action onFight)
    {
        var ui = Ensure();
        if (ui == null)
        {
            onFight?.Invoke();
            return null;
        }
        ui.Begin(randomPrice, focusPrice, cats, coins, onPick, onFight);
        return ui;
    }

    /// <summary>「随机抽奖」按钮的 RectTransform —— 引导关要圈住它做高亮。</summary>
    public RectTransform RandomButtonRect =>
        _randomBtn != null ? _randomBtn.GetComponent<RectTransform>() : null;

    /// <summary>超时未选择时调用：等同玩家点了「开始战斗」，不触发任何回调。</summary>
    public static void Hide()
    {
        if (Instance != null) Instance.Close();
    }

    static SlotOfferUI Ensure()
    {
        if (Instance != null) return Instance;
        Transform parent = BattleUI.Instance != null ? BattleUI.Instance.transform : null;
        if (parent == null)
        {
            var canvas = FindObjectOfType<Canvas>();
            parent = canvas != null ? canvas.transform : null;
        }
        if (parent == null)
        {
            Debug.LogError("[SlotOfferUI] 找不到 UI 父节点，抽奖面板无法创建");
            return null;
        }
        var go = new GameObject("SlotOfferUI", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var ui = go.AddComponent<SlotOfferUI>();
        ui.EnsureSortCanvas();
        return ui;
    }

    /// <summary>
    /// 2026-09-30：面板必须自带 Canvas 抬排序层。
    /// 项目已知坑：嵌套 Canvas 下 SetSiblingIndex 会失效，抽奖面板会被战斗 HUD 盖住
    ///（主人实测「看不见抽奖按钮」）。同时必须补 GraphicRaycaster，否则按钮点不动。
    /// </summary>
    void EnsureSortCanvas()
    {
        var c = GetComponent<Canvas>();
        if (c == null) c = gameObject.AddComponent<Canvas>();
        c.overrideSorting = true;
        c.sortingLayerName = GameConfig.BATTLE_SORTING_LAYER;
        c.sortingOrder = 300;
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
    }

    void Awake()
    {
        Instance = this;
        BuildShell();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void BuildShell()
    {
        var root = GetComponent<RectTransform>();
        Stretch(root);

        var dim = CreateImage("Dim", transform, DimColor);
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        var panel = CreateImage("Panel", transform, PanelColor);
        Center(panel.rectTransform, PanelW, PanelH);

        var title = CreateText("Title", panel.transform, "进关抽奖", 30, Color.white);
        Anchor(title.rectTransform, 0.5f, 1f, 0.5f, 1f, 0.5f, 1f, 0f, -28f, PanelW - 40f, 42f);

        _walletText = CreateText("Wallet", panel.transform, "", 20, WalletColor);
        Anchor(_walletText.rectTransform, 0.5f, 1f, 0.5f, 1f, 0.5f, 1f, 0f, -68f, PanelW - 40f, 32f);

        _priceText = CreateText("Price", panel.transform, "", 22, new Color(0.96f, 0.84f, 0.52f, 1f));
        Anchor(_priceText.rectTransform, 0.5f, 1f, 0.5f, 1f, 0.5f, 1f, 0f, -98f, PanelW - 40f, 32f);

        _randomBtn = CreateButton("BtnRandom", panel.transform, "随机", 28, Color.white, BtnRollColor);
        Anchor(_randomBtn.GetComponent<RectTransform>(), 0.5f, 1f, 0.5f, 1f, 0.5f, 1f,
               0f, -152f, BtnRandomW, BtnRandomH);
        _randomBtn.onClick.AddListener(OnRandomClicked);
        _randomLabel = _randomBtn.GetComponentInChildren<Text>();

        float left = -(BtnFocusW + BtnFocusGap);
        for (int i = 0; i < FocusCats.Length; i++)
        {
            int idx = i;
            var btn = CreateButton("BtnFocus" + i, panel.transform, "", 24, Color.white, BtnFocusColor);
            Anchor(btn.GetComponent<RectTransform>(), 0.5f, 1f, 0.5f, 1f, 0.5f, 1f,
                   left + i * (BtnFocusW + BtnFocusGap), -246f, BtnFocusW, BtnFocusH);
            btn.onClick.AddListener(() => OnFocusClicked(idx));
            _focusBtns.Add(btn);
            _focusLabels.Add(btn.GetComponentInChildren<Text>());
        }

        // 2026-09-30：这个按钮文案叫「继续」，点完原先会在屏幕上甩出「开始游戏」大字再开打
        //             （该大字已于 2026-10-07 移除，见 StageStartBannerUI 与 BattleManager）
        // ⚠ 原注释写着「主人要求」，主人明确否认要求过加那个大字 —— 属误记，已撤掉该说法。
        var fightBtn = CreateButton("BtnFight", panel.transform, "继续", 26, Color.white, BtnSkipColor);
        Anchor(fightBtn.GetComponent<RectTransform>(), 0.5f, 1f, 0.5f, 1f, 0.5f, 1f, 0f, -322f, BtnFightW, BtnFightH);
        fightBtn.onClick.AddListener(OnFightClicked);
    }

    void Begin(int randomPrice, int focusPrice, List<DraftCategory> cats, long coins,
               Action<DraftCategory?> onPick, Action onFight)
    {
        _onPick = onPick;
        _onFight = onFight;
        gameObject.SetActive(true);

        _walletText.text = $"抽奖币 {coins}";
        _priceText.text = $"随机 {randomPrice}　定向 {focusPrice}";

        bool canRandom = coins >= randomPrice;
        if (_randomBtn != null) _randomBtn.interactable = canRandom;
        if (_randomLabel != null)
        {
            _randomLabel.text = $"随机抽奖\n{randomPrice} 币";
            _randomLabel.color = canRandom ? Color.white : LabelDim;
        }

        for (int i = 0; i < _focusBtns.Count; i++)
        {
            var cat = FocusCats[i];
            bool has = cats != null && cats.Contains(cat);
            bool afford = coins >= focusPrice;
            bool on = has && afford;
            if (_focusBtns[i] != null) _focusBtns[i].interactable = on;
            var lab = _focusLabels[i];
            if (lab != null)
            {
                lab.text = !has ? $"{CategoryName(cat)}\n暂无" : $"{CategoryName(cat)}\n{focusPrice} 币";
                lab.color = on ? Color.white : LabelDim;
            }
        }
    }

    void OnRandomClicked()
    {
        var cb = _onPick;
        Close();
        cb?.Invoke(null);
    }

    void OnFocusClicked(int idx)
    {
        var cb = _onPick;
        var cat = FocusCats[idx];
        Close();
        cb?.Invoke(cat);
    }

    /// <summary>点「开始战斗」：关面板，交回 BattleManager 开打。战斗画面里不留抽奖入口。</summary>
    void OnFightClicked()
    {
        var cb = _onFight;
        Close();
        cb?.Invoke();
    }

    void Close()
    {
        _onPick = null;
        _onFight = null;
        gameObject.SetActive(false);
    }

    static string CategoryName(DraftCategory c)
    {
        switch (c)
        {
            case DraftCategory.Skill: return "技能";
            case DraftCategory.Merc: return "佣兵";
            default: return "装备";
        }
    }

    // ============================================================
    // UI 小工具（与 LevelUpDraftUI / SlotMachineUI 同一套写法）
    // ============================================================

    static Image CreateImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
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

    static Button CreateButton(string name, Transform parent, string label, int size, Color textColor, Color bg)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = bg;
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var t = CreateText("Label", go.transform, label, size, textColor);
        Stretch(t.rectTransform);
        return btn;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void Center(RectTransform rt, float w, float h) => Anchor(rt, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0f, 0f, w, h);

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
