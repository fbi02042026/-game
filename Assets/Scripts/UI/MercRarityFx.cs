
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 传奇佣兵显示特效统一出口（2026-11-05 主人指令「传奇佣兵显示的不一样些」）。
///
/// <para><b>UI</b>：佣兵立绘/头像挂金色流光材质（<see cref="EquipRarityMaterials.LegendaryShineMaterial"/>，
/// 与传奇装备图标同一材质，SSU Shine 家族，UI 上已验证同族可用）；非传奇一律回内置默认材质（null）。</para>
/// <para><b>战斗</b>：脚下金色光环 + 形象流光在 Mercenary 分册（Mercenary.LegendaryFx.cs），
/// 本类只管 UI 侧。</para>
/// <para>传奇判定唯一真源 = <see cref="RarityPalette.ResolveMercRarity"/>（与名字颜色同一张色表同一判定）。</para>
/// </summary>
public static class MercRarityFx
{
    /// <summary>UI 立绘/头像：传奇挂流光材质，其余（含 id 取不到）回内置默认材质。hireId / assetId 都认。</summary>
    public static void ApplyPortraitShine(Image img, string hireIdOrAssetId)
    {
        if (img == null) return;
        bool legendary = !string.IsNullOrEmpty(hireIdOrAssetId)
            && RarityPalette.ResolveMercRarity(hireIdOrAssetId) == MercRosterDefs.MercRarity.Legendary;
        img.material = legendary ? EquipRarityMaterials.LegendaryShineMaterial() : null;
    }
}
