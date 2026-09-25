using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 佣兵碎片卡 —— 主人定版预制体的运行时封装。
///
/// 为什么这么改：主人已经把碎片卡做成预制体，并且把尺寸、位置、锚点、缩放全部调好了
/// （Assets/Resources/Prefabs/other/yongbingsuipian.prefab）：
///     yongbingsuipian  100×100  anchor[0.5,0.5] pos(0,0)    —— 根节点，自身无 Image
///       suipianicon     70×70   anchor[0.5,0.5] pos(0,0)    —— 稀有度框体，最下层
///       mask            70×70   anchor[0.5,0.5] pos(0,0)    —— Image(框体图) + Mask，用自己 Image 的 alpha 当裁剪形状
///         HeadIcon     200×200  anchor[0.5,0.5] pos(0,6.4)  —— 佣兵头像，最上层
/// 主人原话：「suipianicon 这个是碎片的稀有度框体在最下层，HeadIcon 这个是佣兵头像在最上层，
/// 大小我都调整好了 不要再给我变了」。
///
/// 所以本类只干一件事：**换 sprite**。绝不碰 sizeDelta / anchoredPosition / anchorMin /
/// anchorMax / pivot / localScale / localPosition / Mask.showMaskGraphic / 任何 Color。
/// 以前那段「纯代码手搭碎片卡」（新建 Clip / Portrait / Frame 三个节点）已被废弃，
/// 因为它跟主人预制体的层级与尺寸完全对不上，主人发现后要求必须改成实例化预制体。
/// </summary>
public class MercFragmentCardUI
{
    /// <summary>主人碎片预制体在 Resources 下的相对路径（不带扩展名）。</summary>
    public const string PrefabPath = "Prefabs/other/yongbingsuipian";

    public GameObject Root;
    /// <summary>Root 的 RectTransform（只读引用，不改几何）。</summary>
    public RectTransform Rect;
    /// <summary>suipianicon —— 稀有度框体，最下层。</summary>
    public Image Frame;
    /// <summary>mask —— 裁剪底图（它自己的 Image 兼作 Mask 的裁剪形状）。</summary>
    public Image MaskGraphic;
    /// <summary>mask/HeadIcon —— 佣兵头像，最上层。</summary>
    public Image Head;
    public MercRosterDefs.MercRarity Rarity;
    public string HireId;

    /// <summary>
    /// 一键回退开关：false 时 <see cref="Create"/> 直接返回 null（由调用方自行兜底），
    /// 方便主人 A/B 对比「主人预制体」与「老的手搭节点」两种效果。
    /// </summary>
    public static bool EnableOwnerPrefab = true;

    /// <summary>
    /// 实例化主人预制体并完成一次 <see cref="Apply"/>。
    /// parent 为 null、开关关闭、或预制体加载不到时返回 null —— 宁可让调用方兜底，
    /// 也不回退去手搭节点（手搭的节点对不上主人调好的尺寸）。
    /// </summary>
    public static MercFragmentCardUI Create(Transform parent, string name,
        MercRosterDefs.MercRarity rarity, string hireIdOrAssetId)
    {
        if (!EnableOwnerPrefab) return null;
        if (parent == null) return null;

        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("[MercFragmentCardUI] 主人碎片预制体加载失败：Resources/"
                + PrefabPath + " —— 交给调用方兜底，不手搭节点。");
            return null;
        }

        var go = Object.Instantiate(prefab, parent, false);
        go.name = name;

        var card = new MercFragmentCardUI
        {
            Root = go,
            Rect = go.GetComponent<RectTransform>(),
            Frame = FindImage(go.transform, "suipianicon"),
            MaskGraphic = FindImage(go.transform, "mask"),
        };

        // 头像是 mask 的子节点；万一主人以后改结构，退化成从根节点递归找。
        var maskT = card.MaskGraphic != null ? card.MaskGraphic.transform : go.transform;
        card.Head = FindImage(maskT, "HeadIcon");

        if (card.Frame == null || card.MaskGraphic == null || card.Head == null)
        {
            Debug.LogWarning("[MercFragmentCardUI] 主人碎片预制体结构对不上（suipianicon/mask/mask-HeadIcon 缺节点），仍按已有引用继续。");
        }

        card.Apply(rarity, hireIdOrAssetId);
        return card;
    }

    /// <summary>
    /// 把「场景里已经存在的」主人预制体实例绑定成托管对象。只做引用绑定，一个 rect 都不改。
    /// 需要换图时自己再调 <see cref="Apply"/>。
    /// </summary>
    public static MercFragmentCardUI Bind(GameObject existing)
    {
        if (existing == null) return null;

        var card = new MercFragmentCardUI
        {
            Root = existing,
            Rect = existing.GetComponent<RectTransform>(),
            Frame = FindImage(existing.transform, "suipianicon"),
            MaskGraphic = FindImage(existing.transform, "mask"),
        };

        var maskT = card.MaskGraphic != null ? card.MaskGraphic.transform : existing.transform;
        card.Head = FindImage(maskT, "HeadIcon");
        return card;
    }

    /// <summary>
    /// 只改 sprite，绝不改尺寸/位置/锚点/pivot/缩放/颜色/showMaskGraphic。
    /// 框体图必须同时赋给 Frame 和 MaskGraphic：Mask 用自己 Image 的 alpha 当裁剪形状，
    /// 只换 Frame 不换 MaskGraphic 就会拿旧形状的 alpha 去裁，头像裁错。
    /// </summary>
    public void Apply(MercRosterDefs.MercRarity rarity, string hireIdOrAssetId)
    {
        Rarity = rarity;
        HireId = hireIdOrAssetId;

        var sp = LoadFrameSprite(rarity);
        SetSprite(Frame, sp);
        SetSprite(MaskGraphic, sp);

        if (Head != null)
        {
            var head = MercPortraitSprites.GetHead(hireIdOrAssetId);
            if (head == null) head = MercPortraitSprites.GetStand(hireIdOrAssetId);
            SetSprite(Head, head);
        }
    }

    /// <summary>按稀有度取碎片底版 sprite（转调已有 MercGrowSprites，不在本类里另起一套取图逻辑）。</summary>
    public static Sprite LoadFrameSprite(MercRosterDefs.MercRarity rarity)
    {
        return MercGrowSprites.LoadFragmentBase(rarity);
    }

    /// <summary>只换 sprite + 开关 enabled；sprite 为 null 时关掉 Image，别留白框。颜色一律不碰。</summary>
    static void SetSprite(Image img, Sprite sp)
    {
        if (img == null) return;
        img.sprite = sp;
        img.enabled = sp != null;
    }

    /// <summary>按名字递归找子节点再取 Image —— 主人以后往中间插一层也不会断。</summary>
    static Image FindImage(Transform from, string childName)
    {
        var t = FindDeep(from, childName);
        return t != null ? t.GetComponent<Image>() : null;
    }

    static Transform FindDeep(Transform from, string childName)
    {
        if (from == null || string.IsNullOrEmpty(childName)) return null;
        for (int i = 0; i < from.childCount; i++)
        {
            var c = from.GetChild(i);
            if (c.name == childName) return c;
            var hit = FindDeep(c, childName);
            if (hit != null) return hit;
        }
        return null;
    }
}
