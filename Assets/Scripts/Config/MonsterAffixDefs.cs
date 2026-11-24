using System.Collections.Generic;
using UnityEngine;

/// <summary>精英 / Boss 词缀（V6 新增的敌人机制，普通小怪不参与）。</summary>
public enum MonsterAffixId
{
    None = 0,
    Frenzy,   // 狂暴：攻速 +30%
    Bulwark,  // 铁壁：受到伤害 -25%
    Thorns,   // 荆棘：反弹近战伤害
    Swift,    // 迅捷：移速 +40%
    Leech,    // 吸血：造成伤害回血
    Split,    // 分裂：死亡时分裂出小怪
    Crit,     // 暴击：攻击概率暴击（2026-10-26 词缀怪新增）
}

/// <summary>
/// 词缀表：全部是<b>纯数值乘子 + 一个死亡事件</b>，不引入新 AI、不需要新美术。
/// 词缀名会挂在屏幕 Boss 血条上（BattleBossHpBar）作为后缀，如「森林守卫·狂暴」。
/// </summary>
public static class MonsterAffixDefs
{
    public struct Def
    {
        public MonsterAffixId Id;
        /// <summary>血条后缀（不要带「·」）</summary>
        public string Name;
        /// <summary>后缀颜色</summary>
        public Color Tint;
        /// <summary>【2026-11-03 主人拍板】词条一句话介绍：首次登场弹窗 / 冒险日志怪物Tip 共用。</summary>
        public string Desc;
        // 2026-11-12 新增：该词条带来的基础属性提升短句；空 = 不显示第二行
        public string StatDesc;

        public float AtkSpeedMul;
        public float MoveSpeedMul;
        /// <summary>受到伤害的乘子（&lt;1 = 减伤）</summary>
        public float DamageTakenMul;
        /// <summary>反弹给近战攻击者的比例（按实际造成的伤害）</summary>
        public float ThornsRatio;
        /// <summary>造成伤害时回血的比例</summary>
        public float LeechRatio;
        /// <summary>死亡时分裂出的小怪数量</summary>
        public int SplitCount;
        /// <summary>攻击暴击概率（0~1，2026-10-26 词缀怪「暴击」用）</summary>
        public float CritChance;
        /// <summary>暴击伤害倍率（暴击词缀生效时实际伤害 × 此值）；运行时组件 MonsterAffix 默认 1f，非 Crit 词缀不进入乘算分支，故此处不写初始化器以兼容 C# 9.0</summary>
        public float CritDamageMul;
        /// <summary>【2026-11-12 主人拍板】表定词缀怪基础属性乘子（>0 才乘，否则视为 1，不写实例初始化器，同 CritDamageMul）。</summary>
        public float HpMul;
        public float AtkMul;
        public float DefMul;
    }

    // 【2026-11-12 主人拍板：按词条各加一项属性，只表定词缀怪生效】
    // 每个词条只加自己那一项基础属性（精英/Boss 掷骰词缀 RollAndAttach 走 RollAttrScale 折扣，见下）：
    //   铁壁 Bulwark → DefMul 1.5f：防御/魔防 ×1.5
    //   狂暴 Frenzy  → AtkMul 1.3f：攻击/魔攻 ×1.3
    //   暴击 Crit    → AtkMul 1.3f：攻击/魔攻 ×1.3
    //   迅捷 Swift   → 移速 ×2.0 + HpMul 1.2f（2026-11-14 补：原来只有移速、血攻和普通怪一样，完全不难打）
    //   吸血 Leech   → HpMul 1.3f：生命 ×1.3
    //   荆棘 Thorns  → HpMul 1.3f：生命 ×1.3
    //   分裂 Split   → HpMul 1.4f：生命 ×1.4
    // 消费点唯一 = MonsterAffix.Apply；HpMul/AtkMul/DefMul 留空为 0 时视为 1。
    /// <summary>
    /// 【2026-11-14 主人拍板「要让带词条的怪比普通怪难打」】精英/Boss 掷骰词缀的属性乘子折扣。
    /// 按 <c>1 + (mul - 1) × 本值</c> 向 1 收敛：表定词缀怪（VisualTinted）不吃折扣、全额生效；
    /// 掷骰词缀吃 0.6（比如 HpMul 1.3 → 1.18）。这些单位本身已经厚，避免叠过头。
    /// </summary>
    public const float RollAttrScale = 0.6f;

