using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 满员换人弹窗「美术效果稿 v1.2」的素材落地脚本。
///
/// 为什么需要它：本套外框/头像框素材（佣兵进阶「进阶标题/框体」、酒馆稀有度头像框、
/// 通用确定/取消按钮）的 .meta guid 都是 56 字符团结长标识，按项目铁律不得写进
/// prefab 的 m_Sprite 引用链路（会断链、不落盘），因此 prefab 里所有 Image 的
/// sprite 槽位留空，统一在 Awake 按路径 Resources.Load 贴图。
///
/// 挂载对象：MercReplaceArtPopup.prefab 根节点。
/// 纯表现层，不改任何玩法字段；EquipReplaceConfirmUI 仍照常按名字绑定按钮/文本。
/// 【只换图口径】只写 Image.sprite，不碰节点宽高 / 缩放 / 锚点 / Image.Type / preserveAspect / 颜色。
///
/// 【2026-11-26 已停用 · 保留仅作回退】主人已在编辑器里把素材 guid 静态挂进 prefab 并调好排版，
/// 本脚本完成过渡使命。MercReplaceConfirmUI.Ensure() 会把它 enabled=false，
/// 避免运行时按老路径重贴覆盖主人手动替换的图、并重设头像框缩放/头像位移。
/// 编辑器右键「重新套用美术素材」仍可用（需手动触发，用于回退到脚本托管版）。
/// </summary>
[DisallowMultipleComponent]
public class MercReplaceArtApply : MonoBehaviour
{
    // ===== 外框（截图同款：佣兵进阶系列，自带九宫格 border）=====
    const string PanelFrameRes = "UI/Art/佣兵进阶/框体";
    const string TitleFrameRes = "UI/Art/佣兵进阶/进阶标题";

    // ===== 三档头像框（酒馆稀有度框，与招募池/竞技场/获得弹窗同款）=====
    const string FrameRareRes = "UI/Art/Tavern/酒馆头像/稀有头像框";
    const string FrameCommonRes = "UI/Art/Tavern/酒馆头像/普通头像框";
    const string FrameLegendRes = "UI/Art/Tavern/酒馆头像/传奇头像框";

    // ===== 按钮（通用执行/放弃）=====
    const string OkBtnRes = "UI/Art/Common/确定按钮";
    const string CancelBtnRes = "UI/Art/Common/取消按钮";

    void Awake()
    {
        Apply();
    }

    /// <summary>编辑器里手动调用：MenuItem 或 Inspector 右键。重导入后重贴一次。</summary>
    [ContextMenu("重新套用美术素材")]
    public void Apply()
    {
        Transform root = transform.Find("Root") != null ? transform.Find("Root") : transform;

        // 1) 窗体底：框体（九宫格）
        Image panel = FindImage(root, "Panel");
        SetSprite(panel, PanelFrameRes);

        // 2) 标题横幅：进阶标题（Simple，等比）
        Image title = FindImage(root, "biaoti");
        SetSprite(title, TitleFrameRes);
        Text titleText = title != null ? title.GetComponent<Text>() : null;
        if (titleText != null) titleText.text = "换下谁？";

        // 3) 三栏头像框：酒馆稀有度框（新栏金 / 在场蓝 / 第三栏银）
        //    三栏都是 prefab 静态节点（第三栏 ColumnOld2 已在 prefab 里克隆好），不再依赖运行时补。
        Transform colNew = FindDeep(root, "ColumnNew");
        Transform colOld1 = FindDeep(root, "ColumnOld");
        Transform colOld2 = FindDeep(root, "ColumnOld2");
        SetColumnArt(colNew, FrameLegendRes);  // 新佣兵金框
        SetColumnArt(colOld1, FrameRareRes);   // 在场蓝框
        SetColumnArt(colOld2, FrameCommonRes); // 第三栏银框

        // 4) 三颗按钮：静态两颗替换 + 一颗放弃
        SetSprite(FindImage(root, "ReplaceButton"), OkBtnRes);
        SetSprite(FindImage(root, "ReplaceButton2"), OkBtnRes);
        SetSprite(FindImage(root, "KeepButton"), CancelBtnRes);
    }

    void SetColumnArt(Transform col, string frameRes)
    {
        if (col == null) return;
        Transform iconBg = FindDeep(col, "Iconbg");
        Transform icon = FindDeep(col, "Icon");

        SetSprite(iconBg != null ? iconBg.GetComponent<Image>() : null, frameRes);
        // 框 / 头像节点的尺寸与位置以 prefab 为准，不在这里缩放或移动
    }

    void SetSprite(Image img, string res)
    {
        if (img == null) return;
        Sprite sp = Resources.Load<Sprite>(res);
        if (sp == null)
        {
            Debug.LogError("[MercReplaceArtApply] 取不到资源：" + res + "（走路径加载，检查 Resources 目录）");
            return;
        }
        // 只换图片，不碰 Image.Type / preserveAspect / color，不碰 RectTransform
        img.sprite = sp;
    }

    static Image FindImage(Transform root, string name)
    {
        Transform t = FindDeep(root, name);
        return t != null ? t.GetComponent<Image>() : null;
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform r = FindDeep(root.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }
}
