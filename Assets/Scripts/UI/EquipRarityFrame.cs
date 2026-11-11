using UnityEngine;

/// <summary>
/// 【2026-11-11 主人拍板「以后装备必走稀有度框体 统一下」】装备稀有度框体图的全局唯一出口。
///
/// 三档装备框美术（Assets/Resources/UI/Battle/zhaungbeitihuan/，主人 2026-10-14 拍板引入）：
///   传奇 → zhaungbeijin（橙金）；史诗/稀有/精良（紫/蓝/绿）→ zhuangbeilan（蓝）；普通 → zhuangbeibai（白）。
/// 150×150 方图、meta 无九宫格 spriteBorder → 使用方一律 Simple + preserveAspect（禁 Sliced 拉伸变形）；
/// 框中心是实心深底 → 必须<b>垫在装备图标之下</b>用（照替换弹窗 Iconbg 用法，压图标之上会盖图）。
///
/// 已落地（装备「展示格」类界面）：战斗装备快捷槽 EquipQuickSlotUI / 装备替换弹窗
/// EquipReplaceConfirmUI / 竞技商店装备类商品 ArenaPopupUI / 邮件详情装备附件格 MailDetailUI /
/// 获得奖励弹窗装备条目 RewardPopupUI。
/// 不适用：背包跨格占位容器（BackpackGridVisual，多格装备是长方形，方框图会留空）——仍走
/// EquipRarityRim 描边，两套边界见各自注释。
/// 取不到资源 LogError 不兜底（铁律：不让缺资源的界面开天窗）。
/// </summary>
public static class EquipRarityFrame
{
    const string Dir = "UI/Battle/zhaungbeitihuan/";

    /// <summary>按装备稀有度取装备框图（三档映射唯一真源；别处禁止再私抄一份）。</summary>
    public static Sprite SpriteOf(Rarity r)
    {
        string name;
        switch (r)
        {
            case Rarity.Legendary:
                name = "zhaungbeijin";   // 只有传奇 → 橙金（主人 2026-10-14 原话口径）
                break;
            case Rarity.Epic:
            case Rarity.Rare:
            case Rarity.Uncommon:
                name = "zhuangbeilan";   // 紫 / 蓝 / 绿 → 蓝
                break;
            default:
                name = "zhuangbeibai";   // 普通 → 白
                break;
        }
        var sp = Resources.Load<Sprite>(Dir + name);
        if (sp == null)
            Debug.LogError($"[EquipRarityFrame] 找不到装备格子框 {Dir + name}，请确认素材已放进 Resources");
        return sp;
    }
}
