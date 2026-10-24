using System.Text.RegularExpressions;

/// <summary>
/// 配表「公式列」数值解析 —— 【2026-10-23 主人拍板】正则只留这一份。
/// <para>原先同款正则散在 <c>MercSkillExecutor</c> 与 <c>MercSkillTable</c> 两处各自维护，
/// 改一处忘另一处就会出现「表里写的是 -20%、战斗里按 +20% 算」这类事故。</para>
/// ⚠️ **带符号 / 不带符号是两种口径，别混用**（数值会翻）：
/// <list type="bullet">
/// <item><see cref="ParseSignedPercent"/>："-20%" → -0.20（merc_skills 配表解析用，减益必须为负）。</item>
/// <item><see cref="ParseUnsignedPercent"/>："-20%" → 0.20（战斗结算用，历史口径，改了会翻数值）。</item>
/// </list>
/// </summary>
public static class FormulaParser
{
    /// <summary>带符号百分比：第一个 ±N% → ±N/100；取不到返回 fallback。</summary>
    public static float ParseSignedPercent(string formula, float fallback)
    {
        if (string.IsNullOrEmpty(formula)) return fallback;
        var m = Regex.Match(formula, @"([+-]?\d+(?:\.\d+)?)\s*%");
        if (!m.Success) return fallback;
        return float.Parse(m.Groups[1].Value) / 100f;
    }

    /// <summary>不带符号百分比：第一个 N% → N/100（忽略正负号）；取不到返回 0。</summary>
    public static float ParseUnsignedPercent(string formula)
    {
        if (string.IsNullOrEmpty(formula)) return 0f;
        var m = Regex.Match(formula, @"(\d+(?:\.\d+)?)\s*%");
        if (!m.Success) return 0f;
        return float.Parse(m.Groups[1].Value) / 100f;
    }

    /// <summary>
    /// 「攻击×N%」倍率：优先匹配 攻击×N%，其次「含『攻击』二字的 N%」；都不匹配返回 0。
    /// 原 MercSkillExecutor.ParseAttackMul 与 MercSkillTable.ParseAttackMultiplier 口径一致，合并后行为不变。
    /// </summary>
    public static float ParseAttackMultiplier(string formula)
    {
        if (string.IsNullOrEmpty(formula)) return 0f;
        var m = Regex.Match(formula, @"攻击\s*[×xX*]\s*(\d+(?:\.\d+)?)\s*%");
        if (m.Success) return float.Parse(m.Groups[1].Value) / 100f;
        m = Regex.Match(formula, @"(\d+(?:\.\d+)?)\s*%");
        if (m.Success && formula.IndexOf("攻击", System.StringComparison.Ordinal) >= 0)
            return float.Parse(m.Groups[1].Value) / 100f;
        return 0f;
    }
}
