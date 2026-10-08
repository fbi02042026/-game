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

    /// <summary>给玩家看的一句话结论（弹窗顶部那行）。</summary>
    public static string Verdict(EquipInstance newEq, EquipInstance oldEq)
    {
        int d = Compare(newEq, oldEq);
        if (d > 0) return "新的更强";
        if (d < 0) return "现在这件更好";
        return "两件差不多";
    }
}
