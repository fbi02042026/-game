using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家头像「头像1整节点」统一出口（2026-11-03 主人拍板：把头像选择框模板格「头像1」的
/// mask+icon+kuang 三层节点模式原样复制到其他用到头像的地方，不要只换 icon 图）。
/// host（名牌 Icon / 角色页 / 竞技场头像位）只当定位容器 + 保留原 Button：
///   host（Image 置透明，只留射线给点击）
///    ├─ AvatarMask（Image=头像框底 + Mask 裁剪，铺满 host）
///    │    └─ AvatarIcon（Image=头像图，比框大一圈、被 mask 裁出「框里露脸」）
///    └─ AvatarKuang（Image=头像框，盖最上层当外框，常显）
/// 全部子节点不接射线 —— host 上原有 Button（名牌点击开弹窗）不受影响。
/// 幂等：重复调用复用已建节点重设图；旧版 AvatarFace 单层结构发现即清理换代。
/// 图源：头像图 MercPortraitSprites.GetHead；底/框 = Resources UI/Art/Main/头像/头像框底|头像框（192×180）。
/// </summary>
public static class AvatarNodeUI
{
    const string LogTag = "[AvatarNode] ";
    const string SpriteCellBg = "UI/Art/Main/头像/头像框底";
    const string SpriteCellFrame = "UI/Art/Main/头像/头像框";