    // 【2026-11-14 主人拍板「词条怪就要比普通硬」】表定词缀怪的整体强度档：
    // 在各词条自己的 HpMul/AtkMul/DefMul 之上再整体乘一层（只有表定词缀怪 VisualTinted 吃，
    // 精英/Boss 掷骰词缀不吃 —— 它们已按 RollAttrScale 向 1 收敛且自身够厚）。
    // 【2026-11-15 口径】基准 = Monster.Init 按「当前章/座/节 + 当前波」算完的
    // attr（GetStatScale 当关倍率 × 表值），跟着当前关的小怪一起爬坡；不取第一章表值、不写死常量。
    // 【2026-11-23 血 5f → 6f、攻 2f → 2.5f】反馈「词条怪数值可以再提高」。
    // 11-22 血 3→5 解决「1-2 下就死」；这次血攻一起抬，让穿插的词条怪更有存在感。
    // 仅表定词条怪（VisualTinted）吃这层，精英/Boss 掷骰词缀不受影响。
    // 消费点唯一 = MonsterAffix.Apply；想调手感只改这两个常量。
    public const float FixedAffixHpMul = 6f;
    public const float FixedAffixAtkMul = 2.5f;

    // 【2026-11-24 主人拍板「随章放大就挺好」】词条机制强度按章递增：第 1 章 1.0 → 第 8 章 1.42。
    // 消费点唯一 = MonsterAffix.Apply：对「减伤 / 攻速 / 移速 / 荆棘反弹 / 吸血 / 暴击率」
    // 统一按 Mathf.Lerp(1, 原值, grow) 或等比放大（减伤向 0 收敛），血攻防三项乘子不受本系数影响。
    // 目的：同一只「铁壁」怪第 8 章就是比第 1 章难啃，机制身份不变，玩家不用重学解法。
    public const float AffixChapterGrowStep = 0.06f;
    public static float ChapterGrow(int chapter)
    {
        int ch = Mathf.Clamp(chapter <= 0 ? 1 : chapter, 1, 8);
        return 1f + AffixChapterGrowStep * (ch - 1);
    }

