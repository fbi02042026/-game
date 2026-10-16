using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>佣兵抽奖池 UI · 卡组模板克隆建卡（top/PoolGrid/Slot0 为模板）与角色图标。</summary>
public partial class MercDrawPoolUI : MonoBehaviour
{
    // ============================================================
    // 模板克隆建卡（2026-10-15 主人拍板：已拥有/未拥有直接复制 top 卡组模板）
    // ============================================================

    /// <summary>卡组模板（top/PoolGrid/Slot0，按名找不到就取第一格）。</summary>
    GameObject CardTemplate()
    {
        for (int i = 0; i < _poolSlots.Count; i++)
            if (_poolSlots[i] != null && _poolSlots[i].name == "Slot0") return _poolSlots[i];
        return _poolSlots.Count > 0 ? _poolSlots[0] : null;
    }

    /// <summary>
    /// 卡的【视觉尺寸】= 模板 sizeDelta × 模板 localScale，全部取局部序列化值，不做世界测量推算。
    /// （2026-10-16 主人拍板：列表卡必须和招募池槽位一模一样大，只复制 Slot0、只换头像和名字。）
    /// </summary>
    static Vector2 TemplateVisualSize(RectTransform tmpl)
    {
        if (tmpl == null) return new Vector2(120f, 180f);
        var s = tmpl.sizeDelta;
        var sc = tmpl.localScale;
        return new Vector2(Mathf.Abs(s.x * sc.x), Mathf.Abs(s.y * sc.y));
    }

    /// <summary>名字牌（namebg）伸出卡底的距离（Content 局部；没伸出返回 0）—— 行距要用它，否则下一行压名字。
    /// 仅用于算行距，不影响卡本身尺寸。建卡时 Root 已激活，世界测量可信。</summary>
    static float TemplateNameDrop(GameObject tmpl, RectTransform content)
    {
        var nb = tmpl != null ? tmpl.transform.Find("namebg") as RectTransform : null;
        var rt = tmpl != null ? tmpl.GetComponent<RectTransform>() : null;
        if (nb == null || rt == null || content == null) return 0f;
        var cb = new Vector3[4];
        nb.GetWorldCorners(cb);
        var cc = new Vector3[4];
        rt.GetWorldCorners(cc);
        float dropWorld = cc[0].y - cb[0].y;              // 卡底 y − 名字牌底 y，>0 = 伸出卡底
        float ky = content.lossyScale.y;
        if (dropWorld <= 0f || ky < 0.0001f) return 0f;
        return dropWorld / ky;
    }

    /// <summary>
    /// 列距：读 top 池 Slot0→Slot1 在【同一父级】里的 anchoredPosition.x 差（主人摆的槽位中心距），
    /// 再按父级→Content 的缩放比换算；读不到退回 卡视觉宽 + GridGap。纯局部值，不做世界测量。
    /// </summary>
    static float TemplateColPitch(GameObject tmpl, RectTransform content, float fallback)
    {
        var s0 = tmpl != null ? tmpl.transform as RectTransform : null;
        var s1 = tmpl != null && tmpl.transform.parent != null
            ? tmpl.transform.parent.Find("Slot1") as RectTransform : null;
        if (s0 == null || s1 == null || content == null) return fallback;
        float parentK = s0.parent.lossyScale.x / content.lossyScale.x;
        if (parentK < 0.0001f || content.lossyScale.x < 0.0001f) return fallback;
        float pitch = Mathf.Abs(s1.anchoredPosition.x - s0.anchoredPosition.x) * parentK;
        return pitch > 1f ? pitch : fallback;
    }

