using UnityEngine;

// 天赋页竖屏适配（2026-10-24 主人拍板：瘦屏「按宽度最大、高拉长点」）。
//
// 口径：
//   9:16 标准屏 —— 一律不动，保持预制体原样（Panel 700×1100 居中，上下各 90 留白）。
//   更瘦屏     —— ① Panel 整体等比放大到宽度贴满 720 逻辑宽（s = 720/700 ≈ 1.0286）；
//                 ② 高度拉长：sizeDelta.y 撑到「逻辑高 − 上下各 90 留白」（留白与 9:16 原生一致），
//                    有效高 = sizeDelta.y × s。
//
// 【2026-11-11 主人拍板「天赋界面在手机上太大了 把下面的五个入口都挡住了 缩小20%」】
// 两种屏都整体再乘 0.8（含高），有效尺寸 = 原有效尺寸 × 0.8；只动 Panel 的 localScale，
// 子节点坐标一概不碰。每次 Show()/Prewarm 现算幂等，不累积。
//
// 为什么只动 Panel 两笔就够：TitleBar / ResourceRow 锚 (0.5,1) 钉顶、Footer 锚 (0.5,0) 钉底、
// Columns 是 (0,0)-(1,1) 上下拉伸锚 —— 拉高 Panel 后两列滚动区自动变长、多露几行，
// 子节点坐标一概不碰（铁律 32）。
public partial class TalentUI
{
    const float FitPanelW = 700f;       // TalentUI.prefab Panel 设计宽（2026-10-16 实测）
    const float FitPanelH = 1100f;      // 同上，设计高
    const float FitVerticalMargin = 90f; // 上下留白 = 9:16 原生留白 (1280-1100)/2
    const float FitScaleFactor = 0.8f;  // 【2026-11-11】整体缩 20%（手机上太大挡住底部五入口）

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
            // 9:16 标准屏：设计值 × 0.8（同时兼作瘦屏态退出的复位，幂等）
            panel.localScale = Vector3.one * FitScaleFactor;
            panel.sizeDelta = new Vector2(FitPanelW, FitPanelH);
            return;
        }

        float logicalH = UiLayoutStretch.ResolveUiLogicalHeight();
        float s = GameConfig.DESIGN_WIDTH / FitPanelW;              // 宽度比（不含缩小系数）
        float targetH = Mathf.Max(FitPanelH, logicalH - FitVerticalMargin * 2f);
        // localScale = s × 0.8：有效宽 = 700 × s × 0.8 = 720 × 0.8；sizeDelta.y 仍按 s 反算，
        // 有效高 = (targetH / s) × s × 0.8 = targetH × 0.8 —— 整体（含高）缩 20%
        panel.localScale = new Vector3(s, s, 1f) * FitScaleFactor;
        panel.sizeDelta = new Vector2(FitPanelW, targetH / s);
    }
}
