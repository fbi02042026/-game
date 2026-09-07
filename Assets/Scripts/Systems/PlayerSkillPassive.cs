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
        if (bm.playerSkillEnergy < 0.99f) return;
        bm.TryUsePlayerSkill();
    }
}
