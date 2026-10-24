using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 冒险界面 partial —— 2026-10-23 由 AdventureUI.ChestNodes.cs 纯位移拆出（无逻辑改动）
// 职责：岛屿连接点（相邻可见槽位之间铺菱形石板，垫在岛屿下层连成路）
public partial class AdventureUI
{
    // ── 岛屿连接点（2026-10-23 主人「岛屿之间你能加上点连接起来吗」）──
    // 相邻两个可见槽位之间铺一串菱形小石板，垫在岛屿下层，把上下岛连成一条路。
    // 没有现成连线美术 → 运行时生成纯色菱形（要换图：给点挂 sprite 即可，尺寸/颜色改这组常量）。
    const string ConnectorRootName = "连接点";
    /// <summary>连接点边长（旋转 45° 成菱形石板）。</summary>
    const float ConnectorDotSize = 12f;
    /// <summary>连接点中心间距。</summary>
    const float ConnectorDotGap = 34f;
    /// <summary>连接点距两端槽位中心的让位（避开岛图/编号牌本体；2026-10-23 晚 74→52：
    /// 之前直线相邻对被剔到一颗点都没有，主人「最下和最上的没有点」）。</summary>
    const float ConnectorEndInset = 52f;
    /// <summary>连接点颜色（暖沙色，贴海图色调）。</summary>
    static readonly Color ConnectorDotColor = new Color(0.93f, 0.86f, 0.66f, 0.9f);

    /// <summary>按当前可见槽位重建连接点（RefreshChestNodes / FitMainColumnStretch 调；坐标取浮动基准）。</summary>
    void RebuildSlotConnectors()
    {
        if (_guankaRoot == null) return;
        var root = _guankaRoot.Find(ConnectorRootName);
        if (root == null)
        {
            var go = new GameObject(ConnectorRootName, typeof(RectTransform));
            root = go.transform;
            root.SetParent(_guankaRoot, false);
            var rt = (RectTransform)root;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);   // 与槽位同一坐标参照（关卡容器中心）
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }
        for (int c = root.childCount - 1; c >= 0; c--)
            Destroy(root.GetChild(c).gameObject);

        // 可见槽位（stage>0，底→顶）两两连线
        var visible = new List<int>();
        for (int i = 0; i < _slotStage.Count && i < _slotFloatBasePos.Count; i++)
            if (_slotStage[i] > 0 && _slotNodes[i] != null && _slotNodes[i].gameObject.activeSelf)
                visible.Add(i);
        for (int k = 0; k + 1 < visible.Count; k++)
        {
            var pa = _slotFloatBasePos[visible[k]];
            var pb = _slotFloatBasePos[visible[k + 1]];
            var dir = pb - pa;
            float dist = dir.magnitude;
            float usable = dist - ConnectorEndInset * 2f;
            if (usable < ConnectorDotSize) continue;   // 只要放得下至少一颗点就连（不再按间距半格剔）
            var u = dir / Mathf.Max(0.01f, dist);
            int dotCount = Mathf.Max(1, Mathf.RoundToInt(usable / ConnectorDotGap) + 1);
            float step = usable / Mathf.Max(1, dotCount - 1);
            for (int d = 0; d < dotCount; d++)
            {
                var pos = pa + u * (ConnectorEndInset + d * step);
                var dotGo = new GameObject($"点_{k}_{d}", typeof(RectTransform), typeof(Image));
                dotGo.transform.SetParent(root, false);
                var drt = (RectTransform)dotGo.transform;
                drt.anchorMin = drt.anchorMax = drt.pivot = new Vector2(0.5f, 0.5f);
                drt.anchoredPosition = pos;
                drt.sizeDelta = new Vector2(ConnectorDotSize, ConnectorDotSize);
                drt.localRotation = Quaternion.Euler(0f, 0f, 45f);
                var img = dotGo.GetComponent<Image>();
                img.color = ConnectorDotColor;
                img.raycastTarget = false;
            }
        }
        ApplySlotZOrder();   // 连接点容器永远垫底
    }
}
