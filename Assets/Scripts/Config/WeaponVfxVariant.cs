using System.Collections.Generic;

/// <summary>
/// 攻击特效档位解析（1/2/3）—— 档位解析的【唯一出口】。
/// <para>【2026-10-24 主人拍板】高级别武器的近战刀光分别固定用 刀光2/刀光3：禁随机、同一武器每次出手同一款。
/// 【2026-10-24 主人拍板】敌人远程物理（弓）/魔法（法球）攻击飞行体也分档：普通怪 fly1、精英/Boss fly2。
/// 只有近战刀光与 Enemy 远程飞行体换档；Ally 弓/法球/治疗不适用（Ally 远程未换素材，固定 Base）。
/// 远程以后可能要细分：本期先把「档位 variant」当参数贯穿加载链路（BattleVFXSystem.LoadSharedKit /
/// GetSharedKitFileNames 的 variant 形参），但不提前写未使用的分支。</para>
/// <para>项目铁律：① 解析不到档位回 <see cref="Base"/> 是「设计最低档」，不是兜底另一套值；
/// ② 调用方不许自己 if 稀有度/精英态，档位只此一个出口。</para>
/// </summary>
public static class WeaponVfxVariant
{
    /// <summary>最低档：fly1 / 刀光1（白/绿/蓝武器、空手、普通怪、解析失败的默认档）。</summary>
    public const int Base = 1;
    /// <summary>高档：刀光2 / 敌人远程 fly2（史诗/紫武、精英与 Boss 怪）。</summary>
    public const int High = 2;
    /// <summary>顶档：刀光3（传说/橙武）。</summary>
    public const int Top = 3;

    /// <summary>
    /// 逐武器覆盖表（key=templateId，value=档位）：要把某把高级武器单独钉死只加这里一行，
    /// 例：<c>{ "wp_excalibur", Top }</c>。命中优先于稀有度默认档；初始空表。
    /// </summary>
    static readonly Dictionary<string, int> TemplateOverride = new Dictionary<string, int>
    {
        // 要把某把高级武器单独钉死只加这里一行，例：{ "wp_excalibur", Top },
    };

    /// <summary>
    /// 稀有度 → 默认档：Legendary→Top；Epic→High；其余（Common/Uncommon/Rare）→Base。
    /// 这是默认档，待主人微调只改这一处。
    /// </summary>
    static int DefaultByRarity(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Legendary: return Top;
            case Rarity.Epic: return High;
            default: return Base;
        }
    }

    /// <summary>佣兵稀有度（Common/Rare/Legendary）映射到装备稀有度档位。</summary>
    static Rarity MercRarityToEquipRarity(MercRosterDefs.MercRarity r)
    {
        switch (r)
        {
            case MercRosterDefs.MercRarity.Legendary: return Rarity.Legendary;
            case MercRosterDefs.MercRarity.Rare: return Rarity.Rare;
            default: return Rarity.Common;
        }
    }

    /// <summary>
    /// 解析攻击者的特效档位（1/2/3）。纯函数：无 Random、无静态可变状态，
    /// 同一单位每次出手必得同一档。规则：
    /// Hero 看主手武器稀有度（覆盖表优先）；近战职业佣兵（Melee/Polearm/Berserker）看花名册稀有度；
    /// 怪物：精英/Boss → High（敌人远程飞行体 fly2 用，Enemy 近战刀光不含档位不受影响）；
    /// 弓/法佣兵、解析不到 → Base（设计最低档，不是兜底另一套值）。
    /// </summary>
    public static int Resolve(UnitBase attacker)
    {
        if (attacker == null) return Base;

        if (attacker is Hero)
        {
            var inst = Hero.TryGetEquippedWeaponInstance(GridBackpackSystem.Instance);
            if (inst == null) return Base;
            if (!string.IsNullOrEmpty(inst.templateId)
                && TemplateOverride.TryGetValue(inst.templateId, out int pinned))
                return pinned;
            return DefaultByRarity(inst.rarity);
        }

        if (attacker is Mercenary merc && !string.IsNullOrEmpty(merc.mercId))
        {
            MercRosterDefs.MercJobKind job = MercRosterDefs.JobKindOf(merc.mercId);
            if (job == MercRosterDefs.MercJobKind.Melee
                || job == MercRosterDefs.MercJobKind.Polearm
                || job == MercRosterDefs.MercJobKind.Berserker)
                return DefaultByRarity(MercRarityToEquipRarity(RarityPalette.ResolveMercRarity(merc.mercId)));
            return Base; // 弓/法：不换档
        }

        // 2026-10-24 主人拍板：敌人远程物理/魔法攻击飞行体分档——精英/Boss 用 fly2，普通怪 fly1。
        // 怪物近战走 Enemy/MeleeSlash（文件名不含档位），拿到 High 也无影响。
        if (attacker is Monster mon && (mon.IsBossUnit || mon.IsEliteWave))
            return High;

        return Base; // 其它单位
    }
}
