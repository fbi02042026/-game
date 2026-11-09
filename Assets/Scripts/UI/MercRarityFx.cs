
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 佣兵显示特效统一出口（2026-11-09 主人新口径）。
///
/// <para><b>UI</b>：立绘 / 头像 / 被 UI Mask 裁剪的图一律不挂流光；本类只负责把材质复位为 null
/// （DDOL 复用时清残留），图标流光改走 <c>EquipRarityMaterials.Apply(Image, Rarity?)</c>。</para>
/// <para>原因：SSU Shine 材质不带 UI Mask 的 stencil，挂上后裁剪失效，
/// 2026-11-07 已踩「立绘腿脚露到卡框外」，故立绘/头像/被 UI Mask 裁剪的图一律不再挂流光。</para>
/// <para><b>战斗</b>：脚下金色光环 + 形象流光在 Mercenary 分册（Mercenary.LegendaryFx.cs），
/// 本类只管 UI 侧，不参与。</para>
/// </summary>
public static class MercRarityFx
{
    /// <summary>2026-11-09 新口径：立绘 / 头像 / 被 UI Mask 裁剪的图一律不挂流光，恒定把材质复位为 null（DDOL 复用清残留）；图标流光走 <c>EquipRarityMaterials.Apply(Image, Rarity?)</c>；hireIdOrAssetId 参数保留以兼容 6 处调用点。</summary>
    public static void ApplyPortraitShine(Image img, string hireIdOrAssetId)
    {
        if (img == null) return;
        img.material = null;
    }
}