    // 【2026-11-24 主人拍板】表定词条怪血量再补一层温和的随章增长：6 → 第 8 章 ≈ 7.26（+21%）。
    // 只补这一小截，是为了让「6 刀」缓降到 4~5 刀，而不是掉到 3 刀；不让它变成血牛。
    public const float FixedAffixHpGrowPerChapter = 0.03f;
    public static float FixedAffixHpMulFor(int chapter)
    {
        int ch = Mathf.Clamp(chapter <= 0 ? 1 : chapter, 1, 8);
        return FixedAffixHpMul * (1f + FixedAffixHpGrowPerChapter * (ch - 1));
    }
    public static readonly Def[] All =
    {
        // 【2026-11-10 主人拍板】全部词条效果再加 50%（在 2026-10-26 词缀怪数值之上整体 ×1.5）：
        // 狂暴 +30%→+45%、铁壁减伤 25%→37.5%、荆棘 15%（11-11 主人拍板回退，不吃本批 +50%）、迅捷 +40%→+60%、
        // 吸血 30%→45%、分裂 2→3 只、暴击概率 20%→30%（倍率 1.6 不变，暴击期望增量正好 +50%）。
        new Def { Id = MonsterAffixId.Frenzy,  Name = "狂暴", Tint = new Color(1.00f, 0.42f, 0.32f),
                  Desc = "攻击速度提升 45%",
                  StatDesc = "攻击 +30%",
                  AtkSpeedMul = 1.45f, MoveSpeedMul = 1f,   DamageTakenMul = 1f, AtkMul = 1.3f },
        new Def { Id = MonsterAffixId.Bulwark, Name = "铁壁", Tint = new Color(0.62f, 0.78f, 1.00f),
                  Desc = "受到的伤害降低 37.5%",
                  StatDesc = "防御 +50%",
                  AtkSpeedMul = 1f,    MoveSpeedMul = 1f,   DamageTakenMul = 0.625f, DefMul = 1.5f },
        // 【2026-11-11 主人拍板】荆棘反弹回到 15%（撤销 11-10 的 15%→22.5% 加成）。
        new Def { Id = MonsterAffixId.Thorns,  Name = "荆棘", Tint = new Color(0.55f, 0.95f, 0.55f),
                  Desc = "近战攻击它会被反弹 15% 伤害",
                  StatDesc = "生命 +30%",
                  AtkSpeedMul = 1f,    MoveSpeedMul = 1f,   DamageTakenMul = 1f, ThornsRatio = 0.15f, HpMul = 1.3f },
        new Def { Id = MonsterAffixId.Swift,   Name = "迅捷", Tint = new Color(0.98f, 0.85f, 0.40f),
                  // 2026-11-12 随 MoveSpeedMul 2.0f 同步文案；2026-11-14 补 HpMul（原来只有移速，血攻与普通怪无异）
                  Desc = "移动速度提升 100%",
                  StatDesc = "生命 +20%",
                  AtkSpeedMul = 1f,    MoveSpeedMul = 2.0f, DamageTakenMul = 1f, HpMul = 1.2f },
        // 【2026-11-05 主人拍板】吸血 12% -> 30%：链路验证全通（普攻/弹道/技能都走 TakeDamage 回调），
        // 但 ch2 怪一刀只回 1~2 点、血条动 ~3%，Boss 更是 0.4% —— 肉眼不可见，主人实测「从来没见过回血」。
        // 提到 30% 后怪一刀回 4~6 点（血条动 8%+）肉眼可辨；Boss 血厚回血占比天然低，属合理衰减。
        // 【2026-11-10 主人拍板】全部词条效果 +50%：吸血 30% -> 45%。
        new Def { Id = MonsterAffixId.Leech,   Name = "吸血", Tint = new Color(0.85f, 0.45f, 0.90f),
                  Desc = "造成伤害的 45% 转化为自身治疗",
                  StatDesc = "生命 +30%",
                  AtkSpeedMul = 1f,    MoveSpeedMul = 1f,   DamageTakenMul = 1f, LeechRatio = 0.45f, HpMul = 1.3f },
        new Def { Id = MonsterAffixId.Split,   Name = "分裂", Tint = new Color(0.75f, 0.75f, 0.80f),
                  Desc = "死亡时分裂出 3 只小怪",
                  StatDesc = "生命 +40%",
                  AtkSpeedMul = 1f,    MoveSpeedMul = 1f,   DamageTakenMul = 1f, SplitCount = 3, HpMul = 1.4f },
        // 2026-10-26 词缀怪「暴击」：20% 概率 ×1.6，玩家侧看得见暴击飘字
        // 【2026-11-10 主人拍板】全部词条效果 +50%：暴击概率 20% -> 30%（×1.6 倍率不变，暴击期望增量 +50%）
        new Def { Id = MonsterAffixId.Crit,    Name = "暴击", Tint = new Color(1.00f, 0.55f, 0.10f),
                  Desc = "攻击有 30% 概率造成 1.6 倍伤害",
                  StatDesc = "攻击 +30%",
                  AtkSpeedMul = 1f,    MoveSpeedMul = 1f,   DamageTakenMul = 1f,
                  CritChance = 0.30f,  CritDamageMul = 1.6f, AtkMul = 1.3f },
    };

    /// <summary>
    /// 【2026-11-05 主人拍板「boss吸血可以高点 和小怪分开」】Boss 专用吸血比例（覆盖 Def.LeechRatio）。
    /// Boss 攻高血厚：60% 一刀回 20~40 点、绿字醒目；屏幕血条每刀 ~1.5-2%，连打可见累积。
    /// 消费点唯一 = MonsterAffix.OnDealtDamage（按 owner.IsBossUnit 取档）。
    /// 【2026-11-10 主人拍板】小怪词条整体 +50%，但 Boss 吸血保持 60% 不变（「boss吸血不变还是60%吧」）。
    /// </summary>
    public const float BossLeechRatio = 0.60f;

    static readonly Dictionary<MonsterAffixId, Def> _map = BuildMap();

