using System.Text.RegularExpressions;

/// <summary>
/// 佣兵主动技执行器：按 merc_skills 的 Category / TargetType / Formula 分派，
/// 禁止再写 <c>if (skillId == "SKxxx")</c>。数值从公式解析，解析失败才用现网种子。
/// </summary>
public static class MercSkillExecutor
{
    public enum Kind
    {
        HealSingle,
        HealTeam,
        SelfDefBuff,
        TeamShield,
        TeamHealAtk,
        FearThenFallback,
        BuffFallback,
        SkillSystemOrFallback
    }

    public static Kind Resolve(string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return Kind.SkillSystemOrFallback;

        bool isHeal = MercSkillTable.IsHealActiveId(skillId);
        if (MercSkillTable.TryGet(skillId, out var row))
        {
            if (!isHeal && row.Category == MercSkillTable.SkillCategory.Heal)
                isHeal = true;
            if (isHeal)
                return IsTeam(row) ? Kind.HealTeam : Kind.HealSingle;

            string f = row.Formula ?? "";
            if (IsTeam(row) && (Contains(f, "治疗") || Contains(f, "恢复")))
                return Kind.TeamHealAtk;
            if (IsTeam(row) && (Contains(f, "生命上限") || Contains(f, "护盾")))
                return Kind.TeamShield;
            if (Contains(row.TargetType, "自身") && row.Category == MercSkillTable.SkillCategory.Defense)
                return Kind.SelfDefBuff;
            if (row.Category == MercSkillTable.SkillCategory.Magic
                && (Contains(f, "目标攻击") || Contains(f, "降低")))
                return Kind.FearThenFallback;
        }
        else if (isHeal)
            return Kind.HealSingle;

        return Kind.SkillSystemOrFallback;
    }

    /// <summary>治疗技攻击倍率。现网：配置 damageMultiplier，缺省 1.3。</summary>
    public static float HealAtkMul(SkillConfig cfg)
    {
        if (cfg != null && cfg.damageMultiplier > 0f) return cfg.damageMultiplier;
        return 1.3f;
    }

    /// <summary>SK007 现网 5 秒；表 duration=5。</summary>
    public static float SelfDefDuration(string skillId)
    {
        if (MercSkillTable.TryGet(skillId, out var row) && row.Duration > 0f)
            return row.Duration;
        return 5f;
    }

    /// <summary>SK008 现网 10% / 6 秒；表「生命上限 × 10%」、duration=6。</summary>
    public static void TeamShieldParams(string skillId, out float ratio, out float duration)
    {
        ratio = 0.10f;
        duration = 6f;
        if (!MercSkillTable.TryGet(skillId, out var row)) return;
        float parsed = ParsePercent(row.Formula);
        if (parsed > 0f) ratio = parsed;
        if (row.Duration > 0f) duration = row.Duration;
    }

    /// <summary>SK010 现网 攻击×50%；表「攻击 × 50% 治疗」。</summary>
    public static float TeamHealAtkMul(string skillId)
    {
        if (MercSkillTable.TryGet(skillId, out var row))
        {
            float parsed = ParseAttackMul(row.Formula);
            if (parsed > 0f) return parsed;
        }
        return 0.5f;
    }

    /// <summary>SK018 现网 8 秒；表 duration=8。</summary>
    public static float FearDuration(string skillId)
    {
        if (MercSkillTable.TryGet(skillId, out var row) && row.Duration > 0f)
            return row.Duration;
        return 8f;
    }

    static bool IsTeam(MercSkillTable.Row row)
    {
        return Contains(row.TargetType, "全体") || Contains(row.RangeDesc, "全体");
    }

    static bool Contains(string s, string token)
    {
        return !string.IsNullOrEmpty(s) && s.IndexOf(token, System.StringComparison.Ordinal) >= 0;
    }

    static float ParsePercent(string formula)
    {
        if (string.IsNullOrEmpty(formula)) return 0f;
        var m = Regex.Match(formula, @"(\d+(?:\.\d+)?)\s*%");
        if (!m.Success) return 0f;
        return float.Parse(m.Groups[1].Value) / 100f;
    }

    static float ParseAttackMul(string formula)
    {
        if (string.IsNullOrEmpty(formula) || formula.IndexOf("攻击", System.StringComparison.Ordinal) < 0)
            return 0f;
        var m = Regex.Match(formula, @"攻击\s*[×xX*]\s*(\d+(?:\.\d+)?)\s*%");
        if (m.Success) return float.Parse(m.Groups[1].Value) / 100f;
        m = Regex.Match(formula, @"(\d+(?:\.\d+)?)\s*%");
        return m.Success ? float.Parse(m.Groups[1].Value) / 100f : 0f;
    }
}
