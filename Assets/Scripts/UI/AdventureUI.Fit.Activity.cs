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

    /// <summary>
    /// 瘦屏适配当前的行距倍率（q=1 = 预制体节奏；FitActivityRowsScale 算好后存下，
    /// 排序落位 ApplyActivityRowOrder 共用，保证两条路径行距一致）。
    /// </summary>
    float _activityRowFitQ = 1f;

    /// <summary>活动横条首触基准补录（等比放大与排序落位共用；已录则 no-op）。</summary>
    void EnsureActivityRowFitBase(Transform row)
    {
        if (!(row is RectTransform rt)) return;
        if (!_activityRowFitBase.ContainsKey(rt))
            _activityRowFitBase[rt] = new[] { rt.anchoredPosition, new Vector2(rt.localScale.x, rt.localScale.y) };
    }

    /// <summary>
    /// 把活动副本四条横条按开放状态分组（2026-11-01 主人拍板「全部显示，开放的放上面、未开放的放下面」）：
    /// openRows = 门槛已到且今天开放（IsActivityRowSelectable 同源），closedRows = 其余；
    /// 组内保持预制体先后序（金币/徽记/卷轴/礼物）。
    /// </summary>
    void SplitActivityRowsByOpen(Transform list, List<Transform> openRows, List<Transform> closedRows)
    {
        var kinds = new[] { ActivityDungeonKind.Gold, ActivityDungeonKind.Badge, ActivityDungeonKind.Scroll, ActivityDungeonKind.Gift };
        var names = new[] { ActivityGoldRowName, ActivityBadgeRowName, ActivityScrollRowName, ActivityGiftRowName };
        for (int i = 0; i < kinds.Length; i++)
        {
            var row = list.Find(names[i]);
            if (row == null) continue;
            EnsureActivityRowFitBase(row);
            (IsActivityRowSelectable(kinds[i]) ? openRows : closedRows).Add(row);
        }
    }

    /// <summary>
    /// 活动横条排序落位（2026-11-01 主人拍板；ApplyActivityRowStates 调，标准屏路径）：
    /// 开放组占顶上槽、未开放组占底下槽；槽位 y = 四条横条基准 y 降序（预制体节奏），
    /// 瘦屏适配过则行距按 _activityRowFitQ 等比放大（与 FitActivityRowsScale 同一套基准账）。
    /// 只动 y：x（右列居中）/缩放归 FitActivityRowsScale 管。幂等：目标 y 从基准现算，重复调不累积。
    /// 追查条等四条之列外的行不参与排序，保持各自位置。
    /// </summary>
    void ApplyActivityRowOrder(Transform list)
    {
        var openRows = new List<Transform>();
        var closedRows = new List<Transform>();
        SplitActivityRowsByOpen(list, openRows, closedRows);
        int n = openRows.Count + closedRows.Count;
        if (n == 0) return;
        var slotYs = new List<float>(n);
        foreach (var r in openRows) slotYs.Add(_activityRowFitBase[r][0].y);
        foreach (var r in closedRows) slotYs.Add(_activityRowFitBase[r][0].y);
        slotYs.Sort((a, b) => b.CompareTo(a));   // 降序 = 顶行槽在前
        float topY = slotYs[0];
        var ordered = openRows;
        ordered.AddRange(closedRows);
        for (int i = 0; i < ordered.Count; i++)
        {
            var rt = ordered[i] as RectTransform;
            float y = topY + (slotYs[i] - topY) * _activityRowFitQ;
            var p = rt.anchoredPosition;
            if (!Mathf.Approximately(p.y, y))
                rt.anchoredPosition = new Vector2(p.x, y);
        }
    }

    /// <summary>活动横条等比放大到右列可视宽（ApplyAdventureFit 调；仅瘦屏路径）。</summary>
    void FitActivityRowsScale(RectTransform pageRoot)
    {
        var right = transform.Find("RightContent");
        var list = right != null ? right.Find(ActivityNodeName) : null;
        if (list == null || pageRoot == null || pageRoot.rect.width <= 0f) return;
        float pageW = pageRoot.rect.width;
        // 左边界 = 左列实际占宽（2026-10-24 左列标签随手机宽度等比缩放后，localScale>1 时占宽跟着涨）
        var sidebarRt = transform.Find("LeftSidebar") as RectTransform;
        float leftW = sidebarRt != null && sidebarRt.rect.width > 0f
            ? sidebarRt.rect.width * Mathf.Abs(sidebarRt.localScale.x)
            : LEFT_W;
        float targetW = pageW - leftW - ActivityRowSidePad * 2f;
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
        //    右列可视区 [leftW, pageW] → 中线的容器本地 x = (leftW + pageW)/2 - pageW。
        //    行序（2026-11-01 主人拍板「开放的放上面、未开放的放下面」）：四条活动横条按
        //    SplitActivityRowsByOpen 的分组占槽（槽位 = 基准 y 降序）；追查条等额外行不参与排序，
        //    仍按自身基准 y。倍率记进 _activityRowFitQ，标准屏路径的 ApplyActivityRowOrder 共用。
        float centerX = (leftW + pageW) * 0.5f - pageW;
        var openRowsFit = new List<Transform>();
        var closedRowsFit = new List<Transform>();
        SplitActivityRowsByOpen(list, openRowsFit, closedRowsFit);
        var activityOrdered = new List<Transform>(openRowsFit);
        activityOrdered.AddRange(closedRowsFit);
        var slotYsFit = new List<float>(activityOrdered.Count);
        foreach (var r in activityOrdered) slotYsFit.Add(_activityRowFitBase[r][0].y);
        slotYsFit.Sort((a, b) => b.CompareTo(a));
        foreach (var kv in bgW)
        {
            var row = (RectTransform)kv.Key;
            var rec = _activityRowFitBase[row];
            float s = targetW / kv.Value;                        // 目标视觉宽 ÷ bg 本地宽
            float q = s / Mathf.Max(0.01f, rec[1].x);            // 相对基准缩放的倍率（行距随之等比）
            _activityRowFitQ = q;                                // 四条同 prefab 缩放，q 相同；排序落位共用
            int slotIdx = activityOrdered.IndexOf(row);
            float slotBaseY = slotIdx >= 0 ? slotYsFit[slotIdx] : rec[0].y;
            float y = topBaseY + (slotBaseY - topBaseY) * q;     // 行距按 q 等比放大，顶行不动
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
        _activityRowFitQ = 1f;   // 行距倍率一并回预制体节奏（下次 ApplyActivityRowOrder 重新等比）
    }
}
