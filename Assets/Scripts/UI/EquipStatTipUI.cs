using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 装备基础属性浮框（2026-10-09 主人拍板）：
/// 点装备图标 → 在图标<b>正上方</b>浮出属性框；再点<b>同一个</b>装备 → 关闭（toggle）。
/// 两处挂载点共用：① 战斗 HUD 底部装备快捷槽；② 背包网格里的装备格。
///
/// 实现约定：
/// - 运行时建树（纯色 Image + Text + UGUI 自带 Outline），不落预制体、不建美术、不 Resources.Load 图片；
/// - 与 <see cref="BackpackItemActionUI"/>（道具操作浮层）互斥：本框弹出时把道具浮层收掉；
/// - 刻意<b>不做</b>全屏遮罩（主人明确不要「点屏幕任意处关闭」），面板本身也不吃射线，免得挡住下层点击。
/// </summary>
public class EquipStatTipUI : MonoBehaviour
{
    public static EquipStatTipUI Instance { get; private set; }

    const float PanelWidth = 240f;
    const float HeadHeight = 44f;
    const float LineHeight = 20f;
    const float PadBottom = 12f;
    /// <summary>浮框底边离图标顶边的间距（像素）。</summary>
    const float GapAbove = 10f;
    const float EdgePad = 6f;

    /// <summary>属性行显示顺序：主人点名的「攻击 / 防御 / 魔攻 / 魔防 / 生命」排最前。</summary>
    static readonly AttrType[] AttrOrder =
    {
        AttrType.Attack, AttrType.MagicAttack, AttrType.Defense, AttrType.MagicDefense,
        AttrType.MaxHp, AttrType.CritRate, AttrType.AttackSpeed,
        AttrType.Strength, AttrType.Intelligence, AttrType.Agility, AttrType.Vitality,
    };

    RectTransform _rootRt;
    GameObject _panel;
    RectTransform _panelRt;
    Text _title;
    Text _body;
    /// <summary>当前浮框是为谁开的（调用方传的 key；未传就用 EquipInstance 本身）。</summary>
    object _openKey;

