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
    /// 模板卡在列表里的排版尺寸：**世界坐标实测**（模板当前世界矩形 ÷ Content 的 lossyScale），
    /// 不用 sizeDelta×localScale 推算 —— 主人在预制体里怎么摆（含缩放）就量出什么样，代码不反推不改。
    /// </summary>
    static Vector2 MeasureInContent(RectTransform rt, RectTransform content)
    {
        if (rt == null || content == null) return new Vector2(CardW, CardH);
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        float kx = content.lossyScale.x, ky = content.lossyScale.y;
        if (kx < 0.0001f || ky < 0.0001f) return new Vector2(CardW, CardH);
        return new Vector2((c[2].x - c[0].x) / kx, (c[1].y - c[0].y) / ky);
    }

    /// <summary>名字牌（namebg）伸出卡底的距离（Content 局部；没伸出返回 0）—— 行距要用它，否则下一行压名字。</summary>
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
    /// 列距：直接量 top 池 Slot0→Slot1 的实际横向间距（与主人摆的 5 格同口径），
    /// 量不到就退回 卡宽 + GridGap。
    /// </summary>
    static float TemplateColPitch(GameObject tmpl, RectTransform content, float fallback)
    {
        var s0 = tmpl != null ? tmpl.transform as RectTransform : null;
        var s1 = tmpl != null && tmpl.transform.parent != null
            ? tmpl.transform.parent.Find("Slot1") as RectTransform : null;
        if (s0 == null || s1 == null || content == null) return fallback;
        var c0 = new Vector3[4];
        s0.GetWorldCorners(c0);
        var c1 = new Vector3[4];
        s1.GetWorldCorners(c1);
        float kx = content.lossyScale.x;
        if (kx < 0.0001f) return fallback;
        float pitch = Mathf.Abs(c1[0].x - c0[0].x) / kx;
        return pitch > 1f ? pitch : fallback;
    }

    /// <summary>
    /// 克隆模板建一列表卡：按稀有度排好序传入，**5 列**网格（与 top 池同宽同口径，整行居中），
    /// 行距 = 卡高 + 名字牌下垂量 + 间隔，下一行绝不压上一行名字牌（2026-10-15 主人要求）。
    /// startY = 本段在 Content 里的起始 y（一条大滚动用）。返回段落结束 y。
    /// </summary>
    float BuildTemplateCards(RectTransform content, List<string> orderedIds, bool owned, float startY)
    {
        if (content == null || orderedIds == null || orderedIds.Count == 0) return startY;
        var tmpl = CardTemplate();
        if (tmpl == null || tmpl.transform.Find("Iconmask") == null)
        {
            Debug.LogError("[MercDrawPoolUI] 找不到卡组模板 top/PoolGrid/Slot0（含 Iconmask），列表无法建卡");
            return startY;
        }

        var size = MeasureInContent(tmpl.GetComponent<RectTransform>(), content);
        float nameDrop = TemplateNameDrop(tmpl, content);
        float pitchX = TemplateColPitch(tmpl, content, size.x + GridGap);
        float rowPitch = size.y + nameDrop + GridGap;     // 行距含名字牌伸出量，行与行留 GridGap 空隙
        int cols = GridCols;
        float cw = content.rect.width;
        if (cw < 100f) cw = 690f;                          // 布局未跑时按面板内容宽兜底（只影响首帧居中）
        float rowW = (cols - 1) * pitchX + size.x;
        float x0 = Mathf.Max(0f, (cw - rowW) * 0.5f);
        for (int i = 0; i < orderedIds.Count; i++)
        {
            int col = i % cols;
            int row = i / cols;
            MercRosterDefs.TryGetByHireId(orderedIds[i], out var def);
            var card = Object.Instantiate(tmpl);
            card.name = owned ? "OwnedCard" : "LockedCard";
            card.transform.SetParent(content, false);
            FillTemplateCard(card, orderedIds[i], def, owned);
            var crt = card.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(0f, 1f);
            crt.pivot = new Vector2(0f, 1f);
            crt.anchoredPosition = new Vector2(x0 + col * pitchX, -(startY + row * rowPitch));
            if (owned) _ownedCards.Add(card);
            else _lockedCards.Add(card);
        }
        int rows = (orderedIds.Count + cols - 1) / cols;
        return startY + (rows - 1) * rowPitch + size.y + nameDrop;
    }

    /// <summary>
    /// 【2026-10-15 主人拍板】稀有度框路径映射：框节点预制体里已摆好（卡本体 Image = 头像框位、
    /// namebg = 名字框位），代码只按稀有度换图，不新建节点。走 <see cref="Art"/> 路径加载出口。
    /// </summary>
    static string TavernFramePath(MercRosterDefs.MercRarity rarity, bool head)
    {
        string tier = rarity == MercRosterDefs.MercRarity.Legendary ? "传奇"
            : rarity == MercRosterDefs.MercRarity.Rare ? "稀有" : "普通";
        return "Art/UI/Tavern/酒馆头像/" + tier + (head ? "头像框" : "名字框");
    }

    /// <summary>已报错的缺图路径只记一次（WarnOnce 口径，避免每刷弹窗刷屏）。</summary>
    static readonly HashSet<string> _frameMissLogged = new HashSet<string>();

    /// <summary>
    /// 按稀有度给卡换框：卡本体 Image ← 稀有度头像框，namebg ← 稀有度名字框。
    /// 取不到图 LogError（每路径一次）并保留预制体原图，不兜底造图、不清空。
    /// </summary>
    static void ApplyRarityFrames(GameObject card, MercRosterDefs.Def def)
    {
        if (card == null || string.IsNullOrEmpty(def.HireId)) return;   // Def 是 struct 不会为 null；HireId 空 = TryGetByHireId 失败给的默认 Struct，挡掉不换框

        var headPath = TavernFramePath(def.Rarity, true);
        var headSp = Art(headPath);
        if (headSp == null)
        {
            if (_frameMissLogged.Add(headPath))
                Debug.LogError("[MercDrawPoolUI] 缺少美术图 " + headPath + "（卡本体头像框不替换，保留预制体原图）");
        }
        else
        {
            var cardImg = card.GetComponent<Image>();
            if (cardImg != null) cardImg.sprite = headSp;
        }

        var namePath = TavernFramePath(def.Rarity, false);
        var nameSp = Art(namePath);
        if (nameSp == null)
        {
            if (_frameMissLogged.Add(namePath))
                Debug.LogError("[MercDrawPoolUI] 缺少美术图 " + namePath + "（名字框不替换，保留预制体原图）");
        }
        else
        {
            var namebg = card.transform.Find("namebg")?.GetComponent<Image>();
            if (namebg != null) namebg.sprite = nameSp;
        }
    }

    /// <summary>
    /// 模板克隆卡填数据：隐藏池专用 quxiao / suo；头像（GetHead，null 报错不造图）、
    /// 名字、Role 四分类图标（T5）、点击事件（口径沿用旧卡）。
    /// 【2026-10-15 主人拍板】卡上框节点预制体里已摆好（卡本体 Image = 头像框位、namebg = 名字框位），
    /// 代码只按稀有度换图（ApplyRarityFrames），不新建节点、不兜底造图。
    /// </summary>
    void FillTemplateCard(GameObject card, string hireId, MercRosterDefs.Def def, bool owned)
    {
        var qx = card.transform.Find("quxiao");
        if (qx != null) qx.gameObject.SetActive(false);
        var suo = card.transform.Find("suo");
        if (suo != null) suo.gameObject.SetActive(false);

        var icon = card.transform.Find("Iconmask/Icon")?.GetComponent<Image>();
        var sp = MercPortraitSprites.GetHead(hireId);
        if (sp == null && !string.IsNullOrEmpty(def.AssetId)) sp = MercPortraitSprites.GetHead(def.AssetId);
        if (icon != null)
        {
            icon.sprite = sp;
            icon.enabled = sp != null;
            icon.color = owned ? Color.white : new Color(0.40f, 0.40f, 0.40f, 0.9f);
        }

        var nameT = card.transform.Find("namebg/Name")?.GetComponent<Text>();
        if (nameT != null) nameT.text = owned ? def.Name : "未获得";

        ApplyRoleIcon(card.transform, def, true);
        ApplyRarityFrames(card, def);

        // 模板本身没有 Button（Slot 只有 Image），克隆后运行时补，口径沿用：已拥有 = 上架/替换，未获得 = 看图鉴
        var img = card.GetComponent<Image>();
        if (img != null) img.raycastTarget = true;
        var btn = card.GetComponent<Button>();
        if (btn == null) btn = card.AddComponent<Button>();
        if (btn.targetGraphic == null && img != null) btn.targetGraphic = img;
        btn.onClick.RemoveAllListeners();
        string hid = hireId;
        if (owned) btn.onClick.AddListener(() => OnOwnedCardClick(hid));
        else btn.onClick.AddListener(() => ShowMercCodex(hid, def));
    }

    /// <summary>
    /// Role 四分类图标 = JobIconResolver.CombatBadge(def.JobName)（T5）；
    /// 取不到就隐藏 Role 节点（CombatBadge 自带 WarnOnce）。show=false 直接隐藏。
    /// </summary>
    static void ApplyRoleIcon(Transform cardRoot, MercRosterDefs.Def def, bool show)
    {
        var roleTr = cardRoot.Find("Role");
        if (roleTr == null) return;
        var rimg = roleTr.GetComponent<Image>();
        var rsp = show && rimg != null && !string.IsNullOrEmpty(def.JobName)
            ? JobIconResolver.CombatBadge(def.JobName) : null;
        if (rimg != null)
        {
            rimg.sprite = rsp;
            rimg.enabled = rsp != null;
        }
        roleTr.gameObject.SetActive(rsp != null);
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
