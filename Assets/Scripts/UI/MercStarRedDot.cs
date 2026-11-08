using UnityEngine;

/// <summary>
/// 「有佣兵能升星」入口红点（2026-11-08 需求）。
///
/// 亮在【主界面底部导航「酒馆」按钮】与【酒馆页「佣兵队列」入口图标】上，
/// **不进佣兵队列界面内部**。两处共用 RedDot.Tavern 一个键，一次 Set 同步亮灭：
/// 底部导航的绑定已由 GuildHallUI.BindRedDots 完成，酒馆页那处由 TavernUI.WireClicks 补。
///
/// 判定唯一出口 = HasAnyStarUpReady：升星功能门槛（通第 2 章）过了，且花名册里
/// 任一佣兵走 MercStarSystem.CanStarUp 权威预检（已拥有 / 未满星 / 碎片 / 徽记 / 资金）为真。
/// </summary>
public static class MercStarRedDot
{
    public static bool HasAnyStarUpReady()
    {
        var data = SaveSystem.Instance?.Data;
        if (data == null) return false;
        // 功能没开就别亮：与队列卡「升阶」钮的 CanShowAscendButton 同门槛，不误导玩家
        if (!FeatureUnlock.MercAscendOpen(data)) return false;

        var all = MercRosterDefs.All;
        for (int i = 0; i < all.Count; i++)
        {
            string hireId = all[i].HireId;
            if (string.IsNullOrEmpty(hireId)) continue;
            if (MercStarSystem.CanStarUp(hireId, out _)) return true;
        }
        return false;
    }

    /// <summary>刷新入口红点（登录 / 回城、进酒馆页、升星关窗各调一次，都走这一个出口）。</summary>
    public static void Refresh()
    {
        RedDot.Set(RedDot.Tavern, HasAnyStarUpReady());
    }
}