    /// <summary>
    /// 克隆模板建一列表卡：按稀有度排好序传入，**5 列**网格（与 top 池同宽同口径，整行居中），
    /// 行距 = 卡视觉高 + 名字牌下垂量 + 间隔，下一行不压上一行名字牌。
    /// 【尺寸铁律 2026-10-16】克隆卡显式复制 Slot0 的 sizeDelta/localScale/锚点/pivot，
    /// 视觉与招募池槽位分毫不差；代码只写位置 + 换头像/名字。startY=段顶（Content 顶起算），返回段结束 y。
    /// </summary>
    float BuildTemplateCards(RectTransform content, List<string> orderedIds, bool owned, float startY)
    {
        if (content == null || orderedIds == null || orderedIds.Count == 0) return startY;
        var tmplGo = CardTemplate();
        var tmpl = tmplGo != null ? tmplGo.GetComponent<RectTransform>() : null;
        if (tmpl == null || tmplGo.transform.Find("Iconmask") == null)
        {
            Debug.LogError("[MercDrawPoolUI] 找不到卡组模板 top/PoolGrid/Slot0（含 Iconmask），列表无法建卡");
            return startY;
        }

        Vector2 visual = TemplateVisualSize(tmpl);
        float nameDrop = TemplateNameDrop(tmplGo, content);
        float pitchX = TemplateColPitch(tmplGo, content, visual.x + GridGap);
        float rowPitch = visual.y + nameDrop + GridGap;  // 行距含名字牌伸出量，行与行留 GridGap 空隙
        int cols = GridCols;
        float cw = content.rect.width;
        if (cw < 100f) cw = ListViewWidth;               // 布局未跑时按大滚动视口宽兜底（只影响首帧居中）
        float rowW = (cols - 1) * pitchX + visual.x;
        float x0 = Mathf.Max(0f, (cw - rowW) * 0.5f);
        float halfW = visual.x * 0.5f;
        float halfH = visual.y * 0.5f;

        for (int i = 0; i < orderedIds.Count; i++)
        {
            int col = i % cols;
            int row = i / cols;
            MercRosterDefs.TryGetByHireId(orderedIds[i], out var def);
            var card = Object.Instantiate(tmplGo);
            card.name = owned ? "OwnedCard" : "LockedCard";
            card.transform.SetParent(content, false);
            var crt = card.GetComponent<RectTransform>();
            // 显式复制模板几何（与 Slot0 完全一致：200×300 @ scale0.6 = 120×180 视觉）
            crt.anchorMin = new Vector2(0.5f, 1f);
            crt.anchorMax = new Vector2(0.5f, 1f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = tmpl.sizeDelta;
            crt.localScale = tmpl.localScale;
            // 卡中心定位（anchor 参考点=Content 顶边中点）
            crt.anchoredPosition = new Vector2(
                -cw * 0.5f + x0 + col * pitchX + halfW,
                -(startY + row * rowPitch + halfH));
            FillTemplateCard(card, orderedIds[i], def, owned);
            if (owned) _ownedCards.Add(card);
            else _lockedCards.Add(card);
        }
        int rows = (orderedIds.Count + cols - 1) / cols;
        return startY + (rows - 1) * rowPitch + visual.y + nameDrop;
    }

    /// <summary>
    /// 模板克隆卡填数据（【2026-10-16 主人拍板】只复制 Slot0，只替换头像和名字，
    /// 不换框、不换职业徽记、不改任何尺寸）：隐藏池专用 quxiao / suo；
    /// 立绘（GetStand 全身，null 则不显示，不造图）、名字、点击事件（已拥有=上架/替换，未获得=看图鉴）。
    /// </summary>
    void FillTemplateCard(GameObject card, string hireId, MercRosterDefs.Def def, bool owned)
    {
        var qx = card.transform.Find("quxiao");
        if (qx != null) qx.gameObject.SetActive(false);
        var suo = card.transform.Find("suo");
        if (suo != null) suo.gameObject.SetActive(false);

        var icon = card.transform.Find("Iconmask/Icon")?.GetComponent<Image>();
        // 【2026-10-16 主人纠正】卡上放全身立绘（GetStand），不是头像；尺寸/取景保持模板原样
        var sp = MercPortraitSprites.GetStand(hireId);
        if (sp == null && !string.IsNullOrEmpty(def.AssetId)) sp = MercPortraitSprites.GetStand(def.AssetId);
        if (icon != null)
        {
            icon.sprite = sp;
            icon.enabled = sp != null;
            icon.color = owned ? Color.white : new Color(0.40f, 0.40f, 0.40f, 0.9f);
            // 【2026-10-16 主人拍板】只替换形象，尺寸/取景一律保持模板原样（主人已调好），不做适配
        }

        var nameT = card.transform.Find("namebg/Name")?.GetComponent<Text>();
        if (nameT != null) nameT.text = owned ? def.Name : "未获得";

        // 2026-10-16 主人拍板：已获得里在池的佣兵，右上角绿「已上架」标（借用列表卡上隐藏的 quxiao 角标位）
        if (owned && _picked.Contains(hireId))
        {
            var tag = NewImage(card.transform, "OnShelfTag", new Color(0.16f, 0.62f, 0.28f, 0.95f));
            var trt = tag.rectTransform;
            trt.anchorMin = new Vector2(1f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(1f, 1f);
            trt.anchoredPosition = new Vector2(-4f, -4f);
            trt.sizeDelta = new Vector2(110f, 36f);
            MakeText(tag.transform, "Label", "已上架", 21);   // Label 铺满角标、不拦点击
        }

        // 模板本身没有 Button（Slot 只有 Image），克隆后运行时补，口径沿用：已拥有 = 上架/替换，未获得 = 看图鉴
        var img = card.GetComponent<Image>();
        if (img != null) img.raycastTarget = true;
        var btn = card.GetComponent<Button>();
        if (btn == null) btn = card.AddComponent<Button>();
        if (btn.targetGraphic == null && img != null) btn.targetGraphic = img;
        btn.onClick.RemoveAllListeners();
        string hid = hireId;
        if (owned) btn.onClick.AddListener(() => OnOwnedCardClick(hid));
        else btn.onClick.AddListener(() => ShowMercDetail(hid, def));
    }

    /// <summary>隐藏 Role 节点（空格/锁格用，不留上一张的图）。</summary>
    static void HideRoleIcon(Transform cardRoot)
    {
        var roleTr = cardRoot.Find("Role");
        if (roleTr == null) return;
        var rimg = roleTr.GetComponent<Image>();
        if (rimg != null)
        {
            rimg.sprite = null;
            rimg.enabled = false;
        }
        roleTr.gameObject.SetActive(false);
    }

    void ClearCards(List<GameObject> list)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null) Destroy(list[i]);
        list.Clear();
    }
}
