using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 城镇入口 · 大地图「资源条」部分（单一职责：只画顶栏 3 种资源的 icon + 数值并刷新）。
/// <para>样式复制主界面 TopBar（icon Image + 数量 Text，见 Prefabs/Town/TopBar.prefab），icon 换成资源图。
/// 战功=UI/Art/Icons/Common/战功、矿石=UI/Art/Icons/Common/矿石、粮草=UI/Art/Icons/Common/粮食
/// （主人 2026-11-22 已放入 Common 目录，56 字符 guid 一律路径加载不碰 guid）。</para>
/// <para>🔴 矿就是石头（主人 2026-11-22）：矿与石材合并为一格「石头」显示 = slgOre + slgStones；
/// 存档两个字段各自保留不动（不动数据结构、不动结算/配表），只改这一处显示层。</para>
/// </summary>
public static partial class SlgMapUI
{
    static Text[] _resValues = new Text[3];

    static void BuildResBar(Transform canvas)
    {
        var bar = NewImage("SlgResBar", canvas, new Color(0.08f, 0.07f, 0.10f, 0.96f));
        var rt = bar.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, 96f);
        rt.anchoredPosition = Vector2.zero;

        var title = NewText("Title", rt, "城镇入口", 34, TextAnchor.MiddleLeft);
        title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        title.rectTransform.pivot = new Vector2(0f, 0.5f);
        title.rectTransform.sizeDelta = new Vector2(220f, 70f);
        title.rectTransform.anchoredPosition = new Vector2(24f, 0f);

        string[] icons = { SlgDefs.MeritIconPath, SlgDefs.OreIconPath, SlgDefs.FoodIconPath };
        string[] names = { "战功", "石头", "粮草" };
        float startX = 236f;
        const float step = 122f;
        for (int i = 0; i < icons.Length; i++)
        {
            var slot = NewRect("Res" + i, rt);
            slot.anchorMin = slot.anchorMax = new Vector2(0f, 0.5f);
            slot.pivot = new Vector2(0f, 0.5f);
            slot.sizeDelta = new Vector2(step - 8f, 72f);
            slot.anchoredPosition = new Vector2(startX + i * step, 0f);

            var icon = NewImage("Icon", slot, Color.white);
            var irt = icon.rectTransform;
            irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
            irt.pivot = new Vector2(0f, 0.5f);
            irt.sizeDelta = new Vector2(58f, 58f);
            irt.anchoredPosition = new Vector2(2f, 0f);
            icon.sprite = LoadSprite(icons[i]);
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var val = NewText("Value", slot, names[i] + " 0", 23, TextAnchor.MiddleLeft);
            var vrt = val.rectTransform;
            vrt.anchorMin = vrt.anchorMax = new Vector2(0f, 0.5f);
            vrt.pivot = new Vector2(0f, 0.5f);
            vrt.sizeDelta = new Vector2(step - 64f, 70f);
            vrt.anchoredPosition = new Vector2(62f, 0f);
            _resValues[i] = val;
        }

        var close = NewButton("CloseBtn", rt, "返回", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f), new Vector2(90f, 64f), new Vector2(-16f, 0f));
        close.GetComponent<Image>().color = new Color(0.45f, 0.18f, 0.18f, 1f);
        close.onClick.AddListener(Hide);
    }

    static void RefreshResBar()
    {
        var d = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        if (d == null) return;
        if (_resValues[0] != null) _resValues[0].text = "战功 " + d.slgMerit;
        // 矿=石头：显示层合并矿+石材；存档两个字段各自保留，不动数据结构与结算
        if (_resValues[1] != null) _resValues[1].text = "石头 " + (d.slgOre + d.slgStones);
        if (_resValues[2] != null) _resValues[2].text = "粮草 " + d.slgFood;
    }
}
