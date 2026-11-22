using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 城镇入口 · 大地图「选兵出征界面」部分（单一职责：点地点后选 3 名闲置佣兵 → 出发）。
/// <para>主人 2026-11-22 拍板流程：点地图地点 → 弹选兵界面 → 勾选 3 名闲置佣兵 → 点出发；
/// 只要粮草 + 金币够就能出发。队伍由玩家自选（不再自动取前 3 名）。</para>
/// <para>🔴 佣兵头像走 MercPortraitSprites.GetHead（项目统一出口）；地图点位图标走 SlgDefs.IconPath，两者不混用。</para>
/// </summary>
public static partial class SlgMapUI
{
    static GameObject _selPanel;
    static RectTransform _selContent;
    static Text _selCount, _selCost, _selTip;
    static Button _selGo, _selTrade;
    static readonly List<GameObject> _selCards = new List<GameObject>();
    static readonly List<string> _selPicked = new List<string>();
    static List<string> _selIdle;
    static string _selPointId;

    /// <summary>清档复位：Canvas 会整体销毁，这里只清静态引用防悬空。</summary>
    static void ResetSelect()
    {
        _selPanel = null; _selContent = null; _selCount = null; _selCost = null;
        _selTip = null; _selGo = null; _selTrade = null;
        _selCards.Clear(); _selPicked.Clear(); _selIdle = null; _selPointId = null;
    }

    static void BuildSelectPanel(Transform canvas)
    {
        _selPanel = new GameObject("SelectPanel", typeof(RectTransform));
        _selPanel.transform.SetParent(canvas, false);
        Stretch((RectTransform)_selPanel.transform);

        var veil = NewImage("Veil", _selPanel.transform, new Color(0f, 0f, 0f, 0.66f));
        Stretch(veil.rectTransform);
        veil.raycastTarget = true;

        var panel = NewImage("Panel", _selPanel.transform, new Color(0.11f, 0.10f, 0.13f, 1f));
        var prt = panel.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(640f, 820f);
        prt.anchoredPosition = Vector2.zero;

        var title = NewText("Title", prt, "选择出征佣兵（3 名闲置）", 36, TextAnchor.MiddleCenter);
        var trt = title.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.sizeDelta = new Vector2(-40f, 80f);
        trt.anchoredPosition = new Vector2(0f, -12f);

        _selCount = NewText("Count", prt, "已选 0/3", 27, TextAnchor.MiddleCenter);
        var crt = _selCount.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.sizeDelta = new Vector2(-40f, 52f);
        crt.anchoredPosition = new Vector2(0f, -92f);

        _selCost = NewText("Cost", prt, "", 23, TextAnchor.MiddleCenter);
        var costRt = _selCost.rectTransform;
        costRt.anchorMin = costRt.anchorMax = new Vector2(0.5f, 1f);
        costRt.pivot = new Vector2(0.5f, 1f);
        costRt.sizeDelta = new Vector2(-40f, 48f);
        costRt.anchoredPosition = new Vector2(0f, -136f);

        // 滚动列表（标准三层：ScrollArea[ScrollRect] → Viewport[Image+Mask] → Content）：
        // 🔴 ScrollRect 不能挂在 Viewport 自己身上又指自己当 viewport，否则裁剪/滚动异常。
        var area = NewRect("ScrollArea", prt);
        area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one;
        area.pivot = new Vector2(0.5f, 0.5f);
        area.offsetMin = new Vector2(24f, 130f); area.offsetMax = new Vector2(-24f, -188f);
        var scroll = area.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        var vp = NewRect("Viewport", area);
        Stretch(vp);
        var vpImg = vp.gameObject.AddComponent<Image>();
        vpImg.color = new Color(0.06f, 0.05f, 0.08f, 1f);
        vp.gameObject.AddComponent<RectMask2D>();

        _selContent = NewRect("Content", vp);
        _selContent.anchorMin = new Vector2(0f, 1f); _selContent.anchorMax = new Vector2(1f, 1f);
        _selContent.pivot = new Vector2(0.5f, 1f);
        _selContent.anchoredPosition = Vector2.zero;
        scroll.viewport = vp;
        scroll.content = _selContent;

        _selTip = NewText("Tip", prt, "", 21, TextAnchor.MiddleCenter);
        var tipt = _selTip.rectTransform;
        tipt.anchorMin = tipt.anchorMax = new Vector2(0.5f, 0f);
        tipt.pivot = new Vector2(0.5f, 0f);
        tipt.sizeDelta = new Vector2(-40f, 52f);
        tipt.anchoredPosition = new Vector2(0f, 76f);

        _selGo = NewButton("Go", prt, "出发", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(220f, 70f), new Vector2(-130f, 18f));
        _selGo.GetComponent<Image>().color = new Color(0.20f, 0.48f, 0.26f, 1f);
        _selGo.onClick.AddListener(() => OnSelectGo(false));

        _selTrade = NewButton("Trade", prt, "通商", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(180f, 70f), new Vector2(120f, 18f));
        _selTrade.GetComponent<Image>().color = new Color(0.22f, 0.38f, 0.58f, 1f);
        _selTrade.onClick.AddListener(() => OnSelectGo(true));

        var cancel = NewButton("Cancel", prt, "取消", new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(1f, 1f), new Vector2(110f, 50f), new Vector2(-10f, -10f));
        cancel.onClick.AddListener(CloseSelect);

        _selPanel.SetActive(false);
    }

