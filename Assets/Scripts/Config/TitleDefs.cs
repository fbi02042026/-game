using UnityEngine;

/// <summary>
/// 称号表（2026-10-31 新增，主人拍板「称号自动上战斗形象头顶」）：id / 显示名 / 稀有度 / 来源。
///
/// <para><b>稀有度口径</b>：沿用 <see cref="SkillRarity"/> 的三档（Common 普通 / Rare 稀有 / Legendary 传说）。
/// <c>Epic</c> 已于 2026-10-05 停用，<b>新称号不要产出 Epic</b>。颜色一律走 <see cref="RarityPalette"/>，
/// 不要在业务文件里抄色值。</para>
///
/// <para><b>发放唯一出口</b>：<see cref="TitleSystem.Unlock"/> —— 解锁 + 自动切换展示都在那边，
/// 别处不要再直接往 <c>SaveData.unlockedTitleIds</c> 里塞（那样不会自动换展示）。
/// 新增称号只在本表加一行，发放处调 Unlock 即可。</para>
/// </summary>
public static class TitleDefs
{
    public struct Def
    {
        public string Id;
        public string Name;
        public SkillRarity Rarity;
        /// <summary>来源说明（给玩家看的一行，可空）。</summary>
        public string Source;
    }

    static readonly Def[] All =
    {
        new Def
        {
            Id = "forest_untouched", Name = "森林无伤者", Rarity = SkillRarity.Rare,
            Source = "冒险日志成就「完美首通」：无伤通关第一章普通难度"
        },
        new Def
        {
            Id = "log_chronicler", Name = "传说记录者", Rarity = SkillRarity.Legendary,
            Source = "冒险日志里程等级 Lv6"
        },
    };

    public static Def[] AllDefs => All;

    public static bool TryGet(string id, out Def def)
    {
        if (!string.IsNullOrEmpty(id))
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id) { def = All[i]; return true; }
            }
        }
        def = default;
        return false;
    }

    public static string NameOf(string id) => TryGet(id, out var d) ? d.Name : "";

    public static SkillRarity RarityOf(string id) =>
        TryGet(id, out var d) ? d.Rarity : SkillRarity.Common;

    /// <summary>稀有度排序值：越大越高级（枚举值本身就是这个顺序，别再另写映射）。</summary>
    public static int Rank(SkillRarity r) => (int)r;

    /// <summary>称号文字颜色（头顶 / 以后的角色页列表统一走这里）。</summary>
    public static Color ColorOf(string id) => RarityPalette.Get(RarityOf(id));

    /// <summary>稀有度中文（普通 / 稀有 / 传说）。</summary>
    public static string RarityName(SkillRarity r)
    {
        switch (r)
        {
            case SkillRarity.Legendary: return "传说";
            case SkillRarity.Rare: return "稀有";
            default: return "普通";
        }
    }
}
