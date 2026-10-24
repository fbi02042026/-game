using UnityEngine;

// 天赋页竖屏适配（2026-10-24 主人拍板：瘦屏「按宽度最大、高拉长点」）。
//
// 口径：
//   9:16 标准屏 —— 一律不动，保持预制体原样（Panel 700×1100 居中，上下各 90 留白）。
//   更瘦屏     —— ① Panel 整体等比放大到宽度贴满 720 逻辑宽（s = 720/700 ≈ 1.0286）；
//                 ② 高度拉长：sizeDelta.y 撑到「逻辑高 − 上下各 90 留白」（留白与 9:16 原生一致），
//                    有效高 = sizeDelta.y × s。
//
// 为什么只动 Panel 两笔就够：TitleBar / ResourceRow 锚 (0.5,1) 钉顶、Footer 锚 (0.5,0) 钉底、
// Columns 是 (0,0)-(1,1) 上下拉伸锚 —— 拉高 Panel 后两列滚动区自动变长、多露几行，
// 子节点坐标一概不碰（铁律 32）。每次 Show()/Prewarm 现算幂等，不累积。
public partial class TalentUI
{
    const float FitPanelW = 700f;       // TalentUI.prefab Panel 设计宽（2026-10-16 实测）
    const float FitPanelH = 1100f;      // 同上，设计高
    const float FitVerticalMargin = 90f; // 上下留白 = 9:16 原生留白 (1280-1100)/2

    void ApplyFit()
    {
        var panel = panelImage != null
            ? panelImage.rectTransform
            : transform.Find("Panel") as RectTransform;
        if (panel == null)
        {
            Debug.LogError("[TalentUI] 找不到 Panel 节点，瘦屏适配跳过 —— 请检查预制体结构");
            return;
        }

        if (!UiLayoutStretch.IsThinnerScreen())
        {
            // 9:16 标准屏：还原设计值（同时兼作瘦屏态退出的复位，幂等）
            panel.localScale = Vector3.one;
            panel.sizeDelta = new Vector2(FitPanelW, FitPanelH);
            return;
        }

        float logicalH = UiLayoutStretch.ResolveUiLogicalHeight();
        float s = GameConfig.DESIGN_WIDTH / FitPanelW;              // 宽度拉满 720
        float targetH = Mathf.Max(FitPanelH, logicalH - FitVerticalMargin * 2f);
        panel.localScale = new Vector3(s, s, 1f);
        panel.sizeDelta = new Vector2(FitPanelW, targetH / s);      // 有效高 = sizeDelta.y × s
    }
}
