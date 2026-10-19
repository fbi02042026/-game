using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 技能简介浮框（2026-10-12 主人拍板：「点击技能出现简单的介绍 可以是个弹窗」）。
///
/// <para>【2026-10-19 主人拍板「点击技能不要弹说明了」→ <b>已整体下线</b>】
/// 唯一调用点（BattleUI 技槽 WireSkillInfoClick）已移除，本类不再被任何入口拉起，
/// 文件保留只为不动 EquipStatTipUI 里的防御性互斥引用；勿再接新调用点。</para>
///
/// <para>点底部技槽 → 在槽位<b>正上方</b>浮出「技能名 + 星级 / 介绍 / 数值 / 冷却 / 耗蓝」；
/// 再点<b>同一个</b>槽 → 关闭（toggle）。空槽点了就把已开的收掉。</para>
///
/// <para>与 <see cref="EquipStatTipUI"/>（装备属性框）同一套做法，且两者<b>互斥</b>：
/// 弹一个就把另一个收掉，避免两个框叠在一起。</para>
///
/// <para>实现约定（沿用 EquipStatTipUI，别另起一套）：</para>
/// <para>- 运行时建树（纯色 Image + Text + UGUI 自带 Outline），不落预制体、不建美术、不 Resources.Load 图片；</para>
/// <para>- 数据与战斗/选人页同一出口：<see cref="PlayerSkillDefs.GetById"/>（desc/numbers/冷却），
/// 星级走 <see cref="RunLoadout.StarOf"/>，耗蓝走 <see cref="PlayerSkillDefs.MpCostOf"/>；
/// <b>不</b>调 <c>RunDraftDirector.BuildRunSkill</c>（它会把倍率乘进注册表里的技能对象，重复调用会叠乘）。</para>
/// </summary>
public class SkillInfoTipUI : MonoBehaviour
{
    public static SkillInfoTipUI Instance { get; private set; }

    /// <summary>浮框宽度（比装备框的 150 宽一档：技能介绍是整句，150 会折成很多行）。</summary>
    const float PanelWidth = 200f;
    const float HeadHeight = 44f;
    const float LineHeight = 28f;
    const float PadBottom = 12f;
    /// <summary>正文/标题字号：与 EquipStatTipUI 同档（2026-10-10 主人定的 20 / 18）。</summary>
    const int BodyFontSize = 20;
    const int TitleFontSize = 18;
    /// <summary>浮框底边离槽位顶边的间距（像素）。</summary>
    const float GapAbove = 10f;
    const float EdgePad = 6f;
    /// <summary>自动收起等待时间（与装备框同档 3 秒；用 unscaled，战斗暂停也照样收）。</summary>
    const float AutoHideSeconds = 3f;

    RectTransform _rootRt;
    GameObject _panel;
    RectTransform _panelRt;
    Text _title;
    Text _body;
    /// <summary>当前浮框是为谁开的（调用方传的 key，一般传槽位 GameObject）。</summary>
    object _openKey;
    Coroutine _autoHide;

