using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>佣兵雇佣队列 UI · 列表卡克隆建卡（Panel/Slot 为模板，点卡一次直接弹详情弹窗）与角色图标。</summary>
public partial class MercDrawPoolUI : MonoBehaviour
{
    // ============================================================
    // 模板克隆建卡（2026-10-15 主人拍板：已拥有/未拥有直接复制 top 卡组模板）
    // ============================================================

    /// <summary>列表卡模板（Panel/Slot，BindFromHierarchy 绑定时已隐藏；缺失由 BuildTemplateCards 报错）。</summary>
    GameObject CardTemplate() => _listTemplate;

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
            Debug.LogError("[MercDrawPoolUI] 找不到列表卡模板 Panel/Slot（含 Iconmask），列表无法建卡");
            return startY;
        }
        tmplGo.SetActive(false);   // 模板本体强制隐藏（每次重建前再按一遍，防开窗时亮着演示位）

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
            bool hasDef = MercRosterDefs.TryGetByHireId(orderedIds[i], out var def);
            var card = Object.Instantiate(tmplGo);
            card.SetActive(true);   // 模板是隐藏的，克隆体默认也是灰的，必须手动点亮
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
            FillTemplateCard(card, orderedIds[i], def, hasDef, owned);
            if (owned) _ownedCards.Add(card);
            else _lockedCards.Add(card);
        }
        int rows = (orderedIds.Count + cols - 1) / cols;
        return startY + (rows - 1) * rowPitch + visual.y + nameDrop;
    }

    /// <summary>
    /// 模板克隆卡填数据（【2026-10-16 主人拍板】只复制 Slot，只替换头像和名字，
    /// 不换框、不换职业徽记、不改任何尺寸）：隐藏池专用 quxiao / suo；
    /// 立绘（GetStand 全身，null 则不显示，不造图）、名字、点击事件（点卡一次直接弹详情弹窗）。
    /// </summary>
    /// <param name="hasDef">花名册是否查到该佣兵（Def 是 struct，用 TryGet 返回值判断，不能比 null）。</param>
    void FillTemplateCard(GameObject card, string hireId, MercRosterDefs.Def def, bool hasDef, bool owned)
    {
        var qx = card.transform.Find("quxiao");
        if (qx != null) qx.gameObject.SetActive(false);
        var suo = card.transform.Find("suo");
        if (suo != null) suo.gameObject.SetActive(false);

        // 【2026-10-16 主人拍板】列表卡边框按稀有度换三档卡框（传奇金 / 稀有蓝 / 普通银，
        // 与池格同款：Resources/Art/UI/Tavern/酒馆头像/，唯一入口 MercHireSession.LoadTavernCardFrame）。
        // 模板根图就是卡框（Sliced）；首版误用三选一高卡插画底（frame_common 等，219×789），
        // 压到 200×300 上半身像下面会空一行——主人 22:56 截图指出，已换同款框。
        // 【2026-10-17 主人拍板】已上阵（已在雇佣队列）的已获得卡灰显；未入队的正常亮
        bool inQueue = owned && _picked.Contains(hireId);

        var cardImg = card.GetComponent<Image>();
        if (cardImg != null)
        {
            var frameSp = hasDef ? MercHireSession.LoadTavernCardFrame(def.Rarity) : null;
            if (frameSp != null)
            {
                cardImg.sprite = frameSp;
                cardImg.type = Image.Type.Sliced;
                cardImg.color = Color.white;
            }
            else
            {
                Debug.LogError("[MercDrawPoolUI] 列表卡换稀有度框失败：hireId=" + hireId
                    + "（hasDef=" + hasDef + "，酒馆卡框图加载为空）");
            }
        }

        // 已上阵的卡框整体压灰一档（稀有度框图不变，只调色；与未入队的已获得卡区分）
        if (inQueue && cardImg != null)
            cardImg.color = new Color(0.72f, 0.72f, 0.72f, 1f);

        var icon = card.transform.Find("Iconmask/Icon")?.GetComponent<Image>();
        // 【2026-10-16 主人纠正】卡上放全身立绘（GetStand），不是头像；尺寸/取景保持模板原样
        var sp = MercPortraitSprites.GetStand(hireId);
        if (sp == null && !string.IsNullOrEmpty(def.AssetId)) sp = MercPortraitSprites.GetStand(def.AssetId);
        if (icon != null)
        {
            icon.sprite = sp;
            icon.enabled = sp != null;
            // 未获得=灰立绘；已上阵=立绘压灰一档；未入队的已获得=正常亮（2026-10-17 主人拍板）
            icon.color = !owned ? new Color(0.40f, 0.40f, 0.40f, 0.9f)
                : inQueue ? new Color(0.45f, 0.45f, 0.45f, 1f)
                : Color.white;
            // 【2026-10-16 主人拍板】只替换形象，尺寸/取景一律保持模板原样（主人已调好），不做适配
        }

        var nameT = card.transform.Find("namebg/Name")?.GetComponent<Text>();
        // 【2026-10-18 主人拍板】未获得也显示佣兵名字（真名），缺数据才回落「未获得」
        if (nameT != null)
            nameT.text = hasDef && !string.IsNullOrEmpty(def.Name) ? def.Name : "未获得";

        // 【2026-10-16 主人拍板】名字底牌也按稀有度换三档图（传奇/稀有/普通名字框，与卡框同目录），
        // 只换图不代码改色；取不到报错不静默。
        var nameBgImg = card.transform.Find("namebg")?.GetComponent<Image>();
        var namePlate = hasDef ? MercHireSession.LoadTavernNamePlate(def.Rarity) : null;
        if (nameBgImg != null && namePlate != null)
        {
            nameBgImg.sprite = namePlate;
            nameBgImg.type = Image.Type.Sliced;
            nameBgImg.color = Color.white;
        }
        else if (hasDef)
        {
            Debug.LogError("[MercDrawPoolUI] 列表卡换稀有度名字底牌失败：hireId=" + hireId
                + "（namebg 图加载为空）");
        }

        // 2026-10-16 主人拍板：已获得里在队的佣兵，右上角绿「已入队」标（原「已上架」，2026-10-17 改名）
        if (owned && _picked.Contains(hireId))
        {
            var tag = NewImage(card.transform, "OnShelfTag", new Color(0.16f, 0.62f, 0.28f, 0.95f));
            var trt = tag.rectTransform;
            trt.anchorMin = new Vector2(1f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(1f, 1f);
            trt.anchoredPosition = new Vector2(-4f, -4f);
            trt.sizeDelta = new Vector2(110f, 36f);
            MakeText(tag.transform, "Label", "已入队", 21);   // Label 铺满角标、不拦点击
        }

        // 模板本身没有 Button（Slot 只有 Image），克隆后运行时补：
        // 【2026-10-18 主人拍板】已获得/未获得都是点卡一次直接弹「获得佣兵」详情弹窗
        // （未入队的已获得佣兵弹窗右下带「替换」钮，见 ShowOwnedDetail / MercObtainPopupUI）。
        var img = card.GetComponent<Image>();
        if (img != null) img.raycastTarget = true;
        var btn = card.GetComponent<Button>();
        if (btn == null) btn = card.AddComponent<Button>();
        if (btn.targetGraphic == null && img != null) btn.targetGraphic = img;
        btn.onClick.RemoveAllListeners();
        string hid = hireId;
        if (owned) btn.onClick.AddListener(() => ShowOwnedDetail(hid));
        else btn.onClick.AddListener(() => ShowMercDetail(hid, def));

        // 模板自带的「查看/替换」双钮：入口已改到详情弹窗（上条拍板），不再接线，只保持隐藏
        // （预制体节点不删；克隆体可能继承模板的激活态，这里显式压一遍防残留）。
        var viewNode = card.transform.Find("查看");
        if (viewNode != null) viewNode.gameObject.SetActive(false);
        var swapNode = card.transform.Find("替换");
        if (swapNode != null) swapNode.gameObject.SetActive(false);
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
