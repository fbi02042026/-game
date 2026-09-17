using UnityEngine;

/// <summary>
/// 【全局唯一稀有度配色表】2026-09-17 收敛。
///
/// 收敛前项目里散着 5 份各自硬编码的稀有度色值：
///   MercRarityColors / TavernUnlockUI.RarityColor / GridCellUI.GetRarityColor /
///   EquipDropPopupUI.RarityColor / StageClearEquipUI.RarityColor
/// 结果是同一个佣兵在酒馆显示橙色、在图鉴显示金色；同一件紫装在不同弹窗里紫色不一样。
/// 现在**所有稀有度颜色只能从这里取**，别再往新文件里抄色值。
///
/// 三套枚举的对应关系（定死，不要额外加分支）：
///   装备 Rarity      ：Common 白 / Uncommon 绿 / Rare 蓝 / Epic 紫 / Legendary 橙
///   技能 SkillRarity ：Common 普通 / Rare 稀有 / Epic 史诗 / Legendary 传说
///   佣兵 MercRarity  ：Common 普通 / Rare 稀有 / Legendary 传说
///
/// ⚠️ 佣兵只有 3 档，**不使用「史诗（紫）」**。佣兵稀有度的权威判定是
/// <see cref="MercSkillMapping.StarToRarity"/>（★≥5 传说 / ★≥3 稀有 / 其余普通），
/// 转成技能稀有度请走 <see cref="SkillRarityUtil.FromMerc"/> / <see cref="SkillRarityUtil.FromMercStar"/>，
/// 别在各处自己写三目。
///
/// 明度两档：
///   Get()     — 亮档，用于文字 / 图标 / 边框描线（深色底上可读）
///   GetDeep() — 暗档 = 亮档 ×0.55，用于「被选中的卡片底 / 填充块」这类大色块，
///               直接铺亮档会刺眼。0.55 不是拍的：它正好还原了改版前弹窗卡片的暗色值
///               （紫 0.4/0.25/0.55、蓝 0.2/0.35/0.55、绿 0.2/0.45/0.25 都吻合）。
///
/// 已收敛的调用点（改色只动这一个文件）：
///   战斗佣兵名牌 / 描边   Unit/Mercenary.cs
///   冒险日志图鉴          UI/AdventureLogCodexPanel.cs
///   酒馆解锁              UI/TavernUnlockUI.cs
///   背包 / 装备格边框     UI/GridCellUI.cs
///   装备三选一弹窗卡片底  UI/EquipDropPopupUI.cs
///   关卡结算装备卡底      UI/StageClearEquipUI.cs
///   技能稀有度（转发）    Config/SkillEnums.cs → SkillRarityUtil.Tint
///   商店 / 三选一 / 技槽  走 SkillRarityUtil.Tint，无需再改
/// </summary>
public static class RarityPalette
{
    // ===== 基准色（亮档）=====
    /// <summary>传说 / 橙金（佣兵与技能共用）。</summary>
    public static readonly Color Legendary = new Color(1f, 0.72f, 0.20f, 1f);
    /// <summary>史诗 / 紫（仅技能与装备使用，佣兵不出现）。</summary>
    public static readonly Color Epic = new Color(0.72f, 0.42f, 0.95f, 1f);
    /// <summary>稀有 / 蓝。</summary>
    public static readonly Color Rare = new Color(0.35f, 0.68f, 0.98f, 1f);
    /// <summary>精良 / 绿（仅装备使用）。</summary>
    public static readonly Color Uncommon = new Color(0.35f, 0.78f, 0.40f, 1f);
    /// <summary>普通 / 灰白（不再用纯白，纯白在浅底卡片上会糊成一片）。</summary>
    public static readonly Color Common = new Color(0.80f, 0.82f, 0.86f, 1f);
    /// <summary>名牌 / 文字描边色（所有稀有度共用，保证对比度一致）。</summary>
    public static readonly Color Outline = new Color(0.10f, 0.10f, 0.10f, 1f);

    /// <summary>暗档系数。见类注释：这个值是还原既有弹窗配色反推出来的，不要随意改。</summary>
    public const float DeepMul = 0.55f;

    // ===== 亮档取值 =====

    public static Color Get(MercRosterDefs.MercRarity rarity)
    {
        switch (rarity)
        {
            case MercRosterDefs.MercRarity.Legendary: return Legendary;
            case MercRosterDefs.MercRarity.Rare: return Rare;
            default: return Common;
        }
    }

    public static Color Get(SkillRarity rarity)
    {
        switch (rarity)
        {
            case SkillRarity.Legendary: return Legendary;
            case SkillRarity.Epic: return Epic;
            case SkillRarity.Rare: return Rare;
            default: return Common;
        }
    }

    public static Color Get(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Legendary: return Legendary;
            case Rarity.Epic: return Epic;
            case Rarity.Rare: return Rare;
            case Rarity.Uncommon: return Uncommon;
            default: return Common;
        }
    }

    // ===== 暗档取值（大色块用）=====

    public static Color GetDeep(MercRosterDefs.MercRarity rarity) => Deepen(Get(rarity));

    public static Color GetDeep(SkillRarity rarity) => Deepen(Get(rarity));

    public static Color GetDeep(Rarity rarity) => Deepen(Get(rarity));

    /// <summary>把亮档压暗成可做大面积填充的暗档（保持 alpha）。</summary>
    public static Color Deepen(Color c) => new Color(c.r * DeepMul, c.g * DeepMul, c.b * DeepMul, c.a);

    // ===== 佣兵判定 =====

    /// <summary>按 AssetId 或 HireId 解析佣兵稀有度；查不到按普通处理。</summary>
    public static MercRosterDefs.MercRarity ResolveMercRarity(string mercIdOrAssetId)
    {
        if (MercRosterDefs.TryGetByAssetId(mercIdOrAssetId, out var def))
            return def.Rarity;
        if (MercRosterDefs.TryGetByHireId(mercIdOrAssetId, out def))
            return def.Rarity;
        return MercRosterDefs.MercRarity.Common;
    }
}
