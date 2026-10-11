using UnityEngine;

/// <summary>
/// 装备强弱比较 —— 只服务一个场景：抽到 / 拿到新装备时，弹窗要告诉玩家「新的更强还是旧的更强」。
///
/// <para><b>真源是 <see cref="EquipInstance"/> 头部注释定的优先级：品质 &gt; 星级 &gt; 强化等级</b>。
/// 词条条数只在以上全同分时做兜底。绝不在这里另立一套强弱口径 ——
/// 换弹窗只要改这一个文件，别处（背包排序、战力估算）要比较也调这里。</para>
///
/// 2026-10-05 新增：主人拍板「抽中高级的直接替换低级的，要弹窗问玩家是否替换」。
/// </summary>
public static class EquipCompare
{
    /// <summary>装备强弱分（越大越强）。null 记最低分，保证「有装备」永远赢「没装备」。</summary>
    public static int Score(EquipInstance e)
    {
        if (e == null) return int.MinValue;
        int attrs = e.attrBonus != null ? e.attrBonus.Count : 0;
        return (int)e.rarity * 100000
             + Mathf.Max(0, e.star) * 1000
             + Mathf.Max(0, e.enhanceLevel) * 10
             + attrs;
    }

    /// <summary>a 比 b 强返回正数，弱返回负数，一样返回 0。</summary>
    public static int Compare(EquipInstance a, EquipInstance b)
    {
        // 用 CompareTo 而不是相减：Score 可能是 int.MinValue，直接减会溢出
        return Score(a).CompareTo(Score(b));
    }

    /// <summary>
    /// 主属性数值（attrBonus 第一条 = 弹窗「攻击+N」那行）。
    /// 2026-10-11 主人反馈：普通★1 攻击+18 vs 普通★1 攻击+5，旧 Verdict 判「两件差不多」——
    /// 因为 Score 只看 品质/星级/强化/词条数，**完全没看属性数值**，两件全同分。
    /// </summary>
    public static float PrimaryAttrValue(EquipInstance e)
    {
        if (e?.attrBonus == null || e.attrBonus.Count == 0) return 0f;
        var a = e.attrBonus[0];
        return a != null ? Mathf.Abs(a.value) : 0f;
    }

    /// <summary>主属性相对差 ≥ 这个比例才算「分得出高下」，否则算差不多。</summary>
    const float VerdictDiffK = 0.15f;

    /// <summary>新的那件是否真的更强（弹窗判定文案着色的唯一判据，与 Verdict 同一出口）。</summary>
    public static bool NewBetter(EquipInstance newEq, EquipInstance oldEq)
    {
        int d = Compare(newEq, oldEq);
        if (d != 0) return d > 0;
        // 品质/星级/强化/词条数全同 → 按主属性数值分高下
        float na = PrimaryAttrValue(newEq), oa = PrimaryAttrValue(oldEq);
        if (na <= 0f || oa <= 0f) return false;
        return na > oa * (1f + VerdictDiffK);
    }

    /// <summary>给玩家看的一句话结论（弹窗顶部那行）。</summary>
    public static string Verdict(EquipInstance newEq, EquipInstance oldEq)
    {
        float na = PrimaryAttrValue(newEq), oa = PrimaryAttrValue(oldEq);
        bool canJudgeByAttr = na > 0f && oa > 0f
                              && Mathf.Abs(na - oa) / Mathf.Max(na, oa) >= VerdictDiffK;
        // 品质/星级/强化/词条数有差距 → 直接按旧口径分；全同但主属性差得多 → 也分
        if (Compare(newEq, oldEq) != 0 || canJudgeByAttr)
            return NewBetter(newEq, oldEq) ? "新的更强" : "现在这件更好";
        return "两件差不多";
    }
}