    /// <summary>点地点 → 弹选兵界面。不可用原因直接复用确认弹窗提示。</summary>
    static void OpenSelect(string pointId)
    {
        var def = SlgSystem.FindPoint(pointId);
        if (string.IsNullOrEmpty(def.Id)) return;
        if (!SlgSystem.CanDispatch(pointId, out string reason, out var idle))
        {
            OpenModal(def.Name, reason, "知道了", null, null, null, null);
            return;
        }

        _selPointId = pointId;
        _selIdle = idle;
        _selPicked.Clear();
        BuildSelectCards(def);
        RefreshSelect();
        _selPanel.SetActive(true);
        _selPanel.transform.SetAsLastSibling();
    }

    static void BuildSelectCards(SlgDefs.PointDef def)
    {
        for (int i = 0; i < _selCards.Count; i++)
            if (_selCards[i] != null) Object.Destroy(_selCards[i]);
        _selCards.Clear();

        const int cols = 3;
        const float cardW = 186f, cardH = 140f;
        int rows = Mathf.Max(1, (_selIdle.Count + cols - 1) / cols);
        _selContent.sizeDelta = new Vector2(0f, rows * cardH + 20f);

        for (int i = 0; i < _selIdle.Count; i++)
        {
            string id = _selIdle[i];
            int col = i % cols, row = i / cols;
            var go = new GameObject("Merc_" + id, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_selContent, false);
            _selCards.Add(go);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(cardW, cardH);
            rt.anchoredPosition = new Vector2(col * (cardW + 6f), -row * cardH);

            var head = NewImage("Head", rt, Color.white);
            var hrt = head.rectTransform;
            hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 1f);
            hrt.pivot = new Vector2(0.5f, 1f);
            hrt.sizeDelta = new Vector2(92f, 92f);
            hrt.anchoredPosition = new Vector2(0f, -8f);
            head.sprite = MercPortraitSprites.GetHead(id);
            head.preserveAspect = true;
            head.raycastTarget = false;

            var name = NewText("Name", rt, SlgDefs.MercName(id), 21, TextAnchor.MiddleCenter);
            var nrt = name.rectTransform;
            nrt.anchorMin = nrt.anchorMax = new Vector2(0f, 0f);
            nrt.pivot = new Vector2(0.5f, 0f);
            nrt.sizeDelta = new Vector2(cardW - 10f, 40f);
            nrt.anchoredPosition = new Vector2(cardW * 0.5f, 4f);

            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(() => TogglePick(id));
        }
        _selTip.text = "目标：" + def.Name + "（闲置 " + _selIdle.Count + " 名）";
        _selTrade.gameObject.SetActive(def.Type == SlgDefs.PointType.City);
    }

    static void TogglePick(string id)
    {
        if (_selPicked.Contains(id)) _selPicked.Remove(id);
        else if (_selPicked.Count < SlgDefs.SquadSize) _selPicked.Add(id);
        else { GlobalToastUI.ShowQueued("最多选 " + SlgDefs.SquadSize + " 名"); return; }
        RefreshSelect();
    }

    static void RefreshSelect()
    {
        _selCount.text = "已选 " + _selPicked.Count + "/" + SlgDefs.SquadSize;
        var d = SaveSystem.Instance != null ? SaveSystem.Instance.Data : null;
        long gold = d != null ? ResourceWallet.Get(d, ResourceWallet.ResourceType.Gold) : 0;
        int food = d != null ? d.slgFood : 0;
        _selCost.text = "出征消耗：粮草 " + SlgDefs.DispatchFoodCost + " / 金币 " + SlgDefs.DispatchGoldCost
            + "　（现有 粮草 " + food + " / 金币 " + gold + "）";

        for (int i = 0; i < _selCards.Count; i++)
        {
            var btn = _selCards[i].GetComponent<Button>();
            string id = _selIdle != null && i < _selIdle.Count ? _selIdle[i] : null;
            bool on = id != null && _selPicked.Contains(id);
            btn.GetComponent<Image>().color = on ? new Color(0.20f, 0.48f, 0.26f, 1f) : new Color(0.20f, 0.19f, 0.23f, 1f);
        }
        bool ok = _selPicked.Count == SlgDefs.SquadSize
                  && SlgSystem.HasDispatchCost(out _)
                  && !SlgSystem.IsPointOnCooldown(_selPointId)
                  && SlgSystem.MarchForPoint(_selPointId) == null;
        _selGo.interactable = ok;
        _selTrade.interactable = ok;
    }

    static void OnSelectGo(bool peaceful)
    {
        if (_selPicked.Count != SlgDefs.SquadSize)
        {
            GlobalToastUI.ShowQueued("请先选满 " + SlgDefs.SquadSize + " 名闲置佣兵");
            return;
        }
        if (!SlgSystem.HasDispatchCost(out string reason))
        {
            GlobalToastUI.ShowQueued(reason);
            return;
        }
        var chosen = new List<string>(_selPicked);
        CloseSelect();
        var r = SlgSystem.Dispatch(_selPointId, peaceful, chosen);
        OpenModal(r.Ok ? "队伍已出发" : "无法出发", r.Message, "确定", null, null, null, null);
        Refresh();
    }

    static void CloseSelect()
    {
        if (_selPanel != null) _selPanel.SetActive(false);
        _selPicked.Clear();
    }
}