    /// <summary>挂在 BattleUI 下（保证在 Canvas 内）。重复调用返回同一个实例。</summary>
    public static EquipStatTipUI Ensure(Transform parent)
    {
        if (Instance != null) return Instance;
        if (parent == null) return null;

        var go = new GameObject("EquipStatTipUI", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Instance = go.AddComponent<EquipStatTipUI>();
        Instance.Build();
        return Instance;
    }

    void Build()
    {
        _rootRt = GetComponent<RectTransform>();
        Stretch(_rootRt);   // 铺满 BattleUI：后续用 anchoredPosition 在它内部定位

        // 2026-10-09 主人拍板：浮框挂到 anchor 所在 Canvas 根下，但默认继承父 Canvas 的 sortingOrder，
        // 会被更高 sortingOrder 的面板（结算 990 / 替换确认 925 等）盖住。给根挂独立嵌套 Canvas，
        // 自带 overrideSorting + 高 sortingOrder，确保永远压在最上层（不依赖 sibling 顺序，也不改预制体）。
        var rootCanvas = gameObject.GetComponent<Canvas>();
        if (rootCanvas == null) rootCanvas = gameObject.AddComponent<Canvas>();
        rootCanvas.overrideSorting = true;
        rootCanvas.sortingOrder = 1000;   // 高于 BattleSettlement(990)，低于 Toast(11000)/FullscreenFx(12000)
        // 刻意不加 GraphicRaycaster：_panel 与文本都 raycastTarget=false，浮框不抢下层点击。

        _panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        _panel.transform.SetParent(transform, false);
        var pImg = _panel.GetComponent<Image>();
        pImg.color = new Color(0.12f, 0.12f, 0.15f, 0.96f);   // 深色半透明底（同 BackpackItemActionUI 观感）
        pImg.raycastTarget = false;                            // 不吃射线，别挡住下面的格子/按钮
        _panelRt = _panel.GetComponent<RectTransform>();
        _panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        _panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        _panelRt.pivot = new Vector2(0.5f, 0f);                // 底边对齐定位点 → 天然浮在图标上方
        _panelRt.sizeDelta = new Vector2(PanelWidth, 120f);

        // 装备名（顶行）
        _title = MakeText(_panel.transform, "Title", 16, TextAnchor.MiddleCenter, Color.white);
        var tRt = _title.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 1f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.pivot = new Vector2(0.5f, 1f);
        tRt.anchoredPosition = new Vector2(0f, -8f);
        tRt.sizeDelta = new Vector2(-24f, 26f);

        // 属性行（多行）
        _body = MakeText(_panel.transform, "Body", 13, TextAnchor.UpperLeft, new Color(0.86f, 0.86f, 0.9f, 1f));
        var bRt = _body.GetComponent<RectTransform>();
        bRt.anchorMin = Vector2.zero;
        bRt.anchorMax = Vector2.one;
        bRt.offsetMin = new Vector2(12f, PadBottom * 0.5f);
        bRt.offsetMax = new Vector2(-12f, -HeadHeight);

        // 项目五坑第五条：运行时建的弹窗默认隐藏，否则一创建就糊在屏幕上
        gameObject.SetActive(false);
    }

    /// <summary>在 <paramref name="anchor"/> 正上方显示某件装备的基础属性。</summary>
    public void Show(EquipInstance eq, RectTransform anchor, object key = null)
    {
        if (eq == null || anchor == null) return;

        // 与道具操作浮层互斥（2026-10-09 主人拍板）：点装备只出属性框
        if (BackpackItemActionUI.Instance != null)
            BackpackItemActionUI.Instance.Hide();

        var lines = BuildAttrLines(eq);
        _title.text = EquipUiText.EquipTitle(eq);
        _body.text = lines.Count > 0 ? string.Join("\n", lines.ToArray()) : "（无额外属性）";
        _panelRt.sizeDelta = new Vector2(PanelWidth, HeadHeight + Mathf.Max(1, lines.Count) * LineHeight + PadBottom);

        gameObject.SetActive(true);
        transform.SetAsLastSibling();          // 抬层级：不抬会被别的层盖住、点不到（项目踩过的坑）
        _panel.SetActive(true);
        PlaceAbove(anchor, _panelRt.sizeDelta.y);
        _openKey = key != null ? key : eq;
    }

    public void Hide()
    {
        _openKey = null;
        if (_panel != null) _panel.SetActive(false);
        gameObject.SetActive(false);
    }

    /// <summary>当前浮框是不是为这个 key 开的 —— 调用方据此做「点同一个 = 关闭，点另一个 = 切过去」。</summary>
    public bool IsOpenFor(object key)
    {
        if (!gameObject.activeSelf) return false;
        if (_panel == null || !_panel.activeSelf) return false;
        return ReferenceEquals(_openKey, key);
    }

    /// <summary>
    /// toggle 入口（2026-10-09 主人拍板）：开着就关、没开就开。
    /// key 传槽位/格子的标识（GameObject 或 EquipInstance 引用都行）。
    /// </summary>
    public static void Toggle(EquipInstance eq, RectTransform anchor, object key, Transform parent = null)
    {
        // 2026-10-09 主人拍板：非战斗场景也要能弹——父节点优先用调用方给的，否则从 anchor 找 Canvas
        if (parent == null && anchor != null)
            parent = anchor.GetComponentInParent<Canvas>()?.transform;
        if (parent == null)
        {
            // 2026-10-09 主人拍板：拿不到 Canvas 父节点要显式报错，不静默 return
            Debug.LogError("[EquipStatTipUI] 找不到 Canvas 父节点，无法弹出装备属性浮框");
            return;
        }
        var tip = Ensure(parent);
        if (tip == null) return;
        if (eq == null || anchor == null) { tip.Hide(); return; }

        if (tip.IsOpenFor(key != null ? key : eq)) { tip.Hide(); return; }
        tip.Show(eq, anchor, key);
    }

    // ===== 属性文本 =====

    /// <summary>
    /// 取装备<b>基础属性</b>行。项目口径（<see cref="EquipStatRollup"/> / GridBackpackSystem 同款）：
    /// attrBonus 中<b>前 baseAttrCount 条</b>是基础属性（吃强化/稀有度系数），其后是随机词条；
    /// 这里只列基础属性（外加模板的 globalBonus），不列词条/附魔。
    /// </summary>
    static List<string> BuildAttrLines(EquipInstance eq)
    {
        var flat = new Dictionary<AttrType, float>();
        var pct = new Dictionary<AttrType, float>();

        int baseCount = Mathf.Clamp(eq.baseAttrCount, 0, eq.attrBonus != null ? eq.attrBonus.Count : 0);
        for (int i = 0; i < baseCount; i++)
            AddAttr(flat, pct, eq.attrBonus[i]);
        // 模板全局加成也算这件装备给的属性（与 EquipDropPopupUI 统计攻击时同口径）
        AddAttr(flat, pct, eq.globalBonus);

        var lines = new List<string>();
        for (int i = 0; i < AttrOrder.Length; i++)
            AppendLine(lines, flat, pct, AttrOrder[i]);
        foreach (var kv in flat)
            if (System.Array.IndexOf(AttrOrder, kv.Key) < 0) AppendLine(lines, flat, pct, kv.Key);
        foreach (var kv in pct)
            if (System.Array.IndexOf(AttrOrder, kv.Key) < 0) AppendLine(lines, flat, pct, kv.Key);

        // 2026-10-09 主人拍板：传奇被动要在 UI 上看得见。
        // skillPassives 的数值早就由 EquipStatRollup.AppendSkillPassives 并入局内汇总并生效了，
        // 这里只补文字：每条一行；没有被动就不追加任何行（不显示「【被动】无」）。
        if (eq.skillPassives != null)
        {
            for (int i = 0; i < eq.skillPassives.Count; i++)
            {
                var p = eq.skillPassives[i];
                if (p == null) continue;
                // 2026-10-09 主人拍板：比例属性（isPercent=false + 小数值）按百分比显示，统一走共享口径
                string pv = EquipUiText.Value(p.attrType, p.value, p.isPercent);
                lines.Add("【被动】" + EquipUiText.Attr(p.attrType) + " " + pv);
            }
        }
        return lines;
    }

    static void AddAttr(Dictionary<AttrType, float> flat, Dictionary<AttrType, float> pct, AttrBonusData b)
    {
        if (b == null) return;
        var map = b.isPercent ? pct : flat;
        float cur = 0f;
        map.TryGetValue(b.attrType, out cur);
        map[b.attrType] = cur + b.value;
    }

    /// <summary>只列 &gt; 0 的属性；固定值与百分比同一 AttrType 各占一行。</summary>
    static void AppendLine(List<string> lines, Dictionary<AttrType, float> flat, Dictionary<AttrType, float> pct, AttrType t)
    {
        float v;
        // 2026-10-09 主人拍板：比例属性（isPercent=false + 小数值）按百分比显示，统一走共享口径
        if (flat.TryGetValue(t, out v) && v > 0f)
            lines.Add(EquipUiText.Attr(t) + " +" + EquipUiText.Value(t, v, false));
        if (pct.TryGetValue(t, out v) && v > 0f)
            lines.Add(EquipUiText.Attr(t) + " +" + EquipUiText.Value(t, v, true));
    }

    // ===== 定位 =====

    /// <summary>把面板底边贴到 anchor 顶边上方，并夹在 BattleUI 矩形内（不出屏）。</summary>
    void PlaceAbove(RectTransform anchor, float height)
    {
        if (_rootRt == null || _panelRt == null || anchor == null) return;
        Canvas.ForceUpdateCanvases();   // 首帧 rect 还没算好，先刷一次再量

        var canvas = GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        var corners = new Vector3[4];
        anchor.GetWorldCorners(corners);
        Vector3 topCenter = (corners[1] + corners[2]) * 0.5f;   // 图标顶边中点
        Vector2 sp = RectTransformUtility.WorldToScreenPoint(cam, topCenter);
        Vector2 lp;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rootRt, sp, cam, out lp))
            lp = Vector2.zero;

        Rect r = _rootRt.rect;
        float halfW = r.width * 0.5f;
        float halfH = r.height * 0.5f;
        float w = _panelRt.sizeDelta.x;
        float x = Mathf.Clamp(lp.x, -halfW + w * 0.5f + EdgePad, halfW - w * 0.5f - EdgePad);
        float y = Mathf.Clamp(lp.y + GapAbove, -halfH + EdgePad, halfH - height - EdgePad);
        _panelRt.anchoredPosition = new Vector2(x, y);
    }

    // ===== 构建辅助 =====

    static Text MakeText(Transform parent, string name, int size, TextAnchor anchor, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = GameFonts.GetChinese();
        t.fontSize = size;
        t.alignment = anchor;
        t.color = color;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var ol = go.AddComponent<Outline>();          // 描边：白字压在深底上也看得清
        ol.effectColor = new Color(0f, 0f, 0f, 0.85f);
        ol.effectDistance = new Vector2(1f, -1f);
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
