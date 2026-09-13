using UnityEngine;

/// <summary>
/// 玩家主动技改为被动：能量满自动释放，禁止点头像手动放。
/// 雷击奥义仍走 HeroThunderUltimate 满条自动。
/// </summary>
public static class PlayerSkillPassive
{
    /// <summary>永久 false：战斗内不可点放主动技。</summary>
    public const bool AllowManualCast = false;

    public static void TickAutoCast()
    {
        var bm = BattleManager.Instance;
        if (bm == null || !bm.isInBattle || !bm.UnitsCanAct) return;
        if (BattleLootMode.Active) return;
        // V6：能量按技能槽独立 —— 先找「能量满且不在冷却」的槽，没有就本次不放。
        // 槽序即释放优先级：多个技能同时就绪时取最靠前的那个。
        // 本方法只负责触发，真正的释放与扣能量在 SkillCastService.TryUsePlayerSkillSlot。
        if (bm.FindReadySkillSlot() < 0) return;
        bm.TryUsePlayerSkill();
    }
}
