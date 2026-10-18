using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 体力通用确认弹窗（2026-10-18 主人拍板）：邮件领取体力 / 背包使用体力药水 共用。
/// 场景：体力将超出上限时，提示「多余的会浪费」，玩家确认后才执行。
/// 运行时建树不碰 prefab，观感与天赋费用确认窗同一套（羊皮纸底 + 取消/确认双按钮）；
/// 层级 TownPopup(900)，高于邮件详情窗(610)，摄像机模式与 DDOL 口径同其他弹窗。
/// </summary>
public static class StaminaConfirmPopup
{
    static GameObject _root;
    static Text _body;
    static Text _okLabel;
    static Action _onOk;

    /// <summary>弹确认框：body 提示文案（含会浪费多少点），okLabel 确认键文字，onConfirm 点确认后回调。</summary>
    public static void Show(string body, string okLabel, Action onConfirm)
    {
        Ensure();
        if (_root == null) return;
        if (_body != null) _body.text = body;
        if (_okLabel != null) _okLabel.text = okLabel;
        _onOk = onConfirm;
        _root.SetActive(true);
        _root.transform.SetAsLastSibling();
        var canvas = _root.GetComponent<Canvas>();
        if (canvas != null)
            UICanvasSetup.RefreshPopup(canvas, GameConfig.UiSort.TownPopup);
    }

    public static void Close()
    {
        if (_root != null) _root.SetActive(false);
        _onOk = null;
    }

    static void Ensure()
    {
        // Unity 假 null：场景切换/域重载后 _root 被销毁会走重建
        if (_root != null) return;

        var go = new GameObject("StaminaConfirmPopup", typeof(RectTransform));
        UnityEngine.Object.DontDestroyOnLoad(go);
        var canvas = go.AddComponent<Canvas>();
        UICanvasSetup.ApplyPopup(canvas, GameConfig.UiSort.TownPopup, UICanvasSetup.ResolveUiCamera());
        go.AddComponent<GraphicRaycaster>();
        Build(go);
        go.SetActive(false);
        _root = go;
    }

    static void Build(GameObject host)
    {
        // 遮罩（点击=取消）
        var dim = NewRect(host.transform, "Dim");
        Stretch(dim);
        var dimImg = dim.gameObject.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.62f);
        dimImg.raycastTarget = true;
        var dimBtn = dim.gameObject.AddComponent<Button>();
        dimBtn.targetGraphic = dimImg;
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.onClick.AddListener(Close);

        // 羊皮纸底盒（同天赋费用确认窗 560×420）
        var box = NewRect(host.transform, "Box");
        box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
        box.pivot = new Vector2(0.5f, 0.5f);
        box.anchoredPosition = Vector2.zero;
        box.sizeDelta = new Vector2(560f, 420f);
        var boxImg = box.gameObject.AddComponent<Image>();
        boxImg.color = new Color(0.93f, 0.88f, 0.78f, 1f);
        boxImg.raycastTarget = true;

        var title = NewText(box, "Title", "体力提示", 30, new Color(0.25f, 0.15f, 0.1f, 1f));
        var trt = (RectTransform)title.transform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
        trt.pivot = new Vector2(0.5f, 0.5f);
        trt.anchoredPosition = new Vector2(0f, -24f);
        trt.sizeDelta = new Vector2(480f, 44f);
        title.fontStyle = FontStyle.Bold;

        var body = NewText(box, "Body", string.Empty, 22, new Color(0.2f, 0.12f, 0.08f, 1f));
        body.alignment = TextAnchor.UpperLeft;
        var brt = (RectTransform)body.transform;
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.pivot = new Vector2(0.5f, 0.5f);
        brt.anchoredPosition = new Vector2(0f, 8f);
        brt.sizeDelta = new Vector2(500f, 240f);
        _body = body;

        // 取消（左）/ 确认（右），同天赋费用确认窗 200×56
        var cancel = NewRect(box, "Cancel");
        cancel.anchorMin = cancel.anchorMax = new Vector2(0.5f, 0f);
        cancel.pivot = new Vector2(0.5f, 0.5f);
        cancel.anchoredPosition = new Vector2(-110f, 28f);
        cancel.sizeDelta = new Vector2(200f, 56f);
        var cancelImg = cancel.gameObject.AddComponent<Image>();
        cancelImg.color = new Color(0.45f, 0.3f, 0.2f, 1f);
        var cancelBtn = cancel.gameObject.AddComponent<Button>();
        cancelBtn.targetGraphic = cancelImg;
        cancelBtn.onClick.AddListener(Close);
        var cancelTxt = NewText(cancel, "Label", "取消", 26, Color.white);
        Stretch(cancelTxt.rectTransform);

        var ok = NewRect(box, "Confirm");
        ok.anchorMin = ok.anchorMax = new Vector2(0.5f, 0f);
        ok.pivot = new Vector2(0.5f, 0.5f);
        ok.anchoredPosition = new Vector2(110f, 28f);
        ok.sizeDelta = new Vector2(200f, 56f);
        var okImg = ok.gameObject.AddComponent<Image>();
        okImg.color = new Color(0.55f, 0.35f, 0.2f, 1f);
        var okBtn = ok.gameObject.AddComponent<Button>();
        okBtn.targetGraphic = okImg;
        okBtn.onClick.AddListener(OnOkClicked);
        var okTxt = NewText(ok, "Label", "确认", 26, Color.white);
        Stretch(okTxt.rectTransform);
        _okLabel = okTxt;

        GameFonts.ApplyToHierarchy(host.transform);
    }

    static void OnOkClicked()
    {
        var act = _onOk;
        Close();
        act?.Invoke();
    }

    static RectTransform NewRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static Text NewText(Transform parent, string name, string content, int size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = GameFonts.GetChinese();
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.supportRichText = false;
        t.raycastTarget = false;
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