    /// <summary>host 变「头像1」三件套容器。hireId 传 "player" 或 "H001"~"H024"。</summary>
    public static void Apply(Image host, string hireId)
    {
        if (host == null)
        {
            Debug.LogError(LogTag + "host 头像节点为空，设不了（不兜底）");
            return;
        }
        var hostRt = host.rectTransform;

        // host 本体只当点击层：旧图清掉（视觉全由三件套负责），射线保留给 host 的 Button
        host.sprite = null;
        host.color = new Color(1f, 1f, 1f, 0f);
        host.raycastTarget = true;
        host.preserveAspect = false;

        // 旧版单层 AvatarFace 换代清理
        var oldFace = hostRt.Find("AvatarFace");
        if (oldFace != null) Object.Destroy(oldFace.gameObject);

        var bg = Resources.Load<Sprite>(SpriteCellBg);
        var frame = Resources.Load<Sprite>(SpriteCellFrame);
        if (bg == null) Debug.LogError(LogTag + "底框图取不到：Resources/" + SpriteCellBg + "（不兜底）");
        if (frame == null) Debug.LogError(LogTag + "外框图取不到：Resources/" + SpriteCellFrame + "（不兜底）");

        // ── AvatarMask（Image+Mask 裁剪，铺满 host，同「头像1」的 mask 层）──
        var maskTr = hostRt.Find("AvatarMask");
        if (maskTr == null)
        {
            var go = new GameObject("AvatarMask", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
            go.transform.SetParent(hostRt, false);
            maskTr = go.transform;
        }
        var maskRt = (RectTransform)maskTr;
        StretchFull(maskRt);
        var maskImg = maskTr.GetComponent<Image>();
        maskImg.sprite = bg;
        maskImg.preserveAspect = true;
        maskImg.color = Color.white;
        maskImg.raycastTarget = false;
        maskTr.GetComponent<Mask>().showMaskGraphic = true;   // 底框常显（同弹窗模板格口径）

        // ── AvatarIcon（头像图，比框大一圈、被 mask 裁，同「头像1」的 icon 层）──
        var iconTr = maskRt.Find("AvatarIcon");
        if (iconTr == null)
        {
            var go = new GameObject("AvatarIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(maskRt, false);
            iconTr = go.transform;
        }
        var iconRt = (RectTransform)iconTr;
        iconRt.anchorMin = new Vector2(0.5f, 0.5f);
        iconRt.anchorMax = new Vector2(0.5f, 0.5f);
        iconRt.pivot = new Vector2(0.5f, 0.5f);
        iconRt.anchoredPosition = Vector2.zero;
        // 200×200 头像 vs 192×180 框：icon 比 host 放大 ~5%，居中被 mask 裁出框内露脸
        iconRt.sizeDelta = new Vector2(hostRt.rect.width, hostRt.rect.height) * 1.05f;
        iconRt.localScale = Vector3.one;
        var iconImg = iconTr.GetComponent<Image>();
        iconImg.sprite = MercPortraitSprites.GetHead(hireId);
        if (iconImg.sprite == null)
            Debug.LogError(LogTag + "头像图取不到：" + hireId + "（应在 Resources/UI/Icons/MercHead/ 下），不兜底");
        iconImg.preserveAspect = true;
        iconImg.color = Color.white;
        iconImg.raycastTarget = false;

        // ── AvatarKuang（外框，最上层常显，同「头像1」的 kuang 层）──
        var kuangTr = hostRt.Find("AvatarKuang");
        if (kuangTr == null)
        {
            var go = new GameObject("AvatarKuang", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(hostRt, false);
            kuangTr = go.transform;
        }
        var kuangRt = (RectTransform)kuangTr;
        StretchFull(kuangRt);
        kuangTr.SetAsLastSibling();
        var kuangImg = kuangTr.GetComponent<Image>();
        kuangImg.sprite = frame;
        kuangImg.preserveAspect = true;
        kuangImg.color = Color.white;
        kuangImg.raycastTarget = false;
    }

    /// <summary>铺满宿主（stretch 0~1、offset 清零、缩放归一）。</summary>
    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    /// <summary>
    /// 【2026-11-03 主人拍板「我把主界面的头像换成头像1节点了 你去重新绑下脚本」】
    /// 绑定主人手摆的「头像1」节点（mask→icon + kuang 已在 prefab 摆好，层级同弹窗模板格）：
    /// 只按路径设三张图（mask=头像框底、icon=头像图、kuang=头像框），
    /// 尺寸/位置/缩放零操作 —— 硬规矩：以主人预制体效果为准，代码不碰大小。
    /// 子件缺失 / 缺 Image / 图取不到一律 LogError 不兜底。
    /// host 传 Transform（头像1 根可以没有 Image，名牌调用传 playerAvatarIcon.transform）。
    /// </summary>
    public static void BindPrefabNode(Transform host, string hireId)
    {
        if (host == null)
        {
            Debug.LogError(LogTag + "host 头像节点为空，绑不了（不兜底）");
            return;
        }
        var maskT = FindIn(host, "mask");
        var iconT = FindIn(host, "icon");
        var kuangT = FindIn(host, "kuang");
        if (maskT == null || iconT == null || kuangT == null)
        {
            Debug.LogError(LogTag + "host 下缺 mask/icon/kuang 子件（mask=" + (maskT != null)
                + " icon=" + (iconT != null) + " kuang=" + (kuangT != null) + "），头像1 节点绑不了（不兜底）");
            return;
        }

        var maskImg = maskT.GetComponent<Image>();
        if (maskImg != null)
        {
            maskImg.sprite = Resources.Load<Sprite>(SpriteCellBg);
            if (maskImg.sprite == null) Debug.LogError(LogTag + "底框图取不到：Resources/" + SpriteCellBg + "（不兜底）");
        }
        else Debug.LogError(LogTag + "mask 缺 Image，底框设不了（不兜底）");

        var iconImg = iconT.GetComponent<Image>();
        if (iconImg != null)
        {
            iconImg.sprite = MercPortraitSprites.GetHead(hireId);
            if (iconImg.sprite == null)
                Debug.LogError(LogTag + "头像图取不到：" + hireId + "（应在 Resources/UI/Icons/MercHead/ 下），不兜底");
        }
        else Debug.LogError(LogTag + "icon 缺 Image，头像设不了（不兜底）");

        var kuangImg = kuangT.GetComponent<Image>();
        if (kuangImg != null)
        {
            kuangImg.sprite = Resources.Load<Sprite>(SpriteCellFrame);
            if (kuangImg.sprite == null) Debug.LogError(LogTag + "外框图取不到：Resources/" + SpriteCellFrame + "（不兜底）");
        }
        else Debug.LogError(LogTag + "kuang 缺 Image，外框设不了（不兜底）");
    }

    /// <summary>子树内按名查找（跨层）。</summary>
    static Transform FindIn(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.gameObject.name == name) return child;
            var r = FindIn(child, name);
            if (r != null) return r;
        }
        return null;
    }
}
