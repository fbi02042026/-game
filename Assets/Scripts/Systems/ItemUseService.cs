using UnityEngine;

/// <summary>
/// 道具使用效果派发 —— <c>item_defs.useEffect</c> 的唯一执行出口（2026-10-31 新增）。
///
/// 为什么要有这个类：<see cref="BattleBagSystem.UseItem"/> 原来只判可用性 + 扣数量，
/// 效果派发是 TODO，等于"点了会消失、但不生效"。现在把解析与执行收在这一处，
/// 背包只管「问能不能用 → 扣数量」。
///
/// 效果串格式：<c>key:参数1:参数2</c>
/// <list type="bullet">
/// <item><c>heal_hp:30</c>      立刻回复 30 点生命（不超过上限）</item>
/// <item><c>heal_pct:25</c>     回复最大生命的 25%</item>
/// <item><c>buff_atk:20:15</c>  攻击 +20%，持续 15 秒（百分比，非绝对值）</item>
/// <item><c>buff_def:20:15</c>  防御 +20%，持续 15 秒</item>
/// <item><c>buff_crit:10:15</c> 暴击率 +10%，持续 15 秒</item>
/// <item><c>buff_magic:20:15</c> 法强 +20%，持续 15 秒</item>
/// </list>
///
/// 红线（沿用本项目「不要兜底、出错要看见」口径）：
/// 未知 key、参数缺失/非法、目标不是 Self → <b>LogError + 返回 false</b>，
/// 调用方据此**不扣数量**。绝不"静默当成功"或"静默当无效"。
/// </summary>
public static class ItemUseService
{
    /// <summary>尝试施加效果。成功返回 true（调用方随后扣数量）；失败返回 false 并带 msg。</summary>
    public static bool TryApply(ItemDef def, out string msg)
    {
        msg = null;
        if (def == null) { msg = "道具不存在"; return false; }
        if (string.IsNullOrEmpty(def.useEffect)) { msg = "该道具没有使用效果"; return false; }

        // 目标：当前只实装「对自己」。队友/敌人目标需要选人 UI，未实装就拒绝，不猜。
        if (def.target != ItemTarget.Self)
        {
            Debug.LogError($"[ItemUse] 道具 {def.id} 的目标 {def.target} 尚未实装（只支持 Self），本次拒绝使用");
            msg = "该道具的目标类型还没做";
            return false;
        }

        var bm = BattleManager.InstanceQuiet;
        var hero = bm != null ? bm.hero : null;
        if (bm == null || hero == null || !bm.isInBattle || hero.isDead)
        {
            msg = "战斗中才能使用";
            return false;
        }

        string[] p = def.useEffect.Split(':');
        string key = p[0] != null ? p[0].Trim().ToLowerInvariant() : "";
        switch (key)
        {
            case "heal_hp":
            {
                if (!TryNum(p, 1, out float amount) || amount <= 0f) return BadArgs(def, out msg);
                float healed = Heal(hero, amount);
                msg = $"回复生命 {Mathf.RoundToInt(healed)}";
                return true;
            }
            case "heal_pct":
            {
                if (!TryNum(p, 1, out float pct) || pct <= 0f) return BadArgs(def, out msg);
                float maxHp = hero.attr != null ? hero.attr.GetAttr(AttrType.MaxHp) : hero.currentHp;
                float healed = Heal(hero, maxHp * pct / 100f);
                msg = $"回复生命 {Mathf.RoundToInt(healed)}";
                return true;
            }
            case "buff_atk": return ApplyBuff(def, hero, AttrType.Attack, p, "攻击", ref msg);
            case "buff_def": return ApplyBuff(def, hero, AttrType.Defense, p, "防御", ref msg);
            case "buff_crit": return ApplyBuff(def, hero, AttrType.CritRate, p, "暴击率", ref msg);
            case "buff_magic": return ApplyBuff(def, hero, AttrType.MagicAttack, p, "法强", ref msg);
            default:
                Debug.LogError($"[ItemUse] 道具 {def.id} 的效果「{def.useEffect}」没有对应实现（key={key}），本次拒绝使用");
                msg = "该道具效果尚未实装";
                return false;
        }
    }

    /// <summary>回血走技能治疗同一原语（钳上限 + 治疗飘字 + 治疗量统计），不另写第二份。</summary>
    static float Heal(Hero hero, float amount)
    {
        float before = hero.currentHp;
        var bm = BattleManager.InstanceQuiet;
        if (bm != null && bm.SkillCast != null)
            bm.SkillCast.ApplyHealToUnit(hero, amount, hero);
        else
            hero.currentHp = Mathf.Min(hero.attr != null ? hero.attr.GetAttr(AttrType.MaxHp) : hero.currentHp, hero.currentHp + amount);
        return Mathf.Max(0f, hero.currentHp - before);
    }

    static bool ApplyBuff(ItemDef def, Hero hero, AttrType type, string[] p, string label, ref string msg)
    {
        if (!TryNum(p, 1, out float percent) || percent <= 0f
            || !TryNum(p, 2, out float seconds) || seconds <= 0f)
            return BadArgs(def, out msg);
        if (hero.attr == null) { msg = "角色属性未就绪"; return false; }

        // 传 true = 百分比语义：+20% 传 0.2（AttrSystem 内部按 1+value 乘算）。
        hero.attr.ApplyTimedBuff(type, percent / 100f, true, seconds);
        msg = $"{label} +{percent:0.#}%，{seconds:0.#} 秒";
        return true;
    }

    static bool BadArgs(ItemDef def, out string msg)
    {
        Debug.LogError($"[ItemUse] 道具 {def.id} 的效果串「{def.useEffect}」参数不合法（应为 key:数值[:秒数]）");
        msg = "道具效果参数不合法";
        return false;
    }

    static bool TryNum(string[] p, int index, out float value)
    {
        value = 0f;
        if (p == null || index >= p.Length || string.IsNullOrEmpty(p[index])) return false;
        return float.TryParse(p[index].Trim(), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out value);
    }
}
