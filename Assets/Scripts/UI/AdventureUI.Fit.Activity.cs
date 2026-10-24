using System.Collections.Generic;
using UnityEngine;

// 冒险界面 partial —— 2026-10-23 由 AdventureUI.Fit.cs 纯位移拆出（无逻辑改动）
// 职责：活动副本横条等比放大 / 还原（FitActivityRowsScale、RestoreActivityRowsScale）
public partial class AdventureUI
{
    // ────────────────────────────────────────────────────
    // 活动副本横条等比放大（2026-10-23 主人「活动副本右侧这个标签应该随着宽度等比放大」）：
    // 预制体横条 bg 宽 480 × 行缩放 0.7 = 视觉 336，右列可视宽 550（720 - 左栏 170）只占六成。
    // 治法：目标宽 = 页宽 - 左栏 - 两侧留白；等比 scale = 目标宽 ÷ bg 本地宽（统一缩放不变形）；
    //   行距按同一倍率放大（等比），顶行钉预制体 y；x 居中到右列中线（否则放大后右缘出屏）。
    //   覆盖 活动副本 下全部横条（金币/徽记/卷轴 + 运行时克隆的追查条）。
    // 幂等：首次触碰记 base（pos + scale），标准屏 / 关开关由 RestoreActivityRowsScale 还原。
    // ────────────────────────────────────────────────────

    /// <summary>放大后横条与左栏 / 屏幕右缘的呼吸边距。</summary>
    const float ActivityRowSidePad = 20f;

    /// <summary>横条缩放/移位前的基准（key = 横条 rectTransform；值 = { anchoredPosition, localScaleXY }；还原用）。</summary>
    readonly Dictionary<Transform, Vector2[]> _activityRowFitBase = new Dictionary<Transform, Vector2[]>();

    /// <summary>活动横条等比放大到右列可视宽（ApplyAdventureFit 调；仅瘦屏路径）。</summary>
    void FitActivityRowsScale(RectTransform pageRoot)
    {
        var right = transform.Find("RightContent");
        var list = right != null ? right.Find(ActivityNodeName) : null;
        if (list == null || pageRoot == null || pageRoot.rect.width <= 0f) return;
        float pageW = pageRoot.rect.width;
        float targetW = pageW - LEFT_W - ActivityRowSidePad * 2f;
        if (targetW < 100f) return;

        // 1) 补快照（首次触碰记预制体基准）+ 量各横条 bg 本地宽 + 找顶行基准 y
        float topBaseY = float.NegativeInfinity;
        var bgW = new Dictionary<Transform, float>();
        for (int i = 0; i < list.childCount; i++)
        {
            if (!(list.GetChild(i) is RectTransform row)) continue;
            var bg = row.Find("bg") as RectTransform;
            if (bg == null || bg.rect.width <= 1f)
            {
                Debug.LogError($"[AdventureFit] 活动横条 {row.name} 缺 bg 底图，等比放大无从量宽（缺真值不兜底）");
                continue;
            }
            bgW[row] = bg.rect.width;
            if (!_activityRowFitBase.ContainsKey(row))
                _activityRowFitBase[row] = new[] { row.anchoredPosition, new Vector2(row.localScale.x, row.localScale.y) };
            var rec = _activityRowFitBase[row];
            if (rec[0].y > topBaseY) topBaseY = rec[0].y;
        }
        if (topBaseY == float.NegativeInfinity || bgW.Count == 0) return;

        // 2) 等比缩放 + 行距同倍率放大 + 右列居中。
        //    活动副本 容器原点 = RightContent 右上角 = 页面右上（页 x = pageW），
        //    右列可视区 [LEFT_W, pageW] → 中线的容器本地 x = (LEFT_W + pageW)/2 - pageW。
        float centerX = (LEFT_W + pageW) * 0.5f - pageW;
        foreach (var kv in bgW)
        {
            var row = (RectTransform)kv.Key;
            var rec = _activityRowFitBase[row];
            float s = targetW / kv.Value;                        // 目标视觉宽 ÷ bg 本地宽
            float q = s / Mathf.Max(0.01f, rec[1].x);            // 相对基准缩放的倍率（行距随之等比）
            float y = topBaseY + (rec[0].y - topBaseY) * q;      // 行距按 q 等比放大，顶行不动
            row.anchoredPosition = new Vector2(centerX, y);
            row.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>活动横条还原到预制体基准（标准屏 / 关开关时调）。</summary>
    void RestoreActivityRowsScale()
    {
        foreach (var kv in _activityRowFitBase)
        {
            if (kv.Key == null) continue;
            var rt = (RectTransform)kv.Key;
            rt.anchoredPosition = kv.Value[0];
            rt.localScale = new Vector3(kv.Value[1].x, kv.Value[1].y, 1f);
        }
        _activityRowFitBase.Clear();
    }
}
