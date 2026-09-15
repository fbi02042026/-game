using UnityEngine;

/// <summary>
/// 玩家主动技改为被动：自动释放，禁止点头像手动放。
/// 雷击奥义仍走 HeroThunderUltimate 满条自动。
/// <para>
/// 2026-09-15：充能槽取消，改<b>纯冷却制</b> —— 开局由 BattleManager 按槽位错峰给初始 CD，
/// 冷却好了按槽序放一个，放完重新进入自己的冷却；任意两次释放之间还要隔 GameConfig.PLAYER_SKILL_GCD 秒，
/// 避免多个技能同时冷却完毕时在同一瞬间全部炸出来。
/// </para>
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
        // 全局间隔没到：直接返回，省掉每帧的 FindReadySkillSlot 遍历（早退闸）
        if (!bm.IsPlayerSkillGcdReady) return;
        // 槽序即释放优先级：多个技能同时就绪时取最靠前的那个。
        // 本方法只负责触发，真正的释放、上冷却与上膛在 SkillCastService.TryUsePlayerSkillSlot。
        if (bm.FindReadySkillSlot() < 0) return;
        bm.TryUsePlayerSkill();
    }
}