    /// <summary>挂在 BattleUI 下（保证在 Canvas 内）。重复调用返回同一个实例。</summary>
    public static SkillInfoTipUI Ensure(Transform parent)
    {
        if (Instance != null) return Instance;
        if (parent == null) return null;

        var go = new GameObject("SkillInfoTipUI", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Instance = go.AddComponent<SkillInfoTipUI>();
        Instance.Build();
        return Instance;
    }

    void Build()
    {
        _rootRt = GetComponent<RectTransform>();
        Stretch(_rootRt);   // 铺满 BattleUI：后续用 anchoredPosition 在它内部定位

        // 与 EquipStatTipUI 同口径：自带嵌套 Canvas + 高 sortingOrder，永远压在最上层
        // （高于 BattleSettlement 990，低于 Toast 11000 / FullscreenFx 12000）。
        var rootCanvas = gameObject.GetComponent<Canvas>();
        if (rootCanvas == null) rootCanvas = gameObject.AddComponent<Canvas>();
        rootCanvas.overrideSorting = true;
        rootCanvas.sortingOrder = 1000;
        // 刻意不加 GraphicRaycaster：面板与文本都 raycastTarget=false，不抢下层点击。

        _panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        _panel.transform.SetParent(transform, false);
        var pImg = _panel.GetComponent<Image>();
        pImg.color = new Color(0.12f, 0.12f, 0.15f, 0.96f);
        pImg.raycastTarget = false;
        _panelRt = _panel.GetComponent<RectTransform>();
        _panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        _panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        _panelRt.pivot = new Vector2(0.5f, 0f);          // 底边对齐定位点 → 天然浮在槽位上方
        _panelRt.sizeDelta = new Vector2(PanelWidth, 120f);

        // 技能名（顶行）
        _title = MakeText(_panel.transform, "Title", TitleFontSize, TextAnchor.MiddleCenter, Color.white);
        var tRt = _title.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 1f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.pivot = new Vector2(0.5f, 1f);
        tRt.anchoredPosition = new Vector2(0f, -8f);
        tRt.sizeDelta = new Vector2(-24f, 26f);

        // 介绍 / 数值（多行）
        _body = MakeText(_panel.transform, "Body", BodyFontSize, TextAnchor.UpperLeft, new Color(0.86f, 0.86f, 0.9f, 1f));
        var bRt = _body.GetComponent<RectTransform>();
        bRt.anchorMin = Vector2.zero;
        bRt.anchorMax = Vector2.one;
        bRt.offsetMin = new Vector2(12f, PadBottom * 0.5f);
        bRt.offsetMax = new Vector2(-12f, -HeadHeight);

        // 项目五坑第五条：运行时建的弹窗默认隐藏，否则一创建就糊在屏幕上
        gameObject.SetActive(false);
    }

    // ===== 对外入口 =====

    /// <summary>
    /// toggle 入口（唯一）：开着就关、没开就开。key 传槽位 GameObject。
    /// </summary>
    public static void Toggle(string skillId, RectTransform anchor, object key, Transform parent = null)
    {
        if (parent == null && anchor != null)
            parent = anchor.GetComponentInParent<Canvas>()?.transform;
        if (parent == null)
        {
            // 与 EquipStatTipUI 同口径：拿不到 Canvas 父节点显式报错，不静默 return
            Debug.LogError("[SkillInfoTipUI] 找不到 Canvas 父节点，无法弹出技能简介浮框");
            return;
        }
        var tip = Ensure(parent);
        if (tip == null) return;
        if (string.IsNullOrEmpty(skillId) || anchor == null) { tip.Hide(); return; }

        if (tip.IsOpenFor(key)) { tip.Hide(); return; }
        tip.Show(skillId, anchor, key);
    }

    public bool IsOpenFor(object key)
    {
        if (!gameObject.activeSelf) return false;
        if (_panel == null || !_panel.activeSelf) return false;
        return ReferenceEquals(_openKey, key);
    }

    public void Show(string skillId, RectTransform anchor, object key)
    {
        if (string.IsNullOrEmpty(skillId) || anchor == null) return;

        // 与装备属性框互斥（2026-10-12）：两个框都是浮在槽位上方，同时开会叠在一起
        if (EquipStatTipUI.Instance != null) EquipStatTipUI.Instance.Hide();

        string title;
        var lines = BuildLines(skillId, out title);
        _title.text = title;
        _body.text = string.Join("\n", lines.ToArray());
        _panelRt.sizeDelta = new Vector2(PanelWidth, HeadHeight + Mathf.Max(1, lines.Count) * LineHeight + PadBottom);

        gameObject.SetActive(true);
        transform.SetAsLastSibling();          // 抬层级：不抬会被别的层盖住、点不到（项目踩过的坑）
        _panel.SetActive(true);
        PlaceAbove(anchor, _panelRt.sizeDelta.y);
        _openKey = key;

        // 与装备框同口径：到点自己收起；再点同一个槽仍能立刻关（toggle 走 Hide），不用等计时器
        if (_autoHide != null) StopCoroutine(_autoHide);
        _autoHide = StartCoroutine(CoAutoHide());
    }

    public void Hide()
    {
        if (_autoHide != null) { StopCoroutine(_autoHide); _autoHide = null; }
        _openKey = null;
        if (_panel != null) _panel.SetActive(false);
        gameObject.SetActive(false);
    }

    System.Collections.IEnumerator CoAutoHide()
    {
        float t = 0f;
        while (t < AutoHideSeconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        _autoHide = null;
        Hide();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ===== 文案 =====

    /// <summary>
    /// 技能简介行。<b>唯一出口</b>：id → 真 id → PlayerSkillDefs.Def。
    /// 星级只用来显示在标题上（数值行不重算星级倍率，避免与战斗两套口径）。
    /// </summary>
    static List<string> BuildLines(string skillId, out string title)
    {
        string real = PlayerSkillDefs.ResolveRealSkillId(skillId);
        var def = PlayerSkillDefs.GetById(real);

        title = (def != null && !string.IsNullOrEmpty(def.displayName)) ? def.displayName : real;
        int star = RunLoadout.StarOf(real);
        if (star > 0) title += "（" + star + " 星）";

        var lines = new List<string>();
        if (def == null)
        {
            lines.Add("（暂无介绍）");
            return lines;
        }
        if (!string.IsNullOrEmpty(def.desc)) lines.Add(def.desc);
        if (!string.IsNullOrEmpty(def.numbers)) lines.Add(def.numbers);
        if (def.cooldown > 0f) lines.Add("冷却 " + def.cooldown.ToString("0.#") + " 秒");
        if (def.duration > 0f) lines.Add("持续 " + def.duration.ToString("0.#") + " 秒");
        float mp = PlayerSkillDefs.MpCostOf(real);   // >0 才是有蓝；0 = 纯 CD；-1 = id 不存在
        if (mp > 0f) lines.Add("耗蓝 " + mp.ToString("0"));
        if (lines.Count == 0) lines.Add("（暂无介绍）");
        return lines;
    }

    // ===== 定位 =====

    /// <summary>把面板底边贴到 anchor 顶边上方，并夹在父矩形内（不出屏）。</summary>
    void PlaceAbove(RectTransform anchor, float height)
    {
        if (_rootRt == null || _panelRt == null || anchor == null) return;
        Canvas.ForceUpdateCanvases();   // 首帧 rect 还没算好，先刷一次再量

        var canvas = GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        var corners = new Vector3[4];
        anchor.GetWorldCorners(corners);
        Vector3 topCenter = (corners[1] + corners[2]) * 0.5f;   // 槽位顶边中点
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
