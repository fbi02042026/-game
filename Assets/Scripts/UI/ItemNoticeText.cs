using UnityEngine;

/// <summary>
/// 道具/展示位告知窗文案的**唯一拼装出口**（2026-10-27 主人拍板「排版统一一个模板」）。
/// 载体 = StaminaConfirmPopup.ShowNotice 单按钮告知窗（共用默认弹窗预制体），
/// 统一模板：标题 = 道具名（报喜金）→ 说明短句（每行 ≤16 字手动断行，居中，不靠引擎折行）
/// → 空一行 → 数量行（报喜金加粗「持有 ×N」/ 战斗内口径「本局 ×N」）→ 知道了。
/// 背景：正文 460 宽 / 字号 25 ≈ 每行 18 个全角字，长句让引擎随机折行后居中排版碎成渣
/// （2026-10-27 主人截图：天赋石说明挤三行、「持有 ×1」混在文案里）。
/// 只拼文案，不碰任何布局尺寸；数量行用富文本（预制体 Body m_RichText 已开）。
/// 调用点：HeroVaultSystem（角色页背包展示位六类）、BattleUI.Backpack（战斗背包展示位四处）。
/// </summary>
public static class ItemNoticeText
{
    /// <summary>
    /// 重生护符说明（角色页 / 战斗背包两处同语义共用一份，防止两处文案漂移）：
    /// 原句「佣兵阵亡时，点击其变灰的头像消耗 1 枚原地满血复活。/ 战斗内关卡商店有售（50% 概率上架）。」
    /// 超 18 字必被引擎折行，按模板拆成三短行，语义不变。
    /// </summary>
    public const string ReviveCharmDesc =
        "佣兵阵亡时原地满血复活，\n点击变灰头像消耗 1 枚。\n战斗内关卡商店有售（50% 上架）。";

    /// <summary>数量行：报喜金 + 加粗（色值只取 RarityPalette.Gold，不抄 hex）。</summary>
    public static string CountLine(string label, long count)
    {
        string hex = ColorUtility.ToHtmlStringRGB(RarityPalette.Gold);
        return "<color=#" + hex + "><b>" + label + " ×" + count + "</b></color>";
    }

    /// <summary>
    /// 标准模板：说明（可含 \n 多短行，超长行请调用方自己断）+ 空行 + 数量行。
    /// desc 为空时不再多出空行。
    /// </summary>
    public static string Build(string desc, string countLabel, long count)
    {
        string d = (desc ?? "").TrimEnd('\n', '\r', ' ');
        return (d.Length > 0 ? d + "\n" : "") + "\n" + CountLine(countLabel, count);
    }
}
