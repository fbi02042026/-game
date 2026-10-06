using UnityEngine;

/// <summary>
/// 玩家脚底阴影的透明度调节 —— <b>2026-10-06 主人拍板：玩家阴影透明度减少 10%</b>。
///
/// <para>真源：SPUM 玩家预制体 <c>Assets/SPUM/Resources/Units/wanjia.prefab</c> 里的 <c>Shadow</c> 节点，
/// 预制体给的 alpha = <b>0.5608</b>（143/255）。按铁律「预制体有值 → 代码不写」，本类<b>不另起一套颜色</b>，
/// 只按主人这一次的要求，在运行时把预制体那个值<b>乘一次系数</b>：0.5608 × 0.9 ≈ 0.5047。</para>
///
/// <para>⚠ <b>只做一次</b>：用 renderer 的 instanceID 记账，防止每次重进战斗 / 重建单位时反复乘，
/// 越来越淡（那种「阴影慢慢消失」的 bug 就是这么来的）。</para>
///
/// <para>⚠ <b>fail closed</b>：在玩家单位下找不到任何 Shadow 节点 → <c>LogError</c> 一次并报出
/// 「单位下所有子节点名」，绝不静默跳过 —— 主人要靠这条日志发现「shadow 节点是不是被谁删了」。</para>
///
/// <para>⚠ 只认<b>名字里带 shadow</b> 的渲染器，绝不按「第一个子物体」猜图（铁律 11）。</para>
/// </summary>
public static class UnitShadowAlpha
{
    /// <summary>玩家阴影透明度系数：减少 10% → ×0.9（主人原话「玩家的阴影透明度减少10%」）。</summary>
    public const float PlayerShadowAlphaScale = 0.9f;

    static readonly System.Collections.Generic.HashSet<int> _applied =
        new System.Collections.Generic.HashSet<int>();

    static bool _warned;

    /// <summary>
    /// 把玩家单位下所有 Shadow 渲染器的 alpha 乘一次 <see cref="PlayerShadowAlphaScale"/>。
    /// 幂等：同一个渲染器只处理一次。
    /// </summary>
    public static void ApplyPlayer(Transform unitRoot)
    {
        if (unitRoot == null) return;

        var srs = unitRoot.GetComponentsInChildren<SpriteRenderer>(true);
        bool hit = false;

        for (int i = 0; i < srs.Length; i++)
        {
            var sr = srs[i];
            if (sr == null) continue;
            if (!IsShadowNode(sr.transform)) continue;

            int id = sr.GetInstanceID();
            if (_applied.Contains(id)) continue;
            _applied.Add(id);

            var c = sr.color;
            float before = c.a;
            c.a = Mathf.Clamp01(c.a * PlayerShadowAlphaScale);
            sr.color = c;
            hit = true;

            Debug.Log($"[UnitShadowAlpha] 玩家阴影 {NodePath(sr.transform)}：" +
                      $"alpha {before:F4} → {c.a:F4}（×{PlayerShadowAlphaScale}，2026-10-06 主人拍板 -10%）");
        }

        if (!hit) WarnNoShadow(unitRoot);
    }

    /// <summary>自身或父链里有名字带「shadow」的节点即算阴影（预制体是 Shadow/Shadow 两层）。</summary>
    static bool IsShadowNode(Transform t)
    {
        while (t != null)
        {
            if (t.name.IndexOf("shadow", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            t = t.parent;
        }
        return false;
    }

    static void WarnNoShadow(Transform unitRoot)
    {
        if (_warned) return;
        _warned = true;

        var sb = new System.Text.StringBuilder();
        CollectNames(unitRoot, sb, 0);
        Debug.LogError($"[UnitShadowAlpha] 玩家单位「{unitRoot.name}」下找不到 shadow 节点，" +
                       $"阴影透明度没改成（不许静默跳过）。单位子节点：{sb}");
    }

    static void CollectNames(Transform t, System.Text.StringBuilder sb, int depth)
    {
        if (t == null || depth > 4) return;
        for (int i = 0; i < t.childCount; i++)
        {
            var c = t.GetChild(i);
            if (sb.Length > 0) sb.Append(',');
            sb.Append(c.name);
            CollectNames(c, sb, depth + 1);
        }
    }

    static string NodePath(Transform t)
    {
        if (t == null) return "(null)";
        string s = t.name;
        var p = t.parent;
        for (int i = 0; i < 3 && p != null; i++)
        {
            s = p.name + "/" + s;
            p = p.parent;
        }
        return s;
    }
}