    static Dictionary<MonsterAffixId, Def> BuildMap()
    {
        var d = new Dictionary<MonsterAffixId, Def>();
        for (int i = 0; i < All.Length; i++) d[All[i].Id] = All[i];
        return d;
    }

    public static Def Get(MonsterAffixId id)
    {
        return _map.TryGetValue(id, out var v) ? v : default;
    }

    public static string Name(MonsterAffixId id)
    {
        return _map.TryGetValue(id, out var v) ? v.Name : "";
    }

    /// <summary>
    /// 【2026-11-03 主人拍板】把词缀列表拼成「狂暴：攻击速度提升 45%\n…」的展示文本。
    /// 首次登场弹窗与冒险日志怪物 Tip 共用；空列表返回空串。
    /// 【2026-11-12 补】每个词条两行：机制（名称：Desc）+ 属性（属性：StatDesc）；Swift 的 StatDesc 为空，只有机制一行。
    /// </summary>
    public static string Describe(List<MonsterAffixId> list)
    {
        if (list == null || list.Count == 0) return "";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < list.Count; i++)
        {
            var d = Get(list[i]);
            if (string.IsNullOrEmpty(d.Name)) continue;
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(d.Name).Append("：").Append(d.Desc);
            if (!string.IsNullOrEmpty(d.StatDesc)) sb.Append('\n').Append("属性：").Append(d.StatDesc);
        }
        return sb.ToString();
    }

    /// <summary>
    /// 解析「| 分隔的词缀枚举名」为列表（monster_stats.affix 列 / WaveData 偏好共用此口径）。
    /// 空串 / null / 全部解析失败 → 返回空列表（调用方按「无词缀」处理，不兜底造词缀）。
    /// </summary>
    public static List<MonsterAffixId> ParseAffixList(string raw)
    {
        var list = new List<MonsterAffixId>();
        if (string.IsNullOrWhiteSpace(raw)) return list;
        var parts = raw.Split('|');
        for (int i = 0; i < parts.Length; i++)
        {
            string p = parts[i].Trim();
            if (string.IsNullOrEmpty(p)) continue;
            if (System.Enum.TryParse(p, true, out MonsterAffixId id) && id != MonsterAffixId.None)
                list.Add(id);
        }
        return list;
    }

    /// <summary>本次要出的词缀数量：第 4 章起有概率出 2 个。</summary>
    public static int RollCount(int chapter)
    {
        int baseCount = 1;
        if (chapter >= GameConfig.EnemyTuning.AffixSecondFromChapter
            && Random.value < GameConfig.EnemyTuning.AffixSecondChance)
            baseCount = 2;
        return baseCount;
    }

    /// <summary>
    /// 下一次 <see cref="Roll"/> 使用的词缀偏好（| 分隔的枚举名，如 "Split"、"Bulwark|Thorns"）。
    /// 由 WavePlanner 在实例化精英前挂上、Roll 消费后立即清空 —— 不给别的怪漏味。
    /// </summary>
    static string _pendingBias = "";

    public static void SetPendingBias(string bias) { _pendingBias = bias ?? ""; }
    public static void ClearPendingBias() { _pendingBias = ""; }

    /// <summary>随机抽 n 个不重复的词缀。</summary>
    public static List<MonsterAffixId> Roll(int chapter)
    {
        int n = RollCount(chapter);
        var pool = new List<MonsterAffixId>();

        // 有偏好就只在偏好里抽（解析失败则退回全池，绝不因为配错表而崩）
        string bias = _pendingBias;
        _pendingBias = "";
        if (!string.IsNullOrEmpty(bias))
        {
            var parts = bias.Split('|');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (string.IsNullOrEmpty(p)) continue;
                if (System.Enum.TryParse(p, true, out MonsterAffixId bid) && bid != MonsterAffixId.None)
                    pool.Add(bid);
            }
        }
        if (pool.Count == 0)
        {
            for (int i = 0; i < All.Length; i++) pool.Add(All[i].Id);
        }

        var picked = new List<MonsterAffixId>(n);
        for (int k = 0; k < n && pool.Count > 0; k++)
        {
            int idx = Random.Range(0, pool.Count);
            picked.Add(pool[idx]);
            pool.RemoveAt(idx);
        }
        return picked;
    }
}
