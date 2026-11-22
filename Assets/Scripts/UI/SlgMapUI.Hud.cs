using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 城镇入口 · 大地图「常驻 HUD」部分（单一职责：右侧在途队伍倒计时栏 + 左下角主城返回按钮）。
/// <para>🔴 右侧栏固定在屏幕右侧（不随地图拖动缩放），每 2 秒由 ArrivalPoller 刷一次倒计时；
/// 左下角主城图点击 = 返回游戏主界面（主人 2026-11-22 拍板）。</para>
/// </summary>
public static partial class SlgMapUI
{
    static RectTransform _marchPanel;
    static readonly List<GameObject> _marchRows = new List<GameObject>();

    /// <summary>清档复位：Canvas 会整体销毁，这里只清静态引用防悬空。</summary>
    static void ResetHud()
    {
        _marchPanel = null;
        _marchRows.Clear();
    }

    static void BuildMarchPanel(Transform canvas)
    {
        var panel = NewImage("MarchPanel", canvas, new Color(0.07f, 0.06f, 0.09f, 0.92f));
        _marchPanel = panel.rectTransform;
        _marchPanel.anchorMin = _marchPanel.anchorMax = new Vector2(1f, 0.5f);
        _marchPanel.pivot = new Vector2(1f, 0.5f);
        _marchPanel.sizeDelta = new Vector2(268f, 520f);
        _marchPanel.anchoredPosition = new Vector2(-16f, 40f);
        panel.raycastTarget = false;

        var title = NewText("Title", _marchPanel, "出征队伍", 27, TextAnchor.MiddleCenter);
        var trt = title.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.sizeDelta = new Vector2(-20f, 60f);
        trt.anchoredPosition = new Vector2(0f, -12f);

        var hint = NewText("Hint", _marchPanel, "点地点选佣兵出征", 19, TextAnchor.MiddleCenter);
        var hrt = hint.rectTransform;
        hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0f);
        hrt.pivot = new Vector2(0.5f, 0f);
        hrt.sizeDelta = new Vector2(-20f, 46f);
        hrt.anchoredPosition = new Vector2(0f, 10f);
    }

    static void RefreshMarchPanel()
    {
        if (_marchPanel == null) return;
        for (int i = 0; i < _marchRows.Count; i++)
            if (_marchRows[i] != null) Object.Destroy(_marchRows[i]);
        _marchRows.Clear();

        var marches = SlgSystem.Marches();
        const float rowH = 92f;
        for (int i = 0; i < marches.Count; i++)
        {
            var m = marches[i];
            var def = SlgSystem.FindPoint(m.pointId);
            bool arrived = SlgSystem.IsArrived(m);
            string txt = (string.IsNullOrEmpty(def.Name) ? m.pointId : def.Name)
                + (m.peaceful ? " · 通商" : "")
                + "\n" + (arrived ? "已抵达 · 结算中" : "行军中 " + FormatSec(SlgSystem.MarchLeftSec(m)));

            var row = NewImage("Row" + i, _marchPanel, new Color(0.15f, 0.13f, 0.18f, 1f));
            var rrt = row.rectTransform;
            rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 1f);
            rrt.pivot = new Vector2(0.5f, 1f);
            rrt.sizeDelta = new Vector2(236f, rowH - 8f);
            rrt.anchoredPosition = new Vector2(0f, -64f - i * rowH);
            row.raycastTarget = false;
            _marchRows.Add(row.gameObject);

            NewText("Time", rrt, txt, 21, TextAnchor.MiddleCenter);
        }
    }

    /// <summary>左下角主城图（按钮）：点击返回游戏主界面。</summary>
    static void BuildHomeButton(Transform canvas)
    {
        var go = new GameObject("HomeCityBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(canvas, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.sizeDelta = new Vector2(150f, 154f);
        rt.anchoredPosition = new Vector2(16f, 16f);

        var img = go.GetComponent<Image>();
        img.sprite = LoadSprite(SlgDefs.HomeCityPath);
        img.preserveAspect = true;
        var btn = go.GetComponent<Button>();
        btn.onClick.AddListener(Hide);

        var label = NewText("Label", rt, "返回主界面", 18, TextAnchor.MiddleCenter);
        var lrt = label.rectTransform;
        lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0f);
        lrt.pivot = new Vector2(0.5f, 0f);
        lrt.sizeDelta = new Vector2(150f, 28f);
        lrt.anchoredPosition = new Vector2(75f, 0f);
    }
}
