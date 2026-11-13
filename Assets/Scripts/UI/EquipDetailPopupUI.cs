using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 点装备槽弹出的「单件属性详情框」（T4）。
///
/// <para>视觉对齐 <see cref="EquipReplaceConfirmUI"/> 的 Column 卡片口径（图标 + 名称 + 稀有度框 + 属性一条一行），
/// 同源复用 <see cref="EquipIcons"/> / <see cref="EquipUiText"/> / <see cref="EquipRarityFrame"/> /
/// <see cref="DraftPool.FormatEquipDescLines"/>，不另起一套。</para>
///
/// <para>🔴 与替换弹窗不同：本框<b>带全屏 Dim（raycastTarget=1）</b>，点击 Dim 任意处关闭（满足「点任意地方关闭」）；
/// 且<b>不含</b>换/不换按钮、不含战力行、不含「新」角标（单件纯展示）。</para>
///
/// <para>Canvas 走 <see cref="UICanvasSetup.ApplyPopup"/>（摄像机模式 + 模态），层级高于角色页装备栏
/// （<see cref="GameConfig.UiSort.TownConfirm"/>）。运行时建树，不碰预制体。</para>
/// </summary>
public class EquipDetailPopupUI : MonoBehaviour
{
    static EquipDetailPopupUI _inst;

    GameObject _root;
    Button _dimBtn;
    Image _iconbg;
    Image _icon;
    Text _name;
    Text _info;
    Text _desc;

    public static void Show(EquipInstance eq)
    {
        var ui = Ensure();
        if (ui == null || eq == null) return;
        ui.Fill(eq);
    }

    static EquipDetailPopupUI Ensure()
    {
        if (_inst != null) return _inst;

        var go = new GameObject("EquipDetailPopupUI", typeof(RectTransform));
        DontDestroyOnLoad(go);
        var canvas = go.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownConfirm);
        var ui = go.AddComponent<EquipDetailPopupUI>();
        ui.Build();
        _inst = ui;
        return ui;
    }

    void Build()
    {
        _root = new GameObject("Root", typeof(RectTransform));
        _root.transform.SetParent(transform, false);
        Stretch(_root.GetComponent<RectTransform>());

        // 全屏遮罩（点任意处关闭）
        var dim = UiFactory.NewImage(_root.transform, "Dim", new Color(0f, 0f, 0f, 0.7f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;
        _dimBtn = dim.gameObject.AddComponent<Button>();
        _dimBtn.targetGraphic = dim;
        _dimBtn.onClick.AddListener(Hide);

        // 金色外框（垫在卡片后）
        var frame = UiFactory.NewImage(_root.transform, "Frame", new Color(0.90f, 0.74f, 0.32f, 1f));
        var frt = frame.rectTransform;
        frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0.5f);
        frt.sizeDelta = new Vector2(368f, 488f);
        frame.raycastTarget = false;

        // 深色卡片（不挡射线 → 点卡片也透到 Dim 关闭）
        var card = UiFactory.NewImage(_root.transform, "Card", new Color(0.14f, 0.14f, 0.17f, 0.98f));
        var crt = card.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(360f, 480f);
        card.raycastTarget = false;

        // 稀有度框（图标底）
        _iconbg = UiFactory.NewImage(crt, "Iconbg", Color.white);
        var ibRt = _iconbg.rectTransform;
        ibRt.anchorMin = ibRt.anchorMax = new Vector2(0.5f, 1f);
        ibRt.pivot = new Vector2(0.5f, 1f);
        ibRt.sizeDelta = new Vector2(150f, 150f);
        ibRt.anchoredPosition = new Vector2(0f, -50f);
        _iconbg.preserveAspect = true;
        _iconbg.raycastTarget = false;

        // 图标
        _icon = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        _icon.transform.SetParent(_iconbg.transform, false);
        var iRt = _icon.rectTransform;
        iRt.anchorMin = iRt.anchorMax = iRt.pivot = new Vector2(0.5f, 0.5f);
        iRt.sizeDelta = new Vector2(120f, 120f);
        _icon.preserveAspect = true;
        _icon.raycastTarget = false;

        // 名称（稀有度上色）
        _name = UiFactory.NewText(crt, "Name", "", 26, TextAnchor.MiddleCenter);
        var nRt = _name.rectTransform;
        nRt.anchorMin = nRt.anchorMax = new Vector2(0.5f, 1f);
        nRt.pivot = new Vector2(0.5f, 1f);
        nRt.sizeDelta = new Vector2(330f, 40f);
        nRt.anchoredPosition = new Vector2(0f, -215f);
        _name.raycastTarget = false;

        // 稀有度 / 星级
        _info = UiFactory.NewText(crt, "Info", "", 22, TextAnchor.MiddleCenter);
        var i2Rt = _info.rectTransform;
        i2Rt.anchorMin = i2Rt.anchorMax = new Vector2(0.5f, 1f);
        i2Rt.pivot = new Vector2(0.5f, 1f);
        i2Rt.sizeDelta = new Vector2(330f, 30f);
        i2Rt.anchoredPosition = new Vector2(0f, -255f);
        _info.raycastTarget = false;

        // 属性（一条一行）
        _desc = UiFactory.NewText(crt, "Desc", "", 24, TextAnchor.UpperLeft);
        var dRt = _desc.rectTransform;
        dRt.anchorMin = dRt.anchorMax = new Vector2(0.5f, 0f);
        dRt.pivot = new Vector2(0.5f, 0f);
        dRt.sizeDelta = new Vector2(320f, 200f);
        dRt.anchoredPosition = new Vector2(0f, 20f);
        _desc.raycastTarget = false;

        gameObject.SetActive(false);
    }

    void Fill(EquipInstance eq)
    {
        // 图标 / 框体
        var sp = EquipIcons.Resolve(eq);
        _icon.sprite = sp;
        _icon.enabled = sp != null;
        _iconbg.sprite = EquipRarityFrame.SpriteOf(eq.rarity);
        _iconbg.enabled = true;

        // 名称 + 稀有度上色
        _name.text = EquipUiText.EquipTitle(eq);
        EquipUiText.ApplyRarityColor(_name, eq.rarity);

        // 稀有度 / 星级（强化 >0 才显示 +N，主人口径）
        _info.text = eq.enhanceLevel > 0
            ? $"{EquipUiText.RarityName(eq.rarity)}　★{Mathf.Max(1, eq.star)}　+{eq.enhanceLevel}"
            : $"{EquipUiText.RarityName(eq.rarity)}　★{Mathf.Max(1, eq.star)}";
        EquipUiText.ApplyRarityColor(_info, eq.rarity);

        // 属性一条一行（与替换弹窗同源）
        var lines = DraftPool.FormatEquipDescLines(eq);
        _desc.text = lines.Count > 0 ? string.Join("\n", lines.ToArray()) : "（无附加属性）";

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        GameFonts.ApplyToHierarchy(transform);
    }

    void Hide()
    {
        if (this == null) return;
        if (_root != null) _root.SetActive(false);
        gameObject.SetActive(false);
    }

    static void Stretch(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
