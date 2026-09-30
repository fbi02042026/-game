using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 进关抽奖的老虎机表现层（2026-09-29 新增）。
/// 只负责「演」：三框（佣兵 / 技能 / 装备）左右切换、逐渐减速、停在指定那一格。
///
/// 停哪一格由外部先抽好再传进来 —— 本类不做任何随机，也不产出奖品，
/// 随机在 <see cref="SlotMachineSystem"/>，奖品在 <see cref="DraftPool"/>。
/// 运行时建树，不依赖预制体。
/// </summary>
public class SlotMachineUI : MonoBehaviour
{
    public static SlotMachineUI Instance { get; private set; }

    const float BoxW = 168f;
    const float BoxH = 196f;
    const float BoxGap = 22f;
    const float PanelW = 640f;
    const float PanelH = 380f;

    static readonly Color DimColor = new Color(0f, 0f, 0f, 0.62f);
    static readonly Color PanelColor = new Color(0.12f, 0.11f, 0.15f, 0.96f);
    static readonly Color BoxIdle = new Color(0.17f, 0.16f, 0.21f, 1f);
    static readonly Color BoxHot = new Color(0.95f, 0.72f, 0.25f, 1f);
    static readonly Color TextIdle = new Color(0.86f, 0.86f, 0.90f, 1f);
    static readonly Color TextHot = new Color(0.24f, 0.16f, 0.04f, 1f);

    List<DraftCategory> _cats = new List<DraftCategory>();
    readonly List<Image> _boxes = new List<Image>();
    readonly List<Text> _labels = new List<Text>();
    RectTransform _row;
    Text _title;
    Action _onRolled;

    /// <summary>
    /// 开转。<paramref name="target"/> 是外部先抽好的结果，这里只把它演出来。
    /// 停稳后回调 <paramref name="onRolled"/>，并把自己隐藏，让位给后面的三选一弹层。
    /// </summary>
    public static SlotMachineUI Show(List<DraftCategory> cats, DraftCategory target, int price, Action onRolled)
    {
        var ui = Ensure();
        if (ui == null)
        {
            onRolled?.Invoke();
            return null;
        }
        ui.Begin(cats, target, price, onRolled);
        return ui;
    }

    static SlotMachineUI Ensure()
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
            Debug.LogError("[SlotMachineUI] 找不到 UI 父节点，老虎机无法创建");
            return null;
        }
        var go = new GameObject("SlotMachineUI", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var ui = go.AddComponent<SlotMachineUI>();
        ui.EnsureSortCanvas();
        return ui;
    }

    /// <summary>
    /// 同 <c>SlotOfferUI.EnsureSortCanvas</c>：嵌套 Canvas 下必须自带 Canvas 抬排序层，
    /// 否则老虎机会被战斗 HUD 盖住；补 GraphicRaycaster 保证交互正常。
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

    // ============================================================
    // 骨架
    // ============================================================

    void BuildShell()
    {
        var root = GetComponent<RectTransform>();
        Stretch(root);

        var dim = CreateImage("Dim", transform, DimColor);
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        var panel = CreateImage("Panel", transform, PanelColor);
        Center(panel.rectTransform, PanelW, PanelH);

        _title = CreateText("Title", panel.transform, "", 26, Color.white);
        Anchor(_title.rectTransform, 0.5f, 1f, 0.5f, 1f, 0.5f, 1f, 0f, -34f, PanelW - 40f, 40f);

        _row = CreateRect("Row", panel.transform);
        Anchor(_row, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0f, -24f, PanelW - 40f, BoxH);
    }

    void Begin(List<DraftCategory> cats, DraftCategory target, int price, Action onRolled)
    {
        _cats = cats != null ? cats : new List<DraftCategory>();
        _onRolled = onRolled;
        gameObject.SetActive(true);
        BuildBoxes();
        _title.text = price > 0 ? $"本关抽奖　花费 {price} 金币" : "幸运老虎机　本次免费";
        StopAllCoroutines();
        StartCoroutine(CoRoll(target));
    }

    void BuildBoxes()
    {
        // 项目约定：代码生成 UI 时先倒序销毁旧节点，避免重复叠加
        for (int i = _boxes.Count - 1; i >= 0; i--)
        {
            if (_boxes[i] != null) Destroy(_boxes[i].gameObject);
        }
        _boxes.Clear();
        _labels.Clear();

        int n = _cats.Count;
        if (n == 0) return;

        float total = n * BoxW + (n - 1) * BoxGap;
        float left = -total * 0.5f + BoxW * 0.5f;
        for (int i = 0; i < n; i++)
        {
            var box = CreateImage("Box" + i, _row, BoxIdle);
            Anchor(box.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f,
                   left + i * (BoxW + BoxGap), 0f, BoxW, BoxH);
            var label = CreateText("Label", box.transform, CategoryName(_cats[i]), 32, TextIdle);
            Stretch(label.rectTransform);
            _boxes.Add(box);
            _labels.Add(label);
        }
    }

    // ============================================================
    // 转动
    // ============================================================

    IEnumerator CoRoll(DraftCategory target)
    {
        int n = _boxes.Count;
        if (n == 0)
        {
            Finish();
            yield break;
        }

        int targetIndex = _cats.IndexOf(target);
        if (targetIndex < 0) targetIndex = 0;

        // 2026-09-29：老虎机只在「再来一次」触发时出场，是免费奖励，走个形式给仪式感。
        // 结果早在 SlotMachineSystem 定死了，这里只让玩家「看见」中了大哪一类。
        // 总时长固定 = SlotMachineDefs.JACKPOT_REVEAL_SEC：前 60% 快闪扫格，后 40% 停在目标格。
        int ticks = Mathf.Max(3, n * 2);
        float per = SlotMachineDefs.JACKPOT_REVEAL_SEC * 0.6f / ticks;
        for (int i = 0; i < ticks; i++)
        {
            Paint((i + 1) % n);
            yield return new WaitForSecondsRealtime(per);
        }

        Paint(targetIndex);
        yield return new WaitForSecondsRealtime(SlotMachineDefs.JACKPOT_REVEAL_SEC * 0.4f);
        Finish();
    }

    void Paint(int index)
    {
        for (int i = 0; i < _boxes.Count; i++)
        {
            bool hot = i == index;
            if (_boxes[i] != null)
            {
                _boxes[i].color = hot ? BoxHot : BoxIdle;
                _boxes[i].transform.localScale = hot ? new Vector3(1.08f, 1.08f, 1f) : Vector3.one;
            }
            if (_labels[i] != null) _labels[i].color = hot ? TextHot : TextIdle;
        }
    }

    void Finish()
    {
        gameObject.SetActive(false);
        var cb = _onRolled;
        _onRolled = null;
        cb?.Invoke();
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
    // UI 小工具（与 LevelUpDraftUI 同一套写法）
    // ============================================================

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
