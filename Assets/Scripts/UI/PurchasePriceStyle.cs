using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【购买 / 消耗数字的唯一样式出口】2026-11-08 收敛。
///
/// 起因：升星弹窗 shengjieButton/Label 的「×N」是纯白字压在亮黄绿底上，缩放后边缘发糊看不清。
/// 项目里同类价格数字散在各界面各写各的（有的纯白、有的暖金、有的带阴影、有的什么都没有），
/// 以后统一从这里取色 + 补描边，别在业务文件里再抄一份色值或各写一套 Shadow。
///
/// 规范（只管「花了多少」的那个数字，不管资源条余额 / 奖励获得量 / 说明正文）：
///   正常态色 = <see cref="RarityPalette.Gold"/> 全局暖金（不新造色值）；
///   深色描边 = <see cref="RarityPalette.Outline"/>（保证压在亮底上也读得出来）；
///   不足 / 满星 / 已售罄 等状态色由各界面自己的三态逻辑决定，本类不接管。
///
/// 边界（严格遵守）：
///   · 只补 Outline，不动字号 / rect / 文案 / 对齐 / 图片 —— 这些是主人预制体里调好的美术。
///   · 预制体或既有代码已经挂了 Outline / Shadow 的文本：尊重 authored，不叠第二层黑边，直接 return。
///     （Outline 继承自 Shadow，GetComponent&lt;Shadow&gt; 能同时挡住两者）
///   · 已有专用配色（招募卡快照色、战前抽奖懒缓存原色、里程商店富文本亮金）只补描边，颜色一个值不碰。
/// </summary>
public static class PurchasePriceStyle
{
    /// <summary>正常态价格色：转发全局暖金，别在业务文件里写死 RGB。</summary>
    public static Color NormalColor => RarityPalette.Gold;

    /// <summary>描边色：转发全局稀有度表的统一描边色。</summary>
    public static Color OutlineColor => RarityPalette.Outline;

    /// <summary>
    /// 半透浅描边（2026-11-09 升阶按钮「×N」专用）：亮黄绿底上纯黑不透明描边对比太硬。
    /// 中性灰提亮一档 + 50% 透明，色相不变；只有显式传入 Apply 的调用点生效，
    /// 其余购买数字仍走 <see cref="OutlineColor"/>，零影响。
    /// </summary>
    public static readonly Color SoftOutlineColor = new Color(0.20f, 0.20f, 0.20f, 0.5f);

    /// <summary>描边偏移（与 LoadingUI / BattleUI 里已有 Outline 同口径）。</summary>
    public static readonly Vector2 EffectDistance = new Vector2(1.5f, -1.5f);

    /// <summary>
    /// 给购买 / 消耗数字套统一样式。幂等，可放在每次 Refresh 里。
    /// useNormalColor=true 时把文字色设成全局暖金（仅用于原本是纯白、确实看不清的地方）。
    /// outlineColor 只给需要浅描边的亮底数字用（默认 null = 全局深描边）。
    /// </summary>
    public static void Apply(Text label, bool useNormalColor = false, Color? outlineColor = null)
    {
        if (label == null) return;
        if (useNormalColor) label.color = NormalColor;

        // 已有任何阴影 / 描边效果就不叠第二层，尊重预制体与既有实现。
        if (label.GetComponent<Shadow>() != null) return;

        var ol = label.gameObject.AddComponent<Outline>();
        ol.effectColor = outlineColor ?? OutlineColor;
        ol.effectDistance = EffectDistance;
        ol.useGraphicAlpha = true;
    }
}
